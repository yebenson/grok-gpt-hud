namespace QuotaHud.Tests;

public class LiveHermesPathTests
{
    [Fact]
    public void Windows_default_hermes_path_points_at_localappdata()
    {
        var paths = QuotaPaths.ForSource("hermes");
        if (OperatingSystem.IsWindows())
        {
            var expected = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "hermes",
                "auth.json");
            var legacy = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".hermes",
                "auth.json");
            Assert.True(
                paths.Hermes == expected || paths.Hermes == legacy,
                $"Hermes path was {paths.Hermes}");
            Assert.False(
                paths.Hermes == legacy && File.Exists(expected),
                "Must prefer %LOCALAPPDATA%\\hermes\\auth.json when it exists");
        }
    }

    [Fact]
    public void Live_hermes_file_parses_codex_and_grok_without_exposing_tokens_in_labels()
    {
        var paths = QuotaPaths.ForSource("hermes");
        if (string.IsNullOrEmpty(paths.Hermes) || !File.Exists(paths.Hermes)) return;
        var loaded = Credentials.LoadFromSource("hermes", new ReadOnlyFileSystem(), paths);
        Assert.True(loaded.Chatgpt.Accounts.Count >= 1, "expected at least one Codex account");
        Assert.True(loaded.Grok.Accounts.Count >= 1, "expected at least one Grok account");
        foreach (var account in loaded.Chatgpt.Accounts.Concat(loaded.Grok.Accounts))
        {
            Assert.False(string.IsNullOrWhiteSpace(account.AccessToken));
            Assert.DoesNotContain("eyJ", account.Label, StringComparison.Ordinal);
        }
    }
}
