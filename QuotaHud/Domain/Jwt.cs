using System.Text;
using System.Text.Json.Nodes;

namespace QuotaHud;

public static class Jwt
{
    public static JsonObject DecodePayload(string token)
    {
        if (string.IsNullOrEmpty(token) || !token.Contains('.')) return new JsonObject();
        var parts = token.Split('.');
        if (parts.Length < 2) return new JsonObject();
        try
        {
            var padded = parts[1].Replace('-', '+').Replace('_', '/');
            padded += padded.Length % 4 == 0 ? "" : new string('=', 4 - (padded.Length % 4));
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(padded));
            return JsonNode.Parse(json) as JsonObject ?? new JsonObject();
        }
        catch
        {
            return new JsonObject();
        }
    }

    public static string? EmailFromClaims(JsonObject? claims)
    {
        if (claims is null) return null;
        var profile = claims["https://api.openai.com/profile"] as JsonObject;
        return FirstString(
            Str(claims, "email"),
            Str(claims, "preferred_username"),
            Str(claims, "upn"),
            profile is null ? null : Str(profile, "email"),
            profile is null ? null : Str(profile, "email_address"));
    }

    public static string? ChatgptAccountIdFromClaims(JsonObject? claims)
    {
        if (claims is null) return null;
        var auth = claims["https://api.openai.com/auth"] as JsonObject;
        return FirstString(
            Str(claims, "chatgpt_account_id"),
            Str(claims, "account_id"),
            auth is null ? null : Str(auth, "chatgpt_account_id"),
            auth is null ? null : Str(auth, "account_id"));
    }

    public static string? ClientId(JsonObject? claims)
    {
        if (claims is null) return null;
        return FirstString(Str(claims, "client_id"), Str(claims, "cid"));
    }

    public static string? Issuer(JsonObject? claims) => claims is null ? null : Str(claims, "iss");

    public static string? ChatgptPlanType(JsonObject? claims)
    {
        if (claims is null) return null;
        var auth = claims["https://api.openai.com/auth"] as JsonObject;
        return FirstString(
            Str(claims, "chatgpt_plan_type"),
            Str(claims, "plan_type"),
            auth is null ? null : Str(auth, "chatgpt_plan_type"),
            auth is null ? null : Str(auth, "plan_type"));
    }

    static string? Str(JsonObject obj, string key)
    {
        var node = obj[key];
        return node is JsonValue v && v.TryGetValue<string>(out var s) ? s : node?.ToString();
    }

    static string? FirstString(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
        }

        return null;
    }
}
