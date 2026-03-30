// RegimeConfig.cs
// Regime-specific configuration for stock selection beta filters
// This file contains the regime-aware selection logic that sits BEFORE the optimizer

using System;
using System.Collections.Generic;
using System.Linq;

namespace RiskEngineMNParity
{

	public enum BookSide { Long, Short }

	#region Regime Configuration Factory

	/// <summary>
	/// Factory for creating regime-specific configurations.
	/// </summary>
	public static class RegimeConfigFactory
	{
		/// <summary>
		/// Create a Config tailored for the specified market regime.
		/// </summary>
		public static Config Create(MarketRegime regime, double portfolioValue = 1_000_000)
		{
			return regime switch
			{
				MarketRegime.HighRiskOn => new Config
				{
					PortfolioValue = portfolioValue,
					Objective = OptimizationObjective.MaximizeAlpha,

					// Full leverage
					GrossTargetPerSideFraction = 1.0,
					MaxTotalGrossFraction = 2.0,
					MinTotalGrossFraction = 1.6,

					// Standard concentration
					MaxPerNameFraction = 0.10,
					MaxSectorFraction = 0.12,
					MaxIndustryFraction = 0.12,
					MaxSubIndustryFraction = 0.12,

					// Market neutral always
					EnforceMarketNeutral = true,
					MaxMarketImbalanceFraction = 0.05,

					// Strong positive beta target
					EnforceBetaTarget = true,
					TargetPortfolioBeta = +0.35,
					BetaTolerance = 0.10,

					// Vol neutral optional when confident
					EnforceVolatilityNeutral = false,

					UseRiskParity = false,
					Verbose = false
				},

				MarketRegime.RiskOn => new Config
				{
					PortfolioValue = portfolioValue,
					Objective = OptimizationObjective.MaximizeAlpha,

					GrossTargetPerSideFraction = 1.0,
					MaxTotalGrossFraction = 2.0,
					MinTotalGrossFraction = 1.6,

					MaxPerNameFraction = 0.10,
					MaxSectorFraction = 0.12,
					MaxIndustryFraction = 0.12,
					MaxSubIndustryFraction = 0.12,

					EnforceMarketNeutral = true,
					MaxMarketImbalanceFraction = 0.05,

					// Moderate positive beta
					EnforceBetaTarget = true,
					TargetPortfolioBeta = +0.15,
					BetaTolerance = 0.08,

					// Vol neutral for smoother P&L
					EnforceVolatilityNeutral = true,
					MaxVolatilityImbalanceFraction = 0.10,

					UseRiskParity = false,
					Verbose = false
				},

				MarketRegime.RiskOff => new Config
				{
					PortfolioValue = portfolioValue,
					Objective = OptimizationObjective.MaximizeAlpha,

					// Slightly reduced leverage
					GrossTargetPerSideFraction = 0.9,
					MaxTotalGrossFraction = 1.8,
					MinTotalGrossFraction = 1.4,

					// Tighter concentration
					MaxPerNameFraction = 0.08,
					MaxSectorFraction = 0.10,
					MaxIndustryFraction = 0.10,
					MaxSubIndustryFraction = 0.10,

					EnforceMarketNeutral = true,
					MaxMarketImbalanceFraction = 0.05,

					// Moderate negative beta
					EnforceBetaTarget = true,
					TargetPortfolioBeta = -0.15,
					BetaTolerance = 0.08,

					// Vol neutral to balance magnitude
					EnforceVolatilityNeutral = true,
					MaxVolatilityImbalanceFraction = 0.10,

					UseRiskParity = false,
					Verbose = false
				},

				MarketRegime.Stress => new Config
				{
					PortfolioValue = portfolioValue,
					Objective = OptimizationObjective.MaximizeAlpha,

					// Reduced leverage
					GrossTargetPerSideFraction = 0.75,
					MaxTotalGrossFraction = 1.5,
					MinTotalGrossFraction = 1.2,

					// Tightest concentration
					MaxPerNameFraction = 0.06,
					MaxSectorFraction = 0.08,
					MaxIndustryFraction = 0.08,
					MaxSubIndustryFraction = 0.08,

					EnforceMarketNeutral = true,
					MaxMarketImbalanceFraction = 0.05,

					// Strong negative beta
					EnforceBetaTarget = true,
					TargetPortfolioBeta = -0.35,
					BetaTolerance = 0.08,

					// Vol neutral critical in stress
					EnforceVolatilityNeutral = true,
					MaxVolatilityImbalanceFraction = 0.08,

					UseRiskParity = false,
					Verbose = false
				},

				_ => throw new ArgumentException($"Unknown regime: {regime}")
			};
		}

		/// <summary>
		/// Get stock selection beta filter ranges for a regime.
		/// </summary>
		public static (double MinLongBeta, double MaxLongBeta, double MinShortBeta, double MaxShortBeta)
			GetBetaFilters(MarketRegime regime)
		{
			return regime switch
			{
				MarketRegime.RiskOn => (1.35, 6.00, 0.00, 0.65),    // MarketRegime.RiskOn => (1.0, 1.6, 0.5, 0.8),
				MarketRegime.RiskOff => (0.00, 0.90, 1.35, 6.00),    // MarketRegime.RiskOff => (0.7, 1.0, 1.0, 1.4),
			};
		}

		/// <summary>
		/// Get turnover configuration for a regime transition.
		/// </summary>
		public static (double MaxSingleDayTurnover, double TotalBudget, int MaxRebalances)
			GetTurnoverConfig(MarketRegime from, MarketRegime to)
		{
			// Immediate full repositioning into Stress
			if (to == MarketRegime.Stress)
			{
				return (0.70, 1.00, 1);
			}

			int distance = GetRegimeDistance(from, to);

			return distance switch
			{
				1 => (0.30, 0.50, 2),
				2 => (0.50, 0.80, 2),
				_ => (0.60, 0.90, 2)
			};
		}

		/// <summary>
		/// Get the "distance" between two regimes (0-3).
		/// </summary>
		public static int GetRegimeDistance(MarketRegime from, MarketRegime to)
		{
			return Math.Abs(RegimeToIndex(from) - RegimeToIndex(to));
		}

		/// <summary>
		/// Map regime to ordinal index for distance calculation.
		/// </summary>
		public static int RegimeToIndex(MarketRegime regime) => regime switch
		{
			MarketRegime.Stress => 0,
			MarketRegime.RiskOff => 1,
			MarketRegime.RiskOn => 2,
			MarketRegime.HighRiskOn => 3,
			_ => 1
		};

		/// <summary>
		/// Calculate smoothed beta target for regime transitions.
		/// </summary>
		public static double GetSmoothedBeta(
			MarketRegime currentRegime, double currentBeta,
			MarketRegime newRegime)
		{
			var newConfig = Create(newRegime);
			double rawTarget = newConfig.TargetPortfolioBeta;
			int distance = GetRegimeDistance(currentRegime, newRegime);

			// Immediate move into Stress
			if (newRegime == MarketRegime.Stress)
			{
				return rawTarget;
			}

			// Gradual transition based on distance
			double blendFactor = distance switch
			{
				1 => 0.70,  // Adjacent: 70% toward target
				2 => 0.50,  // Skip one: 50% toward target
				_ => 0.40   // Coming out of stress: 40% step
			};

			return (1 - blendFactor) * currentBeta + blendFactor * rawTarget;
		}
	}

	#endregion

	#region Example Usage

	/// <summary>
	/// Example showing how to use the engine with regime awareness.
	/// </summary>
	//public static class ExampleUsage
	//{
	//	public static void RunExample()
	//	{
	//		// 1. Determine current market regime
	//		MarketRegime currentRegime = MarketRegime.RiskOn;

	//		// 2. Get regime-specific configuration
	//		var config = RegimeConfigFactory.Create(currentRegime, portfolioValue: 10_000_000);

	//		// 3. Get beta filter ranges for stock selection
	//		var (minLongBeta, maxLongBeta, minShortBeta, maxShortBeta) =
	//			RegimeConfigFactory.GetBetaFilters(currentRegime);

	//		// 4. Create candidate stocks (would come from your alpha signal + beta filter)
	//		var candidates = new List<Stock>
	//		{
 //               // Longs with high beta (RiskOn regime)
 //               new Stock { Ticker = "NVDA", RequiredBook = BookSide.Long, Beta = 1.5, Volatility = 0.45,
	//					   AlphaScore = 0.8, Sector = "Technology", Industry = "Semiconductors" },
	//			new Stock { Ticker = "TSLA", RequiredBook = BookSide.Long, Beta = 1.4, Volatility = 0.55,
	//					   AlphaScore = 0.7, Sector = "Consumer", Industry = "Autos" },
	//			new Stock { Ticker = "META", RequiredBook = BookSide.Long, Beta = 1.2, Volatility = 0.35,
	//					   AlphaScore = 0.75, Sector = "Technology", Industry = "Internet" },
                
 //               // Shorts with low beta (RiskOn regime)
 //               new Stock { Ticker = "JNJ", RequiredBook = BookSide.Short, Beta = 0.6, Volatility = 0.18,
	//					   AlphaScore = 0.65, Sector = "Healthcare", Industry = "Pharma" },
	//			new Stock { Ticker = "PG", RequiredBook = BookSide.Short, Beta = 0.5, Volatility = 0.15,
	//					   AlphaScore = 0.6, Sector = "Consumer", Industry = "Staples" },
	//			new Stock { Ticker = "KO", RequiredBook = BookSide.Short, Beta = 0.55, Volatility = 0.16,
	//					   AlphaScore = 0.55, Sector = "Consumer", Industry = "Beverages" },
	//		};

	//		// 5. Run optimization
	//		using var engine = new Engine(config);
	//		var result = engine.Optimize(candidates);

	//		if (result.Success)
	//		{
	//			Console.WriteLine($"Optimization successful!");
	//			Console.WriteLine($"Metrics: {result.Metrics}");
	//			Console.WriteLine("\nPositions:");
	//			foreach (var (key, weight) in result.DollarWeights.OrderByDescending(kv => kv.Value))
	//			{
	//				Console.WriteLine($"  {key}: {weight:P2}");
	//			}
	//		}
	//		else
	//		{
	//			Console.WriteLine($"Optimization failed: {result.Message}");
	//		}

	//		// 6. Handle regime transition
	//		MarketRegime newRegime = MarketRegime.Stress;
	//		var (maxTurnover, totalBudget, maxRebalances) =
	//			RegimeConfigFactory.GetTurnoverConfig(currentRegime, newRegime);

	//		Console.WriteLine($"\nTransition from {currentRegime} to {newRegime}:");
	//		Console.WriteLine($"  Max single-day turnover: {maxTurnover:P0}");
	//		Console.WriteLine($"  Total transition budget: {totalBudget:P0}");
	//		Console.WriteLine($"  Max rebalances: {maxRebalances}");

	//		// Get smoothed beta for gradual transition
	//		double smoothedBeta = RegimeConfigFactory.GetSmoothedBeta(
	//			currentRegime, result.Metrics.PortfolioBeta, newRegime);
	//		Console.WriteLine($"  Smoothed beta target: {smoothedBeta:F2}");
	//	}
	//}

	#endregion


	/// <summary>
	/// Configuration for stock selection based on regime.
	/// Applied BEFORE passing candidates to the optimizer.
	/// </summary>
	public sealed class RegimeSelectionConfig
	{
		public MarketRegime Regime { get; init; }

		/// <summary>
		/// Minimum beta for long candidates.
		/// </summary>
		public double MinLongBeta { get; init; }

		/// <summary>
		/// Maximum beta for long candidates.
		/// </summary>
		public double MaxLongBeta { get; init; }

		/// <summary>
		/// Minimum beta for short candidates.
		/// </summary>
		public double MinShortBeta { get; init; }

		/// <summary>
		/// Maximum beta for short candidates.
		/// </summary>
		public double MaxShortBeta { get; init; }

		/// <summary>
		/// Grace period (in rebalances) for positions that no longer fit beta filter.
		/// </summary>
		public int ToleranceGracePeriod { get; init; }

		/// <summary>
		/// Check if a stock passes the beta filter for its book side.
		/// </summary>
		public bool PassesBetaFilter(Stock stock)
		{
			if (stock.RequiredBook == BookSide.Long)
			{
				return stock.Beta >= MinLongBeta && stock.Beta <= MaxLongBeta;
			}
			else
			{
				return stock.Beta >= MinShortBeta && stock.Beta <= MaxShortBeta;
			}
		}

		/// <summary>
		/// Check if a stock is "directionally wrong" for this regime.
		/// Used to prioritize exits during transitions.
		/// </summary>
		public bool IsDirectionallyWrong(Stock stock, double targetBeta)
		{
			// High-beta long in bearish regime (negative target beta)
			if (stock.RequiredBook == BookSide.Long && stock.Beta > 1.2 && targetBeta < 0)
				return true;

			// Low-beta short in bullish regime (positive target beta)
			if (stock.RequiredBook == BookSide.Short && stock.Beta < 0.8 && targetBeta > 0)
				return true;

			return false;
		}
	}

	/// <summary>
	/// Factory for regime-specific selection configurations.
	/// </summary>
	public static class RegimeSelectionConfigFactory
	{
		public static RegimeSelectionConfig Create(MarketRegime regime)
		{
			return regime switch
			{
				//MarketRegime.HighRiskOn => new RegimeSelectionConfig
				//{
				//	Regime = MarketRegime.HighRiskOn,
				//	MinLongBeta = 1.2,
				//	MaxLongBeta = 2.0,
				//	MinShortBeta = 0.3,
				//	MaxShortBeta = 0.6,
				//	ToleranceGracePeriod = 3
				//},

				MarketRegime.RiskOn => new RegimeSelectionConfig
				{
					Regime = MarketRegime.RiskOn,
					MinLongBeta = 1.35,
					MaxLongBeta = 1.35,
					MinShortBeta = 0.65,
					MaxShortBeta = 0.65,
					ToleranceGracePeriod = 0
				},

				//MarketRegime.RiskOn => new RegimeSelectionConfig
				//{
				//	Regime = MarketRegime.RiskOn,
				//	MinLongBeta = 1.0,
				//	MaxLongBeta = 1.6,
				//	MinShortBeta = 0.5,
				//	MaxShortBeta = 0.8,
				//	ToleranceGracePeriod = 2
				//},

				MarketRegime.RiskOff => new RegimeSelectionConfig
				{
					Regime = MarketRegime.RiskOff,
					MinLongBeta = 0.9,
					MaxLongBeta = 0.9,
					MinShortBeta = 1.35,
					MaxShortBeta = 1.35,
					ToleranceGracePeriod = 0
				},

				//MarketRegime.RiskOff => new RegimeSelectionConfig
				//{
				//	Regime = MarketRegime.RiskOff,
				//	MinLongBeta = 0.7,
				//	MaxLongBeta = 1.0,
				//	MinShortBeta = 1.0,
				//	MaxShortBeta = 1.4,
				//	ToleranceGracePeriod = 2
				//},

				MarketRegime.Stress => new RegimeSelectionConfig
				{
					Regime = MarketRegime.Stress,
					MinLongBeta = 0.4,
					MaxLongBeta = 0.8,
					MinShortBeta = 1.2,
					MaxShortBeta = 2.0,
					ToleranceGracePeriod = 0  // No tolerance in Stress
				},

				_ => throw new ArgumentException($"Unknown regime: {regime}")
			};
		}

		/// <summary>
		/// Get all regime configurations for reference.
		/// </summary>
		public static IReadOnlyDictionary<MarketRegime, RegimeSelectionConfig> GetAllConfigs()
		{
			return new Dictionary<MarketRegime, RegimeSelectionConfig>
			{
				[MarketRegime.HighRiskOn] = Create(MarketRegime.HighRiskOn),
				[MarketRegime.RiskOn] = Create(MarketRegime.RiskOn),
				[MarketRegime.RiskOff] = Create(MarketRegime.RiskOff),
				[MarketRegime.Stress] = Create(MarketRegime.Stress)
			};
		}
	}

	/// <summary>
	/// Summary of regime characteristics for documentation/UI.
	/// </summary>
	public static class RegimeSummary
	{
		public static void PrintSummary()
		{
			Console.WriteLine("=== REGIME CONFIGURATION SUMMARY ===\n");
			Console.WriteLine($"{"Regime",-15} {"Target β",-10} {"Long β",-12} {"Short β",-12} {"Max Gross",-10} {"Max Pos",-10}");
			Console.WriteLine(new string('-', 75));

			var regimes = new[] { MarketRegime.HighRiskOn, MarketRegime.RiskOn, MarketRegime.RiskOff, MarketRegime.Stress };

			foreach (var regime in regimes)
			{
				var optConfig = RegimeConfigFactory.Create(regime);
				var selConfig = RegimeSelectionConfigFactory.Create(regime);

				Console.WriteLine($"{regime,-15} {optConfig.TargetPortfolioBeta,+6:F2}     " +
								  $"{selConfig.MinLongBeta:F1}-{selConfig.MaxLongBeta:F1}      " +
								  $"{selConfig.MinShortBeta:F1}-{selConfig.MaxShortBeta:F1}      " +
								  $"{optConfig.MaxTotalGrossFraction:P0,-10} " +
								  $"{optConfig.MaxPerNameFraction:P0}");
			}

			Console.WriteLine("\n=== TRANSITION MATRIX ===\n");
			Console.WriteLine($"{"From \\ To",-15} {"HighRiskOn",-12} {"RiskOn",-12} {"RiskOff",-12} {"Stress",-12}");
			Console.WriteLine(new string('-', 65));

			foreach (var from in regimes)
			{
				Console.Write($"{from,-15}");
				foreach (var to in regimes)
				{
					if (from == to)
					{
						Console.Write($"{"--",-12}");
					}
					else
					{
						var (turnover, _, _) = RegimeConfigFactory.GetTurnoverConfig(from, to);
						Console.Write($"{turnover:P0,-12}");
					}
				}
				Console.WriteLine();
			}
		}
	}
}
