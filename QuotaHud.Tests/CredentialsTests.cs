using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace QuotaHud.Tests;

public class CredentialsTests
{
    static string FakeJwt(object payload)
    {
        static string B64Url(string json)
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        var header = B64Url("""{"alg":"none","typ":"JWT"}""");
        var body = B64Url(JsonSerializer.Serialize(payload));
        return $"{header}.{body}.sig";
    }

    static JsonObject Parse(string json) => JsonNode.Parse(json)!.AsObject();

    static IAuthFileReader JsonFile(string json) => new MemoryAuthFiles(new Dictionary<string, string> { ["/file"] = json });

    [Fact]
    public void Reads_every_hermes_openai_codex_and_xai_oauth_pool_entry()
    {
        var tokenA = FakeJwt(new Dictionary<string, object>
        {
            ["email"] = "a@x.com",
            ["https://api.openai.com/auth"] = new Dictionary<string, string> { ["chatgpt_account_id"] = "org-a" },
        });
        var tokenB = FakeJwt(new Dictionary<string, object> { ["email"] = "b@x.com" });
        var json = Parse($$"""
        {
          "credential_pool": {
            "openai-codex": [
              { "id": "acctA", "label": "account-A", "access_token": "{{tokenA}}" },
              { "id": "acctB", "label": "account-B", "access_token": "{{tokenB}}" }
            ],
            "xai-oauth": [
              { "id": "g1", "label": "grok-1", "access_token": "grok-a" },
              { "id": "g2", "label": "grok-2", "access_token": "grok-b" }
            ]
          }
        }
        """);
        var chatgpt = Credentials.ParseChatgptAuth(json);
        var grok = Credentials.ParseGrokAuth(json);
        Assert.Equal(2, chatgpt.Count);
        Assert.Equal(2, grok.Count);
        Assert.Equal("org-a", chatgpt[0].AccountId);
        Assert.Equal("a@x.com", chatgpt[0].Email);
    }

    [Fact]
    public void Does_not_hard_cap_cli_auth_json_at_one_account()
    {
        var json = Parse("""
        {
          "accounts": [
            { "id": "one", "tokens": { "access_token": "t1", "account_id": "org-1" } },
            { "id": "two", "tokens": { "access_token": "t2", "account_id": "org-2" } }
          ]
        }
        """);
        Assert.Equal(2, Credentials.ParseChatgptAuth(json).Count);
    }

    [Fact]
    public void Reads_grok_cli_map_entries()
    {
        var json = Parse("""
        {
          "https://auth.x.ai::client": { "key": "aaa", "expires_at": "2999-01-01T00:00:00Z", "user_id": "u1" },
          "https://auth.x.ai::other": { "key": "bbb", "expires_at": "2999-01-01T00:00:00Z", "user_id": "u2" }
        }
        """);
        var accounts = Credentials.ParseGrokAuth(json);
        Assert.Equal(2, accounts.Count);
        Assert.Equal("aaa", accounts[0].AccessToken);
    }

    [Fact]
    public void Reads_grok_cli_refresh_oidc_and_nanosecond_expiry()
    {
        var json = Parse("""
        {
          "https://auth.x.ai::client": {
            "key": "cli-grok-token",
            "refresh_token": "rt-1",
            "expires_at": "2026-09-02T15:35:18.124145300Z",
            "user_id": "u1",
            "email": "a@x.com",
            "oidc_issuer": "https://auth.x.ai",
            "oidc_client_id": "client"
          }
        }
        """);
        var account = Assert.Single(Credentials.ParseGrokAuth(json));
        Assert.Equal("cli-grok-token", account.AccessToken);
        Assert.Equal("rt-1", account.RefreshToken);
        Assert.Equal("2026-09-02T15:35:18.124145300Z", account.ExpiresAt);
        Assert.Equal("https://auth.x.ai", account.OidcIssuer);
        Assert.Equal("client", account.OidcClientId);
        Assert.False(GrokAuth.NeedsRefresh(account, DateTimeOffset.Parse("2026-09-02T09:00:00Z").UtcDateTime));
        Assert.True(GrokAuth.NeedsRefresh(account, DateTimeOffset.Parse("2026-09-02T15:36:00Z").UtcDateTime));
    }

    [Fact]
    public void Hermes_pool_token_copies_jwt_client_id_and_needs_refresh_from_exp()
    {
        var token = FakeJwt(new Dictionary<string, object>
        {
            ["iss"] = "https://auth.x.ai",
            ["client_id"] = "grok-client",
            ["exp"] = DateTimeOffset.Parse("2026-08-01T00:00:00Z").ToUnixTimeSeconds(),
        });
        var json = Parse($$"""
        {
          "credential_pool": {
            "xai-oauth": [
              { "id": "g1", "label": "device_code", "access_token": "{{token}}", "refresh_token": "rt" }
            ]
          }
        }
        """);
        var account = Assert.Single(Credentials.ParseGrokAuth(json));
        Assert.Equal("grok-client", account.OidcClientId);
        Assert.Equal("https://auth.x.ai", account.OidcIssuer);
        Assert.Equal("rt", account.RefreshToken);
        Assert.True(GrokAuth.NeedsRefresh(account, DateTimeOffset.Parse("2026-09-02T00:00:00Z").UtcDateTime));
    }

    [Fact]
    public void Reports_missing_file_vs_empty_pool_separately()
    {
        var missingFs = new MemoryAuthFiles(new Dictionary<string, string>());
        var missing = Credentials.LoadFromSource("hermes", missingFs, new CredentialPaths("hermes", "/nope", "/nope", "/nope"));
        Assert.Equal("missingFile", missing.Chatgpt.Error?.Code);
        Assert.Equal("missingFile", missing.Grok.Error?.Code);

        var emptyFs = JsonFile("""{ "credential_pool": { "openai-codex": [], "xai-oauth": [] } }""");
        var empty = Credentials.LoadFromSource("hermes", emptyFs, new CredentialPaths("hermes", "/file", "/file", "/file"));
        Assert.Equal("emptyPool", empty.Chatgpt.Error?.Code);
        Assert.Equal("emptyPool", empty.Grok.Error?.Code);
    }

    [Fact]
    public void Empty_xai_oauth_pool_does_not_ingest_chatgpt_singleton_tokens()
    {
        const string chatgptSingleton = "chatgpt-singleton-token";
        var json = Parse($$"""
        {
          "tokens": { "access_token": "{{chatgptSingleton}}", "account_id": "org-chatgpt" },
          "credential_pool": {
            "openai-codex": [{ "id": "codex-1", "label": "codex", "access_token": "{{chatgptSingleton}}" }],
            "xai-oauth": []
          }
        }
        """);
        var grok = Credentials.ParseGrokAuth(json);
        Assert.Empty(grok);
        Assert.DoesNotContain(grok, a => a.AccessToken == chatgptSingleton);
        var loaded = Credentials.LoadFromSource(
            "hermes",
            JsonFile(json.ToJsonString()),
            new CredentialPaths("hermes", "/file", "/file", "/file"));
        Assert.Empty(loaded.Grok.Accounts);
        Assert.Equal("emptyPool", loaded.Grok.Error?.Code);
    }

    [Fact]
    public void Empty_openai_codex_pool_does_not_steal_grok_singleton_tokens()
    {
        const string grokSingleton = "grok-singleton-token";
        var json = Parse($$"""
        {
          "tokens": { "access_token": "{{grokSingleton}}" },
          "providers": { "xai-oauth": { "tokens": { "access_token": "{{grokSingleton}}" } } },
          "credential_pool": {
            "openai-codex": [],
            "xai-oauth": [{ "id": "g1", "label": "grok-pool", "access_token": "{{grokSingleton}}" }]
          }
        }
        """);
        var chatgpt = Credentials.ParseChatgptAuth(json);
        Assert.Empty(chatgpt);
        Assert.DoesNotContain(chatgpt, a => a.AccessToken == grokSingleton);
    }

    [Fact]
    public void Missing_xai_oauth_key_in_present_pool_does_not_ingest_chatgpt_tokens()
    {
        const string chatgptSingleton = "chatgpt-singleton-token";
        var json = Parse($$"""
        {
          "tokens": { "access_token": "{{chatgptSingleton}}", "account_id": "org-chatgpt" },
          "credential_pool": {
            "openai-codex": [{ "id": "codex-1", "label": "codex", "access_token": "{{chatgptSingleton}}" }]
          }
        }
        """);
        Assert.False(json["credential_pool"]!.AsObject().ContainsKey("xai-oauth"));
        var grok = Credentials.ParseGrokAuth(json);
        Assert.Empty(grok);
        var loaded = Credentials.LoadFromSource(
            "hermes",
            JsonFile(json.ToJsonString()),
            new CredentialPaths("hermes", "/file", "/file", "/file"));
        Assert.Empty(loaded.Grok.Accounts);
        Assert.Equal("emptyPool", loaded.Grok.Error?.Code);
        Assert.Single(loaded.Chatgpt.Accounts);
    }

    [Fact]
    public void Source_switch_does_not_write_credential_files()
    {
        var token = FakeJwt(new Dictionary<string, object>
        {
            ["email"] = "benson@outlook.com",
            ["https://api.openai.com/auth"] = new Dictionary<string, string> { ["chatgpt_account_id"] = "acct-1" },
        });
        var hermes = "/tmp/.hermes/auth.json";
        var codex = "/tmp/.codex/auth.json";
        var grok = "/tmp/.grok/auth.json";
        var files = new Dictionary<string, string>
        {
            [hermes] = $$"""
            {
              "credential_pool": {
                "openai-codex": [{ "id": "h-gpt", "label": "hermes gpt", "access_token": "{{token}}" }],
                "xai-oauth": [{ "id": "h-grok", "label": "hermes grok", "access_token": "grok-token" }]
              }
            }
            """,
            [codex] = $$"""{ "tokens": { "access_token": "{{token}}", "account_id": "acct-1" } }""",
            [grok] = """{ "https://auth.x.ai::client": { "key": "cli-grok-token", "expires_at": "2999-01-01T00:00:00Z", "user_id": "u1" } }""",
        };
        var fs = new MemoryAuthFiles(files);
        var hermesLoaded = Credentials.LoadFromSource("hermes", fs, new CredentialPaths("hermes", hermes, hermes, hermes));
        var terminalLoaded = Credentials.LoadFromSource("terminal", fs, new CredentialPaths("terminal", null, codex, grok));
        Assert.Single(hermesLoaded.Chatgpt.Accounts);
        Assert.Single(hermesLoaded.Grok.Accounts);
        Assert.Single(terminalLoaded.Chatgpt.Accounts);
        Assert.Single(terminalLoaded.Grok.Accounts);
        Assert.Empty(fs.Writes);
    }

    sealed class MemoryAuthFiles(Dictionary<string, string> files) : IAuthFileReader
    {
        public List<string> Writes { get; } = [];

        public string ReadAllText(string path)
        {
            if (!files.TryGetValue(path, out var json)) throw new FileNotFoundException("missing", path);
            return json;
        }
    }
}
