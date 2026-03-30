using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Threading;
using System.Windows.Threading;
using System.Timers;
using System.IO;

namespace ATMML
{
    public class ScanRequest
    {
        private Scan _scan;
        private string _symbol;
        private string _condition;

        public ScanRequest(Scan scan, string symbol, string condition)
        {
            _scan = scan;
            _symbol = symbol;
            _condition = condition;
        }

        public Scan Scan
        {
            get { return _scan; }
        }

        public string Symbol
        {
            get { return _symbol; }
        }

        public string Condition
        {
            get { return _condition; }
        }

    }

    public class ScanResult
    {
        private string _symbol;
        private string _interval;
        private int _side;
        private bool _enable;

        public ScanResult(string symbol, string interval, int side)
        {
            _enable = true;
            _symbol = symbol;
            _interval = interval;
            _side = side;
        }

        public string Symbol
        {
            get { return _symbol; }
        }

        public string Interval
        {
            get { return _interval; }
        }

        public int Side
        {
            get { return _side; }
        }

        public bool Enable
        {
            get { return _enable; }
            set { _enable = value; }
        }
    }

    public class ScanThread
    {
        bool _runThread = false;
        Thread _thread = null;
        List<ScanRequest> _requestList = new List<ScanRequest>();

        public ScanThread()
        {
            _runThread = true;
            _thread = new Thread(new ThreadStart(calculate));
            _thread.Start();
        }

        public void AddRequest(ScanRequest scanRequest)
        {
            lock (_requestList)
            {
                bool found = false;
                foreach (ScanRequest request in _requestList)
                {
                    if (request.Scan == scanRequest.Scan && request.Condition == scanRequest.Condition && request.Symbol == scanRequest.Symbol)
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    _requestList.Add(scanRequest);
                }
            }
        }

        public void RemoveScan(Scan scan)
        {
            lock (_requestList)
            {
                List<ScanRequest> remove = new List<ScanRequest>();
                foreach (ScanRequest request in _requestList)
                {
                    if (request.Scan == scan)
                    {
                        remove.Add(request);
                    }
                }
                foreach (ScanRequest request in remove)
                {
                    _requestList.Remove(request);
                }
            }
        }

        public void Close()
        {
            _runThread = false;
           // _thread.Join();
        }

        private Series getPredictionSeries(Model model, BarCache barCache, string ticker, string interval, int count)
        {
            Series output = new Series(count, 0.0);

            var pathName = @"senarios\" + model.Name + @"\" + interval + (model.UseTicker ? @"\" + MainView.ToPath(ticker) : "");
            var data = atm.getModelData(model.FeatureNames, model.Scenario, barCache, (new string[] { ticker }).ToList(), interval, count, model.MLSplit, false, false);
            atm.saveModelData(pathName + @"\test.csv", data);
            var predictions = MainView.AutoMLPredict(pathName, MainView.GetSenarioLabel(model.Scenario));

            if (predictions.ContainsKey(ticker) && predictions[ticker].ContainsKey(interval))
            {
                var times = barCache.GetTimes(ticker, interval, 0, count);
                for (int ago = 0; ago < count; ago++)
                {
                    var date = times[count - ago - 1];
                    var prediction = predictions[ticker][interval].ContainsKey(date) ? predictions[ticker][interval][date] : 0;
                    output[count - ago - 1] = prediction;
                }
            }
            return output;
        }

        private void calculate()
        {
            int requestsPerSecond = 200;  // 4 is smallest

            int sleepMilliseconds = 100;
            double loopFrequency = 1000.0 / sleepMilliseconds;
            int maxRequestsPerLoop = (int)(requestsPerSecond / loopFrequency + 0.5);

            List<ScanRequest> requestList = new List<ScanRequest>();

            while (_runThread)
            {
                lock (_requestList)
                {
                    foreach (var request in requestList)
                    {
                        request.Scan.SetProgressTime();
                    }

                    int count1 = _requestList.Count;
                    if (count1 > 0)
                    {
                        for (int ii = 0; ii < count1; ii++)
                        {
                            Scan scan = _requestList[ii].Scan;
                            if (!scan.Paused)
                            {
                                if (requestList.Count < maxRequestsPerLoop)
                                {
                                    requestList.Add(_requestList[ii]);
                                }
                                else
                                {
                                    break;
                                }
                            }
                        }

                        _requestList.RemoveRange(0, requestList.Count);
                    }
                }

                int count2 = requestList.Count;
                for (int ii = 0; ii < count2; ii++)
                {
                    Scan scan = requestList[ii].Scan;
                    string symbol = requestList[ii].Symbol;
                    string compoundCondition = requestList[ii].Condition;

                    List<string> intervals = scan.getIntervals();
                    string resultInterval = scan.getLowestInterval();

                    BarCache barCache = scan.BarCache;

                    bool ok = true;
                    Dictionary<string, List<DateTime>> times = new Dictionary<string, List<DateTime>>();
                    Dictionary<string, Series[]> bars = new Dictionary<string, Series[]>();
                    foreach (string interval in intervals)
                    {
                        times[interval] = (barCache.GetTimes(symbol, interval, 0));
                        bars[interval] = barCache.GetSeries(symbol, interval, new string[] { "Open", "High", "Low", "Close" }, 0);
                        if (times[interval] == null || times[interval].Count == 0)
                        {
                            ok = false;
                            break;
                        }
                    }

                    if (ok)
                    {
                        if (scan.IsReferenceDataRequired)
                        {
                            ok = false;
                            if (scan.ReferenceDataCount >= 0)
                            {
                                ok = true;
                            }
                        }
                    }

                    if (ok)
                    {
                        int barCount = scan.BarCount;

                        Series[] signal1 = {null, null};
                        Series[] signal2 = {new Series(barCount, 0), new Series(barCount, 0)};
                        bool[] first = {true, true};

                        //Dictionary<string, object> referenceData = scan.ReferenceData;
                        Dictionary<string, object> referenceData = new Dictionary<string, object>();

                        string indexSymbol = "";


                        lock (scan.ReferenceData)
                        {
                            foreach (var kvp in scan.ReferenceData)
                            {
                                string key = kvp.Key;
                                string[] fields = key.Split(':');
                                if (fields.Length >= 2)
                                {
                                    string ticker = fields[0];
                                    string name = fields[1];
                                    string date = (fields.Length > 2) ? ":" + fields[2] : "";

                                    if (symbol == ticker)
                                    {
                                        if (name != "DS192")
                                        {
                                            if (name == "REL_INDEX")
                                            {
                                                indexSymbol = scan.ReferenceData[symbol + ":" + "REL_INDEX"] as string;
                                            }
                                            else
                                            {
                                                double value;
                                                referenceData[name + date] = (double.TryParse(scan.ReferenceData[symbol + ":" + name + date].ToString(), out value)) ? value : double.NaN;
                                            }
                                        }
                                    }
                                }
                            }

                            if (indexSymbol.Length > 0)
                            {
                                List<string> indexIntervals = scan.GetIndexIntervals();
                                foreach (string indexInterval in indexIntervals)
                                {
                                    if (scan.ReferenceData.ContainsKey(indexSymbol + ":" + indexInterval))
                                    {
                                        referenceData["Index Prices : " + indexInterval] = scan.ReferenceData[indexSymbol + ":" + indexInterval];
                                    }
                                }
                            }
                        }

                        lock (scan.ReferenceData)
                        {

                            //Dictionary<string, object> referenceData = new Dictionary<string, object>();
                            foreach (KeyValuePair<string, object> kvp in scan.ReferenceData)
                            {
                                string name = kvp.Key;
                                object value = kvp.Value;
                                string[] fields = name.Split(':');
                                if (fields.Length >= 2)
                                {
                                    string ticker = fields[0];
                                    if (ticker == symbol)
                                    {
                                        string referenceDataName = fields[1];
                                        referenceData[referenceDataName] = value;
                                    }
                                }
                            }
                        }

                        List<string> modelNames = MainView.getModelNames();

                        string[] conditions = compoundCondition.Split('\u0001');
                        foreach (string condition in conditions)
                        {
                            string[] field = condition.Split('\u0002');
                            string name = field[0];
                            string interval = (field.Length > 1) ? field[1].Replace(" Min", "") : "Daily";
                            if (interval.Length == 0) interval = "Daily";
                            string ago = (field.Length > 2) ? field[2] : "10000";

                            Series[] output = { null, null };
                            string[] intervalList = Conditions.GetIntervals(condition + "\u0002" + interval).ToArray();

                            bool stop = false;

                            if (modelNames.Contains(name))
                            {
                                var model = MainView.GetModel(name);
                                var predictions = getPredictionSeries(model, scan.BarCache, symbol, interval, barCount + ago.Length);
                                output[0] = predictions >= 0.5;
                                output[1] = predictions < 0.5;
                            }
                            else if (name == "ATM Pxf UP")
                            {
                                if (MainView.SelectedMLModel != null)
                                {
                                    var predictions = getPredictionSeries(MainView.SelectedMLModel, scan.BarCache, symbol, interval, barCount + ago.Length);
                                    output[0] = predictions >= 0.5;
                                    output[1] = predictions < 0.5;
                                }
                            }
                            else if (name == "ATM Pxf Dn")
                            {
                                if (MainView.SelectedMLModel != null)
                                {
                                    var predictions = getPredictionSeries(MainView.SelectedMLModel, scan.BarCache, symbol, interval, barCount + ago.Length);
                                    output[0] = predictions < 0.5;
                                    output[1] = predictions >= 0.5;
                                }
                            }
                            else if (name.Length >= 13 && name.Substring(0, 13) == "REMOVE SYMBOL")
                            {
                                bool match = false;
                                string[] fields1 = name.Split('\u0004');
                                if (fields1.Length >= 2)
                                {
                                    int minLength = Math.Min(symbol.Length, fields1[1].Length);
                                    string sym1 = symbol.Substring(0, minLength);
                                    string sym2 = fields1[1].Substring(0, minLength);

                                    match = (sym1.Equals(sym2, StringComparison.OrdinalIgnoreCase));
                                    stop = match;
                                }
                                output[0] = new Series(barCount, match ? 0.0 : 1.0);
                                output[1] = new Series(barCount, match ? 0.0 : 1.0);
                            }
                            else if (name == "X Alert" || name == "First Alert" || name == "Add On Alert" || name == "Pull Back Alert" || name == "Exhaustion Alert" || name == "Divergence Alert" || name == "Pressure Alert" || name == "Two Bar Alert" || name == "All X Signals")
                            //else if (name == "X Alert" || name == "First Alert" || name == "Add On Alert" || name == "Pull Back Alert" || name == "Exhaustion Alert" || name == "Divergence Alert" || name == "Pressure Alert" || name == "PT Alert" || name == "Two Bar Trend"|| name == "Two Bar Alert" || name == "All X Signals")
                            {
                                Series signal = Conditions.Calculate(name, symbol, intervalList, barCount, times, bars, referenceData);
                                output[0] = signal.Replace(-1, 0);
                                output[1] = signal.Replace(1, 0).Replace(-1, 1);
                            }
                            else
                            {
                                for (int side = 0; side < 2; side++)
                                {
                                    string conditionName = (side == 0) ? name : Conditions.GetReverseCondition(name);
                                    output[side] = Conditions.Calculate(conditionName, symbol, intervalList, barCount, times, bars, referenceData);
                                }
                            }

                             for (int side = 0; side < 2; side++)
                             {
                                 Series series = output[side];

                                 if (interval != resultInterval)
                                 {
                                    List<DateTime> times1 = times[interval];
                                    List<DateTime> times2 = times[resultInterval];

                                    int cnt = series.Count;

                                    int cnt1 = times1.Count;
                                    int cnt2 = times2.Count;

                                    int sidx1 = cnt1 - 1;

                                    signal1[side] = new Series(cnt2, 0);

                                    string i1 = interval.Substring(0, 1);
                                    string i2 = resultInterval.Substring(0, 1);
                                    int keySize1 = (i1 == "Y") ? 4 : ((i1 == "S" || i1 == "Q" || i1 == "M") ? 6 : ((i1 == "W" || i1 == "D") ? 8 : 12));
                                    int keySize2 = (i2 == "Y") ? 4 : ((i2 == "S" || i2 == "Q" || i2 == "M") ? 6 : ((i2 == "W" || i2 == "D") ? 8 : 12));
                                    int keySize = Math.Min(keySize1, keySize2);

                                    int offset = 0;
                                    if (i1 == "W" && keySize == 8) offset = 5;  //to place at current offset = 0
                                    else if (i1 == "Q" && keySize == 6) offset = 3;  //to place at current offset = 0
                                    else if (i1 == "S" && keySize == 6) offset = 6;  //to place at current offset = 0

                                    //string logText = symbol + " " + times1[cnt1 - 1].ToString("yyyyMMdd HH:mm") + " " + times2[cnt2 - 1].ToString("yyyyMMdd HH:mm");
                                    //Log.Add(logText);

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

                                                for (int kk = 0; kk < ago.Length; kk++)
                                                {
                                                    if (ago[kk] == '1')
                                                    {
                                                        if (series[idx - kk] == 1)
                                                        {
                                                            signal1[side][idx2] = 1;
                                                        }
                                                    }
                                                }

                                                sidx1 = idx1;
                                                break;
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    signal1[side] = new Series(series.Count, 0);
                                    for (int idx = ago.Length; idx < series.Count; idx++)
                                    {
                                        for (int kk = 0; kk < ago.Length; kk++)
                                        {
                                            if (ago[kk] == '1')
                                            {
                                                if (series[idx - kk] == 1)
                                                {
                                                    signal1[side][idx] = 1;
                                                }
                                            }
                                        }
                                    }
                                }

                                int c1 = signal2[side].Count;
                                int c2 = signal1[side].Count;
                                for (int idx = 0; idx < c2; idx++)
                                {
                                    int index1 = c1 - 1 - idx;
                                    int index2 = c2 - 1 - idx;
                                    if (index1 >= 0 && index2 >= 0)
                                    {
                                        if (first[side] || stop)
                                        {
                                            signal2[side][index1] = signal1[side][index2];
                                        }
                                        else
                                        {
                                            signal2[side][index1] = (signal1[side][index2] == 1 && signal2[side][index1] == 1) ? 1 : 0;
                                        }
                                    }
                                }
                                first[side] = false;
                            }

                            if (stop)
                            {
                                break;
                            }
                        }

                        for (int side = 0; side < 2; side++)
                        {
                            int count = Math.Min(signal2[side].Count, times[resultInterval].Count);
                            for (int ago = 0; ago < count; ago++)
                            {
                                int index = times[resultInterval].Count - 1 - ago;
                                DateTime date = times[resultInterval][index];
                                int sig1 = (int)(signal2[side][count - 1 - ago]);
                                if (sig1 != 0)
                                {
                                    ScanResult scanResult = new ScanResult(symbol, resultInterval, side);
                                    scan.AddResult(date, scanResult);
                                }
                                else
                                {
                                    scan.DisableResult(date, symbol, side);
                                }

                                if (ago == 0)
                                {
                                    scan.SetCurrentDateTime(date, true);
                                }
                            }
                        }
                        scan.SymbolComplete(symbol);
                    }
                }
                requestList.Clear();

                Thread.Sleep(sleepMilliseconds);
            }
        }
    }

    public class ScanSummary : IComparable<ScanSummary>
    {
        DateTime _date;
        bool _currentDateTime = false;
        string _interval;  // determines times text format
        List<ScanResult> _results = new List<ScanResult>();

        public ScanSummary(DateTime date, string interval)
        {
            _date = date;
            _interval = interval;
        }

        public int CompareTo(ScanSummary other)
        {
            int val = -(_date.CompareTo(other._date));
            return val;
        }

        public List<ScanResult> GetResults()
        {
            List<ScanResult> results;
            lock (_results)
            {
                results = new List<ScanResult>(_results);
            }
            return results;
        }

        public string GetTimeText()
        {
            DateTime date = _date;
            DateTime time1 = DateTime.UtcNow;
            DateTime time2 = DateTime.Now;
            TimeSpan timeSpan = time2 - time1;
            string format = (_interval == "Daily" || _interval == "Weekly") ? "MMM d" : ((_interval == "Monthly" || _interval == "Quarterly") ? "MMM yy" : "MMM d ddd HH:mm");
            if (format == "MMM d ddd HH:mm")
            {
                date += timeSpan;
            }
            string timeText = date.ToString(format);
            return timeText;
        }

        public void DisableResult(string symbol,int side)
        {
            lock (_results)
            {
                foreach (ScanResult result in _results)
                {
                    if (result.Symbol == symbol && result.Side == side)
                    {
                        result.Enable = false;
                        break;
                    }
                }
            }
        }

        public void AddResult(ScanResult scanResult)
        {
            bool add = true; 
            
            lock (_results)
            {
                foreach (ScanResult result in _results)
                {
                    if (result.Symbol == scanResult.Symbol)
                    {
                        add = false;
                        result.Enable = true;
                        break;
                    }
                }

                if (add)
                {
                    _results.Add(scanResult);
                }
            }
        }

        public DateTime Date
        {
            get { return _date; }
        }

        public bool CurrentDateTime
        {
            get { return _currentDateTime; }
            set { _currentDateTime = value; }
        }

        public string Interval
        {
            get { return _interval; }
        }
    }

    public delegate void ScanStartedEventHandler(object sender, EventArgs e);
    public delegate void ScanProgressEventHandler(object sender, EventArgs e);
    public delegate void ScanCompleteEventHandler(object sender, EventArgs e);

    public class Scan
    {
        public event ScanStartedEventHandler ScanStarted;
        public event ScanProgressEventHandler ScanProgress;
        public event ScanCompleteEventHandler ScanComplete;

        private ScanThread _scanThread;
        private int _scanInterval;
        private string _title = "";
        private string _portfolioName;
        private string _condition;
        private string _interval;
        private int _barCount;
        private List<string> _progressList = new List<string>();
        private DateTime _progressTime;
        private bool _running = false;
        private DateTime _endTime;
        private object _symbolLock = new object();
        private List<Symbol> _symbols = new List<Symbol>();
        private bool _paused = false;
        private bool _referenceDataRequired = false;
        private Dictionary<string, object> _referenceData = new Dictionary<string, object>();

        BarCache _barCache;
        Portfolio _portfolio = new Portfolio(12);

        Dictionary<DateTime, ScanSummary> _results = new Dictionary<DateTime, ScanSummary>();
        Dictionary<DateTime, ScanSummary> _savedResults = null;

        System.Timers.Timer _timer;
        List<ScanBarRequest> _requestBarList = new List<ScanBarRequest>();

        public int BarCount { get { return _barCount; } }
        
        public void SetProgressTime()
        {
            _progressTime = DateTime.Now;
        }

        public bool IsRunning
        {
            get { return _running && !_paused; }
        }

        public bool IsReferenceDataRequired
        {
            get { return _referenceDataRequired; }
        }

        public Dictionary<string, object> ReferenceData
        {
            get { return _referenceData; }
        }

        public bool Paused
        {
            get { return _paused; }
            set { _paused = value; }
        }

        public DateTime EndTime
        {
            get { return _endTime; }
        }

        public string GetLastTime()
        {
            string text = "00:00";
            if (_endTime.Year > 1)
            {
                text = _endTime.ToString("HH:mm");
            }
            return text;
        }

        public string GetNextTime()
        {
            string text = "0:00";
            if (_endTime.Year > 1)
            {
                DateTime time = _endTime + new TimeSpan(0, _scanInterval, 0);
                if (time >= DateTime.Now)
                {
                    TimeSpan span = time - DateTime.Now;
                    text = span.Minutes.ToString("0") + ":" + span.Seconds.ToString("00");
                }
            }
            return text;
        }

        public string GetDescription(string ticker)
        {
            string description = "";
            lock (_symbolLock)
            {
                foreach (Symbol symbol in _symbols)
                {
                    if (symbol.Ticker == ticker)
                    {
                        description = symbol.Description;
                        break;
                    }
                }
            }
            return description;
        }

        private class ScanBarRequest
        {
            private string _symbol;
            private string _interval;

            public ScanBarRequest(string symbol, string interval)
            {
                _symbol = symbol;
                _interval = interval;
            }

            public string Symbol
            {
                get { return _symbol; }
                set { _symbol = value; }
            }

            public string Interval
            {
                get { return _interval; }
                set { _interval = value; }
            }
        }

        public int ScanInterval
        {
            get { return _scanInterval; }
            set { _scanInterval = value; }
        }

        public Scan(string title, string portfolio, string condition, ScanThread scanThread, int scanInterval, int barCount = 6)
        {
            _title = title;
            _portfolioName = portfolio;
            _condition = condition;
            _scanThread = scanThread;
            _scanInterval = scanInterval;
            _barCount = barCount;
            _portfolio.PortfolioChanged += new PortfolioChangedEventHandler(portfolioChanged);
            _barCache = new BarCache(barChanged);

            _timer = new System.Timers.Timer(250);  // 4 per second
            _timer.Elapsed += new ElapsedEventHandler(ScanTimer);
            _timer.AutoReset = true;
            _timer.Start();
        }

        public bool HasDefaultTitle()
        {
            return (_title.Length == 0 || _title == GetTitle(_portfolioName, _condition));
        }

        public string Title
        {
            get { return (_title.Length > 0) ? _title : GetTitle(_portfolioName, _condition); }
        }

        public static string GetTitle(string portfolioText, string conditionText)
        {
            string title = "";

            string[] portfolios = portfolioText.Split('\u0001');
            foreach (string portfolio in portfolios)
            {
                if (portfolio.Length > 0)
                {
                    if (title.Length > 0) title += " ";
                    title += portfolio;
                }
            }

            string[] conditions = conditionText.Split('\u0001');
            int index = 0;
            foreach (string condition in conditions)
            {
                if (condition.Length > 0)
                {
                    string[] field = condition.Split('\u0002');
                    if (field.Length >= 2)
                    {
                        string conditionName = field[0];
                        string conditionInterval = field[1];
                        title += (index++ == 0) ? "   " : " & ";
                        title += " " + conditionInterval + " " + conditionName;
                    }
                    else
                    {
                        title += (index++ == 0) ? "   " : " & ";
                        title += " " + condition;
                    }
                }
            }
            return title;
        }

        public List<ScanSummary> GetScanSummary()
        {
            List<ScanSummary> output;
            lock (_results)
            {
                bool useSavedResults = (_running && _savedResults != null && _savedResults.Count > 0);
                output = new List<ScanSummary>(useSavedResults ? _savedResults.Values : _results.Values);
            }
            output.Sort();
            return output;
        }

        public void Close()
        {
            _scanThread.RemoveScan(this);
            _portfolio.Close();
            _barCache.Clear();
        }

        public string PortfolioName
        {
            get { return _portfolioName; }
            set { _portfolioName = value; }
        }

        public string Condition
        {
            get { return _condition; }
            set { _condition = value; }
        }

        public BarCache BarCache
        {
            get { return _barCache; }
        }

        public void Run()
        {
            if (!_running && !_paused)
            {
                lock (_results)
                {
                    _running = true;
                    _savedResults = _results;

                    // initialize results 
                    _results = new Dictionary<DateTime, ScanSummary>();
                    foreach (KeyValuePair<DateTime, ScanSummary> kvp in _savedResults)
                    {
                        DateTime dateTime = kvp.Key;
                        ScanSummary scanSummary = kvp.Value;
                        List<ScanResult> results = scanSummary.GetResults();
                        foreach (ScanResult result in results)
                        {
                            ScanResult scanResult = new ScanResult(result.Symbol, result.Interval, result.Side);
                            scanResult.Enable = result.Enable;
                            AddResult(dateTime, scanResult);
                        }
                    }
                }

                _referenceDataRequired = isReferenceDataRequired();
                _interval = getLowestInterval();

                if (ScanStarted != null)
                {
                    ScanStarted(this, new EventArgs());
                }

                lock (_symbolLock)
                {
                    _symbols.Clear();

                    lock (_referenceData)
                    {
                        _referenceData.Clear();
                    }

                    string[] portfolios = _portfolioName.Split('\u0001');
                    _requestPortfolioCount = portfolios.Length;
                    foreach (string name in portfolios)
                    {
                        bool isEQS = (name.Length >= 1 && name.Substring(0, 1) == "\u0005");
                        bool isPRTU = (name.Length >= 1 && name.Substring(0, 1) == "\u0006");
                        bool isModel = (name.Length >= 1 && name.Substring(0, 1) == "\u0007");
                        bool isSpread = (name.Length >= 1 && name.Substring(0, 1) == "\u0008");
                        bool isWorksheet = (name.Length >= 1 && name.Substring(0, 1) == "\u001e");

                        var portfolioType = Portfolio.PortfolioType.Index;
                        var portfolioName = name;
                        if (isEQS)
                        {
                            portfolioType = Portfolio.PortfolioType.EQS;
                            portfolioName = name.Substring(1);
                        }
                        else if (isPRTU)
                        {
                            portfolioType = Portfolio.PortfolioType.PRTU;
                            portfolioName = name.Substring(1);
                        }
                        else if (isSpread)
                        {
                            portfolioType = Portfolio.PortfolioType.Spread;
                            portfolioName = name.Substring(1);
                        }
                        else if (isModel)
                        {
                            portfolioType = Portfolio.PortfolioType.Model;
                            portfolioName = name.Substring(1);
                        }
                        else if (isWorksheet)
                        {
                            portfolioType = Portfolio.PortfolioType.Worksheet;
                            portfolioName = name.Substring(1);
                        }

                        _portfolio.RequestSymbols(portfolioName, portfolioType, false);
                    }
                }
            }
        }

        public bool HasTimeChanged(int maxCount)
        {
            bool ok = true;
            lock (_results)
            {
                if (_savedResults != null && _savedResults.Count != 0)
                {
                    List<DateTime> newDateTimes = new List<DateTime>();
                    foreach (ScanSummary summary in _results.Values)
                    {
                        newDateTimes.Add(summary.Date);
                    }

                    List<DateTime> oldDateTimes = new List<DateTime>();
                    foreach (ScanSummary summary in _savedResults.Values)
                    {
                        oldDateTimes.Add(summary.Date);
                    }

                    int count1 = Math.Min(maxCount, newDateTimes.Count);
                    int count2 = Math.Min(maxCount, oldDateTimes.Count);
                    if (count1 == count2)
                    {
                        ok = false;
                        for (int ii = 0; ii < count1; ii++)
                        {
                            if ((newDateTimes[ii].CompareTo(oldDateTimes[ii].Date)) != 0)
                            {
                                ok = true;
                                break;
                            }
                        }
                    }
                }
            }
            return ok;
        }

        public void Stop()
        {
            if (_running)
            {
                if (_portfolio != null)
                {
                    _portfolio.PortfolioChanged -= new PortfolioChangedEventHandler(portfolioChanged);
                }

                if (_barCache != null)
                {
                    _barCache.Clear();
                }

                _barCache = new BarCache(barChanged);
                _portfolio = new Portfolio(13);

                _portfolio.PortfolioChanged += new PortfolioChangedEventHandler(portfolioChanged);

                lock (_progressList)
                {
                    _progressList.Clear();
                }

                lock (_results)
                {
                    _running = false;
                    _results.Clear();
                    _savedResults = null;
                }
                 _endTime = DateTime.Now;
            }
         }

        private List<string> getRequiredReferenceData()
        {
            List<string> output = new List<string>();
            string[] conditions = _condition.Split('\u0001');
            foreach (string condition in conditions)
            {
                if (condition.Length > 0)
                {
                    string[] fields = condition.Split('\u0004');
                    if (fields.Length >= 3)
                    {
                        output.Add(Conditions.GetFieldNameFromDescription(fields[0].Trim()));
                    }
                    else
                    {
                        output.AddRange(Conditions.GetRequiredReferenceData(condition));
                    }
                }
            }
            output = output.Distinct().ToList();
            return output;
        }

        private bool isReferenceDataRequired()
        {
            return getRequiredReferenceData().Count > 0;
        }

        public string getLowestInterval()
        {
            string[] intervals = { "Quarterly", "Monthly", "Weekly", "Daily", "240", "120", "60", "30", "15", "5"};

            int index1 = 0;
            string[] conditions = _condition.Split('\u0001');
            foreach (string condition in conditions)
            {
                if (condition.Length > 0)
                {
                    if (condition.Length > 0)
                    {
                        string[] field = condition.Split('\u0002');
                        string conditionName = field[0];
                        if (field.Length >= 2)
                        {
                            string conditionInterval = field[1].Replace(" Min", "");
                            if (conditionInterval.Length == 0) conditionInterval = "Daily";
                            int index2 = 0;
                            foreach (string interval in intervals)
                            {
                                if (interval == conditionInterval)
                                {
                                    if (index2 > index1)
                                    {
                                        index1 = index2;
                                    }
                                    break;
                                }
                                index2++;
                            }
                        }
                        else
                        {
                            index1 = 3;
                        }
                    }
                }
            }
            return intervals[index1];
        }

        public List<string> getIntervals()
        {
            return Conditions.GetIntervals(_condition);
        }

        public void DisableResult(DateTime dateTime, string symbol, int side)
        {
            lock (_results)
            {
                if (_results.ContainsKey(dateTime))
                {
                    _results[dateTime].DisableResult(symbol, side);
                }
            }
        }

        public void AddResult(DateTime dateTime, ScanResult scanResult)
        {
            lock (_results)
            {
                if (!_results.ContainsKey(dateTime))
                {
                    _results[dateTime] = new ScanSummary(dateTime, _interval);
                }
                _results[dateTime].AddResult(scanResult);
            }
        }

        public void SetCurrentDateTime(DateTime dateTime, bool currentDateTime)
        {
            lock (_results)
            {
                if (_results.ContainsKey(dateTime))
                {
                    _results[dateTime].CurrentDateTime = currentDateTime;
                }
            }
        }

        public int ReferenceDataCount { get { return _referenceDataCount; } }

        void requestReferenceData()
        {

            List<string> dataFieldNames = getRequiredReferenceData();
            //dataFieldNames.Add("REL_INDEX");

            lock (_symbolLock)
            {
                int count = _symbols.Count;
                _referenceDataCount = 0;
                for (int ii = 0; ii < count; ii++)
                {
                    var ticker = _symbols[ii].Ticker; 
                    if (!ticker.Contains(" Index"))
                    {
                        _portfolio.RequestReferenceData(ticker, dataFieldNames.ToArray());
                        _referenceDataCount++;
                    }
                }

                if (_referenceDataCount == 0)
                {
                    addRequests();
                }
            }
        }

        int _requestPortfolioCount = 0;
        int _referenceDataCount = 0;
        int _indexBarRequestCount = 0;

        void portfolioChanged(object sender, PortfolioEventArgs e)
        {

            if (e.Type == PortfolioEventType.Symbol)
            {
                lock (_symbolLock)
                {
                    _symbols = _portfolio.GetSymbols();

                    if (_requestPortfolioCount > 0)
                    {
                        _requestPortfolioCount--;
                        if (_requestPortfolioCount == 0)
                        {
                            //todo
                            requestReferenceData();
                            addRequests();
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
                    if (name == "REL_INDEX") value += " Index";
                    string key = e.Ticker + ":" + name;

                    lock (_referenceData)
                    {
						_referenceData[key] = value;

                        if (name == "REL_INDEX")
                        {
                            if (_referenceDataCount > 0) { 
                            if (--_referenceDataCount == 0)
                            {
                                List<string> indexSymbols = new List<string>();
                                foreach (KeyValuePair<string, object> kvp1 in _referenceData)
                                {
                                    string key1 = kvp1.Key;
                                    if (key1.Contains("REL_INDEX"))
                                    {
                                        string indexSymbol = kvp.Value as string;
                                        if (!indexSymbols.Contains(indexSymbol))
                                        {
                                            indexSymbols.Add(indexSymbol);
                                        }
                                    }
                                }

                                List<string> indexIntervals = GetIndexIntervals();

                                _indexBarRequestCount = indexSymbols.Count * indexIntervals.Count;

                                if (_indexBarRequestCount == 0)
                                {
                                    addRequests(); // request bars for the symbols in the portfolio
                                }
                                else  // request bar data for the related indexes
                                {
                                    foreach (string symbol in indexSymbols)
                                    {
                                        foreach (string interval in indexIntervals)
                                        {
                                            _barCache.RequestBars(symbol, interval, true, _barCache.MaxBarCount, false);
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

        public List<string> GetIndexIntervals()
        {
            List<string> indexIntervals = new List<string>();
            List<string> intervals = getIntervals();
            foreach (string interval in intervals)
            {
                if (!indexIntervals.Contains(interval))
                {
                    indexIntervals.Add(interval);
                }
            }
            return indexIntervals;
        }

        void addRequests()
        {
            lock (_progressList)
            {
                SetProgressTime();
                _progressList.Clear();
                lock (_symbolLock)
                {
                    foreach (Symbol symbol in _symbols)
                    {
                        _progressList.Add(symbol.Ticker);
                    }
                }
            }

            List<string> intervals = getIntervals();
            foreach (string interval in intervals)
            {
                lock (_symbolLock)
                {
                    foreach (Symbol symbol in _symbols)
                    {
                        string barInterval = interval.Replace(" Min", "");
                        lock (_requestBarList)
                        {
                            _requestBarList.Add(new ScanBarRequest(symbol.Ticker, barInterval));
                        }
                    }
                }
            }
        }

        public void SymbolComplete(string symbol)
        {
            SetProgressTime();

            int oldCount = 0;
            int newCount = 0;

            lock (_progressList)
            {
                oldCount = _progressList.Count;
                _progressList.Remove(symbol);
                newCount = _progressList.Count;
            }

            if (newCount == 0)
            {
                if (oldCount > 0)
                {
                    scanComplete();
                }
            }
            else
            {
                scanProgress();
            }
        }

        private void scanProgress()
        {
            if (ScanProgress!= null)
            {
                ScanProgress(this, new EventArgs());
            }
        }

        private void scanComplete()
        {
            lock (_results)
            {
                _running = false;
                _savedResults = null;
            }

            lock (_progressList)
            {
                _progressList.Clear();
            }

            _endTime = DateTime.Now;
            if (ScanComplete != null)
            {
                ScanComplete(this, new EventArgs());
            }
        }

        private void ScanTimer(object source, ElapsedEventArgs e)
        {
            for (int ii = 0; ii < 10; ii++)
            {
                ScanBarRequest request = null;
                lock (_requestBarList)
                {
                    if (_requestBarList.Count > 0)
                    {
                        request = _requestBarList[0];
                        _requestBarList.RemoveAt(0);
                        SetProgressTime();
                    }
                }

                if (request != null)
                {
                    if (_referenceDataRequired)
                    {
                        string[] dataFieldNames = getRequiredReferenceData().ToArray();
						_portfolio.RequestReferenceData(request.Symbol, dataFieldNames);
                    }
                    _barCache.RequestBars(request.Symbol, request.Interval, false);
                }
            }

            int progressCount = 0;
            lock (_progressList)
            {
                progressCount = _progressList.Count;
            }

            if (progressCount > 0) 
            {
                TimeSpan timeSpan = DateTime.Now - _progressTime;
                if (timeSpan.TotalSeconds >= 10) // 10 seconds since last pending request
                {
                    scanComplete();
                }
            }
        }
 
        private void barChanged(object sender, BarEventArgs e)
        {
            string ticker = e.Ticker;
            string interval = e.Interval;
            if (e.Type == BarEventArgs.EventType.BarsReceived)
            {
                if (_indexBarRequestCount > 0)
                {
                    Series[] indexSeries = _barCache.GetSeries(ticker, interval, new string[] { "Close" }, 0);
                    if (indexSeries.Length > 0 && indexSeries[0].Count > 0)
                    {
                        lock (_referenceData)
                        {
                            _referenceData[ticker + ":" + interval] = indexSeries[0];
                        }
                    }

                    if (--_indexBarRequestCount == 0)
                    {
                        addRequests();
                    }
                }
                else
                {

                    bool found = false;
                    lock (_progressList)
                    {
                        foreach (string symbol in _progressList)
                        {
                            if (ticker == symbol)
                            {
                                found = true;
                                break;
                            }
                        }
                    }

                    if (found)
                    {
                        found = false;
                        lock (_symbolLock)
                        {
                            foreach (Symbol symbol in _symbols)
                            {
                                if (symbol.Ticker == ticker)
                                {
                                    found = true;
                                    break;
                                }
                            }
                        }

                        if (found)
                        {
                            _scanThread.AddRequest(new ScanRequest(this, ticker, _condition));
                        }
                    }
                }
            }
        }

        private static ScanManager _scanManager = null;

        public static ScanManager Manager
        {
            get { if (_scanManager == null) _scanManager = new ScanManager(); return _scanManager; }
        }
    }

    public class ScanManager
    {
        private List<Scan> _scans = new List<Scan>();
        private ScanThread _scanThread;
        private  System.Timers.Timer _timer;

        public ScanManager()
        {
            _scanThread = new ScanThread();

            _timer = new System.Timers.Timer(100);
            _timer.Elapsed += new ElapsedEventHandler(ScanManagerTimer);
            _timer.AutoReset = true;
            _timer.Start();
        }

        public void Close()
        {
            foreach(var scan in _scans)
            {
                scan.Close();
            }
            _scanThread.Close();
        }

        private void ScanManagerTimer(object source, ElapsedEventArgs e)
        {
            lock (_scans)
            {
                foreach (Scan scan in _scans)
                {
                    if (!scan.IsRunning && !scan.Paused)
                    {
                        TimeSpan timeSpan = DateTime.Now - scan.EndTime;
                        if (timeSpan.TotalMinutes >= scan.ScanInterval)
                        {
                            scan.Run();
                        }
                    }
                }
            }
        }

        public Scan AddScan(string title, string portfolio, string condition, int scanInterval)
        {
            lock (_scans)
            {
                Scan scan = new Scan(title, portfolio, condition, _scanThread, scanInterval);
                _scans.Add(scan);
                return scan;
            }
        }

        public void RemoveScan(Scan scan)
        {
            lock (_scans)
            {
                scan.Close();
                _scans.Remove(scan);
            }
        }
    }
}
