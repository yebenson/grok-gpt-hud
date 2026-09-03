using QuotaHud;

namespace QuotaHud.Tests;

public class RefreshWindowTests
{
    static DateTime AtLocal(int hours, int minutes = 0)
    {
        var d = DateTime.Now;
        return new DateTime(d.Year, d.Month, d.Day, hours, minutes, 0, DateTimeKind.Local);
    }

    [Fact]
    public void Allows_auto_poll_inside_work_hours()
    {
        Assert.True(RefreshWindow.IsAutoRefreshAllowed(AtLocal(9)));
        Assert.True(RefreshWindow.IsAutoRefreshAllowed(AtLocal(12, 30)));
        Assert.True(RefreshWindow.IsAutoRefreshAllowed(AtLocal(17, 59)));
    }

    [Fact]
    public void Does_not_auto_poll_at_18_01()
    {
        var now = AtLocal(18, 1);
        Assert.False(RefreshWindow.IsAutoRefreshAllowed(now));
        Assert.False(RefreshWindow.ShouldAutoPoll(now, AtLocal(17, 40), RefreshWindow.AutoInterval));
    }

    [Fact]
    public void Does_not_auto_poll_before_09_or_at_18_00()
    {
        Assert.False(RefreshWindow.IsAutoRefreshAllowed(AtLocal(8, 59)));
        Assert.False(RefreshWindow.IsAutoRefreshAllowed(AtLocal(18, 0)));
    }

    [Fact]
    public void Respects_15_minute_interval_inside_window()
    {
        var now = AtLocal(10);
        var recent = now.AddMinutes(-5);
        var stale = now.AddMinutes(-15);
        Assert.False(RefreshWindow.ShouldAutoPoll(now, recent));
        Assert.True(RefreshWindow.ShouldAutoPoll(now, stale));
    }
}
