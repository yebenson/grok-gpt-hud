using System.Text.Json.Nodes;
using QuotaHud;

namespace QuotaHud.Tests;

public class QuotaUnlimitedTests
{
    [Fact]
    public void Missing_primary_window_is_unlimited()
    {
        var five = QuotaParser.ParseFiveHourWindow(JsonNode.Parse("""
        { "rate_limit": { "secondary_window": { "used_percent": 40, "limit_window_seconds": 604800 } } }
        """)!);
        Assert.True(five.Unlimited);
        Assert.Null(five.RemainingPercent);
        Assert.Equal("∞", QuotaParser.Decorate(five).Display);
        Assert.Equal("blue", QuotaParser.Decorate(five).Color);
    }

    [Fact]
    public void Unlimited_flag_is_infinity_not_red()
    {
        var five = QuotaParser.ParseFiveHourWindow(JsonNode.Parse("""
        { "rate_limit": { "primary_window": { "unlimited": true, "used_percent": 0 } } }
        """)!);
        Assert.True(five.Unlimited);
        Assert.NotEqual("red", QuotaParser.Decorate(five).Color);
        Assert.Equal("∞", QuotaParser.Decorate(five).Display);
        Assert.Equal("blue", QuotaParser.Decorate(five).Color);
    }

    [Fact]
    public void Window_with_no_cap_field_is_infinity_even_if_used_percent_present()
    {
        var five = QuotaParser.ParseFiveHourWindow(JsonNode.Parse("""
        { "rate_limit": { "primary_window": { "used_percent": 100 } } }
        """)!);
        Assert.True(five.Unlimited);
        Assert.Equal("∞", QuotaParser.Decorate(five).Display);
        Assert.Equal("blue", QuotaParser.Decorate(five).Color);
    }

    [Fact]
    public void Weekly_needle_still_draws_when_capped()
    {
        var parsed = QuotaParser.ParseChatgptUsage(JsonNode.Parse("""
        {
          "rate_limit": {
            "primary_window": { "used_percent": 10 },
            "secondary_window": { "used_percent": 88, "limit_window_seconds": 604800, "reset_at": 1700000000 }
          }
        }
        """)!);
        Assert.True(parsed.FiveHour.Unlimited);
        Assert.False(parsed.Weekly.Unlimited);
        Assert.Equal(12, parsed.Weekly.RemainingPercent);
        Assert.Equal("red", QuotaParser.Decorate(parsed.Weekly).Color);
    }

    [Fact]
    public void Uses_remaining_percent_when_5h_window_is_capped()
    {
        var five = QuotaParser.ParseFiveHourWindow(JsonNode.Parse("""
        { "rate_limit": { "primary_window": { "used_percent": 19, "limit_window_seconds": 18000, "reset_at": 1700000000 } } }
        """)!);
        Assert.False(five.Unlimited);
        Assert.Equal(81, five.RemainingPercent);
    }

    [Fact]
    public void Weekly_cap_in_primary_window_is_not_shown_as_5h()
    {
        var parsed = QuotaParser.ParseChatgptUsage(JsonNode.Parse("""
        {
          "rate_limit": {
            "primary_window": { "used_percent": 40, "limit_window_seconds": 604800, "reset_at": 1700000000 }
          }
        }
        """)!);
        Assert.True(parsed.FiveHour.Unlimited);
        Assert.Equal("∞", QuotaParser.Decorate(parsed.FiveHour).Display);
        Assert.Equal("blue", QuotaParser.Decorate(parsed.FiveHour).Color);
        Assert.False(parsed.Weekly.Unlimited);
        Assert.Equal(60, parsed.Weekly.RemainingPercent);
    }
}
