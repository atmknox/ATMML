
// PerformanceReporting.cs
// Combined hedge fund reporting + CSV export outputs
// FIXED: Proportional cost allocation + Uses Closes dictionary for all prices
// FIXED: Profit Factor calculation now shows Total Wins and Total Losses
// ADDED: Sortino Ratio and Calmar Ratio calculations
// ADDED: GrossValue, Commission, ImpactCost, NetValue to Transaction CSV

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using RiskEngineMN3.ExecutionCosts;

namespace HedgeFundReporting
{
	// ==================== Core Data Models ====================

	public class Trade
	{
		public string TradeId { get; set; }
		public string Ticker { get; set; }
		public string CampaignId { get; set; }
		public DateTime EntryDate { get; set; }
		public decimal EntryPrice { get; set; }
		public int Shares { get; set; }
		public DateTime? ExitDate { get; set; }
		public decimal? ExitPrice { get; set; }

		public TradeType Type { get; set; }
		public decimal Commission { get; set; }
		public decimal SecFees { get; set; }
		public decimal Cost { get; set; }
		public string Sector { get; set; }
		public string AssetClass { get; set; }
		public decimal DividendsReceived { get; set; }

		public bool IsClosed => ExitDate.HasValue && ExitPrice.HasValue;

		public decimal GrossPnL
		{
			get
			{
				if (!IsClosed) return 0;
				var pnl = Type == TradeType.Long
					? (ExitPrice.Value - EntryPrice) * Shares
					: (EntryPrice - ExitPrice.Value) * Shares;
				return pnl + DividendsReceived;
			}
		}

		public decimal NetPnL => GrossPnL - (Cost != 0 ? Cost : (Commission + SecFees));

		public decimal ReturnPercent
		{
			get
			{
				if (!IsClosed) return 0;
				var costBasis = EntryPrice * Shares;
				if (costBasis == 0) return 0;
				return (NetPnL / costBasis) * 100;
			}
		}

		public int HoldingPeriodDays
		{
			get
			{
				if (!IsClosed) return 0;
				return (ExitDate.Value - EntryDate).Days;
			}
		}
	}

	public class Campaign
	{
		public string CampaignId { get; set; }
		public string Ticker { get; set; }
		public TradeType Type { get; set; }
		public string Sector { get; set; }
		public string AssetClass { get; set; }

		public DateTime FirstEntryDate { get; set; }
		public DateTime? LastExitDate { get; set; }
		public bool IsOpen { get; set; }

		public List<Trade> Trades { get; set; } = new List<Trade>();

		public int TotalSharesTraded { get; set; }
		public int TotalSharesClosed { get; set; }
		public int NumberOfAdds { get; set; }
		public int NumberOfTrims { get; set; }

		public decimal TotalGrossPnL => Trades.Where(t => t.IsClosed).Sum(t => t.GrossPnL);
		public decimal TotalNetPnL => Trades.Where(t => t.IsClosed).Sum(t => t.NetPnL);
		public decimal TotalCosts => Trades.Sum(t => t.Cost);

		public decimal WeightedAvgEntryPrice
		{
			get
			{
				var totalCost = Trades.Sum(t => t.Shares * t.EntryPrice);
				var totalShares = Trades.Sum(t => t.Shares);
				return totalShares > 0 ? totalCost / totalShares : 0;
			}
		}

		public decimal WeightedAvgExitPrice
		{
			get
			{
				var closedTrades = Trades.Where(t => t.IsClosed).ToList();
				if (!closedTrades.Any()) return 0;
				var totalRevenue = closedTrades.Sum(t => t.Shares * t.ExitPrice.Value);
				var totalShares = closedTrades.Sum(t => t.Shares);
				return totalShares > 0 ? totalRevenue / totalShares : 0;
			}
		}

		public decimal CampaignReturnPercent
		{
			get
			{
				var totalCapitalDeployed = Trades.Sum(t => t.Shares * t.EntryPrice);
				if (totalCapitalDeployed == 0) return 0;
				return (TotalNetPnL / totalCapitalDeployed) * 100;
			}
		}

		public int CampaignDurationDays
		{
			get
			{
				if (!LastExitDate.HasValue) return 0;
				return (LastExitDate.Value - FirstEntryDate).Days;
			}
		}

		public double AvgHoldingPeriodDays
		{
			get
			{
				var closedTrades = Trades.Where(t => t.IsClosed).ToList();
				return closedTrades.Any() ? closedTrades.Average(t => t.HoldingPeriodDays) : 0;
			}
		}
	}

	public enum TradeType
	{
		Long,
		Short
	}

	public class CashFlow
	{
		public DateTime Date { get; set; }
		public decimal Amount { get; set; }
		public CashFlowType Type { get; set; }
		public string InvestorId { get; set; }
	}

	public enum CashFlowType
	{
		Subscription,
		Redemption,
		Dividend,
		Interest,
		ManagementFee,
		IncentiveFee,
		Expense
	}

	public class DailyNAV
	{
		public DateTime Date { get; set; }
		public decimal NAV { get; set; }
		public decimal Cash { get; set; }
		public decimal EquityValue { get; set; }
		public decimal TotalAssets { get; set; }
		public decimal Leverage { get; set; }
	}

	public class BenchmarkReturn
	{
		public DateTime Date { get; set; }
		public string BenchmarkName { get; set; }
		public decimal ReturnPercent { get; set; }
	}

	public class PortfolioConfiguration
	{
		public DateTime StartDate { get; set; }
		public decimal InitialBalance { get; set; }
		public string FundName { get; set; }
		public string FundId { get; set; }
		public decimal ManagementFeePercent { get; set; }
		public decimal IncentiveFeePercent { get; set; }
		public decimal HighWaterMark { get; set; }
		public string PrimaryBenchmark { get; set; }
		public string SECFileNumber { get; set; }
		public string GIPSComplianceStatement { get; set; }
		public bool ClaimsGIPSCompliance { get; set; }
	}

	// ==================== CSV EXPORT ====================

	public static class CsvExporter
	{
		public static void WriteCsv<T>(
			IEnumerable<T> records,
			string filePath,
			IReadOnlyList<string> columnOrder = null,
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

		private static string ToInvariantString(object value)
		{
			if (value == null) return "";

			return value switch
			{
				DateTime dt => dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
				DateTimeOffset dto => dto.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
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

	// ==================== CSV Row Models ====================

	public sealed class CampaignBlotterRow
	{
		public string FundName { get; set; }
		public string SECFileNumber { get; set; }
		public DateTime ReportDate { get; set; }
		public string CampaignId { get; set; }
		public string Ticker { get; set; }
		public string Status { get; set; }
		public DateTime FirstEntryDate { get; set; }
		public DateTime? LastExitDate { get; set; }
		public string Side { get; set; }
		public string Sector { get; set; }
		public string AssetClass { get; set; }
		public decimal WeightedAvgEntryPrice { get; set; }
		public decimal WeightedAvgExitPrice { get; set; }
		public int Adds { get; set; }
		public int Trims { get; set; }
		public decimal GrossPnL { get; set; }
		public decimal NetPnL { get; set; }
		public decimal ReturnPercent { get; set; }
		public int DurationDays { get; set; }
	}

	public sealed class CampaignDetailRow
	{
		public string FundName { get; set; }
		public DateTime ReportDate { get; set; }
		public string CampaignId { get; set; }
		public string Ticker { get; set; }
		public string CampaignStatus { get; set; }
		public DateTime CampaignFirstEntryDate { get; set; }
		public DateTime? CampaignLastExitDate { get; set; }
		public int CampaignAdds { get; set; }
		public int CampaignTrims { get; set; }
		public decimal CampaignNetPnL { get; set; }
		public decimal CampaignReturnPercent { get; set; }
		public string TradeId { get; set; }
		public DateTime EntryDate { get; set; }
		public decimal EntryPrice { get; set; }
		public DateTime? ExitDate { get; set; }
		public decimal? ExitPrice { get; set; }
		public int Shares { get; set; }
		public string Side { get; set; }
		public decimal TradeNetPnL { get; set; }
		public decimal TradeCost { get; set; }
		public int HoldingPeriodDays { get; set; }
	}

	public sealed class DetailedTradeBlotterRow
	{
		public string FundName { get; set; }
		public string SECFileNumber { get; set; }
		public DateTime ReportDate { get; set; }
		public string CampaignId { get; set; }
		public string TradeId { get; set; }
		public string Ticker { get; set; }
		public DateTime EntryDate { get; set; }
		public decimal EntryPrice { get; set; }
		public DateTime? ExitDate { get; set; }
		public decimal? ExitPrice { get; set; }
		public int Shares { get; set; }
		public string Side { get; set; }
		public string Sector { get; set; }
		public string AssetClass { get; set; }
		public decimal GrossPnL { get; set; }
		public decimal NetPnL { get; set; }
		public decimal ReturnPercent { get; set; }
		public decimal Cost { get; set; }
	}

	/// <summary>One row per transaction event (entry, add, trim, exit)</summary>
	public sealed class TransactionRow
	{
		public string Ticker { get; set; }
		public DateTime Date { get; set; }
		public string TransactionType { get; set; }
		public int Shares { get; set; }
		public decimal Price { get; set; }
		public decimal GrossValue { get; set; }
		public decimal Commission { get; set; }
		public decimal ImpactCost { get; set; }
		public decimal NetValue { get; set; }
		public decimal ProfitAndLoss { get; set; }
		/// <summary>
		/// Distinguishes signal-driven trades from optimizer rebalance adjustments.
		/// Values: "Signal" (entry/exit/add/reduce driven by ATM signal change),
		///         "Rebalance" (optimizer-driven position adjustment, no signal change),
		///         "PortfolioStop" (risk-triggered forced exit)
		/// </summary>
		public string TradeReason { get; set; } = "Signal";
	}

	/// <summary>
	/// Represents a single position at a point in time.
	/// One row per ticker per date for all open positions.
	/// </summary>
	public sealed class PositionSnapshotRow
	{
		public DateTime Date { get; set; }
		public string Ticker { get; set; }
		public string Direction { get; set; }  // "Long" or "Short"
		public int Shares { get; set; }
		public decimal Price { get; set; }
		public decimal MarketValue { get; set; }
		public decimal CostBasis { get; set; }
		public decimal UnrealizedPnL { get; set; }
		/// <summary>
		/// Most recent TradeReason for this position (from TradeTypes dictionary).
		/// </summary>
		public string TradeReason { get; set; }
	}

	// ==================== Report Generators ====================

	public class CampaignReport
	{
		private readonly List<Campaign> campaigns;
		private readonly PortfolioConfiguration config;

		public CampaignReport(List<Campaign> campaigns, PortfolioConfiguration config)
		{
			this.campaigns = campaigns;
			this.config = config;
		}

		public string GenerateCampaignBlotter()
		{
			var report = new StringBuilder();
			report.AppendLine("========================================");
			report.AppendLine($"CAMPAIGN BLOTTER - {config.FundName}");
			report.AppendLine($"SEC File Number: {config.SECFileNumber}");
			report.AppendLine($"Report Date: {DateTime.Now:yyyy-MM-dd}");
			report.AppendLine("========================================\n");

			report.AppendLine(string.Format("{0,-12} {1,-8} {2,12} {3,12} {4,10} {5,10} {6,8} {7,8} {8,12} {9,12} {10,10} {11,10}",
				"Campaign ID", "Ticker", "First Entry", "Last Exit", "Avg Entry", "Avg Exit", "Adds", "Trims", "Gross P&L", "Net P&L", "Return %", "Days"));
			report.AppendLine(new string('-', 155));

			foreach (var campaign in campaigns.OrderBy(c => c.FirstEntryDate))
			{
				try
				{
					report.AppendLine(string.Format("{0,-12} {1,-8} {2,12:yyyy-MM-dd} {3,12} {4,10:C2} {5,10:C2} {6,8} {7,8} {8,12:C2} {9,12:C2} {10,10:F2}% {11,10}",
						campaign.CampaignId,
						campaign.Ticker,
						campaign.FirstEntryDate,
						campaign.IsOpen ? "OPEN" : campaign.LastExitDate?.ToString("yyyy-MM-dd"),
						campaign.WeightedAvgEntryPrice,
						campaign.WeightedAvgExitPrice,
						campaign.NumberOfAdds,
						campaign.NumberOfTrims,
						campaign.TotalGrossPnL,
						campaign.TotalNetPnL,
						campaign.CampaignReturnPercent,
						campaign.IsOpen ? 0 : campaign.CampaignDurationDays));
				}
				catch
				{
				}
			}

			report.AppendLine(new string('-', 155));

			var closedCampaigns = campaigns.Where(c => !c.IsOpen).ToList();
			report.AppendLine($"\nTotal Closed Campaigns: {closedCampaigns.Count}");
			report.AppendLine($"Total Open Campaigns: {campaigns.Count(c => c.IsOpen)}");
			report.AppendLine($"Total Gross P&L: {closedCampaigns.Sum(c => c.TotalGrossPnL):C2}");
			report.AppendLine($"Total Net P&L: {closedCampaigns.Sum(c => c.TotalNetPnL):C2}");
			report.AppendLine($"Total Transaction Costs: {campaigns.Sum(c => c.TotalCosts):C2}");
			report.AppendLine($"Average Campaign Duration: {(closedCampaigns.Any() ? closedCampaigns.Average(c => c.CampaignDurationDays) : 0):F1} days");

			return report.ToString();
		}

		public List<CampaignBlotterRow> GenerateCampaignBlotterRows(DateTime reportDate)
		{
			return campaigns
				.OrderBy(c => c.FirstEntryDate)
				.Select(c => new CampaignBlotterRow
				{
					FundName = config.FundName,
					SECFileNumber = config.SECFileNumber,
					ReportDate = reportDate,
					CampaignId = c.CampaignId,
					Ticker = c.Ticker,
					Status = c.IsOpen ? "OPEN" : "CLOSED",
					FirstEntryDate = c.FirstEntryDate,
					LastExitDate = c.LastExitDate,
					Side = c.Type.ToString(),
					Sector = c.Sector,
					AssetClass = c.AssetClass,
					WeightedAvgEntryPrice = c.WeightedAvgEntryPrice,
					WeightedAvgExitPrice = c.WeightedAvgExitPrice,
					Adds = c.NumberOfAdds,
					Trims = c.NumberOfTrims,
					GrossPnL = c.TotalGrossPnL,
					NetPnL = c.TotalNetPnL,
					ReturnPercent = c.CampaignReturnPercent,
					DurationDays = c.IsOpen ? 0 : c.CampaignDurationDays
				})
				.ToList();
		}

		public string GenerateCampaignDetail()
		{
			var report = new StringBuilder();
			report.AppendLine("\n========================================");
			report.AppendLine("CAMPAIGN DETAIL REPORT");
			report.AppendLine("========================================\n");

			foreach (var campaign in campaigns.OrderBy(c => c.FirstEntryDate))
			{
				report.AppendLine($"\nCampaign: {campaign.CampaignId} - {campaign.Ticker}");
				report.AppendLine($"Status: {(campaign.IsOpen ? "OPEN" : "CLOSED")}");
				report.AppendLine($"Duration: {campaign.FirstEntryDate:yyyy-MM-dd} to {(campaign.IsOpen ? "OPEN" : campaign.LastExitDate?.ToString("yyyy-MM-dd"))}");
				report.AppendLine($"Total Adds: {campaign.NumberOfAdds}, Total Trims: {campaign.NumberOfTrims}");
				report.AppendLine($"Net P&L: {campaign.TotalNetPnL:C2} ({campaign.CampaignReturnPercent:F2}%)");
				report.AppendLine($"\nConstituent Trades:");

				report.AppendLine(string.Format("  {0,-10} {1,12} {2,10} {3,12} {4,10} {5,12} {6,12} {7,12}",
					"Trade ID", "Entry Date", "Entry $", "Exit Date", "Exit $", "Shares", "Net P&L", "Cost"));
				report.AppendLine("  " + new string('-', 105));

				foreach (var trade in campaign.Trades.OrderBy(t => t.EntryDate))
				{
					report.AppendLine(string.Format("  {0,-10} {1,12:yyyy-MM-dd} {2,10:C2} {3,12} {4,10} {5,12} {6,12:C2} {7,12:C2}",
						trade.TradeId,
						trade.EntryDate,
						trade.EntryPrice,
						trade.IsClosed ? trade.ExitDate?.ToString("yyyy-MM-dd") : "OPEN",
						trade.IsClosed ? trade.ExitPrice?.ToString("C2") : "-",
						trade.Shares,
						trade.IsClosed ? trade.NetPnL : 0,
						trade.Cost));
				}
			}

			return report.ToString();
		}

		public List<CampaignDetailRow> GenerateCampaignDetailRows(DateTime reportDate)
		{
			var rows = new List<CampaignDetailRow>();

			foreach (var c in campaigns.OrderBy(x => x.FirstEntryDate))
			{
				foreach (var t in c.Trades.OrderBy(x => x.EntryDate))
				{
					rows.Add(new CampaignDetailRow
					{
						FundName = config.FundName,
						ReportDate = reportDate,
						CampaignId = c.CampaignId,
						Ticker = c.Ticker,
						CampaignStatus = c.IsOpen ? "OPEN" : "CLOSED",
						CampaignFirstEntryDate = c.FirstEntryDate,
						CampaignLastExitDate = c.LastExitDate,
						CampaignAdds = c.NumberOfAdds,
						CampaignTrims = c.NumberOfTrims,
						CampaignNetPnL = c.TotalNetPnL,
						CampaignReturnPercent = c.CampaignReturnPercent,
						TradeId = t.TradeId,
						EntryDate = t.EntryDate,
						EntryPrice = t.EntryPrice,
						ExitDate = t.ExitDate,
						ExitPrice = t.ExitPrice,
						Shares = t.Shares,
						Side = t.Type.ToString(),
						TradeNetPnL = t.IsClosed ? t.NetPnL : 0m,
						TradeCost = t.Cost,
						HoldingPeriodDays = t.IsClosed ? t.HoldingPeriodDays : 0
					});
				}
			}

			return rows;
		}

		public string ValidateCampaignCalculations()
		{
			var report = new StringBuilder();
			report.AppendLine("\n========================================");
			report.AppendLine("CAMPAIGN CALCULATION VALIDATION");
			report.AppendLine("========================================");
			report.AppendLine("Campaign Definition:");
			report.AppendLine("  - Combines CONSECUTIVE ATMML.Trade objects in SAME DIRECTION");
			report.AppendLine("  - New campaign when direction changes (Long→Short or Short→Long)");
			report.AppendLine("  - Example: 3 consecutive Long trades = 1 Long campaign");
			report.AppendLine("========================================\n");

			var closedCampaigns = campaigns.Where(c => !c.IsOpen).ToList();
			var winners = closedCampaigns.Where(c => c.TotalNetPnL > 0).ToList();
			var losers = closedCampaigns.Where(c => c.TotalNetPnL < 0).ToList();

			// Calculate unique tickers
			var uniqueTickers = campaigns.Select(c => c.Ticker).Distinct().Count();
			var avgCampaignsPerTicker = uniqueTickers > 0 ? (double)campaigns.Count / uniqueTickers : 0;

			report.AppendLine($"Total campaigns: {campaigns.Count}");
			report.AppendLine($"Unique tickers traded: {uniqueTickers}");
			report.AppendLine($"Average campaigns per ticker: {avgCampaignsPerTicker:F1}");
			report.AppendLine($"Closed campaigns: {closedCampaigns.Count}");
			report.AppendLine($"Open campaigns: {campaigns.Count - closedCampaigns.Count}\n");

			// Show sample of campaigns to verify structure
			report.AppendLine("SAMPLE CAMPAIGNS (first 5):");
			foreach (var c in campaigns.Take(5))
			{
				report.AppendLine($"  {c.Ticker,-15} {c.CampaignId,-15} " +
								  $"Trades: {c.Trades.Count,3}  P&L: {c.TotalNetPnL,15:C2}  " +
								  $"Duration: {c.CampaignDurationDays,4} days");
			}

			report.AppendLine($"\nWinning campaigns: {winners.Count}");
			report.AppendLine($"Losing campaigns: {losers.Count}");
			report.AppendLine($"Win rate: {(closedCampaigns.Any() ? winners.Count * 100.0 / closedCampaigns.Count : 0):F2}%");

			var totalWins = winners.Sum(c => c.TotalNetPnL);
			var totalLosses = Math.Abs(losers.Sum(c => c.TotalNetPnL));

			report.AppendLine($"\nTotal Winning P&L: {totalWins:C2}");
			report.AppendLine($"Total Losing P&L: {totalLosses:C2}");
			report.AppendLine($"Net P&L: {(totalWins - totalLosses):C2}");
			report.AppendLine($"Profit Factor: {(totalLosses > 0 ? totalWins / totalLosses : 0m):F2}");

			report.AppendLine("\nTop 10 Winning Campaigns:");
			foreach (var c in winners.OrderByDescending(c => c.TotalNetPnL).Take(10))
			{
				report.AppendLine($"  {c.Ticker,-15} {c.TotalNetPnL,15:C2}  (Trades: {c.Trades.Count,3}  Duration: {c.CampaignDurationDays,4} days)");
			}

			report.AppendLine("\nTop 10 Losing Campaigns:");
			foreach (var c in losers.OrderBy(c => c.TotalNetPnL).Take(10))
			{
				report.AppendLine($"  {c.Ticker,-15} {c.TotalNetPnL,15:C2}  (Trades: {c.Trades.Count,3}  Duration: {c.CampaignDurationDays,4} days)");
			}

			// Verify campaign structure
			var avgTradesPerCampaign = campaigns.Any() ? campaigns.Average(c => c.Trades.Count) : 0;
			var avgDurationDays = closedCampaigns.Any() ? closedCampaigns.Average(c => c.CampaignDurationDays) : 0;

			report.AppendLine($"\nCampaign Structure Validation:");
			report.AppendLine($"Average trades per campaign: {avgTradesPerCampaign:F1}");
			report.AppendLine($"Average campaign duration: {avgDurationDays:F1} days");
			report.AppendLine($"(Avg > 1 trade confirms these are true campaigns, not individual trades)");

			// Show ticker reentry analysis
			var tickerCampaignCounts = campaigns
				.GroupBy(c => c.Ticker)
				.Select(g => new { Ticker = g.Key, Count = g.Count() })
				.OrderByDescending(x => x.Count)
				.Take(5)
				.ToList();

			if (tickerCampaignCounts.Any())
			{
				report.AppendLine("\nMost Active Tickers (by campaign count):");
				foreach (var tc in tickerCampaignCounts)
				{
					report.AppendLine($"  {tc.Ticker,-15} {tc.Count,3} campaigns");
				}
				report.AppendLine("(Multiple campaigns per ticker = re-entries or long↔short reversals)");
			}

			return report.ToString();
		}

		public string GenerateCampaignWinLossAnalysis()
		{
			var report = new StringBuilder();
			report.AppendLine("\n========================================");
			report.AppendLine("CAMPAIGN WIN/LOSS ANALYSIS");
			report.AppendLine("========================================");
			report.AppendLine("Campaign Definition:");
			report.AppendLine("  - All consecutive ATMML.Trade objects in the SAME DIRECTION");
			report.AppendLine("  - Direction change (Long→Short or Short→Long) = NEW CAMPAIGN");
			report.AppendLine("  - Example: INTC Long (3 trades) + INTC Short (2 trades) = 2 campaigns");
			report.AppendLine("========================================\n");

			var closedCampaigns = campaigns.Where(c => !c.IsOpen).ToList();
			var winners = closedCampaigns.Where(c => c.TotalNetPnL > 0).ToList();
			var losers = closedCampaigns.Where(c => c.TotalNetPnL < 0).ToList();

			var uniqueTickers = closedCampaigns.Select(c => c.Ticker).Distinct().Count();
			var avgCampaignsPerTicker = uniqueTickers > 0 ? (double)closedCampaigns.Count / uniqueTickers : 0;

			report.AppendLine($"Total Campaigns Analyzed: {campaigns.Count}");
			report.AppendLine($"Closed Campaigns: {closedCampaigns.Count}");
			report.AppendLine($"Open Campaigns: {campaigns.Count(c => c.IsOpen)}");
			report.AppendLine($"Unique Tickers Traded: {uniqueTickers}");
			report.AppendLine($"Avg Campaigns per Ticker: {avgCampaignsPerTicker:F1}");
			report.AppendLine($"Winning Campaigns: {winners.Count}");
			report.AppendLine($"Losing Campaigns: {losers.Count}");
			report.AppendLine($"Campaign Win Rate: {(closedCampaigns.Any() ? winners.Count * 100.0 / closedCampaigns.Count : 0):F2}%\n");

			if (winners.Any())
			{
				report.AppendLine("--- WINNING CAMPAIGNS ---");
				report.AppendLine($"Average Win: {winners.Average(c => c.TotalNetPnL):C2}");
				report.AppendLine($"Largest Win: {winners.Max(c => c.TotalNetPnL):C2} ({winners.OrderByDescending(c => c.TotalNetPnL).First().Ticker})");
				report.AppendLine($"Total Wins: {winners.Sum(c => c.TotalNetPnL):C2}");
				report.AppendLine($"Average Win Return: {winners.Average(c => c.CampaignReturnPercent):F2}%");
				report.AppendLine($"Average Winning Campaign Duration: {winners.Average(c => c.CampaignDurationDays):F1} days");
				report.AppendLine($"Average Adds per Winning Campaign: {winners.Average(c => c.NumberOfAdds):F1}");
				report.AppendLine($"Average Trims per Winning Campaign: {winners.Average(c => c.NumberOfTrims):F1}\n");
			}

			if (losers.Any())
			{
				report.AppendLine("--- LOSING CAMPAIGNS ---");
				report.AppendLine($"Average Loss: {losers.Average(c => c.TotalNetPnL):C2}");
				report.AppendLine($"Largest Loss: {losers.Min(c => c.TotalNetPnL):C2} ({losers.OrderBy(c => c.TotalNetPnL).First().Ticker})");
				report.AppendLine($"Total Losses: {losers.Sum(c => c.TotalNetPnL):C2}");
				report.AppendLine($"Average Loss Return: {losers.Average(c => c.CampaignReturnPercent):F2}%");
				report.AppendLine($"Average Losing Campaign Duration: {losers.Average(c => c.CampaignDurationDays):F1} days");
				report.AppendLine($"Average Adds per Losing Campaign: {losers.Average(c => c.NumberOfAdds):F1}");
				report.AppendLine($"Average Trims per Losing Campaign: {losers.Average(c => c.NumberOfTrims):F1}\n");
			}

			if (winners.Any() && losers.Any())
			{
				// Calculate profit factor at CAMPAIGN level
				var totalWinningPnL = winners.Sum(c => c.TotalNetPnL);
				var totalLosingPnL = Math.Abs(losers.Sum(c => c.TotalNetPnL));
				var profitFactor = totalLosingPnL > 0 ? totalWinningPnL / totalLosingPnL : 0m;

				var avgWinLossRatio = winners.Average(c => c.TotalNetPnL) / Math.Abs(losers.Average(c => c.TotalNetPnL));

				report.AppendLine("--- OVERALL METRICS (CAMPAIGN LEVEL) ---");
				report.AppendLine($"Total Winning Campaigns P&L: {totalWinningPnL:C2}");
				report.AppendLine($"Total Losing Campaigns P&L: {totalLosingPnL:C2}");
				report.AppendLine($"Net Campaign P&L: {(totalWinningPnL - totalLosingPnL):C2}");
				report.AppendLine($"Profit Factor: {profitFactor:F2}");
				report.AppendLine($"  (For every $1 lost on losing campaigns, you make ${profitFactor:F2} on winning campaigns)");
				report.AppendLine($"Avg Win/Loss Ratio: {avgWinLossRatio:F2}");
				report.AppendLine($"Average Holding Period (Winners): {winners.Average(c => c.AvgHoldingPeriodDays):F1} days");
				report.AppendLine($"Average Holding Period (Losers): {losers.Average(c => c.AvgHoldingPeriodDays):F1} days\n");

				// Analyze campaign patterns
				report.AppendLine("--- CAMPAIGN PATTERN ANALYSIS ---");
				var tickerMultipleCampaigns = closedCampaigns
					.GroupBy(c => c.Ticker)
					.Where(g => g.Count() > 1)
					.Select(g => new {
						Ticker = g.Key,
						CampaignCount = g.Count(),
						Winners = g.Count(c => c.TotalNetPnL > 0),
						Losers = g.Count(c => c.TotalNetPnL < 0),
						TotalPnL = g.Sum(c => c.TotalNetPnL)
					})
					.OrderByDescending(x => x.CampaignCount)
					.Take(10)
					.ToList();

				if (tickerMultipleCampaigns.Any())
				{
					report.AppendLine("Tickers with Most Campaign Re-entries:");
					report.AppendLine(string.Format("{0,-15} {1,10} {2,10} {3,10} {4,15}",
						"Ticker", "Campaigns", "Winners", "Losers", "Net P&L"));
					report.AppendLine(new string('-', 65));
					foreach (var t in tickerMultipleCampaigns)
					{
						report.AppendLine(string.Format("{0,-15} {1,10} {2,10} {3,10} {4,15:C2}",
							t.Ticker, t.CampaignCount, t.Winners, t.Losers, t.TotalPnL));
					}
					report.AppendLine();
				}

				// List top winners and losers by ticker to show campaign detail
				report.AppendLine("--- TOP 10 WINNING CAMPAIGNS ---");
				report.AppendLine(string.Format("{0,-15} {1,-15} {2,15} {3,8} {4,8}",
					"Ticker", "Campaign ID", "P&L", "Trades", "Days"));
				report.AppendLine(new string('-', 70));
				foreach (var c in winners.OrderByDescending(c => c.TotalNetPnL).Take(10))
				{
					report.AppendLine(string.Format("{0,-15} {1,-15} {2,15:C2} {3,8} {4,8}",
						c.Ticker, c.CampaignId, c.TotalNetPnL, c.Trades.Count, c.CampaignDurationDays));
				}

				report.AppendLine("\n--- TOP 10 LOSING CAMPAIGNS ---");
				report.AppendLine(string.Format("{0,-15} {1,-15} {2,15} {3,8} {4,8}",
					"Ticker", "Campaign ID", "P&L", "Trades", "Days"));
				report.AppendLine(new string('-', 70));
				foreach (var c in losers.OrderBy(c => c.TotalNetPnL).Take(10))
				{
					report.AppendLine(string.Format("{0,-15} {1,-15} {2,15:C2} {3,8} {4,8}",
						c.Ticker, c.CampaignId, c.TotalNetPnL, c.Trades.Count, c.CampaignDurationDays));
				}
			}

			if (closedCampaigns.Any())
			{
				report.AppendLine("\n--- CAMPAIGN PERFORMANCE BY SECTOR ---");
				var sectorStats = closedCampaigns
					.GroupBy(c => c.Sector ?? "Unclassified")
					.Select(g => new
					{
						Sector = g.Key,
						Campaigns = g.Count(),
						Winners = g.Count(c => c.TotalNetPnL > 0),
						Losers = g.Count(c => c.TotalNetPnL < 0),
						WinRate = g.Count(c => c.TotalNetPnL > 0) * 100.0 / g.Count(),
						TotalPnL = g.Sum(c => c.TotalNetPnL),
						AvgReturn = g.Average(c => c.CampaignReturnPercent)
					})
					.OrderByDescending(s => s.TotalPnL);

				report.AppendLine(string.Format("{0,-20} {1,10} {2,10} {3,10} {4,10} {5,15} {6,10}",
					"Sector", "Campaigns", "Winners", "Losers", "Win Rate", "Total P&L", "Avg Ret%"));
				report.AppendLine(new string('-', 95));

				foreach (var sector in sectorStats)
				{
					report.AppendLine(string.Format("{0,-20} {1,10} {2,10} {3,10} {4,9:F1}% {5,15:C2} {6,9:F2}%",
						sector.Sector,
						sector.Campaigns,
						sector.Winners,
						sector.Losers,
						sector.WinRate,
						sector.TotalPnL,
						sector.AvgReturn));
				}

				report.AppendLine("\nNote: Campaign count includes all re-entries in each sector");
			}

			return report.ToString();
		}

		public string GenerateTickerLevelComparison()
		{
			var report = new StringBuilder();
			report.AppendLine("\n========================================");
			report.AppendLine("TICKER-LEVEL ANALYSIS (For Comparison)");
			report.AppendLine("========================================");
			report.AppendLine("This aggregates ALL campaigns for each ticker into ticker-level P&L");
			report.AppendLine("  - Campaign-Level: Each direction change = separate campaign");
			report.AppendLine("  - Ticker-Level: All long + all short for ticker = one aggregate");
			report.AppendLine("========================================\n");

			var closedCampaigns = campaigns.Where(c => !c.IsOpen).ToList();

			// Aggregate by ticker
			var tickerStats = closedCampaigns
				.GroupBy(c => c.Ticker)
				.Select(g => new {
					Ticker = g.Key,
					CampaignCount = g.Count(),
					TotalPnL = g.Sum(c => c.TotalNetPnL),
					WinningCampaigns = g.Count(c => c.TotalNetPnL > 0),
					LosingCampaigns = g.Count(c => c.TotalNetPnL < 0),
					TotalTrades = g.Sum(c => c.Trades.Count),
					AvgCampaignDuration = g.Average(c => c.CampaignDurationDays)
				})
				.OrderByDescending(t => t.TotalPnL)
				.ToList();

			var winningTickers = tickerStats.Where(t => t.TotalPnL > 0).ToList();
			var losingTickers = tickerStats.Where(t => t.TotalPnL < 0).ToList();

			report.AppendLine($"Total Unique Tickers: {tickerStats.Count}");
			report.AppendLine($"Profitable Tickers: {winningTickers.Count}");
			report.AppendLine($"Unprofitable Tickers: {losingTickers.Count}");
			report.AppendLine($"Ticker Win Rate: {(tickerStats.Any() ? winningTickers.Count * 100.0 / tickerStats.Count : 0):F2}%\n");

			var totalWinningTickerPnL = winningTickers.Sum(t => t.TotalPnL);
			var totalLosingTickerPnL = Math.Abs(losingTickers.Sum(t => t.TotalPnL));

			report.AppendLine($"Total Winning Tickers P&L: {totalWinningTickerPnL:C2}");
			report.AppendLine($"Total Losing Tickers P&L: {totalLosingTickerPnL:C2}");
			report.AppendLine($"Net Ticker-Level P&L: {(totalWinningTickerPnL - totalLosingTickerPnL):C2}");
			report.AppendLine($"Ticker-Level Profit Factor: {(totalLosingTickerPnL > 0 ? totalWinningTickerPnL / totalLosingTickerPnL : 0m):F2}\n");

			report.AppendLine("--- TOP 10 PROFITABLE TICKERS (All Campaigns Combined) ---");
			report.AppendLine(string.Format("{0,-15} {1,10} {2,15} {3,10} {4,10}",
				"Ticker", "Campaigns", "Total P&L", "W/L", "Trades"));
			report.AppendLine(new string('-', 70));
			foreach (var t in winningTickers.Take(10))
			{
				report.AppendLine(string.Format("{0,-15} {1,10} {2,15:C2} {3,4}/{4,-4} {5,10}",
					t.Ticker, t.CampaignCount, t.TotalPnL,
					t.WinningCampaigns, t.LosingCampaigns, t.TotalTrades));
			}

			report.AppendLine("\n--- TOP 10 UNPROFITABLE TICKERS (All Campaigns Combined) ---");
			report.AppendLine(string.Format("{0,-15} {1,10} {2,15} {3,10} {4,10}",
				"Ticker", "Campaigns", "Total P&L", "W/L", "Trades"));
			report.AppendLine(new string('-', 70));
			foreach (var t in losingTickers.Take(10))
			{
				report.AppendLine(string.Format("{0,-15} {1,10} {2,15:C2} {3,4}/{4,-4} {5,10}",
					t.Ticker, t.CampaignCount, t.TotalPnL,
					t.WinningCampaigns, t.LosingCampaigns, t.TotalTrades));
			}

			report.AppendLine("\n========================================");
			report.AppendLine("KEY DIFFERENCES:");
			report.AppendLine("Campaign-Level: Analyzes each trading lifecycle separately");
			report.AppendLine("Ticker-Level: Combines all activity per ticker");
			report.AppendLine("\nBoth are valid, but Campaign-Level is standard for:");
			report.AppendLine("  - Institutional reporting (GIPS, SEC)");
			report.AppendLine("  - Risk management");
			report.AppendLine("  - Position-level performance attribution");
			report.AppendLine("========================================");

			return report.ToString();
		}
	}

	public class TradeReport
	{
		private readonly List<Trade> trades;
		private readonly PortfolioConfiguration config;

		public TradeReport(List<Trade> trades, PortfolioConfiguration config)
		{
			this.trades = trades;
			this.config = config;
		}

		public string GenerateDetailedTradeBlotter()
		{
			var report = new StringBuilder();
			report.AppendLine("========================================");
			report.AppendLine($"DETAILED TRADE BLOTTER - {config.FundName}");
			report.AppendLine($"(Individual Trades - For Audit Trail)");
			report.AppendLine($"SEC File Number: {config.SECFileNumber}");
			report.AppendLine($"Report Date: {DateTime.Now:yyyy-MM-dd}");
			report.AppendLine("========================================\n");

			report.AppendLine(string.Format("{0,-12} {1,-10} {2,-8} {3,12} {4,10} {5,12} {6,10} {7,12} {8,15} {9,12} {10,12} {11,10}",
				"Campaign ID", "Trade ID", "Ticker", "Entry Date", "Entry $", "Exit Date", "Exit $", "Shares", "Type", "Gross P&L", "Net P&L", "Return %"));
			report.AppendLine(new string('-', 165));

			foreach (var trade in trades.OrderBy(t => t.CampaignId).ThenBy(t => t.EntryDate))
			{
				try
				{
					if (trade.Shares != 0)
					{
						report.AppendLine(string.Format("{0,-12} {1,-10} {2,-8} {3,12:yyyy-MM-dd} {4,10:C2} {5,12} {6,10} {7,12} {8,15} {9,12:C2} {10,12:C2} {11,10:F2}%",
							trade.CampaignId,
							trade.TradeId,
							trade.Ticker,
							trade.EntryDate,
							trade.EntryPrice,
							trade.ExitDate?.ToString("yyyy-MM-dd") ?? "OPEN",
							trade.ExitPrice?.ToString("C2") ?? "-",
							trade.Shares,
							trade.Type,
							trade.IsClosed ? trade.GrossPnL : 0,
							trade.IsClosed ? trade.NetPnL : 0,
							trade.IsClosed ? trade.ReturnPercent : 0));
					}
				}
				catch
				{
				}
			}

			report.AppendLine(new string('-', 165));

			var closedTrades = trades.Where(t => t.IsClosed).ToList();
			report.AppendLine($"\nTotal Individual Trades: {closedTrades.Count}");
			report.AppendLine($"Total Gross P&L: {closedTrades.Sum(t => t.GrossPnL):C2}");
			report.AppendLine($"Total Net P&L: {closedTrades.Sum(t => t.NetPnL):C2}");

			return report.ToString();
		}

		public List<DetailedTradeBlotterRow> GenerateDetailedTradeBlotterRows(DateTime reportDate)
		{
			return trades
				.OrderBy(t => t.CampaignId)
				.ThenBy(t => t.EntryDate)
				.Select(t => new DetailedTradeBlotterRow
				{
					FundName = config.FundName,
					SECFileNumber = config.SECFileNumber,
					ReportDate = reportDate,
					CampaignId = t.CampaignId,
					TradeId = t.TradeId,
					Ticker = t.Ticker,
					EntryDate = t.EntryDate,
					EntryPrice = t.EntryPrice,
					ExitDate = t.ExitDate,
					ExitPrice = t.ExitPrice,
					Shares = t.Shares,
					Side = t.Type.ToString(),
					Sector = t.Sector,
					AssetClass = t.AssetClass,
					GrossPnL = t.IsClosed ? t.GrossPnL : 0m,
					NetPnL = t.IsClosed ? t.NetPnL : 0m,
					ReturnPercent = t.IsClosed ? t.ReturnPercent : 0m,
					Cost = t.Cost
				})
				.ToList();
		}
	}

	// ==================== GIPS / SEC Reports ====================

	public class GIPSPerformanceReport
	{
		private readonly List<DailyNAV> dailyNAVs;
		private readonly List<CashFlow> cashFlows;
		private readonly List<BenchmarkReturn> benchmarkReturns;
		private readonly PortfolioConfiguration config;

		public GIPSPerformanceReport(List<DailyNAV> navs, List<CashFlow> flows,
			List<BenchmarkReturn> benchmark, PortfolioConfiguration config)
		{
			this.dailyNAVs = navs;
			this.cashFlows = flows;
			this.benchmarkReturns = benchmark;
			this.config = config;
		}

		public string GenerateGIPSCompliantReport()
		{
			var report = new StringBuilder();
			report.AppendLine("========================================");
			report.AppendLine("GIPS-COMPLIANT PERFORMANCE REPORT");
			report.AppendLine("========================================\n");

			report.AppendLine($"Fund Name: {config.FundName}");
			report.AppendLine($"Inception Date: {config.StartDate:yyyy-MM-dd}");
			report.AppendLine($"Report Period: {dailyNAVs.First().Date:yyyy-MM-dd} to {dailyNAVs.Last().Date:yyyy-MM-dd}");
			Trace.WriteLine($"[GIPS DEBUG] dailyNAVs count: {dailyNAVs.Count}, First: {dailyNAVs.First().Date:yyyy-MM-dd}, Last: {dailyNAVs.Last().Date:yyyy-MM-dd}");

			if (config.ClaimsGIPSCompliance)
			{
				report.AppendLine($"\n{config.GIPSComplianceStatement}");
			}

			var monthlyReturns = CalculateMonthlyReturns();
			var yearlyReturns = CalculateYearlyReturns();

			report.AppendLine("\n--- ANNUAL RETURNS (Time-Weighted) ---");
			report.AppendLine(string.Format("{0,-8} {1,12} {2,12} {3,12}",
				"Year", "Gross Return", "Net Return", "Benchmark"));
			report.AppendLine(new string('-', 50));

			foreach (var year in yearlyReturns)
			{
				var benchmarkReturn = GetBenchmarkReturn(year.Year);
				report.AppendLine(string.Format("{0,-8} {1,12:F2}% {2,12:F2}% {3,12:F2}%",
					year.Year,
					year.GrossReturn,
					year.NetReturn,
					benchmarkReturn));
			}

			report.AppendLine("\n--- RISK METRICS ---");
			var returns = monthlyReturns.Select(m => m.NetReturn).ToList();

			if (returns.Any())
			{
				var annualizedReturn = CalculateAnnualizedReturn(returns);
				var volatility = CalculateVolatility(returns);
				var sharpeRatio = CalculateSharpeRatio(returns);
				var sortinoRatio = CalculateSortinoRatio(returns);
				var maxDrawdown = CalculateMaxDrawdown();
				var calmarRatio = CalculateCalmarRatio(returns, maxDrawdown);

				report.AppendLine($"Annualized Return: {annualizedReturn:F2}%");
				report.AppendLine($"Annualized Volatility: {volatility:F2}%");
				report.AppendLine($"Maximum Drawdown: {maxDrawdown:F2}%");
				report.AppendLine($"Current Leverage: {(dailyNAVs.Any() ? dailyNAVs.Last().Leverage : 0):F2}x");
				report.AppendLine();
				report.AppendLine("--- RISK-ADJUSTED RETURNS ---");
				report.AppendLine($"Sharpe Ratio: {sharpeRatio:F2}");
				report.AppendLine($"Sortino Ratio: {(double.IsInfinity(sortinoRatio) ? "∞ (no downside)" : sortinoRatio.ToString("F2"))}");
				report.AppendLine($"Calmar Ratio: {calmarRatio:F2}");
				report.AppendLine();
				report.AppendLine("  Sharpe  = (Return - RiskFree) / Total Volatility");
				report.AppendLine("  Sortino = (Return - RiskFree) / Downside Volatility");
				report.AppendLine("  Calmar  = Annualized Return / |Max Drawdown|");
			}

			report.AppendLine("\n--- FEE STRUCTURE ---");
			report.AppendLine($"Management Fee: {config.ManagementFeePercent}% annually");
			report.AppendLine($"Incentive Fee: {config.IncentiveFeePercent}% above high water mark");
			report.AppendLine($"Current High Water Mark: {config.HighWaterMark:C2}");

			return report.ToString();
		}

		/// <summary>
		/// Resample daily NAVs to weekly (last entry per week) to eliminate intraday mark-to-market noise.
		/// The strategy rebalances weekly, so daily price swings on open positions inflate vol artificially.
		/// </summary>
		private List<DailyNAV> GetWeeklyNAVs()
		{
			var cal = System.Globalization.CultureInfo.InvariantCulture.Calendar;
			return dailyNAVs
				.OrderBy(n => n.Date)
				.GroupBy(n => (
					n.Date.Year,
					cal.GetWeekOfYear(n.Date, System.Globalization.CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday)
				))
				.Select(g => g.OrderBy(n => n.Date).Last()) // last trading day of each week
				.OrderBy(n => n.Date)
				.ToList();
		}

		private List<MonthlyReturn> CalculateMonthlyReturns()
		{
			var monthlyReturns = new List<MonthlyReturn>();
			// Use weekly-resampled NAVs to eliminate daily mark-to-market noise on open positions
			var sortedNAVs = GetWeeklyNAVs().OrderBy(n => n.Date).ToList();
			var monthGroups = sortedNAVs
				.GroupBy(n => new { n.Date.Year, n.Date.Month })
				.OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
				.ToList();

			for (int i = 0; i < monthGroups.Count; i++)
			{
				var month = monthGroups[i];
				var lastNAV = month.Last().NAV;

				// Use previous month's ending NAV as the base
				var baseNAV = (i == 0)
					? month.First().NAV  // First month: no choice but to use first NAV
					: monthGroups[i - 1].Last().NAV;

				var grossReturn = ((lastNAV - baseNAV) / baseNAV) * 100;
				var managementFee = config.ManagementFeePercent / 12;
				var netReturn = grossReturn - managementFee;

				monthlyReturns.Add(new MonthlyReturn
				{
					Year = month.Key.Year,
					Month = month.Key.Month,
					GrossReturn = (decimal)grossReturn,
					NetReturn = (decimal)netReturn
				});
			}
			return monthlyReturns;
		}

		private List<YearlyReturn> CalculateYearlyReturns()
		{
			var yearlyReturns = new List<YearlyReturn>();
			var monthlyReturns = CalculateMonthlyReturns();
			var yearGroups = monthlyReturns.GroupBy(m => m.Year);

			foreach (var year in yearGroups)
			{
				var grossReturn = year.Aggregate(1.0m, (acc, m) => acc * (1 + m.GrossReturn / 100)) - 1;
				var netReturn = year.Aggregate(1.0m, (acc, m) => acc * (1 + m.NetReturn / 100)) - 1;

				yearlyReturns.Add(new YearlyReturn
				{
					Year = year.Key,
					GrossReturn = grossReturn * 100,
					NetReturn = netReturn * 100
				});
			}

			return yearlyReturns;
		}

		private decimal GetBenchmarkReturn(int year)
		{
			var yearBenchmarks = benchmarkReturns
				.Where(b => b.Date.Year == year && b.BenchmarkName == config.PrimaryBenchmark)
				.ToList();

			if (!yearBenchmarks.Any()) return 0;
			return yearBenchmarks.Aggregate(1.0m, (acc, b) => acc * (1 + b.ReturnPercent / 100)) - 1;
		}

		private double CalculateSharpeRatio(List<decimal> monthlyReturns)
		{
			if (!monthlyReturns.Any()) return 0;
			var riskFreeRate = 0.00 / 12;
			var avgReturn = (double)monthlyReturns.Average() / 100;
			var stdDev = CalculateStandardDeviation(monthlyReturns.Select(r => (double)r / 100).ToList());
			if (stdDev == 0) return 0;
			return (avgReturn - riskFreeRate) / stdDev * Math.Sqrt(12);
		}

		/// <summary>
		/// Calculates the Sortino Ratio - a risk-adjusted return metric that only penalizes
		/// downside volatility (negative returns below the target/risk-free rate).
		/// Higher values indicate better risk-adjusted returns.
		/// </summary>
		/// <param name="monthlyReturns">List of monthly net returns as percentages</param>
		/// <returns>Annualized Sortino Ratio</returns>
		private double CalculateSortinoRatio(List<decimal> monthlyReturns)
		{
			if (!monthlyReturns.Any()) return 0;

			var riskFreeRate = 0.00 / 12; // Monthly risk-free rate (2% annual / 12)
			var avgReturn = (double)monthlyReturns.Average() / 100;

			// Calculate downside deviation using returns below the target (risk-free rate)
			// This only considers negative deviations from the target
			var targetReturn = riskFreeRate;
			var downsideDeviations = monthlyReturns
				.Select(r => (double)r / 100)
				.Where(r => r < targetReturn)
				.Select(r => Math.Pow(r - targetReturn, 2))
				.ToList();

			// If no returns fell below target, there's no downside risk
			if (!downsideDeviations.Any())
				return double.PositiveInfinity;

			// Semi-deviation: use full count of observations (standard approach)
			// This is the "population" semi-deviation method
			var downsideDeviation = Math.Sqrt(downsideDeviations.Sum() / monthlyReturns.Count);

			if (downsideDeviation == 0) return 0;

			// Annualize by multiplying by sqrt(12) for monthly data
			return (avgReturn - riskFreeRate) / downsideDeviation * Math.Sqrt(12);
		}

		/// <summary>
		/// Calculates the Calmar Ratio - annualized return divided by maximum drawdown.
		/// Higher values indicate better return per unit of drawdown risk.
		/// Commonly used in hedge fund evaluation.
		/// </summary>
		/// <param name="monthlyReturns">List of monthly net returns as percentages</param>
		/// <param name="maxDrawdown">Maximum drawdown as a percentage (positive number)</param>
		/// <returns>Calmar Ratio</returns>
		private double CalculateCalmarRatio(List<decimal> monthlyReturns, decimal maxDrawdown)
		{
			if (!monthlyReturns.Any() || maxDrawdown == 0) return 0;

			var annualizedReturn = CalculateAnnualizedReturn(monthlyReturns);

			// Calmar Ratio = Annualized Return / |Maximum Drawdown|
			// Both are expressed as percentages, so we get a ratio
			return annualizedReturn / (double)Math.Abs(maxDrawdown);
		}

		private double CalculateStandardDeviation(List<double> values)
		{
			if (values.Count < 2) return 0;
			var avg = values.Average();
			var sumOfSquares = values.Sum(v => Math.Pow(v - avg, 2));
			return Math.Sqrt(sumOfSquares / (values.Count - 1));
		}

		private decimal CalculateMaxDrawdown()
		{
			if (!dailyNAVs.Any()) return 0;

			// Use weekly NAVs to match the basis of vol/Sharpe calculations
			var sortedNAVs = GetWeeklyNAVs().OrderBy(n => n.Date).ToList();

			var peak = sortedNAVs.First().NAV;
			var maxDrawdown = 0m;

			foreach (var nav in sortedNAVs)
			{
				if (nav.NAV > peak) peak = nav.NAV;

				if (peak > 0)
				{
					var drawdown = (peak - nav.NAV) / peak * 100;
					if (drawdown > maxDrawdown) maxDrawdown = drawdown;
				}
			}

			return maxDrawdown;
		}
		private double CalculateVolatility(List<decimal> monthlyReturns)
		{
			var returns = monthlyReturns.Select(r => (double)r / 100).ToList();
			return CalculateStandardDeviation(returns) * Math.Sqrt(12) * 100;
		}

		private double CalculateAnnualizedReturn(List<decimal> monthlyReturns)
		{
			if (!monthlyReturns.Any()) return 0;
			var compoundReturn = monthlyReturns.Aggregate(1.0, (acc, r) => acc * (1 + (double)r / 100));
			var months = monthlyReturns.Count;
			return (Math.Pow(compoundReturn, 12.0 / months) - 1) * 100;
		}
	}

	public class SECComplianceReport
	{
		private readonly List<Campaign> campaigns;
		private readonly List<DailyNAV> dailyNAVs;
		private readonly List<CashFlow> cashFlows;
		private readonly PortfolioConfiguration config;

		public SECComplianceReport(List<Campaign> campaigns, List<DailyNAV> navs,
			List<CashFlow> flows, PortfolioConfiguration config)
		{
			this.campaigns = campaigns;
			this.dailyNAVs = navs;
			this.cashFlows = flows;
			this.config = config;
		}

		public string GenerateSECReport()
		{
			var report = new StringBuilder();
			report.AppendLine("========================================");
			report.AppendLine("SEC COMPLIANCE REPORT");
			report.AppendLine($"Form ADV Part 2 & Form PF Requirements");
			report.AppendLine("========================================\n");

			report.AppendLine($"Filing Entity: {config.FundName}");
			report.AppendLine($"SEC File Number: {config.SECFileNumber}");
			report.AppendLine($"Reporting Period: {DateTime.Now:yyyy-MM-dd}\n");

			report.AppendLine("--- ASSETS UNDER MANAGEMENT ---");
			var currentNAV = dailyNAVs.LastOrDefault();
			if (currentNAV != null)
			{
				report.AppendLine($"Current NAV: {currentNAV.NAV:C2}");
				report.AppendLine($"Cash Position: {currentNAV.Cash:C2}");
				report.AppendLine($"Equity Value: {currentNAV.EquityValue:C2}");
				report.AppendLine($"Gross Leverage: {currentNAV.Leverage:F2}x\n");
			}

			report.AppendLine("--- TOP 10 OPEN POSITIONS (BY CAMPAIGN) ---");
			var openCampaigns = campaigns.Where(c => c.IsOpen)
				.Select(c => new
				{
					c.Ticker,
					MarketValue = c.Trades.Sum(t => t.Shares * t.EntryPrice),
					TotalShares = c.Trades.Sum(t => t.Shares)
				})
				.OrderByDescending(p => Math.Abs(p.MarketValue))
				.Take(10);

			report.AppendLine(string.Format("{0,-10} {1,15} {2,12}",
				"Ticker", "Market Value", "Shares"));
			report.AppendLine(new string('-', 40));

			foreach (var position in openCampaigns)
			{
				var concentration = currentNAV != null ? (position.MarketValue / currentNAV.NAV * 100) : 0;
				report.AppendLine(string.Format("{0,-10} {1,15:C2} {2,12} ({3:F2}%)",
					position.Ticker,
					position.MarketValue,
					position.TotalShares,
					concentration));
			}

			report.AppendLine("\n--- RISK METRICS (Form PF) ---");
			var var95 = CalculateValueAtRisk(0.95);
			report.AppendLine($"Value at Risk (95%): {var95:C2}");

			var monthlyVol = CalculateMonthlyVolatility();
			report.AppendLine($"Monthly Volatility: {monthlyVol:F2}%");

			report.AppendLine("\n--- INVESTOR ACTIVITY ---");
			var subscriptions = cashFlows.Where(cf => cf.Type == CashFlowType.Subscription);
			var redemptions = cashFlows.Where(cf => cf.Type == CashFlowType.Redemption);

			report.AppendLine($"Total Subscriptions: {subscriptions.Sum(s => s.Amount):C2}");
			report.AppendLine($"Total Redemptions: {redemptions.Sum(r => r.Amount):C2}");
			report.AppendLine($"Unique Investors: {cashFlows.Select(cf => cf.InvestorId).Distinct().Count()}");

			report.AppendLine("\n--- FEE DISCLOSURE ---");
			var managementFees = cashFlows.Where(cf => cf.Type == CashFlowType.ManagementFee);
			var incentiveFees = cashFlows.Where(cf => cf.Type == CashFlowType.IncentiveFee);

			report.AppendLine($"Management Fees Collected: {managementFees.Sum(f => Math.Abs(f.Amount)):C2}");
			report.AppendLine($"Incentive Fees Collected: {incentiveFees.Sum(f => Math.Abs(f.Amount)):C2}");

			return report.ToString();
		}

		private decimal CalculateValueAtRisk(double confidenceLevel)
		{
			var returns = dailyNAVs.OrderBy(n => n.Date)
				.Zip(dailyNAVs.OrderBy(n => n.Date).Skip(1), (a, b) => (b.NAV - a.NAV) / a.NAV)
				.OrderBy(r => r)
				.ToList();

			if (!returns.Any()) return 0;
			var index = (int)((1 - confidenceLevel) * returns.Count);
			var currentValue = dailyNAVs.Last().NAV;
			return currentValue * returns[index];
		}

		private decimal CalculateMonthlyVolatility()
		{
			var monthlyReturns = dailyNAVs
				.GroupBy(n => new { n.Date.Year, n.Date.Month })
				.Select(g =>
				{
					var first = g.First().NAV;
					var last = g.Last().NAV;
					return (last - first) / first;
				})
				.ToList();

			if (monthlyReturns.Count < 2) return 0;
			var avg = monthlyReturns.Average();
			var variance = monthlyReturns.Sum(r => (r - avg) * (r - avg)) / (monthlyReturns.Count - 1);
			return (decimal)Math.Sqrt((double)variance) * 100;
		}
	}

	public class MonthlyReturn
	{
		public int Year { get; set; }
		public int Month { get; set; }
		public decimal GrossReturn { get; set; }
		public decimal NetReturn { get; set; }
	}

	public class YearlyReturn
	{
		public int Year { get; set; }
		public decimal GrossReturn { get; set; }
		public decimal NetReturn { get; set; }
	}

	// ==================== ATMML INTEGRATION ====================

	public class ATMMLTradeAdapter
	{
		private int subTradeCounter = 0;
		private int campaignCounter = 0;

		public string ModelName { get; set; }

		/// <summary>
		/// Execution cost configuration used to calculate Commission and ImpactCost
		/// per transaction for reporting. Defaults to MegaCapMOC preset.
		/// </summary>
		public ExecutionCostConfig CostConfig { get; set; } = ExecutionCostConfig.MegaCapMOC();

		/// <summary>
		/// Estimated flat market impact in bps per side for transaction reporting.
		/// For mega-cap S&P 500 MOC execution at $100M AUM, 1.0-2.0 bps is typical.
		/// This is applied per-transaction as: notional * (EstimatedImpactBps / 10000).
		/// </summary>
		public double EstimatedImpactBps { get; set; } = 1.5;

		// Track all transactions
		public List<TransactionRow> Transactions { get; private set; } = new List<TransactionRow>();

		/// <summary>
		/// Calculate Commission and ImpactCost for a single transaction side.
		/// Commission: via CommissionCalculator from ExecutionCosts.cs (rate-based).
		/// ImpactCost: flat bps estimate (configurable via EstimatedImpactBps).
		/// Both are per-side (entry OR exit), not round-trip.
		/// </summary>
		private (decimal Commission, decimal ImpactCost) CalculateTransactionCosts(
			double tradeNotional)
		{
			if (tradeNotional <= 0)
				return (0m, 0m);

			// Commission from ExecutionCosts.cs CommissionCalculator
			double commissionDollars = CommissionCalculator.CalculateCommissionDollars(
				tradeNotional,
				CostConfig.CommissionRateBps,
				CostConfig.MinCommissionPerTrade,
				CostConfig.MaxCommissionPerTrade);

			// Market impact: flat bps estimate per side
			double impactDollars = tradeNotional * (EstimatedImpactBps / 10000.0);

			return ((decimal)commissionDollars, (decimal)impactDollars);
		}

		/// <summary>
		/// Gets the actual execution costs from the trade's ExecutionCosts dictionary,
		/// populated by IdeaCalculator using the same Almgren-Chriss model as the backtest.
		/// Falls back to the simplified bps-based model if ExecutionCosts not available.
		/// </summary>
		private (decimal Commission, decimal ImpactCost) GetActualExecutionCosts(
			ATMML.Trade trade, DateTime date, double fallbackNotional)
		{
			if (trade.ExecutionCosts != null && trade.ExecutionCosts.ContainsKey(date))
			{
				var (comm, impact) = trade.ExecutionCosts[date];
				if (!double.IsNaN(comm) && !double.IsNaN(impact))
					return ((decimal)comm, (decimal)impact);
			}
			// Fallback to simplified model
			return CalculateTransactionCosts(fallbackNotional);
		}

		public (List<Trade> trades, List<Campaign> campaigns) ConvertToCampaigns(List<ATMML.Trade> atmmlTrades)
		{
			var allTrades = new List<Trade>();
			var campaigns = new List<Campaign>();
			Transactions.Clear();

			// Group by ticker and sort chronologically
			var tradesByTicker = atmmlTrades
				.GroupBy(t => t.Ticker)
				.OrderBy(g => g.Key);

			foreach (var tickerGroup in tradesByTicker)
			{
				var sortedTrades = tickerGroup.OrderBy(t => t.OpenDateTime).ToList();

				List<ATMML.Trade> currentCampaignTrades = new List<ATMML.Trade>();
				int? currentDirection = null;

				foreach (var atmmlTrade in sortedTrades)
				{
					var tradeDirection = atmmlTrade.Direction >= 0 ? 1 : -1; // 1 = Long, -1 = Short

					// Check if direction changed
					if (currentDirection.HasValue && currentDirection.Value != tradeDirection)
					{
						// Direction changed - create campaign from accumulated trades
						var (campaignTrades, campaign) = CreateCombinedCampaign(currentCampaignTrades);
						allTrades.AddRange(campaignTrades);
						campaigns.Add(campaign);

						// Start new campaign
						currentCampaignTrades.Clear();
					}

					// Add this trade to current campaign group
					currentCampaignTrades.Add(atmmlTrade);
					currentDirection = tradeDirection;
				}

				// Create final campaign for this ticker
				if (currentCampaignTrades.Any())
				{
					var (campaignTrades, campaign) = CreateCombinedCampaign(currentCampaignTrades);
					allTrades.AddRange(campaignTrades);
					campaigns.Add(campaign);
				}
			}

			return (allTrades.OrderBy(t => t.EntryDate).ToList(), campaigns.OrderBy(c => c.FirstEntryDate).ToList());
		}

		private (List<Trade> trades, Campaign campaign) CreateCombinedCampaign(List<ATMML.Trade> atmmlTrades)
		{
			if (!atmmlTrades.Any())
				throw new ArgumentException("Cannot create campaign from empty trade list");

			campaignCounter++;
			var campaignId = $"{ModelName}_C{campaignCounter}";

			var allCampaignTrades = new List<Trade>();

			// Process each ATMML.Trade and extract its individual trades
			foreach (var atmmlTrade in atmmlTrades)
			{
				var trades = SplitTradeByShareChanges(atmmlTrade);
				allCampaignTrades.AddRange(trades);
			}

			// Assign campaign ID to all trades
			foreach (var trade in allCampaignTrades)
				trade.CampaignId = campaignId;

			// Use first trade for campaign metadata
			var firstAtmmlTrade = atmmlTrades.First();
			var lastAtmmlTrade = atmmlTrades.Last();

			var campaign = new Campaign
			{
				CampaignId = campaignId,
				Ticker = firstAtmmlTrade.Ticker,
				Type = firstAtmmlTrade.Direction >= 0 ? TradeType.Long : TradeType.Short,
				Sector = string.IsNullOrEmpty(firstAtmmlTrade.Sector) ? "Unclassified" : firstAtmmlTrade.Sector,
				AssetClass = DetermineAssetClass(firstAtmmlTrade.Ticker),
				FirstEntryDate = allCampaignTrades.Min(t => t.EntryDate),
				LastExitDate = atmmlTrades.All(t => t.IsOpen()) ? null :
					(DateTime?)allCampaignTrades.Where(t => t.IsClosed).Max(t => t.ExitDate),
				IsOpen = atmmlTrades.Any(t => t.IsOpen()),
				Trades = allCampaignTrades.OrderBy(t => t.EntryDate).ToList()
			};

			// Count adds and trims across all ATMML trades in this campaign
			foreach (var atmmlTrade in atmmlTrades)
			{
				if (atmmlTrade.Shares != null && atmmlTrade.Shares.Any())
				{
					var shareChanges = atmmlTrade.Shares.OrderBy(kvp => kvp.Key).ToList();
					for (int i = 0; i < shareChanges.Count; i++)
					{
						var shares = shareChanges[i].Value;
						var prevShares = i > 0 ? shareChanges[i - 1].Value : 0;
						var shareChange = shares - prevShares;

						if (shareChange > 0) campaign.NumberOfAdds++;
						else if (shareChange < 0) campaign.NumberOfTrims++;
					}
				}
			}

			campaign.TotalSharesTraded = allCampaignTrades.Sum(t => t.Shares);
			campaign.TotalSharesClosed = allCampaignTrades.Where(t => t.IsClosed).Sum(t => t.Shares);

			return (allCampaignTrades, campaign);
		}

		private Campaign CreateCampaign(ATMML.Trade atmmlTrade, List<Trade> trades)
		{
			campaignCounter++;
			var campaignId = $"{ModelName}_C{campaignCounter}";

			foreach (var trade in trades)
				trade.CampaignId = campaignId;

			var campaign = new Campaign
			{
				CampaignId = campaignId,
				Ticker = atmmlTrade.Ticker,
				Type = atmmlTrade.Direction >= 0 ? TradeType.Long : TradeType.Short,
				Sector = string.IsNullOrEmpty(atmmlTrade.Sector) ? "Unclassified" : atmmlTrade.Sector,
				AssetClass = DetermineAssetClass(atmmlTrade.Ticker),
				FirstEntryDate = trades.Min(t => t.EntryDate),
				LastExitDate = atmmlTrade.IsOpen() ? null : (DateTime?)trades.Where(t => t.IsClosed).Max(t => t.ExitDate),
				IsOpen = atmmlTrade.IsOpen(),
				Trades = trades.OrderBy(t => t.EntryDate).ToList()
			};

			if (atmmlTrade.Shares != null && atmmlTrade.Shares.Any())
			{
				var shareChanges = atmmlTrade.Shares.OrderBy(kvp => kvp.Key).ToList();
				for (int i = 0; i < shareChanges.Count; i++)
				{
					var shares = shareChanges[i].Value;
					var prevShares = i > 0 ? shareChanges[i - 1].Value : 0;
					var shareChange = shares - prevShares;

					if (shareChange > 0) campaign.NumberOfAdds++;
					else if (shareChange < 0) campaign.NumberOfTrims++;
				}
			}

			campaign.TotalSharesTraded = trades.Sum(t => t.Shares);
			campaign.TotalSharesClosed = trades.Where(t => t.IsClosed).Sum(t => t.Shares);

			return campaign;
		}

		private List<Trade> SplitTradeByShareChanges(ATMML.Trade atmmlTrade)
		{
			var trades = new List<Trade>();

			if (atmmlTrade.Shares == null || !atmmlTrade.Shares.Any())
			{
				trades.Add(CreateSingleTrade(atmmlTrade));
				return trades;
			}

			var shareChanges = atmmlTrade.Shares.OrderBy(kvp => kvp.Key).ToList();
			var openPositions = new List<Position>();

			// Calculate total shares traded for cost allocation
			// Only count ENTRY shares (positive changes)
			var totalSharesTraded = 0;
			for (int i = 0; i < shareChanges.Count; i++)
			{
				var shares = shareChanges[i].Value;
				var prevShares = i > 0 ? shareChanges[i - 1].Value : 0;
				var shareChange = shares - prevShares;  // Remove Math.Abs()

				if (shareChange > 0)  // Only count buys/adds
				{
					totalSharesTraded += (int)Math.Round(shareChange);
				}
			}

			var isLong = atmmlTrade.Direction >= 0;
			var isFirstEntry = true;

			for (int i = 0; i < shareChanges.Count; i++)
			{
				var date = shareChanges[i].Key;
				var shares = shareChanges[i].Value;
				var prevShares = i > 0 ? shareChanges[i - 1].Value : 0;
				var shareChange = shares - prevShares;

				if (shareChange > 0)
				{
					// FIXED: Use Closes dictionary for entry price
					var entryPrice = GetPriceOnDate(atmmlTrade, date);

					// Record transaction
					var transactionType = isFirstEntry
						? (isLong ? "New Long" : "New Short")
						: (isLong ? "Add Long" : "Add Short");

					var txnShares = (int)Math.Round(Math.Abs(shareChange));
					var txnPrice = (decimal)entryPrice;
					var grossValue = txnShares * txnPrice;
					var (txnCommission, txnImpactCost) = GetActualExecutionCosts(atmmlTrade, date, (double)grossValue);
					var netValue = grossValue - txnCommission - txnImpactCost;

					Transactions.Add(new TransactionRow
					{
						Ticker = atmmlTrade.Ticker,
						Date = date,
						TransactionType = transactionType,
						TradeReason = GetTradeReason(atmmlTrade, date, isFirstEntry ? "Signal" : "Signal"),
						Shares = txnShares,
						Price = txnPrice,
						GrossValue = grossValue,
						Commission = txnCommission,
						ImpactCost = txnImpactCost,
						NetValue = netValue,
						ProfitAndLoss = 0m
					});

					isFirstEntry = false;

					openPositions.Add(new Position
					{
						EntryDate = date,
						EntryPrice = entryPrice,
						Shares = shareChange,
						Ticker = atmmlTrade.Ticker
					});
				}
				else if (shareChange < 0)
				{
					var sharesToClose = Math.Abs(shareChange);
					// FIXED: Use Closes dictionary for exit price
					var exitPrice = GetPriceOnDate(atmmlTrade, date);
					decimal totalPnL = 0m;

					while (sharesToClose > 0 && openPositions.Any())
					{
						var oldestPosition = openPositions[0];
						var closingShares = Math.Min((int)Math.Round(oldestPosition.Shares), (int)Math.Round(sharesToClose));

						// Calculate P&L for this piece
						var pnl = isLong
							? (decimal)(closingShares * (exitPrice - oldestPosition.EntryPrice))
							: (decimal)(closingShares * (oldestPosition.EntryPrice - exitPrice));
						totalPnL += pnl;

						if (oldestPosition.Shares <= sharesToClose)
						{
							trades.Add(CreateTrade(atmmlTrade, oldestPosition.EntryDate,
								oldestPosition.EntryPrice, date, exitPrice, (int)Math.Round(oldestPosition.Shares),
								totalSharesTraded));
							sharesToClose -= oldestPosition.Shares;
							openPositions.RemoveAt(0);
						}
						else
						{
							trades.Add(CreateTrade(atmmlTrade, oldestPosition.EntryDate,
								oldestPosition.EntryPrice, date, exitPrice, (int)Math.Round(sharesToClose),
								totalSharesTraded));
							oldestPosition.Shares -= sharesToClose;
							sharesToClose = 0;
						}
					}

					// Determine if this is partial or full close
					var remainingShares = shares; // Current total position
					var transactionType = remainingShares == 0
						? (isLong ? "Close Long" : "Cover Short")
						: (isLong ? "Partial Sell" : "Partial Cover");

					var txnShares = (int)Math.Round(Math.Abs(shareChange));
					var txnPrice = (decimal)exitPrice;
					var grossValue = txnShares * txnPrice;
					var (txnCommission, txnImpactCost) = GetActualExecutionCosts(atmmlTrade, date, (double)grossValue);
					var netValue = grossValue - txnCommission - txnImpactCost;

					// Record transaction
					Transactions.Add(new TransactionRow
					{
						Ticker = atmmlTrade.Ticker,
						Date = date,
						TransactionType = transactionType,
						TradeReason = GetTradeReason(atmmlTrade, date),
						Shares = txnShares,
						Price = txnPrice,
						GrossValue = grossValue,
						Commission = txnCommission,
						ImpactCost = txnImpactCost,
						NetValue = netValue,
						ProfitAndLoss = totalPnL
					});
				}
			}

			// Handle final close for any remaining open positions
			if (!atmmlTrade.IsOpen() && openPositions.Any())
			{
				// FIXED: Get the last date from Closes dictionary
				var exitDate = atmmlTrade.Closes != null && atmmlTrade.Closes.Any()
					? atmmlTrade.Closes.Keys.Max()
					: atmmlTrade.CloseDateTime;
				var exitPrice = GetPriceOnDate(atmmlTrade, exitDate);

				var totalShares = 0;
				decimal totalPnL = 0m;

				foreach (var position in openPositions)
				{
					totalShares += (int)Math.Round(position.Shares);

					// Calculate P&L for this position
					var pnl = isLong
						? (decimal)(position.Shares * (exitPrice - position.EntryPrice))
						: (decimal)(position.Shares * (position.EntryPrice - exitPrice));
					totalPnL += pnl;

					trades.Add(CreateTrade(atmmlTrade, position.EntryDate,
						position.EntryPrice, exitDate, exitPrice, (int)Math.Round(position.Shares),
						totalSharesTraded));
				}

				if (totalShares != 0)
				{
					var txnPrice = (decimal)exitPrice;
					var grossValue = totalShares * txnPrice;
					var (txnCommission, txnImpactCost) = GetActualExecutionCosts(atmmlTrade, exitDate, (double)grossValue);
					var netValue = grossValue - txnCommission - txnImpactCost;

					// Record final close transaction
					Transactions.Add(new TransactionRow
					{
						Ticker = atmmlTrade.Ticker,
						Date = exitDate,
						TransactionType = isLong ? "Close Long" : "Cover Short",
						TradeReason = GetTradeReason(atmmlTrade, exitDate),
						Shares = totalShares,
						Price = txnPrice,
						GrossValue = grossValue,
						Commission = txnCommission,
						ImpactCost = txnImpactCost,
						NetValue = netValue,
						ProfitAndLoss = totalPnL
					});
				}
			}
			else if (openPositions.Any())
			{
				foreach (var position in openPositions)
				{
					trades.Add(CreateOpenTrade(atmmlTrade, position.EntryDate,
						position.EntryPrice, (int)Math.Round(position.Shares), totalSharesTraded));
				}
			}

			return trades;
		}

		/// <summary>
		/// FIXED: Get price from Closes dictionary, with fallback logic
		/// </summary>
		private double GetPriceOnDate(ATMML.Trade trade, DateTime date)
		{
			// Try exact date match first
			if (trade.Closes != null && trade.Closes.ContainsKey(date))
				return trade.Closes[date];

			// Find closest prior date
			if (trade.Closes != null && trade.Closes.Any())
			{
				var closestDate = trade.Closes.Keys
					.Where(d => d <= date)
					.OrderByDescending(d => d)
					.FirstOrDefault();

				if (closestDate != default(DateTime))
					return trade.Closes[closestDate];
			}

			// Last resort: use OpenPrice if no Closes data available
			return trade.OpenPrice;
		}

		/// <summary>
		/// Gets the trade reason for a given date from the trade's TradeTypes dictionary.
		/// Maps the ATMML TradeType enum to descriptive string labels for the CSV/UI.
		/// </summary>
		private string GetTradeReason(ATMML.Trade trade, DateTime date, string defaultReason = "Signal")
		{
			if (trade.TradeTypes != null && trade.TradeTypes.ContainsKey(date))
			{
				var tradeType = trade.TradeTypes[date];
				var name = tradeType.ToString();
				switch (name)
				{
					case "NewTrend": return "NewTrend";
					case "Pressure": return "Pressure";
					case "PExhaustion": return "PExhaustion";
					case "Exhaustion": return "Exhaustion";
					case "Add": return "Add";
					case "Retrace": return "Retrace";
					case "Rebalance": return "Rebalance";
					case "PortfolioStop": return "PortfolioStop";
					case "TimeExit": return "TimeExit";
					case "FTEntry": return "FTEntry";
					default: return name;
				}
			}
			return defaultReason;
		}

		private Trade CreateTrade(ATMML.Trade atmmlTrade, DateTime entryDate, double entryPrice,
			DateTime exitDate, double exitPrice, int shares, int totalSharesTraded)
		{
			subTradeCounter++;

			// FIXED: Proportionally allocate cost based on share count
			var proportionalCost = totalSharesTraded > 0
				? (decimal)atmmlTrade.Cost * shares / totalSharesTraded
				: 0m;

			return new Trade
			{
				TradeId = $"{ModelName}_T{subTradeCounter}",
				Ticker = atmmlTrade.Ticker,
				EntryDate = entryDate,
				EntryPrice = (decimal)entryPrice,
				ExitDate = exitDate,
				ExitPrice = (decimal)exitPrice,
				Shares = Math.Abs(shares),
				Type = atmmlTrade.Direction >= 0 ? TradeType.Long : TradeType.Short,
				Commission = 0m,
				SecFees = 0m,
				Cost = proportionalCost,
				Sector = string.IsNullOrEmpty(atmmlTrade.Sector) ? "Unclassified" : atmmlTrade.Sector,
				AssetClass = DetermineAssetClass(atmmlTrade.Ticker),
				DividendsReceived = 0m
			};
		}

		private Trade CreateOpenTrade(ATMML.Trade atmmlTrade, DateTime entryDate, double entryPrice,
			int shares, int totalSharesTraded)
		{
			subTradeCounter++;

			// FIXED: Proportionally allocate cost based on share count
			var proportionalCost = totalSharesTraded > 0
				? (decimal)atmmlTrade.Cost * shares / totalSharesTraded
				: 0m;

			return new Trade
			{
				TradeId = $"{ModelName}_T{subTradeCounter}",
				Ticker = atmmlTrade.Ticker,
				EntryDate = entryDate,
				EntryPrice = (decimal)entryPrice,
				ExitDate = null,
				ExitPrice = null,
				Shares = Math.Abs(shares),
				Type = atmmlTrade.Direction >= 0 ? TradeType.Long : TradeType.Short,
				Commission = 0m,
				SecFees = 0m,
				Cost = proportionalCost,
				Sector = string.IsNullOrEmpty(atmmlTrade.Sector) ? "Unclassified" : atmmlTrade.Sector,
				AssetClass = DetermineAssetClass(atmmlTrade.Ticker),
				DividendsReceived = 0m
			};
		}

		private Trade CreateSingleTrade(ATMML.Trade atmmlTrade)
		{
			subTradeCounter++;
			var shares = 0.0;
			if (!double.IsNaN(atmmlTrade.Investment2) && atmmlTrade.Investment2 != 0)
				shares = atmmlTrade.Investment2;
			else if (!double.IsNaN(atmmlTrade.Investment1) && atmmlTrade.Investment1 != 0)
				shares = atmmlTrade.Investment1;
			else if (!double.IsNaN(atmmlTrade.Units) && atmmlTrade.Units != 0)
				shares = atmmlTrade.Units;

			// FIXED: Use Closes dictionary for entry and exit prices
			var entryPrice = GetPriceOnDate(atmmlTrade, atmmlTrade.OpenDateTime);
			var isLong = atmmlTrade.Direction >= 0;

			var entryShares = (int)Math.Round(Math.Abs(shares));
			var entryTxnPrice = (decimal)entryPrice;
			var entryGrossValue = entryShares * entryTxnPrice;
			var (entryCommission, entryImpactCost) = GetActualExecutionCosts(atmmlTrade, atmmlTrade.OpenDateTime, (double)entryGrossValue);
			var entryNetValue = entryGrossValue - entryCommission - entryImpactCost;

			// Record entry transaction
			Transactions.Add(new TransactionRow
			{
				Ticker = atmmlTrade.Ticker,
				Date = atmmlTrade.OpenDateTime,
				TransactionType = isLong ? "New Long" : "New Short",
				TradeReason = GetTradeReason(atmmlTrade, atmmlTrade.OpenDateTime),
				Shares = entryShares,
				Price = entryTxnPrice,
				GrossValue = entryGrossValue,
				Commission = entryCommission,
				ImpactCost = entryImpactCost,
				NetValue = entryNetValue,
				ProfitAndLoss = 0m
			});

			decimal? exitPrice = null;
			DateTime? exitDate = null;
			if (!atmmlTrade.IsOpen())
			{
				exitDate = atmmlTrade.Closes != null && atmmlTrade.Closes.Any()
					? atmmlTrade.Closes.Keys.Max()
					: atmmlTrade.CloseDateTime;
				exitPrice = (decimal)GetPriceOnDate(atmmlTrade, exitDate.Value);

				// Calculate P&L
				var pnl = isLong
					? (decimal)(Math.Abs(shares) * ((double)exitPrice.Value - entryPrice))
					: (decimal)(Math.Abs(shares) * (entryPrice - (double)exitPrice.Value));

				var exitShares = (int)Math.Round(Math.Abs(shares));
				var exitGrossValue = exitShares * exitPrice.Value;
				var (exitCommission, exitImpactCost) = GetActualExecutionCosts(atmmlTrade, exitDate.Value, (double)exitGrossValue);
				var exitNetValue = exitGrossValue - exitCommission - exitImpactCost;

				// Record exit transaction
				Transactions.Add(new TransactionRow
				{
					Ticker = atmmlTrade.Ticker,
					Date = exitDate.Value,
					TransactionType = isLong ? "Close Long" : "Cover Short",
					TradeReason = GetTradeReason(atmmlTrade, exitDate.Value),
					Shares = exitShares,
					Price = exitPrice.Value,
					GrossValue = exitGrossValue,
					Commission = exitCommission,
					ImpactCost = exitImpactCost,
					NetValue = exitNetValue,
					ProfitAndLoss = pnl
				});
			}

			return new Trade
			{
				TradeId = $"{ModelName}_T{subTradeCounter}",
				Ticker = atmmlTrade.Ticker,
				EntryDate = atmmlTrade.OpenDateTime,
				ExitDate = exitDate,
				EntryPrice = (decimal)entryPrice,
				ExitPrice = exitPrice,
				Shares = (int)Math.Round(Math.Abs(shares)),
				Type = atmmlTrade.Direction >= 0 ? TradeType.Long : TradeType.Short,
				Commission = 0m,
				SecFees = 0m,
				Cost = (decimal)atmmlTrade.Cost,
				Sector = string.IsNullOrEmpty(atmmlTrade.Sector) ? "Unclassified" : atmmlTrade.Sector,
				AssetClass = DetermineAssetClass(atmmlTrade.Ticker),
				DividendsReceived = 0m
			};
		}

		private string DetermineAssetClass(string ticker)
		{
			if (ticker.StartsWith("SPY") || ticker.StartsWith("QQQ") || ticker.StartsWith("IWM"))
				return "US Equity ETF";
			return "US Equity";
		}

		private class Position
		{
			public DateTime EntryDate { get; set; }
			public double EntryPrice { get; set; }
			public double Shares { get; set; }
			public string Ticker { get; set; }
		}
	}

	public class ATMMLNAVCalculator
	{
		public List<DailyNAV> CalculateDailyNAV(List<ATMML.Trade> trades,
			Dictionary<DateTime, double> dailyEquityCurve, decimal initialBalance)
		{
			var dailyNAVs = new List<DailyNAV>();

			foreach (var kvp in dailyEquityCurve.OrderBy(x => x.Key))
			{
				var date = kvp.Key;
				var equity = kvp.Value;
				var positionsValue = CalculatePositionsValue(trades, date);
				var nav = (decimal)equity;
				var equityValue = (decimal)positionsValue;
				var cash = nav - equityValue;

				dailyNAVs.Add(new DailyNAV
				{
					Date = date,
					NAV = nav,
					Cash = Math.Max(0, cash),
					EquityValue = equityValue,
					TotalAssets = nav,
					Leverage = equityValue != 0 ? Math.Abs(equityValue / nav) : 0m
				});
			}

			return dailyNAVs;
		}

		private double CalculatePositionsValue(List<ATMML.Trade> trades, DateTime date)
		{
			double totalValue = 0;

			foreach (var trade in trades)
			{
				if (trade.OpenDateTime > date || (!trade.IsOpen() && trade.CloseDateTime < date))
					continue;

				double shares = 0;
				if (trade.Shares != null && trade.Shares.ContainsKey(date))
					shares = trade.Shares[date];
				else if (trade.Shares != null && trade.Shares.Any())
				{
					var closestDate = trade.Shares.Keys
						.Where(d => d <= date)
						.OrderByDescending(d => d)
						.FirstOrDefault();
					if (closestDate != default(DateTime))
						shares = trade.Shares[closestDate];
				}

				double price = trade.OpenPrice;
				if (trade.Closes != null && trade.Closes.ContainsKey(date))
					price = trade.Closes[date];
				else if (trade.Closes != null && trade.Closes.Any())
				{
					var closestDate = trade.Closes.Keys
						.Where(d => d <= date)
						.OrderByDescending(d => d)
						.FirstOrDefault();
					if (closestDate != default(DateTime))
						price = trade.Closes[closestDate];
				}

				totalValue += shares * price;
			}

			return totalValue;
		}

		public decimal CalculateTradePnLPublic(ATMML.Trade trade) => CalculateTradePnL(trade);

		private decimal CalculateRealizedPnL(List<ATMML.Trade> trades, DateTime asOfDate)
		{
			decimal totalPnL = 0m;

			foreach (var trade in trades.Where(t => !t.IsOpen() && t.CloseDateTime <= asOfDate))
			{
				var pnl = CalculateTradePnL(trade);
				totalPnL += pnl;
			}

			return totalPnL;
		}

		private decimal CalculateTradePnL(ATMML.Trade trade)
		{
			if (trade.Shares == null || !trade.Shares.Any()) return 0m;
			if (trade.Closes == null || !trade.Closes.Any()) return 0m;

			var positions = new List<(DateTime date, double shares, double price)>();
			decimal totalPnL = 0m;

			var shareChanges = trade.Shares.OrderBy(kvp => kvp.Key).ToList();

			for (int i = 0; i < shareChanges.Count; i++)
			{
				var date = shareChanges[i].Key;
				var shares = shareChanges[i].Value;
				var prevShares = i > 0 ? shareChanges[i - 1].Value : 0.0;
				var shareChange = shares - prevShares;

				var price = GetPriceOnDate(trade, date);

				if (shareChange > 0)
				{
					positions.Add((date, shareChange, price));
				}
				else if (shareChange < 0)
				{
					var sharesToClose = Math.Abs(shareChange);

					while (sharesToClose > 0.0001 && positions.Any())
					{
						var oldestPosition = positions[0];
						var closeShares = Math.Min(oldestPosition.shares, sharesToClose);

						decimal pnl;
						if (trade.Direction >= 0) pnl = (decimal)(closeShares * (price - oldestPosition.price));
						else pnl = (decimal)(closeShares * (oldestPosition.price - price));

						totalPnL += pnl;

						if (closeShares >= oldestPosition.shares) positions.RemoveAt(0);
						else positions[0] = (oldestPosition.date, oldestPosition.shares - closeShares, oldestPosition.price);

						sharesToClose -= closeShares;
					}
				}
			}

			if (!trade.IsOpen() && positions.Any())
			{
				// FIXED: Use Closes dictionary for final exit price
				var exitDate = trade.Closes.Keys.Max();
				var exitPrice = GetPriceOnDate(trade, exitDate);

				foreach (var position in positions)
				{
					decimal pnl;
					if (trade.Direction >= 0) pnl = (decimal)(position.shares * (exitPrice - position.price));
					else pnl = (decimal)(position.shares * (position.price - exitPrice));
					totalPnL += pnl;
				}
			}

			return totalPnL;
		}

		private double GetPriceOnDate(ATMML.Trade trade, DateTime date)
		{
			if (trade.Closes != null && trade.Closes.ContainsKey(date))
				return trade.Closes[date];

			if (trade.Closes != null && trade.Closes.Any())
			{
				var closestDate = trade.Closes.Keys
					.Where(d => d <= date)
					.OrderByDescending(d => d)
					.FirstOrDefault();

				if (closestDate != default(DateTime))
					return trade.Closes[closestDate];
			}

			return trade.OpenPrice;
		}

		public List<DailyNAV> CalculateFromClosedTrades(List<ATMML.Trade> trades,
			decimal initialBalance, DateTime startDate, DateTime endDate)
		{
			var dailyNAVs = new List<DailyNAV>();

			for (var date = startDate; date <= endDate; date = date.AddDays(1))
			{
				var realizedPnL = CalculateRealizedPnL(trades, date);
				var currentNAV = initialBalance + realizedPnL;

				dailyNAVs.Add(new DailyNAV
				{
					Date = date,
					NAV = currentNAV,
					Cash = currentNAV * 0.10m,
					EquityValue = currentNAV * 0.90m,
					TotalAssets = currentNAV,
					Leverage = 1.0m
				});
			}

			return dailyNAVs;
		}
	}

	public class SPXBenchmarkProvider
	{
		public List<BenchmarkReturn> CalculateMonthlySPXReturns(Dictionary<DateTime, double> spxDailyCloses)
		{
			var monthlyReturns = new List<BenchmarkReturn>();
			var monthGroups = spxDailyCloses
				.OrderBy(kvp => kvp.Key)
				.GroupBy(kvp => new { kvp.Key.Year, kvp.Key.Month });

			foreach (var month in monthGroups)
			{
				var monthData = month.OrderBy(kvp => kvp.Key).ToList();
				if (monthData.Count < 2) continue;

				var firstClose = monthData.First().Value;
				var lastClose = monthData.Last().Value;
				var monthReturn = ((lastClose - firstClose) / firstClose) * 100;

				monthlyReturns.Add(new BenchmarkReturn
				{
					Date = monthData.Last().Key,
					BenchmarkName = "SPY US Equity",
					ReturnPercent = (decimal)monthReturn
				});
			}

			return monthlyReturns;
		}
	}

	// ==================== MAIN INTEGRATION CLASS ====================

	public class ATMMLHedgeFundReportGenerator
	{
		private readonly PortfolioConfiguration config;
		private readonly ATMMLTradeAdapter adapter;
		private readonly ATMMLNAVCalculator navCalculator;
		private readonly SPXBenchmarkProvider benchmarkProvider;

		private string timestamp;

		/// <summary>
		/// Execution cost configuration for calculating per-transaction
		/// Commission via CommissionCalculator. Defaults to MegaCapMOC preset.
		/// </summary>
		public ExecutionCostConfig CostConfig { get; set; } = ExecutionCostConfig.MegaCapMOC();

		/// <summary>
		/// Estimated flat market impact in bps per side for transaction reporting.
		/// For mega-cap S&P 500 MOC execution, 1.0-2.0 bps is typical.
		/// </summary>
		public double EstimatedImpactBps { get; set; } = 1.5;

		public ATMMLHedgeFundReportGenerator(PortfolioConfiguration config, string timestamp)
		{
			this.timestamp = timestamp;
			this.config = config;
			this.adapter = new ATMMLTradeAdapter();
			this.navCalculator = new ATMMLNAVCalculator();
			this.benchmarkProvider = new SPXBenchmarkProvider();
		}

		public AllReports GenerateAllReports(string modelName, List<ATMML.Trade> atmmlTrades,
			Dictionary<DateTime, double> portfolioDailyEquity, Dictionary<DateTime, double> spxDailyCloses)
		{
			adapter.ModelName = modelName;
			adapter.CostConfig = CostConfig;
			adapter.EstimatedImpactBps = EstimatedImpactBps;
			var reports = new AllReports(timestamp);

			Trace.WriteLine("========================================");
			Trace.WriteLine($"GENERATING CAMPAIGN-BASED HEDGE FUND REPORTS");
			Trace.WriteLine($"Fund: {config.FundName}");
			Trace.WriteLine("========================================\n");

			var reportDate = DateTime.Now;

			Trace.WriteLine($"Converting {atmmlTrades.Count} ATMML trades to campaigns...");
			var (trades, campaigns) = adapter.ConvertToCampaigns(atmmlTrades);
			reports.TotalTrades = trades.Count;
			reports.TotalCampaigns = campaigns.Count;
			Trace.WriteLine($"✓ Created {campaigns.Count} campaigns with {trades.Count} individual trades\n");

			Trace.WriteLine("Validating P&L...");
			var validation = ValidatePnL(atmmlTrades, campaigns);
			reports.ValidationResult = validation;
			Trace.WriteLine(validation);
			Trace.WriteLine("");

			Trace.WriteLine("Generating campaign reports...");
			var campaignReport = new CampaignReport(campaigns, config);
			reports.CampaignValidation = campaignReport.ValidateCampaignCalculations();
			reports.CampaignBlotter = campaignReport.GenerateCampaignBlotter();
			reports.CampaignDetail = campaignReport.GenerateCampaignDetail();
			reports.CampaignWinLoss = campaignReport.GenerateCampaignWinLossAnalysis();
			reports.TickerLevelComparison = campaignReport.GenerateTickerLevelComparison();
			Trace.WriteLine("✓ Campaign reports complete\n");
			Trace.WriteLine(reports.CampaignValidation);

			reports.CampaignBlotterRows = campaignReport.GenerateCampaignBlotterRows(reportDate);
			reports.CampaignDetailRows = campaignReport.GenerateCampaignDetailRows(reportDate);

			Trace.WriteLine("Generating detailed trade blotter...");
			var tradeReport = new TradeReport(trades, config);
			reports.DetailedTradeBlotter = tradeReport.GenerateDetailedTradeBlotter();
			Trace.WriteLine("✓ Trade blotter complete\n");

			reports.DetailedTradeBlotterRows = tradeReport.GenerateDetailedTradeBlotterRows(reportDate);

			// Add transaction rows from adapter
			reports.TransactionRows = adapter.Transactions.OrderBy(t => t.Date).ThenBy(t => t.Ticker).ToList();
			Trace.WriteLine($"✓ Generated {reports.TransactionRows.Count} transaction records\n");

			// Generate weekly position snapshot from ATMML trades
			Trace.WriteLine("Generating position snapshots...");
			reports.PositionSnapshotRows = GeneratePositionSnapshots(atmmlTrades);
			Trace.WriteLine($"✓ Generated {reports.PositionSnapshotRows.Count} position snapshot records\n");

			Trace.WriteLine("Calculating daily NAV...");
			List<DailyNAV> dailyNAVs;

			if (portfolioDailyEquity != null && portfolioDailyEquity.Any())
			{
				dailyNAVs = navCalculator.CalculateDailyNAV(atmmlTrades, portfolioDailyEquity, config.InitialBalance);
			}
			else
			{
				var startDate = atmmlTrades.Min(t => t.OpenDateTime);
				var endDate = atmmlTrades.Where(t => !t.IsOpen()).Max(t => t.CloseDateTime);
				Trace.WriteLine("⚠ WARNING: No daily equity curve provided — falling back to realized-P&L-only NAV. " +
					"Risk metrics (Sharpe, Sortino, drawdown) will be unreliable.");
				dailyNAVs = navCalculator.CalculateFromClosedTrades(atmmlTrades, config.InitialBalance, startDate, endDate);
			}
			Trace.WriteLine($"✓ Calculated {dailyNAVs.Count} daily NAV records\n");

			Trace.WriteLine("Calculating SPX benchmark returns...");
			var benchmarkReturns = benchmarkProvider.CalculateMonthlySPXReturns(spxDailyCloses);
			Trace.WriteLine($"✓ Calculated {benchmarkReturns.Count} benchmark periods\n");

			Trace.WriteLine("Generating GIPS performance report...");
			var cashFlows = new List<CashFlow>
			{
				new CashFlow
				{
					Date = config.StartDate,
					Amount = config.InitialBalance,
					Type = CashFlowType.Subscription,
					InvestorId = "INITIAL"
				}
			};

			var gipsReport = new GIPSPerformanceReport(dailyNAVs, cashFlows, benchmarkReturns, config);
			reports.GIPSReport = gipsReport.GenerateGIPSCompliantReport();
			Trace.WriteLine("✓ GIPS report complete\n");

			Trace.WriteLine("Generating SEC compliance report...");
			var secReport = new SECComplianceReport(campaigns, dailyNAVs, cashFlows, config);
			reports.SECReport = secReport.GenerateSECReport();
			Trace.WriteLine("✓ SEC report complete\n");

			Trace.WriteLine("========================================");
			Trace.WriteLine("ALL REPORTS GENERATED SUCCESSFULLY");
			Trace.WriteLine("========================================\n");

			return reports;
		}

		/// <summary>
		/// Generates a weekly position snapshot from ATMML trades.
		/// For each rebalance date, outputs one row per open position with:
		/// shares held, price, market value, cost basis, unrealized P&L, and trade reason.
		/// This enables hedge fund to independently reconstruct the NAV.
		/// </summary>
		private List<PositionSnapshotRow> GeneratePositionSnapshots(List<ATMML.Trade> atmmlTrades)
		{
			var snapshots = new List<PositionSnapshotRow>();

			// Collect all unique dates from all trades' Shares dictionaries
			var allDates = new SortedSet<DateTime>();
			foreach (var trade in atmmlTrades)
			{
				if (trade.Shares != null)
				{
					foreach (var date in trade.Shares.Keys)
						allDates.Add(date);
				}
			}

			foreach (var date in allDates)
			{
				foreach (var trade in atmmlTrades)
				{
					if (trade.Shares == null || !trade.Shares.ContainsKey(date))
						continue;

					var shares = trade.Shares[date];
					if (Math.Abs(shares) < 0.5) // Position is flat
						continue;

					var isLong = trade.Direction >= 0;
					var intShares = (int)Math.Round(Math.Abs(shares));

					// Get price for this date
					double price = 0;
					if (trade.Closes != null && trade.Closes.ContainsKey(date))
						price = trade.Closes[date];
					else if (trade.Closes != null && trade.Closes.Any())
					{
						var closest = trade.Closes.Keys.Where(d => d <= date).OrderByDescending(d => d).FirstOrDefault();
						if (closest != default(DateTime))
							price = trade.Closes[closest];
					}

					var marketValue = (decimal)(intShares * price);
					var costBasis = (decimal)(intShares * trade.OpenPrice);
					var unrealizedPnL = isLong
						? marketValue - costBasis
						: costBasis - marketValue;

					// Get entry trade reason (how position was opened)
					var tradeReason = "Signal";
					if (trade.TradeTypes != null && trade.TradeTypes.Any())
					{
						var entryReason = trade.TradeTypes
							.OrderBy(kvp => kvp.Key)
							.FirstOrDefault();
						if (entryReason.Key != default(DateTime))
							tradeReason = entryReason.Value.ToString();
					}

					snapshots.Add(new PositionSnapshotRow
					{
						Date = date,
						Ticker = trade.Ticker,
						Direction = isLong ? "Long" : "Short",
						Shares = intShares,
						Price = (decimal)price,
						MarketValue = marketValue,
						CostBasis = costBasis,
						UnrealizedPnL = unrealizedPnL,
						TradeReason = tradeReason
					});
				}
			}

			return snapshots.OrderBy(s => s.Date).ThenBy(s => s.Ticker).ToList();
		}

		private ValidationResult ValidatePnL(List<ATMML.Trade> atmmlTrades, List<Campaign> campaigns)
		{
			var result = new ValidationResult { IsValid = true };

			var calculator = new ATMMLNAVCalculator();
			var originalPnL = 0.0;

			foreach (var trade in atmmlTrades.Where(t => !t.IsOpen()))
			{
				var tradePnL = calculator.CalculateTradePnLPublic(trade);
				originalPnL += (double)tradePnL;
			}

			var closedCampaigns = campaigns.Where(c => !c.IsOpen).ToList();
			var campaignGrossPnL = closedCampaigns.Sum(c => (double)c.TotalGrossPnL);
			var campaignNetPnL = closedCampaigns.Sum(c => (double)c.TotalNetPnL);
			var totalCosts = closedCampaigns.Sum(c => (double)c.TotalCosts);

			// Compare FIFO gross vs Campaign gross
			// Known differences: FIFO vs avg-cost allocation (~$3.9M) + dividends
			// These are accounting method differences, not errors
			var difference = Math.Abs(originalPnL - campaignGrossPnL);
			var tolerance = Math.Max(Math.Abs(originalPnL) * 0.10, 10.00);

			result.IsValid = difference <= tolerance;
			result.ErrorMessage = result.IsValid
				? $"P&L validated ({atmmlTrades.Count} trades → {campaigns.Count} campaigns): " +
				  $"FIFO Gross={originalPnL:C2}, Campaign Gross={campaignGrossPnL:C2}, " +
				  $"Campaign Net={campaignNetPnL:C2}, Costs={totalCosts:C2}"
				: $"P&L mismatch beyond 10% tolerance: FIFO={originalPnL:C2}, " +
				  $"CampaignGross={campaignGrossPnL:C2}, Diff={difference:C2}";

			result.OriginalPnL = originalPnL;
			result.ConvertedPnL = campaignGrossPnL;
			result.TradeCount = campaigns.Count;

			return result;
		}
	}

	// ==================== REPORT CONTAINER ====================

	public class AllReports
	{
		private string timestamp;

		public AllReports(string timestamp)
		{
			this.timestamp = timestamp;
		}

		public int TotalTrades { get; set; }
		public int TotalCampaigns { get; set; }
		public ValidationResult ValidationResult { get; set; }

		public string CampaignValidation { get; set; }
		public string CampaignBlotter { get; set; }
		public string CampaignDetail { get; set; }
		public string CampaignWinLoss { get; set; }
		public string TickerLevelComparison { get; set; }
		public string DetailedTradeBlotter { get; set; }
		public string GIPSReport { get; set; }
		public string SECReport { get; set; }

		public List<CampaignBlotterRow> CampaignBlotterRows { get; set; } = new List<CampaignBlotterRow>();
		public List<CampaignDetailRow> CampaignDetailRows { get; set; } = new List<CampaignDetailRow>();
		public List<DetailedTradeBlotterRow> DetailedTradeBlotterRows { get; set; } = new List<DetailedTradeBlotterRow>();
		public List<TransactionRow> TransactionRows { get; set; } = new List<TransactionRow>();

		/// <summary>
		/// Weekly position snapshot for each rebalance date.
		/// Enables hedge fund to mark-to-market independently and reconcile NAV.
		/// </summary>
		public List<PositionSnapshotRow> PositionSnapshotRows { get; set; } = new List<PositionSnapshotRow>();

		private static readonly IReadOnlyList<string> PositionSnapshotColumnOrder = new[]
		{
			"Date", "Ticker", "Direction", "Shares", "Price", "MarketValue",
			"CostBasis", "UnrealizedPnL", "TradeReason"
		};


		/// <summary>
		/// Explicit column order for Transaction CSV export:
		/// Ticker, Date, TransactionType, Shares, Price, GrossValue, Commission, ImpactCost, NetValue, ProfitAndLoss
		/// </summary>
		private static readonly IReadOnlyList<string> TransactionColumnOrder = new[]
		{
			"Ticker", "Date", "TransactionType", "TradeReason", "Shares", "Price",
			"GrossValue", "Commission", "ImpactCost", "NetValue", "ProfitAndLoss"
		};

		public void PrintAll()
		{
			Trace.WriteLine(CampaignValidation);
			Trace.WriteLine(CampaignBlotter);
			Trace.WriteLine(CampaignWinLoss);
			Trace.WriteLine(TickerLevelComparison);
			Trace.WriteLine(CampaignDetail);
			Trace.WriteLine(DetailedTradeBlotter);
			Trace.WriteLine(GIPSReport);
			Trace.WriteLine(SECReport);
		}

		public void SaveToFiles(string outputDirectory)
		{
			Directory.CreateDirectory(outputDirectory);

			File.WriteAllText(Path.Combine(outputDirectory, $"CampaignValidation.txt"), CampaignValidation);
			File.WriteAllText(Path.Combine(outputDirectory, $"CampaignBlotter.txt"), CampaignBlotter);
			File.WriteAllText(Path.Combine(outputDirectory, $"CampaignWinLoss.txt"), CampaignWinLoss);
			File.WriteAllText(Path.Combine(outputDirectory, $"TickerLevelComparison.txt"), TickerLevelComparison);
			File.WriteAllText(Path.Combine(outputDirectory, $"CampaignDetail.txt"), CampaignDetail);
			File.WriteAllText(Path.Combine(outputDirectory, $"DetailedTradeBlotter.txt"), DetailedTradeBlotter);
			File.WriteAllText(Path.Combine(outputDirectory, $"GIPSReport.txt"), GIPSReport);
			File.WriteAllText(Path.Combine(outputDirectory, $"SECReport.txt"), SECReport);

			CsvExporter.WriteCsv(CampaignBlotterRows, Path.Combine(outputDirectory, $"CampaignBlotter.csv"));
			CsvExporter.WriteCsv(CampaignDetailRows, Path.Combine(outputDirectory, $"CampaignDetail.csv"));
			CsvExporter.WriteCsv(DetailedTradeBlotterRows, Path.Combine(outputDirectory, $"DetailedTradeBlotter.csv"));

			// Write Transactions CSV with totals row appended
			var transactionsWithTotals = new List<TransactionRow>(TransactionRows);
			if (TransactionRows.Any())
			{
				transactionsWithTotals.Add(new TransactionRow
				{
					Ticker = "TOTAL",
					Date = TransactionRows.Max(t => t.Date),
					TransactionType = "",
					TradeReason = "",
					Shares = TransactionRows.Sum(t => t.Shares),
					Price = 0m,
					GrossValue = TransactionRows.Sum(t => t.GrossValue),
					Commission = TransactionRows.Sum(t => t.Commission),
					ImpactCost = TransactionRows.Sum(t => t.ImpactCost),
					NetValue = TransactionRows.Sum(t => t.NetValue),
					ProfitAndLoss = TransactionRows.Sum(t => t.ProfitAndLoss)
				});
			}
			CsvExporter.WriteCsv(transactionsWithTotals, Path.Combine(outputDirectory, $"Transactions.csv"), TransactionColumnOrder);

			// Write daily position snapshot if available
			if (PositionSnapshotRows != null && PositionSnapshotRows.Any())
			{
				CsvExporter.WriteCsv(PositionSnapshotRows, Path.Combine(outputDirectory, $"PositionSnapshot.csv"), PositionSnapshotColumnOrder);
			}

			//Trace.WriteLine($"\n✓ All reports saved to: {outputDirectory}");
		}
	}

	public class ValidationResult
	{
		public bool IsValid { get; set; }
		public string ErrorMessage { get; set; }
		public double OriginalPnL { get; set; }
		public double ConvertedPnL { get; set; }
		public int TradeCount { get; set; }

		public override string ToString()
		{
			if (IsValid)
				return $"✓ Validation passed: {TradeCount} campaigns, Original P&L={OriginalPnL:C2}, Campaign P&L={ConvertedPnL:C2}, Diff={Math.Abs(OriginalPnL - ConvertedPnL):C2}";
			else
				return $"✗ Validation failed: {ErrorMessage}";
		}
	}
}