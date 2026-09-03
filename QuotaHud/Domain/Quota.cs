using System.Text.Json.Nodes;

namespace QuotaHud;

public static class QuotaParser
{
    static readonly string[] CapKeys = ["limit", "limit_window_seconds", "cap", "max", "quota", "limit_tokens"];

    public static ChatgptUsage ParseChatgptUsage(JsonNode payload)
    {
        var rate = AsObject(payload["rate_limit"]) ?? AsObject(payload["rateLimit"]) ?? AsObject(payload) ?? new JsonObject();
        NeedleQuota? five = null;
        NeedleQuota? week = null;
        foreach (var key in new[] { "primary_window", "primaryWindow", "secondary_window", "secondaryWindow" })
        {
            var window = rate[key];
            if (window is null) continue;
            var parsed = ParseWindowNode(window);
            var kind = ClassifyWindow(window, five is not null, week is not null);
            if (kind == "week") week ??= parsed;
            else five ??= parsed;
        }

        return new ChatgptUsage
        {
            FiveHour = five ?? new NeedleQuota { Unlimited = true },
            Weekly = week ?? new NeedleQuota { Missing = true },
            PlanType = FirstString(
                payload["plan_type"] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null,
                payload["planType"] is JsonValue p && p.TryGetValue<string>(out var pt) ? pt : null,
                Str(rate, "plan_type"),
                Str(rate, "planType"),
                Str(rate, "plan")),
        };
    }

    public static NeedleQuota ParseFiveHourWindow(JsonNode payload) => ParseChatgptUsage(payload).FiveHour;

    public static NeedleQuota ParseWeeklyWindow(JsonNode payload) => ParseChatgptUsage(payload).Weekly;

    public static NeedleQuota ParseGrokBilling(JsonNode payload)
    {
        var config = AsObject(payload["config"]) ?? AsObject(payload);
        if (config is null) return new NeedleQuota { Missing = true };
        var used = PickGrokUsedPercent(config);
        if (used is null) return new NeedleQuota { ResetAt = PickGrokReset(config), Missing = true };
        return new NeedleQuota
        {
            RemainingPercent = Clamp(100 - used.Value, 0, 100),
            UsedPercent = used,
            ResetAt = PickGrokReset(config),
            Missing = false,
        };
    }

    public static NeedleQuota Decorate(NeedleQuota? windowLike)
    {
        if (windowLike is null) return new NeedleQuota();
        if (windowLike.Unlimited)
        {
            windowLike.Display = "∞";
            windowLike.Color = QuotaColors.RemainingColor(100);
            windowLike.Hex = QuotaColors.RemainingHex(100);
            return windowLike;
        }

        var pct = windowLike.RemainingPercent;
        windowLike.Display = pct is null ? "—" : $"{Math.Round(pct.Value)}%";
        windowLike.Color = QuotaColors.RemainingColor(pct);
        windowLike.Hex = QuotaColors.RemainingHex(pct);
        return windowLike;
    }

    public static AccountQuota DecorateNeedle(NeedleQuota windowLike)
    {
        var n = Decorate(windowLike);
        return new AccountQuota
        {
            RemainingPercent = n.RemainingPercent,
            Unlimited = n.Unlimited,
            Missing = n.Missing,
            Display = n.Display,
            Color = n.Color,
            Hex = n.Hex,
            ResetLabel = n.ResetLabel,
        };
    }

    public static bool HasCapField(JsonNode? window)
    {
        var obj = AsObject(window);
        if (obj is null) return false;
        if (IsUnlimited(obj)) return false;
        foreach (var key in CapKeys)
        {
            if (!obj.ContainsKey(key)) continue;
            var value = obj[key];
            if (value is null || (value is JsonValue b && b.TryGetValue<bool>(out var flag) && flag == false)) continue;
            if (value is JsonValue n && n.TryGetValue<double>(out var num))
            {
                if (double.IsFinite(num) && num > 0) return true;
                continue;
            }

            if (value is JsonValue s && s.TryGetValue<string>(out var text))
            {
                if (double.TryParse(text, out var parsed) && double.IsFinite(parsed) && parsed > 0) return true;
                continue;
            }

            return true;
        }

        return false;
    }

    static NeedleQuota ParseWindowNode(JsonNode window)
    {
        if (AsObject(window) is null || IsUnlimited(window) || !HasCapField(window))
            return new NeedleQuota { Unlimited = true, ResetAt = ResetFromWindow(window) };
        return ParseCappedWindow(window);
    }

    static string ClassifyWindow(JsonNode window, bool fiveTaken, bool weekTaken)
    {
        var seconds = WindowSeconds(window);
        if (seconds is >= 86_400) return "week";
        if (seconds is > 0 and < 86_400) return "five";
        if (!fiveTaken) return "five";
        return weekTaken ? "five" : "week";
    }

    static double? WindowSeconds(JsonNode? window)
    {
        var obj = AsObject(window);
        if (obj is null) return null;
        return ToDouble(obj["limit_window_seconds"] ?? obj["limitWindowSeconds"] ?? obj["window_seconds"]);
    }

    static NeedleQuota ParseCappedWindow(JsonNode window)
    {
        var obj = AsObject(window) ?? new JsonObject();
        var usedRaw = obj["used_percent"] ?? obj["usedPercent"];
        var used = ToDouble(usedRaw);
        if (used is null || !double.IsFinite(used.Value))
        {
            return new NeedleQuota { ResetAt = ResetFromWindow(window) };
        }

        return new NeedleQuota
        {
            RemainingPercent = Clamp(100 - used.Value, 0, 100),
            ResetAt = ResetFromWindow(window),
            UsedPercent = used,
        };
    }

    static object? ResetFromWindow(JsonNode? window)
    {
        var obj = AsObject(window);
        if (obj is null) return null;
        if (obj["reset_at"] is not null) return Unwrap(obj["reset_at"]);
        if (obj["resetAt"] is not null) return Unwrap(obj["resetAt"]);
        if (obj["reset_after_seconds"] is not null)
            return new Dictionary<string, double?> { ["afterSeconds"] = ToDouble(obj["reset_after_seconds"]) };
        return null;
    }

    static double? PickGrokUsedPercent(JsonObject config)
    {
        var products = config["productUsage"] as JsonArray ?? config["product_usage"] as JsonArray;
        if (products is not null)
        {
            foreach (var item in products)
            {
                var product = Str(item, "product");
                if (product is "GrokBuild" or "grok-build")
                {
                    var fromProduct = ToDouble(item?["usagePercent"] ?? item?["usage_percent"]);
                    if (fromProduct is not null && double.IsFinite(fromProduct.Value)) return fromProduct;
                }
            }
        }

        var credit = ToDouble(config["creditUsagePercent"] ?? config["credit_usage_percent"]);
        if (credit is not null && double.IsFinite(credit.Value)) return credit;
        var monthly = AsObject(config["monthlyLimit"]) ?? AsObject(config["monthly_limit"]);
        var usedObj = AsObject(config["used"]);
        var limitVal = ToDouble(config["monthlyLimit"]) ?? ToDouble(monthly?["val"]);
        var usedVal = ToDouble(config["used"]) ?? ToDouble(usedObj?["val"]);
        if (limitVal is not null && limitVal.Value > 0 && usedVal is not null && double.IsFinite(usedVal.Value))
            return Clamp(usedVal.Value / limitVal.Value * 100, 0, 100);
        return null;
    }

    static object? PickGrokReset(JsonObject config)
    {
        var period = AsObject(config["currentPeriod"]) ?? AsObject(config["current_period"]) ?? new JsonObject();
        return Unwrap(period["end"]) ?? Unwrap(config["billingPeriodEnd"]) ?? Unwrap(config["billing_period_end"]);
    }

    static bool IsUnlimited(JsonNode? node)
    {
        var obj = AsObject(node);
        return obj?["unlimited"] is JsonValue v && v.TryGetValue<bool>(out var flag) && flag;
    }

    static JsonObject? AsObject(JsonNode? node) => node as JsonObject;

    static double Clamp(double n, double min, double max) => Math.Min(max, Math.Max(min, n));

    static double? ToDouble(JsonNode? node)
    {
        if (node is JsonValue v)
        {
            if (v.TryGetValue<double>(out var d)) return d;
            if (v.TryGetValue<int>(out var i)) return i;
            if (v.TryGetValue<long>(out var l)) return l;
            if (v.TryGetValue<string>(out var s) && double.TryParse(s, out var parsed)) return parsed;
        }

        return null;
    }

    static object? Unwrap(JsonNode? node)
    {
        if (node is null) return null;
        if (node is JsonValue v)
        {
            if (v.TryGetValue<string>(out var s)) return s;
            if (v.TryGetValue<double>(out var d)) return d;
            if (v.TryGetValue<long>(out var l)) return l;
        }

        return node.ToJsonString();
    }

    static string? Str(JsonNode? node, string key)
    {
        if (node is not JsonObject obj) return null;
        return obj[key] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
    }

    public static string? ParseGrokPlan(JsonNode payload)
    {
        var config = AsObject(payload["config"]) ?? AsObject(payload);
        if (config is null) return null;
        return FirstString(
            Str(config, "plan"),
            Str(config, "planName"),
            Str(config, "plan_name"),
            Str(config, "subscription"),
            Str(config, "subscriptionTier"),
            Str(config, "tier"),
            Str(config, "product"),
            Str(config, "sku"));
    }

    static string? FirstString(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
        }

        return null;
    }
}
