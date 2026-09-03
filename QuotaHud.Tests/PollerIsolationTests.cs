using System.Text.Json.Nodes;
using QuotaHud;

namespace QuotaHud.Tests;

public class PollerIsolationTests
{
    static Account Acc(string id, string side, string? accountId = null) => new()
    {
        Id = id,
        Side = side,
        AccessToken = id,
        AccountId = accountId,
        Label = id,
    };

    [Fact]
    public async Task Returns_success_and_401_side_by_side()
    {
        var order = new List<string>();
        var (grok, chatgpt) = await Poller.PollAccounts(
            [Acc("g-ok", "grok"), Acc("g-bad", "grok")],
            [Acc("c-ok", "chatgpt", "acct"), Acc("c-slow", "chatgpt", "acct2")],
            fetchGrok: account =>
            {
                order.Add(account.Id);
                if (account.Id == "g-bad") throw new HttpStatusException(401, "expired");
                return Task.FromResult(JsonNode.Parse("""{ "config": { "creditUsagePercent": 28, "billingPeriodEnd": "2026-09-08T00:00:00Z" } }""")!);
            },
            fetchChatgpt: async account =>
            {
                order.Add(account.Id);
                if (account.Id == "c-slow") await Task.Delay(20);
                return JsonNode.Parse("""
                {
                  "rate_limit": {
                    "primary_window": { "used_percent": 10, "limit_window_seconds": 18000 },
                    "secondary_window": { "used_percent": 40, "limit_window_seconds": 604800 }
                  }
                }
                """)!;
            });

        var grokById = grok.ToDictionary(x => x.Account.Id);
        var gptById = chatgpt.ToDictionary(x => x.Account.Id);
        Assert.True(grokById["g-ok"].Ok);
        Assert.Equal(72, grokById["g-ok"].Quota!.RemainingPercent);
        Assert.False(grokById["g-bad"].Ok);
        Assert.Contains("凭证过期", grokById["g-bad"].Error!.Message);
        Assert.True(gptById["c-ok"].Ok);
        Assert.Equal(90, gptById["c-ok"].Quota!.FiveHour!.RemainingPercent);
        Assert.True(gptById["c-slow"].Ok);
        Assert.Equal(4, grok.Count + chatgpt.Count);
        Assert.Contains("g-ok", order);
        Assert.Contains("g-bad", order);
    }

    [Fact]
    public async Task Does_not_fail_the_whole_batch_when_one_side_throws()
    {
        var started = DateTime.UtcNow;
        var (_, chatgpt) = await Poller.PollAccounts(
            [],
            [Acc("fast", "chatgpt"), Acc("boom", "chatgpt")],
            fetchGrok: _ => Task.FromResult(JsonNode.Parse("{}")!),
            fetchChatgpt: account =>
            {
                if (account.Id == "boom") throw new HttpStatusException(403, "nope");
                return Task.FromResult(JsonNode.Parse("""
                {
                  "rate_limit": {
                    "primary_window": { "used_percent": 1, "limit_window_seconds": 18000 },
                    "secondary_window": { "used_percent": 1, "limit_window_seconds": 604800 }
                  }
                }
                """)!);
            });
        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(1));
        Assert.True(chatgpt.First(x => x.Account.Id == "fast").Ok);
        Assert.Equal("forbidden", chatgpt.First(x => x.Account.Id == "boom").Error!.Code);
    }
}
