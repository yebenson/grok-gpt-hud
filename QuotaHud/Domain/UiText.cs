namespace QuotaHud;

public static class AppLocale
{
    public const string English = "en";
    public const string Chinese = "zh";

    public static string Normalize(string? value) =>
        string.Equals(value, Chinese, StringComparison.OrdinalIgnoreCase) ? Chinese : English;

    public static bool IsChinese(string? value) => Normalize(value) == Chinese;
}

public static class UiText
{
    static string _locale = AppLocale.English;

    public static string Locale
    {
        get => _locale;
        set => _locale = AppLocale.Normalize(value);
    }

    public static bool IsChinese => AppLocale.IsChinese(_locale);

    public static string RightClick => T("Right-click", "右键");
    public static string Loading => T("Loading quota…", "正在读取配额…");
    public static string RefreshNow => T("Refresh now", "立即刷新");
    public static string AlwaysOnTop => T("Always on top", "窗口置顶");
    public static string Exit => T("Exit", "退出");
    public static string Language => T("Language", "语言");
    public static string English => "English";
    public static string Chinese => "中文";

    public static string Error(string? code) => code switch
    {
        "missingFile" => T("Credentials not found", "读不到凭证"),
        "emptyPool" => T("No accounts in pool", "池里没有账号"),
        "expired" => T("Credentials expired — sign in again in Hermes / OpenClaw or the matching CLI", "凭证过期，去 Hermes / OpenClaw 或对应 CLI 重新登录"),
        "forbidden" => T("Request forbidden", "接口拒绝"),
        "network" => T("Fetch failed", "拉取失败"),
        "retry" => T("Fetch failed — will retry", "拉取失败，将自动重试"),
        "parse" => T("Could not parse quota data", "额度数据无法解析"),
        "missingQuota" => T("No quota data", "没有额度数据"),
        _ => T("Fetch failed", "拉取失败"),
    };

    public static string FormatReset(DateTime local)
    {
        if (IsChinese)
            return "重置 " + local.ToString("M月d日 HH:mm", System.Globalization.CultureInfo.GetCultureInfo("zh-CN"));
        return "Reset " + local.ToString("MMM d HH:mm", System.Globalization.CultureInfo.InvariantCulture);
    }

    static string T(string en, string zh) => IsChinese ? zh : en;
}
