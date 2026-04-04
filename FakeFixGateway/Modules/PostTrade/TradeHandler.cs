// ============================================================
// TradeHandler.cs
// Fake FIX Gateway - Module 12 Post-Trade Handler
// MsgType AD（TradeCaptureReportRequest）→ 歷史成交紀錄查詢
// .NET 10 / C# 13 / QuickFIX/n
// ============================================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using QuickFix;
using QuickFix.Fields;

namespace FakeFixGateway.Modules.PostTrade;

// ────────────────────────────────────────────────────────────────────────────
// 資料模型：成交紀錄
// ────────────────────────────────────────────────────────────────────────────

/// <summary>
/// 單筆成交紀錄（Trade Capture Report 的內部表示）。
/// </summary>
public sealed record TradeRecord
{
    /// <summary>成交回報 ID（Tag 571）</summary>
    public string TradeReportID { get; init; } = Guid.NewGuid().ToString("N")[..12].ToUpper();

    /// <summary>執行 ID（Tag 17）</summary>
    public string ExecID       { get; init; } = string.Empty;

    /// <summary>商品代碼（Tag 55）</summary>
    public string Symbol       { get; init; } = string.Empty;

    /// <summary>買賣方向（Tag 54）：'1'=Buy, '2'=Sell</summary>
    public char Side           { get; init; }

    /// <summary>成交數量（Tag 32）</summary>
    public decimal LastQty     { get; init; }

    /// <summary>成交價格（Tag 31）</summary>
    public decimal LastPx      { get; init; }

    /// <summary>成交時間（Tag 60）</summary>
    public DateTime TransactTime { get; init; } = DateTime.UtcNow;

    /// <summary>客戶訂單 ID（Tag 11）</summary>
    public string ClOrdID      { get; init; } = string.Empty;

    /// <summary>交易所訂單 ID（Tag 37）</summary>
    public string OrderID      { get; init; } = string.Empty;

    /// <summary>對手方 ID（Tag 76）</summary>
    public string ExecBroker   { get; init; } = "FAKE-BROKER";
}

// ────────────────────────────────────────────────────────────────────────────
// TradeHandler：處理 AD 請求，回傳 AE（TradeCaptureReport）
// ────────────────────────────────────────────────────────────────────────────

/// <summary>
/// <para><b>FIX MsgType AD — TradeCaptureReportRequest 處理器</b></para>
/// <para>
///   收到 AD 訊息後，依 TradeRequestType（Tag 569）邏輯：
///   <list type="bullet">
///     <item><c>0 = AllTrades</c>：傳回所有歷史成交</item>
///     <item><c>1 = MatchedTrades</c>：依 Symbol 過濾</item>
///     <item><c>2 = UnmatchedTrades</c>：模擬未完成成交（Fake 場景，回傳空集）</item>
///   </list>
/// </para>
/// <para>
///   回應格式：先發送 N 筆 AE（TradeCaptureReport），最後以
///   TradeCaptureReportRequestAck（MsgType=AQ）告知總數。
/// </para>
/// </summary>
public sealed class TradeHandler
{
    // ── 依賴 ──────────────────────────────────────────────────────────────────
    private readonly ILogger<TradeHandler>     _logger;
    private readonly ISession?                 _session;     // QuickFIX Session（可注入 Mock）

    // ── 內部成交帳本 ─────────────────────────────────────────────────────────
    /// <summary>所有歷史成交（執行緒安全），Key = TradeReportID</summary>
    private readonly ConcurrentDictionary<string, TradeRecord> _trades = new();

    // ── 建構子 ────────────────────────────────────────────────────────────────
    public TradeHandler(ILogger<TradeHandler> logger, ISession? session = null)
    {
        _logger  = logger;
        _session = session;
        SeedFakeTrades();   // 預置測試資料
    }

    // ── 公開：新增成交紀錄（由 ExecutionReport 處理器呼叫）─────────────────
    /// <summary>
    /// 將一筆新成交加入內部帳本。
    /// 通常由 Module 5 ExecutionReport 處理器在 Fill/PartialFill 時呼叫。
    /// </summary>
    public void AddTrade(TradeRecord trade)
    {
        _trades[trade.TradeReportID] = trade;
        _logger.LogInformation(
            "[TradeHandler] 新增成交 TradeReportID={Id} Symbol={Symbol} Qty={Qty} Px={Px}",
            trade.TradeReportID, trade.Symbol, trade.LastQty, trade.LastPx);
    }

    // ── 公開：處理 AD 請求 ───────────────────────────────────────────────────
    /// <summary>
    /// 處理 TradeCaptureReportRequest（MsgType=AD）。
    /// </summary>
    /// <param name="message">收到的 FIX AD 訊息</param>
    /// <param name="sessionID">來源 Session</param>
    public void OnTradeCaptureReportRequest(Message message, SessionID sessionID)
    {
        // 讀取必填欄位
        string tradeRequestID = message.GetField(new TradeRequestID()).getValue();
        int    requestType    = message.GetField(new TradeRequestType()).getValue();

        // 選填：Symbol 過濾
        string? symbol = null;
        if (message.IsSetField(Tags.Symbol))
            symbol = message.GetField(new Symbol()).getValue();

        _logger.LogInformation(
            "[TradeHandler] 收到 AD TradeRequestID={Req} RequestType={Type} Symbol={Sym}",
            tradeRequestID, requestType, symbol ?? "(all)");

        // 依 TradeRequestType 篩選成交紀錄
        IEnumerable<TradeRecord> matched = requestType switch
        {
            TradeRequestType.ALL_TRADES      => _trades.Values,
            TradeRequestType.MATCHED_TRADES  => symbol != null
                                                ? _trades.Values.Where(t => t.Symbol == symbol)
                                                : _trades.Values,
            TradeRequestType.UNMATCHED_TRADES => Enumerable.Empty<TradeRecord>(), // Fake：無未配對
            _ => Enumerable.Empty<TradeRecord>()
        };

        var list = matched.OrderBy(t => t.TransactTime).ToList();

        // 回傳每筆 TradeCaptureReport（MsgType=AE）
        int seqNum = 0;
        foreach (var trade in list)
        {
            seqNum++;
            var ae = BuildTradeCaptureReport(tradeRequestID, trade, seqNum, list.Count);
            SendToSession(ae, sessionID);
        }

        // 最後回傳 TradeCaptureReportRequestAck（MsgType=AQ）
        var ack = BuildRequestAck(tradeRequestID, requestType, list.Count);
        SendToSession(ack, sessionID);

        _logger.LogInformation(
            "[TradeHandler] AD 處理完畢，共回傳 {Count} 筆 AE + 1 筆 AQ", list.Count);
    }

    // ── 私有：建立 TradeCaptureReport（AE）────────────────────────────────────
    private static Message BuildTradeCaptureReport(
        string tradeRequestID, TradeRecord trade, int reportSeq, int total)
    {
        var msg = new QuickFix.FIX44.TradeCaptureReport();

        // Tag 571 TradeReportID
        msg.SetField(new TradeReportID(trade.TradeReportID));
        // Tag 568 TradeRequestID（回響請求 ID）
        msg.SetField(new TradeRequestID(tradeRequestID));
        // Tag 912 LastRptRequested：是否最後一筆
        msg.SetField(new LastRptRequested(reportSeq == total));
        // Tag 55 Symbol
        msg.SetField(new Symbol(trade.Symbol));
        // Tag 32 LastQty
        msg.SetField(new LastQty(trade.LastQty));
        // Tag 31 LastPx
        msg.SetField(new LastPx(trade.LastPx));
        // Tag 60 TransactTime
        msg.SetField(new TransactTime(trade.TransactTime));
        // Tag 17 ExecID
        msg.SetField(new ExecID(trade.ExecID));

        // NoSides repeating group（每筆成交含買賣雙方資訊）
        var sideGroup = new QuickFix.FIX44.TradeCaptureReport.NoSidesGroup();
        sideGroup.SetField(new Side(trade.Side));
        sideGroup.SetField(new OrderID(trade.OrderID));
        sideGroup.SetField(new ClOrdID(trade.ClOrdID));
        sideGroup.SetField(new TradeDate(trade.TransactTime.ToString("yyyyMMdd")));
        msg.AddGroup(sideGroup);

        return msg;
    }

    // ── 私有：建立 TradeCaptureReportRequestAck（AQ）──────────────────────────
    private static Message BuildRequestAck(string tradeRequestID, int requestType, int totalTrades)
    {
        var msg = new QuickFix.FIX44.TradeCaptureReportRequestAck();

        msg.SetField(new TradeRequestID(tradeRequestID));
        msg.SetField(new TradeRequestType(requestType));
        // Tag 749 TradeRequestResult：0=Successful
        msg.SetField(new TradeRequestResult(TradeRequestResult.SUCCESSFUL));
        // Tag 750 TradeRequestStatus：0=Completed
        msg.SetField(new TradeRequestStatus(TradeRequestStatus.COMPLETED));
        // Tag 748 TotNumTradeReports
        msg.SetField(new TotNumTradeReports(totalTrades));

        return msg;
    }

    // ── 私有：送出 FIX 訊息 ──────────────────────────────────────────────────
    private void SendToSession(Message msg, SessionID sessionID)
    {
        if (_session != null)
            _session.Send(msg);
        else
            Session.SendToTarget(msg, sessionID);
    }

    // ── 私有：預置 Fake 測試資料 ─────────────────────────────────────────────
    /// <summary>
    /// 預置幾筆成交紀錄供測試查詢使用。
    /// </summary>
    private void SeedFakeTrades()
    {
        var seeds = new[]
        {
            new TradeRecord
            {
                ExecID      = "EXEC-00001",
                Symbol      = "2330.TW",
                Side        = QuickFix.Fields.Side.BUY,
                LastQty     = 1000,
                LastPx      = 800.0m,
                TransactTime = DateTime.UtcNow.AddHours(-3),
                ClOrdID     = "ORD-001",
                OrderID     = "EXCH-001"
            },
            new TradeRecord
            {
                ExecID      = "EXEC-00002",
                Symbol      = "2330.TW",
                Side        = QuickFix.Fields.Side.SELL,
                LastQty     = 500,
                LastPx      = 805.0m,
                TransactTime = DateTime.UtcNow.AddHours(-2),
                ClOrdID     = "ORD-002",
                OrderID     = "EXCH-002"
            },
            new TradeRecord
            {
                ExecID      = "EXEC-00003",
                Symbol      = "0050.TW",
                Side        = QuickFix.Fields.Side.BUY,
                LastQty     = 2000,
                LastPx      = 150.5m,
                TransactTime = DateTime.UtcNow.AddHours(-1),
                ClOrdID     = "ORD-003",
                OrderID     = "EXCH-003"
            }
        };

        foreach (var t in seeds)
            _trades[t.TradeReportID] = t;

        _logger.LogInformation("[TradeHandler] 已預置 {Count} 筆 Fake 成交紀錄", seeds.Length);
    }
}
