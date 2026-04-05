// === src/FakeFIXGateway.Domain/Models/MarketRule.cs ===
namespace FakeFIXGateway.Domain.Models
{
    /// <summary>
    /// 代表特定市場（如 US、HK、JP、TW）的交易規則設定。
    /// </summary>
    public class MarketRule
    {
        /// <summary>市場代碼（例如：US、HK、JP、TW）</summary>
        public string Market { get; set; } = string.Empty;

        /// <summary>最小交易單位（張數 / 股數）</summary>
        public decimal LotSize { get; set; }

        /// <summary>
        /// 漲跌幅限制（百分比，例如 0.10 代表 ±10%）。
        /// 0 表示無限制（如美股）。
        /// </summary>
        public decimal PriceLimit { get; set; }

        /// <summary>最小報價單位（Tick Size，例如 0.01）</summary>
        public decimal TickSize { get; set; }
    }
}
