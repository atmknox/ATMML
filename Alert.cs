using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Drawing.Printing;
using System.Globalization;
using System.Windows.Input;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace ATMML
{
    public delegate void ProgressEventHandler(object sender, EventArgs e);
    public delegate void FlashEventHandler(object sender, FlashEventArgs e);

    public class FlashEventArgs : EventArgs
    {
        public enum EventType
        {
            NewCondition,
            NewPosition,
            NewComment
        }

        EventType _type;

        public FlashEventArgs(EventType type)
        {
            _type = type;
        }

        public EventType Type
        {
            get { return _type; }
        }
    }

    public class Alert
    {
        public event ProgressEventHandler ProgressEvent;
        public event FlashEventHandler FlashEvent;

        public enum AlertViewType
        {
            Spreadsheet,
            DateTree,
            Comment
        }

        protected string _portfolioName = "WORLD EQUITY INDICES";

        protected TreeViewItem _treeViewItem = null;
        protected List<AlertLabel> _leftArrows = new List<AlertLabel>();
        protected List<AlertLabel> _rightArrows = new List<AlertLabel>();
        protected Dictionary<string, Notification> _notifications = new Dictionary<string, Notification>();
        private bool _newInfo = false;

        // properties stored in persistant service
        // --------------------------------------------
        // scan title
        // scan portfolio
        // scan condition
        protected int _rowCount = 6;
        protected bool _isExpanded = false;
        protected bool _notificationOn = true;
        protected bool _useOnTop = true;
        protected int _runInterval = 5;

        protected Color _textColor1 = Color.FromRgb(255, 140, 0);  // dark orange
        protected Color _textColor2 = Color.FromRgb(255, 158, 42);  // orange
        protected Color _textColor3 = Color.FromRgb(160, 160, 160);  // was white 255 255 255
		protected Color _textColor4 = Color.FromRgb(0, 204, 255);  // was white 255 255 255

		protected int _activeColumn = 0; // todo - add to persistence service
        protected AlertViewType _viewType = AlertViewType.Spreadsheet;

        public List<MouseEventInfo> mouseEvents = new List<MouseEventInfo>();

        protected bool _paused = false;

        public virtual void Close()
        {
        }

        public bool Paused
        {
            get { return _paused; }
            set { _paused = value; pauseOrResume(); }
        }

        public virtual bool Running
        { 
            get {return false; }
        }

        public virtual string GetInfoLine()
        {
            return "";
        }

        protected virtual void pauseOrResume()
        {
        }

        public virtual string Title
        {
            get { return ""; }
        }

        public virtual int GetHashMarkType(int index, string symbol)
        {
            int hashMarkType = -1;
            return hashMarkType;
        }

        public virtual string Portfolio
        {
            get { return _portfolioName; }
            set { setPortfolioName(value); }
        }

        protected virtual void setPortfolioName(string portfolioName)
        {
            _portfolioName = portfolioName;
        }

        public TradeHorizon TradeHorizon
        {
            get { return Trade.Manager.GetTradeHorizon(_portfolioName); }
        }

        public bool NewInfo
        {
            get { return _newInfo; }
            set { _newInfo = value; }
        }

        public virtual bool Editable
        {
            get { return false; }
        }

        public virtual bool Deleteable
        {
            get { return false; }
        }

        public virtual bool Scanable
        {
            get { return false; }
        }

        public virtual string GetLastTime()
        {
            return "";
        }

        public virtual string GetNextTime()
        {
            return "";
        }

        public virtual int GetDateTimeIndex(DateTime date)
        {
            int index = 0;
            return index;
        }

        public virtual bool IsBusy()
        {
            return false;
        }

        public virtual int GetColumnCount()
        {
            return 0;
        }

        public virtual string GetTimeText(DateTime dateTime)
        {
            string format = "MMM d";
            string timeText = dateTime.ToString(format);
            return timeText;
        }

        public virtual List<DateTime> GetDateTimes()
        {
            List<DateTime> dateTimes = new List<DateTime>();
            return dateTimes;
        }

        public virtual List<AlertItem> GetItems(DateTime dateTime)
        {
            List<AlertItem> items = new List<AlertItem>();
            return items;
        }

        public virtual bool HasTimeChanged(int dateCount)
        {
            return true;
        }

        public bool HasNotification(DateTime date)
        {
            bool ok = false;
            lock (_notifications)
            {
                if (_notificationOn) 
                {
                    foreach (Notification notification in _notifications.Values)
                    {
                        if (notification.Date == date)
                        {
                            ok = notification.FirstAdded > notification.LastVisited;
                            if (ok)
                            {
                                break;
                            }
                        }
                    }
                }
            }
            return ok;
        }

        public bool HasNotification(string notificationKey, DateTime date)
        {
            bool ok = false;
            lock (_notifications)
            {
                if (_notificationOn && _notifications.ContainsKey(notificationKey))  // todo - exact symbol match could cause an issue
                {
                    Notification notification = _notifications[notificationKey];
                    if (notification.Date == date)
                    {
                        ok = notification.FirstAdded > notification.LastVisited;
                    }
                }
            }
            return ok;
        }

        public Color TextColor1
        {
            get { return _textColor1; }
            set { _textColor1 = value; }
        }

        public Color TextColor2
        {
            get { return _textColor2; }
            set { _textColor2 = value; }
        }

        public Color TextColor3
        {
            get { return _textColor3; }
            set { _textColor3 = value; }
        }

		public Color TextColor4
		{
			get { return _textColor4; }
			set { _textColor3 = value; }
		}

		public AlertViewType ViewType
        {
            get { return _viewType; }
            set { _viewType = value; }
        }

        public bool NotificationOn
        {
            get { return _notificationOn; }
            set { _notificationOn = value; }
        }

        public bool UseOnTop
        {
            get { return _useOnTop; }
            set { _useOnTop = value; }
        }

        public int RunInterval
        {
            get { return _runInterval; }
            set { _runInterval = value; }
        }

        public TreeViewItem TreeViewItem
        {
            get { return _treeViewItem; }
            set { _treeViewItem = value; }
        }

        public void AddVisit(string notificationKey)
        {
            lock (_notifications)
            {
                if (_notifications.ContainsKey(notificationKey))
                {
                    _notifications[notificationKey].Visit();
                }
            }
        }

        public bool HasNotification()
        {
            bool ok = false;
            lock (_notifications)
            {
                if (_notificationOn)
                {
                    DateTime lastAddedTime = new DateTime(2000, 1, 1);
                    DateTime lastVisitedTime = new DateTime(2000, 1, 1);
                    foreach (Notification notification in _notifications.Values)
                    {
                        if (notification.FirstAdded > lastAddedTime)
                        {
                            lastAddedTime = notification.FirstAdded;
                        }
                        if (notification.LastVisited > lastVisitedTime)
                        {
                            lastVisitedTime = notification.LastVisited;
                        }
                    }
                    ok = (lastAddedTime > lastVisitedTime);
                }
            }
            return ok;
        }

        public bool IsExpanded
        {
            get { return _isExpanded; }
            set { _isExpanded = value; }
        }

        public int RowCount
        {
            get { return _rowCount; }
            set { _rowCount = value; }
        }

        public int ActiveColumn
        {
            get { return _activeColumn; }
            set { _activeColumn = value; }
        }

        public List<AlertLabel> LeftArrows
        {
            get { return _leftArrows; }
        }

        public List<AlertLabel> RightArrows
        {
            get { return _rightArrows; }
        }

        protected void sendProgressEvent()
        {
            if (ProgressEvent != null)
            {
                ProgressEvent(this, new EventArgs());
            }
        }

        protected void sendFlashEvent(FlashEventArgs.EventType type)
        {
            if (NotificationOn)
            {
                _newInfo = true;
                if (FlashEvent != null)
                {
                    FlashEvent(this, new FlashEventArgs(type));
                }
            }
        }

        private static AlertManager _alertManager = null;

        public static AlertManager Manager
        {
            get { if (_alertManager == null) _alertManager = new AlertManager(); return _alertManager; }
        }
    }

    public class ConditionAlert : Alert
    {
        private Scan _scan = null;
        private List<ScanSummary> _scanSummaries = new List<ScanSummary>();
        private BarCache _barCache;
        private Dictionary<string, int>[] _hashMarks = {new Dictionary<string, int>(), new Dictionary<string, int>() };
        private bool _useSocket = false;

        public override bool Running
        {
            get { return _scan != null && _scan.IsRunning; }
        }

        public override bool Editable
        {
            get { return true; }
        }

        public override bool Deleteable
        {
            get { return true; }
        }

        public override bool Scanable
        {
            get { return true; }
        }

        public override string GetLastTime()
        {
            return _scan.GetLastTime();
        }

        public override string GetNextTime()
        {
            return _scan.GetNextTime();
        }

        public ConditionAlert(string title, string portfolio, string condition, int runInterval)
        {
            _barCache = new BarCache(barChanged);
            
            setPortfolioName(portfolio);

            _scan = Scan.Manager.AddScan(title, portfolio, condition, runInterval);
            _scan.ScanStarted += new ScanStartedEventHandler(scan_Started);
            _scan.ScanProgress += new ScanProgressEventHandler(scan_Progress);
            _scan.ScanComplete += new ScanCompleteEventHandler(scan_Complete);
        }

        public string GetModelName()
        {
            var output = (MainView.SelectedMLModel!= null) ? MainView.SelectedMLModel.Name : "";

            var modelNames = MainView.getModelNames();

            string[] conditions = Condition.Split('\u0001');
            foreach (string condition in conditions)
            {
                string[] field = condition.Split('\u0002');
                string name = field[0];
                if (modelNames.Contains(name))
                {
                    output = name;
                    break;
                }
            }
            return output;
        }

        public override int GetHashMarkType(int index, string symbol)
        {
            int hashMarkType = -1;

            int type;
            if (_hashMarks[index].TryGetValue(symbol, out type))
            {
                hashMarkType = type;
            }
            return hashMarkType;
        }

        private void barChanged(object sender, BarEventArgs e)
        {
            string symbol = e.Ticker;

            if (e.Type == BarEventArgs.EventType.BarsReceived)
            {
                List<string> intervals = getTradeConditionIntervals();

                bool ok = true;
                Dictionary<string, List<DateTime>> times = new Dictionary<string, List<DateTime>>();
                Dictionary<string, Series[]> bars = new Dictionary<string, Series[]>();
                foreach (string interval in intervals)
                {
                    times[interval] = (_barCache.GetTimes(symbol, interval, 0));
                    bars[interval] = _barCache.GetSeries(symbol, interval, new string[] { "Open", "High", "Low", "Close" }, 0);
                    if (times[interval] == null || times[interval].Count == 0)
                    {
                        ok = false;
                        break;
                    }
                }

                if (ok)
                {
                    string[,] conditionList = getHashmarkConditions();

                    for (int ii = 0; ii < 2; ii++)
                    {
                        string resultInterval = "";

                        if (TradeHorizon == TradeHorizon.MidTerm)
                        {
                            resultInterval = (ii == 0) ? "Monthly" : "Weekly";
                        }
                        else
                        {
                            resultInterval = (ii == 0) ? "Quarterly" : "Monthly";
                        }

                        int barCount = times[resultInterval].Count;

                        Series[,] outputs = new Series[2, 4];
                        for (int type = 0; type < 4; type++)
                        {
                            Series signal1 = null;
                            Series signal2 = new Series(barCount, 0);
                            bool first = true;

                            string compoundCondition = conditionList[ii, type];
                            if (compoundCondition != null && compoundCondition.Length > 0)
                            {
                                string[] conditions = compoundCondition.Split('\u0001');
                                foreach (string condition in conditions)
                                {
                                    string[] field = condition.Split('\u0002');
                                    string name = field[0];
                                    string interval = (field.Length > 1) ? field[1].Replace(" Min", "") : "Daily";
                                    string ago = (field.Length > 2) ? field[2] : "10000";

                                    string[] intervalList = { interval };

                                    Series series = Conditions.Calculate(name, symbol, intervalList, barCount, times, bars, null);

                                    if (interval != resultInterval)
                                    {
                                        List<DateTime> times1 = times[interval];
                                        List<DateTime> times2 = times[resultInterval];

                                        int cnt = series.Count;

                                        int cnt1 = times1.Count;
                                        int cnt2 = times2.Count;

                                        int sidx1 = cnt1 - 1;

                                        signal1 = new Series(cnt2, 0);

                                        string i1 = interval.Substring(0, 1);
                                        string i2 = resultInterval.Substring(0, 1);
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

                                                    for (int kk = 0; kk < ago.Length; kk++)
                                                    {
                                                        if (ago[kk] == '1')
                                                        {
                                                            if (series[idx - kk] == 1)
                                                            {
                                                                signal1[idx2] = 1;
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
                                        signal1 = new Series(series.Count, 0);
                                        for (int idx = ago.Length; idx < series.Count; idx++)
                                        {
                                            for (int kk = 0; kk < ago.Length; kk++)
                                            {
                                                if (ago[kk] == '1')
                                                {
                                                    if (series[idx - kk] == 1)
                                                    {
                                                        signal1[idx] = 1;
                                                    }
                                                }
                                            }
                                        }
                                    }

                                    int c1 = signal2.Count;
                                    int c2 = signal1.Count;
                                    for (int idx = 0; idx < c2; idx++)
                                    {
                                        int index1 = c1 - 1 - idx;
                                        int index2 = c2 - 1 - idx;
                                        if (index1 >= 0 && index2 >= 0)
                                        {
                                            if (first)
                                            {
                                                signal2[index1] = signal1[index2];
                                            }
                                            else
                                            {
                                                signal2[index1] = (signal1[index2] == 1 && signal2[index1] == 1) ? 1 : 0;
                                            }
                                        }
                                    }
                                    first = false;
                                }
                            }
                            outputs[ii, type] = signal2;
                        }

                        lock (_hashMarks)
                        {
                            int direction = 0;
                            int count = outputs[ii, 0].Count;
                            int haskMarkType = -1;
                            for (int jj = 0; jj < count; jj++)
                            {
                                bool enterLong = (!double.IsNaN(outputs[ii, 0][jj]) && outputs[ii, 0][jj] != 0);
                                bool exitLong = (!double.IsNaN(outputs[ii, 1][ii]) && outputs[ii, 1][jj] != 0);
                                bool enterShort = (!double.IsNaN(outputs[ii, 2][ii]) && outputs[ii, 2][jj] != 0);
                                bool exitShort = (!double.IsNaN(outputs[ii, 3][ii]) && outputs[ii, 3][jj] != 0);

                                if (direction == 0) // neutral
                                {
                                    if (enterLong && !enterShort)
                                    {
                                        haskMarkType = 0;  // green line
                                        direction = 1;
                                    }
                                    if (enterShort && !enterLong)
                                    {
                                        haskMarkType = 2;  // red line
                                        direction = -1;
                                    }
                                }
                                else if (direction == 1)  // long
                                {
                                    if (enterShort)
                                    {
                                        haskMarkType = 2;  // red line
                                        direction = -1;
                                    }
                                    else if (exitLong)
                                    {
                                        haskMarkType = 1;  // magenta line
                                        direction = 0;
                                    }
                                }
                                else if (direction == -1)  // short
                                {
                                    if (enterLong)
                                    {
                                        haskMarkType = 0;  // green line
                                        direction = 1;
                                    }
                                    else if (exitShort)
                                    {
                                        haskMarkType = 3;  // yellow line
                                        direction = 0;
                                    }
                                }
                            }

                            _hashMarks[ii][symbol] = haskMarkType;
                        }
                        sendProgressEvent();
                    }
                }
            }
        }
        protected override void setPortfolioName(string portfolio)
        {
            if (portfolio.Contains("US 100"))
            {
                _portfolioName = "US 100";
            }
            else if (portfolio.Contains("US 100 POSITIONS"))
            {
                _portfolioName = "US 100 POSITIONS";
            }
            else if (portfolio.Contains("US 500 POSITIONS"))
            {
                _portfolioName = "US 500 POSITIONS";
            }
            else if (portfolio.Contains("GLOBAL INDICES"))
            {
                _portfolioName = "GLOBAL INDICES";
            }
            else if (portfolio.Contains("SPOT FX"))
            {
                _portfolioName = "SPOT FX";
            }
            else if (portfolio.Contains("GLOBAL 10YR"))
            {
                _portfolioName = "GLOBAL 10YR";
            }
            else if (portfolio.Contains("GLOBAL 30YR"))
            {
                _portfolioName = "GLOBAL 30YR";
            }
            else if (portfolio.Contains("GLOBAL 7YR"))
            {
                _portfolioName = "GLOBAL 7YR";
            }
            else if (portfolio.Contains("GLOBAL 5YR"))
            {
                _portfolioName = "GLOBAL 5YR";
            }
            else if (portfolio.Contains("GLOBAL 2YR"))
            {
                _portfolioName = "GLOBAL 2YR";
            }
            else if (portfolio.Contains("GLOBAL 1YR"))
            {
                _portfolioName = "GLOBAL 1YR";
            }
            else if (portfolio.Contains("COMMODITIES"))
            {
                _portfolioName = "US COMMODITIES";
            }
            else if (portfolio.Contains("CORN"))
            {
                _portfolioName = "CORN";
            }
            else if (portfolio.Contains("FIBERS"))
            {
                _portfolioName = "FIBERS";
            }
            else if (portfolio.Contains("FOODSTUFF"))
            {
                _portfolioName = "FOODSTUFF";
            }
            else if (portfolio.Contains("LIVESTOCK"))
            {
                _portfolioName = "LIVESTOCK";
            }
            else if (portfolio.Contains("OTHER GRAIN"))
            {
                _portfolioName = "OTHER GRAIN";
            }
            else if (portfolio.Contains("SOY"))
            {
                _portfolioName = "SOY";
            }
            else if (portfolio.Contains("COAL"))
            {
                _portfolioName = "COAL";
            }
            else if (portfolio.Contains("CRUDE"))
            {
                _portfolioName = "CRUDE";
            }
            else if (portfolio.Contains("ELECTRICTY"))
            {
                _portfolioName = "ELECTRICTY";
            }
            else if (portfolio.Contains("EMISSIONS"))
            {
                _portfolioName = "EMISSIONS";
            }
            else if (portfolio.Contains("NATURAL GAS"))
            {
                _portfolioName = "NATURAL GAS";
            }
            else if (portfolio.Contains("REFINED PRODUCTS"))
            {
                _portfolioName = "REFINED PRODUCTS";
            }
            else if (portfolio.Contains("SHIPPING"))
            {
                _portfolioName = "SHIPPING";
            }
            else if (portfolio.Contains("BONDS"))
            {
                _portfolioName = "BONDS";
            }
            else if (portfolio.Contains("GLOBAL INDICES POSITIONS"))
            {
                _portfolioName = "GLOBAL INDICES POSITIONS";
            }
            else if (portfolio.Contains("US STOCKS"))
            {
                _portfolioName = "US STOCKS";
            }
            else if (portfolio.Contains("FINANCIAL"))
            {
                _portfolioName = "FINANCIAL";
            }
            else if (portfolio.Contains("FIN SPREADS"))
            {
                _portfolioName = "FIN SPREADS";
            }
            else if (portfolio.Contains("N AMERICAN IND"))
            {
                _portfolioName = "N AMERICAN IND";
            }
            else if (portfolio.Contains("EUROPEAN IND"))
            {
                _portfolioName = "EUROPEAN IND";
            }
            else if (portfolio.Contains("ASIAN IND"))
            {
                _portfolioName = "ASIAN IND";
            }
            else if (portfolio.Contains("US IND"))
            {
                _portfolioName = "US IND";
            }
            else if (portfolio.Contains("CA IND"))
            {
                _portfolioName = "CA IND";
            }
            else if (portfolio.Contains("EU IND"))
            {
                _portfolioName = "EU IND";
            }
            else if (portfolio.Contains("AU IND"))
            {
                _portfolioName = "AU IND";
            }
            else if (portfolio.Contains("HK IND"))
            {
                _portfolioName = "HK IND";
            }
            else if (portfolio.Contains("JP IND"))
            {
                _portfolioName = "JP IND";
            }
            else if (portfolio.Contains("KR IND"))
            {
                _portfolioName = "KR IND";
            }
            else if (portfolio.Contains("TW IND"))
            {
                _portfolioName = "TW IND";
            }
            else if (portfolio.Contains("OCEANIA IND"))
            {
                _portfolioName = "OCEANIA IND";
            }
            else if (portfolio.Length > 5 && portfolio.Substring(0, 5) == "PRTU.")
            {
                _portfolioName = portfolio.Substring(5);
            }
        }
        public override void Close()
        {
            _scan.ScanStarted -= new ScanStartedEventHandler(scan_Started);
            _scan.ScanProgress -= new ScanProgressEventHandler(scan_Progress);
            _scan.ScanComplete -= new ScanCompleteEventHandler(scan_Complete);
            Scan.Manager.RemoveScan(_scan);
        }

        public override string Title
        {
            get { return _scan.Title; }
        }

        public override int GetDateTimeIndex(DateTime dateTime)
        {
            int index = 0;
            lock (_scanSummaries)
            {
                foreach (ScanSummary summary in _scanSummaries)
                {
                    if (summary.Date == dateTime)
                    {
                        break;
                    }
                    index++;
                }
            }
            return index;
        }

        public override bool IsBusy()
        {
            return _scan.IsRunning && !_scan.Paused;
        }

        public override int GetColumnCount()
        {
            int count = 0;
            lock (_scanSummaries)
            {
                count = _scanSummaries.Count;
            }
            return count;
        }

        public override string GetTimeText(DateTime dateTime)
        {
            string text = "";
            lock (_scanSummaries)
            {
                foreach (ScanSummary summary in _scanSummaries)
                {
                    if (dateTime == summary.Date)
                    {
                        text = summary.GetTimeText();
                        break;
                    }
                }
            }
            return text;
        }

        public override List<DateTime> GetDateTimes()
        {
            List<DateTime> dateTimes = new List<DateTime>();
            lock (_scanSummaries)
            {
                foreach (ScanSummary summary in _scanSummaries)
                {
                    dateTimes.Add(summary.Date);
                }
            }
            return dateTimes;
        }

        public override List<AlertItem> GetItems(DateTime dateTime)
        {
            List<AlertItem> items = new List<AlertItem>();
            lock (_scanSummaries)
            {
                foreach (ScanSummary summary in _scanSummaries)
                {
                    if (dateTime == summary.Date)
                    {
                        List<ScanResult> results = summary.GetResults();
                        foreach (ScanResult result in results)
                        {
                            string description = _scan.GetDescription(result.Symbol);
                            items.Add(new AlertItem(result.Symbol, result.Side, result.Interval, description, result.Enable));
                        }
                        break;
                    }
                }
            }
            return items;
        }

        protected override void pauseOrResume()
        {
            _scan.Paused = _paused;

            if (!_paused)
            {
                _scan.Run();
            }
         }

        public override bool HasTimeChanged(int dateCount)
        {
            return _scan.HasTimeChanged(dateCount);
        }

        private string[,] getHashmarkConditions()
        {
            string[,] output = new string[2,4];
            string[] hm1 = MainView.GetHashmarkConditions(0);
            string[] hm2 = MainView.GetHashmarkConditions(1);
            for (int ii = 0; ii < 4; ii++)
            {
                output[0, ii] = hm1[ii];
                output[1, ii] = hm2[ii];
            }
            return output;
        }

        private List<string> getTradeConditionIntervals()
        {
            List<string> intervals = new List<string>();
 
           if (TradeHorizon == TradeHorizon.MidTerm)
            {
                intervals.Add("Weekly");
                intervals.Add("Monthly");
            }
            else
            {
                intervals.Add("Monthly");
                intervals.Add("Quarterly");
            }

            string[,] conditions = getHashmarkConditions();
            for (int ii = 0; ii < 2; ii++)
            {
                for (int type = 0; type < 4; type++)
                {
                    if (conditions[ii, type] != null)
                    {
                        List<string> conditionIntervals = Conditions.GetIntervals(conditions[ii, type]);
                        foreach (string interval in conditionIntervals)
                        {
                            if (!intervals.Contains(interval))
                            {
                                intervals.Add(interval);
                            }
                        }
                    }
                }
            }
            return intervals;
        }

        private void requestBars(string symbol)
        {
            List<string> intervals = getTradeConditionIntervals();
            foreach (string interval in intervals)
            {
                _barCache.RequestBars(symbol, interval, true);
            }
        }

        private void scan_Started(object sender, EventArgs e)
        {
        }

        private void scan_Progress(object sender, EventArgs e)
        {
            lock (_scanSummaries)
            {
                _scanSummaries = _scan.GetScanSummary();

                if (_scanSummaries.Count > 0 && _scanSummaries[0].CurrentDateTime)
                {
                    ScanSummary summary = _scanSummaries[0];
                    List<ScanResult> results = summary.GetResults();

                    foreach (ScanResult result in results)
                    {
                        string notificationKey = result.Symbol;
                        lock (_notifications)
                        {
                            if (!_notifications.ContainsKey(notificationKey))
                            {
                                _notifications[notificationKey] = new Notification(summary.Date);
                                requestBars(result.Symbol);
                            }
                        }
                    }
                }
            }

            sendProgressEvent();
        }

        private void scan_Complete(object sender, EventArgs e)
        {        
            lock (_scanSummaries)
            {
                _scanSummaries = _scan.GetScanSummary();
            }
 
            sendFlashEvent(FlashEventArgs.EventType.NewCondition);
        }

        public override string Portfolio
        {
            get { return _scan.PortfolioName; }
        }

        public string Condition
        {
            get { return _scan.Condition; }
        }

        public Scan Scan
        {
            get { return _scan; }
            set
            {
                if (_scan != null)
                {
                    _scan.ScanStarted -= new ScanStartedEventHandler(scan_Started);
                    _scan.ScanProgress -= new ScanProgressEventHandler(scan_Progress);
                    _scan.ScanComplete -= new ScanCompleteEventHandler(scan_Complete);
                }

                lock (_notifications)
                {
                    _notifications.Clear();
                }

                _scan = value;

                if (_scan != null)
                {
                    _scan.ScanStarted += new ScanStartedEventHandler(scan_Started);
                    _scan.ScanProgress += new ScanProgressEventHandler(scan_Progress);
                    _scan.ScanComplete += new ScanCompleteEventHandler(scan_Complete);
                }
            }
        }

        public void Run()
        {
            {
                _scan.Run();
            }
        }

        public void Stop()
        {
            if (_scan.IsRunning)
            {
                _scan.Stop();
            }
        }
    }

    public class CommentAlert : Alert
    {
        private string _header;

        public override string Title
        {
            get { return _header; }
        }

        public override int GetDateTimeIndex(DateTime date)
        {
            List<DateTime> dateTimes = getDateTimes();
            int index = 0;
            foreach (DateTime dateTime in dateTimes)
            {
                if (dateTime == date)
                {
                    break;
                }
                index++;
            }
            return index;
        }

        public override bool IsBusy()
        {
            return false;
        }

        public override int GetColumnCount()
        {
            List<DateTime> dateTimes = getDateTimes();
            int count = dateTimes.Count;
            return count;
        }

        public override string GetTimeText(DateTime dateTime)
        {
            string format = "MMM d";
            string timeText = dateTime.ToString(format);
            return timeText;
        }

        public override List<DateTime> GetDateTimes()
        {
            return getDateTimes();
        }

        private List<DateTime> getDateTimes()
        {
            List<DateTime> dateTimes = new List<DateTime>();

            dateTimes.Sort((x, y) => y.CompareTo(x));
            return dateTimes;
        }

        public override List<AlertItem> GetItems(DateTime dateTime)
        {
            List<AlertItem> items = new List<AlertItem>();
            return items;
        } 
    }

    public class PositionAlert : Alert
    {
        private string _title;
        List<Symbol> _symbols;
        List<Trade> _positions;
        bool _isCustom = false;

        public PositionAlert(string title, string portfolioName, bool isExpanded, bool isCustom)
        {
            _title = title;
            _portfolioName = portfolioName;

            _isExpanded = isExpanded;
            _isCustom = isCustom;

            Portfolio portfolio = new Portfolio(2);
            _symbols = portfolio.GetSymbols(_portfolioName);
            portfolio.Close();

            _positions = Trade.Manager.getSymbolListPositions(TradeHorizon, _symbols);

            Trade.Manager.NewPositions += new NewPositionEventHandler(new_Positions);
            Trade.Manager.TradeEvent += new TradeEventHandler(TradeEvent);  // event
        }

        public bool IsCustom
        {
            get { return _isCustom; }
            set { _isCustom = value; }
        }

        public override void Close()
        {
            _symbols.Clear();
            Trade.Manager.NewPositions -= new NewPositionEventHandler(new_Positions);
            Trade.Manager.TradeEvent -= new TradeEventHandler(TradeEvent);  // event
        }

        void TradeEvent(object sender, TradeEventArgs e)
        {
            sendFlashEvent(FlashEventArgs.EventType.NewPosition);
        }

        private void new_Positions(object sender, EventArgs e)
        {
            bool updatePositions = true;

            DateTime minDate = DateTime.MinValue;
            List<DateTime> dateTimes = getDateTimes();
            if (dateTimes.Count > 0)
            {
                int index = Math.Min(5, dateTimes.Count - 1);
                minDate = dateTimes[index];
            }

            List<Trade> positions = Trade.Manager.GetNewPositions();
            if (positions.Count > 0)
            {
                foreach (Trade position in positions)
                {
                    string notificationKey = getSymbol(position);
                    if (notificationKey.Length > 0)
                    {
                        DateTime date = position.OpenDateTime;
                        date = new DateTime(date.Year, date.Month, date.Day);
                        if (date >= minDate)
                        {
                            lock (_notifications)
                            {
                                _notifications[notificationKey] = new Notification(date);
                            }
                            updatePositions = true;
                        }
                    }
                }
            }

            if (updatePositions)
            {
                Portfolio portfolio = new Portfolio(3);
                _symbols = portfolio.GetSymbols(_portfolioName);
                portfolio.Close();

                lock (_positions)
                {
                    _positions = Trade.Manager.getSymbolListPositions(TradeHorizon, _symbols);
                }
            }
            sendFlashEvent(FlashEventArgs.EventType.NewPosition);
        }

        private string getSymbol(Trade position)
        {
            string symbol = "";
            foreach (Symbol symbol1 in _symbols)
            {
                if (position.compareSymbol(symbol1.Ticker) == 0)
                {
                    symbol = symbol1.Ticker;
                    break;
                }
            }
            return symbol;
        }

        public override string Title
        {
            get { return _title; }
        }

        public override int GetDateTimeIndex(DateTime date)
        {
            List<DateTime> dateTimes = getDateTimes();
            int index = 0;
            foreach (DateTime dateTime in dateTimes)
            {
                if (dateTime == date)
                {
                    break;
                }
                index++;
            }
            return index;
        }

        public override bool IsBusy()
        {
            return false;
        }

        public override int GetColumnCount()
        {
            List<DateTime> dateTimes = getDateTimes();
            int count = dateTimes.Count;
            return count;
        }

        public override string GetTimeText(DateTime dateTime)
        {
            string format = "MMM d";
            string timeText = dateTime.ToString(format);
            return timeText;
        }

        public override List<DateTime> GetDateTimes()
        {
            return getDateTimes();
        }

        private List<DateTime> getDateTimes()
        {
            List<DateTime> dateTimes = new List<DateTime>();

            lock (_positions)
            {
                foreach (Trade position in _positions)
                {
                    DateTime dateTime1 = position.OpenDateTime;
                    dateTime1 = new DateTime(dateTime1.Year, dateTime1.Month, dateTime1.Day);
                    bool found = false;
                    foreach (DateTime dateTime2 in dateTimes)
                    {
                        if (dateTime1 == dateTime2)
                        {
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                    {
                        dateTimes.Add(dateTime1);
                    }
                }
            }

            dateTimes.Sort((x, y) => y.CompareTo(x));

            List<DateTime> output = new List<DateTime>();
            int count = Math.Min(6, dateTimes.Count);
            for (int ii = 0; ii < count; ii++)
            {
                output.Add(dateTimes[ii]);
            }
            return output;
        }

        public override List<AlertItem> GetItems(DateTime dateTime)
        {
            List<AlertItem> items = new List<AlertItem>();

            lock (_positions)
            {
                int count = _positions.Count;
                for (int ii = 0; ii < count; ii++)
                {
                    Trade position = _positions[ii];
                    DateTime dateTime1 = position.OpenDateTime;
                    dateTime1 = new DateTime(dateTime1.Year, dateTime1.Month, dateTime1.Day);

                    string ticker = position.Ticker;

                    if (dateTime1 == dateTime)
                    {
                        int side = (position.Direction >= 0) ? 0 : 1;

                        if (position.Direction == 0)
                        {
                            if (ii > 0)
                            {
                                side = (_positions[ii - 1].Direction <= 0) ? 0 : 1;
                            }
                            dateTime1 -= new TimeSpan(1, 0, 0, 0);
                        }

                        int size = Trade.Manager.getTradeSize(Portfolio, ticker, dateTime1.ToString("yyyy-MM-dd"));
                        int direction = (int)position.Direction;

                        string description = "";
                        if (size != 0)
                        {

                            bool exit = false;
                            if (direction > 0)
                            {
                                side = 0;
                            }
                            else if (direction < 0)
                            {
                                side = 1;
                            }
                            else
                            {
                                side = (size > 0) ? 1 : 0;
                                exit = true;
                            }

                            size = Math.Abs(size);

                            description = exit ? "CMR Exiting " : ((side == 0) ? "CMR Buying " : "CMR Selling ");
                            description += /*size.ToString() + " " + */ ticker;
                        }

                        string interval = getDataInterval("D");

                        AlertItem item = new AlertItem(ticker, side, interval, description, true);
                        items.Add(item);
                    }
                }
            }
            return items;
        }

        private string getDataInterval(string input)
        {
            string output = input;
            if (input == "D") output = "Daily";
            else if (input == "W") output = "Weekly";
            else if (input == "M") output = "Monthly";
            else if (input == "Q") output = "Quarterly";
            else if (input == "S") output = "SemiAnnually";
            else if (input == "Y") output = "Yearly";
            return output;
        }

        public override string GetInfoLine()
        {
            string info = "";

            List<Trade> allTrades = Trade.Manager.getTrades(_portfolioName);

            List<Trade> trades = new List<Trade>();

            if (allTrades.Count > 0)
            {
                foreach (Trade trade in allTrades)
                {
                    if (trade.IsOpen() || DateTime.Parse(trade.ExitDate).Year >= 2010)
                    {
                        trades.Add(trade);
                    }
                }
            }

            int count = trades.Count;
            if (count > 0)
            {
                int eWinCount = 0;
                int eLossCount = 0;
                double eWinTotal = 0;
                double eLossTotal = 0;
                int eDurationTotal = 0;

                int hWinCount = 0;
                int hLossCount = 0;
                int hDurationTotal = 0;

                for (int index = 0; index < count; index++)
                {
                    Trade trade = trades[index];
                    string sym = trade.Ticker;
                    int size = (int)trade.Direction;
                    string entryDate = trade.EntryDate;
                    double entryPrice = trade.EntryPrice;
                    string exitDate = trade.ExitDate;
                    double exitPrice = trade.IsOpen() ? getCurrentPrice(trade.Ticker) : trade.ExitPrice; // updateTradeOverviewText

                    if (!double.IsNaN(exitPrice))
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
                            }
                        }
                    }
                }

                var eTotalCount = eWinCount + eLossCount;
                var hTotalCount = hWinCount + hLossCount;

                info += (eTotalCount > 0) ? "Avg Holding Period = " + Math.Floor((100.0 * eDurationTotal) / eTotalCount) / 100 + " days    " : "";
                info += (eTotalCount > 0) ? "Win/Loss = " + Math.Floor((10000.0 * eWinCount) / eTotalCount) / 100 + " %    " : "";
                info += (eWinCount > 0) ? "Avg Winner = " + Math.Floor((100.0 * eWinTotal) / eWinCount) / 100 + " %    " : "";
                info += (eLossCount > 0) ? "Avg Loser = " + Math.Floor((100.0 * eLossTotal) / eLossCount) / 100 + " %    " : "";
            }

            return info;
        }

        private double getCurrentPrice(string symbol)
        {
            double price = Trade.Manager.GetLastPrice(symbol);
            return price;
        }

        private int getDayCount(string entryDate, string exitDate)
        {
            int dayCount = 0;

            int year1 = int.Parse(entryDate.Substring(0, 4));
            int month1 = int.Parse(entryDate.Substring(5, 2));
            int day1 = int.Parse(entryDate.Substring(8, 2));
            DateTime date1 = new DateTime(year1, month1, day1);

            DateTime date2 = DateTime.Now;
            if (exitDate != "")
            {
                int year2 = int.Parse(exitDate.Substring(0, 4));
                int month2 = int.Parse(exitDate.Substring(5, 2));
                int day2 = int.Parse(exitDate.Substring(8, 2));
                date2 = new DateTime(year2, month2, day2);
            }

            TimeSpan span = date2 - date1;
            dayCount = (int)span.TotalDays;
            return dayCount;
        }
    }

    public class AlertLabel : Label
    {
        private Alert _alert;

        public AlertLabel(Alert alert)
        {
            _alert = alert;
        }

        public Alert Alert
        {
            get { return _alert; }
        }
    }

    class AlertTextBlock : TextBlock
    {
        private Alert _alert;
        private DateTime _dateTime;
        private string _symbol;
        private string _interval;
        private bool _alerted;
        private List<TextBlock> _alertIndicators = new List<TextBlock>();

        public AlertTextBlock(Alert alert)
        {
            _alert = alert;
        }

        public Alert Alert
        {
            get { return _alert; }
        }

        public DateTime DateTime
        {
            get { return _dateTime; }
            set { _dateTime = value; }
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

        public bool Alerted
        {
            get { return _alerted; }
            set { _alerted = value; }
        }

        public List<TextBlock> AlertIndicators
        {
            get { return _alertIndicators; }
        }
    }

    class Flash
    {
        FrameworkElement _element;
        Brush _brush1;
        Brush _brush2;
        bool _foreground = false;

        int _count = 0;
        bool _useBrush1 = false;

        public Flash(FrameworkElement element, Brush brush1, Brush brush2, int count, bool foreground)
        {
            _element = element;
            _brush1 = brush1;
            _brush2 = brush2;
            _count = 2 * count + 1;
            _foreground = foreground;
        }

        private static FlashManager _flashManager = null;

        public static FlashManager Manager
        {
            get { if (_flashManager == null) _flashManager = new FlashManager(); return _flashManager; }
        }

        public UIElement Element
        {
            get { return _element; }
        }

        public void Stop()
        {
            if (_useBrush1)
            {
                Toggle();
            }
        }

        public bool Toggle() // returns true if done flashing
        {
            Panel panel = _element as Panel;
            Label label = _element as Label;
            if (panel != null)
            {
                panel.Background = _useBrush1 ? _brush1 : _brush2;
            }
            else if (label != null)
            {
                label.Foreground = _useBrush1 ? _brush1 : _brush2;
            }

            _useBrush1 = !_useBrush1;

            _count--;
            return (_count <= 0);
        }
    }

    class FlashManager
    {
        DispatcherTimer _timer;
        List<Flash> _flash = new List<Flash>();

        public FlashManager()
        {
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(1000);
            _timer.Tick += new System.EventHandler(timer_Tick);
            _timer.Start();
        }

        ~FlashManager()
        {
            _timer.Tick -= new System.EventHandler(timer_Tick);
        }

        private void timer_Tick(object sender, EventArgs e)
        {
            lock (_flash)
            {
                bool oldFlashing = (_flash.Count > 0);

                List<Flash> removeFlash = new List<Flash>();
                foreach (Flash flash in _flash)
                {
                    bool done = flash.Toggle();
                    if (done)
                    {
                        removeFlash.Add(flash);
                    }
                }

                foreach (Flash flash in removeFlash)
                {
                    _flash.Remove(flash);
                }

                bool newFlashing = (_flash.Count > 0);

                if (oldFlashing && !newFlashing)
                {
                }
            }
        }

        public void AddFlash(Flash flash)
        {
            lock (_flash)
            {
                bool oldFlashing = (_flash.Count > 0);

                bool addFlash = true;
                foreach (Flash flash2 in _flash)
                {
                    if (flash2.Element == flash.Element)
                    {
                        addFlash = false;
                        break;
                    }
                }

                if (addFlash)
                {
                    _flash.Add(flash);

                    if (!oldFlashing)
                    {
                    }
                }
            }
        }

        public void RemoveFlash(FrameworkElement element)
        {
            lock (_flash)
            {
                foreach (Flash flash in _flash)
                {
                    if (flash.Element == element)
                    {
                        _flash.Remove(flash);
                        flash.Stop();
                        
                        break;
                    }
                }
            }
        }
    }

    class AlertMenuItem : MenuItem
    {
        private Alert _alert;
        private DateTime _dateTime;

        public AlertMenuItem(Alert alert, DateTime dateTime)
        {
            _alert = alert;
            _dateTime = dateTime;
        }

        public Alert Alert
        {
            get { return _alert; }
        }

        public DateTime DateTime
        {
            get { return _dateTime; }
        }
    }

    public class AlertManager
    {
        private List<Alert> _alerts = new List<Alert>();

        public AlertManager()
        {
            loadConditionAlerts();

            loadTradeAlerts(false);
            loadTradeAlerts(true);
        }

        public List<Alert> Alerts
        {
            get { return _alerts; }
        }

        public void AddAlert(Alert alert)
        {
            _alerts.Add(alert);
            saveAlerts();
        }

        public void RemoveAlert(Alert alert)
        {
            alert.Close();
            _alerts.Remove(alert);
            saveAlerts();
        }

        public void Close()
        {
            saveAlerts();
        }

        string exampleTitle = "EXAMPLE  OEX   Daily FT Turns Up ";

        public void AddExampleAlerts()
        {
            addAlert(exampleTitle, "OEX", "FT Turns Up\u0002Daily\u000210000", 5);
        }

        public bool IsExampleAlert(Alert alert)
        {
            return (alert.Title == exampleTitle);
        }

        public bool HasConditionAlerts()
        {
            bool ok = false;
            foreach (Alert alert in _alerts)
            {
                if (alert is ConditionAlert)
                {
                    ok = true;
                    break;
                }
            }
            return ok;
        }

        private void addAlert(string title, string portfolio, string condition, int runInterval)
        {
            bool found = false;
            foreach (Alert alert in _alerts)
            {
                ConditionAlert conditionAlert = alert as ConditionAlert;
                if (conditionAlert != null)
                {
                    if (title == conditionAlert.Title && portfolio == conditionAlert.Portfolio && condition == conditionAlert.Condition)
                    {
                        found = true;
                        break;
                    }
                }
            }

            if (!found)
            {
                ConditionAlert alert1 = new ConditionAlert(title, portfolio, condition, runInterval);
                _alerts.Add(alert1);
                alert1.Run();
            }
        }

        private void saveAlerts()
        {
            lock (_alerts)
            {
                try
                {
                    int number = 1;
                    for (int ii = 0; ii < _maxScanCount; ii++)
                    {
                        string text = "";
                        if (ii < _alerts.Count)
                        {
                            var alert = _alerts[ii];
                            ConditionAlert scanAlert = alert as ConditionAlert;
                            if (scanAlert != null)
                            {
                                text =
                                    scanAlert.Scan.PortfolioName + "\r" +
                                    scanAlert.Scan.Condition + "\r" +
                                    scanAlert.Scan.Title + "\r" +
                                    scanAlert.RowCount.ToString() + "\r" +
                                    (scanAlert.IsExpanded ? "1" : "0") + "\r" +
                                    (scanAlert.NotificationOn ? "1" : "0") + "\r" +
                                    scanAlert.TextColor1.ToString() + "\r" +
                                    scanAlert.TextColor2.ToString() + "\r" +
                                    scanAlert.TextColor3.ToString() + "\r" +
                                    scanAlert.ViewType.ToString() + "\r" +
                                    (scanAlert.Paused ? "1" : "0") + "\r" +
                                    (scanAlert.UseOnTop ? "1" : "0") + "\r" +
                                    scanAlert.RunInterval.ToString();    
                            }
                        }
                        MainView.SaveSetting("Scan" + number, text);
                        number++;
                    }
                }
                catch (Exception)
                {
                }
            }
        }

        bool positionAlertExists(string portfolioName)
        {
            bool found = false;
            foreach (Alert alert in _alerts)
            {
                PositionAlert positionAlert = alert as PositionAlert;
                if (positionAlert != null)
                {
                    if (portfolioName.CompareTo(positionAlert.Portfolio) == 0)
                    {
                        found = true;
                        break;
                    }
                }
            }
            return found;
        }

        private void loadTradeAlerts(bool custom)
        {
            List<string> portfolios = custom ? MainView.GetCustomPortfolios() : MainView.GetPortfolios();

            List<Alert> removeAlerts = new List<Alert>();
            foreach (Alert alert in _alerts)
            {
                PositionAlert positionAlert = alert as PositionAlert;
                if (positionAlert != null && positionAlert.IsCustom == custom)
                {
                    string portfolioName = positionAlert.Portfolio;
                    bool found = false;
                    foreach (string portfolio in portfolios)
                    {
                        if (portfolio.CompareTo(portfolioName) == 0)
                        {
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                    {
                        removeAlerts.Add(alert);
                    }
                }
            }

            foreach (Alert alert in removeAlerts)
            {
                _alerts.Remove(alert);
            }

            bool expand = !custom;
            foreach (string portfolio in portfolios)
            {
                if (!positionAlertExists(portfolio))
                {
                    _alerts.Add(new PositionAlert(portfolio, portfolio, expand, custom));
                    expand = false;
                }
            }
        }

        public void UpdateTradeAlerts()
        {
            loadTradeAlerts(false);
        }

        private const int _maxScanCount = 100;

        private bool loadConditionAlerts()
        {
            bool ok = false;
            lock (_alerts)
            {
                _alerts.Clear();
                for (int ii = 1; ii < _maxScanCount; ii++)
                {
                    ok |= getScan("Scan" + ii);
                }
            }
            return ok;
        }

        private bool getScan(string input)
        {
            bool ok = false;
            try
            {
                string text = MainView.GetSetting(input);
                if (text != null)
                {
                    string[] alerts = text.Split('\n');
                    foreach (string alertText in alerts)
                    {
                        if (alertText.Length > 0)
                        {
                            string[] field = alertText.Split('\r');
                            string portfolio = (field.Length > 0) ? field[0] : "";
                            string condition = (field.Length > 1) ? field[1] : "";
                            string title = (field.Length > 2) ? field[2] : "";
                            int rowCount = (field.Length > 3) ? int.Parse(field[3]) : 6;
                            bool isExpanded = (field.Length > 4) ? (field[4] == "1") : false;
                            bool notificationOn = (field.Length > 5) ? (field[5] == "1") : true;

                            Color textColor1 = Color.FromRgb(255, 158, 42);
                            if (field.Length > 6 && field[6].Length >= 9)
                            {
                                byte a = byte.Parse(field[6].Substring(1, 2), NumberStyles.HexNumber);
                                byte r = byte.Parse(field[6].Substring(3, 2), NumberStyles.HexNumber);
                                byte g = byte.Parse(field[6].Substring(5, 2), NumberStyles.HexNumber);
                                byte b = byte.Parse(field[6].Substring(7, 2), NumberStyles.HexNumber);
                                textColor1 = Color.FromArgb(a, r, g, b);
                            }

                            Color textColor2 = Color.FromRgb(255, 158, 42);
                            if (field.Length > 7 && field[7].Length >= 9)
                            {
                                byte a = byte.Parse(field[7].Substring(1, 2), NumberStyles.HexNumber);
                                byte r = byte.Parse(field[7].Substring(3, 2), NumberStyles.HexNumber);
                                byte g = byte.Parse(field[7].Substring(5, 2), NumberStyles.HexNumber);
                                byte b = byte.Parse(field[7].Substring(7, 2), NumberStyles.HexNumber);
                                textColor2 = Color.FromArgb(a, r, g, b);
                            }

                            Color textColor3 = Colors.Yellow;
                            if (field.Length > 8 && field[8].Length >= 9)
                            {
                                byte a = byte.Parse(field[8].Substring(1, 2), NumberStyles.HexNumber);
                                byte r = byte.Parse(field[8].Substring(3, 2), NumberStyles.HexNumber);
                                byte g = byte.Parse(field[8].Substring(5, 2), NumberStyles.HexNumber);
                                byte b = byte.Parse(field[8].Substring(7, 2), NumberStyles.HexNumber);
                                textColor3 = Color.FromArgb(a, r, g, b);
                            }

                            Alert.AlertViewType viewType = Alert.AlertViewType.Spreadsheet;
                            if (field.Length > 9)
                            {
                                string name = field[9];
                                if (name == "Spreadsheet") viewType = Alert.AlertViewType.Spreadsheet;
                                else if (name == "Tree") viewType = Alert.AlertViewType.DateTree;
                            }

                            bool paused = true; // (field.Length > 10) ? (field[10] == "1") : false;

                            bool useOnTop = (field.Length > 11) ? (field[11] == "1") : true;
                            int runInterval = (field.Length > 12) ? int.Parse(field[12]) : 5;

                            string key = title + " " + portfolio + " " + condition;

                            ConditionAlert alert = new ConditionAlert(title, portfolio, condition, runInterval);
                            alert.RowCount = rowCount;
                            alert.IsExpanded = isExpanded;
                            alert.NotificationOn = notificationOn;
                            alert.UseOnTop = useOnTop;
                            alert.TextColor1 = textColor1;
                            alert.TextColor2 = textColor2;
                            alert.TextColor3 = textColor3;
                            alert.ViewType = viewType;
                            alert.Paused = paused;
                            _alerts.Add(alert);
                            alert.Run();

                            ok = true;
                        }
                    }
                }
            }
            catch (Exception x)
            {
            }
            return ok;
        }
    }

    public class Notification
    {
        private DateTime firstAdded;
        private DateTime lastVisited;

        private DateTime _date;

        public Notification(DateTime date)
        {
            _date = date;

            firstAdded = DateTime.Now;
            lastVisited = new DateTime(2000, 1, 1);
        }

        public void Visit()
        {
            lastVisited = DateTime.Now;
        }

        public DateTime FirstAdded
        {
            get { return firstAdded; }
        }

        public DateTime LastVisited
        {
            get { return lastVisited; }
        }

        public DateTime Date
        {
            get { return _date; }
        }
    }

    public class MouseEventInfo
    {
        public int level;
        public UIElement element;
        public MouseButtonEventHandler eventHandler;

        public MouseEventInfo(int i_level, UIElement i_element, MouseButtonEventHandler i_eventHandler)
        {
            level = i_level;
            element = i_element;
            eventHandler = i_eventHandler;
        }
    }

    public class AlertItem
    {
        private string _symbol;
        private int _side;
        private string _interval;
        private string _description;
        private bool _enable;

        public AlertItem(string symbol, int side, string interval, string description, bool enable)
        {
            _symbol = symbol;
            _side = side;
            _interval = interval;
            _description = description;
            _enable = enable;
        }

        public string Symbol
        {
            get { return _symbol; }
        }

        public int Side
        {
            get { return _side; }
        }

        public string Interval
        {
            get { return _interval; }
        }

        public string Description
        {
            get { return _description; }
        }

        public bool Enable
        {
            get { return _enable; }
        }

        public override string ToString()
        {
            return Symbol + "," + Side + "," + Interval + "," + Description + "," + Enable;
        }

        public static AlertItem FromString(string input)
        {
            var fields = input.Split(',');
            var symbol = (fields.Length > 0) ? fields[0] : "";
            var side = (fields.Length > 1) ? int.Parse(fields[1]) : 0;
            var interval = (fields.Length > 2) ? fields[2] : "";
            var description = (fields.Length > 3) ? fields[3] : "";
            var enable = (fields.Length > 4) ? bool.Parse(fields[4]) : false;
            return new AlertItem(symbol, side, interval, description, enable); 
        }
    }
}
