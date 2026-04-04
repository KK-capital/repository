using System;
using System.Threading.Tasks;
using FakeFixGateway.Modules;
using Microsoft.Extensions.Logging.Abstractions;
using QuickFix;
using QuickFix.Fields;
using QuickFix.FIX44;
using Xunit;

namespace FakeFixGateway.Tests
{
    /// <summary>
    /// QuoteHandler 單元測試（xUnit）
    /// 驗證：QuoteRequest→Quote、QuoteCancel、MassQuote→MassQuoteAck
    /// </summary>
    public class QuoteHandlerTests
    {
        private readonly QuoteHandlerConfig _config = new()
        {
            QuoteValidityDuration = TimeSpan.FromSeconds(5),
            DefaultSpread         = 0.10m,
            DefaultQuoteSize      = 500m
        };

        // ── 測試 QuoteRequest (R) ──────────────────────────────────────────

        [Fact]
        public void QuoteRequest_ShouldStoreActiveQuote()
        {
            // Arrange
            var handler = BuildHandler();
            var sessionID = BuildSessionID();
            var request   = BuildQuoteRequest("REQ-001", "AAPL");

            // Act
            handler.FromApp(request, sessionID);

            // Assert：應儲存一筆有效報價
            Assert.Single(handler.GetActiveQuotes());
        }

        [Fact]
        public void QuoteRequest_BidShouldBeLowerThanAsk()
        {
            var handler   = BuildHandler();
            var sessionID = BuildSessionID();
            handler.FromApp(BuildQuoteRequest("REQ-002", "TSLA"), sessionID);

            foreach (var aq in handler.GetActiveQuotes().Values)
                Assert.True(aq.BidPx < aq.AskPx,
                    $"Bid ({aq.BidPx}) 必須小於 Ask ({aq.AskPx})");
        }

        [Fact]
        public async Task QuoteRequest_ShouldExpireAfterDuration()
        {
            var handler   = BuildHandler(); // validity = 5 秒
            var sessionID = BuildSessionID();
            handler.FromApp(BuildQuoteRequest("REQ-003", "MSFT"), sessionID);

            Assert.Single(handler.GetActiveQuotes());

            await Task.Delay(TimeSpan.FromSeconds(6)); // 等待到期

            Assert.Empty(handler.GetActiveQuotes());
        }

        // ── 測試 QuoteCancel (Z) ──────────────────────────────────────────

        [Fact]
        public void QuoteCancel_AllQuotes_ShouldClearAll()
        {
            var handler   = BuildHandler();
            var sessionID = BuildSessionID();

            // 放入兩筆報價
            handler.FromApp(BuildQuoteRequest("REQ-010", "AAPL"),  sessionID);
            handler.FromApp(BuildQuoteRequest("REQ-011", "GOOGL"), sessionID);
            Assert.Equal(2, handler.GetActiveQuotes().Count);

            // 送出全部取消
            var cancel = new QuoteCancel(
                new QuoteID("QT-ALL"),
                new QuoteCancelType(4)); // 4 = CancelAllQuotes
            handler.FromApp(cancel, sessionID);

            Assert.Empty(handler.GetActiveQuotes());
        }

        // ── 測試 MassQuote (i) ────────────────────────────────────────────

        [Fact]
        public void MassQuote_ShouldStoreMultipleQuotes()
        {
            var handler   = BuildHandler();
            var sessionID = BuildSessionID();
            var mq        = BuildMassQuote("MQ-001", new[] { "AAPL", "MSFT", "TSLA" });

            handler.FromApp(mq, sessionID);

            Assert.Equal(3, handler.GetActiveQuotes().Count);
        }

        // ══════════════════════════════════════════════════════════════════
        // 輔助建構方法
        // ══════════════════════════════════════════════════════════════════

        private QuoteHandler BuildHandler()
        {
            // MockInitiator：僅需實作介面，實際不傳送網路封包
            var initiator = new NullInitiator();
            return new QuoteHandler(
                NullLogger<QuoteHandler>.Instance,
                _config,
                initiator);
        }

        private static SessionID BuildSessionID()
            => new SessionID("FIX.4.4", "SENDER", "TARGET");

        private static QuoteRequest BuildQuoteRequest(string reqID, string symbol)
        {
            var req   = new QuoteRequest(new QuoteReqID(reqID));
            var group = new QuoteRequest.NoRelatedSymGroup();
            group.Set(new Symbol(symbol));
            req.AddGroup(group);
            return req;
        }

        private static MassQuote BuildMassQuote(string quoteID, string[] symbols)
        {
            var mq = new MassQuote(new QuoteID(quoteID));

            var setGroup = new MassQuote.NoQuoteSetsGroup();
            setGroup.Set(new QuoteSetID("SET-1"));
            setGroup.Set(new TotNoQuoteEntries(symbols.Length));

            foreach (var sym in symbols)
            {
                var entry = new MassQuote.NoQuoteSetsGroup.NoQuoteEntriesGroup();
                entry.Set(new QuoteEntryID($"QE-{sym}"));
                entry.Set(new Symbol(sym));
                setGroup.AddGroup(entry);
            }

            mq.AddGroup(setGroup);
            return mq;
        }

        /// <summary>空實作 IInitiator，用於單元測試中隔離 Session 網路層</summary>
        private sealed class NullInitiator : IInitiator
        {
            public void Start()  { }
            public void Stop()   { }
            public void Stop(bool force) { }
            public bool IsStopped => true;
            public System.Collections.Generic.HashSet<SessionID> GetSessionIDs()
                => new();
        }
    }
}
