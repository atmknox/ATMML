using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using TimeZoneConverter;

namespace PolygonFetcher
{
    public class PolygonData
    {
        private readonly string _polygonKey = "5xLFKQIB6kmckPnh1NepGAez1pjn1kGP";
        private static readonly HttpClient _httpClient = new HttpClient();
        private static readonly string CacheBasePath = @"C:\Users\Public\Documents\ATMML\Polygon";

        // Event to report progress during fetch
        public event Action<int> OnBarsUpdated;

        public class PolygonBar
        {
            public double c { get; set; }  // Close
            public double h { get; set; }  // High
            public double l { get; set; }  // Low
            public int n { get; set; }     // Number of transactions
            public double o { get; set; }  // Open
            public double t { get; set; }  // Timestamp (ms)
            public double v { get; set; }  // Volume
            public double vw { get; set; } // VWAP
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
            public string next_url { get; set; }
        }

        #region Cache Methods

        /// <summary>
        /// Saves bars to the cache file for the given ticker and interval.
        /// Path: C:\Users\Public\Documents\ATMML\Polygon\{ticker}\{interval}.csv
        /// </summary>
        public static void SaveToCache(string ticker, string interval, List<Bar> bars)
        {
            if (bars == null || bars.Count == 0) return;

            var dir = Path.Combine(CacheBasePath, ticker);
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

        /// <summary>
        /// Loads bars from the cache file for the given ticker and interval.
        /// Returns empty list if cache file doesn't exist.
        /// </summary>
        public static List<Bar> LoadFromCache(string ticker, string interval)
        {
            var filePath = Path.Combine(CacheBasePath, ticker, $"{interval}.csv");
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
        public static bool CacheExists(string ticker, string interval)
        {
            var filePath = Path.Combine(CacheBasePath, ticker, $"{interval}.csv");
            return File.Exists(filePath);
        }

        /// <summary>
        /// Gets the cache file path for the given ticker and interval.
        /// </summary>
        public static string GetCachePath(string ticker, string interval)
        {
            return Path.Combine(CacheBasePath, ticker, $"{interval}.csv");
        }

        /// <summary>
        /// Builds daily bars from cached 30-minute bars, excluding the last 30-minute bar of each day (15:30-16:00).
        /// This gives daily OHLCV from 9:30 to 15:30.
        /// </summary>
        public static List<Bar> BuildDailyBarsFrom30Min(string ticker)
        {
            var bars30 = LoadFromCache(ticker, "30");
            return BuildDailyBarsFrom30Min(bars30);
        }

        /// <summary>
        /// Builds daily bars from 30-minute bars, excluding the last 30-minute bar of each day (15:30-16:00).
        /// This gives daily OHLCV from 9:30 to 15:30.
        /// </summary>
        public static List<Bar> BuildDailyBarsFrom30Min(List<Bar> bars30)
        {
            if (bars30 == null || bars30.Count == 0) return new List<Bar>();

            // Filter out the 15:30 bar (last bar of each day)
            var filteredBars = bars30
                .Where(b => b.Time.TimeOfDay != new TimeSpan(15, 30, 0))
                .ToList();

            // Group by date and aggregate
            var dailyBars = filteredBars
                .GroupBy(b => b.Time.Date)
                .Select(g => new Bar(
                    g.Key,                              // Date at midnight
                    g.OrderBy(b => b.Time).First().Open,  // Open from first bar
                    g.Max(b => b.High),                   // High of day
                    g.Min(b => b.Low),                    // Low of day
                    g.OrderBy(b => b.Time).Last().Close,  // Close from last bar
                    g.Sum(b => b.Volume)                  // Total volume
                ))
                .OrderBy(b => b.Time)
                .ToList();

            return dailyBars;
        }

        #endregion

        public async Task<List<Bar>> FetchBars(string ticker, string interval, DateTime stime, DateTime etime)
        {
            var st = (long)Math.Floor(stime.ToUniversalTime().Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds);
            var et = (long)Math.Floor(etime.ToUniversalTime().Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds);

            var tz = TZConvert.GetTimeZoneInfo("America/New_York");
            var daySessionStart = new TimeSpan(9, 30, 0);
            var daySessionEnd = new TimeSpan(16, 0, 0);

            List<Bar> output = new List<Bar>();
            
            // Determine range and count based on interval
            var range = interval switch
            {
                "D" => "day",
                "W" => "week",
                "M" => "month",
                "Q" => "quarter",
                "Y" => "year",
                "60" => "hour",
                _ => "minute"
            };
            var count = (range != "minute") ? "1" : interval;

            for (int ii = 0; ii < 200; ii++)
            {
                var url = $"https://api.polygon.io/v2/aggs/ticker/{ticker}/range/{count}/{range}/{st}/{et}?" +
                          $"adjusted=true&sort=asc&limit=5000&apiKey={_polygonKey}";
                try
                {
                    var response = await _httpClient.GetStringAsync(url);
                    var data = JsonConvert.DeserializeObject<PolygonBars>(response);
                    
                    if (data?.results != null && data.results.Count > 0)
                    {
                        double lastTimestamp = 0;
                        foreach (PolygonBar b in data.results)
                        {
                            TimeSpan ts = TimeSpan.FromMilliseconds(b.t);
                            DateTime tu = new DateTime(1970, 1, 1) + ts;
                            DateTime t = TimeZoneInfo.ConvertTimeFromUtc(tu, tz);
                            lastTimestamp = b.t;

                            // For daily/weekly/monthly, include all bars
                            // For intraday, filter to regular session
                            bool includeBar = range == "day" || range == "week" || range == "month" || 
                                              range == "quarter" || range == "year" ||
                                              (t.TimeOfDay >= daySessionStart && t.TimeOfDay < daySessionEnd);

                            if (b.t >= st && includeBar)
                            {
                                Bar bar = new Bar(t, b.o, b.h, b.l, b.c, b.v);
                                var idx = output.FindIndex(x => x.Time == t);
                                if (idx < 0)
                                {
                                    output.Add(bar);
                                }
                                else
                                {
                                    output[idx] = bar;
                                }
                            }
                        }

                        // Notify UI of current bar count
                        OnBarsUpdated?.Invoke(output.Count);

                        st = (long)lastTimestamp + 1;
                        if (st >= et) break;
                    }
                    else
                    {
                        break;
                    }
                }
                catch (HttpRequestException)
                {
                    // Rate limited or network error - break and return what we have
                    break;
                }
                catch (Exception)
                {
                    break;
                }
            }
            
            return output;
        }
    }
}
