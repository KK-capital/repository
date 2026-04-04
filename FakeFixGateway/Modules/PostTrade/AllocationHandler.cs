// ============================================================
// AllocationHandler.cs
// Fake FIX Gateway - Module 12 Post-Trade Handler
// MsgType J（AllocationInstruction）→ AllocationReport（AS）
// .NET 10 / C# 13 / QuickFIX/n
// ============================================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using QuickFix;
using QuickFix.Fields;

namespace FakeFixGateway.Modules.PostTrade;

// ────────────────────────────────────────────────────────────────────────────
// 資料模型：分配指令 & 分配帳戶
// ────────────────────────────────────────────────────────────────────────────

/// <summary>
/// 分配帳戶明細（AllocationInstruction 的 NoAllocs repeating group）。
/// </summary>
public sealed record AllocationAccount
{
    /// <summary>帳戶代碼（Tag 79）</summary>
    public string AllocAccount { get; init; } = string.Empty;

    /// <summary>分配數量（Tag 80）</summary>
    public decimal AllocShares { get; init; }

    /// <summary>分配價格（Tag 153，FIX 4.4+）</summary>
    public decimal AllocPrice  { get; init; }
}

/// <summary>
/// 分配指令記錄（AllocationInstruction 的內部表示）。
/// </summary>
public sealed record AllocationRecord
{
    /// <summary>分配 ID（Tag 70）</summary>
    public string AllocID      { get; init; } = Guid.NewGuid().ToString("N")[..12].ToUpper();

    /// <summary>分配交易類型（Tag 71）：0=New, 1=Replace, 2=Cancel</summary>
    public int    AllocTransType { get; init; }

    /// <summary>商品代碼（Tag 55）</summary>
    public string Symbol        { get; init; } = string.Empty;

    /// <summary>買賣方向（Tag 54）</summary>
    public char   Side          { get; init; }

    /// <summary>成交數量（Tag 53）</summary>
    public decimal Shares       { get; init; }

    /// <summary>平均成交價（Tag 6）</summary>
    public decimal AvgPx        { get; init; }

    /// <summary>成交日期（Tag 75）</summary>
    public string TradeDate     { get; init; } = DateTime.UtcNow.ToString("yyyyMMdd");

    /// <summary>分配帳戶清單</summary>
    public List<AllocationAccount> Accounts { get; init; } = new();

    /// <summary>狀態：pending / accepted / rejected</summary>
    public string Status        { get; set; } = "pending";
}

// ────────────────────────────────────────────────────────────────────────────
// AllocationHandler：處理 J，回傳 AS
// ────────────────────────────────────────────────────────────────────────────

/// <summary>
/// <para><b>FIX MsgType J — AllocationInstruction 處理器</b></para>
/// <para>
///   Fake 邏輯：
///   <list type="number">
///     <item>解析 AllocationInstruction（MsgType=J）中的帳戶分配明細</item>
///     <item>驗證分配數量加總 ≤ 成交數量</item>
///     <item>回傳 AllocationReport（MsgType=AS）</item>
///       <list type="bullet">
///         <item>驗證通過 → AllocStatus=0（Accepted）</item>
///         <item>驗證失敗 → AllocStatus=1（Rejected）附 AllocRejCode</item>
///       </list>
///   </list>
/// </para>
/// </summary>
public sealed class AllocationHandler
{
    // ── 依賴 ──────────────────────────────────────────────────────────────────
    private readonly ILogger<AllocationHandler> _logger;

    // ── 已處理分配帳本 ────────────────────────────────────────────────────────
    private readonly ConcurrentDictionary<string, AllocationRecord> _allocations = new();

    // ── 建構子 ────────────────────────────────────────────────────────────────
    public AllocationHandler(ILogger<AllocationHandler> logger)
    {
        _logger = logger;
    }

    // ── 公開：處理 AllocationInstruction（J）────────────────────────────────
    /// <summary>
    /// 處理 AllocationInstruction（MsgType=J），建立分配記錄並回傳 AS。
    /// </summary>
    /// <param name="message">收到的 FIX J 訊息</param>
    /// <param name="sessionID">來源 Session</param>
    /// <returns>回傳的 AllocationReport（AS）訊息</returns>
    public Message OnAllocationInstruction(Message message, SessionID sessionID)
    {
        // ── 解析必填欄位 ──────────────────────────────────────────────────────
        string allocID       = message.GetField(new AllocID()).getValue();
        int    transType     = message.GetField(new AllocTransType()).getValue();
        string symbol        = message.GetField(new Symbol()).getValue();
        char   side          = message.GetField(new Side()).getValue();
        decimal shares       = message.GetField(new Shares()).getValue();
        decimal avgPx        = message.GetField(new AvgPx()).getValue();
        string tradeDate     = message.IsSetField(Tags.TradeDate)
                               ? message.GetField(new TradeDate()).getValue()
                               : DateTime.UtcNow.ToString("yyyyMMdd");

        _logger.LogInformation(
            "[AllocationHandler] 收到 J AllocID={Id} Symbol={Sym} Side={Side} Shares={Qty} AvgPx={Px}",
            allocID, symbol, side, shares, avgPx);

        // ── 解析 NoAllocs repeating group ────────────────────────────────────
        var accounts = ParseAllocationAccounts(message);

        // ── 驗證分配數量 ──────────────────────────────────────────────────────
        decimal totalAllocated = 0m;
        foreach (var acc in accounts)
            totalAllocated += acc.AllocShares;

        bool isValid   = totalAllocated <= shares;
        int  allocStatus = isValid
            ? AllocStatus.ACCEPTED
            : AllocStatus.REJECTED_BY_INTERMEDIARY;
        int? rejCode   = isValid ? null : AllocRejCode.UNKNOWN_ACCOUNT;

        string statusStr = isValid ? "accepted" : "rejected";

        // ── 更新內部帳本 ──────────────────────────────────────────────────────
        var record = new AllocationRecord
        {
            AllocID       = allocID,
            AllocTransType = transType,
            Symbol        = symbol,
            Side          = side,
            Shares        = shares,
            AvgPx         = avgPx,
            TradeDate     = tradeDate,
            Accounts      = accounts,
            Status        = statusStr
        };
        _allocations[allocID] = record;

        if (!isValid)
        {
            _logger.LogWarning(
                "[AllocationHandler] 分配驗證失敗：AllocID={Id} Allocated={Alloc} > Shares={Shares}",
                allocID, totalAllocated, shares);
        }

        // ── 建立並傳回 AllocationReport（AS）──────────────────────────────────
        var report = BuildAllocationReport(record, allocStatus, rejCode);
        Session.SendToTarget(report, sessionID);

        _logger.LogInformation(
            "[AllocationHandler] 已發送 AS AllocID={Id} Status={Status}", allocID, statusStr);

        return report;
    }

    // ── 查詢 ──────────────────────────────────────────────────────────────────
    /// <summary>取得指定 AllocID 的分配記錄。</summary>
    public AllocationRecord? GetAllocation(string allocID)
        => _allocations.TryGetValue(allocID, out var r) ? r : null;

    /// <summary>取得所有分配記錄。</summary>
    public IEnumerable<AllocationRecord> GetAll() => _allocations.Values;

    // ── 私有：解析 NoAllocs ──────────────────────────────────────────────────
    private static List<AllocationAccount> ParseAllocationAccounts(Message message)
    {
        var accounts = new List<AllocationAccount>();
        int count = 0;

        if (message.IsSetField(Tags.NoAllocs))
            count = message.GetField(new NoAllocs()).getValue();

        for (int i = 1; i <= count; i++)
        {
            var group = new QuickFix.FIX44.AllocationInstruction.NoAllocsGroup();
            message.GetGroup(i, group);

            string acct  = group.IsSetField(Tags.AllocAccount) ? group.GetField(new AllocAccount()).getValue() : $"ACCT-{i:D3}";
            decimal qty  = group.IsSetField(Tags.AllocShares)  ? group.GetField(new AllocShares()).getValue()  : 0m;
            decimal price = group.IsSetField(Tags.AllocPrice)  ? group.GetField(new AllocPrice()).getValue()   : 0m;

            accounts.Add(new AllocationAccount
            {
                AllocAccount = acct,
                AllocShares  = qty,
                AllocPrice   = price
            });
        }

        return accounts;
    }

    // ── 私有：建立 AllocationReport（AS）────────────────────────────────────
    private static Message BuildAllocationReport(
        AllocationRecord record, int allocStatus, int? rejCode)
    {
        var msg = new QuickFix.FIX44.AllocationReport();

        // Tag 755 AllocReportID：本次回報的唯一 ID
        msg.SetField(new AllocReportID(Guid.NewGuid().ToString("N")[..12].ToUpper()));
        // Tag 70 AllocID：對應原始請求
        msg.SetField(new AllocID(record.AllocID));
        // Tag 87 AllocStatus：0=Accepted, 1=Rejected
        msg.SetField(new AllocStatus(allocStatus));
        // Tag 88 AllocRejCode（僅拒絕時）
        if (rejCode.HasValue)
            msg.SetField(new AllocRejCode(rejCode.Value));

        // Tag 55 Symbol
        msg.SetField(new Symbol(record.Symbol));
        // Tag 54 Side
        msg.SetField(new Side(record.Side));
        // Tag 53 Shares（總成交數量）
        msg.SetField(new Shares(record.Shares));
        // Tag 6 AvgPx
        msg.SetField(new AvgPx(record.AvgPx));
        // Tag 75 TradeDate
        msg.SetField(new TradeDate(record.TradeDate));
        // Tag 60 TransactTime
        msg.SetField(new TransactTime(DateTime.UtcNow));

        // NoAllocs repeating group（回響分配明細）
        foreach (var acc in record.Accounts)
        {
            var group = new QuickFix.FIX44.AllocationReport.NoAllocsGroup();
            group.SetField(new AllocAccount(acc.AllocAccount));
            group.SetField(new AllocShares(acc.AllocShares));
            if (acc.AllocPrice > 0)
                group.SetField(new AllocPrice(acc.AllocPrice));
            msg.AddGroup(group);
        }

        return msg;
    }

    // ── 靜態常數：FIX 欄位值對照 ─────────────────────────────────────────────
    private static class AllocStatus
    {
        public const int ACCEPTED                  = 0;
        public const int REJECTED_BY_INTERMEDIARY  = 1;
        public const int RECEIVED                  = 3;
    }

    private static class AllocRejCode
    {
        public const int UNKNOWN_ACCOUNT           = 0;
        public const int INCORRECT_QUANTITY        = 1;
        public const int UNAUTHORIZED_TRANSACTION  = 7;
    }
}
