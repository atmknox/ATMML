namespace ATMML.Monitoring
{
	/// <summary>
	/// Centralised threshold configuration for the alert monitor.
	/// All values expressed as decimals of NAV unless noted.
	/// </summary>
	public static class AlertThresholds
	{
		// ── Connectivity ─────────────────────────────────────────────────────────

		/// <summary>Seconds since last Bloomberg heartbeat before declaring stale.</summary>
		public static int BloombergStaleSec { get; set; } = 60;

		/// <summary>Seconds since last FlexOne heartbeat before declaring disconnected.</summary>
		public static int FlexOneStaleSec { get; set; } = 30;

		// ── Market Neutrality ────────────────────────────────────────────────────

		/// <summary>Maximum |weighted net beta| before flagging non-neutral (e.g. 0.05).</summary>
		public static double MaxNetBeta { get; set; } = 0.05;

		// ── Position Sizing ──────────────────────────────────────────────────────

		/// <summary>Maximum single-position gross weight as fraction of NAV (e.g. 0.05 = 5%).</summary>
		public static double MaxPositionWeight { get; set; } = 0.05;

		// ── Book-Level Exposure ──────────────────────────────────────────────────

		/// <summary>Maximum gross book / NAV (e.g. 2.00 = 200% gross).</summary>
		public static double MaxGrossBook { get; set; } = 2.00;

		/// <summary>Maximum |net exposure| / NAV (e.g. 0.10 = 10%).</summary>
		public static double MaxNetExposure { get; set; } = 0.10;

		// ── Sector Limits ────────────────────────────────────────────────────────

		/// <summary>Maximum gross exposure to any GICS sector / NAV.</summary>
		public static double MaxSectorGross { get; set; } = 0.40;

		/// <summary>Maximum |net exposure| to any GICS sector / NAV.</summary>
		public static double MaxSectorNet { get; set; } = 0.20;

		// ── Industry Limits ──────────────────────────────────────────────────────

		/// <summary>Maximum gross exposure to any GICS industry / NAV.</summary>
		public static double MaxIndustryGross { get; set; } = 0.25;

		/// <summary>Maximum |net exposure| to any GICS industry / NAV.</summary>
		public static double MaxIndustryNet { get; set; } = 0.15;

		// ── Sub-Industry Limits ──────────────────────────────────────────────────

		/// <summary>Maximum gross exposure to any GICS sub-industry / NAV.</summary>
		public static double MaxSubIndustryGross { get; set; } = 0.20;

		/// <summary>Maximum |net exposure| to any GICS sub-industry / NAV.</summary>
		public static double MaxSubIndustryNet { get; set; } = 0.10;

		// ── Risk Metrics ─────────────────────────────────────────────────────────

		/// <summary>Maximum VaR95 for any single position as fraction of NAV.</summary>
		public static double MaxPositionVaR95 { get; set; } = 0.02;

		/// <summary>Maximum predicted annualised portfolio volatility (e.g. 0.15 = 15%).</summary>
		public static double MaxPredictedVol { get; set; } = 0.15;

		// ── Suggested Additional Alerts ──────────────────────────────────────────

		/// <summary>Maximum portfolio-level VaR95 as fraction of NAV.</summary>
		public static double MaxPortfolioVaR95 { get; set; } = 0.05;

		/// <summary>Maximum intraday drawdown from prior close as fraction of NAV.</summary>
		public static double MaxIntradayDrawdown { get; set; } = 0.03;

		/// <summary>Minimum long/short pair count. Fewer than this flags under-deployment.</summary>
		public static int MinActivePositions { get; set; } = 10;

		/// <summary>Maximum |Long-Short correlation| within the book.</summary>
		public static double MaxLSCorrelation { get; set; } = 0.70;

		/// <summary>Minimum cash / NAV to detect over-leverage or settlement failure.</summary>
		public static double MinCashLevel { get; set; } = -0.05;

		/// <summary>Maximum |momentum factor tilt| Z-score.</summary>
		public static double MaxMomentumTilt { get; set; } = 1.50;

		/// <summary>Maximum borrowing cost rate for any single short (bps per annum).</summary>
		public static double MaxBorrowCostBps { get; set; } = 300;
	}
}