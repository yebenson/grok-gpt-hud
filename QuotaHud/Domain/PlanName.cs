using System.Globalization;

namespace QuotaHud;

public static class PlanName
{
    static readonly Dictionary<string, string> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["plus"] = "Plus",
        ["chatgpt_plus"] = "Plus",
        ["chatgptplus"] = "Plus",
        ["pro"] = "Pro",
        ["chatgpt_pro"] = "Pro",
        ["chatgptpro"] = "Pro",
        ["business"] = "Business",
        ["chatgpt_business"] = "Business",
        ["team"] = "Team",
        ["chatgpt_team"] = "Team",
        ["enterprise"] = "Enterprise",
        ["chatgpt_enterprise"] = "Enterprise",
        ["edu"] = "Edu",
        ["education"] = "Edu",
        ["go"] = "Go",
        ["chatgpt_go"] = "Go",
        ["supergrok"] = "SuperGrok",
        ["super_grok"] = "SuperGrok",
        ["super-grok"] = "SuperGrok",
        ["grok_heavy"] = "SuperGrok",
        ["heavy"] = "SuperGrok",
        ["grok"] = "Grok",
        ["grokbuild"] = "SuperGrok",
        ["grok_build"] = "SuperGrok",
    };

    static readonly HashSet<string> Hidden = new(StringComparer.OrdinalIgnoreCase)
    {
        "free", "freeplan", "free_plan", "untitled", "unknown", "default", "none", "null", "0", "1", "2",
    };

    public static string? Display(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var text = raw.Trim();
        if (Hidden.Contains(text)) return null;
        var key = text.Replace(' ', '_').Replace('-', '_');
        if (Map.TryGetValue(key, out var mapped)) return mapped;
        if (Map.TryGetValue(text, out mapped)) return mapped;
        if (text.Length > 18 || text.Contains('@') || text.Contains('/')) return null;
        if (text.All(ch => char.IsDigit(ch))) return null;
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(text.ToLowerInvariant());
    }
}
