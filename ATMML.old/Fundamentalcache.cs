using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace ATMML
{
	/// <summary>
	/// Caches fundamental data (Beta, Sector, Industry, SubIndustry, MarketCap, BetaTTest)
	/// to local files so that repeated backtests on the same end date produce identical results
	/// without re-fetching from Bloomberg.
	///
	/// File structure: {CachePath}\{yyyy-MM-dd}\fundamentals.txt
	/// Format: Tab-separated, one line per ticker per date-keyed entry
	///   Header: TICKER\tFIELD\tDATE_KEY\tVALUE
	///   Example: AAPL US Equity\tBETA\t2026-02-27\t1.23
	///   Example: AAPL US Equity\tSECTOR\t\tInformation Technology
	/// </summary>
	public class FundamentalCache
	{
		private static readonly string CachePath = @"C:\Users\Public\Documents\ATMML\FundamentalCache";

		/// <summary>
		/// Check if a cache file exists for the given date.
		/// </summary>
		public static bool Exists(DateTime date)
		{
			var filePath = GetFilePath(date);
			return File.Exists(filePath);
		}

		/// <summary>
		/// Save fundamental data from all symbols to a cache file.
		/// Call this after all Bloomberg fundamental data has been received.
		/// </summary>
		public static void Save(DateTime date, List<Symbol> symbols)
		{
			try
			{
				var filePath = GetFilePath(date);
				var folder = Path.GetDirectoryName(filePath);
				Directory.CreateDirectory(folder);

				var sb = new StringBuilder();
				sb.AppendLine("TICKER\tFIELD\tDATE_KEY\tVALUE");

				foreach (var symbol in symbols)
				{
					var ticker = symbol.Ticker;

					// Sector (single value)
					if (!string.IsNullOrEmpty(symbol.Sector))
						sb.AppendLine($"{ticker}\tSECTOR\t\t{symbol.Sector}");

					// Industry (single value)
					if (!string.IsNullOrEmpty(symbol.Industry))
						sb.AppendLine($"{ticker}\tINDUSTRY\t\t{symbol.Industry}");

					// SubIndustry (single value)
					if (!string.IsNullOrEmpty(symbol.SubIndustry))
						sb.AppendLine($"{ticker}\tSUBINDUSTRY\t\t{symbol.SubIndustry}");

					// Beta (date-keyed dictionary)
					if (symbol.Beta != null)
					{
						foreach (var kvp in symbol.Beta.OrderBy(k => k.Key))
						{
							sb.AppendLine($"{ticker}\tBETA\t{kvp.Key}\t{kvp.Value.ToString("R", CultureInfo.InvariantCulture)}");
						}
					}

					// BetaTTest (date-keyed dictionary)
					if (symbol.BetaTTest != null)
					{
						foreach (var kvp in symbol.BetaTTest.OrderBy(k => k.Key))
						{
							sb.AppendLine($"{ticker}\tBETA_TTEST\t{kvp.Key}\t{kvp.Value.ToString("R", CultureInfo.InvariantCulture)}");
						}
					}

					// MarketCap (date-keyed dictionary)
					if (symbol.Marketcap != null)
					{
						foreach (var kvp in symbol.Marketcap.OrderBy(k => k.Key))
						{
							sb.AppendLine($"{ticker}\tMARKETCAP\t{kvp.Key}\t{kvp.Value.ToString("R", CultureInfo.InvariantCulture)}");
						}
					}
				}

				File.WriteAllText(filePath, sb.ToString());
				Trace.WriteLine($"[FundamentalCache] Saved {symbols.Count} symbols to {filePath}");
			}
			catch (Exception ex)
			{
				Trace.WriteLine($"[FundamentalCache] Save failed: {ex.Message}");
			}
		}

		/// <summary>
		/// Load fundamental data from cache and apply to symbol objects.
		/// Returns true if cache was loaded successfully.
		/// </summary>
		public static bool Load(DateTime date, List<Symbol> symbols)
		{
			try
			{
				var filePath = GetFilePath(date);
				if (!File.Exists(filePath))
					return false;

				var symbolLookup = symbols.ToDictionary(s => s.Ticker, s => s, StringComparer.OrdinalIgnoreCase);
				int loaded = 0;
				int lines = 0;

				foreach (var line in File.ReadLines(filePath))
				{
					lines++;
					if (lines == 1) continue; // skip header

					var parts = line.Split('\t');
					if (parts.Length < 4) continue;

					var ticker = parts[0];
					var field = parts[1];
					var dateKey = parts[2];
					var value = parts[3];

					if (!symbolLookup.TryGetValue(ticker, out var symbol))
						continue;

					switch (field)
					{
						case "SECTOR":
							symbol.Sector = value;
							break;
						case "INDUSTRY":
							symbol.Industry = value;
							break;
						case "SUBINDUSTRY":
							symbol.SubIndustry = value;
							break;
						case "BETA":
							if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out double beta))
							{
								symbol.Beta[dateKey] = beta;
							}
							break;
						case "BETA_TTEST":
							if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out double ttest))
							{
								symbol.BetaTTest[dateKey] = ttest;
							}
							break;
						case "MARKETCAP":
							if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out double mcap))
							{
								symbol.Marketcap[dateKey] = mcap;
							}
							break;
					}
					loaded++;
				}

				Trace.WriteLine($"[FundamentalCache] Loaded {loaded} entries for {symbolLookup.Count} symbols from {filePath}");
				return loaded > 0;
			}
			catch (Exception ex)
			{
				Trace.WriteLine($"[FundamentalCache] Load failed: {ex.Message}");
				return false;
			}
		}

		/// <summary>
		/// Delete cache for a specific date (e.g., to force refresh).
		/// </summary>
		public static void Delete(DateTime date)
		{
			var filePath = GetFilePath(date);
			if (File.Exists(filePath))
			{
				File.Delete(filePath);
				Trace.WriteLine($"[FundamentalCache] Deleted cache for {date:yyyy-MM-dd}");
			}
		}

		private static string GetFilePath(DateTime date)
		{
			return Path.Combine(CachePath, date.ToString("yyyy-MM-dd"), "fundamentals.txt");
		}
	}
}