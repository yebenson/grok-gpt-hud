namespace QuotaHud;

public static class ErrorMessages
{
    public static string MissingFile => UiText.Error("missingFile");
    public static string EmptyPool => UiText.Error("emptyPool");
    public static string Expired => UiText.Error("expired");
    public static string Forbidden => UiText.Error("forbidden");
    public static string Network => UiText.Error("network");
    public static string Retry => UiText.Error("retry");
    public static string Parse => UiText.Error("parse");
    public static string MissingQuota => UiText.Error("missingQuota");
}

public sealed class HttpStatusException(int status, string message) : Exception(message)
{
    public int Status { get; } = status;
}

public static class ErrorClassifier
{
    public static QuotaError Classify(Exception? error)
    {
        if (error is null) return new QuotaError("retry", ErrorMessages.Retry);
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
            return new QuotaError("retry", ErrorMessages.Retry);
        }

        return new QuotaError("retry", ErrorMessages.Retry);
    }

    public static string? SideErrorMessage(QuotaError? error) =>
        error is null ? null : UiText.Error(error.Code);
}
