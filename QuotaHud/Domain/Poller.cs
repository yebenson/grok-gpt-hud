using System.Text.Json.Nodes;

namespace QuotaHud;

public static class Poller
{
    public static async Task<(List<AccountResult> Grok, List<AccountResult> Chatgpt)> PollAccounts(
        IReadOnlyList<Account> grokAccounts,
        IReadOnlyList<Account> chatgptAccounts,
        Func<Account, Task<JsonNode>> fetchGrok,
        Func<Account, Task<JsonNode>> fetchChatgpt)
    {
        var grokTask = MapIsolated(grokAccounts, async account =>
        {
            var payload = await fetchGrok(account);
            var parsed = QuotaParser.ParseGrokBilling(payload);
            if (parsed.Missing || parsed.RemainingPercent is null)
            {
                return new AccountResult
                {
                    Account = account,
                    Ok = false,
                    Error = new QuotaError("parse", ErrorMessages.MissingQuota),
                    Quota = QuotaParser.DecorateNeedle(parsed),
                };
            }

            var quota = QuotaParser.DecorateNeedle(parsed);
            quota.ResetLabel = DashboardState.FormatReset(parsed.ResetAt);
            quota.PlanType = QuotaParser.ParseGrokPlan(payload) ?? account.PlanHint;
            return new AccountResult { Account = account, Ok = true, Quota = quota };
        });

        var chatgptTask = MapIsolated(chatgptAccounts, async account =>
        {
            var payload = await fetchChatgpt(account);
            var parsed = QuotaParser.ParseChatgptUsage(payload);
            var five = QuotaParser.Decorate(parsed.FiveHour);
            five.ResetLabel = DashboardState.FormatReset(parsed.FiveHour.ResetAt);
            var week = QuotaParser.Decorate(parsed.Weekly);
            week.ResetLabel = DashboardState.FormatReset(parsed.Weekly.ResetAt);
            return new AccountResult
            {
                Account = account,
                Ok = true,
                Quota = new AccountQuota
                {
                    FiveHour = five,
                    Weekly = week,
                    PlanType = parsed.PlanType ?? account.PlanHint,
                },
            };
        });

        await Task.WhenAll(grokTask, chatgptTask);
        return (await grokTask, await chatgptTask);
    }

    static async Task<List<AccountResult>> MapIsolated(
        IReadOnlyList<Account> items,
        Func<Account, Task<AccountResult>> mapper)
    {
        var tasks = items.Select(item => Invoke(item, mapper)).ToArray();
        return [.. await Task.WhenAll(tasks)];
    }

    static async Task<AccountResult> Invoke(Account item, Func<Account, Task<AccountResult>> mapper)
    {
        try
        {
            return await mapper(item);
        }
        catch (Exception ex)
        {
            return new AccountResult
            {
                Account = item,
                Ok = false,
                Error = ErrorClassifier.Classify(ex),
            };
        }
    }
}
