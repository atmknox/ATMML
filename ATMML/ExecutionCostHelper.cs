using System;

namespace ExecutionCosts
{
	public static class ExecutionCostHelper
	{
		/// <summary>
		/// Commission assumption in dollars per share (per SIDE).
		/// Institutional rates: $0.003-$0.005 per share.
		/// Example: 0.005 = half a cent per share for liquid US equities.
		/// </summary>
		public static double CommissionPerShare { get; set; } = 0.003; //standard institutional execution costs for liquid large-cap stocks.

		/// <summary>
		/// Square-root model "Y" coefficient (typical 0.5–1.0).
		/// Industry mid-point = 0.7.
		/// </summary>
		public static double ImpactCoefficientY { get; set; } = 0.10;   //5 bps impact + $0.005/share commission is standard for institutional execution

		/// <summary>
		/// Computes temporary market-impact in basis points using the
		/// square-root model:
		/// impact = 10,000 * Y * sigmaDaily * sqrt(Q / ADV)
		/// </summary>
		public static double ComputeImpactBps(
			double dailyVolatility,      // sigmaDaily (0.02 = 2%)
			double orderShares,          // Q
			double advShares             // average daily volume
		)
		{
			if (advShares <= 0 || orderShares <= 0 || dailyVolatility <= 0)
				return 0.0;

			double participation = orderShares / advShares;
			double rootTerm = Math.Sqrt(participation);
			double impact = 10000.0 * ImpactCoefficientY * dailyVolatility * rootTerm;
			return impact; // in BPS
		}

		/// <summary>
		/// Computes dollar cost of a meta-order.
		/// Returns (commissionDollars, impactBps, totalDollarCost).
		/// </summary>
		public static (double CommissionDollars, double ImpactBps, double TotalCostDollars)
			ComputeExecutionCost(
				double price,
				double shares,
				double portfolioValue,
				double dailyVolatility,
				double advShares
			)
		{
			if (shares == 0 || price <= 0)
				return (0, 0, 0);

			double notional = Math.Abs(shares * price);

			// Commission: per-share pricing (1 side only)
			double commissionDollars = Math.Abs(shares) * CommissionPerShare;

			// Market impact: basis points
			double impactBps = ComputeImpactBps(dailyVolatility, Math.Abs(shares), advShares);
			double impactDollars = notional * (impactBps / 10000.0);

			// Total cost in dollar terms
			double costDollars = commissionDollars + impactDollars;

			return (commissionDollars, impactBps, costDollars);
		}

		/// <summary>
		/// Convert (cost dollars) into a portfolio-level return haircut.
		/// Example: subtract this from expected return in optimizer.
		/// </summary>
		public static double CostAsReturnBps(double costDollars, double portfolioValue)
		{
			if (portfolioValue <= 0)
				return 0;
			return 10000.0 * (costDollars / portfolioValue);
		}
	}
}