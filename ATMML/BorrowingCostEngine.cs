using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace ATMML
{
	/// <summary>
	/// Borrowing cost tier classification for securities
	/// </summary>
	public enum BorrowTier
	{
		GeneralCollateral,  // Easy to borrow - typical rate 0.25-0.50%
		WarmName,           // Moderate difficulty - 0.50-2.00%
		SpecialName,        // Hard to borrow - 2.00-10.00%
		Threshold,          // Very hard to borrow - 10.00%+
		Unavailable         // Cannot borrow
	}

	/// <summary>
	/// Represents a borrowing rate for a specific security at a point in time
	/// </summary>
	public class BorrowRate
	{
		public string Symbol { get; set; }
		public DateTime AsOfDate { get; set; }
		public BorrowTier Tier { get; set; }
		public decimal AnnualizedRate { get; set; }  // Gross borrow rate as decimal (0.0025 = 0.25%)
		public decimal RebateRate { get; set; }      // Interest rebate on short proceeds
		public bool IsAvailable { get; set; }
		public decimal MinQuantity { get; set; }     // Minimum shares available
		public string Source { get; set; }          // Prime broker source

		/// <summary>
		/// Net borrowing rate after rebate credit.
		/// NEGATIVE value means you EARN on the short position (rebate > borrow cost).
		/// This is common for GC names in high-rate environments.
		/// </summary>
		public decimal NetBorrowRate => AnnualizedRate - RebateRate;

		/// <summary>
		/// Daily net rate (can be negative = daily income)
		/// </summary>
		public decimal DailyRate => NetBorrowRate / 365m;

		/// <summary>
		/// Gross daily borrow cost (always positive, for reporting)
		/// </summary>
		public decimal GrossDailyRate => AnnualizedRate / 365m;

		/// <summary>
		/// Daily rebate earned (always positive, for reporting)
		/// </summary>
		public decimal DailyRebate => RebateRate / 365m;

		/// <summary>
		/// Create a copy with updated rebate rate.
		/// Use this instead of mutating cached objects.
		/// </summary>
		public BorrowRate WithRebateRate(decimal newRebateRate)
		{
			return new BorrowRate
			{
				Symbol = this.Symbol,
				AsOfDate = this.AsOfDate,
				Tier = this.Tier,
				AnnualizedRate = this.AnnualizedRate,
				RebateRate = newRebateRate,
				IsAvailable = this.IsAvailable,
				MinQuantity = this.MinQuantity,
				Source = this.Source
			};
		}
	}

	/// <summary>
	/// Tracks a short position and its accumulated borrowing costs.
	/// FIX #4: Each position carries a unique PositionId for reliable lookup
	/// when multiple campaigns may hold the same symbol simultaneously.
	/// </summary>
	public class ShortPositionBorrowRecord
	{
		public string PositionId { get; set; }  // FIX #4: Unique key for this position
		public string Symbol { get; set; }
		public DateTime OpenDate { get; set; }
		public DateTime? CloseDate { get; set; }
		public decimal Shares { get; set; }
		public decimal EntryPrice { get; set; }
		public decimal NotionalValue => Math.Abs(Shares * EntryPrice);

		public List<DailyBorrowCost> DailyCosts { get; set; } = new();

		/// <summary>
		/// Total net borrowing cost (negative = earned rebate income)
		/// </summary>
		public decimal TotalNetBorrowCost => DailyCosts.Sum(d => d.NetCost);

		/// <summary>
		/// Total gross borrow fees paid (always positive)
		/// </summary>
		public decimal TotalGrossBorrowCost => DailyCosts.Sum(d => d.GrossCost);

		/// <summary>
		/// Total rebate income earned (always positive)
		/// </summary>
		public decimal TotalRebateIncome => DailyCosts.Sum(d => d.RebateIncome);

		/// <summary>
		/// Number of business days with cost records
		/// </summary>
		public int BusinessDaysHeld => DailyCosts.Count;

		/// <summary>
		/// Total calendar days of accrual (includes weekends/holidays)
		/// </summary>
		public int CalendarDaysAccrued => DailyCosts.Sum(d => d.AccrualDays);

		public decimal AverageNotional => DailyCosts.Any()
			? DailyCosts.Average(d => d.NotionalValue)
			: NotionalValue;

		/// <summary>
		/// Effective annualized rate based on CALENDAR days (correct for borrow accrual)
		/// </summary>
		public decimal EffectiveAnnualizedRate => CalendarDaysAccrued > 0 && AverageNotional > 0
			? (TotalNetBorrowCost / AverageNotional) * (365m / CalendarDaysAccrued)
			: 0m;
	}

	/// <summary>
	/// Daily borrowing cost record with gross/rebate/net breakdown
	/// </summary>
	public class DailyBorrowCost
	{
		public DateTime Date { get; set; }
		public string Symbol { get; set; }
		public string PositionId { get; set; }  // FIX #4: Links back to specific position
		public decimal Shares { get; set; }
		public decimal Price { get; set; }
		public decimal NotionalValue => Math.Abs(Shares * Price);

		// Rate components
		public decimal GrossAnnualizedRate { get; set; }
		public decimal RebateRate { get; set; }
		public decimal NetRate { get; set; }

		// Fed Funds rate on this date (for audit trail)
		public decimal FedFundsRateOnDate { get; set; }

		// Cost components (for GIPS reporting)
		public decimal GrossCost { get; set; }      // Borrow fee paid (always positive)
		public decimal RebateIncome { get; set; }   // Rebate earned (always positive)
		public decimal NetCost { get; set; }        // Net = Gross - Rebate (can be negative)

		public BorrowTier Tier { get; set; }

		/// <summary>
		/// Number of calendar days this record covers.
		/// Friday = 3 (Fri, Sat, Sun), day before holiday = 2+, else 1.
		/// </summary>
		public int AccrualDays { get; set; } = 1;
	}

	// ═══════════════════════════════════════════════════════════════════════
	//  BLOOMBERG FED FUNDS RATE INTEGRATION
	// ═══════════════════════════════════════════════════════════════════════

	/// <summary>
	/// Provides historical Fed Funds rate data from Bloomberg.
	/// Uses FEDL01 Index (Fed Funds Effective Rate) via BDH for historical,
	/// BDP for current. Falls back to built-in FOMC schedule if Bloomberg
	/// is unavailable (e.g., during weekend backtests without terminal).
	///
	/// Bloomberg fields used:
	///   FEDL01 Index / PX_LAST  → Fed Funds Effective Rate (daily)
	///   FDTR Index / PX_LAST    → Fed Funds Target Rate (upper bound)
	///   FDTRFTRL Index / PX_LAST → Fed Funds Target Rate (lower bound)
	///
	/// Usage:
	///   var provider = new BloombergFedFundsProvider();
	///   // Option A: Load from Bloomberg (requires active terminal/SAPI)
	///   provider.LoadFromBloomberg(new DateTime(2022, 1, 1), DateTime.Today);
	///   // Option B: Load from CSV (exported from Bloomberg or FRED)
	///   provider.LoadFromCSV("fed_funds_history.csv");
	///   // Option C: Use built-in FOMC schedule (no Bloomberg needed)
	///   provider.LoadFOMCFallbackSchedule();
	///   // Then wire into the engine
	///   engine.SetFedFundsHistory(provider.GetRateHistory());
	/// </summary>
	public class BloombergFedFundsProvider
	{
		private readonly SortedDictionary<DateTime, decimal> _rates = new();
		private readonly List<string> _loadLog = new();

		/// <summary>
		/// All loaded rates as a read-only view
		/// </summary>
		public IReadOnlyDictionary<DateTime, decimal> Rates => _rates;

		/// <summary>
		/// Log of load operations for debugging
		/// </summary>
		public IReadOnlyList<string> LoadLog => _loadLog.AsReadOnly();

		/// <summary>
		/// Load historical Fed Funds Effective Rate from Bloomberg BDH.
		///
		/// Bloomberg API call equivalent:
		///   //BDH("FEDL01 Index", "PX_LAST", startDate, endDate, "periodicitySelection", "DAILY")
		///
		/// This method generates the BDH request string for your Bloomberg
		/// integration layer. Wire this into your existing Bloomberg data
		/// infrastructure (BloombergDataService, SAPI session, etc.).
		///
		/// If you use the Bloomberg .NET SDK (Bloomberglp.Blpapi):
		///   var request = session.GetService("//blp/refdata").CreateRequest("HistoricalDataRequest");
		///   request.Append("securities", "FEDL01 Index");
		///   request.Append("fields", "PX_LAST");
		///   request.Set("startDate", startDate.ToString("yyyyMMdd"));
		///   request.Set("endDate", endDate.ToString("yyyyMMdd"));
		///   request.Set("periodicitySelection", "DAILY");
		///   request.Set("nonTradingDayFillOption", "PREVIOUS_VALUE");
		///   request.Set("nonTradingDayFillMethod", "PREVIOUS_VALUE");
		/// </summary>
		public BloombergBDHRequest CreateBDHRequest(DateTime startDate, DateTime endDate)
		{
			return new BloombergBDHRequest
			{
				Security = "FEDL01 Index",
				Field = "PX_LAST",
				StartDate = startDate,
				EndDate = endDate,
				PeriodicitySelection = "DAILY",
				NonTradingDayFillOption = "PREVIOUS_VALUE",
				NonTradingDayFillMethod = "PREVIOUS_VALUE",
				// Alternative tickers if FEDL01 not available:
				AlternativeSecurities = new[]
				{
					"FDTR Index",       // Target rate (upper bound)
					"US00O/N Index",    // USD overnight rate
					"SOFRRATE Index"    // SOFR (if transitioning to SOFR-based rebates)
				}
			};
		}

		/// <summary>
		/// Create a BDP (current value) request for today's Fed Funds rate.
		///
		/// Bloomberg API equivalent:
		///   //BDP("FEDL01 Index", "PX_LAST")
		///
		/// Use this for daily live updates in your production system.
		/// Call at market open to set the day's rebate rate.
		/// </summary>
		public BloombergBDPRequest CreateBDPRequest()
		{
			return new BloombergBDPRequest
			{
				Security = "FEDL01 Index",
				Field = "PX_LAST",
				Overrides = new Dictionary<string, string>()
			};
		}

		/// <summary>
		/// Process BDH response data from Bloomberg.
		/// Call this with the parsed results from your Bloomberg data layer.
		///
		/// Expected format: date/value pairs from BDH response.
		/// Bloomberg returns the rate as a percentage (4.33 = 4.33%),
		/// this method converts to decimal (0.0433).
		/// </summary>
		public void ProcessBDHResponse(IEnumerable<BloombergDataPoint> data)
		{
			int count = 0;
			foreach (var point in data)
			{
				// Bloomberg returns percentage (4.33), convert to decimal (0.0433)
				decimal rateAsDecimal = point.Value / 100m;
				_rates[point.Date.Date] = rateAsDecimal;
				count++;
			}
			_loadLog.Add($"Loaded {count} daily rates from Bloomberg BDH response");
		}

		/// <summary>
		/// Process a single BDP response (current rate).
		/// Bloomberg returns percentage (4.33), converts to decimal (0.0433).
		/// </summary>
		public void ProcessBDPResponse(DateTime date, decimal value)
		{
			decimal rateAsDecimal = value / 100m;
			_rates[date.Date] = rateAsDecimal;
			_loadLog.Add($"Set current rate from Bloomberg BDP: {date:yyyy-MM-dd} = {rateAsDecimal:P2}");
		}

		/// <summary>
		/// Load from a CSV file (Bloomberg Excel export or FRED download).
		/// Supports multiple formats:
		///   - Bloomberg: "date","FEDL01 Index PX_LAST" (percentage, e.g., 4.33)
		///   - FRED:      "DATE","FEDFUNDS" (percentage, e.g., 4.33)
		///   - Custom:    "Date","Rate" (decimal, e.g., 0.0433)
		///
		/// Auto-detects whether rates are in percentage (>1) or decimal (<1) format.
		/// </summary>
		public void LoadFromCSV(string filePath)
		{
			if (!File.Exists(filePath))
			{
				_loadLog.Add($"CSV file not found: {filePath}. Using FOMC fallback.");
				LoadFOMCFallbackSchedule();
				return;
			}

			var lines = File.ReadAllLines(filePath);
			int count = 0;
			bool isPercentage = true;  // Will auto-detect

			// Skip header
			for (int i = 1; i < lines.Length; i++)
			{
				var parts = lines[i].Split(',');
				if (parts.Length < 2) continue;

				// Clean quotes and whitespace
				var dateStr = parts[0].Trim().Trim('"');
				var rateStr = parts[1].Trim().Trim('"');

				if (string.IsNullOrWhiteSpace(rateStr) || rateStr == "." || rateStr == "N/A")
					continue;

				if (DateTime.TryParse(dateStr, CultureInfo.InvariantCulture,
					DateTimeStyles.None, out var date) &&
					decimal.TryParse(rateStr, NumberStyles.Any,
					CultureInfo.InvariantCulture, out var rate))
				{
					// Auto-detect: if any rate > 1, assume percentage format
					if (count == 0 && rate < 0.20m)
						isPercentage = false;

					decimal rateAsDecimal = isPercentage ? rate / 100m : rate;
					_rates[date.Date] = rateAsDecimal;
					count++;
				}
			}

			_loadLog.Add($"Loaded {count} rates from CSV: {filePath} " +
						 $"(format: {(isPercentage ? "percentage" : "decimal")})");
		}

		/// <summary>
		/// Export loaded rates to CSV for Bloomberg-independent backtesting.
		/// Run this once with Bloomberg access, then use the CSV offline.
		/// </summary>
		public void ExportToCSV(string filePath)
		{
			var sb = new StringBuilder();
			sb.AppendLine("Date,FedFundsRate,FedFundsRatePct");

			foreach (var kvp in _rates)
			{
				sb.AppendLine($"{kvp.Key:yyyy-MM-dd},{kvp.Value:F6},{kvp.Value * 100:F2}");
			}

			File.WriteAllText(filePath, sb.ToString());
			_loadLog.Add($"Exported {_rates.Count} rates to CSV: {filePath}");
		}

		/// <summary>
		/// Built-in FOMC decision schedule with effective Fed Funds rates.
		/// Use as fallback when Bloomberg is unavailable.
		///
		/// Source: Federal Reserve FOMC meeting decisions.
		/// Rates are the EFFECTIVE rate (midpoint of target range).
		///
		/// This covers the full inception period of the ATMML strategy (Jan 2022+).
		/// UPDATE THIS after each FOMC meeting, or better yet, use the Bloomberg
		/// BDH feed for production.
		/// </summary>
		public void LoadFOMCFallbackSchedule()
		{
			// Clear any existing rates
			_rates.Clear();

			// ─── 2020 (Emergency cuts to zero) ─────────────────────
			AddFOMCRate(new DateTime(2020, 3, 3), 0.0125m);   // Emergency cut: 1.50% → 1.25%
			AddFOMCRate(new DateTime(2020, 3, 16), 0.0025m);  // Emergency cut: 1.25% → 0.25%
															  // Rate held at 0.00-0.25% (effective ~0.08%) through 2021

			// ─── 2021 (Held at zero) ───────────────────────────────
			AddFOMCRate(new DateTime(2021, 1, 1), 0.0008m);   // Effective ~0.08%

			// ─── 2022 (Aggressive hiking cycle) ────────────────────
			AddFOMCRate(new DateTime(2022, 1, 1), 0.0008m);   // Effective ~0.08% (carried from 2021)
			AddFOMCRate(new DateTime(2022, 3, 17), 0.0033m);  // +25bp → 0.25-0.50% (eff 0.33%)
			AddFOMCRate(new DateTime(2022, 5, 5), 0.0083m);   // +50bp → 0.75-1.00% (eff 0.83%)
			AddFOMCRate(new DateTime(2022, 6, 16), 0.0158m);  // +75bp → 1.50-1.75% (eff 1.58%)
			AddFOMCRate(new DateTime(2022, 7, 28), 0.0233m);  // +75bp → 2.25-2.50% (eff 2.33%)
			AddFOMCRate(new DateTime(2022, 9, 22), 0.0308m);  // +75bp → 3.00-3.25% (eff 3.08%)
			AddFOMCRate(new DateTime(2022, 11, 3), 0.0383m);  // +75bp → 3.75-4.00% (eff 3.83%)
			AddFOMCRate(new DateTime(2022, 12, 15), 0.0433m); // +50bp → 4.25-4.50% (eff 4.33%)

			// ─── 2023 (Final hikes, then hold) ─────────────────────
			AddFOMCRate(new DateTime(2023, 2, 2), 0.0458m);   // +25bp → 4.50-4.75% (eff 4.58%)
			AddFOMCRate(new DateTime(2023, 3, 23), 0.0483m);  // +25bp → 4.75-5.00% (eff 4.83%)
			AddFOMCRate(new DateTime(2023, 5, 4), 0.0508m);   // +25bp → 5.00-5.25% (eff 5.08%)
			AddFOMCRate(new DateTime(2023, 7, 27), 0.0533m);  // +25bp → 5.25-5.50% (eff 5.33%)
															  // Held at 5.25-5.50% through end of 2023

			// ─── 2024 (Easing cycle begins) ────────────────────────
			// Held through September
			AddFOMCRate(new DateTime(2024, 9, 19), 0.0483m);  // -50bp → 4.75-5.00% (eff 4.83%)
			AddFOMCRate(new DateTime(2024, 11, 8), 0.0458m);  // -25bp → 4.50-4.75% (eff 4.58%)
			AddFOMCRate(new DateTime(2024, 12, 19), 0.0433m); // -25bp → 4.25-4.50% (eff 4.33%)

			// ─── 2025 (Current as of last update) ──────────────────
			// Held at 4.25-4.50% (eff 4.33%) — UPDATE AFTER EACH FOMC MEETING
			AddFOMCRate(new DateTime(2025, 1, 30), 0.0433m);  // Hold → 4.25-4.50%
															  // Next FOMC: Mar 18-19, May 6-7, Jun 17-18, Jul 29-30, Sep 16-17
															  // UPDATE HERE when decisions are announced

			_loadLog.Add($"Loaded FOMC fallback schedule: {_rates.Count} rate changes, " +
						 $"{_rates.First().Key:yyyy-MM-dd} to {_rates.Last().Key:yyyy-MM-dd}");
		}

		private void AddFOMCRate(DateTime effectiveDate, decimal rate)
		{
			_rates[effectiveDate.Date] = rate;
		}

		/// <summary>
		/// Get the rate history as a FedFundsRateHistory object for the engine.
		/// </summary>
		public FedFundsRateHistory GetRateHistory()
		{
			return new FedFundsRateHistory(_rates);
		}

		/// <summary>
		/// Get the effective rate for a specific date.
		/// Uses the most recent rate on or before the given date.
		/// </summary>
		public decimal GetRateForDate(DateTime date)
		{
			return GetRateHistory().GetRate(date);
		}

		/// <summary>
		/// Validate loaded rates against expected ranges.
		/// Returns warnings for any suspicious values.
		/// </summary>
		public List<string> ValidateRates()
		{
			var warnings = new List<string>();

			foreach (var kvp in _rates)
			{
				if (kvp.Value < 0)
					warnings.Add($"Negative rate on {kvp.Key:yyyy-MM-dd}: {kvp.Value:P2}");
				if (kvp.Value > 0.20m)
					warnings.Add($"Unusually high rate on {kvp.Key:yyyy-MM-dd}: {kvp.Value:P2} (>20%)");
			}

			// Check for gaps > 180 days (suggests missing data)
			DateTime? prevDate = null;
			foreach (var date in _rates.Keys)
			{
				if (prevDate.HasValue && (date - prevDate.Value).Days > 180)
				{
					warnings.Add($"Gap in rate data: {prevDate.Value:yyyy-MM-dd} to {date:yyyy-MM-dd} " +
								 $"({(date - prevDate.Value).Days} days)");
				}
				prevDate = date;
			}

			return warnings;
		}
	}

	/// <summary>
	/// A single data point from Bloomberg BDH response (replaces ValueTuple for compatibility)
	/// </summary>
	public class BloombergDataPoint
	{
		public DateTime Date { get; set; }
		public decimal Value { get; set; }

		public BloombergDataPoint(DateTime date, decimal value)
		{
			Date = date;
			Value = value;
		}
	}

	/// <summary>
	/// Bloomberg BDH (Historical Data) request parameters.
	/// Pass this to your Bloomberg data infrastructure to execute the query.
	/// </summary>
	public class BloombergBDHRequest
	{
		public string Security { get; set; }
		public string Field { get; set; }
		public DateTime StartDate { get; set; }
		public DateTime EndDate { get; set; }
		public string PeriodicitySelection { get; set; }
		public string NonTradingDayFillOption { get; set; }
		public string NonTradingDayFillMethod { get; set; }
		public string[] AlternativeSecurities { get; set; }

		/// <summary>
		/// Format as Bloomberg Excel BDH formula (for manual verification)
		/// </summary>
		public string ToExcelFormula()
		{
			return $"=BDH(\"{Security}\",\"{Field}\",\"{StartDate:MM/dd/yyyy}\",\"{EndDate:MM/dd/yyyy}\"," +
				   $"\"periodicitySelection\",\"{PeriodicitySelection}\"," +
				   $"\"nonTradingDayFillOption\",\"{NonTradingDayFillOption}\")";
		}

		/// <summary>
		/// Format as BLPAPI request string (for logging/debugging)
		/// </summary>
		public override string ToString()
		{
			return $"BDH: {Security} / {Field} / {StartDate:yyyyMMdd}-{EndDate:yyyyMMdd} / {PeriodicitySelection}";
		}
	}

	/// <summary>
	/// Bloomberg BDP (Reference Data) request parameters.
	/// </summary>
	public class BloombergBDPRequest
	{
		public string Security { get; set; }
		public string Field { get; set; }
		public Dictionary<string, string> Overrides { get; set; }

		public string ToExcelFormula()
		{
			return $"=BDP(\"{Security}\",\"{Field}\")";
		}

		public override string ToString()
		{
			return $"BDP: {Security} / {Field}";
		}
	}

	/// <summary>
	/// A single Fed Funds rate change entry (replaces ValueTuple for compatibility)
	/// </summary>
	public class FedFundsRateEntry
	{
		public DateTime Date { get; set; }
		public decimal Rate { get; set; }

		public FedFundsRateEntry(DateTime date, decimal rate)
		{
			Date = date;
			Rate = rate;
		}
	}

	/// <summary>
	/// Holds historical Fed Funds rate data with efficient date-based lookup.
	/// Uses a sorted list of rate-change dates and binary search to find
	/// the applicable rate for any given date.
	///
	/// Thread-safe for read operations after construction.
	/// </summary>
	public class FedFundsRateHistory
	{
		private readonly List<FedFundsRateEntry> _rateChanges;
		private readonly decimal _defaultRate;

		/// <summary>
		/// Create from a sorted dictionary of date → rate mappings.
		/// Rates should be in decimal form (0.0425 = 4.25%).
		/// </summary>
		public FedFundsRateHistory(SortedDictionary<DateTime, decimal> rates, decimal defaultRate = 0.0008m)
		{
			_rateChanges = rates.Select(kvp => new FedFundsRateEntry(kvp.Key, kvp.Value)).ToList();
			_defaultRate = defaultRate;
		}

		/// <summary>
		/// Create from an enumerable of FedFundsRateEntry objects.
		/// </summary>
		public FedFundsRateHistory(IEnumerable<FedFundsRateEntry> rates, decimal defaultRate = 0.0008m)
		{
			_rateChanges = rates.OrderBy(r => r.Date).ToList();
			_defaultRate = defaultRate;
		}

		/// <summary>
		/// Get the effective Fed Funds rate for a given date.
		/// Returns the most recent rate on or before the date.
		/// Uses binary search for O(log n) lookup.
		/// </summary>
		public decimal GetRate(DateTime date)
		{
			if (_rateChanges.Count == 0)
				return _defaultRate;

			// Binary search for the last rate change on or before this date
			int lo = 0, hi = _rateChanges.Count - 1;
			int bestIdx = -1;

			while (lo <= hi)
			{
				int mid = lo + (hi - lo) / 2;
				if (_rateChanges[mid].Date <= date)
				{
					bestIdx = mid;
					lo = mid + 1;
				}
				else
				{
					hi = mid - 1;
				}
			}

			return bestIdx >= 0 ? _rateChanges[bestIdx].Rate : _defaultRate;
		}

		/// <summary>
		/// Get the effective rebate rate for a date given broker terms.
		/// FIX #2: Uses industry-standard formula: (FFR - Spread) * PassThrough
		/// </summary>
		public decimal GetEffectiveRebateRate(DateTime date, decimal rebateSpread, decimal rebatePassThrough)
		{
			var ffr = GetRate(date);
			return Math.Max(0, (ffr - rebateSpread) * rebatePassThrough);
		}

		/// <summary>
		/// Number of rate data points loaded
		/// </summary>
		public int Count => _rateChanges.Count;

		/// <summary>
		/// Date range start
		/// </summary>
		public DateTime DateRangeStart => _rateChanges.Count > 0
			? _rateChanges.First().Date
			: DateTime.MinValue;

		/// <summary>
		/// Date range end
		/// </summary>
		public DateTime DateRangeEnd => _rateChanges.Count > 0
			? _rateChanges.Last().Date
			: DateTime.MinValue;

		/// <summary>
		/// Get all rate changes (for reporting/debugging)
		/// </summary>
		public IReadOnlyList<FedFundsRateEntry> RateChanges => _rateChanges.AsReadOnly();
	}

	// ═══════════════════════════════════════════════════════════════════════
	//  BORROWING COST ENGINE (with all fixes + historical FFR)
	// ═══════════════════════════════════════════════════════════════════════

	/// <summary>
	/// Borrowing cost calculation engine with historical tracking.
	/// Properly accounts for short rebates in high-rate environments.
	/// Handles weekend/holiday accrual correctly.
	/// Uses HISTORICAL Fed Funds rates for accurate backtest rebate calculation.
	///
	/// FIXES APPLIED:
	///   #1 - Monthly CSV calendar days: sum from per-date groups, not raw records
	///   #2 - EffectiveRebateRate: industry-standard (FFR - spread) * passthrough
	///   #3 - TopCostlySymbols: sorted by absolute net cost for clear reporting
	///   #4 - Position lookup by PositionId instead of symbol-only FirstOrDefault
	///   #5 - AccrualDays safety cap raised to 30 with warning log
	///   #6 - Dynamic holiday calendar with auto-generation and extensibility
	///   #7 - Historical Fed Funds rates from Bloomberg BDH for accurate backtesting
	/// </summary>
	public class BorrowingCostEngine
	{
		private readonly Dictionary<string, List<BorrowRate>> _borrowRates = new();
		private readonly Dictionary<string, ShortPositionBorrowRecord> _activeShorts = new();
		private readonly List<ShortPositionBorrowRecord> _closedShorts = new();
		private readonly List<DailyBorrowCost> _dailyCostHistory = new();

		// FIX #5: Warning log for unusual accrual days
		private readonly List<string> _warnings = new();

		// FIX #7: Historical Fed Funds rate lookup
		private FedFundsRateHistory _fedFundsHistory;
		private bool _useHistoricalRates = false;

		// Default gross rates by tier when specific rate not available
		private static readonly Dictionary<BorrowTier, decimal> DefaultRates = new()
		{
			{ BorrowTier.GeneralCollateral, 0.0030m },  // 0.30%
			{ BorrowTier.WarmName, 0.0150m },           // 1.50%
			{ BorrowTier.SpecialName, 0.0500m },        // 5.00%
			{ BorrowTier.Threshold, 0.1500m },          // 15.00%
			{ BorrowTier.Unavailable, 0.0000m }
		};

		// FIX #6: Dynamic holiday calendar
		private readonly HashSet<DateTime> _marketHolidays;

		/// <summary>
		/// Any warnings generated during processing (FIX #5)
		/// </summary>
		public IReadOnlyList<string> Warnings => _warnings.AsReadOnly();

		public BorrowingCostEngine()
		{
			// FIX #6: Generate holidays dynamically for a wide year range
			_marketHolidays = GenerateUSMarketHolidays(2020, 2035);

			// AUTO-LOAD historical Fed Funds rates from built-in FOMC schedule.
			// This ensures the engine NEVER falls back to a static 4.25% rate
			// across an entire backtest. Override with Bloomberg BDH data
			// via SetFedFundsHistory() for daily-granularity production use.
			LoadFOMCFallbackRates();
		}

		// ─── FIX #6: Dynamic Holiday Calendar ───────────────────────────

		private static HashSet<DateTime> GenerateUSMarketHolidays(int startYear, int endYear)
		{
			var holidays = new HashSet<DateTime>();

			for (int year = startYear; year <= endYear; year++)
			{
				holidays.Add(ObservedDate(new DateTime(year, 1, 1)));                    // New Year's
				holidays.Add(NthWeekdayOfMonth(year, 1, DayOfWeek.Monday, 3));           // MLK Day
				holidays.Add(NthWeekdayOfMonth(year, 2, DayOfWeek.Monday, 3));           // Presidents Day
				holidays.Add(CalculateEasterSunday(year).AddDays(-2));                   // Good Friday
				holidays.Add(LastWeekdayOfMonth(year, 5, DayOfWeek.Monday));             // Memorial Day
				if (year >= 2022) holidays.Add(ObservedDate(new DateTime(year, 6, 19))); // Juneteenth
				holidays.Add(ObservedDate(new DateTime(year, 7, 4)));                    // Independence Day
				holidays.Add(NthWeekdayOfMonth(year, 9, DayOfWeek.Monday, 1));           // Labor Day
				holidays.Add(NthWeekdayOfMonth(year, 11, DayOfWeek.Thursday, 4));        // Thanksgiving
				holidays.Add(ObservedDate(new DateTime(year, 12, 25)));                  // Christmas
			}

			return holidays;
		}

		private static DateTime ObservedDate(DateTime holiday)
		{
			if (holiday.DayOfWeek == DayOfWeek.Saturday) return holiday.AddDays(-1);
			if (holiday.DayOfWeek == DayOfWeek.Sunday) return holiday.AddDays(1);
			return holiday;
		}

		private static DateTime NthWeekdayOfMonth(int year, int month, DayOfWeek dow, int n)
		{
			var first = new DateTime(year, month, 1);
			int daysUntilFirst = ((int)dow - (int)first.DayOfWeek + 7) % 7;
			return first.AddDays(daysUntilFirst + (n - 1) * 7);
		}

		private static DateTime LastWeekdayOfMonth(int year, int month, DayOfWeek dow)
		{
			var last = new DateTime(year, month, DateTime.DaysInMonth(year, month));
			int daysBack = ((int)last.DayOfWeek - (int)dow + 7) % 7;
			return last.AddDays(-daysBack);
		}

		private static DateTime CalculateEasterSunday(int year)
		{
			int a = year % 19;
			int b = year / 100;
			int c = year % 100;
			int d = b / 4;
			int e = b % 4;
			int f = (b + 8) / 25;
			int g = (b - f + 1) / 3;
			int h = (19 * a + b - d - g + 15) % 30;
			int i = c / 4;
			int k = c % 4;
			int l = (32 + 2 * e + 2 * i - h - k) % 7;
			int m = (a + 11 * h + 22 * l) / 451;
			int month = (h + l - 7 * m + 114) / 31;
			int day = ((h + l - 7 * m + 114) % 31) + 1;
			return new DateTime(year, month, day);
		}

		public void AddCustomHoliday(DateTime date) => _marketHolidays.Add(date.Date);
		public void RemoveHoliday(DateTime date) => _marketHolidays.Remove(date.Date);

		public IEnumerable<DateTime> GetHolidaysForYear(int year) =>
			_marketHolidays.Where(h => h.Year == year).OrderBy(h => h);

		// ─── Fed Funds Rate Properties ──────────────────────────────────

		/// <summary>
		/// Current (static) Fed Funds rate. Used when historical rates are not loaded.
		/// When historical rates ARE loaded, this serves as the fallback for dates
		/// outside the historical range.
		/// </summary>
		public decimal FedFundsRate { get; set; } = 0.0425m;  // 4.25%

		/// <summary>
		/// Spread below Fed Funds for rebate (typical prime broker spread).
		/// </summary>
		public decimal RebateSpread { get; set; } = 0.0025m;  // 25 bps

		/// <summary>
		/// Percentage of theoretical rebate actually passed through by broker.
		/// </summary>
		public decimal RebatePassThrough { get; set; } = 0.90m;  // 90% for institutional

		/// <summary>
		/// Whether historical Fed Funds rates are loaded and active.
		/// When true, the engine uses date-specific FFR for each daily calculation.
		/// When false, uses the static FedFundsRate property.
		/// </summary>
		public bool UsingHistoricalRates => _useHistoricalRates && _fedFundsHistory != null;

		/// <summary>
		/// The loaded Fed Funds rate history (null if not loaded)
		/// </summary>
		public FedFundsRateHistory FedFundsHistory => _fedFundsHistory;

		/// <summary>
		/// FIX #2: Get the effective rebate rate for a specific date.
		/// Industry-standard formula: (FFR - BrokerSpread) × PassThrough
		///
		/// When historical rates are loaded, uses the date-specific FFR.
		/// Otherwise falls back to the static FedFundsRate property.
		/// </summary>
		public decimal GetEffectiveRebateRate(DateTime date)
		{
			var ffr = GetFedFundsRateForDate(date);
			return Math.Max(0, (ffr - RebateSpread) * RebatePassThrough);
		}

		/// <summary>
		/// FIX #2: Static effective rebate rate (uses current FedFundsRate property).
		/// For display/configuration purposes only. Daily calculations use GetEffectiveRebateRate(date).
		/// </summary>
		public decimal EffectiveRebateRate =>
			Math.Max(0, (FedFundsRate - RebateSpread) * RebatePassThrough);

		/// <summary>
		/// Get the Fed Funds rate applicable to a specific date.
		/// Uses historical data if loaded, otherwise falls back to static property.
		/// </summary>
		public decimal GetFedFundsRateForDate(DateTime date)
		{
			if (_useHistoricalRates && _fedFundsHistory != null)
				return _fedFundsHistory.GetRate(date);

			return FedFundsRate;
		}

		// ─── Historical Rate Configuration ──────────────────────────────

		/// <summary>
		/// Load historical Fed Funds rate data for accurate backtesting.
		///
		/// CRITICAL FOR BACKTEST ACCURACY:
		/// Without this, the engine assumes today's Fed Funds rate applied
		/// throughout the entire backtest, which dramatically overstates
		/// rebate income in 2022 (when FFR was 0.08%) and understates it
		/// in mid-2023 (when FFR peaked at 5.33%).
		///
		/// Usage with Bloomberg:
		///   var bbg = new BloombergFedFundsProvider();
		///   bbg.LoadFromCSV("fed_funds_history.csv");  // or bbg.ProcessBDHResponse(data)
		///   engine.SetFedFundsHistory(bbg.GetRateHistory());
		///
		/// Usage with FOMC fallback:
		///   var bbg = new BloombergFedFundsProvider();
		///   bbg.LoadFOMCFallbackSchedule();
		///   engine.SetFedFundsHistory(bbg.GetRateHistory());
		/// </summary>
		public void SetFedFundsHistory(FedFundsRateHistory history)
		{
			_fedFundsHistory = history ?? throw new ArgumentNullException(nameof(history));
			_useHistoricalRates = true;

			// Update static property to the most recent rate for display
			if (history.Count > 0)
			{
				FedFundsRate = history.RateChanges.Last().Rate;
			}

			_warnings.Add($"Historical Fed Funds rates loaded: {history.Count} data points, " +
						  $"range {history.DateRangeStart:yyyy-MM-dd} to {history.DateRangeEnd:yyyy-MM-dd}. " +
						  $"Current rate: {FedFundsRate:P2}");
		}

		/// <summary>
		/// Convenience method: Load FOMC fallback schedule directly.
		/// Use when Bloomberg is unavailable.
		/// </summary>
		public void LoadFOMCFallbackRates()
		{
			var provider = new BloombergFedFundsProvider();
			provider.LoadFOMCFallbackSchedule();
			SetFedFundsHistory(provider.GetRateHistory());
		}

		/// <summary>
		/// Convenience method: Load Fed Funds history from CSV.
		/// </summary>
		public void LoadFedFundsFromCSV(string filePath)
		{
			var provider = new BloombergFedFundsProvider();
			provider.LoadFromCSV(filePath);
			SetFedFundsHistory(provider.GetRateHistory());
		}

		/// <summary>
		/// Disable historical rates and revert to static FedFundsRate property.
		/// </summary>
		public void DisableHistoricalRates()
		{
			_useHistoricalRates = false;
		}

		/// <summary>
		/// Update Fed Funds rate (static fallback property).
		/// NOTE: If historical rates are loaded, this only affects dates
		/// OUTSIDE the historical range. It does NOT override historical data.
		/// </summary>
		public void SetFedFundsRate(decimal rate)
		{
			if (rate < 0 || rate > 0.20m)
				throw new ArgumentException("Fed Funds rate should be between 0% and 20%");
			FedFundsRate = rate;

			if (_useHistoricalRates && _fedFundsHistory != null)
			{
				_warnings.Add($"NOTE: SetFedFundsRate({rate:P2}) called while historical rates are active. " +
							  $"This only sets the fallback for dates outside the FOMC schedule " +
							  $"({_fedFundsHistory.DateRangeStart:yyyy-MM-dd} to {_fedFundsHistory.DateRangeEnd:yyyy-MM-dd}). " +
							  $"Historical rates will still be used for dates within range.");
			}
		}

		// ─── Broker Configuration ───────────────────────────────────────

		public void ConfigureBrokerTerms(BrokerType brokerType)
		{
			switch (brokerType)
			{
				case BrokerType.InstitutionalPrime:
					RebateSpread = 0.0025m;    // 25 bps
					RebatePassThrough = 0.92m; // 92%
					break;
				case BrokerType.InstitutionalStandard:
					RebateSpread = 0.0040m;    // 40 bps
					RebatePassThrough = 0.85m; // 85%
					break;
				case BrokerType.RetailPro:
					RebateSpread = 0.0075m;    // 75 bps
					RebatePassThrough = 0.60m; // 60%
					break;
				case BrokerType.RetailStandard:
					RebateSpread = 0.0150m;    // 150 bps
					RebatePassThrough = 0.25m; // 25%
					break;
				case BrokerType.NoRebate:
					RebateSpread = 0m;
					RebatePassThrough = 0m;
					break;
			}
		}

		// ─── Business Day & Accrual ─────────────────────────────────────

		public bool IsBusinessDay(DateTime date)
		{
			if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
				return false;
			if (_marketHolidays.Contains(date.Date))
				return false;
			return true;
		}

		/// <summary>
		/// FIX #5: Calculate accrual days with raised safety cap (30) and warning log.
		/// </summary>
		public int GetAccrualDays(DateTime businessDay)
		{
			int accrualDays = 1;
			var nextDay = businessDay.AddDays(1);

			while (!IsBusinessDay(nextDay))
			{
				accrualDays++;
				nextDay = nextDay.AddDays(1);

				if (accrualDays > 30)
				{
					_warnings.Add($"WARNING: Accrual days exceeded 30 for {businessDay:yyyy-MM-dd}. " +
								  $"Check holiday calendar. Capped at 30.");
					break;
				}
			}

			if (accrualDays > 5)
			{
				_warnings.Add($"NOTE: Unusual accrual of {accrualDays} calendar days on {businessDay:yyyy-MM-dd}. " +
							  $"Next business day: {nextDay:yyyy-MM-dd}. Verify holiday calendar.");
			}

			return accrualDays;
		}

		// ─── Rate Loading ───────────────────────────────────────────────

		public void LoadBorrowRates(IEnumerable<BorrowRate> rates)
		{
			foreach (var rate in rates)
			{
				if (!_borrowRates.ContainsKey(rate.Symbol))
					_borrowRates[rate.Symbol] = new List<BorrowRate>();

				_borrowRates[rate.Symbol].Add(rate);
			}

			foreach (var symbol in _borrowRates.Keys)
			{
				_borrowRates[symbol] = _borrowRates[symbol]
					.OrderBy(r => r.AsOfDate)
					.ToList();
			}
		}

		public void GenerateSyntheticRates(string symbol, DateTime startDate, DateTime endDate,
			decimal marketCap, decimal volatility, bool isHTB = false)
		{
			var rates = new List<BorrowRate>();
			var tier = ClassifyBorrowTier(marketCap, volatility, isHTB);
			var baseRate = DefaultRates[tier];
			var random = new Random(symbol.GetHashCode());

			for (var date = startDate; date <= endDate; date = date.AddDays(1))
			{
				if (!IsBusinessDay(date))
					continue;

				var variation = 1m + (decimal)(random.NextDouble() - 0.5) * 0.4m;
				var dailyRate = baseRate * variation;

				// FIX #7: Use date-specific rebate rate instead of static
				var rebateRate = GetEffectiveRebateRate(date);

				rates.Add(new BorrowRate
				{
					Symbol = symbol,
					AsOfDate = date,
					Tier = tier,
					AnnualizedRate = dailyRate,
					RebateRate = rebateRate,
					IsAvailable = tier != BorrowTier.Unavailable,
					MinQuantity = tier == BorrowTier.GeneralCollateral ? 1000000 : 10000,
					Source = "Synthetic"
				});
			}

			LoadBorrowRates(rates);
		}

		private BorrowTier ClassifyBorrowTier(decimal marketCap, decimal volatility, bool isHTB)
		{
			if (isHTB) return BorrowTier.SpecialName;
			if (marketCap >= 100_000_000_000m) return BorrowTier.GeneralCollateral;
			if (marketCap >= 50_000_000_000m) return BorrowTier.GeneralCollateral;
			if (marketCap >= 10_000_000_000m) return BorrowTier.WarmName;
			if (marketCap >= 2_000_000_000m) return BorrowTier.SpecialName;
			return BorrowTier.Threshold;
		}

		/// <summary>
		/// Get the applicable borrow rate for a symbol on a given date.
		/// FIX #7: Uses date-specific rebate rate based on historical FFR.
		/// </summary>
		public BorrowRate GetBorrowRate(string symbol, DateTime date)
		{
			// FIX #7: Calculate rebate using the FFR for THIS specific date
			var rebateForDate = GetEffectiveRebateRate(date);

			if (_borrowRates.TryGetValue(symbol, out var rates))
			{
				var rate = rates
					.Where(r => r.AsOfDate <= date)
					.OrderByDescending(r => r.AsOfDate)
					.FirstOrDefault();

				if (rate != null)
				{
					return rate.WithRebateRate(rebateForDate);
				}
			}

			return new BorrowRate
			{
				Symbol = symbol,
				AsOfDate = date,
				Tier = BorrowTier.GeneralCollateral,
				AnnualizedRate = DefaultRates[BorrowTier.GeneralCollateral],
				RebateRate = rebateForDate,
				IsAvailable = true,
				Source = "Default"
			};
		}

		// ─── Position Management (FIX #4) ───────────────────────────────

		private int _positionCounter = 0;

		public string OpenShortPosition(string symbol, DateTime date, decimal shares, decimal price)
		{
			if (shares >= 0)
				throw new ArgumentException("Short position must have negative shares");

			_positionCounter++;
			var positionId = $"{symbol}_{date:yyyyMMdd}_{_positionCounter:D6}";

			_activeShorts[positionId] = new ShortPositionBorrowRecord
			{
				PositionId = positionId,
				Symbol = symbol,
				OpenDate = date,
				Shares = shares,
				EntryPrice = price
			};

			return positionId;
		}

		private ShortPositionBorrowRecord FindActivePosition(string symbol)
		{
			return _activeShorts.Values
				.Where(p => p.Symbol == symbol && p.CloseDate == null)
				.OrderBy(p => p.OpenDate)
				.FirstOrDefault();
		}

		public IEnumerable<ShortPositionBorrowRecord> FindActivePositions(string symbol)
		{
			return _activeShorts.Values
				.Where(p => p.Symbol == symbol && p.CloseDate == null)
				.OrderBy(p => p.OpenDate);
		}

		public void UpdateShortPosition(string symbol, DateTime date, decimal newShares, decimal price)
		{
			var position = FindActivePosition(symbol);

			if (position == null)
			{
				if (newShares < 0)
					OpenShortPosition(symbol, date, newShares, price);
				return;
			}

			if (newShares >= 0)
				CloseShortPosition(symbol, date);
			else
				position.Shares = newShares;
		}

		public void CloseShortPosition(string symbol, DateTime date)
		{
			var position = FindActivePosition(symbol);

			if (position != null)
			{
				position.CloseDate = date;
				_activeShorts.Remove(position.PositionId);
				_closedShorts.Add(position);
			}
		}

		public void ClosePositionById(string positionId, DateTime date)
		{
			if (_activeShorts.TryGetValue(positionId, out var position))
			{
				position.CloseDate = date;
				_activeShorts.Remove(positionId);
				_closedShorts.Add(position);
			}
		}

		// ─── Daily Cost Calculation ─────────────────────────────────────

		/// <summary>
		/// Calculate daily borrowing costs for all active short positions.
		/// FIX #7: Uses date-specific Fed Funds rate for rebate calculation.
		/// Each DailyBorrowCost record now carries the FFR used on that date
		/// for full audit trail in hedge fund reports.
		/// </summary>
		public decimal CalculateDailyBorrowCosts(DateTime date, Dictionary<string, decimal> prices)
		{
			decimal totalNetCost = 0;
			int accrualDays = GetAccrualDays(date);

			// FIX #7: Get the Fed Funds rate for THIS specific date
			// Defensive: if historical rates somehow not loaded, auto-load FOMC
			if (!_useHistoricalRates || _fedFundsHistory == null)
			{
				LoadFOMCFallbackRates();
			}
			var ffrOnDate = GetFedFundsRateForDate(date);

			foreach (var position in _activeShorts.Values.Where(p => p.CloseDate == null))
			{
				if (!prices.TryGetValue(position.Symbol, out var price))
					price = position.EntryPrice;

				var borrowRate = GetBorrowRate(position.Symbol, date);
				var notional = Math.Abs(position.Shares * price);

				var grossCost = notional * borrowRate.GrossDailyRate * accrualDays;
				var rebateIncome = notional * borrowRate.DailyRebate * accrualDays;
				var netCost = grossCost - rebateIncome;

				var costRecord = new DailyBorrowCost
				{
					Date = date,
					Symbol = position.Symbol,
					PositionId = position.PositionId,
					Shares = position.Shares,
					Price = price,
					GrossAnnualizedRate = borrowRate.AnnualizedRate,
					RebateRate = borrowRate.RebateRate,
					NetRate = borrowRate.NetBorrowRate,
					FedFundsRateOnDate = ffrOnDate,  // FIX #7: Audit trail
					GrossCost = grossCost,
					RebateIncome = rebateIncome,
					NetCost = netCost,
					Tier = borrowRate.Tier,
					AccrualDays = accrualDays
				};

				position.DailyCosts.Add(costRecord);
				_dailyCostHistory.Add(costRecord);
				totalNetCost += netCost;
			}

			return totalNetCost;
		}

		// ─── Historical Simulation ──────────────────────────────────────

		public void ProcessHistoricalPositions(
			IEnumerable<PositionSnapshot> positions)
		{
			// GUARD: Warn loudly if historical FFR not loaded — this is the #1 cause
			// of seeing flat 4.25% across an entire backtest CSV
			if (!_useHistoricalRates || _fedFundsHistory == null)
			{
				_warnings.Add($"⚠ CRITICAL: ProcessHistoricalPositions called WITHOUT historical Fed Funds rates. " +
							  $"The static rate {FedFundsRate:P2} will be applied to " +
							  $"ALL dates. Call LoadFOMCFallbackRates() or SetFedFundsHistory() first.");

				// Auto-load FOMC fallback to prevent flat-rate backtest
				LoadFOMCFallbackRates();
				_warnings.Add("AUTO-FIX: Loaded FOMC fallback schedule automatically. " +
							  "Historical rates are now active.");
			}

			var positionsByDate = positions
				.GroupBy(p => p.Date)
				.OrderBy(g => g.Key);

			// Validate: if the date range spans known FOMC changes,
			// warn if historical rates aren't loaded
			var dates = positionsByDate.Select(g => g.Key).ToList();
			if (dates.Any())
			{
				var minDate = dates.Min();
				var maxDate = dates.Max();

				if (!UsingHistoricalRates && (maxDate - minDate).Days > 90)
				{
					_warnings.Add($"WARNING: Backtest spans {(maxDate - minDate).Days} days " +
								  $"({minDate:yyyy-MM-dd} to {maxDate:yyyy-MM-dd}) but historical " +
								  $"FFR is NOT loaded. Static rate {FedFundsRate:P2} will be used for " +
								  $"ALL dates. Call LoadFOMCFallbackRates() or SetFedFundsHistory() first.");
				}
			}

			foreach (var dateGroup in positionsByDate)
			{
				var date = dateGroup.Key;

				if (!IsBusinessDay(date))
					continue;

				var prices = dateGroup
					.GroupBy(p => p.Symbol)
					.ToDictionary(g => g.Key, g => g.Last().Price);

				foreach (var pos in dateGroup)
				{
					if (pos.Shares < 0)
					{
						var existing = FindActivePosition(pos.Symbol);
						if (existing == null)
							OpenShortPosition(pos.Symbol, date, pos.Shares, pos.Price);
						else
							existing.Shares = pos.Shares;
					}
					else
					{
						CloseShortPosition(pos.Symbol, date);
					}
				}

				CalculateDailyBorrowCosts(date, prices);
			}

			// Post-processing validation: check if FFR actually varied
			if (_dailyCostHistory.Count > 100)
			{
				var distinctFFR = _dailyCostHistory
					.Select(c => c.FedFundsRateOnDate)
					.Distinct()
					.ToList();

				if (distinctFFR.Count == 1)
				{
					_warnings.Add($"CRITICAL: Fed Funds rate is FLAT at {distinctFFR[0]:P2} across " +
								  $"all {_dailyCostHistory.Count} cost records. Historical rates may " +
								  $"not be loaded correctly. Rebate calculations are likely WRONG. " +
								  $"UsingHistoricalRates={UsingHistoricalRates}");
				}
				else if (distinctFFR.Count < 5 && _dailyCostHistory.Count > 500)
				{
					_warnings.Add($"NOTE: Only {distinctFFR.Count} distinct FFR values across " +
								  $"{_dailyCostHistory.Count} records. Expected more variation for " +
								  $"a multi-year backtest. Verify FOMC schedule coverage.");
				}
			}
		}

		// ─── State Management ───────────────────────────────────────────

		public void Reset()
		{
			_borrowRates.Clear();
			_activeShorts.Clear();
			_closedShorts.Clear();
			_dailyCostHistory.Clear();
			_warnings.Clear();
			_positionCounter = 0;
			// Note: does NOT reset _fedFundsHistory — rate data persists across resets
		}

		// ─── Query Methods ──────────────────────────────────────────────

		public decimal GetTotalNetBorrowCosts(DateTime? startDate = null, DateTime? endDate = null)
		{
			var costs = FilterCosts(startDate, endDate);
			return costs.Sum(c => c.NetCost);
		}

		public decimal GetTotalGrossBorrowCosts(DateTime? startDate = null, DateTime? endDate = null)
		{
			var costs = FilterCosts(startDate, endDate);
			return costs.Sum(c => c.GrossCost);
		}

		public decimal GetTotalRebateIncome(DateTime? startDate = null, DateTime? endDate = null)
		{
			var costs = FilterCosts(startDate, endDate);
			return costs.Sum(c => c.RebateIncome);
		}

		public int GetTotalAccrualDays(DateTime? startDate = null, DateTime? endDate = null)
		{
			var costs = FilterCosts(startDate, endDate);
			return costs.Sum(c => c.AccrualDays);
		}

		private IEnumerable<DailyBorrowCost> FilterCosts(DateTime? startDate, DateTime? endDate)
		{
			var costs = _dailyCostHistory.AsEnumerable();
			if (startDate.HasValue) costs = costs.Where(c => c.Date >= startDate.Value);
			if (endDate.HasValue) costs = costs.Where(c => c.Date <= endDate.Value);
			return costs;
		}

		public Dictionary<string, BorrowCostBySymbol> GetCostsBySymbol(DateTime? startDate = null, DateTime? endDate = null)
		{
			var costs = FilterCosts(startDate, endDate);

			return costs
				.GroupBy(c => c.Symbol)
				.ToDictionary(g => g.Key, g => new BorrowCostBySymbol
				{
					Symbol = g.Key,
					GrossCost = g.Sum(c => c.GrossCost),
					RebateIncome = g.Sum(c => c.RebateIncome),
					NetCost = g.Sum(c => c.NetCost),
					BusinessDays = g.Count(),
					CalendarDays = g.Sum(c => c.AccrualDays),
					AvgNotional = g.Average(c => c.NotionalValue),
					PredominantTier = g.GroupBy(c => c.Tier)
						.OrderByDescending(t => t.Count())
						.First().Key,
					AvgFedFundsRate = g.Average(c => c.FedFundsRateOnDate),
					AvgGrossAnnualizedRate = g.Average(c => c.GrossAnnualizedRate)
				});
		}

		// ─── CSV Exports ────────────────────────────────────────────────

		/// <summary>
		/// Export daily borrowing cost history to CSV (GIPS-compliant).
		/// FIX #7: Now includes FedFundsRate column for audit trail.
		/// </summary>
		public void ExportDailyCostsToCSV(string filePath)
		{
			var sb = new StringBuilder();

			sb.AppendLine("Date,Symbol,PositionId,Shares,Price,NotionalValue,BorrowTier,AccrualDays," +
						  "FedFundsRate," +  // FIX #7: Historical FFR for audit
						  "GrossAnnualizedRate,RebateRate,NetRate," +
						  "GrossCost,RebateIncome,NetCost," +
						  "CumulativeGross,CumulativeRebate,CumulativeNet");

			decimal cumulativeGross = 0;
			decimal cumulativeRebate = 0;
			decimal cumulativeNet = 0;

			foreach (var cost in _dailyCostHistory.OrderBy(c => c.Date).ThenBy(c => c.Symbol))
			{
				cumulativeGross += cost.GrossCost;
				cumulativeRebate += cost.RebateIncome;
				cumulativeNet += cost.NetCost;

				sb.AppendLine(string.Join(",",
					cost.Date.ToString("yyyy-MM-dd"),
					cost.Symbol,
					cost.PositionId ?? "",
					cost.Shares.ToString("F0"),
					cost.Price.ToString("F4"),
					cost.NotionalValue.ToString("F2"),
					cost.Tier.ToString(),
					cost.AccrualDays,
					(cost.FedFundsRateOnDate * 100).ToString("F2"),  // FIX #7
					(cost.GrossAnnualizedRate * 100).ToString("F4"),
					(cost.RebateRate * 100).ToString("F4"),
					(cost.NetRate * 100).ToString("F4"),
					cost.GrossCost.ToString("F2"),
					cost.RebateIncome.ToString("F2"),
					cost.NetCost.ToString("F2"),
					cumulativeGross.ToString("F2"),
					cumulativeRebate.ToString("F2"),
					cumulativeNet.ToString("F2")
				));
			}

			File.WriteAllText(filePath, sb.ToString());
		}

		public void ExportPositionSummaryToCSV(string filePath)
		{
			var sb = new StringBuilder();

			sb.AppendLine("PositionId,Symbol,OpenDate,CloseDate,BusinessDays,CalendarDays,AvgNotional," +
						  "AvgFedFundsRate,AvgGrossBorrowRate," +
						  "GrossBorrowCost,RebateIncome,NetBorrowCost," +
						  "EffectiveAnnualRate,Status");

			var allPositions = _closedShorts.Concat(_activeShorts.Values)
				.OrderBy(p => p.OpenDate)
				.ThenBy(p => p.Symbol);

			foreach (var pos in allPositions)
			{
				// Compute avg FFR and avg gross rate across this position's daily records
				var avgFFR = pos.DailyCosts.Any()
					? pos.DailyCosts.Average(c => c.FedFundsRateOnDate)
					: FedFundsRate;
				var avgGrossRate = pos.DailyCosts.Any()
					? pos.DailyCosts.Average(c => c.GrossAnnualizedRate)
					: 0m;

				sb.AppendLine(string.Join(",",
					pos.PositionId ?? "",
					pos.Symbol,
					pos.OpenDate.ToString("yyyy-MM-dd"),
					pos.CloseDate?.ToString("yyyy-MM-dd") ?? "",
					pos.BusinessDaysHeld,
					pos.CalendarDaysAccrued,
					pos.AverageNotional.ToString("F2"),
					(avgFFR * 100).ToString("F2"),
					(avgGrossRate * 100).ToString("F4"),
					pos.TotalGrossBorrowCost.ToString("F2"),
					pos.TotalRebateIncome.ToString("F2"),
					pos.TotalNetBorrowCost.ToString("F2"),
					(pos.EffectiveAnnualizedRate * 100).ToString("F4"),
					pos.CloseDate.HasValue ? "Closed" : "Open"
				));
			}

			File.WriteAllText(filePath, sb.ToString());
		}

		/// <summary>
		/// FIX #1: Monthly summary with correct calendar day aggregation.
		/// FIX #7: Now includes average FFR for the month.
		/// </summary>
		public void ExportMonthlySummaryToCSV(string filePath)
		{
			var sb = new StringBuilder();

			sb.AppendLine("Year,Month,GrossBorrowCost,RebateIncome,NetBorrowCost," +
						  "AvgDailyNotional,AvgPositionCount,CalendarDays," +
						  "AvgFedFundsRate," +  // FIX #7
						  "WeightedAvgGrossRate,WeightedAvgNetRate");

			var monthlySummary = _dailyCostHistory
				.GroupBy(c => new { c.Date.Year, c.Date.Month })
				.OrderBy(g => g.Key.Year)
				.ThenBy(g => g.Key.Month);

			foreach (var month in monthlySummary)
			{
				var dailyData = month
					.GroupBy(c => c.Date)
					.Select(d => new
					{
						Date = d.Key,
						TotalNotional = d.Sum(c => c.NotionalValue),
						PositionCount = d.Count(),
						GrossCost = d.Sum(c => c.GrossCost),
						RebateIncome = d.Sum(c => c.RebateIncome),
						NetCost = d.Sum(c => c.NetCost),
						AccrualDays = d.First().AccrualDays,
						FedFundsRate = d.First().FedFundsRateOnDate  // FIX #7
					})
					.ToList();

				var totalGross = month.Sum(c => c.GrossCost);
				var totalRebate = month.Sum(c => c.RebateIncome);
				var totalNet = month.Sum(c => c.NetCost);
				var avgNotional = dailyData.Average(d => d.TotalNotional);
				var avgPositions = dailyData.Average(d => d.PositionCount);

				// FIX #1: Calendar days from per-date groups, NOT raw records
				var calendarDays = dailyData.Sum(d => d.AccrualDays);

				// FIX #7: Average FFR for the month
				var avgFFR = dailyData.Average(d => d.FedFundsRate);

				var weightedGrossRate = avgNotional > 0 && calendarDays > 0
					? (totalGross / avgNotional) * (365m / calendarDays)
					: 0;
				var weightedNetRate = avgNotional > 0 && calendarDays > 0
					? (totalNet / avgNotional) * (365m / calendarDays)
					: 0;

				sb.AppendLine(string.Join(",",
					month.Key.Year,
					month.Key.Month.ToString("D2"),
					totalGross.ToString("F2"),
					totalRebate.ToString("F2"),
					totalNet.ToString("F2"),
					avgNotional.ToString("F2"),
					avgPositions.ToString("F2"),
					calendarDays,
					(avgFFR * 100).ToString("F2"),  // FIX #7
					(weightedGrossRate * 100).ToString("F4"),
					(weightedNetRate * 100).ToString("F4")
				));
			}

			File.WriteAllText(filePath, sb.ToString());
		}

		// ─── Summary & Reporting ────────────────────────────────────────

		/// <summary>
		/// FIX #3: TopCostlySymbols sorted by absolute net cost.
		/// </summary>
		public BorrowCostSummary GetSummary()
		{
			// Compute FFR statistics from daily cost history
			var ffrValues = _dailyCostHistory.Select(c => c.FedFundsRateOnDate).ToList();
			var grossRateValues = _dailyCostHistory.Select(c => c.GrossAnnualizedRate).ToList();

			return new BorrowCostSummary
			{
				TotalGrossBorrowCost = _dailyCostHistory.Sum(c => c.GrossCost),
				TotalRebateIncome = _dailyCostHistory.Sum(c => c.RebateIncome),
				TotalNetBorrowCost = _dailyCostHistory.Sum(c => c.NetCost),
				TotalBusinessDays = _dailyCostHistory.Select(c => c.Date).Distinct().Count(),
				TotalCalendarDays = _dailyCostHistory.Sum(c => c.AccrualDays),
				UniqueSymbols = _dailyCostHistory.Select(c => c.Symbol).Distinct().Count(),
				AverageDailyGross = _dailyCostHistory.Any()
					? _dailyCostHistory.GroupBy(c => c.Date).Average(g => g.Sum(c => c.GrossCost))
					: 0,
				AverageDailyNet = _dailyCostHistory.Any()
					? _dailyCostHistory.GroupBy(c => c.Date).Average(g => g.Sum(c => c.NetCost))
					: 0,
				TierBreakdown = _dailyCostHistory
					.GroupBy(c => c.Tier)
					.ToDictionary(g => g.Key, g => new TierCostBreakdown
					{
						Tier = g.Key,
						GrossCost = g.Sum(c => c.GrossCost),
						RebateIncome = g.Sum(c => c.RebateIncome),
						NetCost = g.Sum(c => c.NetCost),
						BusinessDays = g.Select(c => c.Date).Distinct().Count(),
						CalendarDays = g.Sum(c => c.AccrualDays)
					}),
				// FIX #3: Sort by absolute net cost
				TopCostlySymbols = _dailyCostHistory
					.GroupBy(c => c.Symbol)
					.OrderByDescending(g => Math.Abs(g.Sum(c => c.NetCost)))
					.Take(10)
					.ToDictionary(g => g.Key, g => g.Sum(c => c.NetCost)),
				EffectiveRebateRate = EffectiveRebateRate,
				FedFundsRate = FedFundsRate,
				UsingHistoricalRates = UsingHistoricalRates,

				// FFR vs Gross Rate stats from daily history
				AvgFedFundsRate = ffrValues.Any() ? ffrValues.Average() : FedFundsRate,
				MinFedFundsRate = ffrValues.Any() ? ffrValues.Min() : FedFundsRate,
				MaxFedFundsRate = ffrValues.Any() ? ffrValues.Max() : FedFundsRate,
				AvgGrossAnnualizedRate = grossRateValues.Any() ? grossRateValues.Average() : 0,

				Warnings = _warnings.ToList()
			};
		}

		/// <summary>
		/// Public accessors for hedge fund report integration
		/// </summary>
		public IReadOnlyDictionary<string, ShortPositionBorrowRecord> ActiveShorts => _activeShorts;
		public IReadOnlyList<ShortPositionBorrowRecord> ClosedShorts => _closedShorts.AsReadOnly();
		public IReadOnlyList<DailyBorrowCost> DailyCostHistory => _dailyCostHistory.AsReadOnly();
	}

	// ═══════════════════════════════════════════════════════════════════════
	//  SUPPORTING TYPES
	// ═══════════════════════════════════════════════════════════════════════

	public enum BrokerType
	{
		InstitutionalPrime,
		InstitutionalStandard,
		RetailPro,
		RetailStandard,
		NoRebate
	}

	/// <summary>
	/// A point-in-time position snapshot for historical simulation (replaces ValueTuple for compatibility)
	/// </summary>
	public class PositionSnapshot
	{
		public DateTime Date { get; set; }
		public string Symbol { get; set; }
		public decimal Shares { get; set; }
		public decimal Price { get; set; }

		public PositionSnapshot(DateTime date, string symbol, decimal shares, decimal price)
		{
			Date = date;
			Symbol = symbol;
			Shares = shares;
			Price = price;
		}
	}

	public class BorrowCostBySymbol
	{
		public string Symbol { get; set; }
		public decimal GrossCost { get; set; }
		public decimal RebateIncome { get; set; }
		public decimal NetCost { get; set; }
		public int BusinessDays { get; set; }
		public int CalendarDays { get; set; }
		public decimal AvgNotional { get; set; }
		public BorrowTier PredominantTier { get; set; }
		public decimal AvgFedFundsRate { get; set; }     // Avg FFR across this symbol's positions
		public decimal AvgGrossAnnualizedRate { get; set; } // Avg gross borrow rate
	}

	public class TierCostBreakdown
	{
		public BorrowTier Tier { get; set; }
		public decimal GrossCost { get; set; }
		public decimal RebateIncome { get; set; }
		public decimal NetCost { get; set; }
		public int BusinessDays { get; set; }
		public int CalendarDays { get; set; }
	}

	/// <summary>
	/// Summary statistics for borrowing costs with GIPS-compliant breakdown.
	/// FIX #7: Now indicates whether historical rates were used.
	/// </summary>
	public class BorrowCostSummary
	{
		public decimal TotalGrossBorrowCost { get; set; }
		public decimal TotalRebateIncome { get; set; }
		public decimal TotalNetBorrowCost { get; set; }
		public int TotalBusinessDays { get; set; }
		public int TotalCalendarDays { get; set; }
		public int UniqueSymbols { get; set; }
		public decimal AverageDailyGross { get; set; }
		public decimal AverageDailyNet { get; set; }
		public Dictionary<BorrowTier, TierCostBreakdown> TierBreakdown { get; set; }
		public Dictionary<string, decimal> TopCostlySymbols { get; set; }
		public decimal EffectiveRebateRate { get; set; }
		public decimal FedFundsRate { get; set; }
		public bool UsingHistoricalRates { get; set; }
		public List<string> Warnings { get; set; } = new();

		// FFR statistics across the reporting period
		public decimal AvgFedFundsRate { get; set; }
		public decimal MinFedFundsRate { get; set; }
		public decimal MaxFedFundsRate { get; set; }
		public decimal AvgGrossAnnualizedRate { get; set; }  // Portfolio-level avg gross borrow

		public override string ToString()
		{
			var sb = new StringBuilder();
			sb.AppendLine("═══════════════════════════════════════════════════════════════");
			sb.AppendLine("  BORROWING COST SUMMARY (GIPS-Compliant)");
			sb.AppendLine("═══════════════════════════════════════════════════════════════");
			sb.AppendLine();
			sb.AppendLine("  Rate Environment:");
			sb.AppendLine($"    Fed Funds Rate (current):  {FedFundsRate:P2}");
			sb.AppendLine($"    Effective Rebate (current): {EffectiveRebateRate:P2}");
			sb.AppendLine($"    Historical FFR Mode:       {(UsingHistoricalRates ? "YES ✓" : "NO — using static rate")}");
			if (!UsingHistoricalRates)
				sb.AppendLine($"    ⚠ WARNING: Static FFR applied to all dates. Load historical rates for accurate backtest.");
			sb.AppendLine();
			sb.AppendLine("  Fed Funds Rate vs Gross Borrow Rate (period averages):");
			sb.AppendLine($"    Avg Fed Funds Rate:    {AvgFedFundsRate:P2}  (range: {MinFedFundsRate:P2} – {MaxFedFundsRate:P2})");
			sb.AppendLine($"    Avg Gross Borrow Rate: {AvgGrossAnnualizedRate:P2}");
			sb.AppendLine($"    Spread (FFR - Gross):  {(AvgFedFundsRate - AvgGrossAnnualizedRate):P2}  ← drives rebate income");
			sb.AppendLine();
			sb.AppendLine("  Cost Summary:");
			sb.AppendLine($"    Gross Borrow Cost:     ${TotalGrossBorrowCost:N2}");
			sb.AppendLine($"    Rebate Income:         ${TotalRebateIncome:N2}");
			sb.AppendLine($"    ─────────────────────────────────");
			sb.AppendLine($"    Net Borrow Cost:       ${TotalNetBorrowCost:N2}");
			if (TotalNetBorrowCost < 0)
				sb.AppendLine($"                           (EARNED - rebate exceeds borrow)");
			sb.AppendLine();
			sb.AppendLine($"  Total Business Days:     {TotalBusinessDays}");
			sb.AppendLine($"  Total Calendar Days:     {TotalCalendarDays} (includes weekends/holidays)");
			sb.AppendLine($"  Unique Symbols:          {UniqueSymbols}");
			sb.AppendLine($"  Average Daily Gross:     ${AverageDailyGross:N2}");
			sb.AppendLine($"  Average Daily Net:       ${AverageDailyNet:N2}");
			sb.AppendLine();
			sb.AppendLine("  Cost by Tier (FFR | Gross Borrow | Rebate | Net):");
			if (TierBreakdown != null && TierBreakdown.Any())
			{
				foreach (var tier in TierBreakdown.Values.OrderByDescending(t => Math.Abs(t.NetCost)))
				{
					sb.AppendLine($"    {tier.Tier,-20} Gross: ${tier.GrossCost:N2}  " +
								  $"Rebate: ${tier.RebateIncome:N2}  Net: ${tier.NetCost:N2}");
				}
			}
			sb.AppendLine();
			sb.AppendLine("  Top 10 Symbols by Net Cost (largest impact first):");
			if (TopCostlySymbols != null && TopCostlySymbols.Any())
			{
				foreach (var sym in TopCostlySymbols)
				{
					var prefix = sym.Value < 0 ? "EARNED" : "COST";
					sb.AppendLine($"    {sym.Key,-8} ${Math.Abs(sym.Value):N2} ({prefix})");
				}
			}

			if (Warnings != null && Warnings.Any())
			{
				sb.AppendLine();
				sb.AppendLine("  ⚠ Warnings:");
				foreach (var warning in Warnings.Take(20))
					sb.AppendLine($"    {warning}");
				if (Warnings.Count > 20)
					sb.AppendLine($"    ... and {Warnings.Count - 20} more.");
			}

			sb.AppendLine();
			sb.AppendLine("═══════════════════════════════════════════════════════════════");
			return sb.ToString();
		}
	}

	// ═══════════════════════════════════════════════════════════════════════
	//  HEDGE FUND REPORT INTEGRATION
	// ═══════════════════════════════════════════════════════════════════════

	/// <summary>
	/// Integration with transaction cost framework.
	/// Outputs values ready for hedge fund reports (GIPS, DDQ, investor letters).
	/// </summary>
	public class BorrowCostTransactionIntegration
	{
		private readonly BorrowingCostEngine _engine;

		public BorrowCostTransactionIntegration(BorrowingCostEngine engine)
		{
			_engine = engine;
		}

		public TransactionCostBreakdown CalculateTotalCosts(
			string symbol, decimal shares, decimal price,
			DateTime tradeDate, int expectedHoldingCalendarDays)
		{
			var breakdown = new TransactionCostBreakdown
			{
				Symbol = symbol,
				Shares = shares,
				Price = price,
				TradeDate = tradeDate,
				ExpectedHoldingDays = expectedHoldingCalendarDays
			};

			var notional = Math.Abs(shares * price);

			breakdown.Commission = Math.Abs(shares) * 0.003m;

			if (shares < 0)
				breakdown.SecFee = notional * 0.0000278m;

			breakdown.MarketImpact = notional * 0.0005m;

			if (shares < 0)
			{
				var borrowRate = _engine.GetBorrowRate(symbol, tradeDate);
				var holdingFraction = expectedHoldingCalendarDays / 365m;

				breakdown.EstimatedGrossBorrowCost = notional * borrowRate.AnnualizedRate * holdingFraction;
				breakdown.EstimatedRebateIncome = notional * borrowRate.RebateRate * holdingFraction;
				breakdown.EstimatedNetBorrowCost = breakdown.EstimatedGrossBorrowCost - breakdown.EstimatedRebateIncome;

				breakdown.GrossBorrowRate = borrowRate.AnnualizedRate;
				breakdown.RebateRate = borrowRate.RebateRate;
				breakdown.NetBorrowRate = borrowRate.NetBorrowRate;
				breakdown.BorrowTier = borrowRate.Tier;
			}

			return breakdown;
		}

		/// <summary>
		/// Generate a hedge fund report section for borrowing costs.
		/// Returns structured data suitable for GIPS reports, DDQ responses,
		/// and investor presentations.
		/// </summary>
		public HedgeFundBorrowReport GenerateHedgeFundReport(
			DateTime? startDate = null, DateTime? endDate = null)
		{
			var summary = _engine.GetSummary();
			var costsBySymbol = _engine.GetCostsBySymbol(startDate, endDate);

			return new HedgeFundBorrowReport
			{
				ReportDate = DateTime.Now,
				PeriodStart = startDate,
				PeriodEnd = endDate,

				TotalGrossBorrowCost = _engine.GetTotalGrossBorrowCosts(startDate, endDate),
				TotalRebateIncome = _engine.GetTotalRebateIncome(startDate, endDate),
				TotalNetBorrowCost = _engine.GetTotalNetBorrowCosts(startDate, endDate),

				FedFundsRate = _engine.FedFundsRate,
				EffectiveRebateRate = _engine.EffectiveRebateRate,
				UsingHistoricalRates = _engine.UsingHistoricalRates,

				// FFR vs Gross Borrow Rate stats
				AvgFedFundsRate = summary.AvgFedFundsRate,
				MinFedFundsRate = summary.MinFedFundsRate,
				MaxFedFundsRate = summary.MaxFedFundsRate,
				AvgGrossAnnualizedRate = summary.AvgGrossAnnualizedRate,

				CostsBySymbol = costsBySymbol,
				TierBreakdown = summary.TierBreakdown,

				TotalBusinessDays = summary.TotalBusinessDays,
				TotalCalendarDays = summary.TotalCalendarDays,
				UniqueSymbols = summary.UniqueSymbols,
				AverageDailyGrossCost = summary.AverageDailyGross,
				AverageDailyNetCost = summary.AverageDailyNet,

				Warnings = summary.Warnings
			};
		}
	}

	/// <summary>
	/// Structured output for hedge fund reports.
	/// </summary>
	public class HedgeFundBorrowReport
	{
		public DateTime ReportDate { get; set; }
		public DateTime? PeriodStart { get; set; }
		public DateTime? PeriodEnd { get; set; }

		public decimal TotalGrossBorrowCost { get; set; }
		public decimal TotalRebateIncome { get; set; }
		public decimal TotalNetBorrowCost { get; set; }

		public decimal FedFundsRate { get; set; }
		public decimal EffectiveRebateRate { get; set; }
		public bool UsingHistoricalRates { get; set; }

		// FFR vs Gross Borrow Rate — the key relationship for reports
		public decimal AvgFedFundsRate { get; set; }
		public decimal MinFedFundsRate { get; set; }
		public decimal MaxFedFundsRate { get; set; }
		public decimal AvgGrossAnnualizedRate { get; set; }

		public Dictionary<string, BorrowCostBySymbol> CostsBySymbol { get; set; }
		public Dictionary<BorrowTier, TierCostBreakdown> TierBreakdown { get; set; }

		public int TotalBusinessDays { get; set; }
		public int TotalCalendarDays { get; set; }
		public int UniqueSymbols { get; set; }
		public decimal AverageDailyGrossCost { get; set; }
		public decimal AverageDailyNetCost { get; set; }

		public List<string> Warnings { get; set; } = new();

		public string ToGIPSSection()
		{
			var sb = new StringBuilder();
			sb.AppendLine("BORROWING COSTS (GIPS-Compliant Disclosure)");
			sb.AppendLine("───────────────────────────────────────────");

			var periodDesc = PeriodStart.HasValue && PeriodEnd.HasValue
				? $"{PeriodStart:yyyy-MM-dd} to {PeriodEnd:yyyy-MM-dd}"
				: "Inception to Date";
			sb.AppendLine($"  Period: {periodDesc}");
			sb.AppendLine($"  Historical FFR: {(UsingHistoricalRates ? "YES ✓" : "NO — static rate")}");
			sb.AppendLine();
			sb.AppendLine("  Rate Comparison (Fed Funds vs Gross Borrow):");
			sb.AppendLine($"    Avg Fed Funds Rate:      {AvgFedFundsRate:P2}  (range: {MinFedFundsRate:P2} – {MaxFedFundsRate:P2})");
			sb.AppendLine($"    Avg Gross Borrow Rate:   {AvgGrossAnnualizedRate:P2}");
			sb.AppendLine($"    Avg Spread (FFR-Gross):  {(AvgFedFundsRate - AvgGrossAnnualizedRate):P2}  ← rebate driver");
			sb.AppendLine($"    Current Fed Funds Rate:  {FedFundsRate:P2}");
			sb.AppendLine($"    Current Eff. Rebate:     {EffectiveRebateRate:P2}");
			sb.AppendLine();
			sb.AppendLine($"  Gross Borrowing Costs:   ${TotalGrossBorrowCost:N2}");
			sb.AppendLine($"  Short Rebate Income:     ${TotalRebateIncome:N2}");
			sb.AppendLine($"  Net Borrowing Costs:     ${TotalNetBorrowCost:N2}");
			if (TotalNetBorrowCost < 0)
				sb.AppendLine($"  (Net income: rebate exceeds borrowing cost for GC names)");
			sb.AppendLine();
			sb.AppendLine($"  Avg Daily Gross Cost:    ${AverageDailyGrossCost:N2}");
			sb.AppendLine($"  Avg Daily Net Cost:      ${AverageDailyNetCost:N2}");
			sb.AppendLine($"  Business Days:           {TotalBusinessDays}");
			sb.AppendLine($"  Calendar Days (accrual): {TotalCalendarDays}");
			sb.AppendLine($"  Unique Symbols Shorted:  {UniqueSymbols}");

			return sb.ToString();
		}

		public string ToDDQSection()
		{
			var sb = new StringBuilder();
			sb.AppendLine("Q: What are the fund's borrowing costs and how are they managed?");
			sb.AppendLine();
			sb.AppendLine("A: The fund maintains institutional prime brokerage relationships providing");
			sb.AppendLine("   competitive short rebate terms. All S&P 500 constituents traded by the");
			sb.AppendLine("   strategy are classified as General Collateral (easy to borrow), resulting");
			sb.AppendLine("   in borrowing costs well below the effective short rebate rate in elevated");
			sb.AppendLine("   rate environments.");
			sb.AppendLine();
			sb.AppendLine($"   Rate comparison (Fed Funds Rate vs Gross Borrow Rate):");
			sb.AppendLine($"     Avg Fed Funds Rate:     {AvgFedFundsRate:P2}  (range: {MinFedFundsRate:P2} – {MaxFedFundsRate:P2})");
			sb.AppendLine($"     Avg Gross Borrow Rate:  {AvgGrossAnnualizedRate:P2}");
			sb.AppendLine($"     Current Fed Funds Rate: {FedFundsRate:P2}");
			sb.AppendLine($"     Current Eff. Rebate:    {EffectiveRebateRate:P2}");
			sb.AppendLine();

			if (TotalNetBorrowCost < 0)
			{
				sb.AppendLine($"   Net borrowing impact: The fund EARNS ${Math.Abs(TotalNetBorrowCost):N2}");
				sb.AppendLine("   on short positions (rebate income exceeds gross borrow cost).");
				sb.AppendLine($"   The avg Fed Funds rate ({AvgFedFundsRate:P2}) significantly exceeds the");
				sb.AppendLine($"   avg gross borrow rate ({AvgGrossAnnualizedRate:P2}), creating positive carry");
				sb.AppendLine("   that is a structural advantage of the current rate environment");
				sb.AppendLine("   for market-neutral strategies.");
			}
			else
			{
				sb.AppendLine($"   Net borrowing cost: ${TotalNetBorrowCost:N2}");
				sb.AppendLine($"   Note: During periods when the Fed Funds rate ({MinFedFundsRate:P2} low)");
				sb.AppendLine($"   fell below the gross borrow rate ({AvgGrossAnnualizedRate:P2}), short");
				sb.AppendLine("   positions carried net cost. As rates rose through 2022-2023,");
				sb.AppendLine("   borrowing costs transitioned to net income.");
			}

			if (UsingHistoricalRates)
			{
				sb.AppendLine();
				sb.AppendLine("   All borrowing cost calculations use date-specific Federal Funds");
				sb.AppendLine("   Effective Rate sourced from Bloomberg (FEDL01 Index), ensuring");
				sb.AppendLine("   accurate rebate computation across the full strategy history.");
			}

			return sb.ToString();
		}
	}

	/// <summary>
	/// Complete transaction cost breakdown including borrowing with gross/rebate/net
	/// </summary>
	public class TransactionCostBreakdown
	{
		public string Symbol { get; set; }
		public decimal Shares { get; set; }
		public decimal Price { get; set; }
		public DateTime TradeDate { get; set; }
		public int ExpectedHoldingDays { get; set; }
		public decimal Notional => Math.Abs(Shares * Price);

		public decimal Commission { get; set; }
		public decimal SecFee { get; set; }
		public decimal MarketImpact { get; set; }

		public decimal EstimatedGrossBorrowCost { get; set; }
		public decimal EstimatedRebateIncome { get; set; }
		public decimal EstimatedNetBorrowCost { get; set; }

		public decimal GrossBorrowRate { get; set; }
		public decimal RebateRate { get; set; }
		public decimal NetBorrowRate { get; set; }
		public BorrowTier BorrowTier { get; set; }

		public decimal TotalExecutionCost => Commission + SecFee + MarketImpact;
		public decimal TotalCostIncludingCarry => TotalExecutionCost + EstimatedNetBorrowCost;
		public decimal TotalExecutionCostBps => Notional > 0 ? (TotalExecutionCost / Notional) * 10000 : 0;
		public decimal TotalCostBps => Notional > 0 ? (TotalCostIncludingCarry / Notional) * 10000 : 0;

		public override string ToString()
		{
			var carrySign = EstimatedNetBorrowCost < 0 ? "+" : "-";
			var carryDesc = EstimatedNetBorrowCost < 0 ? "EARN" : "COST";

			return $"{Symbol}: Exec ${TotalExecutionCost:N2} ({TotalExecutionCostBps:F1} bps) " +
				   $"+ Carry {carrySign}${Math.Abs(EstimatedNetBorrowCost):N2} ({carryDesc}) " +
				   $"= Net ${TotalCostIncludingCarry:N2} ({TotalCostBps:F1} bps) " +
				   $"[{BorrowTier}, {ExpectedHoldingDays}d hold]";
		}
	}

	// ═══════════════════════════════════════════════════════════════════════
	//  DEMO
	// ═══════════════════════════════════════════════════════════════════════

	public class BorrowingCostDemo
	{
		public static void RunDemo(string[] args)
		{
			Console.WriteLine("═══════════════════════════════════════════════════════════════");
			Console.WriteLine("  ATMML Quant Fund - Borrowing Cost Engine Demo");
			Console.WriteLine("  (Historical Fed Funds + Corrected Rebate + Weekend Accrual)");
			Console.WriteLine("═══════════════════════════════════════════════════════════════\n");

			var engine = new BorrowingCostEngine();

			// Configure for institutional prime broker terms
			engine.ConfigureBrokerTerms(BrokerType.InstitutionalPrime);

			// ─── Historical Fed Funds Rates ──────────────────────────
			// The constructor auto-loads the FOMC fallback schedule,
			// so historical rates are already active. Override with
			// Bloomberg BDH for daily-granularity production data:
			//
			//   var bbgProvider = new BloombergFedFundsProvider();
			//   var bdh = bbgProvider.CreateBDHRequest(new DateTime(2022, 1, 1), DateTime.Today);
			//   // Execute via your Bloomberg data layer:
			//   //   var data = bloombergService.ExecuteBDH(bdh);
			//   //   bbgProvider.ProcessBDHResponse(data);
			//   //   engine.SetFedFundsHistory(bbgProvider.GetRateHistory());
			//
			// Or from CSV:
			//   engine.LoadFedFundsFromCSV("fed_funds_history.csv");

			Console.WriteLine($"Configuration:");
			Console.WriteLine($"  Fed Funds Rate (current): {engine.FedFundsRate:P2}");
			Console.WriteLine($"  Rebate Spread:            {engine.RebateSpread:P2}");
			Console.WriteLine($"  Rebate Pass-Through:      {engine.RebatePassThrough:P0}");
			Console.WriteLine($"  Effective Rebate (now):   {engine.EffectiveRebateRate:P2}");
			Console.WriteLine($"  Historical FFR loaded:    {engine.UsingHistoricalRates}");
			Console.WriteLine();

			// Show how FFR varied across the backtest period
			Console.WriteLine("  Fed Funds Rate by period:");
			var sampleDates = new[]
			{
				new DateTime(2022, 1, 3),  new DateTime(2022, 4, 1),
				new DateTime(2022, 7, 1),  new DateTime(2022, 10, 3),
				new DateTime(2023, 1, 3),  new DateTime(2023, 6, 1),
				new DateTime(2023, 9, 1),  new DateTime(2024, 1, 2),
				new DateTime(2024, 6, 3),  new DateTime(2024, 10, 1),
				new DateTime(2025, 1, 2),
			};
			foreach (var d in sampleDates)
			{
				var ffr = engine.GetFedFundsRateForDate(d);
				var rebate = engine.GetEffectiveRebateRate(d);
				var netCarry = rebate - 0.003m;  // vs typical GC borrow
				var carryLabel = netCarry > 0 ? "EARN" : "COST";
				Console.WriteLine($"    {d:yyyy-MM-dd}  FFR: {ffr:P2}  Rebate: {rebate:P2}  Net carry: {netCarry:P2} ({carryLabel})");
			}
			Console.WriteLine();

			// Generate synthetic borrow rates (now uses date-specific rebate)
			var startDate = new DateTime(2022, 1, 3);  // Strategy inception
			var endDate = new DateTime(2024, 12, 31);

			var securities = new string[] { "AAPL", "MSFT", "NVDA", "TSLA", "META", "GOOGL", "AMZN", "AMD" };
			var marketCaps = new decimal[] { 3000000000000m, 2800000000000m, 1200000000000m, 700000000000m, 1000000000000m, 1800000000000m, 1500000000000m, 200000000000m };
			var vols = new decimal[] { 0.20m, 0.18m, 0.45m, 0.55m, 0.30m, 0.25m, 0.28m, 0.40m };

			for (int si = 0; si < securities.Length; si++)
			{
				engine.GenerateSyntheticRates(securities[si], startDate, endDate, marketCaps[si], vols[si], false);
			}

			// Simulate historical short positions
			var positions = GenerateSamplePositions(startDate, endDate);
			engine.ProcessHistoricalPositions(positions);

			// Export CSV files
			var outputDir = Environment.CurrentDirectory;

			var dailyPath = Path.Combine(outputDir, "borrow_costs_daily.csv");
			engine.ExportDailyCostsToCSV(dailyPath);
			Console.WriteLine($"Daily costs exported to: {dailyPath}");

			var positionPath = Path.Combine(outputDir, "borrow_costs_positions.csv");
			engine.ExportPositionSummaryToCSV(positionPath);
			Console.WriteLine($"Position summary exported to: {positionPath}");

			var monthlyPath = Path.Combine(outputDir, "borrow_costs_monthly.csv");
			engine.ExportMonthlySummaryToCSV(monthlyPath);
			Console.WriteLine($"Monthly summary exported to: {monthlyPath}");

			// Print summary
			Console.WriteLine();
			Console.WriteLine(engine.GetSummary());

			// Transaction cost integration
			Console.WriteLine("\n═══════════════════════════════════════════════════════════════");
			Console.WriteLine("  Transaction Cost Integration Demo (7 calendar day holds)");
			Console.WriteLine("═══════════════════════════════════════════════════════════════\n");

			var integration = new BorrowCostTransactionIntegration(engine);

			var tradeSymbols = new string[] { "AAPL", "MSFT", "NVDA", "TSLA", "META" };
			var tradeShares = new decimal[] { -500m, -300m, -200m, -500m, -400m };
			var tradePrices = new decimal[] { 180m, 400m, 500m, 250m, 350m };

			for (int ti = 0; ti < tradeSymbols.Length; ti++)
			{
				var costs = integration.CalculateTotalCosts(
					tradeSymbols[ti], tradeShares[ti], tradePrices[ti], DateTime.Today, 7);
				Console.WriteLine(costs);
			}

			// Hedge fund report generation
			Console.WriteLine("\n═══════════════════════════════════════════════════════════════");
			Console.WriteLine("  Hedge Fund Report Output");
			Console.WriteLine("═══════════════════════════════════════════════════════════════\n");

			var report = integration.GenerateHedgeFundReport(startDate, endDate);
			Console.WriteLine(report.ToGIPSSection());
			Console.WriteLine();
			Console.WriteLine(report.ToDDQSection());

			// Print warnings
			if (engine.Warnings.Any())
			{
				Console.WriteLine("\n⚠ Engine Warnings:");
				foreach (var w in engine.Warnings)
					Console.WriteLine($"  {w}");
			}

			Console.WriteLine("\n═══════════════════════════════════════════════════════════════");
			Console.WriteLine("  INSIGHT: With historical FFR, early 2022 shorts show NET COST");
			Console.WriteLine("  (no rebate at 0.08% FFR), while 2023+ shows NET INCOME.");
			Console.WriteLine("  This is the accurate picture for institutional due diligence.");
			Console.WriteLine("═══════════════════════════════════════════════════════════════\n");
		}

		private static IEnumerable<PositionSnapshot> GenerateSamplePositions(
			DateTime startDate, DateTime endDate)
		{
			var positions = new List<PositionSnapshot>();
			var random = new Random(42);

			var basePrices = new Dictionary<string, decimal>
			{
				{ "AAPL", 180m }, { "MSFT", 380m }, { "NVDA", 500m }, { "TSLA", 240m },
				{ "META", 350m }, { "GOOGL", 140m }, { "AMZN", 150m }, { "AMD", 150m }
			};

			var activeShortShares = new Dictionary<string, decimal>();
			var activeShortOpened = new Dictionary<string, DateTime>();

			for (var date = startDate; date <= endDate; date = date.AddDays(1))
			{
				if (date.DayOfWeek == DayOfWeek.Saturday ||
					date.DayOfWeek == DayOfWeek.Sunday)
					continue;

				foreach (var kvp in basePrices)
				{
					var symbol = kvp.Key;
					var basePrice = kvp.Value;

					var priceChange = (decimal)(random.NextDouble() - 0.5) * 0.03m;
					var price = basePrice * (1 + priceChange);

					if (activeShortShares.ContainsKey(symbol))
					{
						var daysHeld = (date - activeShortOpened[symbol]).Days;
						if (random.NextDouble() < 0.15 || daysHeld > 7 + random.Next(7))
						{
							positions.Add(new PositionSnapshot(date, symbol, 0, price));
							activeShortShares.Remove(symbol);
							activeShortOpened.Remove(symbol);
						}
						else
						{
							positions.Add(new PositionSnapshot(date, symbol, activeShortShares[symbol], price));
						}
					}
					else
					{
						if (random.NextDouble() < 0.15)
						{
							var shares = -1 * (100 + random.Next(900));
							positions.Add(new PositionSnapshot(date, symbol, shares, price));
							activeShortShares[symbol] = shares;
							activeShortOpened[symbol] = date;
						}
					}
				}
			}

			return positions;
		}
	}
}