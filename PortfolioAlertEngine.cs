using System;
using System.Collections.Generic;
using System.Linq;

namespace ATMML
{
	// ─────────────────────────────────────────────────────────────────────────────
	// PortfolioAlertEngine
	// ─────────────────────────────────────────────────────────────────────────────
	//
	// Pure-function portfolio alert computation. Given a portfolio snapshot and
	// a set of limits, returns the raw alert values + a matching health record
	// (one bool per alert button). No WPF types, no I/O, no singletons — this
	// file depends only on System / System.Collections.Generic / System.Linq so
	// it can be lifted into a Blazor Server build unchanged.
	//
	// Callers (Portfolio_Builder today, Blazor components later):
	//   1. Build an AlertPortfolioInput from their existing data (positions with
	//      dollars/beta/vol/ADV/cap-tier, aggregate long/short dollars, NAV,
	//      session-open NAV, annualised portfolio vol, sector/industry/subInd
	//      net+gross dollar dicts).
	//   2. Call PortfolioAlertEngine.Compute(snap, AlertLimits.Default).
	//   3. Cache the returned AlertComputeResult; have AlertController.Check*
	//      lambdas read AlertHealth.*; have UI label updaters read AlertValues.*.
	//
	// The engine is deterministic and side-effect free. Session-level state
	// (intraday DD baseline, subscription caches, bar cache, market-cap cache)
	// belongs to the caller, not the engine.
	//
	// ─────────────────────────────────────────────────────────────────────────────

	public enum MarketCapTier
	{
		Unknown = 0,
		Large,   // mkt cap > $5B
		Mid,     // $1B < mkt cap <= $5B
		Small    // $0.5B < mkt cap <= $1B
	}

	/// <summary>One open position's contribution to the snapshot.</summary>
	/// <param name="Ticker">Symbol (used for diagnostics only; engine doesn't key off it).</param>
	/// <param name="SignedDollars">Market value × direction (+long / −short).</param>
	/// <param name="Beta">Per-ticker beta (point estimate for the current rebalance date).</param>
	/// <param name="AnnualVol">Annualised volatility in decimal form (e.g. 0.25 = 25%).
	/// Caller divides Symbol.Volatility30D[key] by 100.</param>
	/// <param name="AdvPct">Position shares / 20-day average daily volume (ratio, not %).</param>
	/// <param name="CapTier">Market-cap bucket; Unknown positions are skipped from cap aggregation.</param>
	public sealed record AlertPositionInput(
		string Ticker,
		double SignedDollars,
		double Beta,
		double AnnualVol,
		double AdvPct,
		MarketCapTier CapTier);

	/// <summary>Everything the engine needs for one compute pass.</summary>
	/// <param name="NAV">Current portfolio balance (live mark-to-market).</param>
	/// <param name="OpenNAV">NAV captured at first call of the trading day. 0 ⇒ intraday DD treated as 0.</param>
	/// <param name="LongDollars">Σ|long $| (unsigned).</param>
	/// <param name="ShortDollars">Σ|short $| (unsigned).</param>
	/// <param name="AnnualisedPortfolioVolPercent">Portfolio vol in % units (25.0 = 25%),
	/// matching getAnnVol()'s convention. NaN or ≤0 ⇒ risk analytics emit zeros.</param>
	/// <param name="Positions">Open positions only (not pending rebalance orders).</param>
	/// <param name="SectorNetDollars">Sector → signed net $ (long$ − short$).</param>
	/// <param name="SectorGrossDollars">Sector → gross $ (|long$| + |short$|).</param>
	public sealed record AlertPortfolioInput(
		double NAV,
		double OpenNAV,
		double LongDollars,
		double ShortDollars,
		double AnnualisedPortfolioVolPercent,
		IReadOnlyList<AlertPositionInput> Positions,
		IReadOnlyDictionary<string, double> SectorNetDollars,
		IReadOnlyDictionary<string, double> SectorGrossDollars,
		IReadOnlyDictionary<string, double> IndustryNetDollars,
		IReadOnlyDictionary<string, double> IndustryGrossDollars,
		IReadOnlyDictionary<string, double> SubIndustryNetDollars,
		IReadOnlyDictionary<string, double> SubIndustryGrossDollars);

	/// <summary>Alert thresholds. All values are decimal ratios (0.10 = 10%) unless noted.</summary>
	public sealed record AlertLimits(
		// ── Core exposures ───────────────────────────────────────────────────────
		double NetBeta,           // Mkt Neutral: |net $| / NAV
		double VolNeutral,        // portfolio annualised vol
		double MaxPosition,       // single-name weight cap (strict <)
		double GrossBook,         // (long$ + short$) / NAV
		double NetExposure,       // |net $| / NAV
		double IntradayDD,        // magnitude of max tolerated drawdown (0.03 = -3%)
		double Utilization,       // gross/NAV minimum ("healthy when >=")
		double PredictedVol,      // annualised vol cap

		// ── Risk analytics ───────────────────────────────────────────────────────
		double MaxVaR95,          // portfolio daily VaR95
		double CVaR95,            // portfolio daily CVaR95 (expected shortfall)
		double MVaR95,            // max single-position component VaR as % of NAV
		double IdioRiskMin,       // minimum idiosyncratic fraction ("healthy when >=")
		double EqStress5,         // |PnL| cap at ±5% market move
		double EqStress10,        // |PnL| cap at ±10% market move

		// ── Concentration ────────────────────────────────────────────────────────
		double Top5LongSum, double Top5ShortSum,
		double Top10LongSum, double Top10ShortSum,

		// ── Liquidity ────────────────────────────────────────────────────────────
		double ADV20,             // gross weight of positions > 20% ADV
		double ADV50,             // gross weight of positions > 50% ADV
		double ADV100,            // gross weight of positions > 100% ADV
		double LiqVaR95,          // liquidity-adjusted VaR95

		// ── Market-cap tiers ─────────────────────────────────────────────────────
		double LargeCapGross, double LargeCapNet,
		double MidCapGross,   double MidCapNet,
		double SmallCapGross, double SmallCapNet,

		// ── Sector / Industry / Sub-Industry ─────────────────────────────────────
		double SectorGross,    double SectorNet,
		double IndustryGross,  double IndustryNet,
		double SubIndGross,    double SubIndNet,

		// ── Model constants ──────────────────────────────────────────────────────
		double AssumedMarketVol)  // proxy annualised market vol for idio split
	{
		/// <summary>Current production thresholds (copied verbatim from Timing's _limit* values).</summary>
		public static AlertLimits Default { get; } = new AlertLimits(
			NetBeta:          0.10,
			VolNeutral:       0.12,
			MaxPosition:      0.10,
			GrossBook:        2.00,
			NetExposure:      0.10,
			IntradayDD:       0.03,
			Utilization:      0.50,
			PredictedVol:     0.12,

			MaxVaR95:         0.01,
			CVaR95:           0.015,
			MVaR95:           0.15,
			IdioRiskMin:      0.70,
			EqStress5:        0.02,
			EqStress10:       0.035,

			Top5LongSum:      0.40,
			Top5ShortSum:     0.35,
			Top10LongSum:     0.75,
			Top10ShortSum:    0.65,

			ADV20:            0.30,
			ADV50:            0.10,
			ADV100:           0.00,
			LiqVaR95:         0.02,

			LargeCapGross:    1.75,
			LargeCapNet:      0.15,
			MidCapGross:      1.00,
			MidCapNet:        0.15,
			SmallCapGross:    0.25,
			SmallCapNet:      0.025,

			SectorGross:      2.00,   // 200%
			SectorNet:        0.12,   // 12%
			IndustryGross:    2.00,   // 200%
			IndustryNet:      0.12,   // 12%
			SubIndGross:      1.50,   // 150%
			SubIndNet:        0.12,   // 12%

			AssumedMarketVol: 0.16);  // 16% annualised
	}

	/// <summary>Raw alert values. All ratios are decimals (0.10 = 10%).</summary>
	public sealed record AlertValues(
		// Core
		double GrossBook,
		double NetExposure,            // signed
		double NetBeta,                // |NetExposure|, preserves Timing convention
		double MaxPositionWeight,
		double VolImbalance,
		double Utilization,
		double IntradayDD,             // signed; negative = drawdown
		double PredictedVol,

		// Risk
		double PortfolioVaR95,
		double CVaR95,
		double LiqVaR95,
		double MVaR95Pct,
		double IdioRiskPct,
		double EqStress5,
		double EqStress10,

		// Concentration
		double Top5LongSum, double Top5ShortSum,
		double Top10LongSum, double Top10ShortSum,

		// Liquidity
		double Adv20, double Adv50, double Adv100,

		// Market-cap tiers
		double LargeCapGross, double LargeCapNet,
		double MidCapGross,   double MidCapNet,
		double SmallCapGross, double SmallCapNet,

		// Sector / Industry / Sub-Industry (max across buckets)
		double MaxSectorGross,   double MaxSectorNet,
		double MaxIndustryGross, double MaxIndustryNet,
		double MaxSubIndGross,   double MaxSubIndNet);

	/// <summary>One bool per alert button. True = healthy (green), false = breach (red).</summary>
	public sealed record AlertHealth(
		// Portfolio construction
		bool MktNeutral, bool VolNeutral, bool MaxPosition,

		// Exposure
		bool GrossBook, bool NetExposure, bool IntradayDD, bool Utilization,

		// Risk analytics
		bool MaxVaR95, bool CVaR95, bool MVaR95,
		bool IdioRisk, bool MaxPredVol,
		bool EqStress5, bool EqStress10,

		// Concentration
		bool Top5Long, bool Top5Short, bool Top10Long, bool Top10Short,

		// Liquidity
		bool ADV20, bool ADV50, bool ADV100, bool LiqVaR95,

		// Market-cap tiers
		bool LargeCapGross, bool LargeCapNet,
		bool MidCapGross,   bool MidCapNet,
		bool SmallCapGross, bool SmallCapNet,

		// Sector / Industry / Sub-Industry
		bool SectorGross,   bool SectorNet,
		bool IndustryGross, bool IndustryNet,
		bool SubIndGross,   bool SubIndNet,

		// Meta
		bool DataValid);   // false if NAV<=0 or vol unavailable; IdioRisk passes when false

	public sealed record AlertComputeResult(AlertValues Values, AlertHealth Health);

	public static class PortfolioAlertEngine
	{
		/// <summary>
		/// Single compute pass. Deterministic, side-effect free. Safe to call from
		/// any thread as long as the input snapshot isn't being mutated concurrently.
		/// </summary>
		public static AlertComputeResult Compute(AlertPortfolioInput snap, AlertLimits lim)
		{
			if (snap == null) throw new ArgumentNullException(nameof(snap));
			if (lim  == null) throw new ArgumentNullException(nameof(lim));

			double nav   = snap.NAV;
			bool   navOk = nav > 0;

			// ── Core exposures ────────────────────────────────────────────────
			double grossBook   = navOk ? (snap.LongDollars + snap.ShortDollars) / nav : 0;
			double netExposure = navOk ? (snap.LongDollars - snap.ShortDollars) / nav : 0;
			double utilization = grossBook;   // UCAP = gross/NAV; different threshold direction

			// Intraday drawdown (signed; negative = drawdown)
			double intradayDD = snap.OpenNAV > 0
				? (nav - snap.OpenNAV) / snap.OpenNAV
				: 0;

			// ── Per-position accumulation ────────────────────────────────────
			double maxPosWeight = 0;
			double maxPosVaR95  = 0;

			var longWeights  = new List<double>();
			var shortWeights = new List<double>();

			double adv20DollarSum = 0, adv50DollarSum = 0, adv100DollarSum = 0;
			double liqDaysDollarWeighted = 0;
			double liqDollarTotal        = 0;

			double largeLong = 0, largeShort = 0;
			double midLong   = 0, midShort   = 0;
			double smallLong = 0, smallShort = 0;

			double longBetaDollars  = 0;
			double shortBetaDollars = 0;

			if (navOk && snap.Positions != null)
			{
				foreach (var p in snap.Positions)
				{
					double posDollar = Math.Abs(p.SignedDollars);
					if (posDollar <= 0) continue;

					double w = posDollar / nav;
					if (w > maxPosWeight) maxPosWeight = w;

					int dir = Math.Sign(p.SignedDollars);
					if      (dir > 0) longWeights.Add(w);
					else if (dir < 0) shortWeights.Add(w);

					// Liquidity: ADV buckets + liquidation-day weighting
					double advPct = p.AdvPct;
					if (advPct > 0.20) adv20DollarSum  += posDollar;
					if (advPct > 0.50) adv50DollarSum  += posDollar;
					if (advPct > 1.00) adv100DollarSum += posDollar;
					if (advPct > 0)
					{
						// Days at 20% participation, capped at 20 so one outlier can't
						// swamp the dollar-weighted average.
						double liqDays = Math.Min(20.0, advPct / 0.20);
						liqDaysDollarWeighted += liqDays * posDollar;
						liqDollarTotal        += posDollar;
					}

					// Market-cap tier aggregation (Unknown skipped)
					switch (p.CapTier)
					{
						case MarketCapTier.Large:
							if (dir > 0) largeLong  += posDollar; else largeShort  += posDollar;
							break;
						case MarketCapTier.Mid:
							if (dir > 0) midLong    += posDollar; else midShort    += posDollar;
							break;
						case MarketCapTier.Small:
							if (dir > 0) smallLong  += posDollar; else smallShort  += posDollar;
							break;
					}

					// Per-position VaR95 and beta-dollar accumulators
					if (p.AnnualVol > 0)
					{
						double posVaR95 = 1.645 * p.AnnualVol * w;
						if (posVaR95 > maxPosVaR95) maxPosVaR95 = posVaR95;

						double betaDol = p.Beta * posDollar;
						if      (dir > 0) longBetaDollars  += betaDol;
						else if (dir < 0) shortBetaDollars += betaDol;
					}
				}
			}

			// ── Top-N concentration ───────────────────────────────────────────
			longWeights .Sort((a, b) => b.CompareTo(a));
			shortWeights.Sort((a, b) => b.CompareTo(a));
			double top5Long   = longWeights .Take(5) .Sum();
			double top5Short  = shortWeights.Take(5) .Sum();
			double top10Long  = longWeights .Take(10).Sum();
			double top10Short = shortWeights.Take(10).Sum();

			// ── Liquidity rollups ─────────────────────────────────────────────
			double adv20  = navOk ? adv20DollarSum  / nav : 0;
			double adv50  = navOk ? adv50DollarSum  / nav : 0;
			double adv100 = navOk ? adv100DollarSum / nav : 0;

			// ── Cap-tier exposures ────────────────────────────────────────────
			double largeGross = navOk ? (largeLong + largeShort)          / nav : 0;
			double largeNet   = navOk ? Math.Abs(largeLong - largeShort)  / nav : 0;
			double midGross   = navOk ? (midLong   + midShort)            / nav : 0;
			double midNet     = navOk ? Math.Abs(midLong   - midShort)    / nav : 0;
			double smallGross = navOk ? (smallLong + smallShort)          / nav : 0;
			double smallNet   = navOk ? Math.Abs(smallLong - smallShort)  / nav : 0;

			// ── Risk analytics (only meaningful when predicted vol is available) ──
			double mVaR95Pct    = maxPosVaR95;

			double predictedVol   = 0;
			double portfolioVaR95 = 0;
			double cVaR95         = 0;
			double liqVaR95       = 0;
			double volImbalance   = 0;
			double idioRiskPct    = 1.0;   // default 1.0 = "uncomputed / safe"
			double eqStress5      = 0;
			double eqStress10     = 0;

			double annVolPct = snap.AnnualisedPortfolioVolPercent;
			if (!double.IsNaN(annVolPct) && annVolPct > 0)
			{
				predictedVol   = annVolPct / 100.0;
				portfolioVaR95 = 1.645 * predictedVol / Math.Sqrt(252.0);
				volImbalance   = predictedVol;

				// Normal-distribution CVaR95 approximation:
				//   CVaR = σ · φ(z) / α where φ(1.645)/0.05 ≈ 0.10314 / 0.05
				cVaR95 = predictedVol * 0.10314 / (0.05 * Math.Sqrt(252.0));

				// Liquidity-adjusted VaR: VaR × √(weighted avg liquidation days).
				// Floor at 1 day (can't liquidate faster than same-day).
				double avgLiqDays = liqDollarTotal > 0
					? liqDaysDollarWeighted / liqDollarTotal
					: 0;
				avgLiqDays = Math.Max(1.0, avgLiqDays);
				liqVaR95   = portfolioVaR95 * Math.Sqrt(avgLiqDays);

				// Idiosyncratic risk decomposition.
				// For dollar-neutral L/S strategies, net $ exposure is the better
				// systematic proxy than net beta (vol-neutral construction inflates
				// net beta by pairing high-beta longs with low-beta shorts).
				double systematicVol = Math.Abs(netExposure) * lim.AssumedMarketVol;
				double idioVolSq     = predictedVol * predictedVol - systematicVol * systematicVol;
				double idioVol       = Math.Sqrt(Math.Max(0, idioVolSq));
				idioRiskPct          = predictedVol > 0 ? idioVol / predictedVol : 0;

				// Equity stress: |Σ(β·$ longs) − Σ(β·$ shorts)| × shock / NAV.
				// Beta-neutral books produce ≈0 regardless of vol imbalance.
				if (navOk)
				{
					double netBetaDollars = Math.Abs(longBetaDollars - shortBetaDollars);
					eqStress5  = netBetaDollars * 0.05 / nav;
					eqStress10 = netBetaDollars * 0.10 / nav;
				}
			}

			// ── Sector / Industry / SubInd max ratios ─────────────────────────
			double maxSectorNet     = MaxAbsRatio(snap.SectorNetDollars,        nav);
			double maxSectorGross   = MaxRatio   (snap.SectorGrossDollars,      nav);
			double maxIndustryNet   = MaxAbsRatio(snap.IndustryNetDollars,      nav);
			double maxIndustryGross = MaxRatio   (snap.IndustryGrossDollars,    nav);
			double maxSubIndNet     = MaxAbsRatio(snap.SubIndustryNetDollars,   nav);
			double maxSubIndGross   = MaxRatio   (snap.SubIndustryGrossDollars, nav);

			// Per Timing convention, "NetBeta" alert key reuses net-$ exposure.
			double netBeta = Math.Abs(netExposure);

			var values = new AlertValues(
				GrossBook:         grossBook,
				NetExposure:       netExposure,
				NetBeta:           netBeta,
				MaxPositionWeight: maxPosWeight,
				VolImbalance:      volImbalance,
				Utilization:       utilization,
				IntradayDD:        intradayDD,
				PredictedVol:      predictedVol,

				PortfolioVaR95:    portfolioVaR95,
				CVaR95:            cVaR95,
				LiqVaR95:          liqVaR95,
				MVaR95Pct:         mVaR95Pct,
				IdioRiskPct:       idioRiskPct,
				EqStress5:         eqStress5,
				EqStress10:        eqStress10,

				Top5LongSum:       top5Long,
				Top5ShortSum:      top5Short,
				Top10LongSum:      top10Long,
				Top10ShortSum:     top10Short,

				Adv20:             adv20,
				Adv50:             adv50,
				Adv100:            adv100,

				LargeCapGross:     largeGross,
				LargeCapNet:       largeNet,
				MidCapGross:       midGross,
				MidCapNet:         midNet,
				SmallCapGross:     smallGross,
				SmallCapNet:       smallNet,

				MaxSectorGross:    maxSectorGross,
				MaxSectorNet:      maxSectorNet,
				MaxIndustryGross:  maxIndustryGross,
				MaxIndustryNet:    maxIndustryNet,
				MaxSubIndGross:    maxSubIndGross,
				MaxSubIndNet:      maxSubIndNet);

			// Data is "valid" once we have a positive NAV and a computable vol.
			// IdioRisk short-circuits to healthy when DataValid is false so the
			// first refresh doesn't red-flash before the vol series populates.
			bool dataValid = navOk && predictedVol > 0;

			var health = new AlertHealth(
				// Portfolio construction
				MktNeutral:   netBeta                     <= lim.NetBeta,
				VolNeutral:   volImbalance                <= lim.VolNeutral,
				MaxPosition:  maxPosWeight                <  lim.MaxPosition,   // strict < matches Timing

				// Exposure
				GrossBook:    grossBook                   <= lim.GrossBook,
				NetExposure:  Math.Abs(netExposure)       <= lim.NetExposure,
				IntradayDD:   intradayDD                  >= -lim.IntradayDD,   // DD not worse than -limit
				Utilization:  utilization                 >= lim.Utilization,   // healthy when >= UT

				// Risk analytics
				MaxVaR95:     portfolioVaR95              <= lim.MaxVaR95,
				CVaR95:       cVaR95                      <= lim.CVaR95,
				MVaR95:       mVaR95Pct                   <= lim.MVaR95,
				IdioRisk:     !dataValid || idioRiskPct   >= lim.IdioRiskMin,
				MaxPredVol:   predictedVol                <= lim.PredictedVol,
				EqStress5:    eqStress5                   <= lim.EqStress5,
				EqStress10:   eqStress10                  <= lim.EqStress10,

				// Concentration
				Top5Long:     top5Long                    <= lim.Top5LongSum,
				Top5Short:    top5Short                   <= lim.Top5ShortSum,
				Top10Long:    top10Long                   <= lim.Top10LongSum,
				Top10Short:   top10Short                  <= lim.Top10ShortSum,

				// Liquidity
				ADV20:        adv20                       <= lim.ADV20,
				ADV50:        adv50                       <= lim.ADV50,
				ADV100:       adv100                      <= lim.ADV100,
				LiqVaR95:     liqVaR95                    <= lim.LiqVaR95,

				// Market-cap tiers
				LargeCapGross: largeGross                 <= lim.LargeCapGross,
				LargeCapNet:   largeNet                   <= lim.LargeCapNet,
				MidCapGross:   midGross                   <= lim.MidCapGross,
				MidCapNet:     midNet                     <= lim.MidCapNet,
				SmallCapGross: smallGross                 <= lim.SmallCapGross,
				SmallCapNet:   smallNet                   <= lim.SmallCapNet,

				// Sector / Industry / SubInd
				SectorGross:   maxSectorGross             <= lim.SectorGross,
				SectorNet:     maxSectorNet               <= lim.SectorNet,
				IndustryGross: maxIndustryGross           <= lim.IndustryGross,
				IndustryNet:   maxIndustryNet             <= lim.IndustryNet,
				SubIndGross:   maxSubIndGross             <= lim.SubIndGross,
				SubIndNet:     maxSubIndNet               <= lim.SubIndNet,

				DataValid:     dataValid);

			return new AlertComputeResult(values, health);
		}

		// ── Dictionary helpers ────────────────────────────────────────────────

		/// <summary>Max |value| / NAV across a signed-dollar bucket dict. 0 on empty/null/NAV≤0.</summary>
		private static double MaxAbsRatio(IReadOnlyDictionary<string, double> dict, double nav)
		{
			if (dict == null || dict.Count == 0 || nav <= 0) return 0;
			double max = 0;
			foreach (var v in dict.Values)
			{
				double r = Math.Abs(v) / nav;
				if (r > max) max = r;
			}
			return max;
		}

		/// <summary>Max value / NAV across a gross-dollar bucket dict. 0 on empty/null/NAV≤0.</summary>
		private static double MaxRatio(IReadOnlyDictionary<string, double> dict, double nav)
		{
			if (dict == null || dict.Count == 0 || nav <= 0) return 0;
			double max = 0;
			foreach (var v in dict.Values)
			{
				double r = v / nav;
				if (r > max) max = r;
			}
			return max;
		}
	}
}
