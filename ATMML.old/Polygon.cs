using Bloomberglp.Blpapi;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using TimeZoneConverter;
using static ATMML.IdeaCalculator;

namespace ATMML
{
	public class PolygonData
	{
		private const string polygonKey = "5xLFKQIB6kmckPnh1NepGAez1pjn1kGP";
		private const string CacheBasePath = @"C:\Users\Public\Documents\ATMML\Polygon";
		private object polygonLock = new object();
		public event BarEventHandler BarChanged;

		public class PolygonBar
		{
			public double c { get; set; }
			public double h { get; set; }
			public double l { get; set; }
			public int n { get; set; }
			public double o { get; set; }
			public double t { get; set; }
			public double v { get; set; }
			public double vw { get; set; }
		}

		public class PolygonBars
		{
			public bool adjusted { get; set; }
			public int queryCount { get; set; }
			public string request_id { get; set; }
			public List<PolygonBar> results { get; set; }
			public int resultsCount { get; set; }
			public string status { get; set; }
			public string ticker { get; set; }
		}

		public void FireEvent(BarEventArgs e)
		{
			if (BarChanged != null)
			{
				BarChanged(this, e);
			}
		}

		#region Cache Methods

		/// <summary>
		/// Saves bars to the cache file for the given ticker and interval.
		/// Path: C:\Users\Public\Documents\ATMML\Polygon\{ticker}\{interval}.csv
		/// </summary>
		private void SaveToCache(string polygonTicker, string interval, List<Bar> bars)
		{
			if (bars == null || bars.Count == 0) return;

			lock (polygonLock)
			{
				var dir = Path.Combine(CacheBasePath, polygonTicker);
				Directory.CreateDirectory(dir);

				var filePath = Path.Combine(dir, $"{interval}.csv");

				using (var writer = new StreamWriter(filePath))
				{
					writer.WriteLine("Time,Open,High,Low,Close,Volume");
					foreach (var bar in bars.OrderBy(b => b.Time))
					{
						writer.WriteLine($"{bar.Time:yyyy-MM-dd HH:mm:ss},{bar.Open},{bar.High},{bar.Low},{bar.Close},{bar.Volume}");
					}
				}
			}
		}

		/// <summary>
		/// Loads bars from the cache file for the given ticker and interval.
		/// Returns empty list if cache file doesn't exist.
		/// </summary>
		private List<Bar> LoadFromCache(string polygonTicker, string interval)
		{
			var filePath = Path.Combine(CacheBasePath, polygonTicker, $"{interval}.csv");
			var bars = new List<Bar>();

			if (!File.Exists(filePath)) return bars;

			using (var reader = new StreamReader(filePath))
			{
				// Skip header
				reader.ReadLine();

				string line;
				while ((line = reader.ReadLine()) != null)
				{
					var parts = line.Split(',');
					if (parts.Length >= 6)
					{
						var bar = new Bar(
							DateTime.ParseExact(parts[0], "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
							double.Parse(parts[1], CultureInfo.InvariantCulture),
							double.Parse(parts[2], CultureInfo.InvariantCulture),
							double.Parse(parts[3], CultureInfo.InvariantCulture),
							double.Parse(parts[4], CultureInfo.InvariantCulture),
							double.Parse(parts[5], CultureInfo.InvariantCulture)
						);
						bars.Add(bar);
					}
				}
			}

			return bars;
		}

		/// <summary>
		/// Checks if cache exists for the given ticker and interval.
		/// </summary>
		private bool CacheExists(string polygonTicker, string interval)
		{
			var filePath = Path.Combine(CacheBasePath, polygonTicker, $"{interval}.csv");
			return File.Exists(filePath);
		}

		/// <summary>
		/// Gets the cache file path for the given ticker and interval.
		/// </summary>
		private string GetCachePath(string ticker, string interval)
		{
			return Path.Combine(CacheBasePath, ticker, $"{interval}.csv");
		}

		/// <summary>
		/// Builds daily bars from cached intraday, excluding the last bar of each day
		/// </summary>
		private List<Bar> BuildDailyBarsFromIntraday(string ticker, string interval)
		{
			var polygonInterval = interval.Substring(1);
			var bars = LoadFromCache(ticker, polygonInterval);
			return BuildBarsFromIntraday(bars, interval);
		}

		/// <summary>
		/// Builds daily bars from intraday bars, excluding the last intraday bar of each day.
		/// </summary>
		private List<Bar> BuildBarsFromIntraday(List<Bar> bars, string interval)
		{
			var polygonInterval = interval.Substring(1);

			if (bars == null || bars.Count == 0) return new List<Bar>();

			// Filter out the 15:30 bar (last bar of each day)
			var filteredBars = bars
				.Where(b => b.Time.TimeOfDay < new TimeSpan(15, 60 - int.Parse(polygonInterval), 0))
				.ToList();

			var intervalType = interval[0];

			// Group by date and aggregate
			var outputBars = filteredBars
				.GroupBy(b => (intervalType == 'D') ? 10000 * b.Time.Date.Year + 100 * b.Time.Month + b.Time.Day : GetIsoYearWeek(b.Time.Date))
				.Select(g => new Bar(
					(intervalType == 'D') ? new DateTime(g.Key / 10000, g.Key / 100 % 100, g.Key % 100) : IsoYearWeekToFriday(g.Key / 100, g.Key % 100),                     // Date at midnight
					g.OrderBy(b => b.Time).First().Open,  // Open from first bar
					g.Max(b => b.High),                   // High of day
					g.Min(b => b.Low),                    // Low of day
					g.OrderBy(b => b.Time).Last().Close,  // Close from last bar
					g.Sum(b => b.Volume)                  // Total volume
				))
				.OrderBy(b => b.Time)
				.ToList();

			return outputBars;
		}

		public static DateTime IsoYearWeekToFriday(int isoYear, int isoWeek)
		{
			// Jan 4 is always in ISO week 1
			var jan4 = new DateTime(isoYear, 1, 4);

			// Find Monday of week 1
			int dayOffset = ((int)jan4.DayOfWeek + 6) % 7; // Mon=0
			var week1Monday = jan4.AddDays(-dayOffset);

			// Add (week-1) weeks + 4 days to get Friday
			return week1Monday.AddDays((isoWeek - 1) * 7 + 4);
		}
		public static int GetIsoYearWeek(DateTime date)
		{
			var cal = CultureInfo.InvariantCulture.Calendar;

			int week = cal.GetWeekOfYear(
				date,
				CalendarWeekRule.FirstFourDayWeek,
				DayOfWeek.Monday);

			int year = date.Year;

			// ISO year boundary fix
			if (week == 52 && date.Month == 1) year--;
			if (week == 1 && date.Month == 12) year++;

			return year * 100 + week;
		}


		/// <summary>
		/// Merges two lists of bars, favoring newBars when there are duplicate timestamps.
		/// Returns a sorted list by Time.
		/// </summary>
		private List<Bar> MergeBars(List<Bar> oldBars, List<Bar> newBars)
		{
			if (oldBars == null || oldBars.Count == 0) return newBars ?? new List<Bar>();
			if (newBars == null || newBars.Count == 0) return oldBars;

			// Use dictionary keyed by DateTime, new bars overwrite old
			var merged = oldBars.ToDictionary(b => b.Time, b => b);

			foreach (var bar in newBars)
			{
				merged[bar.Time] = bar;
			}

			return merged.Values.OrderBy(b => b.Time).ToList();
		}

		private string getPolygonTicker(string ticker)
		{
			var f = ticker.Split(' ');
			var polygonTicker = f[0];

			if (polygonTicker == "BRK/B")
			{
				polygonTicker = "BRK.B";
			}
			else if (polygonTicker == "BF/B")
			{
				polygonTicker = "BF.B";
			}

			return polygonTicker;
		}

		#endregion

		public async Task<List<Bar>> GetBars(string ticker, string interval, DateTime stime, DateTime etime)
		{
			var bars = new List<Bar>();

			var polygonTicker = getPolygonTicker(ticker);

			var tickerExists = await TickerExists(polygonTicker);

			if (tickerExists)
			{
				var polygonInterval = interval.Substring(1);

				if (CacheExists(polygonTicker, polygonInterval))
				{
					var cachedBars = LoadFromCache(polygonTicker, polygonInterval);
					bars = cachedBars
						.Where(b => b.Time >= stime && b.Time <= etime)
						.ToList();
				}
				if (bars.Count > 0)
				{
					var tz = TZConvert.GetTimeZoneInfo("America/New_York");
					stime = TimeZoneInfo.ConvertTimeToUtc(bars.Last().Time, tz);
				}

				if (stime < etime)
				{
					var newBars = await FetchBars(polygonTicker, polygonInterval, stime, etime);
					bars = MergeBars(bars, newBars);
					SaveToCache(polygonTicker, polygonInterval, bars);
				}

				bars = BuildBarsFromIntraday(bars, interval);
			}
			else
			{
				bars.Add(new Bar());
			}

			FireEvent(new BarEventArgs(BarEventArgs.EventType.BarsReceived, ticker, interval, bars.Count));

			return bars;
		}

		private async Task<bool> TickerExists(string polygonTicker)
		{
			// todo: remove if index subscription exists
			if (polygonTicker.StartsWith("I:")) return false;

			//https://api.polygon.io/v3/reference/tickers/SPY?apiKey=5xLFKQIB6kmckPnh1NepGAez1pjn1kGP
			var url = "https://api.polygon.io/v3/reference/tickers/" + polygonTicker + "?" + "apiKey=" + polygonKey;
			var request = new HttpClient();
			var response = await request.GetStringAsync(url);
			return !response.Contains("NOT FOUND");
		}

		private async Task<List<Bar>> FetchBars(string polygonTicker, string interval, DateTime stime, DateTime etime)
		{
			List<Bar> output = new List<Bar>();

			//var barCount = getDaysBack(interval);

			//var etime = getBarTime(interval, DateTime.UtcNow);
			//var stime = etime.AddDays(-barCount);
			var st = Math.Floor(stime.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds);
			var et = Math.Floor(etime.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds);

			//var bars = new List<Bar>();
			//if (_bars.ContainsKey(ticker) && _bars[ticker].ContainsKey(interval))
			//{
			//	bars = _bars[ticker][interval].Bars;

			//	if (bars.Count > 0)
			//	{
			//		var idx = bars.FindLastIndex(x => !double.IsNaN(x.Open));
			//		if (idx >= 0)
			//		{
			//			if (idx >= 1) idx--;
			//			if (idx >= 1) idx--;
			//			st = bars[idx].Time.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds;
			//		}
			//	}
			//}

			var tz = TZConvert.GetTimeZoneInfo("America/New_York");
			var daySessionStart = new TimeSpan(9, 30, 0);
			var daySessionEnd = new TimeSpan(16, 0, 0);

			bool reverse = false;
			var range = (interval == "D") ? "day" : (interval == "W") ? "week" : (interval == "M") ? "month" : (interval == "Q") ? "quarter" : (interval == "Y") ? "year" : (interval == "60") ? "hour" : "minute";
			var count = (range != "minute") ? "1" : interval.ToString();

			for (int ii = 0; ii < 200; ii++)
			{
				var url = "https://api.polygon.io/v2/aggs/ticker/" + polygonTicker + "/range/" + count + "/" + range + "/" + st.ToString() + "/" + et.ToString() + "?" +
						"adjusted=true&sort=asc&limit=5000&apiKey=" + polygonKey;
				try
				{
					var addedBarCount = 0;
					// https://api.polygon.io/v2/aggs/ticker/X:BTCUSD/range/1/day/2023-01-09/2023-01-09?adjusted=true&sort=asc&limit=120&apiKey=5xLFKQIB6kmckPnh1NepGAez1pjn1kGP
					var request = new HttpClient();
					var response = await request.GetStringAsync(url);
					var data = JsonConvert.DeserializeObject<PolygonBars>(response);
					if (data.results != null)
					{
						var lastTimestamp = double.MaxValue;
						foreach (PolygonBar b in data.results)
						{
							TimeSpan ts = TimeSpan.FromMilliseconds(b.t);
							DateTime tu = new DateTime(1970, 1, 1) + ts;
							DateTime t = TimeZoneInfo.ConvertTimeFromUtc(tu, tz);
							lastTimestamp = b.t;
							if (b.t >= st && t.TimeOfDay >= daySessionStart && t.TimeOfDay < daySessionEnd)
							{
								Bar bar = new Bar(t, b.o, b.h, b.l, b.c, b.v);
								var idx = output.FindIndex(x => x.Time == t); // todo binary serach
								if (idx < 0)
								{
									output.Add(bar);
								}
								else
								{
									output[idx] = bar;
								}
								addedBarCount++;
							}
						}

						st = lastTimestamp + 1;
						if (st >= et || addedBarCount == 0) break;
					}
				}
				catch (Exception x)
				{
				}
			}

			return output;
		}
	}
}
