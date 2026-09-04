using Microsoft.Data.Sqlite;
using System.Globalization;
using System.Text.Json.Nodes;

namespace QuotaHud;

public static class OpenClawAuth
{
    public const string StoreStateKey = "authProfiles.store";

    public static string? ReadStoreJson(string dbPath)
    {
        if (string.IsNullOrWhiteSpace(dbPath) || !File.Exists(dbPath)) return null;
        try
        {
            var cs = new SqliteConnectionStringBuilder
            {
                DataSource = dbPath,
                Mode = SqliteOpenMode.ReadOnly,
                Cache = SqliteCacheMode.Shared,
                Pooling = false,
            }.ToString();
            using var connection = new SqliteConnection(cs);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT value_json FROM config_machine_state WHERE state_key = $key LIMIT 1";
            command.Parameters.AddWithValue("$key", StoreStateKey);
            var value = command.ExecuteScalar()?.ToString();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
        catch
        {
            return null;
        }
    }

    public static (List<Account> Chatgpt, List<Account> Grok) ParseStore(string json)
    {
        var root = JsonNode.Parse(json) as JsonObject;
        var profiles = root?["profiles"] as JsonObject;
        var chatgpt = new List<Account>();
        var grok = new List<Account>();
        if (profiles is null) return (chatgpt, grok);

        foreach (var kv in profiles)
        {
            var account = ToAccount(kv.Key, kv.Value as JsonObject);
            if (account is null) continue;
            if (account.Side == "chatgpt") chatgpt.Add(account);
            else if (account.Side == "grok") grok.Add(account);
        }

        return (chatgpt, grok);
    }

    static Account? ToAccount(string profileId, JsonObject? entry)
    {
        if (entry is null) return null;
        var provider = Str(entry, "provider")?.Trim().ToLowerInvariant() ?? "";
        var side = provider switch
        {
            "openai" or "openai-codex" or "codex" => "chatgpt",
            "xai" or "xai-oauth" or "grok" => "grok",
            _ => null,
        };
        if (side is null) return null;

        var access = FirstNonEmpty(Str(entry, "access"), Str(entry, "access_token"), Str(entry, "accessToken"));
        if (string.IsNullOrWhiteSpace(access)) return null;

        var email = Str(entry, "email");
        var label = LabelFromProfileId(profileId, email);
        var expires = FormatExpires(entry["expires"]);
        var claims = Jwt.DecodePayload(access);
        return new Account
        {
            Side = side,
            Id = profileId,
            Label = label,
            Email = email,
            AccountId = FirstNonEmpty(Str(entry, "accountId"), Str(entry, "account_id"), Jwt.ChatgptAccountIdFromClaims(claims)),
            AccessToken = access.Trim(),
            RefreshToken = FirstNonEmpty(Str(entry, "refresh"), Str(entry, "refresh_token"), Str(entry, "refreshToken")),
            ExpiresAt = expires,
            OidcIssuer = FirstNonEmpty(Str(entry, "issuer"), Str(entry, "oidc_issuer"), Jwt.Issuer(claims)),
            OidcClientId = FirstNonEmpty(Str(entry, "client_id"), Str(entry, "clientId"), Jwt.ClientId(claims)),
            PlanHint = FirstNonEmpty(Str(entry, "chatgptPlanType"), Str(entry, "chatgpt_plan_type"), Jwt.ChatgptPlanType(claims)),
            UserId = FirstNonEmpty(Str(entry, "accountId"), Str(entry, "user_id")),
        };
    }

    static string LabelFromProfileId(string profileId, string? email)
    {
        var colon = profileId.IndexOf(':');
        if (colon >= 0 && colon < profileId.Length - 1)
        {
            var tail = profileId[(colon + 1)..].Trim();
            if (!string.IsNullOrWhiteSpace(tail)) return tail;
        }

        return email ?? profileId;
    }

    static string? FormatExpires(JsonNode? node)
    {
        if (node is null) return null;
        if (node is JsonValue v)
        {
            if (v.TryGetValue<long>(out var ms)) return FromUnixMs(ms);
            if (v.TryGetValue<double>(out var number)) return FromUnixMs((long)number);
            if (v.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text))
            {
                if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                    return FromUnixMs(parsed);
                return text.Trim();
            }
        }

        return null;
    }

    static string FromUnixMs(long value)
    {
        // OpenClaw stores oauth expires in unix milliseconds; tolerate seconds.
        var ms = value > 1_000_000_000_000L ? value : value * 1000L;
        return DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
    }

    static string? Str(JsonObject obj, string key) =>
        obj[key] is JsonValue v && v.TryGetValue<string>(out var s) && !string.IsNullOrWhiteSpace(s) ? s.Trim() : null;

    static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
        }

        return null;
    }
}
