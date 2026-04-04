// IncrementalRefreshBuilder.cs  （X）
// 根據 MockQuote 建立 FIX MsgType=X（MarketDataIncrementalRefresh）訊息字串。
// Module 9 - Market Data Handler
// FIX Gateway Fake Implementation (.NET 10 / C#)

using System.Text;

namespace FakeFixGateway.Modules.MarketData;

/// <summary>
/// 增量更新建構器（X）：將 MockQuote 轉換為 FIX 4.4 MarketDataIncrementalRefresh 訊息。
/// <para>
/// 每次定時推送時，由 <see cref="MarketDataGenerator"/> 呼叫此類別產生 MsgType=X 訊息，
/// 並廣播給所有已訂閱的 Session。
/// </para>
/// MDUpdateAction (tag 279)：
/// <list type="bullet">
///   <item>0 = New</item>
///   <item>1 = Change</item>
///   <item>2 = Delete</item>
/// </list>
/// </summary>
public static class IncrementalRefreshBuilder
{
    private const char SOH = '\x01';

    /// <summary>
    /// 建立 MarketDataIncrementalRefresh（MsgType=X）的 FIX 訊息字串。
    /// </summary>
    /// <param name="quote">最新報價（增量來源）</param>
    /// <param name="seqNum">訊息序號</param>
    /// <param name="updateAction">MDUpdateAction (0=New,1=Change,2=Delete)</param>
    /// <returns>完整 FIX 訊息字串</returns>
    public static string Build(MockQuote quote, int seqNum, int updateAction = 1)
    {
        // 增量更新包含 Bid / Ask / Last 三筆 MDEntry
        var entries = new (string type, decimal price, long? volume)[]
        {
            ("0", quote.Bid,  null),
            ("1", quote.Ask,  null),
            ("2", quote.Last, quote.Volume)
        };

        var body = new StringBuilder();
        body.Append($"35=X{SOH}");
        body.Append($"49=FAKEGW{SOH}");
        body.Append($"56=CLIENT{SOH}");
        body.Append($"34={seqNum}{SOH}");
        body.Append($"52={FormatUtcTime(quote.Timestamp)}{SOH}");
        body.Append($"268={entries.Length}{SOH}");                     // NoMDEntries

        foreach (var (type, price, volume) in entries)
        {
            body.Append($"279={updateAction}{SOH}");                   // MDUpdateAction
            body.Append($"269={type}{SOH}");                           // MDEntryType
            body.Append($"55={quote.Symbol}{SOH}");                    // Symbol
            body.Append($"270={price:F4}{SOH}");                       // MDEntryPx
            if (volume.HasValue)
                body.Append($"271={volume.Value}{SOH}");               // MDEntrySize
        }

        string bodyStr = body.ToString();
        int    bodyLen = Encoding.ASCII.GetByteCount(bodyStr);
        var    msg     = new StringBuilder();
        msg.Append($"8=FIX.4.4{SOH}");
        msg.Append($"9={bodyLen}{SOH}");
        msg.Append(bodyStr);

        byte checksum = ComputeChecksum(msg.ToString());
        msg.Append($"10={checksum:D3}{SOH}");

        return msg.ToString();
    }

    private static string FormatUtcTime(DateTime dt)
        => dt.ToString("yyyyMMdd-HH:mm:ss.fff");

    private static byte ComputeChecksum(string msg)
    {
        int sum = 0;
        foreach (char c in msg) sum += (byte)c;
        return (byte)(sum % 256);
    }
}
