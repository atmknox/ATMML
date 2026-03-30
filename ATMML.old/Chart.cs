using HedgeFundReporting;
using LoadingControl.Control;
using Microsoft.ML.Probabilistic.Collections;
using Microsoft.ML.Probabilistic.Models.Attributes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using static TorchSharp.torch.distributions.constraints;

namespace ATMML
{
	public delegate void ChartEventHandler(object sender, ChartEventArgs e);

	public enum ChartEventType
	{
		ExitCharts = 1,
		PartitionCharts = 2,
		ReloadData = 3,
		ToggleCursor = 4,
		PrintChart = 5,
		ChartMode = 6,
		Activate = 7,
		AODToggle = 8,
		ChangeDate = 9,
		TradeEvent = 100,
		ChangeSymbol = 101,
		SetupConditions = 102,
		ConfirmTrade = 103,
		ReviewSymbolChange = 104,
		OutsideResearch = 105,
		Calculator = 106,
		SettingChange = 107,
		PCMOrder = 108,
		Cursor = 109,
		ConfusionMatrix = 110,
		CursorTime = 111,
		Update = 112
	}

	public class ChartEventArgs : EventArgs
	{
		ChartEventType _id = ChartEventType.ExitCharts;

		public ChartEventArgs(ChartEventType id, DateTime dateTime = default(DateTime))
		{
			_id = id;
			DateTime = dateTime;
		}

		public ChartEventType Id
		{
			get { return _id; }
		}

		public DateTime DateTime { get; set; }
	}

	public class Parameter
	{
		public Parameter(string name, bool display)
		{
			Name = name;
			Display = display;
		}
		public virtual void FromString(string input)
		{
		}

		public string Name { get; set; }
		public bool Display { get; set; }
	}


	public class ColorParameter : Parameter
	{
		public ColorParameter(string name, Color value, bool display = true) : base(name, display)
		{
			Value = value;
		}

		public override string ToString()
		{
			return Value.ToString();
		}

		public override void FromString(string input)
		{
			Value = (Color)ColorConverter.ConvertFromString(input);
		}

		public Color Value { get; set; }
	}

	public class NumberParameter : Parameter
	{
		public NumberParameter(string name, double value, bool display = false) : base(name, display)
		{
			Value = value;
			MaxValue = double.MaxValue;
			MinValue = double.MinValue;
		}

		public override string ToString()
		{
			return Value.ToString();
		}

		public override void FromString(string input)
		{
			Value = double.Parse(input);
		}

		public double Value { get; set; }
		public double MaxValue { get; set; }
		public double MinValue { get; set; }
	}

	public class BooleanParameter : Parameter
	{
		public BooleanParameter(string name, bool value, bool display = false) : base(name, display)
		{
			Value = value;
		}
		public override string ToString()
		{
			var val = Value.ToString();
			return val;
		}

		public override void FromString(string input)
		{
			try
			{
				Value = bool.Parse(input);
			}
			catch
			{
			}
		}

		public bool Value { get; set; }
	}

	public class ChoiceParameter : Parameter
	{
		public ChoiceParameter(string name, string value, Dictionary<string, string> choices, bool display = false) : base(name, display)
		{
			Value = value;
			Choices = choices;
		}
		public override string ToString()
		{
			return Value;
		}

		public override void FromString(string input)
		{
			Value = input;
		}

		public string Value { get; set; } // key
		public Dictionary<string, string> Choices { get; set; }
	}

	public class Indicator
	{
		public Indicator(string name, bool enabled = false)
		{
			Name = name;
			Enabled = enabled;
			Parameters = new Dictionary<string, Parameter>();
		}
		public string Name { get; set; }
		public bool Enabled { get; set; }
		public Dictionary<string, Parameter> Parameters { get; set; }
	}

	public class Chart
	{
		private string _id;
		private Canvas _canvas;
		private Grid _controlPanel;
		private TreeView _tree;
		private StackPanel _title = new StackPanel();
		private TextBox _symbolEditor = new TextBox();
		private TextBlock _intervalText = new TextBlock();
		private ComboBox _comboBox = new ComboBox();
		private TextBlock _beta120Text = new TextBlock();
		private Matrix _matrix = new Matrix();
		private AOD _aod = new AOD();
		private List<string> _requestIds = new List<string>();
		private object _barLock = new object();
		private List<Bar> _bars = new List<Bar>();
		private int _startIndex = 0;
		private int _endIndex = 0;
		private int _desiredBarCount = 0;
		private double _timeBase = double.NaN;
		private double _timeScale = double.NaN;
		private int _forecastCount = 0;
		private int _leftMargin = 2;
		private int _rightMargin = 2;
		private double _fontSize = 10;
		private int _valueScaleWidth = 40;
		private int _timeScaleHeight = 11;
		private int _titleHeight = 16;
		private bool _scrollChart = false;
		private bool _changeTimeScale = false;
		private bool _changeValueScale = false;
		private int _changeValueScalePanelNumber = 0;
		private double _changeValueScaleOrigin = 0;
		private double _changeValueBaseOrigin = 0;
		private Point _scrollOrigin;
		private bool _clearEdit = true;
		private string _portfolioName = "SPY ETF";
		private string _symbolGroup = "S&P 500";
		private string _symbolName = "";
		private string _oldSymbolName = "";
		private string _symbolDescription = "";
		private string _symbol = "";
		private string _stkSymbol = "";
		private string _indexSymbol = "";
		private string _sectorSymbol = "";
		private string _sectorETFSymbol = "";
		private string _industryGroup = "";
		private double _symbolPrice = double.NaN;
		private double _sectorPrice = double.NaN;
		private double _sectorETFPrice = double.NaN;
		private string _interval = "Daily";
		private string _oldInterval = "";
		private Boolean _reload = false;
		private bool _busy = true;
		private List<Graph> _panels = new List<Graph>();
		private Point _mouseInfoPosition1;
		private Point _mouseInfoPosition2;
		private bool _mouseMove = false;
		private MouseInfo _cursorInfo = null;
		private DateTime _cursorTime = new DateTime();
		private Line _cursorVerticalLine = null;
		private Line _cursorVerticalLine2 = null;
		private double _cursorValue = double.NaN;
		private Line _cursorHorizontalLine = null;
		private TextBlock _cursorHorizontalText = null;
		private TextBlock _cursorVerticalText = null;
		private bool _cursorOn = true;
		private DateTime _mouseTime = new DateTime();
		private BezierCurve _bezierCurve = new BezierCurve();
		private List<Color> _colorList = new List<Color>();
		private List<int> _colorIndex = new List<int>();
		private BarCache _barCache;
		private DateTime _updateChartTime = DateTime.MinValue;
		private List<string> _updateChart = new List<string>();
		private Study _study = null;
		private bool _update = false;
		private Stopwatch _doubleClickStopwatch = null;
		private int _extraBars = 100;
		private int _barCount = 1000;
		private List<Chart> _linkedCharts = new List<Chart>();
		private List<Image> _controlBoxImages = new List<Image>();
		private bool _controlModifier = false;
		private bool _shiftModifier = false;
		private bool _propertiesChanged = false;
		private bool _displayPerformance = false;
		private bool _recordPerformance = false;
		private double _responseTime = 0;
		private double _calculationTime = 0;
		private Stopwatch _performanceStopwatch = null;
		private string[] _timeScaleFormats = { "", "MM/dd-HH:mm", "MM/dd", "yyyy/MM", "yyyy" };
		private bool _showCursor = true;
		private Color _foregroundColor = Colors.WhiteSmoke;
		private int _currentIndex = 0;
		private Portfolio _portfolio = new Portfolio(5);
		private Dictionary<string, object> _referenceData = null;
		private double _beta120;
		private double _sectorBeta;
		private double _sectorETFBeta;
		private Series _scores;
		private Series _units;
		private Series _nets;
		//private Series netSigHistory;
		private Series _PR;
		private Series _RP;
		private Series _RPP;
		private Series _VX;
		private Series _POS;
		private double _bestTargetHi;
		private double _bestTargetPrice;
		private double _bestTargetLo;
		private DateTime _expectedReportDate;
		private DateTime _expectedNxtDVDDate;
		private DateTime _expectedDVDExDate;
		private DateTime _expectedDVDPayDate;
		private bool _symbolDrop = false;
		private int _horizon = 0;
		private bool _tradeConfirmation = false;
		private bool _active = false;
		private string _strategy = "Strategy 1";
		private ModelPredictions _mpst = new ModelPredictions();
		private ModelPredictions _mpmt = new ModelPredictions();

		private double _conversionRate = 1.0;

		private List<int> _level1LeftValues = new List<int>();
		private List<int> _level1RightValues = new List<int>();
		private List<Color> _leftSideCursorColor = new List<Color>();
		private List<Color> _rightSideCursorColor = new List<Color>();

		private List<DateTime> _historicalEarnings = new List<DateTime>();
		private List<DateTime> _historicalDVD = new List<DateTime>();

		private Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<DateTime, double>>>> _predictions = new Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<DateTime, double>>>>();  // key = modelName:ticker:interval:ago  
		private Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<DateTime, double>>>> _actuals = new Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<DateTime, double>>>>();  // key = modelName:ticker:interval:ago  

		private List<string> _modelNames = new List<string>();

		Dictionary<string, Indicator> _indicators = new Dictionary<string, Indicator>();

		string[] _indicatorNames = { 
         // "ATM INDICATORS",
            "ATM Trend Bars",
			"ATM SC Bars",
			"ATM Trend Lines",
			"ATM Trigger",
			"ATM Targets",
			"ATM 3Sigma",

			"Current Volatility",
			"Historical Volatility",

			"ATM CHART CONDITIONS",
			"Mid Term Sector Turn",
			//"Mid Term P Turn",
			"Mid Term FT Turn",
			"Mid Term ST Turn",
			"Mid Term SC Start",
			"Short Term FT Lines",
			"Mid Term FT Lines",

            // "TECHNICAL INDICATORS"
            "ADX",
			"ATR",
			"BOL",
			"DMI",
			"D DIF",
			"MA 200",
			"MA 100",
			"MA 50",
			"MA 1",
			"MA 2",
			"MA 3",
			"MACD",
			"MOM",
			"OSC",
			"ROC",
			"RSI",

            // "ATM XSignals",          
            "X Alert",
			"First Alert",
			"Add On Alert",
			"New Trend",
			"Pressure",
			"Add",
			"2 Bar",
			"Exh",
			"Retrace",
			"ATRX",
			"Pullback Alert",
			"Exhaustion Alert",
			"Pressure Alert",
            //"PT Alert",
            "Two Bar Alert",
            //"Two Bar Trend",
            "FT Alert",
			"ST Alert",
			"FTST Alert",

        // ATM RESEARCH
            "Current Research",
			"Matrix",
			"Histogram",

            
        // ATM RESEARCH
            //"Current Research",
            //"PR",
            //"SC",            
            "FT | FT",
			"FT | P",
            //"FT || FT",
            "FT | SC",
            //"FT | SC PR",
            "FT | ST",
            //"FT | ST PR",
            "FT | TSB",
            //"FT | TSB PR",
            "SC | FT",
			"SC | P",
			"SC | SC",
            //"SC | SC PR",
            "SC | ST",
            //"SC | ST PR",
            "SC | TSB",
            //"SC | TSB PR",
            "ST | FT",
			"ST | SC",
            //"ST | SC PR",
            "ST | ST",
            //"ST | ST PR",
            "ST | TSB",
            //"ST | TSB PR",
            "TSB | FT",
			"TSB | SC",
            //"TSB | SC PR",
            "TSB | ST",
            //"TSB | ST PR",
            "TSB | TSB",
            //"TSB | TSB PR",
            //"Matrix",
            //"Histogram",
            
         // "ATM Turning Pts", 
            "Mid Term FT Current",
			"Short Term FT Current",
			"Short Term FT Nxt Bar",
			"Short Term FT Historical",
			"Short Term ST Current",
			"Short Term ST Nxt Bar",
			"Short Term ST Historical",

         // "ATM Cursor Color" - NOT USED
            "Use ATM FT 1 Time Frame Higher",
			"Use ATM FT 2 Time Frames Higher",
			"Use ATM ST 1 Time Frame Higher",
			"Use ATM ST 2 Time Frames Higher",

         // "ATM ELLIOTT WAVE",
            "EW Counts",
			"EW PTI",
			"EW Channels",
			"EW Projections",
			"EW Major Osc",  

         // "CMR Positions" - NOT USED
            "Positions",
			"Historical Positions",

         // "CMR Direction",
            "LT Direction",
			"MT Direction",
			"ST Direction",
			"ATM ML Forecasts",
			"ATM Trade Velocity", 

         // "NXT EARNINGS",
            "Highest Analysts Target Px",
			"Avg Analysts Target Px",
			"Lowest Analysts Target Px",
			"Next Date",
			"Nxt DVD Date",

			"Forecast",
			"Boundaries"

		};

		private DateTime _requestBarTime;

		System.Windows.Threading.DispatcherTimer _timer = null;

		// todo - move thread into the Study class
		private Thread _studyThread = null;

		public List<string> ModelNames
		{
			get
			{
				return _modelNames;
			}
			set
			{
				_modelNames = value;
				setPredictionHistogramEnable();
				lock (_updateChart)
				{
					_updateChart.Add(getDataInterval());
				}
			}
		}

		private void setPredictionHistogramEnable()
		{
			//var modelName = (_modelNames.Count > 0) ? _modelNames[0] : "NO PREDICTION";
			//var hasPredictions = MainView.HasPredictions(modelName, _symbol, getDataInterval());
			//if (modelName == "NO PREDICTION" || !hasPredictions) _indicators["Histogram"].Enabled = false;
			//else _indicators["Histogram"].Enabled = true;
			//updatePanels();
		}

		public string Strategy
		{
			get
			{
				return _strategy;
			}
			set
			{
				_strategy = value;
				lock (_updateChart)
				{
					_updateChart.Add(getDataInterval());
				}
			}
		}

		public string Symbol { get { return _symbol; } }
		public string SectorSymbol { get { return _sectorSymbol; } }
		public string SectorETFSymbol { get { return _sectorETFSymbol; } }
		public double SymbolPrice { get { return _symbolPrice; } }
		public double SectorPrice { get { return _sectorPrice; } }
		public double SectorETFPrice { get { return _sectorETFPrice; } }
		// todo
		public double SymbolBeta { get { return _beta120; } }
		public double SectorBeta { get { return _sectorBeta; } }
		public double SectorETFBeta { get { return _sectorETFBeta; } }

		public DateTime GetCursorTime()
		{
			return _cursorTime;
		}

		public string PortfolioName
		{
			get { return _portfolioName; }
			set
			{
				if (_portfolioName != value)
				{
					_portfolioName = value.Replace("\a", "");
					//updateProperties();
					_update = true;
				}
			}
		}

		public bool TradeConfirmation
		{
			get { return _tradeConfirmation; }
			set { _tradeConfirmation = value; }
		}

		public event ChartEventHandler ChartEvent;

		public Chart(Canvas canvas, Grid controlPanel, bool showCursor, string id = "")
		{
			_id = id;
			_canvas = canvas;
			Hide();
			_controlPanel = controlPanel;
			_controlPanel.Children.Clear();

			_aod.OrderEvent += onOrderEvent;

			// build control panel
			var rd1 = new RowDefinition();
			var rd2 = new RowDefinition();
			rd1.Height = new GridLength(1, GridUnitType.Star);
			rd2.Height = new GridLength(20, GridUnitType.Pixel);

			_controlPanel.RowDefinitions.Clear();
			_controlPanel.RowDefinitions.Add(rd1);
			_controlPanel.RowDefinitions.Add(rd2);

			_tree = new TreeView();
			_tree.Background = Brushes.Black;
			_tree.Foreground = Brushes.White;
			_tree.FontSize = 9;
			_tree.Margin = new Thickness(0, 0, 0, 0);
			_tree.BorderThickness = new Thickness(1);
			_tree.BorderBrush = new SolidColorBrush(Color.FromArgb(0xff, 0x12, 0x4B, 0x72));
			_tree.Focusable = false;

			_controlPanel.Children.Add(_tree);

			var stackPanel = new StackPanel();
			stackPanel.Orientation = Orientation.Horizontal;
			stackPanel.Background = new SolidColorBrush(Color.FromArgb(0xff, 0x12, 0x4B, 0x72));
			stackPanel.Margin = new Thickness(0, 0, 0, 0);
			Grid.SetRow(stackPanel, 1);

			var apply = new Label();
			apply.Content = "APPLY";
			apply.Foreground = Brushes.White;
			apply.ToolTip = "Apply Chosen Study to Chart";
			apply.FontSize = 9;
			apply.Margin = new Thickness(0, 0, 0, 0);
			apply.MouseEnter += Mouse_Enter;
			apply.MouseLeave += Mouse_Leave;
			apply.MouseDown += Apply_MouseDown;
			stackPanel.Children.Add(apply);

			var cancel = new Label();
			cancel.Content = "CANCEL";
			cancel.Foreground = Brushes.White;
			cancel.ToolTip = "Cancel and Close";
			cancel.FontSize = 9;
			cancel.MouseEnter += Mouse_Enter;
			cancel.MouseLeave += Mouse_Leave;
			cancel.MouseDown += Cancel_MouseDown;
			stackPanel.Children.Add(cancel);

			var clear = new Label();
			clear.Content = "CLEAR ALL";
			clear.Foreground = Brushes.White;
			clear.ToolTip = "Clear All Studies";
			clear.FontSize = 9;
			clear.MouseEnter += Mouse_Enter;
			clear.MouseLeave += Mouse_Leave;
			clear.MouseDown += ClearAll_MouseDown;
			stackPanel.Children.Add(clear);

			_controlPanel.Children.Add(stackPanel);

			_matrix.Visibility = Visibility.Collapsed;

			addPanel(0, "Main");

			_canvas.SetValue(Canvas.SnapsToDevicePixelsProperty, true);

			ShowCursor = showCursor;

			_canvas.Focusable = true;
			_canvas.Focus();

			_title.Orientation = Orientation.Horizontal;
			_symbolEditor.Visibility = Visibility.Visible;
			_symbolEditor.IsReadOnly = true;
			_symbolEditor.BorderThickness = new Thickness(0);
			_symbolEditor.FontSize = 10;
			_symbolEditor.CharacterCasing = CharacterCasing.Upper;
			_symbolEditor.Background = Brushes.Transparent;
			_symbolEditor.Foreground = new SolidColorBrush(_foregroundColor);
			_symbolEditor.AllowDrop = true;
			_symbolEditor.GotFocus += new RoutedEventHandler(_symbolEditor_GotFocus);
			_symbolEditor.LostFocus += new RoutedEventHandler(_symbolEditor_LostFocus);
			_symbolEditor.PreviewMouseLeftButtonUp += new MouseButtonEventHandler(_symbolEditor_PreviewMouseLeftButtonUp);
			_symbolEditor.PreviewMouseLeftButtonDown += new MouseButtonEventHandler(_symbolEditor_PreviewMouseLeftButtonDown);
			_symbolEditor.PreviewDrop += new DragEventHandler(_symbolEditor_PreviewDrop);
			_symbolEditor.PreviewKeyDown += symbolEditor_PreviewKeyDown;
			_symbolEditor.KeyDown += symbolEditor_KeyDown;

			_intervalText.Visibility = Visibility.Visible;
			_intervalText.Background = Brushes.Transparent;
			_intervalText.Foreground = new SolidColorBrush(_foregroundColor);
			_intervalText.Padding = new Thickness(5, 1, 0, 1);
			_intervalText.FontSize = 10;

			_comboBox.ItemsSource = Intervals;
			_comboBox.Width = 15;
			_comboBox.ToolTip = "Change Intervals";
			_comboBox.Padding = new Thickness(0, 1, 0, 1);
			_comboBox.Height = _titleHeight - 3;
			_comboBox.Margin = new Thickness(0, 0, 0, 0);
			_comboBox.FontSize = 7;
			_comboBox.Foreground = Brushes.White;
			_comboBox.Background = Brushes.Transparent;
			_comboBox.SelectionChanged += Interval_SelectionChanged;
			var style = Application.Current.Resources["ComboBoxStyle1"] as Style;
			_comboBox.Style = style;

			_beta120Text.Visibility = Visibility.Visible;
			_beta120Text.Background = Brushes.Transparent;
			_beta120Text.Foreground = new SolidColorBrush(_foregroundColor);
			_beta120Text.Padding = new Thickness(5, 1, 0, 1);
			_beta120Text.FontSize = 10;

			_title.Children.Add(_symbolEditor);
			_title.Children.Add(_intervalText);
			_title.Children.Add(_comboBox);

			_cursorInfo = new MouseInfo(2, _titleHeight + 3, _fontSize);
			//_cursorInfo.Visible = false;

			_canvas.SizeChanged += new SizeChangedEventHandler(SizeChanged);
			_canvas.MouseLeftButtonDown += new MouseButtonEventHandler(MouseLeftButtonDown);
			_canvas.MouseLeftButtonUp += new MouseButtonEventHandler(MouseLeftButtonUp);
			_canvas.MouseMove += new MouseEventHandler(MouseMove);
			_canvas.MouseEnter += new MouseEventHandler(MouseEnter);
			_canvas.MouseLeave += new MouseEventHandler(MouseLeave);
			_canvas.KeyDown += new KeyEventHandler(KeyDown);
			_canvas.KeyUp += new KeyEventHandler(KeyUp);
			_canvas.TextInput += new TextCompositionEventHandler(TextInput);

			_canvas.ContextMenuOpening += _canvas_ContextMenuOpening;
			_canvas.ContextMenuClosing += _canvas_ContextMenuClosing;

			initializeIndicators();

			Trade.Manager.TradeEvent += new TradeEventHandler(TradeEvent);

			_study = new Study(this);
			_barCache = new BarCache(barChanged);

			_portfolio.PortfolioChanged += new PortfolioChangedEventHandler(portfolioChanged);

			_timer = new System.Windows.Threading.DispatcherTimer();
			_timer.Interval = new TimeSpan(0, 0, 0, 0, 500); // 500 milliseconds 
			_timer.Tick += new EventHandler(OnTimer);
			_timer.Start();
		}

		public void processOrderEvent(object sender, OrderEventArgs e)
		{
			onOrderEvent(sender, e);
		}

		private void onOrderEvent(object sender, OrderEventArgs e)
		{
			var orderType = e.Type;
			if (orderType == OrderEventType.Long)
			{

			}
			else if (orderType == OrderEventType.LongAdd)
			{

			}
			else if (orderType == OrderEventType.LongReduce)
			{

			}
			else if (orderType == OrderEventType.LongClose)
			{

			}
			else if (orderType == OrderEventType.Short)
			{

			}
			else if (orderType == OrderEventType.ShortAdd)
			{

			}
			else if (orderType == OrderEventType.ShortReduce)
			{

			}
			else if (orderType == OrderEventType.ShortClose)
			{

			}
		}

		private void Cancel_MouseDown(object sender, MouseButtonEventArgs e)
		{
			_controlPanel.Visibility = Visibility.Collapsed;
			if (_analysisLabel != null)
			{
				_analysisLabel.Visibility = Visibility.Visible;
			}
		}
		private void ClearAll_MouseDown(object sender, MouseButtonEventArgs e)
		{
			setIndicators(_tree, new List<string>());
			if (_analysisLabel != null)
			{
				_analysisLabel.Visibility = Visibility.Visible;
			}
		}

		bool _displayWorking = false;
		Label _workingLabel = null;

		private void Apply_MouseDown(object sender, MouseButtonEventArgs e)
		{
			_displayWorking = true;
			if (_workingLabel != null)
			{
				_workingLabel.Visibility = Visibility.Visible;
			}

			_controlPanel.Visibility = Visibility.Collapsed;

			if (_analysisLabel != null)
			{
				_analysisLabel.Visibility = Visibility.Visible;
			}

			_propertiesChanged = true;
			_indicators.Values.ToList().ForEach(x => x.Enabled = false);
			var indicators = getIndicators(_tree);
			foreach (var x in _indicators) { x.Value.Enabled = indicators.Contains(x.Key); }

			updateTradeEvents();
			//updateProperties();
			updatePanels();

			string interval0 = GetOverviewInterval(_interval, 0);
			string interval1 = GetOverviewInterval(_interval, 1);

			lock (_updateChart)
			{
				_updateChart.Add(interval0 + "!");
				_updateChart.Add(interval1 + "!");
			}
			_update = true;
			onChartEvent(new ChartEventArgs(ChartEventType.SettingChange));
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

		private void Mouse_Score_Card_Leave(object sender, MouseEventArgs e)
		{
			Label label = sender as Label;
			label.Foreground = new SolidColorBrush(_scoreCardActive ? Color.FromRgb(0x00, 0xcc, 0xff) : Color.FromRgb(0xff, 0xff, 0xff));
		}

		bool _contextMenuOpen = false;

		void _canvas_ContextMenuClosing(object sender, ContextMenuEventArgs e)
		{
			_contextMenuOpen = false;
			//Debug.WriteLine("CLOSE");
		}

		void _canvas_ContextMenuOpening(object sender, ContextMenuEventArgs e)
		{
			_contextMenuOpen = true;
			//Debug.WriteLine("OPEN");
		}

		void _symbolEditor_PreviewDrop(object sender, DragEventArgs e)
		{
			_symbolEditor.Text = "";
			_symbolDrop = true;
		}

		void portfolioChanged(object sender, PortfolioEventArgs e)
		{
			if (e.Type == PortfolioEventType.ReferenceData)
			{
				_referenceData = e.ReferenceData;
				string symbol = e.Ticker;

				//System.Diagnostics.Debug.WriteLine(_symbol + " " + _interval + " " + "portfolio event reference count = " + _referenceData.Count);

				foreach (KeyValuePair<string, object> kvp in e.ReferenceData)
				{
					try
					{
						string name = kvp.Key;
						object value = kvp.Value;
						if (name == "BEST_TARGET_PRICE")
						{
							_bestTargetPrice = (double)value;
						}
						else if (name == "BEST_TARGET_HI")
						{
							_bestTargetHi = (double)value;
						}
						else if (name == "BEST_TARGET_LO")
						{
							_bestTargetLo = (double)value;
						}
						else if (name == "EXPECTED_REPORT_DT")
						{
							if (value is DateTime)
							{
								_expectedReportDate = (DateTime)value;
							}
						}
						else if (name == "BDVD_NEXT_EST_DECL_DT")
						{
							if (value is DateTime)
							{
								_expectedNxtDVDDate = (DateTime)value;
							}
						}
						else if (name == "BDVD_NEXT_EST_EX_DT")
						{
							if (value is DateTime)
							{
								_expectedDVDExDate = (DateTime)value;
							}
						}
						else if (name == "BDVD_NEXT_EST_PAY_DT")
						{
							if (value is DateTime)
							{
								_expectedDVDPayDate = (DateTime)value;
							}
						}
						else if (name == "EQY_BETA_6M")
						{
							if (symbol == _symbol)
							{
								_beta120 = (double)value;
							}
							else if (symbol == _sectorSymbol)
							{
								_sectorBeta = (double)value;
								onChartEvent(new ChartEventArgs(ChartEventType.Calculator));
							}
							else if (symbol == _sectorETFSymbol)
							{
								_sectorETFBeta = (double)value;
								onChartEvent(new ChartEventArgs(ChartEventType.Calculator));
							}
						}
						else if (name == "REL_INDEX")
						{
							_indexSymbol = (string)value;// + " Index";
							_barCache.RequestBars(_indexSymbol, getDataInterval(), true, _barCount, true);

							_sectorETFSymbol = _portfolio.getSectorETFSymbol((string)value);
							_barCache.RequestBars(_sectorETFSymbol, getDataInterval(), true, _barCount, true);
						}
						else if (name == "PR910")
						{
							try
							{
								double lastPrice = GetLastClose();
								double lastAdjustedPrice = (value is double) ? (double)value : lastPrice;

								if (!double.IsNaN(lastPrice) && !double.IsNaN(lastAdjustedPrice) && lastPrice != 0 && lastAdjustedPrice != 0)
								{
									_conversionRate = lastPrice / lastAdjustedPrice;
								}
							}
							catch (Exception x)
							{
								//Debug.WriteLine("Exception converting PR910: " + x.Message);
							}
						}
						else if (name == "GICS_INDUSTRY_GROUP_NAME")
						{
							_industryGroup = (string)value;
							//System.Diagnostics.Debug.WriteLine(_symbol + " " + _interval + " " + "gics industry group name = " + _industryGroup);
						}
						else if (name == "GICS_SECTOR")
						{
							int sectorNumber;
							if (int.TryParse(value.ToString(), out sectorNumber))
							{
								_sectorSymbol = _portfolio.getSectorSymbol(sectorNumber);
								_barCache.RequestBars(_sectorSymbol, getDataInterval(), true, _barCount, true);
							}
						}

						else if (name == "EARN_ANN_DT_TIME_HIST_WITH_EPS")
						{
							_historicalEarnings = _referenceData["EARN_ANN_DT_TIME_HIST_WITH_EPS"] as List<DateTime>;
						}
					}
					catch (Exception x)
					{
						//Debug.WriteLine("Fundamental data cast exception :" + x.Message);
					}
				}
				_update = true;
			}
		}

		public double GetLastClose()
		{
			double lastPrice = double.NaN;
			lock (_barLock)
			{
				if (_bars.Count > 0 && _currentIndex >= 0 && _currentIndex < _bars.Count)
				{
					for (int ii = _currentIndex; ii >= 0 && double.IsNaN(lastPrice); ii--)
					{
						lastPrice = _bars[ii].Close;
					}
				}
			}
			return lastPrice;
		}

		public Dictionary<string, Indicator> Indicators
		{
			get { return _indicators; }
		}

		private void initializeIndicators()
		{
			_indicators.Clear();
			foreach (string name in _indicatorNames)
			{
				var indicator = Conditions.CreateIndicator(name);
				_indicators[name] = indicator;
			}
		}

		public bool ShowCursor
		{
			get { return _showCursor; }
			set { _showCursor = value; if (!_showCursor) _cursorTime = new DateTime(); }
		}

		public int Horizon
		{
			get { return _horizon; }
			set { _horizon = value; }
		}

		private void setBarCount(int count)
		{
			int barCount = _endIndex - _startIndex;
			if (count != barCount)
			{
				int delta = count - barCount;
				changeBarSpacing(delta);
			}
		}

		private void addMissingIndicators()
		{
			foreach (string indicatorName in _indicatorNames)
			{
				if (!_indicators.ContainsKey(indicatorName))
				{

					_indicators[indicatorName] = new Indicator(indicatorName);
				}
			}
		}

		private bool hasIndicator(string name)
		{
			return _indicators.ContainsKey(name);
		}

		private void loadProperties(object info, Dictionary<string, Indicator> indicators)
		{
			string id = info as string;
			try
			{
				string name = "Chart_" + id;
				string data = MainView.GetSetting(name);

				if (data != null && data.Length > 0)
				{
					if (!data.Contains("\u0001"))
					{
						// old version
						string[] fields = data.Split(';');
						int fieldCount = fields.Length;
						foreach (string field in fields)
						{
							string[] nvp = field.Split('=');
							if (nvp.Length == 2)
							{
								string indicatorName = nvp[0];
								string indicatorValue = nvp[1];
								if (hasIndicator(indicatorName))
								{
									indicators[indicatorName].Enabled = (indicatorValue == "1");
								}
							}
						}
					}
					else
					{
						var lines = data.Split('\n');
						foreach (var line in lines)
						{
							if (line.Length > 0)
							{
								var level1 = line.Split('\u0001');
								var indicatorName = level1[0];
								var indicator = indicators[indicatorName];


								var properties = level1[1].Split('\u0002');
								foreach (var prop in properties)
								{
									if (prop.Length > 0)
									{
										var fields = prop.Split('\u0003');
										var key = fields[0];
										var value = fields[1];
										if (key == "Enable") indicator.Enabled = (value == "1");
										else
										{
											var parm = indicator.Parameters[key];
											parm.FromString(value);
										}
									}
								}
							}
						}
					}
				}
			}
			catch (Exception x)
			{
				//Trace.WriteLine(x.Message);
			}
		}

		private void saveProperties(object info)
		{
			if (_propertiesChanged)
			{
				_propertiesChanged = false;
				string id = info as string;
				try
				{
					string name = "Chart_" + id;
					string data = "";

					int indicatorCount = _indicatorNames.Length;
					for (int ii = 0; ii < indicatorCount; ii++)
					{
						string indicatorName = _indicatorNames[ii];
						var indicator = _indicators[indicatorName];

						data += indicatorName + "\u0001";
						data += ((getIndicatorEnable(indicatorName)) ? "Enable\u00031\u0002" : "Enable\u00030\u0002");
						foreach (var kvp in indicator.Parameters)
						{
							var key = kvp.Key;
							var parm = kvp.Value;

							var value = "";
							var colorParm = parm as ColorParameter;
							if (colorParm != null)
							{
								value = colorParm.Value.ToString();
							}
							var numberParm = parm as NumberParameter;
							if (numberParm != null)
							{
								value = numberParm.Value.ToString();
							}
							var choiceParm = parm as ChoiceParameter;
							if (choiceParm != null)
							{
								value = choiceParm.Value;
							}
							var booleanParm = parm as BooleanParameter;
							if (booleanParm != null)
							{
								value = booleanParm.Value.ToString();
							}

							data += key + "\u0003" + value + "\u0002";
						}
						data += "\n";
					}
					MainView.SaveSetting(name, data);
				}
				catch (Exception)
				{
				}
			}
		}

		public void LoadProperties(string id)
		{
			addMissingIndicators();

			loadProperties(id, _indicators);

			//updateProperties();
			updatePanels();

			lock (_updateChart)
			{
				_updateChart.Add(getDataInterval() + "!");
			}

			initializeIndicatorTree();
		}

		public void SaveProperties(string id)
		{
			saveProperties(id);
		}

		private void initializeIndicatorTree()
		{
			_tree.Items.Clear();

			_tree.Margin = new Thickness(0, 0, 0, 0);
			_tree.Padding = new Thickness(2);
			_tree.Foreground = Brushes.White;
			_tree.Background = Brushes.Black;
			_tree.FontSize = 9;
			_tree.BorderThickness = new Thickness(1);

			TreeViewItem item = new TreeViewItem();
			item.Margin = new Thickness(0);
			item.Padding = new Thickness(0);
			item.FontSize = 9;
			item.Foreground = Brushes.White;
			item.Background = Brushes.Black;
			_tree.Items.Add(item);
			item.Header = "ATM ANALYSIS";
			addSubTree(item, new string[]
			{
				"New Trend", "Pressure", "Add", "Retrace", "Exh", "ATRX"
				 //"New Trend", "ATM SC Bars", "Pressure", "Add", "2 Bar", "Retrace", "Exh"
			});

			item = new TreeViewItem();
			item.Margin = new Thickness(0);
			item.Padding = new Thickness(0);
			item.FontSize = 9;
			item.Foreground = Brushes.White;
			item.Background = Brushes.Black;
			_tree.Items.Add(item);
			item.Header = "ATM ALIGNMENT";
			addSubTree(item, new string[] {
				"None",
                //"SC",		    
				"FT | FT",
				"FT | P",
                //"FT || FT",
                "FT | SC",
                //"FT | SC PR",
                "FT | ST",
                //"FT | ST PR",
                "FT | TSB",
                //"FT | TSB PR",
                "SC | FT",
				"SC | P",
				"SC | SC",
                //"SC | SC PR",
                "SC | ST",
                //"SC | ST PR",
                "SC | TSB",
                //"SC | TSB PR",
                "ST | FT",
				"ST | SC",
                //"ST | SC PR",
                "ST | ST",
                //"ST | ST PR",
                "ST | TSB",
                //"ST | TSB PR",
                "TSB | FT",
				"TSB | SC",
                //"TSB | SC PR",
                "TSB | ST",
                //"TSB | ST PR",
                "TSB | TSB",
                //"TSB | TSB PR"
            });

			item = new TreeViewItem();
			item.Margin = new Thickness(0);
			item.Padding = new Thickness(0);
			item.FontSize = 9;
			item.Foreground = Brushes.White;
			item.Background = Brushes.Black;
			_tree.Items.Add(item);
			item.Header = "ATM CHART CONDITIONS";

			var shortTermInterval = GetOverviewInterval(_interval, 0);
			var midTermInterval = GetOverviewInterval(_interval, 1);
			var longTermInterval = GetOverviewInterval(midTermInterval, 1);

			var items = new string[] {
				"Mid Term Sector Turn",
				//"Mid Term P Turn",
				"Mid Term FT Turn",
				"Mid Term ST Turn",
				"Mid Term SC Start",
			 }.ToList();
			addSubTree(item, items.ToArray(), true);

			if (MainView.EnableFinancial)
			{
				//item = new TreeViewItem();
				//item.Margin = new Thickness(0);
				//item.Padding = new Thickness(0);
				//item.FontSize = 9;
				//item.Foreground = Brushes.White;
				//item.Background = Brushes.Black;
				//_tree.Items.Add(item);
				//item.Header = "CMR Actions";
				//addSubTree(item, new string[] {
				//"Expect Long Exit",
				//"",
				//"Prepare to Short",
				//"",
				//"Look to Add to Short",
				//"",
				//"Hold Short",
				//"",
				//"Out",
				//"",
				//"Hold Long",
				//"",
				//"Look to Add to Long",
				//"",
				//"Prepare to Buy",
				//"",
				//"Expect Short Exit" }, true);
			}

			item = new TreeViewItem();
			item.Margin = new Thickness(0);
			item.Padding = new Thickness(0);
			item.FontSize = 9;
			item.Foreground = Brushes.White;
			item.Background = Brushes.Black;

			_tree.Items.Add(item);
			item.Header = "ATM FT CHART LINES";

			items = new string[] { "Short Term FT Lines", "Mid Term FT Lines" }.ToList();
			addSubTree(item, items.ToArray(), true);


			item = new TreeViewItem();
			item.Margin = new Thickness(0);
			item.Padding = new Thickness(0);
			item.FontSize = 9;
			item.Foreground = Brushes.White;
			item.Background = Brushes.Black;
			_tree.Items.Add(item);
			item.Header = "ATM ELLIOTT WAVE";
			addSubTree(item, new string[] { "EW Counts", "EW PTI", "EW Channels", "EW Projections", "EW Major Osc" });

			item = new TreeViewItem();
			item.Margin = new Thickness(0);
			item.Padding = new Thickness(0);
			item.FontSize = 9;
			item.Foreground = Brushes.White;
			item.Background = Brushes.Black;

			_tree.Items.Add(item);
			item.Header = "ATM FORECAST";
			addSubTree(item, new string[] { "Forecast", "Boundaries" });

			item = new TreeViewItem();
			item.Margin = new Thickness(0);
			item.Padding = new Thickness(0);
			item.FontSize = 9;
			item.Foreground = Brushes.White;
			item.Background = Brushes.Black;
			item.Header = "ATM INDICATORS";
			addSubTree(item, new string[] { "ATM 3Sigma", "ATM SC Bars", "ATM Targets", "ATM Trend Bars", "ATM Trend Lines", "ATM Trade Velocity", "ATM Trigger" });
			_tree.Items.Add(item);

			item = new TreeViewItem();
			item.Margin = new Thickness(0);
			item.Padding = new Thickness(0);
			item.FontSize = 9;
			item.Foreground = Brushes.White;
			item.Background = Brushes.Black;

			//_tree.Items.Add(item);
			//item.Header = "ATM PREDICTION MATRIX";
			//addSubTree(item, new string[] {
			//    "Histogram"
			//});

			//item = new TreeViewItem();
			//item.Margin = new Thickness(0);
			//item.Padding = new Thickness(0);
			//item.FontSize = 9;
			//item.Foreground = Brushes.White;
			//item.Background = Brushes.Black;

			//_tree.Items.Add(item);
			//item.Header = "ATM RESEARCH";
			//items = new List<string> { "Current Research"};
			//addSubTree(item, items.ToArray());

			//item = new TreeViewItem();
			//item.Margin = new Thickness(0);
			//item.Padding = new Thickness(0);
			//item.FontSize = 9;
			//item.Foreground = Brushes.White;
			//item.Background = Brushes.Black;

			_tree.Items.Add(item);
			item.Header = "ATM TURNING POINTS";
			addSubTree(item, new string[]
			{
				"Mid Term FT Current",
				"Short Term FT Current",
				"Short Term FT Nxt Bar",
				"Short Term FT Historical",
				"Short Term ST Current",
				"Short Term ST Nxt Bar",
				"Short Term ST Historical"
			});

			item = new TreeViewItem();
			item.Margin = new Thickness(0);
			item.Padding = new Thickness(0);
			item.FontSize = 9;
			item.Foreground = Brushes.White;
			item.Background = Brushes.Black;

			_tree.Items.Add(item);
			item.Header = "ATM X ALERTS";
			items = new List<string> { "Add On Alert", "Exhaustion Alert", "First Alert", "Pressure Alert", "Pullback Alert", "Two Bar Alert", "X Alert", "FT Alert", "ST Alert" };

			//items = new List<string> { "Add On Alert","Exhaustion Alert", "First Alert", "Pressure Alert", "PT Alert", "Pullback Alert", "Two Bar Alert", "Two Bar Trend", "X Alert", "FT Alert", "ST Alert", "FTST Alert" };
			addSubTree(item, items.ToArray());

			if (MainView.EnableFinancial)
			{
				//item = new TreeViewItem();
				//item.Margin = new Thickness(0);
				//item.Padding = new Thickness(0);
				//item.FontSize = 9;
				//item.Foreground = Brushes.White;
				//item.Background = Brushes.Black;

				//_tree.Items.Add(item);
				//item.Header = "Outside Research";
				//addSubTree(item, new string[] { "Bullish", " ", "Bearish", " ", "Neutral" });
			}

			//if (MainView.EnableFinancial)
			//{
			//    item = new TreeViewItem();
			//    item.Margin = new Thickness(0);
			//    item.Padding = new Thickness(0);
			//    item.FontSize = 9;
			//    item.Foreground = Brushes.White;
			//    item.Background = Brushes.Black;

			//    _tree.Items.Add(item);
			//    item.Header = "ATM FORECAST";
			//    addSubTree(item, new string[] { "Forecast", " ", "Boundaries"});
			//}
			item = new TreeViewItem();
			item.Margin = new Thickness(0);
			item.Padding = new Thickness(0);
			item.FontSize = 9;
			item.Foreground = Brushes.White;
			item.Background = Brushes.Black;
			item.Header = "TECHNICAL INDICATORS";
			addSubTree(item, new string[] { "ADX", "ATR", "BOL", "DMI", "D DIF", "MA 200", "MA 100", "MA 50", "MA 1", "MA 2", "MA 3", "MACD", "MOM", "OSC", "ROC", "RSI" });
			_tree.Items.Add(item);

			item = new TreeViewItem();
			item.Margin = new Thickness(0);
			item.Padding = new Thickness(0);
			item.FontSize = 9;
			item.Foreground = Brushes.White;
			item.Background = Brushes.Black;

			_tree.Items.Add(item);
			item.Header = "EXPORTING";
			addSubTree(item, new string[] { "Clipboard", "Print" });

			//addSubTree(item, new string[] { "Print Chart", "Clipboard" });

			item = new TreeViewItem();
			item.Margin = new Thickness(0);
			item.Padding = new Thickness(0);
			item.FontSize = 9;
			item.Foreground = Brushes.White;
			item.Background = Brushes.Black;

			_tree.Items.Add(item);
			item.Header = "NXT EARNINGS";
			addSubTree(item, new string[] { "Next Date", }); //"Highest Analysts Target Px", "Avg Analysts Target Px", "Lowest Analysts Target Px",

			item = new TreeViewItem();
			item.Margin = new Thickness(0);
			item.Padding = new Thickness(0);
			item.FontSize = 9;
			item.Foreground = Brushes.White;
			item.Background = Brushes.Black;

			_tree.Items.Add(item);
			//item.Header = "NXT DVD";
			//addSubTree(item, new string[] { "Nxt DVD Date", }); //"Highest Analysts Target Px", "Avg Analysts Target Px", "Lowest Analysts Target Px",

			item = new TreeViewItem();
			item.Margin = new Thickness(0);
			item.Padding = new Thickness(0);
			item.FontSize = 9;
			item.Foreground = Brushes.White;
			item.Background = Brushes.Black;

			if (MainView.EnableRickStocks)
			{
				item = new TreeViewItem();
				item.Margin = new Thickness(0);
				item.Padding = new Thickness(0);
				item.FontSize = 9;
				item.Foreground = Brushes.White;
				item.Background = Brushes.Black;

				_tree.Items.Add(item);
			}

			if (MainView.EnableNetworks)
			{
				item = new TreeViewItem();
				item.Margin = new Thickness(0);
				item.Padding = new Thickness(0);
				item.FontSize = 9;
				item.Foreground = Brushes.White;
				item.Background = Brushes.Black;

				_tree.Items.Add(item);
			}
		}

		private List<IndicatorTreeViewItem> addSubTree(TreeViewItem parent, string[] names, bool action = false, bool click = true)
		{
			var output = new List<IndicatorTreeViewItem>();

			int count = names.Length;

			for (int ii = 0; ii < count; ii++)
			{
				string indicatorName = names[ii];

				//if (indicatorEnable)
				{
					var indicator = _indicators.ContainsKey(indicatorName) ? _indicators[indicatorName] : null;
					bool active = (indicator != null) ? getIndicatorEnable(indicatorName) : indicatorName == "None";

					var group = parent.Header as string;
					var useRadioButton = (group == "ATM ALIGNMENT");

					if (action)
					{
						string reviewInterval;
						ReviewAction reviewAction = MainView.GetReviewSymbolInterval(_portfolioName, _symbol, out reviewInterval).Action;
						if (reviewInterval.Length > 0)
						{
							if (_interval == reviewInterval)
							{
								if (indicatorName == "Expect Long Exit" && reviewAction == ReviewAction.ExpectLongExit) active = true;
								else if (indicatorName == "Expect Short Exit" && reviewAction == ReviewAction.ExpectShortExit) active = true;
								else if (indicatorName == "Look to Add to Short" && reviewAction == ReviewAction.AddShort) active = true;
								else if (indicatorName == "Look to Add to Long" && reviewAction == ReviewAction.AddLong) active = true;
								else if (indicatorName == "Prepare to Buy" && reviewAction == ReviewAction.AnticipateBuy) active = true;
								else if (indicatorName == "Prepare to Short" && reviewAction == ReviewAction.AnticipateShort) active = true;
								else if (indicatorName == "Hold Short" && reviewAction == ReviewAction.HoldPosition) active = true;
								else if (indicatorName == "Hold Long" && reviewAction == ReviewAction.HoldLong) active = true;
								else if (indicatorName == "Out" && reviewAction == ReviewAction.StayingOut) active = true;
							}
						}
					}

					string shortTermInterval = getDataInterval();
					string midTermInterval = GetOverviewInterval(_interval, 1);
					string longTermInterval = GetOverviewInterval(_interval, 2);

					var label = indicatorName.Replace("Short Term", shortTermInterval).Replace("Mid Term", midTermInterval).Replace("Long Term", longTermInterval);

					IndicatorTreeViewItem child = new IndicatorTreeViewItem(useRadioButton);
					child.Margin = new Thickness(0, 0, 0, 0);
					child.Name = indicatorName;
					child.HasToggleBox = click;

					StackPanel panel = new StackPanel();

					panel.Orientation = Orientation.Horizontal;
					panel.Margin = new Thickness(0);
					panel.Background = new SolidColorBrush(Colors.Black);

					if (useRadioButton)
					{
						var rb = child.ToggleButton as RadioButton;
						rb.GroupName = "Indicator Group 1";
						child.ToggleButton.Margin = new Thickness(0, 2, 2, 0);
						child.ToggleButton.Visibility = click ? Visibility.Visible : Visibility.Hidden;
						panel.Children.Add(child.ToggleButton);
					}
					else
					{
						child.ToggleButton.Margin = new Thickness(0, 2, 2, 0);
						child.ToggleButton.Visibility = click ? Visibility.Visible : Visibility.Hidden;
						panel.Children.Add(child.ToggleButton);
					}

					if (indicatorName == "Clipboard")
					{
						child.ToggleButton.Visibility = Visibility.Collapsed;
						child.MouseLeftButtonUp += Clipboard_MouseLeftButtonUp;
					}

					if (indicatorName == "Print")
					{
						child.ToggleButton.Visibility = Visibility.Collapsed;
						child.MouseLeftButtonUp += Print_Click;
					}

					child.IsChecked = active;

					TextBlock textBlock = new TextBlock();
					textBlock.FontSize = 9;
					textBlock.Padding = new Thickness(2);
					textBlock.Foreground = new SolidColorBrush(Colors.White);
					textBlock.Text = label;
					textBlock.Cursor = Cursors.Hand;
					panel.Children.Add(textBlock);

					if (indicator != null && indicator.Parameters.Count > 0)
					{
						Grid parmPanel = new Grid();
						ColumnDefinition c1 = new ColumnDefinition();
						ColumnDefinition c2 = new ColumnDefinition();
						parmPanel.ColumnDefinitions.Add(c1);
						parmPanel.ColumnDefinitions.Add(c2);

						var row = 0;
						foreach (var kvp in indicator.Parameters)
						{
							RowDefinition r1 = new RowDefinition();
							parmPanel.RowDefinitions.Add(r1);

							parmPanel.Margin = new Thickness(0);
							parmPanel.Background = new SolidColorBrush(Colors.Black);

							var parameterKey = kvp.Key;
							var parameter = kvp.Value;
							textBlock = new TextBlock();
							Grid.SetRow(textBlock, row);
							textBlock.FontSize = 9;
							textBlock.Padding = new Thickness(2);
							textBlock.Foreground = new SolidColorBrush(Colors.White);
							textBlock.VerticalAlignment = VerticalAlignment.Center;
							textBlock.Cursor = Cursors.Hand;
							textBlock.Text = parameter.Name;
							textBlock.Tag = indicatorName + "\u0001" + parameterKey;
							parmPanel.Children.Add(textBlock);

							var colorParameter = parameter as ColorParameter;
							if (colorParameter != null)
							{
								textBlock.Foreground = new SolidColorBrush(colorParameter.Value);
								textBlock.MouseLeftButtonDown += IndicatorColor_MouseDown;
							}

							var numberParameter = parameter as NumberParameter;
							if (numberParameter != null)
							{
								var textBox = new TextBox();
								Grid.SetRow(textBox, row);
								Grid.SetColumn(textBox, 1);
								textBox.Background = Brushes.Transparent;
								textBox.HorizontalAlignment = HorizontalAlignment.Right;
								textBox.Foreground = new SolidColorBrush(Colors.White);
								textBox.FontSize = 9;
								textBox.BorderBrush = Brushes.Black;
								textBox.Margin = new Thickness(0, 0, 0, 0);
								textBox.TextAlignment = TextAlignment.Right;
								textBox.Text = numberParameter.Value.ToString();
								textBox.Tag = indicatorName + "\u0001" + parameterKey;
								textBox.TextChanged += NumberParameter_TextChanged;
								parmPanel.Children.Add(textBox);
							}

							var choiceParameter = parameter as ChoiceParameter;
							if (choiceParameter != null)
							{
								var comboBox = new ComboBox();
								Grid.SetRow(comboBox, row);
								Grid.SetColumn(comboBox, 1);
								foreach (var x in choiceParameter.Choices) { comboBox.Items.Add(x.Value); }
								comboBox.Background = Brushes.Silver;
								comboBox.BorderBrush = Brushes.Transparent;
								comboBox.Foreground = new SolidColorBrush(Colors.Black);
								comboBox.FontSize = 9;
								comboBox.FontWeight = FontWeights.SemiBold;
								comboBox.Resources.Add(SystemColors.WindowBrushKey, Brushes.Silver);
								comboBox.Margin = new Thickness(0, 0, 0, 0);
								comboBox.HorizontalAlignment = HorizontalAlignment.Right;
								comboBox.Width = 75;
								comboBox.SelectedItem = choiceParameter.Value;
								comboBox.Tag = indicatorName + "\u0001" + parameterKey;
								comboBox.SelectionChanged += ChoiceParameter_SelectionChanged;
								parmPanel.Children.Add(comboBox);
							}

							var booleanParameter = parameter as BooleanParameter;
							if (booleanParameter != null)
							{
								var checkBox = new CheckBox();
								Grid.SetRow(checkBox, row);
								Grid.SetColumn(checkBox, 1);
								checkBox.Background = Brushes.White;
								checkBox.HorizontalAlignment = HorizontalAlignment.Right;
								checkBox.Foreground = new SolidColorBrush(Colors.White);
								checkBox.FontSize = 9;
								checkBox.BorderBrush = Brushes.White;
								checkBox.Margin = new Thickness(0, 0, 0, 0);
								checkBox.IsChecked = booleanParameter.Value;
								checkBox.Tag = indicatorName + "\u0001" + parameterKey;
								checkBox.Checked += BooleanParameter_Checked;
								checkBox.Unchecked += BooleanParameter_Checked;
								parmPanel.Children.Add(checkBox);
							}


							row++;
						}
						child.Items.Add(parmPanel);
					}

					child.Header = panel;

					parent.Items.Add(child);

					output.Add(child);
				}
			}

			return output;
		}

		private void Print_Click(object sender, MouseButtonEventArgs e)
		{
			PrintDialog printDialog = new PrintDialog();
			if (printDialog.ShowDialog() == true)
			{
				Print(printDialog);
			}
		}

		private void Clipboard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			CopyToClipboard();
		}

		private void BooleanParameter_Checked(object sender, RoutedEventArgs e)
		{
			var cb = sender as CheckBox;
			var key = cb.Tag as string;
			var fields = key.Split('\u0001');
			var indicatorName = fields[0];
			var parameterKey = fields[1];
			var indicator = _indicators[indicatorName];
			var parm = indicator.Parameters[parameterKey] as BooleanParameter;
			parm.Value = cb.IsChecked == true;
		}

		private void ChoiceParameter_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			var cb = sender as ComboBox;
			var key = cb.Tag as string;
			var fields = key.Split('\u0001');
			var indicatorName = fields[0];
			var parameterKey = fields[1];
			var indicator = _indicators[indicatorName];
			var parm = indicator.Parameters[parameterKey] as ChoiceParameter;
			parm.Value = cb.SelectedItem as string;
		}

		private void NumberParameter_TextChanged(object sender, TextChangedEventArgs e)
		{
			var tb = sender as TextBox;
			var key = tb.Tag as string;
			var fields = key.Split('\u0001');
			var indicatorName = fields[0];
			var parameterKey = fields[1];
			var indicator = _indicators[indicatorName];
			var parm = indicator.Parameters[parameterKey] as NumberParameter;
			var value = double.NaN;
			if (double.TryParse(tb.Text, out value))
			{
				parm.Value = value;
			}
		}

		private void IndicatorColor_MouseDown(object sender, MouseButtonEventArgs e)
		{
			var tb = sender as TextBlock;
			var key = tb.Tag as string;
			var cd = new System.Windows.Forms.ColorDialog();
			if (cd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
			{
				var color = cd.Color;
				var fields = key.Split('\u0001');
				var indicatorName = fields[0];
				var parameterKey = fields[1];
				var indicator = _indicators[indicatorName];
				var parm = indicator.Parameters[parameterKey] as ColorParameter;
				parm.Value = Color.FromArgb(color.A, color.R, color.G, color.B);
				tb.Foreground = new SolidColorBrush(parm.Value);
			}
		}
		private List<string> getIndicators(ItemsControl item)
		{
			var output = new List<string>();

			var indicatorItem = item as IndicatorTreeViewItem;

			if (indicatorItem != null)
			{
				bool isChecked = indicatorItem.IsChecked;
				if (isChecked)
				{
					output.Add(indicatorItem.Name as string);
				}
			}
			else
			{
				foreach (ItemsControl child in item.Items)
				{
					output.AddRange(getIndicators(child));
				}
			}
			return output;
		}

		private void setIndicators(ItemsControl item, List<string> inputs)
		{
			var indicatorItem = item as IndicatorTreeViewItem;

			if (indicatorItem != null)
			{
				var isChecked = inputs.Contains(indicatorItem.Name);
				indicatorItem.IsChecked = isChecked;
			}
			else
			{
				foreach (ItemsControl child in item.Items)
				{
					setIndicators(child, inputs);
				}
			}
		}

		private void addPCMOrder(string type, int size)
		{
			int orderSize = 0;
			if (size == 0)
			{
				TradeHorizon horizon = Trade.Manager.GetIntervalTradeHorizon(_interval);
				string portfolioName = _portfolioName;
				orderSize = Trade.Manager.getTradeOpenDirection(portfolioName, horizon, _symbol);
			}
			else
			{
				double lastPrice = GetLastClose();
				double price = lastPrice / _conversionRate;
				double dollar = 1000 * size;
				orderSize = 100 * (int)Math.Round(dollar / price / 100);
			}

			MainView.AddOrder(new Order(_symbol, _portfolioName, type, orderSize));
			onChartEvent(new ChartEventArgs(ChartEventType.PCMOrder));
		}

		private void removePCMOrder()
		{
			MainView.CancelOrder(_portfolioName, _symbol);
			onChartEvent(new ChartEventArgs(ChartEventType.PCMOrder));
		}

		private BitmapSource captureChart()
		{
			double dpiX = 300;
			double dpiY = 300;

			_canvas.Background = Brushes.White;
			_foregroundColor = Colors.Black;
			draw(true); // captureChart 1
			_intervalText.Foreground = new SolidColorBrush(_foregroundColor);
			_symbolEditor.Foreground = new SolidColorBrush(_foregroundColor);
			_title.UpdateLayout();
			_canvas.UpdateLayout();

			Visual target = _canvas;

			Rect bounds = VisualTreeHelper.GetDescendantBounds(target);
			RenderTargetBitmap rtb = new RenderTargetBitmap((int)(bounds.Width * dpiX / 96.0), (int)(bounds.Height * dpiY / 96.0), dpiX, dpiY, PixelFormats.Pbgra32);
			DrawingVisual dv = new DrawingVisual();
			using (DrawingContext ctx = dv.RenderOpen())
			{
				VisualBrush vb = new VisualBrush(target);
				ctx.DrawRectangle(vb, null, new Rect(new Point(), bounds.Size));
			}
			rtb.Render(dv);

			_canvas.Background = Brushes.Transparent;
			_foregroundColor = Colors.DarkGray;
			draw(); // captureChart 2
			_intervalText.Foreground = new SolidColorBrush(_foregroundColor);
			_symbolEditor.Foreground = new SolidColorBrush(_foregroundColor);
			_title.UpdateLayout();
			_canvas.UpdateLayout();

			return rtb;
		}

		public void CopyToClipboard()
		{
			BitmapSource source = captureChart();
			Clipboard.SetData(DataFormats.Bitmap, source);
		}

		public void Print(PrintDialog printDialog)
		{
			const double inch = 96;

			const double leftmargin = 0.5 * inch;
			const double topmargin = 0.5 * inch;

			var renderTarget = captureChart();
			var drawingVisual = new DrawingVisual();
			using (var drawingContext = drawingVisual.RenderOpen())
			{
				var rectangle = new Rect(
					leftmargin, topmargin,
					printDialog.PrintableAreaWidth - leftmargin * 2,
					printDialog.PrintableAreaHeight - topmargin * 2);

				drawingContext.DrawImage(renderTarget, rectangle);
			}
			printDialog.PrintVisual(drawingVisual, "CMR Chart");
		}

		public void Close()
		{
			if (_canvas != null)
			{
				int barCount = _endIndex - _startIndex;
				MainView.SaveSetting("BarCount" + _horizon.ToString(), barCount.ToString());

				_symbolEditor.GotFocus -= new RoutedEventHandler(_symbolEditor_GotFocus);
				_symbolEditor.LostFocus -= new RoutedEventHandler(_symbolEditor_LostFocus);
				_symbolEditor.PreviewMouseLeftButtonUp -= new MouseButtonEventHandler(_symbolEditor_PreviewMouseLeftButtonUp);
				_symbolEditor.PreviewMouseLeftButtonDown -= new MouseButtonEventHandler(_symbolEditor_PreviewMouseLeftButtonDown);
				_symbolEditor.PreviewDrop -= new DragEventHandler(_symbolEditor_PreviewDrop);

				_canvas.SizeChanged -= new SizeChangedEventHandler(SizeChanged);
				_canvas.MouseLeftButtonDown -= new MouseButtonEventHandler(MouseLeftButtonDown);
				_canvas.MouseLeftButtonUp -= new MouseButtonEventHandler(MouseLeftButtonUp);
				_canvas.MouseMove -= new MouseEventHandler(MouseMove);
				_canvas.MouseEnter -= new MouseEventHandler(MouseEnter);
				_canvas.MouseLeave -= new MouseEventHandler(MouseLeave);
				_canvas.KeyDown -= new KeyEventHandler(KeyDown);
				_canvas.KeyUp -= new KeyEventHandler(KeyUp);
				_canvas.TextInput -= new TextCompositionEventHandler(TextInput);

				Trade.Manager.TradeEvent -= new TradeEventHandler(TradeEvent);

				_timer.Tick += new EventHandler(OnTimer);
				_timer.Stop();
				_timer = null;

				waitForStudyThreadToComplete();

				_study.Close();

				foreach (Graph panel in _panels)
				{
					panel.Clear();
				}

				_aod.OrderEvent -= onOrderEvent;

				_requestIds.Clear();
				lock (_barLock)
				{
					_bars.Clear();
				}
				lock (_colorList)
				{
					_colorList.Clear();
					_colorIndex.Clear();
				}
				_updateChartTime = DateTime.MinValue;

				lock (_updateChart)
				{
					_updateChart.Clear();
				}

				_controlBoxImages.Clear();
				_linkedCharts.Clear();

				_barCache.Clear();

				_portfolio.Close();

				_canvas.Children.Clear();
				_referenceVerticalLine = null;
				_referenceHorizontalLine = null;
			}
		}


		bool _studyThreadStop = false;

		void waitForStudyThreadToComplete()
		{
			if (_studyThread != null)
			{
				_studyThreadStop = true;
				_studyThread.Join(100);
				_studyThread = null;
			}
		}

		void TradeEvent(object sender, TradeEventArgs e)
		{
			//_update = true;
		}

		void _symbolEditor_LostFocus(object sender, RoutedEventArgs e)
		{
			_symbolEditor.Background = Brushes.Transparent;
			_symbolEditor.Foreground = Brushes.Silver;
		}

		void _symbolEditor_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			_symbolEditor.SelectAll();
			_symbolEditor.Background = Brushes.Transparent;
			_symbolEditor.Foreground = Brushes.Silver;
		}

		void _symbolEditor_GotFocus(object sender, RoutedEventArgs e)
		{
			_symbolEditor.SelectAll();
			_symbolEditor.Background = Brushes.Transparent;
			_symbolEditor.Foreground = Brushes.Silver;
		}

		void _symbolEditor_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			if (!_symbolEditor.IsKeyboardFocusWithin)
			{
				_symbolEditor.Focus();
				e.Handled = true;
			}
		}

		public void AddLinkedChart(Chart chart)
		{
			_linkedCharts.Add(chart);
		}

		public void InitializeControlBox(List<string> images)
		{
			foreach (Image image in _controlBoxImages)
			{
				image.MouseLeftButtonDown -= new MouseButtonEventHandler(ControlBox_MouseLeftButtonDown);
			}
			_controlBoxImages.Clear();

			int count = images.Count;
			for (int ii = 0; ii < count; ii++)
			{
				string[] fields = images[ii].Split(':');
				string imageName = fields[0];
				string tooltip = (fields.Length > 1) ? fields[1] : "";

				Image image = new Image();
				BitmapImage bi1 = new BitmapImage();
				bi1.BeginInit();
				bi1.UriSource = new Uri(imageName, UriKind.RelativeOrAbsolute);
				bi1.EndInit();
				image.Stretch = Stretch.Fill;
				image.SetValue(Canvas.LeftProperty, 3.0 + (ii * 12));
				image.SetValue(Canvas.TopProperty, 4.0);
				image.Width = _titleHeight - 6;
				image.Height = _titleHeight - 6;
				image.Cursor = Cursors.Hand;
				image.Source = bi1;
				image.MouseLeftButtonDown += new MouseButtonEventHandler(ControlBox_MouseLeftButtonDown);
				image.Visibility = Visibility.Visible;
				image.Name = "ClickBox" + (ii + 1).ToString();
				if (tooltip.Length > 0)
				{
					image.ToolTip = tooltip;
				}
				_controlBoxImages.Add(image);
			}
		}

		private void barChanged(object sender, BarEventArgs e)
		{
			try
			{
				//if (e.Type != BarEventArgs.EventType.BarsLoaded)
				{
					string symbol = e.Ticker;
					string interval = e.Interval;

					if ((symbol == _symbol) || (symbol == _indexSymbol))
					{
						if (symbol == _symbol && interval == _interval)
						{
							if (e.Type == BarEventArgs.EventType.BarsLoaded || e.Type == BarEventArgs.EventType.BarsReceived)
							{
								loadBars();
								try
								{
									if (Application.Current != null) Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, (Action)delegate () { initialize(); });
								}
								catch (Exception x)
								{
									System.Diagnostics.Debug.WriteLine(x);
								}
							}
							else
							{
								updateBars();
							}
						}

						bool ok = false;
						lock (_updateChart)
						{
							ok = true;
							foreach (string findInterval in _updateChart)
							{
								if (interval == findInterval)
								{
									ok = false;
									break;
								}
							}
						}

						if (ok)
						{
							if (e.Type != BarEventArgs.EventType.BarsUpdated)
							{
								//string message = "Bar are ready for the " + _interval + " chart.  The bars are for " + symbol + " " + interval + " " + e.Type.ToString();
								//Debug.WriteLine(message);
							}

							if (_symbol == symbol)
							{
								lock (_updateChart)
								{
									_updateChart.Add(interval);
								}

							}
						}
					}

					if (symbol == _symbol)
					{
						_symbolPrice = _barCache.GetLastPrice(PriceType.Close, _symbol, getDataInterval(), 0);
						//onChartEvent(new ChartEventArgs(ChartEventType.Calculator));
					}

					if (symbol == _sectorETFSymbol)
					{
						_sectorETFPrice = _barCache.GetLastPrice(PriceType.Close, _sectorETFSymbol, getDataInterval(), 0);
						//onChartEvent(new ChartEventArgs(ChartEventType.Calculator));
					}
					else if (symbol == _sectorSymbol)
					{
						_sectorPrice = _barCache.GetLastPrice(PriceType.Close, _sectorSymbol, getDataInterval(), 0);
						//onChartEvent(new ChartEventArgs(ChartEventType.Calculator));
					}

					if (_symbol == symbol)
					{
						if (Application.Current != null && !_drawingClose) Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, (Action)delegate () { drawClose(); });
					}
				}
			}
			catch (Exception x)
			{
			}
		}

		private bool isRegressionModel()
		{
			bool regressionModel = false;
			if (_modelNames.Count > 0)
			{
				var modelName = _modelNames[0];
				var model = MainView.GetModel(modelName);
				if (model != null)
				{
					regressionModel = MainView.GetSenarioLabel(model.Scenario).Contains("PX");
				}
			}
			return regressionModel;
		}

		public void updatePanels()
		{
			bool pxfSigHistory = getIndicatorEnable("Histogram");

			bool adxStudy = getIndicatorEnable("ADX");
			bool macdStudy = getIndicatorEnable("MACD");
			bool dmiStudy = getIndicatorEnable("DMI");
			bool ddmiStudy = getIndicatorEnable("D DIF");
			bool atrStudy = getIndicatorEnable("ATR");
			bool momStudy = getIndicatorEnable("MOM");
			bool oscStudy = getIndicatorEnable("OSC");
			bool rocStudy = getIndicatorEnable("ROC");
			bool rsiStudy = getIndicatorEnable("RSI");
			bool triggerStudy = getIndicatorEnable("ATM Trigger");
			bool oscillatorStudy = getIndicatorEnable("EW Major Osc");
			bool predictionStudy = (pxfSigHistory && !isRegressionModel());
			bool scoreStudy = false;
			bool indicatorStudy1 = getIndicatorEnable("FT | SC PR");
			bool indicatorStudy2 = getIndicatorEnable("FT | FT");
			bool indicatorStudy3 = getIndicatorEnable("ST | SC PR");
			bool indicatorStudy4 = getIndicatorEnable("| Net");
			bool indicatorStudy5 = getIndicatorEnable("Historical Net FT");
			bool indicatorStudy6 = getIndicatorEnable("Historical Net TSB");
			bool indicatorStudy7 = getIndicatorEnable("Historical Net ST TSB");
			bool indicatorStudy8 = getIndicatorEnable("FT | SC");
			bool indicatorStudy9 = getIndicatorEnable("FT | ST");
			bool indicatorStudy10 = getIndicatorEnable("FT | TSB");
			bool indicatorStudy11 = getIndicatorEnable("ST | FT");
			bool indicatorStudy12 = getIndicatorEnable("ST | SC");
			bool indicatorStudy13 = getIndicatorEnable("ST | ST");
			bool indicatorStudy14 = getIndicatorEnable("ST | TSB");
			bool indicatorStudy15 = getIndicatorEnable("SC | FT");
			bool indicatorStudy16 = getIndicatorEnable("SC | SC");
			bool indicatorStudy17 = getIndicatorEnable("SC | ST");
			bool indicatorStudy18 = getIndicatorEnable("SC | TSB");
			bool indicatorStudy19 = getIndicatorEnable("TSB | FT");
			bool indicatorStudy20 = getIndicatorEnable("TSB | SC");
			bool indicatorStudy21 = getIndicatorEnable("TSB | ST");
			bool indicatorStudy22 = getIndicatorEnable("TSB | TSB");
			bool indicatorStudy23 = getIndicatorEnable("FT | ST PR");
			bool indicatorStudy24 = getIndicatorEnable("FT | TSB PR");
			bool indicatorStudy25 = getIndicatorEnable("ST | ST PR");
			bool indicatorStudy26 = getIndicatorEnable("ST | TSB PR");
			bool indicatorStudy27 = getIndicatorEnable("SC | SC PR");
			bool indicatorStudy28 = getIndicatorEnable("SC | ST PR");
			bool indicatorStudy29 = getIndicatorEnable("SC | TSB PR");
			bool indicatorStudy30 = getIndicatorEnable("TSB | SC PR");
			bool indicatorStudy31 = getIndicatorEnable("TSB | ST PR");
			bool indicatorStudy32 = getIndicatorEnable("TSB | TSB PR");
			bool indicatorStudy33 = getIndicatorEnable("SC");
			bool indicatorStudy34 = getIndicatorEnable("PR");
			bool indicatorStudy35 = getIndicatorEnable("FT | P");
			bool indicatorStudy36 = getIndicatorEnable("FT || FT");
			bool indicatorStudy37 = getIndicatorEnable("SC | P");

			int adxIndex = GetPanelIndex("ADX");
			int macdIndex = GetPanelIndex("MACD");
			int dmiIndex = GetPanelIndex("DMI");
			int ddmiIndex = GetPanelIndex("D DIF");
			int atrIndex = GetPanelIndex("ATR");
			int momIndex = GetPanelIndex("MOM");
			int oscIndex = GetPanelIndex("OSC");
			int rocIndex = GetPanelIndex("ROC");
			int rsiIndex = GetPanelIndex("RSI");
			int triggerIndex = GetPanelIndex("TRIGGER");
			int oscillatorIndex = GetPanelIndex("OSCILLATOR");
			int predictionIndex = GetPanelIndex("PREDICTION");
			int scoreIndex = GetPanelIndex("Score");
			int indicatorIndex1 = GetPanelIndex("FT | SC PR");
			int indicatorIndex2 = GetPanelIndex("FT | FT");
			int indicatorIndex3 = GetPanelIndex("ST | SC PR");
			int indicatorIndex4 = GetPanelIndex("Indicator4");
			int indicatorIndex5 = GetPanelIndex("Indicator5");
			int indicatorIndex6 = GetPanelIndex("Indicator6");
			int indicatorIndex7 = GetPanelIndex("Indicator7");
			int indicatorIndex8 = GetPanelIndex("FT | SC");
			int indicatorIndex9 = GetPanelIndex("FT | ST");
			int indicatorIndex10 = GetPanelIndex("FT | TSB");
			int indicatorIndex11 = GetPanelIndex("ST | FT");
			int indicatorIndex12 = GetPanelIndex("ST | SC");
			int indicatorIndex13 = GetPanelIndex("ST | ST");
			int indicatorIndex14 = GetPanelIndex("ST | TSB");
			int indicatorIndex15 = GetPanelIndex("SC | FT");
			int indicatorIndex16 = GetPanelIndex("SC | SC");
			int indicatorIndex17 = GetPanelIndex("SC | ST");
			int indicatorIndex18 = GetPanelIndex("SC | TSB");
			int indicatorIndex19 = GetPanelIndex("TSB | FT");
			int indicatorIndex20 = GetPanelIndex("TSB | SC");
			int indicatorIndex21 = GetPanelIndex("TSB | ST");
			int indicatorIndex22 = GetPanelIndex("TSB | TSB");
			int indicatorIndex23 = GetPanelIndex("FT | ST PR");
			int indicatorIndex24 = GetPanelIndex("FT | TSB PR");
			int indicatorIndex25 = GetPanelIndex("ST | ST PR");
			int indicatorIndex26 = GetPanelIndex("ST | TSB PR");
			int indicatorIndex27 = GetPanelIndex("SC | SC PR");
			int indicatorIndex28 = GetPanelIndex("SC | ST PR");
			int indicatorIndex29 = GetPanelIndex("SC | TSB PR");
			int indicatorIndex30 = GetPanelIndex("TSB | SC PR");
			int indicatorIndex31 = GetPanelIndex("TSB | ST PR");
			int indicatorIndex32 = GetPanelIndex("TSB | TSB PR");
			int indicatorIndex33 = GetPanelIndex("SC");
			int indicatorIndex34 = GetPanelIndex("PR");
			int indicatorIndex35 = GetPanelIndex("FT | P");
			int indicatorIndex36 = GetPanelIndex("FT || FT");
			int indicatorIndex37 = GetPanelIndex("SC | P");

			if (momStudy && momIndex == -1)
			{
				int panelCount = getPanelCount();
				addPanel(panelCount, "MOM");
			}
			else if (!momStudy && momIndex != -1)
			{
				removePanel("MOM");
			}

			if (rsiStudy && rsiIndex == -1)
			{
				int panelCount = getPanelCount();
				addPanel(panelCount, "RSI");
			}
			else if (!rsiStudy && rsiIndex != -1)
			{
				removePanel("RSI");
			}

			if (adxStudy && adxIndex == -1)
			{
				int panelCount = getPanelCount();
				addPanel(panelCount, "ADX");
			}
			else if (!adxStudy && adxIndex != -1)
			{
				removePanel("ADX");
			}

			if (macdStudy && macdIndex == -1)
			{
				int panelCount = getPanelCount();
				addPanel(panelCount, "MACD");
			}
			else if (!macdStudy && macdIndex != -1)
			{
				removePanel("MACD");
			}

			if (dmiStudy && dmiIndex == -1)
			{
				int panelCount = getPanelCount();
				addPanel(panelCount, "DMI");
			}
			else if (!dmiStudy && dmiIndex != -1)
			{
				removePanel("DMI");
			}

			if (ddmiStudy && ddmiIndex == -1)
			{
				int panelCount = getPanelCount();
				addPanel(panelCount, "D DIF");
			}
			else if (!ddmiStudy && ddmiIndex != -1)
			{
				removePanel("D DIF");
			}

			if (atrStudy && atrIndex == -1)
			{
				int panelCount = getPanelCount();
				addPanel(panelCount, "ATR");
			}
			else if (!atrStudy && atrIndex != -1)
			{
				removePanel("ATR");
			}

			if (oscStudy && oscIndex == -1)
			{
				int panelCount = getPanelCount();
				addPanel(panelCount, "OSC");
			}
			else if (!oscStudy && oscIndex != -1)
			{
				removePanel("OSC");
			}

			if (rocStudy && rocIndex == -1)
			{
				int panelCount = getPanelCount();
				addPanel(panelCount, "ROC");
			}
			else if (!rocStudy && rocIndex != -1)
			{
				removePanel("ROC");
			}

			if (triggerStudy && triggerIndex == -1)
			{
				int panelCount = getPanelCount();
				addPanel(panelCount, "TRIGGER");
			}
			else if (!triggerStudy && triggerIndex != -1)
			{
				removePanel("TRIGGER");
			}

			if (oscillatorStudy && oscillatorIndex == -1)
			{
				int panelCount = getPanelCount();
				addPanel(panelCount, "OSCILLATOR");
			}
			else if (!oscillatorStudy && oscillatorIndex != -1)
			{
				removePanel("OSCILLATOR");
			}

			if (predictionStudy && predictionIndex == -1)
			{
				int panelCount = getPanelCount();
				addPanel(panelCount, "PREDICTION");
			}
			else if (!predictionStudy && predictionIndex != -1)
			{
				removePanel("PREDICTION");
			}

			if (scoreStudy && scoreIndex == -1)
			{
				int panelCount = getPanelCount();
				addPanel(panelCount, "Score");
			}
			else if (!scoreStudy && scoreIndex != -1)
			{
				removePanel("Score");
			}

			if (indicatorStudy1 && indicatorIndex1 == -1)
			{
				int panelCount = getPanelCount();
				addPanel(panelCount, "FT | SC PR");
			}
			else if (!indicatorStudy1 && indicatorIndex1 != -1)
			{
				removePanel("FT | SC PR");
			}

			if (indicatorStudy2 && indicatorIndex2 == -1)
			{
				int panelCount = getPanelCount();
				addPanel(panelCount, "FT | FT");
			}
			else if (!indicatorStudy2 && indicatorIndex2 != -1)
			{
				removePanel("FT | FT");
			}
			if (indicatorStudy3 && indicatorIndex3 == -1)
			{
				int panelCount = getPanelCount();
				addPanel(panelCount, "ST | SC PR");
			}
			else if (!indicatorStudy3 && indicatorIndex3 != -1)
			{
				removePanel("ST | SC PR");
			}
			if (indicatorStudy4 && indicatorIndex4 == -1)
			{
				int panelCount = getPanelCount();
				addPanel(panelCount, "Indicator4");
			}
			else if (!indicatorStudy4 && indicatorIndex4 != -1)
			{
				removePanel("Indicator4");
			}
			if (indicatorStudy5 && indicatorIndex5 == -1)
			{
				int panelCount = getPanelCount();
				addPanel(panelCount, "Indicator5");
			}
			else if (!indicatorStudy5 && indicatorIndex5 != -1)
			{
				removePanel("Indicator5");
			}
			if (indicatorStudy6 && indicatorIndex6 == -1)
			{
				int panelCount = getPanelCount();
				addPanel(panelCount, "Indicator6");
			}
			else if (!indicatorStudy6 && indicatorIndex6 != -1)
			{
				removePanel("Indicator6");
			}
			if (indicatorStudy7 && indicatorIndex7 == -1)
			{
				int panelCount = getPanelCount();
				addPanel(panelCount, "Indicator7");
			}
			else if (!indicatorStudy7 && indicatorIndex7 != -1)
			{
				removePanel("Indicator7");
			}
			if (indicatorStudy8 && indicatorIndex8 == -1)
			{
				int panelCount = getPanelCount();
				addPanel(panelCount, "FT | SC");
			}
			else if (!indicatorStudy8 && indicatorIndex8 != -1)
			{
				removePanel("FT | SC");
			}
			if (indicatorStudy9 && indicatorIndex9 == -1)
			{
				int panelCount = getPanelCount();
				addPanel(panelCount, "FT | ST");
			}
			else if (!indicatorStudy9 && indicatorIndex9 != -1)
			{
				removePanel("FT | ST");
			}
			if (indicatorStudy10 && indicatorIndex10 == -1)
			{
				int panelCount = getPanelCount();
				addPanel(panelCount, "FT | TSB");
			}
			else if (!indicatorStudy10 && indicatorIndex10 != -1)
			{
				removePanel("FT | TSB");
			}
			if (indicatorStudy11 && indicatorIndex11 == -1)
			{
				int panelCount = getPanelCount();
				addPanel(panelCount, "ST | FT");
			}
			else if (!indicatorStudy11 && indicatorIndex11 != -1)
			{
				removePanel("ST | FT");
			}

			if (indicatorStudy12 && indicatorIndex12 == -1)
			{
				int panelCount = getPanelCount();
				addPanel(panelCount, "ST | SC");
			}
			else if (!indicatorStudy12 && indicatorIndex12 != -1)
			{
				removePanel("ST | SC");
			}
			if (indicatorStudy13 && indicatorIndex13 == -1)
			{
				int panelCount = getPanelCount();
				addPanel(panelCount, "ST | ST");
			}
			else if (!indicatorStudy13 && indicatorIndex13 != -1)
			{
				removePanel("ST | ST");
			}
			if (indicatorStudy14 && indicatorIndex14 == -1)
			{
				int panelCount = getPanelCount();
				addPanel(panelCount, "ST | TSB");
			}
			else if (!indicatorStudy14 && indicatorIndex14 != -1)
			{
				removePanel("ST | TSB");
			}
			if (indicatorStudy15 && indicatorIndex15 == -1)
			{
				int panelCount = getPanelCount();
				addPanel(panelCount, "SC | FT");
			}
			else if (!indicatorStudy15 && indicatorIndex15 != -1)
			{
				removePanel("SC | FT");
			}
			if (indicatorStudy16 && indicatorIndex16 == -1)
			{
				int panelCount = getPanelCount();
				addPanel(panelCount, "SC | SC");
			}
			else if (!indicatorStudy16 && indicatorIndex16 != -1)
			{
				removePanel("SC | SC");
			}
			if (indicatorStudy17 && indicatorIndex17 == -1)
			{
				int panelCount = getPanelCount();
				addPanel(panelCount, "SC | ST");
			}
			else if (!indicatorStudy17 && indicatorIndex17 != -1)
			{
				removePanel("SC | ST");
			}
			if (indicatorStudy18 && indicatorIndex18 == -1)
			{
				int panelCount = getPanelCount();
				addPanel(panelCount, "SC | TSB");
			}
			else if (!indicatorStudy18 && indicatorIndex18 != -1)
			{
				removePanel("SC | TSB");
			}
			if (indicatorStudy19 && indicatorIndex19 == -1)
			{
				int panelCount = getPanelCount();
				addPanel(panelCount, "TSB | FT");
			}
			else if (!indicatorStudy19 && indicatorIndex19 != -1)
			{
				removePanel("TSB | FT");
			}
			if (indicatorStudy20 && indicatorIndex20 == -1)
			{
				int panelCount = getPanelCount();
				addPanel(panelCount, "TSB | SC");
			}
			else if (!indicatorStudy20 && indicatorIndex20 != -1)
			{
				removePanel("TSB | SC");
			}
			if (indicatorStudy21 && indicatorIndex21 == -1)
			{
				int panelCount = getPanelCount();
				addPanel(panelCount, "TSB | ST");
			}
			else if (!indicatorStudy21 && indicatorIndex21 != -1)
			{
				removePanel("TSB | ST");
			}
			if (indicatorStudy22 && indicatorIndex22 == -1)
			{
				int panelCount = getPanelCount();
				addPanel(panelCount, "TSB | TSB");
			}
			else if (!indicatorStudy22 && indicatorIndex22 != -1)
			{
				removePanel("TSB | TSB");
			}
			if (indicatorStudy23 && indicatorIndex23 == -1)
			{
				int panelCount = getPanelCount();
				addPanel(panelCount, "FT | ST PR");
			}
			else if (!indicatorStudy23 && indicatorIndex23 != -1)
			{
				removePanel("FT | ST PR");
			}
			if (indicatorStudy24 && indicatorIndex24 == -1)
			{
				int panelCount = getPanelCount();
				addPanel(panelCount, "FT | TSB PR");
			}
			else if (!indicatorStudy24 && indicatorIndex24 != -1)
			{
				removePanel("FT | TSB PR");
			}
			if (indicatorStudy25 && indicatorIndex25 == -1)
			{
				int panelCount = getPanelCount();
				addPanel(panelCount, "ST | ST PR");
			}
			else if (!indicatorStudy25 && indicatorIndex25 != -1)
			{
				removePanel("ST | ST PR");
			}
			if (indicatorStudy26 && indicatorIndex26 == -1)
			{
				int panelCount = getPanelCount();
				addPanel(panelCount, "ST | TSB PR");
			}
			else if (!indicatorStudy26 && indicatorIndex26 != -1)
			{
				removePanel("ST | TSB PR");
			}
			if (indicatorStudy27 && indicatorIndex27 == -1)
			{
				int panelCount = getPanelCount();
				addPanel(panelCount, "SC | SC PR");
			}
			else if (!indicatorStudy27 && indicatorIndex27 != -1)
			{
				removePanel("SC | SC PR");
			}
			if (indicatorStudy28 && indicatorIndex28 == -1)
			{
				int panelCount = getPanelCount();
				addPanel(panelCount, "SC | ST PR");
			}
			else if (!indicatorStudy28 && indicatorIndex28 != -1)
			{
				removePanel("SC | ST PR");
			}

			if (indicatorStudy29 && indicatorIndex29 == -1)
			{
				int panelCount = getPanelCount();
				addPanel(panelCount, "SC | TSB PR");
			}
			else if (!indicatorStudy29 && indicatorIndex29 != -1)
			{
				removePanel("SC | TSB PR");
			}

			if (indicatorStudy30 && indicatorIndex30 == -1)
			{
				int panelCount = getPanelCount();
				addPanel(panelCount, "TSB | SC PR");
			}
			else if (!indicatorStudy30 && indicatorIndex30 != -1)
			{
				removePanel("TSB | SC PR");
			}

			if (indicatorStudy31 && indicatorIndex31 == -1)
			{
				int panelCount = getPanelCount();
				addPanel(panelCount, "TSB | ST PR");
			}
			else if (!indicatorStudy31 && indicatorIndex31 != -1)
			{
				removePanel("TSB | ST PR");
			}

			if (indicatorStudy32 && indicatorIndex32 == -1)
			{
				int panelCount = getPanelCount();
				addPanel(panelCount, "TSB | TSB PR");
			}
			else if (!indicatorStudy32 && indicatorIndex32 != -1)
			{
				removePanel("TSB | TSB PR");
			}
			if (indicatorStudy33 && indicatorIndex33 == -1)
			{
				int panelCount = getPanelCount();
				addPanel(panelCount, "SC");
			}
			else if (!indicatorStudy33 && indicatorIndex33 != -1)
			{
				removePanel("SC");
			}
			if (indicatorStudy34 && indicatorIndex34 == -1)
			{
				int panelCount = getPanelCount();
				addPanel(panelCount, "PR");
			}
			else if (!indicatorStudy34 && indicatorIndex34 != -1)
			{
				removePanel("PR");
			}
			if (indicatorStudy35 && indicatorIndex35 == -1)
			{
				int panelCount = getPanelCount();
				addPanel(panelCount, "FT | P");
			}
			else if (!indicatorStudy35 && indicatorIndex35 != -1)
			{
				removePanel("FT | P");
			}
			if (indicatorStudy36 && indicatorIndex36 == -1)
			{
				int panelCount = getPanelCount();
				addPanel(panelCount, "FT || FT");
			}
			else if (!indicatorStudy36 && indicatorIndex36 != -1)
			{
				removePanel("FT || FT");
			}
			if (indicatorStudy37 && indicatorIndex37 == -1)
			{
				int panelCount = getPanelCount();
				addPanel(panelCount, "SC | P");
			}
			else if (!indicatorStudy37 && indicatorIndex37 != -1)
			{
				removePanel("SC | P");
			}
		}

		private Series calculateScore(List<DateTime> t1, Series[] b1, List<DateTime> t2, Series[] b2, string shortTermInterval, Dictionary<string, object> referenceData)
		{
			Dictionary<string, List<DateTime>> times = new Dictionary<string, List<DateTime>>();
			Dictionary<string, Series[]> bars = new Dictionary<string, Series[]>();

			times[shortTermInterval] = t1;
			bars[shortTermInterval] = b1;
			int barCount = t1.Count;

			string midTermInterval = GetOverviewInterval(shortTermInterval, 1);
			times[midTermInterval] = t2;
			bars[midTermInterval] = b2;

			string[] intervalList = { shortTermInterval, midTermInterval };
			return Conditions.Calculate("Score", _symbol, intervalList, barCount, times, bars, referenceData);
		}

		private Series calculatePositionRatio(List<DateTime> t1, Series[] b1, List<DateTime> t2, Series[] b2, Series[] indexSeries, string shortTermInterval)
		{
			Dictionary<string, List<DateTime>> times = new Dictionary<string, List<DateTime>>();
			Dictionary<string, Series[]> bars = new Dictionary<string, Series[]>();

			times[shortTermInterval] = t1;
			bars[shortTermInterval] = b1;
			int barCount = t1.Count;

			string midTermInterval = GetOverviewInterval(shortTermInterval, 1);
			times[midTermInterval] = t2;
			bars[midTermInterval] = b2;

			Dictionary<string, object> referenceData = new Dictionary<string, object>();
			referenceData["symbol"] = _symbol;
			if (indexSeries.Length > 0 && indexSeries[0].Count > 0)
			{
				referenceData["Index Prices : " + shortTermInterval] = indexSeries[0];
			}

			string[] intervalList = { shortTermInterval, midTermInterval };

			string conditionName = "Position Ratio 1";
			if (_strategy == "Strategy 2") conditionName = "Position Ratio 2";
			if (_strategy == "Strategy 3") conditionName = "Position Ratio 3";
			if (_strategy == "Strategy 4") conditionName = "Position Ratio 4";

			return Conditions.Calculate(conditionName, _symbol, intervalList, barCount, times, bars, referenceData);
		}

		private Series calculateRelativePrice(List<DateTime> t, Series[] b, Series[] indexSeries)
		{
			Dictionary<string, List<DateTime>> times = new Dictionary<string, List<DateTime>>();
			Dictionary<string, Series[]> bars = new Dictionary<string, Series[]>();
			times[_interval] = t;
			bars[_interval] = b;
			int barCount = t.Count;
			Dictionary<string, object> referenceData = new Dictionary<string, object>();
			if (indexSeries.Length > 0 && indexSeries[0].Count > 0)
			{
				referenceData["Index Prices : " + _interval] = indexSeries[0];
			}
			string[] intervalList = { _interval };
			return Conditions.Calculate("Relative Price", _symbol, intervalList, barCount, times, bars, referenceData);
		}

		private void calculateRelativePositionPrice(List<DateTime> t1, Series[] b1, List<DateTime> t2, Series[] b2, Series[] indexSeries)
		{
			Dictionary<string, List<DateTime>> times = new Dictionary<string, List<DateTime>>();
			Dictionary<string, Series[]> bars = new Dictionary<string, Series[]>();

			string shortTermInterval = getDataInterval();

			times[shortTermInterval] = t1;
			bars[shortTermInterval] = b1;
			int barCount = t1.Count;

			string midTermInterval = GetOverviewInterval(_interval, 1);
			times[midTermInterval] = t2;
			bars[midTermInterval] = b2;

			Dictionary<string, object> referenceData = new Dictionary<string, object>();
			if (indexSeries.Length > 0 && indexSeries[0].Count > 0)
			{
				referenceData["Index Prices : " + shortTermInterval] = indexSeries[0];
			}
			string[] intervalList = { shortTermInterval, midTermInterval };
			_RPP = Conditions.Calculate("Relative Position Price", _symbol, intervalList, barCount, times, bars, referenceData);
		}

		Queue<string> _studyThreadRequests = new Queue<string>();

		private void updateChart(string interval)
		{
			if (_studyThread == null)
			{
				_studyThreadStop = false;
				ParameterizedThreadStart start = new ParameterizedThreadStart(calculateStudies);
				_studyThread = new Thread(start);
				_studyThread.Start();
			}
			lock (_studyThreadRequests)
			{
				if (!_studyThreadRequests.Contains(interval))
				{
					_studyThreadRequests.Enqueue(interval);
				}
			}
		}

		public bool AnalysisEnable = true;

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
					var series1 = GetSeries(symbol, interval, new string[] { "Close" }, extra, barCount);
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

		private void updateBars()
		{
			var barIdx = 0;
			var barCnt = _bars.Count;
			List<Bar> bars = _barCache.GetBars(_symbol, getDataInterval(), _extraBars, 2);
			for (int ii = 0; ii < bars.Count; ii++)
			{
				lock (_barLock)
				{
					if (barIdx == 0)
					{
						var time = bars[ii].Time;
						barIdx = _bars.FindIndex(x => x.Time == time);
					}
					else
					{
						barIdx++;
					}

					if (barIdx >= barCnt)
					{
						_bars.Add(bars[ii]);
						//_bars.RemoveAt(0);
						_startIndex++;
						_endIndex++;

					}
					else if (barIdx >= 0)
					{
						_bars[barIdx] = bars[ii];
					}
				}
			}
		}

		private void loadBars()
		{
			List<Bar> bars = _barCache.GetBars(_symbol, getDataInterval(), _extraBars, _barCount);

			lock (_barLock)
			{
				if (bars != null && bars.Count > 0)
				{
					_bars = bars;
					adjustBars();
				}
			}

			_loadingBars = false;
		}

		int _ts = 0;
		int _tc = 0;
		//int _td = 0;

		private void calculateStudies(object info)
		{
			while (!_studyThreadStop)
			{
				try
				{
					var request = "";
					if (!_loadingBars)
					{
						lock (_studyThreadRequests)
						{
							if (_studyThreadRequests.Count > 0)
							{
								request = _studyThreadRequests.Dequeue();
							}
						}
					}

					if (request.Length > 0)
					{
						var workComplete = request.Contains("!");
						var interval = request.Replace("!", "");
						//if (Application.Current != null) Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, (Action)delegate () { updatePanels(); });

						if (GetPanelIndex("Main") >= 0)
						{
							bool singularForecast = getIndicatorEnable("ATM Trade Velocity");
							bool ftLinesMidTerm = getIndicatorEnable("Mid Term FT Lines");
							bool pxfSigHistory = getIndicatorEnable("Matrix") || getIndicatorEnable("Histogram") || _aodVisible;
							bool scAdd = getIndicatorEnable("New Trend") || getIndicatorEnable("Pressure") || getIndicatorEnable("Add") || getIndicatorEnable("2 Bar") || getIndicatorEnable("Exh") || getIndicatorEnable("Retrace") || getIndicatorEnable("ATR");

							string updateInterval = interval;

							List<DateTime> times = _barCache.GetTimes(_symbol, interval, _extraBars, _barCount);
							Series[] series = GetSeries(_symbol, interval, new string[] { "Open", "High", "Low", "Close" }, _extraBars, _barCount);
							Series[] indexSeries = GetSeries(_indexSymbol, interval, new string[] { "Close" }, _extraBars, _barCount);

							if (times != null && times.Count > 0)
							{
								if (_studyThreadStop) break;

								//int currentBarIndex = times.Count - _extraBars - 1;
								int shortTermCurrentBarIndex = times.Count - _extraBars - 1;

								int level = -1;
								string interval0 = _interval;
								string interval1 = GetOverviewInterval(_interval, 1);
								string interval2 = GetOverviewInterval(_interval, 2);

								if (interval == interval0) level = 0;
								else if (interval == interval1) level = 1;
								//else if (interval == interval2) level = 2;

								if (level == 0) // short term
								{

									if (_studyThreadStop) break;

									//loadBars();

									var ok = false;
									lock (_barLock)
									{
										ok = _bars != null && _bars.Count > 0;
									}

									if (ok)
									{
										List<DateTime> longTermTimes = _barCache.GetTimes(_symbol, interval2, _extraBars, _barCount);
										Series[] longTermSeries = GetSeries(_symbol, interval2, new string[] { "Open", "High", "Low", "Close" }, _extraBars, _barCount);

										List<DateTime> midTermTimes = _barCache.GetTimes(_symbol, interval1, _extraBars, _barCount);
										Series[] midTermSeries = GetSeries(_symbol, interval1, new string[] { "Open", "High", "Low", "Close" }, _extraBars, _barCount);
										int midTermCurrentBarIndex = midTermTimes.Count - _extraBars - 1;


										List<DateTime> shortTermTimes = times; // _barCache.GetTimes(_symbol, interval0, _extraBars);
										Series[] shortTermSeries = series; // GetSeries(_symbol, interval0, new string[] { "Open", "High", "Low", "Close" }, _extraBars);

										var indexTimes = new List<DateTime>(times);

										Series[] series2 = GetSeries(_symbol, interval, new string[] { "Open", "High", "Low", "Close" }, _extraBars, _barCount);

										int amount = 0;
										if (indexSeries[0].Count > 0)
										{
											amount = indexSeries[0].Count - indexTimes.Count;
											if (amount > 0) // index series is larger than ticker series
											{
												indexSeries[0].Drop(amount);
											}
											else if (amount < 0) // index series is smaller than ticker series
											{
												var earliestTime = indexTimes[0];
												for (int ix = 0; ix < -amount; ix++)
												{
													indexSeries[0].Data.Insert(0, double.NaN);
												}
											}
										}
										Series[] midTermIndexSeries = GetSeries(_indexSymbol, interval1, new string[] { "Close" }, _extraBars, _barCount);
										//Trace.WriteLine("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!Calculate studies for " + _symbol + " " + _interval + " chart." + " Data interval = " + interval);

										Dictionary<string, object> referenceData = new Dictionary<string, object>();
										referenceData["Index Prices : " + interval0] = indexSeries[0];
										referenceData["Index Prices : " + interval1] = midTermIndexSeries[0];

										if (_studyThreadStop) break;

										_PR = calculatePositionRatio(indexTimes, series2, midTermTimes, midTermSeries, indexSeries, interval0);

										if (_studyThreadStop) break;

										_scores = calculateScore(indexTimes, series2, midTermTimes, midTermSeries, interval0, referenceData);

										if (_studyThreadStop) break;

										_RP = calculateRelativePrice(indexTimes, series2, indexSeries);

										if (_studyThreadStop) break;

										updateIndicators(times, series, shortTermCurrentBarIndex);

										if (_studyThreadStop) break;

										calculateRelativePositionPrice(indexTimes, series2, midTermTimes, midTermSeries, indexSeries);

										if (_studyThreadStop) break;

										bool indicatorStudy = getIndicatorEnable("FT | FT")
														   || getIndicatorEnable("FT | SC")
														   || getIndicatorEnable("FT | ST")
														   || getIndicatorEnable("FT | TSB")
														   || getIndicatorEnable("FT | SC PR")
														   || getIndicatorEnable("FT | ST PR")
														   || getIndicatorEnable("FT | TSB PR")
														   || getIndicatorEnable("ST | FT")
														   || getIndicatorEnable("ST | SC")
														   || getIndicatorEnable("ST | ST")
														   || getIndicatorEnable("ST | TSB")
														   || getIndicatorEnable("ST | SC PR")
														   || getIndicatorEnable("ST | ST PR")
														   || getIndicatorEnable("ST | TSB PR")
														   || getIndicatorEnable("SC | FT")
														   || getIndicatorEnable("SC | SC")
														   || getIndicatorEnable("SC | ST")
														   || getIndicatorEnable("SC | TSB")
														   || getIndicatorEnable("SC | SC PR")
														   || getIndicatorEnable("SC | ST PR")
														   || getIndicatorEnable("SC | TSB PR")
														   || getIndicatorEnable("TSB | FT")
														   || getIndicatorEnable("TSB | SC")
														   || getIndicatorEnable("TSB | ST")
														   || getIndicatorEnable("TSB | TSB")
														   || getIndicatorEnable("TSB | SC PR")
														   || getIndicatorEnable("TSB | ST PR")
														   || getIndicatorEnable("TSB | TSB PR")
														   || getIndicatorEnable("SC")
														   || getIndicatorEnable("PR")
														   || getIndicatorEnable("FT | P")
														   || getIndicatorEnable("FT || FT")
														   || getIndicatorEnable("SC | P");

										string[] intervals = { interval0, interval1, interval2 };
										var t = new Dictionary<string, List<DateTime>>();
										t[interval0] = shortTermTimes;
										t[interval1] = midTermTimes;
										t[interval2] = longTermTimes;
										var b = new Dictionary<string, Series[]>();
										b[interval0] = shortTermSeries;
										b[interval1] = midTermSeries;
										b[interval2] = longTermSeries;

										Series prSig = atm.calculatePRSig(_PR, 2);

										if (_studyThreadStop) break;

										if (_symbolName != _oldSymbolName || _interval != _oldInterval || _reload)
										{
											_reload = false;
											_oldSymbolName = _symbolName;
											_oldInterval = _interval;
											lock (_barLock)
											{
												_currentIndex = _bars.Count - _extraBars - 1;
											}
											try
											{
												if (Application.Current != null) Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, (Action)delegate () { initialize(); });
											}
											catch (Exception x)
											{
												System.Diagnostics.Debug.WriteLine(x);
											}
										}

										if (_studyThreadStop) break;

										lock (_barLock)
										{
											for (int ii = _currentIndex + 1; ii >= 0 && ii < _bars.Count; ii++)
											{
												if (!double.IsNaN(_bars[ii].Close))
												{
													_currentIndex = ii;
												}
												else
												{
													break;
												}
											}
										}

										if (_studyThreadStop) break;

										List<Tuple<string, Color>> sid = null;
										if (scAdd)
										{
											if (!_showCursor) _adviceBarIndex = _currentIndex;
											var adviceBarIndex = _enableHistoricalAdvice ? _adviceBarIndex : shortTermCurrentBarIndex;
											sid = atm.getAdvice(_symbol, t, b, intervals, referenceData, GetSCAddEnbs(), adviceBarIndex);
										}
										lock (_studyInfoDataLock)
										{
											_studyInfoData = sid;
										}

										//try
										//{
										//    _studyInfoDatas.Clear();
										//    int idx1 = shortTermCurrentBarIndex - 100;
										//    int idx2 = shortTermCurrentBarIndex + 1;
										//    for (int idx = idx1; idx <= idx2; idx++)
										//    {
										//        _studyInfoDatas[idx] = atm.getAdvice(_symbol, t, b, intervals, referenceData, GetSCAddEnbs(), idx);
										//    }
										//}
										//catch (Exception x)
										//{
										//    var m = x.Message;
										//}


										if (_studyThreadStop) break;

										// FT | P strategy
										int panelIndex = GetPanelIndex("FT | P");
										if (panelIndex != -1)
										{
											var data = atm.getStrategy("FT | P", t, b, intervals, referenceData).Sign();
											var colors = new Color[] { Colors.White, (_indicators["FT | P"].Parameters["FTPUp"] as ColorParameter).Value, (_indicators["FT | P"].Parameters["FTPDn"] as ColorParameter).Value };
											var colorIds = data.Data.Select(x => (x == 1) ? 1 : ((x == -1) ? 2 : 0)).ToList();
											_study.updateHistogram("FT | P", panelIndex, colors, data.Data, colorIds);
											indicatorStudy = false;
										}

										panelIndex = GetPanelIndex("SC | P");
										if (panelIndex != -1)
										{
											var data = atm.getStrategy("SC | P", t, b, intervals, referenceData).Sign();
											var colors = new Color[] { Colors.White, (_indicators["SC | P"].Parameters["SCPUp"] as ColorParameter).Value, (_indicators["SC | P"].Parameters["SCPDn"] as ColorParameter).Value };
											var colorIds = data.Data.Select(x => (x == 1) ? 1 : ((x == -1) ? 2 : 0)).ToList();
											_study.updateHistogram("SC | P", panelIndex, colors, data.Data, colorIds);
											indicatorStudy = false;
										}

										// FT | ST PR strategy
										panelIndex = GetPanelIndex("FT | SC PR");
										if (panelIndex != -1)
										{
											var data = atm.getStrategy("FT | SC PR", t, b, intervals, referenceData).Sign();
											var colors = new Color[] { Colors.White, (_indicators["FT | SC PR"].Parameters["FTSCPRUp"] as ColorParameter).Value, (_indicators["FT | SC PR"].Parameters["FTSCPRDn"] as ColorParameter).Value };
											var colorIds = data.Data.Select(x => (x == 1) ? 1 : ((x == -1) ? 2 : 0)).ToList();
											_study.updateHistogram("FT | SC PR", panelIndex, colors, data.Data, colorIds);
											indicatorStudy = false;
										}

										// FT | FT strategy
										panelIndex = GetPanelIndex("FT | FT");
										if (panelIndex != -1)
										{
											var data = atm.getStrategy("FT | FT", t, b, intervals, referenceData);
											var colors = new Color[] { Colors.White, (_indicators["FT | FT"].Parameters["FTFTUp"] as ColorParameter).Value, (_indicators["FT | FT"].Parameters["FTFTDn"] as ColorParameter).Value };
											var colorIds = data.Data.Select(x => (x == 1) ? 1 : ((x == -1) ? 2 : 0)).ToList();
											_study.updateHistogram("FT | FT", panelIndex, colors, data.Data, colorIds);
											indicatorStudy = false;
										}

										// FT || FT strategy
										panelIndex = GetPanelIndex("FT || FT");
										if (panelIndex != -1)
										{
											var data = atm.getStrategy("FT || FT", t, b, intervals, referenceData);
											var colors = new Color[] { Colors.White, (_indicators["FT || FT"].Parameters["FT||FTUp"] as ColorParameter).Value, (_indicators["FT || FT"].Parameters["FT||FTDn"] as ColorParameter).Value };
											var colorIds = data.Data.Select(x => (x == 1) ? 1 : ((x == -1) ? 2 : 0)).ToList();
											_study.updateHistogram("FT || FT", panelIndex, colors, data.Data, colorIds);
											indicatorStudy = false;
										}

										// FT | SC strategy
										panelIndex = GetPanelIndex("FT | SC");
										if (panelIndex != -1)
										{
											var data = atm.getStrategy("FT | SC", t, b, intervals, referenceData);
											var colors = new Color[] { Colors.White, (_indicators["FT | SC"].Parameters["FTSCUp"] as ColorParameter).Value, (_indicators["FT | SC"].Parameters["FTSCDn"] as ColorParameter).Value };
											var colorIds = data.Data.Select(x => (x == 1) ? 1 : ((x == -1) ? 2 : 0)).ToList();
											_study.updateHistogram("FT | SC", panelIndex, colors, data.Data, colorIds);
											indicatorStudy = false;
										}

										// FT | ST strategy
										panelIndex = GetPanelIndex("FT | ST");
										if (panelIndex != -1)
										{
											var data = atm.getStrategy("FT | ST", t, b, intervals, referenceData);
											var colors = new Color[] { Colors.White, (_indicators["FT | ST"].Parameters["FTSTUp"] as ColorParameter).Value, (_indicators["FT | ST"].Parameters["FTSTDn"] as ColorParameter).Value };
											var colorIds = data.Data.Select(x => (x == 1) ? 1 : ((x == -1) ? 2 : 0)).ToList();
											_study.updateHistogram("FT | ST", panelIndex, colors, data.Data, colorIds);
											indicatorStudy = false;
										}

										// FT | TSB strategy
										panelIndex = GetPanelIndex("FT | TSB");
										if (panelIndex != -1)
										{
											var data = atm.getStrategy("FT | TSB", t, b, intervals, referenceData);
											var colors = new Color[] { Colors.White, (_indicators["FT | TSB"].Parameters["FTTSBUp"] as ColorParameter).Value, (_indicators["FT | TSB"].Parameters["FTTSBDn"] as ColorParameter).Value };
											var colorIds = data.Data.Select(x => (x == 1) ? 1 : ((x == -1) ? 2 : 0)).ToList();
											_study.updateHistogram("FT | TSB", panelIndex, colors, data.Data, colorIds);
											indicatorStudy = false;
										}

										// ST | FT strategy
										panelIndex = GetPanelIndex("ST | FT");
										if (panelIndex != -1)
										{
											var data = atm.getStrategy("ST | FT", t, b, intervals, referenceData);
											var colors = new Color[] { Colors.White, (_indicators["ST | FT"].Parameters["STFTUp"] as ColorParameter).Value, (_indicators["ST | FT"].Parameters["STFTDn"] as ColorParameter).Value };
											var colorIds = data.Data.Select(x => (x == 1) ? 1 : ((x == -1) ? 2 : 0)).ToList();
											_study.updateHistogram("ST | FT", panelIndex, colors, data.Data, colorIds);
											indicatorStudy = false;
										}

										// ST | SC strategy
										panelIndex = GetPanelIndex("ST | SC");
										if (panelIndex != -1)
										{
											var data = atm.getStrategy("ST | SC", t, b, intervals, referenceData);
											var colors = new Color[] { Colors.White, (_indicators["ST | SC"].Parameters["STSCUp"] as ColorParameter).Value, (_indicators["ST | SC"].Parameters["STSCDn"] as ColorParameter).Value };
											var colorIds = data.Data.Select(x => (x == 1) ? 1 : ((x == -1) ? 2 : 0)).ToList();
											_study.updateHistogram("ST | SC", panelIndex, colors, data.Data, colorIds);
											indicatorStudy = false;
										}

										// ST | ST strategy
										panelIndex = GetPanelIndex("ST | ST");
										if (panelIndex != -1)
										{
											var data = atm.getStrategy("ST | ST", t, b, intervals, referenceData);
											var colors = new Color[] { Colors.White, (_indicators["ST | ST"].Parameters["STSTUp"] as ColorParameter).Value, (_indicators["ST | ST"].Parameters["STSTDn"] as ColorParameter).Value };
											var colorIds = data.Data.Select(x => (x == 1) ? 1 : ((x == -1) ? 2 : 0)).ToList();
											_study.updateHistogram("ST | ST", panelIndex, colors, data.Data, colorIds);
											indicatorStudy = false;
										}

										// ST | TSB strategy
										panelIndex = GetPanelIndex("ST | TSB");
										if (panelIndex != -1)
										{
											var data = atm.getStrategy("ST | TSB", t, b, intervals, referenceData);
											var colors = new Color[] { Colors.White, (_indicators["ST | TSB"].Parameters["STTSBUp"] as ColorParameter).Value, (_indicators["ST | TSB"].Parameters["STTSBDn"] as ColorParameter).Value };
											var colorIds = data.Data.Select(x => (x == 1) ? 1 : ((x == -1) ? 2 : 0)).ToList();
											_study.updateHistogram("ST | TSB", panelIndex, colors, data.Data, colorIds);
											indicatorStudy = false;
										}

										// SC | FT strategy
										panelIndex = GetPanelIndex("SC | FT");
										if (panelIndex != -1)
										{
											var data = atm.getStrategy("SC | FT", t, b, intervals, referenceData);
											var colors = new Color[] { Colors.White, (_indicators["SC | FT"].Parameters["SCFTUp"] as ColorParameter).Value, (_indicators["SC | FT"].Parameters["SCFTDn"] as ColorParameter).Value };
											var colorIds = data.Data.Select(x => (x == 1) ? 1 : ((x == -1) ? 2 : 0)).ToList();
											_study.updateHistogram("SC | FT", panelIndex, colors, data.Data, colorIds);
											indicatorStudy = false;
										}

										// SC | ST strategy
										panelIndex = GetPanelIndex("SC | SC");
										if (panelIndex != -1)
										{
											var data = atm.getStrategy("SC | SC", t, b, intervals, referenceData);
											var colors = new Color[] { Colors.White, (_indicators["SC | SC"].Parameters["SCSCUp"] as ColorParameter).Value, (_indicators["SC | SC"].Parameters["SCSCDn"] as ColorParameter).Value };
											var colorIds = data.Data.Select(x => (x == 1) ? 1 : ((x == -1) ? 2 : 0)).ToList();
											_study.updateHistogram("SC | SC", panelIndex, colors, data.Data, colorIds);
											indicatorStudy = false;
										}

										// SC | SC strategy
										panelIndex = GetPanelIndex("SC | ST");
										if (panelIndex != -1)
										{
											var data = atm.getStrategy("SC | ST", t, b, intervals, referenceData);
											var colors = new Color[] { Colors.White, (_indicators["SC | ST"].Parameters["SCSTUp"] as ColorParameter).Value, (_indicators["SC | ST"].Parameters["SCSTDn"] as ColorParameter).Value };
											var colorIds = data.Data.Select(x => (x == 1) ? 1 : ((x == -1) ? 2 : 0)).ToList();
											_study.updateHistogram("SC | ST", panelIndex, colors, data.Data, colorIds);
											indicatorStudy = false;
										}

										// SC | TSB strategy
										panelIndex = GetPanelIndex("SC | TSB");
										if (panelIndex != -1)
										{
											var data = atm.getStrategy("SC | TSB", t, b, intervals, referenceData);
											var colors = new Color[] { Colors.White, (_indicators["SC | TSB"].Parameters["SCTSBUp"] as ColorParameter).Value, (_indicators["SC | TSB"].Parameters["SCTSBDn"] as ColorParameter).Value };
											var colorIds = data.Data.Select(x => (x == 1) ? 1 : ((x == -1) ? 2 : 0)).ToList();
											_study.updateHistogram("SC | TSB", panelIndex, colors, data.Data, colorIds);
											indicatorStudy = false;
										}

										// TSB | FT strategy
										panelIndex = GetPanelIndex("TSB | FT");
										if (panelIndex != -1)
										{
											var data = atm.getStrategy("TSB | FT", t, b, intervals, referenceData);
											var colors = new Color[] { Colors.White, (_indicators["TSB | FT"].Parameters["TSBFTUp"] as ColorParameter).Value, (_indicators["TSB | FT"].Parameters["TSBFTDn"] as ColorParameter).Value };
											var colorIds = data.Data.Select(x => (x == 1) ? 1 : ((x == -1) ? 2 : 0)).ToList();
											_study.updateHistogram("TSB | FT", panelIndex, colors, data.Data, colorIds);
											indicatorStudy = false;
										}

										// TSB | SC strategy
										panelIndex = GetPanelIndex("TSB | SC");
										if (panelIndex != -1)
										{
											var data = atm.getStrategy("TSB | SC", t, b, intervals, referenceData);
											var colors = new Color[] { Colors.White, (_indicators["TSB | SC"].Parameters["TSBSCUp"] as ColorParameter).Value, (_indicators["TSB | SC"].Parameters["TSBSCDn"] as ColorParameter).Value };
											var colorIds = data.Data.Select(x => (x == 1) ? 1 : ((x == -1) ? 2 : 0)).ToList();
											_study.updateHistogram("TSB | SC", panelIndex, colors, data.Data, colorIds);
											indicatorStudy = false;
										}

										// TSB | ST strategy
										panelIndex = GetPanelIndex("TSB | ST");
										if (panelIndex != -1)
										{
											var data = atm.getStrategy("TSB | ST", t, b, intervals, referenceData);
											var colors = new Color[] { Colors.White, (_indicators["TSB | ST"].Parameters["TSBSTUp"] as ColorParameter).Value, (_indicators["TSB | ST"].Parameters["TSBSTDn"] as ColorParameter).Value };
											var colorIds = data.Data.Select(x => (x == 1) ? 1 : ((x == -1) ? 2 : 0)).ToList();
											_study.updateHistogram("TSB | ST", panelIndex, colors, data.Data, colorIds);
											indicatorStudy = false;
										}

										// TSB | TSB strategy
										panelIndex = GetPanelIndex("TSB | TSB");
										if (panelIndex != -1)
										{
											var data = atm.getStrategy("TSB | TSB", t, b, intervals, referenceData);
											var colors = new Color[] { Colors.White, (_indicators["TSB | TSB"].Parameters["TSBTSBUp"] as ColorParameter).Value, (_indicators["TSB | TSB"].Parameters["TSBTSBDn"] as ColorParameter).Value };
											var colorIds = data.Data.Select(x => (x == 1) ? 1 : ((x == -1) ? 2 : 0)).ToList();
											_study.updateHistogram("TSB | TSB", panelIndex, colors, data.Data, colorIds);
											indicatorStudy = false;
										}

										// ST | SC PR strategy
										panelIndex = GetPanelIndex("ST | SC PR");
										if (panelIndex != -1)
										{
											var data = atm.getStrategy("ST | SC PR", t, b, intervals, referenceData).Sign();
											var colors = new Color[] { Colors.White, (_indicators["ST | SC PR"].Parameters["STSCPRUp"] as ColorParameter).Value, (_indicators["ST | SC PR"].Parameters["STSCPRDn"] as ColorParameter).Value };
											var colorIds = data.Data.Select(x => (x == 1) ? 1 : ((x == -1) ? 2 : 0)).ToList();
											_study.updateHistogram("ST | SC PR", panelIndex, colors, data.Data, colorIds);
											indicatorStudy = false;
										}

										// FT | ST PR strategy
										panelIndex = GetPanelIndex("FT | ST PR");
										if (panelIndex != -1)
										{
											var data = atm.getStrategy("FT | ST PR", t, b, intervals, referenceData).Sign();
											var colors = new Color[] { Colors.White, (_indicators["FT | ST PR"].Parameters["FTSTPRUp"] as ColorParameter).Value, (_indicators["FT | ST PR"].Parameters["FTSTPRDn"] as ColorParameter).Value };
											var colorIds = data.Data.Select(x => (x == 1) ? 1 : ((x == -1) ? 2 : 0)).ToList();
											_study.updateHistogram("FT | ST PR", panelIndex, colors, data.Data, colorIds);
											indicatorStudy = false;
										}

										// FT | TSB PR strategy
										panelIndex = GetPanelIndex("FT | TSB PR");
										if (panelIndex != -1)
										{
											var data = atm.getStrategy("FT | TSB PR", t, b, intervals, referenceData).Sign();
											var colors = new Color[] { Colors.White, (_indicators["FT | TSB PR"].Parameters["FTTSBPRUp"] as ColorParameter).Value, (_indicators["FT | TSB PR"].Parameters["FTTSBPRDn"] as ColorParameter).Value };
											var colorIds = data.Data.Select(x => (x == 1) ? 1 : ((x == -1) ? 2 : 0)).ToList();
											_study.updateHistogram("FT | TSB PR", panelIndex, colors, data.Data, colorIds);
											indicatorStudy = false;
										}

										// ST | ST PR strategy
										panelIndex = GetPanelIndex("ST | ST PR");
										if (panelIndex != -1)
										{
											var data = atm.getStrategy("ST | ST PR", t, b, intervals, referenceData).Sign();
											var colors = new Color[] { Colors.White, (_indicators["ST | ST PR"].Parameters["STSTPRUp"] as ColorParameter).Value, (_indicators["ST | ST PR"].Parameters["STSTPRDn"] as ColorParameter).Value };
											var colorIds = data.Data.Select(x => (x == 1) ? 1 : ((x == -1) ? 2 : 0)).ToList();
											_study.updateHistogram("ST | ST PR", panelIndex, colors, data.Data, colorIds);
											indicatorStudy = false;
										}

										// ST | TSB PR strategy
										panelIndex = GetPanelIndex("ST | TSB PR");
										if (panelIndex != -1)
										{
											var data = atm.getStrategy("ST | TSB PR", t, b, intervals, referenceData).Sign();
											var colors = new Color[] { Colors.White, (_indicators["ST | TSB PR"].Parameters["STTSBPRUp"] as ColorParameter).Value, (_indicators["ST | TSB PR"].Parameters["STTSBPRDn"] as ColorParameter).Value };
											var colorIds = data.Data.Select(x => (x == 1) ? 1 : ((x == -1) ? 2 : 0)).ToList();
											_study.updateHistogram("ST | TSB PR", panelIndex, colors, data.Data, colorIds);
											indicatorStudy = false;
										}

										// SC | SC PR strategy
										panelIndex = GetPanelIndex("SC | SC PR");
										if (panelIndex != -1)
										{
											var data = atm.getStrategy("SC | SC PR", t, b, intervals, referenceData).Sign();
											var colors = new Color[] { Colors.White, (_indicators["SC | SC PR"].Parameters["SCSCPRUp"] as ColorParameter).Value, (_indicators["SC | SC PR"].Parameters["SCSCPRDn"] as ColorParameter).Value };
											var colorIds = data.Data.Select(x => (x == 1) ? 1 : ((x == -1) ? 2 : 0)).ToList();
											_study.updateHistogram("SC | SC PR", panelIndex, colors, data.Data, colorIds);
											indicatorStudy = false;
										}

										// SC | ST PR strategy
										panelIndex = GetPanelIndex("SC | ST PR");
										if (panelIndex != -1)
										{
											var data = atm.getStrategy("SC | ST PR", t, b, intervals, referenceData).Sign();
											var colors = new Color[] { Colors.White, (_indicators["SC | ST PR"].Parameters["SCSTPRUp"] as ColorParameter).Value, (_indicators["SC | ST PR"].Parameters["SCSTPRDn"] as ColorParameter).Value };
											var colorIds = data.Data.Select(x => (x == 1) ? 1 : ((x == -1) ? 2 : 0)).ToList();
											_study.updateHistogram("SC | ST PR", panelIndex, colors, data.Data, colorIds);
											indicatorStudy = false;
										}

										// SC | TSB PR strategy
										panelIndex = GetPanelIndex("SC | TSB PR");
										if (panelIndex != -1)
										{
											var data = atm.getStrategy("SC | TSB PR", t, b, intervals, referenceData).Sign();
											var colors = new Color[] { Colors.White, (_indicators["SC | TSB PR"].Parameters["SCTSBPRUp"] as ColorParameter).Value, (_indicators["SC | TSB PR"].Parameters["SCTSBPRDn"] as ColorParameter).Value };
											var colorIds = data.Data.Select(x => (x == 1) ? 1 : ((x == -1) ? 2 : 0)).ToList();
											_study.updateHistogram("SC | TSB PR", panelIndex, colors, data.Data, colorIds);
											indicatorStudy = false;
										}


										// TSB | SC PR strategy
										panelIndex = GetPanelIndex("TSB | SC PR");
										if (panelIndex != -1)
										{
											var data = atm.getStrategy("TSB | SC PR", t, b, intervals, referenceData).Sign();
											var colors = new Color[] { Colors.White, (_indicators["TSB | SC PR"].Parameters["TSBSCPRUp"] as ColorParameter).Value, (_indicators["TSB | SC PR"].Parameters["TSBSCPRDn"] as ColorParameter).Value };
											var colorIds = data.Data.Select(x => (x == 1) ? 1 : ((x == -1) ? 2 : 0)).ToList();
											_study.updateHistogram("TSB | SC PR", panelIndex, colors, data.Data, colorIds);
											indicatorStudy = false;
										}

										// TSB | ST PR strategy
										panelIndex = GetPanelIndex("TSB | ST PR");
										if (panelIndex != -1)
										{
											var data = atm.getStrategy("TSB | ST PR", t, b, intervals, referenceData).Sign();
											var colors = new Color[] { Colors.White, (_indicators["TSB | ST PR"].Parameters["TSBSTPRUp"] as ColorParameter).Value, (_indicators["TSB | ST PR"].Parameters["TSBSTPRDn"] as ColorParameter).Value };
											var colorIds = data.Data.Select(x => (x == 1) ? 1 : ((x == -1) ? 2 : 0)).ToList();
											_study.updateHistogram("TSB | ST PR", panelIndex, colors, data.Data, colorIds);
											indicatorStudy = false;
										}

										// TSB | TSB PR strategy
										panelIndex = GetPanelIndex("TSB | TSB PR");
										if (panelIndex != -1)
										{
											var data = atm.getStrategy("TSB | TSB PR", t, b, intervals, referenceData).Sign();
											var colors = new Color[] { Colors.White, (_indicators["TSB | TSB PR"].Parameters["TSBTSBPRUp"] as ColorParameter).Value, (_indicators["TSB | TSB PR"].Parameters["TSBTSBPRDn"] as ColorParameter).Value };
											var colorIds = data.Data.Select(x => (x == 1) ? 1 : ((x == -1) ? 2 : 0)).ToList();
											_study.updateHistogram("TSB | TSB PR", panelIndex, colors, data.Data, colorIds);
											indicatorStudy = false;
										}

										// SC strategy
										panelIndex = GetPanelIndex("SC");
										if (panelIndex != -1)
										{
											var data = atm.getStrategy("SC", t, b, intervals, referenceData);
											var results = data.Data.Take(data.Count - _extraBars).ToList();
											var colors = new Color[] { Colors.White, (_indicators["SC"].Parameters["SCUp"] as ColorParameter).Value, (_indicators["SC"].Parameters["SCDn"] as ColorParameter).Value };
											var colorIds = results.Select(x => (x == 1) ? 1 : ((x == -1) ? 2 : 0)).ToList();
											_study.updateHistogram("SC", panelIndex, colors, results, colorIds);
											indicatorStudy = false;
										}

										// PR strategy
										panelIndex = GetPanelIndex("PR");
										if (panelIndex != -1)
										{
											var data = atm.getStrategy("PR", t, b, intervals, referenceData).Sign();
											var results = data.Data.Take(data.Count - _extraBars).ToList();
											var colors = new Color[] { Colors.White, (_indicators["PR"].Parameters["PRUp"] as ColorParameter).Value, (_indicators["PR"].Parameters["PRDn"] as ColorParameter).Value };
											var colorIds = results.Select(x => (x == 1) ? 1 : ((x == -1) ? 2 : 0)).ToList();
											_study.updateHistogram("PR", panelIndex, colors, results, colorIds);
											indicatorStudy = false;
										}

										if (_studyThreadStop) break;

										bool prSigCurrent = getIndicatorEnable("PR");
										_study.updatePositionRatio(indexTimes, series2[3], _PR, prSigCurrent);

										if (_studyThreadStop) break;

										if (_displayPerformance)
										{
											if (_recordPerformance)
											{
												_responseTime = _performanceStopwatch.ElapsedMilliseconds;
												_performanceStopwatch = Stopwatch.StartNew();
											}
										}

										if (_studyThreadStop) break;

										try
										{
											if (Application.Current != null) Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, (Action)delegate () { updatePerformance(); });
										}
										catch (Exception x)
										{
											System.Diagnostics.Debug.WriteLine(x);
										}

										if (_studyThreadStop) break;

										if (pxfSigHistory)
										{
											_mpst.predict(_symbol, interval0, _modelNames, _barCache);

											if (_studyThreadStop) break;

											_mpmt.predict(_symbol, interval1, _modelNames, _barCache);
										}

										if (AnalysisEnable && _aodVisible)
										{
											var modelName = (_modelNames.Count > 0) ? _modelNames[0] : "";

											if (_studyThreadStop) break;

											var model = MainView.GetModel(_modelNames[0]);

											var aodInput = new AODInput();
											aodInput.Symbol = _symbol;
											aodInput.Interval = interval0;
											aodInput.Senario = (model == null) ? "No Prediction" : MainView.GetSenarioLabel(model.Scenario);
											aodInput.ShortTermIndex = shortTermCurrentBarIndex;
											aodInput.MidTermIndex = midTermCurrentBarIndex;
											aodInput.ShortTermTimes = shortTermTimes;
											aodInput.MidTermTimes = midTermTimes;
											aodInput.LongTermTimes = longTermTimes;
											aodInput.ShortTermSeries = shortTermSeries;
											aodInput.MidTermSeries = midTermSeries;
											aodInput.LongTermSeries = longTermSeries;
											aodInput.ShortTermIndexSeries = indexSeries;
											aodInput.MidTermIndexSeries = midTermIndexSeries;
											aodInput.ShortTermPredictions = _mpst.getPredictions(_symbol, interval0, modelName);
											aodInput.ShortTermActuals = _mpst.getActuals(_symbol, interval0, modelName);
											aodInput.MidTermPredictions = _mpmt.getPredictions(_symbol, interval1, modelName);
											aodInput.MidTermActuals = _mpmt.getActuals(_symbol, interval1, modelName);

											_aod.update(aodInput);
											_drawAod = true;
										}
									}

									if (_studyThreadStop) break;

									updateConditionTrades(_referenceData);

									if (_studyThreadStop) break;

									updateTradeEvents();

									if (_studyThreadStop) break;

									Panels[0].RemoveCurve("Price");
									if (_modelNames.Count > 0)
									{
										var modelName = _modelNames[0];
										var model = MainView.GetModel(modelName);
										if (model != null)
										{

											if (_studyThreadStop) break;

											var predictions = _mpst.getPredictions(_symbol, getDataInterval(), modelName);
											var actuals = _mpst.getActuals(_symbol, getDataInterval(), modelName);

											if (_studyThreadStop) break;

											if (isRegressionModel())
											{
												_study.calculatePredictionPriceLine(model.Scenario, times, series, predictions, actuals, pxfSigHistory);
											}
											else
											{
												var index = isTimeValid(_cursorTime) ? getTimeIndex(_cursorTime) : shortTermCurrentBarIndex;
												_study.calculatePredictionOscillator(model.Scenario, times, series, predictions, actuals, pxfSigHistory, index);
											}

											if (_studyThreadStop) break;

											_study.calculateScoreOscillator(times, getScores(modelName), true);
										}
									}

									bool enbVal = getIndicatorEnable("Forecast");
									var enbBoundries = getIndicatorEnable("Boundaries");
									_study.calculateForecast(_symbol, interval, _currentIndex, times, series, enbVal, enbBoundries);

								}
								else if (level == 1 || level == 2) // mid term and long term
								{
									if (_studyThreadStop) break;

									if (level == 1)
									{
										_study.calculateFTChartLineStudy(times, series, ftLinesMidTerm, level, series[0].Count - 1, false);
									}
								}

								if (_studyThreadStop) break;

								bool forecastEnable = ((level == 0 && singularForecast));
								_panels[0].ClearBezierShading(level.ToString());
								_study.calculateTradeVelocity(interval, times, series, level, forecastEnable);

								_update = true;
							}
						}

						if (workComplete)
						{
							_displayWorking = false;
						}
					}
					else if (!_studyThreadStop)
					{
						Thread.Sleep(100);
					}
				}
				catch (Exception x)
				{
					//Trace.WriteLine("Calculation exception " + x.ToString());
					//Trace.WriteLine(x.StackTrace);
				}
			}
		}

		bool _noHiLos = false;
		private void adjustBars()
		{
			lock (_barLock)
			{
				int cnt1 = _bars.Aggregate(0, (output, x) => output += (!double.IsNaN(x.Close) && (double.IsNaN(x.Open) || !double.IsNaN(x.Open) && x.Open == x.Close)) ? 1 : 0);
				int cnt2 = _bars.Aggregate(0, (output, x) => output += (!double.IsNaN(x.Close) && (double.IsNaN(x.High) || double.IsNaN(x.Low) || x.High - x.Low <= 0.0001 * x.High) ? 1 : 0));
				int total = _bars.Count;

				var pc1 = (100.0 * cnt1) / total;
				var pc2 = (100.0 * cnt2) / total;


				if (pc1 > 50 || pc2 > 50) // greater than 10% flat bars will draw line
				{
					_noHiLos = true;
					_bars.ForEach(x => { x.Open = x.Close; x.High = x.Close; x.Low = x.Close; });
				}
				else
				{
					_noHiLos = false;
				}
			}
		}

		private void updatePerformance()
		{
			if (_displayPerformance)
			{
				if (_recordPerformance)
				{
					_recordPerformance = false;
					_calculationTime = _performanceStopwatch.ElapsedMilliseconds;
				}
				_intervalText.Text += " " + _responseTime.ToString() + " " + _calculationTime.ToString();
			}

			_intervalText.Text = getIntervalLabel(_interval);
		}

		private Dictionary<DateTime, double> getScores(string modelName)
		{
			var output = new Dictionary<DateTime, double>();

			var ticker = _symbol;
			var interval = getDataInterval();

			var model = MainView.GetModel(modelName);
			var pathName = @"senarios\" + model.Name + @"\" + interval + (model.UseTicker ? @"\" + MainView.ToPath(_symbol) : "");
			var scores = MainView.getScores(pathName);

			if (scores != null && scores.ContainsKey(ticker))
			{
				if (scores[ticker].ContainsKey(interval))
				{
					output = scores[ticker][interval];
				}
			}
			return output;
		}

		//public void updateProperties()
		//{
		//    updatePanels();

		//    string interval = getDataInterval();
		//    List<DateTime> times = _barCache.GetTimes(_symbol, interval, _extraBars);
		//    Series[] series = GetSeries(_symbol, interval, new string[] { "Open", "High", "Low", "Close" }, _extraBars);
		//    int currentBarIndex = times.Count - _extraBars - 1;

		//    if (times.Count > 0)
		//    {
		//        updateIndicators(times, series, currentBarIndex);
		//        updateTradeEvents();
		//        updateConditionTrades();

		//        bool pxfSigHistory = getIndicatorEnable("Matrix") || getIndicatorEnable("Histogram");

		//        if (_modelNames.Count > 0)
		//        {
		//            var modelName = _modelNames[0];
		//            var model = MainView.GetModel(modelName);
		//            if (model != null)
		//            {
		//                var predictions = mpst.getPredictions(_symbol, getDataInterval(), modelName);
		//                var actuals = mpst.getActuals(_symbol, getDataInterval(), modelName);
		//                _study.calculatePredictionOscillator(model.Scenario, times, series, predictions, actuals, pxfSigHistory, currentBarIndex);
		//                _study.calculateScoreOscillator(times, getScores(modelName), true);
		//            }
		//        }

		//        bool singularForecast = getIndicatorEnable("ATM Trade Velocity");
		//        bool multipleForecasts = getIndicatorEnable("Longer Time Frame Forecasts");

		//        _panels[0].ClearBezierShading("0");
		//        if (singularForecast || multipleForecasts)
		//        {
		//            _study.calculateForecast(interval, times, series, 0, true);
		//        }

		//        _panels[0].ClearBezierShading("1");
		//        _panels[0].ClearBezierShading("2");

		//        requestBars();
		//    }
		//}

		private void updateIndicators(List<DateTime> times, Series[] series, int currentBarIndex)
		{
			var rev = atm.isYieldTicker(_symbol);

			// ATM Indicators;
			bool trendBarsStudy = getIndicatorEnable("ATM Trend Bars");
			bool scBarsStudy = getIndicatorEnable("ATM SC Bars");
			bool trendLinesStudy = getIndicatorEnable("ATM Trend Lines");
			bool triggerStudy = getIndicatorEnable("ATM Trigger");
			bool targetLineStudy = getIndicatorEnable("ATM Targets");
			bool threeSigmaStudy = getIndicatorEnable("ATM 3Sigma");

			bool adx = getIndicatorEnable("ADX");
			bool bol = getIndicatorEnable("BOL");
			bool macd = getIndicatorEnable("MACD");
			bool dmi = getIndicatorEnable("DMI");
			bool ddmi = getIndicatorEnable("D DIF");
			bool atr = getIndicatorEnable("ATR");
			bool atrx = getIndicatorEnable("ATRX");
			bool ma200 = getIndicatorEnable("MA 200");
			bool ma100 = getIndicatorEnable("MA 100");
			bool ma50 = getIndicatorEnable("MA 50");
			bool ma1 = getIndicatorEnable("MA 1");
			bool ma2 = getIndicatorEnable("MA 2");
			bool ma3 = getIndicatorEnable("MA 3");
			bool mom = getIndicatorEnable("MOM");
			bool osc = getIndicatorEnable("OSC");
			bool roc = getIndicatorEnable("ROC");
			bool rsi = getIndicatorEnable("RSI");

			// ATM XSignals;
			bool xAlert = getIndicatorEnable("X Alert");
			bool firstAlert = getIndicatorEnable("First Alert");
			bool addOnAlert = getIndicatorEnable("Add On Alert");
			bool scAdd = getIndicatorEnable("Current Research");
			bool pullbackAlert = getIndicatorEnable("Pullback Alert");
			bool exhaustionAlert = getIndicatorEnable("Exhaustion Alert");
			bool pressureAlert = getIndicatorEnable("Pressure Alert");
			//bool PTAlert = getIndicatorEnable("PT Alert");
			bool twoBarAlert = getIndicatorEnable("Two Bar Alert");
			//bool twoBarTrend = getIndicatorEnable("Two Bar Trend");
			bool pressureTrendAlert = getIndicatorEnable("PT Alert");
			bool twoBarTrendAlert = getIndicatorEnable("Two Bar Trend");
			bool ftAlert = getIndicatorEnable("FT Alert");
			bool stAlert = getIndicatorEnable("ST Alert");
			bool ftstAlert = getIndicatorEnable("FTST Alert");
			//bool scSigHistory = getIndicatorEnable("FT | SC PR");
			bool prSigCurrent = getIndicatorEnable("PR");
			//bool prSigHistory = getIndicatorEnable("Historical PR Sig");
			//bool netSigHistory = getIndicatorEnable("| Net");
			bool pSig = getIndicatorEnable("ATM P Alert");

			// ATM Turning Pts;
			bool fastTriggerMTTurningPoint = getIndicatorEnable("Mid Term FT Current");
			bool fastTriggerTurningPoint = getIndicatorEnable("Short Term FT Current");
			bool fastTriggerTurningPointHistory = getIndicatorEnable("Short Term FT Historical");
			bool fastTriggerTurningPointEst = getIndicatorEnable("Short Term FT Nxt Bar");
			bool slowTriggerTurningPoint = getIndicatorEnable("Short Term ST Current");
			bool slowTriggerTurningPointHistory = getIndicatorEnable("Short Term ST Historical");
			bool slowTriggerTurningPointEst = getIndicatorEnable("Short Term ST Nxt Bar");
			bool scBars = getIndicatorEnable("SC");

			// ATM Elliott Wave;
			bool elliottWaveCounts = getIndicatorEnable("EW Counts");
			bool elliottWavePTI = getIndicatorEnable("EW PTI");
			bool elliottWaveChannels = getIndicatorEnable("EW Channels");
			bool elliottWaveProjections = getIndicatorEnable("EW Projections");
			bool elliottWaveOscStudy = getIndicatorEnable("EW Major Osc");

			bool FTChartLinesStudy = getIndicatorEnable("Short Term FT Lines");

			// ATM Indicators;
			lock (_colorList)
			{
				_colorList.Clear();
				_colorIndex.Clear();
			}
			_study.calculateTrendBarStudy(times, series, trendBarsStudy && !scBarsStudy, _indicators["ATM Trend Bars"].Parameters);
			_study.calculateSCBarStudy(_scores, _RP, scBarsStudy, _indicators["ATM SC Bars"].Parameters, rev);

			_study.calculateTrendLineStudy(times, series, trendLinesStudy, _indicators["ATM Trend Lines"].Parameters);
			_study.calculateTargetLineStudy(times, series, targetLineStudy, currentBarIndex, _indicators["ATM Targets"].Parameters);
			_study.calculate3SigmaStudy(times, series, threeSigmaStudy, _indicators["ATM 3Sigma"].Parameters);
			_study.calculateTriggerStudy(times, series, currentBarIndex, triggerStudy, _indicators["ATM Trigger"].Parameters);
			_study.calculateADXStudy("ADX", times, series, adx, _indicators["ADX"].Parameters);
			_study.calculateDMIStudy("DMI", times, series, dmi, _indicators["DMI"].Parameters);
			_study.calculateDDIFStudy("D DIF", times, series, ddmi, _indicators["D DIF"].Parameters);
			_study.calculateATRStudy("ATR", times, series, atr, _indicators["ATR"].Parameters);


			_study.calculateATRXStudy("ATRX", getDataInterval(), times, series, atrx, _indicators["ATRX"].Parameters, _portfolioName, _portfolio.GetTrades(_portfolioName, Symbol));

			_study.calculateOSCStudy("OSC", times, series, osc, _indicators["OSC"].Parameters);
			_study.calculateBOLStudy("BOL", times, series, bol, _indicators["BOL"].Parameters);
			_study.calculateMACDStudy("MACD", times, series, macd, _indicators["MACD"].Parameters);
			_study.calculateMA200Study(times, series, ma200, _indicators["MA 200"].Parameters);
			_study.calculateMA100Study(times, series, ma100, _indicators["MA 100"].Parameters);
			_study.calculateMA50Study(times, series, ma50, _indicators["MA 50"].Parameters);
			_study.calculateMAStudy("MA 1", times, series, ma1, _indicators["MA 1"].Parameters);
			_study.calculateMAStudy("MA 2", times, series, ma2, _indicators["MA 2"].Parameters);
			_study.calculateMAStudy("MA 3", times, series, ma3, _indicators["MA 3"].Parameters);
			_study.calculateMOMStudy("MOM", times, series, mom, _indicators["MOM"].Parameters);
			_study.calculateROCStudy("ROC", times, series, roc, _indicators["ROC"].Parameters);
			_study.calculateRSIStudy("RSI", times, series, rsi, _indicators["RSI"].Parameters);

			var op = series[0];
			var hi = series[1];
			var lo = series[2];
			var cl = series[3];
			_nets = atm.calculateNetSig(op, hi, lo, cl, _PR, _RP, _scores, 2);

			_study.calculateXSignalStudy(_symbol, getDataInterval(), times, series, _scores, _RP, _PR, firstAlert, addOnAlert, pullbackAlert, exhaustionAlert,
				xAlert, false, pressureAlert, false, false, ftAlert, stAlert, ftstAlert, false, twoBarAlert, false,
				false, false, false,
				false, prSigCurrent, false, false, pSig, pressureTrendAlert, twoBarTrendAlert, _indicators);

			var sc = atm.calculateSCSig(_scores, _RP, 0);
			_study.updateSCStartDate(times, series[3], sc, false);

			var st = atm.calculateSTSig(series[0], series[1], series[2], series[3], 0);


			// ATM Turning Pts;
			_study.calculateFastTurningPoint(times, series, currentBarIndex, fastTriggerTurningPoint, fastTriggerTurningPointHistory, fastTriggerTurningPointEst, _indicators);
			_study.calculateSlowTurningPoint(times, series, currentBarIndex, slowTriggerTurningPoint, slowTriggerTurningPointHistory, slowTriggerTurningPointEst, _indicators);

			// ATM Elliott Wave;
			_study.calculateElliottWave(times, series, elliottWaveCounts, elliottWavePTI, elliottWaveChannels, elliottWaveProjections, currentBarIndex, _extraBars, _indicators);
			_study.calculateElliottWaveOscillator(times, series, elliottWaveOscStudy, _indicators["EW Major Osc"].Parameters);

			_study.calculateFTChartLineStudy(times, series, FTChartLinesStudy, 0, currentBarIndex, true);

			_study.calculateSCAdd(times, series, _scores, _RP, currentBarIndex, _indicators);
		}

		object _studyInfoDataLock = new object();
		List<Tuple<string, Color>> _studyInfoData = null;
		//Dictionary<int, List<Tuple<string, Color>>> _studyInfoDatas = new Dictionary<int, List<Tuple<string, Color>>>();

		public Dictionary<string, bool> GetSCAddEnbs()
		{
			var output = new Dictionary<string, bool>();
			output["New Trend"] = getIndicatorEnable("New Trend");
			output["Pressure"] = getIndicatorEnable("Pressure");
			output["Add"] = getIndicatorEnable("Add");
			//output["2 Bar"] =   getIndicatorEnable("2 Bar");
			output["Exh"] = getIndicatorEnable("Exh");
			output["Retrace"] = getIndicatorEnable("Retrace");
			output["ATR"] = getIndicatorEnable("ATR");
			return output;
		}

		private bool getIndicatorEnable(string name)
		{
			bool enable = false;
			Indicator indicator = null;
			_indicators.TryGetValue(name, out indicator);
			if (indicator != null)
			{
				enable = indicator.Enabled;
			}
			return enable;
		}

		private void updateConditionTrades(Dictionary<string, object> _referenceData)
		{
			if (MainView.EnableTradeConditions)
			{
				bool midTermSectorDirection = getIndicatorEnable("Mid Term Sector Turn");
				//bool midTermPDirection = getIndicatorEnable("Mid Term P Turn");
				bool midTermFTDirection = getIndicatorEnable("Mid Term FT Turn");
				bool midTermSTDirection = getIndicatorEnable("Mid Term ST Turn");
				bool midTermSCDirection = getIndicatorEnable("Mid Term SC Start");
				bool longTermFTDirection = getIndicatorEnable("Long Term FT Direction");

				if (midTermFTDirection || midTermSTDirection || longTermFTDirection || midTermSCDirection || midTermSectorDirection)

				{
					string chartInterval = getDataInterval();

					List<string> conditions = new List<string>(); // getConditionTrades().ToList();
					List<string> sectorConditions = new List<string>();
					if (midTermSectorDirection)
					{
						int type = 1;
						string ago = "1000";
						string interval = Study.getForecastInterval(_interval, 1);
						sectorConditions.Add("SC Up and FT TU" + "\u0002" + interval + "\u0002" + ago + "\u0002" + type);
						sectorConditions.Add("SC Up and FT TD" + "\u0002" + interval + "\u0002" + ago + "\u0002" + type);
						sectorConditions.Add("SC Dn and FT TU" + "\u0002" + interval + "\u0002" + ago + "\u0002" + type);
						sectorConditions.Add("SC Dn and FT TD" + "\u0002" + interval + "\u0002" + ago + "\u0002" + type);
					}
					//if (midTermPDirection)
					//{
					//	int type = 1;
					//	string ago = "1000";
					//	string interval = Study.getForecastInterval(_interval, 1);
					//	conditions.Add("SC Up and P TU" + "\u0002" + interval + "\u0002" + ago + "\u0002" + type);
					//	conditions.Add("SC Up and P TD" + "\u0002" + interval + "\u0002" + ago + "\u0002" + type);
					//	conditions.Add("SC Dn and P TU" + "\u0002" + interval + "\u0002" + ago + "\u0002" + type);
					//	conditions.Add("SC Dn and P TD" + "\u0002" + interval + "\u0002" + ago + "\u0002" + type);
					//}
					if (midTermFTDirection)
					{
						int type = 1;
						string ago = "1000";
						string interval = Study.getForecastInterval(_interval, 1);
						conditions.Add("SC Up and FT TU" + "\u0002" + interval + "\u0002" + ago + "\u0002" + type);
						conditions.Add("SC Up and FT TD" + "\u0002" + interval + "\u0002" + ago + "\u0002" + type);
						conditions.Add("SC Dn and FT TU" + "\u0002" + interval + "\u0002" + ago + "\u0002" + type);
						conditions.Add("SC Dn and FT TD" + "\u0002" + interval + "\u0002" + ago + "\u0002" + type);
					}
					if (midTermSTDirection)
					{
						int type = 1;
						string ago = "1000";
						string interval = Study.getForecastInterval(_interval, 1);
						conditions.Add("ST Turns Up 30" + "\u0002" + interval + "\u0002" + ago + "\u0002" + type);
						conditions.Add("");
						conditions.Add("ST Turns Dn 70" + "\u0002" + interval + "\u0002" + ago + "\u0002" + type);
						conditions.Add("");
					}
					if (midTermSCDirection)
					{
						int type = 1;
						string ago = "1000";
						string interval = Study.getForecastInterval(_interval, 1);
						conditions.Add("SC Sig Up Entry" + "\u0002" + interval + "\u0002" + ago + "\u0002" + type);
						conditions.Add("");
						conditions.Add("SC Sig Dn Entry" + "\u0002" + interval + "\u0002" + ago + "\u0002" + type);
						conditions.Add("");
					}

					bool ok = false;
					Dictionary<string, List<DateTime>> times = new Dictionary<string, List<DateTime>>();
					Dictionary<string, Series[]> bars = new Dictionary<string, Series[]>();
					if (conditions.Count > 0)
					{
						ok = true;
						List<string> intervals = getConditionIntervals(conditions);
						foreach (string interval in intervals)
						{
							times[interval] = _barCache.GetTimes(_symbol, interval, 0, _barCount);
							bars[interval] = GetSeries(_symbol, interval, new string[] { "Open", "High", "Low", "Close" }, 0, _barCount);
							if (times[interval] == null || times[interval].Count == 0)
							{
								ok = false;
								break;
							}
						}
					}

					if (ok)
					{
						_study.updateConditionTrades("ConditionTrade1", times, bars, _symbol, chartInterval, conditions.ToArray(), _referenceData);
					}
					else
					{
						for (int ii = 0; ii < _panels.Count; ii++)
						{
							_panels[ii].ClearTrendLines("ConditionTrade1");
						}
					}

					bool sectorOk = false;
					var sectorTicker = getSectorTicker();
					Dictionary<string, List<DateTime>> sectorTimes = new Dictionary<string, List<DateTime>>();
					Dictionary<string, Series[]> sectorBars = new Dictionary<string, Series[]>();
					if (sectorTicker.Length > 0 && sectorConditions.Count > 0)
					{
						sectorOk = true;
						List<string> intervals = getConditionIntervals(sectorConditions);
						foreach (string interval in intervals)
						{
							sectorTimes[interval] = _barCache.GetTimes(sectorTicker, interval, 0, _barCount);
							sectorBars[interval] = GetSeries(sectorTicker, interval, new string[] { "Open", "High", "Low", "Close" }, 0, _barCount);
							if (sectorTimes[interval] == null || sectorTimes[interval].Count == 0)
							{
								sectorOk = false;
								break;
							}
						}
					}

					if (sectorOk)
					{
						_study.updateConditionTrades("SectorConditionTrade", sectorTimes, sectorBars, sectorTicker, chartInterval, sectorConditions.ToArray(), _referenceData);
					}
					else
					{
						for (int ii = 0; ii < _panels.Count; ii++)
						{
							_panels[ii].ClearTrendLines("SectorConditionTrade");
						}
					}
				}
				else
				{
					for (int ii = 0; ii < _panels.Count; ii++)
					{
						_panels[ii].ClearTrendLines("ConditionTrade1");
						_panels[ii].ClearTrendLines("SectorConditionTrade");
					}
				}
			}
			else
			{
				for (int ii = 0; ii < _panels.Count; ii++)
				{
					_panels[ii].ClearTrendLines("ConditionTrade1");
					_panels[ii].ClearTrendLines("SectorConditionTrade");
				}
			}
		}

		private List<string> getConditionIntervals(List<string> conditions)
		{
			string chartInterval = getDataInterval();

			List<string> intervals = getConditionTradeIntervals();
			for (int type = 0; type < conditions.Count; type++)
			{
				List<string> conditionIntervals = Conditions.GetIntervals(conditions[type]);
				foreach (string interval in conditionIntervals)
				{
					if (!intervals.Contains(interval))
					{
						intervals.Add(interval);
					}
				}
			}
			if (!intervals.Contains(chartInterval))
			{
				intervals.Add(chartInterval);
			}

			return intervals;
		}

		private string getSectorTicker()
		{
			var sectorTicker = "";
			var sector = _portfolio.getSectorCode(_symbol);
			if (sector.Length > 0)
			{
				sectorTicker = _portfolio.getSectorSymbol(int.Parse(sector));
			}
			return sectorTicker;
		}

		private string[] getConditionTrades()
		{
			string[] input = MainView.GetConditions(_horizon);
			int conditionCount = input.Length;

			string[] output = new string[conditionCount];

			string[] terms = { "Short Term", "Mid Term", "Long Term" };
			for (int type = 0; type < conditionCount; type++)
			{
				string condition = input[type];
				for (int level = 0; level < 3; level++)
				{
					string interval = Study.getForecastInterval(_interval, level);
					condition = condition.Replace(terms[level], interval);
				}
				output[type] = condition;
			}
			return output;
		}
		private List<string> getConditionTradeIntervals()
		{
			List<string> intervals = new List<string>();
			string[] conditions = getConditionTrades();
			for (int type = 0; type < conditions.Length; type++)
			{
				List<string> conditionIntervals = Conditions.GetIntervals(conditions[type]);
				foreach (string interval in conditionIntervals)
				{
					if (!intervals.Contains(interval))
					{
						intervals.Add(interval);
					}
				}
			}
			return intervals;
		}

		string _tradeKey = "";
		List<Trade> _trades = new List<Trade>();
		private Dictionary<DateTime, double> _dailyNavCache = null;

		public void ResetTrades()
		{
			_tradeKey = "";
			_dailyNavCache = null;
		}

		private void updateTradeEvents()
		{
			bool currentTradeEvents = true; // MainView.EnableRickStocks && getIndicatorEnable("Positions");
			bool historicalTradeEvents = true; // MainView.EnableHistoricalRecommendations && getIndicatorEnable("Historical Positions");
			bool riskArea = getIndicatorEnable("Risk Area");

			string interval = getIntervalAbbreviation(_interval);


			string[] tickers = _symbol.Split(ATMML.Symbol.SpreadCharacter);


			var tradeKey = _symbol + ":" + interval;
			if (_tradeKey != tradeKey)
			{
				_tradeKey = tradeKey;
				_trades = _portfolio.GetTrades(_portfolioName, tickers[0]);
				if (_trades.Count == 0)
				{
					TradeHorizon horizon = Trade.Manager.GetIntervalTradeHorizon(interval);
					_trades = Trade.Manager.getTradesForSymbol(_portfolioName, horizon, _symbol);
				}
			}

			//List<Bar> bars1 = _barCache.GetBars(_symbol, getDataInterval(), 0, 200);
			//List<Bar> bars2 = _barCache.GetBars("SPX Index", getDataInterval(), 0, 200);
			//_study.updateTradeEvents(_trades, _cursorTime, bars1, bars2, currentTradeEvents, historicalTradeEvents);
		}

		private double getUnits(DateTime date, bool total)
		{
			var output = 0.0;
			var trades = _trades.Where(x => x.OpenDateTime <= date && (x.CloseDateTime == default(DateTime) || date <= x.CloseDateTime)).ToList();
			if (trades.Count > 0)
			{
				var trade = trades.Last();
				if (trade.Shares.ContainsKey(date))
				{
					if (total)
					{
						output = Math.Sign(trade.Direction) * trade.Shares[date];
					}
					else
					{
						var dates = trade.Shares.Keys.OrderBy(x => x).ToList();
						var ix1 = Math.Max(1, dates.FindIndex(x => x == date)) - 1;
						var previousShares = trade.Shares[dates[ix1]];
						var currentShares = trade.Shares[date];
						output = Math.Sign(trade.Direction) * (currentShares - previousShares);
					}
				}
			}
			return output;
		}

		/// <summary>
		/// Returns the position's percent profit at the given date.
		/// Calculated as: direction * (currentClose - entryClose) / entryClose * 100
		/// </summary>
		private double getPositionPctProfit(DateTime date)
		{
			var trades = _trades.Where(x => x.OpenDateTime <= date && (x.IsOpen() || date <= x.CloseDateTime)).ToList();
			if (trades.Count > 0)
			{
				// Pick the trade whose OpenDateTime is closest to (but not after) the cursor date.
				// This prevents stale closed trades with unset CloseDateTimes from being selected.
				var trade = trades.OrderByDescending(x => x.OpenDateTime).First();
				var direction = Math.Sign(trade.Direction);

				// Find the bar index for the cursor date
				int cursorIndex = -1;
				int entryIndex = -1;
				for (int i = 0; i < _bars.Count; i++)
				{
					if (_bars[i].Time == date)
						cursorIndex = i;
					if (_bars[i].Time == trade.OpenDateTime)
						entryIndex = i;
				}

				// If exact match not found for entry, find closest bar on or after open
				if (entryIndex < 0)
				{
					for (int i = 0; i < _bars.Count; i++)
					{
						if (_bars[i].Time >= trade.OpenDateTime)
						{
							entryIndex = i;
							break;
						}
					}
				}

				if (cursorIndex >= 0 && entryIndex >= 0 &&
					!double.IsNaN(_bars[cursorIndex].Close) && !double.IsNaN(_bars[entryIndex].Close) &&
					_bars[entryIndex].Close != 0)
				{
					// Use blended AvgPrice if available (correct for positions with adds/trims),
					// otherwise fall back to the bar close at entry date
					double entryPrice = (trade.AvgPrice != null && trade.AvgPrice.Count > 0)
						? trade.AvgPrice.OrderByDescending(kvp => kvp.Key).First().Value
						: _bars[entryIndex].Close;

					double currentPrice = _bars[cursorIndex].Close;
					return direction * 100.0 * (currentPrice - entryPrice) / entryPrice;
				}
			}
			return double.NaN;
		}

		/// <summary>
		/// Returns the position's notional value as a percent of total portfolio value.
		/// Calculated as: |shares * close| / portfolioNAV * 100
		/// Portfolio NAV is loaded from dailyNav.csv
		/// </summary>
		private double getPositionPctOfPortfolio(DateTime date)
		{
			var trades = _trades.Where(x => x.OpenDateTime <= date && (x.IsOpen() || date <= x.CloseDateTime)).ToList();
			if (trades.Count > 0)
			{
				var trade = trades.OrderByDescending(x => x.OpenDateTime).First();
				if (trade.Shares.ContainsKey(date))
				{
					var shares = trade.Shares[date];

					// Find the bar close at the cursor date
					double closePrice = double.NaN;
					for (int i = 0; i < _bars.Count; i++)
					{
						if (_bars[i].Time == date)
						{
							closePrice = _bars[i].Close;
							break;
						}
					}

					if (!double.IsNaN(closePrice) && closePrice != 0)
					{
						double positionValue = Math.Abs(shares * closePrice);
						double portfolioNAV = getDailyNav(date);
						if (portfolioNAV > 0)
						{
							return 100.0 * positionValue / portfolioNAV;
						}
					}
				}
			}
			return double.NaN;
		}

		/// <summary>
		/// Loads the portfolio NAV for the given date from dailyNav.csv.
		/// File lives under portfolios\reports\{portfolioName}\{timestamp}\dailyNav.csv
		/// Uses the most recent timestamp folder.
		/// </summary>
		private double getDailyNav(DateTime date)
		{
			try
			{
				// Load and cache on first call
				if (_dailyNavCache == null)
				{
					_dailyNavCache = new Dictionary<DateTime, double>();

					string basePath = @"C:\Users\Public\Documents\ATMML\portfolios\reports\" + _portfolioName;
					if (!System.IO.Directory.Exists(basePath))
						return 0;

					var directories = System.IO.Directory.GetDirectories(basePath);
					if (directories.Length == 0)
						return 0;

					Array.Sort(directories);
					string latestDir = directories[directories.Length - 1];
					string filePath = latestDir + @"\dailyNav.csv";

					if (!System.IO.File.Exists(filePath))
						return 0;

					var lines = System.IO.File.ReadAllLines(filePath);
					foreach (var line in lines)
					{
						if (line.Trim().Length == 0 || line.Trim().StartsWith("Date"))
							continue;

						var parts = line.Split(',');
						if (parts.Length >= 2)
						{
							DateTime lineDate;
							double lineNav;
							if (DateTime.TryParse(parts[0].Trim(), out lineDate) && double.TryParse(parts[1].Trim(), out lineNav))
							{
								_dailyNavCache[lineDate] = lineNav;
							}
						}
					}
				}

				// Find NAV on or before the requested date
				double nav = 0;
				foreach (var kvp in _dailyNavCache.OrderBy(x => x.Key))
				{
					if (kvp.Key <= date)
						nav = kvp.Value;
					else
						break;
				}
				return nav;
			}
			catch
			{
				return 0;
			}
		}

		public void update()
		{
			updateTimeScale();
			updateValueScale();

			draw(); // update
		}

		public string GetOverviewInterval(string interval, int level)
		{
			return Study.getForecastInterval(interval, level);
		}

		public string getDataInterval()
		{
			return _interval;
		}

		public void Change(string symbol, string interval, bool changeStockSymbol = true)
		{
			try
			{
				if (interval == "")
				{
					interval = getDataInterval();
				}

				if (symbol != _symbol || interval != _interval)   //comment out if problem 
				{
					List<string> intervals = getDataIntervals();
					foreach (string interval1 in intervals)
					{
						_barCache.Clear(_symbol, interval1, false);
					}

					_symbol = symbol;
					if (changeStockSymbol) _stkSymbol = _symbol;
					_symbolName = _symbol;
					_interval = interval;

					waitForStudyThreadToComplete();

					lock (_studyInfoDataLock)
					{
						_studyInfoData = null;
					}

					lock (_barLock)
					{
						_bars.Clear();
					}

					_clearEdit = true;

					_study.clearBarColors();

					if (AnalysisEnable)
					{
						_aod.Clear();
					}

					setPredictionHistogramEnable();

					initializeIndicatorTree();

					if (_displayPerformance)
					{
						_recordPerformance = true;
						_performanceStopwatch = Stopwatch.StartNew();
					}

					_expectedReportDate = new DateTime();
					_expectedNxtDVDDate = new DateTime();
					_expectedDVDExDate = new DateTime();
					_expectedDVDPayDate = new DateTime();
					_referenceData = null;
					_indexSymbol = "";
					_industryGroup = "";
					_symbolPrice = double.NaN;
					_sectorPrice = double.NaN;
					_sectorETFPrice = double.NaN;
					_beta120 = double.NaN;
					_sectorBeta = double.NaN;
					_sectorETFBeta = double.NaN;
					_bestTargetPrice = double.NaN;
					_bestTargetHi = double.NaN;
					_bestTargetLo = double.NaN;

					string[] dataFieldNames = { "EXPECTED_REPORT_DT",  /*"REL_INDEX",*/ "GICS_SECTOR", /*"PR910",*/ "GICS_INDUSTRY_GROUP_NAME", "BDVD_NEXT_EST_DECL_DT", "BDVD_NEXT_EST_EX_DT", "BDVD_NEXT_EST_PAY_DT" };

					string[] tickers = _symbol.Split(ATMML.Symbol.SpreadCharacter);
					_portfolio.RequestReferenceData(tickers[0], dataFieldNames);

					_intervalText.Text = getIntervalLabel(_interval);

					// loadBars();

					_canvas.Children.Clear();
					_referenceVerticalLine = null;
					_referenceHorizontalLine = null;

					for (int ii = 0; ii < _panels.Count; ii++)
					{
						_panels[ii].Clear();
					}

					drawBackground();
					drawTitle();
					drawSubPanelBorders();
					drawBorder();
					drawControlBoxes();
					drawReviewAction();
					drawBusyIndicator();

					//draws();
					requestBars();
				}
			}
			catch (Exception x)
			{
			}
		}

		bool _loadingBars = false;

		private void requestBars()
		{
			try
			{
				if (_interval.Length > 0)
				{
					_loadingBars = true;
					_requestBarTime = DateTime.Now;

					List<string> intervals = getDataIntervals();
					foreach (string interval in intervals)
					{
						_barCache.RequestBars(_symbol, interval, true, _barCount, true);
					}

					var intervalsText = "";
					foreach (var text in intervals) intervalsText += (" " + text);
					//Trace.WriteLine("Request bars for " + _interval + " chart." + " Requesting " + intervalsText);

					_indexSymbol = _portfolio.GetRelativeIndex(_symbol);

					_barCache.RequestBars(_indexSymbol, getDataInterval(), true, _barCount, true);

					_sectorETFSymbol = _portfolio.getSectorETFSymbol(_indexSymbol);
					_barCache.RequestBars(_sectorETFSymbol, getDataInterval(), true, _barCount, true);

					var sectorTicker = getSectorTicker();
					if (sectorTicker.Length > 0)
					{
						foreach (string interval in intervals)
						{
							_barCache.RequestBars(sectorTicker, interval, true, _barCount, true);
						}
					}

					_symbolEditor.Text = _symbolName;
				}
			}
			catch
			{
			}
		}

		private List<string> getDataIntervals()
		{
			List<string> intervals = new List<string>();

			string interval = getDataInterval();
			intervals.Add(interval);

			// add mid term interval for the score calculation
			string midTermInterval = Study.getForecastInterval(interval, 1);
			intervals.Add(midTermInterval);

			bool ft1 = getIndicatorEnable("Use ATM FT 1 Time Frame Higher");
			bool ft2 = getIndicatorEnable("Use ATM FT 2 Time Frames Higher");
			bool st1 = getIndicatorEnable("Use ATM ST 1 Time Frame Higher");
			bool st2 = getIndicatorEnable("Use ATM ST 2 Time Frames Higher");

			int level = 0;
			bool colorCursor = (ft1 || ft2 || st1 || st2);
			if (colorCursor)
			{
				level = (ft1 || st1) ? 1 : 2;
				string interval1 = Study.getForecastInterval(interval, level);
				if (interval1 != interval)
				{
					if (!intervals.Contains(interval1))
					{
						intervals.Add(interval1);
					}
				}
			}

			bool multipleForecasts = getIndicatorEnable("Longer Time Frame Forecasts");
			bool ftLinesMidTerm = getIndicatorEnable("Mid Term FT Lines");
			bool ftLinesLongTerm = getIndicatorEnable("Long Term FT Lines");
			if (level != 1 && (ftLinesMidTerm || multipleForecasts))
			{
				string interval1 = Study.getForecastInterval(interval, 1);
				if (!intervals.Contains(interval1))
				{
					intervals.Add(interval1);
				}
			}

			if (level != 2 && (ftLinesLongTerm || multipleForecasts))
			{
				string interval2 = Study.getForecastInterval(interval, 2);
				if (!intervals.Contains(interval2))
				{
					intervals.Add(interval2);
				}
			}
			return intervals;
		}

		private void drawBusyIndicator()
		{
			Canvas canvas = _panels[0].Canvas;

			int size = 30;
			LoadingAnimation busy = new LoadingAnimation(size, size);
			double width = (canvas.Width > 0) ? canvas.Width : canvas.ActualWidth;
			double height = (canvas.Height > 0) ? canvas.Height : canvas.ActualHeight;
			double xx = (width - size) / 2;
			double yy = (height - size) / 2;
			busy.SetValue(Canvas.LeftProperty, xx);
			busy.SetValue(Canvas.TopProperty, yy);
			canvas.Children.Add(busy);
		}

		public List<Color> ColorList
		{
			get { return _colorList; }
			set { _colorList = value; }
		}

		public List<int> ColorIndex
		{
			get { return _colorIndex; }
			set { _colorIndex = value; }
		}

		public bool Busy
		{
			get { return _busy; }
		}

		bool _drawAod = false;

		public void OnTimer(object o, EventArgs sender)
		{
			if (_drawAod)
			{
				_drawAod = false;
				drawAOD();
			}

			if (_calculateHistoricalAdvice)
			{
				if ((DateTime.Now - _changeCursorTime).TotalMilliseconds >= 1000)
				{
					_calculateHistoricalAdvice = false;
					_adviceBarIndex = getTimeIndex(_cursorTime);
					_updateChart.Add(getDataInterval());
				}
			}

			//if (!_contextMenuOpen)
			{
				if (_symbolDrop)
				{
					string symbol = _symbolEditor.Text;
					if (symbol.Length > 0)
					{
						processSymbolEdit();
						_symbolDrop = false;
					}
				}

				if (IsVisible())
				{
					List<string> updateList = null;
					lock (_updateChart)
					{
						updateList = new List<string>(_updateChart);
						_updateChart.Clear();
					}
					updateList.ForEach(x => updateChart(x));
				}

				if (_update)
				{
					_update = false;
					update();
					onChartEvent(new ChartEventArgs(ChartEventType.Update));
				}
			}

			if (_workingLabel != null)
			{
				_workingLabel.Visibility = _displayWorking ? Visibility.Visible : Visibility.Collapsed;
			}

		}
		void KeyUp(object sender, KeyEventArgs e)
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

		private void symbolEditor_KeyDown(object sender, KeyEventArgs e)
		{
		}

		private void symbolEditor_PreviewKeyDown(object sender, KeyEventArgs e)
		{

			KeyDown(sender, e);
		}

		void KeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
			{
				_controlModifier = true;
			}
			else if (e.Key == Key.LeftShift || e.Key == Key.RightShift)
			{
				_shiftModifier = true;
			}
			else if (e.Key == Key.Left)
			{
				if (_controlModifier)
				{
					removeTrade();   // control left arrow (delete)
					e.Handled = true;
				}
				else
				{
					MoveCursor(-1);
					e.Handled = true;
				}
			}
			else if (e.Key == Key.Right)
			{
				if (_controlModifier)
				{
					addTrade("CMR", 0, true);  // control right arrow = 0 (exit)   was OEX MT
					e.Handled = true;
				}
				else
				{
					MoveCursor(1);
					e.Handled = true;
				}
			}
			else if (e.Key == Key.Up)
			{
				if (_controlModifier)
				{
					if (_shiftModifier)
					{
						addTrade("CMR", 5, true);  // control shift up arrow = risk line (stop out of short)  was OEX MT
					}
					else
					{
						addTrade("CMR", 1, true);  // control up arrow = 1 (enter long)   was OEX MT
					}
					e.Handled = true;
				}
			}
			else if (e.Key == Key.Down)
			{
				if (_controlModifier)
				{
					if (_shiftModifier)
					{
						addTrade("CMR", -5, true);  // control shift down arrow = risk line (stop out of long)  was OEX MT
					}
					else
					{
						addTrade("CMR", -1, true);  // control down arrow = -1 (enter short)  was OEX MT
					}
					e.Handled = true;
				}
			}
			else if (e.Key == Key.Space)
			{
				_symbolEditor.Text += " ";
				e.Handled = true;
			}
			else if (e.Key == Key.Back)
			{
				if (_symbolEditor.Text.Length > 0)
				{
					var index = _symbolEditor.SelectionStart;
					if (index == 0 || index >= _symbolEditor.Text.Length) index = _symbolEditor.Text.Length - 1;
					_symbolEditor.Text = _symbolEditor.Text.Remove(index, 1);
					e.Handled = true;
				}
			}
			else if (e.Key == Key.F1)
			{
				string command = _symbolEditor.Text;
				command += " People";
				_symbolEditor.Text = command;
				e.Handled = true;
			}
			else if (e.Key == Key.F2)
			{
				string command = _symbolEditor.Text;
				command += " Govt";
				_symbolEditor.Text = command;
				e.Handled = true;
			}
			else if (e.Key == Key.F3)
			{
				string command = _symbolEditor.Text;
				command += " Corp";
				_symbolEditor.Text = command;
				e.Handled = true;
			}
			else if (e.Key == Key.F4)
			{
				string command = _symbolEditor.Text;
				command += " Mtge";
				_symbolEditor.Text = command;
				e.Handled = true;
			}
			else if (e.Key == Key.F5)
			{
				string command = _symbolEditor.Text;
				command += " M-Mkt";
				_symbolEditor.Text = command;
				e.Handled = true;
			}
			else if (e.Key == Key.F6)
			{
				string command = _symbolEditor.Text;
				command += " Muni";
				_symbolEditor.Text = command;
				e.Handled = true;
			}
			else if (e.Key == Key.F7)
			{
				string command = _symbolEditor.Text;
				command += " Pfd";
				_symbolEditor.Text = command;
				e.Handled = true;
			}
			else if (e.Key == Key.F8)
			{
				string command = _symbolEditor.Text;
				command += " Equity";
				_symbolEditor.Text = command;
				e.Handled = true;
			}
			else if (e.Key == Key.F9)
			{
				string command = _symbolEditor.Text;
				command += " Comdty";
				_symbolEditor.Text = command;
				e.Handled = true;
			}
			else if (e.Key == Key.F10 || e.Key == Key.System)
			{
				string command = _symbolEditor.Text;
				command += " Index";
				_symbolEditor.Text = command;
				e.Handled = true;
			}
			else if (e.Key == Key.F11)
			{
				string command = _symbolEditor.Text;
				command += " Curncy";
				_symbolEditor.Text = command;
				e.Handled = true;
			}
			else if (e.Key == Key.F12)
			{
				string command = _symbolEditor.Text;
				command += " Client";
				_symbolEditor.Text = command;
				e.Handled = true;
			}
			else if (e.Key == Key.Return)
			{
				processSymbolEdit();
				e.Handled = true;
			}
		}

		void processSymbolEdit()
		{
			string command = _symbolEditor.Text;
			command = command.Trim();

			string[] fields = command.Split(ATMML.Symbol.SpreadCharacter);
			command = "";
			for (int ii = 0; ii < fields.Length; ii++)
			{
				if (ii > 0) command += ATMML.Symbol.SpreadCharacter;
				string symbol = fields[ii].Trim();
				if (!Portfolio.isCQGSymbol(symbol) && !symbol.Contains(' '))
				{
					symbol += " US Equity";
				}
				command += symbol;
			}

			if (command.Length > 0)
			{
				processCommand(command);
				command = "";
				_symbolEditor.Text = command;
			}
		}

		void TextInput(object sender, TextCompositionEventArgs e)
		{
			string command = _clearEdit ? "" : _symbolEditor.Text;
			_clearEdit = false;

			string text = e.Text;

			if (text == "\b")
			{
				if (command.Length > 0)
				{
					command = command.Substring(0, command.Length - 1);
					_symbolEditor.Text = command;
				}
			}
			else if (text == "\r")
			{
				command = command.Trim();
				if (command.Length > 0)
				{
					processCommand(command);
					command = "";
					_symbolEditor.Text = command;
				}
			}
			else
			{
				text = text.ToUpper();
				command += text;
				_symbolEditor.Text = command;
			}
		}

		private void processCommand(string command)
		{
			Change(command, "");

			foreach (Chart chart in _linkedCharts)
			{
				chart.Change(command, "");
			}
			onChartEvent(new ChartEventArgs(ChartEventType.ChangeSymbol));
		}

		private void removeTrade()
		{
			if (MainView.EnableEditTrades)
			{
				string date = _cursorTime.ToString(isIntervalIntraday() ? "yyyy-MM-dd HH:mm" : "yyyy-MM-dd");
				string interval = getIntervalAbbreviation(_interval);

				TradeHorizon horizon = Trade.Manager.GetIntervalTradeHorizon(_interval);

				Trade.Manager.RemovePosition(_portfolioName, horizon, _symbol, date);

				updateTradeEvents();
				update();
				onChartEvent(new ChartEventArgs(ChartEventType.TradeEvent));
			}
		}

		private void addTrade(string groupName, int direction, bool useCursorTime = false)
		{
			if (MainView.EnableEditTrades)
			{
				bool CMR = (groupName == "CMR");
				bool OEX = !CMR && (_portfolioName == "CMR");

				DateTime time = useCursorTime ? _cursorTime : DateTime.UtcNow; // _bars[_currentIndex].Time;
				if (time == default(DateTime)) time = DateTime.UtcNow;
				if (time > DateTime.UtcNow) time = DateTime.UtcNow;

				string date = time.ToString("yyyy-MM-dd HH:mm");

				int index = getTimeIndex(time);

				lock (_barLock)
				{

					if (index >= 0 && index < _bars.Count)
					{
						double hi = _bars[index].High;
						double lo = _bars[index].Low;
						double cl = _bars[index].Close;
						string interval = getIntervalAbbreviation(_interval);

						double price = (direction == 5) ? hi : ((direction == -5) ? lo : cl);

						TradeHorizon horizon = Trade.Manager.GetIntervalTradeHorizon(_interval);
						var name = CMR ? "CMR" : OEX ? "OEX MT" : _portfolioName;

						string comment = "";
						double score = _scores[index];
						double net = _nets[index];
						if (direction != 0)
						{
							comment =
								"The stock's momentum score is " + score.ToString("00.00") + ". " +
								"The stock's net strategy is " + net.ToString("00.00") + ". " +
								"Price is at inflection point.";
						}
						else
						{
							comment =
								"The stock is losing momentum. " +
								"Price is at an exhaustion area.";
						}

						Trade.Manager.ChangePosition(name, horizon, _symbol, direction, date, price, comment);
						updateTradeEvents();
						update();
						onChartEvent(new ChartEventArgs(ChartEventType.TradeEvent));
					}
				}
			}
		}

		public int BarCount
		{
			get { return _endIndex - _startIndex; }
		}

		void MouseEnter(object sender, MouseEventArgs e)
		{
			_active = true;
			onChartEvent(new ChartEventArgs(ChartEventType.Activate));
			draw();
		}

		void MouseLeave(object sender, MouseEventArgs e)
		{
			_active = false;
			changeCursorTime(new DateTime());
			draw();
		}

		private int getInitialStartIndex()
		{
			int barCount = 0;
			if (_desiredBarCount != 0)
			{
				barCount = _desiredBarCount;
			}
			else
			{
				barCount = _endIndex - _startIndex;
				if (barCount == 0)
				{
					string barCountText = MainView.GetSetting("BarCount" + _horizon.ToString());
					if (barCountText.Length > 0)
					{
						barCount = int.Parse(barCountText);
					}

					if (barCount == 0)
					{
						barCount = 100;
					}
				}
			}

			int endIndex = getInitialEndIndex();
			int startIndex = endIndex - barCount;
			if (startIndex < 0)
			{
				_desiredBarCount = barCount;
				startIndex = 0;
			}
			else
			{
				_desiredBarCount = 0;
			}
			if (startIndex < 0)
			{
				startIndex = 0;
			}
			return startIndex;
		}

		private int getInitialEndIndex()
		{
			var barOffset = (double.IsNaN(_timeScale)) ? 20 : (int)(80 / _timeScale);

			var barCount = 0;
			lock (_bars)
			{
				barCount = _bars.Count;
			}
			int endIndex = barCount - _extraBars + barOffset;
			if (endIndex < 0)
			{
				endIndex = 0;
			}
			return endIndex;
		}

		public bool ScrollToTime(DateTime time)
		{
			bool change = false;

			changeCursorTime(time);

			int barCount = _endIndex - _startIndex;

			int endIndex = getTimeIndex(time) + barCount / 2;

			if (_endIndex != endIndex)
			{
				_startIndex = endIndex - (_endIndex - _startIndex);
				if (_startIndex < 0) _startIndex = 0;
				_endIndex = _startIndex + barCount;
				change = true;
			}
			return change;
		}


		public bool ScrollToCurrentTime()
		{
			bool change = false;

			changeCursorTime(new DateTime());

			int barCount = _endIndex - _startIndex;

			int endIndex = getInitialEndIndex();

			if (_endIndex != endIndex)
			{
				_startIndex = endIndex - (_endIndex - _startIndex);
				if (_startIndex < 0) _startIndex = 0;
				_endIndex = _startIndex + barCount;
				change = true;
			}
			return change;
		}

		public void MoveCursor(int direction)
		{
			if (!isTimeValid(_cursorTime))
			{
				var time = new DateTime();
				lock (_bars)
				{
					time = _bars[(direction == 1) ? _startIndex : _endIndex - 1].Time;
				}
				changeCursorTime(time);
			}
			else if (direction != 0)
			{
				int index = getTimeIndex(_cursorTime);

				if (direction > 0) // right
				{
					index = index + 1;
				}
				else // left
				{
					index = index - 1;
				}

				var time = new DateTime();
				lock (_barLock)
				{
					if (index >= _startIndex && index < _endIndex)
					{
						time = _bars[index].Time;
					}
				}
				changeCursorTime(time);
			}
		}

		public void SetCursorOn(bool on)
		{
			_cursorOn = on;
		}

		public void ChangeCursorValue(double value)
		{
			_cursorValue = value;

			if (!double.IsNaN(_cursorValue))
			{
				if (isTimeValid(_cursorTime))
				{
					int index = getTimeIndex(_cursorTime);
					if (index >= _startIndex && index < _endIndex)
					{
						addCursorLine();
						if (_cursorVerticalLine != null)
						{
							var x = getTimePixel(index);
							if (!double.IsNaN(x))
							{
								_cursorVerticalLine.X1 = x;
								_cursorVerticalLine.X2 = x;
							}
						}
						drawCursorInfo();
						//_cursorInfo.Visible = true;

						onChartEvent(new ChartEventArgs(ChartEventType.Cursor, _cursorTime));
					}
					else
					{
						removeCursorLine();
						//_cursorInfo.Visible = false;
					}
				}
				else
				{
					addCursorLine();
					//_cursorInfo.Visible = false;
				}
			}
			else
			{
				addCursorLine();
			}
		}

		private int getAdjustedTimeIndex(DateTime time1, string interval1)
		{
			var index = 0;
			lock (_barLock)
			{
				int barCount = _bars.Count;

				index = barCount - 1;
				if (interval1 == _interval)
				{
					index = getTimeIndex(time1);
				}
				else
				{
					int currentIndex = barCount - _extraBars - 1;
					index = currentIndex;
					int start1 = interval1.Length - 1;
					int start2 = _interval.Length - 1;
					string i1 = interval1.Substring(start1, 1);
					string i2 = _interval.Substring(start2, 1);
					int keySize1 = (i1 == "Y") ? 4 : ((i1 == "S" || i1 == "Q" || i1 == "M") ? 6 : ((i1 == "W" || i1 == "D") ? 8 : 12));
					int keySize2 = (i2 == "Y") ? 4 : ((i2 == "S" || i2 == "Q" || i2 == "M") ? 6 : ((i2 == "W" || i2 == "D") ? 8 : 12));
					int keySize = Math.Min(keySize1, keySize2);

					int offset = 0;
					if (i2 == "W" && keySize == 8) offset = 5;  //to place at current offset = 0
					else if (i2 == "Q" && keySize == 6) offset = 3;  //to place at current offset = 0
					else if (i2 == "S" && keySize == 6) offset = 6;  //to place at current offset = 0

					long key1 = long.Parse(time1.ToString("yyyyMMddHHmm").Substring(0, keySize));
					for (int ii = barCount - 1; ii >= 0; ii--)
					{
						DateTime time2 = _bars[ii].Time;
						long key2 = long.Parse(time2.ToString("yyyyMMddHHmm").Substring(0, keySize));
						if (key2 - offset <= key1)
						{
							index = ii;
							break;
						}
					}
				}
			}

			return index;
		}

		private void updatePrediction()
		{
			int panelIndex = GetPanelIndex("PREDICTION");
			if (panelIndex != -1)
			{
				var text1 = "";
				var text2 = "";
				var text3 = "";
				var text4 = "";
				//var text5 = "";
				//var text6 = "";
				var text7 = "";

				var modelName = _modelNames[0];
				var model = MainView.GetModel(modelName);
				if (model != null)
				{
					var scenario = model.Scenario;
					var cursorBarIndex = isTimeValid(_cursorTime) ? getTimeIndex(_cursorTime) : _currentIndex;

					var label = MainView.GetSenarioLabel(scenario);
					text1 = label.Trim();
					var index1 = text1.IndexOf("|");

					if (index1 != -1)
					{
						var interval = getDataInterval();
						var referencePrice = text1.Substring(index1 + 2, 1).Replace("O", "O").Replace("H", "H").Replace("L", "L").Replace("C", "C");
						var forecastPrice = text1.Substring(0, 1).Replace("O", "O").Replace("H", "H").Replace("L", "L").Replace("C", "C");
						var referenceIndex = int.Parse(text1.Substring(text1.Length - 1, 1)) - 1;
						var forecastIndex = int.Parse(text1.Substring(index1 - 2, 1));

						var barIndex1 = cursorBarIndex - referenceIndex - forecastIndex;
						var barIndex2 = cursorBarIndex - forecastIndex;
						lock (_barLock)
						{
							if (barIndex1 >= 0 && barIndex1 < _bars.Count && barIndex2 >= 0 && barIndex2 < _bars.Count)
							{
								var time = _bars[barIndex2].Time;
								var predictions = _mpst.getPredictions(_symbol, interval, modelName);
								if (predictions.ContainsKey(time))
								{
									var up = predictions[time] > 0.5;

									var fp = _bars[cursorBarIndex].Close;
									if (forecastPrice == "O") fp = _bars[cursorBarIndex].Open;
									else if (forecastPrice == "H") fp = _bars[cursorBarIndex].High;
									else if (forecastPrice == "L") fp = _bars[cursorBarIndex].Low;

									var rp = _bars[barIndex1].Close;
									if (referencePrice == "O") rp = _bars[barIndex1].Open;
									else if (referencePrice == "H") rp = _bars[barIndex1].High;
									else if (referencePrice == "L") rp = _bars[barIndex1].Low;

									var key = _symbol + ":" + interval;
									var bias = model.Biases.ContainsKey(key) ? model.Biases[key] : 0;
									rp += bias;

									var accuracy = getAccuracy();

									text2 = forecastPrice + " + " + forecastIndex + " = " + (double.IsNaN(fp) ? "" : fp.ToString("0.00"));
									text3 = up ? "HIGHER THAN" : "LOWER THAN";
									//text4 = referencePrice + " - " + (referenceIndex + 1) + " = " + (double.IsNaN(rp) ? "" : rp.ToString("0.00"));
									text4 = (double.IsNaN(rp) ? "" : rp.ToString("0.00"));

									//text5 = _bars[cursorBarIndex - referenceIndex].GetTimeLabel(isIntervalIntraday());
									//text6 = (Char.IsLetter(interval[0]) ? interval.Substring(0, 1) : interval) + " PREDICTION";
									text7 = "ACC = " + accuracy.ToString("0%");

									//drawPredictionCursor();
								}
							}
						}
					}
				}
				Panels[panelIndex].Title = text2 + "\n" + text3 + "\n" + text4 + "\n" + text7;
				//Panels[panelIndex].Title = text6 + "\n" + text2 + "\n" + text3 + "\n" + text4 + "\n" + text7;

				draw();
			}
		}

		Line _referenceVerticalLine = null;
		Line _referenceHorizontalLine = null;


		void erasePredictionLines()
		{
			if (_referenceVerticalLine != null)
			{
				_canvas.Children.Remove(_referenceVerticalLine);
				_referenceVerticalLine = null;
			}
			if (_referenceHorizontalLine != null)
			{
				_panels[0].Canvas.Children.Remove(_referenceHorizontalLine);
				_referenceHorizontalLine = null;
			}
		}

		void drawPredictionCursor()
		{
			var modelName = _modelNames[0];
			var model = MainView.GetModel(modelName);
			if (model != null && ShowCursor)
			{
				var scenario = model.Scenario;
				var cursorBarIndex = isTimeValid(_cursorTime) ? getTimeIndex(_cursorTime) : _currentIndex;

				var label = MainView.GetSenarioLabel(scenario);
				var text1 = label.Trim();
				var index1 = text1.IndexOf("|");

				if (index1 != -1)
				{
					var referencePrice = text1.Substring(index1 + 2, 1).Replace("O", "O").Replace("H", "H").Replace("L", "L").Replace("C", "C");
					var referenceIndex = int.Parse(text1.Substring(text1.Length - 1, 1)) - 1;
					var forecastIndex = int.Parse(text1.Substring(index1 - 2, 1));

					var barIndex1 = cursorBarIndex - referenceIndex - forecastIndex;

					lock (_barLock)
					{

						if (barIndex1 >= 0 && barIndex1 < _bars.Count)
						{
							var rp = _bars[barIndex1].Close;
							if (referencePrice == "O") rp = _bars[barIndex1].Open;
							else if (referencePrice == "H") rp = _bars[barIndex1].High;
							else if (referencePrice == "L") rp = _bars[barIndex1].Low;

							var interval = getDataInterval();
							var key = _symbol + ":" + interval;
							var bias = model.Biases.ContainsKey(key) ? model.Biases[key] : 0;
							rp += bias;

							var pixel1 = getTimePixel(barIndex1);
							if (_referenceVerticalLine == null || _referenceVerticalLine.X1 != pixel1)
							{
								if (_referenceVerticalLine != null)
								{
									_canvas.Children.Remove(_referenceVerticalLine);
									_referenceVerticalLine = null;
								}

								if (!double.IsNaN(pixel1))
								{
									_referenceVerticalLine = new Line();
									_referenceVerticalLine.X1 = pixel1;
									_referenceVerticalLine.Y1 = getPanelTop(0);
									_referenceVerticalLine.X2 = pixel1;
									_referenceVerticalLine.Y2 = getPanelBottom(getPanelCount() - 1);
									_referenceVerticalLine.Stroke = Brushes.DodgerBlue;
									_referenceVerticalLine.StrokeEndLineCap = PenLineCap.Flat;
									_referenceVerticalLine.StrokeThickness = 0.5;
									_referenceVerticalLine.ToolTip = "Reference Time";
									_canvas.Children.Add(_referenceVerticalLine);
								}
							}

							var pixel2 = getValuePixel(0, rp);
							if (_referenceHorizontalLine == null || _referenceHorizontalLine.Y1 != pixel2)
							{
								if (_referenceHorizontalLine != null)
								{
									_panels[0].Canvas.Children.Remove(_referenceHorizontalLine);
									_referenceHorizontalLine = null;
								}

								if (!double.IsNaN(pixel2))
								{
									_referenceHorizontalLine = new Line();
									_referenceHorizontalLine.X1 = getPanelLeft(0);
									_referenceHorizontalLine.Y1 = pixel2;
									_referenceHorizontalLine.X2 = getPanelRight(0);
									_referenceHorizontalLine.Y2 = pixel2;
									_referenceHorizontalLine.Stroke = Brushes.DodgerBlue;
									_referenceHorizontalLine.StrokeEndLineCap = PenLineCap.Flat;
									_referenceHorizontalLine.StrokeThickness = 0.5;
									_referenceHorizontalLine.ToolTip = "Reference Value";
									_panels[0].Canvas.Children.Add(_referenceHorizontalLine);
								}
							}
						}
					}
				}

				else
				{
					erasePredictionLines();
				}
			}
			else
			{
				erasePredictionLines();
			}
		}

		bool _enableHistoricalAdvice = true;
		DateTime _changeCursorTime = DateTime.Now;
		bool _calculateHistoricalAdvice = false;
		int _adviceBarIndex = 0;

		public void ChangeCursorTime(DateTime time, string interval, bool atCurrent)
		{
			if (isTimeValid(time))
			{
				int index = 0;
				if (atCurrent)
				{
					index = _currentIndex;
				}
				else
				{
					index = getAdjustedTimeIndex(time, interval);
				}

				DateTime cursorTime = new DateTime();
				lock (_barLock)
				{
					if (index >= 0 && index < _bars.Count)
					{
						cursorTime = _bars[index].Time;
					}
				}

				if (cursorTime != default(DateTime))
				{
					_changeCursorTime = DateTime.Now;
					_calculateHistoricalAdvice = _enableHistoricalAdvice;
					_adviceBarIndex = index;

					var oldCursorTime = _cursorTime;
					_cursorTime = cursorTime;

					if (_cursorTime != oldCursorTime)
					{
						onChartEvent(new ChartEventArgs(ChartEventType.CursorTime));
						updatePrediction();
					}

					addCursorLine();
					if (_cursorVerticalLine != null)
					{
						var x = getTimePixel(index);
						if (!double.IsNaN(x))
						{
							_cursorVerticalLine.X1 = x;
							_cursorVerticalLine.X2 = x;
						}
					}
					drawCursorInfo();
					//_cursorInfo.Visible = true;
				}
				else
				{
					_cursorTime = new DateTime();
					removeCursorLine();
					//_cursorInfo.Visible = false;
				}
			}
			else
			{
				_cursorTime = new DateTime();
				removeCursorLine();
				//_cursorInfo.Visible = false;
			}

			updateTradeEvents();
		}

		private void changeCursorTime(DateTime time)
		{
			int index = getTimeIndex(time);
			bool atCurrent = (index == _currentIndex);
			ChangeCursorTime(time, _interval, atCurrent);

			foreach (Chart chart in _linkedCharts)
			{
				chart.ChangeCursorTime(time, _interval, atCurrent);
			}
		}

		private void changeCursorValue(double value)
		{
			ChangeCursorValue(value);
			foreach (Chart chart in _linkedCharts)
			{
				chart.ChangeCursorValue(value);
			}
		}

		public List<Graph> Panels
		{
			get { return _panels; }
		}


		public bool IsVisible()
		{
			return (_canvas.Visibility == Visibility.Visible);
		}

		public void Show()
		{
			_canvas.Visibility = Visibility.Visible;
			_update = true;
		}

		public void Hide()
		{
			_canvas.Visibility = Visibility.Collapsed;
		}

		private int getPanelCount()
		{
			int count = _panels.Count;
			return count;
		}

		private double _attributionHeight = 25;

		private double getPanelWidth(int panelNumber)
		{
			double space = _canvas.ActualWidth - _valueScaleWidth;
			double width = space;
			return width;
		}

		private double getPanelHeight(int panelNumber)
		{
			double space = _canvas.ActualHeight - _timeScaleHeight - _titleHeight;
			double height = (_panels[panelNumber].Height * space) / 100;
			return height;
		}

		private double getPanelLeft(int panelNumber)
		{
			double left = 0;
			return left;
		}

		private double getPanelTop(int panelNumber)
		{
			double top = _titleHeight;
			for (int ii = 0; ii < panelNumber; ii++)
			{
				top += getPanelHeight(ii);
			}
			return top;
		}

		private double getPanelRight(int panelNumber)
		{
			return getPanelLeft(panelNumber) + getPanelWidth(panelNumber);
		}

		private double getPanelBottom(int panelNumber)
		{
			return getPanelTop(panelNumber) + getPanelHeight(panelNumber);
		}

		public string SymbolName
		{
			get { return _symbolName; }
		}

		public string SymbolGroup
		{
			get { return _symbolGroup; }
		}

		public string SymbolDescription
		{
			get { return _symbolDescription; }
		}

		public string Interval
		{
			get { return _interval; }
		}
		public string DataInterval
		{
			get { return _interval; }
		}

		private void addPanel(int index, string panelName)
		{
			if (index >= 0 && index <= _panels.Count)
			{
				_panels.Insert(index, new Graph(panelName));
			}
			else
			{
				_panels.Add(new Graph(panelName));
			}

			int count = _panels.Count;
			double subPanelHeight = Math.Max(20, 60 / count);
			double mainPanelHeight = 100 - ((count - 1) * subPanelHeight);

			_panels[0].Height = mainPanelHeight;

			for (int ii = 1; ii < count; ii++)
			{
				_panels[ii].Height = subPanelHeight;
			}

			if (index > 0)
			{
				_panels[index].Title = panelName;
			}
		}

		private void removePanel(string panelName)
		{
			int index = GetPanelIndex(panelName);
			if (index != -1)
			{
				_panels.RemoveAt(index);
			}

			int count = _panels.Count;
			if (count > 0)
			{
				double subPanelHeight = Math.Max(20, 60 / count);
				double mainPanelHeight = 100 - ((count - 1) * subPanelHeight);

				_panels[0].Height = mainPanelHeight;

				for (int ii = 1; ii < count; ii++)
				{
					_panels[ii].Height = subPanelHeight;
				}
			}
		}

		public int GetPanelIndex(string panelName)
		{
			int index = -1;
			int count = _panels.Count;
			for (int ii = 0; ii < count; ii++)
			{
				if (_panels[ii].Name == panelName)
				{
					index = ii;
					break;
				}
			}
			return index;
		}

		public double GetClose(DateTime date)
		{
			double output = double.NaN;
			int index = getTimeIndex(date);
			lock (_barLock)
			{
				if (index >= 0 && index < _bars.Count)
				{
					output = _bars[index].Close;
				}
			}
			return output;
		}
		private double GetClose(int index)
		{
			double output = double.NaN;
			lock (_bars)
			{
				if (index >= 0 && index < _bars.Count)
				{
					output = _bars[index].Close;
				}
			}
			return output;
		}

		public double GetPrice(string name, int ago, bool relativeToCursor = false)
		{
			var output = Double.NaN;
			lock (_barLock)
			{
				int currentBarIndex = _bars.Count - _extraBars - 1;

				if (relativeToCursor && isTimeValid(_cursorTime))
				{
					currentBarIndex = getTimeIndex(_cursorTime);
				}

				int index = currentBarIndex - ago;
				if (index >= 0 && index < _bars.Count)
				{
					var bar = _bars[index];
					if (name == "Open") output = bar.Open;
					else if (name == "High") output = bar.High;
					else if (name == "Low") output = bar.Low;
					else if (name == "Close") output = bar.Close;
				}
			}
			return output;
		}

		private int getTimeIndex(DateTime dateTime)
		{
			var index = 0;
			if (!isIntervalIntraday())
			{
				dateTime = new DateTime(dateTime.Year, dateTime.Month, dateTime.Day);
			}

			Bar searchBar = new Bar();
			searchBar.Time = dateTime;
			lock (_barLock)
			{
				index = _bars.BinarySearch(searchBar);
				if (index < 0 || index >= _bars.Count)
				{
					int count = _bars.Count;
					index = count - 1;
					if (isIntervalIntraday())
					{
						for (int ii = count - 1; ii >= 0; ii--)
						{
							if (_bars[ii].Time <= dateTime)
							{
								index = ii;
								break;
							}
						}
					}
					else
					{
						for (int ii = 0; ii < count; ii++)
						{
							if (_bars[ii].Time >= dateTime)
							{
								index = ii;
								break;
							}
						}
					}
				}
			}
			return index;
		}

		private double getTimePixel(int index)
		{
			return _timeBase + (index - _startIndex) * _timeScale;
		}

		private int getTimeIndex(double pixel)
		{
			return (int)((_startIndex + (pixel - _timeBase) / _timeScale) + 0.5);
		}

		private double getValuePixel(int panelNumber, double value)
		{
			return _panels[panelNumber].ValueBase - value * _panels[panelNumber].ValueScale;
		}

		private double getValue(int panelNumber, double pixel)
		{
			return (_panels[panelNumber].ValueBase - pixel) / _panels[panelNumber].ValueScale;
		}

		bool isIntervalIntraday()
		{
			return _interval != "D30" && _interval != "Daily" && _interval != "W30" && _interval != "Weekly" && _interval != "Monthly" && _interval != "Quarterly" && _interval != "SemiAnnual" && _interval != "Yearly";
		}

		bool isIntervalDaily()
		{
			int idx = _interval.Length - 1;
			return (idx >= 0 && _interval[idx] == 'D');
		}

		bool isIntervalWeekly()
		{
			int idx = _interval.Length - 1;
			return (idx >= 0 && _interval[idx] == 'W');
		}

		bool isIntervalMonthly()
		{
			int idx = _interval.Length - 1;
			return (idx >= 0 && _interval[idx] == 'M');
		}

		bool isIntervalQuarterly()
		{
			int idx = _interval.Length - 1;
			return (idx >= 0 && _interval[idx] == 'Q');
		}

		bool isIntervalHalfHourOrLess()
		{
			return (_interval[0] == 'R' || isIntervalIntraday() && int.Parse(_interval) <= 30);
		}

		private void updateTimeScale()
		{
			int barCount = _endIndex - _startIndex;
			if (barCount > 0)
			{
				double timeScale1 = _timeScale;
				for (int ii = 0; ii < 10; ii++)
				{
					//_rightMargin = double.IsNaN(_timeScale) ? 70 : (int)(_forecastCount * _timeScale);
					double width = getPanelWidth(0) - _leftMargin - _rightMargin;
					if (width <= 0)
					{
						width = 1;
					}
					double timeScale = width / barCount;
					_timeScale = width / barCount;
					int gap = (int)Math.Max((4.0 / 100.0) * _timeScale, 1.0);
					_timeBase = (_timeScale + gap) / 2.0;
					if (_timeScale == timeScale1)
					{
						break;
					}
					timeScale1 = _timeScale;
				}
			}
		}

		private void adjustValueScale(int panelNumber)
		{
			var maxHi = double.MinValue;
			var minLo = double.MaxValue;
			for (int index = _startIndex; index < _endIndex; index++)
			{
				lock (_barLock)
				{
					if (0 <= index && index < _bars.Count)
					{
						Bar bar = _bars[index];

						var offsetAbove = 0.0;
						var offsetBelow = 0.0;

						var annotations = new List<ChartAnnotation>();
						lock (_panels[panelNumber].Annotations)
						{
							if (_panels[panelNumber].Annotations.ContainsKey(bar.Time))
							{
								annotations = _panels[panelNumber].Annotations[bar.Time];
								annotations.Sort((x, y) => x.Id.CompareTo(y.Id));
							}

							foreach (ChartAnnotation annotation in annotations)
							{
								double offset = 0;
								double space = _timeScale * annotation.Offset;
								double height = 0;
								if (annotation.Placement == Placement.Above)
								{
									height = Math.Max(6, _timeScale * annotation.Height);
									offset = offsetAbove - space;
									offsetAbove += (height + space);

								}
								else if (annotation.Placement == Placement.Below)
								{
									height = Math.Max(6, _timeScale * annotation.Height);
									offset = offsetBelow + space;
									offsetBelow += (height + space);
								}
							}
						}

						var hi = bar.High;
						if (double.IsNaN(hi))
						{
							hi = bar.Close;
						}
						if (!double.IsNaN(hi))
						{
							var hiVal = hi + offsetAbove / _panels[panelNumber].ValueScale;
							maxHi = Math.Max(maxHi, hiVal);
						}

						var lo = bar.Low;
						if (double.IsNaN(lo))
						{
							lo = bar.Close;
						}
						if (!double.IsNaN(lo))
						{
							var loVal = lo - offsetAbove / _panels[panelNumber].ValueScale;
							minLo = Math.Min(minLo, loVal);
						}
					}
				}
			}

			if (maxHi > minLo)
			{
				double range = maxHi - minLo;
				double panelHeight = getPanelHeight(panelNumber);
				double margin = 0.05 * panelHeight;
				_panels[panelNumber].ValueScale = (panelHeight - (2 * margin)) / range;
				_panels[panelNumber].ValueBase = (panelHeight - margin) + minLo * _panels[0].ValueScale;
			}
		}

		private void updateValueScale()
		{
			int barCount = _endIndex - _startIndex;
			if (barCount > 0)
			{
				if (_panels[0].AutoScale)
				{
					double maximum = Double.NaN;
					double minimum = Double.NaN;
					for (int index = _startIndex; index < _endIndex; index++)
					{
						lock (_barLock)
						{
							if (0 <= index && index < _bars.Count)
							{
								Bar bar = _bars[index];

								double hi = bar.High;
								double lo = bar.Low;

								if (double.IsNaN(hi)) hi = bar.Close;
								if (double.IsNaN(lo)) lo = bar.Close;

								if (!Double.IsNaN(hi) && (Double.IsNaN(maximum) || hi > maximum))
								{
									maximum = hi;
								}
								if (!Double.IsNaN(lo) && (Double.IsNaN(minimum) || lo < minimum))
								{
									minimum = lo;
								}
							}
						}
					}

					if (double.IsNaN(maximum)) maximum = minimum;
					if (double.IsNaN(minimum)) maximum = minimum = 0;
					if (maximum == minimum)
					{
						maximum += 1;
						minimum -= 1;
					}

					double range = (!double.IsNaN(maximum) && !double.IsNaN(minimum) && maximum > minimum) ? maximum - minimum : 1;
					double panelHeight = getPanelHeight(0);
					double margin = 0.05 * panelHeight;
					_panels[0].ValueScale = (panelHeight - (2 * margin)) / range;
					_panels[0].ValueBase = (panelHeight - margin) + minimum * _panels[0].ValueScale;

					adjustValueScale(0);
				}

				int count = getPanelCount();
				for (int ii = 1; ii < count; ii++)
				{
					if (_panels[ii].AutoScale)
					{
						double maximum = Double.NaN;
						double minimum = Double.NaN;
						_panels[ii].GetMinMax(_startIndex, _endIndex, out minimum, out maximum);

						if (double.IsNaN(maximum)) maximum = minimum;
						if (double.IsNaN(minimum)) maximum = minimum = 0;
						if (maximum == minimum)
						{
							maximum += 1;
							minimum -= 1;
						}

						double range = maximum - minimum;
						double panelHeight = getPanelHeight(ii);
						double margin = 0.05 * panelHeight;
						_panels[ii].ValueScale = (panelHeight - (2 * margin)) / range;
						_panels[ii].ValueBase = (panelHeight - margin) + minimum * _panels[ii].ValueScale;
					}
				}
			}
		}

		public string getIntervalLabel(string interval)
		{
			string label = interval;
			if (interval == "1D" || interval == "Daily") label = ", D";  //Daily
			else if (interval == "1W" || interval == "Weekly") label = ", W";  //Weekly
			else if (interval == "1M" || interval == "Monthly") label = ", M";
			else if (interval == "1Q" || interval == "Quarterly") label = ", Q";
			else if (interval == "1S" || interval == "SemiAnually") label = ", S";
			else if (interval == "1Y" || interval == "Yearly") label = ", Y";
			else label = ", " + interval.ToString();  //Minute
			return label;
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

		private IList<Bar> getTestData()
		{
			IList<Bar> bars = new List<Bar>();

			Random random = new Random(0);

			double tick = 100;
			int count = 100;
			DateTime now = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, DateTime.UtcNow.Day);
			DateTime time = now - new TimeSpan(count - 1, 12, 0, 0);
			for (int ii = 0; ii < count; ii++)
			{
				Bar bar = new Bar();
				for (int jj = 0; jj < 100; jj++)
				{
					bar.Time = time;
					double number = (double)random.Next(99 * 1000, 101 * 1000) / 100000;
					tick *= number;
					if (jj == 0)
					{
						bar.Open = tick;
						bar.High = tick;
						bar.Low = tick;
						bar.Close = tick;
					}
					else
					{
						if (tick > bar.High)
						{
							bar.High = tick;
						}
						if (tick < bar.Low)
						{
							bar.Low = tick;
						}
						bar.Close = tick;
					}
				}
				bars.Add(bar);
				time += new TimeSpan(1, 0, 0, 0);
			}
			return bars;
		}

		private void drawLine(int panelNumber, List<double> values, List<int> colorIndex, List<Color> colors, double thickness, int zOrder, bool withDots, string tooltip)
		{
			if (values != null)
			{
				bool drawEverything = (!_scrollChart && !_changeTimeScale && !_changeValueScale);

				Brush defaultBrush = new SolidColorBrush(_foregroundColor);
				List<Brush> brushes = new List<Brush>();
				foreach (Color color in colors)
				{
					brushes.Add(new SolidColorBrush(color));
				}

				double radius = Math.Max(3, Math.Min(_timeScale / 4, 12));

				int pathCount = Math.Max(1, colors.Count);
				List<GeometryGroup> groups = new List<GeometryGroup>(pathCount);
				for (int ii = 0; ii < pathCount; ii++)
				{
					groups.Add(new GeometryGroup());
				}

				double x1 = double.NaN;
				double y1 = double.NaN;
				for (int index = Math.Max(0, _startIndex - 1); index < _endIndex + 1; index++)
				{
					double x2 = double.NaN;
					double y2 = double.NaN;
					double value = (index >= 0 && index < values.Count) ? values[index] : double.NaN;
					x2 = getTimePixel(index);
					y2 = double.IsNaN(value) ? double.NaN : getValuePixel(panelNumber, value);

					int pathIndex = (index < colorIndex.Count && colorIndex[index] >= 0 && colorIndex[index] < groups.Count) ? colorIndex[index] : 0;

					if (!double.IsNaN(x1) && !double.IsNaN(y1) && !double.IsNaN(x2) && !double.IsNaN(y2))
					{
						LineGeometry line1 = new LineGeometry();
						line1.StartPoint = new Point(x1, y1);
						line1.EndPoint = new Point(x2, y2);
						groups[pathIndex].Children.Add(line1);

					}
					else if (!double.IsNaN(x1) && !double.IsNaN(x2) && !double.IsNaN(y1))
					{
						LineGeometry line1 = new LineGeometry();
						line1.StartPoint = new Point(x1 - 1, y1);
						line1.EndPoint = new Point(x1, y1);
						groups[pathIndex].Children.Add(line1);
					}
					else if (!double.IsNaN(x1) && !double.IsNaN(x2) && !double.IsNaN(y2))
					{
						LineGeometry line1 = new LineGeometry();
						line1.StartPoint = new Point(x2, y2);
						line1.EndPoint = new Point(x2 + 1, y2);
						groups[pathIndex].Children.Add(line1);
					}

					if (withDots && drawEverything && !double.IsNaN(x2) && !double.IsNaN(y2))
					{
						Ellipse ellipse = new Ellipse();
						ellipse.SetValue(Canvas.LeftProperty, x2 - radius / 2);
						ellipse.SetValue(Canvas.TopProperty, y2 - radius / 2);
						ellipse.Height = radius;
						ellipse.Width = radius;
						ellipse.Stroke = defaultBrush;
						ellipse.Fill = defaultBrush;
						ellipse.StrokeEndLineCap = PenLineCap.Flat;
						ellipse.StrokeThickness = 1;
						_panels[panelNumber].Canvas.Children.Add(ellipse);  // drawLine
					}
					x1 = x2;
					y1 = y2;
				}

				for (int ii = 0; ii < pathCount; ii++)
				{
					Path path = new Path();
					Brush brush = (ii < colors.Count) ? brushes[ii] : defaultBrush;
					path.Stroke = brush;
					path.StrokeEndLineCap = PenLineCap.Flat;
					path.StrokeThickness = thickness;
					path.Fill = brush;
					path.Data = groups[ii];
					path.SetValue(Canvas.ZIndexProperty, zOrder);
					path.SnapsToDevicePixels = true;
					if (tooltip.Length > 0)
					{
						path.ToolTip = tooltip;
					}
					_panels[panelNumber].Canvas.Children.Add(path);  // drawLines
				}
			}
		}

		private void drawDashes(int panelNumber, List<double> values, List<int> colorIndex, List<Color> colors, double thickness, int zOrder, string tooltip, string name)
		{
			bool update = false;

			if (values != null)
			{
				bool drawEverything = (!_scrollChart && !_changeTimeScale && !_changeValueScale);

				Brush defaultBrush = new SolidColorBrush(_foregroundColor);
				List<Brush> brushes = new List<Brush>();
				foreach (Color color in colors)
				{
					brushes.Add(new SolidColorBrush(color));
				}

				double radius = _timeScale / 2;

				int pathCount = Math.Max(1, colors.Count);
				List<GeometryGroup> groups = new List<GeometryGroup>(pathCount);
				for (int ii = 0; ii < pathCount; ii++)
				{
					groups.Add(new GeometryGroup());
				}

				double x1 = double.NaN;
				double y1 = double.NaN;
				for (int index = Math.Max(0, _startIndex - 1); index < _endIndex + 1; index++)
				{
					double value = (index >= 0 && index < values.Count) ? values[index] : double.NaN;
					if (!double.IsNaN(value))
					{
						x1 = getTimePixel(index);
						y1 = getValuePixel(panelNumber, value);

						if (!double.IsNaN(x1) && !double.IsNaN(y1))
						{
							int pathIndex = (index < colorIndex.Count && colorIndex[index] >= 0 && colorIndex[index] < groups.Count) ? colorIndex[index] : 0;

							LineGeometry line1 = new LineGeometry();
							line1.StartPoint = new Point(x1 - radius, y1);
							line1.EndPoint = new Point(x1 + radius, y1);

							groups[pathIndex].Children.Add(line1);

						}
					}
				}

				for (int ii = 0; ii < pathCount; ii++)
				{
					Path path = new Path();
					Brush brush = (ii < colors.Count) ? brushes[ii] : defaultBrush;
					path.Stroke = brush;
					path.StrokeEndLineCap = PenLineCap.Flat;
					path.StrokeThickness = thickness;
					path.Fill = brush;
					path.Data = groups[ii];
					path.SetValue(Canvas.ZIndexProperty, zOrder);
					path.SnapsToDevicePixels = true;
					if (tooltip.Length > 0) path.ToolTip = tooltip;
					_panels[panelNumber].Canvas.Children.Add(path);  // drawLines
					update = true;
				}
			}
		}

		private void drawBars(int panelNumber)
		{
			bool drawEverything = (!_scrollChart && !_changeTimeScale && !_changeValueScale);

			Brush defaultBrush = new SolidColorBrush(_foregroundColor);

			double height = getPanelHeight(panelNumber);
			double width = getPanelWidth(panelNumber) - _leftMargin - _rightMargin;
			lock (_barLock)
			{
				if (_bars.Count > 0 && height > 0 && width > 0)
				{
					// each bar consists of three lines
					// 1) the vertical line
					// 2) the horizontal line for the open
					// 3) the horizontal line for the close

					// _timeScale is the number of pixels per bar

					// calculate the pen width
					double thickness = Math.Ceiling(Math.Max(0.5, (int)(_timeScale / 3.14)));

					int pathCount = 1;
					List<Brush> brushes = new List<Brush>();
					lock (_colorList)
					{
						foreach (Color color in _colorList)
						{
							Color color1 = color;
							if (color1 == Colors.Transparent)
							{
								color1 = _foregroundColor;
							}
							brushes.Add(new SolidColorBrush(color1));
						}

						pathCount = Math.Max(1, _colorList.Count);
					}

					List<GeometryGroup> groups = new List<GeometryGroup>(pathCount);
					//for (int ii = 0; ii < 2 * pathCount; ii++)
					for (int ii = 0; ii < pathCount; ii++)
					{
						groups.Add(new GeometryGroup());
					}

					// the right and left offsets are used for drawing the horizontal lines
					// so that they line up properly with the vertical line
					double rightOffset = (thickness / 2);
					double leftOffset = (thickness / 2);

					// the gap between the open and close horizontal lines
					// is set to 4 percent (but not less than one pixel)
					double gap = Math.Max((4.0 / 100.0) * _timeScale, 1.0);

					// calculate the first left side of the horizontal line for the open
					double to = (int)(getTimePixel(_startIndex) - (_timeScale - gap) / 2);

					// for all of the bars...
					for (int index = _startIndex; index < _endIndex; index++)
					{
						// calculate the midpoint of the vertical line
						double tt = getTimePixel(index);

						// calculate the right side of the horizontal line for the close
						double tc = (int)(tt + _timeScale / 2 - (gap - 1));

						if (index >= 0 && index < _bars.Count)
						{
							Bar bar = _bars[index];
							double op = bar.Open;
							double hi = bar.High;
							double lo = bar.Low;
							double cl = bar.Close;

							int pathIndex = 0;
							lock (_colorList)
							{
								pathIndex = (index < _colorIndex.Count && _colorIndex[index] >= 0 && _colorIndex[index] < groups.Count) ? _colorIndex[index] : 0;
							}

							if (!Double.IsNaN(cl))
							{
								if (Double.IsNaN(op)) op = cl;
								if (Double.IsNaN(hi)) hi = cl;
								if (Double.IsNaN(lo)) lo = cl;
							}

							if (!Double.IsNaN(hi) && !Double.IsNaN(lo))
							{
								// the vertical line
								double v1 = getValuePixel(panelNumber, hi);
								double v2 = getValuePixel(panelNumber, lo);
								Point p1 = new Point(tt, v1);
								Point p2 = new Point(tt, v2);

								LineGeometry line1 = new LineGeometry();
								line1.StartPoint = p1;
								line1.EndPoint = p2;
								groups[pathIndex].Children.Add(line1);

								if (drawEverything)
								{
									// the horizontal line for the open
									if (!Double.IsNaN(op))
									{
										double v = getValuePixel(panelNumber, op);
										p1 = new Point(to, v);
										p2 = new Point(tt + rightOffset, v);

										LineGeometry line2 = new LineGeometry();
										line2.StartPoint = p1;
										line2.EndPoint = p2;
										// groups[pathCount + pathIndex].Children.Add(line2);
										groups[pathIndex].Children.Add(line2);
									}

									// the horizontal line for the close
									if (!Double.IsNaN(cl))
									{
										double v = getValuePixel(panelNumber, cl);
										p1 = new Point(tt - leftOffset, v);
										p2 = new Point(tc, v);

										LineGeometry line3 = new LineGeometry();
										line3.StartPoint = p1;
										line3.EndPoint = p2;
										//groups[pathCount + pathIndex].Children.Add(line3);
										groups[pathIndex].Children.Add(line3);
									}
								}
							}

							// the next left side of the horizontal line for the open is the previous 
							// right size of the horizontal line for the close plus the gap
							to = tc + gap;
						}
					}

					//for (int type = 0; type < 2; type++)
					{
						for (int ii = 0; ii < pathCount; ii++)
						{
							Path path = new Path();
							path.SnapsToDevicePixels = true;
							Brush brush = (ii < brushes.Count) ? brushes[ii] : defaultBrush;
							path.Stroke = brush;
							path.StrokeEndLineCap = PenLineCap.Flat;
							//path.StrokeThickness = (type == 0) ? thickness : 1.618 * thickness;
							path.StrokeThickness = thickness;
							path.Fill = brush;
							//path.Data = groups[type * pathCount + ii];
							path.Data = groups[ii];
							path.SetValue(Canvas.ZIndexProperty, 10);
							_panels[panelNumber].Canvas.Children.Add(path);  // drawBars                   
						}
					}
				}
			}
		}

		private void drawBarsAsLine(int panelNumber)
		{
			Brush defaultBrush = new SolidColorBrush(_foregroundColor);

			double height = getPanelHeight(panelNumber);
			double width = getPanelWidth(panelNumber) - _leftMargin - _rightMargin;
			lock (_barLock)
			{
				if (_bars.Count > 0 && height > 0 && width > 0)
				{
					double thickness = Math.Ceiling(Math.Max(0.5, (int)(_timeScale / 3.14)));

					int pathCount = 1;
					List<Brush> brushes = new List<Brush>();
					lock (_colorList)
					{
						foreach (Color color in _colorList)
						{
							Color color1 = color;
							if (color1 == Colors.Transparent)
							{
								color1 = _foregroundColor;
							}
							brushes.Add(new SolidColorBrush(color1));
						}

						pathCount = Math.Max(1, _colorList.Count);
					}

					List<GeometryGroup> groups = new List<GeometryGroup>(pathCount);
					for (int ii = 0; ii < pathCount; ii++)
					{
						groups.Add(new GeometryGroup());
					}

					double t1 = 0;
					double cl1 = double.NaN;

					int startIndex = Math.Max(0, _startIndex - 1);
					int endIndex = Math.Min(_endIndex + 1, _bars.Count);
					for (int index = startIndex; index < endIndex; index++)
					{
						double t2 = getTimePixel(index);

						Bar bar = _bars[index];
						double cl2 = bar.Close;

						int pathIndex = 0;
						lock (_colorList)
						{
							pathIndex = (index < _colorIndex.Count && _colorIndex[index] >= 0 && _colorIndex[index] < groups.Count) ? _colorIndex[index] : 0;
						}

						if (!Double.IsNaN(cl1) && !Double.IsNaN(cl2))
						{
							double v1 = getValuePixel(panelNumber, cl1);
							double v2 = getValuePixel(panelNumber, cl2);
							Point p1 = new Point(t1, v1);
							Point p2 = new Point(t2, v2);

							LineGeometry line1 = new LineGeometry();
							line1.StartPoint = p1;
							line1.EndPoint = p2;
							groups[pathIndex].Children.Add(line1);
						}
						t1 = t2;
						cl1 = cl2;
					}

					for (int ii = 0; ii < pathCount; ii++)
					{
						Path path = new Path();
						path.SnapsToDevicePixels = true;
						Brush brush = (ii < brushes.Count) ? brushes[ii] : defaultBrush;
						path.Stroke = brush;
						path.StrokeEndLineCap = PenLineCap.Flat;
						path.StrokeThickness = thickness;
						path.Fill = brush;
						path.Data = groups[ii];
						path.SetValue(Canvas.ZIndexProperty, 10);
						_panels[panelNumber].Canvas.Children.Add(path);  // drawBarsAsLine                   
					}
				}
			}
		}

		private void SizeChanged(object sender, SizeChangedEventArgs e)
		{
			update();
		}

		void MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			try
			{
				_canvas.Focus();

				try
				{
					_canvas.CaptureMouse();
				}
				catch (Exception)
				{
				}

				_mouseMove = false;

				double interval = (_doubleClickStopwatch == null) ? 10000 : _doubleClickStopwatch.ElapsedMilliseconds;
				if (interval < 500)
				{
					double yy = e.GetPosition(_canvas).Y;
					{
						bool scroll = true;
						int panelCount = _panels.Count;
						for (int panelNumber = 0; panelNumber < panelCount; panelNumber++)
						{
							if (!_panels[panelNumber].AutoScale)
							{
								_panels[panelNumber].AutoScale = true;
								scroll = false;
							}
						}

						if (scroll)
						{
							ScrollToCurrentTime();
						}
						update();
					}
				}
				else
				{
					double xx = e.GetPosition(_canvas).X;
					double yy = e.GetPosition(_canvas).Y;

					if (yy >= _canvas.ActualHeight - _timeScaleHeight)
					{
						_changeTimeScale = true;
					}
					else if (xx >= _canvas.ActualWidth - _valueScaleWidth)
					{
						_changeValueScale = true;
						int panelCount = _panels.Count;
						for (int panelNumber = 0; panelNumber < panelCount; panelNumber++)
						{
							double y1 = getPanelTop(panelNumber);
							double y2 = getPanelBottom(panelNumber);
							if (y1 <= yy && yy < y2)
							{
								_changeValueScalePanelNumber = panelNumber;
								_changeValueScaleOrigin = _panels[panelNumber].ValueScale;
								_changeValueBaseOrigin = _panels[panelNumber].ValueBase;
								break;
							}
						}
					}
					else if (yy > _titleHeight)
					{
						_scrollChart = true;
						int panelCount = _panels.Count;
						for (int panelNumber = 0; panelNumber < panelCount; panelNumber++)
						{
							double y1 = getPanelTop(panelNumber);
							double y2 = getPanelBottom(panelNumber);
							if (y1 <= yy && yy < y2)
							{
								_changeValueScalePanelNumber = panelNumber;
								_changeValueScaleOrigin = _panels[panelNumber].ValueScale;
								_changeValueBaseOrigin = _panels[panelNumber].ValueBase;
								break;
							}
						}
					}
				}
				_scrollOrigin = e.GetPosition(_canvas);
				_doubleClickStopwatch = Stopwatch.StartNew();
			}
			catch (System.Security.SecurityException ex)
			{
				string message = ex.Message;
				// for some reason every once in a while a security exception is throw
			}
		}

		void MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			_canvas.ReleaseMouseCapture();

			bool redraw = (_mouseMove && (_scrollChart || _changeTimeScale || _changeValueScale));

			_scrollChart = false;
			_changeTimeScale = false;
			_changeValueScale = false;

			if (redraw)
			{
				update();
			}

			if (!_mouseMove)
			{
				onChartEvent(new ChartEventArgs(ChartEventType.ChangeDate));
			}
		}

		void MouseMove(object sender, MouseEventArgs e)
		{

			if (_changeTimeScale)
			{
				Point position = e.GetPosition(_canvas);
				int delta = (int)(8 * ((_scrollOrigin.X - position.X) / _timeScale));
				if (delta != 0)
				{
					_mouseMove = true;

					_scrollOrigin = position;
					changeBarSpacing(delta);
				}
			}
			else if (_changeValueScale && Math.Abs(_changeValueScaleOrigin) > 0)
			{
				Point position = e.GetPosition(_canvas);
				double delta = _scrollOrigin.Y - position.Y;
				if (delta != 0)
				{
					_mouseMove = true;

					if (0 <= _changeValueScalePanelNumber && _changeValueScalePanelNumber < _panels.Count)
					{

						_panels[_changeValueScalePanelNumber].AutoScale = false;

						double factor = 2.0;
						double scaleMultiplier = 1.0 + (((double)(delta) / getPanelHeight(_changeValueScalePanelNumber)) * factor);
						double newScale = _changeValueScaleOrigin * scaleMultiplier;
						if (newScale < 0.00001) newScale = 0.00001;
						_panels[_changeValueScalePanelNumber].ValueScale = newScale;

						double height = getPanelHeight(_changeValueScalePanelNumber);
						double margin = 0.05 * height;
						double pixel = height / 2;
						double value1 = (_changeValueBaseOrigin - pixel) / _changeValueScaleOrigin;
						double value2 = (_panels[_changeValueScalePanelNumber].ValueBase - pixel) / newScale;
						double diff = (value1 - value2) * _panels[_changeValueScalePanelNumber].ValueScale;
						_panels[_changeValueScalePanelNumber].ValueBase += diff;

						update();
					}
				}
			}
			else if (_scrollChart)
			{
				Point position = e.GetPosition(_canvas);

				int xdelta = (int)(3 * ((_scrollOrigin.X - position.X) / _timeScale));
				int ydelta = (int)(2 * (_scrollOrigin.Y - position.Y));

				if (0 <= _changeValueScalePanelNumber && _changeValueScalePanelNumber < _panels.Count)
				{

					bool autoScale = _panels[_changeValueScalePanelNumber].AutoScale;

					bool moveTime = (xdelta != 0);
					bool moveValue = Math.Abs(ydelta) > (autoScale ? 30 : 0);

					if (moveTime || moveValue)
					{
						_mouseMove = true;

						_scrollOrigin = position;

						if (moveValue)
						{
							if (autoScale) ydelta /= 10;
							_panels[_changeValueScalePanelNumber].AutoScale = false;
							_panels[_changeValueScalePanelNumber].ValueBase -= ydelta;
							update();
						}

						if (moveTime)
						{
							scrollChart(xdelta);
						}
					}
				}
			}
			else if (ShowCursor)
			{
				_mouseInfoPosition1 = e.GetPosition(_canvas);
				_mouseInfoPosition2 = e.GetPosition(null);

				int index = getTimeIndex(_mouseInfoPosition1.X);

				double cursorValue = double.NaN;

				if (_mouseInfoPosition1.Y >= getPanelTop(0) && _mouseInfoPosition1.Y <= getPanelBottom(0))
				{
					cursorValue = getValue(0, _mouseInfoPosition1.Y - getPanelTop(0));
				}
				else
				{
					cursorValue = double.NaN;
				}

				changeCursorValue(cursorValue);

				DateTime time = new DateTime();
				lock (_barLock)
				{
					if (index >= 0 && index < _bars.Count)
					{
						time = _bars[index].Time;
					}
				}

				if (time != default(DateTime))
				{
					if (index >= _startIndex && index < _endIndex)
					{
						_mouseTime = time;
					}

					if (_cursorOn)
					{
						changeCursorTime(time);
					}
				}
				else
				{
					changeCursorTime(new DateTime());
				}
			}
		}

		private void changeBarSpacing(int delta)
		{
			int startIndex = _startIndex;

			startIndex += delta;

			if (_currentIndex < _endIndex)
			{
				_endIndex = getInitialEndIndex();
			}

			var barCount = 0;
			lock (_barLock)
			{
				barCount = _bars.Count;
			}

			if (startIndex > barCount - _extraBars - 1) startIndex = barCount - _extraBars - 1;
			if (startIndex < 0) startIndex = 0;
			if (startIndex > _endIndex - 4) startIndex = _endIndex - 4;

			if (startIndex != _startIndex)
			{
				_startIndex = startIndex;
				if (_startIndex < 0) _startIndex = 0;
				update();
			}
		}
		private void scrollChart(int delta)
		{
			int startIndex = _startIndex;
			int count = _endIndex - _startIndex;

			startIndex += delta;

			var barCount = 0;
			lock (_barLock)
			{
				barCount = _bars.Count;
			}

			if (startIndex > barCount - _extraBars - 1) startIndex = barCount - _extraBars - 1;
			if (startIndex > barCount - count) startIndex = barCount - count;
			if (startIndex < 0) startIndex = 0;

			int endIndex = startIndex + count;

			if (startIndex != _startIndex)
			{
				_startIndex = startIndex;
				_endIndex = endIndex;
				update();
			}
		}

		public string getValueText(double value)
		{
			string text = "";

			if (!double.IsNaN(value))
			{
				if (_symbol == "F.USA")
				{
					int intVal = (int)value;
					double fracVal = 32 * (value - intVal);
					text = intVal.ToString("###") + fracVal.ToString("00 \u00A0");
				}
				else if (_symbol == "F.TYA")
				{
					int intVal = (int)value;
					double fracVal = 320 * (value - intVal);
					text = intVal.ToString("###") + fracVal.ToString("000");
				}
				else if (_symbol == "F.JY" || _symbol == "F.JYA")
				{
					text = value.ToString("###.0000000");
				}
				else if (_symbol == "F.BP" || _symbol == "F.BPA")
				{
					text = value.ToString("###.0000");
				}
				else if (_symbol == "F.CA" || _symbol == "F.CAA" || _symbol == "F.DA" || _symbol == "F.DAA" || _symbol == "F.EU" || _symbol == "F.EUA" || _symbol == "F.SF" || _symbol == "F.SFA")
				{
					text = value.ToString("###.00000");
				}
				else if (_symbol.Contains(" Curncy") && _symbol[0] != 'X')
				{
					text = value.ToString("###.0000");
				}
				else
				{
					text = value.ToString("###.00");
				}
			}
			return text;
		}

		private double[] getOpenTradePercentage(DateTime endTime)
		{
			var output = double.NaN;
			var tickerPercentChange = double.NaN;

			string interval = getIntervalAbbreviation(_interval);

			string[] tickers = _symbol.Split(ATMML.Symbol.SpreadCharacter);

			List<Trade> trades = _portfolio.GetTrades(_portfolioName, tickers[0]);
			if (trades.Count == 0)
			{
				TradeHorizon horizon = Trade.Manager.GetIntervalTradeHorizon(interval);
				trades = Trade.Manager.getTradesForSymbol(_portfolioName, horizon, _symbol);
			}

			var openTrades = trades.Where(x => x.IsOpen()).ToList();

			if (openTrades.Count > 0)
			{
				var trade = openTrades.Last();

				if (endTime >= trade.OpenDateTime)
				{

					List<Bar> symbolBars = _barCache.GetBars(_symbol, getDataInterval(), 0, 200);
					List<Bar> indexBars = _barCache.GetBars("SPX Index", getDataInterval(), 0, 200);

					int barCount = symbolBars.Count;

					if (barCount > 0)
					{
						int index2 = symbolBars.Count - 1;
						if (endTime != default(DateTime))
						{
							for (int idx = 0; idx < barCount; idx++)
							{
								if (symbolBars[idx].Time >= endTime)
								{
									index2 = idx;
									break;
								}
							}
						}

						var startTime = trade.OpenDateTime;
						int index1 = symbolBars.Count - 1;
						if (startTime != default(DateTime))
						{
							for (int idx = 0; idx < barCount; idx++)
							{
								if (symbolBars[idx].Time >= startTime)
								{
									index1 = idx;
									break;
								}
							}
						}
						double openPrice = symbolBars[index1].Close;
						double closePrice = symbolBars[index2].Close;

						var direction = Math.Sign(trade.Direction);

						var indexPercentChange = 0.0;
						if (index1 < indexBars.Count && index2 < indexBars.Count)
						{
							double openIndexPrice = indexBars[index1].Close;
							double closeIndexPrice = indexBars[index2].Close;
							indexPercentChange = direction * 100 * (closeIndexPrice - openIndexPrice) / openIndexPrice;
						}

						tickerPercentChange = direction * 100 * (closePrice - openPrice) / openPrice;
						output = tickerPercentChange - indexPercentChange;
					}
				}
			}

			return new double[] { tickerPercentChange, output };
		}

		TextBlock _highLabel = null;
		TextBlock _lowLabel = null;
		TextBlock _closeLabel = null;
		bool _drawingClose = false;

		private void drawClose()
		{
			lock (_barLock)
			{

				_drawingClose = true;
				var cbi = _bars.Count - _extraBars - 1;

				if (cbi >= 0)
				{
					if (_aod != null)
					{

						var b1 = new Bar[] { (cbi - 1 >= 0) ? _bars[cbi - 1] : null, (cbi >= 0) ? _bars[cbi] : null };
						string interval1 = GetOverviewInterval(_interval, 1);
						var b2 = _barCache.GetBars(_symbol, interval1, 0, 2).ToArray();
						_aod.updatePrices(b1, b2);
					}

					if (_closeLabel != null)
					{
						var time = isTimeValid(_cursorTime) ? _cursorTime : _bars[cbi].Time;

						int index = isTimeValid(time) ? getTimeIndex(time) : cbi;

						if (index == cbi)
						{
							if (index >= 0)
							{
								var bar0 = _bars[index];
								var high = bar0.High;
								var low = bar0.Low;
								_highLabel.Text = getValueText(high);
								_lowLabel.Text = getValueText(low);

								var close0 = bar0.Close;

								Brush brush = Brushes.Silver;
								if (index >= 1)
								{
									var bar1 = _bars[index - 1];
									var close1 = bar1.Close;
									if (close1 > close0)
									{
										brush = Brushes.Red;
									}
									else if (close1 < close0)
									{
										brush = Brushes.Lime;
									}
								}

								_closeLabel.Text = getValueText(close0);
								_closeLabel.Foreground = brush;
							}
						}
					}
				}
				_drawingClose = false;
			}
		}

		//private void drawAttribute()
		//{
		//	if (_forPrint)
		//	{
		//		var panel = new Grid();
		//		panel.Width = _canvas.ActualWidth;
		//		var r1 = new RowDefinition();
		//		//r1.Width = new GridLength(80, GridUnitType.Star);
		//		var r2 = new RowDefinition();
		//		//r2.Width = new GridLength(20, GridUnitType.Star);
		//		panel.RowDefinitions.Add(r1);
		//		panel.RowDefinitions.Add(r2);
		//		Canvas.SetLeft(panel, 0);
		//		Canvas.SetTop(panel, _canvas.ActualHeight - _attributionHeight + 2);

		//		var tb1 = new TextBlock();
		//		Grid.SetRow(tb1, 1);
		//		tb1.FontSize = 7;
		//		tb1.Margin = new Thickness(0, 0, 2, 0);
		//		tb1.Text = @"ATM EW Charts are created using Bloomberg Data. Per the terms of the Bloomberg License agreement, access is restricted to the authorized Bloomberg Terminal Service users, and redistribution is limited.";
		//		tb1.TextWrapping = TextWrapping.Wrap;
		//		tb1.TextAlignment = TextAlignment.Right;
		//		panel.Children.Add(tb1);

		//		var tb2 = new TextBlock();
		//		Grid.SetRow(tb2, 0);
		//		tb2.FontSize = 7;
		//		tb2.Margin = new Thickness(0, 0, 2, 0);
		//		tb2.Text = @"Data Source: Bloomberg Finance L.P.";
		//		tb2.HorizontalAlignment = HorizontalAlignment.Right;
		//		tb2.TextAlignment = TextAlignment.Right;
		//		panel.Children.Add(tb2);
		//		_canvas.Children.Add(panel);
		//	}
		//}

		private void drawCopyright()
		{
			var tb = new TextBlock();
			tb.Foreground = Brushes.Black;
			tb.Background = Brushes.Transparent;
			tb.FontFamily = new FontFamily("Arial");
			tb.FontSize = 9;
			tb.Text = "\u00A0\u00A9 ATM ML";
			tb.SetValue(Canvas.LeftProperty, 3.0);
			tb.SetValue(Canvas.TopProperty, 5.0);
			_canvas.Children.Add(tb);
		}

		private void drawCursorInfo(bool forPrint = false)
		{
			MouseInfo mouseInfo = _cursorInfo;

			_closeLabel = null;

			lock (_barLock)
			{

				if (mouseInfo != null && /* mouseInfo.Visible && */ _bars.Count > 0)
				{
					Point position1 = _mouseInfoPosition1;
					Point position2 = _mouseInfoPosition2;

					Brush labelDefaultForegroundBrush = forPrint ? Brushes.Black : Brushes.Silver;
					Brush labelOrderBrush = Brushes.White;
					Brush labelPositionBrush = Brushes.White;

					var cbi = _bars.Count - _extraBars - 1;

					var highLabelIndex = 0;

					if (cbi >= 0)
					{
						var time = isTimeValid(_cursorTime) ? _cursorTime : _bars[cbi].Time;

						int index = isTimeValid(time) ? getTimeIndex(time) : cbi;

						if (_startIndex <= index && index < _endIndex)
						{
							if (0 <= index && index < _bars.Count)
							{
								double y1 = getPanelTop(0);

								double hiPix = y1 + getValuePixel(0, _bars[index].High);
								double loPix = y1 + getValuePixel(0, _bars[index].Low);

								mouseInfo.Clear();

								mouseInfo.SetHeader(_bars[index].GetTimeLabel(isIntervalIntraday()));

								List<string> labels = new List<string>();
								List<string> values = new List<string>();
								List<Brush> labelBrushes = new List<Brush>();
								List<Brush> valueBrushes = new List<Brush>();

								if (!double.IsNaN(_bars[index].Open))
								{
									labels.Add("\u00A0Op ");
									Brush brush = labelDefaultForegroundBrush;
									double currentBarOpen = _bars[index].Open;
									if (index - 1 >= 0 && index - 1 < _bars.Count - 1)
									{
										double previousBarClose = _bars[index - 1].Close;
										if (previousBarClose > currentBarOpen)
										{
											brush = Brushes.Red;
										}
										else if (previousBarClose < currentBarOpen)
										{
											brush = Brushes.Lime;
										}
									}
									labelBrushes.Add(labelDefaultForegroundBrush);
									valueBrushes.Add(brush);
									values.Add(getValueText(currentBarOpen));
								}

								if (!double.IsNaN(_bars[index].High))
								{
									labels.Add("\u00A0Hi ");
									valueBrushes.Add(labelDefaultForegroundBrush);
									labelBrushes.Add(labelDefaultForegroundBrush);
									highLabelIndex = values.Count;
									values.Add(getValueText(_bars[index].High));
								}

								if (!double.IsNaN(_bars[index].Low))
								{
									labels.Add("\u00A0Lo ");
									labelBrushes.Add(labelDefaultForegroundBrush);
									valueBrushes.Add(labelDefaultForegroundBrush);
									values.Add(getValueText(_bars[index].Low));
								}

								if (!double.IsNaN(_bars[index].Close))
								{
									labels.Add("\u00A0Cl ");
									Brush brush = labelDefaultForegroundBrush;
									double currentBarClose = _bars[index].Close;
									if (index - 1 >= 0 && index - 1 < _bars.Count - 1)
									{
										double previousBarClose = _bars[index - 1].Close;
										if (previousBarClose > currentBarClose)
										{
											brush = Brushes.Red;
										}
										else if (previousBarClose < currentBarClose)
										{
											brush = Brushes.Lime;
										}
									}
									labelBrushes.Add(labelDefaultForegroundBrush);
									valueBrushes.Add(brush);
									values.Add(getValueText(currentBarClose));
								}


								var shares = getUnits(time, false);
								if (!double.IsNaN(shares))
								{
									labels.Add("\u00A0B|S");
									Brush brush = labelOrderBrush;
									labelBrushes.Add(labelDefaultForegroundBrush);
									valueBrushes.Add(shares > 0 ? Brushes.Lime : shares < 0 ? Brushes.Red : Brushes.White);
									var text = shares.ToString("#");
									values.Add(text);
								}

								var units = getUnits(time, true);
								if (!double.IsNaN(units))
								{
									labels.Add("\u00A0Tot");
									Brush brush = labelPositionBrush;
									labelBrushes.Add(labelDefaultForegroundBrush);
									valueBrushes.Add(units > 0 ? Brushes.Lime : units < 0 ? Brushes.Red : Brushes.White);
									var text = units.ToString("#");
									values.Add(text);
								}
								var pctProfit = getPositionPctProfit(time);
								if (!double.IsNaN(pctProfit))
								{
									labels.Add("\u00A0P%");  //PL of the position
									Brush brush = labelPositionBrush;
									labelBrushes.Add(labelDefaultForegroundBrush);
									valueBrushes.Add(pctProfit > 0 ? Brushes.Lime : pctProfit < 0 ? Brushes.Red : Brushes.White);
									var text = pctProfit.ToString("0.00") + "%";
									values.Add(text);
								}
								var pctPortfolio = getPositionPctOfPortfolio(time);
								if (!double.IsNaN(pctPortfolio))
								{
									labels.Add("\u00A0%P"); //Position Size
									Brush brush = labelPositionBrush;
									labelBrushes.Add(labelDefaultForegroundBrush);
									valueBrushes.Add(pctPortfolio > 0 ? Brushes.Lime : pctPortfolio < 0 ? Brushes.Red : Brushes.White);
									var text = pctPortfolio.ToString("0.00") + "%";
									values.Add(text);
								}

								//var CampPercent = getUnits(time);
								//if (!double.IsNaN(shares))
								//{
								//	labels.Add("\u00A0C%");
								//	Brush brush = labelDefaultForegroundBrush;
								//	labelBrushes.Add(labelDefaultForegroundBrush);
								//	valueBrushes.Add(brush);
								//	var text = shares.ToString("#");
								//	values.Add(text);
								//}

								//var OverallPercent = getUnits(time);
								//if (!double.IsNaN(units))
								//{
								//	labels.Add("\u00A0O%");
								//	Brush brush = labelDefaultForegroundBrush;
								//	labelBrushes.Add(labelDefaultForegroundBrush);
								//	valueBrushes.Add(brush);
								//	var text = units.ToString("#");
								//	values.Add(text);
								//}
								//if (!double.IsNaN(_bars[index].Close) && _bars.Count > 1)
								//{
								//    labels.Add("C|C ");
								//    Brush brush = labelDefaultForegroundBrush;
								//    double ClosetoClose = double.NaN;
								//    if (index - 1 > 0 && index - 1 < _bars.Count - 1)
								//    {
								//        double currentBarClose = _bars[index].Close;
								//        double previousBarClose = _bars[index - 1].Close;
								//        ClosetoClose = currentBarClose - previousBarClose; // 100 * (currentBarClose - previousBarClose) / previousBarClose;

								//        if (previousBarClose > currentBarClose)
								//        {
								//            brush = Brushes.Red;
								//        }
								//        else if (previousBarClose < currentBarClose)
								//        {
								//            brush = Brushes.Lime;
								//        }
								//    }

								//    labelBrushes.Add(labelDefaultForegroundBrush);
								//    valueBrushes.Add(brush);
								//    values.Add(getValueText(ClosetoClose));
								//}

								//relationship Current Cl to the Rel Index
								//if (!double.IsNaN(_bars[index].Close))
								//{
								//    var idxBars = _barCache.GetBars(_indexSymbol, getDataInterval(), _extraBars, _barCount);

								//    labels.Add("C|IDX ");
								//    Brush brush = labelDefaultForegroundBrush;

								//    double pc = double.NaN;
								//    if (index > 0 && index < idxBars.Count)
								//    {
								//        double sc0 = _bars[index].Close;
								//        double sc1 = _bars[index - 1].Close;
								//        double symPC = 100 * (sc0 - sc1) / sc1;

								//        double ic0 = idxBars[index].Close;
								//        double ic1 = idxBars[index - 1].Close;
								//        double idxPC = 100 * (ic0 - ic1) / ic1;

								//        pc = symPC - idxPC;

								//        if (pc < 0)
								//        {
								//            brush = Brushes.Red;
								//        }
								//        else if (pc >= 0)
								//        {
								//            brush = Brushes.Lime;
								//        }
								//    }

								//    labelBrushes.Add(labelDefaultForegroundBrush);
								//    valueBrushes.Add(brush);
								//    values.Add(getValueText(pc));
								//}

								//relationship Current Cl to the Sector
								//if (!double.IsNaN(_bars[index].Close))
								//{
								//    var idxBars = _barCache.GetBars(_sectorSymbol, getDataInterval(), _extraBars, _barCount);

								//    labels.Add("C|SEC ");
								//    Brush brush = labelDefaultForegroundBrush;

								//    double pc = double.NaN;

								//    if (index > 0 && index < idxBars.Count)
								//    {
								//        double sc0 = _bars[index].Close;
								//        double sc1 = _bars[index - 1].Close;
								//        double symPC = 100 * (sc0 - sc1) / sc1;

								//        double ic0 = idxBars[index].Close;
								//        double ic1 = idxBars[index - 1].Close;
								//        double idxPC = 100 * (ic0 - ic1) / ic1;

								//        pc = symPC - idxPC;

								//        if (pc < 0)
								//        {
								//            brush = Brushes.Red;
								//        }
								//        else if (pc >= 0)
								//        {
								//            brush = Brushes.Lime;
								//        }
								//    }

								//    labelBrushes.Add(labelDefaultForegroundBrush);
								//    valueBrushes.Add(brush);
								//    values.Add(getValueText(pc));
								//}

								if (_scores != null && !double.IsNaN(_scores[index]))
								{
									labels.Add("\u00A0Sc ");
									labelBrushes.Add(labelDefaultForegroundBrush);
									valueBrushes.Add(forPrint ? Brushes.Black : Brushes.Silver);
									values.Add(_scores[index].ToString("##.00"));
								}

								string[] curveLabels = { "Ax", "FT", "ST", "Sc", "Td", "Tu", "S1", "S2", "S3", "T1", "T2", "T3", "T4", "T5" };

								foreach (string curveLabel in curveLabels)
								{
									int panelCount = _panels.Count;
									for (int panelNumber = 0; panelNumber < panelCount; panelNumber++)
									{
										List<string> names = _panels[panelNumber].GetCurveNames();

										foreach (string name in names)
										{
											string label = _panels[panelNumber].GetCurveLegend(name);
											if (label.Length > 0 && label == curveLabel)
											{
												List<double> curveValues = _panels[panelNumber].GetCurveValues1(name);
												if (0 <= index && index < curveValues.Count)
												{
													double value = curveValues[index];
													if (!double.IsNaN(value))
													{

														string text = getValueText(value);

														Brush labelBrush = labelDefaultForegroundBrush;
														labels.Add("\u00A0" + label);

														List<int> colorIndices = _panels[panelNumber].GetCurveColorIndexes(name);
														List<Color> colors = _panels[panelNumber].GetCurveColors(name);
														Color legendColor = _panels[panelNumber].GetLegendColor(name);

														Color color = Colors.Lime;
														if (legendColor != Colors.Transparent)
														{
															color = legendColor;
														}
														else
														{
															int colorIndex = 0;
															if (colorIndices != null && index >= 0 && index < colorIndices.Count)
															{
																colorIndex = colorIndices[index];
																if (colorIndex >= 0 && colorIndex < colors.Count)
																{
																	color = colors[colorIndex];
																}
															}
															else if (colors.Count > 0)
															{
																color = colors[0];
															}

															if (label == "FT" || label == "ST")
															{
																double cl = _bars[index].Close;
																bool up = (name.IndexOf("Dn") == -1);
																if ((up && cl > value) || (!up && cl < value))
																{
																	labelBrush = new SolidColorBrush(color);
																}
															}
														}

														labelBrushes.Add(labelBrush);
														valueBrushes.Add(new SolidColorBrush(color));
														values.Add(text);
													}
												}
											}
										}
									}
								}

								int count = values.Count;
								bool truncate = true;
								for (int ii = 0; ii < count; ii++)
								{
									string value = values[ii];
									int length = value.Length;
									if (!(length >= 4 && value[length - 4] == '.' && value[length - 1] == '0'))
									{
										truncate = false;
										break;
									}
								}

								if (truncate)
								{
									for (int ii = 0; ii < count; ii++)
									{
										values[ii] = values[ii].Substring(0, values[ii].Length - 1);
									}
								}

								for (int ii = 0; ii < count; ii++)
								{
									if (labels[ii].Length > 0 && values[ii].Length > 0)
									{
										mouseInfo.AddLabel(labels[ii], labelBrushes[ii]);
										var tb = mouseInfo.AddValue(values[ii], valueBrushes[ii]);
										if (ii == highLabelIndex) _highLabel = tb;
										else if (ii == highLabelIndex + 1) _lowLabel = tb;
										else if (ii == highLabelIndex + 2) _closeLabel = tb;
									}
								}
							}
						}
					}
				}
			}
		}

		void draw(bool forPrint = false)
		{
			if (_canvas.Visibility == Visibility.Visible)
			{
				_canvas.Children.Clear();
				_referenceVerticalLine = null;
				_referenceHorizontalLine = null;

				drawBackground();

				drawCopyright();

				IList<TimeScaleInfo> timeScaleInfo = getTimeScaleInfo();
				drawTimeScale(timeScaleInfo);

				int panelCount = _panels.Count;
				for (int panelNumber = 0; panelNumber < panelCount; panelNumber++)
				{
					IList<ValueScaleInfo> valueScaleInfo = getValueScaleInfo(panelNumber);
					drawValueScale(panelNumber, valueScaleInfo);
					drawGrid(panelNumber, valueScaleInfo, timeScaleInfo);

					if (panelNumber == 0)
					{
						bool spread = _symbol.Contains(ATMML.Symbol.SpreadCharacter);
						if (spread || _noHiLos) drawBarsAsLine(0);
						else drawBars(0);

						drawChartCurves(0, forPrint);
						drawChartLines(0);
						drawChartBezierCurves(0);
						drawChartBezierShading(0);
						drawAnnotations(panelNumber);
						drawChartTitle(0, forPrint);
					}
					else
					{
						drawChartLines(panelNumber);
						drawChartCurves(panelNumber, forPrint);
						drawAnnotations(panelNumber);
						drawChartTitle(panelNumber, forPrint);
					}
				}

				drawTitle(forPrint);
				drawSubPanelBorders();
				drawBorder();

				drawCursor();
				drawCursorInfo(forPrint);
				drawInfo(forPrint);

				bool bestTargetHi = getIndicatorEnable("Highest Analysts Target Px");
				if (bestTargetHi)
				{
					drawBestTargetHi();
				}

				bool bestTargetPrice = getIndicatorEnable("Avg Analysts Target Px");
				if (bestTargetPrice)
				{
					drawBestTargetPrice();
				}

				bool bestTargetLo = getIndicatorEnable("Lowest Analysts Target Px");
				if (bestTargetLo)
				{
					drawBestTargetLo();
				}

				drawEarningsDates();

				//drawPredictionCursor();

				drawControlBoxes(forPrint);
				drawReviewAction();

				drawPR();

				if (timeScaleInfo.Count == 0)
				{
					drawBusyIndicator();
				}
				else
				{
					var cnt = timeScaleInfo.Count;
				}

				draws();

				drawMatrix();

				drawAOD();
			}
		}

		public List<Tuple<String, Color>> GetAdvice(string symbol, string interval)
		{
			List<Tuple<String, Color>> output = null;
			var indexOk = (!_enableHistoricalAdvice || _adviceBarIndex == _currentIndex);
			var symbolOk = symbol == _symbol;
			var intervalOk = interval == DataInterval;
			if (indexOk && symbolOk && intervalOk)
			{
				lock (_studyInfoDataLock)
				{
					if (_studyInfoData != null)
					{
						output = new List<Tuple<string, Color>>();
						_studyInfoData.ForEach(x => output.Add(x));
					}
				}
			}
			return output;
		}

		private void drawInfo(bool forPrint = false)
		{
			//var cbi = _bars.Count - _extraBars - 1;
			//var time = (isTimeValid(_cursorTime) || cbi < 0) ? _cursorTime : _bars[cbi].Time;
			//int index = isTimeValid(time) ? getTimeIndex(time) : cbi;
			//var sid = (index == cbi) ? _studyInfoData : _studyInfoDatas.ContainsKey(index) ? _studyInfoDatas[index] : null;

			var sid = new List<Tuple<string, Color>>();
			lock (_studyInfoDataLock)
			{
				if (_studyInfoData != null)
				{
					_studyInfoData.ForEach(x => sid.Add(x));
				}
			}

			var grid = _cursorInfo.GetGrid();

			grid.SetValue(Canvas.LeftProperty, 1.0);
			grid.SetValue(Canvas.TopProperty, _titleHeight + 1.0);

			var foreground = forPrint ? Brushes.Black : Brushes.Silver;
			var background = forPrint ? Brushes.White : new SolidColorBrush(Color.FromArgb(0xff, 0x00, 0x00, 0x00));  //b3 75%
			_cursorInfo.SetForeground(foreground);
			_cursorInfo.SetBackground(background);

			Grid adviceGrid = _cursorInfo.GetAdviceGrid();
			if (sid.Count > 0)
			{
				adviceGrid.Visibility = Visibility.Collapsed;
				//adviceGrid.Visibility = Visibility.Visible;
				//bool scAdd = getIndicatorEnable("Current Research");
				if (/*scAdd && */sid.Count >= 5)
				{
					var sp0 = new StackPanel();
					sp0.Orientation = Orientation.Vertical;

					var sp1 = new StackPanel();
					sp1.Orientation = Orientation.Horizontal;
					var tb1a = new TextBlock();
					tb1a.Text = "\u00A0Trend:\u00A0\u00A0\u00A0\u00A0\u00A0";
					tb1a.FontSize = 9;
					tb1a.Foreground = forPrint ? Brushes.Black : Brushes.Silver;
					tb1a.Background = background;
					tb1a.Margin = new Thickness(0, 5, 0, 0);
					sp1.Children.Add(tb1a);
					var tb1b = new TextBlock();
					tb1b.Text = sid[0].Item1;
					tb1b.FontSize = 9;
					tb1b.Foreground = forPrint ? Brushes.Black : new SolidColorBrush(sid[0].Item2);
					tb1b.Background = background;
					tb1b.Margin = new Thickness(0, 5, 0, 0);
					sp1.Children.Add(tb1b);
					var tb1c = new TextBlock();
					tb1c.Text = sid[27].Item1;
					tb1c.FontSize = 9;
					tb1c.Foreground = forPrint ? Brushes.Black : new SolidColorBrush(sid[27].Item2);
					tb1c.Background = background;
					tb1c.Margin = new Thickness(0, 5, 0, 0);
					sp1.Children.Add(tb1c);
					sp0.Children.Add(sp1);

					var sp2 = new StackPanel();
					sp2.Orientation = Orientation.Horizontal;
					var tb2a = new TextBlock();
					tb2a.Text = "\u00A0Position:\u00A0";
					tb2a.FontSize = 9;
					tb2a.Foreground = forPrint ? Brushes.Black : Brushes.Silver;
					tb2a.Background = background;
					tb2a.Margin = new Thickness(0, 0, 0, 0);
					sp2.Children.Add(tb2a);
					var tb2b = new TextBlock();
					tb2b.Text = sid[1].Item1;
					tb2b.FontSize = 9;
					tb2b.Foreground = forPrint ? Brushes.Black : new SolidColorBrush(sid[1].Item2);
					tb2b.Background = background;
					tb2b.Margin = new Thickness(0, 0, 0, 0);
					sp2.Children.Add(tb2b);
					var tb2c = new TextBlock();
					tb2c.Text = sid[4].Item1;
					tb2c.FontSize = 9;
					tb2c.Foreground = forPrint ? Brushes.Black : new SolidColorBrush(sid[4].Item2);
					tb2c.Background = background;
					tb2c.Margin = new Thickness(0, 0, 0, 0);
					sp2.Children.Add(tb2c);
					sp0.Children.Add(sp2);

					var sp3 = new StackPanel();
					sp3.Orientation = Orientation.Horizontal;
					var tb3a = new TextBlock();
					tb3a.Text = "\u00A0Cur Bar:\u00A0\u00A0";
					tb3a.FontSize = 9;
					tb3a.Foreground = forPrint ? Brushes.Black : Brushes.Silver;
					tb3a.Background = background;
					tb3a.Margin = new Thickness(0, 0, 0, 0);
					sp3.Children.Add(tb3a);
					var tb3b = new TextBlock();
					tb3b.Text = sid[2].Item1;
					tb3b.FontSize = 9;
					tb3b.Foreground = forPrint ? Brushes.Black : new SolidColorBrush(sid[2].Item2);
					tb3b.Background = background;
					tb3b.Margin = new Thickness(0, 0, 0, 0);
					sp3.Children.Add(tb3b);
					sp0.Children.Add(sp3);

					var sp4 = new StackPanel();
					sp4.Orientation = Orientation.Horizontal;
					var tb4a = new TextBlock();
					tb4a.Text = "\u00A0Nxt Bar:\u00A0\u00A0";
					tb4a.FontSize = 9;
					tb4a.Foreground = forPrint ? Brushes.Black : Brushes.Silver;
					tb4a.Background = background;
					tb4a.Margin = new Thickness(0, 0, 0, 4);
					sp4.Children.Add(tb4a);
					var tb4b = new TextBlock();
					tb4b.Text = sid[3].Item1;
					tb4b.FontSize = 9;
					tb4b.Foreground = forPrint ? Brushes.Black : new SolidColorBrush(sid[3].Item2);
					tb4b.Background = background;
					tb4b.Margin = new Thickness(0, 0, 0, 4);
					sp4.Children.Add(tb4b);
					sp0.Children.Add(sp4);

					adviceGrid.Children.Clear();
					adviceGrid.Children.Add(sp0);
				}
			}
			else
			{
				adviceGrid.Visibility = Visibility.Collapsed;
			}


			//var names = new string[] { "New Trend", "Pressure", "Add", "2 Bar", "Exh", "Retrace" };
			//var enable = names.Aggregate(false, (o, n) => o || _indicators[n].Enabled);

			//Grid legendGrid = _cursorInfo.GetLegendGrid();
			//legendGrid.Children.Clear();
			//legendGrid.Margin = new Thickness(0,0,0,4);
			//legendGrid.RowDefinitions.Clear();
			//if (enable) { 
			//    legendGrid.Visibility = Visibility.Visible;
			//    var sp0 = new StackPanel();
			//    sp0.Orientation = Orientation.Vertical;

			//    var row = 0;
			//    foreach (var name in names) 
			//    {
			//        if (_indicators[name].Enabled)
			//        {
			//            var rowDef = new RowDefinition();
			//            legendGrid.RowDefinitions.Add(rowDef);

			//            var upSym = (name == "Retrace") ? "\u25D2" : (name == "Exh") ? "\u25A0" : "\u2B24";
			//            var dnSym = (name == "Retrace") ? "\u25D3" : (name == "Exh") ? "\u25A0" : "\u2B24";
			//            var upBrush = new SolidColorBrush(getSCColor(name, true));
			//            var dnBrush = new SolidColorBrush(getSCColor(name, false));

			//            var tb1a = new TextBlock();
			//            tb1a.Text = "\u00A0" + name;
			//            tb1a.FontSize = 9;
			//            tb1a.Foreground = Brushes.Silver;
			//            tb1a.Background = background;
			//            tb1a.Margin = new Thickness(0, 0, 0, 0);
			//            tb1a.Padding = new Thickness(0);
			//            Grid.SetRow(tb1a, row);
			//            Grid.SetColumn(tb1a, 0);

			//            var tb1b = new TextBlock();
			//            tb1b.Text = "\u00A0" + upSym;
			//            tb1b.FontSize = 9;
			//            tb1b.Foreground = upBrush;
			//            tb1b.Background = background;
			//            tb1b.Margin = new Thickness(0, 0, 0, 0);
			//            tb1b.Padding = new Thickness(0);
			//            Grid.SetRow(tb1b, row);
			//            Grid.SetColumn(tb1b, 1);

			//            var tb1c = new TextBlock();
			//            tb1c.Text = "\u00A0" + dnSym;
			//            tb1c.FontSize = 9;
			//            tb1c.Foreground = dnBrush;
			//            tb1c.Background = background;
			//            tb1c.Margin = new Thickness(0, 0, 0, 0);
			//            tb1c.Padding = new Thickness(0);
			//            Grid.SetRow(tb1c, row);
			//            Grid.SetColumn(tb1c, 2);

			//            legendGrid.Children.Add(tb1a);
			//            legendGrid.Children.Add(tb1b);
			//            legendGrid.Children.Add(tb1c);

			//            row++;
			//        }
			//    }
			//}

			_canvas.Children.Add(grid);
		}

		public Color getSCColor(string name, bool up)
		{
			return (_indicators[name].Parameters[up ? "Up" : "Dn"] as ColorParameter).Value;
		}


		private bool isChartWideEnoughForAOD()
		{
			int width = (int)_canvas.ActualWidth;
			return width > 350;
		}

		private void drawAOD()
		{
			var wideEnough = isChartWideEnoughForAOD();
			if (_analysisLabel != null)
			{
				//_analysisLabel.Content = wideEnough ? "MATRIX" : "MATRIX";
			}

			if (AnalysisEnable)
			{
				if (_aodVisible)
				{
					_canvas.Children.Remove(_aod);
					_canvas.Children.Add(_aod);
					Canvas.SetZIndex(_aod, 2000);
					Canvas.SetTop(_aod, 18);  //17  90
					Canvas.SetLeft(_aod, 1);  //80  
					_aod.Visibility = Visibility.Visible;
					_aod.Draw();
				}
			}
		}

		private void drawMatrix()
		{
			bool pxfSigHistory = getIndicatorEnable("Matrix");
			if (pxfSigHistory && _modelNames.Count > 0 && !isRegressionModel())
			{

				_canvas.Children.Add(_matrix);

				Canvas.SetZIndex(_matrix, 1000);
				Canvas.SetTop(_matrix, 15);  //17  90
				Canvas.SetLeft(_matrix, 80);  //80  2
				_matrix.Visibility = Visibility.Visible;

				var modelName = _modelNames[0];
				var predictions = _mpst.getPredictions(_symbol, getDataInterval(), modelName);
				var actuals = _mpst.getActuals(_symbol, getDataInterval(), modelName);

				List<DateTime> times = _barCache.GetTimes(_symbol, getDataInterval(), 0, _barCount);

				var falsePositive = 0;
				var falseNegative = 0;
				var truePositive = 0;
				var trueNegative = 0;

				for (int ii = 0; ii < times.Count; ii++)
				{
					var prediction = double.NaN;
					var actual = double.NaN;
					if (predictions.ContainsKey(times[ii]))
					{
						prediction = predictions[times[ii]];
					}
					if (actuals.ContainsKey(times[ii]))
					{
						actual = actuals[times[ii]];
					}

					if (!double.IsNaN(prediction) && !double.IsNaN(actual))
					{
						if (actual > 0.5 && prediction > 0.5) truePositive++;
						if (actual < 0.5 && prediction < 0.5) trueNegative++;
						if (actual < 0.5 && prediction > 0.5) falsePositive++;
						if (actual > 0.5 && prediction < 0.5) falseNegative++;
					}
				}

				int total = truePositive + falsePositive + falseNegative + trueNegative;

				var model = MainView.GetModel(modelName);
				var scenarioLabel = (model != null) ? MainView.GetSenarioLabel(model.Scenario) : "";

				_matrix.setScenario(scenarioLabel);

				_matrix.setTestRange(total);

				_matrix.setTruePositive(truePositive);
				_matrix.setFalsePositive(falsePositive);
				_matrix.setFalseNegative(falseNegative);
				_matrix.setTrueNegative(trueNegative);

				_matrix.setTotalPositive(truePositive + falsePositive);
				_matrix.setTotalNegative(trueNegative + falseNegative);
				_matrix.setActualNegative(trueNegative + falsePositive);
				_matrix.setActualPositive(truePositive + falseNegative);

				_matrix.setAccuracy((double)(truePositive + trueNegative) / total);
				_matrix.setErrorRate((double)(falsePositive + falseNegative) / total);
				_matrix.setSensitivity((double)truePositive / (truePositive + falseNegative));
				_matrix.setFalsePos((double)falsePositive / (truePositive + falseNegative));
				_matrix.setSpecificity((double)trueNegative / (trueNegative + falsePositive));
				_matrix.setPrevalence((double)(truePositive + falseNegative) / total);
				_matrix.setNullError((double)(trueNegative + falsePositive) / total);

				var probability = (double)(trueNegative + truePositive) / total;  // Observed Proportionate Agreement
				var probabilityYes = (double)(truePositive + falseNegative) / total * (truePositive + falsePositive) / total;  // Yes Probablity of random agreement
				var probabilityNo = (double)(falsePositive + trueNegative) / total * (falseNegative + trueNegative) / total;  // No Probablity of random agreement
				var probabilityRandom = probabilityYes + probabilityNo;  // overall random agreement probability 

				_matrix.setPositivePredictiveRate((double)truePositive / (truePositive + falsePositive));
				_matrix.setNegativePredictiveRate((double)trueNegative / (trueNegative + falseNegative));

				var names1 = new string[] { "S3up1", "S3up2", "S3up3", "S3dn1", "S3dn2", "S3dn3" };
				var value1 = double.NaN;
				var color1 = Colors.White;
				foreach (string name1 in names1)
				{
					value1 = getCurrentValue(name1);
					if (!double.IsNaN(value1))
					{
						color1 = name1.Contains("up") ? Colors.Lime : Colors.Red;
						break;
					}
				}

				var names2 = new string[] { "FTTPUp1", "FTTPDn1" };
				var value2 = double.NaN;
				var color2 = Colors.White;
				foreach (string name2 in names2)
				{
					value2 = getCurrentValue(name2);
					if (!double.IsNaN(value2))
					{
						color2 = name2.Contains("Up") ? Colors.Lime : Colors.Red;
						break;
					}
				}
				//_matrix.setFTTP0(color2, value2);

				var names3 = new string[] { "FTTPUp2", "FTTPDn2" };
				var value3 = double.NaN;
				var color3 = Colors.White;
				foreach (string name3 in names3)
				{
					value3 = getCurrentValue(name3);
					if (!double.IsNaN(value3))
					{
						color3 = name3.Contains("Up") ? Colors.Lime : Colors.Red;
						break;
					}
				}

				var names4 = new string[] { "STTPUp1", "STTPDn1" };
				var value4 = double.NaN;
				var color4 = Colors.White;
				foreach (string name4 in names4)
				{
					value4 = getCurrentValue(name4);
					if (!double.IsNaN(value4))
					{
						color4 = name4.Contains("Up") ? Colors.Lime : Colors.Red;
						break;
					}
				}

				var names5 = new string[] { "STTPUp2", "STTPDn2" };
				var value5 = double.NaN;
				var color5 = Colors.White;
				foreach (string name5 in names5)
				{
					value5 = getCurrentValue(name5);
					if (!double.IsNaN(value5))
					{
						color5 = name5.Contains("Up") ? Colors.Lime : Colors.Red;
						break;
					}
				}

				var names6 = new string[] { "SC" };
				var value6 = double.NaN;
				var color6 = Colors.White;
				foreach (string name6 in names6)
				{
					value6 = getCurrentValue(name6);
					if (!double.IsNaN(value6))
					{
						color6 = name6.Contains("up") ? Colors.Lime : Colors.Red;
						break;
					}
				}
			}

			else
			{
				_matrix.Visibility = Visibility.Collapsed;
			}
		}


		private double getAccuracy()
		{
			var falsePositive = 0;
			var falseNegative = 0;
			var truePositive = 0;
			var trueNegative = 0;

			var modelName = _modelNames[0];

			List<DateTime> times = _barCache.GetTimes(_symbol, _interval, _extraBars, _barCount);

			var interval0 = GetOverviewInterval(_interval, 0);
			var predictions = _mpst.getPredictions(_symbol, interval0, modelName);
			var actuals = _mpst.getActuals(_symbol, interval0, modelName);

			for (int ii = 0; ii < times.Count; ii++)
			{
				var prediction = double.NaN;
				var actual = double.NaN;
				if (predictions.ContainsKey(times[ii]))
				{
					prediction = predictions[times[ii]];
				}
				if (actuals.ContainsKey(times[ii]))
				{
					actual = actuals[times[ii]];
				}

				if (!double.IsNaN(prediction) && !double.IsNaN(actual))
				{
					if (actual > 0.5 && prediction > 0.5) truePositive++;
					if (actual < 0.5 && prediction < 0.5) trueNegative++;
					if (actual < 0.5 && prediction > 0.5) falsePositive++;
					if (actual > 0.5 && prediction < 0.5) falseNegative++;
				}
			}

			int total = truePositive + falsePositive + falseNegative + trueNegative;

			return ((double)(truePositive + trueNegative) / total);
		}

		private double getCurrentValue(string name)
		{
			var output = double.NaN;
			int panelCount = _panels.Count;
			bool found = false;
			for (int panelNumber = 0; !found && panelNumber < panelCount; panelNumber++)
			{
				List<string> curves = _panels[panelNumber].GetCurveNames();
				int curveCount = curves.Count;
				for (int curveNumber = 0; !found && curveNumber < curveCount; curveNumber++)
				{
					var curveName = curves[curveNumber];
					if (curveName == name)
					{
						List<double> curveValues = _panels[panelNumber].GetCurveValues1(name);
						int currentBarIndex = curveValues.Count - _extraBars - 1;
						output = curveValues[currentBarIndex];
						if (!double.IsNaN(output))
						{
							found = true;
						}
					}
				}
			}
			return output;
		}

		private void drawChartTitle(int panelNumber, bool forPrint = false)
		{
			if (_panels[panelNumber].Title.Length > 0)
			{
				var top = 0.0;
				var left = getPanelLeft(panelNumber) + 65;

				var grid1 = new Grid();
				grid1.SetValue(Canvas.ZIndexProperty, 1000);
				grid1.Background = forPrint ? Brushes.Transparent : Brushes.Black;
				grid1.SetValue(Canvas.LeftProperty, left);
				grid1.SetValue(Canvas.TopProperty, top);
				grid1.Margin = new Thickness(2, 0, 2, 0);

				Label label = new Label();
				label.Margin = new Thickness(0);
				label.Padding = new Thickness(2, 0, 2, 0);
				label.Content = _panels[panelNumber].Title;
				label.HorizontalAlignment = HorizontalAlignment.Left;
				label.FontSize = 8;
				label.Foreground = forPrint ? Brushes.Black : Brushes.White;
				grid1.Children.Add(label);

				_panels[panelNumber].Canvas.Children.Add(grid1);
			}
		}

		private void drawChartCurves(int panelNumber, bool forPrint = false)
		{
			List<string> names = _panels[panelNumber].GetCurveNames();
			foreach (string name in names)
			{
				List<double> values1 = _panels[panelNumber].GetCurveValues1(name);
				List<int> colorIndex = _panels[panelNumber].GetCurveColorIndexes(name);
				List<Color> colors = _panels[panelNumber].GetCurveColors(name);
				double thickness = _panels[panelNumber].GetCurveThickness(name);
				int zOrder = _panels[panelNumber].GetCurveZOrder(name);
				CurveType curveType = _panels[panelNumber].GetCurveType(name);
				string tooltip = _panels[panelNumber].GetCurveTooltip(name);

				if (curveType == CurveType.Histogram)
				{
					var baseValue = _panels[panelNumber].GetCurveBase(name);
					List<double> values2 = _panels[panelNumber].GetCurveValues2(name);
					drawHistogram(panelNumber, values1, values2, colorIndex, colors, 0.618, baseValue);
				}
				else if (curveType == CurveType.Dash)
				{
					drawDashes(panelNumber, values1, colorIndex, colors, thickness, zOrder, tooltip, name);
				}
				else if (curveType == CurveType.None)
				{
				}
				else
				{
					drawLine(panelNumber, values1, colorIndex, colors, thickness, zOrder, false, tooltip);
				}
			}
		}

		private void drawSubPanelBorders()
		{
			double x1 = getPanelLeft(0);
			double x2 = getPanelRight(0);
			double y2 = getPanelBottom(0);

			int count = getPanelCount();
			for (int ii = 1; ii < count; ii++)
			{
				double width = getPanelWidth(ii);
				double height = getPanelHeight(ii);

				if (width >= 0 && height >= 0)
				{
					Line line2 = new Line();
					line2.X1 = x1;
					line2.Y1 = y2;
					line2.X2 = x2;
					line2.Y2 = y2;
					line2.Stroke = new SolidColorBrush(Color.FromArgb(0xff, 0x40, 0x40, 0x40));
					line2.StrokeEndLineCap = PenLineCap.Flat;
					line2.StrokeThickness = 1;
					_canvas.Children.Add(line2); // drawSubPanelBorders

					y2 += height;
				}
			}
		}

		Rect getCanvasClip(double x1, double y1, double width, double height)
		{
			double xMin = 0;
			double yMin = getPanelTop(0) + 1;
			double xMax = getPanelRight(0);
			double yMax = getPanelBottom(0);

			double x2 = x1 + width;
			if (xMax <= 0 || x2 < xMin || x1 > xMax)
			{
				width = 0;
			}
			else if (x1 < xMin && x2 > xMin)
			{
				double offset = xMin - x1;
				width -= offset;
				x1 = offset;
			}
			else if (x1 < xMax && x2 > xMax)
			{
				width = xMax - x1;
				x1 = 0;
			}
			else
			{
				x1 = 0;
			}

			double y2 = y1 + height;
			if (yMax <= 0 || y2 < yMin || y1 > yMax)
			{
				height = 0;
			}
			else if (y1 < yMin && y2 > yMin)
			{
				double offset = yMin - y1;
				height -= offset;
				y1 = offset;
			}
			else if (y1 < yMax && y2 > yMax)
			{
				height = yMax - y1;
				y1 = 0;
			}
			else
			{
				y1 = 0;
			}

			return new Rect(x1, y1, width, height);
		}

		void drawAnnotations(int panelNumber)
		{
			double panelHeight = getPanelHeight(panelNumber);
			if (panelHeight > 0 && !double.IsNaN(_timeScale))
			{
				Dictionary<int, double> offsetAbove = new Dictionary<int, double>();
				Dictionary<int, double> offsetBelow = new Dictionary<int, double>();

				lock (_barLock)
				{

					for (int index = _startIndex; index < _endIndex; index++)
					{
						if (index >= 0 && index < _bars.Count)
						{
							DateTime dateTime = _bars[index].Time;

							lock (_panels[panelNumber].Annotations)
							{
								if (_panels[panelNumber].Annotations.ContainsKey(dateTime))
								{
									if (!offsetAbove.ContainsKey(index)) offsetAbove[index] = 0;
									if (!offsetBelow.ContainsKey(index)) offsetBelow[index] = 0;

									List<ChartAnnotation> annotations = _panels[panelNumber].Annotations[dateTime];
									annotations.Sort((x, y) => x.Id.CompareTo(y.Id));

									foreach (ChartAnnotation annotation in annotations)
									{
										LineAnnotation line = annotation as LineAnnotation;
										if (line != null)
										{
											double width = Math.Max(1, _timeScale * annotation.Width);
											double x1 = getTimePixel(index) - width / 2;
											Rect rect = new Rect(x1, 0, width, panelHeight);
											line.Draw(_panels[panelNumber].Canvas, rect);
										}
										else
										{
											double width = Math.Max(6, _timeScale * annotation.Width);
											double x1 = getTimePixel(index) - width / 2;

											double price;
											double offset = 0;
											double space = _timeScale * annotation.Offset;
											double height = 0;
											if (annotation.Placement == Placement.Above)
											{
												price = _bars[index].High;
												if (double.IsNaN(price) && index > 0)
												{
													price = _bars[index].Close;
												}
												if (!double.IsNaN(price))
												{
													height = Math.Max(6, _timeScale * annotation.Height);
													offset = offsetAbove[index] - space;
													offsetAbove[index] -= (height + space);
												}
											}
											else if (annotation.Placement == Placement.Below)
											{
												price = _bars[index].Low;
												if (double.IsNaN(price) && index > 0)
												{
													price = _bars[index].Close;
												}
												if (!double.IsNaN(price))
												{
													height = Math.Max(6, _timeScale * annotation.Height);
													offset = offsetBelow[index] + space;
													offsetBelow[index] += (height + space);
												}
											}
											else // value placement
											{
												height = Math.Max(6, _timeScale * annotation.Height);
												price = annotation.Value;
												if (space > 0)
												{
													offset = offsetBelow[index] + space;
													offsetBelow[index] += (height + space);
												}
												else
												{
													offset = offsetAbove[index] - space;
													offsetAbove[index] -= (height + space);
												}
											}

											if (!double.IsNaN(price))
											{
												double y = getValuePixel(panelNumber, price);
												if (!double.IsNaN(y))
												{
													double y1 = 0;
													if (annotation.Placement == Placement.Above || annotation.Placement == Placement.Value && space < 0)
													{
														y1 = y - height - 4 + offset;
													}
													else if (annotation.Placement == Placement.Below || annotation.Placement == Placement.Value && space > 0)
													{
														y1 = y - 2 + offset;
													}

													if (index == 2001)
													{
														//Trace.WriteLine("Price = " + price + " Offset = " + offset);
													}

													Rect rect = new Rect(x1, y1, width, height);
													annotation.Draw(_panels[panelNumber].Canvas, rect);
												}
											}
										}
									}
								}
							}
						}
					}
				}
			}
		}

		public List<string> Intervals
		{
			get
			{
				//string[] intervals = { "Q","M", "W", "D" };
				string[] intervals = { "M", "W", "W30", "D", "D30", "240", "120", "60", "30", "15", "5" };
				return intervals.ToList();
			}
		}

		Grid _Grid = null;

		void drawTitle(bool forPrint = false)
		{
			int width = (int)_canvas.ActualWidth;
			double titleWidth1 = _symbolEditor.ActualWidth;
			double titleWidth2 = _intervalText.ActualWidth;
			double titleWidth3 = _intervalText.ActualWidth;
			double titleWidth = titleWidth1 + titleWidth2 + titleWidth3;

			_comboBox.Visibility = forPrint ? Visibility.Hidden : Visibility.Visible;

			_title.SetValue(Canvas.LeftProperty, (width - titleWidth) / 2);
			_title.SetValue(Canvas.TopProperty, 2.0);

#if BETA

            if (!double.IsNaN(_beta120))
            {
                _beta120Text.Text = "Beta 6M = " + _beta120.ToString("0.000");
                _beta120Text.SetValue(Canvas.LeftProperty, (width - 85.0));
                _beta120Text.SetValue(Canvas.TopProperty, 2.0);
                _canvas.Children.Add(_beta120Text);
            }
#endif

			_canvas.Children.Add(_title);

		}

		private Label _analysisLabel = null;

		private void draws()
		{
			if (HasTitleIntervals)
			{
				if (_Grid == null)
				{
					_Grid = new Grid();

					_Grid.HorizontalAlignment = HorizontalAlignment.Stretch;

					var col1 = new ColumnDefinition();
					var col2 = new ColumnDefinition();
					var col3 = new ColumnDefinition();
					col1.Width = new GridLength(1, GridUnitType.Star);
					col2.Width = new GridLength(1, GridUnitType.Auto);
					col3.Width = new GridLength(1, GridUnitType.Auto);
					_Grid.ColumnDefinitions.Add(col1);
					_Grid.ColumnDefinitions.Add(col2);
					_Grid.ColumnDefinitions.Add(col3);

					var label = new Label();
					Grid.SetColumn(label, 0);
					label.HorizontalAlignment = HorizontalAlignment.Right;
					//label.Content = "MATRIX";
					//label.ToolTip = "ATM Analysis on Demand";
					label.FontFamily = new FontFamily("Arial");
					label.FontSize = 9;
					label.Name = "Analysis";
					label.Margin = new Thickness(0, 0, 4, 0);
					label.VerticalAlignment = VerticalAlignment.Top;
					label.Foreground = new SolidColorBrush(_scoreCardActive ? Color.FromRgb(0x00, 0xcc, 0xff) : Color.FromRgb(0xff, 0xff, 0xff));
					label.Background = Brushes.Transparent;
					label.MouseEnter += Mouse_Enter;
					label.MouseLeave += Mouse_Score_Card_Leave;
					label.MouseLeftButtonDown += AOD_MouseLeftButtonDown;
					_analysisLabel = label;
					_analysisLabel.Visibility = Visibility.Visible;
					_Grid.Children.Add(label);

					label = new Label();
					Grid.SetColumn(label, 1);
					label.Content = "WRKG";
					label.HorizontalAlignment = HorizontalAlignment.Center;
					label.FontFamily = new FontFamily("Arial");
					label.FontSize = 9;
					label.Margin = new Thickness(0, 0, 0, 0);
					label.VerticalAlignment = VerticalAlignment.Top;
					label.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xcc, 0xff));
					label.Background = Brushes.Transparent;
					label.Visibility = Visibility.Collapsed;
					_workingLabel = label;
					_Grid.Children.Add(label);


					label = new Label();
					Grid.SetColumn(label, 2);
					label.HorizontalAlignment = HorizontalAlignment.Right;
					label.Content = "STUDIES";
					label.ToolTip = "Add Studies to Chart";
					label.FontFamily = new FontFamily("Arial");
					label.FontSize = 9;
					label.Margin = new Thickness(0, 0, 0, 0);
					label.VerticalAlignment = VerticalAlignment.Top;
					label.Foreground = Brushes.White;
					label.Background = Brushes.Transparent;
					label.MouseEnter += Mouse_Enter;
					label.MouseLeave += Mouse_Leave;
					label.MouseLeftButtonDown += Study_MouseLeftButtonDown;
					_Grid.Children.Add(label);

				}

				int width = (int)_canvas.ActualWidth;
				_Grid.Width = 120;
				_Grid.SetValue(Canvas.LeftProperty, (double)width - _Grid.Width);
				_Grid.SetValue(Canvas.TopProperty, 0.0);
				_canvas.Children.Add(_Grid);
			}
		}

		private bool _aodVisible = false;

		private void AOD_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			if (AnalysisEnable)
			{
				_aodVisible = !_aodVisible;
				draw();

				lock (_updateChart)
				{
					_updateChart.Add(getDataInterval());
				}

				SetScoreCardActive(_aodVisible);
			}
			else
			{
				onChartEvent(new ChartEventArgs(ChartEventType.AODToggle));
			}
		}

		bool _scoreCardActive = false;

		public void SetScoreCardActive(bool input)
		{
			_scoreCardActive = input;
			if (_analysisLabel != null)
			{
				_analysisLabel.Foreground = new SolidColorBrush(_scoreCardActive ? Color.FromRgb(0x00, 0xcc, 0xff) : Color.FromRgb(0xff, 0xff, 0xff));
			}
		}

		private void Study_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			var turnOn = (_controlPanel.Visibility == Visibility.Collapsed);

			if (turnOn)
			{
				var indicators = _indicators.Where(x => x.Value.Enabled).Select(x => x.Key).ToList();
				setIndicators(_tree, indicators);
				if (_analysisLabel != null)
				{
					_analysisLabel.Visibility = Visibility.Collapsed;
				}
			}
			else
			{
				if (_analysisLabel != null)
				{
					_analysisLabel.Visibility = Visibility.Visible;
				}
			}

			_controlPanel.Visibility = turnOn ? Visibility.Visible : Visibility.Collapsed;
		}

		private void Interval_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			var cb = sender as ComboBox;
			var time = cb.SelectedItem as string;
			if (time != null)
			{
				cb.SelectedValue = "";

				if (time == "D") time = "Daily";
				else if (time == "W") time = "Weekly";
				else if (time == "M") time = "Monthly";
				else if (time == "Q") time = "Quarterly";
				else if (time == "S") time = "SemiAnnually";
				else if (time == "Y") time = "Yearly";

				Change(_symbol, time);
			}
		}

		public bool HasTitleIntervals { get; set; }

		private void Interval_MouseEnter(object sender, MouseEventArgs e)
		{
			Label label = sender as Label;
			label.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xcc, 0xff));
		}

		private void Interval_MouseLeave(object sender, MouseEventArgs e)
		{
			Label label = sender as Label;
			label.Foreground = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));
		}

		public bool SpreadSymbols { get; set; }

		void drawControlBoxes(bool forPrint = false)
		{
			if (!forPrint)
			{
				if (SpreadSymbols)
				{
					//string[] fields1 = _sectorSymbol.Split(' ');
					//string[] fields2 = _sectorETFSymbol.Split(' ');
					//string[] fields3 = _stkSymbol.Split(' ');
					//string etfSymbol = (fields1.Length > 0) ? fields1[0] : "";
					//string idxSymbol = (fields2.Length > 0) ? fields2[0] : "";
					//string stkSymbol = (fields2.Length > 0) ? fields3[0] : "";

					var panel = new StackPanel();
					panel.Orientation = Orientation.Horizontal;
					Canvas.SetLeft(panel, 60);  //120
					Canvas.SetTop(panel, -4);

					// stock 
					//if (stkSymbol.Length > 0)
					//{
					//    Label label1 = new Label();
					//    label1.Content = stkSymbol;
					//    label1.Foreground = new SolidColorBrush(_foregroundColor);
					//    label1.FontSize = 10;
					//    label1.Margin = new Thickness(0, 1, 0, 0);
					//    label1.Cursor = Cursors.Hand;
					//    label1.MouseEnter += Interval_MouseEnter;
					//    label1.MouseLeave += Interval_MouseLeave;
					//    label1.MouseLeftButtonDown += StockMouseDown;
					//    panel.Children.Add(label1);
					//}

					// stock - etf
					//if (stkSymbol.Length > 0 && _stkSymbol.Contains(" Equity"))
					//{
					//    Label label3 = new Label();
					//    label3.Content = stkSymbol + ATMML.Symbol.SpreadCharacter;
					//    label3.Foreground = new SolidColorBrush(_foregroundColor);
					//    label3.FontSize = 10;
					//    label3.Margin = new Thickness(0, 1, 0, 0);
					//    label3.Cursor = Cursors.Hand;
					//    label3.MouseEnter += Interval_MouseEnter;
					//    label3.MouseLeave += Interval_MouseLeave;
					//    label3.MouseLeftButtonDown += ETFSpreadMouseDown;
					//    panel.Children.Add(label3);
					//}

					// stock - index
					//if (stkSymbol.Length > 0 && idxSymbol.Length > 0 && _stkSymbol.Contains(" Equity"))
					//{
					//    Label label4 = new Label();
					//    label4.Content = stkSymbol + ATMML.Symbol.SpreadCharacter + idxSymbol;
					//    label4.Foreground = new SolidColorBrush(_foregroundColor);
					//    label4.FontSize = 10;
					//    label4.Margin = new Thickness(0, 1, 0, 0);
					//    label4.Cursor = Cursors.Hand;
					//    label4.MouseEnter += Interval_MouseEnter;
					//    label4.MouseLeave += Interval_MouseLeave;
					//    label4.MouseLeftButtonDown += IndexSpreadMouseDown;
					//    panel.Children.Add(label4);
					//}
					_canvas.Children.Add(panel);
				}

				foreach (Image image in _controlBoxImages)
				{
					_canvas.Children.Add(image);
				}
			}
		}

		private void StockMouseDown(object sender, MouseButtonEventArgs e)
		{
			if (_stkSymbol.Length > 0)
			{
				Change(_stkSymbol, _interval, false);
			}
		}

		private void ETFSpreadMouseDown(object sender, MouseButtonEventArgs e)
		{
			if (_stkSymbol.Length > 0 && _sectorSymbol.Length > 0)
			{
				Change(_stkSymbol + ATMML.Symbol.SpreadCharacter + _sectorSymbol, _interval, false);
			}
		}

		private void IndexSpreadMouseDown(object sender, MouseButtonEventArgs e)
		{
			if (_stkSymbol.Length > 0 && _sectorETFSymbol.Length > 0)
			{
				Change(_stkSymbol + ATMML.Symbol.SpreadCharacter + _sectorETFSymbol, _interval, false);
			}
		}

		void drawPR()
		{
			if (_PR != null && MainView.EnableRP)
			{
				bool prSigCurrent = getIndicatorEnable("PR");
				//bool prSigCurrent = getIndicatorEnable("PR") || getIndicatorEnable("Starting PR Sig");

				if (prSigCurrent)
				{
					string interval = getDataInterval();
					List<DateTime> times = _barCache.GetTimes(_symbol, interval, _extraBars, _barCount);
					if (times.Count > 0)
					{
						Series[] series = GetSeries(_symbol, interval, new string[] { "High", "Low" }, _extraBars, _barCount);
						int currentBarIndex = times.Count - _extraBars - 1;


						double mid = (series[0][currentBarIndex] + series[1][currentBarIndex]) / 2;
						double x = getTimePixel(currentBarIndex + 2);
						double y = getValuePixel(0, mid);

						if (!double.IsNaN(x) && !double.IsNaN(x))
						{
							double value = _PR[currentBarIndex];
							Brush brush = Brushes.Transparent;

							Label label = new Label();
							label.SetValue(Canvas.TopProperty, y);
							label.SetValue(Canvas.LeftProperty, x);
							label.VerticalAlignment = VerticalAlignment.Center;
							label.Margin = new Thickness(0);
							label.Padding = new Thickness(0);
							label.Foreground = brush;
							label.Content = value.ToString(".00");

							_canvas.Children.Add(label);
						}
					}
				}
			}
		}

		void drawReviewAction()
		{
			string interval;
			ReviewAction action = MainView.GetReviewSymbolInterval(_portfolioName, _symbol, out interval).Action;

			if (interval == _interval)
			{
				Label label = new Label();
				label.SetValue(Canvas.TopProperty, 2.0);
				label.SetValue(Canvas.LeftProperty, _canvas.ActualWidth - 120);
				label.Margin = new Thickness(0);
				label.Padding = new Thickness(0);
				label.Foreground = Brushes.Red;
				label.Content = "";
				if (action == ReviewAction.AnticipateBuy) label.Content = "Prepare to Buy";
				else if (action == ReviewAction.AnticipateShort) label.Content = "Prepare to Short";
				else if (action == ReviewAction.ExpectLongExit) label.Content = "Expect Long Exit";
				else if (action == ReviewAction.ExpectShortExit) label.Content = "Expect Short Exit";
				else if (action == ReviewAction.AddShort) label.Content = "Look to Add to Short";
				else if (action == ReviewAction.AddLong) label.Content = "Look to Add to Long";
				else if (action == ReviewAction.HoldPosition) label.Content = "Hold Position";
				else if (action == ReviewAction.HoldLong) label.Content = "Hold Long";

				_canvas.Children.Add(label);
			}
		}

		void ControlBox_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			Image image = sender as Image;
			string name = image.Name;
			ChartEventType id = (ChartEventType)int.Parse(name.Substring(name.Length - 1));

			if (id == ChartEventType.ToggleCursor)
			{
				ShowCursor = !ShowCursor;
				foreach (Chart chart in _linkedCharts)
				{
					chart.ShowCursor = ShowCursor;
					chart.update();
				}
				update();

				onChartEvent(new ChartEventArgs(ChartEventType.ToggleCursor));
			}
			else if (id == ChartEventType.ReloadData)
			{
				_requestBarTime = DateTime.Now;
				_reload = true;

				List<string> intervals = getDataIntervals();
				foreach (string interval in intervals)
				{
					_barCache.Clear(_symbol, interval, true);
				}

				foreach (string interval in intervals)
				{
					_barCache.RequestBars(_symbol, interval, true, _barCount, true);
				}
			}
			else
			{
				onChartEvent(new ChartEventArgs(id));
			}
			e.Handled = true;
		}

		private void onChartEvent(ChartEventArgs e)
		{
			if (ChartEvent != null)
			{
				ChartEvent(this, e);
			}
		}

		void drawBackground()
		{
			for (int ii = 0; ii < _panels.Count; ii++)
			{
				double left = getPanelLeft(ii);
				double top = getPanelTop(ii);
				double width = getPanelWidth(ii);
				double height = getPanelHeight(ii);

				if (height > 0 && width > 0)
				{

					Canvas canvas = _panels[ii].Canvas;
					canvas.Children.Clear();
					canvas.Height = height;
					canvas.Width = width;
					canvas.SetValue(Canvas.LeftProperty, left);
					canvas.SetValue(Canvas.TopProperty, top);

					RectangleGeometry rectangleGeometry = new RectangleGeometry();
					rectangleGeometry.Rect = new Rect(1.5, 1.5, width - 3, height - 3);
					canvas.Clip = rectangleGeometry;

					_canvas.Children.Add(canvas);
				}
			}
		}

		void drawBorder()
		{
			int width1 = (int)_canvas.ActualWidth;
			int height1 = (int)_canvas.ActualHeight;
			int height2 = height1 - _timeScaleHeight;
			int width2 = width1 - _valueScaleWidth;

			Color borderColor = _active ? Color.FromRgb(0xd3, 0xd3, 0xd3) : Color.FromRgb(0x12, 0x4b, 0x72);
			drawRectangle(new Point(0.0, _titleHeight + 0.5), new Point((double)width2, (double)height2), Color.FromRgb(0x12, 0x4b, 0x72), Colors.Transparent, 1);
			drawRectangle(new Point(0.0, 0.0), new Point((double)width1, (double)height1), borderColor, Colors.Transparent, 1);
		}

		void drawTimeScale(IList<TimeScaleInfo> timeScaleInfo)
		{
			int panelCount = getPanelCount();

			double width = _canvas.ActualWidth;

			double x1 = getPanelLeft(0);
			double x2 = getPanelRight(0);

			double y1 = getPanelBottom(panelCount - 1);
			double y2 = _canvas.ActualHeight;

			lock (_barLock)
			{

				if (_bars.Count > 0 && width > 0)
				{
					for (int ii = 0; ii < timeScaleInfo.Count; ii++)
					{
						if (timeScaleInfo[ii].m_level == 1)
						{
							double x = timeScaleInfo[ii].m_pixel;

							if (x1 < x && x < x2)
							{
								drawLine(new Point(x, y1), new Point(x, y2), _foregroundColor, 0.5);

								TextBlock textBlock = new TextBlock();
								DateTime time = timeScaleInfo[ii].m_time;
								textBlock.Text = time.ToString(_timeScaleFormats[timeScaleInfo[ii].m_type]);
								textBlock.Height = _timeScaleHeight;
								textBlock.SetValue(Canvas.LeftProperty, x + 1);
								textBlock.SetValue(Canvas.TopProperty, y1);
								textBlock.TextAlignment = TextAlignment.Left;
								textBlock.FontSize = _fontSize;
								textBlock.FontWeight = FontWeights.Light;
								textBlock.FontFamily = new FontFamily("Arial");
								textBlock.Foreground = new SolidColorBrush(_foregroundColor);
								_canvas.Children.Add(textBlock);  // drawTimeScale
							}
						}
					}
				}
			}
		}

		void drawValueScale(int panelNumber, IList<ValueScaleInfo> valueScaleInfo)
		{
			double top = getPanelTop(panelNumber);

			double x1 = getPanelRight(panelNumber);
			double y2 = getPanelBottom(panelNumber);
			double valueScale = _panels[panelNumber].ValueScale;
			double valueBase = _panels[panelNumber].ValueBase;

			bool validScale = (!double.IsNaN(valueScale) && !double.IsNaN(valueBase));
			if (validScale && y2 > 0)
			{
				int fontHeight = 14;

				for (int ii = 0; ii < valueScaleInfo.Count; ii++)
				{
					ValueScaleInfo vsi = valueScaleInfo[ii];
					if (vsi.m_level == 1)
					{
						double v1 = vsi.m_pixel;

						String valueText = "";

						if (panelNumber == 0 && (_symbol == "F.USA" || _symbol == "F.TYA" || _symbol.Contains(" Curncy")))
						{
							valueText = getValueText(vsi.m_value);
						}
						else
						{

							if (vsi.m_scale == 1)
							{
								valueText = vsi.m_value.ToString("F" + vsi.m_digits.ToString());
							}
							else if (vsi.m_scale == 0)
							{
								valueText = vsi.m_value.ToString("E");
							}
							else
							{
								double value = vsi.m_value / vsi.m_scale;
								valueText = value.ToString("F" + vsi.m_digits.ToString());
								valueText += vsi.m_label;
							}
						}

						double x = _canvas.ActualWidth - _valueScaleWidth;
						double y = v1 - fontHeight / 2;

						if (y >= 0 && y + (fontHeight / 2) <= y2)
						{
							Rect rect = new Rect(Math.Max(0, x), top + y, _valueScaleWidth - 2, fontHeight);

							TextBlock textBlock = new TextBlock();
							textBlock.Text = valueText;
							textBlock.Height = rect.Height;
							textBlock.Width = rect.Width;
							textBlock.SetValue(Canvas.LeftProperty, rect.Left);
							textBlock.SetValue(Canvas.TopProperty, rect.Top);
							textBlock.TextAlignment = TextAlignment.Right;
							textBlock.FontSize = _fontSize;
							textBlock.Foreground = new SolidColorBrush(_foregroundColor);
							textBlock.FontWeight = FontWeights.Light;
							textBlock.FontFamily = new FontFamily("Arial");

							_canvas.Children.Add(textBlock);  // drawValueScale
						}
					}
				}
			}
		}

		void drawGrid(int panelNumber, IList<ValueScaleInfo> valueScaleInfo, IList<TimeScaleInfo> timeScaleInfo)
		{
			double width = getPanelWidth(panelNumber);
			double height = getPanelHeight(panelNumber);

			if (width >= 0 && height >= 0)
			{
				if (!_scrollChart && !_changeTimeScale && !_changeValueScale)
				{

					GeometryGroup group = new GeometryGroup();

					for (int ii = 0; ii < timeScaleInfo.Count; ii++)
					{
						// 0 = nothing, 1 = major grid and label, 2 = minor grid
						double x = timeScaleInfo[ii].m_pixel;

						if (0 <= x && x < width)
						{
							// 0 = nothing, 1 = major grid and label, 2 = major grid, 3 = minor grid
							for (int jj = 0; jj < valueScaleInfo.Count; jj++)
							{
								double y = valueScaleInfo[jj].m_pixel;

								if (0 < y && y < height)
								{
									if ((valueScaleInfo[jj].m_level > 1 && timeScaleInfo[ii].m_level == 1) ||   // major grid
										(valueScaleInfo[jj].m_level == 1 && timeScaleInfo[ii].m_level >= 1))     // minor grid
									{
										RectangleGeometry rect1 = new RectangleGeometry(new Rect(x, y, 0, 0));
										group.Children.Add(rect1);
									}
								}
							}
						}
					}

					Path path = new Path();
					path.Stroke = new SolidColorBrush(Color.FromRgb(0x40, 0x40, 0x40));
					path.StrokeEndLineCap = PenLineCap.Flat;
					path.StrokeThickness = 1;
					path.Fill = Brushes.Transparent;
					path.Data = group;
					_panels[panelNumber].Canvas.Children.Add(path);  // drawGrid                   
				}
			}
		}

		void drawRectangle(Point p1, Point p2, Color strokeColor, Color fillColor, double thickness)
		{
			double width = p2.X - p1.X;
			double height = p2.Y - p1.Y;
			if (width > 0 && height > 0)
			{
				Rectangle rectangle = new Rectangle();
				rectangle.Height = height;
				rectangle.Width = width;
				rectangle.SetValue(Canvas.LeftProperty, p1.X);
				rectangle.SetValue(Canvas.TopProperty, p1.Y);
				rectangle.Fill = new SolidColorBrush(fillColor);
				rectangle.Stroke = new SolidColorBrush(strokeColor);
				rectangle.StrokeEndLineCap = PenLineCap.Flat;
				rectangle.StrokeThickness = thickness;
				rectangle.RadiusX = 2;
				rectangle.RadiusY = 2;
				rectangle.SetValue(Canvas.ZIndexProperty, -100);
				_canvas.Children.Add(rectangle);
			}
		}

		private void drawHistogram(int panelNumber, List<double> values1, List<double> values2, List<int> colorIndex, List<Color> colors, double thickness, double baseValue)
		{
			if (values1 != null && _timeScale > 0)
			{
				double width = thickness * _timeScale;

				Brush defaultBrush = new SolidColorBrush(_foregroundColor);
				List<Brush> brushes = new List<Brush>();
				foreach (Color color in colors)
				{
					brushes.Add(new SolidColorBrush(color));
				}

				for (int index = _startIndex; index < _endIndex; index++)
				{
					double value1 = (index >= 0 && index < values1.Count) ? values1[index] : double.NaN;
					double value2 = (values2 != null && values2.Count > 0 && index >= 0 && index < values2.Count) ? values2[index] : baseValue;
					if (!double.IsNaN(value1))
					{
						double x2 = getTimePixel(index) - (width / 2);
						double y2 = getValuePixel(panelNumber, value1);
						double y1 = getValuePixel(panelNumber, value2);

						if (!double.IsNaN(x2) && !double.IsNaN(y2))
						{
							int colorIdx = (index >= 0 && index < colorIndex.Count) ? colorIndex[index] : 0;
							Rectangle rectangle = new Rectangle();
							rectangle.Height = Math.Abs(y1 - y2);
							rectangle.Width = width;
							rectangle.SetValue(Canvas.LeftProperty, x2);
							rectangle.SetValue(Canvas.TopProperty, Math.Min(y1, y2));
							rectangle.Fill = (colorIdx >= 0 && colorIdx < colors.Count) ? brushes[colorIdx] : defaultBrush;
							rectangle.StrokeThickness = 0;
							_panels[panelNumber].Canvas.Children.Add(rectangle);  // drawHistogram
						}
					}
				}

				double yBase = getValuePixel(panelNumber, baseValue);
				if (!double.IsNaN(yBase))
				{
					Line line = new Line();
					line.X1 = getPanelLeft(panelNumber);
					line.Y1 = yBase;
					line.X2 = getPanelRight(panelNumber);
					line.Y2 = yBase;
					line.Stroke = Brushes.DarkGray;
					line.StrokeEndLineCap = PenLineCap.Flat;
					line.StrokeThickness = 0.25;
					_panels[panelNumber].Canvas.Children.Add(line);  // drawHistogram
				}
			}
		}

		void drawChartBackgrounds(int panelNumber)
		{
			RectangleGeometry rectangle = new RectangleGeometry();

			double width = getPanelWidth(panelNumber);
			double height = getPanelHeight(panelNumber);

			if (width >= 0 && height >= 0 && _timeScale > 0)
			{
				double padding = _timeScale / 2;
				{
					// todo
				}
			}
		}

		void drawChartLines(int panelNumber)
		{
			RectangleGeometry rectangle = new RectangleGeometry();

			double width = getPanelWidth(panelNumber);
			double height = getPanelHeight(panelNumber);

			if (width >= 0 && height >= 0 && _timeScale > 0)
			{
				double padding = _timeScale / 2;

				List<ChartLine> chartLines = _panels[panelNumber].Lines;
				foreach (ChartLine chartLine in chartLines)
				{
					Point p1 = new Point();
					Point p2 = new Point();
					int index1 = getTimeIndex(chartLine.Point1.Time);
					int index2 = getTimeIndex(chartLine.Point2.Time);

					if (chartLine.Truncate)
					{
						if (index1 > _currentIndex)
						{
							continue;
						}
						if (index2 > _currentIndex)
						{
							index2 = _currentIndex;
						}
					}

					p1.X = getTimePixel(index1 + chartLine.Point1.Offset);// - padding;
					p2.X = getTimePixel(index2 + chartLine.Point2.Offset);// + padding;
					p1.Y = getValuePixel(panelNumber, chartLine.Point1.Value);
					p2.Y = getValuePixel(panelNumber, chartLine.Point2.Value);

					if (!double.IsNaN(p1.Y) && !double.IsNaN(p2.Y))
					{

						if (chartLine.Type != LineType.Segment)
						{
							if (p1.X > p2.X)
							{
								Point p3 = p1;
								p1 = p2;
								p2 = p3;
							}

							double slopeX = (p2.X - p1.X) / (p2.Y - p1.Y);
							double slopeY = (p2.Y - p1.Y) / (p2.X - p1.X);

							double minX = getPanelLeft(panelNumber);
							double maxX = getPanelRight(panelNumber);
							double minY = getPanelTop(panelNumber);
							double maxY = getPanelBottom(panelNumber);

							if (Math.Abs(slopeY) < Math.Abs(slopeX))
							{
								if (chartLine.Type == LineType.RayLeft || chartLine.Type == LineType.Line)
								{
									double y1 = p1.Y + slopeY * (minX - p1.X);
									p1.X = minX;
									p1.Y = y1;
								}
								if (chartLine.Type == LineType.RayRight || chartLine.Type == LineType.Line)
								{
									double y2 = p1.Y + slopeY * (maxX - p1.X);
									p2.X = maxX;
									p2.Y = y2;
								}
							}
							else
							{
								if (chartLine.Type == LineType.RayRight || chartLine.Type == LineType.Line)
								{
									double x1 = p1.X + slopeX * (minY - p1.Y);
									p2.X = x1;
									p2.Y = minY;
								}
								if (chartLine.Type == LineType.RayLeft || chartLine.Type == LineType.Line)
								{
									double x2 = p1.X + slopeX * (maxY - p1.Y);
									p1.X = x2;
									p1.Y = maxY;
								}
							}
						}

						Line line = new Line();
						line.ToolTip = chartLine.ToolTip;
						line.X1 = p1.X;
						line.Y1 = p1.Y;
						line.X2 = p2.X;
						line.Y2 = p2.Y;
						line.Stroke = new SolidColorBrush(chartLine.Color);
						line.StrokeEndLineCap = chartLine.Cap;
						line.StrokeThickness = chartLine.Thickness;
						if (chartLine.Dotted)
						{
							DoubleCollection dash = new DoubleCollection();
							dash.Add(2.0);
							dash.Add(4.0);
							line.StrokeDashArray = dash;
						}
						line.SetValue(Canvas.ZIndexProperty, chartLine.ZOrder);
						_panels[panelNumber].Canvas.Children.Add(line);  // drawChartLines
					}
				}
			}
		}

		void drawChartBezierCurves(int panelNumber)
		{
			double width = getPanelWidth(panelNumber);
			double height = getPanelHeight(panelNumber);

			if (width >= 0 && height >= 0 && _timeScale > 0)
			{
				List<ChartBezierCurve> chartBezierCurves = _panels[panelNumber].BezierCurves;
				foreach (ChartBezierCurve chartBezierCurve in chartBezierCurves)
				{
					int idx = 0;
					double begIndex = 0;
					double endIndex = 0;
					double[] b = new double[2 * chartBezierCurve.Points.Count];
					foreach (TimeValuePoint point in chartBezierCurve.Points)
					{
						int index1 = getTimeIndex(point.Time);
						double xx = getTimePixel(index1 + point.Offset);
						double yy = getValuePixel(panelNumber, point.Value);
						if (idx == 0) begIndex = xx;
						endIndex = xx;
						b[idx++] = xx;
						b[idx++] = yy;
					}

					int cpts = (int)(endIndex - begIndex + 1);
					if (cpts > 12) cpts = 12;
					double[] p = new double[2 * cpts];
					_bezierCurve.Bezier2D(b, cpts, p);

					Brush brush = new SolidColorBrush(chartBezierCurve.Color);

					for (int ii = 1; ii < cpts; ii++)
					{
						Line line = new Line();
						line.X1 = p[2 * ii - 2];
						line.Y1 = p[2 * ii - 1];
						line.X2 = p[2 * ii + 0];
						line.Y2 = p[2 * ii + 1];
						line.Stroke = brush;
						line.StrokeEndLineCap = chartBezierCurve.Cap;
						line.StrokeThickness = chartBezierCurve.Thickness;
						if (chartBezierCurve.Dotted)
						{
							DoubleCollection dash = new DoubleCollection();
							dash.Add(2.0);
							dash.Add(4.0);
							line.StrokeDashArray = dash;
						}
						_panels[panelNumber].Canvas.Children.Add(line);  // drawChartBezierCurves
					}
				}
			}
		}

		void drawChartBezierShading(int panelNumber)
		{
			double width = getPanelWidth(panelNumber);
			double height = getPanelHeight(panelNumber);

			if (width >= 0 && height >= 0 && _timeScale > 0)
			{
				List<ChartBezierShading> chartBezierShadings = _panels[panelNumber].BezierShadings;
				foreach (ChartBezierShading chartBezierShading in chartBezierShadings)
				{
					Color color = chartBezierShading.Color;
					if (color == Colors.Transparent)
					{
						color = Color.FromArgb(0x30, _foregroundColor.R, _foregroundColor.G, _foregroundColor.B);
					}
					Brush brush = new SolidColorBrush(color);

					int idx = 0;
					double baseValue = double.NaN;
					double[] b1 = new double[2 * chartBezierShading.FirstCurvePoints.Count];
					lock (_barLock)
					{
						foreach (TimeValuePoint point in chartBezierShading.FirstCurvePoints)
						{
							int index1 = getTimeIndex(point.Time);
							if (double.IsNaN(baseValue) && index1 >= 0 && index1 < _bars.Count)
							{
								Bar bar = _bars[index1];
								baseValue = (chartBezierShading.Direction > 0) ? bar.Low : bar.High;
								if (double.IsNaN(baseValue) && index1 > 0)
								{
									for (int ii = 1; ii < 100; ii++)
									{
										int index2 = index1 - ii;
										if (index2 < 0) break;
										Bar bar2 = _bars[index2];
										baseValue = (chartBezierShading.Direction > 0) ? bar2.Low : bar2.High;
										if (!double.IsNaN(baseValue)) break;
									}
								}
							}
							double xx = getTimePixel(index1 + point.Offset);
							double yy = getValuePixel(panelNumber, baseValue + point.Value);
							b1[idx++] = xx;
							b1[idx++] = yy;
						}
					}
					int cpts = 12;
					double[] p1 = new double[2 * cpts];
					_bezierCurve.Bezier2D(b1, cpts, p1);

					idx = 0;
					double[] b2 = new double[2 * chartBezierShading.SecondCurvePoints.Count];
					foreach (TimeValuePoint point in chartBezierShading.SecondCurvePoints)
					{
						int index1 = getTimeIndex(point.Time);
						double xx = getTimePixel(index1 + point.Offset);
						double yy = getValuePixel(panelNumber, baseValue + point.Value);
						b2[idx++] = xx;
						b2[idx++] = yy;
					}
					double[] p2 = new double[2 * cpts];
					_bezierCurve.Bezier2D(b2, cpts, p2);

					Polygon polygon = new Polygon();
					polygon.Fill = brush;
					PointCollection points = new PointCollection();
					for (int ii = 0; ii < cpts; ii++)
					{
						points.Add(new Point(p2[2 * ii], p2[2 * ii + 1]));
					}
					for (int ii = cpts - 1; ii >= 0; ii--)
					{
						points.Add(new Point(p1[2 * ii], p1[2 * ii + 1]));
					}
					polygon.Points = points;
					int zOrder = chartBezierShading.ZOrder;
					polygon.SetValue(Canvas.ZIndexProperty, zOrder);
					polygon.ToolTip = chartBezierShading.Tooltip;

					_panels[panelNumber].Canvas.Children.Add(polygon);  // drawChartBezierShading
				}
			}
		}

		void drawCursor()
		{
			if (isTimeValid(_cursorTime))
			{
				addCursorLine();
			}
			setCursorVisibility();
		}

		private string getCursorTextFormat()
		{
			string format = "";
			if (_interval == "1Y") format = "yyyy";
			else if (_interval == "1S") format = "MM/yy";
			else if (_interval == "1Q") format = "MM/yy";
			else if (_interval == "1M") format = "MM/yy";
			else if (_interval == "1W") format = "MM/dd";
			else if (_interval == "1D") format = "MM/dd";
			else format = "HH:mm";
			return format;
		}

		private bool isHistoricalInterval()
		{
			return ((_interval == "1Y" || _interval == "1S" || _interval == "1Q" || _interval == "1M" || _interval == "1W" || _interval == "1D"));
		}

		private void drawEarningsDates()
		{
			if (!isHistoricalInterval() || _interval == "1W" || _interval == "1D")
			{
				bool nextEarningDate = getIndicatorEnable("Next Date");
				if (nextEarningDate)
				{
					drawEarningDate(_expectedReportDate);

					foreach (DateTime date in _historicalEarnings)
					{
						drawEarningDate(date);
					}
				}
			}
		}

		private void drawEarningDate(DateTime input)
		{
			if (input != new DateTime())
			{
				double right = getPanelRight(0);
				double left = getPanelLeft(0);
				int index1 = getTimeIndex(input);
				double pixel1 = getTimePixel(index1);
				if (!double.IsNaN(pixel1) && pixel1 < right && pixel1 > left)
				{
					string timeText = input.ToString(getCursorTextFormat());
					int panelCount = _panels.Count;
					double timeScaleTop = getPanelBottom(panelCount - 1);
					double timeScaleBottom = _canvas.ActualHeight;

					Line expectedReportVerticalLine = new Line();
					expectedReportVerticalLine.X1 = pixel1;
					expectedReportVerticalLine.Y1 = getPanelTop(0);
					expectedReportVerticalLine.X2 = pixel1;
					expectedReportVerticalLine.Y2 = getPanelBottom(getPanelCount() - 1);
					expectedReportVerticalLine.Stroke = Brushes.DarkOrange;
					expectedReportVerticalLine.StrokeEndLineCap = PenLineCap.Flat;
					expectedReportVerticalLine.StrokeThickness = 0.5;
					expectedReportVerticalLine.ToolTip = "Next Earnings Report";
					_canvas.Children.Add(expectedReportVerticalLine);

					TextBlock expectedReportDateText = new TextBlock();
					expectedReportDateText.Text = timeText;
					expectedReportDateText.Height = timeScaleBottom - timeScaleTop - 2;
					expectedReportDateText.SetValue(Canvas.LeftProperty, pixel1 - 10);
					expectedReportDateText.SetValue(Canvas.TopProperty, timeScaleTop);
					expectedReportDateText.TextAlignment = TextAlignment.Center;
					expectedReportDateText.FontSize = _fontSize;
					expectedReportDateText.Background = Brushes.Black;
					expectedReportDateText.Foreground = Brushes.DarkOrange;
					expectedReportDateText.FontWeight = FontWeights.Light;
					expectedReportDateText.FontFamily = new FontFamily("Arial");
					expectedReportDateText.ToolTip = "Next Earnings Report";

					_canvas.Children.Add(expectedReportDateText);
				}
			}
		}

		private void drawExpectedDVDDates(DateTime input)
		{
			if (input != new DateTime())
			{
				double right = getPanelRight(0);
				double left = getPanelLeft(0);
				int index1 = getTimeIndex(input);
				double pixel1 = getTimePixel(index1);
				if (!double.IsNaN(pixel1) && pixel1 < right && pixel1 > left)
				{
					string timeText = input.ToString(getCursorTextFormat());
					int panelCount = _panels.Count;
					double timeScaleTop = getPanelBottom(panelCount - 1);
					double timeScaleBottom = _canvas.ActualHeight;

					Line expectedReportVerticalLine = new Line();
					expectedReportVerticalLine.X1 = pixel1;
					expectedReportVerticalLine.Y1 = getPanelTop(0);
					expectedReportVerticalLine.X2 = pixel1;
					expectedReportVerticalLine.Y2 = getPanelBottom(getPanelCount() - 1);
					expectedReportVerticalLine.Stroke = Brushes.DodgerBlue;
					expectedReportVerticalLine.StrokeEndLineCap = PenLineCap.Flat;
					expectedReportVerticalLine.StrokeThickness = 0.5;
					expectedReportVerticalLine.ToolTip = "Nxt Expected Dividend";
					_canvas.Children.Add(expectedReportVerticalLine);

					TextBlock expectedReportDateText = new TextBlock();
					expectedReportDateText.Text = timeText;
					expectedReportDateText.Height = timeScaleBottom - timeScaleTop - 2;
					expectedReportDateText.SetValue(Canvas.LeftProperty, pixel1 - 10);
					expectedReportDateText.SetValue(Canvas.TopProperty, timeScaleTop);
					expectedReportDateText.TextAlignment = TextAlignment.Center;
					expectedReportDateText.FontSize = _fontSize;
					expectedReportDateText.Background = Brushes.Black;
					expectedReportDateText.Foreground = Brushes.DodgerBlue;
					expectedReportDateText.FontWeight = FontWeights.Light;
					expectedReportDateText.FontFamily = new FontFamily("Arial");
					expectedReportDateText.ToolTip = "Nxt Expected Dividend";

					_canvas.Children.Add(expectedReportDateText);
				}
			}
		}

		private void drawBestTargetHi()
		{
			if (!double.IsNaN(_bestTargetHi))
			{
				double pixel1 = getTimePixel(_currentIndex);
				double pixel2 = getValuePixel(0, _bestTargetHi);

				if (!double.IsNaN(pixel1) && !double.IsNaN(pixel2))
				{
					int fontHeight = 14;
					double top = getPanelTop(0);
					double y2 = getPanelBottom(0);

					double v1 = pixel2;
					string valueText = _bestTargetHi.ToString("F" + 2);

					double x = _canvas.ActualWidth - _valueScaleWidth;
					double y = v1 - fontHeight / 2;

					if (y >= 0 && y + (fontHeight / 2) <= y2)
					{
						Rect rect2 = new Rect(Math.Max(0, x), top + y, _valueScaleWidth - 2, fontHeight);

						Line bestTargetHiHorizontalLine = new Line();
						bestTargetHiHorizontalLine.X1 = pixel1;
						bestTargetHiHorizontalLine.Y1 = pixel2;
						bestTargetHiHorizontalLine.X2 = getPanelRight(0);
						bestTargetHiHorizontalLine.Y2 = pixel2;
						bestTargetHiHorizontalLine.Stroke = Brushes.Lime;
						bestTargetHiHorizontalLine.StrokeEndLineCap = PenLineCap.Flat;
						bestTargetHiHorizontalLine.StrokeThickness = 0.5;
						bestTargetHiHorizontalLine.ToolTip = "Highest Analysts Target Px";
						_panels[0].Canvas.Children.Add(bestTargetHiHorizontalLine);

						TextBlock bestTargetHiText = new TextBlock();
						bestTargetHiText.Text = valueText;
						bestTargetHiText.Height = rect2.Height;
						bestTargetHiText.Width = rect2.Width;
						bestTargetHiText.SetValue(Canvas.LeftProperty, rect2.Left);
						bestTargetHiText.SetValue(Canvas.TopProperty, rect2.Top);
						bestTargetHiText.TextAlignment = TextAlignment.Right;
						bestTargetHiText.FontSize = _fontSize;
						bestTargetHiText.Background = Brushes.Black;
						bestTargetHiText.Foreground = Brushes.Lime;
						bestTargetHiText.FontWeight = FontWeights.Light;
						bestTargetHiText.FontFamily = new FontFamily("Arial");
						bestTargetHiText.ToolTip = "Highest Analysts Target Px";

						_canvas.Children.Add(bestTargetHiText);
					}
				}
			}
		}

		private void drawBestTargetPrice()
		{
			if (!double.IsNaN(_bestTargetPrice))
			{
				double pixel1 = getTimePixel(_currentIndex);
				double pixel2 = getValuePixel(0, _bestTargetPrice);

				if (!double.IsNaN(pixel1) && !double.IsNaN(pixel2))
				{
					int fontHeight = 14;
					double top = getPanelTop(0);
					double y2 = getPanelBottom(0);

					double v1 = pixel2;
					string valueText = _bestTargetPrice.ToString("F" + 2);

					double x = _canvas.ActualWidth - _valueScaleWidth;
					double y = v1 - fontHeight / 2;

					if (y >= 0 && y + (fontHeight / 2) <= y2)
					{
						Rect rect2 = new Rect(Math.Max(0, x), top + y, _valueScaleWidth - 2, fontHeight);

						Line bestTargetHorizontalLine = new Line();
						bestTargetHorizontalLine.X1 = pixel1;
						bestTargetHorizontalLine.Y1 = pixel2;
						bestTargetHorizontalLine.X2 = getPanelRight(0);
						bestTargetHorizontalLine.Y2 = pixel2;
						bestTargetHorizontalLine.Stroke = Brushes.DarkOrange;
						bestTargetHorizontalLine.StrokeEndLineCap = PenLineCap.Flat;
						bestTargetHorizontalLine.StrokeThickness = 0.5;
						bestTargetHorizontalLine.ToolTip = "Avg Analysts Target Px";
						_panels[0].Canvas.Children.Add(bestTargetHorizontalLine);

						TextBlock bestTargetPriceText = new TextBlock();
						bestTargetPriceText.Text = valueText;
						bestTargetPriceText.Height = rect2.Height;
						bestTargetPriceText.Width = rect2.Width;
						bestTargetPriceText.SetValue(Canvas.LeftProperty, rect2.Left);
						bestTargetPriceText.SetValue(Canvas.TopProperty, rect2.Top);
						bestTargetPriceText.TextAlignment = TextAlignment.Right;
						bestTargetPriceText.FontSize = _fontSize;
						bestTargetPriceText.Background = Brushes.Black;
						bestTargetPriceText.Foreground = Brushes.DarkOrange;
						bestTargetPriceText.FontWeight = FontWeights.Light;
						bestTargetPriceText.FontFamily = new FontFamily("Arial");
						bestTargetPriceText.ToolTip = "Avg Analysts Target Px";

						_canvas.Children.Add(bestTargetPriceText);
					}
				}
			}
		}

		private void drawBestTargetLo()
		{
			if (!double.IsNaN(_bestTargetLo))
			{
				double pixel1 = getTimePixel(_currentIndex);
				double pixel2 = getValuePixel(0, _bestTargetLo);

				if (!double.IsNaN(pixel1) && !double.IsNaN(pixel2))
				{
					int fontHeight = 14;
					double top = getPanelTop(0);
					double y2 = getPanelBottom(0);

					double v1 = pixel2;
					string valueText = _bestTargetLo.ToString("F" + 2);

					double x = _canvas.ActualWidth - _valueScaleWidth;
					double y = v1 - fontHeight / 2;

					if (y >= 0 && y + (fontHeight / 2) <= y2)
					{
						Rect rect2 = new Rect(Math.Max(0, x), top + y, _valueScaleWidth - 2, fontHeight);

						Line bestTargetLoHorizontalLine = new Line();
						bestTargetLoHorizontalLine.X1 = pixel1;
						bestTargetLoHorizontalLine.Y1 = pixel2;
						bestTargetLoHorizontalLine.X2 = getPanelRight(0);
						bestTargetLoHorizontalLine.Y2 = pixel2;
						bestTargetLoHorizontalLine.Stroke = Brushes.Red;
						bestTargetLoHorizontalLine.StrokeEndLineCap = PenLineCap.Flat;
						bestTargetLoHorizontalLine.StrokeThickness = 0.5;
						bestTargetLoHorizontalLine.ToolTip = "Lowest Analysts Target Px";
						_panels[0].Canvas.Children.Add(bestTargetLoHorizontalLine);

						TextBlock bestTargetLoText = new TextBlock();
						bestTargetLoText.Text = valueText;
						bestTargetLoText.Height = rect2.Height;
						bestTargetLoText.Width = rect2.Width;
						bestTargetLoText.SetValue(Canvas.LeftProperty, rect2.Left);
						bestTargetLoText.SetValue(Canvas.TopProperty, rect2.Top);
						bestTargetLoText.TextAlignment = TextAlignment.Right;
						bestTargetLoText.FontSize = _fontSize;
						bestTargetLoText.Background = Brushes.Black;
						bestTargetLoText.Foreground = Brushes.Red;
						bestTargetLoText.FontWeight = FontWeights.Light;
						bestTargetLoText.FontFamily = new FontFamily("Arial");
						bestTargetLoText.ToolTip = "Lowest Analysts Target Px";

						_canvas.Children.Add(bestTargetLoText);
					}
				}
			}
		}

		private void addCursorLine()
		{
			bool ft1 = getIndicatorEnable("Use ATM FT 1 Time Frame Higher");
			bool ft2 = getIndicatorEnable("Use ATM FT 2 Time Frames Higher");
			bool st1 = getIndicatorEnable("Use ATM ST 1 Time Frame Higher");
			bool st2 = getIndicatorEnable("Use ATM ST 2 Time Frames Higher");

			bool conditionColor = (ft1 || ft2 || st1 || st2);

			removeCursorLine();

			if (ShowCursor && isTimeValid(_cursorTime))
			{
				if (conditionColor)
				{
					_cursorVerticalLine2 = new Line();
				}
				_cursorVerticalLine = new Line();
				_cursorHorizontalLine = new Line();

				int index1 = getTimeIndex(_cursorTime);
				double pixel1 = getTimePixel(index1);
				if (!double.IsNaN(pixel1) && index1 >= _startIndex && index1 < _endIndex)
				{
					if (conditionColor)
					{
						double thickness = Math.Max(_timeScale / 3, 1);
						Color leftColor = (index1 >= 0 && index1 < _leftSideCursorColor.Count) ? _leftSideCursorColor[index1] : Color.FromArgb(0x65, 0x80, 0x80, 0x80);
						Color rightColor = (index1 >= 0 && index1 < _rightSideCursorColor.Count) ? _rightSideCursorColor[index1] : Color.FromArgb(0x65, 0x80, 0x80, 0x80);
						_cursorVerticalLine.X1 = pixel1 - thickness;
						_cursorVerticalLine.Y1 = getPanelTop(0);
						_cursorVerticalLine.X2 = pixel1 - thickness;
						_cursorVerticalLine.Y2 = getPanelBottom(getPanelCount() - 1);
						_cursorVerticalLine.Stroke = new SolidColorBrush(leftColor); // new SolidColorBrush(Color.FromArgb(0x40, _foregroundColor.R, _foregroundColor.G, _foregroundColor.B));
						_cursorVerticalLine.StrokeEndLineCap = PenLineCap.Flat;
						_cursorVerticalLine.StrokeThickness = thickness;
						_cursorVerticalLine.IsHitTestVisible = false;
						_canvas.Children.Add(_cursorVerticalLine);

						_cursorVerticalLine2.X1 = pixel1 + thickness;
						_cursorVerticalLine2.Y1 = getPanelTop(0);
						_cursorVerticalLine2.X2 = pixel1 + thickness;
						_cursorVerticalLine2.Y2 = getPanelBottom(getPanelCount() - 1);
						_cursorVerticalLine2.Stroke = new SolidColorBrush(rightColor); // new SolidColorBrush(Color.FromArgb(0x40, _foregroundColor.R, _foregroundColor.G, _foregroundColor.B));
						_cursorVerticalLine2.StrokeEndLineCap = PenLineCap.Flat;
						_cursorVerticalLine2.StrokeThickness = thickness;
						_cursorVerticalLine2.IsHitTestVisible = false;
						_canvas.Children.Add(_cursorVerticalLine2);
					}
					else
					{
						_cursorVerticalLine.X1 = pixel1;
						_cursorVerticalLine.Y1 = getPanelTop(0);
						_cursorVerticalLine.X2 = pixel1;
						_cursorVerticalLine.Y2 = getPanelBottom(getPanelCount() - 1);
						_cursorVerticalLine.Stroke = new SolidColorBrush(_study.getCursorColor(_portfolioName, _symbol, _interval, _cursorTime)); // new SolidColorBrush(Color.FromArgb(0x40, _foregroundColor.R, _foregroundColor.G, _foregroundColor.B));
						_cursorVerticalLine.StrokeEndLineCap = PenLineCap.Flat;
						_cursorVerticalLine.StrokeThickness = Math.Max(_timeScale / 2, 1);
						_cursorVerticalLine.IsHitTestVisible = false;
						_canvas.Children.Add(_cursorVerticalLine);
					}

					string timeText = ((isHistoricalInterval()) ? _cursorTime : _cursorTime.ToLocalTime()).ToString(getCursorTextFormat());
					int panelCount = _panels.Count;
					double timeScaleTop = getPanelBottom(panelCount - 1);
					double timeScaleBottom = _canvas.ActualHeight;

					_cursorVerticalText = new TextBlock();
					_cursorVerticalText.Text = timeText;
					_cursorVerticalText.Height = timeScaleBottom - timeScaleTop - 2;
					_cursorVerticalText.SetValue(Canvas.LeftProperty, pixel1 - 10);
					_cursorVerticalText.SetValue(Canvas.TopProperty, timeScaleTop);
					_cursorVerticalText.TextAlignment = TextAlignment.Center;
					_cursorVerticalText.FontSize = _fontSize;
					_cursorVerticalText.Background = Brushes.Black;
					_cursorVerticalText.Foreground = Brushes.DeepSkyBlue;
					_cursorVerticalText.FontWeight = FontWeights.Light;
					_cursorVerticalText.FontFamily = new FontFamily("Arial");
					_cursorVerticalText.IsEnabled = false;
					_canvas.Children.Add(_cursorVerticalText);

					//_cursorInfo.Visible = true;
				}

				if (!double.IsNaN(_cursorValue))
				{
					double pixel2 = getValuePixel(0, _cursorValue);
					if (!double.IsNaN(pixel2))
					{
						_cursorHorizontalLine.X1 = getPanelLeft(0);
						_cursorHorizontalLine.Y1 = pixel2;
						_cursorHorizontalLine.X2 = getPanelRight(0);
						_cursorHorizontalLine.Y2 = pixel2;
						_cursorHorizontalLine.Stroke = new SolidColorBrush(Color.FromArgb(0x40, _foregroundColor.R, _foregroundColor.G, _foregroundColor.B));
						_cursorHorizontalLine.StrokeEndLineCap = PenLineCap.Flat;
						_cursorHorizontalLine.StrokeThickness = .1;
						_cursorHorizontalLine.IsHitTestVisible = false;
						_panels[0].Canvas.Children.Add(_cursorHorizontalLine);

						int fontHeight = 14;
						double top = getPanelTop(0);
						double y2 = getPanelBottom(0);

						double v1 = pixel2;

						string valueText = getValueText(_cursorValue);

						double x = _canvas.ActualWidth - _valueScaleWidth;
						double y = v1 - fontHeight / 2;

						if (y >= 0 && y + (fontHeight / 2) <= y2)
						{
							Rect rect2 = new Rect(Math.Max(0, x), top + y, _valueScaleWidth - 2, fontHeight);

							_cursorHorizontalText = new TextBlock();
							_cursorHorizontalText.Text = valueText;
							_cursorHorizontalText.Height = rect2.Height;
							_cursorHorizontalText.Width = rect2.Width;
							_cursorHorizontalText.SetValue(Canvas.LeftProperty, rect2.Left);
							_cursorHorizontalText.SetValue(Canvas.TopProperty, rect2.Top);
							_cursorHorizontalText.TextAlignment = TextAlignment.Right;
							_cursorHorizontalText.FontSize = _fontSize;
							_cursorHorizontalText.Background = Brushes.Black;
							_cursorHorizontalText.Foreground = Brushes.DeepSkyBlue;
							_cursorHorizontalText.FontWeight = FontWeights.Light;
							_cursorHorizontalText.FontFamily = new FontFamily("Arial");
							_cursorHorizontalText.IsEnabled = false;

							_canvas.Children.Add(_cursorHorizontalText);
						}
					}
					else
					{
						_canvas.Children.Remove(_cursorHorizontalText);
						_cursorHorizontalText = null;
					}
				}
				else
				{
					_panels[0].Canvas.Children.Remove(_cursorHorizontalLine);
					_canvas.Children.Remove(_cursorHorizontalText);
					_cursorHorizontalLine = null;
					_cursorHorizontalText = null;
				}
			}
		}

		void removeCursorLine()
		{
			if (_cursorVerticalLine != null)
			{
				if (_cursorVerticalLine2 != null)
				{
					_canvas.Children.Remove(_cursorVerticalLine2);
					_cursorVerticalLine2 = null;
				}
				_canvas.Children.Remove(_cursorVerticalLine);
				_canvas.Children.Remove(_cursorVerticalText);
				_cursorVerticalLine = null;
				//_cursorInfo.Visible = false;
			}
			if (_cursorHorizontalLine != null)
			{
				_panels[0].Canvas.Children.Remove(_cursorHorizontalLine);
				_canvas.Children.Remove(_cursorHorizontalText);
				_cursorHorizontalLine = null;
				_cursorHorizontalText = null;
			}
		}

		void setCursorVisibility()
		{
			if (_cursorVerticalLine != null)
			{
				double pixel = _cursorVerticalLine.X1;
				if (pixel >= getPanelLeft(0) && pixel <= getPanelRight(0))
				{
					_cursorVerticalLine.Visibility = Visibility.Visible;
				}
				else
				{
					_cursorVerticalLine.Visibility = Visibility.Collapsed;
				}
			}
		}

		void drawLine(Point p1, Point p2, Color color, double thickness)
		{
			Line line = new Line();
			line.X1 = p1.X;
			line.Y1 = p1.Y;
			line.X2 = p2.X;
			line.Y2 = p2.Y;
			line.Stroke = new SolidColorBrush(color);
			line.StrokeEndLineCap = PenLineCap.Flat;
			line.StrokeThickness = thickness;
			_canvas.Children.Add(line);
		}

		private bool isTimeValid(DateTime time)
		{
			return (time.Year > 1);
		}

		class TimeScaleInfo
		{
			public TimeScaleInfo()
			{
				m_time = new DateTime();
				m_pixel = 0;
				m_level = 0;
				m_type = 0;
			}
			public DateTime m_time;
			public double m_pixel;
			public int m_level; // 0 = nothing, 1 = major grid and label, 2 = minor grid
			public int m_type;  // 0 = no label, 1 = MM/dd-HH:mm, 2 = MM/dd, 3 = yyyy/MM, 4 = yyyy
		};

		private IList<TimeScaleInfo> getTimeScaleInfo()
		{
			lock (_barLock)
			{
				int endIndex = Math.Max(Math.Min(_bars.Count, _endIndex), 0);
				int cnt = Math.Max(0, endIndex - _startIndex);

				IList<TimeScaleInfo> info = new List<TimeScaleInfo>(cnt);

				if (_bars.Count > 0)
				{
					if (cnt > 0)
					{
						if (cnt > 0)
						{
							for (int ii = 0; ii < cnt; ii++)
							{
								info.Add(new TimeScaleInfo());
							}

							bool useLocalTime = (isIntervalIntraday()) ? true : false;

							bool found = false;

							DateTime firstPrvTime = new DateTime();

							int textWidth1 = 80;
							int textWidth2 = 60;
							int textWidth3 = 40;
							int textWidth4 = 20;

							// MM/dd-HH:mm
							if (isIntervalHalfHourOrLess())
							{
								int[] interval1 = { 5, 10, 15, 20, 30, 60, 120, 240 };
								int[] interval2 = { 1, 5, 5, 5, 10, 15, 30, 30 };
								int collisionDistance1 = textWidth1 + 8;
								int collisionDistance2 = textWidth1 + 12;
								for (int ii = 0; ii < 8 && !found; ii++)
								{
									DateTime prvTime = firstPrvTime;
									double prvPixel = double.NaN;
									bool prvDayChange = false;
									for (int idx = 0; idx < cnt; idx++)
									{
										info[idx].m_type = 0;
										info[idx].m_level = 0;
										DateTime time = _bars[_startIndex + idx].Time;
										double pixel = getTimePixel(_startIndex + idx);
										info[idx].m_pixel = pixel;
										if (isTimeValid(time) && isTimeValid(prvTime))
										{
											time = time.ToLocalTime();
											info[idx].m_time = time;
											int minute = time.Minute + 60 * time.Hour;
											int prvMinute = prvTime.Minute + 60 * prvTime.Hour;
											if (time.Day != prvTime.Day || (minute % interval1[ii]) <= (prvMinute % interval1[ii]))
											{
												found = true;
												if (!(prvDayChange && Math.Abs(pixel - prvPixel) < collisionDistance1))
												{
													info[idx].m_type = 1;
													info[idx].m_level = 1;
													if (!double.IsNaN(prvPixel))
													{
														if (Math.Abs(pixel - prvPixel) < collisionDistance2)
														{
															found = false;
															break;
														}
													}
													prvPixel = pixel;
												}
												prvDayChange = (time.Day != prvTime.Day);
											}
											if (info[idx].m_level == 0 && (minute % interval2[ii]) <= (prvMinute % interval2[ii]))
											{
												info[idx].m_level = 2;
											}
										}
										prvTime = time;
									}
								}
							}

							// MM/dd every day
							if (!found)
							{
								DateTime prvTime = firstPrvTime;
								double prvPixel = double.NaN;
								int collisionDistance = textWidth2 + 12;
								for (int idx = 0; idx < cnt; idx++)
								{
									info[idx].m_type = 0;
									info[idx].m_level = 0;
									DateTime time = _bars[_startIndex + idx].Time;
									double pixel = getTimePixel(_startIndex + idx);
									info[idx].m_pixel = pixel;
									if (isTimeValid(time) && isTimeValid(prvTime))
									{
										if (useLocalTime)
										{
											time = time.ToLocalTime();
										}
										info[idx].m_time = time;
										if (prvTime.Day != time.Day)
										{
											found = true;
											info[idx].m_type = 2;
											info[idx].m_level = 1;
											if (!double.IsNaN(prvPixel))
											{
												if (Math.Abs(pixel - prvPixel) < collisionDistance)
												{
													found = false;
													break;
												}
											}
											prvPixel = pixel;
										}
										int minute = time.Minute + 60 * time.Hour;
										if (info[idx].m_level == 0 && minute % 60 == 0)
										{
											info[idx].m_level = 2;
										}
									}
									prvTime = time;
								}
							}

							// MM/dd every week
							if (!found)
							{
								int[] interval3 = { 1, 2, 3 };
								DateTime prvTime = firstPrvTime;
								double prvPixel = double.NaN;
								int collisionDistance = textWidth2 + 12;
								for (int idx = 0; idx < cnt; idx++)
								{
									info[idx].m_type = 0;
									info[idx].m_level = 0;
									DateTime time = _bars[_startIndex + idx].Time;
									double pixel = getTimePixel(_startIndex + idx);
									info[idx].m_pixel = pixel;
									if (isTimeValid(time) && isTimeValid(prvTime))
									{
										if (useLocalTime)
										{
											time = time.ToLocalTime();
										}
										info[idx].m_time = time;
										if (prvTime.DayOfWeek > time.DayOfWeek)
										{
											found = true;
											info[idx].m_type = 2;
											info[idx].m_level = 1;
											if (!double.IsNaN(prvPixel))
											{
												if (Math.Abs(pixel - prvPixel) < collisionDistance)
												{
													found = false;
													break;
												}
											}
											prvPixel = pixel;
										}
										if (info[idx].m_level == 0 && prvTime.Day != time.Day)
										{
											info[idx].m_level = 2;
										}
									}
									prvTime = time;
								}
							}

							// yyyy/MM
							if (!found)
							{
								int[] interval3 = { 1, 2, 3 };
								int collisionDistance = textWidth3 + 12;
								for (int ii = 0; ii < 3 && !found; ii++)
								{
									DateTime prvTime = firstPrvTime;
									double prvPixel = double.NaN;
									for (int idx = 0; idx < cnt; idx++)
									{
										info[idx].m_type = 0;
										info[idx].m_level = 0;
										DateTime time = _bars[_startIndex + idx].Time;
										double pixel = getTimePixel(_startIndex + idx);
										info[idx].m_pixel = pixel;
										if (isTimeValid(time) && isTimeValid(prvTime))
										{
											if (useLocalTime)
											{
												time = time.ToLocalTime();
											}
											info[idx].m_time = time;
											if (prvTime.Month != time.Month)
											{
												int month = time.Month - 1;
												if (month % interval3[ii] == 0)
												{
													found = true;
													info[idx].m_type = 3;
													info[idx].m_level = 1;
													if (!double.IsNaN(prvPixel))
													{
														if (Math.Abs(pixel - prvPixel) < collisionDistance)
														{
															found = false;
															break;
														}
													}
													prvPixel = pixel;
												}
											}
											if (info[idx].m_level == 0 && ((ii == 0 && prvTime.DayOfWeek > time.DayOfWeek) || (ii > 0 && prvTime.Month != time.Month)))
											{
												info[idx].m_level = 2;
											}
										}
										prvTime = time;
									}
								}
							}

							// yyyy
							if (!found)
							{
								int[] interval4 = { 1, 2, 4, 5, 10, 20, 25, 50, 100 };
								int collisionDistance = textWidth4 + 12;
								for (int ii = 0; ii < 9 && !found; ii++)
								{
									DateTime prvTime = firstPrvTime;
									double prvPixel = double.NaN;
									for (int idx = 0; idx < cnt; idx++)
									{
										info[idx].m_type = 0;
										info[idx].m_level = 0;
										DateTime time = _bars[_startIndex + idx].Time;
										double pixel = getTimePixel(_startIndex + idx);
										info[idx].m_pixel = pixel;
										if (isTimeValid(time) && isTimeValid(prvTime))
										{
											if (useLocalTime)
											{
												time = time.ToLocalTime();
											}
											info[idx].m_time = time;
											if (prvTime.Year != time.Year)
											{
												int year = time.Year;
												if (year % interval4[ii] == 0)
												{
													found = true;
													info[idx].m_type = 4;
													info[idx].m_level = 1;
													if (!double.IsNaN(prvPixel))
													{
														if (Math.Abs(pixel - prvPixel) < collisionDistance)
														{
															found = false;
															break;
														}
													}
													prvPixel = pixel;
												}
											}
											if (info[idx].m_level == 0 && prvTime.Month != time.Month)
											{
												info[idx].m_level = 2;
											}
										}
										prvTime = time;
									}
								}
							}
						}
					}
				}
				return info;
			}
		}

		class ValueScaleInfo
		{
			public ValueScaleInfo()
			{
				m_pixel = 0;
				m_level = 0;
				m_value = double.NaN;
				m_scale = 1;
				m_digits = 2;
				m_label = ' ';
			}
			public double m_pixel;
			public int m_level; // 0 = nothing, 1 = major grid and label, 2 = major grid, 3 = minor grid
			public double m_value;
			public double m_scale;
			public int m_digits;
			public char m_label;
		};

		private IList<ValueScaleInfo> getValueScaleInfo(int panelNumber)
		{
			IList<ValueScaleInfo> info = new List<ValueScaleInfo>();

			double valueScale = _panels[panelNumber].ValueScale;
			double valueBase = _panels[panelNumber].ValueBase;

			double height = getPanelHeight(panelNumber);

			bool validScale = (!double.IsNaN(valueScale) && !double.IsNaN(valueBase));
			if (validScale && height > 0)
			{
				int fontHeight = 14;

				double maxValue = getValue(panelNumber, 0);
				double minValue = getValue(panelNumber, height);

				bool binaryScale = false; //m_displayInfo.IsBinaryScale();
				double tickSize = 0.01; //m_displayInfo.m_displayTickSize;
				if (panelNumber == 0)
				{
					if (_symbol == "F.USA")
					{
						binaryScale = true;
						tickSize = 1.0 / 32.0;
					}
					else if (_symbol == "F.TYA")
					{
						binaryScale = true;
						tickSize = 1.0 / 64.0;
					}
					else if (_symbol == "F.JY" || _symbol == "F.JYA")
					{
						tickSize = 0.0000001;
					}
					else if (_symbol == "F.BP" || _symbol == "F.BPA")
					{
						tickSize = 0.0001;
					}
					else if (_symbol == "F.CA" || _symbol == "F.CAA" || _symbol == "F.EU" || _symbol == "F.EUA" || _symbol == "F.DA" || _symbol == "F.DAA" || _symbol == "F.SF" || _symbol == "F.SFA")
					{
						tickSize = 0.00001;
					}
				}

				bool reverse = (valueScale < 0) ? true : false;
				double round = 1.0;
				if (binaryScale)
				{
					round = 1.0 / 32;  // todo  Price conversion 
				}
				else
				{
					int logBaseValue = (int)((minValue != 0.0) ? Math.Log10(Math.Abs(minValue)) - 1.5 : 0);
					if (logBaseValue > 0)
					{
						logBaseValue = 0;
					}
					round = Math.Pow(10.0, logBaseValue);
				}
				double delta = maxValue - minValue;
				delta = Math.Abs(delta);

				double maxCount = Math.Max(4, height / (3 * fontHeight));
				double minCount = 4;

				double inc = 1.0;
				if (binaryScale)
				{
					bool found = false;
					double prvInc = 1.0 / 2048;
					for (inc = 1.0 / 1024; inc < 1e20 && !found; inc *= 2)
					{
						double count = Math.Abs(delta) / inc;
						if (count < minCount)
						{
							inc = prvInc;
							found = true;
							break;
						}
						if (count < maxCount)
						{
							found = true;
							break;
						}
						prvInc = inc;
					}
				}
				else
				{
					bool found = false;
					inc = 0.01;
					double[] inc2 = { 0.010, 0.020, 0.025, 0.050 };
					for (double inc1 = 0.0000001; inc1 < 1e20 && !found; inc1 *= 10)
					{
						for (int ii = 0; ii < 4; ii++)
						{
							if (!(inc1 == 1.0 && inc2[ii] == 0.025))
							{
								double prvInc = inc;
								inc = inc1 * inc2[ii];
								double count = Math.Abs(delta) / inc;
								if (count < minCount)
								{
									inc = prvInc;
									found = true;
									break;
								}
								if (count < maxCount)
								{
									found = true;
									break;
								}
							}
						}
					}
				}

				double scale = 1.0;
				char label = ' ';
				int digits = 2;

				if (inc > 1e15)
				{
					scale = 0;
					digits = 0;
					label = ' ';
				}
				else if (inc >= 2e11)
				{
					scale = 1e12;
					digits = ((inc % 1e12) == 0) ? 0 : ((inc % 1e11) == 0) ? 1 : 2;
					label = 'T';
				}
				else if (inc >= 2e8)
				{
					scale = 1e9;
					digits = ((inc % 1e9) == 0) ? 0 : ((inc % 1e8) == 0) ? 1 : 2;
					label = 'B';
				}
				else if (inc >= 2e5)
				{
					scale = 1e6;
					digits = ((inc % 1e6) == 0) ? 0 : ((inc % 1e5) == 0) ? 1 : 2;
					label = 'M';
				}
				else if (inc >= 2e3)
				{
					scale = 1e3;
					digits = ((inc % 1e3) == 0) ? 0 : ((inc % 1e2) == 0) ? 1 : 2;
					label = 'K';
				}
				else
				{
					digits = (int)(-Math.Log10(inc) + 1.5);
					if (digits < 0)
					{
						digits = 0;
					}
				}

				if (inc < tickSize)
				{
					inc = tickSize;
				}

				if (reverse)
				{
					inc = -inc;
				}

				double value = reverse ? maxValue : minValue;
				value -= ((value % inc) + 2 * inc);

				inc *= (binaryScale ? 0.125 : 0.1);
				for (int counter = 0; (reverse ? value > maxValue : value < maxValue) && counter < 1000;)
				{
					for (int ii = 0; ii < (binaryScale ? 8 : 10) && counter < 1000; ii++, counter++)
					{
						double pixel = getValuePixel(panelNumber, value);
						if (pixel >= 0 && pixel < height)
						{
							ValueScaleInfo vsi = new ValueScaleInfo();
							int level = (ii == 0) ? 1 : ((ii == (binaryScale ? 4 : 5)) ? 2 : 3);
							vsi.m_level = level;
							vsi.m_value = value;
							vsi.m_scale = scale;
							vsi.m_digits = digits;
							vsi.m_label = label;
							vsi.m_pixel = pixel;
							info.Add(vsi);
						}
						value += inc;
					}
				}
			}
			return info;
		}

		private void initialize()
		{
			_symbolEditor.Text = _symbolName.Replace("|", " | ");

			_startIndex = getInitialStartIndex();
			_endIndex = getInitialEndIndex();

			if (_startIndex < 0) _startIndex = 0;
			if (_endIndex < 0) _endIndex = 0;

			int panelCount = _panels.Count;
			for (int panelNumber = 0; panelNumber < panelCount; panelNumber++)
			{
				_panels[panelNumber].AutoScale = true;
			}
			_update = true;
		}
	}

	public enum Placement
	{
		Above,
		Below,
		Value
	}

	public class ChartAnnotation
	{
		private int _id;
		protected DateTime _dateTime;
		protected Color _color;
		private Placement _placement;
		private double _value;
		private double _offset = 0;
		private double _width = 1;
		private double _height = 1;
		protected string _tooltip = "";

		public int Id
		{
			get { return _id; }
			set { _id = value; }
		}

		public DateTime DateTime
		{
			get { return _dateTime; }
			set { _dateTime = value; }
		}

		public Color Color
		{
			get { return _color; }
			set { _color = value; }
		}

		public Placement Placement
		{
			get { return _placement; }
			set { _placement = value; }
		}

		public double Value
		{
			get { return _value; }
			set { _value = value; }
		}

		public double Offset
		{
			get { return _offset; }
			set { _offset = value; }
		}

		public double Width
		{
			get { return _width; }
			set { _width = value; }
		}

		public double Height
		{
			get { return _height; }
			set { _height = value; }
		}

		public string Tooltip
		{
			get { return _tooltip; }
			set { _tooltip = value; }
		}

		public virtual void Draw(Canvas canvas, Rect rect)
		{
		}
	}

	public class LineAnnotation : ChartAnnotation
	{
		public override void Draw(Canvas canvas, Rect rect)
		{
			double x = (rect.Left + rect.Right) / 2;
			Line line = new Line();
			line.X1 = x;
			line.Y1 = rect.Top;
			line.X2 = x;
			line.Y2 = rect.Bottom;
			line.Stroke = new SolidColorBrush(_color);
			line.StrokeEndLineCap = PenLineCap.Flat;
			line.StrokeThickness = rect.Right - rect.Left;
			canvas.Children.Add(line);
		}
	}

	public class TextAnnotation : ChartAnnotation
	{
		protected string _text;

		public string Text
		{
			get { return _text; }
			set { _text = value; }
		}

		public override void Draw(Canvas canvas, Rect rect)
		{
			TextBlock textBlock = new TextBlock();
			textBlock.Text = _text;
			var fields = _text.Split('\n');
			var rows = fields.Length;
			textBlock.Height = rows * rect.Height;
			textBlock.Width = rect.Width;
			textBlock.SetValue(Canvas.LeftProperty, rect.Left);
			textBlock.SetValue(Canvas.TopProperty, rect.Top);
			textBlock.TextAlignment = TextAlignment.Left;
			textBlock.FontSize = 1.25 * rect.Width;
			textBlock.FontWeight = FontWeights.Light;
			textBlock.FontFamily = new FontFamily("Arial");
			textBlock.Foreground = new SolidColorBrush(_color);
			if (_tooltip.Length > 0)
			{
				textBlock.ToolTip = _tooltip;
			}

			canvas.Children.Add(textBlock);
		}
	}
	public class MultiColorTextAnnotation : TextAnnotation
	{
		private List<Color> _colors = new List<Color>();

		public List<Color> Colors { get { return _colors; } }

		public override void Draw(Canvas canvas, Rect rect)
		{
			var rows = _text.Split('\n');
			var rowCnt = rows.Length;

			var panel1 = new StackPanel();
			panel1.Orientation = Orientation.Vertical;
			panel1.SetValue(Canvas.LeftProperty, rect.Left);
			panel1.SetValue(Canvas.TopProperty, rect.Top);
			panel1.Width = rect.Width;

			for (int ii = 0; ii < rowCnt; ii++)
			{
				var panel2 = new StackPanel();
				panel2.Orientation = Orientation.Horizontal;

				Color color = _colors[0];
				string text = "";
				bool newTextBlock;
				bool newColor;
				for (int jj = 0; jj < rows[ii].Length; jj++)
				{
					newTextBlock = jj == rows[ii].Length - 1;

					if (rows[ii][jj] < 10)
					{
						newColor = true;
						newTextBlock = text.Length > 0;
					}
					else
					{
						newColor = false;
						text += rows[ii][jj];
					}

					if (newTextBlock)
					{
						TextBlock textBlock = new TextBlock();
						textBlock.Text = text;
						textBlock.Height = rect.Height;
						textBlock.TextAlignment = TextAlignment.Left;
						textBlock.FontSize = 0.9 * rect.Height;
						textBlock.FontWeight = FontWeights.Light;
						textBlock.FontFamily = new FontFamily("Arial");
						textBlock.Foreground = new SolidColorBrush(color);
						if (_tooltip.Length > 0)
						{
							textBlock.ToolTip = _tooltip;
						}
						panel2.Children.Add(textBlock);
						text = "";
					}

					if (newColor)
					{
						color = _colors[(int)rows[ii][jj] - 1];
					}
				}
				panel1.Children.Add(panel2);
			}
			canvas.Children.Add(panel1);
		}
	}

	public enum SymbolType
	{
		None = 0,
		UpArrow = 1,
		DownArrow = 2,
		Dash = 3,
		Circle = 4,
		Square = 5,
		Diamond = 6,
		X = 7,
		UP = 8,
		DN = 9
	};

	public class SymbolAnnotation : ChartAnnotation
	{
		private SymbolType _symbolType = SymbolType.DownArrow;

		public SymbolType SymbolType
		{
			get { return _symbolType; }
			set { _symbolType = value; }
		}

		public override void Draw(Canvas canvas, Rect rect)
		{
			double x = rect.X;
			double y = rect.Y;

			if (SymbolType == SymbolType.UpArrow)
			{
				Polygon polygon = new Polygon();
				double size1 = rect.Width;
				double size2 = rect.Width / 2;
				x += size2;
				PointCollection pt = new PointCollection();
				pt.Add(new Point(x, y + size1 / 8));
				pt.Add(new Point(x + 0.75 * size2, y + 1.00 * size2));
				pt.Add(new Point(x + 0.25 * size2, y + 1.00 * size2));
				pt.Add(new Point(x + 0.25 * size2, y + 2.00 * size2));
				pt.Add(new Point(x - 0.25 * size2, y + 2.00 * size2));
				pt.Add(new Point(x - 0.25 * size2, y + 1.00 * size2));
				pt.Add(new Point(x - 0.75 * size2, y + 1.00 * size2));
				polygon.Points = pt;
				polygon.StrokeThickness = 1;
				polygon.Stroke = new SolidColorBrush(_color);
				polygon.Fill = new SolidColorBrush(_color);
				polygon.ToolTip = _tooltip;
				canvas.Children.Add(polygon);
			}
			else if (SymbolType == SymbolType.DownArrow)
			{
				Polygon polygon = new Polygon();
				double size1 = rect.Width;
				double size2 = rect.Width / 2;
				x += size2;
				y += size1;
				PointCollection pt = new PointCollection();
				pt.Add(new Point(x, y - size1 / 8));
				pt.Add(new Point(x + 0.75 * size2, y - 1.00 * size2));
				pt.Add(new Point(x + 0.25 * size2, y - 1.00 * size2));
				pt.Add(new Point(x + 0.25 * size2, y - 2.00 * size2));
				pt.Add(new Point(x - 0.25 * size2, y - 2.00 * size2));
				pt.Add(new Point(x - 0.25 * size2, y - 1.00 * size2));
				pt.Add(new Point(x - 0.75 * size2, y - 1.00 * size2));
				polygon.Points = pt;
				polygon.StrokeThickness = 1;
				polygon.Stroke = new SolidColorBrush(_color);
				polygon.Fill = new SolidColorBrush(_color);
				polygon.ToolTip = _tooltip;
				canvas.Children.Add(polygon);
			}
			else if (SymbolType == SymbolType.Dash)
			{
				Polygon polygon = new Polygon();
				PointCollection pt = new PointCollection();
				pt.Add(new Point(x, y));
				pt.Add(new Point(x + rect.Width, y));
				pt.Add(new Point(x + rect.Width, y + rect.Height));
				pt.Add(new Point(x, y + rect.Height));
				polygon.Points = pt;
				polygon.StrokeThickness = 1;
				polygon.Stroke = new SolidColorBrush(_color);
				polygon.Fill = new SolidColorBrush(_color);
				polygon.ToolTip = _tooltip;
				canvas.Children.Add(polygon);
			}
			else if (SymbolType == SymbolType.Circle)
			{
				Ellipse ellipse = new Ellipse();
				double width = 0.85 * rect.Width;
				double height = 1.00 * rect.Height;
				x += ((rect.Width - width) / 2);
				ellipse.Width = width;
				ellipse.Height = height;
				Canvas.SetLeft(ellipse, x);
				Canvas.SetTop(ellipse, y);
				ellipse.StrokeThickness = 1;
				ellipse.Stroke = new SolidColorBrush(_color);
				ellipse.Fill = new SolidColorBrush(_color);
				ellipse.ToolTip = _tooltip;
				canvas.Children.Add(ellipse);
			}
			else if (SymbolType == SymbolType.Square)
			{
				Polygon polygon = new Polygon();
				double width = 0.65 * rect.Width;
				double height = 0.80 * rect.Height;
				x += ((rect.Width - width) / 2);
				PointCollection pt = new PointCollection();
				pt.Add(new Point(x, y));
				pt.Add(new Point(x + width, y));
				pt.Add(new Point(x + width, y + height));
				pt.Add(new Point(x, y + height));
				polygon.Points = pt;
				polygon.StrokeThickness = 1;
				polygon.Stroke = new SolidColorBrush(_color);
				polygon.Fill = new SolidColorBrush(_color);
				polygon.ToolTip = _tooltip;
				canvas.Children.Add(polygon);
			}
			else if (SymbolType == SymbolType.Diamond)
			{
				Polygon polygon = new Polygon();
				double width = 0.85 * rect.Width;
				double height = 1.00 * rect.Height;
				x += ((rect.Width - width) / 2);
				PointCollection pt = new PointCollection();
				pt.Add(new Point(x + width / 2, y));
				pt.Add(new Point(x + width, y + height / 2));
				pt.Add(new Point(x + width / 2, y + height));
				pt.Add(new Point(x, y + height / 2));
				polygon.Points = pt;
				polygon.StrokeThickness = 1;
				polygon.Stroke = new SolidColorBrush(_color);
				polygon.Fill = new SolidColorBrush(_color);
				polygon.ToolTip = _tooltip;
				canvas.Children.Add(polygon);
			}
			else if (SymbolType == SymbolType.X)
			{
				Polygon polygon = new Polygon();
				double width = 0.70 * rect.Width;
				double height = 0.85 * rect.Height;
				x += ((rect.Width - width) / 2);
				double thickness = width / 24;
				PointCollection pt = new PointCollection();
				pt.Add(new Point(x + thickness, y));
				pt.Add(new Point(x + width / 2, y + height / 2 - thickness));
				pt.Add(new Point(x + width - thickness, y));
				pt.Add(new Point(x + width, y + thickness));
				pt.Add(new Point(x + width / 2 + thickness, y + height / 2));
				pt.Add(new Point(x + width, y + height - thickness));
				pt.Add(new Point(x + width - thickness, y + height));
				pt.Add(new Point(x + width / 2, y + height / 2 + thickness));
				pt.Add(new Point(x + thickness, y + height));
				pt.Add(new Point(x, y + height - thickness));
				pt.Add(new Point(x + width / 2 - thickness, y + height / 2));
				pt.Add(new Point(x, y + thickness));
				polygon.Points = pt;
				polygon.StrokeThickness = 1;
				polygon.Stroke = new SolidColorBrush(_color);
				polygon.Fill = new SolidColorBrush(_color);
				polygon.ToolTip = _tooltip;
				canvas.Children.Add(polygon);
			}
		}
	}

	public enum LineType
	{
		Segment = 1,
		RayRight = 2,
		RayLeft = 3,
		Line = 4
	};

	public class TimeValuePoint
	{
		private DateTime _time = new DateTime();
		private double _value = double.NaN;
		private int _offset = 0;

		public TimeValuePoint()
		{
		}

		public TimeValuePoint(DateTime time, double value)
		{
			_time = time;
			_value = value;
		}

		public TimeValuePoint(DateTime time, double value, int offset)
		{
			_time = time;
			_value = value;
			_offset = offset;
		}

		public DateTime Time
		{
			get { return _time; }
			set { _time = value; }
		}

		public int Offset
		{
			get { return _offset; }
		}

		public double Value
		{
			get { return _value; }
			set { _value = value; }
		}
	}

	public class ChartBackground
	{
		private string _id = "";
		private TimeValuePoint _point1 = new TimeValuePoint();
		private TimeValuePoint _point2 = new TimeValuePoint();
		private Color _color = Colors.DarkGray;
		private double _opacity = 0.25;

		public ChartBackground(string id)
		{
			_id = id;
		}

		public string Id
		{
			get { return _id; }
			set { _id = value; }
		}

		public TimeValuePoint Point1
		{
			get { return _point1; }
			set { _point1 = value; }
		}

		public TimeValuePoint Point2
		{
			get { return _point2; }
			set { _point2 = value; }
		}

		public Color Color
		{
			get { return _color; }
			set { _color = value; }
		}

		public double Opacity
		{
			get { return _opacity; }
			set { _opacity = value; }
		}
	}

	public class ChartLine
	{
		private string _id = "";
		private LineType _type = LineType.Segment;
		private TimeValuePoint _point1 = new TimeValuePoint();
		private TimeValuePoint _point2 = new TimeValuePoint();
		private Color _color = Colors.DarkGray;
		private double _thickness = 1;
		private bool dotted = false;
		private PenLineCap _cap = PenLineCap.Flat;
		private int _zOrder = 1;
		private bool _scalable = true;
		private string _tooltip = "";
		private bool _truncate = false;

		public ChartLine(string id)
		{
			_id = id;
		}

		public string Id
		{
			get { return _id; }
			set { _id = value; }
		}

		public string ToolTip
		{
			get { return _tooltip; }
			set { _tooltip = value; }
		}

		public bool Scalable
		{
			get { return _scalable; }
			set { _scalable = value; }
		}

		public int ZOrder
		{
			get { return _zOrder; }
			set { _zOrder = value; }
		}

		public LineType Type
		{
			get { return _type; }
			set { _type = value; }
		}

		public TimeValuePoint Point1
		{
			get { return _point1; }
			set { _point1 = value; }
		}

		public TimeValuePoint Point2
		{
			get { return _point2; }
			set { _point2 = value; }
		}

		public Color Color
		{
			get { return _color; }
			set { _color = value; }
		}

		public double Thickness
		{
			get { return _thickness; }
			set { _thickness = value; }
		}

		public PenLineCap Cap
		{
			get { return _cap; }
			set { _cap = value; }
		}

		public bool Dotted
		{
			get { return dotted; }
			set { dotted = value; }
		}

		public void Clear()
		{
			_point1 = null;
			_point2 = null;
		}

		public bool Truncate
		{
			get { return _truncate; }
			set { _truncate = value; }
		}
	}

	public class ChartBezierCurve
	{
		private List<TimeValuePoint> _points = new List<TimeValuePoint>();
		private Color _color = Colors.DarkGray;
		private double _thickness = 1;
		private bool dotted = false;
		private PenLineCap _cap = PenLineCap.Flat;

		public List<TimeValuePoint> Points
		{
			get { return _points; }
			set { _points = value; }
		}

		public Color Color
		{
			get { return _color; }
			set { _color = value; }
		}

		public double Thickness
		{
			get { return _thickness; }
			set { _thickness = value; }
		}

		public PenLineCap Cap
		{
			get { return _cap; }
			set { _cap = value; }
		}

		public bool Dotted
		{
			get { return dotted; }
			set { dotted = value; }
		}

		public void Clear()
		{
			_points.Clear();
		}
	}

	public class ChartBezierShading
	{
		private string _id;
		private int _direction;
		private List<TimeValuePoint> _firstCurvePoints = new List<TimeValuePoint>();
		private List<TimeValuePoint> _secondCurvePoints = new List<TimeValuePoint>();
		private Color _color = Colors.Transparent;
		string _tooltip;
		int _zOrder = -1;

		public ChartBezierShading(string id)
		{
			_id = id;
		}

		public int ZOrder
		{
			get { return _zOrder; }
			set { _zOrder = value; }
		}

		public string Id
		{
			get { return _id; }
		}

		public string Tooltip
		{
			get { return _tooltip; }
			set { _tooltip = value; }
		}

		public int Direction
		{
			get { return _direction; }
			set { _direction = value; }
		}

		public List<TimeValuePoint> FirstCurvePoints
		{
			get { return _firstCurvePoints; }
			set { _firstCurvePoints = value; }
		}

		public List<TimeValuePoint> SecondCurvePoints
		{
			get { return _secondCurvePoints; }
			set { _secondCurvePoints = value; }
		}

		public Color Color
		{
			get { return _color; }
			set { _color = value; }
		}

		public void Clear()
		{
			_firstCurvePoints.Clear();
			_secondCurvePoints.Clear();
		}
	}

	public enum CurveType
	{
		None = 0,
		Line = 1,
		Dash = 2,
		Histogram = 3
	};

	public class ChartCurve
	{
		private string _name = "";
		private CurveType _curveType = CurveType.Line;
		private double _thickness = 1;
		private double _base = 0;
		private List<double> _values1 = new List<double>();
		private List<double> _values2 = new List<double>();
		private List<Color> _colors = new List<Color>();
		private List<int> _colorIndex = new List<int>();
		private int _zOrder = 1;
		private bool _scalable = true;
		private string _legend = "";
		private string _tooltip = "";
		private Color _legendColor = System.Windows.Media.Colors.Transparent;

		public ChartCurve(string name)
		{
			_name = name;
		}

		public string Name
		{
			get { return _name; }
			set { _name = value; }
		}

		public string Legend
		{
			get { return _legend; }
			set { _legend = value; }
		}

		public Color LegendColor
		{
			get { return _legendColor; }
			set { _legendColor = value; }
		}

		public bool Scalable
		{
			get { return _scalable; }
			set { _scalable = value; }
		}

		public int ZOrder
		{
			get { return _zOrder; }
			set { _zOrder = value; }
		}

		public List<double> Values1
		{
			get { return _values1; }
			set { _values1 = value; }
		}

		public List<double> Values2
		{
			get { return _values2; }
			set { _values2 = value; }
		}

		public List<int> ColorIndex
		{
			get { return _colorIndex; }
			set { _colorIndex = value; }
		}

		public List<Color> Colors
		{
			get { return _colors; }
			set { _colors = value; }
		}

		public CurveType CurveType
		{
			get { return _curveType; }
			set { _curveType = value; }
		}

		public string Tooltip
		{
			get { return _tooltip; }
			set { _tooltip = value; }
		}

		public double Thickness
		{
			get { return _thickness; }
			set { _thickness = value; }
		}

		public double Base
		{
			get { return _base; }
			set { _base = value; }
		}

		public void Clear()
		{
			_values1.Clear();
			_values2.Clear();
			_colors.Clear();
			_colorIndex.Clear();
		}
	}

	public class Graph
	{
		private string _name = "";
		private string _title = "";
		private string _modelName = "";
		private Canvas _canvas = new Canvas();
		private double _height;  // in percent of available pixel height

		private bool _autoScale = true;
		private double _valueBase = double.NaN;
		private double _valueScale = double.NaN;

		private List<ChartCurve> _curves = new List<ChartCurve>();
		private Dictionary<DateTime, List<ChartAnnotation>> _annotations = new Dictionary<DateTime, List<ChartAnnotation>>();
		private List<ChartLine> _lines = new List<ChartLine>();
		private List<ChartBezierCurve> _bezierCurves = new List<ChartBezierCurve>();
		private List<ChartBezierShading> _bezierShading = new List<ChartBezierShading>();

		public Graph(string name)
		{
			_name = name;
			_canvas.Background = Brushes.Transparent;
			_height = 0;
		}

		public string Name
		{
			get { return _name; }
			set { _name = value; }
		}

		public string Title
		{
			get { return _title; }
			set { _title = value; }
		}

		public Canvas Canvas
		{
			get { return _canvas; }
		}

		public bool AutoScale
		{
			get { return _autoScale; }
			set { _autoScale = value; }
		}

		public void Clear()
		{
			ClearCurveValues();
			ClearAnnotations();
			ClearBezierCurves();
			ClearBezierShading();
			ClearCurves();
			ClearTrendLines();

			_curves.Clear();
			_annotations.Clear();
			_lines.Clear();
			_bezierCurves.Clear();
			_bezierShading.Clear();

			_canvas.Children.Clear();
		}

		public void ClearCurveValues()
		{
			lock (_curves)
			{
				foreach (ChartCurve curve in _curves)
				{
					curve.Values1.Clear();
					curve.Values2.Clear();
				}
			}
		}

		public void RemoveAnnotations(int id)
		{
			lock (_annotations)
			{
				foreach (List<ChartAnnotation> annotations in _annotations.Values)
				{
					List<ChartAnnotation> remove = new List<ChartAnnotation>();
					foreach (ChartAnnotation annotation in annotations)
					{
						if (annotation.Id == id)
						{
							remove.Add(annotation);
						}
					}

					foreach (ChartAnnotation annotation in remove)
					{
						annotations.Remove(annotation);
					}
				}
			}
		}

		public void ClearAnnotations()
		{
			lock (_annotations)
			{
				_annotations.Clear();
			}
		}

		public void AddAnnotation(ChartAnnotation annotation)
		{
			lock (_annotations)
			{
				DateTime time = annotation.DateTime;
				List<ChartAnnotation> annotations = null;
				if (!_annotations.TryGetValue(time, out annotations))
				{
					annotations = new List<ChartAnnotation>();
					_annotations[time] = annotations;
				}
				_annotations[time].Add(annotation);
			}
		}

		public void ClearTrendLines()
		{
			lock (_lines)
			{
				_lines.Clear();
			}
		}

		public void ClearTrendLines(string id)
		{
			lock (_lines)
			{
				List<ChartLine> remove = new List<ChartLine>();
				foreach (ChartLine line in _lines)
				{
					if (line.Id == id)
					{
						remove.Add(line);
					}
				}

				foreach (ChartLine line in remove)
				{
					_lines.Remove(line);
				}
			}
		}

		public void ClearBezierCurves()
		{
			lock (_bezierCurves)
			{
				_bezierCurves.Clear();
			}
		}

		public void ClearBezierShading()
		{
			lock (_bezierShading)
			{
				_bezierShading.Clear();
			}
		}

		public void ClearBezierShading(string id)
		{
			lock (_bezierShading)
			{
				List<ChartBezierShading> remove = new List<ChartBezierShading>();
				foreach (ChartBezierShading shade in _bezierShading)
				{
					if (id == shade.Id)
					{
						remove.Add(shade);
					}
				}

				foreach (ChartBezierShading shade in remove)
				{
					_bezierShading.Remove(shade);
				}
			}
		}

		public void AddTrendLines(List<ChartLine> lines)
		{
			lock (_lines)
			{
				_lines.AddRange(lines);
			}
		}

		public void AddBezierCurves(List<ChartBezierCurve> bezierCurves)
		{
			lock (_bezierCurves)
			{
				_bezierCurves.AddRange(bezierCurves);
			}
		}

		public void AddBezierShading(List<ChartBezierShading> bezierShading)
		{
			lock (_bezierShading)
			{
				_bezierShading.AddRange(bezierShading);
			}
		}

		public double Height
		{
			get { return _height; }
			set { _height = value; }
		}

		public double ValueBase
		{
			get { return _valueBase; }
			set { _valueBase = value; }
		}
		public double ValueScale
		{
			get { return _valueScale; }
			set { _valueScale = value; }
		}

		private ChartCurve findCurve(string name)
		{
			ChartCurve curve = null;
			foreach (ChartCurve c in _curves)
			{
				if (c.Name == name)
				{
					curve = c;
					break;
				}
			}
			return curve;
		}

		public void SetCurvesColorIndexes(string name, List<int> values)
		{
			lock (_curves)
			{
				if (findCurve(name) == null)
				{
					ChartCurve curve = new ChartCurve(name);
					_curves.Add(curve);
				}

				findCurve(name).ColorIndex = values;
			}
		}

		public void SetLegendColor(string name, Color color)
		{
			lock (_curves)
			{
				if (findCurve(name) == null)
				{
					ChartCurve curve = new ChartCurve(name);
					_curves.Add(curve);
				}

				findCurve(name).LegendColor = color;
			}
		}

		public void LoadCurveValues1(string name, List<double> values)
		{
			lock (_curves)
			{
				if (findCurve(name) == null)
				{
					ChartCurve curve = new ChartCurve(name);
					_curves.Add(curve);
				}

				findCurve(name).Values1 = values;
			}
		}

		public void LoadCurveValues2(string name, List<double> values)
		{
			lock (_curves)
			{
				if (findCurve(name) == null)
				{
					ChartCurve curve = new ChartCurve(name);
					_curves.Add(curve);
				}

				findCurve(name).Values2 = values;
			}
		}

		public void ClearCurveValues(string name)
		{
			lock (_curves)
			{
				ChartCurve curve = findCurve(name);
				if (curve != null)
				{
					curve.Values1.Clear();
					curve.Values2.Clear();
				}
			}
		}

		public void AdvanceCurveValues()
		{
			lock (_curves)
			{
				foreach (ChartCurve curve in _curves)
				{
					curve.Values1.RemoveAt(0);
					curve.Values1.Add(double.NaN);
					curve.Values2.RemoveAt(0);
					curve.Values2.Add(double.NaN);
				}
			}
		}

		public void UpdateCurveValue(string name, int index, double value1, double value2)
		{
			lock (_curves)
			{
				ChartCurve curve = findCurve(name);
				if (curve != null)
				{
					if (index >= 0 && index < curve.Values1.Count)
					{
						curve.Values1[index] = value1;
						curve.Values2[index] = value2;
					}
				}
			}
		}

		public List<ChartLine> Lines
		{
			get
			{
				List<ChartLine> lines = new List<ChartLine>();
				lock (_lines)
				{
					lines.AddRange(_lines);
				}
				return lines;
			}
		}

		public List<ChartBezierCurve> BezierCurves
		{
			get
			{
				List<ChartBezierCurve> bezierCurves = new List<ChartBezierCurve>();
				lock (_bezierCurves)
				{
					bezierCurves.AddRange(_bezierCurves);
				}
				return bezierCurves;
			}
		}

		public List<ChartBezierShading> BezierShadings
		{
			get
			{
				List<ChartBezierShading> bezierShading = new List<ChartBezierShading>();
				lock (_bezierShading)
				{
					bezierShading.AddRange(_bezierShading);
				}
				return bezierShading;
			}
		}

		public Dictionary<DateTime, List<ChartAnnotation>> Annotations
		{
			get { return _annotations; }
		}

		public void GetMinMax(int startIndex, int endIndex, out double minimum, out double maximum)
		{
			lock (_curves)
			{
				minimum = double.NaN;
				maximum = double.NaN;
				foreach (ChartCurve curve in _curves)
				{
					if (curve.Scalable)
					{
						for (int ii = startIndex; ii < endIndex; ii++)
						{
							double value1 = (ii >= 0 && ii < curve.Values1.Count) ? curve.Values1[ii] : double.NaN;
							if (!double.IsNaN(value1))
							{
								if (double.IsNaN(minimum) || value1 < minimum) minimum = value1;
								if (double.IsNaN(maximum) || value1 > maximum) maximum = value1;
							}

							double value2 = (ii >= 0 && ii < curve.Values2.Count) ? curve.Values2[ii] : double.NaN;
							if (!double.IsNaN(value2))
							{
								if (double.IsNaN(minimum) || value2 < minimum) minimum = value2;
								if (double.IsNaN(maximum) || value2 > maximum) maximum = value2;
							}
						}
					}
				}
			}

			// todo - fix this - currently assumes entire line is visible
			lock (_lines)
			{
				foreach (ChartLine line in _lines)
				{
					if (line.Scalable)
					{
						double value1 = line.Point1.Value;
						if (double.IsNaN(minimum) || value1 < minimum) minimum = value1;
						if (double.IsNaN(maximum) || value1 > maximum) maximum = value1;

						double value2 = line.Point2.Value;
						if (double.IsNaN(minimum) || value2 < minimum) minimum = value2;
						if (double.IsNaN(maximum) || value2 > maximum) maximum = value2;
					}
				}
			}
		}

		public void ClearCurves()
		{
			lock (_curves)
			{
				_curves.Clear();
			}
		}

		public void RemoveCurve(string name)
		{
			lock (_curves)
			{
				foreach (ChartCurve curve in _curves)
				{
					if (curve.Name == name)
					{
						_curves.Remove(curve);
						break;
					}
				}
			}
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

		public void AddCurve(string name, List<Color> colors, double thickness)
		{
			lock (_curves)
			{
				if (findCurve(name) == null)
				{
					ChartCurve curve = new ChartCurve(name);
					_curves.Add(curve);
				}

				ChartCurve curve1 = findCurve(name);
				curve1.Colors = colors;
				curve1.Thickness = thickness;
			}
		}

		public List<string> GetCurveNames()
		{
			List<string> names = new List<string>();
			lock (_curves)
			{
				foreach (ChartCurve curve in _curves)
				{
					names.Add(curve.Name);
				}
			}
			return names;
		}

		public List<double> GetCurveValues1(string name)
		{
			lock (_curves)
			{
				ChartCurve curve = findCurve(name);
				return (curve != null) ? new List<double>(curve.Values1) : new List<double>();
			}
		}

		public List<double> GetCurveValues2(string name)
		{
			lock (_curves)
			{
				ChartCurve curve = findCurve(name);
				return (curve != null) ? new List<double>(curve.Values2) : new List<double>();
			}
		}

		public List<int> GetCurveColorIndexes(string name)
		{
			lock (_curves)
			{
				ChartCurve curve = findCurve(name);
				return (curve != null) ? new List<int>(curve.ColorIndex) : new List<int>();
			}
		}

		public Color GetLegendColor(string name)
		{
			lock (_curves)
			{
				ChartCurve curve = findCurve(name);
				return (curve != null) ? curve.LegendColor : Colors.Transparent;
			}
		}

		public List<Color> GetCurveColors(string name)
		{
			lock (_curves)
			{
				ChartCurve curve = findCurve(name);
				return (curve != null) ? new List<Color>(curve.Colors) : new List<Color>();
			}
		}

		public void SetCurveColors(string name, List<Color> colors)
		{
			lock (_curves)
			{
				ChartCurve curve = findCurve(name);
				if (curve != null) curve.Colors = colors;
			}
		}

		public CurveType GetCurveType(string name)
		{
			lock (_curves)
			{
				ChartCurve curve = findCurve(name);
				return (curve != null) ? curve.CurveType : 0;
			}
		}

		public string GetCurveTooltip(string name)
		{
			lock (_curves)
			{
				ChartCurve curve = findCurve(name);
				return (curve != null) ? curve.Tooltip : "";
			}
		}

		public double GetCurveThickness(string name)
		{
			lock (_curves)
			{
				ChartCurve curve = findCurve(name);
				return (curve != null) ? curve.Thickness : 0;
			}
		}

		public double GetCurveBase(string name)
		{
			lock (_curves)
			{
				ChartCurve curve = findCurve(name);
				return (curve != null) ? curve.Base : 0;
			}
		}

		public void SetCurveType(string name, CurveType curveType)
		{
			lock (_curves)
			{
				ChartCurve curve = findCurve(name);
				if (curve != null) curve.CurveType = curveType;
			}
		}

		public void SetCurveTooltip(string name, string tooltip)
		{
			lock (_curves)
			{
				ChartCurve curve = findCurve(name);
				if (curve != null) curve.Tooltip = tooltip;
			}
		}

		public void SetCurveThickness(string name, double thickness)
		{
			lock (_curves)
			{
				ChartCurve curve = findCurve(name);
				if (curve != null) curve.Thickness = thickness;
			}
		}

		public void SetCurveBase(string name, double baseValue)
		{
			lock (_curves)
			{
				ChartCurve curve = findCurve(name);
				if (curve != null) curve.Base = baseValue;
			}
		}

		public int GetCurveZOrder(string name)
		{
			lock (_curves)
			{
				ChartCurve curve = findCurve(name);
				return (curve != null) ? curve.ZOrder : 0;
			}
		}

		public void SetCurveZOrder(string name, int zOrder)
		{
			lock (_curves)
			{
				ChartCurve curve = findCurve(name);
				if (curve != null) curve.ZOrder = zOrder;
			}
		}

		public void SetCurveScalable(string name, bool scalable)
		{
			lock (_curves)
			{
				ChartCurve curve = findCurve(name);
				if (curve != null) curve.Scalable = scalable;
			}
		}

		public bool IsCurveScalable(string name)
		{
			lock (_curves)
			{
				ChartCurve curve = findCurve(name);
				return (curve != null) ? curve.Scalable : false;
			}
		}

		public void SetCurveLegend(string name, string legend)
		{
			lock (_curves)
			{
				ChartCurve curve = findCurve(name);
				if (curve != null) curve.Legend = legend;
			}
		}

		public string GetCurveLegend(string name)
		{
			lock (_curves)
			{
				ChartCurve curve = findCurve(name);
				return (curve != null) ? curve.Legend : "";
			}
		}
	}

	public class MouseInfo
	{
		private Grid _grid;
		private TextBlock _header = new TextBlock();
		private Grid _advice = new Grid();
		private Grid _legend = new Grid();
		private StackPanel _labelPanel;
		private StackPanel _valuePanel;
		private double _fontSize = 10;

		public MouseInfo(double xOffset, double yOffset, double fontSize)
		{
			_fontSize = 9;
			_grid = new Grid();

			var col1 = new ColumnDefinition();
			col1.Width = new GridLength(20);
			col1.MaxWidth = 20;
			col1.MinWidth = 20;
			_grid.ColumnDefinitions.Add(col1);
			var col2 = new ColumnDefinition();
			col2.Width = new GridLength(40);
			col2.MaxWidth = 45;
			col2.MinWidth = 45;
			_grid.ColumnDefinitions.Add(col2);
			var col3 = new ColumnDefinition();
			col3.Width = new GridLength(65, GridUnitType.Star);
			_grid.ColumnDefinitions.Add(col3);

			var row1 = new RowDefinition();
			_grid.RowDefinitions.Add(row1);
			var row2 = new RowDefinition();
			_grid.RowDefinitions.Add(row2);
			var row3 = new RowDefinition();
			_grid.RowDefinitions.Add(row3);
			var row4 = new RowDefinition();
			_grid.RowDefinitions.Add(row4);

			_grid.SetValue(Canvas.LeftProperty, xOffset);
			_grid.SetValue(Canvas.TopProperty, yOffset);
			_grid.Background = Brushes.Transparent;
			_grid.Margin = new Thickness(0, 1, 0, 0);
			_grid.Visibility = Visibility.Collapsed;

			_header.Height = 9;
			_header.Margin = new Thickness(0);
			_header.HorizontalAlignment = HorizontalAlignment.Left;
			_header.Foreground = Brushes.Silver; //DarkGray
			_header.FontSize = 9;
			_header.FontWeight = FontWeights.Light;
			_header.FontFamily = new FontFamily("Arial");
			_header.Width = 65;

			_labelPanel = new StackPanel();
			_valuePanel = new StackPanel();
			_labelPanel.HorizontalAlignment = HorizontalAlignment.Left;
			_valuePanel.HorizontalAlignment = HorizontalAlignment.Left;
			//_valuePanel.Margin = new Thickness(0, 0, 4, 0);

			col1 = new ColumnDefinition();
			col1.Width = new GridLength(45);
			col1.MaxWidth = 45;
			col1.MinWidth = 45;
			_legend.ColumnDefinitions.Add(col1);
			col2 = new ColumnDefinition();
			col2.Width = new GridLength(10);
			col2.MaxWidth = 10;
			col2.MinWidth = 10;
			_legend.ColumnDefinitions.Add(col2);
			col3 = new ColumnDefinition();
			col3.Width = new GridLength(15);
			col3.MaxWidth = 15;
			col3.MinWidth = 15;
			_legend.ColumnDefinitions.Add(col3);

			Grid.SetColumnSpan(_header, 2);
			Grid.SetRow(_advice, 1);
			Grid.SetColumnSpan(_advice, 3);
			Grid.SetRow(_legend, 2);
			Grid.SetColumnSpan(_legend, 3);
			Grid.SetRow(_labelPanel, 3);
			Grid.SetColumn(_valuePanel, 1);
			Grid.SetRow(_valuePanel, 3);

			_grid.Children.Add(_header);
			_grid.Children.Add(_advice);
			_grid.Children.Add(_legend);
			_grid.Children.Add(_labelPanel);
			_grid.Children.Add(_valuePanel);

			Visible = true;
		}

		public Grid GetGrid()
		{
			return _grid;
		}

		public Grid GetAdviceGrid()
		{
			return _advice;
		}
		public Grid GetLegendGrid()
		{
			return _legend;
		}

		public void SetBackground(Brush brush)
		{
			_header.Background = brush;
			setTextBlockBackground(_labelPanel, brush);
			setTextBlockBackground(_valuePanel, brush);
		}

		public void SetForeground(Brush brush)
		{
			_header.Foreground = brush;
		}

		private void setTextBlockBackground(StackPanel panel, Brush brush)
		{
			foreach (var child in panel.Children)
			{
				var tb = child as TextBlock;
				if (tb != null)
				{
					tb.Background = brush;
				}
			}
		}

		public TextBlock getHeader()
		{
			return _header;
		}

		public void SetHeader(string text)
		{
			_header.Text = text;
		}

		public void AddLabel(string text, Brush brush)
		{
			TextBlock textBlock = new TextBlock();
			textBlock.Height = 10 * 1.1;
			textBlock.Margin = new Thickness(0);
			textBlock.HorizontalAlignment = HorizontalAlignment.Left;
			textBlock.TextAlignment = TextAlignment.Left;
			textBlock.Text = text;
			textBlock.Foreground = brush;
			textBlock.FontSize = 9;
			textBlock.FontWeight = FontWeights.Light;
			textBlock.FontFamily = new FontFamily("Arial");
			textBlock.Visibility = (text.Length > 0) ? Visibility.Visible : Visibility.Collapsed;
			textBlock.Background = _header.Background;
			textBlock.MinWidth = 20;
			_labelPanel.Children.Add(textBlock);
		}

		public TextBlock AddValue(string text, Brush brush)
		{
			TextBlock textBlock = new TextBlock();
			textBlock.Height = 10 * 1.1;
			textBlock.Margin = new Thickness(0);
			textBlock.HorizontalAlignment = HorizontalAlignment.Right;
			textBlock.TextAlignment = TextAlignment.Right;
			textBlock.Text = text;
			textBlock.Foreground = brush;
			textBlock.FontSize = 9;
			textBlock.FontWeight = FontWeights.Light;
			textBlock.FontFamily = new FontFamily("Arial");
			textBlock.Visibility = (text.Length > 0) ? Visibility.Visible : Visibility.Collapsed;
			textBlock.Background = _header.Background;
			textBlock.MinWidth = 45;
			_valuePanel.Children.Add(textBlock);
			return textBlock;
		}

		public void Clear()
		{
			_labelPanel.Children.Clear();
			_valuePanel.Children.Clear();
		}

		public bool Visible
		{
			get { return (_grid.Visibility == Visibility.Visible); }
			set { _grid.Visibility = (value ? Visibility.Visible : Visibility.Collapsed); }
		}
	}

	public class ChartSymbolEditor : ComboBox
	{
		private TextBox editableTextBox = null;

		public override void OnApplyTemplate()
		{
			var myTextBox = GetTemplateChild("PART_EditableTextBox") as TextBox;
			if (myTextBox != null)
			{
				this.editableTextBox = myTextBox;
			}

			base.OnApplyTemplate();
		}

		public void SetCaret(int position)
		{
			this.editableTextBox.SelectionStart = position;
			this.editableTextBox.SelectionLength = 0;
		}
	}

	public class IndicatorTreeViewItem : TreeViewItem
	{
		string _name = "";
		bool _hasToggleButton = true;
		ToggleButton _toggleButton = null;

		public IndicatorTreeViewItem(bool useRadioButton)
		{
			if (useRadioButton)
			{
				_toggleButton = new RadioButton();
			}
			else
			{
				_toggleButton = new CheckBox();
			}
		}

		public string Name { get { return _name; } set { _name = value; } }

		public ToggleButton ToggleButton { get { return _toggleButton; } }
		public bool HasToggleBox { get { return _hasToggleButton; } set { _hasToggleButton = value; } }
		public bool IsChecked { get { return _toggleButton.IsChecked == true; } set { _toggleButton.IsChecked = value; } }
	}
}