using System.Globalization;
using System.IO;

namespace ImportCanonPhotos.Core;

/// <summary>
/// Works out which day a file belongs to, matching the original script's behaviour.
/// </summary>
public static class PhotoDate
{
    /// <summary>
    /// The original matched the EXIF tag whose display name is exactly "Date/Time"
    /// (IFD0 0x0132), not "Date/Time Original". Kept as-is so files land in the same
    /// folders they always have.
    /// </summary>
    private const string DateTimeTagName = "Date/Time";

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
