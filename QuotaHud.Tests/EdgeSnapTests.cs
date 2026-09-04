namespace QuotaHud.Tests;

public class EdgeSnapTests
{
    static readonly Rectangle Desk = new(0, 0, 1920, 1040);

    [Fact]
    public void Snaps_to_the_near_edges_and_leaves_the_center_alone()
    {
        var nearRight = new Rectangle(1920 - 300 - 10, 40, 300, 400);
        Assert.Equal(new Rectangle(1620, 40, 300, 400), EdgeSnap.Snap(nearRight, Desk));

        var nearLeftTop = new Rectangle(12, 8, 300, 400);
        Assert.Equal(new Rectangle(0, 0, 300, 400), EdgeSnap.Snap(nearLeftTop, Desk));

        var center = new Rectangle(700, 200, 300, 400);
        Assert.Equal(center, EdgeSnap.Snap(center, Desk));
    }

    [Fact]
    public void Constrains_an_overflowing_window_back_onto_the_working_area()
    {
        var overflow = new Rectangle(1700, 800, 300, 400);
        Assert.Equal(new Rectangle(1620, 640, 300, 400), EdgeSnap.Constrain(overflow, Desk));
    }
}
