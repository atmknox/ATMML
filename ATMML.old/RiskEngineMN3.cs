
// RiskEngineMN3.Engine.cs
// Equal-weight market-neutral and beta-neutral portfolio with tolerances.
// OUTPUT: DollarWeights[key] is a SIGNED FRACTION of portfolio value
// (positive for longs, negative for shorts). Caller multiplies by PortfolioValue.

using OPTANO.Modeling.Optimization;
using OPTANO.Modeling.Optimization.Enums;
using OPTANO.Modeling.Optimization.Solver;
using OPTANO.Modeling.Optimization.Solver.Highs15x;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace RiskEngineMN3
{
	public enum BookSide { Long, Short }

	// ---------------------------------------------------------------
	// Stock descriptor
	// ---------------------------------------------------------------
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
		public BookSide RequiredBook { get; init; }

		public override string ToString()
		{
			return $"[{RequiredBook}] {Ticker,-12} | Sector: {Sector,-6} | " +
				   $"Industry: {Industry,-8} | Sub: {SubIndustry,-8}";
		}
	}

	// ---------------------------------------------------------------
	// Config
	// ---------------------------------------------------------------
	public sealed class Config
	{
		public double PortfolioValue { get; init; } = 1_000_000.0;

		// Absolute max per-name fraction of portfolio (e.g., 0.10 = 10%)
		public double MaxPerNameFraction { get; init; } = 0.10;

		// Target gross per side as fraction of portfolio (0.50 = 50% long, 50% short)
		public double GrossTargetPerSideFraction { get; init; } = 0.50;

		public double MaxTotalGrossFraction { get; init; } = 1.0;
		public double MinTotalGrossFraction { get; init; } = 0.80;

		/// <summary>
		/// Maximum allowed market imbalance as fraction of portfolio.
		/// Example: 0.05 = allow up to 5% difference between long and short gross.
		/// </summary>
		public double MaxMarketImbalanceFraction { get; init; } = 0.05;

		/// <summary>
		/// Enable market neutrality constraint (within tolerance).
		/// </summary>
		public bool EnforceMarketNeutral { get; init; } = true;

		/// <summary>
		/// Enable beta neutrality constraint (within tolerance).
		/// </summary>
		public bool EnforceBetaNeutral { get; init; } = true;

		/// <summary>
		/// Maximum allowed portfolio beta deviation from zero.
		/// Example: 0.05 = allow portfolio beta between -0.05 and +0.05.
		/// </summary>
		public double BetaTolerance { get; init; } = 0.05;
	}

	// ---------------------------------------------------------------
	// Core Engine
	// ---------------------------------------------------------------
	public sealed class Engine
	{
		private readonly IReadOnlyList<Stock> _stocks;
		private readonly Config _cfg;

		public Engine(IEnumerable<Stock> stocks, Config cfg)
		{
			_stocks = stocks?.ToList() ?? throw new ArgumentNullException(nameof(stocks));
			_cfg = cfg ?? throw new ArgumentNullException(nameof(cfg));

			if (_cfg.PortfolioValue <= 0.0)
				throw new ArgumentException("Config.PortfolioValue must be > 0.", nameof(cfg));

			if (_cfg.MaxPerNameFraction <= 0.0)
				throw new ArgumentException("Config.MaxPerNameFraction must be > 0.", nameof(cfg));
		}

		public (Dictionary<string, double> DollarWeights, double HedgeNotional) Solve()
		{
			if (_stocks.Count == 0)
				return (new Dictionary<string, double>(), 0.0);

			return SolveEqualWeight();
		}

		// -----------------------------------------------------------
		// Helper to create unique key for a stock
		// -----------------------------------------------------------
		private string MakeKey(Stock s)
		{
			// Create unique key - if GroupId exists, use it; otherwise use Ticker
			if (!string.IsNullOrEmpty(s.GroupId))
				return $"{s.GroupId} {s.Ticker}";

			return s.Ticker;
		}

		// -----------------------------------------------------------
		// Equal-weight sizing with market/beta neutral tolerances.
		// Outputs signed FRACTIONS of portfolio per position.
		// -----------------------------------------------------------
		private (Dictionary<string, double>, double) SolveEqualWeight()
		{
			var result = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
			double hedgeNotional = 0.0;

			Debug.WriteLine($"[MN3] ===== SIZING ALL STOCKS =====");
			Debug.WriteLine($"[MN3] Total input stocks: {_stocks.Count}");

			// CRITICAL FIX: Sort stocks deterministically to ensure consistent ordering across runs
			// This prevents different fractions on each run caused by non-deterministic iteration order
			var sortedStocks = _stocks
				.OrderBy(s => s.Ticker, StringComparer.OrdinalIgnoreCase)
				.ThenBy(s => s.GroupId ?? "", StringComparer.OrdinalIgnoreCase)
				.ThenBy(s => s.RequiredBook)
				.ToList();

			// Separate by RequiredBook
			var longStocks = new List<Stock>();
			var shortStocks = new List<Stock>();

			foreach (var stock in sortedStocks)
			{
				if (stock.RequiredBook == BookSide.Long)
				{
					longStocks.Add(stock);
				}
				else if (stock.RequiredBook == BookSide.Short)
				{
					shortStocks.Add(stock);
				}
				else
				{
					Debug.WriteLine($"[MN3] WARNING: Stock {stock.Ticker} has unrecognized RequiredBook={stock.RequiredBook}, treating as LONG");
					longStocks.Add(stock);
				}
			}

			int nLong = longStocks.Count;
			int nShort = shortStocks.Count;

			Debug.WriteLine($"[MN3] Classified: {nLong} longs, {nShort} shorts");

			bool hasLongs = nLong > 0;
			bool hasShorts = nShort > 0;

			if (!hasLongs && !hasShorts)
			{
				Debug.WriteLine("[MN3] ERROR: No stocks to size.");
				return (result, hedgeNotional);
			}

			// DYNAMIC MARKET NEUTRAL ENFORCEMENT:
			// If market neutrality is required but we only have one side, we need to be strategic:
			// - If we previously had both sides and one disappears, exit ONLY the side that can't be offset
			// - If the offsetting side reappears, immediately resize both sides
			// 
			// For now, we'll take the conservative approach: if we can't be market neutral, 
			// don't enter new positions. But we should allow IMMEDIATE re-entry when offset becomes available.
			//
			// Better approach: Only size what we CAN offset
			if (_cfg.EnforceMarketNeutral && (hasLongs != hasShorts))
			{
				Debug.WriteLine("[MN3] WARNING: Market neutrality required but only one side exists!");
				Debug.WriteLine($"[MN3] Has longs: {hasLongs} ({nLong}), Has shorts: {hasShorts} ({nShort})");
				Debug.WriteLine("[MN3] Strategy: Waiting for offsetting positions before sizing.");
				Debug.WriteLine("[MN3] Returning empty portfolio - will resize immediately when offset is available.");
				return (result, hedgeNotional);
			}

			// Total and average beta per side
			double totalLongBeta = hasLongs ? longStocks.Sum(s => s.Beta) : 0.0;
			double totalShortBeta = hasShorts ? shortStocks.Sum(s => s.Beta) : 0.0;
			double avgLongBeta = hasLongs ? totalLongBeta / nLong : 0.0;
			double avgShortBeta = hasShorts ? totalShortBeta / nShort : 0.0;

			Debug.WriteLine($"[MN3] Long beta:  total={totalLongBeta:F4}, avg={avgLongBeta:F4}");
			Debug.WriteLine($"[MN3] Short beta: total={totalShortBeta:F4}, avg={avgShortBeta:F4}");

			// Base target gross per side (fraction of portfolio)
			double targetGrossPerSide = _cfg.GrossTargetPerSideFraction;
			if (targetGrossPerSide <= 0.0)
			{
				targetGrossPerSide = 0.50;
			}

			double longGrossFrac;
			double shortGrossFrac;
			double maxAllowedImbalance = _cfg.MaxMarketImbalanceFraction;

			if (hasLongs && hasShorts)
			{
				// Both sides exist: apply market/beta neutrality constraints
				// Start with base gross allocation
				longGrossFrac = targetGrossPerSide;
				shortGrossFrac = targetGrossPerSide;

				// Apply per-name position limits FIRST (this is a hard constraint)
				double maxLongGrossFrac = nLong * _cfg.MaxPerNameFraction;
				double maxShortGrossFrac = nShort * _cfg.MaxPerNameFraction;

				longGrossFrac = Math.Min(longGrossFrac, maxLongGrossFrac);
				shortGrossFrac = Math.Min(shortGrossFrac, maxShortGrossFrac);

				Debug.WriteLine($"[MN3] After position limits: Long={longGrossFrac:P2}, Short={shortGrossFrac:P2}");

				// Now apply beta neutrality adjustment IF enabled and needed
				if (_cfg.EnforceBetaNeutral && Math.Abs(avgLongBeta - avgShortBeta) > 0.001)
				{
					// Ideal beta-neutral: longGross * avgLongBeta = shortGross * avgShortBeta
					// Start from the smaller side to avoid exceeding position limits
					double targetBetaExposure;

					if (longGrossFrac * avgLongBeta < shortGrossFrac * avgShortBeta)
					{
						// Long side has less beta exposure, adjust short side down
						targetBetaExposure = longGrossFrac * avgLongBeta;
						shortGrossFrac = targetBetaExposure / Math.Max(0.001, avgShortBeta);
						shortGrossFrac = Math.Min(shortGrossFrac, maxShortGrossFrac);
					}
					else
					{
						// Short side has less beta exposure, adjust long side down
						targetBetaExposure = shortGrossFrac * avgShortBeta;
						longGrossFrac = targetBetaExposure / Math.Max(0.001, avgLongBeta);
						longGrossFrac = Math.Min(longGrossFrac, maxLongGrossFrac);
					}

					Debug.WriteLine($"[MN3] After beta adjustment: Long={longGrossFrac:P2}, Short={shortGrossFrac:P2}");
				}

				// ENFORCE market neutral tolerance - this is MANDATORY if enabled
				if (_cfg.EnforceMarketNeutral)
				{
					double currentImbalance = Math.Abs(longGrossFrac - shortGrossFrac);

					if (currentImbalance > maxAllowedImbalance)
					{
						Debug.WriteLine($"[MN3] Market imbalance {currentImbalance:P2} exceeds tolerance {maxAllowedImbalance:P2}");

						// Force both sides to be equal at the lower of the two values
						double targetGross = Math.Min(longGrossFrac, shortGrossFrac);
						longGrossFrac = targetGross;
						shortGrossFrac = targetGross;

						Debug.WriteLine($"[MN3] FORCED market neutrality: Long={longGrossFrac:P2}, Short={shortGrossFrac:P2}");
					}
				}
			}
			else if (hasLongs)
			{
				// Only longs: invest one side; no neutrality.
				double maxLongGrossFrac = nLong * _cfg.MaxPerNameFraction;
				longGrossFrac = Math.Min(targetGrossPerSide, maxLongGrossFrac);
				shortGrossFrac = 0.0;
				Debug.WriteLine($"[MN3] Only long side present; sizing longs to {longGrossFrac:P2}");
			}
			else
			{
				// Only shorts: invest one side; no neutrality.
				double maxShortGrossFrac = nShort * _cfg.MaxPerNameFraction;
				longGrossFrac = 0.0;
				shortGrossFrac = Math.Min(targetGrossPerSide, maxShortGrossFrac);
				Debug.WriteLine($"[MN3] Only short side present; sizing shorts to {shortGrossFrac:P2}");
			}

			// Per-stock equal weights (fractions of portfolio)
			double longWeightPerStockFrac = hasLongs ? longGrossFrac / nLong : 0.0;
			double shortWeightPerStockFrac = hasShorts ? shortGrossFrac / nShort : 0.0;

			// CRITICAL VERIFICATION: Ensure per-name cap is NEVER exceeded
			if (hasLongs && longWeightPerStockFrac > _cfg.MaxPerNameFraction)
			{
				Debug.WriteLine($"[MN3] ✗✗✗ CRITICAL ERROR: Long weight per stock {longWeightPerStockFrac:P4} exceeds MaxPerNameFraction {_cfg.MaxPerNameFraction:P4}!");
				Debug.WriteLine($"[MN3] This should never happen. Forcing cap and reducing total long gross.");
				longWeightPerStockFrac = _cfg.MaxPerNameFraction;
				longGrossFrac = nLong * longWeightPerStockFrac;
			}

			if (hasShorts && shortWeightPerStockFrac > _cfg.MaxPerNameFraction)
			{
				Debug.WriteLine($"[MN3] ✗✗✗ CRITICAL ERROR: Short weight per stock {shortWeightPerStockFrac:P4} exceeds MaxPerNameFraction {_cfg.MaxPerNameFraction:P4}!");
				Debug.WriteLine($"[MN3] This should never happen. Forcing cap and reducing total short gross.");
				shortWeightPerStockFrac = _cfg.MaxPerNameFraction;
				shortGrossFrac = nShort * shortWeightPerStockFrac;
			}

			Debug.WriteLine($"[MN3] Final per-stock weights: Long={longWeightPerStockFrac:P4}, Short={shortWeightPerStockFrac:P4}");

			// -------------------------------------------------------
			// Assign weights to ALL stocks - CRITICAL SECTION
			// Each stock MUST get an entry in the result dictionary
			// -------------------------------------------------------
			Debug.WriteLine("[MN3] ===== ASSIGNING WEIGHTS TO ALL STOCKS =====");

			int longIndex = 0;
			foreach (var s in longStocks)
			{
				var key = MakeKey(s);

				// Check for duplicate keys
				if (result.ContainsKey(key))
				{
					Debug.WriteLine($"[MN3] ERROR: Duplicate key '{key}' detected! Previous value will be overwritten.");
				}

				result[key] = longWeightPerStockFrac;
				Debug.WriteLine($"[MN3] LONG  [{longIndex,3}] {key,-20} frac={longWeightPerStockFrac:P4}");
				longIndex++;
			}

			int shortIndex = 0;
			foreach (var s in shortStocks)
			{
				var key = MakeKey(s);

				// Check for duplicate keys
				if (result.ContainsKey(key))
				{
					Debug.WriteLine($"[MN3] ERROR: Duplicate key '{key}' detected! Previous value will be overwritten.");
				}

				result[key] = -shortWeightPerStockFrac;
				Debug.WriteLine($"[MN3] SHORT [{shortIndex,3}] {key,-20} frac={-shortWeightPerStockFrac:P4}");
				shortIndex++;
			}

			// CRITICAL VERIFICATION: Ensure every input stock got sized
			Debug.WriteLine("[MN3] ===== VERIFICATION =====");
			Debug.WriteLine($"[MN3] Input stocks:  {_stocks.Count}");
			Debug.WriteLine($"[MN3] Long stocks:   {nLong}");
			Debug.WriteLine($"[MN3] Short stocks:  {nShort}");
			Debug.WriteLine($"[MN3] Total sized:   {nLong + nShort}");
			Debug.WriteLine($"[MN3] Result count:  {result.Count}");

			if (result.Count != _stocks.Count)
			{
				Debug.WriteLine($"[MN3] ✗✗✗ ERROR: Result count {result.Count} != input count {_stocks.Count}!");
				Debug.WriteLine($"[MN3] ✗✗✗ This means {_stocks.Count - result.Count} stocks were NOT sized!");

				// Find which stocks are missing
				var resultKeys = new HashSet<string>(result.Keys, StringComparer.OrdinalIgnoreCase);
				for (int i = 0; i < _stocks.Count; i++)
				{
					var s = _stocks[i];
					var key = MakeKey(s);
					if (!resultKeys.Contains(key))
					{
						Debug.WriteLine($"[MN3] ✗✗✗ MISSING STOCK: {key} (RequiredBook={s.RequiredBook})");
					}
				}
			}
			else
			{
				Debug.WriteLine($"[MN3] ✓✓✓ SUCCESS: All {_stocks.Count} input stocks have been sized.");
			}

			// Portfolio metrics (fractions)
			double totalGrossFrac = longGrossFrac + shortGrossFrac;
			double marketImbalanceFrac = longGrossFrac - shortGrossFrac;
			double marketImbalanceAbsFrac = Math.Abs(marketImbalanceFrac);

			double portfolioBeta = 0.0;
			if (hasLongs)
			{
				portfolioBeta += longStocks.Sum(s => s.Beta * longWeightPerStockFrac);
			}
			if (hasShorts)
			{
				portfolioBeta -= shortStocks.Sum(s => s.Beta * shortWeightPerStockFrac);
			}

			Debug.WriteLine("[MN3] ===== FINAL PORTFOLIO =====");
			Debug.WriteLine($"[MN3] Long:  {nLong} positions × {longWeightPerStockFrac:P4} = {longGrossFrac:P2}");
			Debug.WriteLine($"[MN3] Short: {nShort} positions × {shortWeightPerStockFrac:P4} = {shortGrossFrac:P2}");
			Debug.WriteLine($"[MN3] Total Gross: {totalGrossFrac:P2}");
			Debug.WriteLine($"[MN3] Market Imbalance: {marketImbalanceFrac:P4}");
			Debug.WriteLine($"[MN3] Portfolio Beta: {portfolioBeta:F6}");
			Debug.WriteLine($"[MN3] Total Positions: {result.Count}");

			// CRITICAL CONSTRAINT VALIDATION
			Debug.WriteLine("[MN3] ===== CONSTRAINT VALIDATION =====");

			// Check per-position limits
			bool positionLimitViolation = false;
			if (hasLongs && longWeightPerStockFrac > _cfg.MaxPerNameFraction * 1.0001) // tiny tolerance for floating point
			{
				Debug.WriteLine($"[MN3] ✗✗✗ VIOLATION: Long position size {longWeightPerStockFrac:P4} > {_cfg.MaxPerNameFraction:P4} limit");
				positionLimitViolation = true;
			}
			else if (hasLongs)
			{
				Debug.WriteLine($"[MN3] ✓ Long position size: {longWeightPerStockFrac:P4} ≤ {_cfg.MaxPerNameFraction:P4} limit");
			}

			if (hasShorts && shortWeightPerStockFrac > _cfg.MaxPerNameFraction * 1.0001) // tiny tolerance for floating point
			{
				Debug.WriteLine($"[MN3] ✗✗✗ VIOLATION: Short position size {shortWeightPerStockFrac:P4} > {_cfg.MaxPerNameFraction:P4} limit");
				positionLimitViolation = true;
			}
			else if (hasShorts)
			{
				Debug.WriteLine($"[MN3] ✓ Short position size: {shortWeightPerStockFrac:P4} ≤ {_cfg.MaxPerNameFraction:P4} limit");
			}

			// Check market neutral tolerance
			if (_cfg.EnforceMarketNeutral && hasLongs && hasShorts)
			{
				if (marketImbalanceAbsFrac <= maxAllowedImbalance)
					Debug.WriteLine($"[MN3] ✓ Market neutral: |{marketImbalanceFrac:P4}| ≤ {maxAllowedImbalance:P4} tolerance");
				else
					Debug.WriteLine($"[MN3] ✗✗✗ VIOLATION: Market imbalance |{marketImbalanceFrac:P4}| > {maxAllowedImbalance:P4} tolerance");
			}

			// Check beta neutral tolerance
			if (_cfg.EnforceBetaNeutral && hasLongs && hasShorts)
			{
				if (Math.Abs(portfolioBeta) <= _cfg.BetaTolerance)
					Debug.WriteLine($"[MN3] ✓ Beta neutral: |{portfolioBeta:F4}| ≤ {_cfg.BetaTolerance:F4} tolerance");
				else
					Debug.WriteLine($"[MN3] ⚠ Beta neutral warning: |{portfolioBeta:F4}| > {_cfg.BetaTolerance:F4} tolerance");
			}

			if (positionLimitViolation || (_cfg.EnforceMarketNeutral && hasLongs && hasShorts && marketImbalanceAbsFrac > maxAllowedImbalance))
			{
				Debug.WriteLine("[MN3] ✗✗✗ CONSTRAINT VIOLATIONS DETECTED - PORTFOLIO IS INVALID");
			}
			else
			{
				Debug.WriteLine("[MN3] ✓✓✓ All constraints satisfied - portfolio is valid");
			}

			return (result, hedgeNotional);
		}
	}

	// ---------------------------------------------------------------
	// Compatibility wrapper
	// ---------------------------------------------------------------
	public sealed class OptimizeResult
	{
		public sealed class Position
		{
			public string GroupId { get; init; } = "";
			public string Ticker { get; init; } = "";

			/// <summary>
			/// Shares holds the SIGNED FRACTION of portfolio value
			/// (positive for longs, negative for shorts).
			/// </summary>
			public double Shares { get; init; }

			public override string ToString()
			{
				return $"[{(Shares >= 0 ? "LONG" : "SHORT")}] {Ticker} ({GroupId}): {Math.Abs(Shares):P4}";
			}
		}

		public Dictionary<string, double> DollarWeights { get; }
		public List<Position> Positions { get; }
		public double HedgeNotional { get; }

		public OptimizeResult(Dictionary<string, double> dollarWeights, double hedgeNotional)
		{
			DollarWeights = dollarWeights ?? new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
			HedgeNotional = hedgeNotional;

			Positions = DollarWeights
				.Select(kvp =>
				{
					var key = kvp.Key ?? "";
					int idx = key.IndexOf(' ');

					string groupId;
					string ticker;

					if (idx <= 0)
					{
						groupId = "";
						ticker = key;
					}
					else
					{
						groupId = key.Substring(0, idx);
						ticker = key.Substring(idx + 1);
					}

					return new Position
					{
						GroupId = groupId,
						Ticker = ticker,
						Shares = kvp.Value // signed fraction
					};
				})
				.ToList();
		}
	}

	// ---------------------------------------------------------------
	// Static entry points
	// ---------------------------------------------------------------
	public static class PortfolioOptimizer
	{
		public static OptimizeResult Optimize(IReadOnlyList<Stock> stocks, Config cfg)
		{
			var engine = new Engine(stocks, cfg);
			var (weights, hedgeNotional) = engine.Solve();
			return new OptimizeResult(weights, hedgeNotional);
		}

		public static OptimizeResult Optimize(
			IReadOnlyList<Stock> stocks,
			Config cfg,
			object constraints)
		{
			return Optimize(stocks, cfg);
		}
	}
}