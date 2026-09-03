using System.Text.Json.Nodes;

namespace QuotaHud;

public sealed class Account
{
    public string Side { get; set; } = "";
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public string? Email { get; set; }
    public string? AccountId { get; set; }
    public string AccessToken { get; set; } = "";
    public string? UserId { get; set; }
    public string? RefreshToken { get; set; }
    public string? ExpiresAt { get; set; }
    public string? OidcIssuer { get; set; }
    public string? OidcClientId { get; set; }
    public string? PlanHint { get; set; }
}

public sealed class QuotaError(string code, string message)
{
    public string Code { get; } = code;
    public string Message { get; } = message;
}

public sealed class SideLoad
{
    public List<Account> Accounts { get; set; } = [];
    public QuotaError? Error { get; set; }
}

public sealed class LoadedSource
{
    public string Source { get; set; } = QuotaPaths.Hermes;
    public SideLoad Grok { get; set; } = new();
    public SideLoad Chatgpt { get; set; } = new();
    public List<string> FilesRead { get; set; } = [];
    public List<AccountResult> GrokResults { get; set; } = [];
    public List<AccountResult> ChatgptResults { get; set; } = [];
}

public sealed class NeedleQuota
{
    public double? RemainingPercent { get; set; }
    public bool Unlimited { get; set; }
    public bool Missing { get; set; }
    public object? ResetAt { get; set; }
    public double? UsedPercent { get; set; }
    public string? Display { get; set; }
    public string? Color { get; set; }
    public string? Hex { get; set; }
    public string? ResetLabel { get; set; }
}

public sealed class AccountQuota
{
    public double? RemainingPercent { get; set; }
    public bool Unlimited { get; set; }
    public bool Missing { get; set; }
    public string? Display { get; set; }
    public string? Color { get; set; }
    public string? Hex { get; set; }
    public string? ResetLabel { get; set; }
    public NeedleQuota? FiveHour { get; set; }
    public NeedleQuota? Weekly { get; set; }
    public string? PlanType { get; set; }
}

public sealed class AccountResult
{
    public Account Account { get; set; } = new();
    public bool Ok { get; set; }
    public QuotaError? Error { get; set; }
    public AccountQuota? Quota { get; set; }
}

public sealed class RendererAccount
{
    public string Id { get; set; } = "";
    public string Side { get; set; } = "";
    public string Label { get; set; } = "";
    public string? Email { get; set; }
    public bool Visible { get; set; }
    public string? Error { get; set; }
    public AccountQuota? Quota { get; set; }
    public string? AccessToken { get; set; }
}

public sealed class SideState
{
    public string? SideError { get; set; }
    public List<RendererAccount> Accounts { get; set; } = [];
    public List<RendererAccount> All { get; set; } = [];
}

public sealed class HudState
{
    public string Phase { get; set; } = "loading";
    public bool Loaded { get; set; }
    public string Source { get; set; } = QuotaPaths.Hermes;
    public string SourceLabel { get; set; } = "Windows Hermes";
    public SideState Grok { get; set; } = new();
    public SideState Chatgpt { get; set; } = new();
}

public sealed class ChatgptUsage
{
    public NeedleQuota FiveHour { get; set; } = new();
    public NeedleQuota Weekly { get; set; } = new();
    public string? PlanType { get; set; }
}

public sealed class RefreshResult
{
    public bool Discarded { get; set; }
    public HudState State { get; set; } = new();
}

public delegate bool VisibilityPredicate(string source, string side, string id);

public delegate JsonNode FetchHandler(Account account);

public interface IAuthFileReader
{
    string ReadAllText(string path);
}
