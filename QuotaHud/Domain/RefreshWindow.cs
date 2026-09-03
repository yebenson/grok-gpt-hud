namespace QuotaHud;

public static class RefreshWindow
{
    public const int WorkStartHour = 9;
    public const int WorkEndHour = 18;
    public static readonly TimeSpan AutoInterval = TimeSpan.FromMinutes(15);

    public static bool IsAutoRefreshAllowed(DateTime date)
    {
        var local = date.Kind == DateTimeKind.Utc ? date.ToLocalTime() : date;
        return local.Hour >= WorkStartHour && local.Hour < WorkEndHour;
    }

    public static bool ShouldAutoPoll(DateTime now, DateTime? lastPollAt, TimeSpan? interval = null)
    {
        if (!IsAutoRefreshAllowed(now)) return false;
        if (lastPollAt is null) return true;
        var span = interval ?? AutoInterval;
        return now - lastPollAt.Value >= span;
    }
}
