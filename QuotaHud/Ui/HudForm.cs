using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace QuotaHud;

public sealed class HudForm : Form
{
    const int WmNcLButtonDown = 0xA1;
    const int HtCaption = 0x2;

    readonly VisibilityStore _visibility;
    readonly QuotaSession _session;
    readonly System.Windows.Forms.Timer _timer = new() { Interval = 30_000 };
    readonly ContextMenuStrip _menu = new();
    readonly List<HudTile> _tiles = [];
    HudState _state = new();
    bool _placed;
    bool _ready;
    bool _applyingGlass;

    public HudForm()
    {
        _visibility = new VisibilityStore(new FileSettingsBackend(VisibilityStore.DefaultPath()));
        var client = new QuotaClient();
        _session = new QuotaSession(
            _visibility,
            loadSource: source => Credentials.LoadFromSource(source, new ReadOnlyFileSystem(), QuotaPaths.ForSource(source)),
            fetchGrok: account => client.FetchGrok(account),
            fetchChatgpt: account => client.FetchChatgpt(account),
            onChange: state =>
            {
                if (IsHandleCreated) BeginInvoke(() => ApplyState(state));
                else ApplyState(state);
            });

        Text = "配额小组件";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = true;
        TopMost = _visibility.GetAlwaysOnTop();
        BackColor = Color.Black;
        ForeColor = Theme.Ink;
        Font = HudFonts.Zh(14);
        ClientSize = new Size(Theme.WidthPx, 420);
        DoubleBuffered = false;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
        SetStyle(ControlStyles.OptimizedDoubleBuffer, false);
        ContextMenuStrip = _menu;
        _menu.Font = HudFonts.Zh(14);
        _menu.Opening += (_, _) => RebuildMenu();
        MouseDown += DragWindow;

        _timer.Tick += async (_, _) => await TickAutoRefresh();
        Load += async (_, _) => await OnFirstLoad();
        Shown += (_, _) => ApplyGlass();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle &= ~0x00080000; // WS_EX_LAYERED
            cp.ExStyle &= ~0x00000100; // WS_EX_DLGMODALFRAME
            cp.ExStyle &= ~0x00000200; // WS_EX_CLIENTEDGE
            cp.Style &= ~0x00800000;   // WS_BORDER
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyGlass();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (IsHandleCreated) ApplyGlass();
    }

    protected override void WndProc(ref Message m)
    {
        const int WmEraseBkgnd = 0x0014;
        const int WmDwmCompositionChanged = 0x031E;
        const int WmThemeChanged = 0x031A;
        if (m.Msg == WmEraseBkgnd)
        {
            using var g = Graphics.FromHdc(m.WParam);
            g.Clear(Color.Black);
            m.Result = 1;
            return;
        }

        base.WndProc(ref m);
        if (m.Msg is WmDwmCompositionChanged or WmThemeChanged) ApplyGlass();
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(Color.Black);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Color.Black);
        WindowGlass.Prepare(g);
        if (!_ready)
        {
            DrawSourceChip(g, new Rectangle(Theme.Gap, Theme.Gap, Theme.WidthPx - Theme.Gap * 2, Theme.TitleHeight));
            HudFonts.Draw(g, "正在读取配额…", Width / 2f, Height / 2f - 10, Theme.Secondary, 15, center: true);
            return;
        }

        foreach (var tile in _tiles)
        {
            if (tile.Kind == HudKind.Source) DrawSourceChip(g, tile.Bounds);
            else if (tile.Kind == HudKind.Header) SectionPainter.Draw(g, tile.Bounds, tile.Grok);
            else if (tile.Account is not null) CardPainter.Draw(g, tile.Bounds, tile.Account, tile.Grok);
        }
    }

    void DrawSourceChip(Graphics g, Rectangle bounds)
    {
        var box = new RectangleF(bounds.X + 0.5f, bounds.Y + 0.5f, bounds.Width - 1, bounds.Height - 1);
        WindowGlass.FillRound(g, box, Theme.Card, Theme.Corner);
        WindowGlass.StrokeRound(g, box, Theme.CardBorder, Theme.Corner);
        var source = _state.Source == QuotaPaths.Terminal ? "Terminal" : "Hermes";
        HudFonts.Draw(g, source, bounds.X + 12, bounds.Y + 4, Theme.Ink, 15, FontStyle.Bold);
        HudFonts.Draw(g, "右键", bounds.Right - 42, bounds.Y + 5, Theme.Secondary, 13);
    }

    async Task OnFirstLoad()
    {
        PlaceTopRight();
        ApplyGlass();
        try { await _session.RefreshNow(manual: true); }
        catch { _ready = false; Invalidate(); }
        _timer.Start();
    }

    void PlaceTopRight()
    {
        var area = Screen.FromPoint(Cursor.Position).WorkingArea;
        Location = new Point(Math.Max(area.Left, area.Right - Width - 24), area.Top + 24);
        _placed = true;
    }

    void ApplyState(HudState state)
    {
        _state = state;
        try
        {
            var diag = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "QuotaHud", "last-state.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(diag)!);
            File.WriteAllText(diag, string.Join(Environment.NewLine, [
                $"source={state.Source} phase={state.Phase} loaded={state.Loaded} grok={state.Grok.Accounts.Count}/{state.Grok.All.Count} gpt={state.Chatgpt.Accounts.Count}/{state.Chatgpt.All.Count} grokErr={state.Grok.SideError} gptErr={state.Chatgpt.SideError}",
                "grok=" + string.Join(" | ", state.Grok.Accounts.Select(a => $"{a.Error ?? a.Quota?.Display ?? "—"} plan={PlanName.Display(a.Quota?.PlanType) ?? "-"}")),
                "gpt=" + string.Join(" | ", state.Chatgpt.Accounts.Select(a => $"5h={a.Error ?? a.Quota?.FiveHour?.Display ?? "—"} 7d={a.Quota?.Weekly?.Display ?? "—"} plan={PlanName.Display(a.Quota?.PlanType) ?? "-"}")),
            ]));
        }
        catch { /* ignore diagnostics */ }

        _ready = state.Loaded && state.Phase == "ready";
        _tiles.Clear();
        if (!_ready)
        {
            ClientSize = new Size(Theme.WidthPx, Theme.Gap * 2 + Theme.TitleHeight + 80);
            ApplyGlass();
            Invalidate();
            return;
        }

        try
        {
            _tiles.Clear();
            var y = Theme.Gap;
            var x = Theme.Gap;
            var w = Theme.WidthPx - Theme.Gap * 2;
            _tiles.Add(new HudTile(new Rectangle(x, y, w, Theme.TitleHeight), HudKind.Source, false, null));
            y += Theme.TitleHeight + Theme.Gap;
            y = AddSide(true, state.Grok, x, y, w);
            y = AddSide(false, state.Chatgpt, x, y, w);
            var working = Screen.FromPoint(Cursor.Position).WorkingArea.Height;
            ClientSize = new Size(Theme.WidthPx, Math.Min(working - 48, y));

            if (!_placed) PlaceTopRight();
            ApplyGlass();
            Invalidate();
        }
        catch (Exception ex)
        {
            _ready = false;
            try
            {
                var diag = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "QuotaHud", "last-state.txt");
                File.AppendAllText(diag, Environment.NewLine + ex.GetType().Name + ": " + ex.Message);
            }
            catch { /* ignore */ }
            Invalidate();
        }
    }

    int AddSide(bool grok, SideState side, int x, int y, int w)
    {
        if (side.Accounts.Count == 0) return y;
        _tiles.Add(new HudTile(new Rectangle(x, y, w, Theme.HeaderHeight), HudKind.Header, grok, null));
        y += Theme.HeaderHeight + Theme.Gap;
        foreach (var account in side.Accounts)
        {
            var h = grok ? 138 : 158;
            _tiles.Add(new HudTile(new Rectangle(x, y, w, h), HudKind.Card, grok, account));
            y += h + Theme.Gap;
        }

        return y;
    }

    async Task TickAutoRefresh()
    {
        if (!_session.LoadedOnce || _session.Busy) return;
        if (!RefreshWindow.ShouldAutoPoll(DateTime.Now, _session.LastAutoPollAt)) return;
        try { await _session.RefreshNow(manual: false); }
        catch { /* keep last frame */ }
    }

    void RebuildMenu()
    {
        _menu.Items.Clear();
        var source = _state.Source;
        _menu.Items.Add(Radio("Windows Hermes", source != QuotaPaths.Terminal, () => _ = _session.SetSource(QuotaPaths.Hermes)));
        _menu.Items.Add(Radio("Windows Terminal", source == QuotaPaths.Terminal, () => _ = _session.SetSource(QuotaPaths.Terminal)));
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(new ToolStripMenuItem("立即刷新", null, async (_, _) => await _session.RefreshNow(manual: true)));
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(new ToolStripMenuItem("Grok") { Enabled = false });
        foreach (var account in AccountsFor("grok")) _menu.Items.Add(AccountItem("grok", account));
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(new ToolStripMenuItem("ChatGPT") { Enabled = false });
        foreach (var account in AccountsFor("chatgpt")) _menu.Items.Add(AccountItem("chatgpt", account));
        _menu.Items.Add(new ToolStripSeparator());
        var pin = new ToolStripMenuItem("窗口置顶") { Checked = TopMost, CheckOnClick = true };
        pin.Click += (_, _) =>
        {
            _visibility.SetAlwaysOnTop(pin.Checked);
            TopMost = pin.Checked;
        };
        _menu.Items.Add(pin);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(new ToolStripMenuItem("退出", null, (_, _) => Close()));
    }

    void ApplyGlass()
    {
        if (!IsHandleCreated || _applyingGlass) return;
        _applyingGlass = true;
        try
        {
            WindowGlass.Apply(this);
            Invalidate();
        }
        finally { _applyingGlass = false; }
    }

    IEnumerable<RendererAccount> AccountsFor(string side)
    {
        var all = side == "grok" ? _state.Grok.All : _state.Chatgpt.All;
        return all.Count > 0 ? all : (side == "grok" ? _state.Grok.Accounts : _state.Chatgpt.Accounts);
    }

    ToolStripMenuItem AccountItem(string side, RendererAccount account)
    {
        var item = new ToolStripMenuItem(account.Label) { Checked = account.Visible, CheckOnClick = true };
        item.Click += (_, _) =>
        {
            _visibility.SetVisible(_state.Source, side, account.Id, item.Checked);
            ApplyState(_session.CurrentState());
        };
        return item;
    }

    static ToolStripMenuItem Radio(string text, bool checkedState, Action click)
    {
        var item = new ToolStripMenuItem(text) { Checked = checkedState };
        item.Click += (_, _) => click();
        return item;
    }

    void DragWindow(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        ReleaseCapture();
        SendMessage(Handle, WmNcLButtonDown, HtCaption, 0);
    }

    [DllImport("user32.dll")] static extern bool ReleaseCapture();
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
}

readonly record struct HudTile(Rectangle Bounds, HudKind Kind, bool Grok, RendererAccount? Account);

enum HudKind { Source, Header, Card }

static class SectionPainter
{
    public static void Draw(Graphics g, Rectangle bounds, bool grok)
    {
        var box = new RectangleF(bounds.X + 0.5f, bounds.Y + 0.5f, bounds.Width - 1, bounds.Height - 1);
        WindowGlass.FillRound(g, box, Theme.Card, Theme.Corner);
        WindowGlass.StrokeRound(g, box, Theme.CardBorder, Theme.Corner);
        var logo = new Rectangle(bounds.X + 10, bounds.Y + 6, 40, 40);
        if (grok) BrandLogos.DrawGrok(g, logo);
        else BrandLogos.DrawChatGpt(g, logo);
        HudFonts.Draw(g, grok ? "Grok" : "ChatGPT", bounds.X + 58, bounds.Y + 15, Theme.Ink, 17, FontStyle.Bold);
    }
}

static class CardPainter
{
    public static void Draw(Graphics g, Rectangle bounds, RendererAccount account, bool grok)
    {
        var box = new RectangleF(bounds.X + 0.5f, bounds.Y + 0.5f, bounds.Width - 1, bounds.Height - 1);
        WindowGlass.FillRound(g, box, Theme.Card, Theme.Corner);
        WindowGlass.StrokeRound(g, box, Theme.CardBorder, Theme.Corner);

        var plan = PlanName.Display(account.Quota?.PlanType);
        if (!string.IsNullOrEmpty(plan)) DrawChip(g, bounds, plan);

        var top = bounds.Y + (string.IsNullOrEmpty(plan) ? 6 : 28);
        if (!string.IsNullOrEmpty(account.Error) && account.Quota is null)
        {
            HudFonts.Draw(g, account.Error, bounds.X + bounds.Width / 2f, bounds.Y + bounds.Height / 2f - 10, Theme.Error, 13, center: true);
            return;
        }

        if (grok)
        {
            var needle = new NeedleQuota
            {
                RemainingPercent = account.Quota?.RemainingPercent,
                Unlimited = account.Quota?.Unlimited == true,
                Color = account.Quota?.Color,
                Hex = account.Quota?.Hex,
                ResetLabel = account.Quota?.ResetLabel,
            };
            GaugePainter.DrawRing(g, new Rectangle(bounds.X + 8, top, bounds.Width - 16, bounds.Bottom - top - 22), needle, "", true);
            if (needle.Unlimited != true && needle.Missing != true)
                DrawReset(g, bounds, needle.ResetLabel, 0, bounds.Width);
        }
        else
        {
            var half = bounds.Width / 2;
            GaugePainter.DrawRing(g, new Rectangle(bounds.X + 4, top, half - 8, bounds.Bottom - top - 22), account.Quota?.FiveHour, "5h", true);
            GaugePainter.DrawRing(g, new Rectangle(bounds.X + half + 4, top, half - 8, bounds.Bottom - top - 22), account.Quota?.Weekly, "7d", true);
            if (account.Quota?.FiveHour?.Unlimited != true && account.Quota?.FiveHour?.Missing != true)
                DrawReset(g, bounds, account.Quota?.FiveHour?.ResetLabel, 0, half);
            if (account.Quota?.Weekly?.Unlimited != true && account.Quota?.Weekly?.Missing != true)
                DrawReset(g, bounds, account.Quota?.Weekly?.ResetLabel, half, half);
        }
    }

    static void DrawChip(Graphics g, Rectangle bounds, string plan)
    {
        using var font = HudFonts.En(13, FontStyle.Bold);
        var sz = g.MeasureString(plan, font);
        var rect = new RectangleF(bounds.X + (bounds.Width - sz.Width - 18) / 2f, bounds.Y + 6, sz.Width + 18, 22);
        using var path = WindowGlass.RoundRect(rect, 11);
        using var fill = new SolidBrush(Theme.ChipFill);
        g.FillPath(fill, path);
        using var pen = new Pen(Color.FromArgb(40, 0, 0, 0));
        g.DrawPath(pen, path);
        HudFonts.Draw(g, plan, rect.X + rect.Width / 2f, rect.Y + 2, Theme.ChipText, 13, FontStyle.Bold, center: true);
    }

    static void DrawReset(Graphics g, Rectangle bounds, string? label, int x, int width)
    {
        if (string.IsNullOrEmpty(label)) return;
        HudFonts.Draw(g, label, bounds.X + x + width / 2f, bounds.Bottom - 22, Theme.Secondary, 11, center: true);
    }
}
