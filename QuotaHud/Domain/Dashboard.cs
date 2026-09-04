using System.Globalization;

namespace QuotaHud;

public static class DashboardState
{
    public static string FormatReset(object? resetAt, DateTime? now = null)
    {
        if (resetAt is null) return "";
        DateTime date;
        switch (resetAt)
        {
            case double number:
                date = FromUnix(number);
                break;
            case long number:
                date = FromUnix(number);
                break;
            case int number:
                date = FromUnix(number);
                break;
            case Dictionary<string, double?> dict when dict.TryGetValue("afterSeconds", out var after) && after is not null:
                date = (now ?? DateTime.Now).AddSeconds(after.Value);
                break;
            case string text when DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed):
                date = parsed.ToLocalTime();
                break;
            default:
                return "";
        }

        return UiText.FormatReset(date);
    }

    static DateTime FromUnix(double number)
    {
        var value = (long)number;
        return value > 1_000_000_000_000
            ? DateTimeOffset.FromUnixTimeMilliseconds(value).LocalDateTime
            : DateTimeOffset.FromUnixTimeSeconds(value).LocalDateTime;
    }

    public static HudState Build(
        string source,
        string phase,
        bool loaded,
        string? grokSideError,
        string? chatgptSideError,
        IReadOnlyList<AccountResult> grokResults,
        IReadOnlyList<AccountResult> chatgptResults,
        VisibilityPredicate isVisible)
    {
        var grokAll = grokResults.Select(result => ToRenderer(result, isVisible(source, "grok", result.Account.Id))).ToList();
        var chatgptAll = chatgptResults.Select(result => ToRenderer(result, isVisible(source, "chatgpt", result.Account.Id))).ToList();
        return new HudState
        {
            Phase = phase,
            Loaded = loaded,
            Source = source,
            SourceLabel = QuotaPaths.DisplayLabel(source),
            Grok = new SideState
            {
                SideError = grokSideError,
                Accounts = grokAll.Where(x => x.Visible).ToList(),
                All = grokAll.Select(Summary).ToList(),
            },
            Chatgpt = new SideState
            {
                SideError = chatgptSideError,
                Accounts = chatgptAll.Where(x => x.Visible).ToList(),
                All = chatgptAll.Select(Summary).ToList(),
            },
        };
    }

    static RendererAccount ToRenderer(AccountResult result, bool visible)
    {
        var account = result.Account;
        var baseAccount = new RendererAccount
        {
            Id = account.Id,
            Side = account.Side,
            Label = Mask.MenuLabel(account),
            Email = string.IsNullOrEmpty(account.Email) ? null : Mask.MaskEmail(account.Email),
            Visible = visible,
            AccessToken = null,
        };
        if (!result.Ok)
        {
            baseAccount.Error = result.Error is null ? UiText.Error("network") : UiText.Error(result.Error.Code);
            baseAccount.Quota = result.Quota;
            return baseAccount;
        }

        baseAccount.Error = null;
        baseAccount.Quota = result.Quota;
        return baseAccount;
    }

    static RendererAccount Summary(RendererAccount item) => new()
    {
        Id = item.Id,
        Label = item.Label,
        Email = item.Email,
        Visible = item.Visible,
        Error = item.Error,
        AccessToken = null,
    };
}
