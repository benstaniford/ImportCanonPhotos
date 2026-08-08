using ImportCanonPhotos.Core;
using Xunit;

namespace ImportCanonPhotos.Tests;

public class DngConverterTests
{
    private const string OutputFolder = @"C:\Photos\2026\August\2026-08-08\dng";
    private const string RawFile = @"C:\Temp\ImportCanonPhotos\100CANON\IMG_1234.CR3";

    [Fact]
    public void BuildArguments_Lossy_IncludesLossySwitch()
    {
        var arguments = DngConverter.BuildArguments(OutputFolder, RawFile, lossy: true);

        Assert.Equal($"-fl -c -lossy -d \"{OutputFolder}\" \"{RawFile}\"", arguments);
    }

    [Fact]
    public void BuildArguments_Lossless_OmitsLossySwitch()
    {
        var arguments = DngConverter.BuildArguments(OutputFolder, RawFile, lossy: false);

        Assert.Equal($"-fl -c -d \"{OutputFolder}\" \"{RawFile}\"", arguments);
    }

    [Fact]
    public void BuildArguments_AlwaysPassesDashC()
    {
        // Without -c the converter shows its GUI on Windows and never exits, which would
        // hang the import forever.
        Assert.Contains("-c", DngConverter.BuildArguments(OutputFolder, RawFile, lossy: true), StringComparison.Ordinal);
        Assert.Contains("-c", DngConverter.BuildArguments(OutputFolder, RawFile, lossy: false), StringComparison.Ordinal);
    }

    [Fact]
    public void ExpectedOutputFile_SwapsRawExtensionForDng()
    {
        var expected = DngConverter.ExpectedOutputFile(OutputFolder, RawFile);

        Assert.Equal(Path.Combine(OutputFolder, "IMG_1234.dng"), expected);
    }

    [Fact]
    public void Locate_ConfiguredPathThatExists_IsReturned()
    {
        var temporary = Path.Combine(Path.GetTempPath(), $"dngconv-{Guid.NewGuid():N}.exe");
        File.WriteAllText(temporary, string.Empty);

        try
        {
            Assert.Equal(temporary, DngConverter.Locate(temporary));
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    [Fact]
    public void Locate_MissingEverywhere_ReturnsNull()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"no-such-converter-{Guid.NewGuid():N}.exe");

        // Only meaningful when Adobe DNG Converter is not installed on the build agent.
        var located = DngConverter.Locate(missing);

        Assert.True(located is null || File.Exists(located));
    }
}
