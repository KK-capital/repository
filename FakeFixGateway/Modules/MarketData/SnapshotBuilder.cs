// SnapshotBuilder.cs  （W）
// 根據 MockQuote 建立 FIX MsgType=W（MarketDataSnapshotFullRefresh）訊息字串。
// Module 9 - Market Data Handler
// FIX Gateway Fake Implementation (.NET 10 / C#)

using System.Text;

namespace FakeFixGateway.Modules.MarketData;

/// <summary>
/// 快照建構器（W）：將 MockQuote 轉換為 FIX 4.4 MarketDataSnapshotFullRefresh 訊息。
/// <para>
/// 產出的 FIX 訊息包含：
/// <list type="bullet">
///   <item>MDEntry: BID (tag 269=0)</item>
///   <item>MDEntry: OFFER/ASK (tag 269=1)</item>
///   <item>MDEntry: TRADE/LAST (tag 269=2)</item>
/// </list>
/// </para>
/// </summary>
public static class SnapshotBuilder
{
    private const char SOH = '\x01'; // FIX 欄位分隔符號

    /// <summary>
    /// 建立 MarketDataSnapshotFullRefresh（MsgType=W）的 FIX 訊息字串。
    /// </summary>
    /// <param name="quote">來源報價快照</param>
    /// <param name="mdReqId">對應的行情請求 ID（tag 262）</param>
    /// <param name="seqNum">訊息序號（tag 34）</param>
    /// <returns>完整 FIX 訊息字串（含 SOH 分隔）</returns>
    public static string Build(MockQuote quote, string mdReqId, int seqNum)
    {
        // MDEntry 共 3 筆：Bid / Ask / Last
        var entries = new (string type, decimal price, long? volume)[]
        {
            ("0", quote.Bid,  null),        // Bid
            ("1", quote.Ask,  null),        // Offer / Ask
            ("2", quote.Last, quote.Volume) // Trade / Last
        };

        var body = new StringBuilder();

        // --- Body 欄位 ---
        body.Append($"35=W{SOH}");                                                  // MsgType
        body.Append($"49=FAKEGW{SOH}");                                             // SenderCompID
        body.Append($"56=CLIENT{SOH}");                                             // TargetCompID
        body.Append($"34={seqNum}{SOH}");                                           // MsgSeqNum
        body.Append($"52={FormatUtcTime(quote.Timestamp)}{SOH}");                   // SendingTime
        body.Append($"262={mdReqId}{SOH}");                                         // MDReqID
        body.Append($"55={quote.Symbol}{SOH}");                                     // Symbol
        body.Append($"268={entries.Length}{SOH}");                                  // NoMDEntries

        foreach (var (type, price, volume) in entries)
        {
            body.Append($"269={type}{SOH}");                                        // MDEntryType
            body.Append($"270={price:F4}{SOH}");                                    // MDEntryPx
            if (volume.HasValue)
                body.Append($"271={volume.Value}{SOH}");                            // MDEntrySize
        }

        // --- Header + BodyLength ---
        string bodyStr  = body.ToString();
        int    bodyLen  = Encoding.ASCII.GetByteCount(bodyStr);
        var    msg      = new StringBuilder();
        msg.Append($"8=FIX.4.4{SOH}");
        msg.Append($"9={bodyLen}{SOH}");
        msg.Append(bodyStr);

        // --- Checksum ---
        byte checksum = ComputeChecksum(msg.ToString());
        msg.Append($"10={checksum:D3}{SOH}");

        return msg.ToString();
    }

    // --- 私有輔助方法 ---

    private static string FormatUtcTime(DateTime dt)
        => dt.ToString("yyyyMMdd-HH:mm:ss.fff");

    private static byte ComputeChecksum(string msg)
    {
        int sum = 0;
        foreach (char c in msg) sum += (byte)c;
        return (byte)(sum % 256);
    }
}
