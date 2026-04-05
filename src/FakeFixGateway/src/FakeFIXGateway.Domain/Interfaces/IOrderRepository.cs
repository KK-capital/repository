// === src/FakeFIXGateway.Domain/Interfaces/IOrderRepository.cs ===
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FakeFIXGateway.Domain.Models;

namespace FakeFIXGateway.Domain.Interfaces
{
    /// <summary>
    /// 訂單儲存庫介面，定義訂單的 CRUD 操作契約。
    /// </summary>
    public interface IOrderRepository
    {
        /// <summary>
        /// 根據訂單識別碼取得單一訂單。
        /// </summary>
        /// <param name="orderId">訂單識別碼</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>訂單實體，若不存在則回傳 null</returns>
        Task<Order?> GetByIdAsync(string orderId, CancellationToken cancellationToken = default);

        /// <summary>
        /// 取得所有訂單清單。
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>訂單集合</returns>
        Task<IEnumerable<Order>> GetAllAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 新增一筆訂單。
        /// </summary>
        /// <param name="order">訂單實體</param>
        /// <param name="cancellationToken">取消令牌</param>
        Task AddAsync(Order order, CancellationToken cancellationToken = default);

        /// <summary>
        /// 更新既有訂單的狀態。
        /// </summary>
        /// <param name="order">已修改的訂單實體</param>
        /// <param name="cancellationToken">取消令牌</param>
        Task UpdateAsync(Order order, CancellationToken cancellationToken = default);

        /// <summary>
        /// 刪除指定訂單（邏輯刪除或實體刪除依實作而定）。
        /// </summary>
        /// <param name="orderId">訂單識別碼</param>
        /// <param name="cancellationToken">取消令牌</param>
        Task DeleteAsync(string orderId, CancellationToken cancellationToken = default);
    }
}
