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

    [Theory]
    [InlineData("2019-12-05 19.03.25.jpg", 2019, 12, 5, 19, 3, 25)]
    [InlineData("2022-03-20 22.39.09-1.jpg", 2022, 3, 20, 22, 39, 9)]
    [InlineData("2019-04-20 14.16.18.mov", 2019, 4, 20, 14, 16, 18)]
    [InlineData("2025-03-25 22.20.09.jpg", 2025, 3, 25, 22, 20, 9)]
    [InlineData("2024-06-08.jpg", 2024, 6, 8, 0, 0, 0)]
    public void TryParseFileNameDate_DropboxName_Parses(
        string fileName, int year, int month, int day, int hour, int minute, int second)
    {
        var parsed = PhotoDate.TryParseFileNameDate(fileName, out var value);

        Assert.True(parsed);
        Assert.Equal(new DateTime(year, month, day, hour, minute, second), value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("IMG_1234.JPG")]
    [InlineData("2026-13-45 10.00.00.jpg")]
    [InlineData("2026-02-30.jpg")]
    [InlineData("20260808_142311.jpg")]
    [InlineData("2026-08-081.jpg")]
    public void TryParseFileNameDate_NoUsableDate_ReturnsFalse(string? fileName)
    {
        var parsed = PhotoDate.TryParseFileNameDate(fileName, out _);

        Assert.False(parsed);
    }
}
