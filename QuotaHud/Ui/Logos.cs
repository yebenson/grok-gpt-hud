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

public static class AppBranding
{
    public static Icon Window { get; }
    public static Icon Tray { get; }

    static AppBranding()
    {
        Window = LoadIcon(32) ?? SystemIcons.Application;
        Tray = LoadIcon(16) ?? (Icon)Window.Clone();
    }

    static Icon? LoadIcon(int size)
    {
        try
        {
            using var ico = Open("app.ico");
            if (ico is not null) return new Icon(ico, size, size);

            using var png = Open("app.png");
            if (png is null) return null;
            using var image = Image.FromStream(png);
            return FromImage(image, size);
        }
        catch
        {
            return null;
        }
    }

    static Icon FromImage(Image image, int size)
    {
        using var bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.Transparent);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.DrawImage(image, 0, 0, size, size);
        var handle = bmp.GetHicon();
        using var created = Icon.FromHandle(handle);
        var clone = (Icon)created.Clone();
        DestroyIcon(handle);
        return clone;
    }

    static MemoryStream? Open(string fileName)
    {
        var asm = Assembly.GetExecutingAssembly();
        var name = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
        if (name is null) return null;
        using var stream = asm.GetManifestResourceStream(name);
        if (stream is null) return null;
        var copy = new MemoryStream();
        stream.CopyTo(copy);
        copy.Position = 0;
        return copy;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern bool DestroyIcon(IntPtr handle);
}
