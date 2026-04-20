using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Drawing2D;
//using Bloomberglp.TerminalApi;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;

namespace ATMML
{
    public partial class Charts : ContentControl, INotifyPropertyChanged
    {
        MainView _mainView = null;

        string _nav1 = "SPY US Equity";
        string _nav2 = "";
        string _nav3 = "SPY US Equity";
        string _nav4 = "";
        string _nav5 = "";
        string _nav6 = "";

        string _yieldNav1 = "";
        string _yieldNav2 = "";
        string _yieldNav3 = "";

        string _symbol = "";

        string _symbolKey = "";

        string _interval = "Daily";
        string _aodInterval = "Daily";

        string _view = "CHART";
        string _mode = "";

        string _activeChartInterval = "1D";

        Portfolio.PortfolioType _type;

        private string _strategy = "";

        Dictionary<string, SymbolInfo> _symbols = new Dictionary<string, SymbolInfo>();
        Dictionary<string, string> _abstractSignals = new Dictionary<string, string>();
        Dictionary<int, int> _ago = new Dictionary<int, int>();
        Dictionary<int, string> _intervals = new Dictionary<int, string>();
        private Dictionary<string, object> _referenceData = new Dictionary<string, object>();

        bool _calculating = false;
        List<string> _calculatedSymbols = new List<string>();

        Dictionary<string, List<Trade>> _trades = new Dictionary<string, List<Trade>>();

        string _selectedNav1 = "";
        string _selectedNav2 = "";
        string _selectedNav3 = "";
        string _selectedNav4 = "";
        string _selectedNav5 = "";
        string _selectedNav6 = "";

        BarCache _barCache;
        bool _update1 = false;
        bool _update2 = false;
        bool _update3 = false;
        bool _update4 = false;
        DispatcherTimer _ourViewTimer = new DispatcherTimer();

        bool _updateCalculator = false;

        Portfolio.PortfolioType _clientPortfolioType = Portfolio.PortfolioType.Index;
        string _clientPortfolioName = "";

        int _portfolioRequestedCount = 0;

        Portfolio _portfolio = new Portfolio(6);
        object _memberListLock = new object();
        List<Symbol> _memberList = new List<Symbol>();
        int _updateMemberList = 0;

        Navigation nav = new Navigation();

        Chart _chart1 = null;
        Chart _chart2 = null;
        Chart _chart3 = null;

        bool _legendOn = false;

        string _chartSymbol1 = "";
        string _chartSymbol2 = "";
        Dictionary<int, string> _chartIntervals = new Dictionary<int, string>();

        const double _spreadsheetRowHeight = 24;
        Point _spreadsheetMargin = new Point(4, 4);
        Dictionary<string, int> _spreadsheetRows = new Dictionary<string, int>();
        Dictionary<int, string> _spreadsheetSymbols = new Dictionary<int, string>();
        Dictionary<string, Color> _spreadsheetColors = new Dictionary<string, Color>();
        List<string> _spreadsheetUpdate = new List<string>();
        string _spreadsheetPortfolioName = "";
        Dictionary<int, string> _spreadsheetIntervals = new Dictionary<int, string>();
        bool _spreadsheetChart = false;

        bool _addFlash = false;

        ConditionDialog _conditionDialog;

        ConfirmationDialog _confirmationDialog;

        string _condition = "POSITIONS";
        BarCache _symbolBarCache;
        Dictionary<string, double> _symbolSignals = new Dictionary<string, double>();
        Dictionary<string, Color> _symbolColors = new Dictionary<string, Color>();

        List<string> _modelNames = new List<string>();
        List<string> _activeModelNames = new List<string>();

        bool _updateAod = false;

        bool _initialize = false;

        public Charts(MainView mainView)
        {
            DataContext = this;

            InitializeComponent();

            _mainView = mainView;

            AlertButton.Visibility = MainView.EnableAlerts ? Visibility.Visible : Visibility.Visible;

            _nav1 = "YOUR BLOOMBERG LISTS >";
            _nav2 = "";
            _nav4 = "";
            _nav5 = "";

            _ourViewTimer.Interval = TimeSpan.FromMilliseconds(200);
            _ourViewTimer.Tick += new System.EventHandler(timer_Tick);
            _ourViewTimer.Start();

            _initialize = true;
            setDefaultIntervals();

            AOD.OrderEvent += onOrderEvent;

        }

        private void initializeModelList()
        {
            string[] intervals = new string[] {
                _chart3.getIntervalLabel(getChartInterval(_interval, 0)).Replace(", ", ""),
                _chart3.getIntervalLabel(getChartInterval(_interval, 1)).Replace(", ", ""),
                _chart3.getIntervalLabel(getChartInterval(_interval, 2)).Replace(", ", "") };

            if (_modelNames.Count == 0)
            {
                var path = MainView.GetDataFolder() + @"\senarios";
                Directory.CreateDirectory(path);
                _modelNames = MainView.getModelNames();
                _modelNames.Insert(0, "NO PREDICTION");
            }

            ScenarioModels.Children.Clear();
            ScenarioModels.RowDefinitions.Clear();
            for (int ii = 0; ii < _modelNames.Count; ii++)  
            {
                var modelName = _modelNames[ii];
                if (_activeModelNames.Count == 0 && modelName != "NO PREDICTION")
                {
                    _activeModelNames.Add(modelName);
                }

                string[] priceText = new string[] { "", "", "" };
                if (ii > 0)
                {
                    var model = MainView.GetModel(modelName);
                    var scenario = MainView.GetSenarioLabel(model.Scenario);

                    var text1 = scenario.Trim();
                    var index1 = text1.IndexOf("|");

                    if (index1 != -1)
                    {
                        var referencePrice = text1.Substring(index1 + 2, 1).Replace("O", "Open").Replace("H", "High").Replace("L", "Low").Replace("C", "Close");
                        var referenceIndex = int.Parse(text1.Substring(text1.Length - 1, 1)) - 1;
                        //var forecastIndex = int.Parse(text1.Substring(index1 - 2, 1));
                        priceText[0] = _chart3.GetPrice(referencePrice, referenceIndex, true).ToString("0.00").Replace("NaN", "");
                        priceText[1] = _chart2.GetPrice(referencePrice, referenceIndex, true).ToString("0.00").Replace("NaN", "");
                        priceText[2] = _chart1.GetPrice(referencePrice, referenceIndex, true).ToString("0.00").Replace("NaN", "");
                    }
                }


                    RowDefinition row = new RowDefinition();
                //row.Height = new GridLength(72);
                ScenarioModels.RowDefinitions.Add(row); // row 0

                RadioButton radioButton = new RadioButton();
                radioButton.Tag = modelName;
                radioButton.IsChecked = _activeModelNames.Contains(modelName);
                radioButton.Checked += Model_Checked;
                radioButton.Unchecked += Model_Unchecked;
                radioButton.BorderBrush = new SolidColorBrush(Color.FromRgb(0x00, 0x00, 0x00));
                radioButton.Background = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));
                radioButton.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xcc, 0xff));
                radioButton.Margin = new Thickness(8, 4, 0, 0);
                radioButton.GroupName = "MLModels";
                Grid.SetRow(radioButton, ii);

                Grid grid1 = new Grid();
                Grid.SetRow(grid1, ii);
                Grid.SetColumn(grid1, 1);

                var row1 = new RowDefinition();
                var row2 = new RowDefinition();
                var row3 = new RowDefinition();
                var row4 = new RowDefinition();

                grid1.RowDefinitions.Add(row1);
                grid1.RowDefinitions.Add(row2);
                grid1.RowDefinitions.Add(row3);
                grid1.RowDefinitions.Add(row4);

                Label label = new Label();
                label.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
                label.FontSize = 9;
                label.Content = modelName;
                label.Margin = new Thickness(0, 0, 0, 0);
                grid1.Children.Add(label);

                if (ii > 0)
                {
                    for (var jj = 0; jj < intervals.Length; jj++)
                    {
                        var grid2 = new Grid();
                        var col1 = new ColumnDefinition();
                        var col2 = new ColumnDefinition();
                        col1.Width = new GridLength(25, GridUnitType.Pixel);
                        col2.Width = new GridLength(60, GridUnitType.Pixel);
                        grid2.ColumnDefinitions.Add(col1); 
                        grid2.ColumnDefinitions.Add(col2);
                        
                        Label label1 = new Label();
                        label1.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
                        label1.VerticalAlignment = VerticalAlignment.Center;
                        label1.FontSize = 8;
                        label1.MaxHeight = 20;
                        label1.Padding = new Thickness(0);
                        label1.Content = intervals[jj];
                        label1.Margin = new Thickness(5, 0, 5, 0);
                        grid2.Children.Add(label1);

                        Label label2 = new Label();
                        label2.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
                        label2.HorizontalAlignment = HorizontalAlignment.Right;
                        label2.HorizontalContentAlignment = HorizontalAlignment.Right;
                        label2.FontSize = 9;
                        label2.MaxHeight = 20;
                        label2.Padding = new Thickness(0);
                        label2.Content = priceText[jj];
                        label2.Margin = new Thickness(0, 0, 0, 0);
                        Grid.SetColumn(label2, 1);
                        grid2.Children.Add(label2);

                        Grid.SetRow(grid2, jj + 1);
                        grid1.Children.Add(grid2);
                    }
                }

                ScenarioModels.Children.Add(radioButton);
                ScenarioModels.Children.Add(grid1);
            }
        }

        private void Model_Unchecked(object sender, RoutedEventArgs e)
        {
        }

        private void Model_Checked(object sender, RoutedEventArgs e)
        {
            var item = sender as RadioButton;
            var name = item.Tag as string;
            _activeModelNames.Clear();
            _activeModelNames.Add(name);

            _chart1.ModelNames = _activeModelNames;
            _chart2.ModelNames = _activeModelNames;
            _chart3.ModelNames = _activeModelNames;

            stopPredictionThread();

            lock (_predictionRequests)
            {
                _predictionRequests.Clear();
            }
            lock (_predictions)
            {
                _predictions.Clear();
            }

            updateScenarioLabel();

            _updateMemberList = 1;

            requestSymbolBars();

            _updateAod = true;
        }

        public Charts(MainView mainView, string continent, string country, string group, string subgroup, string industry, string symbol, string clientPortfolioName, Portfolio.PortfolioType clientPortfolioType)
        {
            DataContext = this;

            InitializeComponent();

            _mainView = mainView;

            getInfo();
            Country.Content = getPortfolioName();
            
            _mode = "POSITIONS";

            _nav1 = continent;
            _nav2 = country;
            _nav3 = group;
            _nav4 = subgroup;
            _nav5 = industry;
            _symbol = symbol;

            if (_clientPortfolioName.Length > 0)
            {
                _clientPortfolioName = clientPortfolioName;
                _clientPortfolioType = clientPortfolioType;
            }

            _ourViewTimer.Interval = TimeSpan.FromMilliseconds(200);
            _ourViewTimer.Tick += new System.EventHandler(timer_Tick);
            _ourViewTimer.Start();

            _initialize = true;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private void setDefaultIntervals()
        {
            TradeHorizon tradeHorizon = getTradeHorizon();

            if (tradeHorizon == TradeHorizon.ShortTerm)
            {
                _interval = "60";
                _intervals[1] = "Daily";
                _intervals[2] = "240";
                _intervals[3] = "60";
                _intervals[4] = "30";
            }
            else if (tradeHorizon == TradeHorizon.MidTerm)
            {
                _interval = "Daily";
                _intervals[1] = "Monthly";
                _intervals[2] = "Weekly";
                _intervals[3] = "Daily";
                _intervals[4] = "240";
            }
            else if (tradeHorizon == TradeHorizon.LongTerm)
            {
                _interval = "Weekly";
                _intervals[1] = "Quarterly";
                _intervals[2] = "Monthly";
                _intervals[3] = "Weekly";
                _intervals[4] = "Daily";
            }
            else if (tradeHorizon == TradeHorizon.VeryLongTerm)
            {
                _interval = "Monthly";
                _intervals[1] = "Yearly";
                _intervals[2] = "Quarterly";
                _intervals[3] = "Monthly";
                _intervals[4] = "Weekly";
            }
            else if (tradeHorizon == TradeHorizon.ExtraLongTerm)
            {
                _interval = "Quarterly";
                _intervals[1] = "Yearly";
                _intervals[2] = "SemiAnnually";
                _intervals[3] = "Quarterly";
                _intervals[4] = "Yearly";
            }
            _aodInterval = _interval;
        }

        private void getInfo()
        {
            bool ok = false;

            string info = _mainView.GetInfo("Positions");
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
                if (count > 8) _intervals[1] = fields[8];
                if (count > 9) _intervals[2] = fields[9];
                if (count > 10) _intervals[3] = fields[10];
                if (count > 11) _clientPortfolioType = (Portfolio.PortfolioType)(Enum.Parse(typeof(Portfolio.PortfolioType), fields[11]));
                if (count > 12) _mode = fields[12];
                if (count > 13) _intervals[4] = fields[13];
                if (count > 14) _condition = fields[14];
                if (count > 15) _legendOn = bool.TryParse(fields[15], out ok) ? ok : false;
                if (count > 16) _strategy = fields[16];
                if (count > 17) _activeModelNames = fields[17].Split('\t').ToList();
                if (count > 18) _chartCount = int.Parse(fields[18]);
                if (count > 19) _chartNumber = int.Parse(fields[19]);
            }

            var selectedModelName = (_activeModelNames.Count > 0) ? _activeModelNames[0] : "";
            _activeModelNames.Clear();
            _activeModelNames.Add(selectedModelName);

            _view = "CHART";
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
                _intervals[1] + ";" +
                _intervals[2] + ";" +
                _intervals[3] + ";" +
                _clientPortfolioType + ";" +
                _mode + ";" +
                _intervals[4] + ";" +
                _condition + ";" +
                _legendOn.ToString() + ";" +
                _strategy + ";" +
                String.Join("\t", _activeModelNames) + ";" +
                _chartCount + ";" +
                _chartNumber;

            _mainView.SetInfo("Positions", info);
        }

        private void initialize()
        {
            getInfo();
            Country.Content = getPortfolioName();

            var visibility = (MainView.EnableRickStocks) ? Visibility.Visible : Visibility.Collapsed;

            List<Alert> alerts = Alert.Manager.Alerts;
            foreach (Alert alert in alerts)
            {
                alert.FlashEvent += new FlashEventHandler(alert_FlashEvent);
            }

            ResourceDictionary dictionary = new ResourceDictionary();
            dictionary.Source = new Uri("pack://application:,,,/ATMML;component/StyleDictionary.xaml");
            this.Resources.MergedDictionaries.Add(dictionary);

            LinkCharts.IsChecked = Portfolio.GetLinkCharts();
            LinkAccounts.IsChecked = Portfolio.GetLinkAccounts();

            _mode = MainView.GetChartMode();

            for (int ii = 1; ii <= 4; ii++)
            {
                _ago[ii] = 1;
                _spreadsheetIntervals[ii] = "";
                _chartIntervals[ii] = "";
            }

            Portfolios.ContextMenu = new ContextMenu();
            MenuItem menuItem1 = new MenuItem();
            menuItem1.Header = "Print Results...";
            menuItem1.Click += print_click;
            menuItem1.Foreground = Brushes.White;
            Portfolios.ContextMenu.Items.Add(menuItem1);

            MenuItem menuItem2 = new MenuItem();
            menuItem2.Header = "Save to Excel...";
            menuItem2.Click += save_click;
            menuItem2.Foreground = Brushes.White;
            Portfolios.ContextMenu.Items.Add(menuItem2);

            MenuItem menuItem3 = new MenuItem();
            menuItem3.Header = "Change Balance...";
            menuItem3.Click += balance_click;
            menuItem3.Foreground = Brushes.White;
            Portfolios.ContextMenu.Items.Add(menuItem3);

            _spreadsheetPortfolioName = ""; 

            addIntervalChoices(BackgroundInterval, _intervals[1]);
            addIntervalChoices(AlignmentInterval, _intervals[2]);
            addIntervalChoices(TradeInterval, _intervals[3]);
            addIntervalChoices(SpreadsheetBackgroundInterval, _intervals[1]);
            addIntervalChoices(SpreadsheetAlignmentInterval, _intervals[2]);
            addIntervalChoices(SpreadsheetTradeInterval, _intervals[3]);

            _portfolio.PortfolioChanged += new PortfolioChangedEventHandler(portfolioChanged);
            _barCache = new BarCache(barChanged);
            _symbolBarCache = new BarCache(symbolBarChanged);

            positionCharts();

            updateChart(_symbol, _interval, true);

            _modelNames.Clear();

            //initializeModelList();

            _chart1.ModelNames = _activeModelNames;
            _chart2.ModelNames = _activeModelNames;
            _chart3.ModelNames = _activeModelNames;

            update(_nav1, _nav2, _nav3, _nav4, _nav5, _nav6);

            _mainView.MainEvent += _mainView_MainEvent;

            if (!BarServer.ConnectedToBloomberg())
            {
                GlobalMacButton.Visibility = Visibility.Collapsed;
            }
        }

        private void _mainView_MainEvent(object sender, MainEventArgs e)
        {
            string portfolioName = getPortfolioName();
            if (portfolioName == "PCM Orders")
            {
                requestPortfolio(portfolioName, _type);
            }
        }

        private bool _drawAod = false;
        private void updateAod()
        {
            if (_symbolBarCache != null)
            {
                string interval0 = Study.getForecastInterval(_aodInterval, 0);
                string interval1 = Study.getForecastInterval(_aodInterval, 1);
                string interval2 = Study.getForecastInterval(_aodInterval, 2);

                var extra = 1;

                List<DateTime> shortTermTimes = _symbolBarCache.GetTimes(_symbol, interval0, extra);
                Series[] shortTermSeries = _symbolBarCache.GetSeries(_symbol, interval0, new string[] { "Open", "High", "Low", "Close" }, extra);

                List<DateTime> midTermTimes = _symbolBarCache.GetTimes(_symbol, interval1, extra);
                Series[] midTermSeries = _symbolBarCache.GetSeries(_symbol, interval1, new string[] { "Open", "High", "Low", "Close" }, extra);

                List<DateTime> longTermTimes = _symbolBarCache.GetTimes(_symbol, interval2, extra);
                Series[] longTermSeries = _symbolBarCache.GetSeries(_symbol, interval2, new string[] { "Open", "High", "Low", "Close" }, extra);

                Series[] shortTermIndexSeries = new Series[1] { (_referenceData.ContainsKey(_symbol + ":" + interval0)) ? _referenceData[_symbol + ":" + interval0] as Series : new Series() };
                Series[] midTermIndexSeries = new Series[1] { (_referenceData.ContainsKey(_symbol + ":" + interval1)) ? _referenceData[_symbol + ":" + interval1] as Series : new Series() };

                var shortTermCurrentBarIndex = shortTermTimes.Count - 1 - extra;
                var midTermCurrentBarIndex = midTermTimes.Count - 1 - extra;

                var mpst = new ModelPredictions();
                mpst.predict(_symbol, interval0, _activeModelNames, _barCache);

                var mpmt = new ModelPredictions();
                mpmt.predict(_symbol, interval1, _activeModelNames, _barCache);

                var modelName = (_activeModelNames.Count > 0) ? _activeModelNames[0] : "";

                var aodInput = new AOD1Input();
                aodInput.Symbol = _symbol;
                aodInput.Interval = interval0;
                var model = MainView.GetModel(_activeModelNames[0]);
                aodInput.Senario = (model == null) ? "" : MainView.GetSenarioLabel(model.Scenario);
                aodInput.ShortTermIndex = shortTermCurrentBarIndex;
                aodInput.MidTermIndex = midTermCurrentBarIndex;
                aodInput.ShortTermTimes = shortTermTimes;
                aodInput.MidTermTimes = midTermTimes;
                aodInput.LongTermTimes = longTermTimes;
                aodInput.ShortTermSeries = shortTermSeries;
                aodInput.MidTermSeries = midTermSeries;
                aodInput.LongTermSeries = longTermSeries;
                aodInput.ShortTermIndexSeries = shortTermIndexSeries;
                aodInput.MidTermIndexSeries = midTermIndexSeries;
                aodInput.ShortTermPredictions = mpst.getPredictions(_symbol, interval0, modelName);
                aodInput.ShortTermActuals = mpst.getActuals(_symbol, interval0, modelName);
                aodInput.MidTermPredictions = mpmt.getPredictions(_symbol, interval1, modelName);
                aodInput.MidTermActuals = mpmt.getActuals(_symbol, interval1, modelName);

                AOD.update(aodInput);
                _drawAod = true;
            }
        }
        private void symbolBarChanged(object sender, BarEventArgs e)
        {
            if (e.Type == BarEventArgs.EventType.BarsReceived)
            {
                string ticker = e.Ticker;
                string interval = e.Interval;

                if (_indexBarRequestCount > 0)
                {
                    Series[] indexSeries = _barCache.GetSeries(ticker, interval, new string[] { "Close" }, 0, 200);
                    if (indexSeries.Length > 0 && indexSeries[0].Count > 0)
                    {
                        _referenceData[ticker + ":" + interval] = indexSeries[0];
                    }

                    if (--_indexBarRequestCount == 0)
                    {
                        requestSymbolBars();
                    }
                }
                else if (_calculating)
                {
                    lock (_calculatedSymbols)
                    {
                        Symbol member = _memberList.Find(x => ticker == x.Ticker);
                        if (member != null && !_calculatedSymbols.Contains(ticker +":" + interval))
                        {
                            calculateTrades(ticker, interval);
                        }
                    }
                }
                else if (isMember(ticker) && interval == _interval)
                {
                    //if (!_predictionThreadRunning)
                    //{
                    //    startPredictionThread();
                    //}
                    //lock (_predictionRequests)
                    //{
                    //    if (!_predictions.ContainsKey(ticker) || _predictions[ticker].Count == 0)
                    //    {
                    //        Debug.WriteLine("Request prediction for " + ticker + " " + interval);
                    //        _predictionRequests.Enqueue(new PredictionRequest(ticker, interval));
                    //    }
                    //}
                }
                if (ticker == _symbol)
                {
                    _updateAod = true;
                }
            }
        }

        private bool isMember(string ticker)
        {
            var symbol = _memberList.Find(x => x.Ticker == ticker);
            return symbol != null;
        }

        private void startPredictionThread()
        {
            _predictionThreadRunning = true;
            _predictionThread = new Thread(new ThreadStart(predict));
            _predictionThread.Priority = ThreadPriority.Lowest;
            _predictionThread.Start();
        }

        private void stopPredictionThread()
        {
            if (_predictionThread != null)
            {
                _predictionThreadRunning = false;
                _predictionThread.Join();
            }
        }

        Thread _predictionThread = null;
        bool _predictionThreadRunning = false;
        Queue<PredictionRequest> _predictionRequests = new Queue<PredictionRequest>();
        Dictionary<string, List<int>> _predictions = new Dictionary<string, List<int>>();
        int _predictionIndex = 0; // default is current

        class PredictionRequest
        {
            public PredictionRequest(string t, string i)
            {
                ticker = t;
                interval = i;
            }
            public string ticker;
            public string interval;
        }

        private void predict()
        {
            while (_predictionThreadRunning)
            {
                PredictionRequest request = null;
                lock (_predictionRequests)
                {
                    if (_predictionRequests.Count > 0)
                    {
                        request = _predictionRequests.Dequeue();
                    }
                }

                if (request != null)
                {
                    var prediction = predict(request.ticker, request.interval);
                    //if (prediction == 0)
                    //{
                    //    lock (_predictionRequests)
                    //    {
                    //        _predictionRequests.Enqueue(request);
                    //    }
                    //}

                    Debug.WriteLine("Prediction for " + request.ticker + " " + request.interval + " equals " + prediction);
                    lock (_predictions)
                    {
                        _predictions[request.ticker] = prediction;
                    }
                    _predictionCount++;
                    _updateMemberList = 2;
                }
                Thread.Sleep(100);
            }
        }

        private List<int> predict(string ticker, string interval)
        {
            List<int> output = new List<int>(); 
            if (_activeModelNames.Count > 0)
            {
                var modelName = _activeModelNames[0];
                var model = MainView.GetModel(modelName);
                if (model != null)
                {
                    var split = (100 - int.Parse(model.MLSplit.Substring(0, 2))) / 100.0;
                    var predictionAgoCount = (int)(split * int.Parse(model.MLMaxBars)) + 1;

                    var tickers = new List<string>();
                    tickers.Add(ticker); 

                    var pathName = @"senarios\" + model.Name + @"\" + interval + (model.UseTicker ? @"\" + MainView.ToPath(_symbol) : "");
                    var data = atm.getModelData(model.FeatureNames, model.Scenario, _barCache, tickers, interval, 2, model.MLSplit, false, false);
                    atm.saveModelData(pathName + @"\test.csv", data);
                    var predictions = MainView.AutoMLPredict(pathName, MainView.GetSenarioLabel(model.Scenario));
                    if (predictions.ContainsKey(ticker) && predictions[ticker].ContainsKey(interval))
                    {
                        output.Clear();
                        output.Add(0);
                        output.Add(0);
                        var times = predictions[ticker][interval].Keys.ToList();
                        if (times.Count > 0) {
                            var time2 = times.Max<DateTime>();
                            output[1] = (predictions[ticker][interval][time2] == 1) ? 1 : -1; // plus 1
                            if (times.Count > 1)
                            {
                                var time1 = times.OrderByDescending(x => x).Skip(1).First();
                                output[0] = (predictions[ticker][interval][time1] == 1) ? 1 : -1; // current
                            }
                        }
                    }
                }
            }
            return output;
        }

        private void calculateTrades(string symbol, string interval)
        {
            AccuracyParser parser = new AccuracyParser(_barCache, interval, _referenceData);

            Strategy strategy = _strategies[_selectedStrategy];

            Series l1 = parser.GetSignals(symbol, strategy.LongEntry);

            int idx1 = strategy.LongExit.IndexOf("Exit Time ");
            int count1 = (idx1 >= 0) ? int.Parse(strategy.LongExit.Substring(idx1 + 10)) : 0;
            Series l2 = (count1 > 0) ? l1.ShiftRight(count1) : parser.GetSignals(symbol, strategy.LongExit);

            Series s1 = parser.GetSignals(symbol, strategy.ShortEntry);

            int idx2 = strategy.ShortExit.IndexOf("Exit Time ");
            int count2 = (idx2 >= 0) ? int.Parse(strategy.ShortExit.Substring(idx2 + 10)) : 0;
            Series s2 = (count2 > 0) ? s1.ShiftRight(count2) : parser.GetSignals(symbol, strategy.ShortExit);

            bool update1 = calculateTrades(symbol, interval, 1, l1, l2);
            bool update2 = calculateTrades(symbol, interval, -1, s1, s2);

            if (update1 || update2) _updateMemberList = 3;
        }

        private bool calculateTrades(string symbol, string interval, int direction, Series entry, Series exit)
        {
            bool tradeAdded = false;
            if (entry != null && exit != null)
            {
                var times = (_barCache.GetTimes(symbol, interval, 0));
                var bars = _barCache.GetSeries(symbol, interval, new string[] { "Open", "High", "Low", "Close" }, 0);

                int count = entry.Count;
                double entryPrice = double.NaN;
                DateTime entryTime = DateTime.Now;
                int entryIndex = 0;
                for (int ii = 0; ii < count; ii++)
                {
                    if (!double.IsNaN(entryPrice) && exit[ii] == 1)
                    {
                        // close trade
                        tmsg.IdeaIdType id = new tmsg.IdeaIdType();
                        id.BloombergIdSpecified = false;
                        id.ThirdPartyId = Guid.NewGuid().ToString();

                        double exitPrice = bars[3][ii];
                        DateTime exitTime = times[ii];

                        double maxProfit = double.MinValue;
                        int maxProfitIndex = 0;
                        for (int jj = entryIndex; jj <= ii; jj++)
                        {
                            double close = bars[3][jj];
                            double profit = close - entryPrice;
                            if (direction < 0) profit = -profit;
                            if (profit > maxProfit)
                            {
                                maxProfit = profit;
                                maxProfitIndex = jj - entryIndex;
                            }
                        }

                        Trade trade = new Trade("US 100", symbol, id, direction, 0, entryTime, entryPrice, exitTime, exitPrice, false, maxProfitIndex);

                        lock (_trades)
                        {
                            if (!_trades.ContainsKey(interval))
                            {
                                _trades[interval] = new List<Trade>();
                            }
                            _trades[interval].Add(trade);
                        }
                        tradeAdded = true;
                        entryPrice = double.NaN;

                    }

                    if (entry[ii] == 1)
                    {
                        entryTime = times[ii];
                        entryPrice = bars[3][ii];
                        entryIndex = ii;
                    }
                }

                if (!double.IsNaN(entryPrice))
                {
                    // open trade
                    tmsg.IdeaIdType id = new tmsg.IdeaIdType();
                    id.BloombergIdSpecified = false;
                    id.ThirdPartyId = Guid.NewGuid().ToString();

                    DateTime startTime = getTradeOpenDate(symbol);
                    if (startTime > entryTime)
                    {
                        entryTime = startTime;
                        entryPrice = getPrice(symbol, double.NaN, entryTime.ToString());
                    }
                    Trade trade = new Trade("US 100", symbol, id, direction, 0, entryTime, entryPrice, DateTime.MaxValue, double.NaN, true);
                    lock (_trades)
                    {
                        if (!_trades.ContainsKey(interval))
                        {
                            _trades[interval] = new List<Trade>();
                        }
                        _trades[interval].Add(trade);
                    }
                    tradeAdded = true;
                }

                lock (_calculatedSymbols)
                {
                    if (direction < 0) _calculatedSymbols.Add(symbol + ":" + interval);

                    if (_calculatedSymbols.Count == 3 * _symbols.Count)
                    {
                        _calculating = false;
                    }
                }
            }
            return tradeAdded;
        }

        private DateTime getTradeOpenDate(string ticker)
        {
            TradeHorizon horizon = Trade.Manager.GetIntervalTradeHorizon(_interval);
            DateTime output = Trade.Manager.getOpenDate(getPortfolioName(), horizon, ticker);
            return output;
        }

        private double getPrice(string symbol, double price, string date)
        {
            if (double.IsNaN(price))
            {
                DateTime date1 = DateTime.Parse(date);
                List<Bar> bars = _barCache.GetBars(symbol, _interval, 0, 100);
                foreach (var bar in bars)
                {
                    if (bar.Time.Year == date1.Year && bar.Time.Month == date1.Month && bar.Time.Day == date1.Day)
                    {
                        price = bar.Close;
                        break;
                    }
                }
            }
            return price;
        }

        int _predictionTotal = 0;
        int _predictionCount = 0;

        void requestSymbolBars()
        {
            return;

            lock (_trades)
            {
                _trades.Clear();
            }

            string interval1 = _interval;
            string interval2 = Study.getForecastInterval(interval1, 1);
            string interval3 = Study.getForecastInterval(interval2, 1);
            string interval4 = Study.getForecastInterval(interval3, 1);

            lock (_predictionRequests)
            {
                _predictionRequests.Clear();
            }
            lock (_predictions)
            {
                _predictions.Clear();
            }

            _predictionTotal = _memberList.Count;
            _predictionCount = 0;

            _symbolBarCache.RequestBars(_symbol, interval1);
            _symbolBarCache.RequestBars(_symbol, interval2);
            _symbolBarCache.RequestBars(_symbol, interval3);
            _symbolBarCache.RequestBars(_symbol, interval4);
    }

        private void symbolBarChangedOld(object sender, BarEventArgs e)
        {
            if (e.Type == BarEventArgs.EventType.BarsReceived)
            {
                List<string> intervals = getIntervals();
                string interval = e.Interval;

                string ticker = e.Ticker;

                if (_indexBarRequestCount > 0)
                {
                    Series[] indexSeries = _barCache.GetSeries(ticker, interval, new string[] { "Close" }, 0);
                    if (indexSeries.Length > 0 && indexSeries[0].Count > 0)
                    {
                        _referenceData[ticker + ":" + interval] = indexSeries[0];
                    }

                    if (--_indexBarRequestCount == 0)
                    {
                        this.Dispatcher.Invoke(DispatcherPriority.Normal, (Action)delegate() { startSymbolColoring(); });
                    }
                }
                else if (intervals.Contains(interval))
                {
                    if (_symbolColors.ContainsKey(ticker))
                    {
                         calculateSymbolColor(ticker, interval, true);
                        _updateMemberList = 4;
                    }
                }
            }
        }

        private void calculateSymbolColor(string symbol, string interval, bool calculate)
        {
            double signal = getSymbolSignals(symbol, interval, _condition, calculate);

            Color color = getConditionColor(symbol, _condition, signal);
            lock (_symbolColors)
            {
                _symbolColors[symbol] = color;
            }
        }

        private void startSymbolColoring()
        {
            List<string> tickers = new List<string>();

            lock (_memberListLock)
            {
                foreach (Symbol symbol in _memberList)
                {
                    string ticker = symbol.Ticker;
                    tickers.Add(ticker);
                }
            }

            List<StackPanel> stackPanels = new List<StackPanel>();
            stackPanels.Add(NavCol2);
            stackPanels.Add(NavCol3);
            stackPanels.Add(NavCol4);
            stackPanels.Add(NavCol5);
            stackPanels.Add(NavCol6);
            int count = stackPanels.Count;
            for (int ii = 0; ii < count; ii++)
            {
                foreach (FrameworkElement element in stackPanels[ii].Children)
                {
                    SymbolLabel label = element as SymbolLabel;
                    if (label != null)
                    {
                        string ticker = label.Symbol;
                        string[] field = ticker.Split(' ');
                        if (field.Length == 1) ticker += " Index";
                        tickers.Add(ticker);
                    }
                }
            }

            Dictionary<string, Color> symbolColors = new Dictionary<string, Color>();
            foreach (string ticker in tickers)
            {
                Color color;
                if (_condition == "OFF" || !_symbolColors.TryGetValue(ticker, out color))
                {
                    color = Color.FromRgb(0xff, 0xff, 0xff);
                }
                symbolColors[ticker] = color;
            }

            _symbolColors = symbolColors;

            if (_condition != "OFF")
            {
                string[] intervals = getIntervals().ToArray();
                foreach (string interval in intervals)
                {
                    foreach (string ticker in tickers)
                    {
                        _symbolBarCache.RequestBars(ticker, interval);
                    }
                }
            }
        }

        private Color getConditionColor(string ticker, string condition, double value)
        {
            int color = 0x808080;
            if (condition == "Trend Condition")
            {
                if (value == 1) color = 0x00ffff;
                else if (value == 2) color = 0x008000;
                else if (value == 3) color = 0x00ff00;
                else if (value == 4) color = 0x0155ff;
                else if (value == 5) color = 0xffffff;
                else if (value == 6) color = 0xcc9900;
                else if (value == 7) color = 0xffff00;
                else if (value == 8) color = 0xff0000;
                else if (value == 9) color = 0xff00ff;
            }
            else if (condition == "Trend Heat")
            {
                if (value == 5) color = 0x00ffff;
                else if (value == 4) color = 0x2a420a;
                else if (value == 3) color = 0xf57ff0d;
                else if (value == 2) color = 0x408123;
                else if (value == 1) color = 0x9ccc5d;
                else if (value == -1) color = 0xd28f0d;
                else if (value == -2) color = 0xf54b0f;
                else if (value == -3) color = 0xff1400;
                else if (value == -4) color = 0x821310;
                else if (value == -5) color = 0x9ccc5d;
            }
            else if (condition == "Trend Strength")
            {
                if (value == 3) color = 0x00ffff;
                else if (value == 2) color = 0x00ff00;
                else if (value == 1) color = 0x008000;
                else if (value == -1) color = 0xff0000;
                else if (value == -2) color = 0xff8000;
                else if (value == -3) color = 0xff00ff;
                else if (value == 0) color = 0xffff00;
            }
            else if (condition == "Score")
            {
                if (value > 85) color = 0x00ffff;  //cyan
                else if (value > 75) color = 0x00ff00;  //lime
                else if (value > 65) color = 0x006600;  //dk green
                else if (value > 55) color = 0x0080ff;  //blue
                else if (value > 45) color = 0xffffff;  //white
                else if (value > 35) color = 0xffff00;  //yellow
                else if (value > 25) color = 0xff8000;  //orange
                else if (value > 15) color = 0xff0000;  //red
                else if (value > 0) color = 0xff00ff;  //magenta
            }
            else if (condition == "Position Ratio")
            {
                if (value == 1.5) color = 0x00ff00;  // Lime
                else if (value == 1.0) color = 0xffff00;  // Yellow
                else if (value == -1.0) color = 0xff8000;  // Orange
                else if (value == -1.5) color = 0xff0000;  // Red
            }
            else if (condition == "PCM Orders")
            {
                if (value == 1.5) color = 0x00ff00;  // Buy Lime
                else if (value == 1.0) color = 0xf0000;  // Sell Red
                else if (value == -1.0) color = 0xff00ff;  // Sell Short Magenta
                else if (value == -1.5) color = 0x00ffff;  // Cover Short Cyan
            }
            byte rr = (byte)((color >> 16) & 0xff);
            byte gg = (byte)((color >> 8) & 0xff);
            byte bb = (byte)(color & 0xff);
            return Color.FromRgb(rr, gg, bb);
        }

        private double getSymbolSignals(string symbol, string interval, string condition, bool calculate)
        {
            double signal = 0;
            lock (_symbolSignals)
            {
                string key = symbol + ":" + interval + ":" + condition;
                if (calculate || !_symbolSignals.TryGetValue(key, out signal))
                {
                    if (condition == "Trend Condition" || condition == "HEAT BARS" || condition == "Trend Strength")
                    {
                        signal = calculateSignal(symbol, interval, condition);
                    }
                    else if (condition == "Score")
                    {
                        signal = calculateScore(symbol, interval, condition);
                    }
                    else if (condition == "Position Ratio")
                    {
                        signal = calculatePositionRatio(symbol, interval, condition);
                    }
                }
            }
            return signal;
        }

        private double calculateSignal(string symbol, string interval, string condition)
        {
            double signal = 0;
            Series[] series = _symbolBarCache.GetSeries(symbol, interval, new string[] { "High", "Low", "Close" }, 0);

            if (series != null)
            {
                Series hi = series[0];
                Series lo = series[1];
                Series cl = series[2];

                if (hi != null && lo != null && cl != null)
                {
                    List<int> signals = atm.getSignals(condition, 1, hi, lo, cl);
                    if (signals != null)
                    {
                        signal = signals[0];
                        string key = symbol + ":" + interval + ":" + condition;
                        _symbolSignals[key] = signal;
                    }
                }
            }
            return signal;
        }

        private double calculateScore(string symbol, string interval, string condition)
        {
            double signal = 0;
            Dictionary<string, List<DateTime>> times = new Dictionary<string, List<DateTime>>();
            Dictionary<string, Series[]> bars = new Dictionary<string, Series[]>();

            string[] intervals = getIntervals().ToArray();

            bool ok = true;
            foreach (string interval1 in intervals)
            {
                times[interval1] = (_symbolBarCache.GetTimes(symbol, interval1, 0));
                bars[interval1] = _symbolBarCache.GetSeries(symbol, interval1, new string[] { "Open", "High", "Low", "Close" }, 0);
                if (times[interval1] == null || times[interval1].Count == 0)
                {
                    ok = false;
                }
            }

            if (ok)
            {
                Dictionary<string, object> referenceData = new Dictionary<string, object>();  // don't need reference data            

                Series scores = Conditions.Calculate("Score", symbol, intervals, 1, times, bars, referenceData);
                int count = scores.Count;
                if (count > 0)
                {
                    signal = scores[count - 1];
                    string key = symbol + ":" + interval + ":" + condition;
                    _symbolSignals[key] = signal;
                }
            }
            return signal;
        }

        private double calculatePositionRatio(string symbol, string interval, string condition)
        {
            double signal = 0;
            Dictionary<string, List<DateTime>> times = new Dictionary<string, List<DateTime>>();
            Dictionary<string, Series[]> bars = new Dictionary<string, Series[]>();

            Dictionary<string, object> referenceData = new Dictionary<string, object>();

            string[] intervals = getIntervals().ToArray();

            string indexSymbol = "";
            if (_referenceData.ContainsKey(symbol + ":" + "REL_INDEX"))
            {
                indexSymbol = _referenceData[symbol + ":" + "REL_INDEX"] as string;
            }

            bool ok = true;
            foreach (string interval1 in intervals)
            {
                times[interval1] = (_symbolBarCache.GetTimes(symbol, interval1, 0));
                bars[interval1] = _symbolBarCache.GetSeries(symbol, interval1, new string[] { "Open", "High", "Low", "Close" }, 0);
                if (times[interval1] == null || times[interval1].Count == 0)
                {
                    ok = false;
                }
            }

            Series[] indexSeries = _symbolBarCache.GetSeries(indexSymbol, intervals[0], new string[] { "Close" }, 0);
            if (indexSeries.Length > 0 && indexSeries[0].Count > 0)
            {
                referenceData["Index Prices : " + intervals[0]] = indexSeries[0];
            }
            else if (!symbol.Contains(" Index"))
            {
                ok = false;
            }

            if (ok)
            {
                Series positionRatios = Conditions.Calculate("Position Ratio 1", symbol, intervals, 1, times, bars, referenceData);
                int count = positionRatios.Count;
                if (count > 0)
                {

                    signal = positionRatios[count - 1];
                    string key = symbol + ":" + interval + ":" + condition;
                    _symbolSignals[key] = signal;
                }
            }
            return signal;
        }

        void print_click(object sender, RoutedEventArgs e)
        {
            MenuItem item = sender as MenuItem;

            PrintDialog dialog = new PrintDialog();
            if (dialog.ShowDialog() == true)
            {
                PrintDocument doc = new PrintDocument();
                doc.PrintPage += new PrintPageEventHandler(doc_PrintPage);
                doc.Print();
           }
        }

        void doc_PrintPage(object sender, PrintPageEventArgs e)
        {
            PrintDocument doc = sender as PrintDocument;
            string portfolioName = getPortfolioName();
            List<Trade> trades = Trade.Manager.getOpenTrades(portfolioName);

            System.Drawing.Font font1 = new System.Drawing.Font("Arial", (float)12.0);
 
            float xx = 10;
            float yy = 10;
            float rowHeight = 15;

            e.Graphics.DrawString(portfolioName, font1, System.Drawing.Brushes.Black, new System.Drawing.PointF(xx, yy));

            int lCnt = 0;
            int sCnt = 0;
            foreach (Trade trade in trades)
            {
                bool isLong = (trade.Direction >= 0);
                xx = isLong ? 10 : 200;
                yy = 10 + (2 + (isLong ? lCnt : sCnt)) * rowHeight;
                e.Graphics.DrawString(trade.Ticker, font1, System.Drawing.Brushes.Black, new System.Drawing.PointF(xx, yy));
                if (isLong)
                {
                    lCnt++;
                }
                else
                {
                    sCnt++;
                }
            }
        }

        void balance_click(object sender, RoutedEventArgs e)
        {
            string name = getPortfolioName();
            double startBalance = _portfolio.GetTradeBalance(name);
            double percent = _portfolio.GetTradePercent(name);
            bool linkAccounts = Portfolio.GetLinkAccounts();
            bool linkCharts = Portfolio.GetLinkCharts();
            //var dlg = new AccountDialog(startBalance, percent, linkAccounts, linkCharts);
            //dlg.Title = "Account Setup";
            //dlg.Style = _mainView.LoadStyle("ChromeDialogStyle");
            //var result = dlg.ShowDialog();
            //if (result == true)
            //{
            //    _portfolio.SetTradeBalance(name, dlg.StartBalance);
            //    _portfolio.SetTradePercent(name, dlg.Percent);
            //    //Portfolio.SetLinkAccounts(dlg.LinkAccounts);
            //    //Portfolio.SetLinkCharts(dlg.LinkCharts);
            //}
        }

        void save_click(object sender, RoutedEventArgs e)
        {
            MenuItem item = sender as MenuItem;

            string portfolioName = getPortfolioName();
            List<Trade> trades = Trade.Manager.getOpenTrades(portfolioName);
            List<string>[] rows = { new List<string>(), new List<string>() };
            foreach (Trade trade in trades)
            {
                int index = (trade.Direction >= 0) ? 0 : 1;
                rows[index].Add(trade.Ticker);
            }

            var dlg = new OpenFileDialog { Title = "Save Trades" };
            dlg.Filter = "Excel documents (.xls)|*.xls";
            var ok = dlg.ShowDialog();
            if (ok.Value)
            {
                using (Stream stream = dlg.OpenFile())
                {
                    StreamWriter sr = new StreamWriter(stream); // user dialog

                    sr.WriteLine(portfolioName);

                    int count = Math.Max(rows[0].Count, rows[1].Count);
                    for (int ii = 0; ii < count; ii++)
                    {
                        sr.Write(((ii < rows[0].Count) ? rows[0][ii] : " "));
                        sr.WriteLine("," + ((ii < rows[1].Count) ? rows[1][ii] : " "));
                    }
                    sr.Close();
                }
            }
        }

        private void onOrderEvent(object sender, OrderEventArgs e)
        {
            _chart1.processOrderEvent(sender, e);
        }

        private void addCharts()
        {
            if (_chart1 == null)
            {
                _chartSymbol1 = "";
                _chartSymbol2 = "";

                bool showCursor = !_mainView.HideChartCursor;

                _chart1 = new Chart(ChartCanvas1, ChartControl1, showCursor, "1");
                _chart2 = new Chart(ChartCanvas2, ChartControl2, showCursor, "2");
                _chart3 = new Chart(ChartCanvas3, ChartControl3, showCursor, "3");

                _chart1.AnalysisEnable = false;
                _chart2.AnalysisEnable = false;
                _chart3.AnalysisEnable = false;

                _chart1.HasTitleIntervals = true;
                _chart2.HasTitleIntervals = true;
                _chart3.HasTitleIntervals = true;

                _chart1.Horizon = 2;
                _chart2.Horizon = 1;
                _chart3.Horizon = 0;

                _chart1.Strategy = _strategy;
                _chart2.Strategy = _strategy;
                _chart3.Strategy = _strategy;

                _chart1.ModelNames = _activeModelNames;
                _chart2.ModelNames = _activeModelNames;
                _chart3.ModelNames = _activeModelNames;

                loadChartProperties();

                _chart1.AddLinkedChart(_chart2);
                _chart1.AddLinkedChart(_chart3);
                _chart2.AddLinkedChart(_chart1);
                _chart2.AddLinkedChart(_chart3);
                _chart3.AddLinkedChart(_chart1);
                _chart3.AddLinkedChart(_chart2);

                addBoxes(true);

                _chart1.ChartEvent += new ChartEventHandler(Chart_ChartEvent);
                _chart2.ChartEvent += new ChartEventHandler(Chart_ChartEvent);
                _chart3.ChartEvent += new ChartEventHandler(Chart_ChartEvent);

                //switchMode(_mode);
                initializeChartPositions();
                positionCharts();

            }
        }

        private void loadChartProperties()
        {
            //_chart1.Indicators["ATM Trend Bars"].Enabled = true;
            //_chart2.Indicators["ATM Trend Bars"].Enabled = true;
            //_chart3.Indicators["ATM Trend Bars"].Enabled = true;

            _chart1.Indicators["ATM Trend Lines"].Enabled = true;
            _chart2.Indicators["ATM Trend Lines"].Enabled = true;
            _chart3.Indicators["ATM Trend Lines"].Enabled = true;

            _chart1.Indicators["ATM Trigger"].Enabled = true;
            _chart2.Indicators["ATM Trigger"].Enabled = true;
            _chart3.Indicators["ATM Trigger"].Enabled = true;

            _chart1.Indicators["ATM 3Sigma"].Enabled = true;
            _chart2.Indicators["ATM 3Sigma"].Enabled = true;
            _chart3.Indicators["ATM 3Sigma"].Enabled = true;

            _chart1.Indicators["ATM Targets"].Enabled = false;
            _chart2.Indicators["ATM Targets"].Enabled = false;
            _chart3.Indicators["ATM Targets"].Enabled = false;

            _chart1.Indicators["X Alert"].Enabled = true;
            _chart2.Indicators["X Alert"].Enabled = true;
            _chart3.Indicators["X Alert"].Enabled = true;

            _chart1.Indicators["First Alert"].Enabled = false;
            _chart2.Indicators["First Alert"].Enabled = false;
            _chart3.Indicators["First Alert"].Enabled = false;

            _chart1.Indicators["Add On Alert"].Enabled = true;
            _chart2.Indicators["Add On Alert"].Enabled = true;
            _chart3.Indicators["Add On Alert"].Enabled = true;

            _chart1.Indicators["Pullback Alert"].Enabled = true;
            _chart2.Indicators["Pullback Alert"].Enabled = true;
            _chart3.Indicators["Pullback Alert"].Enabled = true;

            _chart1.Indicators["Pressure Alert"].Enabled = true;
            _chart2.Indicators["Pressure Alert"].Enabled = true;
            _chart3.Indicators["Pressure Alert"].Enabled = true;

            //_chart1.Indicators["PT Alert"].Enabled = true;
            //_chart2.Indicators["PT Alert"].Enabled = true;
            //_chart3.Indicators["PT Alert"].Enabled = true;

            _chart1.Indicators["Exhaustion Alert"].Enabled = true;
            _chart2.Indicators["Exhaustion Alert"].Enabled = true;
            _chart3.Indicators["Exhaustion Alert"].Enabled = true;

            _chart1.Indicators["Two Bar Alert"].Enabled = false;
            _chart2.Indicators["Two Bar Alert"].Enabled = false;
            _chart3.Indicators["Two Bar Alert"].Enabled = false;

            //_chart1.Indicators["Two Bar Trend"].Enabled = false;
            //_chart2.Indicators["Two Bar Trend"].Enabled = false;
            //_chart3.Indicators["Two Bar Trend"].Enabled = false;

            _chart1.Indicators["FT Alert"].Enabled = false;
            _chart2.Indicators["FT Alert"].Enabled = false;
            _chart3.Indicators["FT Alert"].Enabled = false;

            _chart1.Indicators["ST Alert"].Enabled = false;
            _chart2.Indicators["ST Alert"].Enabled = false;
            _chart3.Indicators["ST Alert"].Enabled = false;

            _chart1.Indicators["FTST Alert"].Enabled = false;
            _chart2.Indicators["FTST Alert"].Enabled = false;
            _chart3.Indicators["FTST Alert"].Enabled = false;

            _chart1.Indicators["EW Counts"].Enabled = false;
            _chart2.Indicators["EW Counts"].Enabled = false;
            _chart3.Indicators["EW Counts"].Enabled = false;

            _chart1.Indicators["EW PTI"].Enabled = false;
            _chart2.Indicators["EW PTI"].Enabled = false;
            _chart3.Indicators["EW PTI"].Enabled = false;

            _chart1.Indicators["EW Projections"].Enabled = false;
            _chart2.Indicators["EW Projections"].Enabled = false;
            _chart3.Indicators["EW Projections"].Enabled = false;

			_chart1.Indicators["Mid Term FT Current"].Enabled = true;
			_chart2.Indicators["Mid Term FT Current"].Enabled = true;
			_chart3.Indicators["Mid Term FT Current"].Enabled = true;

			_chart1.Indicators["Short Term FT Current"].Enabled = true;
            _chart2.Indicators["Short Term FT Current"].Enabled = true;
            _chart3.Indicators["Short Term FT Current"].Enabled = true;

            _chart1.Indicators["Short Term FT Nxt Bar"].Enabled = true;
            _chart2.Indicators["Short Term FT Nxt Bar"].Enabled = true;
            _chart3.Indicators["Short Term FT Nxt Bar"].Enabled = true;

            _chart1.Indicators["Short Term ST Current"].Enabled = true;
            _chart2.Indicators["Short Term ST Current"].Enabled = true;
            _chart3.Indicators["Short Term ST Current"].Enabled = true;

            _chart1.Indicators["Short Term ST Nxt Bar"].Enabled = true;
            _chart2.Indicators["Short Term ST Nxt Bar"].Enabled = true;
            _chart3.Indicators["Short Term ST Nxt Bar"].Enabled = true;

            _chart1.LoadProperties("Positions1_v13");
            _chart2.LoadProperties("Positions2_v13");
            _chart3.LoadProperties("Positions3_v13");
        }

        private void saveChartProperties()
        {
            _chart1.SaveProperties("Positions1_v13");
            _chart2.SaveProperties("Positions2_v13");
            _chart3.SaveProperties("Positions3_v13");
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

        void TradeEvent(object sender, TradeEventArgs e)
        {
            List<Alert> alerts = Alert.Manager.Alerts;
            foreach (Alert alert in alerts)
            {
                alert.FlashEvent -= new FlashEventHandler(alert_FlashEvent);
                alert.FlashEvent += new FlashEventHandler(alert_FlashEvent);
            }
        }

        private void addIntervalChoices(ComboBox comboBox, string choice)
        {
            comboBox.Items.Add("Yearly");
            comboBox.Items.Add("SemiAnnually");
            comboBox.Items.Add("Quarterly");
            comboBox.Items.Add("Monthly");
            comboBox.Items.Add("Weekly");
            comboBox.Items.Add("Daily");
            comboBox.Items.Add("240 Min");
            comboBox.Items.Add("120 Min");
            comboBox.Items.Add("60 Min");
            comboBox.Items.Add("30 Min");
            comboBox.Items.Add("15 Min");
            comboBox.Items.Add("5 Min");
            comboBox.Text = choice;
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

        void Chart_ChartEvent(object sender, ChartEventArgs e)
        {
            if (e.Id == ChartEventType.CursorTime)
            {
                //initializeModelList();
            }
            else if (e.Id == ChartEventType.Update)
            {
                //initializeModelList();
            }
            else if (e.Id == ChartEventType.AODToggle)
            {
                Chart chart = sender as Chart;
                var interval = getIntervalAbbreviation(chart.Interval);

                var visible = AOD.Visibility == Visibility.Visible;
                if (visible && _aodInterval == interval)
                {
                    AOD.Visibility = Visibility.Collapsed;
                    _chart1.SetScoreCardActive(false);
                    _chart2.SetScoreCardActive(false);
                    _chart3.SetScoreCardActive(false);
                }
                else
                {
                    AOD.Clear();
                    AOD.Visibility = Visibility.Visible;
                    _aodInterval = interval;
                    _updateAod = true;

                    _chart1.SetScoreCardActive(chart == _chart1);
                    _chart2.SetScoreCardActive(chart == _chart2);
                    _chart3.SetScoreCardActive(chart == _chart3);
                }
            }
            else if (e.Id == ChartEventType.SettingChange)
            {
                saveChartProperties();
            }
            else if (e.Id == ChartEventType.ExitCharts)
            {
                _spreadsheetChart = false;
                showView();
            }
            else if (e.Id == ChartEventType.ReviewSymbolChange)
            {
                _updateMemberList = 5;
            }
            else if (e.Id == ChartEventType.OutsideResearch)
            {
                _updateMemberList = 6;
            }
            else if (e.Id == ChartEventType.PCMOrder)
            {
                _updateMemberList = 7;
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
            else if (e.Id == ChartEventType.Calculator)
            {
                Chart chart = sender as Chart;
                if (chart == _chart3)
                {
                    updateCalculator(chart);
                }
            }
            else if (e.Id == ChartEventType.PartitionCharts)
            {
                _chartCount--;
                if (_chartCount == 0) _chartCount = 3;

                if (sender == _chart1) _chartNumber = 1;
                else if (sender == _chart2) _chartNumber = 2;
                else if (sender == _chart3) _chartNumber = 3;

                positionCharts();

                updateChart(_symbol, _interval, true);
            }
            else if (e.Id == ChartEventType.TradeEvent) // trade event change
            {

            }
            else if (e.Id == ChartEventType.ChangeSymbol) // change symbol change
            {
                Chart chart = sender as Chart;
                _symbol = chart.SymbolName;

                string[] subSymbol = _symbol.Split(' ');
                _symbolKey = (subSymbol.Length <= 3) ? subSymbol[0] : subSymbol[0] + " " + subSymbol[1];
                updateSymbol();

            }
            else if (e.Id == ChartEventType.ToggleCursor) // toggle cursor on/off
            {
                Chart chart = sender as Chart;
                _mainView.HideChartCursor = !chart.ShowCursor;
            }
            else if (e.Id == ChartEventType.ChartMode)
            {
                //switchMode((_mode == "POSITIONS") ? "PCM RESEARCH" : "POSITIONS");
            }
            else if (e.Id == ChartEventType.SetupConditions)
            {
                Chart chart = sender as Chart;

                ConditionDialog dlg = getConditionDialog();

                int horizon = chart.Horizon;

                dlg.Condition = MainView.GetConditions(horizon);
                dlg.Horizon = horizon;

                ////DialogWindow.Show();
            }
            else if (e.Id == ChartEventType.ConfirmTrade)
            {
                Chart chart = sender as Chart;
                ConfirmationDialog dlg = getConfirmationDialog("Are you sure you want to trade?");
                ////DialogWindow.Show();
            }
            else if (e.Id == ChartEventType.Activate)
            {
                Chart chart = sender as Chart;
                _activeChartInterval = chart.Interval;
                //_updateMemberList = true;
            }
        }
        private void adjustChartPositions()
        {
            if (_view == "CHART")
            {
            }
        }

        private bool displayYieldChart
        {
            get
            {
                return false; //_yieldPortfolio
            }
        }

        private void initializeChartPositions()
        {
            bool allCharts = _chart1.IsVisible() && _chart2.IsVisible() && _chart3.IsVisible(); // (ChartBorder1.Visibility == Visibility.Visible && ChartBorder2.Visibility == Visibility.Visible && ChartBorder3.Visibility == Visibility.Visible);

            if (allCharts) // only one chart is visible
            {
                resetCharts();
                //ChartBorder1.Visibility = Visibility.Visible;
                //ChartBorder2.Visibility = Visibility.Visible;
                //ChartBorder3.Visibility = Visibility.Visible;
                if (_chart1 != null) _chart1.Show();
                if (_chart2 != null) _chart2.Show();
                if (_chart3 != null) _chart3.Show();
                if (displayYieldChart)
                {
                    _chart3.SpreadSymbols = false;
                    CurveGrid.Visibility = Visibility.Visible;
                }
                else
                {
                    //_chart3.SpreadSymbols = true;
                    CurveGrid.Visibility = Visibility.Collapsed;
                }
            }
        }

        int _chartCount = 3;
        int _chartNumber = 0;

        private void positionCharts()
        {
            if (_chartCount == 3)
            {
                resetCharts();
                //ChartBorder1.Visibility = Visibility.Visible;
                //ChartBorder2.Visibility = Visibility.Visible;
                //ChartBorder3.Visibility = Visibility.Visible;
                if (_chart1 != null) _chart1.Show();
                if (_chart2 != null) _chart2.Show();
                if (_chart3 != null)
                {
                    _chart3.Show();
                    if (displayYieldChart)
                    {
                        _chart3.SpreadSymbols = false;
                    }
                    else
                    {
                        _chart3.SpreadSymbols = true;
                    }
                }
            }
            else if (_chartCount == 2)
            {
                resetCharts();
                //ChartBorder1.Visibility = Visibility.Collapsed;
                //ChartBorder2.Visibility = Visibility.Visible;
                //ChartBorder3.Visibility = Visibility.Visible;
                if (_chart1 != null) _chart1.Hide();
                if (_chart2 != null) _chart2.Show();
                if (_chart3 != null)
                {
                    _chart3.Show();
                    if (displayYieldChart)
                    {
                        _chart3.SpreadSymbols = false;
                    }
                    else
                    {
                        _chart3.SpreadSymbols = true;
                    }
                }
            }
            else if (_chartNumber == 1)
            {
                Grid.SetRow(ChartBorder1, 1);
                Grid.SetColumn(ChartBorder1, 0);
                Grid.SetRowSpan(ChartBorder1, 2);
                Grid.SetColumnSpan(ChartBorder1, 2);
                //ChartBorder2.Visibility = Visibility.Collapsed;
                //ChartBorder3.Visibility = Visibility.Collapsed;
                if (_chart1 != null) _chart1.Show();
                if (_chart2 != null) _chart2.Hide();
                if (_chart3 != null) _chart3.Hide();
            }
            else if (_chartNumber == 2)
            {
                Grid.SetRow(ChartBorder2, 1);
                Grid.SetColumn(ChartBorder2, 0);
                Grid.SetRowSpan(ChartBorder2, 2);
                Grid.SetColumnSpan(ChartBorder2, 2);
                //ChartBorder1.Visibility = Visibility.Collapsed;
                //ChartBorder3.Visibility = Visibility.Collapsed;
                if (_chart1 != null) _chart1.Hide();
                if (_chart2 != null) _chart2.Show();
                if (_chart3 != null) _chart3.Hide();
            }   
            else if (_chartNumber == 3)
            {
                Grid.SetRow(ChartBorder3, 1);
                Grid.SetColumn(ChartBorder3, 0);
                Grid.SetRowSpan(ChartBorder3, 2);
                Grid.SetColumnSpan(ChartBorder3, 2);
                //ChartBorder1.Visibility = Visibility.Collapsed;
                //ChartBorder2.Visibility = Visibility.Collapsed;
                if (_chart1 != null) _chart1.Hide();
                if (_chart2 != null) _chart2.Hide();
                if (_chart3 != null) _chart3.Show();
            }
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

        private void updateCalculator(Chart chart)
        {
            _updateCalculator = true;
        }

        private ConfirmationDialog getConfirmationDialog(string message)
        {
            if (_confirmationDialog == null)
            {
                _confirmationDialog = new ConfirmationDialog(_mainView, message);
                _confirmationDialog.DialogEvent += new DialogEventHandler(dialogEvent);
            }
            ////DialogWindow.Content = _confirmationDialog;
            return _confirmationDialog;
        }

        private ConditionDialog getConditionDialog()
        {
            if (_conditionDialog == null)
            {
                _conditionDialog = new ConditionDialog(_mainView);
                _conditionDialog.DialogEvent += new DialogEventHandler(dialogEvent);
            }
            ////DialogWindow.Content = _conditionDialog;
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
            ConfirmationDialog dlg2 = sender as ConfirmationDialog;
            if (dlg2 != null)
            {
                // set confirmation flag in all charts.
                bool ok = (e.Type == DialogEventArgs.EventType.Ok);
                if (_chart1 != null) _chart1.TradeConfirmation = ok;
                if (_chart2 != null) _chart2.TradeConfirmation = ok;
                if (_chart3 != null) _chart3.TradeConfirmation = ok;
            }
            ////DialogWindow.Close();
        }

        private void switchMode(string mode)
        {
            saveChartProperties();

            _mode = mode;

            MainView.SetChartMode(_mode);

            //Mode.Content = _mode;

            loadChartProperties();

            resetCharts();

            if (_view == "CHART")
            {
                updateChart(_symbol, _interval, true);
            }
            else if (_view == "ABSTRACT")
            {
                updateAbstract();
            }
            showView();

            addBoxes(true);
            //ChartBorder1.Visibility = Visibility.Visible;
            //ChartBorder2.Visibility = Visibility.Visible;
            //ChartBorder3.Visibility = Visibility.Visible;
            if (_chart1 != null) _chart1.Show();
            if (_chart2 != null) _chart2.Show();
            if (_chart3 != null) _chart3.Show();
        }
        private void resetCharts()
        {
            if (_chartCount == 3)
            {
                Grid.SetRow(ChartBorder1, 1);
                Grid.SetColumn(ChartBorder1, 0);
                Grid.SetRowSpan(ChartBorder1, 1);
                Grid.SetColumnSpan(ChartBorder1, 1);

                Grid.SetRow(ChartBorder2, 1);
                Grid.SetColumn(ChartBorder2, 1);
                Grid.SetRowSpan(ChartBorder2, 1);
                Grid.SetColumnSpan(ChartBorder2, 1);

                Grid.SetRow(ChartBorder3, 2);
                Grid.SetColumn(ChartBorder3, displayYieldChart ? 1 : 0);
                Grid.SetRowSpan(ChartBorder3, 1);
                Grid.SetColumnSpan(ChartBorder3, displayYieldChart ? 1 : 2);
            }   
            if (_chartCount == 2)
            {
                Grid.SetRow(ChartBorder2, 1);
                Grid.SetColumn(ChartBorder2, 0);
                Grid.SetRowSpan(ChartBorder2, 1);
                Grid.SetColumnSpan(ChartBorder2, 2);

                Grid.SetRow(ChartBorder3, 2);
                Grid.SetColumn(ChartBorder3, 0);
                Grid.SetRowSpan(ChartBorder3, 1);
                Grid.SetColumnSpan(ChartBorder3, 2);
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


            //imageNames.Add((_mode == "POSITIONS") ? @"Images/Research3.png:Research Mode" : @"Images/Position3.png:Positions Mode");  // tile:max
            _chart1.InitializeControlBox(imageNames);
            _chart2.InitializeControlBox(imageNames);
            _chart3.InitializeControlBox(imageNames);
        }

        private string getChartInterval(string interval, int level)
        {
            var output = _chart1.GetOverviewInterval(interval, level);           
            return output;
        }

        private void updateChart(string symbol, string interval, bool initialize)
        {
            addCharts();

            var portfolioName = getPortfolioName();
            _yieldPortfolio = _portfolio.IsYieldPortfolio(portfolioName) && _memberList.Select(x => x.Ticker).Contains(symbol);
            initializeChartPositions();

            string symbol1 = symbol;
            string symbol2 = symbol;
            string interval1 = (_view == "CHART") ? getChartInterval(interval, 2) : _intervals[1];
            string interval2 = (_view == "CHART") ? getChartInterval(interval, 1) : _intervals[2];
            string interval3 = (_view == "CHART") ? getChartInterval(interval, 0) : _intervals[3];
            string interval4 = (_view == "CHART") ? getChartInterval(interval, 3) : _intervals[4];
 
            if (Portfolio.GetLinkCharts())
            {
                interval1 = (_view == "CHART") ? getChartInterval(interval, 1) : _intervals[2];
                interval2 = (_view == "CHART") ? getChartInterval(interval, 1) : _intervals[3];
                interval3 = (_view == "CHART") ? getChartInterval(interval, 0) : _intervals[2];
                interval4 = (_view == "CHART") ? getChartInterval(interval, 0) : _intervals[3];

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
                    symbol1 = (hedge.Length > 0) ? symbol + " - " +  hedge : symbol;
                }
            }

            bool symbolChange1 = (symbol1 != _chartSymbol1);
            bool symbolChange2 = (symbol2 != _chartSymbol2);
            bool intervalChange1 = (interval1 != _chartIntervals[1]);
            bool intervalChange2 = (interval2 != _chartIntervals[2]);
            bool intervalChange3 = (interval3 != _chartIntervals[3]);
            bool intervalChange4 = (interval4 != _chartIntervals[4]);

            if (symbolChange1 || symbolChange2)
            {
                //InvestmentAmt.Text = "";
            }

            if (initialize || symbolChange1 || symbolChange2 || intervalChange1 || intervalChange2 || intervalChange3 || intervalChange4)
            {
                AOD.Clear();

                _chartSymbol1 = symbol1; 
                _chartSymbol2 = symbol2;

                _chartIntervals[1] = interval1;
                _chartIntervals[2] = interval2;
                _chartIntervals[3] = interval3;
                _chartIntervals[4] = interval4;

                interval1 = interval1.Replace(" Min", "");
                interval2 = interval2.Replace(" Min", "");
                interval3 = interval3.Replace(" Min", "");
                interval4 = interval4.Replace(" Min", "");

                if (_chart1.IsVisible()) _chart1.Change(symbol1, interval1);
                if (_chart2.IsVisible()) _chart2.Change(symbol2, interval2);
                if (_chart3.IsVisible()) _chart3.Change(symbol1, interval3);

                _chart1.Strategy = _strategy;
                _chart2.Strategy = _strategy;
                _chart3.Strategy = _strategy;
            }

            highlightIntervalButton(ChartIntervals, interval3);

            initializeCalculator();
        }

        private void initializeCalculator()
        {
        }
        
        private string getInterval(int number)
        {
            string interval = _intervals[number];
            if (interval == "Yearly") interval = "Y";
            else if (interval == "SemiAnnually") interval = "S";
            else if (interval == "Quarterly") interval = "Q";
            else if (interval == "Monthly") interval = "M";
            else if (interval == "Weekly") interval = "W";
            else if (interval == "Daily") interval = "D";
            else interval = interval.Replace(" Min", "");
            return interval;
        }

        string _overlayPortfolioName = "";

        private void update(string nav1, string nav2, string nav3, string nav4, string nav5, string nav6)
        {

            // RBAC gate: block loading TEST ML portfolios for non-admin users.
            // Single chokepoint covering chart/alert update paths from every caller.
            if (nav1 == "ML PORTFOLIOS >"
                && !ATMML.Auth.AuthContext.Current.IsAdmin
                && !string.IsNullOrEmpty(nav2)
                && !ModelAccessGate.IsLive(nav2))
            {
                return;  // silently refuse TEST ML portfolio load
            }

            if (_setOverlayPortfolio)
            {
                _overlayPortfolioName = getPortfolioName(nav2, nav3, nav4, nav5, nav6, _clientPortfolioName);

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
                SpreadsheetGroup.Content = (_clientPortfolioName.Length > 0) ? name : getGroupLabel();
                var type = getPortfolioType(name, _clientPortfolioName);

                NotifyPropertyChanged("YieldTitle");

                Condition.Content = _condition;

                PortfolioPanel.Children.Clear();
                Spreadsheet.Children.Clear();

                requestPortfolio(name, type);
            }

            updateScenarioLabel();
 
            hideNavigation();
            showView();
        }

        private void updateScenarioLabel()
        {
            if (_activeModelNames.Count > 0)
            {
                var modelName = _activeModelNames[0];
                var model = MainView.GetModel(modelName);
                if (model != null)
                {
                    ScenarioLabel.Content = MainView.GetSenarioLabel(model.Scenario);
                }
            }
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
            else if (name.Contains(" Index"))
            {
                type = Portfolio.PortfolioType.Single;
            }
            else if (name.Contains(" Curncy"))
            {
                type = Portfolio.PortfolioType.Single;
            }
            else if (name.Contains(" Equity"))
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

        DateTime _time1 = new DateTime();

        private void requestPortfolio(string name, Portfolio.PortfolioType type)
        {
            //System.Diagnostics.Debug.WriteLine("Request Portfolio");
            _time1 = DateTime.Now;
            _portfolio.Clear();
            _barCache.Clear();
            setDefaultIntervals();
            _type = type;
            _portfolioRequestedCount = 0;
            _predictions.Clear();

            _memberList.Clear();
            _updateMemberList = 8;

            //System.Diagnostics.Trace.WriteLine("Charts " + name + " " + type);

            if (type == Portfolio.PortfolioType.Model && name == "COM")
            {
                type = Portfolio.PortfolioType.BuiltIn;
                name = "TIM2";
            }

            var fields = name.Split(',');
            _portfolioRequestedCount = fields.Length;
            //Trace.WriteLine("Request portfolio start" + name);
            _portfolioTime = DateTime.Now;
            foreach (var field in fields)
            {
                _portfolio.RequestSymbols(field, type, false);
            }
        }

        DateTime _portfolioTime;
        void requestYieldMembers(string name)
        {
            if (_yieldPortfolio)
            {
                string yieldTicker = _portfolio.GetYieldTicker(name);

                yieldTicker = yieldTicker.Replace(" Index", "");
                _portfolio.RequestSymbols(yieldTicker, Portfolio.PortfolioType.Index, false);
            }
        }

        bool _requestSymbolBars = false;

        void requestReferenceData(List<Symbol> symbols)
        {
            _referenceData.Clear();

            int count = symbols.Count;

            _requestSymbolBars = true;

            _referenceDataCount = 0;
            for (int ii = 0; ii < count; ii++)
            {
                string ticker = symbols[ii].Ticker;
                if (!ticker.Contains(" Index"))
                {
                    _referenceDataCount++;
                }
            }

            for (int ii = 0; ii < count; ii++)
            {
                string[] dataFieldNames = { "REL_INDEX", "DS192" };
                _portfolio.RequestReferenceData(symbols[ii].Ticker, dataFieldNames);
            }
        }

        int _referenceDataCount = 0;
        int _indexBarRequestCount = 0;
        bool _calculateMemberList = false;

        void portfolioChanged(object sender, PortfolioEventArgs e)
        {
            if (e.Type == PortfolioEventType.Symbol)
            {

                if (_memberPanel != null)
                {
                    this.Dispatcher.Invoke(new Action(() => { loadMemberPanel(); }));
                }
                else
                {
                    if (_compareToSelection)
                    {
                        _compareToSelection = false;
                        _compareSymbols = _portfolio.GetSymbols();
                        _compareSymbols.ForEach(x => _barCache.RequestBars(x.Ticker, "D", false, BarServer.MaxBarCount));
                        //this.Dispatcher.Invoke(DispatcherPriority.Normal, (Action)delegate () { drawYieldCurve(); });
                    }
                    else if (_portfolioRequestedCount > 0)
                    {
                        //Trace.WriteLine("Portfolio recieved " + (DateTime.Now - _portfolioTime).TotalSeconds);

                        _portfolioRequestedCount--;

                        var memberList = _portfolio.GetSymbols();

                        if (_portfolioRequestedCount == 0)
                        {
                            var ml = new List<Symbol>();
                            foreach (var member in memberList)
                            {
                                Symbol symbol = new Symbol(member.Ticker, member.Description, member.Group, member.Sector, member.Size1, member.Size2, member.Time1);
                                symbol.Interval = member.Interval;
                                ml.Add(symbol);
                            }

                            requestReferenceData(ml);

                            lock (_memberList)
                            {
                                _memberList = ml;
                            }

                            if (ml.Count > 0 && ml.FindIndex(x => x.Ticker == _symbol) == -1)
                            {
                                var tickers = ml.OrderBy(x => x.Description).Select(x => x.Ticker).ToList();
                                this.Dispatcher.Invoke(DispatcherPriority.Normal, (Action)delegate () { updateChart(tickers[0], _interval, false); });
                            }

                            _calculateMemberList = false;
                            _updateMemberList = 9;
                        }
                    }
                }
            }
            else if (e.Type == PortfolioEventType.ReferenceData)
            {
                foreach (KeyValuePair<string, object> kvp in e.ReferenceData)
                {
                    string name = kvp.Key;
                    object value = kvp.Value;
                    if (name == "REL_INDEX")
                    {
                        string relIdx = value as string;
                        if (relIdx.Length > 0)
                        {
                            relIdx += " Index";
                            string key = e.Ticker + ":" + name;
                            _referenceData[key] = relIdx;
                        }
                    }
                    else
                    {
                        if (name == "DS192")
                        {
                            foreach (var member in _memberList)
                            {
                                if (member.Ticker == e.Ticker)
                                {
                                    if (member.Description.Length == 0)
                                    {
                                        member.Description = value as string;
                                    }
                                    break;
                                }
                            }
                        }
                    }
                }

                if (_referenceData.Count == _referenceDataCount)
                {
                    List<string> indexSymbols = new List<string>();
                    foreach (KeyValuePair<string, object> kvp in _referenceData)
                    {
                        string key = kvp.Key;
                        if (key.Contains("REL_INDEX"))
                        {
                            string indexSymbol = kvp.Value as string;
                            if (!indexSymbols.Contains(indexSymbol))
                            {
                                indexSymbols.Add(indexSymbol);
                            }
                        }
                    }

                    List<string> intervals = getIntervals();
                    string indexInterval = intervals[0];

                    _indexBarRequestCount = indexSymbols.Count;

                    if (_indexBarRequestCount > 0)
                    {
                        foreach (string symbol in indexSymbols)
                        {
                            _symbolBarCache.RequestBars(symbol, indexInterval, true);
                        }
                    }
                    else if (_requestSymbolBars)
                    {
                        _requestSymbolBars = false;
                        requestSymbolBars();
                    }
                }
            }
        }
        private void loadMemberPanel()
        {
            var members = _portfolio.GetSymbols();

            var items = new List<string>();
            items.Add(_memberIndex);
            var fields = _memberIndex.Split(' ');
            items.Add(fields[0] + ":" + fields[0] + " All Members");
            for (int ii = 0; ii < members.Count; ii++)
            {
                var member = members[ii];
                fields = member.Ticker.Split(' ');
                items.Add(member.Ticker + ":" + fields[0]);
            }
            nav.setNavigation(_memberPanel, Member_MouseDown, items.ToArray());
            _memberPanel = null;

            setCheckBoxes();
        }

        private void setCheckBoxes()
        {
            nav.SetCheckBoxes();
        }

        private void Member_MouseDown(object sender, MouseButtonEventArgs e)
        {
        }

        public List<string> getIntervals()
        {
            // short and mid term
            List<string> outputs = new List<string>();
            outputs.Add(_chartIntervals[3].Replace(" Min", ""));
            outputs.Add(_chartIntervals[2].Replace(" Min", ""));
            return outputs;
        }

        public List<string> getIndexIntervals()
        {
            // short term only
            List<string> outputs = new List<string>();
            outputs.Add(_chartIntervals[3].Replace(" Min", ""));
            return outputs;
        }

        void updateSymbol()
        {
            SymbolInfo symbolInfo;
            if (_symbols.TryGetValue(_symbolKey, out symbolInfo))
            {
                if (_symbol != symbolInfo.Symbol)
                {
                    _symbol = symbolInfo.Symbol;

                    TradeHorizon tradeHorizon = getTradeHorizon();
                    int size = Trade.Manager.getTradeOpenDirection(getPortfolioName(), tradeHorizon, _symbol);
                    double price = Trade.Manager.getTradeOpenPrice(tradeHorizon, _symbol);

                    var interval = symbolInfo.Interval;
                    if (interval.Length > 0)
                    {
                        _interval = interval;
                        _aodInterval = interval;
                        _updateAod = true;
                    }

                    highlightIntervalButton(ChartIntervals, _interval.Replace(" Min", ""));

                    if (_view == "CHART")
                    {
                        updateChart(_symbol, _interval, false);
                    }

                    setInfo();
                }
            }
            var portfolioName = getPortfolioName();
            _yieldPortfolio = _portfolio.IsYieldPortfolio(portfolioName) && _memberList.Select(x => x.Ticker).Contains(_symbol);
            initializeChartPositions();
        }

        private void updateAbstract()
        {
            _barCache.Clear();
            _abstractSignals.Clear();

            string interval1 = _intervals[1].Replace(" Min", "");
            string interval2 = _intervals[2].Replace(" Min", "");
            string interval3 = _intervals[3].Replace(" Min", "");

            _barCache.RequestBars(_symbol, interval1);
            _barCache.RequestBars(_symbol, interval2);
            _barCache.RequestBars(_symbol, interval3);
        }

        private void drawBorder(int number)
        {
            string interval = getInterval(number);

            string signal;
            if (_abstractSignals.TryGetValue(interval + ":" + "TSBSig", out signal))
            {
                string tsb = signal.Substring(7, 1);
                Brush brush = (tsb == "1") ? Brushes.Lime : (tsb == "2") ? Brushes.Red : Brushes.DarkOrange;
                Border border = FindName("Border" + number.ToString()) as Border;
                if (border != null)
                {
                    border.BorderBrush = brush;
                }
            }
        }

        private void drawXSignals(int number)
        {
            string interval = getInterval(number);

            Canvas canvas = FindName("Signal" + number.ToString()) as Canvas;
            if (canvas != null)
            {
                canvas.Children.Clear();

                int barIndex = _ago[number];

                Brush brush1 = Brushes.Lime;
                Brush brush2 = Brushes.Red;
                Brush brush3 = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x66));
                Brush brush4 = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33));
                Brush brush5 = new SolidColorBrush(Color.FromRgb(0x00, 0xcc, 0xfc)); // cursor

                double width = canvas.ActualWidth;
                double height = canvas.ActualHeight;

                double lMargin = 17;
                double tMargin = 10;
                double rMargin = 16;
                double bMargin = 8;

                double xDiv = (width - (lMargin + rMargin)) / 7;
                double yDiv = (height - (tMargin + bMargin)) / 6;

                double xRadius = 7;
                double yRadius = 9;
                double barHeight = 5;

                Line cursor = new Line();
                cursor.Stroke = brush5;
                cursor.StrokeThickness = 0.5;
                double cursorxx = lMargin + (8 - barIndex) * xDiv;
                cursor.X1 = cursorxx;
                cursor.Y1 = 0;
                cursor.X2 = cursorxx;
                cursor.Y2 = height;
                canvas.Children.Add(cursor);

                for (int ii = 0; ii < 9; ii++)
                {
                    double yy = (ii < 5) ? tMargin + (ii * yDiv) : tMargin + (5 + (ii - 5) / 2) * yDiv + (((ii % 2) == 0) ? -barHeight / 2 - 1 : barHeight / 2 + 1);
                    Line line1 = new Line();
                    line1.Stroke = brush3;
                    line1.StrokeThickness = 0.5;
                    line1.X1 = 0;
                    line1.X2 = width;
                    line1.Y1 = yy;
                    line1.Y2 = yy;
                    canvas.Children.Add(line1);
                }

                for (int ii = 0; ii < 8; ii++)
                {
                    double xx = lMargin + (ii * xDiv);
                    Line line1 = new Line();
                    line1.Stroke = brush4;
                    line1.StrokeThickness = 0.5;
                    line1.X1 = xx;
                    line1.X2 = xx;
                    line1.Y1 = 00;
                    line1.Y2 = height;
                    canvas.Children.Add(line1);
                }

                int[] row1 = new int[] { 0, 0, 1, 2, 3, 4 };
                string[] name1 = new string[] { "Exh", "DivSig", "PB", "AOA", "FA", "XAlert", "ADXUp", "ADXDn", "FT Alert", "ST Alert", "FTST Alert"};
                //string[] name1 = new string[] { "Exh", "DivSig", "PB", "AOA", "FA", "XAlert", "ADXUp", "ADXDn", "FT Alert", "ST Alert", "FTST Alert", "PT Alert", "Two Bar Trend" };
                int count1 = row1.Length;
                for (int ii = 0; ii < count1; ii++)
                {
                    string signal;
                    if (_abstractSignals.TryGetValue(interval + ":" + name1[ii], out signal))
                    {
                        double yy = tMargin + row1[ii] * yDiv - yRadius / 2;

                        for (int jj = 0; jj < 8; jj++)
                        {
                            double xx = lMargin + (jj * xDiv) - xRadius / 2;

                            string sig = signal.Substring(jj, 1);
                            if (sig == "1" || sig == "2")
                            {
                                Ellipse circle = new Ellipse();
                                circle.Fill = (sig == "1") ? brush1 : brush2;
                                Canvas.SetLeft(circle, xx);
                                Canvas.SetTop(circle, yy);
                                circle.Width = xRadius;
                                circle.Height = yRadius;
                                canvas.Children.Add(circle);
                            }
                        }
                    }
                }

                int[] row2 = new int[] { 5, 6 };
                string[] name2 = new string[] { "TLSig", "TBSig" };
                int count2 = row2.Length;
                for (int ii = 0; ii < count2; ii++)
                {
                    double xxUp = 0;
                    double xxDn = 0;
                    string lines1 = "3";
                    string signal;
                    if (_abstractSignals.TryGetValue(interval + ":" + name2[ii], out signal))
                    {
                        double yy = tMargin + row2[ii] * yDiv - barHeight / 2;

                        for (int jj = 0; jj <= 8; jj++)
                        {
                            double xx = lMargin + (jj * xDiv) - (xDiv / 2);

                            string lines2 = (jj < 8) ? signal.Substring(jj, 1) : "3";
                            if (lines1 != "1" && lines2 == "1")
                            {
                                xxUp = xx;
                            }
                            if (lines1 != "2" && lines2 == "2")
                            {
                                xxDn = xx;
                            }

                            bool upLine = lines1 == "1" && lines2 != "1" || lines2 == "1" && ii == 7;
                            bool dnLine = lines1 == "2" && lines2 != "2" || lines2 == "2" && ii == 7;

                            if (upLine || dnLine)
                            {
                                double xxLeft = upLine ? xxUp : xxDn;
                                Rectangle rectangle = new Rectangle();
                                Canvas.SetLeft(rectangle, xxLeft);
                                Canvas.SetTop(rectangle, yy);
                                rectangle.Width = xx - xxLeft;
                                rectangle.Height = barHeight;
                                rectangle.Fill = upLine ? brush1 : brush2;
                                canvas.Children.Add(rectangle);
                            }

                            lines1 = lines2;
                        }
                    }
                }
            }
        }

        private void drawChart(int number)
        {
            Canvas canvas = FindName("Chart" + number.ToString()) as Canvas;
            if (canvas != null)
            {
                canvas.Children.Clear();
                drawCurves(number);
                drawTSB(number);
            }
        }

        private void drawBar(int number)
        {
            string interval = getInterval(number);

            if (_abstractSignals.ContainsKey(interval + ":" + "cl" + "0"))
            {
                Canvas canvas = FindName("Bar" + number.ToString()) as Canvas;
                if (canvas != null)
                {
                    canvas.Children.Clear();

                    Label label1 = FindName("Op" + number.ToString()) as Label;
                    Label label2 = FindName("Cl" + number.ToString()) as Label;
                    Label label3 = FindName("FTTP" + number.ToString()) as Label;
                    Label label4 = FindName("STTP" + number.ToString()) as Label;
                    if (label1 != null && label2 != null && label3 != null && label4 != null)
                    {
                        label1.Content = "";
                        label2.Content = "";
                        label3.Content = "";
                        label4.Content = "";

                        int barIndex = _ago[number];

                        double width = canvas.ActualWidth;
                        double height = canvas.ActualHeight;

                        Brush brush1 = Brushes.White;  // neutral color
                        Brush brush2 = Brushes.Lime;   // up color
                        Brush brush3 = Brushes.Red;    // down color
                        Brush brush4 = Brushes.DarkOrange; // marker color

                        Label label = FindName("Ago" + number.ToString()) as Label;
                        if (label != null)
                        {
                            string intervalLabel = (interval == "M" || interval == "W" || interval == "D") ? " " + interval : "";
                            label.Content = barIndex.ToString() + intervalLabel + " Ago";
                        }

                        string index1 = (barIndex - 1).ToString();
                        string index2 = (barIndex - 0).ToString();

                        double op1 = double.Parse(_abstractSignals[interval + ":" + "op" + index1]);
                        double hi1 = double.Parse(_abstractSignals[interval + ":" + "hi" + index1]);
                        double lo1 = double.Parse(_abstractSignals[interval + ":" + "lo" + index1]);
                        double cl1 = double.Parse(_abstractSignals[interval + ":" + "cl" + index1]);
                        double cl2 = double.Parse(_abstractSignals[interval + ":" + "cl" + index2]);

                        double fp1 = double.Parse(_abstractSignals[interval + ":" + "fp" + index1]);
                        double sp1 = double.Parse(_abstractSignals[interval + ":" + "sp" + index1]);

                        double ft1 = double.Parse(_abstractSignals[interval + ":" + "ft" + index1]);
                        double st1 = double.Parse(_abstractSignals[interval + ":" + "st" + index1]);
                        double ft2 = double.Parse(_abstractSignals[interval + ":" + "ft" + index2]);
                        double st2 = double.Parse(_abstractSignals[interval + ":" + "st" + index2]);

                        double barHeight = 100;
                        double barWidth = 8;

                        double xBase = (width - barWidth) / 2;
                        double yBase = (height - barHeight) / 2;

                        double clY = (hi1 > lo1) ? yBase + barHeight * (hi1 - cl1) / (hi1 - lo1) : yBase + barHeight / 2;
                        double opY = (hi1 > lo1) ? yBase + barHeight * (hi1 - op1) / (hi1 - lo1) : yBase + barHeight / 2;

                        Rectangle pressDn = new Rectangle();
                        pressDn.Fill = brush3;
                        Canvas.SetLeft(pressDn, xBase);
                        Canvas.SetTop(pressDn, yBase);
                        pressDn.Width = barWidth;
                        pressDn.Height = Math.Max(0.01, clY - yBase);
                        canvas.Children.Add(pressDn);

                        Rectangle pressUp = new Rectangle();
                        pressUp.Fill = brush2;
                        Canvas.SetLeft(pressUp, xBase);
                        Canvas.SetTop(pressUp, clY);
                        pressUp.Width = barWidth;
                        pressUp.Height = Math.Max(0.01, yBase + barHeight - clY);
                        canvas.Children.Add(pressUp);

                        Polygon opMarker = new Polygon();
                        PointCollection opPointCollection = new PointCollection();
                        opPointCollection.Add(new System.Windows.Point(0, 0));
                        opPointCollection.Add(new System.Windows.Point(8, 8));
                        opPointCollection.Add(new System.Windows.Point(0, 16));
                        opMarker.Points = opPointCollection;
                        Canvas.SetLeft(opMarker, xBase - 6);
                        Canvas.SetTop(opMarker, opY - 8);
                        opMarker.Fill = brush4;
                        canvas.Children.Add(opMarker);

                        Polygon clMarker = new Polygon();
                        PointCollection clPointCollection = new PointCollection();
                        clPointCollection.Add(new System.Windows.Point(8, 0));
                        clPointCollection.Add(new System.Windows.Point(0, 8));
                        clPointCollection.Add(new System.Windows.Point(8, 16));
                        clMarker.Points = clPointCollection;
                        Canvas.SetLeft(clMarker, xBase + barWidth - 2);
                        Canvas.SetTop(clMarker, clY - 8);
                        clMarker.Fill = brush4;
                        canvas.Children.Add(clMarker);

                        label1.Content = (!double.IsNaN(op1)) ? _abstractSignals[interval + ":" + "op" + index1] : "";
                        label1.Margin = new Thickness(2, opY - 12, 2, 0);
                        label1.Foreground = (op1 > cl2) ? brush2 : (op1 < cl2) ? brush3 : brush1;

                        label2.Content = (!double.IsNaN(cl1)) ? _abstractSignals[interval + ":" + "cl" + index1] : "";
                        label2.Margin = new Thickness(2, clY - 12, 2, 0);
                        label2.Foreground = (cl1 > cl2) ? brush2 : (cl1 < cl2) ? brush3 : brush1;

                        label3.Content = (!double.IsNaN(fp1)) ? _abstractSignals[interval + ":" + "fp" + index1] : "";
                        double fpY = yBase + barHeight * (hi1 - fp1) / (hi1 - lo1);
                        label3.Foreground = (ft1 <= ft2) ? brush2 : brush3;
                        if (!double.IsNaN(fpY))
                        {
                            if (fpY < 12) fpY = 12;
                            else if (fpY > height - 12) fpY = height - 12;
                            label3.Margin = new Thickness(2, fpY - 12, 2, 0);
                        }

                        label4.Content = (!double.IsNaN(sp1)) ? _abstractSignals[interval + ":" + "sp" + index1] : "";
                        double spY = yBase + barHeight * (hi1 - sp1) / (hi1 - lo1);
                        label4.Foreground = (st1 <= st2) ? brush2 : brush3;
                        if (!double.IsNaN(spY))
                        {
                            if (spY < 12) spY = 12;
                            else if (spY > height - 12) spY = height - 12;
                            label4.Margin = new Thickness(2, spY - 12, 2, 0);
                        }
                    }
                }
            }
        }

        private void drawCurves(int number)
        {
            string interval = getInterval(number);

            if (_abstractSignals.ContainsKey(interval + ":" + "cl" + "0"))
            {
                Canvas canvas = FindName("Chart" + number.ToString()) as Canvas;
                if (canvas != null)
                {

                    int barIndex = _ago[number];

                    Brush brush1 = Brushes.DarkGray; // bar color
                    Brush brush2 = Brushes.Lime;     // up color
                    Brush brush3 = Brushes.Red;      // down color
                    Brush brush4 = Brushes.Red;      // ob color
                    Brush brush5 = Brushes.Blue;     // os color
                    Brush brush6 = new SolidColorBrush(Color.FromRgb(0x00, 0xcc, 0xfc)); // cursor

                    double width = canvas.ActualWidth;
                    double height = canvas.ActualHeight;

                    int count = 8;

                    double lMargin = 17;
                    double tMargin = 12;
                    double rMargin = 16;
                    double bMargin = 12;

                    double xBase = lMargin;
                    double yBase = height - bMargin;
                    double xScale = (width - (lMargin + rMargin)) / (count - 1);

                    double xDiv = (width - (lMargin + rMargin)) / 7;

                    double max = double.NaN;
                    double min = double.NaN;
                    for (int ii = 0; ii < count; ii++)
                    {
                        string index = (count - 1 - ii).ToString();
                        double hi = double.Parse(_abstractSignals[interval + ":" + "hi" + index]);
                        double lo = double.Parse(_abstractSignals[interval + ":" + "lo" + index]);
                        if (double.IsNaN(max) || hi > max) max = hi;
                        if (double.IsNaN(min) || lo < min) min = lo;
                    }

                    if (double.IsNaN(max) || double.IsNaN(max))
                    {
                        canvas.Children.Clear();
                    }
                    else
                    {
                        if (max == min)
                        {
                            max += .0000001;
                            min -= .0000001;
                        }

                        double yScale1 = (height - (tMargin + bMargin)) / (max - min);
                        double yScale2 = (height - (tMargin + bMargin)) / 100;

                        Line cursor = new Line();
                        cursor.Stroke = brush6;
                        cursor.StrokeThickness = 0.5;
                        double cursorxx = xBase + (8 - barIndex) * xScale;
                        cursor.X1 = cursorxx;
                        cursor.Y1 = 0;
                        cursor.X2 = cursorxx;
                        cursor.Y2 = height;
                        canvas.Children.Add(cursor);

                        Line obLine = new Line();
                        double obyy = yBase - 85 * yScale2;
                        obLine.Stroke = brush4;
                        obLine.StrokeThickness = 0.5;
                        obLine.X1 = lMargin - xDiv / 2;
                        obLine.Y1 = obyy;
                        obLine.X2 = (width - rMargin) + xDiv / 2;
                        obLine.Y2 = obyy;
                        canvas.Children.Add(obLine);

                        Line osLine = new Line();
                        double osyy = yBase - 15 * yScale2;
                        osLine.Stroke = brush5;
                        osLine.StrokeThickness = 0.5;
                        osLine.X1 = lMargin - xDiv / 2;
                        osLine.Y1 = osyy;
                        osLine.X2 = (width - rMargin) + xDiv / 2;
                        osLine.Y2 = osyy;
                        canvas.Children.Add(osLine);

                        for (int ii = 0; ii < count; ii++)
                        {
                            string index = (count - 1 - ii).ToString();
                            double op = double.Parse(_abstractSignals[interval + ":" + "op" + index]);
                            double hi = double.Parse(_abstractSignals[interval + ":" + "hi" + index]);
                            double lo = double.Parse(_abstractSignals[interval + ":" + "lo" + index]);
                            double cl = double.Parse(_abstractSignals[interval + ":" + "cl" + index]);

                            if (!double.IsNaN(op) && !double.IsNaN(hi) && !double.IsNaN(lo) && !double.IsNaN(cl))
                            {
                                double xx = xBase + ii * xScale;
                                double yo = yBase - (op - min) * yScale1;
                                double yh = yBase - (hi - min) * yScale1;
                                double yl = yBase - (lo - min) * yScale1;
                                double yc = yBase - (cl - min) * yScale1;

                                Line line1 = new Line();
                                line1.Stroke = brush1;
                                line1.X1 = xx;
                                line1.Y1 = yh;
                                line1.X2 = xx;
                                line1.Y2 = yl;
                                canvas.Children.Add(line1);

                                Line line2 = new Line();
                                line2.Stroke = brush1;
                                line2.X1 = xx - 3;
                                line2.Y1 = yo;
                                line2.X2 = xx;
                                line2.Y2 = yo;
                                canvas.Children.Add(line2);

                                Line line3 = new Line();
                                line3.Stroke = brush1;
                                line3.X1 = xx + 3;
                                line3.Y1 = yc;
                                line3.X2 = xx;
                                line3.Y2 = yc;
                                canvas.Children.Add(line3);
                            }
                        }

                        for (int ii = 0; ii < 2; ii++)
                        {
                            double xx1 = 0;
                            double yy1 = 0;
                            double value1 = 0;
                            for (int jj = 0; jj < count; jj++)
                            {
                                string index = (count - 1 - jj).ToString();
                                string name = (ii == 0) ? "ft" : "st";
                                double value2 = double.Parse(_abstractSignals[interval + ":" + name + index]);
                                if (!double.IsNaN(value1) && !double.IsNaN(value2))
                                {
                                    double xx2 = xBase + jj * xScale;
                                    double yy2 = yBase - value2 * yScale2;
                                    if (jj > 0)
                                    {
                                        Line line1 = new Line();
                                        line1.Stroke = (value1 < value2) ? brush2 : brush3;
                                        line1.StrokeThickness = (ii == 0) ? 2 : 1;
                                        line1.X1 = xx1;
                                        line1.Y1 = yy1;
                                        line1.X2 = xx2;
                                        line1.Y2 = yy2;
                                        canvas.Children.Add(line1);
                                    }
                                    xx1 = xx2;
                                    yy1 = yy2;
                                }
                                value1 = value2;
                            }
                        }
                    }
                }
            }
        }

        private void drawTSB(int number)
        {
            string interval = getInterval(number);

            string signal;
            if (_abstractSignals.TryGetValue(interval + ":" + "TSBSig", out signal))
            {
                Canvas canvas = FindName("Chart" + number.ToString()) as Canvas;
                if (canvas != null)
                {
                    Brush[] brush = new Brush[] 
                    {
                        new SolidColorBrush(Color.FromRgb(0x00, 0xff, 0x00)),
                        new SolidColorBrush(Color.FromRgb(0xff, 0x00, 0x00)),
                        new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0x00)),
                        new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0x00)),
                        new SolidColorBrush(Color.FromRgb(0x00, 0x80, 0x00)),
                        new SolidColorBrush(Color.FromRgb(0xff, 0x66, 0x00)),
                        new SolidColorBrush(Color.FromRgb(0x00, 0xff, 0xff)),
                        new SolidColorBrush(Color.FromRgb(0xff, 0x00, 0xff))
                    };

                    Brush brush1 = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x66));

                    double width = canvas.ActualWidth;
                    double height = canvas.ActualHeight;

                    double lMargin = 17;
                    double tMargin = 2;
                    double rMargin = 16;
                    double bMargin = 2;

                    double xDiv = (width - (lMargin + rMargin)) / 7;

                    double barHeight = 5;

                    string lines1 = "5";

                    double yy1 = tMargin;
                    double yy2 = height - bMargin - barHeight;

                    for (int ii = 0; ii < 2; ii++)
                    {
                        for (int jj = 0; jj < 2; jj++)
                        {
                            double yy = ((ii == 0) ? yy1 : yy2) + ((jj == 0) ? -1 : barHeight + 1);
                            Line line1 = new Line();
                            line1.Stroke = brush1;
                            line1.StrokeThickness = 0.5;
                            line1.X1 = 0;
                            line1.X2 = width;
                            line1.Y1 = yy;
                            line1.Y2 = yy;
                            canvas.Children.Add(line1);
                        }
                    }

                    double[] xx1 = new double[] { 0, 0, 0, 0, 0, 0, 0, 0 };
                    for (int jj = 0; jj <= 8; jj++)
                    {
                        double xx = lMargin + (jj * xDiv) - (xDiv / 2);

                        string lines2 = (jj < 8) ? signal.Substring(jj, 1) : "5";
                        if (lines1 != lines2)
                        {
                            int index1 = int.Parse(lines1) - 1;
                            int index2 = int.Parse(lines2) - 1;
                            xx1[index2] = xx;

                            double xxLeft = xx1[index1];
                            if (xxLeft != 0)
                            {
                                double yyTop = ((index1 % 2) == 0) ? yy1 : yy2;

                                Rectangle rectangle = new Rectangle();
                                Canvas.SetLeft(rectangle, xxLeft);
                                Canvas.SetTop(rectangle, yyTop);
                                rectangle.Width = xx - xxLeft;
                                rectangle.Height = barHeight;
                                rectangle.Fill = brush[index1];
                                canvas.Children.Add(rectangle);
                            }
                        }
                        lines1 = lines2;
                    }
                }
            }
        }

        private void drawElliottSignals(int number)
        {
            string interval = getInterval(number);

            Brush brush1 = Brushes.Lime;
            Brush brush2 = Brushes.Red;

            string value1;
            if (_abstractSignals.TryGetValue(interval + ":" + "EWMajor", out value1))
            {
                Label label1 = FindName("EWMajor" + number.ToString()) as Label;
                if (label1 != null)
                {
                    int count1 = (value1.Length > 0 && value1 != "NaN") ? int.Parse(value1) : 0;
                    label1.Foreground = (count1 >= 0) ? brush1 : brush2;
                    count1 = Math.Abs(count1);
                    string text1 = "";
                    if (count1 == 7) text1 = "A";
                    else if (count1 == 8) text1 = "B";
                    else if (count1 == 9) text1 = "C";
                    else if (count1 > 0) text1 = (count1 - 1).ToString();
                    label1.Content = text1;
                }
            }

            string value2;
            if (_abstractSignals.TryGetValue(interval + ":" + "EWInter", out value2))
            {
                Label label2 = FindName("EWInter" + number.ToString()) as Label;
                if (label2 != null)
                {
                    int count2 = (value2.Length > 0 && value2 != "NaN") ? int.Parse(value2) : 0;
                    label2.Foreground = (count2 >= 0) ? brush1 : brush2;
                    count2 = Math.Abs(count2);
                    string text2 = "";
                    if (count2 == 7) text2 = "A";
                    else if (count2 == 8) text2 = "B";
                    else if (count2 == 9) text2 = "C";
                    else if (count2 > 0) text2 = (count2 - 1).ToString();
                    label2.Content = text2;
                }
            }

            string value3;
            if (_abstractSignals.TryGetValue(interval + ":" + "EWMinor", out value3))
            {
                Label label3 = FindName("EWMinor" + number.ToString()) as Label;
                if (label3 != null)
                {
                    int count3 = (value3.Length > 0 && value3 != "NaN") ? int.Parse(value3) : 0;
                    label3.Foreground = (count3 >= 0) ? brush1 : brush2;
                    count3 = Math.Abs(count3);
                    string text3 = "";
                    if (count3 == 2) text3 = "i";
                    else if (count3 == 3) text3 = "ii";
                    else if (count3 == 4) text3 = "iii";
                    else if (count3 == 5) text3 = "iv";
                    else if (count3 == 6) text3 = "v";
                    else if (count3 == 7) text3 = "a";
                    else if (count3 == 8) text3 = "b";
                    else if (count3 == 9) text3 = "c";
                    label3.Content = text3;
                }
            }

            string value4;
            if (_abstractSignals.TryGetValue(interval + ":" + "EWPTI", out value4))
            {
                Label label4 = FindName("EWPTI" + number.ToString()) as Label;
                if (label4 != null)
                {
                    int count4 = (value4.Length > 0) ? int.Parse(value4) : 0;
                    label4.Foreground = (count4 >= 0) ? brush1 : brush2;
                    string text4 = (count4 == 0) ? "" : count4.ToString();
                    label4.Content = text4;
                }
            }
        }

        private void Portfolio_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _setOverlayPortfolio = false;
            _overlayPortfolioName = "";

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
                SubgroupActionMenu1.Visibility = Visibility.Collapsed;
                SubgroupActionMenu2.Visibility = Visibility.Collapsed;

                _selectedNav1 = _nav1;
                _selectedNav2 = _nav2;
                _selectedNav3 = _nav3;
                _selectedNav4 = _nav4;
                _selectedNav5 = _nav5;

                List<string> items = new List<string>();

                if (BarServer.ConnectedToBloomberg() || !BarServer.ConnectedToCQG()) items.AddRange(new string[] { "BLOOMBERG >", " ","SPREADS >", " ", "COMMODITIES >", " ", "EQ | AMERICAS >", " ", "EQ | ASIA PACIFIC >", " ", "EQ | EUROPE & MEA >", " ", "ETF >", " ", "FX & CRYPTO >", " ", "GLOBAL FUTURES >", " ", "INTEREST RATES >", " ", "ML PORTFOLIOS >" });
                if (BarServer.ConnectedToCQG()) items.AddRange(new string[] { " ", "CQG COMMODITIES >", " ", "CQG EQUITIES >", " ", "CQG ETF >", " ", "CQG FX & CRYPTO >", " ", "CQG INTEREST RATES >", " ", "CQG STOCK INDICES >" });
                //items.AddRange(new string[] { "COMMODITIES >", " ", "EQ | AMERICAS >", " ", "EQ | ASIA PACIFIC >", " ", "EQ | EUROPE & MEA >"," ", "FX & CRYPTO >", " ", "INTEREST RATES >", " ", "CQG >"});
                //items.AddRange(new string[] { "COMMODITIES >", " ", "EQ | AMERICAS >", " ", "EQ | ASIA PACIFIC >", " ", "EQ | EUROPE & MEA >", " ", "CRYPTO >", " ", "ETF >", " ", "GLOBAL FUTURES >", " ", "INTEREST RATES >", " ", "US INDUSTRIES >", " ", "USER SYMBOL LIST 1 >", " ", "USER PORTFOLIOS >" });

                nav.setNavigation(NavCol1, RegionMenu_MouseDown, items.ToArray());
                nav.setNavigationLevel1(_nav1, NavCol2, NavCol2_MouseDown, go_Click);
                filterMLPortfoliosForRole(_nav1);
                nav.setNavigationLevel2(_nav2, NavCol3, NavCol3_MouseDown, go_Click);
                nav.setNavigationLevel3(_nav2, _nav3, NavCol4, NavCol4_MouseDown);
                nav.setNavigationLevel4(_selectedNav2, _selectedNav3, _nav4, NavCol5, NavCol5_MouseDown);

                // todo
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

                if (_selectedNav1 == "ALPHA PORTFOLIOS >")
                {
                    var fields = _clientPortfolioName.Split(',');
                    var cbs = FindVisualChildren<CheckBox>(NavCol2).ToList();
                    foreach (var cb in cbs)
                    {

                        cb.IsChecked = fields.Contains(cb.Tag);
                    }
                }

                highlightButton(NavCol1, _nav1);
                highlightButton(NavCol2, _nav2);
                highlightButton(NavCol3, _nav3);
                highlightButton(NavCol4, _nav4);
                highlightButton(NavCol5, _nav5);
                highlightButton(NavCol6, _nav6);

                string resourceKey = "";
                if (_legendOn)
                {
                    if (_condition == "Trend Condition") resourceKey = "FTLegend2";
                    else if (_condition == "Trend Heat") resourceKey = "HBLegend2";
                    else if (_condition == "Trend Strength") resourceKey = "TSBLegend2";
                    else if (_condition == "POSITIONS") resourceKey = "PositionLegend2";
                    else if (_condition == "Position Ratio") resourceKey = "PositionRatioLegend2";
                    else if (_condition == "Score") resourceKey = "ScoreLegend2";
                    else if (_condition == "PCM Orders") resourceKey = "PCMOrdersLegend2";
                }

                if (resourceKey.Length > 0)
                {
                    Viewbox legend = this.Resources[resourceKey] as Viewbox;
                    if (legend != null)
                    {
                        legend.SetValue(MarginProperty, new Thickness(0, 20, 0, 0));
                        NavCol1.Children.Add(legend);
                    }
                }

                startSymbolColoring();
            }
            else
            {
                showView();
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            hideNavigation();
            showView();
        }

        private bool _setOverlayPortfolio = false;

        private void Portfolio2_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _setOverlayPortfolio = true;

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
                SubgroupActionMenu1.Visibility = Visibility.Collapsed;
                SubgroupActionMenu2.Visibility = Visibility.Collapsed;

                _selectedNav1 = _nav1;
                _selectedNav2 = _nav2;
                _selectedNav3 = _nav3;
                _selectedNav4 = _nav4;
                _selectedNav5 = _nav5;

                if (MainView.EnablePortfolio)
                {
                    List<string> items = new List<string>();
                    nav.setNavigation(NavCol1, RegionMenu_MouseDown, items.ToArray());
                }

                if (MainView.EnablePortfolio)
                {
                    List<string> items = new List<string>();
                    if (MainView.EnableSQPTPortfolio) items.Add("HF2 >");
                    nav.setNavigation(NavCol1, RegionMenu_MouseDown, items.ToArray());
                }

                nav.setNavigationLevel1(_nav1, NavCol2, NavCol2_MouseDown, go_Click);
                filterMLPortfoliosForRole(_nav1);
                nav.setNavigationLevel2(_nav2, NavCol3, NavCol3_MouseDown, go_Click);
                nav.setNavigationLevel3(_nav2, _nav3, NavCol4, NavCol4_MouseDown);
                nav.setNavigationLevel4(_selectedNav2, _selectedNav3, _nav4, NavCol5, NavCol5_MouseDown);

                highlightButton(NavCol1, _nav1);
                highlightButton(NavCol2, _nav2);
                highlightButton(NavCol3, _nav3);
                highlightButton(NavCol4, _nav4);
                highlightButton(NavCol5, _nav5);
                highlightButton(NavCol6, _nav6);

                string resourceKey = "";
                if (_legendOn)
                {
                    if (_condition == "Trend Condition") resourceKey = "FTLegend2";
                    else if (_condition == "Trend Heat") resourceKey = "HBLegend2";
                    else if (_condition == "Trend Strength") resourceKey = "TSBLegend2";
                    else if (_condition == "POSITIONS") resourceKey = "PositionLegend2";
                    else if (_condition == "Position Ratio") resourceKey = "PositionRatioLegend2";
                    else if (_condition == "Score") resourceKey = "ScoreLegend2";
                    else if (_condition == "PCM Orders") resourceKey = "PCMOrdersLegend2";
                }

                if (resourceKey.Length > 0)
                {
                    Viewbox legend = this.Resources[resourceKey] as Viewbox;
                    if (legend != null)
                    {
                        legend.SetValue(MarginProperty, new Thickness(0, 20, 0, 0));
                        NavCol1.Children.Add(legend);
                    }
                }

                startSymbolColoring();
            }
            else
            {
                showView();
            }
        }

        private void showView()
        {
            if (_view == "CHART")
            {
                MainGrid.Visibility = Visibility.Visible;
                ChartGrid.Visibility = Visibility.Visible;
                SignalGrid.Visibility = Visibility.Collapsed;
                SpreadsheetGrid.Visibility = Visibility.Collapsed;
                PortfolioScroller.Visibility = Visibility.Visible;

                Links.Visibility = (MainView.EnableLinks) ? Visibility.Visible : Visibility.Collapsed;
                addCharts();
            }
            else if (_view == "SPREADSHEET")
            {
                MainGrid.Visibility = Visibility.Visible;
                SignalGrid.Visibility = Visibility.Collapsed;
                PortfolioScroller.Visibility = Visibility.Collapsed;
                if (_spreadsheetChart)
                {
                    ChartGrid.Visibility = Visibility.Visible;
                    SpreadsheetScroller.Height = 3 * _spreadsheetRowHeight + 2;
                    int row = getRowNumber(_symbol);
                    SpreadsheetScroller.ScrollToVerticalOffset(Math.Max(0, row - 1) * _spreadsheetRowHeight);
                    addCharts();
                }
                else
                {
                    ChartGrid.Visibility = Visibility.Collapsed;
                    SpreadsheetScroller.Height = 21 * _spreadsheetRowHeight + 2;
                    removeCharts();
                }
            }
            else // ABSTRACT
            {
                MainGrid.Visibility = Visibility.Visible;
                ChartGrid.Visibility = Visibility.Collapsed;
                SpreadsheetGrid.Visibility = Visibility.Collapsed;
                PortfolioScroller.Visibility = Visibility.Visible;
                removeCharts();
            }
            hideNavigation();
        }

        private void hideView()
        {
            MainGrid.Visibility = Visibility.Collapsed;
            ChartGrid.Visibility = Visibility.Collapsed;
            SignalGrid.Visibility = Visibility.Collapsed;
            SpreadsheetGrid.Visibility = Visibility.Collapsed;
            SpreadsheetGrid.Visibility = Visibility.Collapsed;
        }

        private void MarketMaps_MouseEnter(object sender, MouseEventArgs e)
        {
            Label label = sender as Label;
            label.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xcc, 0xff));
        }

        private void FundamentalML_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Close();
            _mainView.Content = new PortfolioBuilder(_mainView);
        }

        private void MarketMaps_MouseLeave(object sender, MouseEventArgs e)
        {
            Label label = sender as Label;
            label.Foreground = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));
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

        private void Alert_MouseEnter(object sender, MouseEventArgs e)
        {
            Label label = sender as Label;
            label.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xcc, 0xff));
        }

        private void Alert_MouseLeave(object sender, MouseEventArgs e)
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

        private void Server_MouseEnter(object sender, MouseEventArgs e)
        {
            Label label = sender as Label;
            label.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xcc, 0xff));
        }

        private void Server_MouseLeave(object sender, MouseEventArgs e)
        {
            Label label = sender as Label;
            label.Foreground = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));
        }
        private void Networks_MouseEnter(object sender, MouseEventArgs e)
        {
            Label label = sender as Label;
            label.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xcc, 0xff));
        }

        private void Networks_MouseLeave(object sender, MouseEventArgs e)
        {
            Label label = sender as Label;
            label.Foreground = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));
        }

        private void Diary_MouseEnter(object sender, MouseEventArgs e)
        {
            Label label = sender as Label;
            label.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xcc, 0xff));
        }

        private void Diary_MouseLeave(object sender, MouseEventArgs e)
        {
            Label label = sender as Label;
            label.Foreground = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));
        }

        private void Diary_MouseDown(object sender, MouseButtonEventArgs e)
        {
            //    _mainView.Content = new Diary(_mainView);
        }

        private void ContactUs_MouseEnter(object sender, MouseEventArgs e)
        {
            Label label = sender as Label;
            label.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xcc, 0xff));
        }

        private void View_MouseEnter(object sender, MouseEventArgs e)
        {
            Label label = sender as Label;
            label.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xcc, 0xff));
        }

        private void View_MouseLeave(object sender, MouseEventArgs e)
        {
            Label label = sender as Label;
            label.Foreground = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));
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

        private void CloseHelp_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            ViewHelpSection.BringIntoView();
            ChartHelp.Visibility = Visibility.Collapsed;
            ViewHelp.Visibility = Visibility.Collapsed;
            StudyHelp.Visibility = Visibility.Collapsed;
            HelpON.Visibility = Visibility.Visible;
            HelpOFF.Visibility = Visibility.Collapsed;
        }

        private void StudyHelp_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            StudyHelpSection.BringIntoView();
            ViewHelp.Visibility = Visibility.Collapsed;
            ChartHelp.Visibility = Visibility.Collapsed;
            StudyHelp.Visibility = Visibility.Visible;
            HelpON.Visibility = Visibility.Collapsed;
            HelpOFF.Visibility = Visibility.Visible;
        }

        private void Help_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            ViewHelpSection.BringIntoView();
            ViewHelp.Visibility = Visibility.Visible;
            ChartHelp.Visibility = Visibility.Collapsed;
            StudyHelp.Visibility = Visibility.Collapsed;
            HelpON.Visibility = Visibility.Collapsed;
            HelpOFF.Visibility = Visibility.Visible;
        }

        private void MarketMaps_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Close();
            _mainView.Content = new MarketMonitor(_mainView);
        }

        private void ML_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Close();
            _mainView.Content = new AutoMLView(_mainView);
        }

        private void Alert_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Close();
            _mainView.Content = new Alerts(_mainView);
        }

        private void Server_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Close();
            _mainView.Content = new Timing(_mainView);
        }

		private void Legend_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (LegendMenu.Visibility == Visibility.Collapsed)
            {
                hideView();
                hideNavigation();

                PortfolioSelectionPanel.Visibility = Visibility.Visible;
                LegendMenu.Visibility = Visibility.Visible;

                nav.setNavigation(LegendMenu, LegendMenu_MouseDown, new string[] { "Legend On", "Legend Off" });

                highlightButton(LegendMenu, _legendOn ? "Legend On" : "Legend Off", true);
            }
            else
            {
                showView();
                hideNavigation();
            }
        }

        private void LegendMenu_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Label label = e.Source as Label;
            string selection = label.Content as string;
            if (selection == "Legend On")
            {
                _legendOn = true;
            }
            else if (selection == "Legend Off")
            {
                _legendOn = false;
            }

            Legend.Content = (_legendOn && _condition != "OFF") ? "Legend On" : "Legend Off";

            showView();
            hideNavigation();

            adjustChartPositions();
        }

        private void ConditionMenu_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Label label = e.Source as Label;
            string selection = label.Content as string;

            if (selection != _condition)
            {
                _condition = selection;

                adjustChartPositions();

                Condition.Content = _condition;

                Legend.Content = (_legendOn && _condition != "OFF") ? "Legend On" : "Legend Off";

                startSymbolColoring();
            }

            showView();
            hideNavigation();
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

        private void RegionMenu_MouseDown(object sender, MouseButtonEventArgs e)
        {
            NavCol2.Children.Clear();
            NavCol3.Children.Clear();
            GroupMenu2.Children.Clear();
            NavCol4.Children.Clear();
            SubgroupMenu2.Children.Clear();
            NavCol5.Children.Clear();
            NavCol6.Children.Clear();
            IndustryMenu2.Children.Clear();
            SubgroupActionMenu1.Visibility = Visibility.Collapsed;
            SubgroupActionMenu2.Visibility = Visibility.Collapsed;

            Label label = e.Source as Label;
            string nav1 = label.Content as string;

            _selectedNav1 = nav1;

            highlightButton(NavCol1, _selectedNav1);

            nav.setNavigationLevel1(nav1, NavCol2, NavCol2_MouseDown, go_Click);
            filterMLPortfoliosForRole(nav1);

            if (_selectedNav1 == "ALPHA PORTFOLIOS >")
            {
                var fields = _clientPortfolioName.Split(',');
                var cbs = FindVisualChildren<CheckBox>(NavCol2).ToList();
                foreach (var cb in cbs)
                {

                    cb.IsChecked = fields.Contains(cb.Tag);
                }
            }

            startSymbolColoring();
        }

        /// <summary>
        /// RBAC: removes TEST portfolios from the ML PORTFOLIOS nav list for non-admin users.
        /// Only entries that positively resolve to a non-LIVE model are removed.
        /// LIVE entries and unresolvable entries stay visible.
        /// </summary>
        private void filterMLPortfoliosForRole(string nav1)
        {
            System.Diagnostics.Debug.WriteLine("[RBAC_BUILD_CHECK] Charts.filterMLPortfoliosForRole invoked");
            if (nav1 != "ML PORTFOLIOS >") return;
            if (ATMML.Auth.AuthContext.Current.IsAdmin) return;

            for (int i = NavCol2.Children.Count - 1; i >= 0; i--)
            {
                var child = NavCol2.Children[i] as FrameworkElement;
                if (child == null) continue;
                string name = (child as SymbolLabel)?.Symbol
                    ?? (child as System.Windows.Controls.Label)?.Content?.ToString()
                    ?? (child as System.Windows.Controls.TextBlock)?.Text
                    ?? (child as System.Windows.Controls.ContentControl)?.Content?.ToString()
                    ?? "";
                if (string.IsNullOrWhiteSpace(name)) continue;
                // Use _meta-backed lookup — MainView.GetModel returns null for ML portfolios.
                if (!ModelAccessGate.IsLive(name))
                    NavCol2.Children.RemoveAt(i);
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
                hideNavigation();
                showView();
                _clientPortfolioName = "";
                _update = true;
            }
            startSymbolColoring();
        }

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

                // todo
                update(_selectedNav1, _selectedNav2, _selectedNav3, "", "", "");
            }
            startSymbolColoring();
        }
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            Label label = sender as Label;
            hideNavigation();
            showView();
            _clientPortfolioName = nav.GetPortfolio();
        }

        private void addSaveAndCloseButtons(StackPanel input, MouseButtonEventHandler saveButtonEvent)
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
            label1.MouseDown += saveButtonEvent;
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
            label2.MouseDown += Cancel_Click;
            panel.Children.Add(label2);
            input.Children.Add(panel);
        }

        private StackPanel _memberPanel = null;
        private string _memberIndex = "";
        private string _portfolioRequested = "";

        private void requestMembers(string ticker, StackPanel panel)
        {
            _memberPanel = panel;
            requestPortfolio(ticker);
        }

        // name of built in portfolio
        // name of index to ask bbg for
        // single index name
        // prtu and eqs portfolios
        // list of tickers
        private void requestPortfolio(string name)
        {
            Portfolio.PortfolioType type = Portfolio.PortfolioType.Index;

            if (Portfolio.IsBuiltInPortfolio(name))
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

            if (_clientPortfolioName.Length > 0)
            {
                type = _clientPortfolioType;
            }

            _portfolio.Clear();
            _barCache.Clear();
            _type = type;

            _portfolioRequested = name;

            string[] fields = name.Split('&');

            if (type == Portfolio.PortfolioType.Single)
            {
                var symbols = new List<Symbol>();
                for (int ii = 0; ii < fields.Length; ii++)
                {
                    symbols.Add(new Symbol(fields[ii].Trim()));
                    // add these symbols to the member list
                }

            }
            else if (name != "Custom")
            {
                _portfolioRequestedCount = fields.Length;
                foreach (var field in fields)
                {
                    _portfolio.RequestSymbols(field.Trim(), type, false);
                }
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

                // todo
                update(_selectedNav1, _selectedNav2, _selectedNav3, _selectedNav4, "", "");
             }
            startSymbolColoring();
        }

        private void NavCol5_MouseDown(object sender, MouseButtonEventArgs e)
        {
            SymbolLabel label = e.Source as SymbolLabel;
            string nav5 = (label.Symbol == (string)label.Content) ? label.Symbol : label.Symbol + ":" + label.Content;

            _selectedNav5 = nav5;

            highlightButton(NavCol5, nav5);

            if (nav.setNavigationLevel4(_selectedNav3, _selectedNav4, _selectedNav5, NavCol6, NavCol6_MouseDown))
            {
                highlightButton(NavCol6, _nav6);
            }
            else if (nav.setNavigationLevel5(_selectedNav2, _selectedNav3, _selectedNav4, NavCol6, NavCol6_MouseDown))
            {
                highlightButton(NavCol6, _nav6);
                hideNavigation();

                showView();
            }
            else if (_compareToSelection)
            {
                requestYieldCompareMembers(nav5);
                hideNavigation();
                showView();
            }
            else
            {
                _clientPortfolioName = "";

                // todo
                update(_selectedNav1, _selectedNav2, _selectedNav3, _selectedNav4, _selectedNav5, "");
            }
        }

        private void NavCol6_MouseDown(object sender, MouseButtonEventArgs e)
        {
            SymbolLabel label = e.Source as SymbolLabel;
            string nav6 = (label.Symbol == (string)label.Content) ? label.Symbol : label.Symbol + ":" + label.Content;
            _selectedNav6 = nav6;

            _clientPortfolioName = "";
            update(_selectedNav1, _selectedNav2, _selectedNav3, _selectedNav4, _selectedNav5, _selectedNav6);
        }


        private void requestYieldCompareMembers(string input)
        {
            string[] field = input.Split(':');
            var index = field.Length - 1;
            _yieldCompare = field[index];

            //_portfolio.Clear();
            _portfolio.PortfolioChanged -= new PortfolioChangedEventHandler(portfolioChanged);
            _portfolio.Close();
            _portfolio = new Portfolio(20);
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

        private void Symbol_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Label label = e.Source as Label;
            if (label != null)
            {
                string key = label.Tag as string;
                _symbolKey = key;
                updateSymbol();
                _updateMemberList = 11;
            }
        }

        void startCalculatingTrades()
        {
            _calculatedSymbols.Clear();
            _calculating = true;
            _barCache.Clear();
            requestReferenceData(_memberList);
        }

        void go_Click(object sender, RoutedEventArgs e)
        {
            _portfolio.Clear();
            _barCache.Clear();

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

            _clientPortfolioName = portfolioName;
            _clientPortfolioType = Portfolio.PortfolioType.PRTU;
            if (buttonName.Contains("EQS")) _clientPortfolioType = Portfolio.PortfolioType.EQS;
            else if (buttonName.Contains("Peers")) _clientPortfolioType = Portfolio.PortfolioType.Peers;
            else if (buttonName.Contains("Model")) _clientPortfolioType = Portfolio.PortfolioType.Model;
            else if (buttonName.Contains("Alert")) _clientPortfolioType = Portfolio.PortfolioType.Alert;
            else if (buttonName.Contains("Your Lists")) _clientPortfolioType = Portfolio.PortfolioType.YourList;
            else if (buttonName.Contains("LIST")) _clientPortfolioType = Portfolio.PortfolioType.Worksheet;

            Country.Content = _clientPortfolioName;
            SpreadsheetGroup.Content = _clientPortfolioName;

            requestPortfolio(portfolioName, _clientPortfolioType);

            hideNavigation();

            showView();

            update(_selectedNav1, "", "", "", "", "");
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
            StrategyMenu.Visibility = Visibility.Collapsed;
            ModeMenu.Visibility = Visibility.Collapsed;
            NavCol1.Visibility = Visibility.Collapsed;
            NavCol2.Visibility = Visibility.Collapsed;
            NavCol3.Visibility = Visibility.Collapsed;
            GroupMenu2.Visibility = Visibility.Collapsed;
            NavScroller5.Visibility = Visibility.Collapsed;
            IndustryMenuScroller2.Visibility = Visibility.Collapsed;
            NavCol5.Visibility = Visibility.Collapsed;
            NavCol6.Visibility = Visibility.Collapsed;
            IndustryMenu2.Visibility = Visibility.Collapsed;

            ViewMenu.Children.Clear();
            LegendMenu.Children.Clear();
            ConditionMenu.Children.Clear();
            StrategyMenu.Children.Clear();
            ModeMenu.Children.Clear();
            NavCol1.Children.Clear();
            NavCol2.Children.Clear();
            NavCol3.Children.Clear();
            GroupMenu2.Children.Clear();
            NavCol4.Children.Clear();
            SubgroupMenu2.Children.Clear();
            NavCol5.Children.Clear();
            NavCol6.Children.Clear();
            IndustryMenu2.Children.Clear();
            SubgroupActionMenu1.Visibility = Visibility.Collapsed;
            SubgroupActionMenu2.Visibility = Visibility.Collapsed;
        }

        private void update()
        {
            update(_selectedNav1, _selectedNav2, nav.getGroup(_selectedNav2), "", "", "");
        }

        class SymbolInfo
        {
            private string _symbol;
            private string _description;
            private string _interval;

            public SymbolInfo(string symbol, string description, string interval)
            {
                _symbol = symbol;
                _description = description;
                _interval = interval;
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

            public string Interval
            {
                get { return _interval; }
                set { _interval = value; }
            }
        }

        private void drawLine(Brush brush, double thickness, double x1, double y1, double x2, double y2)
        {
            Line colLine = new Line();
            colLine.Stroke = brush;
            colLine.StrokeThickness = thickness;
            colLine.X1 = x1;
            colLine.Y1 = y1;
            colLine.X2 = x2;
            colLine.Y2 = y2;
            Spreadsheet.Children.Add(colLine);

        }

        private void drawRectangle(Brush brush, int row, int group, int col)
        {
            double width = Spreadsheet.ActualWidth;
            if (width > 0)
            {
                double symbolWidth = 100;
                double intervalWidth = (width - 2 - symbolWidth) / 3;
                double columnWidth = intervalWidth / 5;
                double rectWidth = columnWidth - (2 * _spreadsheetMargin.X);
                double rectHeight = _spreadsheetRowHeight - (2 * _spreadsheetMargin.Y);

                double x1 = Math.Round(symbolWidth + group * intervalWidth + col * columnWidth + _spreadsheetMargin.X);
                double y1 = row * _spreadsheetRowHeight + _spreadsheetMargin.Y;

                Rectangle rectangle = new Rectangle();
                Canvas.SetLeft(rectangle, x1);
                Canvas.SetTop(rectangle, y1);
                rectangle.Width = rectWidth;
                rectangle.Height = rectHeight;
                rectangle.Fill = brush;
                rectangle.Stroke = brush;
                Spreadsheet.Children.Add(rectangle);
            }
        }

        private void clearSpreadsheet()
        {
            int rowCount = _spreadsheetRows.Count;
            Brush brush = Brushes.Black;
            for (int row = 0; row < rowCount; row++)
            {
                for (int group = 0; group < 3; group++)
                {
                    for (int col = 0; col < 5; col++)
                    {

                        drawRectangle(brush, row, group, col);
                    }
                }
            }

            lock (_spreadsheetUpdate)
            {
                _spreadsheetColors.Clear();
                _spreadsheetUpdate.Clear();
            }
        }

        bool _update = false;

        private void timer_Tick(object sender, EventArgs e)
        {
            if (_update)
            {
                _update = false;
                update();
            }

            if (_updateAod)
            {
                _updateAod = false;
                updateAod();
            }

            if (_initialize)
            {
                initialize();
                _initialize = false;
            }

            if (_updateMemberList != 0)
            {

                //System.Diagnostics.Debug.WriteLine("Update Started " + (DateTime.Now - _time1).TotalSeconds);

                var tickers = new List<string>();
                var descriptions = new List<string>();
                var intervals = new List<string>();
                lock (_memberListLock)
                {
                    foreach (Symbol symbol in _memberList)
                    {
                        tickers.Add(symbol.Ticker);
                        descriptions.Add(symbol.Description);
                        intervals.Add(symbol.Interval);
                    }
                }

                if (_updateMemberList == 9)
                {
                    //Trace.WriteLine("Portfolio update " + (DateTime.Now - _portfolioTime).TotalSeconds);
                }

                _updateMemberList = 0;

                var portfolioName = getPortfolioName();
                _yieldPortfolio = _portfolio.IsYieldPortfolio(portfolioName) && _memberList.Select(x => x.Ticker).Contains(_symbol);
                initializeChartPositions();

                _symbols.Clear();

                for (int ii = 0; ii < tickers.Count; ii++)
                {
                    string ticker = tickers[ii];
                    string description = descriptions[ii];

                    string[] fields = ticker.Split(Symbol.SpreadCharacter);
                    string key = "";
                    foreach (string field in fields)
                    {
                        string[] subSymbol = field.Trim().Split(' ');
                        string sym = (subSymbol.Length <= 3) ? subSymbol[0] : subSymbol[0] + " " + subSymbol[1];
                        key += (key.Length > 0) ? " - " + sym : sym;
                    }

                    if (_symbols.Count == 0 || ticker == _symbol)
                    {
                        _symbolKey = key;
                    }

                    var interval = intervals[ii];

                    _symbols[key] = new SymbolInfo(ticker, description, interval);
                }

                updateMemberList();

                if (_chart1 != null)
                {
                    _chart1.PortfolioName = portfolioName;
                    _chart2.PortfolioName = portfolioName;
                    _chart3.PortfolioName = portfolioName;
                    //_chart4.PortfolioName = portfolioName;
                }
            }

            if (_drawAod)
            {
                _drawAod = false;
                AOD.Draw();
            }

            //progress bar
            int count1 = _predictionCount;
            int count2 = _predictionTotal;
            double percentage = (count2 == 0) ? 0 : 100.0 * count1 / count2;
            ProgressBar.Value = percentage;
            Progress.Visibility = (percentage > 0 && percentage < 100) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void setSymbolColor(string symbol, Color color)
        {
            List<StackPanel> stackPanels = new List<StackPanel>();
            stackPanels.Add(NavCol2);
            stackPanels.Add(NavCol3);
            stackPanels.Add(NavCol4);
            stackPanels.Add(NavCol5);
            stackPanels.Add(NavCol6);
            int count = stackPanels.Count;
            for (int ii = 0; ii < count; ii++)
            {
                foreach (FrameworkElement element in stackPanels[ii].Children)
                {
                    SymbolLabel label = element as SymbolLabel;
                    if (label != null)
                    {
                        string ticker = label.Symbol;
                        string[] field = ticker.Split(' ');
                        if (field.Length == 1) ticker += " Index";
                        if (ticker == symbol)
                        {
                            SolidColorBrush brush = label.Foreground as SolidColorBrush;
                            brush.Color = color;
                        }
                    }
                }
            }

            foreach (FrameworkElement element in PortfolioPanel.Children)
            {
                SymbolLabel label = element as SymbolLabel;
                if (label != null)
                {
                    if (label.Ticker == symbol)
                    {
                        SolidColorBrush brush = label.Foreground as SolidColorBrush;
                        brush.Color = color;
                    }
                }
            }
        }

        private int getTradeOpenSize(string ticker)
        {
            TradeHorizon horizon = Trade.Manager.GetIntervalTradeHorizon(_interval);
            var portfolioName = getPortfolioName();
            int size = (int)Math.Round(Trade.Manager.getOpenMarketValue(portfolioName, horizon, ticker));
            return size;
        }

        private string getOverlayPortfolioName()
        {
            string output = "";
            if (_overlayPortfolioName.Length > 0)
            {
                output = _overlayPortfolioName;
            }
            else
            {
                output = getPortfolioName();
            }
            return output;
        }

        List<Trade> getAutoTrades(string interval)
        {
            List<Trade> output = new List<Trade>();

            if (_trades.ContainsKey(interval))
            {
                if (_portDependant)
                {
                    lock (_trades)
                    {
                        foreach (var trade in _trades[interval])
                        {
                            int portfolioSize = getTradeOpenSize(trade.Ticker);
                            if (trade.Direction == Math.Sign(portfolioSize))
                            {
                                output.Add(trade);
                            }
                        }
                    }
                }
                else
                {
                    output = _trades[interval];
                }
            }

            return output;
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

        private bool _yieldPortfolio = false;

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

        private string getSymbolKey(Symbol symbol)
        {
            var name = getPortfolioName();
            var yieldDesription = isYieldDescription(symbol.Description);
            var useDescription = _yieldPortfolio || _portfolio.UseDescription(name) || yieldDesription;
            var yieldTicker = _portfolio.GetYieldTicker(name);
            return (yieldTicker.Length > 0) ? getYieldSymbol(symbol) : (useDescription ? symbol.Description : getSym(symbol.Ticker));
        }

        private void updateMemberList()
        {
            PortfolioPanel.Children.Clear();

            string interval1 = _interval;
            string interval2 = Study.getForecastInterval(interval1, 1);
            string interval3 = Study.getForecastInterval(interval2, 1);

            string[] intervals = { interval3, interval2, interval1 };

            Dictionary<string, List<Trade>> trades = new Dictionary<string, List<Trade>>();
            foreach (var interval in intervals)
            {
                trades[interval] = getAutoTrades(interval);
            }

            var portfolioName = getPortfolioName();

            var labels = new Dictionary<string, string>();
            var descriptions = new Dictionary<string, string>();

            var memberSize = new Dictionary<string, int>();
            foreach (Symbol member in _memberList)
            {
                var ticker = member.Ticker;
                var description = member.Description;

                descriptions[ticker] = description;

                labels[ticker] = member.Ticker;// description; // text;
                memberSize[ticker] = member.Size1;
            }

            var tickers = labels.OrderBy(x => x.Value).Select(x => x.Key).ToList();

            var count = 1;
            foreach (var ticker in tickers)
            {
                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(100) });  // Col 0 Symbol
                grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(10) });  // Col 1 Rect 1 ATM Strategy long term

                int size = memberSize[ticker];

                //int size = getTradeOpenSize(ticker);
                //if (_clientPortfolioType == Portfolio.PortfolioType.Alert)
                //{
                //    size = memberSize[ticker];
                //}
                //var openTrades = new List<Trade>(); // _portfolio.GetTrades(getPortfolioName(), ticker, DateTime.Now);
                //if (openTrades.Count > 0)
                //{
                //    size = openTrades[0].Direction;
                //}


                var key = getSym(ticker);

                Label label1 = new Label();
                label1.Content = labels[ticker];
                label1.Tag = key;
                label1.Name = "Symbol" + count++;
                Color color = (ticker == _symbol) ? Color.FromRgb(0x00, 0xcc, 0xff) : ((size == 0) ? Color.FromRgb(0xff, 0xff, 0xff) : ((size > 0) ? Colors.Lime : Colors.Red));
                label1.Foreground = new SolidColorBrush(color);
                label1.Background = Brushes.Transparent;
                label1.Padding = new Thickness(8, 5, 0, 0);
                label1.HorizontalAlignment = HorizontalAlignment.Left;
                label1.VerticalAlignment = VerticalAlignment.Top;
                label1.FontFamily = new FontFamily("Helvetica Neue");
                label1.FontWeight = FontWeights.Normal;
                label1.FontSize = 10;
                label1.Cursor = Cursors.Hand;
                //label1.ToolTip = descriptions[ticker];
                label1.MouseDown += Symbol_MouseDown;
                //label1.MouseEnter += Server_MouseEnter;
                //label1.MouseLeave += Server_MouseLeave;
                grid.Children.Add(label1);

                var prediction = 0;
                lock (_predictions)
                {
                    if (_predictions.ContainsKey(ticker) && _predictions[ticker].Count > _predictionIndex)
                    {
                        prediction = _predictions[ticker][_predictionIndex];
                    }
                }

                Rectangle rect1 = new Rectangle();
                Grid.SetColumn(rect1, 1);
                Brush brush0 = new SolidColorBrush(Color.FromRgb(0x00, 0x00, 0x00));
                Brush brush1 = new SolidColorBrush(Color.FromRgb(0xff, 0x00, 0x00));
                Brush brush2 = new SolidColorBrush(Color.FromRgb(0x00, 0xff, 0x00));
                rect1.Margin = new Thickness(4, 10, 0, 0);
                rect1.HorizontalAlignment = HorizontalAlignment.Center;
                rect1.VerticalAlignment = VerticalAlignment.Center;
                rect1.Fill = (prediction == -1) ? brush1 : ((prediction == 1) ? brush2 : brush0);
                rect1.Width = 8;
                rect1.Height = 8;
                grid.Children.Add(rect1);

                PortfolioPanel.Children.Add(grid);
            }

            if (!_calculateMemberList)
            {
                _calculateMemberList = true;
                _updateMemberList = 11;
            }
            //System.Diagnostics.Debug.WriteLine("Update Ended " + (DateTime.Now - _time1).TotalSeconds);
        }

        private Trade getOpenTrade(string symbol, List<Trade> trades)
        {
            Trade output = null;
            foreach (var trade in trades)
            {
                if (trade.IsOpen() && trade.Ticker == symbol)
                {
                    output = trade;
                    break;
                }
            }
            return output;
        }

        private void updateMemberListOld()
        {
            PortfolioPanel.Children.Clear();

            string name = getPortfolioName();
            bool addLines = (name == "US 500");



            string sector1 = "";
            TradeHorizon tradeHorizon = getTradeHorizon();
            foreach (KeyValuePair<string, SymbolInfo> kvp in _symbols)
            {
                string sym = kvp.Key;
                SymbolInfo symbolInfo = kvp.Value;

                if (addLines)
                {
                    string symbol = symbolInfo.Symbol;
                    string sector2 = _portfolio.GetSector(symbol);
                    if (sector1 != sector2)
                    {
                        sector1 = sector2;
                        Line line = new Line();
                        line.Stroke = Brushes.DodgerBlue;
                        line.StrokeThickness = 3.0;
                        line.X1 = 0;
                        line.Y1 = 0;
                        line.X2 = 100;
                        line.Y2 = 0;
                        PortfolioPanel.Children.Add(line);
                    }
                }

                Grid panel = getSymbolLabel(tradeHorizon, sym, symbolInfo);

                PortfolioPanel.Children.Add(panel);
            }
        }

        private int getResearchDirection(string name, string symbol, int ago)
        {
            return MainView.GetResearchDirection(name, symbol, ago);
        }

        private string getResearchComment(string name, string symbol, int ago)
        {
            return MainView.GetResearchComment(name, symbol, ago);
        }

        private bool isRecentDate(DateTime date2)
        {
            bool recent = false;
            DateTime now = DateTime.Now;
            DateTime date1 = new DateTime(now.Year, now.Month, now.Day);
            TimeSpan oneDay = new TimeSpan(1, 0, 0, 0);
            int weekDaysBack= 0;
            while (weekDaysBack < 1)  //was 2
            {
                date1 -= oneDay;
                bool weekend = (date1.DayOfWeek == DayOfWeek.Saturday || date1.DayOfWeek == DayOfWeek.Sunday);
                if (!weekend) weekDaysBack++;
            }
            recent = date2 >= date1;
            return recent;
        }

        private Grid getSymbolLabel(TradeHorizon tradeHorizon, string ticker, SymbolInfo info)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(50) });
            grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(25) });
            grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(25) });

            TradeHorizon horizon = Trade.Manager.GetIntervalTradeHorizon(_activeChartInterval); 

            //int size = getTradeOpenSize(ticker);
            int size = Trade.Manager.getTradeOpenDirection(getPortfolioName(), horizon, info.Symbol);
            if (size == 0)
            {
                var trade = _portfolio.GetTrades(getPortfolioName(), info.Symbol).First();
                if (trade != null)
                {
                    size = (int)trade.Direction;
                }
            }

            string[] subSymbol = ticker.Split(' ');
            string sym = (subSymbol.Length <= 3) ? subSymbol[0] : subSymbol[0] + " " + subSymbol[1];

            Label label1 = new Label();
            label1.Content = sym;
            label1.Name = ticker.Replace(" ", "_1_").Replace("/", "_2_").Replace("-", "_3_").Replace(".", "_4_").Replace("+", "_6_").Replace("*", "_5_").Replace("|", "_7_");
            Color color = (size == 0) ? Color.FromRgb(0xff, 0xff, 0xff) : ((size > 0) ? Colors.Lime : Colors.Red);
            label1.Foreground = new SolidColorBrush(color);
            label1.Background = Brushes.Transparent;
            label1.Padding = new Thickness(0, 10, 0, 0);
            label1.HorizontalAlignment = HorizontalAlignment.Left;
            label1.VerticalAlignment = VerticalAlignment.Top;
            label1.FontFamily = new FontFamily("Helvetica Neue");
            label1.FontWeight = FontWeights.Normal;
            label1.FontSize = 10;
            label1.Cursor = Cursors.Hand;
            //label1.ToolTip = info.Description;
            label1.MouseDown += Symbol_MouseDown;
            //label1.MouseDown += Symbol_MouseDown;
            label1.MouseEnter += Server_MouseEnter;
            label1.MouseLeave += Server_MouseLeave;
            grid.Children.Add(label1);

            int value = (int)calculatePXF(ticker) + 2; // 0 through 4
            Rectangle rect2 = new Rectangle();
            Grid.SetColumn(rect2, 2);
            Brush[] brushes =
            {
                new SolidColorBrush(Color.FromRgb(0xc2, 0xd2, 0xd2)),  
                new SolidColorBrush(Color.FromRgb(0xd2, 0x69, 0x1e)),  
                new SolidColorBrush(Color.FromRgb(0x00, 0x00, 0x00)),  
                new SolidColorBrush(Color.FromRgb(0xfa, 0xe0, 0x3c)),  
                new SolidColorBrush(Color.FromRgb(0x66, 0xcc, 0x66))    
            };
            rect2.Margin = new Thickness(0, 10, 10, 0);
            rect2.HorizontalAlignment = HorizontalAlignment.Center;
            rect2.VerticalAlignment = VerticalAlignment.Center;
            rect2.Fill = (0 <= value && value < brushes.Length) ? brushes[value] : Brushes.White;
            rect2.Width = 8;
            rect2.Height = 8;
            grid.Children.Add(rect2);

            return grid;
        }


        private double calculatePXF(string symbol)
        {
            double signal = 0;
            Dictionary<string, List<DateTime>> times = new Dictionary<string, List<DateTime>>();
            Dictionary<string, Series[]> bars = new Dictionary<string, Series[]>();

            string[] intervals = { "Daily", "Weekly" };

            bool ok = true;
            foreach (string interval1 in intervals)
            {
                times[interval1] = (_barCache.GetTimes(symbol, interval1, 0));
                bars[interval1] = _barCache.GetSeries(symbol, interval1, new string[] { "Open", "High", "Low", "Close" }, 0);
                if (times[interval1] == null || times[interval1].Count == 0)
                {
                    ok = false;
                }
            }

            if (ok)
            {
                Series PxF = atm.getPxf(times, bars, intervals);
                int count = PxF.Count;
                if (count > 0)
                {
                    signal = PxF[count - 1];
                }
            }
            return signal;
        }

        private double calculatePXFTSB(string symbol, string interval)
        {
            double signal = 0;
            Dictionary<string, List<DateTime>> times = new Dictionary<string, List<DateTime>>();
            Dictionary<string, Series[]> bars = new Dictionary<string, Series[]>();

            bool ok = true;
            times[interval] = (_barCache.GetTimes(symbol, interval, 0));
            bars[interval] = _barCache.GetSeries(symbol, interval, new string[] { "Open", "High", "Low", "Close" }, 0);
            if (times[interval] == null || times[interval].Count == 0)
            {
                ok = false;
            }

            if (ok)
            {
                Series PxF = atm.getPxf(times, bars, interval);

                Series hi = bars[interval][1];
                Series lo = bars[interval][2];
                Series cl = bars[interval][3];
                Series TSB = atm.calculateBullishTSB(hi, lo, cl) - atm.calculateBearishTSB(hi, lo, cl);

                int count = PxF.Count;
                if (count > 0)
                {
                    double pxf0 = PxF[PxF.Count - 1];
                    double pxf1 = PxF[PxF.Count - 2];
                    double tsb0 = TSB[TSB.Count - 1];

                    bool newLongBullish = (pxf0 == 1 && pxf1 == -1 && tsb0 == 1);   
                    bool newLongBearish = (pxf0 == 1 && pxf1 == -1 && tsb0 == -1);  

                    bool newShortBearish = (pxf0 == -1 && pxf1 == 1 && tsb0 == -1); 
                    bool newShortBullish = (pxf0 == -1 && pxf1 == 1 && tsb0 == 1);  

                    bool isLongBullish = (pxf0 == 1 && tsb0 == 1);                  
                    bool isLongBearish = (pxf0 == 1 && tsb0 == -1);                 

                    bool isShortBearish = (pxf0 == -1 && tsb0 == -1);               
                    bool isShortBullish = (pxf0 == -1 && tsb0 == 1);                
                                                                                                                                                
                    if (newLongBullish) signal = 4;          
                    else if (newLongBearish) signal = 3;     

                    else if (newShortBearish) signal = -4;   
                    else if (newShortBullish) signal = -3;   

                    else if (isLongBullish) signal = 2;      
                    else if (isLongBearish) signal = 1;      
                                                             
                    else if (isShortBearish) signal = -2;    
                    else if (isShortBullish) signal = -1;      
                }                                            
            }                                                
            return signal;
        }


        private Grid getSymbolLabelOld(TradeHorizon tradeHorizon, string symbol, SymbolInfo info)
        {
            Grid panel = new Grid();

            ColumnDefinition col0 = new ColumnDefinition();
            ColumnDefinition col1 = new ColumnDefinition();
            ColumnDefinition col2 = new ColumnDefinition();
            ColumnDefinition col3 = new ColumnDefinition();
            ColumnDefinition col4 = new ColumnDefinition();
            col0.Width = new GridLength(52, GridUnitType.Star);
            col1.Width = new GridLength(8, GridUnitType.Star);
            col2.Width = new GridLength(15, GridUnitType.Star);
            col3.Width = new GridLength(10, GridUnitType.Star);
            col4.Width = new GridLength(15, GridUnitType.Star);
            panel.ColumnDefinitions.Add(col0);
            panel.ColumnDefinitions.Add(col1);
            panel.ColumnDefinitions.Add(col2);
            panel.ColumnDefinitions.Add(col3);
            panel.ColumnDefinitions.Add(col4);

            TradeHorizon horizon = Trade.Manager.GetIntervalTradeHorizon(_activeChartInterval);

            int size =  Trade.Manager.getTradeOpenDirection(getPortfolioName(), horizon, info.Symbol);
            DateTime entryDate = Trade.Manager.getTradeOpenDate(TradeHorizon.Any, info.Symbol, getPortfolioName());
            bool recent = isRecentDate(entryDate);

            Order order = MainView.GetOrder(getPortfolioName(), info.Symbol);

            string side = (size > 0) ? " Long " + size.ToString() : ((size < 0) ? " Short " + (-size).ToString() : "");
            
            int outsideResearchDirection1 = getResearchDirection("Outside", info.Symbol, 0);
            int outsideResearchDirection2 = getResearchDirection("Outside", info.Symbol, 1);

            Color color1 = Color.FromRgb(0xff, 0xff, 0xff);
            if (order != null)
            {
                string type = order.Type;
                if (type == "b") color1 = Colors.Lime;
                else if (type == "s") color1 = Colors.Yellow;
                else if (type == "ss") color1 = Colors.Red;
                else if (type == "bs") color1 = Colors.Orange;

            }
            else if (_condition == "POSITIONS")
            {
                color1 = (size == 0) ? Color.FromRgb(0xff, 0xff, 0xff) : ((size > 0) ? Colors.Lime : Colors.Red);
            }
            else if (_symbolColors.ContainsKey(info.Symbol))
            {
                color1 = _symbolColors[info.Symbol];
            }

            Brush background = Brushes.Transparent;

            bool index = (info.Symbol.Contains(" Index"));
            int margin = index ? 10 : 10;  //Symbol spacing in Member List was 20

            TextBlock label1 = new TextBlock();
            label1.SetValue(Grid.ColumnProperty, 0);
            label1.Text = symbol;
            label1.Foreground = index ? Brushes.White : new SolidColorBrush(color1);  //color of index symbol
            label1.Background = background;
            label1.Padding = new Thickness(0, margin, 0, 2);
            label1.HorizontalAlignment = HorizontalAlignment.Left;
            label1.VerticalAlignment = VerticalAlignment.Top;
            label1.FontFamily = new FontFamily("Helvetica Neue");
            label1.FontWeight = FontWeights.Normal;
            label1.FontSize = 10;
            if (_symbolKey == symbol)
            {
                TextDecoration deco = new TextDecoration();
                deco.PenOffset = 4;
                TextDecorationCollection decos = new TextDecorationCollection();
                decos.Add(deco);
                label1.TextDecorations = decos;
            }
            label1.Cursor = Cursors.Hand;
            label1.MouseDown += Symbol_MouseDown;

            panel.Children.Add(label1);


            if (order != null)
            {
                TextBlock label4 = new TextBlock();
                label4.SetValue(Grid.ColumnProperty, 2);
                label4.SetValue(Grid.ColumnSpanProperty, 3);
                label4.Text = order.Size.ToString();
                label4.Foreground = new SolidColorBrush(color1);
                label4.Background = background;
                label4.Padding = new Thickness(0, margin, 0, 2);
                label4.HorizontalAlignment = HorizontalAlignment.Right;
                label4.VerticalAlignment = VerticalAlignment.Top;
                label4.FontFamily = new FontFamily("Helvetica Neue");
                label4.FontWeight = FontWeights.Normal;
                label4.FontSize = 10;
                //label4.ToolTip = info.Description + " " + order.PRTU;
 
                panel.Children.Add(label4);
            }

            if (recent) 
            {
                Label label2 = new Label();
                label2.SetValue(Grid.ColumnProperty, 1);
                label2.Content = "";  //label2.Content = "* "
                label2.Foreground = Brushes.White;
                label2.Background = background;
                label2.Padding = new Thickness(0, 5, 0, 2);
                label2.HorizontalAlignment = HorizontalAlignment.Left;
                label2.VerticalAlignment = VerticalAlignment.Top;
                label2.FontFamily = new FontFamily("Helvetica Neue");
                label2.FontWeight = FontWeights.Normal;
                label2.FontSize = 10;

                panel.Children.Add(label2);
            }

            {

                Color color3 = (outsideResearchDirection1 == 0) ? Color.FromRgb(0x00, 0x00, 0x00) : ((outsideResearchDirection1 > 0) ? Colors.Lime : Colors.Red);

                string comment = getResearchComment("Outside", info.Symbol, 0);

                Label label3 = new Label();
                label3.SetValue(Grid.ColumnProperty, 2);
                label3.Content = "";   //label3.Content = "  \u25cf";
                label3.Foreground = new SolidColorBrush(color3);
                label3.Background = background;
                label3.Padding = new Thickness(0, 5, 0, 2);
                label3.HorizontalAlignment = HorizontalAlignment.Left;
                label3.VerticalAlignment = VerticalAlignment.Top;
                label3.FontFamily = new FontFamily("Helvetica Neue");
                label3.FontWeight = FontWeights.Normal;
                label3.FontSize = 10;

                panel.Children.Add(label3);
            }

            {
                Label label3 = new Label();
                label3.SetValue(Grid.ColumnProperty, 3);
                label3.Content = "";  //label3.Content = "  |";
                label3.Foreground = (outsideResearchDirection1 != 0 || outsideResearchDirection2 != 0) ? Brushes.White : Brushes.Black;
                label3.Background = background;
                label3.Padding = new Thickness(0, 5, 0, 2);
                label3.HorizontalAlignment = HorizontalAlignment.Left;
                label3.VerticalAlignment = VerticalAlignment.Top;
                label3.FontFamily = new FontFamily("Helvetica Neue");
                label3.FontWeight = FontWeights.Normal;
                label3.FontSize = 10;

                panel.Children.Add(label3);
            }

            {

                Color color3 = (outsideResearchDirection2 == 0) ? Color.FromRgb(0x00, 0x00, 0x00) : ((outsideResearchDirection2 > 0) ? Colors.Lime : Colors.Red);

                string comment = getResearchComment("Outside", info.Symbol, 1);

                Label label3 = new Label();
                label3.SetValue(Grid.ColumnProperty, 4);
                label3.Content = "";  //label3.Content = "  \u25cf";
                label3.Foreground = new SolidColorBrush(color3);
                label3.Background = background;
                label3.Padding = new Thickness(0, 5, 0, 2);
                label3.HorizontalAlignment = HorizontalAlignment.Left;
                label3.VerticalAlignment = VerticalAlignment.Top;
                label3.FontFamily = new FontFamily("Helvetica Neue");
                label3.FontWeight = FontWeights.Normal;
                label3.FontSize = 10;

                panel.Children.Add(label3);
            }

            return panel;
        }

        private void barChanged(object sender, BarEventArgs e)
        {
            string symbol = e.Ticker;
            string interval = e.Interval;
        }

        private void calculateSpreadsheet(string symbol, string interval)
        {
            int group = getGroupNumber(interval);
            if (group != -1)
            {
                int row = getRowNumber(symbol);
                if (row >= 0)
                {
                    Series[] series = _barCache.GetSeries(symbol, interval, new string[] { "High", "Low", "Close" }, 0);
                    Series hi = series[0];
                    Series lo = series[1];
                    Series cl = series[2];

                    int barCount = cl.Count;
                    if (barCount > 0)
                    {
                        calculateFTSignals(group, row, hi, lo, cl);
                        calculateSTSignals(group, row, hi, lo, cl);
                        calculateTBSignals(group, row, hi, lo, cl);
                        calculateTLSignals(group, row, hi, lo, cl);
                        calculateTSBSignals(group, row, hi, lo, cl);
                    }
                }
            }
        }

        private void calculateFTSignals(int group, int row, Series hi, Series lo, Series cl)
        {
            Series ft = atm.calculateFT(hi, lo, cl);
            int count = ft.Count;

            int signal = 0;
            if (count >= 2)
            {
                if (ft[count - 1] >= ft[count - 2])
                {
                    signal = 1;
                }
                else
                {
                    signal = -1;
                }
            }

            int col = getColumnNumber("FT");
            string key = group.ToString() + ":" + col.ToString() + ":" + row.ToString();
            Color color = (signal == 1) ? Colors.Lime : Colors.Red;
            lock (_spreadsheetUpdate)
            {
                _spreadsheetColors[key] = color;
                _spreadsheetUpdate.Add(key);
            }
        }

        private void calculateSTSignals(int group, int row, Series hi, Series lo, Series cl)
        {
            Series st = atm.calculateST(hi, lo, cl);
            int count = st.Count;

            int signal = 0;
            if (count >= 2)
            {
                if (st[count - 1] >= st[count - 2])
                {
                    signal = 1;
                }
                else
                {
                    signal = -1;
                }
            }

            int col = getColumnNumber("ST");
            string key = group.ToString() + ":" + col.ToString() + ":" + row.ToString();
            Color color = (signal == 1) ? Colors.Lime : Colors.Red;
            lock (_spreadsheetUpdate)
            {
                _spreadsheetColors[key] = color;
                _spreadsheetUpdate.Add(key);
            }
        }

        private void calculateTBSignals(int group, int row, Series hi, Series lo, Series cl)
        {
            Series tb = atm.calculateTrendBars(hi, lo, cl);

            int count = tb.Count;

            int signal = (int)tb[count - 1];

            int col = getColumnNumber("TB");
            string key = group.ToString() + ":" + col.ToString() + ":" + row.ToString();
            Color color = (signal == 1) ? Colors.Lime : (signal == -1) ? Colors.Red : Colors.White;
            lock (_spreadsheetUpdate)
            {
                _spreadsheetColors[key] = color;
                _spreadsheetUpdate.Add(key);
            }
        }

        private void calculateTLSignals(int group, int row, Series hi, Series lo, Series cl)
        {
            List<Series> TL = atm.calculateTrendLines(hi, lo, cl, 3.0);
            Series tlUp = Series.NotEqual(TL[0].ReplaceNaN(0), 0);
            Series tlDn = Series.NotEqual(TL[1].ReplaceNaN(0), 0);
            Series tl = tlUp - tlDn;

            int count = tl.Count;

            int signal = (int)tl[count - 1];

            int col = getColumnNumber("TL");
            string key = group.ToString() + ":" + col.ToString() + ":" + row.ToString();
            Color color = (signal == 1) ? Colors.Lime : (signal == -1) ? Colors.Red : Colors.White;
            lock (_spreadsheetUpdate)
            {
                _spreadsheetColors[key] = color;
                _spreadsheetUpdate.Add(key);
            }
        }

        private void calculateTSBSignals(int group, int row, Series hi, Series lo, Series cl)
        {
            Series tsb = atm.calculateTSB(hi, lo, cl);
            Series st = atm.calculateST(hi, lo, cl);
            Series ezi = atm.calculateEZI(cl);

            Series upSet = (tsb > 30).And(st > 75).And((st.ShiftRight(6) < 20).Or(st.ShiftRight(7) < 20));
            Series dnSet = (tsb < 70).And(st < 25).And((st.ShiftRight(6) > 80).Or(st.ShiftRight(7) > 80));
            Series upRes = st < 65;
            Series dnRes = st > 35;

            Series Utsb = (ezi >= 80).And(tsb >= 70);
            Series Dtsb = (ezi <= 20).And(tsb <= 30);
            Series Uezi = (ezi <= 80).And(tsb >= 70);
            Series Dezi = (ezi >= 20).And(tsb <= 30);
            Series Uset = (tsb < 70) * atm.setReset(upSet, upRes);
            Series Dset = (tsb > 30) * atm.setReset(dnSet, dnRes);

            int count = Utsb.Count;

            Color color = Colors.DarkGoldenrod;
            if (Utsb[count - 1] == 1.0) color = Colors.Lime;
            else if (Dtsb[count - 1] == 1.0) color = Colors.Red;
            else if (Uezi[count - 1] == 1.0) color = Colors.DarkGreen;
            else if (Dezi[count - 1] == 1.0) color = Colors.Orange;
            else if (Uset[count - 1] == 1.0) color = Colors.Cyan;
            else if (Dset[count - 1] == 1.0) color = Colors.Magenta;

            int col = getColumnNumber("TSB");
            string key = group.ToString() + ":" + col.ToString() + ":" + row.ToString();
            lock (_spreadsheetUpdate)
            {
                _spreadsheetColors[key] = color;
                _spreadsheetUpdate.Add(key);
            }
        }

        private int getGroupNumber(string interval)
        {
            string interval1 = _intervals[1].Replace(" Min", "");
            string interval2 = _intervals[2].Replace(" Min", "");
            string interval3 = _intervals[3].Replace(" Min", "");

            int group = -1;
            if (interval == interval1) group = 0;
            else if (interval == interval2) group = 1;
            else if (interval == interval3) group = 2;
            return group;
        }

        private int getColumnNumber(string signal)
        {
            int col = 0;
            if (signal == "TB")
            {
                col = 1;
            }
            else if (signal == "TL")
            {
                col = 2;
            }
            else if (signal == "FT")
            {
                col = 3;
            }
            else if (signal == "ST")
            {
                col = 4;
            }
            return col;
        }

        private int getRowNumber(string symbol)
        {
            int row = -1;
            int row1;
            if (_spreadsheetRows.TryGetValue(symbol, out row1))
            {
                row = row1;
            }
            return row;
        }

        private string getRowSymbol(int row)
        {
            string symbol = "";
            string symbol1;
            if (_spreadsheetSymbols.TryGetValue(row, out symbol1))
            {
                symbol = symbol1;
            }
            return symbol;
        }

        private void calculateAbstract(string symbol, string interval)
        {
            Series[] series = _barCache.GetSeries(symbol, interval, new string[] { "Open", "High", "Low", "Close" }, 0);
            Series op = series[0];
            Series hi = series[1];
            Series lo = series[2];
            Series cl = series[3];

            if (interval == "Monthly") interval = "M";
            else if (interval == "Yearly") interval = "Y";
            else if (interval == "SemiAnnually") interval = "S";
            else if (interval == "Quarterly") interval = "Q";
            else if (interval == "Weekly") interval = "W";
            else if (interval == "Daily") interval = "D";

            Series ft = atm.calculateFT(hi, lo, cl);
            List<Series> fttp = atm.calculateFastTurningPoints(hi, lo, cl, ft);
            Series st = atm.calculateST(hi, lo, cl);
            List<Series> sttp = atm.calculateSlowTurningPoints(hi, lo, cl, st);

            int barCount = cl.Count;
            for (int ii = 0; ii < 10; ii++)
            {
                int index = barCount - 1 - ii;
                bool ok = index >= 0;
                _abstractSignals[interval + ":" + "op" + ii.ToString()] = (ok) ? op[index].ToString() : "NaN";
                _abstractSignals[interval + ":" + "hi" + ii.ToString()] = (ok) ? hi[index].ToString() : "NaN";
                _abstractSignals[interval + ":" + "lo" + ii.ToString()] = (ok) ? lo[index].ToString() : "NaN";
                _abstractSignals[interval + ":" + "cl" + ii.ToString()] = (ok) ? cl[index].ToString() : "NaN";
                _abstractSignals[interval + ":" + "ft" + ii.ToString()] = (ok) ? ft[index].ToString() : "NaN";
                _abstractSignals[interval + ":" + "st" + ii.ToString()] = (ok) ? st[index].ToString() : "NaN";
                _abstractSignals[interval + ":" + "fp" + ii.ToString()] = (ok) ? ((double.IsNaN(fttp[0][index])) ? fttp[1][index].ToString() : fttp[0][index].ToString()) : "NaN";
                _abstractSignals[interval + ":" + "sp" + ii.ToString()] = (ok) ? ((double.IsNaN(sttp[0][index])) ? sttp[1][index].ToString() : sttp[0][index].ToString()) : "NaN";
            }

            addXSignals(symbol, interval,op, hi, lo, cl);
            addFTSignals(symbol, interval, ft, fttp);
            addSTSignals(symbol, interval, st, sttp);
            addTSBSignals(symbol, interval, hi, lo, cl);
            addTrendLineSignals(symbol, interval, hi, lo, cl);
            addTrendBarSignals(symbol, interval, hi, lo, cl);
            addElliottSignals(symbol, interval, hi, lo, cl);
        }

        private void addXSignals(string symbol, string interval, Series op, Series hi, Series lo, Series cl)
        {
            Dictionary<string, Series> xsignal = new Dictionary<string, Series>();
            xsignal["FA"] = atm.calculateFirstAlert(hi, lo, cl, atm.FirstAlertLevelSelection.AllLevels);
            xsignal["AOA"] = atm.calculateAddOnAlert(hi, lo, cl, atm.AddonAlertLevelSelection.AllLevels);
            xsignal["PB"] = atm.calculatePullbackAlert(hi, lo, cl);
            xsignal["Exh"] = atm.calculateExhaustion(hi, lo, cl, atm.ExhaustionLevelSelection.AllLevels);
            xsignal["XAlert"] = atm.calculateXAlert(hi, lo, cl);
            xsignal["DivSig"] = atm.calculateZAlert(hi, lo, cl);
            xsignal["ADXUp"] = atm.calculateADXUpAlert(hi, lo, cl);
            xsignal["ADXDn"] = atm.calculateADXDnAlert(hi, lo, cl);
            xsignal["FT Alert"] = atm.calculateFTAlert(hi, lo, cl);
            xsignal["ST Alert"] = atm.calculateSTAlert(hi, lo, cl);
            xsignal["FTST Alert"] = atm.calculateFTSTAlert(hi, lo, cl);
            //xsignal["Two Bar Trend"] = atm.calculateTwoBarPattern(op, hi, lo, cl);
            //xsignal["PT Alert"] = atm.calculatePTAlert(op, hi, lo, cl);

            foreach (string name in xsignal.Keys)
            {
                double value = 0;
                int index = cl.Count - 1;
                for (int ii = 7; ii >= 0; ii--)
                {
                    double indicator = (index - ii >= 0) ? xsignal[name][index - ii] : 0;

                    value *= 10;

                    if (indicator > 0) value += 1;
                    else if (indicator < 0) value += 2;
                    else value += 3;
                }
                _abstractSignals[interval + ":" + name] = value.ToString();
            }
        }

        private void addFTSignals(string symbol, string interval, Series ft, List<Series> fttp)
        {
            if (ft.Count > 0)
            {
                double value = 0;
                int index = ft.Count - 1;
                if (index >= 1)
                {
                    double ft0 = ft[index];
                    double ft1 = ft[index - 1];

                    double ob = 80;
                    double os = 20;

                    if (ft0 > ob) value += 1;
                    else if (ft0 < os) value += 2;
                    else value += 3;

                    value *= 10;

                    if (ft0 > ft1) value += 1;
                    else if (ft0 < ft1) value += 2;
                    else value += 3;
                }

                _abstractSignals[interval + ":" + "FTSig"] = value.ToString();

                double tpUp = fttp[0][index];
                double tpDn = fttp[1][index];
                string tp = (!double.IsNaN(tpUp)) ? tpUp.ToString() + "u" : tpDn.ToString() + "d";

                _abstractSignals[interval + ":" + "FTTPVal"] = tp;
            }
        }

        private void addSTSignals(string symbol, string interval, Series st, List<Series> sttp)
        {
            if (st.Count > 0)
            {
                double value = 0;
                int index = st.Count - 1;
                if (index >= 1)
                {
                    double st0 = st[index];
                    double st1 = st[index - 1];

                    double ob = 80;
                    double os = 20;

                    if (st0 > ob) value += 1;
                    else if (st0 < os) value += 2;
                    else value += 3;

                    value *= 10;

                    if (st0 > st1) value += 1;
                    else if (st0 < st1) value += 2;
                    else value += 3;
                }

                _abstractSignals[interval + ":" + "STSig"] = value.ToString();

                double spUp = sttp[0][st.Count - 1];
                double spDn = sttp[1][st.Count - 1];
                string tp = (!double.IsNaN(spUp)) ? spUp.ToString() + "u" : spDn.ToString() + "d";

                _abstractSignals[interval + ":" + "STTPVal"] = tp;
            }
        }

        private void addTSBSignals(string symbol, string interval, Series hi, Series lo, Series cl)
        {
            Series tsb = atm.calculateTSB(hi, lo, cl);
            Series st = atm.calculateST(hi, lo, cl);
            Series ezi = atm.calculateEZI(cl);

            Series upSet = (tsb > 30).And(st > 75).And((st.ShiftRight(6) < 20).Or(st.ShiftRight(7) < 20));
            Series dnSet = (tsb < 70).And(st < 25).And((st.ShiftRight(6) > 80).Or(st.ShiftRight(7) > 80));
            Series upRes = st < 65;
            Series dnRes = st > 35;

            Series Utsb = (ezi >= 80).And(tsb >= 70);
            Series Dtsb = (ezi <= 20).And(tsb <= 30);
            Series Uezi = (ezi <= 80).And(tsb >= 70);
            Series Dezi = (ezi >= 20).And(tsb <= 30);
            Series Uset = (tsb < 70) * atm.setReset(upSet, upRes);
            Series Dset = (tsb > 30) * atm.setReset(dnSet, dnRes);

            double value = 0;
            int index = cl.Count - 1;
            for (int ii = 7; ii >= 0; ii--)
            {
                double val1 = (index - ii >= 0) ? tsb[index - ii] : 50;
                double val2 = (index - ii - 1 >= 0) ? tsb[index - ii - 1] : 50;

                value *= 10;

                if (Utsb[index - ii] == 1) value += 1;
                else if (Dtsb[index - ii] == 1) value += 2;
                else if (Uezi[index - ii] == 1) value += 5;
                else if (Dezi[index - ii] == 1) value += 6;
                else if (Uset[index - ii] == 1) value += 7;
                else if (Dset[index - ii] == 1) value += 8;
                else value += ((val1 >= val2) ? 3 : 4);
            }

            _abstractSignals[interval + ":" + "TSBSig"] = value.ToString();
        }

        private void addTrendLineSignals(string symbol, string interval, Series hi, Series lo, Series cl)
        {
            List<Series> TL = atm.calculateTrendLines(hi, lo, cl, 3.0);
            Series tlUp = Series.NotEqual(TL[0].ReplaceNaN(0), 0);
            Series tlDn = Series.NotEqual(TL[1].ReplaceNaN(0), 0);
            Series tl = tlUp - tlDn;

            double value = 0;
            int index = cl.Count - 1;
            for (int ii = 7; ii >= 0; ii--)
            {
                double indicator = (index - ii >= 0) ? tl[index - ii] : 0;

                value *= 10;

                if (indicator == 1) value += 1;
                else if (indicator == -1) value += 2;
                else value += 3;
            }

            _abstractSignals[interval + ":" + "TLSig"] = value.ToString();
        }

        private void addTrendBarSignals(string symbol, string interval, Series hi, Series lo, Series cl)
        {
            Series tb = atm.calculateTrendBars(hi, lo, cl);

            double value = 0;
            int index = cl.Count - 1;
            for (int ii = 7; ii >= 0; ii--)
            {
                double indicator = (index - ii >= 0) ? tb[index - ii] : 0;

                value *= 10;

                if (indicator == 1) value += 1;
                else if (indicator == -1) value += 2;
                else value += 3;
            }

            _abstractSignals[interval + ":" + "TBSig"] = value.ToString();
        }

        private void addElliottSignals(string symbol, string interval, Series hi, Series lo, Series cl)
        {
            int barCount = 1000;

            Series mid = Series.Mid(hi, lo);

            ElliottWaveInput input = new ElliottWaveInput();
            ElliottWaveOutput output = new ElliottWaveOutput();

            ElliottAlgorithm algorithm = new ElliottAlgorithm();

            input.lowX = lo;
            input.highX = hi;
            input.low = lo;
            input.high = hi;
            input.close = cl;
            int period1 = ElliottAlgorithm.EW_PERIOD1;
            int period2 = ElliottAlgorithm.EW_PERIOD2;

            input.oscX = mid.osc(OscType.Difference, MAType.Simple, MAType.Simple, period1, period2);
            input.osc1 = mid.osc(OscType.Difference, MAType.Simple, MAType.Simple, period1, period2);
            input.osc2 = mid.osc(OscType.Difference, MAType.Simple, MAType.Simple, period1, period2 / 2);
            input.overlap = ElliottAlgorithm.STOCKS_4_1_OVERLAP;
            input.completedWavesOnly = true;
            input.barCount = (short)barCount;

            int barCount2 = input.close.Count;

            int end = Math.Max(0, barCount2 - ((short)barCount));
            for (int ii = 0; ii < end; ii++)
            {
                input.lowX[ii] = double.NaN;
                input.highX[ii] = double.NaN;
                input.low[ii] = double.NaN;
                input.high[ii] = double.NaN;
                input.close[ii] = double.NaN;
            }

            end = Math.Max(0, barCount2 - ((short)barCount - period2));
            for (int ii = 0; ii < end; ii++)
            {
                input.oscX[ii] = 0;
                input.osc1[ii] = 0;
                input.osc2[ii] = 0;
            }

            algorithm.Evaluate(input, output);

            int index = cl.Count - 2;
            _abstractSignals[interval + ":" + "EWMajor"] = output.wave[0][index].ToString();
            _abstractSignals[interval + ":" + "EWIntr"] = output.wave[1][index].ToString();
            _abstractSignals[interval + ":" + "EWMinor"] = output.wave[2][index].ToString();
            _abstractSignals[interval + ":" + "EWPTI"] = output.pti.ToString();
        }

        public void Close()
        {
            _ourViewTimer.Tick -= new System.EventHandler(timer_Tick);
            _ourViewTimer.Stop();

            _portfolio.PortfolioChanged -= new PortfolioChangedEventHandler(portfolioChanged);

            List<Alert> alerts = Alert.Manager.Alerts;
            foreach (Alert alert in alerts)
            {
                alert.FlashEvent -= new FlashEventHandler(alert_FlashEvent);
            }

            if (_portfolio != null)
            {
                _portfolio.Close();
            }

            if (_barCache != null)
            {
                _barCache.Clear();
            }

            removeCharts();

            setInfo();

            if (_predictionThreadRunning)
            {
                _predictionThreadRunning = false;
            }

            AOD.OrderEvent -= onOrderEvent;
        }

        private void changeInterval(int number, string interval)
        {
            _intervals[number] = interval;
        }

        private void BackgroundInterval_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            changeInterval(1, e.AddedItems[0].ToString());
        }

        private void AlignmentInterval_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            changeInterval(2, e.AddedItems[0].ToString());
        }

        private void TradeInterval_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            changeInterval(3, e.AddedItems[0].ToString());
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
            }
        }

        private void OurViewEdit_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F1)
            {
                OurViewEdit.Text += " People";
            }
            else if (e.Key == Key.F2)
            {
                OurViewEdit.Text += " Govt";
            }
            else if (e.Key == Key.F3)
            {
                OurViewEdit.Text += " Corp";
            }
            else if (e.Key == Key.F4)
            {
                OurViewEdit.Text += " Mtge";
            }
            else if (e.Key == Key.F5)
            {
                OurViewEdit.Text += " M-Mkt";
            }
            else if (e.Key == Key.F6)
            {
                OurViewEdit.Text += " Muni";
            }
            else if (e.Key == Key.F7)
            {
                OurViewEdit.Text += " Pfd";
            }
            else if (e.Key == Key.F8)
            {
                OurViewEdit.Text += " Equity";
            }
            else if (e.Key == Key.F9)
            {
                OurViewEdit.Text += " Comdty";
            }
            else if (e.Key == Key.F10 || e.Key == Key.System)
            {
                OurViewEdit.Text += " Index";
            }
            else if (e.Key == Key.F11)
            {
                OurViewEdit.Text += " Curncy";
            }
            else if (e.Key == Key.F12)
            {
                OurViewEdit.Text += " Client";
            }
            else if (e.Key == Key.Enter || e.Key == Key.Return)
            {
                _symbol = OurViewEdit.Text;

                string[] subSymbol = _symbol.Split(' ');
                _symbolKey = (subSymbol.Length <= 3) ? subSymbol[0] : subSymbol[0] + " " + subSymbol[1];
                setInfo();
            }
        }

        private void Spreadsheet_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Canvas canvas = sender as Canvas;
            Point position = e.GetPosition(canvas);
            int row = (int)((position.Y - _spreadsheetMargin.Y) / _spreadsheetRowHeight);
            _spreadsheetChart = true;
            _symbol = getRowSymbol(row);
            updateChart(_symbol, _interval, false);
            showView();
        }

        private string getDataInterval(string input)
        {
            var output = input;
            if (input == "D") output = "Daily";
            else if (input == "W") output = "Weekly";
            else if (input == "M") output = "Monthly";
            else if (input == "Q") output = "Quarterly";
            else if (input == "S") output = "SemiAnnually";
            else if (input == "Y") output = "Yearly";
            return output;
        }

        private void ChartInterval_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Label label = e.Source as Label;
            string time = label.Content as string;

            _interval = getDataInterval(time);
            _aodInterval = _interval;
            _updateAod = true;

            highlightIntervalButton(ChartIntervals, _interval.Replace(" Min", ""));

            if (_view == "CHART")
            {
                updateChart(_symbol, _interval, false);
            }
            else if (_view == "ABSTRACT")
            {
                updateAbstract();
            }
            showView();
            hideNavigation();
            //initializeModelList();
        }

        private void ChartRangeInterval_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Label label = e.Source as Label;
            string time = label.Content as string;

            _interval = "R" + time;

            highlightIntervalButton(RangeBarIntervals, time);

            if (_view == "CHART")
            {
                updateChart(_symbol, _interval, false);
            }
            else if (_view == "ABSTRACT")
            {
                updateAbstract();
            }
            showView();
            hideNavigation();
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
                        string text = label.Content as string;

                        label.Foreground = (interval == text) ? brush2 : brush1;
                    }
                }
            }
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
            updateCalculator(_chart3);
        }

        private void Symbol_Click(object sender, MouseButtonEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            if (textBox.Text.Length > 0)
            {
                string text = textBox.Text;
            }
        }

        private void Strategy_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Label label = e.Source as Label;
            string strategy = label.Content as string;
            _strategy = strategy;
            //highlightButton(StrategyList, _strategy);
            startCalculatingTrades();
            _updateMemberList = 12;
        }

        Dictionary<string, Strategy> _strategies;
        string _selectedStrategy = "";

        private void Label_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Label label = sender as Label;
            _selectedStrategy = (string)label.Content;
        }

        private Label getStrategyLabel(string strategy)
        {
            Label label = new Label();
            label.Content = strategy;
            Color color = Color.FromRgb(0xff, 0xff, 0xff);
            label.Foreground = new SolidColorBrush(color);
            label.Background = Brushes.Transparent;
            label.Padding = new Thickness(0, 2, 0, 2);
            label.HorizontalAlignment = HorizontalAlignment.Left;
            label.VerticalAlignment = VerticalAlignment.Top;
            label.FontFamily = new FontFamily("Helvetica Neue");
            label.FontWeight = FontWeights.Normal;
            label.FontSize = 10;
            label.Cursor = Cursors.Hand;
            //label.ToolTip = "";
            label.MouseDown += Strategy_MouseDown;
            return label;
        }

        private void Condition_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (ConditionMenu.Visibility == Visibility.Collapsed)
            {
                hideView();
                hideNavigation();
                PortfolioSelectionPanel.Visibility = Visibility.Visible;
                ConditionMenu.Visibility = Visibility.Visible;

                List<string> items = new List<string>(){ "OFF", "Trend Condition", "Trend Strength", "Trend Heat" };
                if (MainView.EnablePortfolio) items.Add("POSITIONS");
                if (MainView.EnablePortfolio) items.Add("Score");
                if (MainView.EnablePR) items.Add("Position Ratio");

                nav.setNavigation(ConditionMenu, ConditionMenu_MouseDown, items.ToArray());  
                highlightButton(ConditionMenu, _condition, true);
            }
            else
            {
                showView();
                hideNavigation();
            }
        }

        private void Condition_MouseEnter(object sender, MouseEventArgs e)
        {
            Label label = sender as Label;
            label.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xcc, 0xff));
        }

        private void Condition_MouseLeave(object sender, MouseEventArgs e)
        {
            Label label = sender as Label;
            label.Foreground = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));
        }

        private void SecurityList_MouseDown(object sender, RoutedEventArgs e)
        {
            //SecurityListHelp.Visibility = Visibility.Visible;
            //PriceModelHelp.Visibility = Visibility.Collapsed;
            //ViewHelp.Visibility = Visibility.Collapsed;
        }

        private void PriceModel_MouseDown(object sender, RoutedEventArgs e)
        {

        }

        //private void GlobalCost_MouseDown(object sender, MouseButtonEventArgs e)
        //{
            //Close();
            //_mainView.Content = new MarketMonitor(_mainView);
            //hideNavigation();
            //GlobalCostNav.Visibility = Visibility.Visible;
            //ChartIntervals.Visibility = Visibility.Visible;
            //ProdCostImpactNav.Visibility = Visibility.Collapsed;
            //AOD3ControlGrid.Visibility = Visibility.Collapsed;
            //CostCal.Visibility = Visibility.Collapsed;
            //showMap();
            //_viewType = ViewType.Map;
            //startMapColoring(500);
        //}

        private void GlobalCost_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Close();
			//_mainView.Content = new MarketMonitor(_mainView, ViewType.Map);
			_mainView.Content = new MarketMonitor(_mainView);
		}

        private void ChartHelp_MouseDown(object sender, RoutedEventArgs e)
        {
            ChartHelpSection.BringIntoView();
            StudyHelp.Visibility = Visibility.Collapsed;
            ChartHelp.Visibility = Visibility.Visible;
            ViewHelp.Visibility = Visibility.Collapsed;
        }

        private void AODView_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Close();
            //_mainView.Content = new Competitor(_mainView);
        }

        private void Help_MouseEnter(object sender, MouseEventArgs e) // change to &#x3F
        {
            var label = sender as Label;
            label.Tag = label.Foreground;
            //label.Content = "\u2630"; // u24db
            label.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xcc, 0xff));
        }

        private void Help_MouseLeave(object sender, MouseEventArgs e)  //return to &#x1392
        {
            var label = sender as Label;
            //label.Content = "\u2630";
            label.Foreground = (Brush)label.Tag;
        }

        bool _portDependant = false;

        private void PORTResults_Checked(object sender, RoutedEventArgs e)
        {
            if (!_portDependant)
            {
                _portDependant = true;
                _updateMemberList = 13;
            }
        }

        private void ATMResults_Checked(object sender, RoutedEventArgs e)
        {
            if (_portDependant)
            {
                _portDependant = false;
                _updateMemberList = 14;
            }
        }

        bool _compareToSelection = false;

        private void CompareTo_MouseDown(object sender, MouseButtonEventArgs e)
        {

            if (NavCol1.Visibility == Visibility.Collapsed)
            {
                hideView();
                hideNavigation();

                PortfolioSelectionPanel.Visibility = Visibility.Visible;
                NavCol1.Visibility = Visibility.Visible;
                NavCol2.Visibility = Visibility.Visible;
                NavCol3.Visibility = Visibility.Visible;

                _compareToSelection = true;

                List<string> items = new List<string>();
                items.AddRange(new string[]
                {
                    "US MUNI >",
                    " ",
                    " ",
                    "NA CANADA >",
                    "NA US >",
                    "NA MEXICO >",
                    " ",
                    "SA ARGENTINA >",
                    "SA BRAZIL >",
                    "SA CHILE >",
                    "SA COLOMBIA >",
                    "SA PERU >",
                    " ","" +
                    "EU EUROZONE >",
                    "EU AUSTRIA >",
                    "EU BELGIUM >",
                    "EU CZECH REP >",
                    "EU DENMARK >",
                    "EU ENGLAND >",
                    "EU FINLAND >",
                    "EU FRANCE >",
                    "EU GERMANY >",
                    "EU GREECE >",
                    "EU HUNGARY >",
                    "EU IRELAND >",
                    "EU ITALY >",
                    "EU NETHERLANDS >",
                    "EU POLAND >",
                    "EU PORTUGAL >",
                    "EU ROMANIA >",
                    "EU SLOVAKIA >",
                    "EU SLOVENIA >",
                    "EU SPAIN >",
                    "EU SWEDEN >",
                    " ",
                    "MEA ISRAEL >",
                    "MEA SOUTH AFRICA >",
                    " ",
                    "AP AUSTRALIA >",
                    "AP CHINA >",
                    "AP HONG KONG >",
                    "AP INDIA >",
                    "AP INDONESIA >",
                    "AP JAPAN >",
                    "AP MALAYSIA >",
                    "AP NEW ZEALAND >",
                    "AP PAKISTAN >",
                    "AP PHILIPPINES >",
                    "AP SINGAPORE >",
                    "AP SOUTH KOREA >",
                    "AP TAIWAN >",
                    "AP THAILAND >",
                });

                nav.setNavigation(NavCol1, NavCol1_MouseDown, items.ToArray());
                nav.setNavigationLevel1(_yieldNav1, NavCol2, NavCol2_MouseDown, null);
                filterMLPortfoliosForRole(_yieldNav1);
                nav.setNavigationLevel2(_yieldNav2, NavCol3, NavCol3_MouseDown, go_Click);

                highlightButton(NavCol1, _yieldNav1);
                highlightButton(NavCol2, _yieldNav2);
                highlightButton(NavCol3, _yieldNav3);
            }
            else
            {
                showView();
            }
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
            filterMLPortfoliosForRole(_selectedNav1);
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

        private void yieldChartEvent(object sender, YieldChartEventArgs e)
        {
            var type = e.Type;
            if (type == YieldEventType.Ticker)
            {
                var ticker = e.Description;
                updateChart(ticker, _interval, false);
            }
            else if (type == YieldEventType.Condition)
            {
                _yieldCondition = e.Description;
            }
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

        List<Yield> getYields(string conditionName, List<Symbol> symbols, string interval, int ago)
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

                    var series = _symbolBarCache.GetSeries(ticker, interval, new string[] { "High", "Low", "Close" }, 0, 100);

                    var hi = series[0];
                    var lo = series[1];
                    var cl = series[2];

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

                    Series sig = null;

                    if (conditionName == "ATM FT")
                    {
                        Series ft = atm.calculateFT(hi, lo, cl);
                        sig = atm.goingUp(ft) - atm.goingDn(ft);
                    }
                    else if (conditionName == "ATM ST")
                    {
                        Series st = atm.calculateST(hi, lo, cl);
                        sig = atm.goingUp(st) - atm.goingDn(st);
                    }
                    else if (conditionName == "ATM Trend Bars")
                    {
                        sig = atm.calculateTrendBars(hi, lo, cl);  //  ?? not working
                    }

                    var value = (cl.Count > ago) ? cl[cl.Count - 1 - ago] : double.NaN;
                    var condition = (sig.Count > ago) ? sig[sig.Count - 1 - ago] : double.NaN;  // not working on Compare to

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

        private DateTime getYieldDate(string interval, int ago)
        {
            DateTime output = DateTime.Now;

            var ticker = (_memberList.Count > 0) ? _memberList[0].Ticker : "";
            if (ticker.Length > 0)
            {
                var times = _barCache.GetTimes(ticker, interval, 0, 250);

                if (times.Count > ago)
                {
                    var date = times[times.Count - 1 - ago];
                    output = date;
                }
            }
            return output;
        }

        private int getYieldAgo(string interval, DateTime time)
        {
            int output = 0;

            var ticker = (_memberList.Count > 0) ? _memberList[0].Ticker : "";
            if (ticker.Length > 0)
            {
                var times = _barCache.GetTimes(ticker, interval, 0, 250);
                var count = times.Count;
                for (int ago = 0; ago < times.Count; ago++)
                {
                    output = ago;
                    var index = count - 1 - ago;
                    if (times[index] <= time)
                    {
                        break;
                    }
                }              
            }
            return output;
        }

        private int _yieldAgo = 0;
        private string _yieldCompare = "";
        private string _yieldCondition = "ATM FT";
        private List<Symbol> _compareSymbols = new List<Symbol>();

        private void CursorLeft_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_yieldAgo < 100) _yieldAgo++;
        }

        private void CursorRight_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_yieldAgo > 0) _yieldAgo--;
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

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Label label = sender as Label;
            hideNavigation();
            showView();
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
            highlightIntervalButton(ChartIntervals, _interval.Replace(" Min", ""));
        }

        private void Arrow_MouseEnter(object sender, MouseEventArgs e)
        {
            Label label = sender as Label;
            label.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xcc, 0xff));
        }

        private void Arrow_MouseLeave(object sender, MouseEventArgs e)
        {
            Label label = sender as Label;
            label.Foreground = new SolidColorBrush(Color.FromRgb(0x12, 0x4b, 0x72));
        }

        private void StudyPanel_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
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

		private void UserFactorModelTestStartDate_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            var dp = sender as DatePicker;
            var dt = dp.SelectedDate.GetValueOrDefault(DateTime.Now);
        }

        private void StackPanel_MouseEnter(object sender, MouseEventArgs e)
        {
            Border sp = sender as Border;
            DoubleAnimation db = new DoubleAnimation();
            //db.From = 12;
            db.To = 150;
            db.Duration = TimeSpan.FromSeconds(0.5);
            db.AutoReverse = false;
            db.RepeatBehavior = new RepeatBehavior(1);
            sp.BeginAnimation(StackPanel.HeightProperty, db);
        }

        private void StackPanel_MouseLeave(object sender, MouseEventArgs e)
        {
            Border sp = sender as Border;
            DoubleAnimation db = new DoubleAnimation();
            //db.From = 12;
            db.To = 12;
            db.Duration = TimeSpan.FromSeconds(0.5);
            db.AutoReverse = false;
            db.RepeatBehavior = new RepeatBehavior(1);
            sp.BeginAnimation(StackPanel.HeightProperty, db);
        }

        private void Current_Checked(object sender, RoutedEventArgs e)
        {
            _predictionIndex = 0;
            _updateMemberList = 16;
        }

        private void Plus1_Checked(object sender, RoutedEventArgs e)
        {
            _predictionIndex = 1;
            _updateMemberList = 17;
        }
    }
}
