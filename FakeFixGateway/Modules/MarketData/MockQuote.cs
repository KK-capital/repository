// MockQuote.cs
// 代表一個模擬報價的資料結構，包含 Bid、Ask、Last 與 Volume。
// Module 9 - Market Data Handler
// FIX Gateway Fake Implementation (.NET 10 / C#)

namespace FakeFixGateway.Modules.MarketData;

/// <summary>
/// 模擬報價資料物件，用於維護各 Symbol 的即時行情快照。
/// </summary>
public class MockQuote
{
    /// <summary>買入價</summary>
    public decimal Bid { get; set; }

    /// <summary>賣出價</summary>
    public decimal Ask { get; set; }

    /// <summary>最新成交價</summary>
    public decimal Last { get; set; }

    /// <summary>成交量</summary>
    public long Volume { get; set; }

    /// <summary>商品代碼</summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>報價時間戳記（UTC）</summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 建立一個初始報價，Bid/Ask/Last 以基準價為基礎設定。
    /// </summary>
    public static MockQuote Create(string symbol, decimal basePrice)
    {
        return new MockQuote
        {
            Symbol    = symbol,
            Bid       = basePrice - 0.01m,
            Ask       = basePrice + 0.01m,
            Last      = basePrice,
            Volume    = 0,
            Timestamp = DateTime.UtcNow
        };
    }

    /// <summary>深複製，避免外部直接修改內部狀態。</summary>
    public MockQuote Clone() => new()
    {
        Symbol    = Symbol,
        Bid       = Bid,
        Ask       = Ask,
        Last      = Last,
        Volume    = Volume,
        Timestamp = Timestamp
    };
}
