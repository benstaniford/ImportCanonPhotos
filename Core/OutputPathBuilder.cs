using System.Globalization;
using System.IO;

namespace ImportCanonPhotos.Core;

/// <summary>
/// Reproduces the output-folder convention of the original ProcessRawPhotos script exactly:
/// <c>{root}\{yyyy}\{MMMM}\{yyyy-MM-dd}\{ext}</c>, e.g.
/// <c>...\Photos\2026\August\2026-08-08\jpg</c>.
/// </summary>
public static class OutputPathBuilder
{
    /// <summary>
    /// Builds (but does not create) the destination folder for a file.
    /// </summary>
    /// <param name="suffix">
    /// "-lossless" for the second DNG pass. It is appended <em>before</em> the cr3-to-dng
    /// replacement below, which is what turns "\cr3-lossless" into "\dng-lossless".
    /// </param>
    public static string BuildOutputFolder(string outputRoot, string imageFile, DateTime date, string suffix = "")
    {
        var extension = Path.GetExtension(imageFile).ToLowerInvariant().Trim('.');

        // Raw files are converted, so a ".cr3" file lands in a "dng" folder. The original
        // did this with an unanchored Replace(@"\cr3", @"\dng") over the whole path, which
        // would also rewrite an output root such as D:\cr3\Photos. Mapping just the leaf
        // gives identical results for every sane root, without that trap.
        var leaf = extension == "cr3" ? "dng" : extension;

        if (suffix.Length > 0)
        {
            leaf += suffix;
        }

        var month = date.ToString("MMMM", CultureInfo.CurrentCulture);
        var day = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        return string.Create(
            CultureInfo.InvariantCulture,
            $@"{outputRoot}\{date.Year}\{month}\{day}\{leaf}");
    }

    /// <summary>Builds the folder and creates it on disk.</summary>
    public static string CreateOutputFolder(string outputRoot, string imageFile, DateTime date, string suffix = "")
    {
        var folder = BuildOutputFolder(outputRoot, imageFile, date, suffix);
        Directory.CreateDirectory(folder);
        return folder;
    }

    /// <summary>
    /// The "re-encoded-mp4" folder sits alongside the "mp4" folder for the same day, so it
    /// is derived from an already-copied MP4 by stepping up one level.
    /// </summary>
    public static string ReEncodedFolderFor(string copiedMp4File)
    {
        var dayFolder = Directory.GetParent(copiedMp4File)?.Parent
            ?? throw new InvalidOperationException($"Cannot locate the day folder for {copiedMp4File}.");
        return Path.Combine(dayFolder.FullName, "re-encoded-mp4");
    }
}
