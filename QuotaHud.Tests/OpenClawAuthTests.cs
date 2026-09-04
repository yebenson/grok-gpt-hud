using Microsoft.Data.Sqlite;

namespace QuotaHud.Tests;

public class OpenClawAuthTests
{
    [Fact]
    public void Parses_openai_and_xai_oauth_profiles()
    {
        var json = """
        {
          "version": 1,
          "profiles": {
            "openai:chatgpt1": {
              "type": "oauth",
              "provider": "openai",
              "email": "a@x.com",
              "access": "tok-a",
              "refresh": "rt-a",
              "accountId": "acct-a",
              "chatgptPlanType": "pro",
              "expires": 2000000000000
            },
            "openai:chatgpt2": {
              "type": "oauth",
              "provider": "openai",
              "email": "b@x.com",
              "access": "tok-b",
              "refresh": "rt-b",
              "accountId": "acct-b",
              "chatgptPlanType": "team",
              "expires": 2000000000000
            },
            "xai:user@x.com": {
              "type": "oauth",
              "provider": "xai",
              "email": "user@x.com",
              "access": "tok-g",
              "refresh": "rt-g",
              "accountId": "grok-1",
              "expires": 2000000000000
            },
            "anthropic:skip": {
              "type": "api_key",
              "provider": "anthropic",
              "access": "should-skip"
            }
          }
        }
        """;
        var (chatgpt, grok) = OpenClawAuth.ParseStore(json);
        Assert.Equal(2, chatgpt.Count);
        Assert.Equal(["openai:chatgpt1", "openai:chatgpt2"], chatgpt.Select(a => a.Id).ToArray());
        Assert.Equal(["chatgpt1", "chatgpt2"], chatgpt.Select(a => a.Label).ToArray());
        Assert.Equal("pro", chatgpt[0].PlanHint);
        Assert.Equal("team", chatgpt[1].PlanHint);
        Assert.Single(grok);
        Assert.Equal("tok-g", grok[0].AccessToken);
        Assert.Equal("user@x.com", grok[0].Label);
        Assert.False(string.IsNullOrWhiteSpace(chatgpt[0].ExpiresAt));
        Assert.True(DateTimeOffset.TryParse(chatgpt[0].ExpiresAt, out _));
    }

    [Fact]
    public void Load_from_openclaw_sqlite_store_is_read_only()
    {
        var dir = Directory.CreateTempSubdirectory("quota-openclaw-");
        var dbPath = Path.Combine(dir.FullName, "openclaw.sqlite");
        using (var connection = new SqliteConnection($"Data Source={dbPath}"))
        {
            connection.Open();
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = """
                    CREATE TABLE config_machine_state (
                      state_key TEXT PRIMARY KEY,
                      value_json TEXT NOT NULL,
                      updated_at_ms INTEGER NOT NULL
                    );
                    """;
                cmd.ExecuteNonQuery();
            }

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = """
                    INSERT INTO config_machine_state(state_key, value_json, updated_at_ms)
                    VALUES ($k, $v, 1);
                    """;
                cmd.Parameters.AddWithValue("$k", OpenClawAuth.StoreStateKey);
                cmd.Parameters.AddWithValue("$v", """
                {
                  "version": 1,
                  "profiles": {
                    "openai:one": { "type":"oauth", "provider":"openai", "access":"a1", "refresh":"r1", "email":"a@x.com" },
                    "xai:two": { "type":"oauth", "provider":"xai", "access":"g1", "refresh":"rg", "email":"g@x.com" }
                  }
                }
                """);
                cmd.ExecuteNonQuery();
            }
        }

        var before = OpenClawAuth.ReadStoreJson(dbPath);
        var loaded = Credentials.LoadFromSource(
            QuotaPaths.OpenClaw,
            new ReadOnlyFileSystem(),
            new CredentialPaths(QuotaPaths.OpenClaw, dbPath, dbPath, dbPath));
        Assert.Equal(QuotaPaths.OpenClaw, loaded.Source);
        Assert.Single(loaded.Chatgpt.Accounts);
        Assert.Single(loaded.Grok.Accounts);
        Assert.Equal("one", loaded.Chatgpt.Accounts[0].Label);
        Assert.Equal(before, OpenClawAuth.ReadStoreJson(dbPath));
    }
}
