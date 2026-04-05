// === src/FakeFIXGateway.Domain/Enums/OrderSide.cs ===
namespace FakeFIXGateway.Domain.Enums
{
    /// <summary>
    /// 買賣方向列舉，對應 FIX Side（Tag 54）。
    /// </summary>
    public enum OrderSide
    {
        /// <summary>買入（FIX '1'）</summary>
        Buy,

        /// <summary>賣出（FIX '2'）</summary>
        Sell
    }
}
