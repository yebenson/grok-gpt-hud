using QuotaHud;

namespace QuotaHud.Tests;

public class PathsTests
{
    static Dictionary<string, string?> WindowsEnv() => new()
    {
        ["USERPROFILE"] = @"C:\Users\Tester",
        ["LOCALAPPDATA"] = @"C:\Users\Tester\AppData\Local",
        ["HOME"] = @"C:\Users\Tester",
    };

    [Fact]
    public void Windows_hermes_uses_localappdata()
    {
        var paths = QuotaPaths.ForSource("hermes", WindowsEnv(), windows: true);
        Assert.Equal(@"C:\Users\Tester\AppData\Local\hermes\auth.json", paths.Hermes);
    }

    [Fact]
    public void Hermes_home_environment_variable_wins()
    {
        var env = WindowsEnv();
        env["HERMES_HOME"] = @"D:\custom-hermes";
        var paths = QuotaPaths.ForSource("hermes", env, windows: true);
        Assert.Equal(@"D:\custom-hermes\auth.json", paths.Hermes);
    }

    [Fact]
    public void Terminal_uses_codex_and_grok_under_home()
    {
        var paths = QuotaPaths.ForSource("terminal", WindowsEnv(), windows: true);
        Assert.Equal(@"C:\Users\Tester\.codex\auth.json", paths.Chatgpt);
        Assert.Equal(@"C:\Users\Tester\.grok\auth.json", paths.Grok);
    }

    [Fact]
    public void Quota_widget_home_overrides_user_profile()
    {
        var env = WindowsEnv();
        env["QUOTA_WIDGET_HOME"] = @"E:\hud-home";
        var paths = QuotaPaths.ForSource("terminal", env, windows: true);
        Assert.Equal(@"E:\hud-home\.codex\auth.json", paths.Chatgpt);
    }

    [Fact]
    public void Openclaw_home_environment_variable_wins()
    {
        var env = WindowsEnv();
        env["OPENCLAW_HOME"] = @"D:\custom-openclaw";
        var paths = QuotaPaths.ForSource("openclaw", env, windows: true);
        Assert.Equal(@"D:\custom-openclaw\state\openclaw.sqlite", paths.Chatgpt);
        Assert.Equal(paths.Chatgpt, paths.Grok);
    }

    [Fact]
    public void Openclaw_uses_userprofile_dot_openclaw_state_db()
    {
        var paths = QuotaPaths.ForSource("openclaw", WindowsEnv(), windows: true);
        Assert.Equal(@"C:\Users\Tester\.openclaw\state\openclaw.sqlite", paths.Chatgpt);
    }
}
