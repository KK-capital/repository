// MarketDataGenerator.cs
// 維護所有 Symbol 的 MockQuote，並以定時器週期性推送增量更新（MsgType=X）。
// Module 9 - Market Data Handler
// FIX Gateway Fake Implementation (.NET 10 / C#)

using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace FakeFixGateway.Modules.MarketData;

/// <summary>
/// 行情資料產生器：
/// <list type="bullet">
///   <item>維護每個 Symbol 的 <see cref="MockQuote"/> 快照字典</item>
///   <item>透過 <see cref="System.Threading.Timer"/> 每 <c>IntervalMs</c> 毫秒隨機擾動報價，
///         並呼叫 <see cref="IncrementalRefreshBuilder"/> 建立 MsgType=X 訊息廣播給訂閱者</item>
///   <item>提供 REST API 呼叫用的 <see cref="SetPrice"/> 方法，可手動覆寫價格</item>
/// </list>
/// </summary>
public sealed class MarketDataGenerator : IDisposable
{
    // ── 依賴 ─────────────────────────────────────────────────────────────────
    private readonly ILogger<MarketDataGenerator> _logger;

    // ── 狀態 ─────────────────────────────────────────────────────────────────
    /// <summary>Symbol → 最新報價快照（執行緒安全字典）</summary>
    private readonly ConcurrentDictionary<string, MockQuote> _quotes = new();

    /// <summary>推送訊息的回呼，由 MarketDataHandler 設定</summary>
    public Action<string, string>? OnBroadcast { get; set; }
    //                 ↑symbol  ↑fixMessage

    /// <summary>訊息序號（原子遞增）</summary>
    private int _seqNum = 0;

    /// <summary>定時推送計時器</summary>
    private Timer? _timer;

    /// <summary>推送間隔（毫秒），預設 1000 ms</summary>
    public int IntervalMs { get; private set; } = 1000;

    private readonly Random _rng = new();
    private bool _disposed;

    // ── 建構子 ────────────────────────────────────────────────────────────────
    public MarketDataGenerator(ILogger<MarketDataGenerator> logger)
    {
        _logger = logger;
    }

    // ── 公開 API ──────────────────────────────────────────────────────────────

    /// <summary>
    /// 啟動定時推送，若已啟動則重新設定間隔。
    /// </summary>
    public void Start(int intervalMs = 1000)
    {
        IntervalMs = intervalMs;
        _timer?.Dispose();
        _timer = new Timer(Tick, null, intervalMs, intervalMs);
        _logger.LogInformation("[MarketDataGenerator] 定時推送已啟動，間隔={IntervalMs}ms", intervalMs);
    }

    /// <summary>停止定時推送。</summary>
    public void Stop()
    {
        _timer?.Change(Timeout.Infinite, Timeout.Infinite);
        _logger.LogInformation("[MarketDataGenerator] 定時推送已停止");
    }

    /// <summary>
    /// 新增或重置一個 Symbol 的報價快照。
    /// </summary>
    public void RegisterSymbol(string symbol, decimal basePrice = 100m)
    {
        _quotes[symbol] = MockQuote.Create(symbol, basePrice);
        _logger.LogInformation("[MarketDataGenerator] 已註冊 Symbol={Symbol}, BasePrice={BasePrice}", symbol, basePrice);
    }

    /// <summary>
    /// 取得指定 Symbol 的最新快照副本（供 SnapshotBuilder 使用）。
    /// </summary>
    public MockQuote? GetSnapshot(string symbol)
        => _quotes.TryGetValue(symbol, out var q) ? q.Clone() : null;

    /// <summary>
    /// 取得所有已知 Symbol 清單。
    /// </summary>
    public IEnumerable<string> GetSymbols() => _quotes.Keys;

    /// <summary>
    /// REST API 手動設定價格入口：覆寫 Bid / Ask / Last。
    /// <para>若該 Symbol 尚未存在，自動以此價格建立快照。</para>
    /// </summary>
    /// <param name="symbol">商品代碼</param>
    /// <param name="bid">買入價（null 表示不更新）</param>
    /// <param name="ask">賣出價（null 表示不更新）</param>
    /// <param name="last">最新成交價（null 表示不更新）</param>
    /// <param name="volume">成交量（null 表示不更新）</param>
    public void SetPrice(string symbol, decimal? bid, decimal? ask, decimal? last, long? volume)
    {
        var quote = _quotes.GetOrAdd(symbol, s => MockQuote.Create(s, last ?? ask ?? bid ?? 100m));

        lock (quote)
        {
            if (bid.HasValue)    quote.Bid    = bid.Value;
            if (ask.HasValue)    quote.Ask    = ask.Value;
            if (last.HasValue)   quote.Last   = last.Value;
            if (volume.HasValue) quote.Volume = volume.Value;
            quote.Timestamp = DateTime.UtcNow;
        }

        _logger.LogInformation("[MarketDataGenerator] 手動設定 {Symbol}: Bid={Bid}, Ask={Ask}, Last={Last}, Vol={Vol}",
            symbol, bid, ask, last, volume);

        // 立即廣播一次增量更新
        BroadcastIncremental(quote.Clone());
    }

    // ── 私有：定時回呼 ────────────────────────────────────────────────────────

    /// <summary>定時器回呼：隨機擾動所有 Symbol 報價並廣播增量更新。</summary>
    private void Tick(object? _)
    {
        foreach (var (symbol, quote) in _quotes)
        {
            MockQuote snapshot;
            lock (quote)
            {
                // 以 ±0.05% 隨機擾動模擬行情跳動
                double drift = 1.0 + (_rng.NextDouble() - 0.5) * 0.001;
                quote.Last    = Math.Round(quote.Last * (decimal)drift, 4);
                quote.Bid     = quote.Last - 0.01m;
                quote.Ask     = quote.Last + 0.01m;
                quote.Volume += _rng.Next(1, 100);
                quote.Timestamp = DateTime.UtcNow;
                snapshot = quote.Clone();
            }
            BroadcastIncremental(snapshot);
        }
    }

    /// <summary>建立 MsgType=X 並透過回呼廣播給 Handler。</summary>
    private void BroadcastIncremental(MockQuote snapshot)
    {
        int seq     = Interlocked.Increment(ref _seqNum);
        string fixMsg = IncrementalRefreshBuilder.Build(snapshot, seq);
        OnBroadcast?.Invoke(snapshot.Symbol, fixMsg);
    }

    // ── IDisposable ───────────────────────────────────────────────────────────
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer?.Dispose();
        _logger.LogInformation("[MarketDataGenerator] 已釋放資源");
    }
}
