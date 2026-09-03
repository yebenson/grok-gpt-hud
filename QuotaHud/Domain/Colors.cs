using System.Drawing;

namespace QuotaHud;

public static class QuotaColors
{
    public const string Blue = "blue";
    public const string Yellow = "yellow";
    public const string Red = "red";

    public static readonly Color BlueColor = ColorTranslator.FromHtml("#007AFF");
    public static readonly Color YellowColor = ColorTranslator.FromHtml("#FF9F0A");
    public static readonly Color RedColor = ColorTranslator.FromHtml("#FF3B30");

    public static string? RemainingColor(double? remainingPercent)
    {
        if (remainingPercent is null) return null;
        if (double.IsNaN(remainingPercent.Value) || double.IsInfinity(remainingPercent.Value)) return null;
        if (remainingPercent.Value >= 67) return Blue;
        if (remainingPercent.Value >= 33) return Yellow;
        return Red;
    }

    public static string? RemainingHex(double? remainingPercent) => RemainingColor(remainingPercent) switch
    {
        Blue => "#007AFF",
        Yellow => "#FF9F0A",
        Red => "#FF3B30",
        _ => null,
    };

    public static Color? RemainingDrawingColor(double? remainingPercent) => RemainingColor(remainingPercent) switch
    {
        Blue => BlueColor,
        Yellow => YellowColor,
        Red => RedColor,
        _ => null,
    };

    public static Color FromName(string? name) => name switch
    {
        Blue => BlueColor,
        Yellow => YellowColor,
        Red => RedColor,
        _ => Color.FromArgb(180, 60, 60, 67),
    };
}
