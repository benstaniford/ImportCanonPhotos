using System.Globalization;
using ImportCanonPhotos.Core;
using Xunit;

namespace ImportCanonPhotos.Tests;

public class OutputPathBuilderTests
{
    private const string Root = @"C:\Photos";

    private static readonly DateTime August8th = new(2026, 8, 8, 14, 23, 11);

    [Fact]
    public void BuildOutputFolder_Jpg_UsesLowercaseExtensionLeaf()
    {
        var month = August8th.ToString("MMMM", CultureInfo.CurrentCulture);

        var folder = OutputPathBuilder.BuildOutputFolder(Root, @"D:\DCIM\IMG_1234.JPG", August8th);

        Assert.Equal($@"{Root}\2026\{month}\2026-08-08\jpg", folder);
    }

    [Fact]
    public void BuildOutputFolder_Cr3_LandsInDngFolder()
    {
        var month = August8th.ToString("MMMM", CultureInfo.CurrentCulture);

        var folder = OutputPathBuilder.BuildOutputFolder(Root, @"D:\DCIM\IMG_1234.CR3", August8th);

        Assert.Equal($@"{Root}\2026\{month}\2026-08-08\dng", folder);
    }

    [Fact]
    public void BuildOutputFolder_Cr3WithLosslessSuffix_LandsInDngLosslessFolder()
    {
        var month = August8th.ToString("MMMM", CultureInfo.CurrentCulture);

        var folder = OutputPathBuilder.BuildOutputFolder(Root, @"D:\DCIM\IMG_1234.CR3", August8th, "-lossless");

        Assert.Equal($@"{Root}\2026\{month}\2026-08-08\dng-lossless", folder);
    }

    [Fact]
    public void BuildOutputFolder_UppercaseMp4_IsLowercased()
    {
        var folder = OutputPathBuilder.BuildOutputFolder(Root, @"D:\DCIM\MVI_0001.MP4", August8th);

        Assert.EndsWith(@"\mp4", folder, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildOutputFolder_SingleDigitMonthAndDay_AreZeroPadded()
    {
        var date = new DateTime(2026, 1, 5, 9, 0, 0);
        var month = date.ToString("MMMM", CultureInfo.CurrentCulture);

        var folder = OutputPathBuilder.BuildOutputFolder(Root, "IMG_0001.jpg", date);

        Assert.Equal($@"{Root}\2026\{month}\2026-01-05\jpg", folder);
    }

    [Fact]
    public void BuildOutputFolder_OutputRootContainingCr3_IsNotRewritten()
    {
        // The original script string-replaced "\cr3" across the whole path, which would
        // have turned this root into D:\dng\Photos.
        var folder = OutputPathBuilder.BuildOutputFolder(@"D:\cr3\Photos", "IMG_1234.CR3", August8th);

        Assert.StartsWith(@"D:\cr3\Photos\", folder, StringComparison.Ordinal);
        Assert.EndsWith(@"\dng", folder, StringComparison.Ordinal);
    }

    [Fact]
    public void ReEncodedFolderFor_SitsAlongsideTheMp4Folder()
    {
        var copied = $@"{Root}\2026\August\2026-08-08\mp4\MVI_0001.MP4";

        var folder = OutputPathBuilder.ReEncodedFolderFor(copied);

        Assert.Equal($@"{Root}\2026\August\2026-08-08\re-encoded-mp4", folder);
    }
}
