using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;

namespace ATMML
{
	public static class LocalBarDataFallback
	{
		private static readonly string LocalDataPath = @"C:\Users\Public\Documents\ATMML\Bloomberg";

		/// <summary>
		/// Load bar data from local .txt files
		/// Format: YYYYMMDD:Open:High:Low:Close:Volume
		/// </summary>
		public static List<Bar> GetBarsFromLocal(string ticker, string interval)
		{
			var bars = new List<Bar>();

			try
			{
				// File name matches ticker: "AAPL US Equity.txt"
				var fileName = $"{ticker}.txt";
				var filePath = Path.Combine(LocalDataPath, fileName);

				if (!File.Exists(filePath))
				{
					// Try without " US Equity" suffix
					var shortTicker = ticker.Replace(" US Equity", "").Replace(" Index", "");
					fileName = $"{shortTicker}.txt";
					filePath = Path.Combine(LocalDataPath, fileName);
				}

				if (!File.Exists(filePath))
				{
					Debug.WriteLine($"[LocalFallback] No local file for {ticker}");
					return bars;
				}

				Debug.WriteLine($"[LocalFallback] Loading {ticker} from {filePath}");

				foreach (var line in File.ReadLines(filePath))
				{
					if (string.IsNullOrWhiteSpace(line))
						continue;

					// Format: YYYYMMDD:Open:High:Low:Close:Volume
					// Example: 20210922:141.1441:143.0788:140.4113:142.512:76404341
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

					bars.Add(new Bar
					{
						Time = date,
						Open = open,
						High = high,
						Low = low,
						Close = close,
						Volume = volume
					});
				}

				Debug.WriteLine($"[LocalFallback] Loaded {bars.Count} bars for {ticker}");
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"[LocalFallback] Error loading {ticker}: {ex.Message}");
			}

			return bars.OrderBy(b => b.Time).ToList();
		}

		/// <summary>
		/// Convert daily bars to weekly bars
		/// </summary>
		public static List<Bar> ToWeeklyBars(List<Bar> dailyBars)
		{
			if (dailyBars == null || dailyBars.Count == 0)
				return dailyBars;

			var result = new List<Bar>();

			// Group by week ending Friday
			var grouped = dailyBars.GroupBy(b =>
			{
				var diff = (7 + (b.Time.DayOfWeek - DayOfWeek.Friday)) % 7;
				return b.Time.AddDays(diff == 0 ? 0 : 7 - diff).Date;
			});

			foreach (var week in grouped.OrderBy(g => g.Key))
			{
				var weekBars = week.OrderBy(b => b.Time).ToList();
				result.Add(new Bar
				{
					Time = weekBars.Last().Time,
					Open = weekBars.First().Open,
					High = weekBars.Max(b => b.High),
					Low = weekBars.Min(b => b.Low),
					Close = weekBars.Last().Close,
					Volume = weekBars.Sum(b => b.Volume)
				});
			}

			return result;
		}
	}
}
