using System.Text.Json.Nodes;

namespace QuotaHud;

public sealed class QuotaSession
{
    readonly VisibilityStore _visibility;
    readonly Func<string, LoadedSource> _loadSource;
    readonly Func<Account, Task<JsonNode>> _fetchGrok;
    readonly Func<Account, Task<JsonNode>> _fetchChatgpt;
    readonly Action<HudState>? _onChange;
    readonly BackoffTracker _backoff = new();
    LoadedSource _snapshot = new();
    int _generation;
    Task<RefreshResult>? _inflight;
    bool _loadedOnce;
    DateTime? _lastAutoPollAt;

    public QuotaSession(
        VisibilityStore visibility,
        Func<string, LoadedSource> loadSource,
        Func<Account, Task<JsonNode>> fetchGrok,
        Func<Account, Task<JsonNode>> fetchChatgpt,
        Action<HudState>? onChange = null)
    {
        _visibility = visibility;
        _loadSource = loadSource;
        _fetchGrok = fetchGrok;
        _fetchChatgpt = fetchChatgpt;
        _onChange = onChange;
        _snapshot.Source = visibility.GetSource();
    }

    public bool LoadedOnce => _loadedOnce;
    public bool Busy => _inflight is not null;
    public DateTime? LastAutoPollAt => _lastAutoPollAt;

    public HudState CurrentState(string? phase = null) => DashboardState.Build(
        _snapshot.Source,
        phase ?? (_loadedOnce ? "ready" : "loading"),
        _loadedOnce,
        ErrorClassifier.SideErrorMessage(_snapshot.Grok.Error),
        ErrorClassifier.SideErrorMessage(_snapshot.Chatgpt.Error),
        _snapshot.GrokResults,
        _snapshot.ChatgptResults,
        _visibility.IsVisible);

    public Task<RefreshResult> RefreshNow(bool manual = false)
    {
        var myGen = ++_generation;
        if (!manual) _lastAutoPollAt = DateTime.Now;
        var work = Run(myGen, manual);
        _inflight = work;
        return Await(work);
    }

    public Task<RefreshResult> SetSource(string source)
    {
        _generation++;
        _visibility.SetSource(source);
        _loadedOnce = false;
        _snapshot = new LoadedSource { Source = source == QuotaPaths.Terminal ? QuotaPaths.Terminal : QuotaPaths.Hermes };
        Notify();
        return RefreshNow(manual: true);
    }

    async Task<RefreshResult> Await(Task<RefreshResult> work)
    {
        try
        {
            return await work;
        }
        finally
        {
            if (ReferenceEquals(_inflight, work)) _inflight = null;
        }
    }

    async Task<RefreshResult> Run(int myGen, bool manual)
    {
        var source = _visibility.GetSource();
        var loaded = _loadSource(source);
        loaded = MaybeFallback(source, loaded, out source);
        if (myGen != _generation) return new RefreshResult { Discarded = true, State = CurrentState() };
        ApplyLoaded(source, loaded);
        var grokAccounts = _snapshot.Grok.Accounts.ToList();
        var chatgptAccounts = _snapshot.Chatgpt.Accounts.ToList();
        if (grokAccounts.Count + chatgptAccounts.Count == 0)
        {
            _snapshot.GrokResults = [];
            _snapshot.ChatgptResults = [];
            _loadedOnce = true;
            Notify();
            return new RefreshResult { State = CurrentState() };
        }

        if (!manual) Notify();
        var grokToFetch = grokAccounts.Where(a => manual || !_backoff.ShouldSkip(a.Id)).ToList();
        var chatgptToFetch = chatgptAccounts.Where(a => manual || !_backoff.ShouldSkip(a.Id)).ToList();
        var polled = await Poller.PollAccounts(grokToFetch, chatgptToFetch, _fetchGrok, _fetchChatgpt);
        if (myGen != _generation) return new RefreshResult { Discarded = true, State = CurrentState() };
        foreach (var result in polled.Grok.Concat(polled.Chatgpt))
        {
            if (result.Ok) _backoff.RecordSuccess(result.Account.Id);
            else _backoff.RecordFailure(result.Account.Id);
        }

        _snapshot.GrokResults = Merge(grokAccounts, polled.Grok);
        _snapshot.ChatgptResults = Merge(chatgptAccounts, polled.Chatgpt);
        _loadedOnce = true;
        if (manual || RefreshWindow.IsAutoRefreshAllowed(DateTime.Now)) _lastAutoPollAt = DateTime.Now;
        Notify();
        return new RefreshResult { State = CurrentState() };
    }

    LoadedSource MaybeFallback(string source, LoadedSource loaded, out string nextSource)
    {
        nextSource = source;
        if (!BothMissing(loaded)) return loaded;
        var other = source == QuotaPaths.Hermes ? QuotaPaths.Terminal : QuotaPaths.Hermes;
        var otherLoaded = _loadSource(other);
        if (BothMissing(otherLoaded)) return loaded;
        _visibility.SetSource(other);
        nextSource = other;
        return otherLoaded;
    }

    static bool BothMissing(LoadedSource loaded) =>
        loaded.Grok.Accounts.Count == 0
        && loaded.Chatgpt.Accounts.Count == 0
        && loaded.Grok.Error?.Code == "missingFile"
        && loaded.Chatgpt.Error?.Code == "missingFile";

    void ApplyLoaded(string source, LoadedSource loaded)
    {
        _snapshot.Source = source;
        _snapshot.FilesRead = loaded.FilesRead;
        _snapshot.Grok = loaded.Grok;
        _snapshot.Chatgpt = loaded.Chatgpt;
    }

    static List<AccountResult> Merge(List<Account> accounts, List<AccountResult> fetched)
    {
        var byId = fetched.ToDictionary(x => x.Account.Id);
        return accounts.Select(account =>
        {
            if (byId.TryGetValue(account.Id, out var result)) return result;
            return new AccountResult
            {
                Account = account,
                Ok = false,
                Error = new QuotaError("network", ErrorMessages.Retry),
            };
        }).ToList();
    }

    void Notify() => _onChange?.Invoke(CurrentState());
}
