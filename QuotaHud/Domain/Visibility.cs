using System.Text.Json;
using System.Text.Json.Serialization;

namespace QuotaHud;

public sealed class SideHidden
{
    public List<string> Grok { get; set; } = [];
    public List<string> Chatgpt { get; set; } = [];
}

public sealed class HiddenSettings
{
    public SideHidden Hermes { get; set; } = new();
    public SideHidden Terminal { get; set; } = new();
}

public sealed class WidgetSettings
{
    public string Source { get; set; } = QuotaPaths.Hermes;
    public HiddenSettings Hidden { get; set; } = new();
    public int TransparencyPercent { get; set; }
    public bool AlwaysOnTop { get; set; } = true;
}

public interface ISettingsBackend
{
    WidgetSettings Load();
    void Save(WidgetSettings settings);
}

public sealed class FileSettingsBackend(string path) : ISettingsBackend
{
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public WidgetSettings Load()
    {
        try
        {
            if (!File.Exists(path)) return new WidgetSettings();
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<WidgetSettings>(json, JsonOptions) ?? new WidgetSettings();
        }
        catch
        {
            return new WidgetSettings();
        }
    }

    public void Save(WidgetSettings settings)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(path, JsonSerializer.Serialize(settings, JsonOptions));
    }
}

public sealed class VisibilityStore(ISettingsBackend backend)
{
    WidgetSettings _data = Normalize(backend.Load());

    public string GetSource() => _data.Source == QuotaPaths.Terminal ? QuotaPaths.Terminal : QuotaPaths.Hermes;

    public void SetSource(string source)
    {
        _data.Source = source == QuotaPaths.Terminal ? QuotaPaths.Terminal : QuotaPaths.Hermes;
        Persist();
    }

    public bool IsVisible(string source, string side, string id)
    {
        var list = ListHidden(source, side);
        return !list.Contains(id);
    }

    public void SetVisible(string source, string side, string id, bool visible)
    {
        var bucket = Bucket(source, side);
        var key = id;
        if (visible) bucket.RemoveAll(x => x == key);
        else if (!bucket.Contains(key)) bucket.Add(key);
        Persist();
    }

    public IReadOnlyList<string> ListHidden(string source, string side) => [.. Bucket(source, side)];

    public int GetTransparency() => Math.Clamp(_data.TransparencyPercent, 0, 90);

    public void SetTransparency(int percent)
    {
        _data.TransparencyPercent = Math.Clamp(percent, 0, 90);
        Persist();
    }

    public bool GetAlwaysOnTop() => _data.AlwaysOnTop;

    public void SetAlwaysOnTop(bool value)
    {
        _data.AlwaysOnTop = value;
        Persist();
    }

    List<string> Bucket(string source, string side)
    {
        var map = source == QuotaPaths.Terminal ? _data.Hidden.Terminal : _data.Hidden.Hermes;
        return side == "grok" ? map.Grok : map.Chatgpt;
    }

    void Persist()
    {
        backend.Save(_data);
        _data = Normalize(backend.Load());
    }

    static WidgetSettings Normalize(WidgetSettings? raw)
    {
        raw ??= new WidgetSettings();
        raw.Source = raw.Source == QuotaPaths.Terminal ? QuotaPaths.Terminal : QuotaPaths.Hermes;
        raw.Hidden ??= new HiddenSettings();
        raw.Hidden.Hermes ??= new SideHidden();
        raw.Hidden.Terminal ??= new SideHidden();
        raw.Hidden.Hermes.Grok ??= [];
        raw.Hidden.Hermes.Chatgpt ??= [];
        raw.Hidden.Terminal.Grok ??= [];
        raw.Hidden.Terminal.Chatgpt ??= [];
        raw.TransparencyPercent = Math.Clamp(raw.TransparencyPercent, 0, 90);
        return raw;
    }

    public static string DefaultPath()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(root, "QuotaHud", "settings.json");
    }
}
