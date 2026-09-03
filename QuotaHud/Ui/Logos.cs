using System.Drawing.Drawing2D;
using System.Reflection;

namespace QuotaHud;

static class BrandLogos
{
    static readonly Image? GrokPng = Load("QuotaHud.Brand.supergrok.png");
    static readonly Image? ChatGptPng = Load("QuotaHud.Brand.chatgpt.png");

    public static void DrawGrok(Graphics g, Rectangle bounds) => DrawPng(g, GrokPng, bounds);

    public static void DrawChatGpt(Graphics g, Rectangle bounds) => DrawPng(g, ChatGptPng, bounds);

    static void DrawPng(Graphics g, Image? image, Rectangle bounds)
    {
        if (image is null) return;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.CompositingQuality = CompositingQuality.HighQuality;
        var pad = Math.Max(1, Math.Min(bounds.Width, bounds.Height) * 0.04f);
        var box = new RectangleF(bounds.X + pad, bounds.Y + pad, bounds.Width - pad * 2, bounds.Height - pad * 2);
        g.DrawImage(image, box);
    }

    static Image? Load(string resource)
    {
        var asm = Assembly.GetExecutingAssembly();
        var stream = asm.GetManifestResourceStream(resource);
        if (stream is null)
        {
            var tail = resource.Contains("chatgpt", StringComparison.OrdinalIgnoreCase) ? "chatgpt.png" : "supergrok.png";
            var match = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith(tail, StringComparison.OrdinalIgnoreCase));
            if (match is not null) stream = asm.GetManifestResourceStream(match);
        }

        return stream is null ? null : Image.FromStream(stream);
    }
}
