//#define POSITION

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Path = System.Windows.Shapes.Path;

namespace ATMML
{
    public enum ViewType
    {
        Cost,
        Map,
        Symbol
    }

    public partial class MarketMonitor : ContentControl
    {
        int _barCount = 1000;

        MainView _mainView = null;

        DispatcherTimer _mapTimer = null;

        ViewType _viewType = ViewType.Map;

        string _region = "";
        string _country = "";
        string _group = "";
        string _subgroup = "";
        string _industry = "";
        string _symbol = "";

        string _condition = "ATM ANALYSIS";
        string _interval = "Daily";
        string _asset = "WORLD EQ INDICES";

        int _ago = 0;

        const int _maxAgo = 100;
        List<DateTime> _agoTime = null;

        bool _initialize = false;

        List<Path> _countryPaths = new List<Path>();

        // progress
        int _countrySymbolCount = 0;
        int _labelSymbolCount = 0;
        List<string> _countrySymbols = new List<string>();
        List<string> _labelSymbols = new List<string>();

        BarCache _countryBarCache;
        Dictionary<string, List<int>> _countrySignals = new Dictionary<string, List<int>>();
        Dictionary<string, int> _countryValues = new Dictionary<string, int>();

        Dictionary<DateTime, Dictionary<string, double>> _countryFundamentalValues = new Dictionary<DateTime, Dictionary<string, double>>();
        Dictionary<DateTime, Dictionary<string, double>> _countryEconomicValues = new Dictionary<DateTime, Dictionary<string, double>>();

        Dictionary<string, double> _symbolFundamentalValues = new Dictionary<string, double>();
        Dictionary<string, double> _symbolEconomicValues = new Dictionary<string, double>();

        Chart _chart1 = null;
        Chart _chart2 = null;
        Chart _chart3 = null;

        Chart _AODchart1 = null;

        string _mode = "";

        string _chartSymbol = "";
        string _chartInterval = "";

        Navigation nav = new Navigation();
        Navigation srcNav = new Navigation();

        BarCache _symbolBarCache;
        Dictionary<string, List<int>> _symbolSignals = new Dictionary<string, List<int>>();
        Dictionary<string, Color> _symbolColors = new Dictionary<string, Color>();
        Dictionary<string, SymbolLabel> _symbolLabels = new Dictionary<string, SymbolLabel>();

        BarCache _aodBarCache;

        Portfolio.PortfolioType _clientPortfolioType = Portfolio.PortfolioType.Index;
        string _clientPortfolioName = "";

        Portfolio _countryPortfolio = new Portfolio(9);
        Portfolio _symbolPortfolio = new Portfolio(10);
        List<Symbol> _memberList = new List<Symbol>();

        string _currentActiveNavigation = "";
        Stack<string> _navigationHistory = new Stack<string>();

        bool _addFlash = false;

        public event AodEventHandler AodEvent;
        private bool _aodUpdateThreadStop = false;
        private Thread _aodUpdateThread = null;

        ConditionDialog _conditionDialog;

        public MarketMonitor(MainView mainView, ViewType viewType = ViewType.Cost)
        {
            _mainView = mainView;
            _viewType = viewType;

            _region = "GLOBAL";
            _country = "";
            _group = "";
            _subgroup = "";
            _industry = "";
            _symbol = "";
			_condition = "ATM ANALYSIS";
			_asset = "WORLD EQ INDICES";

			InitializeComponent();

            getInfo();

            initializeContextMenu(WorldMap);
            initializeContextMenu(NorthAmericaMap);
            initializeContextMenu(SouthAmericaMap);
            initializeContextMenu(EuropeMap);
            initializeContextMenu(AsiaMap);
            initializeContextMenu(MEAMap);
            initializeContextMenu(OceaniaMap);

            _initialize = true;

            ParameterizedThreadStart start = new ParameterizedThreadStart(updateAodThread);
            _aodUpdateThread = new Thread(start);
            _aodUpdateThread.Start();

            _mapTimer = new DispatcherTimer();
            _mapTimer.Interval = TimeSpan.FromMilliseconds(500);
            _mapTimer.Tick += new System.EventHandler(mapTimer_Tick);
            _mapTimer.Start();

            createAODChart();

            ChartIntervals.Visibility = Visibility.Visible;
        }

        private void requestUpdate(AOD3 aod)
        {
            lock (_aodUpdate)
            {
                if (!_aodUpdate.Contains(aod))
                {
                    _aodUpdate.Enqueue(aod);
                }
            }
        }

        private void updateAodThread(object info)
        {
            while (!_aodUpdateThreadStop)
            {
                AOD3 aod = null;

                lock (_aodUpdate)
                {
                    if (_aodUpdate.Count > 0)
                    {
                        aod = _aodUpdate.Dequeue();
                    }
                }

                while (aod != null)
                {
                    try
                    {

                        var t1 = DateTime.Now;
                        updateAod(aod);
                        var t2 = DateTime.Now;
                        System.Diagnostics.Debug.WriteLine(" 1 " + (t2 - t1).Milliseconds);

                        this.Dispatcher.Invoke(DispatcherPriority.Normal, (Action)delegate () { drawAod(aod); });
                    }
                    catch (Exception x)
                    {

                    }
                    aod = null;
                }
                Thread.Sleep(100);
            }
        }


        private void getInfo()
        {
            try
            {
                L1.Text = "WORLD EQ INDICES";
                L2.Text = "CONDITION OF TREND";
                L3.Text = "ATM ANALYSIS";
				L4.Text = "";
                L5.Text = "";

                _level1 = L1.Text;

                string info = _mainView.GetInfo("Map");
                if (info != null && info.Length > 0)
                {
                    string[] fields = info.Split(';');
                    int count = fields.Length;
                    if (count > 0) { /*ViewType temp; if (Enum.TryParse<ViewType>(fields[0], out temp)) _viewType = temp; */ } 
                    if (count > 1) _region = fields[1];
                    if (count > 2) _country = fields[2];
                    if (count > 3) _group = fields[3];
                    if (count > 4) _subgroup = fields[4];
                    if (count > 5) _industry = fields[5];
                    if (count > 6) _symbol = fields[6];
                    if (count > 7) _condition = fields[7];
                    if (count > 8) _interval = fields[8];
                    if (count > 9) _asset = fields[9];
                    if (count > 10) _ago = int.Parse(fields[10]);
                    if (count > 11) _chartSymbol = fields[11];
                    if (count > 12) _chartInterval = fields[12];
                    if (count > 13) _clientPortfolioName = fields[13];
                    if (count > 14) { int temp = 0; if (int.TryParse(fields[14], out temp)) _clientPortfolioType = (Portfolio.PortfolioType)temp; }
                    if (count > 15) L1.Text = fields[15];
                    if (count > 16) L2.Text = fields[16];
                    if (count > 17) L3.Text = fields[17];
                    if (count > 18) L4.Text = fields[18];
                    if (count > 19) _isFundamental = (fields[19].Length > 0) ? bool.Parse(fields[19]) : false;
                    if (count > 20) _selectedFundamental = fields[20];
                    if (count > 21) L5.Text = fields[21];
                    if (count > 22) _modelName = fields[22];
                    if (count > 23) _factorName = fields[23];
                    if (count > 24) _strategy = fields[24];

                    _level1 = L1.Text;
                }
            }
            catch (Exception x)
            {
                //System.Diagnostics.Debug.WriteLine("Map initialization failure: " + x.Message);
            }

            if (_region == "") _region = "GLOBAL";
        }

        private void setInfo()
        {

            string info =
                _viewType.ToString() + ";" +
                _region + ";" +
                _country + ";" +
                _group + ";" +
                _subgroup + ";" +
                _industry + ";" +
                _symbol + ";" +
                _condition + ";" +
                _interval + ";" +
                _asset + ";" +
                _ago.ToString() + ";" +
                _chartSymbol + ";" +
                _chartInterval + ";" +
                _clientPortfolioName + ";" +
                ((int)_clientPortfolioType).ToString() + ";" +
                L1.Text.ToString() + ";" +
                L2.Text.ToString() + ";" +
                L3.Text.ToString() + ";" +
                L4.Text.ToString() + ";" +
                _isFundamental.ToString() + ";" +
                _selectedFundamental + ";" +
                L5.Text.ToString() + ";" +
                _modelName + ";" +
                _factorName + ";" +
                _strategy;

                _mainView.SetInfo("Map", info);
        }

        public MarketMonitor(MainView mainView, string continent, string country, string group, string subgroup, string industry, string symbol)
        {
            _mainView = mainView;

            _viewType = ViewType.Map;
            _region = continent;
            _country = country;
            _group = group;
            _subgroup = subgroup;
            _industry = industry;
            _symbol = symbol;

            InitializeComponent();

            getInfo();

            _initialize = true;

            _mapTimer = new DispatcherTimer();
            _mapTimer.Interval = TimeSpan.FromMilliseconds(500);
            _mapTimer.Tick += new System.EventHandler(mapTimer_Tick);
            _mapTimer.Start();
        }

        private void initialize()
        {
            ResourceDictionary dictionary = new ResourceDictionary();
            dictionary.Source = new Uri("pack://application:,,,/ATMML;component/StyleDictionary.xaml");
            this.Resources.MergedDictionaries.Add(dictionary);

            _countryPortfolio.PortfolioChanged -= new PortfolioChangedEventHandler(countryPortfolioChanged);
            _symbolPortfolio.PortfolioChanged -= new PortfolioChangedEventHandler(symbolPortfolioChanged);
            _portfolio1.PortfolioChanged -= new PortfolioChangedEventHandler(portfolioChanged);

            _countryPortfolio.PortfolioChanged += new PortfolioChangedEventHandler(countryPortfolioChanged);
            _symbolPortfolio.PortfolioChanged += new PortfolioChangedEventHandler(symbolPortfolioChanged);
            _portfolio1.PortfolioChanged += new PortfolioChangedEventHandler(portfolioChanged);

            _countryBarCache = new BarCache(countryBarChanged);
            _symbolBarCache = new BarCache(symbolBarChanged);
            _aodBarCache = new BarCache(aodBarChanged);

            _mode = MainView.GetChartMode();

            MapTitle.Visibility = Visibility.Visible;
            WorldMap.Visibility = Visibility.Visible;
            WorldMap2.Visibility = Visibility.Collapsed;
            NorthAmericaMap.Visibility = Visibility.Collapsed;
            SouthAmericaMap.Visibility = Visibility.Collapsed;
            EuropeMap.Visibility = Visibility.Collapsed;
            AsiaMap.Visibility = Visibility.Collapsed;
            MEAMap.Visibility = Visibility.Collapsed;
            OceaniaMap.Visibility = Visibility.Collapsed;

            initializeCountries("WorldMap");
            initializeCountries("WorldMap2");
            initializeCountries("NorthAmericaMap");
            initializeCountries("SouthAmericaMap");
            initializeCountries("EuropeMap");
            initializeCountries("AsiaMap");
            initializeCountries("MEAMap");
            initializeCountries("OceaniaMap");

#if POSITIONS
            Trade.Manager.NewPositions += new NewPositionEventHandler(new_Positions);
#endif

            ChartGrid.Visibility = System.Windows.Visibility.Collapsed;
            MLScenario.Visibility = Visibility.Collapsed;

            AgoSlider.Maximum = _maxAgo;

            hideNavigation();

			startMapColoring(1);

			update();

            //setView(_viewType);

            highlightIntervalButton(ChartIntervals, _interval.Replace(" Min", ""));

            initializeAlert();

            if (!BarServer.ConnectedToBloomberg())
            {
                GlobalMacButton.Visibility = Visibility.Collapsed;
            }
        }

        private void setView(ViewType viewType)
        {
            _viewType = viewType;

            var text = (viewType == ViewType.Cost) ? "MONITORS" : "GLOBAL MACRO";
            highlightButton(GlobalCostNav, text, true);

            if (_viewType == ViewType.Symbol)
            {
                requestSymbols();
                WorldMap.Visibility = Visibility.Collapsed;
                startMapColoring(1);
                startSymbolColoring();
                changeChart(_chartSymbol, _chartInterval, true);
            }
            else if (_viewType == ViewType.Map)
            {
                MapGrid.Visibility = Visibility.Visible;
                MapNavigationGrid.Visibility = Visibility.Visible;
                CostCal.Visibility = Visibility.Collapsed;
                AODGrid.Visibility = Visibility.Collapsed;
                AODChartGrid.Visibility = Visibility.Collapsed;
                AOD3ControlGrid.Visibility = Visibility.Collapsed;
                ATMStrategyScrollViewer.Visibility = Visibility.Collapsed;
                PredictionMatrixGrid.Visibility = Visibility.Collapsed;
                CostCalOld.Visibility = Visibility.Collapsed;
                Menus.Visibility = Visibility.Collapsed;
                HelpMenu.Visibility = Visibility.Collapsed;
                GlobalNav.Visibility = Visibility.Collapsed;
                setMapVisibility(_region);

                updateModel();
            }
            else if (_viewType == ViewType.Cost)
            {
                MapGrid.Visibility = Visibility.Collapsed;
                MapNavigationGrid.Visibility = Visibility.Collapsed;
                CostCal.Visibility = Visibility.Visible;
                AODGrid.Visibility = Visibility.Visible;
                AODChartGrid.Visibility = Visibility.Visible;
                AOD3ControlGrid.Visibility = Visibility.Visible;
                ATMStrategyScrollViewer.Visibility = Visibility.Visible;
                PredictionMatrixGrid.Visibility = Visibility.Collapsed;
                CostCalOld.Visibility = Visibility.Collapsed;
                Menus.Visibility = Visibility.Collapsed;
                HelpMenu.Visibility = Visibility.Collapsed;
                GlobalNav.Visibility = Visibility.Collapsed;
                hideMap();

                updateModel();

                loadAODs();
                if (_aods.Count == 0) testAOD();
                drawAod();

                ForecastModel.Content = _strategy;

                calculateAOD();

                _aodChartVisibility = Visibility.Collapsed;
            }
        }

#if POSITIONS
        private void new_Positions(object sender, NewPositionEventArgs e)
        {
            if (e.Complete)
            {
                startMapColoring();
            }
        }
#endif

        private void updateModel()
        {
            var model = MainView.GetModel(_modelName);
            var modelName = (model != null) ? _modelName : "";
            var scenario = (model != null) ? MainView.GetSenarioLabel(model.Scenario) : "";
            ScenarioModel.Content = modelName;
            ScenarioName.Content = scenario;
            ScenarioModel1a.Content = modelName;
            ScenarioName1a.Content = scenario;

            setChartModel();
        }


        private void setChartModel() 
        { 
            var modelNames = new List<string>();
            modelNames.Add(_modelName);
            if (_chart1 != null) _chart1.ModelNames = modelNames;
            if (_chart2 != null) _chart2.ModelNames = modelNames;
            if (_chart3 != null) _chart3.ModelNames = modelNames;
        }

        private void createAODChart()
        {
            AODChartGrid.Visibility = Visibility.Collapsed;
            bool showCursor = !_mainView.HideChartCursor;
            var modelNames = new List<string>();
            modelNames.Add(_modelName);
            _AODchart1 = new Chart(AODChartCanvas1, AODChartControlPanel1, showCursor);
            _AODchart1.HasTitleIntervals = true;
            _AODchart1.LoadProperties("AODChart1");
            _AODchart1.ModelNames = modelNames;
            _AODchart1.ChartEvent += new ChartEventHandler(AODChart_ChartEvent);
            addAODBoxes(true);
        }

        private void showAODChart()
        {
            _AODchart1.Show();
            Grid.SetRowSpan(AODScrollViewer, 2);
        }

        private void addCharts()
        {
            if (_chart1 == null)
            {
                ChartGrid.Visibility = Visibility.Visible;
                MLScenario.Visibility = Visibility.Visible;

                bool showCursor = !_mainView.HideChartCursor;

                _chart1 = new Chart(ChartCanvas1, ChartControlPanel1, showCursor);
                _chart2 = new Chart(ChartCanvas2, ChartControlPanel2, showCursor);
                _chart3 = new Chart(ChartCanvas3, ChartControlPanel3, showCursor);

                _chart1.HasTitleIntervals = true;
                _chart2.HasTitleIntervals = true;
                _chart3.HasTitleIntervals = true;

                loadChartProperties();

                _chart1.AddLinkedChart(_chart2);
                _chart1.AddLinkedChart(_chart3);
                _chart2.AddLinkedChart(_chart1);
                _chart2.AddLinkedChart(_chart3);
                _chart3.AddLinkedChart(_chart1);
                _chart3.AddLinkedChart(_chart2);

                addBoxes(true);

                setChartModel();

                _chart1.ChartEvent += new ChartEventHandler(Chart_ChartEvent);
                _chart2.ChartEvent += new ChartEventHandler(Chart_ChartEvent);
                _chart3.ChartEvent += new ChartEventHandler(Chart_ChartEvent);

                setChartLayout(true, _chart3);

                _chart1.Show();
                _chart2.Show();
                _chart3.Show();

                switchMode(_mode);
            }
        }

        private void switchMode(string mode)
        {
            saveChartProperties();
            saveAODChartProperties();

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

                _chart1.Hide();
                _chart2.Hide();
                _chart3.Hide();

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

        private void removeAODChart()
        {

            AODChartGrid.Visibility = Visibility.Collapsed;

            saveAODChartProperties();

            _AODchart1.Hide();

            Grid.SetRowSpan(AODScrollViewer, 3);
        }

        private void closeAODChart()
        {
            _AODchart1.ChartEvent -= new ChartEventHandler(AODChart_ChartEvent);
            _AODchart1.Close();
            _AODchart1 = null;
        }

        private void loadChartProperties()
        {
            bool research = (_mode == "");

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

            _chart1.LoadProperties(research ? "" : "Positions1_v13");
            _chart2.LoadProperties(research ? "" : "Positions2_v13");
            _chart3.LoadProperties(research ? "" : "Positions3_v13");
        }

        private void saveChartProperties()
        {
            bool research = (_mode == "");

            _chart1.SaveProperties(research ? "" : "Positions1_v13");
            _chart2.SaveProperties(research ? "" : "Positions2_v13");
            _chart3.SaveProperties(research ? "" : "Positions3_v13");
        }

        private void saveAODChartProperties()
        {
            if (_AODchart1 != null)
            {
                bool research = (_mode == "");

                _AODchart1.SaveProperties(research ? "" : "AODChart1");
            }
        }

        void alert_FlashEvent(object sender, EventArgs e)
        {
            Alert alert = sender as Alert;
            if (alert.HasNotification())
            {
                if (alert.UseOnTop)
                {
                    //this.Dispatcher.Invoke(DispatcherPriority.Normal, (Action)delegate () { _mainView.Activate(); });
                }
                _addFlash = true;
            }
        }

        void go_Click(object sender, RoutedEventArgs e)
        {
        }

        void Chart_ChartEvent(object sender, ChartEventArgs e)
        {
            if (e.Id == ChartEventType.ExitCharts)
            {
                switchBackToMap();
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
                bool allCharts = (_chart1.IsVisible() && _chart2.IsVisible() && _chart3.IsVisible());
                setChartLayout(!allCharts, sender as Chart);
            }
            else if (e.Id == ChartEventType.TradeEvent) // trade event change
            {
            }
            else if (e.Id == ChartEventType.ChangeSymbol) // change symbol change
            {
                Chart chart = sender as Chart;
                _chartSymbol = chart.SymbolName;
            }
            else if (e.Id == ChartEventType.ToggleCursor) // toggle cursor on/off
            {
                Chart chart = sender as Chart;
                _mainView.HideChartCursor = !chart.ShowCursor;
            }
            else if (e.Id == ChartEventType.ChartMode)
            {
                switchMode((_mode == "POSITIONS") ? "" : "POSITIONS");
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
        }

        void AODChart_ChartEvent(object sender, ChartEventArgs e)
        {
            if (e.Id == ChartEventType.ExitCharts)
            {
                removeAODChart();
                drawAod();
            }
            else if (e.Id == ChartEventType.PrintChart)
            {
            }
            else if (e.Id == ChartEventType.PartitionCharts)
            {
            }
            else if (e.Id == ChartEventType.TradeEvent) // trade event change
            {
            }
            else if (e.Id == ChartEventType.ChangeSymbol) // change symbol change
            {
                Chart chart = sender as Chart;
            }
            else if (e.Id == ChartEventType.ToggleCursor) // toggle cursor on/off
            {
                Chart chart = sender as Chart;
                _mainView.HideChartCursor = !chart.ShowCursor;
            }
            else if (e.Id == ChartEventType.ChartMode)
            {
            }
            else if (e.Id == ChartEventType.SetupConditions)
            {
            }
            else if (e.Id == ChartEventType.SettingChange)
            {
                var scAddEnbs = _AODchart1.GetSCAddEnbs();
                var updateAOD = false;
                foreach(var kvp in scAddEnbs)
                {
                    if (!_scAddEnbs.ContainsKey(kvp.Key) || kvp.Value != _scAddEnbs[kvp.Key])
                    {
                        updateAOD = true;
                    }
                    _scAddEnbs[kvp.Key] = kvp.Value;
                }

                if (updateAOD) 
                { 
                    updateAods();
                }
            }
            else if (e.Id == ChartEventType.Update)
            {
                foreach (var aod in _aods)
                {
                    if (aod.Symbol == _AODchart1.Symbol && aod.Interval == _AODchart1.DataInterval)
                    {
                        var advice = _AODchart1.GetAdvice(aod.Symbol, aod.Interval);
                        if (advice != null)
                        {

                            aod.DrawAdvice(advice);
                        }
                    }
                }
            }
        }

        Dictionary<string, bool> _scAddEnbs = new Dictionary<string, bool>();

        private void updateAods()
        {
            foreach (var aod in _aods)
            {
                requestUpdate(aod);
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
            ConditionDialog dlg = sender as ConditionDialog;
            if (dlg != null)
            {
                DialogEventArgs.EventType type = e.Type;
                if (e.Type == DialogEventArgs.EventType.Ok)
                {
                    int horizon = dlg.Horizon;
                    string[] conditions = dlg.Condition;

                    MainView.SetConditions(horizon, conditions);
                }
            }
            //DialogWindow.Close();
        }

        private void setChartLayout(bool allCharts, Chart expandChart)
        {
            if (allCharts)
            {
                resetCharts();
                _chart1.Show();
                _chart2.Show();
                _chart3.Show();
            }
            else if (expandChart == _chart1)
            {
                Grid.SetRow(ChartBorder1, 0);
                Grid.SetColumn(ChartBorder1, 0);
                Grid.SetRowSpan(ChartBorder1, 2);
                Grid.SetColumnSpan(ChartBorder1, 2);
                _chart2.Hide();
                _chart3.Hide();
            }
            else if (expandChart == _chart2)
            {
                Grid.SetRow(ChartBorder2, 0);
                Grid.SetColumn(ChartBorder2, 0);
                Grid.SetRowSpan(ChartBorder2, 2);
                Grid.SetColumnSpan(ChartBorder2, 2);
                _chart1.Hide();
                _chart3.Hide();
            }
            else if (expandChart == _chart3)
            {
                Grid.SetRow(ChartBorder3, 0);
                Grid.SetColumn(ChartBorder3, 0);
                Grid.SetRowSpan(ChartBorder3, 2);
                Grid.SetColumnSpan(ChartBorder3, 2);
                _chart1.Hide();
                _chart2.Hide();
            }
            addBoxes(allCharts);
        }

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
            _chart1.InitializeControlBox(imageNames);
            _chart2.InitializeControlBox(imageNames);
            _chart3.InitializeControlBox(imageNames);
        }
        private void addAODBoxes(bool allCharts)
        {
            List<string> imageNames = new List<string>();
            imageNames.Add(@"Images/CloseChart3.png:Close Charts");
            imageNames.Add(allCharts ? @"Images/TileChart.png:Tile Chart" : @"Images/TileChart.png:Tile Charts");
            imageNames.Add(@"Images/Refresh10.png:Cursor Data");
            imageNames.Add(@"Images/Track17.png:Cursor On/Off");
            //imageNames.Add(@"Images/printer icon2.png:Print Chart");
            _AODchart1.InitializeControlBox(imageNames);
        }

        private List<CountryInfo> getSymbols(string type)
        {
            List<CountryInfo> symbols = new List<CountryInfo>();
            CountryInfo[] countryInfos = Portfolio.GetCountryInfos();
            foreach (CountryInfo countryInfo in countryInfos)
            {
                if (countryInfo.Type == type)
                {
                    symbols.Add(countryInfo);
                }
            }
            return symbols;
        }

        private string getRegion(string country)
        {
            string region = "";
            CountryInfo[] countryInfos = Portfolio.GetCountryInfos();
            foreach (CountryInfo countryInfo in countryInfos)
            {
                if (countryInfo.Name == country)
                {
                    region = countryInfo.Region;
                    break;
                }
            }
            return region;
        }

        private void countryBarChanged(object sender, BarEventArgs e)
        {           
            if (e.Type == BarEventArgs.EventType.BarsReceived)
            {
                string interval1 = _interval.Replace(" Min", "");

                string interval2 = e.Interval;

                if (interval1 == interval2)
                {
                    string symbol = e.Ticker;
                    string type1 = getDatabaseAssetType();


                    var times = _countryBarCache.GetTimes(symbol, interval1, 0);
                    if (times.Count > _agoTime.Count)
                    {
                        _agoTime = times;
                    }
 

                    bool isEconomic = isEconomicAsset();

                    if (isEconomic)
                    {
                        bool done = false;

                        CountryInfo[] countryInfos = Portfolio.GetCountryInfos();
                        foreach (CountryInfo countryInfo in countryInfos)
                        {
                            if (symbol == countryInfo.Symbol)
                            {
                                var country = countryInfo.Name;
                                var type2 = countryInfo.Type;

                                if (type1 == type2)
                                {
                                    lock (_countryEconomicValues)
                                    {
                                        var bars = _countryBarCache.GetBars(symbol, interval1, 0);

                                        //Debug.WriteLine("Response: " + country + " " + symbol + " " + bars.Count);

                                        if (country == "UK")
                                        {
                                            for (int ii = 0; ii < bars.Count; ii++)
                                            {
                                                //Debug.WriteLine("    " + (ii + 1).ToString("##") + "   " + bars[ii].Time.ToString("yyyyMMdd") + "  " + bars[ii].Close);
                                            }
                                        }

                                        for (var jj = 0; jj < _agoTime.Count; jj++)
                                        {
                                            var time = _agoTime[jj];

                                            double value = double.NaN;
                                            for (var ii = 0; ii < bars.Count; ii++)
                                            {
                                                var bar = bars[ii];
                                                if (bar.Time > time)
                                                {
                                                    break;
                                                }
                                                value = bar.Close;
                                            }

                                            if (!_countryEconomicValues.ContainsKey(time))
                                            {
                                                _countryEconomicValues[time] = new Dictionary<string, double>();
                                            }
                                            _countryEconomicValues[time][country] = value;
                                        }
                                    }

                                    lock (_countrySymbols)
                                    {
                                        var oldCnt = _countrySymbols.Count;
                                        _countrySymbols.Remove(symbol);
                                        var newCnt = _countrySymbols.Count;
                                        done = oldCnt == 1 && newCnt == 0;
                                    }
                                    break;
                                }
                            }
                        }

                        if (done)
                        {
                            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => { economicColorCountries(); }));
                        }
                    }
                    else if (!_isFundamental) // if condition
                    {
                        var country = new  List<string>();

                        CountryInfo[] countryInfos = Portfolio.GetCountryInfos();
                        foreach (CountryInfo countryInfo in countryInfos)
                        {
                            if (symbol == countryInfo.Symbol)
                            {
                                country.Add(countryInfo.Name);
                            }
                        }

                        calculateCountryColor(country, symbol, interval1, true);

                        lock (_countrySymbols)
                        {
                            _countrySymbols.Remove(symbol);
                        }
                    }
                }
            }
        }

        private void economicColorCountries()
        {
            var date1 = getSelectedDate(1);
            var date2 = getSelectedDate();


            lock (_countryEconomicValues)
            {
                var values = new Dictionary<string, double>();
                if (_countryEconomicValues.ContainsKey(date1))
                {
                    foreach (var country in _countryEconomicValues[date1].Keys)
                    {
                        values[country] = _countryEconomicValues[date1][country];
                    }
                }
                if (_countryEconomicValues.ContainsKey(date2))
                {
                    foreach (var country in _countryEconomicValues[date2].Keys)
                    {
                        values[country] = _countryEconomicValues[date2][country];
                    }
                }

                var colors = calculateCategoryColors(values);

                showLegend();

                foreach (var kvp in colors)
                {
                    var country = kvp.Key;
                    var color = kvp.Value;

                    setCountryColor(country, color);

                    if (values.ContainsKey(country))
                    {
                        setCountryTooltip(country, values[country]);
                    }
                }
            }
        }

        Dictionary<string, bool> getSCAddEnbs()
        {
            Dictionary<string, bool> output = new Dictionary<string, bool>();
            if (_AODchart1 != null)
            {
                output = _AODchart1.GetSCAddEnbs();
            }
            return output;
        }

        private void updateAod(AOD3 aod)
        {
            DateTime time1 = DateTime.Now;

            var ticker = aod.Symbol;
            var modelName = aod.ModelName;
            var interval = aod.Interval;

            var indexTicker = _portfolio1.GetRelativeIndex(ticker);

            var aodInput = new AOD3Input();

            var mpst = new ModelPredictions();
            
            string interval0 = Study.getForecastInterval(interval, 0);
            string interval1 = Study.getForecastInterval(interval, 1);

            var extra = 1;

            List<DateTime> shortTermTimes = _aodBarCache.GetTimes(ticker, interval0, extra);
            Series[] shortTermSeries = _aodBarCache.GetSeries(ticker, interval0, new string[] { "Open", "High", "Low", "Close" }, extra);
            List<DateTime> midTermTimes = _aodBarCache.GetTimes(ticker, interval1, extra);
            Series[] midTermSeries = _aodBarCache.GetSeries(ticker, interval1, new string[] { "Open", "High", "Low", "Close" }, extra);
            var shortTermCurrentBarIndex = shortTermTimes.Count - 1 - extra;
            var midTermCurrentBarIndex = midTermTimes.Count - 1 - extra;
            mpst.predict(ticker, interval0, new string[] { modelName }.ToList(), _aodBarCache);

            _referenceData["Index Prices : " + interval0] = _aodBarCache.GetSeries(indexTicker, interval0, new string[] {"Close" }, extra)[0];
            _referenceData["Index Prices : " + interval1] = _aodBarCache.GetSeries(indexTicker, interval1, new string[] { "Close" }, extra)[0];

            if (shortTermTimes.Count > 0) // && midTermTimes.Count > 0) 
            {
                aodInput.Interval = interval0;
                var model = MainView.GetModel(modelName);
                aodInput.ModelName = (model == null) ? "" : model.Name;
                aodInput.SCAddEnbs = getSCAddEnbs();
                aodInput.ShortTermIndex = shortTermCurrentBarIndex;
                aodInput.ReferenceData = _referenceData;
                aodInput.ShortTermTimes = shortTermTimes;
                aodInput.MidTermTimes = midTermTimes;
                aodInput.ShortTermSeries = shortTermSeries;
                aodInput.MidTermSeries = midTermSeries;
                aodInput.ShortTermPredictions = mpst.getPredictions(ticker, interval0, modelName);
                aodInput.ShortTermActuals = mpst.getActuals(ticker, interval0, modelName);

                aod.update(aodInput);
            }
            //Trace.WriteLine("AOD Update " + (DateTime.Now - time1).Seconds);
        }


        int _indexBarRequestCount = 0;
        Dictionary<string, object> _referenceData = new Dictionary<string, object>();

        private void calculateAOD()
        {
            foreach (var aod in _aods)
            {
                string interval0 = Study.getForecastInterval(_interval, 0);
                string interval1 = Study.getForecastInterval(_interval, 1);

                aod.ShortTermInterval = interval0;
                aod.MidTermInterval = interval1;
                aod.Clear();
                drawAod();
            }
            requestAODIndexBars();
        }

        // todo
        private void changeAODInterval(AOD3 aod, string interval)
        {
            string interval0 = Study.getForecastInterval(interval, 0);
            string interval1 = Study.getForecastInterval(interval, 1);

            aod.ShortTermInterval = interval0;
            aod.MidTermInterval = interval1;
            aod.Clear();
            aod.Draw();

        }

        private void requestAODIndexBars()
        {
            var indexTickers = _aods.Select(x => _portfolio1.GetRelativeIndex(x.Symbol)).Distinct().Where(x => x.Length > 0).ToList();
            var intervals = getAodIntervals();

            _indexBarRequestCount = indexTickers.Count * intervals.Count;
            foreach (var ticker in indexTickers) {
                foreach (var interval in intervals)
                {
                    _aodBarCache.RequestBars(ticker, interval, true);
                }
            }
        }

        private void requestAODBars()
        {
            var tickers = _aods.Select(x => x.Symbol).Distinct();
            var intervals = getAodIntervals();

            foreach(var ticker in tickers)
            {
                foreach(var interval in intervals)
                {
                    _aodBarCache.RequestBars(ticker, interval, true, 300, true);
                }
            }
        }

        private List<string> getAodIntervals(string ticker = "")
        {
            var output = new List<string>();
            foreach(var aod in _aods)
            {
                if (ticker.Length == 0 || ticker == aod.Symbol)
                {
                    output.Add(aod.Interval);
                }
            }
            return output.Distinct().ToList();
        }

        private void aodBarChanged(object sender, BarEventArgs e)
        {
            if (e.Type == BarEventArgs.EventType.BarsReceived)
            {
                string ticker = e.Ticker;
                string interval = e.Interval;

                if (_indexBarRequestCount > 0)
                {
                    if (--_indexBarRequestCount == 0)
                    {
                        requestAODBars();
                    }
                }
                else
                {
                    foreach (var aod in _aods)
                    {
                        if (aod.Symbol == ticker)
                        {
                            requestUpdate(aod);
                        }
                    }
                }
            }
            else if (e.Type == BarEventArgs.EventType.BarsUpdated)
            {
                string ticker = e.Ticker;
                foreach (var aod in _aods)
                {
                    if (aod.Symbol == ticker)
                    {
                        if (!_inUpdateAOD) this.Dispatcher.Invoke(DispatcherPriority.Normal, (Action)delegate () { updateAODClose(aod); });
                        requestUpdate(aod);
                    }
                }
            }
        }

        bool _inUpdateAOD = false;

        private void updateAODClose(AOD3 aod)
        {
            _inUpdateAOD = true;
            var ticker = aod.Symbol;
            var interval = aod.Interval;
            var close = _aodBarCache.GetSeries(ticker, interval, new string[] { "Close" }, 0, 2)[0];
            var count = close.Count;
            if (count >= 2)
            {
                var close0 = close[count - 1];
                var close1 = close[count - 2];
                aod.DrawClose(close0, close1);
            }
            _inUpdateAOD = false;
        }

        private void symbolBarChanged(object sender, BarEventArgs e)
        {
            if (e.Type != BarEventArgs.EventType.BarsUpdated)
            {
                string interval1 = _interval.Replace(" Min", "");

                string symbol = e.Ticker;

                bool isEconomic = isEconomicAsset();

                SymbolLabel label;
                if (_symbolLabels.TryGetValue(symbol, out label))
                {
                    if (isEconomic)
                    {
                        /// todo: 
                    }
                    else
                    {
                        calculateSymbolColor(symbol, interval1, true);
                    }

                    lock (_labelSymbols)
                    {
                        _labelSymbols.Remove(symbol);
                    }
                }
            }
        }

        private void calculateCountryColor(List<string> countries, string symbol, string interval, bool calculate)
        {
            string condition = getDatabaseCondition();

            List<int> signals = getCountrySignals(symbol, interval, condition, calculate);

            if (signals != null && signals.Count > _ago)
            {
                lock (_countryValues)
                {
                    foreach (var country in countries)
                    {
                        _countryValues[country] = signals[_ago];
                    }
                }
            }
        }

        private void calculateSymbolColor(string symbol, string interval, bool calculate)
        {
            string condition = getDatabaseCondition();

            List<int> signals = getSymbolSignals(symbol, interval, condition, calculate);

            if (signals != null && signals.Count > _ago)
            {
                Color color = getConditionColor(symbol, _condition, signals[_ago]);
                lock (_symbolColors)
                {
                    _symbolColors[symbol] = color;
                }
            }
        }

        private void setCountryColor(string name1, Color color)
        {
            Brush brush = new SolidColorBrush(color);
            foreach (Path country in _countryPaths)
            {
                string name2 = country.Name;
                int index = name2.LastIndexOf('_');
                if (index >= 0) name2 = name2.Substring(index + 1);
                index = name2.IndexOfAny(new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' });
                if (index >= 0) name2 = name2.Substring(0, index);
                if (name1 == name2)
                {
                    country.Fill = brush;
                }
            }
        }

        private void setCountryTooltip(string countryName, double value)
        {
            string assetType = getDatabaseAssetType();
            string symbol = _countryPortfolio.GetCountrySymbol(countryName, assetType);

            string dateTimeText = "";
            if (_agoTime != null)
            {
                int idx = _agoTime.Count - 1 - _ago;
                if (idx >= 0)
                {
                    DateTime dateTime = _agoTime[idx];
                    dateTimeText = dateTime.ToString("MMMM dd, yyyy");
                }
            }

            string status = "";
            if (!double.IsNaN(value))
            {
                if (_isFundamental || isEconomicAsset())
                {
                    status = value.ToString(".00");
                }
                else
                {
                    status = getConditionStatus(_condition, value);
                }
            }

#if POSITIONS
            string tooltip = (_condition == "Positions") ? countryName : countryName + "    " + dateTimeText + "\r\r" + symbol + "\r\r" + status + "\r\r" + _time;
#else
            string tooltip = countryName + "    " + dateTimeText + "\r\r" + symbol + "\r\r" + status + "\r\r" + _interval;
#endif
            foreach (Path country in _countryPaths)
            {
                string name2 = country.Name;
                int index = name2.LastIndexOf('_');
                if (index >= 0) name2 = name2.Substring(index + 1);
                index = name2.IndexOfAny(new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' });
                if (index >= 0) name2 = name2.Substring(0, index);
                if (countryName == name2)
                {
                    country.ToolTip = tooltip;
                    country.SetValue(ToolTipService.ShowDurationProperty, 30000);
                }
            }
        }

        private void setSymbolColor(string symbol, Color color)
        {
            SymbolLabel label;
            if (_symbolLabels.TryGetValue(symbol, out label))
            {
                SolidColorBrush brush = label.Foreground as SolidColorBrush;
                brush.Color = color;
            }
        }

        private void resetAllCountryColors()
        {
            lock (_countryValues)
            {
                _countryValues.Clear();
            }
            clearCountryColors();
            clearCountryToolTips();
        }

        private void clearCountryColors()
        {
            Brush brush = Brushes.DarkGray;
            foreach (Path country in _countryPaths)
            {
                country.Fill = brush;
            }
        }

        private void clearCountryToolTips()
        {
            string assetType = getDatabaseAssetType();
            List<CountryInfo> countries = getSymbols(assetType);
            string interval = _interval.Replace(" Min", "");

            var symbols = new Dictionary<string, List<string>>();
            foreach (CountryInfo country in countries)
            {
                setCountryTooltip(country.Name, double.NaN);
            }
        }

        private void requestSignals()
        {
            string assetType = getDatabaseAssetType();
            string condition = getDatabaseCondition();
            string interval = getDatabaseInterval();

            WebClient client = new WebClient();
            string request = "https://tcx1.com/atmsignal2.php";
            request += "?condition=" + condition;
            request += "&assetType=" + assetType;
            request += "&ago=" + _ago.ToString();
            request += "&interval=" + interval;
            request += "&time=" + DateTime.Now.ToString();
            client.DownloadStringCompleted += new DownloadStringCompletedEventHandler(signals_DownloadStringCompleted);
            client.DownloadStringAsync(new Uri(request));
        }

        private string getDatabaseAssetType()
        {
            string assetType = "";
            if (_asset == "WORLD EQ INDICES") assetType = "EQ";
            else if (_asset == "FUNDAMENTALS") assetType = "EQ";
            else if (_asset == "COMMODITIES") assetType = "COM";
            else if (_asset == "USD BASE") assetType = "FX";
            else if (_asset == "EUR BASE") assetType = "EURFX";
            else if (_asset == "GBP BASE") assetType = "GBPFX";
            else if (_asset == "G 10") assetType = "G10FX";
            else if (isRates()) assetType = "FI";

             else assetType = _asset;

            // todo: add economic assets
            return assetType;
        }

        private string getDatabaseCondition()
        {
            string condition = "";
            if (_condition == "TREND CONDITION") condition = "TC";
            else if (_condition == "TREND STRENGTH") condition = "TSB";
            else if (_condition == "TREND HEAT") condition = "HB";
			else if (_condition == "ATM ANALYSIS") condition = "ATM RESEARCH";
			else if (_condition == "CURRENT RESEARCH") condition = "CURRENT RESEARCH";
            else if (_condition == "PR") condition = "PR";
            else if (_condition == "SC") condition = "SC";
            else if (_condition == "FT | FT") condition = "FT | FT";
            else if (_condition == "FT || FT") condition = "FT || FT";
            else if (_condition == "FT | SC") condition = "FT | SC";
            else if (_condition == "FT | SC PR") condition = "FT | SC PR";
            else if (_condition == "FT | ST") condition = "FT | ST";
            else if (_condition == "FT | ST PR") condition = "FT | ST PR";
            else if (_condition == "FT | TSB") condition = "FT | TSB";
            else if (_condition == "FT | TSB PR") condition = "FT | TSB PR";
            else if (_condition == "SC | FT") condition = "SC | FT";
            else if (_condition == "SC | SC") condition = "SC | SC";
            else if (_condition == "SC | ST") condition = "SC | ST";
            else if (_condition == "SC | TSB") condition = "SC | TSB";
            else if (_condition == "SC | SC PR") condition = "SC | SC PR";
            else if (_condition == "SC | ST PR") condition = "SC | ST PR";
            else if (_condition == "SC | TSB PR") condition = "SC | TSB PR";
            else if (_condition == "ST | FT") condition = "ST | FT";
            else if (_condition == "ST | SC") condition = "ST | SC";
            else if (_condition == "ST | SC PR") condition = "ST | SC PR";
            else if (_condition == "ST | ST") condition = "ST | ST";
            else if (_condition == "ST | TSB") condition = "ST | TSB";
            else if (_condition == "ST | ST PR") condition = "ST | ST PR";
            else if (_condition == "ST | TSB PR") condition = "ST | TSB PR";
            else if (_condition == "TSB | FT") condition = "TSB | FT";
            else if (_condition == "TSB | SC") condition = "TSB | SC";
            else if (_condition == "TSB | ST") condition = "TSB | ST";
            else if (_condition == "TSB | TSB") condition = "TSB | TSB";
            else if (_condition == "TSB | SC PR") condition = "TSB | SC PR";
            else if (_condition == "TSB | ST PR") condition = "TSB | ST PR";
            else if (_condition == "TSB | TSB PR") condition = "TSB | TSB PR";
            else if (_condition == "ATM Pxf") condition = "ATM Pxf";

#if POSITIONS
            else if (_condition == "Positions") condition = "Positions";
#endif
            return condition;
        }

        private string getDatabaseInterval()
        {
            string interval = "";
            if (_interval == "Daily") interval = "D";
            else if (_interval == "Weekly") interval = "W";
            else if (_interval == "Monthly") interval = "M";
            else if (_interval == "Quarterly") interval = "Q";
            else if (_interval == "Yearly") interval = "Y";
            else interval = _interval.Replace(" MIN", "");
            return interval;
        }

        private void update()
        {
            setTitle();
            showLegend();

            Region.Content = _region;
            Condition.Content = _condition;
            Time.Content = _interval;

            if (_viewType == ViewType.Symbol)
            {
                RegionTitle.Content = "COUNTRY";
                Region.Content = _country;
                stopSymbolColoring();

                AgoSliderPanel.Visibility = Visibility.Collapsed;
                MapTitle.Visibility = Visibility.Collapsed;
                ChartGrid.Visibility = Visibility.Visible;
                MLScenario.Visibility = Visibility.Visible;

                if (_asset == "WORLD EQ INDICES")
                {
                    Navigation.Visibility = Visibility.Visible;
                    startSymbolColoring();
                }

                addCharts();
            }
            else if (_viewType == ViewType.Map)
            {
                ChartGrid.Visibility = Visibility.Collapsed;
                MLScenario.Visibility = Visibility.Collapsed;

                RegionTitle.Content = "REGION";
                Region.Content = _region;
                startMapColoring(2);

                removeCharts();
            }
            setInfo();
        }

        private DateTime getSelectedDate(int offset = 0)
        {
            DateTime output = default(DateTime);
            if (_agoTime != null)
            {
                int count = _agoTime.Count;
                if (count > 0)
                {
                    int index = count - 1 - _ago - offset;
                    if (index < 0) index = 0;
                    output = _agoTime[index];
                }
            }
            return output;
        }

        private void setTitle()
        {
            if (MapTitle != null && _viewType != ViewType.Cost)
            {
                string timeText = _interval;

                string assetText = _asset;

                string agoText = "";

                if (_agoTime != null)
                {
                    DateTime time = getSelectedDate();
                    if (time == default(DateTime))
                    {
                        agoText = " ";
                    }
                    else if (_interval == "Yearly")
                    {
                        agoText = time.ToString("yyyy");
                    }
                    else if (_interval == "Monthly" || _interval == "Quarterly")
                    {
                        agoText = time.ToString("MMM yyyy");
                    }
                    else if (_interval == "Weekly" || _interval == "Daily")
                    {
                        agoText = time.ToString("MMM d yyyy");
                    }
                    else
                    {
                        agoText = time.ToString("MMM d yyyy HH:mm");
                        timeText += " Min";
                    }
                }

                else
                {
                   //agoText = _ago.ToString() + " Ago";
                }

#if POSITIONS
            MapTitle.Content = (_condition == "Positions") ?
                "Current CMR Positions for " + _region + " " + _asset
                :
                agoText + " " + _time + " " + _condition + " for " + _region + " " + _asset;
#else

                if (_viewType != ViewType.Cost) { 
                    var tb = new TextBlock();
                    if (isEconomicAsset())
                    {
                        tb.Text = agoText + "  " + assetText;
                        ChartIntervals.Visibility = Visibility.Collapsed;
                    }
                    else if (_isFundamental)
                    {
                        tb.Text = agoText + " " + _region + "  " + _selectedFundamental;
                        ChartIntervals.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        tb.Text = timeText + "  " + _condition + " for " + _region + "  " + assetText + "    "  + agoText;
                        ChartIntervals.Visibility = Visibility.Visible;
                    }
                    MapTitle.Content = tb;
                }
            }
#endif
        }

        private void stopMapColoring()
        {
            if (_countryPortfolio != null) _countryPortfolio.Clear();
            if (_countryBarCache != null) _countryBarCache.Clear();
        }

        private void stopSymbolColoring()
        {
            _symbolLabels.Clear();
            _symbolPortfolio.Clear();
            if (_symbolBarCache != null)
            {
                _symbolBarCache.Clear();
            }
        }

        bool _inStartMapColoring = false;

        private void startMapColoring(int id)
        {
            //Debug.WriteLine("startMapColoring: " + id);
            
            try
            {
                stopMapColoring();

                if (_isFundamental)
                {
                    _agoTime = new List<DateTime>();

                    lock (_countryFundamentalValues)
                    {
                        _countryFundamentalValues.Clear();
                    }

                    List<CountryInfo> countryInfos = getSymbols("EQ");
                    lock (_countrySymbols)
                    {
                        _countrySymbols.Clear();

                        List<string> symbols = new List<string>();
                        foreach (var info in countryInfos)
                        {
                            string ticker = info.Symbol;
                            _countrySymbols.Add(ticker);
                            symbols.Add(ticker);
                        }

                        _countrySymbolCount = _countrySymbols.Count;
                        requestFundamentalData(_countryPortfolio, symbols, false);
                    }

                    string interval = _interval.Replace(" Min", "");
                    _countryBarCache.RequestBars("SPX Index", interval, true);
                }
                else
                {
                    string assetType = getDatabaseAssetType();
                    List<CountryInfo> countryInfos = getSymbols(assetType);

                    lock (_countrySymbols)
                    {
                        _countrySymbols.Clear();
                        foreach (var info in countryInfos)
                        {
                            _countrySymbols.Add(info.Symbol);
                        }

                        _countrySymbolCount = _countrySymbols.Count;
                    }

                    string interval = _interval.Replace(" Min", "");

                    _agoTime = new List<DateTime>();

                    _countryBarCache.Clear();

                    List<string> intervals = new List<string>();
                    intervals.Add(interval);
                    intervals.Add(getInterval(interval, 1));

                    foreach (CountryInfo countryInfo in countryInfos)
                    {
                        //Debug.WriteLine("Request: " + countryInfo.Name + " " + countryInfo.Symbol);

                        foreach (var interval2 in intervals)
                        {
                            _countryBarCache.RequestBars(countryInfo.Symbol, interval2, true);
                        }
                    }

                    lock (_countryEconomicValues)
                    {
                        _countryEconomicValues.Clear();
                    }

                    setInfo();
                }
            }
            catch (Exception x)
            {
                //Debug.WriteLine("Map coloring exception: " + x.Message);
            }
        }

        Queue<AOD3> _aodUpdate = new Queue<AOD3>();

        private void mapTimer_Tick(object sender, EventArgs e)
        {
            if (_initialize)
            {
                _initialize = false;
                initialize();
            }

            lock (_memberList)
            {
                if (_memberList.Count > 0)
                {
                    MemberMenu.Children.Clear();
                    foreach (Symbol symbol in _memberList)
                    {
                        SymbolLabel label = getSymbolLabel(symbol.Ticker, symbol.Description);
                        label.MouseDown += new MouseButtonEventHandler(Symbol_MouseDown);

                        MemberMenu.Children.Add(label);
                    }

                    if (_clientPortfolioName != "" || isCMRPortfolio())
                    {
                        string ticker = _memberList[0].Ticker;
                        changeChart(ticker, _chartInterval, false);
                    }
                    _memberList.Clear();
                    startSymbolColoring();
                }
            }

            lock (_countryValues)
            {
                foreach (KeyValuePair<string, int> kvp in _countryValues)
                {
                    string country = kvp.Key;
                    int value = kvp.Value;

                    string assetType = getDatabaseAssetType();
                    string symbol = _countryPortfolio.GetCountrySymbol(country, assetType);

                    Color color = getConditionColor(symbol, _condition, value);
                    setCountryColor(country, color);
                    setCountryTooltip(country, value);
                }
                _countryValues.Clear();
            }

            lock (_symbolColors)
            {
                foreach (KeyValuePair<string, Color> kvp in _symbolColors)
                {
                    setSymbolColor(kvp.Key, kvp.Value);
                }
                _symbolColors.Clear();
            }

            // progress bar
            int count1 = 0;
            lock (_countrySymbols)
            {
                count1 = _countrySymbolCount - _countrySymbols.Count;
            }
            int count2 = _countrySymbolCount;
            double percent = (count2 == 0) ? 0 : 100.0 * count1 / count2;

            bool mapComplete = (count2 == 0 || percent > 95 || count1 >= count2);

            Visibility visibility = (mapComplete || _viewType != ViewType.Map) ? Visibility.Collapsed : Visibility.Visible;

            WorldMap2.Visibility = (_region == "GLOBAL" || visibility == Visibility.Visible) ? Visibility.Collapsed : Visibility.Visible;

            ProgressBar.Visibility = visibility;

            ProgressBar.Value = percent;
            string percentText = percent.ToString("##");
            string textContent = "Calculating ......";
            ProgressBarLabel.Text = (percentText.Length > 0) ? percent.ToString("##") + "%" : "";
            ProgressTitle.Text = (percentText.Length > 0) ? textContent.ToString() : "";

            ProgressBarLabel.Visibility = visibility;
            ProgressTitle.Visibility = visibility;

            if (count1 >= count2)
            {
                lock (_labelSymbols)
                {
                    _labelSymbols.Clear();
                    _labelSymbolCount = 0;
                }
            }

            if (_addFlash)
            {
                _addFlash = false;
                Brush alertColor = Brushes.Red;
            }

            int count = _countryBarCache.GetRequestCount();
            if (count == 0)
            {
            }
            else
            {
            }
        }

        private void drawAod(AOD3 aod)
        {
            _symbolPortfolio.GetDescription(aod.Symbol);
            aod.Draw();
        }

        private void initializeAlert()
        {
            //List<Alert> alerts = Alert.Manager.Alerts;
            //foreach (Alert alert in alerts)
            //{
            //    alert.FlashEvent -= new FlashEventHandler(alert_FlashEvent);
            //    alert.FlashEvent += new FlashEventHandler(alert_FlashEvent);
            //}
        }

        void signals_DownloadStringCompleted(object sender, DownloadStringCompletedEventArgs e)
        {
            string response = e.Result;
            response = Uri.UnescapeDataString(response);
            setCountryColors(response);
        }

        private void setCountryColors(string data)
        {
            Dictionary<string, Color> countryColors = new Dictionary<string, Color>();
            string[] signals = data.Split(':');
            int count = signals.Count();
            for (int ii = 0; ii < count; ii += 2)
            {
                string country = signals[ii].Replace("&s=", "").Replace("&\n", "").Replace(" ", "");
                if (country.Length > 0)
                {
                    int value = int.Parse(signals[ii + 1]);

                    string assetType = getDatabaseAssetType();
                    string symbol = _countryPortfolio.GetCountrySymbol(country, assetType);

                    Color color = getConditionColor(symbol, _condition, value);
                    countryColors[country] = color;
                }
            }

            foreach (Path country in _countryPaths)
            {
                string name = country.Name;
                int index = name.LastIndexOf('_');
                if (index >= 0) name = name.Substring(index + 1);
                index = name.IndexOfAny(new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' });
                if (index >= 0) name = name.Substring(0, index);
                Color color;
                if (countryColors.TryGetValue(name, out color))
                {
                    country.Fill = new SolidColorBrush(color);
                }
            }
        }

        private void initializeCountries(string map)
        {
            Grid grid = FindName(map) as Grid;
            Viewbox viewbox = grid.Children[0] as Viewbox;
            Canvas canvas = viewbox.Child as Canvas;
            foreach (FrameworkElement element in canvas.Children)
            {
                TextBlock textBlock = element as TextBlock;
                string name = (textBlock != null) ? textBlock.Text : element.Name;
                if (name.Length > 0)
                {
                    if (map == "WorldMap") element.MouseLeftButtonDown += World_MouseDown;
                    else if (map == "WorldMap2") element.MouseLeftButtonDown += World_MouseDown2;
                    else element.MouseLeftButtonDown += Country_MouseDown;
                    element.MouseEnter += Country_MouseEnter;
                    element.MouseLeave += Country_MouseLeave;

                    string country = name;
                    int index = country.LastIndexOf('_');
                    if (index > 0) country = country.Substring(index + 1);
                    index = country.IndexOfAny(new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' });
                    if (index >= 0) country = country.Substring(0, index);
                    element.ToolTip = country;

                    Path path = element as Path;
                    if (path != null)
                    {
                        _countryPaths.Add(path);
                    }
                }
            }
        }

        private string getConditionStatus(string condition, double value)
        {
            string tooltip = "";
            if (condition == "TREND CONDITION")
            {
                if (value == 1) tooltip = "Overbought in Bullish Market";
                else if (value == 2) tooltip = "Bullish Market";
                else if (value == 3) tooltip = "Oversold in Bullish Market";
                else if (value == 4) tooltip = "Overbought in Neutral Market";
                else if (value == 5) tooltip = "Neutral Market";
                else if (value == 6) tooltip = "Oversold in Neutral Market";
                else if (value == 7) tooltip = "Overbought in Bearish Market";
                else if (value == 8) tooltip = "Bearish Market";
                else if (value == 9) tooltip = "Oversold in Bearish Market";
            }
            else if (condition == "TREND HEAT")
            {
                if (value == 5) tooltip = "Bullish Market above Target Level 4";
                else if (value == 4) tooltip = "Bullish Market between Target Level 3 and 4";
                else if (value == 3) tooltip = "Bullish Market between Target Level 2 and 3";
                else if (value == 2) tooltip = "Bullish Market between Target Level 1 and 2";
                else if (value == 1) tooltip = "Bullish Market below Target Level 1";
                else if (value == -1) tooltip = "Bearish Market above Target Level 1";
                else if (value == -2) tooltip = "Bearish Market between Target Level 1 and 2";
                else if (value == -3) tooltip = "Bearish Market between Target Level 2 and 3";
                else if (value == -4) tooltip = "Bearish Market between Target Level 3 and 4";
                else if (value == -5) tooltip = "Bearish Market below Target Level 4";
            }
            else if (condition == "TREND STRENGTH")
            {
                if (value == 3) tooltip = "Quick Price move up in potential new Bull Market";
                else if (value == 2) tooltip = "Strong Bullish Market";
                else if (value == 1) tooltip = "Mild Bullish Market";
                else if (value == -1) tooltip = "Mild Bearish Market";
                else if (value == -2) tooltip = "Strong Bearish Market";
                else if (value == -3) tooltip = "Quick Price move down in potential new Bear Market";
                else if (value == 0) tooltip = "Market in Transition";
                else if (value == 4) tooltip = "Market in Bullish Transition";
                else if (value == -4) tooltip = "Market in Bearish Transition";
            }
			else if (condition == "ATM ANALYSIS") // Need New Alert for both
			{
				if (value == 5) tooltip = "Bullish | New Trend";
				else if (value == 4) tooltip = "Bullish | In Long";
				else if (value == 3) tooltip = "Bullish | New Alert";
				else if (value == 2) tooltip = "Bullish | Retracing";
				else if (value == 1) tooltip = "Bullish | Exhaustion";

				else if (value == -1) tooltip = "Bearish | Exhaustion";
				else if (value == -2) tooltip = "Bearish | Retracing";
				else if (value == -3) tooltip = "Bearish | New Alert";
				else if (value == -4) tooltip = "Bearish | In Short";
				else if (value == -5) tooltip = "Bearish | New Trend";
				else if (value == 0) tooltip = "Out";
			}
			else if (condition == "CURRENT RESEARCH")
            {
                if (value == 4) tooltip = "New Long";
                else if (value == 1) tooltip = "Reduce | Wait";
                else if (value == 2) tooltip = "Exit Long";
                else if (value == 3) tooltip = "Long";

                else if (value == -3) tooltip = "Short";
                else if (value == -2) tooltip = "Cover Short";
                else if (value == -1) tooltip = "Reduce | Wait";
                else if (value == -4) tooltip = "New Short";
                else if (value == 0) tooltip = "Out | Wait";
            }

            else if (condition == "PR")
            {
                if (value == 4) tooltip = "New Bullish";
                else if (value == 3) tooltip = "Reduce Bullish";
                else if (value == 2) tooltip = "Exit Bullish";
                else if (value == 1) tooltip = "Is Bullish";

                else if (value == -1) tooltip = "Is Bearish";
                else if (value == -2) tooltip = "Exit Bearish";
                else if (value == -3) tooltip = "Reduce Bearish";
                else if (value == -4) tooltip = "New Bearish";
                else if (value == 0) tooltip = "Out";
            }

            else if (condition == "SC")
            {
                if (value == 1) tooltip = "Bullish";
                else if (value == -1) tooltip = "Bearish";
                else if (value == 0) tooltip = "Out";

            }
            else if (condition == "FT | FT")
            {
                if (value == 1) tooltip = "Bullish";
                else if (value == -1) tooltip = "Bearish";
                else if (value == 0) tooltip = "False";

                //if (value == 4) tooltip = "New Long";
                //else if (value == 3) tooltip = "Reduce Long";
                //else if (value == 2) tooltip = "Exit Long";
                //else if (value == 1) tooltip = "Long";

                //else if (value == -1) tooltip = "Short";
                //else if (value == -2) tooltip = "Cover Short";
                //else if (value == -3) tooltip = "Reduce Short";
                //else if (value == -4) tooltip = "New Short";

                //else if (value == 0) tooltip = "Out";
            }
            else if (condition == "FT || FT")
            {
                if (value == 4) tooltip = "New Long";
                else if (value == 3) tooltip = "Reduce Long";
                else if (value == 2) tooltip = "Exit Long";
                else if (value == 1) tooltip = "Long";

                else if (value == -1) tooltip = "Short";
                else if (value == -2) tooltip = "Cover Short";
                else if (value == -3) tooltip = "Reduce Short";
                else if (value == -4) tooltip = "New Short";

                else if (value == 0) tooltip = "Out";
            }
            else if (condition == "FT | SC")
            {
                if (value == 1) tooltip = "Bullish";
                else if (value == -1) tooltip = "Bearish";
                else if (value == 0) tooltip = "False";

                //if (value == 4) tooltip = "New Long";
                //else if (value == 3) tooltip = "RWaiting on New Long";
                //else if (value == 2) tooltip = "Exit Long";
                //else if (value == 1) tooltip = "Long";

                //else if (value == -1) tooltip = "Short";
                //else if (value == -2) tooltip = "Cover Short";
                //else if (value == -3) tooltip = "Waiting on New Short";
                //else if (value == -4) tooltip = "New Short";
                //else if (value == 0) tooltip = "Out";
            }
            else if (condition == "FT | SC PR")
            {
                if (value == 4) tooltip = "New Bullish";
                else if (value == 3) tooltip = "Reduce Bullish";
                else if (value == 2) tooltip = "Exit Bullish";
                else if (value == 1) tooltip = "Is Bullish";

                else if (value == -1) tooltip = "Is Bearish";
                else if (value == -2) tooltip = "Exit Bearish";
                else if (value == -3) tooltip = "Reduce Bearish";
                else if (value == -4) tooltip = "New Bearish";
                else if (value == 0) tooltip = "Out";
            }
            else if (condition == "FT | ST")
            {
                if (value == 1) tooltip = "Bullish";
                else if (value == -1) tooltip = "Bearish";
                else if (value == 0) tooltip = "False";
            }
            else if (condition == "FT | ST PR")
            {
                if (value == 4) tooltip = "New Bullish";
                else if (value == 3) tooltip = "Reduce Bullish";
                else if (value == 2) tooltip = "Exit Bullish";
                else if (value == 1) tooltip = "Is Bullish";

                else if (value == -1) tooltip = "Is Bearish";
                else if (value == -2) tooltip = "Exit Bearish";
                else if (value == -3) tooltip = "Reduce Bearish";
                else if (value == -4) tooltip = "New Bearish";
                else if (value == 0) tooltip = "Out";
            }
            else if (condition == "FT | TSB")
            {
                if (value == 1) tooltip = "Bullish";
                else if (value == -1) tooltip = "Bearish";
                else if (value == 0) tooltip = "False";
            }
            else if (condition == "FT | TSB PR")
            {
                if (value == 4) tooltip = "New Bullish";
                else if (value == 3) tooltip = "Reduce Bullish";
                else if (value == 2) tooltip = "Exit Bullish";
                else if (value == 1) tooltip = "Is Bullish";

                else if (value == -1) tooltip = "Is Bearish";
                else if (value == -2) tooltip = "Exit Bearish";
                else if (value == -3) tooltip = "Reduce Bearish";
                else if (value == -4) tooltip = "New Bearish";
                else if (value == 0) tooltip = "Out";
            }
            else if (condition == "SC | FT")
            {
                if (value == 1) tooltip = "Bullish";
                else if (value == -1) tooltip = "Bearish";
                else if (value == 0) tooltip = "False";
            }
            else if (condition == "SC | SC")
            {
                if (value == 1) tooltip = "Bullish";
                else if (value == -1) tooltip = "Bearish";
                else if (value == 0) tooltip = "False";
            }
            else if (condition == "SC | SC PR")
            {
                if (value == 4) tooltip = "New Bullish";
                else if (value == 3) tooltip = "Reduce Bullish";
                else if (value == 2) tooltip = "Exit Bullish";
                else if (value == 1) tooltip = "Is Bullish";

                else if (value == -1) tooltip = "Is Bearish";
                else if (value == -2) tooltip = "Exit Bearish";
                else if (value == -3) tooltip = "Reduce Bearish";
                else if (value == -4) tooltip = "New Bearish";
                else if (value == 0) tooltip = "Out";
            }
            else if (condition == "SC | ST")
            {
                if (value == 1) tooltip = "Bullish";
                else if (value == -1) tooltip = "Bearish";
                else if (value == 0) tooltip = "False";
            }
            else if (condition == "SC | ST PR")
            {
                if (value == 4) tooltip = "New Bullish";
                else if (value == 3) tooltip = "Reduce Bullish";
                else if (value == 2) tooltip = "Exit Bullish";
                else if (value == 1) tooltip = "Is Bullish";

                else if (value == -1) tooltip = "Is Bearish";
                else if (value == -2) tooltip = "Exit Bearish";
                else if (value == -3) tooltip = "Reduce Bearish";
                else if (value == -4) tooltip = "New Bearish";
                else if (value == 0) tooltip = "Out";
            }
            else if (condition == "SC | TSB")
            {
                if (value == 1) tooltip = "Bullish";
                else if (value == -1) tooltip = "Bearish";
                else if (value == 0) tooltip = "False";
            }
            else if (condition == "SC | TSB PR")
            {
                if (value == 4) tooltip = "New Bullish";
                else if (value == 3) tooltip = "Reduce Bullish";
                else if (value == 2) tooltip = "Exit Bullish";
                else if (value == 1) tooltip = "Is Bullish";

                else if (value == -1) tooltip = "Is Bearish";
                else if (value == -2) tooltip = "Exit Bearish";
                else if (value == -3) tooltip = "Reduce Bearish";
                else if (value == -4) tooltip = "New Bearish";
                else if (value == 0) tooltip = "Out";
            }
            else if (condition == "ST | FT")
            {
                if (value == 1) tooltip = "Bullish";
                else if (value == -1) tooltip = "Bearish";
                else if (value == 0) tooltip = "False";
            }
            else if (condition == "ST | SC")
            {
                if (value == 1) tooltip = "Bullish";
                else if (value == -1) tooltip = "Bearish";
                else if (value == 0) tooltip = "False";
            }
            else if (condition == "ST | SC PR")
            {
                if (value == 4) tooltip = "New Bullish";
                else if (value == 3) tooltip = "Reduce Bullish";
                else if (value == 2) tooltip = "Exit Bullish";
                else if (value == 1) tooltip = "Is Bullish";

                else if (value == -1) tooltip = "Is Bearish";
                else if (value == -2) tooltip = "Exit Bearish";
                else if (value == -3) tooltip = "Reduce Bearish";
                else if (value == -4) tooltip = "New Bearish";
                else if (value == 0) tooltip = "Out";
            }
            else if (condition == "ST | ST")
            {
                if (value == 1) tooltip = "Bullish";
                else if (value == -1) tooltip = "Bearish";
                else if (value == 0) tooltip = "False";
            }
            else if (condition == "ST | ST PR")
            {
                if (value == 4) tooltip = "New Bullish";
                else if (value == 3) tooltip = "Reduce Bullish";
                else if (value == 2) tooltip = "Exit Bullish";
                else if (value == 1) tooltip = "Is Bullish";

                else if (value == -1) tooltip = "Is Bearish";
                else if (value == -2) tooltip = "Exit Bearish";
                else if (value == -3) tooltip = "Reduce Bearish";
                else if (value == -4) tooltip = "New Bearish";
                else if (value == 0) tooltip = "Out";
            }
            else if (condition == "ST | TSB")
            {
                if (value == 1) tooltip = "Bullish";
                else if (value == -1) tooltip = "Bearish";
                else if (value == 0) tooltip = "False";
            }
            else if (condition == "ST | TSB PR")
            {
                if (value == 4) tooltip = "New Bullish";
                else if (value == 3) tooltip = "Reduce Bullish";
                else if (value == 2) tooltip = "Exit Bullish";
                else if (value == 1) tooltip = "Is Bullish";

                else if (value == -1) tooltip = "Is Bearish";
                else if (value == -2) tooltip = "Exit Bearish";
                else if (value == -3) tooltip = "Reduce Bearish";
                else if (value == -4) tooltip = "New Bearish";
                else if (value == 0) tooltip = "Out";
            }
            else if (condition == "TSB | FT")
            {
                if (value == 1) tooltip = "Bullish";
                else if (value == -1) tooltip = "Bearish";
                else if (value == 0) tooltip = "False";
            }
            else if (condition == "TSB | SC")
            {
                if (value == 1) tooltip = "Bullish";
                else if (value == -1) tooltip = "Bearish";
                else if (value == 0) tooltip = "False";
            }
            else if (condition == "TSB | SC PR")
            {
                if (value == 4) tooltip = "New Bullish";
                else if (value == 3) tooltip = "Reduce Bullish";
                else if (value == 2) tooltip = "Exit Bullish";
                else if (value == 1) tooltip = "Is Bullish";

                else if (value == -1) tooltip = "Is Bearish";
                else if (value == -2) tooltip = "Exit Bearish";
                else if (value == -3) tooltip = "Reduce Bearish";
                else if (value == -4) tooltip = "New Bearish";
                else if (value == 0) tooltip = "Out";
            }
            else if (condition == "TSB | ST")
            {
                if (value == 1) tooltip = "Bullish";
                else if (value == -1) tooltip = "Bearish";
                else if (value == 0) tooltip = "False";
            }
            else if (condition == "TSB | ST PR")
            {
                if (value == 4) tooltip = "New Bullish";
                else if (value == 3) tooltip = "Reduce Bullish";
                else if (value == 2) tooltip = "Exit Bullish";
                else if (value == 1) tooltip = "Is Bullish";

                else if (value == -1) tooltip = "Is Bearish";
                else if (value == -2) tooltip = "Exit Bearish";
                else if (value == -3) tooltip = "Reduce Bearish";
                else if (value == -4) tooltip = "New Bearish";
                else if (value == 0) tooltip = "Out";
            }
            else if (condition == "TSB | TSB")
            {
                if (value == 1) tooltip = "Bullish";
                else if (value == -1) tooltip = "Bearish";
                else if (value == 0) tooltip = "False";
            }
            else if (condition == "TSB | TSB PR")
            {
                if (value == 4) tooltip = "New Bullish";
                else if (value == 3) tooltip = "Reduce Bullish";
                else if (value == 2) tooltip = "Exit Bullish";
                else if (value == 1) tooltip = "Is Bullish";

                else if (value == -1) tooltip = "Is Bearish";
                else if (value == -2) tooltip = "Exit Bearish";
                else if (value == -3) tooltip = "Reduce Bearish";
                else if (value == -4) tooltip = "New Bearish";
                else if (value == 0) tooltip = "Out";
            }
            else if (condition == "ST")
            {
                if (value == 2) tooltip = "New Long";
                else if (value == 1) tooltip = "Current Long";
                else if (value == -1) tooltip = "Current Short";
                else if (value == -2) tooltip = "New Long";
                else if (value == 0) tooltip = "";
            }

            else if (condition == "PR")
            {
                if (value == 4) tooltip = "New Bullish";
                else if (value == 3) tooltip = "Reduce Bullish";
                else if (value == 2) tooltip = "Exit Bullish";
                else if (value == 1) tooltip = "Is Bullish";

                else if (value == -1) tooltip = "Is Bearish";
                else if (value == -2) tooltip = "Exit Bearish";
                else if (value == -3) tooltip = "Reduce Bearish";
                else if (value == -4) tooltip = "New Bearish";
                else if (value == 0) tooltip = "";
            }

            else if (condition == "NET")
            {
                if (value == 3) tooltip = "Net Long 3 Strategies";
                else if (value == 2) tooltip = "Net Long 2 Strategies";
                else if (value == 1) tooltip = "Net Long 1 Strategies";

                else if (value == -1) tooltip = "Net Short 1 Strategies";
                else if (value == -2) tooltip = "Net Short 2 Strategies";
                else if (value == -3) tooltip = "Net Short 3 Strategies";

                else if (value == 0) tooltip = "Strategies Neutral";

            }
            else if (condition == "ATM Pxf")
            {
                if (value == 2) tooltip = "New Up";
                else if (value == 1) tooltip = "Higher";
                else if (value == -1) tooltip = "Lower";
                else if (value == -2) tooltip = "New Dn";
                else if (value == 0) tooltip = "";

            }
            return tooltip;
        }

        private Color getConditionColor(string ticker, string condition, int value)
        {
            int color = 0x808080;
            if (condition == "TREND CONDITION")
            {
                if (value == 1) color = 0x00ffff;  // cyan bullish ob
                else if (value == 2) color = 0x008000;  // dk green bullish os
                else if (value == 3) color = 0x00ff00;  // lime bullish
                else if (value == 4) color = 0x0155ff;  // blue neurtral ob
                else if (value == 5) color = 0xffffff;  // white neurtral
                else if (value == 6) color = 0xffff00;  // was cc9900
                else if (value == 7) color = 0xff8c00;  // orange bearish ob
                else if (value == 8) color = 0xff0000;  // red bearish
                else if (value == 9) color = 0xff00ff;  // magneta bearish os
            }
            else if (condition == "TREND HEAT")
            {
                if (value == 5) color = 0x00ffff;      // 5
                else if (value == 4) color = 0x294109; // 4
                else if (value == 3) color = 0x56ff0c; // 3
                else if (value == 2) color = 0x3f8022; // 2
                else if (value == 1) color = 0x9bcd5c; // 1
                else if (value == -1) color = 0xffff00;  // yellow  was fffff66
                else if (value == -2) color = 0xff8c00;  // orange
                else if (value == -3) color = 0xff1300; // -3
                else if (value == -4) color = 0x81120f; // -4
                else if (value == -5) color = 0xff00ff; // -5
            }
            else if (condition == "TREND STRENGTH")
            {
                if (value == 3) color = 0x00ffff;
                else if (value == 2) color = 0x00ff00;
                else if (value == 1) color = 0x008000;
                else if (value == -1) color = 0xff8000;
                else if (value == -2) color = 0xff0000;
                else if (value == -3) color = 0xff00ff;
                else if (value == 0) color = 0xffffff;
                else if (value == -4) color = 0xff8c00;
                else if (value == 4) color = 0xffff00;
            }
			else if (condition == "ATM ANALYSIS")  //NEED NEW ALERT
			{
				if (value == 5) color = 0xccff66; //  "New Bull Trend" }
				else if (value == 4) color = 0x00ff00; //  "Is Bullish" });
				else if (value == 3) color = 0x008000; //  "New Alert Bullish
				else if (value == 2) color = 0xee82ee; //  "Retrace Bullish" 
				else if (value == 1) color = 0x00ffff; //  "Exh Exit Bullish"
				else if (value == 0) color = 0xffffff; //  "Out" });
				else if (value == -1) color = 0xffff66; //  "Exh Exit Bearish"
				else if (value == -2) color = 0xff8c00; //  "Retrace Bearish" 
				else if (value == -3) color = 0x990000; //  "New Alert Bearish
				else if (value == -4) color = 0xff0000; //  "Is Bearish" });
				else if (value == -5) color = 0xff6666; //  "New Bear Trend" }

			}
			else if (condition == "SC")
            {
                if (value == 1) color = 0x00CC00;
                else if (value == -1) color = 0xA80000;
                else if (value == 0) color = 0xffffff;
            }
            else if (condition == "ST")
            {
                if (value == 2) color = 0x00ff00;
                else if (value == 1) color = 0x00CC00;
                else if (value == -1) color = 0xA80000;
                else if (value == -2) color = 0xff0000;
                else if (value == 0) color = 0xffffff;
            }

            else if (condition == "CURRENT RESEARCH")
            {
                if (value == 4) color = 0x00ff00;       // lime      new Bullish      previous !1.5  current 1.5
                else if (value == 1) color = 0xffff00;  // yellow    reduce Bullish   previou   1.5  current 1.0
                else if (value == 2) color = 0x00ffff;  // cyan      exit Bullish     previous  1.5 or 1.0 current is < 0 
                else if (value == 3) color = 0x00b900;  // dk green  current Bullish  previous  1.5  current 1.5

                else if (value == -4) color = 0xff0000;  // red      new Bearish      previous !-1.5  current -1.5
                else if (value == -1) color = 0xff8c00;  // dr org   reduce Bearish   previou   -1.5  current -1.0
                else if (value == -2) color = 0xff00ff;  // magenta  exit Bearish     previous  -1.5 or -1.0 current is > 0 
                else if (value == -3) color = 0xA80000;  // dk red   current Bearish  previous  -1.5  current -1.5 

                else if (value == 0) color = 0xffffff;
            }

            else if (condition == "PR")
            {
                if (value == 4) color = 0x00ff00;       // lime      new Bullish      previous !1.5  current 1.5
                else if (value == 3) color = 0xffff00;  // yellow    reduce Bullish   previou   1.5  current 1.0
                else if (value == 2) color = 0x00ffff;  // cyan      exit Bullish     previous  1.5 or 1.0 current is < 0 
                else if (value == 1) color = 0x00CC00;  // dk green  current Bullish  previous  1.5  current 1.5

                else if (value == -4) color = 0xff0000;  // red      new Bearish      previous !-1.5  current -1.5
                else if (value == -3) color = 0xff8c00;  // dr org   reduce Bearish   previou   -1.5  current -1.0
                else if (value == -2) color = 0xff00ff;  // magenta  exit Bearish     previous  -1.5 or -1.0 current is > 0 
                else if (value == -1) color = 0xA80000;  // dk red   current Bearish  previous  -1.5  current -1.5 

                else if (value == 0) color = 0xffffff;
            }

            else if (condition == "FT | FT")
            {
                if (value == 4) color = 0x00ff00;       // lime      new Bullish      previous !1.5  current 1.5
                else if (value == 3) color = 0xffff00;  // yellow    reduce Bullish   previou   1.5  current 1.0
                else if (value == 2) color = 0x00ffff;  // cyan      exit Bullish     previous  1.5 or 1.0 current is < 0 
                else if (value == 1) color = 0x00CC00;  // dk green  current Bullish  previous  1.5  current 1.5

                else if (value == -4) color = 0xff0000;  // red      new Bearish      previous !-1.5  current -1.5
                else if (value == -3) color = 0xff8c00;  // dr org   reduce Bearish   previou   -1.5  current -1.0
                else if (value == -2) color = 0xff00ff;  // magenta  exit Bearish     previous  -1.5 or -1.0 current is > 0 
                else if (value == -1) color = 0xA80000;  // dk red   current Bearish  previous  -1.5  current -1.5 

                else if (value == 0) color = 0xffffff;
            }
            else if (condition == "FT || FT")
            {
                if (value == 4) color = 0x00ff00;       // lime      new Bullish      previous !1.5  current 1.5
                else if (value == 3) color = 0xffff00;  // yellow    reduce Bullish   previou   1.5  current 1.0
                else if (value == 2) color = 0x00ffff;  // cyan      exit Bullish     previous  1.5 or 1.0 current is < 0 
                else if (value == 1) color = 0x00CC00;  // dk green  current Bullish  previous  1.5  current 1.5

                else if (value == -4) color = 0xff0000;  // red      new Bearish      previous !-1.5  current -1.5
                else if (value == -3) color = 0xff8c00;  // dr org   reduce Bearish   previou   -1.5  current -1.0
                else if (value == -2) color = 0xff00ff;  // magenta  exit Bearish     previous  -1.5 or -1.0 current is > 0 
                else if (value == -1) color = 0xA80000;  // dk red   current Bearish  previous  -1.5  current -1.5 

                else if (value == 0) color = 0xffffff;
            }
            else if (condition == "FT | SC")
            {
                if (value == 4) color = 0x00ff00;       // lime      new Bullish      previous !1.5  current 1.5
                else if (value == 3) color = 0xffff00;  // yellow    reduce Bullish   previou   1.5  current 1.0
                else if (value == 2) color = 0x00ffff;  // cyan      exit Bullish     previous  1.5 or 1.0 current is < 0 
                else if (value == 1) color = 0x00CC00;  // dk green  current Bullish  previous  1.5  current 1.5

                else if (value == -4) color = 0xff0000;  // red      new Bearish      previous !-1.5  current -1.5
                else if (value == -3) color = 0xff8c00;  // dr org   reduce Bearish   previou   -1.5  current -1.0
                else if (value == -2) color = 0xff00ff;  // magenta  exit Bearish     previous  -1.5 or -1.0 current is > 0 
                else if (value == -1) color = 0xA80000;  // dk red   current Bearish  previous  -1.5  current -1.5 

                else if (value == 0) color = 0xffffff;
            }
            else if (condition == "FT | SC PR")
            {
                if (value == 4) color = 0x00ff00;       // lime      new Bullish      previous !1.5  current 1.5
                else if (value == 3) color = 0xffff00;  // yellow    reduce Bullish   previou   1.5  current 1.0
                else if (value == 2) color = 0x00ffff;  // cyan      exit Bullish     previous  1.5 or 1.0 current is < 0 
                else if (value == 1) color = 0x00CC00;  // dk green  current Bullish  previous  1.5  current 1.5

                else if (value == -4) color = 0xff0000;  // red      new Bearish      previous !-1.5  current -1.5
                else if (value == -3) color = 0xff8c00;  // dr org   reduce Bearish   previou   -1.5  current -1.0
                else if (value == -2) color = 0xff00ff;  // magenta  exit Bearish     previous  -1.5 or -1.0 current is > 0 
                else if (value == -1) color = 0xA80000;  // dk red   current Bearish  previous  -1.5  current -1.5 

                else if (value == 0) color = 0xffffff;
            }
            else if (condition == "FT | ST")
            {
                if (value == 1) color = 0x00ff00;
                else if (value == -1) color = 0xff0000;
                else if (value == 0) color = 0xffffff;
            }
            else if (condition == "FT | ST PR")
            {
                if (value == 4) color = 0x00ff00;       // lime      new Bullish      previous !1.5  current 1.5
                else if (value == 3) color = 0xffff00;  // yellow    reduce Bullish   previou   1.5  current 1.0
                else if (value == 2) color = 0x00ffff;  // cyan      exit Bullish     previous  1.5 or 1.0 current is < 0 
                else if (value == 1) color = 0x00CC00;  // dk green  current Bullish  previous  1.5  current 1.5

                else if (value == -4) color = 0xff0000;  // red      new Bearish      previous !-1.5  current -1.5
                else if (value == -3) color = 0xff8c00;  // dr org   reduce Bearish   previou   -1.5  current -1.0
                else if (value == -2) color = 0xff00ff;  // magenta  exit Bearish     previous  -1.5 or -1.0 current is > 0 
                else if (value == -1) color = 0xA80000;  // dk red   current Bearish  previous  -1.5  current -1.5 

                else if (value == 0) color = 0xffffff;
            }
            else if (condition == "FT | TSB")
            {
                if (value == 1) color = 0x00ff00;
                else if (value == -1) color = 0xff0000;
                else if (value == 0) color = 0xffffff;
            }
            else if (condition == "FT | TSB PR")
            {
                if (value == 4) color = 0x00ff00;       // lime      new Bullish      previous !1.5  current 1.5
                else if (value == 3) color = 0xffff00;  // yellow    reduce Bullish   previou   1.5  current 1.0
                else if (value == 2) color = 0x00ffff;  // cyan      exit Bullish     previous  1.5 or 1.0 current is < 0 
                else if (value == 1) color = 0x00CC00;  // dk green  current Bullish  previous  1.5  current 1.5

                else if (value == -4) color = 0xff0000;  // red      new Bearish      previous !-1.5  current -1.5
                else if (value == -3) color = 0xff8c00;  // dr org   reduce Bearish   previou   -1.5  current -1.0
                else if (value == -2) color = 0xff00ff;  // magenta  exit Bearish     previous  -1.5 or -1.0 current is > 0 
                else if (value == -1) color = 0xA80000;  // dk red   current Bearish  previous  -1.5  current -1.5 

                else if (value == 0) color = 0xffffff;
            }
            else if (condition == "SC | FT")
            {
                if (value == 1) color = 0x00ff00;
                else if (value == -1) color = 0xff0000;
                else if (value == 0) color = 0xffffff;
            }
            else if (condition == "SC | SC")
            {
                if (value == 1) color = 0x00ff00;
                else if (value == -1) color = 0xff0000;
                else if (value == 0) color = 0xffffff;
            }
            else if (condition == "SC | SC PR")
            {
                if (value == 4) color = 0x00ff00;       // lime      new Bullish      previous !1.5  current 1.5
                else if (value == 3) color = 0xffff00;  // yellow    reduce Bullish   previou   1.5  current 1.0
                else if (value == 2) color = 0x00ffff;  // cyan      exit Bullish     previous  1.5 or 1.0 current is < 0 
                else if (value == 1) color = 0x00CC00;  // dk green  current Bullish  previous  1.5  current 1.5

                else if (value == -4) color = 0xff0000;  // red      new Bearish      previous !-1.5  current -1.5
                else if (value == -3) color = 0xff8c00;  // dr org   reduce Bearish   previou   -1.5  current -1.0
                else if (value == -2) color = 0xff00ff;  // magenta  exit Bearish     previous  -1.5 or -1.0 current is > 0 
                else if (value == -1) color = 0xA80000;  // dk red   current Bearish  previous  -1.5  current -1.5 

                else if (value == 0) color = 0xffffff;
            }
            else if (condition == "SC | ST")
            {
                if (value == 1) color = 0x00ff00;
                else if (value == -1) color = 0xff0000;
                else if (value == 0) color = 0xffffff;
            }
            else if (condition == "SC | ST PR")
            {
                if (value == 4) color = 0x00ff00;       // lime      new Bullish      previous !1.5  current 1.5
                else if (value == 3) color = 0xffff00;  // yellow    reduce Bullish   previou   1.5  current 1.0
                else if (value == 2) color = 0x00ffff;  // cyan      exit Bullish     previous  1.5 or 1.0 current is < 0 
                else if (value == 1) color = 0x00CC00;  // dk green  current Bullish  previous  1.5  current 1.5

                else if (value == -4) color = 0xff0000;  // red      new Bearish      previous !-1.5  current -1.5
                else if (value == -3) color = 0xff8c00;  // dr org   reduce Bearish   previou   -1.5  current -1.0
                else if (value == -2) color = 0xff00ff;  // magenta  exit Bearish     previous  -1.5 or -1.0 current is > 0 
                else if (value == -1) color = 0xA80000;  // dk red   current Bearish  previous  -1.5  current -1.5 

                else if (value == 0) color = 0xffffff;
            }
            else if (condition == "SC | TSB")
            {
                if (value == 1) color = 0x00ff00;
                else if (value == -1) color = 0xff0000;
                else if (value == 0) color = 0xffffff;
            }
            else if (condition == "SC | TSB PR")
            {
                if (value == 4) color = 0x00ff00;       // lime      new Bullish      previous !1.5  current 1.5
                else if (value == 3) color = 0xffff00;  // yellow    reduce Bullish   previou   1.5  current 1.0
                else if (value == 2) color = 0x00ffff;  // cyan      exit Bullish     previous  1.5 or 1.0 current is < 0 
                else if (value == 1) color = 0x00CC00;  // dk green  current Bullish  previous  1.5  current 1.5

                else if (value == -4) color = 0xff0000;  // red      new Bearish      previous !-1.5  current -1.5
                else if (value == -3) color = 0xff8c00;  // dr org   reduce Bearish   previou   -1.5  current -1.0
                else if (value == -2) color = 0xff00ff;  // magenta  exit Bearish     previous  -1.5 or -1.0 current is > 0 
                else if (value == -1) color = 0xA80000;  // dk red   current Bearish  previous  -1.5  current -1.5 

                else if (value == 0) color = 0xffffff;
            }
            else if (condition == "ST | FT")
            {
                if (value == 1) color = 0x00ff00;
                else if (value == -1) color = 0xff0000;
                else if (value == 0) color = 0xffffff;
            }
            else if (condition == "ST | SC")
            {
                if (value == 1) color = 0x00ff00;
                else if (value == -1) color = 0xff0000;
                else if (value == 0) color = 0xffffff;
            }
            else if (condition == "ST | SC PR")
            {
                if (value == 4) color = 0x00ff00;       // lime      new Bullish      previous !1.5  current 1.5
                else if (value == 3) color = 0xffff00;  // yellow    reduce Bullish   previou   1.5  current 1.0
                else if (value == 2) color = 0x00ffff;  // cyan      exit Bullish     previous  1.5 or 1.0 current is < 0 
                else if (value == 1) color = 0x00CC00;  // dk green  current Bullish  previous  1.5  current 1.5

                else if (value == -4) color = 0xff0000;  // red      new Bearish      previous !-1.5  current -1.5
                else if (value == -3) color = 0xff8c00;  // dr org   reduce Bearish   previou   -1.5  current -1.0
                else if (value == -2) color = 0xff00ff;  // magenta  exit Bearish     previous  -1.5 or -1.0 current is > 0 
                else if (value == -1) color = 0xA80000;  // dk red   current Bearish  previous  -1.5  current -1.5 

                else if (value == 0) color = 0xffffff;
            }

            else if (condition == "ST | ST")
            {
                if (value == 1) color = 0x00ff00;
                else if (value == -1) color = 0xff0000;
                else if (value == 0) color = 0xffffff;
            }
            else if (condition == "ST | ST PR")
            {
                if (value == 4) color = 0x00ff00;       // lime      new Bullish      previous !1.5  current 1.5
                else if (value == 3) color = 0xffff00;  // yellow    reduce Bullish   previou   1.5  current 1.0
                else if (value == 2) color = 0x00ffff;  // cyan      exit Bullish     previous  1.5 or 1.0 current is < 0 
                else if (value == 1) color = 0x00CC00;  // dk green  current Bullish  previous  1.5  current 1.5

                else if (value == -4) color = 0xff0000;  // red      new Bearish      previous !-1.5  current -1.5
                else if (value == -3) color = 0xff8c00;  // dr org   reduce Bearish   previou   -1.5  current -1.0
                else if (value == -2) color = 0xff00ff;  // magenta  exit Bearish     previous  -1.5 or -1.0 current is > 0 
                else if (value == -1) color = 0xA80000;  // dk red   current Bearish  previous  -1.5  current -1.5 

                else if (value == 0) color = 0xffffff;
            }
            else if (condition == "ST | TSB")
            {
                if (value == 1) color = 0x00ff00;
                else if (value == -1) color = 0xff0000;
                else if (value == 0) color = 0xffffff;
            }

            else if (condition == "ST | TSB PR")
            {
                if (value == 4) color = 0x00ff00;       // lime      new Bullish      previous !1.5  current 1.5
                else if (value == 3) color = 0xffff00;  // yellow    reduce Bullish   previou   1.5  current 1.0
                else if (value == 2) color = 0x00ffff;  // cyan      exit Bullish     previous  1.5 or 1.0 current is < 0 
                else if (value == 1) color = 0x00CC00;  // dk green  current Bullish  previous  1.5  current 1.5

                else if (value == -4) color = 0xff0000;  // red      new Bearish      previous !-1.5  current -1.5
                else if (value == -3) color = 0xff8c00;  // dr org   reduce Bearish   previou   -1.5  current -1.0
                else if (value == -2) color = 0xff00ff;  // magenta  exit Bearish     previous  -1.5 or -1.0 current is > 0 
                else if (value == -1) color = 0xA80000;  // dk red   current Bearish  previous  -1.5  current -1.5 

                else if (value == 0) color = 0xffffff;
            }
            else if (condition == "TSB | FT")
            {
                if (value == 1) color = 0x00ff00;
                else if (value == -1) color = 0xff0000;
                else if (value == 0) color = 0xffffff;
            }
            else if (condition == "TSB | SC")
            {
                if (value == 1) color = 0x00ff00;
                else if (value == -1) color = 0xff0000;
                else if (value == 0) color = 0xffffff;
            }
            else if (condition == "TSB | SC PR")
            {
                if (value == 4) color = 0x00ff00;       // lime      new Bullish      previous !1.5  current 1.5
                else if (value == 3) color = 0xffff00;  // yellow    reduce Bullish   previou   1.5  current 1.0
                else if (value == 2) color = 0x00ffff;  // cyan      exit Bullish     previous  1.5 or 1.0 current is < 0 
                else if (value == 1) color = 0x00CC00;  // dk green  current Bullish  previous  1.5  current 1.5

                else if (value == -4) color = 0xff0000;  // red      new Bearish      previous !-1.5  current -1.5
                else if (value == -3) color = 0xff8c00;  // dr org   reduce Bearish   previou   -1.5  current -1.0
                else if (value == -2) color = 0xff00ff;  // magenta  exit Bearish     previous  -1.5 or -1.0 current is > 0 
                else if (value == -1) color = 0xA80000;  // dk red   current Bearish  previous  -1.5  current -1.5 

                else if (value == 0) color = 0xffffff;
            }
            else if (condition == "TSB | ST")
            {
                if (value == 1) color = 0x00ff00;
                else if (value == -1) color = 0xff0000;
                else if (value == 0) color = 0xffffff;
            }
            else if (condition == "TSB | ST PR")
            {
                if (value == 4) color = 0x00ff00;       // lime      new Bullish      previous !1.5  current 1.5
                else if (value == 3) color = 0xffff00;  // yellow    reduce Bullish   previou   1.5  current 1.0
                else if (value == 2) color = 0x00ffff;  // cyan      exit Bullish     previous  1.5 or 1.0 current is < 0 
                else if (value == 1) color = 0x00CC00;  // dk green  current Bullish  previous  1.5  current 1.5

                else if (value == -4) color = 0xff0000;  // red      new Bearish      previous !-1.5  current -1.5
                else if (value == -3) color = 0xff8c00;  // dr org   reduce Bearish   previou   -1.5  current -1.0
                else if (value == -2) color = 0xff00ff;  // magenta  exit Bearish     previous  -1.5 or -1.0 current is > 0 
                else if (value == -1) color = 0xA80000;  // dk red   current Bearish  previous  -1.5  current -1.5 

                else if (value == 0) color = 0xffffff;
            }
            else if (condition == "TSB | TSB")
            {
                if (value == 1) color = 0x00ff00;
                else if (value == -1) color = 0xff0000;
                else if (value == 0) color = 0xffffff;
            }
            else if (condition == "TSB | TSB PR")
            {
                if (value == 4) color = 0x00ff00;       // lime      new Bullish      previous !1.5  current 1.5
                else if (value == 3) color = 0xffff00;  // yellow    reduce Bullish   previou   1.5  current 1.0
                else if (value == 2) color = 0x00ffff;  // cyan      exit Bullish     previous  1.5 or 1.0 current is < 0 
                else if (value == 1) color = 0x00CC00;  // dk green  current Bullish  previous  1.5  current 1.5

                else if (value == -4) color = 0xff0000;  // red      new Bearish      previous !-1.5  current -1.5
                else if (value == -3) color = 0xff8c00;  // dr org   reduce Bearish   previou   -1.5  current -1.0
                else if (value == -2) color = 0xff00ff;  // magenta  exit Bearish     previous  -1.5 or -1.0 current is > 0 
                else if (value == -1) color = 0xA80000;  // dk red   current Bearish  previous  -1.5  current -1.5 

                else if (value == 0) color = 0xffffff;
            }

            else if (condition == "NET")
            {
                if (value == 3) color = 0x00CC00;  // dk green Net 3
                else if (value == 2) color = 0x00ff00;  // lime exit Net 2
                else if (value == 1) color = 0xadff2f;  // green yellow  Net 1

                else if (value == -3) color = 0xA80000;  // dr red Net -3
                else if (value == -2) color = 0xff0000;  // red net -2
                else if (value == -1) color = 0xA80000;  // tomato   net -1

                else if (value == 0) color = 0xffffff;
            }

            else if (condition == "ATM Pxf")
            {
                if (value == 2) color = 0x00ff00;
                else if (value == 1) color = 0x00CC00;
                else if (value == -1) color = 0xA80000;
                else if (value == -2) color = 0xff0000;
                else if (value == 0) color = 0xffffff;
            }
            byte rr = (byte)((color >> 16) & 0xff);
            byte gg = (byte)((color >> 8) & 0xff);
            byte bb = (byte)(color & 0xff);
            return Color.FromRgb(rr, gg, bb);
        }

        private void Region_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (RegionMenu.Visibility == Visibility.Collapsed)
            {
                hideNavigation();
                Menus.Visibility = Visibility.Visible;
                RegionMenu.Visibility = Visibility.Visible;

                Navigation.Visibility = Visibility.Collapsed;
                ChartGrid.Visibility = Visibility.Collapsed;
                ChartIntervals.Visibility = Visibility.Hidden;
                AgoSliderPanel.Visibility = Visibility.Collapsed;
                MapTitle.Visibility = Visibility.Collapsed;
                MLScenario.Visibility = Visibility.Collapsed;

                setNavigationMenu(RegionMenu, new string[] { "GLOBAL"," ", "NorthAmerica", " ", "SouthAmerica", " ", "EUROPE", " ", "MEA", " ", "ASIA", " ", "OCEANIA" }, Menu_MouseDown);
            }

            else
            {
                hideNavigation();
            }
        }

        private void Condition_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (ConditionMenu.Visibility == Visibility.Collapsed)
            {
                hideNavigation();
                Menus.Visibility = Visibility.Visible;
                ConditionMenu.Visibility = Visibility.Visible;

                Navigation.Visibility = Visibility.Collapsed;
                ChartGrid.Visibility = Visibility.Collapsed;
                ChartIntervals.Visibility = Visibility.Hidden;
                AgoSliderPanel.Visibility = Visibility.Collapsed;
                MapTitle.Visibility = Visibility.Collapsed;
                MLScenario.Visibility = Visibility.Collapsed;

                List<string> items = new List<string>()
                {   
                    "ATM ANALYSIS",
					"",
					"TREND CONDITION",
                    "",
                    "TREND STRENGTH",
                    "",
                    "TREND HEAT",
                    "",
                    "CURRENT RESEARCH",
                    "",
                    "FT | FT",
					"",
					"FT | P",
					"",
                    "FT | SC",
                    "",
                    "FT | SC PR",
                    "",
                    "FT | ST",
                    "",
                    "FT | ST PR",
                    "",
                    "FT | TSB",
                    "",
                    "FT | TSB PR",
                    "",
                    "SC | FT",
					"",
					"SC | P",
                    "",
                    "SC | SC PR",
                    "",
                    "SC | ST",
                    "",
                    "SC | ST PR",
                    "",
                    "SC | TSB",
                    "",
                    "SC | TSB PR",
                    "",
                    "ST | FT",
                    "",
                    "ST | SC",
                    "",
                    "ST | SC PR",
                    "",
                    "ST | ST",
                    "",
                    "ST | ST PR",
                    "",
                    "ST | TSB",
                    "",
                    "ST | TSB PR",
                    "",
                    "TSB | FT",
                    "",
                    "TSB | SC",
                   "",
                   "TSB | SC PR",
                   "",
                   "TSB | ST",
                   "",
                   "TSB | ST PR",
                   "",
                   "TSB | TSB",
                   "",
                   "TSB | TSB PR"
                };
                setNavigationMenu(ConditionMenu, items.ToArray(), Menu_MouseDown);
            }
            else
            {
                hideNavigation();
            }
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
            var label = sender as Label;
            label.Tag = label.Foreground;
            label.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xcc, 0xff));
        }

        private void OurView_MouseLeave(object sender, MouseEventArgs e)
        {
            var label = sender as Label;
            var brush = (Brush)label.Tag;
            label.Foreground = brush;
        }

        private void Server_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Close();
            _mainView.Content = new Timing(_mainView);
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

        private void Asset_MouseEnter(object sender, MouseEventArgs e)
        {
            Label label = sender as Label;
            label.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xcc, 0xff));
        }

        private void Asset_MouseLeave(object sender, MouseEventArgs e)
        {
            Label label = sender as Label;
            label.Foreground = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));
        }
        private void Region_MouseEnter(object sender, MouseEventArgs e)
        {
            TextBlock tb = sender as TextBlock;
            tb.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xcc, 0xff));
        }

        private void Region_MouseLeave(object sender, MouseEventArgs e)
        {
            TextBlock tb = sender as TextBlock;
            tb.Foreground = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));
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

        private void Time_MouseEnter(object sender, MouseEventArgs e)
        {
            Label label = sender as Label;
            label.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xcc, 0xff));
        }

        private void Time_MouseLeave(object sender, MouseEventArgs e)
        {
            Label label = sender as Label;
            label.Foreground = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));
        }

        private void Country_MouseEnter(object sender, MouseEventArgs e)
        {
            this.Cursor = Cursors.Hand;
        }

        private void Country_MouseLeave(object sender, MouseEventArgs e)
        {
            this.Cursor = Cursors.Arrow;
        }

        private void Menu_MouseEnter(object sender, MouseEventArgs e)
        {
            this.Cursor = Cursors.Hand;
        }

        private void Menu_MouseLeave(object sender, MouseEventArgs e)
        {
            this.Cursor = Cursors.Arrow;
        }

        private bool isEconomicAsset()
        {
            return (!_isFundamental && _condition == ""); // (_asset != "WORLD EQ INDICES" && !isRates() && _asset != "GLOBAL FUTURES");
        }

        private void showLegend()
        {
            bool isEconomic = isEconomicAsset();
            bool isCondition = !isEconomic && !_isFundamental;

            string title = "";
            var list1 = new List<LegendRow>();

            if (isCondition)
            {
                title = _condition;
                if (_condition == "TREND STRENGTH")
                {

                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "Strong Up" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0x7f, 0x00), Label = "Early Up" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0xff), Label = "Quick Up" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0x00), Label = "Transition Up" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x8c, 0x00), Label = "Transition Dn" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x0f, 0xff), Label = "Quick Dn" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x8b, 0x00, 0x00), Label = "Early Dn" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "Strong Dn" });

                }
                else if (_condition == "TREND CONDITION" && !isRates())
                {
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0xff), Label = "Bullish O/B" }); //"90 to 100"
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "Bullish" });  //"75 to 90"
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0x80, 0x00), Label = "Bullish O/S" });  //"65 to 75"
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0x80, 0xff), Label = "Neutral O/B" });  //"55 to 65"
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0xff), Label = "Neutral" });  //"45 to 55"
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0x00), Label = "Neutral O/S" });  //"35 to 45"
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x80, 0x00), Label = "Bearish O/B" });  //"25 to 35"
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "Bearish" });  //"10 to 25"
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0xff), Label = "Bearish O/S" });   //"0 to 10"
                }
                else if (_condition == "TREND CONDITION" && isRates())
                {
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0xff), Label = "Higher Rates O/B" }); //"90 to 100"
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "Higher Rates" });  //"75 to 90"
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0x80, 0x00), Label = "Higher Rates O/S" });  //"65 to 75"
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0x80, 0xff), Label = "Neutral O/B" });  //"55 to 65"
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0xff), Label = "Neutral" });  //"45 to 55"
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0x00), Label = "Neutral O/S" });  //"35 to 45"
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x80, 0x00), Label = "Lower Rates O/B" });  //"25 to 35"
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "Lower" });  //"10 to 25"
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0xff), Label = "Lower Rates O/S" });   //"0 to 10"
                }
                else if (_condition == "TREND HEAT")
                {
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0xff), Label = "Px > T4 up" });       // 5
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x29, 0x41, 0x09), Label = "Px > T3 up & < T4 up" }); // 4
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x56, 0xff, 0x0c), Label = "Px > T2 up & < T3 up" }); // 3
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x3f, 0x80, 0x22), Label = "Px > T1 up & < T2 up" });  // 2
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0x80), Label = "Px < T1 up" });        // 1
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x8c, 0x00), Label = "Px > T1 dn" });              // 1
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xf4, 0x4a, 0x0e), Label = "Px < T1 dn & > T2 dn" }); // 2
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x13, 0x00), Label = "Px < T2 dn & > T3 dn" }); // 3
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x81, 0x12, 0x0f), Label = "Px < T3 dn & > T4 dn" }); // 4
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0xff), Label = "Px < T4 dn" });       // 5
                }
				else if (_condition == "ATM ANALYSIS" && !isRates())
				{
					list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xcc, 0xff, 0x66), Label = "Bullish | New Trend" });
					list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "Bullish | In Long" });
					list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0x80, 0x00), Label = "Bullish | New Alert" });
					list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xee, 0x82, 0xee), Label = "Bullish | Retracing" });
					list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0xff), Label = "Bullish | Exit" });  //dc143c
					list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0xff), Label = "Out" });
					list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0x66), Label = "Bearish | Exit" }); //7fff00
					list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x8c, 0x00), Label = "Bearish | Retracing" });
					list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x99, 0x00, 0x00), Label = "Bearish | New Alert" });
					list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "Bearish | In Short" });
					list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x66, 0x66), Label = "Bearish | New Trend" });
					list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0x00, 0x00), Label = "" }); // Blank
					list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0x00, 0x00), Label = "" }); // Blank
				}
				else if (_condition == "ATM ANALYSIS" && isRates())
				{
					list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x66, 0x66), Label = "Bearish | New Trend" });
					list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "Bearish | In Short" });
					list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x99, 0x00, 0x00), Label = "Bearish | New Alert" });
					list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x8c, 0x00), Label = "Bearish | Retracing" });
					list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0x66), Label = "Bearish | Exit" }); //7fff00
					list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0xff), Label = "Out" });
					list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0xff), Label = "Bullish | Exit" }); //dc143c
					list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xee, 0x82, 0xee), Label = "Bullish | Retracing" });
					list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0x80, 0x00), Label = "Bullish | New Alert" });
					list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "Bullish | In Long" });
					list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xcc, 0xff, 0x66), Label = "Bullish | New Trend" });
					list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0x00, 0x00), Label = "" }); // Blank
					list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0x00, 0x00), Label = "" }); // Blank
				}
				else if (_condition == "CURRENT RESEARCH" && !isRates())
                {
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0xff), Label = "Exit Long" });  // cyan
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0x00), Label = "Reduce Long | Wait" });  // yellow 
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xb9, 0x00), Label = "Current Long" });  // dk green
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "New Long" });  // lime

                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "New Short" });  // red  was FromArgb(0xff, 0xff, 0x00, 0x00)
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xb3, 0x00, 0x00), Label = "Current Short" });  //  dk red  was FromArgb(0xff, 0xb3, 0x00, 0x00)
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x8c, 0x00), Label = "Reduce Short | Wait" });  // dk orange
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0xff), Label = "Exit Short" });  // magenta
                }
                else if (_condition == "CURRENT RESEARCH" && isRates())
                {
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0xff), Label = "Exit Short Rates" });  // cyan
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0x00), Label = "Reduce Short | Wait" });  // yellow 
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xb9, 0x00), Label = "Current Short Rates" });  // dk green
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "New Short Rates" });  // lime

                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "New Long Rates" });  // red
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xb3, 0x00, 0x00), Label = "Current Long Rates" });  //  dk red
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x8c, 0x00), Label = "Reduce Long | Wait" });  // dk orange
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0xff), Label = "Exit Long Rates" });  // magenta
                }
                else if (_condition == "SC" && !isRates())
                {
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0x64, 0x00), Label = "Bullish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0xff), Label = "Neutral" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xb3, 0x00, 0x00), Label = "Bearish" });
                }

                else if (_condition == "SC" && isRates())
                {
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0x64, 0x00), Label = "Short Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0xff), Label = "Neutral" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xb3, 0x00, 0x00), Label = "Long Rates" });
                }

                else if (_condition == "PR" && !isRates())
                {
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "New Bullish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0x64, 0x00), Label = "Bullish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0x00), Label = "Reduce Bullish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0xff), Label = "Exit Bullish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0xff), Label = "Out" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0xff), Label = "Exit Bearish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x8c, 0x00), Label = "Reduce Bearish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xb3, 0x00, 0x00), Label = "Bearish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "New Bearish" });
                }

                else if (_condition == "PR" && isRates())
                {
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "New Short Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0x64, 0x00), Label = "Short Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0x00), Label = "Reduce Short Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0xff), Label = "Exit Short Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0xff), Label = "Out" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0xff), Label = "Exit Long Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x8c, 0x00), Label = "Reduce Long Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xb3, 0x00, 0x00), Label = "Long Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "New Long Rates" });
                }
                else if (_condition == "FT | FT" && !isRates())
                {
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "Bullish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0xff), Label = "False" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "Bearish" });
                }

                else if (_condition == "FT | FT" && isRates())
                {
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "Short Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0xff), Label = "False" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "Long Rates" });
                }

                //else if (_condition == "FT | FT" && !isRates())
                //{
                //    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0xff), Label = "Exit Long" });  // cyan
                //    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0x00), Label = "Reduce Long" });  // yellow 
                //    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0x80, 0x00), Label = "Current Long" });  // dk green
                //    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "New Long" });  // lime

                //    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "New Short" });  // red  was FromArgb(0xff, 0xff, 0x00, 0x00)
                //    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xb3, 0x00, 0x00), Label = "Current Short" });  //  dk red  was FromArgb(0xff, 0xb3, 0x00, 0x00)
                //    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x8c, 0x00), Label = "Reduce Short" });  // dk orange
                //    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0xff), Label = "Exit Short" });  // magenta
                //}

                //else if (_condition == "FT | FT" && isRates())
                //{
                //    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0xff), Label = "Exit Short Rates" });  // cyan
                //    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0x00), Label = "Reduce Short Rates" });  // yellow 
                //    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0x80, 0x00), Label = "Current Short Rates" });  // dk green
                //    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "New Short Rates" });  // lime

                //    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "New Long Rates" });  // red
                //    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xb3, 0x00, 0x00), Label = "Current Long Rates" });  //  dk red
                //    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x8c, 0x00), Label = "Cover Long Rates" });  // dk orange
                //    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0xff), Label = "Exit Long Rates" });  // magenta
                //}
                else if (_condition == "FT || FT" && !isRates())
                {
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0xff), Label = "Exit Long" });  // cyan
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0x00), Label = "Reduce Long" });  // yellow 
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0x80, 0x00), Label = "Current Long" });  // dk green
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "New Long" });  // lime

                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "New Short" });  // red  was FromArgb(0xff, 0xff, 0x00, 0x00)
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xb3, 0x00, 0x00), Label = "Current Short" });  //  dk red  was FromArgb(0xff, 0xb3, 0x00, 0x00)
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x8c, 0x00), Label = "Reduce Short" });  // dk orange
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0xff), Label = "Exit Short" });  // magenta
                }
                else if (_condition == "FT || FT" && isRates())
                {
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0xff), Label = "Exit Short Rates" });  // cyan
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0x00), Label = "Reduce Short Rates" });  // yellow 
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0x80, 0x00), Label = "Current Short Rates" });  // dk green
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "New Short Rates" });  // lime

                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "New Long Rates" });  // red
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xb3, 0x00, 0x00), Label = "Current Long Rates" });  //  dk red
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x8c, 0x00), Label = "Cover Long Rates" });  // dk orange
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0xff), Label = "Exit Long Rates" });  // magenta
                }
                else if (_condition == "FT | SC" && !isRates())
                {
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "Bullish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0xff), Label = "False" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "Bearish" });
                }

                else if (_condition == "FT | SC" && isRates())
                {
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "Short Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0xff), Label = "False" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "Long Rates" });
                }
                //else if (_condition == "FT | SC" && !isRates())
                //{
                //    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0xff), Label = "Exit Long" });  // cyan
                //    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0x00), Label = "Reduce Long" });  // yellow 
                //    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0x80, 0x00), Label = "Current Long" });  // dk green
                //    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "New Long" });  // lime

                //    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "New Short" });  // red  was FromArgb(0xff, 0xff, 0x00, 0x00)
                //    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xb3, 0x00, 0x00), Label = "Current Short" });  //  dk red  was FromArgb(0xff, 0xb3, 0x00, 0x00)
                //    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x8c, 0x00), Label = "Reduce Short" });  // dk orange
                //    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0xff), Label = "Exit Short" });  // magenta
                //}

                //else if (_condition == "FT | SC" && isRates())
                //{
                //    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0xff), Label = "Exit Short Rates" });  // cyan
                //    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0x00), Label = "Reduce Short Rates" });  // yellow 
                //    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0x80, 0x00), Label = "Current Short Rates" });  // dk green
                //    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "New Short Rates" });  // lime

                //    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "New Long Rates" });  // red
                //    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xb3, 0x00, 0x00), Label = "Current Long Rates" });  //  dk red
                //    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x8c, 0x00), Label = "Cover Long Rates" });  // dk orange
                //    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0xff), Label = "Exit Long Rates" });  // magenta
                //}
                else if (_condition == "FT | SC PR" && !isRates())
                {
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "New Bullish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0x64, 0x00), Label = "Bullish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0x00), Label = "Reduce Bullish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0xff), Label = "Exit Bullish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0xff), Label = "Out" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0xff), Label = "Exit Bearish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x8c, 0x00), Label = "Reduce Bearish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xb3, 0x00, 0x00), Label = "Bearish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "New Bearish" });
                }

                else if (_condition == "FT | SC PR" && isRates())
                {
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "New Short Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0x64, 0x00), Label = "Short Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0x00), Label = "Reduce Short Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0xff), Label = "Exit Short Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0xff), Label = "Out" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0xff), Label = "Exit Long Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x8c, 0x00), Label = "Reduce Long Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xb3, 0x00, 0x00), Label = "Long Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "New Long Rates" });
                }
                else if (_condition == "FT | ST" && !isRates())
                {
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "Bullish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0xff), Label = "False" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "Bearish" });
                }

                else if (_condition == "FT | ST" && isRates())
                {
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "Short Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0xff), Label = "False" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "Long Rates" });
                }
                else if (_condition == "FT | ST PR" && !isRates())
                {
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "New Bullish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0x64, 0x00), Label = "Bullish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0x00), Label = "Reduce Bullish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0xff), Label = "Exit Bullish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0xff), Label = "Out" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0xff), Label = "Exit Bearish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x8c, 0x00), Label = "Reduce Bearish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xb3, 0x00, 0x00), Label = "Bearish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "New Bearish" });
                }

                else if (_condition == "FT | ST PR" && isRates())
                {
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "New Short Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0x64, 0x00), Label = "Short Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0x00), Label = "Reduce Short Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0xff), Label = "Exit Short Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0xff), Label = "Out" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0xff), Label = "Exit Long Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x8c, 0x00), Label = "Reduce Long Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xb3, 0x00, 0x00), Label = "Long Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "New Long Rates" });
                }
                else if (_condition == "FT | TSB" && !isRates())
                {
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "Bullish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0xff), Label = "False" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "Bearish" });
                }

                else if (_condition == "FT | TSB" && isRates())
                {
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "Short Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0xff), Label = "False" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "Long Rates" });
                }
                else if (_condition == "FT | TSB PR" && !isRates())
                {
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "New Bullish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0x64, 0x00), Label = "Bullish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0x00), Label = "Reduce Bullish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0xff), Label = "Exit Bullish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0xff), Label = "Out" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0xff), Label = "Exit Bearish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x8c, 0x00), Label = "Reduce Bearish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xb3, 0x00, 0x00), Label = "Bearish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "New Bearish" });
                }

                else if (_condition == "FT | TSB PR" && isRates())
                {
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "New Short Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0x64, 0x00), Label = "Short Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0x00), Label = "Reduce Short Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0xff), Label = "Exit Short Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0xff), Label = "Out" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0xff), Label = "Exit Long Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x8c, 0x00), Label = "Reduce Long Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xb3, 0x00, 0x00), Label = "Long Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "New Long Rates" });
                }
                else if (_condition == "SC | FT" && !isRates())
                {
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "Bullish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0xff), Label = "False" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "Bearish" });
                }

                else if (_condition == "SC | FT" && isRates())
                {
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "Short Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0xff), Label = "False" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "Long Rates" });
                }
                else if (_condition == "SC | SC" && !isRates())
                {
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "Bullish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0xff), Label = "False" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "Bearish" });
                }

                else if (_condition == "SC | SC" && isRates())
                {
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "Short Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0xff), Label = "False" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "Long Rates" });
                }
                else if (_condition == "SC | SC PR" && !isRates())
                {
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "New Bullish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0x64, 0x00), Label = "Bullish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0x00), Label = "Reduce Bullish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0xff), Label = "Exit Bullish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0xff), Label = "Out" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0xff), Label = "Exit Bearish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x8c, 0x00), Label = "Reduce Bearish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xb3, 0x00, 0x00), Label = "Bearish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "New Bearish" });
                }

                else if (_condition == "SC | SC PR" && isRates())
                {
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "New Short Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0x64, 0x00), Label = "Short Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0x00), Label = "Reduce Short Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0xff), Label = "Exit Short Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0xff), Label = "Out" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0xff), Label = "Exit Long Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x8c, 0x00), Label = "Reduce Long Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xb3, 0x00, 0x00), Label = "Long Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "New Long Rates" });
                }
                else if (_condition == "SC | ST" && !isRates())
                {
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "Bullish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0xff), Label = "False" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "Bearish" });
                }

                else if (_condition == "SC | ST" && isRates())
                {
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "Short Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0xff), Label = "False" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "Long Rates" });
                }
                else if (_condition == "SC | ST PR" && !isRates())
                {
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "New Bullish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0x64, 0x00), Label = "Bullish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0x00), Label = "Reduce Bullish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0xff), Label = "Exit Bullish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0xff), Label = "Out" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0xff), Label = "Exit Bearish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x8c, 0x00), Label = "Reduce Bearish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xb3, 0x00, 0x00), Label = "Bearish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "New Bearish" });
                }

                else if (_condition == "SC | ST PR" && isRates())
                {
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "New Short Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0x64, 0x00), Label = "Short Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0x00), Label = "Reduce Short Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0xff), Label = "Exit Short Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0xff), Label = "Out" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0xff), Label = "Exit Long Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x8c, 0x00), Label = "Reduce Long Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xb3, 0x00, 0x00), Label = "Long Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "New Long Rates" });
                }
                else if (_condition == "SC | TSB" && !isRates())
                {
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "Bullish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0xff), Label = "False" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "Bearish" });
                }

                else if (_condition == "SC | TSB" && isRates())
                {
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "Short Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0xff), Label = "False" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "Long Rates" });
                }
                else if (_condition == "SC | TSB PR" && !isRates())
                {
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "New Bullish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0x64, 0x00), Label = "Bullish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0x00), Label = "Reduce Bullish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0xff), Label = "Exit Bullish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0xff), Label = "Out" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0xff), Label = "Exit Bearish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x8c, 0x00), Label = "Reduce Bearish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xb3, 0x00, 0x00), Label = "Bearish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "New Bearish" });
                }

                else if (_condition == "SC | TSB PR" && isRates())
                {
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "New Short Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0x64, 0x00), Label = "Short Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0x00), Label = "Reduce Short Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0xff), Label = "Exit Short Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0xff), Label = "Out" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0xff), Label = "Exit Long Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x8c, 0x00), Label = "Reduce Long Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xb3, 0x00, 0x00), Label = "Long Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "New Long Rates" });
                }
                else if (_condition == "ST | FT" && !isRates())
                {
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "Bullish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0xff), Label = "False" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "Bearish" });
                }

                else if (_condition == "ST | FT" && isRates())
                {
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "Short Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0xff), Label = "False" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "Long Rates" });
                }
                else if (_condition == "ST | SC" && !isRates())
                {
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "Bullish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0xff), Label = "False" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "Bearish" });
                }

                else if (_condition == "ST | SC" && isRates())
                {
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "Short Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0xff), Label = "False" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "Long Rates" });
                }
                else if (_condition == "ST | SC PR" && !isRates())
                {
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "New Bullish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0x64, 0x00), Label = "Bullish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0x00), Label = "Reduce Bullish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0xff), Label = "Exit Bullish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0xff), Label = "Out" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0xff), Label = "Exit Bearish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x8c, 0x00), Label = "Reduce Bearish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xb3, 0x00, 0x00), Label = "Bearish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "New Bearish" });
                }

                else if (_condition == "ST | SC PR" && isRates())
                {
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "New Short Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0x64, 0x00), Label = "Short Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0x00), Label = "Reduce Short Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0xff), Label = "Exit Short Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0xff), Label = "Out" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0xff), Label = "Exit Long Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x8c, 0x00), Label = "Reduce Long Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xb3, 0x00, 0x00), Label = "Long Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "New Long Rates" });
                }
                else if (_condition == "ST | ST" && !isRates())
                {
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "Bullish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0xff), Label = "False" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "Bearish" });
                }

                else if (_condition == "ST | ST" && isRates())
                {
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "Short Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0xff), Label = "False" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "Long Rates" });
                }
                else if (_condition == "ST | ST PR" && !isRates())
                {
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "New Bullish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0x64, 0x00), Label = "Bullish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0x00), Label = "Reduce Bullish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0xff), Label = "Exit Bullish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0xff), Label = "Out" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0xff), Label = "Exit Bearish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x8c, 0x00), Label = "Reduce Bearish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xb3, 0x00, 0x00), Label = "Bearish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "New Bearish" });
                }

                else if (_condition == "ST | ST PR" && isRates())
                {
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "New Short Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0x64, 0x00), Label = "Short Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0x00), Label = "Reduce Short Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0xff), Label = "Exit Short Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0xff), Label = "Out" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0xff), Label = "Exit Long Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x8c, 0x00), Label = "Reduce Long Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xb3, 0x00, 0x00), Label = "Long Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "New Long Rates" });
                }
                else if (_condition == "ST | TSB" && !isRates())
                {
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "Bullish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0xff), Label = "False" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "Bearish" });
                }

                else if (_condition == "ST | TSB" && isRates())
                {
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "Short Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0xff), Label = "False" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "Long Rates" });
                }
                else if (_condition == "ST | TSB PR" && !isRates())
                {
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "New Bullish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0x64, 0x00), Label = "Bullish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0x00), Label = "Reduce Bullish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0xff), Label = "Exit Bullish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0xff), Label = "Out" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0xff), Label = "Exit Bearish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x8c, 0x00), Label = "Reduce Bearish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xb3, 0x00, 0x00), Label = "Bearish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "New Bearish" });
                }

                else if (_condition == "ST | TSB PR" && isRates())
                {
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "New Short Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0x64, 0x00), Label = "Short Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0x00), Label = "Reduce Short Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0xff), Label = "Exit Short Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0xff), Label = "Out" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0xff), Label = "Exit Long Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x8c, 0x00), Label = "Reduce Long Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xb3, 0x00, 0x00), Label = "Long Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "New Long Rates" });
                }
                else if (_condition == "TSB | FT" && !isRates())
                {
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "Bullish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0xff), Label = "False" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "Bearish" });
                }

                else if (_condition == "TSB | FT" && isRates())
                {
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "Short Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0xff), Label = "Out" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "Long Rates" });
                }
                else if (_condition == "TSB | SC" && !isRates())
                {
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "Bullish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0xff), Label = "False" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "Bearish" });
                }

                else if (_condition == "TSB | SC" && isRates())
                {
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "Short Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0xff), Label = "Out" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "Long Rates" });
                }
                else if (_condition == "TSB | SC PR" && !isRates())
                {
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "New Bullish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0x64, 0x00), Label = "Bullish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0x00), Label = "Reduce Bullish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0xff), Label = "Exit Bullish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0xff), Label = "Out" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0xff), Label = "Exit Bearish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x8c, 0x00), Label = "Reduce Bearish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xb3, 0x00, 0x00), Label = "Bearish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "New Bearish" });
                }

                else if (_condition == "TSB | SC PR" && isRates())
                {
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "New Short Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0x64, 0x00), Label = "Short Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0x00), Label = "Reduce Short Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0xff), Label = "Exit Short Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0xff), Label = "Out" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0xff), Label = "Exit Long Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x8c, 0x00), Label = "Reduce Long Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xb3, 0x00, 0x00), Label = "Long Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "New Long Rates" });
                }
                else if (_condition == "TSB | ST" && !isRates())
                {
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "Bullish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0xff), Label = "False" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "Bearish" });
                }

                else if (_condition == "TSB | ST" && isRates())
                {
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "Short Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0xff), Label = "False" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "Long Rates" });
                }
                else if (_condition == "TSB | ST PR" && !isRates())
                {
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "New Bullish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0x64, 0x00), Label = "Bullish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0x00), Label = "Reduce Bullish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0xff), Label = "Exit Bullish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0xff), Label = "Out" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0xff), Label = "Exit Bearish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x8c, 0x00), Label = "Reduce Bearish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xb3, 0x00, 0x00), Label = "Bearish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "New Bearish" });
                }

                else if (_condition == "TSB | ST PR" && isRates())
                {
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "New Short Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0x64, 0x00), Label = "Short Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0x00), Label = "Reduce Short Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0xff), Label = "Exit Short Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0xff), Label = "Out" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0xff), Label = "Exit Long Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x8c, 0x00), Label = "Reduce Long Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xb3, 0x00, 0x00), Label = "Long Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "New Long Rates" });
                }
                else if (_condition == "TSB | TSB" && !isRates())
                {
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "Bullish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0xff), Label = "False" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "Bearish" });
                }

                else if (_condition == "TSB | TSB" && isRates())
                {
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "Short Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0xff), Label = "Out" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "Long Rates" });
                }
                else if (_condition == "TSB | TSB PR" && !isRates())
                {
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "New Bullish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0x64, 0x00), Label = "Bullish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0x00), Label = "Reduce Bullish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0xff), Label = "Exit Bullish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0xff), Label = "Out" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0xff), Label = "Exit Bearish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x8c, 0x00), Label = "Reduce Bearish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xb3, 0x00, 0x00), Label = "Bearish" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "New Bearish" });
                }

                else if (_condition == "TSB | TSB PR" && isRates())
                {
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "New Short Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0x64, 0x00), Label = "Short Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0x00), Label = "Reduce Short Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0xff), Label = "Exit Short Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0xff), Label = "Out" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0xff), Label = "Exit Long Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x8c, 0x00), Label = "Reduce Long Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xb3, 0x00, 0x00), Label = "Long Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "New Long Rates" });
                }

                else if (_condition == "ST" && !isRates())
                {
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "New Long" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0x64, 0x00), Label = "Current Long" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xb3, 0x00, 0x00), Label = "Current Short" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "New Short" });

                }

                else if (_condition == "ST" && isRates())
                {
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "New Short Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0x64, 0x00), Label = "Current Short Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xb3, 0x00, 0x00), Label = "Current Long Rates" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "New Long Rates" });
                }

                else if (_condition == "ATM Pxf" && !isRates())
                {
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0x00, 0x00), Label = "ATM Pxf" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0x00, 0x00), Label = "Current Bar | Next Bar" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "Dn | Up" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0x64, 0x00), Label = "Up | Up" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xb3, 0x00, 0x00), Label = "Dn | Dn" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "Up | Dn" });
                }

                else if (_condition == "ATM Pxf" && isRates())
                {
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0x00, 0x00), Label = "ATM Yldf" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0x00, 0x00), Label = "Current | Next" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "Lower   | Higher" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0x64, 0x00), Label = "Higher  | Higher" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xb3, 0x00, 0x00), Label = "Lower   | Lower" });
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "Higher  | Lower" });
                }                                                                                              

                else if (_condition == "NET" && !isRates())
                {

                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0x64, 0x00), Label = "Net Long 3" });  // dk green
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "Net Long 2" });  // lime
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xad, 0xff, 0x2f), Label = "Net Long 1" });  // green yellow
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0xff, 0xff), Label = "Net Neutral" });  // white
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x8c, 0x00), Label = "Net Short 1" });  // tomato
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "Net Short 2" });  // red )
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xb3, 0x00, 0x00), Label = "Net Short 3" });  //  dk red 

                }
                else if (_condition == "NET" && isRates())
                {

                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0x64, 0x00), Label = "Rates - Net Short 3" });  // dk green
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0x00, 0xff, 0x00), Label = "Rates - Net Short 3" }); // lime
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xad, 0xff, 0x2f), Label = "Rates - Net Short 3" });     // green yellow

                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x8c, 0x00), Label = "Rates - Net Long 3" });   // tomato
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xff, 0x00, 0x00), Label = "Rates - Net Long 3" });     // red )
                    list1.Add(new LegendRow { Color = Color.FromArgb(0xff, 0xb3, 0x00, 0x00), Label = "Rates - Net Long 3" });      //  dk red )
                }
            }
            else
            {
                title = isEconomic ? _asset : _selectedFundamental;
                if (_colorBoundaries != null && _colorBoundaries.ContainsKey(_region))
                {
                    List<double> boundaries = _colorBoundaries[_region];
                    var size = _categoryColors.Length;
                    for (int ii = 0; ii < size; ii++)
                    {
                        var color = _categoryColors[ii];
                        var b1 = (ii > 0 && ii - 1 < boundaries.Count) ? boundaries[ii - 1].ToString("##.00") : "";
                        var b2 = (ii < boundaries.Count) ? boundaries[ii].ToString("##.00") : "";
                        var label = (ii == 0) ? "> " + b2 : ((ii == size - 1) ? "<= " + b1 : b1 + " to " + b2);
                        list1.Add(new LegendRow { Color = color, Label = label });
                    }
                }
             }

            drawLegend(title, list1);
        }

        private bool isRates()
        {
            return (_level1 == "INTEREST RATES");
        }

        string _region1;
        string _country1;
        string _group1;
        string _subgroup1;
        string _industry1;

        string l1;
        string l2;
        string l3;
        string l4;
        string l5;

        private void Level0_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (Menus.Visibility == Visibility.Visible)
            {
                _region = _region1;
                _country = _country1;
                _group = _group1;
                _subgroup = _subgroup1;
                _industry = _industry1;
                L1.Text = l1;
                L2.Text = l2;
                L3.Text = l3;
                L4.Text = l4;
                L5.Text = l5;

                _condition = _previousCondition;
                _viewType = ViewType.Map;
                _region = "GLOBAL";

                Menus.Visibility = Visibility.Collapsed;
                Level1.Visibility = Visibility.Collapsed;
                Level2.Visibility = Visibility.Collapsed;
                Level3.Visibility = Visibility.Collapsed;
                Level4.Visibility = Visibility.Collapsed;
                Level5.Visibility = Visibility.Collapsed;

                Navigation.Visibility = Visibility.Collapsed;
                ChartGrid.Visibility = Visibility.Collapsed;
                AgoSliderPanel.Visibility = Visibility.Visible;
                MapTitle.Visibility = Visibility.Visible;
                ChartIntervals.Visibility = Visibility.Visible;
                MLScenario.Visibility = Visibility.Collapsed;

                removeCharts();
                setMapVisibility(_region);
            }
            else
            {
                _region1 = _region;
                _country1 = _country;
                _group1 = _group;
                _subgroup1 = _subgroup;
                _industry1 = _industry;
                l1 = L1.Text;
                l2 = L2.Text;
                l3 = L3.Text;
                l4 = L4.Text;
                l5 = L5.Text;

                removeCharts();
                setMapVisibility(_region);
                showLevel1();
                highlightButton(Level1, L1.Text as string);
            }
        }

        private void showLevel1()
        {
            _previousCondition = _condition;

            hideMap();
            Menus.Visibility = Visibility.Visible;
            Level1.Visibility = Visibility.Visible;
            Level2.Visibility = Visibility.Collapsed;
            Level3.Visibility = Visibility.Collapsed;
            Level4.Visibility = Visibility.Collapsed;
            Level5.Visibility = Visibility.Collapsed;

            Navigation.Visibility = Visibility.Collapsed;
            ChartGrid.Visibility = Visibility.Collapsed;
            ChartIntervals.Visibility = Visibility.Visible;
            AgoSliderPanel.Visibility = Visibility.Collapsed;
            MapTitle.Visibility = Visibility.Collapsed;
            MLScenario.Visibility = Visibility.Collapsed;

            setNavigationMenu(Level1, new string[] {
                "WORLD EQUITY INDICES",
                "GLOBAL FX RATES",
                "GLOBAL INTEREST RATES",
                " ",
                "AGRI | MEATS",
                "AGRI | FERTILIZER",
                "AGRI | FOODSTUFF",
                "AGRI | FOREST PRODUCTS",
                "AGRI | FIBERS",
                "AGRI | GRAINS",
                "AGRI | OLECHEMICALS",
                "AGRI | SOFTS",
                " ",
                "ENERGY | COKING COAL",
                "ENERGY | CRUDE",
                "ENERGY | NAT GAS",
                "ENERGY | PETROCHEMICAL",
                "ENERGY | REFINED HEAVY",
                "ENERGY | REFINED LIGHT",
                "ENERGY | REFINED MIDDLE",
                "",
                "ENVIRONMENT | EMISSIONS",
                " ",
                "METALS | BASE",
                "METALS | BRASS",
                "METALS | FERRO ALLOYS",
                "METALS | MINERALS",
                "METALS | MINOR",
                "METALS | PRECIOUS",
                "METALS | RARE EARTHS",
                "METALS | STEEL",
                " ",
                "EQ FUNDAMENTALS",
                " ",
                "COUNTRY STATISTICS"
            }, Level1_MouseDown);            
        }

        string _level1 = "";

        private void Level1_MouseDown(object sender, MouseButtonEventArgs e)
        {
            TextBlock label = sender as TextBlock;
            string name = label.Text as string;

            L1.Text = name;
            L2.Text = "";
            L3.Text = "";
            L4.Text = "";
            L5.Text = "";

            _level1 = L1.Text;

            showLevel2(name);
        }

        string _previousCondition = "";

        private void showLevel2(string name)
        {
            _condition = "";

            hideMap();
            Level2.Visibility = Visibility.Visible;
            Level3.Visibility = Visibility.Collapsed;
            Level4.Visibility = Visibility.Collapsed;
            Level5.Visibility = Visibility.Collapsed;

            highlightButton(Level1, name);

            if (name == "OTHER COSTS")
            {
                _asset = "WORLD EQ INDICES";
                setNavigationMenu(Level2, new string[] {"INTEREST RATES", " ", "WORLD EQ INDICES", " ", "USD BASE" }, Level2_MouseDown);
            }
            
            else if (name == "GLOBAL FX RATES")
            {
                _asset = "FX";
                setNavigationMenu(Level2, new string[] {"USD BASE", "EUR BASE", "GBP BASE", "G 10" }, Level2_MouseDown);
            }
                        
            else if (name == "GLOBAL INTEREST RATES")
            {
                _asset = "RATES";
                setNavigationMenu(Level2, new string[] {"INTEREST RATES" }, Level2_MouseDown);
            }
                        
            else if (name == "WORLD EQUITY INDICES")
            {
                _asset = "WORLD EQ INDICES";
                setNavigationMenu(Level2, new string[] {"WORLD EQ INDICES" }, Level2_MouseDown);
            }

            else if (name == "AGRI | MEATS")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level2, new string[] { 
                    "MEATS | CATTLE",
                    "MEATS | PORK",
                    "MEATS | POULTRY",
                    "MEATS | SEAFOOD",
                    "MEATS | SHEEP",
                }, Level2_MouseDown);
            }

            else if (name == "AGRI | FERTILIZER")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level2, new string[] { 
                    "FERT | UREA",
                    "FERT | AMMOFOS",
                    "FERT | AMMONIA",
                    "FERT | AMMO NITRATE",
                    "FERT | AMMO SULFATE",
                    "FERT | AQUA AMMONIA",
                    "FERT | CALCIUM AMMO NITRATE",
                    "FERT | DIAMMOFOS",
                    "FERT | MONO AMMO PHOSPHATE",
                    "FERT | PHOSPHORIC ROCK",
                    "FERT | PHOSPORIC ACID",
                    "FERT | POTASH",
                    "FERT | POTASSIUM CHLORIDE",
                    "FERT | POTASSIUM NITRATE",
                    "FERT | POTASSIUM SULFATE",
                    "FERT | SODA ASH",
                    "FERT | SODIUM MITRATE",
                    "FERT | SOP MAGNESIA",
                    "FERT | SULFUR",
                    "FERT | SULFURIC ACID",
                }, Level2_MouseDown);
            }

            else if (name == "AGRI | FOODSTUFF")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level2, new string[] { 
                    "FOOD | DAIRY", 
                    "FOOD | EGGS", 
                    "FOOD | FOOD OIL" 
                }, Level2_MouseDown);
            }

            else if (name == "AGRI | FOREST PRODUCTS")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level2, new string[] { 
                    "FOREST | LUMBER", 
                    "FOREST | PAPER" }, Level2_MouseDown);
            }
            else if (name == "AGRI | FIBERS")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level2, new string[] { 
                    "FIBERS | COTTON" }, Level2_MouseDown);
            }
            else if (name == "AGRI | OLECHEMICALS")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level2, new string[] { 
                    "OLECHEM | FATTY ACID", 
                    "OLECHEM | FATTY ALCOHOL", 
                    "OLECHEM | GLYCERINE", 
                    "OLECHEM | VEGETABLE FAT" 
                }, Level2_MouseDown);
            }
            else if (name == "AGRI | GRAINS")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level2, new string[] {
                    "GRAINS | CORN",
                    "GRAINS | SOYBEANS",
                    "GRAINS | WHEAT",                    
                    "GRAINS | BARLEY",                    
                    "GRAINS | OATS",                   
                    "GRAINS | RICE",
                    //"GRAINS | OILSEED",
                }, Level2_MouseDown);
            }
            else if (name == "AGRI | SOFTS")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level2, new string[] { 
                    "SOFTS | COCOA",                     
                    "SOFTS | COFFEE",                    
                    "SOFTS | OJ",                    
                    "SOFTS | SUGAR",
                }, Level2_MouseDown);
            }
            else if (name == "ENERGY | COKING COAL")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level2, new string[] {
                    "COKING COAL | COKE"
                }, Level2_MouseDown);
            }
            else if (name == "ENERGY | NAT GAS")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level2, new string[] {
                    "NAT GAS | CNG",
                    "NAT GAS | LNG",
                }, Level2_MouseDown);
            }
            else if (name == "ENERGY | CRUDE")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level2, new string[] {
                    "CRUDE | CRUDE OIL",
                }, Level2_MouseDown);
            }
            else if (name == "ENERGY | PETROCHEMICAL")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level2, new string[] {
                    "PETRO | 2-ETHYLHEXANOL",
                    "PETRO | ACETIC ACID",
                    "PETRO | ACETONE",
                    "PETRO | ACRYLIC ACID",
                    "PETRO | ADIPIC ACID",
                    "PETRO | ALPHA OLEFIN",
                    "PETRO | AROMA LINEAR ALKYLBENZENE",
                    "PETRO | AROMA MIXED XYLENES",
                    "PETRO | AROMA TOLUENE",
                    "PETRO | BASE OILS",
                    "PETRO | CAPROLACTAM",
                    "PETRO | CARBON BLACK",
                    "PETRO | CYCLOHEXANONE",
                    "PETRO | EDC",
                    "PETRO | EPOXIDE RESINS",
                    "PETRO | ETHYLENE GLYCOL",
                    "PETRO | ETHYLENE OXIDE",
                    "PETRO | HDPE",
                    "PETRO | ISOPROPYL ALCOHOL",
                    "PETRO | LDPE",
                    "PETRO | MALEIC ANHYDRIDE",
                    "PETRO | MDI",
                    "PETRO | MEG",
                    "PETRO | MONOMER CAN",
                    "PETRO | MTBE",
                    "PETRO | OCTANOL",
                    "PETRO | OLEFINS BUTADIENE",
                    "PETRO | OLEFINS ETHYLENE",
                    "PETRO | OLEFINS PROPYLENE",
                    "PETRO | PET",
                    "PETRO | PHENOL",
                    "PETRO | POLYAMIDE FIBER",
                    "PETRO | POLYAMIDE RESIN",
                    "PETRO | POLYESTER RESIN",
                    "PETRO | POLYMER ABS",
                    "PETRO | POLYMER EVA",
                    "PETRO | POLYMER LLDPE",
                    "PETRO | POLYMER POLYSTYRENE",
                    "PETRO | POLYMER SBR",
                    "PETRO | PP",
                    "PETRO | PROPYLENE GLYCOL",
                    "PETRO | PROPYLENE OXIDE",
                    "PETRO | PTA",
                    "PETRO | PVC",
                    "PETRO | STYRENE",
                    "PETRO | TDI",
                    "PETRO | VCM",
                    "PETRO | VINYL ACETATE MONOMER"
                }, Level2_MouseDown);
            }
            else if (name == "ENERGY | REFINED HEAVY")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level2, new string[] {
                    "REF HEAVY | BITUMEN",
                    "REF HEAVY | FUEL OIL",
                    "REF HEAVY | MARINE DIESEL",
                    "REF HEAVY | MARINE GAS"
                }, Level2_MouseDown);
            }
            else if (name == "ENERGY | REFINED LIGHT")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level2, new string[] {
                    "REF LIGHT | CONDENSATE",
                    "REF LIGHT | GASOLINE GASOHOL",
                    "REF LIGHT | GASOLINE LEAD SUB",
                    "REF LIGHT | GASOLINE REFORMATE",
                    "REF LIGHT | NAPHTHA"
                }, Level2_MouseDown);
            }
            else if (name == "ENERGY | REFINED MIDDLE")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level2, new string[] {
                    "REF MIDDLE | DIESEL",
                    "REF MIDDLE | GASOIL",
                    "REF MIDDLE | JET FUEL",
                }, Level2_MouseDown);
            }
            else if (name == "ENVIRONMENT | EMISSIONS")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level2, new string[] {
                    "EMISSIONS | EMISSIONS",
                }, Level2_MouseDown);
            }
            else if (name == "ENVIRONMENT | RENEWABLES")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level2, new string[] {
                    "RENEWABLES | RENEWABLES",
                }, Level2_MouseDown);
            }
            else if (name == "ENVIRONMENT | WEATHER")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level2, new string[] {
                    "WEATHER | WEATHER",
                }, Level2_MouseDown);
            }
            else if (name == "METALS | BASE")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level2, new string[] { 
                    "BASE | ALUMINUM", 
                    "BASE | COPPER", 
                    "BASE | IRON", 
                    "BASE | LEAD", 
                    "BASE | NICKEL", 
                    "BASE | TIN", 
                    "BASE | URANIUM", 
                    "BASE | ZINC" 
                }, Level2_MouseDown);
            }
            else if (name == "METALS | BRASS")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level2, new string[] { 
                    "BRASS | BRASS" 
                }, Level2_MouseDown);
            }

            else if (name == "METALS | MINOR")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level2, new string[] { 
                    "MINOR | ANTIMONY",
                    "MINOR | ARSENIC",
                    "MINOR | BERYLLIUM",
                    "MINOR | BISMUTH",
                    "MINOR | CADMIUM",
                    "MINOR | CALCIUM",
                    "MINOR | CARBON",
                    "MINOR | CHROMIUM",
                    "MINOR | COBALT",
                    "MINOR | GALLIUM",
                    "MINOR | GERMANIUM",
                    "MINOR | INDIUM",
                    "MINOR | LITHIUM",
                    "MINOR | MAGNESIUM",
                    "MINOR | MANGANESE",
                    "MINOR | MERCURY",
                    "MINOR | MOLYBDENUM",
                    "MINOR | NIOBIUM",
                    "MINOR | POTASSIUM",
                    "MINOR | SELENIUM",
                    "MINOR | SILICON",
                    "MINOR | SODIUM",
                    "MINOR | STRONTIUM",
                    "MINOR | TANTALUM",
                    "MINOR | TELLURIUM",
                    //"MINOR | TITANIUM",
                    "MINOR | TUNGSTEN",
                    "MINOR | VANADIUM",
                    "MINOR | ZIRCONIUM"
                }, Level2_MouseDown);
            }
            else if (name == "METALS | PRECIOUS")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level2, new string[] {
                    "PRECIOUS | GOLD",
                    "PRECIOUS | SILVER",
                    "PRECIOUS | PALLADIUM",          
                    "PRECIOUS | PLATINUM",
                    "PRECIOUS | IRIDIUM",
                    "PRECIOUS | RHENIUM",
                    "PRECIOUS | RHODIUM",
                    "PRECIOUS | RUTHENIUM",
                }, Level2_MouseDown);
            }

            else if (name == "METALS | RARE EARTHS")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level2, new string[] {
                    //"RARE EARTHS | CERIUM",
                    "RARE EARTHS | DYSPROSIUM",
                    "RARE EARTHS | ERBIUM",
                    "RARE EARTHS | EUROPIUM",
                    "RARE EARTHS | GADOLINIUM",
                    "RARE EARTHS | HOLMIUM",
                    "RARE EARTHS | LANTHANUM",
                    "RARE EARTHS | LUTETIUM",
                    "RARE EARTHS | MISCHMETAL",
                    "RARE EARTHS | NEODYMIUM",
                    "RARE EARTHS | PRASEODYMIUM",
                    "RARE EARTHS | SAMARIUM",
                    "RARE EARTHS | SCANDIUM",
                    "RARE EARTHS | TERBIUM",
                    "RARE EARTHS | YTTRIUM",
                    "RARE EARTHS | YTTERBIUM"
                }, Level2_MouseDown);
            }
            
            else if (name == "METALS | MINERALS")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level2, new string[] {
                    "MINERALS | FLUORSPAR",
                    "MINERALS | GODANTI"
                }, Level2_MouseDown);
            }

            else if (name == "METALS | FERRO ALLOYS")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level2, new string[] {

                    "FERRO | CALCIUM SILICON",
                    "FERRO | BORON",
                    "FERRO | CHROME",
                    "FERRO | DYSPROSIUM",
                    "FERRO | GADOLINIUM",
                    "FERRO | HOLMIUM",
                    "FERRO | MANGANESE",
                    "FERRO | MOLYBDENUM",
                    "FERRO | NICKEL",
                    "FERRO | NIOBIUM",
                    "FERRO | PHOSPHORUS",
                    "FERRO | SILICON",
                    "FERRO | TITANIUM",
                    "FERRO | TUNGSTEN",
                    "FERRO | VANADIUM",
                    "FERRO | SILICO MANGANESE"
                }, Level2_MouseDown);
            }

            else if (name == "METALS | STEEL")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level2, new string[] {
                    "STEEL | ANGLE",
                    "STEEL | BAR",
                    "STEEL | BILLET",
                    "STEEL | CHANNEL",
                    "STEEL | CR COIL",
                    "STEEL | HR COIL",
                    "STEEL | H BEAM",
                    "STEEL | PIPE",
                    "STEEL | PLATE",
                    "STEEL | REBAR",
                    "STEEL | SCRAP",
                    "STEEL | WIRE ROD",
                    "STEEL | STAINLESS BAR",
                    "STEEL | STAINLESS CR",
                    "STEEL | STAINLESS HR",
                    "STEEL | STAINLESS SCRAP",
                    "STEEL | STAINLESS SEAMLESS",
                    "STEEL | CARBON STRUT",
                    "STEEL | CAST IRON"
                }, Level2_MouseDown);
            }

            else if (name == "SHIPPING | BARGE")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level2, new string[] {
                    "BARGE | BARGE",
                }, Level2_MouseDown);
            }
            else if (name == "SHIPPING | RAIL")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level2, new string[] {
                    "RAIL | RAIL",
                }, Level2_MouseDown);
            }
            else if (name == "SHIPPING | SEABORNE")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level2, new string[] {
                    "SEABORNE | CONTAINER",
                    "SEABORNE | DRY",
                    "SEABORNE | WET-DIRTY",
                    "SEABORNE | WET-CLEAN",
                    "SEABORNE | WET-LNG",
                }, Level2_MouseDown);
            }
            else if (name == "USD BASE")
            {
                _asset = "FX";
                setNavigationMenu(Level2, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level2_MouseDown);
            }
            else if (name == "EUR BASE")
            {
                _asset = "EURFX";
                setNavigationMenu(Level2, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level2_MouseDown);
            }
            else if (name == "GBP BASE")
            {
                _asset = "GBPFX";
                setNavigationMenu(Level2, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level2_MouseDown);
            }
            else if (name == "G 10")
            {
                _asset = "G10FX";
                setNavigationMenu(Level2, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level2_MouseDown);
            }

            else if (name == "WORLD EQ INDICES")
            {
                _asset = "WORLD EQ INDICES";
                setNavigationMenu(Level2, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level2_MouseDown);
                //setNavigationMenu(Level2, new string[] { "CONDITION OF TREND", " ", "CURRENT RESEARCH", " ", "ATM ALIGNMENT" }, Level2_MouseDown);
            }

            else if (name == "INTEREST RATES")
            {
                _asset = "INTEREST RATES";
                setNavigationMenu(Level2, new string[] { "30 YR", " ", "10 YR", " ", "7 YR", " ", "5 YR", " ", "3 YR", " ", "2 YR", " ", "1 YR", " ", "3 MO" }, Level2_MouseDown);
            }

            else if (name == "EQ FUNDAMENTALS")
            {
                _asset = "FUNDAMENTALS";
                setNavigationMenu(Level2, new string[] { "WORLD EQUITY INDICES" }, Level2_MouseDown);
            }

            else if (name == "COUNTRY STATISTICS")
            {
                setNavigationMenu(Level2, new string[] {
                    "COUNTRY RISK",
                    "",
                    "DEMOGRAPHIC",
                    "",
                    "ECONOMIC",
                    "",
                    "EXTERNAL SECTOR",
                    "",
                    "LABOR",
                    "",
                    "PUBLIC SECTOR"
                }, Level2_MouseDown);
            }

            else if (name == "PUBLIC SECTOR")
            {
                setNavigationMenu(Level2, new string[] {
                    "DEBT TO GDP",
                    "",
                    "BUDGET BALANCE"
                }, Level2_MouseDown);
            }

            else if (name == "LABOR")
            {
                setNavigationMenu(Level2, new string[] {
                    "UNEMPLOYMENT"
                }, Level2_MouseDown);
            }

            else if (name == "ECONOMIC")
            {
                setNavigationMenu(Level2, new string[] {
                    "CAPACITY UTILIZATION",
                    "",
                    "CONSUMER PRICE IDX",
                    "",
                    "CONSUMER CONF",
                    "",
                    "DOMESTIC PRODUCT",
                    "",
                    "INDUSTRIAL PRODUCTION",
                    "",
                    "PRODUCER PRICE IDX",
                    "",
                    "RETAIL SALES"
                }, Level2_MouseDown);
            }

            else if (name == "COUNTRY RISK")
            {
                setNavigationMenu(Level2, new string[] {
                    "POTENTIAL RECESSION",
                    "",
                    "ECONOMIC RISK",
                    "",
                    "FINANCIAL RISK",
                    "",
                    "POLITICAL RISK",
                    "",
                    "POLITICAL UNREST"
                }, Level2_MouseDown);
            }

            else if (name == "EXTERNAL SECTOR")
            {
                setNavigationMenu(Level2, new string[] {
                    "FX RESERVES",
                    "",
                    "GOLD RESERVES",
                    "",
                    "TREASURY RESERVES",
                    "",
                    "EXTERNAL DEBT"
                }, Level2_MouseDown);
            }

            else if (name == "DEMOGRAPHIC")
            {
                setNavigationMenu(Level2, new string[] {
                    "POPULATION",
                    "",
                    "SENIOR POPULATION",
                    "",
                    "YOUTH POPULATION",
                    "",
                    "NET MIGRATION"
                }, Level2_MouseDown);
            }

            else if (name == "CONDITION OF TREND") setNavigationMenu(Level4, new string[] { "ATM ANALYSIS", "", "TREND CONDITION", "", "TREND STRENGTH", "", "TREND HEAT" }, Level4_MouseDown);
            //else if (name == "CURRENT RESEARCH") setNavigationMenu(Level4, new string[] { "CURRENT RESEARCH" }, Level4_MouseDown);

        }

        private void Level2_MouseDown(object sender, MouseButtonEventArgs e)
        {
            TextBlock label = sender as TextBlock;
            string name = label.Text as string;

            L2.Text = name;
            L3.Text = "";
            L4.Text = "";
            L5.Text = "";

            showLevel3(name);
        }

        private void showLevel3(string name)
        {
            _condition = "";

            hideMap();
            Level3.Visibility = Visibility.Visible;
            Level4.Visibility = Visibility.Collapsed;
            Level5.Visibility = Visibility.Collapsed;

            highlightButton(Level2, name);

            if (name == "30 YR") { _asset = "30 YR YLD"; setNavigationMenu(Level3, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT", " ", "30 YR YIELD" }, Level3_MouseDown); }
            else if (name == "10 YR") { _asset = "10 YR YLD"; setNavigationMenu(Level3, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT", " ", "10 YR YIELD" }, Level3_MouseDown); }
            else if (name == "7 YR") { _asset = "7 YR YLD"; setNavigationMenu(Level3, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT", " ", "7 YR YIELD" }, Level3_MouseDown); }
            else if (name == "5 YR") { _asset = "5 YR YLD"; setNavigationMenu(Level3, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT", " ", "5 YR YIELD" }, Level3_MouseDown); }
            else if (name == "3 YR") { _asset = "3 YR YLD"; setNavigationMenu(Level3, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT", " ", "3 YR YIELD" }, Level3_MouseDown); }
            else if (name == "2 YR") { _asset = "2 YR YLD"; setNavigationMenu(Level3, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT", " ", "2 YR YIELD" }, Level3_MouseDown); }
            else if (name == "1 YR") { _asset = "1 YR YLD"; setNavigationMenu(Level3, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT", " ", "1 YR YIELD" }, Level3_MouseDown); }

            else if (name == "WORLD EQUITY INDICES") setNavigationMenu(Level3, new string[] { "BOOK", " ", "EARNINGS", " ", "EBITDA", " ", "RETURNS", " ", "SALES" }, Level3_MouseDown);
            else if (name == "DEMOGRAPHIC") setNavigationMenu(Level3, new string[] { "NET MIGRATION", " ", "POPULATION", " ", "SENIOR POPULATION", " ", "YOUTH POPULATION" }, Level3_MouseDown);
            else if (name == "COUNTRY RISK") setNavigationMenu(Level3, new string[] { "POTENTIAL RECESSION", " ", "ECONOMIC RISK", "", "FINANCIAL RISK", "", "POLITICAL RISK", "", "POLITICAL UNREST" }, Level3_MouseDown);
            else if (name == "ECONOMIC") setNavigationMenu(Level3, new string[] { "CAPACITY UTILIZATION", " ", "CONSUMER CONF", "", "CONSUMER PRICE IDX", "", "DOMESTIC PRODUCT", "", "INDUSTRIAL PRODUCTION", "", "PRODUCER PRICE IDX", "", "RETAIL SALES" }, Level3_MouseDown);
            else if (name == "LABOR") setNavigationMenu(Level3, new string[] { "UNEMPLOYMENT" }, Level3_MouseDown);
            else if (name == "EXTERNAL SECTOR") setNavigationMenu(Level3, new string[] { "EXTERNAL DEBT", "", "FX RESERVES", " ", "GOLD RESERVES", "", "TREASURY RESERVES" }, Level3_MouseDown);
            else if (name == "PUBLIC SECTOR") setNavigationMenu(Level3, new string[] { "DEBT TO GDP", " ", "BUDGET BALANCE" }, Level3_MouseDown);

            else if (name == "USD BASE")
            {
                _asset = "FX";
                setNavigationMenu(Level3, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level3_MouseDown);
            }
            else if (name == "EUR BASE")
            {
                _asset = "EURFX";
                setNavigationMenu(Level3, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level3_MouseDown);
            }
            else if (name == "GBP BASE")
            {
                _asset = "GBPFX";
                setNavigationMenu(Level3, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level3_MouseDown);
            }
            else if (name == "G 10")
            {
                _asset = "G10FX";
                setNavigationMenu(Level3, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level3_MouseDown);
            }

            else if (name == "WORLD EQ INDICES")
            {
                _asset = "WORLD EQ INDICES";
                setNavigationMenu(Level3, new string[] {"ATM ANALYSIS", " ", "CONDITION OF TREND", " ", "ATM ALIGNMENT"}, Level3_MouseDown);
            }

            else if (name == "FOREST PRODUCTS | LUMBER")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] {
                    "LUMBER FUTURES"
            }, Level3_MouseDown);
            }

            else if (name == "FOREST PRODUCTS |PAPER")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] {
                    "PAPER"
            }, Level3_MouseDown);
            }

            else if (name == "MEATS | CATTLE")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] {
                    "CATTLE FUTURES",
                    "HEIFER | DRESSED",
                    "HEIFER | LIVE",
                    "STEER | STEER",
                    "STEER | DRESSED",
                    "STEER | LIVE",
                    "FEEDER CATTLE FUTURES",
                    "FEEDER CATTLE SPOT"}, Level3_MouseDown);
            }
            else if (name == "MEATS | PORK")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "PORK" }, Level3_MouseDown);
            }  
            else if (name == "MEATS | POULTRY")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "BROILER", " ", "TURKEY" }, Level3_MouseDown);
            }  
            else if (name == "MEATS | SEAFOOD")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Fish" }, Level3_MouseDown);
            }
            else if (name == "MEATS | SHEEP")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Sheep" }, Level3_MouseDown);
            }            
            else if (name == "FERT | UREA")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "UREA" }, Level3_MouseDown);
            }            
            else if (name == "FERT | AMMOFOS")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "AMMOFOS" }, Level3_MouseDown);
            }
            else if (name == "FERT | AMMONIA")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "AMMONIA" }, Level3_MouseDown);
            }
            else if (name == "FERT | AMMO NITRATE")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "AMMO NITRATE" }, Level3_MouseDown);
            }          
            else if (name == "FERT | AMMO SULFATE")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "AMMO SULFATE" }, Level3_MouseDown);
            }           
            else if (name == "FERT | AQUA AMMONIA")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "AQUA AMMONIA" }, Level3_MouseDown);
            }          
            else if (name == "FERT | CALCIUM AMMO NITRATE")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "CALCIUM AMMO NITRATE" }, Level3_MouseDown);
            }            
            else if (name == "FERT | DIAMMOFOS")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "DIAMMOFOS" }, Level3_MouseDown);
            }  
            else if (name == "FERT | MONO AMMO PHOSPHATE")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "MONO AMMO PHOSPHATE" }, Level3_MouseDown);
            }  
            else if (name == "FERT | PHOSPHORIC ROCK")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "PHOSPHORIC ROCK" }, Level3_MouseDown);
            }             
            else if (name == "FERT | PHOSPORIC ACID")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "PHOSPORIC ACID" }, Level3_MouseDown);
            } 
            else if (name == "FERT | POTASH")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "POTASH" }, Level3_MouseDown);
            }             
            else if (name == "FERT | POTASSIUM CHLORIDE")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "POTASSIUM CHLORIDE" }, Level3_MouseDown);
            }             
            else if (name == "FERT | POTASSIUM NITRATE")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "POTASSIUM NITRATE" }, Level3_MouseDown);
            }             
            else if (name == "FERT | POTASSIUM SULFATE")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "POTASSIUM SULFATE" }, Level3_MouseDown);
            }              
            else if (name == "FERT | SODA ASH")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "SODA ASH" }, Level3_MouseDown);
            }              
            else if (name == "FERT | SODIUM MITRATE")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "SODIUM MITRATE" }, Level3_MouseDown);
            }               
            else if (name == "FERT | SOP MAGNESIA")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "SOP MAGNESIA" }, Level3_MouseDown);
            }                
            else if (name == "FERT | SULFUR")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "SULFUR" }, Level3_MouseDown);
            }                 
            else if (name == "FERT | SULFURIC ACID")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "SULFURIC ACID" }, Level3_MouseDown);
            }                  
            else if (name == "FOOD | DAIRY")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Dairy" }, Level3_MouseDown);
            }                  
            else if (name == "FOOD | EGGS")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Eggs" }, Level3_MouseDown);
            }                   
            else if (name == "FOOD | FOOD OIL")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Food Oil" }, Level3_MouseDown);
            }             
            else if (name == "FOREST | LUMBER")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "LUMBER" }, Level3_MouseDown);
            } 
            else if (name == "FOREST | PAPER")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { 
                    "CORRUGATED BOXES", 
                    "CORRUGATED CONTAINERS", 
                    "CORRUGATED WASTEPAPER", "FLUTING", "Kraftliner", "Magazine Paper", "Newsprint Paper", "Testliner Paper",
                }, Level3_MouseDown);
            } 
            else if (name == "FIBERS | COTTON")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Cotton" }, Level3_MouseDown);
            }
            else if (name == "GRAINS | CORN")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Corn" }, Level3_MouseDown);
            }
            else if (name == "GRAINS | SOYBEANS")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Soybeans" }, Level3_MouseDown);
            }
            else if (name == "GRAINS | WHEAT")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Wheat" }, Level3_MouseDown);
            }
            else if (name == "GRAINS | BARLEY")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Barley" }, Level3_MouseDown);
            }  
            else if (name == "GRAINS | OATS")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Oats" }, Level3_MouseDown);
            }            
            else if (name == "GRAINS | RICE")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Rice" }, Level3_MouseDown);
            }
            else if (name == "GRAINS | OILSEED")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Oilseed" }, Level3_MouseDown);
            }
            else if (name == "CRUDE | CRUDE OIL")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "CRUDE" }, Level3_MouseDown);
            }
            else if (name == "OLECHEM | FATTY ACID")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Fatty Acid" }, Level3_MouseDown);
            }            
            else if (name == "OLECHEM | FATTY ALCOHOL")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Fatty Alcohol" }, Level3_MouseDown);
            }            
            else if (name == "OLECHEM | GLYCERINE")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Glycerine" }, Level3_MouseDown);
            }            
            else if (name == "OLECHEM | VEGETABLE FAT")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Vegetable Fat" }, Level3_MouseDown);
            }            
            else if (name == "SOFTS | COCOA")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Cocoa" }, Level3_MouseDown);
            }            
            else if (name == "SOFTS | COFFEE")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Coffee" }, Level3_MouseDown);
            }            
            else if (name == "SOFTS | OJ")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "OJ" }, Level3_MouseDown);
            }            
            else if (name == "SOFTS | SUGAR")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Sugar" }, Level3_MouseDown);
            }           
            else if (name == "COKING COAL | COKE")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Coke" }, Level3_MouseDown);
            }
            else if (name == "NAT GAS | LNG")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Nat Gas" }, Level3_MouseDown);
            }            
            else if (name == "PETRO | 2-ETHYLHEXANOL")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "2-Ethylhexanol" }, Level3_MouseDown);
            }            
            else if (name == "PETRO | ACETIC ACID")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Acetic Acid" }, Level3_MouseDown);
            }           
            else if (name == "PETRO | ACETONE")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Acetone" }, Level3_MouseDown);
            }               
            else if (name == "PETRO | ACRYLIC ACID")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Acrylic Acid" }, Level3_MouseDown);
            }         
            else if (name == "PETRO | ADIPIC ACID")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Adipic Acid" }, Level3_MouseDown);
            }            
            else if (name == "PETRO | ALPHA OLEFIN")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Alpha Olefin" }, Level3_MouseDown);
            }            
            else if (name == "PETRO | AROMA LINEAR ALKYLBENZENE")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Aroma Linear Alkybenzene" }, Level3_MouseDown);
            }            
            else if (name == "PETRO | AROMA MIXED XYLENES")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Aroma Mixed Xylenes" }, Level3_MouseDown);
            }           
            else if (name == "PETRO | AROMA TOLUENE")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Aroma Toluene" }, Level3_MouseDown);
            }           
            else if (name == "PETRO | BASE OILS")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Base Oils" }, Level3_MouseDown);
            }            
            else if (name == "PETRO | CAPROLACTAM")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Caprolactam" }, Level3_MouseDown);
            }            
            else if (name == "PETRO | CARBON BLACK")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Carbon Black" }, Level3_MouseDown);
            }            
            else if (name == "PETRO | CYCLOHEXANONE")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Cyclohexanone" }, Level3_MouseDown);
            }             
            else if (name == "PETRO | EPOXIDE RESINS")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Epoxide Resins" }, Level3_MouseDown);
            }                 
            else if (name == "PETRO | ETHYLENE GLYCOL")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Ethylene Glycol" }, Level3_MouseDown);
            }                    
            else if (name == "PETRO | ETHYLENE OXIDE")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Ethylene Oxide" }, Level3_MouseDown);
            }           
            else if (name == "PETRO | EDC")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "EDC" }, Level3_MouseDown);
            }           
            else if (name == "PETRO | HDPE")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "HDPE" }, Level3_MouseDown);
            }            
            else if (name == "PETRO | ISOPROPYL ALCOHOL")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Isopropyl Alcohol" }, Level3_MouseDown);
            }             
            else if (name == "PETRO | LDPE")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "LDPE" }, Level3_MouseDown);
            }                   
            else if (name == "PETRO | MALEIC ANHYDRIDE")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Maleic Anhydride" }, Level3_MouseDown);
            }                   
            else if (name == "PETRO | MDI")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "MDI" }, Level3_MouseDown);
            }                   
            else if (name == "PETRO | MEG")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "MEG" }, Level3_MouseDown);
            }                   
            else if (name == "PETRO | MONOMER CAN")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Monomer Can" }, Level3_MouseDown);
            }                  
            else if (name == "PETRO | MTBE")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "MTBE" }, Level3_MouseDown);
            }                  
            else if (name == "PETRO | OCTANOL")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Octanol" }, Level3_MouseDown);
            }                  
            else if (name == "PETRO | OLEFINS BUTADIENE")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Olefins Butadiene" }, Level3_MouseDown);
            }                 
            else if (name == "PETRO | OLEFINS ETHYLENE")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Olefins Ethylene" }, Level3_MouseDown);
            }                 
            else if (name == "PETRO | OLEFINS PROPYLENE")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Olefins Propylene" }, Level3_MouseDown);
            }               
            else if (name == "PETRO | PET")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "PET" }, Level3_MouseDown);
            }               
            else if (name == "PETRO | PHENOL")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Phenol" }, Level3_MouseDown);
            }               
            else if (name == "PETRO | POLYAMIDE FIBER")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Polyamide Fiber" }, Level3_MouseDown);
            }               
            else if (name == "PETRO | POLYAMIDE RESIN")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Polyamide Resin" }, Level3_MouseDown);
            }              
            else if (name == "PETRO | POLYESTER RESIN")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Polyester Resin" }, Level3_MouseDown);
            }              
            else if (name == "PETRO | POLYMER ABS")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Polymer ABS" }, Level3_MouseDown);
            }             
            else if (name == "PETRO | POLYMER EVA")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Polymer EVA" }, Level3_MouseDown);
            }             
            else if (name == "PETRO | POLYMER LLDPE")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Polymer LLDPE" }, Level3_MouseDown);
            }             
            else if (name == "PETRO | POLYMER POLYSTYRENE")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Polymer Polystyrene" }, Level3_MouseDown);
            }             
            else if (name == "PETRO | POLYMER SBR")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Polymer SBR" }, Level3_MouseDown);
            }             
            else if (name == "PETRO | PP")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "PP" }, Level3_MouseDown);
            }             
            else if (name == "PETRO | PROPYLENE GLYCOL")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Propylene Glycol" }, Level3_MouseDown);
            }             
            else if (name == "PETRO | PROPYLENE OXIDE")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Propylene Oxide" }, Level3_MouseDown);
            }            
            else if (name == "PETRO | PTA")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "PTA" }, Level3_MouseDown);
            }            
            else if (name == "PETRO | PVC")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "PVC" }, Level3_MouseDown);
            }           
            else if (name == "PETRO | STYRENE")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Styrene" }, Level3_MouseDown);
            }           
            else if (name == "PETRO | TDI")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "TDI" }, Level3_MouseDown);
            }           
            else if (name == "PETRO | VCM")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "VCM" }, Level3_MouseDown);
            }           
            else if (name == "PETRO | VINYL ACETATE MONOMER")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Vinyl Acetate Monomer" }, Level3_MouseDown);
            }            
            else if (name == "REF HEAVY | BITUMEN")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Bitumen" }, Level3_MouseDown);
            }            
            else if (name == "REF HEAVY | FUEL OIL")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Fuel Oil" }, Level3_MouseDown);
            }            
            else if (name == "REF HEAVY | MARINE DIESEL")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Marine Diesel" }, Level3_MouseDown);
            }            
            else if (name == "REF HEAVY | MARINE GAS")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Marine Gas" }, Level3_MouseDown);
            }            
            else if (name == "REF LIGHT | CONDENSATE")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Condensate" }, Level3_MouseDown);
            }            
            else if (name == "REF LIGHT | GASOLINE GASOHOL")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Gasoline Gasohol" }, Level3_MouseDown);
            }            
            else if (name == "REF LIGHT | GASOLINE LEAD SUB")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Gasoline Lead Sub" }, Level3_MouseDown);
            }            
            else if (name == "REF LIGHT | GASOLINE REFORMATE")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Reformate" }, Level3_MouseDown);
            }            
            else if (name == "REF LIGHT | NAPHTHA")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Naphtha" }, Level3_MouseDown);
            }            
            else if (name == "REF MIDDLE | DIESEL")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Diesel" }, Level3_MouseDown);
            }            
            else if (name == "REF MIDDLE | GASOIL")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Gasoil" }, Level3_MouseDown);
            }            
            else if (name == "REF MIDDLE | JET FUEL")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Jet Fuel" }, Level3_MouseDown);
            }            
            else if (name == "EMISSIONS | EMISSIONS")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Emissions" }, Level3_MouseDown);
            }            
            else if (name == "BASE | ALUMINUM")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Aluminum" }, Level3_MouseDown);
            }            
            else if (name == "BASE | COPPER")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Copper" }, Level3_MouseDown);
            }            
            else if (name == "BASE | IRON")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Iron" }, Level3_MouseDown);
            }           
            else if (name == "BASE | LEAD")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Lead" }, Level3_MouseDown);
            }          
            else if (name == "BASE | NICKEL")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Nickel" }, Level3_MouseDown);
            }          
            else if (name == "BASE | TIN")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Tin" }, Level3_MouseDown);
            }          
            else if (name == "BASE | URANIUM")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Uranium" }, Level3_MouseDown);
            }          
            else if (name == "BASE | ZINC")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Zinc" }, Level3_MouseDown);
            }          
            else if (name == "BRASS | BRASS")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Brass" }, Level3_MouseDown);
            }          
            else if (name == "FERRO | CALCIUM SILICON")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Ferro Calicum Silicon" }, Level3_MouseDown);
            }          
            else if (name == "FERRO | BORON")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Ferro Boron" }, Level3_MouseDown);
            }          
            else if (name == "FERRO | CHROME")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Ferro Chrome" }, Level3_MouseDown);
            }          
            else if (name == "FERRO | DYSPROSIUM")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Ferro Dysprosium" }, Level3_MouseDown);
            }         
            else if (name == "FERRO | GADOLINIUM")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Ferro Gadolinium" }, Level3_MouseDown);
            }        
            else if (name == "FERRO | HOLMIUM")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Ferro Holmium" }, Level3_MouseDown);
            }       
            else if (name == "FERRO | MANGANESE")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Ferro Manganese" }, Level3_MouseDown);
            }      
            else if (name == "FERRO | MOLYBDENUM")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Ferro Molybdenum" }, Level3_MouseDown);
            }      
            else if (name == "FERRO | NICKEL")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Ferro Nickel" }, Level3_MouseDown);
            }      
            else if (name == "FERRO | NIOBIUM")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Ferro Niobium" }, Level3_MouseDown);
            }      
            else if (name == "FERRO | PHOSPHORUS")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Ferro Phosphorus" }, Level3_MouseDown);
            }      
            else if (name == "FERRO | SILICON")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Ferro Silicon" }, Level3_MouseDown);
            }      
            else if (name == "FERRO | TITANIUM")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Ferro Titanium" }, Level3_MouseDown);
            }     
            else if (name == "FERRO | TUNGSTEN")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Ferro Tungsten" }, Level3_MouseDown);
            }     
            else if (name == "FERRO | VANADIUM")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Ferro Vanadium" }, Level3_MouseDown);
            }    
            else if (name == "FERRO | SILICO MANGANESE")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Ferro Silico Manganese" }, Level3_MouseDown);
            }               
            else if (name == "MINERALS | FLUORSPAR")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Fluorspar" }, Level3_MouseDown);
            }           
            else if (name == "MINERALS | GODANTI")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Godanti" }, Level3_MouseDown);
            }             
            else if (name == "MINOR | ANTIMONY")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Antimony" }, Level3_MouseDown);
            }                 
            else if (name == "MINOR | ARSENIC")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Arsenic" }, Level3_MouseDown);
            }                
            else if (name == "MINOR | BERYLLIUM")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Beryllium" }, Level3_MouseDown);
            }              
            else if (name == "MINOR | BISMUTH")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Bismuth" }, Level3_MouseDown);
            }               
            else if (name == "MINOR | CADMIUM")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Cadmium" }, Level3_MouseDown);
            }                 
            else if (name == "MINOR | CALCIUM")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Calcium" }, Level3_MouseDown);
            }              
            else if (name == "MINOR | CARBON")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Carbon" }, Level3_MouseDown);
            }                  
            else if (name == "MINOR | CHROMIUM")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Chromium" }, Level3_MouseDown);
            }                     
            else if (name == "MINOR | COBALT")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Cobalt" }, Level3_MouseDown);
            }                   
            else if (name == "MINOR | GALLIUM")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Gallium" }, Level3_MouseDown);
            }                
            else if (name == "MINOR | GERMANIUM")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Germanium" }, Level3_MouseDown);
            }                
            else if (name == "MINOR | INDIUM")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Indium" }, Level3_MouseDown);
            }                
            else if (name == "MINOR | LITHIUM")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Lithium" }, Level3_MouseDown);
            }                
            else if (name == "MINOR | MAGNESIUM")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Magnesium" }, Level3_MouseDown);
            }                
            else if (name == "MINOR | MANGANESE")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Manganese" }, Level3_MouseDown);
            }                
            else if (name == "MINOR | MERCURY")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Mercury" }, Level3_MouseDown);
            }                
            else if (name == "MINOR | MOLYBDENUM")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Molybdenum" }, Level3_MouseDown);
            }                        
            else if (name == "MINOR | NIOBIUM")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Niobium" }, Level3_MouseDown);
            }                         
            else if (name == "MINOR | POTASSIUM")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Potassium" }, Level3_MouseDown);
            }              
            else if (name == "MINOR | SELENIUM")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Selenium" }, Level3_MouseDown);
            }                  
            else if (name == "MINOR | SILICON")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Silicon" }, Level3_MouseDown);
            }               
            else if (name == "MINOR | SODIUM")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Sodium" }, Level3_MouseDown);
            }                 
            else if (name == "MINOR | STRONTIUM")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Strontium" }, Level3_MouseDown);
            }                            
            else if (name == "MINOR | TANTALUM")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Tantalum" }, Level3_MouseDown);
            }               
            else if (name == "MINOR | TELLURIUM")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Tellurium" }, Level3_MouseDown);
            }           
            //else if (name == "MINOR | TITANIUM")
            //{
            //    _asset = "COMMODITIES";
            //    setNavigationMenu(Level3, new string[] { "Titanium" }, Level3_MouseDown);
            //}                  
            else if (name == "MINOR | TUNGSTEN")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Tungsten" }, Level3_MouseDown);
            }                
            else if (name == "MINOR | VANADIUM")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Vanadium" }, Level3_MouseDown);
            }                
            else if (name == "MINOR | ZIRCONIUM")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Zirconium" }, Level3_MouseDown);
            }   
            else if (name == "PRECIOUS | GOLD")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Gold" }, Level3_MouseDown);
            }           
            else if (name == "PRECIOUS | SILVER")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Silver" }, Level3_MouseDown);
            }           
            else if (name == "PRECIOUS | PALLADIUM")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Palladium" }, Level3_MouseDown);
            }           
            else if (name == "PRECIOUS | PLATINUM")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Platinum" }, Level3_MouseDown);
            }           
            else if (name == "PRECIOUS | IRIDIUM")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Iridium" }, Level3_MouseDown);
            }           
            else if (name == "PRECIOUS | RHENIUM")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Rhenium" }, Level3_MouseDown);
            }           
            else if (name == "PRECIOUS | RHODIUM")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Rhodium" }, Level3_MouseDown);
            }           
            else if (name == "PRECIOUS | RUTHENIUM")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Ruthenium" }, Level3_MouseDown);
            }         
            else if (name == "RARE EARTHS | DYSPROSIUM")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Dysprosium" }, Level3_MouseDown);
            }         
            else if (name == "RARE EARTHS | ERBIUM")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Erbium" }, Level3_MouseDown);
            }         
            else if (name == "RARE EARTHS | EUROPIUM")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Europium" }, Level3_MouseDown);
            }         
            else if (name == "RARE EARTHS | GADOLINIUM")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Gadolinium" }, Level3_MouseDown);
            }         
            else if (name == "RARE EARTHS | HOLMIUM")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Holmium" }, Level3_MouseDown);
            }         
            else if (name == "RARE EARTHS | LANTHANUM")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Lanthanum" }, Level3_MouseDown);
            }         
            else if (name == "RARE EARTHS | LUTETIUM")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Lutetium" }, Level3_MouseDown);
            }         
            else if (name == "RARE EARTHS | MISCHMETAL")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Mischmetal" }, Level3_MouseDown);
            }         
            else if (name == "RARE EARTHS | NEODYMIUM")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Neodymium" }, Level3_MouseDown);
            }         
            else if (name == "RARE EARTHS | PRASEODYMIUM")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Praseodymium" }, Level3_MouseDown);
            }         
            else if (name == "RARE EARTHS | SAMARIUM")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Samarium" }, Level3_MouseDown);
            }         
            else if (name == "RARE EARTHS | SCANDIUM")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Scandium" }, Level3_MouseDown);
            }         
            else if (name == "RARE EARTHS | TERBIUM")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Terbium" }, Level3_MouseDown);
            }         
            else if (name == "RARE EARTHS | YTTRIUM")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Yttrium" }, Level3_MouseDown);
            }         
            else if (name == "RARE EARTHS | YTTERBIUM")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Ytterbium" }, Level3_MouseDown);
            }
            else if (name == "STEEL | ANGLE")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Angle" }, Level3_MouseDown);
            }
            else if (name == "STEEL | BAR")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Bar" }, Level3_MouseDown);
            }
            else if (name == "STEEL | BILLET")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "BILLET SPOT" }, Level3_MouseDown);
            }
            else if (name == "STEEL | CHANNEL")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Channel" }, Level3_MouseDown);
            }
            else if (name == "STEEL | CR COIL")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "CR COIL SPOT" }, Level3_MouseDown);
            }
            else if (name == "STEEL | HR COIL")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "HR COIL SPOT" }, Level3_MouseDown);
            }            
            else if (name == "STEEL | H BEAM")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "H Beam" }, Level3_MouseDown);
            }          
            else if (name == "STEEL | PIPE")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Pipe" }, Level3_MouseDown);
            }           
            else if (name == "STEEL | PLATE")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Plate" }, Level3_MouseDown);
            }
            else if (name == "STEEL | REBAR")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "REBAR SPOT" }, Level3_MouseDown);
            }       
            else if (name == "STEEL | SCRAP")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Scrap" }, Level3_MouseDown);
            }
            else if (name == "STEEL | WIRE ROD")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Wire Rod" }, Level3_MouseDown);
            }
            else if (name == "STEEL | STAINLESS BAR")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "SS Bar 304" }, Level3_MouseDown);
            }         
            else if (name == "STEEL | STAINLESS CR")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "CR SS Coil" }, Level3_MouseDown);
            }            
            else if (name == "STEEL | STAINLESS HR")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "HR SS Plate" }, Level3_MouseDown);
            }                            
            else if (name == "STEEL | STAINLESS SCRAP")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "SS Scrap" }, Level3_MouseDown);
            }                       
            else if (name == "STEEL | STAINLESS SEAMLESS")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "SS Seamless" }, Level3_MouseDown);
            }                      
            else if (name == "STEEL | CARBON STRUT")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Carbon Strut" }, Level3_MouseDown);
            }                      
            else if (name == "STEEL | CAST IRON")
            {
                _asset = "COMMODITIES";
                setNavigationMenu(Level3, new string[] { "Cast Iron" }, Level3_MouseDown);
            }
            else if (name == "INTEREST RATES")
            {
                _asset = "INTEREST RATES";
                setNavigationMenu(Level3, new string[] { "30 YR", " ", "10 YR", " ", "7 YR", " ", "5 YR", " ", "3 YR", " ", "2 YR", " ", "1 YR", " ", "3 MO" }, Level3_MouseDown);
            }
            else if (name == "SOVR CDS")
            {
                _asset = "SOVR CDS";
                setNavigationMenu(Level3, new string[] { "CONDITION OF TREND"," ", "ATM ALIGNMENT" }, Level3_MouseDown);
            }
        }
        private void Level3_MouseDown(object sender, MouseButtonEventArgs e)
        {
            TextBlock label = sender as TextBlock;
            string name = label.Text as string;

            L3.Text = name;
            L4.Text = "";
            L5.Text = "";

            if (name == "CURRENT RESEARCH")
            {
                _isFundamental = false;
                _condition = "CURRENT RESEARCH";

                resetAllCountryColors();
                showLegend();
                ChartIntervals.Visibility = _isFundamental ? Visibility.Collapsed : Visibility.Visible;
                AgoSliderPanel.Visibility = _isFundamental ? Visibility.Collapsed : Visibility.Visible;
                MapTitle.Visibility = _isFundamental ? Visibility.Collapsed : Visibility.Visible;
                update();
                startMapColoring(8);

                showMap();
            }
            else
            {
                showLevel4(name);
            }
        }

        private void showLevel4(string name)
        { 
            hideMap();
            Level4.Visibility = Visibility.Visible;
            Level5.Visibility = Visibility.Collapsed;

            highlightButton(Level3, name);

            if (name == "30 YR") { _asset = "30 YR YLD"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND"," ", "ATM ALIGNMENT", " ", "30 YR YIELD" }, Level4_MouseDown); }
            else if (name == "10 YR") { _asset = "10 YR YLD"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND"," ", "ATM ALIGNMENT", " ", "10 YR YIELD" }, Level4_MouseDown); }
            else if (name == "7 YR") { _asset = "7 YR YLD"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT", " ", "7 YR YIELD" }, Level4_MouseDown); }
            else if (name == "5 YR") { _asset = "5 YR YLD"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT", " ", "5 YR YIELD" }, Level4_MouseDown); }
            else if (name == "3 YR") { _asset = "3 YR YLD"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT", " ", "3 YR YIELD" }, Level4_MouseDown); }
            else if (name == "2 YR") { _asset = "2 YR YLD"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT", " ", "2 YR YIELD" }, Level4_MouseDown); }
            else if (name == "1 YR") { _asset = "1 YR YLD"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT", " ", "1 YR YIELD" }, Level4_MouseDown); }
            
            else if (name == "BILLET SPOT") { _asset = "BILLET SPOT"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "CR COIL SPOT") { _asset = "CR COIL SPOT"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "HR COIL SPOT") { _asset = "HR COIL SPOT"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND"," ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "REBAR SPOT") { _asset = "REBAR SPOT"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND"," ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "STAINLESS BAR SPOT") { _asset = "STAINLESS BAR SPOT"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
  
            else if (name == "CATTLE FUTURES") { _asset = "CATTLE FUTURES"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "FEEDER CATTLE FUTURES") { _asset = "FEEDER CATTLE FUTURES"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "FEEDER CATTLE SPOT") { _asset = "FEEDER CATTLE SPOT"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "HEIFER | DRESSED") { _asset = "HEIFER | DRESSED"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "HEIFER | LIVE") { _asset = "HEIFER | LIVE"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "STEER | DRESSED") { _asset = "STEER | DRESSED"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "STEER | LIVE") { _asset = "STEER | LIVE"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "STEER | STEER") { _asset = "STEER | STEER"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "PORK") { _asset = "PORK"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Fish") { _asset = "Fish"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Sheep") { _asset = "Sheep"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }

            else if (name == "BROILER") { _asset = "BROILER"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "TURKEY") { _asset = "TURKEY"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }

            else if (name == "Corn") { _asset = "Corn"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Soybeans") { _asset = "Soybeans"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Wheat") { _asset = "Wheat"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Cotton") { _asset = "Cotton"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }

            else if (name == "LUMBER") { _asset = "LUMBER"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "PAPER") { _asset = "PAPER"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            
            else if (name == "UREA") { _asset = "UREA"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "AMMOFOS") { _asset = "AMMOFOS"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "AMMONIA") { _asset = "AMMONIA"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "AMMO NITRATE") { _asset = "AMMO NITRATE"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "AMMO SULFATE") { _asset = "AMMO SULFATE"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "AQUA AMMONIA") { _asset = "AQUA AMMONIA"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "CALCIUM AMMO NITRATE") { _asset = "CALCIUM AMMO NITRATE"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "DIAMMOFOS") { _asset = "DIAMMOFOS"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "MONO AMMO PHOSPHATE") { _asset = "MONO AMMO PHOSPHATE"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "PHOSPHORIC ROCK") { _asset = "PHOSPHORIC ROCK"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "PHOSPORIC ACID") { _asset = "PHOSPORIC ACID"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "POTASH") { _asset = "POTASH"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "POTASSIUM CHLORIDE") { _asset = "POTASSIUM CHLORIDE"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "POTASSIUM NITRATE") { _asset = "POTASSIUM NITRATE"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "POTASSIUM SULFATE") { _asset = "POTASSIUM SULFATE"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "SODA ASH") { _asset = "SODA ASH"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "SODIUM MITRATE") { _asset = "SODIUM MITRATE"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "SOP MAGNESIA") { _asset = "SOP MAGNESIA"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "SULFUR") { _asset = "SULFUR"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "SULFURIC ACID") { _asset = "SULFURIC ACID"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
                                                     
            else if (name == "CRUDE") { _asset = "CRUDE"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
           
            else if (name == "2-Ethylhexanol") { _asset = "2-Ethylhexanol"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Acetic Acid") { _asset = "Acetic Acid"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Acetone") { _asset = "Acetone"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Acrylic Acid") { _asset = "Acrylic Acid"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Adipic Acid") { _asset = "Adipic Acid"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Alpha Olefin") { _asset = "Alpha Olefin"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Aluminum") { _asset = "Aluminum"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Angle") { _asset = "Angle"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Antimony") { _asset = "Antimony"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Aqua Ammonia") { _asset = "Aqua Ammonia"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Aroma Linear Alkylbenzene") { _asset = "Aroma Linear Alkylbenzene"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Aroma Mixed Xylenes") { _asset = "Aroma Mixed Xylenes"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Aroma Toluene") { _asset = "Aroma Toluene"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Arsenic") { _asset = "Arsenic"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Bar") { _asset = "Bar"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Barley") { _asset = "Barley"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Base Oils") { _asset = "Base Oils"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Beryllium") { _asset = "Beryllium"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Bismuth") { _asset = "Bismuth"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Bitumen") { _asset = "Bitumen"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Brass") { _asset = "Brass"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Cadmium") { _asset = "Cadmium"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Calcium Silicon") { _asset = "Calcium Silicon"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Calcium") { _asset = "Calcium"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Caprolactam") { _asset = "Caprolactam"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Carbon Black") { _asset = "Carbon Black"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Carbon Strut") { _asset = "Carbon Strut"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Carbon") { _asset = "Carbon"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Cast Iron") { _asset = "Cast Iron"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Channel") { _asset = "Channel"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Chromium") { _asset = "Chromium"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Cobalt") { _asset = "Cobalt"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Cocoa") { _asset = "Cocoa"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Coke") { _asset = "Coke"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Coffee") { _asset = "Coffee"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Condensate") { _asset = "Condensate"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Copper") { _asset = "Copper"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "CORRUGATED BOXES") { _asset = "CORRUGATED BOXES"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "CORRUGATED CONTAINERS") { _asset = "CORRUGATED CONTAINERS"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "CORRUGATED WASTEPAPER") { _asset = "CORRUGATED WASTEPAPER"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "FLUTING") { _asset = "FLUTING"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Kraftliner") { _asset = "Kraftliner"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Magazine Paper") { _asset = "Magazine Paper"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Newsprint Paper") { _asset = "Newsprint Paper"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Testliner Paper") { _asset = "Testliner Paper"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Cyclohexanone") { _asset = "Cyclohexanone"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Dairy") { _asset = "Dairy"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Diesel") { _asset = "Diesel"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Dysprosium") { _asset = "Dysprosium"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "EDC") { _asset = "EDC"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Eggs") { _asset = "Eggs"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Emissions") { _asset = "Emissions"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Epoxide Resins") { _asset = "Epoxide Resins"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Erbium") { _asset = "Erbium"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Ethylene Glycol") { _asset = "Ethylene Glycol"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Ethylene Oxide") { _asset = "Ethylene Oxide"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Europium") { _asset = "Europium"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Fatty Acid") { _asset = "Fatty Acid"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Fatty Alcohol") { _asset = "Fatty Alcohol"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Ferro Calcium Silicon") { _asset = "Ferro Calcium Silicon"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Ferro Boron") { _asset = "Ferro Boron"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Ferro Chrome") { _asset = "Ferro Chrome"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Ferro Dysprosium") { _asset = "Ferro Dysprosium"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Ferro Gadolinium") { _asset = "Ferro Gadolinium"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Ferro Holmium") { _asset = "Ferro Holmium"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Ferro Manganese") { _asset = "Ferro Manganese"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Ferro Molybdenum") { _asset = "Ferro Molybdenum"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Ferro Nickel") { _asset = "Ferro Nickel"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Ferro Niobium") { _asset = "Ferro Niobium"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Ferro Phosphorus") { _asset = "Ferro Phosphorus"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Ferro Silicon") { _asset = "Ferro Silicon"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Ferro Titanium") { _asset = "Ferro Titanium"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Ferro Tungsten") { _asset = "Ferro Tungsten"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Ferro Vanadium") { _asset = "Ferro Vanadium"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Ferro Silico Manganese") { _asset = "Ferro Silico Manganese"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Fluorspar") { _asset = "Fluorspar"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Food Oil") { _asset = "Food Oil"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Fuel Oil") { _asset = "Fuel Oil"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Gadolinium") { _asset = "Gadolinium"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Gallium") { _asset = "Gallium"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Gasoil") { _asset = "Gasoil"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Gasoline Gasohol") { _asset = "Gasoline Gasohol"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Gasoline Lead Sub") { _asset = "Gasoline Lead Sub"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Gasoline Reformate") { _asset = "Gasoline Reformate"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Gasoline") { _asset = "Gasoline"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Germanium") { _asset = "Germanium"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Glycerine") { _asset = "Glycerine"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Godanti") { _asset = "Godanti"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Gold") { _asset = "Gold"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "H Beam") { _asset = "H Beam"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "HDPE") { _asset = "HDPE"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Holmium") { _asset = "Holmium"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "I Beam") { _asset = "I Beam"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Indium") { _asset = "Indium"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Iridium") { _asset = "Iridium"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Iron") { _asset = "Iron"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Isopropyl Alcohol") { _asset = "Isopropyl Alcohol"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Jet Fuel") { _asset = "Jet Fuel"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Lanthanum") { _asset = "Lanthanum"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "LDPE") { _asset = "LDPE"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Lead") { _asset = "Lead"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Lithium") { _asset = "Lithium"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Nat Gas") { _asset = "Nat Gas"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Lutetium") { _asset = "Lutetium"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Magnesium") { _asset = "Magnesium"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Maize") { _asset = "Maize"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Maleic Anhydride") { _asset = "Maleic Anhydride"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Manganese") { _asset = "Manganese"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Marine Diesel") { _asset = "Marine Diesel"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Marine Gasoil") { _asset = "Marine Gasoil"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "MDI") { _asset = "MDI"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "MEG") { _asset = "MEG"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Mercury") { _asset = "Mercury"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Mischmetal") { _asset = "Mischmetal"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Molybdenum") { _asset = "Molybdenum"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "MTBE") { _asset = "MTBE"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Naphtha") { _asset = "Naphtha"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Neodymium") { _asset = "Neodymium"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Niobium") { _asset = "Niobium"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Nickel") { _asset = "Nickel"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Oats") { _asset = "Oats"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Octanol") { _asset = "Octanol"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Olefins Butadiene") { _asset = "Olefins Butadiene"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Olefins Ethylene") { _asset = "Olefins Ethylene"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Olefins Propylene") { _asset = "Olefins Propylene"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "OJ") { _asset = "OJ"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Palladium") { _asset = "Palladium"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Paper") { _asset = "Paper"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "PET") { _asset = "PET"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Phenol") { _asset = "Phenol"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Pipe") { _asset = "Pipe"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Plate") { _asset = "Plate"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Platinum") { _asset = "Platinum"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Polyamide Fiber") { _asset = "Polyamide Fiber"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Polyamide Resin") { _asset = "Polyamide Resin"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Polyester Resin") { _asset = "Polyester Resin"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Polymer Abs") { _asset = "Polymer Abs"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Polymer Eva") { _asset = "Polymer Eva"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Polymer Lldpe") { _asset = "Polymer Lldpe"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Polymer Polystyrene") { _asset = "Polymer Polystyrene"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Potassium") { _asset = "Potassium"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "PP") { _asset = "PP"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Praseodymium") { _asset = "Praseodymium"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Propylene Glycol") { _asset = "Propylene Glycol"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Propylene Oxide") { _asset = "Propylene Oxide"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "PTA") { _asset = "PTA"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "PVC") { _asset = "PVC"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Rhenium") { _asset = "Rhenium"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Rhodium") { _asset = "Rhodium"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Rice") { _asset = "Rice"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Ruthenium") { _asset = "Ruthenium"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Samarium") { _asset = "Samarium"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Scandium") { _asset = "Scandium"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Scrap") { _asset = "Scrap"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Selenium") { _asset = "Selenium"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Silico Manganese") { _asset = "Silico Manganese"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Silicon") { _asset = "Silicon"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Silver") { _asset = "Silver"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Sodium") { _asset = "Sodium"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Soybeans") { _asset = "Soybeans"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "SS Bar 304") { _asset = "SS Bar 304"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "CR SS Coil") { _asset = "CR SS Coil"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "HR SS Plate") { _asset = "HR SS Plate"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "SS Scrap") { _asset = "SS Scrap"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "SS Seamless") { _asset = "SS Seamless"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Strontium") { _asset = "Strontium"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Styrene") { _asset = "Styrene"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Sugar") { _asset = "Sugar"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Tantalum") { _asset = "Tantalum"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "TDI") { _asset = "Tdi"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Tellurium") { _asset = "Tellurium"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Terbium") { _asset = "Terbium"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Tin") { _asset = "Tin"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Titanium") { _asset = "Titanium"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Tungsten") { _asset = "Tungsten"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Turpentine") { _asset = "Turpentine"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Uranium") { _asset = "Uranium"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Vanadium") { _asset = "Vanadium"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Varnish") { _asset = "Varnish"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "VCM") { _asset = "VCM"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Vegetable Fat") { _asset = "Vegetable Fat"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Vinyl Acetate Monomer") { _asset = "Vinyl Acetate Monomer"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Wheat") { _asset = "Wheat"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Wire Rod") { _asset = "Wire Rod"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Ytterbium") { _asset = "Ytterbium"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Yttrium") { _asset = "Yttrium"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Zinc") { _asset = "Zinc"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }
            else if (name == "Zirconium") { _asset = "Zirconium"; setNavigationMenu(Level4, new string[] { "CONDITION OF TREND", " ", "ATM ALIGNMENT" }, Level4_MouseDown); }

            else if (name == "CONDITION OF TREND") setNavigationMenu(Level4, new string[] { "ATM ANALYSIS", "", "TREND CONDITION", "", "TREND STRENGTH", "", "TREND HEAT" }, Level4_MouseDown);
            else if (name == "CURRENT RESEARCH") setNavigationMenu(Level4, new string[] { "CURRENT RESEARCH" }, Level4_MouseDown);
            //else if (name == "ATM ALIGNMENT") setNavigationMenu(Level4, new string[] 
            //    {
            //        //"CURRENT RESEARCH",
            //        //"PR",
            //        //"SC",
            //        //"FT | P",
            //        "FT | FT",
            //        "FT | SC",
            //        //"FT | SC PR",
            //        "FT | ST",
            //        //"FT | ST PR",
            //        "FT | TSB",
            //        //"FT | TSB PR",
            //        "SC | FT",
            //        "SC | SC",
            //        //"SC | SC PR",
            //        "SC | ST",
            //        //"SC | ST PR",
            //        "SC | TSB",
            //        //"SC | TSB PR",
            //        "ST | FT",
            //        "ST | SC",
            //        //"ST | SC PR",
            //        "ST | ST",
            //        //"ST | ST PR",
            //        "ST | TSB",
            //        //"ST | TSB PR",
            //        "TSB | FT",
            //        "TSB | SC",
            //        //"TSB | SC PR",
            //        "TSB | ST",
            //        //"TSB | ST PR",
            //        "TSB | TSB",
            //        //"TSB | TSB PR",
            //    }, Level4_MouseDown);

            else if (name == "3 MO Yield")
            {
                _asset = "3 MO YLD";
                _isFundamental = false;
                showMap();
            }

            else if (name == "1 YR YIELD")
            {
                _asset = "1 YR YLD";
                _isFundamental = false;
                showMap();
            }

            else if (name == "2 YR YIELD")
            {
                _asset = "2 YR YLD";
                _isFundamental = false;
                showMap();
            }

            else if (name == "3 YR YIELD")
            {
                _asset = "3 YR YLD";
                _isFundamental = false;
                showMap();
            }

            else if (name == "5 YR YIELD")
            {
                _asset = "5 YR YLD";
                _isFundamental = false;
                showMap();
            }

            else if (name == "7 YR YIELD")
            {
                _asset = "7 YR YLD";
                _isFundamental = false;
                showMap();
            }

            else if (name == "10 YR YIELD")
            {
                _asset = "10 YR YLD";
                _isFundamental = false;
                showMap();
            }

            else if (name == "30 YR YIELD")
            {
                _asset = "30 YR YLD";
                _isFundamental = false;
                showMap();
            }

            else if (name == "LUMBER FUTURES")
            {
                _asset = "LUMBER FUTURES";
                _isFundamental = false;
                showMap();
            }

            else if (name == "BILLET SPOT")
            {
                _asset = "BILLET SPOT";
                _isFundamental = false;
                showMap();
            }
            else if (name == "CR COIL SPOT")
            {
                _asset = "CR COIL SPOT";
                _isFundamental = false;
                showMap();
            }

            else if (name == "HR COIL SPOT")
            {
                _asset = "HR COIL SPOT";
                _isFundamental = false;
                showMap();
            }
            else if (name == "REBAR SPOT")
            {
                _asset = "REBAR SPOT";
                _isFundamental = false;
                showMap();
            }
            else if (name == "STAINLESS BAR SPOT")
            {
                _asset = "STAINLESS BAR SPOT";
                _isFundamental = false;
                showMap();
            }

            else if (name == "PE") setNavigationMenu(Level4, new string[] {
                    "INDX_GENERAL_PE_RATIO",
                    "",
                    "INDX_GENERAL_EST_PE",
                    "",
                    "INDX_ADJ_POSITIVE_PE"
            }, Level4_MouseDown);
            else if (name == "BOOK") setNavigationMenu(Level4, new string[] {
                    "INDX_PX_BOOK",
                    "",
                    "IDX_EST_PRICE_BOOK",
                    "",
                    "EST_PX_BOOK_NEXT_YR_AGGTE",
                    "",
                    "EST_PX_BOOK_FY3_AGGTE"
            }, Level4_MouseDown);
            else if (name == "SALES") setNavigationMenu(Level4, new string[] {
                    "INDX_PX_SALES",
                    "",
                    "IDX_EST_PRICE_SALES",
                    "",
                    "EST_PX_SALES_NEXT_YR_AGGTE",
                    "",
                    "EST_PX_SALES_FY3_AGGTE"
            }, Level4_MouseDown);
            else if (name == "EARNINGS") setNavigationMenu(Level4, new string[] {
                    "INDX_GENERAL_PE_RATIO",
                    "",
                    "INDX_GENERAL_EST_PE",
                    "",
                    "INDX_ADJ_POSITIVE_PE",
                    "",
                    "INDX_POS_ERN",
                    "",
                    "INDX_POS_EST_ERN",
                    "",
                    "INDX_GENERAL_EARN",
                    "",
                    "INDX_GENERAL_EST_EARN",
                    "",
                    "INDX_WEIGHTED_EST_ERN",
                    "",
                    "IDX_EST_EARNINGS_NXT_YR",
                    "",
                    "INDX_ADJ_POS_PX_EE",
                    "",
                    "IDX_EST_GEN_EARN_NXT_YR",
                    "",
                    "IDX_EST_EBITDA_CURR_YR",
                    "",
                    "IDX_EST_EBITDA_NXT_YR"
            }, Level4_MouseDown);
            else if (name == "PCT CHG") setNavigationMenu(Level4, new string[] {
                    "CHG_PCT_1D",
                    "",
                    "CHG_PCT_5D",
                    "",
                    "CHG_PCT_MTD",
                    "",
                    "CHG_PCT_3M",
                    "",
                    "CHG_PCT_QTD",
                    "",
                    "CHG_PCT_6M",
                    "",
                    "CHG_PCT_YTD",
                    "",
                    "CHG_PCT_1YR",
                    "",
                    "CHG_PCT_2YR",
                    "",
                    "CHG_PCT_3YR",
                    "",
                    "CHG_PCT_4YR",
                    "",
                    "CHG_PCT_5YR"
            }, Level4_MouseDown);
            else if (name == "Cash Flow") setNavigationMenu(Level4, new string[] {
                    "PX_TO_CASH_FLOW",
                    "",
                    "IDX_EST_CASH_FLOW_CURR_YR",
                    "",
                    "IDX_EST_CASH_FLOW_NXT_YR",
                    "",
                    "IDX_EST_PRICE_CF"
            }, Level4_MouseDown);
            else if (name == "DIVIDENDS") setNavigationMenu(Level4, new string[] {
                    "IDX_EST_DVD_CURR_YR",
                    "",
                    "IDX_EST_DVD_NXT_YR",
                    "",
                    "IDX_EST_DVD_YLD"
            }, Level4_MouseDown);
            else if (name == "ENTERPRISE VALUE") setNavigationMenu(Level4, new string[] {
                    "IDX_ENTERPRISE_VALUE"
            }, Level4_MouseDown);
            else if (name == "EBITDA") setNavigationMenu(Level4, new string[] {
                    "PX_TO_EBITDA",
                    "",
                    "PX_TO_EST_EBITDA",
                    "",
                    "IDX_EST_EBITDA_CURR_YR",
                    "",
                    "IDX_EST_EBITDA_NXT_YR",
                    "",
                    "IDX_EST_EV_EBITDA"
            }, Level4_MouseDown);
            else if (name == "RETURNS") setNavigationMenu(Level4, new string[] {
                    "RETURN_COM_EQY",
                    "",
                    "RETURN_ON_CAP",
                    "",
                    "RETURN_ON_ASSET"
            }, Level4_MouseDown);
            else if (name == "CURR ACCT BALANCE")
            {
                _asset = "CURRENT ACCT BAL (%GDP)";
                _isFundamental = false;
                showMap();
            }
            else if (name == "DOMESTIC PRODUCT")
            {
                _asset = "GROSS DOMESTIC PRODUCT";
                _isFundamental = false;
                showMap();
            }
            else if (name == "UNEMPLOYMENT")
            {
                _asset = "UNEMPLOYMENT RATE";
                _isFundamental = false;
                showMap();
            }
            else if (name == "YOUTH UNEMPLOYMENT")
            {
                _asset = "UNEMPLOYMENT RATE AGES 15 to 24";
                _isFundamental = false;
                showMap();
            }
            else if (name == "PRODUCER PRICE IDX")
            {
                _asset = "PRODUCER PRICE INDEX";
                _isFundamental = false;
                showMap();
            }
            else if (name == "RETAIL SALES")
            {
                _asset = "RETAIL SALES";
                _isFundamental = false;
                showMap();
            }
            else if (name == "CAPACITY UTILIZATION")
            {
                _asset = "CAPACITY UTILIZATION";
                _isFundamental = false;
                showMap();
            }
            else if (name == "INDUSTRIAL PRODUCTION")
            {
                _asset = "INDUSTRIAL PRODUCTION YOY";
                _isFundamental = false;
                showMap();
            }
            else if (name == "CONSUMER PRICE IDX")
            {
                _asset = "CONSUMER PRICE INDEX";
                _isFundamental = false;
                showMap();
            }
            else if (name == "CONSUMER CONF")
            {
                _asset = "CONSUMER CONFIDENCE";
                _isFundamental = false;
                showMap();
            }
            else if (name == "POTENTIAL RECESSION")
            {
                _asset = "RECESSION PROBABILITY";
                _isFundamental = false;
                showMap();
            }
            else if (name == "ECONOMIC RISK")
            {
                _asset = "ECONOMIC RISK";
                _isFundamental = false;
                showMap();
            }
            else if (name == "FINANCIAL RISK")
            {
                _asset = "FINANCIAL RISK";
                _isFundamental = false;
                showMap();
            }
            else if (name == "POLITICAL RISK")
            {
                _asset = "POLITICAL RISK";
                _isFundamental = false;
                showMap();
            }
            else if (name == "POPULATION")
            {
                _asset = "WORLD POPULATION";
                _isFundamental = false;
                showMap();
            }
            else if (name == "SENIOR POPULATION")
            {
                _asset = "WORLD POPULATION OVER 65";
                _isFundamental = false;
                showMap();
            }
            else if (name == "YOUTH POPULATION")
            {
                _asset = "WORLD POPULATION UNDER 15";
                _isFundamental = false;
                showMap();
            }
            else if (name == "NET MIGRATION")
            {
                _asset = "NET MIGRATION";
                _isFundamental = false;
                showMap();
            }
            else if (name == "POLITICAL UNREST")
            {
                _asset = "GEOPOLITICAL VOLATILITY";
                _isFundamental = false;
                showMap();
            }
            else if (name == "GOLD RESERVES")
            {
                _asset = "GOLD HOLDINGS";
                _isFundamental = false;
                showMap();
            }
            else if (name == "TREASURY RESERVES")
            {
                _asset = "US TREAS HOLDERS";
                _isFundamental = false;
                showMap();
            }
            else if (name == "FX RESERVES")
            {
                _asset = "RESERVE FX HOLDINGS";
                _isFundamental = false;
                showMap();
            }
            else if (name == "EXTERNAL DEBT")
            {
                _asset = "TOTAL GROSS EXTERNAL DEBT";
                _isFundamental = false;
                showMap();
            }
            else if (name == "DEBT TO GDP")
            {
                _isFundamental = false;
                _asset = "GROSS DEBT % of GDP";
                _isFundamental = false;
                showMap();
            }
            else if (name == "BUDGET BALANCE")
            {
                _isFundamental = false;
                _asset = "BUDGET BALANCE (%GDP)";
                _isFundamental = false;
                showMap();
            }
        }
        private bool isModelName(string name)
        {
            var modelNames = MainView.getModelNames();
            return modelNames.Contains(name);
        }

        private string _modelName = "";

        private void Level4_MouseDown(object sender, MouseButtonEventArgs e)
        {
            TextBlock label = sender as TextBlock;
            string name = label.Text as string;

            L4.Text = name;
            L5.Text = "";

            highlightButton(Level4, name);

            _condition = "";

            if (name == "TREND CONDITION")
            {
                _isFundamental = false;
                _condition = "TREND CONDITION";
            }
			else if (name == "ATM ANALYSIS")
			{
				_isFundamental = false;
				_condition = "ATM ANALYSIS";
			}
			else if (name == "TREND STRENGTH")
            {
                _isFundamental = false;
                _condition = "TREND STRENGTH";
            }
            else if (name == "TREND HEAT")
            {
                _isFundamental = false;
                _condition = "TREND HEAT";
            }
            else if (name == "CURRENT RESEARCH")
            {
                _isFundamental = false;
                _condition = "CURRENT RESEARCH";

                resetAllCountryColors();
                showLegend();
                ChartIntervals.Visibility = _isFundamental ? Visibility.Collapsed : Visibility.Visible;
                AgoSliderPanel.Visibility = _isFundamental ? Visibility.Collapsed : Visibility.Visible;
                MapTitle.Visibility = _isFundamental ? Visibility.Collapsed : Visibility.Visible;
                update();
                startMapColoring(8);

                showMap();
            }
            else if (name == "PR")
            {
                _isFundamental = false;
                _condition = "PR";
            }
            else if (name == "SC")
            {
                _isFundamental = false;
                _condition = "SC";
            }
            else if (name == "FT | FT")
            {
                _isFundamental = false;
                _condition = "FT | FT";
            }
            else if (name == "FT || FT")
            {
                _isFundamental = false;
                _condition = "FT || FT";
            }
            else if (name == "FT | SC")
            {
                _isFundamental = false;
                _condition = "FT | SC";
            }
            else if (name == "FT | SC PR")
            {
                _isFundamental = false;
                _condition = "FT | SC PR";
            }
            else if (name == "FT | ST")
            {
                _isFundamental = false;
                _condition = "FT | ST";
            }
            else if (name == "FT | ST PR")
            {
                _isFundamental = false;
                _condition = "FT | ST PR";
            }
            else if (name == "FT | TSB")
            {
                _isFundamental = false;
                _condition = "FT | TSB";
            }
            else if (name == "FT | TSB PR")
            {
                _isFundamental = false;
                _condition = "FT | TSB PR";
            }
            else if (name == "SC | FT")
            {
                _isFundamental = false;
                _condition = "SC | FT";
            }
            else if (name == "SC | SC")
            {
                _isFundamental = false;
                _condition = "SC | SC";
            }
            else if (name == "SC | SC PR")
            {
                _isFundamental = false;
                _condition = "SC | SC PR";
            }
            else if (name == "SC | ST")
            {
                _isFundamental = false;
                _condition = "SC | ST";
            }
            else if (name == "SC | ST PR")
            {
                _isFundamental = false;
                _condition = "SC | ST PR";
            }
            else if (name == "SC | TSB")
            {
                _isFundamental = false;
                _condition = "SC | TSB";
            }
            else if (name == "SC | TSB PR")
            {
                _isFundamental = false;
                _condition = "SC | TSB PR";
            }
            else if (name == "ST | FT")
            {
                _isFundamental = false;
                _condition = "ST | FT";
            }
            else if (name == "ST | SC")
            {
                _isFundamental = false;
                _condition = "ST | SC";
            }
            else if (name == "ST | SC PR")
            {
                _isFundamental = false;
                _condition = "ST | SC PR";
            }
            else if (name == "ST | SC PR")
            {
                _isFundamental = false;
                _condition = "ST | SC PR";
            }
            else if (name == "ST | ST")
            {
                _isFundamental = false;
                _condition = "ST | ST";
            }
            else if (name == "ST | ST PR")
            {
                _isFundamental = false;
                _condition = "ST | ST PR";
            }
            else if (name == "ST | TSB")
            {
                _isFundamental = false;
                _condition = "ST | TSB";
            }
            else if (name == "ST | TSB PR")
            {
                _isFundamental = false;
                _condition = "ST | TSB PR";
            }
            else if (name == "TSB | FT")
            {
                _isFundamental = false;
                _condition = "TSB | FT";
            }
            else if (name == "TSB | SC")
            {
                _isFundamental = false;
                _condition = "TSB | SC";
            }
            else if (name == "TSB | SC PR")
            {
                _isFundamental = false;
                _condition = "TSB | SC PR";
            }
            else if (name == "TSB | ST")
            {
                _isFundamental = false;
                _condition = "TSB | ST";
            }
            else if (name == "TSB | ST PR")
            {
                _isFundamental = false;
                _condition = "TSB | ST PR";
            }
            else if (name == "TSB | TSB")
            {
                _isFundamental = false;
                _condition = "TSB | TSB";
            }
            else if (name == "TSB | TSB PR")
            {
                _isFundamental = false;
                _condition = "TSB | TSB PR";
            }
            else if (isModelName(name))
            {
                _isFundamental = false;
                _condition = "ATM Pxf";
                _modelName = name;
            }
            else if (name == "3 MO Yield")
            {
                _asset = "3 MO YLD";
                _isFundamental = false;
                showMap();
            }
            else if (name == "1 YR YIELD")
            {
                _asset = "1 YR YLD";
                _isFundamental = false;
                showMap();
            }
            else if (name == "2 YR YIELD")
            {
                _asset = "2 YR YLD";
                _isFundamental = false;
                showMap();
            }
            else if (name == "3 YR YIELD")
            {
                _asset = "3 YR YLD";
                _isFundamental = false;
                showMap();
            }
            else if (name == "5 YR YIELD")
            {
                _asset = "5 YR YLD";
                _isFundamental = false;
                showMap();
            }
            else if (name == "7 YR YIELD")
            {
                _asset = "7 YR YLD";
                _isFundamental = false;
                showMap();
            }
            else if (name == "10 YR YIELD")
            {
                _asset = "10 YR YLD";
                _isFundamental = false;
                showMap();
            }
            else if (name == "30 YR YIELD")
            {
                _asset = "30 YR YLD";
                _isFundamental = false;
                showMap();
            }
            else
            {
                _isFundamental = true;
                _selectedFundamental = name;
            }

            if (L4.Text == "CONDITION OF TREND")
            {
                showLevel5(name);
            }
            else if (L4.Text == "ATM ALIGNMENT")
            {
                showLevel5(name);
            }
            //else if (L4.Text == "CURRENT RESEARCH")
            //{
            //    showLevel5(name);
            //}
            else
            {
                resetAllCountryColors();
                showLegend();
                ChartIntervals.Visibility = _isFundamental ? Visibility.Visible : Visibility.Visible;
                AgoSliderPanel.Visibility = _isFundamental ? Visibility.Visible : Visibility.Visible;
                MapTitle.Visibility = _isFundamental ? Visibility.Visible : Visibility.Visible;
                update();
                startMapColoring(8);

                showMap();
            }
        }

        private void showLevel5(string name)
        {
            hideMap();
            Level5.Visibility = Visibility.Visible;

            highlightButton(Level4, name);

            if (name == "CONDITION OF TREND") setNavigationMenu(Level5, new string[] { "ATM ANALYSIS", "", "TREND CONDITION", "", "TREND STRENGTH", "", "TREND HEAT" }, Level5_MouseDown);
            //else if (name == "CURRENT RESEARCH") setNavigationMenu(Level5, new string[] { "CURRENT RESEARCH" }, Level5_MouseDown);
            //else if (name == "ATM ALIGNMENT") setNavigationMenu(Level5, new string[] 
            //    {
            //        "CURRENT RESEARCH",            
            //        "PR",
            //        //"SC",
            //        //"FT | P",
            //        "FT | FT",
            //        "FT | SC",
            //        //"FT | SC PR",
            //        "FT | ST",
            //        //"FT | ST PR",
            //        "FT | TSB",
            //        //"FT | TSB PR",
            //        "SC | FT",
            //        "SC | SC",
            //        //"SC | SC PR",
            //        "SC | ST",
            //        //"SC | ST PR",
            //        "SC | TSB",
            //        //"SC | TSB PR",
            //        "ST | FT",
            //        "ST | SC",
            //        //"ST | SC PR",
            //        "ST | ST",
            //        //"ST | ST PR",
            //        "ST | TSB",
            //        //"ST | TSB PR",
            //        "TSB | FT",
            //        "TSB | SC",
            //        //"TSB | SC PR",
            //        "TSB | ST",
            //        //"TSB | ST PR",
            //        "TSB | TSB",
            //        //"TSB | TSB PR",
            //    }, Level5_MouseDown);
        }

        private void Level5_MouseDown(object sender, MouseButtonEventArgs e)
        {
            TextBlock label = sender as TextBlock;
            string name = label.Text as string;

            L5.Text = name;

            highlightButton(Level5, name);

            _condition = "";

            if (name == "TREND CONDITION")
            {
                _isFundamental = false;
                _condition = "TREND CONDITION";
            }
			else if (name == "ATM ANALYSIS")
			{
				_isFundamental = false;
				_condition = "ATM ANALYSIS";
			}
			else if (name == "TREND STRENGTH")
            {
                _isFundamental = false;
                _condition = "TREND STRENGTH";
            }
            else if (name == "TREND HEAT")
            {
                _isFundamental = false;
                _condition = "TREND HEAT";
            }
            else if (name == "CURRENT RESEARCH")
            {
                _isFundamental = false;
                _condition = "CURRENT RESEARCH";
            }
            else if (name == "PR")
            {
                _isFundamental = false;
                _condition = "PR";
            }
            else if (name == "SC")
            {
                _isFundamental = false;
                _condition = "SC";
            }
            else if (name == "FT | FT")
            {
                _isFundamental = false;
                _condition = "FT | FT";
            }
            else if (name == "FT || FT")
            {
                _isFundamental = false;
                _condition = "FT || FT";
            }
            else if (name == "FT | SC")
            {
                _isFundamental = false;
                _condition = "FT | SC";
            }
            else if (name == "FT | SC PR")
            {
                _isFundamental = false;
                _condition = "FT | SC PR";
            }
            else if (name == "FT | ST")
            {
                _isFundamental = false;
                _condition = "FT | ST";
            }
            else if (name == "FT | ST PR")
            {
                _isFundamental = false;
                _condition = "FT | ST PR";
            }
            else if (name == "FT | TSB")
            {
                _isFundamental = false;
                _condition = "FT | TSB";
            }
            else if (name == "FT | TSB PR")
            {
                _isFundamental = false;
                _condition = "FT | TSB PR";
            }
            else if (name == "SC | FT")
            {
                _isFundamental = false;
                _condition = "SC | FT";
            }
            else if (name == "SC | SC")
            {
                _isFundamental = false;
                _condition = "SC | SC";
            }
            else if (name == "SC | SC PR")
            {
                _isFundamental = false;
                _condition = "SC | SC PR";
            }
            else if (name == "SC | ST")
            {
                _isFundamental = false;
                _condition = "SC | ST";
            }
            else if (name == "SC | ST PR")
            {
                _isFundamental = false;
                _condition = "SC | ST PR";
            }
            else if (name == "SC | TSB")
            {
                _isFundamental = false;
                _condition = "SC | TSB";
            }
            else if (name == "SC | TSB PR")
            {
                _isFundamental = false;
                _condition = "SC | TSB PR";
            }
            else if (name == "ST | FT")
            {
                _isFundamental = false;
                _condition = "ST | FT";
            }
            else if (name == "ST | SC")
            {
                _isFundamental = false;
                _condition = "ST | SC";
            }
            else if (name == "ST | SC PR")
            {
                _isFundamental = false;
                _condition = "ST | SC PR";
            }
            else if (name == "ST | ST")
            {
                _isFundamental = false;
                _condition = "ST | ST";
            }
            else if (name == "ST | ST PR")
            {
                _isFundamental = false;
                _condition = "ST | ST PR";
            }
            else if (name == "ST | TSB")
            {
                _isFundamental = false;
                _condition = "ST | TSB";
            }
            else if (name == "ST | TSB PR")
            {
                _isFundamental = false;
                _condition = "ST | TSB PR";
            }
            else if (name == "TSB | FT")
            {
                _isFundamental = false;
                _condition = "TSB | FT";
            }
            else if (name == "TSB | SC")
            {
                _isFundamental = false;
                _condition = "TSB | SC";
            }
            else if (name == "TSB | SC PR")
            {
                _isFundamental = false;
                _condition = "TSB | SC PR";
            }
            else if (name == "TSB | ST")
            {
                _isFundamental = false;
                _condition = "TSB | ST";
            }
            else if (name == "TSB | ST PR")
            {
                _isFundamental = false;
                _condition = "TSB | ST PR";
            }
            else if (name == "TSB | TSB")
            {
                _isFundamental = false;
                _condition = "TSB | TSB";
            }
            else if (name == "TSB | TSB PR")
            {
                _isFundamental = false;
                _condition = "TSB | TSB PR";
            }
            else if (name == "ST")
            {
                _isFundamental = false;
                _condition = "ST";
            }
            else if (name == "NET")
            {
                _isFundamental = false;
                _condition = "NET";
            }
            else if (isModelName(name))
            {
                _isFundamental = false;
                _condition = "ATM Pxf";
                _modelName = name;
            }
            else
            {
                _isFundamental = true;
                _selectedFundamental = name;
            }

            resetAllCountryColors();
            showLegend();
            ChartIntervals.Visibility = _isFundamental ? Visibility.Collapsed : Visibility.Visible;
            AgoSliderPanel.Visibility = _isFundamental ? Visibility.Collapsed : Visibility.Visible;
            MapTitle.Visibility = _isFundamental ? Visibility.Collapsed : Visibility.Visible;
            update();
            startMapColoring(8);

            showMap();
        }

        class LegendRow
        {
            public Color Color { get; set; }
            public string Label { get; set; }
        }

        private void drawLegend(string title, List<LegendRow> rows)
        {
            L0Title.Text = L1.Text as string;
            L2Title.Text = L2.Text as string;

            LegendTitle.Text = title;
            var rowCount = rows.Count;
            for (int ii = 0; ii < 12; ii++)
            {
                var rectangleName = "R" + (ii + 2).ToString();
                var textBlockName = "T" + (ii + 2).ToString();
                var rectangle = FindName(rectangleName) as Rectangle;
                var textBlock = FindName(textBlockName) as TextBlock;
                var brush = (ii < rowCount) ? new SolidColorBrush(rows[ii].Color) : Brushes.Transparent;
                var text = (ii < rowCount) ? rows[ii].Label : "";
                rectangle.Fill = brush;
                textBlock.Text = text;
            }
            Legends.Visibility = Visibility.Visible;
        }

        private void hideMap()
        {
            Grid grid1 = FindName(getMapName()) as Grid;
            if (grid1 != null)
            {
                grid1.Visibility = Visibility.Collapsed;
            }
            WorldMap.Visibility = Visibility.Collapsed;
            RegionMap.Visibility = Visibility.Collapsed;
        }

        private void showMap()
        {
            Level1.Visibility = Visibility.Collapsed;
            Level2.Visibility = Visibility.Collapsed;
            Level3.Visibility = Visibility.Collapsed;
            Level4.Visibility = Visibility.Collapsed;
            MapTitle.Visibility = Visibility.Collapsed;

            setMapVisibility(_region);

            resetAllCountryColors();
            showLegend();
            update();
        }

        private void Menu_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _ago = 0;
            AgoSlider.Value = _maxAgo - _ago;

            Label label = e.Source as Label;
            string selection = label.Content as string;
            if (selection == "TREND CONDITION")
            {
                _isFundamental = false;
                resetAllCountryColors();
                _condition = selection;
                showLegend();
                ChartIntervals.Visibility = Visibility.Visible;
                AgoSliderPanel.Visibility = Visibility.Visible;
                MapTitle.Visibility = Visibility.Visible;
                update();
                startMapColoring(4);
            }
            else if (selection == "TREND HEAT")
            {
                _isFundamental = false;
                resetAllCountryColors();
                _condition = selection;
                showLegend();
                ChartIntervals.Visibility = Visibility.Visible;
                AgoSliderPanel.Visibility = Visibility.Visible;
                MapTitle.Visibility = Visibility.Visible;
                update();
                startMapColoring(5);
            }
			else if (selection == "ATM ANALYSIS")
			{
				_isFundamental = false;
				resetAllCountryColors();
				_condition = selection;
				showLegend();
				ChartIntervals.Visibility = Visibility.Visible;
				AgoSliderPanel.Visibility = Visibility.Visible;
				MapTitle.Visibility = Visibility.Visible;
				update();
				startMapColoring(6);
			}
			else if (selection == "TREND STRENGTH")
            {
                _isFundamental = false;
                resetAllCountryColors();
                _condition = selection;
                showLegend();
                ChartIntervals.Visibility = Visibility.Visible;
                AgoSliderPanel.Visibility = Visibility.Visible;
                MapTitle.Visibility = Visibility.Visible;
                update();
                startMapColoring(6);
            }
            else if (selection == "CURRENT RESEARCH")
            {
                resetAllCountryColors();
                _condition = selection;
                showLegend();
                ChartIntervals.Visibility = Visibility.Visible;
                AgoSliderPanel.Visibility = Visibility.Visible;
                MapTitle.Visibility = Visibility.Visible;
                update();
                startMapColoring(7);
            }
            else if (selection == "SC")
            {
                resetAllCountryColors();
                _condition = selection;
                showLegend();
                ChartIntervals.Visibility = Visibility.Visible;
                AgoSliderPanel.Visibility = Visibility.Visible;
                MapTitle.Visibility = Visibility.Visible;
                update();
                startMapColoring(8);
            }
            else if (selection == "FT | FT")
            {
                resetAllCountryColors();
                _condition = selection;
                showLegend();
                ChartIntervals.Visibility = Visibility.Visible;
                AgoSliderPanel.Visibility = Visibility.Visible;
                MapTitle.Visibility = Visibility.Visible;
                update();
                startMapColoring(9);
            }
            else if (selection == "FT || FT")
            {
                resetAllCountryColors();
                _condition = selection;
                showLegend();
                ChartIntervals.Visibility = Visibility.Visible;
                AgoSliderPanel.Visibility = Visibility.Visible;
                MapTitle.Visibility = Visibility.Visible;
                update();
                startMapColoring(9);
            }

            else if (selection == "FT | SC")
            {
                resetAllCountryColors();
                _condition = selection;
                showLegend();
                ChartIntervals.Visibility = Visibility.Visible;
                AgoSliderPanel.Visibility = Visibility.Visible;
                MapTitle.Visibility = Visibility.Visible;
                update();
                startMapColoring(10);
            }
            else if (selection == "FT | SC PR")
            {
                resetAllCountryColors();
                _condition = selection;
                showLegend();
                ChartIntervals.Visibility = Visibility.Visible;
                AgoSliderPanel.Visibility = Visibility.Visible;
                MapTitle.Visibility = Visibility.Visible;
                update();
                startMapColoring(11);
            }
            else if (selection == "FT | ST")
            {
                resetAllCountryColors();
                _condition = selection;
                showLegend();
                ChartIntervals.Visibility = Visibility.Visible;
                AgoSliderPanel.Visibility = Visibility.Visible;
                MapTitle.Visibility = Visibility.Visible;
                update();
                startMapColoring(12);
            }
            else if (selection == "FT | ST PR")
            {
                resetAllCountryColors();
                _condition = selection;
                showLegend();
                ChartIntervals.Visibility = Visibility.Visible;
                AgoSliderPanel.Visibility = Visibility.Visible;
                MapTitle.Visibility = Visibility.Visible;
                update();
                startMapColoring(13);
            }
            else if (selection == "FT | TSB")
            {
                resetAllCountryColors();
                _condition = selection;
                showLegend();
                ChartIntervals.Visibility = Visibility.Visible;
                AgoSliderPanel.Visibility = Visibility.Visible;
                MapTitle.Visibility = Visibility.Visible;
                update();
                startMapColoring(14);
            }
            else if (selection == "FT | TSB PR")
            {
                resetAllCountryColors();
                _condition = selection;
                showLegend();
                ChartIntervals.Visibility = Visibility.Visible;
                AgoSliderPanel.Visibility = Visibility.Visible;
                MapTitle.Visibility = Visibility.Visible;
                update();
                startMapColoring(15);
            }
            else if (selection == "SC | FT")
            {
                resetAllCountryColors();
                _condition = selection;
                showLegend();
                ChartIntervals.Visibility = Visibility.Visible;
                AgoSliderPanel.Visibility = Visibility.Visible;
                MapTitle.Visibility = Visibility.Visible;
                update();
                startMapColoring(16);
            }
            else if (selection == "SC | SC")
            {
                resetAllCountryColors();
                _condition = selection;
                showLegend();
                ChartIntervals.Visibility = Visibility.Visible;
                AgoSliderPanel.Visibility = Visibility.Visible;
                MapTitle.Visibility = Visibility.Visible;
                update();
                startMapColoring(17);
            }
            else if (selection == "SC | SC PR")
            {
                resetAllCountryColors();
                _condition = selection;
                showLegend();
                ChartIntervals.Visibility = Visibility.Visible;
                AgoSliderPanel.Visibility = Visibility.Visible;
                MapTitle.Visibility = Visibility.Visible;
                update();
                startMapColoring(18);
            }
            else if (selection == "SC | ST")
            {
                resetAllCountryColors();
                _condition = selection;
                showLegend();
                ChartIntervals.Visibility = Visibility.Visible;
                AgoSliderPanel.Visibility = Visibility.Visible;
                MapTitle.Visibility = Visibility.Visible;
                update();
                startMapColoring(19);
            }
            else if (selection == "SC | ST PR")
            {
                resetAllCountryColors();
                _condition = selection;
                showLegend();
                ChartIntervals.Visibility = Visibility.Visible;
                AgoSliderPanel.Visibility = Visibility.Visible;
                MapTitle.Visibility = Visibility.Visible;
                update();
                startMapColoring(20);
            }
            else if (selection == "SC | TSB")
            {
                resetAllCountryColors();
                _condition = selection;
                showLegend();
                ChartIntervals.Visibility = Visibility.Visible;
                AgoSliderPanel.Visibility = Visibility.Visible;
                MapTitle.Visibility = Visibility.Visible;
                update();
                startMapColoring(21);
            }
            else if (selection == "SC | TSB PR")
            {
                resetAllCountryColors();
                _condition = selection;
                showLegend();
                ChartIntervals.Visibility = Visibility.Visible;
                AgoSliderPanel.Visibility = Visibility.Visible;
                MapTitle.Visibility = Visibility.Visible;
                update();
                startMapColoring(22);
            }
            else if (selection == "ST | FT")
            {
                resetAllCountryColors();
                _condition = selection;
                showLegend();
                ChartIntervals.Visibility = Visibility.Visible;
                AgoSliderPanel.Visibility = Visibility.Visible;
                MapTitle.Visibility = Visibility.Visible;
                update();
                startMapColoring(23);
            }
            else if (selection == "ST | SC")
            {
                resetAllCountryColors();
                _condition = selection;
                showLegend();
                ChartIntervals.Visibility = Visibility.Visible;
                AgoSliderPanel.Visibility = Visibility.Visible;
                MapTitle.Visibility = Visibility.Visible;
                update();
                startMapColoring(24);
            }
            else if (selection == "ST | SC PR")
            {
                resetAllCountryColors();
                _condition = selection;
                showLegend();
                ChartIntervals.Visibility = Visibility.Visible;
                AgoSliderPanel.Visibility = Visibility.Visible;
                MapTitle.Visibility = Visibility.Visible;
                update();
                startMapColoring(25);
            }
            else if (selection == "ST | ST")
            {
                resetAllCountryColors();
                _condition = selection;
                showLegend();
                ChartIntervals.Visibility = Visibility.Visible;
                AgoSliderPanel.Visibility = Visibility.Visible;
                MapTitle.Visibility = Visibility.Visible;
                update();
                startMapColoring(26);
            }
            else if (selection == "ST | ST PR")
            {
                resetAllCountryColors();
                _condition = selection;
                showLegend();
                ChartIntervals.Visibility = Visibility.Visible;
                AgoSliderPanel.Visibility = Visibility.Visible;
                MapTitle.Visibility = Visibility.Visible;
                update();
                startMapColoring(27);
            }
            else if (selection == "ST | TSB")
            {
                resetAllCountryColors();
                _condition = selection;
                showLegend();
                ChartIntervals.Visibility = Visibility.Visible;
                AgoSliderPanel.Visibility = Visibility.Visible;
                MapTitle.Visibility = Visibility.Visible;
                update();
                startMapColoring(28);
            }
            else if (selection == "ST | TSB PR")
            {
                resetAllCountryColors();
                _condition = selection;
                showLegend();
                ChartIntervals.Visibility = Visibility.Visible;
                AgoSliderPanel.Visibility = Visibility.Visible;
                MapTitle.Visibility = Visibility.Visible;
                update();
                startMapColoring(29);
            }
            else if (selection == "TSB | FT")
            {
                resetAllCountryColors();
                _condition = selection;
                showLegend();
                ChartIntervals.Visibility = Visibility.Visible;
                AgoSliderPanel.Visibility = Visibility.Visible;
                MapTitle.Visibility = Visibility.Visible;
                update();
                startMapColoring(30);
            }
            else if (selection == "TSB | SC")
            {
                resetAllCountryColors();
                _condition = selection;
                showLegend();
                ChartIntervals.Visibility = Visibility.Visible;
                AgoSliderPanel.Visibility = Visibility.Visible;
                MapTitle.Visibility = Visibility.Visible;
                update();
                startMapColoring(31);
            }
            else if (selection == "TSB | SC PR")
            {
                resetAllCountryColors();
                _condition = selection;
                showLegend();
                ChartIntervals.Visibility = Visibility.Visible;
                AgoSliderPanel.Visibility = Visibility.Visible;
                MapTitle.Visibility = Visibility.Visible;
                update();
                startMapColoring(32);
            }
            else if (selection == "TSB | ST")
            {
                resetAllCountryColors();
                _condition = selection;
                showLegend();
                ChartIntervals.Visibility = Visibility.Visible;
                AgoSliderPanel.Visibility = Visibility.Visible;
                MapTitle.Visibility = Visibility.Visible;
                update();
                startMapColoring(33);
            }
            else if (selection == "TSB | ST PR")
            {
                resetAllCountryColors();
                _condition = selection;
                showLegend();
                ChartIntervals.Visibility = Visibility.Visible;
                AgoSliderPanel.Visibility = Visibility.Visible;
                MapTitle.Visibility = Visibility.Visible;
                update();
                startMapColoring(34);
            }
            else if (selection == "TSB | TSB")
            {
                resetAllCountryColors();
                _condition = selection;
                showLegend();
                ChartIntervals.Visibility = Visibility.Visible;
                AgoSliderPanel.Visibility = Visibility.Visible;
                MapTitle.Visibility = Visibility.Visible;
                update();
                startMapColoring(35);
            }
            else if (selection == "TSB | TSB PR")
            {
                resetAllCountryColors();
                _condition = selection;
                showLegend();
                ChartIntervals.Visibility = Visibility.Visible;
                AgoSliderPanel.Visibility = Visibility.Visible;
                MapTitle.Visibility = Visibility.Visible;
                update();
                startMapColoring(36);
            }
            else if (isModelName(selection))
            {
                _modelName = selection;
                _isFundamental = false;
                resetAllCountryColors();
                _condition = selection;
                showLegend();
                ChartIntervals.Visibility = Visibility.Visible;
                AgoSliderPanel.Visibility = Visibility.Visible;
                MapTitle.Visibility = Visibility.Visible;
                update();
                startMapColoring(37);
            }


#if POSITIONS
            else if (selection == "Positions")
            {
                _ago = 0;
                resetAllCountryColors();
                _condition = selection;
                showLegend();
                ChartIntervals.Visibility = Visibility.Hidden;
                AgoSliderPanel.Visibility = Visibility.Collapsed;
                MapTitle.Visibility = Visibility.Visible;
                update();
                startMapColoring();
            }
#endif
            else if (selection == "Quarterly" || selection == "Monthly" || selection == "Weekly" || selection == "Daily" || selection == "240 Min" || selection == "120 Min" || selection == "60 Min" || selection == "30 Min" || selection == "15 Min")
            {
                resetCharts();
                resetAllCountryColors();
                _interval = selection;
                update();
                changeChart(_chartSymbol, _interval.Replace(" Min", ""), false);
            }
            else if (selection == "GLOBAL" || selection == "NorthAmerica" || selection == "SouthAmerica" || selection == "EUROPE" || selection == "MEA" || selection == "ASIA" || selection == "OCEANIA")
            {
                setMapVisibility(selection);
                MapTitle.Visibility = Visibility.Collapsed;

                _viewType = ViewType.Map;

                update();
                if (_viewType == ViewType.Symbol)
                {
                    requestSymbols();
                }
            }

            else if (selection == "WORLD EQ INDICES" || selection == "USD BASE" || selection == "EUR BASE" || selection == "GBP BASE" || selection == "G 10" || selection == "SOVR CDS" || selection == "INTEREST RATES")
            {
                resetAllCountryColors();
                _asset = selection;
                MapTitle.Visibility = Visibility.Collapsed;

                _viewType = ViewType.Map;

                update();
                if (_viewType == ViewType.Symbol)
                {
                    requestSymbols();
                }
            }
            else
            {
                _isFundamental = true;
                resetAllCountryColors();
                _selectedFundamental = selection;
                showLegend();
                ChartIntervals.Visibility = Visibility.Collapsed;
                AgoSliderPanel.Visibility = Visibility.Collapsed;
                MapTitle.Visibility = Visibility.Collapsed;
                update();
                startMapColoring(11);
            }

            hideNavigation();
        }

        private void setMapVisibility(string region)
        {
            hideNavigation();

            Grid grid1 = FindName(getMapName()) as Grid;
            if (grid1 != null)
            {
                grid1.Visibility = Visibility.Collapsed;
            }

            _region = region;

            Grid grid2 = FindName(getMapName()) as Grid;
            if (grid2 != null)
            {
                grid2.Visibility = Visibility.Visible;
            }

            WorldMap2.Visibility = (_region == "GLOBAL") ? Visibility.Collapsed : Visibility.Visible;
            WorldMap.Visibility = (_region == "GLOBAL") ? Visibility.Visible : Visibility.Collapsed;
            RegionMap.Visibility = (_region == "GLOBAL") ? Visibility.Collapsed : Visibility.Visible;
        }

        private string getMapName()
        {
            string name = "";
            if (_region == "NorthAmerica") name = "NorthAmericaMap";
            else if (_region == "SouthAmerica") name = "SouthAmericaMap";
            else name = _region + "Map";
            return name;
        }

        //private void SelectModel_MouseDown(object sender, MouseButtonEventArgs e)
        //{
        //    if (RegionMenu.Visibility == Visibility.Collapsed)
        //    {
        //        hideNavigation();

        //        Menus.Visibility = Visibility.Visible;
        //        RegionMenu.Visibility = Visibility.Visible;

        //        AODScrollViewer.Visibility = Visibility.Collapsed;
        //        SourceNavigation.Visibility = Visibility.Collapsed;
        //        Navigation.Visibility = Visibility.Collapsed;
        //        ChartGrid.Visibility = Visibility.Collapsed;
        //        ChartIntervals.Visibility = Visibility.Collapsed;
        //        AgoSliderPanel.Visibility = Visibility.Collapsed;
        //        MapTitle.Visibility = Visibility.Collapsed;
        //        MLScenario.Visibility = Visibility.Collapsed;

        //        var modelNames = MainView.getModelNames();

        //        nav.setNavigationLevel1(RegionMenu, Model_MouseDown, modelNames.ToArray());
        //        highlightButton(RegionMenu, _modelName);
        //    }
        //    else
        //    {
        //        hideNavigation();

        //        Menus.Visibility = Visibility.Collapsed;
        //        RegionMenu.Visibility = Visibility.Collapsed;
        //        ChartIntervals.Visibility = Visibility.Collapsed;

        //        AODScrollViewer.Visibility = Visibility.Visible;
        //    }
        //}
       
        private void ATMTimingMenu_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (ATMStrategyScrollViewer.Visibility == Visibility.Collapsed)
            {
                hideNavigation();

                Menus.Visibility = Visibility.Visible;
                RegionMenu.Visibility = Visibility.Collapsed;
                ATMStrategyScrollViewer.Visibility = Visibility.Visible;

                AODScrollViewer.Visibility = Visibility.Collapsed;
                SourceNavigation.Visibility = Visibility.Collapsed;
                Navigation.Visibility = Visibility.Collapsed;
                ChartGrid.Visibility = Visibility.Collapsed;
                ChartIntervals.Visibility = Visibility.Hidden;
                AgoSliderPanel.Visibility = Visibility.Collapsed;
                MapTitle.Visibility = Visibility.Collapsed;
                MLScenario.Visibility = Visibility.Collapsed;

                nav.setNavigation(ATMStrategyMenu, ATMStrategySelected_MouseDown, Model.ATMStrategies.ToArray());
                highlightButton(ATMStrategyMenu, _strategy);
            }
            else
            {
                hideNavigation();

                Menus.Visibility = Visibility.Collapsed;
                ATMStrategyScrollViewer.Visibility = Visibility.Collapsed;
                AODScrollViewer.Visibility = Visibility.Visible;
            }
        }

        private string _strategy = "CURRENT RESEARCH";

        private void ATMStrategySelected_MouseDown(object sender, RoutedEventArgs e)
        {
            Label label = e.Source as Label;
            _strategy = label.Content as string;
            ForecastModel.Content = _strategy;
            hideNavigation();
            AODScrollViewer.Visibility = Visibility.Visible;
        }

        private Model getModel(string name = "")
        {
            Model model = null;
            return model;
        }

        string _factorName;

        private void Factor_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Label label = e.Source as Label;
            _factorName = label.Content as string;
            ForecastModel.Content = _factorName;
        }

        private void AODModel_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Label label = e.Source as Label;
            var modelName = label.Content as string;
            _modelName = modelName;

            hideNavigation();
            Menus.Visibility = Visibility.Collapsed;
            RegionMenu.Visibility = Visibility.Collapsed;
            AODScrollViewer.Visibility = Visibility.Visible;

            _selectedAOD.ModelName = modelName;
            requestUpdate(_selectedAOD);

            var modelNames = new List<string>();
            modelNames.Add(modelName);

            if (_AODchart1 != null)
            {
                _AODchart1.ModelNames = modelNames;
            }
        }

        //private void Model_MouseDown(object sender, MouseButtonEventArgs e)
        //{
        //    Label label = e.Source as Label;
        //    _modelName = label.Content as string;

        //    var model = MainView.GetModel(_modelName);
        //    var modelName = (model != null) ? _modelName : "";
        //    var scenario = (model != null) ? MainView.GetSenarioLabel(model.Scenario) : "";
        //    ScenarioModel.Content = modelName;
        //    ScenarioName.Content = scenario;
        //    ScenarioModel1a.Content = modelName;
        //    ScenarioName1a.Content = scenario;

        //    hideNavigation();

        //    Menus.Visibility = Visibility.Collapsed;
        //    RegionMenu.Visibility = Visibility.Collapsed;

        //    AODScrollViewer.Visibility = Visibility.Visible;

        //    var modelNames = new List<string>();
        //    modelNames.Add(_modelName);
        //    if (_chart1 != null)
        //    {
        //        _chart1.ModelNames = modelNames;
        //        _chart2.ModelNames = modelNames;
        //        _chart3.ModelNames = modelNames;
        //    }

        //    if (_AODchart1 != null)
        //    {
        //        _AODchart1.ModelNames = modelNames;
        //    }

        //    updateAods();
        //}

        private void setNavigationMenu(StackPanel panel, string[] menuItems, MouseButtonEventHandler mouseDownEvent)
        {
            BrushConverter bc = new BrushConverter();

            panel.Children.Clear();

            int count = menuItems.Count();
            for (int ii = 0; ii < count; ii++)
            {
                TextBlock label1 = new TextBlock();
                label1.Text = menuItems[ii];
                label1.Foreground = (Brush)bc.ConvertFrom("#ffffff");
                label1.Background = Brushes.Transparent;
                label1.Padding = new Thickness(0, 2, 0, 2);
                label1.HorizontalAlignment = HorizontalAlignment.Left;
                label1.VerticalAlignment = VerticalAlignment.Top;
                label1.FontFamily = new FontFamily("Helvetica Neue");
                label1.FontWeight = FontWeights.Normal;
                label1.FontSize = 10;
                label1.MouseEnter += Menu_MouseEnter;
                label1.MouseLeave += Menu_MouseLeave;
                label1.MouseDown += mouseDownEvent;
                panel.Children.Add(label1);
            }
        }

        private void hideNavigation()
        {
#if POSITIONS
            ChartIntervals.Visibility = (_condition == "Positions") ? Visibility.Hidden : Visibility.Visible;
            AgoSliderPanel.Visibility = (_condition == "Positions") ? Visibility.Hidden : Visibility.Visible;
#else
            if (_viewType == ViewType.Map)
            {
                ChartIntervals.Visibility = Visibility.Visible;
            }
            AgoSliderPanel.Visibility = Visibility.Visible;
            MapTitle.Visibility = Visibility.Visible;
#endif
            Menus.Visibility = Visibility.Collapsed;

            Level1.Visibility = Visibility.Collapsed;
            Level2.Visibility = Visibility.Collapsed;
            Level3.Visibility = Visibility.Collapsed;
            Level4.Visibility = Visibility.Collapsed;
            RegionMenu.Visibility = Visibility.Collapsed;
            ConditionMenu.Visibility = Visibility.Collapsed;
            FundamentalMenu.Visibility = Visibility.Collapsed;
            Fundamental2Menu.Visibility = Visibility.Collapsed;
            MacroMenu.Visibility = Visibility.Collapsed;
            TimeMenu.Visibility = Visibility.Collapsed;
            ATMStrategyScrollViewer.Visibility = Visibility.Collapsed;

            Level1.Children.Clear();
            Level2.Children.Clear();
            Level3.Children.Clear();
            Level4.Children.Clear();
            RegionMenu.Children.Clear();
            ConditionMenu.Children.Clear();
            FundamentalMenu.Children.Clear();
            Fundamental2Menu.Children.Clear();
            MacroMenu.Children.Clear();
            TimeMenu.Children.Clear();

            if (_viewType == ViewType.Symbol)
            {
                Navigation.Visibility = Visibility.Visible;
                ChartGrid.Visibility = Visibility.Visible;
                AgoSliderPanel.Visibility = Visibility.Collapsed;
                MapTitle.Visibility = Visibility.Collapsed;
                MLScenario.Visibility = Visibility.Visible;
            }

            setTitle();
        }

        private void World_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var frameworkElement = sender as FrameworkElement;
            string country = frameworkElement.Name;

            string[] fields = country.Split('_');
            _region = fields[0];

            WorldMap.Visibility = Visibility.Collapsed;

            RegionMap.Visibility = Visibility.Visible;
            Grid grid2 = FindName(_region + "Map") as Grid;
            if (grid2 != null)
            {
                grid2.Visibility = Visibility.Visible;
            }

            WorldMap2.Visibility = Visibility.Visible;

            setTitle();
            Region.Content = _region;
            setInfo();
        }

        private void CloseHelp_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            MktMonitorHelp.BringIntoView();
            ViewHelp.Visibility = Visibility.Collapsed;
            GenNav.Visibility = Visibility.Collapsed;
            GlobalNav.Visibility = Visibility.Collapsed;
            MktMonitorHelp.Visibility = Visibility.Collapsed;
            MMGenNav.Visibility = Visibility.Collapsed;
            MMChart.Visibility = Visibility.Collapsed;
            HelpON.Visibility = Visibility.Visible;
            HelpOFF.Visibility = Visibility.Collapsed;
        }

        private void HelpOn_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            MktMonitorHelp.BringIntoView();
            ViewHelp.Visibility = Visibility.Collapsed;
            GenNav.Visibility = Visibility.Collapsed;
            GlobalNav.Visibility = Visibility.Collapsed;
            MktMonitorHelp.Visibility = Visibility.Collapsed;
            MMGenNav.Visibility = Visibility.Collapsed;
            MMChart.Visibility = Visibility.Collapsed;
            //HelpON.Visibility = Visibility.Collapsed;
            //HelpOFF.Visibility = Visibility.Visible;
        }

        private void World_MouseDown2(object sender, MouseButtonEventArgs e)
        {
            switchBackToMap();
        }

        private void switchBackToMap(string region = "GLOBAL")
        {
            hideNavigation();

            if (region == "GLOBAL")
            {
                NorthAmericaMap.Visibility = Visibility.Collapsed;
                SouthAmericaMap.Visibility = Visibility.Collapsed;
                EuropeMap.Visibility = Visibility.Collapsed;
                AsiaMap.Visibility = Visibility.Collapsed;
                MEAMap.Visibility = Visibility.Collapsed;
                OceaniaMap.Visibility = Visibility.Collapsed;
            }

            WorldMap2.Visibility = (region == "GLOBAL") ? Visibility.Collapsed : Visibility.Visible;
            WorldMap.Visibility = (region == "GLOBAL") ? Visibility.Visible : Visibility.Collapsed;
            RegionMap.Visibility = (region == "GLOBAL") ? Visibility.Collapsed : Visibility.Visible;

            Navigation.Visibility = Visibility.Collapsed;
            ChartGrid.Visibility = Visibility.Collapsed;
            MLScenario.Visibility = Visibility.Collapsed;

#if POSITIONS
            AgoSliderPanel.Visibility = (_condition == "Positions") ? Visibility.Hidden : Visibility.Collapsed;
#else
            AgoSliderPanel.Visibility = Visibility.Visible;

#endif
            MapTitle.Visibility = Visibility.Visible;

            _region = region;
            _country = "";

            _viewType = ViewType.Map;

            stopSymbolColoring();

            setTitle();
            RegionTitle.Content = "REGION";
            Region.Content = _region;
            setInfo();
       }

        private void Country_MouseDown(object sender, MouseButtonEventArgs e)
        {
            //if (!isEconomicAsset())
            {
                FrameworkElement element = sender as FrameworkElement;

                WorldMap2.Visibility = Visibility.Visible;

                TextBlock textBlock = element as TextBlock;
                string country = (textBlock != null) ? textBlock.Text : element.Name;

                int index = country.IndexOfAny(new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' });
                if (index >= 0)
                {
                    country = country.Substring(0, index);
                }

                setActiveNavigation("");

                if (_country != country)
                {
                    _country = country;
                    _group = "";
                    _subgroup = "";
                    _industry = "";

                    GroupMenu.Children.Clear();
                    SubgroupMenu.Children.Clear();
                    IndustryMenu.Children.Clear();
                    MemberMenu.Children.Clear();
                }

                _clientPortfolioName = "";
                requestSymbols();

                AgoSliderPanel.Visibility = Visibility.Collapsed;
                MapTitle.Visibility = Visibility.Collapsed;

                _viewType = ViewType.Symbol;
                string assetType = getDatabaseAssetType();

                update();

                changeChart(_countryPortfolio.GetCountrySymbol(country, assetType), _interval.Replace(" Min", ""), true);

                setInfo();
            }
        }

        private void setActiveNavigation(string name)
        {
            if (name == "group" || name == "")
            {
                _navigationHistory.Clear();
            }

            if (_currentActiveNavigation.Length > 0 && name != _currentActiveNavigation)
            {
                _navigationHistory.Push(_currentActiveNavigation);
            }

            setCurrentNavigation(name);
        }

        private void setCurrentNavigation(string name)
        {
            _currentActiveNavigation = name;
            GroupLabel.Visibility = (name == "group") ? Visibility.Visible : Visibility.Collapsed;
            GroupList.Visibility = (name == "group") ? Visibility.Visible : Visibility.Collapsed;
            SubgroupLabel.Visibility = (name == "subgroup") ? Visibility.Visible : Visibility.Collapsed;
            SubgroupList.Visibility = (name == "subgroup") ? Visibility.Visible : Visibility.Collapsed;
            IndustryLabel.Visibility = (name == "industry") ? Visibility.Visible : Visibility.Collapsed;
            IndustryList.Visibility = (name == "industry") ? Visibility.Visible : Visibility.Collapsed;
            MemberLabel.Visibility = (name == "member") ? Visibility.Visible : Visibility.Collapsed;
            MemberList.Visibility = (name == "member") ? Visibility.Visible : Visibility.Collapsed;
        }

        private void requestSymbols()
        {
            if (_asset == "WORLD EQ INDICES")
            {
                stopSymbolColoring();

                WorldMap2.Visibility = Visibility.Visible;
                RegionMap.Visibility = Visibility.Collapsed;

                Navigation.Visibility = Visibility.Visible;
                ChartGrid.Visibility = Visibility.Visible;
                AgoSliderPanel.Visibility = Visibility.Collapsed;
                MapTitle.Visibility = Visibility.Collapsed;
                MLScenario.Visibility = Visibility.Visible;

                if (isEconomicAsset())
                {
                    setActiveNavigation("");
                }
                else
                {
                    var label = _country;
                    GroupLabel.Content = label.Replace(">", "").ToUpper();

                    bool hasMoreThanOneGroup = nav.setNavigationLevel2(_country, GroupMenu, GroupMenu_MouseDown, go_Click);

                    if (hasMoreThanOneGroup)
                    {
                        setActiveNavigation("group");
                    }
                    else
                    {
                        _group = nav.getGroup(_country);

                        Portfolio.PortfolioType type = Portfolio.PortfolioType.Index;
                        if (_group.Contains(" Index"))
                        {
                            type = Portfolio.PortfolioType.Single;
                        }
                        else
                        {
                            setActiveNavigation("member");
                        }

                        _clientPortfolioName = "";
                        requestMemberList(type);
                    }
                }
            }
        }

        private void GroupMenu_MouseDown(object sender, MouseButtonEventArgs e)
        {
            SubgroupMenu.Children.Clear();

            SymbolLabel label = e.Source as SymbolLabel;
            string group = (label.Symbol == (string)label.Content) ? label.Symbol : label.Symbol + ":" + label.Content;

            if (_group != group)
            {
                SubgroupMenu.Children.Clear();
                IndustryMenu.Children.Clear();
                MemberMenu.Children.Clear();
            }

            _group = group;
            _subgroup = "";
            _industry = "";

            string[] field1 = _group.Split(':');

            var groupLabel = _country;
            GroupLabel.Content = groupLabel.Replace(">", "").ToUpper();
            SubgroupLabel.Content = (field1.Length > 1) ? field1[1] : _group;

            bool hasMoreThanOneSubgroup = nav.setNavigationLevel3(_country, group, SubgroupMenu, SubgroupMenu_MouseDown);

            if (hasMoreThanOneSubgroup)
            {
                setActiveNavigation("subgroup");
            }
            else
            {
                setActiveNavigation("member");
                _clientPortfolioName = "";
                requestMemberList(Portfolio.PortfolioType.Index);
            }

            startSymbolColoring();

            changeChart(((field1.Length == 2) ? field1[0] : _group) + (isCMRPortfolio() ? "" : " Index"), _interval.Replace(" Min", ""), false);
        }

        private bool isCMRPortfolio()
        {
            string name = getPortfolioName();
            bool cmrPortfolio = (name == "ALPHA" || name == "ALPHA 2" || name == "MAJOR ETF WK" || name == "WORLD EQ INDICES" || name == "GLOBAL FUTURES" || name == "GLOBAL 10yr" || name == "FINANCIAL" || name == "US 100" || name == "US STOCKS");
            return cmrPortfolio;
        }

        private void SubgroupMenu_MouseDown(object sender, MouseButtonEventArgs e)
        {
            IndustryMenu.Children.Clear();

            SymbolLabel label = e.Source as SymbolLabel;
            string subgroup = (label.Symbol == (string)label.Content) ? label.Symbol : label.Symbol + ":" + label.Content;
            string description = label.Content as string;
            subgroupMouseDown(description, subgroup);
        }

        private void subgroupMouseDown(string description, string subgroup)
        {
            IndustryLabel.Content = description;

            if (_subgroup != subgroup)
            {
                IndustryMenu.Children.Clear();
                MemberMenu.Children.Clear();
            }

            _subgroup = subgroup;
            _industry = "";

            bool hasMoreThanOneIndustry = nav.setNavigationLevel4(_country, _group, subgroup, IndustryMenu, IndustryMenu_MouseDown);
            if (hasMoreThanOneIndustry)
            {
                setActiveNavigation("industry");

                string[] field2 = subgroup.Split(':');
                IndustryActionMenu.Visibility = (field2.Length == 2) ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                MembersActionMenu.Visibility = Visibility.Visible;

                setActiveNavigation("member");
                _clientPortfolioName = "";
                requestMemberList(Portfolio.PortfolioType.Index);
            }

            startSymbolColoring();

            string[] field1 = subgroup.Split(':');
            changeChart(((field1.Length == 2) ? field1[0] : _subgroup) + " Index", _interval.Replace(" Min", ""), false);
        }

        private void IndustryMenu_MouseDown(object sender, MouseButtonEventArgs e)
        {
            SymbolLabel label = e.Source as SymbolLabel;
            string industry = (label.Symbol == (string)label.Content) ? label.Symbol : label.Symbol + ":" + label.Content;
            {
                MemberMenu.Children.Clear();
                MembersActionMenu.Visibility = Visibility.Visible;

                _industry = industry;

                setActiveNavigation("member");
                _clientPortfolioName = "";
                requestMemberList(Portfolio.PortfolioType.Index);

                startSymbolColoring();

                string[] field1 = _industry.Split(':');
                changeChart(((field1.Length == 2) ? field1[0] : _industry) + " Index", _interval.Replace(" Min", ""), false);
            }
        }

        private string getGroupLabel()
        {
            string name = (_clientPortfolioName != "") ? _clientPortfolioName : (_subgroup == "") ? _group : (_industry == "") ? _subgroup : _industry;
            string[] field = name.Split(':');
            string label = (field.Length > 1) ? field[1] : field[0];
            return label;
        }

        private string getPortfolioName()
        {
            string portfolioName = "";
            if (_clientPortfolioName.Length > 0)
            {
                portfolioName = _clientPortfolioName;
            }
            else
            {
                string name = (_subgroup == "" ) ? _group : (_industry == "" ) ? _subgroup : _industry;
                string[] field = name.Split(':');
                portfolioName = field[0];
            }
            return portfolioName;
        }

        private void requestMemberList(Portfolio.PortfolioType type)
        {
            MemberMenu.Children.Clear();

            string name = getPortfolioName();

            setDefaultInterval();

            _symbolPortfolio.Clear();
            _symbolPortfolio.RequestSymbols(name, type, false);

            MemberLabel.Content = getGroupLabel();
        }

        private void setDefaultInterval()
        {
            TradeHorizon tradeHorizon = getTradeHorizon();

            if (tradeHorizon == TradeHorizon.ShortTerm)
            {
                _interval = "60";
            }
            else if (tradeHorizon == TradeHorizon.MidTerm)
            {
                _interval = "Daily";
            }
            else if (tradeHorizon == TradeHorizon.LongTerm)
            {
                _interval = "Weekly";
            }
            else if (tradeHorizon == TradeHorizon.VeryLongTerm)
            {
                _interval = "Monthly";
            }
            else if (tradeHorizon == TradeHorizon.ExtraLongTerm)
            {
                _interval = "Quarterly";
            }
        }

        private TradeHorizon getTradeHorizon()
        {
            return Trade.Manager.GetTradeHorizon(getPortfolioName());
        }

        void countryPortfolioChanged(object sender, PortfolioEventArgs e)
        {
            if (e.Type == PortfolioEventType.ReferenceData)
            {
                if (_isFundamental)
                {
                    string symbol = e.Ticker;
                    Dictionary<string, object> referenceData = e.ReferenceData;
                    //System.Diagnostics.Debug.WriteLine("Response " + (++_responseNumber) + "  " + symbol + " " + referenceData.Count + " " + e.Id);
                    colorCountry(symbol, referenceData);
                }
            }
        }

        void portfolioChanged(object sender, PortfolioEventArgs e)
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
            }
        }

        private void loadMemberPanel()
        {
            var members = _portfolio1.GetSymbols();

            var items = new List<string>();
            for (int ii = 0; ii < members.Count; ii++)
            {
                var member = members[ii];
                var fields = member.Ticker.Split(' ');
                items.Add(member.Ticker + ":" + fields[0] + ":" + member.Description);
            }
            nav.setNavigation(_memberPanel, Member_MouseDown, items.ToArray());
            _memberPanel = null;
        }

        private void Member_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var label = sender as SymbolLabel;
            var ticker = label.Ticker;

            _selectedAOD.Clear();
            _selectedAOD.Symbol = ticker;
            _selectedAOD.Description = label.Description;

            AODScrollViewer.Visibility = Visibility.Visible;
            SourceNavigation.Visibility = Visibility.Collapsed;
            AODChartGrid.Visibility = _aodChartVisibility;

            string interval0 = Study.getForecastInterval(_interval, 0);
            string interval1 = Study.getForecastInterval(_interval, 1);
            string interval2 = Study.getForecastInterval(_interval, 2);
            var intervals = new string[] { interval0, interval1, interval2 };

            foreach (var interval in intervals)
            {
                _aodBarCache.RequestBars(ticker, interval);
            }
        }
        
        void symbolPortfolioChanged(object sender, PortfolioEventArgs e)
        {
            if (e.Type == PortfolioEventType.Symbol)
            {
                lock (_memberList)
                {
                    _memberList = _symbolPortfolio.GetSymbols();
                }
            }
            else if (e.Type == PortfolioEventType.ReferenceData)
            {
                string symbol = e.Ticker;
                Dictionary<string, object> referenceData = e.ReferenceData;
                colorSymbols(symbol, referenceData);
            }
        }

        private void colorCountry(string symbol, Dictionary<string, object> referenceData)
        {
            bool done = false;

            DateTime time = getSelectedDate();

            CountryInfo[] countryInfos = Portfolio.GetCountryInfos();
            foreach (CountryInfo countryInfo in countryInfos)
            {
                if (countryInfo.Type == "EQ" && symbol == countryInfo.Symbol)
                {
                    string country = countryInfo.Name;

                    foreach(var kvp in referenceData)
                    {
                        var key = kvp.Key;

                        string[] fields = key.Split(':');
                        if (fields.Length >= 2)
                        {
                            DateTime date;
                            if (DateTime.TryParse(fields[1], out date))
                            {
                                var fieldName = fields[0];

                                lock (_countryFundamentalValues)
                                {
                                    string text = kvp.Value.ToString();
                                    if (!_countryFundamentalValues.ContainsKey(date))
                                    {
                                        _countryFundamentalValues[date] = new Dictionary<string, double>();
                                    }
                                    _countryFundamentalValues[date][country] = (text.Length == 0) ? double.NaN : double.Parse(text);
                                }

                            }
                        }
                    }

                    lock (_countrySymbols)
                    {
                        var oldCnt = _countrySymbols.Count;
                        _countrySymbols.Remove(symbol);
                        var newCnt = _countrySymbols.Count;
                        done = (oldCnt == 1 && newCnt == 0);
                    }

                    break;
                }
            }

            if (done)
            {
                Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => { fundamentalColorCountries(); }));
            }
        }

        private void fundamentalColorCountries()
        {
            var date = getSelectedDate();
            if (date != default(DateTime))
            {

                clearCountryColors();
                clearCountryToolTips();

                lock (_countryFundamentalValues)
                {
                    var oneDay = new TimeSpan(1, 0, 0, 0);
                    var countries = new Dictionary<string, double>();
                    for (int ii = 0; ii < 5; ii++) // 5 = days back to look for fundamental value
                    {
                        if (_countryFundamentalValues.ContainsKey(date))
                        {
                            var list1 = _countryFundamentalValues[date].Keys.ToList();
                            foreach (var country in list1)
                            {
                                if (!countries.ContainsKey(country))
                                {
                                    countries[country] = _countryFundamentalValues[date][country];
                                }
                            }
                        }
                        date -= oneDay;
                    }

                    var colors = calculateCategoryColors(countries);

                    showLegend();

                    foreach (var kvp in colors)
                    {
                        var country = kvp.Key;
                        var color = kvp.Value;

                        setCountryColor(country, color);

                        if (countries.ContainsKey(country))
                        {
                            setCountryTooltip(country, countries[country]);
                        }
                    }
                }
            }
        }

        private void colorSymbols(string symbol, Dictionary<string, object> referenceData)
        {

            if (referenceData.ContainsKey(_selectedFundamental))
            {
                lock (_symbolFundamentalValues)
                {
                    _symbolFundamentalValues[symbol] = (referenceData[_selectedFundamental] is double) ? (double)referenceData[_selectedFundamental] : double.NaN;
                }
            }

            bool done = false;
            lock (_labelSymbols)
            {
                var oldCnt = _labelSymbols.Count;
                _labelSymbols.Remove(symbol);
                var newCnt = _labelSymbols.Count;
                done = (oldCnt == 1 && newCnt == 0);
            }

            if (done)
            {
                Application.Current.Dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(() =>
                {
                    lock (_symbolFundamentalValues)
                    {

                        var colors = calculateCategoryColors(_symbolFundamentalValues);
                        foreach (var kvp in colors)
                        {
                            var key = kvp.Key;
                            var color = kvp.Value;
                            setSymbolColor(key, color);
                        }
                    }
                }));
            }
        }

        private bool _isFundamental = false;
        private string _selectedFundamental = "";
        private Dictionary<string, List<double>> _colorBoundaries;
        private Color[] _categoryColors = new Color[]
        {
            Color.FromArgb(0xff, 0x00, 0xff, 0xff), // higest
            Color.FromArgb(0xff, 0x29, 0x41, 0x09), // 
            Color.FromArgb(0xff, 0x56, 0xff, 0x0c), // 
            Color.FromArgb(0xff, 0x3f, 0x80, 0x22), //  
            Color.FromArgb(0xff, 0x9b, 0xcd, 0x5c), // 

            Color.FromArgb(0xff, 0xff, 0xff, 0x80), // 
            Color.FromArgb(0xff, 0xff, 0x8c, 0x00), // 
            Color.FromArgb(0xff, 0xff, 0x13, 0x00), // 
            Color.FromArgb(0xff, 0x81, 0x12, 0x0f), // 
            Color.FromArgb(0xff, 0xff, 0x00, 0xff), // lowest
        };

        int _responseNumber = 0;

        private void requestFundamentalData(Portfolio portfolio, List<string> symbols, bool currentOnly)
        {
            int number = 0;
            _responseNumber = 0;
            foreach (var symbol in symbols)
            {
                //System.Diagnostics.Debug.WriteLine("Request " + ++number + "  " + symbol);
                portfolio.RequestReferenceData(symbol, new string[] { _selectedFundamental }, currentOnly);
            }
        }

        bool _sortByRegion = false;

        private Dictionary<string, Color> calculateCategoryColors(Dictionary<string, double> input)
        {
            DateTime date = getSelectedDate();

            var output = new Dictionary<string, Color>();

            Dictionary<string, List<double>> list1 = new Dictionary<string, List<double>>();

            list1["GLOBAL"] = new List<double>();
            lock (input)
            {
                foreach (var kvp in input)
                {
                    string region = getRegion(kvp.Key);
                    if (!list1.ContainsKey(region))
                    {
                        list1[region] = new List<double>();
                    }
                    double value = kvp.Value;
                    list1[region].Add(value);
                    list1["GLOBAL"].Add(value);
                }
            }

            try
            {
                int numberOfCategories = _categoryColors.Length;
                _colorBoundaries = new Dictionary<string, List<double>>();

                foreach (var kvp in list1)
                {
                    var region = kvp.Key;
                    var list2 = kvp.Value;
                    if (list2.Count > 0)
                    {
                        list2.Sort();
                        list2.Reverse();
                        double numberInEachCategory = (double)list2.Count / numberOfCategories;
                        for (int ii = 0; ii < numberOfCategories - 1; ii++)
                        {
                            int index = (int)Math.Floor((ii + 1) * numberInEachCategory);
                            if (!_colorBoundaries.ContainsKey(region))
                            {
                                _colorBoundaries[region] = new List<double>();
                            }
                            _colorBoundaries[region].Add(list2[index]);
                        }
                    }
                }

                foreach (var kvp in input)
                {
                    var country = kvp.Key;
                    double value = kvp.Value;
                    int category = 0;
                    for (int ii = 0; ii < numberOfCategories; ii++)
                    {
                        string region = _sortByRegion ? getRegion(country) : "GLOBAL";
                        if (ii == numberOfCategories - 1 || value > _colorBoundaries[region][ii])
                        {
                            category = ii;
                            break;
                        }
                    }

                    Color color = _categoryColors[category];

                    output[country] = color;
                }
            }

            catch (Exception x)
            {
            }

            return output;
        }

        private void startSymbolColoring()
        {
            List<Tuple<string, SymbolLabel>> labels = new List<Tuple<string, SymbolLabel>>();

            List<StackPanel> stackPanels = new List<StackPanel>();
            stackPanels.Add(GroupMenu);
            stackPanels.Add(SubgroupMenu);
            stackPanels.Add(IndustryMenu);
            stackPanels.Add(MemberMenu);
            int count = stackPanels.Count;
            for (int ii = 0; ii < count; ii++)
            {
                foreach (FrameworkElement element in stackPanels[ii].Children)
                {
                    SymbolLabel label = element as SymbolLabel;
                    if (label != null)
                    {
                        labels.Add(new Tuple<string, SymbolLabel>(label.Symbol + ((ii < 3) ? " Index" : ""), label));
                    }
                }
            }

            string interval = _interval.Replace(" Min", "");

            // remove labels
            List<string> remove = new List<string>();
            foreach (KeyValuePair<string, SymbolLabel> kvp in _symbolLabels)
            {
                List<Tuple<string, SymbolLabel>> symbolLabels = labels.Where(x => x.Item1 == kvp.Key).ToList();
                if (symbolLabels.Count == 0)
                {
                    remove.Add(kvp.Key);
                }
                else
                {
                    foreach (var symbolLabel in symbolLabels)
                    {
                        SolidColorBrush brush1 = kvp.Value.Foreground as SolidColorBrush;
                        SolidColorBrush brush2 = symbolLabel.Item2.Foreground as SolidColorBrush;
                        if (brush1 != null && brush2 != null)
                        {
                            brush2.Color = brush1.Color;
                        }
                    }
                }
            }

            foreach (string symbol in remove)
            {
                _symbolLabels.Remove(symbol);
            }

            lock (_labelSymbols)
            {
                _labelSymbols.Clear();

                _countrySymbolCount = 0;
                foreach (Tuple<string, SymbolLabel> symbolLabel in labels)
                {
                    var symbol = symbolLabel.Item1;
                    SymbolLabel label;
                    if (!_symbolLabels.TryGetValue(symbol, out label))
                    {
                        _labelSymbolCount++;
                        _labelSymbols.Add(symbol);
                    }
                }
            }


            lock (_labelSymbols)
            {
                foreach (Tuple<string, SymbolLabel> symbolLabel in labels)
                {
                    var symbol = symbolLabel.Item1;
                    SymbolLabel label;
                    if (!_symbolLabels.TryGetValue(symbol, out label))
                    {
                        _symbolLabels[symbol] = symbolLabel.Item2;

                        SolidColorBrush brush = _symbolLabels[symbol].Foreground as SolidColorBrush;
                        if (brush != null)
                        {
                            brush.Color = Color.FromRgb(0xff, 0xff, 0xff);
                        }
                    }
                }
            }

            if (_isFundamental)
            {
                lock (_labelSymbols)
                {
                    requestFundamentalData(_symbolPortfolio, _labelSymbols, true);
                }

                lock (_symbolFundamentalValues)
                {
                    _symbolFundamentalValues.Clear();
                }
            }
            else
            {
                List<string> intervals = new List<string>();
                intervals.Add(interval);
                intervals.Add(getInterval(interval, 1));

                _symbolBarCache.Clear();
                foreach (var symbol in _labelSymbols)
                {
                    foreach (var interval2 in intervals)
                    {
                        _symbolBarCache.RequestBars(symbol, interval2);
                    }
                }
            }

            setInfo();
        }

        private SymbolLabel getSymbolLabel(string symbol, string description)
        {
            SymbolLabel label1 = new SymbolLabel();
            label1.Content = symbol;
            label1.Symbol = symbol;
            Color color = Color.FromRgb(0xff, 0xff, 0xff);
            label1.Foreground = new SolidColorBrush(color);
            label1.Background = Brushes.Transparent;
            label1.Padding = new Thickness(0, 2, 0, 2);
            label1.HorizontalAlignment = HorizontalAlignment.Left;
            label1.VerticalAlignment = VerticalAlignment.Top;
            label1.FontFamily = new FontFamily("Helvetica Neue");
            label1.FontWeight = FontWeights.Normal;
            label1.FontSize = 11;
            label1.ToolTip = description;
            label1.Cursor = Cursors.Hand;
            return label1;
        }

        private void Symbol_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Label label = sender as Label;
            _symbol = label.Content as string;

            _viewType = ViewType.Symbol;

            changeChart(_symbol, _interval.Replace(" Min", ""), false);
        }

        List<AOD3> _aods = new List<AOD3>(); // key = ticker

        private void loadAODs()
        {
            _aods.ForEach(x => x.AodEvent -= handleAodEvent);

            _aods.Clear();
            var text = MainView.LoadUserData("aods");
            var lines = text.Split('\n');
            foreach(var line in lines)  
            {
                if (line.Length > 0)
                {
                    var fields = line.Split('\t');
                    var aod1 = new AOD3();
                    aod1.Symbol = fields[0];
                    aod1.Description = fields[1];
                    aod1.Interval = (fields.Length > 2) ? fields[2] : "D";
                    aod1.ModelName = (fields.Length > 3) ? fields[3] : "";
                    if (aod1.ModelName == "") aod1.ModelName = _modelName;
                    aod1.PoP = "1.0";
                    aod1.MouseRightButtonUp += AOD_Capture;
                    _aods.Add(aod1);
                }
            }

            _aods.ForEach(x => x.AodEvent += handleAodEvent);
        }

        private void AOD_Capture(object sender, MouseButtonEventArgs e)
        {
            var aod = sender as AOD3;
            aod.CopyToClipboard();
        }

        private void saveAODs()
        {
            var text = "";
            _aods.ForEach(x => text += x.Symbol + "\t" + x.Description + "\t" + x.Interval +  "\t" + x.ModelName + "\n");
            MainView.SaveUserData("aods", text);
        }

        private void testAOD()
        {
            var aod1 = new AOD3();
            aod1.Symbol = "LB1 Comdty";
            aod1.Description = "LUMBER";
            aod1.PoP = "19.10";
             _aods.Add(aod1);

            var aod2 = new AOD3();
            aod2.Symbol = "RBT1 Comdty";
            aod2.Description = "REBAR";
            aod2.PoP = "1.60";
            _aods.Add(aod2);

            var aod3 = new AOD3();
            aod3.Symbol = "MERSPVHO Index";
            aod3.Description = "PVC";
            aod3.PoP = "4.35";
            _aods.Add(aod3);

            var aod4 = new AOD3();
            aod4.Symbol = "SPX Index";
            aod4.Description = "S P 500";
            aod4.PoP = "3.20";
            _aods.Add(aod4);

            var aod5 = new AOD3();
            aod5.Symbol = "DHI US Equity";
            aod5.Description = "DR HORTON";
            aod5.PoP = "3.80";
            _aods.Add(aod5);

            var aod6 = new AOD3();
            aod6.Symbol = "USGG10YR Index";
            aod6.Description = "10 YR Treas";
            aod6.PoP = "1.90";
            aod6.PxProj = "";
            _aods.Add(aod6);

            _aods.ForEach(x => x.AodEvent += handleAodEvent);
        }

 
        AOD3 _selectedAOD = null;

        private void handleAodEvent(object sender, AodEventArgs e)
        {
            var aod = sender as AOD3;
            var id = e.Id;
            if (id == AodEventType.Chart)
            {
                showAODChart();

                _aodChartVisibility = Visibility.Visible;
                AODChartGrid.Visibility = _aodChartVisibility;
                _chartSymbol = aod.Symbol;
                var interval = aod.Interval;
                _AODchart1.Change(_chartSymbol, interval);

                var modelNames = new List<string>();
                modelNames.Add(aod.ModelName);
                _AODchart1.ModelNames = modelNames;
            }
            else if (id == AodEventType.Add)
            {
                var aod1 = new AOD3();
                aod1.Symbol = "SPX Index";
                aod1.Description = "S&P Index";
                aod1.PoP = "";
                aod1.PxProj = "";
                aod1.AodEvent += handleAodEvent;
                var index = _aods.FindIndex(x => x == aod);
                _aods.Insert(index + 1, aod1);
                drawAod();
            }
            else if (id == AodEventType.Close)
            {
                if (_aods.Count > 1)
                {
                    aod.AodEvent -= handleAodEvent;
                    aod.Close();
                    _aods.Remove(aod);
                    drawAod();
                }
            }
            else if (id == AodEventType.Interval)
            {
                aod.Clear();
                drawAod();
                requestAODIndexBars();
                if (_AODchart1 != null)
                {
                    _AODchart1.Change(aod.Symbol, aod.Interval);
                }
            }
            else if (id == AodEventType.Symbol)
            {
                aod.Clear();
                aod.Description = _symbolPortfolio.GetDescription(aod.Symbol);
                drawAod();
                requestAODIndexBars();
            }
            else if (id == AodEventType.Save)
            {
                _selectedAOD = aod;
                saveAODs();
            }
            else if (id == AodEventType.Model)
            {
                _selectedAOD = aod;
                AODScrollViewer.Visibility = Visibility.Collapsed;

                hideNavigation();

                Menus.Visibility = Visibility.Visible;
                RegionMenu.Visibility = Visibility.Visible;

                AODScrollViewer.Visibility = Visibility.Collapsed;
                SourceNavigation.Visibility = Visibility.Collapsed;
                Navigation.Visibility = Visibility.Collapsed;
                ChartGrid.Visibility = Visibility.Collapsed;
                ChartIntervals.Visibility = Visibility.Collapsed;
                AgoSliderPanel.Visibility = Visibility.Collapsed;
                MapTitle.Visibility = Visibility.Collapsed;
                MLScenario.Visibility = Visibility.Collapsed;

                var modelNames = MainView.getModelNames();
                modelNames.Insert(0, "No Prediction");

                nav.setNavigation(RegionMenu, AODModel_MouseDown, modelNames.ToArray());
                highlightButton(RegionMenu, _modelName);
            }
            else if (id == AodEventType.Source)
            {
                _selectedAOD = aod;
                AODScrollViewer.Visibility = Visibility.Collapsed;
                SourceNavigation.Visibility = Visibility.Visible;
                

                _aodChartVisibility = AODChartGrid.Visibility;
                AODChartGrid.Visibility = Visibility.Collapsed;

                srcNav.UseCheckBoxes = false;
                srcNav.UseGroup = true;

                List<string> items = new List<string>();
                if (BarServer.ConnectedToBloomberg() || !BarServer.ConnectedToCQG()) items.AddRange(new string[] { "BLOOMBERG >", " ", "COMMODITIES >", " ", "EQ | AMERICAS >", " ", "EQ | ASIA PACIFIC >", " ", "EQ | EUROPE & MEA >", " ", "ETF >", " ", "FX & CRYPTO >", " ", "GLOBAL FUTURES >", " ", "INTEREST RATES >", " ", "ML PORTFOLIOS >" });
                if (BarServer.ConnectedToCQG()) items.AddRange(new string[] { " ", "CQG COMMODITIES >", " ", "CQG EQUITIES >", " ", "CQG ETF >", " ", "CQG FX & CRYPTO >", " ", "CQG INTEREST RATES >", " ", "CQG STOCK INDICES >" });
                //items.AddRange(new string[] { "COMMODITIES >", " ", "EQ | AMERICAS >", " ", "EQ | ASIA PACIFIC >", " ", "EQ | EUROPE & MEA >", " ", "CRYPTO >", " ", "ETF >", " ", "GLOBAL FUTURES >", " ", "INTEREST RATES >", " ", "US INDUSTRIES >", " ", "USER SYMBOL LIST 1 >", " ", "USER PORTFOLIOS >", , " ", "US INDUSTRIES >", " ", "US GOVERNMENT >" });
                //items.AddRange(new string[] { "COMMODITIES >", " ", "EQ | AMERICAS >", " ", "EQ | ASIA PACIFIC >", " ", "EQ | EUROPE & MEA >", " ", "ETF >", " ", "GLOBAL FUTURES >", " ", "INTEREST RATES >", " ", "US INDUSTRIES >"});
                srcNav.setNavigation(SourceLevel1, SourceLevel1_MouseDown, items.ToArray());

                highlightButton(SourceLevel1, _sourceLevel1);
                highlightButton(SourceLevel2, _sourceLevel2);
                highlightButton(SourceLevel3, _sourceLevel3);
                highlightButton(SourceLevel4, _sourceLevel4);
                highlightButton(SourceLevel5, _sourceLevel5);
                highlightButton(SourceLevel6, _sourceLevel6);

                var panel = new StackPanel();
                panel.Orientation = Orientation.Horizontal;

                var label1 = new Label();
                label1.Background = new SolidColorBrush(Color.FromRgb(0x30, 0x30, 0x30));
                label1.BorderBrush = new SolidColorBrush(Color.FromRgb(0x30, 0x30, 0x30));
                label1.BorderThickness = new Thickness(1);
                label1.Foreground = Brushes.White;
                label1.Height = 20;
                label1.Padding = new Thickness(6, 2, 0, 2);
                label1.HorizontalAlignment = HorizontalAlignment.Center;
                label1.VerticalAlignment = VerticalAlignment.Bottom;
                label1.FontFamily = new FontFamily("Helvetica Neue");
                label1.FontWeight = FontWeights.Normal;
                label1.FontSize = 11;
                label1.Cursor = Cursors.Hand;
                label1.Width = 45;
                label1.Margin = new Thickness(5, 20, 0, 0);
                label1.Content = "Close";
                label1.MouseEnter += Mouse_Enter;
                label1.MouseLeave += Mouse_Leave;
                label1.MouseDown += Close_Click;
                panel.Children.Add(label1);

                SourceLevel1.Children.Add(panel);
            }
        }

        string _sourceLevel1 = "";
        string _sourceLevel2 = "";
        string _sourceLevel3 = "";
        string _sourceLevel4 = "";
        string _sourceLevel5 = "";
        string _sourceLevel6 = "";

        Visibility _aodChartVisibility = Visibility.Collapsed;

        private void SourceLevel1_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var label = sender as SymbolLabel;
            _sourceLevel1 = label.Content as string;
            _sourceLevel2 = "";
            _sourceLevel3 = "";
            _sourceLevel4 = "";
            _sourceLevel5 = "";
            _sourceLevel6 = "";

            srcNav.setNavigationLevel1(_sourceLevel1, SourceLevel2, SourceLevel2_MouseDown, SourceLevel2_MouseDown);
            SourceLevel2.Visibility = Visibility.Visible;
            ScrollLevel3.Visibility = Visibility.Collapsed;
            ScrollLevel4.Visibility = Visibility.Collapsed;
            ScrollLevel5.Visibility = Visibility.Collapsed;
            ScrollLevel6.Visibility = Visibility.Collapsed;

            highlightButton(SourceLevel1, _sourceLevel1);
        }

        private void SourceLevel2_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var label = sender as SymbolLabel;
            _sourceLevel2 = (label.Symbol == (string)label.Content) ? label.Symbol : label.Symbol + ":" + label.Content;
            _sourceLevel3 = "";
            _sourceLevel4 = "";
            _sourceLevel5 = "";
            _sourceLevel6 = "";

            if (!srcNav.setNavigationLevel2(_sourceLevel2, SourceLevel3, SourceLevel3_MouseDown, SourceLevel3_MouseDown))
            {
                requestMembers(SourceLevel3);
            }
            ScrollLevel3.Visibility = Visibility.Visible;
            ScrollLevel4.Visibility = Visibility.Collapsed;
            ScrollLevel5.Visibility = Visibility.Collapsed;
            ScrollLevel6.Visibility = Visibility.Collapsed;

            highlightButton(SourceLevel2, _sourceLevel2);
        }

        private void SourceLevel3_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var label = sender as SymbolLabel;
            _sourceLevel3 = (label.Symbol == (string)label.Content) ? label.Symbol : label.Symbol + ":" + label.Content;
            _sourceLevel4 = "";
            _sourceLevel5 = "";
            _sourceLevel6 = "";

            if (!srcNav.setNavigationLevel3(_sourceLevel2, _sourceLevel3, SourceLevel4, SourceLevel4_MouseDown))
            {
                requestMembers(SourceLevel4);
            }
            ScrollLevel4.Visibility = Visibility.Visible;
            ScrollLevel5.Visibility = Visibility.Collapsed;
            ScrollLevel6.Visibility = Visibility.Collapsed;

            highlightButton(SourceLevel3, _sourceLevel3);
        }

        private void SourceLevel4_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var label = sender as SymbolLabel;
            _sourceLevel4 = (label.Symbol == (string)label.Content) ? label.Symbol : label.Symbol + ":" + label.Content;
            _sourceLevel5 = "";
            _sourceLevel6 = "";

            if (!srcNav.setNavigationLevel3(_sourceLevel3, _sourceLevel4, SourceLevel5, SourceLevel5_MouseDown) &&
                !srcNav.setNavigationLevel4(_sourceLevel2, _sourceLevel3, _sourceLevel4, SourceLevel5, SourceLevel5_MouseDown))
            {
                requestMembers(SourceLevel5);
            }

            highlightButton(SourceLevel4, _sourceLevel4);
            ScrollLevel5.Visibility = Visibility.Visible;
            ScrollLevel6.Visibility = Visibility.Collapsed;
        }

        private void SourceLevel5_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var label = sender as SymbolLabel;
            _sourceLevel5 = (label.Symbol == (string)label.Content) ? label.Symbol : label.Symbol + ":" + label.Content;
            _sourceLevel6 = "";

            if (!srcNav.setNavigationLevel5(_sourceLevel2, _sourceLevel3, _sourceLevel5, SourceLevel6, SourceLevel6_MouseDown))
            {
                requestMembers(SourceLevel6);
            }

            highlightButton(SourceLevel5, _sourceLevel5);
            ScrollLevel6.Visibility = Visibility.Visible;
        }

        private void SourceLevel6_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var label = sender as SymbolLabel;
            _sourceLevel6 = (label.Symbol == (string)label.Content) ? label.Symbol : label.Symbol + ":" + label.Content;

            AODScrollViewer.Visibility = Visibility.Visible;
            SourceNavigation.Visibility = Visibility.Collapsed;
            AODChartGrid.Visibility = _aodChartVisibility;
        }

        StackPanel _memberPanel = null;

        private void requestMembers(StackPanel panel)
        {
            _memberPanel = panel;
            _memberPanel.Visibility = Visibility.Visible;
            requestPortfolio();
        }

        Portfolio _portfolio1 = new Portfolio(5001);

        private void requestPortfolio()
        {
            var name = getSourcePortfolioName();
            var type = getSourcePortfolioType(name);

            _portfolio1.Clear();
            _portfolio1.RequestSymbols(name, type, true);
         }

        private Portfolio.PortfolioType getSourcePortfolioType(string name)
        {
            Portfolio.PortfolioType type = Portfolio.PortfolioType.Index;

            if (_sourceLevel1 == "ML PORTFOLIOS >")
            {
                type = Portfolio.PortfolioType.Model;
            }
            else if (Portfolio.IsBuiltInPortfolio(name))
            {
                type = Portfolio.PortfolioType.BuiltIn;
            }
            else if (name.Contains(" Index") || name.Contains(" Comdty") || name.Contains(" Equity") || name.Contains(" Curncy"))
            {
                type = Portfolio.PortfolioType.Single;
            }
            else if (name.Contains("Spread"))
            {
                type = Portfolio.PortfolioType.Spread;
            }
            else
            {
                type = Portfolio.PortfolioType.Index;
            }
            return type;
        }

        private string getSourcePortfolioName()
        {
            return getSourcePortfolioName(_sourceLevel2, _sourceLevel3, _sourceLevel4, _sourceLevel5,_sourceLevel6);
        }

        private string getSourcePortfolioName(string nav2, string nav3, string nav4, string nav5, string nav6)
        {
            string portfolioName = "";
            string name = "";

            if (nav6 != "" && nav6 != "MEMBERS") name = nav6;
            else if (nav5 != "" && nav5 != "MEMBERS") name = nav5;
            else if (nav4 != "" && nav4 != "MEMBERS") name = nav4;
            else if (nav3 != "" && nav3 != "MEMBERS") name = nav3;
            else name = nav2;

            string[] field = name.Split(':');
            portfolioName = field[0];
            return portfolioName;
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

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Label label = sender as Label;
            AODScrollViewer.Visibility = Visibility.Visible;
            SourceNavigation.Visibility = Visibility.Collapsed;
            AODChartGrid.Visibility = _aodChartVisibility;
        }

        private void drawAod()
        {
            AODGrid.Children.Clear();
            var rowCnt = (_aods.Count + 2) / 3;
            AODGrid.RowDefinitions.Clear();
            for(var row = 0; row < rowCnt; row++)
            {
                var rowDef = new RowDefinition();
                rowDef.Height = new GridLength(1.0, GridUnitType.Auto);
                AODGrid.RowDefinitions.Add(rowDef);
            }

            for(var ii = 0; ii < _aods.Count; ii++) 
            {
                var aod = _aods[ii];
                Grid.SetColumn(aod, ii % 4);
                Grid.SetRow(aod, ii / 4);

                AODGrid.Children.Add(aod);

                aod.Draw();
            }
        }

        private void changeChart(string symbol, string interval, bool initialize)
        {
            highlightIntervalButton(ChartIntervals, interval);

            if (_chart1 != null)
            {
                string name = getPortfolioName();
                if (name != "")
                {
                    _chart1.PortfolioName = name;
                    _chart2.PortfolioName = name;
                    _chart3.PortfolioName = name;
                }
            }

            if (initialize || symbol != _chartSymbol || interval != _chartInterval)
            {
                _chartSymbol = symbol;
                _chartInterval = interval;

                addCharts();
                _chart1.Change(symbol, _chart1.GetOverviewInterval(interval, 2));
                _chart2.Change(symbol, _chart1.GetOverviewInterval(interval, 1));
                _chart3.Change(symbol, interval);
            }
        }

        //private void Monitors_MouseDown(object sender, MouseButtonEventArgs e)
        //{
        //    _viewType = ViewType.Map;

        //    hideNavigation();
        //    //setView(ViewType.Cost);
        //}

        private void GlobalMacro_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _viewType = ViewType.Map;

            hideNavigation();
            setView(ViewType.Map);
            startMapColoring(3000);
        }

		private void Alert_MouseDown(object sender, MouseButtonEventArgs e)
		{
			Close();
			_mainView.Content = new Alerts(_mainView);
		}

		private void ML_MouseDown(object sender, MouseButtonEventArgs e)
		{
			Close();
			_mainView.Content = new AutoMLView(_mainView);
		}
		private void OurView_MouseDown(object sender, MouseButtonEventArgs e)
		{
			Close();
			_mainView.Content = new Charts(_mainView);
		}

		private void GlobalCost_MouseDown(object sender, MouseButtonEventArgs e)
        {
            hideNavigation();
            GlobalCostNav.Visibility = Visibility.Visible;
            ChartIntervals.Visibility = Visibility.Visible;
            ProdCostImpactNav.Visibility = Visibility.Collapsed;
            AOD3ControlGrid.Visibility = Visibility.Collapsed;
            CostCal.Visibility = Visibility.Collapsed;
            showMap();
            _viewType = ViewType.Map;
            startMapColoring(500);
        }

        private void PredictionMatrix_MouseDown(object sender, MouseButtonEventArgs e)
        {
            hideNavigation();
            GlobalCostNav.Visibility = Visibility.Visible;
            ChartIntervals.Visibility = Visibility.Visible;
            ProdCostImpactNav.Visibility = Visibility.Collapsed;
            AOD3ControlGrid.Visibility = Visibility.Collapsed;
            CostCal.Visibility = Visibility.Collapsed;
            showMap();
            _viewType = ViewType.Map;
            startMapColoring(500);
        }

        private void SelectModel_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var label = sender as Label;
            var name = label.Content as String;
            _modelName = name;
            updateModel();
            ScenarioMenu1.Visibility = Visibility.Collapsed;        
        }

        private void FundamentalML_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Close();
            _mainView.Content = new PortfolioBuilder(_mainView);
        }

        private void REFRESH_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {   
            //requestIndexBars();
        }

        private void SaveMonitorSettings_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            saveAODs();
            //requestIndexBars();
        }

        //private void requestIndexBars()
        //{
        //    List<string> indexIntervals = getIntervals();

        //    _indexBarRequestCount = _indexSymbols.Count * indexIntervals.Count;

        //    lock (_indexBarRequests)
        //    {
        //        _indexBarRequests.Clear();
        //    }

        //    _conditionBrushes.Clear();

        //    if (_indexBarRequestCount > 0)
        //    {

        //        lock (_indexBarRequests)
        //        {
        //            foreach (var interval in indexIntervals)
        //            {
        //                foreach (string symbol in _indexSymbols)
        //                {
        //                    _indexBarRequests.Add(symbol + ":" + interval);
        //                }
        //            }
        //        }

        //        foreach (var interval in indexIntervals)
        //        {
        //            foreach (string symbol in _indexSymbols)
        //            {
        //                _barCache.RequestBars(symbol, interval, true);
        //            }
        //        }
        //    }
        //    else
        //    {
        //        requestSymbolBars();
        //    }
        //}

        public void Close()
        {
            saveAODChartProperties();

            _mapTimer.Tick -= new System.EventHandler(mapTimer_Tick);
            _mapTimer.Stop();

            _countryPortfolio.PortfolioChanged -= new PortfolioChangedEventHandler(countryPortfolioChanged);
            _symbolPortfolio.PortfolioChanged -= new PortfolioChangedEventHandler(symbolPortfolioChanged);
            _portfolio1.PortfolioChanged -= new PortfolioChangedEventHandler(portfolioChanged);

            //List<Alert> alerts = Alert.Manager.Alerts;
            //foreach (Alert alert in alerts)
            //{
            //    alert.FlashEvent -= new FlashEventHandler(alert_FlashEvent);
            //}

            stopMapColoring();
            stopSymbolColoring();

            if (_aodUpdateThread != null)
            {
                _aodUpdateThreadStop = true;
                _aodUpdateThread.Join(100);
                _aodUpdateThread = null;
            }

            if (_aodBarCache != null)
            {
                _aodBarCache.Clear();
            }

            closeAODChart();
            removeCharts();

            saveAODs();
            setInfo();
         }

        private List<int> getCountrySignals(string symbol, string interval, string condition, bool calculate)
        {
            List<int> signals = null;
            lock (_countrySignals)
            {
                string key = (condition == "Positions") ? symbol + ":" + condition : symbol + ":" + interval + ":" + condition;
                if (condition == "Positions")
                {
                    if (MainView.PositionsAvailable)
                    {
                        int position = Trade.Manager.getCurrentPosition(symbol, "W");
                        signals = new List<int>();
                        signals.Add(position);
                        _countrySignals[key] = signals;
                    }
                }
                else
                {
                    if (calculate || !_countrySignals.TryGetValue(key, out signals) || (signals != null && signals.Count == 0))
                    {

                        signals = getSignals(condition, symbol, interval);
                        _countrySignals[key] = signals;
                    }
                }
            }
            return signals;
        }

        private List<int> getSymbolSignals(string symbol, string interval, string condition, bool calculate)
        {
            List<int> signals = null;
            lock (_symbolSignals)
            {
                string key = symbol + ":" + interval + ":" + condition;

                if (condition == "Positions")
                {
                    if (MainView.PositionsAvailable)
                    {
                        int position = Trade.Manager.getCurrentPosition(symbol, "W");
                        signals = new List<int>();
                        signals.Add(position);
                        lock (_countrySignals)
                        {
                            _countrySignals[key] = signals;
                        }
                    }
                }
                else
                {
                    if (calculate || !_symbolSignals.TryGetValue(key, out signals))
                    {
                        Series[] series = _symbolBarCache.GetSeries(symbol, interval, new string[] { "High", "Low", "Close" }, 0);

                        if (series != null)
                        {
                            Series hi = series[0];
                            Series lo = series[1];
                            Series cl = series[2];

                            if (hi != null && lo != null && cl != null)
                            {
                                signals = getSignals(condition, symbol, interval);
                                _symbolSignals[key] = signals;
                            }
                        }
                    }
                }
            }
            return signals;
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

        private List<int> getSignals(string condition, string symbol, string interval)
        {
            List<int> signals = null;

 
            if (_countryBarCache != null)
            {
                if (condition == "ATM Pxf")
                {
                    var predictionAgoCount = _barCount;
                    var tickers = new List<string>();
                    tickers.Add(symbol);
                    var model = MainView.GetModel(_modelName);

                    if (model != null)
                    {
                        var pathName = @"senarios\" + model.Name + @"\" + interval + (model.UseTicker ? @"\" + MainView.ToPath(symbol) : "");
                        var data = atm.getModelData(model.FeatureNames, model.Scenario, _countryBarCache, tickers, interval, predictionAgoCount, model.MLSplit, false, true);
                        atm.saveModelData(pathName + @"\test.csv", data);
                        var predictions = MainView.AutoMLPredict(pathName, MainView.GetSenarioLabel(model.Scenario));

                        if (predictions.Count > 0)
                        {
                            if (predictions.ContainsKey(symbol))
                            {
                                if (predictions[symbol].ContainsKey(interval))
                                {
                                    var times = _countryBarCache.GetTimes(symbol, interval, 0, _barCount);
                                    var values = new List<double>();
                                    foreach (var time in times)
                                    {
                                        var value = (predictions[symbol][interval].ContainsKey(time)) ? predictions[symbol][interval][time] : double.NaN;
                                        values.Add(value);
                                    }

                                    signals = new List<int>();
                                    int count = values.Count;
                                    for (int ii = count - 1; ii >= 0; ii--)
                                    {
                                        var signal = 0;
                                        var oldVal = (ii > 0) ? values[ii - 1] : double.NaN;
                                        var newVal = values[ii];

                                        if (!double.IsNaN(newVal) && !double.IsNaN(oldVal))
                                        {
                                            var newUp = newVal >= 0.5;
                                            var oldUp = oldVal >= 0.5;
                                            if (newUp && !oldUp) signal = 2;
                                            else if (!newUp && oldUp) signal = -2;
                                            else if (newUp) signal = 1;
                                            else signal = -1;
                                        }
                                        signals.Add(signal);
                                    }
                                }
                            }
                        }
                    }
                }

                else if (condition == "TC")
                {
                    Series[] series = _countryBarCache.GetSeries(symbol, interval, new string[] { "High", "Low", "Close" }, 0);
                    if (series != null)
                    {
                        Series hi = series[0];
                        Series lo = series[1];
                        Series cl = series[2];

                        if (hi != null && lo != null && cl != null)
                        {
                            signals = getFTSignals(hi, lo, cl);
                        }
                    }
                }
                else if (condition == "HB")
                {
                    Series[] series = _countryBarCache.GetSeries(symbol, interval, new string[] { "High", "Low", "Close" }, 0);
                    if (series != null)
                    {
                        Series hi = series[0];
                        Series lo = series[1];
                        Series cl = series[2];

                        if (hi != null && lo != null && cl != null)
                        {
                            signals = getHBSignals(hi, lo, cl);
                        }
                    }
                }
                else if (condition == "TSB")
                {
                    Series[] series = _countryBarCache.GetSeries(symbol, interval, new string[] { "High", "Low", "Close" }, 0);
                    if (series != null)
                    {
                        Series hi = series[0];
                        Series lo = series[1];
                        Series cl = series[2];

                        if (hi != null && lo != null && cl != null)
                        {
                            signals = atm.getTSBSignals2(_maxAgo, hi, lo, cl);
                        }
                    }
                }
                else if (condition == "FT")
                {
                    Series[] series = _countryBarCache.GetSeries(symbol, interval, new string[] { "High", "Low", "Close" }, 0);
                    if (series != null)
                    {
                        Series hi = series[0];
                        Series lo = series[1];
                        Series cl = series[2];

                        if (hi != null && lo != null && cl != null)
                        {
                            signals = getFTSignals(hi, lo, cl);
                        }
                    }
                }
				//else if (condition == "ATM ANALYSIS")
				//{
				//	Series[] series = _countryBarCache.GetSeries(symbol, interval, new string[] { "High", "Low", "Close" }, 0);
				//	if (series != null)
				//	{
				//		Series hi = series[0];
				//		Series lo = series[1];
				//		Series cl = series[2];

				//		if (hi != null && lo != null && cl != null)
				//		{
				//			//signals = getATMAnalysis(hi, lo, cl);
				//		}
				//	}
				//}
				else
                {
                    List<string> intervals = new List<string>();
                    intervals.Add(interval);
                    intervals.Add(getInterval(interval, 1));

                    Dictionary<string, List<DateTime>> times = new Dictionary<string, List<DateTime>>();
                    Dictionary<string, Series[]> bars = new Dictionary<string, Series[]>();
                    foreach (string interval2 in intervals)
                    {
                        times[interval2] = (_countryBarCache.GetTimes(symbol, interval2, 0, _barCount));
                        bars[interval2] = _countryBarCache.GetSeries(symbol, interval2, new string[] { "Open", "High", "Low", "Close" }, 0, _barCount);
                    }
                    var referenceData = new Dictionary<string, object>();
                    var strategyData = atm.getStrategy(condition, times, bars, intervals.ToArray(), referenceData);
                    signals = new List<int>();
                    if (strategyData != null)
                    {
                        for (int ago = 0; ago < _maxAgo; ago++)
                        {
                            int index = strategyData.Count - 1 - ago;
                            if (index >= 0)
                            {
                                int sig = (int)strategyData[index];
                                signals.Add(sig);
                            }
                        }
                    }
                }
            }
            return signals;
        }

		//private List<int> getATMAnalysis(Series hi, Series lo, Series cl)
		//{
		//  return ATMAnalysis(_model.DataRange.Time1, interval);
		//}

		private List<int> getFTSignals(Series hi, Series lo, Series cl)
        {
            return atm.GetTrendCondition(_maxAgo, hi, lo, cl);
        }

        private List<int> getHBSignals(Series hi, Series lo, Series cl)
        {
            List<int> signals = new List<int>();

            Dictionary<string, Series> tl = atm.calculateTrend(hi, lo, cl);
            Series TLup = tl["TLup"];
            Series TLdn = tl["TLdn"];

            List<Series> TargetUp = atm.calculateUpperTargets(hi, lo, TLup);
            List<Series> TargetDn = atm.calculateLowerTargets(hi, lo, TLdn);

            Series hi1 = (TargetUp[0] & (hi < TargetUp[0]));
            Series hi2 = (TargetUp[0] & (hi >= TargetUp[0]) & (hi < TargetUp[1])).Replace(1, 2);
            Series hi3 = (TargetUp[0] & (hi >= TargetUp[1]) & (hi < TargetUp[2])).Replace(1, 3);
            Series hi4 = (TargetUp[0] & (hi >= TargetUp[2]) & (hi < TargetUp[3])).Replace(1, 4);
            Series hi5 = (TargetUp[0] & (hi >= TargetUp[3])).Replace(1, 5);

            Series lo1 = (TargetDn[0] & (lo > TargetDn[0]));
            Series lo2 = (TargetDn[0] & (lo <= TargetDn[0]) & (lo > TargetDn[1])).Replace(1, 2);
            Series lo3 = (TargetDn[0] & (lo <= TargetDn[1]) & (lo > TargetDn[2])).Replace(1, 3);
            Series lo4 = (TargetDn[0] & (lo <= TargetDn[2]) & (lo > TargetDn[3])).Replace(1, 4);
            Series lo5 = (TargetDn[0] & (lo <= TargetDn[3])).Replace(1, 5);

            Series heatBars = (hi1 + hi2 + hi3 + hi4 + hi5) - (lo1 + lo2 + lo3 + lo4 + lo5);

            for (int ago = 0; ago < _maxAgo; ago++)
            {
                int index = heatBars.Count - 1 - ago;

                if (index >= 0)
                {
                    int sig = (int)heatBars[index];
                    signals.Add(sig);
                }
            }
            return signals;
        }

        private void Ago_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            changeAgo((int)(_maxAgo - e.NewValue));
        }

        private void changeAgo(int ago)
        {
            if (ago < 0) ago = 0;
            else if (ago > _maxAgo) ago = _maxAgo;

            if (_ago != ago)
            {
                _ago = ago;

                clearCountryColors();
                clearCountryToolTips();

                setTitle();

                if (_isFundamental)
                {
                    fundamentalColorCountries();
                }
                else if (isEconomicAsset())
                {
                    economicColorCountries();
                }
                else
                {
                    calulateCountryColors();
                }
            }
        }

        private void calulateCountryColors()
        {
            string assetType = getDatabaseAssetType();
            List<CountryInfo> countries = getSymbols(assetType);
            string interval = _interval.Replace(" Min", "");

            var symbols = new Dictionary<string, List<string>>();
            foreach (CountryInfo country in countries)
            {
                var symbol = country.Symbol;
                if (!symbols.ContainsKey(symbol))
                {
                    symbols[symbol] = new List<string>();
                }
                symbols[symbol].Add(country.Name);
            }

            foreach(var kvp in symbols)
            {
                calculateCountryColor(kvp.Value, kvp.Key, interval, false);
            }

            foreach (string symbol in _symbolLabels.Keys)
            {
                calculateSymbolColor(symbol, interval, false);
            }
        }

        private void BackNavigation_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_navigationHistory.Count > 0)
            {
                string name = _navigationHistory.Pop();
                setCurrentNavigation(name);
            }
            else
            {
                switchBackToMap(_region);
            }
        }

        private void ChartInterval_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Label label = e.Source as Label;
            string time = label.Content as string;

            if (time == "D") time = "Daily";
            else if (time == "W") time = "Weekly";
            else if (time == "M") time = "Monthly";
            else if (time == "Q") time = "Quarterly";
            
            resetAllCountryColors();
            _interval = time;

            highlightIntervalButton(ChartIntervals, _interval.Replace(" Min", ""));

            update();
            if (_chart1 != null && _chart1.IsVisible())
            {
                changeChart(_chartSymbol, _interval.Replace(" Min", ""), false);
            }

            if (_AODchart1 != null)
            {
                _AODchart1.Change(_chartSymbol, _interval.Replace(" Min", ""));
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

        private Canvas getCanvas()
        {
            Viewbox viewBox = null;
            if (WorldMap.Visibility == Visibility.Visible)
            {
                viewBox = WorldMap.Children[0] as Viewbox;
            }
            else
            {
                Grid grid = RegionMap as Grid;
                foreach (UIElement element in grid.Children)
                {
                    Grid childGrid = element as Grid;
                    if (childGrid != null && childGrid.Visibility == Visibility.Visible)
                    {
                        viewBox = childGrid.Children[0] as Viewbox;
                        break;
                    }
                }
            }
            Canvas canvas = (viewBox != null) ? viewBox.Child as Canvas : null;
            return canvas;
        }

        private BitmapSource captureMap()
        {
            double dpiX = 300;
            double dpiY = 300;


            Canvas canvas = getCanvas();
            canvas.Background = Brushes.White;
            canvas.UpdateLayout();

            Visual target = canvas;

            Rect bounds = VisualTreeHelper.GetDescendantBounds(target);
            RenderTargetBitmap rtb = new RenderTargetBitmap((int)(bounds.Width * dpiX / 96.0), (int)(bounds.Height * dpiY / 96.0), dpiX, dpiY, PixelFormats.Pbgra32);
            DrawingVisual dv = new DrawingVisual();
            using (DrawingContext ctx = dv.RenderOpen())
            {
                VisualBrush vb = new VisualBrush(target);
                ctx.DrawRectangle(vb, null, new Rect(new Point(), bounds.Size));
            }
            rtb.Render(dv);

            canvas.Background = Brushes.Black;
            canvas.UpdateLayout();

            return rtb;
        }

        public void CopyToClipboard()
        {
            BitmapSource source = captureMap();
            Clipboard.SetData(DataFormats.Bitmap, source);
        }

        public void Print(PrintDialog printDialog)
        {
            const double inch = 96;

            const double leftmargin = 0.5 * inch;
            const double topmargin = 0.5 * inch;

            var renderTarget = captureMap();
            var drawingVisual = new DrawingVisual();
            using (var drawingContext = drawingVisual.RenderOpen())
            {
                var rectangle = new Rect(
                    leftmargin, topmargin,
                    printDialog.PrintableAreaWidth - leftmargin * 2,
                    printDialog.PrintableAreaHeight - topmargin * 2);

                drawingContext.DrawImage(renderTarget, rectangle);
            }
            printDialog.PrintVisual(drawingVisual, "CMR Map");
        }

        private void initializeContextMenu(Grid map)
        {
            ContextMenu menu = new ContextMenu();
            menu.Margin = new Thickness(0);
            menu.Padding = new Thickness(0);
            menu.Foreground = Brushes.White;
            menu.Background = Brushes.Black;
            menu.FontSize = 9;
            menu.BorderThickness = new Thickness(1);

            //MenuItem item = new MenuItem();
            //item = new MenuItem();
            //item.Margin = new Thickness(4);
            //item.Padding = new Thickness(4);
            //item.FontSize = 9;
            //item.Foreground = Brushes.LightGray;
            //item.Background = Brushes.Black;
            //item.StaysOpenOnClick = true;
            //menu.Items.Add(item);
            //item.Header = "Exporting";
            //addSubMenu(item, new string[] { "Print Chart", "Copy to Clipboard" }, false);

            Viewbox vb = map.Children[0] as Viewbox;
            Canvas canvas = vb.Child as Canvas;
            canvas.ContextMenu = menu;
        }

        private void addSubMenu(MenuItem parent, string[] names, bool indicator)
        {
            int count = names.Length;

            for (int ii = 0; ii < count; ii++)
            {
                string name = names[ii];

                bool active = false;

                MenuItem child = new MenuItem();
                child.Margin = new Thickness(0);
                child.Padding = new Thickness(4);
                child.FontSize = 9;
                child.Foreground = Brushes.LightGray;
                child.Background = Brushes.Black;
                child.Header = name;
                child.IsCheckable = indicator;
                child.IsChecked = active;

                child.StaysOpenOnClick = indicator;
                child.Click += new RoutedEventHandler(contextMenu_Click);
                parent.Items.Add(child);
            }
        }

        void contextMenu_Click(object sender, RoutedEventArgs e)
        {
            MenuItem item = sender as MenuItem;
            string name = item.Header as string;

            if (name == "Copy to Clipboard")
            {
                CopyToClipboard();
            }
            else if (name == "Print Chart")
            {
                PrintDialog printDialog = new PrintDialog();
                if (printDialog.ShowDialog() == true)
                {
                    Print(printDialog);
                }
            }
        }

        private void MapTitle_MouseDown(object sender, MouseButtonEventArgs e)
        {
            CopyToClipboard();
        }

        private void LevelLabel_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var tb = sender as TextBlock;
            string name = tb.Name;
            if (name == "L1")
            {
                showLevel1();
                highlightButton(Level1, L1.Text as string);
            }
            else if (name == "L2")
            {
                showLevel1();
                showLevel2(L1.Text as string);
                highlightButton(Level1, L1.Text as string);
                highlightButton(Level2, L2.Text as string);
            }
            else if (name == "L3")
            {
                showLevel1();
                showLevel2(L1.Text as string);
                showLevel3(L2.Text as string);
                highlightButton(Level1, L1.Text as string);
                highlightButton(Level2, L2.Text as string);
                highlightButton(Level3, L3.Text as string);
            }
            else if (name == "L4")
            {
                showLevel1();
                showLevel2(L1.Text as string);
                showLevel3(L2.Text as string);
                showLevel4(L3.Text as string);
                highlightButton(Level1, L1.Text as string);
                highlightButton(Level2, L2.Text as string);
                highlightButton(Level3, L3.Text as string);
                highlightButton(Level4, L4.Text as string);
            }
        }

        private void MktMonitorHelp_MouseDown(object sender, MouseButtonEventArgs e)
        {
            MktMonitorHelpSection.BringIntoView();
            MktMonitorHelp.Visibility = Visibility.Visible;
            MMGenNav.Visibility = Visibility.Collapsed;
            MMChart.Visibility = Visibility.Collapsed;
            ViewHelp.Visibility = Visibility.Collapsed;
            GenNav.Visibility = Visibility.Collapsed;
            GlobalNav.Visibility = Visibility.Collapsed;
        }

        private void MMGenNav_MouseDown(object sender, MouseButtonEventArgs e)
        {
            MMGenNavSection.BringIntoView();
            MktMonitorHelp.Visibility = Visibility.Collapsed;
            MMGenNav.Visibility = Visibility.Visible;
            MMChart.Visibility = Visibility.Collapsed;
            ViewHelp.Visibility = Visibility.Collapsed;
            GenNav.Visibility = Visibility.Collapsed;
            GlobalNav.Visibility = Visibility.Collapsed;
        }

        private void MMChart_MouseDown(object sender, MouseButtonEventArgs e)
        {
            MMChartSection.BringIntoView();
            MktMonitorHelp.Visibility = Visibility.Collapsed;
            MMGenNav.Visibility = Visibility.Collapsed;
            MMChart.Visibility = Visibility.Visible;
            ViewHelp.Visibility = Visibility.Collapsed;
            GenNav.Visibility = Visibility.Collapsed;
            GlobalNav.Visibility = Visibility.Collapsed;
        }

        private void ViewHelp_MouseDown(object sender, MouseButtonEventArgs e)
        {
            ViewHelpSection.BringIntoView();
            MktMonitorHelp.Visibility = Visibility.Collapsed;
            MMGenNav.Visibility = Visibility.Collapsed;
            MMChart.Visibility = Visibility.Collapsed;
            ViewHelp.Visibility = Visibility.Visible;
            GenNav.Visibility = Visibility.Collapsed;
            GlobalNav.Visibility = Visibility.Collapsed;
        }

        private void GenNav_MouseDown(object sender, MouseButtonEventArgs e)
        {
            GenNavSection.BringIntoView();
            MktMonitorHelp.Visibility = Visibility.Collapsed;
            MMGenNav.Visibility = Visibility.Collapsed;
            MMChart.Visibility = Visibility.Collapsed;
            ViewHelp.Visibility = Visibility.Collapsed;
            GenNav.Visibility = Visibility.Visible;
            GlobalNav.Visibility = Visibility.Collapsed;
        }

        private void GlobalNav_MouseDown(object sender, MouseButtonEventArgs e)
        {
            GlobalNavSection.BringIntoView();
            MktMonitorHelp.Visibility = Visibility.Collapsed;
            MMGenNav.Visibility = Visibility.Collapsed;
            MMChart.Visibility = Visibility.Collapsed;
            ViewHelp.Visibility = Visibility.Collapsed;
            GenNav.Visibility = Visibility.Collapsed;
            GlobalNav.Visibility = Visibility.Visible;
        }

        private void MLModel_MouseDown(object sender, MouseButtonEventArgs e)
        {
            ScenarioMenu1.Visibility = Visibility.Visible;
            MktMonitorHelp.Visibility = Visibility.Collapsed;
            MMGenNav.Visibility = Visibility.Collapsed;
            MMChart.Visibility = Visibility.Collapsed;
            ViewHelp.Visibility = Visibility.Collapsed;
            GenNav.Visibility = Visibility.Collapsed;
            GlobalNav.Visibility = Visibility.Collapsed;

            nav.UseCheckBoxes = false;

            var modelNames = MainView.getModelNames();

            modelNames.Insert(0, "No Predictions");

            nav.setNavigation(ScenarioMenu1, SelectModel_MouseDown, modelNames.ToArray());
            highlightButton(ScenarioMenu1, _modelName);
        }

        private void AODView_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Close();
            //_mainView.Content = new Competitor(_mainView);
        }

        private void Help_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            ViewHelpSection.BringIntoView();
            MktMonitorHelp.Visibility = Visibility.Visible;
            MMGenNav.Visibility = Visibility.Collapsed;
            MMChart.Visibility = Visibility.Collapsed;
            ViewHelp.Visibility = Visibility.Collapsed;
            GenNav.Visibility = Visibility.Collapsed;
            GlobalNav.Visibility = Visibility.Collapsed;
            //HelpOFF.Visibility = Visibility.Visible;
            //HelpON.Visibility = Visibility.Collapsed;
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

        private void NextAgo_MouseDown(object sender, MouseButtonEventArgs e)
        {
            changeAgo(_ago - 1);
            AgoSlider.Value = _maxAgo - _ago;
        }

        private void PreviousAgo_MouseDown(object sender, MouseButtonEventArgs e)
        {
            changeAgo(_ago + 1);
            AgoSlider.Value = _maxAgo - _ago;
        }
    }
}
