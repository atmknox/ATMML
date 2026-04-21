using RiskEngine2;
using RiskEngine3;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;

//using Bloomberglp.TerminalApi;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using static ATMML.PortfolioBuilder;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TaskbarClock;

namespace ATMML
{
	public class Member : Symbol
	{
		public Member(string ticker = "", string description = "", string group = "", string sector = "", int size1 = 0, int size2 = 0, DateTime time1 = new DateTime())
			: base(ticker, description, group, sector, size1, size2, time1)
		{
		}

		public int[] ConditionValues; // indexed by column number
		public int[] ConditionCountUp;
		public int[] ConditionCountDn;

		public int LongTradeState = 0;
		public int ShortTradeState = 0;
	}
	public partial class Timing : ContentControl, INotifyPropertyChanged
	{
		MainView _mainView = null;

		YieldChart _yieldChart = null;
		Chart _chart1 = null;
		Chart _chart2 = null;

		string _nav1 = "SPY US Equity";  //was OEX MT
		string _nav2 = "";
		string _nav3 = "SPY US Equity";
		string _nav4 = "";
		string _nav5 = "";
		string _nav6 = "";

		string _yieldNav1 = "";
		string _yieldNav2 = "";
		string _yieldNav3 = "";

		string _symbol = "SPY US Equity";

		string _interval = "Daily";
		string _interval1 = "Weekly";
		string _interval2 = "Monthly";
		string _interval3 = "Quarterly";

		string _view = "By CONDITION";

		string _activeChartInterval = "1D";

		Portfolio.PortfolioType _type;

		int _memberListSortType = 0;

		int _ago = 0;
		int _agoLT = 0;

		bool _positionTimeAlert = false;
		bool _atmTimeAlert = false;

		bool _adviceView = false;
		bool _pxfvofView = false;
		bool _netView = false;

		Dictionary<string, string> _abstractSignals = new Dictionary<string, string>();

		private Dictionary<string, object> _referenceData = new Dictionary<string, object>();

		List<string> _indexSymbols = new List<string>();

		string _selectedNav1 = "";
		string _selectedNav2 = "";
		string _selectedNav3 = "";
		string _selectedNav4 = "";
		string _selectedNav5 = "";
		string _selectedNav6 = "";

		bool _chartVisible = false;

		BarCache _barCache;
		bool _update1 = false;
		bool _update2 = false;
		bool _update3 = false;
		DispatcherTimer _ourViewTimer = new DispatcherTimer();

		Dictionary<string, Result> _atmResults = new Dictionary<string, Result>();

		Portfolio.PortfolioType _clientPortfolioType = Portfolio.PortfolioType.Index;
		string _clientPortfolioName = "";
		string _clientPortfolioName2 = "";

		int _portfolioRequested = 0;
		bool _requestRelativeIndexes = false;

		bool _addFlash = false;

		Portfolio _portfolio = new Portfolio(18);

		Portfolio.PortfolioType _overlayPortfolioType;
		string _overlayPortfolioName = "";

		Portfolio.PortfolioType _comparePortfolioType;
		string _comparePortfolioName = "";

		object _memberListLock = new object();
		List<Member> _memberList = new List<Member>();
		bool _updateMemberList = false;

		Navigation nav = new Navigation();

		const double _spreadsheetRowHeight = 20;  //27
		Point _spreadsheetMargin = new Point(0, 0);
		double _midMargin = 1;

		int _updateSpreadsheet = 0;
		// Queue of rows that need redrawn after calculateConditions completes.
		// Must be a Queue — NOT a single string — because multiple tickers can
		// finish between two 200ms timer ticks and a single string silently drops all but the last.
		Queue<string> _updateRows = new Queue<string>();
		Dictionary<Tuple<int, int>, Brush> _conditionBrushes = new Dictionary<Tuple<int, int>, Brush>();

		ConditionDialog _conditionDialog;

		ConfirmationDialog _confirmationDialog;

		private Dictionary<string, string> sectorPercents;
		private Dictionary<string, string> industryPercents;
		private Dictionary<string, string> subIndustryPercents;
		private string _activeLabel = "Sector";

		// ── Alert monitor ────────────────────────────────────────────────────────
		private AlertController _alertController;
		// Tracks live Bloomberg connection state — updated in timer_Tick, not in barChanged.
		// barChanged fires for cache hits too so it cannot be used for this purpose.
		private bool _bloombergConnected = false;

		// ── Live portfolio metrics — populated by loadPortfolioInfo() ─────────────
		// Read by alert lambdas. All values are fractions of NAV (0.0–1.0+).
		private double _alertGrossBook = 0;  // (longAmt + shortAmt) / NAV
		private double _alertNetExposure = 0;  // (longAmt - shortAmt) / NAV, signed
		private double _alertMaxPositionWeight = 0;  // largest single |position| / NAV
		private double _alertNetBeta = 0;  // weighted net beta

		// ── Alert thresholds — match to your optimizer constraint values ──────────
		private const double _limitMaxPosition = 0.10;  // 10%  single name cap abs
		private const double _limitGrossBook = 2.00;  // 200% gross book abs
		private const double _limitNetExposure = 0.10;  // 10%  net book abs
		private const double _limitMktNeutral = 0.05;  // 5%   market neutral — dollar net / NAV (early warning tier)
		private double _alertVolImbalance = 0;    // |longVolDollars - shortVolDollars| / totalVolDollars
												  // Mkt Neutral: net dollar exposure / NAV — matches _limitNetExposure since same measure
		private const double _limitNetBeta = 0.10;  // 10%  net dollar book / NAV tolerance (matches Net Exposure limit)
													// Vol Neutral: realized portfolio annualized vol — green when within target range
		private const double _limitVolNeutral = 0.12;  // 12%  max annualized portfolio vol
													   // UCAP: gross capital utilization (grossInvestment/NAV) — green when >= 50% (UT threshold)
		private double _alertUtilization = 0;
		private const double _limitUtilization = 0.50;  // UT = 50% minimum threshold
		private const double _limitPredictedVol = 0.12;  // 12%  max predicted annualised vol

		private double _alertPredictedVol = 0;  // trailing annualised vol (decimal, e.g. 0.12 = 12%)
		private double _alertIntradayDD = 0;   // (currentNAV - openNAV) / openNAV, negative = drawdown
		private double _navAtSessionStart = 0;   // NAV captured on first loadPortfolioInfo call of the day
		private DateTime _navSessionDate = DateTime.MinValue; // date of that capture
		private const double _limitIntradayDD = 0.03; // 3% intraday drawdown limit

		// ── Statistical risk alert fields ─────────────────────────────────────
		private double _alertPortfolioVaR95 = 0;  // daily VaR95 — used internally for MVaR95 ratio
		private double _alertMVaR95Pct = 0;  // worst single-position component VaR as % of portfolio VaR
		private double _alertIdioRiskPct = 1.0;  // idiosyncratic vol as % of total portfolio vol (1.0 = uncomputed/safe)
		private double _alertEqStress5 = 0;  // |portfolio PnL| if market moves ±5%
		private double _alertEqStress10 = 0;  // |portfolio PnL| if market moves ±10%
		private bool _alertDataValid = false;  // true after first successful loadPortfolioInfo

		private const double _limitMVaR95Pct = 0.15;  // 15%  max single-position component VaR as % of portfolio VaR
		private const double _limitIdioRiskMin = 0.70;  // 70%  minimum idiosyncratic risk fraction
		private const double _limitEqStress5 = 0.02;  // 2.0% max portfolio loss if market ±5%
		private const double _limitEqStress10 = 0.035; // 3.5% max portfolio loss if market ±10%
													   // Market vol assumption for idiosyncratic risk split (S&P 500 long-run annualised vol)
		private const double _assumedMarketVol = 0.16;  // 16% — adjust if using a live VIX-based estimate

		// ── Concentration: top-N weight sum fields ────────────────────────────────
		private double _alertTop5LongSum = 0;     // sum of top 5 long weights / NAV
		private double _alertTop5ShortSum = 0;    // sum of top 5 short weights / NAV
		private double _alertTop10LongSum = 0;    // sum of top 10 long weights / NAV
		private double _alertTop10ShortSum = 0;   // sum of top 10 short weights / NAV
		private const double _limitTop5LongSum = 0.40;    // 40%
		private const double _limitTop5ShortSum = 0.35;   // 35%
		private const double _limitTop10LongSum = 0.75;   // 75%
		private const double _limitTop10ShortSum = 0.65;  // 65%

		// ── Liquidity: position size as % of 20-day ADV ──────────────────────────
		private double _alertADV20 = 0;   // gross weight of positions where pos_size > 20% ADV
		private double _alertADV50 = 0;   // gross weight of positions where pos_size > 50% ADV
		private double _alertADV100 = 0;  // gross weight of positions where pos_size > 100% ADV
		private double _alertLiqVaR95 = 0;  // liquidity-adjusted VaR95: daily VaR × √(weighted avg liquidation days)
		private const double _limitADV20 = 0.30;   // max 30% of portfolio in >20% ADV positions
		private const double _limitADV50 = 0.10;   // max 10% of portfolio in >50% ADV positions
		private const double _limitADV100 = 0.00;  // 0% tolerance for >100% ADV positions
		private const double _limitLiqVaR95 = 0.02;  // 2.0% — LVaR at 95% confidence, ~4-day liquidation at 20% participation

		// ── Exposure: market cap tier gross/net ───────────────────────────────────
		// Large cap > $5B, Mid cap $1B-$5B, Small cap $500M-$1B
		// Market cap sourced from Bloomberg CUR_MKT_CAP (millions USD)
		private double _alertLargeCapGross = 0;   // (longLarge$ + shortLarge$) / NAV
		private double _alertLargeCapNet = 0;     // |longLarge$ - shortLarge$| / NAV
		private double _alertMidCapGross = 0;
		private double _alertMidCapNet = 0;
		private double _alertSmallCapGross = 0;
		private double _alertSmallCapNet = 0;
		// Market cap cache: ticker → millions USD via CUR_MKT_CAP reference data
		private Dictionary<string, double> _marketCapCache = new Dictionary<string, double>();
		// Position weights for yellow circle display in getSymbolLabel
		private Dictionary<string, double> _positionWeights = new Dictionary<string, double>();
		private const double _limitSymbolWeight = 0.10;
		private const double _limitLargeCapGross = 1.75;  // 175%
		private const double _limitLargeCapNet = 0.15;    // 15%
		private const double _limitMidCapGross = 1.00;    // 100%
		private const double _limitMidCapNet = 0.15;      // 15%
		private const double _limitSmallCapGross = 0.25;  // 25%
		private const double _limitSmallCapNet = 0.025;   // 2.5%

		// ── Risk Analytics: CVaR95 ────────────────────────────────────────────────
		// CVaR95 (Expected Shortfall) = portfolio_vol × φ(1.645) / 0.05
		// For normal dist: φ(1.645) ≈ 0.10314, so CVaR95 ≈ vol × 2.063 (annualized)
		// Daily CVaR95 = annualCVaR95 / √252
		private double _alertCVaR95 = 0;  // daily CVaR95 as % of NAV
		private const double _limitCVaR95 = 0.015;  // 1.5% daily CVaR95

		// Max Portfolio VaR95: daily VaR95 = 1.645 × annVol / √252
		private const double _limitMaxVaR95 = 0.01;  // 1% daily portfolio VaR95

		public Timing(MainView mainView)
		{
			DataContext = this;

			InitializeComponent();

			if (_adviceView)
			{
				ColDef19.Width = new GridLength(1, GridUnitType.Star);
				ColDef20.Width = new GridLength(1, GridUnitType.Star);
				ColDef21.Width = new GridLength(1, GridUnitType.Star);
				ColDef23.Width = new GridLength(1, GridUnitType.Star);
				ColDef24.Width = new GridLength(1, GridUnitType.Star);
				ColDef25.Width = new GridLength(1, GridUnitType.Star);
			}

			_mainView = mainView;

			initializePxfVof();

			CustomAdvice.Visibility = MainView.EnableAutoTrade ? Visibility.Collapsed : Visibility.Collapsed;

			ExportHistoricalResults.Visibility = MainView.EnablePRTUHistory ? Visibility.Collapsed : Visibility.Collapsed;
			HistoricalStartDate.Visibility = MainView.EnablePRTUHistory ? Visibility.Collapsed : Visibility.Collapsed;
			ExportResults.Visibility = MainView.EnableServerOutput ? Visibility.Collapsed : Visibility.Collapsed;

			setDefaultIntervals();

			showViewItems();

			_nav1 = "CMR";    //was OEX MT
			_ = "";
			_nav4 = "";
			_nav5 = "";

			getInfo();
			Country.Content = getPortfolioName();
			Country1.Content = getPortfolioName();

			var model = MainView.GetModel(_modelName);
			var modelName = (model != null) ? _modelName : "";
			var scenario = (model != null) ? MainView.GetSenarioLabel(model.Scenario) : "";
			ScenarioModel.Content = modelName;
			ScenarioName.Content = scenario;

			// ── Alert monitor setup ──────────────────────────────────────────────
			// Created BEFORE initialize() so _alertController exists when
			// loadPortfolioInfo() runs and SetLabelDisplay calls succeed.
			_alertController = new AlertController(pollSeconds: 30);

			// Wire each check to a live data source.
			_alertController.CheckFlexOne = () => true;  // TODO: FlexOneSession.IsConnected
			_alertController.CheckBloomberg = () => _bloombergConnected;
			_alertController.CheckMktNeutral = () => _alertNetBeta <= _limitNetBeta;
			_alertController.CheckVolNeutral = () => _alertVolImbalance <= _limitVolNeutral;
			_alertController.CheckMaxPosition = () => _alertMaxPositionWeight < _limitMaxPosition;
			_alertController.CheckGrossBook = () => _alertGrossBook <= _limitGrossBook;
			_alertController.CheckNetExposure = () => Math.Abs(_alertNetExposure) <= _limitNetExposure;
			_alertController.CheckIntradayDD = () => _alertIntradayDD >= -_limitIntradayDD;  // negative = drawdown
			_alertController.CheckMaxPredVol = () => _alertPredictedVol <= _limitPredictedVol;
			_alertController.CheckMVaR95 = () => _alertMVaR95Pct <= _limitMVaR95Pct;
			_alertController.CheckIdioRisk = () => !_alertDataValid || _alertIdioRiskPct >= _limitIdioRiskMin;
			_alertController.CheckEqStress5 = () => _alertEqStress5 <= _limitEqStress5;
			_alertController.CheckEqStress10 = () => _alertEqStress10 <= _limitEqStress10;
			// Note: do NOT call _alertController.Start() here.
			// Start() is called from UserControl_Loaded after buttons are registered.

			initialize();
		}

		private void showViewItems()
		{
			if (_view == "By CONDITION")
			{
				TimeIntervals.Visibility = Visibility.Visible;
				LTimeIntervals.Visibility = Visibility.Collapsed;
				RTimeIntervals.Visibility = Visibility.Collapsed;
				//JustOneTitle.Visibility = Visibility.Visible;
				Column4.Visibility = Visibility.Visible;
				Column5.Visibility = Visibility.Visible;
				Column6.Visibility = Visibility.Visible;
				Column7.Visibility = Visibility.Visible;
				Column8.Visibility = Visibility.Visible;
				Column9.Visibility = Visibility.Visible;
				//Column10.Visibility = Visibility.Visible;
				Column11.Visibility = Visibility.Visible;
				Column12.Visibility = Visibility.Visible;
				Column22.Visibility = Visibility.Visible;
				Column23.Visibility = Visibility.Visible;
				Column24.Visibility = Visibility.Visible;
				Column25.Visibility = Visibility.Visible;
			}

			else if (_view == "By CONDITION 2")
			{
				TimeIntervals.Visibility = Visibility.Collapsed;
				LTimeIntervals.Visibility = Visibility.Visible;
				RTimeIntervals.Visibility = Visibility.Visible;
				//JustOneTitle.Visibility = Visibility.Collapsed;
				Column4.Visibility = Visibility.Collapsed;
				Column5.Visibility = Visibility.Collapsed;
				Column6.Visibility = Visibility.Collapsed;
				Column7.Visibility = Visibility.Collapsed;
				Column8.Visibility = Visibility.Collapsed;
				Column9.Visibility = Visibility.Collapsed;
				//Column10.Visibility = Visibility.Collapsed;
				Column11.Visibility = Visibility.Collapsed;
				Column12.Visibility = Visibility.Collapsed;
				Column22.Visibility = Visibility.Collapsed;
				Column23.Visibility = Visibility.Collapsed;
				Column24.Visibility = Visibility.Collapsed;
				Column25.Visibility = Visibility.Collapsed;
			}
		}

		public Timing(MainView mainView, string symbol)
		{
			InitializeComponent();

			_mainView = mainView;

			_nav1 = "CMR";  //was OEX MT
			_nav2 = "";
			_nav4 = "";
			_nav5 = "";

			getInfo();
			Country.Content = getPortfolioName();
			Country1.Content = getPortfolioName();

			setDefaultIntervals();

			_symbol = symbol;
			_view = "By CONDITION";

			initialize();
		}

		public Timing(MainView mainView, string continent, string country, string group, string subgroup, string industry, string symbol, string clientPortfolioName, Portfolio.PortfolioType clientPortfolioType)
		{
			InitializeComponent();

			_mainView = mainView;

			getInfo();
			Country.Content = getPortfolioName();
			Country1.Content = getPortfolioName();

			_nav1 = continent;
			_nav2 = country;
			_nav3 = group;
			_nav4 = subgroup;
			_nav5 = industry;
			_symbol = symbol;
			_clientPortfolioName = clientPortfolioName;
			_clientPortfolioName2 = clientPortfolioName;
			_clientPortfolioType = clientPortfolioType;

			updateOverlayUI();

			setDefaultIntervals();

			initialize();
		}


		private void setDefaultIntervals()
		{
			_interval = "Weekly";

			this.Dispatcher.Invoke(DispatcherPriority.Normal, (Action)delegate () { adjustLabels(); });
		}

		private void getInfo()
		{
			string info = _mainView.GetInfo("Server");
			if (info != null && info.Length > 0)

			{
				string[] fields = info.Split(';');
				int count = fields.Length;
				if (count > 0) _nav1 = fields[0];
				if (count > 1) _nav2 = fields[1];
				if (count > 2) _nav3 = fields[2];
				if (count > 3) _nav4 = fields[3];
				if (count > 4) _nav5 = fields[4];
				if (count > 5) _symbol = fields[5];
				if (count > 6) _clientPortfolioName = fields[6];
				if (count > 7) _view = fields[7];
				if (count > 8) _interval = fields[8];
				if (count > 9) _interval1 = fields[9];
				if (count > 10) _interval2 = fields[10];
				if (count > 11) _clientPortfolioType = (Portfolio.PortfolioType)(int.Parse(fields[11]));
				if (count > 12) _twoCharts = (fields[12].Length == 0) ? true : bool.Parse(fields[12]);
				if (count > 13) _modelName = fields[13];

				// RBAC: if the saved state references a TEST portfolio/model and the user
				// is not an Administrator, clear it so the chart and alerts don't auto-load
				// TEST data on startup. This handles role downgrades — a user who was
				// previously Admin (and saved state pointing at a TEST model) should not
				// continue seeing TEST data after their role changes.
				if (!ATMML.Auth.AuthContext.Current.IsAdmin)
				{
					if (_nav1 == "ML PORTFOLIOS >" && !string.IsNullOrEmpty(_nav2)
						&& !ModelAccessGate.IsLive(_nav2))
					{
						_nav1 = "";
						_nav2 = "";
						_nav3 = "";
						_nav4 = "";
						_nav5 = "";
					}
					if (!string.IsNullOrEmpty(_clientPortfolioName) && !ModelAccessGate.IsLive(_clientPortfolioName))
						_clientPortfolioName = "";
					if (!string.IsNullOrEmpty(_modelName) && !ModelAccessGate.IsLive(_modelName))
						_modelName = "";
				}

				if (_interval1 == "") _interval1 = "Weekly";
				if (_interval == "") _interval = "Daily";


				if (_view != "By CONDITION" || _view != "By TIME FRAME" || _view != "By CONDITION 2") _view = "By CONDITION";

				adjustLabels();
			}

			getTradeMangementSettings();
		}

		private void setInfo()
		{
			string info =
				_nav1 + ";" +
				_nav2 + ";" +
				_nav3 + ";" +
				_nav4 + ";" +
				_nav5 + ";" +
				_symbol + ";" +
				_clientPortfolioName + ";" +
				_view + ";" +
				_interval + ";" +
				_interval1 + ";" +
				_interval2 + ";" +
				((int)_clientPortfolioType).ToString() + ";" +
				_twoCharts.ToString() + ";" +
				_modelName;

			_mainView.SetInfo("Server", info);

			setTradeMangementSettings();
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
						if (count > 1) cbStopLoss.IsChecked = bool.Parse(fields[1]);
						if (count > 2) StopLossPercent.Text = fields[2];
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
						// add properties
					}
				}
			}

			if (useDefaults)
			{
				cbStopLoss.IsChecked = true;
				StopLossPercent.Text = "10";
				cbMTFTUp.IsChecked = true;
				cbMTFTDn.IsChecked = true;
				cbMTSCBuy.IsChecked = true;
				cbMTSCSell.IsChecked = true;
				cbSTSCBuy.IsChecked = true;
				cbSTSCSell.IsChecked = true;
				// add defaults
			}
		}

		private void setTradeMangementSettings()
		{
			int version = 1;

			string info =
				version.ToString() + ";" +
				cbStopLoss.IsChecked.ToString() + ";" +
				StopLossPercent.Text + ";" +
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
				ShortNet3.IsChecked.ToString()
				//  add properties
				;

			_mainView.SetInfo("TradeMangement", info);
		}

		private void initialize()
		{
			ResourceDictionary dictionary = new ResourceDictionary();
			dictionary.Source = new Uri("pack://application:,,,/ATMML;component/StyleDictionary.xaml");
			this.Resources.MergedDictionaries.Add(dictionary);

			initializeCharts();

			_portfolio.PortfolioChanged += new PortfolioChangedEventHandler(portfolioChanged);
			_barCache = new BarCache(barChanged);

			loadPortfolioInfo();

			update(_nav1, _nav2, _nav3, _nav4, _nav5, _nav6);

			_ourViewTimer.Interval = TimeSpan.FromMilliseconds(200);
			_ourViewTimer.Tick += new System.EventHandler(timer_Tick);
			_ourViewTimer.Start();

			_mainView.MainEvent += _mainView_MainEvent;

			showViewItems();

			updateOverlayUI();

			Trade.Manager.TradeEvent += new TradeEventHandler(TradeEvent);

			_calcThread = new Thread(calc);
			_calcThread.Start();

			if (!BarServer.ConnectedToBloomberg())
			{
				GlobalMacButton.Visibility = Visibility.Visible;
			}
		}

		void TradeEvent(object sender, TradeEventArgs e)
		{
			if (_memberList.Count == 0 && e.Type == TradeEventType.PRTU_Loading_Complete)
			{
				var name = getPortfolioName();
				var type = getPortfolioType(name, _clientPortfolioName);
				//Trace.WriteLine("request symbols " + name + " " + type);
				requestPortfolio(name, type);
			}
		}

		private void _mainView_MainEvent(object sender, MainEventArgs e)

		{
			drawOrderList();
		}

		bool _yieldPortfolio = true;
		bool _yieldSpreadPortfolio = true;
		bool _twoCharts = true;

		private bool displayYieldChart
		{
			get
			{
				return false; //_yieldPortfolio
			}
		}

		private void showCharts()
		{
			ChartGrid.Visibility = Visibility.Visible;
			Grid.SetRowSpan(ConditionsGrid, 1);

			if (displayYieldChart)
			{
				ChartGridCol1.Width = new GridLength(34, GridUnitType.Star);
				ChartGridCol2.Width = new GridLength(33, GridUnitType.Star);
				ChartGridCol3.Width = new GridLength(33, GridUnitType.Star);
			}
			else
			{
				ChartGridCol1.Width = new GridLength(0, GridUnitType.Star);
				ChartGridCol2.Width = new GridLength(50, GridUnitType.Star);
				ChartGridCol3.Width = new GridLength(50, GridUnitType.Star);
			}

			if (!_twoCharts)
			{
				Grid.SetColumn(ChartCanvasBorder2, 1);
				Grid.SetColumnSpan(ChartCanvasBorder2, 2);

				if (_chart2 != null)
				{
					// _chart1.SpreadSymbols = true;
					_chart2.HasTitleIntervals = true;
					_chart2.Show();
				}

				if (_chart1 != null)
				{
					ChartCanvasBorder1.Visibility = Visibility.Collapsed;
					_chart1.Hide();
				}
			}

			else if (_yieldPortfolio)
			{
				Grid.SetColumnSpan(ChartCanvasBorder1, 1);
				if (_chart1 != null)
				{
					_chart1.SpreadSymbols = false;
					_chart1.HasTitleIntervals = true;
					ChartCanvasBorder1.Visibility = Visibility.Visible;
					_chart1.Show();
				}

				if (_chart2 != null)
				{
					ChartCanvasBorder2.Visibility = Visibility.Visible;
					_chart2.SpreadSymbols = false;
					_chart2.HasTitleIntervals = true;
					_chart2.Show();

				}
			}

			else
			{
				Grid.SetColumn(ChartCanvasBorder2, 2);
				Grid.SetColumnSpan(ChartCanvasBorder2, 1);

				if (_chart1 != null)
				{
					_chart1.SpreadSymbols = true;
					_chart1.HasTitleIntervals = true;
					ChartCanvasBorder1.Visibility = Visibility.Visible;
					_chart1.Show();
				}

				if (_chart2 != null)
				{
					ChartCanvasBorder2.Visibility = Visibility.Visible;
					_chart2.HasTitleIntervals = true;
					_chart2.Show();
				}
			}

			setHeights();
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

		string _portfolioModelName = "";
		Model _portfolioModel = null;
		Model getModel(string name)
		{
			if (name != _portfolioModelName)
			{
				_portfolioModelName = name;
				var path = @"models\Models\" + name;
				var data = MainView.LoadUserData(path);
				_portfolioModel = Model.load(data);
			}
			return _portfolioModel;
		}

		private double getPortfolioBalance(DateTime time)
		{
			var portfolioName = getPortfolioName();
			var portfolioBalance = 0.0;
			Model model = getModel(portfolioName);
			if (model != null)
			{
				var portfolioTimes = loadList<DateTime>(portfolioName + " PortfolioTimes");
				var portfolioValues = loadList<double>(portfolioName + " PortfolioValues");
				// Use .Date comparison — saved times may carry a time component
				var idx = portfolioTimes.FindIndex(x => x.Date == time.Date);
				if (idx == -1)
					idx = portfolioTimes.FindLastIndex(x => x.Date <= time.Date);

				if (idx != -1 && idx < portfolioValues.Count)
				{
					portfolioBalance = model.InitialPortfolioBalance * (1 + portfolioValues[idx] / 100);
				}
			}
			return portfolioBalance;
		}

		/// <summary>
		/// Beta at last rebalance — uses entry price × shares at rebalance date / NAV.
		/// Near zero post-rebalance by construction. Comparable to backtest historical log.
		/// </summary>
		private double getRebalanceBeta(DateTime rebalTime, double nav)
		{
			if (nav <= 0 || rebalTime == default) return double.NaN;
			var portfolioName = getPortfolioName();
			var portfolioBeta = 0.0;
			Model model = getModel(portfolioName);
			if (model != null)
			{
				var portfolio = _portfolio.GetTrades(model.Name, "", rebalTime);
				var symbolLookup = model.Symbols != null
					? model.Symbols.GroupBy(s => s.Ticker, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase)
					: new Dictionary<string, Symbol>(StringComparer.OrdinalIgnoreCase);
				foreach (var trade in portfolio)
				{
					var ticker = trade.Ticker;
					symbolLookup.TryGetValue(ticker, out var symbol);
					if (symbol == null) continue;
					var key = rebalTime.ToString("yyyy-MM-dd");
					// Use Volatility30D — matches AdjustForVolatilityNeutrality in the optimizer.
					double vol = 1.0;
					if (symbol.Volatility30D != null && symbol.Volatility30D.Count > 0)
					{
						vol = symbol.Volatility30D.ContainsKey(key) ? symbol.Volatility30D[key] : symbol.Volatility30D.Last().Value;
						vol /= 100.0; // Volatility30D stored as % — convert to decimal
					}
					var bars = _barCache.GetBars(ticker, "D", 0, BarServer.MaxBarCount);
					// Use .Date comparison — keys carry a time component (3 PM MOC) so exact match fails.
					var _apkR = trade.AvgPrice.Keys.Where(k => k.Date == rebalTime.Date).OrderByDescending(k => k).FirstOrDefault();
					double entryPrice = _apkR != default ? trade.AvgPrice[_apkR] : getPrice(bars, trade.OpenDateTime);
					var _shkR = trade.Shares.Keys.Where(k => k.Date == rebalTime.Date && trade.Shares[k] != 0).OrderByDescending(k => k).FirstOrDefault();
					if (_shkR == default(DateTime))
						_shkR = trade.Shares.Keys.Where(k => k.Date <= rebalTime.Date && trade.Shares[k] != 0).OrderByDescending(k => k).FirstOrDefault();
					double sharesAtRebal = Math.Abs(_shkR != default ? trade.Shares[_shkR] : 0);
					double dollarSize = entryPrice * sharesAtRebal;
					var value = vol * dollarSize / nav;
					portfolioBeta += (trade.Direction > 0) ? value : -value;
				}
			}
			return portfolioBeta;
		}

		private double getPortfolioBeta(DateTime time, double nav = 0.0)
		{
			var portfolioName = getPortfolioName();
			var portfolioBeta = 0.0;
			Model model = getModel(portfolioName);
			if (model != null)
			{
				var portfolio = (time == default(DateTime)) ? new List<Trade>() : _portfolio.GetTrades(model.Name, "", time);
				Dictionary<string, double> shares = new Dictionary<string, double>();
				Dictionary<string, double> price = new Dictionary<string, double>();
				var totalInvestment = 0.0;
				foreach (var trade in portfolio)
				{
					string ticker = trade.Ticker;
					var bars = _barCache.GetBars(ticker, "D", 0, BarServer.MaxBarCount);
					var entryTime = trade.OpenDateTime;
					// Use .Date comparison — keys carry a time component (3 PM MOC) so exact match fails.
					var _apKey2 = trade.AvgPrice.Keys.Where(k => k.Date == time.Date).OrderByDescending(k => k).FirstOrDefault();
					double price1 = _apKey2 != default ? trade.AvgPrice[_apKey2] : getPrice(bars, entryTime);
					var _shKey2 = trade.Shares.Keys.Where(k => k.Date == time.Date && trade.Shares[k] != 0).OrderByDescending(k => k).FirstOrDefault();
					if (_shKey2 == default(DateTime))
						_shKey2 = trade.Shares.Keys.Where(k => k.Date <= time.Date && trade.Shares[k] != 0).OrderByDescending(k => k).FirstOrDefault();
					var shares1 = shares[ticker] = Math.Abs(_shKey2 != default ? trade.Shares[_shKey2] : 0); // gross — always positive
					price[ticker] = price1;
					shares[ticker] = shares1;
					totalInvestment += price1 * shares1;
				}

				if (totalInvestment <= 0) return 0.0;

				// Use NAV as denominator when provided (industry standard for long/short funds).
				// Falls back to gross totalInvestment if NAV is unavailable.
				var denominator = (nav > 0) ? nav : totalInvestment;
				var symbolLookup = model.Symbols != null
					? model.Symbols.GroupBy(s => s.Ticker, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase)
					: new Dictionary<string, Symbol>(StringComparer.OrdinalIgnoreCase);

				foreach (var trade in portfolio)
				{
					string ticker = trade.Ticker;
					// Volatility-neutral metric — matches optimizer's AdjustForVolatilityNeutrality.
					var vol = 1.0;
					symbolLookup.TryGetValue(ticker, out var symbol);
					if (symbol != null && symbol.Volatility30D != null && symbol.Volatility30D.Count > 0)
					{
						var key = time.ToString("yyyy-MM-dd");
						vol = symbol.Volatility30D.ContainsKey(key) ? symbol.Volatility30D[key] : symbol.Volatility30D.Last().Value;
						vol /= 100.0; // Volatility30D stored as % (e.g. 25.0) — convert to decimal (0.25)
					}
					int side = trade.Direction;
					var amount = price[ticker] * shares[ticker]; // shares already abs
					var value = vol * amount / denominator;
					portfolioBeta += (side > 0) ? value : -value;
				}
			}
			return portfolioBeta;
		}

		private double getAnnFactor(List<DateTime> input)
		{
			var annFactor = double.NaN;
			if (input.Count >= 2)
			{
				var startTime = input.First();
				var endTime = input.Last();
				var duration = endTime - startTime;
				var yearDur = duration.TotalDays / 365.25;
				// Guard against near-zero duration (e.g. live mode with only 1-2 observations)
				// Fall back to 52 (weekly) as a safe default annualisation factor
				annFactor = yearDur > 0.1 ? input.Count / yearDur : 52.0;
			}
			else if (input.Count == 1)
			{
				annFactor = 52.0; // weekly default
			}
			return annFactor;
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

		private Dictionary<DateTime, double> getAnnVol(List<DateTime> portfolioTimes, List<double> portfolioReturns)
		{
			var output = new Dictionary<DateTime, double>();

			// Need at least 4 observations for a meaningful vol estimate
			if (portfolioTimes.Count < 4 || portfolioReturns.Count < 4)
				return output;

			var annFactor = getAnnFactor(portfolioTimes);
			if (double.IsNaN(annFactor) || double.IsInfinity(annFactor))
				return output;

			for (int ii = 0; ii < portfolioTimes.Count; ii++)
			{
				var stdReturns = getStandardDeviation(portfolioReturns);
				var AnnVol = 100 * Math.Sqrt(annFactor) * stdReturns;
				output[portfolioTimes[ii]] = AnnVol;
			}
			return output;
		}


		void loadPortfolioInfo()
		{
			var portfolioName = getPortfolioName();

			var model = getModel(portfolioName);
			if (model == null) return;

			var portfolioTimes = loadList<DateTime>(portfolioName + " PortfolioTimes");
			var portfolioValues = loadList<double>(portfolioName + " PortfolioValues");
			var portfolioReturns = loadList<double>(portfolioName + " PortfolioReturns");
			var time2 = portfolioTimes.LastOrDefault();
			// Filter to only currently open positions at time2 -- excludes proposed new orders
			var trades = time2 != default(DateTime)
				? _portfolio.GetTrades(portfolioName, "", time2)
				: _portfolio.GetTrades(portfolioName);

			// Subscribe daily bars for all portfolio tickers so barCache stays current.
			// Also subscribe Weekly for intraday price updates during market hours.
			foreach (var t in trades.Select(tr => tr.Ticker).Distinct())
			{
				_barCache.RequestBars(t, "D", true, 10);
				_barCache.RequestBars(t, "Weekly", true, 5);
			}

			// Compute portfolioBalance at time2.
			// For live portfolios: portfolioValues entries are zero (lockedNav is empty).
			// Use LiveNav_ if available, otherwise fall back to InitialPortfolioBalance.
			// The sector/concentration % denominator must be NAV, not gross book.
			var portfolioBalance = 0.0;
			if (model != null && portfolioValues.Count > 0)
			{
				var idx = portfolioTimes.FindIndex(x => x.Date == time2.Date);
				if (idx == -1) idx = portfolioTimes.FindLastIndex(x => x.Date <= time2.Date);
				if (idx == -1 || idx >= portfolioValues.Count) idx = portfolioValues.Count - 1;
				var fromDisk = model.InitialPortfolioBalance * (1 + portfolioValues[idx] / 100);
				if (fromDisk > 0) portfolioBalance = fromDisk;
			}
			// LiveNav_ override: Hedge Fund App computes this from real-time prices each tick
			var liveNavStr = _mainView.GetInfo("LiveNav_" + (_portfolioModelName ?? getPortfolioName()));
			bool isLiveDate = time2 == default(DateTime) || time2.Date >= DateTime.Today.AddDays(-7);
			if (isLiveDate && !string.IsNullOrEmpty(liveNavStr) && double.TryParse(liveNavStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double liveNavEarly) && liveNavEarly > 0)
				portfolioBalance = liveNavEarly;
			// Final fallback: use InitialPortfolioBalance so denominators are never near-zero
			if (portfolioBalance <= 0 && model != null)
				portfolioBalance = model.InitialPortfolioBalance;

			var totalInvestment = 0.0;
			Dictionary<string, double> returns = new Dictionary<string, double>();
			Dictionary<string, double> shares = new Dictionary<string, double>();
			Dictionary<string, double> price = new Dictionary<string, double>();
			Dictionary<string, double> volatility = new Dictionary<string, double>(); // for per-position VaR95 alert
			Dictionary<string, double> betaForAlert = new Dictionary<string, double>(); // per-stock beta for eq stress
			Dictionary<string, double> currentPrice = new Dictionary<string, double>(); // current market price for alert market value
			var sectorInvestments = new Dictionary<string, double>();
			var industryInvestments = new Dictionary<string, double>();
			var subIndustryInvestments = new Dictionary<string, double>();
			// Separate signed-net dictionaries for alert monitoring (longs +, shorts -)
			var sectorNetAmounts = new Dictionary<string, double>();
			var industryNetAmounts = new Dictionary<string, double>();
			var subIndNetAmounts = new Dictionary<string, double>();
			var longAmount = 0.0;
			var shortAmount = 0.0;

			// Build lookup dict once — avoids O(810) scan per trade per cycle
			var symbolLookup = model.Symbols != null
				? model.Symbols.GroupBy(s => s.Ticker, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase)
				: new Dictionary<string, Symbol>(StringComparer.OrdinalIgnoreCase);

			try
			{
				foreach (var trade in trades)
				{
					int side = (int)trade.Direction;
					string ticker = trade.Ticker;
					bool useYield = atm.isYieldTicker(ticker);

					var bars = _barCache.GetBars(ticker, "D", 0, BarServer.MaxBarCount);


					var entryTime = trade.OpenDateTime;
					var exitTime = trade.CloseDateTime;

					// Use .Date matching throughout — bar dates from Bloomberg may carry a time
					// component that prevents exact DateTime equality with saved portfolioTimes entries.
					var avgPriceKey = trade.AvgPrice.Keys.FirstOrDefault(k => k.Date == time2.Date);
					var closesKey = trade.Closes.Keys.FirstOrDefault(k => k.Date == time2.Date);
					var sharesKey = trade.Shares.Keys.FirstOrDefault(k => k.Date == time2.Date);

					double returnValue = double.NaN;
					double price1 = avgPriceKey != default ? trade.AvgPrice[avgPriceKey] : getPrice(bars, entryTime);
					double price2 = closesKey != default ? trade.Closes[closesKey] : getPrice(bars, time2);
					if (!double.IsNaN(price1) && !double.IsNaN(price2) && (trade.IsOpen() || exitTime == default || time2 < exitTime))
					{
						returns[ticker] = 100 * Math.Sign(side) * (price2 - price1) / price1;
					}
					else
					{
						returns[ticker] = 0;
					}
					price[ticker] = price1;
					currentPrice[ticker] = double.IsNaN(price2) ? price1 : price2;  // current market price
					shares[ticker] = sharesKey != default ? trade.Shares[sharesKey] : 0;

					// Calculate dollar amount for this position
					// shares[ticker] is already signed (+ for long, - for short)

					var numberOfShares = Math.Abs(shares[ticker]);
					var dollarAmount = numberOfShares == 0 ? 0 : price[ticker] * numberOfShares;

					// Accumulate to correct side based on direction
					if (side > 0)
					{
						longAmount += dollarAmount;  // Long position (amount is positive)
					}
					else if (side < 0)
					{
						shortAmount += dollarAmount;  // Short position (amount is positive)
					}

					var name = "";
					int sectorNumber;
					int industryNumber;
					int subIndustryNumber;
					symbolLookup.TryGetValue(ticker, out var symbol);
					if (symbol != null)
					{
						int.TryParse(symbol.Sector, out sectorNumber);
						name = _portfolio.GetSectorLabel(sectorNumber);
						var amount = (side > 0 ? 1 : -1) * price[ticker] * shares[ticker];  // signed entry price, matches Portfolio_Builder
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
						// Signed net: long = positive, short = negative — use current market price
						var curPxSec = currentPrice.ContainsKey(ticker) ? currentPrice[ticker] : price[ticker];
						var signedAmount = side > 0
							? curPxSec * Math.Abs(shares[ticker])
							: -curPxSec * Math.Abs(shares[ticker]);
						if (!double.IsNaN(signedAmount))
						{
							if (sectorNetAmounts.ContainsKey(name)) sectorNetAmounts[name] += signedAmount;
							else sectorNetAmounts[name] = signedAmount;
						}
					}
					if (symbol != null)
					{
						int.TryParse(symbol.Industry, out industryNumber);
						name = _portfolio.GetIndustryLabel(industryNumber);
						var amountInd = (side > 0 ? 1 : -1) * price[ticker] * shares[ticker];  // signed entry price
						if (!double.IsNaN(amountInd))
						{
							if (industryInvestments.ContainsKey(name))
							{
								industryInvestments[name] += amountInd;
							}
							else
							{
								industryInvestments[name] = amountInd;
							}
						}
						// Signed net: use current market price
						var curPxInd = currentPrice.ContainsKey(ticker) ? currentPrice[ticker] : price[ticker];
						var signedAmtInd = side > 0
							? curPxInd * Math.Abs(shares[ticker])
							: -curPxInd * Math.Abs(shares[ticker]);
						if (!double.IsNaN(signedAmtInd))
						{
							if (industryNetAmounts.ContainsKey(name)) industryNetAmounts[name] += signedAmtInd;
							else industryNetAmounts[name] = signedAmtInd;
						}
					}
					if (symbol != null)
					{
						int.TryParse(symbol.SubIndustry, out subIndustryNumber);
						name = _portfolio.GetSubIndustryLabel(subIndustryNumber);
						var amountSub = (side > 0 ? 1 : -1) * price[ticker] * shares[ticker];  // signed entry price
						if (!double.IsNaN(amountSub))
						{
							if (subIndustryInvestments.ContainsKey(name))
							{
								subIndustryInvestments[name] += amountSub;
							}
							else
							{
								subIndustryInvestments[name] = amountSub;
							}
						}
						// Signed net: use current market price
						var curPxSub = currentPrice.ContainsKey(ticker) ? currentPrice[ticker] : price[ticker];
						var signedAmtSub = side > 0
							? curPxSub * Math.Abs(shares[ticker])
							: -curPxSub * Math.Abs(shares[ticker]);
						if (!double.IsNaN(signedAmtSub))
						{
							if (subIndNetAmounts.ContainsKey(name)) subIndNetAmounts[name] += signedAmtSub;
							else subIndNetAmounts[name] = signedAmtSub;
						}
					}
					// Capture beta for equity stress test
					if (symbol != null && symbol.Beta != null && symbol.Beta.Count > 0)
					{
						var betaKey = time2.ToString("yyyy-MM-dd");
						betaForAlert[ticker] = symbol.Beta.ContainsKey(betaKey)
							? symbol.Beta[betaKey]
							: symbol.Beta.Last().Value;
					}
					// Capture Volatility30D for per-position VaR95 alert — same pattern as Beta
					if (symbol != null && symbol.Volatility30D != null && symbol.Volatility30D.Count > 0)
					{
						var key = time2.ToString("yyyy-MM-dd");
						double vol30 = symbol.Volatility30D.ContainsKey(key)
							? symbol.Volatility30D[key]
							: symbol.Volatility30D.Last().Value;
						// Volatility30D is annualised % (e.g. 25.0 = 25%) — convert to decimal
						volatility[ticker] = vol30 / 100.0;
					}
				}

				// Balance2 reflects the live MtM NAV set at top of this method
				Balance2.Content = "$ " + portfolioBalance.ToString("#,##0");

				var percents = _portfolio.initPercents();
				sectorPercents = percents[0];
				industryPercents = percents[1];
				subIndustryPercents = percents[2];

				// For live portfolios, use sector percents published by Hedge Fund App
				// (calculated at rebalance prices) instead of recalculating with current
				// market prices which causes apparent drift above the constraint limit.
				var pbSectorStr = _mainView?.GetInfo("SectorPercents");
				var pbIndustryStr = _mainView?.GetInfo("IndustryPercents");
				var pbSubIndStr = _mainView?.GetInfo("SubIndustryPercents");
				if (!string.IsNullOrEmpty(pbSectorStr))
				{
					deserializePercents(pbSectorStr, sectorPercents);
					deserializePercents(pbIndustryStr, industryPercents);
					deserializePercents(pbSubIndStr, subIndustryPercents);
				}
				else if (totalInvestment != 0 && portfolioBalance > 0)
				{
					sectorPercents.Keys.ToList().ForEach(k => sectorPercents[k] = sectorInvestments.ContainsKey(k) && !double.IsNaN(sectorInvestments[k]) ? Math.Abs(sectorInvestments[k] / portfolioBalance).ToString(".00%") : "");
					industryPercents.Keys.ToList().ForEach(k => industryPercents[k] = industryInvestments.ContainsKey(k) && !double.IsNaN(industryInvestments[k]) ? Math.Abs(industryInvestments[k] / portfolioBalance).ToString(".00%") : "");
					subIndustryPercents.Keys.ToList().ForEach(k => subIndustryPercents[k] = subIndustryInvestments.ContainsKey(k) && !double.IsNaN(subIndustryInvestments[k]) ? Math.Abs(subIndustryInvestments[k] / portfolioBalance).ToString(".00%") : "");
				}

				// R = beta at last SETTLED rebalance date (optimizer enforced neutrality — near zero).
				// L = live beta using today's prices (shows intra-week drift from rebalance).
				// portfolioTimes may have one more entry than portfolioValues (live placeholder),
				// so the last SETTLED rebalance is index (portfolioValues.Count - 1).
				// portfolioTimes may contain a live placeholder (default DateTime) at the end
				// Find the last settled (non-default) entry
				var _settledIdx = portfolioValues.Count - 1;
				var _rebalTime = portfolioTimes
					.Where(t => t != default(DateTime))
					.OrderByDescending(t => t)
					.FirstOrDefault();
				var _rebalNav = (_settledIdx >= 0 && model != null)
					? model.InitialPortfolioBalance * (1 + portfolioValues[_settledIdx] / 100)
					: 0.0;
				// portfolioBalance is already updated to live MtM nav (from _mainView.GetInfo("LiveNav"))
				// by the time we reach this point, so use it directly for both calculations.
				var rebalanceBeta = getRebalanceBeta(_rebalTime, _rebalNav);
				var portfolioBeta = getPortfolioBeta(time2, portfolioBalance);

				// Show both: "R:0.01  L:0.42" — R=rebalance (optimizer output), L=live (intra-week drift)
				var rebalStr = $"R:{rebalanceBeta:0.00}";
				var liveStr = $"L:{portfolioBeta:0.00}";
				BetaValue.Content = $"{rebalStr}  {liveStr}";

				var portfolioVol = getAnnVol(portfolioTimes, portfolioReturns);
				double annVol = double.NaN;
				if (portfolioVol.ContainsKey(time2))
				{
					annVol = portfolioVol[time2];
					PortfolioVOL.Content = double.IsNaN(annVol) ? "" : $"{annVol:0.00}";
				}

				var grossInvestment = (longAmount + shortAmount) / portfolioBalance;
				var netExposure = (longAmount - shortAmount) / portfolioBalance;
				var longExposure = longAmount / portfolioBalance;
				var shortExposure = shortAmount / portfolioBalance;

				GrossInvestment.Content = grossInvestment.ToString("P2", CultureInfo.InvariantCulture); // → "12.34%"
				NetExposure.Content = netExposure.ToString("P2", CultureInfo.InvariantCulture); // → "12.34%"
				TotalLongs.Content = longExposure.ToString("P2", CultureInfo.InvariantCulture); // → "12.34%"
				TotalShorts.Content = shortExposure.ToString("P2", CultureInfo.InvariantCulture); // → "12.34%"


				// ── Stamp portfolio metrics for alert monitoring ─────────────────────
				_alertGrossBook = portfolioBalance > 0 ? grossInvestment : 0;
				_alertUtilization = portfolioBalance > 0 ? grossInvestment : 0;  // UCAP = gross/NAV, green when >= UT (50%)
				_alertNetExposure = portfolioBalance > 0 ? netExposure : 0;
				// Alert uses beta-vol imbalance (computed below after per-position loop)
				// _alertNetBeta is set after the trade loop

				// Intraday drawdown: capture NAV on first call of each day, compare on every subsequent call
				if (_navSessionDate.Date != DateTime.Today || _navAtSessionStart == 0)
				{
					_navAtSessionStart = portfolioBalance;
					_navSessionDate = DateTime.Today;
				}
				_alertIntradayDD = _navAtSessionStart > 0
					? (portfolioBalance - _navAtSessionStart) / _navAtSessionStart
					: 0;

				double longBetaVolDollars = 0;  // Σ(long:  beta × vol × market_value)
				double shortBetaVolDollars = 0;  // Σ(short: beta × vol × market_value)
				double longBetaDollars = 0;     // Σ(long:  beta × market_value) — for equity stress
				double shortBetaDollars = 0;    // Σ(short: beta × market_value) — for equity stress
				double totalVolDollars = 0;  // Σ(all:   vol × market_value) — normalizer
				double longVolDollars = 0;  // Σ(long:  vol × market_value) for vol neutrality
				double shortVolDollars = 0;  // Σ(short: vol × market_value) for vol neutrality
				double maxPosWeight = 0;
				double maxPosVaR95 = 0;  // for MVaR95 ratio

				// Top-N concentration accumulators
				var longWeights = new List<double>();
				var shortWeights = new List<double>();

				// Liquidity accumulators (position size as % of 20-day ADV)
				double adv20DollarSum = 0, adv50DollarSum = 0, adv100DollarSum = 0;
				double liqDaysDollarWeighted = 0; // Σ(days_to_liquidate_i × posDollar_i) for dollar-weighted avg
				double liqDollarTotal = 0;        // Σ(posDollar_i) — denominator for dollar weighting

				// Market cap tier accumulators ($ values, signed by direction)
				double largeLong = 0, largeShort = 0;
				double midLong = 0, midShort = 0;
				double smallLong = 0, smallShort = 0;
				foreach (var trade in trades)
				{
					string t = trade.Ticker;
					if (price.ContainsKey(t) && shares.ContainsKey(t) && portfolioBalance > 0)
					{
						double curPx = currentPrice.ContainsKey(t) ? currentPrice[t] : price[t];
						double w = Math.Abs(curPx * shares[t]) / portfolioBalance;
						lock (_positionWeights) { _positionWeights[t] = w; }  // for yellow circle in getSymbolLabel
						if (w > maxPosWeight) maxPosWeight = w;
						int dir = (int)trade.Direction;
						double posDollar = Math.Abs(curPx * shares[t]);

						// Top-N concentration
						if (dir > 0) longWeights.Add(w);
						else if (dir < 0) shortWeights.Add(w);

						// Liquidity: position shares vs 20-day average daily volume
						var dailyBarsLiq = _barCache.GetBars(t, "Daily", 0, 25);
						if (dailyBarsLiq != null && dailyBarsLiq.Count >= 5)
						{
							double avgVol = dailyBarsLiq.Skip(Math.Max(0, dailyBarsLiq.Count - 20))
								.Where(b => b.Volume > 0).Select(b => b.Volume).DefaultIfEmpty(0).Average();
							if (avgVol > 0)
							{
								double advPct = Math.Abs(shares[t]) / avgVol;
								if (advPct > 0.20) adv20DollarSum += posDollar;
								if (advPct > 0.50) adv50DollarSum += posDollar;
								if (advPct > 1.00) adv100DollarSum += posDollar;
								// Liquidation days at 20% participation: days = advPct / 0.20.
								// Cap at 20 to prevent a single outlier from dominating the weighted avg.
								double liqDays = Math.Min(20.0, advPct / 0.20);
								liqDaysDollarWeighted += liqDays * posDollar;
								liqDollarTotal += posDollar;
							}
						}

						// Market cap tier: Bloomberg CUR_MKT_CAP in millions
						var sym = getSymbol(t);
						if (sym != null)
						{
							// Market cap tier: primary from CUR_MKT_CAP reference data (millions USD)
							double mktCapM = double.NaN;
							lock (_marketCapCache)
							{
								if (_marketCapCache.TryGetValue(t, out double cached)) mktCapM = cached;
							}
							if (!double.IsNaN(mktCapM))
							{
								double mktCapB = mktCapM / 1000.0; // millions → billions
								if (mktCapB > 5.0) { if (dir > 0) largeLong += posDollar; else largeShort += posDollar; }
								else if (mktCapB > 1.0) { if (dir > 0) midLong += posDollar; else midShort += posDollar; }
								else if (mktCapB > 0.5) { if (dir > 0) smallLong += posDollar; else smallShort += posDollar; }
							}
						}

						// Per-position VaR95 = 1.645 × annualised vol × position weight
						if (volatility.ContainsKey(t))
						{
							double posVaR95 = 1.645 * volatility[t] * w;
							if (posVaR95 > maxPosVaR95) maxPosVaR95 = posVaR95;

							// Beta-vol accumulation: beta × vol × marketValue
							// For vol-neutral strategy: cancels when long/short have identical beta×vol
							if (betaForAlert.ContainsKey(t) && volatility.ContainsKey(t))
							{
								double posMarketVal = curPx * Math.Abs(shares[t]);
								double betaVolDol = betaForAlert[t] * volatility[t] * posMarketVal;
								totalVolDollars += volatility[t] * posMarketVal;
								// dir declared above
								double betaDol = betaForAlert[t] * posMarketVal;
								if (dir > 0) { longBetaVolDollars += betaVolDol; longVolDollars += volatility[t] * posMarketVal; longBetaDollars += betaDol; }
								else if (dir < 0) { shortBetaVolDollars += betaVolDol; shortVolDollars += volatility[t] * posMarketVal; shortBetaDollars += betaDol; }
							}
						}
					}
				}
				_alertMaxPositionWeight = maxPosWeight;
				// Mkt Neutral: reuse _alertNetExposure (net dollar / NAV) already computed above
				_alertNetBeta = Math.Abs(_alertNetExposure);
				// Vol Neutral assigned below after _alertPredictedVol is computed

				// ── Top-N concentration ───────────────────────────────────────────
				longWeights.RemoveAll(w => w <= 0);   // exclude exits / zero-share positions
				shortWeights.RemoveAll(w => w <= 0);
				longWeights.Sort((a, b) => b.CompareTo(a));   // descending
				shortWeights.Sort((a, b) => b.CompareTo(a));
				_alertTop5LongSum = longWeights.Take(5).Sum();
				_alertTop5ShortSum = shortWeights.Take(5).Sum();
				_alertTop10LongSum = longWeights.Take(10).Sum();
				_alertTop10ShortSum = shortWeights.Take(10).Sum();

				// ── Liquidity ─────────────────────────────────────────────────────
				_alertADV20 = portfolioBalance > 0 ? adv20DollarSum / portfolioBalance : 0;
				_alertADV50 = portfolioBalance > 0 ? adv50DollarSum / portfolioBalance : 0;
				_alertADV100 = portfolioBalance > 0 ? adv100DollarSum / portfolioBalance : 0;

				// ── Stock Loan (placeholder — wire Bloomberg borrow rate field) ───

				// ── Market cap tier exposure ──────────────────────────────────────
				if (portfolioBalance > 0)
				{
					_alertLargeCapGross = (largeLong + largeShort) / portfolioBalance;
					_alertLargeCapNet = Math.Abs(largeLong - largeShort) / portfolioBalance;
					_alertMidCapGross = (midLong + midShort) / portfolioBalance;
					_alertMidCapNet = Math.Abs(midLong - midShort) / portfolioBalance;
					_alertSmallCapGross = (smallLong + smallShort) / portfolioBalance;
					_alertSmallCapNet = Math.Abs(smallLong - smallShort) / portfolioBalance;
				}

				// MVaR95: worst single position daily VaR95 as % of NAV
				// posVaR95 = 1.645 * vol(decimal) * weight(decimal) — already % of NAV in decimal form
				_alertMVaR95Pct = maxPosVaR95;  // limit is 15% = 0.15



				// Predicted vol (annualised %, already computed above) and daily VaR95
				// PortfolioVOL shows this value as a percentage — store as decimal for alert comparison.
				// VaR95 daily = 1.645 * (annVol% / 100) / sqrt(252)
				if (!double.IsNaN(annVol))
				{
					_alertPredictedVol = annVol / 100.0;
					_alertPortfolioVaR95 = 1.645 * (_alertPredictedVol / Math.Sqrt(252.0));  // for MVaR95 ratio
																							 // Vol Neutral: portfolio vol vs target — uses _alertPredictedVol (decimal)
					_alertVolImbalance = _alertPredictedVol;
					// CVaR95 (Expected Shortfall): normal dist approximation = vol * phi(1.645) / 0.05 / sqrt(252)
					_alertCVaR95 = _alertPredictedVol * 0.10314 / (0.05 * Math.Sqrt(252.0));

					// Liquidity-adjusted VaR: VaR95 scaled by √(weighted avg liquidation days).
					// Textbook formula: LVaR = VaR × √T where T is the liquidation horizon.
					// Weighted avg days uses $-weighting so large positions dominate.
					// Positions without ADV data get days=1 floor via the dollar-weighting (they
					// contribute to the denominator but not to days, which is conservative-low).
					double avgLiqDays = liqDollarTotal > 0 ? liqDaysDollarWeighted / liqDollarTotal : 0;
					avgLiqDays = Math.Max(1.0, avgLiqDays);  // can't liquidate faster than 1 day
					_alertLiqVaR95 = _alertPortfolioVaR95 * Math.Sqrt(avgLiqDays);

					// Idiosyncratic risk: split total vol into systematic and idio components.
					// For a dollar-neutral long/short strategy, systematic exposure is better
					// proxied by net dollar exposure than net beta, because the strategy targets
					// dollar neutrality (not beta neutrality). Net beta is inflated by the
					// vol-neutral construction pairing high-beta longs with low-beta shorts.
					var systematicVol = Math.Abs(_alertNetExposure) * _assumedMarketVol;
					var idioVol = Math.Sqrt(Math.Max(0, _alertPredictedVol * _alertPredictedVol - systematicVol * systematicVol));
					_alertIdioRiskPct = _alertPredictedVol > 0 ? idioVol / _alertPredictedVol : 0;

					// Equity stress: expected portfolio PnL as % of NAV if the market moves ±5% / ±10%.
					// Formula: net_beta_dollars × shock / NAV
					// where net_beta_dollars = |Σ(beta × posValue for longs) − Σ(beta × posValue for shorts)|
					// If beta-neutral across the book, net_beta_dollars ≈ 0 → stress ≈ 0.
					// Vol-neutral ≠ beta-neutral, so this is measured independently of vol imbalance.
					if (portfolioBalance > 0)
					{
						double netBetaDollars = Math.Abs(longBetaDollars - shortBetaDollars);
						_alertEqStress5 = netBetaDollars * 0.05 / portfolioBalance;
						_alertEqStress10 = netBetaDollars * 0.10 / portfolioBalance;
					}
					else
					{
						_alertEqStress5 = 0;
						_alertEqStress10 = 0;
					}
				}

				_alertDataValid = true;  // data has been computed at least once

				// Update eq stress circles with actual percentage values
				// Format eq stress as signed decimal (e.g. "Eq Stress 5%  -2.8")
				_alertController?.ForceRefresh();
				updateExtendedAlertCircles();
				updateCategoryCircles();
			}
			catch (Exception ex)
			{
			}
			finally
			{
				// Always refresh sector tiles regardless of any exception above
				//				int openCount = trades?.Count(t => Math.Abs(t.Shares.Keys.FirstOrDefault(k => k.Date == time2.Date) != default ? t.Shares[t.Shares.Keys.First(k => k.Date == time2.Date)] : 0) > 0) ?? 0;
				//				if (sectorPercents != null)
				if (_activeTileView == "Industry") LoadTiles(industryPercents);
				else if (_activeTileView == "SubIndustry") LoadTiles(subIndustryPercents);
				else LoadTiles(sectorPercents);
			}
		}

		// Tracks which sector tab is active so refresh doesn't revert to Sectors
		private string _activeTileView = "Sector";

		private const double _limitTilePercent = 0.12;  // 12% — Hedge fund constraint for sector/industry/sub-industry

		/// <summary>
		/// Colors the new alert buttons that AlertController doesn't know about,
		/// using the same Lime/Red convention. Call after ForceRefresh().
		/// </summary>
		private void updateExtendedAlertCircles()
		{
			var checks = new Dictionary<string, bool>
			{
				// Exposure
				{ "BtnUtilization", _alertUtilization >= _limitUtilization },  // UCAP >= UT (50%) = lime
				// Risk Analytics: Max Portfolio VaR95
				{ "BtnMaxVaR95", _alertPortfolioVaR95 <= _limitMaxVaR95 },
				// Concentration: top-N
				{ "BtnTop5Long",   _alertTop5LongSum   <= _limitTop5LongSum },
				{ "BtnTop5Short",  _alertTop5ShortSum  <= _limitTop5ShortSum },
				{ "BtnTop10Long",  _alertTop10LongSum  <= _limitTop10LongSum },
				{ "BtnTop10Short", _alertTop10ShortSum <= _limitTop10ShortSum },
				// Liquidity
				{ "BtnADV20",  _alertADV20  <= _limitADV20 },
				{ "BtnADV50",  _alertADV50  <= _limitADV50 },
				{ "BtnADV100", _alertADV100 <= _limitADV100 },
				{ "BtnLiqVaR95", _alertLiqVaR95 <= _limitLiqVaR95 },
				// Market cap tiers
				{ "BtnLargeCapGross", _alertLargeCapGross <= _limitLargeCapGross },
				{ "BtnLargeCapNet",   _alertLargeCapNet   <= _limitLargeCapNet },
				{ "BtnMidCapGross",   _alertMidCapGross   <= _limitMidCapGross },
				{ "BtnMidCapNet",     _alertMidCapNet     <= _limitMidCapNet },
				{ "BtnSmallCapGross", _alertSmallCapGross <= _limitSmallCapGross },
				{ "BtnSmallCapNet",   _alertSmallCapNet   <= _limitSmallCapNet },
				// Risk Analytics: CVaR95
				{ "BtnCVaR95", _alertCVaR95 <= _limitCVaR95 },
			};

			var allButtons = FindVisualChildren<Button>(this)
				.Where(b => !string.IsNullOrEmpty(b.Name))
				.ToDictionary(b => b.Name, b => b, StringComparer.Ordinal);

			foreach (var kvp in checks)
			{
				if (allButtons.TryGetValue(kvp.Key, out var btn))
					btn.Foreground = kvp.Value ? Brushes.Lime : Brushes.Red;
			}

			// Update Risk Analytics threshold labels: show "limit | actual" when red
			updateRiskThresholdLabels(allButtons);
		}

		/// <summary>
		/// For each Risk Analytics alert, always show "limit | actual" in the threshold column.
		/// The actual value is silver when within limit, red when violating.
		/// </summary>
		private void updateRiskThresholdLabels(Dictionary<string, Button> allButtons)
		{
			// (btnName, limitText, isGreen, actualText)
			var riskAlerts = new[]
			{
				// Portfolio Const
				("BtnMktNeutral",  "10",  _alertNetBeta        <= _limitNetBeta,       $"{_alertNetBeta * 100:F1}"),
				("BtnVolNeutral",  "12",  _alertVolImbalance   <= _limitVolNeutral,    $"{_alertVolImbalance * 100:F1}"),
				// IntradayDD: circle only — clear the static "3" TextBlock, write nothing
				("BtnIntradayDD", "", _alertIntradayDD >= -_limitIntradayDD, ""),
				// UCAP: green when utilization >= 50% (capital deployed); red when under-utilised
				("BtnUtilization",  "50",  _alertUtilization         >= _limitUtilization,                  $"{_alertUtilization * 100:F1}"),
				// Gross book: sum of |longs| + |shorts| / NAV — limit 200%
				("BtnGrossBook",    "200", _alertGrossBook           <= _limitGrossBook,                    $"{_alertGrossBook * 100:F1}"),
				// Net exposure: signed (longAmt - shortAmt) / NAV — limit ±10%
				("BtnNetExposure",  "10",  Math.Abs(_alertNetExposure) <= _limitNetExposure,                $"{_alertNetExposure * 100:F1}"),
				// Max single-name position weight — red when any position reaches or exceeds 10%
				("BtnMaxPosition",  "10",  _alertMaxPositionWeight   < _limitMaxPosition,                   $"{_alertMaxPositionWeight * 100:F1}"),
				// Concentration: top-N weight sums
				("BtnTop5Long",   "40",  _alertTop5LongSum   <= _limitTop5LongSum,   $"{_alertTop5LongSum * 100:F1}"),
				("BtnTop5Short",  "35",  _alertTop5ShortSum  <= _limitTop5ShortSum,  $"{_alertTop5ShortSum * 100:F1}"),
				("BtnTop10Long",  "75",  _alertTop10LongSum  <= _limitTop10LongSum,  $"{_alertTop10LongSum * 100:F1}"),
				("BtnTop10Short", "65",  _alertTop10ShortSum <= _limitTop10ShortSum, $"{_alertTop10ShortSum * 100:F1}"),
				// Liquidity: color the static threshold text lime/red — actual % shown as text
				("BtnADV20",  "30", _alertADV20  <= _limitADV20,  ""),
				("BtnADV50",  "10", _alertADV50  <= _limitADV50,  ""),
				("BtnADV100", "0",  _alertADV100 <= _limitADV100, ""),
				("BtnLiqVaR95", "2", _alertLiqVaR95 <= _limitLiqVaR95, $"{_alertLiqVaR95 * 100:F2}"),
				// Market cap tiers: gross and |net| as % of NAV
				("BtnLargeCapGross", "175", _alertLargeCapGross <= _limitLargeCapGross, ""),
				("BtnLargeCapNet",   "15",  _alertLargeCapNet   <= _limitLargeCapNet,   ""),
				("BtnMidCapGross",   "100", _alertMidCapGross   <= _limitMidCapGross,   ""),
				("BtnMidCapNet",     "15",  _alertMidCapNet     <= _limitMidCapNet,     ""),
				("BtnSmallCapGross", "25",  _alertSmallCapGross <= _limitSmallCapGross, ""),
				("BtnSmallCapNet",   "2.5", _alertSmallCapNet   <= _limitSmallCapNet,   ""),
				// Statistical Risk
				("BtnMaxVaR95",   "1",    _alertPortfolioVaR95 <= _limitMaxVaR95,     $"{_alertPortfolioVaR95 * 100:F2}"),
				("BtnCVaR95",     "1.5",  _alertCVaR95         <= _limitCVaR95,       $"{_alertCVaR95 * 100:F2}"),
				("BtnMVaR95",     "15",   _alertMVaR95Pct      <= _limitMVaR95Pct,    $"{_alertMVaR95Pct * 100:F1}"),
				("BtnIdioRisk",   "70",   !_alertDataValid || _alertIdioRiskPct >= _limitIdioRiskMin, $"{_alertIdioRiskPct * 100:F1}"),
				("BtnMaxPredVol", "12",   _alertPredictedVol   <= _limitPredictedVol, $"{_alertPredictedVol * 100:F1}"),
				("BtnEqStress5",  "2",    _alertEqStress5      <= _limitEqStress5,    $"{_alertEqStress5 * 100:F1}"),
				("BtnEqStress10", "3.5",  _alertEqStress10     <= _limitEqStress10,   $"{_alertEqStress10 * 100:F1}"),
			};

			foreach (var (btnName, limitText, isGreen, actualText) in riskAlerts)
			{
				if (!allButtons.TryGetValue(btnName, out var btn)) continue;
				var color = isGreen ? Brushes.Lime : Brushes.Red;
				btn.Foreground = color;
				// For Utilization, update the named value TextBlock to show actual %
				if (btnName == "BtnUtilization")
				{
					var valLbl = FindVisualChildren<TextBlock>(this).FirstOrDefault(tb => tb.Name == "LblUtilizationValue");
					if (valLbl != null) { valLbl.Text = actualText; valLbl.Foreground = color; }
				}

				// Primary path: TextBlock at column 1 of button's parent Grid.
				// Works for alerts whose XAML row has an inline threshold TextBlock.
				var parentGrid = btn.Parent as Grid;
				if (parentGrid != null)
				{
					var threshold = parentGrid.Children.OfType<TextBlock>()
						.FirstOrDefault(tb => Grid.GetColumn(tb) == 1);
					if (threshold != null)
					{
						threshold.Inlines.Clear();
						threshold.Inlines.Add(new System.Windows.Documents.Run(actualText) { Foreground = color });
						continue;  // done — skip fallback
					}
				}

				// Fallback path: write directly to the registered Lbl* TextBox.
				// Used for alerts whose parent Grid does not contain a column-1 TextBlock.
				// Skip entirely when actualText is empty — those alerts (ADV, MktCap)
				// use the silver pre-baked "Lbl ≤N" label and should not be overwritten.
				// NOTE: FindName() cannot see into Expander content namescopes, so
				// we use a visual tree walk (same technique as allButtons) instead.
				if (string.IsNullOrEmpty(actualText)) continue;
				var lblName = btnName.Replace("Btn", "Lbl");
				var lbl = FindVisualChildren<TextBox>(this).FirstOrDefault(tb => tb.Name == lblName);
				if (lbl != null)
				{
					lbl.Text = actualText;
					lbl.Foreground = color;
				}
			}
		}
		private void collapseAllAlertCategories()
		{
			foreach (var expander in FindVisualChildren<Expander>(this))
				expander.IsExpanded = false;
		}

		/// <summary>
		/// Rolls up individual alert circle colors to category header circles.
		/// A category header turns red if any child circle is red.
		/// Call this immediately after AlertController.ForceRefresh().
		/// </summary>
		private void updateCategoryCircles()
		{
			var categories = new Dictionary<string, string[]>
			{
				{ "BtnCatConnectivity",    new[] { "BtnFlexOne", "BtnBloomberg" } },
				{ "BtnCatPortfolioConst",  new[] { "BtnMktNeutral", "BtnVolNeutral" } },
				{ "BtnCatExposure",        new[] { "BtnIntradayDD", "BtnUtilization", "BtnGrossBook",
												   "BtnNetExposure", "BtnMaxPosition" } },
				{ "BtnCatConcentration",   new[] { "BtnTop5Long", "BtnTop5Short",
												   "BtnTop10Long", "BtnTop10Short" } },
				{ "BtnCatLiquidity",       new[] { "BtnADV20", "BtnADV50", "BtnADV100", "BtnLiqVaR95" } },
				{ "BtnCatMktCap",          new[] { "BtnLargeCapGross", "BtnLargeCapNet",
												   "BtnMidCapGross", "BtnMidCapNet",
												   "BtnSmallCapGross", "BtnSmallCapNet" } },
				{ "BtnCatRisk",            new[] { "BtnMaxVaR95", "BtnMaxPredVol", "BtnCVaR95",
												   "BtnMVaR95", "BtnIdioRisk", "BtnFactorRisk",
												   "BtnEqStress5", "BtnEqStress10" } },
			};

			var allButtons = FindVisualChildren<Button>(this)
				.Where(b => !string.IsNullOrEmpty(b.Name))
				.ToDictionary(b => b.Name, b => b, StringComparer.Ordinal);

			foreach (var kvp in categories)
			{
				if (!allButtons.TryGetValue(kvp.Key, out var headerBtn)) continue;
				bool anyRed = kvp.Value
					.Select(name => allButtons.TryGetValue(name, out var b) ? b : null)
					.Where(b => b != null)
					.Any(b => b.Foreground is SolidColorBrush sb && sb.Color == Colors.Red);
				headerBtn.Foreground = anyRed ? Brushes.Red : Brushes.Lime;
			}
		}

		private void LoadTiles(Dictionary<string, string> data)
		{
			if (TilesList == null || data == null) return;

			var rows = data.OrderByDescending(x => x.Value.Length > 0 ? double.Parse(x.Value.Replace("%", "")) : 0).Select(kv =>
			{
				double pct = 0;
				if (kv.Value.Length > 0 && double.TryParse(kv.Value.Replace("%", ""), out double parsed))
					pct = parsed / 100.0;
				bool breach = Math.Abs(pct) > _limitTilePercent;
				return new NameValueRow
				{
					Name = kv.Key,
					Value = kv.Value,
					ValueForeground = breach ? "#ff0000" : "#98fb98"  // light yellow if > 12%, white otherwise
				};
			}).ToList();

			TilesList.ItemsSource = rows;
		}

		/// <summary>Deserializes pipe-delimited sector percents from Hedge Fund App into the dict.</summary>
		private void deserializePercents(string data, Dictionary<string, string> target)
		{
			if (string.IsNullOrEmpty(data) || target == null) return;
			foreach (var pair in data.Split('|'))
			{
				var idx = pair.IndexOf('=');
				if (idx < 0) continue;
				var key = pair.Substring(0, idx);
				var val = pair.Substring(idx + 1);
				if (target.ContainsKey(key)) target[key] = val;
			}
		}

		private void Sector_MouseEnter(object sender, MouseEventArgs e)
		{
			if (sender is Label label && label.Tag?.ToString() != _activeLabel)
				label.Foreground = (Brush)new BrushConverter().ConvertFrom("#00ccff");
		}
		private void Sector_MouseLeave(object sender, MouseEventArgs e)
		{
			if (sender is Label label && label.Tag?.ToString() != _activeLabel)
				label.Foreground = Brushes.White;
		}
		private void Performance_MouseDown(object sender, MouseEventArgs e)
		{
			hideNavigation();

			//PositionsChartBorder.Visibility = Visibility.Visible;
			//TotalReturnChartBorder.Visibility = Visibility.Visible;

			//TISetup.Visibility = Visibility.Collapsed;

			//TIOpenPL.Visibility = Visibility.Collapsed;
			//TIAllocations.Visibility = Visibility.Collapsed;

			//RiskTable.Visibility = Visibility.Collapsed;
			//RecoveryTable.Visibility = Visibility.Collapsed;

			//UserFactorModelGrid.Visibility = Visibility.Collapsed;

			//PerformanceGrid.Visibility = Visibility.Visible;
			//ReturnTable.Visibility = Visibility.Visible;
			//PerformanceSideNav.Visibility = Visibility.Visible;

			//ATMStrategyOverlay.Visibility = Visibility.Collapsed;

			//RebalanceChartGrid.Visibility = Visibility.Collapsed;
			//RebalanceGrid.Visibility = Visibility.Collapsed;
			//RebalanceSideNav.Visibility = Visibility.Collapsed;

			//AllocationGrid.Visibility = Visibility.Collapsed;
			//AllocationSideNav.Visibility = Visibility.Collapsed;

			//UserFactorModelPanel2.Visibility = _useUserFactorModel ? Visibility.Visible : Visibility.Collapsed;
			//updateUserFactorModelList();
		}
		private void RebalanceView_MouseDown(object sender, MouseEventArgs e)
		{
			//hideNavigation();

			//showRebalanceGrid();

			//setModelRadioButtons();

			//updateUserFactorModelList();

			////saveUserFactorModels();

			//if (_portfolioTimes.Count > 0)
			//{
			//	var model = getModel();
			//	var date = _portfolioTimes.Last();
			//	setHoldingsCursorTime(date);
			//}
		}


		private void LabelSectors_MouseDown(object sender, MouseButtonEventArgs e)
		{
			_activeTileView = "Sector";
			_activeTileView = "Sector";
			_activeLabel = "Sector";
			HighlightActiveLabel(LabelSectors);
			LoadTiles(sectorPercents);
		}

		private void LabelIndustry_MouseDown(object sender, MouseButtonEventArgs e)
		{
			_activeTileView = "Industry";
			_activeTileView = "Industry";
			_activeLabel = "Industry";
			HighlightActiveLabel(LabelIndustry);
			LoadTiles(industryPercents);
		}

		private void LabelSubIndustry_MouseDown(object sender, MouseButtonEventArgs e)
		{
			_activeTileView = "SubIndustry";
			_activeTileView = "SubIndustry";
			_activeLabel = "SubIndustry";
			HighlightActiveLabel(LabelSubIndustry);
			LoadTiles(subIndustryPercents);
		}

		private void HighlightActiveLabel(Label active)
		{
			foreach (var l in new[] { LabelSectors, LabelIndustry, LabelSubIndustry })
				l.Foreground = Brushes.White;

			active.Foreground = (Brush)new BrushConverter().ConvertFrom("#00ccff");
		}

		private void changeChart(Chart chart, string symbol, string interval, string portfolioName)
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

			if (symbol != null && symbol != chart.Symbol || interval != chart.Interval)
			{
				chart.Change(symbol, interval);
			}
		}

		int _yieldAgo = 0;

		List<Yield> getYields(string conditionName, BarCache barCache, List<Symbol> symbols, string interval, int ago)
		{
			var output = new List<Yield>();

			if (symbols != null)
			{
				var yearExists = new Dictionary<double, string>();
				foreach (var member in symbols)
				{
					var description = (member.Description.Length > 0) ? member.Description : member.Ticker;
					var ticker = member.Ticker;

					double years = getYears(member);

					var series = barCache.GetSeries(ticker, interval, new string[] { "High", "Low", "Close" }, 0, 100);

					var hi = series[0];
					var lo = series[1];
					var cl = series[2];

					var dates = barCache.GetTimes(ticker, interval, 0);

					bool hiOk = false;
					for (int ii = 0; ii < hi.Count && !hiOk; ii++)
					{
						hiOk = !double.IsNaN(hi[ii]);
					}

					if (!hiOk) hi = cl;

					bool loOk = false;
					for (int ii = 0; ii < lo.Count && !loOk; ii++)
					{
						loOk = !double.IsNaN(lo[ii]);
					}

					if (!loOk) lo = cl;

					double condition = 0;

					if (conditionName == "ATM FT")
					{
						Series ft = atm.calculateFT(hi, lo, cl);
						Series sig = atm.goingUp(ft) - atm.goingDn(ft);
						condition = (sig.Count > ago) ? sig[sig.Count - 1 - ago] : double.NaN;  // not working on Compare to
					}
					else if (conditionName == "ATM ST")
					{
						Series st = atm.calculateST(hi, lo, cl);
						Series sig = atm.goingUp(st) - atm.goingDn(st);
						condition = (sig.Count > ago) ? sig[sig.Count - 1 - ago] : double.NaN;  // not working on Compare to
					}
					//else if (conditionName == "ATM Trend Bars")
					//{
					//    Series sig = atm.calculateTrendBars(hi, lo, cl);  //  ?? not working
					//    condition = (sig.Count > ago) ? sig[sig.Count - 1 - ago] : double.NaN;  // not working on Compare to
					//}
					else if (conditionName == "ATM Pxf")
					{
						var tickers = new List<string>();
						tickers.Add(ticker);

						var predictionAgoCount = ago + 1;
						var model = MainView.GetModel(_modelName);
						if (model != null)
						{
							var pathName = @"senarios\" + model.Name + @"\" + interval + (model.UseTicker ? @"\" + MainView.ToPath(ticker) : "");
							var data = atm.getModelData(model.FeatureNames, model.Scenario, _barCache, tickers, interval, predictionAgoCount, model.MLSplit, false, true);
							atm.saveModelData(pathName + @"\test.csv", data);
							var predictions = MainView.AutoMLPredict(pathName, MainView.GetSenarioLabel(model.Scenario));

							var date = (dates.Count > ago) ? dates[dates.Count - 1 - ago] : default(DateTime);
							condition = (predictions[ticker][interval].ContainsKey(date) ? ((predictions[ticker][interval][date] > 0.5) ? 1 : -1) : 0);
						}
					}

					var value = (cl.Count > ago) ? cl[cl.Count - 1 - ago] : double.NaN;

					if (symbols == _compareSymbols)
					{
						//Trace.WriteLine(ticker + " yield = " + value.ToString("0.00") + " years = " + years);
					}

					if (!yearExists.ContainsKey(years))
					{
						var yield = new Yield(ticker, getYieldSymbol(member), years, value, condition);
						yearExists[years] = ticker;
						output.Add(yield);
					}
					else
					{
						//Trace.WriteLine("The number of years is the same for |" + yearExists[years] + "| and |" + ticker + "|");
						yearExists[years] = ticker;
					}
				}
			}
			return output;
		}

		void setHeights()
		{
			bool hasChart = _chart1 != null && ChartGrid.Visibility == Visibility.Visible;
			double gridHeight = Grid1.ActualHeight;

			if (hasChart)
			{
				// Chart visible: split space evenly
				ConditionsGrid.Height = 0.50 * gridHeight;
				TimeFramesGrid.Height = 0.50 * gridHeight;
				ChartGrid.Height = 0.50 * gridHeight;
			}
			else
			{
				// No chart: expand ConditionsGrid to full height
				ConditionsGrid.Height = 1.00 * gridHeight;
				TimeFramesGrid.Height = 0.00 * gridHeight;
				ChartGrid.Height = 0.00 * gridHeight;
			}
		}

		private void initializeCharts()
		{
			bool showCursor = !_mainView.HideChartCursor;

			var modelNames = new List<string>();
			modelNames.Add(_modelName);

			_chart1 = new Chart(ChartCanvas1, ChartControlPanel1, showCursor);
			//_chart1.SpreadSymbols = (_view != "By CONDITION 2");
			_chart1.HasTitleIntervals = (_view != "By CONDITION 2");
			_chart1.Horizon = 2;
			_chart1.Strategy = getStrategy();
			_chart1.ModelNames = modelNames;
			_chart1.ChartEvent += new ChartEventHandler(Chart_ChartEvent);

			_chart2 = new Chart(ChartCanvas2, ChartControlPanel2, showCursor);
			_chart2.SpreadSymbols = false;
			_chart2.HasTitleIntervals = false;
			_chart2.Horizon = 1;
			_chart2.Strategy = getStrategy();
			_chart2.ModelNames = modelNames;
			_chart2.ChartEvent += new ChartEventHandler(Chart_ChartEvent);

			_chart1.AddLinkedChart(_chart2);
			_chart2.AddLinkedChart(_chart1);

			loadChartProperties(1);
			loadChartProperties(2);
			addBoxes(true);
			setHeights();

			hideChart();
		}

		//string _yieldCondition = "ATM Trend Bars";

		private void hideChart()
		{
			if (_chart1 != null)
			{
				ChartGrid.Visibility = Visibility.Collapsed;
				//CurveGrid.Visibility = Visibility.Collapsed;

				Grid.SetRowSpan(ConditionsGrid, 2);
				_chart1.Hide();
				_chart2.Hide();
				//_yieldChart.Hide();

				//removeCharts();
				setHeights();
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
			if (e.Id == ChartEventType.Cursor)
			{
				Chart chart = sender as Chart;
			}
			else if (e.Id == ChartEventType.SettingChange)
			{
				if (sender == _chart2)
				{
					calculateColors(false);
				}
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
			else if (e.Id == ChartEventType.PCMOrder)
			{
				updatePCMResult(_symbol);
			}
			else if (e.Id == ChartEventType.TradeEvent) // trade event change
			{
				if (_memberList.Count == 0)
				{
					var name = getPortfolioName();
					var type = getPortfolioType(name, _clientPortfolioName);
					requestPortfolio(name, type);
				}
				_updateSpreadsheet = 1;
			}
			else if (e.Id == ChartEventType.ChangeSymbol) // change symbol change
			{
				Chart chart = sender as Chart;
				_symbol = chart.SymbolName;

				var portfolioName = getPortfolioName();
				_yieldPortfolio = _portfolio.IsYieldPortfolio(portfolioName) && _memberList.Select(x => x.Ticker).Contains(_symbol);
				showCharts();
			}
			else if (e.Id == ChartEventType.ToggleCursor) // toggle cursor on/off
			{
				Chart chart = sender as Chart;
				_mainView.HideChartCursor = !chart.ShowCursor;
			}
			else if (e.Id == ChartEventType.SetupConditions)
			{
				Chart chart = sender as Chart;

				ConditionDialog dlg = getConditionDialog();
				int horizon = chart.Horizon;
				dlg.Condition = MainView.GetConditions(horizon);
				dlg.Horizon = horizon;

				//DialogWindow2.Show();
			}
			else if (e.Id == ChartEventType.PartitionCharts)
			{
				_twoCharts = !_twoCharts;

				updateChart(_symbol);
				showCharts();
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

				chart.Indicators["Two Bar Alert"].Enabled = (false);

				//chart.Indicators["Two Bar Trend"].Enabled = (false);

				chart.Indicators["FT Alert"].Enabled = (false);

				chart.Indicators["ST Alert"].Enabled = (false);

				chart.Indicators["FTST Alert"].Enabled = (false);

				chart.Indicators["EW Counts"].Enabled = (false);

				chart.Indicators["EW PTI"].Enabled = (false);

				chart.Indicators["EW Projections"].Enabled = (false);

				chart.Indicators["Short Term FT Current"].Enabled = (research);

				chart.Indicators["Short Term FT Nxt Bar"].Enabled = (research);

				chart.Indicators["Short Term ST Current"].Enabled = (research);

				chart.Indicators["Short Term ST Nxt Bar"].Enabled = (research);

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

		private ConditionDialog getConditionDialog()
		{
			if (_conditionDialog == null)
			{
				_conditionDialog = new ConditionDialog(_mainView);
				_conditionDialog.DialogEvent += new DialogEventHandler(dialogEvent);
			}
			//DialogWindow2.Content = _conditionDialog;
			return _conditionDialog;
		}

		private void dialogEvent(object sender, DialogEventArgs e)
		{
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
			//DialogWindow2.Close();
		}

		private string getInterval(string interval)
		{
			if (interval == "Yearly") interval = "Y";
			else if (interval == "SemiAnnually") interval = "S";
			else if (interval == "Quarterly") interval = "Q";
			else if (interval == "Monthly") interval = "M";
			else if (interval == "Weekly") interval = "W";
			else if (interval == "Daily") interval = "D";
			else interval = interval.Replace(" Min", "");
			return interval;
		}

		private void update(string nav1, string nav2, string nav3, string nav4, string nav5, string nav6)
		{
			// RBAC gate: block loading TEST ML portfolios for non-admin users.
			// This catches every path into the chart/alert/view update — NavCol2 clicks,
			// state restoration, Portfolio_MouseDown, compare/overlay triggers, etc.
			if (nav1 == "ML PORTFOLIOS >"
				&& !ATMML.Auth.AuthContext.Current.IsAdmin
				&& !string.IsNullOrEmpty(nav2)
				&& !ModelAccessGate.IsLive(nav2))
			{
				return;  // silently refuse — nav filter should have prevented this anyway
			}

			if (_setComparePortfolio)
			{
				_comparePortfolioName = getPortfolioName(nav2, nav3, nav4, nav5, nav6, _clientPortfolioName);
				_comparePortfolioType = getPortfolioType(_overlayPortfolioName, _clientPortfolioName);

				ComparePortfolio.Content = _comparePortfolioName;

				_updateMemberList = true;
			}
			else if (_setOverlayPortfolio)
			{
				_overlayPortfolioName = getPortfolioName(nav2, nav3, nav4, nav5, nav6, _clientPortfolioName);
				_overlayPortfolioType = getPortfolioType(_overlayPortfolioName, _clientPortfolioName);

				Country2.Content = _overlayPortfolioName;
			}
			else
			{
				_nav1 = nav1;
				_nav2 = nav2;
				_nav3 = nav3;
				_nav4 = nav4;
				_nav5 = nav5;
				_nav6 = nav6;

				string name = getPortfolioName();
				Country.Content = name;
				Country1.Content = name;
				SpreadsheetGroup.Content = (_clientPortfolioName.Length > 0) ? name : getGroupLabel();
				var type = getPortfolioType(name, _clientPortfolioName);

				//_yieldChart.Title = getPortfolioName();
				NotifyPropertyChanged("YieldTitle");

				requestPortfolio(name, type);
			}

			hideNavigation();

			showView();

			updateOverlayUI();
		}

		private bool isCQGPortfolio(List<Symbol> symbols)
		{
			return symbols.Count > 0 && Portfolio.isCQGSymbol(symbols[0].Ticker);
		}

		private Portfolio.PortfolioType getPortfolioType(string name, string clientPortfolioName)
		{
			Portfolio.PortfolioType type = Portfolio.PortfolioType.Index;

			if (_nav1 == "ML PORTFOLIOS >")
			{
				type = Portfolio.PortfolioType.Model;
			}
			else if (clientPortfolioName.Length > 0)
			{
				type = _clientPortfolioType;
			}
			else if (Portfolio.IsBuiltInPortfolio(name))
			{
				type = Portfolio.PortfolioType.BuiltIn;
			}
			else if (name.Contains(" Index") || name.Contains(" Equity") || name.Contains(" Comdty") || name.Contains(" Curncy"))
			{
				type = Portfolio.PortfolioType.Single;
			}
			else if (name.Contains("Spread"))
			{
				type = Portfolio.PortfolioType.Spread;
			}
			else if (_nav2.Contains("EQS"))
			{
				type = Portfolio.PortfolioType.EQS;
			}
			else if (_nav2.Contains("PRTU"))
			{
				type = Portfolio.PortfolioType.PRTU;
			}
			else if (_nav2.Contains("WORKSHEET"))
			{
				type = Portfolio.PortfolioType.Worksheet;
			}
			else
			{
				type = Portfolio.PortfolioType.Index;
			}
			return type;
		}

		private string getGroupLabel()
		{
			string name = (_nav4 == "" || _nav4 == "MEMBERS") ? _nav3 : (_nav5 == "" || _nav5 == "MEMBERS") ? _nav4 : _nav5;
			string[] field = name.Split(':');
			string label = (field.Length == 2) ? field[1] : field[0];
			return label;
		}

		private TradeHorizon getTradeHorizon()
		{
			return Trade.Manager.GetTradeHorizon(getPortfolioName());
		}

		private string getPortfolioName()
		{
			return getPortfolioName(_nav2, _nav3, _nav4, _nav5, _nav6, _clientPortfolioName);
		}

		private string getPortfolioName(string nav2, string nav3, string nav4, string nav5, string nav6, string clientPortfolioName)
		{
			string portfolioName = "";
			if (clientPortfolioName.Length > 0)
			{
				portfolioName = clientPortfolioName;
			}
			else
			{
				string name = "";
				if (nav6 != "" && _nav6 != "MEMBERS") name = _nav6;
				else if (nav5 != "" && _nav5 != "MEMBERS") name = _nav5;
				else if (nav4 != "" && _nav4 != "MEMBERS") name = _nav4;
				else if (nav3 != "" && _nav3 != "MEMBERS") name = _nav3;
				else name = nav2;

				string[] field = name.Split(':');
				portfolioName = field[0];
			}
			return portfolioName;
		}

		private void updateMemberList()
		{
			List<string> tickers = new List<string>();

			lock (_memberListLock)
			{
				int colCnt = getColCount();

				_memberList.Clear();

				if (_clientPortfolioName.Length > 0 && _clientPortfolioType == Portfolio.PortfolioType.Peers)
				{
					var member = new Member(_clientPortfolioName);
					member.ConditionValues = new int[colCnt];
					member.ConditionCountUp = new int[colCnt];
					member.ConditionCountDn = new int[colCnt];
					_memberList.Add(member);
				}

				var name = getPortfolioName();
				var type = getPortfolioType(name, _clientPortfolioName);

				DateTime maxTime = DateTime.MinValue;
				foreach (var symbol in _symbols)
				{
					var time = symbol.Time1;
					if (time > maxTime)
					{
						maxTime = time;
					}
				}

				Dictionary<string, Member> members = new Dictionary<string, Member>();

				foreach (var symbol in _symbols)
				{
					bool ok = true; // (type == Portfolio.PortfolioType.Model) ? symbol.Time1 == maxTime && symbol.Size1 != 0 : true;
					if (ok)
					{

						var member = new Member(symbol.Ticker, symbol.Description, symbol.Group, symbol.Sector, symbol.Size1, symbol.Size2);
						member.Interval = symbol.Interval;
						member.ConditionValues = new int[colCnt];
						member.ConditionCountUp = new int[colCnt];
						member.ConditionCountDn = new int[colCnt];


						var symbolKey = getSymbolKey(member);

						if (symbolKey.Length > 0)
						{
							members[symbolKey] = member;
						}
					}
				}

				List<Member> sortedMembers = members.ToList().OrderBy(x => x.Key).Select(x => x.Value).ToList();

				foreach (var member in sortedMembers)
				{
					tickers.Add(member.Ticker);
					_memberList.Add(member);
				}

				_updateMemberList = true;
			}


			requestReferenceData(tickers);
		}

		List<string> _referenceSymbols = new List<string>();

		void requestYieldMembers(string name)
		{
			if (_yieldPortfolio)
			{
				string yieldTicker = _portfolio.GetYieldTicker(name);

				yieldTicker = yieldTicker.Replace(" Index", "");
				_portfolio.RequestSymbols(yieldTicker, Portfolio.PortfolioType.Index, false);
			}
		}

		void requestReferenceData(List<string> tickers)
		{
			int count = tickers.Count;

			lock (_referenceSymbols)
			{
				if (_yieldPortfolio)
				{
					string[] dataFieldNames2 = { "CURVE_TENOR_RATES" };
					string yieldTicker = _portfolio.GetYieldTicker(getPortfolioName());
					_portfolio.RequestReferenceData(yieldTicker, dataFieldNames2);

					_referenceSymbols.Clear();
					for (int ii = 0; ii < count; ii++)
					{
						if (tickers.Contains(" Equity"))
						{
							_referenceSymbols.Add(tickers[ii]);
						}
					}
				}
			}

			for (int jj = 0; jj < count; jj++)
			{
				string[] fields = tickers[jj].Split(Symbol.SpreadCharacter);

				var ticker = fields[0];
				if (!tickers.Contains(" Index"))
				{
					string[] dataFieldNames = { "REL_INDEX", "CUR_MKT_CAP" };
					_portfolio.RequestReferenceData(ticker, dataFieldNames, true);
				}
			}
		}

		int _indexBarRequestCount = 0;

		List<Symbol> _symbols = new List<Symbol>();
		List<Symbol> _compareSymbols;

		int _barCount = 500;

		void portfolioChanged(object sender, PortfolioEventArgs e)
		{
			if (e.Type == PortfolioEventType.Symbol)
			{
				if (_compareToSelection)
				{
					_compareToSelection = false;
					_compareSymbols = _portfolio.GetSymbols();
					_compareSymbols.ForEach(x => _barCache.RequestBars(x.Ticker, "D", false, _barCount));

					//this.Dispatcher.Invoke(DispatcherPriority.Normal, (Action)delegate () { drawYieldCurve(); });
				}
				else if (_portfolioRequested > 0)
				{
					_portfolioRequested--;
					_symbols = _portfolio.GetSymbols();
					if (_portfolioRequested == 0)
					{
						_requestRelativeIndexes = true;
						_referenceData.Clear();
						updateMemberList();
						if (_memberList.Count > 0)
						{
							this.Dispatcher.Invoke(DispatcherPriority.Normal, (Action)delegate () { updateChart(_memberList[0].Ticker); });
						}

						if (isCQGPortfolio(_symbols))
						{
							_indexSymbols.Add("F.EP");
							calculateColors(true);
						}
					}
				}
			}
			else if (e.Type == PortfolioEventType.ReferenceData)
			{
				bool okToRequestIndexBars = false;
				_updateMonitorData = false;

				foreach (KeyValuePair<string, object> kvp in e.ReferenceData)
				{
					string name = kvp.Key;
					object value = kvp.Value;
					if (name == "CUR_MKT_CAP")
					{
						// Strip date prefix e.g. "2012-12-1252731.0753" → "52731.0753"
						var text = value?.ToString() ?? "";
						if (text.Length > 10 && text[4] == '-' && text[7] == '-')
							text = text.Substring(10);
						if (!string.IsNullOrEmpty(text) && double.TryParse(text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double capM) && capM > 0)
							lock (_marketCapCache) { _marketCapCache[e.Ticker] = capM; }
					}
					else if (name == "REL_INDEX")
					{
						var text = value as string;
						if (text.Length > 0 && !text.Contains(" Index"))
						{
							text += " Index";
						}
						string key = e.Ticker + ":" + name;
						lock (_referenceData)
						{
							_referenceData[key] = text;
						}

						lock (_referenceSymbols)
						{
							if (_referenceSymbols.Count >= 0)
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

					}
					else if (name == "DS192")
					{
						lock (_memberListLock)
						{
							foreach (var member in _memberList)
							{
								if (member.Ticker == e.Ticker)
								{
									member.Description = value as string;
									break;

								}
							}
						}
					}

					else if (value is double)
					{
						var factor = (name == "BID_SIZE" || name == "ASK_SIZE") ? 100 : 1;
						var format = (name == "PX_VOLUME" || name == "BID_SIZE" || name == "ASK_SIZE") ? "0" : "0.00";
						_monitorData[name] = ((double)value * factor).ToString(format);
						_updateMonitorData = true;
					}
				}

				if (_requestRelativeIndexes && okToRequestIndexBars)
				{
					stopForecasting();

					_requestRelativeIndexes = false;

					_indexSymbols = new List<string>();
					lock (_referenceData)
					{
						foreach (KeyValuePair<string, object> kvp in _referenceData)
						{
							string key = kvp.Key;
							if (key.Contains("REL_INDEX"))
							{
								string indexSymbol = kvp.Value as string;
								if (indexSymbol.Length > 0 && !_indexSymbols.Contains(indexSymbol))
								{
									_indexSymbols.Add(indexSymbol);
								}
							}
						}
					}
					calculateColors(true);
				}
			}
		}

		Dictionary<string, string> _monitorData = new Dictionary<string, string>();
		bool _updateMonitorData = false;

		private void stopForecasting()
		{
			while (_forecasting)
			{
				_forecastRunning = false;
			}
		}

		private List<string> _indexBarRequests = new List<string>();

		private DateTime _calculateTime = DateTime.Now;
		private DateTime _lastPortfolioInfoTime = DateTime.MinValue;

		private void calculateColors(bool reset)
		{
			_calculateTime = DateTime.Now;
			if (reset) clearResults();
			_barCache.Clear();
			requestIndexBars();
		}

		private void requestIndexBars()
		{
			List<string> indexIntervals = getIntervals();

			_indexBarRequestCount = _indexSymbols.Count * indexIntervals.Count;

			lock (_indexBarRequests)
			{
				_indexBarRequests.Clear();
			}

			_conditionBrushes.Clear();

			if (_indexBarRequestCount > 0)
			{

				lock (_indexBarRequests)
				{
					foreach (var interval in indexIntervals)
					{
						foreach (string symbol in _indexSymbols)
						{
							_indexBarRequests.Add(symbol + ":" + interval);
						}
					}
				}

				foreach (var interval in indexIntervals)
				{
					foreach (string symbol in _indexSymbols)
					{
						_barCache.RequestBars(symbol, interval, false, _barCount);
					}
				}
			}
			else
			{
				requestSymbolBars();
			}
		}
		private void requestSymbolBars()
		{
			var intervals = getIntervals();

			lock (_memberListLock)
			{
				_symbolsRemaining.Clear();

				foreach (var member in _memberList)
				{
					string symbol = member.Ticker;
					_symbolsRemaining[symbol] = intervals.Count;

					foreach (string interval1 in intervals)
					{
						_barCache.RequestBars(symbol, interval1, false, _barCount);
					}
				}

				// Immediately enqueue symbols that already have all bars cached —
				// their barChanged won't fire again so they'd otherwise wait 15s for timeout
				foreach (var member in _memberList)
				{
					string symbol = member.Ticker;
					bool allCached = intervals.All(iv =>
					{
						var t = _barCache.GetTimes(symbol, iv, 0, 10);
						return t != null && t.Count > 0;
					});
					if (allCached)
					{
						lock (_symbolsRemaining) { _symbolsRemaining.Remove(symbol); }
						lock (_calcRequest) { _calcRequest.Enqueue(symbol); }
					}
				}
			}
		}

		private void clearResults()
		{
			lock (_atmResults)
			{
				_atmResults.Clear();
			}
			_updateSpreadsheet = 2;
		}

		public List<string> getIntervals()
		{
			List<string> outputs = new List<string>();
			if (_view == "By CONDITION" || _view == "By CONDITION 2")
			{
				string interval1 = _interval.Replace(" Min", "");
				string interval2 = getInterval(interval1, 1);
				string interval3 = getInterval(interval2, 1);
				//string interval4 = getInterval(interval3, 1);

				outputs.Add(interval1);
				outputs.Add(interval2);
				outputs.Add(interval3);
				// outputs.Add(interval4);

				if (!outputs.Contains("Daily"))
				{
					outputs.Add("Daily");
				}
			}
			else
			{
				outputs.Add("Yearly");
				outputs.Add("Quarterly");
				outputs.Add("Monthly");
				outputs.Add("Weekly");
				outputs.Add("Daily");
				outputs.Add("240");
				outputs.Add("120");
				outputs.Add("60");
				outputs.Add("30");
			}

			outputs = outputs.Distinct().ToList();
			return outputs;
		}

		public static string getInterval(string input, int level)
		{
			string output = "";
			if (input == "Yearly" || input == "1Y")
			{
				output = "Yearly";
			}
			else if (input == "SemiAnnually" || input == "1S")
			{
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
			else if (input == "Weekly" || input == "1W")
			{
				output = (level == -1) ? "Daily" : (level == 0) ? "Weekly" : (level == 1) ? "Monthly" : "Quarterly";
			}
			else if (input == "Daily" || input == "1D")
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

		private void CompareToPortfolio_MouseDown(object sender, MouseButtonEventArgs e)
		{
			_setOverlayPortfolio = false;
			_setComparePortfolio = true;

			if (NavCol1.Visibility == Visibility.Collapsed)
			{
				hideView();
				hideNavigation();

				//NewLongMemberList.Visibility = Visibility.Visible;
				//NewShortMemberList.Visibility = Visibility.Visible;

				PortfolioSelectionPanel.Visibility = Visibility.Visible;
				NavCol1.Visibility = Visibility.Visible;
				NavCol2.Visibility = Visibility.Visible;
				NavCol3.Visibility = Visibility.Visible;
				NavCol4.Visibility = Visibility.Visible;
				NavScroller4.Visibility = Visibility.Visible;
				NavCol5.Visibility = Visibility.Visible;
				NavCol6.Visibility = Visibility.Visible;
				NavScroller5.Visibility = Visibility.Visible;
				NavScroller6.Visibility = Visibility.Visible;
				SubgroupActionMenu1.Visibility = Visibility.Collapsed;
				SubgroupActionMenu2.Visibility = Visibility.Collapsed;
				MonitorGrid.Visibility = Visibility.Collapsed;

				var nav1 = "ALPHA PORTFOLIOS >";
				var nav2 = "";
				var nav3 = "";
				var nav4 = "";
				var nav5 = "";
				var nav6 = "";

				List<string> items = new List<string>();
				if (MainView.EnableRickStocks)
				{
					items.AddRange(new string[] { "ATM ALERTS >", " ", "ATM ML PX MODELS >", " ", "YOUR LISTS >", " ", "ALPHA NETWORKS >" });
				}

				else
				{
				}

				nav.setNavigation(NavCol1, NavCol1_MouseDown, items.ToArray());

				nav.setNavigationLevel1(nav1, NavCol2, NavCol2_MouseDown, go_Click);
				nav.setNavigationLevel2(nav2, NavCol3, NavCol3_MouseDown, go_Click);
				nav.setNavigationLevel3(nav2, nav3, NavCol4, NavCol4_MouseDown);
				nav.setNavigationLevel4(nav2, nav3, nav4, NavCol5, NavCol5_MouseDown);

				highlightButton(NavCol1, nav1);
				highlightButton(NavCol2, nav2);
				highlightButton(NavCol3, nav3);
				highlightButton(NavCol4, nav4);
				highlightButton(NavCol5, nav5);
				highlightButton(NavCol6, nav6);

				string resourceKey = "";

				if (resourceKey.Length > 0)
				{
					Viewbox legend = this.Resources[resourceKey] as Viewbox;
					if (legend != null)
					{
						legend.SetValue(MarginProperty, new Thickness(0, 20, 0, 0));
						NavCol1.Children.Add(legend);
					}
				}
			}
			else
			{
				showView();
			}
		}

		private string _modelName = "";

		private void ML_MouseDown(object sender, MouseButtonEventArgs e)
		{
			if (NavCol1.Visibility == Visibility.Collapsed)
			{
				hideView();
				hideNavigation();

				PortfolioSelectionPanel.Visibility = Visibility.Visible;
				NavCol1.Visibility = Visibility.Visible;

				var modelNames = MainView.getModelNames();
				// RBAC: non-admin users see LIVE models only in the ML model picker.
				if (!ATMML.Auth.AuthContext.Current.IsAdmin)
					modelNames = modelNames.Where(n => ModelAccessGate.IsLive(n)).ToList();
				modelNames.Insert(0, "NO PREDICTION");

				nav.setNavigation(NavCol1, Model_MouseDown, modelNames.ToArray());
				highlightButton(NavCol1, _modelName);
			}
			else
			{
				showView();
			}
		}

		private void Model_MouseDown(object sender, MouseButtonEventArgs e)
		{
			Label label = e.Source as Label;
			_modelName = label.Content as string;

			var model = MainView.GetModel(_modelName);
			var modelName = (model != null) ? _modelName : "";
			var scenario = (model != null) ? MainView.GetSenarioLabel(model.Scenario) : "";
			ScenarioModel.Content = modelName;
			ScenarioName.Content = scenario;

			if (_symbolsRemaining.Count == 0)
			{
				clearForecastCells();
				startForecasting();
			}

			hideNavigation();
			showView();

			var modelNames = new List<string>();
			modelNames.Add(_modelName);
			if (_chart1 != null)
			{
				_chart1.ModelNames = modelNames;
				_chart2.ModelNames = modelNames;
			}
		}

		private void Portfolio_MouseDown(object sender, MouseButtonEventArgs e)
		{
			_setOverlayPortfolio = false;
			_setComparePortfolio = false;

			if (NavCol1.Visibility == Visibility.Collapsed)
			{
				hideView();
				hideNavigation();

				PortfolioSelectionPanel.Visibility = Visibility.Visible;
				NavCol1.Visibility = Visibility.Visible;
				NavCol2.Visibility = Visibility.Visible;
				NavCol3.Visibility = Visibility.Visible;
				NavCol4.Visibility = Visibility.Visible;
				NavScroller4.Visibility = Visibility.Visible;
				NavCol5.Visibility = Visibility.Visible;
				NavCol6.Visibility = Visibility.Visible;
				NavScroller5.Visibility = Visibility.Visible;
				NavScroller6.Visibility = Visibility.Visible;
				SubgroupActionMenu1.Visibility = Visibility.Collapsed;
				SubgroupActionMenu2.Visibility = Visibility.Collapsed;
				MonitorGrid.Visibility = Visibility.Collapsed;

				_selectedNav1 = _nav1;
				_selectedNav2 = _nav2;
				_selectedNav3 = _nav3;
				_selectedNav4 = _nav4;

				List<string> items = new List<string>();
				if (BarServer.ConnectedToBloomberg() || !BarServer.ConnectedToCQG()) items.AddRange(new string[] { "BLOOMBERG >", " ", "COMMODITIES >", " ", "EQ | AMERICAS >", " ", "EQ | ASIA PACIFIC >", " ", "EQ | EUROPE & MEA >", " ", "ETF >", " ", "FX & CRYPTO >", " ", "GLOBAL FUTURES >", " ", "INTEREST RATES >", " ", "ML PORTFOLIOS >" });
				if (BarServer.ConnectedToCQG()) items.AddRange(new string[] { " ", "CQG COMMODITIES >", " ", "CQG EQUITIES >", " ", "CQG ETF >", " ", "CQG FX & CRYPTO >", " ", "CQG INTEREST RATES >", " ", "CQG STOCK INDICES >" });

				nav.setNavigation(NavCol1, NavCol1_MouseDown, items.ToArray());

				nav.setNavigationLevel1(_nav1, NavCol2, NavCol2_MouseDown, go_Click);
				// Apply RBAC filter when restoring ML PORTFOLIOS view — hides TEST models from non-admin.
				filterMLPortfoliosForRole(_nav1);
				nav.setNavigationLevel2(_nav2, NavCol3, NavCol3_MouseDown, go_Click);
				nav.setNavigationLevel3(_nav2, _nav3, NavCol4, NavCol4_MouseDown);
				nav.setNavigationLevel4(_selectedNav2, _selectedNav3, _nav4, NavCol5, NavCol5_MouseDown);

				var panel = new StackPanel();
				panel.Orientation = Orientation.Horizontal;
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
				label2.Margin = new Thickness(0, 20, 0, 0);
				label2.Content = "Close";
				label2.MouseEnter += Mouse_Enter;
				label2.MouseLeave += Mouse_Leave;
				label2.MouseDown += Close_Click;
				panel.Children.Add(label2);
				NavCol1.Children.Add(panel);

				highlightButton(NavCol1, _nav1);
				highlightButton(NavCol2, _nav2);
				highlightButton(NavCol3, _nav3);
				highlightButton(NavCol4, _nav4);
				highlightButton(NavCol5, _nav5);
				highlightButton(NavCol6, _nav6);

				string resourceKey = "";

				if (resourceKey.Length > 0)
				{
					Viewbox legend = this.Resources[resourceKey] as Viewbox;
					if (legend != null)
					{
						legend.SetValue(MarginProperty, new Thickness(0, 20, 0, 0));
						NavCol1.Children.Add(legend);
					}
				}
			}
			else
			{
				showView();
			}
		}

		private void Close_Click(object sender, RoutedEventArgs e)
		{
			hideNavigation();

			loadPortfolioInfo();

			showView();
		}

		private bool _setOverlayPortfolio;
		private bool _setComparePortfolio;

		private void Portfolio2_MouseDown(object sender, MouseButtonEventArgs e)
		{
			_setOverlayPortfolio = true;
			_setComparePortfolio = false;

			if (NavCol1.Visibility == Visibility.Collapsed)
			{
				hideView();
				hideNavigation();

				PortfolioSelectionPanel.Visibility = Visibility.Visible;

				NavCol1.Visibility = Visibility.Visible;
				NavCol2.Visibility = Visibility.Visible;
				NavCol3.Visibility = Visibility.Visible;
				NavCol4.Visibility = Visibility.Visible;
				NavCol5.Visibility = Visibility.Visible;
				NavCol6.Visibility = Visibility.Visible;
				NavScroller4.Visibility = Visibility.Visible;
				NavScroller5.Visibility = Visibility.Visible;
				NavScroller6.Visibility = Visibility.Visible;

				SubgroupActionMenu1.Visibility = Visibility.Collapsed;
				SubgroupActionMenu2.Visibility = Visibility.Collapsed;

				if (MainView.EnablePortfolio)
				{
					List<string> items = new List<string>();
					nav.setNavigation(NavCol1, NavCol1_MouseDown, items.ToArray());
				}
			}
			else
			{
				showView();
			}
		}

		private void showView()
		{
			TimeFramesGrid.Visibility = (_view == "By TIME FRAME") ? Visibility.Visible : Visibility.Hidden;
			ConditionsGrid.Visibility = (_view == "By CONDITION" || _view == "By CONDITION 2") ? Visibility.Visible : Visibility.Hidden;
			Grid1.Visibility = Visibility.Visible;
			SectorGrid.Visibility = Visibility.Visible;
			BalGrid.Visibility = Visibility.Visible;
			if (_chartVisible)
			{
				ChartGrid.Visibility = Visibility.Visible;
			}
			hideNavigation();
		}

		private void hideView()
		{
			TimeFramesGrid.Visibility = Visibility.Hidden;
			ConditionsGrid.Visibility = Visibility.Hidden;
			Grid1.Visibility = Visibility.Hidden;
			SectorGrid.Visibility = Visibility.Hidden;
			BalGrid.Visibility = Visibility.Hidden;
			_chartVisible = (ChartGrid.Visibility == Visibility.Visible);
			ChartGrid.Visibility = Visibility.Hidden;
		}

		private void Help_MouseEnter(object sender, MouseEventArgs e) // change to &#x3F
		{
			var label = sender as Label;
			label.Tag = label.Foreground;
			//label.Content = "\u2630";   //was "\u003f";
			label.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xcc, 0xff));
		}

		private void Help_MouseLeave(object sender, MouseEventArgs e)  //return to &#x1392
		{
			var label = sender as Label;
			//label.Content = "\u2630";   // "\u1392";
			label.Foreground = (Brush)label.Tag;
		}

		private void Portfolio_MouseEnter(object sender, MouseEventArgs e)
		{
			var label = sender as Label;
			enterBrush = label.Foreground;
			label.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xcc, 0xff));
		}

		private void TextBlock_MouseLeave(object sender, MouseEventArgs e)
		{
			var label = sender as TextBlock;
			label.Foreground = enterBrush;
		}

		Brush enterBrush = null;
		private void TextBlock_MouseEnter(object sender, MouseEventArgs e)
		{
			var label = sender as TextBlock;
			enterBrush = label.Foreground;
			label.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xcc, 0xff));
		}

		private void Portfolio_MouseLeave2(object sender, MouseEventArgs e)
		{
			var label = sender as Label;
			label.Foreground = enterBrush;
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

		private void FundamentalML_MouseDown(object sender, MouseButtonEventArgs e)
		{
			Close();
			_mainView.Content = new PortfolioBuilder(_mainView);
		}

		private void Country_MouseEnter(object sender, MouseEventArgs e)
		{
			Label label = sender as Label;
			label.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xcc, 0xff));
		}

		private void Country_MouseLeave(object sender, MouseEventArgs e)
		{
			Label label = sender as Label;
			label.Foreground = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));
		}

		private void MarketMaps_MouseDown(object sender, MouseButtonEventArgs e)
		{
			Close();
			_mainView.Content = new MarketMonitor(_mainView);
		}

		private void Alert_MouseDown(object sender, MouseButtonEventArgs e)
		{
			Close();
			_mainView.Content = new Alerts(_mainView);
		}

		private void OurView_MouseDown(object sender, MouseButtonEventArgs e)
		{
			Close();
			_mainView.Content = new Charts(_mainView);
		}

		private void AdviceTimingHelp_MouseDown(object sender, MouseButtonEventArgs e)
		{
			ViewHelp.Visibility = Visibility.Collapsed;
		}

		private void ViewHelp_MouseDown(object sender, MouseButtonEventArgs e)
		{
			ViewHelp.Visibility = Visibility.Visible;
		}

		/// <summary>
		/// Removes TEST portfolio entries from NavCol2 when the user is not an Administrator
		/// and the current nav1 selection is "ML PORTFOLIOS >". Removes ONLY entries that
		/// positively resolve to a non-LIVE model. Entries whose names cannot be extracted
		/// or whose models cannot be resolved are left in place — they were legitimately
		/// added by nav.setNavigationLevel1 so they belong there.
		/// Safe to call unconditionally — it no-ops for other categories or for admins.
		/// </summary>
		private void filterMLPortfoliosForRole(string nav1)
		{
			//System.Diagnostics.Debug.WriteLine("[RBAC_BUILD_CHECK] Timing.filterMLPortfoliosForRole invoked");
			/*
			System.Diagnostics.Debug.WriteLine(
				$"[RBAC] START nav1='{nav1}' IsAdmin={ATMML.Auth.AuthContext.Current.IsAdmin} " +
				$"NavCol2.Count={NavCol2.Children.Count}");
			*/
			if (nav1 != "ML PORTFOLIOS >") return;
			if (ATMML.Auth.AuthContext.Current.IsAdmin) return;

			for (int i = NavCol2.Children.Count - 1; i >= 0; i--)
			{
				var child = NavCol2.Children[i] as FrameworkElement;
				if (child == null) continue;

				string symStr = (child as SymbolLabel)?.Symbol;
				string labelStr = (child as System.Windows.Controls.Label)?.Content?.ToString();
				string tbStr = (child as System.Windows.Controls.TextBlock)?.Text;
				string ccStr = (child as System.Windows.Controls.ContentControl)?.Content?.ToString();
				string name = symStr ?? labelStr ?? tbStr ?? ccStr ?? "";

				// Use ModelAccessGate.IsLive (reads models\Models\_meta) instead of
				// MainView.GetModel — the latter returns null for ML portfolios.
				bool isLive = !string.IsNullOrWhiteSpace(name) && ModelAccessGate.IsLive(name);
				bool willRemove = !string.IsNullOrWhiteSpace(name) && !isLive;

				/*
				System.Diagnostics.Debug.WriteLine(
					$"[RBAC]   idx={i} type={child.GetType().Name} sym='{symStr}' " +
					$"label='{labelStr}' tb='{tbStr}' cc='{ccStr}' -> name='{name}' " +
					$"isLive={isLive} willRemove={willRemove}");
				*/

				if (willRemove) NavCol2.Children.RemoveAt(i);
			}
			//System.Diagnostics.Debug.WriteLine($"[RBAC] END NavCol2.Count={NavCol2.Children.Count}");
		}

		private void NavCol1_MouseDown(object sender, MouseButtonEventArgs e)
		{
			NavCol2.Children.Clear();
			NavCol3.Children.Clear();
			NavCol4.Children.Clear();
			NavCol5.Children.Clear();
			NavCol6.Children.Clear();

			GroupMenu2.Children.Clear();
			SubgroupMenu2.Children.Clear();
			IndustryMenu2.Children.Clear();
			SubgroupActionMenu1.Visibility = Visibility.Collapsed;
			SubgroupActionMenu2.Visibility = Visibility.Collapsed;

			Label label = e.Source as Label;
			string nav1 = label.Content as string;

			_selectedNav1 = nav1;

			highlightButton(NavCol1, _selectedNav1);

			nav.setNavigationLevel1(_selectedNav1, NavCol2, NavCol2_MouseDown, go_Click);

			// ML PORTFOLIOS nav: only Admin sees TEST portfolios. All other roles see LIVE only.
			filterMLPortfoliosForRole(_selectedNav1);

			if (_selectedNav1 == "ALPHA PORTFOLIOS >")
			{
				var fields = _clientPortfolioName.Split(',');
				var cbs = FindVisualChildren<CheckBox>(NavCol2).ToList();
				foreach (var cb in cbs)
				{
					cb.IsChecked = fields.Contains(cb.Tag);
				}
			}
		}

		private void NavCol2_MouseDown(object sender, MouseButtonEventArgs e)
		{
			NavCol3.Children.Clear();
			NavCol4.Children.Clear();
			NavCol5.Children.Clear();
			NavCol6.Children.Clear();

			GroupMenu2.Children.Clear();
			SubgroupMenu2.Children.Clear();
			IndustryMenu2.Children.Clear();
			SubgroupActionMenu1.Visibility = Visibility.Collapsed;
			SubgroupActionMenu2.Visibility = Visibility.Collapsed;

			SymbolLabel label = e.Source as SymbolLabel;
			string nav2 = (label.Symbol == (string)label.Content) ? label.Symbol : label.Symbol + ":" + label.Content;
			if (nav2.Length == 0)
			{
				nav2 = label.Content as string;
			}

			highlightButton(NavCol2, nav2);

			if (nav.setNavigationLevel2(nav2, NavCol3, NavCol3_MouseDown, go_Click))
			{
				_selectedNav2 = nav2;
				highlightButton(NavCol3, _nav3);
			}
			else if (_compareToSelection)
			{
				requestYieldCompareMembers(nav2);

				hideNavigation();

				showView();
			}
			else
			{
				_selectedNav2 = nav2;
				_clientPortfolioName = "";
				update(_selectedNav2);
				loadPortfolioInfo();
			}
		}

		private void requestPortfolio(string name, Portfolio.PortfolioType type)
		{
			_portfolio.Clear();

			_barCache.Clear();

			_type = type;
			_portfolioRequested = 0;
			_symbols.Clear();

			_yieldPortfolio = _portfolio.IsYieldPortfolio(name);// && _memberList.Select(x => x.Ticker).Contains(_symbol);
			_yieldSpreadPortfolio = _portfolio.IsYieldSpreadPortfolio(name);
			showCharts();

			var yieldTicker = _portfolio.GetYieldTicker(name);

			//Trace.WriteLine("Request portfolio " + name + " " + type + " " + yieldTicker);

			if (yieldTicker.Length > 0)
			{
				_portfolioRequested = 1;
				requestYieldMembers(name);
			}
			else
			{
				var fields = name.Split(',');
				_portfolioRequested = fields.Length;
				foreach (var field in fields)
				{
					_portfolio.RequestSymbols(field, type, false);
				}
			}
		}

		private void requestYieldCompareMembers(string input)
		{
			string[] field = input.Split(':');
			_yieldCompare = field[1];

			_portfolio.PortfolioChanged -= new PortfolioChangedEventHandler(portfolioChanged);
			_portfolio.Close();
			_portfolio = new Portfolio(22);
			_portfolio.PortfolioChanged += new PortfolioChangedEventHandler(portfolioChanged);

			string name = _yieldCompare;
			var type = getPortfolioType(name, _clientPortfolioName);

			var yieldTicker = _portfolio.GetYieldTicker(name);

			if (yieldTicker.Length > 0)
			{
				requestYieldMembers(name);
			}
			else
			{
				_portfolio.RequestSymbols(name, type, false);
			}
		}

		private string _yieldCompare = "";

		private void NavCol3_MouseDown(object sender, MouseButtonEventArgs e)
		{
			NavCol4.Children.Clear();
			NavCol5.Children.Clear();
			NavCol6.Children.Clear();

			SymbolLabel label = e.Source as SymbolLabel;
			string nav3 = (label.Symbol == (string)label.Content) ? label.Symbol : label.Symbol + ":" + label.Content;

			highlightButton(NavCol3, nav3);

			if (nav.setNavigationLevel3(_selectedNav2, nav3, NavCol4, NavCol4_MouseDown))
			{
				_selectedNav3 = nav3;
				SubgroupActionMenu1.Visibility = Visibility.Visible;
				highlightButton(NavCol4, _nav4);
			}
			else if (_compareToSelection)
			{
				requestYieldCompareMembers(nav3);

				hideNavigation();

				showView();
			}
			else
			{
				_selectedNav3 = nav3;
				_clientPortfolioName = "";
				update(_selectedNav1, _selectedNav2, _selectedNav3, "", "", "");
				loadPortfolioInfo();
			}
		}

		private void NavCol4_MouseDown(object sender, MouseButtonEventArgs e)
		{
			NavCol5.Children.Clear();
			NavCol6.Children.Clear();

			SymbolLabel label = e.Source as SymbolLabel;
			string nav4 = (label.Symbol == (string)label.Content) ? label.Symbol : label.Symbol + ":" + label.Content;

			_selectedNav4 = nav4;

			highlightButton(NavCol4, nav4);

			if (nav.setNavigationLevel3(_selectedNav3, _selectedNav4, NavCol5, NavCol5_MouseDown))
			{
				highlightButton(NavCol5, _nav5);
			}
			else if (nav.setNavigationLevel4(_selectedNav2, _selectedNav3, _selectedNav4, NavCol5, NavCol5_MouseDown))
			{
				highlightButton(NavCol5, _nav5);
			}
			else if (_compareToSelection)
			{
				requestYieldCompareMembers(nav4);

				hideNavigation();

				showView();
			}
			else
			{
				_clientPortfolioName = "";
				update(_selectedNav1, _selectedNav2, _selectedNav3, _selectedNav4, "", "");
				loadPortfolioInfo();
			}
		}

		private void NavCol5_MouseDown(object sender, MouseButtonEventArgs e)
		{
			SymbolLabel label = e.Source as SymbolLabel;
			string nav5 = (label.Symbol == (string)label.Content) ? label.Symbol : label.Symbol + ":" + label.Content;

			_selectedNav5 = nav5;

			if (nav.setNavigationLevel4(_selectedNav3, _selectedNav4, _selectedNav5, NavCol6, NavCol6_MouseDown))
			{
				highlightButton(NavCol6, _nav6);
			}
			else if (nav.setNavigationLevel5(_selectedNav2, _selectedNav3, _selectedNav4, NavCol6, NavCol6_MouseDown))
			{
				highlightButton(NavCol6, _nav6);
			}
			else if (_compareToSelection)
			{
				string[] dataFieldNames2 = { "CURVE_TENOR_RATES" };

				requestYieldCompareMembers(nav5);

				hideNavigation();

				showView();
			}
			else
			{
				_clientPortfolioName = "";
				update(_selectedNav1, _selectedNav2, _selectedNav3, _selectedNav4, _selectedNav5, "");
				loadPortfolioInfo();
			}
		}

		private void NavCol6_MouseDown(object sender, MouseButtonEventArgs e)
		{
			SymbolLabel label = e.Source as SymbolLabel;
			string nav6 = (label.Symbol == (string)label.Content) ? label.Symbol : label.Symbol + ":" + label.Content;
			_selectedNav6 = nav6;

			_clientPortfolioName = "";
			update(_selectedNav1, _selectedNav2, _selectedNav3, _selectedNav4, _selectedNav5, _selectedNav6);
			loadPortfolioInfo();
		}

		private string getSymbol(string key)
		{
			string output = "";
			lock (_memberListLock)
			{
				foreach (var member in _memberList)
				{
					if (_yieldPortfolio)
					{
						if (key == member.Description)
						{
							output = member.Ticker;
							break;
						}
					}
					if (_yieldSpreadPortfolio)
					{
						if (key == member.Description)
						{
							output = member.Ticker;
							break;
						}
					}
					else if (key == getSym(member.Ticker))
					{
						output = member.Ticker;
						break;
					}
				}
			}

			if (output == "")
			{
				var orders = MainView.GetOrders(getPortfolioName());

				foreach (var order in orders)
				{
					string[] fields = order.Ticker.Split(' ');
					if (fields.Length >= 1 && fields[0] == key)
					{
						output = order.Ticker;
						break;
					}
				}
			}
			return output;
		}

		private string getSymbolDescription(string key)
		{
			string output = "";
			lock (_memberListLock)
			{
				foreach (var member in _memberList)
				{
					if (key == getSym(member.Ticker))
					{
						output = member.Description;
						break;
					}
				}
			}
			return output;
		}

		private void Symbol_MouseDown(object sender, MouseButtonEventArgs e)
		{
			TextBlock label = e.Source as TextBlock;
			string text = label.Tag as string;

			var fields = text.Split('\t');

			var ticker = fields[0];

			_symbol = ticker;
			_interval = (fields.Length > 1 && fields[1].Length > 0) ? fields[1] : _interval;

			adjustLabels();

			_interval1 = getInterval(_interval, 1);
			_interval2 = _interval;

			var portfolioName = getPortfolioName();
			_yieldPortfolio = _portfolio.IsYieldPortfolio(portfolioName) && _memberList.Select(x => x.Ticker).Contains(_symbol);

			_monitorData.Clear();

			if (MonitorGrid.Visibility == Visibility.Visible)
			{
				getMonitorData();
			}

			drawMonitorMemberList();
			showCharts();
			updateChart(ticker);
		}

		private void getMonitorData()
		{
			var dataFieldNames = new string[] {
				"PRICE_LAST_RT",
				"PX_LAST_ACTUAL",
				"PX_OPEN",
				"PX_HIGH",
				"PX_LOW",
				"PX_VOLUME",
				"CHG_NET_1D",
				"CHG_PCT_1D",
				"CHG_PCT_VOLUME_REG_SESSION_CLOSE",
				"BID_SIZE",
				"BID",
				"ASK",
				"ASK_SIZE",
				"TOT_ANALYST_REC",
				"TOT_BUY_REC",
				"TOT_HOLD_REC",
				"TOT_SELL_REC"
			};
			_portfolio.RequestReferenceData(_symbol, dataFieldNames, true);
		}

		private void updateChart(string symbol)
		{
			if (_chart1 != null)
			{
				var portfolioName = getPortfolioName();
				_yieldPortfolio = _portfolio.IsYieldPortfolio(portfolioName) && _memberList.Select(x => x.Ticker).Contains(symbol);
				showCharts();

				if (_view == "By CONDITION")
				{
					if (_twoCharts)
					{
						changeChart(_chart2, symbol, _interval2, getPortfolioName());
						changeChart(_chart1, symbol, _interval1, getPortfolioName());
					}
					else
					{
						changeChart(_chart2, symbol, _interval, getPortfolioName());
					}
				}
				else
				{
					changeChart(_chart2, symbol, _interval2, getPortfolioName());
					changeChart(_chart1, symbol, _interval1, getPortfolioName());
				}
			}
		}

		private bool isTradingModel(string name)
		{
			return (name == "CMR");
		}

		public static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
		{
			if (depObj != null)
			{
				for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
				{
					DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
					if (child != null && child is T)
					{
						yield return (T)child;
					}

					foreach (T childOfChild in FindVisualChildren<T>(child))
					{
						yield return childOfChild;
					}
				}
			}
		}

		void go_Click(object sender, RoutedEventArgs e)
		{
			TextBoxButton button = sender as TextBoxButton;
			string buttonName = button.Name;

			string portfolioName = "";
			if (buttonName.Contains("Model"))
			{
				var panel1 = button.Parent as StackPanel;
				var panel2 = panel1.Parent as StackPanel;
				var panel3 = panel2.Parent as StackPanel;
				var cbs = FindVisualChildren<CheckBox>(panel3).ToList();
				foreach (var cb in cbs)
				{
					if (cb.IsChecked == true)
					{
						if (portfolioName.Length > 0) portfolioName += ',';
						portfolioName += cb.Tag as string;
					}
				}
			}
			else
			{
				portfolioName = button.TextBox.Text;
			}

			if (_setComparePortfolio)
			{
				_comparePortfolioName = portfolioName;

				_comparePortfolioType = Portfolio.PortfolioType.PRTU;
				if (buttonName.Contains("EQS")) _comparePortfolioType = Portfolio.PortfolioType.EQS;
				else if (buttonName.Contains("Peers")) _comparePortfolioType = Portfolio.PortfolioType.Peers;
				else if (buttonName.Contains("Model")) _comparePortfolioType = isTradingModel(portfolioName) ? Portfolio.PortfolioType.BuiltIn : Portfolio.PortfolioType.Model;
				else if (buttonName.Contains("Alert")) _clientPortfolioType = Portfolio.PortfolioType.Alert;
				else if (buttonName.Contains("WORKSHEET")) _clientPortfolioType = Portfolio.PortfolioType.LIST;

				ComparePortfolio.Content = _comparePortfolioName;

				_updateMemberList = true;
			}
			else
			{
				_portfolio.Clear();

				_clientPortfolioName = portfolioName;

				_clientPortfolioType = Portfolio.PortfolioType.PRTU;
				if (buttonName.Contains("EQS")) _clientPortfolioType = Portfolio.PortfolioType.EQS;
				else if (buttonName.Contains("Peers")) _clientPortfolioType = Portfolio.PortfolioType.Peers;
				else if (buttonName.Contains("Model")) _clientPortfolioType = isTradingModel(portfolioName) ? Portfolio.PortfolioType.BuiltIn : Portfolio.PortfolioType.Model;
				else if (buttonName.Contains("Alert")) _clientPortfolioType = Portfolio.PortfolioType.Alert;
				else if (buttonName.Contains("WORKSHEET")) _clientPortfolioType = Portfolio.PortfolioType.LIST;

				Country.Content = _clientPortfolioName;  //"Custom";
				Country1.Content = _clientPortfolioName;  //"Custom";

				updateOverlayUI();

				SpreadsheetGroup.Content = _clientPortfolioName;

				var name = getPortfolioName();
				var type = getPortfolioType(name, _clientPortfolioName);
				requestPortfolio(name, type);
			}

			hideNavigation();

			showView();
		}

		private bool useOverlay()
		{
			return false;
		}

		private void updateOverlayUI()
		{
			bool visible = useOverlay();
			Country2.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
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

		private void hideNavigation()
		{
			PortfolioSelectionPanel.Visibility = Visibility.Collapsed;

			ViewMenu.Visibility = Visibility.Collapsed;
			LegendMenu.Visibility = Visibility.Collapsed;
			ConditionMenu.Visibility = Visibility.Collapsed;
			ModeMenu.Visibility = Visibility.Collapsed;
			NavCol1.Visibility = Visibility.Collapsed;
			NavCol2.Visibility = Visibility.Collapsed;
			NavCol3.Visibility = Visibility.Collapsed;
			GroupMenu2.Visibility = Visibility.Collapsed;
			NavScroller5.Visibility = Visibility.Collapsed;
			IndustryMenuScroller2.Visibility = Visibility.Collapsed;
			NavCol5.Visibility = Visibility.Collapsed;
			IndustryMenu2.Visibility = Visibility.Collapsed;

			ViewMenu.Children.Clear();
			LegendMenu.Children.Clear();
			ConditionMenu.Children.Clear();
			ModeMenu.Children.Clear();
			NavCol1.Children.Clear();
			NavCol2.Children.Clear();
			NavCol3.Children.Clear();
			GroupMenu2.Children.Clear();
			NavCol4.Children.Clear();
			SubgroupMenu2.Children.Clear();
			NavCol5.Children.Clear();
			IndustryMenu2.Children.Clear();
			SubgroupActionMenu1.Visibility = Visibility.Collapsed;
			SubgroupActionMenu2.Visibility = Visibility.Collapsed;
		}

		private void update(string nav2)
		{
			update(_selectedNav1, nav2, nav.getGroup(nav2), "", "", "");
		}

		class SymbolInfo
		{
			private string _symbol;
			private string _description;

			public SymbolInfo(string symbol, string description)
			{
				_symbol = symbol;
				_description = description;
			}

			public string Symbol
			{
				get { return _symbol; }
				set { _symbol = value; }
			}

			public string Description
			{
				get { return _description; }
				set { _description = value; }
			}
		}

		private void drawLine(Canvas canvas, Brush brush, double thickness, double x1, double y1, double x2, double y2)
		{
			Line colLine = new Line();
			colLine.Stroke = brush;
			colLine.StrokeThickness = thickness;
			colLine.X1 = x1;
			colLine.Y1 = y1;
			colLine.X2 = x2;
			colLine.Y2 = y2;
			canvas.Children.Add(colLine);

		}

		private void drawRectangle(Canvas canvas, Brush brush1, Brush brush2, int colCnt, int row, int col, string text = "")
		{
			double width = canvas.ActualWidth;
			if (width > 0)
			{
				double rectWidth = getColumnWidth(col) - .5;
				if (rectWidth > 0)
				{
					double rectHeight = _spreadsheetRowHeight - (2 * _spreadsheetMargin.Y) - .5;

					double rectLeft = getColumnLeft(col);
					double rectTop = row * _spreadsheetRowHeight + _spreadsheetMargin.Y;

					Rectangle rectangle = new Rectangle();
					Canvas.SetLeft(rectangle, rectLeft);
					Canvas.SetTop(rectangle, rectTop);
					rectangle.Width = rectWidth;
					rectangle.Height = rectHeight;
					rectangle.Margin = new Thickness(0, .5, 0, .5);
					rectangle.Fill = brush1;
					rectangle.Stroke = brush2;
					rectangle.StrokeThickness = 2;
					rectangle.Tag = "cell";
					canvas.Children.Add(rectangle);

					if (text.Length > 0)
					{

					}
				}
			}
		}

		int _oldProgressCount1 = 0;
		DateTime _lastProgressTime;

		Thread _predictionThread;

		private void timer_Tick(object sender, EventArgs e)
		{
			// Update Balance2 from Portfolio_Builder's live MtM every tick (200ms)
			var liveNavStr = _mainView.GetInfo("LiveNav_" + (_portfolioModelName ?? getPortfolioName()));
			if (!string.IsNullOrEmpty(liveNavStr) && double.TryParse(liveNavStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double timerLiveNav) && timerLiveNav > 0)
				Balance2.Content = "$ " + timerLiveNav.ToString("#,##0");

			// BarServer._bloombergConnectionOk is maintained live by bloombergConnectionThread
			// (sets false when sessions drop, true when openSession() succeeds).
			// Poll every 200ms and refresh alerts immediately on any state change.
			bool bbgNow = BarServer.ConnectedToBloomberg();
			if (bbgNow != _bloombergConnected)
			{
				_bloombergConnected = bbgNow;
				_alertController?.ForceRefresh();
				updateExtendedAlertCircles();
				updateCategoryCircles();
			}

			if ((DateTime.Now - _calculateTime).TotalMinutes >= 5)
			{
				calculateColors(false);
			}

			// Refresh balance, sector, and alert data every 30 seconds during market hours
			if ((DateTime.Now - _lastPortfolioInfoTime).TotalSeconds >= 30)
			{
				_lastPortfolioInfoTime = DateTime.Now;
				loadPortfolioInfo();
			}

			if ((DateTime.Now - _mainView.StartUpTime).TotalSeconds <= 10)
			{
				_updateMemberList = true;
			}

			if (_addFlash)
			{
				_addFlash = false;
				Brush alertColor = Brushes.Red;
				Flash.Manager.AddFlash(new Flash(AlertButton, AlertButton.Foreground, alertColor, 10, false));
			}

			if (_updateMonitorData)
			{
				_updateMonitorData = false;

				TicketSymbol.Content = _symbol;
				Interval1.Content = _interval;
				Interval2.Content = _interval;

				LastPx.Content = (_monitorData.ContainsKey("PRICE_LAST_RT")) ? _monitorData["PRICE_LAST_RT"] : "";
				LastTrade.Content = (_monitorData.ContainsKey("PRICE_LAST_RT")) ? _monitorData["PRICE_LAST_RT"] : "";

				OpPx.Content = (_monitorData.ContainsKey("PX_OPEN")) ? _monitorData["PX_OPEN"] : "";
				HiPx.Content = (_monitorData.ContainsKey("PX_HIGH")) ? _monitorData["PX_HIGH"] : "";
				LoPx.Content = (_monitorData.ContainsKey("PX_LOW")) ? _monitorData["PX_LOW"] : "";
				Vol.Content = (_monitorData.ContainsKey("PX_VOLUME")) ? _monitorData["PX_VOLUME"] : "";

				var chgNetText = (_monitorData.ContainsKey("CHG_NET_1D")) ? _monitorData["CHG_NET_1D"] : "";
				var chgPctText = (_monitorData.ContainsKey("CHG_PCT_1D")) ? _monitorData["CHG_PCT_1D"] : "";
				var chgNet = (chgNetText.Length > 0) ? double.Parse(chgNetText) : double.NaN;
				var chgPct = (chgNetText.Length > 0) ? double.Parse(chgPctText) : double.NaN;
				ChgLastPx.Content = (double.IsNaN(chgNet)) ? "" : Math.Abs(chgNet).ToString("0.00");
				ChgLastPx.Foreground = (chgNet >= 0) ? Brushes.Lime : Brushes.Red;
				PercentChgLastPx.Content = (double.IsNaN(chgPct)) ? "" : Math.Abs(chgPct).ToString("0.00");
				PercentChgLastPx.Foreground = (chgPct >= 0) ? Brushes.Lime : Brushes.Red;

				BidSize.Content = (_monitorData.ContainsKey("BID_SIZE")) ? _monitorData["BID_SIZE"] : ""; BidPx.Content = (_monitorData.ContainsKey("BID")) ? _monitorData["BID"] : "";
				AskPx.Content = (_monitorData.ContainsKey("ASK")) ? _monitorData["ASK"] : "";
				AskSize.Content = (_monitorData.ContainsKey("ASK_SIZE")) ? _monitorData["ASK_SIZE"] : "";
				TotalAnalyst.Content = (_monitorData.ContainsKey("TOT_ANALYST_REC")) ? _monitorData["TOT_ANALYST_REC"] : "";
				TotalBuy.Content = (_monitorData.ContainsKey("TOT_BUY_REC")) ? _monitorData["TOT_BUY_REC"] : "";
				TotalHold.Content = (_monitorData.ContainsKey("TOT_HOLD_REC")) ? _monitorData["TOT_HOLD_REC"] : "";
				TotalSell.Content = (_monitorData.ContainsKey("TOT_SELL_REC")) ? _monitorData["TOT_SELL_REC"] : "";

				var relIndex = _portfolio.GetRelativeIndex(_symbol);
				var indexTicker = relIndex;
				var indexCloses = _barCache.GetSeries(indexTicker, "Daily", new string[] { "Close" }, 0, 2);
				var tickerCloses = _barCache.GetSeries(_symbol, "Daily", new string[] { "Close" }, 0, 2);
				var pc = double.NaN;
				if (indexCloses[0].Count == 2 && tickerCloses[0].Count == 2)
				{
					var ic0 = indexCloses[0][1];
					var ic1 = indexCloses[0][0];
					var tc0 = tickerCloses[0][1];
					var tc1 = tickerCloses[0][0];
					pc = 100 * (((ic0 - ic1) / ic1) - ((tc0 - tc1) / tc1));
				}
				CltoIDX.Content = (double.IsNaN(pc)) ? "" : Math.Abs(pc).ToString("0.00");
				CltoIDX.Foreground = (pc >= 0) ? Brushes.Lime : Brushes.Red;

				var modelName = _modelName;
				var model = MainView.GetModel(modelName);
				if (model != null)
				{
					var useTicker = model.UseTicker;
					var intervals = new string[] { "30", "60", "120", "240", "Daily", "Weekly", "Monthly", "Quarterly" };
					var labels = new Label[] { Best30, Best60, Best120, Best240, BestDaily, BestWeekly, BestMonthly, BestQuarterly };
					var intervalCount = intervals.Length;
					string results = "";
					for (var ii = 0; ii < intervalCount; ii++)
					{
						var path = @"senarios\" + modelName + @"\" + intervals[ii] + (useTicker ? @"\" + _symbol : "") + @"\result.csv";
						results = MainView.LoadUserData(path);

						var rows = results.Split(':');
						var row = (rows.Length > 0) ? rows[0] : "";
						var fields = row.Split(',');
						var accuracy = (fields.Length > 1) ? fields[1] : "";
						var label = labels[ii];
						label.Foreground = (_interval == intervals[ii]) ? new SolidColorBrush(Color.FromRgb(0x00, 0xcc, 0xff)) : Brushes.White;
						label.Content = accuracy;
					}
				}

				var ago = 0;

				// current
				Result value0 = null;
				string key0 = _symbol + ":" + _interval + ":" + (ago + 1).ToString();
				lock (_atmResults)
				{
					_atmResults.TryGetValue(key0, out value0);
				}

				// future
				Result value1 = null;
				string key1 = _symbol + ":" + _interval + ":" + ago.ToString();
				lock (_atmResults)
				{
					_atmResults.TryGetValue(key1, out value1);
				}

				Pxf0Up.Visibility = (value0 != null && value0.stPxfLong == TradeState.CurrentBullish) ? Visibility.Visible : Visibility.Hidden;
				Pxf0Dn.Visibility = (value0 != null && value0.stPxfShort == TradeState.CurrentBearish) ? Visibility.Visible : Visibility.Hidden;
				Pxf1Up.Visibility = (value1 != null && value1.stPxfLong == TradeState.CurrentBullish) ? Visibility.Visible : Visibility.Hidden;
				Pxf1Dn.Visibility = (value1 != null && value1.stPxfShort == TradeState.CurrentBearish) ? Visibility.Visible : Visibility.Hidden;

			}

			lock (_atmResults)
			{
				int count = 0;
				lock (_memberListLock)
				{
					count = _memberList.Count;
				}

				int progressCount1 = _memberList.Count - _symbolsRemaining.Count; // _atmResults.Count;
				int progressCount2 = _memberList.Count; // count * 4;
				double percent = (progressCount2 == 0) ? 0 : 100.0 * progressCount1 / progressCount2;
				bool timeOut = false;

				if (_oldProgressCount1 == progressCount1 && progressCount1 > 0 && _symbolsRemaining.Count > 0)
				{
					TimeSpan timeSpan = DateTime.Now - _lastProgressTime;
					timeOut = (timeSpan.TotalSeconds > 5);
				}
				else
				{
					_lastProgressTime = DateTime.Now;
				}

				if (timeOut)
				{
					_symbolsRemaining.Clear();
					startForecasting();
				}

				bool calculating = (!timeOut && progressCount1 > 0 && progressCount1 < progressCount2);

				if (calculating) _calculateTime = DateTime.Now;

				var progressTitleVisible = (calculating || _forecasting) ? Visibility.Visible : Visibility.Hidden;
				var progressBarVisible = (calculating) ? Visibility.Visible : Visibility.Hidden;

				//ProgressTitle.Content = _forecasting ? "ML Forecast......." : "Calculating.......";
				//ProgressTitle.Visibility = progressTitleVisible;
				ProgressBarLabel.Visibility = progressBarVisible;
				ProgressBar.Visibility = progressBarVisible;


				ProgressBar.Value = percent;
				string percentText = percent.ToString("##");
				ProgressBarLabel.Text = (percentText.Length > 0) ? percent.ToString("##") + "%" : "";
				_oldProgressCount1 = progressCount1;
			}

			if (_updateSpreadsheet > 0)
			{
				_updateSpreadsheet = 0;

				highlightIntervalButton(TimeIntervals, _interval.Replace(" Min", ""));
				highlightIntervalButton(LTimeIntervals, _interval1.Replace(" Min", ""));
				highlightIntervalButton(RTimeIntervals, _interval2.Replace(" Min", ""));

				ConditionsSpreadsheet.Children.Clear();
				TimeFramesSpreadsheet.Children.Clear();

				drawOrderList();

				drawMemberList();

				var intervals = getIntervals();

				lock (_memberListLock)
				{
					foreach (var member in _memberList)
					{
						foreach (var interval in intervals)
						{
							drawCell(member, interval);
						}
					}
				}

				drawCounts();

				drawGrid();
			}

			// Drain the entire pending-row queue.
			// _updateRow was formerly a single string: if N tickers finished between two
			// 200ms timer ticks, only the last writer survived and the other N-1 rows were
			// never redrawn — which is exactly why not all rows updated on first entry and
			// a manual Refresh was needed. The queue below guarantees every completed
			// ticker gets its row redrawn, regardless of how many finish in one timer interval.
			List<string> pendingRows = null;
			lock (_updateRows)
			{
				if (_updateRows.Count > 0)
				{
					pendingRows = new List<string>();
					while (_updateRows.Count > 0)
						pendingRows.Add(_updateRows.Dequeue());
				}
			}
			if (pendingRows != null)
			{
				var intervals = getIntervals();
				bool anyDrawn = false;
				foreach (string pendingSymbol in pendingRows)
				{
					int row = -1;
					lock (_memberListLock)
					{
						row = getRowNumber(pendingSymbol);
					}
					if (row >= 0)
					{
						Member member = null;
						lock (_memberListLock)
						{
							member = _memberList[row];
						}
						removeTaggedChildren(ConditionsSpreadsheet, "cell" + row);
						removeTaggedChildren(TimeFramesSpreadsheet, "cell" + row);
						foreach (var interval in intervals)
						{
							drawCell(member, interval);
						}
						anyDrawn = true;
					}
				}
				if (anyDrawn)
					drawCounts();
			}

			if (_updateMemberList)
			{
				lock (_memberListLock)
				{
					_updateMemberList = false;
					//string portfolioName = getPortfolioName();

					//var modelSymbols = _portfolio.GetModelSymbols(DateTime.Now, _comparePortfolioName);
					//var modelDirections = modelSymbols.ToDictionary(x => x.Ticker, x => x.Size1);

					//foreach (Member symbol in _memberList)
					//{
					//    if (_clientPortfolioType != Portfolio.PortfolioType.Alert)
					//    {
					//        DateTime entryDate = default(DateTime);

					//        int size1 = 0;
					//        var openTrades = _portfolio.GetTrades(portfolioName, symbol.Ticker, DateTime.Now);
					//        if (openTrades.Count > 0)
					//        {
					//            size1 = openTrades[0].Direction;
					//            entryDate = openTrades[0].OpenDateTime;
					//        }
					//        int size2 = 0;
					//        if (_overlayPortfolioName.Length > 0)
					//        {
					//            openTrades = _portfolio.GetTrades(_overlayPortfolioName, symbol.Ticker, DateTime.Now);
					//            if (openTrades.Count > 0)
					//            {
					//                size2 = openTrades[0].Direction;
					//            }
					//        }
					//        int size3 = 0;
					//        if (_comparePortfolioName.Length > 0)
					//        {
					//            openTrades = _portfolio.GetTrades(_comparePortfolioName, symbol.Ticker, DateTime.Now);
					//            if (openTrades.Count > 0)
					//            {
					//                size3 = openTrades[0].Direction;
					//            }
					//        }

					//        //Debug.WriteLine(symbol.Ticker + " size = " + size1); "*"

					//        //DateTime entryDate = Trade.Manager.getOpenDate(portfolioName, horizon, symbol.Ticker);
					//        symbol.Size1 = size1;
					//        symbol.Time1 = entryDate;

					//        symbol.Size2 = size2;

					//        symbol.Size3 = size3;
					//    }
					//}
					//_memberList.Sort(getSort());
					_updateSpreadsheet = 3;
				}
			}
		}

		private void drawOrderList()
		{
			//OrderPanel.Children.Clear();

			string portfolioName = getPortfolioName();

			var orders = MainView.GetOrders(portfolioName);

			foreach (Order order in orders)
			{
				var grid = new Grid();

				var col0 = new ColumnDefinition();
				var col1 = new ColumnDefinition();
				var col2 = new ColumnDefinition();

				col0.Width = new GridLength(35, GridUnitType.Pixel);
				col1.Width = new GridLength(20, GridUnitType.Pixel);
				col2.Width = new GridLength(50, GridUnitType.Pixel);

				grid.ColumnDefinitions.Add(col0);
				grid.ColumnDefinitions.Add(col1);
				grid.ColumnDefinitions.Add(col2);

				Color color1 = Color.FromRgb(0xff, 0xff, 0xff);
				if (order != null)
				{
					string type = order.Type;
					if (type == "b") color1 = Colors.Lime;
					else if (type == "s") color1 = Colors.Yellow;
					else if (type == "ss") color1 = Colors.Red;
					else if (type == "bs") color1 = Colors.Orange;
				}

				string[] fields = order.Ticker.Split(' ');
				string sym = fields[0];
				string description = getSymbolDescription(sym);

				TextBlock label1 = new TextBlock();
				label1.SetValue(Grid.ColumnProperty, 0);
				label1.Text = sym;
				label1.Foreground = Brushes.White;
				label1.Background = Brushes.Transparent;
				label1.Padding = new Thickness(4);
				label1.HorizontalAlignment = HorizontalAlignment.Left;
				label1.VerticalAlignment = VerticalAlignment.Top;
				label1.FontFamily = new FontFamily("Helvetica Neue");
				label1.FontWeight = FontWeights.Normal;
				label1.FontSize = 10;

				if (MainView.EnableServerTicker)
				{
					label1.Cursor = Cursors.Hand;
					label1.MouseLeftButtonDown += Label1_MouseLeftButtonDown;
				}

				TextBlock label2 = new TextBlock();
				label2.SetValue(Grid.ColumnProperty, 1);
				label2.Cursor = Cursors.Arrow;
				label2.Text = order.Type;
				label2.Foreground = new SolidColorBrush(color1);
				label2.Background = Brushes.Transparent;
				label2.Padding = new Thickness(4);
				label2.HorizontalAlignment = HorizontalAlignment.Center;
				label2.VerticalAlignment = VerticalAlignment.Top;
				label2.FontFamily = new FontFamily("Helvetica Neue");
				label2.FontWeight = FontWeights.Normal;
				label2.FontSize = 10;

				int size = order.Size;

				TextBlock label3 = new TextBlock();
				label3.SetValue(Grid.ColumnProperty, 2);
				label3.Cursor = Cursors.Arrow;
				label3.Text = Math.Abs(size).ToString();
				label3.Foreground = Brushes.White;
				label3.Background = Brushes.Transparent;
				label3.Padding = new Thickness(4);
				label3.HorizontalAlignment = HorizontalAlignment.Right;
				label3.VerticalAlignment = VerticalAlignment.Top;
				label3.FontFamily = new FontFamily("Helvetica Neue");
				label3.FontWeight = FontWeights.Normal;
				label3.FontSize = 10;

				grid.Children.Add(label1);
				grid.Children.Add(label2);
				grid.Children.Add(label3);

				if (order.Cancelled)
				{
					Line line1 = new Line();
					line1.Stroke = Brushes.White;
					line1.StrokeThickness = 1;
					line1.SetValue(Grid.ColumnSpanProperty, 3);
					line1.X1 = 0;
					line1.Y1 = 10;
					line1.X2 = 1000;
					line1.Y2 = 10;
					grid.Children.Add(line1);
				}
			}
		}

		private void Label1_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			TextBlock label = e.Source as TextBlock;
			string key = label.Text;
			string symbol = getSymbol(key);

			showCharts();

			updateChart(symbol);
		}

		IComparer<Member> getSort()
		{
			if (_memberListSortType == 1) return new SortSymbol2();
			else if (_memberListSortType == 2) return new SortSymbol3();
			else if (_yieldPortfolio) return new SortSymbol5();
			else if (_yieldSpreadPortfolio) return new SortSymbol5();
			return new SortSymbol1();
		}

		// sort by  ticker
		public class SortSymbol1 : IComparer<Member>
		{
			public int Compare(Member x, Member y)
			{
				bool xIndex = x.Ticker.Contains(" Index");
				bool yIndex = y.Ticker.Contains(" Index");
				int output = yIndex.CompareTo(xIndex);
				if (output == 0)
				{
					output = x.Ticker.CompareTo(y.Ticker);
				}
				return output;
			}
		}

		// sort by description

		// sort by long and ticker
		public class SortSymbol2 : IComparer<Member>
		{
			public int Compare(Member x, Member y)
			{
				bool xIndex = x.Ticker.Contains(" Index");
				bool yIndex = y.Ticker.Contains(" Index");
				int output = yIndex.CompareTo(xIndex);
				if (output == 0)
				{
					output = -Math.Sign(x.Size1).CompareTo(Math.Sign(y.Size1));
					if (output == 0) output = x.Ticker.CompareTo(y.Ticker);
				}
				return output;
			}
		}

		// sort by short and ticker
		public class SortSymbol3 : IComparer<Member>
		{
			public int Compare(Member x, Member y)
			{
				bool xIndex = x.Ticker.Contains(" Index");
				bool yIndex = y.Ticker.Contains(" Index");
				int output = yIndex.CompareTo(xIndex);
				if (output == 0)
				{
					output = Math.Sign(x.Size1).CompareTo(Math.Sign(y.Size1));
					if (output == 0) output = x.Ticker.CompareTo(y.Ticker);
				}
				return output;
			}
		}

		public class SortSymbol4 : IComparer<Member>
		{
			private int _column;
			private int _type;
			private int convertValue(int input)
			{
				int output = 0;
				if (input == 4) output = 1;
				else if (input == 3) output = 2;
				else if (input == 2) output = 3;
				else if (input == 1) output = 4;
				else if (input == -1) output = -4;
				else if (input == -2) output = -3;
				else if (input == -3) output = -2;
				else if (input == -4) output = -1;
				return output;
			}

			public SortSymbol4(int column, int type = 1)
			{
				_column = column;
				// 0 sort acsending by symbol
				// 1 = sort descending by value and acsending by symbol
				// 2 = sort ascending by value and acsending by symbol
				_type = type;
			}

			public int Compare(Member x, Member y)
			{
				bool xIndex = x.Ticker.Contains(" Index");
				bool yIndex = y.Ticker.Contains(" Index");
				int output = yIndex.CompareTo(xIndex);
				if (output == 0)
				{
					if (_type != 0 && x.ConditionValues != null && y.ConditionValues != null)
					{
						var val1 = x.ConditionValues[_column];
						var val2 = y.ConditionValues[_column];
						if (_type == 3 || _type == 4)
						{
							val1 = convertValue(val1);
							val2 = convertValue(val2);
						}
						output = val1.CompareTo(val2);
						if (_type == 1 || _type == 3)
						{
							output = -output;
						}
					}
					if (output == 0)
					{
						output = x.Ticker.CompareTo(y.Ticker);
					}
				}
				return output;
			}
		}

		public class SortSymbol5 : IComparer<Member>
		{
			public int Compare(Member x, Member y)
			{
				bool xIndex = x.Ticker.Contains(" Index");
				bool yIndex = y.Ticker.Contains(" Index");
				int output = yIndex.CompareTo(xIndex);
				if (output == 0)
				{
					output = x.Description.CompareTo(y.Description);
				}
				return output;
			}
		}

		public class SortSymbol6 : IComparer<Member>
		{
			public int Compare(Member x, Member y)
			{
				var output = 0;

				int xState = x.LongTradeState;
				int yState = y.LongTradeState;
				output = yState.CompareTo(xState);

				if (output == 0)
				{
					bool xIndex = x.Ticker.Contains(" Index");
					bool yIndex = y.Ticker.Contains(" Index");
					output = yIndex.CompareTo(xIndex);
					if (output == 0)
					{
						output = x.Ticker.CompareTo(y.Ticker);
					}
				}
				return output;
			}
		}

		public class SortSymbol7 : IComparer<Member>
		{
			public int Compare(Member x, Member y)
			{
				var output = 0;

				int xState = x.ShortTradeState;
				int yState = y.ShortTradeState;
				output = yState.CompareTo(xState);

				if (output == 0)
				{
					bool xIndex = x.Ticker.Contains(" Index");
					bool yIndex = y.Ticker.Contains(" Index");
					output = yIndex.CompareTo(xIndex);
					if (output == 0)
					{
						output = x.Ticker.CompareTo(y.Ticker);
					}
				}
				return output;
			}
		}


		private int getColCount(bool visible = false)
		{
			int output = 0;

			if (_view == "By CONDITION" || _view == "By CONDITION 2")
			{
				output = 44;
				if (visible)
				{
					if (!_adviceView) output -= 6;
				}
			}
			else
			{
				output = 9;
			}
			return output;
		}

		private double getColumnWidth(int col)
		{
			double output = 0;
			if (columnVisible(col))
			{
				int colCnt1 = getColCount(true);
				var l1 = getColumnLeft(col);
				var l2 = l1;
				for (int ii = col + 1; ii < 100 && l2 == l1; ii++)
				{
					l2 = getColumnLeft(ii);
				}
				output = l2 - l1;
				if (col == (colCnt1 / 2) - 1) output -= _midMargin;
			}

			return output;
		}

		private bool columnVisible(int col)
		{
			bool output = true;
			if (col == 0)
			{
				output = false;
			}

			if (!_adviceView)
			{
				if (col == 19 || col == 20 || col == 21 || col == 23 || col == 24 || col == 25)
				{
					output = false;
				}
			}
			return output;
		}

		private double getColumnLeft(int col)
		{
			double output = 0;
			double symbolColumnWidth1 = 0;
			double symbolColumnWidth2 = 65;

			double margin = _spreadsheetMargin.X;
			if (_view == "By CONDITION" || _view == "By CONDITION 2")
			{
				int colCnt1 = getColCount(true);

				int column = 0;
				for (int ii = 0; ii <= col; ii++)
				{
					if (columnVisible(ii))
					{
						column++;
					}
				}

				double spreadSheetWidth1 = ConditionsSpreadsheet.ActualWidth - 2 * margin;
				double colWidth1 = (spreadSheetWidth1 - (symbolColumnWidth1 + symbolColumnWidth2)) / (colCnt1 - 2);
				if (column == 0) output = 0;
				else if (column <= colCnt1 / 2) output = symbolColumnWidth1 + (column - 1) * colWidth1;
				else output = (symbolColumnWidth1 + symbolColumnWidth2) + (column - 2) * colWidth1;
			}
			else
			{
				int colCnt2 = getColCount();
				double spreadSheetWidth2 = TimeFramesSpreadsheet.ActualWidth - 2 * margin;
				double colWidth2 = (spreadSheetWidth2 - symbolColumnWidth1) / (colCnt2 - 1);
				if (col == 0) output = 0;
				else output = symbolColumnWidth1 + (col - 1) * colWidth2;

			}
			return output + margin;
		}

		private void drawGrid()
		{
			var canvas = (_view == "By CONDITION" || _view == "By CONDITION 2") ? ConditionsSpreadsheet : TimeFramesSpreadsheet;
			var colCnt = getColCount();

			Color color = Color.FromRgb(0x12, 0x4b, 0x72);
			Brush brush1 = new SolidColorBrush(color);

			int rowCnt = 0;
			lock (_memberListLock)
			{
				rowCnt = _memberList.Count;
			}
			double x1 = 0;
			double x2 = canvas.ActualWidth;
			for (int row = 0; row <= rowCnt; row++)
			{
				double y = row * _spreadsheetRowHeight;
				drawLine(canvas, brush1, 1.0, x1, y, x2, y);
			}

			double y1 = 0;
			double y2 = canvas.Height;

			for (int col = 0; col <= colCnt; col++)
			{
				double x = getColumnLeft(col);
				//Debug.Write(x + " ");
				drawLine(canvas, brush1, 1.0, x, y1, x, y2);
			}
			//Debug.WriteLine("");
		}

		private void removeTaggedChildren(Canvas canvas, string tag)
		{
			List<FrameworkElement> removeList = new List<FrameworkElement>();
			foreach (FrameworkElement child in canvas.Children)
			{
				string childTag = child.Tag as string;
				if (childTag == tag)
				{
					removeList.Add(child);
				}
			}

			foreach (var item in removeList)
			{
				canvas.Children.Remove(item);
			}
		}

		private void drawMonitorMemberList()
		{
			if (MonitorGrid.Visibility == Visibility.Visible)
			{
				lock (_memberListLock)
				{
					PortfolioPanel.Children.Clear();
					TradeHorizon tradeHorizon = getTradeHorizon();
					foreach (var member in _memberList)
					{
						Grid panel = getSymbolLabel(tradeHorizon, member, true);
						if (panel != null)
						{
							PortfolioPanel.Children.Add(panel);
						}
					}
				}
			}
		}

		private void drawMemberList()
		{
			lock (_memberListLock)
			{
				double height = _memberList.Count * _spreadsheetRowHeight;

				ConditionsSpreadsheet.Height = height;
				TimeFramesSpreadsheet.Height = height;

				TradeHorizon tradeHorizon = getTradeHorizon();

				int row = 0;

				int countL = 0;
				int countS = 0;

				foreach (var member in _memberList)
				{
					int size = useOverlay() ? member.Size2 : member.Size1;
					if (size > 0) countL++;
					if (size < 0) countS++;

					Grid panel3 = getSymbolLabel(tradeHorizon, member, false);
					if (panel3 != null)
					{
						Canvas.SetLeft(panel3, getColumnLeft(22) + 2);
						Canvas.SetTop(panel3, row * _spreadsheetRowHeight);
						ConditionsSpreadsheet.Children.Add(panel3);
					}
					row++;
				}

				//Count17.Content = countL.ToString();
				//CoreBuyCount2.Content = countL.ToString();
				//CoreSellCount2.Content = countS.ToString();
			}
		}

		private string getSym(string ticker)
		{
			string sym = "";
			string[] fields = ticker.Split(Symbol.SpreadCharacter);
			string key = "";
			foreach (string field in fields)
			{
				string[] subSymbol = field.Trim().Split(' ');
				sym = (subSymbol.Length <= 3) ? subSymbol[0] : subSymbol[0] + " " + subSymbol[1];
				key += (key.Length > 0) ? " - " + sym : sym;
				break;
			}
			return key;
		}

		private string getYieldSymbol(Symbol symbol)
		{
			var output = "";

			var ticker = symbol.Ticker;
			if (ticker.Length > 0)
			{
				var weekText = Regex.Match(ticker, @"\d+[W]\s").Value;
				weekText = weekText.Replace("W", "");
				weekText = weekText.Trim();

				var monthText = Regex.Match(ticker, @"\d+[M][O]?\s").Value;
				monthText = monthText.Replace("MO", "");
				monthText = monthText.Replace("M", "");
				monthText = monthText.Trim();

				var yearText = Regex.Match(ticker, @"\d+[Y]?[R]?[T]?\s").Value;
				yearText = yearText.Replace("YR", "");
				yearText = yearText.Replace("Y", "");
				yearText = yearText.Replace("T", "");
				yearText = yearText.Trim();

				if (weekText.Length > 0 || monthText.Length > 0 || yearText.Length > 0)
				{
					output = (weekText.Length > 0) ? weekText + "W" : (monthText.Length > 0) ? monthText + "M" : yearText + "Y";
					output = output.PadLeft((weekText.Length > 0) ? 9 : ((monthText.Length > 0) ? 7 : 5));

					//Trace.WriteLine("|" + symbol.Ticker + "|" + symbol.Description + "| -> |" + output + "|");
				}
			}
			return output;
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

		private string getSymbolKey(Member symbol)
		{
			var name = getPortfolioName();
			var yieldDesription = isYieldDescription(symbol.Description);
			var useDescription = _yieldPortfolio || _portfolio.UseDescription(name) || yieldDesription;
			var yieldTicker = _portfolio.GetYieldTicker(name);
			return (yieldTicker.Length > 0) ? getYieldSymbol(symbol) : (useDescription ? symbol.Description : getSym(symbol.Ticker));
		}

		private Grid getSymbolLabel(TradeHorizon tradeHorizon, Member symbol, bool highlightSelected)
		{
			Grid panel = null;

			string symbolKey = getSymbolKey(symbol);

			if (symbolKey.Length > 0)
			{
				panel = new Grid();
				var col1 = new ColumnDefinition();
				col1.Width = new GridLength(10);
				var col2 = new ColumnDefinition();
				col2.Width = new GridLength(45);
				var col3 = new ColumnDefinition();
				col3.Width = new GridLength(10);

				panel.ColumnDefinitions.Add(col1);
				panel.ColumnDefinitions.Add(col2);
				panel.ColumnDefinitions.Add(col3);

				int size = useOverlay() ? symbol.Size2 : symbol.Size1;

				Brush color1 = new SolidColorBrush((size == 0) ? Color.FromRgb(0xff, 0xff, 0xff) : ((size == 1) ? Colors.Lime : (size == -1) ? Colors.Red : Colors.Yellow));

				//string side = (size > 0) ? " Long " + size.ToString() : ((size < 0) ? " Short " + (-size).ToString() : "");

				Brush background = Brushes.Transparent;

				bool index = false; // (symbol.Ticker.Contains(" Index"));

				//int tradeDirection = 0;
				//bool tradeOpen = false;
				//int tradeDuration = 0;

				//var trades = _portfolio.GetTrades(_comparePortfolioName, symbol.Ticker);
				//if (trades.Count > 0)
				//{
				//    var trade = trades.OrderBy(x => x.OpenDateTime).Last();
				//    tradeDirection = trade.Direction;
				//    tradeOpen = trade.IsOpen();
				//    tradeDuration = (int)Math.Round((DateTime.Now - (tradeOpen ? trade.OpenDateTime : trade.CloseDateTime)).TotalDays);
				//}

				//if (tradeDirection > 0)
				//{
				//    TextBlock indicator = new TextBlock();
				//    indicator.SetValue(Grid.ColumnProperty, 0);
				//    indicator.Text = "*";
				//    indicator.FontSize = 14;
				//    indicator.Padding = new Thickness(0, 5, 0, 0);
				//    indicator.Margin = new Thickness(2, 0, 0, 0);

				//    symbol.LongTradeState = 0;
				//    if (tradeOpen && tradeDuration < 30)
				//    {
				//        symbol.LongTradeState = 3;
				//        indicator.Foreground = Brushes.Lime;
				//    }
				//    else if (tradeOpen)
				//    {
				//        symbol.LongTradeState = 2;
				//        indicator.Foreground = Brushes.DarkGreen;
				//    }
				//    else if (!tradeOpen && tradeDuration < 30)
				//    {
				//        symbol.LongTradeState = 1;
				//        indicator.Foreground = Brushes.Yellow;
				//    }
				//    else indicator.Foreground = Brushes.Transparent;

				//    indicator.Background = Brushes.Transparent;
				//    indicator.HorizontalAlignment = HorizontalAlignment.Center;
				//    indicator.VerticalAlignment = VerticalAlignment.Center;
				//    indicator.FontFamily = new FontFamily("Helvetica Neue");
				//    indicator.FontWeight = FontWeights.Bold;
				//    panel.Children.Add(indicator);
				//}
				//else if (tradeDirection < 0)
				//{
				//    TextBlock indicator = new TextBlock();
				//    indicator.SetValue(Grid.ColumnProperty, 2);
				//    indicator.Text = "*";
				//    indicator.FontSize = 14;
				//    indicator.Padding = new Thickness(0, 5, 0, 0);
				//    indicator.Margin = new Thickness(0, 0, 0, 0);

				//    symbol.ShortTradeState = 0;
				//    if (tradeOpen && tradeDuration < 30)
				//    {
				//        symbol.ShortTradeState = 3;
				//        indicator.Foreground = Brushes.Red;
				//    }
				//    else if (tradeOpen)
				//    {
				//        symbol.ShortTradeState = 2;
				//        indicator.Foreground = Brushes.DarkRed;
				//    }
				//    else if (!tradeOpen && tradeDuration < 30)
				//    {
				//        symbol.ShortTradeState = 1;
				//        indicator.Foreground = Brushes.Yellow;
				//    }
				//    else indicator.Foreground = Brushes.Transparent;

				//    indicator.Background = Brushes.Transparent;
				//    indicator.HorizontalAlignment = HorizontalAlignment.Left;
				//    indicator.VerticalAlignment = VerticalAlignment.Center;
				//    indicator.FontFamily = new FontFamily("Helvetica Neue");
				//    indicator.FontWeight = FontWeights.Bold;
				//    panel.Children.Add(indicator);
				//}

				TextBlock label1 = new TextBlock();
				label1.SetValue(Grid.ColumnProperty, 1);
				label1.Text = symbolKey;
				label1.Foreground = (highlightSelected && symbol.Ticker == _symbol) ? new SolidColorBrush(Color.FromRgb(0x00, 0xcc, 0xff)) : (index ? Brushes.DeepSkyBlue : color1);
				label1.Background = background;
				label1.Padding = new Thickness(0, 3, 0, 0);
				label1.Margin = new Thickness(0, 0, 5, 0);
				label1.HorizontalAlignment = HorizontalAlignment.Center;
				label1.VerticalAlignment = VerticalAlignment.Center;
				label1.FontFamily = new FontFamily("Helvetica Neue");
				label1.FontWeight = FontWeights.Normal;
				label1.FontSize = 9;
				label1.Height = _spreadsheetRowHeight;
				label1.Tag = symbol.Ticker + "\t" + symbol.Interval;
				label1.MouseEnter += TextBlock_MouseEnter;
				label1.MouseLeave += TextBlock_MouseLeave;

				//if (MainView.EnablePortfolio)
				{
					label1.Cursor = Cursors.Hand;
					label1.MouseDown += Symbol_MouseDown;
				}

				panel.Tag = symbolKey;
				panel.Children.Add(label1);

				// Yellow ● in col 0 for longs > 10% NAV, col 2 for shorts > 10% NAV
				double posWeight = 0;
				lock (_positionWeights) { _positionWeights.TryGetValue(symbol.Ticker, out posWeight); }
				if (posWeight > _limitSymbolWeight)
				{
					TextBlock circle = new TextBlock();
					circle.Text = "●";
					circle.FontSize = 7;
					circle.Foreground = Brushes.Yellow;
					circle.VerticalAlignment = VerticalAlignment.Center;
					circle.HorizontalAlignment = HorizontalAlignment.Center;
					circle.Padding = new Thickness(0, 2, 0, 0);
					circle.SetValue(Grid.ColumnProperty, size > 0 ? 0 : 2);
					panel.Children.Add(circle);
				}
			}
			return panel;
		}

		private double getYears(Symbol symbol)
		{
			double output = double.NaN;
			var ticker = symbol.Ticker;
			if (ticker.Length > 0)
			{
				var weekText = Regex.Match(ticker, @"\d+[W]\s").Value;
				weekText = weekText.Replace("W", "");
				weekText = weekText.Trim();

				var monthText = Regex.Match(ticker, @"\d+[M][O]?\s").Value;
				monthText = monthText.Replace("MO", "");
				monthText = monthText.Replace("M", "");
				monthText = monthText.Trim();

				var yearText = Regex.Match(ticker, @"\d+[Y]?[R]?[T]?\s").Value;
				yearText = yearText.Replace("YR", "");
				yearText = yearText.Replace("Y", "");
				yearText = yearText.Replace("T", "");
				yearText = yearText.Trim();

				if (weekText.Length > 0 || monthText.Length > 0 || yearText.Length > 0)
				{
					output = (weekText.Length > 0) ? int.Parse(weekText) / 52.0 : (monthText.Length > 0) ? int.Parse(monthText) / 12.0 : int.Parse(yearText);
				}
			}
			return output;
		}

		bool _trainModel = false;

		private void adjustLabels()
		{
			var st = getInterval(_interval);
			var mt = getInterval(getInterval(_interval, 1));
			var lt = getInterval(getInterval(_interval, 2));

			//TSB LONG
			ltTSBLong.Content = lt;
			mtTSBLong.Content = mt;
			stTSBLong.Content = st;

			//ST BULLISH
			ltSTtUp.Content = lt;
			mtSTtUp.Content = mt;
			stSTtUp.Content = st;

			//FT UP
			ltFTUp.Content = lt;
			mtFTUp.Content = mt;
			stFTUp.Content = st;

			//PXF LONG
			//stPxfLong1.Content = st;


			//TSB SHORT
			ltTSBShort.Content = lt;
			mtTSBShort.Content = mt;
			stTSBShort.Content = st;

			//ST BEARISH
			ltSTtDn.Content = lt;
			mtSTtDn.Content = mt;
			stSTtDn.Content = st;

			//FT DN
			ltFTDn.Content = lt;
			mtFTDn.Content = mt;
			stFTDn.Content = st;

			//PXF SHORT
			//stPxfShort1.Content = st;

			XSigTime1.Content = st;
			XSigTime2.Content = st;

			highlightIntervalButton(TimeIntervals, _interval.Replace(" Min", ""));
		}

		private void barChanged(object sender, BarEventArgs e)
		{
			// NOTE: barChanged fires for cache hits as well as live Bloomberg data,
			// so it cannot be used to detect Bloomberg connectivity. Bloomberg state
			// is tracked separately via _bloombergConnected in timer_Tick.

			if (e.Type == BarEventArgs.EventType.BarsReceived)
			{
				List<string> intervals = getIntervals();
				string interval = e.Interval;

				string ticker = e.Ticker;

				if (_indexBarRequestCount > 0)
				{
					Series[] indexSeries = GetSeries(ticker, interval, new string[] { "Close" }, 0, 200);
					if (indexSeries.Length > 0 && indexSeries[0].Count > 0)
					{
						lock (_referenceData)
						{
							_referenceData[ticker + " : " + interval] = indexSeries[0];
						}
					}

					lock (_indexBarRequests)
					{
						_indexBarRequests.Remove(ticker + ":" + interval);

						//						if (_indexBarRequestCount <= 4)
						//						{
						foreach (var key in _indexBarRequests)
						{
							//							}
						}
					}

					if (--_indexBarRequestCount == 0)
					{
						requestSymbolBars();
						//displayChart();
					}
				}
				else if (intervals.Contains(interval))
				{
					bool ok1 = false;
					lock (_memberListLock)
					{
						ok1 = (_memberList.FirstOrDefault(o => o.Ticker == ticker) != null);
					}

					bool ok2 = false;
					bool startPrediction = false;
					lock (_symbolsRemaining)
					{
						if (_symbolsRemaining.Count > 0)
						{
							int count = (_symbolsRemaining.ContainsKey(ticker)) ? _symbolsRemaining[ticker] - 1 : 0;
							if (count > 0)
							{
								_symbolsRemaining[ticker] = count;
							}
							else
							{
								_symbolsRemaining.Remove(ticker);
								ok2 = true;
								startPrediction = (_symbolsRemaining.Count == 0);
							}
						}
					}

					if (ok1 && ok2)
					{
						lock (_calcRequest)
						{
							_calcRequest.Enqueue(ticker);
						}

						//
						if (startPrediction)
						{
							startPrediction = false;
							startForecasting();
						}
					}
				}
			}
		}

		bool _calcRunning = true;
		Thread _calcThread = null;
		Queue<string> _calcRequest = new Queue<string>();

		private void startForecasting()
		{
			//if (MainView.EnablePortfolio)
			{
				_predictionThread = new Thread(predict);
				_predictionThread.Start();
			}
		}

		bool _forecasting = false;
		bool _forecastRunning = false;

		private void predict()
		{
			_forecasting = true;

			var interval = _interval;
			var tickers = _memberList.Select(x => x.Ticker).ToList();

			_forecastRunning = true;

			var model = MainView.GetModel(_modelName);

			if (model != null)
			{
				var predictionAgoCount = 5;

				if (model.UseTicker)
				{
					foreach (var ticker in tickers)
					{
						var pathName1 = @"senarios\" + model.Name + @"\" + interval + @"\" + MainView.ToPath(ticker);
						var data1 = atm.getModelData(model.FeatureNames, model.Scenario, _barCache, (new string[] { ticker }).ToList(), interval, predictionAgoCount, model.MLSplit, false, false);
						atm.saveModelData(pathName1 + @"\test.csv", data1);
						var predictions = MainView.AutoMLPredict(pathName1, MainView.GetSenarioLabel(model.Scenario));
						createPredictions(predictions);
					}
				}
				else
				{
					var pathName2 = @"senarios\" + model.Name + @"\" + interval;
					var data2 = atm.getModelData(model.FeatureNames, model.Scenario, _barCache, tickers, interval, predictionAgoCount, model.MLSplit, false, true);
					atm.saveModelData(pathName2 + @"\test.csv", data2);
					var predictions = MainView.AutoMLPredict(pathName2, MainView.GetSenarioLabel(model.Scenario));
					createPredictions(predictions);
				}
			}

			_forecasting = false;
			_updateMonitorData = true;
		}

		private void clearForecastCells()
		{
			lock (_atmResults)
			{
				foreach (var member in _memberList)
				{
					string ticker = member.Ticker;
					for (var ago = -1; ago < 4; ago++)
					{
						string atmKey = ticker + ":" + _interval + ":" + ago;
						if (_atmResults.ContainsKey(atmKey))
						{
							_atmResults[atmKey].stPxfLong = TradeState.Off;
							_atmResults[atmKey].stPxfShort = TradeState.Off;
						}
					}
				}
			}
			_updateSpreadsheet = 4;
		}

		private void createPredictions(Dictionary<string, Dictionary<string, Dictionary<DateTime, double>>> input)
		{
			string stInterval = _interval;
			string mtInterval = getInterval(stInterval, 1);
			string ltInterval = getInterval(mtInterval, 1);

			foreach (var kvp1 in input)
			{
				string ticker = kvp1.Key;
				var intervals = kvp1.Value;

				foreach (var kvp2 in intervals)
				{
					string interval = kvp2.Key;
					var predictions = kvp2.Value;

					var direction = getPriceDirection(ticker, interval);
					lock (_atmResults)
					{

						foreach (var kvp3 in predictions)
						{
							int ago = getBarsAgo(ticker, interval, kvp3.Key);
							bool prediction = (kvp3.Value >= 0.5);

							string atmKey = ticker + ":" + stInterval + ":" + (ago - 1);

							if (interval == ltInterval)
							{
								if (_atmResults.ContainsKey(atmKey))
								{

									_atmResults[atmKey].ltPxfLong = (prediction) ? TradeState.CurrentBullish : TradeState.Off;
									_atmResults[atmKey].ltPxfShort = (prediction) ? TradeState.Off : TradeState.CurrentBearish;
								}

								for (int ago1 = 0; ago1 < 4; ago1++)
								{
									var dir = direction[direction.Count - 1 - ago];
									if (ago > ago1 && !double.IsNaN(dir))
									{
										string atmKey1 = ticker + ":" + stInterval + ":" + (ago1 - 1);
										if (_atmResults.ContainsKey(atmKey1))
										{
											_atmResults[atmKey1].ltTotalCount++;
											if ((dir >= 0 && prediction) || (dir <= 0 && !prediction))
											{
												_atmResults[atmKey1].ltCorrectCount++;
											}
										}
									}
								}
							}
							else if (interval == mtInterval)
							{
								if (_atmResults.ContainsKey(atmKey))
								{
									_atmResults[atmKey].mtPxfLong = (prediction) ? TradeState.CurrentBullish : TradeState.Off;
									_atmResults[atmKey].mtPxfShort = (prediction) ? TradeState.Off : TradeState.CurrentBearish;
								}

								for (int ago1 = 0; ago1 < 4; ago1++)
								{
									var dir = direction[direction.Count - 1 - ago];
									if (ago > ago1 && !double.IsNaN(dir))
									{
										string atmKey1 = ticker + ":" + stInterval + ":" + (ago1 - 1);
										if (_atmResults.ContainsKey(atmKey1))
										{
											_atmResults[atmKey1].mtTotalCount++;
											if ((dir >= 0 && prediction) || (dir <= 0 && !prediction))
											{
												_atmResults[atmKey1].mtCorrectCount++;
											}
										}
									}
								}
							}
							else if (interval == stInterval)
							{
								if (_atmResults.ContainsKey(atmKey))
								{
									_atmResults[atmKey].stPxfLong = (prediction) ? TradeState.CurrentBullish : TradeState.Off;
									_atmResults[atmKey].stPxfShort = (prediction) ? TradeState.Off : TradeState.CurrentBearish;
								}


								for (int ago1 = 0; ago1 < 4; ago1++)
								{
									var dir = direction[direction.Count - 1 - ago];
									if (ago > ago1 && !double.IsNaN(dir))
									{
										string atmKey1 = ticker + ":" + stInterval + ":" + (ago1 - 1);
										if (_atmResults.ContainsKey(atmKey1))
										{
											_atmResults[atmKey1].stTotalCount++;
											bool correct = ((dir >= 0 && prediction) || (dir <= 0 && !prediction));
											if (correct)
											{
												_atmResults[atmKey1].stCorrectCount++;
											}
										}
									}
								}
							}
						}
					}
				}
			}

			_updateSpreadsheet = 5;
		}

		private Series getPriceDirection(string ticker, string interval)
		{
			Series output = null;
			var series = GetSeries(ticker, interval, new string[] { "Close" }, 0, 200);
			if (series != null && series.Length > 0)
			{
				var close1 = series[0];
				var close2 = close1.ShiftLeft(1);
				output = (close2 > close1) - (close2 < close1);

				//if (ticker == "KRE US Equity" && interval == "Daily")
				//{
				//    int barCount = close1.Count;
				//    for (int ago = 10; ago > 0; ago--)
				//    {
				//        Debug.WriteLine(ticker + " " + interval + " " + ago + " " + close1[barCount - 1 - ago] + " " + close2[barCount - 1 - ago] + " " + output[barCount - 1 - ago]);
				//    }
				//}
			}
			return output;
		}

		private Dictionary<string, int> _symbolsRemaining = new Dictionary<string, int>();

		private int getBarsAgo(string symbol, string interval, DateTime time)
		{
			int output = 0;
			var times = _barCache.GetTimes(symbol, interval, 0);
			for (int ii = times.Count - 1; ii >= 0; ii--)
			{
				if (time == times[ii]) break;
				output++;
			}
			return output;
		}

		private void calc()
		{
			while (_calcRunning)
			{
				string ticker = null;
				lock (_calcRequest)
				{
					if (_calcRequest.Count > 0)
					{
						ticker = _calcRequest.Dequeue();
					}
				}
				if (ticker != null)
				{
					calculateConditions(ticker);
				}
				Thread.Sleep(10);
			}
		}

		private void calculateConditions(string symbol)
		{
			var intervals = getIntervals();

			bool ok = true;
			Dictionary<string, List<DateTime>> times = new Dictionary<string, List<DateTime>>();
			Dictionary<string, Series[]> bars = new Dictionary<string, Series[]>();
			foreach (string interval in intervals)
			{
				times[interval] = (_barCache.GetTimes(symbol, interval, 0, 200));
				bars[interval] = GetSeries(symbol, interval, new string[] { "Open", "High", "Low", "Close" }, 0, 200);

				if (times[interval] == null || times[interval].Count == 0)
				{
					ok = false;
					break;
				}

				int count = times[intervals[0]].Count;
				var dt = times[intervals[0]][count - 1].ToString("yyyyMMdd HH:mm");
				var h0 = bars[intervals[0]][1][count - 1].ToString("####.00").PadLeft(7);
				var h1 = bars[intervals[0]][1][count - 2].ToString("####.00").PadLeft(7);
				var l0 = bars[intervals[0]][2][count - 1].ToString("####.00").PadLeft(7);
				var l1 = bars[intervals[0]][2][count - 2].ToString("####.00").PadLeft(7);
				var c0 = bars[intervals[0]][3][count - 1].ToString("####.00").PadLeft(7);
				var c1 = bars[intervals[0]][3][count - 2].ToString("####.00").PadLeft(7);
				var sym = symbol;
				sym = sym.PadLeft(20);
				var intervalText = interval;
				intervalText = intervalText.PadLeft(12);
				//Debug.WriteLine(
				// "Calculate: " + sym + " " + intervalText + " " + dt + " " +
				// "   hi " + h1 + " " + h0 +
				// "   lo " + l1 + " " + l0 +
				// "   cl " + c1 + " " + c0);
			}

			if (ok)
			{
				Series PR_st = calculatePositionRatio(symbol, intervals[0], 200);
				Series PR_mt = calculatePositionRatio(symbol, intervals[1], 200);
				Series PR_lt = calculatePositionRatio(symbol, intervals[2], 200);

				Series TSB = calculateTSBSig(symbol, intervals[0], 200);

				Series PXF = null;    // not used 
				Series PXFlt = null;
				Series PXFmt = null;
				Series PXFst = null;
				Series PXFtOBOS = null;
				Series PXSTFtOBOS = null;
				Series PXLTFtOBOS = null;

				Series stSTHigher = null;
				Series mtSTHigher = null;
				Series ltSTHigher = null;

				Series stSTLower = null;
				Series mtSTLower = null;
				Series ltSTLower = null;

				Series stSTOB = null;
				Series mtSTOB = null;
				Series ltSTOB = null;

				Series stSTOS = null;
				Series mtSTOS = null;
				Series ltSTOS = null;

				Series TBarsSTUp = null;
				Series TBarsSTDn = null;

				Series TSBlt = null;
				Series TSBmt = null;
				Series TSBst = null;

				Series STFTTurnsUp = null;
				Series STFTTurnsDn = null;

				Series MTFTTurnsUp = null;
				Series MTFTTurnsDn = null;

				Series LTFTTurnsUp = null;
				Series LTFTTurnsDn = null;

				Series STSTTurnsUp = null;
				Series STSTTurnsDn = null;

				Series MTSTTurnsUp = null;
				Series MTSTTurnsDn = null;

				Series LTSTTurnsUp = null;
				Series LTSTTurnsDn = null;

				// sft 
				Series SFT = null;

				// ST 
				Series ST = null;

				// col 11
				Series PA = null;
				Series TB = null;  // 2 bar
				Series SA = null;  // Score Alert
				Series EXH = null;  //exh 

				Series stTBarUp = null;  // Trend Bars  ST
				Series stTBarDn = null;
				Series mtTBarUp = null;  // Trend Bars  MT
				Series mtTBarDn = null;
				Series ltTBarUp = null;  // Trend Bars  LT
				Series ltTBarDn = null;

				Series stTLineUp = null;  // Trend Lines ST
				Series stTLineDn = null;
				Series mtTLineUp = null;  // Trend Lines MT
				Series mtTLineDn = null;
				Series ltTLineUp = null;  // Trend Lines LT
				Series ltTLineDn = null;

				object obj;
				lock (_referenceData)
				{
					if (_referenceData.TryGetValue(symbol + ":" + "REL_INDEX", out obj))
					{
						string indexSymbol = obj as string;
						if (_referenceData.TryGetValue(indexSymbol + " : " + intervals[0], out obj))
						{
							Series indexSeries = obj as Series;
							_referenceData["Index Prices : " + intervals[0]] = indexSeries;
						}
					}
				}

				// not used 
				PXF = atm.getPxf(times, bars, intervals.ToArray());  // ST MT FT up up up dn dn dn 

				//PXLTFtOBOS = atm.getFTOBOS(times, bars, intervals[2]); // FT OS or OB  LT

				//PXFtOBOS = atm.getFTOBOS(times, bars, intervals[1]); // FT OS or OB  MT

				// Trend 
				PXSTFtOBOS = atm.getFTOBOS(times, bars, intervals[0]); // FT OS or OB  ST

				PXLTFtOBOS = atm.getFTOBOS(times, bars, intervals[2]); // FT OS or OB  LT

				PXFtOBOS = atm.getFTOBOS(times, bars, intervals[1]); // FT OS or OB  MT

				//stSTHigher = atm.getSThigherFT(times, bars, intervals[0]);
				//mtSTHigher = atm.getSThigherFT(times, bars, intervals[0]);
				//ltSTHigher = atm.getSThigherFT(times, bars, intervals[0]);

				//stSTLower = atm.getSTlowerFT(times, bars, intervals[0]);
				//mtSTLower = atm.getSTlowerFT(times, bars, intervals[0]);
				//ltSTLower = atm.getSTlowerFT(times, bars, intervals[0]);

				stSTOB = atm.getSTOB(times, bars, intervals[0]);
				mtSTOB = atm.getSTOB(times, bars, intervals[1]);
				ltSTOB = atm.getSTOB(times, bars, intervals[2]);

				stSTOS = atm.getSTOS(times, bars, intervals[0]);
				mtSTOS = atm.getSTOS(times, bars, intervals[1]);
				ltSTOS = atm.getSTOS(times, bars, intervals[2]);

				PXFst = atm.getPxf(times, bars, intervals[0]); // ST FT up dn Short Term

				// Trend 
				PXFmt = atm.getPxf(times, bars, intervals[1]); // MT FT up dn Mid Term

				// Trend 
				PXFlt = atm.getPxf(times, bars, intervals[2]); // LT FT up dn  Long Term

				TSBst = calculateTSBSig(symbol, intervals[0], 200);
				TSBmt = calculateTSBSig(symbol, intervals[1], 200);
				TSBlt = calculateTSBSig(symbol, intervals[2], 200);

				// FTST 
				Series ft = atm.calculateFT(bars[intervals[0]][1], bars[intervals[0]][2], bars[intervals[0]][3]);
				Series st = atm.calculateST(bars[intervals[0]][1], bars[intervals[0]][2], bars[intervals[0]][3]);
				//SFT = (atm.turnsUpAboveLevel(ft, 0) - atm.turnsDnBelowLevel(ft, 100)) + ((atm.turnsUpAboveLevel(st, 0) - atm.turnsDnBelowLevel(st, 100)));  // FT or ST Up Dn
				SFT = (atm.turnsUpAboveLevel(ft, 0) - atm.turnsDnBelowLevel(ft, 100));  // FT Up Dn


				// ST Turns Up Dn
				//ST = ((atm.turnsUpAboveLevel(st, 0) - atm.turnsDnBelowLevel(st, 100)));  // ST Up Dn


				// TrendBars  ST
				Series TrendBarsST = atm.calculateTrendBars(bars[intervals[0]][1], bars[intervals[0]][2], bars[intervals[0]][3]);
				Series SThi = bars[intervals[0]][1];
				Series STlo = bars[intervals[0]][2];
				Series STcl = bars[intervals[0]][3];
				List<Series> stTDI = atm.calculateTDI(SThi, STlo);
				stTBarUp = (STlo > stTDI[1]).And(STcl > stTDI[0]).Replace(double.NaN, 0);
				stTBarDn = (SThi < stTDI[0]).And(STcl < stTDI[1]).Replace(double.NaN, 0);

				// TrendBars  MT
				Series TrendBarsMT = atm.calculateTrendBars(bars[intervals[1]][1], bars[intervals[1]][2], bars[intervals[1]][3]);
				Series MThi = bars[intervals[1]][1];
				Series MTlo = bars[intervals[1]][2];
				Series MTcl = bars[intervals[1]][3];
				List<Series> mtTDI = atm.calculateTDI(MThi, MTlo);
				mtTBarUp = (MTlo > mtTDI[1]).And(MTcl > mtTDI[0]).Replace(double.NaN, 0);
				mtTBarDn = (MThi < mtTDI[0]).And(MTcl < mtTDI[1]).Replace(double.NaN, 0);

				// TrendBars  LT
				Series TrendBarsLT = atm.calculateTrendBars(bars[intervals[2]][1], bars[intervals[2]][2], bars[intervals[2]][3]);
				Series LThi = bars[intervals[2]][1];
				Series LTlo = bars[intervals[2]][2];
				Series LTcl = bars[intervals[2]][3];
				List<Series> ltTDI = atm.calculateTDI(LThi, LTlo);
				ltTBarUp = (LTlo > ltTDI[1]).And(LTcl > ltTDI[0]).Replace(double.NaN, 0);
				ltTBarDn = (LThi < ltTDI[0]).And(LTcl < ltTDI[1]).Replace(double.NaN, 0);


				// TrendLines ST
				List<Series> TLST = atm.calculateTrendLines(bars[intervals[0]][1], bars[intervals[0]][2], bars[intervals[0]][3], 2);
				Series STtl_up = atm.hasVal(TLST[0]);
				Series STnot_tl_up = atm.nothasVal(TLST[0]);
				Series STtl_dn = atm.hasVal(TLST[1]);
				Series STnot_tl_dn = atm.notHasVal(TLST[1]);
				stTLineUp = (STtl_up).And(STnot_tl_dn);
				stTLineDn = (STtl_dn).And(STnot_tl_up);

				// TrendLines MT
				List<Series> TLMT = atm.calculateTrendLines(bars[intervals[1]][1], bars[intervals[1]][2], bars[intervals[1]][3], 2);
				Series MTtl_up = atm.hasVal(TLMT[0]);
				Series MTnot_tl_up = atm.nothasVal(TLMT[0]);
				Series MTtl_dn = atm.hasVal(TLMT[1]);
				Series MTnot_tl_dn = atm.notHasVal(TLMT[1]);
				mtTLineUp = (MTtl_up).And(MTnot_tl_dn);
				mtTLineDn = (MTtl_dn).And(MTnot_tl_up);

				// TrendLines LT
				List<Series> TLLT = atm.calculateTrendLines(bars[intervals[2]][1], bars[intervals[2]][2], bars[intervals[2]][3], 2);
				Series LTtl_up = atm.hasVal(TLLT[0]);
				Series LTnot_tl_up = atm.nothasVal(TLLT[0]);
				Series LTtl_dn = atm.hasVal(TLLT[1]);
				Series LTnot_tl_dn = atm.notHasVal(TLLT[1]);
				ltTLineUp = (LTtl_up).And(LTnot_tl_dn);
				ltTLineDn = (LTtl_dn).And(LTnot_tl_up);

				var count = SFT.Count;
				var dt = times[intervals[0]][count - 1].ToString("yyyyMMdd HH:mm");
				var h0 = bars[intervals[0]][1][count - 1].ToString("####.00").PadLeft(7);
				var h1 = bars[intervals[0]][1][count - 2].ToString("####.00").PadLeft(7);
				var l0 = bars[intervals[0]][2][count - 1].ToString("####.00").PadLeft(7);
				var l1 = bars[intervals[0]][2][count - 2].ToString("####.00").PadLeft(7);
				var c0 = bars[intervals[0]][3][count - 1].ToString("####.00").PadLeft(7);
				var c1 = bars[intervals[0]][3][count - 2].ToString("####.00").PadLeft(7);
				var f0 = ft[count - 1].ToString("####.00").PadLeft(5);
				var f1 = ft[count - 2].ToString("####.00").PadLeft(5);
				var f2 = ft[count - 3].ToString("####.00").PadLeft(5);
				var s0 = st[count - 1].ToString("####.00").PadLeft(5);
				var s1 = st[count - 2].ToString("####.00").PadLeft(5);
				var s2 = st[count - 3].ToString("####.00").PadLeft(5);
				var ftu = atm.turnsUpAboveLevel(ft, 0)[count - 1];
				var ftd = atm.turnsDnBelowLevel(ft, 100)[count - 1];
				var stu = atm.turnsUpAboveLevel(st, 0)[count - 1];
				var std = atm.turnsDnBelowLevel(st, 100)[count - 1];
				var sym = symbol;
				sym = sym.PadLeft(20);
				var intervalText = intervals[0];
				intervalText = intervalText.PadLeft(12);

				//var count1 = STTurns.Count;
				//var dt1 = times[intervals[0]][count - 1].ToString("yyyyMMdd HH:mm");
				//var h01 = bars[intervals[0]][1][count - 1].ToString("####.00").PadLeft(7);
				//var h11 = bars[intervals[0]][1][count - 2].ToString("####.00").PadLeft(7);
				//var l01 = bars[intervals[0]][2][count - 1].ToString("####.00").PadLeft(7);
				//var l11 = bars[intervals[0]][2][count - 2].ToString("####.00").PadLeft(7);
				//var c01 = bars[intervals[0]][3][count - 1].ToString("####.00").PadLeft(7);
				//var c11 = bars[intervals[0]][3][count - 2].ToString("####.00").PadLeft(7);
				//var s01 = st[count - 1].ToString("####.00").PadLeft(5);
				//var s11 = st[count - 2].ToString("####.00").PadLeft(5);
				//var s21 = st[count - 3].ToString("####.00").PadLeft(5);
				//var stu1 = atm.turnsUpAboveLevel(st, 0)[count - 1];
				//var std1 = atm.turnsDnBelowLevel(st, 100)[count - 1];
				//var sym1 = symbol;
				//sym = sym.PadLeft(20);
				//var intervalText1 = intervals[0];
				//intervalText1 = intervalText1.PadLeft(12);

				//Debug.WriteLine(
				//"SFT      : " + sym + " " + intervalText + " " + dt + " " +
				//"   FTTurn u = " + ftu + " d = " + ftd +
				//"   STTurn u = " + stu + " d = " + std +
				//"   FT " + f2 + " " + f1 + " " + f0 +
				//"   ST " + s2 + " " + s1 + " " + s0 +
				//"   hi " + h1 + " " + h0 +
				//"   lo " + l1 + " " + l0 +
				//"   cl " + c1 + " " + c0);

				Series stft = atm.calculateFT(bars[intervals[0]][1], bars[intervals[0]][2], bars[intervals[0]][3]);
				STFTTurnsUp = atm.turnsUpAboveLevel(stft, 0);
				STFTTurnsDn = atm.turnsDnBelowLevel(stft, 100);

				Series mtft = atm.calculateFT(bars[intervals[1]][1], bars[intervals[1]][2], bars[intervals[1]][3]);
				MTFTTurnsUp = atm.turnsUpAboveLevel(mtft, 0);
				MTFTTurnsDn = atm.turnsDnBelowLevel(mtft, 100);

				Series ltft = atm.calculateFT(bars[intervals[2]][1], bars[intervals[2]][2], bars[intervals[2]][3]);
				LTFTTurnsUp = atm.turnsUpAboveLevel(ltft, 0);
				LTFTTurnsDn = atm.turnsDnBelowLevel(ltft, 100);

				Series stst = atm.calculateST(bars[intervals[0]][1], bars[intervals[0]][2], bars[intervals[0]][3]);
				STSTTurnsUp = atm.turnsUpAboveLevel(stst, 0);
				STSTTurnsDn = atm.turnsDnBelowLevel(stst, 100);

				Series mtst = atm.calculateST(bars[intervals[1]][1], bars[intervals[1]][2], bars[intervals[1]][3]);
				MTSTTurnsUp = atm.turnsUpAboveLevel(mtst, 0);
				MTSTTurnsDn = atm.turnsDnBelowLevel(mtst, 100);

				Series ltst = atm.calculateST(bars[intervals[2]][1], bars[intervals[2]][2], bars[intervals[2]][3]);
				LTSTTurnsUp = atm.turnsUpAboveLevel(ltst, 0);
				LTSTTurnsDn = atm.turnsDnBelowLevel(ltst, 100);


				Series FT_st = (PR_st > 1) - (PR_st < -1);
				Series FT_mt = (PR_mt > 1) - (PR_mt < -1);
				Series FT_lt = (PR_lt > 1) - (PR_lt < -1);
				Series ST_st = atm.calculateSTSig(bars[intervals[0]][0], bars[intervals[0]][1], bars[intervals[0]][2], bars[intervals[0]][3], 1);
				Series ST_mt = atm.calculateSTSig(bars[intervals[1]][0], bars[intervals[1]][1], bars[intervals[1]][2], bars[intervals[1]][3], 1);
				Series ST_lt = atm.calculateSTSig(bars[intervals[2]][0], bars[intervals[2]][1], bars[intervals[2]][2], bars[intervals[2]][3], 1);
				Series STFTGoingUp = stft.IsRising();
				Series STFTGoingDn = stft.IsFalling();
				Series MTFTGoingUp = mtft.IsRising();
				Series MTFTGoingDn = mtft.IsFalling();
				Series LTFTGoingUp = ltft.IsRising();
				Series LTFTGoingDn = ltft.IsFalling();

				Series STSTGoingUp = stst.IsRising();
				Series STSTGoingDn = stst.IsFalling();
				Series MTSTGoingUp = mtst.IsRising();
				Series MTSTGoingDn = mtst.IsFalling();
				Series LTSTGoingUp = ltst.IsRising();
				Series LTSTGoingDn = ltst.IsFalling();

				Series score_st = atm.getScore(times, bars, new string[] { intervals[0], intervals[1] });
				Series RP_st = atm.calculateRelativePrice(intervals[0], bars[intervals[0]], _referenceData, 5);
				Series SC_st = atm.calculateSCSig(score_st, RP_st, 1);
				Series score_mt = atm.getScore(times, bars, new string[] { intervals[1], intervals[2] });
				Series RP_mt = atm.calculateRelativePrice(intervals[0], bars[intervals[1]], _referenceData, 5);
				Series SC_mt = atm.calculateSCSig(score_mt, RP_mt, 1);
				//Series score_lt = atm.getScore(times, bars, new string[] { intervals[2], intervals[3] });
				//Series RP_lt = atm.calculateRelativePrice(intervals[0], bars[intervals[2]], _referenceData, 5);
				//Series SC_lt = atm.calculateSCSig(score_lt, RP_lt, 1);
				//Series NET_mt = atm.calculateNetSig(score_mt, RP_mt, SC_mt, 1);
				//Series NET_st = atm.calculateNetSig(score_st, RP_st, SC_st, 1);

				// Timing 
				PA = atm.calculatePressureAlertNoFilter(score_st, bars[intervals[0]][0], bars[intervals[0]][1], bars[intervals[0]][2], bars[intervals[0]][3]); // P Alert 
																																							   //PA5 = atm.calculateP5Sig(score_st, bars[intervals[0]][0], bars[intervals[0]][1], bars[intervals[0]][2], bars[intervals[0]][3]); // P5 Alert 
				TB = atm.calculateTwoBarPattern(bars[intervals[0]][0], bars[intervals[0]][1], bars[intervals[0]][2], bars[intervals[0]][3]);  //Two Bar added to PA
				SA = atm.calculateScoreAlert(score_st);  //score alert
				EXH = atm.calculateExhaustion(bars[intervals[0]][1], bars[intervals[0]][2], bars[intervals[0]][3], atm.ExhaustionLevelSelection.AllLevels);  //exh

				var scAddEnbs = _chart2.GetSCAddEnbs();
				var advice = atm.getAdvice(symbol, times, bars, intervals.ToArray(), _referenceData, scAddEnbs, times[intervals[0]].Count - 1);

				var portfolioName = getPortfolioName();

				for (int ago = -1; ago <= 2; ago++)
				{
					string key = symbol + ":" + intervals[0] + ":" + ago.ToString();
					Result result;

					lock (_atmResults)
					{
						result = _atmResults.TryGetValue(key, out result) ? result : new Result();
					}

					if (ago == 0)
					{
						result.PositionColor = advice[1].Item2;
						result.Add1UpColor = advice[5].Item2;
						result.Add1DnColor = advice[6].Item2;
						result.Add2UpColor = advice[7].Item2;
						result.Add2DnColor = advice[8].Item2;
						result.Add3UpColor = advice[9].Item2;
						result.Add3DnColor = advice[10].Item2;
						result.ShortRetUpColor = advice[11].Item2;
						result.LongRetDnColor = advice[12].Item2;
						result.ExhUpColor = advice[13].Item2;
						result.ExhDnColor = advice[14].Item2;
						result.PositionUpColor = advice[25].Item2;
						result.PositionDnColor = advice[26].Item2;
					}
					if (ago == -1)
					{
						result.Add1UpColorNxt = advice[15].Item2;
						result.Add1DnColorNxt = advice[16].Item2;
						result.Add2UpColorNxt = advice[17].Item2;
						result.Add2DnColorNxt = advice[18].Item2;
						result.Add3UpColorNxt = advice[19].Item2;
						result.Add3DnColorNxt = advice[20].Item2;
						result.ShortRetUpColorNxt = advice[21].Item2;
						result.LongRetDnColorNxt = advice[22].Item2;
						result.ExhUpColorNxt = advice[23].Item2;
						result.ExhDnColorNxt = advice[24].Item2;
						result.PositionUpColor = Colors.Transparent;
						result.PositionDnColor = Colors.Transparent;
					}

					if (ago >= 0)
					{
						string networkName = _portfolio.GetTradeNetwork(portfolioName);
						int direction = Trade.Manager.getOpenTradeDirectionForNetworkAndSymbol(networkName, symbol);

						bool OEX = (portfolioName == "CMR");
						bool PCM = (portfolioName == "PCM MAIN EUR" || portfolioName == "PCM MAIN US" || portfolioName == "PCM MAIN ASIA" || portfolioName == "PCM RESEARCH BUYS NA" || portfolioName == "PCM RESEARCH SELLS NA" || portfolioName == "US ETF");

						double pr0 = PR_mt[PR_mt.Count - ago - 1];
						double pr1 = PR_mt[PR_mt.Count - ago - 2];
						double pr2 = PR_mt[PR_mt.Count - ago - 3];
						double pr3 = PR_mt[PR_mt.Count - ago - 4];

						bool isOldLong = (pr0 == 1.5 && pr1 == 1.5 && pr2 == 1.5 && pr3 == 1.5);
						bool isOldShort = (pr0 == -1.5 && pr1 == -1.5 && pr2 == -1.5 && pr3 == -1.5);
						bool isLong = (pr0 == 1.5 && pr1 == 1.5); //new
						bool isBullish = (pr0 == 1.0 || pr0 == 1.5);
						bool newLong = pr0 == 1.5 && (pr1 == 1.0 || pr1 == -1.0 || pr1 == -1.5);
						bool newSCLong = (pr0 == 1.0 || pr0 == 1.5) && (pr1 == -1.0 || pr1 == -1.5);
						bool addToLong = pr0 == 1.5 && pr1 == 1.0;
						bool reduceLong = pr0 == 1.0;  //pr0 == 1.0 && pr1 == 1.5;
						bool exitLong = (pr0 == -1.0 || pr0 == -1.5) && pr1 == 1.5;
						bool isShort = (pr0 == -1.5 && pr1 == -1.5); //new 
						bool isBearish = (pr0 == -1.0 || pr0 == -1.5);
						bool newShort = pr0 == -1.5 && (pr1 == -1.0 || pr1 == 1.0 || pr1 == 1.5);
						bool newSCShort = (pr0 == -1.0 || pr0 == -1.5) && (pr1 == 1.0 || pr1 == 1.5);
						bool addToShort = pr0 == -1.5 && pr1 == -1.0;
						bool reduceShort = pr0 == -1.0;  //pr0 == -1.0 && pr1 == -1.5;
						bool exitShort = (pr0 == 1.0 || pr0 == 1.5) && pr1 == -1.5;

						result.PRLong = TradeState.Off;
						if (isLong) result.PRLong = TradeState.Current;
						if (newLong) result.PRLong = TradeState.New;
						if (reduceLong && direction >= 0) result.PRLong = TradeState.Reduce;

						result.PRShort = TradeState.Off;
						if (isShort) result.PRShort = TradeState.Current;
						if (newShort) result.PRShort = TradeState.New;
						if (reduceShort && direction <= 0) result.PRShort = TradeState.Reduce;
						//if (exitShort) result.PRShort = TradeState.Exit;

						pr0 = PR_lt[PR_lt.Count - ago - 1];
						pr1 = PR_lt[PR_lt.Count - ago - 2];
						pr2 = PR_lt[PR_lt.Count - ago - 3];
						pr3 = PR_lt[PR_lt.Count - ago - 4];

						//isOldLong = (pr0 == 1.5 && pr1 == 1.5 && pr2 == 1.5 && pr3 == 1.5);
						//isOldShort = (pr0 == -1.5 && pr1 == -1.5 && pr2 == -1.5 && pr3 == -1.5);
						isLong = (pr0 == 1.5 && pr1 == 1.5); //new
															 //isBullish = (pr0 == 1.0 || pr0 == 1.5);
						newLong = pr0 == 1.5 && (pr1 == 1.0 || pr1 == -1.0 || pr1 == -1.5);
						//newSCLong = (pr0 == 1.0 || pr0 == 1.5) && (pr1 == -1.0 || pr1 == -1.5);
						//addToLong = pr0 == 1.5 && pr1 == 1.0;
						reduceLong = pr0 == 1.0;  //pr0 == 1.0 && pr1 == 1.5;
												  //exitLong = (pr0 == -1.0 || pr0 == -1.5) && pr1 == 1.5;
						isShort = (pr0 == -1.5 && pr1 == -1.5); //new 
																//isBearish = (pr0 == -1.0 || pr0 == -1.5);
						newShort = pr0 == -1.5 && (pr1 == -1.0 || pr1 == 1.0 || pr1 == 1.5);
						//newSCShort = (pr0 == -1.0 || pr0 == -1.5) && (pr1 == 1.0 || pr1 == 1.5);
						//addToShort = pr0 == -1.5 && pr1 == -1.0;
						reduceShort = pr0 == -1.0;  //pr0 == -1.0 && pr1 == -1.5;
													//exitShort = (pr0 == 1.0 || pr0 == 1.5) && pr1 == -1.5;

						result.PRLongLT = TradeState.Off;
						if (isLong) result.PRLongLT = TradeState.Current;
						if (newLong) result.PRLongLT = TradeState.New;
						if (reduceLong && direction >= 0) result.PRLongLT = TradeState.Reduce;

						result.PRShortLT = TradeState.Off;
						if (isShort) result.PRShortLT = TradeState.Current;
						if (newShort) result.PRShortLT = TradeState.New;
						if (reduceShort && direction <= 0) result.PRShortLT = TradeState.Reduce;
						//if (exitShort) result.PRShortLT = TradeState.Exit;

						pr0 = PR_st[PR_st.Count - ago - 1];
						pr1 = PR_st[PR_st.Count - ago - 2];
						pr2 = PR_st[PR_st.Count - ago - 3];
						pr3 = PR_st[PR_st.Count - ago - 4];

						//isOldLong = (pr0 == 1.5 && pr1 == 1.5 && pr2 == 1.5 && pr3 == 1.5);
						//isOldShort = (pr0 == -1.5 && pr1 == -1.5 && pr2 == -1.5 && pr3 == -1.5);
						isLong = (pr0 == 1.5 && pr1 == 1.5); //new
															 //isBullish = (pr0 == 1.0 || pr0 == 1.5);
						newLong = pr0 == 1.5 && (pr1 == 1.0 || pr1 == -1.0 || pr1 == -1.5);
						//newSCLong = (pr0 == 1.0 || pr0 == 1.5) && (pr1 == -1.0 || pr1 == -1.5);
						//addToLong = pr0 == 1.5 && pr1 == 1.0;
						reduceLong = pr0 == 1.0;  //pr0 == 1.0 && pr1 == 1.5;
												  //exitLong = (pr0 == -1.0 || pr0 == -1.5) && pr1 == 1.5;
						isShort = (pr0 == -1.5 && pr1 == -1.5); //new 
																//isBearish = (pr0 == -1.0 || pr0 == -1.5);
						newShort = pr0 == -1.5 && (pr1 == -1.0 || pr1 == 1.0 || pr1 == 1.5);
						//newSCShort = (pr0 == -1.0 || pr0 == -1.5) && (pr1 == 1.0 || pr1 == 1.5);
						//addToShort = pr0 == -1.5 && pr1 == -1.0;
						reduceShort = pr0 == -1.0;  //pr0 == -1.0 && pr1 == -1.5;
													//exitShort = (pr0 == 1.0 || pr0 == 1.5) && pr1 == -1.5;

						result.PRLongST = TradeState.Off;
						if (isLong) result.PRLongST = TradeState.Current;
						if (newLong) result.PRLongST = TradeState.New;
						if (reduceLong && direction >= 0) result.PRLongST = TradeState.Reduce;

						result.PRShortST = TradeState.Off;
						if (isShort) result.PRShortST = TradeState.Current;
						if (newShort) result.PRShortST = TradeState.New;
						if (reduceShort && direction <= 0) result.PRShortST = TradeState.Reduce;

						result.STFTTU = TradeState.Off;
						if (STFTTurnsUp != null)
						{
							if (STFTTurnsUp[STFTGoingUp.Count - ago - 1] == 1) result.STFTTU = TradeState.Current;
						}

						result.STFTTD = TradeState.Off;
						if (STFTTurnsDn != null)
						{
							if (STFTTurnsDn[STFTGoingDn.Count - ago - 1] == 1) result.STFTTD = TradeState.Current;
						}

						result.STSTTU = TradeState.Off;
						if (STSTTurnsUp != null)
						{
							if (STSTTurnsUp[STSTGoingUp.Count - ago - 1] == 1) result.STSTTU = TradeState.Current;
						}

						result.STSTTD = TradeState.Off;
						if (STSTTurnsDn != null)
						{
							if (STSTTurnsDn[STSTGoingDn.Count - ago - 1] == 1) result.STSTTD = TradeState.Current;
						}

						result.STFTGD = TradeState.Off;
						if (STFTGoingDn != null)
						{
							if (STFTGoingDn[STFTGoingDn.Count - ago - 1] == 1) result.STFTGD = TradeState.Current;
						}

						result.STFTGU = TradeState.Off;
						if (STFTGoingUp != null)
						{
							if (STFTGoingUp[STFTGoingUp.Count - ago - 1] == 1) result.STFTGU = TradeState.Current;
						}

						result.STSTGD = TradeState.Off;
						if (STSTGoingDn != null)
						{
							if (STSTGoingDn[STSTGoingDn.Count - ago - 1] == 1) result.STSTGD = TradeState.Current;
						}

						result.STSTGU = TradeState.Off;
						if (STSTGoingUp != null)
						{
							if (STSTGoingUp[STSTGoingUp.Count - ago - 1] == 1) result.STSTGU = TradeState.Current;
						}

						result.MTFTTU = TradeState.Off;
						if (MTFTTurnsUp != null)
						{
							if (MTFTTurnsUp[MTFTGoingUp.Count - ago - 1] == 1) result.MTFTTU = TradeState.Current;
						}

						result.MTFTTD = TradeState.Off;
						if (MTFTTurnsDn != null)
						{
							if (MTFTTurnsDn[MTFTGoingDn.Count - ago - 1] == 1) result.MTFTTD = TradeState.Current;
						}

						result.MTSTTU = TradeState.Off;
						if (MTSTTurnsUp != null)
						{
							if (MTSTTurnsUp[MTSTGoingUp.Count - ago - 1] == 1) result.MTSTTU = TradeState.Current;
						}

						result.MTSTTD = TradeState.Off;
						if (MTSTTurnsDn != null)
						{
							if (MTSTTurnsDn[MTSTGoingDn.Count - ago - 1] == 1) result.MTSTTD = TradeState.Current;
						}

						result.MTFTGD = TradeState.Off;
						if (MTFTGoingDn != null)
						{
							if (MTFTGoingDn[MTFTGoingDn.Count - ago - 1] == 1) result.MTFTGD = TradeState.Current;
						}

						result.MTFTGU = TradeState.Off;
						if (MTFTGoingUp != null)
						{
							if (MTFTGoingUp[MTFTGoingUp.Count - ago - 1] == 1) result.MTFTGU = TradeState.Current;
						}

						result.MTSTGD = TradeState.Off;
						if (MTSTGoingDn != null)
						{
							if (MTSTGoingDn[MTSTGoingDn.Count - ago - 1] == 1) result.MTSTGD = TradeState.Current;
						}

						result.MTSTGU = TradeState.Off;
						if (MTSTGoingUp != null)
						{
							if (MTSTGoingUp[MTSTGoingUp.Count - ago - 1] == 1) result.MTSTGU = TradeState.Current;
						}

						result.LTFTTU = TradeState.Off;
						if (LTFTTurnsUp != null)
						{
							if (LTFTTurnsUp[LTFTGoingUp.Count - ago - 1] == 1) result.LTFTTU = TradeState.Current;
						}

						result.LTFTTD = TradeState.Off;
						if (LTFTTurnsDn != null)
						{
							if (LTFTTurnsDn[LTFTGoingDn.Count - ago - 1] == 1) result.LTFTTD = TradeState.Current;
						}

						result.LTSTTU = TradeState.Off;
						if (LTSTTurnsUp != null)
						{
							if (LTSTTurnsUp[LTSTGoingUp.Count - ago - 1] == 1) result.LTSTTU = TradeState.Current;
						}

						result.LTSTTD = TradeState.Off;
						if (LTSTTurnsDn != null)
						{
							if (LTSTTurnsDn[LTSTGoingDn.Count - ago - 1] == 1) result.LTSTTD = TradeState.Current;
						}

						result.LTFTGD = TradeState.Off;
						if (LTFTGoingDn != null)
						{
							if (LTFTGoingDn[LTFTGoingDn.Count - ago - 1] == 1) result.LTFTGD = TradeState.Current;
						}

						result.LTFTGU = TradeState.Off;
						if (LTFTGoingUp != null)
						{
							if (LTFTGoingUp[LTFTGoingUp.Count - ago - 1] == 1) result.LTFTGU = TradeState.Current;
						}

						result.LTSTGD = TradeState.Off;
						if (LTSTGoingDn != null)
						{
							if (LTSTGoingDn[LTSTGoingDn.Count - ago - 1] == 1) result.LTSTGD = TradeState.Current;
						}

						result.LTSTGU = TradeState.Off;
						if (LTSTGoingUp != null)
						{
							if (LTSTGoingUp[LTSTGoingUp.Count - ago - 1] == 1) result.LTSTGU = TradeState.Current;
						}

						result.LTFT[0] = (int)(LTFTGoingUp[LTFTGoingUp.Count - ago - 1] - LTFTGoingDn[LTFTGoingDn.Count - ago - 1]);
						result.MTFT[0] = (int)(MTFTGoingUp[MTFTGoingUp.Count - ago - 1] - MTFTGoingDn[MTFTGoingDn.Count - ago - 1]);
						result.STFT[0] = (int)(STFTGoingUp[STFTGoingUp.Count - ago - 1] - STFTGoingDn[STFTGoingDn.Count - ago - 1]);

						result.LTFTTurn[0] = (int)(LTFTTurnsUp[LTFTTurnsUp.Count - ago - 1] - LTFTTurnsDn[LTFTTurnsDn.Count - ago - 1]);
						result.MTFTTurn[0] = (int)(MTFTTurnsUp[MTFTTurnsUp.Count - ago - 1] - MTFTTurnsDn[MTFTTurnsDn.Count - ago - 1]);
						result.STFTTurn[0] = (int)(STFTTurnsUp[STFTTurnsUp.Count - ago - 1] - STFTTurnsDn[STFTTurnsDn.Count - ago - 1]);

						result.LTST[0] = (ltst[ltst.Count - ago - 1] > ltft[ltft.Count - ago - 1]) ? 1 : (ltst[ltst.Count - ago - 1] < ltft[ltft.Count - ago - 1]) ? -1 : 0;
						result.MTST[0] = (mtst[mtst.Count - ago - 1] > mtft[mtft.Count - ago - 1]) ? 1 : (mtst[mtst.Count - ago - 1] < mtft[mtft.Count - ago - 1]) ? -1 : 0;
						result.STST[0] = (stst[stst.Count - ago - 1] > stft[stft.Count - ago - 1]) ? 1 : (stst[stst.Count - ago - 1] < stft[stft.Count - ago - 1]) ? -1 : 0;

						result.LTSTTurn[0] = (int)(LTSTTurnsUp[LTSTTurnsUp.Count - ago - 1] - LTSTTurnsDn[LTSTTurnsDn.Count - ago - 1]);
						result.MTSTTurn[0] = (int)(MTSTTurnsUp[MTSTTurnsUp.Count - ago - 1] - MTSTTurnsDn[MTSTTurnsDn.Count - ago - 1]);
						result.STSTTurn[0] = (int)(STSTTurnsUp[STSTTurnsUp.Count - ago - 1] - STSTTurnsDn[STSTTurnsDn.Count - ago - 1]);

						result.LTft[0] = (int)(FT_lt[FT_lt.Count - ago - 1]);
						result.MTft[0] = (int)(FT_mt[FT_mt.Count - ago - 1]);
						result.STft[0] = (int)(FT_st[FT_st.Count - ago - 1]);

						result.LTst[0] = (int)(ST_lt[ST_lt.Count - ago - 1]);
						result.MTst[0] = (int)(ST_mt[ST_mt.Count - ago - 1]);
						result.STst[0] = (int)(ST_st[ST_st.Count - ago - 1]);

						result.STpr[0] = PR_st[PR_st.Count - ago - 1];
						result.MTpr[0] = PR_mt[PR_mt.Count - ago - 1];
						result.STpr[1] = PR_st[PR_st.Count - ago - 2];
						result.MTpr[1] = PR_mt[PR_mt.Count - ago - 2];

						//result.LTsc[0] = (int)(SC_lt[SC_lt.Count - ago - 1]);
						result.MTsc[0] = (int)(SC_mt[SC_mt.Count - ago - 1]);
						result.STsc[0] = (int)(SC_st[SC_st.Count - ago - 1]);


						result.LTFT[1] = (int)(LTFTGoingUp[LTFTGoingUp.Count - ago - 2] - LTFTGoingDn[LTFTGoingDn.Count - ago - 2]);
						result.MTFT[1] = (int)(MTFTGoingUp[MTFTGoingUp.Count - ago - 2] - MTFTGoingDn[MTFTGoingDn.Count - ago - 2]);
						result.STFT[1] = (int)(STFTGoingUp[STFTGoingUp.Count - ago - 2] - STFTGoingDn[STFTGoingDn.Count - ago - 2]);

						result.LTFTTurn[1] = (int)(LTFTTurnsUp[LTFTTurnsUp.Count - ago - 2] - LTFTTurnsDn[LTFTTurnsDn.Count - ago - 2]);
						result.MTFTTurn[1] = (int)(MTFTTurnsUp[MTFTTurnsUp.Count - ago - 2] - MTFTTurnsDn[MTFTTurnsDn.Count - ago - 2]);
						result.STFTTurn[1] = (int)(STFTTurnsUp[STFTTurnsUp.Count - ago - 2] - STFTTurnsDn[STFTTurnsDn.Count - ago - 2]);

						result.LTST[1] = (int)(LTSTGoingUp[LTSTGoingUp.Count - ago - 2] - LTSTGoingDn[LTSTGoingDn.Count - ago - 2]);
						result.MTST[1] = (int)(MTSTGoingUp[MTSTGoingUp.Count - ago - 2] - MTSTGoingDn[MTSTGoingDn.Count - ago - 2]);
						result.STST[1] = (int)(STSTGoingUp[STSTGoingUp.Count - ago - 2] - STSTGoingDn[STSTGoingDn.Count - ago - 2]);

						result.LTSTTurn[1] = (int)(LTSTTurnsUp[LTSTTurnsUp.Count - ago - 2] - LTSTTurnsDn[LTSTTurnsDn.Count - ago - 2]);
						result.MTSTTurn[1] = (int)(MTSTTurnsUp[MTSTTurnsUp.Count - ago - 2] - MTSTTurnsDn[MTSTTurnsDn.Count - ago - 2]);
						result.STSTTurn[1] = (int)(STSTTurnsUp[STSTTurnsUp.Count - ago - 2] - STSTTurnsDn[STSTTurnsDn.Count - ago - 2]);

						result.LTft[1] = (int)(FT_lt[FT_lt.Count - ago - 2]);
						result.MTft[1] = (int)(FT_mt[FT_mt.Count - ago - 2]);
						result.STft[1] = (int)(FT_st[FT_st.Count - ago - 2]);

						result.LTst[1] = (int)(ST_lt[ST_lt.Count - ago - 2]);
						result.MTst[1] = (int)(ST_mt[ST_mt.Count - ago - 2]);
						result.STst[1] = (int)(ST_st[ST_st.Count - ago - 2]);

						//result.LTsc[1] = (int)(SC_lt[SC_lt.Count - ago - 2]);
						result.MTsc[1] = (int)(SC_mt[SC_mt.Count - ago - 2]);
						result.STsc[1] = (int)(SC_st[SC_st.Count - ago - 2]);


						//Debug.WriteLine(symbol + " LT: total = " + LTct + " ft = " + FT_lt[FT_lt.Count - 1] + " st = " + ST_lt[ST_lt.Count - 1] + " sc = " + SC_lt[SC_lt.Count - 1]);
						//Debug.WriteLine(symbol + " MT: total = " + MTct + " ft = " + FT_mt[FT_mt.Count - 1] + " st = " + ST_mt[ST_mt.Count - 1] + " sc = " + SC_mt[SC_mt.Count - 1]);
						//Debug.WriteLine(symbol + " ST: total = " + STct + " ft = " + FT_st[FT_st.Count - 1] + " st = " + ST_st[ST_st.Count - 1] + " sc = " + SC_st[SC_st.Count - 1]);

						// SC New Current SC Strategy col 7 20 
						// ST Signal New Current ST Strategy col 8 21 
						// FT signals New Current Reduce Exit  col 9 22

						// TSB 
						double tsb0 = TSB[TSB.Count - ago - 1];
						double tsb1 = TSB[TSB.Count - ago - 2];

						isLong = (tsb0 == 1);
						newLong = (tsb0 == 1 && tsb1 != 1);
						exitLong = (tsb0 != 1 && tsb1 == 1);

						isShort = (tsb0 == -1);
						newShort = (tsb0 == -1 && tsb1 != -1);
						exitShort = (tsb0 != -1 && tsb1 == -1);

						result.TSBLong = TradeState.Off;
						if (isLong) result.TSBLong = TradeState.Current;

						result.TSBShort = TradeState.Off;
						if (isShort) result.TSBShort = TradeState.Current;

						// TSBst Trend 
						if (TSBst != null)
						{
							double tsbST0 = TSBst[TSBst.Count - ago - 1];

							bool isLongBullish = (tsbST0 == 1);
							bool newLongBullish = (tsbST0 == 1);
							bool isLongBearish = (tsbST0 != 1);
							bool newLongBearish = (tsbST0 != 1);

							bool isShortBearish = (tsbST0 == -1);
							bool newShortBearish = (tsbST0 == -1);
							bool isShortBullish = (tsbST0 != -1);
							bool newShortBullish = (tsbST0 != -1);

							result.TSBstLong = TradeState.Off;
							if (isLongBullish) result.TSBstLong = TradeState.CurrentBullish;
							else if (newLongBearish) result.TSBstLong = TradeState.NewBearish;
							else if (isLongBullish) result.TSBstLong = TradeState.CurrentBullish;
							else if (isLongBearish) result.TSBstLong = TradeState.CurrentBearish;

							result.TSBstShort = TradeState.Off;
							if (isShortBearish) result.TSBstShort = TradeState.CurrentBearish;
							else if (newShortBullish) result.TSBstShort = TradeState.NewBullish;
							else if (isShortBearish) result.TSBstShort = TradeState.CurrentBearish;
							else if (isShortBullish) result.TSBstShort = TradeState.CurrentBullish;
						}

						// TSBmt Trend 
						if (TSBmt != null)
						{
							double tsbMT0 = TSBmt[TSBmt.Count - ago - 1];

							bool isLongBullish = (tsbMT0 == 1);
							bool newLongBullish = (tsbMT0 == 1);
							bool isLongBearish = (tsbMT0 != 1);
							bool newLongBearish = (tsbMT0 != 1);

							bool isShortBearish = (tsbMT0 == -1);
							bool newShortBearish = (tsbMT0 == -1);
							bool isShortBullish = (tsbMT0 != -1);
							bool newShortBullish = (tsbMT0 != -1);

							result.TSBmtLong = TradeState.Off;
							if (isLongBullish) result.TSBmtLong = TradeState.CurrentBullish;
							else if (newLongBearish) result.TSBmtLong = TradeState.NewBearish;
							else if (isLongBullish) result.TSBmtLong = TradeState.CurrentBullish;
							else if (isLongBearish) result.TSBmtLong = TradeState.CurrentBearish;

							result.TSBmtShort = TradeState.Off;
							if (isShortBearish) result.TSBmtShort = TradeState.CurrentBearish;
							else if (newShortBullish) result.TSBmtShort = TradeState.NewBullish;
							else if (isShortBearish) result.TSBmtShort = TradeState.CurrentBearish;
							else if (isShortBullish) result.TSBmtShort = TradeState.CurrentBullish;
						}

						// TSBlt Trend 
						if (TSBlt != null)
						{
							double tsbLT0 = TSBlt[TSBlt.Count - ago - 1];

							bool isLongBullish = (tsbLT0 == 1);
							bool newLongBullish = (tsbLT0 == 1);
							bool isLongBearish = (tsbLT0 != 1);
							bool newLongBearish = (tsbLT0 != 1);

							bool isShortBearish = (tsbLT0 == -1);
							bool newShortBearish = (tsbLT0 == -1);
							bool isShortBullish = (tsbLT0 != -1);
							bool newShortBullish = (tsbLT0 != -1);

							result.TSBltLong = TradeState.Off;
							if (isLongBullish) result.TSBltLong = TradeState.CurrentBullish;
							else if (newLongBearish) result.TSBltLong = TradeState.NewBearish;
							else if (isLongBullish) result.TSBltLong = TradeState.CurrentBullish;
							else if (isLongBearish) result.TSBltLong = TradeState.CurrentBearish;

							result.TSBltShort = TradeState.Off;
							if (isShortBearish) result.TSBltShort = TradeState.CurrentBearish;
							else if (newShortBullish) result.TSBltShort = TradeState.NewBullish;
							else if (isShortBearish) result.TSBltShort = TradeState.CurrentBearish;
							else if (isShortBullish) result.TSBltShort = TradeState.CurrentBullish;
						}

						// ftOB or ftOS  MT


						double ftOBOS0 = PXFtOBOS[PXFtOBOS.Count - ago - 1];
						double ftOBOS1 = PXFtOBOS[PXFtOBOS.Count - ago - 2];

						isShort = (ftOBOS0 == 1);
						isLong = (ftOBOS0 == -1);

						result.FTOS = TradeState.Off;
						if (isLong) result.FTOS = TradeState.Current;

						result.FTOB = TradeState.Off;
						if (isShort) result.FTOB = TradeState.Current;


						// ftOB or ftOS  ST

						double ftSTOBOS0 = PXSTFtOBOS[PXSTFtOBOS.Count - ago - 1];
						double ftSTOBOS1 = PXSTFtOBOS[PXSTFtOBOS.Count - ago - 2];

						isShort = (ftSTOBOS0 == 1);
						isLong = (ftSTOBOS0 == -1);

						result.STFTOS = TradeState.Off;
						if (isLong) result.STFTOS = TradeState.Current;

						result.STFTOB = TradeState.Off;
						if (isShort) result.STFTOB = TradeState.Current;

						// ftOB or ftOS  LT

						double ftLTOBOS0 = PXLTFtOBOS[PXLTFtOBOS.Count - ago - 1];
						double ftLTOBOS1 = PXLTFtOBOS[PXLTFtOBOS.Count - ago - 2];

						isShort = (ftLTOBOS0 == 1);
						isLong = (ftLTOBOS0 == -1);

						result.LTFTOS = TradeState.Off;
						if (isLong) result.LTFTOS = TradeState.Current;

						result.LTFTOB = TradeState.Off;
						if (isShort) result.LTFTOB = TradeState.Current;

						// stOB or stOS  lt
						double ltSTOB0 = ltSTOB[ltSTOB.Count - ago - 1];
						double ltSTOB1 = ltSTOB[ltSTOB.Count - ago - 2];
						double ltSTOS0 = ltSTOS[ltSTOS.Count - ago - 1];
						double ltSTOS1 = ltSTOS[ltSTOS.Count - ago - 2];

						isLong = (ltSTOB0 == 1);
						isShort = (ltSTOS0 == 1);

						result.LTSTOB = TradeState.Off;
						if (isLong) result.LTSTOB = TradeState.Current;

						result.LTSTOS = TradeState.Off;
						if (isShort) result.LTSTOS = TradeState.Current;

						// stOB or stOS  mt
						double mtSTOB0 = mtSTOB[mtSTOB.Count - ago - 1];
						double mtSTOB1 = mtSTOB[mtSTOB.Count - ago - 2];
						double mtSTOS0 = mtSTOS[mtSTOS.Count - ago - 1];
						double mtSTOS1 = mtSTOS[mtSTOS.Count - ago - 2];

						isLong = (mtSTOB0 == 1);
						isShort = (mtSTOS0 == 1);

						result.MTSTOB = TradeState.Off;
						if (isLong) result.MTSTOB = TradeState.Current;

						result.MTSTOS = TradeState.Off;
						if (isShort) result.MTSTOS = TradeState.Current;

						// stOB or stOS  st
						double stSTOB0 = stSTOB[stSTOB.Count - ago - 1];
						double stSTOB1 = stSTOB[stSTOB.Count - ago - 2];
						double stSTOS0 = stSTOS[stSTOS.Count - ago - 1];
						double stSTOS1 = stSTOS[stSTOS.Count - ago - 2];


						isLong = (stSTOB0 == 1);
						isShort = (stSTOS0 == 1);

						result.STSTOB = TradeState.Off;
						if (isLong) result.STSTOB = TradeState.Current;

						result.STSTOS = TradeState.Off;
						if (isShort) result.STSTOS = TradeState.Current;

						// TrendBars ST
						double TrendBarsSTUp = stTBarUp[stTBarUp.Count - ago - 1];
						double TrendBarsSTDn = stTBarDn[stTBarDn.Count - ago - 1];

						isLong = (TrendBarsSTUp == 1);
						isShort = (TrendBarsSTDn == 1);

						result.TBarUpST = TradeState.Off;
						if (isLong) result.TBarUpST = TradeState.Current;

						result.TBarDnST = TradeState.Off;
						if (isShort) result.TBarDnST = TradeState.Current;

						// TrendBars MT
						double TrendBarsMTUp = mtTBarUp[mtTBarUp.Count - ago - 1];
						double TrendBarsMTDn = mtTBarDn[mtTBarDn.Count - ago - 1];

						isLong = (TrendBarsMTUp == 1);
						isShort = (TrendBarsMTDn == 1);

						result.TBarUpMT = TradeState.Off;
						if (isLong) result.TBarUpMT = TradeState.Current;

						result.TBarDnMT = TradeState.Off;
						if (isShort) result.TBarDnMT = TradeState.Current;

						// TrendBars LT
						double TrendBarsLTUp = ltTBarUp[ltTBarUp.Count - ago - 1];
						double TrendBarsLTDn = ltTBarDn[ltTBarDn.Count - ago - 1];

						isLong = (TrendBarsLTUp == 1);
						isShort = (TrendBarsLTDn == 1);

						result.TBarUpLT = TradeState.Off;
						if (isLong) result.TBarUpLT = TradeState.Current;

						result.TBarDnLT = TradeState.Off;
						if (isShort) result.TBarDnLT = TradeState.Current;

						// TrendLines ST
						double TrendLineSTUp = stTLineUp[stTLineUp.Count - ago - 1];
						double TrendLineSTDn = stTLineDn[stTLineDn.Count - ago - 1];

						isLong = (TrendLineSTUp == 1);
						isShort = (TrendLineSTDn == 1);

						result.TLineUpST = TradeState.Off;
						if (isLong) result.TLineUpST = TradeState.Current;

						result.TLineDnST = TradeState.Off;
						if (isShort) result.TLineDnST = TradeState.Current;

						// TrendLines MT
						double TrendLineMTUp = mtTLineUp[mtTLineUp.Count - ago - 1];
						double TrendLineMTDn = mtTLineDn[mtTLineDn.Count - ago - 1];

						isLong = (TrendLineMTUp == 1);
						isShort = (TrendLineMTDn == 1);

						result.TLineUpMT = TradeState.Off;
						if (isLong) result.TLineUpMT = TradeState.Current;

						result.TLineDnMT = TradeState.Off;
						if (isShort) result.TLineDnMT = TradeState.Current;

						// TrendLines LT
						double TrendLineLTUp = ltTLineUp[ltTLineUp.Count - ago - 1];
						double TrendLineLTDn = ltTLineDn[ltTLineDn.Count - ago - 1];

						isLong = (TrendLineLTUp == 1);
						isShort = (TrendLineLTDn == 1);

						result.TLineUpLT = TradeState.Off;
						if (isLong) result.TLineUpLT = TradeState.Current;

						result.TLineDnLT = TradeState.Off;
						if (isShort) result.TLineDnLT = TradeState.Current;


						// PxF  not used
						if (PXF != null)
						{
							double pxf0 = PXF[PXF.Count - ago - 1];
							double pxf1 = PXF[PXF.Count - ago - 2];

							var isUpUp = (pxf0 == 2);
							var isUpDn = (pxf0 == 1);

							var isDnDn = (pxf0 == -2);
							var isDnUp = (pxf0 == -1);

							result.PXFLong = TradeState.Off;
							if (isUpUp) result.PXFLong = TradeState.New;
							else if (isUpDn) result.PXFLong = TradeState.Current;

							result.PXFShort = TradeState.Off;
							if (isDnDn) result.PXFShort = TradeState.New;
							else if (isDnUp) result.PXFShort = TradeState.Current;
						}


						// PXFlt Trend 
						if (PXFlt != null)
						{
							double pxfLT0 = PXFlt[PXFlt.Count - ago - 1];
							double pxfLT1 = PXFlt[PXFlt.Count - ago - 2];
							double tsbLT0 = TSBlt[TSBlt.Count - ago - 1];

							bool isLongBullish = (pxfLT0 == 1 && tsbLT0 == 1);
							bool newLongBullish = (pxfLT0 == 1 && pxfLT1 != 1 && tsbLT0 == 1);
							bool isLongBearish = (pxfLT0 == 1 && tsbLT0 != 1);
							bool newLongBearish = (pxfLT0 == 1 && pxfLT1 != 1 && tsbLT0 != 1);

							bool isShortBearish = (pxfLT0 == -1 && tsbLT0 == -1);
							bool newShortBearish = (pxfLT0 == -1 && pxfLT1 != -1 && tsbLT0 == -1);
							bool isShortBullish = (pxfLT0 == -1 && tsbLT0 != -1);
							bool newShortBullish = (pxfLT0 == -1 && pxfLT1 != -1 && tsbLT0 != -1);

							result.PXFltLong = TradeState.Off;
							if (newLongBullish) result.PXFltLong = TradeState.NewBullish;
							else if (newLongBearish) result.PXFltLong = TradeState.NewBearish;
							else if (isLongBullish) result.PXFltLong = TradeState.CurrentBullish;
							else if (isLongBearish) result.PXFltLong = TradeState.CurrentBearish;

							result.PXFltShort = TradeState.Off;
							if (newShortBearish) result.PXFltShort = TradeState.NewBearish;
							else if (newShortBullish) result.PXFltShort = TradeState.NewBullish;
							else if (isShortBearish) result.PXFltShort = TradeState.CurrentBearish;
							else if (isShortBullish) result.PXFltShort = TradeState.CurrentBullish;
						}

						// PXFmt Trend 
						if (PXFmt != null)
						{
							double pxfMT0 = PXFmt[PXFmt.Count - ago - 1];
							double pxfMT1 = PXFmt[PXFmt.Count - ago - 2];
							double tsbMT0 = TSBmt[TSBmt.Count - ago - 1];

							bool isLongBullish = (pxfMT0 == 1 && tsbMT0 == 1);
							bool newLongBullish = (pxfMT0 == 1 && pxfMT1 != 1 && tsbMT0 == 1);
							bool isLongBearish = (pxfMT0 == 1 && tsbMT0 != 1);
							bool newLongBearish = (pxfMT0 == 1 && pxfMT1 != 1 && tsbMT0 != 1);

							bool isShortBearish = (pxfMT0 == -1 && tsbMT0 == -1);
							bool newShortBearish = (pxfMT0 == -1 && pxfMT1 != -1 && tsbMT0 == -1);
							bool isShortBullish = (pxfMT0 == -1 && tsbMT0 != -1);
							bool newShortBullish = (pxfMT0 == -1 && pxfMT1 != -1 && tsbMT0 != -1);

							result.PXFmtLong = TradeState.Off;
							if (newLongBullish) result.PXFmtLong = TradeState.NewBullish;
							else if (newLongBearish) result.PXFmtLong = TradeState.NewBearish;
							else if (isLongBullish) result.PXFmtLong = TradeState.CurrentBullish;
							else if (isLongBearish) result.PXFmtLong = TradeState.CurrentBearish;

							result.PXFmtShort = TradeState.Off;
							if (newShortBearish) result.PXFmtShort = TradeState.NewBearish;
							else if (newShortBullish) result.PXFmtShort = TradeState.NewBullish;
							else if (isShortBearish) result.PXFmtShort = TradeState.CurrentBearish;
							else if (isShortBullish) result.PXFmtShort = TradeState.CurrentBullish;
						}

						// PXFst Trend 
						if (PXFst != null)
						{
							double pxfST0 = PXFst[PXFst.Count - ago - 1];
							double pxfST1 = PXFst[PXFst.Count - ago - 2];
							double tsbST0 = TSBst[TSBst.Count - ago - 1];

							bool isLongBullish = (pxfST0 == 1 && tsbST0 == 1);
							bool newLongBullish = (pxfST0 == 1 && pxfST1 != 1 && tsbST0 == 1);
							bool isLongBearish = (pxfST0 == 1 && tsbST0 != 1);
							bool newLongBearish = (pxfST0 == 1 && pxfST1 != 1 && tsbST0 != 1);

							bool isShortBearish = (pxfST0 == -1 && tsbST0 == -1);
							bool newShortBearish = (pxfST0 == -1 && pxfST1 != -1 && tsbST0 == -1);
							bool isShortBullish = (pxfST0 == -1 && tsbST0 != -1);
							bool newShortBullish = (pxfST0 == -1 && pxfST1 != -1 && tsbST0 != -1);

							result.PXFstLong = TradeState.Off;
							if (newLongBullish) result.PXFstLong = TradeState.NewBullish;
							else if (newLongBearish) result.PXFstLong = TradeState.NewBearish;
							else if (isLongBullish) result.PXFstLong = TradeState.CurrentBullish;
							else if (isLongBearish) result.PXFstLong = TradeState.CurrentBearish;

							result.PXFstShort = TradeState.Off;
							if (newShortBearish) result.PXFstShort = TradeState.NewBearish;
							else if (newShortBullish) result.PXFstShort = TradeState.NewBullish;
							else if (isShortBearish) result.PXFstShort = TradeState.CurrentBearish;
							else if (isShortBullish) result.PXFstShort = TradeState.CurrentBullish;
						}

						// Combine SFT and PR 
						if (SFT != null)
						{
							double pr = ((result.PRLong == TradeState.New) ? 1 : 0) - ((result.PRShort == TradeState.New) ? 1 : 0);

							double sft0 = SFT[SFT.Count - ago - 1] + pr;

							result.SFTPRLong = TradeState.Off;
							if (sft0 > 0) result.SFTPRLong = TradeState.New;

							result.SFTPRShort = TradeState.Off;
							if (sft0 < 0) result.SFTPRShort = TradeState.New;
						}

						if (SFT != null)
						{
							double sft0 = SFT[SFT.Count - ago - 1];

							result.SFTLong = TradeState.Off;
							if (sft0 > 0) result.SFTLong = TradeState.New;

							result.SFTShort = TradeState.Off;
							if (sft0 < 0) result.SFTShort = TradeState.New;
						}

						// PA Timing   not used
						if (PA != null)
						{
							var PAlert = PA;

							// PA P Alert
							double pa0 = PAlert[PAlert.Count - ago - 1];

							result.PALong = TradeState.Off;
							if (pa0 > 0) result.PALong = TradeState.New;

							result.PAShort = TradeState.Off;
							if (pa0 < 0) result.PAShort = TradeState.New;
						}

						// TB Timing not used
						if (TB != null)
						{
							var TwoBar = TB;

							// PA P Alert
							double tb0 = TwoBar[TwoBar.Count - ago - 1];

							result.TBLong = TradeState.Off;
							if (tb0 > 0) result.TBLong = TradeState.New;

							result.TBShort = TradeState.Off;
							if (tb0 < 0) result.TBShort = TradeState.New;
						}


						// Combine P Alert and Two Bar Alert
						if (PA != null && TB != null)
						{
							var PATB = PA + TB;

							// PA P Alert
							double patb0 = PATB[PATB.Count - ago - 1];

							result.PATBLong = TradeState.Off;
							if (patb0 > 0) result.PATBLong = TradeState.New;

							result.PATBShort = TradeState.Off;
							if (patb0 < 0) result.PATBShort = TradeState.New;
						}

						if (PA != null)
						{
							var PATB = PA;

							// PA P Alert
							double pa0 = PATB[PATB.Count - ago - 1];

							result.PALong = TradeState.Off;
							if (pa0 > 0) result.PALong = TradeState.New;

							result.PAShort = TradeState.Off;
							if (pa0 < 0) result.PAShort = TradeState.New;
						}
						if (TB != null)
						{
							var PATB = TB;

							// PA P Alert
							double tb0 = PATB[PATB.Count - ago - 1];

							result.TBLong = TradeState.Off;
							if (tb0 > 0) result.TBLong = TradeState.New;

							result.TBShort = TradeState.Off;
							if (tb0 < 0) result.TBShort = TradeState.New;
						}

						//EXH
						if (EXH != null)
						{
							var EXH1 = EXH;

							double exh0 = EXH1[EXH1.Count - ago - 1];

							result.EXHLong = TradeState.Off;
							if (exh0 > 0) result.EXHLong = TradeState.New;

							result.EXHShort = TradeState.Off;
							if (exh0 < 0) result.EXHShort = TradeState.New;
						}




						result.longAdvice = TradeState.Off;
						result.shortAdvice = TradeState.Off;

						int index = times["Daily"].Count - ago - 1;
						if (index >= 0)
						{
							var date = times["Daily"][index];
							date = new DateTime(date.Year, date.Month, date.Day, 0, 0, 0);
							var trades = _portfolio.GetTrades(PCM ? "PCM" : getPortfolioName(), symbol, date);
							if (trades.Count > 0)
							{
								var trade = trades[0];
								var oDate = trade.OpenDateTime;
								oDate = new DateTime(oDate.Year, oDate.Month, oDate.Day, 0, 0, 0);
								var cDate = trade.CloseDateTime;
								cDate = new DateTime(cDate.Year, cDate.Month, cDate.Day, 0, 0, 0);

								if (ago == 0)
								{

									var model = getModel(portfolioName);
									if (model != null)
									{
										var modelInterval = model.RankingInterval; // Use RankingInterval (Weekly) matching IdeaCalculator
										var g1 = model.Groups.Where(g => g.Direction == trade.Direction).ToList();
										var group = g1.Where(g => g.AlphaSymbols.Select(s => s.Ticker).Contains(symbol)).ToList().FirstOrDefault();
										if (group != null && group.UseATRRisk)
										{
											var factor = group.ATRRiskFactor;
											var period = group.ATRRiskPeriod;
											var hi = bars[modelInterval][1];
											var lo = bars[modelInterval][2];
											var cl = bars[modelInterval][3];
											var atr = Series.SmoothAvg(Series.TrueRange(hi, lo, cl), period);

											var entryDate = trade.OpenDateTime;
											var beg = times[modelInterval].FindIndex(x => x.Date == entryDate.Date);
											// Use end-1 to exclude the current in-progress bar (not yet closed),
											// matching IdeaCalculator which only processes fully settled bars
											var end = times[modelInterval].Count - 2;
											if (beg != -1 && end >= beg)
											{
												if (trade.Direction > 0)
												{
													var atrl = double.MinValue;
													for (var ix = beg; ix <= end; ix++)
													{
														atrl = Math.Max(lo[ix - 1] - factor * atr[ix - 1], atrl);
													}
													// Compare trailing stop against today's live close, not last Friday's
													var close = cl[times[modelInterval].Count - 1];
													var signal = close < atrl;
													result.ATRX[ago] = signal ? 1 : 0;
												}
												else
												{
													var atrs = double.MaxValue;
													for (var ix = beg; ix <= end; ix++)
													{
														atrs = Math.Min(hi[ix - 1] + factor * atr[ix - 1], atrs);
													}
													var close = cl[times[modelInterval].Count - 1];
													var signal = close > atrs;
													result.ATRX[ago] = signal ? -1 : 0;
												}
											}
											else
											{
												// Entry date not found in bar series — clear stale signal
												result.ATRX[ago] = 0;
											}
										}
										else
										{
											// ATR risk disabled — clear any previously cached signal
											result.ATRX[ago] = 0;
										}
									}
								}

								if (PCM)
								{
									if (date == oDate)
									{
										var dir = trade.Direction;
										if (dir == 2) result.longAdvice = TradeState.New;
										else if (dir == 1) result.longAdvice = TradeState.Exit;
										else if (dir == -2) result.shortAdvice = TradeState.New;
										else if (dir == -1) result.shortAdvice = TradeState.Exit;
									}
								}
								else
								{
									if (trade.Direction > 0)
									{
										if (date == oDate) result.longAdvice = TradeState.New;
										else if (date == cDate) result.longAdvice = TradeState.Exit;
										else result.longAdvice = TradeState.Current;
									}
									else if (trade.Direction < 0)
									{
										if (date == oDate) result.shortAdvice = TradeState.New;
										else if (date == cDate) result.shortAdvice = TradeState.Exit;
										else result.shortAdvice = TradeState.Current;
									}
								}
							}
						}
					}

					lock (_atmResults)
					{
						_atmResults[key] = result;
					}
					// Enqueue — never overwrite. Between two 200ms timer ticks, many tickers
					// can finish and every one must be redrawn, not just the last one.
					lock (_updateRows)
					{
						_updateRows.Enqueue(symbol);
					}
				}
			}
		}

		private void updatePCMResult(string symbol)
		{
			var ago = 0;
			var interval = "Daily";

			string key = symbol + ":" + interval + ":" + ago.ToString();
			Result result;

			lock (_atmResults)
			{
				result = _atmResults.TryGetValue(key, out result) ? result : new Result();
			}

			if (result != null)
			{
				var times = _barCache.GetTimes(symbol, interval, 0, 200);
				int index = times.Count - ago - 1;
				if (index >= 0)
				{
					var date = times[index];
					var date1 = new DateTime(date.Year, date.Month, date.Day, 0, 0, 0);
					var trades = _portfolio.GetTrades("PCM", symbol);
					if (trades.Count > 0)
					{
						var trade = trades[0];

						date = trade.OpenDateTime;
						var date2 = new DateTime(date.Year, date.Month, date.Day, 0, 0, 0);

						if (date1 == date2)
						{
							var dir = trade.Direction;
							if (dir == 2) result.longAdvice = TradeState.New;
							else if (dir == 1) result.longAdvice = TradeState.Exit;
							else if (dir == -2) result.shortAdvice = TradeState.New;
							else if (dir == -1) result.shortAdvice = TradeState.Exit;
						}
					}
				}
				_updateSpreadsheet = 6;
			}
		}

		private void addCount(int col, int row, Tuple<int, int> size)
		{
			Member member = null;
			lock (_memberListLock)
			{
				member = _memberList[row];
			}
			if (member != null)
			{
				member.ConditionCountUp[col] = size.Item1;
				member.ConditionCountDn[col] = size.Item2;
			}
		}

		private void drawCounts()
		{
			if (_view == "By CONDITION")
			{
				int colCnt = getColCount();
				for (int col = 0; col < colCnt; col++)
				{
					int count = 0;
					lock (_memberListLock)
					{
						for (var ii = 0; ii < _memberList.Count; ii++)
						{
							count += _memberList[ii].ConditionCountUp[col] + _memberList[ii].ConditionCountDn[col];
						}
					}

					string name = "Count" + col.ToString();
					Label label1 = FindName(name) as Label;
					if (label1 != null)
					{
						label1.Content = count.ToString();
					}
				}
			}
			else if (_view == "By CONDITION 2")
			{
				int colCnt = getColCount();
				for (int col = 0; col < colCnt; col++)
				{

					string name1 = "Count" + col.ToString();
					Label label1 = FindName(name1) as Label;
					if (label1 != null)
					{
						label1.Content = "";
					}

					int countL = 0;
					int countS = 0;
					lock (_memberListLock)
					{
						for (var ii = 0; ii < _memberList.Count; ii++)
						{
							countL += _memberList[ii].ConditionCountUp[col];
							countS += _memberList[ii].ConditionCountDn[col];
						}
					}

					string name2 = "Count" + col.ToString() + "L";
					Label label2 = FindName(name2) as Label;
					if (label2 != null)
					{
						label2.Content = countL.ToString();
					}

					string name3 = "Count" + col.ToString() + "S";
					Label label3 = FindName(name3) as Label;
					if (label3 != null)
					{
						label3.Content = countS.ToString();
					}
				}
			}
		}

		private double getCurrentPrice(string symbol)
		{
			List<Bar> bars = _barCache.GetBars(symbol, _interval, 0, 10);
			double price = (bars.Count > 0) ? bars[bars.Count - 1].Close : double.NaN;
			return price;
		}

		private double getPrice(string symbol, double price, string date)
		{
			if (double.IsNaN(price))
			{
				DateTime date1 = DateTime.Parse(date);
				List<Bar> bars = _barCache.GetBars(symbol, "Daily", 0, 200);
				foreach (var bar in bars)
				{
					if (bar.Time >= date1)
					{
						price = bar.Close;
						break;
					}
				}
			}
			return price;
		}

		private void drawCell(Member symbol, string interval)
		{
			bool view1 = false;
			bool rightInterval = false;

			var rev = atm.isYieldTicker(symbol.Ticker);

			int ago = _ago;

			if (_view == "By CONDITION" || _view == "By CONDITION 2")
			{
				var intervals = getIntervals();
				rightInterval = (interval != intervals[0]);
				view1 = (_view == "By CONDITION");
				ago = (!view1 && !rightInterval) ? _agoLT : _ago;
			}

			bool ok = false;

			Result value0 = null;
			string key0 = symbol.Ticker + ":" + interval + ":" + ago.ToString();
			lock (_atmResults)
			{
				ok = _atmResults.TryGetValue(key0, out value0);
			}

			Result value1 = null;
			string key1 = symbol.Ticker + ":" + interval + ":" + (ago + 1).ToString();
			lock (_atmResults)
			{
				_atmResults.TryGetValue(key1, out value1);
			}

			ok = ok && !(view1 && rightInterval);

			if (ok)
			{
				int row = getRowNumber(symbol.Ticker);

				int size1 = symbol.Size1;
				int size2 = symbol.Size2;

				if (row >= 0)
				{
					//int col = -1;
					Canvas canvas = null;
					int colCnt = 0;

					if (_view == "By CONDITION" || _view == "By CONDITION 2")
					{
						canvas = ConditionsSpreadsheet;

						colCnt = getColCount();

						bool enableLong = _portfolioDependent ? size1 > 0 : true;
						bool enableShort = _portfolioDependent ? size1 < 0 : true;


						//var ticker = symbol.Ticker;
						//var trades = _portfolio.GetTrades(getPortfolioName(), ticker);
						//trades = trades.Where(x => x.IsOpen()).ToList();
						//if (trades.Count > 0)
						//{
						//    double stopLossPercent;
						//    if (double.TryParse(StopLossPercent.Text, out stopLossPercent))
						//    {
						//        var trade = trades[0];
						//        double entryPrice = getPrice(ticker, trade.EntryPrice, trade.EntryDate);
						//        double currentClose = getCurrentPrice(ticker);
						//        double percent = trade.Direction * 100 * (currentClose - entryPrice) / entryPrice;
						//        bool signal = percent <= -stopLossPercent;
						//    }
						//}


						// TSB LT UP NEW
						if (enableLong && value0.TSBltLong == TradeState.NewBullish)
						{
							var index = view1 ? 1 : (rightInterval ? 43 : 1);
							Brush brush = Brushes.Transparent;
							brush = rev ? Brushes.Red : Brushes.Lime;
							addCount(index, row, new Tuple<int, int>(1, 0));
							drawRectangle(canvas, brush, brush, colCnt, row, index);
							symbol.ConditionValues[index] = 2;
						}

						// TSB LT UP OLD
						if (enableLong && value0.TSBltLong == TradeState.CurrentBullish)
						{
							var index = view1 ? 1 : (rightInterval ? 43 : 1);
							Brush brush = Brushes.Transparent;
							brush = rev ? Brushes.DarkRed : Brushes.DarkGreen;
							addCount(index, row, new Tuple<int, int>(1, 0));
							drawRectangle(canvas, brush, brush, colCnt, row, index);
							symbol.ConditionValues[index] = 1;
						}

						// TSB LT DN NEW 
						if (enableShort && value0.TSBltShort == TradeState.NewBearish)
						{
							var index = view1 ? 43 : (rightInterval ? 43 : 1);
							Brush brush = Brushes.Transparent;
							brush = rev ? Brushes.Lime : Brushes.Red;
							addCount(index, row, new Tuple<int, int>(0, 1));
							drawRectangle(canvas, brush, brush, colCnt, row, index);
							symbol.ConditionValues[index] = -2;
						}

						// TSB LT DN OLD
						if (enableShort && value0.TSBltShort == TradeState.CurrentBearish)
						{
							var index = view1 ? 43 : (rightInterval ? 43 : 1);
							Brush brush = Brushes.Transparent;
							brush = rev ? Brushes.DarkGreen : Brushes.DarkRed;
							addCount(index, row, new Tuple<int, int>(0, 1));
							drawRectangle(canvas, brush, brush, colCnt, row, index);
							symbol.ConditionValues[index] = -1;

						}

						// TSB MT UP NEW
						if (enableLong && value0.TSBmtLong == TradeState.NewBullish)
						{
							var index = view1 ? 2 : (rightInterval ? 42 : 2);
							Brush brush = Brushes.Transparent;
							brush = rev ? Brushes.Red : Brushes.Lime;
							addCount(index, row, new Tuple<int, int>(1, 0));
							drawRectangle(canvas, brush, brush, colCnt, row, index);
							symbol.ConditionValues[index] = 2;
						}

						// TSB MT UP OLD
						if (enableLong && value0.TSBmtLong == TradeState.CurrentBullish)
						{
							var index = view1 ? 2 : (rightInterval ? 42 : 2);
							Brush brush = Brushes.Transparent;
							brush = rev ? Brushes.DarkRed : Brushes.DarkGreen;
							addCount(index, row, new Tuple<int, int>(1, 0));
							drawRectangle(canvas, brush, brush, colCnt, row, index);
							symbol.ConditionValues[index] = 1;
						}

						// TSB MT DN NEW 
						if (enableShort && value0.TSBmtShort == TradeState.NewBearish)
						{
							var index = view1 ? 42 : (rightInterval ? 42 : 2);
							Brush brush = Brushes.Transparent;
							brush = rev ? Brushes.Lime : Brushes.Red;
							addCount(index, row, new Tuple<int, int>(0, 1));
							drawRectangle(canvas, brush, brush, colCnt, row, index);
							symbol.ConditionValues[index] = -2;
						}

						// TSB MT DN OLD
						if (enableShort && value0.TSBmtShort == TradeState.CurrentBearish)
						{
							var index = view1 ? 42 : (rightInterval ? 42 : 2);
							Brush brush = Brushes.Transparent;
							brush = rev ? Brushes.DarkGreen : Brushes.DarkRed;
							addCount(index, row, new Tuple<int, int>(0, 1));
							drawRectangle(canvas, brush, brush, colCnt, row, index);
							symbol.ConditionValues[index] = -1;
						}


						// TSB ST UP NEW
						if (enableLong && value0.TSBstLong == TradeState.NewBullish)
						{
							var index = view1 ? 3 : (rightInterval ? 41 : 3);
							Brush brush = Brushes.Transparent;
							brush = rev ? Brushes.Red : Brushes.Lime;
							addCount(index, row, new Tuple<int, int>(1, 0));
							drawRectangle(canvas, brush, brush, colCnt, row, index);
							symbol.ConditionValues[index] = 2;
						}

						// TSB ST UP OLD
						if (enableLong && value0.TSBstLong == TradeState.CurrentBullish)
						{
							var index = view1 ? 3 : (rightInterval ? 41 : 3);
							Brush brush = Brushes.Transparent;
							brush = rev ? Brushes.DarkRed : Brushes.DarkGreen;
							addCount(index, row, new Tuple<int, int>(1, 0));
							drawRectangle(canvas, brush, brush, colCnt, row, index);
							symbol.ConditionValues[index] = 1;
						}

						// TSB ST DN NEW 
						if (enableShort && value0.TSBstShort == TradeState.NewBearish)
						{
							var index = view1 ? 41 : (rightInterval ? 41 : 3);
							Brush brush = Brushes.Transparent;
							brush = rev ? Brushes.Lime : Brushes.Red;
							addCount(index, row, new Tuple<int, int>(0, 1));
							drawRectangle(canvas, brush, brush, colCnt, row, index);
							symbol.ConditionValues[index] = -2;
						}

						// TSB ST DN OLD
						if (enableShort && value0.TSBstShort == TradeState.CurrentBearish)
						{
							var index = view1 ? 41 : (rightInterval ? 41 : 3);
							Brush brush = Brushes.Transparent;
							brush = rev ? Brushes.DarkGreen : Brushes.DarkRed;
							addCount(index, row, new Tuple<int, int>(0, 1));
							drawRectangle(canvas, brush, brush, colCnt, row, index);
							symbol.ConditionValues[index] = -1;

						}

						// LT ST BULLISH 
						if (enableLong && value0.LTSTOB == TradeState.Current)
						{
							var index = view1 ? 4 : (rightInterval ? 40 : 4);
							Brush brush = Brushes.Transparent;
							brush = rev ? Brushes.DarkRed : Brushes.DarkGreen;
							addCount(index, row, new Tuple<int, int>(1, 0));
							drawRectangle(canvas, brush, brush, colCnt, row, index);
							symbol.ConditionValues[index] = (value0.LTSTOB == TradeState.Current) ? 2 : 1;
						}

						// LT ST BEARISH 
						if (enableShort && value0.LTSTOS == TradeState.Current)
						{
							var index = view1 ? 40 : (rightInterval ? 40 : 4);
							Brush brush = Brushes.Transparent;
							brush = rev ? Brushes.DarkGreen : Brushes.Red;
							addCount(index, row, new Tuple<int, int>(0, 1));
							drawRectangle(canvas, brush, brush, colCnt, row, index);
							symbol.ConditionValues[index] = (value0.LTSTOS == TradeState.Current) ? -2 : -1;
						}

						// MT ST BULLISH 
						if (enableLong && value0.MTSTOB == TradeState.Current)
						{
							var index = view1 ? 5 : (rightInterval ? 39 : 5);
							Brush brush = Brushes.Transparent;
							brush = rev ? Brushes.DarkRed : Brushes.DarkGreen;
							addCount(index, row, new Tuple<int, int>(1, 0));
							drawRectangle(canvas, brush, brush, colCnt, row, index);
							symbol.ConditionValues[index] = (value0.MTSTOB == TradeState.Current) ? 2 : 1;
						}

						// MT ST BEARISH 
						if (enableShort && value0.MTSTOS == TradeState.Current)
						{
							var index = view1 ? 39 : (rightInterval ? 39 : 5);
							Brush brush = Brushes.Transparent;
							brush = rev ? Brushes.DarkGreen : Brushes.DarkRed;
							addCount(index, row, new Tuple<int, int>(0, 1));
							drawRectangle(canvas, brush, brush, colCnt, row, index);
							symbol.ConditionValues[index] = (value0.MTSTOS == TradeState.Current) ? -2 : -1;
						}

						// ST ST BULLISH 
						if (enableLong && value0.STSTOB == TradeState.Current)
						{
							var index = view1 ? 6 : (rightInterval ? 38 : 6);
							Brush brush = Brushes.Transparent;
							brush = rev ? Brushes.DarkRed : Brushes.DarkGreen;
							addCount(index, row, new Tuple<int, int>(1, 0));
							drawRectangle(canvas, brush, brush, colCnt, row, index);
							symbol.ConditionValues[index] = (value0.STSTOB == TradeState.Current) ? 2 : 1;
						}

						// ST ST BEARISH 
						if (enableShort && value0.STSTOS == TradeState.Current)
						{
							var index = view1 ? 38 : (rightInterval ? 38 : 6);
							Brush brush = Brushes.Transparent;
							brush = rev ? Brushes.DarkGreen : Brushes.DarkRed;
							addCount(index, row, new Tuple<int, int>(0, 1));
							drawRectangle(canvas, brush, brush, colCnt, row, index);
							symbol.ConditionValues[index] = (value0.STSTOS == TradeState.Current) ? -2 : -1;
						}

						// LT FT GOING UP 
						if (enableLong && (value0.LTFTTU == TradeState.New || value0.LTFT[0] == 1))
						{
							var index = view1 ? 7 : (rightInterval ? 37 : 7);
							var brush1 = (value0.LTFTTU == TradeState.Current) ? Brushes.Lime : Brushes.DarkGreen;
							var brush2 = (value0.LTFTTU == TradeState.Current) ? Brushes.Red : Brushes.DarkRed;
							var brush = rev ? brush2 : brush1;
							addCount(index, row, new Tuple<int, int>(1, 0));
							drawRectangle(canvas, brush, brush, colCnt, row, index);
							symbol.ConditionValues[index] = (value0.LTFTTU == TradeState.Current) ? 2 : 1;
						}

						// LT FT GOING DN  
						if (enableShort && (value0.LTFTTD == TradeState.New || value0.LTFT[0] == -1))
						{
							var index = view1 ? 37 : (rightInterval ? 37 : 7);
							var brush1 = (value0.LTFTTD == TradeState.Current) ? Brushes.Red : Brushes.DarkRed;
							var brush2 = (value0.LTFTTD == TradeState.Current) ? Brushes.Lime : Brushes.DarkGreen;
							var brush = rev ? brush2 : brush1;
							addCount(index, row, new Tuple<int, int>(0, 1));
							drawRectangle(canvas, brush, brush, colCnt, row, index);
							symbol.ConditionValues[index] = (value0.LTFTTD == TradeState.Current) ? -2 : -1;
						}

						// MT FT GOING UP  
						if (enableLong && (value0.MTFTTU == TradeState.New || value0.MTFT[0] == 1))
						{
							var index = view1 ? 8 : (rightInterval ? 36 : 8);
							var brush1 = (value0.MTFTTU == TradeState.Current) ? Brushes.Lime : Brushes.DarkGreen;
							var brush2 = (value0.MTFTTU == TradeState.Current) ? Brushes.Red : Brushes.DarkRed;
							var brush = rev ? brush2 : brush1;
							addCount(index, row, new Tuple<int, int>(1, 0));
							drawRectangle(canvas, brush, brush, colCnt, row, index);
							symbol.ConditionValues[index] = (value0.MTFTTU == TradeState.Current) ? 2 : 1;
						}

						// MT FT GOING DN  
						if (enableShort && (value0.MTFTTD == TradeState.New || value0.MTFT[0] == -1))
						{
							var index = view1 ? 36 : (rightInterval ? 36 : 8);
							var brush1 = (value0.MTFTTD == TradeState.Current) ? Brushes.Red : Brushes.DarkRed;
							var brush2 = (value0.MTFTTD == TradeState.Current) ? Brushes.Lime : Brushes.DarkGreen;
							var brush = rev ? brush2 : brush1;
							addCount(index, row, new Tuple<int, int>(0, 1));
							drawRectangle(canvas, brush, brush, colCnt, row, index);
							symbol.ConditionValues[index] = (value0.MTFTTD == TradeState.Current) ? -2 : -1;
						}


						// ST FT GOING UP  
						if (enableLong && (value0.STFTTU == TradeState.New || value0.STFT[0] == 1))
						{
							var index = view1 ? 9 : (rightInterval ? 35 : 9);
							var brush1 = (value0.STFTTU == TradeState.Current) ? Brushes.Lime : Brushes.DarkGreen;
							var brush2 = (value0.STFTTU == TradeState.Current) ? Brushes.Red : Brushes.DarkRed;
							var brush = rev ? brush2 : brush1;
							addCount(index, row, new Tuple<int, int>(1, 0));
							drawRectangle(canvas, brush, brush, colCnt, row, index);
							symbol.ConditionValues[index] = (value0.STFTTU == TradeState.Current) ? 2 : 1;
						}

						// ST FT GOING DN 
						if (enableShort && (value0.STFTTD == TradeState.New || value0.STFT[0] == -1))
						{
							var index = view1 ? 35 : (rightInterval ? 35 : 9);
							var brush1 = (value0.STFTTD == TradeState.Current) ? Brushes.Red : Brushes.DarkRed;
							var brush2 = (value0.STFTTD == TradeState.Current) ? Brushes.Lime : Brushes.DarkGreen;
							var brush = rev ? brush2 : brush1;
							addCount(index, row, new Tuple<int, int>(0, 1));
							drawRectangle(canvas, brush, brush, colCnt, row, index);
							symbol.ConditionValues[index] = (value0.STFTTD == TradeState.Current) ? -2 : -1;
						}



						// Exh Short Exit a buy
						if (enableShort && value0.ExhUpColor != Colors.Transparent)
						{
							var index = view1 ? 11 : (rightInterval ? 33 : 11);
							addCount(index, row, new Tuple<int, int>(1, 0));
							var brush = new SolidColorBrush(value0.ExhUpColor);
							drawRectangle(canvas, brush, brush, colCnt, row, index);
							symbol.ConditionValues[index] = 1;
						}

						// Exh Short Exit a buy Nxt
						if (enableShort && value0.ExhUpColorNxt != Colors.Transparent)
						{
							var index = view1 ? 11 : (rightInterval ? 33 : 11);
							addCount(index, row, new Tuple<int, int>(1, 0));
							var brush = new SolidColorBrush(value0.ExhUpColorNxt);
							drawRectangle(canvas, brush, brush, colCnt, row, index);
							symbol.ConditionValues[index] = 1;
						}

						// EXH Long Exit a sell
						if (enableLong && value0.ExhDnColor != Colors.Transparent)
						{
							var index = view1 ? 33 : (rightInterval ? 33 : 11);
							addCount(index, row, new Tuple<int, int>(0, 1));
							var brush = new SolidColorBrush(value0.ExhDnColor);
							drawRectangle(canvas, brush, brush, colCnt, row, index);
							symbol.ConditionValues[index] = -1;
						}
						// EXH Long Exit a sell Nxt
						if (enableLong && value0.ExhDnColorNxt != Colors.Transparent)
						{
							var index = view1 ? 33 : (rightInterval ? 33 : 11);
							addCount(index, row, new Tuple<int, int>(0, 1));
							var brush = new SolidColorBrush(value0.ExhDnColorNxt);
							drawRectangle(canvas, brush, brush, colCnt, row, index);
							symbol.ConditionValues[index] = -1;
						}

						// Retrace Up  sretrace short
						if (ago == 0 && enableShort && value0.ShortRetUpColor != Colors.Transparent)
						{
							var index = view1 ? 12 : (rightInterval ? 32 : 12);
							addCount(index, row, new Tuple<int, int>(0, 1));
							var brush = new SolidColorBrush(value0.ShortRetUpColor);
							drawRectangle(canvas, brush, brush, colCnt, row, index);
							symbol.ConditionValues[index] = 1;
						}
						// Retrace Up  sretrace short Nxt
						if (ago == -1 && enableShort && value0.ShortRetUpColorNxt != Colors.Transparent)
						{
							var index = view1 ? 12 : (rightInterval ? 32 : 12);
							addCount(index, row, new Tuple<int, int>(0, 1));
							var brush = new SolidColorBrush(value0.ShortRetUpColorNxt);
							drawRectangle(canvas, brush, brush, colCnt, row, index);
							symbol.ConditionValues[index] = 1;
						}

						//  Retrace Dn lretrace long
						if (ago == 0 && enableLong && value0.LongRetDnColor != Colors.Transparent)
						{
							var index = view1 ? 32 : (rightInterval ? 32 : 12);
							addCount(index, row, new Tuple<int, int>(1, 0));
							var brush = new SolidColorBrush(value0.LongRetDnColor);
							drawRectangle(canvas, brush, brush, colCnt, row, index);
							symbol.ConditionValues[index] = -1;
						}

						//  Retrace Dn lretrace long Nxt
						if (ago == -1 && enableLong && value0.LongRetDnColorNxt != Colors.Transparent)
						{
							var index = view1 ? 32 : (rightInterval ? 32 : 12);
							addCount(index, row, new Tuple<int, int>(1, 0));
							var brush = new SolidColorBrush(value0.LongRetDnColorNxt);
							drawRectangle(canvas, brush, brush, colCnt, row, index);
							symbol.ConditionValues[index] = -1;
						}


						// A3 Up 
						if (enableLong && value0.Add3UpColor != Colors.Transparent)
						{
							var index = view1 ? 13 : (rightInterval ? 31 : 13);
							addCount(index, row, new Tuple<int, int>(1, 0));
							var brush = new SolidColorBrush(value0.Add3UpColor);
							drawRectangle(canvas, brush, brush, colCnt, row, index);
							symbol.ConditionValues[index] = 1;
						}
						// A3 Up Nxt
						if (enableLong && value0.Add3UpColorNxt != Colors.Transparent)
						{
							var index = view1 ? 13 : (rightInterval ? 31 : 13);
							addCount(index, row, new Tuple<int, int>(1, 0));
							var brush = new SolidColorBrush(value0.Add3UpColorNxt);
							drawRectangle(canvas, brush, brush, colCnt, row, index);
							symbol.ConditionValues[index] = 1;
						}

						// A3 Dn
						if (enableShort && value0.Add3DnColor != Colors.Transparent)
						{
							var index = view1 ? 31 : (rightInterval ? 31 : 13);
							addCount(index, row, new Tuple<int, int>(0, 1));
							var brush = new SolidColorBrush(value0.Add3DnColor);
							drawRectangle(canvas, brush, brush, colCnt, row, index);
							symbol.ConditionValues[index] = -1;
						}
						// A3 Dn Nxt
						if (enableShort && value0.Add3DnColorNxt != Colors.Transparent)
						{
							var index = view1 ? 31 : (rightInterval ? 31 : 13);
							addCount(index, row, new Tuple<int, int>(0, 1));
							var brush = new SolidColorBrush(value0.Add3DnColorNxt);
							drawRectangle(canvas, brush, brush, colCnt, row, index);
							symbol.ConditionValues[index] = -1;
						}

						// A2 Up 
						if (enableLong && value0.Add2UpColor != Colors.Transparent)
						{
							var index = view1 ? 14 : (rightInterval ? 30 : 14);
							addCount(index, row, new Tuple<int, int>(1, 0));
							var brush = new SolidColorBrush(value0.Add2UpColor);
							drawRectangle(canvas, brush, brush, colCnt, row, index);
							symbol.ConditionValues[index] = 1;
						}
						// A2 Up Nxt
						if (enableLong && value0.Add2UpColorNxt != Colors.Transparent)
						{
							var index = view1 ? 14 : (rightInterval ? 30 : 14);
							addCount(index, row, new Tuple<int, int>(1, 0));
							var brush = new SolidColorBrush(value0.Add2UpColorNxt);
							drawRectangle(canvas, brush, brush, colCnt, row, index);
							symbol.ConditionValues[index] = 1;
						}

						// A2 Dn
						if (enableShort && value0.Add2DnColor != Colors.Transparent)
						{
							var index = view1 ? 30 : (rightInterval ? 30 : 14);
							addCount(index, row, new Tuple<int, int>(0, 1));
							var brush = new SolidColorBrush(value0.Add2DnColor);
							drawRectangle(canvas, brush, brush, colCnt, row, index);
							symbol.ConditionValues[index] = -1;
						}
						// A2 Dn Nxt
						if (enableShort && value0.Add2DnColorNxt != Colors.Transparent)
						{
							var index = view1 ? 30 : (rightInterval ? 30 : 14);
							addCount(index, row, new Tuple<int, int>(0, 1));
							var brush = new SolidColorBrush(value0.Add2DnColorNxt);
							drawRectangle(canvas, brush, brush, colCnt, row, index);
							symbol.ConditionValues[index] = -1;
						}

						// A1 Up 
						if (enableLong && value0.Add1UpColor != Colors.Transparent)
						{
							var index = view1 ? 15 : (rightInterval ? 29 : 15);
							addCount(index, row, new Tuple<int, int>(1, 0));
							var brush = new SolidColorBrush(value0.Add1UpColor);
							drawRectangle(canvas, brush, brush, colCnt, row, index);
							symbol.ConditionValues[index] = 1;
						}

						// A1 Up Nxt
						if (enableLong && value0.Add1UpColorNxt != Colors.Transparent)
						{
							var index = view1 ? 15 : (rightInterval ? 29 : 15);
							addCount(index, row, new Tuple<int, int>(1, 0));
							var brush = new SolidColorBrush(value0.Add1UpColorNxt);
							drawRectangle(canvas, brush, brush, colCnt, row, index);
							symbol.ConditionValues[index] = 1;
						}


						// A1 Dn
						if (enableShort && value0.Add1DnColor != Colors.Transparent)
						{
							var index = view1 ? 29 : (rightInterval ? 29 : 15);
							addCount(index, row, new Tuple<int, int>(0, 1));
							var brush = new SolidColorBrush(value0.Add1DnColor);
							drawRectangle(canvas, brush, brush, colCnt, row, index);
							symbol.ConditionValues[index] = -1;
						}
						// A1 Dn Nxt
						if (enableShort && value0.Add1DnColorNxt != Colors.Transparent)
						{
							var index = view1 ? 29 : (rightInterval ? 29 : 15);
							addCount(index, row, new Tuple<int, int>(0, 1));
							var brush = new SolidColorBrush(value0.Add1DnColorNxt);
							drawRectangle(canvas, brush, brush, colCnt, row, index);
							symbol.ConditionValues[index] = -1;
						}

						// PoLong 
						if (enableLong && value0.PositionUpColor != Colors.Transparent)
						{
							var index = view1 ? 18 : (rightInterval ? 26 : 18);
							addCount(index, row, new Tuple<int, int>(1, 0));
							var brush = new SolidColorBrush(value0.PositionUpColor);
							drawRectangle(canvas, brush, brush, colCnt, row, index);
							symbol.ConditionValues[index] = 2;
						}

						// PoShort 
						if (enableShort && value0.PositionDnColor != Colors.Transparent)
						{
							var index = view1 ? 26 : (rightInterval ? 26 : 18);
							addCount(index, row, new Tuple<int, int>(0, 1));
							var brush = new SolidColorBrush(value0.PositionDnColor);
							drawRectangle(canvas, brush, brush, colCnt, row, index);
							symbol.ConditionValues[index] = -2;
						}

						// TrendUp SC
						if (enableLong)
						{
							var STct = value0.STft[0] + value0.STst[0] + value0.STsc[0];
							var newSc = value0.STsc[0] == 1 && value0.STsc[1] == 0;
							var oldSc = false;// value0.STsc[0] == 1 && value0.STsc[1] == 1;
							Brush brush = Brushes.Transparent;
							if (newSc) brush = rev ? Brushes.Red : Brushes.Lime;
							else if (oldSc) brush = rev ? Brushes.DarkRed : Brushes.DarkGreen;
							//else if (STct == 3) brush = Brushes.DarkGreen;
							//else if (STct == 2) brush = Brushes.Lime;
							//else if (STct == 1) brush = Brushes.GreenYellow;
							var index = view1 ? 16 : (rightInterval ? 28 : 16);
							if (newSc || oldSc) addCount(index, row, new Tuple<int, int>(1, 0));
							drawRectangle(canvas, brush, brush, colCnt, row, index);
							symbol.ConditionValues[index] = (newSc) ? 2 : oldSc ? 1 : 0;
						}

						// TrendDn SC
						if (enableShort)
						{
							var STct = value0.STft[0] + value0.STst[0] + value0.STsc[0];
							var newSc = value0.STsc[0] == -1 && value0.STsc[1] == 0;
							var oldSc = false; // value0.STsc[0] == -1 && value0.STsc[1] == -1;
							Brush brush = Brushes.Transparent;
							if (newSc) brush = rev ? Brushes.Lime : Brushes.Red;
							else if (oldSc) brush = rev ? Brushes.DarkGreen : Brushes.DarkRed;
							//else if (STct == -3) brush = Brushes.DarkRed;
							//else if (STct == -2) brush = Brushes.Red;
							//else if (STct == -1) brush = Brushes.Tomato;
							var index = view1 ? 28 : (rightInterval ? 28 : 16);
							if (newSc || oldSc) addCount(index, row, new Tuple<int, int>(0, 1));
							drawRectangle(canvas, brush, brush, colCnt, row, index);
							symbol.ConditionValues[index] = (newSc) ? -2 : oldSc ? -1 : 0;
						}

						// new pxf up for next bar 
						//if (enableLong && value0.stPxfLong == TradeState.CurrentBullish)
						//{
						//    var text = (value0.stTotalCount == 0) ? "" : Math.Round(100.0 * value0.stCorrectCount / value0.stTotalCount).ToString();

						//    var index = view1 ? 18 : (rightInterval ? 26 : 18);
						//    addCount(index, row, new Tuple<int, int>(1, 0));
						//    drawRectangle(canvas, Brushes.DarkGreen, Brushes.DarkGreen, colCnt, row, index, text);
						//    symbol.ConditionValues[index] = 1;
						//}

						//  new pxf dn for next bar  
						//if (enableShort && value0.stPxfShort == TradeState.CurrentBearish)
						//{
						//    var text = (value0.stTotalCount == 0) ? "" : Math.Round(100.0 * value0.stCorrectCount / value0.stTotalCount).ToString();

						//    var index = view1 ? 26 : (rightInterval ? 26  : 18);
						//    addCount(index, row, new Tuple<int, int>(0, 1));
						//    drawRectangle(canvas, Brushes.DarkRed, Brushes.DarkRed, colCnt, row, index, text);
						//    symbol.ConditionValues[index] = -1;
						//}

						if (enableLong && value0.ATRX[ago] > 0)
						{
							var index = view1 ? 17 : (rightInterval ? 27 : 17);
							addCount(index, row, new Tuple<int, int>(1, 0));
							drawRectangle(canvas, Brushes.Red, Brushes.Red, colCnt, row, index);
							symbol.ConditionValues[index] = 1;
						}

						// Short ATRX breach: close > short stop level → red
						if (enableShort && value0.ATRX[ago] < 0)
						{
							var index = view1 ? 27 : (rightInterval ? 27 : 17);
							addCount(index, row, new Tuple<int, int>(0, 1));
							drawRectangle(canvas, Brushes.Red, Brushes.Red, colCnt, row, index);
							symbol.ConditionValues[index] = -1;
						}

						//Exit Long Position 
						if (enableLong && value0.longAdvice == TradeState.Exit)
						{
							var index = view1 ? 19 : (rightInterval ? 25 : 19);
							addCount(index, row, new Tuple<int, int>(1, 0));
							drawRectangle(canvas, Brushes.Cyan, Brushes.Cyan, colCnt, row, index);
							symbol.ConditionValues[index] = 1;
						}

						//Exit Short Position 
						if (enableShort && value0.shortAdvice == TradeState.Exit)
						{
							var index = view1 ? 25 : (rightInterval ? 25 : 19);
							addCount(index, row, new Tuple<int, int>(0, 1));
							drawRectangle(canvas, Brushes.Magenta, Brushes.Magenta, colCnt, row, index);
							symbol.ConditionValues[index] = -1;
						}

						//New Long Position  
						if (enableLong && value0.longAdvice == TradeState.New)
						{
							var index = view1 ? 20 : (rightInterval ? 24 : 20);
							addCount(index, row, new Tuple<int, int>(1, 0));
							drawRectangle(canvas, Brushes.Lime, Brushes.Lime, colCnt, row, index);
							symbol.ConditionValues[index] = 1;
						}

						//New Short Position  
						if (enableShort && value0.shortAdvice == TradeState.New)
						{
							var index = view1 ? 24 : (rightInterval ? 24 : 20);
							addCount(index, row, new Tuple<int, int>(0, 1));
							drawRectangle(canvas, Brushes.Red, Brushes.Red, colCnt, row, index);
							symbol.ConditionValues[index] = -1;
						}

						//Long Position 
						if (enableLong && (value0.longAdvice == TradeState.Current))
						{
							var index = view1 ? 21 : (rightInterval ? 23 : 21);
							addCount(index, row, new Tuple<int, int>(1, 0));
							drawRectangle(canvas, Brushes.DarkGreen, Brushes.DarkGreen, colCnt, row, index);
							symbol.ConditionValues[index] = 1;
						}

						//Short Position 
						if (enableShort && (value0.shortAdvice == TradeState.Current))
						{
							var index = view1 ? 23 : (rightInterval ? 23 : 21);
							addCount(index, row, new Tuple<int, int>(0, 1));
							drawRectangle(canvas, Brushes.DarkRed, Brushes.DarkRed, colCnt, row, index);
							symbol.ConditionValues[index] = -1;
						}



					}

					else
					{
						canvas = TimeFramesSpreadsheet;
						colCnt = getColCount();
						int col = getColNumber(interval);
						if (col >= 0)
						{
							// drawRectangle(canvas, brush, colCnt, row, col);
						}
					}
				}
			}
		}

		private int getColNumber(string interval)
		{
			int col = 1;
			if (interval == "Monthly" || interval == "M") col = 2;
			else if (interval == "Weekly" || interval == "W") col = 3;
			else if (interval == "Daily" || interval == "D") col = 4;
			else if (interval == "240") col = 5;
			else if (interval == "120") col = 6;
			else if (interval == "60") col = 7;
			else if (interval == "30") col = 8;
			return col;
		}

		private int getRowNumber(string symbol)
		{
			return _memberList.FindIndex(x => x.Ticker == symbol);
		}

		private Series calculatePositionRatio(string symbol, string interval, int barCount)
		{
			Series output = new Series();

			Dictionary<string, List<DateTime>> times = new Dictionary<string, List<DateTime>>();
			Dictionary<string, Series[]> bars = new Dictionary<string, Series[]>();

			Dictionary<string, object> referenceData = new Dictionary<string, object>();

			string[] intervals = { interval, getInterval(interval, 1).Replace(" Min", "") };

			string indexSymbol = "";
			lock (_referenceData)
			{
				if (_referenceData.ContainsKey(symbol + ":" + "REL_INDEX"))
				{
					indexSymbol = _referenceData[symbol + ":" + "REL_INDEX"] as string;
				}
			}

			bool ok = true;
			foreach (string interval1 in intervals)
			{
				times[interval1] = (_barCache.GetTimes(symbol, interval1, 0, barCount));
				bars[interval1] = GetSeries(symbol, interval1, new string[] { "Open", "High", "Low", "Close" }, 0, barCount);
				if (times[interval1] == null || times[interval1].Count == 0)
				{
					ok = false;
				}
			}

			Series[] indexSeries = GetSeries(indexSymbol, intervals[0], new string[] { "Close" }, 0, barCount);
			if (indexSeries.Length > 0 && indexSeries[0].Count > 0)
			{
				referenceData["Index Prices : " + intervals[0]] = indexSeries[0];
			}

			if (ok)
			{
				string conditionName = "Position Ratio 1";
				if (getStrategy() == "Strategy 2") conditionName = "Position Ratio 2";

				output = Conditions.Calculate(conditionName, symbol, intervals, 1, times, bars, referenceData);  // Strategy 1 is Postion Ratio
			}
			return output;
		}

		private Series[] GetSeries(string symbol, string interval, string[] priceTypes, int extra, int barCount = BarServer.MaxBarCount)
		{
			var output = _barCache.GetSeries(symbol, interval, priceTypes, extra, barCount);

			var noValues = new List<bool>();
			var adjust = false;
			var closeIndex = -1;
			for (var ii = 0; ii < priceTypes.Length; ii++)
			{
				if (priceTypes[ii] == "Close")
				{
					closeIndex = ii;
				}

				noValues.Add(true);

				for (var jj = 0; jj < output[ii].Count; jj++)
				{
					if (!double.IsNaN(output[ii][jj]))
					{
						noValues[ii] = false;
						break;
					}
				}

				if (noValues[ii])
				{
					adjust = true;
				}
			}

			if (adjust)
			{
				Series close = null;
				if (closeIndex == -1)
				{
					var series1 = _barCache.GetSeries(symbol, interval, new string[] { "Close" }, extra, barCount);
					close = series1[0];
				}
				else
				{
					close = output[closeIndex];
				}
				for (var ii = 0; ii < noValues.Count; ii++)
				{
					if (noValues[ii])
					{
						output[ii] = close;
					}
				}
			}
			return output;
		}

		private Series calculateTSBSig(string symbol, string interval, int barCount)
		{
			Series output = new Series();

			Dictionary<string, List<DateTime>> times = new Dictionary<string, List<DateTime>>();
			Dictionary<string, Series[]> bars = new Dictionary<string, Series[]>();

			Dictionary<string, object> referenceData = new Dictionary<string, object>();

			string[] intervals = { interval, getInterval(interval, 1).Replace(" Min", "") };

			bool ok = true;
			foreach (string interval1 in intervals)
			{
				times[interval1] = (_barCache.GetTimes(symbol, interval1, 0, barCount));
				bars[interval1] = GetSeries(symbol, interval1, new string[] { "Open", "High", "Low", "Close" }, 0, barCount);
				if (times[interval1] == null || times[interval1].Count == 0)
				{
					ok = false;
				}
			}

			if (ok)
			{
				string conditionName = "TSB Signal";
				output = Conditions.Calculate(conditionName, symbol, intervals, 1, times, bars, referenceData);   // TSB
			}
			return output;
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

		public void Close()
		{
			_ourViewTimer.Tick -= new System.EventHandler(timer_Tick);
			_ourViewTimer.Stop();

			Trade.Manager.TradeEvent -= new TradeEventHandler(TradeEvent);  // event

			_portfolio.PortfolioChanged -= new PortfolioChangedEventHandler(portfolioChanged);

			//List<Alert> alerts = Alert.Manager.Alerts;
			//foreach (Alert alert in alerts)
			//{
			//    alert.FlashEvent -= new FlashEventHandler(alert_FlashEvent);
			//}

			_portfolio.Close();
			_barCache.Clear();

			_calcRunning = false;
			_calcThread.Join(100);

			removeCharts();

			setInfo();
		}

		private void SubgroupAction_MouseDown(object sender, MouseButtonEventArgs e)
		{
			Label label = sender as Label;

			string[] field = _nav3.Split(':');
			string group = field[0];
			string symbol = group + " Index";

			if (label.Name == "SubgroupChart1" || label.Name == "SubgroupChart2")
			{
				//BlpTerminal.RunFunction("GPO", symbol, "ASTGG");
			}
			else if (label.Name == "SubgroupMEMBERS1" || label.Name == "SubgroupMEMBERS2")
			{
				_clientPortfolioName = "";
				update(_selectedNav1, _selectedNav2, _selectedNav3, "MEMBERS", "", "");
				loadPortfolioInfo();
			}
		}

		private void TimeInterval_MouseDown(object sender, MouseButtonEventArgs e)
		{
			Label label = e.Source as Label;
			string time = label.Content as string;

			string name = label.Name;

			if (time == "D") time = "Daily";
			else if (time == "W") time = "Weekly";
			else if (time == "M") time = "Monthly";
			else if (time == "Q") time = "Quarterly";
			else if (time == "S") time = "SemiAnnually";
			else if (time == "Y") time = "Yearly";

			if (name[0] == 'L')
			{
				_interval1 = time;
				if (_chart1 != null)
				{
					changeChart(_chart1, _symbol, _interval1, getPortfolioName());
				}
				highlightIntervalButton(LTimeIntervals, _interval1.Replace(" Min", ""));
			}
			else if (name[0] == 'R')
			{
				_interval2 = time;
				if (_chart2 != null)
				{
					changeChart(_chart2, _symbol, _interval2, getPortfolioName());
				}
				highlightIntervalButton(RTimeIntervals, _interval2.Replace(" Min", ""));
			}
			else
			{
				_interval = time;

				adjustLabels();

				_interval1 = getInterval(_interval, 1);
				_interval2 = _interval;

				if (_chart1 != null)
				{
					changeChart(_chart1, _symbol, _interval1, getPortfolioName());
				}

				if (_chart2 != null)
				{
					changeChart(_chart2, _symbol, _interval2, getPortfolioName());
				}

				highlightIntervalButton(TimeIntervals, _interval.Replace(" Min", ""));
			}

			showView();
			hideNavigation();
			stopForecasting();
			calculateColors(true);
			_updateMonitorData = true;
		}

		private void showIntervalLabel(Label label, string interval1)
		{
			string name = label.Name;
			Brush brush = new SolidColorBrush(Color.FromRgb(0x00, 0xcc, 0xff));
			if (_view == "By CONDITION")
			{
				if (name.Substring(0, 3) == "Ago")
				{
					string interval2 = name.Substring(3);
					bool active = (interval1 == interval2);
					label.Content = (_ago == 0) ? "0" : (-_ago).ToString();
					label.Foreground = brush;
					label.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
				}
			}
			else if (_view == "By CONDITION 2")
			{
				if (name.Substring(0, 4) == "RAgo")
				{
					string interval2 = name.Substring(4);
					bool active = (interval1 == interval2);
					label.Content = (_ago == 0) ? "0" : (-_ago).ToString();
					label.Foreground = brush;
					label.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
				}
				if (name.Substring(0, 4) == "LAgo")
				{
					string interval2 = name.Substring(4);
					bool active = (interval1 == interval2);
					label.Content = (_agoLT == 0) ? "0" : (-_agoLT).ToString();
					label.Foreground = brush;
					label.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
				}
			}
		}

		private void highlightIntervalButton(Panel panel, string interval)
		{
			if (interval.Length > 0)
			{
				if (interval == "Monthly") interval = "M";
				else if (interval == "Yearly") interval = "Y";
				else if (interval == "SemiAnnually") interval = "S";
				else if (interval == "Quarterly") interval = "Q";
				else if (interval == "Weekly") interval = "W";
				else if (interval == "Daily") interval = "D";

				Brush brush1 = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));
				Brush brush2 = new SolidColorBrush(Color.FromRgb(0x00, 0xcc, 0xff));

				for (int ii = 1; ii < panel.Children.Count; ii++)
				{
					Label label = panel.Children[ii] as Label;
					if (label != null)
					{
						string name = label.Name;
						string text = label.Content as string;
						bool active = (interval == text);
						label.Foreground = active ? brush2 : brush1;

						showIntervalLabel(label, interval);
					}
				}
			}
		}

		private void ExportResults_MouseDown(object sender, MouseButtonEventArgs e)
		{
		}

		private void ExportHistoricalResults_MouseDown(object sender, MouseButtonEventArgs e)
		{

		}

		int[] _sizeSortType = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

		private void SpreadsheetGroupConditions_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			lock (_memberListLock)
			{
				Label label = sender as Label;
				if (label != null)
				{
					string name = label.Name;

					if (name == "ltTSBLong")
					{
						_memberList.Sort(new SortSymbol4(1, 1));

					}

					else if (name == "mtTSBLong")
					{
						_memberList.Sort(new SortSymbol4(2, 1));

					}

					else if (name == "stTSBLong")
					{
						_memberList.Sort(new SortSymbol4(3, 1));

					}
					else if (name == "ltSTtUp")
					{
						_memberList.Sort(new SortSymbol4(4, 1));

					}

					else if (name == "mtSTtUp")
					{
						_memberList.Sort(new SortSymbol4(5, 1));

					}

					else if (name == "stSTtUp")
					{
						_memberList.Sort(new SortSymbol4(6, 1));

					}

					else if (name == "ltFTUp")
					{
						_memberList.Sort(new SortSymbol4(7, 1));

					}

					else if (name == "mtFTUp")
					{
						_memberList.Sort(new SortSymbol4(8, 1));

					}

					else if (name == "stFTUp")
					{
						_memberList.Sort(new SortSymbol4(9, 1));

					}



					else if (name == "EXHLong")
					{
						_memberList.Sort(new SortSymbol4(11, 1));

					}

					else if (name == "RetraceDn")
					{
						_memberList.Sort(new SortSymbol4(12, 1));

					}

					else if (name == "TBLong")
					{
						_memberList.Sort(new SortSymbol4(13, 1));

					}

					else if (name == "SFTLong")
					{
						_memberList.Sort(new SortSymbol4(14, 1));

					}

					else if (name == "PALong")
					{
						_memberList.Sort(new SortSymbol4(15, 1));

					}
					else if (name == "PosLong")
					{
						_memberList.Sort(new SortSymbol4(18, 1));

					}
					else if (name == "stSCUp")
					{
						_memberList.Sort(new SortSymbol4(16, 1));

					}
					else if (name == "stPxfLong1")
					{
						_memberList.Sort(new SortSymbol4(18, 1));

					}
					else if (name == "ExitLong")
					{
						_memberList.Sort(new SortSymbol4(19, 1));

					}
					else if (name == "NewLong")
					{
						_memberList.Sort(new SortSymbol4(20, 1));

					}
					else if (name == "LongPos")
					{
						_memberList.Sort(new SortSymbol4(21, 1));

					}

					// Right Side Sell 
					else if (name == "ltTSBShort")
					{
						_memberList.Sort(new SortSymbol4(43, 2));

					}
					else if (name == "mtTSBShort")
					{
						_memberList.Sort(new SortSymbol4(42, 2));

					}
					else if (name == "stTSBShort")
					{
						_memberList.Sort(new SortSymbol4(41, 2));

					}
					else if (name == "ltSTtDn")
					{
						_memberList.Sort(new SortSymbol4(40, 2));

					}
					else if (name == "mtSTtDn")
					{
						_memberList.Sort(new SortSymbol4(39, 2));

					}
					else if (name == "stSTtDn")
					{
						_memberList.Sort(new SortSymbol4(38, 2));

					}
					else if (name == "ltFTDn")
					{
						_memberList.Sort(new SortSymbol4(37, 2));

					}
					else if (name == "mtFTDn")
					{
						_memberList.Sort(new SortSymbol4(36, 2));

					}
					else if (name == "stFTDn")
					{
						_memberList.Sort(new SortSymbol4(35, 2));

					}

					else if (name == "EXHShort")
					{
						_memberList.Sort(new SortSymbol4(33, 2));

					}
					else if (name == "RetraceUp")
					{
						_memberList.Sort(new SortSymbol4(32, 2));

					}
					else if (name == "TBShort")
					{
						_memberList.Sort(new SortSymbol4(31, 2));

					}
					else if (name == "SFTShort")
					{
						_memberList.Sort(new SortSymbol4(30, 2));

					}
					else if (name == "PAShort")
					{
						_memberList.Sort(new SortSymbol4(29, 2));

					}
					else if (name == "PosShort")
					{
						_memberList.Sort(new SortSymbol4(26, 2));

					}
					else if (name == "stSCDn")
					{
						_memberList.Sort(new SortSymbol4(28, 2));

					}
					else if (name == "stPxfShort1")
					{
						//_memberList.Sort(new SortSymbol4(26, 2));

					}
					else if (name == "ExitShort")
					{
						_memberList.Sort(new SortSymbol4(25, 2));

					}
					else if (name == "NewShort")
					{
						_memberList.Sort(new SortSymbol4(24, 2));

					}
					else if (name == "ShortPos")
					{
						_memberList.Sort(new SortSymbol4(23, 2));

					}

					else if (name == "NewLongMemberList")
					{
						_memberList.Sort(new SortSymbol6());

					}

					else if (name == "NewShortMemberList")
					{
						_memberList.Sort(new SortSymbol7());

					}
					else
					{
						_memberListSortType++;
						if (_memberListSortType >= 3) _memberListSortType = 0;
						_memberList.Sort(getSort());
					}
					SpreadsheetConditionsScroller.VerticalScrollBarVisibility = ScrollBarVisibility.Visible;
					SpreadsheetConditionsScroller.ScrollToTop();
					_updateSpreadsheet = 6;
				}
			}
		}

		private void PlusBar_Click(object sender, RoutedEventArgs e)
		{
			if ((sender as RadioButton).Name.Contains("V2")) _agoLT = -1;
			else _ago = -1;

			PlusTime.Visibility = Visibility.Visible;
			//CurrentTime.Visibility = Visibility.Collapsed;
			Ago1Time.Visibility = Visibility.Collapsed;
			Ago2Time.Visibility = Visibility.Collapsed;
			//AgoZero.Visibility = Visibility.Collapsed;
			AgoOnePlus.Visibility = Visibility.Visible;

			_updateSpreadsheet = 7; // ago change
		}
		private void CurrentBar_Click(object sender, RoutedEventArgs e)
		{
			if ((sender as RadioButton).Name.Contains("V2")) _agoLT = 0;
			else _ago = 0;

			PlusTime.Visibility = Visibility.Collapsed;
			//CurrentTime.Visibility = Visibility.Visible;
			Ago1Time.Visibility = Visibility.Collapsed;
			Ago2Time.Visibility = Visibility.Collapsed;
			//AgoZero.Visibility = Visibility.Visible;
			AgoOnePlus.Visibility = Visibility.Collapsed;

			_updateSpreadsheet = 8; // ago change
		}

		private void PreviousBar_Click(object sender, RoutedEventArgs e)
		{
			if ((sender as RadioButton).Name.Contains("V2")) _agoLT = 1;
			else _ago = 1;

			PlusTime.Visibility = Visibility.Collapsed;
			//CurrentTime.Visibility = Visibility.Collapsed;
			Ago1Time.Visibility = Visibility.Visible;
			Ago2Time.Visibility = Visibility.Collapsed;

			_updateSpreadsheet = 9; // sgo change
		}

		private void BarTwoAgo_Click(object sender, RoutedEventArgs e)
		{
			if ((sender as RadioButton).Name.Contains("V2")) _agoLT = 2;
			else _ago = 2;

			PlusTime.Visibility = Visibility.Collapsed;
			//CurrentTime.Visibility = Visibility.Collapsed;
			Ago1Time.Visibility = Visibility.Collapsed;
			Ago2Time.Visibility = Visibility.Visible;

			_updateSpreadsheet = 10; // sgo change
		}

		private void UserControl_Loaded(object sender, RoutedEventArgs e)
		{
			setHeights();

			// Register every alert button/label with the controller now that
			// the visual tree is fully built. FindName works reliably here
			// because we are in the same class as the XAML.
			if (_alertController != null)
			{
				_alertController.Register("FlexOne", FindName("BtnFlexOne") as Button, FindName("LblFlexOne") as TextBox);
				_alertController.Register("Bloomberg", FindName("BtnBloomberg") as Button, FindName("LblBloomberg") as TextBox);
				_alertController.Register("MktNeutral", FindName("BtnMktNeutral") as Button, FindName("LblMktNeutral") as TextBox);
				_alertController.Register("VolNeutral", FindName("BtnVolNeutral") as Button, FindName("LblVolNeutral") as TextBox);
				_alertController.Register("MaxPosition", FindName("BtnMaxPosition") as Button, FindName("LblMaxPosition") as TextBox);
				_alertController.Register("GrossBook", FindName("BtnGrossBook") as Button, FindName("LblGrossBook") as TextBox);
				_alertController.Register("NetExposure", FindName("BtnNetExposure") as Button, FindName("LblNetExposure") as TextBox);
				_alertController.Register("MaxPredVol", FindName("BtnMaxPredVol") as Button, FindName("LblMaxPredVol") as TextBox);
				_alertController.Register("MVaR95", FindName("BtnMVaR95") as Button, FindName("LblMVaR95") as TextBox);
				_alertController.Register("MaxVaR95", FindName("BtnMaxVaR95") as Button, FindName("LblMaxVaR95") as TextBox);
				// FactorRisk: placeholder — always green pending Fama-French factor attribution implementation
				if (FindName("BtnFactorRisk") is Button btnFactorRisk)
					btnFactorRisk.Foreground = Brushes.Lime;
				// Utilization — registered for label update even though circle is set by updateExtendedAlertCircles
				_alertController.Register("Utilization", FindName("BtnUtilization") as Button, FindName("LblUtilization") as TextBox);
				_alertController.Register("IdioRisk", FindName("BtnIdioRisk") as Button, FindName("LblIdioRisk") as TextBox);
				_alertController.Register("EqStress5", FindName("BtnEqStress5") as Button, FindName("LblEqStress5") as TextBox);
				_alertController.Register("EqStress10", FindName("BtnEqStress10") as Button, FindName("LblEqStress10") as TextBox);
				_alertController.Register("IntradayDD", FindName("BtnIntradayDD") as Button, FindName("LblIntradayDD") as TextBox);
				// Concentration: top-N
				_alertController.Register("Top5Long", FindName("BtnTop5Long") as Button, FindName("LblTop5Long") as TextBox);
				_alertController.Register("Top5Short", FindName("BtnTop5Short") as Button, FindName("LblTop5Short") as TextBox);
				_alertController.Register("Top10Long", FindName("BtnTop10Long") as Button, FindName("LblTop10Long") as TextBox);
				_alertController.Register("Top10Short", FindName("BtnTop10Short") as Button, FindName("LblTop10Short") as TextBox);
				// Liquidity
				_alertController.Register("ADV20", FindName("BtnADV20") as Button, FindName("LblADV20") as TextBox);
				_alertController.Register("ADV50", FindName("BtnADV50") as Button, FindName("LblADV50") as TextBox);
				_alertController.Register("ADV100", FindName("BtnADV100") as Button, FindName("LblADV100") as TextBox);
				// Stock Loan
				// Market cap tiers
				_alertController.Register("LargeCapGross", FindName("BtnLargeCapGross") as Button, FindName("LblLargeCapGross") as TextBox);
				_alertController.Register("LargeCapNet", FindName("BtnLargeCapNet") as Button, FindName("LblLargeCapNet") as TextBox);
				_alertController.Register("MidCapGross", FindName("BtnMidCapGross") as Button, FindName("LblMidCapGross") as TextBox);
				_alertController.Register("MidCapNet", FindName("BtnMidCapNet") as Button, FindName("LblMidCapNet") as TextBox);
				_alertController.Register("SmallCapGross", FindName("BtnSmallCapGross") as Button, FindName("LblSmallCapGross") as TextBox);
				_alertController.Register("SmallCapNet", FindName("BtnSmallCapNet") as Button, FindName("LblSmallCapNet") as TextBox);
				// CVaR95
				_alertController.Register("CVaR95", FindName("BtnCVaR95") as Button, FindName("LblCVaR95") as TextBox);

				_alertController.Start();  // paints initial state then starts poll timer
			}

			// Collapse all categories now that FindName has run and buttons are registered.
			// Category header buttons live inside ControlTemplate namescopes so we find them
			// via visual tree walk rather than FindName.
			collapseAllAlertCategories();
			updateExtendedAlertCircles();
			updateCategoryCircles();
		}

		bool _portfolioDependent = false;

		private void Dependent_Checked(object sender, RoutedEventArgs e)
		{
			if (!_portfolioDependent)
			{
				_portfolioDependent = true;
				_updateSpreadsheet = 11;
			}
		}

		private void Independent_Checked(object sender, RoutedEventArgs e)
		{
			if (_portfolioDependent)
			{
				_portfolioDependent = false;
				_updateSpreadsheet = 12;
			}
		}

		private void Against_Checked(object sender, RoutedEventArgs e)
		{
			if (_portfolioDependent)
			{
				_portfolioDependent = false;
				_updateSpreadsheet = 13;
			}
		}

		private void GlobalCost_MouseDown(object sender, MouseButtonEventArgs e)
		{
			Close();
			//_mainView.Content = new MarketMonitor (_mainView, ViewType.Map);
			_mainView.Content = new MarketMonitor(_mainView);
		}

		private void CloseHelp_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			TimingHelpSection.BringIntoView();
			ViewHelp.Visibility = Visibility.Collapsed;
			HelpON.Visibility = Visibility.Visible;
			HelpOFF.Visibility = Visibility.Collapsed;
		}

		private void Help_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			TimingHelpSection.BringIntoView();
			ViewHelp.Visibility = Visibility.Visible;
			HelpOFF.Visibility = Visibility.Visible;
			HelpON.Visibility = Visibility.Collapsed;
		}

		private void AlertLabel_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			// Single click: Red → Yellow (acknowledged) or Yellow → Red (restore).
			// Lime circles are ignored. The Tag on each TextBox holds the alert key.
			if (sender is TextBox tb && tb.Tag is string key)
				_alertController?.HandleLabelClick(key);
		}

		private void REFRESH_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			calculateColors(false);
		}

		bool _compareToSelection = false;

		public event PropertyChangedEventHandler PropertyChanged;

		private void NotifyPropertyChanged(string name)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
		}

		private void MarketView_Checked(object sender, RoutedEventArgs e)
		{
			if (MarketView.IsChecked == true)
			{
				if (Column19 != null)
				{
					_adviceView = false;

					MonitorGrid.Visibility = Visibility.Collapsed;
					AgoGrid.Visibility = Visibility.Visible;

					ConditionsGrid.Visibility = Visibility.Visible;

					//ColDef17.Width = new GridLength(0);
					//ColDef18.Width = new GridLength(0);
					ColDef19.Width = new GridLength(0);
					ColDef20.Width = new GridLength(0);
					ColDef21.Width = new GridLength(0);
					ColDef23.Width = new GridLength(0);
					ColDef24.Width = new GridLength(0);
					ColDef25.Width = new GridLength(0);

					//NewLongMemberList.Visibility = Visibility.Visible;
					//NewShortMemberList.Visibility = Visibility.Visible;

					_updateSpreadsheet = 14;
				}
			}
		}

		private void TradeIdeas_Checked(object sender, RoutedEventArgs e)
		{
			if (TradeIdeas.IsChecked == true)
			{
				if (Column19 != null)
				{
					_adviceView = true;

					MonitorGrid.Visibility = Visibility.Collapsed;
					ConditionsGrid.Visibility = Visibility.Visible;
					AgoGrid.Visibility = Visibility.Visible;

					//ColDef17.Width = new GridLength(1, GridUnitType.Star);
					//ColDef18.Width = new GridLength(1, GridUnitType.Star);
					ColDef19.Width = new GridLength(1, GridUnitType.Star);
					ColDef20.Width = new GridLength(1, GridUnitType.Star);
					ColDef21.Width = new GridLength(1, GridUnitType.Star);
					ColDef23.Width = new GridLength(1, GridUnitType.Star);
					ColDef24.Width = new GridLength(1, GridUnitType.Star);
					ColDef25.Width = new GridLength(1, GridUnitType.Star);

					//NewLongMemberList.Visibility = Visibility.Collapsed;
					//NewShortMemberList.Visibility = Visibility.Collapsed;

					_updateSpreadsheet = 15;
				}
			}
		}

		private void MonitorView_Checked(object sender, RoutedEventArgs e)
		{
			if (MonitorView.IsChecked == true)
			{
				AgoGrid.Visibility = Visibility.Collapsed;
				MonitorGrid.Visibility = Visibility.Visible;
				ConditionsGrid.Visibility = Visibility.Collapsed;
				drawMonitorMemberList();
				getMonitorData();
			}
		}

		private void initializePxfVof()
		{
			if (MainView.EnableNetworks == true)
			{
				if (Column1 != null)
				{
					_pxfvofView = true;

					//ColDef1.Width = new GridLength(1, GridUnitType.Star);
					//ColDef2.Width = new GridLength(1, GridUnitType.Star);
					//ColDef40.Width = new GridLength(1, GridUnitType.Star);
					//ColDef41.Width = new GridLength(1, GridUnitType.Star);

					_updateSpreadsheet = 16;
				}
			}
		}

		private void MLView_MouseDown(object sender, MouseButtonEventArgs e)
		{
			Close();
			_mainView.Content = new AutoMLView(_mainView);
		}

		private void TradeIdeas_MouseEnter(object sender, MouseEventArgs e)
		{
			var rb = sender as RadioButton;
			rb.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xcc, 0xff));
		}

		private void TradeIdeas_MouseLeave(object sender, MouseEventArgs e)
		{
			var rb = sender as RadioButton;
			rb.Foreground = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));
		}

		private void Mouse_Enter(object sender, MouseEventArgs e)
		{
			Label label = sender as Label;
			label.Background = Brushes.DimGray;
		}

		private void AODView_MouseDown(object sender, MouseButtonEventArgs e)
		{
			Close();
			_mainView.Content = new Charts(_mainView);
		}

		private void Mouse_Leave(object sender, MouseEventArgs e)
		{
			Label label = sender as Label;
			label.Background = new SolidColorBrush(Color.FromRgb(0x30, 0x30, 0x30));
		}

		private void GlobalMacro_MouseDown(object sender, MouseButtonEventArgs e)
		{
			Close();
			_mainView.Content = new MarketMonitor(_mainView);
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
}