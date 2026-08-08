namespace ImportCanonPhotos.Core;

/// <summary>A snapshot of import progress, marshalled to the UI by <see cref="IProgress{T}"/>.</summary>
public sealed record ImportProgress(
    string Phase,
    string CurrentFile,
    int PhaseDone,
    int PhaseTotal,
    int OverallDone,
    int OverallTotal);

/// <summary>Raised for conditions that stop a whole run, as opposed to a single bad file.</summary>
public sealed class ImportException : Exception
{
    public ImportException(string message) : base(message)
    {
    }

    public ImportException(string message, Exception innerException) : base(message, innerException)
    {
    }

    public ImportException()
    {
    }
}

public sealed class ImportResult
{
    public List<string> Imported { get; } = [];

    public List<string> Skipped { get; } = [];

    public List<string> Failed { get; } = [];

    public int DeletedFromSource { get; set; }

    public bool Cancelled { get; set; }
}

/// <summary>
/// Ports the flow of the original ProcessRawPhotos console script, with the three console
/// prompts replaced by settings and per-file failures recorded rather than aborting the run.
/// </summary>
public sealed class ImportEngine
{
    private const string RawExtension = "cr3";

    /// <summary>Camera Uploads holds phone and scanner output, not raw files.</summary>
    private static readonly string[] DropboxExtensions = ["jpg", "mov", "tif"];

    private readonly AppSettings _settings;
    private readonly IProgress<ImportProgress>? _progress;

    private string _phase = string.Empty;
    private int _phaseDone;
    private int _phaseTotal;
    private int _overallDone;
    private int _overallTotal;

    public ImportEngine(AppSettings settings, IProgress<ImportProgress>? progress)
    {
        _settings = settings;
        _progress = progress;
    }

    // ---------------------------------------------------------------- Dropbox

    public Task<ImportResult> ImportFromDropboxAsync(CancellationToken cancellationToken)
    {
        var result = new ImportResult();
        var source = _settings.ExpandedDropboxFolder;

        if (!Directory.Exists(source))
        {
            throw new ImportException($"Dropbox folder not found:{Environment.NewLine}{source}");
        }

        var outputRoot = RequireOutputRoot();

        _overallTotal = DropboxExtensions.Sum(extension => CountFiles(source, extension));
        _overallDone = 0;
        AppLog.Info($"Dropbox import starting from {source} ({_overallTotal} files).");

        try
        {
            foreach (var extension in DropboxExtensions)
            {
                var files = Directory.GetFiles(source, $"*.{extension}", SearchOption.TopDirectoryOnly);
                BeginPhase($"Importing {extension.ToUpperInvariant()} files", files.Length);
                ImportFiles(files, outputRoot, result, cancellationToken);
            }

            if (_settings.DeleteSourceAfterImport)
            {
                // Only the extensions actually imported. The original deleted MP4 and CR3
                // from Camera Uploads too, even though it never imported them from there.
                DeleteSourceFiles(source, DropboxExtensions, "Dropbox", result);
            }
        }
        catch (OperationCanceledException)
        {
            result.Cancelled = true;
        }

        AppLog.Info($"Dropbox import finished. {Summarise(result)}");
        return Task.FromResult(result);
    }

    // ----------------------------------------------------------------- Camera

    public async Task<ImportResult> ImportFromCameraAsync(CancellationToken cancellationToken)
    {
        var result = new ImportResult();
        var dcimRoot = _settings.ExpandedCameraDcimFolder;

        if (!Directory.Exists(dcimRoot))
        {
            throw new ImportException(
                $"No camera or card found at:{Environment.NewLine}{dcimRoot}{Environment.NewLine}{Environment.NewLine}" +
                "Connect the camera or insert the card, then try again.");
        }

        var outputRoot = RequireOutputRoot();

        // The original only ever looked in 100CANON. Every DCIM subfolder is scanned here
        // so 101CANON onwards are not silently missed.
        var cardFolders = Directory.GetDirectories(dcimRoot).OrderBy(folder => folder, StringComparer.OrdinalIgnoreCase).ToList();
        if (cardFolders.Count == 0)
        {
            cardFolders.Add(dcimRoot);
        }

        var converterPath = DngConverter.Locate(_settings.ExpandedAdobeDngConverterPath);
        var rawCount = cardFolders.Sum(folder => CountFiles(folder, RawExtension));
        if (rawCount > 0 && converterPath is null)
        {
            throw new ImportException(
                $"Adobe DNG Converter was not found at:{Environment.NewLine}{_settings.ExpandedAdobeDngConverterPath}" +
                $"{Environment.NewLine}{Environment.NewLine}Set its location in Preferences, or install it, to import raw files.");
        }

        var converter = converterPath is null ? null : new DngConverter(converterPath);

        _overallTotal = cardFolders.Sum(CountCameraWork);
        _overallDone = 0;
        AppLog.Info($"Camera import starting from {dcimRoot}: {cardFolders.Count} folder(s), {_overallTotal} steps.");

        try
        {
            foreach (var cardFolder in cardFolders)
            {
                await ImportCardFolderAsync(cardFolder, outputRoot, converter, result, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            result.Cancelled = true;
        }

        AppLog.Info($"Camera import finished. {Summarise(result)}");
        return result;
    }

    private async Task ImportCardFolderAsync(
        string cardFolder,
        string outputRoot,
        DngConverter? converter,
        ImportResult result,
        CancellationToken cancellationToken)
    {
        var staging = CopyCardToStaging(cardFolder, cancellationToken);

        try
        {
            if (converter is not null)
            {
                await ImportRawAsync(staging, outputRoot, converter, lossy: true, result, cancellationToken);
            }

            var jpgFiles = Directory.GetFiles(staging, "*.jpg", SearchOption.TopDirectoryOnly);
            BeginPhase("Importing JPG files", jpgFiles.Length);
            ImportFiles(jpgFiles, outputRoot, result, cancellationToken);

            var mp4Files = Directory.GetFiles(staging, "*.mp4", SearchOption.TopDirectoryOnly);
            BeginPhase("Importing MP4 files", mp4Files.Length);
            var copiedMp4Files = ImportFiles(mp4Files, outputRoot, result, cancellationToken);

            if (_settings.ReEncodeVideos && copiedMp4Files.Count > 0)
            {
                ReEncodeVideos(copiedMp4Files, result, cancellationToken);
            }

            if (converter is not null && _settings.ImportLossless)
            {
                await ImportRawAsync(staging, outputRoot, converter, lossy: false, result, cancellationToken);
            }

            if (_settings.DeleteSourceAfterImport)
            {
                DeleteSourceFiles(cardFolder, [RawExtension, "jpg", "mp4"], "memory card", result);
            }
        }
        finally
        {
            // The original leaked this folder whenever a conversion threw.
            TryDeleteDirectory(staging);
        }
    }

    /// <summary>
    /// Card files are staged locally first: card readers power down during slow DNG
    /// conversions, after which reading the card fails with "A device which does not
    /// exist was specified". Timestamps are carried across because the date fallback
    /// depends on them.
    /// </summary>
    private string CopyCardToStaging(string cardFolder, CancellationToken cancellationToken)
    {
        var staging = Path.Combine(AppPaths.StagingFolder, Path.GetFileName(cardFolder.TrimEnd(Path.DirectorySeparatorChar)));
        TryDeleteDirectory(staging);
        Directory.CreateDirectory(staging);

        var files = Directory.GetFiles(cardFolder, "*", SearchOption.TopDirectoryOnly);
        BeginPhase($"Copying {Path.GetFileName(cardFolder)} from the card", files.Length);
        AppLog.Info($"Copying {files.Length} files from {cardFolder} to {staging}.");

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Report(file);

            try
            {
                var destination = Path.Combine(staging, Path.GetFileName(file));
                File.Copy(file, destination, true);
                File.SetCreationTimeUtc(destination, File.GetCreationTimeUtc(file));
                File.SetLastWriteTimeUtc(destination, File.GetLastWriteTimeUtc(file));
            }
            catch (Exception ex)
            {
                AppLog.Error($"Could not stage {file}.", ex);
            }
            finally
            {
                Advance();
            }
        }

        return staging;
    }

    // ------------------------------------------------------------ File import

    /// <summary>
    /// Copies files into their dated destination folder. Never moves, and never renames.
    /// Returns the destinations actually written, which is what feeds the re-encode step.
    /// </summary>
    private List<string> ImportFiles(
        IReadOnlyList<string> files,
        string outputRoot,
        ImportResult result,
        CancellationToken cancellationToken)
    {
        var copied = new List<string>();

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Report(file);

            try
            {
                var date = PhotoDate.GetDateFromImage(file);
                var folder = OutputPathBuilder.CreateOutputFolder(outputRoot, file, date);
                var destination = Path.Combine(folder, Path.GetFileName(file));

                if (File.Exists(destination) && !_settings.OverwriteExisting)
                {
                    AppLog.Info($"Skipping {file}: {destination} already exists.");
                    result.Skipped.Add(destination);
                    continue;
                }

                AppLog.Info($"Copying {file} -> {destination}");
                File.Copy(file, destination, true);
                result.Imported.Add(destination);
                copied.Add(destination);
            }
            catch (Exception ex)
            {
                AppLog.Error($"Failed to import {file}.", ex);
                result.Failed.Add(file);
            }
            finally
            {
                Advance();
            }
        }

        return copied;
    }

    private async Task ImportRawAsync(
        string sourceFolder,
        string outputRoot,
        DngConverter converter,
        bool lossy,
        ImportResult result,
        CancellationToken cancellationToken)
    {
        var suffix = lossy ? string.Empty : "-lossless";
        var rawFiles = Directory.GetFiles(sourceFolder, $"*.{RawExtension}", SearchOption.TopDirectoryOnly);

        BeginPhase(lossy ? "Compressing CR3 to DNG" : "Compressing CR3 to lossless DNG", rawFiles.Length);

        foreach (var rawFile in rawFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Report(rawFile);

            try
            {
                var date = PhotoDate.GetDateFromImage(rawFile);
                var folder = OutputPathBuilder.CreateOutputFolder(outputRoot, rawFile, date, suffix);
                var expected = DngConverter.ExpectedOutputFile(folder, rawFile);

                if (File.Exists(expected) && !_settings.OverwriteExisting)
                {
                    AppLog.Info($"Skipping {rawFile}: {expected} already exists.");
                    result.Skipped.Add(expected);
                    continue;
                }

                AppLog.Info($"Converting {rawFile} -> {folder}");
                var dngFile = await converter.ConvertAsync(rawFile, folder, lossy, cancellationToken);
                result.Imported.Add(dngFile);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // One unconvertible file must not abort the whole import, which is what
                // the original did.
                AppLog.Error($"Failed to convert {rawFile}.", ex);
                result.Failed.Add(rawFile);
            }
            finally
            {
                Advance();
            }
        }
    }

    private void ReEncodeVideos(IReadOnlyList<string> copiedMp4Files, ImportResult result, CancellationToken cancellationToken)
    {
        BeginPhase("Re-encoding videos", copiedMp4Files.Count);

        VideoEncoder encoder;
        try
        {
            encoder = new VideoEncoder();
        }
        catch (Exception ex)
        {
            AppLog.Error("Could not start the video encoder; skipping re-encoding.", ex);
            _overallDone += copiedMp4Files.Count;
            return;
        }

        foreach (var mp4File in copiedMp4Files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Report(mp4File);

            try
            {
                var folder = OutputPathBuilder.ReEncodedFolderFor(mp4File);
                Directory.CreateDirectory(folder);
                var destination = Path.Combine(folder, Path.GetFileName(mp4File));

                if (File.Exists(destination) && !_settings.OverwriteExisting)
                {
                    AppLog.Info($"Skipping re-encode of {mp4File}: {destination} already exists.");
                    result.Skipped.Add(destination);
                    continue;
                }

                AppLog.Info($"Re-encoding {mp4File} -> {destination}");
                encoder.Encode(mp4File, destination);
                result.Imported.Add(destination);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                AppLog.Error($"Failed to re-encode {mp4File}.", ex);
                result.Failed.Add(mp4File);
            }
            finally
            {
                Advance();
            }
        }
    }

    // ---------------------------------------------------------------- Cleanup

    private void DeleteSourceFiles(string folder, IReadOnlyList<string> extensions, string label, ImportResult result)
    {
        AppLog.Info($"Deleting imported files from {label} ({folder}).");

        foreach (var extension in extensions)
        {
            foreach (var file in Directory.GetFiles(folder, $"*.{extension}", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    File.Delete(file);
                    result.DeletedFromSource++;
                    AppLog.Info($"Deleted {file}");
                }
                catch (Exception ex)
                {
                    AppLog.Error($"Could not delete {file}.", ex);
                }
            }
        }
    }

    private static void TryDeleteDirectory(string folder)
    {
        try
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn($"Could not remove staging folder {folder}: {ex.Message}");
        }
    }

    // --------------------------------------------------------------- Plumbing

    private string RequireOutputRoot()
    {
        var outputRoot = _settings.ExpandedOutputFolder;

        if (string.IsNullOrWhiteSpace(outputRoot))
        {
            throw new ImportException("No output folder is configured. Set one in Preferences.");
        }

        try
        {
            Directory.CreateDirectory(outputRoot);
        }
        catch (Exception ex)
        {
            throw new ImportException($"Cannot write to the output folder:{Environment.NewLine}{outputRoot}", ex);
        }

        return outputRoot;
    }

    private static int CountFiles(string folder, string extension) =>
        Directory.Exists(folder)
            ? Directory.GetFiles(folder, $"*.{extension}", SearchOption.TopDirectoryOnly).Length
            : 0;

    /// <summary>Number of progress steps one card folder represents.</summary>
    private int CountCameraWork(string folder)
    {
        var staged = Directory.GetFiles(folder, "*", SearchOption.TopDirectoryOnly).Length;
        var raw = CountFiles(folder, RawExtension);
        var jpg = CountFiles(folder, "jpg");
        var mp4 = CountFiles(folder, "mp4");

        return staged
            + (raw * (_settings.ImportLossless ? 2 : 1))
            + jpg
            + (mp4 * (_settings.ReEncodeVideos ? 2 : 1));
    }

    private void BeginPhase(string phase, int total)
    {
        _phase = phase;
        _phaseDone = 0;
        _phaseTotal = total;
    }

    private void Report(string currentFile) =>
        _progress?.Report(new ImportProgress(
            _phase,
            Path.GetFileName(currentFile),
            _phaseDone,
            _phaseTotal,
            _overallDone,
            _overallTotal));

    private void Advance()
    {
        _phaseDone++;
        _overallDone++;
        _progress?.Report(new ImportProgress(
            _phase,
            string.Empty,
            _phaseDone,
            _phaseTotal,
            _overallDone,
            _overallTotal));
    }

    private static string Summarise(ImportResult result) =>
        $"{result.Imported.Count} imported, {result.Skipped.Count} skipped, " +
        $"{result.Failed.Count} failed, {result.DeletedFromSource} deleted from source" +
        (result.Cancelled ? " (cancelled)" : string.Empty);
}
