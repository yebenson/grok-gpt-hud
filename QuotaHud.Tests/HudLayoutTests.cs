namespace QuotaHud.Tests;

public class HudLayoutTests
{
    static readonly Rectangle Desk = new(0, 0, 1920, 1040);

    [Fact]
    public void Does_not_move_or_resize_while_dragging()
    {
        var current = new Rectangle(100, 80, 300, 400);
        Assert.Null(HudLayout.ResolveBounds(current, new Size(300, 500), Desk, moving: true));
    }

    [Fact]
    public void Constrains_resized_bounds_onto_working_area_when_idle()
    {
        var current = new Rectangle(1700, 800, 300, 400);
        var next = HudLayout.ResolveBounds(current, new Size(300, 400), Desk, moving: false);
        Assert.Equal(new Rectangle(1620, 640, 300, 400), next);
    }

    [Fact]
    public void Working_area_comes_from_window_bounds_not_a_cursor_point()
    {
        // Primary left, secondary right — window on secondary must use that screen's working area.
        var secondary = new Rectangle(1920, 0, 1600, 900);
        var windowOnSecondary = new Rectangle(2000, 40, 300, 400);
        Assert.Equal(secondary, HudLayout.WorkingAreaForBounds(windowOnSecondary, screenFromBounds: _ => secondary));
    }
}
