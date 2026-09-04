using System.Globalization;
using System.Text.Json.Nodes;

namespace QuotaHud;

public static class GrokAuth
{
    public const string DefaultIssuer = "https://auth.x.ai";
    const int SkewSeconds = 30;

    public static bool NeedsRefresh(Account account, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(account.RefreshToken)) return false;
        var exp = ParseExpiry(account);
        if (exp is null) return false;
        return utcNow.ToUniversalTime() >= exp.Value.UtcDateTime.AddSeconds(-SkewSeconds);
    }

    public static DateTimeOffset? ParseExpiry(Account account)
    {
        if (!string.IsNullOrWhiteSpace(account.ExpiresAt))
        {
            if (DateTimeOffset.TryParse(account.ExpiresAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
                return parsed;
            if (long.TryParse(account.ExpiresAt, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unix))
            {
                var ms = unix > 1_000_000_000_000L ? unix : unix * 1000L;
                return DateTimeOffset.FromUnixTimeMilliseconds(ms);
            }
        }

        var claims = Jwt.DecodePayload(account.AccessToken ?? "");
        if (claims["exp"] is JsonValue v)
        {
            if (v.TryGetValue<long>(out var unix)) return DateTimeOffset.FromUnixTimeSeconds(unix);
            if (v.TryGetValue<double>(out var number)) return DateTimeOffset.FromUnixTimeSeconds((long)number);
        }

        return null;
    }
}
