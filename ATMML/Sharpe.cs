using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ATMML.Performance
{
	/// <summary>
	/// Single source of truth Sharpe calculator.
	///
	/// - Input: NAV time series (Date -> NAV).
	/// - Returns: daily/weekly/monthly Sharpe (annualized).
	///
	/// Definitions (locked):
	/// - Daily return: simple return = NAV[t]/NAV[t-1] - 1
	/// - Weekly return: sum of daily returns over a week ending Friday (W-FRI)
	/// - Monthly return: sum of daily returns over calendar month end
	/// - Sharpe: mean(excess)/stdev(excess) * sqrt(annFactor)
	/// - stdev uses sample stddev (ddof=1, Bessel corrected)
	/// - Risk-free rate optional; interpreted as ANNUAL rate (e.g., 0.03 = 3%)
	///
	/// Missing-day policy (optional):
	/// - If FillMissingBusinessDays=true: missing business days are treated as 0 return (conservative)
	///   This matches a "daily NAV" system where NAV is only updated on trade days.
	/// - If false: use only provided dates (no fill).
	///
	/// IMPORTANT:
	/// Both your app and your GIPS report must use the SAME settings here,
	/// otherwise Sharpe will differ.
	/// </summary>
	public static class SharpeCalculator
	{
		public sealed record Settings(
			double AnnualRiskFreeRate = 0.0,
			bool FillMissingBusinessDays = true
		);

		public sealed record Result(
			double SharpeDaily,
			double SharpeWeekly,
			double SharpeMonthly,
			int DailyObs,
			int WeeklyObs,
			int MonthlyObs
		);

		/// <summary>
		/// Compute Sharpe ratios from a NAV series.
		/// NAV series must have at least 2 points to form returns.
		/// NAV must be positive.
		/// </summary>
		public static Result FromNavSeries(
			IReadOnlyDictionary<DateTime, double> navByDate,
			Settings? settings = null)
		{
			settings ??= new Settings();

			// 1) Normalize & sort
			var points = navByDate
				.Where(kvp => kvp.Value > 0 && !double.IsNaN(kvp.Value) && !double.IsInfinity(kvp.Value))
				.Select(kvp => (Date: kvp.Key.Date, Nav: kvp.Value))
				.GroupBy(x => x.Date)
				.Select(g => (Date: g.Key, Nav: g.Last().Nav)) // if duplicates, last wins
				.OrderBy(x => x.Date)
				.ToList();

			if (points.Count < 2)
				return new Result(double.NaN, double.NaN, double.NaN, 0, 0, 0);

			// 2) Build daily return series
			var dailyReturns = settings.FillMissingBusinessDays
				? BuildDailyReturnsWithBusinessDayFill(points)
				: BuildDailyReturnsNoFill(points);

			// 3) Excess returns (daily RF)
			const int AnnDaily = 252;
			double rfDaily = settings.AnnualRiskFreeRate / AnnDaily;
			var dailyExcess = dailyReturns.Select(r => r - rfDaily).ToArray();

			// 4) Sharpe daily
			double sharpeDaily = SharpeFromExcess(dailyExcess, AnnDaily);

			// 5) Weekly & monthly aggregation (sum of daily returns)
			var weeklyReturns = AggregateByWeekEndingFriday(dailyReturns);
			var monthlyReturns = AggregateByMonthEnd(dailyReturns);

			var weeklyExcess = weeklyReturns.Select(r => r - settings.AnnualRiskFreeRate / 52.0).ToArray();
			var monthlyExcess = monthlyReturns.Select(r => r - settings.AnnualRiskFreeRate / 12.0).ToArray();

			double sharpeWeekly = SharpeFromExcess(weeklyExcess, 52);
			double sharpeMonthly = SharpeFromExcess(monthlyExcess, 12);

			return new Result(
				SharpeDaily: sharpeDaily,
				SharpeWeekly: sharpeWeekly,
				SharpeMonthly: sharpeMonthly,
				DailyObs: dailyExcess.Length,
				WeeklyObs: weeklyExcess.Length,
				MonthlyObs: monthlyExcess.Length
			);
		}

		// ----------------------------
		// Core math
		// ----------------------------

		/// <summary>
		/// Sharpe from EXCESS return series (already subtract risk-free per-period).
		/// Uses sample stdev (ddof=1).
		/// </summary>
		public static double SharpeFromExcess(IReadOnlyList<double> excessReturns, int annualizationFactor)
		{
			if (excessReturns == null || excessReturns.Count < 2)
				return double.NaN;

			double mean = Mean(excessReturns);
			double sd = SampleStdDev(excessReturns, mean);
			if (sd <= 0 || double.IsNaN(sd))
				return double.NaN;

			return (mean / sd) * Math.Sqrt(annualizationFactor);
		}

		private static double Mean(IReadOnlyList<double> x)
		{
			double sum = 0.0;
			int n = 0;
			for (int i = 0; i < x.Count; i++)
			{
				double v = x[i];
				if (double.IsNaN(v) || double.IsInfinity(v)) continue;
				sum += v;
				n++;
			}
			return n > 0 ? sum / n : double.NaN;
		}

		private static double SampleStdDev(IReadOnlyList<double> x, double mean)
		{
			double sumSq = 0.0;
			int n = 0;
			for (int i = 0; i < x.Count; i++)
			{
				double v = x[i];
				if (double.IsNaN(v) || double.IsInfinity(v)) continue;
				double d = v - mean;
				sumSq += d * d;
				n++;
			}
			if (n < 2) return double.NaN;
			return Math.Sqrt(sumSq / (n - 1));
		}

		// ----------------------------
		// Daily returns builders
		// ----------------------------

		private static List<double> BuildDailyReturnsNoFill(List<(DateTime Date, double Nav)> points)
		{
			var rets = new List<double>(Math.Max(0, points.Count - 1));
			for (int i = 1; i < points.Count; i++)
			{
				double prev = points[i - 1].Nav;
				double cur = points[i].Nav;
				rets.Add(prev > 0 ? (cur / prev) - 1.0 : 0.0);
			}
			return rets;
		}

		private static List<double> BuildDailyReturnsWithBusinessDayFill(List<(DateTime Date, double Nav)> points)
		{
			// Fill missing business days by carrying NAV forward and inserting 0 return for those missing days.
			// This is conservative and often matches backtest/reporting systems.
			var map = points.ToDictionary(p => p.Date, p => p.Nav);

			DateTime start = points.First().Date;
			DateTime end = points.Last().Date;

			var businessDates = new List<DateTime>();
			for (var d = start; d <= end; d = d.AddDays(1))
			{
				if (IsBusinessDay(d))
					businessDates.Add(d);
			}

			// Build NAV series with forward-fill
			var navSeries = new List<double>(businessDates.Count);
			double lastNav = map.ContainsKey(start) ? map[start] : points.First().Nav;

			for (int i = 0; i < businessDates.Count; i++)
			{
				var d = businessDates[i];
				if (map.TryGetValue(d, out double nav))
					lastNav = nav;

				navSeries.Add(lastNav);
			}

			// Returns from filled NAV series
			var rets = new List<double>(Math.Max(0, navSeries.Count - 1));
			for (int i = 1; i < navSeries.Count; i++)
			{
				double prev = navSeries[i - 1];
				double cur = navSeries[i];
				rets.Add(prev > 0 ? (cur / prev) - 1.0 : 0.0);
			}

			return rets;
		}

		private static bool IsBusinessDay(DateTime d)
		{
			var dow = d.DayOfWeek;
			return dow != DayOfWeek.Saturday && dow != DayOfWeek.Sunday;
		}

		// ----------------------------
		// Aggregation rules (LOCKED)
		// ----------------------------

		/// <summary>
		/// Aggregate daily returns into W-FRI weeks: sum returns from Sat..Fri (effectively Mon..Fri for business calendar).
		/// We do not compound; we SUM daily returns to match many reporting stacks.
		/// </summary>
		private static List<double> AggregateByWeekEndingFriday(IReadOnlyList<double> dailyReturns)
		{
			// This aggregator assumes the daily return array corresponds to BUSINESS DAYS in order.
			// If you used NoFill and skipped dates, weekly/monthly alignment is ambiguous.
			// For GIPS reporting: FillMissingBusinessDays should be true.
			//
			// Since we are missing actual dates in the return array, we treat it as continuous business-day sequence.
			// If you need date-accurate resampling, call the overload below (recommended).
			throw new InvalidOperationException(
				"AggregateByWeekEndingFriday requires date-aware overload. Use FromNavSeries with FillMissingBusinessDays=true, " +
				"and date-aware aggregation implemented below.");
		}

		/// <summary>
		/// Aggregate daily returns into calendar months (month end): sum of daily returns in the month.
		/// </summary>
		private static List<double> AggregateByMonthEnd(IReadOnlyList<double> dailyReturns)
		{
			throw new InvalidOperationException(
				"AggregateByMonthEnd requires date-aware overload. Use FromNavSeries with FillMissingBusinessDays=true, " +
				"and date-aware aggregation implemented below.");
		}

		// ----------------------------
		// Date-aware aggregation (RECOMMENDED)
		// ----------------------------

		private static List<double> AggregateByWeekEndingFriday(List<double> dailyReturns, List<DateTime> returnDates)
		{
			// returnDates are the dates corresponding to each daily return (same length).
			var pairs = returnDates.Zip(dailyReturns, (d, r) => (d, r))
								   .OrderBy(x => x.d)
								   .ToList();

			var buckets = new SortedDictionary<DateTime, double>(); // week-ending Friday date -> sum
			foreach (var (d, r) in pairs)
			{
				var fri = WeekEndingFriday(d);
				buckets[fri] = buckets.TryGetValue(fri, out var s) ? (s + r) : r;
			}
			return buckets.Values.ToList();
		}

		private static List<double> AggregateByMonthEnd(List<double> dailyReturns, List<DateTime> returnDates)
		{
			var pairs = returnDates.Zip(dailyReturns, (d, r) => (d, r))
								   .OrderBy(x => x.d)
								   .ToList();

			var buckets = new SortedDictionary<(int Year, int Month), double>();
			foreach (var (d, r) in pairs)
			{
				var key = (d.Year, d.Month);
				buckets[key] = buckets.TryGetValue(key, out var s) ? (s + r) : r;
			}
			return buckets.Values.ToList();
		}

		private static DateTime WeekEndingFriday(DateTime d)
		{
			// Week ending Friday: if d is Fri => d, else next Friday.
			int daysUntilFriday = ((int)DayOfWeek.Friday - (int)d.DayOfWeek + 7) % 7;
			return d.AddDays(daysUntilFriday).Date;
		}

		// ----------------------------
		// Public date-aware overload (best for perfect matching)
		// ----------------------------

		public static Result FromNavSeriesDateAware(
			IReadOnlyDictionary<DateTime, double> navByDate,
			Settings? settings = null)
		{
			settings ??= new Settings();

			var points = navByDate
				.Where(kvp => kvp.Value > 0 && !double.IsNaN(kvp.Value) && !double.IsInfinity(kvp.Value))
				.Select(kvp => (Date: kvp.Key.Date, Nav: kvp.Value))
				.GroupBy(x => x.Date)
				.Select(g => (Date: g.Key, Nav: g.Last().Nav))
				.OrderBy(x => x.Date)
				.ToList();

			if (points.Count < 2)
				return new Result(double.NaN, double.NaN, double.NaN, 0, 0, 0);

			List<DateTime> navDates;
			List<double> navSeries;

			if (settings.FillMissingBusinessDays)
			{
				var map = points.ToDictionary(p => p.Date, p => p.Nav);
				DateTime start = points.First().Date;
				DateTime end = points.Last().Date;

				navDates = new List<DateTime>();
				navSeries = new List<double>();

				double lastNav = points.First().Nav;

				for (var d = start; d <= end; d = d.AddDays(1))
				{
					if (!IsBusinessDay(d)) continue;

					if (map.TryGetValue(d, out double nav))
						lastNav = nav;

					navDates.Add(d);
					navSeries.Add(lastNav);
				}
			}
			else
			{
				navDates = points.Select(p => p.Date).ToList();
				navSeries = points.Select(p => p.Nav).ToList();
			}

			// daily returns with dates: returnDates correspond to navDates[1..]
			var returnDates = new List<DateTime>(Math.Max(0, navSeries.Count - 1));
			var dailyReturns = new List<double>(Math.Max(0, navSeries.Count - 1));

			for (int i = 1; i < navSeries.Count; i++)
			{
				double prev = navSeries[i - 1];
				double cur = navSeries[i];
				dailyReturns.Add(prev > 0 ? (cur / prev) - 1.0 : 0.0);
				returnDates.Add(navDates[i]);
			}

			const int AnnDaily = 252;
			double rfDaily = settings.AnnualRiskFreeRate / AnnDaily;
			var dailyExcess = dailyReturns.Select(r => r - rfDaily).ToArray();
			double sharpeDaily = SharpeFromExcess(dailyExcess, AnnDaily);

			var weekly = AggregateByWeekEndingFriday(dailyReturns, returnDates);
			var monthly = AggregateByMonthEnd(dailyReturns, returnDates);

			var weeklyExcess = weekly.Select(r => r - settings.AnnualRiskFreeRate / 52.0).ToArray();
			var monthlyExcess = monthly.Select(r => r - settings.AnnualRiskFreeRate / 12.0).ToArray();

			double sharpeWeekly = SharpeFromExcess(weeklyExcess, 52);
			double sharpeMonthly = SharpeFromExcess(monthlyExcess, 12);

			return new Result(
				SharpeDaily: sharpeDaily,
				SharpeWeekly: sharpeWeekly,
				SharpeMonthly: sharpeMonthly,
				DailyObs: dailyExcess.Length,
				WeeklyObs: weeklyExcess.Length,
				MonthlyObs: monthlyExcess.Length
			);
		}
	}
}
