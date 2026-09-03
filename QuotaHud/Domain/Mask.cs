namespace QuotaHud;

public static class Mask
{
    public static string MaskEmail(string? email)
    {
        if (string.IsNullOrEmpty(email) || !email.Contains('@')) return email ?? "";
        var at = email.LastIndexOf('@');
        var local = email[..at];
        var domain = email[(at + 1)..];
        if (local.Length == 0) return $"****@{domain}";
        if (local.Length == 1) return $"{local}****@{domain}";
        if (local.Length == 2) return $"{local[0]}****{local[1]}@{domain}";
        return $"{local[0]}****{local[^1]}@{domain}";
    }

    public static string DisplayLabel(Account? account)
    {
        if (account is null) return "账号";
        var masked = string.IsNullOrEmpty(account.Email) ? "" : MaskEmail(account.Email);
        if (!string.IsNullOrEmpty(masked)) return masked;
        if (!string.IsNullOrWhiteSpace(account.Label)) return account.Label.Trim();
        if (!string.IsNullOrWhiteSpace(account.Id))
        {
            var id = account.Id.Trim();
            return id.Length > 12 ? $"{id[..6]}…" : id;
        }

        return "账号";
    }

    public static bool IsPlaceholderLabel(string? label)
    {
        if (string.IsNullOrWhiteSpace(label)) return true;
        var text = label.Trim();
        return text.Equals("device_code", StringComparison.OrdinalIgnoreCase)
            || text.Equals("device-code", StringComparison.OrdinalIgnoreCase);
    }

    public static string MenuLabel(Account? account)
    {
        if (account is null) return "账号";
        if (!string.IsNullOrEmpty(account.Email)) return MaskEmail(account.Email);
        if (!IsPlaceholderLabel(account.Label) && !string.IsNullOrWhiteSpace(account.Label))
            return account.Label.Trim();
        if (account.Side == "grok") return "Grok";
        if (account.Side == "chatgpt") return "ChatGPT";
        return DisplayLabel(account);
    }
}
