using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace QuotaHud;

public static class Credentials
{
    static readonly HashSet<string> GrokSkipKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "credential_pool", "credentialPool", "providers", "tokens", "version",
        "accounts", "auth_mode", "last_refresh", "OPENAI_API_KEY",
    };

    public static IReadOnlyList<Account> ParseChatgptAuth(JsonNode json)
    {
        var fromPool = CollectFromPool(json, "openai-codex", "chatgpt");
        if (fromPool.Present) return DedupeChatgpt(fromPool.Accounts);
        var fromAccounts = CollectAccountsArray(json, "chatgpt");
        if (fromAccounts.Count > 0) return DedupeChatgpt(fromAccounts);
        var fromSingleton = CollectCodexSingleton(json);
        if (fromSingleton.Count > 0) return DedupeChatgpt(fromSingleton);
        return DedupeChatgpt(CollectProviderSingleton(json, "openai-codex", "chatgpt"));
    }

    public static IReadOnlyList<Account> ParseGrokAuth(JsonNode json)
    {
        var fromPool = CollectFromPool(json, "xai-oauth", "grok");
        if (fromPool.Present) return fromPool.Accounts;
        var fromAccounts = CollectAccountsArray(json, "grok");
        if (fromAccounts.Count > 0) return fromAccounts;
        var fromMap = CollectGrokCliMap(json);
        if (fromMap.Count > 0) return fromMap;
        if (json is JsonObject obj)
        {
            var wrapped = new JsonObject { ["tokens"] = obj["tokens"]?.DeepClone() };
            foreach (var kv in obj)
            {
                if (kv.Key == "tokens") continue;
                wrapped[kv.Key] = kv.Value?.DeepClone();
            }
            var fromTokens = CollectCodexSingleton(wrapped);
            if (fromTokens.Count > 0)
            {
                foreach (var item in fromTokens)
                {
                    item.Side = "grok";
                    if (item.Id == "codex-default") item.Id = "grok-default";
                }
                return fromTokens;
            }
        }

        return CollectProviderSingleton(json, "xai-oauth", "grok");
    }

    public static LoadedSource LoadFromSource(string source, IAuthFileReader fs, CredentialPaths paths)
    {
        if (source == QuotaPaths.Hermes)
        {
            var file = ReadJsonFile(fs, paths.Hermes ?? paths.Chatgpt);
            if (!file.Ok)
            {
                return new LoadedSource
                {
                    Source = source,
                    Grok = new SideLoad { Error = file.Error },
                    Chatgpt = new SideLoad { Error = file.Error },
                    FilesRead = [file.Path],
                };
            }

            var chatgpt = ParseChatgptAuth(file.Json!);
            var grok = ParseGrokAuth(file.Json!);
            return new LoadedSource
            {
                Source = source,
                Grok = new SideLoad
                {
                    Accounts = grok.ToList(),
                    Error = grok.Count > 0 ? null : new QuotaError("emptyPool", ErrorMessages.EmptyPool),
                },
                Chatgpt = new SideLoad
                {
                    Accounts = chatgpt.ToList(),
                    Error = chatgpt.Count > 0 ? null : new QuotaError("emptyPool", ErrorMessages.EmptyPool),
                },
                FilesRead = [file.Path],
            };
        }

        var chatgptFile = ReadJsonFile(fs, paths.Chatgpt);
        var grokFile = ReadJsonFile(fs, paths.Grok);
        var chatgptAccounts = new List<Account>();
        QuotaError? chatgptError = null;
        if (!chatgptFile.Ok) chatgptError = chatgptFile.Error;
        else
        {
            chatgptAccounts = ParseChatgptAuth(chatgptFile.Json!).ToList();
            if (chatgptAccounts.Count == 0) chatgptError = new QuotaError("emptyPool", ErrorMessages.EmptyPool);
        }

        var grokAccounts = new List<Account>();
        QuotaError? grokError = null;
        if (!grokFile.Ok) grokError = grokFile.Error;
        else
        {
            grokAccounts = ParseGrokAuth(grokFile.Json!).ToList();
            if (grokAccounts.Count == 0) grokError = new QuotaError("emptyPool", ErrorMessages.EmptyPool);
        }

        return new LoadedSource
        {
            Source = source,
            Grok = new SideLoad { Accounts = grokAccounts, Error = grokError },
            Chatgpt = new SideLoad { Accounts = chatgptAccounts, Error = chatgptError },
            FilesRead = [chatgptFile.Path, grokFile.Path],
        };
    }

    static (bool Present, List<Account> Accounts) CollectFromPool(JsonNode json, string provider, string side)
    {
        var pool = CredentialPool(json);
        if (pool is null) return (false, []);
        if (pool[provider] is not JsonArray entries) return (true, []);
        var outList = new List<Account>();
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            var id = Str(entry, "id") ?? Str(entry, "label") ?? $"{provider}-{i}";
            UniquePush(outList, MakeAccount(side, id, entry, null, null));
        }

        return (true, outList);
    }

    static List<Account> CollectCodexSingleton(JsonNode json)
    {
        var obj = AsObject(json);
        if (obj is null) return [];
        var tokens = AsObject(obj["tokens"]);
        if (tokens is null && obj["access_token"] is null) return [];
        var entry = tokens is null ? obj : CloneWith(obj, "tokens", tokens);
        var token = TokenFromEntry(entry);
        var id = AccountIdFromEntry(entry, token) ?? "codex-default";
        var account = MakeAccount("chatgpt", id, entry, token, null);
        return account is null ? [] : [account];
    }

    static List<Account> CollectAccountsArray(JsonNode json, string side)
    {
        JsonArray? list = json["accounts"] as JsonArray;
        if (list is null && json is JsonArray arr) list = arr;
        if (list is null) return [];
        var outList = new List<Account>();
        for (var i = 0; i < list.Count; i++)
        {
            var entry = list[i];
            var nested = AsObject(entry?["auth"]) ?? entry;
            var id = Str(entry, "id")
                ?? Str(entry, "accountId")
                ?? Str(entry, "account_id")
                ?? AccountIdFromEntry(nested, TokenFromEntry(nested))
                ?? $"{side}-{i}";
            UniquePush(outList, MakeAccount(side, id, nested, null, null));
        }

        return outList;
    }

    static List<Account> CollectGrokCliMap(JsonNode json)
    {
        if (json is not JsonObject obj) return [];
        var outList = new List<Account>();
        foreach (var kv in obj)
        {
            if (GrokSkipKeys.Contains(kv.Key)) continue;
            var entry = AsObject(kv.Value);
            if (entry is null) continue;
            if (entry["key"] is null && entry["access_token"] is null && AsObject(entry["tokens"]) is null) continue;
            UniquePush(outList, MakeAccount(
                "grok",
                Str(entry, "user_id") ?? Str(entry, "userId") ?? kv.Key,
                entry,
                null,
                Str(entry, "user_id") ?? Str(entry, "userId")));
        }

        return outList;
    }

    static List<Account> CollectProviderSingleton(JsonNode json, string provider, string side)
    {
        var providers = AsObject(json["providers"]);
        var state = providers is null ? null : AsObject(providers[provider]);
        if (state is null) return [];
        var entry = AsObject(state["tokens"]) is { } tokens ? CloneWith(state, "tokens", tokens) : state;
        var id = Str(state, "label") ?? AccountIdFromEntry(entry, TokenFromEntry(entry)) ?? provider;
        var account = MakeAccount(side, id, entry, null, null);
        return account is null ? [] : [account];
    }

    static JsonObject? CredentialPool(JsonNode json) =>
        AsObject(json["credential_pool"]) ?? AsObject(json["credentialPool"]);

    static Account? MakeAccount(string side, string id, JsonNode? entry, string? token, string? userId)
    {
        var accessToken = (token ?? TokenFromEntry(entry) ?? "").Trim();
        if (accessToken.Length == 0) return null;
        var email = EmailFromEntry(entry, accessToken);
        var accountId = AccountIdFromEntry(entry, accessToken);
        var claims = Jwt.DecodePayload(accessToken);
        return new Account
        {
            Side = side,
            Id = id,
            Label = LabelFromEntry(entry, email ?? id),
            Email = email,
            AccountId = accountId,
            AccessToken = accessToken,
            UserId = NestedStr(entry, "user_id", "userId") ?? userId,
            RefreshToken = NestedStr(entry, "refresh_token", "refreshToken"),
            ExpiresAt = NestedStr(entry, "expires_at", "expiresAt"),
            OidcIssuer = NestedStr(entry, "oidc_issuer", "oidcIssuer", "issuer") ?? Jwt.Issuer(claims),
            OidcClientId = NestedStr(entry, "oidc_client_id", "oidcClientId", "client_id", "clientId") ?? Jwt.ClientId(claims),
            PlanHint = Jwt.ChatgptPlanType(claims),
        };
    }

    static string TokenFromEntry(JsonNode? entry)
    {
        if (entry is JsonValue v && v.TryGetValue<string>(out var s) && !string.IsNullOrWhiteSpace(s)) return s.Trim();
        var obj = AsObject(entry);
        if (obj is null) return "";
        var tokens = AsObject(obj["tokens"]) ?? AsObject(obj["token"]);
        var keyObj = AsObject(obj["key"]);
        return FirstNonEmpty(
            Str(obj, "access_token"),
            Str(obj, "accessToken"),
            Str(obj, "key"),
            keyObj is null ? null : Str(keyObj, "access_token"),
            keyObj is null ? null : Str(keyObj, "accessToken"),
            tokens is null ? null : Str(tokens, "access_token"),
            tokens is null ? null : Str(tokens, "accessToken"),
            tokens is null ? null : Str(tokens, "key")) ?? "";
    }

    static string? EmailFromEntry(JsonNode? entry, string token)
    {
        var obj = AsObject(entry) ?? new JsonObject();
        var claims = Jwt.DecodePayload(token);
        return FirstNonEmpty(
            Str(obj, "email"),
            Str(obj, "label_email"),
            AsObject(obj["tokens"]) is { } tokens ? Str(tokens, "email") : null,
            Jwt.EmailFromClaims(claims));
    }

    static string? AccountIdFromEntry(JsonNode? entry, string token)
    {
        var obj = AsObject(entry) ?? new JsonObject();
        var tokens = AsObject(obj["tokens"]) ?? new JsonObject();
        var claims = Jwt.DecodePayload(token);
        return FirstNonEmpty(
            Str(obj, "account_id"),
            Str(obj, "accountId"),
            Str(obj, "chatgpt_account_id"),
            Str(obj, "chatgptAccountId"),
            Str(tokens, "account_id"),
            Str(tokens, "accountId"),
            Jwt.ChatgptAccountIdFromClaims(claims));
    }

    static string LabelFromEntry(JsonNode? entry, string fallback)
    {
        var obj = AsObject(entry);
        var label = obj is null ? null : FirstNonEmpty(Str(obj, "label"), Str(obj, "name"));
        return label ?? fallback;
    }

    static void UniquePush(List<Account> list, Account? account)
    {
        if (account is null) return;
        var key = $"{account.Side}:{account.Id}";
        if (list.Any(item => $"{item.Side}:{item.Id}" == key)) return;
        list.Add(account);
    }

    // Hermes sometimes copies one OAuth login into every openai-codex slot.
    // Collapse only identical access tokens. Team seats can share chatgpt_account_id
    // while still being distinct logins with different tokens — keep those separate.
    static List<Account> DedupeChatgpt(IReadOnlyList<Account> accounts)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var list = new List<Account>();
        foreach (var account in accounts)
        {
            if (!seen.Add(account.AccessToken)) continue;
            list.Add(account);
        }

        return list;
    }

    static (bool Ok, JsonNode? Json, QuotaError? Error, string Path) ReadJsonFile(IAuthFileReader fs, string filePath)
    {
        try
        {
            var raw = fs.ReadAllText(filePath);
            try
            {
                return (true, JsonNode.Parse(raw), null, filePath);
            }
            catch (JsonException)
            {
                return (false, null, new QuotaError("missingFile", ErrorMessages.MissingFile), filePath);
            }
        }
        catch (Exception)
        {
            return (false, null, new QuotaError("missingFile", ErrorMessages.MissingFile), filePath);
        }
    }

    static JsonObject? AsObject(JsonNode? node) =>
        node is JsonObject obj ? obj : null;

    static JsonObject CloneWith(JsonObject obj, string key, JsonNode value)
    {
        var clone = new JsonObject();
        foreach (var kv in obj) clone[kv.Key] = kv.Value?.DeepClone();
        clone[key] = value.DeepClone();
        return clone;
    }

    static string? Str(JsonNode? node, string key)
    {
        var obj = AsObject(node);
        if (obj is null) return null;
        var value = obj[key];
        if (value is JsonValue v)
        {
            if (v.TryGetValue<string>(out var s)) return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
            return v.ToJsonString().Trim('"');
        }

        return null;
    }

    static string? NestedStr(JsonNode? entry, params string[] keys)
    {
        var obj = AsObject(entry);
        if (obj is null) return null;
        var nested = new JsonObject?[] { obj, AsObject(obj["tokens"]), AsObject(obj["token"]), AsObject(obj["key"]) };
        foreach (var key in keys)
        {
            foreach (var node in nested)
            {
                if (node is null) continue;
                var value = Str(node, key);
                if (!string.IsNullOrWhiteSpace(value)) return value;
            }
        }

        return null;
    }

    static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
        }

        return null;
    }
}

public sealed class ReadOnlyFileSystem : IAuthFileReader
{
    public string ReadAllText(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
