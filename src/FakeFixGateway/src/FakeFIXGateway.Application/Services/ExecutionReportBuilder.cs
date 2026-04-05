// === src/FakeFIXGateway.Application/Services/ExecutionReportBuilder.cs ===
using System;
using FakeFIXGateway.Domain.Models;
using QuickFix;
using QuickFix.Fields;
using Microsoft.Extensions.Logging;

namespace FakeFIXGateway.Application.Services
{
    /// <summary>
    /// 執行回報建構器：依 FIX 版本（4.2 / 4.4 / 5.0）建構 ExecutionReport（35=8）訊息。
    /// </summary>
    public class ExecutionReportBuilder
    {
        private readonly ILogger<ExecutionReportBuilder> _logger;

        /// <summary>
        /// 初始化 <see cref="ExecutionReportBuilder"/>。
        /// </summary>
        /// <param name="logger">日誌記錄器</param>
        public ExecutionReportBuilder(ILogger<ExecutionReportBuilder> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// 依訂單與成交資訊建構 FIX ExecutionReport 訊息。
        /// </summary>
        /// <param name="order">對應訂單</param>
        /// <param name="fill">成交資訊（若為拒絕或取消可為 null）</param>
        /// <param name="fixVersion">FIX 協議版本（例如："FIX.4.2"）</param>
        /// <returns>可傳送的 FIX Message 物件</returns>
        public Message BuildReport(Order order, Fill? fill, string fixVersion)
        {
            // TODO: 依 fixVersion 選擇 QuickFix.FIX42 / FIX44 / FIXT11 命名空間
            // TODO: 填入 ExecID(17)、OrdStatus(39)、ExecType(150)
            // TODO: 填入 Symbol(55)、Side(54)、OrderQty(38)、CumQty(14)、AvgPx(6)
            // TODO: 若有成交資訊填入 LastQty(32)、LastPx(31)
            throw new NotImplementedException("BuildReport 尚未實作");
        }

        /// <summary>
        /// 建構拒絕回報（OrdStatus=Rejected，ExecType=Rejected）。
        /// </summary>
        /// <param name="order">被拒絕的訂單</param>
        /// <param name="rejectReason">拒絕原因文字</param>
        /// <param name="fixVersion">FIX 協議版本</param>
        /// <returns>拒絕用的 ExecutionReport FIX Message</returns>
        public Message BuildRejectReport(Order order, string rejectReason, string fixVersion)
        {
            // TODO: 設定 OrdStatus='8'、OrdRejReason(103)、Text(58)
            throw new NotImplementedException("BuildRejectReport 尚未實作");
        }
    }
}
