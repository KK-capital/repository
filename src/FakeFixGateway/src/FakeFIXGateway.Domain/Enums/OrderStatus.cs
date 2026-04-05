// === src/FakeFIXGateway.Domain/Enums/OrderStatus.cs ===
namespace FakeFIXGateway.Domain.Enums
{
    /// <summary>
    /// 訂單狀態列舉，對應 FIX OrdStatus（Tag 39）。
    /// </summary>
    public enum OrderStatus
    {
        /// <summary>新委託（FIX '0'）</summary>
        New,

        /// <summary>部分成交（FIX '1'）</summary>
        PartiallyFilled,

        /// <summary>全部成交（FIX '2'）</summary>
        Filled,

        /// <summary>已取消（FIX '4'）</summary>
        Cancelled,

        /// <summary>已拒絕（FIX '8'）</summary>
        Rejected
    }
}
