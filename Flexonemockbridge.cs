using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace ATMML
{
    /// <summary>
    /// Drop-in mock for FlexOneOrderBridge. Implements IFlexOneOrderBridge identically
    /// to the real bridge so all calling code is exercised without any gRPC/network
    /// dependency. This includes the staging surface (StageOrders/SubmitStagedAsync)
    /// and the order-management surface (cancel/modify/cancel-all). Working-order
    /// state changes simulate venue behavior with a 150ms delay; staged-order changes
    /// are immediate.
    /// </summary>
    public class FlexOneMockBridge : IFlexOneOrderBridge
    {
        // ── Test control ──────────────────────────────────────────────────────────

        public bool SimulateFailure { get; set; } = false;
        public Exception SimulateException { get; set; } = null;
        public HashSet<string> FailTickers { get; } =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // ── Inspection ────────────────────────────────────────────────────────────

        public List<FlexOneTrade> SubmittedTrades { get; } = new List<FlexOneTrade>();
        public List<string> CancelledOrderIds { get; } = new List<string>();
        public List<FlexOneOrderResult> LastResults { get; } = new List<FlexOneOrderResult>();
        public int SubmitCallCount { get; private set; }

        // ── Order Blotter surface ─────────────────────────────────────────────────

        private readonly ObservableCollection<WorkingOrder> _orders =
            new ObservableCollection<WorkingOrder>();

        public ObservableCollection<WorkingOrder> Orders => _orders;
        public bool IsMock() => true;

        public IEnumerable<WorkingOrder> GetWorkingOrders() =>
            _orders.Where(o => o.IsActive).ToList();

        public WorkingOrder FindByClOrdId(string clOrdId) =>
            _orders.FirstOrDefault(o => o.ClOrdId == clOrdId);

        public int StagedCount => _orders.Count(o => o.IsStaged);

        // ── Staging ───────────────────────────────────────────────────────────────

        public void StageOrders(IEnumerable<FlexOneTrade> trades, string strategyName)
        {
            if (trades == null) throw new ArgumentNullException(nameof(trades));
            var list = trades.ToList();

            DispatchUI(() =>
            {
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
            });

            Console.WriteLine($"[FlexOneMock] Staged {list.Count} orders for '{strategyName}'.");
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

            // Hydrate state from the result. Match by Symbol since OrderId is freshly
            // assigned; the staged row already has the SourceTrade so we know its ticker.
            foreach (var st in staged)
            {
                var detail = result.Details.FirstOrDefault(d => d.Ticker == st.Symbol);
                if (detail == null)
                {
                    st.Status = nameof(OrderStatus.Rejected);
                    st.LastMessage = "No detail returned from OMS";
                    continue;
                }
                if (detail.Success)
                {
                    st.OrderId = detail.OrderId;
                    st.Status = nameof(OrderStatus.New);
                    st.LastMessage = detail.Message;
                }
                else
                {
                    st.Status = nameof(OrderStatus.Rejected);
                    st.LastMessage = detail.Message;
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
                    $"Order {clOrdId} is not Staged (Status={ord.Status}).");
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

        // ── IFlexOneOrderBridge: bulk submission ──────────────────────────────────
        // Note: this is called by SubmitStagedAsync; it does NOT auto-stage. Staging
        // is the explicit caller's responsibility (PortfolioBuilder calls StageOrders
        // when the model produces a rebalance).

        public FlexOneRebalanceResult SubmitRebalance(IEnumerable<FlexOneTrade> trades)
        {
            if (SimulateException != null)
                throw SimulateException;

            var tradeList = trades?.ToList()
                ?? throw new ArgumentNullException(nameof(trades));

            SubmitCallCount++;
            SubmittedTrades.AddRange(tradeList);
            LastResults.Clear();

            Console.WriteLine($"[FlexOneMock] SubmitRebalance — {tradeList.Count} trades.");

            var result = new FlexOneRebalanceResult();

            foreach (var trade in tradeList)
            {
                bool ok = !SimulateFailure && !FailTickers.Contains(trade.Ticker);

                var orderResult = new FlexOneOrderResult
                {
                    Ticker  = trade.Ticker,
                    OrderId = ok ? GenerateMockOrderId(trade.Ticker) : null,
                    Success = ok,
                    Message = ok
                        ? $"[Mock] Accepted — {trade.Action} {trade.Shares} {trade.Ticker}"
                        : $"[Mock] Rejected — SimulateFailure={SimulateFailure} " +
                          $"FailTicker={FailTickers.Contains(trade.Ticker)}"
                };

                result.Details.Add(orderResult);
                LastResults.Add(orderResult);

                if (ok) result.OrdersPlaced++;
                else result.OrdersFailed++;
            }

            result.Success = result.OrdersFailed == 0;
            result.Description = result.Success
                ? $"[Mock] All {result.OrdersPlaced} orders accepted"
                : $"[Mock] {result.OrdersFailed} of {tradeList.Count} orders failed";

            return result;
        }

        public bool CancelOrders(IEnumerable<string> orderIds)
        {
            var ids = orderIds?.ToList()
                ?? throw new ArgumentNullException(nameof(orderIds));

            CancelledOrderIds.AddRange(ids);
            foreach (var id in ids)
            {
                var ord = FindByClOrdId(id);
                if (ord != null && ord.IsActive)
                {
                    ord.LeavesQty = 0;
                    ord.Status = nameof(OrderStatus.Canceled);
                    ord.LastMessage = "Canceled (bulk, mock)";
                }
            }
            return !SimulateFailure;
        }

        public IEnumerable<Ft.OrderUpdateResponse> GetTradeActivity(DateTime tradeDate) =>
            Enumerable.Empty<Ft.OrderUpdateResponse>();

        public IEnumerable<Ft.OrderUpdateResponse> ReplayOrders(int sinceSequenceId = 0) =>
            Enumerable.Empty<Ft.OrderUpdateResponse>();

        // ── Cancel / Modify (Staged-aware) ────────────────────────────────────────

        public async Task<OrderActionResult> CancelOrderAsync(string clOrdId)
        {
            var ord = FindByClOrdId(clOrdId);
            if (ord == null) return Fail(clOrdId, "Unknown ClOrdID");
            if (!ord.IsActive) return Fail(clOrdId, $"Not active ({ord.Status})");

            if (ord.IsStaged)
            {
                DispatchUI(() => _orders.Remove(ord));
                return Ok(clOrdId, "Discarded (was staged)");
            }

            ord.Status = nameof(OrderStatus.PendingCancel);
            ord.LastMessage = "Cancel sent (mock)";

            await Task.Delay(150);
            ord.LeavesQty = 0;
            ord.Status = nameof(OrderStatus.Canceled);
            ord.LastMessage = "Canceled (mock)";
            return Ok(clOrdId, "Canceled");
        }

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

        // ── Helpers ───────────────────────────────────────────────────────────────

        public void Reset()
        {
            SubmittedTrades.Clear();
            CancelledOrderIds.Clear();
            LastResults.Clear();
            SubmitCallCount = 0;
            FailTickers.Clear();
            SimulateFailure = false;
            SimulateException = null;
            DispatchUI(() => _orders.Clear());
        }

        private static string GenerateMockOrderId(string ticker)
        {
            var guid = Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper();
            return $"MOCK-{ticker}-{guid}";
        }

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
