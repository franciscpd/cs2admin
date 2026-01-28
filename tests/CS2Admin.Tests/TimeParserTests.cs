using CS2Admin.Utils;
using Xunit;

namespace CS2Admin.Tests;

public class TimeParserTests
{
    [Theory]
    [InlineData("30m", 30)]
    [InlineData("30", 30)]
    [InlineData("60m", 60)]
    [InlineData("1h", 60)]
    [InlineData("2h", 120)]
    public void Parse_Minutes_ReturnsCorrectTimeSpan(string input, int expectedMinutes)
    {
        var result = TimeParser.Parse(input);

        Assert.NotNull(result);
        Assert.Equal(expectedMinutes, result.Value.TotalMinutes);
    }

    [Theory]
    [InlineData("1d", 1)]
    [InlineData("7d", 7)]
    [InlineData("30d", 30)]
    public void Parse_Days_ReturnsCorrectTimeSpan(string input, int expectedDays)
    {
        var result = TimeParser.Parse(input);

        Assert.NotNull(result);
        Assert.Equal(expectedDays, result.Value.TotalDays);
    }

    [Theory]
    [InlineData("1w", 7)]
    [InlineData("2w", 14)]
    public void Parse_Weeks_ReturnsCorrectTimeSpan(string input, int expectedDays)
    {
        var result = TimeParser.Parse(input);

        Assert.NotNull(result);
        Assert.Equal(expectedDays, result.Value.TotalDays);
    }

    [Fact]
    public void Parse_Zero_ReturnsPermanent()
    {
        var result = TimeParser.Parse("0");

        Assert.Null(result);
    }

    [Theory]
    [InlineData("30s", 30)]
    [InlineData("60s", 60)]
    public void Parse_Seconds_ReturnsCorrectTimeSpan(string input, int expectedSeconds)
    {
        var result = TimeParser.Parse(input);

        Assert.NotNull(result);
        Assert.Equal(expectedSeconds, result.Value.TotalSeconds);
    }

    [Fact]
    public void Format_Null_ReturnsPermanent()
    {
        var result = TimeParser.Format(null);

        Assert.Equal("permanent", result);
    }

    [Fact]
    public void Format_Hours_ReturnsCorrectString()
    {
        var result = TimeParser.Format(TimeSpan.FromHours(2));

        Assert.Equal("2 hour(s)", result);
    }

    [Fact]
    public void Format_Days_ReturnsCorrectString()
    {
        var result = TimeParser.Format(TimeSpan.FromDays(3));

        Assert.Equal("3 day(s)", result);
    }

    [Fact]
    public void Format_Weeks_ReturnsCorrectString()
    {
        var result = TimeParser.Format(TimeSpan.FromDays(14));

        Assert.Equal("2 week(s)", result);
    }
}
