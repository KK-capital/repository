// === src/FakeFIXGateway.Application/Services/FillScheduler.cs ===
using System;
using System.Threading;
using System.Threading.Tasks;
using FakeFIXGateway.Domain.Models;
using Microsoft.Extensions.Logging;

namespace FakeFIXGateway.Application.Services
{
    /// <summary>
    /// 成交排程器：模擬不同成交模式（立即、延遲、部分、手動）。
    /// </summary>
    public class FillScheduler
    {
        private readonly ILogger<FillScheduler> _logger;

        /// <summary>
        /// 初始化 <see cref="FillScheduler"/>。
        /// </summary>
        /// <param name="logger">日誌記錄器</param>
        public FillScheduler(ILogger<FillScheduler> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// 立即成交模式：收到委託後立刻產生全數成交。
        /// </summary>
        /// <param name="order">目標訂單</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async Task ScheduleImmediateFillAsync(Order order, CancellationToken cancellationToken = default)
        {
            // TODO: 立即呼叫 ExecutionReportBuilder 產生 Filled 回報
            throw new NotImplementedException("ScheduleImmediateFillAsync 尚未實作");
        }

        /// <summary>
        /// 延遲成交模式：等待指定毫秒後再產生成交回報。
        /// </summary>
        /// <param name="order">目標訂單</param>
        /// <param name="delayMs">延遲毫秒數</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async Task ScheduleDelayedFillAsync(Order order, int delayMs, CancellationToken cancellationToken = default)
        {
            // TODO: 使用 Task.Delay 等待後成交
            throw new NotImplementedException("ScheduleDelayedFillAsync 尚未實作");
        }

        /// <summary>
        /// 部分成交模式：分多次傳送 PartiallyFilled + Filled 回報。
        /// </summary>
        /// <param name="order">目標訂單</param>
        /// <param name="fillCount">拆分成交次數</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async Task SchedulePartialFillAsync(Order order, int fillCount, CancellationToken cancellationToken = default)
        {
            // TODO: 依 fillCount 分割數量，依序送出部分成交回報
            throw new NotImplementedException("SchedulePartialFillAsync 尚未實作");
        }

        /// <summary>
        /// 手動成交模式：等待外部 API 觸發才產生成交（用於測試）。
        /// </summary>
        /// <param name="order">目標訂單</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async Task RegisterManualFillAsync(Order order, CancellationToken cancellationToken = default)
        {
            // TODO: 將訂單加入待手動成交佇列
            throw new NotImplementedException("RegisterManualFillAsync 尚未實作");
        }

        /// <summary>
        /// 由 AdminController 呼叫，手動觸發特定訂單成交。
        /// </summary>
        /// <param name="orderId">訂單識別碼</param>
        /// <param name="cancellationToken">取消令牌</param>
        public async Task TriggerManualFillAsync(string orderId, CancellationToken cancellationToken = default)
        {
            // TODO: 從待手動成交佇列找到訂單，執行成交
            throw new NotImplementedException("TriggerManualFillAsync 尚未實作");
        }
    }
}
