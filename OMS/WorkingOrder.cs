using System;
using System.ComponentModel;

namespace ATMML
{
    public enum OrderSide { Buy, Sell, SellShort, BuyToCover }
    public enum OrderType { Market, Limit, MarketOnClose, LimitOnClose }
    public enum OrderStatus
    {
        Staged,            // produced by model, not yet submitted to OMS
        PendingNew,
        New,
        PartiallyFilled,
        Filled,
        PendingCancel,
        Canceled,
        PendingReplace,
        Replaced,
        Rejected
    }

    /// <summary>
    /// Represents a single order — staged (pre-submission) or working (post-submission).
    /// The blotter binds to a collection of these and the bridge mutates them in place
    /// as state transitions occur (model produces → user sends → OMS acks → fills).
    /// </summary>
    public class WorkingOrder : INotifyPropertyChanged
    {
        private string _status;
        private int _filledQty;
        private int _leavesQty;
        private decimal? _avgPx;
        private string _lastMessage;
        private int _qty;
        private decimal? _limitPrice;

        // Identity
        public string ClOrdId { get; set; }
        public string OrderId { get; set; }
        public string Symbol { get; set; }
        public OrderSide Side { get; set; }
        public OrderType Type { get; set; }
        public DateTime SubmittedAt { get; set; }
        public string Strategy { get; set; }
        public string Destination { get; set; }

        // Optional reference to source FlexOneTrade — set when staging so that
        // SubmitStagedAsync can convert back without recomputing.
        public FlexOneTrade SourceTrade { get; set; }

        // Mutable
        public int Qty
        {
            get => _qty;
            set { if (_qty != value) { _qty = value; OnChanged(nameof(Qty)); } }
        }

        public decimal? LimitPrice
        {
            get => _limitPrice;
            set { if (_limitPrice != value) { _limitPrice = value; OnChanged(nameof(LimitPrice)); } }
        }

        public string Status
        {
            get => _status;
            set
            {
                if (_status == value) return;
                _status = value;
                OnChanged(nameof(Status));
                OnChanged(nameof(IsActive));
                OnChanged(nameof(IsStaged));
            }
        }

        public int FilledQty
        {
            get => _filledQty;
            set { if (_filledQty != value) { _filledQty = value; OnChanged(nameof(FilledQty)); } }
        }

        public int LeavesQty
        {
            get => _leavesQty;
            set { if (_leavesQty != value) { _leavesQty = value; OnChanged(nameof(LeavesQty)); } }
        }

        public decimal? AvgPx
        {
            get => _avgPx;
            set { if (_avgPx != value) { _avgPx = value; OnChanged(nameof(AvgPx)); } }
        }

        public string LastMessage
        {
            get => _lastMessage;
            set { if (_lastMessage != value) { _lastMessage = value; OnChanged(nameof(LastMessage)); } }
        }

        /// <summary>
        /// True when this order is showing in the blotter as something the user can
        /// act on — staged (pre-submission), working, or in-flight state changes.
        /// </summary>
        public bool IsActive =>
            Status == nameof(OrderStatus.Staged) ||
            Status == nameof(OrderStatus.PendingNew) ||
            Status == nameof(OrderStatus.New) ||
            Status == nameof(OrderStatus.PartiallyFilled) ||
            Status == nameof(OrderStatus.PendingCancel) ||
            Status == nameof(OrderStatus.PendingReplace);

        /// <summary>
        /// True if the order has not yet been submitted to the OMS. Cancel and modify
        /// behave differently for staged orders — they are mutated locally without
        /// any OMS round-trip.
        /// </summary>
        public bool IsStaged => Status == nameof(OrderStatus.Staged);

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnChanged(string n) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    public class OrderActionResult
    {
        public bool Success { get; set; }
        public string ClOrdId { get; set; }
        public string Message { get; set; }
    }
}
