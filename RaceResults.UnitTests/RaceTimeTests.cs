using RaceResults.Web.Services;

namespace RaceResults.UnitTests;

public class RaceTimeTests
{
    [Theory]
    [InlineData("17:51", 0, 17, 51)]      // mm:ss
    [InlineData("00:17:51", 0, 17, 51)]   // hh:mm:ss
    [InlineData("1:05:09", 1, 5, 9)]      // h:mm:ss
    [InlineData("75:30", 1, 15, 30)]      // mm:ss with minutes over 59
    [InlineData("00:20:00.5", 0, 20, 0)]  // fractional seconds tolerated
    public void TryParse_AcceptsCommonFormats(string raw, int h, int m, int s)
    {
        Assert.True(RaceTime.TryParse(raw, out var value));
        Assert.Equal(h, (int)value.TotalHours);
        Assert.Equal(m, value.Minutes);
        Assert.Equal(s, value.Seconds);
    }

    [Theory]
    [InlineData("00:2X:99")]   // non-numeric + impossible
    [InlineData("12:75")]      // seconds >= 60
    [InlineData("1:75:00")]    // minutes >= 60 in h:mm:ss
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("10")]         // single component is ambiguous → rejected
    [InlineData("1:2:3:4")]    // too many components
    public void TryParse_RejectsMalformed(string raw)
    {
        Assert.False(RaceTime.TryParse(raw, out _));
    }

    [Theory]
    [InlineData(0, 17, 51, "17:51")]
    [InlineData(0, 7, 5, "07:05")]
    [InlineData(1, 5, 9, "1:05:09")]
    public void Format_IsCanonical(int h, int m, int s, string expected)
    {
        var span = new TimeSpan(h, m, s);
        Assert.Equal(expected, RaceTime.Format(span));
    }

    [Theory]
    [InlineData(0, 1, 23, "+1:23")]
    [InlineData(1, 2, 3, "+1:02:03")]
    public void FormatGap_ShowsPlusPrefixedGap(int h, int m, int s, string expected)
    {
        Assert.Equal(expected, RaceTime.FormatGap(new TimeSpan(h, m, s)));
    }

    [Fact]
    public void FormatGap_ZeroOrNegative_ShowsDash()
    {
        Assert.Equal("—", RaceTime.FormatGap(TimeSpan.Zero));
    }
}
