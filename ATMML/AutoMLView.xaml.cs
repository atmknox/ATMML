using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
//using Bloomberglp.TerminalApi;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using LoadingControl.Control;
using Microsoft.Win32;

namespace ATMML
{
    public partial class AutoMLView : ContentControl
    {
        MainView _mainView = null;

        string _view = "Trade Overview";
        string _symbol = "";
        int _monthlySummaryYear = 2014;
        int _monthlySummaryMonth = 1;
        string _tradeOverviewGroup = "Open";

        string _level1 = "";
        string _level2 = "";
        string _level3 = "";
        string _level4 = "";
        string _level5 = "";
        string _level6 = "";

        string _portfolioRequested = "";
        int _portfolioRequestedCount = 0;
        Portfolio.PortfolioType _type;

        Portfolio.PortfolioType _clientPortfolioType = Portfolio.PortfolioType.Index;
        string _clientPortfolioName = "";

        Navigation nav = new Navigation();

        List<Trade> _allTrades = new List<Trade>();
        List<Trade> _trades = new List<Trade>();
        List<Trade> _positions = new List<Trade>();

        Dictionary<string, SummaryInfo> _summary = new Dictionary<string, SummaryInfo>();

        int _update = 0;
        DispatcherTimer _portfolioTimer = new DispatcherTimer();
        DispatcherTimer _testTimer = new DispatcherTimer();

        Portfolio _portfolio1 = new Portfolio(7);
        Portfolio _portfolio2 = new Portfolio(8);
        Dictionary<string, int> _sizes = new Dictionary<string, int>();

        Portfolio _filterPortfolio = new Portfolio(40);

        object _memberListLock = new object();
        List<Member> _memberList = new List<Member>();

        bool _addFlash = false;

        Dictionary<string, Model> _models = new Dictionary<string, Model>();

        BarCache _barCache;
        string _interval = "Daily";
        List<string> _indexSymbols = new List<string>();

        string _selectedSector = "";

        public AutoMLView(MainView mainView, string portfolioName = "")
        {
            _mainView = mainView;

            DataContext = this;

            InitializeComponent();

            loadMLModels();

            if (_selectedModel == "")
            {
                loadExampleModel();
            }

            model_to_ui();

            string info = _mainView.GetInfo("AutoML");

            if (info != null && info.Length > 0)
            {
                string[] fields = info.Split(';');
                int count = fields.Length;
                if (count > 0) _view = fields[0];
                if (count > 2) _symbol = fields[2];
                if (count > 3) _tradeOverviewGroup = fields[3];
                if (count > 4) _monthlySummaryYear = int.Parse(fields[4]);
                if (count > 5) _monthlySummaryMonth = int.Parse(fields[5]);
                if (count > 6) _selectedSector = fields[6];
                if (count > 7) _level2 = fields[7];
                if (count > 8) _level3 = fields[8];
                if (count > 9) _level4 = fields[9];
                if (count > 10) _level5 = fields[10];
                if (count > 13) bool.TryParse(fields[13], out _useUserFactorModel);
                if (count > 14) _level1 = fields[14];
                if (count > 15) _level6 = fields[15];
            }

            _useUserFactorModel = true;

            var visibility = (MainView.EnableRickStocks) ? Visibility.Visible : Visibility.Collapsed;

            TrainerPanel.Visibility = Visibility.Visible;
            LabelPanel.Visibility = Visibility.Visible;
           
            TrainResults.Visibility = Visibility.Visible;

            UserFactorModelGrid.Visibility = _useUserFactorModel ? Visibility.Collapsed : Visibility.Visible;

            ProgressCalculations.Visibility = Visibility.Collapsed;

            initConditionTree();

            initFeatureTree(ATMMLFeatureTree);

            updateNavigation();

            _barCache = new BarCache(barChanged);

            //List<Alert> alerts = Alert.Manager.Alerts;
            //foreach (Alert alert in alerts)
            //{
            //    alert.FlashEvent += new FlashEventHandler(alert_FlashEvent);
            //}

            ResourceDictionary dictionary = new ResourceDictionary();
            dictionary.Source = new Uri("pack://application:,,,/ATMML;component/StyleDictionary.xaml");
            this.Resources.MergedDictionaries.Add(dictionary);

            Trade.Manager.TradeEvent += new TradeEventHandler(TradeEvent);
            Trade.Manager.NewPositions += new NewPositionEventHandler(new_Positions);
            _portfolio1.PortfolioChanged += new PortfolioChangedEventHandler(portfolio1Changed);
            _filterPortfolio.PortfolioChanged += _filterPortfolio_PortfolioChanged;

            _strategies = getStrategies();
            _selectedStrategy = (_strategies.Count == 0) ? "" : _strategies.First().Key;

            _initModels = true;

            hideNavigation();
            showView();

            _portfolioTimer = new DispatcherTimer();
            _portfolioTimer.Interval = TimeSpan.FromMilliseconds(1000);
            _portfolioTimer.Tick += new EventHandler(Timer_tick);
            _portfolioTimer.Start();
        }

        bool _initModels = false;

        public static string[] GetOutputList(string name = "", int level = 0)
        {
            string[] output = { };

            if (level == 0)
            {
                output = new string[] {
                    "PERCENT CHANGE"
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
                    lock (_barRequests)
                    {
                        string key = ticker + ":" + interval;
                        int index = _barRequests.IndexOf(key);
                        if (index >= 0)
                        {
                            _barRequests.RemoveAt(index);
                            getATMFactorInputs(ticker);
                            checkFactorComplete();
                        }
                    }

                    if (_compareToSymbolChange && ticker == _compareToSymbol)
                    {
                        _compareToSymbolChange = false;

                        Model model = getModel();
                        if (model != null)
                        {
                            loadCompareValues(model);
                            saveList<double>(model.Name + " CompareToValues", _compareToValues);
                            _update = 1;
                        }
                    }
                }
            }
        }

        Dictionary<string, Series> _factorInputs = new Dictionary<string, Series>();

        private bool isATMFactor(string name)
        {
            return (name.Contains("\u0002"));
        }

        private void getATMFactorInputs(string symbol)
        {
            Model model = getModel();
            if (model != null)
            {
                var factors = model.FactorNames;
                foreach (var factor in factors)
                {
                    if (isATMFactor(factor))
                    {
                        string conditionName = getConditionName(factor);
                        string interval = getConditionInterval(factor);
                        string[] intervalList = { interval, Study.getForecastInterval(interval, 1) };

                        string key = factor + ":" + symbol;
                        _factorInputs[key] = getSignals(symbol, conditionName, intervalList);
                    }
                }
            }
        }

        private bool _runModel = false;

        private Model getModel()
        {
            return _models.ContainsKey(_selectedModel) ? _models[_selectedModel] : null;
        }

        TextBlock _tb = new TextBlock();

        private void updateProgress(string modelName)
        {
            LoadingAnimation busy1 = new LoadingAnimation(30, 30);
 
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
                Predict.Content = "\uD83D\uDD52";
                Finish.Content = "\uD83D\uDD52";

            }
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
                Predict.Content = "\uD83D\uDD52";
                Finish.Content = "\uD83D\uDD52";
            }

            else if (_progressState == ProgressState.Training)
            {
                _tb.Text = (_progressTime > DateTime.Now) ? (_progressTime - DateTime.Now).ToString(@"hh\:mm\:ss") : "\u2713";
                _tb.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xcc, 0xff));

                Collect.Content = "\u2713";
                Process.Content = "\u2713";
                Predict.Content = _tb;
                Finish.Content = "\uD83D\uDD52";
            }
            else if (_progressState == ProgressState.Finished)
            {
                Collect.Content = "\u2713";
                Process.Content = "\u2713";
                Predict.Content = "\u2713";
                Finish.Content = "\u2713";

                _progressState = ProgressState.Complete;
                updateAutoResults(_selectedInterval, _selectedTicker);

                _update = 2;

            }
            else if (_progressState == ProgressState.Complete)
            {
                if (modelName == _selectedModel)
                {
                    ProgressCalculations.Visibility = Visibility.Collapsed;
                    UserFactorModelGrid.Visibility = Visibility.Visible;
                    TrainResults.Visibility = Visibility.Visible;
                }
            }
        }

        private ProgressState _progressState = ProgressState.Complete;
        private DateTime _progressTime = DateTime.Now;
        private int _progressTotalNumber = 0;
        private int _progressCompletedNumber = 0;

        private void checkFactorComplete()
        {
            if (_runModel && _totalBarRequest > 0)
            {
                int requestedCount = _totalBarRequest;
                int remainingCount = _barRequests.Count;
                int completedCount = requestedCount - remainingCount;

                _progressCompletedNumber = completedCount;

                bool outputsOk = requestedCount > 0 && remainingCount <= 0;

                Debug.WriteLine(" Symbols = " + completedCount + " of " + requestedCount);

                if (outputsOk)
                {
                    Thread thread = new Thread(new ThreadStart(trainModel));
                    thread.Start();
                }
            }
        }

        static ModelCalculator _mc = null;

        private void trainModel()
        {
            Debug.WriteLine("TRAINING");

            Model model = getModel();

            _totalBarRequest = 0;

            if (model != null)
            {
                trainModel(model);
            }

            _runModel = false;
            _totalBarRequest = 0;
            _progressState = ProgressState.Finished;

            _update = 6;
        }

        private List<string> getModelIntervals()
        {
            var model = getModel();

            var modelIntervals = (model != null) ? model.MLInterval.Split(';').Select(x => x.Trim()).ToList() : new List<string>();
            return modelIntervals;
        }

        private List<string> getDataIntervals()
        {
            var intervals = new List<string>();
            var model = getModel();
            if (model != null)
            {
                var modelIntervals = getModelIntervals();
                modelIntervals.Add(Study.getForecastInterval(modelIntervals[0], 1));

                var features = model.FeatureNames;
                for (int ii = 0; ii < features.Count; ii++)
                {
                    var items = features[ii].Split('\u0002');
                    if (items.Length == 2)
                    {
                        var featureIntervalName = items[1].Trim();
                        if (featureIntervalName.ToLower() == "short term")
                        {
                            intervals.AddRange(modelIntervals);

                        }
                        else if (featureIntervalName.ToLower() == "mid term")
                        {
                            intervals.AddRange(modelIntervals.Select(x => Study.getForecastInterval(x, 1)));
                        }
                    }
                }
                intervals = intervals.Distinct().ToList();
            }
            return intervals;
        }

        private void trainModel(Model model)
        {
            try
            {
                var tickers = model.Symbols.Select(x => x.Ticker).ToList();

                var maxBars = int.Parse(model.MLMaxBars);

                var intervals = getModelIntervals();

                _progressState = ProgressState.ProcessingData;
                _progressTotalNumber = tickers.Count * intervals.Count;
                _progressCompletedNumber = 0;

                model.Biases.Clear();

                foreach (var interval in intervals)
                {
                    if (!_runModel) break;

                    if (!model.UseTicker)
                    {
                        var data1 = new List<List<string>>();
                        foreach (var ticker in tickers)
                        {
                            if (!_runModel) break;
                            var thisTicker = new List<string>();
                            thisTicker.Add(ticker);
                            var modelData = atm.getModelData(model.FeatureNames, model.Scenario, _barCache, thisTicker, interval, default(DateTime), default(DateTime), maxBars, model.MLSplit, true, true);
                            data1.AddRange(modelData.data);
                            model.Biases[ticker + ":" + interval] = modelData.bias;
                            _progressCompletedNumber++;
                        }

                        var data2 = data1.OrderBy(sample => sample[2].Split(';')[1]).ToList();

                        var pathName1 = @"senarios\" + model.Name + @"\" + interval + @"\train.csv";
                        atm.saveModelData(pathName1, data2);
                    }
                    else
                    {
                        foreach (var ticker in tickers)
                        {
                            if (!_runModel) break;

                            var modelData = atm.getModelData(model.FeatureNames, model.Scenario, _barCache, (new string[] { ticker }).ToList(), interval, default(DateTime), default(DateTime), maxBars, model.MLSplit, true, false);
                            var data2 = modelData.data;
                            model.Biases[ticker + ":" + interval] = modelData.bias;
                            var pathName2 =   @"senarios\" + model.Name + @"\" + interval + @"\" + MainView.ToPath(ticker) + @"\train.csv";
                            atm.saveModelData(pathName2, data2);

                            _progressCompletedNumber++;
                        }
                    }
                }

                var seconds = int.Parse(model.MLMaxTime) * (intervals.Count * (model.UseTicker ? tickers.Count : 1));

                _progressState = ProgressState.Training;
                _progressTime = DateTime.Now + new TimeSpan(0, 0, seconds);

                foreach (var interval in intervals)
                {
                    if (!_runModel) break;

                    if (!model.UseTicker)
                    {
                        var pathName1 = @"senarios\" + model.Name + @"\" + interval;
                        MainView.AutoMLTrain(pathName1, MainView.GetSenarioLabel(model.Scenario), model.MLMaxTime, model.MLRank, model.MLSplit.Substring(0, 2), model.Trained);
                    }
                    else
                    {
                        foreach (var ticker in tickers)
                        {
                            if (!_runModel) break;
                            var pathName2 = @"senarios\" + model.Name + @"\" + interval + @"\" + MainView.ToPath(ticker);
                            MainView.AutoMLTrain(pathName2, MainView.GetSenarioLabel(model.Scenario), model.MLMaxTime, model.MLRank, model.MLSplit.Substring(0, 2), model.Trained);
                            //if (Application.Current != null) Application.Current.Dispatcher.Invoke(new Action(() => { updateProgress(); }));
                        }
                    }
                }

                if (!model.Trained)
                {
                    model.Trained = true;
                    saveModel(model.Name);
                }

                try
                {
                    if (Application.Current != null) Application.Current.Dispatcher.Invoke(new Action(() => { updateAutoResults(_selectedInterval, _selectedTicker); }));
                }
                catch (Exception x)
                {
                    System.Diagnostics.Debug.WriteLine(x);
                }

            }
            catch (Exception x)
            {
                //Trace.WriteLine("Train model exception: " + x.Message);
            }
        }

        private void updateAutoResults(string interval, string ticker = "")
        {
            var model = getModel();
            if (model != null)
            {
                var all = (_selectedInterval == "ML INTERVALS - BEST TRAINER");

                var results = "";
                var lines = new List<string>();

                var intervals = getModelIntervals();

                if (all)
                {
                    foreach (var interval1 in intervals)
                    {
                        var path = @"senarios\" + model.Name + @"\" + interval1 + ((model.UseTicker) ? @"\" + MainView.ToPath(ticker) : "");
                        results = MainView.LoadUserData(path + @"\result.csv");
                        var time = MainView.LoadUserData(path + @"\time");
                        var lastRun = MainView.LoadUserData(path + @"\lastRun");
                        if (results.Length > 0)
                        {
                            var rows = results.Split(':');
                            lines.Add((rows.Length > 0) ? rows[0] + "," + time + "," + lastRun : "");
                        }
                        else
                        {
                            lines.Add("");
                        }
                    }
                }
                else
                {
                    var path = @"senarios\" + model.Name + @"\" + interval + ((model.UseTicker) ? @"\" + MainView.ToPath(ticker) : "");
                    results = MainView.LoadUserData(path + @"\result.csv");
                    var time = MainView.LoadUserData(path + @"\time");
                    var lastRun = MainView.LoadUserData(path + @"\lastRun");
                    lines = results.Split(':').ToList();
                    for (var ii = 0; ii < lines.Count; ii++)
                    {
                        lines[ii] += "," + time + "," + lastRun;
                    }
                }

                header.Content = all ? "Interval" : "Rank";
                header2.Content = all ? "Interval" : "Rank";

                var labels = new Dictionary<string, List<Label>>();
                labels["row"] = new List<Label> { row, row1, row2, row3, row4, row5, row6, row7, row8, row9, row10, row11 };
                labels["date"] = new List<Label> { date, date1, date2, date3, date4, date5, date6, date7, date8, date9, date10, date11 };
                labels["bestDate"] = new List<Label> { bestDate, bestDate1, bestDate2, bestDate3, bestDate4, bestDate5, bestDate6, bestDate7, bestDate8, bestDate9, bestDate10, bestDate11 };
                labels["name"] = new List<Label> { name, name1, name2, name3, name4, name5, name6, name7, name8, name9, name10, name11 };
                labels["m1"] = new List<Label> { acc, acc1, acc2, acc3, acc4, acc5, acc6, acc7, acc8, acc9, acc10, acc11 };
                labels["m2"] = new List<Label> { auc, auc1, auc2, auc3, auc4, auc5, auc6, auc7, auc8, auc9, auc10, auc11 };
                labels["m3"] = new List<Label> { aup, aup1, aup2, aup3, aup4, aup5, aup6, aup7, aup8, aup9, aup10, aup11 };
                labels["m4"] = new List<Label> { f1, f11, f12, f13, f14, f15, f16, f17, f18, f19, f110, f111 };

                for (var row1 = 0; row1 < labels["name"].Count; row1++)
                {
                    labels["row"][row1].Content = "";
                    labels["date"][row1].Content = "";
                    labels["bestDate"][row1].Content = "";
                    labels["name"][row1].Content = "";
                    labels["m1"][row1].Content = "";
                    labels["m2"][row1].Content = "";
                    labels["m3"][row1].Content = "";
                    labels["m4"][row1].Content = "";
                }

                var brush1 = new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99));
                var brush2 = new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99));

                var idx = 0;
                lines.ForEach(line =>
                {
                    var items = line.Split(',');
                    if (idx < labels["name"].Count && items.Length >= 6)
                    {
                        labels["row"][idx].Content = all ? intervals[idx] : (idx + 1).ToString();
                        labels["date"][idx].Content = (items.Length >= 7) ? items[6] : ""; 
                        labels["bestDate"][idx].Content = items[5];
                        labels["name"][idx].Content = items[0];
                        labels["m1"][idx].Content = items[1];
                        labels["m2"][idx].Content = items[2];
                        labels["m3"][idx].Content = items[3];
                        labels["m4"][idx].Content = items[4];

                        var brush = (idx == 0 || all) ? brush2 : brush1;
                        labels["date"][idx].Foreground = brush; 
                        labels["bestDate"][idx].Foreground = brush;
                        labels["name"][idx].Foreground = brush;
                        labels["m1"][idx].Foreground = brush;
                        labels["m2"][idx].Foreground = brush;
                        labels["m3"][idx].Foreground = brush;
                        labels["m4"][idx].Foreground = brush;
                    }
                    else if (all)
                    {
                        labels["row"][idx].Content = intervals[idx];
                    }
                    idx++;
                });
            }
        }

        private string readModel(string name)
        {
            var path = @"senarios" + @"\" + name + @"\model";
            var output = MainView.LoadUserData(path);
            return output;
        }

        private void saveModel(string name)
        {
            if (_models.ContainsKey(name))
            {
                var newModel = _models[name];
                var oldModel = MainView.GetModel(name);

                var modelIsSameAsLastTrained = newModel.Equals(oldModel);

                if (!modelIsSameAsLastTrained)
                {
                    RemoveAllResults(newModel);
                }

                newModel.Trained = modelIsSameAsLastTrained;

                saveModel(newModel.Name, newModel);
            }
        }

        private void saveModel(string name, Model model)
        { 
            var path = @"senarios" + @"\" + name + @"\model";
            var data = model.save();
            MainView.SaveUserData(path, data);
        }

        private void DeleteMLModel_Mousedown(object sender, MouseButtonEventArgs e)
        {
            //saveModel();
            var model = getModel();
            var name = (model != null) ? model.Name : "";
            MessageBoxResult result = MessageBox.Show("Are you sure you wish to delete " + name + "?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    var path1 = MainView.GetDataFolder() + @"\senarios";
                    var path2 = path1 + @"\" + name;
                    var path3 = path1 + @"\" + name + "-save";
                    _models.Remove(name);
                    var modelNames = _models.Keys.ToList();
                    _selectedModel = (modelNames.Count > 0) ? modelNames[0] : "";
                    Directory.Delete(path2, true);
                    Directory.Delete(path3, true);
                    updateModelList(_selectedModel, modelNames, ModelList);
                    initializeCalculator(getModel());
                    model_to_ui();

                    if (name == "EXAMPLE")
                    {
                        Example.Visibility = Visibility.Visible;
                    }
                }
                catch
                {

                }
            }
        }

        private void loadMLModels()
        {
            var modelNames = MainView.getModelNames();
            if (modelNames.Count == 0)
            {
                _selectedModel = "";
            }
            modelNames.ForEach(x => loadModel(x));
            Example.Visibility = (modelNames.FindIndex(x => x == "EXAMPLE") == -1) ? Visibility.Visible : Visibility.Hidden;
            updateModelList(_selectedModel, modelNames, ModelList);
        }

        private void loadExampleModel()
        {
            var exampleModelName = "EXAMPLE";
            var model = new Model(exampleModelName);
            model.FeatureNames.AddRange(ATMDefaultFeatures.Split('\n'));
            model.Scenario = Senario.Close2AgoClose1Plus;
            model.UseTicker = false;
            saveModel(model.Name, model);
            loadMLModels();
         }

        private void loadModel(string name)
        {
            var path = @"senarios" + @"\" + name + @"\model";
            var data = MainView.LoadUserData(path);
            var model = Model.load(data);

            if (_selectedModel.Length == 0)
            {
                _selectedModel = name;
                initializeCalculator(model);
            }
            _models[name] = model;
        }

        string _selectedModel = "";

        private void updateModelList(string selectedModel, List<string> modelNames, ListBox input)
        {

            input.Items.Clear();
            modelNames.Sort();
            foreach (var name in modelNames)
            {
                var panel = new Grid();

                var col1 = new ColumnDefinition();
                col1.Width = new GridLength(1, GridUnitType.Star);

                panel.ColumnDefinitions.Add(col1);

                var tb = new TextBox();
                tb.Text = name;
                tb.Background = Brushes.Transparent;
                tb.Foreground = Brushes.White;
                tb.BorderBrush = Brushes.Transparent;
                tb.BorderThickness = new Thickness(0);
                tb.Height = 18;
                tb.PreviewMouseDown += Tb_PreviewMouseDown;
                tb.Padding = new Thickness(0, 2, 0, 2);
                tb.Margin = new Thickness(2, 0, 0, 0);
                tb.HorizontalAlignment = HorizontalAlignment.Left;
                tb.VerticalAlignment = VerticalAlignment.Center;
                tb.MouseEnter += Portfolio_MouseEnter;
                tb.MouseLeave += Portfolio_MouseLeave2;
                tb.FontFamily = new FontFamily("Helvetica Neue");
                tb.FontWeight = FontWeights.Normal;
                tb.FontSize = 11;
                tb.Cursor = Cursors.Hand;
                tb.Width = 110;
                tb.CharacterCasing = CharacterCasing.Upper;
                panel.Children.Add(tb);

                if (name == selectedModel)
                {
                    input.SelectedItem = panel;
                }

                input.Items.Add(panel);
            }
        }

        private void Tb_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            FrameworkElement fe = sender as FrameworkElement;
            ModelList.SelectedItem = fe.Parent;
        }

        private Dictionary<string, Dictionary<DateTime, double>> _factorScores = new Dictionary<string, Dictionary<DateTime, double>>();

        private string getConditionName(string input)
        {
            string name = input;
            if (name.Length > 0)
            {
                int index1 = 0;
                int index2 = input.IndexOf("\u0002");
                if (index1 >= 0)
                {
                    name = input.Substring(index1, index2 - index1);
                }
            }
            return name;
        }

        private string getConditionInterval(string input)
        {
            string output = _interval;
            int index1 = input.IndexOf("\u0002");
            if (index1 >= 0)
            {
                int index2 = input.Length;
            }
            return output;
        }

        private Series getSignals(string symbol, string conditionName, string[] intervalList)
        {
            Series output = null;

            bool ok = true;
            Dictionary<string, List<DateTime>> times = new Dictionary<string, List<DateTime>>();
            Dictionary<string, Series[]> bars = new Dictionary<string, Series[]>();
            Dictionary<string, Series[]> indexBars = new Dictionary<string, Series[]>();
            for (int ii = 0; ii < intervalList.Length; ii++)
            {
                string interval = intervalList[ii];
                times[interval] = (_barCache.GetTimes(symbol, interval, 0, BarServer.MaxBarCount));
                bars[interval] = _barCache.GetSeries(symbol, interval, new string[] { "Open", "High", "Low", "Close" }, 0, BarServer.MaxBarCount);
                if (ii == 0) indexBars[interval] = _barCache.GetSeries(symbol, interval, new string[] { "Close" }, 0, BarServer.MaxBarCount);
                if (times[interval] == null || times[interval].Count == 0)
                {
                    ok = false;
                    break;
                }
            }

            if (ok)
            {
                int barCount = times[intervalList[0]].Count;
                output = Conditions.Calculate(conditionName, symbol, intervalList, barCount, times, bars, _referenceData);
            }

            // sync
            if (output != null && intervalList[0] != _interval)
            {
                var time1 = times[intervalList[0]];
                var time2 = _barCache.GetTimes(symbol, _interval, 0, BarServer.MaxBarCount);
                output = sync(output, intervalList[0], _interval, time1, time2);
            }

            return output;
        }

        private Series sync(Series input, string interval1, string interval2, List<DateTime> times1, List<DateTime> times2)
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

                        if (input[idx] == 1)
                        {
                            output[idx2] = 1;
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

        void requestReferenceData(List<Symbol> symbols)
        {
            _referenceData.Clear();

            int count = symbols.Count;

            _referenceSymbols = new List<string>();
            for (int ii = 0; ii < count; ii++)
            {
                if (symbols[ii].Ticker.Contains(" Equity"))
                {
                    _referenceSymbols.Add(symbols[ii].Ticker);
                }
            }

            for (int jj = 0; jj < _referenceSymbols.Count; jj++)
            {
                string[] dataFieldNames = { "REL_INDEX" };
                _portfolio1.RequestReferenceData(_referenceSymbols[jj], dataFieldNames);
            }

            if (_referenceSymbols.Count == 0)
            {
                Model model = getModel();
                if (model != null)
                {
                    requestSymbolBars(model);
                }
            }
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

                        _portfolioRequested = "";

                    }
                }
            }
            else if (e.Type == PortfolioEventType.ReferenceData)
            {
                bool okToRequestIndexBars = false;

                foreach (KeyValuePair<string, object> kvp in e.ReferenceData)
                {
                    string name = kvp.Key;
                    object value = kvp.Value;
                    if (name == "REL_INDEX")
                    {
                        string key = e.Ticker + ":" + name;
                        _referenceData[key] = value;

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

                                if (indexSymbol.Length > 0)
                                {
                                    if (!indexSymbol.Contains(" Index"))
                                    {
                                        indexSymbol += " Index";
                                    }

                                    if (!_indexSymbols.Contains(indexSymbol))
                                    {
                                        _indexSymbols.Add(indexSymbol);
                                    }
                                }
                            }
                        }
                        requestIndexBars();
                    }
                }
            }
        }

        DateTime _fundamentalResponseTime = DateTime.Now;

        void setModelSymbols(string portfolioName)
        {
            var model = getModel();
            if (model != null)
            {
                if (model.Universe == portfolioName)
                {
                    model.Groups[0].AlphaSymbols = _portfolio1.GetSymbols();
                    saveList<Symbol>(model.Name + " Symbols", model.Symbols);
                }
                try
                {
                    if (Application.Current != null) Application.Current.Dispatcher.Invoke(new Action(() => { model_to_ui(); }));
                }
                catch (Exception x)
                {
                    System.Diagnostics.Debug.WriteLine(x);
                }
            }
        }

        int _indexBarRequestCount = 0;

        private void requestIndexBars()
        {
            Model model = getModel();
            if (model != null)
            {
                var indexIntervals = getDataIntervals();

                _indexBarRequestCount = _indexSymbols.Count * indexIntervals.Count;

                if (_indexBarRequestCount > 0)
                {
                    foreach (var interval in indexIntervals)
                    {
                        foreach (string symbol in _indexSymbols)
                        {
                            _barCache.RequestBars(symbol, interval, true);
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
            if (model != null)
            {
                if (_mc != null)
                {
                    _mc.ProgressEvent -= progressEvent;
                }
                _mc = ModelCalculator.GetModelCalculator(model);
                _mc.ProgressEvent += progressEvent;
                _runModel = _mc.ProgressState != ProgressState.Complete;
                if (_runModel)
                {;
                    TrainResults.Visibility = Visibility.Collapsed;

                    _tradeOverviewGroup = "Open";

                    ProgressCalculations.Visibility = Visibility.Visible;
                    UserFactorModelGrid.Visibility = Visibility.Visible;

                    _progressState = _mc.ProgressState;
                    _progressTotalNumber = _mc.ProgressTotalNumber;
                    _progressCompletedNumber = _mc.ProgressCompletedNumber;
                    _progressTime = _mc.ProgressTime;
                    var modelName = _mc.ModelName;

                    updateProgress(modelName);

                    update();
                }
                else
                {
                    ProgressCalculations.Visibility = Visibility.Collapsed;
                    UserFactorModelGrid.Visibility = Visibility.Visible;
                    TrainResults.Visibility = Visibility.Visible;
                }
            }
        }

        void runSelectedModel()
        {
            var model = getModel();

            if (model != null)
            {
                var features = model.FeatureNames;
                if (features.Count == 0)
                {
                    MessageBox.Show("Add your feature inputs.", "CAN'T TRAIN MODEL", MessageBoxButton.OK);
                }
                else
                {
                    TrainResults.Visibility = Visibility.Collapsed;

                    _tradeOverviewGroup = "Open";

                    UserFactorModelGrid.Visibility = Visibility.Visible;

                    ProgressCalculations.Visibility = Visibility.Visible;
                    TrainResults.Visibility = Visibility.Collapsed;

                    _progressState = ProgressState.CollectingData;
                    _progressTotalNumber = 0;
                    _progressCompletedNumber = 0;

                    var modelName = _mc.ModelName;
                    updateProgress(modelName);

                    ProgressCalculations.Visibility = Visibility.Visible;
                    UserFactorModelGrid.Visibility = Visibility.Visible;

                    _runModel = true;

                    _mc.Run();
                }
            }
        }

        private void progressEvent(object sender, EventArgs e)
        {
            try {
                _progressState = _mc.ProgressState;
                _progressCompletedNumber = _mc.ProgressCompletedNumber;
                _progressTotalNumber = _mc.ProgressTotalNumber;
                _progressTime = _mc.ProgressTime;
                var modelName = _mc.ModelName;
                if (Application.Current != null) Application.Current.Dispatcher.Invoke(new Action(() => { updateProgress(modelName); }));
            }
            catch (Exception x)
            {
                System.Diagnostics.Debug.WriteLine(x);
            }
        }

        private List<ScanSummary> _scanSummary = null;

        int _totalBarRequest = 0;
        List<string> _barRequests = new List<string>();

        void requestSymbolBars(Model model)
        {
            lock (_allTrades)
            {
                _allTrades.Clear();
            }

            var intervals = getDataIntervals();

            lock (_barRequests)
            {
                _totalBarRequest = 0;
                _barRequests.Clear();

                string benchMarkSymbol = model.Benchmark;
                foreach (var interval in intervals)
                {
                    var key1 = benchMarkSymbol + ":" + interval;
                    if (!_barRequests.Contains(key1))
                    {
                        _barRequests.Add(key1);
                        _barCache.RequestBars(benchMarkSymbol, interval);
                    }

                    var key2 = _compareToSymbol + ":" + interval;
                    if (!_barRequests.Contains(key2))
                    {
                        _barRequests.Add(key2);
                        _barCache.RequestBars(_compareToSymbol, interval);
                    }
                }

                foreach (var symbol in model.Symbols)
                {
                    var ticker = symbol.Ticker;
                    foreach (var interval in intervals)
                    {
                        var key = ticker + ":" + interval;
                        if (!_barRequests.Contains(key))
                        {
                            _barRequests.Add(key);
                            _barCache.RequestBars(ticker, interval, false);
                        }
                    }
                }
                _totalBarRequest = _barRequests.Count;
                _progressTotalNumber = _totalBarRequest;
            }
        }

        private void setInfo()
        {
            string view = (_view == "Sector Positions") ? "Sector Breakdown" : _view;


            string info =
                view + ";" +
                "" + ";" +
                _symbol + ";" +
                _tradeOverviewGroup + ";" +
                _monthlySummaryYear.ToString() + ";" +
                _monthlySummaryMonth.ToString() + ";" +
                _selectedSector + ";" +
                _level2 + ";" +
                _level3 + ";" +
                _level4 + ";" +
                _level5 + ";" +
                " " + ";" +
                " " + ";" +
                _useUserFactorModel + ";" +
                _level1 + ";" +
                _level6 + ";";

            _mainView.SetInfo("AutoML", info);
        }

        void Timer_tick(object sender, EventArgs e)
        {
            if (_initModels)
            {
                _initModels = false;
                loadModel();
                var model = getModel();
                requestPortfolio((model != null) ? model.Universe : "");
                initializeCalculator(model);
            }

            if (_update != 0 && _progressState == ProgressState.Complete)
            {
                update();
                _update = 0;
            }

            if (_addFlash)
            {
                _addFlash = false;
                Brush alertColor = Brushes.Red;
                Flash.Manager.AddFlash(new Flash(AlertButton, AlertButton.Foreground, alertColor, 10, false));
            }
        }

        private void update()
        {
            try
            {
                updateNavigation();
                getPositions();
                updateMonthSummary();
            }
            catch (Exception ex)
            {
                string msg = ex.Message;
            }
        }

        private void updateNavigation()
        {
            BrushConverter bc = new BrushConverter();
            Brush grayBrush = (Brush)bc.ConvertFrom("#cccccc");
            Brush blueBrush = (Brush)bc.ConvertFrom("#00ccff");
            Brush whiteBrush = (Brush)bc.ConvertFrom("#ffffff");

            bool enableMonthlySummary = (getPortfolioName() == "US 100") && MainView.EnableEditTrades;

            string portfolioName = getPortfolioName();

            Visibility sectorVisiblilty = (

                portfolioName != "SINGLE CTY ETF" &&
                portfolioName != "GLOBAL INDICES" &&
                portfolioName != "GLOBAL MACRO" &&
                portfolioName != "GLOBAL 10YR" &&
                portfolioName != "SPOT FX" &&
                //portfolioName != "TIM" &&
                //portfolioName != "TIM2" && 
                portfolioName != "CQG COMMODITIES >" &&
                portfolioName != "CQG EQUITIES >" &&
                portfolioName != "CQG ETF >" &&
                portfolioName != "CQG FX & CRYPTO >" &&
                portfolioName != "CQG INTEREST RATES >" && 
                portfolioName != "CQG STOCK INDICES >" &&
                portfolioName != "CQG EQUITY SPREADS" &&
                portfolioName != "COMMODITIES") ? Visibility.Visible : Visibility.Collapsed;
        }

        private void MarketMaps_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Close();
            _mainView.Content = new MarketMonitor(_mainView);
        }

        private void Server_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Close();
            _mainView.Content = new Timing(_mainView);
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

        private void showView()
        {
            if (_useUserFactorModel)
            {
                UserFactorModelGrid.Visibility = Visibility.Visible;
            }
            else
            {
                UserFactorModelGrid.Visibility = Visibility.Collapsed;
            }

            hideNavigation();
        }

        private void hideView()
        {
            UserFactorModelGrid.Visibility = Visibility.Visible;
        }

        private void hideNavigation()
        {
            Menus.Visibility = Visibility.Collapsed;

            Level1Menu.Visibility = Visibility.Collapsed;
            BenchmarkMenu1.Visibility = Visibility.Collapsed;
            Level2Menu.Visibility = Visibility.Collapsed;
            Level3Menu1.Visibility = Visibility.Collapsed;
            GroupMenu2.Visibility = Visibility.Collapsed;
            Level5MenuScroller1.Visibility = Visibility.Collapsed;
            Level6MenuScroller1.Visibility = Visibility.Collapsed;
            IndustryMenuScroller2.Visibility = Visibility.Collapsed;
            Level5Menu1.Visibility = Visibility.Collapsed;
            IndustryMenu2.Visibility = Visibility.Collapsed;

            Level1Menu.Children.Clear();
            BenchmarkMenu1.Children.Clear();
            Level2Menu.Children.Clear();
            Level3Menu1.Children.Clear();
            GroupMenu2.Children.Clear();
            Level4Menu1.Children.Clear();
            SubgroupMenu2.Children.Clear();
            Level5Menu1.Children.Clear();
            IndustryMenu2.Children.Clear();
            Level4ActionMenu1.Visibility = Visibility.Collapsed;
            SubgroupActionMenu2.Visibility = Visibility.Collapsed;

            MLRankMenu.Visibility = Visibility.Collapsed;
            MLIterationMenu.Visibility = Visibility.Collapsed;
            MLIntervalsMenu.Visibility = Visibility.Collapsed;
            MLDataSplitMenu.Visibility = Visibility.Collapsed;
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

            hideNavigation();

            showView();

            update(_level1, "", "", "", "", "");
            setUniverse(getPortfolioName());
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

        private void Level1Menu_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Level2Menu.Children.Clear();
            Level3Menu1.Children.Clear();
            GroupMenu2.Children.Clear();
            Level4Menu1.Children.Clear();
            SubgroupMenu2.Children.Clear();
            Level5Menu1.Children.Clear();
            IndustryMenu2.Children.Clear();
            Level4ActionMenu1.Visibility = Visibility.Collapsed;
            SubgroupActionMenu2.Visibility = Visibility.Collapsed;

            Label label = e.Source as Label;
            string region = label.Content as string;

            _level1 = region;
            _level2 = "";
            _level3 = "";
            _level4 = "";
            _level5 = "";
            _level6 = "";

            highlightButton(Level1Menu, _level1);

            nav.setNavigationLevel1(region, Level2Menu, Level2_MouseDown, go_Click);
        }

        private void Level2_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Level3Menu1.Children.Clear();
            GroupMenu2.Children.Clear();
            Level4Menu1.Children.Clear();
            SubgroupMenu2.Children.Clear();
            Level5Menu1.Children.Clear();
            IndustryMenu2.Children.Clear();
            Level4ActionMenu1.Visibility = Visibility.Collapsed;
            SubgroupActionMenu2.Visibility = Visibility.Collapsed;

            SymbolLabel label = e.Source as SymbolLabel;
            string level2 = (label.Symbol == (string)label.Content) ? label.Symbol : label.Symbol + ":" + label.Content;
            if (level2.Length == 0)
            {
                level2 = label.Content as string;
            }

            _level2 = level2;
            _level3 = "";
            _level4 = "";
            _level5 = "";
            _level6 = "";

            highlightButton(Level2Menu, _level2);

            if (level2 == "ADD CUSTOM UNIVERSE")
            {
            }
            else if (nav.setNavigationLevel2(_level2, Level3Menu1, Level3Menu1_MouseDown, go_Click))
            {
                highlightButton(Level3Menu1, _level3);
            }
            else
            {
                var ticker = label.Ticker;
                Scroller5.Visibility = Visibility.Visible;
                _memberIndex = ticker.Contains('>') ? "" : ticker;
                _level3 = label.Symbol;
                var name = getPortfolioName();
                requestMembers(name, Level3Menu1);
            }

            highlightButton(Level2Menu, _level2);
        }

        private void setUniverse(string universe)
        {
            if (_useUserFactorModel)
            {
                var model = getModel();
                if (model != null)
                {
                    model.Universe = universe;
                    UserFactorModelUniverse.Content = universe;
                }
            }
        }

        private void Level3Menu1_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Level4Menu1.Children.Clear();

            SymbolLabel label = e.Source as SymbolLabel;
            string level3 = (label.Symbol == (string)label.Content) ? label.Symbol : label.Symbol + ":" + label.Content;

            _level3 = level3;
            _level4 = "";
            _level5 = "";
            _level6 = "";

            highlightButton(Level3Menu1, level3);

            if (nav.setNavigationLevel3(_level2, level3, Level4Menu1, Level4Menu1_MouseDown))
            {
                Level4ActionMenu1.Visibility = Visibility.Visible;
                highlightButton(Level4Menu1, _level4);
            }
            else
            {
                var ticker = label.Ticker;
                Level4MenuScroller1.Visibility = Visibility.Visible;
                _memberIndex = ticker;
                _level4 = label.Symbol;
                var name = getPortfolioName();
                requestMembers(name, Level4Menu1);
            }
        }

        private void Level4Menu1_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Level5Menu1.Children.Clear();

            SymbolLabel label = e.Source as SymbolLabel;
            string level4 = (label.Symbol == (string)label.Content) ? label.Symbol : label.Symbol + ":" + label.Content;

            _level4 = level4;
            _level5 = "";
            _level6 = "";

            highlightButton(Level4Menu1, level4);

            if (nav.setNavigationLevel4(_level2, _level3, level4, Level5Menu1, Level5Menu_MouseDown))
            {
                Level5Menu1.Visibility = Visibility.Visible;
                highlightButton(Level5Menu1, _level5);
            }
            else
            {
                var ticker = label.Ticker;
                Level5MenuScroller1.Visibility = Visibility.Visible;
                _memberIndex = ticker;
                _level5 = label.Symbol;
                var name = getPortfolioName();
                requestMembers(name, Level5Menu1);
            }
        }

        private StackPanel _memberPanel = null;
        private string _memberIndex = "";

        private void requestMembers(string ticker, StackPanel panel)
        {
            _memberPanel = panel;
            requestPortfolio(ticker);
        }

        private void loadMemberPanel()
        {
            var members = _portfolio1.GetSymbols();

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

            setCheckBoxes(getModel());
        }

        private void Member_MouseDown(object sender, MouseButtonEventArgs e)
        {
        }

        private void SubgroupAction_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Label label = sender as Label;

            string[] field = _level3.Split(':');
            string group = field[0];
            string symbol = group + " Index";

            if (label.Name == "SubgroupChart1" || label.Name == "SubgroupChart2")
            {
            }
        }

        private void Level5Menu_MouseDown(object sender, MouseButtonEventArgs e)
        {
            SymbolLabel label = e.Source as SymbolLabel;
            string level5 = (label.Symbol == (string)label.Content) ? label.Symbol : label.Symbol + ":" + label.Content;
            _level6 = "";


            highlightButton(Level5Menu1, level5);

            if (nav.setNavigationLevel5(_level2, _level3, _level4, Level6Menu, Level6Menu_MouseDown))
            {
                Level6Menu.Visibility = Visibility.Visible;
                highlightButton(Level6Menu, _level6);
            }
            else
            {
                var ticker = label.Ticker;
                Level6MenuScroller1.Visibility = Visibility.Visible;
                _memberIndex = ticker;
                _level6 = label.Symbol;
                var name = getPortfolioName();
                requestMembers(name, Level6Menu);
            }
        }

        private void Level6Menu_MouseDown(object sender, MouseButtonEventArgs e)
        {
            SymbolLabel label = e.Source as SymbolLabel;
            string level6 = (label.Symbol == (string)label.Content) ? label.Symbol : label.Symbol + ":" + label.Content;

            highlightButton(Level6Menu, level6);

            _clientPortfolioName = "";
            update(_level1, _level2, _level3, _level4, _level5, level6);
            setUniverse(getPortfolioName());
        }

        private void update(string level1, string level2, string level3, string level4, string level5, string level6)
        {
            _level1 = level1;
            _level2 = level2;
            _level3 = level3;
            _level4 = level4;
            _level5 = level5;
            _level6 = level6;

            hideNavigation();
            showView();
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
                string name = "";
                if (_level6 != "" && _level6 != "MEMBERS") name = _level6;
                else if (_level5 != "" && _level5 != "MEMBERS") name = _level5;
                else if (_level4 != "" && _level4 != "MEMBERS") name = _level4;
                else if (_level3 != "" && _level3 != "MEMBERS") name = _level3;
                else name = _level2;

                string[] field = name.Split(':');
                portfolioName = field[0].Replace(">", "").Trim();
            }
            return portfolioName;
        }

        // name of built in portfolio
        // name of index to ask bbg for
        // single index name
        // prtu and eqs portfolios
        // list of tickers
        private void requestPortfolio(string name)
        {
            Portfolio.PortfolioType type = Portfolio.PortfolioType.Index;

            string[] fields = name.Split('&');

            if (Portfolio.IsBuiltInPortfolio(name))
            {
                type = Portfolio.PortfolioType.BuiltIn;
            }
            else if (name.Contains(" Index") || name.Contains(" Equity") || name.Contains(" Comdty") || name.Contains(" Curncy") || Portfolio.isCQGSymbol(fields[0]))
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

            _portfolioRequested = name;

            if (type == Portfolio.PortfolioType.Single)
            {
                var model = getModel();
                if (model != null)
                {
                    model.Universe = "Custom";
                    var symbols = new List<Symbol>();
                    for (int ii = 0; ii < fields.Length; ii++)
                    {
                        symbols.Add(new Symbol(fields[ii].Trim()));
                    }
                    model.Groups[0].AlphaSymbols = symbols;
                    saveList<Symbol>(model.Name + " Symbols", model.Symbols);
                    model_to_ui();
                }
            }
            else if (name != "Custom")
            {
                _portfolioRequestedCount = fields.Length;
                foreach (var field in fields)
                {
                    _portfolio1.RequestSymbols(field.Trim(), type, true);
                }
            }
            else
            {
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

        private void StopPrediction_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _runModel = false;
            _totalBarRequest = 0;

            _portfolioValues.Clear();
            _portfolioTimes.Clear();
            _benchmarkValues.Clear();
            _compareToValues.Clear();
            _portfolioMonthTimes.Clear();
            _portfolioMonthValues.Clear();
            _update = 12;

            _factorInputs.Clear();
            _barCache.Clear();

           var modelName = _mc.ModelName;
            _mc.Stop();

            _progressState = ProgressState.Finished;
            updateProgress(modelName);
            ProgressCalculations.Visibility = Visibility.Collapsed;

            loadModel(modelName + "-save"); // resore original model
            model_to_ui();
            ProgressCalculations.Visibility = Visibility.Collapsed;
            UserFactorModelGrid.Visibility = Visibility.Visible;
            TrainResults.Visibility = Visibility.Visible;

        }

        private void RemoveAllResults(Model model)
        {
            var intervals = Model.MLIntervals;
            var tickers = model.Symbols.Select(x => x.Ticker).ToList();

            var baseDir = MainView.GetDataFolder();

            foreach (var interval in intervals)
            {
                if (!model.UseTicker)
                {
                    var pathName1 = baseDir + @"\" + @"senarios\" + model.Name + @"\" + interval;
                    try
                    {
                        Directory.Delete(pathName1, true);
                    }
                    catch (Exception)
                    {

                    }
                }
                else
                {
                    foreach (var ticker in tickers)
                    {
                        var pathName2 = @"senarios\" + model.Name + @"\" + interval + @"\" + MainView.ToPath(ticker);
                        Directory.Delete(pathName2, true);
                    }
                }
            }
        }

        private void RunMLModel_Mousedown(object sender, MouseButtonEventArgs e)
        {
            ui_to_model();
            model_to_ui();

            var model = getModel();

            if (model != null)
            {
                //saveModel(model.Name);

                saveModel(model.Name + "-save", model); // save original model

                runSelectedModel();

                update();
            }
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
        List<double> _benchmarkValues = new List<double>();
        List<DateTime> _portfolioMonthTimes = new List<DateTime>();
        List<double> _portfolioMonthValues = new List<double>();

        List<double> _compareToValues = new List<double>();
        string _compareToSymbol = "";

        private void loadCompareValues(Model model)
        {
            string interval = "ML INTERVALS - BEST TRAINER";

            _compareToValues.Clear();

            Dictionary<string, List<Bar>> bars = new Dictionary<string, List<Bar>>();
            bars[_compareToSymbol] = getBars(_compareToSymbol, interval, BarServer.MaxBarCount);

            List<DateTime> times = bars[_compareToSymbol].Select(x => x.Time).ToList();

            DateTime time1 = model.TestingRange.Time1;
            DateTime time2 = model.TestingRange.Time2;

            double compareToPrice1 = double.NaN;
            for (int ii = 0; ii < bars[_compareToSymbol].Count; ii++)
            {
                DateTime time = times[ii];
                if (time >= time1 && time <= time2)
                {

                    double value = double.NaN;
                    if (bars.ContainsKey(_compareToSymbol))
                    {
                        double close = getTradePrice(model, bars[_compareToSymbol], ii);
                        if (!double.IsNaN(close))
                        {
                            if (double.IsNaN(compareToPrice1))
                            {
                                compareToPrice1 = close;
                            }

                            if (!double.IsNaN(compareToPrice1))
                            {
                                double price2 = close;
                                value = 100 * (price2 - compareToPrice1) / compareToPrice1;
                            }
                        }
                    }
                    _compareToValues.Add(value);
                }
            }
        }

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
            foreach(var line in lines) {
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
                    output.Add(symbol);
                }
            }
            return output;
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

        private string getInterval(Model model)
        {
            string output = "Daily";

            string period = model.RankingInterval;
            if (period == "Weekly")
            {
                output = "Weekly";
            }
            else if (period == "Monthly")
            {
                output = "Monthly";
            }
            else if (period == "Quarterly")
            {
                output = "Quarterly";
            }
            else if (period == "Semi Annual")
            {
                output = "SemiAnnually";
            }
            else if (period == "Annually")
            {
                output = "Yearly";
            }
            return output;
        }

        private Dictionary<string, Dictionary<string, Dictionary<DateTime, double>>> _predictionData = new Dictionary<string, Dictionary<string, Dictionary<DateTime, double>>>();

        Dictionary<string, Dictionary<string, Dictionary<string, double>>> _scoreCache = new Dictionary<string, Dictionary<string, Dictionary<string, double>>>();

        private void clearData(string name, Model model)
        {
            try
            {
                string path = MainView.GetDataFolder() + @"\models";
                path += @"\" + name;
                path += _useUserFactorModel ? @"\User" : @"\ML";
                path += @"\" + model.Name;
                File.Delete(path);
            }
            catch (Exception x)
            {
                //System.Diagnostics.Debug.WriteLine("Clear scores error: " + x.Message);
            }
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
            double price = Trade.Manager.GetMarkToMarketPrice("SPX Index", year, month);
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
                            double profit = 100 * (exitPrice - entryPrice) / entryPrice; // (multiplier * trade.Size * (exitPrice - entryPrice));

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

        public static void  Shutdown()
        {
            if (_mc != null)
            {
                _mc.Close();
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
            _portfolio2.Close();
            _filterPortfolio.Close();
            _barCache.Clear();

            setInfo();
        }

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
                string text =
                    _portfolioBalance.ToString() + "," +
                    _yearToDatePercent + "," +
                    _yearToDateDollar + "," +
                    _compoundedReturn + "," +
                    _portfolioAlpha + "," +
                    _monthlyReturnPercent + "," +
                    _monthlyReturnDollar + "," +
                    _monthlyAlpha + "," +
                    _cash + "," +
                    _longs + "," +
                    _shorts;

                return text;
            }
        }

        private void Portfolio_MouseEnter(object sender, MouseEventArgs e)
        {
            Control label = sender as Control;
            label.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xcc, 0xff));
        }

        private void FundamentalML_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _mainView.Content = new PortfolioBuilder(_mainView);
        }

        private void LandingPage_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _mainView.Content = new LandingPage(_mainView);
        }

        private void Portfolio_MouseLeave2(object sender, MouseEventArgs e)
        {
            Control label = sender as Control;
            label.Foreground = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));
        }

        bool _useUserFactorModel;
        bool _useAzure = true;

        private void updateScenario(Senario scenario)
        {
            var refText = MainView.GetSenarioLabel(scenario);
            var refTextFields = refText.Split('|');
            if (refTextFields.Length > 1)
            {
                MLScenario.Visibility = Visibility.Visible;
                VolScenario.Visibility = Visibility.Collapsed;
                RefText2.Content = refTextFields[0];
                RefText1.Content = refTextFields[1];
            }
            else
            {
                MLScenario.Visibility = Visibility.Collapsed;
                VolScenario.Visibility = Visibility.Visible;
                VolText.Content = refTextFields[0];
            }
        }

        private void model_to_ui()
        {
            var model = getModel();
            if (model != null)
            {
                var name = model.Name;

                var senario = model.Scenario;

                updateScenario(senario);
 
                MainView.SelectedMLModel = model;
                ModelName.Content = MainView.SelectedMLModel.Name;
                ModelName1.Content = MainView.SelectedMLModel.Name;

                MaxTimeAmt.Text = model.MLMaxTime.Trim();
                MaxBars.Text = model.MLMaxBars.Trim();
                MLSplit.Content = model.MLSplit.Trim();
                MLRank.Content = model.MLRank;
                MLIntervalButton.Content = model.MLInterval.Replace(";", ",");
                TickerList.Visibility = model.UseTicker ? Visibility.Visible : Visibility.Collapsed;
                GroupLabel.Visibility = !model.UseTicker ? Visibility.Visible : Visibility.Collapsed;

                BinaryClass.Visibility = isRegressionModel(model) ? Visibility.Collapsed : Visibility.Visible;
                Regression.Visibility = isRegressionModel(model) ? Visibility.Visible : Visibility.Collapsed;

                string text1 = "";
                string text2 = "";
                bool first = true;
                foreach (var factor in model.FeatureNames)
                {
                    var fields = factor.Split('\u0002');
                    if (!first) { text1 += "\n"; text2 += "\n"; }
                    text1 += fields[0]; 
                    if (fields.Length >= 2) text2 += fields[1];
                    first = false;
                }
                FeatureEditor1.Text = text1;
                FeatureEditor2.Text = text2;

                UserFactorModelUniverse.Content = model.Universe;

                UserFactorModelRebalance.Content = _selectedInterval;

                _compareToSymbol = model.Benchmark;
                _compareToSymbolChange = true;

                TickerList.Items.Clear();
                var symbols = loadList<string>(model.Name + " Symbols");
                var select = true;
                foreach (var symbol in symbols)
                {
                    var fields = symbol.Split('\t');
                    if (fields.Length > 0)
                    {
                        var ticker = fields[0];

                        var tickerFields = ticker.Split(' ');
                        var label = new Label();

                        label.Tag = ticker;
                        label.Content = tickerFields[0];
                        label.VerticalContentAlignment = VerticalAlignment.Center;
                        label.Foreground = Brushes.White;
                        label.MouseEnter += Portfolio_MouseEnter;
                        label.MouseLeave += Portfolio_MouseLeave2;
                        label.Cursor = Cursors.Hand;
                        label.Height = 18;
                        label.FontSize = 11;
                        label.Margin = new Thickness(2, 0, 0, 0);
                        label.Padding = new Thickness(2);
                        label.ToolTip = fields[1];

                        TickerList.Items.Add(label);
                        if (select)
                        {
                            select = false;
                            TickerList.SelectedItem = label;
                        }
                    }
                }
                updateAutoResults(_selectedInterval, _selectedTicker);
                SelectedTicker.Content = model.UseTicker ? _selectedTicker : "";
            }
            else
            {
                FeatureEditor1.Text = "";
                FeatureEditor2.Text = "";
                TickerList.Items.Clear();
            }
        }

        string _selectedTicker = "";
        string _selectedInterval = "ML INTERVALS - BEST TRAINER";

        private void ui_to_model()
        {
            var model = getModel();
            if (model != null)
            {
                model.Universe = UserFactorModelUniverse.Content as string;
                _selectedInterval = UserFactorModelRebalance.Content as string;
                model.MLMaxTime = MaxTimeAmt.Text.Trim();
                model.MLMaxBars = MaxBars.Text.Trim();
                model.MLSplit = MLSplit.Content as string;
                model.MLRank = MLRank.Content as string;
                model.MLInterval = (MLIntervalButton.Content as string).Replace(',', ';');

                if (VolScenario.Visibility == Visibility.Visible)
                {
                    var refText = VolText.Content as string;
                    model.Scenario = MainView.GetSenarioFromLabel(refText);
                }
                else
                {
                    var t1 = RefText1.Content.ToString();
                    var t2 = RefText2.Content.ToString();
                    var refText = t2 + ((t1.Length > 0) ? "|" + t1 : "");
                    model.Scenario = MainView.GetSenarioFromLabel(refText);
                }

                //model.UseTicker = (StockTrain.IsChecked == true);

                var text1 = FeatureEditor1.Text;
                var text2 = FeatureEditor2.Text;
                string[] lines1 = text1.Split('\n');
                string[] lines2 = text2.Split('\n');
                model.FeatureNames.Clear();
                var cnt = Math.Min(lines1.Length, lines2.Length);
                for (var ii = 0; ii < cnt; ii++)
                {
                    if (lines1[ii].Length > 0)
                    {
                        model.FeatureNames.Add(lines1[ii] + "\u0002" + lines2[ii]);
                    }
                }
            }
        }

        private void Iterations_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Menus.Visibility = Visibility.Visible;
            ATMFeatures.Visibility = Visibility.Collapsed;
            MLIntervalsMenu.Visibility = Visibility.Collapsed;
            MLDataSplitMenu.Visibility = Visibility.Collapsed;
            ScenarioMenu1.Visibility = Visibility.Collapsed;

            Level1Menu.Visibility = Visibility.Collapsed;
            BenchmarkMenu1.Visibility = Visibility.Collapsed;

            RankingMenu.Visibility = Visibility.Collapsed;
            RebalanceMenu.Visibility = Visibility.Collapsed;

            MLRankMenu.Visibility = Visibility.Collapsed;
            MLIterationMenu.Visibility = Visibility.Visible;

            Level2Menu.Visibility = Visibility.Collapsed;
            Level3Menu1.Visibility = Visibility.Collapsed;
            Level4Menu1.Visibility = Visibility.Collapsed;
            Level4MenuScroller1.Visibility = Visibility.Collapsed;
            Level5Menu1.Visibility = Visibility.Collapsed;
            Level5MenuScroller1.Visibility = Visibility.Collapsed;
            Level6MenuScroller1.Visibility = Visibility.Collapsed;
            Level4ActionMenu1.Visibility = Visibility.Collapsed;
            SubgroupActionMenu2.Visibility = Visibility.Collapsed;

            ProgressCalculations.Visibility = Visibility.Collapsed;
            TrainResults.Visibility = Visibility.Collapsed;

            FilterMenu.Visibility = Visibility.Collapsed;
            InputMenu.Visibility = Visibility.Collapsed;
            FeaturesMenu.Visibility = Visibility.Collapsed;
            PortfolioSettings.Visibility = Visibility.Collapsed;

            nav.setNavigation(MLIterationMenu, Iterations_MouseDown, Model.MLITERATIONS);
            addSaveAndCloseButtons(MLIterationMenu, Save_Click);

            nav.UseCheckBoxes = false;
        }

        private void AddFeatures_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var model = getModel();

            if (model != null)
            {

                if (BenchmarkMenu1.Visibility == Visibility.Collapsed)
                {
                    hideView();
                    hideNavigation();

                    Menus.Visibility = Visibility.Visible;

                    Level1Menu.Visibility = Visibility.Collapsed;
                    BenchmarkMenu1.Visibility = Visibility.Collapsed;
                    ScenarioMenu1.Visibility = Visibility.Collapsed;
                    ScenarioMenu2.Visibility = Visibility.Collapsed;
                    ScenarioMenu3.Visibility = Visibility.Collapsed;
                    ScenarioMenu4.Visibility = Visibility.Collapsed;

                    RankingMenu.Visibility = Visibility.Collapsed;
                    ATMFeatures.Visibility = Visibility.Collapsed;
                    MLIntervalsMenu.Visibility = Visibility.Collapsed;
                    MLDataSplitMenu.Visibility = Visibility.Collapsed;
                    RebalanceMenu.Visibility = Visibility.Collapsed;

                    Level2Menu.Visibility = Visibility.Collapsed;
                    Level3Menu1.Visibility = Visibility.Collapsed;
                    Level4Menu1.Visibility = Visibility.Collapsed;
                    Level4MenuScroller1.Visibility = Visibility.Collapsed;
                    Level5Menu1.Visibility = Visibility.Collapsed;
                    Level5MenuScroller1.Visibility = Visibility.Collapsed;
                    Level6MenuScroller1.Visibility = Visibility.Collapsed;
                    Level4ActionMenu1.Visibility = Visibility.Collapsed;
                    SubgroupActionMenu2.Visibility = Visibility.Collapsed;
                    MLRankMenu.Visibility = Visibility.Collapsed;
                    MLIterationMenu.Visibility = Visibility.Collapsed;

                    FilterMenu.Visibility = Visibility.Collapsed;
                    FeaturesMenu.Visibility = Visibility.Visible;
                    PortfolioSettings.Visibility = Visibility.Collapsed;

                    nav.UseCheckBoxes = false;

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

                string text1 = FeatureEditor1.Text;
                string text2 = FeatureEditor2.Text;
                string[] lines1 = text1.Split('\n');
                string[] lines2 = text2.Split('\n');
                var cnt = Math.Min(lines1.Length, lines2.Length);
                var text = "";
                for (var ii = 0; ii < cnt; ii++)
                {
                    if (text.Length > 0) text += "\n";
                    text += lines1[ii] + "\u0002" + lines2[ii];
                }
                setFeatures(text);
                ATMMLFeatureTree.Items.Refresh();
                ATMMLFeatureTree.UpdateLayout();
            }
        }

        private void MLRanking_MouseDown(object sender, MouseButtonEventArgs e)
        {

            nav.UseCheckBoxes = false;

            Menus.Visibility = Visibility.Visible;
            ATMFeatures.Visibility = Visibility.Collapsed;
            MLIntervalsMenu.Visibility = Visibility.Collapsed;
            MLDataSplitMenu.Visibility = Visibility.Collapsed;
            ScenarioMenu1.Visibility = Visibility.Collapsed;
            ScenarioMenu2.Visibility = Visibility.Collapsed;
            ScenarioMenu3.Visibility = Visibility.Collapsed;
            ScenarioMenu4.Visibility = Visibility.Collapsed;

            Level1Menu.Visibility = Visibility.Collapsed;
            BenchmarkMenu1.Visibility = Visibility.Collapsed;

            RankingMenu.Visibility = Visibility.Collapsed;
            RebalanceMenu.Visibility = Visibility.Collapsed;

            MLRankMenu.Visibility = Visibility.Visible;
            MLIterationMenu.Visibility = Visibility.Collapsed;

            Level2Menu.Visibility = Visibility.Collapsed;
            Level3Menu1.Visibility = Visibility.Collapsed;
            Level4Menu1.Visibility = Visibility.Collapsed;
            Level4MenuScroller1.Visibility = Visibility.Collapsed;
            Level5Menu1.Visibility = Visibility.Collapsed;
            Level5MenuScroller1.Visibility = Visibility.Collapsed;
            Level6MenuScroller1.Visibility = Visibility.Collapsed;
            Level4ActionMenu1.Visibility = Visibility.Collapsed;
            SubgroupActionMenu2.Visibility = Visibility.Collapsed;

            ProgressCalculations.Visibility = Visibility.Collapsed;
            TrainResults.Visibility = Visibility.Collapsed;

            FilterMenu.Visibility = Visibility.Collapsed;
            InputMenu.Visibility = Visibility.Collapsed;
            FeaturesMenu.Visibility = Visibility.Collapsed;
            PortfolioSettings.Visibility = Visibility.Collapsed;

            var model = getModel();
            var ranks = isRegressionModel(model) ? Model.MLRegressionRanks : Model.MLBinaryRanks;

            nav.setNavigation(MLRankMenu, MLRankingSelected_MouseDown, ranks);
            addSaveAndCloseButtons(MLRankMenu, Save_Click);

            if (model != null)
            {
                highlightButton(MLRankMenu, model.MLRank);
            }

            nav.UseCheckBoxes = false;
        }

        private bool isRegressionModel(Model model)
        {
            return model != null && MainView.GetSenarioLabel(model.Scenario).Contains("PX");
        }

        private void MLRankingSelected_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var label = sender as Label;
            MLRank.Content = label.Content;

            ui_to_model();
            model_to_ui();

            showView();
        }

        private void MLDataSplitSelected_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var label = sender as Label;
            MLSplit.Content = label.Content;

            ui_to_model();
            model_to_ui();

            showView();
        }


        private void MLDataSplit_MouseDown(object sender, MouseButtonEventArgs e)
        {
            nav.UseCheckBoxes = false;

            Menus.Visibility = Visibility.Visible;
            ATMFeatures.Visibility = Visibility.Collapsed;
            MLIntervalsMenu.Visibility = Visibility.Collapsed;
            MLDataSplitMenu.Visibility = Visibility.Visible;
            ScenarioMenu1.Visibility = Visibility.Collapsed;
            ScenarioMenu2.Visibility = Visibility.Collapsed;
            ScenarioMenu3.Visibility = Visibility.Collapsed;
            ScenarioMenu4.Visibility = Visibility.Collapsed;

            Level1Menu.Visibility = Visibility.Collapsed;
            BenchmarkMenu1.Visibility = Visibility.Collapsed;

            RankingMenu.Visibility = Visibility.Collapsed;
            RebalanceMenu.Visibility = Visibility.Collapsed;

            MLRankMenu.Visibility = Visibility.Collapsed;
            MLIterationMenu.Visibility = Visibility.Collapsed;

            Level2Menu.Visibility = Visibility.Collapsed;
            Level3Menu1.Visibility = Visibility.Collapsed;
            Level4Menu1.Visibility = Visibility.Collapsed;
            Level4MenuScroller1.Visibility = Visibility.Collapsed;
            Level5Menu1.Visibility = Visibility.Collapsed;
            Level5MenuScroller1.Visibility = Visibility.Collapsed;
            Level6MenuScroller1.Visibility = Visibility.Collapsed;
            Level4ActionMenu1.Visibility = Visibility.Collapsed;
            SubgroupActionMenu2.Visibility = Visibility.Collapsed;

            ProgressCalculations.Visibility = Visibility.Collapsed;
            TrainResults.Visibility = Visibility.Collapsed;

            FilterMenu.Visibility = Visibility.Collapsed;
            InputMenu.Visibility = Visibility.Collapsed;
            FeaturesMenu.Visibility = Visibility.Collapsed;
            PortfolioSettings.Visibility = Visibility.Collapsed;

            nav.setNavigation(MLDataSplitMenu, MLDataSplitSelected_MouseDown, Model.MLSplits);
            addSaveAndCloseButtons(MLDataSplitMenu, Save_Click);

            var model = getModel();
            if (model != null)
            {
                highlightButton(MLDataSplitMenu, model.MLSplit);
            }

            nav.UseCheckBoxes = false;

        }


        private void MLIntervals_MouseDown(object sender, MouseButtonEventArgs e)
        {
            nav.UseCheckBoxes = true;

            Menus.Visibility = Visibility.Visible;
            ATMFeatures.Visibility = Visibility.Collapsed;
            MLIntervalsMenu.Visibility = Visibility.Visible;
            MLDataSplitMenu.Visibility = Visibility.Collapsed;
            ScenarioMenu1.Visibility = Visibility.Collapsed;
            ScenarioMenu2.Visibility = Visibility.Collapsed;
            ScenarioMenu3.Visibility = Visibility.Collapsed;
            ScenarioMenu4.Visibility = Visibility.Collapsed;

            Level1Menu.Visibility = Visibility.Collapsed;
            BenchmarkMenu1.Visibility = Visibility.Collapsed;

            RankingMenu.Visibility = Visibility.Collapsed;
            RebalanceMenu.Visibility = Visibility.Collapsed;

            MLRankMenu.Visibility = Visibility.Collapsed;
            MLIterationMenu.Visibility = Visibility.Collapsed;

            Level2Menu.Visibility = Visibility.Collapsed;
            Level3Menu1.Visibility = Visibility.Collapsed;
            Level4Menu1.Visibility = Visibility.Collapsed;
            Level4MenuScroller1.Visibility = Visibility.Collapsed;
            Level5Menu1.Visibility = Visibility.Collapsed;
            Level5MenuScroller1.Visibility = Visibility.Collapsed;
            Level6MenuScroller1.Visibility = Visibility.Collapsed;
            Level4ActionMenu1.Visibility = Visibility.Collapsed;
            SubgroupActionMenu2.Visibility = Visibility.Collapsed;

            ProgressCalculations.Visibility = Visibility.Collapsed;
            TrainResults.Visibility = Visibility.Collapsed;

            FilterMenu.Visibility = Visibility.Collapsed;
            InputMenu.Visibility = Visibility.Collapsed;
            FeaturesMenu.Visibility = Visibility.Collapsed;
            PortfolioSettings.Visibility = Visibility.Collapsed;

            var model = getModel();

            if (model != null)
            {
                nav.setNavigation(MLIntervalsMenu, MLIntervalsSelected_MouseDown, Model.MLIntervals);
                loadIntervals(MLIntervalsMenu, model.MLInterval);
                addSaveAndCloseButtons(MLIntervalsMenu, SaveIntervals_Click);
            }

            nav.UseCheckBoxes = true;

        }

        private void MLIntervalsSelected_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var label = sender as Label;
            MLIntervalButton.Content = label.Content;

            ui_to_model();
            model_to_ui();

            showView();
        }

        private void ATMFeatures_MouseDown(object sender, RoutedEventArgs e)
        {
            {
                if (Menus.Visibility == Visibility.Collapsed)
                {
                    hideView();
                    hideNavigation();

                    Menus.Visibility = Visibility.Visible;
                    ATMFeatures.Visibility = Visibility.Visible;
                    MLIntervalsMenu.Visibility = Visibility.Collapsed;
                    MLDataSplitMenu.Visibility = Visibility.Collapsed;
                    ScenarioMenu1.Visibility = Visibility.Collapsed;
                    ScenarioMenu2.Visibility = Visibility.Collapsed;
                    ScenarioMenu3.Visibility = Visibility.Collapsed;
                    ScenarioMenu4.Visibility = Visibility.Collapsed;

                    Level1Menu.Visibility = Visibility.Collapsed;
                    BenchmarkMenu1.Visibility = Visibility.Collapsed;

                    RankingMenu.Visibility = Visibility.Collapsed;
                    RebalanceMenu.Visibility = Visibility.Collapsed;

                    MLRankMenu.Visibility = Visibility.Collapsed;
                    MLIterationMenu.Visibility = Visibility.Collapsed;

                    Level2Menu.Visibility = Visibility.Collapsed;
                    Level3Menu1.Visibility = Visibility.Collapsed;
                    Level4Menu1.Visibility = Visibility.Collapsed;
                    Level4MenuScroller1.Visibility = Visibility.Collapsed;
                    Level5Menu1.Visibility = Visibility.Collapsed;
                    Level5MenuScroller1.Visibility = Visibility.Collapsed;
                    Level6MenuScroller1.Visibility = Visibility.Collapsed;
                    Level4ActionMenu1.Visibility = Visibility.Collapsed;
                    SubgroupActionMenu2.Visibility = Visibility.Collapsed;

                    ProgressCalculations.Visibility = Visibility.Collapsed;
                    TrainResults.Visibility = Visibility.Collapsed;

                    FilterMenu.Visibility = Visibility.Collapsed;
                    InputMenu.Visibility = Visibility.Collapsed;
                    FeaturesMenu.Visibility = Visibility.Collapsed;
                    PortfolioSettings.Visibility = Visibility.Collapsed;

                    nav.UseCheckBoxes = false;

                    if (MainView.EnablePortfolio)
                    {
                        List<string> items = new List<string>();

                        items.AddRange(new string[]
                        {
                        });

                        nav.UseCheckBoxes = false;
                        nav.setNavigation(ATMFeatures, ATMFeatures_MouseDown, items.ToArray());
                        addSaveAndCloseButtons(ATMFeatures, Save_Click);
                    }

                    string resourceKey = "";

                    if (resourceKey.Length > 0)
                    {
                        Viewbox legend = this.Resources[resourceKey] as Viewbox;
                        if (legend != null)
                        {
                            legend.SetValue(MarginProperty, new Thickness(0, 20, 0, 0));
                            Level1Menu.Children.Add(legend);
                        }
                    }
                }
                else
                {
                    showView();
                }
            }
        }

        private void MaxTimeAmt_TextChanged(object sender, TextChangedEventArgs e)
        {
            var tb = sender as TextBox;
            if (tb != null && _selectedModel.Length > 0)
            {
                var text = tb.Text;
                var model = getModel();
                if (model != null)
                {
                    model.MLMaxTime = text;
                }
            }
        }

        private void MaxBarsText_TextChanged(object sender, TextChangedEventArgs e)
        {
            var tb = sender as TextBox;
            if (tb != null && _selectedModel.Length > 0)
            {
                var text = tb.Text;
                var model = getModel();
                if (model != null)
                {
                    model.MLMaxBars = text;
                }
            }
        }

        private void Ticker_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ListBox listBox = sender as ListBox;
            var label = listBox.SelectedItem as Label;
            if (label != null)
            {
                _selectedTicker = label.Tag as string;
                updateAutoResults(_selectedInterval, _selectedTicker);
                SelectedTicker.Content = _selectedTicker;
            }
        }

        private void MLModel_SelectionChanged(object sender, SelectionChangedEventArgs e)
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
                _selectedModel = model;
                model_to_ui();
                initializeCalculator(getModel());
            }
        }

        private void SelectScenario_MouseDown(object sender, RoutedEventArgs e)
        {
            {
                if (Menus.Visibility == Visibility.Collapsed)
                {
                    hideView();
                    hideNavigation();

                    Menus.Visibility = Visibility.Visible;
                    ScenarioMenu1.Visibility = Visibility.Visible;
                    ScenarioMenu2.Visibility = Visibility.Collapsed;
                    ScenarioMenu3.Visibility = Visibility.Collapsed;
                    ScenarioMenu4.Visibility = Visibility.Collapsed;
                    ATMFeatures.Visibility = Visibility.Collapsed;
                    MLIntervalsMenu.Visibility = Visibility.Collapsed;
                    MLDataSplitMenu.Visibility = Visibility.Collapsed;

                    Level1Menu.Visibility = Visibility.Collapsed;
                    BenchmarkMenu1.Visibility = Visibility.Collapsed;

                    MLRankMenu.Visibility = Visibility.Collapsed;
                    MLIterationMenu.Visibility = Visibility.Collapsed;

                    RankingMenu.Visibility = Visibility.Collapsed;
                    RebalanceMenu.Visibility = Visibility.Collapsed;

                    Level2Menu.Visibility = Visibility.Collapsed;
                    Level3Menu1.Visibility = Visibility.Collapsed;
                    Level4Menu1.Visibility = Visibility.Collapsed;
                    Level4MenuScroller1.Visibility = Visibility.Collapsed;
                    Level5Menu1.Visibility = Visibility.Collapsed;
                    Level5MenuScroller1.Visibility = Visibility.Collapsed;
                    Level6MenuScroller1.Visibility = Visibility.Collapsed;
                    Level4ActionMenu1.Visibility = Visibility.Collapsed;
                    SubgroupActionMenu2.Visibility = Visibility.Collapsed;

                    ProgressCalculations.Visibility = Visibility.Collapsed;
                    TrainResults.Visibility = Visibility.Collapsed;

                    FilterMenu.Visibility = Visibility.Collapsed;
                    InputMenu.Visibility = Visibility.Collapsed;
                    FeaturesMenu.Visibility = Visibility.Collapsed;
                    PortfolioSettings.Visibility = Visibility.Collapsed;

                    nav.UseCheckBoxes = false;
                    {
                        _scenarioLevel = new string[]{ "", "", "", ""};

                        nav.UseCheckBoxes = false;
                        nav.setScenarioNavigation(_scenarioLevel, ScenarioMenu1, SelectedScenario1_MouseDown);
                        addSaveAndCloseButtons(ScenarioMenu1, Save_Click);

                        var panel = new StackPanel();
                        panel.Orientation = Orientation.Horizontal;
                        var label1 = new Label();
                        label1.Background = new SolidColorBrush(Color.FromRgb(0x30, 0x30, 0x30));
                        label1.BorderBrush = new SolidColorBrush(Color.FromRgb(0x30, 0x30, 0x30));
                        label1.BorderThickness = new Thickness(1);
                        label1.Foreground = Brushes.White;
                        label1.Height = 20;
                        label1.Padding = new Thickness(0, 2, 0, 2);
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
                        label2.Padding = new Thickness(0, 2, 0, 2);
                        label2.HorizontalAlignment = HorizontalAlignment.Center;
                        label2.VerticalAlignment = VerticalAlignment.Bottom;
                        label2.FontFamily = new FontFamily("Helvetica Neue");
                        label2.FontWeight = FontWeights.Normal;
                        label2.FontSize = 11;
                        label2.Cursor = Cursors.Hand;
                        label2.Width = 45;
                        label2.Margin = new Thickness(5, 20, 0, 0);
                        label2.Content = "Cancel";
                        label2.MouseEnter += Mouse_Enter;
                        label2.MouseLeave += Mouse_Leave;
                        label2.MouseDown += Cancel_Click;
                        panel.Children.Add(label2);
                        Level1Menu.Children.Add(panel);
                    }

                    var model = getModel();
                    if (model != null)
                    {
                        highlightButton(ScenarioMenu1, MainView.GetSenarioLabel(model.Scenario), true);
                    }

                    string resourceKey = "";

                    if (resourceKey.Length > 0)
                    {
                        Viewbox legend = this.Resources[resourceKey] as Viewbox;
                        if (legend != null)
                        {
                            legend.SetValue(MarginProperty, new Thickness(0, 20, 0, 0));
                            Level1Menu.Children.Add(legend);
                        }
                    }
                }
                else
                {
                    showView();
                }
            }
        }           

        string[] _scenarioLevel = { "", "", "", "" };
    
        private void SelectedScenario1_MouseDown(object sender, RoutedEventArgs e)
        {
            ScenarioMenu1.Visibility = Visibility.Visible;
            ScenarioMenu2.Visibility = Visibility.Visible;
            ScenarioMenu3.Visibility = Visibility.Collapsed;
            ScenarioMenu4.Visibility = Visibility.Collapsed;

            var label = sender as SymbolLabel;
            _scenarioLevel[0] = label.Content as string;
            _scenarioLevel[1] = "";
            _scenarioLevel[2] = "";
            _scenarioLevel[3] = "";


            highlightButton(ScenarioMenu1, _scenarioLevel[0], true);

            if (nav.setScenarioNavigation(_scenarioLevel, ScenarioMenu2, SelectedScenario2_MouseDown))
            {
                ScenarioMenu2.Visibility = Visibility.Visible;
            }
            else
            {
                hideNavigation();
                var senario = MainView.GetSenarioFromLabel(_scenarioLevel[0]);
                updateScenario(senario);

                var model = getModel();
                if (model != null)
                {
                    model.Scenario = senario;
                }
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
                updateScenario(senario);
                var model = getModel();
                if (model != null)
                {
                    model.Scenario = senario;
                }
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
                updateScenario(senario);
                var model = getModel();
                if (model != null)
                {
                    model.Scenario = senario;
                }
            }
        }

        private void SelectedScenario4_MouseDown(object sender, RoutedEventArgs e)
        {
            var label = sender as SymbolLabel;
            _scenarioLevel[3] = label.Content as string;

            hideNavigation();
            var senario = MainView.GetSenarioFromLabel(_scenarioLevel[2], _scenarioLevel[3]);
            updateScenario(senario);
            var model = getModel();
            if (model != null)
            {
                model.Scenario = senario;
            }
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

        private void SelectUniverse_MouseDown(object sender, RoutedEventArgs e)
        {
            {
                if (Level1Menu.Visibility == Visibility.Collapsed)
                {
                    hideView();
                    hideNavigation();

                    Menus.Visibility = Visibility.Visible;

                    Level1Menu.Visibility = Visibility.Visible;
                    BenchmarkMenu1.Visibility = Visibility.Visible;

                    RankingMenu.Visibility = Visibility.Collapsed;
                    RebalanceMenu.Visibility = Visibility.Collapsed;
                    ScenarioMenu1.Visibility = Visibility.Collapsed;
                    ScenarioMenu2.Visibility = Visibility.Collapsed;
                    ScenarioMenu3.Visibility = Visibility.Collapsed;
                    ScenarioMenu4.Visibility = Visibility.Collapsed;
                    ATMFeatures.Visibility = Visibility.Collapsed;
                    MLIntervalsMenu.Visibility = Visibility.Collapsed;
                    MLDataSplitMenu.Visibility = Visibility.Collapsed;

                    MLRankMenu.Visibility = Visibility.Collapsed;
                    MLIterationMenu.Visibility = Visibility.Collapsed;

                    Level2Menu.Visibility = Visibility.Visible;
                    Level3Menu1.Visibility = Visibility.Visible;
                    Level4Menu1.Visibility = Visibility.Visible;
                    Level4MenuScroller1.Visibility = Visibility.Visible;
                    Level5Menu1.Visibility = Visibility.Visible;
                    Level5MenuScroller1.Visibility = Visibility.Visible;
                    Level6MenuScroller1.Visibility = Visibility.Collapsed;
                    Level4ActionMenu1.Visibility = Visibility.Collapsed;
                    SubgroupActionMenu2.Visibility = Visibility.Collapsed;

                    ProgressCalculations.Visibility = Visibility.Collapsed;
                    TrainResults.Visibility = Visibility.Collapsed;

                    FilterMenu.Visibility = Visibility.Collapsed;
                    InputMenu.Visibility = Visibility.Collapsed;
                    FeaturesMenu.Visibility = Visibility.Collapsed;
                    PortfolioSettings.Visibility = Visibility.Collapsed;

                    nav.UseCheckBoxes = true;

                    {
                        List<string> items = new List<string>();

                        //items.AddRange(new string[] { "COMMODITIES >", " ", "EQ | AMERICAS >", " ", "EQ | ASIA PACIFIC >", " ", "EQ | EUROPE & MEA >", " ", "FX & CRYPTO >", " ", "INTEREST RATES >", " ", "CQG >" });
                        if (BarServer.ConnectedToBloomberg() || !BarServer.ConnectedToCQG()) items.AddRange(new string[] { "BLOOMBERG >", " ", "COMMODITIES >", " ", "EQ | AMERICAS >", " ", "EQ | ASIA PACIFIC >", " ", "EQ | EUROPE & MEA >", " ", "ETF >"," ", "FX & CRYPTO >", " ", "GLOBAL FUTURES >", " ", "INTEREST RATES >" });
                        if (BarServer.ConnectedToCQG()) items.AddRange(new string[] { " ","CQG COMMODITIES >", " ", "CQG EQUITIES >", " ", "CQG ETF >", " ", "CQG FX & CRYPTO >",  " ", "CQG INTEREST RATES >", " ", "CQG STOCK INDICES >" });
                        //items.AddRange(new string[] { "COMMODITIES >", " ", "EQ | AMERICAS >", " ", "EQ | ASIA PACIFIC >", " ", "EQ | EUROPE & MEA >", " ", "CRYPTO >", " ", "ETF >", " ", "GLOBAL FUTURES >", " ", "INTEREST RATES >", " ", "USER SYMBOL LIST 1 >" });
                        //items.AddRange(new string[] { "COMMODITIES >", " ", "EQ | AMERICAS >", " ", "EQ | ASIA PACIFIC >", " ", "EQ | EUROPE & MEA >", " ", "ETF >", " ", "GLOBAL FUTURES >"," ", "INTEREST RATES >" , " ", "US INDUSTRIES >"});

                        nav.UseCheckBoxes = true;
                        nav.clearCheckBoxes("Level1Menu");
                        nav.clearCheckBoxes("Level2Menu");
                        nav.clearCheckBoxes("Level3Menu1");
                        nav.clearCheckBoxes("Level4Menu1");
                        nav.setNavigation(Level1Menu, Level1Menu_MouseDown, items.ToArray());

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
                        label2.MouseDown += Cancel_Click;
                        panel.Children.Add(label2);
                        Level1Menu.Children.Add(panel);
                    }

                    nav.setNavigationLevel1(_level1, Level2Menu, Level2_MouseDown, go_Click);
                    nav.setNavigationLevel2(_level2, Level3Menu1, Level3Menu1_MouseDown, go_Click);
                    nav.setNavigationLevel3(_level2, _level3, Level4Menu1, Level4Menu1_MouseDown);
                    nav.setNavigationLevel4(_level2, _level3, _level4, Level5Menu1, Level5Menu_MouseDown);

                    var model = getModel();
                    var universe = (model != null) ? model.Universe : "";
                    if (universe == "Custom")
                    {
                        universe = "";
                        var symbols = model.Symbols;
                        for (int ii = 0; ii < symbols.Count; ii++)
                        {
                            var symbol = symbols[ii];
                            var ticker = symbol.Ticker;
                            if (universe.Length > 0) universe += "&";
                            universe += ticker;
                        }
                    }


                    if (model != null) 
                    {
                        highlightButton(Level1Menu, _level1);
                        highlightButton(Level2Menu, _level2);
                        highlightButton(Level3Menu1, _level3);
                        highlightButton(Level4Menu1, _level4);
                        highlightButton(Level5Menu1, _level5);
                    }
                    setCheckBoxes(model);

                    string resourceKey = "";

                    if (resourceKey.Length > 0)
                    {
                        Viewbox legend = this.Resources[resourceKey] as Viewbox;
                        if (legend != null)
                        {
                            legend.SetValue(MarginProperty, new Thickness(0, 20, 0, 0));
                            Level1Menu.Children.Add(legend);
                        }
                    }
                }
                else
                {
                    showView();
                }
            }
        }

        private void setCheckBoxes(Model model)
        {        
            nav.SetCheckBoxes();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Label label = sender as Label;
            hideNavigation();
            showView();
            TrainResults.Visibility = Visibility.Visible;
        }

        private void SaveFilter_MouseDown(object sender, RoutedEventArgs e)
        {
            var model = getModel();
            if (model != null)
            {
                model.Conditions = getCondition(_conditionTree);
            }
        }
        private void MarketMaps_MouseEnter(object sender, MouseEventArgs e)
        {
            Label label = sender as Label;
            label.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xcc, 0xff));
        }

        private void MarketMaps_MouseLeave(object sender, MouseEventArgs e)
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

        private void Alert_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Close();
            _mainView.Content = new Alerts(_mainView);
        }

        private void Server_MouseEnter(object sender, MouseEventArgs e)
        {
            Label label = sender as Label;
            label.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xcc, 0xff));
        }

        private void OurView_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Close();
            _mainView.Content = new Charts(_mainView);
        }

        private void Server_MouseLeave(object sender, MouseEventArgs e)
        {
            Label label = sender as Label;
            label.Foreground = new SolidColorBrush(Color.FromRgb(0xff, 0xff, 0xff));
        }
        private void CloseFilter_MouseDown(object sender, RoutedEventArgs e)
        {
            var model = getModel();
            if (model != null)
            {
                model.Conditions = getCondition(_conditionTree);
            }
            hideNavigation();
            showView();
        }

        private void SaveEditor_MouseDown(object sender, RoutedEventArgs e)
        {
        }

        private void CloseEditor_MouseDown(object sender, RoutedEventArgs e)
        {
            var text = getFeatures(root);
            var lines = text.Split('\n');
            var text1 = "";
            var text2 = "";
            for (var ii = 0; ii < lines.Length; ii++)
            {
                if (ii > 0) { text1 += "\n"; text2 += "\n"; }
                var fields = lines[ii].Split('\u0002');
                if (fields.Length >= 2)
                {
                    text1 += fields[0];
                    text2 += fields[1];
                }
            }
            FeatureEditor1.Text = text1;
            FeatureEditor2.Text = text2;

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

        private void loadIntervals(StackPanel panel1, string intervals)
        {
           foreach (var child in panel1.Children)
            {
                var panel2 = child as StackPanel;
                if (panel2 != null)
                {
                    var checkBox = panel2.Children[0] as CheckBox;
                    if (checkBox != null)
                    {
                        var label2 = panel2.Children[1] as Label;
                        var interval = label2.Content as string;
                        checkBox.IsChecked = (intervals.Contains(interval));
                    }
                }
            }

        }

        private void SaveIntervals_Click(object sender, RoutedEventArgs e)
        {           
            Label label1 = sender as Label;
            StackPanel panel1 = label1.Parent as StackPanel;
            StackPanel panel2 = panel1.Parent as StackPanel;
            var intervals = "";
            foreach (var child in panel2.Children)
            {
                var panel3 = child as StackPanel;
                if (panel3 != null)
                {
                    var checkBox = panel3.Children[0] as CheckBox;
                    if (checkBox != null)
                    {
                        var label2 = panel3.Children[1] as Label;
                        if (checkBox.IsChecked == true)
                        {
                            if (intervals.Length > 0) intervals += ",  ";
                            intervals += label2.Content as string;
                        }
                    }
                }
            }
            MLIntervalButton.Content = intervals;

            hideNavigation();
            showView();

            ui_to_model();
            model_to_ui();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            Label label = sender as Label;
            hideNavigation();
            showView();
            setUniverse(nav.GetPortfolio());

            ui_to_model();
            model_to_ui();
        }


        private void AddModelHelp_MouseDown(object sender, RoutedEventArgs e)
        {
            ProgressCalculations.Visibility = Visibility.Collapsed;
            TrainResults.Visibility = Visibility.Collapsed;
        }

        private void SetupHelp_MouseDown(object sender, RoutedEventArgs e)
        {
            ProgressCalculations.Visibility = Visibility.Collapsed;
            TrainResults.Visibility = Visibility.Collapsed;
        }

        private void InputHelp_MouseDown(object sender, RoutedEventArgs e)
        {
            ProgressCalculations.Visibility = Visibility.Collapsed;
            TrainResults.Visibility = Visibility.Collapsed;
        }

        private void ResultsHelp_MouseDown(object sender, RoutedEventArgs e)
        {
            ProgressCalculations.Visibility = Visibility.Collapsed;
            TrainResults.Visibility = Visibility.Collapsed;
        }

        private void TrainHelp_MouseDown(object sender, RoutedEventArgs e)
        {          
            ProgressCalculations.Visibility = Visibility.Collapsed;
            TrainResults.Visibility = Visibility.Collapsed;
        }

        private void Rebalance_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (Menus.Visibility == Visibility.Collapsed)
            {
                hideView();
                hideNavigation();

                Menus.Visibility = Visibility.Visible;

                Level1Menu.Visibility = Visibility.Collapsed;
                BenchmarkMenu1.Visibility = Visibility.Collapsed;

                RankingMenu.Visibility = Visibility.Collapsed;
                RebalanceMenu.Visibility = Visibility.Visible;

                ScenarioMenu1.Visibility = Visibility.Collapsed;
                ScenarioMenu2.Visibility = Visibility.Collapsed;
                ScenarioMenu3.Visibility = Visibility.Collapsed;
                ScenarioMenu4.Visibility = Visibility.Collapsed;

                ATMFeatures.Visibility = Visibility.Collapsed;
                MLIntervalsMenu.Visibility = Visibility.Collapsed;
                MLDataSplitMenu.Visibility = Visibility.Collapsed;

                MLRankMenu.Visibility = Visibility.Collapsed;
                MLIterationMenu.Visibility = Visibility.Collapsed;

                Level2Menu.Visibility = Visibility.Collapsed;
                Level3Menu1.Visibility = Visibility.Collapsed;
                Level4Menu1.Visibility = Visibility.Collapsed;
                Level4MenuScroller1.Visibility = Visibility.Collapsed;
                Level5Menu1.Visibility = Visibility.Collapsed;
                Level5MenuScroller1.Visibility = Visibility.Collapsed;
                Level6MenuScroller1.Visibility = Visibility.Collapsed;
                Level4ActionMenu1.Visibility = Visibility.Collapsed;
                SubgroupActionMenu2.Visibility = Visibility.Collapsed;

                ProgressCalculations.Visibility = Visibility.Collapsed;
                TrainResults.Visibility = Visibility.Collapsed;

                FilterMenu.Visibility = Visibility.Collapsed;
                InputMenu.Visibility = Visibility.Collapsed;
                FeaturesMenu.Visibility = Visibility.Collapsed;
                PortfolioSettings.Visibility = Visibility.Collapsed;

                nav.UseCheckBoxes = false;
                var intervals = Model.MLIntervals.ToList();
                intervals.Add("ML INTERVALS - BEST TRAINER");
                nav.setNavigation(RebalanceMenu, RebalanceSelected_MouseDown, intervals.ToArray());
                addSaveAndCloseButtons(RebalanceMenu, Save_Click);

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
                label2.Content = "Cancel";
                label2.MouseEnter += Mouse_Enter;
                label2.MouseLeave += Mouse_Leave;
                label2.MouseDown += Cancel_Click;
                panel.Children.Add(label2);
                Level1Menu.Children.Add(panel);

                var rebalance = _selectedInterval; 
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

        private void RebalanceSelected_MouseDown(object sender, RoutedEventArgs e)
        {
            Label label = e.Source as Label;
            string name = label.Content as string;
          
            UserFactorModelRebalance.Content = name;

            TrainResults.Visibility = Visibility.Visible;

            ui_to_model();
            model_to_ui();

            showView();
        }

        private void loadModel()
        {
            var model = getModel();
            if (model != null)
            {
                model.Groups[0].AlphaSymbols = loadSymbolList(model.Name + " Symbols");
                _currentPortfolio = loadList<string>(model.Name + " CurrentPortFolio");
                _portfolioTimes = loadList<DateTime>(model.Name + " PortfolioTimes");
                _benchmarkValues = loadList<double>(model.Name + " BenchmarkValues");
                _portfolioValues = loadList<double>(model.Name + " PortfolioValues");
                _portfolioMonthTimes = loadList<DateTime>(model.Name + " PortfolioMonthTimes");
                _portfolioMonthValues = loadList<double>(model.Name + " PortfolioMonthValues");
                _compareToValues = loadList<double>(model.Name + " CompareToValues");
            }
        }

        List<Trade> _strategyTrades = new List<Trade>();

        private bool _compareToSymbolChange = false;

        private TreeNode root = null;

        private void initFeatureTree(TreeView treeView)
        {
            if (root == null)
            {
                string[] intervals = { "Mid Term", "Short Term" };
                root = new TreeNode("ATMMLTrainerData", intervals, true);
            }
            treeView.ItemsSource = root.Children;
        }

        static string ATMDefaultFeatures =
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
                    "ST Going Dn    \u0002 Mid Term\n" +
                    "Score    \u0002 Short Term\n" +
                    "Score    \u0002 Mid Term";

        private string getFeatures(TreeNode input)
        {
            string output = "";
            if (input.IsGroup)
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
                    output = ATMDefaultFeatures;
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
            clearTree(root.Children);

            input = input.Replace(ATMDefaultFeatures, "Default");

            var features = input.Split('\n');

            foreach (string feature in features)
            {
                if (feature.Length > 0)
                {
                    if (feature == "Default")
                    {
                        var item = getItem(root.Children, feature);
                        if (item != null)
                        {
                            item.IsChecked = true;
                        }
                    }
                    else
                    {
                        var items = feature.Split('\u0002');
                        var condition = items[0].Trim();
                        var term = items[1].Trim();

                        var item1 = getItem(root.Children, condition);

                        if (item1 != null)
                        {
                            if (item1.Children != null)
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
                            else
                            {
                                item1.IsChecked = true;
                            }
                        }

                        var item3 = getItem(root.Children, feature);
                        if (item3 != null)
                        {
                            item3.IsChecked = true;
                        }

                    }
                }
            }
        }

        private TreeNode _conditionTree = null;

        private void initConditionTree()
        {

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
                    item.IsChecked = true;
                    item.IsExpanded = true;
                    output = item;
                    break;
                }
                else if (item.IsIndicatorInterval)
                {
                    var description = item.GetDescription();
                    var fields = description.Split('\u0002');
                    if (fields[0] == name)
                    {
                        item.IsChecked = true;
                        item.IsExpanded = true;
                        output = item;
                        break;
                    }
                }
                else if (item.Children != null)
                {
                    output = getItem(item.Children, name);
                    if (output != null)
                    {
                        item.IsChecked = true;
                        item.IsExpanded = true;
                        break;
                    }
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
                break;
            }
            return key;
        }

        private string getCondition(TreeNode root)
        {
            string output = "";
            if (root.HasChildren)
            {
                foreach (var node in root.Children)
                {
                    var text = getCondition(node);
                    if (text.Length > 0)
                    {
                        output += ((output.Length > 0) ? "\u0001" : "") + text;
                    }
                }
            }
            else if (root.IsChecked)
            {
                output = root.GetDescription();
            }
            return output;
        }

        private void ConditionTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            TreeNode tn1 = e.OldValue as TreeNode;
            TreeNode tn2 = e.NewValue as TreeNode;
        }

        private void _filterPortfolio_PortfolioChanged(object sender, PortfolioEventArgs e)
        {
            if (e.Type == PortfolioEventType.ReferenceData)
            {
                foreach (KeyValuePair<string, object> kvp in e.ReferenceData)
                {
                    string fieldName = kvp.Key;
                    object fieldValue = kvp.Value;

                    _conditionTree.SetValue(fieldName, fieldValue);
                }
            }
        }

        private void ChangeTrainer(object sender, RoutedEventArgs e)
        {
        }

        private void CloseSettings_MouseDown(object sender, RoutedEventArgs e)
        {
            hideNavigation();
            showView();
            ui_to_model();
            model_to_ui();
        }

        private void SaveSettings_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            hideNavigation();
            showView();
            ui_to_model();
            model_to_ui();
        }

        private void CloseHelp_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {    
            ViewHelp.Visibility = Visibility.Collapsed;
            MLSetup.Visibility = Visibility.Collapsed;
            MLResults.Visibility = Visibility.Collapsed;
            TrainResults.Visibility = Visibility.Visible;
            HelpMenu.Visibility = Visibility.Collapsed;
            TestVideo.Visibility = Visibility.Collapsed;
            HelpON.Visibility = Visibility.Visible;
            HelpOFF.Visibility = Visibility.Collapsed;
        }

        private void Help_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            ViewHelpSection.BringIntoView();
            ViewHelp.Visibility = Visibility.Visible;
            MLSetup.Visibility = Visibility.Collapsed;
            HelpON.Visibility = Visibility.Collapsed;
            HelpOFF.Visibility = Visibility.Visible;
        }
        private void ViewHelp_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            ViewHelpSection.BringIntoView();
            ViewHelp.Visibility = Visibility.Visible;
            MLSetup.Visibility = Visibility.Collapsed;
            MLResults.Visibility = Visibility.Collapsed;
            HelpMenu.Visibility = Visibility.Collapsed;
            TestVideo.Visibility = Visibility.Collapsed;
            HelpON.Visibility = Visibility.Collapsed;
            HelpOFF.Visibility = Visibility.Visible;
        }
        private void MLSetup_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            MLSetupSection.BringIntoView();
            ViewHelp.Visibility = Visibility.Collapsed;
            MLSetup.Visibility = Visibility.Visible;
            MLResults.Visibility = Visibility.Collapsed;
            HelpMenu.Visibility = Visibility.Collapsed;
            TestVideo.Visibility = Visibility.Collapsed;
            HelpON.Visibility = Visibility.Collapsed;
            HelpOFF.Visibility = Visibility.Visible;
        }

        private void MLResults_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            MLResultsSection.BringIntoView();
            ViewHelp.Visibility = Visibility.Collapsed;
            MLSetup.Visibility = Visibility.Collapsed;
            MLResults.Visibility = Visibility.Visible;
            HelpMenu.Visibility = Visibility.Collapsed;
            TestVideo.Visibility = Visibility.Collapsed;
            HelpON.Visibility = Visibility.Collapsed;
            HelpOFF.Visibility = Visibility.Visible;
        }

        private void TestVideo_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            MLResultsSection.BringIntoView();
            ViewHelp.Visibility = Visibility.Collapsed;
            MLSetup.Visibility = Visibility.Collapsed;
            MLResults.Visibility = Visibility.Collapsed;
            HelpMenu.Visibility = Visibility.Visible;
            TestVideo.Visibility = Visibility.Visible;
            HelpON.Visibility = Visibility.Collapsed;
            HelpOFF.Visibility = Visibility.Visible;
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

        private void CheckBox_Click(object sender, RoutedEventArgs e)
        {
            CheckBox checkBox = sender as CheckBox;
            TreeNode treeNode = checkBox.DataContext as TreeNode;
            treeNode.IsSelected = (checkBox.IsChecked == true);
        }

        private void AddNewMLModel(object sender, MouseButtonEventArgs e)
        {
            string name = "";
            for (int ii = 1; ii < 100; ii++)
            {
                var tryName = "MODEL " + ii.ToString();
                if (!_models.ContainsKey(tryName))
                {
                    name = tryName;
                    break;
                }
            }

            if (name.Length > 0)
            {
                _models[name] = new Model(name);
            }

            _selectedModel = name;
            updateModelList(_selectedModel, _models.Keys.ToList(), ModelList);
            model_to_ui();
            requestPortfolio(_models[name].Universe);
            initializeCalculator(getModel());
        }

        private void SaveModel_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var model = getModel();
            if (model != null)
            {

                //var model1 = readModel(model.Name);
                //var model2 = model.save();

                //if (model1 != model2)
                //{
                //    model.Trained = false;
                //}

                saveModel();
                requestPortfolio(model.Universe);
            }
        }

        private void saveModel()
        {
            Grid grid = ModelList.SelectedItem as Grid;
            if (grid != null)
            {
                TextBox tb = grid.Children[0] as TextBox;
                if (tb != null)
                {
                    ui_to_model();

                    string newName = tb.Text;
                    string oldName = _selectedModel;
                    if (newName != oldName)
                    {
                        changeSelectedModel(newName, oldName);
                    }
                    saveModel(newName);
                }
            }
        }

        private void changeSelectedModel(string newName, string oldName)
        {
            if (!_models.ContainsKey(newName))
            {
                var oldPath = MainView.GetDataFolder() + @"\senarios" + @"\" + oldName;
                var newPath = MainView.GetDataFolder() + @"\senarios" + @"\" + newName;
                if (Directory.Exists(oldPath) && !Directory.Exists(newPath))
                {
                    Directory.Move(oldPath, newPath);
                }

                var model = _models[oldName];
                _models.Remove(oldName);
                model.Name = newName;
                _models[newName] = model;
                updateModelList(newName, _models.Keys.ToList(), ModelList);

            }

            _selectedModel = newName;
            initializeCalculator(getModel());
        }

        private void StockTrain_Checked(object sender, RoutedEventArgs e)
        {
            TickerList.Visibility = Visibility.Visible;
            GroupLabel.Visibility = Visibility.Collapsed;
        }

        private void GroupTrain_Checked(object sender, RoutedEventArgs e)
        {
            TickerList.Visibility = Visibility.Collapsed;
            GroupLabel.Visibility = Visibility.Visible;
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

        private void Example_MouseDown(object sender, MouseButtonEventArgs e)
        {
            loadExampleModel();
            Example.Visibility = Visibility.Collapsed;
        }

        void OnMouseDownPlayMedia(object sender, MouseButtonEventArgs args)
        {

            // The Play method will begin the media if it is not currently active or
            // resume media if it is paused. This has no effect if the media is
            // already running.
            myMediaElement.Play();

            // Initialize the MediaElement property values.
            InitializePropertyValues();
        }

        // Pause the media.
        void OnMouseDownPauseMedia(object sender, MouseButtonEventArgs args)
        {

            // The Pause method pauses the media if it is currently running.
            // The Play method can be used to resume.
            myMediaElement.Pause();
        }

        // Stop the media.
        void OnMouseDownStopMedia(object sender, MouseButtonEventArgs args)
        {

            // The Stop method stops and resets the media to be played from
            // the beginning.
            myMediaElement.Stop();
        }

        // Change the volume of the media.
        private void ChangeMediaVolume(object sender, RoutedPropertyChangedEventArgs<double> args)
        {
            myMediaElement.Volume = (double)volumeSlider.Value;
        }

        // Change the speed of the media.
        private void ChangeMediaSpeedRatio(object sender, RoutedPropertyChangedEventArgs<double> args)
        {
            myMediaElement.SpeedRatio = (double)speedRatioSlider.Value;
        }

        // When the media opens, initialize the "Seek To" slider maximum value
        // to the total number of miliseconds in the length of the media clip.
        private void Element_MediaOpened(object sender, EventArgs e)
        {
            timelineSlider.Maximum = myMediaElement.NaturalDuration.TimeSpan.TotalMilliseconds;
        }

        // When the media playback is finished. Stop() the media to seek to media start.
        private void Element_MediaEnded(object sender, EventArgs e)
        {
            myMediaElement.Stop();
        }

        // Jump to different parts of the media (seek to).
        private void SeekToMediaPosition(object sender, RoutedPropertyChangedEventArgs<double> args)
        {
            int SliderValue = (int)timelineSlider.Value;

            // Overloaded constructor takes the arguments days, hours, minutes, seconds, milliseconds.
            // Create a TimeSpan with miliseconds equal to the slider value.
            TimeSpan ts = new TimeSpan(0, 0, 0, 0, SliderValue);
            myMediaElement.Position = ts;
        }

        void InitializePropertyValues()
        {
            // Set the media's starting Volume and SpeedRatio to the current value of the
            // their respective slider controls.
            myMediaElement.Volume = (double)volumeSlider.Value;
            myMediaElement.SpeedRatio = (double)speedRatioSlider.Value;
        }
    }
}

