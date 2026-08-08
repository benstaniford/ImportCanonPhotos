using System.IO;

namespace ImportCanonPhotos.Core;

/// <summary>
/// Everything the app writes lives under %LOCALAPPDATA%\ImportCanonPhotos. The app is
/// installed to Program Files and runs asInvoker, so its install directory is read-only.
/// </summary>
public static class AppPaths
{
    public static string RootFolder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ImportCanonPhotos");

    public static string SettingsFile => Path.Combine(RootFolder, "settings.json");

    public static string LogFolder => Path.Combine(RootFolder, "logs");

    public static string LogFile => Path.Combine(LogFolder, "import.log");

    /// <summary>
    /// NReco.VideoConverter extracts its embedded ffmpeg.exe here. It must not be allowed
    /// to default to somewhere under Program Files, which the app cannot write to.
    /// </summary>
    public static string FfmpegFolder => Path.Combine(RootFolder, "ffmpeg");

    /// <summary>
    /// Card files are staged here before conversion, because card readers power down
    /// during slow DNG conversions and re-reading the card then fails with
    /// "A device which does not exist was specified".
    /// </summary>
    public static string StagingFolder => Path.Combine(Path.GetTempPath(), "ImportCanonPhotos");
}
