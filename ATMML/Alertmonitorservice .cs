using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Threading;

// Adjust these using statements to match your actual namespaces.
// The service depends on whatever data sources you already have wired up.
// All data-source references are injected via the constructor.

namespace ATMML.Monitoring
{
	/// <summary>
	/// Runs on a DispatcherTimer and re-evaluates every alert condition.
	/// Exposes an ObservableCollection&lt;AlertItem&gt; that the XAML binds to directly.
	/// 
	/// Data sources injected:
	///   IFlexOneSessionState   – FlexOne connectivity
	///   IBloombergSessionState – Bloomberg connectivity / heartbeat
	///   IPortfolioSnapshot     – live position / exposure snapshot
	///   IRiskEngineSnapshot    – beta, VaR, predicted vol
	/// </summary>
	public class AlertMonitorService
	{
		// ── Injected data sources ────────────────────────────────────────────────

		private readonly IFlexOneSessionState _flexOne;
		private readonly IBloombergSessionState _bloomberg;
		private readonly IPortfolioSnapshot _portfolio;
		private readonly IRiskEngineSnapshot _risk;

		// ── Timer ────────────────────────────────────────────────────────────────

		private readonly DispatcherTimer _timer;
		public int PollIntervalSeconds { get; set; } = 30;

		// ── Public collection ────────────────────────────────────────────────────

		public ObservableCollection<AlertItem> Alerts { get; } = new();

		// ── Constructor ──────────────────────────────────────────────────────────

		public AlertMonitorService(
			IFlexOneSessionState flexOne,
			IBloombergSessionState bloomberg,
			IPortfolioSnapshot portfolio,
			IRiskEngineSnapshot risk)
		{
			_flexOne = flexOne;
			_bloomberg = bloomberg;
			_portfolio = portfolio;
			_risk = risk;

			BuildAlerts();

			_timer = new DispatcherTimer
			{
				Interval = TimeSpan.FromSeconds(PollIntervalSeconds)
			};
			_timer.Tick += (_, _) => EvaluateAll();
			_timer.Start();

			// Evaluate immediately on startup so circles are painted before first tick.
			EvaluateAll();
		}

		// ── Alert definitions ────────────────────────────────────────────────────

		private void BuildAlerts()
		{
			Alerts.Add(new AlertItem
			{
				Key = "FlexOne",
				Name = "Flex One",
				Description = "FlexOne OMS session connected and heartbeat active"
			});
			Alerts.Add(new AlertItem
			{
				Key = "Bloomberg",
				Name = "Bloomberg",
				Description = "Bloomberg data feed connected, last tick < 60 s ago"
			});
			Alerts.Add(new AlertItem
			{
				Key = "MktNeutral",
				Name = "Mkt Neutral",
				Description = $"Weighted net beta within ±{AlertThresholds.MaxNetBeta:P0} of zero"
			});
			Alerts.Add(new AlertItem
			{
				Key = "MaxPosition",
				Name = "Max Position",
				Description = $"No single position exceeds {AlertThresholds.MaxPositionWeight:P0} of NAV"
			});
			Alerts.Add(new AlertItem
			{
				Key = "GrossBook",
				Name = "Gross Book",
				Description = $"Total gross exposure ≤ {AlertThresholds.MaxGrossBook:P0} of NAV"
			});
			Alerts.Add(new AlertItem
			{
				Key = "NetExposure",
				Name = "Net Exposure",
				Description = $"|Net exposure| ≤ {AlertThresholds.MaxNetExposure:P0} of NAV"
			});
			Alerts.Add(new AlertItem
			{
				Key = "SectorGross",
				Name = "Sector Gross",
				Description = $"No GICS sector gross > {AlertThresholds.MaxSectorGross:P0} of NAV"
			});
			Alerts.Add(new AlertItem
			{
				Key = "SectorNet",
				Name = "Sector Net",
				Description = $"No GICS sector |net| > {AlertThresholds.MaxSectorNet:P0} of NAV"
			});
			Alerts.Add(new AlertItem
			{
				Key = "IndustryGross",
				Name = "Industry Gross",
				Description = $"No GICS industry gross > {AlertThresholds.MaxIndustryGross:P0} of NAV"
			});
			Alerts.Add(new AlertItem
			{
				Key = "IndustryNet",
				Name = "Industry Net",
				Description = $"No GICS industry |net| > {AlertThresholds.MaxIndustryNet:P0} of NAV"
			});
			Alerts.Add(new AlertItem
			{
				Key = "SubIndGross",
				Name = "Sub Ind Gross",
				Description = $"No GICS sub-industry gross > {AlertThresholds.MaxSubIndustryGross:P0} of NAV"
			});
			Alerts.Add(new AlertItem
			{
				Key = "SubIndNet",
				Name = "Sb Ind Net",
				Description = $"No GICS sub-industry |net| > {AlertThresholds.MaxSubIndustryNet:P0} of NAV"
			});
			Alerts.Add(new AlertItem
			{
				Key = "MaxVaR95",
				Name = "Max VaR95 Position",
				Description = $"Largest position VaR95 ≤ {AlertThresholds.MaxPositionVaR95:P1} of NAV"
			});
			Alerts.Add(new AlertItem
			{
				Key = "MaxPredVol",
				Name = "Max Predicted Vol",
				Description = $"Portfolio predicted annualised vol ≤ {AlertThresholds.MaxPredictedVol:P0}"
			});

			// ── Suggested additional alerts ──────────────────────────────────────
			Alerts.Add(new AlertItem
			{
				Key = "PortfolioVaR",
				Name = "Portfolio VaR",
				Description = $"Portfolio-level VaR95 ≤ {AlertThresholds.MaxPortfolioVaR95:P0} of NAV"
			});
			Alerts.Add(new AlertItem
			{
				Key = "IntradayDD",
				Name = "Intraday DD",
				Description = $"Intraday drawdown from prior close ≤ {AlertThresholds.MaxIntradayDrawdown:P1}"
			});
			Alerts.Add(new AlertItem
			{
				Key = "Deployment",
				Name = "Deployment",
				Description = $"Active positions ≥ {AlertThresholds.MinActivePositions} (both sides deployed)"
			});
			Alerts.Add(new AlertItem
			{
				Key = "BorrowCost",
				Name = "Borrow Cost",
				Description = $"No short borrow rate exceeds {AlertThresholds.MaxBorrowCostBps} bps"
			});
		}

		// ── Evaluation engine ────────────────────────────────────────────────────

		public void EvaluateAll()
		{
			var now = DateTime.Now;
			foreach (var a in Alerts)
			{
				try
				{
					var (healthy, detail) = Evaluate(a.Key);
					a.IsHealthy = healthy;
					a.TooltipDetail = detail;
					a.LastChecked = now;
				}
				catch (Exception ex)
				{
					// If the check itself throws, mark red and surface the error.
					a.IsHealthy = false;
					a.TooltipDetail = $"Evaluation error: {ex.Message}";
					a.LastChecked = now;
				}
			}
		}

		// ── Per-alert condition logic ─────────────────────────────────────────────

		private (bool healthy, string detail) Evaluate(string key)
		{
			switch (key)
			{
				// ── Connectivity ─────────────────────────────────────────────────

				case "FlexOne":
					{
						bool ok = _flexOne.IsConnected
							&& (DateTime.Now - _flexOne.LastHeartbeat).TotalSeconds < AlertThresholds.FlexOneStaleSec;
						string detail = ok
							? $"Connected. Last heartbeat {(DateTime.Now - _flexOne.LastHeartbeat).TotalSeconds:F0}s ago."
							: $"DISCONNECTED or stale. Last heartbeat: {_flexOne.LastHeartbeat:HH:mm:ss}";
						return (ok, detail);
					}

				case "Bloomberg":
					{
						bool ok = _bloomberg.IsConnected
							&& (DateTime.Now - _bloomberg.LastTickTimestamp).TotalSeconds < AlertThresholds.BloombergStaleSec;
						string detail = ok
							? $"Connected. Last tick {(DateTime.Now - _bloomberg.LastTickTimestamp).TotalSeconds:F0}s ago."
							: $"STALE or disconnected. Last tick: {_bloomberg.LastTickTimestamp:HH:mm:ss}";
						return (ok, detail);
					}

				// ── Market neutrality ─────────────────────────────────────────────

				case "MktNeutral":
					{
						double beta = _risk.WeightedNetBeta;
						bool ok = Math.Abs(beta) <= AlertThresholds.MaxNetBeta;
						return (ok, $"Net beta = {beta:+0.0000;-0.0000}  limit = ±{AlertThresholds.MaxNetBeta:F4}");
					}

				// ── Position sizing ───────────────────────────────────────────────

				case "MaxPosition":
					{
						var worst = _portfolio.Positions
							.OrderByDescending(p => Math.Abs(p.WeightOfNAV))
							.FirstOrDefault();
						if (worst == null) return (true, "No positions.");
						bool ok = Math.Abs(worst.WeightOfNAV) <= AlertThresholds.MaxPositionWeight;
						return (ok, $"Largest: {worst.Ticker} = {worst.WeightOfNAV:P2}  limit = {AlertThresholds.MaxPositionWeight:P0}");
					}

				// ── Book-level exposure ───────────────────────────────────────────

				case "GrossBook":
					{
						double gross = _portfolio.GrossExposureOfNAV;
						bool ok = gross <= AlertThresholds.MaxGrossBook;
						return (ok, $"Gross = {gross:P1}  limit = {AlertThresholds.MaxGrossBook:P0}");
					}

				case "NetExposure":
					{
						double net = _portfolio.NetExposureOfNAV;
						bool ok = Math.Abs(net) <= AlertThresholds.MaxNetExposure;
						return (ok, $"Net = {net:+P1;-P1}  limit = ±{AlertThresholds.MaxNetExposure:P0}");
					}

				// ── Sector limits ─────────────────────────────────────────────────

				case "SectorGross":
					{
						var worst = _portfolio.SectorExposures
							.OrderByDescending(s => s.GrossOfNAV)
							.FirstOrDefault();
						if (worst == null) return (true, "No sector data.");
						bool ok = worst.GrossOfNAV <= AlertThresholds.MaxSectorGross;
						return (ok, $"Largest: {worst.Name} gross = {worst.GrossOfNAV:P1}  limit = {AlertThresholds.MaxSectorGross:P0}");
					}

				case "SectorNet":
					{
						var worst = _portfolio.SectorExposures
							.OrderByDescending(s => Math.Abs(s.NetOfNAV))
							.FirstOrDefault();
						if (worst == null) return (true, "No sector data.");
						bool ok = Math.Abs(worst.NetOfNAV) <= AlertThresholds.MaxSectorNet;
						return (ok, $"Largest: {worst.Name} net = {worst.NetOfNAV:+P1;-P1}  limit = ±{AlertThresholds.MaxSectorNet:P0}");
					}

				// ── Industry limits ───────────────────────────────────────────────

				case "IndustryGross":
					{
						var worst = _portfolio.IndustryExposures
							.OrderByDescending(s => s.GrossOfNAV)
							.FirstOrDefault();
						if (worst == null) return (true, "No industry data.");
						bool ok = worst.GrossOfNAV <= AlertThresholds.MaxIndustryGross;
						return (ok, $"Largest: {worst.Name} = {worst.GrossOfNAV:P1}  limit = {AlertThresholds.MaxIndustryGross:P0}");
					}

				case "IndustryNet":
					{
						var worst = _portfolio.IndustryExposures
							.OrderByDescending(s => Math.Abs(s.NetOfNAV))
							.FirstOrDefault();
						if (worst == null) return (true, "No industry data.");
						bool ok = Math.Abs(worst.NetOfNAV) <= AlertThresholds.MaxIndustryNet;
						return (ok, $"Largest: {worst.Name} = {worst.NetOfNAV:+P1;-P1}  limit = ±{AlertThresholds.MaxIndustryNet:P0}");
					}

				// ── Sub-industry limits ───────────────────────────────────────────

				case "SubIndGross":
					{
						var worst = _portfolio.SubIndustryExposures
							.OrderByDescending(s => s.GrossOfNAV)
							.FirstOrDefault();
						if (worst == null) return (true, "No sub-industry data.");
						bool ok = worst.GrossOfNAV <= AlertThresholds.MaxSubIndustryGross;
						return (ok, $"Largest: {worst.Name} = {worst.GrossOfNAV:P1}  limit = {AlertThresholds.MaxSubIndustryGross:P0}");
					}

				case "SubIndNet":
					{
						var worst = _portfolio.SubIndustryExposures
							.OrderByDescending(s => Math.Abs(s.NetOfNAV))
							.FirstOrDefault();
						if (worst == null) return (true, "No sub-industry data.");
						bool ok = Math.Abs(worst.NetOfNAV) <= AlertThresholds.MaxSubIndustryNet;
						return (ok, $"Largest: {worst.Name} = {worst.NetOfNAV:+P1;-P1}  limit = ±{AlertThresholds.MaxSubIndustryNet:P0}");
					}

				// ── Risk metrics ──────────────────────────────────────────────────

				case "MaxVaR95":
					{
						var worst = _portfolio.Positions
							.OrderByDescending(p => p.VaR95OfNAV)
							.FirstOrDefault();
						if (worst == null) return (true, "No VaR data.");
						bool ok = worst.VaR95OfNAV <= AlertThresholds.MaxPositionVaR95;
						return (ok, $"Worst: {worst.Ticker} VaR95 = {worst.VaR95OfNAV:P2}  limit = {AlertThresholds.MaxPositionVaR95:P1}");
					}

				case "MaxPredVol":
					{
						double vol = _risk.PredictedAnnualisedVol;
						bool ok = vol <= AlertThresholds.MaxPredictedVol;
						return (ok, $"Pred vol = {vol:P1}  limit = {AlertThresholds.MaxPredictedVol:P0}");
					}

				// ── Additional suggested alerts ───────────────────────────────────

				case "PortfolioVaR":
					{
						double var95 = _risk.PortfolioVaR95OfNAV;
						bool ok = var95 <= AlertThresholds.MaxPortfolioVaR95;
						return (ok, $"Portfolio VaR95 = {var95:P2}  limit = {AlertThresholds.MaxPortfolioVaR95:P0}");
					}

				case "IntradayDD":
					{
						double dd = _portfolio.IntradayDrawdownOfNAV;   // negative number
						bool ok = Math.Abs(dd) <= AlertThresholds.MaxIntradayDrawdown;
						return (ok, $"Intraday DD = {dd:P2}  limit = -{AlertThresholds.MaxIntradayDrawdown:P1}");
					}

				case "Deployment":
					{
						int count = _portfolio.Positions.Count(p => p.WeightOfNAV != 0);
						bool ok = count >= AlertThresholds.MinActivePositions;
						return (ok, $"Active positions = {count}  minimum = {AlertThresholds.MinActivePositions}");
					}

				case "BorrowCost":
					{
						var worst = _portfolio.Positions
							.Where(p => p.WeightOfNAV < 0)
							.OrderByDescending(p => p.BorrowCostBps)
							.FirstOrDefault();
						if (worst == null) return (true, "No short positions.");
						bool ok = worst.BorrowCostBps <= AlertThresholds.MaxBorrowCostBps;
						return (ok, $"Worst: {worst.Ticker} = {worst.BorrowCostBps:F0} bps  limit = {AlertThresholds.MaxBorrowCostBps:F0} bps");
					}

				default:
					return (true, "No condition defined.");
			}
		}

		// ── Lifecycle ────────────────────────────────────────────────────────────

		public void Stop() => _timer.Stop();

		public void ForceRefresh() => EvaluateAll();
	}
}
