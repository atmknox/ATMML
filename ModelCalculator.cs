using System.Collections.Generic;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Timers;

namespace ATMML
{
    public enum ProgressState
    {
        CollectingData,
        ProcessingData,
        Training,
        Predicting,
        Finished,
        Complete
    }

    public class ModelCalculator
	{
        Model _model = null;
        Portfolio _portfolio1 = null;
        Portfolio _portfolio2 = null;
        BarCache _barCache = null;
        int _indexBarRequestCount = 0;
        List<string> _indexSymbols = new List<string>();
        List<string> _referenceSymbols = new List<string>();
        Dictionary<string, object> _referenceData = new Dictionary<string, object>();
        int _totalBarRequest = 0;
        List<string> _barRequests = new List<string>();
        ProgressState _progressState = ProgressState.Complete;
        int _progressTotalNumber = 0;
        int _progressCompletedNumber = 0;
        DateTime _progressTime = DateTime.Now;
        bool _waitForBars = false;
        bool _waitForFundamentals = false;
        bool _stop = false;
        int _reportCount = 0;
        DateTime _fundamentalResponseTime = DateTime.UtcNow;
        List<string> _fundamentalSymbols = new List<string>();
        System.Timers.Timer _timer;

        static Dictionary<string, ModelCalculator> _calculators = new Dictionary<string, ModelCalculator>();

        public event ProgressEventHandler ProgressEvent;

        public static ModelCalculator GetModelCalculator(Model model)
        {
            ModelCalculator output = null;
            if (_calculators.ContainsKey(model.Name))
            {
                output = _calculators[model.Name];
                output._model = model;
            }
            else {
                output = new ModelCalculator(model);
                _calculators[model.Name] = output;
            }
            return output;
        }

        private ModelCalculator(Model model)
        {
			_model = model;
            _portfolio1 = new Portfolio(102);
            _portfolio2 = new Portfolio(103);
            _barCache = new BarCache(barChanged);
            _portfolio1.PortfolioChanged += new PortfolioChangedEventHandler(portfolio1Changed);
            _portfolio2.PortfolioChanged += new PortfolioChangedEventHandler(portfolio2Changed);
        
            _timer = new System.Timers.Timer(1000);
            _timer.Elapsed += new ElapsedEventHandler(timerEvent);
            _timer.AutoReset = true;
            _timer.Start();
        }

        public string ModelName { get { return _model.Name; } }

        Thread _thread = null;

        public void Run()
        {
            if (_model != null)
            {
                _stop = false;
                _progressState = ProgressState.CollectingData;
                _thread = new Thread(new ThreadStart(run));
                _thread.Start();
            }
        }

        public void Stop()
        {
            _stop = true;

            if (_timer != null) {
             _timer.Stop();
            }

            if (_thread != null)
            {
                _thread.Join(10000);
            }

            _progressState = ProgressState.Complete;
            sendProgressEvent();
        }

        private void run()
        {
            requestReferenceData();

            var symbols = _model.Symbols;
            _waitForFundamentals = isCQGPortfolio(symbols) ? false : requestReportDates(); // requests report dates and then requests fundamentals after getting all the report dates

            waitForDataComplete();
            trainModel();
        }

        private void waitForDataComplete()
        {
            while (!_stop && (_waitForBars || _waitForFundamentals))
            {
                Thread.Sleep(100);
            }
        }

        public void Close()
        {
            Stop();

            _portfolio1.PortfolioChanged -= new PortfolioChangedEventHandler(portfolio1Changed);
            _portfolio1.Close();
            _portfolio2.PortfolioChanged -= new PortfolioChangedEventHandler(portfolio2Changed);
            _portfolio2.Close();

            _barCache.Clear();

        }

        public ProgressState ProgressState
        {
            get { return _progressState; }
        }

        public int ProgressTotalNumber
        {
            get { return _progressTotalNumber; }
        }

        public int ProgressCompletedNumber
        {
            get { return _progressCompletedNumber; }
        }

        public DateTime ProgressTime
        {
            get { return _progressTime; }
        }

        protected void sendProgressEvent()
        {
            if (ProgressEvent != null && !_stop)
            {
                if (_progressCompletedNumber > _progressTotalNumber)
                {
                    _progressCompletedNumber = _progressTotalNumber;
                }
                ProgressEvent(this, new EventArgs());
            }
        }

        private bool isCQGPortfolio(List<Symbol> symbols)
        {
            return symbols.Count > 0 && Portfolio.isCQGSymbol(symbols[0].Ticker);
        }

        void requestReferenceData()
        {
            _waitForBars = true;

            var symbols = _model.Symbols;

            _referenceData.Clear();

            if (isCQGPortfolio(symbols))
            {
                _indexSymbols.Clear();
                _indexSymbols.Add("F.EP");
                requestIndexBars();
            }
            else
            {
                int count = symbols.Count;

                _referenceSymbols = new List<string>();
                for (int ii = 0; ii < count && !_stop; ii++)
                {
                    if (symbols[ii].Ticker.Contains(" Equity"))
                    {
                        _referenceSymbols.Add(symbols[ii].Ticker);
                    }
                }

                for (int jj = 0; jj < _referenceSymbols.Count && !_stop; jj++)
                {
                    string[] dataFieldNames = { "REL_INDEX" };
                    _portfolio1.RequestReferenceData(_referenceSymbols[jj], dataFieldNames);
                }

                if (_referenceSymbols.Count == 0)
                {
                    requestSymbolBars();
                }
            }
        }

        private List<string> getTickers()
        {
            List<string> tickers = new List<string>();
            if (_model.Symbols != null)
            {
                foreach (var symbol in _model.Symbols)
                {
                    tickers.Add(symbol.Ticker);
                }
            }
            return tickers;
        }

        bool requestReportDates()
        {
            List<string> symbols = getTickers();
            DateTime date = DateTime.UtcNow - new TimeSpan(20 * 365, 0, 0, 0);
            _reportCount = symbols.Count;
            var ok = false;
            if (_reportCount > 0)
            {
                ok = _portfolio2.RequestFundamentalReportDates(symbols, date.Year);
            }
            return ok;
        }

        void requestHistoricalReferenceData()
        {
            var tickers = getTickers();

            int count = tickers.Count;

            var features = new List<string>(_model.FeatureNames);
  
            var dataFieldNames = new List<string>();
            foreach (var feature in features)
            {
                string fieldName = Conditions.GetFieldNameFromDescription(feature);
                if (fieldName.Length > 0) {
                    dataFieldNames.AddRange(getRequiredHistoricalReferenceData(fieldName));
                }
            }

            _fundamentalSymbols.Clear();
            _fundamentalResponseTime = DateTime.UtcNow;
            if (dataFieldNames.Count > 0)
            {
                for (int jj = 0; jj < count; jj++)
                {
                    _fundamentalSymbols.Add(tickers[jj]);
                    _portfolio2.RequestReferenceData(tickers[jj], dataFieldNames.ToArray());
                }
            }
            else
            {
                _waitForFundamentals = false;
            }
        }

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

        private Dictionary<string, object> getFundamentalFeatures(string ticker, string interval)
        {
            var output = new Dictionary<string, object>();
            try
            {
                string benchmarkSymbol = _model.Benchmark;
                List<DateTime> barTimes = _barCache.GetTimes(benchmarkSymbol, interval, 0, BarServer.MaxBarCount);
                int barCount = barTimes.Count;

                List<string> fieldNames = new List<string>();

                var features = _model.FeatureNames;

                foreach (var feature in features)
                {
                    string fieldName = Conditions.GetFieldNameFromDescription(feature);
                    fieldNames.Add(fieldName);
                }

                var data = new Dictionary<string, Dictionary<string, string>>();
                foreach (var fieldName in fieldNames)
                {
                    data[fieldName] = _portfolio2.loadData(ticker, fieldName);
                }

                foreach (var featureName in fieldNames)
                {
                    int featureCount = data[featureName].Count;
                    if (featureCount > 0)
                    {

                        List<string> inputTimes = data[featureName].Keys.ToList();
                        inputTimes.Sort();
                        int inputCount = inputTimes.Count;

                        //    a     b      c
                        // xxxaaaaaabbbbbbbccccccccccccccccccc

                        int iIdx = 0;

                        double iValue = double.NaN;
                        for (int oIdx = 0; oIdx < barCount; oIdx++)
                        {
                            string oDate = barTimes[oIdx].ToString("yyyy-MM-dd");
                            for (; iIdx < inputCount; iIdx++)
                            {
                                var date = inputTimes[iIdx];
                                if (date.CompareTo(oDate) > 0) break; // iDate > oDate
                                if (data[featureName].ContainsKey(date))
                                {
                                    iValue = double.Parse(data[featureName][date]);
                                }
                            }
                            string key = featureName + ":" + oDate;
                            output[key] = iValue;
                        }
                    }
                }
            }
            catch (Exception x)
            {
                //Debug.WriteLine(x.Message);
            }
            return output;
        }


        void portfolio1Changed(object sender, PortfolioEventArgs e)
        {
            if (e.Type == PortfolioEventType.ReferenceData)
            {
                bool okToRequestIndexBars = false;

                foreach (KeyValuePair<string, object> kvp in e.ReferenceData)
                {
                    if (_stop) break;
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
                            if (_stop) break;
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

        void portfolio2Changed(object sender, PortfolioEventArgs e)
        {
            if (e.Type == PortfolioEventType.ReferenceData)
            {
                if (_reportCount > 0)
                {
                    if (--_reportCount == 0)
                    {
                        requestHistoricalReferenceData();
                    }
                }
                else
                {
                    _fundamentalResponseTime = DateTime.UtcNow;
                    _fundamentalSymbols.Remove(e.Ticker);
                    if (_fundamentalSymbols.Count == 0)
                    {
                        _waitForFundamentals = false;
                    }
                }
            }
        }

        private void requestIndexBars()
        {
            var indexIntervals = getDataIntervals();

            _indexBarRequestCount = _indexSymbols.Count * indexIntervals.Count;

            _progressCompletedNumber = 0;
            _progressTotalNumber = (_indexSymbols.Count + _model.Symbols.Count) * indexIntervals.Count;
            sendProgressEvent();

            if (_indexBarRequestCount > 0)
            {
                foreach (var interval in indexIntervals)
                {
                    if (_stop) break;
                    foreach (string symbol in _indexSymbols)
                    {
                        if (_stop) break;
                        _barCache.RequestBars(symbol, interval, false, BarServer.MaxBarCount, false);
                    }
                }
            }
            else
            {
                requestSymbolBars();
            }
        }

        private List<string> getDataIntervals()
        {
            var intervals = new List<string>();
            var modelIntervals = getModelIntervals();
            modelIntervals.Add(Study.getForecastInterval(modelIntervals[0], 1));
            
            var features = _model.FeatureNames;
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
            if (intervals.Count == 0) intervals = modelIntervals;
            intervals = intervals.Distinct().ToList();
            return intervals;
        }

        private List<string> getModelIntervals()
        {
            var modelIntervals = _model.MLInterval.Split(';').Select(x => x.Trim()).ToList();
            return modelIntervals;
        }

        void requestSymbolBars()
        {
            var intervals = getDataIntervals();

            lock (_barRequests)
            {
                _totalBarRequest = 0;
                _barRequests.Clear();

                //string benchMarkSymbol = _model.Benchmark;
                //foreach (var interval in intervals)
                //{
                //    if (_stop) break;
                //    var key1 = benchMarkSymbol + ":" + interval;
                //    if (!_barRequests.Contains(key1))
                //    {
                //        _barRequests.Add(key1);
                //        _barCache.RequestBars(benchMarkSymbol, interval, false, BarServer.MaxBarCount, false);
                //    }
                //}

                foreach (var symbol in _model.Symbols)
                {
                    if (_stop) break;
                    var ticker = symbol.Ticker;
                    foreach (var interval in intervals)
                    {
                        var key = ticker + ":" + interval;
                        if (!_barRequests.Contains(key))
                        {
                            _barRequests.Add(key);
                            _barCache.RequestBars(ticker, interval, false, BarServer.MaxBarCount, false);
                        }
                    }
                }
                _totalBarRequest = _barRequests.Count;
            }
        }

        private void barChanged(object sender, BarEventArgs e)
        {
            if (e.Type == BarEventArgs.EventType.BarsReceived)
            {
                string ticker = e.Ticker;
                string interval = e.Interval;

                _progressCompletedNumber++;
                sendProgressEvent();

                if (_indexBarRequestCount > 0)
                {
                    Series[] indexSeries = _barCache.GetSeries(ticker, interval, new string[] { "Close" }, 0, BarServer.MaxBarCount);
                    if (indexSeries.Length > 0 && indexSeries[0].Count > 0)
                    {
                        _referenceData[ticker + ":" + interval] = indexSeries[0];
                    }

                    if (--_indexBarRequestCount == 0)
                    {
                        requestSymbolBars();
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

                            if (_barRequests.Count == 0)
                            {
                                _waitForBars = false;
                            }
                        }
                    }
                }
            }
        }

        private void trainModel()
        {
            try
            {
                var tickers = _model.Symbols.Select(x => x.Ticker).ToList();

                var maxBars = int.Parse(_model.MLMaxBars);

                var intervals = getModelIntervals();

                _progressState = ProgressState.ProcessingData;
                _progressTotalNumber = _model.Symbols.Count * intervals.Count;
                _progressCompletedNumber = 0;
                sendProgressEvent();

                foreach (var interval in intervals)
                {
                    if (!_model.UseTicker)
                    {
                        var data1 = new List<List<string>>();
                        foreach (var ticker in tickers)
                        {
                            if (_stop) break;
                            var thisTicker = new List<string>();
                            thisTicker.Add(ticker);
                            var referenceData1 = getFundamentalFeatures(ticker, interval);
                            var modelData = atm.getModelData(_model.FeatureNames, _model.Scenario, _barCache, thisTicker, interval, default(DateTime), default(DateTime), maxBars, _model.MLSplit, true, true);
                            data1.AddRange(modelData.data);
                            _model.Biases[ticker + ":" + interval] = modelData.bias;
                            _progressCompletedNumber++;
                            sendProgressEvent();
                        }

                        var data2 = data1.OrderBy(sample => sample[2].Split(';')[1]).ToList();

                        var pathName1 = @"senarios\" + _model.Name + @"\" + interval + @"\train.csv";
                        atm.saveModelData(pathName1, data2);
                    }
                    else
                    {
                        foreach (var ticker in tickers)
                        {
                            if (_stop) break;
                            var referenceData2 = getFundamentalFeatures(ticker, interval);

                            var modelData = atm.getModelData(_model.FeatureNames, _model.Scenario, _barCache, (new string[] { ticker }).ToList(), interval, default(DateTime), default(DateTime), maxBars, _model.MLSplit, true, false);
                            var data2 = modelData.data;
                            _model.Biases[ticker + ":" + interval] = modelData.bias;

                            var pathName2 = @"senarios\" + _model.Name + @"\" + interval + @"\" + MainView.ToPath(ticker) + @"\train.csv";
                            atm.saveModelData(pathName2, data2);

                            _progressCompletedNumber++;
                            sendProgressEvent();
                        }
                    }
                }

                var seconds = (int.Parse(_model.MLMaxTime) + 2) * (intervals.Count * (_model.UseTicker ? tickers.Count : 1));
                _progressState = ProgressState.Training;
                _progressTime = DateTime.Now + new TimeSpan(0, 0, seconds);
                sendProgressEvent();

                foreach (var interval in intervals)
                {
                    if (!_model.UseTicker)
                    {
                        var pathName1 = @"senarios\" + _model.Name + @"\" + interval;
                        MainView.AutoMLTrain(pathName1, MainView.GetSenarioLabel(_model.Scenario), _model.MLMaxTime, _model.MLRank, _model.MLSplit.Substring(0, 2), _model.Trained);
                        _progressCompletedNumber++;
                        sendProgressEvent();
                    }
                    else
                    {

                        for (var ii = 0; ii < tickers.Count; ii++)
                        {
                            if (_stop) break;
                            var ticker = tickers[ii];
                            var pathName2 = @"senarios\" + _model.Name + @"\" + interval + @"\" + MainView.ToPath(ticker);
                            MainView.AutoMLTrain(pathName2, MainView.GetSenarioLabel(_model.Scenario), _model.MLMaxTime, _model.MLRank, _model.MLSplit.Substring(0, 2), _model.Trained);
                            _progressCompletedNumber++;
                             seconds = (int.Parse(_model.MLMaxTime) + 2) * (intervals.Count *  (tickers.Count - ii));
                            _progressTime = DateTime.Now + new TimeSpan(0, 0, seconds);
                            sendProgressEvent();
                        }
                    }
                }

                _model.Trained = true;
                saveModel();
            }
            catch (Exception x)
            {
                //Trace.WriteLine("Train model exception: " + x.Message);
            }

            _progressState = ProgressState.Finished;
            sendProgressEvent();
            Thread.Sleep(500);

            _progressState = ProgressState.Complete;
            sendProgressEvent();
        }

        private void timerEvent(object source, ElapsedEventArgs e)
        {
            sendProgressEvent();
        }

        private void saveModel()
        {
            var path = @"senarios\" + _model.Name + @"\model";
            var data = _model.save();
            MainView.SaveUserData(path, data);
        }
    }
}