using ImportCanonPhotos.Core;
using Xunit;

namespace ImportCanonPhotos.Tests;

public class AppSettingsTests
{
    /// <summary>
    /// A tripwire: these are the constants from the original ProcessRawPhotos script and
    /// should not drift without a deliberate decision.
    /// </summary>
    [Fact]
    public void Defaults_MatchTheOriginalScript()
    {
        var settings = new AppSettings();

        Assert.Equal(@"E:\DCIM", settings.CameraDcimFolder);
        Assert.Equal(@"%USERPROFILE%\Dropbox\Camera Uploads", settings.DropboxFolder);
        Assert.Equal(@"%USERPROFILE%\OneDrive - BeyondTrust Corporation\Photos", settings.OutputFolder);
        Assert.Equal(
            @"C:\Program Files\Adobe\Adobe DNG Converter\Adobe DNG Converter.exe",
            settings.AdobeDngConverterPath);
    }

    [Fact]
    public void Defaults_DestructiveOptionsAreOff()
    {
        var settings = new AppSettings();

        // These two replace console prompts that were answered per run; deleting from a
        // card is permanent, so neither may default to on.
        Assert.False(settings.DeleteSourceAfterImport);
        Assert.False(settings.OverwriteExisting);
        Assert.False(settings.ImportLossless);

        // The original always re-encoded videos.
        Assert.True(settings.ReEncodeVideos);
    }

    [Fact]
    public void Expand_ResolvesEnvironmentVariables()
    {
        var expanded = AppSettings.Expand(@"%USERPROFILE%\Dropbox\Camera Uploads");

        Assert.DoesNotContain("%USERPROFILE%", expanded, StringComparison.Ordinal);
        Assert.EndsWith(@"\Dropbox\Camera Uploads", expanded, StringComparison.Ordinal);
    }

    [Fact]
    public void Expand_EmptyInput_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, AppSettings.Expand("   "));
    }

    [Fact]
    public void Clone_CopiesEveryValueAndIsIndependent()
    {
        var original = new AppSettings
        {
            CameraDcimFolder = @"F:\DCIM",
            DropboxFolder = @"F:\Dropbox",
            OutputFolder = @"F:\Photos",
            AdobeDngConverterPath = @"F:\dng.exe",
            ImportLossless = true,
            DeleteSourceAfterImport = true,
            OverwriteExisting = true,
            ReEncodeVideos = false,
        };

        var clone = original.Clone();
        clone.OutputFolder = @"G:\Elsewhere";

        Assert.Equal(@"F:\DCIM", clone.CameraDcimFolder);
        Assert.Equal(@"F:\Dropbox", clone.DropboxFolder);
        Assert.Equal(@"F:\dng.exe", clone.AdobeDngConverterPath);
        Assert.True(clone.ImportLossless);
        Assert.True(clone.DeleteSourceAfterImport);
        Assert.True(clone.OverwriteExisting);
        Assert.False(clone.ReEncodeVideos);

        Assert.Equal(@"F:\Photos", original.OutputFolder);
    }
}
