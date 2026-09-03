namespace QuotaHud;

public sealed record CredentialPaths(string Source, string? Hermes, string Chatgpt, string Grok);

public static class QuotaPaths
{
    public const string Hermes = "hermes";
    public const string Terminal = "terminal";

    public static string HomeDir(IReadOnlyDictionary<string, string?> env, bool windows)
    {
        var custom = Get(env, "QUOTA_WIDGET_HOME");
        if (!string.IsNullOrWhiteSpace(custom)) return custom;
        if (windows)
        {
            return First(env, "USERPROFILE", "HOME")
                ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        return Get(env, "HOME")
            ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

    public static string HermesHome(IReadOnlyDictionary<string, string?> env, bool windows)
    {
        var fromEnv = Get(env, "HERMES_HOME");
        if (!string.IsNullOrWhiteSpace(fromEnv)) return fromEnv;
        if (windows)
        {
            var local = Get(env, "LOCALAPPDATA");
            if (!string.IsNullOrWhiteSpace(local)) return Path.Combine(local, "hermes");
            return Path.Combine(HomeDir(env, true), "AppData", "Local", "hermes");
        }

        return Path.Combine(HomeDir(env, false), ".hermes");
    }

    public static string HermesAuthPath(
        IReadOnlyDictionary<string, string?> env,
        bool windows,
        Func<string, bool>? exists = null)
    {
        exists ??= File.Exists;
        var primary = Path.Combine(HermesHome(env, windows), "auth.json");
        if (exists(primary)) return primary;
        var legacy = Path.Combine(HomeDir(env, windows), ".hermes", "auth.json");
        if (exists(legacy)) return legacy;
        return primary;
    }

    public static CredentialPaths ForSource(
        string source,
        IReadOnlyDictionary<string, string?> env,
        bool windows,
        Func<string, bool>? exists = null)
    {
        if (source == Terminal)
        {
            var home = HomeDir(env, windows);
            return new CredentialPaths(
                Terminal,
                Hermes: null,
                Chatgpt: Path.Combine(home, ".codex", "auth.json"),
                Grok: Path.Combine(home, ".grok", "auth.json"));
        }

        var hermes = HermesAuthPath(env, windows, exists);
        return new CredentialPaths(Hermes, hermes, hermes, hermes);
    }

    public static CredentialPaths ForSource(string source)
    {
        var env = SnapshotProcessEnv();
        var windows = OperatingSystem.IsWindows();
        return ForSource(source, env, windows);
    }

    public static IReadOnlyDictionary<string, string?> SnapshotProcessEnv()
    {
        var map = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            map[entry.Key.ToString()!] = entry.Value?.ToString();
        }

        return map;
    }

    static string? First(IReadOnlyDictionary<string, string?> env, params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = Get(env, key);
            if (!string.IsNullOrWhiteSpace(value)) return value;
        }

        return null;
    }

    static string? Get(IReadOnlyDictionary<string, string?> env, string key)
    {
        if (env.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)) return value;
        foreach (var pair in env)
        {
            if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(pair.Value))
            {
                return pair.Value;
            }
        }

        return null;
    }
}
