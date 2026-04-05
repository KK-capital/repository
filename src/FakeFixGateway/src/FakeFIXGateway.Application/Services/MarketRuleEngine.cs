// === src/FakeFIXGateway.Application/Services/MarketRuleEngine.cs ===
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FakeFIXGateway.Domain.Models;
using Microsoft.Extensions.Logging;

namespace FakeFIXGateway.Application.Services
{
    /// <summary>
    /// 市場規則引擎：依市場規則驗證委託訂單的合法性。
    /// </summary>
    public class MarketRuleEngine
    {
        private readonly ILogger<MarketRuleEngine> _logger;

        /// <summary>
        /// 初始化 <see cref="MarketRuleEngine"/>。
        /// </summary>
        /// <param name="logger">日誌記錄器</param>
        public MarketRuleEngine(ILogger<MarketRuleEngine> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// 依指定市場規則驗證訂單，回傳驗證結果與拒絕原因清單。
        /// </summary>
        /// <param name="order">待驗證的訂單</param>
        /// <param name="rule">市場規則設定</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>驗證是否通過；若失敗，rejectReasons 含原因描述</returns>
        public async Task<(bool IsValid, IList<string> RejectReasons)> ValidateOrderAsync(
            Order order,
            MarketRule rule,
            CancellationToken cancellationToken = default)
        {
            // TODO: 驗證 Qty 是否為 LotSize 整數倍
            // TODO: 驗證 Price 是否符合 TickSize
            // TODO: 驗證 Price 是否在 PriceLimit 範圍內（需參考基準價）
            throw new NotImplementedException("ValidateOrderAsync 尚未實作");
        }

        /// <summary>
        /// 依市場代碼取得對應市場規則。
        /// </summary>
        /// <param name="market">市場代碼（例如：US、HK、TW）</param>
        /// <returns>市場規則實體</returns>
        public MarketRule GetRuleByMarket(string market)
        {
            // TODO: 從設定檔或資料庫載入市場規則
            throw new NotImplementedException("GetRuleByMarket 尚未實作");
        }
    }
}
