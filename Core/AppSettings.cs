using System.Text.Json;
using System.Text.Json.Serialization;

namespace ImportCanonPhotos.Core;

/// <summary>
/// User preferences. The defaults reproduce the constants from the original
/// ProcessRawPhotos console script, with %USERPROFILE% substituted for the literal
/// C:\Users\Ben so the app survives a different account name.
/// </summary>
public sealed class AppSettings
{
    /// <summary>DCIM root on the camera or card reader. Every subfolder is imported.</summary>
    public string CameraDcimFolder { get; set; } = @"E:\DCIM";

    public string DropboxFolder { get; set; } = @"%USERPROFILE%\Dropbox\Camera Uploads";

    public string OutputFolder { get; set; } = @"%USERPROFILE%\OneDrive - BeyondTrust Corporation\Photos";

    public string AdobeDngConverterPath { get; set; } = @"C:\Program Files\Adobe\Adobe DNG Converter\Adobe DNG Converter.exe";

    /// <summary>Run a second, lossless DNG pass into a parallel "dng-lossless" folder.</summary>
    public bool ImportLossless { get; set; }

    /// <summary>Delete imported files from the card / Dropbox afterwards. Permanent — no recycle bin.</summary>
    public bool DeleteSourceAfterImport { get; set; }

    /// <summary>Overwrite a file that already exists at the destination, rather than skipping it.</summary>
    public bool OverwriteExisting { get; set; }

    /// <summary>Also write an ffmpeg-shrunk copy of each MP4 into "re-encoded-mp4".</summary>
    public bool ReEncodeVideos { get; set; } = true;

    [JsonIgnore]
    public string ExpandedCameraDcimFolder => Expand(CameraDcimFolder);

    [JsonIgnore]
    public string ExpandedDropboxFolder => Expand(DropboxFolder);

    [JsonIgnore]
    public string ExpandedOutputFolder => Expand(OutputFolder);

    [JsonIgnore]
    public string ExpandedAdobeDngConverterPath => Expand(AdobeDngConverterPath);

    public static string Expand(string path) =>
        string.IsNullOrWhiteSpace(path) ? string.Empty : Environment.ExpandEnvironmentVariables(path).Trim();

    public AppSettings Clone() => new()
    {
        CameraDcimFolder = CameraDcimFolder,
        DropboxFolder = DropboxFolder,
        OutputFolder = OutputFolder,
        AdobeDngConverterPath = AdobeDngConverterPath,
        ImportLossless = ImportLossless,
        DeleteSourceAfterImport = DeleteSourceAfterImport,
        OverwriteExisting = OverwriteExisting,
        ReEncodeVideos = ReEncodeVideos,
    };
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AppSettings))]
internal sealed partial class SettingsJsonContext : JsonSerializerContext
{
}

/// <summary>Loads and saves <see cref="AppSettings"/> as JSON under %LOCALAPPDATA%.</summary>
public static class SettingsStore
{
    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(AppPaths.SettingsFile))
            {
                var json = File.ReadAllText(AppPaths.SettingsFile);
                var loaded = JsonSerializer.Deserialize(json, SettingsJsonContext.Default.AppSettings);
                if (loaded is not null)
                {
                    return loaded;
                }
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("Could not read settings; falling back to defaults.", ex);
        }

        var defaults = new AppSettings();
        Save(defaults);
        return defaults;
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.RootFolder);
            var json = JsonSerializer.Serialize(settings, SettingsJsonContext.Default.AppSettings);
            File.WriteAllText(AppPaths.SettingsFile, json);
        }
        catch (Exception ex)
        {
            AppLog.Error("Could not save settings.", ex);
        }
    }
}
