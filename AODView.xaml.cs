using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace ATMML
{
    /// <summary>
    /// Analysis-on-Demand page: a user-selected portfolio's positions rendered as a
    /// grid of AOD3 cards. The population pipeline (bar fetch -> predictions ->
    /// update/Draw) is lifted from MarketMonitor so the cards fill exactly as they
    /// do there. Launch via: _mainView.Content = new AODView(_mainView);
    /// </summary>
    public partial class AODView : UserControl
    {
        private readonly MainView _mainView;

        // --- portfolio plumbing (same instances/ids MarketMonitor uses) ---
        private Portfolio _portfolio1 = new Portfolio(5001);      // for GetRelativeIndex
        private Portfolio _symbolPortfolio = new Portfolio(10);   // for GetDescription

        // --- AOD population engine (lifted from MarketMonitor) ---
        private BarCache _aodBarCache;
        private List<AOD3> _aods = new List<AOD3>();
        private Queue<AOD3> _aodUpdate = new Queue<AOD3>();
        private Thread _aodUpdateThread = null;
        private bool _aodUpdateThreadStop = false;
        private int _indexBarRequestCount = 0;
        private Dictionary<string, object> _referenceData = new Dictionary<string, object>();

        // defaults applied to every card; wire a model/interval picker later if needed
        private string _modelName = "";
        private string _interval = "Daily";

        // card size in the grid (tune to taste)
        private const double CardWidth = 220;
        private const double CardHeight = 180;

        public AODView(MainView mainView)
        {
            _mainView = mainView;
            InitializeComponent();

            _aodBarCache = new BarCache(aodBarChanged);

            _aodUpdateThread = new Thread(updateAodThread);
            _aodUpdateThread.IsBackground = true;
            _aodUpdateThread.Start();

            Unloaded += AODView_Unloaded;

            populatePortfolioPicker();
        }

        // ----------------------------------------------------------------------
        // Portfolio picker
        // ----------------------------------------------------------------------
        private void populatePortfolioPicker()
        {
            // The named portfolios the app knows (includes the active/live ones).
            var names = new List<string>(Trade.Manager.Portfolios.Keys);
            names.Sort();

            PortfolioPicker.ItemsSource = names;
            PortfolioPicker.SelectionChanged += PortfolioPicker_SelectionChanged;

            // Default selection. If you have an "active portfolio" accessor, set it here
            // instead, e.g.: var active = _mainView.ActivePortfolioName;
            if (names.Count > 0)
                PortfolioPicker.SelectedIndex = 0;
        }

        private void PortfolioPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var name = PortfolioPicker.SelectedItem as string;
            if (string.IsNullOrEmpty(name)) return;
            loadPortfolio(name);
        }

        private void loadPortfolio(string name)
        {
            // tear down previous set
            foreach (var a in _aods) a.AodEvent -= handleAodEvent;
            _aods.Clear();
            lock (_aodUpdate) _aodUpdate.Clear();
            _indexBarRequestCount = 0;

            // synchronous enumeration from Trade.Manager.Portfolios
            var members = _portfolio1.GetSymbols(name);

            foreach (var m in members)
            {
                var aod = new AOD3();
                aod.Symbol = m.Ticker;
                aod.Description = m.Description;
                aod.Interval = _interval;
                aod.ModelName = _modelName;
                aod.AodEvent += handleAodEvent;
                _aods.Add(aod);
            }

            StatusText.Text = members.Count == 0
                ? "No positions in \"" + name + "\""
                : name + "  \u2014  " + members.Count + " positions";

            drawAods();
            requestAODIndexBars();   // kicks off the async bar -> populate flow
        }

        // ----------------------------------------------------------------------
        // Layout
        // ----------------------------------------------------------------------
        private void drawAods()
        {
            AODGrid.Children.Clear();
            foreach (var aod in _aods)
            {
                aod.Width = CardWidth;
                aod.Height = CardHeight;
                aod.Margin = new Thickness(4);
                AODGrid.Children.Add(aod);
            }
        }

        // ----------------------------------------------------------------------
        // Population engine (lifted from MarketMonitor; identical semantics)
        // ----------------------------------------------------------------------
        private void requestUpdate(AOD3 aod)
        {
            lock (_aodUpdate)
            {
                if (!_aodUpdate.Contains(aod))
                    _aodUpdate.Enqueue(aod);
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
                        aod = _aodUpdate.Dequeue();
                }

                while (aod != null)
                {
                    try
                    {
                        updateAod(aod);
                        var captured = aod;
                        this.Dispatcher.Invoke(DispatcherPriority.Normal,
                            (Action)delegate () { drawAod(captured); });
                    }
                    catch (Exception)
                    {
                        // swallow per tile so one bad symbol can't stall the queue
                    }
                    aod = null;
                }
                Thread.Sleep(100);
            }
        }

        private void updateAod(AOD3 aod)
        {
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

            mpst.predict(ticker, interval0, new string[] { modelName }.ToList(), _aodBarCache);

            _referenceData["Index Prices : " + interval0] = _aodBarCache.GetSeries(indexTicker, interval0, new string[] { "Close" }, extra)[0];
            _referenceData["Index Prices : " + interval1] = _aodBarCache.GetSeries(indexTicker, interval1, new string[] { "Close" }, extra)[0];

            if (shortTermTimes.Count > 0)
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
        }

        // No AOD chart hosted in this view, so no signal-condition add-enables.
        private Dictionary<string, bool> getSCAddEnbs()
        {
            return new Dictionary<string, bool>();
        }

        private void drawAod(AOD3 aod)
        {
            _symbolPortfolio.GetDescription(aod.Symbol);
            aod.Draw();
        }

        // ----------------------------------------------------------------------
        // Async bar fetch (lifted from MarketMonitor)
        // ----------------------------------------------------------------------
        private void requestAODIndexBars()
        {
            var indexTickers = _aods.Select(x => _portfolio1.GetRelativeIndex(x.Symbol))
                                    .Distinct().Where(x => x.Length > 0).ToList();
            var intervals = getAodIntervals();

            _indexBarRequestCount = indexTickers.Count * intervals.Count;

            if (_indexBarRequestCount == 0)
            {
                // no index bars to wait on -> go straight to symbol bars
                requestAODBars();
                return;
            }

            foreach (var ticker in indexTickers)
                foreach (var interval in intervals)
                    _aodBarCache.RequestBars(ticker, interval, true);
        }

        private void requestAODBars()
        {
            var tickers = _aods.Select(x => x.Symbol).Distinct();
            var intervals = getAodIntervals();

            foreach (var ticker in tickers)
                foreach (var interval in intervals)
                    _aodBarCache.RequestBars(ticker, interval, true, 300, true);
        }

        private List<string> getAodIntervals(string ticker = "")
        {
            var output = new List<string>();
            foreach (var aod in _aods)
            {
                if (ticker.Length == 0 || ticker == aod.Symbol)
                {
                    // request both forecast intervals the tile needs
                    output.Add(Study.getForecastInterval(aod.Interval, 0));
                    output.Add(Study.getForecastInterval(aod.Interval, 1));
                }
            }
            return output.Distinct().ToList();
        }

        private void aodBarChanged(object sender, BarEventArgs e)
        {
            if (e.Type == BarEventArgs.EventType.BarsReceived)
            {
                string ticker = e.Ticker;

                if (_indexBarRequestCount > 0)
                {
                    if (--_indexBarRequestCount == 0)
                        requestAODBars();
                }
                else
                {
                    foreach (var aod in _aods)
                        if (aod.Symbol == ticker)
                            requestUpdate(aod);
                }
            }
            else if (e.Type == BarEventArgs.EventType.BarsUpdated)
            {
                string ticker = e.Ticker;
                foreach (var aod in _aods)
                    if (aod.Symbol == ticker)
                        requestUpdate(aod);
            }
        }

        // ----------------------------------------------------------------------
        // Tile events
        // ----------------------------------------------------------------------
        private void handleAodEvent(object sender, AodEventArgs e)
        {
            var aod = sender as AOD3;
            if (aod == null) return;

            if (e.Id == AodEventType.Interval)
            {
                // interval changed on a card -> refetch and repopulate
                requestAODBars();
            }
            // Chart / Add / Close are MarketMonitor-specific and intentionally
            // not handled in the portfolio-driven view.
        }

        // ----------------------------------------------------------------------
        // Lifecycle
        // ----------------------------------------------------------------------
        private void AODView_Unloaded(object sender, RoutedEventArgs e)
        {
            _aodUpdateThreadStop = true;
            foreach (var a in _aods) a.AodEvent -= handleAodEvent;
        }
    }
}