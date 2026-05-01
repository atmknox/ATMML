using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace ATMML
{
    /// <summary>
    /// Abstraction over the FlexOne OMS layer.
    /// Swap between FlexOneOrderBridge (live) and FlexOneMockBridge (testing)
    /// via FlexOneBridgeFactory without changing any calling code.
    ///
    /// Surface covers three responsibilities:
    ///   1. Bulk rebalance submission and reconciliation (existing).
    ///   2. Live order management for the blotter: cancel, modify, inspect.
    ///   3. Staging / pre-trade workflow: orders produced by the model are
    ///      registered as Staged and visible in the blotter BEFORE any OMS
    ///      round-trip. The user reviews, optionally cancels/modifies, then
    ///      explicitly submits via the blotter's SEND ORDERS button.
    /// </summary>
    public interface IFlexOneOrderBridge
    {
        // ── Bulk submission / reconciliation ──────────────────────────────────

        FlexOneRebalanceResult SubmitRebalance(IEnumerable<FlexOneTrade> trades);
        bool CancelOrders(IEnumerable<string> orderIds);
        IEnumerable<Ft.OrderUpdateResponse> GetTradeActivity(DateTime tradeDate);
        IEnumerable<Ft.OrderUpdateResponse> ReplayOrders(int sinceSequenceId = 0);

        // ── Order Management (Blotter) ────────────────────────────────────────

        ObservableCollection<WorkingOrder> Orders { get; }
        bool IsMock();
        WorkingOrder RegisterWorkingOrder(WorkingOrder ord);
        WorkingOrder FindByClOrdId(string clOrdId);
        IEnumerable<WorkingOrder> GetWorkingOrders();
        Task<OrderActionResult> CancelOrderAsync(string clOrdId);
        Task<OrderActionResult> ModifyOrderAsync(string clOrdId, int? newQty, decimal? newLimit);
        Task<List<OrderActionResult>> CancelAllAsync(string strategyFilter = null);
        string NewClOrdId();

        // ── Staging / Pre-trade workflow ──────────────────────────────────────

        /// <summary>
        /// Registers the given trades as Staged in the blotter (pre-submission).
        /// Replaces any existing Staged orders for the same strategy so re-running
        /// the model overwrites the previous staged set rather than accumulating.
        /// </summary>
        void StageOrders(IEnumerable<FlexOneTrade> trades, string strategyName);

        /// <summary>Returns the count of currently-Staged orders.</summary>
        int StagedCount { get; }

        /// <summary>
        /// Submits all currently-Staged orders to the OMS via SubmitRebalance.
        /// Walks each order's state Staged → PendingNew → New (or → Rejected).
        /// </summary>
        Task<FlexOneRebalanceResult> SubmitStagedAsync();

        /// <summary>
        /// Removes a single Staged order from the blotter without any OMS round-trip.
        /// Throws if the order is not Staged (use CancelOrderAsync for working orders).
        /// </summary>
        void DiscardStaged(string clOrdId);

        /// <summary>Removes all Staged orders, optionally filtered by strategy.</summary>
        int DiscardAllStaged(string strategyFilter = null);
    }
}
