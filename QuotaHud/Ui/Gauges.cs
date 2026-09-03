using System.Drawing.Drawing2D;

namespace QuotaHud;

static class GaugePainter
{
    public static void DrawRing(Graphics g, Rectangle bounds, NeedleQuota? quota, string caption, bool light)
    {
        var unlimited = quota?.Unlimited == true;
        var pct = quota?.RemainingPercent;
        var color = ColorFrom(quota);
        var cx = bounds.X + bounds.Width / 2f;
        var cy = bounds.Y + bounds.Height / 2f - (string.IsNullOrEmpty(caption) ? 2 : 10);
        var r = Math.Min(bounds.Width, bounds.Height) * 0.32f;
        var track = new RectangleF(cx - r, cy - r, r * 2, r * 2);
        var ratio = unlimited ? 1f : pct is null ? 0f : Math.Clamp((float)pct.Value / 100f, 0f, 1f);
        using var trackPen = new Pen(light ? Theme.Track : Color.FromArgb(40, 255, 255, 255), 6)
        {
            LineJoin = LineJoin.Round,
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
        };
        g.DrawEllipse(trackPen, track);
        if (unlimited || pct is not null)
        {
            using var valuePen = new Pen(color, 6)
            {
                LineJoin = LineJoin.Round,
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
            };
            g.DrawArc(valuePen, track, -90, 360f * ratio);
        }

        var label = unlimited ? "∞" : pct is null ? "—" : $"{Math.Round(pct.Value)}%";
        HudFonts.Draw(g, label, cx, cy - 10, light ? Theme.Ink : Color.White, 17, FontStyle.Bold, center: true);
        if (!string.IsNullOrEmpty(caption))
            HudFonts.Draw(g, caption, cx, bounds.Bottom - 18, light ? Theme.Secondary : Color.FromArgb(160, 255, 255, 255), 12, FontStyle.Regular, center: true);
    }

    static Color ColorFrom(NeedleQuota? quota)
    {
        if (quota?.Unlimited == true) return QuotaColors.BlueColor;
        return QuotaColors.RemainingDrawingColor(quota?.RemainingPercent) ?? Theme.Track;
    }
}
