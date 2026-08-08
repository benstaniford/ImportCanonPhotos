using System.Diagnostics;

namespace ImportCanonPhotos.Core;

public sealed class DngConversionException : Exception
{
    public DngConversionException(string message) : base(message)
    {
    }

    public DngConversionException(string message, Exception innerException) : base(message, innerException)
    {
    }

    public DngConversionException()
    {
    }
}

/// <summary>Drives Adobe DNG Converter one file at a time, as the original script did.</summary>
public sealed class DngConverter
{
    private static readonly string[] FallbackPaths =
    [
        @"C:\Program Files\Adobe\Adobe DNG Converter\Adobe DNG Converter.exe",
        @"C:\Program Files (x86)\Adobe\Adobe DNG Converter\Adobe DNG Converter.exe",
    ];

    private readonly string _converterPath;

    public DngConverter(string converterPath) => _converterPath = converterPath;

    /// <summary>
    /// Returns the configured converter path if it exists, else a known install location,
    /// else null. The original assumed the path and died with a Win32Exception mid-import.
    /// </summary>
    public static string? Locate(string configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath) && File.Exists(configuredPath))
        {
            return configuredPath;
        }

        foreach (var candidate in FallbackPaths)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    /// -c produces a compressed DNG and, on Windows, stops the converter showing its GUI.
    /// -fl embeds fast-load data. -lossy is the lossy pass.
    /// </summary>
    internal static string BuildArguments(string outputFolder, string rawFile, bool lossy)
    {
        var switches = lossy ? "-fl -c -lossy" : "-fl -c";
        return $"{switches} -d \"{outputFolder}\" \"{rawFile}\"";
    }

    internal static string ExpectedOutputFile(string outputFolder, string rawFile) =>
        Path.Combine(outputFolder, Path.GetFileNameWithoutExtension(rawFile) + ".dng");

    /// <summary>Converts one raw file, returning the path of the DNG that was produced.</summary>
    public async Task<string> ConvertAsync(
        string rawFile,
        string outputFolder,
        bool lossy,
        CancellationToken cancellationToken)
    {
        var expectedOutput = ExpectedOutputFile(outputFolder, rawFile);
        var arguments = BuildArguments(outputFolder, rawFile, lossy);

        var startInfo = new ProcessStartInfo(_converterPath, arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(startInfo)
            ?? throw new DngConversionException($"Could not start Adobe DNG Converter for {rawFile}.");

        await process.WaitForExitAsync(cancellationToken);

        // The original judged success purely on the output file existing, which meant a
        // stale DNG from a previous run masked a failed conversion. Check both.
        if (process.ExitCode != 0)
        {
            throw new DngConversionException(
                $"Adobe DNG Converter exited with code {process.ExitCode} converting {rawFile}.");
        }

        if (!File.Exists(expectedOutput))
        {
            throw new DngConversionException($"Adobe DNG Converter produced no output for {rawFile}.");
        }

        return expectedOutput;
    }
}
