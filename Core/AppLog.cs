using System.Globalization;
using System.Text;

namespace ImportCanonPhotos.Core;

/// <summary>
/// Minimal thread-safe file log. The original script only wrote to the console, which a
/// tray app has none of, so every line it printed becomes a log entry here instead.
/// </summary>
public static class AppLog
{
    private const long MaxBytes = 5 * 1024 * 1024;

    private static readonly System.Threading.Lock Gate = new();

    public static string LogFilePath => AppPaths.LogFile;

    public static void Info(string message) => Write("INFO ", message);

    public static void Warn(string message) => Write("WARN ", message);

    public static void Error(string message, Exception? exception = null) =>
        Write("ERROR", exception is null ? message : $"{message} :: {exception}");

    private static void Write(string level, string message)
    {
        var line = string.Create(
            CultureInfo.InvariantCulture,
            $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {level} {message}{Environment.NewLine}");

        lock (Gate)
        {
            try
            {
                Directory.CreateDirectory(AppPaths.LogFolder);
                RollIfTooLarge();
                File.AppendAllText(AppPaths.LogFile, line, Encoding.UTF8);
            }
            catch (Exception)
            {
                // Logging must never take the app down.
            }
        }
    }

    private static void RollIfTooLarge()
    {
        var file = new FileInfo(AppPaths.LogFile);
        if (!file.Exists || file.Length < MaxBytes)
        {
            return;
        }

        var archive = Path.Combine(AppPaths.LogFolder, "import.1.log");
        if (File.Exists(archive))
        {
            File.Delete(archive);
        }

        File.Move(AppPaths.LogFile, archive);
    }
}
