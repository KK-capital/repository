// === src/FakeFIXGateway.Domain/Models/Fill.cs ===
using System;

namespace FakeFIXGateway.Domain.Models
{
    /// <summary>
    /// 代表一筆成交記錄，記錄部分或全部成交的詳細資訊。
    /// </summary>
    public class Fill
    {
        /// <summary>唯一成交識別碼（對應 FIX ExecID，Tag 17）</summary>
        public string FillId { get; set; } = string.Empty;

        /// <summary>關聯的訂單識別碼（對應 FIX ClOrdID，Tag 11）</summary>
        public string OrderId { get; set; } = string.Empty;

        /// <summary>本次成交數量（對應 FIX LastQty，Tag 32）</summary>
        public decimal FillQty { get; set; }

        /// <summary>本次成交價格（對應 FIX LastPx，Tag 31）</summary>
        public decimal FillPx { get; set; }

        /// <summary>成交時間（UTC，對應 FIX TransactTime，Tag 60）</summary>
        public DateTime FillTime { get; set; } = DateTime.UtcNow;
    }
}
