using System.Text.Json;
using QuotaHud;

namespace QuotaHud.Tests;

public class VisibilityTests
{
    [Fact]
    public void Stores_visibility_only_in_widget_settings_and_does_not_write_auth_json()
    {
        var dir = Directory.CreateTempSubdirectory("quota-hud-");
        var authPath = Path.Combine(dir.FullName, "auth.json");
        var original = """
        {
          "credential_pool": {
            "openai-codex": [{ "id": "acctA", "label": "A", "access_token": "tok-a" }]
          }
        }
        """;
        File.WriteAllText(authPath, original);
        var before = File.ReadAllText(authPath);
        var store = new MemorySettingsBackend();
        var visibility = new VisibilityStore(store);
        visibility.SetSource("hermes");
        visibility.SetVisible("hermes", "chatgpt", "acctA", false);
        visibility.SetVisible("hermes", "grok", "g1", false);

        Assert.False(visibility.IsVisible("hermes", "chatgpt", "acctA"));
        Assert.True(visibility.IsVisible("hermes", "chatgpt", "acctB"));
        Assert.Equal(["acctA"], visibility.ListHidden("hermes", "chatgpt"));
        Assert.Equal("hermes", store.Data.Source);
        Assert.Contains("acctA", store.Data.Hidden.Hermes.Chatgpt);
        Assert.Equal(before, File.ReadAllText(authPath));
        Assert.DoesNotContain("tok-a", JsonSerializer.Serialize(store.Data));
    }

    [Fact]
    public void Keeps_terminal_and_hermes_hide_lists_independent()
    {
        var visibility = new VisibilityStore(new MemorySettingsBackend());
        visibility.SetVisible("hermes", "chatgpt", "shared", false);
        Assert.True(visibility.IsVisible("terminal", "chatgpt", "shared"));
        Assert.False(visibility.IsVisible("hermes", "chatgpt", "shared"));
    }

    [Fact]
    public void Keeps_openclaw_hide_list_independent()
    {
        var visibility = new VisibilityStore(new MemorySettingsBackend());
        visibility.SetVisible("openclaw", "chatgpt", "shared", false);
        Assert.True(visibility.IsVisible("hermes", "chatgpt", "shared"));
        Assert.True(visibility.IsVisible("terminal", "chatgpt", "shared"));
        Assert.False(visibility.IsVisible("openclaw", "chatgpt", "shared"));
        visibility.SetSource("openclaw");
        Assert.Equal("openclaw", visibility.GetSource());
    }
}

public sealed class MemorySettingsBackend : ISettingsBackend
{
    public WidgetSettings Data { get; } = new();

    public WidgetSettings Load() => Data;

    public void Save(WidgetSettings settings)
    {
        Data.Source = settings.Source;
        Data.Hidden = settings.Hidden;
        Data.TransparencyPercent = settings.TransparencyPercent;
        Data.AlwaysOnTop = settings.AlwaysOnTop;
    }
}
