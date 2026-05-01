using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace ATMML
{
    /// <summary>
    /// Partial-class extension on FlexOneOrderBridge: order-management surface
    /// (cancel/modify/cancel-all), staging surface (StageOrders/SubmitStagedAsync),
    /// and the in-memory order book the blotter binds to.
    ///
    /// LIVE PATH gRPC calls are commented inline in CancelOrderAsync / ModifyOrderAsync.
    /// Staged orders are mutated locally with no OMS round-trip; only orders that have
    /// been submitted (status New / PartiallyFilled / etc.) hit the OMS.
    /// </summary>
    public partial class FlexOneOrderBridge
    {
        private readonly ObservableCollection<WorkingOrder> _orders =
            new ObservableCollection<WorkingOrder>();

        public ObservableCollection<WorkingOrder> Orders => _orders;

        public bool IsMock() => false;

        public IEnumerable<WorkingOrder> GetWorkingOrders() =>
            _orders.Where(o => o.IsActive).ToList();

        public WorkingOrder FindByClOrdId(string clOrdId) =>
            _orders.FirstOrDefault(o => o.ClOrdId == clOrdId);

        public int StagedCount => _orders.Count(o => o.IsStaged);

        // ---------------------------------------------------------------------
        // Staging
        // ---------------------------------------------------------------------

        public void StageOrders(IEnumerable<FlexOneTrade> trades, string strategyName)
        {
            if (trades == null) throw new ArgumentNullException(nameof(trades));
            var list = trades.ToList();

            Action stage = () =>
            {
                // Replace any existing Staged orders for this strategy — re-running
                // the model overwrites the previous staged set rather than accumulating.
                var stale = _orders
                    .Where(o => o.IsStaged &&
                                (strategyName == null || o.Strategy == strategyName))
                    .ToList();
                foreach (var s in stale) _orders.Remove(s);

                foreach (var t in list)
                {
                    var wo = WorkingOrderFactory.StageFromTrade(t, strategyName, NewClOrdId());
                    _orders.Add(wo);
                }
            };

            DispatchUI(stage);
        }

        public async Task<FlexOneRebalanceResult> SubmitStagedAsync()
        {
            var staged = _orders.Where(o => o.IsStaged).ToList();
            if (staged.Count == 0)
            {
                return new FlexOneRebalanceResult
                {
                    Success = true,
                    Description = "No staged orders to submit.",
                    OrdersPlaced = 0,
                    OrdersFailed = 0
                };
            }

            // Mark Pending so the UI reflects in-flight state
            foreach (var o in staged)
            {
                o.Status = nameof(OrderStatus.PendingNew);
                o.LastMessage = "Submitting…";
            }

            FlexOneRebalanceResult result = null;
            await Task.Run(() =>
            {
                var trades = staged.Select(o => o.SourceTrade).Where(t => t != null).ToList();
                result = SubmitRebalance(trades);
            });

            // Hydrate state from the result
            foreach (var staged_ord in staged)
            {
                var detail = result.Details
                    .FirstOrDefault(d => d.Ticker == staged_ord.Symbol);
                if (detail == null)
                {
                    staged_ord.Status = nameof(OrderStatus.Rejected);
                    staged_ord.LastMessage = "No detail returned from OMS";
                    continue;
                }
                if (detail.Success)
                {
                    staged_ord.OrderId = detail.OrderId;
                    staged_ord.Status = nameof(OrderStatus.New);
                    staged_ord.LastMessage = detail.Message;
                }
                else
                {
                    staged_ord.Status = nameof(OrderStatus.Rejected);
                    staged_ord.LastMessage = detail.Message;
                }
            }

            return result;
        }

        public void DiscardStaged(string clOrdId)
        {
            var ord = FindByClOrdId(clOrdId);
            if (ord == null) return;
            if (!ord.IsStaged)
                throw new InvalidOperationException(
                    $"Order {clOrdId} is not Staged (Status={ord.Status}). " +
                    "Use CancelOrderAsync for working orders.");
            DispatchUI(() => _orders.Remove(ord));
        }

        public int DiscardAllStaged(string strategyFilter = null)
        {
            var targets = _orders
                .Where(o => o.IsStaged &&
                            (strategyFilter == null || o.Strategy == strategyFilter))
                .ToList();
            DispatchUI(() => { foreach (var t in targets) _orders.Remove(t); });
            return targets.Count;
        }

        // ---------------------------------------------------------------------
        // Cancel — Staged orders go through DiscardStaged path (no OMS); working
        // orders go through gRPC.
        // ---------------------------------------------------------------------
        public async Task<OrderActionResult> CancelOrderAsync(string clOrdId)
        {
            var ord = FindByClOrdId(clOrdId);
            if (ord == null) return Fail(clOrdId, "Unknown ClOrdID");
            if (!ord.IsActive) return Fail(clOrdId, $"Not active ({ord.Status})");

            if (ord.IsStaged)
            {
                // Pre-submission: just remove. No OMS round-trip.
                DispatchUI(() => _orders.Remove(ord));
                return Ok(clOrdId, "Discarded (was staged)");
            }

            ord.Status = nameof(OrderStatus.PendingCancel);
            ord.LastMessage = "Cancel sent";

            // === LIVE PATH ===
            // using (var client = _session.CreateOrderClient()) {
            //     var req = new Ft.CancelOrdersRequest { User = _cfg.User };
            //     req.OrderIds.Add(ord.OrderId);
            //     var resp = client.CancelOrders(req);
            //     ord.LastMessage = resp?.Status?.Description ?? "(no response)";
            //     if (resp?.Status?.Success != true) {
            //         ord.Status = nameof(OrderStatus.New);
            //         return Fail(clOrdId, ord.LastMessage);
            //     }
            //     return Ok(clOrdId, "Cancel accepted");
            // }

            await Task.Yield();
            throw new NotImplementedException(
                "CancelOrderAsync (live): wire to FlexOne gRPC CancelOrders. " +
                "See LIVE PATH comment in FlexOneOrderBridge.OrderManagement.cs.");
        }

        // ---------------------------------------------------------------------
        // Modify — Staged: update locally; working: cancel-replace via gRPC.
        // ---------------------------------------------------------------------
        public async Task<OrderActionResult> ModifyOrderAsync(
            string clOrdId, int? newQty, decimal? newLimit)
        {
            var ord = FindByClOrdId(clOrdId);
            if (ord == null) return Fail(clOrdId, "Unknown ClOrdID");
            if (!ord.IsActive) return Fail(clOrdId, $"Not active ({ord.Status})");
            if (!newQty.HasValue && !newLimit.HasValue)
                return Fail(clOrdId, "Nothing to modify");
            if (newQty.HasValue && newQty.Value <= ord.FilledQty)
                return Fail(clOrdId, $"New qty ({newQty}) must exceed filled ({ord.FilledQty})");
            if (newQty.HasValue && newQty.Value <= 0)
                return Fail(clOrdId, "New qty must be positive");

            if (ord.IsStaged)
            {
                // Pre-submission: mutate the order AND its source trade so the
                // eventual SubmitStagedAsync sends the modified values.
                if (newQty.HasValue)
                {
                    ord.Qty = newQty.Value;
                    ord.LeavesQty = newQty.Value;
                    if (ord.SourceTrade != null) ord.SourceTrade.Shares = newQty.Value;
                }
                if (newLimit.HasValue)
                {
                    ord.LimitPrice = newLimit.Value;
                    ord.Type = OrderType.Limit;
                    if (ord.SourceTrade != null)
                        ord.SourceTrade.LimitPrice = (double)newLimit.Value;
                }
                ord.LastMessage = "Modified (staged, no OMS round-trip)";
                await Task.Yield();
                return Ok(clOrdId, "Modified");
            }

            ord.Status = nameof(OrderStatus.PendingReplace);
            ord.LastMessage = $"Replace sent (qty={(newQty?.ToString() ?? "-")}, " +
                              $"px={(newLimit?.ToString("0.0000") ?? "-")})";

            // === LIVE PATH ===
            // FlexOne canonical pattern is cancel-replace: cancel the original
            // and submit a new order with the new params. Implement here when ready.

            await Task.Yield();
            throw new NotImplementedException(
                "ModifyOrderAsync (live): wire to FlexOne gRPC cancel-replace. " +
                "See LIVE PATH comment in FlexOneOrderBridge.OrderManagement.cs.");
        }

        // ---------------------------------------------------------------------
        // Cancel-all — Staged orders are discarded; working orders are cancelled.
        // ---------------------------------------------------------------------
        public async Task<List<OrderActionResult>> CancelAllAsync(string strategyFilter = null)
        {
            var targets = _orders
                .Where(o => o.IsActive && (strategyFilter == null || o.Strategy == strategyFilter))
                .ToList();
            var results = new List<OrderActionResult>();
            foreach (var o in targets)
            {
                try { results.Add(await CancelOrderAsync(o.ClOrdId)); }
                catch (Exception ex) { results.Add(Fail(o.ClOrdId, ex.Message)); }
            }
            return results;
        }

        // ---------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------

        public WorkingOrder RegisterWorkingOrder(WorkingOrder ord)
        {
            if (ord == null) throw new ArgumentNullException(nameof(ord));
            if (string.IsNullOrEmpty(ord.ClOrdId)) ord.ClOrdId = NewClOrdId();
            if (ord.SubmittedAt == default) ord.SubmittedAt = DateTime.Now;
            if (string.IsNullOrEmpty(ord.Status)) ord.Status = nameof(OrderStatus.PendingNew);
            if (ord.LeavesQty == 0 && ord.FilledQty == 0) ord.LeavesQty = ord.Qty;

            DispatchUI(() => _orders.Add(ord));
            return ord;
        }

        public string NewClOrdId() =>
            $"ATMML-{DateTime.Now:yyyyMMdd-HHmmssfff}-" +
            Guid.NewGuid().ToString("N").Substring(0, 6);

        private static void DispatchUI(Action a)
        {
            if (Application.Current?.Dispatcher != null &&
                !Application.Current.Dispatcher.CheckAccess())
                Application.Current.Dispatcher.Invoke(a);
            else
                a();
        }

        private static OrderActionResult Ok(string id, string msg) =>
            new OrderActionResult { Success = true, ClOrdId = id, Message = msg };

        private static OrderActionResult Fail(string id, string msg) =>
            new OrderActionResult { Success = false, ClOrdId = id, Message = msg };
    }
}
