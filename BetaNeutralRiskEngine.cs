// BetaNeutralRiskEngine.cs - Version 7
// Market-neutral, beta-tilted portfolio constructor with comprehensive risk management.
//
// FEATURES:
// - Alpha-weighted sizing (signal strength drives position size)
// - Risk parity sizing (inverse volatility weighting)
// - Market neutrality (dollar neutral)
// - Beta neutrality (optional)
// - Volatility neutrality (optional)
// - Sector/industry/sub-industry concentration limits
//
// FEATURE APPLICATION ORDER (v7):
// 1. Initial Weights (Alpha / RiskParity / Equal)
// 2. Normalize + Per-Name Cap
// 3. Volatility Neutral (if enabled)
// 4. Beta Neutral (if enabled, includes internal sector limits)
// 5. Sector/Industry/Sub-Industry Limits (standalone, only when beta neutral OFF)
// 6. Market Neutral (dollar neutral)
// 7. Risk Governor (VIX, Vol Target, Circuit Breaker, Correlation)
// 8. Final Per-Name Cap (first pass)
// 9. Final Concentration Limits (no normalization after)
// 10. Final Per-Name Cap (second pass)
// 11. Final Market Neutral Re-Check
//
// RISK MANAGEMENT (v5):
// - VIX Stress Detection: ANTICIPATORY - reduces exposure when market fear rises
// - Volatility Targeting: EARLY REACTIVE - scales to maintain target portfolio vol
// - Circuit Breaker: LAST RESORT - state machine with gradual recovery
// - Regime-Aware Recovery: Bidirectional flip detection
//   * Bearish → Bullish: Advance one state immediately
//   * Bullish → Bearish: Regress one state immediately
//
// REGIME SIGNAL SCALE:
// -1.0 = Maximum bearish/defensive
// +1.0 = Maximum bullish/risk-on
//
// FIX HISTORY:
// - v1: Fixed position cap enforcement after normalization
// - v2: Fixed List<double> reference bug in ApplySectorLimits
// - v3: Replaced reactive drawdown governor with predictive risk management
// - v4: Added regime-aware recovery with flip detection
// - v5: Bidirectional regime flip (bearish→bullish advances, bullish→bearish regresses)
// - v6: Fixed sector limit enforcement - NormalizeAndCapToGross was undoing sector caps
//        by rescaling weights back to gross target. Now uses capped targets after sector
//        limits and adds final sector enforcement pass at end of SolveCore with no
//        subsequent normalization.
// - v7: Fixed feature application order in SolveCore:
//        * Eliminated redundant sector limits pass when beta neutral is ON
//          (BetaNeutralizeWithinBooks already applies sector limits internally)
//        * Extracted EnforceMarketNeutrality to reusable method
//        * Added second per-name cap pass after final concentration limits
//        * Added final market neutral re-check after all capping steps
//        * Full 11-step documented ordering ensures all constraints hold at exit

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ATMML.Compliance;

namespace BetaNeutralRiskEngine
{
	public enum BookSide { Long, Short }

	/// <summary>
	/// Circuit breaker state machine states.
	/// </summary>
	public enum CircuitBreakerState
	{
		Normal = 0,      // 100% exposure
		Triggered = 1,   // Minimum exposure (default 10%)
		Recovery1 = 2,   // 25% exposure
		Recovery2 = 3,   // 50% exposure
		Recovery3 = 4    // 75% exposure
	}

	/// <summary>
	/// Direction of regime flip.
	/// </summary>
	public enum RegimeFlipDirection
	{
		None = 0,
		BearishToBullish = 1,   // Advance state
		BullishToBearish = 2    // Regress state
	}

	// ===============================================================
	// STOCK
	// ===============================================================
	public sealed class Stock
	{
		public string GroupId { get; init; } = "";
		public string Ticker { get; init; } = "";
		public string Sector { get; init; } = "";
		public string Industry { get; init; } = "";
		public string SubIndustry { get; init; } = "";
		public double Price { get; init; }
		public double MarketCap { get; init; }
		public double Beta { get; init; }
		public double Volatility { get; init; }
		public BookSide RequiredBook { get; init; }
		public double SignalScore { get; init; } = 1.0;
		public double TargetPrice { get; init; }

		public double ExpectedReturn => Price > 0 && TargetPrice > 0
			? (TargetPrice - Price) / Price
			: 0.0;
	}

	// ===============================================================
	// RISK GOVERNOR CONFIG
	// ===============================================================
	public sealed class RiskGovernorConfig
	{
		// ===========================================================
		// VIX STRESS DETECTION
		// ===========================================================

		public bool EnableVixStressDetection { get; init; } = true;
		public double VixLevel { get; set; } = double.NaN;
		public double VixThreshold1 { get; init; } = 25.0;
		public double VixThreshold2 { get; init; } = 35.0;
		public double VixScale1 { get; init; } = 0.75;
		public double VixScale2 { get; init; } = 0.50;

		// ===========================================================
		// VOLATILITY TARGETING
		// ===========================================================

		public bool EnableVolatilityTargeting { get; init; } = true;
		public double TargetPortfolioVolatility { get; init; } = 0.12;
		public double TrailingVolatility { get; init; } = double.NaN;
		public double MinVolatilityScale { get; init; } = 0.25;
		public double MaxVolatilityScale { get; init; } = 1.25;
		public double VolatilityScaleSmoothingFactor { get; init; } = 0.5;
		public double PreviousVolatilityScale { get; init; } = double.NaN;

		// ===========================================================
		// CIRCUIT BREAKER
		// ===========================================================

		public bool EnableCircuitBreaker { get; init; } = true;
		public double CurrentNav { get; init; } = double.NaN;
		public double PeakNav { get; init; } = double.NaN;
		public double CircuitBreakerDrawdown { get; init; } = 0.15;

		public CircuitBreakerState CurrentCircuitBreakerState { get; init; } = CircuitBreakerState.Normal;
		public int PeriodsInCurrentState { get; init; } = 0;
		public int PeriodsRequiredToAdvance { get; init; } = 2;

		// Exposure scales
		public double ScaleTriggered { get; init; } = 0.10;
		public double ScaleRecovery1 { get; init; } = 0.25;
		public double ScaleRecovery2 { get; init; } = 0.50;
		public double ScaleRecovery3 { get; init; } = 0.75;

		// Advancement thresholds (DD must stay BELOW)
		public double ThresholdToRecovery1 { get; init; } = 0.12;
		public double ThresholdToRecovery2 { get; init; } = 0.08;
		public double ThresholdToRecovery3 { get; init; } = 0.05;
		public double ThresholdToNormal { get; init; } = 0.02;

		// Regression thresholds (DD exceeds = regress)
		public double RegressionToTriggered { get; init; } = 0.15;
		public double RegressionToRecovery1 { get; init; } = 0.12;
		public double RegressionToRecovery2 { get; init; } = 0.08;

		// ===========================================================
		// REGIME-AWARE RECOVERY (Bidirectional)
		// ===========================================================

		/// <summary>
		/// Enable regime-aware circuit breaker transitions.
		/// Regime flips cause immediate state changes:
		/// - Bearish → Bullish: Advance one state
		/// - Bullish → Bearish: Regress one state
		/// </summary>
		public bool EnableRegimeAwareRecovery { get; init; } = true;

		/// <summary>
		/// Current regime signal from ML model.
		/// Scale: -1.0 (bearish) to +1.0 (bullish)
		/// </summary>
		public double RegimeSignal { get; init; } = double.NaN;

		/// <summary>
		/// Previous period's regime signal. CALLER MUST track across rebalances.
		/// </summary>
		public double PreviousRegimeSignal { get; init; } = double.NaN;

		/// <summary>
		/// Threshold for bullish regime. Signal >= this is bullish.
		/// </summary>
		public double RegimeBullishThreshold { get; init; } = 0.30;

		/// <summary>
		/// Threshold for bearish regime. Signal below this is bearish.
		/// </summary>
		public double RegimeBearishThreshold { get; init; } = -0.30;

		/// <summary>
		/// Enable regime flip detection for state transitions.
		/// </summary>
		public bool EnableRegimeFlipTransitions { get; init; } = true;

		/// <summary>
		/// Regime level for immediate CB exit (requires VIX calm + vol normal).
		/// </summary>
		public double RegimeImmediateExitThreshold { get; init; } = 0.70;

		/// <summary>
		/// Max VIX for regime-based immediate exit.
		/// </summary>
		public double RegimeExitMaxVix { get; init; } = 22.0;

		// ===========================================================
		// CORRELATION MONITOR
		// ===========================================================

		public bool EnableCorrelationMonitor { get; init; } = false;
		public double LongShortCorrelation { get; init; } = double.NaN;
		public double CorrelationThreshold { get; init; } = 0.30;
		public double CorrelationBreakdownScale { get; init; } = 0.50;

		// ===========================================================
		// PRESETS
		// ===========================================================

		public static RiskGovernorConfig Default => new RiskGovernorConfig();

		public static RiskGovernorConfig Conservative => new RiskGovernorConfig
		{
			VixThreshold1 = 22.0,
			VixThreshold2 = 30.0,
			VixScale1 = 0.70,
			VixScale2 = 0.40,
			TargetPortfolioVolatility = 0.08,
			MinVolatilityScale = 0.20,
			MaxVolatilityScale = 1.0,
			CircuitBreakerDrawdown = 0.12,
			PeriodsRequiredToAdvance = 3,
		};

		public static RiskGovernorConfig Aggressive => new RiskGovernorConfig
		{
			VixThreshold1 = 30.0,
			VixThreshold2 = 40.0,
			VixScale1 = 0.80,
			VixScale2 = 0.60,
			TargetPortfolioVolatility = 0.15,
			MinVolatilityScale = 0.30,
			MaxVolatilityScale = 1.50,
			CircuitBreakerDrawdown = 0.20,
			PeriodsRequiredToAdvance = 1,
		};

		public static RiskGovernorConfig Disabled => new RiskGovernorConfig
		{
			EnableVixStressDetection = false,
			EnableVolatilityTargeting = false,
			EnableCircuitBreaker = false,
			EnableRegimeAwareRecovery = false,
			EnableCorrelationMonitor = false,
		};

		public void Validate()
		{
			if (EnableVixStressDetection)
			{
				if (VixThreshold1 <= 0 || VixThreshold2 <= 0 || VixThreshold1 >= VixThreshold2)
					throw new ArgumentException("Invalid VIX thresholds.");
			}
			if (EnableVolatilityTargeting && TargetPortfolioVolatility <= 0)
				throw new ArgumentException("TargetPortfolioVolatility must be > 0.");
			if (EnableCircuitBreaker && CircuitBreakerDrawdown <= 0)
				throw new ArgumentException("CircuitBreakerDrawdown must be > 0.");
		}
	}

	// ===============================================================
	// RISK GOVERNOR RESULT
	// ===============================================================
	public sealed class RiskGovernorResult
	{
		public double FinalScale { get; init; } = 1.0;
		public double VixScale { get; init; } = 1.0;
		public double VolatilityScale { get; init; } = 1.0;
		public double CircuitBreakerScale { get; init; } = 1.0;
		public double CorrelationScale { get; init; } = 1.0;
		public string ActiveFactors { get; init; } = "";

		// State tracking - CALLER MUST PERSIST
		public CircuitBreakerState NewCircuitBreakerState { get; init; } = CircuitBreakerState.Normal;
		public int NewPeriodsInCurrentState { get; init; } = 0;
		public double CurrentRegimeSignal { get; init; } = double.NaN;

		// Monitoring
		public bool CircuitBreakerActive => NewCircuitBreakerState != CircuitBreakerState.Normal;
		public double DrawdownThresholdToAdvance { get; init; } = 0.0;
		public int PeriodsRemainingToAdvance { get; init; } = 0;

		// Regime flip info
		public RegimeFlipDirection RegimeFlip { get; init; } = RegimeFlipDirection.None;
		public bool RegimeFlipCausedTransition { get; init; } = false;
	}

	// ===============================================================
	// CONFIG
	// ===============================================================
	public sealed class Config
	{
		public double PortfolioValue { get; init; } = 100_000_000.0;
		public double MaxPerNameFraction { get; init; } = 0.10;
		public double GrossTargetPerSideFraction { get; init; } = 1.0;
		public double MaxTotalGrossFraction { get; init; } = 2.0;
		public double MinTotalGrossFraction { get; init; } = 1.60;

		// Sizing
		public bool UseAlphaWeighting { get; init; } = true;
		public double AlphaVolatilityPower { get; init; } = 0.5;
		public bool UseRiskParity { get; init; } = true;

		// Neutrality
		public double MaxMarketImbalanceFraction { get; init; } = 0.05;
		public bool EnforceMarketNeutral { get; init; } = true;
		public bool EnforceVolatilityNeutral { get; init; } = false;
		public double MaxVolatilityImbalanceFraction { get; init; } = 0.05;
		public bool EnforceBetaNeutral { get; init; } = false;
		public double BetaTolerance { get; init; } = 0.05;

		// Concentration limits

		public bool EnforceSectorLimits { get; init; } = true;
		public double MaxSectorFraction { get; init; } = 0.12;
		public bool EnforceIndustryLimits { get; init; } = true;
		public double MaxIndustryFraction { get; init; } = 0.12;
		public bool EnforceSubIndustryLimits { get; init; } = true;
		public double MaxSubIndustryFraction { get; init; } = 0.12;

		// Risk governor
		public bool EnableRiskGovernor { get; init; } = true;
		public RiskGovernorConfig RiskGovernor { get; init; } = RiskGovernorConfig.Default;
	}

	// ===============================================================
	// ENGINE
	// ===============================================================
	public sealed class Engine
	{
		private readonly IReadOnlyList<Stock> _stocks;
		private readonly Config _cfg;

		public RiskGovernorResult LastRiskGovernorResult { get; private set; }

		public Engine(IEnumerable<Stock> stocks, Config cfg)
		{
			_stocks = stocks?.ToList() ?? throw new ArgumentNullException(nameof(stocks));
			_cfg = cfg ?? throw new ArgumentNullException(nameof(cfg));

			if (_cfg.PortfolioValue <= 0)
				throw new ArgumentException("PortfolioValue must be > 0.");
			if (_cfg.MaxPerNameFraction <= 0)
				throw new ArgumentException("MaxPerNameFraction must be > 0.");

			_cfg.RiskGovernor?.Validate();
		}

		public (Dictionary<string, double> DollarWeights, double HedgeNotional) Solve()
		{
			if (_stocks.Count == 0)
			{
				LastRiskGovernorResult = new RiskGovernorResult { ActiveFactors = "NO_POSITIONS" };
				return (new Dictionary<string, double>(), 0.0);
			}
			return SolveCore();
		}

		// ---------------------------------------------------------------
		// Helpers
		// ---------------------------------------------------------------

		private string MakeKey(Stock s) =>
			string.IsNullOrEmpty(s.GroupId) ? s.Ticker : $"{s.GroupId}\t{s.Ticker}";

		private static double SafeSum(List<double> w) =>
			(w == null || w.Count == 0) ? 0.0 : w.Sum();

		private static double Clamp(double x, double lo, double hi) =>
			x < lo ? lo : (x > hi ? hi : x);

		private bool CapPerNameInPlace(List<double> weights)
		{
			bool anyCapped = false;
			for (int i = 0; i < weights.Count; i++)
			{
				if (weights[i] > _cfg.MaxPerNameFraction)
				{
					weights[i] = _cfg.MaxPerNameFraction;
					anyCapped = true;
				}
				if (weights[i] < 0) weights[i] = 0;
			}
			return anyCapped;
		}

		private static void CopyWeightsInPlace(List<double> src, List<double> dst)
		{
			for (int i = 0; i < src.Count; i++)
				dst[i] = src[i];
		}

		private (double Achieved, bool Reduced) NormalizeAndCapToGross(List<double> weights, double target, string name = "")
		{
			if (weights == null || weights.Count == 0) return (0, false);

			double current = SafeSum(weights);
			if (current <= 0) return (0, false);

			bool reduced = false;
			double effectiveTarget = target;
			double theoreticalMax = weights.Count * _cfg.MaxPerNameFraction;

			if (target > theoreticalMax)
			{
				effectiveTarget = theoreticalMax;
				reduced = true;
			}

			for (int iter = 0; iter < 50; iter++)
			{
				current = SafeSum(weights);
				if (Math.Abs(current - effectiveTarget) <= 0.0001 * effectiveTarget) break;

				var capped = new HashSet<int>();
				double cappedTotal = 0, uncappedTotal = 0;

				for (int i = 0; i < weights.Count; i++)
				{
					if (weights[i] >= _cfg.MaxPerNameFraction * 0.999)
					{
						capped.Add(i);
						cappedTotal += weights[i];
					}
					else
					{
						uncappedTotal += weights[i];
					}
				}

				if (capped.Count == weights.Count)
				{
					if (current < effectiveTarget) effectiveTarget = current;
					reduced = true;
					break;
				}

				double targetUncapped = effectiveTarget - cappedTotal;
				if (targetUncapped <= 0)
				{
					double scale = effectiveTarget / current;
					for (int i = 0; i < weights.Count; i++) weights[i] *= scale;
					CapPerNameInPlace(weights);
					continue;
				}

				if (uncappedTotal > 0)
				{
					double scale = targetUncapped / uncappedTotal;
					for (int i = 0; i < weights.Count; i++)
						if (!capped.Contains(i)) weights[i] *= scale;
				}
				CapPerNameInPlace(weights);
			}

			for (int i = 0; i < weights.Count; i++)
				if (weights[i] > _cfg.MaxPerNameFraction)
					weights[i] = _cfg.MaxPerNameFraction;

			double final = SafeSum(weights);
			if (final < target * 0.999) reduced = true;
			return (final, reduced);
		}

		private double ComputePortfolioBeta(List<Stock> longs, List<double> lw, List<Stock> shorts, List<double> sw)
		{
			double beta = 0;
			for (int i = 0; i < longs.Count; i++) beta += longs[i].Beta * lw[i];
			for (int i = 0; i < shorts.Count; i++) beta -= shorts[i].Beta * sw[i];
			return beta;
		}

		private void AdjustForVolatilityNeutrality(List<Stock> longs, List<double> lw, List<Stock> shorts, List<double> sw)
		{
			double lv = 0, sv = 0;
			for (int i = 0; i < longs.Count; i++) lv += lw[i] * longs[i].Volatility;
			for (int i = 0; i < shorts.Count; i++) sv += sw[i] * shorts[i].Volatility;

			if (lv <= 0 || sv <= 0) return;

			double imbalance = Math.Abs(lv - sv) / (lv + sv);
			if (imbalance <= _cfg.MaxVolatilityImbalanceFraction) return;

			double target = Math.Sqrt(lv * sv);
			double ls = target / lv, ss = target / sv;
			for (int i = 0; i < lw.Count; i++) lw[i] *= ls;
			for (int i = 0; i < sw.Count; i++) sw[i] *= ss;
		}

		private void BetaNeutralizeWithinBooks(List<Stock> longs, List<double> lw, List<Stock> shorts, List<double> sw,
			ref double longTarget, ref double shortTarget)
		{
			if (longs.Count == 0 || shorts.Count == 0) return;

			for (int outer = 0; outer < 6; outer++)
			{
				var (al, lr) = NormalizeAndCapToGross(lw, longTarget);
				var (ash, sr) = NormalizeAndCapToGross(sw, shortTarget);
				if (lr) longTarget = al;
				if (sr) shortTarget = ash;

				for (int iter = 0; iter < 80; iter++)
				{
					double beta = ComputePortfolioBeta(longs, lw, shorts, sw);
					if (Math.Abs(beta) <= _cfg.BetaTolerance) break;

					bool reduce = beta > 0;
					ShiftWithinBook(longs, lw, reduce, longTarget);
					ShiftWithinBook(shorts, sw, !reduce, shortTarget);
					NormalizeAndCapToGross(lw, longTarget);
					NormalizeAndCapToGross(sw, shortTarget);
				}

				var ll = ApplySectorLimits(longs, lw);
				var sl = ApplySectorLimits(shorts, sw);
				CopyWeightsInPlace(ll, lw);
				CopyWeightsInPlace(sl, sw);

				// v6 FIX: After sector capping, normalize only up to the post-cap sum,
				// not the original target. This prevents NormalizeAndCapToGross from
				// rescaling sectors back above their limit.
				double postSectorLong = SafeSum(lw);
				double postSectorShort = SafeSum(sw);
				double cappedLongTarget = Math.Min(longTarget, postSectorLong);
				double cappedShortTarget = Math.Min(shortTarget, postSectorShort);

				(al, lr) = NormalizeAndCapToGross(lw, cappedLongTarget);
				(ash, sr) = NormalizeAndCapToGross(sw, cappedShortTarget);
				if (lr) longTarget = al;
				if (sr) shortTarget = ash;

				if (Math.Abs(ComputePortfolioBeta(longs, lw, shorts, sw)) <= _cfg.BetaTolerance) break;
			}
		}

		private void ShiftWithinBook(List<Stock> stocks, List<double> w, bool fromHigh, double target)
		{
			if (stocks.Count < 2) return;

			int donor = fromHigh ? ArgMaxBeta(stocks, w) : ArgMinBeta(stocks, w);
			int recv = fromHigh ? ArgMinBetaWithRoom(stocks, w) : ArgMaxBetaWithRoom(stocks, w);

			if (donor < 0 || recv < 0 || donor == recv) return;
			if (w[donor] <= 0) return;

			double room = _cfg.MaxPerNameFraction - w[recv];
			if (room <= 1e-12) return;

			double step = Math.Min(Math.Min(0.0025 * target, w[donor]), room);
			if (step <= 1e-12) return;

			w[donor] -= step;
			w[recv] += step;
			if (w[donor] < 0) w[donor] = 0;
			if (w[recv] > _cfg.MaxPerNameFraction) w[recv] = _cfg.MaxPerNameFraction;
		}

		private int ArgMaxBeta(List<Stock> s, List<double> w)
		{
			double best = double.NegativeInfinity; int idx = -1;
			for (int i = 0; i < s.Count; i++)
				if (w[i] > 0 && s[i].Beta > best) { best = s[i].Beta; idx = i; }
			return idx;
		}

		private int ArgMinBeta(List<Stock> s, List<double> w)
		{
			double best = double.PositiveInfinity; int idx = -1;
			for (int i = 0; i < s.Count; i++)
				if (w[i] > 0 && s[i].Beta < best) { best = s[i].Beta; idx = i; }
			return idx;
		}

		private int ArgMaxBetaWithRoom(List<Stock> s, List<double> w)
		{
			double best = double.NegativeInfinity; int idx = -1;
			for (int i = 0; i < s.Count; i++)
				if (_cfg.MaxPerNameFraction - w[i] > 1e-12 && s[i].Beta > best) { best = s[i].Beta; idx = i; }
			return idx;
		}

		private int ArgMinBetaWithRoom(List<Stock> s, List<double> w)
		{
			double best = double.PositiveInfinity; int idx = -1;
			for (int i = 0; i < s.Count; i++)
				if (_cfg.MaxPerNameFraction - w[i] > 1e-12 && s[i].Beta < best) { best = s[i].Beta; idx = i; }
			return idx;
		}

		private List<double> ApplySectorLimits(List<Stock> stocks, List<double> weights)
		{
			var w = new List<double>(weights);
			for (int iter = 0; iter < 10; iter++)
			{
				bool changed = false;
				if (_cfg.EnforceSectorLimits)
					changed |= ApplyGroupLimit(stocks, w, s => s.Sector, _cfg.MaxSectorFraction);
				if (_cfg.EnforceIndustryLimits)
					changed |= ApplyGroupLimit(stocks, w, s => s.Industry, _cfg.MaxIndustryFraction);
				if (_cfg.EnforceSubIndustryLimits)
					changed |= ApplyGroupLimit(stocks, w, s => s.SubIndustry, _cfg.MaxSubIndustryFraction);
				if (!changed) break;
			}
			return w;
		}

		private bool ApplyGroupLimit(List<Stock> stocks, List<double> w, Func<Stock, string> sel, double max)
		{
			var groups = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
			for (int i = 0; i < stocks.Count; i++)
			{
				var g = sel(stocks[i]);
				if (string.IsNullOrWhiteSpace(g)) continue;
				if (!groups.ContainsKey(g)) groups[g] = 0;
				groups[g] += w[i];
			}

			bool changed = false;
			foreach (var kvp in groups.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
			{
				if (kvp.Value > max)
				{
					double scale = max / kvp.Value;
					for (int i = 0; i < stocks.Count; i++)
						if (string.Equals(sel(stocks[i]), kvp.Key, StringComparison.OrdinalIgnoreCase))
						{ w[i] *= scale; changed = true; }
				}
			}
			return changed;
		}

		/// <summary>
		/// v6: Final enforcement of sector/industry/subindustry limits applied directly
		/// to weight arrays. No normalization follows this call, so caps stick.
		/// </summary>
		private void EnforceFinalConcentrationLimits(List<Stock> longs, List<double> lw,
			List<Stock> shorts, List<double> sw)
		{
			if (!_cfg.EnforceSectorLimits && !_cfg.EnforceIndustryLimits && !_cfg.EnforceSubIndustryLimits)
				return;

			for (int iter = 0; iter < 10; iter++)
			{
				bool changed = false;
				if (_cfg.EnforceSectorLimits)
				{
					changed |= ApplyGroupLimit(longs, lw, s => s.Sector, _cfg.MaxSectorFraction);
					changed |= ApplyGroupLimit(shorts, sw, s => s.Sector, _cfg.MaxSectorFraction);
				}
				if (_cfg.EnforceIndustryLimits)
				{
					changed |= ApplyGroupLimit(longs, lw, s => s.Industry, _cfg.MaxIndustryFraction);
					changed |= ApplyGroupLimit(shorts, sw, s => s.Industry, _cfg.MaxIndustryFraction);
				}
				if (_cfg.EnforceSubIndustryLimits)
				{
					changed |= ApplyGroupLimit(longs, lw, s => s.SubIndustry, _cfg.MaxSubIndustryFraction);
					changed |= ApplyGroupLimit(shorts, sw, s => s.SubIndustry, _cfg.MaxSubIndustryFraction);
				}
				if (!changed) break;
			}

			// Log any violations or near-violations that remain
			LogConcentrationExposures(longs, lw, "LONG");
			LogConcentrationExposures(shorts, sw, "SHORT");
		}

		/// <summary>
		/// v6: Diagnostic logging for sector exposures after final enforcement.
		/// </summary>
		private void LogConcentrationExposures(List<Stock> stocks, List<double> w, string side)
		{
			if (_cfg.EnforceSectorLimits)
			{
				var sectors = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
				for (int i = 0; i < stocks.Count; i++)
				{
					var sec = stocks[i].Sector;
					if (string.IsNullOrWhiteSpace(sec)) continue;
					if (!sectors.ContainsKey(sec)) sectors[sec] = 0;
					sectors[sec] += w[i];
				}
				foreach (var kvp in sectors.OrderByDescending(x => x.Value))
				{
					if (kvp.Value > _cfg.MaxSectorFraction)
					{
						Debug.WriteLine($"[Engine] SECTOR VIOLATION {side}: '{kvp.Key}' = {kvp.Value:P2} > {_cfg.MaxSectorFraction:P0}");
						AuditService.LogConstraintBreach(null,
							$"SECTOR VIOLATION {side}: '{kvp.Key}' = {kvp.Value:P2} > {_cfg.MaxSectorFraction:P0}",
							"High");
					}
					else if (kvp.Value > _cfg.MaxSectorFraction * 0.9)
					{
						Debug.WriteLine($"[Engine] SECTOR NEAR LIMIT {side}: '{kvp.Key}' = {kvp.Value:P2}");
						AuditService.LogConstraintBreach(null,
							$"SECTOR NEAR LIMIT {side}: '{kvp.Key}' = {kvp.Value:P2}",
							"Warning");
					}
				}
			}

			if (_cfg.EnforceIndustryLimits)
			{
				var industries = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
				for (int i = 0; i < stocks.Count; i++)
				{
					var ind = stocks[i].Industry;
					if (string.IsNullOrWhiteSpace(ind)) continue;
					if (!industries.ContainsKey(ind)) industries[ind] = 0;
					industries[ind] += w[i];
				}
				foreach (var kvp in industries.OrderByDescending(x => x.Value))
				{
					if (kvp.Value > _cfg.MaxIndustryFraction)
					{
						Debug.WriteLine($"[Engine] INDUSTRY VIOLATION {side}: '{kvp.Key}' = {kvp.Value:P2} > {_cfg.MaxIndustryFraction:P0}");
						AuditService.LogConstraintBreach(null,
							$"INDUSTRY VIOLATION {side}: '{kvp.Key}' = {kvp.Value:P2} > {_cfg.MaxIndustryFraction:P0}",
							"High");
					}
				}
			}

			if (_cfg.EnforceSubIndustryLimits)
			{
				var subIndustries = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
				for (int i = 0; i < stocks.Count; i++)
				{
					var sub = stocks[i].SubIndustry;
					if (string.IsNullOrWhiteSpace(sub)) continue;
					if (!subIndustries.ContainsKey(sub)) subIndustries[sub] = 0;
					subIndustries[sub] += w[i];
				}
				foreach (var kvp in subIndustries.OrderByDescending(x => x.Value))
				{
					if (kvp.Value > _cfg.MaxSubIndustryFraction)
					{
						Debug.WriteLine($"[Engine] SUB-INDUSTRY VIOLATION {side}: '{kvp.Key}' = {kvp.Value:P2} > {_cfg.MaxSubIndustryFraction:P0}");
						AuditService.LogConstraintBreach(null,
							$"SUB-INDUSTRY VIOLATION {side}: '{kvp.Key}' = {kvp.Value:P2} > {_cfg.MaxSubIndustryFraction:P0}",
							"High");
					}
				}
			}
		}

		// ===============================================================
		// RISK GOVERNOR
		// ===============================================================

		private RiskGovernorResult ComputeRiskGovernorScale()
		{
			if (!_cfg.EnableRiskGovernor)
			{
				return new RiskGovernorResult
				{
					FinalScale = 1.0,
					ActiveFactors = "DISABLED",
					NewCircuitBreakerState = CircuitBreakerState.Normal
				};
			}

			var g = _cfg.RiskGovernor ?? RiskGovernorConfig.Default;
			var factors = new List<string>();

			double vixScale = 1.0, volScale = 1.0, cbScale = 1.0, corrScale = 1.0;
			var newState = g.CurrentCircuitBreakerState;
			int newPeriods = g.PeriodsInCurrentState;
			double threshold = 0.0;
			int remaining = 0;
			var regimeFlip = RegimeFlipDirection.None;
			bool flipCausedTransition = false;

			// -----------------------------------------------------------
			// 1. VIX
			// -----------------------------------------------------------
			if (g.EnableVixStressDetection && !double.IsNaN(g.VixLevel) && g.VixLevel > 0)
			{
				if (g.VixLevel >= g.VixThreshold2)
				{
					vixScale = g.VixScale2;
					factors.Add($"VIX_SEVERE({vixScale:P0})");
					Debug.WriteLine($"[RiskGov] VIX SEVERE: {g.VixLevel:F1} >= {g.VixThreshold2} → {vixScale:P0}");
				}
				else if (g.VixLevel >= g.VixThreshold1)
				{
					vixScale = g.VixScale1;
					factors.Add($"VIX_ELEVATED({vixScale:P0})");
					Debug.WriteLine($"[RiskGov] VIX ELEVATED: {g.VixLevel:F1} >= {g.VixThreshold1} → {vixScale:P0}");
				}
			}

			// -----------------------------------------------------------
			// 2. VOLATILITY TARGETING
			// -----------------------------------------------------------
			if (g.EnableVolatilityTargeting && !double.IsNaN(g.TrailingVolatility) && g.TrailingVolatility > 0)
			{
				double raw = g.TargetPortfolioVolatility / g.TrailingVolatility;
				volScale = Clamp(raw, g.MinVolatilityScale, g.MaxVolatilityScale);

				if (!double.IsNaN(g.PreviousVolatilityScale) && g.VolatilityScaleSmoothingFactor < 1.0)
				{
					volScale = g.PreviousVolatilityScale + g.VolatilityScaleSmoothingFactor * (volScale - g.PreviousVolatilityScale);
					volScale = Clamp(volScale, g.MinVolatilityScale, g.MaxVolatilityScale);
				}

				if (Math.Abs(volScale - 1.0) > 0.01)
					factors.Add($"VOL({volScale:P0})");

				Debug.WriteLine($"[RiskGov] VolTarget: Trailing={g.TrailingVolatility:P1}, Target={g.TargetPortfolioVolatility:P1}, Raw={raw:F2}, PrevScale={g.PreviousVolatilityScale:F2}, FinalScale={volScale:P0}, Smoothing={(double.IsNaN(g.PreviousVolatilityScale) ? "SKIPPED" : "APPLIED")}");
			}

			// -----------------------------------------------------------
			// 3. REGIME FLIP DETECTION
			// -----------------------------------------------------------
			if (g.EnableRegimeAwareRecovery && g.EnableRegimeFlipTransitions &&
				!double.IsNaN(g.RegimeSignal) && !double.IsNaN(g.PreviousRegimeSignal))
			{
				bool wasBearish = g.PreviousRegimeSignal < g.RegimeBearishThreshold;
				bool wasBullish = g.PreviousRegimeSignal >= g.RegimeBullishThreshold;
				bool nowBearish = g.RegimeSignal < g.RegimeBearishThreshold;
				bool nowBullish = g.RegimeSignal >= g.RegimeBullishThreshold;

				if (wasBearish && nowBullish)
				{
					regimeFlip = RegimeFlipDirection.BearishToBullish;
					//Debug.WriteLine($"[RiskGov] REGIME FLIP: Bearish→Bullish ({g.PreviousRegimeSignal:F2} → {g.RegimeSignal:F2})");
				}
				else if (wasBullish && nowBearish)
				{
					regimeFlip = RegimeFlipDirection.BullishToBearish;
					//Debug.WriteLine($"[RiskGov] REGIME FLIP: Bullish→Bearish ({g.PreviousRegimeSignal:F2} → {g.RegimeSignal:F2})");
				}
			}

			// -----------------------------------------------------------
			// 4. CIRCUIT BREAKER STATE MACHINE
			// -----------------------------------------------------------
			if (g.EnableCircuitBreaker && !double.IsNaN(g.CurrentNav) && !double.IsNaN(g.PeakNav) &&
				g.CurrentNav > 0 && g.PeakNav > 0)
			{
				double dd = Math.Max(0, (g.PeakNav - g.CurrentNav) / g.PeakNav);
				Debug.WriteLine($"[RiskGov] CB: DD={dd:P2}, State={g.CurrentCircuitBreakerState}, Periods={g.PeriodsInCurrentState}, Regime={g.RegimeSignal:F2}");

				// Check for immediate exit via strong bullish regime
				if (g.EnableRegimeAwareRecovery &&
					g.CurrentCircuitBreakerState != CircuitBreakerState.Normal &&
					!double.IsNaN(g.RegimeSignal) &&
					g.RegimeSignal >= g.RegimeImmediateExitThreshold &&
					dd < g.CircuitBreakerDrawdown * 0.5 &&
					!double.IsNaN(g.VixLevel) && g.VixLevel < g.RegimeExitMaxVix &&
					!double.IsNaN(g.TrailingVolatility) && g.TrailingVolatility <= g.TargetPortfolioVolatility)
				{
					Debug.WriteLine($"[RiskGov] CB: REGIME IMMEDIATE EXIT");
					newState = CircuitBreakerState.Normal;
					newPeriods = 0;
					cbScale = 1.0;
					flipCausedTransition = true;
					factors.Add("REGIME_EXIT");
				}
				// Check for trigger
				else if (dd >= g.CircuitBreakerDrawdown)
				{
					if (g.CurrentCircuitBreakerState != CircuitBreakerState.Triggered)
						Debug.WriteLine($"[RiskGov] CB: TRIGGERED");

					newState = CircuitBreakerState.Triggered;
					newPeriods = 0;
					cbScale = g.ScaleTriggered;
					threshold = g.ThresholdToRecovery1;
					remaining = GetEffectivePeriods(g);
					factors.Add($"CB_TRIGGERED({cbScale:P0})");
				}
				else
				{
					// Process state machine with regime awareness
					(newState, newPeriods, cbScale, threshold, remaining, flipCausedTransition) =
						ProcessCircuitBreakerWithRegime(g, dd, regimeFlip, factors);
				}
			}

			// -----------------------------------------------------------
			// 5. CORRELATION
			// -----------------------------------------------------------
			if (g.EnableCorrelationMonitor && !double.IsNaN(g.LongShortCorrelation))
			{
				if (g.LongShortCorrelation >= g.CorrelationThreshold)
				{
					corrScale = g.CorrelationBreakdownScale;
					factors.Add($"CORR({corrScale:P0})");
				}
			}

			// -----------------------------------------------------------
			// COMBINE
			// -----------------------------------------------------------
			double finalScale = vixScale * volScale * cbScale * corrScale;
			finalScale = Math.Max(finalScale, 0.05);

			Debug.WriteLine($"[RiskGov] FINAL: {finalScale:P0} = VIX({vixScale:P0}) × Vol({volScale:P0}) × CB({cbScale:P0}) × Corr({corrScale:P0})");

			if (regimeFlip != RegimeFlipDirection.None)
				factors.Add($"REGIME_FLIP_{regimeFlip}");

			return new RiskGovernorResult
			{
				FinalScale = finalScale,
				VixScale = vixScale,
				VolatilityScale = volScale,
				CircuitBreakerScale = cbScale,
				CorrelationScale = corrScale,
				ActiveFactors = factors.Count > 0 ? string.Join(", ", factors) : "NONE",
				NewCircuitBreakerState = newState,
				NewPeriodsInCurrentState = newPeriods,
				CurrentRegimeSignal = g.RegimeSignal,
				DrawdownThresholdToAdvance = threshold,
				PeriodsRemainingToAdvance = remaining,
				RegimeFlip = regimeFlip,
				RegimeFlipCausedTransition = flipCausedTransition
			};
		}

		private int GetEffectivePeriods(RiskGovernorConfig g)
		{
			if (!g.EnableRegimeAwareRecovery || double.IsNaN(g.RegimeSignal))
				return g.PeriodsRequiredToAdvance;

			if (g.RegimeSignal < g.RegimeBearishThreshold)
				return g.PeriodsRequiredToAdvance * 2; // Slow in bearish

			if (g.RegimeSignal >= g.RegimeBullishThreshold)
				return 1; // Fast in bullish

			return g.PeriodsRequiredToAdvance;
		}

		private CircuitBreakerState AdvanceState(CircuitBreakerState current)
		{
			return current switch
			{
				CircuitBreakerState.Triggered => CircuitBreakerState.Recovery1,
				CircuitBreakerState.Recovery1 => CircuitBreakerState.Recovery2,
				CircuitBreakerState.Recovery2 => CircuitBreakerState.Recovery3,
				CircuitBreakerState.Recovery3 => CircuitBreakerState.Normal,
				_ => current
			};
		}

		private CircuitBreakerState RegressState(CircuitBreakerState current)
		{
			return current switch
			{
				CircuitBreakerState.Normal => CircuitBreakerState.Recovery3,
				CircuitBreakerState.Recovery3 => CircuitBreakerState.Recovery2,
				CircuitBreakerState.Recovery2 => CircuitBreakerState.Recovery1,
				CircuitBreakerState.Recovery1 => CircuitBreakerState.Triggered,
				_ => current
			};
		}

		private double GetScaleForState(RiskGovernorConfig g, CircuitBreakerState state)
		{
			return state switch
			{
				CircuitBreakerState.Normal => 1.0,
				CircuitBreakerState.Triggered => g.ScaleTriggered,
				CircuitBreakerState.Recovery1 => g.ScaleRecovery1,
				CircuitBreakerState.Recovery2 => g.ScaleRecovery2,
				CircuitBreakerState.Recovery3 => g.ScaleRecovery3,
				_ => 1.0
			};
		}

		private (double Threshold, double Regression) GetThresholdsForState(RiskGovernorConfig g, CircuitBreakerState state)
		{
			return state switch
			{
				CircuitBreakerState.Triggered => (g.ThresholdToRecovery1, g.CircuitBreakerDrawdown),
				CircuitBreakerState.Recovery1 => (g.ThresholdToRecovery2, g.RegressionToTriggered),
				CircuitBreakerState.Recovery2 => (g.ThresholdToRecovery3, g.RegressionToRecovery1),
				CircuitBreakerState.Recovery3 => (g.ThresholdToNormal, g.RegressionToRecovery2),
				_ => (0, 1.0)
			};
		}

		private string GetStateLabel(CircuitBreakerState state)
		{
			return state switch
			{
				CircuitBreakerState.Normal => "NORMAL",
				CircuitBreakerState.Triggered => "CB_TRIGGERED",
				CircuitBreakerState.Recovery1 => "CB_RECOVERY1",
				CircuitBreakerState.Recovery2 => "CB_RECOVERY2",
				CircuitBreakerState.Recovery3 => "CB_RECOVERY3",
				_ => "UNKNOWN"
			};
		}

		private (CircuitBreakerState State, int Periods, double Scale, double Threshold, int Remaining, bool FlipTransition)
			ProcessCircuitBreakerWithRegime(RiskGovernorConfig g, double dd, RegimeFlipDirection flip, List<string> factors)
		{
			var currentState = g.CurrentCircuitBreakerState;
			int effectivePeriods = GetEffectivePeriods(g);

			// Handle Normal state
			if (currentState == CircuitBreakerState.Normal)
			{
				// Bullish → Bearish flip while in Normal: go to Recovery3 (preemptive defense)
				if (flip == RegimeFlipDirection.BullishToBearish)
				{
					Debug.WriteLine($"[RiskGov] CB: REGIME FLIP DEFENSE Normal → Recovery3");
					factors.Add($"CB_RECOVERY3({g.ScaleRecovery3:P0})");
					factors.Add("FLIP_DEFENSE");
					return (CircuitBreakerState.Recovery3, 0, g.ScaleRecovery3, g.ThresholdToNormal, effectivePeriods, true);
				}
				return (CircuitBreakerState.Normal, 0, 1.0, 0, 0, false);
			}

			var (advanceThreshold, regressionThreshold) = GetThresholdsForState(g, currentState);
			double scale = GetScaleForState(g, currentState);
			string label = GetStateLabel(currentState);

			// REGIME FLIP TRANSITIONS (take priority over DD-based transitions)
			if (flip == RegimeFlipDirection.BearishToBullish && currentState != CircuitBreakerState.Normal)
			{
				// Advance one state
				var nextState = AdvanceState(currentState);
				double nextScale = GetScaleForState(g, nextState);
				var (nextThreshold, _) = GetThresholdsForState(g, nextState);

				Debug.WriteLine($"[RiskGov] CB: REGIME FLIP ADVANCE {currentState} → {nextState}");
				factors.Add($"{GetStateLabel(nextState)}({nextScale:P0})");
				factors.Add("FLIP_ADVANCE");
				return (nextState, 0, nextScale, nextThreshold, effectivePeriods, true);
			}

			if (flip == RegimeFlipDirection.BullishToBearish && currentState != CircuitBreakerState.Triggered)
			{
				// Regress one state
				var prevState = RegressState(currentState);
				double prevScale = GetScaleForState(g, prevState);
				var (prevThreshold, _) = GetThresholdsForState(g, prevState);

				Debug.WriteLine($"[RiskGov] CB: REGIME FLIP REGRESS {currentState} → {prevState}");
				factors.Add($"{GetStateLabel(prevState)}({prevScale:P0})");
				factors.Add("FLIP_REGRESS");
				return (prevState, 0, prevScale, prevThreshold, effectivePeriods, true);
			}

			// DD-BASED REGRESSION (check first)
			if (dd > regressionThreshold && currentState != CircuitBreakerState.Triggered)
			{
				// Find the right state to regress to based on DD
				CircuitBreakerState regressTo;
				if (dd >= g.RegressionToTriggered)
					regressTo = CircuitBreakerState.Triggered;
				else if (dd >= g.RegressionToRecovery1)
					regressTo = CircuitBreakerState.Recovery1;
				else if (dd >= g.RegressionToRecovery2)
					regressTo = CircuitBreakerState.Recovery2;
				else
					regressTo = currentState; // No regression needed

				if (regressTo != currentState)
				{
					double regScale = GetScaleForState(g, regressTo);
					var (regThreshold, _) = GetThresholdsForState(g, regressTo);
					Debug.WriteLine($"[RiskGov] CB: DD REGRESS {currentState} → {regressTo} (DD={dd:P2})");
					factors.Add($"{GetStateLabel(regressTo)}({regScale:P0})");
					return (regressTo, 0, regScale, regThreshold, effectivePeriods, false);
				}
			}

			// DD-BASED ADVANCEMENT
			if (dd <= advanceThreshold)
			{
				// Bullish regime = instant advance
				bool instantAdvance = g.EnableRegimeAwareRecovery &&
									 !double.IsNaN(g.RegimeSignal) &&
									 g.RegimeSignal >= g.RegimeBullishThreshold;

				int newPeriods = g.PeriodsInCurrentState + 1;

				if (instantAdvance || newPeriods >= effectivePeriods)
				{
					var nextState = AdvanceState(currentState);
					if (nextState != currentState)
					{
						double nextScale = GetScaleForState(g, nextState);
						var (nextThreshold, _) = GetThresholdsForState(g, nextState);
						string reason = instantAdvance ? "BULLISH REGIME" : "PERIODS MET";
						Debug.WriteLine($"[RiskGov] CB: {reason} ADVANCE {currentState} → {nextState}");
						factors.Add($"{GetStateLabel(nextState)}({nextScale:P0})");
						return (nextState, 0, nextScale, nextThreshold, effectivePeriods, instantAdvance);
					}
				}

				// Still progressing
				Debug.WriteLine($"[RiskGov] CB: {currentState} progressing ({newPeriods}/{effectivePeriods})");
				factors.Add($"{label}({scale:P0})");
				return (currentState, newPeriods, scale, advanceThreshold, effectivePeriods - newPeriods, false);
			}

			// Holding - DD above advance threshold
			Debug.WriteLine($"[RiskGov] CB: {currentState} holding (DD={dd:P2} > {advanceThreshold:P2})");
			factors.Add($"{label}({scale:P0})");
			return (currentState, 0, scale, advanceThreshold, effectivePeriods, false);
		}

		private void ApplyExposureScale(List<double> w, double scale)
		{
			if (w == null) return;
			for (int i = 0; i < w.Count; i++) w[i] *= scale;
		}

		// ===============================================================
		// MAIN SOLVER
		// ===============================================================

		// ===============================================================
		// SOLVE CORE - FEATURE APPLICATION ORDER (v7)
		// ===============================================================
		//
		// The order matters because each constraint can disturb the ones
		// applied before it. The last constraint applied will be the most
		// strictly satisfied. This ordering ensures all constraints hold
		// at the end with proper re-checks.
		//
		// STEP 1: Initial Weights (Alpha / RiskParity / Equal)
		// STEP 2: Normalize + Per-Name Cap to gross target
		// STEP 3: Volatility Neutral (if enabled)
		//         - Re-normalize after adjustment
		// STEP 4: Beta Neutral (if enabled)
		//         - Internal loop: shift weights, re-normalize, sector limits
		// STEP 5: Sector / Industry / Sub-Industry Limits
		//         - Only runs standalone when beta neutral is OFF
		//         - When beta neutral is ON, sector limits already applied inside step 4
		//         - Normalize to post-cap sum (v6 fix: don't rescale above caps)
		// STEP 6: Market Neutral (dollar neutral)
		//         - Scale larger side down to match smaller side
		// STEP 7: Risk Governor (VIX, Vol Target, Circuit Breaker, Correlation)
		//         - Scales both sides equally, preserves market neutrality
		// STEP 8: Final Per-Name Cap
		//         - Catches any names pushed above cap by earlier steps
		// STEP 9: Final Concentration Limits (sector/industry/sub-industry)
		//         - Catches any groups pushed above cap by steps 6-8
		//         - No normalization after this - caps are guaranteed to stick
		// STEP 10: Final Per-Name Cap (second pass)
		//          - Catches any names pushed above cap by step 9 redistribution
		// STEP 11: Final Market Neutral Re-Check
		//          - Ensures dollar balance still holds after all capping
		//
		private (Dictionary<string, double>, double) SolveCore()
		{
			var result = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

			var sorted = _stocks.OrderBy(s => s.Ticker).ThenBy(s => s.GroupId ?? "").ToList();
			var longs = new List<Stock>();
			var shorts = new List<Stock>();

			foreach (var s in sorted)
			{
				if ((_cfg.UseAlphaWeighting || _cfg.UseRiskParity || _cfg.EnforceVolatilityNeutral) &&
					(s.Volatility <= 0 || double.IsNaN(s.Volatility)))
					continue;

				if (s.RequiredBook == BookSide.Long) longs.Add(s);
				else if (s.RequiredBook == BookSide.Short) shorts.Add(s);
			}

			if (longs.Count == 0 && shorts.Count == 0)
			{
				LastRiskGovernorResult = new RiskGovernorResult { ActiveFactors = "NO_POSITIONS" };
				return (result, 0);
			}

			if (_cfg.EnforceMarketNeutral && (longs.Count == 0 || shorts.Count == 0))
			{
				LastRiskGovernorResult = new RiskGovernorResult { ActiveFactors = "ONE_SIDED" };
				return (result, 0);
			}

			// ---------------------------------------------------------------
			// STEP 1: Initial Weights
			// ---------------------------------------------------------------
			var lw = CalcInitialWeights(longs);
			var sw = CalcInitialWeights(shorts);

			double lt = _cfg.GrossTargetPerSideFraction;
			double st = _cfg.GrossTargetPerSideFraction;

			// ---------------------------------------------------------------
			// STEP 2: Normalize + Per-Name Cap to gross target
			// ---------------------------------------------------------------
			var (al, lr) = NormalizeAndCapToGross(lw, lt);
			var (ash, sr) = NormalizeAndCapToGross(sw, st);
			if (lr) lt = al;
			if (sr) st = ash;

			// ---------------------------------------------------------------
			// STEP 3: Volatility Neutral
			// ---------------------------------------------------------------
			if (_cfg.EnforceVolatilityNeutral && longs.Count > 0 && shorts.Count > 0)
			{
				AdjustForVolatilityNeutrality(longs, lw, shorts, sw);
				NormalizeAndCapToGross(lw, lt);
				NormalizeAndCapToGross(sw, st);
			}

			// ---------------------------------------------------------------
			// STEP 4: Beta Neutral
			// BetaNeutralizeWithinBooks has its own internal sector limits
			// and normalization loop, so we skip the standalone sector pass
			// when beta neutral is enabled.
			// ---------------------------------------------------------------
			if (_cfg.EnforceBetaNeutral && longs.Count > 0 && shorts.Count > 0)
			{
				BetaNeutralizeWithinBooks(longs, lw, shorts, sw, ref lt, ref st);
			}

			// ---------------------------------------------------------------
			// STEP 5: Sector / Industry / Sub-Industry Limits
			// Only runs standalone when beta neutral is OFF.
			// When beta neutral is ON, sector limits were already applied
			// inside BetaNeutralizeWithinBooks (step 4).
			// ---------------------------------------------------------------
			if (!_cfg.EnforceBetaNeutral)
			{
				var ll = ApplySectorLimits(longs, lw);
				var sl = ApplySectorLimits(shorts, sw);
				CopyWeightsInPlace(ll, lw);
				CopyWeightsInPlace(sl, sw);

				// v6 FIX: After sector capping, normalize only up to the post-cap sum,
				// not the original target. This prevents NormalizeAndCapToGross from
				// rescaling sectors back above their limit.
				double postSectorLong = SafeSum(lw);
				double postSectorShort = SafeSum(sw);
				NormalizeAndCapToGross(lw, Math.Min(lt, postSectorLong));
				NormalizeAndCapToGross(sw, Math.Min(st, postSectorShort));
			}

			// ---------------------------------------------------------------
			// STEP 6: Market Neutral (dollar neutral)
			// ---------------------------------------------------------------
			if (_cfg.EnforceMarketNeutral && longs.Count > 0 && shorts.Count > 0)
			{
				EnforceMarketNeutrality(lw, sw);
			}

			// ---------------------------------------------------------------
			// STEP 7: Risk Governor
			// Scales both sides equally so market neutrality is preserved.
			// ---------------------------------------------------------------
			var rg = ComputeRiskGovernorScale();
			LastRiskGovernorResult = rg;
			AuditService.LastRiskGovernorResult = rg;

			if (Math.Abs(rg.FinalScale - 1.0) > 0.001)
			{
				ApplyExposureScale(lw, rg.FinalScale);
				ApplyExposureScale(sw, rg.FinalScale);
			}

			// ---------------------------------------------------------------
			// STEP 8: Final Per-Name Cap (first pass)
			// Risk governor scaling can push names above cap.
			// ---------------------------------------------------------------
			CapPerNameInPlace(lw);
			CapPerNameInPlace(sw);

			// ---------------------------------------------------------------
			// STEP 9: Final Concentration Limits
			// Market neutral scaling and risk governor scaling can push
			// sectors back above limits. No normalization after this so
			// caps are guaranteed to stick. Accepts slightly lower gross.
			// ---------------------------------------------------------------
			EnforceFinalConcentrationLimits(longs, lw, shorts, sw);

			// ---------------------------------------------------------------
			// STEP 10: Final Per-Name Cap (second pass)
			// Concentration limit enforcement can redistribute weight,
			// potentially pushing individual names above the per-name cap.
			// This second pass catches those cases.
			// ---------------------------------------------------------------
			CapPerNameInPlace(lw);
			CapPerNameInPlace(sw);

			// ---------------------------------------------------------------
			// STEP 11: Final Market Neutral Re-Check
			// Steps 8-10 (capping) can break dollar balance by reducing
			// one side more than the other. Re-enforce if needed.
			// ---------------------------------------------------------------
			if (_cfg.EnforceMarketNeutral && longs.Count > 0 && shorts.Count > 0)
			{
				EnforceMarketNeutrality(lw, sw);
			}

			// ---------------------------------------------------------------
			// BUILD RESULT
			// ---------------------------------------------------------------
			for (int i = 0; i < longs.Count; i++) result[MakeKey(longs[i])] = lw[i];
			for (int i = 0; i < shorts.Count; i++) result[MakeKey(shorts[i])] = -sw[i];

			Debug.WriteLine($"[Engine] Final: L={SafeSum(lw):P1}, S={SafeSum(sw):P1}, Scale={rg.FinalScale:P0}");
			Debug.WriteLine($"[Engine] Positions: {string.Join(", ", result.OrderByDescending(kv => Math.Abs(kv.Value)).Take(5).Select(kv => $"{kv.Key}={kv.Value:P2}"))}...");

			return (result, 0);
		}

		/// <summary>
		/// Enforce market neutrality by scaling the larger side down to match the smaller side.
		/// Extracted to a method so it can be called in both the main pass and the final re-check.
		/// </summary>
		private void EnforceMarketNeutrality(List<double> lw, List<double> sw)
		{
			double lg = SafeSum(lw), sg = SafeSum(sw);
			double imb = Math.Abs(lg - sg);
			if (imb > _cfg.MaxMarketImbalanceFraction)
			{
				double tg = Math.Min(lg, sg);
				if (lg > tg && lg > 0) { double sc = tg / lg; for (int i = 0; i < lw.Count; i++) lw[i] *= sc; }
				if (sg > tg && sg > 0) { double sc = tg / sg; for (int i = 0; i < sw.Count; i++) sw[i] *= sc; }
			}
		}

		private List<double> CalcInitialWeights(List<Stock> stocks)
		{
			if (stocks.Count == 0) return new List<double>();

			List<double> contrib;
			if (_cfg.UseAlphaWeighting)
			{
				double p = _cfg.AlphaVolatilityPower;
				contrib = p < 0.001
					? stocks.Select(s => Math.Max(s.SignalScore, 0.001)).ToList()
					: stocks.Select(s => Math.Max(s.SignalScore, 0.001) / Math.Pow(s.Volatility, p)).ToList();
			}
			else if (_cfg.UseRiskParity)
			{
				contrib = stocks.Select(s => 1.0 / s.Volatility).ToList();
			}
			else
			{
				contrib = Enumerable.Repeat(1.0, stocks.Count).ToList();
			}

			double sum = contrib.Sum();
			return sum > 0 ? contrib.Select(c => c / sum).ToList() : Enumerable.Repeat(0.0, stocks.Count).ToList();
		}
	}

	// ===============================================================
	// RESULT
	// ===============================================================
	public sealed class OptimizeResult
	{
		public Dictionary<string, double> DollarWeights { get; }
		public double HedgeNotional { get; }

		public OptimizeResult(Dictionary<string, double> w, double h)
		{
			DollarWeights = w ?? new Dictionary<string, double>();
			HedgeNotional = h;
		}
	}

	// ===============================================================
	// STATIC API
	// ===============================================================
	public static class PortfolioOptimizer
	{
		public static OptimizeResult Optimize(IReadOnlyList<Stock> stocks, Config cfg)
		{
			var engine = new Engine(stocks, cfg);
			var (w, h) = engine.Solve();
			return new OptimizeResult(w, h);
		}

		public static (OptimizeResult Result, Engine Engine) OptimizeWithEngine(IReadOnlyList<Stock> stocks, Config cfg)
		{
			var engine = new Engine(stocks, cfg);
			var (w, h) = engine.Solve();
			return (new OptimizeResult(w, h), engine);
		}
	}
}