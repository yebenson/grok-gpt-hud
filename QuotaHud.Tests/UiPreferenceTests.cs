using QuotaHud;

namespace QuotaHud.Tests;

public class MaskTests
{
    [Fact]
    public void Treats_device_code_as_placeholder()
    {
        Assert.True(Mask.IsPlaceholderLabel("device_code"));
        Assert.True(Mask.IsPlaceholderLabel("device-code"));
        Assert.False(Mask.IsPlaceholderLabel("GPT-B"));
    }

    [Fact]
    public void Menu_label_skips_device_code()
    {
        var grok = new Account { Side = "grok", Label = "device_code", Email = "a@x.com", Id = "g1" };
        Assert.Equal("a****@x.com", Mask.MenuLabel(grok));
        var grokNoEmail = new Account { Side = "grok", Label = "device_code", Id = "g1" };
        Assert.Equal("Grok", Mask.MenuLabel(grokNoEmail));
    }
}

public class TransparencyTests
{
    [Fact]
    public void Clamps_transparency_to_0_90_and_persists_without_auth_json()
    {
        var backend = new MemorySettingsBackend();
        var visibility = new VisibilityStore(backend);
        visibility.SetTransparency(150);
        Assert.Equal(90, visibility.GetTransparency());
        visibility.SetTransparency(-4);
        Assert.Equal(0, visibility.GetTransparency());
        visibility.SetTransparency(40);
        Assert.Equal(40, visibility.GetTransparency());
        Assert.Equal(40, backend.Data.TransparencyPercent);
    }
}

public class AlwaysOnTopTests
{
    [Fact]
    public void Defaults_to_topmost_and_persists_without_auth_json()
    {
        var backend = new MemorySettingsBackend();
        var visibility = new VisibilityStore(backend);
        Assert.True(visibility.GetAlwaysOnTop());
        visibility.SetAlwaysOnTop(false);
        Assert.False(visibility.GetAlwaysOnTop());
        Assert.False(backend.Data.AlwaysOnTop);
        visibility.SetAlwaysOnTop(true);
        Assert.True(backend.Data.AlwaysOnTop);
    }
}

public class GrokRefreshTests
{
    [Fact]
    public void Expired_cli_token_needs_in_memory_refresh()
    {
        var account = new Account
        {
            AccessToken = "expired-key",
            RefreshToken = "rt",
            ExpiresAt = "2026-08-27T09:00:24Z",
            OidcIssuer = "https://auth.x.ai",
            OidcClientId = "client",
        };
        Assert.True(GrokAuth.NeedsRefresh(account, DateTimeOffset.Parse("2026-09-02T00:00:00Z").UtcDateTime));
        Assert.False(GrokAuth.NeedsRefresh(account, DateTimeOffset.Parse("2026-08-01T00:00:00Z").UtcDateTime));
    }

    [Fact]
    public void Parses_nanosecond_expires_at_as_utc()
    {
        var account = new Account
        {
            RefreshToken = "rt",
            ExpiresAt = "2026-09-02T15:35:18.124145300Z",
        };
        var exp = GrokAuth.ParseExpiry(account);
        Assert.NotNull(exp);
        Assert.Equal(DateTimeOffset.Parse("2026-09-02T15:35:18.1241453+00:00").UtcDateTime, exp.Value.UtcDateTime);
    }
}
