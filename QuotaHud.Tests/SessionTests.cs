using System.Text.Json.Nodes;
using QuotaHud;

namespace QuotaHud.Tests;

public class SessionTests
{
    static LoadedSource Loaded(string source, string[] grokIds, string[] chatgptIds) => new()
    {
        Source = source,
        FilesRead = [$"/{source}.json"],
        Grok = new SideLoad
        {
            Accounts = grokIds.Select(id => new Account { Id = id, Side = "grok", AccessToken = $"{id}-token", Label = id }).ToList(),
            Error = grokIds.Length == 0 ? new QuotaError("emptyPool", ErrorMessages.EmptyPool) : null,
        },
        Chatgpt = new SideLoad
        {
            Accounts = chatgptIds.Select(id => new Account { Id = id, Side = "chatgpt", AccessToken = $"{id}-token", Label = id }).ToList(),
            Error = chatgptIds.Length == 0 ? new QuotaError("emptyPool", ErrorMessages.EmptyPool) : null,
        },
    };

    [Fact]
    public async Task Source_switch_drops_stale_in_flight_results()
    {
        var visibility = new VisibilityStore(new MemorySettingsBackend { Data = { Source = "hermes" } });
        var grokStarted = new Dictionary<string, bool>();
        var grokGate = new TaskCompletionSource();
        var session = new QuotaSession(
            visibility,
            loadSource: source => source == "hermes"
                ? Loaded("hermes", ["hermes-grok"], ["hermes-gpt"])
                : Loaded("terminal", ["terminal-grok"], ["terminal-gpt"]),
            fetchGrok: async account =>
            {
                grokStarted[account.Id] = true;
                if (account.Id == "hermes-grok") await grokGate.Task;
                return JsonNode.Parse($$"""{ "config": { "creditUsagePercent": {{(account.Id == "hermes-grok" ? 10 : 40)}} } }""")!;
            },
            fetchChatgpt: _ => Task.FromResult(JsonNode.Parse("""
            {
              "rate_limit": {
                "primary_window": { "used_percent": 10, "limit_window_seconds": 18000 },
                "secondary_window": { "used_percent": 20, "limit_window_seconds": 604800 }
              }
            }
            """)!));

        var stale = session.RefreshNow(manual: false);
        while (!grokStarted.ContainsKey("hermes-grok")) await Task.Delay(5);
        var switched = session.SetSource("terminal");
        grokGate.SetResult();
        var staleResult = await stale;
        var freshResult = await switched;
        var state = session.CurrentState();
        Assert.True(staleResult.Discarded);
        Assert.False(freshResult.Discarded);
        Assert.Equal("terminal", state.Source);
        Assert.Equal(["terminal-grok"], state.Grok.All.Select(x => x.Id).ToArray());
        Assert.Equal(["terminal-gpt"], state.Chatgpt.All.Select(x => x.Id).ToArray());
    }

    [Fact]
    public async Task Missing_hermes_file_falls_back_to_terminal()
    {
        var visibility = new VisibilityStore(new MemorySettingsBackend { Data = { Source = "hermes" } });
        var session = new QuotaSession(
            visibility,
            loadSource: source =>
            {
                if (source == "hermes")
                {
                    return new LoadedSource
                    {
                        Source = "hermes",
                        Grok = new SideLoad { Error = new QuotaError("missingFile", ErrorMessages.MissingFile) },
                        Chatgpt = new SideLoad { Error = new QuotaError("missingFile", ErrorMessages.MissingFile) },
                    };
                }

                return Loaded("terminal", ["cli-grok"], ["cli-gpt"]);
            },
            fetchGrok: _ => Task.FromResult(JsonNode.Parse("""{ "config": { "creditUsagePercent": 20 } }""")!),
            fetchChatgpt: _ => Task.FromResult(JsonNode.Parse("""
            {
              "rate_limit": {
                "primary_window": { "used_percent": 10, "limit_window_seconds": 18000 },
                "secondary_window": { "used_percent": 20, "limit_window_seconds": 604800 }
              }
            }
            """)!));

        await session.RefreshNow(manual: true);
        var state = session.CurrentState();
        Assert.Equal("terminal", state.Source);
        Assert.Equal("terminal", visibility.GetSource());
        Assert.Contains(state.Grok.All, x => x.Id == "cli-grok");
    }
}
