using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace ImportCanonPhotos.Core;

/// <summary>
/// Works out which day a file belongs to, matching the original script's behaviour.
/// </summary>
public static partial class PhotoDate
{
    /// <summary>
    /// The original matched the EXIF tag whose display name is exactly "Date/Time"
    /// (IFD0 0x0132), not "Date/Time Original". Kept as-is so files land in the same
    /// folders they always have.
    /// </summary>
    private const string DateTimeTagName = "Date/Time";

    /// <summary>
    /// Dropbox Camera Uploads names files "yyyy-MM-dd HH.mm.ss.ext", with "-1", "-2"...
    /// appended on collisions. That name is set from the capture time when Dropbox uploads
    /// the file, so it survives files being re-synced with a fresh timestamp and no EXIF.
    /// </summary>
    [GeneratedRegex(@"^(?<date>\d{4}-\d{2}-\d{2})(?: (?<time>\d{2}\.\d{2}\.\d{2}))?(?!\d)")]
    private static partial Regex FileNameDatePattern();

    /// <summary>
    /// For Dropbox uploads: the date in the filename wins, then EXIF, then file creation time.
    /// </summary>
    public static DateTime GetDateFromFileNameOrImage(string file)
    {
        return TryParseFileNameDate(Path.GetFileName(file), out var fileNameDate)
            ? fileNameDate
            : GetDateFromImage(file);
    }

    internal static bool TryParseFileNameDate(string? fileName, out DateTime value)
    {
        value = default;

        if (string.IsNullOrEmpty(fileName))
        {
            return false;
        }

        var match = FileNameDatePattern().Match(fileName);
        if (!match.Success)
        {
            return false;
        }

        var time = match.Groups["time"];
        return time.Success
            ? DateTime.TryParseExact(
                $"{match.Groups["date"].Value} {time.Value}",
                "yyyy-MM-dd HH.mm.ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out value)
            : DateTime.TryParseExact(
                match.Groups["date"].Value,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out value);
    }

    public static DateTime GetDateFromImage(string imageFile)
    {
        try
        {
            // Fully qualified: MetadataExtractor.Directory would otherwise collide with System.IO.Directory.
            foreach (var directory in MetadataExtractor.ImageMetadataReader.ReadMetadata(imageFile))
            {
                foreach (var tag in directory.Tags)
                {
                    if (tag.Name == DateTimeTagName && TryParseExifDateTime(tag.Description, out var exifDate))
                    {
                        return exifDate;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn($"No usable EXIF date in {imageFile} ({ex.GetType().Name}); falling back to file creation time.");
        }

        return File.GetCreationTime(imageFile);
    }

    /// <summary>
    /// EXIF reports "yyyy:MM:dd HH:mm:ss". Patching the two colons in the date part to
    /// slashes makes it parseable, which is what the original did.
    /// </summary>
    internal static bool TryParseExifDateTime(string? description, out DateTime value)
    {
        value = default;

        if (description is null || description.Length < 10)
        {
            return false;
        }

        var chars = description.ToCharArray();
        chars[4] = '/';
        chars[7] = '/';

        return DateTime.TryParse(
            new string(chars),
            CultureInfo.CurrentCulture,
            DateTimeStyles.None,
            out value);
    }
}
