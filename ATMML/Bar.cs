using Bloomberglp.Blpapi;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
//using CQG;
using BDateTime = Bloomberglp.Blpapi.Datetime;

namespace ATMML
{
	public enum PriceType
	{
		Open,
		High,
		Low,
		Close
	}

	public delegate void BarEventHandler(object sender, BarEventArgs e);

	public class BarEventArgs : EventArgs
	{
		public enum EventType
		{
			BarsLoaded,
			BarsReceived,
			BarsUpdated
		}

		EventType _type;
		string _ticker;
		string _interval;
		int _barCount;

		public BarEventArgs(EventType type, string ticker, string interval, int barCount)
		{
			_type = type;
			_ticker = ticker;
			_interval = convertOutputInterval(interval);
			_barCount = barCount;
		}

		private string convertOutputInterval(string input)
		{
			string output = input;
			if (output == "Y")
			{
				output = "Yearly";
			}
			else if (output == "S")
			{
				output = "SemiAnnually";
			}
			else if (output == "Q")
			{
				output = "Quarterly";
			}
			else if (output == "M")
			{
				output = "Monthly";
			}
			else if (output == "W")
			{
				output = "Weekly";
			}
			else if (output == "D")
			{
				output = "Daily";
			}
			return output;
		}

		public EventType Type
		{
			get { return _type; }
		}

		public string Ticker
		{
			get { return _ticker; }
		}

		public string Interval
		{
			get { return _interval; }
		}

		public int BarCount
		{
			get { return _barCount; }
		}
	}

	public class Bar : IEquatable<Bar>, IComparable<Bar>
	{
		private DateTime _time;
		private double _open = double.NaN;
		private double _high = double.NaN;
		private double _low = double.NaN;
		private double _close = double.NaN;
		private double _volume = double.NaN;

		public Bar()
		{
		}

		public Bar(DateTime time)
		{
			_time = time;
		}

		public Bar(DateTime time, double open, double high, double low, double close, double volume)
		{
			_time = time;
			_open = (open == -2147483648) ? double.NaN : open;
			_high = (high == -2147483648) ? double.NaN : high;
			_low = (low == -2147483648) ? double.NaN : low;
			_close = (close == -2147483648) ? double.NaN : close;
			_volume = (volume == -2147483648) ? double.NaN : volume;
		}

		public bool IsOHLCDifferent(Bar other)
		{
			if (double.IsNaN(Open) != double.IsNaN(other.Open)) return true;
			if (double.IsNaN(High) != double.IsNaN(other.High)) return true;
			if (double.IsNaN(Low) != double.IsNaN(other.Low)) return true;
			if (double.IsNaN(Close) != double.IsNaN(other.Close)) return true;

			if (!double.IsNaN(Open) && Math.Abs(Open - other.Open) > 10e-9) return true;
			if (!double.IsNaN(High) && Math.Abs(High - other.High) > 10e-9) return true;
			if (!double.IsNaN(Low) && Math.Abs(Low - other.Low) > 10e-9) return true;
			if (!double.IsNaN(Close) && Math.Abs(Close - other.Close) > 10e-9) return true;

			return false;
		}

		public override string ToString()
		{
			return
				_time.ToString("yyyyMMddHHmm") + ":" +
				_open.ToString("R") + ":" +
				_high.ToString("R") + ":" +
				_low.ToString("R") + ":" +
				_close.ToString("R") + ":" +
				_volume.ToString("R") + ";";
		}

		public void Parse(string text)
		{
			string[] fields = text.Split(':');
			if (fields.Length > 0 && fields[0].Length > 0)
			{
				_time = DateTime.ParseExact(fields[0], "yyyyMMddHHmm", CultureInfo.InvariantCulture);
				_open = double.Parse(fields[1]);
				_high = double.Parse(fields[2]);
				_low = double.Parse(fields[3]);
				_close = double.Parse(fields[4]);
				_volume = double.Parse(fields[5]);
			}
		}

		public double Open
		{
			get { return _open; }
			set { _open = value; }
		}

		public double High
		{
			get { return _high; }
			set { _high = value; }
		}

		public double Low
		{
			get { return _low; }
			set { _low = value; }
		}

		public double Close
		{
			get { return _close; }
			set { _close = value; }
		}

		public double Volume
		{
			get { return _volume; }
			set { _volume = value; }
		}

		public DateTime Time
		{
			get { return _time; }
			set { _time = value; }
		}

		public DateTime ParseTime(string time)
		{
			int length = time.Length;
			int year = (length >= 4) ? int.Parse(time.Substring(0, 4)) : 0;
			int month = (length >= 6) ? int.Parse(time.Substring(4, 2)) : 0;
			int day = (length >= 8) ? int.Parse(time.Substring(6, 2)) : 0;
			int hour = (length >= 10) ? int.Parse(time.Substring(8, 2)) : 0;
			int minute = (length >= 12) ? int.Parse(time.Substring(10, 2)) : 0;
			return new DateTime(year, month, day, hour, minute, 0);
		}

		public string GetTimeLabel(bool forIntraday)
		{
			string text;
			if (forIntraday)
			{
				DateTime time1 = DateTime.UtcNow;
				DateTime time2 = DateTime.Now;
				TimeSpan timeSpan = time2 - time1;
				DateTime time = _time + timeSpan;
				text = "\u00a0" + time.Month.ToString("00") + "/" + time.Day.ToString("00") + /*"/" + (time.Year % 100).ToString("00") +*/ " " + time.Hour.ToString("00") + ":" + _time.Minute.ToString("00");
			}
			else
			{
				DateTime time = _time;
				text = "\u00a0" + time.Month.ToString("00") + "/" + time.Day.ToString("00") + "/" + time.Year.ToString("0000");
			}
			return text;
		}

		public bool Equals(Bar other)
		{
			if (this._time == other._time)
				return true;
			else
				return false;
		}

		public override bool Equals(Object obj)
		{
			if (obj == null) return base.Equals(obj);

			if (!(obj is Bar))
				throw new InvalidCastException("The 'obj' argument is not a Bar object.");
			else
				return Equals(obj as Bar);
		}

		public override int GetHashCode()
		{
			return base.GetHashCode();
		}

		public int CompareTo(Bar other)
		{
			return _time.CompareTo(other._time);
		}
	}

	//public class CQGId
	//{
	//    public CQGTimedBars TimedBarRequest;
	//    //public CQGRangeBars RangeBarRequest;
	//}

	public class BarData
	{
		public event BarEventHandler BarChanged;

		private DateTime _dateTime = DateTime.MinValue;
		private SortedList<string, Bar> _bars;
		//public CQGId cqgId = null;

		public BarData(string symbol, string interval, int capacity)
		{
			Symbol = symbol;
			Interval = interval;
			_bars = new SortedList<string, Bar>();
			_bars.Capacity = capacity;
		}

		public DateTime DateTime
		{
			get { return _dateTime; }
			set { _dateTime = value; }
		}

		public string Symbol { get; set; }
		public string Interval { get; set; }

		public int Count
		{
			get
			{
				lock (_bars)
				{
					return _bars.Count;
				}
			}
		}

		public void Subscribe(BarEventHandler eventHandler)
		{
			BarChanged -= eventHandler;
			BarChanged += eventHandler;
		}

		public void Release(BarEventHandler eventHandler)
		{
			BarChanged -= eventHandler;
		}

		public List<Bar> GetBars()
		{
			lock (_bars)
			{
				return new List<Bar>(_bars.Values);
			}
		}

		public void SetBars(string interval, List<Bar> bars)
		{
			int keySize = (interval == "Y") ? 4 : ((interval == "S" || interval == "Q" || interval == "M") ? 6 : ((interval == "W" || interval == "D") ? 8 : 12));

			lock (_bars)
			{

				_bars.Clear();
				foreach (var bar in bars)
				{
					string key = bar.Time.ToString("yyyyMMddHHmm");
					string barKey = key.Substring(0, keySize);
					_bars.Remove(barKey);
					_bars.Add(barKey, bar);
				}
			}
		}

		public string ToString(int barCount)
		{
			string text = "";

			lock (_bars)
			{

				int count = _bars.Count;
				int index = Math.Max(0, count - barCount);

				for (int ii = index; ii < count; ii++)
				{
					string key = _bars.Keys[ii];
					Bar bar = _bars.Values[ii];
					text += key + "," + bar.ToString();
				}
			}
			return text;
		}

		public void Parse(string interval, string text)
		{
			lock (_bars)
			{

				_bars.Clear();
				string[] fields1 = text.Split(';');
				int count = fields1.Length;
				_bars.Capacity = count + 10;
				for (int ii = 0; ii < count; ii++)
				{
					if (fields1[ii].Length > 0)
					{
						string[] fields2 = fields1[ii].Split(',');
						string key = fields2[0];
						string barData = (fields2.Length >= 2) ? fields2[1] : "";
						Bar bar = new Bar();
						bar.Parse(barData);

						AddBar(interval, key, bar);
					}
				}
			}
		}

		private bool mergeBars(Bar oldBar, Bar newBar)
		{
			bool changed = oldBar.IsOHLCDifferent(newBar);

			if (changed)
			{
				if (!double.IsNaN(newBar.Open))
				{
					oldBar.Open = newBar.Open;
				}
				if (!double.IsNaN(newBar.High))
				{
					oldBar.High = double.IsNaN(oldBar.High) ? newBar.High : Math.Max(oldBar.High, newBar.High);
				}
				if (!double.IsNaN(newBar.Low))
				{
					oldBar.Low = double.IsNaN(oldBar.Low) ? newBar.Low : Math.Min(oldBar.Low, newBar.Low);
				}
				if (!double.IsNaN(newBar.Close))
				{
					oldBar.Close = newBar.Close;
				}
			}
			if (!double.IsNaN(newBar.Volume))
			{
				oldBar.Volume = newBar.Volume;
			}
			return changed;
		}

		public Tuple<bool, bool> AddBar(string interval, string key, Bar bar, bool update = false)
		{
			bool changed = false;
			bool retry = false;

			lock (_bars)
			{

				int keySize = (interval == "Y") ? 4 : ((interval == "S" || interval == "Q" || interval == "M") ? 6 : ((interval == "W" || interval == "D") ? 8 : 12));

				string barKey = key.Substring(0, keySize);
				if (_bars.ContainsKey(barKey))
				{
					// Freeze historical bars - only update bars from the last 7 days
					var existingBar = _bars[barKey];
					var age = (DateTime.Now - existingBar.Time).TotalDays;
					if (age > 7)
					{
						// Historical bar - keep cached version, ignore Bloomberg revision
						return new Tuple<bool, bool>(false, false);
					}

					Bar oldBar = new Bar(existingBar.Time, existingBar.Open, existingBar.High, existingBar.Low, existingBar.Close, existingBar.Volume);
					changed = mergeBars(_bars[barKey], bar);
					retry = (changed && _bars.IndexOfKey(barKey) < _bars.Count - 1);
					bool bp = false;
					if (retry)
					{
						var idx10 = _bars.IndexOfKey(barKey);
						var cnt10 = _bars.Count - 1;
						bp = true;
					}
				}
				else
				{
					if (update)
					{
						foreach (KeyValuePair<string, Bar> kvp in _bars)
						{
							if (kvp.Key.CompareTo(barKey) > 0)
							{

								Bar oldBar = new Bar(_bars[kvp.Key].Time, _bars[kvp.Key].Open, _bars[kvp.Key].High, _bars[kvp.Key].Low, _bars[kvp.Key].Close, _bars[kvp.Key].Volume);
								bar.Time = _bars[kvp.Key].Time;
								changed = mergeBars(_bars[kvp.Key], bar);
								retry = (changed && _bars.IndexOfKey(kvp.Key) < _bars.Count - 1);
								bool bp = false;
								if (retry)
								{
									//Debug.WriteLine("Bar mismatch2 " + Symbol + " " + Interval + " "  + barKey + " " + kvp.Key + " old  = " + oldBar + " new = " + bar);
									bp = true;
								}
								break;
							}
						}
					}
					else
					{
						_bars[barKey] = bar;
					}
				}
			}

			return new Tuple<bool, bool>(changed, retry);
		}

		public bool UpdateBar(string interval, DateTime dateTime, string key, double close, double volume)
		{
			bool priceChanged = false;
			lock (_bars)
			{

				int keySize = (interval == "Y") ? 4 : ((interval == "S" || interval == "Q" || interval == "M") ? 6 : ((interval == "W" || interval == "D") ? 8 : 12));
				string key1 = key.Substring(0, keySize);

				Bar bar;
				bool found = _bars.TryGetValue(key1, out bar);

				if (!found)
				{
					int count = _bars.Count;
					long date1 = long.Parse(key1);
					int offset = (interval == "W") ? 5 : (interval == "Q") ? 3 : (interval == "S") ? 6 : 0;
					for (int ii = count - 1; ii >= 0; ii--)
					{
						long date2 = long.Parse(_bars.Keys[ii].Substring(0, keySize));
						if (date2 - offset < date1 && date1 <= date2)
						{
							bar = _bars.Values[ii];
							found = true;
							break;
						}
					}
				}

				if (found)
				{
					if (!double.IsNaN(close))
					{
						if (double.IsNaN(bar.Open))
						{
							bar.Open = close;
							priceChanged = true;
						}
						if (double.IsNaN(bar.High) || close > bar.High)
						{
							bar.High = close;
							priceChanged = true;
						}
						if (double.IsNaN(bar.Low) || close < bar.Low)
						{
							bar.Low = close;
							priceChanged = true;
						}
						if (bar.Close != close)
						{
							bar.Close = close;
							priceChanged = true;
						}
					}
					if (!double.IsNaN(volume))
					{
						bar.Volume = volume;
					}
				}
				else
				{
					priceChanged = true;
					DateTime time = getBarDate(dateTime, interval);
					string key2 = time.ToString("yyyyMMddHHmm");
					string key3 = key2.Substring(0, keySize);
					bar = new Bar(time, close, close, close, close, volume);
					AddBar(interval, key3, bar, false);
				}
			}
			return priceChanged;
		}

		private DateTime getBarDate(DateTime date, string interval)
		{
			DateTime output = date;

			TimeSpan oneDay = new TimeSpan(1, 0, 0, 0);
			int year = date.Year;
			int month = date.Month;
			int day = date.Day;


			if (interval == "Y")
			{
				DateTime time = new DateTime(year, 12, 31);
				while (time.DayOfWeek == DayOfWeek.Saturday || time.DayOfWeek == DayOfWeek.Sunday)
				{
					time -= oneDay;
				}
				output = time;
			}
			else if (interval == "S")
			{
				int m1 = ((month - 1) % 6 + 1) * 6;
				DateTime time = new DateTime(year, m1, getLastDayOfMonth(year, m1));
				while (time.DayOfWeek == DayOfWeek.Saturday || time.DayOfWeek == DayOfWeek.Sunday)
				{
					time -= oneDay;
				}
				output = time;
			}
			else if (interval == "Q")
			{
				int m1 = ((month - 1) % 3 + 1) * 3;
				DateTime time = new DateTime(year, m1, getLastDayOfMonth(year, m1));
				while (time.DayOfWeek == DayOfWeek.Saturday || time.DayOfWeek == DayOfWeek.Sunday)
				{
					time -= oneDay;
				}
				output = time;
			}
			else if (interval == "M")
			{
				DateTime time = new DateTime(year, month, getLastDayOfMonth(year, month));
				while (time.DayOfWeek == DayOfWeek.Saturday || time.DayOfWeek == DayOfWeek.Sunday)
				{
					time -= oneDay;
				}
				output = time;
			}
			else if (interval == "W")
			{
				DateTime time = new DateTime(year, month, day);
				var startDayOfWeek = (Symbol.Contains("Curncy") && Symbol[0] == 'X') ? DayOfWeek.Sunday : DayOfWeek.Friday;
				while (time.DayOfWeek != startDayOfWeek)
				{
					time += oneDay;
				}
				output = time;
			}
			return output;
		}

		private int getLastDayOfMonth(int year, int month)
		{
			if (month == 1 || month == 3 || month == 5 || month == 7 || month == 8 || month == 10 || month == 12) return 31;
			else if (month == 2)
			{
				if ((year % 4) == 0 && (year % 100) != 0) return 29;
				else return 28;
			}
			return 30;
		}

		public void ClearBars()
		{
			lock (_bars)
			{
				_bars.Clear();
				_dateTime = DateTime.MinValue;
			}
		}

		public DateTime GetRequestBarTime()
		{
			DateTime dateTime = DateTime.MinValue;
			int count = _bars.Count;
			if (count >= 4)
			{
				dateTime = _bars.Values[count - 4].Time;
			}
			return dateTime;
		}

		public double GetLastPrice(PriceType priceType, int offset)
		{
			double price = double.NaN;
			lock (_bars)
			{

				int count = _bars.Values.Count;
				for (int ii = count - 1; ii >= 0; ii--)
				{
					if (priceType == PriceType.Open) price = _bars.Values[ii].Open;
					else if (priceType == PriceType.High) price = _bars.Values[ii].High;
					else if (priceType == PriceType.Low) price = _bars.Values[ii].Low;
					else price = _bars.Values[ii].Close;
					if (!double.IsNaN(price))
					{
						if (offset <= 0)
						{
							break;
						}
						offset--;
					}
				}
			}
			return price;
		}

		public bool CheckBars(string ticker, string interval)
		{
			bool ok = true;

			bool minBarCountRequired = !(interval == "Y" || interval == "Q" || interval == "S" || interval == "M" || interval == "W");

			if (minBarCountRequired && _bars.Count < 750)
			{
				ok = false;
			}
			else
			{
				lock (_bars)
				{
					double previousClose = double.NaN;
					foreach (Bar bar in _bars.Values)
					{
						double open = bar.Open;
						double close = bar.Close;
						if (!double.IsNaN(previousClose) && !double.IsNaN(open))
						{
							if (100 * Math.Abs((open - previousClose) / previousClose) > 10)
							{
								ok = false;
								break;
							}
						}
						previousClose = close;
					}
				}
			}
			return ok;
		}

		public void FireEvent(BarEventArgs e)
		{
			if (e.Type == BarEventArgs.EventType.BarsReceived)
			{
				_dateTime = DateTime.Now;
			}

			if (BarChanged != null)
			{
				BarChanged(this, e);
			}
		}
	}

	public class BarRequest
	{
		private int _retryCount;
		private int _id;
		private string _symbol;
		private string _interval;
		private int _barCount;
		private bool _update;
		private bool _log;

		public BarRequest(int retryCount, int id, string symbol, string interval, int barCount, bool update = true, bool log = false)
		{
			_retryCount = retryCount;
			_id = id;
			_symbol = symbol;
			_interval = interval;
			_barCount = barCount;
			_update = update;
			_log = log;
		}

		public string Ticker
		{
			get { return _symbol; }
		}

		public string Interval
		{
			get { return _interval; }
		}

		public int RetryCount
		{
			get { return _retryCount; }
		}
		public int Id
		{
			get { return _id; }
		}

		public int BarCount
		{
			get { return _barCount; }
		}
		public bool Update
		{
			get { return _update; }
		}
		public bool Log
		{
			get { return _log; }
		}
	}

	public class SubscriptionInfo
	{
		private bool _subscribe = true;
		private int _threadId = -1;
		private int _referenceCount = 1;

		public SubscriptionInfo()
		{
		}

		public bool Subscribe
		{
			get { return _subscribe; }
			set { _subscribe = value; }
		}

		public int ThreadId
		{
			get { return _threadId; }
			set { _threadId = value; }
		}

		public int ReferenceCount
		{
			get { return _referenceCount; }
			set { _referenceCount = value; }
		}
	}

	public class BarServer
	{
		public const int MaxBarCount = 1000; // was 2000

		private int _barCount = BarServer.MaxBarCount; // Bloomberg
		private int _cqgBarCount = 300; // CQG

		private PolygonData _polygonData = new PolygonData();

		private int _numberOfThreads = 4;
		private TimeSpan _updateTimeSpan = new TimeSpan(0, 0, 0);   //  orginally 0, 1, 0 intraday updates 0, 0, 0 plus change line 1230 to BarData barData = getBarData(key + interval2);
		private bool _runThread = false;
		private object _fileLock = new object();
		private Thread _bloombergConnectionThread = null; // periodically attempt to create bloomberg sessions for the bar request threads
		private List<Session> _bloombergSessions = null;
		private List<Thread> _barRequestThreads = null;
		private Dictionary<string, SubscriptionInfo> _subscriptionInfos = new Dictionary<string, SubscriptionInfo>();
		private List<BarRequest> _requestList = new List<BarRequest>();
		private Dictionary<string, BarData> _barDatas = new Dictionary<string, BarData>(); // key = symbol:interval
		private int _requestId = 0;
		private int _requestCount = 0;
		private readonly Name EXCEPTIONS = new Name("exceptions");
		private readonly Name FIELD_ID = new Name("fieldId");
		private readonly Name REASON = new Name("reason");
		private readonly Name CATEGORY = new Name("category");
		private readonly Name DESCRIPTION = new Name("description");
		private readonly Name ERROR_CODE = new Name("errorCode");
		private readonly Name SOURCE = new Name("source");
		private readonly Name SECURITY_ERROR = new Name("securityError");
		private readonly Name MESSAGE = new Name("message");
		private readonly Name RESPONSE_ERROR = new Name("responseError");
		private readonly Name SECURITY_DATA = new Name("securityData");
		private readonly Name FIELD_EXCEPTIONS = new Name("fieldExceptions");
		private readonly Name ERROR_INFO = new Name("errorInfo");
		private readonly Name MARKET_BAR_START = new Name("MarketBarStart");
		private readonly Name MARKET_BAR_END = new Name("MarketBarEnd");
		private readonly Name MARKET_BAR_UPDATE = new Name("MarketBarUpdate");
		private readonly string LAST_PRICE_TAG = "PX_LAST";

		private bool _shuttingDown = false;

		private ApiLogger _logger = new ApiLogger();

		//private CQGCEL CEL;
		// private DispatcherTimer _cqgTimer;

		public BarServer()
		{
			Logging.RegisterCallback(_logger, TraceLevel.Warning);

			if (_barRequestThreads == null)
			{
				_runThread = true;
				_barRequestThreads = new List<Thread>(_numberOfThreads);
				_bloombergSessions = new List<Session>(_numberOfThreads);

				for (int ii = 0; ii < _numberOfThreads; ii++)
				{
					Thread thread = new Thread(requestBarThread);
					_barRequestThreads.Add(thread);
					_bloombergSessions.Add(null);
					thread.Start(ii);
				}

				_bloombergConnectionThread = new Thread(bloombergConnectionThread);
				_bloombergConnectionThread.Start();
			}

			try
			{
				initializeCQG();
			}
			catch (Exception x)
			{
				Trace.WriteLine("CQG initialization failed " + x);
				//_cqgConnectionOk = false;
			}

			_polygonData.BarChanged += _polygonData_BarChanged;
		}
		public void ClearCache()
		{
			lock (_fileLock)
			{
				_barDatas.Clear();
			}
		}

		private void _polygonData_BarChanged(object sender, BarEventArgs e)
		{
			string ticker = e.Ticker;
			string interval = e.Interval;
			string barDataKey = ticker + ":" + interval;
			BarData barData = getBarData(barDataKey);
			barData.FireEvent(e);
		}

		public static bool ConnectedToCQG()
		{
			return false; // _cqgConnectionOk;
		}

		public static bool ConnectedToBloomberg()
		{
			return _bloombergConnectionOk;
		}


		private void initializeCQG()
		{
			// //Creates the CQGCEL object
			// CEL = new CQGCEL();
			// CEL.DataError += new CQG._ICQGCELEvents_DataErrorEventHandler(CEL_DataError);
			// CEL.DataConnectionStatusChanged += new CQG._ICQGCELEvents_DataConnectionStatusChangedEventHandler(CEL_DataConnectionStatusChanged);
			// CEL.TimedBarsResolved += new CQG._ICQGCELEvents_TimedBarsResolvedEventHandler(CEL_TimedBarsResolved);
			// CEL.TimedBarsAdded += new CQG._ICQGCELEvents_TimedBarsAddedEventHandler(CEL_TimedBarsAdded);
			// CEL.TimedBarsUpdated += new CQG._ICQGCELEvents_TimedBarsUpdatedEventHandler(CEL_TimedBarsUpdated);
			// CEL.TimedBarsInserted += new CQG._ICQGCELEvents_TimedBarsInsertedEventHandler(CEL_TimedBarsInserted);
			// CEL.TimedBarsRemoved += new CQG._ICQGCELEvents_TimedBarsRemovedEventHandler(CEL_TimedBarsRemoved);
			// //            CEL.RangeBarsResolved += new CQG._ICQGCELEvents_RangeBarsResolvedEventHandler(CEL_RangeBarsResolved);
			// CEL.TradableExchangesResolved += CEL_TradableExchangesResolved;
			// CEL.DataSourcesResolved += CEL_DataSourcesResolved;
			// CEL.APIConfiguration.ReadyStatusCheck = eReadyStatusCheck.rscOff;
			// CEL.APIConfiguration.CollectionsThrowException = false;
			// CEL.APIConfiguration.TimeZoneCode = eTimeZone.tzCentral;
			// CEL.APIConfiguration.TimeZoneCode = eTimeZone.tzUTC;
			// CEL.Startup();

			// _runCqgThread = true;
			// _cqgThread = new Thread(new ThreadStart(cqgRun));
			// _cqgThread.Priority = ThreadPriority.Lowest;
			// _cqgThread.Start();
			//_cqgTimer = new DispatcherTimer();
			//_cqgTimer.Interval = TimeSpan.FromMilliseconds(100);
			//_cqgTimer.Tick += new System.EventHandler(cqgTimer);
			//_cqgTimer.Start();
		}



		//private void CEL_DataSourcesResolved(CQGDataSources cqg_data_sources, CQGError cqg_error)
		//{
		//    var exchanges = new List<Tuple<string, eDataSourceStatus>>();
		//    var sources = cqg_data_sources.GetEnumerator();
		//    while (sources.MoveNext())
		//    {
		//        var source = sources.Current as CQGDataSource;
		//        exchanges.Add(new Tuple<string, eDataSourceStatus>(source.Abbreviation, source.Status));
		//        //if (source.Status == eDataSourceStatus.dssAvailable)
		//        //{
		//        //    exchanges.Add(source.Abbreviation);
		//        //}
		//    }
		//}

		//{ (CBOT, dssDelay)}
		//{ (CME, dssDelay)}
		//{ (ICEUS, dssDisabled)}
		//{ (KCBOT, dssDelay)}
		//{ (MGE, dssDelay)}
		//{ (EurexVol, dssDisabled)}
		//{ (ERISX, dssDisabled)}
		//{ (NYMEX, dssDelay)}
		//{ (Baltic, dssDisabled)}
		//{ (COMP, dssAvailable)}
		//{ (COMEX, dssDelay)}
		//{ (CBOE, dssDisabled)}
		//{ (CQBLK, dssDisabled)}
		//{ (LME, dssDisabled)}
		//{ (ICAP, dssDisabled)}
		//{ (ICECAN, dssDelay)}
		//{ (SGX, dssDelay)}
		//{ (SFE, dssDelay)}
		//{ (NASDAQI, dssDelay)}
		//{ (TSX, dssDelay)}
		//{ (SWX, dssDisabled)}
		//{ (USDA, dssAvailable)}
		//{ (CQGIR, dssDelay)}
		//{ (KRX, dssDelay)}
		//{ (BGCntrEB, dssDisabled)}
		//{ (NYSE, dssDelay)}
		//{ (BdM, dssDelay)}
		//{ (ICEEU, dssDelay)}
		//{ (EUREX, dssDelay)}
		//{ (CQG FX, dssDisabled)}
		//{ (TPIMTL, dssDisabled)}
		//{ (GOVPX, dssDisabled)}
		//{ (FTSE, dssDisabled)}
		//{ (COMEX - G, dssDelay)}
		//{ (MADSE, dssDisabled)}
		//{ (DCE, dssDisabled)}
		//{ (ChaseFX, dssDisabled)}
		//{ (MICEX E, dssDisabled)}
		//{ (GE, dssDisabled)}
		//{ (MEFF - RV, dssDisabled)}
		//{ (NFXBK, dssDisabled)}
		//{ (IDEM, dssDelay)}
		//{ (MICEX F, dssDisabled)}
		//{ (CQGFX - I, dssDisabled)}
		//{ (TSE, dssDisabled)}
		//{ (NYSM, dssDelay)}
		//{ (NASDAQ, dssDelay)}
		//{ (DIMMTRX, dssDisabled)}
		//{ (MICEX B, dssDisabled)}
		//{ (MOEXFIX, dssDisabled)}
		//{ (TulSovgn, dssDisabled)}
		//{ (TulTSYKR, dssDisabled)}
		//{ (BTCEGB, dssDisabled)}
		//{ (CBOEFE, dssDelay)}
		//{ (EBS, dssDisabled)}
		//{ (LSEIN, dssDisabled)}
		//{ (LSE, dssDisabled)}
		//{ (RTS, dssDisabled)}
		//{ (RTE, dssDisabled)}
		//{ (TFX, dssDisabled)}
		//{ (OSE, dssDisabled)}
		//{ (EuroCash, dssDisabled)}
		//{ (STOXX, dssDelay)}
		//{ (ITALEQ, dssDelay)}
		//{ (BRAZIL, dssDisabled)}
		//{ (RFMTRX, dssDisabled)}
		//{ (NYMEX - G, dssDelay)}
		//{ (Platts - G, dssDisabled)}
		//{ (Platts - M, dssDisabled)}
		//{ (TOCOM, dssDisabled)}
		//{ (BMDEX, dssDisabled)}
		//{ (HKFE, dssDisabled)}
		//{ (CFESP, dssDelay)}
		//{ (EURIBS, dssDisabled)}
		//{ (EUREID, dssDisabled)}
		//{ (ICEFIN, dssDelay)}
		//{ (EURCMD, dssDisabled)}
		//{ (FMTRX, dssDisabled)}
		//{ (PDMTRX, dssDisabled)}
		//{ (CBFTSE, dssDisabled)}
		//{ (ICPEGB, dssDisabled)}
		//{ (BTEC, dssDisabled)}
		//{ (CBOT - G, dssDelay)}
		//{ (GLOBEX, dssDelay)}
		//{ (TPISMUS, dssDisabled)}
		//{ (TPISMKR, dssDisabled)}
		//{ (DBIndex, dssDelay)}
		//{ (NASFI, dssDisabled)}
		//{ (IFBDA, dssDisabled)}
		//{ (EMiniCME, dssDelay)}
		//{ (TPISMCMD, dssDisabled)}
		//{ (GLOIR, dssDelay)}
		//{ (EMiniNYX, dssDelay)}
		//{ (ASX, dssDisabled)}
		//{ (SPIND, dssDisabled)}
		//{ (HSI, dssDelay)}
		//{ (MDI, dssDelay)}
		//{ (SSE, dssDelay)}
		//{ (TWE, dssDisabled)}
		//{ (NYI, dssDisabled)}
		//{ (IPX, dssDisabled)}
		//{ (AFM, dssDisabled)}
		//{ (ICM, dssDisabled)}
		//{ (IFX, dssDisabled)}
		//{ (DME, dssDisabled)}
		//{ (NYSEInx, dssDelay)}
		//{ (NYUM, dssDisabled)}
		//{ (ZCEI, dssDisabled)}
		//{ (CQGMT, dssDisabled)}
		//{ (LMS, dssDisabled)}
		//{ (TMM, dssDisabled)}
		//{ (RTU, dssDisabled)}
		//{ (EMiniCMX, dssDelay)}
		//{ (RIC, dssDisabled)}
		//{ (CXE, dssDisabled)}
		//{ (CBMSCI, dssDisabled)}
		//{ (METLOG, dssDisabled)}
		//{ (BTECGW, dssDisabled)}
		//{ (NZFOE, dssDelay)}
		//{ (OTDCG, dssDisabled)}
		//{ (DVEX, dssDisabled)}
		//{ (BXE, dssDisabled)}
		//{ (EEX, dssDisabled)}
		//{ (NTKNEW, dssDisabled)}
		//{ (MTLCEX, dssDisabled)}
		//{ (NYMGEX, dssDelay)}
		//{ (EDFGD, dssDisabled)}
		//{ (SGXFX, dssAvailable)}
		//{ (NYMCPT, dssDelay)}
		//{ (TPILAT, dssDisabled)}
		//{ (DCEI, dssDisabled)}
		//{ (PPX24, dssDisabled)}
		//{ (SJMX, dssDisabled)}
		//{ (NODAL, dssDisabled)}
		//{ (PLTPP1, dssDisabled)}
		//{ (TKRMM, dssDisabled)}
		//{ (NASCMD, dssDelay)}
		//{ (PLTPP2, dssDisabled)}
		//{ (PLTAG, dssDisabled)}
		//{ (PLTCC, dssDisabled)}
		//{ (PLTCJ, dssDisabled)}
		//{ (PLTCL, dssDisabled)}
		//{ (PLTCS, dssDisabled)}
		//{ (PLTCX, dssDisabled)}
		//{ (PLTEB, dssDisabled)}
		//{ (PLTJF, dssDisabled)}
		//{ (PLTLG, dssDisabled)}
		//{ (PLTPG, dssDisabled)}
		//{ (PLTPL, dssDisabled)}
		//{ (PLTPN, dssDisabled)}
		//{ (PANEX, dssDisabled)}
		//{ (PLTPS, dssDisabled)}
		//{ (PLTPZ, dssDisabled)}
		//{ (PLTRC, dssDisabled)}
		//{ (PLTRI, dssDisabled)}
		//{ (PLTRP, dssDisabled)}
		//{ (PLTRU, dssDisabled)}
		//{ (PLTRW, dssDisabled)}
		//{ (PLTUG, dssDisabled)}
		//{ (PLTUW, dssDisabled)}
		//{ (PLTUY, dssDisabled)}
		//{ (PLTUZ, dssDisabled)}
		//{ (PLTWC, dssDisabled)}
		//{ (PLTDA, dssDisabled)}
		//{ (PLTDR, dssDisabled)}
		//{ (PLTDU, dssDisabled)}
		//{ (PLTDP, dssDisabled)}
		//{ (PLTSK, dssDisabled)}
		//{ (PLTEE, dssDisabled)}
		//{ (PLTEH, dssDisabled)}
		//{ (PLTEK, dssDisabled)}
		//{ (PLTEM, dssDisabled)}
		//{ (PLTES, dssDisabled)}
		//{ (PLTET, dssDisabled)}
		//{ (PLTGF, dssDisabled)}
		//{ (PLTGN, dssDisabled)}
		//{ (PLTEG, dssDisabled)}
		//{ (PLTGD, dssDisabled)}
		//{ (PLTGM, dssDisabled)}
		//{ (PLTGS, dssDisabled)}
		//{ (PLTLF, dssDisabled)}
		//{ (PLCRY, dssDisabled)}
		//{ (ASXTrade, dssDisabled)}
		//{ (VIRTU, dssDisabled)}
		//{ (NSEFUT, dssDisabled)}
		//{ (MCX, dssDisabled)}
		//{ (MDX, dssDisabled)}
		//{ (MDXIND, dssDisabled)}
		//{ (SGXSPD, dssDisabled)}
		//{ (PLTMA, dssDisabled)}
		//{ (TRKDEX, dssDisabled)}
		//{ (GWACKT, dssDisabled)}
		//{ (NZX, dssDisabled)}
		//{ (BGCANB, dssDisabled)}
		//{ (IEOTC, dssDisabled)}
		//{ (SHFE, dssDisabled)}
		//{ (AXCFD, dssDisabled)}
		//{ (JDERIV, dssDisabled)}
		//{ (CBCCCY, dssDisabled)}
		//{ (NYCFD, dssDelay)}
		//{ (JSE, dssDisabled)}
		//{ (GLOFF, dssDisabled)}
		//{ (DJENF, dssDisabled)}
		//{ (DJEBS, dssDisabled)}
		//{ (TORVX, dssDelay)}
		//{ (NQCFD, dssDelay)}
		//{ (SGXCQ, dssDisabled)}
		//{ (TFEX, dssDisabled)}
		//{ (LSEDM, dssDisabled)}
		//{ (MDRFX, dssDisabled)}
		//{ (DGCX, dssDisabled)}
		//{ (HMS, dssDisabled)}
		//{ (EURFX, dssDisabled)}
		//{ (MDCFD, dssDisabled)}
		//{ (MRY, dssDisabled)}
		//{ (TFC365, dssDisabled)}
		//{ (LDMCG, dssDisabled)}
		//{ (IBEXI, dssDisabled)}
		//{ (ERIS, dssDelay)}
		//{ (NACFD, dssDisabled)}
		//{ (RUSSL, dssDisabled)}
		//{ (OANDA, dssDisabled)}
		//{ (RICEI, dssDisabled)}
		//{ (DJI, dssDisabled)}
		//{ (IFSNG, dssDisabled)}
		//{ (BICBT, dssDisabled)}
		//{ (BIIUS, dssDisabled)}
		//{ (FTSIT, dssDisabled)}
		//{ (BINYM, dssDisabled)}
		//{ (AQIEX, dssDisabled)}
		//{ (ASXAB, dssDisabled)}
		//{ (FEX, dssDisabled)}
		//{ (ICECS, dssDelay)}
		//{ (FENIC, dssDisabled)}
		//{ (BIICME, dssDisabled)}
		//{ (ZCE, dssDisabled)}
		//{ (CFFEX, dssDisabled)}
		//{ (SHZSE, dssDisabled)}
		//{ (SHGSE, dssDisabled)}
		//{ (LBMA, dssDisabled)}
		//{ (TAIFX, dssDisabled)}
		//{ (APEX, dssDisabled)}
		//{ (OTCMG, dssDisabled)}
		//{ (CMBIT, dssDisabled)}
		//{ (FRWAV, dssDisabled)}
		//{ (INE, dssDisabled)}
		//{ (EURRT, dssDisabled)}
		//{ (LMTST, dssDisabled)}
		//{ (PLTGI, dssDisabled)}
		//{ (JMPLQ, dssDisabled)}
		//{ (BICMX, dssDisabled)}
		//{ (JBMKT, dssDisabled)}
		//{ (NQFI, dssDisabled)}
		//{ (PLLNC, dssDisabled)}
		//{ (GAVLN, dssDisabled)}
		//{ (ICOTC, dssDisabled)}
		//{ (ECOTC, dssDisabled)}
		//{ (MFTST, dssDisabled)}
		//{ (BXCFD, dssDisabled)}
		//{ (CXCFD, dssDisabled)}
		//{ (SMXCH, dssDisabled)}
		//{ (PLTAN, dssDisabled)}
		//{ (DBSOT, dssDisabled)}
		//{ (AGCEN, dssDisabled)}
		//{ (CHFHM, dssDisabled)}
		//{ (BTNL, dssDisabled)}
		//{ (IFAD, dssDisabled)}
		//{ (DXE, dssDisabled)}
		//{ (DXCFD, dssDisabled)}
		//{ (HNEX, dssDisabled)}
		//{ (EXTST, dssDisabled)}
		//{ (SEDCX, dssDisabled)}
		//{ (NNEQ, dssDisabled)}
		//{ (ERIXF, dssDisabled)}
		//{ (NNFN, dssDisabled)}
		//{ (EMCBT, dssDelay)}
		//{ (HLFHM, dssDisabled)}
		//{ (COT, dssAvailable)}
		//{ (FAIRX, dssDisabled)}
		//{ (BTNLC, dssDisabled)}
		//{ (WASDE, dssAvailable)}
		//{ (OTCIFEC, dssDisabled)}
		//{ (MKTDEQ, dssDisabled)}
		//{ (SKYHM, dssDisabled)}
		//{ (NNIND, dssDisabled)}
		//{ (DISCOT, dssAvailable)}
		//{ (NWGHM, dssDisabled)}
		//{ (ABARES, dssAvailable)}
		//{ (ECAGRI, dssAvailable)}
		//{ (DLGHM, dssDisabled)}
		//{ (LIQEDGE, dssDisabled)}
		//{ (FCAHM, dssDisabled)}
		//{ (MNSHM, dssDisabled)}
		//{ (CGSHM, dssDisabled)}
		//{ (XSTO, dssDisabled)}
		//{ (XCSE, dssDisabled)}
		//{ (XHEL, dssDisabled)}
		//{ (XICE, dssDisabled)}
		//{ (XTAL, dssDisabled)}
		//{ (XRIS, dssDisabled)}
		//{ (XLIT, dssDisabled)}
		//{ (FNIS, dssDisabled)}
		//{ (FNDK, dssDisabled)}
		//{ (FNSE, dssDisabled)}
		//{ (FNEE, dssDisabled)}
		//{ (FNLV, dssDisabled)}
		//{ (XLITW, dssDisabled)}
		//{ (FNFI, dssDisabled)}
		//{ (FNFIW, dssDisabled)}
		//{ (FNDKW, dssDisabled)}
		//{ (SSMEW, dssDisabled)}
		//{ (PEPW, dssDisabled)}
		//{ (FNLT, dssDisabled)}
		//{ (RVCHM, dssDisabled)}
		//{ (AFCFD, dssDisabled)}
		//{ (AFFX, dssDisabled)}
		//{ (UGCHM, dssDisabled)}
		//{ (LYFHM, dssDisabled)}
		//{ (MRLHM, dssDisabled)}
		//{ (PLTHY, dssDisabled)}
		//{ (TOKHM, dssDisabled)}
		//{ (BCRHM, dssDisabled)}
		//{ (LPKGI, dssDisabled)}
		//{ (TRGHM, dssDisabled)}
		//{ (PAOHM, dssDisabled)}
		//{ (LMECOT, dssAvailable)}
		//{ (FHRHM, dssDisabled)}
		//{ (PCFHM, dssDisabled)}
		//{ (NDTS, dssDisabled)}
		//{ (NNDDVINT, dssDisabled)}
		//{ (NNDQA, dssDisabled)}
		//{ (DMGHM, dssDisabled)}
		//{ (ENDEX, dssDisabled)}
		//{ (DGSHM, dssDisabled)}
		//{ (PSHMS, dssDisabled)}
		//{ (MVCHM, dssDisabled)}
		//{ (AGLHM, dssDisabled)}
		//{ (FRCHM, dssDisabled)}
		//{ (JCMHM, dssDisabled)}
		//{ (MSFHM, dssDisabled)}
		//{ (LPBGM, dssDisabled)}
		//{ (PNFHM, dssDisabled)}
		//{ (TM2EX, dssDisabled)}
		//{ (BRRHM, dssDisabled)}
		//{ (RLDHM, dssDisabled)}
		//{ (TFX365, dssDisabled)}
		//{ (PLTCBN, dssDisabled)}
		//{ (PLTCNL, dssDisabled)}
		//{ (NWTHM, dssDisabled)}
		//{ (DUFHM, dssDisabled)}
		//{ (AQUARIUS, dssDisabled)}
		//{ (ELWHM, dssDisabled)}
		//{ (KCOTC, dssDisabled)}
		//{ (PLCTR, dssDisabled)}
		//{ (PLQDX, dssDisabled)}
		//{ (PLQMX, dssDisabled)}
		//{ (CFMHM, dssDisabled)}
		//{ (LPUPE, dssDisabled)}
		//{ (HNEXKGI, dssDisabled)}
		//{ (BFEHM, dssDisabled)}
		//{ (SGXDAIRY, dssDisabled)}
		//{ (CMEBMK, dssDisabled)}
		//{ (DDMHM, dssDisabled)}
		//{ (BIKBT, dssDisabled)}
		//{ (MAREXFX, dssDisabled)}
		//{ (RCAHM, dssDisabled)}
		//{ (B3SA, dssDisabled)}
		//{ (IFMHM, dssDisabled)}
		//{ (ABAXX, dssDisabled)}
		//{ (RDMHM, dssDisabled)}
		//{ (INDHM, dssDisabled)}
		//{ (OTCEUR, dssDisabled)}
		//{ (OTCHKF, dssDisabled)}
		//{ (OTCIFF, dssDisabled)}
		//{ (OTCSGX, dssDisabled)}
		//{ (LANHM, dssDisabled)}
		//{ (NSEIE, dssDisabled)}
		//{ (ADMHM, dssDisabled)}
		//{ (OTCMCME, dssDisabled)}
		//{ (OTCASX24, dssDisabled)}
		//{ (OTCCBTM, dssDisabled)}
		//{ (OTCSGXSP, dssDisabled)}
		//{ (PCGHM, dssDisabled)}
		//{ (SCOHM, dssDisabled)}
		//{ (GDIGEX, dssDisabled)}
		//{ (PGAHM, dssDisabled)}
		//{ (ADSHM, dssDisabled)}
		//{ (GRCHM, dssDisabled)}
		//{ (LFCHM, dssDisabled)}
		//{ (MCGHM, dssDisabled)}
		//{ (OAKHD, dssDisabled)}
		//{ (FOAHM, dssDisabled)}
		//{ (ADLHM, dssDisabled)}
		//{ (CMEEVNT, dssDisabled)}
		//{ (NYMEVNT, dssDisabled)}
		//{ (CMXEVNT, dssDisabled)}
		//{ (CBTEVNT, dssDisabled)}
		//{ (EMIEVNT, dssDisabled)}
		//{ (FTX, dssDisabled)}
		//{ (UWGHM, dssDisabled)}
		//{ (QCCHM, dssDisabled)}
		//{ (FMX, dssDisabled)}
		//{ (UNAHM, dssDisabled)}
		//{ (DFAHM, dssDisabled)}
		//{ (USEIA, dssAvailable)}
		//{ (CNFHM, dssDisabled)}
		//{ (RMSHM, dssDisabled)}

		//        private void CEL_TradableExchangesResolved(int gw_account_id, CQGExchanges cqg_exchanges, CQGError cqg_error)
		//        {

		//        }

		//        private void CEL_DataError1(object cqg_error, string error_description)
		//        {

		//        }

		//        private void CEL_CommodityInstrumentsResolved(string commodity_name, eInstrumentType instrument_types, CQGCommodityInstruments cqg_commodity_intruments)
		//        {

		//        }

		//        private void CEL_InstrumentSubscribed(string symbol_, CQGInstrument cqg_instrument)
		//        {

		//        }

		//        private void CEL_InstrumentResolved(string Symbol, CQGInstrument cqg_instrument, CQGError cqg_error)
		//        {

		//        }

		//        private void CEL_IncorrectSymbol(string symbol_)
		//        {

		//        }

		//        private static  bool _cqgConnectionOk = false;
		//        private bool _runCqgThread = false;
		//        private Thread _cqgThread = null;
		//        private Queue<BarRequest> _cqgBarRequests = new Queue<BarRequest>();
		//        private int _cqgRequestCount = 0;
		//        private DateTime _cqgRequestTime;


		private static bool _bloombergConnectionOk = false;


		//        //void cqgTimer(object sender, EventArgs e)
		//        //{
		//        //    processCqgRequest();
		//        //}

		//        private void cqgRun()
		//        {
		//            while (_runCqgThread)
		//            {
		//                processCqgRequest();
		//            }
		//        }

		//        private void processCqgRequest()
		//        {
		//            BarRequest request = null;
		//            lock (_cqgBarRequests)
		//            {
		//                if (_cqgConnectionOk && _cqgBarRequests.Count > 0)
		//                {
		//                    var ts = DateTime.Now - _cqgRequestTime;
		//                    if (ts.TotalSeconds > 5)
		//                    {
		//                        Trace.WriteLine("CQG data timeout!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
		//                        _cqgRequestCount = 0;
		//                    }

		//                    if (_cqgRequestCount == 0)
		//                    {
		//                        request = _cqgBarRequests.Dequeue();
		//                    }
		//                }
		//            }

		//            if (request != null)
		//            {
		//                string ticker = request.Ticker;
		//                string interval = request.Interval;
		//                string barDataKey = ticker + ":" + interval;
		//                BarData barData = getBarData(barDataKey);

		//                var id = requestCQGBars(ticker, interval, request.Update);

		//                lock (barData)
		//                {
		//                    barData.cqgId = id;
		//                }
		//            }
		//            Thread.Sleep(10);
		//        }

		//        private void requestCQGBars(BarRequest request)
		//        {
		//            lock (_cqgBarRequests)
		//            {
		//                _cqgBarRequests.Enqueue(request);
		//            }
		//        }

		//        private CQGId requestCQGBars(string symbol, string interval, bool update)
		//        {
		//            CQGId output = new CQGId();

		//            Trace.WriteLine("CQG request bars: " + symbol + " " + interval + " " + update);

		//            _cqgRequestTime = DateTime.Now;
		//            _cqgRequestCount++;

		////            var rangeBarRequest = (interval[0] == 'R');

		//            try
		//            {
		//                //if (rangeBarRequest)
		//                //{
		//                //    var range = int.Parse(interval.Substring(1));
		//                //    CQGRangeBarsRequest cqgRangeBarRequest = CEL.CreateRangeBarsRequest();
		//                //    cqgRangeBarRequest.Symbol = symbol;
		//                //    cqgRangeBarRequest.RangeUnit = eRangeUnit.ruTick;
		//                //    cqgRangeBarRequest.Range = range;
		//                //    cqgRangeBarRequest.UpdatesEnabled = true;
		//                //    cqgRangeBarRequest.RangeEnd = -_cqgBarCount;
		//                //    cqgRangeBarRequest.RangeStart = 0;
		//                //    cqgRangeBarRequest.Continuation = eTimeSeriesContinuationType.tsctActive;
		//                //    cqgRangeBarRequest.EqualizeCloses = true;

		//                //    Trace.WriteLine("Send CQG range bar request begin");
		//                //    output.RangeBarRequest = CEL.RequestRangeBars(cqgRangeBarRequest);
		//                //    Trace.WriteLine("Send CQG range bar request end");
		//                //}
		//                //else
		//                {
		//                    eHistoricalPeriod historicalPeriod = eHistoricalPeriod.hpUndefined;
		//                    int intradayPeriod = 1;

		//                    if (interval == "D") historicalPeriod = eHistoricalPeriod.hpDaily;
		//                    else if (interval == "W") historicalPeriod = eHistoricalPeriod.hpWeekly;
		//                    else if (interval == "M") historicalPeriod = eHistoricalPeriod.hpMonthly;
		//                    else if (interval == "Q") historicalPeriod = eHistoricalPeriod.hpQuarterly;
		//                    else if (interval == "S") historicalPeriod = eHistoricalPeriod.hpSemiannual;
		//                    else if (interval == "Y") historicalPeriod = eHistoricalPeriod.hpYearly;
		//                    else intradayPeriod = int.Parse(interval);

		//                    CQGTimedBarsRequest cqgTimedBarsRequest = CEL.CreateTimedBarsRequest();
		//                    cqgTimedBarsRequest.Symbol = symbol;
		//                    cqgTimedBarsRequest.UpdatesEnabled = update;
		//                    if (historicalPeriod != eHistoricalPeriod.hpUndefined)
		//                    {
		//                        cqgTimedBarsRequest.HistoricalPeriod = historicalPeriod;
		//                    }
		//                    else
		//                    {
		//                        cqgTimedBarsRequest.IntradayPeriod = intradayPeriod;
		//                    }
		//                    cqgTimedBarsRequest.RangeEnd = -_cqgBarCount;
		//                    cqgTimedBarsRequest.RangeStart = 0;
		//                    cqgTimedBarsRequest.SessionsFilter = 31;
		//                    cqgTimedBarsRequest.Continuation = eTimeSeriesContinuationType.tsctActive;
		//                    cqgTimedBarsRequest.EqualizeCloses = true;

		//                    Trace.WriteLine("Send CQG bar request begin");
		//                    output.TimedBarRequest = CEL.RequestTimedBars(cqgTimedBarsRequest);
		//                    Trace.WriteLine("Send CQG bar request end");
		//                }
		//            }
		//            catch (Exception)
		//            {
		//            }
		//            return output;
		//        }

		//        private void CEL_DataError(object cqg_error, string error_description)
		//        {
		//            Trace.WriteLine("CQG Data Error: " + error_description);
		//        }

		//        private void CEL_DataConnectionStatusChanged(CQG.eConnectionStatus new_status)
		//        {
		//            if (new_status != eConnectionStatus.csConnectionUp)
		//            {
		//                _runCqgThread = false;
		//                _cqgThread.Join(5000);
		//                initializeCQG();
		//            }

		//            _cqgConnectionOk = (new_status == CQG.eConnectionStatus.csConnectionUp);
		//            if (_cqgConnectionOk)
		//            {
		//                _cqgRequestTime = DateTime.Now;
		//                //CEL.RequestTradableExchanges();
		//                CEL.RequestDataSources();
		//            }
		//            Trace.WriteLine("CQG connection status = " + new_status.ToString());
		//        }

		//        private void CEL_TimedBarsResolved(CQG.CQGTimedBars cqgTimedBars, CQG.CQGError cqg_error)
		//        {

		//            string ticker = cqgTimedBars.Request.Symbol;
		//            string interval = getIntervalFromCQGTimedBarRequest(cqgTimedBars.Request);
		//            var count = cqgTimedBars.Count;
		//            Trace.WriteLine("CQG returns bars: " + ticker + " " + interval + " count = " + count);

		//            _cqgRequestCount--;

		//            if (cqg_error != null)
		//            {
		//                int code = cqg_error.Code;
		//                string description = cqg_error.Description;
		//                Trace.WriteLine("CQG Error: " + description);

		//            }
		//            loadCQGTimedBars(cqgTimedBars, 0);
		//        }

		//        //private void CEL_RangeBarsResolved(CQG.CQGRangeBars cqgRangeBars, CQG.CQGError cqg_error)
		//        //{
		//        //    string ticker = cqgRangeBars.Request.Symbol;
		//        //    string interval = getIntervalFromCQGRangeBarRequest(cqgRangeBars.Request);
		//        //    var count = cqgRangeBars.Count;
		//        //    Trace.WriteLine("CQG returns range bars: " + ticker + " " + interval + " count = " + count);

		//        //    _cqgRequestCount--;

		//        //    if (cqg_error != null)
		//        //    {
		//        //        int code = cqg_error.Code;
		//        //        string description = cqg_error.Description;
		//        //        Trace.WriteLine("CQG Error: " + description);

		//        //    }
		//        //    loadCQGRangeBars(cqgRangeBars, 0);
		//        //}

		//        private void CEL_TimedBarsAdded(CQG.CQGTimedBars cqgTimedBars)
		//        {
		//            //var count = cqgTimedBars.Count;
		//            //loadCQGBars(cqgTimedBars, count - 1);
		//        }

		//        private void CEL_TimedBarsInserted(CQG.CQGTimedBars cqgTimedBars, int index)
		//        {
		//            // loadCQGBars(cqgTimedBars, index);
		//        }

		//        private void CEL_TimedBarsRemoved(CQG.CQGTimedBars cqgTimedBars, int index)
		//        {
		//            try
		//            {
		//                string ticker = cqgTimedBars.Request.Symbol;
		//                string interval = getIntervalFromCQGTimedBarRequest(cqgTimedBars.Request);
		//                //string key = ticker + ":" + interval;
		//                //BarData barData = getBarData(key);

		//                // todo
		//                //Trace.WriteLine("CQG remove bar " + ticker + " " + interval + " " + index);


		//            }
		//            catch (Exception ex)
		//            {
		//            }
		//        }

		//        private void CEL_TimedBarsUpdated(CQG.CQGTimedBars cqgTimedBars, int index)
		//        {
		//            loadCQGTimedBars(cqgTimedBars, index);
		//        }

		//        private void loadCQGTimedBars(CQG.CQGTimedBars cqgTimedBars, int index = 0)
		//        {
		//            string ticker = cqgTimedBars.Request.Symbol;
		//            string interval = getIntervalFromCQGTimedBarRequest(cqgTimedBars.Request);
		//            string key = ticker + ":" + interval;
		//            BarData barData = getBarData(key);

		//            bool intraday = (char.IsDigit(interval[0]));

		//            bool changed = (index == 0);

		//            int count = (index == 0) ? count = cqgTimedBars.Count : index + 1;

		//            try
		//            {
		//                TimeSpan timeSpan = new TimeSpan(12, 0, 0);

		//                for (int ii = index; ii < count; ii++)
		//                {
		//                    DateTime time = cqgTimedBars[ii].Timestamp;
		//                    if (!intraday)
		//                    {
		//                        time += timeSpan;
		//                        time = new DateTime(time.Year, time.Month, time.Day);
		//                    }
		//                    Bar bar = new Bar(time, cqgTimedBars[ii].Open, cqgTimedBars[ii].High, cqgTimedBars[ii].Low, cqgTimedBars[ii].Close, cqgTimedBars[ii].ActualVolume);

		//                    string barKey = bar.Time.ToString(intraday ? "yyyyMMddHHmm" : "yyyyMMdd");
		//                    changed |= barData.AddBar(interval, barKey, bar).Item1;
		//                }

		//                //if (changed)
		//                //{
		//                //    string text = ticker + " " + interval + " " + cqgTimedBars[count - 1].Close.ToString("##.##");
		//                //    Console.WriteLine(text);
		//                //}

		//            }
		//            catch (Exception x)
		//            {
		//            }

		//            if (changed)
		//            {
		//                barData.FireEvent(new BarEventArgs((index == 0) ? BarEventArgs.EventType.BarsReceived : BarEventArgs.EventType.BarsUpdated, ticker, interval, count - index));
		//            }
		//        }

		//        //private void loadCQGRangeBars(CQG.CQGRangeBars cqgRangeBars, int index = 0)
		//        //{
		//        //    string ticker = cqgRangeBars.Request.Symbol;
		//        //    string interval = getIntervalFromCQGRangeBarRequest(cqgRangeBars.Request);
		//        //    string key = ticker + ":" + interval;
		//        //    BarData barData = getBarData(key);

		//        //    bool intraday = (char.IsDigit(interval[0]));

		//        //    bool changed = (index == 0);

		//        //    int count = (index == 0) ? count = cqgRangeBars.Count : index + 1;

		//        //    try
		//        //    {
		//        //        TimeSpan timeSpan = new TimeSpan(12, 0, 0);

		//        //        for (int ii = index; ii < count; ii++)
		//        //        {
		//        //            DateTime time = cqgRangeBars[ii].Timestamp;
		//        //            Bar bar = new Bar(time, cqgRangeBars[ii].Open, cqgRangeBars[ii].High, cqgRangeBars[ii].Low, cqgRangeBars[ii].Close, cqgRangeBars[ii].ActualVolume);

		//        //            string barKey = bar.Time.ToString("yyyyMMddHHmm");
		//        //            changed |= barData.AddBar(interval, barKey, bar);
		//        //        }

		//        //        //if (changed)
		//        //        //{
		//        //        //    string text = ticker + " " + interval + " " + cqgTimedBars[count - 1].Close.ToString("##.##");
		//        //        //    Console.WriteLine(text);
		//        //        //}

		//        //    }
		//        //    catch (Exception x)
		//        //    {
		//        //    }

		//        //    if (changed)
		//        //    {
		//        //        barData.FireEvent(new BarEventArgs((index == 0) ? BarEventArgs.EventType.BarsReceived : BarEventArgs.EventType.BarsUpdated, ticker, interval, count - index));
		//        //    }
		//        //}

		//        //private string getIntervalFromCQGRangeBarRequest(CQGRangeBarsRequest request)
		//        //{
		//        //    return "R" + request.Range.ToString();
		//        //}


		//        private string getIntervalFromCQGTimedBarRequest(CQGTimedBarsRequest request)
		//        {
		//            string interval = "";
		//            eHistoricalPeriod historicalPeriod = request.HistoricalPeriod;
		//            int intradayPeriod = request.IntradayPeriod;

		//            if (historicalPeriod == eHistoricalPeriod.hpUndefined)
		//            {
		//                interval = intradayPeriod.ToString();
		//            }
		//            else if (historicalPeriod == eHistoricalPeriod.hpDaily)
		//            {
		//                interval = "D";
		//            }
		//            else if (historicalPeriod == eHistoricalPeriod.hpWeekly)
		//            {
		//                interval = "W";
		//            }
		//            else if (historicalPeriod == eHistoricalPeriod.hpMonthly)
		//            {
		//                interval = "M";
		//            }
		//            else if (historicalPeriod == eHistoricalPeriod.hpQuarterly)
		//            {
		//                interval = "Q";
		//            }
		//            else if (historicalPeriod == eHistoricalPeriod.hpSemiannual)
		//            {
		//                interval = "S";
		//            }
		//            else if (historicalPeriod == eHistoricalPeriod.hpYearly)
		//            {
		//                interval = "Y";
		//            }

		//            return interval;
		//        }

		~BarServer()
		{
		}

		public void Shutdown()
		{
			if (!_shuttingDown)
			{
				_shuttingDown = true;
				try
				{
					_runThread = false;
					_bloombergConnectionThread.Join(1000);
					foreach (Thread thread in _barRequestThreads)
					{
						thread.Join(1000);
					}
					_barRequestThreads.Clear();
				}
				catch (Exception)
				{
				}

				//_runCqgThread = false;
				//                _cqgTimer.Stop();

				Clear();
			}
		}

		public void Clear()
		{

			lock (_requestList)
			{
				_requestList.Clear();
			}


			lock (_subscriptionInfos)
			{
				_subscriptionInfos.Clear();
			}

			lock (_barDatas)
			{
				_barDatas.Clear();
			}
		}

		public int GetRequestCount()
		{
			int requestCount = 0;
			lock (_requestList)
			{
				requestCount = _requestCount;
			}
			return requestCount;
		}


		public string GetRequestSymbol()
		{
			string symbol = "";
			lock (_requestList)
			{
				if (_requestList.Count > 0)
				{
					symbol = _requestList[0].Ticker;
				}
			}
			return symbol;
		}

		public void DeleteFile(string symbol, string interval)
		{
			try
			{
				var fileName = getFileName(symbol, interval);
				MainView.SaveUserData(fileName, "", true);
			}
			catch
			{
			}
		}

		private string getFileName(string symbol, string interval)
		{
			string output = "";

			try
			{
				symbol = symbol.Replace("/", "-").Replace("*", "#");
				output = @"Bloomberg\" + symbol + @"\" + interval;
			}
			catch (Exception)
			{
			}

			return output;
		}

		private string timeToString(DateTime time, string interval)
		{
			string format = isHistorical(interval) ? "yyyyMMdd" : "yyyyMMddHHmm";
			return time.ToString(format);
		}

		private DateTime parseTime(string time)
		{
			int length = time.Length;
			int year = (length >= 4) ? int.Parse(time.Substring(0, 4)) : 0;
			int month = (length >= 6) ? int.Parse(time.Substring(4, 2)) : 0;
			int day = (length >= 8) ? int.Parse(time.Substring(6, 2)) : 0;
			int hour = (length >= 10) ? int.Parse(time.Substring(8, 2)) : 0;
			int minute = (length >= 12) ? int.Parse(time.Substring(10, 2)) : 0;
			return new DateTime(year, month, day, hour, minute, 0);
		}

		private bool isHistorical(string interval)
		{
			string text = interval.Substring(interval.Length - 1, 1);
			bool historical = (text == "D" || text == "W" || text == "M" || text == "S" || text == "Q" || text == "Y");
			return historical;
		}

		private void saveBars(string symbol, string interval, List<Bar> bars)
		{
			string fileName = getFileName(symbol, interval);
			if (fileName.Length > 0)
			{

				lock (_fileLock)
				{
					var sb = new StringBuilder();
					foreach (Bar bar in bars)
					{
						string line = timeToString(bar.Time, interval) + ":" + bar.Open + ":" + bar.High + ":" + bar.Low + ":" + bar.Close + ":" + bar.Volume + "\n";
						sb.Append(line);
					}
					MainView.SaveUserData(fileName, sb.ToString(), true);
				}
			}
		}

		private bool isPolygon(string interval)
		{
			bool polygon = false;
			string text = interval.Substring(1, interval.Length - 1);
			int number;
			polygon = (interval[0] == 'D' || interval[0] == 'W') && int.TryParse(text, out number);
			return polygon;
		}

		private async Task<List<Bar>> loadBars(string symbol, string interval)
		{
			List<Bar> bars = new List<Bar>();
			if (isPolygon(interval))
			{
				bars = await _polygonData.GetBars(symbol, interval, new DateTime(2022, 1, 1, 0, 0, 0, DateTimeKind.Utc), DateTime.UtcNow);
			}
			else
			{

				//return bars;  gets new set of bars

				string fileName = getFileName(symbol, interval);

				if (fileName.Length > 0 && MainView.ExistsUserData(fileName, true))
				{

					lock (_fileLock)
					{
						var data1 = MainView.LoadUserData(fileName, true);
						var lines = data1.Split('\n');
						foreach (var line in lines)
						{
							string[] fields = line.Split(':');
							if (fields.Length >= 6)
							{
								string time = fields[0];
								string open = fields[1];
								string high = fields[2];
								string low = fields[3];
								string close = fields[4];
								string volume = fields[5];

								Bar bar = new Bar();
								bar.Time = parseTime(time);
								bar.Open = (open == "NaN" || open.Length == 0) ? Double.NaN : Convert.ToDouble(open);
								bar.High = (high == "NaN" || high.Length == 0) ? Double.NaN : Convert.ToDouble(high);
								bar.Low = (low == "NaN" || low.Length == 0) ? Double.NaN : Convert.ToDouble(low);
								bar.Close = (close == "NaN" || close.Length == 0) ? Double.NaN : Convert.ToDouble(close);
								bar.Volume = (volume == "NaN" || volume.Length == 0) ? Double.NaN : Convert.ToDouble(volume);
								bars.Add(bar);
							}
						}
					}
				}
				else
				{
					bool bp;
					bp = true;
				}
			}
			return bars;
		}

		private async Task load(string key, BarData barData)
		{
			await load1(key, barData);
		}

		private async Task load1(string key, BarData barData)
		{
			string[] fields = key.Split(':');
			string ticker = fields[0];
			string interval = fields[1];

			if (ticker.Length > 0)
			{
				var bars = await loadBars(ticker, interval);
				barData.SetBars(interval, bars);
			}
		}

		private void save(string key, BarData barData)
		{
			string[] fields = key.Split(':');
			string ticker = fields[0];
			string interval = fields[1];

			if (ticker.Length > 0)
			{
				var bars = barData.GetBars();
				saveBars(ticker, interval, bars);
			}
		}

		//private void save1(string key, BarData barData)
		//{

		//    string[] fields = key.Split(':');
		//    string ticker = fields[0];
		//    string interval = fields[1];

		//    if (ticker.Length > 0)
		//    {

		//        IsolatedStorageFile isf = IsolatedStorageFile.GetStore(IsolatedStorageScope.User | IsolatedStorageScope.Domain | IsolatedStorageScope.Assembly, null, null);

		//        string[] folders = isf.GetDirectoryNames("*");
		//        int count = folders.Length;
		//        bool found = false;
		//        for (int ii = 0; ii < count; ii++)
		//        {
		//            if (folders[ii] == "bars")
		//            {
		//                found = true;
		//                break;
		//            }
		//        }
		//        if (!found)
		//        {
		//            isf.CreateDirectory("bars");
		//        }

		//        string fileName = "bars/" + key.Replace(":", ",").Replace("/", ";");
		//        IsolatedStorageFileStream isfm = new IsolatedStorageFileStream(fileName, System.IO.FileMode.Create);
		//        GZipStream Compress = new GZipStream(isfm, CompressionMode.Compress);
		//        string text = key + "|" + "1" + "|" + barData.ToString(_barCount);
		//        byte[] bytes2 = System.Text.Encoding.ASCII.GetBytes(text);
		//        Int32 size = bytes2.Length;
		//        byte[] bytes1 = BitConverter.GetBytes(size);
		//        Compress.Write(bytes1, 0, bytes1.Length);
		//        Compress.Write(bytes2, 0, bytes2.Length);
		//        bytes2 = null;
		//        Compress.Close();
		//        isfm.Close();
		//    }
		//}

		//private void load1(string key, BarData barData)
		//{

		//    string[] fields = key.Split(':');
		//    string ticker = fields[0];
		//    string interval = fields[1];

		//    try
		//    {
		//        string fileName = "bars/" + key.Replace(":", ",").Replace("/", ";");
		//        IsolatedStorageFileStream isfm = new IsolatedStorageFileStream(fileName, System.IO.FileMode.Open);

		//        GZipStream Decompress = new GZipStream(isfm, CompressionMode.Decompress);
		//        byte[] bytes1 = new byte[4];
		//        while (Decompress.Read(bytes1, 0, bytes1.Length) == 4)
		//        {
		//            Int32 size = BitConverter.ToInt32(bytes1, 0);
		//            byte[] bytes2 = new byte[size];
		//            Decompress.Read(bytes2, 0, bytes2.Length);
		//            string text = System.Text.Encoding.UTF8.GetString(bytes2);
		//            bytes2 = null;

		//            string[] fields1 = text.Split('|');
		//            if (fields1.Length == 3 && fields1[0].Length > 0)
		//            {
		//                string dataKey = fields1[0];
		//                string version = fields1[1];
		//                if (version == "1")
		//                {
		//                    string data = fields1[2];
		//                    barData.Parse(interval, data);
		//                }
		//            }
		//        }
		//        Decompress.Close();
		//        isfm.Close();
		//    }
		//    catch (Exception)
		//    {
		//    }

		//    if (!barData.CheckBars(ticker, interval))
		//    {
		//        barData.ClearBars();
		//    }
		//}

		private class ApiLogger : Logging.Callback
		{
			public void OnMessage(long threadId, TraceLevel level, Datetime dateTime, string loggerName, string message)
			{
				try
				{

					if (level == TraceLevel.Error)
					{
						Error = true;
					}
				}
				catch (Exception)
				{
				}
			}

			public bool Error { get; set; }
		}

		private Session openSession()
		{
			string Host = "localhost";
			int Port = 8194;

			var sessionOptions = new SessionOptions
			{
				ServerHost = Host,
				ServerPort = Port,
				ClientMode = SessionOptions.ClientModeType.DAPI,
				AutoRestartOnDisconnection = true,
				ConnectTimeout = 60000
			};

			Session session = new Session(sessionOptions, new Bloomberglp.Blpapi.EventHandler(processEvent));
			if (session.Start())
			{
				bool ok = session.OpenService("//blp/refdata");
				if (ok)
				{
					if (_updateTimeSpan.TotalSeconds == 0)
					{
						ok = ok && session.OpenService("//blp/mktdata");
						ok = ok && session.OpenService("//blp/mktbar");
					}
				}

				if (ok)
				{
					_bloombergConnectionOk = true;
				}
				else
				{
					session.Stop();
					session = null;
				}
			}
			else
			{
				session = null;
			}
			return session;
		}

		private DateTime _lastRequestTime = DateTime.Now;

		private void bloombergConnectionThread()
		{
			while (_runThread)
			{
				for (int ii = 0; ii < _numberOfThreads; ii++)
				{
					if (_bloombergSessions[ii] == null)
					{
						Session session = openSession();
						_bloombergSessions[ii] = session;
						if (session == null)
						{
							_bloombergConnectionOk = false;
						}
					}
				}
				Thread.Sleep(60 * 1000);
			}
		}

		private void requestBarThread(object obj)
		{
			int sleepMilliseconds = 50;

			int id = (int)obj;
			string threadId = id.ToString();

			while (_runThread)
			{
				BarRequest request = null;

				int pending = 0;
				lock (_requestList)
				{
					if ((DateTime.Now - _lastRequestTime).TotalSeconds > 15)
					{
						_requestCount = 0;
					}
					pending = _requestCount;
				}

				if (pending < 8)
				{
					lock (_requestList)
					{
						if (_requestList.Count > 0)
						{
							request = _requestList[0];
							_requestList.RemoveAt(0);
						}
					}

					if (request != null && request.Ticker.Length > 0 && request.Interval.Length > 0)
					{
						_lastRequestTime = DateTime.Now;

						string ticker = request.Ticker;
						string interval = request.Interval;
						string barDataKey = ticker + ":" + interval;


						//string message = "Request 1 " + ticker + " " + interval;
						//Debug.WriteLine(message);


						BarData barData = getBarData(barDataKey);


						//message = "Request 2 " + ticker + " " + interval;
						//Debug.WriteLine(message);


						DateTime barDataTimeStamp;
						lock (barData)
						{
							barDataTimeStamp = barData.DateTime;
						}

						if (barDataTimeStamp == DateTime.MinValue || isPolygon(interval))
						{
							int barCount = 0;
							lock (barData)
							{
								load(barDataKey, barData);
								barCount = barData.Count;
								barData.DateTime = DateTime.Now;
							}
							barData.FireEvent(new BarEventArgs(BarEventArgs.EventType.BarsLoaded, ticker, interval, barCount));
						}

						if (isCQGSymbol(ticker) || isRangeInterval(interval))
						{
							//requestCQGBars(request);
						}
						else if (_bloombergSessions[id] != null && !isPolygon(interval))
						{
							requestBars(threadId, _bloombergSessions[id], request);
						}
						else
						{
							var barCount = barData.Count;
							barData.FireEvent(new BarEventArgs(BarEventArgs.EventType.BarsLoaded, ticker, interval, barCount));
						}

					}
				}
				Thread.Sleep(sleepMilliseconds);
			}
			if (_bloombergSessions[id] != null)
			{
				_bloombergSessions[id].Stop();
			}
		}

		private bool isCQGSymbol(string symbol)
		{
			return Portfolio.isCQGSymbol(symbol);
		}

		private bool isRangeInterval(string interval)
		{
			return interval[0] == 'R';
		}

		private string getHistoricalIntervalName(string interval)
		{
			string name = "";
			if (interval == "Y")
			{
				name = "YEARLY";
			}
			if (interval == "S")
			{
				name = "SEMI_ANNUALLY";
			}
			else if (interval == "Q")
			{
				name = "QUARTERLY";
			}
			else if (interval == "M")
			{
				name = "MONTHLY";
			}
			else if (interval == "W")
			{
				name = "WEEKLY";
			}
			else if (interval == "D")
			{
				name = "DAILY";
			}
			return name;
		}

		private DateTime getLastBarDateTime(string ticker, string interval)
		{
			DateTime date = DateTime.MinValue;

			string barDataKey = ticker + ":" + interval;

			BarData barData = getBarData(barDataKey);

			int minBarCount = 100;

			if (interval == "Y") minBarCount = 2;
			else if (interval == "S") minBarCount = 4;
			else if (interval == "Q") minBarCount = 8;
			else if (interval == "M") minBarCount = 24;
			else if (interval == "W") minBarCount = 100;
			else if (interval == "D") minBarCount = 500;

			lock (barData)
			{
				if (barData.Count > minBarCount)
				{
					date = barData.GetRequestBarTime();
				}
			}

			return date;
		}

		private DateTime getIntradayStartDate(string symbol, string interval, int barCount)
		{
			DateTime date = getLastBarDateTime(symbol, interval);

			if (date.CompareTo(DateTime.MinValue) == 0)
			{
				int barsPerDay = 1440 / int.Parse(interval);
				int barsPerWeek = 5 * barsPerDay;
				int weeks = barCount / barsPerWeek + 1;
				int dayCount = Math.Min(110, 7 * weeks);

				date = DateTime.Now - new TimeSpan(dayCount, 0, 0, 0);
			}
			return date;
		}

		private DateTime getHistoricalStartDate(string symbol, string interval, int barCount)
		{
			DateTime date = getLastBarDateTime(symbol, interval);

			if (date.CompareTo(DateTime.MinValue) == 0)
			{
				date = new DateTime(1980, 1, 1);
			}
			return date;
		}

		private class RequestInfo
		{
			public DateTime RequestTime { get; set; }
			public string StartTime { get; set; }
		}

		private Dictionary<int, RequestInfo> _requestInfo = new Dictionary<int, RequestInfo>();

		private object _requestLock = new object();

		private void requestBars(string threadId, Session session, BarRequest barRequest)
		{
			if (session != null)
			{
				int id = barRequest.Id;
				string ticker = barRequest.Ticker;
				string interval = barRequest.Interval;
				int retryCount = barRequest.RetryCount;

				try
				{
					lock (_requestList)
					{
						_requestCount++;
					}

					bool historical = (interval == "Y" || interval == "S" || interval == "Q" || interval == "M" || interval == "W" || interval == "D");

					if (historical)
					{
						int barCount = Math.Max(_barCount, barRequest.BarCount);

						TimeSpan oneYear = new TimeSpan(365, 0, 0, 0);
						DateTime endTime = DateTime.Now + oneYear;
						DateTime startTime = getHistoricalStartDate(ticker, interval, barCount);

						Service service = session.GetService("//blp/refdata");
						Request request = service.CreateRequest("HistoricalDataRequest");

						Element securities = request.GetElement("securities");
						securities.AppendValue(ticker);

						Element fields = request.GetElement("fields");
						fields.AppendValue("PX_OPEN");
						fields.AppendValue("PX_HIGH");
						fields.AppendValue("PX_LOW");
						fields.AppendValue("PX_VOLUME");
						fields.AppendValue(LAST_PRICE_TAG);
						request.Set("periodicitySelection", getHistoricalIntervalName(interval));
						request.Set("startDate", startTime.ToString("yyyyMMdd"));
						request.Set("endDate", endTime.ToString("yyyyMMdd"));
						request.Set("maxDataPoints", barCount + 100);
						request.Set("nonTradingDayFillOption", "ACTIVE_DAYS_ONLY");
						request.Set("nonTradingDayFillMethod", "PREVIOUS_VALUE");
						request.Set("overrideOption", "OVERRIDE_OPTION_CLOSE");
						request.Set("returnEids", true);

						if (barRequest.Log)
						{
							//Trace.WriteLine("Request bars: " + ticker + " " + interval + " " + startTime.ToString("yyyyMMdd") + " " + " " + endTime.ToString("yyyyMMdd") + " bar count = " + barCount + 100);
						}

						var correlationId = "";
						lock (_requestLock)
						{
							var ri = new RequestInfo();
							ri.RequestTime = DateTime.Now;
							ri.StartTime = startTime.ToString("yyyyMMdd");
							_requestInfo[_requestId] = ri;
							var now = DateTime.Now;
							correlationId = threadId + ":" + id + ":" + ticker + ":" + interval + ":" + retryCount.ToString() + ":" + _requestId + ":" + barRequest.Log + ":" + now.ToString("HHmmss");
							_requestId = ((_requestId + 1) % 1000);
						}

						session.SendRequest(request, new CorrelationID(correlationId));
					}
					else
					{
						TimeSpan oneDay = new TimeSpan(1, 0, 0, 0);
						DateTime endTime = DateTime.Now + oneDay;
						DateTime startTime = getIntradayStartDate(ticker, interval, _barCount);

						Service service = session.GetService("//blp/refdata");
						Request request = service.CreateRequest("IntradayBarRequest");

						request.Set("eventType", "TRADE");
						request.Set("security", ticker);
						request.Set("interval", interval);
						request.Set("startDateTime", new BDateTime(startTime.Year, startTime.Month, startTime.Day, startTime.Hour, startTime.Minute, startTime.Second, 0));
						request.Set("endDateTime", new BDateTime(endTime.Year, endTime.Month, endTime.Day, endTime.Hour, endTime.Minute, endTime.Second, 0));
						request.Set("maxDataPoints", _barCount);
						request.Set("gapFillInitialBar", false);
						request.Set("interval", interval);

						var correlationId = "";
						lock (_requestLock)
						{
							var ri = new RequestInfo();
							ri.RequestTime = DateTime.Now;
							ri.StartTime = startTime.ToString("yyyyMMdd");
							_requestInfo[_requestId] = ri;
							var now = DateTime.Now;
							correlationId = threadId + ":" + id + ":" + ticker + ":" + interval + ":" + retryCount.ToString() + ":" + _requestId + ":" + barRequest.Log + ":" + now.ToString("HHmmss");
							_requestId = ((_requestId + 1) % 1000);
						}

						session.SendRequest(request, new CorrelationID(correlationId));
					}
				}
				catch (Bloomberglp.Blpapi.RequestQueueOverflowException)
				{
					lock (_requestList)
					{
						if (_requestCount > 0)
						{
							_requestCount--;
						}
					}
					retryBarRequest(retryCount, id, ticker, interval, _barCount);
				}
				catch (Bloomberglp.Blpapi.DuplicateCorrelationIDException ex)
				{
					Log.Add(ex.Message);
				}
				catch (InvalidOperationException ex)
				{
					for (int ii = 0; ii < _numberOfThreads; ii++)
					{
						_bloombergSessions[ii] = null;
					}
				}

				if (_updateTimeSpan.TotalSeconds == 0 && barRequest.Update)
				{
					initializeUpdating(threadId, session, ticker, interval);
				}
			}
		}

		private bool retryBarRequest(int retryCount, int id, string ticker, string interval, int barCount)
		{
			bool failed = false;

			retryCount++;

			if (retryCount < 10)
			{
				BarRequest barRequest = new BarRequest(retryCount, id, ticker, interval, barCount);
				lock (_requestList)
				{
					_requestList.Add(barRequest);
				}
			}
			else
			{
				failed = true;
				//Trace.WriteLine("Retried Bloomberg bar request for " + ticker + " " + interval + " 10 times without success.");

				string barDataKey = ticker + ":" + interval;
				var barData = getBarData(barDataKey);
				barData.FireEvent(new BarEventArgs(BarEventArgs.EventType.BarsReceived, ticker, interval, barCount));
			}
			return failed;
		}

		private void processEvent(Event eventObj, Session session)
		{
			try
			{
				switch (eventObj.Type)
				{
					case Event.EventType.RESPONSE:
						processRequestDataEvent(eventObj, session, true);
						lock (_requestList)
						{
							if (_requestCount > 0)
							{
								_requestCount--;
							}
						}
						break;
					case Event.EventType.PARTIAL_RESPONSE:
						processRequestDataEvent(eventObj, session, false);
						break;
					case Event.EventType.SUBSCRIPTION_DATA:
						processSubscriptionDataEvent(eventObj, session);
						break;
					case Event.EventType.SUBSCRIPTION_STATUS:
						processSubscriptionStatusEvent(eventObj, session);
						break;
					default:
						processMiscEvents(eventObj, session);
						break;
				}
			}
			catch (System.Exception e)
			{
				string message = e.Message.ToString();
				//Trace.WriteLine(message);
				lock (_requestList)
				{
					if (_requestCount > 0)
					{
						_requestCount--;
					}
				}
			}

		}



		private void initializeUpdating(string threadId, Session session, string ticker, string interval)
		{
			try
			{
				if (ticker.Length > 0)
				{
					bool historical = !isCQGSymbol(ticker) && (interval == "Y" || interval == "S" || interval == "Q" || interval == "M" || interval == "W" || interval == "D");
					string subscriptionKey = ticker + ":" + (historical ? "" : interval);

					bool ok = false;
					lock (_subscriptionInfos)
					{
						if (_subscriptionInfos.ContainsKey(subscriptionKey))
						{
							if (_subscriptionInfos[subscriptionKey].Subscribe)
							{
								ok = true;
								_subscriptionInfos[subscriptionKey].Subscribe = false;
								_subscriptionInfos[subscriptionKey].ThreadId = int.Parse(threadId);
								//Log.Add("SUBSCRIBE: " + ticker + " " + interval);
							}
						}
					}

					if (ok)
					{
						List<string> fields = new List<string>();
						fields.Add("LAST_PRICE");

						List<string> options = new List<string>();
						if (!historical)
						{
							//List<string> intervals = getSubscriptionIntervals(ticker);
							options.Add("interval=" + interval);
						}

						string security = historical ? ticker : "//blp/mktbar" + "/ticker/" + ticker;

						List<Subscription> subscriptions = new List<Subscription>();
						Subscription subscription = new Subscription(security, fields, options, new CorrelationID(subscriptionKey));
						subscriptions.Add(subscription);

						session.Subscribe(subscriptions);
					}
				}
			}
			catch (Exception)
			{
			}
		}

		private List<string> getSubscriptionIntervals(string symbol)
		{
			List<string> intervals = new List<string>();
			lock (_subscriptionInfos)
			{
				foreach (string subscription in _subscriptionInfos.Keys)
				{
					string[] fields = subscription.Split(':');
					string ticker = fields[0];
					string interval = fields[1];
					if (ticker == symbol && interval.Length > 0)
					{
						intervals.Add(interval);
					}
				}
			}
			return intervals;
		}

		private void processSubscriptionDataEvent(Event eventObj, Session session)
		{
			try
			{
				foreach (var msg in eventObj)
				{
					string key = (string)msg.CorrelationID.Object;
					string[] fields = key.Split(':');

					string ticker = fields[0];
					string interval = fields[1];

					string barKey = "";
					DateTime dateTime = DateTime.MinValue;
					double open = double.NaN;
					double high = double.NaN;
					double low = double.NaN;
					double close = double.NaN;
					double volume = double.NaN;

					int barCount = 0;
					if (msg.MessageType.Equals(Bloomberglp.Blpapi.Name.GetName("MarketDataEvents")))
					{
						bool isBar = false;

						foreach (Element field in msg.Elements)
						{
							if (field.NumValues > 0)
							{
								if (field.Name.Equals("TIME"))
								{
									dateTime = field.GetValueAsTime().ToSystemDateTime();
									dateTime = new DateTime(dateTime.Year, dateTime.Month, dateTime.Day);
									barKey = dateTime.ToString("yyyyMMdd");
								}
								else if (field.Name.Equals("LAST_PRICE"))
								{
									close = field.GetValueAsFloat64();
								}
								else if (field.Name.Equals("OPEN"))
								{
									open = field.GetValueAsFloat64();
									isBar = true;
								}
								else if (field.Name.Equals("HIGH"))
								{
									high = field.GetValueAsFloat64();
									isBar = true;
								}
								else if (field.Name.Equals("LOW"))
								{
									low = field.GetValueAsFloat64();
									isBar = true;
								}
								else if (field.Name.Equals("VOLUME"))
								{
									if (field.NumValues != 0)
									{
										volume = field.GetValueAsInt64();
									}
								}
							}
						}

						if (barKey.Length > 0)
						{
							barCount++;

							string[] intervals = new string[] { "D", "W", "M", "Q", "S", "Y" };

							foreach (string interval2 in intervals)
							{
								bool change = false;

								BarData barData = getBarData(key + interval2, false);    // to move to intraday px change line to 0,0,0 and this line BarData barData = getBarData(key + interval2) from getBarData(key)

								if (barData != null && barData.Count > 0)
								{

									lock (barData)
									{
										if (isBar)
										{
											Bar bar = new Bar(dateTime, open, high, low, close, volume);
											barData.AddBar(interval2, barKey, bar, true);
											change = true;
										}
										else
										{
											change = barData.UpdateBar(interval2, dateTime, barKey, close, volume);
										}
									}

									if (change)
									{
										barData.FireEvent(new BarEventArgs(BarEventArgs.EventType.BarsUpdated, ticker, interval2, barCount));
									}
								}
							}
						}
					}
					else if (msg.MessageType.Equals(Bloomberglp.Blpapi.Name.GetName("MarketBarStart")))
					{
						foreach (Element field in msg.Elements)
						{
							if (field.Name.Equals("DATE_TIME"))
							{
								dateTime = field.GetValueAsTime().ToSystemDateTime();
								barKey = dateTime.ToString("yyyyMMddHHmm");
							}
							else if (field.Name.Equals("OPEN"))
							{
								open = field.GetValueAsFloat64();
							}
							else if (field.Name.Equals("HIGH"))
							{
								high = field.GetValueAsFloat64();
							}
							else if (field.Name.Equals("LOW"))
							{
								low = field.GetValueAsFloat64();
							}
							else if (field.Name.Equals("CLOSE"))
							{
								close = field.GetValueAsFloat64();
							}
							else if (field.Name.Equals("VOLUME"))
							{
								volume = field.GetValueAsFloat64();
							}
						}

						if (barKey.Length > 0)
						{
							barCount++;

							Bar bar = new Bar(dateTime, open, high, low, close, volume);

							BarData barData = getBarData(key, false);

							if (barData != null)
							{
								lock (barData)
								{
									barData.AddBar(interval, barKey, bar); // processSubscriptionDataEvent
								}

								barData.FireEvent(new BarEventArgs(BarEventArgs.EventType.BarsUpdated, ticker, interval, barCount));
							}
						}
					}
					else if (msg.MessageType.Equals(Bloomberglp.Blpapi.Name.GetName("MarketBarUpdate")))
					{
						foreach (Element field in msg.Elements)
						{
							if (field.Name.Equals("DATE_TIME"))
							{
								dateTime = field.GetValueAsTime().ToSystemDateTime();
								barKey = dateTime.ToString("yyyyMMddHHmm");
							}
							else if (field.Name.Equals("CLOSE"))
							{
								close = field.GetValueAsFloat64();
							}
							else if (field.Name.Equals("VOLUME"))
							{
								volume = field.GetValueAsFloat64();
							}
						}

						if (barKey.Length > 0)
						{
							barCount++;

							BarData barData = getBarData(key, false);
							if (barData != null)
							{
								bool change = false;

								lock (barData)
								{
									change = barData.UpdateBar(interval, dateTime, barKey, close, volume);
								}

								if (change)
								{
									barData.FireEvent(new BarEventArgs(BarEventArgs.EventType.BarsUpdated, ticker, interval, barCount));
								}
							}
						}
					}
					else
					{
						//Debug.WriteLine("Unhandled message type in processing Bloomberg update = " + msg.MessageType.ToString());
					}
				}
			}
			catch (Exception x)
			{
				Debug.WriteLine("Exception processing Bloomberg bar update = " + x.Message);
			}
		}

		private void processSubscriptionStatusEvent(Event eventObj, Session session)
		{
			List<string> dataList = new List<string>();
			foreach (Message msg in eventObj)
			{
				if (msg.MessageType.Equals(Bloomberglp.Blpapi.Name.GetName("SubscriptionStarted")))
				{
					try
					{
						if (msg.HasElement("exceptions"))
						{
							Element error = msg.GetElement("exceptions");
							for (int errorIndex = 0; errorIndex < error.NumValues; errorIndex++)
							{
								Element errorException = error.GetValueAsElement(errorIndex);
								string field = errorException.GetElementAsString(FIELD_ID);
								Element reason = errorException.GetElement(REASON);
								string message = reason.GetElementAsString(DESCRIPTION);
								//Trace.WriteLine(message);
							}
						}
					}
					catch (Exception ex)
					{
						string message = ex.Message;
					}
				}
				else
				{
					if (msg.MessageType.Equals(Bloomberglp.Blpapi.Name.GetName("SubscriptionFailure")))
					{
						if (msg.HasElement(REASON))
						{
							Element reason = msg.GetElement(REASON);
							string message = reason.GetElementAsString(DESCRIPTION);
						}
					}
				}
			}
		}

		private void processRequestDataEvent(Event eventObj, Session session, bool sendBarUpdate)
		{
			string securityName = string.Empty;
			Boolean hasFieldError = false;

			foreach (Message msg in eventObj.GetMessages())
			{
				string correlationId = (string)msg.CorrelationID.Object;

				//Trace.WriteLine("Process " + correlationId);

				string[] fields = correlationId.Split(':');

				bool retry = false;

				string threadId = fields[0];
				int id = int.Parse(fields[1]);
				string ticker = fields[2];
				string interval = fields[3];
				int retryCount = int.Parse(fields[4]);
				int requestId = int.Parse(fields[5]);

				bool log = (fields.Length > 6) ? bool.Parse(fields[6]) : false;
				var now = DateTime.Now;
				var second1 = 3600 * now.Hour + 60 * now.Minute + now.Second;
				var second2 = 3600 * int.Parse(fields[7].Substring(0, 2)) + 60 * int.Parse(fields[7].Substring(2, 2)) + int.Parse(fields[7].Substring(4, 2));
				var seconds = second1 - second2;

				//Trace.WriteLine(seconds + " Response for " + ticker + " " + interval);

				string barDataKey = ticker + ":" + interval;

				BarData barData = getBarData(barDataKey);

				if (msg.MessageType.Equals(Bloomberglp.Blpapi.Name.GetName("HistoricalDataResponse")))
				{
					if (msg.HasElement(RESPONSE_ERROR))
					{
						Element error = msg.GetElement(RESPONSE_ERROR);
						string message = error.GetElementAsString(MESSAGE);
						//Trace.WriteLine("Bar error: " + ticker + " " + interval + " " + message);
						retryBarRequest(retryCount, id, ticker, interval, _barCount);
					}
					else
					{
						Element secDataArray = msg.GetElement(SECURITY_DATA);
						int numberOfSecurities = secDataArray.NumValues;
						if (secDataArray.HasElement(SECURITY_ERROR))
						{
							Element secError = secDataArray.GetElement(SECURITY_ERROR);
							string message = secError.GetElementAsString(MESSAGE);
						}
						if (secDataArray.HasElement(FIELD_EXCEPTIONS))
						{
							Element error = secDataArray.GetElement(FIELD_EXCEPTIONS);
							for (int errorIndex = 0; errorIndex < error.NumValues; errorIndex++)
							{
								Element errorException = error.GetValueAsElement(errorIndex);
								string field = errorException.GetElementAsString(FIELD_ID);
								Element errorInfo = errorException.GetElement(ERROR_INFO);
								string message = errorInfo.GetElementAsString(MESSAGE);
								//Trace.WriteLine(message);
								hasFieldError = true;
							}
						}

						int barCount = 0;
						for (int index = 0; index < numberOfSecurities; index++)
						{
							foreach (Element secData in secDataArray.Elements)
							{
								switch (secData.Name.ToString())
								{
									case "eidsData":
										break;
									case "security":
										securityName = secData.GetValueAsString();
										break;
									case "fieldData":
										if (!hasFieldError && secData.NumValues > 0)
										{
											//Trace.WriteLine(securityName + " " + secData.NumValues);
											for (int pointIndex = 0; pointIndex < secData.NumValues; pointIndex++)
											{
												Element field = secData.GetValueAsElement(pointIndex);
												if (field.HasElement("date"))
												{
													Element item = field.GetElement("date");
													DateTime time = item.GetValueAsTime().ToSystemDateTime();
													string barKey = time.ToString("yyyyMMdd");

													double open = double.NaN;
													double high = double.NaN;
													double low = double.NaN;
													double close = double.NaN;
													double volume = double.NaN;

													if (field.HasElement("PX_OPEN"))
													{
														open = field.GetElement("PX_OPEN").GetValueAsFloat64();
													}
													if (field.HasElement("PX_HIGH"))
													{
														high = field.GetElement("PX_HIGH").GetValueAsFloat64();
													}
													if (field.HasElement("PX_LOW"))
													{
														low = field.GetElement("PX_LOW").GetValueAsFloat64();
													}
													if (field.HasElement("PX_VOLUME"))
													{
														volume = field.GetElement("PX_VOLUME").GetValueAsFloat64();
													}
													if (field.HasElement(LAST_PRICE_TAG))
													{
														close = field.GetElement(LAST_PRICE_TAG).GetValueAsFloat64();
													}

													barCount++;

													Bar bar = new Bar(time, open, high, low, close, volume);

													//if (log)
													//{
													//    var sym = ticker;
													//    sym = sym.PadLeft(20);
													//    var intervalText = interval;
													//    intervalText = intervalText.PadLeft(12);
													//    Trace.WriteLine("Bar      : " +sym + " " + intervalText + " " + bar.ToString());
													//}

													lock (barData)
													{
														retry |= barData.AddBar(interval, barKey, bar).Item2;
													}
												}
											}
										}
										break;
								}
							}
						}
						if (sendBarUpdate)
						{
							if (retry)
							{
								//Debug.WriteLine("Reload bars for " + ticker + " " + interval);
								DeleteFile(ticker, interval);
								barData.ClearBars();
								if (retryBarRequest(retryCount, id, ticker, interval, _barCount))
								{
									// retry failed...
									barData.FireEvent(new BarEventArgs(BarEventArgs.EventType.BarsReceived, ticker, interval, barCount));
								}
							}
							else
							{
								var ri = _requestInfo[requestId];
								var ts = DateTime.Now - ri.RequestTime;
								//Trace.WriteLine("Bloomberg response: " + ticker + " " + interval + " " + ri.StartTime + " " + ts.TotalSeconds);

								lock (barData)
								{
									save(barDataKey, barData);
								}
								barData.FireEvent(new BarEventArgs(BarEventArgs.EventType.BarsReceived, ticker, interval, barCount));
							}
						}
					}
				}
				else if (msg.MessageType.Equals(Bloomberglp.Blpapi.Name.GetName("IntradayBarResponse")))
				{
					if (msg.HasElement(RESPONSE_ERROR))
					{
						Element error = msg.GetElement(RESPONSE_ERROR);
						if (msg.NumElements == 1)
						{
							string message = error.GetElementAsString(MESSAGE);
							//Trace.WriteLine(message);
							retryBarRequest(retryCount, id, ticker, interval, _barCount);
						}
					}
					else
					{
						int barCount = 0;
						Element barDataArray = msg.GetElement("barData");
						foreach (Element element in barDataArray.Elements)
						{
							if (element.Name.ToString() == "barTickData")
							{
								for (int pointIndex = 0; pointIndex < element.NumValues; pointIndex++)
								{
									Element field = element.GetValueAsElement(pointIndex);

									Element item = field.GetElement("time");
									DateTime time = item.GetValueAsTime().ToSystemDateTime();
									string barKey = time.ToString("yyyyMMddHHmm");

									double open = double.NaN;
									double high = double.NaN;
									double low = double.NaN;
									double close = double.NaN;
									double volume = double.NaN;

									if (field.HasElement("open"))
									{
										open = field.GetElement("open").GetValueAsFloat64();
									}
									if (field.HasElement("high"))
									{
										high = field.GetElement("high").GetValueAsFloat64();
									}
									if (field.HasElement("low"))
									{
										low = field.GetElement("low").GetValueAsFloat64();
									}
									if (field.HasElement("close"))
									{
										close = field.GetElement("close").GetValueAsFloat64();
									}
									if (field.HasElement("volume"))
									{
										volume = field.GetElement("volume").GetValueAsFloat64();
									}

									barCount++;

									Bar bar = new Bar(time, open, high, low, close, volume);

									lock (barData)
									{
										retry |= barData.AddBar(interval, barKey, bar).Item2;
									}

								}
							}
						}

						if (sendBarUpdate)
						{
							retry = false;
							if (retry)
							{
								//Debug.WriteLine("Reload bars for " + ticker + " " + interval);
								DeleteFile(ticker, interval);
								barData.ClearBars();
								if (retryBarRequest(retryCount, id, ticker, interval, _barCount))
								{
									// retry failed...
									barData.FireEvent(new BarEventArgs(BarEventArgs.EventType.BarsReceived, ticker, interval, barCount));
								}
							}
							else
							{
								var ri = _requestInfo[requestId];
								var ts = DateTime.Now - ri.RequestTime;
								//Trace.WriteLine("Bloomberg response: " + ticker + " " + interval + " " + ri.StartTime + " " + ts.TotalSeconds);

								lock (barData)
								{
									save(barDataKey, barData);
								}

								barData.FireEvent(new BarEventArgs(BarEventArgs.EventType.BarsReceived, ticker, interval, barCount));
							}
						}
					}
				}
			}
		}

		private void processMiscEvents(Event eventObj, Session session)
		{
			foreach (Message msg in eventObj.GetMessages())
			{
				switch (msg.MessageType.ToString())
				{
					case "SessionStarted":
						break;
					case "SessionTerminated":
					case "SessionStopped":
						break;
					case "ServiceOpened":
						break;
					case "RequestFailure":
						Element reason = msg.GetElement(REASON);
						string message = string.Concat("Error: Source-", reason.GetElementAsString(SOURCE),
							", Code-", reason.GetElementAsString(ERROR_CODE), ", category-", reason.GetElementAsString(CATEGORY),
							", desc-", reason.GetElementAsString(DESCRIPTION));
						//Trace.WriteLine(message);

						lock (_requestList)
						{
							if (_requestCount > 0)
							{
								_requestCount--;
							}
						}
						break;
					default:
						string defaultMessage = msg.MessageType.ToString();
						break;
				}
			}
		}

		private BarData getBarData(string key, bool createIfMissing = true)
		{
			BarData barData = null;
			lock (_barDatas)
			{
				if (!_barDatas.TryGetValue(key, out barData))
				{
					if (createIfMissing)
					{
						string[] fields = key.Split(':');
						if (fields[0].Length > 0 && fields[0] != "Index")
						{
							barData = new BarData(fields[0], fields[1], _barCount + 100);
							_barDatas[key] = barData;

							//Debug.WriteLine("Add " + key + "  " + _barDatas.Count);
						}
					}
				}
			}
			return barData;
		}

		public void RemoveBarData(string key)
		{
			BarData barData = null;
			lock (_barDatas)
			{
				if (_barDatas.TryGetValue(key, out barData))
				{
					_barDatas.Remove(key);
				}
			}
		}

		public void Subscribe(int id, string ticker, string interval, bool highPriority, BarEventHandler eventHandler, int barCount, bool update, bool log = false)
		{
			bool historical = !isCQGSymbol(ticker) && (interval == "Y" || interval == "S" || interval == "Q" || interval == "M" || interval == "W" || interval == "D");
			string subscriptionKey = ticker + ":" + (historical ? "" : interval);

			lock (_subscriptionInfos)
			{
				if (_subscriptionInfos.ContainsKey(subscriptionKey))
				{
					_subscriptionInfos[subscriptionKey].ReferenceCount = _subscriptionInfos[subscriptionKey].ReferenceCount + 1;
				}
				else
				{
					_subscriptionInfos[subscriptionKey] = new SubscriptionInfo();
					//Log.Add("Add subscription: " + ticker + " " + interval + " " + _subscriptionInfos.Count.ToString());
				}
			}

			string barDataKey = ticker + ":" + interval;

			BarData barData = getBarData(barDataKey);
			if (barData != null)
			{
				lock (barData)
				{
					barData.Subscribe(eventHandler);
				}
			}

			BarRequest barRequest = new BarRequest(0, id, ticker, interval, barCount, update, log);
			lock (_requestList)
			{
				if (highPriority)
				{
					_requestList.Insert(0, barRequest);
				}
				else
				{
					_requestList.Add(barRequest);
				}
			}
		}

		public void Unsubscribe(int id, string ticker, string interval, BarEventHandler eventHandler)
		{
			bool historical = !isCQGSymbol(ticker) && (interval == "Y" || interval == "S" || interval == "Q" || interval == "M" || interval == "W" || interval == "D");
			string subscriptionKey = ticker + ":" + (historical ? "" : interval);
			lock (_subscriptionInfos)
			{
				SubscriptionInfo subscription;
				if (_subscriptionInfos.TryGetValue(subscriptionKey, out subscription))
				{
					int referenceCount = subscription.ReferenceCount;
					referenceCount--;
					subscription.ReferenceCount = referenceCount;

					if (referenceCount <= 0)
					{
						if (isCQGSymbol(ticker))
						{
							string barDataKey = ticker + ":" + interval;
							BarData barData = getBarData(barDataKey, false);
							if (barData != null) //&& barData.cqgId != null)
							{
								//var cqgId1 = barData.cqgId.TimedBarRequest;
								//if (cqgId1 != null)
								//{
								//    CEL.RemoveTimedBars(cqgId1);
								//}
								//var cqgId2 = barData.cqgId.RangeBarRequest;
								//if (cqgId2 != null)
								//{
								//    CEL.RemoveRangeBars(cqgId2);
								//}
							}
						}
						else
						{
							int threadId = subscription.ThreadId;
							if (threadId != -1)
							{

								try
								{
									Session session = _bloombergSessions[threadId];
									if (session != null)
									{
										session.Cancel(new CorrelationID(subscriptionKey));
									}
								}
								catch
								{
								}
							}
						}


						var intervals = new List<string>();
						if (historical)
						{
							intervals.Add("D");
							intervals.Add("W");
							intervals.Add("M");
							intervals.Add("Q");
							intervals.Add("S");
							intervals.Add("Y");
						}
						else
						{
							intervals.Add(interval);
						}

						foreach (var interval1 in intervals)
						{
							string barDataKey = ticker + ":" + interval1;
							BarData barData = getBarData(barDataKey, false);
							if (barData != null)
							{
								lock (barData)
								{
									barData.Release(eventHandler);
									barData.ClearBars();
								}

								lock (_barDatas)
								{
									_barDatas.Remove(barDataKey);
								}

								//Debug.WriteLine("Remove " + barDataKey + "  " + _barDatas.Count);
							}
						}

						_subscriptionInfos.Remove(subscriptionKey);
					}
				}
			}

			List<BarRequest> removeRequests = new List<BarRequest>();
			lock (_requestList)
			{
				foreach (var request in _requestList)
				{
					if (request.Id == id && request.Ticker == ticker && request.Interval == interval)
					{
						removeRequests.Add(request);
					}
				}

				foreach (var request in removeRequests)
				{
					_requestList.Remove(request);
				}
			}
		}

		public double GetLastPrice(PriceType priceType, string ticker, string interval, int offset)
		{
			double price = double.NaN;

			string barDataKey = ticker + ":" + interval;

			BarData barData = getBarData(barDataKey);

			lock (barData)
			{
				price = barData.GetLastPrice(priceType, offset);
			}

			return price;
		}

		public List<Bar> GetBars(string ticker, string interval, int barCount)
		{
			List<Bar> bars = new List<Bar>();

			string barDataKey = ticker + ":" + interval;

			BarData barData = getBarData(barDataKey);
			if (barData != null)
			{
				lock (barData)
				{
					if (barData.Count == 0)
					{
						load(barDataKey, barData);
					}

					bars = barData.GetBars();
					int count = bars.Count - barCount;
					if (count > 0)
					{
						bars.RemoveRange(0, count);
					}
				}
			}

			return bars;
		}

	}

	public class BarCache
	{
		private static int _nextId = 1;
		private static BarServer _barServer = null;
		private static readonly string LocalDataPath = @"C:\Users\Public\Documents\ATMML\Bloomberg";

		// Negative cache: ticker:interval combos where both Bloomberg and local file
		// returned no data. Skip immediately on subsequent calls to avoid log spam
		// and repeated filesystem hits on every 500ms timer tick.
		// Instance-level (not static) so it resets if BarCache is recreated.
		private readonly HashSet<string> _localFileMissing = new HashSet<string>();

		private int _id;
		BarEventHandler _eventHandler = null;
		List<string> _subscriptions = new List<string>();
		const SpreadType _spreadType = SpreadType.Ratio;

		public int MaxBarCount { get { return BarServer.MaxBarCount; } }

		enum SpreadType
		{
			Difference,
			Ratio,
			Percent
		}

		public BarCache(BarEventHandler eventHandler)
		{
			_id = _nextId++;
			_eventHandler = eventHandler;
			if (_barServer == null)
			{
				_barServer = new BarServer();
			}
		}

		public static void Shutdown()
		{
			_barServer.Shutdown();
		}

		public void ClearCache()
		{
			_barServer.ClearCache();
		}

		~BarCache()
		{
			Clear();
		}

		private void barChanged(object sender, BarEventArgs e)
		{
			if (_update || e.Type != BarEventArgs.EventType.BarsUpdated)
			{
				string ticker = e.Ticker;
				string interval1 = convertInputInterval(e.Interval);

				List<string> symbols = new List<string>();
				List<string> subscriptions = new List<string>();
				lock (_subscriptions)
				{
					foreach (string subscription in _subscriptions)
					{
						subscriptions.Add(subscription);
					}
				}

				foreach (string subscription in subscriptions)
				{
					string[] fields = subscription.Split(':');
					string symbol = fields[0];
					string interval2 = fields[1];

					string[] tickers = symbol.Split(Symbol.SpreadCharacter);
					bool spread = (tickers.Length == 2);

					bool symbolOk = false;
					foreach (string ticker1 in tickers)
					{
						if (ticker1 == ticker)
						{
							symbolOk = true;
							break;
						}
					}


					if (symbolOk && interval1 == interval2)
					{
						bool ok = true;
						if (spread)
						{
							ok = false;
							List<Bar> bars1 = _barServer.GetBars(tickers[0].Trim(), interval1, 1);
							List<Bar> bars2 = _barServer.GetBars(tickers[1].Trim(), interval1, 1);
							ok = (bars1.Count > 0 && bars2.Count > 0);
						}

						if (ok)
						{
							symbols.Add(symbol);
						}
					}
				}

				if (_eventHandler != null)
				{
					foreach (string symbol in symbols)
					{
						BarEventArgs barEventArgs = new BarEventArgs(e.Type, symbol, interval1, e.BarCount);
						_eventHandler(this, barEventArgs);
					}

				}
			}
		}

		private string convertInputInterval(string input)
		{
			string output = input;
			if (input == "Yearly" || input == "1Y")
			{
				output = "Y";
			}
			else if (input == "SemiAnnually" || input == "1S")
			{
				output = "S";
			}
			else if (input == "Quarterly" || input == "1Q")
			{
				output = "Q";
			}
			else if (input == "Monthly" || input == "1M")
			{
				output = "M";
			}
			else if (input == "Weekly" || input == "1W")
			{
				output = "W";
			}
			else if (input == "Daily" || input == "1D")
			{
				output = "D";
			}

			output = output.Replace(" Min", "");
			return output;
		}

		public void Clear()
		{
			lock (_subscriptions)
			{
				foreach (string subscription in _subscriptions)
				{
					string[] fields = subscription.Split(':');
					string ticker = fields[0];
					string interval = fields[1];
					_barServer.Unsubscribe(_id, ticker, interval, barChanged);
				}

				_subscriptions.Clear();
			}
		}

		public void Clear(string symbol, string interval, bool removeData = false)
		{
			string[] tickers = symbol.Split(Symbol.SpreadCharacter);

			interval = convertInputInterval(interval);

			foreach (string field in tickers)
			{
				string ticker = field.Trim();

				string barDataKey = ticker + ":" + interval;

				_barServer.Unsubscribe(_id, ticker, interval, barChanged);

				if (removeData)
				{
					_barServer.DeleteFile(symbol, interval);
					_barServer.RemoveBarData(barDataKey);
				}
			}

			List<string> remove = new List<string>();

			lock (_subscriptions)
			{
				foreach (string subscription in _subscriptions)
				{
					string[] fields = subscription.Split(':');
					if (fields[0] == symbol)
					{
						remove.Add(subscription);
					}
				}

				foreach (string subscription in remove)
				{
					_subscriptions.Remove(subscription);
				}
			}
		}

		bool _update = false;

		public void RequestBars(string symbol, string interval, bool highPriority = false, int barCount = BarServer.MaxBarCount, bool update = false, bool log = false)
		{
			var bp = false;
			if (symbol.StartsWith("SPX"))
			{
				bp = true;
			}


			if (update) _update = true;

			string[] tickers = symbol.Split(Symbol.SpreadCharacter);
			bool spread = (tickers.Length == 2);

			interval = convertInputInterval(interval);

			lock (_subscriptions)
			{
				_subscriptions.Add(symbol + ":" + interval);
			}


			foreach (string field in tickers)
			{
				string ticker = field.Trim();

				if (ticker != "Index")
				{
					//Debug.WriteLine("subscribe to " + ticker + " " + interval + " " + barCount);

					_barServer.Subscribe(_id, ticker, interval, highPriority, barChanged, barCount, update, log);

					if (spread && _spreadType == SpreadType.Difference && interval != "M")
					{
						_barServer.Subscribe(_id, ticker, "M", highPriority, barChanged, barCount, update, log);
					}
				}
			}
		}

		public List<DateTime> GetTimes(string symbol, string interval, int extra, int barCount = BarServer.MaxBarCount)
		{
			interval = convertInputInterval(interval);

			string[] tickers = symbol.Split(Symbol.SpreadCharacter);

			string ticker = tickers[0].Trim();

			List<DateTime> output = new List<DateTime>();
			List<Bar> bars = GetBars(ticker, interval, extra, barCount);
			foreach (Bar bar in bars)
			{
				output.Add(bar.Time);
			}
			return output;
		}

		public double GetClosePrice(string symbol, DateTime dateTime)
		{
			string interval = convertInputInterval("Daily");

			double price = double.NaN;
			List<Bar> bars = GetBars(symbol, interval, 0, 1);
			foreach (Bar bar in bars)
			{
				if (bar.Time.Year == dateTime.Year && bar.Time.Month == dateTime.Month && bar.Time.Day == dateTime.Day)
				{
					price = bar.Close;
					break;
				}
			}
			return price;
		}

		public double GetLastPrice(PriceType priceType, string symbol, string interval, int offset)
		{
			double price = double.NaN;

			string[] tickers = symbol.Split(Symbol.SpreadCharacter);
			bool spread = (tickers.Length == 2);

			interval = convertInputInterval(interval);

			double price1 = _barServer.GetLastPrice(priceType, tickers[0].Trim(), interval, offset);
			if (!double.IsNaN(price1))
			{
				if (spread)
				{
					double price2 = _barServer.GetLastPrice(priceType, tickers[1].Trim(), interval, offset);
					if (!double.IsNaN(price2))
					{
						if (_spreadType == SpreadType.Difference)
						{
							price = price1 - price2;
						}
						else
						{
							price = price1 / price2;
						}
					}
				}
				else
				{
					price = price1;
				}
			}

			return price;
		}

		public int GetRequestCount()
		{
			return _barServer.GetRequestCount();
		}

		public string GetRequestSymbol()
		{
			return _barServer.GetRequestSymbol();
		}

		public Series[] GetSeries(string symbol, string interval, string[] priceTypes, int extra, int barCount = BarServer.MaxBarCount)
		{
			interval = convertInputInterval(interval);

			int seriesCount = priceTypes.Length;
			Series[] series = new Series[seriesCount + 1];

			List<Bar> bars = GetBars(symbol, interval, extra, barCount);

			barCount = bars.Count;

			for (int ii = 0; ii < seriesCount; ii++)
			{
				series[ii] = new Series();
				series[ii].Capacity = barCount;
				if (priceTypes[ii] == "Open") foreach (Bar bar in bars) series[ii].Append(bar.Open);
				else if (priceTypes[ii] == "High") foreach (Bar bar in bars) series[ii].Append(bar.High);
				else if (priceTypes[ii] == "Low") foreach (Bar bar in bars) series[ii].Append(bar.Low);
				else if (priceTypes[ii] == "Close") foreach (Bar bar in bars) series[ii].Append(bar.Close);
			}
			return series;
		}

		public List<Bar> GetBars(string symbol, string interval, int extra, int barCount = BarServer.MaxBarCount)
		{

			List<Bar> bars = new List<Bar>();

			var rangeBars = interval[0] == 'R';

			string[] tickers = symbol.Split(Symbol.SpreadCharacter);
			bool spread = (tickers.Length == 2);

			interval = convertInputInterval(interval);

			List<Bar> bars1 = _barServer.GetBars(tickers[0].Trim(), interval, barCount);

			// LOCAL FALLBACK: If Bloomberg returned no data, try local files
			if (bars1 == null || bars1.Count == 0)
			{
				string missingKey = tickers[0].Trim() + ":" + interval;
				if (!_localFileMissing.Contains(missingKey))
				{
					bars1 = GetBarsFromLocalFile(tickers[0].Trim(), interval);
					if (bars1 == null || bars1.Count == 0)
						_localFileMissing.Add(missingKey);   // don't retry next tick
				}
			}

			if (spread)
			{
				if (_spreadType == SpreadType.Difference)
				{
					List<Bar> bars2 = _barServer.GetBars(tickers[1].Trim(), interval, barCount);

					// LOCAL FALLBACK for second ticker
					if (bars2 == null || bars2.Count == 0)
					{
						string missingKey2 = tickers[1].Trim() + ":" + interval;
						if (!_localFileMissing.Contains(missingKey2))
						{
							bars2 = GetBarsFromLocalFile(tickers[1].Trim(), interval);
							if (bars2 == null || bars2.Count == 0)
								_localFileMissing.Add(missingKey2);
						}
					}

					foreach (Bar bar1 in bars1)
					{
						int index = bars2.BinarySearch(bar1);

						double value = (index >= 0) ? bar1.Close - bars2[index].Close : double.NaN;

						Bar bar = new Bar(bar1.Time);
						bar.Open = value;
						bar.High = value;
						bar.Low = value;
						bar.Close = value;
						bars.Add(bar);
					}
				}
				else if (_spreadType == SpreadType.Percent)
				{
					double price1 = GetLastPrice(PriceType.Close, tickers[0].Trim(), "M", 1);
					double price2 = GetLastPrice(PriceType.Close, tickers[1].Trim(), "M", 1);

					if (!double.IsNaN(price1) && !double.IsNaN(price2))
					{
						double ratio = price1 / price2;

						List<Bar> bars2 = _barServer.GetBars(tickers[1].Trim(), interval, barCount);

						// LOCAL FALLBACK for second ticker
						if (bars2 == null || bars2.Count == 0)
						{
							string missingKey2 = tickers[1].Trim() + ":" + interval;
							if (!_localFileMissing.Contains(missingKey2))
							{
								bars2 = GetBarsFromLocalFile(tickers[1].Trim(), interval);
								if (bars2 == null || bars2.Count == 0)
									_localFileMissing.Add(missingKey2);
							}
						}

						foreach (Bar bar1 in bars1)
						{
							int index = bars2.BinarySearch(bar1);

							double value = (index >= 0) ? bar1.Close - (ratio * bars2[index].Close) : double.NaN;

							Bar bar = new Bar(bar1.Time);
							bar.Open = value;
							bar.High = value;
							bar.Low = value;
							bar.Close = value;
							bars.Add(bar);
						}
					}
				}
				else
				{
					List<Bar> bars2 = _barServer.GetBars(tickers[1].Trim(), interval, barCount);

					// LOCAL FALLBACK for second ticker
					if (bars2 == null || bars2.Count == 0)
					{
						string missingKey2 = tickers[1].Trim() + ":" + interval;
						if (!_localFileMissing.Contains(missingKey2))
						{
							bars2 = GetBarsFromLocalFile(tickers[1].Trim(), interval);
							if (bars2 == null || bars2.Count == 0)
								_localFileMissing.Add(missingKey2);
						}
					}

					foreach (Bar bar1 in bars1)
					{
						int index = bars2.BinarySearch(bar1);

						double value = (index >= 0) ? (100.00 * bar1.Close / bars2[index].Close) : double.NaN;

						Bar bar = new Bar(bar1.Time);
						bar.Open = value;
						bar.High = value;
						bar.Low = value;
						bar.Close = value;
						bars.Add(bar);
					}
				}
			}
			else
			{
				bool closeOnly = true;
				foreach (Bar bar1 in bars1)
				{
					if (bar1 != null)
					{
						if (closeOnly && !double.IsNaN(bar1.Open) && bar1.Open != bar1.Close)
						{
							closeOnly = false;
						}
						Bar bar = new Bar(bar1.Time, bar1.Open, bar1.High, bar1.Low, bar1.Close, bar1.Volume);
						bars.Add(bar);
					}
					else
					{
						// todo why null bars
					}
				}
				if (closeOnly) bars.ForEach(x => x.Open = x.Low = x.High = x.Close);
			}

			// ... rest of the method for adding extra bars remains the same ...
			int count = bars.Count;
			if (count > 0 && extra > 0)
			{
				// ... existing code for adding extra future bars ...
				DateTime lastTime = bars[count - 1].Time;
				TimeSpan oneDay = new TimeSpan(1, 0, 0, 0);

				if (interval == "Y")
				{
					int addCount = 0;
					DateTime time = lastTime;
					DateTime prvTime = lastTime;
					while (addCount < extra)
					{
						time = time.Add(oneDay);
						if (time.DayOfWeek != DayOfWeek.Saturday && time.DayOfWeek != DayOfWeek.Sunday)
						{
							if (time.Year != prvTime.Year && prvTime != lastTime)
							{
								Bar bar1 = new Bar(prvTime, double.NaN, double.NaN, double.NaN, double.NaN, 0);
								bars.Add(bar1);
								addCount++;
							}
							prvTime = time;
						}
					}
				}
				else if (interval == "S")
				{
					int addCount = 0;
					DateTime time = lastTime;
					DateTime prvTime = lastTime;
					while (addCount < extra)
					{
						time = time.Add(oneDay);
						if (time.DayOfWeek != DayOfWeek.Saturday && time.DayOfWeek != DayOfWeek.Sunday)
						{
							if (time.Month != prvTime.Month && prvTime != lastTime && (prvTime.Month % 6) == 0)
							{
								Bar bar1 = new Bar(prvTime, double.NaN, double.NaN, double.NaN, double.NaN, 0);
								bars.Add(bar1);
								addCount++;
							}
							prvTime = time;
						}
					}
				}
				else if (interval == "Q")
				{
					int addCount = 0;
					DateTime time = lastTime;
					DateTime prvTime = lastTime;
					while (addCount < extra)
					{
						time = time.Add(oneDay);
						if (time.DayOfWeek != DayOfWeek.Saturday && time.DayOfWeek != DayOfWeek.Sunday)
						{
							if (time.Month != prvTime.Month && prvTime != lastTime && (prvTime.Month % 3) == 0)
							{
								Bar bar1 = new Bar(prvTime, double.NaN, double.NaN, double.NaN, double.NaN, 0);
								bars.Add(bar1);
								addCount++;
							}
							prvTime = time;
						}
					}
				}
				else if (interval == "M")
				{
					int addCount = 0;
					DateTime time = lastTime;
					DateTime prvTime = lastTime;
					while (addCount < extra)
					{
						time = time.Add(oneDay);
						if (time.DayOfWeek != DayOfWeek.Saturday && time.DayOfWeek != DayOfWeek.Sunday)
						{
							if (time.Month != prvTime.Month && prvTime != lastTime)
							{
								Bar bar1 = new Bar(prvTime, double.NaN, double.NaN, double.NaN, double.NaN, 0);
								bars.Add(bar1);
								addCount++;
							}
							prvTime = time;
						}
					}
				}
				else if (interval == "W" || interval == "W30")
				{
					int addCount = 0;
					DateTime time = lastTime;
					while (addCount < extra)
					{
						time = time.Add(oneDay);
						if (time.DayOfWeek == DayOfWeek.Friday)
						{
							Bar bar1 = new Bar(time, double.NaN, double.NaN, double.NaN, double.NaN, 0);
							bars.Add(bar1);
							addCount++;
						}
					}
				}
				else if (interval == "D" || interval == "D30")
				{
					int addCount = 0;
					DateTime time = lastTime;
					while (addCount < extra)
					{
						time = time.Add(oneDay);
						if (time.DayOfWeek != DayOfWeek.Saturday && time.DayOfWeek != DayOfWeek.Sunday)
						{
							Bar bar1 = new Bar(time, double.NaN, double.NaN, double.NaN, double.NaN, 0);
							bars.Add(bar1);
							addCount++;
						}
					}
				}
				else
				{
					int addCount = 0;
					DateTime time = lastTime;
					int minutes = rangeBars ? 1 : int.Parse(interval);
					TimeSpan timeSpan = new TimeSpan(0, minutes, 0);
					while (addCount < extra)
					{
						time = time.Add(timeSpan);
						if (time.DayOfWeek != DayOfWeek.Saturday && time.DayOfWeek != DayOfWeek.Sunday)
						{
							Bar bar1 = new Bar(time, double.NaN, double.NaN, double.NaN, double.NaN, 0);
							bars.Add(bar1);
							addCount++;
						}
					}
				}
			}
			return bars;
		}
		/// <summary>
		/// Load bars from local .txt file when Bloomberg is unavailable
		/// File format: YYYYMMDD:Open:High:Low:Close:Volume
		/// Example: 20210922:141.1441:143.0788:140.4113:142.512:76404341
		/// </summary>
		/// <summary>
		/// Load bars from local .txt file when Bloomberg is unavailable
		/// File structure: {LocalDataPath}\{Ticker}\{Interval}.txt
		/// Example: C:\Users\Public\Documents\ATMML\Bloomberg\AAPL US Equity\D.txt
		/// File format: YYYYMMDD:Open:High:Low:Close:Volume
		/// </summary>
		private List<Bar> GetBarsFromLocalFile(string ticker, string interval)
		{
			var bars = new List<Bar>();

			try
			{

				// Map interval to file name
				string intervalFile = interval;
				if (interval == "D" || interval == "D30") intervalFile = "D";
				else if (interval == "W" || interval == "W30") intervalFile = "W";
				else if (interval == "M") intervalFile = "M";
				else if (interval == "Q") intervalFile = "Q";
				else if (interval == "Y") intervalFile = "Y";

				// Path: {LocalDataPath}\{Ticker}\{Interval}.txt
				var folderPath = Path.Combine(LocalDataPath, ticker);
				var filePath = Path.Combine(folderPath, intervalFile + ".txt");


				// If exact interval file doesn't exist, try to load Daily and convert
				bool needsConversion = false;
				if (!File.Exists(filePath) && intervalFile != "D")
				{
					var dailyPath = Path.Combine(folderPath, "D.txt");
					if (File.Exists(dailyPath))
					{
						filePath = dailyPath;
						needsConversion = true;
					}
				}

				if (!File.Exists(filePath))
				{
					return bars;
				}


				int lineCount = 0;
				int parsedCount = 0;

				foreach (var line in File.ReadLines(filePath))
				{
					lineCount++;

					if (string.IsNullOrWhiteSpace(line))
						continue;

					// Format: YYYYMMDD:Open:High:Low:Close:Volume
					var parts = line.Split(':');

					if (parts.Length < 6)
						continue;

					// Parse date (YYYYMMDD)
					if (!DateTime.TryParseExact(parts[0].Trim(), "yyyyMMdd",
						CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date))
						continue;

					// Parse OHLCV
					if (!double.TryParse(parts[1].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double open))
						continue;
					if (!double.TryParse(parts[2].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double high))
						continue;
					if (!double.TryParse(parts[3].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double low))
						continue;
					if (!double.TryParse(parts[4].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double close))
						continue;

					double volume = 0;
					double.TryParse(parts[5].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out volume);

					bars.Add(new Bar(date, open, high, low, close, volume));
					parsedCount++;
				}


				// Convert to requested interval if needed
				if (bars.Count > 0 && needsConversion)
				{
					bars = ConvertToInterval(bars, interval);
				}

			}
			catch (Exception ex)
			{
			}

			return bars.OrderBy(b => b.Time).ToList();
		}

		/// <summary>
		/// Convert daily bars to other intervals (Weekly, Monthly, etc.)
		/// </summary>
		private List<Bar> ConvertToInterval(List<Bar> dailyBars, string interval)
		{
			if (dailyBars == null || dailyBars.Count == 0)
				return dailyBars;

			var result = new List<Bar>();

			if (interval == "W" || interval == "W30")
			{
				// Group by week ending Friday
				var grouped = dailyBars.GroupBy(b =>
				{
					int daysUntilFriday = ((int)DayOfWeek.Friday - (int)b.Time.DayOfWeek + 7) % 7;
					if (daysUntilFriday == 0 && b.Time.DayOfWeek != DayOfWeek.Friday)
						daysUntilFriday = 7;
					return b.Time.AddDays(daysUntilFriday).Date;
				});

				foreach (var week in grouped.OrderBy(g => g.Key))
				{
					var weekBars = week.OrderBy(b => b.Time).ToList();
					result.Add(new Bar(
						weekBars.Last().Time,
						weekBars.First().Open,
						weekBars.Max(b => b.High),
						weekBars.Min(b => b.Low),
						weekBars.Last().Close,
						weekBars.Sum(b => b.Volume)
					));
				}
			}
			else if (interval == "M")
			{
				var grouped = dailyBars.GroupBy(b => new DateTime(b.Time.Year, b.Time.Month, 1));

				foreach (var month in grouped.OrderBy(g => g.Key))
				{
					var monthBars = month.OrderBy(b => b.Time).ToList();
					result.Add(new Bar(
						monthBars.Last().Time,
						monthBars.First().Open,
						monthBars.Max(b => b.High),
						monthBars.Min(b => b.Low),
						monthBars.Last().Close,
						monthBars.Sum(b => b.Volume)
					));
				}
			}
			else if (interval == "Q")
			{
				var grouped = dailyBars.GroupBy(b => new DateTime(b.Time.Year, ((b.Time.Month - 1) / 3) * 3 + 1, 1));

				foreach (var quarter in grouped.OrderBy(g => g.Key))
				{
					var qtrBars = quarter.OrderBy(b => b.Time).ToList();
					result.Add(new Bar(
						qtrBars.Last().Time,
						qtrBars.First().Open,
						qtrBars.Max(b => b.High),
						qtrBars.Min(b => b.Low),
						qtrBars.Last().Close,
						qtrBars.Sum(b => b.Volume)
					));
				}
			}
			else if (interval == "Y")
			{
				var grouped = dailyBars.GroupBy(b => new DateTime(b.Time.Year, 1, 1));

				foreach (var year in grouped.OrderBy(g => g.Key))
				{
					var yearBars = year.OrderBy(b => b.Time).ToList();
					result.Add(new Bar(
						yearBars.Last().Time,
						yearBars.First().Open,
						yearBars.Max(b => b.High),
						yearBars.Min(b => b.Low),
						yearBars.Last().Close,
						yearBars.Sum(b => b.Volume)
					));
				}
			}
			else
			{
				return dailyBars;
			}

			return result;
		}
	}
}