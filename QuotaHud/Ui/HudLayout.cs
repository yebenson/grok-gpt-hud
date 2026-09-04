namespace QuotaHud;

public static class HudLayout
{
    public static Rectangle? ResolveBounds(Rectangle current, Size size, Rectangle working, bool moving)
    {
        if (moving) return null;
        var sized = new Rectangle(current.Location, size);
        return EdgeSnap.Constrain(sized, working);
    }

    public static Rectangle WorkingAreaForBounds(Rectangle bounds, Func<Rectangle, Rectangle>? screenFromBounds = null)
    {
        if (screenFromBounds is not null) return screenFromBounds(bounds);
        return Screen.FromRectangle(bounds).WorkingArea;
    }
}
