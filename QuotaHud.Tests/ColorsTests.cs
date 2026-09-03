using QuotaHud;

namespace QuotaHud.Tests;

public class ColorsTests
{
    [Fact]
    public void Maps_67_and_above_to_blue()
    {
        Assert.Equal("blue", QuotaColors.RemainingColor(67));
        Assert.Equal("blue", QuotaColors.RemainingColor(100));
        Assert.Equal("blue", QuotaColors.RemainingColor(80.1));
    }

    [Fact]
    public void Maps_33_to_67_to_yellow()
    {
        Assert.Equal("yellow", QuotaColors.RemainingColor(33));
        Assert.Equal("yellow", QuotaColors.RemainingColor(66.9));
        Assert.Equal("yellow", QuotaColors.RemainingColor(50));
    }

    [Fact]
    public void Maps_below_33_to_red()
    {
        Assert.Equal("red", QuotaColors.RemainingColor(32.9));
        Assert.Equal("red", QuotaColors.RemainingColor(0));
        Assert.Equal("red", QuotaColors.RemainingColor(12));
    }

    [Fact]
    public void Does_not_invent_a_color_when_data_is_missing()
    {
        Assert.Null(QuotaColors.RemainingColor(null));
        Assert.Null(QuotaColors.RemainingColor(double.NaN));
    }
}
