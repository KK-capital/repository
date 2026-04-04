// ============================================================
// ExecutionReportBuilder.cs
// Fake FIX Gateway - Module 5
// 支援 FIX 4.2 / 4.4 / 5.0
// 使用 QuickFIX/n（.NET 10）
// ============================================================

using System;
using QuickFix;
using QuickFix.Fields;

namespace FakeFixGateway
{
    /// <summary>
    /// ExecutionReport（FIX Tag 35=8）產生器。
    /// 支援 FIX 4.2、4.4、5.0 三種版本格式差異。
    /// 主要差異：FIX 4.2 需填 ExecTransType（Tag 20），4.4/5.0 已廢除。
    /// </summary>
    public class ExecutionReportBuilder
    {
        // ────────────────────────────────────────────────────
        // 私有欄位：訂單基本資訊
        // ────────────────────────────────────────────────────

        private readonly string _clOrdID;        // Tag 11  客戶訂單 ID
        private readonly string _orderID;        // Tag 37  交易所訂單 ID
        private readonly string _symbol;         // Tag 55  商品代碼
        private readonly char   _side;           // Tag 54  買賣方向
        private readonly double _orderQty;       // Tag 38  訂單數量
        private readonly double _price;          // Tag 44  限價
        private readonly string _fixVersion;     // FIX 版本字串，例如 "FIX.4.2"

        // 累計成交數量（跨多次 BuildPartialFill 累積）
        private double _cumQty = 0;

        // ────────────────────────────────────────────────────
        // 建構子
        // ────────────────────────────────────────────────────

        /// <summary>
        /// 初始化 ExecutionReportBuilder。
        /// </summary>
        /// <param name="clOrdID">客戶訂單 ID（Tag 11）</param>
        /// <param name="orderID">交易所訂單 ID（Tag 37）</param>
        /// <param name="symbol">商品代碼（Tag 55）</param>
        /// <param name="side">買賣方向：'1'=Buy, '2'=Sell</param>
        /// <param name="orderQty">訂單數量（Tag 38）</param>
        /// <param name="price">限價（Tag 44），市價填 0</param>
        /// <param name="fixVersion">FIX 版本字串，預設 "FIX.4.2"</param>
        public ExecutionReportBuilder(
            string clOrdID,
            string orderID,
            string symbol,
            char   side,
            double orderQty,
            double price,
            string fixVersion = "FIX.4.2")
        {
            _clOrdID    = clOrdID;
            _orderID    = orderID;
            _symbol     = symbol;
            _side       = side;
            _orderQty   = orderQty;
            _price      = price;
            _fixVersion = fixVersion;
        }

        // ────────────────────────────────────────────────────
        // 公開建構方法
        // ────────────────────────────────────────────────────

        /// <summary>
        /// 建立 New 回報（ExecType=0, OrdStatus=0）。
        /// </summary>
        public Message BuildNew()
        {
            return Build(
                execType:  ExecType.NEW,
                ordStatus: OrdStatus.NEW,
                lastQty:   0,
                lastPx:    0);
        }

        /// <summary>
        /// 建立 PendingNew 回報（ExecType=A, OrdStatus=A）。
        /// </summary>
        public Message BuildPendingNew()
        {
            return Build(
                execType:  ExecType.PENDING_NEW,
                ordStatus: OrdStatus.PENDING_NEW,
                lastQty:   0,
                lastPx:    0);
        }

        /// <summary>
        /// 建立 PartialFill 回報（ExecType=1, OrdStatus=1）。
        /// 自動累加 CumQty，計算 LeavesQty 與 AvgPx。
        /// </summary>
        /// <param name="qty">本次成交數量</param>
        /// <param name="price">本次成交價格</param>
        public Message BuildPartialFill(double qty, double price)
        {
            _cumQty += qty;
            return Build(
                execType:  ExecType.PARTIAL_FILL,
                ordStatus: OrdStatus.PARTIALLY_FILLED,
                lastQty:   qty,
                lastPx:    price);
        }

        /// <summary>
        /// 建立 Fill（完全成交）回報（ExecType=2, OrdStatus=2）。
        /// 自動累加 CumQty，LeavesQty 應為 0。
        /// </summary>
        /// <param name="qty">本次成交數量</param>
        /// <param name="price">本次成交價格</param>
        public Message BuildFill(double qty, double price)
        {
            _cumQty += qty;
            return Build(
                execType:  ExecType.FILL,
                ordStatus: OrdStatus.FILLED,
                lastQty:   qty,
                lastPx:    price);
        }

        /// <summary>
        /// 建立 Canceled 回報（ExecType=4, OrdStatus=4）。
        /// </summary>
        public Message BuildCanceled()
        {
            return Build(
                execType:  ExecType.CANCELED,
                ordStatus: OrdStatus.CANCELED,
                lastQty:   0,
                lastPx:    0);
        }

        /// <summary>
        /// 建立 Rejected 回報（ExecType=8, OrdStatus=8）。
        /// </summary>
        /// <param name="reason">拒絕原因代碼（Tag 103 OrdRejReason）</param>
        /// <param name="text">拒絕說明文字（Tag 58 Text）</param>
        public Message BuildRejected(int reason, string text)
        {
            var msg = Build(
                execType:  ExecType.REJECTED,
                ordStatus: OrdStatus.REJECTED,
                lastQty:   0,
                lastPx:    0);

            msg.SetField(new OrdRejReason(reason));
            msg.SetField(new Text(text));
            return msg;
        }

        /// <summary>
        /// 建立 Replaced（修改完成）回報（ExecType=5, OrdStatus=0）。
        /// </summary>
        public Message BuildReplaced()
        {
            return Build(
                execType:  ExecType.REPLACE,
                ordStatus: OrdStatus.NEW,
                lastQty:   0,
                lastPx:    0);
        }

        /// <summary>
        /// 建立 Expired 回報（ExecType=C, OrdStatus=C）。
        /// </summary>
        public Message BuildExpired()
        {
            return Build(
                execType:  ExecType.EXPIRED,
                ordStatus: OrdStatus.EXPIRED,
                lastQty:   0,
                lastPx:    0);
        }

        // ────────────────────────────────────────────────────
        // 私有核心建構方法
        // ────────────────────────────────────────────────────

        /// <summary>
        /// 核心建構邏輯：填入所有通用欄位，並處理 FIX 版本差異。
        /// </summary>
        private Message Build(
            char   execType,
            char   ordStatus,
            double lastQty,
            double lastPx)
        {
            // 依版本建立對應 Message 物件
            var msg = CreateMessage();

            // ── 必填欄位 ──────────────────────────────────────

            // Tag 17 ExecID：每次回報必須唯一，使用 UUID
            msg.SetField(new ExecID(Guid.NewGuid().ToString("N")));

            // Tag 11 ClOrdID
            msg.SetField(new ClOrdID(_clOrdID));

            // Tag 37 OrderID
            msg.SetField(new OrderID(_orderID));

            // Tag 55 Symbol
            msg.SetField(new Symbol(_symbol));

            // Tag 54 Side
            msg.SetField(new Side(_side));

            // Tag 38 OrderQty
            msg.SetField(new OrderQty((decimal)_orderQty));

            // Tag 150 ExecType
            msg.SetField(new ExecType(execType));

            // Tag 39 OrdStatus
            msg.SetField(new OrdStatus(ordStatus));

            // Tag 60 TransactTime：UTC 當下時間
            msg.SetField(new TransactTime(DateTime.UtcNow));

            // Tag 32 LastQty（本次成交數量）
            msg.SetField(new LastQty((decimal)lastQty));

            // Tag 31 LastPx（本次成交價格）
            msg.SetField(new LastPx((decimal)lastPx));

            // Tag 14 CumQty（累計成交數量）
            msg.SetField(new CumQty((decimal)_cumQty));

            // Tag 151 LeavesQty（剩餘數量 = 訂單量 - 累計成交量）
            double leavesQty = Math.Max(0, _orderQty - _cumQty);
            msg.SetField(new LeavesQty((decimal)leavesQty));

            // Tag 6 AvgPx（平均成交價）：
            // 簡化邏輯：若 CumQty > 0，以 lastPx 為代表；複雜場景應由呼叫端管理
            double avgPx = (_cumQty > 0 && lastPx > 0) ? lastPx : 0;
            msg.SetField(new AvgPx((decimal)avgPx));

            // ── FIX 4.2 專屬欄位 ──────────────────────────────
            // Tag 20 ExecTransType：FIX 4.2 必填，4.4/5.0 已廢除
            if (_fixVersion == "FIX.4.2")
            {
                // 0 = New（新增回報）
                msg.SetField(new ExecTransType(ExecTransType.NEW));
            }

            // ── 選填欄位 ──────────────────────────────────────

            // Tag 44 Price（若有限價）
            if (_price > 0)
            {
                msg.SetField(new Price((decimal)_price));
            }

            return msg;
        }

        /// <summary>
        /// 依 FIX 版本建立對應的 ExecutionReport Message 物件。
        /// </summary>
        private Message CreateMessage()
        {
            return _fixVersion switch
            {
                "FIX.4.2" => new QuickFix.FIX42.ExecutionReport(),
                "FIX.4.4" => new QuickFix.FIX44.ExecutionReport(),
                "FIX.5.0" => new QuickFix.FIX50.ExecutionReport(),
                _         => throw new ArgumentException(
                    $"不支援的 FIX 版本：{_fixVersion}，請使用 FIX.4.2、FIX.4.4 或 FIX.5.0")
            };
        }
    }

    // ============================================================
    // 使用範例（單元測試 / 整合測試參考）
    // ============================================================

    /// <summary>
    /// ExecutionReportBuilderExample：示範各種回報建構流程。
    /// </summary>
    public static class ExecutionReportBuilderExample
    {
        public static void RunDemo()
        {
            // ── FIX 4.2 範例 ──────────────────────────────────
            var builder42 = new ExecutionReportBuilder(
                clOrdID:    "ORD-20260404-001",
                orderID:    "EXCH-00001",
                symbol:     "2330.TW",
                side:       Side.BUY,
                orderQty:   1000,
                price:      800.0,
                fixVersion: "FIX.4.2");

            var pendingNew42 = builder42.BuildPendingNew();
            Console.WriteLine($"[FIX 4.2] PendingNew ExecID: {pendingNew42.GetField(Tags.ExecID)}");
            Console.WriteLine($"[FIX 4.2] ExecTransType: {pendingNew42.GetField(Tags.ExecTransType)}");  // 應輸出 0

            var newAck42 = builder42.BuildNew();
            Console.WriteLine($"[FIX 4.2] New ExecID: {newAck42.GetField(Tags.ExecID)}");

            var partialFill42 = builder42.BuildPartialFill(qty: 300, price: 799.5);
            Console.WriteLine($"[FIX 4.2] PartialFill CumQty: {partialFill42.GetField(Tags.CumQty)}");   // 300
            Console.WriteLine($"[FIX 4.2] PartialFill LeavesQty: {partialFill42.GetField(Tags.LeavesQty)}"); // 700

            var fill42 = builder42.BuildFill(qty: 700, price: 800.0);
            Console.WriteLine($"[FIX 4.2] Fill CumQty: {fill42.GetField(Tags.CumQty)}");      // 1000
            Console.WriteLine($"[FIX 4.2] Fill LeavesQty: {fill42.GetField(Tags.LeavesQty)}"); // 0

            // ── FIX 4.4 範例（無 ExecTransType）─────────────────
            var builder44 = new ExecutionReportBuilder(
                clOrdID:    "ORD-20260404-002",
                orderID:    "EXCH-00002",
                symbol:     "AAPL",
                side:       Side.SELL,
                orderQty:   500,
                price:      175.0,
                fixVersion: "FIX.4.4");

            var canceled44 = builder44.BuildCanceled();
            Console.WriteLine($"[FIX 4.4] Canceled ExecID: {canceled44.GetField(Tags.ExecID)}");
            Console.WriteLine($"[FIX 4.4] Has ExecTransType: {canceled44.IsSetField(Tags.ExecTransType)}"); // False

            var rejected44 = builder44.BuildRejected(
                reason: OrdRejReason.UNKNOWN_SYMBOL,
                text:   "商品代碼不存在");
            Console.WriteLine($"[FIX 4.4] Rejected Text: {rejected44.GetField(Tags.Text)}");

            // ── FIX 5.0 範例 ──────────────────────────────────
            var builder50 = new ExecutionReportBuilder(
                clOrdID:    "ORD-20260404-003",
                orderID:    "EXCH-00003",
                symbol:     "0050.TW",
                side:       Side.BUY,
                orderQty:   200,
                price:      0,          // 市價
                fixVersion: "FIX.5.0");

            var replaced50 = builder50.BuildReplaced();
            Console.WriteLine($"[FIX 5.0] Replaced ExecID: {replaced50.GetField(Tags.ExecID)}");

            var expired50 = builder50.BuildExpired();
            Console.WriteLine($"[FIX 5.0] Expired OrdStatus: {expired50.GetField(Tags.OrdStatus)}");
        }
    }
}
