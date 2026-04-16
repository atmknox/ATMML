using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Net;
using System.Windows.Threading;
using System.Threading;
//using Bloomberglp.TerminalApi;
using System.Windows.Markup;
using System.IO;
using System.Xml;
using LoadingControl.Control;
using System.Drawing.Printing;
using System.Globalization;
using Microsoft.Win32;

namespace ATMML
{
	public partial class Alerts : ContentControl
	{
		public enum AlertType
		{
			ConditionAlert,
			ServerAlert,
			PositionAlert,
			CommentAlert
		}

		MainView _mainView = null;

		AlertType _alertType = AlertType.ConditionAlert;

		Chart _chart1 = null;
		Chart _chart2 = null;
		Chart _chart3 = null;
		//Chart //_chart4 = null;

		bool _updateCalculator = false;

		string _mode = "";

		string _chartSymbol1 = "";
		string _chartSymbol2 = "";
		Dictionary<int, string> _chartIntervals = new Dictionary<int, string>();

		List<Alert> _updateAlerts = new List<Alert>();
		bool _addConditionFlash = false;
		bool _addPositionFlash = false;
		bool _addCommentFlash = false;
		DispatcherTimer _timer = new DispatcherTimer();

		Brush _oldButtonBrush = Brushes.White;

		BarCache _barCache = new BarCache(null);

		ScanDialog _scanDialog;
		PortfolioDialog _portfolioDialog;

		ConditionDialog _conditionDialog;

		Portfolio _portfolio = new Portfolio(4);

		const int _columnCount = 5;

		public Alerts(MainView mainView)
		{
			_mainView = mainView;

			initialize();
		}

		private ConditionDialog getConditionDialog()
		{
			if (_conditionDialog == null)
			{
				_conditionDialog = new ConditionDialog(_mainView);
				_conditionDialog.DialogEvent += new DialogEventHandler(dialogEvent);
			}
			_conditionDialog.Visibility = Visibility.Visible;

			return _conditionDialog;
		}

		private void setInfo()
		{
			string info =
				   _modelName;

			_mainView.SetInfo("Alerts", info);
		}

		private void getInfo()
		{
			string info = _mainView.GetInfo("Alerts");
			if (info != null && info.Length > 0)
			{
				string[] fields = info.Split(';');
				int count = fields.Length;
				if (count > 0) _modelName = fields[0];
			}
		}

		private void initialize()
		{
			InitializeComponent();

			LinkCharts.IsChecked = Portfolio.GetLinkCharts();
			LinkAccounts.IsChecked = Portfolio.GetLinkAccounts();

			for (int ii = 1; ii <= 4; ii++)
			{
				_chartIntervals[ii] = "";
			}

			ResourceDictionary dictionary = new ResourceDictionary();
			dictionary.Source = new Uri("pack://application:,,,/ATMML;component/StyleDictionary.xaml");
			this.Resources.MergedDictionaries.Add(dictionary);

			_mode = MainView.GetChartMode();

			if (!Alert.Manager.HasConditionAlerts())
			{
				Alert.Manager.AddExampleAlerts();
			}

			Links.Visibility = (MainView.EnableLinks) ? Visibility.Visible : Visibility.Hidden;

			ChartIntervals.Visibility = System.Windows.Visibility.Collapsed;
			Tree.SetValue(Grid.ColumnSpanProperty, 2);
			ChartGrid.Visibility = Visibility.Collapsed;
			CommentGrid.Visibility = Visibility.Collapsed;

			StkSymbol.AddHandler(MouseLeftButtonUpEvent, new MouseButtonEventHandler(Symbol_Click), true);
			SectorETFSymbol.AddHandler(MouseLeftButtonUpEvent, new MouseButtonEventHandler(Symbol_Click), true);
			IndexETFSymbol.AddHandler(MouseLeftButtonUpEvent, new MouseButtonEventHandler(Symbol_Click), true);
			SectorSpreadSymbol.AddHandler(MouseLeftButtonUpEvent, new MouseButtonEventHandler(Symbol_Click), true);
			IndexSpreadSymbol.AddHandler(MouseLeftButtonUpEvent, new MouseButtonEventHandler(Symbol_Click), true);

			_timer.Interval = TimeSpan.FromMilliseconds(100);
			_timer.Tick += new System.EventHandler(timer_Tick);
			_timer.Start();

			getInfo();

			updateModelInfo();

			initializeAlerts();

			Tree.Visibility = Visibility.Visible;

			ScanManager scanManager = Scan.Manager;

			if (!BarServer.ConnectedToBloomberg())
			{
				GlobalMacButton.Visibility = Visibility.Collapsed;
			}
		}

		private ScanDialog getScanDialog()
		{
			if (_scanDialog == null)
			{
				_scanDialog = new ScanDialog(_mainView);
				_scanDialog.DialogEvent += new DialogEventHandler(dialogEvent);
			}
			_scanDialog.Visibility = Visibility.Visible;

			return _scanDialog;
		}

		private void dialogEvent(object sender, DialogEventArgs e)
		{
			ScanDialog scanDialog = sender as ScanDialog;
			if (scanDialog != null)
			{
				DialogEventArgs.EventType type = e.Type;
				if (e.Type == DialogEventArgs.EventType.Ok)
				{
					ConditionAlert scanAlert = scanDialog.ConditionAlert;
					if (scanAlert == null)
					{
						ConditionAlert alert = addScanAlert(scanDialog.Title, scanDialog.PortfolioText, scanDialog.Condition, scanDialog.RunIntervalMinutes);
						alert.TextColor1 = scanDialog.TextColor1;
						alert.TextColor2 = scanDialog.TextColor2;
						alert.TextColor3 = scanDialog.TextColor3;
						alert.ViewType = scanDialog.ViewType;
						alert.NotificationOn = scanDialog.NotificationOn;
						alert.UseOnTop = scanDialog.UseOnTop;
						alert.RunInterval = scanDialog.RunIntervalMinutes;
						alert.Paused = false;
					}
					else
					{
						string title = scanAlert.Scan.Title;
						string portfolio = scanAlert.Scan.PortfolioName;
						string condition = scanAlert.Scan.Condition;
						int scanInterval = scanAlert.Scan.ScanInterval;

						scanAlert.NotificationOn = scanDialog.NotificationOn;
						scanAlert.UseOnTop = scanDialog.UseOnTop;
						scanAlert.RunInterval = scanDialog.RunIntervalMinutes;
						scanAlert.TextColor1 = scanDialog.TextColor1;
						scanAlert.TextColor2 = scanDialog.TextColor2;
						scanAlert.TextColor3 = scanDialog.TextColor3;
						scanAlert.ViewType = scanDialog.ViewType;
						updateAlertUI(scanAlert);

						if (scanDialog.Title != title)
						{
							MainView.DeleteUserData(@"alerts\" + title);
						}

						if (scanDialog.Title != title || scanDialog.PortfolioText != portfolio || scanDialog.Condition != condition || scanDialog.RunIntervalMinutes != scanInterval)
						{
							scanAlert.Stop();

							Scan.Manager.RemoveScan(scanAlert.Scan);
							Scan scan = Scan.Manager.AddScan(scanDialog.Title, scanDialog.PortfolioText, scanDialog.Condition, scanDialog.RunIntervalMinutes);
							scanAlert.Scan = scan;

							scanAlert.Portfolio = scanDialog.PortfolioText;

							updateAlertUI(scanAlert);
							scanAlert.TreeViewItem.Items.Clear();

							scanAlert.Paused = false;
							scanAlert.Run();
						}
						else
						{
							updateThisAlert(scanAlert);
						}
						scanDialog.ConditionAlert = null;
					}
				}
			}

			PortfolioDialog portfolioDialog = sender as PortfolioDialog;
			if (portfolioDialog != null)
			{
				if (e.Type == DialogEventArgs.EventType.Ok)
				{
					List<string> portfolios = portfolioDialog.GetPortfolios();
					MainView.SetPortfolios(portfolios);

				}
			}
			scanDialog.Visibility = System.Windows.Visibility.Hidden;
		}

		private void initializeAlerts()
		{
			List<Alert> alerts = Alert.Manager.Alerts;


			bool startFirst = false;  // set to true if you want the firt scan to auto start
			bool showRestoreExample = true;
			foreach (Alert alert in alerts)
			{
				alert.ActiveColumn = 0;

				ConditionAlert scanAlert = alert as ConditionAlert;

				if (scanAlert != null && scanAlert.NewInfo)
				{
				}

				if ((_alertType == AlertType.ConditionAlert && scanAlert != null))
				{
					addAlertUI(alert);
					if (startFirst)
					{
						startFirst = false;
						alert.Paused = false;
					}
				}
				else
				{
					removeAlertUI(alert);
				}
				addAlertEvents(alert);

				if (Alert.Manager.IsExampleAlert(alert))
				{
					showRestoreExample = false;
				}
			}
			RestoreExamplesButton.Visibility = showRestoreExample ? Visibility.Visible : Visibility.Hidden;
		}

		private void addAlertEvents(Alert alert)
		{
			alert.ProgressEvent -= new ProgressEventHandler(alert_ProgressEvent);
			alert.ProgressEvent += new ProgressEventHandler(alert_ProgressEvent);
			alert.FlashEvent -= new FlashEventHandler(alert_FlashEvent);
			alert.FlashEvent += new FlashEventHandler(alert_FlashEvent);
		}

		private void removeAlertEvents(Alert alert)
		{
			alert.ProgressEvent -= new ProgressEventHandler(alert_ProgressEvent);
			alert.FlashEvent -= new FlashEventHandler(alert_FlashEvent);
		}

		private void addCharts()
		{
			if (_chart1 == null)
			{
				bool showCursor = !_mainView.HideChartCursor;

				ChartGrid.Visibility = Visibility.Visible;

				_chart1 = new Chart(ChartCanvas1, ChartControlPanel1, showCursor);
				_chart2 = new Chart(ChartCanvas2, ChartControlPanel2, showCursor);
				_chart3 = new Chart(ChartCanvas3, ChartControlPanel3, showCursor);

				_chart1.HasTitleIntervals = true;
				_chart2.HasTitleIntervals = true;
				_chart3.HasTitleIntervals = true;

				//_chart3.SpreadSymbols = true;

				_chart1.Horizon = 2;
				_chart2.Horizon = 1;
				_chart3.Horizon = 0;

				_chart1.Strategy = "Strategy 1";
				_chart2.Strategy = "Strategy 1";
				_chart3.Strategy = "Strategy 1";

				loadChartProperties();

				_chart1.AddLinkedChart(_chart2);
				_chart1.AddLinkedChart(_chart3);
				_chart2.AddLinkedChart(_chart1);
				_chart2.AddLinkedChart(_chart3);
				_chart3.AddLinkedChart(_chart1);
				_chart3.AddLinkedChart(_chart2);

				addBoxes(false);

				_chart1.ChartEvent += new ChartEventHandler(Chart_ChartEvent);
				_chart2.ChartEvent += new ChartEventHandler(Chart_ChartEvent);
				_chart3.ChartEvent += new ChartEventHandler(Chart_ChartEvent);

				switchMode(_mode);
			}
		}

		private void updateCalculator(Chart chart)
		{
			_updateCalculator = true;
		}

		private void switchMode(string mode)
		{
			saveChartProperties();

			_mode = mode;

			MainView.SetChartMode(_mode);

			loadChartProperties();

			resetCharts();

			bool allCharts = (_mode == "");
			addBoxes(allCharts);
			if (allCharts)
			{
				_chart1.Show();
				_chart2.Show();
				_chart3.Show();
			}

			else
			{
				_chart1.Show();
				_chart2.Show();
				_chart3.Show();
			}
		}

		private void removeCharts()
		{
			if (_chart1 != null)
			{
				saveChartProperties();

				_chart1.ChartEvent -= new ChartEventHandler(Chart_ChartEvent);
				_chart2.ChartEvent -= new ChartEventHandler(Chart_ChartEvent);
				_chart3.ChartEvent -= new ChartEventHandler(Chart_ChartEvent);

				_chart1.Close();
				_chart2.Close();
				_chart3.Close();

				_chart1 = null;
				_chart2 = null;
				_chart3 = null;
			}
		}

		private void loadChartProperties()
		{
			bool research = (_mode == "PCM RESEARCH");

			//_chart1.Indicators["ATM Trend Bars"].Enabled = (research);
			//_chart2.Indicators["ATM Trend Bars"].Enabled = (research);
			//_chart3.Indicators["ATM Trend Bars"].Enabled = (research);

			_chart1.Indicators["ATM Trend Lines"].Enabled = (research);
			_chart2.Indicators["ATM Trend Lines"].Enabled = (research);
			_chart3.Indicators["ATM Trend Lines"].Enabled = (research);

			_chart1.Indicators["ATM Trigger"].Enabled = (research);
			_chart2.Indicators["ATM Trigger"].Enabled = (research);
			_chart3.Indicators["ATM Trigger"].Enabled = (research);

			_chart1.Indicators["ATM 3Sigma"].Enabled = (research);
			_chart2.Indicators["ATM 3Sigma"].Enabled = (research);
			_chart3.Indicators["ATM 3Sigma"].Enabled = (research);

			_chart1.Indicators["ATM Targets"].Enabled = (false);
			_chart2.Indicators["ATM Targets"].Enabled = (false);
			_chart3.Indicators["ATM Targets"].Enabled = (false);

			_chart1.Indicators["X Alert"].Enabled = (research);
			_chart2.Indicators["X Alert"].Enabled = (research);
			_chart3.Indicators["X Alert"].Enabled = (research);

			_chart1.Indicators["First Alert"].Enabled = (false);
			_chart2.Indicators["First Alert"].Enabled = (false);
			_chart3.Indicators["First Alert"].Enabled = (false);

			_chart1.Indicators["Add On Alert"].Enabled = (research);
			_chart2.Indicators["Add On Alert"].Enabled = (research);
			_chart3.Indicators["Add On Alert"].Enabled = (research);

			_chart1.Indicators["Pullback Alert"].Enabled = (research);
			_chart2.Indicators["Pullback Alert"].Enabled = (research);
			_chart3.Indicators["Pullback Alert"].Enabled = (research);

			_chart1.Indicators["Pressure Alert"].Enabled = (research);
			_chart2.Indicators["Pressure Alert"].Enabled = (research);
			_chart3.Indicators["Pressure Alert"].Enabled = (research);

			//_chart1.Indicators["PT Alert"].Enabled = (research);
			//_chart2.Indicators["PT Alert"].Enabled = (research);
			//_chart3.Indicators["PT Alert"].Enabled = (research);

			_chart1.Indicators["Exhaustion Alert"].Enabled = (research);
			_chart2.Indicators["Exhaustion Alert"].Enabled = (research);
			_chart3.Indicators["Exhaustion Alert"].Enabled = (research);

			_chart1.Indicators["Two Bar Alert"].Enabled = (false);
			_chart2.Indicators["Two Bar Alert"].Enabled = (false);
			_chart3.Indicators["Two Bar Alert"].Enabled = (false);

			//_chart1.Indicators["Two Bar Trend"].Enabled = (false);
			//_chart2.Indicators["Two Bar Trend"].Enabled = (false);
			//_chart3.Indicators["Two Bar Trend"].Enabled = (false);

			_chart1.Indicators["FT Alert"].Enabled = (false);
			_chart2.Indicators["FT Alert"].Enabled = (false);
			_chart3.Indicators["FT Alert"].Enabled = (false);

			_chart1.Indicators["ST Alert"].Enabled = (false);
			_chart2.Indicators["ST Alert"].Enabled = (false);
			_chart3.Indicators["ST Alert"].Enabled = (false);

			_chart1.Indicators["FTST Alert"].Enabled = (false);
			_chart2.Indicators["FTST Alert"].Enabled = (false);
			_chart3.Indicators["FTST Alert"].Enabled = (false);

			_chart1.Indicators["EW Counts"].Enabled = (false);
			_chart2.Indicators["EW Counts"].Enabled = (false);
			_chart3.Indicators["EW Counts"].Enabled = (false);

			_chart1.Indicators["EW PTI"].Enabled = (false);
			_chart2.Indicators["EW PTI"].Enabled = (false);
			_chart3.Indicators["EW PTI"].Enabled = (false);

			_chart1.Indicators["EW Projections"].Enabled = (false);
			_chart2.Indicators["EW Projections"].Enabled = (false);
			_chart3.Indicators["EW Projections"].Enabled = (false);

			_chart1.Indicators["Short Term FT Current"].Enabled = (research);
			_chart2.Indicators["Short Term FT Current"].Enabled = (research);
			_chart3.Indicators["Short Term FT Current"].Enabled = (research);

			_chart1.Indicators["Short Term FT Nxt Bar"].Enabled = (research);
			_chart2.Indicators["Short Term FT Nxt Bar"].Enabled = (research);
			_chart3.Indicators["Short Term FT Nxt Bar"].Enabled = (research);

			_chart1.Indicators["Short Term ST Current"].Enabled = (research);
			_chart2.Indicators["Short Term ST Current"].Enabled = (research);
			_chart3.Indicators["Short Term ST Current"].Enabled = (research);

			_chart1.Indicators["Short Term ST Nxt Bar"].Enabled = (research);
			_chart2.Indicators["Short Term ST Nxt Bar"].Enabled = (research);
			_chart3.Indicators["Short Term ST Nxt Bar"].Enabled = (research);

			_chart1.LoadProperties(research ? "PCM RESEARCH1_v13" : "Positions1_v13");
			_chart2.LoadProperties(research ? "PCM RESEARCH2_v13" : "Positions2_v13");
			_chart3.LoadProperties(research ? "PCM RESEARCH3_v13" : "Positions3_v13");
		}

		private void saveChartProperties()
		{
			bool research = (_mode == "PCM RESEARCH");

			_chart1.SaveProperties(research ? "PCM RESEARCH1_v13" : "Positions1_v13");
			_chart2.SaveProperties(research ? "PCM RESEARCH2_v13" : "Positions2_v13");
			_chart3.SaveProperties(research ? "PCM RESEARCH3_v13" : "Positions3_v13");
		}

		private ScanDialog? Alerts_scanDialog;

		private ConditionAlert addScanAlert(string title, string portfolio, string condition, int runInterval)
		{
			ConditionAlert alert = new ConditionAlert(title, portfolio, condition, runInterval);
			Alert.Manager.AddAlert(alert);
			addAlertUI(alert);
			addAlertEvents(alert);
			alert.Run();
			return alert;
		}

		private void removeAlertUI(Alert alert)
		{
			lock (alert)
			{
				if (alert.TreeViewItem != null)
				{
					alert.TreeViewItem.Expanded -= new RoutedEventHandler(item_Expanded);
					alert.TreeViewItem.Collapsed -= new RoutedEventHandler(item_Collapsed);

					alert.IsExpanded = alert.TreeViewItem.IsExpanded;

					Tree.Items.Remove(alert.TreeViewItem);

					alert.TreeViewItem = null;
				}
			}
		}

		private void updateAlertTimes()
		{
			List<Alert> alerts = Alert.Manager.Alerts;
			foreach (Alert alert in alerts)
			{
				if (alert.TreeViewItem != null)
				{
					Border border = alert.TreeViewItem.Header as Border;
					Grid grid1 = border.Child as Grid;

					Label label2 = grid1.Children[1] as Label;
					label2.Foreground = new SolidColorBrush(Colors.DarkOrange);// alert.TextColor3);
					label2.Content = alert.Title;

					// scan times
					StackPanel panel2 = grid1.Children[2] as StackPanel;
					TextBlock lastTime = panel2.Children[0] as TextBlock;
					TextBlock nextTime = panel2.Children[1] as TextBlock;
					lastTime.Foreground = Brushes.DarkGray;
					lastTime.Text = "LAST: " + alert.GetLastTime() + "  ";
					nextTime.Text = alert.Paused ? "\u25b6  " + "START" : (alert.Running ? "Calculating" : "NEXT: " + alert.GetNextTime());

					if (alert.Paused)
					{
						nextTime.Foreground = Brushes.PaleGreen;
						nextTime.ToolTip = "Click to begin your Scan";
					}
					else
					{
						nextTime.Foreground = Brushes.Red;
						nextTime.ToolTip = "Click to pause your Scan";
					}
				}
			}
		}

		void nextTime_MouseDown(object sender, MouseButtonEventArgs e)
		{
			TextBlock label = sender as TextBlock;
			StackPanel panel1 = label.Parent as StackPanel;
			Grid panel2 = panel1.Parent as Grid;
			Border border = panel2.Parent as Border;
			TreeViewItem item = border.Parent as TreeViewItem;

			List<Alert> alerts = Alert.Manager.Alerts;

			foreach (Alert alert in alerts)
			{
				if (alert.TreeViewItem == item)
				{
					alert.Paused = !alert.Paused;
					break;
				}
			}

			updateAlertTimes();
		}

		private void updateAlertUI(Alert alert)
		{
			if (alert.TreeViewItem != null)
			{
				Border border = alert.TreeViewItem.Header as Border;
				Grid grid1 = border.Child as Grid;

				Label label2 = grid1.Children[1] as Label;
				label2.Foreground = new SolidColorBrush(alert.TextColor3);
				label2.Content = alert.Title;

				// scan times
				StackPanel panel2 = grid1.Children[2] as StackPanel;
				TextBlock lastTime = panel2.Children[0] as TextBlock;
				TextBlock nextTime = panel2.Children[1] as TextBlock;
				lastTime.Text = "LAST: " + alert.GetLastTime();
				if (!alert.Paused)
				{
					nextTime.Text = "NEXT: " + alert.GetNextTime();
				}

				// number of rows
				StackPanel panel3 = grid1.Children[3] as StackPanel;
				panel3.Visibility = (alert.ViewType == Alert.AlertViewType.Spreadsheet) ? Visibility.Collapsed : Visibility.Visible;
				Label label3 = panel3.Children[1] as Label;
				label3.Foreground = new SolidColorBrush(alert.TextColor3);

				// row up/dn
				StackPanel panel4 = grid1.Children[4] as StackPanel;
				Label label4 = panel4.Children[1] as Label;
				label4.Foreground = new SolidColorBrush(alert.TextColor3);

				// edit
				Label label5 = grid1.Children[5] as Label;
				label5.Visibility = (alert.Editable) ? Visibility.Visible : Visibility.Collapsed;
				label5.Foreground = new SolidColorBrush(alert.TextColor3);

				// delete
				Label label6 = grid1.Children[6] as Label;
				label6.Visibility = (alert.Deleteable) ? Visibility.Visible : Visibility.Collapsed;
				label6.Foreground = new SolidColorBrush(alert.TextColor3);
			}
		}

		// Create (or reuse) a live ScanDialog, *passing MainView* to the ctor.
		private ScanDialog EnsureScanDialog(DependencyObject context)
		{
			// Resolve the owner Window for this control/handler
			var ownerWin = Window.GetWindow(context);

			// Resolve MainView (first try owner, then Application.Current.MainWindow)
			var mainView = ownerWin as MainView ?? Application.Current?.MainWindow as MainView;
			if (mainView == null)
				throw new InvalidOperationException("Cannot locate MainView. Make sure your main window is of type MainView.");

			// Reuse if we still have a visible instance
			if (_scanDialog != null && _scanDialog.IsVisible)
				return _scanDialog;

			// Create a fresh dialog with the required MainView parameter
			_scanDialog = new ScanDialog(mainView);

			if (ownerWin != null)
				_scanDialog.Owner = ownerWin;

			_scanDialog.Closed += (_, __) => _scanDialog = null;
			return _scanDialog;
		}

		private void addAlertUI(Alert alert)
		{
			lock (alert)
			{
				int fontSize = 10;

				alert.NewInfo = false;

				alert.TreeViewItem = new TreeViewItem();
				alert.TreeViewItem.Expanded += new RoutedEventHandler(item_Expanded);
				alert.TreeViewItem.Collapsed += new RoutedEventHandler(item_Collapsed);
				//alert.TreeViewItem.Padding = new Thickness(5);
				alert.TreeViewItem.Margin = new Thickness(0, 5, 13, 5);

				Brush textBrush = new SolidColorBrush(alert.TextColor3);
				Brush textBrushOrg = new SolidColorBrush(alert.TextColor1);

				LoadingAnimation busy = new LoadingAnimation(15, 15);
				busy.SetValue(Grid.ColumnProperty, 0);
				busy.HorizontalAlignment = HorizontalAlignment.Left;

				Border border = new Border();
				border.BorderBrush = new SolidColorBrush(Color.FromRgb(18, 75, 114));
				border.BorderThickness = new Thickness(1); // use 0 to remove border
				Grid panel1 = new Grid();
				panel1.Margin = new Thickness(0, 0, 0, 0);
				border.Child = panel1;

				RowDefinition row0 = new RowDefinition();
				RowDefinition row1 = new RowDefinition();
				row0.Height = new GridLength(20, GridUnitType.Pixel);
				row1.Height = new GridLength(5, GridUnitType.Pixel);
				panel1.RowDefinitions.Add(row0);
				panel1.RowDefinitions.Add(row1);

				ColumnDefinition col0 = new ColumnDefinition();
				ColumnDefinition col1 = new ColumnDefinition();
				ColumnDefinition col2 = new ColumnDefinition();
				ColumnDefinition col3 = new ColumnDefinition();
				ColumnDefinition col4 = new ColumnDefinition();
				ColumnDefinition col5 = new ColumnDefinition();
				ColumnDefinition col6 = new ColumnDefinition();
				ColumnDefinition col7 = new ColumnDefinition();
				col0.Width = new GridLength(8, GridUnitType.Pixel);
				col1.Width = new GridLength(14, GridUnitType.Pixel);
				col2.Width = new GridLength(559, GridUnitType.Pixel);
				col3.Width = new GridLength(118, GridUnitType.Pixel);
				col4.Width = new GridLength(42, GridUnitType.Pixel);
				col5.Width = new GridLength(63, GridUnitType.Pixel);
				col6.Width = new GridLength(30, GridUnitType.Pixel);
				col7.Width = new GridLength(42, GridUnitType.Pixel);
				panel1.ColumnDefinitions.Add(col0);
				panel1.ColumnDefinitions.Add(col1);
				panel1.ColumnDefinitions.Add(col2);
				panel1.ColumnDefinitions.Add(col3);
				panel1.ColumnDefinitions.Add(col4);
				panel1.ColumnDefinitions.Add(col5);
				panel1.ColumnDefinitions.Add(col6);
				panel1.ColumnDefinitions.Add(col7);

				TextBlock alertLabel = new TextBlock();
				alertLabel.SetValue(Grid.ColumnProperty, 1);
				alertLabel.Margin = new Thickness(0, 0, 0, 0);
				alertLabel.Padding = new Thickness(0, 5, 0, 0);
				alertLabel.VerticalAlignment = System.Windows.VerticalAlignment.Top;
				alertLabel.Text = "\u25C6";
				alertLabel.Foreground = Brushes.Red;
				alertLabel.FontSize = 11;
				alertLabel.Visibility = (alert.HasNotification()) ? Visibility.Visible : Visibility.Hidden;
				panel1.Children.Add(alertLabel); // child 1 - red diamond

				Label label1 = new Label();
				label1.SetValue(Grid.ColumnProperty, 2);
				label1.Content = alert.Title;
				label1.Padding = new Thickness(4, 5, 0, 0);
				label1.Margin = new Thickness(0);
				label1.Foreground = Brushes.Gray;
				panel1.Children.Add(label1); // child 2 - alert title

				StackPanel panel2 = new StackPanel();
				panel2.Margin = new Thickness(0, 5, 0, 0);
				panel2.SetValue(Grid.ColumnProperty, 3);
				panel2.Orientation = Orientation.Horizontal;  // time of last

				TextBlock lastTimeText = new TextBlock();
				lastTimeText.Text = "LAST:";
				lastTimeText.Padding = new Thickness(0, 0, 4, 0);
				lastTimeText.Background = Brushes.Transparent;
				lastTimeText.Foreground = Brushes.Gray;
				lastTimeText.FontSize = fontSize;  //

				TextBlock nextTimeText = new TextBlock();
				nextTimeText.Text = "NEXT:";
				nextTimeText.Background = Brushes.Transparent;
				nextTimeText.Foreground = Brushes.Gray;
				nextTimeText.FontSize = fontSize;
				nextTimeText.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
				nextTimeText.Cursor = Cursors.Hand;
				nextTimeText.MouseDown += new MouseButtonEventHandler(nextTime_MouseDown);

				panel2.Children.Add(lastTimeText);
				panel2.Children.Add(nextTimeText);
				panel2.Visibility = (alert.Scanable) ? Visibility.Visible : Visibility.Collapsed;
				panel1.Children.Add(panel2); // child 3 - scan times

				panel2 = new StackPanel();
				panel2.Margin = new Thickness(0, 0, 0, 0);
				panel2.SetValue(Grid.ColumnProperty, 4);
				panel2.Orientation = Orientation.Horizontal;

				Label plusButton = new Label();
				plusButton.ToolTip = "Increase the number of Results";
				Image image1 = new Image();
				plusButton.HorizontalContentAlignment = System.Windows.HorizontalAlignment.Left;
				plusButton.Width = 20;
				plusButton.Height = 10;
				plusButton.Margin = new Thickness(0, 0, 0, 0);
				plusButton.Padding = new Thickness(0, 0, 5, 0);
				plusButton.Cursor = Cursors.Hand;
				plusButton.Foreground = Brushes.Gray;
				plusButton.MouseDown += new MouseButtonEventHandler(displayMore_MouseDown);

				Label minusButton = new Label();
				minusButton.ToolTip = "Decrease the number of Results";
				minusButton.HorizontalContentAlignment = System.Windows.HorizontalAlignment.Right;
				minusButton.Width = 20;
				minusButton.Height = 10;
				minusButton.Margin = new Thickness(0, 0, 0, 0);
				minusButton.Padding = new Thickness(5, 0, 0, 0);
				minusButton.Cursor = Cursors.Hand;
				minusButton.Foreground = Brushes.Gray;
				minusButton.MouseDown += new MouseButtonEventHandler(displayLess_MouseDown);


				Label label2 = new Label();
				label2.FontSize = fontSize;
				label2.Background = Brushes.Transparent;
				label2.Foreground = Brushes.Gray;
				label2.Margin = new Thickness(0, 0, 0, 0);
				label2.Padding = new Thickness(2, 0, 2, 0);
				label2.Content = alert.RowCount.ToString();

				panel2.Children.Add(minusButton);
				panel2.Children.Add(label2);
				panel2.Children.Add(plusButton);
				panel2.Visibility = (alert.ViewType == Alert.AlertViewType.Spreadsheet) ? Visibility.Collapsed : Visibility.Visible;
				panel1.Children.Add(panel2); // child 4 = number of cells

				panel2 = new StackPanel();
				panel2.Margin = new Thickness(0, 0, 0, 0);
				panel2.SetValue(Grid.ColumnProperty, 5);
				panel2.Orientation = Orientation.Horizontal;

				plusButton = new Label();
				plusButton.ToolTip = "Move Row Up";
				plusButton.FontFamily = new FontFamily("Segoe MDL2 Assets");
				plusButton.Content = "\uE74a";
				plusButton.HorizontalContentAlignment = System.Windows.HorizontalAlignment.Right;
				plusButton.FontWeight = FontWeights.SemiBold;
				plusButton.Width = 20;
				plusButton.Height = 10;
				plusButton.Margin = new Thickness(0, 0, 0, 0);
				plusButton.Padding = new Thickness(0, 0, 0, 0);
				plusButton.Cursor = Cursors.Hand;
				plusButton.Foreground = Brushes.White;
				plusButton.MouseDown += new MouseButtonEventHandler(moveup_MouseDown);
				plusButton.MouseEnter += Mouse_Enter;
				plusButton.MouseLeave += Mouse_Leave;

				minusButton = new Label();
				minusButton.ToolTip = "Move Row Down";
				minusButton.FontFamily = new FontFamily("Segoe MDL2 Assets");
				minusButton.Content = "\uE74b";
				minusButton.FontWeight = FontWeights.SemiBold;
				minusButton.HorizontalContentAlignment = System.Windows.HorizontalAlignment.Left;
				minusButton.Width = 20;
				minusButton.Height = 10;
				minusButton.Margin = new Thickness(0, 0, 0, 0);
				minusButton.Padding = new Thickness(0, 0, 0, 0);
				minusButton.Cursor = Cursors.Hand;
				minusButton.Foreground = Brushes.White;
				minusButton.MouseDown += new MouseButtonEventHandler(movedn_MouseDown);
				minusButton.MouseEnter += Mouse_Enter;
				minusButton.MouseLeave += Mouse_Leave;

				label2 = new Label();
				label2.FontSize = fontSize;
				label2.Background = Brushes.Transparent;
				label2.Foreground = new SolidColorBrush(Color.FromRgb(0xA0, 0xA0, 0xA0));
				label2.HorizontalContentAlignment = System.Windows.HorizontalAlignment.Center;
				label2.Margin = new Thickness(0, 5, 0, 0);
				label2.Padding = new Thickness(0, 0, 0, 0);
				label2.Content = "ROW";

				panel2.Children.Add(minusButton);
				panel2.Children.Add(label2);
				panel2.Children.Add(plusButton);

				panel1.Children.Add(panel2);  // child 5 = move row

				Label button3 = new Label();
				button3.ToolTip = "Edit";
				button3.SetValue(Grid.ColumnProperty, 6);
				button3.FontFamily = new FontFamily("Segoe MDL2 Assets");
				button3.Content = "\uE70F";
				button3.HorizontalAlignment = HorizontalAlignment.Right;
				button3.Foreground = Brushes.White;
				button3.Margin = new Thickness(0, 5, 0, 0);
				button3.Padding = new Thickness(4, 0, 2, 0);
				button3.FontSize = fontSize;
				button3.MouseDown += new MouseButtonEventHandler(editScan_MouseDown);
				button3.MouseEnter += Mouse_Enter;
				button3.MouseLeave += Mouse_Leave;
				button3.Cursor = Cursors.Hand;
				button3.Visibility = (alert.Editable) ? Visibility.Visible : Visibility.Collapsed;
				panel1.Children.Add(button3); // child 6 = edit button

				Label button4 = new Label();
				button4.ToolTip = "Delete";
				button4.SetValue(Grid.ColumnProperty, 7);
				button4.FontFamily = new FontFamily("Segoe MDL2 Assets");
				button4.Content = "\uE711";
				button4.HorizontalAlignment = HorizontalAlignment.Center;
				button4.Foreground = Brushes.White;
				button4.Margin = new Thickness(0, 5, 0, 0);
				button4.Padding = new Thickness(4, 0, 2, 0);
				button4.FontSize = fontSize;
				button4.MouseDown += new MouseButtonEventHandler(deleteScan_MouseDown);
				button4.MouseEnter += Mouse_Enter;
				button4.MouseLeave += Mouse_Leave;
				button4.Cursor = Cursors.Hand;
				button4.Visibility = (alert.Deleteable) ? Visibility.Visible : Visibility.Collapsed;
				panel1.Children.Add(button4); // child 7 = delete

				Rectangle line1 = new Rectangle();
				line1.SetValue(Grid.ColumnProperty, 0);
				line1.SetValue(Grid.ColumnSpanProperty, 9);
				line1.SetValue(Grid.RowProperty, 1);
				line1.Margin = new Thickness(0, 0, 0, 0);
				line1.Fill = (SolidColorBrush)new BrushConverter().ConvertFromString("#124B72");
				line1.Height = 0;
				line1.Width = 894;
				panel1.Children.Add(line1);  // child 8 - space between rows

				panel1.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;

				panel1.ContextMenu = new ContextMenu();

				panel1.ContextMenu.Foreground = Brushes.White;

				AlertMenuItem menuItem1 = new AlertMenuItem(alert, new DateTime());
				menuItem1.Header = "Replicate";
				menuItem1.FontSize = fontSize;
				menuItem1.Click += replicate_click;
				menuItem1.Foreground = Brushes.White;
				panel1.ContextMenu.Items.Add(menuItem1);

				alert.TreeViewItem.Header = border;
				alert.TreeViewItem.FontSize = fontSize;

				Tree.Items.Add(alert.TreeViewItem);
			}

			updateThisAlert(alert);
		}

		void item_Collapsed(object sender, RoutedEventArgs e)
		{
			setExpanded(sender, false);
		}

		void item_Expanded(object sender, RoutedEventArgs e)
		{
			setExpanded(sender, true);
		}

		void setExpanded(object sender, bool value)
		{
			TreeViewItem item = sender as TreeViewItem;
			List<Alert> alerts = Alert.Manager.Alerts;
			foreach (Alert alert in alerts)
			{
				if (alert.TreeViewItem == item)
				{
					alert.IsExpanded = value;
					break;
				}
			}
		}

		void displayLess_MouseDown(object sender, MouseButtonEventArgs e)
		{
			changeRowCount(sender, -1);
			e.Handled = true;
		}

		void displayMore_MouseDown(object sender, MouseButtonEventArgs e)
		{
			changeRowCount(sender, 1);
			e.Handled = true;
		}

		private void changeRowCount(object sender, int amount)
		{
			Label label = sender as Label;
			StackPanel panel1 = label.Parent as StackPanel;
			Label rowCountLabel = panel1.Children[1] as Label;
			Grid panel2 = panel1.Parent as Grid;
			Border border = panel2.Parent as Border;
			TreeViewItem item = border.Parent as TreeViewItem;

			List<Alert> alerts = Alert.Manager.Alerts;
			foreach (Alert alert in alerts)
			{
				if (alert.TreeViewItem == item)
				{
					int oldRowCount = alert.RowCount;
					int newRowCount = oldRowCount + amount;
					if (newRowCount < 1)
					{
						newRowCount = 1;
					}
					else if (newRowCount > 10)
					{
						newRowCount = 10;
					}
					if (newRowCount != oldRowCount)
					{
						alert.RowCount = newRowCount;
						rowCountLabel.Content = newRowCount.ToString();
						updateThisAlert(alert);
					}
					break;
				}
			}
		}

		void editScan_MouseDown(object sender, MouseButtonEventArgs e)
		{
			try
			{
				// Always get a *fresh or live* dialog instance
				var dlg = EnsureScanDialog(this);

				// Find the clicked item's TreeViewItem
				if (sender is Label label &&
					label.Parent is Grid panel &&
					panel.Parent is Border border &&
					border.Parent is TreeViewItem item)
				{
					var alerts = Alert.Manager.Alerts;

					lock (alerts)
					{
						foreach (var alert in alerts)
						{
							if (alert.TreeViewItem == item)
							{
								if (alert is ConditionAlert scanAlert)
								{
									var scan = scanAlert.Scan;

									// Populate dialog fields
									dlg.ConditionAlert = scanAlert;
									dlg.PortfolioText = scan.PortfolioName;
									dlg.Condition = scan.Condition;
									dlg.NotificationOn = alert.NotificationOn;
									dlg.UseOnTop = alert.UseOnTop;
									dlg.RunIntervalMinutes = alert.RunInterval;
									dlg.TextColor1 = scanAlert.TextColor1;
									dlg.TextColor2 = scanAlert.TextColor2;
									dlg.TextColor3 = scanAlert.TextColor3;
									dlg.ViewType = scanAlert.ViewType;
									dlg.UseDefaultTitle = scan.HasDefaultTitle();
									dlg.Title = scan.Title;
								}
								break;
							}
						}
					}
				}

				// IMPORTANT: show/activate the window — do NOT set Visibility directly
				if (!dlg.IsVisible)
					dlg.Show();          // or dlg.ShowDialog() if you need modal

				if (dlg.WindowState == WindowState.Minimized)
					dlg.WindowState = WindowState.Normal;

				dlg.Activate();
			}
			catch (Exception ex)
			{
				// Log as appropriate for your app
				System.Diagnostics.Debug.WriteLine(ex);
			}
		}
		void deleteScan_MouseDown(object sender, MouseButtonEventArgs e)
		{
			MessageBoxResult result = MessageBox.Show("Are you sure you wish to delete this Alert?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
			if (result == MessageBoxResult.Yes)
			{
				Label label = sender as Label;
				Grid panel = label.Parent as Grid;
				Border border = panel.Parent as Border;

				TreeViewItem item = border.Parent as TreeViewItem;
				List<Alert> alerts = Alert.Manager.Alerts;
				foreach (Alert alert in alerts)
				{
					if (alert.TreeViewItem == item)
					{
						if (Alert.Manager.IsExampleAlert(alert))
						{
							RestoreExamplesButton.Visibility = Visibility.Visible;
						}
						Alert.Manager.RemoveAlert(alert);
						MainView.DeleteUserData(@"alerts\" + alert.Title);
						break;
					}
				}
				Tree.Items.Remove(item);

				bool removeFlash = true;
				foreach (Alert alert in alerts)
				{
					if (alert.HasNotification())
					{
						removeFlash = false;
						break;
					}
				}
				if (removeFlash)
				{
					Flash.Manager.RemoveFlash(AlertButton);
				}
			}
		}

		void moveup_MouseDown(object sender, MouseButtonEventArgs e)
		{
			Label label = sender as Label;
			StackPanel panel1 = label.Parent as StackPanel;
			Grid panel2 = panel1.Parent as Grid;
			Border border = panel2.Parent as Border;
			TreeViewItem treeViewItem1 = border.Parent as TreeViewItem;

			int index1 = findAlertIndex(treeViewItem1);
			int index2 = findHigherAlertIndex(index1);

			if (index2 >= 0)
			{
				List<Alert> alerts = Alert.Manager.Alerts;

				Alert alert1 = alerts[index1];
				Alert alert2 = alerts[index2];

				TreeViewItem treeViewItem2 = alert2.TreeViewItem;

				alert2.TreeViewItem = treeViewItem1;
				alert1.TreeViewItem = treeViewItem2;

				alerts[index2] = alert1;
				alerts[index1] = alert2;

				updateThisAlert(alert1);
				updateThisAlert(alert2);

				updateAlertUI(alert1);
				updateAlertUI(alert2);
			}
		}

		void movedn_MouseDown(object sender, MouseButtonEventArgs e)
		{
			Label label = sender as Label;
			StackPanel panel1 = label.Parent as StackPanel;
			Grid panel2 = panel1.Parent as Grid;
			Border border = panel2.Parent as Border;
			TreeViewItem item1 = border.Parent as TreeViewItem;

			int index1 = findAlertIndex(item1);
			int index2 = findLowerAlertIndex(index1);

			if (index2 >= 0)
			{
				List<Alert> alerts = Alert.Manager.Alerts;

				Alert alert1 = alerts[index1];
				Alert alert2 = alerts[index2];

				TreeViewItem item2 = alert2.TreeViewItem;

				alert2.TreeViewItem = item1;
				alert1.TreeViewItem = item2;

				alerts[index2] = alert1;
				alerts[index1] = alert2;

				updateThisAlert(alert1);
				updateThisAlert(alert2);

				updateAlertUI(alert1);
				updateAlertUI(alert2);
			}
		}

		private int findAlertIndex(TreeViewItem item)
		{
			int index = -1;

			int idx = 0;
			List<Alert> alerts = Alert.Manager.Alerts;
			foreach (Alert alert in alerts)
			{
				if (alert.TreeViewItem == item)
				{
					index = idx;
					break;
				}
				idx++;
			}
			return index;
		}

		private int findHigherAlertIndex(int index1)
		{
			int index2 = -1;
			if (index1 >= 0)
			{
				List<Alert> alerts = Alert.Manager.Alerts;
				for (int index = index1 - 1; index >= 0; index--)
				{
					if (_alertType == AlertType.ConditionAlert)
					{
						ConditionAlert alert = alerts[index] as ConditionAlert;
						if (alert != null)
						{
							index2 = index;
							break;
						}
					}

					if (_alertType == AlertType.PositionAlert)
					{
						PositionAlert alert = alerts[index] as PositionAlert;
						if (alert != null)
						{
							index2 = index;
							break;
						}
					}

					if (_alertType == AlertType.CommentAlert)
					{
						CommentAlert alert = alerts[index] as CommentAlert;
						if (alert != null)
						{
							index2 = index;
							break;
						}
					}
				}
			}
			return index2;
		}

		private int findLowerAlertIndex(int index1)
		{
			int index2 = -1;
			if (index1 >= 0)
			{
				List<Alert> alerts = Alert.Manager.Alerts;
				for (int index = index1 + 1; index < alerts.Count; index++)
				{
					if (_alertType == AlertType.ConditionAlert)
					{
						ConditionAlert alert = alerts[index] as ConditionAlert;
						if (alert != null)
						{
							index2 = index;
							break;
						}
					}

					if (_alertType == AlertType.PositionAlert)
					{
						PositionAlert alert = alerts[index] as PositionAlert;
						if (alert != null)
						{
							index2 = index;
							break;
						}
					}

					if (_alertType == AlertType.CommentAlert)
					{
						CommentAlert alert = alerts[index] as CommentAlert;
						if (alert != null)
						{
							index2 = index;
							break;

						}
					}
				}
			}
			return index2;
		}


		private string _modelName = "";
		Navigation nav = new Navigation();


		private void ML_MouseDown(object sender, MouseButtonEventArgs e)
		{
			if (NavScroller1.Visibility == Visibility.Collapsed)
			{
				//    hideView();
				//    hideNavigation();

				//    PortfolioSelectionPanel.Visibility = Visibility.Visible;
				NavScroller1.Visibility = Visibility.Visible;

				var modelNames = MainView.getModelNames();
				modelNames.Insert(0, "NO PREDICTION");


				nav.setNavigation(NavCol1, Model_MouseDown, modelNames.ToArray());
				highlightButton(NavCol1, _modelName);
			}
			else
			{
				//    showView();
				NavScroller1.Visibility = Visibility.Collapsed;
			}
		}

		private void highlightButton(StackPanel panel, string selectedButton, bool foregroundHighlight = true)
		{
			if (selectedButton.Length > 0)
			{
				Brush brush1 = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));  // white
				Brush brush2 = new SolidColorBrush(Color.FromRgb(0x12, 0x4b, 0x72));  // highlights text on start up of Monitor. that needs to be 0x00, 0xcc, 0xff
				Brush brush3 = new SolidColorBrush(Colors.Transparent);
				Brush brush4 = new SolidColorBrush(Color.FromRgb(0x40, 0x40, 0x40));  // darker gray
				Brush brush5 = new SolidColorBrush(Color.FromRgb(0x00, 0x00, 0x00));  // black                                                                                     
				Brush brush6 = new SolidColorBrush(Color.FromRgb(0x00, 0xcc, 0xff));  // button text blue


				string[] field = selectedButton.Split(':');
				string symbol = field[0];
				string text = (field.Length == 2) ? field[1] : field[0];

				foreach (UIElement item in panel.Children)
				{
					var element = item;
					var grid = element as Grid;
					if (grid != null)
					{
						element = grid.Children[0];
					}

					var textBlock = element as TextBlock;
					if (textBlock != null)
					{
						if (foregroundHighlight)
						{
							textBlock.Tag = brush6;
							textBlock.Foreground = (text == textBlock.Text) ? brush6 : brush1;
							textBlock.VerticalAlignment = VerticalAlignment.Center;
						}
						else
						{
							textBlock.Background = (text == textBlock.Text) ? brush2 : brush5;  //brush4 : brush3;
							textBlock.VerticalAlignment = VerticalAlignment.Center;
						}
					}

					var label = element as Label;
					if (label != null)
					{
						var labelText = label.Content as string;
						if (foregroundHighlight)
						{
							label.Tag = brush6;
							label.Foreground = (text == labelText) ? brush6 : brush1;
							label.VerticalAlignment = VerticalAlignment.Center;
						}
						else
						{
							label.Background = (text == labelText) ? brush6 : brush5;  //brush4 : brush3;
							label.VerticalAlignment = VerticalAlignment.Center;
						}
					}
				}
			}
		}

		private void updateModelInfo()
		{
			var model = MainView.GetModel(_modelName);
			var modelName = (model != null) ? _modelName : "";
			var scenario = (model != null) ? MainView.GetSenarioLabel(model.Scenario) : "";
			ScenarioModel.Content = modelName;
			ScenarioName.Content = scenario;
		}

		private void Model_MouseDown(object sender, MouseButtonEventArgs e)
		{
			Label label = e.Source as Label;
			var text = label.Content as string;

			if (text != _modelName)
			{
				_modelName = text;

				updateModelInfo();

				//if (_symbolsRemaining.Count == 0)
				//{
				//    clearForecastCells();
				//    startForecasting();
				//}

				//hideNavigation();
				//showView();
				NavScroller1.Visibility = Visibility.Collapsed;
				if (_chart3 != null)
				{
					changeChart(_chart3.Symbol, _chart3.Interval, true, _modelName);
				}
			}
		}

		void setChartModel(string modelName)
		{
			var modelNames = new List<string>();
			modelNames.Add(modelName);
			if (_chart1 != null)
			{
				_chart1.ModelNames = modelNames;
				_chart2.ModelNames = modelNames;
				_chart3.ModelNames = modelNames;
			}
		}

		void RestoreExamples_MouseDown(object sender, MouseButtonEventArgs e)
		{
			Alert.Manager.AddExampleAlerts();
			RestoreExamplesButton.Visibility = Visibility.Hidden;

			List<Alert> alerts = Alert.Manager.Alerts;
			foreach (Alert alert in alerts)
			{
				ConditionAlert conditionAlert = alert as ConditionAlert;
				if (conditionAlert != null && conditionAlert.TreeViewItem == null)
				{
					addAlertUI(conditionAlert);
					addAlertEvents(alert);
				}
			}
		}

		private void alert_ProgressEvent(object sender, EventArgs e)
		{
			Alert alert = sender as Alert;
			updateThisAlert(alert);
		}

		private void alert_FlashEvent(object sender, FlashEventArgs e)
		{
			Alert alert = sender as Alert;

			if (alert.HasNotification())
			{
				if (e.Type == FlashEventArgs.EventType.NewCondition)
				{
					if (alert.UseOnTop)
					{
						this.Dispatcher.Invoke(DispatcherPriority.Normal, (Action)delegate () { _mainView.Activate(); });
					}
					_addConditionFlash = true;
				}
				else if (e.Type == FlashEventArgs.EventType.NewPosition)
				{
					_addPositionFlash = true;
				}
				else if (e.Type == FlashEventArgs.EventType.NewComment)
				{
					_addCommentFlash = true;
				}
			}

			updateThisAlert(alert);
		}

		private void updateThisAlert(Alert alert)
		{
			lock (_updateAlerts)
			{
				_updateAlerts.Remove(alert);
				_updateAlerts.Add(alert);
			}
		}

		private void timer_Tick(object sender, EventArgs e)
		{

			if (_updateCalculator && _chart3 != null)
			{
				_updateCalculator = false;

				string symbol = _chart3.Symbol;
				string[] tickers = symbol.Split(Symbol.SpreadCharacter);
				symbol = tickers[0];

				string sectorSymbol = _chart3.SectorSymbol;
				string sectorETFSymbol = _chart3.SectorETFSymbol;
				double symbolPrice = _chart3.SymbolPrice;
				double sectorPrice = _chart3.SectorPrice;
				double sectorETFPrice = _chart3.SectorETFPrice;
				double symbolBeta = _chart3.SymbolBeta;
				double sectorBeta = _chart3.SectorBeta;
				double sectorETFBeta = _chart3.SectorETFBeta;

				string settings = MainView.CalculatorSettings;
				if (settings == null) settings = "";
				string[] fields = settings.Split(';');
				int count = (settings.Length == 0) ? 0 : fields.Length;
				double portfolioBalance = (count >= 1) ? double.Parse(fields[0]) : 1000000;
				double portfolioPercent = (count >= 2) ? double.Parse(fields[1]) : 1;
				bool useBeta = (count >= 3) ? bool.Parse(fields[2]) : true;
				bool usePricePercent = (count >= 4) ? bool.Parse(fields[3]) : true;
				double pricePercent = (count >= 5) ? double.Parse(fields[4]) : 25;
				string priceStopText = InvestmentAmt.Text;
				bool useFixedDollar = (count >= 6) ? bool.Parse(fields[5]) : false;
				double fixedDollarAmount = (count >= 7) ? double.Parse(fields[6]) : 0;

				double priceStop = 0;
				try
				{
					priceStop = (priceStopText.Length == 0) ? 0 : double.Parse(priceStopText);
				}
				catch
				{
					priceStop = 0;
				}

				double beta1 = useBeta ? symbolBeta : 1;
				double beta2 = useBeta ? sectorBeta : 1;
				double beta3 = useBeta ? sectorETFBeta : 1;

				double rValue = useFixedDollar ? fixedDollarAmount : portfolioBalance * portfolioPercent / 100;
				double percent = usePricePercent ? pricePercent : 100 * Math.Abs(symbolPrice - priceStop) / symbolPrice;
				double riskFactor = 100 / percent;
				double investment = rValue * riskFactor;

				double symbolShares = Math.Round(investment / beta1 / symbolPrice);
				double sectorShares = Math.Round(investment / beta2 / sectorPrice);
				double sectorETFShares = Math.Round(investment / beta3 / sectorETFPrice);

				InvestmentAmt.Visibility = usePricePercent ? Visibility.Collapsed : Visibility.Visible;
				StopPx.Visibility = usePricePercent ? Visibility.Collapsed : Visibility.Visible;
				InvestmentAmt.Text = priceStopText;


				string[] fields1 = symbol.Split(' ');
				string[] fields2 = sectorSymbol.Split(' ');
				string[] fields3 = sectorETFSymbol.Split(' ');
				bool usEquity = (fields1.Length > 1 && (fields1[1] == "US" || fields1[1] == "UN" || fields1[1] == "UW"));

				StkSymbol.Text = fields1[0];
				StkShares.Text = (double.IsNaN(symbolShares)) ? "" : symbolShares.ToString("N0") + " sh";
				SectorETFSymbol.Text = usEquity ? fields2[0] : "";
				SectorETFShares.Text = (double.IsNaN(sectorShares) || !usEquity) ? "" : sectorShares.ToString("N0") + " sh";
				IndexETFSymbol.Text = usEquity ? fields3[0] : "";
				IndexETFShares.Text = (double.IsNaN(sectorETFShares) || !usEquity) ? "" : sectorETFShares.ToString("N0") + " sh";

			}

			Alert alert = null;

			updateAlertTimes();

			lock (_updateAlerts)
			{
				if (_updateAlerts.Count > 0)
				{
					alert = _updateAlerts[0];
					_updateAlerts.Remove(alert);
				}
			}

			if (alert != null)
			{
				try
				{
					updateAlert(alert);
				}
				catch (System.Exception ex)
				{
					string message = ex.Message;
				}
			}

			if (_addConditionFlash)
			{
				_addConditionFlash = false;

				Brush alertColor = Brushes.Red;
				Flash.Manager.AddFlash(new Flash(AlertButton, AlertButton.Foreground, alertColor, 10, false));
			}

			if (_addPositionFlash)
			{
				_addPositionFlash = false;

				Brush alertColor = Brushes.Red;
				Flash.Manager.AddFlash(new Flash(AlertButton, AlertButton.Foreground, alertColor, 10, false));
			}

			if (_addCommentFlash)
			{
				_addCommentFlash = false;

				Brush alertColor = Brushes.Red;
				Flash.Manager.AddFlash(new Flash(AlertButton, AlertButton.Foreground, alertColor, 10, false));
			}

			int count1 = _barCache.GetRequestCount();
			if (count1 == 0)
			{
				RequestCount1.Visibility = Visibility.Visible;
				RequestCount2.Visibility = Visibility.Hidden;
				RequestSymbol.Visibility = Visibility.Hidden;
			}
			else
			{
				RequestCount1.Visibility = Visibility.Visible;
				RequestCount2.Visibility = Visibility.Visible;
				RequestSymbol.Visibility = Visibility.Visible;
				RequestCount2.Text = count1.ToString();
				RequestSymbol.Text = _barCache.GetRequestSymbol();
			}
		}

		private void updateAlert(Alert alert)
		{
			if (alert.ViewType == Alert.AlertViewType.Spreadsheet)
			{
				updateSpreadsheetAlert(alert);
			}
			else if (alert.ViewType == Alert.AlertViewType.DateTree)
			{
				updateDateTreeAlert(alert);
			}
			else
			{
			}

			updateAlertPortfolio(alert);
		}

		private List<double> getScrollOffsets(Alert alert)
		{
			List<double> offsets = new List<double>();
			if (alert.TreeViewItem.Items != null && alert.TreeViewItem.Items.Count > 0)
			{
				Grid grid = alert.TreeViewItem.Items[0] as Grid;
				for (int ii = 0; ii < _columnCount; ii++)
				{
					ScrollViewer scrollViewer = LogicalTreeHelper.FindLogicalNode(grid, "ScrollViewer" + ii.ToString()) as ScrollViewer;
					if (scrollViewer != null)
					{
						offsets.Add(scrollViewer.VerticalOffset);
					}
				}
			}
			return offsets;
		}

		private void setScrollOffsets(Alert alert, List<double> offsets)
		{
			if (alert.TreeViewItem.Items != null && alert.TreeViewItem.Items.Count > 0)
			{
				Grid grid = alert.TreeViewItem.Items[0] as Grid;
				for (int ii = 0; ii < _columnCount; ii++)
				{
					ScrollViewer scrollViewer = LogicalTreeHelper.FindLogicalNode(grid, "ScrollViewer" + ii.ToString()) as ScrollViewer;
					if (scrollViewer != null)
					{
						try
						{
							double offset = (ii < offsets.Count) ? offsets[ii] : 0;
							scrollViewer.ScrollToVerticalOffset(offset);
						}
						catch (Exception)
						{
						}
					}
				}
			}
		}

		private void updateAlertPortfolio(Alert alert)
		{
			List<DateTime> dateTimes = alert.GetDateTimes();
			List<AlertItem> data = (dateTimes.Count > 0) ? alert.GetItems(dateTimes[0]) : new List<AlertItem>();

			StringBuilder sb = new StringBuilder();
			foreach (var item in data)
			{
				sb.Append(item.ToString() + "\n");
			}
			var path = @"alerts\" + alert.Title;
			MainView.SaveUserData(path, sb.ToString());
		}

		private bool isCQGPortfolio(List<Symbol> symbols)
		{
			return symbols.Count > 0 && Portfolio.isCQGSymbol(symbols[0].Ticker);
		}

		private bool isYieldDescription(string input)
		{
			var output = false;
			var fields = input.Trim().Replace("  ", " ").Split(' ');
			if (fields.Length == 2)
			{
				var ok1 = (Char.IsDigit(fields[0].First()) && (Char.IsDigit(fields[0].Last()) || fields[0].Last() == 'M'));
				var ok2 = (Char.IsDigit(fields[1].First()) && (Char.IsDigit(fields[1].Last()) || fields[1].Last() == 'M'));
				output = ok1 && ok2;
			}
			return output;
		}

		private void updateSpreadsheetAlert(Alert alert)
		{
			if (alert.TreeViewItem != null)
			{
				List<double> verticalOffsets = getScrollOffsets(alert);
				alert.TreeViewItem.Items.Clear();

				List<DateTime> dateTimes = alert.GetDateTimes();
				int count = Math.Min(dateTimes.Count, _columnCount);

				int maxRowNum = 0;
				for (int col = 0; col < count; col++)
				{
					List<AlertItem> results = alert.GetItems(dateTimes[col]);
					int rowNum = 0;
					int[] counts = { 0, 0 };
					foreach (AlertItem result in results)
					{
						counts[result.Side]++;

						if (rowNum < counts[0] || rowNum < counts[1])
						{
							rowNum++;
						}
					}
					maxRowNum = Math.Max(rowNum, maxRowNum);
				}
				maxRowNum = Math.Min(15, maxRowNum);

				alert.LeftArrows.Clear();
				alert.RightArrows.Clear();

				if (count > 0)
				{
					int fontSize = 10;   // date and count label text size

					Border border1 = alert.TreeViewItem.Header as Border;
					Grid grid1 = border1.Child as Grid;

					TextBlock alertLabel1 = grid1.Children[0] as TextBlock;
					alertLabel1.Visibility = (alert.HasNotification()) ? Visibility.Visible : Visibility.Hidden;

					Grid grid = new Grid();

					RowDefinition row0 = new RowDefinition();
					RowDefinition row1 = new RowDefinition();
					RowDefinition row2 = new RowDefinition();
					grid.RowDefinitions.Add(row0);
					grid.RowDefinitions.Add(row1);
					grid.RowDefinitions.Add(row2);

					string info = alert.GetInfoLine();
					Border border = new Border();
					border.Background = Brushes.Transparent;
					border.BorderBrush = Brushes.SteelBlue;  //new SolidColorBrush(Color.FromRgb(0x18, 0x31, 0x52)
					border.Opacity = 0.65;
					border.BorderThickness = new Thickness(0, 0, 0, 1);
					border.Margin = new Thickness(0, 4, 0, 10);
					border.Width = 525;
					border.SetValue(Grid.ColumnSpanProperty, _columnCount);

					Label label = new Label();
					label.Content = info;
					label.Margin = new Thickness(0, 0, 0, 0);
					label.Padding = new Thickness(30, 1, 0, 7);
					label.FontSize = fontSize;
					label.Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 255));  //216, 216, 216
					label.ContextMenu = new ContextMenu();
					label.ContextMenu.Foreground = Brushes.White;

					border.Child = label;

					border.Visibility = (info.Length > 0) ? Visibility.Visible : Visibility.Collapsed;
					grid.Children.Add(border);

					int colCount = Math.Min(dateTimes.Count, _columnCount);
					for (int col = 0; col < colCount; col++)
					{
						DateTime dateTime = dateTimes[col];

						ColumnDefinition col0 = new ColumnDefinition();
						col0.Width = new GridLength(100, GridUnitType.Auto);
						grid.ColumnDefinitions.Add(col0);

						string timeText = alert.GetTimeText(dateTime);


						StackPanel panel1 = new StackPanel();
						panel1.Orientation = Orientation.Horizontal;
						panel1.HorizontalAlignment = HorizontalAlignment.Center;

						AlertLabel leftArrow = new AlertLabel(alert);
						leftArrow.Margin = new Thickness(0, 0, 0, 0);
						leftArrow.Padding = new Thickness(0, 1, 10, 0);
						leftArrow.VerticalAlignment = System.Windows.VerticalAlignment.Top;
						leftArrow.VerticalContentAlignment = System.Windows.VerticalAlignment.Top;
						leftArrow.Visibility = Visibility.Hidden;
						leftArrow.MouseDown += SignalLeft_MouseDown;
						panel1.Children.Add(leftArrow);

						alert.LeftArrows.Add(leftArrow);

						// Red Diamond Alert
						TextBlock alertLabel2 = new TextBlock();
						alertLabel2.Margin = new Thickness(0, 0, 0, 0);
						alertLabel2.Padding = new Thickness(0, 0, 0, 0);
						alertLabel2.VerticalAlignment = System.Windows.VerticalAlignment.Top;
						alertLabel2.Text = "";  //"\u25C6"
						alertLabel2.Foreground = Brushes.Red;
						alertLabel2.FontSize = 11;
						alertLabel2.Visibility = (alert.HasNotification(dateTime)) ? Visibility.Visible : Visibility.Hidden;
						panel1.Children.Add(alertLabel2);

						// Data of each scan

						Label label1 = new Label();
						label1.Content = timeText;
						label1.Margin = new Thickness(0, 0, 0, 0);
						label1.Padding = new Thickness(7, 0, 0, 0);
						label1.FontSize = fontSize;
						label1.Foreground = new SolidColorBrush(Color.FromRgb(152, 251, 152));
						label1.ContextMenu = new ContextMenu();
						label1.ContextMenu.Foreground = Brushes.White;
						//label1.Width = 150;
						label1.HorizontalAlignment = HorizontalAlignment.Center;
						panel1.Children.Add(label1);

						AlertLabel rightArrow = new AlertLabel(alert);
						rightArrow.Margin = new Thickness(0, 0, 30, 0);
						rightArrow.Padding = new Thickness(0, 0, 0, 0);
						rightArrow.VerticalAlignment = System.Windows.VerticalAlignment.Top;
						rightArrow.VerticalContentAlignment = System.Windows.VerticalAlignment.Top;
						rightArrow.Visibility = Visibility.Hidden;
						rightArrow.MouseDown += SignalRight_MouseDown;
						panel1.Children.Add(rightArrow);

						alert.RightArrows.Add(rightArrow);

						int number = Math.Min(count, _columnCount);
						int colNum = Math.Abs(number + (col - alert.ActiveColumn)) % number;

						panel1.SetValue(Grid.ColumnProperty, colNum);
						panel1.SetValue(Grid.RowProperty, 1);
						grid.Children.Add(panel1);

						Grid cell = createSpreadsheetCell(alert, col, dateTime, alertLabel1, alertLabel2, maxRowNum);
						cell.SetValue(Grid.ColumnProperty, colNum);
						cell.SetValue(Grid.RowProperty, 2);
						cell.VerticalAlignment = VerticalAlignment.Top;
						grid.Children.Add(cell);
					}
					alert.TreeViewItem.Items.Add(grid);
				}
				setScrollOffsets(alert, verticalOffsets);
				alert.TreeViewItem.IsExpanded = alert.IsExpanded;
			}
		}

		private void updateDateTreeAlert(Alert alert)
		{
			if (alert.TreeViewItem != null)
			{
				int fontSize = 12;   // date and count label

				alert.TreeViewItem.IsExpanded = alert.IsExpanded;

				Border border = alert.TreeViewItem.Header as Border;
				Grid grid1 = border.Child as Grid;

				TextBlock alertLabel1 = grid1.Children[0] as TextBlock;
				alertLabel1.Visibility = (alert.HasNotification()) ? Visibility.Visible : Visibility.Hidden;

				Label titleLabel = grid1.Children[1] as Label;
				titleLabel.Content = alert.Title;

				List<DateTime> dateTimes = alert.GetDateTimes();
				int rowCount = Math.Min(alert.RowCount, dateTimes.Count);

				bool oldItemsAvailable = false;
				if (alert.TreeViewItem.Items.Count > 0)
				{
					oldItemsAvailable = ((alert.TreeViewItem.Items[0] as TreeViewItem) != null);
				}

				if (oldItemsAvailable)
				{
					List<TreeViewItem> removeItems = new List<TreeViewItem>();
					foreach (TreeViewItem oldItem in alert.TreeViewItem.Items)
					{
						StackPanel panel = oldItem.Header as StackPanel;
						TextBlock label = panel.Children[1] as TextBlock;
						string oldTimeText = label.Text;
						bool found = false;
						for (int ii = 0; ii < rowCount; ii++)
						{
							DateTime dateTime = dateTimes[ii];
							string timeText = alert.GetTimeText(dateTime);
							if (oldTimeText == timeText)
							{
								found = true;
								break;
							}
						}
						if (!found)
						{
							removeItems.Add(oldItem);
						}
					}

					foreach (TreeViewItem oldItem in removeItems)
					{
						alert.TreeViewItem.Items.Remove(oldItem);
					}
				}
				else
				{
					alert.TreeViewItem.Items.Clear();
				}

				for (int ii = 0; ii < rowCount; ii++)
				{
					DateTime dateTime = dateTimes[ii];
					string timeText = alert.GetTimeText(dateTime);

					List<AlertItem> alertItems = alert.GetItems(dateTime);
					int count = alertItems.Count;

					StackPanel panel1 = new StackPanel();
					panel1.Orientation = Orientation.Horizontal;

					TextBlock alertLabel2 = new TextBlock();
					alertLabel2.Margin = new Thickness(0, 0, 0, 0);
					alertLabel2.Padding = new Thickness(0, 0, 0, 0);
					alertLabel2.VerticalAlignment = System.Windows.VerticalAlignment.Top;
					alertLabel2.Text = "\u25C6";
					alertLabel2.Foreground = Brushes.Red;
					alertLabel2.FontSize = 11;
					alertLabel2.Visibility = (alert.HasNotification(dateTime)) ? Visibility.Visible : Visibility.Hidden;
					panel1.Children.Add(alertLabel2);

					TextBlock label1 = new TextBlock();
					label1.Text = timeText;
					label1.Margin = new Thickness(0);
					label1.Padding = new Thickness(0);
					label1.FontSize = fontSize;
					label1.Foreground = new SolidColorBrush(Color.FromRgb(152, 251, 152));
					label1.ContextMenu = new ContextMenu();

					label1.Width = 60;
					panel1.Children.Add(label1);

					TreeViewItem item = new TreeViewItem();
					item.Header = panel1;
					item.HorizontalContentAlignment = System.Windows.HorizontalAlignment.Stretch;

					bool expand = false;
					if (oldItemsAvailable)
					{
						foreach (TreeViewItem oldItem in alert.TreeViewItem.Items)
						{
							StackPanel panel = oldItem.Header as StackPanel;
							TextBlock label = panel.Children[1] as TextBlock;
							string oldTimeText = label.Text;
							if (oldTimeText == timeText)
							{
								expand = oldItem.IsExpanded;
								alert.TreeViewItem.Items.Remove(oldItem);
								break;
							}
						}
					}

					alert.TreeViewItem.Items.Add(item);

					updateSymbols(item, alert, ii, dateTime, alertLabel1, alertLabel2);

					item.IsExpanded = alert.IsExpanded && expand;
				}
			}
		}

		void replicate_click(object sender, RoutedEventArgs e)
		{
			AlertMenuItem item = sender as AlertMenuItem;
			ConditionAlert alert1 = item.Alert as ConditionAlert;
			if (alert1 != null)
			{
				ConditionAlert alert2 = addScanAlert(alert1.Title, alert1.Portfolio, alert1.Condition, alert1.RunInterval);

				alert2.ActiveColumn = alert1.ActiveColumn;
				alert2.NotificationOn = alert1.NotificationOn;
				alert2.RowCount = alert1.RowCount;
				alert2.TextColor1 = alert1.TextColor1;
				alert2.TextColor2 = alert1.TextColor2;
				alert2.TextColor3 = alert1.TextColor3;
				alert2.ViewType = alert1.ViewType;

				updateAlertUI(alert2);
			}
		}

		void updateSymbols(TreeViewItem treeViewItem, Alert alert, int col, DateTime dateTime, TextBlock alertLabel1, TextBlock alertLabel2)
		{
			treeViewItem.Items.Clear();
			Grid grid = createSpreadsheetCell(alert, col, dateTime, alertLabel1, alertLabel2, 0);
			treeViewItem.Items.Add(grid);
		}

		Grid createSpreadsheetCell(Alert alert, int col, DateTime dateTime, TextBlock alertLabel1, TextBlock alertLabel2, int rowCount)
		{
			TradeHorizon tradeHorizon = alert.TradeHorizon;

			bool displayCounts = (alert.GetType() == typeof(ConditionAlert));

			int fontSize = 9;
			int rowHeight = 15;

			// extra column width for asterisk
			int extraColumnWidth = 6;

			PositionAlert tradeAlert = alert as PositionAlert;
			bool colorSymbols = true; // (tradeAlert != null); 

			List<AlertItem> results = alert.GetItems(dateTime);
			int[] sideCount = { 0, 0 };
			foreach (AlertItem result in results)
			{
				if (result.Enable)
				{
					sideCount[result.Side]++;
				}
			}

			Grid cell = new Grid();
			ColumnDefinition col0a = new ColumnDefinition();
			ColumnDefinition col1a = new ColumnDefinition();
			ColumnDefinition col2a = new ColumnDefinition();
			ColumnDefinition col3a = new ColumnDefinition();
			ColumnDefinition col4a = new ColumnDefinition();
			ColumnDefinition col5a = new ColumnDefinition();
			ColumnDefinition col6a = new ColumnDefinition();
			ColumnDefinition col7a = new ColumnDefinition();
			ColumnDefinition col8a = new ColumnDefinition();
			ColumnDefinition col9a = new ColumnDefinition();
			ColumnDefinition col10a = new ColumnDefinition();
			ColumnDefinition col11a = new ColumnDefinition();
			ColumnDefinition col12a = new ColumnDefinition();
			ColumnDefinition col13a = new ColumnDefinition();

			col0a.Width = new GridLength(10, GridUnitType.Pixel);
			col1a.Width = new GridLength(extraColumnWidth, GridUnitType.Pixel);
			col2a.Width = new GridLength(extraColumnWidth, GridUnitType.Pixel);
			col3a.Width = new GridLength(5, GridUnitType.Pixel);
			col4a.Width = new GridLength(5, GridUnitType.Pixel);
			col5a.Width = new GridLength(40, GridUnitType.Pixel);

			col6a.Width = new GridLength(4, GridUnitType.Pixel);

			col7a.Width = new GridLength(10, GridUnitType.Pixel);
			col8a.Width = new GridLength(extraColumnWidth, GridUnitType.Pixel);
			col9a.Width = new GridLength(extraColumnWidth, GridUnitType.Pixel);
			col10a.Width = new GridLength(5, GridUnitType.Pixel);
			col11a.Width = new GridLength(5, GridUnitType.Pixel);
			col12a.Width = new GridLength(40, GridUnitType.Pixel);
			col13a.Width = new GridLength(4, GridUnitType.Pixel);

			cell.ColumnDefinitions.Add(col0a);
			cell.ColumnDefinitions.Add(col1a);
			cell.ColumnDefinitions.Add(col2a);
			cell.ColumnDefinitions.Add(col3a);
			cell.ColumnDefinitions.Add(col4a);
			cell.ColumnDefinitions.Add(col5a);
			cell.ColumnDefinitions.Add(col6a);
			cell.ColumnDefinitions.Add(col7a);
			cell.ColumnDefinitions.Add(col8a);
			cell.ColumnDefinitions.Add(col9a);
			cell.ColumnDefinitions.Add(col10a);
			cell.ColumnDefinitions.Add(col11a);
			cell.ColumnDefinitions.Add(col12a);
			cell.ColumnDefinitions.Add(col13a);

			RowDefinition row = new RowDefinition();
			cell.RowDefinitions.Add(row); // row 0
			StackPanel actionPanel = new StackPanel();
			actionPanel.Orientation = Orientation.Horizontal;
			actionPanel.SetValue(Grid.ColumnProperty, 3);
			actionPanel.SetValue(Grid.RowProperty, 0);
			actionPanel.SetValue(Grid.ColumnSpanProperty, 14);

			cell.Children.Add(actionPanel);

			row = new RowDefinition();
			cell.RowDefinitions.Add(row); // row 1
			for (int side = 0; side < 2; side++)
			{
				string text1 = sideCount[side].ToString();
				string text2 = (side == 0) ? "Buys" : "Sells";

				TextBlock label = new TextBlock();
				label.SetValue(Grid.RowProperty, 1);
				label.Text = displayCounts ? text1 : text2;
				label.Margin = new Thickness(0);
				label.Padding = new Thickness(5, 0, 0, 0);
				label.FontFamily = new FontFamily("Helvetica Neue");
				label.FontWeight = FontWeights.Normal;
				label.Foreground = Brushes.GhostWhite;
				label.FontSize = 10;
				label.SetValue(Grid.ColumnProperty, 7 * side + 4);
				label.SetValue(Grid.RowProperty, 1);
				label.SetValue(Grid.ColumnSpanProperty, 2);
				label.HorizontalAlignment = HorizontalAlignment.Left;
				label.VerticalAlignment = VerticalAlignment.Top;
				cell.Children.Add(label);
			}

			row = new RowDefinition();
			row.Height = new GridLength(1);
			cell.RowDefinitions.Add(row); // row 2

			// horizontal line
			Rectangle line1 = new Rectangle();
			line1.SetValue(Grid.ColumnProperty, 0);
			line1.SetValue(Grid.ColumnSpanProperty, 14);
			line1.SetValue(Grid.RowProperty, 2);
			line1.Fill = new SolidColorBrush(Color.FromRgb(0x18, 0x31, 0x52));
			line1.Height = 1;
			line1.Width = 124;
			cell.Children.Add(line1);

			row = new RowDefinition();
			cell.RowDefinitions.Add(row);

			ScrollViewer scrollView = new ScrollViewer() { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
			scrollView.SetValue(Grid.ColumnProperty, 0);
			scrollView.SetValue(Grid.ColumnSpanProperty, 14);
			scrollView.SetValue(Grid.RowProperty, 3);
			scrollView.Name = "ScrollViewer" + col.ToString();
			cell.Children.Add(scrollView);

			Grid panel = new Grid();
			ColumnDefinition col0b = new ColumnDefinition();
			ColumnDefinition col1b = new ColumnDefinition();
			ColumnDefinition col2b = new ColumnDefinition();
			ColumnDefinition col3b = new ColumnDefinition();
			ColumnDefinition col4b = new ColumnDefinition();
			ColumnDefinition col5b = new ColumnDefinition();
			ColumnDefinition col6b = new ColumnDefinition();
			ColumnDefinition col7b = new ColumnDefinition();
			ColumnDefinition col8b = new ColumnDefinition();
			ColumnDefinition col9b = new ColumnDefinition();
			ColumnDefinition col10b = new ColumnDefinition();
			ColumnDefinition col11b = new ColumnDefinition();
			ColumnDefinition col12b = new ColumnDefinition();
			//ColumnDefinition col13b = new ColumnDefinition();

			col0b.Width = new GridLength(10, GridUnitType.Pixel);
			col1b.Width = new GridLength(extraColumnWidth, GridUnitType.Pixel);
			col2b.Width = new GridLength(extraColumnWidth, GridUnitType.Pixel);
			col3b.Width = new GridLength(5, GridUnitType.Pixel);
			col4b.Width = new GridLength(5, GridUnitType.Pixel);
			col5b.Width = new GridLength(40, GridUnitType.Pixel);

			col6b.Width = new GridLength(4, GridUnitType.Pixel);

			col7b.Width = new GridLength(10, GridUnitType.Pixel);
			col8b.Width = new GridLength(extraColumnWidth, GridUnitType.Pixel);
			col9b.Width = new GridLength(extraColumnWidth, GridUnitType.Pixel);
			col10b.Width = new GridLength(5, GridUnitType.Pixel);
			col11b.Width = new GridLength(5, GridUnitType.Pixel);
			col12b.Width = new GridLength(40, GridUnitType.Pixel);

			panel.ColumnDefinitions.Add(col0b);
			panel.ColumnDefinitions.Add(col1b);
			panel.ColumnDefinitions.Add(col2b);
			panel.ColumnDefinitions.Add(col3b);
			panel.ColumnDefinitions.Add(col4b);
			panel.ColumnDefinitions.Add(col5b);
			panel.ColumnDefinitions.Add(col6b);
			panel.ColumnDefinitions.Add(col7b);
			panel.ColumnDefinitions.Add(col8b);
			panel.ColumnDefinitions.Add(col9b);
			panel.ColumnDefinitions.Add(col10b);
			panel.ColumnDefinitions.Add(col11b);
			panel.ColumnDefinitions.Add(col12b);

			scrollView.Content = panel;

			int rowNum = 0;
			int[] count = { 0, 0 };
			foreach (AlertItem result in results)
			{
				int side = result.Side;

				count[side]++;

				if (rowNum < count[0] || rowNum < count[1])
				{
					row = new RowDefinition();
					row.Height = new GridLength(rowHeight, GridUnitType.Pixel);
					panel.RowDefinitions.Add(row);
					rowNum++;
				}

				string symbol = result.Symbol;
				string description = result.Description;
				string interval = result.Interval;

				bool useDescription = isYieldDescription(description);

				bool hasAlert = (alert.HasNotification(symbol, dateTime));

				int exit = 0;

				TradeHorizon horizon = Trade.Manager.GetIntervalTradeHorizon("D");

				string portfolioName = alert.Portfolio;
				string network = _portfolio.GetTradeNetwork(portfolioName);
				int size = Trade.Manager.getTradeOpenDirection(portfolioName, horizon, symbol);
				string sideText = (size > 0) ? " Long " + size.ToString() : ((size < 0) ? " Short " + (-size).ToString() : "");

				int hashMarkType1 = MainView.EnableHistoricalRecommendations ? alert.GetHashMarkType(0, symbol) : -1;
				int hashMarkType2 = MainView.EnableHistoricalRecommendations ? alert.GetHashMarkType(1, symbol) : -1;

				TextBlock alertLabel3 = null;
				if (hasAlert)
				{
					alertLabel3 = new TextBlock();
					alertLabel3.SetValue(Grid.ColumnProperty, 7 * side);
					alertLabel3.SetValue(Grid.RowProperty, count[side] - 1);
					alertLabel3.Margin = new Thickness(0, 0, 0, 0);
					alertLabel3.Padding = new Thickness(0, 0, 0, 0);
					alertLabel3.VerticalAlignment = System.Windows.VerticalAlignment.Top;
					alertLabel3.Text = "\u25C6";
					alertLabel3.Foreground = Brushes.Red;
					alertLabel3.FontSize = 11;
					alertLabel3.Visibility = Visibility.Visible;
					panel.Children.Add(alertLabel3);
				}

				int outsideResearchDirection = getResearchDirection("Outside", symbol, 0);

				if (outsideResearchDirection != 0)
				//if (hashMarkType1 != -1)
				{
					Brush brush = (outsideResearchDirection > 0) ? Brushes.Lime : Brushes.Red;

					TextBlock label1 = new TextBlock();
					label1.SetValue(Grid.ColumnProperty, 7 * side + 1);
					label1.SetValue(Grid.RowProperty, count[side] - 1);
					label1.Text = "\u25cf";
					label1.Foreground = brush;
					label1.Background = Brushes.Transparent;
					label1.Margin = new Thickness(0, 3, 0, 0);
					label1.Padding = new Thickness(0, 0, 0, 0);
					label1.HorizontalAlignment = HorizontalAlignment.Left;
					label1.VerticalAlignment = VerticalAlignment.Bottom;
					label1.FontFamily = new FontFamily("Helvetica Neue");
					label1.FontWeight = FontWeights.Bold;
					label1.FontSize = fontSize;
					label1.ToolTip = getResearchComment("Outside", symbol, 0);
					panel.Children.Add(label1);
				}

				if (hashMarkType2 != -1)
				{
					Brush brush = Brushes.Black;
					if (hashMarkType2 == 0) brush = Brushes.Lime;
					else if (hashMarkType2 == 1) brush = Brushes.Magenta;
					else if (hashMarkType2 == 2) brush = Brushes.Red;
					else if (hashMarkType2 == 3) brush = Brushes.Yellow;

					TextBlock label1 = new TextBlock();
					label1.SetValue(Grid.ColumnProperty, 7 * side + 2);
					label1.SetValue(Grid.RowProperty, count[side] - 1);
					label1.Text = "";  //"\u2758"
					label1.Foreground = brush;
					label1.Background = Brushes.Transparent;
					label1.Margin = new Thickness(0, 3, 0, 0);
					label1.Padding = new Thickness(0, 0, 0, 0);
					label1.HorizontalAlignment = HorizontalAlignment.Left;
					label1.VerticalAlignment = VerticalAlignment.Bottom;
					label1.FontFamily = new FontFamily("Helvetica Neue");
					label1.FontWeight = FontWeights.Bold;
					label1.FontSize = fontSize;
					panel.Children.Add(label1);
				}

				string reviewInterval;
				ReviewAction reviewAction = MainView.GetReviewSymbolInterval(alert.Portfolio, symbol, out reviewInterval).Action;

				if (MainView.EnableRickStocks && (reviewAction != ReviewAction.StayingOut || reviewAction != ReviewAction.HoldPosition))
				{
					Color color1 = Colors.White;
					if (reviewAction == ReviewAction.ExpectLongExit) color1 = Colors.Magenta;  // size s/b int   1 = Expect Long Exit magenta
					else if (reviewAction == ReviewAction.ExpectLongExit) color1 = Colors.Red;  // 2 = Anticipate Short red
					else if (reviewAction == ReviewAction.AddShort) color1 = Colors.Firebrick;  // 3 = Add Short Firebrick
					else if (reviewAction == ReviewAction.HoldPosition) color1 = Colors.Orange;  //  4 = Hold Position  orange
					else if (reviewAction == ReviewAction.StayingOut) color1 = Colors.Yellow;  // 5 = Staying Out  yellow
					else if (reviewAction == ReviewAction.HoldLong) color1 = Colors.DodgerBlue;  //  6 = Hold Long  dodger blue
					else if (reviewAction == ReviewAction.AddLong) color1 = Colors.Green;  // 7 = Add Long green
					else if (reviewAction == ReviewAction.AnticipateBuy) color1 = Colors.Lime;  // 8 = Anticipate Buy   lime
					else if (reviewAction == ReviewAction.AnticipateShort) color1 = Colors.Cyan;  // 9 = Expect Short Exit   cyan

					TextBlock label1 = new TextBlock();
					label1.SetValue(Grid.ColumnProperty, 7 * side + 4);
					label1.SetValue(Grid.RowProperty, count[side] - 1);
					label1.Text = "";  //WAS "!"
					label1.Foreground = new SolidColorBrush(color1);
					label1.Background = Brushes.Transparent;
					label1.Margin = new Thickness(0, 3, 0, 0);
					label1.Padding = new Thickness(0, 0, 0, 0);
					label1.HorizontalAlignment = HorizontalAlignment.Left;
					label1.VerticalAlignment = VerticalAlignment.Bottom;
					label1.FontFamily = new FontFamily("Helvetica Neue");
					label1.FontWeight = FontWeights.Bold;
					label1.FontSize = fontSize;
					panel.Children.Add(label1);
				}
				string[] fields = symbol.Split(Symbol.SpreadCharacter);
				string key = "";
				foreach (string field in fields)
				{
					string[] subSymbol = field.Trim().Split(' ');
					string sym = (subSymbol.Length <= 3) ? subSymbol[0] : subSymbol[0] + " " + subSymbol[1];
					key += (key.Length > 0) ? " - " + sym : sym;
				}

				AlertTextBlock label2 = new AlertTextBlock(alert);
				label2.SetValue(Grid.ColumnProperty, 7 * side + 5);
				label2.SetValue(Grid.RowProperty, count[side] - 1);
				label2.Text = useDescription ? description : key;
				label2.Interval = interval;
				label2.Symbol = symbol;
				label2.DateTime = dateTime;
				label2.Alerted = hasAlert;
				label2.AlertIndicators.Add(alertLabel1);
				label2.AlertIndicators.Add(alertLabel2);
				if (alertLabel3 != null)
				{
					label2.AlertIndicators.Add(alertLabel3);
				}

				Color color;
				if (colorSymbols)
				{
					if (size == 0)
					{
						color = Color.FromArgb(0xff, 0xff, 0xff, 0xff); //
					}
					else if (size > 0)
					{
						color = Color.FromArgb(0xFF, 0x00, 0xff, 0x00);  //Lime  now same as short on chart  alpha is different FromArgb(0x95, 0x00, 0xff, 0x00) 
					}
					else
					{
						color = Color.FromArgb(0xFF, 0xFF, 0x00, 0x00);  //Red  now same as short on chart   alpha is different
					}
				}
				else
				{
					color = (side == 0) ? alert.TextColor1 : alert.TextColor2;
				}

				label2.Foreground = new SolidColorBrush(color);
				label2.Background = Brushes.Transparent;
				label2.Padding = new Thickness(0, 2, 0, 0);
				label2.HorizontalAlignment = HorizontalAlignment.Left;
				label2.VerticalAlignment = VerticalAlignment.Top;
				label2.FontFamily = new FontFamily("Helvetica Neue");
				label2.FontWeight = FontWeights.Normal;
				label2.FontSize = fontSize;
				if (!result.Enable)
				{
					TextDecoration td = new TextDecoration(TextDecorationLocation.Strikethrough, new Pen(Brushes.White, 1), 0, TextDecorationUnit.FontRecommended, TextDecorationUnit.FontRecommended);
					label2.TextDecorations.Add(td);
				}
				else
				{
					label2.TextDecorations.Clear();
				}
				if (description.Length > 0)
				{

				}
				label2.Cursor = Cursors.Hand;
				label2.MouseDown += new MouseButtonEventHandler(Ticker_MouseDown);
				panel.Children.Add(label2);
			}

			double height = ((rowCount == 0) ? rowNum : rowCount) * rowHeight;

			// vertical line
			if (rowNum > 0)
			{
				Rectangle line2 = new Rectangle();
				line2.SetValue(Grid.RowProperty, 2);
				line2.SetValue(Grid.RowSpanProperty, rowNum);
				line2.SetValue(Grid.ColumnProperty, 6);
				line2.Fill = new SolidColorBrush(Color.FromRgb(0x18, 0x31, 0x52));
				line2.Height = height;
				line2.Width = 1;
				cell.Children.Add(line2);
			}

			scrollView.Height = height;
			return cell;
		}

		private int getResearchDirection(string name, string symbol, int ago)
		{
			return MainView.GetResearchDirection(name, symbol, ago);
		}

		private string getResearchComment(string name, string symbol, int ago)
		{
			return MainView.GetResearchComment(name, symbol, ago);
		}

		private void SignalLeft_MouseDown(object sender, MouseButtonEventArgs e)
		{
			AlertLabel label = sender as AlertLabel;
			Alert alert = label.Alert;
			int column = alert.ActiveColumn - 1;
			if (column < 0) column = _columnCount - 1;
			changeSignalColumns(alert, column);
		}

		private void SignalRight_MouseDown(object sender, MouseButtonEventArgs e)
		{
			AlertLabel label = sender as AlertLabel;
			Alert alert = label.Alert;
			int column = alert.ActiveColumn + 1;
			if (column > _columnCount - 1) column = 0;
			changeSignalColumns(alert, column);
		}

		private void changeSignalColumns(Alert alert, int column)
		{
			if (alert.ViewType == Alert.AlertViewType.Spreadsheet)
			{
				if (alert.ActiveColumn != column)
				{
					setArrowVisibility(alert, Visibility.Hidden);
					alert.ActiveColumn = column;

					Grid grid = alert.TreeViewItem.Items[0] as Grid;
					int count = alert.GetColumnCount();
					int number = Math.Min(count, _columnCount);
					for (int col = 0; col < number; col++)
					{
						int colNum = Math.Abs(number + (col - alert.ActiveColumn)) % number;
						grid.Children[2 * col + 1].SetValue(Grid.ColumnProperty, colNum);
						grid.Children[2 * col + 2].SetValue(Grid.ColumnProperty, colNum);
					}


					setArrowVisibility(alert, Visibility.Visible);
				}
			}
		}

		private void setArrowVisibility(Alert alert, Visibility visibility)
		{
			int index = alert.ActiveColumn;
			if (index < alert.LeftArrows.Count)
			{
				alert.LeftArrows[index].Visibility = visibility;
			}
			if (index < alert.RightArrows.Count)
			{
				alert.RightArrows[index].Visibility = visibility;
			}
		}

		void Ticker_MouseDown(object sender, MouseButtonEventArgs e)
		{
			try
			{
				AlertTextBlock label = sender as AlertTextBlock;
				if (label != null)
				{
					string symbol = label.Symbol;
					string interval = label.Interval;
					DateTime dateTime = label.DateTime;
					Alert alert = label.Alert;

					foreach (TextBlock alertLabel in label.AlertIndicators)
					{
						alertLabel.Visibility = Visibility.Hidden;
					}

					if (label.Alerted)
					{
						alert.AddVisit(symbol);

						label.Alerted = false;

						Brush brush1 = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));
						Brush brush2 = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));
						AlertButton.Foreground = brush1;
					}

					var conditionAlert = alert as ConditionAlert;
					var modelName = (conditionAlert != null) ? conditionAlert.GetModelName() : "";
					changeChart(symbol, interval, true, alert.Portfolio, modelName);

					if (_alertType == AlertType.ConditionAlert)
					{
						for (int ii = 0; ii < _chart3.Panels.Count; ii++)
						{
							int id = 10000 + ii;
							LineAnnotation marker = new LineAnnotation();
							marker.Id = id;
							marker.DateTime = dateTime;
							marker.Color = Colors.DodgerBlue;
							marker.Width = 0.125;
							_chart3.Panels[ii].RemoveAnnotations(id);
							_chart3.Panels[ii].AddAnnotation(marker);
						}
					}

					int index = alert.GetDateTimeIndex(dateTime);
					changeSignalColumns(alert, index);
					highlightIntervalButton(ChartIntervals, interval);
				}

				Flash.Manager.RemoveFlash(AlertButton);
			}
			catch
			{
				// index out of range issue
			}

			e.Handled = true;
		}

		void Chart_ChartEvent(object sender, ChartEventArgs e)
		{
			if (e.Id == ChartEventType.SettingChange)
			{
				saveChartProperties();
			}
			else if (e.Id == ChartEventType.ExitCharts)
			{
				hideChart();
			}
			else if (e.Id == ChartEventType.PrintChart)
			{
				PrintDialog printDialog = new PrintDialog();
				if (printDialog.ShowDialog() == true)
				{
					Chart chart = sender as Chart;
					chart.Print(printDialog);
				}
			}
			else if (e.Id == ChartEventType.PartitionCharts)
			{
				_chartCount--;
				if (_chartCount == 0) _chartCount = 3;

				ChartGrid.Visibility = Visibility.Visible;

				if (_chartCount == 2)
				{
					resetCharts();
					_chart1.Hide();
					_chart2.Show();
					_chart3.Show();
					Grid.SetColumn(ChartBorder2, 0);
					Grid.SetColumnSpan(ChartBorder2, 2);
					Grid.SetColumnSpan(ChartBorder3, 2);
				}
				else if (_chartCount == 3)
				{
					resetCharts();
					_chart1.Show();
					_chart2.Show();
					_chart3.Show();
					Grid.SetColumnSpan(ChartBorder1, 1);
					Grid.SetColumnSpan(ChartBorder2, 1);
					Grid.SetColumnSpan(ChartBorder3, 2);
				}
				else if (_chartCount == 1)
				{
					if (sender == _chart1)
					{
						Grid.SetRow(ChartBorder1, 0);
						Grid.SetColumn(ChartBorder1, 0);
						Grid.SetRowSpan(ChartBorder1, 2);
						Grid.SetColumnSpan(ChartBorder1, 2);
						_chart2.Hide();
						_chart3.Hide();
					}
					else if (sender == _chart2)
					{
						Grid.SetRow(ChartBorder2, 0);
						Grid.SetColumn(ChartBorder2, 0);
						Grid.SetRowSpan(ChartBorder2, 2);
						Grid.SetColumnSpan(ChartBorder2, 2);
						_chart1.Hide();
						_chart3.Hide();
					}
					else if (sender == _chart3)
					{
						Grid.SetRow(ChartBorder3, 0);
						Grid.SetColumn(ChartBorder3, 0);
						Grid.SetRowSpan(ChartBorder3, 2);
						Grid.SetColumnSpan(ChartBorder3, 2);
						_chart1.Hide();
						_chart2.Hide();
					}
				}
				addBoxes(_chartCount != 1);
			}
			else if (e.Id == ChartEventType.TradeEvent) // trade event change
			{
			}
			else if (e.Id == ChartEventType.ChangeSymbol) // change symbol change
			{
				Chart chart = sender as Chart;
				_chartSymbol1 = chart.SymbolName;
			}
			else if (e.Id == ChartEventType.ToggleCursor) // toggle cursor on/off
			{
				Chart chart = sender as Chart;
				_mainView.HideChartCursor = !chart.ShowCursor;
			}
			else if (e.Id == ChartEventType.ChartMode)
			{
				switchMode((_mode == "POSITIONS") ? "PCM RESEARCH" : "POSITIONS");
			}
			else if (e.Id == ChartEventType.SetupConditions)
			{
				Chart chart = sender as Chart;

				ConditionDialog dlg = getConditionDialog();
				int horizon = chart.Horizon;
				dlg.Condition = MainView.GetConditions(horizon);
				dlg.Horizon = horizon;

				_scanDialog.Visibility = Visibility.Visible;
			}
			else if (e.Id == ChartEventType.Calculator)
			{
				Chart chart = sender as Chart;
				if (chart == _chart3)
				{
					updateCalculator(chart);
				}
			}
		}

		int _chartCount = 3;

		private void resetCharts()
		{
			Grid.SetRow(ChartBorder1, 0);
			Grid.SetColumn(ChartBorder1, 0);
			Grid.SetRowSpan(ChartBorder1, 1);
			Grid.SetColumnSpan(ChartBorder1, 1);

			Grid.SetRow(ChartBorder2, 0);
			Grid.SetColumn(ChartBorder2, 1);
			Grid.SetRowSpan(ChartBorder2, 1);
			Grid.SetColumnSpan(ChartBorder2, 1);

			Grid.SetRow(ChartBorder3, 1);
			Grid.SetColumn(ChartBorder3, 0);
			Grid.SetRowSpan(ChartBorder3, 1);
			Grid.SetColumnSpan(ChartBorder3, 2);
		}

		private void addBoxes(bool allCharts)
		{
			List<string> imageNames = new List<string>();
			imageNames.Add(@"Images/CloseChart3.png:Close Charts");
			imageNames.Add(allCharts ? @"Images/TileChart.png:Tile Chart" : @"Images/TileChart.png:Tile Charts");
			imageNames.Add(@"Images/Refresh10.png:Refresh Data");
			imageNames.Add(@"Images/Track17.png:Cursor On/Off");
			//imageNames.Add(@"Images/printer icon2.png:Print Chart");
			//imageNames.Add((_mode == "POSITIONS") ? @"Images/Research3.png:Research Mode" : @"Images/Position3.png:Positions Mode");
			_chart1.InitializeControlBox(imageNames);
			_chart2.InitializeControlBox(imageNames);
			_chart3.InitializeControlBox(imageNames);
		}

		private void showComment(string comment)
		{
			CommentGrid.Visibility = System.Windows.Visibility.Visible;
			//Comment.AppendText(comment);
		}

		private void hideComment()
		{
			//FlowDocument document = Comment.Document;

			//string text = XamlWriter.Save(document);

			CommentGrid.Visibility = Visibility.Collapsed;
		}

		private void hideChart()
		{
			if (_chart1 != null)
			{
				resetCharts();

				_chart1.Hide();
				_chart2.Hide();
				_chart3.Hide();
				//_chart4.Hide();

				ChartIntervals.Visibility = Visibility.Collapsed;
				Tree.SetValue(Grid.ColumnSpanProperty, 2);
				ChartGrid.Visibility = Visibility.Collapsed;
				List<Alert> alerts = Alert.Manager.Alerts;
				foreach (Alert alert in alerts)
				{
					setArrowVisibility(alert, Visibility.Hidden);
					if (alert.ActiveColumn != 0)
					{
						alert.ActiveColumn = 0;
						updateThisAlert(alert);
					}
				}

				removeCharts();
			}
		}

		private void changeChart(string symbol, string interval, bool initialize, string portfolioName, string modelName = "")
		{
			addCharts();

			if (portfolioName != "")
			{
				_chart1.PortfolioName = portfolioName;
				_chart2.PortfolioName = portfolioName;
				_chart3.PortfolioName = portfolioName;
			}

			bool noCharts = (!_chart1.IsVisible() && !_chart2.IsVisible() && !_chart3.IsVisible());

			if (noCharts)
			{
				_chart1.Show();
				_chart2.Show();
				_chart3.Show();
				switchMode(_mode);
			}

			ChartGrid.Visibility = Visibility.Visible;
			ChartIntervals.Visibility = Visibility.Visible;

			string symbol1 = symbol;
			string symbol2 = symbol;
			string interval1 = getChartInterval(interval, 2);
			string interval2 = getChartInterval(interval, 1);
			string interval3 = getChartInterval(interval, 0);

			if (Portfolio.GetLinkCharts())
			{
				interval1 = _chart1.GetOverviewInterval(interval, 1);
				interval2 = _chart1.GetOverviewInterval(interval, 1);
				interval3 = _chart1.GetOverviewInterval(interval, 0);

				bool pair = symbol.Contains(" - ");
				if (pair)
				{
					symbol1 = symbol;
					string[] fields = symbol.Split(Symbol.SpreadCharacter);
					symbol2 = fields[0].Trim();
				}
				else
				{
					symbol2 = symbol;
					var hedge = _portfolio.GetTradeHedge("US 100 H", symbol);
					symbol1 = (hedge.Length > 0) ? symbol + " - " + hedge : symbol;
				}
			}

			bool symbolChange1 = (symbol1 != _chartSymbol1);
			bool symbolChange2 = (symbol2 != _chartSymbol2);
			bool intervalChange1 = (interval1 != _chartIntervals[1]);
			bool intervalChange2 = (interval2 != _chartIntervals[2]);
			bool intervalChange3 = (interval3 != _chartIntervals[3]);


			if (symbolChange1 || symbolChange2)
			{
				InvestmentAmt.Text = "";
			}

			if (initialize || symbolChange1 || symbolChange2 || intervalChange1 || intervalChange2 || intervalChange3)
			{
				_chartSymbol1 = symbol1;
				_chartSymbol2 = symbol2;

				_chartIntervals[1] = interval1;
				_chartIntervals[2] = interval2;
				_chartIntervals[3] = interval3;

				interval1 = interval1.Replace(" Min", "");
				interval2 = interval2.Replace(" Min", "");
				interval3 = interval3.Replace(" Min", "");

				_chart1.Change(symbol1, interval1);
				_chart2.Change(symbol2, interval2);
				_chart3.Change(symbol1, interval3);

				setChartModel((modelName.Length > 0) ? modelName : _modelName);

				updateCalculator(_chart3);
			}
		}

		private string getChartInterval(string interval, int level)
		{
			string output = "D";
			if (interval == "120 Min")
			{
				if (level == 2) output = "240";
				else if (level == 1) output = "120";
				else if (level == 0) output = "60";
				else if (level == -1) output = "30";
			}
			else
			{
				output = _chart1.GetOverviewInterval(interval, level);
			}
			return output;
		}

		public void Close()
		{
			_timer.Tick -= new System.EventHandler(timer_Tick);
			_timer.Stop();

			_portfolio.Close();

			removeCharts();

			List<Alert> alerts = Alert.Manager.Alerts;
			foreach (Alert alert in alerts)
			{
				removeAlertUI(alert);
				removeAlertEvents(alert);
			}

			setInfo();
		}

		private void Interval_MouseEnter(object sender, MouseEventArgs e)
		{
			var label = sender as Label;
			label.Tag = label.Foreground;
			label.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xcc, 0xff));
		}

		private void Interval_MouseLeave(object sender, MouseEventArgs e)
		{
			var label = sender as Label;
			label.Foreground = (Brush)label.Tag;
			//highlightIntervalButton(ChartIntervals, _time.Replace(" Min", ""));
		}

		private void AutoMLView_MouseDown(object sender, MouseButtonEventArgs e)
		{
			Close();
			_mainView.Content = new AutoMLView(_mainView);
		}

		private void Alert_MouseDown(object sender, MouseButtonEventArgs e)
		{
			Flash.Manager.RemoveFlash(AlertButton);
		}

		//private void GlobalCost_MouseDown(object sender, MouseButtonEventArgs e)
		//{
		//    Close();
		//    _mainView.Content = new MarketMonitor(_mainView);
		//    hideNavigation();
		//    GlobalCostNav.Visibility = Visibility.Visible;
		//    ChartIntervals.Visibility = Visibility.Visible;
		//    ProdCostImpactNav.Visibility = Visibility.Collapsed;
		//    AOD3ControlGrid.Visibility = Visibility.Collapsed;
		//    CostCal.Visibility = Visibility.Collapsed;
		//    showMap();
		//    _viewType = ViewType.Map;
		//    startMapColoring(500);
		//}

		private void GlobalCost_MouseDown(object sender, MouseButtonEventArgs e)
		{
			Close();
			_mainView.Content = new MarketMonitor(_mainView, ViewType.Map);
			//_mainView.Content = new MarketMonitor(_mainView);
		}

		private void AODView_MouseDown(object sender, MouseButtonEventArgs e)
		{
			Close();
			//_mainView.Content = new Competitor(_mainView);
		}

		private void OurView_MouseDown(object sender, MouseButtonEventArgs e)
		{
			Close();
			_mainView.Content = new Charts(_mainView);
		}
		private void LandingPage_MouseDown(object sender, MouseButtonEventArgs e)
		{
			Close();
			_mainView.Content = new LandingPage(_mainView);
		}

		//private void ML_MouseDown(object sender, MouseButtonEventArgs e)
		//{
		//    Close();
		//    _mainView.Content = new AutoMLView(_mainView);
		//}


		private void FundamentalML_MouseDown(object sender, MouseButtonEventArgs e)
		{
			_mainView.Content = new PortfolioBuilder(_mainView);
		}

		private void MarketMaps_MouseDown(object sender, MouseButtonEventArgs e)
		{
			Close();
			_mainView.Content = new MarketMonitor(_mainView);
		}

		private static Window? GetOwnerWindow(DependencyObject? context)
		{
			return context is Window w ? w : Window.GetWindow(context);
		}

		private static MainView? GetMainView()
		{
			return Application.Current?.MainWindow as MainView;
		}

		private void AddScan_MouseDown(object sender, MouseButtonEventArgs e)
		{
			// Create a new dialog if none is open
			if (Alerts_scanDialog == null)
			{
				// 1) Find the owner window safely (works from Window or UserControl)
				var ownerWin = GetOwnerWindow(this);

				// 2) Get the MainView required by ScanDialog’s constructor
				var mainView = GetMainView();
				if (mainView == null)
				{
					MessageBox.Show("MainView is not available. Ensure Application.Current.MainWindow is a MainView.");
					return;
				}

				// 3) Create dialog with required ctor
				Alerts_scanDialog = new ScanDialog(mainView);

				// 4) Set Owner only if we successfully resolved a Window
				if (ownerWin != null)
					Alerts_scanDialog.Owner = ownerWin;

				// Reset the reference when the dialog closes
				Alerts_scanDialog.Closed += (_, __) => Alerts_scanDialog = null;

				// --- Set your defaults ---
				Alerts_scanDialog.ConditionAlert = null;
				Alerts_scanDialog.UseDefaultTitle = true;
				Alerts_scanDialog.Title = string.Empty;
				Alerts_scanDialog.PortfolioText = string.Empty;
				Alerts_scanDialog.Condition = string.Empty;
				Alerts_scanDialog.NotificationOn = true;
				Alerts_scanDialog.UseOnTop = true;
				Alerts_scanDialog.RunIntervalMinutes = 5;
				Alerts_scanDialog.TextColor1 = Color.FromRgb(255, 158, 42);
				Alerts_scanDialog.TextColor2 = Color.FromRgb(255, 158, 42);
				Alerts_scanDialog.TextColor3 = Color.FromRgb(255, 255, 255);
				Alerts_scanDialog.ViewType = Alert.AlertViewType.Spreadsheet;

				Alerts_scanDialog.DialogEvent += dialogEvent;

				// Show the dialog (pick one)
				// modeless
				// Alerts_scanDialog.ShowDialog(); // modal

				Alerts_scanDialog.Activate();
			}
			else
			{
				// Already open: bring to front
				if (Alerts_scanDialog.WindowState == WindowState.Minimized)
					Alerts_scanDialog.WindowState = WindowState.Normal;

				Alerts_scanDialog.Activate();
			}
			Alerts_scanDialog.Show();
		}

		//private void AddScan_MouseDown(object sender, MouseButtonEventArgs e)
		//{
		//    try
		//    {
		//        Label label = sender as Label;

		//        ScanDialog dlg = getScanDialog();

		//        dlg.ConditionAlert = null;
		//        dlg.UseDefaultTitle = true;
		//        dlg.Title = "";
		//        dlg.PortfolioText = "";
		//        dlg.Condition = "";
		//        dlg.NotificationOn = true;
		//        dlg.UseOnTop = true;
		//        dlg.RunIntervalMinutes = 5;
		//        dlg.TextColor1 = Color.FromRgb(255, 158, 42);
		//        dlg.TextColor2 = Color.FromRgb(255, 158, 42);
		//        dlg.TextColor3 = Color.FromRgb(255, 255, 255);
		//        dlg.ViewType = Alert.AlertViewType.Spreadsheet;

		//        dlg.Visibility = System.Windows.Visibility.Visible;
		//    }
		//    catch (Exception x)
		//    {
		//        var text = x.Message;
		//    }
		//}

		private string getChartInterval(string interval)
		{
			string output;
			if (interval == "D") output = "Daily";
			else if (interval == "W") output = "Weekly";
			else if (interval == "M") output = "Monthly";
			else if (interval == "Q") output = "Quarterly";
			else output = interval + " Min";
			return output;
		}

		private void ChartInterval_MouseDown(object sender, MouseButtonEventArgs e)
		{
			Label label = sender as Label;
			string interval = label.Content as string;
			highlightIntervalButton(ChartIntervals, interval);
			changeChart(_chartSymbol1, getChartInterval(interval), false, "");
		}

		private void highlightIntervalButton(Panel panel, string interval)
		{
			if (interval.Length > 0)
			{
				Brush brush1 = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));
				Brush brush2 = new SolidColorBrush(Color.FromRgb(0x00, 0xcc, 0xff));

				for (int ii = 1; ii < panel.Children.Count; ii++)
				{
					Label label = panel.Children[ii] as Label;
					if (label != null)
					{
						string text = label.Content as string;

						int length = Math.Min(interval.Length, text.Length);
						label.Foreground = (interval.Substring(0, length) == text.Substring(0, length)) ? brush2 : brush1;
					}
				}
			}
		}

		private void AddScan_MouseEnter(object sender, MouseEventArgs e)
		{
			Label label = sender as Label;
			label.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xcc, 0xff));
		}

		private void AddScan_MouseLeave(object sender, MouseEventArgs e)
		{
			Label label = sender as Label;
			label.Foreground = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));
		}

		private void RestoreExamples_MouseEnter(object sender, MouseEventArgs e)
		{
			Label label = sender as Label;
			label.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xcc, 0xff));
		}

		private void RestoreExamples_MouseLeave(object sender, MouseEventArgs e)
		{
			Label label = sender as Label;
			label.Foreground = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));
		}

		private void Server_MouseDown(object sender, MouseButtonEventArgs e)
		{
			Close();
			_mainView.Content = new Timing(_mainView);
		}

		private void Portfolios_MouseEnter(object sender, MouseEventArgs e)
		{
			Label label = sender as Label;
			label.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xcc, 0xff));
		}

		private void Portfolios_MouseLeave(object sender, MouseEventArgs e)
		{
			Label label = sender as Label;
			label.Foreground = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));
		}

		private void OurView_MouseEnter(object sender, MouseEventArgs e)
		{
			Label label = sender as Label;
			label.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xcc, 0xff));
		}

		private void OurView_MouseLeave(object sender, MouseEventArgs e)
		{
			Label label = sender as Label;
			label.Foreground = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));
		}


		private void CloseComment_MouseDown(object sender, MouseButtonEventArgs e)
		{
			hideComment();
		}


		private void Link_Charts_CheckBox_Checked(object sender, RoutedEventArgs e)
		{
			CheckBox checkbox = sender as CheckBox;
			bool linkCharts = (checkbox.IsChecked == true);
			Portfolio.SetLinkCharts(linkCharts);
		}

		private void Link_Accounts_CheckBox_Checked(object sender, RoutedEventArgs e)
		{
			CheckBox checkbox = sender as CheckBox;
			bool linkAccounts = (checkbox.IsChecked == true);
			Portfolio.SetLinkAccounts(linkAccounts);
		}

		private void InvestmentAmt_TextChanged(object sender, TextChangedEventArgs e)
		{
			InvestmentAmt.SelectionStart = InvestmentAmt.Text.Length;
			updateCalculator(_chart3);
		}

		private void Mouse_Enter(object sender, MouseEventArgs e)
		{
			Label label = sender as Label;
			label.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xcc, 0xff));
		}

		private void Mouse_Leave(object sender, MouseEventArgs e)
		{
			Label label = sender as Label;
			label.Foreground = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));
		}

		private void ArrowDn_Mouse_Enter(object sender, MouseEventArgs e)
		{
			BitmapImage bi2 = new BitmapImage();
			bi2 = new BitmapImage();
			bi2.BeginInit();
			bi2.UriSource = new Uri(@"Images/RedArrow.png", UriKind.RelativeOrAbsolute);
			bi2.EndInit();
		}

		private void ArrowDn_Mouse_Leave(object sender, MouseEventArgs e)
		{
			BitmapImage bi2 = new BitmapImage();
			bi2 = new BitmapImage();
			bi2.BeginInit();
			bi2.UriSource = new Uri(@"Images/Silver Dn Arrow.png", UriKind.RelativeOrAbsolute);
			bi2.EndInit();
		}

		private void CloseHelp_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			HelpCreateSection.BringIntoView();
			ViewHelp.Visibility = Visibility.Collapsed;
			ViewHelp.Visibility = Visibility.Collapsed;
			Results.Visibility = Visibility.Collapsed;
			Edit.Visibility = Visibility.Collapsed;
			HelpON.Visibility = Visibility.Visible;
			HelpOFF.Visibility = Visibility.Collapsed;
		}

		private void Help_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			HelpCreateSection.BringIntoView();
			ViewHelp.Visibility = Visibility.Visible;
			//GenNav.Visibility = Visibility.Collapsed;
			//GlobalNav.Visibility = Visibility.Collapsed;
			HelpOFF.Visibility = Visibility.Visible;
			HelpON.Visibility = Visibility.Collapsed;
		}

		private void HelpCreate_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			HelpCreateSection.BringIntoView();
			ViewHelp.Visibility = Visibility.Visible;
			ViewHelp.Visibility = Visibility.Visible;
			Results.Visibility = Visibility.Collapsed;
			Edit.Visibility = Visibility.Collapsed;
		}

		private void HelpResults_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			ResultsSection.BringIntoView();
			ViewHelp.Visibility = Visibility.Collapsed;
			ViewHelp.Visibility = Visibility.Collapsed;
			Results.Visibility = Visibility.Visible;
			Edit.Visibility = Visibility.Collapsed;
		}

		private void HelpEdit_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			EditSection.BringIntoView();
			ViewHelp.Visibility = Visibility.Collapsed;
			ViewHelp.Visibility = Visibility.Collapsed;
			Results.Visibility = Visibility.Collapsed;
			Edit.Visibility = Visibility.Visible;
		}

		private void Help_MouseEnter(object sender, MouseEventArgs e) // change to &#x3F
		{
			var label = sender as Label;
			label.Tag = label.Foreground;
			//label.Content = "\u2630";  // was 003f
			label.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xcc, 0xff));
		}

		private void Help_MouseLeave(object sender, MouseEventArgs e)
		{
			var label = sender as Label;
			//label.Content = "\u2630";  //was 1392  0069
			label.Foreground = (Brush)label.Tag;
		}

		private void Symbol_Click(object sender, MouseButtonEventArgs e)
		{
			TextBox textBox = sender as TextBox;
			if (textBox.Text.Length > 0)
			{
				string text = textBox.Text;

				if (text == "ST-GP")
				{
					string spread = StkSymbol.Text + " US Equity-" + SectorETFSymbol.Text + " US Equity";
					changeChart(spread, _chartIntervals[3], false, "");
				}
				else if (text == "ST-SP")
				{
					string spread = StkSymbol.Text + " US Equity-" + IndexETFSymbol.Text + " US Equity";
					changeChart(spread, _chartIntervals[3], false, "");
				}
				else
				{
					string symbol = text + " US Equity";
					changeChart(symbol, _chartIntervals[3], false, "");
				}
			}
		}

		private void Label_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			string settings = MainView.CalculatorSettings;
			if (settings == null) settings = "";
			string[] fields = settings.Split(';');
			int count = (settings.Length == 0) ? 0 : fields.Length;
			double portfolioBalance = (count >= 1) ? double.Parse(fields[0]) : 1000000;
			double portfolioPercent = (count >= 2) ? double.Parse(fields[1]) : 1;
			bool useBeta = (count >= 3) ? bool.Parse(fields[2]) : true;
			bool usePricePercent = (count >= 4) ? bool.Parse(fields[3]) : true;
			double pricePercent = (count >= 5) ? double.Parse(fields[4]) : 25;
			bool useFixedDollar = (count >= 6) ? bool.Parse(fields[5]) : false;
			double fixedDollarAmount = (count >= 7) ? double.Parse(fields[6]) : 0;
			TradeDialog dlg = new TradeDialog(portfolioBalance, portfolioPercent, useBeta, usePricePercent, pricePercent, useFixedDollar, fixedDollarAmount);
			if (dlg.ShowDialog() == true)
			{
				portfolioBalance = dlg.PortfolioBalance;
				portfolioPercent = dlg.PortfolioPercent;
				useBeta = dlg.UseBeta;
				usePricePercent = dlg.UsePricePercent;
				pricePercent = dlg.PricePercent;
				useFixedDollar = dlg.UseFixedDollar;
				fixedDollarAmount = dlg.FixedDollarAmount;
				MainView.CalculatorSettings = portfolioBalance.ToString() + ";" + portfolioPercent.ToString() + ";" + useBeta.ToString() +
					";" + usePricePercent.ToString() + ";" + pricePercent.ToString() + ";" + useFixedDollar.ToString() + ";" + fixedDollarAmount.ToString();
				_updateCalculator = true;
			}
		}

		private void BtnSettings_Click(object sender, System.Windows.RoutedEventArgs e)
		{
			var menu = new System.Windows.Controls.ContextMenu();

			var changePassword = new System.Windows.Controls.MenuItem { Header = "Change Password" };
			changePassword.Click += (_, _) =>
				new ATMML.Auth.ChangePasswordDialog { Owner = System.Windows.Window.GetWindow(this) }.ShowDialog();
			menu.Items.Add(changePassword);

			if (ATMML.Auth.AuthContext.Current.CanManageUsers)
			{
				var manageUsers = new System.Windows.Controls.MenuItem { Header = "User Management" };
				manageUsers.Click += (_, _) =>
					new ATMML.Auth.UserManagementWindow { Owner = System.Windows.Window.GetWindow(this) }.ShowDialog();
				menu.Items.Add(manageUsers);
			}

			menu.Items.Add(new System.Windows.Controls.Separator());

			var logout = new System.Windows.Controls.MenuItem { Header = "Logout" };
			logout.Click += (_, _) =>
			{
				var confirm = MessageBox.Show(
					"Are you sure you want to exit?",
					"Exit ATMML",
					MessageBoxButton.YesNo,
					MessageBoxImage.Question);
				if (confirm == MessageBoxResult.Yes)
				{
					ATMML.Auth.AuthContext.Current.Logout();
					Application.Current.Shutdown();
				}
			};
			menu.Items.Add(logout);

			menu.PlacementTarget = BtnSettings;
			menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
			menu.IsOpen = true;
		}
	}
	class AlertPrintDocument : PrintDocument
	{
		private Alert _alert;
		private DateTime _dateTime;

		//public AlertPrintDocument(Alert alert, DateTime dateTime)
		//{
		//     _alert = alert;
		//     _dateTime = dateTime;
		//}


	}
}