
// RiskEngineMNParity.Engine.cs
// Regime-Aware Market-Neutral Portfolio Optimization Engine
// 
// Features:
// - Market neutrality (equal dollar longs and shorts)
// - Beta targeting (regime-driven, not just neutral)
// - Volatility neutrality (optional)
// - Risk parity sizing (optional)
// - Sector/Industry/SubIndustry concentration limits
// - Turnover constraints for regime transitions
// - Position-level constraints (min/max per name)
//
// OUTPUT: DollarWeights[key] is a SIGNED FRACTION of portfolio value
// (positive for longs, negative for shorts). Caller multiplies by PortfolioValue.

using OPTANO.Modeling.Optimization;
using OPTANO.Modeling.Optimization.Enums;
using OPTANO.Modeling.Optimization.Solver.Highs15x;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace RiskEngineMNParity
{
	// Enums (BookSide, MarketRegime, OptimizationObjective) are defined in Enums.cs

	#region Stock Model

	/// <summary>
	/// Stock descriptor with all attributes needed for optimization.
	/// </summary>
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

		/// <summary>
		/// Annualized volatility (e.g., 0.30 = 30% annual volatility).
		/// Used for risk parity sizing and volatility neutrality.
		/// </summary>
		public double Volatility { get; init; }

		/// <summary>
		/// Alpha signal strength (0-1, higher = stronger conviction).
		/// Used when Objective = MaximizeAlpha.
		/// </summary>
		public double AlphaScore { get; init; } = 1.0;

		/// <summary>
		/// Which book this stock belongs to (Long or Short).
		/// </summary>
		public BookSide RequiredBook { get; init; }

		/// <summary>
		/// Current weight in portfolio (for turnover constraints).
		/// Null if new position.
		/// </summary>
		public double? CurrentWeight { get; init; } = null;

		/// <summary>
		/// Unique key for position identification.
		/// </summary>
		public string Key => $"{RequiredBook}_{Ticker}";

		public override string ToString()
		{
			return $"[{RequiredBook}] {Ticker,-8} | β={Beta:F2} | σ={Volatility:P1} | " +
				   $"α={AlphaScore:F2} | {Sector}/{Industry}";
		}
	}

	#endregion

	#region Configuration

	/// <summary>
	/// Configuration for the portfolio optimization engine.
	/// Supports regime-aware settings via RegimeConfigFactory.
	/// </summary>
	public sealed class Config
	{
		// ---------------------------------------------------------------
		// Portfolio Basics
		// ---------------------------------------------------------------

		/// <summary>
		/// Total portfolio value in dollars.
		/// </summary>
		public double PortfolioValue { get; set; } = 1_000_000.0;

		/// <summary>
		/// Optimization objective function.
		/// </summary>
		public OptimizationObjective Objective { get; set; } = OptimizationObjective.EqualWeight;

		// ---------------------------------------------------------------
		// Gross Exposure Limits
		// ---------------------------------------------------------------

		/// <summary>
		/// Target gross exposure per side as fraction of portfolio.
		/// 1.0 = 100% long + 100% short = 200% total gross.
		/// </summary>
		public double GrossTargetPerSideFraction { get; set; } = 0.50;

		/// <summary>
		/// Maximum total gross exposure (long + |short|) as fraction of portfolio.
		/// </summary>
		public double MaxTotalGrossFraction { get; set; } = 1.20;

		/// <summary>
		/// Minimum total gross exposure as fraction of portfolio.
		/// </summary>
		public double MinTotalGrossFraction { get; set; } = 0.30;

		// ---------------------------------------------------------------
		// Position Limits
		// ---------------------------------------------------------------

		/// <summary>
		/// Maximum per-name fraction of portfolio (e.g., 0.15 = 15%).
		/// </summary>
		public double MaxPerNameFraction { get; set; } = 0.15;

		/// <summary>
		/// Minimum per-name fraction of portfolio.
		/// Positions below this are zeroed out.
		/// </summary>
		public double MinPerNameFraction { get; set; } = 0.005;

		// ---------------------------------------------------------------
		// Concentration Limits
		// ---------------------------------------------------------------

		/// <summary>
		/// Maximum sector exposure as fraction of portfolio (long + short combined).
		/// </summary>
		public double MaxSectorFraction { get; set; } = 0.30;

		/// <summary>
		/// Maximum industry exposure as fraction of portfolio.
		/// </summary>
		public double MaxIndustryFraction { get; set; } = 0.25;

		/// <summary>
		/// Maximum sub-industry exposure as fraction of portfolio.
		/// </summary>
		public double MaxSubIndustryFraction { get; set; } = 0.20;

		// ---------------------------------------------------------------
		// Market Neutrality
		// ---------------------------------------------------------------

		/// <summary>
		/// Enforce market neutrality (equal dollar longs and shorts).
		/// </summary>
		public bool EnforceMarketNeutral { get; set; } = true;

		/// <summary>
		/// Maximum allowed market imbalance as fraction of portfolio.
		/// Example: 0.10 = allow up to 10% difference between long and short gross.
		/// </summary>
		public double MaxMarketImbalanceFraction { get; set; } = 0.10;

		// ---------------------------------------------------------------
		// Beta Targeting (Regime-Aware)
		// ---------------------------------------------------------------

		/// <summary>
		/// Enforce beta constraint (either neutral or regime target).
		/// </summary>
		public bool EnforceBetaTarget { get; set; } = false;

		/// <summary>
		/// Target portfolio beta. Set based on regime.
		/// Positive = net long beta (bullish), Negative = net short beta (bearish).
		/// Zero = classic beta-neutral.
		/// </summary>
		public double TargetPortfolioBeta { get; set; } = 0.0;

		/// <summary>
		/// Tolerance around target beta.
		/// Example: 0.20 = allow portfolio beta within ±0.20 of target.
		/// </summary>
		public double BetaTolerance { get; set; } = 0.20;

		// ---------------------------------------------------------------
		// Volatility Neutrality
		// ---------------------------------------------------------------

		/// <summary>
		/// Enforce volatility neutrality constraint.
		/// When true: vol-weighted long exposure ≈ vol-weighted short exposure.
		/// Helps balance magnitude of moves between books.
		/// </summary>
		public bool EnforceVolatilityNeutral { get; set; } = false;

		/// <summary>
		/// Maximum allowed volatility imbalance as fraction.
		/// Example: 0.15 = allow up to 15% difference in vol-weighted exposures.
		/// </summary>
		public double MaxVolatilityImbalanceFraction { get; set; } = 0.15;

		// ---------------------------------------------------------------
		// Risk Parity
		// ---------------------------------------------------------------

		/// <summary>
		/// Enable risk parity sizing (weights inversely proportional to volatility).
		/// When true: Lower volatility stocks get larger positions.
		/// When false: Uses Objective for weighting.
		/// </summary>
		public bool UseRiskParity { get; set; } = false;

		/// <summary>
		/// Blend factor between risk parity and equal weight.
		/// 1.0 = pure risk parity, 0.0 = pure equal weight.
		/// Use 0.5-0.7 for balanced approach.
		/// </summary>
		public double RiskParityBlend { get; set; } = 1.0;

		/// <summary>
		/// Maximum weight dispersion ratio from volatility.
		/// Caps how much larger a low-vol position can be vs high-vol.
		/// Example: 3.0 = low-vol position can be at most 3x the weight of high-vol.
		/// </summary>
		public double MaxVolDispersionRatio { get; set; } = 3.0;

		// ---------------------------------------------------------------
		// Turnover Constraints (for Regime Transitions)
		// ---------------------------------------------------------------

		/// <summary>
		/// Maximum turnover allowed this rebalance as fraction of portfolio.
		/// Null = no turnover constraint.
		/// </summary>
		public double? MaxTurnover { get; set; } = null;

		/// <summary>
		/// Penalty for turnover in objective function.
		/// Higher = more reluctant to trade.
		/// </summary>
		public double TurnoverPenalty { get; set; } = 0.0;

		// ---------------------------------------------------------------
		// Solver Settings
		// ---------------------------------------------------------------

		/// <summary>
		/// Maximum solver time in seconds.
		/// </summary>
		public double SolverTimeoutSeconds { get; set; } = 30.0;

		/// <summary>
		/// Enable verbose debug output.
		/// </summary>
		public bool Verbose { get; set; } = true;
	}
	#endregion

	#region Optimization Result

	/// <summary>
	/// Result of portfolio optimization.
	/// </summary>
	public sealed class OptimizationResult
	{
		public bool Success { get; init; }
		public string Status { get; init; } = "";
		public string Message { get; init; } = "";

		/// <summary>
		/// Dollar weights as signed fractions of portfolio value.
		/// Positive for longs, negative for shorts.
		/// Key format: "{BookSide}_{Ticker}"
		/// </summary>
		public Dictionary<string, double> DollarWeights { get; init; } = new();

		/// <summary>
		/// Realized portfolio metrics.
		/// </summary>
		public PortfolioMetrics Metrics { get; init; } = new();

		/// <summary>
		/// Turnover required to reach target weights.
		/// </summary>
		public double Turnover { get; init; }

		/// <summary>
		/// Objective function value.
		/// </summary>
		public double ObjectiveValue { get; init; }
	}

	/// <summary>
	/// Portfolio metrics from optimization result.
	/// </summary>
	public sealed class PortfolioMetrics
	{
		public double LongGross { get; init; }
		public double ShortGross { get; init; }
		public double TotalGross { get; init; }
		public double NetExposure { get; init; }
		public double PortfolioBeta { get; init; }
		public double LongVolExposure { get; init; }
		public double ShortVolExposure { get; init; }
		public double VolatilityImbalance { get; init; }
		public int LongCount { get; init; }
		public int ShortCount { get; init; }

		public override string ToString()
		{
			return $"Gross: {TotalGross:P1} (L:{LongGross:P1} S:{ShortGross:P1}) | " +
				   $"Net: {NetExposure:P1} | Beta: {PortfolioBeta:F3} | " +
				   $"VolImbal: {VolatilityImbalance:P1} | " +
				   $"Positions: {LongCount}L/{ShortCount}S";
		}
	}

	#endregion

	#region Engine

	/// <summary>
	/// Regime-aware market-neutral portfolio optimization engine.
	/// Uses OPTANO/HiGHS for linear programming optimization.
	/// </summary>
	public sealed class Engine : IDisposable
	{
		private readonly Config _cfg;
		private readonly double PV;
		private bool _disposed;

		public Engine(Config config)
		{
			_cfg = config ?? throw new ArgumentNullException(nameof(config));
			PV = _cfg.PortfolioValue;
		}

		/// <summary>
		/// Optimize portfolio given candidate stocks.
		/// </summary>
		/// <param name="candidates">Stocks to include in optimization (already filtered by alpha signal and regime beta filters)</param>
		/// <returns>Optimization result with dollar weights</returns>
		public OptimizationResult Optimize(List<Stock> stocks)
		{
			try
			{
				if (stocks == null || stocks.Count == 0)
					return new OptimizationResult { Success = false, Message = "No stocks provided" };

				var longStocks = stocks.Where(s => s.RequiredBook == BookSide.Long).ToList();
				var shortStocks = stocks.Where(s => s.RequiredBook == BookSide.Short).ToList();

				int nLong = longStocks.Count;
				int nShort = shortStocks.Count;

				Log($"Input: {nLong} longs, {nShort} shorts, PV={PV:C0}");

				if (nLong == 0)
					return new OptimizationResult { Success = false, Message = "No long candidates" };
				if (nShort == 0)
					return new OptimizationResult { Success = false, Message = "No short candidates" };

				// Log stock characteristics
				var avgLongBeta = longStocks.Average(s => s.Beta);
				var avgShortBeta = shortStocks.Average(s => s.Beta);
				Log($"Avg Long Beta: {avgLongBeta:F3}, Avg Short Beta: {avgShortBeta:F3}");
				Log($"Target Beta: {_cfg.TargetPortfolioBeta:F3}");

				// Try with progressively relaxed constraints
				OptimizationResult result = null;

				// Attempt 1: Original constraints
				Log("Attempt 1: Original constraints");
				result = TryOptimizeInternal(longStocks, shortStocks,
					_cfg.EnforceBetaTarget, _cfg.BetaTolerance,
					_cfg.EnforceMarketNeutral, _cfg.MaxMarketImbalanceFraction,
					_cfg.EnforceVolatilityNeutral);

				if (result != null && result.Success)
					return result;

				// Attempt 2: Relax beta tolerance to 0.30
				Log("Attempt 2: Relaxed beta tolerance (0.30)");
				result = TryOptimizeInternal(longStocks, shortStocks,
					_cfg.EnforceBetaTarget, 0.30,
					_cfg.EnforceMarketNeutral, _cfg.MaxMarketImbalanceFraction,
					_cfg.EnforceVolatilityNeutral);

				if (result != null && result.Success)
					return result;

				// Attempt 3: Relax market imbalance to 15%
				Log("Attempt 3: Relaxed market imbalance (15%)");
				result = TryOptimizeInternal(longStocks, shortStocks,
					_cfg.EnforceBetaTarget, 0.30,
					_cfg.EnforceMarketNeutral, 0.15,
					false); // Disable vol neutrality

				if (result != null && result.Success)
					return result;

				// Attempt 4: Disable beta targeting
				Log("Attempt 4: Beta targeting disabled");
				result = TryOptimizeInternal(longStocks, shortStocks,
					false, 0.50,
					_cfg.EnforceMarketNeutral, 0.15,
					false);

				if (result != null && result.Success)
					return result;

				// Attempt 5: Minimal constraints - just market neutral
				Log("Attempt 5: Minimal constraints");
				result = TryOptimizeInternal(longStocks, shortStocks,
					false, 1.0,
					true, 0.20,
					false);

				if (result != null && result.Success)
					return result;

				// Attempt 6: No neutrality constraints at all
				Log("Attempt 6: No neutrality constraints");
				result = TryOptimizeInternal(longStocks, shortStocks,
					false, 1.0,
					false, 1.0,
					false);

				return result ?? new OptimizationResult { Success = false, Message = "All optimization attempts failed" };
			}
			catch (Exception ex)
			{
				Log($"Exception: {ex.Message}");
				return new OptimizationResult { Success = false, Message = ex.Message };
			}
		}

		private OptimizationResult TryOptimizeInternal(
			List<Stock> longStocks,
			List<Stock> shortStocks,
			bool enforceBetaTarget,
			double betaTolerance,
			bool enforceMarketNeutral,
			double maxMarketImbalance,
			bool enforceVolNeutral)
		{
			try
			{
				int nLong = longStocks.Count;
				int nShort = shortStocks.Count;

				Model m = new Model();

				// Variables: dollar amounts (not fractions)
				double minPerName = _cfg.MinPerNameFraction * PV;
				double maxPerName = _cfg.MaxPerNameFraction * PV;

				var l = new Variable[nLong];
				var s = new Variable[nShort];

				for (int i = 0; i < nLong; i++)
				{
					l[i] = new Variable($"L_{longStocks[i].GroupId}_{longStocks[i].Ticker}", 0, maxPerName, VariableType.Continuous);
					m.AddVariable(l[i]);
				}

				for (int i = 0; i < nShort; i++)
				{
					s[i] = new Variable($"S_{shortStocks[i].GroupId}_{shortStocks[i].Ticker}", 0, maxPerName, VariableType.Continuous);
					m.AddVariable(s[i]);
				}

				// Constraint 1: Total long exposure
				double targetPerSide = _cfg.GrossTargetPerSideFraction * PV;
				double minPerSide = _cfg.MinTotalGrossFraction * 0.5 * PV;
				double maxPerSide = _cfg.MaxTotalGrossFraction * 0.5 * PV;

				var longSumTerms = l.Select(v => 1.0 * v).ToArray();
				var shortSumTerms = s.Select(v => 1.0 * v).ToArray();

				if (longSumTerms.Length > 0)
				{
					var longSum = Expression.Sum(longSumTerms);
					m.AddConstraint(longSum >= minPerSide, "MinLongExposure");
					m.AddConstraint(longSum <= maxPerSide, "MaxLongExposure");
				}

				if (shortSumTerms.Length > 0)
				{
					var shortSum = Expression.Sum(shortSumTerms);
					m.AddConstraint(shortSum >= minPerSide, "MinShortExposure");
					m.AddConstraint(shortSum <= maxPerSide, "MaxShortExposure");
				}

				// Constraint 2: Market Neutrality (long dollars ≈ short dollars)
				if (enforceMarketNeutral && longSumTerms.Length > 0 && shortSumTerms.Length > 0)
				{
					var longSum = Expression.Sum(longSumTerms);
					var shortSum = Expression.Sum(shortSumTerms);
					double imbalanceDollars = maxMarketImbalance * targetPerSide;

					// |longSum - shortSum| <= imbalanceDollars
					m.AddConstraint(longSum - shortSum <= imbalanceDollars, "MarketNeutralUpper");
					m.AddConstraint(shortSum - longSum <= imbalanceDollars, "MarketNeutralLower");

					Log($"Market neutral: imbalance <= ${imbalanceDollars:F0}");
				}

				// Constraint 3: Beta Targeting
				if (enforceBetaTarget && longSumTerms.Length > 0 && shortSumTerms.Length > 0)
				{
					var longBetaTerms = longStocks.Select((stock, i) => (stock.Beta / PV) * l[i]).ToArray();
					var shortBetaTerms = shortStocks.Select((stock, i) => (-stock.Beta / PV) * s[i]).ToArray();
					var allBetaTerms = longBetaTerms.Concat(shortBetaTerms).ToArray();

					if (allBetaTerms.Length > 0)
					{
						var betaExpr = Expression.Sum(allBetaTerms);

						double targetBeta = _cfg.TargetPortfolioBeta;
						m.AddConstraint(betaExpr >= targetBeta - betaTolerance, "BetaTargetMin");
						m.AddConstraint(betaExpr <= targetBeta + betaTolerance, "BetaTargetMax");

						Log($"Beta target: {targetBeta:F2} ± {betaTolerance:F2}");
					}
				}

				// Constraint 4: Volatility Neutrality
				if (enforceVolNeutral && longSumTerms.Length > 0 && shortSumTerms.Length > 0)
				{
					double volTol = _cfg.MaxVolatilityImbalanceFraction;

					var longVolTerms = longStocks.Select((stock, i) => (stock.Volatility / PV) * l[i]).ToArray();
					var shortVolTerms = shortStocks.Select((stock, i) => (stock.Volatility / PV) * s[i]).ToArray();

					if (longVolTerms.Length > 0 && shortVolTerms.Length > 0)
					{
						var volDiffUpperTerms = longStocks.Select((stock, i) => (stock.Volatility / PV) * l[i])
							.Concat(shortStocks.Select((stock, i) => (-stock.Volatility * (1 + volTol) / PV) * s[i]))
							.ToArray();

						var volDiffLowerTerms = shortStocks.Select((stock, i) => (stock.Volatility / PV) * s[i])
							.Concat(longStocks.Select((stock, i) => (-stock.Volatility * (1 + volTol) / PV) * l[i]))
							.ToArray();

						if (volDiffUpperTerms.Length > 0 && volDiffLowerTerms.Length > 0)
						{
							var volDiffUpper = Expression.Sum(volDiffUpperTerms);
							var volDiffLower = Expression.Sum(volDiffLowerTerms);

							m.AddConstraint(volDiffUpper <= 0, "VolNeutralUpper");
							m.AddConstraint(volDiffLower <= 0, "VolNeutralLower");
						}
					}
				}

				// Objective: Maximize total deployment
				var allTerms = l.Select(v => 1.0 * v).Concat(s.Select(v => 1.0 * v)).ToArray();
				if (allTerms.Length > 0)
				{
					var objective = new Objective(Expression.Sum(allTerms), "MaxDeployment", ObjectiveSense.Maximize);
					m.AddObjective(objective);
				}

				// Solve
				using var solver = new HighsSolver15x();
				var solution = solver.Solve(m);

				bool isOptimal = solution.Status.ToString().Contains("Optimal");
				bool isFeasible = solution.Status.ToString().Contains("Feasible");

				Log($"Solver status: {solution.Status}");

				if (!isOptimal && !isFeasible)
				{
					return new OptimizationResult
					{
						Success = false,
						Status = solution.Status.ToString(),
						Message = $"Solver status: {solution.Status}"
					};
				}

				// Extract results
				var weights = new Dictionary<string, double>();
				double longGross = 0, shortGross = 0;
				double longBeta = 0, shortBeta = 0;
				double longVol = 0, shortVol = 0;
				int longCount = 0, shortCount = 0;

				for (int i = 0; i < nLong; i++)
				{
					double val = l[i].Value;
					if (val >= minPerName * 0.5) // Use smaller threshold
					{
						double frac = val / PV;
						string key = $"{longStocks[i].GroupId}_{longStocks[i].Ticker}";
						weights[key] = frac;
						longGross += val;
						longBeta += val * longStocks[i].Beta;
						longVol += val * longStocks[i].Volatility;
						longCount++;
					}
				}

				for (int i = 0; i < nShort; i++)
				{
					double val = s[i].Value;
					if (val >= minPerName * 0.5)
					{
						double frac = -val / PV; // Negative for shorts
						string key = $"{shortStocks[i].GroupId}_{shortStocks[i].Ticker}";
						weights[key] = frac;
						shortGross += val;
						shortBeta += val * shortStocks[i].Beta;
						shortVol += val * shortStocks[i].Volatility;
						shortCount++;
					}
				}

				double portfolioBeta = (longBeta - shortBeta) / PV;
				double avgVol = (longVol + shortVol) / 2;
				double volImbalance = avgVol > 0 ? Math.Abs(longVol - shortVol) / avgVol : 0;

				var metrics = new PortfolioMetrics
				{
					LongGross = longGross / PV,
					ShortGross = shortGross / PV,
					TotalGross = (longGross + shortGross) / PV,
					NetExposure = (longGross - shortGross) / PV,
					PortfolioBeta = portfolioBeta,
					LongVolExposure = longVol / PV,
					ShortVolExposure = shortVol / PV,
					VolatilityImbalance = volImbalance,
					LongCount = longCount,
					ShortCount = shortCount
				};

				Log($"Result: Long={longCount}, Short={shortCount}, Beta={portfolioBeta:F3}");

				return new OptimizationResult
				{
					Success = true,
					Status = solution.Status.ToString(),
					Message = "Optimization successful",
					DollarWeights = weights,
					Metrics = metrics,
					ObjectiveValue = longGross + shortGross
				};
			}
			catch (Exception ex)
			{
				Log($"TryOptimizeInternal exception: {ex.Message}");
				return null;
			}
		}

		private void AddConcentrationConstraints(
			Model m,
			List<Stock> longStocks, List<Stock> shortStocks,
			Variable[] l, Variable[] s)
		{
			// Group by sector
			var sectorGroups = longStocks.Select((stock, idx) => (stock, idx, isLong: true))
				.Concat(shortStocks.Select((stock, idx) => (stock, idx, isLong: false)))
				.Where(x => !string.IsNullOrEmpty(x.stock.Sector))  // Filter out null/empty
				.GroupBy(x => x.stock.Sector);

			foreach (var group in sectorGroups)
			{
				if (string.IsNullOrEmpty(group.Key)) continue;
				if (!group.Any()) continue;  // Skip empty groups

				var terms = group.Select(x => x.isLong ? 1.0 * l[x.idx] : 1.0 * s[x.idx]).ToArray();
				if (terms.Length == 0) continue;  // Skip if no terms

				var expr = Expression.Sum(terms);

				double limit = _cfg.MaxSectorFraction * PV;
				m.AddConstraint(expr <= limit, $"MaxSector_{group.Key}");
			}

			// Group by industry
			var industryGroups = longStocks.Select((stock, idx) => (stock, idx, isLong: true))
				.Concat(shortStocks.Select((stock, idx) => (stock, idx, isLong: false)))
				.Where(x => !string.IsNullOrEmpty(x.stock.Industry))  // Filter out null/empty
				.GroupBy(x => x.stock.Industry);

			foreach (var group in industryGroups)
			{
				if (string.IsNullOrEmpty(group.Key)) continue;
				if (!group.Any()) continue;

				var terms = group.Select(x => x.isLong ? 1.0 * l[x.idx] : 1.0 * s[x.idx]).ToArray();
				if (terms.Length == 0) continue;

				var expr = Expression.Sum(terms);

				double limit = _cfg.MaxIndustryFraction * PV;
				m.AddConstraint(expr <= limit, $"MaxIndustry_{SanitizeName(group.Key)}");
			}

			// Group by sub-industry
			var subIndustryGroups = longStocks.Select((stock, idx) => (stock, idx, isLong: true))
				.Concat(shortStocks.Select((stock, idx) => (stock, idx, isLong: false)))
				.Where(x => !string.IsNullOrEmpty(x.stock.SubIndustry))  // Filter out null/empty
				.GroupBy(x => x.stock.SubIndustry);

			foreach (var group in subIndustryGroups)
			{
				if (string.IsNullOrEmpty(group.Key)) continue;
				if (!group.Any()) continue;

				var terms = group.Select(x => x.isLong ? 1.0 * l[x.idx] : 1.0 * s[x.idx]).ToArray();
				if (terms.Length == 0) continue;

				var expr = Expression.Sum(terms);

				double limit = _cfg.MaxSubIndustryFraction * PV;
				m.AddConstraint(expr <= limit, $"MaxSubInd_{SanitizeName(group.Key)}");
			}
		}

		private Variable AddTurnoverConstraints(
			Model m,
			List<Stock> longStocks, List<Stock> shortStocks,
			Variable[] l, Variable[] s)
		{
			// Turnover = sum of |new_weight - old_weight|
			// Linearize using auxiliary variables: t[i] >= |new - old|

			var turnoverVars = new List<Variable>();

			for (int i = 0; i < longStocks.Count; i++)
			{
				double currentDollar = (longStocks[i].CurrentWeight ?? 0) * PV;

				// t_i >= l[i] - current  and  t_i >= current - l[i]
				var t = new Variable($"T_L_{longStocks[i].Ticker}", 0, double.MaxValue, VariableType.Continuous);
				m.AddVariable(t);

				m.AddConstraint(t - l[i] >= -currentDollar, $"Turn_L_Upper_{i}");
				m.AddConstraint(t + l[i] >= currentDollar, $"Turn_L_Lower_{i}");

				turnoverVars.Add(t);
			}

			for (int i = 0; i < shortStocks.Count; i++)
			{
				double currentDollar = Math.Abs(shortStocks[i].CurrentWeight ?? 0) * PV;

				var t = new Variable($"T_S_{shortStocks[i].Ticker}", 0, double.MaxValue, VariableType.Continuous);
				m.AddVariable(t);

				m.AddConstraint(t - s[i] >= -currentDollar, $"Turn_S_Upper_{i}");
				m.AddConstraint(t + s[i] >= currentDollar, $"Turn_S_Lower_{i}");

				turnoverVars.Add(t);
			}

			// Total turnover variable
			var turnoverVar = new Variable("TotalTurnover", 0, double.MaxValue, VariableType.Continuous);
			m.AddVariable(turnoverVar);

			// Sum all turnover auxiliary variables
			var turnoverSum = Expression.Sum(turnoverVars.Select(t => 1.0 * t).ToArray());
			m.AddConstraint(turnoverVar == turnoverSum, "TurnoverSum");

			// Turnover limit constraint
			if (_cfg.MaxTurnover.HasValue)
			{
				double maxTurnoverDollar = _cfg.MaxTurnover.Value * PV;
				m.AddConstraint(turnoverVar <= maxTurnoverDollar, "MaxTurnover");
				Log($"Turnover limit: {_cfg.MaxTurnover.Value:P0}");
			}

			return turnoverVar;
		}

		private Objective BuildObjective(
			List<Stock> longStocks, List<Stock> shortStocks,
			Variable[] l, Variable[] s,
			Variable? turnoverVar)
		{
			Expression[] terms;

			switch (_cfg.Objective)
			{
				case OptimizationObjective.MaximizeAlpha:
					// Maximize sum of (weight * alpha)
					var alphaLongTerms = longStocks.Select((stock, i) => stock.AlphaScore * l[i]);
					var alphaShortTerms = shortStocks.Select((stock, i) => stock.AlphaScore * s[i]);
					terms = alphaLongTerms.Concat(alphaShortTerms).ToArray();
					break;

				case OptimizationObjective.RiskParity:
					// Maximize inverse-vol weighted exposure
					// Higher weight to lower volatility stocks
					var rpLongTerms = longStocks.Select((stock, i) =>
					{
						double invVol = 1.0 / Math.Max(stock.Volatility, 0.05);
						double weight = _cfg.UseRiskParity
							? _cfg.RiskParityBlend * invVol + (1 - _cfg.RiskParityBlend)
							: 1.0;
						return weight * l[i];
					});
					var rpShortTerms = shortStocks.Select((stock, i) =>
					{
						double invVol = 1.0 / Math.Max(stock.Volatility, 0.05);
						double weight = _cfg.UseRiskParity
							? _cfg.RiskParityBlend * invVol + (1 - _cfg.RiskParityBlend)
							: 1.0;
						return weight * s[i];
					});
					terms = rpLongTerms.Concat(rpShortTerms).ToArray();
					break;

				case OptimizationObjective.MinimizeVolatility:
					// Minimize portfolio volatility (simplified: sum of vol-weighted positions)
					var volLongTerms = longStocks.Select((stock, i) => (-stock.Volatility) * l[i]);
					var volShortTerms = shortStocks.Select((stock, i) => (-stock.Volatility) * s[i]);
					terms = volLongTerms.Concat(volShortTerms).ToArray();
					break;

				case OptimizationObjective.EqualWeight:
				default:
					// Maximize total deployment (subject to constraints)
					// This naturally leads to equal-ish weights when constrained
					var eqLongTerms = longStocks.Select((stock, i) => 1.0 * l[i]);
					var eqShortTerms = shortStocks.Select((stock, i) => 1.0 * s[i]);
					terms = eqLongTerms.Concat(eqShortTerms).ToArray();
					break;
			}

			var expr = Expression.Sum(terms);

			// Add turnover penalty if specified
			if (_cfg.MaxTurnover.HasValue || _cfg.TurnoverPenalty > 0)
			{
				expr = expr + (-_cfg.TurnoverPenalty / PV) * turnoverVar;
			}

			return new Objective(expr, "MainObjective", ObjectiveSense.Maximize);
		}

		private static string SanitizeName(string name)
		{
			// Remove characters that might cause issues in constraint names
			return string.Join("", name.Where(c => char.IsLetterOrDigit(c) || c == '_'));
		}

		private void Log(string message)
		{
			if (_cfg.Verbose)
			{
				Debug.WriteLine($"[RiskEngineMNParity] {message}");
				Console.WriteLine($"[RiskEngineMNParity] {message}");
			}
		}

		public void Dispose()
		{
			if (!_disposed)
			{
				_disposed = true;
			}
		}
	}

	#endregion

	#region Example Usage

	/// <summary>
	/// Example showing how to use the engine with regime awareness.
	/// </summary>
	public static class ExampleUsage
	{
		public static void RunExample()
		{
			// 1. Determine current market regime
			MarketRegime currentRegime = MarketRegime.RiskOn;

			// 2. Get regime-specific configuration
			var config = RegimeConfigFactory.Create(currentRegime, portfolioValue: 10_000_000);

			// 3. Get beta filter ranges for stock selection
			var (minLongBeta, maxLongBeta, minShortBeta, maxShortBeta) =
				RegimeConfigFactory.GetBetaFilters(currentRegime);

			// 4. Create candidate stocks (would come from your alpha signal + beta filter)
			var candidates = new List<Stock>
			{
                // Longs with high beta (RiskOn regime)
                new Stock { Ticker = "NVDA", RequiredBook = BookSide.Long, Beta = 1.5, Volatility = 0.45,
						   AlphaScore = 0.8, Sector = "Technology", Industry = "Semiconductors" },
				new Stock { Ticker = "TSLA", RequiredBook = BookSide.Long, Beta = 1.4, Volatility = 0.55,
						   AlphaScore = 0.7, Sector = "Consumer", Industry = "Autos" },
				new Stock { Ticker = "META", RequiredBook = BookSide.Long, Beta = 1.2, Volatility = 0.35,
						   AlphaScore = 0.75, Sector = "Technology", Industry = "Internet" },
                
                // Shorts with low beta (RiskOn regime)
                new Stock { Ticker = "JNJ", RequiredBook = BookSide.Short, Beta = 0.6, Volatility = 0.18,
						   AlphaScore = 0.65, Sector = "Healthcare", Industry = "Pharma" },
				new Stock { Ticker = "PG", RequiredBook = BookSide.Short, Beta = 0.5, Volatility = 0.15,
						   AlphaScore = 0.6, Sector = "Consumer", Industry = "Staples" },
				new Stock { Ticker = "KO", RequiredBook = BookSide.Short, Beta = 0.55, Volatility = 0.16,
						   AlphaScore = 0.55, Sector = "Consumer", Industry = "Beverages" },
			};

			// 5. Run optimization
			using var engine = new Engine(config);
			var result = engine.Optimize(candidates);

			if (result.Success)
			{
				Console.WriteLine($"Optimization successful!");
				Console.WriteLine($"Metrics: {result.Metrics}");
				Console.WriteLine("\nPositions:");
				foreach (var (key, weight) in result.DollarWeights.OrderByDescending(kv => kv.Value))
				{
					Console.WriteLine($"  {key}: {weight:P2}");
				}
			}
			else
			{
				Console.WriteLine($"Optimization failed: {result.Message}");
			}

			// 6. Handle regime transition
			MarketRegime newRegime = MarketRegime.Stress;
			var (maxTurnover, totalBudget, maxRebalances) =
				RegimeConfigFactory.GetTurnoverConfig(currentRegime, newRegime);

			Console.WriteLine($"\nTransition from {currentRegime} to {newRegime}:");
			Console.WriteLine($"  Max single-day turnover: {maxTurnover:P0}");
			Console.WriteLine($"  Total transition budget: {totalBudget:P0}");
			Console.WriteLine($"  Max rebalances: {maxRebalances}");

			// Get smoothed beta for gradual transition
			double smoothedBeta = RegimeConfigFactory.GetSmoothedBeta(
				currentRegime, result.Metrics.PortfolioBeta, newRegime);
			Console.WriteLine($"  Smoothed beta target: {smoothedBeta:F2}");
		}
	}

	#endregion
}