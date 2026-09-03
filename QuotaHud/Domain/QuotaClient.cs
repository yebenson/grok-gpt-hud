using System.Net.Http.Headers;
using System.Text.Json.Nodes;

namespace QuotaHud;

public sealed class QuotaClient
{
    public const string ChatgptUsageUrl = "https://chatgpt.com/backend-api/wham/usage";
    public const string GrokBillingUrl = "https://cli-chat-proxy.grok.com/v1/billing?format=credits";
    public const string OpenAiTokenFallback = "https://auth.openai.com/oauth/token";
    public const string XaiTokenFallback = "https://auth.x.ai/oauth2/token";
    public const string OpenAiClientFallback = "app_EMoamEEZ73f0CkXaXp7hrann";
    readonly HttpClient _http;

    public QuotaClient(HttpClient? http = null)
    {
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
    }

    public async Task<JsonNode> FetchChatgpt(Account account, CancellationToken ct = default)
    {
        var token = account.AccessToken;
        if (GrokAuth.NeedsRefresh(account, DateTime.UtcNow))
        {
            var refreshed = await TryRefresh(account, openai: true, ct);
            if (!string.IsNullOrWhiteSpace(refreshed)) token = refreshed;
        }

        try
        {
            return await GetChatgpt(token, account, ct);
        }
        catch (HttpStatusException ex) when (ex.Status == 401)
        {
            var refreshed = await TryRefresh(account, openai: true, ct);
            if (string.IsNullOrWhiteSpace(refreshed) || refreshed == token) throw;
            return await GetChatgpt(refreshed, account, ct);
        }
    }

    public async Task<JsonNode> FetchGrok(Account account, CancellationToken ct = default)
    {
        var token = account.AccessToken;
        if (GrokAuth.NeedsRefresh(account, DateTime.UtcNow))
        {
            var refreshed = await TryRefresh(account, openai: false, ct);
            if (!string.IsNullOrWhiteSpace(refreshed)) token = refreshed;
        }

        try
        {
            return await GetGrok(token, account, ct);
        }
        catch (HttpStatusException ex) when (ex.Status == 401)
        {
            var refreshed = await TryRefresh(account, openai: false, ct);
            if (string.IsNullOrWhiteSpace(refreshed) || refreshed == token) throw;
            return await GetGrok(refreshed, account, ct);
        }
    }

    Task<JsonNode> GetChatgpt(string token, Account account, CancellationToken ct) =>
        GetJson(
            ChatgptUsageUrl,
            token,
            headers =>
            {
                if (!string.IsNullOrWhiteSpace(account.AccountId))
                {
                    headers.TryAddWithoutValidation("ChatGPT-Account-Id", account.AccountId);
                    headers.TryAddWithoutValidation("ChatGPT-Account-ID", account.AccountId);
                }
            },
            ct);

    Task<JsonNode> GetGrok(string token, Account account, CancellationToken ct) =>
        GetJson(
            GrokBillingUrl,
            token,
            headers =>
            {
                headers.TryAddWithoutValidation("X-XAI-Token-Auth", "xai-grok-cli");
                headers.TryAddWithoutValidation("x-grok-client-version", "1.0.13");
                headers.TryAddWithoutValidation("x-grok-client-mode", "cli");
                headers.TryAddWithoutValidation(
                    "x-grok-client-identifier",
                    string.IsNullOrWhiteSpace(account.OidcClientId) ? "grok-cli" : account.OidcClientId);
                if (!string.IsNullOrWhiteSpace(account.UserId))
                    headers.TryAddWithoutValidation("x-userid", account.UserId);
                if (!string.IsNullOrWhiteSpace(account.Email))
                    headers.TryAddWithoutValidation("x-email", account.Email);
            },
            ct);

    async Task<string?> TryRefresh(Account account, bool openai, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(account.RefreshToken)) return null;
        try
        {
            var issuer = (string.IsNullOrWhiteSpace(account.OidcIssuer)
                ? (openai ? "https://auth.openai.com" : GrokAuth.DefaultIssuer)
                : account.OidcIssuer).Trim().TrimEnd('/');
            var tokenEndpoint = await DiscoverTokenEndpoint(issuer, openai, ct);
            if (string.IsNullOrWhiteSpace(tokenEndpoint)) return null;

            var form = new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = account.RefreshToken,
            };
            var clientId = account.OidcClientId;
            if (string.IsNullOrWhiteSpace(clientId) && openai) clientId = OpenAiClientFallback;
            if (!string.IsNullOrWhiteSpace(clientId)) form["client_id"] = clientId;

            using var tokenReq = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
            {
                Content = new FormUrlEncodedContent(form),
            };
            using var tokenResp = await _http.SendAsync(tokenReq, ct);
            if (!tokenResp.IsSuccessStatusCode) return null;
            var json = JsonNode.Parse(await tokenResp.Content.ReadAsStringAsync(ct)) as JsonObject;
            var access = json?["access_token"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(access)) return null;
            account.AccessToken = access;
            if (json?["refresh_token"]?.GetValue<string>() is { Length: > 0 } nextRefresh)
                account.RefreshToken = nextRefresh;
            return access;
        }
        catch
        {
            return null;
        }
    }

    async Task<string?> DiscoverTokenEndpoint(string issuer, bool openai, CancellationToken ct)
    {
        try
        {
            using var discoReq = new HttpRequestMessage(HttpMethod.Get, issuer + "/.well-known/openid-configuration");
            using var discoResp = await _http.SendAsync(discoReq, ct);
            if (discoResp.IsSuccessStatusCode)
            {
                var disco = JsonNode.Parse(await discoResp.Content.ReadAsStringAsync(ct)) as JsonObject;
                var tokenEndpoint = disco?["token_endpoint"]?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(tokenEndpoint)) return tokenEndpoint;
            }
        }
        catch { /* fall back */ }

        if (issuer.Contains("openai.com", StringComparison.OrdinalIgnoreCase) || openai)
            return OpenAiTokenFallback;
        return issuer + "/oauth2/token";
    }

    async Task<JsonNode> GetJson(
        string url,
        string accessToken,
        Action<HttpRequestHeaders> extra,
        CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        extra(req.Headers);
        using var resp = await _http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
            throw new HttpStatusException((int)resp.StatusCode, $"http {(int)resp.StatusCode}");
        var text = await resp.Content.ReadAsStringAsync(ct);
        return JsonNode.Parse(string.IsNullOrWhiteSpace(text) ? "{}" : text) ?? new JsonObject();
    }
}
