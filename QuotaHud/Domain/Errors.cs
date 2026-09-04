namespace QuotaHud;

public static class ErrorMessages
{
    public const string MissingFile = "读不到凭证";
    public const string EmptyPool = "池里没有账号";
    public const string Expired = "凭证过期，去 Hermes / OpenClaw 或对应 CLI 重新登录";
    public const string Forbidden = "接口拒绝";
    public const string Network = "拉取失败";
    public const string Retry = "拉取失败，将自动重试";
    public const string Parse = "额度数据无法解析";
    public const string MissingQuota = "没有额度数据";
}

public sealed class HttpStatusException(int status, string message) : Exception(message)
{
    public int Status { get; } = status;
}

public static class ErrorClassifier
{
    public static QuotaError Classify(Exception? error)
    {
        if (error is null) return new QuotaError("network", ErrorMessages.Retry);
        var status = error is HttpStatusException http ? http.Status : 0;
        if (status == 401) return new QuotaError("expired", ErrorMessages.Expired);
        if (status == 403) return new QuotaError("forbidden", ErrorMessages.Forbidden);
        if (error is FileNotFoundException or DirectoryNotFoundException)
            return new QuotaError("missingFile", ErrorMessages.MissingFile);
        if (error.Message.Contains("fetch", StringComparison.OrdinalIgnoreCase)
            || error.Message.Contains("network", StringComparison.OrdinalIgnoreCase)
            || error.Message.Contains("socket", StringComparison.OrdinalIgnoreCase)
            || error is HttpRequestException or TaskCanceledException or TimeoutException)
        {
            return new QuotaError("network", ErrorMessages.Retry);
        }

        return new QuotaError("network", ErrorMessages.Retry);
    }

    public static string? SideErrorMessage(QuotaError? error) => error?.Message;
}
