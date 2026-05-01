using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using System.ComponentModel;

namespace ATMML
{
    public partial class OrderBlotter : Window
    {
        private readonly FlexOneOrderBridge _bridge;
        private readonly DispatcherTimer _refreshTimer;
        private ICollectionView _view;

        public OrderBlotter(FlexOneOrderBridge bridge)
        {
            InitializeComponent();
            _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));

            TxtMode.Text = _bridge.IsMock() ? "MOCK" : "LIVE";
            TxtMode.Foreground = _bridge.IsMock()
                ? System.Windows.Media.Brushes.Goldenrod
                : System.Windows.Media.Brushes.LimeGreen;

            _view = CollectionViewSource.GetDefaultView(_bridge.Orders);
            _view.Filter = OrderFilter;
            OrderGrid.ItemsSource = _view;

            BuildStrategyFilter();

            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _refreshTimer.Tick += (_, __) => RefreshView();
            _refreshTimer.Start();

            Closed += (_, __) => _refreshTimer.Stop();
            RefreshView();
        }

        // ---------------------------------------------------------------
        // Filtering
        // ---------------------------------------------------------------
        private void BuildStrategyFilter()
        {
            var items = new List<string> { "(All)" };
            items.AddRange(_bridge.Orders.Select(o => o.Strategy)
                                          .Where(s => !string.IsNullOrEmpty(s))
                                          .Distinct()
                                          .OrderBy(s => s));
            CmbStrategy.ItemsSource = items;
            CmbStrategy.SelectedIndex = 0;
        }

        private bool OrderFilter(object o)
        {
            var w = o as WorkingOrder;
            if (w == null) return false;
            if (ChkShowAll.IsChecked != true && !w.IsActive) return false;

            var sel = CmbStrategy.SelectedItem as string;
            if (!string.IsNullOrEmpty(sel) && sel != "(All)" && w.Strategy != sel) return false;

            return true;
        }

        private void RefreshView()
        {
            // Refresh strategy list if new strategies appeared
            var current = CmbStrategy.SelectedItem as string;
            var known = (CmbStrategy.ItemsSource as IEnumerable<string>) ?? new List<string>();
            var actual = new HashSet<string>(_bridge.Orders.Select(o => o.Strategy)
                                                            .Where(s => !string.IsNullOrEmpty(s)));
            if (!actual.IsSubsetOf(known))
            {
                BuildStrategyFilter();
                if (current != null && (CmbStrategy.ItemsSource as IEnumerable<string>).Contains(current))
                    CmbStrategy.SelectedItem = current;
            }

            _view.Refresh();
            TxtActiveCount.Text = _bridge.GetWorkingOrders().Count().ToString();
        }

        private List<WorkingOrder> SelectedOrders() =>
            OrderGrid.SelectedItems.OfType<WorkingOrder>().ToList();

        // ---------------------------------------------------------------
        // Cancel
        // ---------------------------------------------------------------
        private async void BtnCancelSelected_Click(object sender, RoutedEventArgs e)
        {
            var sel = SelectedOrders().Where(o => o.IsActive).ToList();
            if (sel.Count == 0) { TxtStatus.Text = "No active orders selected."; return; }

            if (MessageBox.Show($"Cancel {sel.Count} order(s)?",
                                "Confirm Cancel",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Question) != MessageBoxResult.Yes) return;

            int ok = 0, fail = 0;
            foreach (var o in sel)
            {
                try
                {
                    var r = await _bridge.CancelOrderAsync(o.ClOrdId);
                    if (r.Success) ok++; else fail++;
                }
                catch (Exception ex)
                {
                    fail++;
                    o.LastMessage = $"Cancel error: {ex.Message}";
                }
            }
            TxtStatus.Text = $"Cancel: {ok} ok, {fail} failed.";
            RefreshView();
        }

        private async void BtnCancelAll_Click(object sender, RoutedEventArgs e)
        {
            var n = _bridge.GetWorkingOrders().Count();
            if (n == 0) { TxtStatus.Text = "No active orders."; return; }

            var msg = $"Cancel ALL {n} working order(s)?\n\nThis cannot be undone.";
            if (MessageBox.Show(msg, "Confirm Cancel ALL",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

            try
            {
                var results = await _bridge.CancelAllAsync();
                int ok = results.Count(r => r.Success);
                TxtStatus.Text = $"Cancel ALL: {ok}/{results.Count} succeeded.";
            }
            catch (Exception ex)
            {
                TxtStatus.Text = $"Cancel ALL failed: {ex.Message}";
            }
            RefreshView();
        }

        // ---------------------------------------------------------------
        // Modify
        // ---------------------------------------------------------------
        private async void BtnModifySelected_Click(object sender, RoutedEventArgs e)
        {
            var sel = SelectedOrders().Where(o => o.IsActive).ToList();
            if (sel.Count != 1)
            {
                TxtStatus.Text = "Select exactly one active order to modify.";
                return;
            }
            await ModifyOne(sel[0]);
        }

        private async Task ModifyOne(WorkingOrder ord)
        {
            var dlg = new ModifyOrderDialog(ord) { Owner = this };
            if (dlg.ShowDialog() != true) return;

            try
            {
                var r = await _bridge.ModifyOrderAsync(ord.ClOrdId, dlg.NewQty, dlg.NewLimit);
                TxtStatus.Text = r.Success
                    ? $"Modified {ord.ClOrdId}: {r.Message}"
                    : $"Modify failed: {r.Message}";
                if (!r.Success)
                    MessageBox.Show(r.Message, "Modify Failed",
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                TxtStatus.Text = $"Modify error: {ex.Message}";
                MessageBox.Show(ex.Message, "Modify Error",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
            RefreshView();
        }

        // ---------------------------------------------------------------
        // Misc handlers
        // ---------------------------------------------------------------
        private void BtnRefresh_Click(object sender, RoutedEventArgs e) => RefreshView();
        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
        private void ChkShowAll_Click(object sender, RoutedEventArgs e) => RefreshView();
        private void CmbStrategy_SelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshView();

        private async void OrderGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var ord = SelectedOrders().FirstOrDefault();
            if (ord != null && ord.IsActive) await ModifyOne(ord);
        }

        private async void MenuCancel_Click(object sender, RoutedEventArgs e)
        {
            var sel = SelectedOrders().Where(o => o.IsActive).ToList();
            foreach (var o in sel)
            {
                try { await _bridge.CancelOrderAsync(o.ClOrdId); }
                catch (Exception ex) { o.LastMessage = $"Cancel error: {ex.Message}"; }
            }
            RefreshView();
        }

        private async void MenuModify_Click(object sender, RoutedEventArgs e)
        {
            var ord = SelectedOrders().FirstOrDefault();
            if (ord != null && ord.IsActive) await ModifyOne(ord);
        }

        private void MenuCopyId_Click(object sender, RoutedEventArgs e)
        {
            var sel = SelectedOrders().FirstOrDefault();
            if (sel != null)
            {
                Clipboard.SetText(sel.ClOrdId);
                TxtStatus.Text = $"Copied: {sel.ClOrdId}";
            }
        }
    }
}
