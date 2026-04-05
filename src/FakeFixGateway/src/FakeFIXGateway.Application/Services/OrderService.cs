// === src/FakeFIXGateway.Application/Services/OrderService.cs ===
using System;
using System.Threading;
using System.Threading.Tasks;
using FakeFIXGateway.Domain.Interfaces;
using FakeFIXGateway.Domain.Models;
using Microsoft.Extensions.Logging;

namespace FakeFIXGateway.Application.Services
{
    /// <summary>
    /// 訂單服務：負責委託新增、取消、查詢等核心業務邏輯。
    /// </summary>
    public class OrderService
    {
        private readonly IOrderRepository _orderRepository;
        private readonly ILogger<OrderService> _logger;

        /// <summary>
        /// 初始化 <see cref="OrderService"/>，透過 DI 注入依賴。
        /// </summary>
        /// <param name="orderRepository">訂單儲存庫</param>
        /// <param name="logger">日誌記錄器</param>
        public OrderService(IOrderRepository orderRepository, ILogger<OrderService> logger)
        {
            _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// 送出新委託訂單，驗證後儲存並觸發後續成交流程。
        /// </summary>
        /// <param name="order">委託訂單實體</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>建立後的訂單實體</returns>
        public async Task<Order> PlaceOrderAsync(Order order, CancellationToken cancellationToken = default)
        {
            // TODO: 驗證訂單欄位（Symbol、Qty、Price）
            // TODO: 呼叫 MarketRuleEngine.ValidateOrder
            // TODO: 儲存訂單至 Repository
            // TODO: 發布 OrderPlaced 事件給 FillScheduler
            throw new NotImplementedException("PlaceOrderAsync 尚未實作");
        }

        /// <summary>
        /// 取消既有委託訂單。
        /// </summary>
        /// <param name="orderId">要取消的訂單識別碼</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>取消後的訂單實體</returns>
        public async Task<Order> CancelOrderAsync(string orderId, CancellationToken cancellationToken = default)
        {
            // TODO: 確認訂單存在且狀態允許取消
            // TODO: 更新狀態為 Cancelled
            // TODO: 發送 ExecutionReport(OrdStatus=Cancelled) 回 FIX Client
            throw new NotImplementedException("CancelOrderAsync 尚未實作");
        }

        /// <summary>
        /// 查詢單一訂單詳細資訊。
        /// </summary>
        /// <param name="orderId">訂單識別碼</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>訂單實體，若不存在則拋出例外</returns>
        public async Task<Order> GetOrderAsync(string orderId, CancellationToken cancellationToken = default)
        {
            // TODO: 從 Repository 取得訂單
            // TODO: 若不存在拋出 OrderNotFoundException
            throw new NotImplementedException("GetOrderAsync 尚未實作");
        }
    }
}
