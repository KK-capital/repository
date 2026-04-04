using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuickFix;
using QuickFix.Fields;
using QuickFix.FIX44;

namespace FakeFixGateway.Modules
{
    /// <summary>
    /// Module 10 - QuoteHandler
    /// 處理 FIX 4.4 報價相關訊息：
    ///   - QuoteRequest (MsgType=R)  → 自動回覆 Quote (MsgType=S)
    ///   - QuoteCancel  (MsgType=Z)  → 取消報價
    ///   - MassQuote    (MsgType=i)  → 大量報價，回 MassQuoteAck (MsgType=b)
    /// </summary>
    public class QuoteHandler : IApplication
    {
        // ── 注入依賴 ──────────────────────────────────────────────────────
        private readonly ILogger<QuoteHandler> _logger;
        private readonly QuoteHandlerConfig    _config;
        private readonly IInitiator            _initiator;   // 用於主動送出回應

        // ── 內部狀態 ──────────────────────────────────────────────────────
        /// <summary>儲存目前有效的報價：Key = QuoteID</summary>
        private readonly ConcurrentDictionary<string, ActiveQuote> _activeQuotes = new();

        /// <summary>模擬市場最新成交價：Key = Symbol</summary>
        private readonly ConcurrentDictionary<string, decimal> _lastPrices = new()
        {
            ["AAPL"]  = 175.00m,
            ["TSLA"]  = 245.00m,
            ["GOOGL"] = 140.00m,
            ["MSFT"]  = 415.00m,
            ["DEFAULT"] = 100.00m
        };

        // ── 建構子 ────────────────────────────────────────────────────────
        public QuoteHandler(
            ILogger<QuoteHandler> logger,
            QuoteHandlerConfig    config,
            IInitiator            initiator)
        {
            _logger    = logger    ?? throw new ArgumentNullException(nameof(logger));
            _config    = config    ?? throw new ArgumentNullException(nameof(config));
            _initiator = initiator ?? throw new ArgumentNullException(nameof(initiator));
        }

        // ═══════════════════════════════════════════════════════════════════
        // IApplication 實作
        // ═══════════════════════════════════════════════════════════════════

        public void OnCreate(SessionID sessionID) =>
            _logger.LogInformation("[QuoteHandler] Session 建立：{Session}", sessionID);

        public void OnLogon(SessionID sessionID) =>
            _logger.LogInformation("[QuoteHandler] 登入成功：{Session}", sessionID);

        public void OnLogout(SessionID sessionID) =>
            _logger.LogInformation("[QuoteHandler] 登出：{Session}", sessionID);

        public void ToAdmin(Message message, SessionID sessionID) { /* 不需攔截 */ }

        public void FromAdmin(Message message, SessionID sessionID) { /* 不需攔截 */ }

        public void ToApp(Message message, SessionID sessionID)
            => _logger.LogDebug("[QuoteHandler] 送出訊息：{MsgType}",
                message.Header.GetString(Tags.MsgType));

        /// <summary>
        /// 接收應用層訊息的入口，依 MsgType 路由至對應處理器
        /// </summary>
        public void FromApp(Message message, SessionID sessionID)
        {
            var msgType = message.Header.GetString(Tags.MsgType);
            _logger.LogInformation("[QuoteHandler] 收到訊息 MsgType={MsgType}", msgType);

            try
            {
                switch (msgType)
                {
                    case MsgType.QUOTE_REQUEST:  // R
                        HandleQuoteRequest(message, sessionID);
                        break;

                    case MsgType.QUOTE_CANCEL:   // Z
                        HandleQuoteCancel(message, sessionID);
                        break;

                    case MsgType.MASS_QUOTE:     // i
                        HandleMassQuote(message, sessionID);
                        break;

                    default:
                        _logger.LogWarning("[QuoteHandler] 不支援的 MsgType：{MsgType}", msgType);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[QuoteHandler] 處理訊息時發生錯誤 MsgType={MsgType}", msgType);
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // QuoteRequest (R) → Quote (S)
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 處理報價請求 (MsgType=R)。
        /// 讀取 QuoteReqID、Symbol，依 LastPrice ± Spread 計算雙邊報價，
        /// 回覆 Quote (MsgType=S)，並設定有效期限計時器。
        /// </summary>
        private void HandleQuoteRequest(Message request, SessionID sessionID)
        {
            var quoteReqID = request.GetString(Tags.QuoteReqID);

            // ── 解析 NoRelatedSym 群組（取第一個標的）────────────────────
            var noRelatedSym = request.GetInt(Tags.NoRelatedSym);
            if (noRelatedSym == 0)
            {
                _logger.LogWarning("[QuoteRequest] 無標的 Symbol，忽略此請求 QuoteReqID={ID}", quoteReqID);
                return;
            }

            // 逐一回覆每個 Symbol
            var symbolGroup = new QuoteRequest.NoRelatedSymGroup();
            for (int i = 1; i <= noRelatedSym; i++)
            {
                request.GetGroup(i, symbolGroup);
                var symbol = symbolGroup.GetString(Tags.Symbol);
                SendQuote(quoteReqID, symbol, sessionID);
            }
        }

        /// <summary>
        /// 建立並傳送 Quote (S) 回應，並啟動有效期限計時器
        /// </summary>
        private void SendQuote(string quoteReqID, string symbol, SessionID sessionID)
        {
            var lastPrice = GetLastPrice(symbol);
            var spread    = _config.DefaultSpread;
            var bidPx     = Math.Round(lastPrice - spread / 2m, 4);
            var askPx     = Math.Round(lastPrice + spread / 2m, 4);
            var quoteID   = GenerateQuoteId();

            var quote = new Quote(
                new QuoteID(quoteID),
                new Symbol(symbol))
            {
                // 雙邊報價
                BidPx     = new BidPx(bidPx),
                OfferPx   = new OfferPx(askPx),
                BidSize   = new BidSize(_config.DefaultQuoteSize),
                OfferSize = new OfferSize(_config.DefaultQuoteSize),

                // 參考欄位
                QuoteReqID    = new QuoteReqID(quoteReqID),
                QuoteType     = new QuoteType(QuoteType.INDICATIVE),
                TransactTime  = new TransactTime(DateTime.UtcNow),

                // 有效期限
                ValidUntilTime = new ValidUntilTime(
                    DateTime.UtcNow.Add(_config.QuoteValidityDuration))
            };

            Session.SendToTarget(quote, sessionID);

            // 儲存有效報價並設定自動到期
            var active = new ActiveQuote(quoteID, symbol, bidPx, askPx, sessionID);
            _activeQuotes[quoteID] = active;

            _logger.LogInformation(
                "[Quote] 送出報價 QuoteID={QID} Symbol={Sym} Bid={Bid} Ask={Ask} 有效期={Dur}s",
                quoteID, symbol, bidPx, askPx, _config.QuoteValidityDuration.TotalSeconds);

            // 有效期限到期後自動移除
            _ = Task.Delay(_config.QuoteValidityDuration).ContinueWith(_ =>
            {
                if (_activeQuotes.TryRemove(quoteID, out _))
                    _logger.LogInformation("[Quote] 報價已到期，自動移除 QuoteID={QID}", quoteID);
            });
        }

        // ═══════════════════════════════════════════════════════════════════
        // QuoteCancel (Z)
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 處理報價取消請求 (MsgType=Z)。
        /// 支援單一取消（QuoteID）與全部取消（QuoteCancelType=4）。
        /// </summary>
        private void HandleQuoteCancel(Message cancel, SessionID sessionID)
        {
            var quoteMsgID = cancel.IsSetField(Tags.QuoteID)
                ? cancel.GetString(Tags.QuoteID)
                : string.Empty;

            var cancelType = cancel.IsSetField(Tags.QuoteCancelType)
                ? cancel.GetInt(Tags.QuoteCancelType)
                : QuoteCancelType.CANCEL_ALL_QUOTES;

            _logger.LogInformation(
                "[QuoteCancel] 收到取消請求 QuoteID={QID} CancelType={CT}",
                quoteMsgID, cancelType);

            if (cancelType == QuoteCancelType.CANCEL_ALL_QUOTES)
            {
                // 取消所有報價
                var removed = _activeQuotes.Count;
                _activeQuotes.Clear();
                _logger.LogInformation("[QuoteCancel] 已取消全部 {Count} 筆報價", removed);
            }
            else if (!string.IsNullOrEmpty(quoteMsgID))
            {
                // 取消指定報價
                if (_activeQuotes.TryRemove(quoteMsgID, out var aq))
                    _logger.LogInformation(
                        "[QuoteCancel] 已取消報價 QuoteID={QID} Symbol={Sym}",
                        quoteMsgID, aq.Symbol);
                else
                    _logger.LogWarning(
                        "[QuoteCancel] 找不到報價 QuoteID={QID}，可能已到期", quoteMsgID);
            }

            // QuoteCancel 不需明確回覆（FIX 標準），記錄即可
        }

        // ═══════════════════════════════════════════════════════════════════
        // MassQuote (i) → MassQuoteAck (b)
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 處理大量報價請求 (MsgType=i)。
        /// 遍歷所有 Quote Set → Quote Entry，逐一儲存並回覆 MassQuoteAck (MsgType=b)。
        /// </summary>
        private void HandleMassQuote(Message massQuote, SessionID sessionID)
        {
            var quoteID = massQuote.GetString(Tags.QuoteID);
            var noQuoteSets = massQuote.GetInt(Tags.NoQuoteSets);

            _logger.LogInformation(
                "[MassQuote] 收到大量報價 QuoteID={QID} QuoteSets={Sets}",
                quoteID, noQuoteSets);

            var ackEntries = new List<(string symbol, decimal bid, decimal ask)>();

            var quoteSetGroup   = new MassQuote.NoQuoteSetsGroup();
            var quoteEntryGroup = new MassQuote.NoQuoteSetsGroup.NoQuoteEntriesGroup();

            for (int s = 1; s <= noQuoteSets; s++)
            {
                massQuote.GetGroup(s, quoteSetGroup);
                var noEntries = quoteSetGroup.GetInt(Tags.NoQuoteEntries);

                for (int e = 1; e <= noEntries; e++)
                {
                    quoteSetGroup.GetGroup(e, quoteEntryGroup);

                    var entrySymbol = quoteEntryGroup.IsSetField(Tags.Symbol)
                        ? quoteEntryGroup.GetString(Tags.Symbol)
                        : "UNKNOWN";

                    var bidPx = quoteEntryGroup.IsSetField(Tags.BidPx)
                        ? quoteEntryGroup.GetDecimal(Tags.BidPx)
                        : GetLastPrice(entrySymbol) - _config.DefaultSpread / 2m;

                    var askPx = quoteEntryGroup.IsSetField(Tags.OfferPx)
                        ? quoteEntryGroup.GetDecimal(Tags.OfferPx)
                        : GetLastPrice(entrySymbol) + _config.DefaultSpread / 2m;

                    var entryQuoteID = quoteEntryGroup.IsSetField(Tags.QuoteEntryID)
                        ? quoteEntryGroup.GetString(Tags.QuoteEntryID)
                        : GenerateQuoteId();

                    _activeQuotes[entryQuoteID] = new ActiveQuote(
                        entryQuoteID, entrySymbol, bidPx, askPx, sessionID);

                    ackEntries.Add((entrySymbol, bidPx, askPx));

                    _logger.LogDebug(
                        "[MassQuote] Entry Symbol={Sym} Bid={Bid} Ask={Ask}",
                        entrySymbol, bidPx, askPx);
                }
            }

            // 回覆 MassQuoteAck (b)
            SendMassQuoteAck(quoteID, QuoteAckStatus.ACCEPTED, sessionID);

            _logger.LogInformation(
                "[MassQuote] 已接受 {Count} 筆報價，回覆 MassQuoteAck", ackEntries.Count);
        }

        /// <summary>
        /// 發送 MassQuoteAck (MsgType=b)
        /// </summary>
        private void SendMassQuoteAck(
            string quoteID,
            int    quoteStatus,
            SessionID sessionID)
        {
            var ack = new MassQuoteAck(
                new QuoteID(quoteID),
                new QuoteStatus(quoteStatus))
            {
                TransactTime = new TransactTime(DateTime.UtcNow)
            };

            Session.SendToTarget(ack, sessionID);

            _logger.LogInformation(
                "[MassQuoteAck] 已送出 QuoteID={QID} Status={Status}",
                quoteID, quoteStatus);
        }

        // ═══════════════════════════════════════════════════════════════════
        // 輔助方法
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>取得標的最新成交價，若不存在則回傳預設值</summary>
        private decimal GetLastPrice(string symbol)
            => _lastPrices.TryGetValue(symbol, out var px) ? px
             : _lastPrices["DEFAULT"];

        /// <summary>更新標的最新成交價（供外部市場資料模組呼叫）</summary>
        public void UpdateLastPrice(string symbol, decimal price)
        {
            _lastPrices[symbol] = price;
            _logger.LogDebug("[QuoteHandler] 更新 LastPrice {Symbol}={Price}", symbol, price);
        }

        /// <summary>產生唯一報價 ID（時間戳 + 亂數）</summary>
        private static string GenerateQuoteId()
            => $"QT-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{Random.Shared.Next(1000, 9999)}";

        /// <summary>取得目前所有有效報價（供測試或監控使用）</summary>
        public IReadOnlyDictionary<string, ActiveQuote> GetActiveQuotes()
            => _activeQuotes;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 設定類別
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// QuoteHandler 可設定參數，透過 DI / appsettings.json 注入
    /// </summary>
    public class QuoteHandlerConfig
    {
        /// <summary>報價有效期限（預設 30 秒）</summary>
        public TimeSpan QuoteValidityDuration { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>預設買賣價差（預設 0.10）</summary>
        public decimal DefaultSpread { get; set; } = 0.10m;

        /// <summary>預設報價數量（預設 1000 股）</summary>
        public decimal DefaultQuoteSize { get; set; } = 1000m;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 資料模型
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>代表一筆目前有效的報價快照</summary>
    public record ActiveQuote(
        string    QuoteID,
        string    Symbol,
        decimal   BidPx,
        decimal   AskPx,
        SessionID SessionID,
        DateTime  CreatedAt = default)
    {
        public DateTime CreatedAt { get; init; } =
            CreatedAt == default ? DateTime.UtcNow : CreatedAt;
    }

    /// <summary>FIX QuoteAckStatus 常數（Tag 297）</summary>
    internal static class QuoteAckStatus
    {
        public const int ACCEPTED = 0;
        public const int CANCELED = 5;
        public const int REJECTED = 9;
    }

    /// <summary>FIX QuoteCancelType 常數（Tag 298）</summary>
    internal static class QuoteCancelType
    {
        public const int CANCEL_FOR_SYMBOL    = 1;
        public const int CANCEL_FOR_SECURITY  = 2;
        public const int CANCEL_FOR_UNDERLYING = 3;
        public const int CANCEL_ALL_QUOTES    = 4;
    }
}
