namespace QuotaHud.Tests;

public class AppBrandingTests
{
    [Fact]
    public void Loads_the_tool_logo_for_window_and_tray()
    {
        Assert.NotNull(AppBranding.Window);
        Assert.NotNull(AppBranding.Tray);
        Assert.True(AppBranding.Window.Width >= 16);
        Assert.True(AppBranding.Tray.Width >= 16);
    }
}
