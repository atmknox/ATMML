// PerformanceReporting.cs
//
// Combined file that includes:
// 1) TotalReturnReporting (SEC + GIPS-aware total return engine)
// 2) Generic CsvExporter utility
// 3) Trade-report CSV export hooks for:
//      - CampaignBlotter
//      - CampaignDetail
//      - DetailedTradeBlotter
//
// NOTE:
// - The trade-report section includes small stubs for CampaignBlotter/CampaignDetail/DetailedTradeBlotter
//   and their Row types so this file compiles standalone.
//   If you already have real versions of those types in your project, DELETE the stubs and
//   update the export calls to match your actual row collection property (Rows/Items/Records/etc).

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace PerformanceReporting
{
	// ---------------------------------------------------------
	// Core configuration (put this in your app-level config)
	// ---------------------------------------------------------
	public sealed class TotalReturnConfig
	{
		/// <summary>
		/// Management fee rate per year (e.g., 0.005 = 0.50%).
		/// </summary>
		public decimal ManagementFeeRateAnnual { get; set; } = 0.000m;  // was 0.005

		/// <summary>
		/// Incentive / performance fee rate (e.g., 0.20 = 20%).
		/// </summary>
		public decimal PerformanceFeeRate { get; set; } = 0.00m;  // was 0.20

		/// <summary>
		/// Custody fee rate per year, as % of AUM, e.g. 0.0005 = 0.05%.
		/// </summary>
		public decimal CustodyFeeRateAnnual { get; set; } = 0.0000m;  // was 0.0005m

		/// <summary>
		/// Day-count basis used for annual-to-period fee accrual (e.g. 365 or 252).
		/// </summary>
		public int DayCountBasis { get; set; } = 365;

		/// <summary>
		/// If true, performance fee uses a high-water mark (HWM).
		/// If false, it is simply % of positive period profit.
		/// </summary>
		public bool UseHighWaterMark { get; set; } = false;

		/// <summary>
		/// Optional starting high-water mark NAV per share or portfolio NAV.
		/// You can update this dynamically when performance fees crystallize.
		/// </summary>
		public decimal? InitialHighWaterMarkNav { get; set; } = null;

		/// <summary>
		/// If true, operating expenses are assumed ALREADY included in NAV_end_before_fees.
		/// If false, OperatingExpenses in PeriodCashFlows are deducted explicitly.
		/// </summary>
		public bool OperatingExpensesInNav { get; set; } = true;

		/// <summary>
		/// If true, withholding taxes are treated as flowing out AFTER net-of-fee performance,
		/// and a separate net-after-tax return is shown.
		/// </summary>
		public bool TrackAfterTaxReturn { get; set; } = true;

		/// <summary>
		/// Tolerance for NAV continuity checks in basis points (e.g., 1 = 0.01%).
		/// </summary>
		public decimal NavContinuityToleranceBps { get; set; } = 1m;
	}

	// ---------------------------------------------------------
	// Period-level economic inputs (other than NAVs)
	// ---------------------------------------------------------
	public sealed class PeriodCashFlows
	{
		// External investor flows (subscriptions/redemptions/distributions)
		public decimal Subscriptions { get; set; } = 0m;
		public decimal Redemptions { get; set; } = 0m;
		public decimal DistributionsToInvestors { get; set; } = 0m;

		// Flow timing for Modified Dietz weighting
		// Number of days from period start when flow occurred (0 = start, totalDays = end)
		public double SubscriptionsDaysFromStart { get; set; } = 0.0; // Default: beginning of period
		public double RedemptionsDaysFromStart { get; set; } = 0.0;
		public double DistributionsDaysFromStart { get; set; } = 0.0;

		// Trading / transaction costs
		public decimal Commissions { get; set; } = 0m;
		public decimal MarketImpactCosts { get; set; } = 0m;

		// Income & financing
		public decimal DividendsReceived { get; set; } = 0m;
		public decimal DividendsPaid { get; set; } = 0m; // e.g., on shorts
		public decimal InterestIncome { get; set; } = 0m;
		public decimal InterestExpense { get; set; } = 0m;
		public decimal FinancingCosts { get; set; } = 0m; // margin / repo / leverage

		// Operating expenses (legal, audit, admin, compliance, overhead)
		public decimal OperatingExpenses { get; set; } = 0m;

		// Taxes
		public decimal TaxesWithheldOnDividends { get; set; } = 0m;
		public decimal TaxesWithheldOnCapitalGains { get; set; } = 0m;

		/// <summary>
		/// Net external investor cash flow (subscriptions - redemptions - distributions).
		/// Positive = net cash in; negative = net cash out.
		/// </summary>
		public decimal NetExternalFlow() => Subscriptions - Redemptions - DistributionsToInvestors;

		/// <summary>Total taxes withheld in period (for reporting-only attribution).</summary>
		public decimal TotalTaxesWithheld() => TaxesWithheldOnDividends + TaxesWithheldOnCapitalGains;

		/// <summary>Trading-related costs (commissions + market impact).</summary>
		public decimal TotalTradingCosts() => Commissions + MarketImpactCosts;

		/// <summary>Total financing cost (interest expense + leverage costs).</summary>
		public decimal TotalFinancingCosts() => InterestExpense + FinancingCosts - InterestIncome;
	}

	// ---------------------------------------------------------
	// Period-level output / breakdown
	// ---------------------------------------------------------
	public sealed class PeriodReturnBreakdown
	{
		public DateTime StartDate { get; set; }
		public DateTime EndDate { get; set; }

		public decimal StartNav { get; set; }
		public decimal EndNavBeforeFees { get; set; }
		public decimal EndNavAfterFeesBeforeTax { get; set; }
		public decimal EndNavAfterTax { get; set; }

		public decimal NetExternalFlow { get; set; }

		// Fee and expense amounts for the period
		public decimal ManagementFee { get; set; }
		public decimal PerformanceFee { get; set; }
		public decimal CustodyFee { get; set; }
		public decimal OperatingExpensesExplicit { get; set; }
		public decimal TaxesWithheld { get; set; }

		// Return figures
		public double GrossOfFeesReturn { get; set; }       // net of transaction costs, before mgmt/perf/custody
		public double NetOfFeesReturn { get; set; }         // after mgmt + perf + custody (+ optionally operating)
		public double NetAfterTaxReturn { get; set; }       // after net-of-fee and taxes withheld

		// Diagnostics
		public double PeriodLengthInDays { get; set; }
		public decimal AverageAum { get; set; }

		// High-water mark tracking
		public decimal? HighWaterMarkAtStart { get; set; }
		public decimal? HighWaterMarkAtEnd { get; set; }

		public override string ToString()
		{
			return $"{StartDate:yyyy-MM-dd}->{EndDate:yyyy-MM-dd} | " +
				   $"Gross: {GrossOfFeesReturn:P4}, Net: {NetOfFeesReturn:P4}, " +
				   $"NetAfterTax: {NetAfterTaxReturn:P4}";
		}
	}

	// ---------------------------------------------------------
	// Engine: single-period and chained time-weighted returns
	// ---------------------------------------------------------
	public sealed class TotalReturnEngine
	{
		private readonly TotalReturnConfig _cfg;
		private decimal? _currentHighWaterMarkNav;

		public TotalReturnEngine(TotalReturnConfig cfg)
		{
			_cfg = cfg ?? throw new ArgumentNullException(nameof(cfg));
			_currentHighWaterMarkNav = cfg.InitialHighWaterMarkNav;
		}

		/// <summary>
		/// Compute a single period's return breakdown.
		/// navEndBeforeFees should be the NAV after all trading/income/financing/etc.,
		/// but BEFORE management, performance, and custody fees (and optionally operating expenses).
		/// </summary>
		public PeriodReturnBreakdown ComputePeriodReturn(
			DateTime startDate,
			DateTime endDate,
			decimal navStart,
			decimal navEndBeforeFees,
			PeriodCashFlows cashFlows)
		{
			if (navStart <= 0m) throw new ArgumentOutOfRangeException(nameof(navStart));
			if (navEndBeforeFees <= 0m) throw new ArgumentOutOfRangeException(nameof(navEndBeforeFees));

			cashFlows ??= new PeriodCashFlows();

			// --- 1) Basic quantities ---
			double days = (endDate - startDate).TotalDays;
			if (days <= 0) throw new ArgumentException("End date must be after start date.");

			var netExternal = cashFlows.NetExternalFlow();

			// Modified Dietz with proper flow timing
			decimal weightedFlows = 0m;

			if (cashFlows.Subscriptions != 0m)
			{
				double weight = 1.0 - (cashFlows.SubscriptionsDaysFromStart / days);
				weightedFlows += cashFlows.Subscriptions * (decimal)weight;
			}

			if (cashFlows.Redemptions != 0m)
			{
				double weight = 1.0 - (cashFlows.RedemptionsDaysFromStart / days);
				weightedFlows -= cashFlows.Redemptions * (decimal)weight;
			}

			if (cashFlows.DistributionsToInvestors != 0m)
			{
				double weight = 1.0 - (cashFlows.DistributionsDaysFromStart / days);
				weightedFlows -= cashFlows.DistributionsToInvestors * (decimal)weight;
			}

			decimal dietzDenominator = navStart + weightedFlows;

			if (dietzDenominator <= 0m)
				throw new InvalidOperationException("Dietz denominator is non-positive; check NAVs and flows.");

			decimal investmentGainBeforeFees = navEndBeforeFees - navStart - netExternal;
			double grossReturn = (double)(investmentGainBeforeFees / dietzDenominator);

			// Average AUM for fee calculations
			decimal avgAum = (navStart + navEndBeforeFees) / 2m;
			if (avgAum < 0m) avgAum = Math.Max(navStart, 0m);

			decimal mgmtFeeRatePeriod =
				_cfg.ManagementFeeRateAnnual * (decimal)(days / _cfg.DayCountBasis);

			decimal custodyFeeRatePeriod =
				_cfg.CustodyFeeRateAnnual * (decimal)(days / _cfg.DayCountBasis);

			decimal managementFee = mgmtFeeRatePeriod * avgAum;
			decimal custodyFee = custodyFeeRatePeriod * avgAum;

			// --- 3) Performance fee (with optional HWM) ---
			decimal? hwmAtStart = _currentHighWaterMarkNav;

			decimal navAfterMgmtCustody = navEndBeforeFees - managementFee - custodyFee;

			decimal performanceFeeBase;
			if (_cfg.UseHighWaterMark && _currentHighWaterMarkNav.HasValue)
			{
				decimal hwm = _currentHighWaterMarkNav.Value;
				performanceFeeBase = Math.Max(0m, navAfterMgmtCustody - hwm);
			}
			else
			{
				decimal profitAfterMgmtCustody = navAfterMgmtCustody - navStart - netExternal;
				performanceFeeBase = Math.Max(0m, profitAfterMgmtCustody);
			}

			decimal performanceFee = _cfg.PerformanceFeeRate * performanceFeeBase;

			decimal? hwmAtEnd = hwmAtStart;
			if (_cfg.UseHighWaterMark && performanceFee > 0m)
			{
				_currentHighWaterMarkNav = navAfterMgmtCustody - performanceFee;
				hwmAtEnd = _currentHighWaterMarkNav;
			}

			// --- 4) Optional explicit operating expenses ---
			decimal explicitOperating = 0m;
			if (!_cfg.OperatingExpensesInNav)
				explicitOperating = cashFlows.OperatingExpenses;

			decimal navAfterFeesBeforeTax = navEndBeforeFees
											- managementFee
											- custodyFee
											- performanceFee
											- explicitOperating;

			// --- 5) Net-of-fees and after-tax returns ---
			decimal netProfitBeforeTax = navAfterFeesBeforeTax - navStart - netExternal;
			double netOfFeesReturn = (double)(netProfitBeforeTax / dietzDenominator);

			decimal taxesWithheld = _cfg.TrackAfterTaxReturn
				? cashFlows.TotalTaxesWithheld()
				: 0m;

			decimal navAfterTax = navAfterFeesBeforeTax - taxesWithheld;
			decimal netProfitAfterTax = navAfterTax - navStart - netExternal;
			double netAfterTaxReturn = (double)(netProfitAfterTax / dietzDenominator);

			return new PeriodReturnBreakdown
			{
				StartDate = startDate,
				EndDate = endDate,
				StartNav = navStart,
				EndNavBeforeFees = navEndBeforeFees,
				EndNavAfterFeesBeforeTax = navAfterFeesBeforeTax,
				EndNavAfterTax = navAfterTax,
				NetExternalFlow = netExternal,
				ManagementFee = managementFee,
				PerformanceFee = performanceFee,
				CustodyFee = custodyFee,
				OperatingExpensesExplicit = explicitOperating,
				TaxesWithheld = taxesWithheld,
				GrossOfFeesReturn = grossReturn,
				NetOfFeesReturn = netOfFeesReturn,
				NetAfterTaxReturn = netAfterTaxReturn,
				PeriodLengthInDays = days,
				AverageAum = avgAum,
				HighWaterMarkAtStart = hwmAtStart,
				HighWaterMarkAtEnd = hwmAtEnd
			};
		}

		/// <summary>
		/// Compute a chained time-weighted return series from a sequence
		/// of periods. Includes NAV continuity validation.
		/// </summary>
		public (double GrossTwr, double NetTwr, double NetAfterTaxTwr,
				IReadOnlyList<PeriodReturnBreakdown> Periods)
			ComputeTimeWeightedSeries(
				IReadOnlyList<(DateTime Start, DateTime End,
							   decimal NavStart, decimal NavEndBeforeFees,
							   PeriodCashFlows Flows)> periods)
		{
			if (periods == null || periods.Count == 0)
				throw new ArgumentException("At least one period is required.", nameof(periods));

			var breakdowns = new List<PeriodReturnBreakdown>(periods.Count);

			double grossLink = 1.0;
			double netLink = 1.0;
			double netAfterTaxLink = 1.0;

			PeriodReturnBreakdown? previousBreakdown = null;

			foreach (var p in periods)
			{
				if (previousBreakdown != null)
				{
					decimal expectedStartNav = previousBreakdown.EndNavAfterTax;
					decimal actualStartNav = p.NavStart;
					decimal difference = Math.Abs(actualStartNav - expectedStartNav);
					decimal toleranceAmount = expectedStartNav * (_cfg.NavContinuityToleranceBps / 10000m);

					if (difference > toleranceAmount)
					{
						throw new InvalidOperationException(
							$"NAV continuity violation detected between periods:\n" +
							$"  Previous period ended on {previousBreakdown.EndDate:yyyy-MM-dd} " +
							$"with NAV after tax = {expectedStartNav:N2}\n" +
							$"  Current period starting {p.Start:yyyy-MM-dd} " +
							$"has NAV = {actualStartNav:N2}\n" +
							$"  Difference: {difference:N2} (tolerance: {toleranceAmount:N2})\n" +
							$"  Check for missing flows, incorrect fees, or data errors.");
					}
				}

				var br = ComputePeriodReturn(p.Start, p.End, p.NavStart, p.NavEndBeforeFees, p.Flows);
				breakdowns.Add(br);

				grossLink *= (1.0 + br.GrossOfFeesReturn);
				netLink *= (1.0 + br.NetOfFeesReturn);
				netAfterTaxLink *= (1.0 + br.NetAfterTaxReturn);

				previousBreakdown = br;
			}

			return (grossLink - 1.0, netLink - 1.0, netAfterTaxLink - 1.0, breakdowns);
		}

		public decimal? GetCurrentHighWaterMark() => _currentHighWaterMarkNav;
		public void SetHighWaterMark(decimal? hwm) => _currentHighWaterMarkNav = hwm;
	}

	// ---------------------------------------------------------
	// Bloomberg fields needed for the calculations
	// ---------------------------------------------------------
	public static class BloombergFields
	{
		public const string PxLast = "PX_LAST";
		public const string TotReturnIndexGrossDivs = "TOT_RETURN_INDEX_GROSS_DVDS";
		public const string TotReturnIndexNetDivs = "TOT_RETURN_INDEX_NET_DVDS";
		public const string CustTotalReturnHoldingPeriod = "CUST_TRR_RETURN_HOLDING_PER";

		public const string DividendAmount = "DVD_AMT";
		public const string DividendCurrency = "DVD_CRNCY";
		public const string DividendExDate = "DVD_EX_DT";
		public const string DividendPayDate = "DVD_PAY_DT";
		public const string IndicatedDividendYield = "EQY_DVD_YLD_IND";
		public const string Trailing12MDividendYield = "EQY_DVD_YLD_12M";

		public const string CurrentMarketCap = "CUR_MKT_CAP";
		public const string SharesOutstanding = "EQY_SH_OUT";
	}

	// =====================================================================
	// CSV EXPORT (Generic)
	// =====================================================================
	public static class CsvExporter
	{
		/// <summary>
		/// Writes an IEnumerable of POCOs (public readable properties) to CSV.
		/// - Deterministic header ordering: uses provided columnOrder if given, else reflection order.
		/// - Culture-invariant numbers/dates (avoids Excel locale surprises).
		/// </summary>
		public static void WriteCsv<T>(
			IEnumerable<T> records,
			string filePath,
			IReadOnlyList<string>? columnOrder = null,
			bool includeHeader = true)
		{
			if (records == null) throw new ArgumentNullException(nameof(records));
			if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentNullException(nameof(filePath));

			var list = records as IList<T> ?? records.ToList();

			var props = typeof(T)
				.GetProperties(BindingFlags.Public | BindingFlags.Instance)
				.Where(p => p.CanRead)
				.ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);

			var columns = (columnOrder != null && columnOrder.Count > 0)
				? columnOrder.Where(c => props.ContainsKey(c)).ToList()
				: props.Keys.ToList();

			var dir = Path.GetDirectoryName(filePath);
			if (!string.IsNullOrWhiteSpace(dir))
				Directory.CreateDirectory(dir);

			// UTF8 BOM helps Excel open cleanly
			using var writer = new StreamWriter(filePath, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

			if (includeHeader)
				writer.WriteLine(string.Join(",", columns.Select(Escape)));

			foreach (var row in list)
			{
				var line = string.Join(",", columns.Select(col =>
				{
					var p = props[col];
					var v = p.GetValue(row, null);
					return Escape(ToInvariantString(v));
				}));

				writer.WriteLine(line);
			}
		}

		private static string ToInvariantString(object? value)
		{
			if (value == null) return "";

			return value switch
			{
				DateTime dt => dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
				DateTimeOffset dto => dto.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture),
				decimal d => d.ToString("G", CultureInfo.InvariantCulture),
				double d => d.ToString("G", CultureInfo.InvariantCulture),
				float f => f.ToString("G", CultureInfo.InvariantCulture),
				bool b => b ? "true" : "false",
				_ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""
			};
		}

		private static string Escape(string s)
		{
			if (string.IsNullOrEmpty(s)) return "";
			if (s.Contains(',') || s.Contains('"') || s.Contains('\n') || s.Contains('\r'))
				return $"\"{s.Replace("\"", "\"\"")}\"";
			return s;
		}
	}

	// =====================================================================
	// TRADE REPORTING CSV EXPORTS
	// =====================================================================
	public static class TradeReportingCsvExports
	{
		/// <summary>
		/// Exports all 3 trade artifacts to CSV in the same output folder.
		/// </summary>
		public static void ExportAllToCsv(
			CampaignBlotter campaignBlotter,
			CampaignDetail campaignDetail,
			DetailedTradeBlotter detailedTradeBlotter,
			string outputDirectory,
			string fileStem,
			DateTime reportDateUtc)
		{
			if (campaignBlotter == null) throw new ArgumentNullException(nameof(campaignBlotter));
			if (campaignDetail == null) throw new ArgumentNullException(nameof(campaignDetail));
			if (detailedTradeBlotter == null) throw new ArgumentNullException(nameof(detailedTradeBlotter));
			if (string.IsNullOrWhiteSpace(outputDirectory)) throw new ArgumentNullException(nameof(outputDirectory));
			if (string.IsNullOrWhiteSpace(fileStem)) throw new ArgumentNullException(nameof(fileStem));

			Directory.CreateDirectory(outputDirectory);

			var dateTag = reportDateUtc.ToString("yyyyMMdd");

			ExportCampaignBlotterCsv(
				campaignBlotter,
				Path.Combine(outputDirectory, $"{fileStem}_CampaignBlotter_{dateTag}.csv"));

			ExportCampaignDetailCsv(
				campaignDetail,
				Path.Combine(outputDirectory, $"{fileStem}_CampaignDetail_{dateTag}.csv"));

			ExportDetailedTradeBlotterCsv(
				detailedTradeBlotter,
				Path.Combine(outputDirectory, $"{fileStem}_DetailedTradeBlotter_{dateTag}.csv"));
		}

		public static void ExportCampaignBlotterCsv(CampaignBlotter blotter, string csvPath)
		{
			// If you want explicit column ordering, put property names here in the exact order you want.
			// Example:
			// IReadOnlyList<string> columns = new[] { "CampaignId", "Ticker", "OpenDate", "CloseDate", "NetPnl", ... };
			IReadOnlyList<string>? columns = null;

			CsvExporter.WriteCsv(blotter.Rows, csvPath, columns);
		}

		public static void ExportCampaignDetailCsv(CampaignDetail detail, string csvPath)
		{
			IReadOnlyList<string>? columns = null;

			CsvExporter.WriteCsv(detail.Rows, csvPath, columns);
		}

		public static void ExportDetailedTradeBlotterCsv(DetailedTradeBlotter detailed, string csvPath)
		{
			IReadOnlyList<string>? columns = null;

			CsvExporter.WriteCsv(detailed.Rows, csvPath, columns);
		}
	}

	// =====================================================================
	// TRADE REPORT STUB TYPES (REMOVE IF YOU ALREADY HAVE REAL TYPES)
	// =====================================================================
	// If these types already exist in your trade reporting project, DELETE everything
	// in this section and keep only TradeReportingCsvExports + CsvExporter above.

	public sealed class CampaignBlotter
	{
		// Adjust name if your actual property is Items/Records/etc.
		public IReadOnlyList<CampaignBlotterRow> Rows { get; init; } = Array.Empty<CampaignBlotterRow>();
	}

	public sealed class CampaignDetail
	{
		public IReadOnlyList<CampaignDetailRow> Rows { get; init; } = Array.Empty<CampaignDetailRow>();
	}

	public sealed class DetailedTradeBlotter
	{
		public IReadOnlyList<DetailedTradeBlotterRow> Rows { get; init; } = Array.Empty<DetailedTradeBlotterRow>();
	}

	// Minimal row stubs (replace with your real row models)
	public sealed class CampaignBlotterRow { }
	public sealed class CampaignDetailRow { }
	public sealed class DetailedTradeBlotterRow { }
}
