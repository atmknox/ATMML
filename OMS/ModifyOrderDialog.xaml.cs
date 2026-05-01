using System.Globalization;
using System.Windows;

namespace ATMML
{
    public partial class ModifyOrderDialog : Window
    {
        public int? NewQty { get; private set; }
        public decimal? NewLimit { get; private set; }
        private readonly WorkingOrder _ord;

        public ModifyOrderDialog(WorkingOrder ord)
        {
            InitializeComponent();
            _ord = ord;

            TxtSummary.Text = $"{ord.Side} {ord.Qty} {ord.Symbol} ({ord.Type})  ClOrdID={ord.ClOrdId}";
            TxtFilled.Text  = $"{ord.FilledQty} of {ord.Qty} (leaves {ord.LeavesQty})";
            TxtQty.Text     = ord.Qty.ToString();
            TxtLimit.Text   = ord.LimitPrice.HasValue
                                ? ord.LimitPrice.Value.ToString("0.0000", CultureInfo.InvariantCulture)
                                : "";

            Loaded += (_, __) =>
            {
                TxtQty.Focus();
                TxtQty.SelectAll();
            };
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            TxtError.Text = "";

            int? qty = null;
            decimal? lim = null;

            if (!string.IsNullOrWhiteSpace(TxtQty.Text))
            {
                if (!int.TryParse(TxtQty.Text, NumberStyles.Integer,
                                  CultureInfo.InvariantCulture, out var q) || q <= 0)
                {
                    TxtError.Text = "Qty must be a positive integer.";
                    return;
                }
                qty = q;
            }

            if (!string.IsNullOrWhiteSpace(TxtLimit.Text))
            {
                if (!decimal.TryParse(TxtLimit.Text, NumberStyles.Float,
                                      CultureInfo.InvariantCulture, out var p) || p <= 0)
                {
                    TxtError.Text = "Limit must be a positive number.";
                    return;
                }
                lim = p;
            }

            if (!qty.HasValue && !lim.HasValue)
            {
                TxtError.Text = "Enter a new qty and/or new limit.";
                return;
            }

            bool qtyChanged = qty.HasValue && qty.Value != _ord.Qty;
            bool limChanged = lim.HasValue && lim.Value != (_ord.LimitPrice ?? 0m);
            if (!qtyChanged && !limChanged)
            {
                TxtError.Text = "No change vs current order.";
                return;
            }

            if (qty.HasValue && qty.Value <= _ord.FilledQty)
            {
                TxtError.Text = $"New qty must exceed already-filled qty ({_ord.FilledQty}).";
                return;
            }

            // Only return values that changed (lets bridge skip no-op fields).
            NewQty   = qtyChanged ? qty : null;
            NewLimit = limChanged ? lim : null;
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
