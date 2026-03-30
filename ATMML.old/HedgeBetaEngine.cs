using System;
using System.Collections.Generic;
using System.Linq;

namespace ATMML
{
	// -----------------------------------------------------------
	// Stock: (backward compatible) — now supports Beta
	// -----------------------------------------------------------
	public class BetaHedgeStock
	{
		public string Ticker { get; set; } = "";
		public string Sector { get; set; } = "";
		public double Price { get; set; }              // last price
		public double Shares { get; set; }             // positive magnitude
		public bool IsLong { get; set; }               // true = long, false = short

		// Preferred: direct beta vs the same benchmark as SPY.
		// If null, we'll fallback to MarketCorrelation as a coarse proxy.
		public double? Beta { get; set; }              // optional

		// Legacy field (kept): used as a beta proxy if Beta is null.
		public double MarketCorrelation { get; set; }  // 0..1, optional
	}

	// -----------------------------------------------------------
	// HedgeInput: parameters for beta-neutral sizing
	// -----------------------------------------------------------
	public class BetaHedgeInput
	{
		public double MarketPrice { get; set; }    // SPY price (benchmark instrument)
		public double HedgeBeta { get; set; } = 1.0; // beta of hedge instrument vs benchmark (SPY ≈ 1)

		// Safety rails / ergonomics:
		public double MaxHedgeAsGrossPct { get; set; } = 1.10;  // allow hedge up to 110% of gross (notional clamp)
		public double BetaTolerance { get; set; } = 0.01;       // residual beta tolerance in "beta dollars" / $ (see notes)
	}

	// -----------------------------------------------------------
	// HedgeResult: beta-neutral outcome
	// -----------------------------------------------------------
	public class BetaHedgeResult
	{
		// Direction convention: +1 = Buy SPY, -1 = Short SPY, 0 = none
		public int HedgeDirection { get; set; }
		public string Interpretation { get; set; } = "Beta-Neutral";
		public double HedgeNotional { get; set; }             // $ notional to trade in SPY (signed)
		public int SpyShares { get; set; }                    // signed SPY shares

		// Diagnostics
		public double PortfolioBetaExposure { get; set; }     // Sum_i beta_i * signedValue_i  (in $-beta)
		public double HedgeBetaExposure { get; set; }         // beta_H * HedgeNotional       (in $-beta)
		public double BetaAfter { get; set; }                 // (PortfolioBetaExposure + HedgeBetaExposure) / PortfolioValue
		public double PortfolioValue { get; set; }            // signed net value
		public double PortfolioGross { get; set; }            // gross notional (|longs|+|shorts|)
	}

	// -----------------------------------------------------------
	// BetaHedgeEngine: now beta-neutral
	// -----------------------------------------------------------
	public class BetaHedgeEngine
	{
		private readonly double _spyPrice;
		private readonly double _contractSize; // kept for compatibility; =1 for SPY
		public bool Debug { get; set; } = false;

		public BetaHedgeEngine(double spyPrice)
		{
			_spyPrice = Math.Max(1.0, spyPrice);
			_contractSize = 1.0;
		}

		// Compatibility
		public BetaHedgeEngine(double portfolioValue, double spyPrice)
		{
			_spyPrice = Math.Max(1.0, spyPrice);
			_contractSize = 1.0;
		}

		public BetaHedgeEngine(double portfolioValue, double spyPrice, double contractSize)
		{
			_spyPrice = Math.Max(1.0, spyPrice);
			_contractSize = (contractSize <= 0) ? 1.0 : contractSize;
		}

		// -------------------------------------------------------
		// DetermineHedge (beta-neutral):
		//   - Computes beta exposure = Σ beta_i * signedValue_i
		//   - H* = - (beta exposure) / beta_H
		//   - Shares = H* / (price * contractSize)
		//   - Clamped by MaxHedgeAsGrossPct * Gross
		// -------------------------------------------------------
		public BetaHedgeResult DetermineHedge(BetaHedgeInput input, List<BetaHedgeStock> stocks)
		{
			if (stocks == null || stocks.Count == 0)
			{
				return new BetaHedgeResult
				{
					HedgeDirection = 0,
					Interpretation = "No positions"
				};
			}

			double spyPrice = Math.Max(1.0, (input?.MarketPrice ?? _spyPrice));
			double betaHedge = (input?.HedgeBeta == 0.0) ? 1.0 : (input?.HedgeBeta ?? 1.0);
			double betaTol = Math.Max(0.0, input?.BetaTolerance ?? 0.01);
			double grossClampPct = Math.Max(0.0, input?.MaxHedgeAsGrossPct ?? 1.10);

			// Signed values
			//double signed(longSign) => longSign;

			// Portfolio totals
			double grossLongs = stocks.Where(s => s.IsLong).Sum(s => s.Price * s.Shares);
			double grossShorts = stocks.Where(s => !s.IsLong).Sum(s => s.Price * s.Shares);
			double gross = grossLongs + grossShorts; // both positive by construction
			double net = stocks.Sum(s => (s.IsLong ? +1.0 : -1.0) * s.Price * s.Shares);

			// Compute per-stock beta (with safe fallback)
			double GetBeta(BetaHedgeStock s)
			{
				if (s.Beta.HasValue) return s.Beta.Value;
				// Fallback: treat correlation as a rough proxy for beta if no vol ratio available.
				// (You can upgrade to beta ≈ corr * (σ_i/σ_mkt) when you have vols.)
				return Clamp01(s.MarketCorrelation);
			}

			// Portfolio beta exposure in "$-beta" (beta × $ value)
			double betaExposure = stocks.Sum(s =>
			{
				double sign = s.IsLong ? +1.0 : -1.0;
				double value = s.Price * s.Shares * sign;
				return GetBeta(s) * value;
			});

			// Early exit if already within tolerance
			// betaExposure is in "$-beta"; tolerance is relative to gross to scale sensibly.
			double tolDollars = betaTol * Math.Max(1.0, gross);
			if (Math.Abs(betaExposure) <= tolDollars)
			{
				var early = new BetaHedgeResult
				{
					HedgeDirection = 0,
					HedgeNotional = 0,
					SpyShares = 0,
					PortfolioBetaExposure = betaExposure,
					HedgeBetaExposure = 0,
					PortfolioValue = net,
					PortfolioGross = gross
				};
				early.BetaAfter = SafeBetaRatio(early.PortfolioBetaExposure + early.HedgeBetaExposure, gross);
				if (Debug) Dump(early, "Already ~beta-neutral (within tolerance).");
				return early;
			}

			// Ideal hedge notional to neutralize beta:
			// betaExposure + betaHedge * H* = 0  =>  H* = -betaExposure / betaHedge
			double idealHedge = -betaExposure / betaHedge; // signed dollars

			// Clamp hedge notional (risk control)
			double maxHedgeAbs = grossClampPct * gross;
			double hedgeNotional = Math.Clamp(idealHedge, -maxHedgeAbs, +maxHedgeAbs);

			// Convert to shares (signed). For SPY, contractSize = 1
			int spyShares = (int)Math.Round(hedgeNotional / (spyPrice * _contractSize));

			// If rounding wipes out the effect, keep direction with at least 1 share when material
			if (spyShares == 0 && Math.Abs(hedgeNotional) > spyPrice)
			{
				spyShares = (hedgeNotional >= 0) ? +1 : -1;
			}

			// Recompute effective hedge $ after rounding to shares
			double execHedgeNotional = spyShares * spyPrice * _contractSize;

			var res = new BetaHedgeResult
			{
				HedgeDirection = (spyShares > 0) ? +1 : (spyShares < 0 ? -1 : 0),
				Interpretation = "Beta-Neutral",
				HedgeNotional = execHedgeNotional,
				SpyShares = spyShares,
				PortfolioBetaExposure = betaExposure,
				HedgeBetaExposure = betaHedge * execHedgeNotional,
				PortfolioValue = net,
				PortfolioGross = gross
			};
			res.BetaAfter = SafeBetaRatio(res.PortfolioBetaExposure + res.HedgeBetaExposure, gross);

			if (Debug) Dump(res, $"Ideal hedge: {idealHedge:N0}, clamped: {hedgeNotional:N0}, executed: {execHedgeNotional:N0}");
			return res;
		}

		// -------------------------------------------------------
		// Compatibility method expected by your app:
		//  - Returns signed SPY shares (not futures)
		// -------------------------------------------------------
		public int CalculateFuturesContracts(BetaHedgeResult hedge)
		{
			return hedge?.SpyShares ?? 0;
		}

		// ---------------- helpers ----------------
		private static double Clamp01(double x) => (x < 0.0) ? 0.0 : (x > 1.0 ? 1.0 : x);

		private static double SafeBetaRatio(double betaDollars, double gross)
		{
			// Converts $-beta back into a dimensionless sense of "beta after" by normalizing to gross.
			if (gross <= 1e-9) return 0.0;
			return betaDollars / gross;
		}

		private void Dump(BetaHedgeResult r, string msg)
		{
			Console.WriteLine("==== Beta-Neutral Hedge Debug ====");
			Console.WriteLine(msg);
			Console.WriteLine($"Gross:          {r.PortfolioGross:N0}");
			Console.WriteLine($"Net:            {r.PortfolioValue:N0}");
			Console.WriteLine($"β Exposure:     {r.PortfolioBetaExposure:N0}  ($-beta)");
			Console.WriteLine($"Hedge β Exp:    {r.HedgeBetaExposure:N0}     ($-beta)");
			Console.WriteLine($"β After (~):    {r.BetaAfter:0.0000}");
			Console.WriteLine($"SPY Price:      {_spyPrice:N2}");
			Console.WriteLine($"SPY Shares:     {r.SpyShares:N0}   (dir: {(r.SpyShares > 0 ? "+1" : (r.SpyShares < 0 ? "-1" : "0"))})");
			Console.WriteLine($"Hedge Notional: {r.HedgeNotional:N0}");
			Console.WriteLine("==================================");
		}
	}
}
