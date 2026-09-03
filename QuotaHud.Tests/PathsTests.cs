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
    public void Windows_hermes_uses_localappdata_not_userprofile_dot_hermes()
    {
        var paths = QuotaPaths.ForSource("hermes", WindowsEnv(), windows: true, exists: _ => true);
        Assert.Equal(@"C:\Users\Tester\AppData\Local\hermes\auth.json", paths.Hermes);
        Assert.DoesNotContain(@".hermes", paths.Hermes!);
    }

    [Fact]
    public void Windows_hermes_falls_back_to_userprofile_dot_hermes_when_local_file_missing()
    {
        var paths = QuotaPaths.ForSource(
            "hermes",
            WindowsEnv(),
            windows: true,
            exists: p => p.Contains(@"\.hermes\"));
        Assert.Equal(@"C:\Users\Tester\.hermes\auth.json", paths.Hermes);
    }

    [Fact]
    public void Hermes_home_environment_variable_wins()
    {
        var env = WindowsEnv();
        env["HERMES_HOME"] = @"D:\custom-hermes";
        var paths = QuotaPaths.ForSource("hermes", env, windows: true, exists: _ => true);
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
}
