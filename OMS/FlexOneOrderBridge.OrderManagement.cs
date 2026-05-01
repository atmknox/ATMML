using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace ATMML
{
    /// <summary>
    /// Partial-class extension on FlexOneOrderBridge that adds order-management
    /// (cancel/modify/cancel-all) and an in-memory order book the blotter binds to.
    ///
    /// Assumes the existing FlexOneOrderBridge declares a private/internal/protected
    /// field named "useMock" (bool). If it doesn't, replace `useMock` below with
    /// whatever flag your bridge already exposes for mock vs. live mode.
    ///
    /// LIVE PATH: The CancelOrderAsync / ModifyOrderAsync methods throw
    /// NotImplementedException in non-mock mode. Replace those throws with calls
    /// against your generated FlexOne gRPC client. The shape of those calls is
    /// commented inline.
    /// </summary>
    public partial class FlexOneOrderBridge
    {
        // Single source of truth for the blotter. Hydrated from execution reports
        // in live mode; mutated directly in mock mode.
        private readonly ObservableCollection<WorkingOrder> _orders = new ObservableCollection<WorkingOrder>();
        public ObservableCollection<WorkingOrder> Orders => _orders;

        public bool IsMock() => useMock;

        public IEnumerable<WorkingOrder> GetWorkingOrders() =>
            _orders.Where(o => o.IsActive).ToList();

        public WorkingOrder FindByClOrdId(string clOrdId) =>
            _orders.FirstOrDefault(o => o.ClOrdId == clOrdId);

        // ---------------------------------------------------------------------
        // Cancel
        // ---------------------------------------------------------------------
        public async Task<OrderActionResult> CancelOrderAsync(string clOrdId)
        {
            var ord = FindByClOrdId(clOrdId);
            if (ord == null)
                return Fail(clOrdId, "Unknown ClOrdID");
            if (!ord.IsActive)
                return Fail(clOrdId, $"Not active ({ord.Status})");

            ord.Status = nameof(OrderStatus.PendingCancel);
            ord.LastMessage = "Cancel sent";

            if (useMock)
            {
                await Task.Delay(150);
                ord.LeavesQty = 0;
                ord.Status = nameof(OrderStatus.Canceled);
                ord.LastMessage = "Canceled (mock)";
                return Ok(clOrdId, "Canceled");
            }

            // === LIVE PATH ===
            // var resp = await _grpcClient.CancelOrderAsync(new CancelOrderRequest {
            //     OrigClOrdId = clOrdId,
            //     ClOrdId     = NewClOrdId(),     // FIX: cancel request gets a new ClOrdID
            //     Symbol      = ord.Symbol,
            //     Side        = ord.Side.ToString(),
            //     Qty         = ord.Qty
            // });
            // ord.LastMessage = resp.Message;
            // if (!resp.Accepted) {
            //     ord.Status = nameof(OrderStatus.New);   // revert pending
            //     return Fail(clOrdId, resp.Message);
            // }
            // // Final Canceled state will arrive via execution report and update ord.Status.
            // return Ok(clOrdId, "Cancel accepted");

            throw new NotImplementedException(
                "CancelOrderAsync: wire to FlexOne gRPC CancelOrder request.");
        }

        // ---------------------------------------------------------------------
        // Modify (Replace)
        // ---------------------------------------------------------------------
        public async Task<OrderActionResult> ModifyOrderAsync(
            string clOrdId, int? newQty, decimal? newLimit)
        {
            var ord = FindByClOrdId(clOrdId);
            if (ord == null)
                return Fail(clOrdId, "Unknown ClOrdID");
            if (!ord.IsActive)
                return Fail(clOrdId, $"Not active ({ord.Status})");
            if (!newQty.HasValue && !newLimit.HasValue)
                return Fail(clOrdId, "Nothing to modify");
            if (newQty.HasValue && newQty.Value <= ord.FilledQty)
                return Fail(clOrdId, $"New qty ({newQty}) must exceed filled ({ord.FilledQty})");
            if (newQty.HasValue && newQty.Value <= 0)
                return Fail(clOrdId, "New qty must be positive");

            ord.Status = nameof(OrderStatus.PendingReplace);
            ord.LastMessage = $"Replace sent (qty={(newQty?.ToString() ?? "-")}, px={(newLimit?.ToString("0.0000") ?? "-")})";

            if (useMock)
            {
                await Task.Delay(150);
                if (newQty.HasValue)
                {
                    ord.Qty = newQty.Value;
                    ord.LeavesQty = Math.Max(0, newQty.Value - ord.FilledQty);
                }
                if (newLimit.HasValue) ord.LimitPrice = newLimit.Value;
                ord.Status = nameof(OrderStatus.New);
                ord.LastMessage = "Replaced (mock)";
                return Ok(clOrdId, "Replaced");
            }

            // === LIVE PATH ===
            // var resp = await _grpcClient.ReplaceOrderAsync(new ReplaceOrderRequest {
            //     OrigClOrdId = clOrdId,
            //     ClOrdId     = NewClOrdId(),
            //     Symbol      = ord.Symbol,
            //     Side        = ord.Side.ToString(),
            //     Qty         = newQty   ?? ord.Qty,
            //     Price       = (double)(newLimit ?? ord.LimitPrice ?? 0m),
            //     OrdType     = (newLimit ?? ord.LimitPrice).HasValue ? "LIMIT" : "MARKET",
            //     Tif         = "DAY"
            // });
            // ord.LastMessage = resp.Message;
            // if (!resp.Accepted) {
            //     ord.Status = nameof(OrderStatus.New);
            //     return Fail(clOrdId, resp.Message);
            // }
            // // Final New/Replaced state will arrive via execution report.
            // return Ok(clOrdId, "Replace accepted");

            throw new NotImplementedException(
                "ModifyOrderAsync: wire to FlexOne gRPC ReplaceOrder request.");
        }

        // ---------------------------------------------------------------------
        // Cancel-all (optionally scoped by strategy name)
        // ---------------------------------------------------------------------
        public async Task<List<OrderActionResult>> CancelAllAsync(string strategyFilter = null)
        {
            var targets = _orders
                .Where(o => o.IsActive && (strategyFilter == null || o.Strategy == strategyFilter))
                .ToList();
            var results = new List<OrderActionResult>();
            foreach (var o in targets)
                results.Add(await CancelOrderAsync(o.ClOrdId));
            return results;
        }

        // ---------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------

        /// <summary>
        /// Call this from your existing order-submission path (FlexOneTradeAdapter,
        /// or wherever NewOrderSingle is sent) so the blotter sees the order.
        /// </summary>
        public WorkingOrder RegisterWorkingOrder(WorkingOrder ord)
        {
            if (ord == null) throw new ArgumentNullException(nameof(ord));
            if (string.IsNullOrEmpty(ord.ClOrdId)) ord.ClOrdId = NewClOrdId();
            if (ord.SubmittedAt == default) ord.SubmittedAt = DateTime.Now;
            if (string.IsNullOrEmpty(ord.Status)) ord.Status = nameof(OrderStatus.PendingNew);
            if (ord.LeavesQty == 0 && ord.FilledQty == 0) ord.LeavesQty = ord.Qty;

            if (Application.Current?.Dispatcher != null &&
                !Application.Current.Dispatcher.CheckAccess())
                Application.Current.Dispatcher.Invoke(() => _orders.Add(ord));
            else
                _orders.Add(ord);

            return ord;
        }

        public string NewClOrdId() =>
            $"ATMML-{DateTime.Now:yyyyMMdd-HHmmssfff}-{Guid.NewGuid().ToString("N").Substring(0, 6)}";

        private static OrderActionResult Ok(string id, string msg) =>
            new OrderActionResult { Success = true,  ClOrdId = id, Message = msg };

        private static OrderActionResult Fail(string id, string msg) =>
            new OrderActionResult { Success = false, ClOrdId = id, Message = msg };
    }
}
