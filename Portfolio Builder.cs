using ATMML.Compliance;
using ATMML.Performance;
using Google.Protobuf.WellKnownTypes;
using HedgeFundReporting;
using iTextSharp.text.pdf.parser;
using LoadingControl.Control;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.CodeAnalysis;
using Microsoft.FSharp.Control;
using Microsoft.ML.Probabilistic.Collections;
using Microsoft.Win32;
using OPTANO.Modeling.Optimization;
using RiskEngine2;
using RiskEngine3;
using RiskEngineMNParity;

//using Microsoft.Windows.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
//using System.Web.UI.WebControls;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Tensorflow.Operations.Initializers;
using static Microsoft.ML.Data.SchemaDefinition;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TaskbarClock;
using Label = System.Windows.Controls.Label;
using ListBox = System.Windows.Controls.ListBox;
using Orientation = System.Windows.Controls.Orientation;
using TextBox = System.Windows.Controls.TextBox;
using TreeView = System.Windows.Controls.TreeView;

namespace ATMML
{
	public sealed class TickerInfo
	{
		public string Ticker { get; init; } = "";
		public DateTime StartDate { get; init; }
		public DateTime EndDate { get; init; }
		public string SectorTicker { get; init; }
	}

	public partial class PortfolioBuilder : ContentControl, INotifyPropertyChanged
	{
		public event PropertyChangedEventHandler PropertyChanged;

		protected virtual void OnPropertyChanged(string name)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
		}

		static IdeaCalculator _ic = null;

		MainView _mainView = null;

		string _view = "Trade Overview";
		string _symbol = "";
		int _monthlySummaryYear = 2014;
		int _monthlySummaryMonth = 1;
		string _tradeOverviewGroup = "Open";

		string _benchmarksymbol = "";
		string _rebalancePeriod = "";
		string _rankingType = "";

		string _selectedRegion = "";
		string _selectedCountry = "";
		string _selectedGroup = "";
		string _selectedSubgroup = "";
		string _selectedIndustry = "";

		string _time = "Daily";

		string _portfolioRequested = "";
		int _portfolioRequestedCount = 0;
		Portfolio.PortfolioType _type;

		Portfolio.PortfolioType _clientPortfolioType = Portfolio.PortfolioType.Index;
		string _clientPortfolioName = "";

		string _entryDate = "";
		double _xClick = 0;

		bool _calculating;

		private string _strategy = "Strategy 1";

		Navigation nav = new Navigation();

		List<Trade> _allTrades = new List<Trade>();
		List<Trade> _trades = new List<Trade>();
		List<Trade> _positions = new List<Trade>();

		Dictionary<string, SummaryInfo> _summary = new Dictionary<string, SummaryInfo>();

		int _update = 0;
		private DateTime _lastGridRefreshTime = DateTime.MinValue;
		private DateTime _lastPerfCursorTime = default(DateTime); // last cursor time user explicitly set on perf chart
		private double _liveMtMBalance = 0.0; // cached live MtM balance, updated by drawPortfolioGrid
		private Dictionary<string, (double price, DateTime ts)> _rtPrices = new Dictionary<string, (double, DateTime)>(); // PRICE_LAST_RT per ticker — timestamped for 3-min TTL
		private HashSet<string> _pendingRtTickers = new HashSet<string>(); // tickers waiting for Symbol event before RT subscribe
		private Dictionary<string, double> _lastKnownPrices = new Dictionary<string, double>(); // latest prices from drawPortfolioGrid
		DispatcherTimer _portfolioTimer = new DispatcherTimer();
		DispatcherTimer _testTimer = new DispatcherTimer();

		private Stopwatch _doubleClickStopwatch = null;

		Portfolio _portfolio1 = new Portfolio(7);
		Portfolio _livePortfolio = new Portfolio(47); // dedicated instance for PRICE_LAST_RT subscriptions
		Dictionary<string, int> _sizes = new Dictionary<string, int>();

		Portfolio _filterPortfolio = new Portfolio(41);

		PortfolioDialog _portfolioDialog;

		bool _addFlash = false;

		BarCache _barCache;
		string _interval = "Daily";

		List<string> _indexSymbols = new List<string>();

		string _selectedSector = "";

		FundamentalGraph _mainReturnGraph;
		FundamentalGraph _monthlyReturnGraph;
		System.Windows.Controls.ToolTip _histTooltip; // floating tooltip over histogram overlay

		private Dictionary<string, string> sectorPercents;
		private Dictionary<string, string> industryPercents;
		private Dictionary<string, string> subIndustryPercents;
		private string _activeLabel = "Sector";
		private double _lastGrossExposure = 0;
		private double _lastNetExposure = 0;
		private double _lastLongExposure = 0;
		private double _lastShortExposure = 0;

		private string SnapshotModelParams(Model model)
		{
			if (model == null) return "";
			return $"Rebalance={model.RankingInterval}" +
				   $"|Leverage={model.Groups[_g].Leverage}" +
				   $"|InitialNAV={model.InitialPortfolioBalance}" +
				   $"|MgmtFee={model.ManagementFee}" +
				   $"|PerfFee={model.PerformanceFee}" +
				   $"|PriceImpact={model.PriceImpactAmt}" +
				   $"|UseRiskEngine2={model.UseRiskEngine2}" +
				   $"|UseRiskEngine3={model.UseRiskEngine3}" +
				   $"|UseHedge={model.UseHedge}" +
				   $"|UseBetaHedge={model.UseBetaHedge}" +
				   $"|Ranking={model.Ranking}" +
				   $"|PortfolioWeight={model.PortfolioWeight}" +
				   $"|UseExecutionCost={model.UseExecutionCost}" +
				   $"|UseBorrowingCost={model.UseBorrowingCost}";
		}

		public class NameValueRow
		{
			public string Name { get; set; }
			public string Value { get; set; }
			public string ValueForeground { get; set; } = "#FFFFFF";
		}

		bool _initializing = false;

		public PortfolioBuilder(MainView mainView, string portfolioName = "")
		{
			InitializeComponent();
			DataContext = this;
			_mainView = mainView;

			if (_mainView == null)
				Console.WriteLine("⚠️ MainView was null (testing mode)");

			Loaded += PortfolioBuilder_Loaded;

			LoadGrid("Sector", sectorPercents);

			_mainReturnGraph = new FundamentalGraph(PositionsChart);
			_monthlyReturnGraph = new FundamentalGraph(TotalReturnGraph);
			_monthlyReturnGraph.ShowTimeScale = false;
			// Histogram cursor fix — overlay approach:
			// FundamentalGraph registers its cursor-draw handlers with handledEventsToo=true,
			// so setting e.Handled = true on PreviewMouseMove does not stop it.
			// Solution: place a transparent Canvas overlay ON TOP of TotalReturnGraph inside
			// TotalReturnChartBorder.  The overlay captures all mouse input first.
			// FundamentalGraph's handlers on TotalReturnGraph beneath it never fire.
			{
				var histGrid = new Grid();
				TotalReturnChartBorder.Child = null;          // detach Canvas from Border
				histGrid.Children.Add(TotalReturnGraph);     // Canvas at bottom (visual)
				var histOverlay = new System.Windows.Controls.Canvas
				{
					Background = System.Windows.Media.Brushes.Transparent,
					IsHitTestVisible = true
				};
				// Floating tooltip — restores the legacy mouseover behavior.
				_histTooltip = new System.Windows.Controls.ToolTip
				{
					Placement       = System.Windows.Controls.Primitives.PlacementMode.Relative,
					HasDropShadow   = true,
					Background      = System.Windows.Media.Brushes.Black,
					Foreground      = System.Windows.Media.Brushes.White,
					BorderBrush     = new System.Windows.Media.SolidColorBrush(
						System.Windows.Media.Color.FromRgb(0x00, 0xcc, 0xff)),
					BorderThickness = new System.Windows.Thickness(1),
					Padding         = new System.Windows.Thickness(6, 3, 6, 3),
					FontSize        = 11
				};
				histOverlay.ToolTip = _histTooltip;
				System.Windows.Controls.ToolTipService.SetInitialShowDelay(histOverlay, 0);
				System.Windows.Controls.ToolTipService.SetBetweenShowDelay(histOverlay, 0);
				System.Windows.Controls.ToolTipService.SetShowDuration(histOverlay, 60000);
				histOverlay.MouseMove  += Histogram_PreviewMouseMove;
				histOverlay.MouseLeave += Histogram_MouseLeave;
				histGrid.Children.Add(histOverlay);          // overlay on top — absorbs mouse
				TotalReturnChartBorder.Child = histGrid;
			}
			// GraphEvent not subscribed — hover is handled manually above.
			// _monthlyReturnGraph.GraphEvent += _monthlyReturnGraphEvent;
			_mainReturnGraph.GraphEvent += _mainReturnGraphEvent;


			_mainReturnGraph.PropertyChanged += _mainReturnGraph_PropertyChanged;

			string info = _mainView?.GetInfo("FundamentalML");
			if (info != null && info.Length > 0)
			{
				string[] fields = info.Split(';');
				int count = fields.Length;
				if (count > 0) _view = fields[0];
				if (count > 1) _symbol = fields[1];
				if (count > 2) _tradeOverviewGroup = fields[2];
				if (count > 3) _monthlySummaryYear = int.Parse(fields[3]);
				if (count > 4) _monthlySummaryMonth = int.Parse(fields[4]);
				if (count > 5) _selectedSector = fields[5];
				if (count > 6) _selectedUserFactorModel = fields[6];
				if (count > 7) _selectedMLFactorModel = fields[7];
				if (count > 8) bool.TryParse(fields[8], out _useUserFactorModel);
				if (count > 9) _modelName = fields[9];
			}

			var model = MainView.GetModel(_modelName);
			var modelName = (model != null) ? _modelName : "";
			var scenario = (model != null) ? MainView.GetSenarioLabel(model.Scenario) : "";
			ScenarioModel.Content = modelName;
			ScenarioName.Content = scenario;

			_useUserFactorModel = true;
			var visibility = (MainView.EnableRickStocks) ? Visibility.Visible : Visibility.Collapsed;

			PortfolioInput2.Visibility = MainView.EnableNetworks ? Visibility.Visible : Visibility.Collapsed;
			PortfolioInput3.Visibility = MainView.EnableNetworks ? Visibility.Visible : Visibility.Collapsed;
			PortfolioInput4.Visibility = MainView.EnableNetworks ? Visibility.Visible : Visibility.Collapsed;
			PortfolioInput5.Visibility = MainView.EnableNetworks ? Visibility.Visible : Visibility.Collapsed;

			UserFactorModelGrid.Visibility = _useUserFactorModel ? Visibility.Visible : Visibility.Collapsed;
			PerformanceGrid.Visibility = Visibility.Collapsed;
			PerformanceSideNav.Visibility = Visibility.Collapsed;
			ATMStrategyOverlay.Visibility = Visibility.Collapsed;
			RebalanceChartGrid.Visibility = Visibility.Collapsed;
			RebalanceGrid.Visibility = Visibility.Collapsed;
			RebalanceSideNav.Visibility = Visibility.Collapsed;
			AllocationGrid.Visibility = Visibility.Collapsed;
			AllocationSideNav.Visibility = Visibility.Collapsed;
			SetupNav.Visibility = Visibility.Collapsed;

			initConditionTree();
			initFeatureTree(ATMMLFeatureTree);
			initStrategyTree();
			updateNavigation();
			setModelRadioButtons();

			_barCache = new BarCache(barChanged);

			ResourceDictionary dictionary = new ResourceDictionary();
			dictionary.Source = new Uri("pack://application:,,,/ATMML;component/StyleDictionary.xaml");
			this.Resources.MergedDictionaries.Add(dictionary);

			Trade.Manager.TradeEvent += new TradeEventHandler(TradeEvent);
			Trade.Manager.NewPositions += new NewPositionEventHandler(new_Positions);
			_portfolio1.PortfolioChanged += new PortfolioChangedEventHandler(portfolio1Changed);
			_livePortfolio.PortfolioChanged += new PortfolioChangedEventHandler(livePortfolioChanged);
			_filterPortfolio.PortfolioChanged += _filterPortfolio_PortfolioChanged;

			_portfolioTimer = new DispatcherTimer();
			_portfolioTimer.Interval = TimeSpan.FromMilliseconds(500);
			_portfolioTimer.Tick += new EventHandler(Timer_tick);
			_portfolioTimer.Start();

			_MLFactorModels = new Dictionary<string, Model>();
			_userFactorModels = new Dictionary<string, Model>();

			initFactorTree(UserFactorInputTree);
			loadUserFactorModels();

			if (_selectedUserFactorModel == "")
			{
				//loadExampleModel();
			}

			hideNavigation();
			showView();
			loadModel();
			highlightStats();
			highlightSectors();

			_strategies = new Dictionary<string, Strategy>();
			_selectedStrategy = (_strategies.Count == 0) ? "" : _strategies.First().Key;
			updateStrategyTree();

			if (!BarServer.ConnectedToBloomberg())
			{
				//RestrictionTitle.Visibility = Visibility.Collapsed;
				//ApplyRestrictions.Visibility = Visibility.Collapsed;
				//UseFilters.Visibility = Visibility.Collapsed;
				//FilterInputGray.Visibility = Visibility.Collapsed;
				//RankingTitle.Visibility = Visibility.Collapsed;
				//ApplyRanking.Visibility = Visibility.Collapsed;
				//UseRankingInputs.Visibility = Visibility.Collapsed;
				//RankInputGray.Visibility = Visibility.Collapsed;
			}

			//PerformanceReporting.Tests.SmokeTests.Test();
		}

		private void _mainReturnGraph_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			showRebalanceGrid();
			setHoldingsCursorTime(_mainReturnGraph.CursorTime);
		}


		DateTime _showHoldingsTime;
		DateTime _projectedRebalDate;

		// True after "Send Orders to FlexOne" is pressed on a live Friday.
		// Resets to false each time the model is re-run so the user can preview
		// potential trades before the 3:00 PM deadline on subsequent runs.
		bool _liveOrdersSent = false;

		// FlexOne rebalance state — populated by drawPortfolioGrid() on pre-order Friday,
		// consumed by SendOrders_MouseDown() when the button is pressed after 3:00 PM.
		private List<Trade> _flexEnteredTrades = new List<Trade>();
		private List<Trade> _flexExitedTrades = new List<Trade>();
		private List<Trade> _flexAddTrades = new List<Trade>();
		private List<Trade> _flexReduceTrades = new List<Trade>();
		private DateTime _flexTradesTime1;
		private DateTime _flexTradesTime2;
		private Dictionary<string, double> _flexShares = new Dictionary<string, double>();
		private Dictionary<string, double> _flexPrice = new Dictionary<string, double>();

		// Returns true when we are on a live rebalance Friday BEFORE 3:00 PM and
		// orders have not yet been sent.  DataGrid1, counts, and balance must all
		// reflect the CURRENT (pre-rebalance) portfolio until one of these is true:
		//   (a) the clock passes 3:00 PM  — gate lifts automatically, or
		//   (b) Send Orders to FlexOne is pressed before 3:00 PM (not allowed).
		private bool IsLiveFridayPreOrder()
		{
			var model = getModel();
			return model != null
				&& model.IsLiveMode
				&& DateTime.Today.DayOfWeek == DayOfWeek.Friday
				&& DateTime.Now.TimeOfDay < TimeSpan.FromHours(15)   // before 3:00 PM
				&& !_liveOrdersSent;
		}

		// Extends IsLiveFridayPreOrder() to cover Mon–Thu intra-week days.
		// On any live weekday where the next rebalance has not yet executed,
		// DataGrid1, counts, and balance must show the LAST CONFIRMED snapshot
		// (time1 / oldTrades), not the projected next-Friday portfolio.
		private bool IsLivePreRebalance()
		{
			var model = getModel();
			if (model == null || !model.IsLiveMode) return false;

			var dow = DateTime.Today.DayOfWeek;

			// Mon–Thu: check if the most recent Friday already has a settled snapshot.
			// If it does, orders were executed and we should show the new portfolio (not pre-rebalance).
			if (dow == DayOfWeek.Monday || dow == DayOfWeek.Tuesday ||
				dow == DayOfWeek.Wednesday || dow == DayOfWeek.Thursday)
			{
				// Find the most recent Friday
				// Mon=1 → 3 days back, Tue=2 → 4, Wed=3 → 5, Thu=4 → 6
				int daysToFriday = ((int)dow + 2);
				var lastFriday = DateTime.Today.AddDays(-daysToFriday);
				// If portfolioTimes contains that Friday (settled snapshot exists), orders already done
				if (_portfolioTimes != null && _portfolioTimes.Any(t => t.Date == lastFriday.Date))
					return false;  // rebalance already executed — show new portfolio
				return true;
			}

			// Friday before 3 PM and orders not yet sent.
			return dow == DayOfWeek.Friday
				&& DateTime.Now.TimeOfDay < TimeSpan.FromHours(15)
				&& !_liveOrdersSent;
		}

		// Key used to persist the orders-sent state across re-runs within the same day.
		private string OrdersSentKey =>
			$"LiveOrdersSent_{getModel()?.Name}_{DateTime.Today:yyyy-MM-dd}";

		private void LoadOrdersSentFlag()
		{
			try { _liveOrdersSent = MainView.LoadUserData(OrdersSentKey) == "sent"; }
			catch { _liveOrdersSent = false; }
		}

		private void SaveOrdersSentFlag()
		{
			try { MainView.SaveUserData(OrdersSentKey, "sent"); } catch { }
		}

		string _portfolioResultsTimestamp = "";
		double _lastSharpe = double.NaN; // Authoritative Sharpe from live portfolio stats
		string _lastSharpePortfolio = ""; // Portfolio name when _lastSharpe was set
		Dictionary<string, double> _portfolioResults = new Dictionary<string, double>();

		private Dictionary<string, double> getPortfolioResults()
		{
			var model = getModel();
			var portfolioName = (model == null) ? "" : model.Name;
			var rootPath = @"C:\Users\Public\Documents\ATMML\portfolios\reports\" + portfolioName;
			var folders = new List<string>();
			try
			{
				folders = Directory.GetDirectories(rootPath).ToList();
			}
			catch (Exception x)
			{
				//Debug.WriteLine($"[PortfolioResults] ERROR getting directories for '{rootPath}': {x.Message}");
			}

			// Use most recent timestamp subfolder if present, otherwise fall back to root folder
			string timestamp;
			if (folders.Count > 0)
			{
				folders.Sort();
				timestamp = folders.Last();
			}
			else
			{
				timestamp = rootPath; // files sit directly in reports folder (no archive subfolder)
			}

			//Debug.WriteLine($"[PortfolioResults] portfolio='{portfolioName}' folders={folders.Count} timestamp='{timestamp}' cached='{_portfolioResultsTimestamp}'");

			if (timestamp != _portfolioResultsTimestamp)
			{
				_portfolioResultsTimestamp = timestamp;

				var output = new Dictionary<string, double>();

				// Support both prefixed (OEX V2 DUP_GIPSReport.txt) and unprefixed (GIPSReport.txt)
				var gipsPath = timestamp + @"\" + portfolioName + "_GIPSReport.txt";
				if (!File.Exists(gipsPath))
					gipsPath = timestamp + @"\GIPSReport.txt";

				var campaignPath = timestamp + @"\" + portfolioName + "_CampaignWinLoss.txt";
				if (!File.Exists(campaignPath))
					campaignPath = timestamp + @"\CampaignWinLoss.txt";

				//Debug.WriteLine($"[PortfolioResults] GIPSReport exists={File.Exists(gipsPath)} path='{gipsPath}'");
				//Debug.WriteLine($"[PortfolioResults] CampaignWinLoss exists={File.Exists(campaignPath)} path='{campaignPath}'");
				if (File.Exists(campaignPath))
				{
					foreach (string line in File.ReadLines(campaignPath))
					{
						// Campaign Level Statistics
						if (line.StartsWith("Winning Campaigns:"))
						{
							string valuePart = line.Split(':')[1].Replace("%", "").Trim();
							double winningCampaigns = double.Parse(valuePart, CultureInfo.InvariantCulture);
							output["LongHoldings2a"] = winningCampaigns;
						}
						else if (line.StartsWith("Losing Campaigns:"))
						{
							string valuePart = line.Split(':')[1].Replace("%", "").Trim();
							double losingCampaigns = double.Parse(valuePart, CultureInfo.InvariantCulture);
							output["ShortHoldings2a"] = losingCampaigns;
						}
						else if (line.StartsWith("Total Campaigns Analyzed:"))
						{
							string valuePart = line.Split(':')[1].Replace("%", "").Trim();
							double totalCampaigns = double.Parse(valuePart, CultureInfo.InvariantCulture);
							output["TotalCampaigns"] = totalCampaigns;
						}
						else if (line.StartsWith("Campaign Win Rate:"))
						{
							string valuePart = line.Split(':')[1].Replace("%", "").Trim();
							double winRate = double.Parse(valuePart, CultureInfo.InvariantCulture);
							output["winRate"] = winRate;
						}
						else if (line.StartsWith("Profit Factor:"))
						{
							string valuePart = line.Split(':')[1].Replace("%", "").Trim();
							double profitFactor = double.Parse(valuePart, CultureInfo.InvariantCulture);
							output["profitFactor"] = profitFactor;
						}
						else if (line.StartsWith("Avg Win/Loss Ratio:"))
						{
							string valuePart = line.Split(':')[1].Replace("%", "").Trim();
							double avgWinLoss = double.Parse(valuePart, CultureInfo.InvariantCulture);
							output["winLossRatio"] = avgWinLoss;
						}
						else if (line.StartsWith("Average Holding Period (Winners):"))
						{
							string valuePart = line.Split(':')[1]
												   .Replace("days", "", StringComparison.OrdinalIgnoreCase)
												   .Trim();

							if (double.TryParse(valuePart, NumberStyles.Any, CultureInfo.InvariantCulture, out double avgHoldWin))
							{
								output["avgHoldWin"] = avgHoldWin;   // e.g., 24.9
							}
						}
						else if (line.StartsWith("Average Holding Period (Losers):"))
						{
							string valuePart = line.Split(':')[1]
												   .Replace("days", "", StringComparison.OrdinalIgnoreCase)
												   .Trim();

							if (double.TryParse(valuePart, NumberStyles.Any, CultureInfo.InvariantCulture, out double avgHoldLoss))
							{
								output["avgHoldLoss"] = avgHoldLoss;   // e.g., 24.9
							}
						}

						// winning campaigns
						else if (line.StartsWith("Average Win Return:"))   // 5.39%
						{
							string valuePart = line.Split(':')[1].Replace("%", "").Trim();
							double avgWinReturn = double.Parse(valuePart, CultureInfo.InvariantCulture);
							output["avgWinWinner"] = avgWinReturn;
						}
						else if (line.StartsWith("Average Winning Campaign Duration:"))
						{
							string valuePart = line.Split(':')[1]
												   .Replace("days", "", StringComparison.OrdinalIgnoreCase)
												   .Trim();

							if (double.TryParse(valuePart, NumberStyles.Any, CultureInfo.InvariantCulture, out double avgHoldWinCampaign))
							{
								output["avgHoldWinCampaign"] = avgHoldWinCampaign;   // e.g., 24.9
							}
						}
						else if (line.StartsWith("Average Adds per Winning Campaign:"))  // 9.9
						{
							string valuePart = line.Split(':')[1].Replace("%", "").Trim();
							double avgAddWinner = double.Parse(valuePart, CultureInfo.InvariantCulture);
							output["addOnWinner"] = avgAddWinner;
						}
						else if (line.StartsWith("Average Trims per Winning Campaign:"))  // 10.9
						{
							string valuePart = line.Split(':')[1].Replace("%", "").Trim();
							double avgtrimsWinner = double.Parse(valuePart, CultureInfo.InvariantCulture);
							output["trimsOnWinner"] = avgtrimsWinner;
						}

						// Losing Campaigns
						else if (line.StartsWith("Average Loss Return:"))   // -4.1%
						{
							string valuePart = line.Split(':')[1].Replace("%", "").Trim();
							double avgLossLosing = double.Parse(valuePart, CultureInfo.InvariantCulture);
							output["avgLossLosing"] = avgLossLosing;
						}
						else if (line.StartsWith("Average Losing Campaign Duration:"))
						{
							string valuePart = line.Split(':')[1]
												   .Replace("days", "", StringComparison.OrdinalIgnoreCase)
												   .Trim();

							if (double.TryParse(valuePart, NumberStyles.Any, CultureInfo.InvariantCulture, out double avgHoldLossCampaign))
							{
								output["avgHoldLosingCampaign"] = avgHoldLossCampaign;   // e.g., 24.9
							}
						}
						else if (line.StartsWith("Average Adds per Losing Campaign:"))  // 9.9
						{
							string valuePart = line.Split(':')[1].Replace("%", "").Trim();
							double avgAddLosing = double.Parse(valuePart, CultureInfo.InvariantCulture);
							output["addOnLosing"] = avgAddLosing;
						}
						else if (line.StartsWith("Average Trims per Losing Campaign:"))  // 10.9
						{
							string valuePart = line.Split(':')[1].Replace("%", "").Trim();
							double avgtrimsLosing = double.Parse(valuePart, CultureInfo.InvariantCulture);
							output["trimsOnLosing"] = avgtrimsLosing;
						}
					}
				}
				if (File.Exists(gipsPath))
				{
					foreach (string line in File.ReadLines(gipsPath))
					{
						if (line.StartsWith("Annualized Return:"))
						{
							string valuePart = line.Split(':')[1].Replace("%", "").Trim();
							double annReturn = double.Parse(valuePart, CultureInfo.InvariantCulture);
							output["annRet1"] = annReturn;
						}
						else if (line.StartsWith("Annualized Volatility:"))
						{
							string valuePart = line.Split(':')[1].Replace("%", "").Trim();
							double annVol = double.Parse(valuePart, CultureInfo.InvariantCulture);
							output["annVol1"] = annVol;
						}
						else if (line.StartsWith("Sharpe Ratio:"))
						{
							string valuePart = line.Split(':')[1].Replace("%", "").Trim();
							double sharpeRatio2 = double.Parse(valuePart, CultureInfo.InvariantCulture);
							output["sharpeRatio"] = sharpeRatio2;
						}
						else if (line.StartsWith("Maximum Drawdown:"))
						{
							string valuePart = line.Split(':')[1].Replace("%", "").Trim();
							double maxDD = double.Parse(valuePart, CultureInfo.InvariantCulture);
							output["maxDD1"] = maxDD;
						}
						else if (line.StartsWith("Sortino Ratio:"))
						{
							string valuePart = line.Split(':')[1].Replace("%", "").Trim();
							double sortino = valuePart.Contains("∞")
								? double.PositiveInfinity
								: double.Parse(valuePart, CultureInfo.InvariantCulture);
							output["sortinoRatio"] = sortino;
						}
						else if (line.StartsWith("Calmar Ratio:"))
						{
							string valuePart = line.Split(':')[1].Replace("%", "").Trim();
							double calmar = double.Parse(valuePart, CultureInfo.InvariantCulture);
							output["calmarRatio"] = calmar;
						}
					}
				}



				_portfolioResults = output;
			}
			return _portfolioResults;
		}

		private void _mainReturnGraphEvent(object sender, GraphEventArgs e)
		{
			if (e.CursorTime != default(DateTime))
				_lastPerfCursorTime = e.CursorTime;
			updateStatistics();
			// Don't sync cursor to histogram -- no vertical cursor line on bottom chart
		}

		private void _monthlyReturnGraphEvent(object sender, GraphEventArgs e)
		{
			// Intentionally empty — hover is handled by Histogram_PreviewMouseMove.
		}

		/// <summary>
		/// Fires before FundamentalGraph sees the mouse-move event, so the internal
		/// cursor line is never drawn.  Computes the hovered month from x position and
		/// updates the monthly-return label directly.
		/// </summary>
		private void Histogram_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
		{
			// sender = transparent overlay Canvas on top of TotalReturnGraph.
			// Populate the floating _histTooltip with monthly return of the hovered bar.
			var el = sender as System.Windows.FrameworkElement;
			if (el == null || _histTooltip == null) return;

			var w = el.ActualWidth;
			if (w <= 0 || _portfolioMonthTimes == null || _portfolioMonthTimes.Count == 0)
			{
				_histTooltip.IsOpen = false;
				return;
			}

			var pos = e.GetPosition(el);
			var idx = (int)(pos.X / w * _portfolioMonthTimes.Count);
			idx = Math.Max(0, Math.Min(idx, _portfolioMonthTimes.Count - 1));

			var model    = getModel();
			var useHedge = model != null && isTradingModel(model.Name) && _useHedgeCurve;
			var monthVals = useHedge ? _portfolioHedgeMonthValues : _portfolioMonthValues;

			if (monthVals != null && idx < monthVals.Count && !double.IsNaN(monthVals[idx]))
			{
				var val = monthVals[idx];
				var t   = (idx < _portfolioMonthTimes.Count) ? _portfolioMonthTimes[idx] : default(DateTime);
				var label = (t == default(DateTime))
					? val.ToString("##0.00") + "%"
					: t.ToString("yyyy-MM") + ": " + val.ToString("##0.00") + "%";
				_histTooltip.Content          = label;
				_histTooltip.HorizontalOffset = pos.X + 12;
				_histTooltip.VerticalOffset   = pos.Y + 12;
				_histTooltip.IsOpen           = true;
			}
			else
			{
				_histTooltip.IsOpen = false;
			}
		}

		private void Histogram_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
		{
			// Hide the floating tooltip when mouse leaves the histogram.
			if (_histTooltip != null)
				_histTooltip.IsOpen = false;
		}

		private double getHoldings(DateTime time, int side, int column, bool all = false)
		{
			double output = 0;
			Model model = getModel((column == 1) ? _selectedUserFactorModel : _selectedUserFactorModel2);
			if (model != null)
			{
				var trades = _portfolio1.GetTrades(model.Name, "", time, all);
				// Normalise to ±1 — only count active hold positions, not entry/exit markers
				foreach (var item in trades)
				{
					int dir = Math.Sign((int)item.Direction);
					if (dir == 0) continue;
					if (Math.Sign(side) == dir) output++;
				}
			}
			return output;
		}

		private void updateTradeStats(DateTime time, int direction, int column)
		{
			Model model = getModel((column == 1) ? _selectedUserFactorModel : _selectedUserFactorModel2);
			if (model != null)
			{
				int winCount = 0;
				double winTotal = 0;
				int lossCount = 0;
				double lossTotal = 0;
				int durationTotal = 0;

				int eClWinCount = 0;
				int eClLossCount = 0;
				double eClWinTotal = 0;
				double eClLossTotal = 0;
				int eClDurationTotal = 0;
				int eClLongCount = 0;
				int eClShortCount = 0;
				double clSum1 = 0;
				double clSum2 = 0;
				double csSum1 = 0;
				double csSum2 = 0;

				int eTotalWinCount = 0;
				double eTotalWinTotal = 0;
				int eTotalDurationTotal = 0;
				int eTotalShortCount = 0;

				bool hedgeCurve = (isTradingModel(model.Name) && _useHedgeCurve);

				// Pre-order Friday: show current balance until orders are sent.
				var balDisplayTime = (IsLivePreRebalance()
					&& time.Date >= DateTime.Today
					&& _portfolioTimes.Count >= 2)
					? _portfolioTimes[_portfolioTimes.Count - 2]
					: time;
				// Use live MtM only for dates AFTER the last settled Friday (today, projected rebal).
				// The settled Friday itself shows its locked NAV from portfolioValues.
				var mostRecentSettledFriday = _portfolioTimes != null
					? _portfolioTimes.Where(t => t != default(DateTime) && t.Date <= DateTime.Today && t.DayOfWeek == DayOfWeek.Friday)
						.OrderByDescending(t => t).FirstOrDefault()
					: default(DateTime);
				// IsViewingLive() handles intra-week: last Friday cursor IS the live view.
				// The old >= DateTime.Today check always returned false Mon-Thu,
				// causing updateTradeStats to overwrite Balance with the static historical NAV.
				var isViewingLiveDate = model.IsLiveMode && IsViewingLive(time);
				var portfolioBalance = (isViewingLiveDate && _liveMtMBalance > 0) ? _liveMtMBalance : getPortfolioBalance(balDisplayTime);
				Balance.Content = "$ " + portfolioBalance.ToString("#,##0");
				Balance2.Content = "$ " + portfolioBalance.ToString("#,##0");
				Balance3.Content = "$ " + portfolioBalance.ToString("##,##0");

				var trades = _portfolio1.GetTrades(model.Name, "", time, true);
				foreach (var trade in trades)
				{
					if (direction == 0 || Math.Sign(trade.Direction) == direction)
					{
						var entryPrice = trade.EntryPrice;
						var exitPrice = trade.ExitPrice;

						if (double.IsNaN(exitPrice))
						{
							exitPrice = getPrice(trade.Ticker, exitPrice, trade.CloseDateTime);
						}

						if (!double.IsNaN(entryPrice) && !double.IsNaN(exitPrice))
						{
							var investment = trade.Investment1;
							var tradeDir = Math.Sign(trade.Direction);

							//var hedgePercent = 0.0;
							//if (hedgeCurve)
							//{
							//    var bars = getBars(model.Benchmark, "D", BarServer.MaxBarCount);
							//    var hedgeEntryPrice = getPrice(bars, trade.OpenDateTime);
							//    var hedgeExitPrice = getPrice(bars, trade.CloseDateTime);
							//    hedgePercent = getReturn(model.Benchmark, -tradeDir,  hedgeEntryPrice, hedgeExitPrice); // 100 * -tradeDir * (hedgeExitPrice - hedgeEntryPrice) / hedgeEntryPrice;
							//}

							var equityPercent = model.Groups[_g].Leverage * getReturn(trade.Ticker, tradeDir, entryPrice, exitPrice); // 100 * tradeDir * (exitPrice - entryPrice) / entryPrice;
							double percent = equityPercent;// + hedgePercent;

							if (percent < 0)
							{
								lossCount++;
								lossTotal -= percent;
							}
							else
							{
								winCount++;
								winTotal += percent;
							}

							int duration = getDayCount(trade.EntryDate, trade.ExitDate);
							durationTotal += duration;
						}
					}
				}
				var totalCount = winCount + lossCount;

				var results = getPortfolioResults();


				var annRet1Value = results.ContainsKey("annRet1") ? results["annRet1"] : double.NaN;
				var annVol1value = results.ContainsKey("annVol1") ? results["annVol1"] : double.NaN;
				var maxDD1Value = results.ContainsKey("maxDD1") ? results["maxDD1"] : double.NaN;
				var sharpeRatioValue2 = results.ContainsKey("sharpeRatio") ? results["sharpeRatio"] : double.NaN;
				var sortinoValue = results.ContainsKey("sortinoRatio") ? results["sortinoRatio"] : double.NaN;
				var calmarValue = results.ContainsKey("calmarRatio") ? results["calmarRatio"] : double.NaN;

				var totalCampaigns = results.ContainsKey("TotalCampaigns") ? results["TotalCampaigns"] : double.NaN;
				var campaignWinRate = results.ContainsKey("winRate") ? results["winRate"] : double.NaN;
				var profitFactorValue = results.ContainsKey("profitFactor") ? results["profitFactor"] : double.NaN;
				var winLossRatioValue = results.ContainsKey("winLossRatio") ? results["winLossRatio"] : double.NaN;
				var avgHoldWinValue = results.ContainsKey("avgHoldWin") ? results["avgHoldWin"] : double.NaN;
				var avgHoldLossValue = results.ContainsKey("avgHoldLoss") ? results["avgHoldLoss"] : double.NaN;

				var winningCampaigns = results.ContainsKey("LongHoldings2a") ? results["LongHoldings2a"] : double.NaN;
				var avgWinWinnerValue = results.ContainsKey("avgWinWinner") ? results["avgWinWinner"] : double.NaN;
				var avgHoldWinnerValue = results.ContainsKey("avgHoldWinCampaign") ? results["avgHoldWinCampaign"] : double.NaN;
				var addsOnWinnerValue = results.ContainsKey("addOnWinner") ? results["addOnWinner"] : double.NaN;
				var trimsOnWinnerValue = results.ContainsKey("trimsOnWinner") ? results["trimsOnWinner"] : double.NaN;

				var losingCampaigns = results.ContainsKey("ShortHoldings2a") ? results["ShortHoldings2a"] : double.NaN;
				var avgLossLosingValue = results.ContainsKey("avgLossLosing") ? results["avgLossLosing"] : double.NaN;
				var avgHoldLosingValue = results.ContainsKey("avgHoldLosingCampaign") ? results["avgHoldLosingCampaign"] : double.NaN;
				var addsOnLosingValue = results.ContainsKey("addOnLosing") ? results["addOnLosing"] : double.NaN;
				var trimsOnLosingValue = results.ContainsKey("trimsOnLosing") ? results["trimsOnLosing"] : double.NaN;

				if (direction == 0)
				{
					if (column == 1)
					{
						annRet1.Content = double.IsNaN(annRet1Value) ? "" : annRet1Value;
						annVol1.Content = double.IsNaN(annVol1value) ? "" : annVol1value;
						maxDD1.Content = double.IsNaN(maxDD1Value) ? "" : maxDD1Value;
						// File-read value (GIPSReport.txt) is authoritative — written at backtest completion with locked data
						// In-memory _lastSharpe may be stale from a previous run with different data
						sharpeRatio.Content = !double.IsNaN(sharpeRatioValue2)
							? sharpeRatioValue2
							: (!double.IsNaN(_lastSharpe) && _lastSharpePortfolio == _selectedUserFactorModel)
								? Math.Round(_lastSharpe, 2)
								: (object)"";
						sortinoRatio.Content = double.IsNaN(sortinoValue) ? "" : sortinoValue;
						calmarRatio.Content = double.IsNaN(calmarValue) ? "" : calmarValue;

						//sharpeRatioD.Content = double.IsNaN(sharpeRatioDValue) ? "" : sharpeRatioDValue.ToString("F2");						
						//                  sharpeRatioW.Content = double.IsNaN(sharpeRatioWValue) ? "" : sharpeRatioWValue.ToString("F2");
						//sharpeRatioM.Content = double.IsNaN(sharpeRatioMValue) ? "" : sharpeRatioMValue.ToString("F2");

						TotalCampaigns.Content = double.IsNaN(totalCampaigns) ? "" : totalCampaigns;
						winRate.Content = double.IsNaN(campaignWinRate) ? "" : campaignWinRate + " %";
						profitFactor.Content = double.IsNaN(profitFactorValue) ? "" : profitFactorValue;
						winLossRatio.Content = double.IsNaN(winLossRatioValue) ? "" : winLossRatioValue;
						avgHoldWin.Content = double.IsNaN(avgHoldWinValue) ? "" : avgHoldWinValue.ToString("0.0") + " d";
						avgHoldLoss.Content = double.IsNaN(avgHoldLossValue) ? "" : avgHoldLossValue.ToString("0.0") + " d";

						LongHoldings2a.Content = double.IsNaN(winningCampaigns) ? "" : winningCampaigns;
						avgWinWinner.Content = double.IsNaN(avgWinWinnerValue) ? "" : avgWinWinnerValue + " %";
						avgHoldWinner.Content = double.IsNaN(avgHoldWinnerValue) ? "" : avgHoldWinnerValue.ToString("0.0") + " d";
						addOnWinner.Content = double.IsNaN(addsOnWinnerValue) ? "" : addsOnWinnerValue;
						trimsOnWinner.Content = double.IsNaN(trimsOnWinnerValue) ? "" : trimsOnWinnerValue;

						ShortHoldings2a.Content = double.IsNaN(losingCampaigns) ? "" : losingCampaigns;
						avgLossLosing.Content = double.IsNaN(avgLossLosingValue) ? "" : avgLossLosingValue + " %";
						avgHoldLosing.Content = double.IsNaN(avgHoldLossValue) ? "" : avgHoldLossValue.ToString("0.0") + " d";
						addOnLosing.Content = double.IsNaN(addsOnLosingValue) ? "" : addsOnLosingValue;
						trimsOnLosing.Content = double.IsNaN(trimsOnLosingValue) ? "" : trimsOnLosingValue;
					}
					else
					{
						//defavgWinners.Content = (winCount > 0) ? Math.Floor((100.0 * winTotal) / winCount) / 100 + " %" : "";
						//defavgLosers.Content = (lossCount > 0) ? Math.Floor((100.0 * lossTotal) / lossCount) / 100 + " %" : "";
						//defavgHoldingPeriod.Content = (totalCount > 0) ? Math.Floor((100.0 * durationTotal) / totalCount) / 100 + " d" : "";
						//defwinLossRatio.Content = (totalCount > 0) ? Math.Floor((10000.0 * winCount) / totalCount) / 100 + " %" : "";
					}
				}
				else if (direction == 1)
				{
					if (column == 1)
					{
						//avgWinnersLongs.Content = (winCount > 0) ? Math.Floor((100.0 * winTotal) / winCount) / 100 + " %" : "";
						//avgLosersLongs.Content = (lossCount > 0) ? Math.Floor((100.0 * lossTotal) / lossCount) / 100 + " %" : "";
						//avgHoldingPeriodLongs.Content = (totalCount > 0) ? Math.Floor((100.0 * durationTotal) / totalCount) / 100 + " d" : "";
						//winLossRatioLongs.Content = (totalCount > 0) ? Math.Floor((10000.0 * winCount) / totalCount) / 100 + " %" : "";
						//longProfitFactor.Content = (totalCount > 0) ? Math.Floor((100.0 * winTotal) / lossTotal) / 100 + " %" : "";
					}
					else
					{
						//defavgWinnersLongs.Content = (winCount > 0) ? Math.Floor((100.0 * winTotal) / winCount) / 100 + " %" : "";
						//defavgLosersLongs.Content = (lossCount > 0) ? Math.Floor((100.0 * lossTotal) / lossCount) / 100 + " %" : "";
						//defavgHoldingPeriodLongs.Content = (totalCount > 0) ? Math.Floor((100.0 * durationTotal) / totalCount) / 100 + " d" : "";
						//defwinLossRatioLongs.Content = (totalCount > 0) ? Math.Floor((10000.0 * winCount) / totalCount) / 100 + " %" : "";
					}
				}
				if (direction == -1)
				{
					if (column == 1)
					{
						//avgWinnersShorts.Content = (winCount > 0) ? Math.Floor((100.0 * winTotal) / winCount) / 100 + " %" : "";
						//avgLosersShorts.Content = (lossCount > 0) ? Math.Floor((100.0 * lossTotal) / lossCount) / 100 + " %" : "";
						//avgHoldingPeriodShorts.Content = (totalCount > 0) ? Math.Floor((100.0 * durationTotal) / totalCount) / 100 + " d" : "";
						//winLossRatioShorts.Content = (totalCount > 0) ? Math.Floor((10000.0 * winCount) / totalCount) / 100 + " %" : "";
						//shortProfitFactor.Content = (totalCount > 0) ? Math.Floor((100.0 * winTotal) / lossTotal) / 100 + " %" : "";
					}
					else
					{
						//defavgWinnersShorts.Content = (winCount > 0) ? Math.Floor((100.0 * winTotal) / winCount) / 100 + " %" : "";
						//defavgLosersShorts.Content = (lossCount > 0) ? Math.Floor((100.0 * lossTotal) / lossCount) / 100 + " %" : "";
						//defavgHoldingPeriodShorts.Content = (totalCount > 0) ? Math.Floor((100.0 * durationTotal) / totalCount) / 100 + " d" : "";
						//defwinLossRatioShorts.Content = (totalCount > 0) ? Math.Floor((10000.0 * winCount) / totalCount) / 100 + " %" : "";
					}
				}

				Brush brush = hedgeCurve ? Brushes.White : new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));
				Brush brushPG = new SolidColorBrush(Color.FromRgb(0x98, 0xfb, 0x98));
				Brush brushC = new SolidColorBrush(Color.FromRgb(0xdc, 0x14, 0x3c));

				//avgWinners.Foreground = brushPG;
				//avgLosers.Foreground = brushC;
				//avgHoldingPeriod.Foreground = brush;
				winLossRatio.Foreground = brush;
				winRate.Foreground = brush;
				//campaignWinRate.Foreground = brush;
				//profitFactor.Foreground = brush;
				//totalProfitFactor.Foreground = brush;

				//avgWinnersLongs.Foreground = brushPG;
				//avgLosersLongs.Foreground = brushC;
				//avgHoldingPeriodLongs.Foreground = brush;
				//winLossRatioLongs.Foreground = brush;

				//avgWinnersShorts.Foreground = brushPG;
				//avgLosersShorts.Foreground = brushC;
				//avgHoldingPeriodShorts.Foreground = brush;
				//winLossRatioShorts.Foreground = brush;

			}
		}

		private double getPortfolioBalance(DateTime time)
		{
			if (time == default(DateTime)) return 0.0;
			var portfolioBalance = 0.0;
			Model model = getModel(_selectedUserFactorModel);
			if (model != null)
			{
				// Always load from model.Name — _selectedUserFactorModel2 may point to a different model
				var portfolioTimes = loadList<DateTime>(model.Name + " PortfolioTimes");
				var portfolioValues = loadList<double>(model.Name + " PortfolioValues");
				if (portfolioValues.Count == 0) return 0.0;

				var idx = portfolioTimes.FindIndex(x => x == time);
				if (idx == -1)
					idx = portfolioTimes.FindLastIndex(x => x.Date <= time.Date);

				// portfolioTimes may have a placeholder entry beyond portfolioValues
				if (idx >= portfolioValues.Count)
					idx = portfolioValues.Count - 1;

				if (idx >= 0)
				{
					portfolioBalance = model.InitialPortfolioBalance * (1 + portfolioValues[idx] / 100);
				}
			}
			return portfolioBalance;
		}



		public static Dictionary<DateTime, double> ReadCsv(string filePath)
		{
			var result = new Dictionary<DateTime, double>();

			foreach (var line in File.ReadLines(filePath))
			{
				if (string.IsNullOrWhiteSpace(line))
					continue;

				// Skip header
				if (line.StartsWith("Date", StringComparison.OrdinalIgnoreCase))
					continue;

				var parts = line.Split(',');

				if (parts.Length < 2)
					continue;

				if (!DateTime.TryParse(
						parts[0].Trim(),
						CultureInfo.InvariantCulture,
						DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
						out DateTime date))
					continue;

				if (!double.TryParse(
						parts[1].Trim(),
						NumberStyles.Any,
						CultureInfo.InvariantCulture,
						out double value))
					continue;

				result[date] = value; // last value wins if duplicates
			}

			return result;
		}

		private enum PortfolioSortType
		{
			Date,
			Score,
			Ticker,
			Weight,
			Sector,
			Return,
			NewLong,
			ExitLong,
			NewShort,
			CoverShort,
			AddLong,
			ReduceLong,
			AddShort,
			ReduceShort,
			Units,
			PnL
		}

		private PortfolioSortType _portfolioSortType = PortfolioSortType.Score;

		private class TradeItem
		{
			public string Ticker;
			public int Direction;
			public bool Entry;
			public bool Exit;
			public DateTime EntryDateTime;
		}

		public double getReturn(string ticker, double direction, double price1, double price2)
		{
			double returnValue = 0;
			if (atm.isYieldTicker(ticker))
			{
				returnValue = direction * (price2 - price1);
			}
			else
			{
				returnValue = direction * 100 * (price2 - price1) / Math.Abs(price1);
				//returnValue = size * (price2 - price1);
			}
			return returnValue;
		}

		/// <summary>
		/// Beta at last rebalance — uses Investment1 (fixed optimizer dollar weight) / NAV.
		/// Near zero post-rebalance by construction. Comparable to backtest historical log.
		/// </summary>
		private double getRebalanceBeta(DateTime rebalTime, double nav)
		{
			if (nav <= 0) return double.NaN;
			var portfolioBeta = 0.0;
			Model model = getModel();
			if (model != null)
			{
				var portfolio = (rebalTime == default(DateTime)) ? new List<Trade>() : _portfolio1.GetTrades(model.Name, "", rebalTime);
				foreach (var trade in portfolio)
				{
					var ticker = trade.Ticker;
					var symbol = model.Symbols.Find(x => x.Ticker == ticker);
					if (symbol == null) continue;
					var key = rebalTime.ToString("yyyy-MM-dd");
					double beta = symbol.Beta.Count > 0
						? symbol.Beta.ContainsKey(key) ? symbol.Beta[key] : symbol.Beta.Last().Value
						: 1.0;
					// Investment1 = optimizer-assigned dollar size, fixed at rebalance
					var value = beta * trade.Investment1 / nav;
					portfolioBeta += (trade.Direction > 0) ? value : -value;
				}
			}
			return portfolioBeta;
		}

		private double getPortfolioBeta(DateTime time, double nav = 0.0)
		{
			var portfolioBeta = 0.0;
			Model model = getModel();
			if (model != null)
			{
				var portfolio = (time == default(DateTime)) ? new List<Trade>() : _portfolio1.GetTrades(model.Name, "", time);
				Dictionary<string, double> shares = new Dictionary<string, double>();
				Dictionary<string, double> price = new Dictionary<string, double>();
				var totalInvestment = 0.0;
				foreach (var trade in portfolio)
				{
					string ticker = trade.Ticker;
					var bars = getBars(ticker, "D", BarServer.MaxBarCount);
					var entryTime = trade.OpenDateTime;
					double price1 = trade.AvgPrice.ContainsKey(time) ? trade.AvgPrice[time] : getPrice(bars, entryTime);
					var shares1 = shares[ticker] = trade.Shares.ContainsKey(time) ? trade.Shares[time] : 0;
					price[ticker] = price1;
					shares[ticker] = shares1;
					totalInvestment += price1 * shares1;
				}

				// Use NAV as denominator when provided (industry standard for long/short funds).
				// Falls back to gross totalInvestment if NAV is unavailable.
				var denominator = (nav > 0) ? nav : totalInvestment;

				foreach (var trade in portfolio)
				{
					string ticker = trade.Ticker;
					var beta = 1.0;
					var symbol = model.Symbols.Find(x => x.Ticker == ticker);
					if (symbol != null)
					{
						var key = time.ToString("yyyy-MM-dd");
						beta = symbol.Beta.Count > 0 ? symbol.Beta.ContainsKey(key) ? symbol.Beta[key] : symbol.Beta.Last().Value : 1;
					}
					int side = trade.Direction;
					var amount = price[ticker] * shares[ticker];
					var value = beta * amount / denominator;
					portfolioBeta += (side > 0) ? value : -value;
				}
			}
			return portfolioBeta;
		}

		/// <summary>
		/// Returns true when the cursor should be treated as "viewing live" data.
		/// Fixes the intra-week blind spot: Mon–Thu the cursor sits at last Friday's date
		/// which is &lt; today, causing every >= DateTime.Today gate to evaluate false and
		/// freezing balance + Cur Px for the entire week between rebalances.
		/// </summary>
		private bool IsViewingLive(DateTime cursor)
		{
			if (cursor == default(DateTime)) return true;
			if (cursor.Date >= DateTime.Today) return true;
			var model = getModel();
			if (model?.IsLiveMode == true && _portfolioTimes?.Count > 0)
			{
				var latestTime = _portfolioTimes
					.Where(t => t != default(DateTime))
					.OrderByDescending(t => t).FirstOrDefault();
				if (latestTime != default(DateTime) && cursor.Date >= latestTime.Date)
					return true;
			}
			return false;
		}

		private void drawPortfolioGrid()
		{
			Model model = getModel();
			// Clear cached prices so getCurrentPrice fetches fresh values from bar cache this cycle
			lock (_lastKnownPrices) { _lastKnownPrices.Clear(); }
			if (model != null)
			{

				DataGridColumnDtd.Children.Clear();         // Entry Date
				DataGridColumnSector.Children.Clear();      // Sector
				DataGridColumnSymbol.Children.Clear();      // symbol
				DataGridColumnDesc.Children.Clear();        // description
				DataGridColumnShares.Children.Clear();      // Shares
				DataGridColumnInvestment.Children.Clear();  // Investment
				DataGridColumnBeta.Children.Clear();        // Beta
				DataGridColumnAvgPx.Children.Clear();       // Entry Price
				DataGridColumnCurPx.Children.Clear();       // Current Price
				DataGridColumnATRX.Children.Clear();        // ATRX
				DataGridColumnPL.Children.Clear();          // PnL				
															//DataGridColumnVOL.Children.Clear();         // VOL%
															//DataGridColumnKelly.Children.Clear();       // Kelly%
															//DataGridColumnRisk.Children.Clear();        // Risk%


				//DataGridColumnOrders.Children.Clear();      // Orders



				//DataGridColumnOrderDate.Children.Clear();
				DataGridColumnNewLong.Children.Clear();
				DataGridColumnAddLong.Children.Clear();
				DataGridColumnReduceLong.Children.Clear();
				DataGridColumnExitLong.Children.Clear();
				DataGridColumnNewShort.Children.Clear();
				DataGridColumnAddShort.Children.Clear();
				DataGridColumnReduceShort.Children.Clear();
				DataGridColumnCoverShort.Children.Clear();

				if (_portfolioTimes != null && _portfolioTimes.Count > 0)
				{
					TextInfo textInfo = new CultureInfo("en-US", false).TextInfo;

					DateTime time2 = _showHoldingsTime;
					DateTime time1 = _portfolioTimes.OrderByDescending(x => x).FirstOrDefault(x => x < time2);

					// Pre-order Friday: show current (pre-rebalance) balance until orders are sent.
					var isProjectedRebalDate = model.IsLiveMode && time2.Date > DateTime.Today;
					var isPreOrderFriday = IsLivePreRebalance() && time2.Date == DateTime.Today && !isProjectedRebalDate;
					var balanceTime = (isPreOrderFriday && time1 != default(DateTime)) ? time1 : time2;
					var portfolioBalance = getPortfolioBalance(balanceTime);

					// Live intra-week: time2 may be a daily date with no trade snapshot.
					// Fall back to time1 (last rebalance Friday) for all trade display.
					var tradesTime2 = time2;
					var tradesTime1 = time1;
					bool fallbackFired = false;
					if (model.IsLiveMode)
					{
						var probe = _portfolio1.GetTrades(model.Name, "", tradesTime2);
						if (probe.Count == 0 && tradesTime1 != default(DateTime))
						{
							tradesTime2 = tradesTime1;
							tradesTime1 = default(DateTime);
							fallbackFired = true;
						}
					}
					var oldTrades = (tradesTime1 == default(DateTime)) ? new List<Trade>() : _portfolio1.GetTrades(model.Name, "", tradesTime1);
					var newTrades = (tradesTime2 == default(DateTime)) ? new List<Trade>() : _portfolio1.GetTrades(model.Name, "", tradesTime2);
					// isIntraWeekDisplay: true ONLY when the fallback swap fired (no snapshot at time2).
					// NOT true just because this is the first rebalance (tradesTime1==default normally).
					var isIntraWeekDisplay = model.IsLiveMode && fallbackFired;
					var enteredTrades = (!isIntraWeekDisplay)
						? newTrades.Where(x => !oldTrades.Any(y => x.Ticker == y.Ticker && x.Direction == y.Direction)).ToList()
						: new List<Trade>();
					var exitedTrades = (!isIntraWeekDisplay)
						? oldTrades.Where(x => !newTrades.Any(y => x.Ticker == y.Ticker && x.Direction == y.Direction)).ToList()
						: new List<Trade>();
					var addTrades = (!isIntraWeekDisplay)
						? newTrades.Where(x => x.Shares.ContainsKey(tradesTime1) && x.Shares.ContainsKey(tradesTime2) && x.Shares[tradesTime2] > x.Shares[tradesTime1]).ToList()
						: new List<Trade>();
					var reduceTrades = (!isIntraWeekDisplay)
						? newTrades.Where(x => x.Shares.ContainsKey(tradesTime1) && x.Shares.ContainsKey(tradesTime2) && x.Shares[tradesTime2] < x.Shares[tradesTime1]).ToList()
						: new List<Trade>();

					newTrades.ForEach(x => x.Direction = (x.Direction > 0) ? 1 : (x.Direction < 0) ? -1 : 0);
					oldTrades.ForEach(x => x.Direction = (x.Direction > 0) ? 1 : (x.Direction < 0) ? -1 : 0);

					// this is bad
					addTrades.ForEach(x => x.Direction = (x.Direction > 0) ? 4 : -4);
					reduceTrades.ForEach(x => x.Direction = (x.Direction > 0) ? 5 : -5);
					exitedTrades.ForEach(x => x.Direction = (x.Direction > 0) ? 3 : -3);
					enteredTrades.ForEach(x => x.Direction = (x.Direction > 0) ? 2 : -2);

					List<Trade> portfolio;
					if (isPreOrderFriday || isProjectedRebalDate)
					{
						foreach (var t in oldTrades)
							t.Direction = (t.Direction > 0) ? 1 : -1;
						portfolio = new List<Trade>(oldTrades);
					}
					else
					{
						portfolio = newTrades;
						portfolio.AddRange(exitedTrades);
					}

					if (tradesTime1 == default(DateTime)) tradesTime1 = model.TestingRange.Time1;

					Dictionary<string, double> returns = new Dictionary<string, double>();
					Dictionary<string, double> shares = new Dictionary<string, double>();
					Dictionary<string, double> price = new Dictionary<string, double>();
					Dictionary<string, double> entryPrice = new Dictionary<string, double>();

					var sectorInvestments = new Dictionary<string, double>();
					var industryInvestments = new Dictionary<string, double>();
					var subIndustryInvestments = new Dictionary<string, double>();

					var totalInvestment = 0.0;
					foreach (var trade in portfolio)
					{
						int side = (int)trade.Direction;
						string ticker = trade.Ticker;
						bool useYield = atm.isYieldTicker(ticker);

						var bars = getBars(ticker, "D", BarServer.MaxBarCount);

						var entryTime = trade.OpenDateTime;
						var exitTime = trade.CloseDateTime;

						double returnValue = double.NaN;

						// Avg Px — always the original entry-date close price (or true average if
						// adds occurred). Use the AvgPrice entry keyed to entryTime.Date first.
						// Fall back to a date-matched bar close — NOT getPrice(bars, entryTime),
						// which compares timestamps and can return today's close when bar Time is
						// midnight-stamped and entryTime has a 4 PM component (or vice versa).
						var entryPriceKey = trade.AvgPrice.Keys.FirstOrDefault(k => k.Date == entryTime.Date);
						// Fallback: if no exact entry date match, use most recent AvgPrice key
						if (entryPriceKey == default(DateTime))
							entryPriceKey = trade.AvgPrice.Keys
								.Where(k => k.Date <= entryTime.Date)
								.OrderByDescending(k => k).FirstOrDefault();
						var entryBar = bars.LastOrDefault(b => b.Time.Date == entryTime.Date);
						double price1 = entryPriceKey != default(DateTime) ? trade.AvgPrice[entryPriceKey]
							: (entryBar != null && entryBar.Close > 0) ? entryBar.Close
							: double.NaN;

						// Cur Px — for live/current date use Bloomberg live price.
						// For historical dates use the closing bar at tradesTime2 (not today's price).
						var isLiveDisplay = model.IsLiveMode && tradesTime2.Date >= DateTime.Today.AddDays(-7);
						var livePrice = isLiveDisplay ? getCurrentPrice(ticker) : double.NaN;
						var refBar = bars.LastOrDefault(b2 => b2.Time.Date == tradesTime2.Date)
							?? (isLiveDisplay ? bars.LastOrDefault(b2 => b2.Time.Date == DateTime.Today) : null);
						double price2 = (!double.IsNaN(livePrice) && livePrice > 0) ? livePrice
							: refBar != null && refBar.Close > 0 ? refBar.Close
							: entryPriceKey != default(DateTime) ? trade.AvgPrice[entryPriceKey]
							: getPrice(bars, tradesTime2);
						if (!double.IsNaN(price1) && !double.IsNaN(price2) && (trade.IsOpen() || exitTime == default || time2 < exitTime))
						{
							returns[ticker] = 100 * Math.Sign(side) * (price2 - price1) / price1;
						}
						else
						{
							returns[ticker] = 0;
						}
						price[ticker] = price2;
						entryPrice[ticker] = price1;
						// Cache for refreshLiveBalance so it uses the same prices as the grid
						if (!double.IsNaN(price2) && price2 > 0)
							lock (_lastKnownPrices) { _lastKnownPrices[ticker] = price2; }
						// Shares lookup:
						//   Pre-order Friday — use tradesTime1 (last settled Friday) for ALL positions.
						//   Exits have 0 shares in the new portfolio (tradesTime2), so using
						//   tradesTime2.Date would zero them out and blank their rows.
						//   Adds/reduces also look better anchored to the last-Friday share count.
						var isExitTrade = Math.Abs((int)trade.Direction) == 3;
						var sharesLookupDate = (isPreOrderFriday && tradesTime1 != default(DateTime))
							? tradesTime1.Date                        // pre-order: all positions anchored to last Friday
							: (isExitTrade && tradesTime1 != default(DateTime))
								? tradesTime1.Date                    // post-order exits: closing position at tradesTime1
								: tradesTime2.Date;                   // normal hold/new/add/reduce
						var sharesKey = trade.Shares.Keys
							.Where(k => k.Date == sharesLookupDate && trade.Shares[k] != 0)
							.OrderByDescending(k => k).FirstOrDefault();
						if (sharesKey == default(DateTime))
							sharesKey = trade.Shares.Keys
								.Where(k => k.Date <= sharesLookupDate && trade.Shares[k] != 0)
								.OrderByDescending(k => k).FirstOrDefault();
						shares[ticker] = sharesKey != default(DateTime) ? trade.Shares[sharesKey] : 0;

						var name = "";
						int sectorNumber;
						int industryNumber;
						int subIndustryNumber;
						var symbol = model.Symbols.Find(x => x.Ticker == ticker);
						// Skip exited positions — they are included in `portfolio` for display
						// purposes (showing exit rows) but are no longer held and must not
						// contribute to sector/industry/sub-industry exposure totals.
						// Direction ±3 = Exit, so Math.Abs(Direction) == 3 identifies them.
						bool isExitedForSector = Math.Abs((int)trade.Direction) == 3;
						if (symbol != null && !isExitedForSector)
						{
							int.TryParse(symbol.Sector, out sectorNumber);
							name = _portfolio1.GetSectorLabel(sectorNumber);
							// NET per sector: shares[ticker] is stored as absolute value,
							// so the (side > 0 ? 1 : -1) multiplier provides the sign.
							// Longs contribute positive, shorts negative -> NET exposure.
							var amount = (side > 0 ? 1 : -1) * price[ticker] * shares[ticker];
							if (!double.IsNaN(amount))
							{
								if (sectorInvestments.ContainsKey(name))
								{
									sectorInvestments[name] += amount;
								}
								else
								{
									sectorInvestments[name] = amount;
								}
								totalInvestment += amount;
							}
						}
						if (symbol != null && !isExitedForSector)  // reuse flag — same exited filter
						{
							int.TryParse(symbol.Industry, out industryNumber);
							name = _portfolio1.GetIndustryLabel(industryNumber);
							// NET per industry: (side > 0 ? 1 : -1) gives the sign since
							// shares[ticker] is stored as absolute value.
							var amount = (side > 0 ? 1 : -1) * price[ticker] * shares[ticker];
							if (!double.IsNaN(amount))
							{

								if (industryInvestments.ContainsKey(name))
								{
									industryInvestments[name] += amount;
								}
								else
								{
									industryInvestments[name] = amount;
								}
							}
						}
						if (symbol != null && !isExitedForSector)  // reuse flag — same exited filter
						{
							int.TryParse(symbol.SubIndustry, out subIndustryNumber);
							name = _portfolio1.GetSubIndustryLabel(subIndustryNumber);
							// NET per sub-industry: (side > 0 ? 1 : -1) gives the sign since
							// shares[ticker] is stored as absolute value.
							var amount = (side > 0 ? 1 : -1) * price[ticker] * shares[ticker];
							if (!double.IsNaN(amount))
							{

								if (subIndustryInvestments.ContainsKey(name))
								{
									subIndustryInvestments[name] += amount;
								}
								else
								{
									subIndustryInvestments[name] = amount;
								}
							}
						}
					}

					// Fix 2a: Pre-order — price/shares dicts only contain oldTrades tickers.
					// Seed entries for proposed new positions so DataGrid2 can render them.
					var hasOrders = enteredTrades.Count > 0 || exitedTrades.Count > 0 || addTrades.Count > 0 || reduceTrades.Count > 0;
					if (hasOrders && !isIntraWeekDisplay)
					{
						foreach (var t in enteredTrades)
						{
							var tk = t.Ticker;
							if (shares.ContainsKey(tk)) continue;
							var sk = t.Shares.Keys
								.Where(k => k.Date == tradesTime2.Date && t.Shares[k] != 0)
								.OrderByDescending(k => k).FirstOrDefault();
							shares[tk] = sk != default(DateTime) ? t.Shares[sk] : 0;
							var lp = getCurrentPrice(tk);
							if (!double.IsNaN(lp) && lp > 0)
							{
								price[tk] = lp;
							}
							else
							{
								var bars2 = getBars(tk, "D", BarServer.MaxBarCount);
								var rb = bars2.LastOrDefault(b => b.Time.Date == DateTime.Today)
									  ?? bars2.LastOrDefault(b => b.Time.Date == tradesTime2.Date);
								price[tk] = (rb != null && rb.Close > 0) ? rb.Close : double.NaN;
							}
						}
					}

					// ── Save rebalance state for FlexOne submission ───────────────────────────
					// shares and price are fully populated here (Fix 2a has seeded entered trades).
					// Stored in fields so SendOrders_MouseDown can read them after 3:00 PM.
					if (hasOrders && !isIntraWeekDisplay)
					{
						_flexEnteredTrades = enteredTrades;
						_flexExitedTrades = exitedTrades;
						_flexAddTrades = addTrades;
						_flexReduceTrades = reduceTrades;
						_flexTradesTime1 = tradesTime1;
						_flexTradesTime2 = tradesTime2;
						_flexShares = new Dictionary<string, double>(shares);
						_flexPrice = new Dictionary<string, double>(price);
					}
					// ─────────────────────────────────────────────────────────────────────────

					var percents = _portfolio1.initPercents();
					sectorPercents = percents[0];
					industryPercents = percents[1];
					subIndustryPercents = percents[2];

					if (totalInvestment != 0)
					{
						// Try loading optimizer-saved sector fractions (exact values from PHASE 1b/1c).
						// These are saved by IdeaCalculator after each rebalance and are guaranteed
						// to match the constraint-enforced allocation, regardless of current prices.
						// Always compute sector/industry/sub-industry percents as NET at rebalance prices.
						// (Previously sector was loaded from an optimizer snapshot file when available,
						// which introduced inconsistency when the file was missing and diverged from net.)
						// Math.Abs is applied for display so a negative net (net-short) shows as a magnitude.
						var savedFracsData = (string)null; // path removed; always use net-investment calc
						if (!string.IsNullOrEmpty(savedFracsData))
						{
							// Map 2-digit GICS sector code to sector label for display
							var gicsToLabel = new Dictionary<string, string>();
							foreach (var sym in model.Symbols)
							{
								if (sym.Sector == null) continue;
								var raw = sym.Sector;
								var code2 = raw.Length >= 2 ? raw.Substring(0, 2) : raw;
								if (gicsToLabel.ContainsKey(code2)) continue;
								int sn; int.TryParse(raw, out sn);
								gicsToLabel[code2] = _portfolio1.GetSectorLabel(sn);
							}
							foreach (var line in savedFracsData.Split('\n'))
							{
								var commaIdx = line.IndexOf(',');
								if (commaIdx < 0) continue;
								var code = line.Substring(0, commaIdx).Trim();
								if (!double.TryParse(line.Substring(commaIdx + 1).Trim(),
									System.Globalization.NumberStyles.Any,
									System.Globalization.CultureInfo.InvariantCulture, out double frac)) continue;
								if (!gicsToLabel.TryGetValue(code, out var label)) continue;
								if (sectorPercents.ContainsKey(label))
									sectorPercents[label] = Math.Abs(frac).ToString(".00%");
							}
							// Industry and sub-industry still use computed values (no optimizer save yet)
							industryPercents.Keys.ToList().ForEach(k => industryPercents[k] = industryInvestments.ContainsKey(k) && !double.IsNaN(industryInvestments[k]) ? Math.Abs(industryInvestments[k] / portfolioBalance).ToString(".00%") : "");
							subIndustryPercents.Keys.ToList().ForEach(k => subIndustryPercents[k] = subIndustryInvestments.ContainsKey(k) && !double.IsNaN(subIndustryInvestments[k]) ? Math.Abs(subIndustryInvestments[k] / portfolioBalance).ToString(".00%") : "");
						}
						else
						{
							// Fallback: compute from shares and prices
							sectorPercents.Keys.ToList().ForEach(k => sectorPercents[k] = sectorInvestments.ContainsKey(k) && !double.IsNaN(sectorInvestments[k]) ? Math.Abs(sectorInvestments[k] / portfolioBalance).ToString(".00%") : "");
							industryPercents.Keys.ToList().ForEach(k => industryPercents[k] = industryInvestments.ContainsKey(k) && !double.IsNaN(industryInvestments[k]) ? Math.Abs(industryInvestments[k] / portfolioBalance).ToString(".00%") : "");
							subIndustryPercents.Keys.ToList().ForEach(k => subIndustryPercents[k] = subIndustryInvestments.ContainsKey(k) && !double.IsNaN(subIndustryInvestments[k]) ? Math.Abs(subIndustryInvestments[k] / portfolioBalance).ToString(".00%") : "");
						}
					}

					//var beta = getPortfolioBeta(time2);

					var portfolioBeta = getPortfolioBeta(time2, portfolioBalance);
					var rebalanceTime = getPreviousRebalanceDate(model, _showHoldingsTime, _portfolioTimes);
					var rebalanceBeta = getRebalanceBeta(rebalanceTime, portfolioBalance);

					// Show both on every beta label: "R:0.01  L:0.42"
					// R = rebalance beta (optimizer output, near zero), L = live drift beta
					var combinedBetaText = $"R:{(double.IsNaN(rebalanceBeta) ? "--" : rebalanceBeta.ToString("0.00"))}  L:{(double.IsNaN(portfolioBeta) ? "--" : portfolioBeta.ToString("0.00"))}";
					BetaValue.Content = combinedBetaText;
					BetaValue2.Content = combinedBetaText;

					// Update GROSS / NET cells to the left of the tiles based on active view.

					if (_activeTileView == "Industry") LoadTiles(industryPercents);
					else if (_activeTileView == "SubIndustry") LoadTiles(subIndustryPercents);
					else LoadTiles(sectorPercents);

					// Push sector percents + alert max values to shared info so Timing reads
					// PB's live-price calculations instead of recalculating from stale bar closes.
					if (_mainView != null)
					{
						_mainView.SetInfo("SectorPercents",      string.Join("|", sectorPercents.Select(kv      => $"{kv.Key}={kv.Value}")));
						_mainView.SetInfo("IndustryPercents",    string.Join("|", industryPercents.Select(kv    => $"{kv.Key}={kv.Value}")));
						_mainView.SetInfo("SubIndustryPercents", string.Join("|", subIndustryPercents.Select(kv => $"{kv.Key}={kv.Value}")));
						if (portfolioBalance > 0)
						{
							double maxSectorNet = sectorInvestments.Count      > 0 ? sectorInvestments.Values.Max(v      => Math.Abs(v)) / portfolioBalance : 0;
							double maxIndNet    = industryInvestments.Count    > 0 ? industryInvestments.Values.Max(v    => Math.Abs(v)) / portfolioBalance : 0;
							double maxSubIndNet = subIndustryInvestments.Count > 0 ? subIndustryInvestments.Values.Max(v => Math.Abs(v)) / portfolioBalance : 0;
							_mainView.SetInfo("AlertMaxSectorNet",   maxSectorNet.ToString("R"));
							_mainView.SetInfo("AlertMaxIndustryNet", maxIndNet.ToString("R"));
							_mainView.SetInfo("AlertMaxSubIndNet",   maxSubIndNet.ToString("R"));
						}
					}

					Dictionary<string, double> scores = loadData("scores", model, rebalanceTime);
					Dictionary<string, double> weights = loadData("weights", model, rebalanceTime);

					portfolio.ForEach(x => { if (!scores.ContainsKey(x.Ticker)) scores[x.Ticker] = 0; });
					portfolio.ForEach(x => { if (!weights.ContainsKey(x.Ticker)) weights[x.Ticker] = 0; });

					var longAmount = 0.0;
					var shortAmount = 0.0;
					Dictionary<string, string> sectors = new Dictionary<string, string>();

					foreach (var trade in portfolio)
					{
						string ticker = trade.Ticker;
						string sectorNumberText = model.GetSector(ticker);
						int sectorNumber;
						int.TryParse(sectorNumberText, out sectorNumber);
						string sector = textInfo.ToTitleCase(_portfolio1.GetSectorName(sectorNumber).ToLower());
						sectors[ticker] = sector;

						int side = trade.Direction;

						// Calculate dollar amount for this position
						// shares[ticker] is already signed (+ for long, - for short)
						var amount = price[ticker] * Math.Abs(shares[ticker]);

						// Accumulate to correct side based on direction
						if (side > 0)
						{
							longAmount += amount;  // Long position (amount is positive)
						}
						else if (side < 0)
						{
							shortAmount += amount;  // Short position (amount is positive)
						}
					}

					// Report market neutral status
					var totalGross = longAmount + shortAmount;
					var imbalance = longAmount - shortAmount;
					var imbalancePct = totalGross > 0 ? Math.Abs(imbalance) / totalGross : 0.0;

					//Debug.WriteLine($"[Portfolio] Long Amount:  ${longAmount:N0}");
					//Debug.WriteLine($"[Portfolio] Short Amount: ${shortAmount:N0}");
					//Debug.WriteLine($"[Portfolio] Total Gross:  ${totalGross:N0}");
					//Debug.WriteLine($"[Portfolio] Imbalance:    ${Math.Abs(imbalance):N0} ({imbalancePct:P2})");
					//Debug.WriteLine($"[Portfolio] Market Neutral: {(imbalancePct <= 0.05 ? "✓" : "✗")}");

					if (_portfolioSortType == PortfolioSortType.Score)
					{
						portfolio = portfolio.OrderByDescending(x => scores[x.Ticker]).ToList();
					}
					else if (_portfolioSortType == PortfolioSortType.Ticker)
					{
						portfolio = portfolio.OrderBy(x => x.Ticker).ToList();
					}
					else if (_portfolioSortType == PortfolioSortType.Date)
					{
						portfolio = portfolio.OrderBy(x => x.OpenDateTime).ToList();
					}
					else if (_portfolioSortType == PortfolioSortType.Units)
					{
						portfolio = portfolio.OrderBy(x => { var sk = x.Shares.Keys.FirstOrDefault(k => k.Date == tradesTime2.Date); return sk != default(DateTime) ? x.Shares[sk] : 0; }).ToList();
					}
					else if (_portfolioSortType == PortfolioSortType.PnL)
					{
						portfolio = portfolio.OrderBy(x => returns.ContainsKey(x.Ticker) ? returns[x.Ticker] : 0).Reverse().ToList();
					}
					else if (_portfolioSortType == PortfolioSortType.Weight)
					{
						portfolio = portfolio.OrderByDescending(x => weights[x.Ticker]).ToList();
					}
					else if (_portfolioSortType == PortfolioSortType.Sector)
					{
						portfolio = portfolio.OrderBy(x => sectors[x.Ticker]).ToList();
					}
					else if (_portfolioSortType == PortfolioSortType.Return)
					{
						portfolio = portfolio.OrderByDescending(x => returns[x.Ticker]).ToList();
					}
					else if (_portfolioSortType == PortfolioSortType.NewLong)
					{
						portfolio = portfolio.OrderBy(x => (x.Direction == 2) ? 0 : 1).ToList();
					}
					else if (_portfolioSortType == PortfolioSortType.AddLong)
					{
						portfolio = portfolio.OrderBy(x => (x.Direction == 4) ? 0 : 1).ToList();
					}
					else if (_portfolioSortType == PortfolioSortType.ReduceLong)
					{
						portfolio = portfolio.OrderBy(x => (x.Direction == 5) ? 0 : 1).ToList();
					}
					else if (_portfolioSortType == PortfolioSortType.ExitLong)
					{
						portfolio = portfolio.OrderBy(x => ((x.Direction == 3) ? "0" : "1") + x.Ticker).ToList();
					}
					else if (_portfolioSortType == PortfolioSortType.NewShort)
					{
						portfolio = portfolio.OrderBy(x => (x.Direction == -2) ? 0 : 1).ToList();
					}
					else if (_portfolioSortType == PortfolioSortType.AddShort)
					{
						portfolio = portfolio.OrderBy(x => (x.Direction == -4) ? 0 : 1).ToList();
					}
					else if (_portfolioSortType == PortfolioSortType.ReduceShort)
					{
						portfolio = portfolio.OrderBy(x => (x.Direction == -5) ? 0 : 1).ToList();
					}
					else if (_portfolioSortType == PortfolioSortType.CoverShort)
					{
						portfolio = portfolio.OrderBy(x => (x.Direction == -3) ? 0 : 1).ToList();
					}

					var total = 0.0;
					foreach (var trade in portfolio)
					{
						string ticker = trade.Ticker;
						var t1 = ticker;
						var t2 = ticker.Split(' ')[0];

						var beta = 1.0;
						var symbol = model.Symbols.Find(x => x.Ticker == ticker);
						if (symbol != null)
						{
							var key = time2.ToString("yyyy-MM-dd");
							beta = symbol.Beta.Count > 0 ? symbol.Beta.ContainsKey(key) ? symbol.Beta[key] : symbol.Beta.Last().Value : 1;
						}

						int direction = (int)trade.Direction;

						var rev = atm.isYieldTicker(ticker);
						if (rev) direction = -direction;

						string description = textInfo.ToTitleCase(model.GetDescription(ticker).ToLower());
						if (description.Length == 0) description = _portfolio1.GetDescription(ticker);

						// Col 1 is Entry Exit dates.   Add the dates of entry and exit.   Exit text is white. 
						Brush brush = Brushes.White;
						Brush brush2 = new SolidColorBrush(Color.FromRgb(0x77, 0x77, 0x77));
						Brush brush3 = new SolidColorBrush(Color.FromRgb(0xff, 0x8c, 0x00));

						if (direction == 5) brush = Brushes.Cyan;             // reduce long
						else if (direction == 4) brush = Brushes.PaleGreen;   // add to long
						else if (direction == 3) brush = Brushes.Cyan;        // enter long
						else if (direction == 2) brush = Brushes.PaleGreen;   // enter long
						else if (direction == 1) brush = Brushes.PaleGreen;   // new long
						else if (direction == -1) brush = Brushes.Crimson;    // new short
						else if (direction == -2) brush = Brushes.Crimson;    // enter short
						else if (direction == -3) brush = Brushes.Yellow;     //exit short
						else if (direction == -4) brush = Brushes.Crimson;    //add to short
						else if (direction == -5) brush = Brushes.Yellow;     //reduce short
						else if (direction == 0) brush = Brushes.White;       //neutral 

						// Exits/covers close the full position — units = all shares being closed.
						// Adds/reduces use the delta vs. the prior period.
						var units = (Math.Abs(direction) == 3)
							? Math.Abs(shares[ticker])   // closing trade: full position size
							: shares[ticker] != 0
								? Math.Abs(shares[ticker] - (tradesTime1 != default(DateTime) && trade.Shares.Keys.Any(k => k.Date == tradesTime1.Date)
									? trade.Shares[trade.Shares.Keys.First(k => k.Date == tradesTime1.Date)] : 0))
								: 0;

						if (!(direction == 0 || direction == 0 || direction == 3 || direction == -3))
						{
							// Col 1  Entry Date
							TextBlock tb1 = new TextBlock();
							tb1.Text = trade.OpenDateTime.ToString("MM-dd-yy");
							tb1.Padding = new Thickness(5, 2, 0, 2);
							tb1.FontSize = 9;
							tb1.Foreground = Brushes.Silver;
							tb1.Tag = ticker;
							tb1.MouseLeftButtonDown += TradeDateClicked;
							DataGridColumnDtd.Children.Add(tb1);

							// Col 2  Sector
							TextBlock tbSECT = new TextBlock();
							var name = "";
							int sectorNumber;
							if (symbol != null)
							{
								int.TryParse(symbol.Sector, out sectorNumber);
								name = _portfolio1.GetSectorLabel(sectorNumber);
							}
							tbSECT.Text = name;
							tbSECT.HorizontalAlignment = HorizontalAlignment.Left;
							tbSECT.Padding = new Thickness(5, 2, 0, 2);
							tbSECT.FontSize = 9;
							tbSECT.Foreground = Brushes.Silver;
							tbSECT.Tag = ticker;
							tbSECT.MouseLeftButtonDown += TickerClicked;
							DataGridColumnSector.Children.Add(tbSECT);

							// Col 3  Ticker
							TextBlock tb2 = new TextBlock();
							tb2.Text = t2;
							tb2.Tag = ticker;
							tb2.HorizontalAlignment = HorizontalAlignment.Left;
							tb2.Padding = new Thickness(0, 2, 0, 2);
							tb2.FontSize = 9;
							tb2.Foreground = Brushes.White;
							tb2.Cursor = Cursors.Hand;
							tb2.MouseLeftButtonDown += TickerClicked;
							DataGridColumnSymbol.Children.Add(tb2);

							// Col 4 Description
							TextBlock tb4 = new TextBlock();
							tb4.Text = description;
							tb4.Tag = ticker;
							tb4.Padding = new Thickness(0, 2, 0, 2);
							tb4.FontSize = 9;
							tb4.Foreground = Brushes.White;
							tb4.Cursor = Cursors.Hand;
							tb4.MouseLeftButtonDown += TickerClicked;
							DataGridColumnDesc.Children.Add(tb4);

							// Col 5 Shares
							TextBlock tb9 = new TextBlock();
							tb9.Text = shares[ticker] != 0 ? shares[ticker].ToString("N0", CultureInfo.CurrentCulture) : "";
							tb9.Padding = new Thickness(5, 2, 0, 2);
							tb9.HorizontalAlignment = HorizontalAlignment.Right;
							tb9.FontSize = 9;
							tb9.Foreground = direction > 0 ? Brushes.PaleGreen : Brushes.Crimson;
							tb9.Tag = ticker;
							tb9.MouseLeftButtonDown += TickerClicked;
							DataGridColumnShares.Children.Add(tb9);


							total += shares[ticker] != 0 ? price[ticker] * shares[ticker] : 0;

							// Col 6 Investment
							TextBlock tbInvest = new TextBlock();
							tbInvest.Text = shares[ticker] != 0 ? (price[ticker] * shares[ticker]).ToString("C0", CultureInfo.CurrentCulture) : "";
							tbInvest.Padding = new Thickness(5, 2, 0, 2);
							tbInvest.HorizontalAlignment = HorizontalAlignment.Right;
							tbInvest.FontSize = 9;
							tbInvest.Foreground = direction > 0 ? Brushes.PaleGreen : Brushes.Crimson;
							tbInvest.Tag = ticker;
							tbInvest.MouseLeftButtonDown += TickerClicked;
							DataGridColumnInvestment.Children.Add(tbInvest);

							// Col 7 beta
							TextBlock tbB = new TextBlock();
							tbB.Text = beta.ToString("0.00");
							tbB.Padding = new Thickness(5, 2, 0, 2);
							tbB.HorizontalAlignment = HorizontalAlignment.Right;
							tbB.FontSize = 9;
							tbB.Foreground = direction > 0 ? Brushes.PaleGreen : Brushes.Crimson;
							tbB.Tag = ticker;
							tbB.MouseLeftButtonDown += TickerClicked;
							DataGridColumnBeta.Children.Add(tbB);

							// Col 8 Entry Price
							TextBlock tbEP = new TextBlock();
							tbEP.Padding = new Thickness(0, 2, 20, 2);
							tbEP.Text = (entryPrice.ContainsKey(ticker) && !double.IsNaN(entryPrice[ticker])) ? entryPrice[ticker].ToString(".00") : "";
							tbEP.HorizontalAlignment = HorizontalAlignment.Right;
							tbEP.FontSize = 9;
							tbEP.Foreground = Foreground = Brushes.Silver;
							DataGridColumnAvgPx.Children.Add(tbEP);

							// Col 9 Current Price
							TextBlock tbCP = new TextBlock();
							tbCP.Padding = new Thickness(0, 2, 20, 2);
							tbCP.Text = !double.IsNaN(price[ticker]) && price[ticker] != 0 ? price[ticker].ToString("0.00", CultureInfo.CurrentCulture) : "";
							tbCP.HorizontalAlignment = HorizontalAlignment.Right;
							tbCP.FontSize = 9;
							tbCP.Foreground = Foreground = Brushes.Silver;
							DataGridColumnCurPx.Children.Add(tbCP);

							// Col 10 ATRX
							TextBlock tbATRX = new TextBlock();
							tbATRX.Padding = new Thickness(0, 2, 20, 2);
							var atrxKey = trade.ATRX.Keys.FirstOrDefault(k => k.Date == tradesTime2.Date);
							tbATRX.Text = atrxKey != default(DateTime) ? trade.ATRX[atrxKey].ToString("0.00", CultureInfo.CurrentCulture) : "";
							tbATRX.HorizontalAlignment = HorizontalAlignment.Right;
							tbATRX.FontSize = 9;
							tbATRX.Foreground = Foreground = Brushes.Silver;
							DataGridColumnATRX.Children.Add(tbATRX);

							// Col 11 is PnL
							TextBlock tbPL = new TextBlock();
							tbPL.Text = returns.ContainsKey(ticker) ? returns[ticker].ToString("0.00", CultureInfo.InvariantCulture) + " %" : "";
							tbPL.Padding = new Thickness(0, 2, 0, 2); //37
							tbPL.HorizontalAlignment = HorizontalAlignment.Right;
							tbPL.FontSize = 9;
							tbPL.Foreground = returns[ticker] > 0 ? Brushes.PaleGreen : returns[ticker] < 0 ? Brushes.Crimson : Brushes.White;
							tbPL.Tag = ticker;
							tbPL.MouseLeftButtonDown += TickerClicked;
							DataGridColumnPL.Children.Add(tbPL);

							// Col 11 KELLY
							//TextBlock tbKelly = new TextBlock();
							//tbKelly.Padding = new Thickness(0, 2, 20, 2);
							//tbKelly.Text = price[ticker].ToString(".00");  // chg to Entry Price
							//tbKelly.HorizontalAlignment = HorizontalAlignment.Right;
							//tbKelly.FontSize = 9;
							//tbKelly.Foreground = Foreground = Brushes.Silver;
							//DataGridColumnKelly.Children.Add(tbKelly);

							// Col 12 VOL
							//TextBlock tbVOL = new TextBlock();
							//tbVOL.Padding = new Thickness(0, 2, 20, 2);
							//tbVOL.Text = price[ticker].ToString(".00");  // chg to Entry Price
							//tbVOL.HorizontalAlignment = HorizontalAlignment.Right;
							//tbVOL.FontSize = 9;
							//tbVOL.Foreground = Foreground = Brushes.Silver;
							//DataGridColumnVOL.Children.Add(tbVOL);

							// Col 13 Rixk
							//TextBlock tbRisk = new TextBlock();
							//tbRisk.Padding = new Thickness(0, 2, 20, 2);
							//tbRisk.Text = price[ticker].ToString(".00");  // chg to Entry Price
							//tbRisk.HorizontalAlignment = HorizontalAlignment.Right;
							//tbRisk.FontSize = 9;
							//tbRisk.Foreground = Foreground = Brushes.Silver;
							//DataGridColumnRisk.Children.Add(tbRisk);

							//Col 4 Order
							//TextBlock tb5 = new TextBlock();
							//tb5.Text = sectors[ticker];
							//tb5.Text = (direction > 0 ? "B " : "S ") + units.ToString("#") + " " + t2;
							//tb5.Tag = ticker;
							//tb5.Padding = new Thickness(0, 2, 35, 2);
							//tb5.HorizontalAlignment = HorizontalAlignment.Left;
							//tb5.FontSize = 9;
							//tb5.Foreground = brush;
							//tb5.MouseLeftButtonDown += TickerTagClicked;
							//DataGridColumnOrders.Children.Add(tb5);

							// Col 4 Shares
							//TextBlock tb4 = new TextBlock();
							//tb4.Text = sectors[ticker];
							//tb4.Text = shares[ticker].ToString("0");  // chg to Shares 
							//tb4.Padding = new Thickness(35, 2, 20, 2);
							//tb4.HorizontalAlignment = HorizontalAlignment.Right;
							//tb4.FontSize = 11;
							//Color color = Color.FromRgb(0xff, 0xff, 0xff);
							//tb4.Foreground = new SolidColorBrush(color);
							//DataGridColumn6.Children.Add(tb4);


							// Col 6 is Weight
							TextBlock tb6 = new TextBlock();
							tb6.Padding = new Thickness(0, 2, 20, 2);
							tb6.Text = (model.PortfolioWeight == Model.PortfolioWeightType.Equal || !weights.ContainsKey(ticker)) ? "1.0" : (100 * weights[ticker]).ToString(".00");
							tb6.HorizontalAlignment = HorizontalAlignment.Right;
							tb6.FontSize = 9;
							//tb6.Foreground = new SolidColorBrush(color);
							//DataGridColumn8.Children.Add(tb6);

							// Col 6 is Score
							TextBlock tb7 = new TextBlock();
							tb7.Text = scores[ticker].ToString("0.###");
							tb7.Padding = new Thickness(0, 2, 35, 2);  //43 70
							tb7.HorizontalAlignment = HorizontalAlignment.Right;
							tb7.FontSize = 9;
							//tb7.Foreground = new SolidColorBrush(color);
							//DataGridColumn9.Children.Add(tb7);

							// Col 7 is Return
							//TextBlock tb8 = new TextBlock();
							//tb8.Text = (double.IsNaN(returns[ticker])) ? "" : returns[ticker].ToString(".00");
							//tb8.Padding = new Thickness(0, 2, 40, 2); //37
							//tb8.HorizontalAlignment = HorizontalAlignment.Right;
							//tb8.FontSize = 11;
							//tb8.Foreground = new SolidColorBrush(color);
							//DataGridColumn3.Children.Add(tb8);
						}

						if (direction >= 2 || direction <= -2)
						{
							//TextBlock tb8 = new TextBlock();
							//tb8.Text = (direction == 0 || direction == 0) ? item.CloseDateTime.ToString("MMM dd yyyy") : item.OpenDateTime.ToString("MMM dd yyyy");
							//tb8.Padding = new Thickness(5, 2, 0, 2);
							//tb8.HorizontalAlignment = HorizontalAlignment.Left;
							//tb8.FontSize = 11;
							//tb8.Foreground = new SolidColorBrush(Colors.White);
							//DataGridColumnOrderDate.Children.Add(tb8);

							TextBlock tb9 = new TextBlock();
							tb9.Text = "Current Close";
							//tb9.Text = (RBSlippageCurrentClose.IsChecked == true) ? "On or Before Close" : "On Next Bar Open";
							tb9.Padding = new Thickness(5, 2, 0, 2);
							tb9.HorizontalAlignment = HorizontalAlignment.Left;
							tb9.FontSize = 9;
							tb9.Foreground = new SolidColorBrush(Colors.White);
							//DataGridColumnOrderType.Children.Add(tb9);

							TextBlock tb = new TextBlock();
							tb.Padding = new Thickness(0, 2, 0, 2);
							tb.HorizontalAlignment = HorizontalAlignment.Right;
							tb.FontSize = 9;
							tb.Foreground = brush;

							TextBlock tbBuy = new TextBlock();
							tbBuy.Text = "B " + units.ToString("N0", CultureInfo.InvariantCulture) + " " + t2;
							tbBuy.Tag = ticker;
							tbBuy.Padding = new Thickness(0, 2, 0, 2);
							tbBuy.HorizontalAlignment = HorizontalAlignment.Left;
							tbBuy.FontSize = 9;
							tbBuy.Foreground = brush;
							tbBuy.MouseLeftButtonDown += TickerClicked;

							TextBlock tbSell = new TextBlock();
							tbSell.Text = "S " + units.ToString("N0", CultureInfo.InvariantCulture) + " " + t2;
							tbSell.Tag = ticker;
							tbSell.Padding = new Thickness(0, 2, 0, 2);
							tbSell.HorizontalAlignment = HorizontalAlignment.Left;
							tbSell.FontSize = 9;
							tbSell.Foreground = brush;
							tbSell.MouseLeftButtonDown += TickerClicked;

							if (direction == 2)
							{
								DataGridColumnNewLong.Children.Add(tbBuy);
							}
							else if (direction == 3)
							{
								DataGridColumnExitLong.Children.Add(tbSell);
							}
							else if (direction == 4)
							{
								DataGridColumnAddLong.Children.Add(tbBuy);
							}
							else if (direction == 5)
							{
								DataGridColumnReduceLong.Children.Add(tbSell);
							}
							else if (direction == -2)
							{
								DataGridColumnNewShort.Children.Add(tbSell);
							}
							else if (direction == -3)
							{
								DataGridColumnCoverShort.Children.Add(tbBuy);
							}
							else if (direction == -4)
							{
								DataGridColumnAddShort.Children.Add(tbSell);
							}
							else if (direction == -5)
							{
								DataGridColumnReduceShort.Children.Add(tbBuy);
							}
						}
					}

					// Fix 2b: DataGrid1 shows only confirmed holdings (oldTrades, rendered above).
					// Render proposed order labels into DataGrid2 columns here as a separate pass.
					if ((isPreOrderFriday || isProjectedRebalDate) && hasOrders && !isIntraWeekDisplay)
					{
						var proposedOrders = new List<Trade>();
						proposedOrders.AddRange(enteredTrades);
						proposedOrders.AddRange(exitedTrades);
						proposedOrders.AddRange(addTrades);
						proposedOrders.AddRange(reduceTrades);

						foreach (var ot in proposedOrders)
						{
							var ticker = ot.Ticker;
							if (!shares.ContainsKey(ticker) || !price.ContainsKey(ticker)) continue;
							var t2Label = ticker.Split(' ')[0];
							int dir = (int)ot.Direction;

							// For add/reduce, shares[ticker] holds tradesTime1 size (confirmed position).
							// Delta must come from tradesTime2 shares minus tradesTime1 shares.
							var ot2Key = ot.Shares.Keys
								.Where(k => k.Date == tradesTime2.Date && ot.Shares[k] != 0)
								.OrderByDescending(k => k).FirstOrDefault();
							var ot2Shares = ot2Key != default(DateTime) ? ot.Shares[ot2Key] : 0;
							var ot1Key = ot.Shares.Keys
								.Where(k => k.Date == tradesTime1.Date)
								.OrderByDescending(k => k).FirstOrDefault();
							var ot1Shares = ot1Key != default(DateTime) ? ot.Shares[ot1Key] : 0;
							var unitsO = (Math.Abs(dir) == 3)
								? Math.Abs(shares[ticker])              // exit: full confirmed position
								: (Math.Abs(dir) == 4 || Math.Abs(dir) == 5)
									? Math.Abs(ot2Shares - ot1Shares)   // add/reduce: t2 minus t1 delta
									: Math.Abs(ot2Shares);              // new entry: full new size

							Brush ob = dir == 2 ? Brushes.PaleGreen
									 : dir == 3 ? Brushes.Cyan
									 : dir == 4 ? Brushes.PaleGreen
									 : dir == 5 ? Brushes.Cyan
									 : dir == -2 ? Brushes.Crimson
									 : dir == -3 ? Brushes.Yellow
									 : dir == -4 ? Brushes.Crimson
									 : dir == -5 ? Brushes.Yellow
									 : Brushes.White;

							var tbB = new TextBlock { Text = "B " + unitsO.ToString("N0", CultureInfo.InvariantCulture) + " " + t2Label, Tag = ticker, Padding = new Thickness(0, 2, 0, 2), HorizontalAlignment = HorizontalAlignment.Left, FontSize = 9, Foreground = ob };
							tbB.MouseLeftButtonDown += TickerClicked;
							var tbS = new TextBlock { Text = "S " + unitsO.ToString("N0", CultureInfo.InvariantCulture) + " " + t2Label, Tag = ticker, Padding = new Thickness(0, 2, 0, 2), HorizontalAlignment = HorizontalAlignment.Left, FontSize = 9, Foreground = ob };
							tbS.MouseLeftButtonDown += TickerClicked;

							if (dir == 2) DataGridColumnNewLong.Children.Add(tbB);
							else if (dir == 3) DataGridColumnExitLong.Children.Add(tbS);
							else if (dir == 4) DataGridColumnAddLong.Children.Add(tbB);
							else if (dir == 5) DataGridColumnReduceLong.Children.Add(tbS);
							else if (dir == -2) DataGridColumnNewShort.Children.Add(tbS);
							else if (dir == -3) DataGridColumnCoverShort.Children.Add(tbB);
							else if (dir == -4) DataGridColumnAddShort.Children.Add(tbS);
							else if (dir == -5) DataGridColumnReduceShort.Children.Add(tbB);
						}
					}

					//TotalInvestment.Content = "$" + total.ToString("N0", CultureInfo.InvariantCulture); // → "$12,000.00"

					//PortfolioBeta.Content = FormattableString.Invariant($"{beta:0.00}");

					// Use live balance only when cursor is on today
					bool isLiveNow = _showHoldingsTime == default(DateTime) || _showHoldingsTime.Date >= DateTime.Today;
					if (_liveMtMBalance > 0 && time2.Date == DateTime.Today && isLiveNow)
						portfolioBalance = _liveMtMBalance;

					var grossInvestment = (longAmount + shortAmount) / portfolioBalance;
					var netExposure = (longAmount - shortAmount) / portfolioBalance;
					var longExposure = longAmount / portfolioBalance;
					var shortExposure = shortAmount / portfolioBalance;
					_lastGrossExposure = grossInvestment;
					_lastNetExposure = netExposure;
					_lastLongExposure = longExposure;
					_lastShortExposure = shortExposure;

					var volatility = getAnnVol();
					var annVol = volatility.Count > 0 ? volatility.Last() : double.NaN;
					PortfolioVOL.Content = double.IsNaN(annVol) ? "" : $"{annVol:0.00}";
					GrossInvestment.Content = grossInvestment.ToString("P2", CultureInfo.InvariantCulture); // → "12.34%"
					NetExposure.Content = netExposure.ToString("P2", CultureInfo.InvariantCulture); // → "12.34%"
					TotalLongs.Content = longExposure.ToString("P2", CultureInfo.InvariantCulture); // → "12.34%"
					TotalShorts.Content = shortExposure.ToString("P2", CultureInfo.InvariantCulture); // → "12.34%"

					// Compliance: daily risk snapshot
					//AuditService.LogDailyRisk(
					//	portfolioId: model?.Name ?? _clientPortfolioName,
					//	isLive: model?.IsLiveMode == true,
					//	nav: portfolioBalance,
					//	longExposure: longExposure,
					//	shortExposure: shortExposure,
					//	netExposure: netExposure,
					//	grossExposure: grossInvestment,
					//	longCount: (int)getHoldings(time2, 1, 1),
					//	shortCount: (int)getHoldings(time2, -1, 1),
					//	sectorPercents: sectorPercents,
					//	modelVersion: model?.Name
					//);
				}
			}
			InvalidateVisual();
		}

		private void updateAutoResults()
		{
			var model = getModel();

			var interval = model.RankingInterval;

			var lines = new List<string>();

			var path = @"portfolio-senarios\" + model.Name + @"\" + interval + @"\result.csv";
			var results = MainView.LoadUserData(path);

			lines = results.Split(':').ToList();

			header.Content = "Rank";

			var labels = new Dictionary<string, List<Label>>();
			labels["row"] = new List<Label> { row, row1, row2, row3, row4, row5, row6, row7, row8, row9, row10, row11, row12, row13, row14 };
			labels["name"] = new List<Label> { name, name1, name2, name3, name4, name5, name6, name7, name8, name9, name10, name11, name12, name13, name14 };
			labels["acc"] = new List<Label> { acc, acc1, acc2, acc3, acc4, acc5, acc6, acc7, acc8, acc9, acc10, acc11, acc12, acc13, acc14 };
			labels["auc"] = new List<Label> { auc, auc1, auc2, auc3, auc4, auc5, auc6, auc7, auc8, auc9, auc10, auc11, auc12, auc13, auc14 };
			labels["aup"] = new List<Label> { aup, aup1, aup2, aup3, aup4, aup5, aup6, aup7, aup8, aup9, aup10, aup11, aup12, aup13, aup14 };
			labels["f1"] = new List<Label> { f1, f11, f12, f13, f14, f15, f16, f17, f18, f19, f110, f111, f112, f113, f114 };

			for (var row1 = 0; row1 < labels["name"].Count; row1++)
			{
				labels["row"][row1].Content = "";
				labels["name"][row1].Content = "";
				labels["acc"][row1].Content = "";
				labels["auc"][row1].Content = "";
				labels["aup"][row1].Content = "";
				labels["f1"][row1].Content = "";
			}

			//var brush1 = Brushes.White;
			var brush1 = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));
			var brush2 = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));

			var idx = 0;
			lines.ForEach(line =>
			{
				var items = line.Split(',');
				if (idx < labels["name"].Count && items.Length >= 5)
				{
					if (idx == 0)
					{
						//BestName.Content = items[0];
						//BestAcc.Content = items[1];
					}
					labels["row"][idx].Content = (idx + 1).ToString();
					labels["name"][idx].Content = items[0];
					labels["acc"][idx].Content = items[1];
					labels["auc"][idx].Content = items[2];
					labels["aup"][idx].Content = items[3];
					labels["f1"][idx].Content = items[4];

					var brush = (idx == 0) ? brush2 : brush1;
					labels["name"][idx].Foreground = brush;
					labels["acc"][idx].Foreground = brush;
					labels["auc"][idx].Foreground = brush;
					labels["aup"][idx].Foreground = brush;
					labels["f1"][idx].Foreground = brush;
					labels["f1"][idx].Foreground = brush;

					idx++;
				}
			});
		}

		private void TickerClicked(object sender, MouseButtonEventArgs e)
		{
			var tb = sender as TextBlock;
			var ticker = tb.Tag as string;
			gotoChart(ticker);
		}
		private void TradeDateClicked(object sender, MouseButtonEventArgs e)
		{
			var tb = sender as TextBlock;
			var ticker = tb.Tag as string;
			var date = DateTime.ParseExact(tb.Text, "MM-dd-yy", null);
			gotoChart(ticker, date);
		}

		private void gotoChart(string ticker, DateTime? date = null)
		{
			addCharts();

			var gotoInterval2 = _interval;
			var gotoInterval1 = Study.getForecastInterval(gotoInterval2, 1);

			// Request bars for both intervals immediately -- each chart
			// is independent and will populate as its bars arrive.
			if (!string.IsNullOrEmpty(ticker))
			{
				_barCache.RequestBars(ticker, gotoInterval1, true);
				_barCache.RequestBars(ticker, gotoInterval2, true);
			}

			var model = getModel();
			updateChartAnalysisSettings(model);
			var portfolioName = (model == null) ? "" : model.Name;

			// Open both charts immediately -- bars populate as they arrive
			if (_twoCharts)
			{
				changeChart(_chart1, ticker, gotoInterval1, portfolioName);
				changeChart(_chart2, ticker, gotoInterval2, portfolioName);
			}
			else
			{
				changeChart(_chart1, ticker, gotoInterval2, portfolioName);
			}

			if (date != null && _chart1 != null)
			{
				_chart1.ScrollToTime(date.Value);
				if (_chart2 != null) _chart2.ScrollToTime(date.Value);
			}
		}

		Chart _chart1 = null;
		Chart _chart2 = null;
		bool _twoCharts = true;
		string _pendingChart2Ticker = null;
		string _pendingChart2Interval = null;
		string _pendingChart2Portfolio = null;
		ConditionDialog _conditionDialog;

		private void showCharts()
		{
			if (!_twoCharts)
			{
				Grid.SetColumnSpan(ChartCanvasBorder1, 2);
				if (_chart1 != null)
				{
					//_chart1.SpreadSymbols = true;
					_chart1.HasTitleIntervals = true;
					_chart1.Show();
				}

				if (_chart2 != null)
				{
					ChartCanvasBorder2.Visibility = Visibility.Collapsed;
					_chart2.Hide();
				}
			}
			else
			{
				Grid.SetColumnSpan(ChartCanvasBorder1, 1);
				if (_chart1 != null)
				{
					_chart1.SpreadSymbols = false;
					_chart1.HasTitleIntervals = true;
					_chart1.Show();
				}

				if (_chart2 != null)
				{
					ChartCanvasBorder2.Visibility = Visibility.Visible;
					_chart2.HasTitleIntervals = true;
					_chart2.Show();
				}
			}
		}

		private void changeChart(Chart chart, string symbol, string interval, string portfolioName)
		{
			if (chart != null)
			{
				if (portfolioName != "")
				{
					chart.PortfolioName = portfolioName;
				}

				bool noCharts = (!chart.IsVisible());

				showCharts();

				if (noCharts)
				{
					chart.Show();
				}

				if (symbol != chart.Symbol || interval != chart.Interval)
				{
					chart.Change(symbol, interval);
				}
			}
		}

		private void addCharts()
		{
			if (_chart1 == null)
			{
				ChartGrid.Visibility = Visibility.Visible;
				Grid.SetRowSpan(RebalanceGrid, 1);

				bool showCursor = !_mainView.HideChartCursor;

				var modelNames = new List<string>();
				modelNames.Add(_selectedUserFactorModel);

				_chart1 = new Chart(ChartCanvas1, ChartControlPanel1, showCursor);
				//_chart1.SpreadSymbols = true;
				_chart1.HasTitleIntervals = true;
				_chart1.Horizon = 2;
				_chart1.Strategy = getStrategy();
				_chart1.ModelNames = modelNames;
				_chart1.ChartEvent += new ChartEventHandler(Chart_ChartEvent);

				_chart2 = new Chart(ChartCanvas2, ChartControlPanel2, showCursor);
				_chart2.SpreadSymbols = true;
				_chart2.HasTitleIntervals = false;
				_chart2.Horizon = 1;
				_chart2.Strategy = getStrategy();
				_chart2.ModelNames = modelNames;
				_chart2.ChartEvent += new ChartEventHandler(Chart_ChartEvent);

				_chart1.AddLinkedChart(_chart2);
				_chart2.AddLinkedChart(_chart1);

				loadChartProperties(1);
				loadChartProperties(2);
				var model = getModel(_selectedUserFactorModel);
				updateChartAnalysisSettings(model);
				addBoxes(false);
			}
		}

		private void hideChart()
		{
			if (_chart1 != null)
			{
				ChartGrid.Visibility = Visibility.Collapsed;
				Grid.SetRowSpan(RebalanceGrid, 2);

				_chart1.Hide();
				_chart2.Hide();

				removeCharts();
			}
		}

		private void removeCharts()
		{
			if (_chart1 != null)
			{
				saveChartProperties();

				_chart1.ChartEvent -= new ChartEventHandler(Chart_ChartEvent);
				_chart1.Close();
				_chart1 = null;

				_chart2.ChartEvent -= new ChartEventHandler(Chart_ChartEvent);
				_chart2.Close();
				_chart2 = null;
			}
		}

		void Chart_ChartEvent(object sender, ChartEventArgs e)
		{
			Chart chart = sender as Chart;
			if (e.Id == ChartEventType.ChangeDate)
			{
				var date = chart.GetCursorTime();
				if (date == default(DateTime)) return;   // ignore reset fired by symbol change
				setHoldingsCursorTime(date);
			}
			else if (e.Id == ChartEventType.SettingChange)
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
					chart.Print(printDialog);
				}
			}
			else if (e.Id == ChartEventType.ChangeSymbol) // change symbol change
			{
			}
			else if (e.Id == ChartEventType.ToggleCursor) // toggle cursor on/off
			{
				_mainView.HideChartCursor = !chart.ShowCursor;
			}
			else if (e.Id == ChartEventType.SetupConditions)
			{
				ConditionDialog dlg = getConditionDialog();
				int horizon = chart.Horizon;
				dlg.Condition = MainView.GetConditions(horizon);
				dlg.Horizon = horizon;

				//DialogWindow.Show();
			}
			else if (e.Id == ChartEventType.PartitionCharts)
			{
				_twoCharts = !_twoCharts;
				string interval2 = getInterval(_interval, 1);
				if (_twoCharts)
				{
					changeChart(_chart1, _symbol, interval2, getPortfolioName());
					changeChart(_chart2, _symbol, _interval, getPortfolioName());
				}
				else
				{
					changeChart(_chart1, _symbol, _interval, getPortfolioName());
				}
				showCharts();
			}
		}

		private ConditionDialog getConditionDialog()
		{
			if (_conditionDialog == null)
			{
				_conditionDialog = new ConditionDialog(_mainView);
				_conditionDialog.DialogEvent += new DialogEventHandler(dialogEvent);
			}
			//DialogWindow.Content = _conditionDialog;
			return _conditionDialog;
		}
		private void dialogEvent(object sender, DialogEventArgs e)
		{
			StrategyBuilder dialog = sender as StrategyBuilder;
			if (dialog != null)
			{
				DialogEventArgs.EventType type = e.Type;
				if (e.Type == DialogEventArgs.EventType.Ok)
				{
					Dictionary<string, Strategy> oldStrategies = _strategies;
					Dictionary<string, Strategy> newStrategies = dialog.Strategies;

					foreach (var kvp in newStrategies)
					{
						string name = kvp.Key;
						Strategy newStrategy = kvp.Value;

						Strategy oldStrategy;
						if (oldStrategies.TryGetValue(name, out oldStrategy))
						{
							if (newStrategy.CompareTo(oldStrategy) != 0)
							{
								saveStrategy(name, newStrategy);
							}
						}
						else
						{
							saveStrategy(name, newStrategy);
						}
					}

					foreach (var kvp in oldStrategies)
					{
						string name = kvp.Key;
						Strategy oldStrategy = kvp.Value;
						if (!newStrategies.ContainsKey(name))
						{
							deleteStrategy(name);
						}
					}
					_strategies = newStrategies;
					initializeStrategyList();
				}
			}

			ConditionDialog dlg1 = sender as ConditionDialog;
			if (dlg1 != null)
			{
				DialogEventArgs.EventType type = e.Type;
				if (e.Type == DialogEventArgs.EventType.Ok)
				{
					int horizon = dlg1.Horizon;
					string[] conditions = dlg1.Condition;

					MainView.SetConditions(horizon, conditions);
				}
			}

			//DialogWindow.Visibility = System.Windows.Visibility.Hidden;
		}

		private void initializeStrategyList()
		{
			//StrategyList.Children.Clear();

			foreach (string strategy in _strategies.Keys)
			{
				if (_selectedStrategy.Length == 0) _selectedStrategy = strategy;
				Label label = getStrategyLabel(strategy);
				label.MouseLeftButtonDown += Label_MouseLeftButtonDown;
				label.HorizontalAlignment = HorizontalAlignment.Left;
				label.Width = 125;
				update();
			}
		}

		private void addBoxes(bool allCharts)
		{
			List<string> imageNames = new List<string>();
			imageNames.Add(@"Images/CloseChart3.png:Close Charts");
			imageNames.Add(allCharts ? @"Images/TileChart.png:Tile Chart" : @"Images/TileChart.png:Tile Charts");
			imageNames.Add(@"Images/Refresh10.png:Refresh Data");
			imageNames.Add(@"Images/Track17.png:Cursor On/Off");
			//imageNames.Add(@"Images/printer icon2.png:Print Chart");
			_chart1.InitializeControlBox(imageNames);
			_chart2.InitializeControlBox(imageNames);
		}

		private string getStrategy()
		{
			string output = "Strategy 1";
			if (_interval == "Monthly" || _interval == "Quarterly")
			{
				output = "Strategy 2";
			}
			return output;
		}

		private void loadChartProperties(int chartNumber)
		{
			bool research = false;

			Chart chart = null;
			if (chartNumber == 1) chart = _chart1;
			else if (chartNumber == 2) chart = _chart2;

			if (chart != null)
			{
				//chart.Indicators["ATM Trend Bars"].Enabled = (research);

				chart.Indicators["ATM Trend Lines"].Enabled = (research);

				chart.Indicators["ATM Trigger"].Enabled = (research);

				chart.Indicators["ATM 3Sigma"].Enabled = (research);

				chart.Indicators["ATM Targets"].Enabled = (false);

				chart.Indicators["X Alert"].Enabled = (research);

				chart.Indicators["First Alert"].Enabled = (false);

				chart.Indicators["Add On Alert"].Enabled = (research);

				chart.Indicators["Pullback Alert"].Enabled = (research);

				chart.Indicators["Pressure Alert"].Enabled = (research);

				//chart.Indicators["PT Alert"].Enabled = (research);

				chart.Indicators["Exhaustion Alert"].Enabled = (research);

				//chart.Indicators["ATM Divergence Alert"].Enabled = (false);

				//chart.Indicators["Two Bar Trend"].Enabled = (false);

				chart.Indicators["Two Bar Alert"].Enabled = (false);

				//chart.Indicators["ADX Up Alert"].Enabled = (false);

				//chart.Indicators["ADX Dn Alert"].Enabled = (false);

				chart.Indicators["FT Alert"].Enabled = (false);

				chart.Indicators["ST Alert"].Enabled = (false);

				chart.Indicators["FTST Alert"].Enabled = (false);

				//chart.Indicators["TRT Alert"].Enabled = (false);

				chart.Indicators["EW Counts"].Enabled = (false);

				chart.Indicators["EW PTI"].Enabled = (false);

				chart.Indicators["EW Projections"].Enabled = (false);

				//chart.Indicators["Current Recommendations"].Enabled = (true);

				//chart.Indicators["Historical Recommendations"].Enabled = (false);

				//chart.Indicators["Risk Area"].Enabled = (false);

				chart.Indicators["Short Term FT Current"].Enabled = (research);

				chart.Indicators["Short Term FT Nxt Bar"].Enabled = (research);

				chart.Indicators["Short Term ST Current"].Enabled = (research);

				chart.Indicators["Short Term ST Nxt Bar"].Enabled = (research);

				//chart.Indicators["Current TRT Turning Pt"].Enabled = (research);

				//chart.Indicators["Est TRTTP Next Bar"].Enabled = (research);

				//chart.Indicators["Current Chart Forecast"].Enabled = (true);

				string name = research ? "" + chartNumber + "_v13" : "Positions" + chartNumber + "_v13";
				chart.LoadProperties(name);
			}
		}

		private void saveChartProperties()
		{
			bool research = false;

			_chart1.SaveProperties(research ? "" : "Positions1_v13");
			_chart2.SaveProperties(research ? "" : "Positions2_v13");
		}


		private List<string> getTrainingIntervals(bool forStudies)
		{
			var intervals = Model.MLIntervals.ToList();
			if (forStudies)
			{
				intervals.Add(Study.getForecastInterval(intervals[0], 1));
			}
			return intervals;
		}

		//private void updateAutoResults(string interval)
		//{
		//    var model = getModel();
		//    var all = (model.Rebalance == "Best of All Intervals");

		//    var lines = new List<string>();

		//    var intervals = getTrainingIntervals(false);

		//    if (all)
		//    {
		//        foreach (var interval1 in intervals)
		//        {
		//            var path = @"senarios\" + model.Name + @"\" + interval1 + @"\result.csv";
		//            var results = MainView.LoadUserData(path);
		//            var rows = results.Split(':');
		//            lines.Add((rows.Length > 0) ? rows[0] : "");
		//        }
		//    }
		//    else
		//    {
		//        var path = @"senarios\" + model.Name + @"\" + interval + @"\result.csv";
		//        var results = MainView.LoadUserData(path);
		//        lines = results.Split(':').ToList();
		//    }

		//    header.Content = all ? "Interval" : "Trainer Rank";

		//    var labels = new Dictionary<string, List<Label>>();
		//    labels["row"] = new List<Label> { row, row1, row2, row3, row4, row5, row6, row7, row8, row9, row10, row11, row12, row13 };
		//    labels["name"] = new List<Label> { name, name1, name2, name3, name4, name5, name6, name7, name8, name9, name10, name11, name12, name13 };
		//    labels["acc"] = new List<Label> { acc, acc1, acc2, acc3, acc4, acc5, acc6, acc7, acc8, acc9, acc10, acc11, acc12, acc13 };
		//    labels["auc"] = new List<Label> { auc, auc1, auc2, auc3, auc4, auc5, auc6, auc7, auc8, auc9, auc10, auc11, auc12, auc13 };
		//    labels["aup"] = new List<Label> { aup, aup1, aup2, aup3, aup4, aup5, aup6, aup7, aup8, aup9, aup10, aup11, aup12, aup13 };
		//    labels["f1"] = new List<Label> { f1, f11, f12, f13, f14, f15, f16, f17, f18, f19, f110, f111, f112, f113 };

		//    for (var row1 = 0; row1 < labels["name"].Count; row1++)
		//    {
		//        labels["row"][row1].Content = "";
		//        labels["name"][row1].Content = "";
		//        labels["acc"][row1].Content = "";
		//        labels["auc"][row1].Content = "";
		//        labels["aup"][row1].Content = "";
		//        labels["f1"][row1].Content = "";
		//    }


		//    //var brush1 = Brushes.White;
		//    var brush1 = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));
		//    var brush2 = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));

		//    var idx = 0;
		//    lines.ForEach(line =>
		//    {
		//        var items = line.Split(',');
		//        if (idx < labels["name"].Count && items.Length >= 5)
		//        {
		//            if (idx == 0)
		//            {
		//                //BestName.Content = items[0];
		//                //BestAcc.Content = items[1];
		//            }
		//            labels["row"][idx].Content = all ? intervals[idx] : (idx + 1).ToString();
		//            labels["name"][idx].Content = items[0];
		//            labels["acc"][idx].Content = items[1];
		//            labels["auc"][idx].Content = items[2];
		//            labels["aup"][idx].Content = items[3];
		//            labels["f1"][idx].Content = items[4];

		//            var brush = (idx == 0 || all) ? brush2 : brush1;
		//            labels["name"][idx].Foreground = brush;
		//            labels["acc"][idx].Foreground = brush;
		//            labels["auc"][idx].Foreground = brush;
		//            labels["aup"][idx].Foreground = brush;
		//            labels["f1"][idx].Foreground = brush;
		//            labels["f1"][idx].Foreground = brush;

		//            idx++;
		//        }
		//    });
		//}


		private DateTime getRebalanceDate(Model model, DateTime input, List<DateTime> times)
		{
			DateTime output = new DateTime();
			for (int ii = 0; ii < times.Count; ii++)
			{
				DateTime time = times[ii];
				DateTime nextTime = (ii < times.Count - 1) ? times[ii + 1] : times[ii] + new TimeSpan(1, 0, 0, 0);
				if (rebalance(model, time, nextTime))
				{
					if (time >= input)
					{
						output = time;
						break;
					}
				}
			}
			return output;
		}

		private DateTime getPreviousRebalanceDate(Model model, DateTime input, List<DateTime> times)
		{
			DateTime output = new DateTime();

			int count = times.Count;
			for (int ii = count - 1; ii > 0; ii--)
			{
				DateTime time = times[ii];
				DateTime previousTime = (ii >= 1) ? times[ii - 1] : times[ii] - new TimeSpan(1, 0, 0, 0, 0);

				if (rebalance(model, previousTime, time))
				{
					if (previousTime < input)
					{
						output = previousTime;
						break;
					}
				}
			}
			return output;
		}


		//private DateTime getNextRebalanceDate(Model model, DateTime input, List<DateTime> times)
		//{
		//    DateTime output = new DateTime();

		//    int count = times.Count;
		//    for (int ii = count - 1; ii >= 0; ii--)
		//    {
		//        DateTime time = times[ii];
		//        DateTime nextTime = (ii < count - 1) ? times[ii + 1] : times[ii] + new TimeSpan(1, 0, 0, 0, 0);

		//        if (rebalance(model, time, nextTime))
		//        {
		//            output = time;
		//        }
		//    }
		//    return output;
		//}

		private void initFactorTree(TreeView treeView)
		{
			if (_fundamentalTree == null)
			{
				_fundamentalTree = new TreeNode("FundamentalMain");
			}
			treeView.ItemsSource = _fundamentalTree.Children;
		}

		public static string[] GetOutputList(string name = "", int level = 0)
		{
			string[] output = { };

			if (level == 0)
			{
				output = new string[] {
					"PERCENT CHANGE"
                    //"PERFORMANCE",
                    //"RISK"
                };
			}
			else if (name == "PERCENT CHANGE")
			{
				output = new string[] {
					"Percent Change Forward 1 Trading Day",
					"Percent Change Forward 5 Trading Days",
					"Percent Change Forward 10 Trading Days",
					"Percent Change Forward 20 Trading Days",
					"Percent Change Forward 30 Trading Days"
				};
			}
			return output;
		}

		private void initOutputTree(IAddChild parentItem, string parentName = "FundamentalMain", bool fundamental = false, int level = 0)
		{

			string[] childNames = GetOutputList(parentName, level);

			if (childNames.Length > 0)
			{
				foreach (string childName in childNames)
				{
					string[] field = childName.Split('\u0003');
					string name = field[0];
					string tooltip = (field.Length > 1) ? field[1] : "";

					TreeViewItem childItem = new TreeViewItem();

					Label label = new Label();
					label.Content = name;
					label.Padding = new Thickness(0, 2, 0, 2);
					label.Foreground = Brushes.White;
					label.FontSize = 11;
					label.FontFamily = new FontFamily("Helvetica Neue");
					if (tooltip.Length > 0) label.ToolTip = tooltip;
					childItem.Header = label;

					parentItem.AddChild(childItem);

					initOutputTree(childItem, name, fundamental, level + 1);
				}
			}
		}


		private void initStrategyTree()
		{
			ATMStrategyTree.Background = Brushes.Black;
			ATMStrategyTree.Foreground = Brushes.White;
			ATMStrategyTree.FontSize = 10;

			if (_atmStrategyTree == null)
			{
				_atmStrategyTree = new TreeNode("ATMStrategies", null, false);
			}
			ATMStrategyTree.ItemsSource = _atmStrategyTree.Children;
		}

		private void updateStrategyTree()
		{
			var model = getModel();
			if (model != null && model.Groups[_g].Strategy != null)
			{
				var strategy = model.Groups[_g].Strategy;
				var strategyFields = strategy.Split('(');
				var nodeName = strategyFields[0];
				//"CurrentResearch(Pressure=true;..."
				var node = getNode(nodeName, _atmStrategyTree);
				if (node != null)
				{
					node.IsSelected = false;
					node.IsChecked = true;
					if (strategyFields.Length > 1)
					{
						var parmText = strategyFields[1].Replace(")", "").Replace("\u0002", "");
						var parms = parmText.Split(';');
						foreach (var parm in parms)
						{
							var items = parm.Split('=');
							var parmName = items[0];
							var parmValue = items[1];
							var parmNode = getNode(parmName, node);
							if (parmNode != null)
							{
								parmNode.ParameterValue = parmValue;
								if (parmNode.IsBooleanParameter)
								{
									parmNode.IsChecked = bool.Parse(parmValue);
								}
							}
						}
					}
				}
			}
		}

		private TreeNode getNode(string name, TreeNode parent)
		{
			TreeNode output = null;
			if (parent.Children != null)
			{
				for (int ii = 0; ii < parent.Children.Count; ii++)
				{
					var childNode = parent.Children.ElementAt(ii);
					if (childNode.Name == name)
					{
						output = childNode;
						break;
					}
					else output = getNode(name, childNode);
				}
			}
			return output;
		}

		private string[] getIntervalList()
		{
			string[] list = { "Quarterly", "Monthly", "Weekly", "Daily", "240 Min", "120 Min", "60 Min", "30 Min", "15 Min", "5 Min" };
			return list;
		}

		private string getFactor(TreeView treeView)
		{
			string factor = "";

			TreeNode item = treeView.SelectedItem as TreeNode;

			if (item != null)
			{

				string interval = item.Interval;

				if (interval != null)
				{
					factor = item.Name + " " + "\u0002" + interval;
				}
				else
				{
					factor = item.Name;
				}
			}
			return factor;
		}

		private void barChanged(object sender, BarEventArgs e)
		{
			if (e.Type == BarEventArgs.EventType.BarsReceived)
			{
				string ticker = e.Ticker;
				string interval = e.Interval;

				if (_indexBarRequestCount > 0)
				{
					Series[] indexSeries = _barCache.GetSeries(ticker, interval, new string[] { "Close" }, 0, BarServer.MaxBarCount);
					if (indexSeries.Length > 0 && indexSeries[0].Count > 0)
					{
						_referenceData[ticker + ":" + interval] = indexSeries[0];
					}

					if (--_indexBarRequestCount == 0)
					{
						Model model = getModel();
						if (model != null)
						{
							requestSymbolBars(model);
						}
					}
				}
				else
				{
					Model model = getModel();
					if (model != null)
					{
						if (model.IsLiveMode)
						{
							// Trigger grid redraw on bar updates — but do NOT overwrite _portfolioTimes
							// from bar dates; rebalance dates are loaded from saved data in loadModel()
							// and must not be replaced with daily/weekly bar timestamps
							_update = 500;
						}
					}

					lock (_barRequests)
					{
						string key = ticker + ":" + interval;
						int index = _barRequests.IndexOf(key);
						if (index >= 0)
						{
							_barRequests.RemoveAt(index);
						}
					}
				}
			}
		}

		Dictionary<string, Series> _factorInputs = new Dictionary<string, Series>();

		private List<string> getRequiredHistoricalReferenceData(string factor)
		{
			List<string> output = new List<string>();

			// todo: figure out another way to distiguish between atm and fundamental factors
			if (factor.Length >= 3 && factor.Substring(0, 3) != "ATM")
			{
				output.Add(factor);
			}
			output = output.Distinct().ToList();
			return output;
		}

		private bool isATMFactor(string name)
		{
			return (name.Contains("ARBITRAGE"));
		}

		//private void getATMFactorInputs(string symbol)
		//{
		//    Model model = getModel();
		//    if (model != null)
		//    {
		//        var factors = model.FactorNames;
		//        foreach (var factor in factors)
		//        {
		//            if (isATMFactor(factor))
		//            {
		//                string conditionName = factor; // getConditionName(factor);
		//                string interval = model.Rebalance; // getConditionInterval(factor);
		//                string[] intervalList = { interval, Study.getForecastInterval(interval, 1) };

		//                string key = factor + ":" + symbol;
		//                _factorInputs[key] = getSignals(symbol, conditionName, intervalList);
		//            }
		//        }
		//    }
		//}

		//private int getOutputPeriod(Model model)  // is stk higher at end of rebalance
		//{
		//    return returnPeriod(model);
		//}

		private bool getStockVsIndex(Model model)  //does stk have positive alpha to index
		{
			return true;
		}

		//private Dictionary<string, Series> createModelOutputs(Model model, TimeRange timeRange, int numberOfCategories, bool stockVsIndex)
		//{
		//    string benchmark = model.Benchmark;

		//    int period = getOutputPeriod(model);

		//    string interval = "D";

		//    var symbols = getTickers(model);

		//    Dictionary<string, Series> scores = new Dictionary<string, Series>();
		//    List<double> allValues = new List<double>();
		//    int count = BarServer.MaxBarCount;

		//    var idxbars = getBars(benchmark, interval, count);

		//    foreach (var symbol in symbols)
		//    {
		//        var symbars = getBars(symbol, interval, count, idxbars);
		//        Series values = new Series(count);
		//        for (int ii = 0; ii < count; ii++)
		//        {
		//            var val1s = symbars[ii].Close;
		//            var val2s = (ii + period < count) ? symbars[ii + period].Close : double.NaN;
		//            var val1i = idxbars[ii].Close;
		//            var val2i = (ii + period < count) ? idxbars[ii + period].Close : double.NaN;
		//            if (!double.IsNaN(val1s) && !double.IsNaN(val2s) && !double.IsNaN(val1i) && !double.IsNaN(val2i))
		//            {
		//                var val = (val2s - val1s) / val1s;

		//                if (stockVsIndex)
		//                {
		//                    val -= ((val2i - val1i) / val1i);
		//                }

		//                values[ii] = val;

		//                var time = idxbars[ii].Time;
		//                if (timeRange.Time1 <= time && time < timeRange.Time2)
		//                {
		//                    allValues.Add(val);
		//                }
		//            }
		//        }
		//        scores[symbol] = values;
		//    }

		//    if (allValues.Count > 0)
		//    {

		//        allValues.Sort();

		//        List<Tuple<double, double>> ranges = new List<Tuple<double, double>>();

		//        double value1 = allValues[0];
		//        double increment = (double)allValues.Count / numberOfCategories;
		//        double indexAccumulator = 0;

		//        for (int ii = 0; ii < numberOfCategories; ii++)
		//        {
		//            indexAccumulator += increment;
		//            int index = (int)Math.Round(indexAccumulator);
		//            double value2 = allValues[index - 1];
		//            ranges.Add(new Tuple<double, double>(value1, value2));
		//            value1 = value2;
		//        }

		//        foreach (var symbol in symbols)
		//        {
		//            for (int jj = 0; jj < count; jj++)
		//            {
		//                double value = scores[symbol][jj];
		//                scores[symbol][jj] = 1;
		//                if (!double.IsNaN(value))
		//                {
		//                    for (int kk = 0; kk < numberOfCategories; kk++)
		//                    {
		//                        if (ranges[kk].Item1 <= value && value <= ranges[kk].Item2)
		//                        {
		//                            scores[symbol][jj] = kk + 1;
		//                            break;
		//                        }
		//                    }
		//                }
		//                else
		//                {
		//                    var bp = true;
		//                }
		//            }
		//        }
		//    }

		//    // 1 means down, 2 means up
		//    return scores;
		//}

		private bool _runModel = false;

		private bool _stopModel = false;

		private void deleteModel(string name)
		{
			var path = MainView.GetDataFolder() + @"\models\Models\" + name;
			try
			{
				File.Delete(path);
			}
			catch (Exception x)
			{
				System.Diagnostics.Debug.WriteLine(x);
			}
		}

		private Model getModel(string name = "")
		{
			Model model = null;
			var bp = false;
			try
			{
				if (name.Length == 0) name = _useUserFactorModel ? _selectedUserFactorModel : _selectedMLFactorModel;
				if (_useUserFactorModel && _userFactorModels != null && _userFactorModels.ContainsKey(name)) model = _userFactorModels[name] as Model;
				else if (!_useUserFactorModel && _MLFactorModels != null && _MLFactorModels.ContainsKey(name)) model = _MLFactorModels[name] as Model;

				if (model != null && model.NeedsLoading)
				{
					var path = MainView.GetDataFolder() + @"\models\Models";
					var files = Directory.GetFiles(path).ToList();
					files.ForEach(f =>
					{
						var p = f.Split('\\');
						var n = p.Last();
						if (n == name)
						{
							var fileName = String.Join("\\", p.Reverse().Take(3).Reverse());
							var data = MainView.LoadUserData(fileName);
							model = Model.load(data);

							if (model.Constraints.Count == 0)
							{
								model.Constraints = Model.getDefaultConstraints();
							}
							model.NeedsLoading = false;
							_userFactorModels[name] = model;
							userFactorModel_to_ui(name);
							// Update meta index so LIVE/TEST grouping stays accurate
							// as each model is lazily loaded for the first time.
							saveModelMeta();
							updateUserFactorModelList();
						}
					});
				}
			}
			catch (Exception x)
			{
				bp = true;
			}

			return model;
		}

		TextBlock _tb = new TextBlock();

		private void progressEvent(object sender, EventArgs e)
		{
			try
			{
				_progressState = _ic.ProgressState;
				_progressCompletedNumber = _ic.ProgressCompletedNumber;
				_progressTotalNumber = _ic.ProgressTotalNumber;
				_progressTime = _ic.ProgressTime;
				var modelName = _ic.ModelName;
				Dispatcher.BeginInvoke(new Action(() => { updateProgress(modelName); }));
			}
			catch (Exception x)
			{
				System.Diagnostics.Debug.WriteLine(x);
			}
		}

		LoadingAnimation busy1 = new LoadingAnimation(30, 30);

		private void updateProgress(string modelName)
		{

			//Collecting Fundamental and Px data
			if (_progressState == ProgressState.CollectingData)
			{
				if (_progressTotalNumber != 0)
				{
					var pc = (int)Math.Round(100.0 * _progressCompletedNumber / _progressTotalNumber);
					_tb.Text = pc.ToString() + "%";
					_tb.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xcc, 0xff));
					Collect.Content = _tb;
				}
				else
				{
					Collect.Content = busy1;
				}
				Process.Content = "\uD83D\uDD52";
				Train.Content = "\uD83D\uDD52";
				Predict.Content = "\uD83D\uDD52";
				Finish.Content = "\uD83D\uDD52";
			}

			// Ranking Fundamental data and calculating ATM outuput
			else if (_progressState == ProgressState.ProcessingData)
			{
				if (_progressTotalNumber != 0)
				{
					var pc = (int)Math.Round(100.0 * _progressCompletedNumber / _progressTotalNumber);
					_tb.Text = pc.ToString() + "%";
					_tb.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xcc, 0xff));
					Process.Content = _tb;
				}
				else
				{
					Process.Content = busy1;
				}
				Collect.Content = "\u2713";
				//Process.Content = busy1;
				Train.Content = "\uD83D\uDD52";
				Predict.Content = "\uD83D\uDD52";
				Finish.Content = "\uD83D\uDD52";
			}

			// Train Predict on each Rebalance period
			else if (_progressState == ProgressState.Training)
			{
				if (_progressTime > DateTime.Now)
				{
					_tb.Text = (_progressTime > DateTime.Now) ? (_progressTime - DateTime.Now).ToString(@"hh\:mm\:ss") : "\u2713";
					_tb.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xcc, 0xff));
					Train.Content = _tb;
				}
				else
				{
					Train.Content = busy1;
				}

				Collect.Content = "\u2713";
				Process.Content = "\u2713";
				Predict.Content = "\uD83D\uDD52";
				Finish.Content = "\uD83D\uDD52";
			}

			// Constructing Portolio and Calculating Returns
			else if (_progressState == ProgressState.Predicting)
			{
				if (_progressTotalNumber != 0)
				{
					var pc = (int)Math.Round(100.0 * _progressCompletedNumber / _progressTotalNumber);
					_tb.Text = pc.ToString() + "%";
					_tb.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xcc, 0xff));
					Predict.Content = _tb;
				}
				else
				{
					Predict.Content = busy1;
				}

				Collect.Content = "\u2713";
				Process.Content = "\u2713";
				Train.Content = "\u2713";
				Finish.Content = "\uD83D\uDD52";

				_progressTime = DateTime.Now;
			}

			else if (_progressState == ProgressState.Finished || _progressState == ProgressState.Complete)
			{
				if (_runModel)
				{
					_runModel = false;

					Collect.Content = "\u2713";
					Process.Content = "\u2713";
					Train.Content = "\u2713";
					Predict.Content = "\u2713";
					Finish.Content = "\u2713";

					_portfolioResultsTimestamp = ""; // Force fresh archive re-read after backtest
					loadModel();
					updateModelData();
					Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
					{
						loadModel();
						updateModelData();
					}));
					if (_chart1 != null) _chart1.ResetTrades();
					if (_chart2 != null) _chart2.ResetTrades();
					updateStatistics();
					update();

					// Compliance: daily risk snapshot — fires once per completed run
					var _riskModel = getModel();
					if (_riskModel != null)
					{
						var _riskBalance = getPortfolioBalance(_portfolioTimes.Count > 0 ? _portfolioTimes.Last() : DateTime.Today);
						var _riskLong = (double)getHoldings(_portfolioTimes.Count > 0 ? _portfolioTimes.Last() : DateTime.Today, 1, 1);
						var _riskShort = (double)getHoldings(_portfolioTimes.Count > 0 ? _portfolioTimes.Last() : DateTime.Today, -1, 1);
						AuditService.LogDailyRisk(
							portfolioId: _riskModel.Name,
							isLive: _riskModel.IsLiveMode,
							nav: _riskBalance,
							longExposure: _lastLongExposure,
							shortExposure: _lastShortExposure,
							netExposure: _lastNetExposure,
							grossExposure: _lastGrossExposure,
							longCount: (int)_riskLong,
							shortCount: (int)_riskShort,
							sectorPercents: sectorPercents,
							modelVersion: _riskModel.Name
						);
					}

					// Compliance: generate exception and audit reports
					ExceptionReportPdf.Generate(DateTime.UtcNow.AddDays(-30), DateTime.MaxValue);
					AuditReportPdf.Generate(DateTime.UtcNow.AddDays(-30), DateTime.MaxValue);
					// Reset pre-order flag on every fresh run so the user sees the
					// current portfolio preview before pressing Send Orders to FlexOne.
					_liveOrdersSent = false;

					if (_portfolioTimes.Count > 0)
					{
						var model = getModel();
						var date = _portfolioTimes.Last();
						if (model != null && model.IsLiveMode)
						{
							var allT = _portfolio1.GetTrades(model.Name);
							var projD = allT.SelectMany(t => t.Shares.Keys)
								.Where(k => k.Date > date.Date).OrderBy(k => k).FirstOrDefault();
							if (projD != default(DateTime))
							{
								_projectedRebalDate = projD;
								if (!_portfolioTimes.Any(t => t.Date == projD.Date)) _portfolioTimes.Add(projD);
								date = projD;
							}
							else _projectedRebalDate = default(DateTime);
						}
						setHoldingsCursorTime(date);
					}

					if (_stopModel)
					{
						if (ATMML.Auth.AuthContext.Current.IsAdmin)
							showPortfolioSetup();
						else
							showRebalanceGrid();
					}
					else
					{
						showRebalanceGrid();
					}
					PositionsChartBorder.Visibility = Visibility.Visible;
					TotalReturnChartBorder.Visibility = Visibility.Visible;
					ProgressCalculations2.Visibility = Visibility.Collapsed;

					// Live mode: show signal window status popup on rebalance Friday only
					var _liveModel = getModel();
					if (_liveModel != null && _liveModel.IsLiveMode
						&& DateTime.Today.DayOfWeek == DayOfWeek.Friday)
					{
						var _now = DateTime.Now.TimeOfDay;
						string _title, _msg;
						if (_now < TimeSpan.FromHours(15.0))
						{
							_title = "\u26a0 Pre-Signal Run";
							_msg = $"Run time: {DateTime.Now:HH:mm}\n\nThe 3:00 PM run is the authoritative signal.\nTrades generated now are preliminary only.";
						}
						else if (_now < TimeSpan.FromHours(16.0))
						{
							_title = "\u2705 Signal Window Open";
							_msg = $"Run time: {DateTime.Now:HH:mm}\n\nSignal window is open. These trades are final.\nSend MOC orders before 3:50 PM.";
						}
						else
						{
							_title = "\u2705 Market Closed";
							_msg = $"Run time: {DateTime.Now:HH:mm}\n\nMarket is closed. NAV is confirmed.\nTrades executed at MOC prices.";
						}
						MessageBox.Show(_msg, _title, MessageBoxButton.OK,
							_now < TimeSpan.FromHours(15.0) ? MessageBoxImage.Warning : MessageBoxImage.Information);
					}
				}
			}
		}

		private ProgressState _progressState = ProgressState.Complete;
		private DateTime _progressTime = DateTime.Now;
		private int _progressTotalNumber = 0;
		private int _progressCompletedNumber = 0;

		private Dictionary<string, Dictionary<DateTime, double>> _factorScores = new Dictionary<string, Dictionary<DateTime, double>>();

		//private void calculateAzureModelPrediction(Model model)
		//{
		//    var endPnt = "https://ussouthcentral.services.azureml.net/subscriptions/0111a19682204bf58bd9ba352d5d4cb4/services/17c1c9907b9841eb82b76564ee9ee5ae/jobs";

		//    var apiKey = "bh4qGj+jXXbspEBmHNl9OqghQEWfqbuSjugWGtEkxQPGkwYqi+FNmzjyq1z9tFUDjJKpdGtBPTr7sigAMfA6WQ==";

		//    var task = TrainModel.InvokeBatchExecutionService(false, endPnt, apiKey, @"scripts\Azure_test.csv");
		//    task.Wait();

		//    var info = new List<Tuple<string, DateTime>>();
		//    var sr1 = new StreamReader(@"scripts\azure_test.csv");
		//    string line1 = "";
		//    int row1 = 0;
		//    while ((line1 = sr1.ReadLine()) != null)
		//    {
		//        if (row1 > 0)
		//        {
		//            string[] fields = line1.Split(',');
		//            info.Add(new Tuple<string, DateTime>(fields[1], DateTime.ParseExact(fields[2], "yyyyMMdd", null)));
		//        }
		//        row1++;
		//    }
		//    sr1.Close();

		//    var sr2 = new StreamReader(@"scripts\azure_output_data.csv");
		//    string line2 = "";
		//    int row2 = 0;
		//    while ((line2 = sr2.ReadLine()) != null)
		//    {
		//        if (row2 > 0)
		//        {
		//            string[] fields = line2.Split(',');


		//            var probIndex = fields.Length - 1;
		//            var classIndex = fields.Length - 2;
		//            var prediction = double.Parse(fields[probIndex]);


		//            var symbol = info[row2 - 1].Item1;
		//            var date = info[row2 - 1].Item2;

		//            if (!_predictions.ContainsKey(symbol))
		//            {
		//                _predictions[symbol] = new Dictionary<DateTime, double>();
		//            }

		//            _predictions[symbol][date] = prediction;
		//        }
		//        row2++;
		//    }
		//    sr2.Close();

		//    calculatePortfolioReturn(model);  // version 1
		//    _update = true;
		//}

		private Label getStrategyLabel(string strategy)
		{
			Label label = new Label();
			label.Content = strategy;
			Color color = Color.FromRgb(0xff, 0xff, 0xff);
			label.Foreground = new SolidColorBrush(color);
			label.Background = (strategy == _selectedStrategy) ? new SolidColorBrush(Color.FromRgb(0x12, 0x4b, 0x72)) : Brushes.Transparent;
			//label.Padding = new Thickness(0, 2, 0, 2);
			label.Height = 22;
			label.HorizontalAlignment = HorizontalAlignment.Left;
			label.VerticalAlignment = VerticalAlignment.Top;
			label.FontFamily = new FontFamily("Helvetica Neue");
			label.FontWeight = FontWeights.Normal;
			label.FontSize = 10;
			label.Cursor = Cursors.Hand;
			label.MouseDown += Strategy_MouseDown;
			return label;
		}

		//private Series getSignals(string symbol, string conditionName, string[] intervalList)
		//{
		//    Series output = null;

		//    bool ok = true;
		//    Dictionary<string, List<DateTime>> times = new Dictionary<string, List<DateTime>>();
		//    Dictionary<string, Series[]> bars = new Dictionary<string, Series[]>();
		//    Dictionary<string, Series[]> indexBars = new Dictionary<string, Series[]>();
		//    for (int ii = 0; ii < intervalList.Length; ii++)
		//    {
		//        string interval = intervalList[ii];
		//        times[interval] = (_barCache.GetTimes(symbol, interval, 0, BarServer.MaxBarCount));
		//        bars[interval] = _barCache.GetSeries(symbol, interval, new string[] { "Open", "High", "Low", "Close" }, 0, BarServer.MaxBarCount);
		//        if (ii == 0) indexBars[interval] = _barCache.GetSeries(symbol, interval, new string[] { "Close" }, 0, BarServer.MaxBarCount);
		//        if (times[interval] == null || times[interval].Count == 0)
		//        {
		//            ok = false;
		//            break;
		//        }
		//    }

		//    if (ok)
		//    {
		//        int barCount = times[intervalList[0]].Count;
		//        output = Conditions.Calculate(conditionName, symbol, intervalList, barCount, times, bars, _referenceData);
		//    }

		//    // sync
		//    if (output != null && intervalList[0] != _interval)
		//    {
		//        var time1 = times[intervalList[0]];
		//        var time2 = _barCache.GetTimes(symbol, _interval, 0, BarServer.MaxBarCount);
		//        output = sync(output, intervalList[0], _interval, time1, time2);
		//    }

		//    return output;
		//}

		private Series sync(Series input, string interval1, string interval2, List<DateTime> times1, List<DateTime> times2, bool isBool = true)
		{
			int cnt = input.Count;

			int cnt1 = times1.Count;
			int cnt2 = times2.Count;

			int sidx1 = cnt1 - 1;

			Series output = new Series(cnt2, 0);

			string i1 = interval1.Substring(0, 1);
			string i2 = interval2.Substring(0, 1);
			int keySize1 = (i1 == "Y") ? 4 : ((i1 == "S" || i1 == "Q" || i1 == "M") ? 6 : ((i1 == "W" || i1 == "D") ? 8 : 12));
			int keySize2 = (i2 == "Y") ? 4 : ((i2 == "S" || i2 == "Q" || i2 == "M") ? 6 : ((i2 == "W" || i2 == "D") ? 8 : 12));
			int keySize = Math.Min(keySize1, keySize2);

			int offset = 0;
			if (i1 == "W" && keySize == 8) offset = 5;  //to place at current offset = 0
			else if (i1 == "Q" && keySize == 6) offset = 3;  //to place at current offset = 0
			else if (i1 == "S" && keySize == 6) offset = 6;  //to place at current offset = 0

			for (int idx2 = cnt2 - 1; idx2 >= 0; idx2--)
			{
				DateTime time2 = times2[idx2];
				long key2 = long.Parse(time2.ToString("yyyyMMddHHmm").Substring(0, keySize));

				for (int idx1 = sidx1; idx1 >= 0; idx1--)
				{
					DateTime time1 = times1[idx1];
					long key1 = long.Parse(time1.ToString("yyyyMMddHHmm").Substring(0, keySize));

					if (key1 - offset <= key2)
					{
						int idx = (cnt - 1) - ((cnt1 - 1) - idx1);

						if (isBool)
						{
							if (input[idx] == 1)
							{
								output[idx2] = 1;
							}
						}
						else
						{
							output[idx2] = input[idx];
						}

						sidx1 = idx1;
						break;
					}
				}
			}
			return output;
		}

		Dictionary<string, Strategy> _strategies;
		string _selectedStrategy = "";

		Dictionary<string, Model> _MLFactorModels;
		string _selectedMLFactorModel = "";

		Dictionary<string, Model> _userFactorModels;
		string _selectedUserFactorModel = "";
		string _selectedUserFactorModel2 = "";
		string _selectedUserFactorModelBlue = "";

		private void Label_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			Label label = sender as Label;
			changeUserFactorModel((string)label.Content);
		}

		//private void alert_FlashEvent(object sender, EventArgs e)
		//{
		//    Alert alert = sender as Alert;
		//    if (alert.HasNotification())
		//    {
		//        _addFlash = true;
		//    }
		//}

		void alert_FlashEvent(object sender, EventArgs e)
		{
			Alert alert = sender as Alert;
			if (alert.HasNotification())
			{
				if (alert.UseOnTop)
				{
					this.Dispatcher.Invoke(DispatcherPriority.Normal, (Action)delegate () { _mainView.Activate(); });
				}
				_addFlash = true;
			}
		}

		private void new_Positions(object sender, NewPositionEventArgs e)
		{
			bool complete = e.Complete;
			if (complete)
			{
				_update = 8;
			}
		}

		void TradeEvent(object sender, TradeEventArgs e)
		{
			_update = 9;
		}


		List<string> _referenceSymbols = new List<string>();
		private Dictionary<string, object> _referenceData = new Dictionary<string, object>();

		void portfolio1Changed(object sender, PortfolioEventArgs e)
		{
			if (e.Type == PortfolioEventType.Symbol)
			{
				if (_memberPanel != null)
				{
					try
					{
						if (Application.Current != null) Application.Current.Dispatcher.Invoke(new Action(() => { loadMemberPanel(); }));
					}
					catch (Exception x)
					{
						System.Diagnostics.Debug.WriteLine(x);
					}
				}
				else
				{
					if (_portfolioRequested.Length > 0)
					{
						_portfolioRequestedCount--;
					}

					if (_portfolioRequested.Length > 0 && _portfolioRequestedCount == 0)
					{
						setModelSymbols(_portfolioRequested);

						//var symbols = _portfolio1.GetSymbols();
						//symbols.ForEach(symbol => _portfolio1.RequestBetas(symbol.Ticker));

						_portfolioRequested = "";
						//loadMemberList();
					}
				}
			}
			else if (e.Type == PortfolioEventType.ReferenceData)
			{
				bool okToRequestIndexBars = false;

				foreach (KeyValuePair<string, object> kvp in e.ReferenceData)
				{
					string name = kvp.Key;
					string value = kvp.Value as string;
					// Store live RT price for portfolio balance calculation
					if (name == "PRICE_LAST_RT")
					{
						if (kvp.Value is double rtPx && rtPx > 0)
						{
							lock (_rtPrices) { _rtPrices[e.Ticker] = (rtPx, DateTime.Now); }
							_update = 500; // trigger grid redraw
						}
						else if (double.TryParse(value, System.Globalization.NumberStyles.Any,
							System.Globalization.CultureInfo.InvariantCulture, out double parsed) && parsed > 0)
						{
							lock (_rtPrices) { _rtPrices[e.Ticker] = (parsed, DateTime.Now); }
							_update = 500;
						}
						else
						{
						}
					}
					else if (name == "REL_INDEX")
					{
						if (!value.Contains(" Index"))
						{
							value += " Index";
						}
						string key = e.Ticker + ":" + name;
						_referenceData[key] = value;

						System.Diagnostics.Debug.WriteLine("Reference list count " + _referenceSymbols.Count);

						if (_referenceSymbols.Count > 0)
						{
							for (int ii = 0; ii < _referenceSymbols.Count; ii++)
							{
								if (e.Ticker == _referenceSymbols[ii])
								{
									_referenceSymbols.RemoveAt(ii);
									break;
								}
							}
							okToRequestIndexBars = (_referenceSymbols.Count == 0);
						}
					}
					else if (name == "DS192")
					{
						var model = getModel();
						var index = model.Symbols.FindIndex(x => x.Ticker == e.Ticker);
						if (index != -1) model.Symbols[index].Description = _portfolio1.GetDescription(e.Ticker);
					}
					//else if (name.Contains("CUR_MKT_CAP"))
					//{
					//	var model = getModel();
					//	var index = model.Symbols.FindIndex(x => x.Ticker == e.Ticker);
					//	if (index != -1)
					//                   {
					//                       var fields = name.Split(':');
					//		model.Symbols[index].Marketcap[fields[1]] = Convert.ToDouble(kvp.Value);
					//	}
					//}
				}

				if (okToRequestIndexBars)
				{
					if (_referenceSymbols.Count == 0)
					{
						_indexSymbols = new List<string>();
						foreach (KeyValuePair<string, object> kvp in _referenceData)
						{
							string key = kvp.Key;
							if (key.Contains("REL_INDEX"))
							{
								string indexSymbol = kvp.Value as string;
								if (!_indexSymbols.Contains(indexSymbol) && indexSymbol != " Index")
								{
									_indexSymbols.Add(indexSymbol);
								}
							}
						}
						requestIndexBars();
					}
				}
			}
			//else if (e.Type == PortfolioEventType.Beta)
			//{
			//	var model = getModel();
			//	var symbol1 = model.Symbols.Find(x => x.Ticker == e.Ticker);
			//             var symbols = _portfolio1.GetSymbols();
			//	var symbol2 = symbols.Find(x => x.Ticker == e.Ticker);

			//             if (symbol1 != null && symbol2 != null)
			//             {
			//                 symbol1.Beta = symbol2.Beta;
			//             }
			//}


		}

		void livePortfolioChanged(object sender, PortfolioEventArgs e)
		{
			if (e.Type == PortfolioEventType.Symbol)
			{
				// Symbol registered — now safe to subscribe RT reference data
				bool pending = false;
				lock (_pendingRtTickers)
				{
					pending = _pendingRtTickers.Remove(e.Ticker);
				}
				if (pending)
				{
					_livePortfolio.RequestReferenceData(e.Ticker, new[] { "PRICE_LAST_RT" }, true);
				}
			}
			else if (e.Type == PortfolioEventType.ReferenceData)
			{
				foreach (KeyValuePair<string, object> kvp in e.ReferenceData)
				{
					if (kvp.Key == "PRICE_LAST_RT")
					{
						double px = double.NaN;
						if (kvp.Value is double d && d > 0) px = d;
						else if (double.TryParse(kvp.Value?.ToString(),
							System.Globalization.NumberStyles.Any,
							System.Globalization.CultureInfo.InvariantCulture, out double parsed) && parsed > 0) px = parsed;
						if (!double.IsNaN(px))
						{
							lock (_rtPrices) { _rtPrices[e.Ticker] = (px, DateTime.Now); }
							_update = 500;
						}
					}
				}
			}
		}

		DateTime _fundamentalResponseTime = DateTime.Now;

		/// <summary>
		/// Lightweight live balance refresh — computes MtM from current prices without
		/// rebuilding the full grid. Called from updateStatistics so the balance stays
		/// current whenever the chart streams new data.
		/// </summary>
		private void refreshLiveBalance()
		{
			var model = getModel();
			if (model == null || !model.IsLiveMode) return;
			if (_portfolioTimes == null || _portfolioTimes.Count == 0) return;

			// Bloomberg disconnected: do NOT compute live PnL from stale bar-cache closes.
			// Instead, read the last settled Friday's NAV directly from portfolioValues on disk
			// (written by IC's weekly run) and display that. No save file needed.
			if (!BarServer.ConnectedToBloomberg())
			{
				var disconnectTimes  = _portfolioTimes
					.Where(t => t != default(DateTime) && t.Date <= DateTime.Today)
					.OrderByDescending(t => t).ToList();
				if (disconnectTimes.Count == 0) return;
				var disconnectTime2 = disconnectTimes[0];
				var pTimes  = loadList<DateTime>(model.Name + " PortfolioTimes");
				var pValues = loadList<double>(model.Name + " PortfolioValues");
				if (pValues.Count == 0) return;
				var idx = pTimes.FindIndex(x => x.Date == disconnectTime2.Date);
				if (idx == -1) idx = pTimes.FindLastIndex(x => x.Date <= disconnectTime2.Date);
				if (idx < 0 || idx >= pValues.Count) return;
				var settledNav = model.InitialPortfolioBalance * (1 + pValues[idx] / 100);
				if (settledNav <= 0) return;
				_liveMtMBalance = settledNav;
				_mainView.SetInfo("LiveNav_" + model.Name, settledNav.ToString("R"));
				if (IsViewingLive(_showHoldingsTime))
				{
					Balance.Content  = "$ " + settledNav.ToString("#,##0");
					Balance2.Content = "$ " + settledNav.ToString("#,##0");
					Balance3.Content = settledNav.ToString("##,##0");
				}
				return;
			}

			// Always compute live MtM from the most recent SETTLED Friday —
			// ignore _showHoldingsTime so historical navigation doesn't corrupt LiveNav
			var settledTimes = _portfolioTimes
				.Where(t => t != default(DateTime) && t.Date <= DateTime.Today)
				.OrderByDescending(t => t).ToList();
			if (settledTimes.Count == 0) return;

			var time2 = settledTimes[0];  // most recent settled rebalance (e.g. Apr 17)
			var time1 = settledTimes.Count > 1 ? settledTimes[1] : default(DateTime);  // prior settled (e.g. Apr 10)

			// Base NAV = most recent settled value with valid (>0) portfolio value
			// Fall back to prior week if time2's value is zero (run failed or pre-market)
			var portfolioTimes2 = loadList<DateTime>(model.Name + " PortfolioTimes");
			var portfolioValues = loadList<double>(model.Name + " PortfolioValues");
			var baseBalance = 0.0;
			if (portfolioValues.Count > 0)
			{
				var baseIdx = portfolioTimes2.FindIndex(x => x.Date == time2.Date);
				if (baseIdx == -1) baseIdx = portfolioTimes2.FindLastIndex(x => x.Date <= time2.Date);
				if (baseIdx >= 0 && baseIdx < portfolioValues.Count)
					baseBalance = model.InitialPortfolioBalance * (1 + portfolioValues[baseIdx] / 100);
				// If time2 has no valid value (pre-market run failed), fall back to prior week
				if (baseBalance <= 0 && time1 != default(DateTime))
				{
					var priorIdx = portfolioTimes2.FindIndex(x => x.Date == time1.Date);
					if (priorIdx == -1) priorIdx = portfolioTimes2.FindLastIndex(x => x.Date <= time1.Date);
					if (priorIdx >= 0 && priorIdx < portfolioValues.Count)
						baseBalance = model.InitialPortfolioBalance * (1 + portfolioValues[priorIdx] / 100);
					time2 = time1;  // compute PnL relative to prior week
				}
			}
			if (baseBalance <= 0) return;

			// IMPORTANT: baseBalance comes from portfolioValues on disk (authoritative IC output).
			// Do NOT override with LiveClosingNav — that creates a feedback loop where a bad
			// save poisons the next cycle: bad save becomes baseBalance, which makes next
			// liveMtM also bad, which saves again, cascading downward. The only trusted source
			// for baseBalance is portfolioValues written by IdeaCalculator at rebalance.

			// If no prior date to compute PnL from, just show settled balance
			if (time1 == default(DateTime))
			{
				_liveMtMBalance = baseBalance;
				_mainView.SetInfo("LiveNav_" + (getModel()?.Name ?? ""), baseBalance.ToString("R"));
				// Only update labels if currently viewing the live date
				if (_showHoldingsTime == default(DateTime) || _showHoldingsTime.Date > time2.Date)
				{
					Balance.Content = "$ " + baseBalance.ToString("#,##0");
					Balance2.Content = "$ " + baseBalance.ToString("#,##0");
					Balance3.Content = baseBalance.ToString("##,##0");
				}
				return;
			}

			// On rebalance Friday: pivot to the prior week (time1) as the base so live PnL
			// is computed as: April 10 NAV + Σ(today's price − April 10 price) × shares.
			// Do NOT use portfolioValues[today] — that value is written by the model run and
			// may be stale if the run used old data (failed collection).  The bar cache has
			// actual market prices all day including after close, so live PnL always reflects
			// real trading, not a pre-baked model assumption.
			if (time2.Date == DateTime.Today)
			{
				time2 = time1;
				time1 = settledTimes.Count > 2 ? settledTimes[2] : default(DateTime);
				// Recompute baseBalance from the prior Friday (now time2 = April 10)
				baseBalance = 0.0;
				if (portfolioValues.Count > 0)
				{
					var priorBaseIdx = portfolioTimes2.FindIndex(x => x.Date == time2.Date);
					if (priorBaseIdx == -1) priorBaseIdx = portfolioTimes2.FindLastIndex(x => x.Date <= time2.Date);
					if (priorBaseIdx >= 0 && priorBaseIdx < portfolioValues.Count)
						baseBalance = model.InitialPortfolioBalance * (1 + portfolioValues[priorBaseIdx] / 100);
				}
				if (baseBalance <= 0) return;
				// Fall through to live PnL section below.
			}

			var tradesTime2 = time2;
			var probe = _portfolio1.GetTrades(model.Name, "", tradesTime2);
			if (probe.Count == 0 && time1 != default(DateTime)) tradesTime2 = time1;
			var portfolio = _portfolio1.GetTrades(model.Name, "", tradesTime2);
			if (portfolio.Count == 0) return;

			double livePnL = 0.0;
			foreach (var liveTrade in portfolio)
			{
				var lt = liveTrade.Ticker;
				double livePx = getCurrentPrice(lt);
				if (double.IsNaN(livePx) || livePx <= 0) continue;

				// Reference price = time2 close (most recent rebalance, e.g. Apr 3)
				var fridayKey = liveTrade.Closes.Keys
					.Where(k => k.Date == time2.Date)
					.OrderByDescending(k => k).FirstOrDefault();
				double fridayPx = fridayKey != default(DateTime) ? liveTrade.Closes[fridayKey] : double.NaN;
				if (double.IsNaN(fridayPx))
				{
					var fridayBars = _barCache.GetBars(lt, _ic?.GetLowestInterval() ?? "Weekly", 0, 500);
					var fridayBar = fridayBars?.LastOrDefault(b => b.Time.Date == time2.Date);
					fridayPx = (fridayBar != null && fridayBar.Close > 0) ? fridayBar.Close : double.NaN;
				}
				if (double.IsNaN(fridayPx) || fridayPx <= 0) continue;

				var sharesKey = liveTrade.Shares.Keys
					.Where(k => k.Date == tradesTime2.Date && liveTrade.Shares[k] != 0)
					.OrderByDescending(k => k).FirstOrDefault();
				if (sharesKey == default(DateTime))
					sharesKey = liveTrade.Shares.Keys
						.Where(k => k.Date <= tradesTime2.Date && liveTrade.Shares[k] != 0)
						.OrderByDescending(k => k).FirstOrDefault();
				double sharesVal = sharesKey != default(DateTime) ? liveTrade.Shares[sharesKey] : 0;
				if (sharesVal == 0) continue;

				int ldir = Math.Sign((int)liveTrade.Direction);
				livePnL += ldir * sharesVal * (livePx - fridayPx);
			}

			double liveMtM = baseBalance + livePnL;
			if (liveMtM > 0)
			{
				_liveMtMBalance = liveMtM;
				_mainView.SetInfo("LiveNav_" + (getModel()?.Name ?? ""), liveMtM.ToString("R"));
				// LiveClosingNav save removed — caused feedback loop where a bad save
				// becomes next cycle's baseBalance, cascading the portfolio toward zero.
				// baseBalance now always comes from portfolioValues (IC authoritative).
				// Only push to Balance labels when viewing today (not a historical date)
				if (IsViewingLive(_showHoldingsTime))
				{
					Balance.Content = "$ " + liveMtM.ToString("#,##0");
					Balance2.Content = "$ " + liveMtM.ToString("#,##0");
					Balance3.Content = liveMtM.ToString("##,##0");
				}
			}
		}

		List<string> _refSyms = new List<string>();

		void setModelSymbols(string portfolioName)
		{
			var symbols = _portfolio1.GetSymbols().Distinct().ToList();
			var model = getModel();
			if (_forAlphaSymbols) model.Groups[_g].AlphaSymbols = symbols;
			else model.Groups[_g].HedgeSymbols = symbols;
			//saveList<Symbol>(model.Name + " Symbols", model.Symbols);
			Dispatcher.BeginInvoke(new Action(() => { userFactorModel_to_ui(_selectedUserFactorModel); }));
		}

		int _indexBarRequestCount = 0;

		private void requestIndexBars()
		{
			Model model = getModel();
			if (model != null)
			{

				string interval1 = getInterval(model);
				string interval2 = Study.getForecastInterval(interval1, 1);
				string interval3 = "Daily";

				var indexIntervals = (new string[] { interval1, interval2, interval3 }).ToList().Distinct().ToList();

				_indexBarRequestCount = _indexSymbols.Count * indexIntervals.Count;

				if (_indexBarRequestCount > 0)
				{
					foreach (var interval in indexIntervals)
					{
						if (interval.Length > 0)
						{
							foreach (string symbol in _indexSymbols)
							{
								_barCache.RequestBars(symbol, interval, true);
							}
						}
					}
				}
				else
				{
					if (model != null)
					{
						requestSymbolBars(model);
					}
				}
			}
		}

		void initializeCalculator(Model model)
		{
			loadModel();

			if (_ic != null)
			{
				_ic.ProgressEvent -= progressEvent;
			}
			_ic = IdeaCalculator.GetIdeaCalculator(model);
			if (_ic != null)
			{
				_interval = _ic.GetLowestInterval();
				_ic.ProgressEvent += progressEvent;
				_runModel = !(_ic.ProgressState == ProgressState.Finished || _ic.ProgressState == ProgressState.Complete);
				if (_runModel)
				{
					TrainResults.Visibility = Visibility.Collapsed;
					ViewHelp.Visibility = Visibility.Collapsed;

					_tradeOverviewGroup = "Open";

					ProgressCalculations2.Visibility = Visibility.Collapsed;
					//UserFactorModelGrid.Visibility = Visibility.Visible;

					_progressState = _ic.ProgressState;
					_progressTotalNumber = _ic.ProgressTotalNumber;
					_progressCompletedNumber = _ic.ProgressCompletedNumber;
					_progressTime = _ic.ProgressTime;
					var modelName = _ic.ModelName;

					//updateProgress(modelName);

					update();
				}
				else
				{
					ProgressCalculations2.Visibility = Visibility.Collapsed;
					//UserFactorModelGrid.Visibility = Visibility.Visible;
					//TrainResults.Visibility = Visibility.Visible;
				}
			}
		}

		void runSelectedModel()
		{
			// testFundamentalHistoryData();
			// return;

			_runModel = true;
			_stopModel = false;

			_totalBarRequest = 0;

			_portfolioValues.Clear();
			_portfolioReturns.Clear();
			_portfolioNavs.Clear();
			_portfolioHedgeValues.Clear();
			//            _strategyPortfolioValues.Clear();
			_portfolioTimes.Clear();
			_benchmarkValues.Clear();
			_compareToValues.Clear();
			//_compareToMonthValues.Clear();
			_portfolioMonthTimes.Clear();
			_portfolioMonthValues.Clear();
			_portfolioHedgeMonthValues.Clear();
			_averageTurnover = 0;
			clearStats();

			_scoreCache.Clear();

			_update = 11;

			_factorInputs.Clear();
			_calculating = true;
			_barCache.Clear();

			// run selected Model
			Model model = getModel();
			AuditService.LogRebalanceRequest(model?.Name ?? "Unknown");
			if (model != null)
			{
				// Capture live mode state at run time
				model.IsLiveMode = LiveTrade.IsChecked == true;
				if (model.IsLiveMode && UserFactorModelTrainStartDate.SelectedDate.HasValue)
				{
					model.LiveStartDate = UserFactorModelTrainStartDate.SelectedDate.Value;
					model.DataRange.Time1 = model.LiveStartDate.AddDays(-90);

					// One-time archive of backtest artifacts on first live run
					var dataFolder = MainView.GetDataFolder();
					var inceptionMarker = dataFolder + @"\portfolios\live_inception\" + model.Name;

					if (!File.Exists(inceptionMarker))
					{
						Directory.CreateDirectory(dataFolder + @"\portfolios\live_inception");

						// Archive trades file
						var tradesPath = dataFolder + @"\portfolios\trades\" + model.Name;
						if (File.Exists(tradesPath))
						{
							File.Move(tradesPath, dataFolder + @"\portfolios\trades\_archive_" + model.Name + "_" + DateTime.Now.ToString("yyyyMMdd"));
							//Debug.WriteLine($"[LIVEMODE] Archived trades file");
						}

						// Archive decisionlock folder
						var decisionLockPath = dataFolder + @"\decisionlock\" + model.Name;
						if (Directory.Exists(decisionLockPath))
						{
							Directory.Move(decisionLockPath, dataFolder + @"\decisionlock\_archive_" + model.Name + "_" + DateTime.Now.ToString("yyyyMMdd"));
							//Debug.WriteLine($"[LIVEMODE] Archived decisionlock");
						}

						// Archive tradelock
						var tradeLockPath = dataFolder + @"\tradelock\" + model.Name;
						if (File.Exists(tradeLockPath))
						{
							File.Move(tradeLockPath, dataFolder + @"\tradelock\_archive_" + model.Name + "_" + DateTime.Now.ToString("yyyyMMdd"));
							//Debug.WriteLine($"[LIVEMODE] Archived tradelock");
						}

						// Write inception marker
						File.WriteAllText(inceptionMarker, model.LiveStartDate.ToString("yyyy-MM-dd"));
						//Debug.WriteLine($"[LIVEMODE] Live inception marker written for {model.Name}");
					}
				}
				else
				{
					// Backtest mode — restore DataRange.Time1 from DatePicker
					model.IsLiveMode = false;
					if (UserFactorModelTrainStartDate.SelectedDate.HasValue)
						model.DataRange.Time1 = UserFactorModelTrainStartDate.SelectedDate.Value;
				}

				// Save model so IsLiveMode and LiveStartDate persist
				saveUserFactorModels(model.Name);

				//Debug.WriteLine($"[RUNMODE] IsLiveMode={model.IsLiveMode} LiveStartDate={model.LiveStartDate:yyyy-MM-dd} DataRange.Time1={model.DataRange.Time1:yyyy-MM-dd}");
				model.SetTimeRanges();  // ← existing code continues from here unchanged
				string path = MainView.GetDataFolder() + @"\portfolios\trades" + @"\" + model.Name;
				try
				{
					File.Delete(path);
				}
				catch (Exception)
				{
				}
				if (_useUserFactorModel)
				{
					updateUserFactorModelList();
				}
				else
				{
					//addMLFactors(model);
					//updateMLFactorModelList();
				}
				_scanSummary = null;
				if (model != null && _runModel)
				{
					deleteModelData(model.Name);
					_ic = IdeaCalculator.GetIdeaCalculator(model);
					_ic.Run();
				}
			}
		}

		private List<ScanSummary> _scanSummary = null;

		int _totalBarRequest = 0;
		List<string> _barRequests = new List<string>();

		void requestSymbolBars(Model model)
		{
			_portfolioNavs.Clear();
			_portfolioValues.Clear();
			_portfolioReturns.Clear();
			_portfolioHedgeValues.Clear();
			_portfolioTimes.Clear();
			_benchmarkValues.Clear();
			_compareToValues.Clear();
			//_compareToMonthValues.Clear();
			_portfolioMonthTimes.Clear();
			_portfolioHedgeMonthValues.Clear();
			_portfolioMonthValues.Clear();
			_update = 500;

			_progressState = ProgressState.CollectingData;
			_progressTotalNumber = 0;
			_progressCompletedNumber = 0;

			var modelName = _ic.ModelName;
			try
			{

				//if (Application.Current != null) Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, (Action)delegate () { updateProgress(modelName); });
			}
			catch (Exception x)
			{
				System.Diagnostics.Debug.WriteLine(x);
			}

			lock (_allTrades)
			{
				_allTrades.Clear();
			}

			var intervals = new List<string>();
			if (model.IsLiveMode)
			{
				intervals.Add("Daily");
			}
			else
			{
				intervals.Add(getInterval(model));
				intervals.Add(Study.getForecastInterval(intervals[0], 1));
				intervals.Add(Study.getForecastInterval(intervals[0], -1));
				if (!intervals.Contains("Daily")) intervals.Add("Daily");
			}

			lock (_barRequests)
			{
				_barRequests.Clear();

				string benchMarkSymbol = model.Benchmark;
				foreach (var interval in intervals)
				{
					_barRequests.Add(benchMarkSymbol + ":" + interval);
					_barCache.RequestBars(benchMarkSymbol, interval);

					_barRequests.Add(_compareToSymbol + ":" + interval);
					_barCache.RequestBars(_compareToSymbol, interval);
				}

				var trades = _portfolio1.GetTrades(model.Name);
				var tickers = trades.Select(x => x.Ticker);
				tickers.Concat(model.Symbols.Select(x => x.Ticker));
				tickers = tickers.Distinct();

				foreach (var ticker in tickers)
				{
					foreach (var interval in intervals)
					{
						_barRequests.Add(ticker + ":" + interval);
						// Subscribe live for Daily bars in live mode so getCurrentPrice() stays current
						bool liveSubscribe = model.IsLiveMode && interval == "Daily";
						_barCache.RequestBars(ticker, interval, liveSubscribe);
					}
					// Weekly live subscription for intraday prices — the current week's
					// incomplete bar updates its Close with the latest trade throughout the day
					if (model.IsLiveMode)
					{
						_barRequests.Add(ticker + ":Weekly");
						_barCache.RequestBars(ticker, "Weekly", true);
						// RequestSymbols establishes Bloomberg session; once Symbol event fires,
						// livePortfolioChanged calls RequestReferenceData for PRICE_LAST_RT
						lock (_pendingRtTickers) { _pendingRtTickers.Add(ticker); }
						_livePortfolio.RequestSymbols(ticker, Portfolio.PortfolioType.Single, false);
					}
				}
				_totalBarRequest = _barRequests.Count;
			}
		}

		private void setInfo()
		{
			string view = (_view == "Sector Positions") ? "Sector Breakdown" : _view;

			string info =
				 view + ";" +
				_symbol + ";" +
				_tradeOverviewGroup + ";" +
				_monthlySummaryYear.ToString() + ";" +
				_monthlySummaryMonth.ToString() + ";" +
				_selectedSector + ";" +
				_selectedUserFactorModel + ";" +
				_selectedMLFactorModel + ";" +
				_useUserFactorModel + ";" +
				_modelName + ";";

			_mainView.SetInfo("FundamentalML", info);

			//setTradeMangementSettings();
		}

		private bool _settingRadioButtons = false;
		private bool _suppressTISetup = false; // when true, any attempt to show TISetup is blocked
		private void setModelRadioButtons()
		{
			_settingRadioButtons = true;
			FactorModelSetup.IsChecked = _useUserFactorModel;
			FactorModelList.IsChecked = _useUserFactorModel;
			RebalanceFactorModelList.IsChecked = _useUserFactorModel;
			_settingRadioButtons = false;
		}

		void Timer_tick(object sender, EventArgs e)
		{
			if (_run)
			{
				_run = false;
				run();
			}

			if (_update != 0) // && _progressState == ProgressState.Complete)
			{
				update();
				_update = 0;
			}

			// Periodic live balance refresh every 10 seconds during market hours
			if ((DateTime.Now - _lastGridRefreshTime).TotalSeconds >= 10)
			{
				_lastGridRefreshTime = DateTime.Now;
				var m = getModel();
				if (m != null && m.IsLiveMode && BarServer.ConnectedToBloomberg())
				{
					var liveTrades = _portfolio1.GetTrades(m.Name);
					foreach (var t in liveTrades.Select(x => x.Ticker).Distinct())
					{
						_barCache.RequestBars(t, "Daily", true);
						_barCache.RequestBars(t, "Weekly", true);
					}
				}
				// Determine which cursor time is active based on which grid is visible
				var activeCursor = (PerformanceGrid.Visibility == Visibility.Visible)
					? _lastPerfCursorTime
					: _showHoldingsTime;
				// Only redraw grid/balance when viewing live date -- don't disturb historical cursor.
				// IsViewingLive() handles intra-week: last Friday cursor IS the live view.
				bool viewingLive = IsViewingLive(activeCursor);
				if (viewingLive)
				{
					// Only recalculate live prices when Bloomberg is connected.
					// Without this guard, the timer uses stale bar-cache closes from a
					// previous session as "current" prices, producing wrong PnL and balance.
					if (BarServer.ConnectedToBloomberg())
					{
						drawPortfolioGrid();
						refreshLiveBalance();
					}
					drawReturnChart();
				}
				// When on historical date: skip all live updates entirely
			}

			if (_addFlash)
			{
				_addFlash = false;
				Brush alertColor = Brushes.Red;
				//Flash.Manager.AddFlash(new Flash(AlertButton, AlertButton.Foreground, alertColor, 10, false));
			}

			//// progress bar
			if (_ic != null)
			{
				var modelName = _ic.ModelName;
				//updateProgress(modelName);
			}
		}

		private void update()
		{
			try
			{
				updateNavigation();
				getPositions();
				updateMonthSummary();
				updateModelData();
				Model model = getModel();
				var allocations = getAllocations(model);
				updateAllocations(SectorAllocation, getSectorPercentages(allocations));
				updateAllocations(IndustryAllocation, getIndustryPercentages(allocations));
				updateAllocation(GeoAllocation, getCountryPercentages(allocations));
				updateAllocation(AssetAllocation, getSecurityTypePercentages(allocations));
				updateHoldingAnalysis();
				drawPortfolioGrid();
			}
			catch (Exception ex)
			{
				string msg = ex.Message;
			}
		}

		private void updateHoldingAnalysis()
		{
			Model model = getModel();

			if (model != null)
			{

				AvgPE.Content = "";
				AvgPB.Content = "";
				AvgPS.Content = "";
				AvgPC.Content = "";

				double sumPS = 0;
				double sumPB = 0;
				double sumPE = 0;
				double sumPC = 0;
				int cntPS = 0;
				int cntPB = 0;
				int cntPE = 0;
				int cntPC = 0;
				foreach (var symbol in model.Symbols)
				{
					if (symbol.PriceToSalesRatio != null)
					{
						double ps;
						if (double.TryParse(symbol.PriceToSalesRatio, out ps))
						{
							sumPS += ps;
							cntPS++;
						}
					}
					if (symbol.PriceToEarningsRatio != null)
					{
						double pe;
						if (double.TryParse(symbol.PriceToEarningsRatio, out pe))
						{
							sumPE += pe;
							cntPE++;
						}
					}
					if (symbol.PriceToBookRatio != null)
					{
						double pb;
						if (double.TryParse(symbol.PriceToBookRatio, out pb))
						{
							sumPB += pb;
							cntPB++;
						}
					}
					if (symbol.PriceToCashFlow != null)
					{
						double pc;
						if (double.TryParse(symbol.PriceToCashFlow, out pc))
						{
							sumPC += pc;
							cntPC++;
						}
					}
				}

				if (cntPE > 0)
				{
					double avgPE = sumPE / cntPE;
					AvgPE.Content = avgPE.ToString("0.00");
				}
				if (cntPB > 0)
				{
					double avgPB = sumPB / cntPB;
					AvgPB.Content = avgPB.ToString("0.00");
				}
				if (cntPS > 0)
				{
					double avgPS = sumPS / cntPS;
					AvgPS.Content = avgPS.ToString("0.00");
				}
				if (cntPC > 0)
				{
					double avgPC = sumPC / cntPC;
					AvgPC.Content = avgPC.ToString("0.00");
				}

				double topPC = getTopPercent(10);
				Top10.Content = topPC.ToString("0.00");
			}
		}

		private void updateAllocation(Grid grid, Dictionary<string, double> input)
		{
			var percentages = input.ToList();
			percentages.Sort((pair1, pair2) => -pair1.Value.CompareTo(pair2.Value));
			grid.RowDefinitions.Clear();
			grid.Children.Clear();
			int row = 0;
			foreach (var percentage in percentages)
			{
				var rowDef = new RowDefinition();
				rowDef.Height = new GridLength(25);

				grid.RowDefinitions.Add(rowDef);
				string name = percentage.Key;
				double value = percentage.Value;

				//Brush brush = new SolidColorBrush(Color.FromRgb(0x77, 0x77, 0x77));
				//Brush brush2 = new SolidColorBrush(Color.FromRgb(0xff, 0x8c, 0x00));

				Brush brush = new SolidColorBrush(Color.FromRgb(0xff, 0x8c, 0x00));
				Brush brush2 = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));

				// sector, industry, sub-industry name
				var label1 = new Label();
				label1.SetValue(Grid.ColumnProperty, 0);
				label1.SetValue(Grid.RowProperty, row);
				label1.Content = name;
				label1.Foreground = brush;
				label1.FontSize = 10;
				label1.HorizontalAlignment = HorizontalAlignment.Left;
				label1.VerticalAlignment = VerticalAlignment.Top;
				label1.FontFamily = new FontFamily("Helvetica Neue");
				label1.FontWeight = FontWeights.Normal;
				grid.Children.Add(label1);


				// total allocation
				var label2 = new Label();
				label2.SetValue(Grid.ColumnProperty, 1);
				label2.SetValue(Grid.RowProperty, row);
				label2.Content = value.ToString("P");
				label2.Foreground = brush2;
				label2.FontSize = 10;
				label2.HorizontalAlignment = HorizontalAlignment.Right;
				label2.VerticalAlignment = VerticalAlignment.Top;
				label2.FontFamily = new FontFamily("Helvetica Neue");
				label2.FontWeight = FontWeights.Normal;
				grid.Children.Add(label2);

				row++;
			}
		}


		private void updateAllocations(Grid grid, Dictionary<string, Allocations> input)
		{
			var percentages = input.ToList();
			percentages.Sort((pair1, pair2) => -pair1.Value.TotalAllocation.CompareTo(pair2.Value.TotalAllocation));
			grid.RowDefinitions.Clear();
			grid.Children.Clear();
			int row = 0;
			foreach (var percentage in percentages)
			{
				var rowDef = new RowDefinition();
				rowDef.Height = new GridLength(25);

				grid.RowDefinitions.Add(rowDef);
				string name = percentage.Key;

				double totalAmount = percentage.Value.TotalAllocation;
				double longAmount = percentage.Value.LongAllocation;
				double shortAmount = percentage.Value.ShortAllocation;
				double netAmount = percentage.Value.LongAllocation - percentage.Value.ShortAllocation;

				Brush lBrush = new SolidColorBrush(Color.FromRgb(0x00, 0xcc, 0x00));
				Brush sBrush = new SolidColorBrush(Color.FromRgb(0xfb, 0x30, 0x00));
				Brush lBrush2 = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));

				// sector, industry, sub-industry name
				var label1 = new Label();
				label1.SetValue(Grid.ColumnProperty, 0);
				label1.SetValue(Grid.RowProperty, row);
				label1.Content = name;
				//label1.Foreground = (netAmount >= 0) ? lBrush : sBrush;
				label1.Foreground = (netAmount >= 0) ? Brushes.PaleGreen : Brushes.Crimson;
				label1.FontSize = 10;
				label1.HorizontalAlignment = HorizontalAlignment.Left;
				label1.VerticalAlignment = VerticalAlignment.Top;
				label1.FontFamily = new FontFamily("Helvetica Neue");
				label1.FontWeight = FontWeights.Normal;
				grid.Children.Add(label1);

				// total allocation
				var label2 = new Label();
				label2.SetValue(Grid.ColumnProperty, 1);
				label2.SetValue(Grid.RowProperty, row);
				label2.Content = netAmount.ToString("P");
				label2.Foreground = lBrush2;
				//label2.Foreground = (netAmount >= 0) ? Brushes.Lime : Brushes.Red;
				label2.FontSize = 10;
				label2.HorizontalAlignment = HorizontalAlignment.Right;
				label2.VerticalAlignment = VerticalAlignment.Top;
				label2.FontFamily = new FontFamily("Helvetica Neue");
				label2.FontWeight = FontWeights.Normal;
				grid.Children.Add(label2);

				// long allocation
				var label3 = new Label();
				label3.SetValue(Grid.ColumnProperty, 2);
				label3.SetValue(Grid.RowProperty, row);
				label3.Content = longAmount.ToString("P");
				label3.Foreground = lBrush2;
				label3.FontSize = 10;
				label3.HorizontalAlignment = HorizontalAlignment.Right;
				label3.VerticalAlignment = VerticalAlignment.Top;
				label3.FontFamily = new FontFamily("Helvetica Neue");
				label3.FontWeight = FontWeights.Normal;
				grid.Children.Add(label3);

				// short allocation
				var label4 = new Label();
				label4.SetValue(Grid.ColumnProperty, 3);
				label4.SetValue(Grid.RowProperty, row);
				label4.Content = shortAmount.ToString("P");
				label4.Foreground = lBrush2;
				label4.FontSize = 10;
				label4.HorizontalAlignment = HorizontalAlignment.Right;
				label4.VerticalAlignment = VerticalAlignment.Top;
				label4.FontFamily = new FontFamily("Helvetica Neue");
				label4.FontWeight = FontWeights.Normal;
				grid.Children.Add(label4);

				row++;
			}
		}

		private void updateModelData()
		{
			Model model = getModel();
			if (model != null)
			{
				updateCurrentPortfolio();

				drawReturnChart();

				List<double> graphData = _portfolioMonthValues; // Cumulative Return

				//drawChart(_returnGraph, FundamentalChartCurveType.Line, _portfolioTimes, null, _portfolioValues, null, null);
				var useHedgeCurve = (isTradingModel(model.Name) && _useHedgeCurve);

				var portfolioTimes = new List<DateTime>(_portfolioMonthTimes);

				List<double> secondPortfolioValues = null;
				//if (_selectedUserFactorModel2 != _selectedUserFactorModel)
				//{
				//    secondPortfolioValues = loadList<double>(_selectedUserFactorModel2 + " PortfolioMonthValues");
				//    var portfolioTimes2 = loadList<DateTime>(_selectedUserFactorModel2 + " PortfolioMonthTimes");
				//    portfolioTimes.AddRange(portfolioTimes2);
				//    portfolioTimes = portfolioTimes.Distinct().OrderBy(x => x).ToList();

				//    Series series1 = new Series(_portfolioMonthValues);
				//    Series series2 = new Series(secondPortfolioValues);
				//    series1 = sync(series1, "D", "D", _portfolioMonthTimes, portfolioTimes, false);
				//    series2 = sync(series2, "D", "D", portfolioTimes2, portfolioTimes, false);

				//    graphData = series1.Data;
				//    secondPortfolioValues = series2.Data;
				//}
				drawChart(_monthlyReturnGraph, FundamentalChartCurveType.Histogram, portfolioTimes, null, useHedgeCurve ? _portfolioHedgeMonthValues : graphData, null, null, secondPortfolioValues, _portfolioRegimes);
				// Clear the histogram's internal cursor so no vertical cursor line is drawn.
				try { _monthlyReturnGraph.CursorTime = default(DateTime); } catch { }

				//drawPortfolioGrid();
				updateStatistics();
			}
		}

		private void drawReturnChart()
		{
			List<double> graphData = _portfolioValues; // Cumulative Return
			if (_graphDataType == "ALPHA") graphData = getAlpha();
			else if (_graphDataType == "BETA") graphData = getBeta();
			else if (_graphDataType == "SHARPE RATIO") graphData = getSharpe();
			else if (_graphDataType == "CORRELATION") graphData = getCorrelation();
			else if (_graphDataType == "SORTINO RATIO") graphData = getSortino();
			else if (_graphDataType == "STD DEVIATION") graphData = getStdDeviation();
			else if (_graphDataType == "INFORMATION RATIO") graphData = getInfoRatio();
			else if (_graphDataType == "VOLATILITY 30 D") graphData = getVol30d();

			List<double> portfolioHedgeValues = null;
			var model = getModel();
			var showHedgeCurve = (isTradingModel(model.Name) && _useHedgeCurve);
			if (showHedgeCurve)
			{
				graphData = null;
				portfolioHedgeValues = _portfolioHedgeValues;
			}

			var portfolioTimes = new List<DateTime>(_portfolioTimes);

			// For live portfolios intra-week: append today's live MtM as a new point
			// Only append if:
			//   1. Today is strictly after the last SETTLED date (no future projected dates in the way)
			//   2. No date >= today already exists in portfolioTimes (avoids out-of-order append)
			var liveChartModel = getModel();
			var lastSettledDate = portfolioTimes.Where(t => t.Date <= DateTime.Today).OrderByDescending(t => t).FirstOrDefault();
			var hasFutureDate = portfolioTimes.Any(t => t.Date > DateTime.Today);
			var alreadyHasToday = portfolioTimes.Any(t => t.Date == DateTime.Today);
			if (liveChartModel != null && liveChartModel.IsLiveMode
				&& BarServer.ConnectedToBloomberg()   // no today-point when disconnected
				&& _liveMtMBalance > 0 && liveChartModel.InitialPortfolioBalance > 0
				&& graphData != null && graphData.Count > 0
				&& portfolioTimes.Count > 0
				&& !hasFutureDate       // no projected rebalance date extends beyond today
				&& !alreadyHasToday     // today not already a settled rebalance date
				&& lastSettledDate != default(DateTime)
				&& lastSettledDate.Date < DateTime.Today
				&& (DateTime.Today - lastSettledDate.Date).TotalDays <= 7)
			{
				var liveReturn = (_liveMtMBalance / liveChartModel.InitialPortfolioBalance - 1.0) * 100.0;
				graphData = new List<double>(graphData);
				graphData.Add(liveReturn);
				portfolioTimes = new List<DateTime>(portfolioTimes);
				portfolioTimes.Add(DateTime.Today);
			}

			List<double> secondPortfolioValues = null;
			//if (_selectedUserFactorModel2 != _selectedUserFactorModel)
			//{
			//    secondPortfolioValues = loadList<double>(_selectedUserFactorModel2 + " PortfolioValues");
			//    var portfolioTimes2 = loadList<DateTime>(_selectedUserFactorModel2 + " PortfolioTimes");
			//    portfolioTimes.AddRange(portfolioTimes2);
			//    portfolioTimes = portfolioTimes.Distinct().OrderBy(x => x).ToList();

			//    Series series1 = new Series(_portfolioValues);
			//    Series series2 = new Series(secondPortfolioValues);
			//    series1 = sync(series1, "D", "D", _portfolioTimes, portfolioTimes, false);
			//    series2 = sync(series2, "D", "D", portfolioTimes2, portfolioTimes, false);

			//    graphData = series1.Data;
			//    secondPortfolioValues = series2.Data;
			//}

			List<double> compareToValues = getCompareToValues();
			drawChart(_mainReturnGraph, FundamentalChartCurveType.Line, portfolioTimes, (_graphDataType == "CUMULATIVE RETURN") ? _benchmarkValues : null, graphData, compareToValues, portfolioHedgeValues, secondPortfolioValues, _portfolioRegimes);
		}

		private List<double> getCompareToValues()
		{
			var output = new List<double>();
			var bars = _barCache.GetBars(_compareToSymbol, _ic != null ? _ic.GetLowestInterval() : "Daily", 0);

			var startDate = _portfolioTimes.FirstOrDefault();
			var endDate = _portfolioTimes.LastOrDefault();
			var startPrice = double.NaN;
			foreach (var bar in bars)
			{
				if (bar.Time >= startDate && bar.Time <= endDate)
				{
					if (double.IsNaN(startPrice))
					{
						startPrice = bar.Close;
					}
					var value = double.IsNaN(startPrice) ? Double.NaN : getReturn(_compareToSymbol, 1, startPrice, bar.Close); // 100 * (bar.Close - startPrice) / startPrice;
					output.Add(value);
				}
			}
			return output;
		}

		private List<double> getAlpha()
		{
			var output = new List<double>();
			for (int ii = 0; ii < _portfolioTimes.Count; ii++)
			{
				var alpha = _portfolioValues[ii] - _benchmarkValues[ii];
				output.Add(alpha);
			}
			return output;
		}

		private List<double> getBeta()
		{
			var output = new List<double>();

			List<double> portfolioValues = new List<double>();
			List<double> benchmarkValues = new List<double>();
			for (int ii = 0; ii < _portfolioTimes.Count; ii++)
			{
				portfolioValues.Add(_portfolioValues[ii]);
				benchmarkValues.Add(_benchmarkValues[ii]);
				var correlation = atm.getCorrelation(benchmarkValues, portfolioValues);
				var stdPortfolio = getStandardDeviation(portfolioValues);
				var stdBenchmark = getStandardDeviation(benchmarkValues);
				var beta = correlation * (stdPortfolio / stdBenchmark);
				output.Add(beta);
			}
			return output;
		}


		private List<double> getAnnVol()
		{
			var output = new List<double>();

			List<double> portfolioReturns = new List<double>();

			var annFactor = getAnnFactor(_portfolioTimes);

			for (int ii = 0; ii < _portfolioReturns.Count; ii++)
			{
				portfolioReturns.Add(_portfolioReturns[ii]);
				var stdReturns = getStandardDeviation(portfolioReturns);
				var AnnVol = 100 * Math.Sqrt(annFactor) * stdReturns;
				output.Add(AnnVol);
			}
			return output;
		}

		private List<double> getAnnRet()
		{
			var output = new List<double>();

			List<double> portfolioReturns = new List<double>();

			var annFactor = getAnnFactor(_portfolioTimes);

			for (int ii = 0; ii < _portfolioTimes.Count; ii++)
			{
				portfolioReturns.Add(_portfolioReturns[ii]);
				var meanReturns = getMean(portfolioReturns);
				var AnnRet = annFactor * meanReturns;
				output.Add(AnnRet);
			}
			return output;
		}

		private List<double> getAvgDVol()
		{
			var output = new List<double>();

			List<double> portfolioReturns = new List<double>();

			for (int ii = 0; ii < _portfolioTimes.Count; ii++)
			{
				portfolioReturns.Add(_portfolioReturns[ii]);
				var stdReturns = getStandardDeviation(portfolioReturns);
				var AvgDVol = stdReturns;
				output.Add(AvgDVol);
			}
			return output;
		}

		private List<double> getAvgDRet()
		{
			var output = new List<double>();

			List<double> portfolioReturns = new List<double>();

			for (int ii = 0; ii < _portfolioTimes.Count; ii++)
			{
				portfolioReturns.Add(_portfolioReturns[ii]);
				var meanReturns = getMean(portfolioReturns);
				var AvgDRet = meanReturns;
				output.Add(AvgDRet);
			}
			return output;
		}

		private List<double> getSharpe()
		{
			var output = new List<double>();

			List<double> portfolioReturns = new List<double>();

			var annFactor = getAnnFactor(_portfolioTimes);

			for (int ii = 0; ii < _portfolioTimes.Count; ii++)
			{
				portfolioReturns.Add(_portfolioReturns[ii]);
				var meanReturns = getMean(portfolioReturns);
				//var riskFreeRate = 0.0475 / 365;
				var stdReturns = getStandardDeviation(portfolioReturns);
				var sharpe = (!double.IsNaN(meanReturns) && !double.IsNaN(stdReturns) && stdReturns != 0) ? ((annFactor * meanReturns) / (Math.Sqrt(annFactor) * stdReturns)) : double.NaN;
				output.Add(sharpe);
			}
			return output;
		}

		private List<double> getSortino()
		{
			var output = new List<double>();

			List<double> portfolioReturns = new List<double>();

			var annFactor = getAnnFactor(_portfolioTimes);

			for (int ii = 0; ii < _portfolioTimes.Count; ii++)
			{
				portfolioReturns.Add(_portfolioValues[ii]);
				var meanReturns = getMean(portfolioReturns);
				//var riskFreeRate = 0.01;
				var negativeReturns = getNegatives(portfolioReturns);
				var stdNegativeReturns = getStandardDeviation(negativeReturns);
				var sortino = (stdNegativeReturns == 0) ? double.NaN : ((annFactor * meanReturns) / (Math.Sqrt(annFactor) * stdNegativeReturns));
				output.Add(sortino);
			}
			return output;
		}

		private List<double> getCorrelation()
		{
			var output = new List<double>();

			List<double> portfolioValues = new List<double>();
			List<double> benchmarkValues = new List<double>();

			for (int ii = 0; ii < _portfolioTimes.Count; ii++)
			{
				portfolioValues.Add(_portfolioValues[ii]);
				benchmarkValues.Add(_benchmarkValues[ii]);
				var correlation = atm.getCorrelation(benchmarkValues, portfolioValues);
				output.Add(correlation);
			}
			return output;
		}

		private List<double> getStdDeviation()
		{
			var output = new List<double>();

			List<double> portfolioValues = new List<double>();

			for (int ii = 0; ii < _portfolioTimes.Count; ii++)
			{
				portfolioValues.Add(_portfolioValues[ii]);
				var returns = getReturns(portfolioValues);
				var stdReturns = getStandardDeviation(returns);
				output.Add(stdReturns);
			}
			return output;
		}

		private List<double> getInfoRatio()
		{
			var output = new List<double>();

			List<double> portfolioReturns = new List<double>();
			List<double> sharpe = new List<double>();

			var annFactor = getAnnFactor(_portfolioTimes);

			for (int ii = 0; ii < _portfolioTimes.Count; ii++)
			{
				portfolioReturns.Add(_portfolioReturns[ii]);
				var meanReturns = getMean(portfolioReturns);
				//var riskFreeRate = 0.01;
				var stdReturns = getStandardDeviation(portfolioReturns);
				var sharpeValue = ((annFactor * meanReturns) / (Math.Sqrt(annFactor) * stdReturns));
				sharpe.Add(sharpeValue);
				var infoRatio = (_portfolioValues[ii] - _benchmarkValues[ii]) / getStandardDeviation(sharpe);
				output.Add(infoRatio);
			}
			return output;
		}

		private List<double> getVol30d()
		{
			var output = new List<double>();

			List<double> portfolioValues = new List<double>();

			for (int ii = 0; ii < _portfolioTimes.Count; ii++)
			{
				portfolioValues.Add(_portfolioValues[ii]);
				var annFactor = getAnnFactor(_portfolioTimes);
				var volatility = (ii < 30) ? double.NaN : 100 * atm.GetVolatility(30, ii, new Series(portfolioValues), annFactor);
				output.Add(volatility);
			}
			return output;
		}

		private class Statistics
		{
			public double Return { get; set; }
			public double Alpha { get; set; }
			public double Beta { get; set; }
			public double Sharpe { get; set; }
			public double AvgDRet { get; set; }
			public double AvgDVol { get; set; }
			public double AnnRet { get; set; }
			public double AnnVol { get; set; }
			public double Sortino { get; set; }
			public double StdDev { get; set; }
			public double Correlation { get; set; }
			public double InfoRatio { get; set; }
			public double VaR { get; set; }
			public double MaxDrawDown { get; set; }
			public double MaxDrawDownDuration { get; set; }
			public double DailyMaxDrawDown { get; set; }
			public double WeeklyMaxDrawDown { get; set; }
			public double MonthlyMaxDrawDown { get; set; }
			public double YearlyMaxDrawDown { get; set; }
			public double MonthlyReturn { get; set; }
			public double Return90 { get; set; }
			public double Return180 { get; set; }
			public double ReturnYTD { get; set; }
			public double AnnualReturn { get; set; }
			public double Volatility { get; set; }
		}

		private void updateStatistics()
		{
			Model model = getModel();

			// Use _lastPerfCursorTime (stable, user-driven) not CursorTime which resets during redraws
			var perfCursorTime = PerformanceGrid.Visibility == Visibility.Visible
				? _lastPerfCursorTime
				: _showHoldingsTime;
			// IsViewingLive() handles intra-week: Mon–Thu the cursor is at last Friday
			// which is < today but IS the live view. Without this fix, perfViewingLive=false
			// causes _liveMtMBalance to be zeroed and refreshLiveBalance to never run.
			bool perfViewingLive = IsViewingLive(perfCursorTime);

			if (model != null && model.IsLiveMode && perfViewingLive)
				refreshLiveBalance();
			// When on historical date: zero _liveMtMBalance so it can't corrupt balance display
			if (!perfViewingLive)
				_liveMtMBalance = 0;

			var cursorTime = (RebalanceGrid.Visibility == Visibility.Visible) ? _showHoldingsTime : _mainReturnGraph.CursorTime;
			if (cursorTime == default(DateTime) && _portfolioTimes.Count > 0)
			{
				cursorTime = _portfolioTimes.Last();
			}

			if (cursorTime != default(DateTime))
			{
				//Date7.Content = cursorTime.ToString("MM-dd-yyyy");
				Dateb.Content = cursorTime.ToString("MM-dd-yyyy");

				//InceptionDate7.Content = (model != null) ? model.TestingRange.Time1.ToString("MM-dd-yyyy") : "";

				var pcs = getRankingPercents(model);
				var cnt = model.Symbols.Count;

				int rlcnt = (int)Math.Round((100 - pcs.Item1) * cnt / 100.0);
				int rscnt = (int)Math.Round(pcs.Item2 * cnt / 100.0);

				// Intra-week or projected rebalance date: show last settled Friday's holdings.
				var statsLastSettledFriday = _portfolioTimes
					.Where(t => t != default(DateTime) && t.Date <= DateTime.Today && t.DayOfWeek == DayOfWeek.Friday)
					.OrderByDescending(t => t).FirstOrDefault();
				var statsTime = (model != null && model.IsLiveMode
					&& statsLastSettledFriday != default(DateTime)
					&& cursorTime.Date > statsLastSettledFriday.Date)
					? statsLastSettledFriday
					: cursorTime;

				double lcnt = getHoldings(statsTime, 1, 1);
				double scnt = getHoldings(statsTime, -1, 1);

				double lcntAll = getHoldings(statsTime, 1, 1, true);
				double scntAll = getHoldings(statsTime, -1, 1, true);

				LongHoldingsa1.Content = (lcnt > 0) ? lcnt.ToString() : "";
				ShortHoldings1a.Content = (scnt > 0) ? scnt.ToString() : "";
				LongHoldings1.Content = (lcnt > 0) ? lcnt.ToString() : "";
				//ShortHoldings1.Content = (scnt > 0) ? scnt.ToString() : "";
				LongHoldings7a.Content = (lcnt > 0) ? lcnt.ToString() : "";
				//PORTLongCount2.Content = (lcnt > 0) ? lcnt.ToString() : "";
				ShortHoldings7a.Content = (scnt > 0) ? scnt.ToString() : "";
				LongHoldings2a.Content = (lcntAll > 0) ? lcntAll.ToString() : "";
				ShortHoldings2a.Content = (scntAll > 0) ? scntAll.ToString() : "";
				TotalCampaigns.Content = "";
				//LongHoldings2b.Content = (lcntAll > 0) ? lcntAll.ToString() : "";
				//ShortHoldings2b.Content = (scntAll > 0) ? scntAll.ToString() : "";
				//ShortHoldings1a.Content = (lcnt > 0) ? lcnt.ToString() : "";
				//LongHoldings3.Content = (lcnt > 0) ? lcnt.ToString() : "";
				//ShortHoldings3.Content = (scnt > 0) ? scnt.ToString() : "";
				//LongHoldings.Content = (lcnt > 0) ? lcnt.ToString() : "";
				//ShortHoldings.Content = (scnt > 0) ? scnt.ToString() : "";

				if (_selectedUserFactorModel != _selectedUserFactorModel2)
				{
					lcnt = getHoldings(statsTime, 1, 2);
					scnt = getHoldings(statsTime, -1, 2);

					//defLongHoldings.Content = (lcnt > 0) ? lcnt.ToString() : "";
					//defShortHoldings.Content = (scnt > 0) ? scnt.ToString() : "";
					//defLongHoldings3.Content = (lcnt > 0) ? lcnt.ToString() : "";
					//defShortHoldings4.Content = (scnt > 0) ? scnt.ToString() : "";
					//defLongHoldingsb.Content = (lcnt > 0) ? lcnt.ToString() : "";
					//defShortHoldingsb.Content = (scnt > 0) ? scnt.ToString() : "";
				}

				updateTradeStats(cursorTime, 0, 1);
				updateTradeStats(cursorTime, 1, 1);
				updateTradeStats(cursorTime, -1, 1);

				if (_selectedUserFactorModel != _selectedUserFactorModel2)
				{
					updateTradeStats(cursorTime, 0, 2);
					updateTradeStats(cursorTime, 1, 2);
					updateTradeStats(cursorTime, -1, 2);
				}

				var useHedgeCurve = (isTradingModel(model.Name) && _useHedgeCurve);
				var stat1 = getStatistics(_portfolioNavs, _portfolioReturns, useHedgeCurve ? _portfolioHedgeValues : _portfolioValues, useHedgeCurve ? _portfolioHedgeMonthValues : _portfolioMonthValues);

				Return90Valueb.Content = (double.IsNaN(stat1.Return90)) ? "" : stat1.Return90.ToString("##.00");
				Return180Valueb.Content = (double.IsNaN(stat1.Return180)) ? "" : stat1.Return180.ToString("##.00");
				ReturnYTDValueb.Content = (double.IsNaN(stat1.ReturnYTD)) ? "" : stat1.ReturnYTD.ToString("##.00");

				CumulativeReturnValue.Content = (double.IsNaN(stat1.Return)) ? "" : stat1.Return.ToString("##.00");
				CumulativeReturnValue5b.Content = (double.IsNaN(stat1.Return)) ? "" : stat1.Return.ToString("##.00");
				CumulativeReturnValue7.Content = (double.IsNaN(stat1.Return)) ? "" : stat1.Return.ToString("##.00");

				AnnualizedReturnValueb.Content = (double.IsNaN(stat1.AnnualReturn)) ? "" : stat1.AnnualReturn.ToString("##.00");
				MonthlyReturnValueb.Content = (double.IsNaN(stat1.MonthlyReturn)) ? "" : stat1.MonthlyReturn.ToString("##.00");

				//AlphaValue.Content = (double.IsNaN(stat1.Alpha)) ? "" : stat1.Alpha.ToString("##.00");
				//AlphaValue7.Content = (double.IsNaN(stat1.Alpha)) ? "" : stat1.Alpha.ToString("##.00");
				//BetaValue.Content = (double.IsNaN(stat1.Beta)) ? "" : stat1.Beta.ToString("##.00");
				//StdDevValue.Content = (double.IsNaN(stat1.StdDev)) ? "" : stat1.StdDev.ToString("##.00");
				//CorrelationValue.Content = (double.IsNaN(stat1.Correlation)) ? "" : stat1.Correlation.ToString("##.00");
				//AvgDRet.Content = (double.IsNaN(stat1.AvgDRet)) ? "" : stat1.AvgDRet.ToString("##.00");
				//AvgDVol.Content = (double.IsNaN(stat1.AvgDVol)) ? "" : stat1.AvgDVol.ToString("##.00");
				AnnRet.Content = (double.IsNaN(stat1.AnnRet)) ? "" : stat1.AnnRet.ToString("##.00");
				AnnVol.Content = (double.IsNaN(stat1.AnnVol)) ? "" : stat1.AnnVol.ToString("##.00");
				_lastSharpe = stat1.Sharpe;
				_lastSharpePortfolio = _selectedUserFactorModel;
				SharpeRatioValue.Content = (double.IsNaN(stat1.Sharpe)) ? "" : stat1.Sharpe.ToString("##.00");
				SortinoRatioValue.Content = (double.IsNaN(stat1.Sortino)) ? "" : stat1.Sortino.ToString("##.00");
				//BetaValue2.Content = (double.IsNaN(stat1.Beta)) ? "" : stat1.Beta.ToString("##.00");

				MaximumDD.Content = (double.IsNaN(stat1.MaxDrawDown)) ? "" : stat1.MaxDrawDown.ToString("##.00");
				DailyMaximumDD.Content = (double.IsNaN(stat1.DailyMaxDrawDown)) ? "" : stat1.DailyMaxDrawDown.ToString("##.00");
				WeeklyMaximumDD.Content = (double.IsNaN(stat1.WeeklyMaxDrawDown)) ? "" : stat1.WeeklyMaxDrawDown.ToString("##.00");
				MonthlyMaximumDD.Content = (double.IsNaN(stat1.MonthlyMaxDrawDown)) ? "" : stat1.MonthlyMaxDrawDown.ToString("##.00");
				YearlyMaximumDD.Content = (double.IsNaN(stat1.YearlyMaxDrawDown)) ? "" : stat1.YearlyMaxDrawDown.ToString("##.00");
				DDDuration.Content = (double.IsNaN(stat1.MaxDrawDownDuration)) ? "" : stat1.MaxDrawDownDuration.ToString();

				//var stat2 = getStatistics(_compareToValues, _compareToMonthValues);
				//CompareReturnValue.Content = (double.IsNaN(stat2.Return)) ? "" : stat2.Return.ToString("##.00");
				//CompareReturnValue5b.Content = (double.IsNaN(stat2.Return)) ? "" : stat2.Return.ToString("##.00");
				//CompareReturnValue7.Content = (double.IsNaN(stat2.Return)) ? "" : stat2.Return.ToString("##.00");

				//bmAnnualizedReturnValue.Content = (double.IsNaN(stat2.AnnualReturn)) ? "" : stat2.AnnualReturn.ToString("##.00");
				//bmAnnualizedReturnValueb.Content = (double.IsNaN(stat2.AnnualReturn)) ? "" : stat2.AnnualReturn.ToString("##.00");
				//bmMonthlyReturnValue.Content = (double.IsNaN(stat2.MonthlyReturn)) ? "" : stat2.MonthlyReturn.ToString("##.00");
				//bmMonthlyReturnValueb.Content = (double.IsNaN(stat2.MonthlyReturn)) ? "" : stat2.MonthlyReturn.ToString("##.00");

				//bmAlphaValue.Content = (double.IsNaN(stat2.Alpha)) ? "" : stat2.Alpha.ToString("##.00");
				//bmBetaValue.Content = (double.IsNaN(stat2.Beta)) ? "" : stat2.Beta.ToString("##.00");
				//bmCorrelationValue.Content = (double.IsNaN(stat2.Correlation)) ? "" : stat2.Correlation.ToString("##.00");
				//bmSharpeRatioValue.Content = (double.IsNaN(stat2.Sharpe)) ? "" : stat2.Sharpe.ToString("##.00");
				//bmSortinoRatioValue.Content = (double.IsNaN(stat2.Sortino)) ? "" : stat2.Sortino.ToString("##.00");
				//bmVaRValue.Content = (double.IsNaN(stat2.VaR)) ? "" : stat2.VaR.ToString("##.00");
				//bmVOLValue.Content = (double.IsNaN(stat2.Volatility)) ? "" : stat2.Volatility.ToString("##.00");

				//bmMaximumDD.Content = (double.IsNaN(stat2.MaxDrawDown)) ? "" : stat2.MaxDrawDown.ToString("##.00");
				//bmDDDuration.Content = (double.IsNaN(stat2.MaxDrawDownDuration)) ? "" : stat2.MaxDrawDownDuration.ToString();
				//bmDailyMaximumDD.Content = (double.IsNaN(stat2.DailyMaxDrawDown)) ? "" : stat2.DailyMaxDrawDown.ToString("##.00");
				//bmWeeklyMaximumDD.Content = (double.IsNaN(stat2.WeeklyMaxDrawDown)) ? "" : stat2.WeeklyMaxDrawDown.ToString("##.00");
				//bmMonthlyMaximumDD.Content = (double.IsNaN(stat2.MonthlyMaxDrawDown)) ? "" : stat2.MonthlyMaxDrawDown.ToString("##.00");
				//bmYearlyMaximumDD.Content = (double.IsNaN(stat2.YearlyMaxDrawDown)) ? "" : stat2.YearlyMaxDrawDown.ToString("##.00");

				// ATM Default
				//            if (_selectedUserFactorModel != _selectedUserFactorModel2)
				{
					var portfolioValues = loadList<double>(_selectedUserFactorModel2 + " PortfolioValues");
					var portfolioReturns = loadList<double>(_selectedUserFactorModel2 + " PortfolioReturns");
					var portfolioMonthValues = loadList<double>(_selectedUserFactorModel2 + " PortfolioMonthValues");
					var portfolioNavs = loadList<double>(_selectedUserFactorModel2 + " PortfolioNavs");
					var stat3 = getStatistics(portfolioNavs, portfolioReturns, portfolioValues, portfolioMonthValues);

					//defCumulativeReturnValue.Content = (double.IsNaN(stat3.Return)) ? "" : stat3.Return.ToString("##.00");
					defCumulativeReturnValueb.Content = (double.IsNaN(stat3.Return)) ? "" : stat3.Return.ToString("##.00");

					//defAnnualizedReturnValue.Content = (double.IsNaN(stat3.AnnualReturn)) ? "" : stat3.AnnualReturn.ToString("##.00");
					//defAnnualizedReturnValueb.Content = (double.IsNaN(stat3.AnnualReturn)) ? "" : stat3.AnnualReturn.ToString("##.00");
					//defMonthlyReturnValue.Content = (double.IsNaN(stat3.MonthlyReturn)) ? "" : stat3.MonthlyReturn.ToString("##.00");
					//defMonthlyReturnValueb.Content = (double.IsNaN(stat3.MonthlyReturn)) ? "" : stat3.MonthlyReturn.ToString("##.00");
					//defAlphaValue.Content = (double.IsNaN(stat3.Alpha)) ? "" : stat3.Alpha.ToString("##.00");

					//defBetaValue.Content = (double.IsNaN(stat3.Beta)) ? "" : stat3.Beta.ToString("##.00");
					//defCorrelationValue.Content = (double.IsNaN(stat3.Correlation)) ? "" : stat3.Correlation.ToString("##.00");
					//defSharpeRatioValue.Content = (double.IsNaN(stat3.Sharpe)) ? "" : stat3.Sharpe.ToString("##.00");
					//defSortinoRatioValue.Content = (double.IsNaN(stat3.Sortino)) ? "" : stat3.Sortino.ToString("##.00");
					//defVaRValue.Content = (double.IsNaN(stat3.VaR)) ? "" : stat3.VaR.ToString("##.00");
					//defVOLValue.Content = (double.IsNaN(stat3.Volatility)) ? "" : stat3.Volatility.ToString("##.00");

					//defMaximumDD.Content = (double.IsNaN(stat3.MaxDrawDown)) ? "" : stat3.MaxDrawDown.ToString("##.00");
					//defDDDuration.Content = (double.IsNaN(stat3.MaxDrawDownDuration)) ? "" : stat3.MaxDrawDownDuration.ToString();
					//defDailyMaximumDD.Content = (double.IsNaN(stat3.DailyMaxDrawDown)) ? "" : stat3.DailyMaxDrawDown.ToString("##.00");
					//defWeeklyMaximumDD.Content = (double.IsNaN(stat3.WeeklyMaxDrawDown)) ? "" : stat3.WeeklyMaxDrawDown.ToString("##.00");
					//defMonthlyMaximumDD.Content = (double.IsNaN(stat3.MonthlyMaxDrawDown)) ? "" : stat3.MonthlyMaxDrawDown.ToString("##.00");
					//defYearlyMaximumDD.Content = (double.IsNaN(stat3.YearlyMaxDrawDown)) ? "" : stat3.YearlyMaxDrawDown.ToString("##.00");
				}

				bool hedgeCurve = (isTradingModel(model.Name) && _useHedgeCurve);
				Brush brush = hedgeCurve ? Brushes.White : new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));

				//CumulativeReturnValue5.Foreground = brush;
				CumulativeReturnValue5b.Foreground = brush;
				//AnnualizedReturnValue.Foreground = brush;
				//MonthlyReturnValue.Foreground = brush;
				AnnualizedReturnValueb.Foreground = brush;
				MonthlyReturnValueb.Foreground = brush;
				Return90Valueb.Foreground = brush;
				Return180Valueb.Foreground = brush;
				ReturnYTDValueb.Foreground = brush;

				//AlphaValue7.Foreground = brush;
				//BetaValue.Foreground = brush;
				//StdDevValue.Foreground = brush;
				//CorrelationValue.Foreground = brush;
				SharpeRatioValue.Foreground = brush;
				SortinoRatioValue.Foreground = brush;

				MaximumDD.Foreground = brush;
				DailyMaximumDD.Foreground = brush;
				WeeklyMaximumDD.Foreground = brush;
				MonthlyMaximumDD.Foreground = brush;
				YearlyMaximumDD.Foreground = brush;
				DDDuration.Foreground = brush;

				var cursorNav = getPortfolioBalance(cursorTime);
				var liveBeta = getPortfolioBeta(cursorTime, cursorNav);
				var rebalCursorTime = getPreviousRebalanceDate(getModel(), cursorTime, _portfolioTimes);
				var rebalBeta = getRebalanceBeta(rebalCursorTime, cursorNav);
				var cursorBetaText = $"R:{(double.IsNaN(rebalBeta) ? "--" : rebalBeta.ToString("0.00"))}  L:{(double.IsNaN(liveBeta) ? "--" : liveBeta.ToString("0.00"))}";
				BetaValue.Content = cursorBetaText;
				BetaValue2.Content = cursorBetaText;
			}
		}

		private Statistics getStatistics(List<double> portNavs, List<double> portReturns, List<double> portValues, List<double> monthInput)
		{
			var output = new Statistics();

			List<DateTime> portfolioTimes = new List<DateTime>();
			List<double> portfolioValues = new List<double>();
			List<double> portfolioReturns = new List<double>();
			List<double> portfolioNavs = new List<double>();
			List<double> benchmarkValues = new List<double>();
			double monthlyReturn = double.NaN;
			double Return90 = double.NaN;
			double Return180 = double.NaN;
			double ReturnYTD = double.NaN;
			var cursorTime = _mainReturnGraph.CursorTime;

			if (portValues != null && portValues.Count > 0)
			//if (input != null && _benchmarkValues != null && input.Count > 0 && _benchmarkValues.Count > 0)
			{
				int inputOffset = portValues.Count - _portfolioTimes.Count;
				//int benchmarkOffset = _benchmarkValues.Count - _portfolioTimes.Count;

				// Full series — Sharpe/vol always computed over full backtest, never cursor-gated
				var fullReturns = new List<double>();
				var fullTimes = new List<DateTime>();
				for (int jj = 0; jj < _portfolioTimes.Count; jj++)
				{
					var jjIndex = jj + inputOffset;
					fullTimes.Add(_portfolioTimes[jj]);
					fullReturns.Add((jjIndex >= 0 && jjIndex < portReturns.Count) ? portReturns[jjIndex] : double.NaN);
				}

				// Cursor-gated — for display values only
				for (int ii = 0; ii < _portfolioTimes.Count; ii++)
				{
					var inputIndex = ii + inputOffset;
					//var benchmarkIndex = ii + benchmarkOffset;
					var time = _portfolioTimes[ii];
					portfolioTimes.Add(time);
					//benchmarkValues.Add((benchmarkIndex >= 0 && benchmarkIndex < input.Count) ? _benchmarkValues[benchmarkIndex] : double.NaN);

					portfolioNavs.Add((inputIndex >= 0 && inputIndex < portNavs.Count) ? portNavs[inputIndex] : double.NaN);
					portfolioReturns.Add((inputIndex >= 0 && inputIndex < portReturns.Count) ? portReturns[inputIndex] : double.NaN);
					portfolioValues.Add((inputIndex >= 0 && inputIndex < portValues.Count) ? portValues[inputIndex] : double.NaN);

					monthlyReturn = (ii < monthInput.Count) ? monthInput[ii] : double.NaN;
					if (time >= cursorTime) break;
				}

				var annFactor = getAnnFactor(fullTimes); // full series for correct annualization

				int year = cursorTime.Year;
				double start = 0;
				double end = 0;
				double initial = 1000000;
				double annualReturn = 0;
				for (int ii = 0; ii < _portfolioTimes.Count; ii++)
				{
					if (ii < portValues.Count)
					{
						var time = _portfolioTimes[ii];
						if (time.Year == year && start == 0)
						{
							start = initial * (1 + portValues[ii] / 100);
						}
						if (time.Year > year || time > cursorTime) break;
						end = initial * (1 + portValues[ii] / 100);
					}
				}



				annualReturn = 100 * (end - start) / start;
				//double annualizedReturn = 100 * (end - start) / start;  overall return by number of years (1 + Overal Return) 1/7 - 1 = 13.99 annualized return

				int count = portfolioValues.Count;
				if (count > 0)
				{
					output.Return = portfolioValues[count - 1];
					output.Alpha = 0.0; // portfolioValues[count - 1] - benchmarkValues[count - 1];
					var stdBenchmark = getStandardDeviation(benchmarkValues);
					var correlation = atm.getCorrelation(benchmarkValues, portfolioValues);

					var stdPortfolio = getStandardDeviation(portfolioValues);
					output.Beta = correlation * (stdPortfolio / stdBenchmark);
					output.Correlation = correlation;

					var stdReturns = getStandardDeviation(fullReturns); // full series
					var meanReturns = getMean(fullReturns); // full series
															//var riskFreeRate = 0.042 / annFactor;
					output.AvgDRet = meanReturns;
					output.AvgDVol = stdReturns;
					output.AnnRet = 100 * (annFactor * meanReturns);
					output.AnnVol = 100 * Math.Sqrt(annFactor) * stdReturns;
					output.Sharpe = ((annFactor * meanReturns) / (Math.Sqrt(annFactor) * stdReturns));
					output.StdDev = stdReturns;

					//output.InfoRatio = (cumulativeReturns - benchmarkReturns) / getStandardDeviation(getSharpe());

					var postiveReturns = getPostives(fullReturns);
					var negativeReturns = getNegatives(fullReturns);
					var stdNegativeReturns = getStandardDeviation(negativeReturns);
					var sumPositiveReturns = postiveReturns.Sum();
					var sumNegativeReturns = negativeReturns.Sum();
					var profitFactor = sumPositiveReturns / sumNegativeReturns;
					output.Sortino = ((annFactor * meanReturns) / (Math.Sqrt(annFactor) * stdNegativeReturns));


					output.Volatility = 100 * atm.GetVolatility(30, count - 1, new Series(portfolioValues));
					output.MaxDrawDown = getMaxDrawdown(portfolioNavs);
					//output.DailyMaxDrawDown = getMaxDrawdown(portfolioTimes, portfolioValues, "Daily");
					//output.WeeklyMaxDrawDown = getMaxDrawdown(portfolioTimes, portfolioValues, "Weekly");
					//output.MonthlyMaxDrawDown = getMaxDrawdown(portfolioTimes, portfolioValues, "Monthly");
					//output.YearlyMaxDrawDown = getMaxDrawdown(portfolioTimes, portfolioValues, "Yearly");

					output.MaxDrawDownDuration = getMaxDrawdownDuration(portfolioValues);
					//output.VaR = VaRValue.HistoricalValueAtRisk(input.ToArray(), 20, .95);
					//output.AvgTurnover = _averageTurnover;

					var time90 = cursorTime - new TimeSpan(90, 0, 0, 0);
					var time180 = cursorTime - new TimeSpan(180, 0, 0, 0);
					var time365 = cursorTime - new TimeSpan(365, 0, 0, 0);
					var index90 = _portfolioTimes.FindIndex(x => x >= time90);
					var index180 = _portfolioTimes.FindIndex(x => x >= time180);
					var index365 = _portfolioTimes.FindIndex(x => x >= time365);

					Return90 = (index90 >= 0 && index90 < _portfolioValues.Count) ? _portfolioValues.Last() - _portfolioValues[index90] : Double.NaN;
					Return180 = (index180 >= 0 && index180 < _portfolioValues.Count) ? _portfolioValues.Last() - _portfolioValues[index180] : Double.NaN;
					ReturnYTD = (index365 >= 0 && index365 < _portfolioValues.Count) ? _portfolioValues.Last() - _portfolioValues[index365] : Double.NaN;

					output.Return90 = Return90;
					output.Return180 = Return180;
					output.ReturnYTD = ReturnYTD;
					output.MonthlyReturn = monthlyReturn;
					output.AnnualReturn = annualReturn;
				}
			}
			return output;
		}

		private double getAnnFactor(List<DateTime> input)
		{
			var annFactor = double.NaN;
			if (input.Count > 0)
			{
				var startTime = input.First();
				var endTime = input.Last();
				var duration = endTime - startTime;
				var yearDur = duration.TotalDays / 365.25;
				annFactor = input.Count / yearDur;
			}
			return annFactor;
		}

		private int getMaxDrawdownDuration(List<double> input)
		{
			double maxValue = double.MinValue;
			int duration = 0;
			int maxDrawdownDuration = 0;
			foreach (var value in input)
			{
				if (!double.IsNaN(value))
				{
					if (value > maxValue)
					{
						maxValue = value;
						duration = 0;
					}
					else
					{
						duration++;
						maxDrawdownDuration = Math.Max(duration, maxDrawdownDuration);
					}
				}
			}
			return maxDrawdownDuration;
		}

		public static double ComputeRankCorrelation(double[] X, double[] Y)
		{
			Debug.Assert(X.Length == Y.Length);
			var n = Math.Min(X.Length, Y.Length);
			var list = new List<DataPoint>(n);
			for (var i = 0; i < n; i++)
			{
				list.Add(new DataPoint() { X = X[i], Y = Y[i] });
			}
			var byXList = list.OrderBy(r => r.X).ToArray();
			var byYList = list.OrderBy(r => r.Y).ToArray();
			for (var i = 0; i < n; i++)
			{
				byXList[i].RankByX = i + 1;
				byYList[i].RankByY = i + 1;
			}
			var sumRankDiff
			  = list.Aggregate((long)0, (total, r) =>
			  total += lsqr(r.RankByX - r.RankByY));
			var rankCorrelation
			  = 1 - (double)(6 * sumRankDiff)
			  / (n * ((long)n * n - 1));
			return rankCorrelation;
		}

		private class DataPoint
		{
			public double X, Y;
			public int RankByX, RankByY;
		}
		public static long lsqr(long d)
		{
			return d * d;
		}

		private double getMaxDrawdown(List<double> input)
		{
			double maxValue = double.MinValue;
			double maxDrawdown = 0;

			for (var ii = 0; ii < input.Count; ii++)
			{
				var value = input[ii];

				if (!double.IsNaN(value))
				{
					if (value > maxValue)
					{
						maxValue = value;
					}
					else
					{
						double drawDown = (maxValue - value) / maxValue * 100;
						maxDrawdown = Math.Max(drawDown, maxDrawdown);
					}
				}
			}
			return maxDrawdown;
		}

		private List<double> getNegatives(List<double> input)
		{
			var output = new List<double>();
			foreach (var value in input)
			{
				output.Add((!double.IsNaN(value) && value < 0) ? value : double.NaN);
			}
			return output;
		}

		private List<double> getPostives(List<double> input)
		{
			var output = new List<double>();
			foreach (var value in input)
			{
				output.Add((!double.IsNaN(value) && value > 0) ? value : double.NaN);
			}
			return output;
		}

		private List<double> getReturns(List<double> input)
		{
			var output = new List<double>();
			double value1 = double.NaN;
			foreach (var value in input)
			{
				output.Add((!double.IsNaN(value) && !double.IsNaN(value1)) ? value - value1 : double.NaN);
				value1 = value;
			}
			return output;
		}

		private class SectorCounts
		{
			public int memberCount = 0;
			public int longCount = 0;
			public int shortCount = 0;
		}

		private void updateNavigation()
		{
			BrushConverter bc = new BrushConverter();
			Brush grayBrush = (Brush)bc.ConvertFrom("#cccccc");
			Brush blueBrush = (Brush)bc.ConvertFrom("#00ccff");
			Brush whiteBrush = (Brush)bc.ConvertFrom("#ffffff");

			bool enableMonthlySummary = (getPortfolioName() == "US 100") && MainView.EnableEditTrades;   //EnableEditTrades  -  this is just Rick

			string portfolioName = getPortfolioName();

			Visibility sectorVisiblilty = (

				portfolioName != "SINGLE CTY ETF" &&
				portfolioName != "GLOBAL INDICES" &&
				portfolioName != "GLOBAL MACRO" &&
				portfolioName != "GLOBAL 10YR" &&
				portfolioName != "SPOT FX" &&
				portfolioName != "COMMODITIES") ? Visibility.Visible : Visibility.Collapsed;
		}

		private void MarketMaps_MouseDown(object sender, MouseButtonEventArgs e)
		{
			Close();
			_mainView.Content = new MarketMonitor(_mainView);
		}

		private void AutoMLView_MouseDown(object sender, MouseButtonEventArgs e)
		{
			Close();
			_mainView.Content = new AutoMLView(_mainView);
		}

		private void Alert_MouseDown(object sender, MouseButtonEventArgs e)
		{
			Close();
			_mainView.Content = new Alerts(_mainView);
		}

		//private void Auto_MouseDown(object sender, MouseButtonEventArgs e)
		//{
		//    _mainView.Content = new Accuracy(_mainView);
		//}

		private void FundamentalML_MouseDown(object sender, MouseButtonEventArgs e)
		{
			showPortfolioSetup();
		}

		private void showPortfolioSetup()
		{
			// Non-Admin roles never see Portfolio Setup -- redirect to rebalance grid
			if (!ATMML.Auth.AuthContext.Current.IsAdmin)
			{
				showRebalanceGrid();
				return;
			}
			if (!_suppressTISetup) TISetup.Visibility = Visibility.Visible;
			PortfolioInput2.Visibility = MainView.EnableNetworks ? Visibility.Visible : Visibility.Collapsed;
			PortfolioInput3.Visibility = MainView.EnableNetworks ? Visibility.Visible : Visibility.Collapsed;
			PortfolioInput4.Visibility = MainView.EnableNetworks ? Visibility.Visible : Visibility.Collapsed;
			PortfolioInput5.Visibility = MainView.EnableNetworks ? Visibility.Visible : Visibility.Collapsed;
			UserFactorModelGrid.Visibility = _useUserFactorModel ? Visibility.Visible : Visibility.Collapsed;
			PerformanceGrid.Visibility = Visibility.Collapsed;
			PerformanceSideNav.Visibility = Visibility.Collapsed;
			ATMStrategyOverlay.Visibility = Visibility.Collapsed;
			//Example.Visibility = Visibility.Visible;
			RebalanceChartGrid.Visibility = Visibility.Collapsed;
			RebalanceGrid.Visibility = Visibility.Collapsed;
			RebalanceSideNav.Visibility = Visibility.Collapsed;
			AllocationGrid.Visibility = Visibility.Collapsed;
			AllocationSideNav.Visibility = Visibility.Collapsed;
			SetupNav.Visibility = Visibility.Collapsed;
		}

		private void Server_MouseDown(object sender, MouseButtonEventArgs e)
		{
			Close();
			_mainView.Content = new Timing(_mainView);
		}

		private void OurView_MouseDown(object sender, MouseButtonEventArgs e)
		{
			Close();
			_mainView.Content = new Charts(_mainView);
		}

		private void Arrow_MouseEnter(object sender, MouseEventArgs e)
		{
			Label label = sender as Label;
			label.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xcc, 0xff));
		}


		private void Arrow_MouseLeave(object sender, MouseEventArgs e)
		{
			Label label = sender as Label;
			label.Foreground = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));
		}

		private void RunPortfolio_MouseDown(object sender, MouseButtonEventArgs e)
		{
			var isAdmin = ATMML.Auth.AuthContext.Current.IsAdmin;
			ui_to_userFactorModel(_selectedUserFactorModel);
			hideNavigation();
			TISetup.Visibility = Visibility.Collapsed;
			TIOpenPL.Visibility = Visibility.Collapsed;
			TIAllocations.Visibility = Visibility.Collapsed;
			RebalanceGrid.Visibility = Visibility.Collapsed;
			RebalanceChartGrid.Visibility = Visibility.Collapsed;
			RebalanceSideNav.Visibility = Visibility.Collapsed;
			PerformanceGrid.Visibility = Visibility.Collapsed;
			PerformanceSideNav.Visibility = Visibility.Collapsed;
			UserFactorModelGrid.Visibility = Visibility.Visible;
			// Re-collapse TISetup for non-Admin at every dispatcher priority to catch WPF resets
			if (!isAdmin)
			{
				TISetup.Visibility = Visibility.Collapsed;
				Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Send,    new Action(() => TISetup.Visibility = Visibility.Collapsed));
				Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal,  new Action(() => TISetup.Visibility = Visibility.Collapsed));
				Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render,  new Action(() => TISetup.Visibility = Visibility.Collapsed));
				Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded,  new Action(() => TISetup.Visibility = Visibility.Collapsed));
				Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() => TISetup.Visibility = Visibility.Collapsed));
			}
			setModelRadioButtons();
			_progressState = ProgressState.CollectingData;
			_run = true;
		}

		private void SendOrders_MouseEnter(object sender, MouseEventArgs e)
		{
			Label label = sender as Label;
			if (label != null)
				label.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xcc, 0xff));
		}

		private void SendOrders_MouseLeave(object sender, MouseEventArgs e)
		{
			Label label = sender as Label;
			if (label != null)
				label.Foreground = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));
		}

		private void SendOrders_MouseDown(object sender, MouseButtonEventArgs e)
		{
			// ── Single flag controls: time lock, factory mode, and UI flip ───────────
			// useMock = true  → works any time of day, no real orders sent, grid unchanged
			// useMock = false → time lock enforced, live FlexOne orders, grid flips
			// TODO: set to false when FlexOne credentials are provided
			const bool useMock = true;
			// ─────────────────────────────────────────────────────────────────────────

			var now = DateTime.Now.TimeOfDay;
			if (!useMock && now < TimeSpan.FromHours(15))
			{
				var label = sender as Label;
				if (label != null)
					label.Content = "\u23F3 Send Orders to FlexOne";
				MessageBox.Show(
					$"Current time:  {DateTime.Now:HH:mm}\n\n" +
					 "Orders cannot be placed before 3:00 PM.\n" +
					 "The 3:00 PM signal run is the authoritative trade list.\n\n" +
					 "Run the model again at or after 3:00 PM to send final orders.",
					"\u26A0  Cannot Place Trades Before 3:00 PM",
					MessageBoxButton.OK,
					MessageBoxImage.Warning);
				return;
			}

			// ── FlexOne order submission ──────────────────────────────────────────────
			FlexOneRebalanceResult result = null;
			try
			{
				var trades = FlexOneTradeAdapter.BuildFlexOneTrades(
					_flexEnteredTrades,
					_flexExitedTrades,
					_flexAddTrades,
					_flexReduceTrades,
					_flexTradesTime1,
					_flexTradesTime2,
					_flexShares,
					_flexPrice);

				if (trades.Count == 0)
				{
					MessageBox.Show(
						"No orders were generated from the current rebalance.\n\n" +
						"Verify the model has run and trade lists are populated.",
						"\u26A0  No Orders to Send",
						MessageBoxButton.OK,
						MessageBoxImage.Warning);
					return;
				}

				var flexOneConfig = new FlexOneConfig
				{
					Host = "your-flexone-host",   // TODO: move to app config / settings file
					Port = 8080,
					User = "PFA",
					Password = "your-password"        // TODO: move to app config / settings file
				};

				var bridge = FlexOneBridgeFactory.Create(flexOneConfig, useMock: useMock);
				result = bridge.SubmitRebalance(trades);

				if (!result.Success || result.OrdersFailed > 0)
				{
					var failures = string.Join("\n", result.Details
						.Where(d => !d.Success)
						.Select(d => $"  • {d.Ticker}: {d.Message}"));

					var msg = $"Orders placed:  {result.OrdersPlaced}\n" +
							  $"Orders failed:  {result.OrdersFailed}\n\n" +
							  (string.IsNullOrEmpty(failures) ? "" : $"Failed orders:\n{failures}\n\n") +
							  "Do you want to continue and update the portfolio grid?";

					var proceed = MessageBox.Show(msg,
						"\u26A0  FlexOne Submission Warnings",
						MessageBoxButton.YesNo,
						MessageBoxImage.Warning);

					if (proceed == MessageBoxResult.No)
						return;
				}
				else
				{
					Console.WriteLine($"[FlexOne] All {result.OrdersPlaced} orders submitted successfully.");

					// Log each submitted trade to the blotter
					foreach (var detail in result.Details.Where(d => d.Success))
					{
						AuditService.LogTradeSubmitted(
							portfolioId: _clientPortfolioName,
							ticker: detail.Ticker,
							side: detail.Message ?? "Unknown",
							shares: 0,
							price: null,
							orderState: "Submitted",
							modelVersion: getModel()?.Name,
							isOverride: false
						);
					}

					AuditService.LogAction("FLEXONE_SUBMISSION_SUCCESS",
						objectType: "Portfolio",
						objectId: _clientPortfolioName,
						after: $"OrdersPlaced={result.OrdersPlaced}");
				}
			}
			catch (Exception ex)
			{
				AuditService.LogAction("FLEXONE_SUBMISSION_FAILED",
					objectType: "Portfolio",
					objectId: _clientPortfolioName,
					after: ex.Message);

				MessageBox.Show(
					$"FlexOne submission failed:\n\n{ex.Message}\n\n" +
								 "Orders were NOT sent.\nThe portfolio grid has not been updated.",
					"\u274C  FlexOne Error",
					MessageBoxButton.OK,
					MessageBoxImage.Error);
				return;   // do NOT flip UI — nothing went out
			}
			// ─────────────────────────────────────────────────────────────────────────

			// Live only: lock in the rebalance and flip the UI to the new portfolio.
			if (!useMock)
			{
				_liveOrdersSent = true;
				SaveOrdersSentFlag();
				drawPortfolioGrid();
				updateStatistics();
			}
			else
			{
				// result.Details contains one entry per order with Ticker + Message.
				// Message from FlexOneMockBridge: "[Mock] Accepted — {Action} {Shares} {Ticker}"
				var summary = string.Join("\n", result.Details
					.Select(d => $"  {(d.Success ? "\u2713" : "\u2717")} {d.Message}"));

				MessageBox.Show(
					$"Mock test complete.\n\n" +
					$"Orders built:   {result.OrdersPlaced + result.OrdersFailed}\n" +
					$"Orders passed:  {result.OrdersPlaced}\n" +
					$"Orders failed:  {result.OrdersFailed}\n\n" +
					$"{summary}\n\n" +
					 "No real orders were placed. ATM SP 1 is unchanged.",
					"\u2705  FlexOne Mock Test",
					MessageBoxButton.OK,
					MessageBoxImage.Information);
			}
		}
		private void highlightButton(StackPanel panel, string selectedButton, bool foregroundHighlight = true)
		{
			//if (panel == Level1Panel)
			//{
			//    var model = getModel();
			//    if (model != null)
			//    {
			//        var text = model.TickerNavigationPaths;
			//    }
			//}


			if (selectedButton != null && selectedButton.Length > 0)
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

		private void showView()
		{
			//PortfolioGrid.Visibility = Visibility.Collapsed;
			//OverView.Visibility = Visibility.Collapsed;

			if (_useUserFactorModel)
			{
				//MLFactorModelGrid.Visibility = Visibility.Collapsed;
				UserFactorModelGrid.Visibility = Visibility.Visible;
			}
			else
			{
				//MLFactorModelGrid.Visibility = Visibility.Visible;
				UserFactorModelGrid.Visibility = Visibility.Collapsed;
			}

			hideNavigation();
		}

		private void hideView()
		{
			//OverView.Visibility = Visibility.Visible;
			//PortfolioGrid.Visibility = Visibility.Visible;
			UserFactorModelGrid.Visibility = Visibility.Visible;
		}

		private void hideNavigation()
		{
			Menus.Visibility = Visibility.Collapsed;

			Level1Panel.Visibility = Visibility.Collapsed;
			BenchmarkMenu1.Visibility = Visibility.Collapsed;
			Level2Panel.Visibility = Visibility.Collapsed;
			Level3Panel.Visibility = Visibility.Collapsed;
			GroupMenu2.Visibility = Visibility.Collapsed;
			IndustryMenuScroller1.Visibility = Visibility.Collapsed;
			IndustryMenuScroller2.Visibility = Visibility.Collapsed;
			Level5Panel.Visibility = Visibility.Collapsed;
			Level6Panel.Visibility = Visibility.Collapsed;
			IndustryMenu2.Visibility = Visibility.Collapsed;

			Level1Panel.Children.Clear();
			BenchmarkMenu1.Children.Clear();
			Level2Panel.Children.Clear();
			//NetworkGrid.Children.Clear();
			Level3Panel.Children.Clear();
			GroupMenu2.Children.Clear();
			Level4Panel.Children.Clear();
			SubgroupMenu2.Children.Clear();
			Level5Panel.Children.Clear();
			Level6Panel.Children.Clear();
			IndustryMenu2.Children.Clear();
			SubgroupActionMenu1.Visibility = Visibility.Collapsed;
			SubgroupActionMenu2.Visibility = Visibility.Collapsed;
		}



		void go_Click(object sender, MouseEventArgs e)
		{
			_portfolio1.Clear();
			_barCache.Clear();

			TextBoxButton button = sender as TextBoxButton;
			string buttonName = button.Name;
			string portfolioName = button.TextBox.Text;

			_clientPortfolioName = portfolioName;
			_clientPortfolioType = (buttonName == "EQS") ? Portfolio.PortfolioType.EQS : Portfolio.PortfolioType.PRTU;

			//requestPortfolio(portfolioName, _clientPortfolioType);

			hideNavigation();

			showView();

			update(_selectedRegion, "", "", "", "");
			setUniverse(getPortfolioName());
		}

		private void OnTradeHistory(object sender, MouseButtonEventArgs e)
		{
			Model model = getModel();
			var time1 = default(DateTime);
			_portfolioTimes.ForEach(time2 =>
			{
				var oldTrades = (time1 == default(DateTime)) ? new List<Trade>() : _portfolio1.GetTrades(model.Name, "", time1);
				var newTrades = (time2 == default(DateTime)) ? new List<Trade>() : _portfolio1.GetTrades(model.Name, "", time2);
				var enteredTrades = newTrades.Where(x => !oldTrades.Any(y => x.Ticker == y.Ticker && x.Direction == y.Direction)).ToList();
				var exitedTrades = oldTrades.Where(x => !newTrades.Any(y => x.Ticker == y.Ticker && x.Direction == y.Direction)).ToList();
				var addTrades = newTrades.Where(x => x.Shares.ContainsKey(time1) && x.Shares.ContainsKey(time2) && x.Shares[time2] > x.Shares[time1]).ToList();
				var reduceTrades = newTrades.Where(x => x.Shares.ContainsKey(time1) && x.Shares.ContainsKey(time2) && x.Shares[time2] < x.Shares[time1]).ToList();

				var date = time2.ToString("yyyy-MM-dd");
				enteredTrades.ForEach(trade =>
				{
					var ticker = trade.Ticker;
					var side = trade.Direction > 0 ? "New Long" : "New Short";
					var shares0 = trade.Shares.ContainsKey(time2) ? trade.Shares[time2] : 0;
					var shares1 = trade.Shares.ContainsKey(time1) ? trade.Shares[time1] : 0;
					var shares = Math.Abs(shares0 - shares1);
					var price = trade.Closes.ContainsKey(time2) ? trade.Closes[time2] : 0;
					Trace.WriteLine("New," + date + "," + side + "," + ticker + "," + shares.ToString("0") + "," + price.ToString("0.##"));
				});
				addTrades.ForEach(trade =>
				{
					var ticker = trade.Ticker;
					var side = trade.Direction > 0 ? "Add Long" : "Add Short";
					var shares0 = trade.Shares.ContainsKey(time2) ? trade.Shares[time2] : 0;
					var shares1 = trade.Shares.ContainsKey(time1) ? trade.Shares[time1] : 0;
					var shares = Math.Abs(shares0 - shares1);
					var price = trade.Closes.ContainsKey(time2) ? trade.Closes[time2] : 0;
					Trace.WriteLine("Add," + date + "," + side + "," + ticker + "," + shares.ToString("0") + "," + price.ToString("0.##"));
				});
				reduceTrades.ForEach(trade =>
				{
					var ticker = trade.Ticker;
					var side = trade.Direction > 0 ? "Partial Sell" : "Partial Cover";
					var shares0 = trade.Shares.ContainsKey(time2) ? trade.Shares[time2] : 0;
					var shares1 = trade.Shares.ContainsKey(time1) ? trade.Shares[time1] : 0;
					var shares = Math.Abs(shares0 - shares1);
					var price = trade.Closes.ContainsKey(time2) ? trade.Closes[time2] : 0;
					Trace.WriteLine("Reduce," + date + "," + side + "," + ticker + "," + shares.ToString("0") + "," + price.ToString("0.##"));
				});
				exitedTrades.ForEach(trade =>
				{
					var ticker = trade.Ticker;
					var side = trade.Direction > 0 ? "Close Long" : "Cover Short";
					var shares0 = trade.Shares.ContainsKey(time2) ? trade.Shares[time2] : 0;
					var shares1 = trade.Shares.ContainsKey(time1) ? trade.Shares[time1] : 0;
					var shares = Math.Abs(shares0 - shares1);
					var price = trade.Closes.ContainsKey(time2) ? trade.Closes[time2] : trade.ExitPrice;
					Trace.WriteLine("Exit," + date + "," + side + "," + ticker + "," + shares.ToString("0") + "," + price.ToString("0.##"));
				});
				time1 = time2;
			});
		}

		private void Strategy_MouseDown(object sender, MouseButtonEventArgs e)
		{
			lock (_trades)
			{
				_trades.Clear();
			}

			Label label = e.Source as Label;
			string strategy = label.Content as string;
			_strategy = strategy;
			//highlightButton(StrategyList, _strategy);
			//startCalculatingTrades();
			update();
		}

		private void Level1_MouseDown(object sender, MouseButtonEventArgs e)
		{
			Label label = e.Source as Label;
			string region = label.Content as string;
			setLevel1(region);
		}

		private void setLevel1(string name)
		{
			Level2Panel.Children.Clear();
			Level3Panel.Children.Clear();
			GroupMenu2.Children.Clear();
			Level4Panel.Children.Clear();
			SubgroupMenu2.Children.Clear();
			Level5Panel.Children.Clear();
			Level6Panel.Children.Clear();
			IndustryMenu2.Children.Clear();
			SubgroupActionMenu1.Visibility = Visibility.Collapsed;
			SubgroupActionMenu2.Visibility = Visibility.Collapsed;

			var model = getModel();
			model.Groups[_g].Levels[1][0] = "";
			model.Groups[_g].Levels[2][0] = "";
			model.Groups[_g].Levels[3][0] = "";
			model.Groups[_g].Levels[4][0] = "";
			model.Groups[_g].Levels[5][0] = "";

			_selectedRegion = name;
			highlightButton(Level1Panel, _selectedRegion);
			nav.setNavigationLevel1(name, Level2Panel, Level2_MouseDown, go_Click);
		}

		private void Level2_MouseDown(object sender, MouseButtonEventArgs e)
		{
			SymbolLabel label = e.Source as SymbolLabel;
			string country = (label.Symbol == (string)label.Content) ? label.Symbol : label.Symbol + ":" + label.Content;
			if (country.Length == 0)
			{
				country = label.Content as string;
			}
			var ticker = label.Ticker;
			setLevel2(country, ticker);
		}

		private void setLevel2(string label, string ticker)
		{
			Level3Panel.Children.Clear();
			GroupMenu2.Children.Clear();
			Level4Panel.Children.Clear();
			SubgroupMenu2.Children.Clear();
			Level5Panel.Children.Clear();
			Level6Panel.Children.Clear();
			IndustryMenu2.Children.Clear();
			SubgroupActionMenu1.Visibility = Visibility.Collapsed;
			SubgroupActionMenu2.Visibility = Visibility.Collapsed;

			var model = getModel();
			model.Groups[_g].Levels[2][0] = "";
			model.Groups[_g].Levels[3][0] = "";
			model.Groups[_g].Levels[4][0] = "";
			model.Groups[_g].Levels[5][0] = "";

			_selectedCountry = label;

			highlightButton(Level2Panel, _selectedCountry);

			if (label == "ADD CUSTOM UNIVERSE")
			{
				//ScanDialog dlg = getScanDialog();
			}
			else if (nav.setNavigationLevel2(_selectedCountry, Level3Panel, Level3_MouseDown, go_Click))
			{
				Level3Panel.Visibility = Visibility.Visible;
				highlightButton(Level3Panel, model.Groups[_g].Levels[2][0]);
			}
			else
			{
				Scroller5.Visibility = Visibility.Visible;
				_memberIndex = ticker.Contains('>') ? "" : ticker;
				model.Groups[_g].Levels[3][0] = label;
				var name = getPortfolioName();
				requestMembers(name, Level3Panel);
			}
		}

		private StackPanel _memberPanel = null;
		private string _memberIndex = "";

		//private void requestMembers(string ticker, StackPanel panel)
		//{
		//    _memberPanel = panel;
		//    requestPortfolio(ticker);
		//}

		private void loadMemberPanel()
		{
			var members = _portfolio1.GetSymbols();

			var tickers = members.Select(x => x.Ticker).ToList();

			var items = new List<string>();
			items.Add(_memberIndex);
			var fields = _memberIndex.Split(' ');
			items.Add(fields[0] + ":" + fields[0] + " All Members");
			for (int ii = 0; ii < tickers.Count; ii++)
			{
				var ticker = tickers[ii];
				fields = ticker.Split(' ');
				items.Add(ticker + ":" + fields[0]);
			}
			nav.setNavigation(_memberPanel, Member_MouseDown, items.ToArray());
			_memberPanel = null;

			setCheckBoxes(getModel());
		}

		private void Member_MouseDown(object sender, MouseButtonEventArgs e)
		{
		}

		private void setUniverse(string universe)
		{
			if (_useUserFactorModel)
			{
				UserFactorModelUniverse.Content = universe;
				//if (_userFactorModels.ContainsKey(_selectedUserFactorModel))
				//{
				//    _userFactorModels[_selectedUserFactorModel].Universe = universe;
				//}
			}
			else
			{
				//MLFactorModelUniverse.Content = universe;
				//if (_MLFactorModels.ContainsKey(_selectedMLFactorModel))
				//{
				//    _MLFactorModels[_selectedMLFactorModel].Universe = universe;
				//}
			}
			//Universe2.Content = universe;
			//Universe2b.Content = universe;
			//Universe3.Content = universe;
			//Universe3a.Content = universe;
			//Universe4.Content = universe;
			//Universe5.Content = universe;
			Universe6.Content = universe;
			Universe7.Content = universe;

			//requestPortfolio(universe);
		}

		private void Level3_MouseDown(object sender, MouseButtonEventArgs e)
		{
			SymbolLabel label = e.Source as SymbolLabel;
			string group = (label.Symbol == (string)label.Content) ? label.Symbol : label.Symbol + ":" + label.Content;
			string ticker = label.Ticker;
			setLevel3(group, ticker);
		}

		private void setLevel3(string label, string ticker)
		{
			Level4Panel.Children.Clear();
			Level5Panel.Children.Clear();
			Level6Panel.Children.Clear();

			int index = _benchmarkSelection ? 1 : 0;

			var model = getModel();
			model.Groups[_g].Levels[3][index] = "";
			model.Groups[_g].Levels[4][index] = "";
			model.Groups[_g].Levels[5][index] = "";

			_selectedGroup = label;

			highlightButton(Level3Panel, label);

			if (label != "SPX:SPX >" && nav.setNavigationLevel3(_selectedCountry, label, Level4Panel, Level4_MouseDown))
			{
				Level4Panel.Visibility = Visibility.Visible;
				SubgroupActionMenu1.Visibility = Visibility.Visible;
				highlightButton(Level4Panel, model.Groups[_g].Levels[3][index]);
			}
			else if (_universeSelection)
			{
				IndustryMenuScroller1.Visibility = Visibility.Visible;
				_memberIndex = ticker;
				model.Groups[_g].Levels[3][index] = label;
				var name = getPortfolioName();
				requestMembers(name, Level5Panel);

				//_clientPortfolioName = "";
				//update(_selectedRegion, _selectedCountry, group, "", "");
				//setUniverse(getPortfolioName());
			}
			else if (_benchmarkSelection)
			{
				update(_selectedRegion, _selectedCountry, label, "", "");
				setBenchMark(_selectedGroup);
			}
		}
		private void requestMembers(string ticker, StackPanel panel)
		{
			if (ticker.Length > 0)
			{
				_memberPanel = panel;
				_memberPanel.Visibility = Visibility.Visible;
				requestPortfolio(ticker);
			}
		}

		private void Level4_MouseDown(object sender, MouseButtonEventArgs e)
		{
			SymbolLabel label = e.Source as SymbolLabel;
			string subgroup = (label.Symbol == (string)label.Content) ? label.Symbol : label.Symbol + ":" + label.Content;
			var ticker = label.Ticker;
			setLevel4(subgroup, ticker);
		}

		private void setLevel4(string label, string ticker)
		{
			Level5Panel.Children.Clear();
			Level6Panel.Children.Clear();

			int index = _benchmarkSelection ? 1 : 0;

			var model = getModel();
			model.Groups[_g].Levels[4][index] = "";
			model.Groups[_g].Levels[5][index] = "";

			_selectedSubgroup = label;

			highlightButton(Level4Panel, label);

			if (nav.setNavigationLevel4(_selectedCountry, _selectedGroup, label, Level5Panel, Level5_MouseDown))
			{
				Level5Panel.Visibility = Visibility.Visible;
				highlightButton(Level5Panel, model.Groups[_g].Levels[4][index]);
			}
			else if (_universeSelection)
			{
				IndustryMenuScroller1.Visibility = Visibility.Visible;
				_memberIndex = ticker;
				model.Groups[_g].Levels[4][index] = label;
				var name = getPortfolioName();
				requestMembers(name, Level5Panel);
				//_clientPortfolioName = "";
				//update(_selectedRegion, _selectedCountry, _selectedGroup, subgroup, "");
				//setUniverse(getPortfolioName());
			}
			else if (_benchmarkSelection)
			{
				update(_selectedRegion, _selectedCountry, _selectedGroup, label, "");
				setBenchMark(_selectedSubgroup);
			}
		}

		private void SubgroupAction_MouseDown(object sender, MouseButtonEventArgs e)
		{
			Label label = sender as Label;

			int index = _benchmarkSelection ? 1 : 0;

			var model = getModel();
			string[] field = model.Groups[_g].Levels[2][index].Split(':');
			string group = field[0];
			string symbol = group + " Index";

			if (label.Name == "SubgroupChart1" || label.Name == "SubgroupChart2")
			{
				//BlpTerminal.RunFunction("GPO", symbol, "ASTGG");
			}
			else if (label.Name == "SubgroupMEMBERS1" || label.Name == "SubgroupMEMBERS2")
			{
				_clientPortfolioName = "";
				update(_selectedRegion, _selectedCountry, _selectedGroup, "MEMBERS", "");
				setUniverse(getPortfolioName());
			}
		}

		private void Level5_MouseDown(object sender, MouseButtonEventArgs e)
		{
			SymbolLabel label = e.Source as SymbolLabel;
			string industry = (label.Symbol == (string)label.Content) ? label.Symbol : label.Symbol + ":" + label.Content;
			string ticker = label.Ticker;
			setLevel5(industry, ticker);

		}

		private void setLevel5(string label, string ticker)
		{
			Level6Panel.Children.Clear();

			int index = _benchmarkSelection ? 1 : 0;

			var model = getModel();
			model.Groups[_g].Levels[5][index] = "";

			_selectedIndustry = label;

			highlightButton(Level5Panel, label);

			if (nav.setNavigationLevel5(_selectedCountry, _selectedGroup, label, Level6Panel, Level6_MouseDown))
			{
				Level6Panel.Visibility = Visibility.Visible;
				highlightButton(Level5Panel, model.Groups[_g].Levels[5][index]);
			}
			else if (_universeSelection)
			{
				SymbolMenuScroller1.Visibility = Visibility.Visible;
				_memberIndex = ticker;
				model.Groups[_g].Levels[5][index] = label;
				var name = getPortfolioName();
				requestMembers(name, Level6Panel);
				//_clientPortfolioName = "";
				//update(_selectedRegion, _selectedCountry, _selectedGroup, _selectedSubgroup, industry);
				//setUniverse(getPortfolioName());
			}
			else if (_benchmarkSelection)
			{
				update(_selectedRegion, _selectedCountry, _selectedGroup, _selectedSubgroup, label);
				setBenchMark(label);
			}
		}

		private void Level6_MouseDown(object sender, MouseButtonEventArgs e)
		{
		}

		private void SelectMLModel1_MouseDown(object sender, RoutedEventArgs e)
		{
			if (Menus.Visibility == Visibility.Collapsed)
			{
				// hideView();
				hideNavigation();

				Menus.Visibility = Visibility.Visible;
				ScenarioMenu1.Visibility = Visibility.Visible;
				ScenarioMenu2.Visibility = Visibility.Collapsed;
				ScenarioMenu3.Visibility = Visibility.Collapsed;
				ScenarioMenu4.Visibility = Visibility.Collapsed;
				ATMTimingMenus.Visibility = Visibility.Collapsed;

				Level1Panel.Visibility = Visibility.Collapsed;
				BenchmarkMenu1.Visibility = Visibility.Collapsed;


				MLIntervalsMenu.Visibility = Visibility.Collapsed;
				RankingMenu.Visibility = Visibility.Collapsed;

				RebalanceMenu.Visibility = Visibility.Collapsed;
				AlignmentIntervalMenu.Visibility = Visibility.Collapsed;
				AutoMLIntervalMenu.Visibility = Visibility.Collapsed;

				ATMStrategyMenu.Visibility = Visibility.Collapsed;
				FeaturesMenu.Visibility = Visibility.Collapsed;

				Level2Panel.Visibility = Visibility.Collapsed;
				Level3Panel.Visibility = Visibility.Collapsed;
				Level4Panel.Visibility = Visibility.Collapsed;
				SubgroupMenuScroller1.Visibility = Visibility.Collapsed;
				Level5Panel.Visibility = Visibility.Collapsed;
				Level6Panel.Visibility = Visibility.Collapsed;
				IndustryMenuScroller1.Visibility = Visibility.Collapsed;
				SubgroupActionMenu1.Visibility = Visibility.Collapsed;
				SubgroupActionMenu2.Visibility = Visibility.Collapsed;

				MLRankMenu.Visibility = Visibility.Collapsed;
				MLIterationMenu.Visibility = Visibility.Collapsed;

				//ProgressCalculations.Visibility = Visibility.Collapsed;
				//ProgressCalculations2b.Visibility = Visibility.Collapsed;

				FilterMenu.Visibility = Visibility.Collapsed;
				InputMenu.Visibility = Visibility.Collapsed;
				PortfolioSettings.Visibility = Visibility.Collapsed;

				_universeSelection = true;
				_benchmarkSelection = false;

				nav.UseCheckBoxes = false;

				var modelNames = MainView.getModelNames();

				modelNames.Insert(0, "No Predictions");

				nav.setNavigation(ScenarioMenu1, Model1_MouseDown, modelNames.ToArray());
				highlightButton(ScenarioMenu1, MLModelName.Content as string);
			}
			else
			{
				showView();
			}
		}

		private void Model1_MouseDown(object sender, RoutedEventArgs e)
		{
			ScenarioMenu1.Visibility = Visibility.Visible;

			var label = sender as SymbolLabel;

			var modelName = label.Content as string;

			hideNavigation();
			MLModelName.Content = modelName;
			ScenarioModel.Content = modelName;

			var mlModel = MainView.GetModel(modelName);
			if (mlModel != null)
			{
				string text1 = "";
				string text2 = "";
				bool first = true;
				foreach (var factor in mlModel.FeatureNames)
				{
					var items = factor.Split('\u0002');
					if (!first) { text1 += "\n"; text2 += "\n"; }
					text1 += items[0]; text2 += items[1];
					first = false;
				}
				FeatureEditor1.Text = text1;
				FeatureEditor2.Text = text2;
			}

			updateScenario(mlModel);

			Trained.Content = (mlModel != null && mlModel.UseTicker) ? "Individually" : "Group";
		}

		string[] _scenarioLevel = { "", "", "", "" };

		string _modelName = "";

		private void SelectMLModel2_MouseDown(object sender, RoutedEventArgs e)
		{
			if (Menus.Visibility == Visibility.Collapsed)
			{
				// hideView();
				hideNavigation();

				Menus.Visibility = Visibility.Visible;
				ScenarioMenu1.Visibility = Visibility.Visible;
				ScenarioMenu2.Visibility = Visibility.Collapsed;
				ScenarioMenu3.Visibility = Visibility.Collapsed;
				ScenarioMenu4.Visibility = Visibility.Collapsed;
				ATMTimingMenus.Visibility = Visibility.Collapsed;

				Level1Panel.Visibility = Visibility.Collapsed;
				BenchmarkMenu1.Visibility = Visibility.Collapsed;


				MLIntervalsMenu.Visibility = Visibility.Collapsed;
				RankingMenu.Visibility = Visibility.Collapsed;

				RebalanceMenu.Visibility = Visibility.Collapsed;
				AlignmentIntervalMenu.Visibility = Visibility.Collapsed;
				AutoMLIntervalMenu.Visibility = Visibility.Collapsed;

				ATMStrategyMenu.Visibility = Visibility.Collapsed;
				FeaturesMenu.Visibility = Visibility.Collapsed;

				Level2Panel.Visibility = Visibility.Collapsed;
				Level3Panel.Visibility = Visibility.Collapsed;
				Level4Panel.Visibility = Visibility.Collapsed;
				SubgroupMenuScroller1.Visibility = Visibility.Collapsed;
				Level5Panel.Visibility = Visibility.Collapsed;
				Level6Panel.Visibility = Visibility.Collapsed;
				IndustryMenuScroller1.Visibility = Visibility.Collapsed;
				SubgroupActionMenu1.Visibility = Visibility.Collapsed;
				SubgroupActionMenu2.Visibility = Visibility.Collapsed;

				MLRankMenu.Visibility = Visibility.Collapsed;
				MLIterationMenu.Visibility = Visibility.Collapsed;

				//ProgressCalculations.Visibility = Visibility.Collapsed;
				//ProgressCalculations2b.Visibility = Visibility.Collapsed;_modelName

				FilterMenu.Visibility = Visibility.Collapsed;
				InputMenu.Visibility = Visibility.Collapsed;
				PortfolioSettings.Visibility = Visibility.Collapsed;

				nav.UseCheckBoxes = false;

				var modelNames = MainView.getModelNames();

				modelNames.Insert(0, "No Predictions");

				nav.setNavigation(ScenarioMenu1, Model2_MouseDown, modelNames.ToArray());
				highlightButton(ScenarioMenu1, _modelName);
			}
			else
			{
				showView();
			}
		}

		private void Model2_MouseDown(object sender, MouseButtonEventArgs e)
		{
			Label label = e.Source as Label;
			_modelName = label.Content as string;

			var model = MainView.GetModel(_modelName);
			var modelName = (model != null) ? _modelName : "";
			var scenario = (model != null) ? MainView.GetSenarioLabel(model.Scenario) : "";
			ScenarioModel.Content = modelName;
			//ScenarioModel.Content = modelName + " " + scenario;
			ScenarioName.Content = scenario;

			hideNavigation();
			//showView();

			var modelNames = new List<string>();
			modelNames.Add(_modelName);
			if (_chart1 != null)
			{
				_chart1.ModelNames = modelNames;
				_chart2.ModelNames = modelNames;
			}
		}

		private void SelectedScenario2_MouseDown(object sender, RoutedEventArgs e)
		{
			ScenarioMenu1.Visibility = Visibility.Visible;
			ScenarioMenu2.Visibility = Visibility.Visible;
			ScenarioMenu3.Visibility = Visibility.Visible;
			ScenarioMenu4.Visibility = Visibility.Collapsed;

			var label = sender as SymbolLabel;
			_scenarioLevel[1] = label.Content as string;
			_scenarioLevel[2] = "";
			_scenarioLevel[3] = "";

			highlightButton(ScenarioMenu2, _scenarioLevel[1], true);

			if (nav.setScenarioNavigation(_scenarioLevel, ScenarioMenu3, SelectedScenario3_MouseDown))
			{
				ScenarioMenu3.Visibility = Visibility.Visible;
			}
			else
			{
				hideNavigation();
				var senario = MainView.GetSenarioFromLabel(_scenarioLevel[1]);
				var model = getModel();
				model.Scenario = senario;
				userFactorModel_to_ui(_selectedUserFactorModel);
			}
		}

		private void SelectedScenario3_MouseDown(object sender, RoutedEventArgs e)
		{
			ScenarioMenu1.Visibility = Visibility.Visible;
			ScenarioMenu2.Visibility = Visibility.Visible;
			ScenarioMenu3.Visibility = Visibility.Visible;
			ScenarioMenu4.Visibility = Visibility.Visible;

			var label = sender as SymbolLabel;
			_scenarioLevel[2] = label.Content as string;
			_scenarioLevel[3] = "";

			highlightButton(ScenarioMenu3, _scenarioLevel[2], true);

			if (nav.setScenarioNavigation(_scenarioLevel, ScenarioMenu4, SelectedScenario4_MouseDown))
			{
				ScenarioMenu4.Visibility = Visibility.Visible;
			}
			else
			{
				hideNavigation();
				var senario = MainView.GetSenarioFromLabel(_scenarioLevel[2]);
				var model = getModel();
				model.Scenario = senario;
				userFactorModel_to_ui(_selectedUserFactorModel);
			}
		}

		private void SelectedScenario4_MouseDown(object sender, RoutedEventArgs e)
		{
			var label = sender as SymbolLabel;
			_scenarioLevel[3] = label.Content as string;

			hideNavigation();
			var senario = MainView.GetSenarioFromLabel(_scenarioLevel[2], _scenarioLevel[3]);
			var model = getModel();
			model.Scenario = senario;
			userFactorModel_to_ui(_selectedUserFactorModel);
		}

		private void update(string region, string country, string group, string subgroup, string industry)
		{
			var model = getModel();
			int index = _benchmarkSelection ? 1 : 0;

			model.Groups[_g].Levels[0][index] = region;
			model.Groups[_g].Levels[1][index] = country;
			model.Groups[_g].Levels[2][index] = group;
			model.Groups[_g].Levels[3][index] = subgroup;
			model.Groups[_g].Levels[4][index] = industry;

			hideNavigation();
			showView();
		}

		private string getPortfolioName()
		{
			string portfolioName = "";

			var model = getModel();

			if (_clientPortfolioName.Length > 0)
			{
				portfolioName = _clientPortfolioName;
			}
			else
			{
				string name = "";
				if (model != null)
				{
					if (model.Groups[_g].Levels[5][0] != "" && model.Groups[_g].Levels[5][0] != "MEMBERS") name = model.Groups[_g].Levels[5][0];
					else if (model.Groups[_g].Levels[4][0] != "" && model.Groups[_g].Levels[4][0] != "MEMBERS") name = model.Groups[_g].Levels[4][0];
					else if (model.Groups[_g].Levels[3][0] != "" && model.Groups[_g].Levels[3][0] != "MEMBERS") name = model.Groups[_g].Levels[3][0];
					else if (model.Groups[_g].Levels[2][0] != "" && model.Groups[_g].Levels[2][0] != "MEMBERS") name = model.Groups[_g].Levels[2][0];
					else name = model.Groups[_g].Levels[1][0];
				}

				string[] field = name.Split(':');
				portfolioName = field[0].Replace(">", "").Trim();
			}
			return portfolioName;
		}


		private void requestPortfolio(string name)
		{
			Portfolio.PortfolioType type = Portfolio.PortfolioType.Index;

			string[] fields = name.Split(" & ");
			var firstName = fields[0];

			if (Portfolio.IsBuiltInPortfolio(name))
			{
				type = Portfolio.PortfolioType.BuiltIn;
			}
			else if (firstName.Contains(" Index") || firstName.Contains(" Equity") || firstName.Contains(" Comdty") || firstName.Contains(" Curncy") || Portfolio.isCQGSymbol(firstName))
			{
				type = Portfolio.PortfolioType.Single;
			}
			else if (name.Contains("Spread"))
			{
				type = Portfolio.PortfolioType.Spread;
			}

			if (_clientPortfolioName.Length > 0)
			{
				type = _clientPortfolioType;
			}

			_portfolio1.Clear();
			_barCache.Clear();
			_type = type;

			if (type == Portfolio.PortfolioType.Single)
			{
				var model = getModel();
				if (model != null)
				{
					if (_forAlphaSymbols) model.Groups[_g].AlphaPortfolio = "Custom";
					else model.Groups[_g].HedgePortfolio = "Custom";

					var symbols = new List<Symbol>();
					var portfolioSymbols = _portfolio1.GetSymbols();
					for (int ii = 0; ii < fields.Length; ii++)
					{
						var ticker = fields[ii].Trim();
						var symbol = portfolioSymbols.Find(x => x.Ticker == ticker);
						if (symbol == null)
						{
							symbol = new Symbol(ticker);
							_portfolio1.RequestBetas(ticker);
							symbol.Description = _portfolio1.GetDescription(ticker);
							symbol.Beta = _portfolio1.GetBeta(ticker);
							symbol.Dividends = _portfolio1.GetDividends(ticker);
							symbol.BetaTTest = _portfolio1.GetBetaTTest(ticker);
							symbol.Volatility30D = _portfolio1.GetVolatility30D(ticker);
							symbol.Sector = _portfolio1.GetSector(ticker);
							symbol.Industry = _portfolio1.GetIndustry(ticker);
							symbol.SubIndustry = _portfolio1.GetSubIndustry(ticker);
						}
						symbols.Add(symbol);
					}
					if (_forAlphaSymbols) model.Groups[_g].AlphaSymbols = symbols.Distinct().ToList();
					else model.Groups[_g].HedgeSymbols = symbols.Distinct().ToList();
					saveList<Symbol>(model.Name + " Symbols", model.Symbols);
					userFactorModel_to_ui(model.Name);
				}
			}
			else if (name != "Custom")
			{
				_portfolioRequested = firstName;

				//_portfolioRequestedCount = fields.Length;
				//foreach (var field in fields)
				//{
				//    _portfolio1.RequestSymbols(field.Trim(), type, true);
				//}
				_portfolioRequestedCount = 1;
				var n = firstName.Replace(" All Members", "");
				if (n.Length > 0)
				{
					_portfolio1.RequestSymbols(n, type, true);
				}
			}
			else
			{
				var model = getModel();
				if (model != null)
				{
					_portfolio1.SetSymbols(model.Symbols);
				}
				//var model = getModel();
				//if (model != null)
				//{
				//    requestReferenceData(model.Symbols);
				//}
			}
		}

		private Dictionary<string, double> getAllocations(Model model)
		{
			var output = new Dictionary<string, double>();
			if (model != null)
			{
				if (isTradingModel(model.Name))
				{
					double investment = 1.0 / model.Symbols.Count;

					var trades = _portfolio1.GetTrades(model.Name).Where(x => x.IsOpen());
					foreach (var trade in trades)
					{
						output[trade.Ticker] = Math.Sign(trade.Direction) * investment;
					}
				}
				else if (!_runModel)
				{
					output = _portfolio1.GetModelPortfolioAllocations(model.Name);
				}
			}
			return output;
		}

		private class Allocations
		{
			public Allocations()
			{
				LongAllocation = 0;
				ShortAllocation = 0;
				TotalAllocation = 0;
			}

			public double LongAllocation { get; set; }
			public double ShortAllocation { get; set; }
			public double TotalAllocation { get; set; }
		}

		private Dictionary<string, Allocations> getSectorPercentages(Dictionary<string, double> allocations)
		{
			var output = new Dictionary<string, Allocations>();

			Model model = getModel();

			if (model != null)
			{
				double total = 0;
				foreach (var symbol in model.Symbols)
				{
					if (allocations.ContainsKey(symbol.Ticker))
					{
						if (symbol.Sector != null)
						{
							int sectorNumber;
							int.TryParse(symbol.Sector, out sectorNumber);
							var name = _portfolio1.GetSectorName(sectorNumber);
							var symbolCap = getMarketCap(symbol.Ticker);
							if (!double.IsNaN(symbolCap))
							{
								if (!output.ContainsKey(name))
								{
									output[name] = new Allocations();
								}

								var allocation = allocations[symbol.Ticker];
								if (!double.IsNaN(allocation))
								{
									int direction = Math.Sign(allocation);
									double amount = Math.Abs(allocation);

									if (direction > 0) output[name].LongAllocation += amount;
									if (direction < 0) output[name].ShortAllocation += amount;
									output[name].TotalAllocation += amount;
									total += amount;
								}
							}
						}
					}
				}
				output = output.ToDictionary(x => x.Key, x => { var v = new Allocations(); v.LongAllocation = x.Value.LongAllocation / total; v.ShortAllocation = x.Value.ShortAllocation / total; v.TotalAllocation = x.Value.TotalAllocation / total; return v; });
			}
			return output;
		}

		private Dictionary<string, Allocations> getIndustryPercentages(Dictionary<string, double> allocations)
		{
			var output = new Dictionary<string, Allocations>();

			Model model = getModel();
			if (model != null)
			{
				double total = 0;
				foreach (var symbol in model.Symbols)
				{
					if (allocations.ContainsKey(symbol.Ticker))
					{
						if (symbol.Industry != null)
						{
							var name = symbol.Industry;
							var symbolCap = getMarketCap(symbol.Ticker);
							if (!double.IsNaN(symbolCap))
							{
								if (!output.ContainsKey(name))
								{
									output[name] = new Allocations();
								}

								var allocation = allocations[symbol.Ticker];
								if (!double.IsNaN(allocation))
								{
									int direction = Math.Sign(allocation);
									double amount = Math.Abs(allocation);

									if (direction > 0) output[name].LongAllocation += amount;
									if (direction < 0) output[name].ShortAllocation += amount;
									output[name].TotalAllocation += amount;
									total += amount;
								}
							}
						}
					}
				}
				output = output.ToDictionary(x => x.Key, x => { var v = new Allocations(); v.LongAllocation = x.Value.LongAllocation / total; v.ShortAllocation = x.Value.ShortAllocation / total; v.TotalAllocation = x.Value.TotalAllocation / total; return v; });
			}
			return output;
		}

		private Dictionary<string, double> getCountryPercentages(Dictionary<string, double> allocations)
		{
			var output = new Dictionary<string, double>();

			Model model = getModel();

			if (model != null)
			{
				double total = 0;
				foreach (var symbol in model.Symbols)
				{
					if (allocations.ContainsKey(symbol.Ticker))
					{
						if (symbol.Country != null)
						{
							var symbolCap = getMarketCap(symbol.Ticker);
							if (!double.IsNaN(symbolCap))
							{
								if (!output.ContainsKey(symbol.Country))
								{
									output[symbol.Country] = 0;
								}

								var allocation = allocations[symbol.Ticker];
								if (!double.IsNaN(allocation))
								{
									output[symbol.Country] += allocation;
									total += allocation;
								}
							}
						}
					}
				}

				output = output.ToDictionary(x => x.Key, x => x.Value / total);
			}
			return output;
		}

		private Dictionary<string, double> getSecurityTypePercentages(Dictionary<string, double> allocations)
		{
			var output = new Dictionary<string, double>();

			Model model = getModel();

			if (model != null)
			{
				double total = 0;
				foreach (var symbol in model.Symbols)
				{
					if (allocations.ContainsKey(symbol.Ticker))
					{
						if (symbol.SecurityType != null)
						{
							var symbolCap = getMarketCap(symbol.Ticker);
							if (!double.IsNaN(symbolCap))
							{
								if (!output.ContainsKey(symbol.SecurityType))
								{
									output[symbol.SecurityType] = 0;
								}

								var allocation = allocations[symbol.Ticker];
								if (!double.IsNaN(allocation))
								{
									output[symbol.SecurityType] += allocation;
									total += allocation;
								}
							}
						}
					}
				}
				output = output.ToDictionary(x => x.Key, x => x.Value / total);
			}
			return output;
		}

		private double getTopPercent(double percentage)
		{
			var output = double.NaN;

			Model model = getModel();

			if (model != null)
			{

				int count = (int)Math.Round(model.Symbols.Count * percentage / 100.0);

				var mktCap = new List<double>();

				double totalCap = getTotalMarketCap();
				foreach (var symbol in model.Symbols)
				{
					var symbolCap = getMarketCap(symbol.Ticker);
					if (!double.IsNaN(symbolCap))
					{
						var marketCapPercent = symbolCap / totalCap;
						mktCap.Add(marketCapPercent);
					}
				}

				mktCap.Sort();
				mktCap.Reverse();

				output = 100.0 * mktCap.Take(count).Sum();
			}
			return output;
		}

		private Dictionary<string, Dictionary<string, double>> _marketCaps = new Dictionary<string, Dictionary<string, double>>();

		private double getMarketCap(string symbol, string date = "")
		{
			double output = double.NaN;

			//lock (_marketCaps)
			//{
			//    if (_marketCaps.ContainsKey(symbol))
			//    {
			//        if (date.Length > 0 && _marketCaps[symbol].ContainsKey(date))
			//        {
			//            output = _marketCaps[symbol][date];
			//        }
			//        else
			//        {
			//            var maxDate = _marketCaps[symbol].Keys.ToList().Max();
			//            output = _marketCaps[symbol][maxDate];
			//        }
			//    }
			//    else
			//    {
			//        var marketCaps = _portfolio2.loadData(symbol, "CUR_MKT_CAP");
			//        foreach (var kvp in marketCaps)
			//        {
			//            var marketCapDate = kvp.Key;
			//            var marketCap = kvp.Value;
			//            double marketCapValue;
			//            if (double.TryParse(marketCap, out marketCapValue))
			//            {
			//                if (!_marketCaps.ContainsKey(symbol))
			//                {
			//                    _marketCaps[symbol] = new Dictionary<string, double>();
			//                }
			//                _marketCaps[symbol][marketCapDate] = marketCapValue;
			//                if (marketCapDate == date)
			//                {
			//                    output = marketCapValue;
			//                }
			//            }
			//        }
			//    }
			//}
			return output;
		}

		private double getTotalMarketCap(string date = "")
		{
			double output = 0;

			Model model = getModel();
			foreach (var symbol in model.Symbols)
			{
				var symbolCap = getMarketCap(symbol.Ticker, date);
				if (!double.IsNaN(symbolCap))
				{
					output += symbolCap;
				}
			}

			return output;
		}

		private double getTotalPrice(Dictionary<string, List<Bar>> bars, string date = "")
		{
			double output = 0;

			Model model = getModel();
			foreach (var symbol in model.Symbols)
			{
				var price = getPrice(bars[symbol.Ticker], DateTime.Parse(date));
				if (!double.IsNaN(price))
				{
					output += price;
				}
			}

			return output;
		}

		void requestReferenceData(List<Symbol> symbols)
		{
			_referenceData.Clear();

			int count = symbols.Count;

			_referenceSymbols = new List<string>();
			for (int ii = 0; ii < count; ii++)
			{
				_referenceSymbols.Add(symbols[ii].Ticker);
				_refSyms.Add(symbols[ii].Ticker);
			}

			for (int jj = 0; jj < count; jj++)
			{
				string[] dataFieldNames = { "REL_INDEX", "EQY_WEIGHTED_AVG_PX" };
				_portfolio1.RequestReferenceData(symbols[jj].Ticker, dataFieldNames);
			}
		}

		int _reportCount = 0;

		List<string> _fundamentalSymbols = new List<string>();

		private void deleteStrategy(string name)
		{
			MainView.SendWebRequest(MainView.CMREndPoint + "/deleteStrategy.php?" + "n=" + name);
		}

		private void saveStrategy(string name, Strategy strategy)
		{
			string data = WebUtility.UrlEncode(strategy.LongEntry + ":" + strategy.LongExit + ":" + strategy.ShortEntry + ":" + strategy.ShortExit);

			MainView.SendWebRequest(MainView.CMREndPoint + "/saveStrategy.php?" + "n=" + name + "&" + "v=" + data);
		}

		private Dictionary<string, Strategy> getStrategies()
		{
			var strategies = new Dictionary<string, Strategy>();

			string value = MainView.SendWebRequest(MainView.CMREndPoint + "/loadStrategies.php");

			string[] rows = value.Split(';');
			foreach (string row in rows)
			{
				if (row.Length > 0)
				{
					string[] fields = row.Split(',');
					if (fields.Length >= 2)
					{
						string name = fields[0];
						string data = fields[1];
						fields = data.Split(':');
						if (fields.Length >= 4)
						{
							var strategy = new Strategy();
							strategy.LongEntry = fields[0];
							strategy.LongExit = fields[1];
							strategy.ShortEntry = fields[2];
							strategy.ShortExit = fields[3];

							strategies[name] = strategy;
						}
					}
				}
			}
			return strategies;
		}

		private void toSector_MouseDown(object sender, MouseButtonEventArgs e)
		{
			Label label = sender as Label;
			_selectedSector = label.Content as string;
			if (_selectedSector == "All")
			{
				_selectedSector = "";
			}

			update();
		}

		private void Sector_MouseDown(object sender, MouseButtonEventArgs e)
		{
			_view = "Sector Positions";
			_xClick = 0;

			Label label = sender as Label;
			_selectedSector = label.Content as string;

			update();
		}

		private void StopPrediction_MouseDown(object sender, MouseButtonEventArgs e)
		{

			//atm.runPowerShell();

			_stopModel = true;

			_portfolioNavs.Clear();
			_portfolioValues.Clear();
			_portfolioReturns.Clear();
			_portfolioHedgeValues.Clear();
			_portfolioTimes.Clear();
			_benchmarkValues.Clear();
			_compareToValues.Clear();
			//_compareToMonthValues.Clear();
			_portfolioMonthTimes.Clear();
			_portfolioMonthValues.Clear();
			_portfolioHedgeMonthValues.Clear();
			_averageTurnover = 0;
			clearStats();
			_update = 12;

			hideNavigation();

			_factorInputs.Clear();
			_calculating = false;
			_barCache.Clear();

			_progressState = ProgressState.Finished;

			if (_ic != null)
			{
				_ic.Stop();
				var modelName = _ic.ModelName;
				//updateProgress(modelName);
			}
		}


		bool _run = false;

		private void RunMLModel_Mousedown(object sender, MouseButtonEventArgs e)
		{
			//if (_accuracyView != null)
			//{
			//    _selectedUserFactorModel = _accuracyView.Model.Name;
			//    _accuracyView.Close();
			//    _accuracyView = null;
			//}

			ui_to_userFactorModel(_selectedUserFactorModel);
			//saveUserFactorModels();

			_tradeOverviewGroup = "Open";

			hideNavigation();

			if (ATMML.Auth.AuthContext.Current.IsAdmin && !_suppressTISetup)
				TISetup.Visibility = Visibility.Visible;
			//TIStats.Visibility = Visibility.Collapsed;
			//TIPositions.Visibility = Visibility.Collapsed;
			TIOpenPL.Visibility = Visibility.Collapsed;
			TIAllocations.Visibility = Visibility.Collapsed;
			RiskTable.Visibility = Visibility.Collapsed;
			RecoveryTable.Visibility = Visibility.Collapsed;
			UserFactorModelGrid.Visibility = Visibility.Visible;
			PerformanceGrid.Visibility = Visibility.Collapsed;
			ReturnTable.Visibility = Visibility.Collapsed;
			PerformanceSideNav.Visibility = Visibility.Collapsed;
			ATMStrategyOverlay.Visibility = Visibility.Collapsed;
			RebalanceChartGrid.Visibility = Visibility.Collapsed;
			RebalanceGrid.Visibility = Visibility.Collapsed;
			RebalanceSideNav.Visibility = Visibility.Collapsed;
			AllocationGrid.Visibility = Visibility.Collapsed;
			AllocationSideNav.Visibility = Visibility.Collapsed;

			setModelRadioButtons();

			UserFactorModelPanel2.Visibility = Visibility.Visible;

			_run = true;
		}

		private void run()
		{

			updateUserFactorModelList();

			var model = getModel();
			if (model != null)
			{

				if (_portfolioTimes.Count > 0)
				{
					var date = _portfolioTimes.Last();
					setHoldingsCursorTime(date);
				}

				PositionsChartBorder.Visibility = Visibility.Collapsed;
				TotalReturnChartBorder.Visibility = Visibility.Collapsed;
				updateModelData();

				if (checkMLModel())
				{
					ProgressCalculations2.Visibility = Visibility.Visible;
					clearStats();
					_progressState = ProgressState.CollectingData;
					var modelName = _ic.ModelName;
					//updateProgress(modelName);

					clearPredictions();
					runSelectedModel();
				}
			}

			update();
		}

		private void clearPredictions()
		{
			_factorScores.Clear();
			string name = _selectedMLFactorModel;
			string fileName = name + ".log";
			File.Delete(fileName);
		}

		private double getTradePrice(Model model, List<Bar> bars, int index)
		{
			double output = double.NaN;
			if (model.Slippage == Slippage.CurrentClose)
			{
				output = getLastClose(bars, index);
			}
			else if (model.Slippage == Slippage.NextDayOpen)
			{
				output = getNextOpen(bars, index);
				if (double.IsNaN(output))
				{
					output = getLastClose(bars, index);
				}
			}
			return output;
		}

		private double getLastClose(List<Bar> bars, int index)
		{
			double output = double.NaN;
			for (int ii = index; ii >= 0; ii--)
			{
				var close = bars[ii].Close;
				if (!double.IsNaN(close))
				{
					output = close;
					break;
				}
			}
			return output;
		}
		private double getNextOpen(List<Bar> bars, int index)
		{
			double output = double.NaN;
			for (int ii = index + 1; ii < bars.Count; ii++)
			{
				var open = bars[ii].Open;
				if (!double.IsNaN(open))
				{
					output = open;
					break;
				}
			}
			return output;
		}

		List<string> _currentPortfolio = new List<string>();
		List<DateTime> _portfolioTimes = new List<DateTime>();
		List<double> _portfolioValues = new List<double>();
		List<double> _portfolioReturns = new List<double>();
		List<double> _portfolioNavs = new List<double>();
		List<double> _portfolioHedgeValues = new List<double>();
		List<double> _strategyPortfolioValues = new List<double>();
		List<double> _benchmarkValues = new List<double>();
		List<DateTime> _portfolioMonthTimes = new List<DateTime>();
		List<double> _portfolioMonthValues = new List<double>();
		List<double> _portfolioHedgeMonthValues = new List<double>();
		List<int> _portfolioRegimes = new List<int>();
		double _averageTurnover = 0;

		List<double> _compareToValues = new List<double>();
		//List<double> _compareToMonthValues = new List<double>();
		string _compareToSymbol = "";

		//private Dictionary<DateTime, List<string>> calculatePortfolio()
		//{
		//    var model = getModel();

		//    var output = new Dictionary<DateTime, List<string>>();

		//    string stInterval = "Daily";
		//    string mtInterval = Study.getForecastInterval(stInterval, 1);
		//    string[] intervals = { stInterval, mtInterval };
		//    var symbols = model.Symbols;

		//    var direction = new Dictionary<string, int>();

		//    var nets = new Dictionary<string, Series>();
		//    var ftmtUps = new Dictionary<string, Series>();
		//    var ftmtDns = new Dictionary<string, Series>();
		//    var ftst = new Dictionary<string, Series>();
		//    var tbst = new Dictionary<string, Series>();
		//    var past = new Dictionary<string, Series>();

		//    var time = (_barCache.GetTimes(symbols[0].Ticker, intervals[0], 0));

		//    _progressTotalNumber = symbols.Count;
		//    _progressState = ProgressState.Predicting;
		//    _progressCompletedNumber = 0;

		//    foreach (var symbol in symbols)
		//    {
		//        _progressCompletedNumber++;

		//        direction[symbol.Ticker] = 0;

		//        Dictionary<string, List<DateTime>> times = new Dictionary<string, List<DateTime>>();
		//        Dictionary<string, Series[]> bars = new Dictionary<string, Series[]>();

		//        foreach (string interval in intervals)
		//        {
		//            times[interval] = (_barCache.GetTimes(symbol.Ticker, interval, 0));
		//            bars[interval] = _barCache.GetSeries(symbol.Ticker, interval, new string[] { "Open", "High", "Low", "Close" }, 0);
		//        }

		//        bool bp = false;
		//        if (symbol.Ticker == "SIA Comdty")
		//        {
		//            bp = true;
		//        }

		//        string[] calcIntervals = { intervals[0], intervals[1] };
		//        Series op = bars[intervals[0]][0];
		//        Series hi = bars[intervals[0]][1];
		//        Series lo = bars[intervals[0]][2];
		//        Series cl = bars[intervals[0]][3];
		//        Series PR = Conditions.calculatePositionRatio1(times, bars, calcIntervals, _referenceData, 1);
		//        Series rp = atm.calculateRelativePrice(calcIntervals[0], bars[calcIntervals[0]], _referenceData, 5);
		//        Series score = atm.getScore(times, bars, calcIntervals);

		//        Series himt = bars[intervals[1]][1];
		//        Series lomt = bars[intervals[1]][2];
		//        Series clmt = bars[intervals[1]][3];

		//        Series ftmt = atm.calculateFT(himt, lomt, clmt);
		//        Series FTmtUp = sync(ftmt.IsRising(), intervals[1], intervals[0], times[intervals[1]], times[intervals[0]], false);
		//        Series FTmtDn = sync(ftmt.IsFalling(), intervals[1], intervals[0], times[intervals[1]], times[intervals[0]], false);
		//        Series FTst = atm.calculateFT(hi, lo, cl);

		//        //Series ST = atm.calculateST(hi, lo, cl);

		//        Series st = atm.calculateSTSig(op, hi, lo, cl, 2);
		//        Series sc = atm.calculateSCSig(score, rp, 2);
		//        Series pr = atm.calculatePRSig(PR, 2);

		//        Series tb = atm.calculateTwoBarPattern(op, hi, lo, cl);
		//        Series pa = atm.calculatePressureAlertNoFilter(score, op, hi, lo, cl);

		//        Series net = st + sc + pr;

		//        nets[symbol.Ticker] = net;
		//        ftmtUps[symbol.Ticker] = FTmtUp;
		//        ftmtDns[symbol.Ticker] = FTmtDn;
		//        ftst[symbol.Ticker] = FTst;
		//        tbst[symbol.Ticker] = tb;
		//        past[symbol.Ticker] = pa;
		//    }

		//    bool useMaxBarsInTrade = false;
		//    int maxBarsInTrade = 4;

		//    var startTime = model.TrainingRange.Time1;

		//    for (int ii = 0; ii < time.Count; ii++)
		//    {
		//        output[time[ii]] = new List<string>();

		//        var log = false;// (ii > 1924);

		//        if (log) Trace.WriteLine("Time = " + time[ii].ToString());

		//        if (time[ii] >= startTime)
		//        {
		//            foreach (var symbol in symbols)
		//            {
		//                if (direction[symbol.Ticker] >= 1) direction[symbol.Ticker]++;
		//                if (direction[symbol.Ticker] <= -1) direction[symbol.Ticker]--;

		//                var net = nets[symbol.Ticker];
		//                var ftmtUp = ftmtUps[symbol.Ticker];
		//                var ftmtDn = ftmtDns[symbol.Ticker];
		//                var ftSTs = ftst[symbol.Ticker];
		//                var paSTs = past[symbol.Ticker];
		//                var tbSTs = tbst[symbol.Ticker];

		//                var ftTurnUp = ii >= 2 && ftSTs[ii - 2] > ftSTs[ii - 1] && ftSTs[ii - 1] < ftSTs[ii];
		//                var ftTurnDn = ii >= 2 && ftSTs[ii - 2] < ftSTs[ii - 1] && ftSTs[ii - 1] > ftSTs[ii];

		//                //var enterLong = net[ii - 1] < net[ii] && net[ii] > 0 && ftUp[ii] == 1;
		//                //var enterShort = net[ii - 1] > net[ii] && net[ii] < 0 && ftDn[ii] == 1;

		//                //var enterLong =  (ftTurnUp || paSTs[ii] ==  1 || (tbSTs[ii] ==  1 && ftSTs[ii] < 50)) && ftmtUp[ii] == 1;  // enter long on ft turn, p alert, 2 bar & FT < 50
		//                //var enterShort = (ftTurnDn || paSTs[ii] == -1 || (tbSTs[ii] == -1 && ftSTs[ii] > 50)) && ftmtDn[ii] == 1;

		//                //var enterLong = (ftTurnUp || paSTs[ii] == 1) && ftmtUp[ii] == 1;  // enter long on ft turn, p alert, 2 bar & FT < 50
		//                //var enterShort = (ftTurnDn || paSTs[ii] == -1) && ftmtDn[ii] == 1;

		//                var enterLong = (ftTurnUp && ftSTs[ii] < 70) && ftmtUp[ii] == 1;  // enter long on ft turn, p alert, 2 bar & FT < 50
		//                var enterShort = (ftTurnDn && ftSTs[ii] > 30) && ftmtDn[ii] == 1;


		//                bool bp = false;
		//                if (symbol.Ticker == "SIA Comdty" && time[ii].Year == 2020 && time[ii].Month == 2 && time[ii].Day == 25)
		//                {
		//                    bp = true;
		//                }

		//                //var exitLong  = (net[ii - 1] > net[ii]) || (useMaxBarsInTrade && direction[symbol.Ticker] ==  maxBarsInTrade);
		//                //var exitShort = (net[ii - 1] < net[ii]) || (useMaxBarsInTrade && direction[symbol.Ticker] == -maxBarsInTrade);

		//                //var exitLong =  ftTurnDn || paSTs[ii] == -1 || tbSTs[ii] == -1 || (useMaxBarsInTrade && direction[symbol.Ticker] == maxBarsInTrade);  // exit long on ft turn, p alert and 2 bar
		//                //var exitShort = ftTurnUp || paSTs[ii] ==  1 || tbSTs[ii] ==  1 || (useMaxBarsInTrade && direction[symbol.Ticker] == -maxBarsInTrade);

		//                //var exitLong = ftTurnDn || paSTs[ii] == -1 || (useMaxBarsInTrade && direction[symbol.Ticker] == maxBarsInTrade);  // exit long on ft turn, p alert and 2 bar
		//                //var exitShort = ftTurnUp || paSTs[ii] == 1 || (useMaxBarsInTrade && direction[symbol.Ticker] == -maxBarsInTrade);

		//                var exitLong = ftTurnDn || (useMaxBarsInTrade && direction[symbol.Ticker] == maxBarsInTrade);  // exit long on ft turn, p alert and 2 bar
		//                var exitShort = ftTurnUp || (useMaxBarsInTrade && direction[symbol.Ticker] == -maxBarsInTrade);


		//                if (log && direction[symbol.Ticker] !=  1 && enterLong) Trace.WriteLine(symbol.Ticker + " enter long");
		//                if (log && direction[symbol.Ticker] ==  1 && exitLong) Trace.WriteLine(symbol.Ticker + " exit long");
		//                if (log && direction[symbol.Ticker] != -1 && enterShort) Trace.WriteLine(symbol.Ticker + " enter short");
		//                if (log && direction[symbol.Ticker] == -1 && exitShort) Trace.WriteLine(symbol.Ticker + " exit short");

		//                if (enterLong) direction[symbol.Ticker] = 1;
		//                if (enterShort) direction[symbol.Ticker] = -1;
		//                if (exitLong && !enterShort) direction[symbol.Ticker] = 0;
		//                if (exitShort && !enterLong) direction[symbol.Ticker] = 0;

		//                if (direction[symbol.Ticker] >= 1) output[time[ii]].Add("T:" + symbol.Ticker);
		//                if (direction[symbol.Ticker] <= -1) output[time[ii]].Add("B:" + symbol.Ticker);

		//            }
		//        }
		//    }
		//    return output;
		//}

		private void saveList<T>(string name, List<T> input)
		{
			var path = @"models\lists\" + name;
			StringBuilder sb = new StringBuilder();
			foreach (var item in input)
			{
				var text = item.ToString();
				if (text.Length > 0)
				{
					sb.Append(text + "\n");
				}
			}
			var data = sb.ToString();

			MainView.SaveUserData(path, data);
		}

		private List<T> loadList<T>(string name)
		{
			List<T> output = new List<T>();

			var path = @"models\lists\" + name;
			var data = MainView.LoadUserData(path);

			var lines = data.Split('\n');
			foreach (var line in lines)
			{
				if (line.Length > 0)
				{
					output.Add((T)Convert.ChangeType(line, typeof(T)));
				}
			}
			return output;
		}

		private List<Symbol> loadSymbolList(string name)
		{
			List<Symbol> output = new List<Symbol>();

			string path = @"models\lists\" + name;
			var data = MainView.LoadUserData(path);
			var lines = data.Split('\n');
			foreach (var line in lines)
			{
				if (line.Length > 0)
				{
					var symbol = new Symbol();
					symbol.FromString(line);
					symbol.FromString(line);
					output.Add(symbol);
				}
			}
			return output;
		}

		private void drawChart(FundamentalGraph graph, FundamentalChartCurveType curveType, List<DateTime> times,
			List<double> values1, List<double> values2, List<double> values3, List<double> values4, List<double> values5, List<int> regimes)
		{

			graph.RemoveCurves();

			var model = getModel();
			bool hedgeCurve = (isTradingModel(model.Name) && _useHedgeCurve);
			Brush brush = hedgeCurve ? Brushes.WhiteSmoke : new SolidColorBrush(Color.FromRgb(0x00, 0xcc, 0xff));

			if (times != null)
			{
				graph.Times = times;

				if (values2 != null) // first model   blue curve
				{
					FundamentalChartCurve curve2 = new FundamentalChartCurve(curveType, values2);
					curve2.Brush = new SolidColorBrush(Color.FromRgb(0x00, 0xCC, 0xff));
					graph.AddCurve(curve2);
				}

				var regime1 = MarketRegime.None;
				if (regimes.Count == times.Count)
				{
					for (var ii = 0; ii < regimes.Count; ii++)
					{
						var regime0 = (MarketRegime)regimes[ii];
						if (regime0 != regime1)
						{
							var color = regime0 == MarketRegime.RiskOn ? Colors.Lime : Colors.Red;
							var line = new VerticalLine { Date = times[ii], Color = color };
							graph.AddVerticalLine(line);
							regime1 = regime0;
						}
					}
				}

				//if (values3 != null && BenchmarkCurve.IsChecked == true) // bm curve
				//{
				//    FundamentalChartCurve curve3 = new FundamentalChartCurve(curveType, values3);  // default to benchmark but allow type over on Textbox
				//    curve3.Brush = new SolidColorBrush(Color.FromRgb(0x77, 0x77, 0x77)); //DimGray
				//    graph.AddCurve(curve3);
				//}

				if (values4 != null) // hedge
				{
					FundamentalChartCurve curve4 = new FundamentalChartCurve(curveType, values4);
					curve4.Brush = brush;
					graph.AddCurve(curve4);
				}

				if (values5 != null) // compare model
				{
					FundamentalChartCurve curve5 = new FundamentalChartCurve(curveType, values5);
					curve5.Brush = new SolidColorBrush(Color.FromRgb(0x12, 0x4b, 0x72));
					graph.AddCurve(curve5);
				}

				graph.Draw();
			}
		}

		private void updateCurrentPortfolio()
		{
			PortfolioPanel.Items.Clear();

			if (_currentPortfolio != null)
			{
				foreach (var item in _currentPortfolio)
				{
					string[] fields = item.Split(':');
					string text = (fields.Length > 1) ? fields[1] : "";
					Brush brush = (fields[0] == "B") ? Brushes.Red : Brushes.PaleGreen;
					TextBlock tb = new TextBlock();
					tb.Text = text;
					tb.Foreground = brush;
					PortfolioPanel.Items.Add(tb);
				}
			}
		}

		private List<Bar> getBars(string symbol, string interval, int barCount, List<Bar> sync = null)
		{
			List<Bar> output = null;

			List<Bar> bars = _barCache.GetBars(symbol, interval, 0, barCount);

			if (sync == null || bars.Count == 0)
			{
				int count = barCount - bars.Count;
				for (int ii = 0; ii < count; ii++)
				{
					var bar = new Bar();
					bar.Time = DateTime.MinValue;
					bars.Insert(0, bar);
				}
				output = bars;
			}
			else
			{
				output = new List<Bar>();

				int index2 = 0;
				for (var index1 = 0; index1 < sync.Count; index1++)
				{
					var time1 = sync[index1].Time;
					var time2 = (index2 < bars.Count) ? bars[index2].Time : DateTime.MaxValue;
					if (time1 < time2)
					{
						var bar1 = new Bar();
						bar1.Time = time1;
						output.Add(bar1);
					}
					else if (time1 == time2)
					{
						output.Add(bars[index2]);
						index2++;
					}
					else if (time1 > time2)
					{
						index2++;
						index1--;
					}
				}
			}

			return output;
		}

		private bool rebalance(Model model, DateTime time1, DateTime time2)
		{
			return true;

			bool ok = false;

			while (time2.DayOfWeek == DayOfWeek.Saturday || time2.DayOfWeek == DayOfWeek.Sunday)
			{
				time2 += new TimeSpan(1, 0, 0, 0);
			}

			string period = model.RankingInterval;
			if (period == "Daily")
			{
				ok = true;
			}
			else if (period == "Weekly")
			{
				ok = ((int)time1.DayOfWeek > (int)time2.DayOfWeek);
			}
			else if (period == "Monthly")
			{
				ok = (time1.Month != time2.Month);
			}
			else if (period == "Quarterly")
			{
				ok = (((time1.Month - 1) / 3) != ((time2.Month - 1) / 3));
			}
			else if (period == "SemiAnnually")
			{
				ok = (((time1.Month - 1) / 6) != ((time2.Month - 1) / 6));
			}
			else if (period == "Annually")
			{
				ok = (time1.Year != time2.Year);
			}
			return ok;
		}


		//private int returnPeriod(Model model)
		//{
		//    int output = 1;

		//    string period = model.Rebalance;
		//    if (period == "Weekly")
		//    {
		//        output = 5;
		//    }
		//    else if (period == "Monthly")
		//    {
		//        output = 20;
		//    }
		//    else if (period == "Quarterly")
		//    {
		//        output = 60;
		//    }
		//    else if (period == "Semi Annual")
		//    {
		//        output = 125;
		//    }
		//    else if (period == "Annually")
		//    {
		//        output = 250;
		//    }
		//    return output;
		//}

		private string getInterval(Model model)
		{
			string output = model.RankingInterval;
			if (output == "Semi Annual")
			{
				output = "SemiAnnually";
			}
			else if (output == "Annually")
			{
				output = "Yearly";
			}
			return output;
		}

		Dictionary<DateTime, Dictionary<string, double>> _scores = new Dictionary<DateTime, Dictionary<string, double>>();
		Dictionary<DateTime, Dictionary<string, Dictionary<string, double>>> _rawValues = new Dictionary<DateTime, Dictionary<string, Dictionary<string, double>>>();

		private Tuple<int, int> getRankingPercents(Model model)
		{
			var top = 100;
			var bot = 0;
			if (model != null)
			{
				var rankingType = model.Ranking;

				if (rankingType == "T 10% B 10%") { top = 90; bot = 10; }
				else if (rankingType == "T 20% B 20%") { top = 80; bot = 20; }
				else if (rankingType == "T 30% B 30%") { top = 70; bot = 30; }
				else if (rankingType == "T 40% B 40%") { top = 60; bot = 40; }
				else if (rankingType == "T 50% B 50%") { top = 50; bot = 50; }
				else if (rankingType == "T 60% B 60%") { top = 40; bot = 60; }
				else if (rankingType == "T 70% B 70%") { top = 30; bot = 70; }
				else if (rankingType == "T 80% B 80%") { top = 20; bot = 80; }
				else if (rankingType == "T 90% B 90%") { top = 10; bot = 90; }
				else if (rankingType == "T 100% B 100%") { top = 0; bot = 100; }
				else if (rankingType == "T 10%") { top = 90; }
				else if (rankingType == "T 20%") { top = 80; }
				else if (rankingType == "T 30%") { top = 70; }
				else if (rankingType == "T 40%") { top = 60; }
				else if (rankingType == "T 50%") { top = 50; }
				else if (rankingType == "T 60%") { top = 40; }
				else if (rankingType == "T 70%") { top = 30; }
				else if (rankingType == "T 80%") { top = 20; }
				else if (rankingType == "T 90%") { top = 10; }
				else if (rankingType == "T 100%") { top = 0; }
				else if (rankingType == "B 10%") { bot = 10; }
				else if (rankingType == "B 20%") { bot = 20; }
				else if (rankingType == "B 30%") { bot = 30; }
				else if (rankingType == "B 40%") { bot = 40; }
				else if (rankingType == "B 50%") { bot = 50; }
				else if (rankingType == "B 60%") { bot = 60; }
				else if (rankingType == "B 70%") { bot = 70; }
				else if (rankingType == "B 80%") { bot = 80; }
				else if (rankingType == "B 90%") { bot = 90; }
				else if (rankingType == "B 100%") { bot = 100; }
			}
			return new Tuple<int, int>(top, bot);
		}

		private List<string> getPortfolio(DateTime rebalanceDate, Model model, Dictionary<string, double> scores, Dictionary<string, double> predictions)
		{
			List<string> output = new List<string>();

			List<string> topGroup = new List<string>();
			List<string> botGroup = new List<string>();

			if (model.UseRanking)
			{
				// rank lowest to highest
				List<string> rank = null;
				rank = scores.OrderBy(x => x.Value).Select(x => x.Key).ToList();
				var tickerCount = rank.Count;

				//Debug.WriteLine("REBALANCE TIME = " + rebalanceDate.ToString());
				//foreach (var kvp in rank)
				//{
				//    Debug.WriteLine(kvp.Key + " = " + kvp.Value.ToString("###.00"));
				//}

				var pcs = getRankingPercents(model);
				var top = pcs.Item1;
				var bot = pcs.Item2;

				//double change = 100.0 / tickerCount;
				//double percent = 0.5 * change;

				//               bool filter = (model.Sector.Length > 0);

				// todo = get atm prediction and filter out tickers
				// we now have the universe of tickers sorted by the rebalance score

				if (scores.Count > 0)
				{
					// assume top 10% -  starting with the tickers with the top rebalance score...  find the 10% of the universe that has a buy atm prediction

					var count = (int)(model.GetPortfolioPercent() / 100 * model.Symbols.Count);
					bool getTopGroup = (top != 100);
					bool getBotGroup = (bot != 0);

					// bool useAtmFilters = model.UseATMFactors && (_ftFilter || _scFilter || _stFilter || _prFilter);

					if (getTopGroup)
					{
						// select from lower half  (the higher score half)
						int idx1 = rank.Count - 1;
						int idx2 = scores.Count / 2;
						for (var idx = idx1; idx >= idx2; idx--)
						{
							var ticker = rank[idx];

							var ok1 = true;
							if (model.UseML)
							{
								ok1 = (predictions.ContainsKey(ticker)) ? predictions[ticker] >= 0.5 : false;
							}

							if (ok1)
							{
								topGroup.Add("T:" + ticker);
								if (topGroup.Count == count) break;
							}
						}
					}

					if (getBotGroup)
					{
						// select from upper half  (the lower score half)
						int idx1 = 0;
						int idx2 = rank.Count / 2;
						for (var idx = idx1; idx <= idx2; idx++)
						{
							var ticker = rank[idx];

							var ok1 = true;
							if (model.UseML)
							{

								ok1 = (predictions.ContainsKey(ticker)) ? predictions[ticker] < 0.5 : false;
							}

							if (ok1)
							{
								botGroup.Add("B:" + ticker);
								if (botGroup.Count == count) break;
							}
						}
					}

					//if (filter)
					//{
					//    var keep = new List<string>();
					//    foreach (var item in output)
					//    {
					//        string[] fields = item.Split(':');
					//        string ticker = fields[1];
					//        string sectorNumberText = model.GetSector(ticker);
					//        int sectorNumber;
					//        int.TryParse(sectorNumberText, out sectorNumber);
					//        string sector = _portfolio1.GetSectorName(sectorNumber);
					//        if (String.Compare(sector, model.Sector, StringComparison.OrdinalIgnoreCase) == 0)
					//        {
					//            keep.Add(item);
					//        }
					//    }
					//    output = keep;
					//}
				}
				//else
				//{
				//    // sorted worst to best
				//    foreach (var ticker in rank)
				//    {
				//        string sectorNumberText = model.GetSector(ticker);
				//        int sectorNumber;
				//        int.TryParse(sectorNumberText, out sectorNumber);
				//        string sector = _portfolio1.GetSectorName(sectorNumber);

				//        bool ok = (!filter || sector == model.Sector);
				//        if (ok)
				//        {
				//            if (percent <= bot) output.Add("B:" + ticker);
				//            if (percent >= top) output.Add("T:" + ticker);
				//        }

				//        percent += change;
				//    }
				//}
			}
			else
			{
				topGroup = scores.Select(x => "T:" + x.Key).ToList();
				botGroup = scores.Select(x => "B:" + x.Key).ToList();
			}

			output.AddRange(topGroup);
			output.AddRange(botGroup);


			return output;
		}

		private Dictionary<string, Dictionary<string, Dictionary<DateTime, double>>> _predictionData = new Dictionary<string, Dictionary<string, Dictionary<DateTime, double>>>();

		public static string getInterval(string input, int level)
		{
			string output = "";
			if (input == "Yearly" || input == "1Y")
			{
				output = "Yearly";
			}
			else if (input == "SemiAnnually" || input == "1S")
			{
				//output = "SemiAnnually";
				output = (level == -1) ? "Quarterly" : (level == 0) ? "SemiAnnually" : (level == 1) ? "Yearly" : "Yearly";
			}
			else if (input == "Quarterly" || input == "1Q")
			{
				output = (level == -1) ? "Monthly" : (level == 0) ? "Quarterly" : (level == 1) ? "Yearly" : "Yearly";  //replaced SemiAnnually in level 1
			}
			else if (input == "Monthly" || input == "1M")
			{
				output = (level == -1) ? "Weekly" : (level == 0) ? "Monthly" : (level == 1) ? "Quarterly" : "Yearly";
			}
			else if (input == "Weekly" || input == "1W" || input == "W30")
			{
				output = (level == -1) ? "Daily" : (level == 0) ? "Weekly" : (level == 1) ? "Monthly" : "Quarterly";
			}
			else if (input == "Daily" || input == "1D" || input == "D30")
			{
				output = (level == -1) ? "240" : (level == 0) ? "Daily" : (level == 1) ? "Weekly" : "Monthly";
			}
			else if (input == "240" || input == "240 Min")
			{
				output = (level == -1) ? "120" : (level == 0) ? "240" : (level == 1) ? "Daily" : "Weekly";
			}
			else if (input == "120" || input == "120 Min")
			{
				output = (level == -1) ? "60" : (level == 0) ? "120" : (level == 1) ? "Daily" : "Weekly";
			}
			else if (input == "60" || input == "60 Min")
			{
				output = (level == -1) ? "30" : (level == 0) ? "60" : (level == 1) ? "240" : "Daily";
			}
			else if (input == "30" || input == "30 Min")
			{
				output = (level == -1) ? "15" : (level == 0) ? "30" : (level == 1) ? "120" : "Daily";
			}
			else if (input == "15" || input == "15 Min")
			{
				output = (level == -1) ? "5" : (level == 0) ? "15" : (level == 1) ? "60" : "240";
			}
			else if (input == "5" || input == "5 Min")
			{
				output = (level == -1) ? "1" : (level == 0) ? "5" : (level == 1) ? "30" : "120";
			}
			else if (input == "2" || input == "2 Min")
			{
				output = (level == -1) ? "1" : (level == 0) ? "2" : (level == 1) ? "5" : "15";
			}
			else if (input == "1" || input == "1 Min")
			{
				output = (level == -1) ? "1" : (level == 0) ? "1" : (level == 1) ? "5" : "30";
			}
			return output;
		}

		Dictionary<string, Dictionary<string, Dictionary<string, double>>> _scoreCache = new Dictionary<string, Dictionary<string, Dictionary<string, double>>>();

		private Dictionary<string, double> loadData(string name, Model model, DateTime date)
		{
			Dictionary<string, double> output = null;

			string path = @"models\" + name + @"\User\" + model.Name;
			string data = MainView.LoadUserData(path);

			lock (_scoreCache)
			{
				var key = model.Name + ":" + name;
				if (!_scoreCache.ContainsKey(key))
				{
					_scoreCache[key] = new Dictionary<string, Dictionary<string, double>>();

					var lines = data.Split('\n');
					foreach (var line in lines)
					{
						if (line.Length > 0)
						{

							string[] fields = line.Split(':');
							if (fields.Length == 3)
							{
								string date1 = fields[0];
								string ticker = fields[1];
								double value = double.Parse(fields[2]);

								if (!_scoreCache[key].ContainsKey(date1))
								{
									_scoreCache[key][date1] = new Dictionary<string, double>();
								}

								_scoreCache[key][date1][ticker] = value;
							}
						}
					}
				}
				string dateText = date.ToString("yyyyMMdd");
				output = (_scoreCache[key].ContainsKey(dateText)) ? _scoreCache[key][dateText] : new Dictionary<string, double>();
			}

			return output;
		}

		private string getIntervalAbbreviation(string interval)
		{
			string label = interval;
			if (interval == "1D" || interval == "Daily") label = "D";
			else if (interval == "1W" || interval == "Weekly") label = "W";
			else if (interval == "1M" || interval == "Monthly") label = "M";
			else if (interval == "1Q" || interval == "Quarterly") label = "Q";
			else if (interval == "1S" || interval == "SemiAnually") label = "S";
			else if (interval == "1Y" || interval == "Yearly") label = "Y";
			else label = interval;
			return label;
		}

		private string getPosSetting(int direction)
		{
			string label = (string)PosSetting.Content;
			if (direction > 0) label = "L";
			else if (direction < 0) label = "S";
			else if (direction == 0) label = "L|S";
			return label;
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

		private double getMarkToMarketPrice(string symbol, int year, int month)
		{
			double price = Trade.Manager.GetMarkToMarketPrice(symbol, year, month);
			return price;
		}
		private double getBenchmark(int year, int month)
		{
			double price = Trade.Manager.GetMarkToMarketPrice("SPY US Equity", year, month);
			return price;
		}

		private double getPortfolioMonthReturn(int year, int month, out double lcash, out double scash)
		{
			double monthlyProfit = 0;

			int month1 = month - 1;
			int year1 = year;
			if (month1 == 0)
			{
				month1 = 12;
				year1--;
			}

			int month2 = month;
			int year2 = year;

			int month3 = month + 1;
			int year3 = year;
			if (month3 == 13)
			{
				month3 = 1;
				year3++;
			}

			DateTime startDate = new DateTime(year2, month2, 1);
			DateTime endDate = new DateTime(year3, month3, 1);

			lcash = 0;
			scash = 0;

			lock (_trades)
			{

				foreach (Trade trade in _trades)
				{
					DateTime entryDate = DateTime.Parse(trade.EntryDate, System.Globalization.CultureInfo.CurrentCulture, System.Globalization.DateTimeStyles.AssumeUniversal);
					DateTime exitDate = (trade.ExitDate == "") ? endDate : DateTime.Parse(trade.ExitDate, System.Globalization.CultureInfo.CurrentCulture, System.Globalization.DateTimeStyles.AssumeUniversal);

					if (entryDate < endDate && exitDate >= startDate)
					{
						bool markToMarket1 = (entryDate < startDate);
						bool markToMarket2 = (exitDate >= endDate);

						double multiplier = (trade.Ticker == "ES1 Index") ? 50 : 1;
						double entryPrice = markToMarket1 ? getMarkToMarketPrice(trade.Ticker, year1, month1) : trade.EntryPrice;
						double exitPrice = markToMarket2 ? getMarkToMarketPrice(trade.Ticker, year2, month2) : trade.ExitPrice;  // getPortfolioMonthReturn

						if (!double.IsNaN(entryPrice) && !double.IsNaN(exitPrice))
						{
							//count++;
							double profit = getReturn(trade.Ticker, 1, entryPrice, exitPrice); // 100 * (exitPrice - entryPrice) / entryPrice; // (multiplier * trade.Size * (exitPrice - entryPrice));

							//info += (trade.Symbol + "," + trade.Size.ToString() + "," + entryPrice.ToString() + "," + exitPrice.ToString() + "," + ((int)profit).ToString());

							monthlyProfit += profit;

							if (markToMarket2)
							{
							}
						}
					}
				}
			}

			return monthlyProfit;
		}

		private void updateMonthSummary()
		{
			lock (_trades)
			{
				string portfolioName = getPortfolioName();
				double lastMonthPortfolioBalance = Trade.Manager.getStartBalance(portfolioName);
				double lastYearPortfolioBalance = lastMonthPortfolioBalance;

				double lastMonthBenchmark = 0;
				double lastYearBenchmark = 0;

				double portfolioBalance = 0;
				double monthlyBenchmark = 0;

				double dividendTotal = 0;

				DateTime now = DateTime.Now;

				lock (_summary)
				{
					_summary.Clear();
					for (int year = 2010; year <= now.Year; year++)
					{
						for (int month = 1; month <= 12; month++)
						{
							double lcash = 0;
							double scash = 0;
							double monthlyPortfolioReturnDollar = getPortfolioMonthReturn(year, month, out lcash, out scash);

							monthlyBenchmark = getBenchmark(year, month);

							double monthlyDividends = Trade.Manager.getDividends(portfolioName, year, month);

							if (monthlyDividends != 0)
							{
								dividendTotal += monthlyDividends;
							}

							monthlyPortfolioReturnDollar += monthlyDividends;
							portfolioBalance = lastMonthPortfolioBalance + monthlyPortfolioReturnDollar;

							double monthlyPortfolioReturnPercent = (lastMonthPortfolioBalance == 0) ? 0 : 100 * monthlyPortfolioReturnDollar / lastMonthPortfolioBalance;

							double yearlyPortfolioReturnDollar = portfolioBalance - lastYearPortfolioBalance;
							double yearlyPortfolioReturnPercent = (lastYearPortfolioBalance == 0) ? 0 : 100 * yearlyPortfolioReturnDollar / lastYearPortfolioBalance;

							double monthlyBenchmarkReturnPercent = (lastMonthBenchmark == 0) ? 0 : 100 * (monthlyBenchmark - lastMonthBenchmark) / lastMonthBenchmark;
							double yearlyBenchmarkReturnPercent = (lastYearBenchmark == 0) ? 0 : 100 * (monthlyBenchmark - lastYearBenchmark) / lastYearBenchmark;

							double cash = portfolioBalance - lcash - scash;

							double cashPercent = (portfolioBalance == 0) ? 0 : 100.0 * cash / portfolioBalance;
							double longPercent = (portfolioBalance == 0) ? 0 : 100.0 * lcash / portfolioBalance;
							double shortPercent = (portfolioBalance == 0) ? 0 : 100.0 * scash / portfolioBalance;

							double monthlyAlpha = monthlyPortfolioReturnPercent - monthlyBenchmarkReturnPercent;
							double yearlyAlpha = yearlyPortfolioReturnPercent - yearlyBenchmarkReturnPercent;

							SummaryInfo summary = new SummaryInfo(portfolioBalance, yearlyPortfolioReturnPercent, yearlyPortfolioReturnDollar, 0, yearlyAlpha, monthlyPortfolioReturnPercent, monthlyPortfolioReturnDollar, monthlyAlpha, cashPercent, longPercent, shortPercent);

							string key = year.ToString() + month.ToString("00");
							_summary[key] = summary;

							lastMonthPortfolioBalance = portfolioBalance;
							lastMonthBenchmark = monthlyBenchmark;
						}
						lastYearPortfolioBalance = portfolioBalance;
						lastYearBenchmark = monthlyBenchmark;
					}
				}
			}
		}

		private void getPositions()
		{
			//PositionsChartTitle.Content = _symbol;

			_positions.Clear();
			lock (_allTrades)
			{
				foreach (Trade trade in _allTrades)
				{
					if (trade.Ticker == _symbol)
					{
						_positions.Add(trade);
					}
				}
			}
		}

		//private void updateMonthlySummaryText()
		//{
		//    // LastUpdate.Content = _lastUpdate.ToString("MMMM dd, yyyy");

		//    string key = _monthlySummaryYear.ToString() + _monthlySummaryMonth.ToString("00");

		//    string portfolioName = getPortfolioName();

		//    Visibility showAlpha = (portfolioName == "US 100") ? Visibility.Visible : Visibility.Collapsed;
		//    PortfolioAlphaLabel.Visibility = showAlpha;
		//    PortfolioAlpha.Visibility = showAlpha;
		//    PortfolioAlphaLine.Visibility = showAlpha;
		//    MonthlyAlphaLabel.Visibility = showAlpha;
		//    MonthlyAlpha.Visibility = showAlpha;
		//    MonthlyAlphaLine.Visibility = showAlpha;

		//    lock (_summary)
		//    {

		//        SummaryInfo summary;
		//        if (_summary.TryGetValue(key, out summary) && summary._monthlyReturnPercent != 0)
		//        {
		//            PortfolioBalance.Content = (summary._portfolioBalance == 0) ? "" : "$ " + summary._portfolioBalance.ToString("#,###");
		//            YearToDatePercent.Content = (summary._yearToDatePercent == 0) ? "" : summary._yearToDatePercent.ToString(" 0.00") + " %";
		//            YearToDateDollar.Content = (summary._yearToDateDollar == 0) ? "" : "$ " + summary._yearToDateDollar.ToString("#,###");
		//            //CompoundedReturn.Content = (summary._compoundedReturn == 0) ? "" : summary._compoundedReturn.ToString(" 0.00") + " %";
		//            PortfolioAlpha.Content = (summary._portfolioAlpha == 0) ? "" : summary._portfolioAlpha.ToString(" 0.00") + " %";
		//            MonthlyReturnPercent.Content = (summary._monthlyReturnPercent == 0) ? "" : summary._monthlyReturnPercent.ToString(" 0.00") + " %";
		//            MonthlyReturnDollar.Content = (summary._monthlyReturnDollar == 0) ? "" : "$ " + summary._monthlyReturnDollar.ToString("#,###");
		//            MonthlyAlpha.Content = (summary._monthlyAlpha == 0) ? "" : summary._monthlyAlpha.ToString(" 0.00") + " %";
		//            Cash.Content = (summary._cash == 0) ? "" : summary._cash.ToString(" 0.00") + " %";
		//            Longs.Content = (summary._longs == 0) ? "" : summary._longs.ToString(" 0.00") + " %";
		//            Shorts.Content = (summary._shorts == 0) ? "" : summary._shorts.ToString(" 0.00") + " %";
		//        }
		//        else
		//        {
		//            PortfolioBalance.Content = "";
		//            YearToDatePercent.Content = "";
		//            YearToDateDollar.Content = "";
		//            //CompoundedReturn.Content = "";
		//            PortfolioAlpha.Content = "";
		//            MonthlyReturnPercent.Content = "";
		//            MonthlyReturnDollar.Content = "";
		//            MonthlyAlpha.Content = "";
		//            Cash.Content = "";
		//            Longs.Content = "";
		//            Shorts.Content = "";
		//        }
		//    }
		//}

		public class SharpeSort : IComparer<Trade>
		{
			public int Compare(Trade x, Trade y)
			{
				var xCloseDate = x.CloseDateTime;
				var yCloseDate = y.CloseDateTime;
				int output = xCloseDate.CompareTo(yCloseDate);
				if (output == 0)
				{
					var xOpenDate = x.OpenDateTime;
					var yOpenDate = y.OpenDateTime;
					output = xOpenDate.CompareTo(yOpenDate);
				}
				return output;
			}
		}


		private double getSharpeRatio()
		{
			double output = 0;

			List<Trade> trades = new List<Trade>(_trades);
			var sort = new SharpeSort();
			trades.Sort(sort);

			int dollars = 1000;
			int groupSize = 12;

			double volatilityFactor = Math.Sqrt(groupSize);
			int tradeCount = _trades.Count;
			int groupCount = (int)Math.Ceiling((double)tradeCount / groupSize);

			List<double> returns = new List<double>();
			List<double> volatilities = new List<double>();

			int idx = 0;
			for (int ii = 0; ii < groupCount; ii++)
			{
				double profitSum = 0;
				double costSum = 0;
				List<double> profits = new List<double>();
				for (int jj = 0; jj < groupSize && idx < tradeCount; jj++)
				{
					Trade trade = trades[idx++];
					int size = (int)Math.Round(dollars / trade.EntryPrice);
					double exitPrice = trade.IsOpen() ? getCurrentPrice(trade.Ticker) : trade.ExitPrice;
					double profit = trade.Direction * size * (exitPrice - trade.EntryPrice);
					costSum += size * trade.EntryPrice;
					profitSum += profit;
					profits.Add(profit);
				}
				volatilities.Add(volatilityFactor * getStandardDeviationForSharpeRatio(profits));
				returns.Add(100 * profitSum / costSum);
			}

			double averageFactor = (double)tradeCount / groupSize;
			double averageReturn = returns.Sum() / averageFactor;
			double averageVolatility = volatilities.Sum() / averageFactor;

			output = averageReturn / averageVolatility;

			return output;
		}

		private double getStandardDeviationForSharpeRatio(List<double> input)
		{
			double m = 0.0;
			double s = 0.0;
			int k = 1;

			foreach (double value in input)
			{
				if (!double.IsNaN(value))
				{
					double tmpM = m;
					m += (value - tmpM) / k;
					s += (value - tmpM) * (value - m);
					k++;
				}
			}
			return (k > 1) ? (Math.Sqrt(s / (k - 1))) / 100 : double.NaN;
		}

		private void updateTradeOverviewText()
		{
			string equitiesWinLossRatio = "";
			string equitiesNumberOfWinners = "";
			string equitiesNumberOfLosers = "";
			string equitiesAvgWinners = "";
			string equitiesAvgLosers = "";
			string equitiesAvgHoldingPeriod = "";
			string hedgeAvgHoldingPeriod = "";

			int count = _trades.Count;
			if (count > 0)
			{
				int eWinCount = 0;
				int eLossCount = 0;
				double eWinTotal = 0;
				double eLossTotal = 0;
				int eDurationTotal = 0;

				int eMaxProfitTotal = 0;

				int hWinCount = 0;
				int hLossCount = 0;
				int hDurationTotal = 0;

				for (int index = 0; index < count; index++)
				{
					Trade trade = _trades[index];
					string sym = trade.Ticker;
					int size = (int)trade.Direction;
					string entryDate = trade.EntryDate;
					double entryPrice = trade.EntryPrice;
					string exitDate = trade.ExitDate;
					double exitPrice = trade.IsOpen() ? getCurrentPrice(trade.Ticker) : trade.ExitPrice; // updateTradeOverviewText
					int maxProfitIndex = trade.MaxProfitIndex;

					if (!double.IsNaN(entryPrice) && !double.IsNaN(exitPrice))
					{

						bool hedge = (sym == "ES1 Index");

						if (entryDate != "")
						{
							int time = getDayCount(entryDate, exitDate);

							double entryValue = size * entryPrice;
							double exitValue = size * exitPrice;
							double profitLoss = Math.Floor((10000.0 * (exitValue - entryValue)) / Math.Abs(entryValue)) / 100;

							if (hedge)
							{
								if (profitLoss < 0)
								{
									hLossCount++;
								}
								else
								{
									hWinCount++;
								}
								hDurationTotal += time;
							}
							else
							{
								if (profitLoss < 0)
								{
									eLossCount++;
									eLossTotal -= profitLoss;
								}
								else
								{
									eWinCount++;
									eWinTotal += profitLoss;
								}
								eDurationTotal += time;
								eMaxProfitTotal += maxProfitIndex;
							}
						}
					}
				}

				var eTotalCount = eWinCount + eLossCount;
				var hTotalCount = hWinCount + hLossCount;

				equitiesWinLossRatio = (eTotalCount > 0) ? Math.Floor((10000.0 * eWinCount) / eTotalCount) / 100 + " %" : "";
				equitiesNumberOfWinners = (eTotalCount > 0) ? eWinCount.ToString() : "";
				equitiesNumberOfLosers = (eTotalCount > 0) ? eLossCount.ToString() : "";
				equitiesAvgWinners = (eWinCount > 0) ? Math.Floor((100.0 * eWinTotal) / eWinCount) / 100 + " %" : "";
				equitiesAvgLosers = (eLossCount > 0) ? Math.Floor((100.0 * eLossTotal) / eLossCount) / 100 + " %" : "";
				equitiesAvgHoldingPeriod = (eTotalCount > 0) ? Math.Floor((100.0 * eDurationTotal) / eTotalCount) / 100 + " days" : "";

				// hedgeAvgHoldingPeriod = (hTotalCount > 0) ? Math.Floor((100.0 * hDurationTotal) / hTotalCount) / 100 + " days" : "";
				// reusing for max profit index
				hedgeAvgHoldingPeriod = (eTotalCount > 0) ? Math.Floor((100.0 * eMaxProfitTotal) / eTotalCount) / 100 + "" : "";


				double totalProfit = eWinTotal - eLossTotal;
				TotalProfit.Content = (double.IsNaN(totalProfit)) ? "" : totalProfit.ToString("0.00");
			}

			EquitiesWinLossRatio.Content = equitiesWinLossRatio;
			EquitiesNumberOfWinners.Content = equitiesNumberOfWinners;
			EquitiesNumberOfLosers.Content = equitiesNumberOfLosers;
			EquitiesAvgWinners.Content = equitiesAvgWinners;
			EquitiesAvgLosers.Content = equitiesAvgLosers;
			//EquitiesAvgHoldingPeriod.Content = equitiesAvgHoldingPeriod;
			HedgeAvgHoldingPeriod.Content = hedgeAvgHoldingPeriod;

			double sr = getSharpeRatio();
			Sharpe.Content = (double.IsNaN(sr)) ? "" : sr.ToString("0.00");
		}

		private void updatePositionsText()
		{
			string openPosition = "";
			string openEntryDate = "";
			string openEntryPrice = "";
			string openProfitLoss = "";
			string openHoldingTime = "";
			string closedPosition = "";
			string closedEntryDate = "";
			string closedEntryPrice = "";
			string closedExitDate = "";
			string closedExitPrice = "";
			string closedProfitLoss = "";
			string closedHoldingTime = "";

			int count = _positions.Count;
			for (int ii = 0; ii < count; ii++)
			{
				Trade trade = _positions[ii];

				DateTime dateTime1 = DateTime.Parse(trade.EntryDate, System.Globalization.CultureInfo.CurrentCulture, System.Globalization.DateTimeStyles.AssumeUniversal);
				//bool atZeroHour1 = (dateTime1.Hour == 0 && dateTime1.Minute == 0);
				//string entryDate = dateTime1.ToLocalTime().ToString(atZeroHour1 ? "yyyy-MM-dd" : "yyyy-MM-dd HH:mm");
				string entryDate = dateTime1.ToLocalTime().ToString("yyyy-MM-dd");

				string exitDate = trade.ExitDate;
				if (exitDate.Length > 0)
				{
					DateTime dateTime2 = DateTime.Parse(exitDate, System.Globalization.CultureInfo.CurrentCulture, System.Globalization.DateTimeStyles.AssumeUniversal);
					//bool atZeroHour2 = (dateTime2.Hour == 0 && dateTime2.Minute == 0);
					//exitDate = dateTime2.ToLocalTime().ToString(atZeroHour2 ? "yyyy-MM-dd" : "yyyy-MM-dd HH:mm");
					exitDate = dateTime2.ToLocalTime().ToString("yyyy-MM-dd");
				}

				if ((trade.EntryDate == _entryDate) || (_entryDate == "" && ii == count - 1))
				{
					bool tradeClosed = (exitDate != "");
					int size = (int)trade.Direction;
					int time = getDayCount(entryDate, exitDate);

					// Use blended AvgPrice if available (accounts for position adds/trims),
					// otherwise fall back to original EntryPrice
					double avgEntryPrice = (trade.AvgPrice != null && trade.AvgPrice.Count > 0)
						? trade.AvgPrice.OrderByDescending(kvp => kvp.Key).First().Value
						: trade.EntryPrice;

					double entryValue = size * avgEntryPrice;
					double exitValue = size * (trade.IsOpen() ? getCurrentPrice(trade.Ticker) : trade.ExitPrice);
					if (!double.IsNaN(exitValue))
					{
						double profitLoss = Math.Floor((10000 * (exitValue - entryValue)) / Math.Abs(entryValue)) / 100;

						if (tradeClosed)
						{
							if (size == 1) closedPosition = "Long";
							else if (size == -1) closedPosition = "Short";
							else closedPosition = (size < 0) ? "S " + -size : "L " + size;
							closedEntryDate = entryDate;
							closedEntryPrice = avgEntryPrice.ToString("0.00");
							closedExitDate = exitDate;
							closedExitPrice = trade.ExitPrice.ToString();
							closedProfitLoss = profitLoss.ToString(" 0.00") + " %";
							closedHoldingTime = time + " days";
						}
						else
						{
							if (size == 1) openPosition = "Long";
							else if (size == -1) openPosition = "Short";
							else openPosition = (size < 0) ? "S " + -size : "L " + size;
							openEntryDate = entryDate;
							openEntryPrice = avgEntryPrice.ToString("0.00");
							openProfitLoss = profitLoss.ToString(" 0.00") + " %";
							openHoldingTime = time + " days";
						}
						break;
					}
				}
			}

			//OpenPosition.Content = openPosition;
			//OpenEntryDate.Content = openEntryDate;
			//OpenEntryPrice.Content = openEntryPrice;
			//OpenProfitLoss.Content = openProfitLoss;
			//OpenHoldingTime.Content = openHoldingTime;
			//ClosedPosition.Content = closedPosition;
			//ClosedEntryDate.Content = closedEntryDate;
			//ClosedEntryPrice.Content = closedEntryPrice;
			//ClosedExitDate.Content = closedExitDate;
			//ClosedExitPrice.Content = closedExitPrice;
			//ClosedProfitLoss.Content = closedProfitLoss;
			//ClosedHoldingTime.Content = closedHoldingTime;
		}


		private void MonthlySummaryChart_MouseDown(object sender, MouseButtonEventArgs e)
		{
			Canvas canvas = sender as Canvas;
			Point point = e.GetPosition(canvas);
			_xClick = point.X;
			_entryDate = "";
			update();
		}


		private int getDayCount(string entryDate, string exitDate)
		{
			int dayCount = 0;

			DateTime date = DateTime.Parse(entryDate, System.Globalization.CultureInfo.CurrentCulture, System.Globalization.DateTimeStyles.AssumeUniversal);
			DateTime date1 = new DateTime(date.Year, date.Month, date.Day);

			bool bp = false;
			DateTime date2 = DateTime.UtcNow;
			date = DateTime.Parse(exitDate, System.Globalization.CultureInfo.CurrentCulture, System.Globalization.DateTimeStyles.AssumeUniversal);
			if (date.Year > 1500 && date.Year < 2500)
			{
				date2 = new DateTime(date.Year, date.Month, date.Day);
			}
			else
			{
				bp = true;
			}

			TimeSpan span = date2 - date1;
			dayCount = (int)span.TotalDays;
			return dayCount;
		}

		private double getCurrentPrice(string symbol)
		{
			// RT tick price from PRICE_LAST_RT subscription — 3-minute TTL prevents a lapsed
			// subscription from masking fresher bar-cache closes indefinitely.
			lock (_rtPrices)
			{
				if (_rtPrices.TryGetValue(symbol, out var rtEntry)
					&& rtEntry.price > 0
					&& (DateTime.Now - rtEntry.ts).TotalMinutes < 3)
					return rtEntry.price;
			}

			// Last price computed by drawPortfolioGrid — same as what Cur Px column shows
			lock (_lastKnownPrices)
			{
				if (_lastKnownPrices.TryGetValue(symbol, out double cached) && cached > 0)
					return cached;
			}

			// Weekly bar close
			var weeklyBars = _barCache.GetBars(symbol, "Weekly", 0, 5);
			if (weeklyBars != null && weeklyBars.Count > 0)
			{
				var latestBar = weeklyBars.LastOrDefault(b => b.Close > 0);
				if (latestBar != null && latestBar.Close > 0) return latestBar.Close;
			}

			// Daily bar close
			var dailyBars = _barCache.GetBars(symbol, "Daily", 0, 5);
			if (dailyBars != null && dailyBars.Count > 0)
			{
				var latestBar = dailyBars.LastOrDefault(b => b.Close > 0);
				if (latestBar != null && latestBar.Close > 0) return latestBar.Close;
			}

			// Trade.Manager last resort
			double tmPrice = Trade.Manager.GetLastPrice(symbol);
			if (!double.IsNaN(tmPrice) && tmPrice > 0) return tmPrice;

			return double.NaN;
		}


		private double getPrice(List<Bar> bars, DateTime time)
		{
			double price = double.NaN;

			var index = bars.Count - 1;

			for (var ii = bars.Count - 1; ii >= 0 && bars[ii].Time > time; ii--)
			{
				index--;
			}

			price = (index >= 0) ? bars[index].Close : double.NaN;
			if (double.IsNaN(price) && index > 0)
			{
				price = bars[index - 1].Close;
			}

			return price;
		}

		private double getPrice(string symbol, double price, DateTime date)
		{
			if (double.IsNaN(price))
			{
				List<Bar> bars = _barCache.GetBars(symbol, "Daily", 0, 500);

				if (date == default)
				{
					var index = bars.FindLastIndex(x => !double.IsNaN(x.Close));
					if (index >= 0)
					{
						price = bars[index].Close;
					}
				}
				else
				{
					foreach (var bar in bars)
					{
						if (bar.Time >= date)
						{
							price = bar.Close;
							break;
						}
					}
				}
			}
			return price;
		}

		public static void Shutdown()
		{
			if (_ic != null)
			{
				_ic.Close();
			}
		}

		public void Close()
		{
			Trade.Manager.TradeEvent -= new TradeEventHandler(TradeEvent);  // event
			Trade.Manager.NewPositions -= new NewPositionEventHandler(new_Positions);
			_portfolio1.PortfolioChanged -= new PortfolioChangedEventHandler(portfolio1Changed);
			_portfolioTimer.Tick -= new EventHandler(Timer_tick);

			//List<Alert> alerts = Alert.Manager.Alerts;
			//foreach (Alert alert in alerts)
			//{
			//    alert.FlashEvent -= new FlashEventHandler(alert_FlashEvent);
			//}

			_portfolio1.Close();
			_filterPortfolio.Close();

			//if  (_accuracyView != null)
			//{
			//    _accuracyView.Close();
			//}

			_barCache.Clear();

			setInfo();
		}

		//private string getTextFromImage(string fileName)
		//{
		//    string text = "";
		//    bool ok = true;
		//    System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(fileName);
		//    int width = bitmap.Width;
		//    int height = bitmap.Height;
		//    for (int col = 0; col < width && ok; col++)
		//    {
		//        for (int row = 0; row < height && ok; row++)
		//        {
		//            System.Drawing.Color color = bitmap.GetPixel(col, row);
		//            string[] value = new string[4];
		//            value[0] = Char.ConvertFromUtf32(color.A);
		//            value[1] = Char.ConvertFromUtf32(color.R);
		//            value[2] = Char.ConvertFromUtf32(color.G);
		//            value[3] = Char.ConvertFromUtf32(color.B);
		//            for (int ii = 0; ii < 4 && ok; ii++)
		//            {
		//                if (value[ii] != "\0")
		//                {
		//                    text += value[ii];
		//                }
		//                else
		//                {
		//                    ok = false;
		//                }
		//            }
		//        }
		//    }
		//    return text;
		//}

		class SummaryInfo
		{
			public double _portfolioBalance = 0;
			public double _yearToDatePercent = 0;
			public double _yearToDateDollar = 0;
			public double _compoundedReturn = 0;
			public double _portfolioAlpha = 0;
			public double _monthlyReturnPercent = 0;
			public double _monthlyReturnDollar = 0;
			public double _monthlyAlpha = 0;
			public double _cash = 0;
			public double _longs = 0;
			public double _shorts = 0;

			public SummaryInfo(double portfolioBalance, double yearToDatePercent, double yearToDateDollar, double compoundedReturn, double portfolioAlpha, double monthlyReturnPercent, double monthlyReturnDollar, double monthlyAlpha, double cash, double longs, double shorts)
			{
				_portfolioBalance = portfolioBalance;
				_yearToDatePercent = yearToDatePercent;
				_yearToDateDollar = yearToDateDollar;
				_compoundedReturn = compoundedReturn;
				_portfolioAlpha = portfolioAlpha;
				_monthlyReturnPercent = monthlyReturnPercent;
				_monthlyReturnDollar = monthlyReturnDollar;
				_monthlyAlpha = monthlyAlpha;
				_cash = cash;
				_longs = longs;
				_shorts = shorts;
			}

			public override string ToString()
			{
				var sb = new StringBuilder();
				sb.Append(_portfolioBalance.ToString());
				sb.Append(",");
				sb.Append(_yearToDatePercent);
				sb.Append(",");
				sb.Append(_yearToDateDollar);
				sb.Append(",");
				sb.Append(_compoundedReturn);
				sb.Append(",");
				sb.Append(_portfolioAlpha);
				sb.Append(",");
				sb.Append(_monthlyReturnPercent);
				sb.Append(",");
				sb.Append(_monthlyReturnDollar);
				sb.Append(",");
				sb.Append(_monthlyAlpha);
				sb.Append(",");
				sb.Append(_cash);
				sb.Append(",");
				sb.Append(_longs);
				sb.Append(",");
				sb.Append(_shorts);

				return sb.ToString();
			}
		}

		private Dictionary<string, List<double>> getRawFactors(Model model, int index)
		{
			Dictionary<string, List<double>> rawFactors = new Dictionary<string, List<double>>(); // keyed by fieldName:ticker

			int factorCount = model.FactorNames.Count;

			List<string> tickers = getTickers(model);
			var tickerCount = tickers.Count;

			for (var ii = 0; ii < factorCount; ii++)
			{
				var factorName = model.FactorNames[ii];
				string fieldName = Conditions.GetFieldNameFromDescription(factorName);

				rawFactors[fieldName] = new List<double>();

				for (var jj = 0; jj < tickerCount; jj++)
				{
					string ticker = tickers[jj];

					string key = fieldName + ":" + ticker;
					Series factorSeries = null;
					double factor = double.NaN;
					if (_factorInputs.TryGetValue(key, out factorSeries))
					{
						int count = factorSeries.Count;
						int idx = (index == int.MaxValue) ? count - 1 : index;
						factor = (idx >= 0 && idx < count) ? factorSeries[idx] : double.NaN;
					}
					rawFactors[fieldName].Add(factor);
				}
			}
			return rawFactors;
		}

		private double getMean(List<double> input)
		{
			double sum = 0;
			int cnt = 0;
			foreach (double value in input)
			{
				if (!double.IsNaN(value))
				{
					cnt++;
					sum += value;
				}
			}

			double output = (cnt == 0) ? double.NaN : sum / cnt;
			return output;
		}

		private double getStandardDeviation(List<double> input)
		{
			double output = double.NaN;

			double mean = getMean(input);

			if (!double.IsNaN(mean))
			{

				// Calculate the total for the standard deviation
				double bigSum = 0;
				for (int i = 0; i < input.Count; i++)
				{
					var value = input[i];
					if (!double.IsNaN(value))
					{
						bigSum += Math.Pow(value - mean, 2);
					}
				}

				// Now we can calculate the standard deviation
				output = Math.Sqrt(bigSum / (input.Count - 1));
			}

			return output;
		}

		private Dictionary<string, List<double>> getNormalizedFactors(Dictionary<string, List<double>> rawFactors)
		{
			Dictionary<string, List<double>> normFactors = new Dictionary<string, List<double>>();
			foreach (var kvp in rawFactors)
			{
				var fieldName = kvp.Key;
				var values = kvp.Value;

				double mean = getMean(values);
				double stdDev = getStandardDeviation(values);
				normFactors[fieldName] = new List<double>();

				if (!double.IsNaN(stdDev))
				{
					for (int i = 0; i < values.Count; i++)
					{
						var value = values[i];
						double normValue = (!double.IsNaN(value)) ? (value - mean) / stdDev : value;
						normFactors[fieldName].Add(normValue);
					}
				}
				else
				{
					for (int i = 0; i < values.Count; i++)
					{
						normFactors[fieldName].Add(double.NaN);
					}
				}
			}
			return normFactors;
		}

		private List<string> getTickers(Model model)
		{
			List<string> tickers = new List<string>();
			if (model.Symbols != null)
			{
				foreach (var symbol in model.Symbols)
				{
					tickers.Add(symbol.Ticker);
				}
			}
			return tickers;
		}

		private void AODGroupsSetup_MouseDown(object sender, MouseButtonEventArgs e)
		{
			if (Level1Panel.Visibility == Visibility.Collapsed)
			{
				hideView();
				hideNavigation();

				Menus.Visibility = Visibility.Visible;

				Level1Panel.Visibility = Visibility.Visible;
				BenchmarkMenu1.Visibility = Visibility.Visible;

				MLIntervalsMenu.Visibility = Visibility.Collapsed;

				RankingMenu.Visibility = Visibility.Collapsed;
				RebalanceMenu.Visibility = Visibility.Collapsed;
				AlignmentIntervalMenu.Visibility = Visibility.Collapsed;
				AutoMLIntervalMenu.Visibility = Visibility.Collapsed;
				ATMStrategyMenu.Visibility = Visibility.Collapsed;
				FeaturesMenu.Visibility = Visibility.Collapsed;

				ATMTimingMenus.Visibility = Visibility.Collapsed;
				ScenarioMenu1.Visibility = Visibility.Collapsed;
				PortfolioSettings.Visibility = Visibility.Collapsed;


				MLRankMenu.Visibility = Visibility.Collapsed;
				MLIterationMenu.Visibility = Visibility.Collapsed;

				Level2Panel.Visibility = Visibility.Visible;
				Level3Panel.Visibility = Visibility.Visible;
				Level4Panel.Visibility = Visibility.Visible;
				SubgroupMenuScroller1.Visibility = Visibility.Visible;
				Level5Panel.Visibility = Visibility.Visible;
				IndustryMenuScroller1.Visibility = Visibility.Visible;

				SubgroupActionMenu1.Visibility = Visibility.Collapsed;
				SubgroupActionMenu2.Visibility = Visibility.Collapsed;

				FilterMenu.Visibility = Visibility.Collapsed;
				InputMenu.Visibility = Visibility.Collapsed;
				PortfolioSettings.Visibility = Visibility.Collapsed;

				SetupNav.Visibility = Visibility.Collapsed;

				drawGroupSetup();
			}
			else
			{
				showView();
			}
		}

		private void drawGroupSetup()
		{
			Level1Panel.Children.Clear();
			BrushConverter bc = new BrushConverter();
			Brush borderBrush = (Brush)bc.ConvertFrom("#FF124b72");

			// Create main grid
			Grid mainGrid = new Grid
			{
				HorizontalAlignment = HorizontalAlignment.Left,
				VerticalAlignment = VerticalAlignment.Center,
				Margin = new Thickness(0, 2, 2, 2)
			};

			mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) });
			mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(35) });
			mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(100, GridUnitType.Star) });
			mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) });

			// Instruction text
			TextBlock instructionText = new TextBlock
			{
				Background = Brushes.Black,
				Foreground = Brushes.PaleGreen,
				Text = "To change a group name, highlight it, then type your new name.",
				FontSize = 11
			};
			mainGrid.Children.Add(instructionText);

			// Add New Page button
			Button addPageButton = CreateStyledButton("Add Group", borderBrush);
			addPageButton.SetValue(Grid.RowProperty, 1);
			addPageButton.Click += OnAddNewGroup;
			addPageButton.Template = CreateHoverTemplate(borderBrush); // ✅ Apply hover template
			mainGrid.Children.Add(addPageButton);

			// ScrollViewer for group entries
			ScrollViewer scrollViewer = new ScrollViewer
			{
				VerticalScrollBarVisibility = ScrollBarVisibility.Auto
			};
			scrollViewer.SetValue(Grid.RowProperty, 2);
			StackPanel groupPanel = new StackPanel { Orientation = Orientation.Vertical };
			scrollViewer.Content = groupPanel;
			mainGrid.Children.Add(scrollViewer);

			// Close button
			Button closeButton = CreateStyledButton("Close", borderBrush);
			closeButton.SetValue(Grid.RowProperty, 3);
			closeButton.Click += OnCloseGroupEdit;
			closeButton.Template = CreateHoverTemplate(borderBrush);
			mainGrid.Children.Add(closeButton);

			// Add group entries
			var model = getModel();
			foreach (var group in model.Groups)
			{
				StackPanel groupEntryPanel = new StackPanel { Orientation = Orientation.Horizontal };

				TextBox groupNameBox = new TextBox
				{
					Height = 25,
					Width = 230,
					BorderBrush = borderBrush,
					BorderThickness = new Thickness(1),
					Background = Brushes.Black,
					Foreground = Brushes.WhiteSmoke,
					FontSize = 10,
					VerticalContentAlignment = VerticalAlignment.Center,
					Margin = new Thickness(0),
					Padding = new Thickness(0),
					Text = group.Name,
					Tag = group.Id
				};
				groupNameBox.TextChanged += OnGroupNameChanged;
				groupEntryPanel.Children.Add(groupNameBox);

				Button deleteButton = CreateStyledButton("Delete", borderBrush);
				deleteButton.Width = 75;
				deleteButton.Tag = group.Id;
				deleteButton.Click += OnGroupDelete;
				deleteButton.Template = CreateHoverTemplate(borderBrush);
				groupEntryPanel.Children.Add(deleteButton);

				groupPanel.Children.Add(groupEntryPanel);
			}

			Level1Panel.Children.Add(mainGrid);
		}

		// Helper to create a styled button
		private Button CreateStyledButton(string content, Brush borderBrush)
		{
			return new Button
			{
				Height = 25,
				BorderBrush = borderBrush,
				BorderThickness = new Thickness(1),
				Background = Brushes.Black,
				Foreground = Brushes.WhiteSmoke,
				HorizontalContentAlignment = HorizontalAlignment.Center,
				VerticalContentAlignment = VerticalAlignment.Center,
				FontSize = 11,
				Margin = new Thickness(0),
				Padding = new Thickness(0),
				Content = content
			};
		}

		// Helper to create a hover template
		private ControlTemplate CreateHoverTemplate(Brush borderBrush)
		{
			var template = new ControlTemplate(typeof(Button));

			var border = new FrameworkElementFactory(typeof(Border));
			border.Name = "ButtonBorder";
			border.SetValue(Border.BackgroundProperty, Brushes.Black);
			border.SetValue(Border.BorderBrushProperty, borderBrush);
			border.SetValue(Border.BorderThicknessProperty, new Thickness(1));

			var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
			contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
			contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
			contentPresenter.SetValue(TextElement.ForegroundProperty, Brushes.WhiteSmoke);
			contentPresenter.SetValue(TextElement.FontSizeProperty, 11.0);

			border.AppendChild(contentPresenter);
			template.VisualTree = border;

			var mouseOverTrigger = new Trigger
			{
				Property = UIElement.IsMouseOverProperty,
				Value = true
			};

			// ✅ Replace DodgerBlue with #124b72
			Brush hoverBrush = (Brush)new BrushConverter().ConvertFrom("#FF124B72");
			mouseOverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, hoverBrush, "ButtonBorder"));
			template.Triggers.Add(mouseOverTrigger);

			return template;
		}

		private void OnGroupNameChanged(object sender, TextChangedEventArgs e)
		{
			var textBox = sender as TextBox;
			if (textBox != null)
			{
				var name = textBox.Text as string;
				var id = textBox.Tag as string;
				var model = getModel();
				var groups = model.Groups;
				var index = groups.FindIndex(g => g.Id == id);
				if (index >= 0)
				{
					groups[index].Name = name;
				}
			}
		}

		private void OnGroupDelete(object sender, RoutedEventArgs e)
		{
			var button = sender as Button;
			if (button != null)
			{
				var id = button.Tag as string;
				var model = getModel();
				var groups = model.Groups;
				var index = groups.FindIndex(g => g.Id == id);

				var new_group = _g;
				if (new_group == index)
				{
					if (--new_group < 0) new_group = 0;
				}
				else if (new_group >= model.Groups.Count - 1)
				{
					if (--new_group < 0) new_group = 0;
				}

				if (new_group != _g)
				{
					_g = new_group;
					_portfolio1.Clear();
				}

				model.Groups.RemoveAt(index);
				drawGroupSetup();
				OnPropertyChanged("ATMConditionsEnter");
				OnPropertyChanged("ATMConditionsExit");
			}
		}

		private void OnAddNewGroup(object sender, RoutedEventArgs e)
		{
			var model = getModel();
			var groups = model.Groups;
			var index = groups.Where(g => g.Name.StartsWith("Group ")).ToList().Count() + 1;
			var name = "Group " + index;
			var group = new ModelGroup();
			group.Name = name;
			model.Groups.Add(group);
			drawGroupSetup();
		}

		private void OnCloseGroupEdit(object sender, RoutedEventArgs e)
		{
			showView();
			userFactorModel_to_ui(_selectedUserFactorModel);
		}

		// Event handlers for label clicks

		#region Dynamic Sector/Industry/SubIndustry Switching


		private void LabelSectors_MouseDown(object sender, MouseButtonEventArgs e)
		{
			_activeLabel = "Sector";
			HighlightActiveLabel(LabelSectors);
			LoadTiles(sectorPercents);
		}

		private void LabelIndustry_MouseDown(object sender, MouseButtonEventArgs e)
		{
			_activeLabel = "Industry";
			HighlightActiveLabel(LabelIndustry);
			LoadTiles(industryPercents);
		}

		private void LabelSubIndustry_MouseDown(object sender, MouseButtonEventArgs e)
		{
			_activeLabel = "SubIndustry";
			HighlightActiveLabel(LabelSubIndustry);
			LoadTiles(subIndustryPercents);
		}

		private void Portfolio_MouseEnter(object sender, MouseEventArgs e)
		{
			if (sender is Label label && label.Tag?.ToString() != _activeLabel)
				label.Foreground = (Brush)new BrushConverter().ConvertFrom("#00ccff");
		}
		private void Portfolio_MouseLeave2(object sender, MouseEventArgs e)
		{
			if (sender is Label label && label.Tag?.ToString() != _activeLabel)
				label.Foreground = Brushes.White;
		}
		private void HighlightActiveLabel(Label active)
		{
			foreach (var l in new[] { LabelSectors, LabelIndustry, LabelSubIndustry })
				l.Foreground = Brushes.White;

			active.Foreground = (Brush)new BrushConverter().ConvertFrom("#00ccff");
		}

		#endregion

		private void PortfolioBuilder_Loaded(object sender, RoutedEventArgs e)
		{
			HighlightActiveLabel(LabelSectors);
			LoadTiles(sectorPercents);
			ApplyEntitlementMargins();
		}

		private void ApplyEntitlementMargins()
		{
			var role = ATMML.Auth.AuthContext.Current.IsAuthenticated
				? ATMML.Auth.AuthContext.Current.User?.Role ?? ATMML.Auth.UserRole.Viewer
				: ATMML.Auth.UserRole.Viewer;

			var isAdmin      = role == ATMML.Auth.UserRole.Admin;
			var isPM         = role == ATMML.Auth.UserRole.PortfolioManager;
			var isTrader     = role == ATMML.Auth.UserRole.Trader;
			var isCompliance = role == ATMML.Auth.UserRole.Compliance;
			var isViewer     = role == ATMML.Auth.UserRole.Viewer;

			var V = Visibility.Visible;
			var C = Visibility.Collapsed;
			var defaultMargin = new System.Windows.Thickness(10, -50, 10, 4);
			var tightMargin   = new System.Windows.Thickness(10, 0, 10, 4);

			if (isViewer)
			{
				if (TISetup        != null) TISetup.Visibility        = C;
				if (TIPositions22 != null) TIPositions22.Visibility   = C;
				if (PerformanceGrid    != null) PerformanceGrid.Visibility    = C;
				if (PerformanceSideNav != null) PerformanceSideNav.Visibility = C;
				return;
			}

			if (isCompliance)
			{
				if (TISetup       != null) TISetup.Visibility      = C;
				if (TIPositions22 != null) TIPositions22.Visibility = C;
				if (PerformanceGrid != null) PerformanceGrid.Visibility = V;
				if (PerformanceSideNav != null) PerformanceSideNav.Visibility = C;
				if (PortSetup3 != null) PortSetup3.Visibility = C;
				if (OrderMgm3  != null) OrderMgm3.Visibility  = C;
				if (PortPerf3  != null) { PortPerf3.Visibility = V; PortPerf3.Margin = tightMargin; }
				return;
			}

			if (isPM || isTrader)
			{
				if (TISetup != null) TISetup.Visibility = C;
				// Show the Order Management grids so TIPositions22 is visible
				if (RebalanceChartGrid != null) RebalanceChartGrid.Visibility = V;
				if (RebalanceGrid      != null) RebalanceGrid.Visibility      = V;
				if (TIPositions22 != null) TIPositions22.Visibility = V;
				if (PerformanceGrid != null) PerformanceGrid.Visibility = C;
				if (PortSetup2 != null) PortSetup2.Visibility = C;
				if (OrderMgm2  != null) { OrderMgm2.Visibility = V; OrderMgm2.Margin = tightMargin; }
				if (PortPerf2  != null) PortPerf2.Visibility = V;
				if (PortSetup3 != null) PortSetup3.Visibility = C;
				if (OrderMgm3  != null) { OrderMgm3.Visibility = V; OrderMgm3.Margin = tightMargin; }
				if (PortPerf3  != null) PortPerf3.Visibility = V;
				return;
			}

			if (isAdmin)
			{
				if (TISetup    != null) TISetup.Visibility    = V;
				if (PortSetup1 != null) PortSetup1.Visibility = V;
				if (OrderMgm1  != null) { OrderMgm1.Visibility = V; OrderMgm1.Margin = tightMargin; }
				if (PortPerf1  != null) PortPerf1.Visibility = V;
				if (PortSetup2 != null) PortSetup2.Visibility = V;
				if (OrderMgm2  != null) { OrderMgm2.Visibility = V; OrderMgm2.Margin = defaultMargin; }
				if (PortPerf2  != null) PortPerf2.Visibility = V;
				if (PortSetup3 != null) PortSetup3.Visibility = V;
				if (OrderMgm3  != null) { OrderMgm3.Visibility = V; OrderMgm3.Margin = defaultMargin; }
				if (PortPerf3  != null) PortPerf3.Visibility = V;
			}
		}

		private void LoadGrid(Dictionary<string, string> data) => LoadTiles(data);
		private void LoadGrid(string _, Dictionary<string, string> data) => LoadTiles(data);

		#region Dynamic Grid Builder


		#endregion

		// Tile loader
		private void LoadTiles(Dictionary<string, string> data)
		{
			if (TilesList == null || data == null) return;

			var rows = data.OrderByDescending(x => x.Value.Length > 0 ? double.Parse(x.Value.Replace("%", "")) : 0).Select(kv => new NameValueRow
			{
				Name = kv.Key,
				Value = kv.Value
			}).ToList();

			TilesList.ItemsSource = rows;
		}


		private void PortfolioWhite_MouseEnter(object sender, MouseEventArgs e)
		{
			Control label = sender as Control;
			label.Foreground = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));
		}

		private void Portfolio_MouseLeave(object sender, MouseEventArgs e)
		{
			Control label = sender as Control;
			label.Foreground = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));
		}

		private void PortfolioBlue_MouseLeave2(object sender, MouseEventArgs e)
		{
			Control label = sender as Control;
			label.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xcc, 0xff));
			//highlightStats();
			//highlightSectors();
		}
		private void Portfolio_MouseLeave_SelectedModel(object sender, MouseEventArgs e)
		{
			var control = sender as Control;
			var name = control.Tag as string;
			var active = name == _selectedUserFactorModel;
			control.Foreground = active ? new SolidColorBrush(Color.FromRgb(0x00, 0xcc, 0xff)) : Brushes.White;
		}

		private void Group_MouseLeave(object sender, MouseEventArgs e)
		{
			var label = sender as Label;
			if (label != null)
			{
				var name = label.Content as string;
				var model = getModel();
				var index = model.Groups.FindIndex(group => group.Name == name);
				var active = _g == index;
				label.Foreground = active ? new SolidColorBrush(Color.FromRgb(0x00, 0xcc, 0xff)) : Brushes.White;
			}
		}


		private void OnLongOnly(object sender, RoutedEventArgs e)
		{
			var m = getModel();
			if (m != null)
			{
				m.Groups[_g].Direction = 1;
			}
		}

		private void OnShortOnly(object sender, RoutedEventArgs e)
		{
			var m = getModel();
			if (m != null)
			{
				m.Groups[_g].Direction = -1;
			}
		}

		private void OnLongShort(object sender, RoutedEventArgs e)
		{
			var m = getModel();
			if (m != null)
			{
				m.Groups[_g].Direction = 0;
			}
		}
		private void OnMTNToff(object sender, RoutedEventArgs e)
		{
			var m = getModel();
			if (m != null)
			{
				//m.Groups[g].OnMTNToff = 1;
			}
		}
		private void OnMTNT(object sender, RoutedEventArgs e)
		{
			var m = getModel();
			if (m != null)
			{
				//m.Groups[g].OnMTNT = 1;
			}
		}
		private void OnNTPlus1(object sender, RoutedEventArgs e)
		{
			var m = getModel();
			if (m != null)
			{
				//m.Groups[g].OnNTPlus1 = 1;
			}
		}
		private void OnMTSecNT(object sender, RoutedEventArgs e)
		{
			var m = getModel();
			if (m != null)
			{
				//m.Groups[g].OnMTSecNT = 1;
			}
		}
		private void OnMTPoff(object sender, RoutedEventArgs e)
		{
			var m = getModel();
			if (m != null)
			{
				//m.Groups[g].OnMTPoff = 1;
			}
		}
		private void OnMTP(object sender, RoutedEventArgs e)
		{
			var m = getModel();
			if (m != null)
			{
				//m.Groups[g].OnMTP = 1;
			}
		}
		private void OnPPlus1(object sender, RoutedEventArgs e)
		{
			var m = getModel();
			if (m != null)
			{
				//m.Groups[g].OnPPlus1 = 1;
			}
		}
		private void OnMTSecP(object sender, RoutedEventArgs e)
		{
			var m = getModel();
			if (m != null)
			{
				//m.Groups[g].OnMTSecP = 1;
			}
		}
		private void OnFtFtoff(object sender, RoutedEventArgs e)
		{
			var m = getModel();
			if (m != null)
			{
				//m.Groups[g].OnFtFtoff = 1;
			}
		}
		private void OnMTFT(object sender, RoutedEventArgs e)
		{
			var m = getModel();
			if (m != null)
			{
				//m.Groups[g].OnMTFT = 1;
			}
		}
		private void OnFTPlus1(object sender, RoutedEventArgs e)
		{
			var m = getModel();
			if (m != null)
			{
				//m.Groups[g].OnFTPlus1 = 1;
			}
		}
		private void OnMTSecFT(object sender, RoutedEventArgs e)
		{
			var m = getModel();
			if (m != null)
			{
				//m.Groups[g].OnMTSecFT = 1;
			}
		}

		private void OnSTFTChecked(object sender, RoutedEventArgs e)
		{
			var cb = sender as CheckBox;
			var m = getModel();
			if (m != null)
			{
				m.Groups[_g].UseSTFT = cb.IsChecked == true;
			}
		}

		private void OnSTSTChecked(object sender, RoutedEventArgs e)
		{
			var cb = sender as CheckBox;
			var m = getModel();
			if (m != null)
			{
				m.Groups[_g].UseSTST = cb.IsChecked == true;
			}
		}

		private void OnSTSCChecked(object sender, RoutedEventArgs e)
		{
			var cb = sender as CheckBox;
			var m = getModel();
			if (m != null)
			{
				m.Groups[_g].UseSTSC = cb.IsChecked == true;
			}
		}
		private void OnNewTrendChecked(object sender, RoutedEventArgs e)
		{
			var cb = sender as CheckBox;
			var m = getModel();
			if (m != null)
			{
				m.Groups[_g].UseNewTrend = cb.IsChecked == true;
			}
		}

		private void OnNewTrendTextChanged(object sender, RoutedEventArgs e)
		{
			var tb = sender as TextBox;
			var m = getModel();
			if (m != null)
			{
				int value = 0;
				if (int.TryParse(tb.Text, out value))
				{
					m.Groups[_g].NewTrendUnit = value;
				}
			}
		}
		private void OnUseMTChecked(object sender, RoutedEventArgs e)
		{
			var cb = sender as CheckBox;
			var m = getModel();
			if (m != null)
			{
				var vis = cb.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
				UseMTVisChange(cb.IsChecked == true);
				m.Groups[_g].UseMT = cb.IsChecked == true;
			}
		}
		private void UseMTVisChange(bool visible)
		{
			var vis = visible ? Visibility.Visible : Visibility.Collapsed;
			//UseMTNToff.Visibility = vis;
			//UseMTNT.Visibility = vis;
			//UseNTPlus1.Visibility = vis;
			//UseMTSecNT.Visibility = vis;
			//UseMTPoff.Visibility = vis;
			//UseMTP.Visibility = vis;
			//UsePPlus1.Visibility = vis;
			//UseMTSecP.Visibility = vis;
			//UseFtFtoff.Visibility = vis;
			//UseMTFT.Visibility = vis;
			//UseFTPlus1.Visibility = vis;
			//UseMTSecFT.Visibility = vis;
		}

		private void OnUseSTChecked(object sender, RoutedEventArgs e)
		{
			var cb = sender as CheckBox;
			var m = getModel();
			if (m != null)
			{
				var vis = cb.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
				UseSTVisChange(cb.IsChecked == true);
				m.Groups[_g].UseST = cb.IsChecked == true;
			}
		}

		private void UseSTVisChange(bool visible)
		{
			var vis = visible ? Visibility.Visible : Visibility.Collapsed;
			//UseSTFT.Visibility = vis;
			//UseSTST.Visibility = vis;
			//UseSTSC.Visibility = vis;
			//UseSTTSB.Visibility = vis;
		}
		private void OnUseEntriesChecked(object sender, RoutedEventArgs e)
		{
			var cb = sender as CheckBox;
			var m = getModel();
			if (m != null)
			{
				var vis = cb.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
				UseENTERVisChange(cb.IsChecked == true);
				m.Groups[_g].UseATMStrategies = cb.IsChecked == true;
			}
		}

		private void UseENTERVisChange(bool visible)
		{
			var vis = visible ? Visibility.Visible : Visibility.Collapsed;
			NewTrendEntry.Visibility = vis;
			NewTrendUnit.Visibility = vis;
			PressureEntry.Visibility = vis;
			PressureUnit.Visibility = vis;
			AddEntry.Visibility = vis;
			AddUnit.Visibility = vis;
			RetraceExit.Visibility = vis;
			RetracePercent.Visibility = vis;
			ExhaustionExit.Visibility = vis;
			ExhaustionPercent.Visibility = vis;
			FreqPeriodCheckbox.Visibility = vis;
			FreqPeriod.Visibility = vis;
			//UseATMStrategiesCB.Visibility = vis;
			UseConviction.Visibility = vis;
			ConvictionPercent.Visibility = vis;
			//ApplyAlignmentInterval.Visibility = vis;
			//AlignmentInterval.Visibility = vis;
			//UseATMStrategies.Visibility = vis;
			//ATMStrategy.Visibility = vis;
			//UseATRRisk.Visibility = vis;
			//XMulti.Visibility = vis;
			//ATRRiskFactor.Visibility = vis;
			//XPeriod.Visibility = vis;
			//ATRRiskPeriod.Visibility = vis;
			//UseTradeRisk.Visibility = vis;
			//TradeRiskPercent.Visibility = vis;
			UseNTObOs.Visibility = vis;
			//UseFTST.Visibility = vis;
			UsePExit.Visibility = vis;
			PExitPercent.Visibility = vis;
			UseTwoBar.Visibility = vis;
			TwoBarPercent.Visibility = vis;
			UseFTRoc.Visibility = vis;
			FTRocPercent.Visibility = vis;
			UseFTSTdiv.Visibility = vis;
			FtStdivPeriod.Visibility = vis;
			UsePercentExit.Visibility = vis;
			ExitPercentage.Visibility = vis;
			PercentBarsToExit.Visibility = vis;
			UseTimeExit.Visibility = vis;
			BarsToExit.Visibility = vis;
			PercentBarsToExit.Visibility = vis;
		}

		private void OnUseMoneyMgtChecked(object sender, RoutedEventArgs e)
		{
			var cb = sender as CheckBox;
			var m = getModel();
			if (m != null)
			{
				var vis = cb.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
				UseMoneyMgtVisChange(cb.IsChecked == true);
				m.ApplyMoneyMgt = cb.IsChecked == true;
			}
		}

		private void UseMoneyMgtVisChange(bool visible)
		{
			var vis = visible ? Visibility.Visible : Visibility.Visible;
			UsePortRiskL1.Visibility = vis;
			//PortRiskPercentL1.Visibility = vis;
			//UsePortRiskL2.Visibility = vis;
			//PortRiskPercentL2.Visibility = vis;
			//UsePortRiskL2.Visibility = vis;
			//PortRiskPercentL2.Visibility = vis;
			//UsePortRiskL3.Visibility = vis;
			//PortRiskPercentL3.Visibility = vis;
			//RampUpL1.Visibility = vis;
			//RampUpL1Percent.Visibility = vis;
			//RampUpL2.Visibility = vis;
			//RampUpL2Percent.Visibility = vis;
			//RampUpL3.Visibility = vis;
			//RampUpL3Percent.Visibility = vis;
			//UseCoolDown.Visibility = vis;
			//CoolDownPeriod.Visibility = vis;
			//UseHedge.Visibility = vis;
			PosX.Visibility = vis;
			UseATRRisk.Visibility = vis;
			XMulti.Visibility = vis;
			ATRRiskFactor.Visibility = vis;
			XPeriod.Visibility = vis;
			ATRRiskPeriod.Visibility = vis;
			//UseTradeRisk.Visibility = vis;
			//TradeRiskPercent.Visibility = vis;
		}



		bool _useUserFactorModel;
		bool _useAzure = true;

		private void Performance_MouseDown(object sender, MouseEventArgs e)
		{
			//if (_accuracyView != null)
			//{
			//    _selectedUserFactorModel = _accuracyView.Model.Name;
			//    _accuracyView.Close();
			//    _accuracyView = null;
			//}

			hideNavigation();

			PositionsChartBorder.Visibility = Visibility.Visible;
			TotalReturnChartBorder.Visibility = Visibility.Visible;

			TISetup.Visibility = Visibility.Collapsed;
			//TIStats.Visibility = Visibility.Visible;
			//TIPositions.Visibility = Visibility.Collapsed;

			TIOpenPL.Visibility = Visibility.Collapsed;
			TIAllocations.Visibility = Visibility.Collapsed;

			RiskTable.Visibility = Visibility.Collapsed;
			RecoveryTable.Visibility = Visibility.Collapsed;

			UserFactorModelGrid.Visibility = Visibility.Collapsed;

			PerformanceGrid.Visibility = Visibility.Visible;
			ReturnTable.Visibility = Visibility.Visible;
			PerformanceSideNav.Visibility = Visibility.Visible;

			ATMStrategyOverlay.Visibility = Visibility.Collapsed;

			RebalanceChartGrid.Visibility = Visibility.Collapsed;
			RebalanceGrid.Visibility = Visibility.Collapsed;
			RebalanceSideNav.Visibility = Visibility.Collapsed;

			AllocationGrid.Visibility = Visibility.Collapsed;
			AllocationSideNav.Visibility = Visibility.Collapsed;

			UserFactorModelPanel2.Visibility = _useUserFactorModel ? Visibility.Visible : Visibility.Collapsed;
			updateUserFactorModelList();
			//Model model = getModel();
			//if (isTradingModel(model.Name))
			//{
			//    if (model != null)
			//    {
			//        requestPortfolio(model.Universe);
			//    }
			//}

		}

		private void RebalanceView_MouseDown(object sender, MouseEventArgs e)
		{
			//if (_accuracyView != null)
			//{
			//    _selectedUserFactorModel = _accuracyView.Model.Name;
			//    _accuracyView.Close();
			//    _accuracyView = null;
			//}

			hideNavigation();

			showRebalanceGrid();

			setModelRadioButtons();

			updateUserFactorModelList();

			//saveUserFactorModels();

			if (_portfolioTimes.Count > 0)
			{
				var model = getModel();
				var date = _portfolioTimes.Last();
				setHoldingsCursorTime(date);
			}
		}

		private void showRebalanceGrid()
		{
			TISetup.Visibility = Visibility.Collapsed;
			//TIStats.Visibility = Visibility.Collapsed;
			//TIPositions.Visibility = Visibility.Visible;

			TIOpenPL.Visibility = Visibility.Collapsed;
			TIAllocations.Visibility = Visibility.Collapsed;

			TrainResultsTime.Visibility = Visibility.Collapsed;
			TrainResults.Visibility = Visibility.Collapsed;

			ReturnTable.Visibility = Visibility.Collapsed;
			RiskTable.Visibility = Visibility.Collapsed;
			RecoveryTable.Visibility = Visibility.Collapsed;

			UserFactorModelGrid.Visibility = Visibility.Collapsed;

			PerformanceGrid.Visibility = Visibility.Collapsed;

			PerformanceSideNav.Visibility = Visibility.Collapsed;

			ATMStrategyOverlay.Visibility = Visibility.Collapsed;

			RebalanceChartGrid.Visibility = Visibility.Visible;
			RebalanceGrid.Visibility = Visibility.Visible;
			RebalanceChartGrid.Visibility = Visibility.Visible;

			RebalanceSideNav.Visibility = Visibility.Visible;

			AllocationGrid.Visibility = Visibility.Collapsed;

			AllocationSideNav.Visibility = Visibility.Collapsed;
		}

		//private void showRebalanceGrid()
		//{
		//	TISetup.Visibility = Visibility.Collapsed;
		//	//TIStats.Visibility = Visibility.Collapsed;
		//	//TIPositions.Visibility = Visibility.Visible;

		//	TIOpenPL.Visibility = Visibility.Collapsed;
		//	TIAllocations.Visibility = Visibility.Collapsed;

		//	TrainResultsTime.Visibility = Visibility.Collapsed;
		//	TrainResults.Visibility = Visibility.Collapsed;

		//	ReturnTable.Visibility = Visibility.Collapsed;
		//	RiskTable.Visibility = Visibility.Collapsed;
		//	RecoveryTable.Visibility = Visibility.Collapsed;

		//	UserFactorModelGrid.Visibility = Visibility.Collapsed;

		//	PerformanceGrid.Visibility = Visibility.Collapsed;

		//	PerformanceSideNav.Visibility = Visibility.Collapsed;

		//	ATMStrategyOverlay.Visibility = Visibility.Collapsed;

		//	RebalanceChartGrid.Visibility = Visibility.Visible;
		//	RebalanceGrid.Visibility = Visibility.Visible;
		//	RebalanceChartGrid.Visibility = Visibility.Visible;

		//	RebalanceSideNav.Visibility = Visibility.Visible;

		//	AllocationGrid.Visibility = Visibility.Collapsed;

		//	AllocationSideNav.Visibility = Visibility.Collapsed;
		//}

		private void Accuracy_MouseDown(object sender, MouseEventArgs e)
		{
			hideNavigation();

			TISetup.Visibility = Visibility.Collapsed;
			//TIStats.Visibility = Visibility.Collapsed;
			//TIPositions.Visibility = Visibility.Collapsed;
			TIOpenPL.Visibility = Visibility.Visible;
			TIAllocations.Visibility = Visibility.Collapsed;

			ReturnTable.Visibility = Visibility.Collapsed;
			RiskTable.Visibility = Visibility.Collapsed;
			RecoveryTable.Visibility = Visibility.Collapsed;

			UserFactorModelGrid.Visibility = Visibility.Collapsed;

			PerformanceGrid.Visibility = Visibility.Collapsed;
			PerformanceSideNav.Visibility = Visibility.Collapsed;

			ATMStrategyOverlay.Visibility = Visibility.Visible;

			RebalanceChartGrid.Visibility = Visibility.Collapsed;
			RebalanceGrid.Visibility = Visibility.Collapsed;
			RebalanceSideNav.Visibility = Visibility.Collapsed;

			AllocationGrid.Visibility = Visibility.Collapsed;
			AllocationSideNav.Visibility = Visibility.Collapsed;

			Model model = getModel();

			//_accuracyView = new Accuracy(_mainView, _userFactorModels,  model);
			//ATMStrategyOverlay.Children.Clear();
			//ATMStrategyOverlay.Children.Add(_accuracyView);

			updateUserFactorModelList();

			//saveUserFactorModels();

			_update = 14;
		}

		private void Allocation_MouseDown(object sender, MouseEventArgs e)
		{

			hideNavigation();

			TISetup.Visibility = Visibility.Collapsed;
			//TIStats.Visibility = Visibility.Collapsed;
			//TIPositions.Visibility = Visibility.Collapsed;
			TIOpenPL.Visibility = Visibility.Collapsed;
			TIAllocations.Visibility = Visibility.Visible;

			SetupNav.Visibility = Visibility.Collapsed;

			ReturnTable.Visibility = Visibility.Collapsed;
			RiskTable.Visibility = Visibility.Collapsed;
			RecoveryTable.Visibility = Visibility.Collapsed;

			UserFactorModelGrid.Visibility = Visibility.Collapsed;

			PerformanceGrid.Visibility = Visibility.Collapsed;
			PerformanceSideNav.Visibility = Visibility.Collapsed;

			ATMStrategyOverlay.Visibility = Visibility.Collapsed;

			ChartGrid.Visibility = Visibility.Collapsed;
			//Grid.Visibility = Visibility.Collapsed;
			//SideNav.Visibility = Visibility.Collapsed;

			AllocationGrid.Visibility = Visibility.Visible;
			AllocationSideNav.Visibility = Visibility.Visible;

			UserFactorModelPanel2.Visibility = _useUserFactorModel ? Visibility.Visible : Visibility.Collapsed;

			updateUserFactorModelList();

			//Model model = getModel();
			//if (model != null)
			//{
			//    requestPortfolio(model.Universe);
			//}
		}

		//Accuracy _accuracyView = null;

		private BitmapSource capturePortfolio()
		{
			double dpiX = 300;
			double dpiY = 300;


			Grid grid = PortfolioGrid;
			PositionsChartBorder.Background = Brushes.Black;  // changing to a lighter color will change the graph background
			TradeOverviewChartBorder.Background = Brushes.Black;  // changing to a lighter color will change the graph background
			MonthlySummaryChartBorder.Background = Brushes.Black;   // changing to a lighter color will change the graph background
			grid.UpdateLayout();

			Visual target = grid;

			Rect bounds = VisualTreeHelper.GetDescendantBounds(target);
			RenderTargetBitmap rtb = new RenderTargetBitmap((int)(bounds.Width * dpiX / 96.0), (int)(bounds.Height * dpiY / 96.0), dpiX, dpiY, PixelFormats.Pbgra32);
			DrawingVisual dv = new DrawingVisual();
			using (DrawingContext ctx = dv.RenderOpen())
			{
				VisualBrush vb = new VisualBrush(target);
				ctx.DrawRectangle(vb, null, new Rect(new Point(), bounds.Size));
			}
			rtb.Render(dv);

			PositionsChartBorder.Background = Brushes.Black;
			TradeOverviewChartBorder.Background = Brushes.Black;
			MonthlySummaryChartBorder.Background = Brushes.Black;
			grid.UpdateLayout();

			return rtb;
		}

		//public void CopyToClipboard()
		//{
		//    BitmapSource source = capturePortfolio();
		//    Clipboard.SetData(DataFormats.Bitmap, source);
		//}

		//private void PositionsChartScrollBar_Scroll(object sender, System.Windows.Controls.Primitives.ScrollEventArgs e)
		//{
		//    //updatePositionsChart();
		//    updatePositionsText();
		//}

		private void ChangeUserFactorModelName(object sender, RoutedEventArgs e)
		{
			//string newName = UserFactorModelName.Text;

			//changeSelectedUserFactorModel(newName);
		}


		public void deleteModelData(string name)
		{
			MainView.DeleteUserData(@"portfolios\rebalance_" + name);
			MainView.DeleteUserData(@"portfolios\" + name);
			MainView.DeleteUserData(@"portfolios\trades\" + name);
			MainView.DeleteUserData(@"models\lists\" + name);
			MainView.DeleteUserData(@"portfolio-senarios\" + name);
			MainView.DeleteUserData(@"models\scores\User\" + name);
			MainView.DeleteUserData(@"models\predictions\User\" + name);
			MainView.DeleteUserData(@"models\weights\User\" + name);
			MainView.DeleteUserData(@"models\shares\User\" + name);
		}

		private void changeSelectedUserFactorModel(string newName)
		{
			string oldName = _selectedUserFactorModel;
			if (newName != oldName)
			{
				if (!_userFactorModels.ContainsKey(newName))
				{
					var model = _userFactorModels[oldName];
					_userFactorModels.Remove(oldName);
					deleteModelData(oldName);
					model.Name = newName;
					_userFactorModels[newName] = model;
					updateUserFactorModelList();
					//changeUserFactorModel(newName);

					//UserFactorModelName.Text = newName;
					//ModelName2.Content = newName;
					ModelName3.Content = newName;
					ModelName4.Content = newName;
					//ModelName5.Content = newName;
					ModelName5b.Content = newName;
					ModelName6.Content = newName;
					//ModelName6b.Content = newName;
					//ModelName7.Content = newName;

					updateChartAnalysisSettings(model);
					updateChartPortfolio();
				}

				_selectedUserFactorModel = newName;
				_g = 0;
				_portfolio1.Clear();

				updateUserFactorModelList();
				initializeCalculator(getModel());
				updateStrategyTree();
			}
			OnPropertyChanged("Constraints");
			OnPropertyChanged("ATMConditionsEnter");
			OnPropertyChanged("ATMConditionsExit");
		}

		private void updateChartPortfolio()
		{
			string interval2 = getInterval(_interval, 1);
			if (_twoCharts)
			{
				changeChart(_chart1, _symbol, interval2, getPortfolioName());
				changeChart(_chart2, _symbol, _interval, getPortfolioName());
			}
			else
			{
				changeChart(_chart1, _symbol, _interval, getPortfolioName());
			}
		}

		private void updateChartAnalysisSettings(Model model)
		{
			if (_chart1 != null)
			{
				_chart1.Indicators["New Trend"].Enabled = model.Groups[_g].UseNewTrend;
				_chart1.Indicators["Pressure"].Enabled = model.Groups[_g].UsePressure;
				_chart1.Indicators["Add"].Enabled = model.Groups[_g].UseAdd;
				_chart1.Indicators["Retrace"].Enabled = model.Groups[_g].UseRetrace;
				_chart1.Indicators["Exh"].Enabled = model.Groups[_g].UseExhaustion;
				_chart1.Indicators["FT | FT"].Enabled = false;
				_chart1.Indicators["FT | SC"].Enabled = false;
				_chart1.Indicators["FT | ST"].Enabled = false;
				_chart1.Indicators["FT | TSB"].Enabled = false;
				_chart1.Indicators["SC | FT"].Enabled = false;
				_chart1.Indicators["SC | SC"].Enabled = false;
				_chart1.Indicators["SC | ST"].Enabled = false;
				_chart1.Indicators["SC | TSB"].Enabled = false;
				_chart1.Indicators["ST | FT"].Enabled = false;
				_chart1.Indicators["ST | SC"].Enabled = false;
				_chart1.Indicators["ST | ST"].Enabled = false;
				_chart1.Indicators["ST | TSB"].Enabled = false;
				_chart1.Indicators["TSB | FT"].Enabled = false;
				_chart1.Indicators["TSB | SC"].Enabled = false;
				_chart1.Indicators["TSB | ST"].Enabled = false;
				_chart1.Indicators["TSB | TSB"].Enabled = false;
				_chart1.Indicators[model.Groups[_g].Strategy].Enabled = model.Groups[_g].UseATMStrategies;
				_chart1.Indicators["ATRX"].Enabled = model.Groups[_g].UseATRRisk;
				((NumberParameter)(_chart1.Indicators["ATRX"].Parameters["Period"])).Value = model.Groups[_g].ATRRiskPeriod;
				((NumberParameter)(_chart1.Indicators["ATRX"].Parameters["Multiplier"])).Value = model.Groups[_g].ATRRiskFactor;
				_chart1.updatePanels();

			}
			if (_chart2 != null)
			{
				_chart2.Indicators["New Trend"].Enabled = model.Groups[_g].UseNewTrend;
				_chart2.Indicators["Pressure"].Enabled = model.Groups[_g].UsePressure;
				_chart2.Indicators["Add"].Enabled = model.Groups[_g].UseAdd;
				_chart2.Indicators["Retrace"].Enabled = model.Groups[_g].UseRetrace;
				_chart2.Indicators["Exh"].Enabled = model.Groups[_g].UseExhaustion;
				_chart2.Indicators["FT | FT"].Enabled = false;
				_chart2.Indicators["FT | SC"].Enabled = false;
				_chart2.Indicators["FT | ST"].Enabled = false;
				_chart2.Indicators["FT | TSB"].Enabled = false;
				_chart2.Indicators["SC | FT"].Enabled = false;
				_chart2.Indicators["SC | SC"].Enabled = false;
				_chart2.Indicators["SC | ST"].Enabled = false;
				_chart2.Indicators["SC | TSB"].Enabled = false;
				_chart2.Indicators["ST | FT"].Enabled = false;
				_chart2.Indicators["ST | SC"].Enabled = false;
				_chart2.Indicators["ST | ST"].Enabled = false;
				_chart2.Indicators["ST | TSB"].Enabled = false;
				_chart2.Indicators["TSB | FT"].Enabled = false;
				_chart2.Indicators["TSB | SC"].Enabled = false;
				_chart2.Indicators["TSB | ST"].Enabled = false;
				_chart2.Indicators["TSB | TSB"].Enabled = false;
				_chart2.Indicators[model.Groups[_g].Strategy].Enabled = model.Groups[_g].UseATMStrategies;
				_chart1.Indicators["ATRX"].Enabled = model.Groups[_g].UseATRRisk;
				((NumberParameter)(_chart1.Indicators["ATRX"].Parameters["Period"])).Value = model.Groups[_g].ATRRiskPeriod;
				((NumberParameter)(_chart1.Indicators["ATRX"].Parameters["Multiplier"])).Value = model.Groups[_g].ATRRiskFactor;
				_chart2.updatePanels();
			}
		}

		//private void changeSelectedMLFactorModel(string newName)
		//{
		//    string oldName = _selectedMLFactorModel;
		//    if (newName != oldName)
		//    {
		//        if (!_MLFactorModels.ContainsKey(newName))
		//        {
		//            var model = _MLFactorModels[oldName];
		//            _MLFactorModels.Remove(oldName);
		//            model.Name = newName;
		//            _MLFactorModels[newName] = model;
		//            updateMLFactorModelList();
		//            changeMLFactorModel(newName);

		//            MLFactorModelName.Text = newName;
		//            ModelName2.Content = newName;
		//            ModelName3.Content = newName;
		//        }
		//    }
		//}

		private void loadUserFactorModels()
		{
			_initializing = true;

			_userFactorModels.Clear();

			var path = MainView.GetDataFolder() + @"\models\Models";
			var files = Directory.GetFiles(path).ToList();
			// Read lightweight meta index so LIVE/TEST grouping is correct without
			// loading full model files (which is slow for many portfolios).
			var liveMeta = loadModelMeta();

			files.ForEach(f =>
			{
				var p = f.Split('\\');
				var name = p.Last();
				if (name == "_meta") return; // skip meta index file

				var model = new Model();
				model.Name = name;
				model.NeedsLoading = true;
				model.SetTimeRanges();
				// Apply cached IsLiveMode from meta so grouping is correct immediately.
				if (liveMeta.ContainsKey(name))
					model.IsLiveMode = liveMeta[name];
				_userFactorModels[name] = model;
			});

			if (!_userFactorModels.ContainsKey(_selectedUserFactorModel))
			{
				changeUserFactorModel((_userFactorModels.Count > 0) ? _userFactorModels.Keys.ToList()[0] : "");
			}

			updateUserFactorModelList();
			//userFactorModel_to_ui(_selectedUserFactorModel);
			_update = 800;

			_initializing = false;
		}

		private void saveUserFactorModels(string name = "")
		{
			var path = @"models\Models";
			foreach (var model in _userFactorModels.Values)
			{
				if ((name == "" || name == model.Name) && !model.NeedsLoading)
				{
					var modelName = model.Name.Replace("|", "(bar)").Replace(":", "(colon)");
					var data = model.save();
					MainView.SaveUserData(path + @"\" + modelName, data);
				}
			}
			// Keep meta index in sync so startup grouping is always current.
			saveModelMeta();
		}

		/// <summary>Writes a lightweight name->IsLiveMode index to models\Models\_meta.</summary>
		private void saveModelMeta()
		{
			var sb = new System.Text.StringBuilder();
			foreach (var kvp in _userFactorModels)
				sb.AppendLine(kvp.Key + "\t" + (kvp.Value.IsLiveMode ? "1" : "0"));
			try { MainView.SaveUserData(@"models\Models\_meta", sb.ToString()); } catch { }
		}

		/// <summary>Reads the lightweight meta index. Returns name->isLive dictionary.</summary>
		private Dictionary<string, bool> loadModelMeta()
		{
			var result = new Dictionary<string, bool>(StringComparer.Ordinal);
			try
			{
				var data = MainView.LoadUserData(@"models\Models\_meta");
				if (data == null) return result;
				foreach (var line in data.Split('\n'))
				{
					var parts = line.Trim().Split('\t');
					if (parts.Length == 2)
						result[parts[0]] = parts[1] == "1";
				}
			}
			catch { }
			return result;
		}

		private void UserFactorModelSave_Mousedown(object sender, MouseButtonEventArgs e)
		{
			var label = sender as Label;
			label.Focus();
			var model = getModel();
			if (model != null && _userFactorModels.Keys.Count > 0)
			{
				var name = model.Name;

				// Check if model is locked
				var dataFolder = MainView.GetDataFolder();
				var decisionLockPath = dataFolder + @"\decisionlock\" + name;
				bool isLocked = Directory.Exists(decisionLockPath) || model.IsLiveMode;

				if (isLocked)
				{
					// Log tamper attempt
					AuditService.LogAction(
						action: "TAMPER_ATTEMPT",
						objectType: "Model",
						objectId: name,
						after: $"Attempted save on locked model. IsLiveMode={model.IsLiveMode}"
					);
					AuditService.LogConstraintBreach(
						portfolioId: name,
						description: $"Save attempted on locked model '{name}' — blocked.",
						severity: "High"
					);
					MessageBox.Show(
						$"Model '{name}' is locked and cannot be modified.\n\nThis attempt has been logged.",
						"\uD83D\uDD12  Model Locked",
						MessageBoxButton.OK,
						MessageBoxImage.Warning);
					return;
				}

				MessageBoxResult result = System.Windows.MessageBox.Show("Are you sure you wish to save changes " + name + "?", "Confirm Save", MessageBoxButton.YesNo, MessageBoxImage.Question);
				if (result == MessageBoxResult.Yes)
				{
					// Snapshot parameters before save
					var beforeSnapshot = SnapshotModelParams(model);
					saveModel(name);
					var afterSnapshot = SnapshotModelParams(getModel());

					// Log parameter changes
					if (beforeSnapshot != afterSnapshot)
					{
						AuditService.LogAction(
							action: "MODEL_PARAMETER_CHANGE",
							objectType: "Model",
							objectId: name,
							before: beforeSnapshot,
							after: afterSnapshot
						);
					}

					checkMLModel();
				}
			}
		}

		private void saveModel(string name = "")
		{
			Grid grid = UserFactorModelPanel1.SelectedItem as Grid;
			if (grid != null)
			{
				TextBox tb = grid.Children[0] as TextBox;
				if (tb != null)
				{
					string newName = tb.Text;
					changeSelectedUserFactorModel(newName);
					ui_to_userFactorModel(_selectedUserFactorModel);
					var model = _userFactorModels[_selectedUserFactorModel];
					// Ensure new portfolios (NeedsLoading=true) are not skipped by saveUserFactorModels.
					model.NeedsLoading = false;
					// Save BEFORE userFactorModel_to_ui triggers initializeCalculator -> loadModel
					// -> SetTimeRanges, which would otherwise overwrite the date we just committed.
					saveUserFactorModels(name);
					userFactorModel_to_ui(_selectedUserFactorModel);
					updateChartAnalysisSettings(model);
					// Re-render grouped list now that IsLiveMode is committed.
					updateUserFactorModelList();
					hideNavigation();
				}
			}
		}

		//private void MonthlySummary_Click(object sender, RoutedEventArgs e)
		//{

		//    MonthlySummaryGrid.Visibility = Visibility.Visible;
		//    SectorBreakdownGrid.Visibility = Visibility.Collapsed;
		//    RebalanceGrid.Visibility = Visibility.Collapsed;
		//    TradeOverviewGrid.Visibility = Visibility.Collapsed;
		//    RebalanceGrid.Visibility = Visibility.Collapsed;
		//}

		//private void SectorBreakdown_Click(object sender, RoutedEventArgs e)
		//{

		//    MonthlySummaryGrid.Visibility = Visibility.Collapsed;
		//    SectorBreakdownGrid.Visibility = Visibility.Visible;
		//    RebalanceGrid.Visibility = Visibility.Collapsed;
		//    TradeOverviewGrid.Visibility = Visibility.Collapsed;
		//    RebalanceGrid.Visibility = Visibility.Collapsed;
		//}

		//private void SectorPosition_Click(object sender, RoutedEventArgs e)
		//{

		//    MonthlySummaryGrid.Visibility = Visibility.Collapsed;
		//    SectorBreakdownGrid.Visibility = Visibility.Collapsed;
		//    RebalanceGrid.Visibility = Visibility.Visible;
		//    TradeOverviewGrid.Visibility = Visibility.Collapsed;
		//    RebalanceGrid.Visibility = Visibility.Collapsed;
		//}

		//private void TradeOverview_Click(object sender, RoutedEventArgs e)
		//{

		//    MonthlySummaryGrid.Visibility = Visibility.Collapsed;
		//    SectorBreakdownGrid.Visibility = Visibility.Collapsed;
		//    RebalanceGrid.Visibility = Visibility.Collapsed;
		//    TradeOverviewGrid.Visibility = Visibility.Visible;
		//    RebalanceGrid.Visibility = Visibility.Collapsed;
		//}

		//private void PositionGrid_Click(object sender, RoutedEventArgs e)
		//{

		//    MonthlySummaryGrid.Visibility = Visibility.Collapsed;
		//    SectorBreakdownGrid.Visibility = Visibility.Collapsed;
		//    RebalanceGrid.Visibility = Visibility.Collapsed;
		//    TradeOverviewGrid.Visibility = Visibility.Collapsed;
		//    RebalanceGrid.Visibility = Visibility.Visible;
		//}

		//private void ui_to_MLFactorModel(string name)
		//{
		//    if (_MLFactorModels != null && _MLFactorModels.ContainsKey(name))
		//    {
		//        var mlFactorModel = _MLFactorModels[name];
		//        mlFactorModel.Name = name;

		//        mlFactorModel.FactorNames.Clear();
		//        string text = MLFactorModelEditor.Text;
		//        string[] lines = text.Split('\n');
		//        foreach (var line in lines)
		//        {
		//            if (line.Length > 0)
		//            {
		//                mlFactorModel.FactorNames.Add(line);
		//            }
		//        }

		//        mlFactorModel.TrainingRange.Time1 = (DateTime)MLFactorModelTrainStartDate.SelectedDate;
		//        mlFactorModel.TrainingRange.Time2 = (DateTime)MLFactorModelTrainEndDate.SelectedDate;
		//        mlFactorModel.TestingRange.Time1 = (DateTime)MLFactorModelTestStartDate.SelectedDate;
		//        mlFactorModel.TestingRange.Time2 = (DateTime)MLFactorModelTestEndDate.SelectedDate;
		//        mlFactorModel.Universe = MLFactorModelUniverse.Content as string;
		//        mlFactorModel.Benchmark = MLFactorModelBenchmark.Content as string;
		//        mlFactorModel.Rebalance = MLFactorModelRebalance.Content as string;
		//        mlFactorModel.Ranking = MLFactorModelRanking.Content as string;
		//        mlFactorModel.OutputName = MLFactorModelOutput.Text;
		//    }
		//}

		private void userFactorModel_to_ui(string name)
		{
			Result value = null;

			bool ok = false;

			// Reset stale state from previous portfolio
			_portfolioResultsTimestamp = "";
			_lastSharpe = double.NaN;
			_lastSharpePortfolio = "";
			_portfolioResults = new Dictionary<string, double>();

			var activeBrush = new SolidColorBrush(Color.FromRgb(0x00, 0xcc, 0xff));
			var inactiveBrush = new SolidColorBrush(Color.FromRgb(0xbd, 0xbd, 0xbd));

			UserFactorModelEditor.Text = "";
			UserFactorModelEditor1.Text = "";
			UserFactorModelFilter.Text = "";
			UserFactorModelFilter1.Text = "";
			if (_userFactorModels.ContainsKey(name))
			{
				var model = _userFactorModels[name];
				if (model.Groups.Count == 0) model.Groups.Add(new ModelGroup());

				GroupName.Content = model.Groups[_g].Name;

				//UseMT.IsChecked = model.Groups[g].UseMT;
				UseMTVisChange(model.Groups[_g].UseMT);
				//UseST.IsChecked = model.Groups[g].UseST;
				UseSTVisChange(model.Groups[_g].UseST);

				MLModelName.Content = model.MLModelName;

				UseMoneyMgtVisChange(model.ApplyMoneyMgt);

				var lowestInterval = (_ic != null) ? _ic.GetLowestInterval() : "Daily";

				var mlModel = MainView.GetModel(model.MLModelName);
				bool hasModel = (model.UseML && mlModel != null);

				if (model.UseML)
				{
					ApplyATMML.Visibility = Visibility.Visible;
					MLModelName.Visibility = Visibility.Visible;
					FeatureEditor1.Visibility = Visibility.Visible;
					FeatureEditor2.Visibility = Visibility.Visible;
					FeatureInputGray.Visibility = Visibility.Visible;
					TrainAs.Visibility = Visibility.Visible;
					Trained.Visibility = Visibility.Visible;
					Trained.Content = (mlModel != null && mlModel.UseTicker) ? "Individually" : "Group";
				}
				else
				{
					ApplyATMML.Visibility = Visibility.Collapsed;
					MLModelName.Visibility = Visibility.Collapsed;
					FeatureEditor1.Visibility = Visibility.Collapsed;
					FeatureEditor2.Visibility = Visibility.Collapsed;
					FeatureInputGray.Visibility = Visibility.Visible;
					TrainAs.Visibility = Visibility.Collapsed;
					Trained.Visibility = Visibility.Collapsed;
				}

				GroupList.Children.Clear();
				var index = 0;
				model.Groups.ForEach(group =>
				{
					var label = new Label();
					label.Content = group.Name;
					label.Background = Brushes.Transparent;
					label.Margin = new Thickness(5, 0, 5, 0);
					label.Cursor = Cursors.Hand;
					label.MouseEnter += OurView_MouseEnter;
					label.MouseLeave += Group_MouseLeave;
					label.MouseDown += Group_MouseDown;
					label.FontFamily = new FontFamily("Helvetica Neue");
					label.FontSize = 11;
					label.Foreground = (index == _g) ? new SolidColorBrush(Color.FromRgb(0x00, 0xcc, 0xff)) : Brushes.WhiteSmoke;
					GroupList.Children.Add(label);
					index++;
				});

				AlignmentTime.Content = getIntervalAbbreviation(model.ATMAnalysisInterval);
				AlignmentTime2.Content = AlignmentTime.Content;
				PosSetting.Content = getPosSetting(model.Groups[_g].Direction);
				PosSetting2.Content = getPosSetting(model.Groups[_g].Direction);

				SigFreq.Content = model.FreqPeriod.ToString();
				NewTrendUnit1.Content = model.CommissionAmt.ToString();
				PressureUnit1.Content = model.CommissionAmt.ToString();
				AddUnit1.Content = model.CommissionAmt.ToString();
				RetracePercent1.Content = model.CommissionAmt.ToString();

				//mtNTOnOff.Content = model.useMTNT.ToString();
				//mtPOnOff.Content = model.UseMTP.ToString();
				//mtftOnOff.Content = model.UseMTFT.ToString();

				SigFreq2.Content = model.FreqPeriod.ToString();
				NewTrendUnit2.Content = model.CommissionAmt.ToString();
				PressureUnit2.Content = model.CommissionAmt.ToString();
				AddUnit2.Content = model.CommissionAmt.ToString();
				RetracePercent2.Content = model.CommissionAmt.ToString();

				//mtNTOnOff1.Content = (model.UseMTExit == SizeRecommendation.MaxDollar) ? "Max Dollar"
				//mtPOnOff1.Content = model.UseMTP.ToString();
				//mtftOnOff1.Content = model.UseMTFT.ToString();

				CONDyn2.Content = model.Groups[_g].UseATMStrategies ? model.Groups[_g].Strategy : "ALIGN";
				CONDyn2.Foreground = model.Groups[_g].UseATMStrategies ? activeBrush : inactiveBrush;
				CONDyn2a.Content = model.Groups[_g].UseATMStrategies ? model.Groups[_g].Strategy : "ALIGN";
				CONDyn2a.Foreground = model.Groups[_g].UseATMStrategies ? activeBrush : inactiveBrush;
				CONDyn2.Foreground = model.Groups[_g].UseATMStrategies ? activeBrush : inactiveBrush;

				//ATRRiskPercent.text = model.ATRRiskPercent.ToString();
				//ATRRiskPercent1.Content = model.ATRRiskPercent.ToString();
				//ATRRiskPercent2.Content = model.ATRRiskPercent.ToString();

				TwoBarX.Foreground = model.Groups[_g].UseTwoBar ? activeBrush : inactiveBrush;
				TwoBarX2.Foreground = model.Groups[_g].UseTwoBar ? activeBrush : inactiveBrush;

				//RocX.Foreground = model.UseTwoBar ? activeBrush : inactiveBrush;
				//RocX2.Foreground = model.UseTwoBar ? activeBrush : inactiveBrush;

				ModelName3.Content = name;
				ModelName4.Content = name;
				//ModelName5.Content = name;
				ModelName5b.Content = name;
				ModelName6.Content = name;
				//ModelName7.Content = name;

				string text1 = "";
				string text2 = "";
				bool first = true;
				foreach (var factor in model.FeatureNames)
				{
					var items = factor.Split('\u0002');
					if (!first) { text1 += "\n"; text2 += "\n"; }
					text1 += items[0]; text2 += items[1];
					first = false;
				}
				FeatureEditor1.Text = text1;
				FeatureEditor2.Text = text2;

				string text = "";
				foreach (var factor in model.FactorNames)
				{
					if (text.Length > 0) text += "\n";
					text += factor;
				}
				UserFactorModelEditor.Text = text;
				UserFactorModelEditor1.Text = text;
				UserFactorModelFilter.Text = model.Filter.Replace('\u0001', '\n');
				UserFactorModelFilter1.Text = model.Filter.Replace('\u0001', '\n');

				UseEndTime.IsChecked = !model.UseCurrentDate;

				FactorActionPanel.Visibility = (!isTradingModel(model.Name)) ? Visibility.Visible : Visibility.Collapsed;
				//UserFactorModelUniverse.Content = model.Groups[g].AlphaPortfolio;
				//Universe7.Content = model.Universe;
				UserFactorModelBenchmark.Text = model.Benchmark;
				Benchmark4.Content = model.Benchmark;

				MaxUnits.Text = model.Groups[_g].Leverage.ToString();
				MaxUnits2.Content = model.Groups[_g].Leverage.ToString();
				MaxUnits3.Content = model.Groups[_g].Leverage.ToString();
				FreqPeriod.Text = model.FreqPeriod.ToString();
				FreqPeriodCheckbox.IsChecked = model.FreqPeriodEnable;
				PortBal2.Text = model.InitialPortfolioBalance.ToString("C0");
				AlphaSymbolCount.Text = model.Groups[_g].AlphaSymbols.Count.ToString();
				HedgeSymbolCount.Text = model.Groups[_g].HedgeSymbols.Count.ToString();

				UserFactorModelRebalance.Content = model.RankingInterval;
				Rebalance4.Content = model.RankingInterval;
				UserFactorModelRebalance1.Content = model.RankingInterval;

				AlignmentInterval.Content = model.Groups[_g].AlignmentInterval;

				PositionInterval.Content = model.ATMAnalysisInterval;

				UseHedge.IsChecked = model.UseHedge == true;
				UseBetaHedge.IsChecked = model.UseBetaHedge == true;
				UseRiskOnOff.IsChecked = model.UseRiskOnOff == true;
				//UseBetaAdjust.IsChecked = model.UseBetaAdjust == true;
				//UseMktNeutral.IsChecked = model.UseMktNeutral == true;

				//BetaHedgeEnable.IsChecked = model.Groups[g].BetaHedgeEnable == true;
				//BetaMaxEnable.IsChecked = model.Groups[g].BetaMaxEnable == true;
				//BetaTTestEnable.IsChecked = model.Groups[g].BetaTTestEnable == true;

				var mt = model.RankingInterval;
				var st = getInterval(mt, -1);
				var lt = getInterval(mt, 1);

				LT1.Content = lt.ToUpper();
				LT2.Content = lt.ToUpper();
				LT3.Content = lt.ToUpper();
				LT4.Content = lt.ToUpper();

				MT1.Content = mt.ToUpper();
				MT2.Content = mt.ToUpper();
				MT3.Content = mt.ToUpper();
				MT4.Content = mt.ToUpper();

				ST1.Content = st.ToUpper();
				ST2.Content = st.ToUpper();
				ST3.Content = st.ToUpper();
				ST4.Content = st.ToUpper();

				UserFactorModelRanking.Content = model.Ranking;
				Ranking4.Content = model.Ranking;
				UserFactorModelManagementFee.Text = model.ManagementFee.ToString("0.00");
				UserFactorModelPerformanceFee.Text = model.PerformanceFee.ToString("0.00");

				if (model.PortfolioWeight == Model.PortfolioWeightType.Equal)
				{
					EqualWeight.IsChecked = true;
					MktCapWeight.IsChecked = false;
					//PriceWeight.IsChecked = false;
				}
				else if (model.PortfolioWeight == Model.PortfolioWeightType.MarketCap)
				{
					EqualWeight.IsChecked = false;
					MktCapWeight.IsChecked = true; ;
				}
				else if (model.PortfolioWeight == Model.PortfolioWeightType.Price)
				{
					EqualWeight.IsChecked = false;
					MktCapWeight.IsChecked = false;
					//PriceWeight.IsChecked = true;
				}

				_compareToSymbol = model.Benchmark;
				_compareToSymbolChange = true;

				var fields = _compareToSymbol.Split(' ');

				CompareToSymbol1.Text = _compareToSymbol;
				CompareToSymbol5.Text = _compareToSymbol;

				UseML.IsChecked = model.UseML;
				UseRankingInputs.IsChecked = model.UseRanking;
				UseFilters.IsChecked = model.UseFilters;

				UseExecutionCost.IsChecked = model.UseExecutionCost;
				UseBorrowingCost.IsChecked = model.UseBorrowingCost;
				UseDiv.IsChecked = model.UseDiv;
				//UseBetaAdjust.IsChecked = model.UseBetaAdjust;

				NewTrendEntry.IsChecked = model.Groups[_g].UseNewTrend;
				UseNTObOs.IsChecked = model.Groups[_g].UseNTObOs;
				PressureEntry.IsChecked = model.Groups[_g].UsePressure;
				AddEntry.IsChecked = model.Groups[_g].UseAdd;
				RetraceExit.IsChecked = model.Groups[_g].UseRetrace;
				ExhaustionExit.IsChecked = model.Groups[_g].UseExhaustion;
				UsePExit.IsChecked = model.Groups[_g].UsePExhaustion;
				UseProveIt.IsChecked = model.Groups[_g].UseProveIt;

				UseFTRoc.IsChecked = model.Groups[_g].UseFTRoc;
				FTRocPercent.Text = model.Groups[_g].UseFTRoc ? model.Groups[_g].FTRocPercent.ToString() : "";

				UseTwoBar.IsChecked = model.Groups[_g].UseTwoBar;
				TwoBarPercent.Text = model.Groups[_g].UseTwoBar ? model.Groups[_g].TwoBarPercent.ToString() : "";
				UseATRRisk.IsChecked = model.Groups[_g].UseATRRisk;
				ATRRiskFactor.Text = model.Groups[_g].UseATRRisk ? model.Groups[_g].ATRRiskFactor.ToString() : "";
				ATRRiskPeriod.Text = model.Groups[_g].UseATRRisk ? model.Groups[_g].ATRRiskPeriod.ToString() : "";
				ATRRiskPercent1.Content = model.Groups[_g].UseATRRisk ? model.Groups[_g].ATRRiskFactor.ToString() : "";
				ATRRiskPercent2.Content = model.Groups[_g].UseATRRisk ? model.Groups[_g].ATRRiskFactor.ToString() : "";

				//UseCoolDown.IsChecked = model.UseCoolDown;
				//CoolDownPeriod.Text = model.UseCoolDown ? model.CoolDownPeriod.ToString() : "";

				UseRiskEngine2.IsChecked = model.UseRiskEngine2;
				UseRiskEngine3.IsChecked = model.UseRiskEngine3;
				ConstraintsListView.Visibility = Visibility.Visible; // model.UseRiskEngine2 ? Visibility.Visible : Visibility.Collapsed;
																	 //UseRiskEngine4.IsChecked = model.UseRiskEngine4;
																	 //RiskBudget.Text = model.UseRiskEngine ? model.RiskBudget.ToString() : "";

				//RiskInterval.Content = model.RiskInterval;

				UsePortRiskL1.IsChecked = model.UsePortRisk1;
				//PortRiskPercentL1.Text = model.UsePortRisk1 ? model.PortRiskPercent1.ToString() : "";
				PortRiskPercentL1a.Content = model.UsePortRisk1 ? model.PortRiskPercent1.ToString() : "";
				PortRiskPercentL1b.Content = model.UsePortRisk1 ? model.PortRiskPercent1.ToString() : "";

				//UsePortRiskL2.IsChecked = model.UsePortRisk2;
				//PortRiskPercentL2.Text = model.UsePortRisk2 ? model.PortRiskPercent2.ToString() : "";
				//PortRiskPercentL2a.Content = model.UsePortRisk2 ? model.PortRiskPercent2.ToString() : "";
				//PortRiskPercentL2b.Content = model.UsePortRisk2 ? model.PortRiskPercent2.ToString() : "";

				//UsePortRiskL3.IsChecked = model.UsePortRisk3;
				//PortRiskPercentL3.Text = model.UsePortRisk3 ? model.PortRiskPercent3.ToString() : "";
				//PortRiskPercentL3a.Content = model.UsePortRisk3 ? model.PortRiskPercent3.ToString() : "";
				//PortRiskPercentL3b.Content = model.UsePortRisk3 ? model.PortRiskPercent3.ToString() : "";

				//UseTradeRisk.IsChecked = model.UseTradeRisk;
				//TradeRiskPercent.Text = model.UseTradeRisk ? model.TradeRiskPercent.ToString() : "";
				TradeRiskPercent1.Content = model.UseTradeRisk ? model.TradeRiskPercent.ToString() : "";
				TradeRiskPercent2.Content = model.UseTradeRisk ? model.TradeRiskPercent.ToString() : "";

				UseConviction.IsChecked = model.Groups[_g].UseConviction;
				ConvictionPercent.Text = model.Groups[_g].UseConviction ? model.Groups[_g].ConvictionPercent.ToString() : "";
				ConvictionPercent1.Content = model.Groups[_g].UseConviction ? model.Groups[_g].ConvictionPercent.ToString() : "";
				ConvictionPercent2.Content = model.Groups[_g].UseConviction ? model.Groups[_g].ConvictionPercent.ToString() : "";

				//UseSTFT.IsChecked = model.Groups[g].UseSTFT;
				//UseSTST.IsChecked = model.Groups[g].UseSTST;
				//UseSTSC.IsChecked = model.Groups[g].UseSTSC;
				//UseSTTSB.IsChecked = model.Groups[g].UseSTTSB;

				//UseSTFT.IsChecked = model.UseSTFT = 1;
				//UseSTST.IsChecked = model.UseSTST = 1;
				//UseSTSC.IsChecked = model.UseSTSC = 1; 
				//UseSTTSB.IsChecked = model.UseSTTSB = 1;

				//UseHedge.IsChecked = model.UseHedge;

				//UseMTNToff.IsChecked = model.Groups[g].MTNT == 0;
				//UseMTNT.IsChecked = model.Groups[g].MTNT == 1;
				//UseNTPlus1.IsChecked = model.Groups[g].MTNT == 2;
				//UseMTSecNT.IsChecked = model.Groups[g].MTNT == 3;

				//UseMTPoff.IsChecked = model.Groups[g].MTP == 0;
				//UseMTP.IsChecked = model.Groups[g].MTP == 1;
				//UsePPlus1.IsChecked = model.Groups[g].MTP == 2;
				//UseMTSecP.IsChecked = model.Groups[g].MTP == 3;

				//UseFtFtoff.IsChecked = model.Groups[g].MTFT == 0;
				//UseMTFT.IsChecked = model.Groups[g].MTFT == 1;
				//UseFTPlus1.IsChecked = model.Groups[g].MTFT == 2;
				//UseMTSecFT.IsChecked = model.Groups[g].MTFT == 3;

				NewTrendUnit.Text = model.Groups[_g].UseNewTrend ? model.Groups[_g].NewTrendUnit.ToString() : "";
				NewTrendUnit1.Content = model.Groups[_g].UseNewTrend ? model.Groups[_g].NewTrendUnit.ToString() : "";
				NewTrendUnit2.Content = model.Groups[_g].UseNewTrend ? model.Groups[_g].NewTrendUnit.ToString() : "";
				PressureUnit.Text = model.Groups[_g].UsePressure ? model.Groups[_g].PressureUnit.ToString() : "";
				PressureUnit1.Content = model.Groups[_g].UsePressure ? model.Groups[_g].PressureUnit.ToString() : "";
				PressureUnit2.Content = model.Groups[_g].UsePressure ? model.Groups[_g].PressureUnit.ToString() : "";
				AddUnit.Text = model.Groups[_g].UseAdd ? model.Groups[_g].AddUnit.ToString() : "";
				AddUnit1.Content = model.Groups[_g].UseAdd ? model.Groups[_g].AddUnit.ToString() : "";
				AddUnit2.Content = model.Groups[_g].UseAdd ? model.Groups[_g].AddUnit.ToString() : "";
				RetracePercent.Text = model.Groups[_g].UseRetrace ? model.Groups[_g].RetracePercent.ToString() : "";
				RetracePercent1.Content = model.Groups[_g].UseRetrace ? model.Groups[_g].RetracePercent.ToString() : "";
				RetracePercent2.Content = model.Groups[_g].UseRetrace ? model.Groups[_g].RetracePercent.ToString() : "";
				ExhaustionPercent.Text = model.Groups[_g].UseExhaustion ? model.Groups[_g].ExhaustionPercent.ToString() : "";
				ExhaustionPercent1.Content = model.Groups[_g].UseExhaustion ? model.Groups[_g].ExhaustionPercent.ToString() : "";
				ExhaustionPercent2.Content = model.Groups[_g].UseExhaustion ? model.Groups[_g].ExhaustionPercent.ToString() : "";
				TwoBarX.Foreground = model.Groups[_g].UseTwoBar ? activeBrush : inactiveBrush;

				UseTimeExit.IsChecked = model.Groups[_g].UseTimeExit;
				BarsToExit.Text = model.Groups[_g].UseTimeExit ? model.Groups[_g].BarsToExit.ToString() : "";
				PercentBarsToExit.Text = model.Groups[_g].UseTimeExit ? model.Groups[_g].PercentBarsToExit.ToString() : "";

				PExitPercent.Text = model.Groups[_g].UsePExhaustion ? model.Groups[_g].PExhaustionPercent.ToString() : "";

				stFTOnOff.Foreground = model.Groups[_g].UseSTFT ? activeBrush : inactiveBrush;
				stSTOnOff.Foreground = model.Groups[_g].UseSTST ? activeBrush : inactiveBrush;
				stSCOnOff.Foreground = model.Groups[_g].UseSTSC ? activeBrush : inactiveBrush;
				stTSBOnOff.Foreground = model.Groups[_g].UseSTTSB ? activeBrush : inactiveBrush;

				mtNTOnOff.Content = model.Groups[_g].MTNT == 0 ? "MTN" : model.Groups[_g].MTNT == 1 ? "MTN" : model.Groups[_g].MTNT == 2 ? "MTN1" : "SNT1";
				mtNTOnOff.Foreground = model.Groups[_g].MTNT == 0 ? inactiveBrush : activeBrush;
				mtPrsOnOff.Content = model.Groups[_g].MTP == 0 ? "MTP" : model.Groups[_g].MTP == 1 ? "MTP" : model.Groups[_g].MTP == 2 ? "MTP1" : "SP1";
				mtPrsOnOff.Foreground = model.Groups[_g].MTP == 0 ? inactiveBrush : activeBrush;
				mtFTOnOff.Content = model.Groups[_g].MTFT == 0 ? "MTFT" : model.Groups[_g].MTFT == 1 ? "MTFT" : model.Groups[_g].MTFT == 2 ? "MTFT1" : "SFT1";
				mtFTOnOff.Foreground = model.Groups[_g].MTFT == 0 ? inactiveBrush : activeBrush;

				mtNTOnOff1.Content = model.Groups[_g].MTNT == 0 ? "MTN" : model.Groups[_g].MTNT == 1 ? "MTN" : model.Groups[_g].MTNT == 2 ? "MTN1" : "SNT1";
				mtNTOnOff1.Foreground = model.Groups[_g].MTNT == 0 ? inactiveBrush : activeBrush;
				mtPrsOnOff1.Content = model.Groups[_g].MTP == 0 ? "MTP" : model.Groups[_g].MTP == 1 ? "MTP" : model.Groups[_g].MTP == 2 ? "MTP1" : "SP1";
				mtPrsOnOff1.Foreground = model.Groups[_g].MTP == 0 ? inactiveBrush : activeBrush;
				mtFTOnOff1.Content = model.Groups[_g].MTFT == 0 ? "MTFT" : model.Groups[_g].MTFT == 1 ? "MTFT" : model.Groups[_g].MTFT == 2 ? "MTFT1" : "SFT1";
				mtFTOnOff1.Foreground = model.Groups[_g].MTFT == 0 ? inactiveBrush : activeBrush;

				UseVWAP.IsChecked = model.rbNextBarVWAP;

				var strategy = model.Groups[_g].Strategy;
				if (strategy != null)
				{
					var strategyFields = strategy.Split('(');
					ATMStrategy.Content = strategyFields[0];
					ATMStrategy.Tag = strategy;
				}

				UseATMStrategiesCB.IsChecked = model.Groups[_g].UseATMStrategies;

				ATMStrategy.Visibility = (model.Groups[_g].UseATMStrategies) ? Visibility.Visible : Visibility.Collapsed;
				UseATMStrategies.Visibility = (model.Groups[_g].UseATMStrategies) ? Visibility.Visible : Visibility.Collapsed;
				AlignmentInterval.Visibility = (model.Groups[_g].UseATMStrategies) ? Visibility.Visible : Visibility.Collapsed;
				ApplyAlignmentInterval.Visibility = (model.Groups[_g].UseATMStrategies) ? Visibility.Visible : Visibility.Collapsed;

				RankButton.Visibility = (model.UseRanking) ? Visibility.Visible : Visibility.Collapsed;
				UserFactorModelRebalance.Visibility = (model.UseRanking) ? Visibility.Visible : Visibility.Collapsed;
				ApplyRankingInterval.Visibility = (model.UseRanking) ? Visibility.Visible : Visibility.Collapsed;

				UserFactorModelEditor1.Visibility = (model.UseRanking) ? Visibility.Visible : Visibility.Collapsed;
				UserFactorModelRanking.Visibility = (model.UseRanking) ? Visibility.Visible : Visibility.Collapsed;
				RankInputButton.Visibility = (model.UseRanking) ? Visibility.Visible : Visibility.Collapsed;
				RankInputGray.Visibility = (model.UseRanking) ? Visibility.Collapsed : Visibility.Visible;

				FilterInputButton.Visibility = (model.UseFilters) ? Visibility.Visible : Visibility.Collapsed; ;
				UserFactorModelFilter1.Visibility = (model.UseFilters) ? Visibility.Visible : Visibility.Collapsed;
				FilterInputGray.Visibility = (model.UseFilters) ? Visibility.Collapsed : Visibility.Visible;

				cbSTNetLong.IsChecked = model.UseATMTimingLong;
				cbSTNetShort.IsChecked = model.UseATMTimingShort;

				GrossLeverage.Text = model.GrossLeverage.ToString();
				GrossLeverage2.Text = model.GrossLeverage.ToString();

				PriceImpactPercent.Text = model.PriceImpactAmt.ToString();

				CommissionAmt.Text = model.CommissionAmt.ToString(); // int

				if (model.LongNet == 1) LongNet1.IsChecked = true;
				else if (model.LongNet == 2) LongNet2.IsChecked = true;
				else if (model.LongNet == 3) LongNet3.IsChecked = true;

				if (model.ShortNet == -1) ShortNet1.IsChecked = true;
				else if (model.ShortNet == -2) ShortNet2.IsChecked = true;
				else if (model.ShortNet == -3) ShortNet3.IsChecked = true;

				cbLTFTUp.IsChecked = model.cbLTFTUp;
				cbLTFTDn.IsChecked = model.cbLTFTDn;
				cbLTSCBuy.IsChecked = model.cbLTSCBuy;
				cbLTSCSell.IsChecked = model.cbLTSCSell;
				cbLTSTBuy.IsChecked = model.cbLTSTBuy;
				cbLTSTSell.IsChecked = model.cbLTSTSell;
				cbLTFTBuy.IsChecked = model.cbLTFTBuy;
				cbLTFTSell.IsChecked = model.cbLTFTSell;
				cbLTNetLong.IsChecked = model.cbLTNetLong;
				cbLTNetShort.IsChecked = model.cbLTNetShort;
				rbLTLong0.IsChecked = model.rbLTLong0;
				rbLTShort0.IsChecked = model.rbLTShort0;
				rbLTLong1.IsChecked = model.rbLTLong1;
				rbLTShort1.IsChecked = model.rbLTShort1;
				rbLTLong2.IsChecked = model.rbLTLong2;
				rbLTShort2.IsChecked = model.rbLTShort2;
				rbLTLong3.IsChecked = model.rbLTLong3;
				rbLTShort3.IsChecked = model.rbLTShort3;
				cbMTFTUp.IsChecked = model.cbMTFTUp;
				cbMTFTDn.IsChecked = model.cbMTFTDn;
				cbMTSCBuy.IsChecked = model.cbMTSCBuy;
				cbMTSCSell.IsChecked = model.cbMTSCSell;
				cbMTSTBuy.IsChecked = model.cbMTSTBuy;
				cbMTSTSell.IsChecked = model.cbMTSTSell;
				cbMTFTBuy.IsChecked = model.cbMTFTBuy;
				cbMTFTSell.IsChecked = model.cbMTFTSell;
				cbMTNetLong.IsChecked = model.cbMTNetLong;
				cbMTNetShort.IsChecked = model.cbMTNetShort;
				rbMTLong0.IsChecked = model.rbMTLong0;
				rbMTShort0.IsChecked = model.rbMTShort0;
				rbMTLong1.IsChecked = model.rbMTLong1;
				rbMTShort1.IsChecked = model.rbMTShort1;
				rbMTLong2.IsChecked = model.rbMTLong2;
				rbMTShort2.IsChecked = model.rbMTShort2;
				rbMTLong3.IsChecked = model.rbMTLong3;
				rbMTShort3.IsChecked = model.rbMTShort3;
				cbSTFTUp.IsChecked = model.cbSTFTUp;
				cbSTFTDn.IsChecked = model.cbSTFTDn;
				cbSTSCBuy.IsChecked = model.cbSTSCBuy;
				cbSTSCSell.IsChecked = model.cbSTSCSell;
				cbSTSTBuy.IsChecked = model.cbSTSTBuy;
				cbSTSTSell.IsChecked = model.cbSTSTSell;
				cbSTFTBuy.IsChecked = model.cbSTFTBuy;
				cbSTFTSell.IsChecked = model.cbSTFTSell;
				cbSTNetLong.IsChecked = model.cbSTNetLong;
				cbSTNetShort.IsChecked = model.cbSTNetShort;
				LongNet0.IsChecked = model.LongNet0;
				ShortNet0.IsChecked = model.ShortNet0;
				LongNet1.IsChecked = model.LongNet1;
				ShortNet1.IsChecked = model.ShortNet1;
				LongNet2.IsChecked = model.LongNet2;
				ShortNet2.IsChecked = model.ShortNet2;
				LongNet3.IsChecked = model.LongNet3;
				ShortNet3.IsChecked = model.ShortNet3;

				cbLTSTUp.IsChecked = model.cbLTSTUp;
				cbLTSTDn.IsChecked = model.cbLTSTDn;
				cbLTTSBUp.IsChecked = model.cbLTTSBUp;
				cbLTTSBDn.IsChecked = model.cbLTTSBDn;
				cbLTTLUp.IsChecked = model.cbLTTLUp;
				cbLTTLDn.IsChecked = model.cbLTTLDn;
				cbLTTBUp.IsChecked = model.cbLTTBUp;
				cbLTTBDn.IsChecked = model.cbLTTBDn;
				cbMTSTUp.IsChecked = model.cbMTSTUp;
				cbMTSTDn.IsChecked = model.cbMTSTDn;
				cbMTTSBUp.IsChecked = model.cbMTTSBUp;
				cbMTTSBDn.IsChecked = model.cbMTTSBDn;
				cbMTTLUp.IsChecked = model.cbMTTLUp;
				cbMTTLDn.IsChecked = model.cbMTTLDn;
				cbMTTBUp.IsChecked = model.cbMTTBUp;
				cbMTTBDn.IsChecked = model.cbMTTBDn;
				cbSTSTUp.IsChecked = model.cbSTSTUp;
				cbSTSTDn.IsChecked = model.cbSTSTDn;
				cbSTTSBUp.IsChecked = model.cbSTTSBUp;
				cbSTTSBDn.IsChecked = model.cbSTTSBDn;
				cbSTTLUp.IsChecked = model.cbSTTLUp;
				cbSTTLDn.IsChecked = model.cbSTTLDn;
				cbSTTBUp.IsChecked = model.cbSTTBUp;
				cbSTTBDn.IsChecked = model.cbSTTBDn;
				cbLTFTOB.IsChecked = model.cbLTFTOB;
				cbMTFTOB.IsChecked = model.cbMTFTOB;
				cbSTFTOB.IsChecked = model.cbSTFTOB;
				cbLTFTOS.IsChecked = model.cbLTFTOS;
				cbMTFTOS.IsChecked = model.cbMTFTOS;
				cbSTFTOS.IsChecked = model.cbSTFTOS;
				cbLTScoreUp.IsChecked = model.cbLTScoreUp;
				cbMTScoreUp.IsChecked = model.cbMTScoreUp;
				cbSTScoreUp.IsChecked = model.cbSTScoreUp;
				cbLTScoreDn.IsChecked = model.cbLTScoreDn;
				cbMTScoreDn.IsChecked = model.cbMTScoreDn;
				cbSTScoreDn.IsChecked = model.cbSTScoreDn;

				updateScenario(MainView.GetModel(model.MLModelName));

				FullyInvest.IsChecked = (model.SizeRecommendation == SizeRecommendation.FullyInvested);
				MaxDollar.IsChecked = (model.SizeRecommendation == SizeRecommendation.MaxDollar);
				MaxPercent.IsChecked = (model.SizeRecommendation == SizeRecommendation.MaxPercent);
				ATR.IsChecked = (model.SizeRecommendation == SizeRecommendation.ATR);

				MaxDollarAmt.Text = model.MaxDollarAmt.ToString();
				MaxPercentAmt.Text = model.MaxPercentAmt.ToString();
				//ATRRiskPeriod.Text = model.ATRPeriod.ToString();

				LongOnly.IsChecked = model.Groups[_g].Direction > 0;
				ShortOnly.IsChecked = model.Groups[_g].Direction < 0;
				//LongShort.IsChecked = model.Groups[g].Direction == 0;

				if (model.IsLiveMode && model.LiveStartDate != default)
					UserFactorModelTrainStartDate.SelectedDate = model.LiveStartDate;
				else
					UserFactorModelTrainStartDate.SelectedDate = model.DataRange.Time1;
				UserFactorModelTrainEndDate.SelectedDate = model.DataRange.Time2;

				// Restore Live/Backtest radio button state from model
				BackTest.IsChecked = !model.IsLiveMode;
				LiveTrade.IsChecked = model.IsLiveMode;
				if (model.IsLiveMode && model.LiveStartDate != default)
					UserFactorModelTrainStartDate.SelectedDate = model.LiveStartDate;

				UserFactorModelTrainEndDate.Visibility = model.UseCurrentDate ? Visibility.Collapsed : Visibility.Visible;
			}

			initializeCalculator(getModel());
			_update = 700;
		}

		private int _g = 0; // selected group

		private void Group_MouseDown(object sender, MouseButtonEventArgs e)
		{
			var label = sender as Label;
			if (label != null)
			{
				var model = getModel();
				var index = model.Groups.FindIndex(g => g.Name == label.Content as string);
				if (index != _g)
				{
					_initializing = true;
					_g = index;
					_portfolio1.Clear();
					userFactorModel_to_ui(model.Name);
					OnPropertyChanged("ATMConditionsEnter");
					OnPropertyChanged("ATMConditionsExit");
					_initializing = false;
				}
			}
		}

		private void OnBackTestSelected(object sender, RoutedEventArgs e)
		{
			if (UserFactorModelTrainStartDate == null) return;

			var model = getModel();
			if (model == null) return;
			model.IsLiveMode = false;
			// Backtest mode: DatePicker drives DataRange.Time1 directly
			if (UserFactorModelTrainStartDate.SelectedDate.HasValue)
				model.DataRange.Time1 = UserFactorModelTrainStartDate.SelectedDate.Value;
		}

		private void OnLiveTradeSelected(object sender, RoutedEventArgs e)
		{
			if (UserFactorModelTrainStartDate == null) return;

			var model = getModel();
			if (model == null) return;
			model.IsLiveMode = true;
			//Debug.WriteLine($"[UIMODE] getModel()={model.Name} IsLiveMode={model.IsLiveMode}");
			// Live mode: DatePicker is the live inception date
			// DataRange.Time1 is pushed back for signal lookback
			if (UserFactorModelTrainStartDate.SelectedDate.HasValue)
			{
				model.LiveStartDate = UserFactorModelTrainStartDate.SelectedDate.Value;
				model.DataRange.Time1 = model.LiveStartDate.AddDays(-90);
			}
		}
		private bool IsLiveMode => LiveTrade.IsChecked == true;

		private void updateScenario(Model mlModel)
		{
			var model = getModel();
			if (model.UseML && mlModel != null)
			{
				var scenario = mlModel.Scenario;
				var refText = MainView.GetSenarioLabel(scenario);
				var refTextFields = refText.Split('|');
				if (refTextFields.Length > 1)
				{
					VolScenario.Visibility = Visibility.Collapsed;
				}
				else
				{
					VolScenario.Visibility = Visibility.Visible;
					VolText.Content = refTextFields[0];
				}
			}
			else
			{
				VolScenario.Visibility = Visibility.Collapsed;
			}
		}

		private bool checkMLModel()
		{
			var ok = true;

			var model = getModel();
			var useML = model != null && model.UseML;

			if (useML)
			{
				ok = false;

				var name = model.MLModelName;
				var mlModel = MainView.GetModel(name);

				if (mlModel != null)
				{
					var interval = model.ATMMLInterval;

					ok = true;
					if (mlModel.UseTicker)
					{
						foreach (var symbol in model.Symbols)
						{
							var ticker = symbol.Ticker;
							var path = @"senarios\" + name + @"\" + interval + @"\" + ticker + @"\model.zip";
							ok = MainView.ExistsUserData(path);
							if (!ok) break;
						}
					}
					else
					{
						var path = @"senarios\" + name + @"\" + interval + @"\model.zip";
						ok = MainView.ExistsUserData(path);
					}

				}
				if (!ok)
				{
					MessageBoxResult result = System.Windows.MessageBox.Show("Train Auto ML Model that meets your Idea Setup Scenario requirement.", "Caption", MessageBoxButton.OK);
				}
			}
			return ok;
		}

		public List<Model.Constraint> Constraints
		{
			get
			{
				var model = getModel();
				if (model != null)
				{
					return model.Constraints;
				}
				return new List<Model.Constraint>();
			}

		}

		public List<string> ATMConditionsEnter
		{
			get
			{
				var model = getModel();
				if (model != null)
				{
					return model.Groups[_g].ATMConditionsEnter.Select(x => x.Replace("Mid Term", " MT").Replace("Short Term", " ST")).ToList();
				}
				return new List<string>();
			}
		}

		public List<string> ATMConditionsExit
		{
			get
			{
				var model = getModel();
				if (model != null)
				{
					return model.Groups[_g].ATMConditionsExit.Select(x => x.Replace("Mid Term", " MT").Replace("Short Term", " ST")).ToList();
				}
				return new List<string>();
			}
		}

		public List<string> ATMConditionsHedge
		{
			get
			{
				var model = getModel();
				if (model != null)
				{
					return model.Groups[_g].ATMConditionsHedge.Select(x => x.Replace("Mid Term", " MT").Replace("Short Term", " ST")).ToList();
				}
				return new List<string>();
			}
		}


		private void ui_to_userFactorModel(string name)
		{
			if (_userFactorModels != null && _userFactorModels.ContainsKey(name))
			{
				var model = _userFactorModels[name];
				model.Name = name;

				model.MLModelName = MLModelName.Content as string;

				model.FactorNames.Clear();
				string text = UserFactorModelEditor.Text;
				string[] lines = text.Split('\n');
				foreach (var line in lines)
				{
					if (line.Length > 0)
					{
						model.FactorNames.Add(line);
					}
				}

				model.UseCurrentDate = (UseEndTime.IsChecked == false);

				//model.Groups[g].AlphaPortfolio = (isTradingModel(model.Name)) ? model.Name : UserFactorModelUniverse.Content as string;

				model.RankingInterval = UserFactorModelRebalance.Content as string;
				model.Groups[_g].AlignmentInterval = AlignmentInterval.Content as string;
				model.ATMAnalysisInterval = PositionInterval.Content as string;

				model.UseHedge = UseHedge.IsChecked == true;
				model.UseBetaHedge = UseBetaHedge.IsChecked == true;
				model.UseRiskOnOff = UseRiskOnOff.IsChecked == true;
				//model.UseBetaAdjust = UseBetaAdjust.IsChecked == true;
				//model.UseMktNeutral = UseMktNeutral.IsChecked == true;

				//model.Groups[g].BetaHedgeEnable = BetaHedgeEnable.IsChecked == true;
				//model.Groups[g].BetaMaxEnable = BetaMaxEnable.IsChecked == true;
				//model.Groups[g].BetaTTestEnable = BetaTTestEnable.IsChecked == true;

				model.FreqPeriodEnable = FreqPeriodCheckbox.IsChecked == true;
				model.Groups[_g].Leverage = int.Parse(MaxUnits.Text);
				model.FreqPeriod = int.Parse(FreqPeriod.Text);

				model.InitialPortfolioBalance = double.Parse(PortBal2.Text.Replace("$", "").Replace(",", ""));
				model.CurrentPortfolioBalance = double.Parse(PortBal2.Text.Replace("$", "").Replace(",", ""));

				model.Ranking = UserFactorModelRanking.Content as string;
				model.ManagementFee = double.Parse(UserFactorModelManagementFee.Text);
				model.PerformanceFee = double.Parse(UserFactorModelPerformanceFee.Text);
				model.PriceImpactAmt = double.Parse(PriceImpactPercent.Text);

				Model.PortfolioWeightType weightType = Model.PortfolioWeightType.Equal;
				if (MktCapWeight.IsChecked == true) weightType = Model.PortfolioWeightType.MarketCap;
				else if (EqualWeight.IsChecked == true) weightType = Model.PortfolioWeightType.Equal;

				model.PortfolioWeight = weightType;

				model.Filter = UserFactorModelFilter.Text.Replace('\n', '\u0001');

				model.UseRanking = (UseRankingInputs.IsChecked == true);
				model.UseML = (UseML.IsChecked == true);
				model.Groups[_g].UseATMStrategies = (UseATMStrategiesCB.IsChecked == true);
				model.UseFilters = (UseFilters.IsChecked == true);
				model.Groups[_g].Strategy = ATMStrategy.Tag as string;

				model.UseExecutionCost = UseExecutionCost.IsChecked == true;
				model.UseBorrowingCost = UseBorrowingCost.IsChecked == true;
				model.UseDiv = UseDiv.IsChecked == true;

				model.Groups[_g].UseNewTrend = (NewTrendEntry.IsChecked == true);
				model.Groups[_g].UseNTObOs = (UseNTObOs.IsChecked == true);
				model.Groups[_g].UsePressure = (PressureEntry.IsChecked == true);
				model.Groups[_g].UseAdd = (AddEntry.IsChecked == true);
				model.Groups[_g].UseRetrace = (RetraceExit.IsChecked == true);
				model.Groups[_g].UseExhaustion = (ExhaustionExit.IsChecked == true);
				model.Groups[_g].UsePExhaustion = (UsePExit.IsChecked == true);
				model.Groups[_g].UseProveIt = (UseProveIt.IsChecked == true);

				model.Groups[_g].UseTimeExit = (UseTimeExit.IsChecked == true);
				model.Groups[_g].BarsToExit = !model.Groups[_g].UseTimeExit || BarsToExit.Text.Length == 0 ? model.Groups[_g].BarsToExit : int.Parse(BarsToExit.Text);
				model.Groups[_g].PercentBarsToExit = !model.Groups[_g].UseTimeExit || PercentBarsToExit.Text.Length == 0 ? model.Groups[_g].PercentBarsToExit : double.Parse(PercentBarsToExit.Text);

				model.Groups[_g].UseTwoBar = (UseTwoBar.IsChecked == true);
				model.Groups[_g].TwoBarPercent = !model.Groups[_g].UseTwoBar || TwoBarPercent.Text.Length == 0 ? model.Groups[_g].TwoBarPercent : double.Parse(TwoBarPercent.Text);

				model.Groups[_g].UseATRRisk = (UseATRRisk.IsChecked == true);
				model.Groups[_g].ATRRiskFactor = !model.Groups[_g].UseATRRisk || ATRRiskFactor.Text.Length == 0 ? model.Groups[_g].ATRRiskFactor : double.Parse(ATRRiskFactor.Text);
				model.Groups[_g].ATRRiskPeriod = !model.Groups[_g].UseATRRisk || ATRRiskPeriod.Text.Length == 0 ? model.Groups[_g].ATRRiskPeriod : int.Parse(ATRRiskPeriod.Text);

				model.Groups[_g].UseFTRoc = UseFTRoc.IsChecked == true;
				model.Groups[_g].FTRocPercent = !model.Groups[_g].UseFTRoc || FTRocPercent.Text.Length == 0 ? model.Groups[_g].FTRocPercent : double.Parse(FTRocPercent.Text);

				//model.UseCoolDown = (UseCoolDown.IsChecked == true);
				//model.CoolDownPeriod = !model.UseCoolDown || CoolDownPeriod.Text.Length == 0 ? model.CoolDownPeriod : int.Parse(CoolDownPeriod.Text);

				//model.RiskInterval = RiskInterval.Content as string;

				model.UseRiskEngine2 = (UseRiskEngine2.IsChecked == true);
				model.UseRiskEngine3 = (UseRiskEngine3.IsChecked == true);
				//model.UseRiskEngine4 = (UseRiskEngine4.IsChecked == true);
				//model.RiskBudget = !model.UseRiskEngine || RiskBudget.Text.Length == 0 ? model.RiskBudget : double.Parse(RiskBudget.Text);

				model.UsePortRisk1 = UsePortRiskL1.IsChecked == true;
				//model.PortRiskPercent1 = !model.UsePortRisk1 || PortRiskPercentL1.Text.Length == 0 ? model.PortRiskPercent1 : double.Parse(PortRiskPercentL1.Text);

				//model.UsePortRisk2 = UsePortRiskL2.IsChecked == true;
				//model.PortRiskPercent2 = !model.UsePortRisk2 || PortRiskPercentL2.Text.Length == 0 ? model.PortRiskPercent2 : double.Parse(PortRiskPercentL2.Text);

				//model.UsePortRisk3 = UsePortRiskL3.IsChecked == true;
				//model.PortRiskPercent3 = !model.UsePortRisk3 || PortRiskPercentL3.Text.Length == 0 ? model.PortRiskPercent3 : double.Parse(PortRiskPercentL3.Text);

				//model.UseTradeRisk = UseTradeRisk.IsChecked == true;
				//model.TradeRiskPercent = !model.UseTradeRisk || TradeRiskPercent.Text.Length == 0 ? model.TradeRiskPercent : double.Parse(TradeRiskPercent.Text);
				model.Groups[_g].UseConviction = UseConviction.IsChecked == true;
				model.Groups[_g].ConvictionPercent = !model.Groups[_g].UseConviction || ConvictionPercent.Text.Length == 0 ? model.Groups[_g].ConvictionPercent : double.Parse(ConvictionPercent.Text);
				//model.UseConviction = (UseConviction.IsChecked == true);
				//model.ConvictionPercent = !model.UseConviction || ConvictionPercent.Text.Length == 0 ? 0 : double.Parse(ConvictionPercent.Text);

				//model.UseHedge = UseHedge.IsChecked == true;

				//model.Groups[g].UseSTTSB = UseSTTSB.IsChecked == true;
				//model.Groups[g].UseSTFT = UseSTFT.IsChecked == true;
				//model.Groups[g].UseSTST = UseSTST.IsChecked == true;
				//model.Groups[g].UseSTSC = UseSTSC.IsChecked == true;

				//model.Groups[g].MTNT =
				//                UseMTNT.IsChecked == true ? 1 :
				//                UseNTPlus1.IsChecked == true ? 2 :
				//                UseMTSecNT.IsChecked == true ? 3 : 0;

				//model.Groups[g].MTP =
				//    UseMTP.IsChecked == true ? 1 :
				//    UsePPlus1.IsChecked == true ? 2 :
				//    UseMTSecP.IsChecked == true ? 3 : 0;

				//model.Groups[g].MTFT =
				//    UseMTFT.IsChecked == true ? 1 :
				//    UseFTPlus1.IsChecked == true ? 2 :
				//    UseMTSecFT.IsChecked == true ? 3 : 0;

				model.Groups[_g].NewTrendUnit = !model.Groups[_g].UseNewTrend || NewTrendUnit.Text.Length == 0 ? model.Groups[_g].NewTrendUnit : double.Parse(NewTrendUnit.Text);
				model.Groups[_g].PressureUnit = !model.Groups[_g].UsePressure || PressureUnit.Text.Length == 0 ? model.Groups[_g].PressureUnit : double.Parse(PressureUnit.Text);
				model.Groups[_g].AddUnit = !model.Groups[_g].UseAdd || AddUnit.Text.Length == 0 ? model.Groups[_g].AddUnit : double.Parse(AddUnit.Text);
				model.Groups[_g].RetracePercent = !model.Groups[_g].UseRetrace || RetracePercent.Text.Length == 0 ? model.Groups[_g].RetracePercent : double.Parse(RetracePercent.Text);
				model.Groups[_g].ExhaustionPercent = !model.Groups[_g].UseExhaustion || ExhaustionPercent.Text.Length == 0 ? model.Groups[_g].ExhaustionPercent : double.Parse(ExhaustionPercent.Text);
				model.Groups[_g].PExhaustionPercent = !model.Groups[_g].UsePExhaustion || PExitPercent.Text.Length == 0 ? model.Groups[_g].PExhaustionPercent : double.Parse(PExitPercent.Text);

				//model.rbNextBarVWAP = UseVWAP.IsChecked == true;

				//model.UseATMTimingLong = (cbSTNetLong.IsChecked == true);
				// model.UseATMTimingShort = (cbSTNetShort.IsChecked == true);

				model.GrossLeverage = int.Parse(GrossLeverage.Text);

				model.UseCommission = true;  // (CBUseCommission.IsChecked == true);  // bool
				model.CommissionAmt = int.Parse(CommissionAmt.Text); // int

				var text1 = FeatureEditor1.Text;
				var text2 = FeatureEditor2.Text;
				string[] lines1 = text1.Split('\n');
				string[] lines2 = text2.Split('\n');
				model.FeatureNames.Clear();
				var cnt = Math.Min(lines1.Length, lines2.Length);
				for (var ii = 0; ii < cnt; ii++)
				{
					if (lines1[ii].Length > 0 && lines2[ii].Length > 0)
					{
						model.FeatureNames.Add(lines1[ii] + "\u0002" + lines2[ii]);
					}
				}

				model.UsePriceImpactAmt = true; // (CBPriceImpactPercent.IsChecked == true); model.LongNet = (LongNet1.IsChecked == true) ? 1 : (LongNet2.IsChecked == true) ? 2 : 3;
				model.ShortNet = (ShortNet1.IsChecked == true) ? -1 : (ShortNet2.IsChecked == true) ? -2 : -3;

				model.UseStopLossPercent = true;  // (cbStopLoss.IsChecked == true);

				var st = UserFactorModelTrainStartDate.SelectedDate;
				var et = UserFactorModelTrainEndDate.Text.Length == 0 ? null : UserFactorModelTrainEndDate.SelectedDate;

				// Read live/test mode from radio buttons and apply correct date semantics.
				// For live mode: picker shows LiveStartDate; DataRange.Time1 is pushed back 90 days.
				// For test mode: picker is DataRange.Time1 directly.
				bool isLiveFromUI = LiveTrade.IsChecked == true;
				model.IsLiveMode = isLiveFromUI;
				if (st != null)
				{
					if (isLiveFromUI)
					{
						model.LiveStartDate = st.Value;
						model.DataRange.Time1 = st.Value.AddDays(-90);
					}
					else
					{
						model.DataRange.Time1 = st.Value;
					}
				}
				else
				{
					model.DataRange.Time1 = DateTime.UtcNow - new TimeSpan(500, 0, 0, 0);
				}
				model.DataRange.Time2 = (et != null) ? et.Value : DateTime.UtcNow;

				model.SetTimeRanges();

				model.Groups[_g].Direction = (LongOnly.IsChecked == true) ? 1 : (ShortOnly.IsChecked == true) ? -1 : 0;
			}
		}

		public string InitialPortfolioBalance
		{
			get
			{
				return _userFactorModels != null && _userFactorModels.ContainsKey(_selectedUserFactorModel) ? _userFactorModels[_selectedUserFactorModel].InitialPortfolioBalance.ToString() : "0";
			}

			set
			{
				if (_userFactorModels != null && _userFactorModels.ContainsKey(_selectedUserFactorModel))
				{
					_userFactorModels[_selectedUserFactorModel].InitialPortfolioBalance = double.Parse(value);
				}

			}
		}

		private Grid MakeGroupHeader(string text)
		{
			var header = new Grid();
			header.Background = new SolidColorBrush(Color.FromRgb(0x0d, 0x2b, 0x45));
			header.Width = 117;
			header.Margin = new Thickness(0);
			header.HorizontalAlignment = HorizontalAlignment.Center;
			header.PreviewMouseDown += (s, e) => e.Handled = true;
			var tb = new TextBlock();
			tb.Text = text;
			tb.Foreground = Brushes.White;
			tb.FontFamily = new FontFamily("Helvetica Neue");
			tb.FontSize = 11;
			tb.FontWeight = FontWeights.Normal;
			tb.Padding = new Thickness(3, 3, 0, 3);
			header.Children.Add(tb);
			return header;
		}

		private void updateUserFactorModelList()
		{
			var selectedModel = _selectedUserFactorModel;
			var allNames = new List<string>(_userFactorModels.Keys);
			var liveNames = allNames.Where(n => _userFactorModels[n].IsLiveMode).OrderBy(n => n).ToList();
			// Only Admin and Compliance can see TEST portfolios
			var testNamesAll = allNames.Where(n => !_userFactorModels[n].IsLiveMode).OrderBy(n => n).ToList();
			var testNames = ATMML.Auth.AuthContext.Current.CanAccessTestPortfolios
				? testNamesAll : new List<string>();
			//Example.Visibility = (allNames.FindIndex(x => x == "EXAMPLE") == -1) ? Visibility.Visible : Visibility.Hidden;
			updateModelList(selectedModel, liveNames, testNames, UserFactorModelPanel1, false);
			updateModelList(selectedModel, liveNames, testNames, UserFactorModelPanel2, true);
			updateModelList(selectedModel, liveNames, testNames, UserFactorModelPanel3, true);
			updateModelList(selectedModel, liveNames, testNames, UserFactorModelPanel4, true);
			updateModelListBlue(selectedModel, liveNames, testNames, UserFactorModelPanelCompare, true);
		}

		private void updateModelList(string selectedModel, List<string> liveNames, List<string> testNames, ListBox input, bool readOnly)
		{
			input.Items.Clear();
			var groups = new (string Label, List<string> Names)[] { ("LIVE", liveNames), ("TEST", testNames) };
			foreach (var (groupLabel, modelNames) in groups)
			{
				if (modelNames.Count == 0) continue;
				input.Items.Add(MakeGroupHeader(groupLabel));
				foreach (var name in modelNames)
				{
					if (readOnly)
					{
						var panel = new Grid();

						var col1 = new ColumnDefinition();
						col1.Width = new GridLength(1, GridUnitType.Star);

						panel.ColumnDefinitions.Add(col1);

						var lb = new Label();
						lb.Content = name;
						lb.Tag = name;
						lb.Background = Brushes.Transparent;
						lb.Foreground = name == selectedModel ? new SolidColorBrush(Color.FromRgb(0x00, 0xcc, 0xff)) : Brushes.White;
						lb.BorderBrush = Brushes.Transparent;
						lb.PreviewMouseDown += Tb_PreviewMouseDown;
						lb.Height = 18;
						lb.Padding = new Thickness(0, 2, 0, 2);
						lb.Margin = new Thickness(2, 0, 0, 0);
						lb.HorizontalAlignment = HorizontalAlignment.Left;
						lb.VerticalAlignment = VerticalAlignment.Center;
						lb.MouseEnter += Portfolio_MouseEnter;
						lb.MouseLeave += Portfolio_MouseLeave_SelectedModel;
						lb.FontFamily = new FontFamily("Helvetica Neue");
						lb.FontWeight = FontWeights.Normal;
						lb.FontSize = 11;
						lb.Cursor = Cursors.Hand;
						panel.Children.Add(lb);

						if (name == selectedModel)
						{
							input.SelectedItem = panel;
						}

						input.Items.Add(panel);

					}
					else
					{
						var panel = new Grid();

						var col1 = new ColumnDefinition();
						col1.Width = new GridLength(1, GridUnitType.Star);
						var col2 = new ColumnDefinition();
						col2.Width = new GridLength(1, GridUnitType.Auto);

						panel.ColumnDefinitions.Add(col1);
						panel.ColumnDefinitions.Add(col2);


						var tb = new TextBox();
						tb.Text = name;
						tb.Tag = name;
						tb.Background = Brushes.Transparent;
						tb.Foreground = name == selectedModel ? new SolidColorBrush(Color.FromRgb(0x00, 0xcc, 0xff)) : Brushes.White;
						tb.BorderBrush = Brushes.Transparent;
						tb.BorderThickness = new Thickness(0);
						tb.MouseEnter += Portfolio_MouseEnter;
						tb.MouseLeave += Portfolio_MouseLeave_SelectedModel;
						tb.PreviewMouseDown += Tb_PreviewMouseDown;
						tb.Height = 18;
						tb.Padding = new Thickness(0, 2, 0, 2);
						tb.Margin = new Thickness(2, 0, 0, 0);
						tb.HorizontalAlignment = HorizontalAlignment.Left;
						tb.VerticalAlignment = VerticalAlignment.Center;
						tb.FontFamily = new FontFamily("Helvetica Neue");
						tb.FontWeight = FontWeights.Normal;
						tb.FontSize = 11;
						tb.Cursor = Cursors.Hand;
						tb.Width = 90;
						tb.CharacterCasing = CharacterCasing.Upper;
						panel.Children.Add(tb);

						if (name == selectedModel)
						{
							input.SelectedItem = panel;
						}

						input.Items.Add(panel);
					}
				}
			}
		}


		private void updateModelListBlue(string selectedModel, List<string> liveNames, List<string> testNames, ListBox input, bool readOnly)
		{

			input.Items.Clear();
			var groups = new (string Label, List<string> Names)[] { ("LIVE", liveNames), ("TEST", testNames) };
			foreach (var (groupLabel, modelNames) in groups)
			{
				if (modelNames.Count == 0) continue;
				input.Items.Add(MakeGroupHeader(groupLabel));
				foreach (var name in modelNames)
				{
					if (readOnly)
					{
						var panel = new Grid();

						var col1 = new ColumnDefinition();
						col1.Width = new GridLength(1, GridUnitType.Star);

						panel.ColumnDefinitions.Add(col1);

						var lb = new Label();
						lb.Content = name;
						lb.Background = Brushes.Transparent;
						lb.Foreground = Brushes.White;
						//lb.Foreground = lb.Foreground = new SolidColorBrush(Color.FromArgb(0xff, 0xff, 0x8c, 00));
						lb.BorderBrush = Brushes.Transparent;
						lb.PreviewMouseDown += Tb_PreviewMouseDown;
						lb.Height = 18;
						lb.Padding = new Thickness(0, 2, 0, 2);
						lb.Margin = new Thickness(4, 0, 0, 0);
						lb.HorizontalAlignment = HorizontalAlignment.Left;
						lb.VerticalAlignment = VerticalAlignment.Center;
						lb.MouseEnter += PortfolioBlue_MouseLeave2;
						lb.MouseLeave += PortfolioWhite_MouseEnter;
						//lb.MouseLeave += PortfolioOrg_MouseLeave2;
						lb.FontFamily = new FontFamily("Helvetica Neue");
						lb.FontWeight = FontWeights.Normal;
						lb.FontSize = 11;
						lb.Cursor = Cursors.Hand;
						panel.Children.Add(lb);

						if (name == selectedModel)
						{
							input.SelectedItem = panel;
						}

						input.Items.Add(panel);

					}
				}
			}
		}


		private void updateModelListOrange(string selectedModel, List<string> modelNames, ListBox input, bool readOnly)
		{

			input.Items.Clear();
			modelNames.Sort();
			foreach (var name in modelNames)
			{
				if (readOnly)
				{
					var panel = new Grid();

					var col1 = new ColumnDefinition();
					col1.Width = new GridLength(1, GridUnitType.Star);

					panel.ColumnDefinitions.Add(col1);

					var lb = new Label();
					lb.Content = name;
					lb.Background = Brushes.Transparent;
					lb.Foreground = Brushes.White;
					lb.BorderBrush = Brushes.Transparent;
					lb.PreviewMouseDown += Tb_PreviewMouseDown;
					lb.Height = 18;
					lb.Padding = new Thickness(0, 2, 0, 2);
					lb.Margin = new Thickness(4, 0, 0, 0);
					lb.HorizontalAlignment = HorizontalAlignment.Left;
					lb.VerticalAlignment = VerticalAlignment.Center;
					lb.MouseEnter += PortfolioBlue_MouseLeave2;
					lb.MouseLeave += PortfolioWhite_MouseEnter;
					//lb.MouseLeave += PortfolioBlue_MouseLeave2;
					lb.FontFamily = new FontFamily("Helvetica Neue");
					lb.FontWeight = FontWeights.Normal;
					lb.FontSize = 11;
					lb.Cursor = Cursors.Hand;
					panel.Children.Add(lb);

					if (name == selectedModel)
					{
						input.SelectedItem = panel;
					}

					input.Items.Add(panel);

				}
			}
		}

		private void Icon_MouseLeave(object sender, MouseEventArgs e)
		{
			Label icon = sender as Label;
			icon.Foreground = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));
		}

		private void Icon_MouseEnter(object sender, MouseEventArgs e)
		{
			Label icon = sender as Label;
			icon.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xcc, 0xff));
		}

		private void Tb_PreviewMouseDown(object sender, MouseButtonEventArgs e)
		{
			FrameworkElement fe = sender as FrameworkElement;
			if (_useUserFactorModel)
			{
				UserFactorModelPanel1.SelectedItem = fe.Parent;
			}
			else
			{
				//                MLFactorModelPanel1.SelectedItem = fe.Parent;
			}
		}

		private void DeleteUserFactorModel_Mousedown(object sender, MouseButtonEventArgs e)
		{
			saveModel();

			var model = getModel();
			if (model != null)
			{
				var name = model.Name;
				MessageBoxResult result = System.Windows.MessageBox.Show("Are you sure you wish to delete " + name + "?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
				if (result == MessageBoxResult.Yes && _userFactorModels.Keys.Count > 0)
				{
					if (name == "EXAMPLE")
					{
						//Example.Visibility = Visibility.Visible;
					}

					deleteModel(name);
					deleteModelData(name);

					List<string> list1 = _userFactorModels.Keys.ToList();
					list1.Sort();
					name = list1[0];
					int index = list1.IndexOf(_selectedUserFactorModel);
					if (index >= 0)
					{
						list1.Remove(_selectedUserFactorModel);
						if (index >= list1.Count) index = 0;

						_userFactorModels.Remove(_selectedUserFactorModel);
						name = (index < list1.Count) ? list1[index] : "";
					}
					changeUserFactorModel(name);
					updateUserFactorModelList();
					hideNavigation();

					//saveUserFactorModels();
				}
			}
		}

		private void RemoveFilter(object sender, MouseEventArgs e)
		{
			var editor = UserFactorModelFilter;

			var index = UserFactorModelFilter.SelectionStart;

			string text = "";
			string[] lines = editor.Text.Split('\n');

			for (int ii = 0; ii < lines.Length; ii++)
			{
				if (lines[ii].Length > 0)
				{
					if (text.Length <= index && index < text.Length + lines[ii].Length)
					{
						index = -1;
					}
					else
					{
						if (text.Length > 0) text += "\n";
						text += lines[ii];
					}
				}
			}
			editor.Text = text;
		}

		private void RemoveFactorInput(object sender, MouseEventArgs e)
		{
			//var editor = _useUserFactorModel ? UserFactorModelEditor : MLFactorModelEditor;
			var editor = UserFactorModelEditor;

			var index = UserFactorModelEditor.SelectionStart;

			string text = "";
			string[] lines = editor.Text.Split('\n');

			for (int ii = 0; ii < lines.Length; ii++)
			{
				if (lines[ii].Length > 0)
				{
					if (text.Length <= index && index < text.Length + lines[ii].Length)
					{
						index = -1;
					}
					else
					{
						if (text.Length > 0) text += "\n";
						text += lines[ii];
					}
				}
			}
			editor.Text = text;
		}

		private void AddFilter(object sender, RoutedEventArgs e)
		{
			if (_useUserFactorModel)
			{
				string filter = getCondition(ConditionTree);
				string text = UserFactorModelFilter.Text;
				if (text.Length > 0) text += "\n";
				text += filter;
				UserFactorModelFilter.Text = text;
				if (_userFactorModels.ContainsKey(_selectedUserFactorModel))
				{
					_userFactorModels[_selectedUserFactorModel].Filter = text.Replace('\n', '\u0001');
				}
			}
		}
		private void AddFactorInput(object sender, RoutedEventArgs e)
		{
			if (_useUserFactorModel)
			{
				string factor = getFactor(UserFactorInputTree);
				string text = UserFactorModelEditor.Text;
				if (text.Length > 0) text += "\n";
				text += factor;
				UserFactorModelEditor.Text = text;

				if (_userFactorModels.ContainsKey(_selectedUserFactorModel))
				{
					_userFactorModels[_selectedUserFactorModel].FactorNames.Clear();
					text = UserFactorModelEditor.Text;
					string[] lines = text.Split('\n');
					foreach (var line in lines)
					{
						_userFactorModels[_selectedUserFactorModel].FactorNames.Add(line);
					}
				}
			}
		}
		private void UserFactorModel_SelectionChanged2(object sender, SelectionChangedEventArgs e)
		{
			string model = "";
			ListBox listBox = sender as ListBox;

			Grid grid = listBox.SelectedItem as Grid;
			if (grid != null)
			{
				TextBox tb1 = grid.Children[0] as TextBox;
				if (tb1 != null) model = tb1.Text;
				Label lb1 = grid.Children[0] as Label;
				if (lb1 != null) model = lb1.Content as string;
			}

			if (model.Length > 0 && _useUserFactorModel)
			{
				_selectedUserFactorModel2 = model;

				//clearStats();
				clearStats2();
				_update = 15;
			}


			if (_portfolioTimes.Count > 0)
			{
				var date = _portfolioTimes.Last();
				setHoldingsCursorTime(date);
			}
		}

		private void UserFactorModel_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			string model = "";
			ListBox listBox = sender as ListBox;

			Grid grid = listBox.SelectedItem as Grid;
			if (grid != null)
			{
				TextBox tb1 = grid.Children[0] as TextBox;
				if (tb1 != null) model = tb1.Text;
				Label lb1 = grid.Children[0] as Label;
				if (lb1 != null) model = lb1.Content as string;
			}

			if (model.Length > 0 && _useUserFactorModel)
			{
				changeUserFactorModel(model);
				_update = 15;
			}

			if (_portfolioTimes.Count > 0)
			{
				var date = _portfolioTimes.Last();
				setHoldingsCursorTime(date);
			}
		}

		bool _forAlphaSymbols = true;

		private void Level0_MouseDown(object sender, RoutedEventArgs e)
		{
			var label = sender as Label;
			//_forAlphaSymbols = (label.Content as string).Contains("ALPHA");

			if (Level1Panel.Visibility == Visibility.Collapsed)
			{
				hideView();
				hideNavigation();

				Menus.Visibility = Visibility.Visible;

				Level1Panel.Visibility = Visibility.Visible;
				BenchmarkMenu1.Visibility = Visibility.Visible;

				MLIntervalsMenu.Visibility = Visibility.Collapsed;

				RankingMenu.Visibility = Visibility.Collapsed;
				RebalanceMenu.Visibility = Visibility.Collapsed;
				AlignmentIntervalMenu.Visibility = Visibility.Collapsed;
				AutoMLIntervalMenu.Visibility = Visibility.Collapsed;
				ATMStrategyMenu.Visibility = Visibility.Collapsed;
				FeaturesMenu.Visibility = Visibility.Collapsed;

				ATMTimingMenus.Visibility = Visibility.Collapsed;
				ScenarioMenu1.Visibility = Visibility.Collapsed;
				PortfolioSettings.Visibility = Visibility.Collapsed;


				MLRankMenu.Visibility = Visibility.Collapsed;
				MLIterationMenu.Visibility = Visibility.Collapsed;

				Level2Panel.Visibility = Visibility.Visible;
				Level3Panel.Visibility = Visibility.Visible;
				Level4Panel.Visibility = Visibility.Visible;
				SubgroupMenuScroller1.Visibility = Visibility.Visible;
				Level5Panel.Visibility = Visibility.Visible;
				IndustryMenuScroller1.Visibility = Visibility.Visible;

				SubgroupActionMenu1.Visibility = Visibility.Collapsed;
				SubgroupActionMenu2.Visibility = Visibility.Collapsed;

				FilterMenu.Visibility = Visibility.Collapsed;
				InputMenu.Visibility = Visibility.Collapsed;
				PortfolioSettings.Visibility = Visibility.Collapsed;

				SetupNav.Visibility = Visibility.Collapsed;

				var model = getModel();
				if (model != null)
				{
					nav.ClearPortfolio();
					_selectedRegion = model.Groups[_g].Levels[0][0];
					_selectedCountry = model.Groups[_g].Levels[1][0];
					_selectedGroup = model.Groups[_g].Levels[2][0];
					_selectedSubgroup = model.Groups[_g].Levels[3][0];
					_selectedIndustry = model.Groups[_g].Levels[4][0];
					nav.SetNavigationPaths(model.TickerNavigationPaths);

					nav.SetPortfolio(model.Groups[_g].AlphaSymbols.Select(x => x.Ticker).ToList());
				}

				_universeSelection = true;
				_benchmarkSelection = false;

				nav.UseCheckBoxes = true;

				//if (MainView.EnablePortfolio)
				{
					List<string> items = new List<string>();
					if (BarServer.ConnectedToBloomberg() || !BarServer.ConnectedToCQG()) items.AddRange(new string[] { "BLOOMBERG >", " ", "COMMODITIES >", " ", "EQ | AMERICAS >", " ", "EQ | ASIA PACIFIC >", " ", "EQ | EUROPE & MEA >", " ", "ETF >", " ", "FX & CRYPTO >", " ", "GLOBAL FUTURES >", " ", "INTEREST RATES >" });
					if (BarServer.ConnectedToCQG()) items.AddRange(new string[] { " ", "CQG COMMODITIES >", " ", "CQG EQUITIES >", " ", "CQG ETF >", " ", "CQG FX & CRYPTO >", " ", "CQG INTEREST RATES >", " ", "CQG STOCK INDICES >" });

					nav.UseCheckBoxes = true;
					nav.setNavigation(Level1Panel, Level1_MouseDown, items.ToArray());

					var panel = new StackPanel();
					panel.Orientation = Orientation.Horizontal;
					var label1 = new Label();
					label1.Background = new SolidColorBrush(Color.FromRgb(0x30, 0x30, 0x30));
					label1.BorderBrush = new SolidColorBrush(Color.FromRgb(0x30, 0x30, 0x30));
					label1.BorderThickness = new Thickness(1);
					label1.Foreground = Brushes.White;
					label1.Height = 20;
					label1.Padding = new Thickness(10, 2, 0, 2);
					label1.HorizontalAlignment = HorizontalAlignment.Center;
					label1.VerticalAlignment = VerticalAlignment.Bottom;
					label1.FontFamily = new FontFamily("Helvetica Neue");
					label1.FontWeight = FontWeights.Normal;
					label1.FontSize = 11;
					label1.Cursor = Cursors.Hand;
					label1.Width = 45;
					label1.Margin = new Thickness(0, 20, 5, 0);
					label1.Content = "Save";
					label1.MouseEnter += Mouse_Enter;
					label1.MouseLeave += Mouse_Leave;
					label1.MouseDown += Save_Click;
					panel.Children.Add(label1);

					var label2 = new Label();
					label2.Background = new SolidColorBrush(Color.FromRgb(0x30, 0x30, 0x30));
					label2.BorderBrush = new SolidColorBrush(Color.FromRgb(0x30, 0x30, 0x30));
					label2.BorderThickness = new Thickness(1);
					label2.Foreground = Brushes.White;
					label2.Height = 20;
					label2.Padding = new Thickness(6, 2, 0, 2);
					label2.HorizontalAlignment = HorizontalAlignment.Center;
					label2.VerticalAlignment = VerticalAlignment.Bottom;
					label2.FontFamily = new FontFamily("Helvetica Neue");
					label2.FontWeight = FontWeights.Normal;
					label2.FontSize = 11;
					label2.Cursor = Cursors.Hand;
					label2.Width = 45;
					label2.Margin = new Thickness(5, 20, 0, 0);
					label2.Content = "Close";
					label2.MouseEnter += Mouse_Enter;
					label2.MouseLeave += Mouse_Leave;
					label2.MouseDown += Close_Click;
					panel.Children.Add(label2);
					Level1Panel.Children.Add(panel);
				}

				if (model != null)
				{
					var level0 = model.Groups[_g].Levels[0][0]; // region
					var level1 = model.Groups[_g].Levels[1][0]; // country
					var level2 = model.Groups[_g].Levels[2][0]; // group
					var level3 = model.Groups[_g].Levels[3][0]; // sub-group
					var level4 = model.Groups[_g].Levels[4][0]; // industry
					var level5 = model.Groups[_g].Levels[5][0]; // sub-industry

					if (!nav.setNavigationLevel1(level0, Level2Panel, Level2_MouseDown, go_Click))
					{
						Level3Panel.Visibility = Visibility.Collapsed;
						Level4Panel.Visibility = Visibility.Collapsed;
						Level5Panel.Visibility = Visibility.Collapsed;
						Level6Panel.Visibility = Visibility.Collapsed;
						requestMembers(level0, Level2Panel);
					}
					else if (!nav.setNavigationLevel2(level1, Level3Panel, Level3_MouseDown, go_Click))
					{
						Level4Panel.Visibility = Visibility.Collapsed;
						Level5Panel.Visibility = Visibility.Collapsed;
						Level6Panel.Visibility = Visibility.Collapsed;
						requestMembers(level1, Level3Panel);
					}
					else if (!nav.setNavigationLevel3(level1, level2, Level4Panel, Level4_MouseDown))
					{
						Level5Panel.Visibility = Visibility.Collapsed;
						Level6Panel.Visibility = Visibility.Collapsed;
						requestMembers(level2, Level4Panel);
					}
					else if (!nav.setNavigationLevel4(level1, level2, level3, Level5Panel, Level5_MouseDown))
					{
						Level6Panel.Visibility = Visibility.Collapsed;
						requestMembers(level3, Level5Panel);
					}
					else if (!nav.setNavigationLevel5(level1, level2, level4, Level6Panel, Level6_MouseDown))
					{
						var n = level4.Split(':');
						var ticker = n[0];
						_memberIndex = ticker;
						model.Groups[_g].Levels[5][0] = ticker;
						var name = getPortfolioName();
						Level6Panel.Visibility = Visibility.Visible;
						SymbolMenuScroller1.Visibility = Visibility.Visible;
						requestMembers(name, Level6Panel);
					}
				}

				if (model != null)
				{
					highlightButton(Level1Panel, model.Groups[_g].Levels[0][0]);
					highlightButton(Level2Panel, model.Groups[_g].Levels[1][0]);
					highlightButton(Level3Panel, model.Groups[_g].Levels[2][0]);
					highlightButton(Level4Panel, model.Groups[_g].Levels[3][0]);
					highlightButton(Level5Panel, model.Groups[_g].Levels[4][0]);

					var path = model.TickerNavigationPaths.Split("\n").First();
					if (path != null)
					{
						var fields = path.Split("\t");
						var f0 = fields.Length > 0 ? fields[0].Split(':') : null;
						var f1 = fields.Length > 1 ? fields[1].Split(':') : null;
						var f2 = fields.Length > 2 ? fields[2].Split(':') : null;
						var f3 = fields.Length > 3 ? fields[3].Split(':') : null;
						var f4 = fields.Length > 4 ? fields[4].Split(':') : null;

						if (f0 != null) setLevel1(f0.Last());
						if (f1 != null) setLevel2(f1.Last(), f1.First());
						if (f2 != null) setLevel3(fields[2], f2.First() + " Index");
						if (f3 != null) setLevel4(fields[3], f3.First() + " Index");
						if (f4 != null) setLevel5(fields[4], f4.First() + " Index");
					}

				}

				string resourceKey = "";

				if (resourceKey.Length > 0)
				{
					Viewbox legend = this.Resources[resourceKey] as Viewbox;
					if (legend != null)
					{
						legend.SetValue(MarginProperty, new Thickness(0, 20, 0, 0));
						Level1Panel.Children.Add(legend);
					}
				}
			}
			else
			{
				showView();
			}
		}

		private void setCheckBoxes(Model model)
		{
			nav.SetCheckBoxes();
		}

		private void Close_Click(object sender, RoutedEventArgs e)
		{
			Label label = sender as Label;
			hideNavigation();
			showView();
		}

		private void ATMStrategyClose_Click(object sender, RoutedEventArgs e)
		{
			foreach (var node in _atmStrategyTree.Children)
			{
				if (node.IsSelected)
				{
					var strategy = node.GetDescription().Replace("\u0002", "");
					var strategyFields = strategy.Split('(');
					ATMStrategy.Content = strategyFields[0];
					ATMStrategy.Tag = strategy;
					break;
				}
			}
			Label label = sender as Label;
			hideNavigation();
			showView();
		}

		private void CloseFilter_MouseDown(object sender, RoutedEventArgs e)
		{

			UserFactorModelFilter1.Text = UserFactorModelFilter.Text;
			hideNavigation();
			showView();
		}

		private void FactorCloseEditor_MouseDown(object sender, RoutedEventArgs e)
		{

			UserFactorModelEditor1.Text = UserFactorModelEditor.Text;
			hideNavigation();
			showView();
		}

		private void FeatureCloseEditor_MouseDown(object sender, RoutedEventArgs e)
		{

			var text = getFeatures(_atmMLTree);
			var atmConditions = text.Split('\n').ToList();
			var model = getModel();

			if (_for_hedge_conditions)
			{
				model.Groups[_g].ATMConditionsHedge = atmConditions;
				OnPropertyChanged("ATMConditionsHedge");
			}
			else if (_setupEnter)
			{
				model.Groups[_g].ATMConditionsEnter = atmConditions;
				OnPropertyChanged("ATMConditionsEnter");
			}
			else
			{
				model.Groups[_g].ATMConditionsExit = atmConditions;
				OnPropertyChanged("ATMConditionsExit");
			}
			hideNavigation();
			showView();
		}

		private void Mouse_Enter(object sender, MouseEventArgs e)
		{
			Label label = sender as Label;
			label.Background = Brushes.DimGray;
		}
		private void Mouse_Leave(object sender, MouseEventArgs e)
		{
			Label label = sender as Label;
			label.Background = new SolidColorBrush(Color.FromRgb(0x30, 0x30, 0x30));
		}

		private void Save_Click(object sender, RoutedEventArgs e)
		{
			Label label = sender as Label;
			hideNavigation();
			showView();
			var model = getModel();
			if (model != null)
			{
				var universe = nav.GetPortfolio();
				model.TickerNavigationPaths = nav.GetNavigationPaths();

				model.Groups[_g].AlphaSymbols.Clear();

				if (_forAlphaSymbols) model.Groups[_g].AlphaPortfolio = universe;
				else model.Groups[_g].HedgePortfolio = universe;
				//update(_selectedRegion, _selectedCountry, _selectedGroup, _selectedSubgroup, _selectedIndustry);
				//setUniverse(universe);
				requestPortfolio(universe); // save after selection
				userFactorModel_to_ui(_selectedUserFactorModel);
			}
		}

		private bool _universeSelection = false;
		private bool _benchmarkSelection = false;

		private void Benchmark_MouseDown(object sender, RoutedEventArgs e)
		{
			{
				if (BenchmarkMenu1.Visibility == Visibility.Collapsed)
				{
					hideView();
					hideNavigation();

					Menus.Visibility = Visibility.Visible;

					Level1Panel.Visibility = Visibility.Visible;
					BenchmarkMenu1.Visibility = Visibility.Visible;

					ATMTimingMenus.Visibility = Visibility.Collapsed;
					ScenarioMenu1.Visibility = Visibility.Collapsed;
					PortfolioSettings.Visibility = Visibility.Collapsed;

					MLIntervalsMenu.Visibility = Visibility.Collapsed;

					RankingMenu.Visibility = Visibility.Collapsed;
					RebalanceMenu.Visibility = Visibility.Collapsed;
					AlignmentIntervalMenu.Visibility = Visibility.Collapsed;
					AutoMLIntervalMenu.Visibility = Visibility.Collapsed;
					ATMStrategyMenu.Visibility = Visibility.Collapsed;
					FeaturesMenu.Visibility = Visibility.Collapsed;

					MLRankMenu.Visibility = Visibility.Collapsed;
					MLIterationMenu.Visibility = Visibility.Collapsed;

					Level2Panel.Visibility = Visibility.Visible;
					Level3Panel.Visibility = Visibility.Visible;
					Level4Panel.Visibility = Visibility.Visible;
					SubgroupMenuScroller1.Visibility = Visibility.Visible;
					Level5Panel.Visibility = Visibility.Visible;
					IndustryMenuScroller1.Visibility = Visibility.Visible;
					SubgroupActionMenu1.Visibility = Visibility.Visible;
					SubgroupActionMenu2.Visibility = Visibility.Visible;

					FilterMenu.Visibility = Visibility.Collapsed;
					InputMenu.Visibility = Visibility.Collapsed;
					PortfolioSettings.Visibility = Visibility.Collapsed;

					SetupNav.Visibility = Visibility.Collapsed;

					var model = getModel();
					if (model != null)
					{
						_selectedRegion = model.Groups[_g].Levels[0][1];
						_selectedCountry = model.Groups[_g].Levels[1][1];
						_selectedGroup = model.Groups[_g].Levels[2][1];
						_selectedSubgroup = model.Groups[_g].Levels[3][1];
						_selectedIndustry = model.Groups[_g].Levels[4][1];
					}

					_universeSelection = false;
					_benchmarkSelection = true;

					nav.UseCheckBoxes = false;

					//if (MainView.EnablePortfolio)
					{
						List<string> items = new List<string>();

						if (BarServer.ConnectedToBloomberg() || !BarServer.ConnectedToCQG()) items.AddRange(new string[] { "COMMODITIES >", " ", "EQ | AMERICAS >", " ", "EQ | ASIA PACIFIC >", " ", "EQ | EUROPE & MEA >", " ", "FX & CRYPTO >", " ", "INTEREST RATES >" });
						if (BarServer.ConnectedToCQG()) items.AddRange(new string[] { " ", "CQG COMMODITIES >", " ", "CQG EQUITIES >", " ", "CQG ETF >", " ", "CQG FX & CRYPTO >", " ", "CQG INTEREST RATES >", " ", "CQG STOCK INDICES >" });
						if (MainView.EnableGuiPortfolio) items.Add("GUI >");

						nav.UseCheckBoxes = true;
						nav.setNavigation(Level1Panel, Level1_MouseDown, items.ToArray());

						var panel = new StackPanel();
						panel.Orientation = Orientation.Horizontal;
						var label1 = new Label();
						label1.Background = new SolidColorBrush(Color.FromRgb(0x30, 0x30, 0x30));
						label1.BorderBrush = new SolidColorBrush(Color.FromRgb(0x30, 0x30, 0x30));
						label1.BorderThickness = new Thickness(1);
						label1.Foreground = Brushes.White;
						label1.Height = 20;
						label1.Padding = new Thickness(10, 2, 0, 2);
						label1.HorizontalAlignment = HorizontalAlignment.Center;
						label1.VerticalAlignment = VerticalAlignment.Bottom;
						label1.FontFamily = new FontFamily("Helvetica Neue");
						label1.FontWeight = FontWeights.Normal;
						label1.FontSize = 11;
						label1.Cursor = Cursors.Hand;
						label1.Width = 45;
						label1.Margin = new Thickness(0, 20, 5, 0);
						label1.Content = "Save";
						label1.MouseEnter += Mouse_Enter;
						label1.MouseLeave += Mouse_Leave;
						label1.MouseDown += Save_Click;
						panel.Children.Add(label1);

						var label2 = new Label();
						label2.Background = new SolidColorBrush(Color.FromRgb(0x30, 0x30, 0x30));
						label2.BorderBrush = new SolidColorBrush(Color.FromRgb(0x30, 0x30, 0x30));
						label2.BorderThickness = new Thickness(1);
						label2.Foreground = Brushes.White;
						label2.Height = 20;
						label2.Padding = new Thickness(6, 2, 0, 2);
						label2.HorizontalAlignment = HorizontalAlignment.Center;
						label2.VerticalAlignment = VerticalAlignment.Bottom;
						label2.FontFamily = new FontFamily("Helvetica Neue");
						label2.FontWeight = FontWeights.Normal;
						label2.FontSize = 11;
						label2.Cursor = Cursors.Hand;
						label2.Width = 45;
						label2.Margin = new Thickness(5, 20, 0, 0);
						label2.Content = "Close";
						label2.MouseEnter += Mouse_Enter;
						label2.MouseLeave += Mouse_Leave;
						label2.MouseDown += Close_Click;
						panel.Children.Add(label2);
						Level1Panel.Children.Add(panel);
					}

					if (model != null)
					{
						nav.setNavigationLevel1(model.Groups[_g].Levels[0][1], Level2Panel, Level2_MouseDown, go_Click);
						nav.setNavigationLevel2(model.Groups[_g].Levels[1][1], Level3Panel, Level3_MouseDown, go_Click);
						nav.setNavigationLevel3(model.Groups[_g].Levels[1][1], model.Groups[_g].Levels[2][1], Level4Panel, Level4_MouseDown);
						nav.setNavigationLevel4(_selectedCountry, _selectedGroup, model.Groups[_g].Levels[3][1], Level5Panel, Level5_MouseDown);
					}

					var benchmark = (model != null) ? model.Benchmark : "";
					highlightButton(Level1Panel, benchmark);
					highlightButton(Level2Panel, benchmark);
					highlightButton(Level3Panel, benchmark);
					highlightButton(Level4Panel, benchmark);
					highlightButton(Level5Panel, benchmark);
					highlightButton(BenchmarkMenu1, benchmark);

					if (model != null)
					{
						highlightButton(Level1Panel, model.Groups[_g].Levels[0][1]);
						highlightButton(Level2Panel, model.Groups[_g].Levels[1][1]);
						highlightButton(Level3Panel, model.Groups[_g].Levels[2][1]);
						highlightButton(Level4Panel, model.Groups[_g].Levels[3][1]);
						highlightButton(Level5Panel, model.Groups[_g].Levels[4][1]);
					}

					string resourceKey = "";

					if (resourceKey.Length > 0)
					{
						Viewbox legend = this.Resources[resourceKey] as Viewbox;
						if (legend != null)
						{
							legend.SetValue(MarginProperty, new Thickness(0, 20, 0, 0));
							BenchmarkMenu1.Children.Add(legend);
						}
					}
				}
				else
				{
					showView();
				}
			}
		}

		private void setBenchMark(string input)
		{
			string[] fields = input.Split(':');
			string symbol = fields[0] + ((fields[0].Contains(" Index")) ? "" : " Index");

			_benchmarksymbol = symbol;

			if (_useUserFactorModel)
			{
				UserFactorModelBenchmark.Text = _benchmarksymbol;
				if (_userFactorModels.ContainsKey(_selectedUserFactorModel))
				{
					_userFactorModels[_selectedUserFactorModel].Benchmark = _benchmarksymbol;
				}
			}

			showView();
		}

		private void Rebalance_MouseDown(object sender, MouseButtonEventArgs e)
		{
			if (BenchmarkMenu1.Visibility == Visibility.Collapsed)
			{
				hideView();
				hideNavigation();

				Menus.Visibility = Visibility.Visible;

				Level1Panel.Visibility = Visibility.Collapsed;
				BenchmarkMenu1.Visibility = Visibility.Collapsed;

				RankingMenu.Visibility = Visibility.Collapsed;

				MLIntervalsMenu.Visibility = Visibility.Collapsed;
				RebalanceMenu.Visibility = Visibility.Visible;

				AlignmentIntervalMenu.Visibility = Visibility.Collapsed;
				AutoMLIntervalMenu.Visibility = Visibility.Collapsed;
				ATMStrategyMenu.Visibility = Visibility.Collapsed;
				FeaturesMenu.Visibility = Visibility.Collapsed;

				ATMTimingMenus.Visibility = Visibility.Collapsed;
				ScenarioMenu1.Visibility = Visibility.Collapsed;
				PortfolioSettings.Visibility = Visibility.Collapsed;
				MLRankMenu.Visibility = Visibility.Collapsed;
				MLIterationMenu.Visibility = Visibility.Collapsed;

				Level2Panel.Visibility = Visibility.Collapsed;
				Level3Panel.Visibility = Visibility.Collapsed;
				Level4Panel.Visibility = Visibility.Collapsed;
				SubgroupMenuScroller1.Visibility = Visibility.Collapsed;
				Level5Panel.Visibility = Visibility.Collapsed;
				Level6Panel.Visibility = Visibility.Collapsed;
				IndustryMenuScroller1.Visibility = Visibility.Collapsed;
				SubgroupActionMenu1.Visibility = Visibility.Collapsed;
				SubgroupActionMenu2.Visibility = Visibility.Collapsed;

				FilterMenu.Visibility = Visibility.Collapsed;
				InputMenu.Visibility = Visibility.Collapsed;
				PortfolioSettings.Visibility = Visibility.Collapsed;

				SetupNav.Visibility = Visibility.Collapsed;

				nav.UseCheckBoxes = false;
				nav.setNavigation(RebalanceMenu, RankingIntervalSelected_MouseDown, Model.Rebalances);

				var model = getModel();
				var rebalance = (model != null) ? model.RankingInterval : "";
				highlightButton(RebalanceMenu, rebalance);

				string resourceKey = "";

				if (resourceKey.Length > 0)
				{
					Viewbox legend = this.Resources[resourceKey] as Viewbox;
					if (legend != null)
					{
						legend.SetValue(MarginProperty, new Thickness(0, 20, 0, 0));
						BenchmarkMenu1.Children.Add(legend);
					}
				}
			}
			else
			{
				showView();
			}
		}

		private void AlignmentInterval_MouseDown(object sender, MouseButtonEventArgs e)
		{
			if (BenchmarkMenu1.Visibility == Visibility.Collapsed)
			{
				hideView();
				hideNavigation();

				Menus.Visibility = Visibility.Visible;

				Level1Panel.Visibility = Visibility.Collapsed;
				BenchmarkMenu1.Visibility = Visibility.Collapsed;

				RankingMenu.Visibility = Visibility.Collapsed;

				MLIntervalsMenu.Visibility = Visibility.Collapsed;

				RebalanceMenu.Visibility = Visibility.Collapsed;
				AlignmentIntervalMenu.Visibility = Visibility.Visible;
				AutoMLIntervalMenu.Visibility = Visibility.Collapsed;

				ATMStrategyMenu.Visibility = Visibility.Collapsed;
				FeaturesMenu.Visibility = Visibility.Collapsed;

				ATMTimingMenus.Visibility = Visibility.Collapsed;
				ScenarioMenu1.Visibility = Visibility.Collapsed;
				PortfolioSettings.Visibility = Visibility.Collapsed;
				MLRankMenu.Visibility = Visibility.Collapsed;
				MLIterationMenu.Visibility = Visibility.Collapsed;

				Level2Panel.Visibility = Visibility.Collapsed;
				Level3Panel.Visibility = Visibility.Collapsed;
				Level4Panel.Visibility = Visibility.Collapsed;
				SubgroupMenuScroller1.Visibility = Visibility.Collapsed;
				Level5Panel.Visibility = Visibility.Collapsed;
				Level6Panel.Visibility = Visibility.Collapsed;
				IndustryMenuScroller1.Visibility = Visibility.Collapsed;
				SubgroupActionMenu1.Visibility = Visibility.Collapsed;
				SubgroupActionMenu2.Visibility = Visibility.Collapsed;

				FilterMenu.Visibility = Visibility.Collapsed;
				InputMenu.Visibility = Visibility.Collapsed;
				PortfolioSettings.Visibility = Visibility.Collapsed;

				SetupNav.Visibility = Visibility.Collapsed;

				nav.UseCheckBoxes = false;
				nav.setNavigation(AlignmentIntervalMenu, AlignmentIntervalSelected_MouseDown, Model.Alignments);
				addSaveAndCloseButtons(AlignmentIntervalMenu, Save_Click, Close_Click);

				var model = getModel();
				var alignmentInterval = (model != null) ? model.Groups[_g].AlignmentInterval : "";
				highlightButton(AlignmentIntervalMenu, alignmentInterval);

				string resourceKey = "";

				if (resourceKey.Length > 0)
				{
					Viewbox legend = this.Resources[resourceKey] as Viewbox;
					if (legend != null)
					{
						legend.SetValue(MarginProperty, new Thickness(0, 20, 0, 0));
						BenchmarkMenu1.Children.Add(legend);
					}
				}
			}
			else
			{
				showView();
			}
		}

		private void AutoMLInterval_MouseDown(object sender, MouseButtonEventArgs e)
		{
			if (BenchmarkMenu1.Visibility == Visibility.Collapsed)
			{
				hideView();
				hideNavigation();

				Menus.Visibility = Visibility.Visible;

				Level1Panel.Visibility = Visibility.Collapsed;
				BenchmarkMenu1.Visibility = Visibility.Collapsed;

				RankingMenu.Visibility = Visibility.Collapsed;

				MLIntervalsMenu.Visibility = Visibility.Collapsed;
				RebalanceMenu.Visibility = Visibility.Collapsed;
				AlignmentIntervalMenu.Visibility = Visibility.Collapsed;
				AutoMLIntervalMenu.Visibility = Visibility.Visible;

				ATMStrategyMenu.Visibility = Visibility.Collapsed;
				FeaturesMenu.Visibility = Visibility.Collapsed;

				ATMTimingMenus.Visibility = Visibility.Collapsed;
				ScenarioMenu1.Visibility = Visibility.Collapsed;
				PortfolioSettings.Visibility = Visibility.Collapsed;
				MLRankMenu.Visibility = Visibility.Collapsed;
				MLIterationMenu.Visibility = Visibility.Collapsed;

				Level2Panel.Visibility = Visibility.Collapsed;
				Level3Panel.Visibility = Visibility.Collapsed;
				Level4Panel.Visibility = Visibility.Collapsed;
				SubgroupMenuScroller1.Visibility = Visibility.Collapsed;
				Level5Panel.Visibility = Visibility.Collapsed;
				Level6Panel.Visibility = Visibility.Collapsed;
				IndustryMenuScroller1.Visibility = Visibility.Collapsed;
				SubgroupActionMenu1.Visibility = Visibility.Collapsed;
				SubgroupActionMenu2.Visibility = Visibility.Collapsed;

				FilterMenu.Visibility = Visibility.Collapsed;
				InputMenu.Visibility = Visibility.Collapsed;
				PortfolioSettings.Visibility = Visibility.Collapsed;

				SetupNav.Visibility = Visibility.Collapsed;

				nav.UseCheckBoxes = false;
				nav.setNavigation(AutoMLIntervalMenu, ATMMLIntervalSelected_MouseDown, Model.MLIntervals);
				addSaveAndCloseButtons(AutoMLIntervalMenu, Save_Click, Close_Click);

				var model = getModel();
				var ATMMLInterval = (model != null) ? model.ATMMLInterval : "";
				highlightButton(AutoMLIntervalMenu, ATMMLInterval);

				string resourceKey = "";

				if (resourceKey.Length > 0)
				{
					Viewbox legend = this.Resources[resourceKey] as Viewbox;
					if (legend != null)
					{
						legend.SetValue(MarginProperty, new Thickness(0, 20, 0, 0));
						BenchmarkMenu1.Children.Add(legend);
					}
				}
			}
			else
			{
				showView();
			}
		}

		private void PositionIntervalSelected_MouseDown(object sender, RoutedEventArgs e)
		{
			Label label = e.Source as Label;
			string name = label.Content as string;

			if (_useUserFactorModel)
			{
				var model = getModel();
				if (model != null)
				{
					model.ATMAnalysisInterval = name;
				}
				PositionInterval.Content = name;
			}

			showView();
		}

		private void RiskIntervalSelected_MouseDown(object sender, RoutedEventArgs e)
		{
			Label label = e.Source as Label;
			string name = label.Content as string;

			if (_useUserFactorModel)
			{
				var model = getModel();
				if (model != null)
				{
					model.RiskInterval = name;
				}
				//RiskInterval.Content = name;
			}

			showView();
		}

		private void RankingIntervalSelected_MouseDown(object sender, RoutedEventArgs e)
		{
			Label label = e.Source as Label;
			string name = label.Content as string;

			_rebalancePeriod = name;

			if (_useUserFactorModel)
			{
				var model = getModel();
				if (model != null)
				{
					model.RankingInterval = name;
				}
				UserFactorModelRebalance.Content = _rebalancePeriod;
			}

			showView();
		}

		private void AlignmentIntervalSelected_MouseDown(object sender, RoutedEventArgs e)
		{
			Label label = e.Source as Label;
			string name = label.Content as string;
			AlignmentInterval.Content = name;
			showView();
		}

		private void ATMMLIntervalSelected_MouseDown(object sender, RoutedEventArgs e)
		{
			Label label = e.Source as Label;
			string name = label.Content as string;
			//MLInterval.Content = name;
			showView();
		}

		private void BtnSettings_Click(object sender, RoutedEventArgs e)
		{
			var menu = new ContextMenu();

			// Change Password — available to all roles
			var changePassword = new MenuItem { Header = "Change Password" };
			changePassword.Click += (_, _) =>
				new ATMML.Auth.ChangePasswordDialog { Owner = Window.GetWindow(this) }.ShowDialog();
			menu.Items.Add(changePassword);

			// User Management — Admin only
			if (ATMML.Auth.AuthContext.Current.CanManageUsers)
			{
				var manageUsers = new MenuItem { Header = "User Management" };
				manageUsers.Click += (_, _) =>
					new ATMML.Auth.UserManagementWindow { Owner = Window.GetWindow(this) }.ShowDialog();
				menu.Items.Add(manageUsers);
			}

			menu.Items.Add(new Separator());

			// Logout
			var logout = new MenuItem { Header = "Logout" };
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

		private void Iterations_MouseDown(object sender, MouseButtonEventArgs e)
		{
			if (BenchmarkMenu1.Visibility == Visibility.Collapsed)
			{
				hideView();
				hideNavigation();

				Menus.Visibility = Visibility.Visible;

				Level1Panel.Visibility = Visibility.Collapsed;
				BenchmarkMenu1.Visibility = Visibility.Collapsed;
				MLRankMenu.Visibility = Visibility.Collapsed;
				MLIterationMenu.Visibility = Visibility.Visible;
				RankingMenu.Visibility = Visibility.Collapsed;
				RebalanceMenu.Visibility = Visibility.Collapsed;
				AlignmentIntervalMenu.Visibility = Visibility.Collapsed;
				AutoMLIntervalMenu.Visibility = Visibility.Collapsed;

				MLIntervalsMenu.Visibility = Visibility.Collapsed;

				ATMStrategyMenu.Visibility = Visibility.Collapsed;
				FeaturesMenu.Visibility = Visibility.Collapsed;


				ATMTimingMenus.Visibility = Visibility.Collapsed;
				ScenarioMenu1.Visibility = Visibility.Collapsed;
				PortfolioSettings.Visibility = Visibility.Collapsed;

				Level2Panel.Visibility = Visibility.Collapsed;
				Level3Panel.Visibility = Visibility.Collapsed;
				Level4Panel.Visibility = Visibility.Collapsed;
				SubgroupMenuScroller1.Visibility = Visibility.Collapsed;
				Level5Panel.Visibility = Visibility.Collapsed;
				Level6Panel.Visibility = Visibility.Collapsed;
				IndustryMenuScroller1.Visibility = Visibility.Collapsed;
				SubgroupActionMenu1.Visibility = Visibility.Collapsed;
				SubgroupActionMenu2.Visibility = Visibility.Collapsed;

				FilterMenu.Visibility = Visibility.Collapsed;
				InputMenu.Visibility = Visibility.Collapsed;
				PortfolioSettings.Visibility = Visibility.Collapsed;

				SetupNav.Visibility = Visibility.Collapsed;

				nav.UseCheckBoxes = false;
				nav.setNavigation(MLIterationMenu, Iterations_MouseDown, Model.MLITERATIONS);
				addSaveAndCloseButtons(MLIterationMenu, Save_Click, Close_Click);

				var model = getModel();
				var ranking = (model != null) ? model.Ranking : "";

				highlightButton(RankingMenu, Model.GetRankingDescription(ranking));

				string resourceKey = "";

				if (resourceKey.Length > 0)
				{
					Viewbox legend = this.Resources[resourceKey] as Viewbox;
					if (legend != null)
					{
						legend.SetValue(MarginProperty, new Thickness(0, 20, 0, 0));
						BenchmarkMenu1.Children.Add(legend);
					}
				}
			}
			else
			{
				showView();
			}
		}

		private void PositionInterval_MouseDown(object sender, MouseButtonEventArgs e)
		{
			if (BenchmarkMenu1.Visibility == Visibility.Collapsed)
			{
				hideView();
				hideNavigation();

				Menus.Visibility = Visibility.Visible;

				Level1Panel.Visibility = Visibility.Collapsed;
				BenchmarkMenu1.Visibility = Visibility.Collapsed;

				RankingMenu.Visibility = Visibility.Collapsed;

				MLIntervalsMenu.Visibility = Visibility.Collapsed;
				RebalanceMenu.Visibility = Visibility.Visible;

				AlignmentIntervalMenu.Visibility = Visibility.Collapsed;
				AutoMLIntervalMenu.Visibility = Visibility.Collapsed;
				ATMStrategyMenu.Visibility = Visibility.Collapsed;
				FeaturesMenu.Visibility = Visibility.Collapsed;

				ATMTimingMenus.Visibility = Visibility.Collapsed;
				ScenarioMenu1.Visibility = Visibility.Collapsed;
				PortfolioSettings.Visibility = Visibility.Collapsed;
				MLRankMenu.Visibility = Visibility.Collapsed;
				MLIterationMenu.Visibility = Visibility.Collapsed;

				Level2Panel.Visibility = Visibility.Collapsed;
				Level3Panel.Visibility = Visibility.Collapsed;
				Level4Panel.Visibility = Visibility.Collapsed;
				SubgroupMenuScroller1.Visibility = Visibility.Collapsed;
				Level5Panel.Visibility = Visibility.Collapsed;
				Level6Panel.Visibility = Visibility.Collapsed;
				IndustryMenuScroller1.Visibility = Visibility.Collapsed;
				SubgroupActionMenu1.Visibility = Visibility.Collapsed;
				SubgroupActionMenu2.Visibility = Visibility.Collapsed;

				FilterMenu.Visibility = Visibility.Collapsed;
				InputMenu.Visibility = Visibility.Collapsed;
				PortfolioSettings.Visibility = Visibility.Collapsed;

				SetupNav.Visibility = Visibility.Collapsed;

				nav.UseCheckBoxes = false;
				nav.setNavigation(RebalanceMenu, PositionIntervalSelected_MouseDown, Model.Rebalances);
				//addSaveAndCloseButtons(RebalanceMenu, Save_Click, Close_Click);

				var model = getModel();
				var positionInterval = (model != null) ? model.ATMAnalysisInterval : "";
				highlightButton(RebalanceMenu, positionInterval);

				string resourceKey = "";

				if (resourceKey.Length > 0)
				{
					Viewbox legend = this.Resources[resourceKey] as Viewbox;
					if (legend != null)
					{
						legend.SetValue(MarginProperty, new Thickness(0, 20, 0, 0));
						BenchmarkMenu1.Children.Add(legend);
					}
				}
			}
			else
			{
				showView();
			}
		}

		private void RiskInterval_MouseDown(object sender, MouseButtonEventArgs e)
		{
			if (BenchmarkMenu1.Visibility == Visibility.Collapsed)
			{
				hideView();
				hideNavigation();

				Menus.Visibility = Visibility.Visible;

				Level1Panel.Visibility = Visibility.Collapsed;
				BenchmarkMenu1.Visibility = Visibility.Collapsed;

				RankingMenu.Visibility = Visibility.Collapsed;

				MLIntervalsMenu.Visibility = Visibility.Collapsed;
				RebalanceMenu.Visibility = Visibility.Visible;

				AlignmentIntervalMenu.Visibility = Visibility.Collapsed;
				AutoMLIntervalMenu.Visibility = Visibility.Collapsed;
				ATMStrategyMenu.Visibility = Visibility.Collapsed;
				FeaturesMenu.Visibility = Visibility.Collapsed;

				ATMTimingMenus.Visibility = Visibility.Collapsed;
				ScenarioMenu1.Visibility = Visibility.Collapsed;
				PortfolioSettings.Visibility = Visibility.Collapsed;
				MLRankMenu.Visibility = Visibility.Collapsed;
				MLIterationMenu.Visibility = Visibility.Collapsed;

				Level2Panel.Visibility = Visibility.Collapsed;
				Level3Panel.Visibility = Visibility.Collapsed;
				Level4Panel.Visibility = Visibility.Collapsed;
				SubgroupMenuScroller1.Visibility = Visibility.Collapsed;
				Level5Panel.Visibility = Visibility.Collapsed;
				Level6Panel.Visibility = Visibility.Collapsed;
				IndustryMenuScroller1.Visibility = Visibility.Collapsed;
				SubgroupActionMenu1.Visibility = Visibility.Collapsed;
				SubgroupActionMenu2.Visibility = Visibility.Collapsed;

				FilterMenu.Visibility = Visibility.Collapsed;
				InputMenu.Visibility = Visibility.Collapsed;
				PortfolioSettings.Visibility = Visibility.Collapsed;

				SetupNav.Visibility = Visibility.Collapsed;

				nav.UseCheckBoxes = false;
				nav.setNavigation(RebalanceMenu, RiskIntervalSelected_MouseDown, Model.Rebalances);
				//addSaveAndCloseButtons(RebalanceMenu, Save_Click, Close_Click);

				var model = getModel();
				var riskInterval = (model != null) ? model.RiskInterval : "";
				highlightButton(RebalanceMenu, riskInterval);

				string resourceKey = "";

				if (resourceKey.Length > 0)
				{
					Viewbox legend = this.Resources[resourceKey] as Viewbox;
					if (legend != null)
					{
						legend.SetValue(MarginProperty, new Thickness(0, 20, 0, 0));
						BenchmarkMenu1.Children.Add(legend);
					}
				}
			}
			else
			{
				showView();
			}
		}


		private void MLRanking_MouseDown(object sender, MouseButtonEventArgs e)
		{
			if (BenchmarkMenu1.Visibility == Visibility.Collapsed)
			{
				hideView();
				hideNavigation();

				Menus.Visibility = Visibility.Visible;

				Level1Panel.Visibility = Visibility.Collapsed;
				BenchmarkMenu1.Visibility = Visibility.Collapsed;
				MLRankMenu.Visibility = Visibility.Visible;
				MLIterationMenu.Visibility = Visibility.Collapsed;
				RankingMenu.Visibility = Visibility.Collapsed;
				RebalanceMenu.Visibility = Visibility.Collapsed;
				AlignmentIntervalMenu.Visibility = Visibility.Collapsed;
				AutoMLIntervalMenu.Visibility = Visibility.Collapsed;
				ATMStrategyMenu.Visibility = Visibility.Collapsed;
				FeaturesMenu.Visibility = Visibility.Collapsed;

				ATMTimingMenus.Visibility = Visibility.Collapsed;

				MLIntervalsMenu.Visibility = Visibility.Collapsed;
				ScenarioMenu1.Visibility = Visibility.Collapsed;
				PortfolioSettings.Visibility = Visibility.Collapsed;

				Level2Panel.Visibility = Visibility.Collapsed;
				Level3Panel.Visibility = Visibility.Collapsed;
				Level4Panel.Visibility = Visibility.Collapsed;
				SubgroupMenuScroller1.Visibility = Visibility.Collapsed;
				Level5Panel.Visibility = Visibility.Collapsed;
				Level6Panel.Visibility = Visibility.Collapsed;
				IndustryMenuScroller1.Visibility = Visibility.Collapsed;
				SubgroupActionMenu1.Visibility = Visibility.Collapsed;
				SubgroupActionMenu2.Visibility = Visibility.Collapsed;

				FilterMenu.Visibility = Visibility.Collapsed;
				InputMenu.Visibility = Visibility.Collapsed;
				PortfolioSettings.Visibility = Visibility.Collapsed;

				SetupNav.Visibility = Visibility.Collapsed;

				nav.UseCheckBoxes = false;
				nav.setNavigation(MLRankMenu, MLRanking_MouseDown, Model.MLBinaryRanks);
				addSaveAndCloseButtons(MLRankMenu, Save_Click, Close_Click);

				var model = getModel();
				var ranking = (model != null) ? model.Ranking : "";

				highlightButton(RankingMenu, Model.GetRankingDescription(ranking));

				string resourceKey = "";

				if (resourceKey.Length > 0)
				{
					Viewbox legend = this.Resources[resourceKey] as Viewbox;
					if (legend != null)
					{
						legend.SetValue(MarginProperty, new Thickness(0, 20, 0, 0));
						BenchmarkMenu1.Children.Add(legend);
					}
				}
			}
			else
			{
				showView();
			}
		}

		private void Ranking_MouseDown(object sender, MouseButtonEventArgs e)
		{
			if (BenchmarkMenu1.Visibility == Visibility.Collapsed)
			{
				hideView();
				hideNavigation();

				Menus.Visibility = Visibility.Visible;

				Level1Panel.Visibility = Visibility.Collapsed;
				BenchmarkMenu1.Visibility = Visibility.Collapsed;
				MLIterationMenu.Visibility = Visibility.Collapsed;
				MLRankMenu.Visibility = Visibility.Collapsed;
				RankingMenu.Visibility = Visibility.Visible;
				RebalanceMenu.Visibility = Visibility.Collapsed;
				AlignmentIntervalMenu.Visibility = Visibility.Collapsed;
				AutoMLIntervalMenu.Visibility = Visibility.Collapsed;

				MLIntervalsMenu.Visibility = Visibility.Collapsed;
				ATMStrategyMenu.Visibility = Visibility.Collapsed;
				FeaturesMenu.Visibility = Visibility.Collapsed;

				ATMTimingMenus.Visibility = Visibility.Collapsed;
				ScenarioMenu1.Visibility = Visibility.Collapsed;
				PortfolioSettings.Visibility = Visibility.Collapsed;

				Level2Panel.Visibility = Visibility.Collapsed;
				Level3Panel.Visibility = Visibility.Collapsed;
				Level4Panel.Visibility = Visibility.Collapsed;
				SubgroupMenuScroller1.Visibility = Visibility.Collapsed;
				Level5Panel.Visibility = Visibility.Collapsed;
				Level6Panel.Visibility = Visibility.Collapsed;
				IndustryMenuScroller1.Visibility = Visibility.Collapsed;
				SubgroupActionMenu1.Visibility = Visibility.Collapsed;
				SubgroupActionMenu2.Visibility = Visibility.Collapsed;

				FilterMenu.Visibility = Visibility.Collapsed;
				InputMenu.Visibility = Visibility.Collapsed;
				PortfolioSettings.Visibility = Visibility.Collapsed;

				SetupNav.Visibility = Visibility.Collapsed;

				nav.UseCheckBoxes = false;
				nav.setNavigation(RankingMenu, RankingSelected_MouseDown, Model.RankingDescriptions);
				addSaveAndCloseButtons(RankingMenu, Save_Click, Close_Click);

				var model = getModel();
				var ranking = (model != null) ? model.Ranking : "";

				highlightButton(RankingMenu, Model.GetRankingDescription(ranking));

				string resourceKey = "";

				if (resourceKey.Length > 0)
				{
					Viewbox legend = this.Resources[resourceKey] as Viewbox;
					if (legend != null)
					{
						legend.SetValue(MarginProperty, new Thickness(0, 20, 0, 0));
						BenchmarkMenu1.Children.Add(legend);
					}
				}
			}
			else
			{
				showView();
			}
		}

		private void SelectFile_MouseDown(object sender, MouseButtonEventArgs e)
		{
			var tb = sender as TextBlock;
			var text = tb.Text;
			var fields = text.Split('\u0002');

			var dlg = new OpenFileDialog();
			if (dlg.ShowDialog() == true)
			{
				tb.Text = fields[0] + "\u0002" + dlg.FileName;
			}
			else tb.Text = fields[0];
		}

		private void addSaveAndCloseButtons(StackPanel input, MouseButtonEventHandler save_click, MouseButtonEventHandler close_click)
		{
			var panel = new StackPanel();
			panel.Orientation = Orientation.Horizontal;
			var label1 = new Label();
			label1.Background = new SolidColorBrush(Color.FromRgb(0x30, 0x30, 0x30));
			label1.BorderBrush = new SolidColorBrush(Color.FromRgb(0x30, 0x30, 0x30));
			label1.BorderThickness = new Thickness(1);
			label1.Foreground = Brushes.White;
			label1.Height = 20;
			label1.Padding = new Thickness(10, 2, 0, 2);
			label1.HorizontalAlignment = HorizontalAlignment.Center;
			label1.VerticalAlignment = VerticalAlignment.Bottom;
			label1.FontFamily = new FontFamily("Helvetica Neue");
			label1.FontWeight = FontWeights.Normal;
			label1.FontSize = 11;
			label1.Cursor = Cursors.Hand;
			label1.Width = 45;
			label1.Margin = new Thickness(0, 20, 5, 0);
			label1.Content = "Save";
			label1.MouseEnter += Mouse_Enter;
			label1.MouseLeave += Mouse_Leave;
			label1.MouseDown += save_click;
			panel.Children.Add(label1);

			var label2 = new Label();
			label2.Background = new SolidColorBrush(Color.FromRgb(0x30, 0x30, 0x30));
			label2.BorderBrush = new SolidColorBrush(Color.FromRgb(0x30, 0x30, 0x30));
			label2.BorderThickness = new Thickness(1);
			label2.Foreground = Brushes.White;
			label2.Height = 20;
			label2.Padding = new Thickness(6, 2, 0, 2);
			label2.HorizontalAlignment = HorizontalAlignment.Center;
			label2.VerticalAlignment = VerticalAlignment.Bottom;
			label2.FontFamily = new FontFamily("Helvetica Neue");
			label2.FontWeight = FontWeights.Normal;
			label2.FontSize = 11;
			label2.Cursor = Cursors.Hand;
			label2.Width = 45;
			label2.Margin = new Thickness(5, 20, 0, 0);
			label2.Content = "Close";
			label2.MouseEnter += Mouse_Enter;
			label2.MouseLeave += Mouse_Leave;
			label2.MouseDown += close_click;
			panel.Children.Add(label2);
			input.Children.Add(panel);
		}

		private void AddFilters_MouseDown(object sender, MouseButtonEventArgs e)
		{
			if (BenchmarkMenu1.Visibility == Visibility.Collapsed)
			{
				hideView();
				hideNavigation();

				Menus.Visibility = Visibility.Visible;

				Level1Panel.Visibility = Visibility.Collapsed;
				BenchmarkMenu1.Visibility = Visibility.Collapsed;

				ScenarioMenu1.Visibility = Visibility.Collapsed;

				RankingMenu.Visibility = Visibility.Collapsed;
				RebalanceMenu.Visibility = Visibility.Collapsed;
				AlignmentIntervalMenu.Visibility = Visibility.Collapsed;
				AutoMLIntervalMenu.Visibility = Visibility.Collapsed;

				MLIntervalsMenu.Visibility = Visibility.Collapsed;
				ATMStrategyMenu.Visibility = Visibility.Collapsed;
				FeaturesMenu.Visibility = Visibility.Collapsed;

				Level2Panel.Visibility = Visibility.Collapsed;
				Level3Panel.Visibility = Visibility.Collapsed;
				Level4Panel.Visibility = Visibility.Collapsed;
				SubgroupMenuScroller1.Visibility = Visibility.Collapsed;
				Level5Panel.Visibility = Visibility.Collapsed;
				Level6Panel.Visibility = Visibility.Collapsed;
				IndustryMenuScroller1.Visibility = Visibility.Collapsed;
				SubgroupActionMenu1.Visibility = Visibility.Collapsed;
				SubgroupActionMenu2.Visibility = Visibility.Collapsed;

				MLRankMenu.Visibility = Visibility.Collapsed;
				MLIterationMenu.Visibility = Visibility.Collapsed;

				FilterMenu.Visibility = Visibility.Visible;
				InputMenu.Visibility = Visibility.Collapsed;
				PortfolioSettings.Visibility = Visibility.Collapsed;

				SetupNav.Visibility = Visibility.Collapsed;

				nav.UseCheckBoxes = false;
				nav.setNavigation(RankingMenu, RankingSelected_MouseDown, Model.RankingDescriptions);
				addSaveAndCloseButtons(RankingMenu, Save_Click, Close_Click);

				var model = getModel();
				var ranking = (model != null) ? model.Ranking : "";

				highlightButton(RankingMenu, Model.GetRankingDescription(ranking));

				string resourceKey = "";

				if (resourceKey.Length > 0)
				{
					Viewbox legend = this.Resources[resourceKey] as Viewbox;
					if (legend != null)
					{
						legend.SetValue(MarginProperty, new Thickness(0, 20, 0, 0));
						BenchmarkMenu1.Children.Add(legend);
					}
				}
			}
			else
			{
				showView();
			}
		}

		private void AddInputs_MouseDown(object sender, MouseButtonEventArgs e)
		{
			if (BenchmarkMenu1.Visibility == Visibility.Collapsed)
			{
				hideView();
				hideNavigation();

				Menus.Visibility = Visibility.Visible;

				Level1Panel.Visibility = Visibility.Collapsed;
				BenchmarkMenu1.Visibility = Visibility.Collapsed;
				ScenarioMenu1.Visibility = Visibility.Collapsed;

				RankingMenu.Visibility = Visibility.Collapsed;

				MLIntervalsMenu.Visibility = Visibility.Collapsed;
				RebalanceMenu.Visibility = Visibility.Collapsed;
				AlignmentIntervalMenu.Visibility = Visibility.Collapsed;
				AutoMLIntervalMenu.Visibility = Visibility.Collapsed;
				ATMStrategyMenu.Visibility = Visibility.Collapsed;
				FeaturesMenu.Visibility = Visibility.Collapsed;

				Level2Panel.Visibility = Visibility.Collapsed;
				Level3Panel.Visibility = Visibility.Collapsed;
				Level4Panel.Visibility = Visibility.Collapsed;
				SubgroupMenuScroller1.Visibility = Visibility.Collapsed;
				Level5Panel.Visibility = Visibility.Collapsed;
				Level6Panel.Visibility = Visibility.Collapsed;
				IndustryMenuScroller1.Visibility = Visibility.Collapsed;
				SubgroupActionMenu1.Visibility = Visibility.Collapsed;
				SubgroupActionMenu2.Visibility = Visibility.Collapsed;
				MLRankMenu.Visibility = Visibility.Collapsed;
				MLIterationMenu.Visibility = Visibility.Collapsed;


				FilterMenu.Visibility = Visibility.Collapsed;
				InputMenu.Visibility = Visibility.Visible;
				FeaturesMenu.Visibility = Visibility.Collapsed;
				PortfolioSettings.Visibility = Visibility.Collapsed;

				SetupNav.Visibility = Visibility.Collapsed;

				nav.UseCheckBoxes = false;
				nav.setNavigation(RankingMenu, RankingSelected_MouseDown, Model.RankingDescriptions);
				addSaveAndCloseButtons(RankingMenu, Save_Click, Close_Click);

				var model = getModel();
				var ranking = (model != null) ? model.Ranking : "";

				highlightButton(RankingMenu, Model.GetRankingDescription(ranking));

				string resourceKey = "";

				if (resourceKey.Length > 0)
				{
					Viewbox legend = this.Resources[resourceKey] as Viewbox;
					if (legend != null)
					{
						legend.SetValue(MarginProperty, new Thickness(0, 20, 0, 0));
						BenchmarkMenu1.Children.Add(legend);
					}
				}
			}
			else
			{
				showView();
			}
		}

		private TreeNode _atmStrategyTree = null;

		private bool _addStrategySaveAndClose = true;

		private void ATMTimingMenu_MouseDown(object sender, MouseButtonEventArgs e)
		{

			Model model = getModel();

			if (model != null)
			{
				if (BenchmarkMenu1.Visibility == Visibility.Collapsed)
				{
					hideView();
					hideNavigation();

					Menus.Visibility = Visibility.Visible;

					Level1Panel.Visibility = Visibility.Collapsed;
					BenchmarkMenu1.Visibility = Visibility.Collapsed;
					MLIterationMenu.Visibility = Visibility.Collapsed;
					MLRankMenu.Visibility = Visibility.Collapsed;
					RankingMenu.Visibility = Visibility.Collapsed;
					RebalanceMenu.Visibility = Visibility.Collapsed;
					AlignmentIntervalMenu.Visibility = Visibility.Collapsed;
					AutoMLIntervalMenu.Visibility = Visibility.Collapsed;

					MLIntervalsMenu.Visibility = Visibility.Collapsed;
					FeaturesMenu.Visibility = Visibility.Collapsed;

					ATMTimingMenus.Visibility = Visibility.Collapsed;
					ScenarioMenu1.Visibility = Visibility.Collapsed;
					PortfolioSettings.Visibility = Visibility.Collapsed;

					Level2Panel.Visibility = Visibility.Collapsed;
					Level3Panel.Visibility = Visibility.Collapsed;
					Level4Panel.Visibility = Visibility.Collapsed;
					SubgroupMenuScroller1.Visibility = Visibility.Collapsed;
					Level5Panel.Visibility = Visibility.Collapsed;
					Level6Panel.Visibility = Visibility.Collapsed;
					IndustryMenuScroller1.Visibility = Visibility.Collapsed;
					SubgroupActionMenu1.Visibility = Visibility.Collapsed;
					SubgroupActionMenu2.Visibility = Visibility.Collapsed;

					FilterMenu.Visibility = Visibility.Collapsed;
					InputMenu.Visibility = Visibility.Collapsed;
					PortfolioSettings.Visibility = Visibility.Collapsed;

					SetupNav.Visibility = Visibility.Collapsed;

					ATMStrategyMenu.Visibility = Visibility.Visible;

					if (_addStrategySaveAndClose)
					{
						_addStrategySaveAndClose = false;
						addSaveAndCloseButtons(ATMStrategyMenu, ATMStrategyClose_Click, ATMStrategyClose_Click);
					}

					var strategy = (model != null) ? model.Groups[_g].Strategy : "";

					var node = getNode(strategy, _atmStrategyTree);
					if (node != null)
					{
						node.IsSelected = true;
					}

					string resourceKey = "";

					if (resourceKey.Length > 0)
					{
						Viewbox legend = this.Resources[resourceKey] as Viewbox;
						if (legend != null)
						{
							legend.SetValue(MarginProperty, new Thickness(0, 20, 0, 0));
							BenchmarkMenu1.Children.Add(legend);
						}
					}
				}
				else
				{
					showView();
				}
			}
		}
		private void UserFactorModelSetup_Click(object sender, RoutedEventArgs e)
		{
			if (_settingRadioButtons) return;
			_useUserFactorModel = true;

			UserFactorModelGrid.Visibility = Visibility.Visible;
			if (ATMML.Auth.AuthContext.Current.IsAdmin && !_suppressTISetup)
				TISetup.Visibility = Visibility.Visible;
			else
				TISetup.Visibility = Visibility.Collapsed;

			PerformanceGrid.Visibility = Visibility.Collapsed;
			PerformanceSideNav.Visibility = Visibility.Collapsed;

			ATMStrategyOverlay.Visibility = Visibility.Collapsed;
			RebalanceChartGrid.Visibility = Visibility.Collapsed;
			RebalanceGrid.Visibility = Visibility.Collapsed;
			RebalanceSideNav.Visibility = Visibility.Collapsed;

			// User Model radio button clicked

			setModelRadioButtons();
			userFactorModel_to_ui(_selectedUserFactorModel);
			_update = 16;
		}

		private void UserFactorModelMemberList_Click(object sender, RoutedEventArgs e)
		{
			if (_settingRadioButtons) return;
			_useUserFactorModel = true;

			UserFactorModelPanel2.Visibility = Visibility.Visible;

			setModelRadioButtons();
			userFactorModel_to_ui(_selectedUserFactorModel);
			_update = 17;
		}


		private void UserFactorModelRebalanceMemberList_Click(object sender, RoutedEventArgs e)
		{
			_useUserFactorModel = true;

			setModelRadioButtons();
			userFactorModel_to_ui(_selectedUserFactorModel);
			_update = 19;
		}

		private void RankingSelected_MouseDown(object sender, RoutedEventArgs e)
		{
			Label label = e.Source as Label;
			string name = label.Content as string;

			_rankingType = Model.GetRanking(name);

			if (_useUserFactorModel)
			{
				UserFactorModelRanking.Content = _rankingType;
				if (_userFactorModels.ContainsKey(_selectedUserFactorModel))
				{
					_userFactorModels[_selectedUserFactorModel].Ranking = _rankingType;
				}
			}
			showView();
		}

		private void UserFactorModelName_MouseDown(object sender, MouseButtonEventArgs e)
		{
			{
				for (int ii = 1; ii < 100; ii++)
				{
					string name = "PORTFOLIO " + ii.ToString();
					if (!_userFactorModels.ContainsKey(name))
					{
						Model model = new Model(name);
						model.Constraints = Model.getDefaultConstraints();
						model.MLMaxBars = "2000";
						_userFactorModels[name] = model;
						changeUserFactorModel(name);
						break;
					}
				}
				updateUserFactorModelList();
				userFactorModel_to_ui(_selectedUserFactorModel);
				_update = 801;
			}
		}

		private void changeUserFactorModel(string name)
		{
			if (name != _selectedUserFactorModel)
			{
				_initializing = true;

				_selectedUserFactorModel = name;
				_g = 0;
				_portfolio1.Clear();

				_currentPortfolio.Clear();
				_portfolioTimes.Clear();
				_benchmarkValues.Clear();
				_portfolioNavs.Clear();
				_portfolioValues.Clear();
				_portfolioReturns.Clear();
				_portfolioMonthTimes.Clear();
				_portfolioMonthValues.Clear();
				_portfolioHedgeMonthValues.Clear();
				_compareToValues.Clear();

				_averageTurnover = 0;
				clearStats();

				loadModel();

				userFactorModel_to_ui(_selectedUserFactorModel);

				var model = getModel();

				updateChartAnalysisSettings(model);
				updateChartPortfolio();
				updateUserFactorModelList();
				initializeCalculator(model);
				updateStrategyTree();
				hideChart();

				_initializing = false;
			}
			_update = 20;
			OnPropertyChanged("Constraints");
			OnPropertyChanged("ATMConditionsEnter");
			OnPropertyChanged("ATMConditionsExit");
		}

		private bool isTradingModel(string name)
		{
			return (name == "CMR" || name == "TMSG");
		}

		private void loadModel()
		{
			LoadOrdersSentFlag();
			var model = getModel();
			if (model != null)
			{
				//model.Symbols = loadSymbolList(model.Name + " Symbols").Distinct().ToList();
				_currentPortfolio = loadList<string>(model.Name + " CurrentPortFolio");
				_portfolioTimes = loadList<DateTime>(model.Name + " PortfolioTimes");
				_benchmarkValues = loadList<double>(model.Name + " BenchmarkValues");
				_portfolioNavs = loadList<double>(model.Name + " PortfolioNavs");
				_portfolioReturns = loadList<double>(model.Name + " PortfolioReturns");
				_portfolioValues = loadList<double>(model.Name + " PortfolioValues");
				//Debug.WriteLine($"[LoadModel] {model.Name} PortfolioTimes={_portfolioTimes.Count} PortfolioValues={_portfolioValues.Count} PortfolioReturns={_portfolioReturns.Count}");
				_portfolioMonthTimes = loadList<DateTime>(model.Name + " PortfolioMonthTimes");
				_portfolioMonthValues = loadList<double>(model.Name + " PortfolioMonthValues");
				//_portfolioRegimes = loadList<int>(model.Name + " PortfolioRegimes");

				if (isTradingModel(model.Name))
				{
					_barCache.RequestBars("SPY US Equity", "Daily");
				}

				model.SetTimeRanges();
			}
		}

		private string _graphDataType = "CUMULATIVE RETURN";
		private string _graphSector = "ALL SECTORS";
		private string _activeTileView = "Sector";

		private void OnUseMTExitChecked(object sender, RoutedEventArgs e)
		{
			//FTEntry.Content = "FT IN|OUT";
		}
		private void OnUseMTExitUnchecked(object sender, RoutedEventArgs e)
		{
			//FTEntry.Content = "FT IN";
		}

		private void NewTrendChecked(object sender, RoutedEventArgs e)
		{
			var model = _userFactorModels[_selectedUserFactorModel];
			NewTrendUnit.Text = model.Groups[_g].NewTrendUnit.ToString();
		}
		private void NewTrendUnchecked(object sender, RoutedEventArgs e)
		{
			NewTrendUnit.Text = "";
		}
		private void PressureUnitChecked(object sender, RoutedEventArgs e)
		{
			var model = _userFactorModels[_selectedUserFactorModel];
			PressureUnit.Text = model.Groups[_g].PressureUnit.ToString();
		}
		private void PressureUnitUnchecked(object sender, RoutedEventArgs e)
		{
			PressureUnit.Text = "";
		}
		private void AddUnitChecked(object sender, RoutedEventArgs e)
		{
			var model = _userFactorModels[_selectedUserFactorModel];
			AddUnit.Text = model.Groups[_g].AddUnit.ToString();
		}
		private void AddUnitUnchecked(object sender, RoutedEventArgs e)
		{
			AddUnit.Text = "";
		}
		private void RetracePercentChecked(object sender, RoutedEventArgs e)
		{
			var model = _userFactorModels[_selectedUserFactorModel];
			RetracePercent.Text = model.Groups[_g].RetracePercent.ToString();
		}
		private void RetracePercentUnchecked(object sender, RoutedEventArgs e)
		{
			RetracePercent.Text = "";
		}

		private void ExhaustionPercentChecked(object sender, RoutedEventArgs e)
		{
			var model = _userFactorModels[_selectedUserFactorModel];
			ExhaustionPercent.Text = model.Groups[_g].ExhaustionPercent.ToString();
		}
		private void ExhaustionPercentUnchecked(object sender, RoutedEventArgs e)
		{
			ExhaustionPercent.Text = "";
		}

		private void OnUseMTExhExitUnchecked(object sender, RoutedEventArgs e)
		{
			ExhaustionExit.Content = "EXH IN|OUT";
		}
		private void OnUseMTExhExitChecked(object sender, RoutedEventArgs e)
		{
			ExhaustionExit.Content = "EXH EXIT";
		}



		private void ModelReturn_Checked(object sender, MouseEventArgs e)
		{
			string sector = (sender as Label).Name.Replace("_", " ");

			_graphSector = sector;

			if (sector == "ALL SECTORS") sector = "";

			Model model = getModel();
			if (model != null)
			{
				model.Sector = sector;
				updateModelData();
			}

			highlightSectors();
		}

		private void SelectGraphData(object sender, MouseButtonEventArgs e)
		{
			Label label = sender as Label;

			_graphDataType = label.Content as string;

			highlightStats();

			update();
		}

		// needs work
		private void highlightSectors()
		{
			DISCRETIONARY.Foreground = (_graphSector == "DISCRETIONARY") ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00ccff")) : new SolidColorBrush(Color.FromArgb(0xff, 0xff, 0xff, 0xff));
			STAPLES.Foreground = (_graphSector == "STAPLES") ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00ccff")) : new SolidColorBrush(Color.FromArgb(0xff, 0xff, 0xff, 0xff));
			ENERGY.Foreground = (_graphSector == "ENERGY") ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00ccff")) : new SolidColorBrush(Color.FromArgb(0xff, 0xff, 0xff, 0xff));
			FINANCIALS.Foreground = (_graphSector == "FINANCIALS") ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00ccff")) : new SolidColorBrush(Color.FromArgb(0xff, 0xff, 0xff, 0xff));
			HEALTHCARE.Foreground = (_graphSector == "HEALTHCARE") ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00ccff")) : new SolidColorBrush(Color.FromArgb(0xff, 0xff, 0xff, 0xff));
			INDUSTRIALS.Foreground = (_graphSector == "INDUSTRIALS") ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00ccff")) : new SolidColorBrush(Color.FromArgb(0xff, 0xff, 0xff, 0xff));
			MATERIALS.Foreground = (_graphSector == "MATERIALS") ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00ccff")) : new SolidColorBrush(Color.FromArgb(0xff, 0xff, 0xff, 0xff));
			TECHNOLOGY.Foreground = (_graphSector == "TECHNOLOGY") ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00ccff")) : new SolidColorBrush(Color.FromArgb(0xff, 0xff, 0xff, 0xff));
			TELCOM.Foreground = (_graphSector == "TELCOM") ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00ccff")) : new SolidColorBrush(Color.FromArgb(0xff, 0xff, 0xff, 0xff));
			UTILITIES.Foreground = (_graphSector == "UTILITIES") ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00ccff")) : new SolidColorBrush(Color.FromArgb(0xff, 0xff, 0xff, 0xff));
		}

		private void highlightStats()
		{
			CumulativeReturnValue.Foreground = (_graphDataType == "CUMULATIVE RETURN") ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00ccff")) : new SolidColorBrush(Color.FromArgb(0xff, 0xff, 0x8c, 0x00));

			//Alpha.Foreground = (_graphDataType == "ALPHA") ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("98fb98")) : new SolidColorBrush(Color.FromArgb(0xff, 0x98, 0xfb, 98));
			//AlphaValue.Foreground = (_graphDataType == "ALPHA") ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00ccff")) : new SolidColorBrush(Color.FromArgb(0xff, 0xff, 0x8c, 00));

			//BetaLabel.Foreground = (_graphDataType == "BETA") ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#98fb98")) : new SolidColorBrush(Color.FromArgb(0xff, 0x98, 0xfb, 98));
			//BetaValue.Foreground = (_graphDataType == "BETA") ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00ccff")) : new SolidColorBrush(Color.FromArgb(0xff, 0xff, 0x8c, 00));

			//SharpeLabel.Foreground = (_graphDataType == "SHARPE RATIO") ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#98fb98")) : new SolidColorBrush(Color.FromArgb(0xff, 0x98, 0xfb, 98));
			SharpeRatioValue.Foreground = (_graphDataType == "SHARPE RATIO") ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00ccff")) : new SolidColorBrush(Color.FromArgb(0xff, 0xff, 0x8c, 00));

			//SortinoRatioLabel.Foreground = (_graphDataType == "SORTINO RATIO") ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#98fb98")) : new SolidColorBrush(Color.FromArgb(0xff, 0x98, 0xfb, 98));
			SortinoRatioValue.Foreground = (_graphDataType == "SORTINO RATIO") ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ff8c00")) : new SolidColorBrush(Color.FromArgb(0xff, 0xff, 0x8c, 00));

			//CorrelationLabel.Foreground = (_graphDataType == "CORRELATION") ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#98fb98")) : new SolidColorBrush(Color.FromArgb(0xff, 0x98, 0xfb, 98));
			//CorrelationValue.Foreground = (_graphDataType == "CORRELATION") ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00ccff")) : new SolidColorBrush(Color.FromArgb(0xff, 0xff, 0x8c, 00));

			//StdDevLabel.Foreground = (_graphDataType == "STD DEVIATION") ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#98fb98")) : new SolidColorBrush(Color.FromArgb(0xff, 0x98, 0xfb, 98));
			//StdDevValue.Foreground = (_graphDataType == "STD DEVIATION") ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00ccff")) : new SolidColorBrush(Color.FromArgb(0xff, 0xff, 0x8c, 00));

			//InfoRatioLabel.Foreground = (_graphDataType == "INFORMATION RATIO") ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00ccff")) : new SolidColorBrush(Color.FromArgb(0xff, 0xff, 0xff, 0xff));
			//InfoRatioValue.Foreground = (_graphDataType == "INFORMATION RATIO") ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00ccff")) : new SolidColorBrush(Color.FromArgb(0xff, 0x00, 0xcc, 0xff));

			//Vol30DLabel.Foreground = (_graphDataType == "VOLATILITY 30 D") ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00ccff")) : new SolidColorBrush(Color.FromArgb(0xff, 0xff, 0xff, 0xff));
			//Volatility30DValue.Foreground = (_graphDataType == "VOLATILITY 30 D") ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00ccff")) : new SolidColorBrush(Color.FromArgb(0xff, 0x00, 0xcc, 0xff));

		}

		private void clearStats()
		{
			//Stats Col1 Right of Graph
			//LongHoldings.Content = "";
			//ShortHoldings.Content = "";
			LongHoldings7a.Content = "";
			//PORTLongCount2.Content = "";
			ShortHoldings7a.Content = "";
			LongHoldings2a.Content = "";
			ShortHoldings2a.Content = "";
			TotalCampaigns.Content = "";
			//LongHoldings2b.Content = "";
			//ShortHoldings2b.Content = "";
			//LongHoldings3.Content = "";
			//ShortHoldings3.Content = "";
			ShortHoldings1a.Content = "";

			//avgHoldingPeriod.Content = "";
			winLossRatio.Content = "";
			winRate.Content = "";
			profitFactor.Content = "";
			avgHoldWin.Content = "";
			avgHoldLoss.Content = "";
			//avgWinners.Content = "";
			//avgLosers.Content = "";
			//totalProfitFactor.Content = "";

			//avgHoldingPeriodLongs.Content = "";
			//winLossRatioLongs.Content = "";
			//avgWinnersLongs.Content = "";
			//avgLosersLongs.Content = "";
			//longProfitFactor.Content = "";

			//avgHoldingPeriodShorts.Content = "";
			//winLossRatioShorts.Content = "";
			//avgWinnersShorts.Content = "";
			//avgLosersShorts.Content = "";
			//shortProfitFactor.Content = "";

			//AlphaValue.Content = "";

			//Stats Col2 Right of Graph
			//defLongHoldings.Content = "";
			//defShortHoldings.Content = "";

			//defavgHoldingPeriod.Content = "";
			//defwinLossRatio.Content = "";
			//defavgWinners.Content = "";
			//defavgLosers.Content = "";

			//defAlphaValue.Content = "";

			//defavgHoldingPeriodLongs.Content = "";
			//defwinLossRatioLongs.Content = "";
			//defavgWinnersLongs.Content = "";
			//defavgLosersLongs.Content = "";

			//defavgHoldingPeriodShorts.Content = "";
			//defwinLossRatioShorts.Content = "";
			//defavgWinnersShorts.Content = "";
			//defavgLosersShorts.Content = "";

			//Stat Col1 left of Graph
			CumulativeReturnValue5b.Content = "";
			AnnualizedReturnValueb.Content = "";
			MonthlyReturnValueb.Content = "";
			//LongHoldings.Content = "";
			//ShortHoldings.Content = "";
			LongHoldingsa1.Content = "";
			ShortHoldings1a.Content = "";

			//AlphaValue7.Content = "";
			//BetaValue.Content = "";
			SharpeRatioValue.Content = "";
			SortinoRatioValue.Content = "";
			//CorrelationValue.Content = "";
			//VaRValue.Content = "";
			//VOLValue.Content = "";

			MaximumDD.Content = "";
			DDDuration.Content = "";
			DailyMaximumDD.Content = "";
			WeeklyMaximumDD.Content = "";
			MonthlyMaximumDD.Content = "";
			YearlyMaximumDD.Content = "";

			// Stat Col2 left of Graph
			//defCumulativeReturnValueb.Content = "";
			//defAnnualizedReturnValueb.Content = "";
			//defMonthlyReturnValueb.Content = "";
			//defLongHoldingsb.Content = "";
			//defShortHoldingsb.Content = "";

			//defBetaValue.Content = "";
			//defCorrelationValue.Content = "";
			//defSharpeRatioValue.Content = "";
			//defSortinoRatioValue.Content = "";
			//defVaRValue.Content = "";
			//defVOLValue.Content = "";

			//defMaximumDD.Content = "";
			//defDDDuration.Content = "";
			//defDailyMaximumDD.Content = "";
			//defWeeklyMaximumDD.Content = "";
			//defMonthlyMaximumDD.Content = "";
			//defYearlyMaximumDD.Content = "";

			// Stat Col3 left of Graph
			CompareReturnValue5b.Content = "";
			bmAnnualizedReturnValueb.Content = "";
			bmMonthlyReturnValueb.Content = "";

			//bmBetaValue.Content = "";
			//bmSharpeRatioValue.Content = "";
			//bmSortinoRatioValue.Content = "";
			//bmCorrelationValue.Content = "";
			//bmVaRValue.Content = "";
			//bmVOLValue.Content = "";

			//bmMaximumDD.Content = "";
			//bmDDDuration.Content = "";
			//bmDailyMaximumDD.Content = "";
			//bmWeeklyMaximumDD.Content = "";
			//bmMonthlyMaximumDD.Content = "";
			//bmYearlyMaximumDD.Content = "";
		}

		private void clearStats2()
		{
			//defwinLossRatio.Content = "";
			//defavgWinners.Content = "";
			//defavgLosers.Content = "";
			//defavgHoldingPeriod.Content = "";

			//defLongHoldings.Content = "";
			//defShortHoldings.Content = "";
			//defLongHoldings3.Content = "";
			//defShortHoldings4.Content = "";
			//defLongHoldingsb.Content = "";
			//defShortHoldingsb.Content = "";

			//defAlphaValue.Content = "";

			//defwinLossRatioLongs.Content = "";
			//defavgWinnersLongs.Content = "";
			//defavgLosersLongs.Content = "";
			//defavgHoldingPeriodLongs.Content = "";

			//defwinLossRatioShorts.Content = "";
			//defavgWinnersShorts.Content = "";
			//defavgLosersShorts.Content = "";
			//defavgHoldingPeriodShorts.Content = "";

			//defCumulativeReturnValue.Content = "";
			//defCumulativeReturnValueb.Content = "";
			//defAnnualizedReturnValue.Content = "";
			//defAnnualizedReturnValueb.Content = "";
			//defMonthlyReturnValue.Content = "";
			//defMonthlyReturnValueb.Content = "";
			//defBetaValue.Content = "";
			//defCorrelationValue.Content = "";
			//defSharpeRatioValue.Content = "";
			//defSortinoRatioValue.Content = "";
			//defMaximumDD.Content = "";
			//defDDDuration.Content = "";
			//defDailyMaximumDD.Content = "";
			//defWeeklyMaximumDD.Content = "";
			//defMonthlyMaximumDD.Content = "";
			//defYearlyMaximumDD.Content = "";

			//defVaRValue.Content = "";
			//defVOLValue.Content = "";
		}

		private void ExportOrders_MouseDown(object sender, MouseButtonEventArgs e)  // just a code copy from Export_mousedown
		{
			var dlg = new SaveFileDialog { Title = "Rebalance Orders" };
			dlg.Filter = "Excel documents (.csv)|*.csv";
			var ok = dlg.ShowDialog();
			if (ok.Value)
			{
				Model model = getModel();
				if (model != null)
				{
					DateTime newRebalanceDate = getRebalanceDate(model, _portfolioTimes[_portfolioTimes.Count - 1], _portfolioTimes);
					DateTime oldRebalanceDate = getPreviousRebalanceDate(model, newRebalanceDate, _portfolioTimes);

					Dictionary<string, double> old_weights = loadData("shares", model, oldRebalanceDate);
					Dictionary<string, double> old_scores = loadData("scores", model, oldRebalanceDate);
					Dictionary<string, double> old_atmPredictions = loadData("predictions", model, oldRebalanceDate);
					List<string> oldPortfolio = getPortfolio(oldRebalanceDate, model, old_scores, old_atmPredictions);
					oldPortfolio = oldPortfolio.OrderByDescending(x => x.Substring(0, 2)).ThenBy(x => x.Substring(2)).ToList();

					Dictionary<string, double> new_weights = loadData("shares", model, newRebalanceDate);
					Dictionary<string, double> new_scores = loadData("scores", model, newRebalanceDate);
					Dictionary<string, double> new_atmPredictions = loadData("predictions", model, newRebalanceDate);
					List<string> newPortfolio = getPortfolio(newRebalanceDate, model, new_scores, new_atmPredictions);
					newPortfolio = newPortfolio.OrderByDescending(x => x.Substring(0, 2)).ThenBy(x => x.Substring(2)).ToList();

					Dictionary<string, int> newDir = new Dictionary<string, int>();
					Dictionary<string, int> oldDir = new Dictionary<string, int>();
					foreach (var symbol in oldPortfolio)
					{
						string[] fields = symbol.Split(':');
						var ticker = fields[1];
						oldDir[ticker] = (fields[0] == "T") ? 1 : -1;
					}

					foreach (var symbol in newPortfolio)
					{
						string[] fields = symbol.Split(':');
						var ticker = fields[1];
						newDir[ticker] = (fields[0] == "T") ? 1 : -1;
					}

					var tickers = oldDir.Keys.ToList();
					tickers.AddRange(newDir.Keys.ToList());
					tickers = tickers.Distinct().ToList();
					tickers.Sort();

					using (Stream stream = dlg.OpenFile())
					{
						StreamWriter sr = new StreamWriter(stream); // user dialog
						foreach (var ticker in tickers)
						{
							int dir1 = oldDir.ContainsKey(ticker) ? oldDir[ticker] : 0;
							int dir2 = newDir.ContainsKey(ticker) ? newDir[ticker] : 0;

							var transaction = "";
							if (dir1 > 0 && dir2 <= 0) transaction = "Exit long " + ticker;
							if (dir1 < 0 && dir2 >= 0) transaction = "Cover short " + ticker;
							if (dir1 <= 0 && dir2 > 0) transaction = "Enter long " + ticker;
							if (dir1 >= 0 && dir2 < 0) transaction = "Enter short " + ticker;
							if (transaction.Length > 0)
							{
								sr.WriteLine(transaction);
							}
						}
						sr.Close();
					}
				}
			}
		}

		private void Export_MouseDown(object sender, MouseButtonEventArgs e)
		{
			var dlg = new SaveFileDialog { Title = "Export Portfolio" };
			dlg.Filter = "Excel documents (.csv)|*.csv";
			var ok = dlg.ShowDialog();
			if (ok.Value)
			{
				Model model = getModel();
				if (model != null)
				{
					DateTime rebalanceDate = getRebalanceDate(model, _portfolioTimes[_portfolioTimes.Count - 1], _portfolioTimes);

					Dictionary<string, double> weights = loadData("shares", model, rebalanceDate);
					Dictionary<string, double> scores = loadData("scores", model, rebalanceDate);
					Dictionary<string, double> atmPredictions = loadData("predictions", model, rebalanceDate);
					List<string> portfolio = getPortfolio(rebalanceDate, model, scores, atmPredictions);
					portfolio = portfolio.OrderByDescending(x => x.Substring(0, 2)).ThenBy(x => x.Substring(2)).ToList();

					using (Stream stream = dlg.OpenFile())
					{
						StreamWriter sr = new StreamWriter(stream); // user dialog
						foreach (var symbol in portfolio)
						{
							var ticker = symbol.Substring(2);
							sr.WriteLine(ticker + "," + ((symbol.Substring(0, 2) == "T:") ? 1 : -1) + "," + (100 * weights[ticker]).ToString("##.00"));
						}
						sr.Close();
					}
				}
			}
		}

		//private void ExportHistory_MouseDown(object sender, MouseButtonEventArgs e)
		//{
		//    var dlg = new SaveFileDialog { Title = "Export Portfolio" };
		//    dlg.Filter = "Excel documents (.csv)|*.csv";
		//    var ok = dlg.ShowDialog();
		//    if (ok.Value)
		//    {
		//        using (Stream stream = dlg.OpenFile())
		//        {
		//            StreamWriter sr = new StreamWriter(stream); // user dialog

		//            DateTime previousRebalanceDate = new DateTime();
		//            Model model = getModel();
		//            if (model != null)
		//            {
		//                foreach (var date in _portfolioTimes)
		//                {
		//                    DateTime rebalanceDate = getRebalanceDate(model, date, _portfolioTimes);
		//                    if (rebalanceDate != previousRebalanceDate)
		//                    {
		//                        previousRebalanceDate = rebalanceDate;
		//                        Dictionary<string, double> weights = loadData("shares", model, rebalanceDate);
		//                        Dictionary<string, double> scores = loadData("scores", model, rebalanceDate);
		//                        Dictionary<string, double> atmPredictions = loadData("predictions", model, rebalanceDate);
		//                        List<string> portfolio = getPortfolio(rebalanceDate, model, scores, atmPredictions);
		//                        portfolio = portfolio.OrderByDescending(x => x.Substring(0, 2)).ThenBy(x => x.Substring(2)).ToList();

		//                        foreach (var symbol in portfolio)
		//                        {
		//                            var ticker = symbol.Substring(2);
		//                            sr.WriteLine(rebalanceDate.ToString("yyyyMMdd") + "," + ticker + "," + ((symbol.Substring(0, 2) == "T:") ? 1 : -1) + "," + (100 * weights[ticker]).ToString("##.00"));
		//                        }
		//                    }
		//                }
		//            }
		//            sr.Close();
		//        }
		//    }
		//}

		//private void CurrentDate_Click(object sender, RoutedEventArgs e)
		//{
		//    var cb = sender as CheckBox;
		//    //UserFactorModelTestEndDate.IsEnabled = (cb.IsChecked == false);
		//    //UserFactorModelTestEndDate.Visibility = (cb.IsChecked == false) ? Visibility.Visible : Visibility.Collapsed;
		//}

		//private void BeginDate_Click(object sender, RoutedEventArgs e)
		//{
		//    var cb = sender as CheckBox;
		//    //change to InceptionDate of Port
		//    //UserFactorModelTestStartDate.IsEnabled = (cb.IsChecked == false);
		//    //UserFactorModelTestStartDate.Visibility = (cb.IsChecked == false) ? Visibility.Visible : Visibility.Collapsed;
		//}

		bool _shiftModifier = false;
		bool _controlModifier = false;

		void compareToSymbol_KeyUp(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
			{
				_controlModifier = false;
			}
			else if (e.Key == Key.LeftShift || e.Key == Key.RightShift)
			{
				_shiftModifier = false;
			}
		}

		private void initFeatureTree(TreeView treeView)
		{
			if (_atmMLTree == null)
			{
				string[] intervals = { "Mid Term", "Short Term" };
				_atmMLTree = new TreeNode("ATMMLTrainerData", intervals, true);
			}
			treeView.ItemsSource = _atmMLTree.Children;
		}

		static string defaults =
			"ATM FT    \u0002 Short Term\n" +
			"FT Going Up    \u0002 Short Term\n" +
			"FT Going Dn    \u0002 Short Term\n" +
			"ATM ST    \u0002 Short Term\n" +
			"ST Going Up    \u0002 Short Term\n" +
			"ST Going Dn    \u0002 Short Term\n" +
			"TSB Bullish    \u0002 Short Term\n" +
			"TSB Bearish    \u0002 Short Term\n" +
			"TL Bullish    \u0002 Short Term\n" +
			"TL Bearish    \u0002 Short Term\n" +
			"TB Bullish    \u0002 Short Term\n" +
			"TB Bearish    \u0002 Short Term\n" +
			"Current SC Sig    \u0002 Short Term\n" +
			"Current PR Sig    \u0002 Short Term\n" +
			"ATM FT    \u0002 Mid Term\n" +
			"FT Going Up    \u0002 Mid Term\n" +
			"FT Going Dn    \u0002 Mid Term\n" +
			"ATM ST    \u0002 Mid Term\n" +
			"ST Going Up    \u0002 Mid Term\n" +
			"ST Going Dn    \u0002 Mid Term";


		private string getFeatures(TreeNode input)
		{
			string output = "";
			if (input.HasChildren)
			{
				foreach (var node in input.Children)
				{
					var text = getFeatures(node);
					if (text.Length > 0)
					{
						output += ((output.Length > 0) ? "\n" : "") + text;
					}
				}
			}
			else if (input.IsChecked)
			{
				if (input.Name == "Default")
				{
					output = defaults;
				}
				else
				{
					output = input.GetDescription();
				}
			}
			return output;
		}

		private void setFeatures(string input)
		{
			clearTree(_atmMLTree.Children);

			input = input.Replace(defaults, "Default");

			var features = input.Split('\n');

			foreach (string feature in features)
			{
				if (feature.Length > 0)
				{
					if (feature == "Default")
					{
						var item = getItem(_atmMLTree.Children, feature);
						item.IsChecked = true;
					}
					else
					{
						var items = feature.Split('\u0002');
						var condition = items[0].Trim();
						var term = items[1].Trim();

						var item1 = getItem(_atmMLTree.Children, condition);

						if (item1 != null && item1.Children != null)
						{

							for (int ii = 0; ii < item1.Children.Count; ii++)
							{
								var item2 = item1.Children.ElementAt(ii) as TreeNode;
								if (item2.Interval == term)
								{
									item2.IsChecked = true;
									item1.IsExpanded = true;
									break;
								}
							}
						}

						var item3 = getItem(_atmMLTree.Children, feature);
						if (item3 != null)
						{
							item3.IsChecked = true;
						}

					}
				}
			}
		}

		private void compareToSymbol_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			compareToSymbolKeyDown(sender, e);
		}

		private void compareToSymbolKeyDown(object sender, KeyEventArgs e)
		{
			TextBox tb = sender as TextBox;
			string name = tb.Name;

			bool forBenchmark = (name == "UserFactorModelBenchmark");
			bool forDefaultSymbol = (name == "DefaultSymbol");

			if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
			{
				_controlModifier = true;
			}
			else if (e.Key == Key.LeftShift || e.Key == Key.RightShift)
			{
				_shiftModifier = true;
			}
			//else if (e.Key == Key.Space)
			//{
			//    tb.Text += " ";
			//    e.Handled = true;
			//}
			else if (e.Key == Key.Back)
			{
				if (tb.Text.Length > 0)
				{
					var index = tb.SelectionStart;
					if (index == 0 || index >= tb.Text.Length) index = tb.Text.Length - 1;
					tb.Text = tb.Text.Remove(index, 1);
					e.Handled = true;
				}
			}
			else if (e.Key == Key.Delete)
			{
				tb.Text = "";
			}
			else if (e.Key == Key.F1)
			{
				string command = tb.Text;
				command += " People";
				tb.Text = command;
				e.Handled = true;
			}
			else if (e.Key == Key.F2)
			{
				string command = tb.Text;
				command += " Govt";
				tb.Text = command;
				e.Handled = true;
			}
			else if (e.Key == Key.F3)
			{
				string command = tb.Text;
				command += " Corp";
				tb.Text = command;
				e.Handled = true;
			}
			else if (e.Key == Key.F4)
			{
				string command = tb.Text;
				command += " Mtge";
				tb.Text = command;
				e.Handled = true;
			}
			else if (e.Key == Key.F5)
			{
				string command = tb.Text;
				command += " M-Mkt";
				tb.Text = command;
				e.Handled = true;
			}
			else if (e.Key == Key.F6)
			{
				string command = tb.Text;
				command += " Muni";
				tb.Text = command;
				e.Handled = true;
			}
			else if (e.Key == Key.F7)
			{
				string command = tb.Text;
				command += " Pfd";
				tb.Text = command;
				e.Handled = true;
			}
			else if (e.Key == Key.F8)
			{
				string command = tb.Text;
				command += " Equity";
				tb.Text = command;
				e.Handled = true;
			}
			else if (e.Key == Key.F9)
			{
				string command = tb.Text;
				command += " Comdty";
				tb.Text = command;
				e.Handled = true;
			}
			else if (e.Key == Key.F10 || e.Key == Key.System)
			{
				string command = tb.Text;
				command += " Index";
				tb.Text = command;
				e.Handled = true;
			}
			else if (e.Key == Key.F11)
			{
				string command = tb.Text;
				command += " Curncy";
				tb.Text = command;
				e.Handled = true;
			}
			else if (e.Key == Key.F12)
			{
				string command = tb.Text;
				command += " Client";
				tb.Text = command;
				e.Handled = true;
			}
			else if (e.Key == Key.Return)
			{
				processSymbolEdit(tb, forBenchmark, forDefaultSymbol);
				e.Handled = true;
			}

			if (tb.Text.Length == 0 && !forBenchmark)
			{
				_update = 21;
			}
		}

		private void processSymbolEdit(TextBox tb, bool forBenchmark, bool forDefaultSymbol)
		{
			string symbol = tb.Text;
			symbol = symbol.Trim().ToUpper();

			if (symbol.Length > 0)
			{
				if (forBenchmark)
				{
					setBenchMark(symbol);
				}
				else if (forDefaultSymbol)
				{
					if (!symbol.Contains(' '))
					{
						symbol += " US Equity";
					}

					DefaultSymbol.Text = symbol;
					DefaultSymbol2.Focus();
				}
				else
				{
					if (!symbol.Contains(' '))
					{
						symbol += " US Equity";
					}

					var fields = symbol.Split(' ');

					processCompareToSymbol(symbol);
					CompareToSymbol1.Text = symbol;
					//CompareToSymbol3.Text = symbol;
					CompareToSymbol5.Text = symbol;
					//CompareToSymbol7.Text = fields[0];
					//CompareToSymbol7b.Text = fields[0];
					CompareToSymbol2.Focus();
					//CompareToSymbol4.Focus();
					CompareToSymbol6.Focus();
				}
			}
		}

		List<Trade> _strategyTrades = new List<Trade>();


		//private void calculateTrades(string symbol, string interval)
		//{
		//    var indexSymbol = _referenceData[symbol + ":" + "REL_INDEX"];
		//    _referenceData["Index Prices : " + interval] = _referenceData[indexSymbol + ":" + interval];

		//    AccuracyParser parser = new AccuracyParser(_barCache, interval, _referenceData);

		//    Strategy strategy = _strategies[_selectedStrategy];

		//    Series l1 = parser.GetSignals(symbol, strategy.LongEntry);

		//    int idx1 = strategy.LongExit.IndexOf("Exit Time ");
		//    int count1 = (idx1 >= 0) ? int.Parse(strategy.LongExit.Substring(idx1 + 10)) : 0;
		//    Series l2 = (count1 > 0) ? l1.ShiftRight(count1) : parser.GetSignals(symbol, strategy.LongExit);

		//    Series s1 = parser.GetSignals(symbol, strategy.ShortEntry);

		//    int idx2 = strategy.ShortExit.IndexOf("Exit Time ");
		//    int count2 = (idx2 >= 0) ? int.Parse(strategy.ShortExit.Substring(idx2 + 10)) : 0;
		//    Series s2 = (count2 > 0) ? s1.ShiftRight(count2) : parser.GetSignals(symbol, strategy.ShortExit);

		//    bool update1 = calculateTrades(symbol, interval, 1, l1, l2);
		//    bool update2 = calculateTrades(symbol, interval, -1, s1, s2);

		//    _update = (update1 || update2) ? 22 : 0;
		//}

		//private List<string> getStrategyPortfolio(List<string> portfolio, DateTime time)
		//{
		//    List<string> output = new List<string>();

		//    Dictionary<string, string> directions = new Dictionary<string, string>();

		//    foreach (var symbol in portfolio)
		//    {
		//        string[] fields = symbol.Split(':');
		//        var direction = fields[0];
		//        var ticker = fields[1];
		//        directions[ticker] = direction;
		//    }

		//    foreach (var trade in _strategyTrades)
		//    {
		//        if (trade.OpenDateTime <= time && time <= trade.CloseDateTime)
		//        {
		//            if (directions.ContainsKey(trade.Ticker) && trade.Direction == ((directions[trade.Ticker] == "T") ? 1 : -1))
		//            {
		//                output.Add(directions[trade.Ticker] + ":" + trade.Ticker);
		//            }
		//        }
		//    }
		//    return output;
		//}

		//private List<Trade> getStrategyTrades(List<string> portfolio, Dictionary<string, double> shares, Dictionary<string, List<Bar>> bars, DateTime time1, DateTime time2)
		//{
		//    List<string> tickers = new List<string>();
		//    Dictionary<string, string> directions = new Dictionary<string, string>();

		//    foreach(var symbol in portfolio)
		//    {
		//        string[] fields = symbol.Split(':');
		//        var direction = fields[0];
		//        var ticker = fields[1];
		//        tickers.Add(ticker);
		//        directions[ticker] = direction;
		//    }

		//    List<Trade> output = new List<Trade>();
		//    foreach (var trade in _strategyTrades)
		//    {
		//        if (trade.OpenDateTime < time2 && trade.CloseDateTime >= time1)
		//        {
		//            if (tickers.Contains(trade.Symbol))
		//            {
		//                int direction = (directions[trade.Symbol] == "T") ? 1 : -1;
		//                if (trade.Direction == direction)
		//                {
		//                    bool clipOpen = (time1 > trade.OpenDateTime);
		//                    bool clipClose = (time2 < trade.CloseDateTime);
		//                    DateTime entryTime = clipOpen ? time1 : trade.OpenDateTime;
		//                    DateTime exitTime = clipClose ? time2 : trade.CloseDateTime;
		//                    double entryPrice = clipOpen ? getPrice(bars[trade.Symbol], time1) : trade.EntryPrice;
		//                    double exitPrice = clipClose ? getPrice(bars[trade.Symbol], time2) : trade.ExitPrice;
		//                    double investment = shares[trade.Symbol] * entryPrice;
		//                    Trade strategyTrade = new Trade(trade.Network, trade.Symbol, trade.Id, trade.Direction, investment, entryTime, entryPrice, exitTime, exitPrice, time2 > DateTime.Now);
		//                    output.Add(strategyTrade);
		//                }
		//            }
		//        }
		//    }
		//    return output;
		//}

		//private bool calculateTrades(string symbol, string interval, int direction, Series entry, Series exit)
		//{
		//    bool tradeAdded = false;
		//    if (entry != null && exit != null)
		//    {
		//        var times = (_barCache.GetTimes(symbol, interval, 0));
		//        var bars = _barCache.GetSeries(symbol, interval, new string[] { "Open", "High", "Low", "Close" }, 0);

		//        int count = entry.Count;
		//        double entryPrice = double.NaN;
		//        DateTime entryTime = DateTime.Now;
		//        int entryIndex = 0;
		//        int size = 0;
		//        double close = double.NaN;
		//        for (int ii = 0; ii < count; ii++)
		//        {
		//            if (!double.IsNaN(bars[3][ii]))
		//            {
		//                close = bars[3][ii];
		//            }

		//            if (!double.IsNaN(entryPrice) && exit[ii] > 0)
		//            {
		//                // close trade
		//                tmsg.IdeaIdType id = new tmsg.IdeaIdType();
		//                id.BloombergIdSpecified = false;
		//                id.ThirdPartyId = Guid.NewGuid().ToString();

		//                double exitPrice = close;
		//                DateTime exitTime = times[ii];

		//                double maxProfit = double.MinValue;
		//                int maxProfitIndex = 0;
		//                for (int jj = entryIndex; jj <= ii; jj++)
		//                {
		//                    double price = bars[3][jj];
		//                    double profit = price - entryPrice;
		//                    if (direction < 0) profit = -profit;
		//                    if (profit > maxProfit)
		//                    {
		//                        maxProfit = profit;
		//                        maxProfitIndex = jj - entryIndex;
		//                    }
		//                }

		//                Trade trade = new Trade("US 100", symbol, id, size, 0, entryTime, entryPrice, exitTime, exitPrice, false, maxProfitIndex);

		//                _strategyTrades.Add(trade);
		//                tradeAdded = true;
		//                entryPrice = double.NaN;
		//            }

		//            if (entry[ii] > 0)
		//            {
		//                entryTime = times[ii];
		//                entryPrice = bars[3][ii];
		//                entryIndex = ii;
		//                size = direction * (int)entry[ii];
		//            }
		//        }

		//        if (!double.IsNaN(entryPrice))
		//        {
		//            // open trade
		//            tmsg.IdeaIdType id = new tmsg.IdeaIdType();
		//            id.BloombergIdSpecified = false;
		//            id.ThirdPartyId = Guid.NewGuid().ToString();

		//            Trade trade = new Trade("US 100", symbol, id, size, 0, entryTime, entryPrice, DateTime.MaxValue, double.NaN, true);
		//            _strategyTrades.Add(trade);
		//            tradeAdded = true;
		//        }
		//    }

		//    return tradeAdded;
		//}

		private bool _compareToSymbolChange = false;
		private void processCompareToSymbol(string symbol)
		{
			_compareToSymbolChange = true;
			_compareToSymbol = symbol;
			_barCache.RequestBars(_compareToSymbol, _ic != null ? _ic.GetLowestInterval() : "Daily");
		}


		private string _condition = "";
		private TreeNode _filterTree = null;
		private TreeNode _fundamentalTree = null;
		private TreeNode _atmMLTree = null;

		private void initConditionTree()
		{
			ConditionTree.Background = Brushes.Black;
			ConditionTree.Foreground = Brushes.White;
			ConditionTree.FontSize = 10;

			if (_filterTree == null)
			{
				_filterTree = new TreeNode("FiltersMain");
			}
			ConditionTree.ItemsSource = _filterTree.Children;
		}

		private void clearTree(IEnumerable<TreeNode> items)
		{
			foreach (TreeNode item in items)
			{
				item.IsExpanded = false;
				item.IsChecked = false;
				if (item.Children != null)
				{
					clearTree(item.Children);
				}
			}
		}

		private void ConditionTree_Loaded(object sender, RoutedEventArgs e)
		{
		}

		private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
		}

		private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			TextBox textBox = sender as TextBox;
			TreeNode treeNode = textBox.DataContext as TreeNode;
			treeNode.Value = textBox.Text;
		}

		private TreeNode getItem(IEnumerable<TreeNode> items, string name)
		{
			TreeNode output = null;
			foreach (TreeNode item in items)
			{
				if (item.Name == name)
				{
					item.IsExpanded = true;
					output = item;
					break;
				}
				else if (item.Children != null)
				{
					output = getItem(item.Children, name);
					if (output != null)
					{
						item.IsExpanded = true;
						break;
					}
				}
			}
			return output;
		}

		private string getCondition(TreeView input)
		{
			string output = "";

			foreach (TreeNode node in input.Items)
			{
				var text = getCondition(node);
				if (text.Length > 0)
				{
					output += ((output.Length > 0) ? "\n" : "") + text;
				}
			}
			return output;
		}

		private string getCondition(TreeNode input)
		{
			string output = "";
			if (input.HasChildren)
			{
				foreach (var node in input.Children)
				{
					var text = getCondition(node);
					if (text.Length > 0)
					{
						output += ((output.Length > 0) ? "\n" : "") + text;
					}
				}
			}
			else if (input.IsChecked)
			{
				output = input.GetDescription();
			}
			return output;
		}

		private void ConditionTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
		{
			TreeNode tn1 = e.OldValue as TreeNode;
			TreeNode tn2 = e.NewValue as TreeNode;
			if (tn1 != null) tn1.IsChecked = false;
			if (tn2 != null)
			{
				tn2.IsChecked = true;
				if (tn2.IsFundamental)
				{
					var symbol = DefaultSymbol.Text;
					var field = tn2.Name;

					_filterTree.SetValue(field, "NA");

					string[] dataFieldNames = { field };
					_filterPortfolio.RequestReferenceData(symbol, dataFieldNames, true);
				}
			}
		}

		private void _filterPortfolio_PortfolioChanged(object sender, PortfolioEventArgs e)
		{
			if (e.Type == PortfolioEventType.ReferenceData)
			{
				foreach (KeyValuePair<string, object> kvp in e.ReferenceData)
				{
					string fieldName = kvp.Key;
					object fieldValue = kvp.Value;


					if (fieldName == "CUR_MKT_CAP")
					{
						var text = fieldValue.ToString();
						if (text.Length > 0)
						{
							double value;
							if (double.TryParse(text, out value))
							{
								fieldValue = value / 1e6;
							}
						}
					}

					_filterTree.SetValue(fieldName, fieldValue);
				}
			}
		}

		private void Symbol_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			_portfolioSortType = PortfolioSortType.Ticker;
			_update = 23;
		}
		private void Sector_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			_portfolioSortType = PortfolioSortType.Sector;
			_update = 232;
		}
		private void Units_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			_portfolioSortType = PortfolioSortType.Units;
			_update = 231;
		}
		private void PnL_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			_portfolioSortType = PortfolioSortType.PnL;
			_update = 232;
		}
		private void Date_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			_portfolioSortType = PortfolioSortType.Date;
			_update = 23;
		}
		private void Weight_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			_portfolioSortType = PortfolioSortType.Weight;
			_update = 25;
		}

		private void Score_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			_portfolioSortType = PortfolioSortType.Score;
			_update = 26;
		}

		private void Return_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			_portfolioSortType = PortfolioSortType.Return;
			_update = 27;
		}
		private void NewLong_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			_portfolioSortType = PortfolioSortType.NewLong;
			_update = 28;
		}
		private void ExitLong_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			_portfolioSortType = PortfolioSortType.ExitLong;
			_update = 29;
		}
		private void NewShort_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			_portfolioSortType = PortfolioSortType.NewShort;
			_update = 30;
		}
		private void CoverShort_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			_portfolioSortType = PortfolioSortType.CoverShort;
			_update = 31;
		}
		private void AddLong_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			_portfolioSortType = PortfolioSortType.AddLong;
			_update = 312;
		}
		private void AddShort_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			_portfolioSortType = PortfolioSortType.AddShort;
			_update = 313;
		}
		private void ReduceLong_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			_portfolioSortType = PortfolioSortType.ReduceLong;
			_update = 314;
		}
		private void ReduceShort_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			_portfolioSortType = PortfolioSortType.ReduceShort;
			_update = 315;
		}

		private void CompareToSymbol_GotFocus(object sender, RoutedEventArgs e)
		{
			CompareToSymbol1.Text = "";
			//CompareToSymbol3.Text = "";
			CompareToSymbol5.Text = "";
			//CompareToSymbol7b.Text = "";
		}

		private void DefaultSymbol_GotFocus(object sender, RoutedEventArgs e)
		{
			DefaultSymbol.Text = "";
		}


		Model _model = null;


		private void MachineLearning_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			Menus.Visibility = Visibility.Visible;

			Level1Panel.Visibility = Visibility.Collapsed;
			BenchmarkMenu1.Visibility = Visibility.Collapsed;

			RankingMenu.Visibility = Visibility.Collapsed;

			MLIntervalsMenu.Visibility = Visibility.Collapsed;
			RebalanceMenu.Visibility = Visibility.Collapsed;
			AlignmentIntervalMenu.Visibility = Visibility.Collapsed;
			AutoMLIntervalMenu.Visibility = Visibility.Collapsed;
			ATMStrategyMenu.Visibility = Visibility.Collapsed;
			FeaturesMenu.Visibility = Visibility.Collapsed;

			ClassifierSetup.Visibility = Visibility.Collapsed;
			MachineLearning.Visibility = Visibility.Visible;
			PositionSizing.Visibility = Visibility.Collapsed;
			Fees.Visibility = Visibility.Collapsed;
			Commissions.Visibility = Visibility.Collapsed;
			PortfolioLeverage.Visibility = Visibility.Collapsed;
			TradeMgm.Visibility = Visibility.Collapsed;

			MLRankMenu.Visibility = Visibility.Collapsed;
			MLIterationMenu.Visibility = Visibility.Collapsed;


			SetupNav.Visibility = Visibility.Collapsed;
		}

		private void StopLoss_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			Menus.Visibility = Visibility.Visible;

			Level1Panel.Visibility = Visibility.Collapsed;
			BenchmarkMenu1.Visibility = Visibility.Collapsed;

			RankingMenu.Visibility = Visibility.Collapsed;

			MLIntervalsMenu.Visibility = Visibility.Collapsed;
			RebalanceMenu.Visibility = Visibility.Collapsed;
			AlignmentIntervalMenu.Visibility = Visibility.Collapsed;
			AutoMLIntervalMenu.Visibility = Visibility.Collapsed;
			ATMStrategyMenu.Visibility = Visibility.Collapsed;

			ClassifierSetup.Visibility = Visibility.Visible;
			MachineLearning.Visibility = Visibility.Collapsed;
			PositionSizing.Visibility = Visibility.Collapsed;
			Fees.Visibility = Visibility.Collapsed;
			Commissions.Visibility = Visibility.Collapsed;
			PortfolioLeverage.Visibility = Visibility.Collapsed;
			TradeMgm.Visibility = Visibility.Collapsed;

			MLRankMenu.Visibility = Visibility.Collapsed;
			MLIterationMenu.Visibility = Visibility.Collapsed;

			SetupNav.Visibility = Visibility.Collapsed;
		}

		//private void PortfolioConstruction_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
		//{
		//    Menus.Visibility = Visibility.Collapsed;
		//    ClassifierSetup.Visibility = Visibility.Collapsed;
		//    MachineLearning.Visibility = Visibility.Collapsed;
		//    PositionSizing.Visibility = Visibility.Collapsed;
		//    Fees.Visibility = Visibility.Collapsed;
		//    Commissions.Visibility = Visibility.Collapsed;
		//    PortfolioLeverage.Visibility = Visibility.Collapsed;
		//    TradeMgm.Visibility = Visibility.Collapsed;
		//}
		private void Fees_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			Menus.Visibility = Visibility.Visible;

			Level1Panel.Visibility = Visibility.Collapsed;
			BenchmarkMenu1.Visibility = Visibility.Collapsed;

			RankingMenu.Visibility = Visibility.Collapsed;

			MLIntervalsMenu.Visibility = Visibility.Collapsed;
			RebalanceMenu.Visibility = Visibility.Collapsed;
			AlignmentIntervalMenu.Visibility = Visibility.Collapsed;
			AutoMLIntervalMenu.Visibility = Visibility.Collapsed;
			ATMStrategyMenu.Visibility = Visibility.Collapsed;
			FeaturesMenu.Visibility = Visibility.Collapsed;

			ClassifierSetup.Visibility = Visibility.Collapsed;
			MachineLearning.Visibility = Visibility.Collapsed;
			PositionSizing.Visibility = Visibility.Collapsed;
			Fees.Visibility = Visibility.Visible;
			Commissions.Visibility = Visibility.Collapsed;
			PortfolioLeverage.Visibility = Visibility.Collapsed;
			TradeMgm.Visibility = Visibility.Collapsed;

			MLRankMenu.Visibility = Visibility.Collapsed;
			MLIterationMenu.Visibility = Visibility.Collapsed;

			SetupNav.Visibility = Visibility.Collapsed;
		}

		//private void TradeMgm_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
		//{
		//    Menus.Visibility = Visibility.Visible;
		//    ClassifierSetup.Visibility = Visibility.Collapsed;
		//    MachineLearning.Visibility = Visibility.Collapsed;
		//    PositionSizing.Visibility = Visibility.Collapsed;
		//    Fees.Visibility = Visibility.Collapsed;
		//    Commissions.Visibility = Visibility.Collapsed;
		//    PortfolioLeverage.Visibility = Visibility.Collapsed;
		//    TradeMgm.Visibility = Visibility.Visible;
		//    FeaturesMenu.Visibility = Visibility.Collapsed;

		//    MLRankMenu.Visibility = Visibility.Collapsed;
		//    MLIterationMenu.Visibility = Visibility.Collapsed;

		//    SetupNav.Visibility = Visibility.Collapsed;
		//}


		//private void Server_MouseEnter(object sender, MouseEventArgs e)
		//{
		//    Label label = sender as Label;
		//    label.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xcc, 0xff));
		//}

		//private void Server_MouseLeave(object sender, MouseEventArgs e)
		//{
		//    Label label = sender as Label;
		//    label.Foreground = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));
		//}

		private void MLResults_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{

			if (BenchmarkMenu1.Visibility == Visibility.Collapsed)
			{
				hideView();
				hideNavigation();

				UserFactorModelPanel1.Visibility = Visibility.Collapsed;
				//SetupRec.Visibility = Visibility.Collapsed;

				ATMPxInputs.Visibility = Visibility.Collapsed;

				Menus.Visibility = Visibility.Visible;
				PortfolioSettings.Visibility = Visibility.Visible;
				TrainResultsTime.Visibility = Visibility.Visible;
				TrainResults.Visibility = Visibility.Visible;
				FeaturesMenu.Visibility = Visibility.Collapsed;


				TISetup.Visibility = Visibility.Collapsed;
				//TIStats.Visibility = Visibility.Visible;
				//TIPositions.Visibility = Visibility.Collapsed;
				TIAllocations.Visibility = Visibility.Collapsed;

				MLRankMenu.Visibility = Visibility.Collapsed;
				MLIterationMenu.Visibility = Visibility.Collapsed;

				ATMTimingMenus.Visibility = Visibility.Collapsed;
				ScenarioMenu1.Visibility = Visibility.Collapsed;

				Level1Panel.Visibility = Visibility.Collapsed;
				BenchmarkMenu1.Visibility = Visibility.Collapsed;

				RankingMenu.Visibility = Visibility.Collapsed;

				MLIntervalsMenu.Visibility = Visibility.Collapsed;
				RebalanceMenu.Visibility = Visibility.Collapsed;
				AlignmentIntervalMenu.Visibility = Visibility.Collapsed;
				AutoMLIntervalMenu.Visibility = Visibility.Collapsed;
				ATMStrategyMenu.Visibility = Visibility.Collapsed;
				FeaturesMenu.Visibility = Visibility.Collapsed;

				Level2Panel.Visibility = Visibility.Collapsed;
				Level3Panel.Visibility = Visibility.Collapsed;
				Level4Panel.Visibility = Visibility.Collapsed;
				SubgroupMenuScroller1.Visibility = Visibility.Collapsed;
				Level5Panel.Visibility = Visibility.Collapsed;
				Level6Panel.Visibility = Visibility.Collapsed;
				IndustryMenuScroller1.Visibility = Visibility.Collapsed;
				SubgroupActionMenu1.Visibility = Visibility.Collapsed;
				SubgroupActionMenu2.Visibility = Visibility.Collapsed;

				FilterMenu.Visibility = Visibility.Collapsed;
				InputMenu.Visibility = Visibility.Collapsed;

				ClassifierSetup.Visibility = Visibility.Collapsed;
				MachineLearning.Visibility = Visibility.Collapsed;
				PositionSizing.Visibility = Visibility.Collapsed;
				Fees.Visibility = Visibility.Collapsed;
				Commissions.Visibility = Visibility.Collapsed;
				PortfolioLeverage.Visibility = Visibility.Collapsed;
				TradeMgm.Visibility = Visibility.Collapsed;

				SetupNav.Visibility = Visibility.Collapsed;
				MachineLearning.Visibility = Visibility.Collapsed;

				updateAutoResults();

			}
			else
			{
				showView();
			}
		}

		bool _setupEnter = false;
		bool _for_hedge_conditions = false;

		private void ATMFeatures_MouseDown(object sender, RoutedEventArgs e)
		{
			var label = sender as Label;
			_for_hedge_conditions = (label.Content as string).Contains("HEDGE");

			if (Menus.Visibility == Visibility.Collapsed)
			{
				var fe = sender as FrameworkElement;
				var mode = fe.Tag as string;
				_setupEnter = mode == "Enter";

				hideView();
				hideNavigation();

				Menus.Visibility = Visibility.Visible;

				FeaturesMenu.Visibility = Visibility.Visible;

				MLIntervalsMenu.Visibility = Visibility.Collapsed;
				ScenarioMenu1.Visibility = Visibility.Collapsed;

				Level1Panel.Visibility = Visibility.Collapsed;
				BenchmarkMenu1.Visibility = Visibility.Collapsed;

				RankingMenu.Visibility = Visibility.Collapsed;
				RebalanceMenu.Visibility = Visibility.Collapsed;
				AlignmentIntervalMenu.Visibility = Visibility.Collapsed;
				AutoMLIntervalMenu.Visibility = Visibility.Collapsed;
				ATMStrategyMenu.Visibility = Visibility.Collapsed;

				MLRankMenu.Visibility = Visibility.Collapsed;
				MLIterationMenu.Visibility = Visibility.Collapsed;

				Level2Panel.Visibility = Visibility.Collapsed;
				Level3Panel.Visibility = Visibility.Collapsed;
				Level4Panel.Visibility = Visibility.Collapsed;
				SubgroupMenuScroller1.Visibility = Visibility.Collapsed;
				Level5Panel.Visibility = Visibility.Collapsed;
				Level6Panel.Visibility = Visibility.Collapsed;
				IndustryMenuScroller1.Visibility = Visibility.Collapsed;
				SubgroupActionMenu1.Visibility = Visibility.Collapsed;
				SubgroupActionMenu2.Visibility = Visibility.Collapsed;

				TrainResults.Visibility = Visibility.Collapsed;

				FilterMenu.Visibility = Visibility.Collapsed;
				InputMenu.Visibility = Visibility.Collapsed;
				PortfolioSettings.Visibility = Visibility.Collapsed;

				_universeSelection = true;
				_benchmarkSelection = false;

				var model = getModel();
				var text1 = String.Join("\n", _for_hedge_conditions ? model.Groups[_g].ATMConditionsHedge.ToArray() : _setupEnter ? model.Groups[_g].ATMConditionsEnter.ToArray() : model.Groups[_g].ATMConditionsExit.ToArray());
				setFeatures(text1);

				//nav.UseCheckBoxes = false;

				////if (MainView.EnablePortfolio)
				//{
				//    List<string> items = new List<string>();

				//    items.AddRange(new string[]
				//    {

				//    });

				//    nav.UseCheckBoxes = false;
				//    nav.setNavigation(ATMFeatures, ATMFeatures_MouseDown, items.ToArray());
				//    //addSaveAndCloseButtons(ATMFeatures, Save_Click, Close_Click);
				//}

				//string resourceKey = "";

				//if (resourceKey.Length > 0)
				//{
				//    Viewbox legend = this.Resources[resourceKey] as Viewbox;
				//    if (legend != null)
				//    {
				//        legend.SetValue(MarginProperty, new Thickness(0, 20, 0, 0));
				//        Level1Panel.Children.Add(legend);
				//    }
				//}
			}
			else
			{
				showView();
			}
		}


		//private void MLResults2_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
		//{

		//    if (BenchmarkMenu1.Visibility == Visibility.Collapsed)
		//    {
		//        hideView();
		//        hideNavigation();

		//        UserFactorModelPanel1.Visibility = Visibility.Collapsed;
		//        SetupRec.Visibility = Visibility.Collapsed;

		//        Menus.Visibility = Visibility.Visible;
		//        //PortfolioSettings.Visibility = Visibility.Visible;
		//        TrainResultsTime.Visibility = Visibility.Visible;
		//        TrainResults.Visibility = Visibility.Visible;

		//        ATMPxInputs.Visibility = Visibility.Collapsed;
		//        //Step1Text.Visibility = Visibility.Collapsed;

		//        TISetup.Visibility = Visibility.Collapsed;
		//        TIStats.Visibility = Visibility.Collapsed;
		//        //TIPositions.Visibility = Visibility.Visible;
		//        //TIOpenPL.Visibility = Visibility.Collapsed;
		//        TIAllocations.Visibility = Visibility.Collapsed;

		//        ATMTimingMenus.Visibility = Visibility.Collapsed;
		//        ScenarioMenu1.Visibility = Visibility.Collapsed;

		//        MLRankMenu.Visibility = Visibility.Collapsed;
		//        MLIterationMenu.Visibility = Visibility.Collapsed;

		//        RegionMenu.Visibility = Visibility.Collapsed;
		//        BenchmarkMenu1.Visibility = Visibility.Collapsed;

		//        RankingMenu.Visibility = Visibility.Collapsed;
		//        
		//        MLIntervalsMenu.Visibility = Visibility.Collapsed;
		//        RebalanceMenu.Visibility = Visibility.Collapsed;
		//        ATMStrategyMenu.Visibility = Visibility.Collapsed;
		//        FeaturesMenu.Visibility = Visibility.Collapsed;

		//        CountryMenu.Visibility = Visibility.Collapsed;
		//        GroupMenu1.Visibility = Visibility.Collapsed;
		//        SubgroupMenu1.Visibility = Visibility.Collapsed;
		//        SubgroupMenuScroller1.Visibility = Visibility.Collapsed;
		//        IndustryMenu1.Visibility = Visibility.Collapsed;
		//        IndustryMenuScroller1.Visibility = Visibility.Collapsed;
		//        SubgroupActionMenu1.Visibility = Visibility.Collapsed;
		//        SubgroupActionMenu2.Visibility = Visibility.Collapsed;

		//        FilterMenu.Visibility = Visibility.Collapsed;
		//        InputMenu.Visibility = Visibility.Collapsed;


		//        ClassifierSetup.Visibility = Visibility.Collapsed;
		//        MachineLearning.Visibility = Visibility.Collapsed;
		//        PositionSizing.Visibility = Visibility.Collapsed;
		//        Fees.Visibility = Visibility.Collapsed;
		//        Commissions.Visibility = Visibility.Collapsed;
		//        PortfolioLeverage.Visibility = Visibility.Collapsed;
		//        TradeMgm.Visibility = Visibility.Collapsed;

		//        SetupNav.Visibility = Visibility.Collapsed;
		//        MachineLearning.Visibility = Visibility.Collapsed;

		//        updateAutoResults();

		//    }
		//    else
		//    {
		//        showView();
		//    }
		//}


		//private void Leverage_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
		//{
		//    Menus.Visibility = Visibility.Visible;
		//    ClassifierSetup.Visibility = Visibility.Collapsed;
		//    MachineLearning.Visibility = Visibility.Collapsed;
		//    PositionSizing.Visibility = Visibility.Collapsed;
		//    Fees.Visibility = Visibility.Collapsed;
		//    Commissions.Visibility = Visibility.Collapsed;
		//    PortfolioLeverage.Visibility = Visibility.Visible;
		//    TradeMgm.Visibility = Visibility.Collapsed;

		//    MLRankMenu.Visibility = Visibility.Collapsed;
		//    MLIterationMenu.Visibility = Visibility.Collapsed;
		//}
		//private void PortfolioSetup_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
		//{
		//    Menus.Visibility = Visibility.Collapsed;

		//    RegionMenu.Visibility = Visibility.Collapsed;
		//    BenchmarkMenu1.Visibility = Visibility.Collapsed;

		//    
		//    MLIntervalsMenu.Visibility = Visibility.Collapsed;
		//    RankingMenu.Visibility = Visibility.Collapsed;
		//    RebalanceMenu.Visibility = Visibility.Collapsed;
		//    ATMStrategyMenu.Visibility = Visibility.Collapsed;
		//    FeaturesMenu.Visibility = Visibility.Collapsed;

		//    ClassifierSetup.Visibility = Visibility.Collapsed;
		//    MachineLearning.Visibility = Visibility.Collapsed;
		//    PositionSizing.Visibility = Visibility.Visible;
		//    Fees.Visibility = Visibility.Collapsed;
		//    Commissions.Visibility = Visibility.Collapsed;
		//    PortfolioLeverage.Visibility = Visibility.Collapsed;
		//    TradeMgm.Visibility = Visibility.Collapsed;

		//    ReturnTable.Visibility = Visibility.Collapsed;
		//    RiskTable.Visibility = Visibility.Collapsed;
		//    RecoveryTable.Visibility = Visibility.Collapsed;

		//    SetupNav.Visibility = Visibility.Collapsed;
		//}
		private void PositionSizing_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			Menus.Visibility = Visibility.Visible;

			Level1Panel.Visibility = Visibility.Collapsed;
			BenchmarkMenu1.Visibility = Visibility.Collapsed;

			RankingMenu.Visibility = Visibility.Collapsed;

			MLIntervalsMenu.Visibility = Visibility.Collapsed;
			RebalanceMenu.Visibility = Visibility.Collapsed;
			AlignmentIntervalMenu.Visibility = Visibility.Collapsed;
			AutoMLIntervalMenu.Visibility = Visibility.Collapsed;
			ATMStrategyMenu.Visibility = Visibility.Collapsed;
			FeaturesMenu.Visibility = Visibility.Collapsed;

			ClassifierSetup.Visibility = Visibility.Collapsed;
			MachineLearning.Visibility = Visibility.Collapsed;
			PositionSizing.Visibility = Visibility.Visible;
			Fees.Visibility = Visibility.Collapsed;
			Commissions.Visibility = Visibility.Collapsed;
			PortfolioLeverage.Visibility = Visibility.Collapsed;
			TradeMgm.Visibility = Visibility.Collapsed;

			MLRankMenu.Visibility = Visibility.Collapsed;
			MLIterationMenu.Visibility = Visibility.Collapsed;

			SetupNav.Visibility = Visibility.Collapsed;
		}

		private void LTConditions_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			Menus.Visibility = Visibility.Visible;

			Level1Panel.Visibility = Visibility.Collapsed;
			BenchmarkMenu1.Visibility = Visibility.Collapsed;

			RankingMenu.Visibility = Visibility.Collapsed;

			MLIntervalsMenu.Visibility = Visibility.Collapsed;
			RebalanceMenu.Visibility = Visibility.Collapsed;
			AlignmentIntervalMenu.Visibility = Visibility.Collapsed;
			AutoMLIntervalMenu.Visibility = Visibility.Collapsed;
			ATMStrategyMenu.Visibility = Visibility.Collapsed;
			FeaturesMenu.Visibility = Visibility.Collapsed;

			MLRankMenu.Visibility = Visibility.Collapsed;
			MLIterationMenu.Visibility = Visibility.Collapsed;

			ClassifierSetup.Visibility = Visibility.Collapsed;
			MachineLearning.Visibility = Visibility.Visible;
			PositionSizing.Visibility = Visibility.Collapsed;
			Fees.Visibility = Visibility.Collapsed;
			Commissions.Visibility = Visibility.Collapsed;
			PortfolioLeverage.Visibility = Visibility.Collapsed;
			TradeMgm.Visibility = Visibility.Collapsed;

			SetupNav.Visibility = Visibility.Collapsed;

			UseTimingGrid.Visibility = Visibility.Visible;
			LongTermTimingGrid.Visibility = Visibility.Visible;
			MidTermTimingGrid.Visibility = Visibility.Collapsed;
			ShortTermTimingGrid.Visibility = Visibility.Collapsed;

			LongTermTimingNav.Visibility = Visibility.Visible;
			LongTermTimingNav2.Visibility = Visibility.Visible;
			MidTermTimingNav.Visibility = Visibility.Collapsed;
			MidTermTimingNav2.Visibility = Visibility.Collapsed;
			ShortTermTimingNav.Visibility = Visibility.Collapsed;
			ShortTermTimingNav2.Visibility = Visibility.Collapsed;

		}


		private void MTConditions_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			Menus.Visibility = Visibility.Visible;

			Level1Panel.Visibility = Visibility.Collapsed;
			BenchmarkMenu1.Visibility = Visibility.Collapsed;

			RankingMenu.Visibility = Visibility.Collapsed;

			MLIntervalsMenu.Visibility = Visibility.Collapsed;
			RebalanceMenu.Visibility = Visibility.Collapsed;
			AlignmentIntervalMenu.Visibility = Visibility.Collapsed;
			AutoMLIntervalMenu.Visibility = Visibility.Collapsed;
			ATMStrategyMenu.Visibility = Visibility.Collapsed;
			FeaturesMenu.Visibility = Visibility.Collapsed;


			MLRankMenu.Visibility = Visibility.Collapsed;
			MLIterationMenu.Visibility = Visibility.Collapsed;

			ClassifierSetup.Visibility = Visibility.Collapsed;
			MachineLearning.Visibility = Visibility.Visible;
			PositionSizing.Visibility = Visibility.Collapsed;
			Fees.Visibility = Visibility.Collapsed;
			Commissions.Visibility = Visibility.Collapsed;
			PortfolioLeverage.Visibility = Visibility.Collapsed;
			TradeMgm.Visibility = Visibility.Collapsed;

			SetupNav.Visibility = Visibility.Collapsed;

			UseTimingGrid.Visibility = Visibility.Visible;
			LongTermTimingGrid.Visibility = Visibility.Collapsed;
			MidTermTimingGrid.Visibility = Visibility.Visible;
			ShortTermTimingGrid.Visibility = Visibility.Collapsed;

			LongTermTimingNav.Visibility = Visibility.Collapsed;
			LongTermTimingNav2.Visibility = Visibility.Collapsed;
			MidTermTimingNav.Visibility = Visibility.Visible;
			MidTermTimingNav2.Visibility = Visibility.Visible;
			ShortTermTimingNav.Visibility = Visibility.Collapsed;
			ShortTermTimingNav2.Visibility = Visibility.Collapsed;
		}

		private void STConditions_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			Menus.Visibility = Visibility.Visible;

			Level1Panel.Visibility = Visibility.Collapsed;
			BenchmarkMenu1.Visibility = Visibility.Collapsed;

			RankingMenu.Visibility = Visibility.Collapsed;

			MLIntervalsMenu.Visibility = Visibility.Collapsed;
			RebalanceMenu.Visibility = Visibility.Collapsed;
			AlignmentIntervalMenu.Visibility = Visibility.Collapsed;
			AutoMLIntervalMenu.Visibility = Visibility.Collapsed;
			ATMStrategyMenu.Visibility = Visibility.Collapsed;
			FeaturesMenu.Visibility = Visibility.Collapsed;

			MLRankMenu.Visibility = Visibility.Collapsed;
			MLIterationMenu.Visibility = Visibility.Collapsed;

			ClassifierSetup.Visibility = Visibility.Collapsed;
			MachineLearning.Visibility = Visibility.Visible;
			PositionSizing.Visibility = Visibility.Collapsed;
			Fees.Visibility = Visibility.Collapsed;
			Commissions.Visibility = Visibility.Collapsed;
			PortfolioLeverage.Visibility = Visibility.Collapsed;
			TradeMgm.Visibility = Visibility.Collapsed;

			SetupNav.Visibility = Visibility.Collapsed;

			UseTimingGrid.Visibility = Visibility.Visible;
			LongTermTimingGrid.Visibility = Visibility.Collapsed;
			MidTermTimingGrid.Visibility = Visibility.Collapsed;
			ShortTermTimingGrid.Visibility = Visibility.Visible;

			LongTermTimingNav.Visibility = Visibility.Collapsed;
			LongTermTimingNav2.Visibility = Visibility.Collapsed;
			MidTermTimingNav.Visibility = Visibility.Collapsed;
			MidTermTimingNav2.Visibility = Visibility.Collapsed;
			ShortTermTimingNav.Visibility = Visibility.Visible;
			ShortTermTimingNav2.Visibility = Visibility.Visible;
		}

		private void Commission_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			Menus.Visibility = Visibility.Visible;

			Level1Panel.Visibility = Visibility.Collapsed;
			BenchmarkMenu1.Visibility = Visibility.Collapsed;

			RankingMenu.Visibility = Visibility.Collapsed;

			MLIntervalsMenu.Visibility = Visibility.Collapsed;
			RebalanceMenu.Visibility = Visibility.Collapsed;
			AlignmentIntervalMenu.Visibility = Visibility.Collapsed;
			AutoMLIntervalMenu.Visibility = Visibility.Collapsed;
			ATMStrategyMenu.Visibility = Visibility.Collapsed;
			FeaturesMenu.Visibility = Visibility.Collapsed;

			MLRankMenu.Visibility = Visibility.Collapsed;
			MLIterationMenu.Visibility = Visibility.Collapsed;

			ClassifierSetup.Visibility = Visibility.Collapsed;
			MachineLearning.Visibility = Visibility.Collapsed;
			PositionSizing.Visibility = Visibility.Collapsed;
			Fees.Visibility = Visibility.Collapsed;
			Commissions.Visibility = Visibility.Visible;
			PortfolioLeverage.Visibility = Visibility.Collapsed;
			TradeMgm.Visibility = Visibility.Collapsed;

			SetupNav.Visibility = Visibility.Collapsed;

		}

		private void DataTransformation_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			Menus.Visibility = Visibility.Visible;
			ClassifierSetup.Visibility = Visibility.Collapsed;
			MachineLearning.Visibility = Visibility.Collapsed;
			PositionSizing.Visibility = Visibility.Collapsed;
			Fees.Visibility = Visibility.Collapsed;
			Commissions.Visibility = Visibility.Collapsed;
			PortfolioLeverage.Visibility = Visibility.Collapsed;
			TradeMgm.Visibility = Visibility.Collapsed;
			MLRankMenu.Visibility = Visibility.Collapsed;
			MLIterationMenu.Visibility = Visibility.Collapsed;
			FeaturesMenu.Visibility = Visibility.Collapsed;
		}

		private string _mlClassifier = "Decision Forest";
		public string MLClassifier
		{
			get
			{
				return _mlClassifier;
			}

			set
			{
				_mlClassifier = value;

				//if (BayesianLinear != null)
				//{
				//    BayesianLinear.Visibility = (_mlClassifier == "Bayesian Linear") ? Visibility.Visible : Visibility.Collapsed;
				//    BoostedDecisionTree.Visibility = (_mlClassifier == "Boosted Decision Tree") ? Visibility.Visible : Visibility.Collapsed;
				//    DecisionForest.Visibility = (_mlClassifier == "Decision Forest") ? Visibility.Visible : Visibility.Collapsed;
				//    FastForestQuantile.Visibility = (_mlClassifier == "Fast Forest Quantile") ? Visibility.Visible : Visibility.Collapsed;
				//    Linear.Visibility = (_mlClassifier == "Linear") ? Visibility.Visible : Visibility.Collapsed;
				//    Ordinal.Visibility = (_mlClassifier == "Ordinal") ? Visibility.Visible : Visibility.Collapsed;
				//    Poisson.Visibility = (_mlClassifier == "Poisson") ? Visibility.Visible : Visibility.Collapsed;
				//}
			}
		}


		//private void MLClassifier_SelectionChanged(object sender, SelectionChangedEventArgs e)
		//{
		//    var cb = sender as ComboBox;
		//    MLClassifier = (cb.SelectedValue as ComboBoxItem).Content as string;
		//}


		//private void Cancel_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
		//{
		//    //this.DialogResult = false;
		//    this.Close();
		//}

		private void CloseSettings_MouseDown(object sender, RoutedEventArgs e)
		{
			hideNavigation();

			ClassifierSetup.Visibility = Visibility.Visible;
			MachineLearning.Visibility = Visibility.Collapsed;
			PositionSizing.Visibility = Visibility.Collapsed;
			Fees.Visibility = Visibility.Collapsed;
			Commissions.Visibility = Visibility.Collapsed;
			PortfolioLeverage.Visibility = Visibility.Collapsed;
			TradeMgm.Visibility = Visibility.Collapsed;

			SetupNav.Visibility = Visibility.Collapsed;

			showView();
			ui_to_userFactorModel(_selectedUserFactorModel);
			//saveUserFactorModels();
		}

		private void SaveSettings_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
		}

		private void AODView_MouseDown(object sender, MouseButtonEventArgs e)
		{
			Close();
			// _mainView.Content = new Competitor(_mainView);
		}

		private void Exit_Enter(object sender, MouseEventArgs e)
		{
			Label label = sender as Label;
			label.Background = Brushes.DimGray;
		}

		private void Exit_Leave(object sender, MouseEventArgs e)
		{
			Label label = sender as Label;
			label.Background = new SolidColorBrush(Color.FromRgb(0x30, 0x30, 0x30));
		}

		private void LandingPage_MouseDown(object sender, MouseButtonEventArgs e)
		{
			Close();
			_mainView.Content = new LandingPage(_mainView);
		}

		private void PerformanceTable_MouseDown(object sender, MouseButtonEventArgs e)
		{
			PerformanceTable.Visibility = Visibility.Visible;
			ReturnTable.Visibility = Visibility.Collapsed;
			ATMMLTable.Visibility = Visibility.Collapsed;
		}

		private void RiskTable_MouseDown(object sender, MouseButtonEventArgs e)
		{
			PerformanceTable.Visibility = Visibility.Collapsed;
			ReturnTable.Visibility = Visibility.Visible;
			ATMMLTable.Visibility = Visibility.Collapsed;
		}
		//private void ATMMLTable_MouseDown(object sender, MouseButtonEventArgs e)
		//{
		//    PerformanceTable.Visibility = Visibility.Collapsed;
		//    ReturnTable.Visibility = Visibility.Collapsed;
		//    ATMMLTable.Visibility = Visibility.Visible;
		//}

		private void CursorLeft_MouseDown(object sender, MouseButtonEventArgs e)
		{
			if (_projectedRebalDate != default(DateTime) && !_portfolioTimes.Any(t => t.Date == _projectedRebalDate.Date))
				_portfolioTimes.Add(_projectedRebalDate);
			var index = _portfolioTimes.FindLastIndex(x => x.Date < _showHoldingsTime.Date);
			if (index >= 0)
			{
				setHoldingsCursorTime(_portfolioTimes[index]);
				updateStatistics();
			}
		}

		private void CursorRight_MouseDown(object sender, MouseButtonEventArgs e)
		{
			if (_projectedRebalDate != default(DateTime) && !_portfolioTimes.Any(t => t.Date == _projectedRebalDate.Date))
				_portfolioTimes.Add(_projectedRebalDate);
			var index = _portfolioTimes.FindIndex(x => x.Date > _showHoldingsTime.Date);
			if (index >= 0)
			{
				setHoldingsCursorTime(_portfolioTimes[index]);
				updateStatistics();
			}
		}

		private bool isHistorical(string interval)
		{
			string text = interval.Substring(0, 1);
			bool historical = (text == "D" || text == "W" || text == "M" || text == "S" || text == "Q" || text == "Y");
			return historical;
		}

		private void setHoldingsCursorTime(DateTime time)
		{
			var model = getModel();

			if (model != null && time != _showHoldingsTime)
			{
				// Do NOT reset _liveMtMBalance on navigation — refreshLiveBalance manages it
				// and resetting here causes flickering as it gets written back immediately.

				var interval = (_ic != null) ? _ic.GetLowestInterval() : model.RankingInterval;

				_showHoldingsTime = time;// - new TimeSpan(1, 0, 0, 0, 0);

				var pcs = getRankingPercents(model);
				var cnt = model.Symbols.Count;

				int rlcnt = (int)Math.Round((100 - pcs.Item1) * cnt / 100.0);
				int rscnt = (int)Math.Round(pcs.Item2 * cnt / 100.0);

				// Pre-order / intra-week: counts reflect last SETTLED Friday's holdings.
				// Use the most recent settled Friday (a real Friday ≤ today) not Count-2
				// which may land on an intra-week daily nav date.
				var lastSettledFriday = _portfolioTimes
					.Where(t => t != default(DateTime) && t.Date <= DateTime.Today && t.DayOfWeek == DayOfWeek.Friday)
					.OrderByDescending(t => t).FirstOrDefault();
				// Position counts: any date after the last settled Friday (intra-week or projected
				// rebalance) shows the CURRENT holdings — not the projected next portfolio.
				var countTime = (model.IsLiveMode
					&& lastSettledFriday != default(DateTime)
					&& time.Date > lastSettledFriday.Date)
					? lastSettledFriday
					: time;

				double lcnt = getHoldings(countTime, 1, 1);
				double scnt = getHoldings(countTime, -1, 1);

				double lcntAll = getHoldings(countTime, 1, 1, true);
				double scntAll = getHoldings(countTime, -1, 1, true);

				LongHoldings1.Content = (lcnt > 0) ? lcnt.ToString() : "";
				LongHoldings7a.Content = (lcnt > 0) ? lcnt.ToString() : "";
				LongHoldings2a.Content = (lcntAll > 0) ? lcntAll.ToString() : "";
				//LongHoldings2b.Content = (lcntAll > 0) ? lcntAll.ToString() : "";
				//PORTLongCount2.Content = (lcnt > 0) ? lcnt.ToString() : "";
				//LongHoldings3.Content = (lcnt > 0) ? lcnt.ToString() : "";

				ShortHoldings1a.Content = (scnt > 0) ? scnt.ToString() : "";
				//ShortHoldings1.Content = (scnt > 0) ? scnt.ToString() : "";
				ShortHoldings7a.Content = (scnt > 0) ? scnt.ToString() : "";
				ShortHoldings2a.Content = (scntAll > 0) ? scntAll.ToString() : "";
				//ShortHoldings2b.Content = (scntAll > 0) ? scntAll.ToString() : "";
				//ShortHoldings3.Content = (scnt > 0) ? scnt.ToString() : "";

				string format = isHistorical(interval) ? "MM-dd-yy" : "MM-dd-yyyy HH:mm";
				time = isHistorical(interval) ? time : time.ToLocalTime();
				//DateRebalance.Content = time.ToString(format);
				DateRebalance2.Content = time.ToString(format);
				DateRebalance3.Content = time.ToString(format);
				DateRebalance4.Content = time.ToString(format);

				drawPortfolioGrid();
			}
		}


		private void getTradeMangementSettings()
		{
			bool useDefaults = true;

			string info = _mainView.GetInfo("TradeMangement");
			if (info != null && info.Length > 0)
			{
				int version = 0;

				string[] fields = info.Split(';');
				int count = fields.Length;

				if (count > 0 && int.TryParse(fields[0], out version))
				{
					if (version == 1)
					{
						useDefaults = false;
						//if (count > 1) cbStopLoss.IsChecked = bool.Parse(fields[1]);
						//if (count > 2) StopLossPercent.Text = fields[2];
						if (count > 3) cbLTFTUp.IsChecked = bool.Parse(fields[3]);
						if (count > 4) cbLTFTDn.IsChecked = bool.Parse(fields[4]);
						if (count > 5) cbLTSCBuy.IsChecked = bool.Parse(fields[5]);
						if (count > 6) cbLTSCSell.IsChecked = bool.Parse(fields[6]);
						if (count > 7) cbLTSTBuy.IsChecked = bool.Parse(fields[7]);
						if (count > 8) cbLTSTSell.IsChecked = bool.Parse(fields[8]);
						if (count > 9) cbLTFTBuy.IsChecked = bool.Parse(fields[9]);
						if (count > 10) cbLTFTSell.IsChecked = bool.Parse(fields[10]);
						if (count > 11) cbLTNetLong.IsChecked = bool.Parse(fields[11]);
						if (count > 12) cbLTNetShort.IsChecked = bool.Parse(fields[12]);
						if (count > 13) rbLTLong0.IsChecked = bool.Parse(fields[13]);
						if (count > 14) rbLTShort0.IsChecked = bool.Parse(fields[14]);
						if (count > 15) rbLTLong1.IsChecked = bool.Parse(fields[15]);
						if (count > 16) rbLTShort1.IsChecked = bool.Parse(fields[16]);
						if (count > 17) rbLTLong2.IsChecked = bool.Parse(fields[17]);
						if (count > 18) rbLTShort2.IsChecked = bool.Parse(fields[18]);
						if (count > 19) rbLTLong3.IsChecked = bool.Parse(fields[19]);
						if (count > 20) rbLTShort3.IsChecked = bool.Parse(fields[20]);
						if (count > 21) cbMTFTUp.IsChecked = bool.Parse(fields[21]);
						if (count > 22) cbMTFTDn.IsChecked = bool.Parse(fields[22]);
						if (count > 23) cbMTSCBuy.IsChecked = bool.Parse(fields[23]);
						if (count > 24) cbMTSCSell.IsChecked = bool.Parse(fields[24]);
						if (count > 25) cbMTSTBuy.IsChecked = bool.Parse(fields[25]);
						if (count > 26) cbMTSTSell.IsChecked = bool.Parse(fields[26]);
						if (count > 27) cbMTFTBuy.IsChecked = bool.Parse(fields[27]);
						if (count > 28) cbMTFTSell.IsChecked = bool.Parse(fields[28]);
						if (count > 29) cbMTNetLong.IsChecked = bool.Parse(fields[29]);
						if (count > 30) cbMTNetShort.IsChecked = bool.Parse(fields[30]);
						if (count > 31) rbMTLong0.IsChecked = bool.Parse(fields[31]);
						if (count > 32) rbMTShort0.IsChecked = bool.Parse(fields[32]);
						if (count > 33) rbMTLong1.IsChecked = bool.Parse(fields[33]);
						if (count > 34) rbMTShort1.IsChecked = bool.Parse(fields[34]);
						if (count > 35) rbMTLong2.IsChecked = bool.Parse(fields[35]);
						if (count > 36) rbMTShort2.IsChecked = bool.Parse(fields[36]);
						if (count > 37) rbMTLong3.IsChecked = bool.Parse(fields[37]);
						if (count > 38) rbMTShort3.IsChecked = bool.Parse(fields[38]);
						if (count > 39) cbSTFTUp.IsChecked = bool.Parse(fields[39]);
						if (count > 40) cbSTFTDn.IsChecked = bool.Parse(fields[40]);
						if (count > 41) cbSTSCBuy.IsChecked = bool.Parse(fields[41]);
						if (count > 42) cbSTSCSell.IsChecked = bool.Parse(fields[42]);
						if (count > 43) cbSTSTBuy.IsChecked = bool.Parse(fields[43]);
						if (count > 44) cbSTSTSell.IsChecked = bool.Parse(fields[44]);
						if (count > 45) cbSTFTBuy.IsChecked = bool.Parse(fields[45]);
						if (count > 46) cbSTFTSell.IsChecked = bool.Parse(fields[46]);
						if (count > 47) cbSTNetLong.IsChecked = bool.Parse(fields[47]);
						if (count > 48) cbSTNetShort.IsChecked = bool.Parse(fields[48]);
						if (count > 49) LongNet0.IsChecked = bool.Parse(fields[49]);
						if (count > 50) ShortNet0.IsChecked = bool.Parse(fields[50]);
						if (count > 51) LongNet1.IsChecked = bool.Parse(fields[51]);
						if (count > 52) ShortNet1.IsChecked = bool.Parse(fields[52]);
						if (count > 53) LongNet2.IsChecked = bool.Parse(fields[53]);
						if (count > 54) ShortNet2.IsChecked = bool.Parse(fields[54]);
						if (count > 55) LongNet3.IsChecked = bool.Parse(fields[55]);
						if (count > 56) ShortNet3.IsChecked = bool.Parse(fields[56]);
						if (count > 57) cbLTSTUp.IsChecked = bool.Parse(fields[57]);
						if (count > 58) cbLTSTDn.IsChecked = bool.Parse(fields[58]);
						if (count > 59) cbLTTSBUp.IsChecked = bool.Parse(fields[59]);
						if (count > 60) cbLTTSBDn.IsChecked = bool.Parse(fields[60]);
						if (count > 61) cbLTTLUp.IsChecked = bool.Parse(fields[61]);
						if (count > 62) cbLTTLDn.IsChecked = bool.Parse(fields[62]);
						if (count > 63) cbLTTBUp.IsChecked = bool.Parse(fields[63]);
						if (count > 64) cbLTTBDn.IsChecked = bool.Parse(fields[64]);
						if (count > 65) cbMTSTUp.IsChecked = bool.Parse(fields[65]);
						if (count > 66) cbMTSTDn.IsChecked = bool.Parse(fields[66]);
						if (count > 67) cbMTTSBUp.IsChecked = bool.Parse(fields[67]);
						if (count > 68) cbMTTSBDn.IsChecked = bool.Parse(fields[68]);
						if (count > 69) cbMTTLUp.IsChecked = bool.Parse(fields[69]);
						if (count > 70) cbMTTLDn.IsChecked = bool.Parse(fields[70]);
						if (count > 71) cbMTTBUp.IsChecked = bool.Parse(fields[71]);
						if (count > 72) cbMTTBDn.IsChecked = bool.Parse(fields[72]);
						if (count > 73) cbSTSTUp.IsChecked = bool.Parse(fields[73]);
						if (count > 74) cbSTSTDn.IsChecked = bool.Parse(fields[74]);
						if (count > 75) cbSTTSBUp.IsChecked = bool.Parse(fields[75]);
						if (count > 76) cbSTTSBDn.IsChecked = bool.Parse(fields[76]);
						if (count > 77) cbSTTLUp.IsChecked = bool.Parse(fields[77]);
						if (count > 78) cbSTTLDn.IsChecked = bool.Parse(fields[78]);
						if (count > 79) cbSTTBUp.IsChecked = bool.Parse(fields[79]);
						if (count > 80) cbSTTBDn.IsChecked = bool.Parse(fields[80]);
						if (count > 81) cbLTFTOB.IsChecked = bool.Parse(fields[81]);
						if (count > 82) cbMTFTOB.IsChecked = bool.Parse(fields[82]);
						if (count > 83) cbSTFTOB.IsChecked = bool.Parse(fields[83]);
						if (count > 84) cbLTFTOS.IsChecked = bool.Parse(fields[84]);
						if (count > 85) cbMTFTOS.IsChecked = bool.Parse(fields[85]);
						if (count > 86) cbSTFTOS.IsChecked = bool.Parse(fields[86]);
						// add properties
					}
				}
			}

			if (useDefaults)
			{
				cbMTFTUp.IsChecked = true;
				cbMTFTDn.IsChecked = true;
				cbMTSCBuy.IsChecked = true;
				cbMTSCSell.IsChecked = true;
				cbSTSCBuy.IsChecked = true;
				cbSTSCSell.IsChecked = true;
			}
		}

		private void setTradeMangementSettings()
		{
			int version = 1;

			string info =
				version.ToString() + ";" +
				//cbStopLoss.IsChecked.ToString() + ";" +
				//StopLossPercent.Text + ";" +
				cbLTFTUp.IsChecked.ToString() + ";" +
				cbLTFTDn.IsChecked.ToString() + ";" +
				cbLTSCBuy.IsChecked.ToString() + ";" +
				cbLTSCSell.IsChecked.ToString() + ";" +
				cbLTSTBuy.IsChecked.ToString() + ";" +
				cbLTSTSell.IsChecked.ToString() + ";" +
				cbLTFTBuy.IsChecked.ToString() + ";" +
				cbLTFTSell.IsChecked.ToString() + ";" +
				cbLTNetLong.IsChecked.ToString() + ";" +
				cbLTNetShort.IsChecked.ToString() + ";" +
				rbLTLong0.IsChecked.ToString() + ";" +
				rbLTShort0.IsChecked.ToString() + ";" +
				rbLTLong1.IsChecked.ToString() + ";" +
				rbLTShort1.IsChecked.ToString() + ";" +
				rbLTLong2.IsChecked.ToString() + ";" +
				rbLTShort2.IsChecked.ToString() + ";" +
				rbLTLong3.IsChecked.ToString() + ";" +
				rbLTShort3.IsChecked.ToString() + ";" +
				cbMTFTUp.IsChecked.ToString() + ";" +
				cbMTFTDn.IsChecked.ToString() + ";" +
				cbMTSCBuy.IsChecked.ToString() + ";" +
				cbMTSCSell.IsChecked.ToString() + ";" +
				cbMTSTBuy.IsChecked.ToString() + ";" +
				cbMTSTSell.IsChecked.ToString() + ";" +
				cbMTFTBuy.IsChecked.ToString() + ";" +
				cbMTFTSell.IsChecked.ToString() + ";" +
				cbMTNetLong.IsChecked.ToString() + ";" +
				cbMTNetShort.IsChecked.ToString() + ";" +
				rbMTLong0.IsChecked.ToString() + ";" +
				rbMTShort0.IsChecked.ToString() + ";" +
				rbMTLong1.IsChecked.ToString() + ";" +
				rbMTShort1.IsChecked.ToString() + ";" +
				rbMTLong2.IsChecked.ToString() + ";" +
				rbMTShort2.IsChecked.ToString() + ";" +
				rbMTLong3.IsChecked.ToString() + ";" +
				rbMTShort3.IsChecked.ToString() + ";" +
				cbSTFTUp.IsChecked.ToString() + ";" +
				cbSTFTDn.IsChecked.ToString() + ";" +
				cbSTSCBuy.IsChecked.ToString() + ";" +
				cbSTSCSell.IsChecked.ToString() + ";" +
				cbSTSTBuy.IsChecked.ToString() + ";" +
				cbSTSTSell.IsChecked.ToString() + ";" +
				cbSTFTBuy.IsChecked.ToString() + ";" +
				cbSTFTSell.IsChecked.ToString() + ";" +
				cbSTNetLong.IsChecked.ToString() + ";" +
				cbSTNetShort.IsChecked.ToString() + ";" +
				LongNet0.IsChecked.ToString() + ";" +
				ShortNet0.IsChecked.ToString() + ";" +
				LongNet1.IsChecked.ToString() + ";" +
				ShortNet1.IsChecked.ToString() + ";" +
				LongNet2.IsChecked.ToString() + ";" +
				ShortNet2.IsChecked.ToString() + ";" +
				LongNet3.IsChecked.ToString() + ";" +
				ShortNet3.IsChecked.ToString() + ";" +
				//UseATMStrategies.IsChecked.ToString() + ";" +
				cbLTSTUp.IsChecked.ToString() + ";" +
				cbLTSTDn.IsChecked.ToString() + ";" +
				cbLTTSBUp.IsChecked.ToString() + ";" +
				cbLTTSBDn.IsChecked.ToString() + ";" +
				cbLTTLUp.IsChecked.ToString() + ";" +
				cbLTTLDn.IsChecked.ToString() + ";" +
				cbLTTBUp.IsChecked.ToString() + ";" +
				cbLTTBDn.IsChecked.ToString() + ";" +
				cbMTSTUp.IsChecked.ToString() + ";" +
				cbMTSTDn.IsChecked.ToString() + ";" +
				cbMTTSBUp.IsChecked.ToString() + ";" +
				cbMTTSBDn.IsChecked.ToString() + ";" +
				cbMTTLUp.IsChecked.ToString() + ";" +
				cbMTTLDn.IsChecked.ToString() + ";" +
				cbMTTBUp.IsChecked.ToString() + ";" +
				cbMTTBDn.IsChecked.ToString() + ";" +
				cbSTSTUp.IsChecked.ToString() + ";" +
				cbSTSTDn.IsChecked.ToString() + ";" +
				cbSTTSBUp.IsChecked.ToString() + ";" +
				cbSTTSBDn.IsChecked.ToString() + ";" +
				cbSTTLUp.IsChecked.ToString() + ";" +
				cbSTTLDn.IsChecked.ToString() + ";" +
				cbSTTBUp.IsChecked.ToString() + ";" +
				cbSTTBDn.IsChecked.ToString() + ";" +
				cbLTFTOB.IsChecked.ToString() + ";" +
				cbMTFTOB.IsChecked.ToString() + ";" +
				cbSTFTOB.IsChecked.ToString() + ";" +
				cbLTFTOS.IsChecked.ToString() + ";" +
				cbMTFTOS.IsChecked.ToString() + ";" +
				cbSTFTOS.IsChecked.ToString();
			//  add properties
			;

			_mainView.SetInfo("TradeMangement", info);
		}

		//private void Port1Curve_Checked(object sender, RoutedEventArgs e)
		//{
		//    drawReturnChart();
		//}
		//private void Port2Curve_Checked(object sender, RoutedEventArgs e)
		//{
		//    drawReturnChart();
		//}
		private void BenchmarkCurve_Checked(object sender, RoutedEventArgs e)
		{
			drawReturnChart();
		}

		//private void CurrentDate_Checked(object sender, RoutedEventArgs e)
		//{

		//}
		//private void BeginDate_Checked(object sender, RoutedEventArgs e)
		//{

		//}

		bool _useHedgeCurve = false;

		//private void HedgeCurve_Checked(object sender, RoutedEventArgs e)
		//{
		//    _useHedgeCurve = true;
		//    drawReturnChart();
		//    updateStatistics();
		//}

		//private void HedgeCurve_Unchecked(object sender, RoutedEventArgs e)
		//{
		//    _useHedgeCurve = false;
		//    drawReturnChart();
		//    updateStatistics();
		//}

		//private void UseATMTiming_Checked(object sender, RoutedEventArgs e)
		//{
		//    if (UseTimingGrid != null)
		//    {
		//        UseTimingGrid.Visibility = Visibility.Visible;
		//        MachineLearning.Visibility = Visibility.Visible;
		//        //TimingRectangle.Visibility = Visibility.Visible;
		//        //ThreeRowRectangle.Visibility = Visibility.Collapsed;
		//    }
		//}

		private void UseEndTimeUnchecked(object sender, RoutedEventArgs e)
		{
			UserFactorModelTrainEndDate.Visibility = Visibility.Collapsed;
		}

		private void UseEndTimeChecked(object sender, RoutedEventArgs e)
		{
			UserFactorModelTrainEndDate.Visibility = Visibility.Visible;
		}


		private void Help_MouseEnter(object sender, MouseEventArgs e) // change to &#x3F
		{
			var label = sender as Label;
			label.Tag = label.Foreground;
			//label.Content = "\u2630";
			label.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xcc, 0xff));
		}

		private void Help_MouseLeave(object sender, MouseEventArgs e)  //return to &#x1392
		{
			var label = sender as Label;
			//label.Content = "\u2630";
			label.Foreground = (Brush)label.Tag;
		}



		private void CloseHelp_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			ViewHelpSection.BringIntoView();
			ExportHelp.Visibility = Visibility.Collapsed;
			ViewHelp.Visibility = Visibility.Collapsed;
			OpenIdeas.Visibility = Visibility.Collapsed;
			ExportHelp.Visibility = Visibility.Collapsed;
			ViewHelp.Visibility = Visibility.Collapsed;
			HelpON.Visibility = Visibility.Visible;
			HelpOFF.Visibility = Visibility.Collapsed;
		}

		private void ViewHelp_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			ViewHelpSection.BringIntoView();
			ViewHelp.Visibility = Visibility.Visible;
			OpenIdeas.Visibility = Visibility.Collapsed;
			HelpON.Visibility = Visibility.Collapsed;
			HelpOFF.Visibility = Visibility.Visible;
		}


		private void OpenIdeas_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			OpenIdeasSection.BringIntoView();
			ViewHelp.Visibility = Visibility.Collapsed;
			OpenIdeas.Visibility = Visibility.Visible;
			HelpON.Visibility = Visibility.Collapsed;
			HelpOFF.Visibility = Visibility.Visible;
		}

		private void Help_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			ViewHelp.Visibility = Visibility.Visible;
			OpenIdeas.Visibility = Visibility.Collapsed;
			HelpOFF.Visibility = Visibility.Visible;
			HelpON.Visibility = Visibility.Collapsed;
		}

		private void CompareToHelp_MouseDown(object sender, MouseButtonEventArgs e)
		{
			ExportHelp.Visibility = Visibility.Collapsed;
			ViewHelp.Visibility = Visibility.Collapsed;
		}

		//private void ExportHelp_MouseDown(object sender, MouseButtonEventArgs e)
		//{
		//    //PORTIntro.Visibility = Visibility.Collapsed;
		//    //Step1.Visibility = Visibility.Collapsed;
		//    //Step2.Visibility = Visibility.Collapsed;
		//    //Step3.Visibility = Visibility.Collapsed;
		//    //Step4.Visibility = Visibility.Collapsed;
		//    //Step5.Visibility = Visibility.Collapsed;
		//    //Step6.Visibility = Visibility.Collapsed;
		//    //Step7.Visibility = Visibility.Collapsed;
		//    //PortfolioHelp.Visibility = Visibility.Collapsed;
		//    ///CompareToHelp.Visibility = Visibility.Collapsed;
		//    ExportHelp.Visibility = Visibility.Visible;
		//    //FeatureList.Visibility = Visibility.Collapsed;
		//    //AutoTrainHelp.Visibility = Visibility.Collapsed;
		//    ViewHelp.Visibility = Visibility.Collapsed;
		//}


		//private void button1_Click(object sender, RoutedEventArgs e)
		//{
		//    (sender as Button).ContextMenu.IsEnabled = true;
		//    (sender as Button).ContextMenu.PlacementTarget = (sender as Button);
		//    (sender as Button).ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
		//    (sender as Button).ContextMenu.IsOpen = true;
		//}

		//private void UseATMTiming_Unchecked(object sender, RoutedEventArgs e)
		//{
		//    if (UseTimingGrid != null)
		//    {
		//        UseTimingGrid.Visibility = Visibility.Collapsed;
		//        MachineLearning.Visibility = Visibility.Collapsed;
		//        //ThreeRowRectangle.Visibility = Visibility.Visible;
		//        //TimingRectangle.Visibility = Visibility.Collapsed;
		//    }
		//}

		private void FullyInvest_Checked(object sender, RoutedEventArgs e)
		{

			var model = getModel();
			if (model != null)
			{
				model.SizeRecommendation = SizeRecommendation.FullyInvested;
			}
		}
		private void MaxDollar_Checked(object sender, RoutedEventArgs e)
		{
			var model = getModel();
			if (model != null)
			{
				model.SizeRecommendation = SizeRecommendation.MaxDollar;
			}
		}

		private void ATR_Checked(object sender, RoutedEventArgs e)
		{
			var model = getModel();
			if (model != null)
			{
				model.SizeRecommendation = SizeRecommendation.ATR;
			}
		}

		private void ATRParameter_TextChanged(object sender, TextChangedEventArgs e)
		{
			var tb = sender as TextBox;
			if (tb != null)
			{
				var text = tb.Text;
				var model = getModel();
				if (model != null)
				{
					int amount;
					if (!int.TryParse(text, out amount))
					{
						amount = 0;
					}
					model.ATRPeriod = amount;
				}
			}
		}

		private void MaxPercent_Checked(object sender, RoutedEventArgs e)
		{
			var model = getModel();
			if (model != null)
			{
				model.SizeRecommendation = SizeRecommendation.MaxPercent;
			}
		}
		private void MaxPercentAmt_TextChanged(object sender, TextChangedEventArgs e)
		{
			var tb = sender as TextBox;
			if (tb != null)
			{
				var text = tb.Text;
				var model = getModel();
				if (model != null)
				{
					double amount;
					if (!double.TryParse(text, out amount))
					{
						amount = 0;
					}
					model.MaxPercentAmt = amount;
				}
			}
		}
		private void MaxDollarAmt_TextChanged(object sender, TextChangedEventArgs e)
		{
			var tb = sender as TextBox;
			if (tb != null)
			{
				var text = tb.Text;
				var model = getModel();
				if (model != null)
				{
					double amount;
					if (!double.TryParse(text, out amount))
					{
						amount = 0;
					}
					model.MaxDollarAmt = amount;
				}
			}
		}

		private void UseATM_Checked(object sender, RoutedEventArgs e)
		{
			//if (!_initializing)
			//{
			//    var model = getModel();
			//    if (model != null)
			//    {
			//        ui_to_userFactorModel(_selectedUserFactorModel);
			//        model.Groups[_g].UseATMStrategies = true;
			//        userFactorModel_to_ui(_selectedUserFactorModel);
			//    }
			//}
		}

		private void UseATM_Unchecked(object sender, RoutedEventArgs e)
		{
			//if (!_initializing)
			//{
			//    var model = getModel();
			//    if (model != null)
			//    {
			//        ui_to_userFactorModel(_selectedUserFactorModel);
			//        model.Groups[_g].UseATMStrategies = false;
			//        userFactorModel_to_ui(_selectedUserFactorModel);
			//    }
			//}
		}

		public void OnUseRiskEngineChecked(object sender, RoutedEventArgs e)
		{
			//         var cb = sender as CheckBox;
			//if (cb != null && ConstraintsListView != null)
			//{
			//	if (cb.IsChecked == true)
			//	{
			//		ConstraintsListView.Visibility = Visibility.Visible;
			//	}
			//	else
			//	{
			//		ConstraintsListView.Visibility = Visibility.Hidden;
			//	}
			//}
		}

		private void UseML_Checked(object sender, RoutedEventArgs e)
		{
			if (!_initializing)
			{
				var model = getModel();
				if (model != null && !model.UseML)
				{
					ui_to_userFactorModel(_selectedUserFactorModel);
					userFactorModel_to_ui(_selectedUserFactorModel);
				}
			}
		}

		private void UseML_Unchecked(object sender, RoutedEventArgs e)
		{
			if (!_initializing)
			{
				var model = getModel();
				if (model != null && model.UseML)
				{
					ui_to_userFactorModel(_selectedUserFactorModel);
					userFactorModel_to_ui(_selectedUserFactorModel);
				}
			}
		}

		private void UseRanking_Checked(object sender, RoutedEventArgs e)
		{
			if (!_initializing)
			{
				var model = getModel();
				if (model != null && !model.UseRanking)
				{
					ui_to_userFactorModel(_selectedUserFactorModel);
					userFactorModel_to_ui(_selectedUserFactorModel);
				}
			}
		}

		private void UseRanking_Unchecked(object sender, RoutedEventArgs e)
		{
			if (!_initializing)
			{
				var model = getModel();
				if (model != null && model.UseRanking)
				{
					ui_to_userFactorModel(_selectedUserFactorModel);
					userFactorModel_to_ui(_selectedUserFactorModel);
				}
			}
		}
		private void UseFilters_Checked(object sender, RoutedEventArgs e)
		{
			if (!_initializing)
			{
				var model = getModel();
				if (model != null && !model.UseFilters)
				{
					ui_to_userFactorModel(_selectedUserFactorModel);
					userFactorModel_to_ui(_selectedUserFactorModel);
				}
			}
		}

		private void UseFilters_Unchecked(object sender, RoutedEventArgs e)
		{
			if (!_initializing)
			{
				var model = getModel();
				if (model != null && model.UseFilters)
				{
					ui_to_userFactorModel(_selectedUserFactorModel);
					userFactorModel_to_ui(_selectedUserFactorModel);
				}
			}
		}

		private void Example_MouseDown(object sender, MouseButtonEventArgs e)
		{
			//loadExampleModel();
			//Example.Visibility = Visibility.Collapsed;
		}
	}

	public class PortfolioValue
	{
		public double longValue;
		public double shortValue;
		public double cashValue;
	}
}