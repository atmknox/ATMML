using System;
using System.ComponentModel;

namespace ATMML
{
    public enum OrderSide { Buy, Sell, SellShort, BuyToCover }
    public enum OrderType { Market, Limit, MarketOnClose, LimitOnClose }
    public enum OrderStatus
    {
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
    /// Represents a single order routed to FlexOne. The blotter binds to a
    /// collection of these and the bridge mutates them in place as
    /// execution reports arrive (in live mode) or as the mock simulates them.
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
        public string ClOrdId { get; set; }       // our id
        public string OrderId { get; set; }       // venue/OMS id
        public string Symbol { get; set; }
        public OrderSide Side { get; set; }
        public OrderType Type { get; set; }
        public DateTime SubmittedAt { get; set; }
        public string Strategy { get; set; }      // e.g. "OEX V2", "CMR US LG CAP"
        public string Destination { get; set; }   // broker/venue tag

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

        public bool IsActive =>
            Status == nameof(OrderStatus.PendingNew) ||
            Status == nameof(OrderStatus.New) ||
            Status == nameof(OrderStatus.PartiallyFilled) ||
            Status == nameof(OrderStatus.PendingCancel) ||
            Status == nameof(OrderStatus.PendingReplace);

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
