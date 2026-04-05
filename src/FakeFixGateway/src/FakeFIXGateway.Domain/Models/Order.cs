// === src/FakeFIXGateway.Domain/Models/Order.cs ===
using System;
using FakeFIXGateway.Domain.Enums;

namespace FakeFIXGateway.Domain.Models
{
    /// <summary>
    /// 代表一筆委託訂單實體，記錄訂單的完整生命週期資訊。
    /// </summary>
    public class Order
    {
        /// <summary>唯一訂單識別碼（對應 FIX ClOrdID）</summary>
        public string OrderId { get; set; } = string.Empty;

        /// <summary>股票代碼（對應 FIX Symbol，Tag 55）</summary>
        public string Symbol { get; set; } = string.Empty;

        /// <summary>買賣方向（Buy / Sell）</summary>
        public OrderSide Side { get; set; }

        /// <summary>訂單類型（Market / Limit 等，對應 FIX OrdType，Tag 40）</summary>
        public string OrdType { get; set; } = string.Empty;

        /// <summary>委託價格（限價單使用，市價單為 0）</summary>
        public decimal Price { get; set; }

        /// <summary>委託數量（對應 FIX OrderQty，Tag 38）</summary>
        public decimal Qty { get; set; }

        /// <summary>訂單目前狀態</summary>
        public OrderStatus Status { get; set; }

        /// <summary>訂單建立時間（UTC）</summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
