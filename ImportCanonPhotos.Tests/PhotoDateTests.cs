using ImportCanonPhotos.Core;
using Xunit;

namespace ImportCanonPhotos.Tests;

public class PhotoDateTests
{
    [Fact]
    public void TryParseExifDateTime_StandardExifValue_Parses()
    {
        var parsed = PhotoDate.TryParseExifDateTime("2026:08:08 14:23:11", out var value);

        Assert.True(parsed);
        Assert.Equal(new DateTime(2026, 8, 8, 14, 23, 11), value);
    }

    [Fact]
    public void TryParseExifDateTime_DateOnly_Parses()
    {
        var parsed = PhotoDate.TryParseExifDateTime("2026:01:05", out var value);

        Assert.True(parsed);
        Assert.Equal(new DateTime(2026, 1, 5), value.Date);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData("2026:08")]
    [InlineData("2026:13:45 99:99:99")]
    public void TryParseExifDateTime_UnusableValue_ReturnsFalseWithoutThrowing(string? description)
    {
        // The original patched characters 4 and 7 unconditionally, so anything shorter than
        // eight characters threw IndexOutOfRangeException into a blanket catch.
        var parsed = PhotoDate.TryParseExifDateTime(description, out _);

        Assert.False(parsed);
    }
}
