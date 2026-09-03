using QuotaHud;

namespace QuotaHud.Tests;

public class SettingsFileTests
{
    [Fact]
    public void Json_settings_file_does_not_touch_auth_json()
    {
        var dir = Directory.CreateTempSubdirectory("quota-settings-");
        var auth = Path.Combine(dir.FullName, "auth.json");
        File.WriteAllText(auth, """{"tokens":{"access_token":"keep-me"}}""");
        var beforeWrite = File.GetLastWriteTimeUtc(auth);
        var settingsPath = Path.Combine(dir.FullName, "settings.json");
        var store = new FileSettingsBackend(settingsPath);
        var visibility = new VisibilityStore(store);
        visibility.SetSource("terminal");
        visibility.SetVisible("terminal", "grok", "u1", false);
        Assert.Equal("terminal", new VisibilityStore(new FileSettingsBackend(settingsPath)).GetSource());
        Assert.Equal(beforeWrite, File.GetLastWriteTimeUtc(auth));
        Assert.Equal("""{"tokens":{"access_token":"keep-me"}}""", File.ReadAllText(auth));
        Assert.DoesNotContain("keep-me", File.ReadAllText(settingsPath));
    }
}
