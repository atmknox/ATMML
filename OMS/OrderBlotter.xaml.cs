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
        private readonly IFlexOneOrderBridge _bridge;
        private readonly DispatcherTimer _refreshTimer;
        private ICollectionView _view;

        public OrderBlotter(IFlexOneOrderBridge bridge)
        {
            InitializeComponent();
            _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));

            TxtMode.Text = _bridge.IsMock() ? "MOCK" : "LIVE";
            TxtMode.Foreground = _bridge.IsMock()
                ? System.Windows.Media.Brushes.Yellow
                : System.Windows.Media.Brushes.PaleGreen;

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

            int staged = _bridge.StagedCount;
            int working = _bridge.GetWorkingOrders().Count() - staged;
            TxtStagedCount.Text = staged.ToString();
            TxtActiveCount.Text = working.ToString();

            // Submit-bar state
            BtnSendOrders.IsEnabled = staged > 0;
            BtnDiscardStaged.IsEnabled = staged > 0;
            UpdateSubmitStatus(staged);
        }

        private void UpdateSubmitStatus(int staged)
        {
            if (staged == 0)
            {
                TxtSubmitStatus.Text = "Run a model to stage orders.";
                TxtSubmitStatus.Foreground = System.Windows.Media.Brushes.Gray;
                return;
            }

            // Time-lock applies in LIVE mode only. Mock allows any-time submission.
            if (!_bridge.IsMock() && DateTime.Now.TimeOfDay < TimeSpan.FromHours(15))
            {
                TxtSubmitStatus.Text = $"{staged} order(s) staged. Submission gated until 3:00 PM " +
                                       $"(current: {DateTime.Now:HH:mm}).";
                TxtSubmitStatus.Foreground = System.Windows.Media.Brushes.Yellow;
                BtnSendOrders.IsEnabled = false;
            }
            else
            {
                TxtSubmitStatus.Text = $"{staged} order(s) ready to submit.";
                TxtSubmitStatus.Foreground = System.Windows.Media.Brushes.PaleGreen;
            }
        }

        private List<WorkingOrder> SelectedOrders() =>
            OrderGrid.SelectedItems.OfType<WorkingOrder>().ToList();

        // ---------------------------------------------------------------
        // SEND ORDERS — submits all Staged orders
        // ---------------------------------------------------------------
        private async void BtnSendOrders_Click(object sender, RoutedEventArgs e)
        {
            int staged = _bridge.StagedCount;
            if (staged == 0) return;

            // Time-lock guard (live only)
            if (!_bridge.IsMock() && DateTime.Now.TimeOfDay < TimeSpan.FromHours(15))
            {
                MessageBox.Show(
                    $"Current time: {DateTime.Now:HH:mm}\n\n" +
                    "Orders cannot be placed before 3:00 PM.\n" +
                    "The 3:00 PM signal run is the authoritative trade list.\n\n" +
                    "Run the model again at or after 3:00 PM to send final orders.",
                    "⚠ Cannot Place Trades Before 3:00 PM",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Build a summary of what's about to be sent
            var stagedList = _bridge.Orders.Where(o => o.IsStaged).ToList();
            var summary = string.Join("\n",
                stagedList
                    .GroupBy(o => o.Side)
                    .OrderBy(g => g.Key.ToString())
                    .Select(g => $"  {g.Key}: {g.Count()} order(s), {g.Sum(o => o.Qty):N0} shares"));

            var modeTag = _bridge.IsMock() ? "[MOCK]" : "[LIVE]";
            var confirm = MessageBox.Show(
                $"Submit {staged} staged order(s) to FlexOne?\n\n" +
                $"Mode: {modeTag}\n\n" +
                $"{summary}\n\n" +
                "After submission, individual orders can still be modified or cancelled " +
                "from the blotter.",
                "Confirm Submission",
                MessageBoxButton.YesNo,
                _bridge.IsMock() ? MessageBoxImage.Question : MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            BtnSendOrders.IsEnabled = false;
            TxtSubmitStatus.Text = "Submitting…";
            TxtSubmitStatus.Foreground = System.Windows.Media.Brushes.PaleGreen;

            try
            {
                var result = await _bridge.SubmitStagedAsync();
                TxtStatus.Text = $"Submitted: {result.OrdersPlaced} ok, {result.OrdersFailed} failed.";

                if (!result.Success || result.OrdersFailed > 0)
                {
                    var failures = string.Join("\n",
                        result.Details.Where(d => !d.Success)
                                      .Select(d => $"  • {d.Ticker}: {d.Message}"));
                    MessageBox.Show(
                        $"Orders placed: {result.OrdersPlaced}\n" +
                        $"Orders failed: {result.OrdersFailed}\n\n" +
                        (string.IsNullOrEmpty(failures) ? "" : $"Failed:\n{failures}"),
                        "Submission Warnings",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    MessageBox.Show(
                        $"{result.OrdersPlaced} order(s) submitted successfully.",
                        modeTag + " Submission Complete",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                TxtStatus.Text = $"Submit error: {ex.Message}";
                MessageBox.Show($"Submission failed:\n\n{ex.Message}",
                                "❌ FlexOne Error",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
            RefreshView();
        }

        private void BtnDiscardStaged_Click(object sender, RoutedEventArgs e)
        {
            int staged = _bridge.StagedCount;
            if (staged == 0) return;

            if (MessageBox.Show(
                    $"Discard {staged} staged order(s)?\n\n" +
                    "Nothing has been submitted yet — this just clears the staged list.",
                    "Discard Staged",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            int n = _bridge.DiscardAllStaged();
            TxtStatus.Text = $"Discarded {n} staged order(s).";
            RefreshView();
        }

        // ---------------------------------------------------------------
        // Cancel — works for both Staged (discard) and working (OMS cancel)
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

            var msg = $"Cancel ALL {n} active order(s)?\n\n" +
                      "Staged orders will be discarded; working orders will be cancelled at the OMS.\n" +
                      "This cannot be undone.";
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
				TxtStatus.Foreground = System.Windows.Media.Brushes.Yellow;
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
		}// ---------------------------------------------------------------
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
