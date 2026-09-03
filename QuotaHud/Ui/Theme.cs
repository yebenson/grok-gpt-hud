using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace QuotaHud;

static class Theme
{
    public const int Corner = 12;
    public const int WidthPx = 300;
    public const int Gap = 10;
    public const int TitleHeight = 28;
    public const int HeaderHeight = 52;

    public static readonly Color Ink = Color.FromArgb(29, 29, 31);
    public static readonly Color Secondary = Color.FromArgb(110, 110, 115);
    public static readonly Color Card = Color.FromArgb(255, 255, 255);
    public static readonly Color CardBorder = Color.FromArgb(36, 0, 0, 0);
    public static readonly Color Track = Color.FromArgb(220, 220, 224);
    public static readonly Color Logo = Color.FromArgb(15, 15, 15);
    public static readonly Color Error = Color.FromArgb(255, 59, 48);
    public static readonly Color ChipFill = Color.FromArgb(242, 242, 247);
    public static readonly Color ChipText = Color.FromArgb(50, 50, 55);
    public static readonly Color GlassTint = Color.FromArgb(0x60, 252, 252, 253);
}

static class HudFonts
{
    public static Font Zh(float px, FontStyle style = FontStyle.Regular) =>
        New("Microsoft YaHei UI", "微软雅黑", "Microsoft YaHei", px, style);

    public static Font En(float px, FontStyle style = FontStyle.Regular) =>
        New("Segoe UI", "Segoe UI Variable", px, style);

    static Font New(string first, string second, float px, FontStyle style) =>
        New(first, second, first, px, style);

    static Font New(string a, string b, string c, float px, FontStyle style)
    {
        try { return new Font(a, px, style, GraphicsUnit.Pixel); }
        catch { try { return new Font(b, px, style, GraphicsUnit.Pixel); } catch { return new Font(c, px, style, GraphicsUnit.Pixel); } }
    }

    public static void Draw(Graphics g, string text, float x, float y, Color color, float size, FontStyle style = FontStyle.Regular, bool center = false)
    {
        if (string.IsNullOrEmpty(text)) return;
        using var zh = Zh(size, style);
        using var en = En(size, style);
        using var brush = new SolidBrush(color);
        float width = 0;
        foreach (var run in Runs(text))
        {
            using var font = run.English ? En(size, style) : Zh(size, style);
            width += g.MeasureString(run.Text, font, 1000, StringFormat.GenericTypographic).Width;
        }

        var cx = center ? x - width / 2f : x;
        var cur = cx;
        foreach (var run in Runs(text))
        {
            using var font = run.English ? En(size, style) : Zh(size, style);
            g.DrawString(run.Text, font, brush, cur, y, StringFormat.GenericTypographic);
            cur += g.MeasureString(run.Text, font, 1000, StringFormat.GenericTypographic).Width;
        }
    }

    static List<(string Text, bool English)> Runs(string text)
    {
        var list = new List<(string, bool)>();
        var buffer = new System.Text.StringBuilder();
        bool? en = null;
        foreach (var ch in text)
        {
            var isEn = ch <= 0x024F || ch == '∞' || ch == '%' || ch == '—' || char.IsDigit(ch) || char.IsWhiteSpace(ch) || ":+-./()".Contains(ch);
            if (en is null) en = isEn;
            if (isEn != en)
            {
                list.Add((buffer.ToString(), en.Value));
                buffer.Clear();
                en = isEn;
            }
            buffer.Append(ch);
        }

        if (buffer.Length > 0) list.Add((buffer.ToString(), en ?? true));
        return list;
    }
}

static class WindowGlass
{
    const int WcaAccentPolicy = 19;
    const int AccentAcrylic = 4;
    const int AccentBlur = 3;
    const int AccentUseColor = 2;
    const int DwmwaBorderColor = 34;
    const int DwmwaColorNone = unchecked((int)0xFFFFFFFE);
    const uint SwpNoSize = 0x0001;
    const uint SwpNoMove = 0x0002;
    const uint SwpNoZOrder = 0x0004;
    const uint SwpNoActivate = 0x0010;
    const uint SwpFrameChanged = 0x0020;

    public static void Apply(Form form)
    {
        if (!form.IsHandleCreated) return;
        form.Region?.Dispose();
        form.Region = null;

        var none = DwmwaColorNone;
        DwmSetWindowAttribute(form.Handle, DwmwaBorderColor, ref none, sizeof(int));

        var margins = new Margins { cxLeftWidth = -1, cxRightWidth = -1, cyTopHeight = -1, cyBottomHeight = -1 };
        DwmExtendFrameIntoClientArea(form.Handle, ref margins);

        var tint = Theme.GlassTint;
        var accent = new AccentPolicy
        {
            AccentState = AccentAcrylic,
            AccentFlags = AccentUseColor,
            GradientColor = (tint.A << 24) | (tint.B << 16) | (tint.G << 8) | tint.R,
        };
        if (!SetAccent(form, accent))
        {
            accent.AccentState = AccentBlur;
            SetAccent(form, accent);
        }

        SetWindowPos(form.Handle, IntPtr.Zero, 0, 0, 0, 0,
            SwpNoMove | SwpNoSize | SwpNoZOrder | SwpNoActivate | SwpFrameChanged);
    }

    public static void Prepare(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.CompositingQuality = CompositingQuality.HighQuality;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
    }

    public static void FillRound(Graphics g, RectangleF bounds, Color color, float radius)
    {
        using var path = RoundRect(bounds, radius);
        using var brush = new SolidBrush(color);
        g.FillPath(brush, path);
    }

    public static void StrokeRound(Graphics g, RectangleF bounds, Color color, float radius, float width = 1f)
    {
        using var path = RoundRect(bounds, radius);
        using var pen = new Pen(color, width) { Alignment = PenAlignment.Center, LineJoin = LineJoin.Round };
        g.DrawPath(pen, path);
    }

    public static GraphicsPath RoundRect(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();
        var d = Math.Max(1.5f, radius * 2f);
        d = Math.Min(d, Math.Min(bounds.Width, bounds.Height));
        var r = bounds;
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    static bool SetAccent(Form form, AccentPolicy accent)
    {
        var size = Marshal.SizeOf(accent);
        var ptr = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(accent, ptr, false);
            var data = new WindowCompositionAttributeData
            {
                Attribute = WcaAccentPolicy,
                Data = ptr,
                SizeOfData = size,
            };
            return SetWindowCompositionAttribute(form.Handle, ref data) != 0;
        }
        catch
        {
            return false;
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct AccentPolicy
    {
        public int AccentState;
        public int AccentFlags;
        public int GradientColor;
        public int AnimationId;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct WindowCompositionAttributeData
    {
        public int Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct Margins
    {
        public int cxLeftWidth;
        public int cxRightWidth;
        public int cyTopHeight;
        public int cyBottomHeight;
    }

    [DllImport("user32.dll")] static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
    [DllImport("dwmapi.dll")] static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref Margins margins);
}
