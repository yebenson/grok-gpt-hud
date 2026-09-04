namespace QuotaHud;

public static class EdgeSnap
{
    public const int Threshold = 24;

    public static Rectangle Snap(Rectangle bounds, Rectangle working, int threshold = Threshold)
    {
        var x = bounds.X;
        var y = bounds.Y;

        if (bounds.Width >= working.Width) x = working.Left;
        else if (Math.Abs(bounds.Left - working.Left) <= threshold) x = working.Left;
        else if (Math.Abs(bounds.Right - working.Right) <= threshold) x = working.Right - bounds.Width;

        if (bounds.Height >= working.Height) y = working.Top;
        else if (Math.Abs(bounds.Top - working.Top) <= threshold) y = working.Top;
        else if (Math.Abs(bounds.Bottom - working.Bottom) <= threshold) y = working.Bottom - bounds.Height;

        return new Rectangle(x, y, bounds.Width, bounds.Height);
    }

    public static Rectangle Constrain(Rectangle bounds, Rectangle working)
    {
        var x = bounds.X;
        var y = bounds.Y;
        if (bounds.Width >= working.Width) x = working.Left;
        else
        {
            if (bounds.Left < working.Left) x = working.Left;
            if (x + bounds.Width > working.Right) x = working.Right - bounds.Width;
        }

        if (bounds.Height >= working.Height) y = working.Top;
        else
        {
            if (bounds.Top < working.Top) y = working.Top;
            if (y + bounds.Height > working.Bottom) y = working.Bottom - bounds.Height;
        }

        return new Rectangle(x, y, bounds.Width, bounds.Height);
    }
}
