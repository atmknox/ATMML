using System;
using System.Collections.Generic;
using System.Linq;

namespace RiskEngineMN3.ExecutionCosts
{
	// ============================================================================
	// DOMAIN MODELS
	// ============================================================================

	/// <summary>
	/// Position input for cost calculation
	/// </summary>
	public sealed class PositionInput
	{
		public string Ticker { get; init; } = "";
		public double Price { get; init; }
		public double CurrentWeight { get; init; }
		public double TargetWeight { get; init; }
		public double DailyVolatility { get; init; }
		public double AverageDailyVolumeShares { get; init; }
		public double ExpectedAlphaAnnual { get; init; }
		public double BidAskSpreadBps { get; init; } = 2.0;

		/// <summary>
		/// Annual borrow cost in bps for short positions.
		/// Typical: 25-50 bps for GC (general collateral), 100-500+ for hard-to-borrow.
		/// Set to 0 for longs or if using config default.
		/// </summary>
		public double BorrowCostAnnualBps { get; init; } = 0.0;

		/// <summary>
		/// True if this position involves shorting
		/// </summary>
		public bool IsShort => TargetWeight < 0 || (TargetWeight == 0 && CurrentWeight < 0);

		/// <summary>
		/// Quick validation
		/// </summary>
		public bool IsValid() =>
			!string.IsNullOrWhiteSpace(Ticker) &&
			Price > 0 &&
			DailyVolatility >= 0 &&
			AverageDailyVolumeShares >= 0;
	}

	/// <summary>
	/// Trade cost breakdown for a single position
	/// </summary>
	public sealed class PositionCostResult
	{
		public string Ticker { get; init; } = "";

		// Trade characteristics
		public double WeightChange { get; init; }
		public double TradeNotionalValue { get; init; }
		public double SharesTraded { get; init; }
		public double ParticipationRate { get; init; }
		public double EstimatedExecutionDays { get; init; }
		public bool IsShort { get; init; }

		// Cost components (in bps of trade notional)
		public double CommissionBps { get; init; }
		public double SpreadCostBps { get; init; }
		public double TemporaryImpactBps { get; init; }
		public double PermanentImpactBps { get; init; }
		public double TotalImpactBps { get; init; }
		public double BorrowCostBps { get; init; }
		public double TotalCostBps { get; init; }

		// Borrow rate (for reference)
		public double BorrowCostAnnualBps { get; init; }

		// Dollar amounts
		public double CommissionDollars { get; init; }
		public double SpreadCostDollars { get; init; }
		public double ImpactDollars { get; init; }
		public double BorrowCostDollars { get; init; }
		public double TotalCostDollars { get; init; }

		// Warnings
		public bool HasWarnings => Warnings.Length > 0;
		public string[] Warnings { get; init; } = Array.Empty<string>();
	}

	/// <summary>
	/// Portfolio-level cost summary
	/// </summary>
	public sealed class PortfolioCostResult
	{
		// Basic metrics
		public int TradeCount { get; init; }
		public int ShortCount { get; init; }
		public double TotalTurnoverNotional { get; init; }
		public double TotalCostDollars { get; init; }
		public double TotalCostBps { get; init; }

		// Portfolio return impact
		public double PortfolioReturnDragDecimal { get; init; }
		public double PortfolioReturnDragBps { get; init; }

		// Cost breakdown averages (all in bps of trade notional)
		public double AvgCommissionBps { get; init; }
		public double AvgSpreadBps { get; init; }
		public double AvgImpactBps { get; init; }
		public double AvgTotalCostBps { get; init; }

		// Borrow cost totals (separate from execution costs)
		public double TotalBorrowCostDollars { get; init; }
		public double TotalBorrowCostBps { get; init; }
		public double AvgShortBorrowCostBps { get; init; }

		// Warnings
		public int WarningCount { get; init; }
		public IReadOnlyList<PositionCostResult> Positions { get; init; } =
			Array.Empty<PositionCostResult>();
	}

	// ============================================================================
	// CONFIGURATION
	// ============================================================================

	/// <summary>
	/// Execution strategy type
	/// </summary>
	public enum ExecutionStrategy
	{
		Aggressive,           // Market orders (1.2x impact)
		VWAP,                 // Volume-weighted (0.9x impact)
		TWAP,                 // Time-weighted (0.85x impact)
		SmartRouter,          // Optimized routing (0.8x impact)
		Patient,              // Passive/limit orders (0.75x impact)
		MOC                   // Market-on-Close auction (0.85x impact)
	}

	/// <summary>
	/// Configuration for execution cost calculation
	/// OPTIMIZED FOR INSTITUTIONAL EXECUTION
	/// </summary>
	public sealed class ExecutionCostConfig
	{
		// Portfolio parameters
		public double PortfolioValueUsd { get; init; } = 100_000_000;

		// Commission structure (institutional rates)
		public double CommissionRateBps { get; init; } = 0.3;
		public double MinCommissionPerTrade { get; init; } = 1.0;
		public double MaxCommissionPerTrade { get; init; } = 5000.0;

		// Market impact parameters (calibrated to modern markets)
		public double ImpactCoefficientGamma { get; init; } = 0.20;
		public double PermanentImpactRatio { get; init; } = 0.25;

		// Spread capture
		public double SpreadCaptureRate { get; init; } = 0.30;

		// Execution constraints
		public double MaxParticipationRate { get; init; } = 0.15;
		public double MinTradeSizeShares { get; init; } = 1.0;

		// Borrow cost parameters
		/// <summary>
		/// Default annual borrow cost in bps for short positions.
		/// Applied when position-level BorrowCostAnnualBps is 0.
		/// Typical: 30 bps for easy-to-borrow mega-caps.
		/// </summary>
		public double DefaultBorrowCostAnnualBps { get; init; } = 30.0;

		/// <summary>
		/// Expected holding period in days for borrow cost proration.
		/// Used to calculate per-trade borrow cost from annual rate.
		/// </summary>
		public double HoldingPeriodDays { get; init; } = 7.0;

		/// <summary>
		/// Threshold for hard-to-borrow warning (annual bps)
		/// </summary>
		public double HardToBorrowThresholdAnnualBps { get; init; } = 100.0;

		// Strategy
		public ExecutionStrategy Strategy { get; init; } = ExecutionStrategy.MOC;

		// Validation
		public void Validate()
		{
			if (PortfolioValueUsd <= 0)
				throw new ArgumentException("Portfolio value must be positive");
			if (CommissionRateBps < 0)
				throw new ArgumentException("Commission rate cannot be negative");
			if (CommissionRateBps > 10.0)
				throw new ArgumentException("Commission rate > 10 bps is unrealistic");
			if (MaxParticipationRate <= 0 || MaxParticipationRate > 1)
				throw new ArgumentException("Max participation must be between 0 and 1");
			if (ImpactCoefficientGamma < 0.05 || ImpactCoefficientGamma > 1.0)
				throw new ArgumentException("Impact coefficient should be 0.05-1.0");
			if (DefaultBorrowCostAnnualBps < 0)
				throw new ArgumentException("Default borrow cost cannot be negative");
			if (HoldingPeriodDays <= 0)
				throw new ArgumentException("Holding period must be positive");
		}

		/// <summary>
		/// Get default configuration optimized for mega-cap S&P 500
		/// </summary>
		public static ExecutionCostConfig Default() => new ExecutionCostConfig();

		/// <summary>
		/// Get conservative configuration with higher cost estimates
		/// </summary>
		public static ExecutionCostConfig Conservative() => new ExecutionCostConfig
		{
			CommissionRateBps = 0.5,
			ImpactCoefficientGamma = 0.30,
			PermanentImpactRatio = 0.30,
			SpreadCaptureRate = 0.40,
			MaxParticipationRate = 0.10,
			DefaultBorrowCostAnnualBps = 50.0
		};

		/// <summary>
		/// Optimized for $100M mega-cap S&P 500, MOC execution, weekly rebalance
		/// </summary>
		public static ExecutionCostConfig MegaCapMOC() => new ExecutionCostConfig
		{
			PortfolioValueUsd = 100_000_000,
			CommissionRateBps = 0.3,
			ImpactCoefficientGamma = 0.15,
			PermanentImpactRatio = 0.20,
			SpreadCaptureRate = 0.35,
			DefaultBorrowCostAnnualBps = 30.0,
			HoldingPeriodDays = 7.0,
			MaxParticipationRate = 0.15,
			Strategy = ExecutionStrategy.MOC
		};
	}

	// ============================================================================
	// MARKET IMPACT CALCULATOR
	// ============================================================================

	/// <summary>
	/// Market impact calculations using square-root model
	/// Based on Almgren-Chriss framework with modern calibrations
	/// </summary>
	internal static class MarketImpactCalculator
	{
		/// <summary>
		/// Calculate temporary market impact in basis points
		/// Uses: Impact = gamma * volatility * sqrt(participation_rate) * strategy_multiplier
		/// </summary>
		public static double CalculateTemporaryImpactBps(
			double participationRate,
			double dailyVolatility,
			double gamma,
			double strategyMultiplier)
		{
			if (participationRate <= 0 || dailyVolatility <= 0)
				return 0.0;

			double impactBps = gamma * dailyVolatility * 10000.0 *
							   Math.Sqrt(participationRate) *
							   strategyMultiplier;

			return Math.Max(0, impactBps);
		}

		/// <summary>
		/// Calculate permanent market impact
		/// </summary>
		public static double CalculatePermanentImpactBps(
			double temporaryImpactBps,
			double permanentRatio)
		{
			return temporaryImpactBps * permanentRatio;
		}

		/// <summary>
		/// Get strategy multiplier
		/// </summary>
		public static double GetStrategyMultiplier(ExecutionStrategy strategy)
		{
			return strategy switch
			{
				ExecutionStrategy.Aggressive => 1.2,
				ExecutionStrategy.VWAP => 0.9,
				ExecutionStrategy.TWAP => 0.85,
				ExecutionStrategy.SmartRouter => 0.8,
				ExecutionStrategy.Patient => 0.75,
				ExecutionStrategy.MOC => 0.85,
				_ => 1.0
			};
		}

		/// <summary>
		/// Estimate execution days based on participation limit
		/// </summary>
		public static double EstimateExecutionDays(
			double shares,
			double avgDailyVolumeShares,
			double maxParticipationRate)
		{
			if (avgDailyVolumeShares <= 0 || shares <= 0)
				return 0;

			double maxDailyShares = avgDailyVolumeShares * maxParticipationRate;
			return Math.Ceiling(shares / maxDailyShares);
		}
	}

	// ============================================================================
	// COMMISSION CALCULATOR
	// ============================================================================

	/// <summary>
	/// Commission calculation with min/max constraints
	/// </summary>
	internal static class CommissionCalculator
	{
		public static double CalculateCommissionDollars(
			double tradeNotional,
			double rateBps,
			double minCommission,
			double maxCommission)
		{
			double commission = tradeNotional * (rateBps / 10000.0);
			commission = Math.Max(commission, minCommission);
			commission = Math.Min(commission, maxCommission);
			return commission;
		}

		public static double ConvertToBps(double commissionDollars, double tradeNotional)
		{
			if (tradeNotional <= 0) return 0;
			return (commissionDollars / tradeNotional) * 10000.0;
		}
	}

	// ============================================================================
	// BORROW COST CALCULATOR
	// ============================================================================

	/// <summary>
	/// Borrow cost calculation for short positions
	/// </summary>
	internal static class BorrowCostCalculator
	{
		/// <summary>
		/// Calculate borrow cost for a short position in dollars
		/// </summary>
		/// <param name="shortNotional">Absolute notional value of short position</param>
		/// <param name="annualBorrowBps">Annual borrow rate in basis points</param>
		/// <param name="holdingPeriodDays">Expected holding period in days</param>
		/// <returns>Borrow cost in dollars</returns>
		public static double CalculateBorrowCostDollars(
			double shortNotional,
			double annualBorrowBps,
			double holdingPeriodDays)
		{
			if (shortNotional <= 0 || annualBorrowBps <= 0 || holdingPeriodDays <= 0)
				return 0.0;

			double holdingPeriodYears = holdingPeriodDays / 365.0;
			double borrowCostDecimal = (annualBorrowBps / 10000.0) * holdingPeriodYears;
			return shortNotional * borrowCostDecimal;
		}

		/// <summary>
		/// Convert dollar cost to bps of a reference notional
		/// </summary>
		public static double ConvertToBps(double costDollars, double referenceNotional)
		{
			if (referenceNotional <= 0) return 0.0;
			return (costDollars / referenceNotional) * 10000.0;
		}
	}

	// ============================================================================
	// MAIN EXECUTION COST ENGINE
	// ============================================================================

	/// <summary>
	/// Production-grade execution cost calculator
	/// Designed for accurate historical backtesting and forward optimization
	/// Includes borrow cost support for market-neutral strategies
	/// </summary>
	public sealed class ExecutionCostEngine
	{
		private readonly ExecutionCostConfig _config;

		public ExecutionCostEngine(ExecutionCostConfig config)
		{
			_config = config ?? throw new ArgumentNullException(nameof(config));
			_config.Validate();
		}

		/// <summary>
		/// Calculate execution costs for portfolio rebalancing
		/// Returns portfolio-level cost summary with per-position details
		/// </summary>
		public PortfolioCostResult CalculateRebalancingCosts(
			IReadOnlyList<PositionInput> positions)
		{
			if (positions == null || positions.Count == 0)
			{
				return new PortfolioCostResult();
			}

			// Calculate costs for each position
			var positionResults = new List<PositionCostResult>();
			double totalCostDollars = 0;
			double totalTurnover = 0;
			double totalBorrowCostDollars = 0;
			int warningCount = 0;
			int shortCount = 0;

			foreach (var position in positions.Where(p => p.IsValid()))
			{
				var result = CalculatePositionCost(position);
				positionResults.Add(result);

				totalCostDollars += result.TotalCostDollars;
				totalTurnover += result.TradeNotionalValue;
				totalBorrowCostDollars += result.BorrowCostDollars;

				if (result.HasWarnings)
					warningCount++;

				if (result.IsShort)
					shortCount++;
			}

			// Portfolio-level metrics
			double portfolioReturnDragDecimal = totalCostDollars / _config.PortfolioValueUsd;
			double portfolioReturnDragBps = portfolioReturnDragDecimal * 10000.0;
			double totalCostBps = portfolioReturnDragBps;
			double totalBorrowCostBps = (totalBorrowCostDollars / _config.PortfolioValueUsd) * 10000.0;

			// Calculate averages (only for positions with actual trades)
			var tradedPositions = positionResults
				.Where(r => r.SharesTraded > 0)
				.ToList();

			// Short positions only for borrow cost average
			var shortPositions = tradedPositions
				.Where(r => r.IsShort && r.BorrowCostDollars > 0)
				.ToList();

			double avgCommBps = tradedPositions.Any()
				? tradedPositions.Average(r => r.CommissionBps) : 0;
			double avgSpreadBps = tradedPositions.Any()
				? tradedPositions.Average(r => r.SpreadCostBps) : 0;
			double avgImpactBps = tradedPositions.Any()
				? tradedPositions.Average(r => r.TotalImpactBps) : 0;
			double avgTotalBps = tradedPositions.Any()
				? tradedPositions.Average(r => r.TotalCostBps) : 0;

			// Average borrow cost only across short positions (not diluted by longs)
			double avgShortBorrowBps = shortPositions.Any()
				? shortPositions.Average(r => r.BorrowCostBps) : 0;

			return new PortfolioCostResult
			{
				TradeCount = tradedPositions.Count,
				ShortCount = shortCount,
				TotalTurnoverNotional = totalTurnover,
				TotalCostDollars = totalCostDollars,
				TotalCostBps = totalCostBps,
				PortfolioReturnDragDecimal = portfolioReturnDragDecimal,
				PortfolioReturnDragBps = portfolioReturnDragBps,
				AvgCommissionBps = avgCommBps,
				AvgSpreadBps = avgSpreadBps,
				AvgImpactBps = avgImpactBps,
				AvgTotalCostBps = avgTotalBps,
				TotalBorrowCostDollars = totalBorrowCostDollars,
				TotalBorrowCostBps = totalBorrowCostBps,
				AvgShortBorrowCostBps = avgShortBorrowBps,
				WarningCount = warningCount,
				Positions = positionResults
			};
		}

		/// <summary>
		/// Calculate cost for a single position trade
		/// </summary>
		private PositionCostResult CalculatePositionCost(PositionInput position)
		{
			var warnings = new List<string>();

			// Calculate trade details
			double weightChange = position.TargetWeight - position.CurrentWeight;
			double tradeNotional = Math.Abs(weightChange) * _config.PortfolioValueUsd;
			double shares = position.Price > 0 ? tradeNotional / position.Price : 0;

			// Initialize cost components
			double commissionDollars = 0;
			double commissionBps = 0;
			double spreadCostDollars = 0;
			double spreadCostBps = 0;
			double tempImpactBps = 0;
			double permImpactBps = 0;
			double impactDollars = 0;
			double participationRate = 0;
			double executionDays = 0;
			double borrowCostDollars = 0;
			double borrowCostBps = 0;
			double annualBorrowBps = 0;

			// Calculate costs only for meaningful trades
			if (shares >= _config.MinTradeSizeShares && tradeNotional > 0)
			{
				// Calculate participation and execution time
				if (position.AverageDailyVolumeShares > 0)
				{
					participationRate = shares / position.AverageDailyVolumeShares;
					executionDays = MarketImpactCalculator.EstimateExecutionDays(
						shares,
						position.AverageDailyVolumeShares,
						_config.MaxParticipationRate);

					// Check participation limit
					if (participationRate > _config.MaxParticipationRate)
					{
						warnings.Add($"Trade requires {participationRate:P1} of ADV " +
								   $"(limit: {_config.MaxParticipationRate:P1})");
					}

					// Calculate market impact using capped participation
					if (position.DailyVolatility > 0)
					{
						double effectiveParticipation = Math.Min(
							participationRate,
							_config.MaxParticipationRate);

						double strategyMult = MarketImpactCalculator
							.GetStrategyMultiplier(_config.Strategy);

						tempImpactBps = MarketImpactCalculator.CalculateTemporaryImpactBps(
							effectiveParticipation,
							position.DailyVolatility,
							_config.ImpactCoefficientGamma,
							strategyMult);

						permImpactBps = MarketImpactCalculator.CalculatePermanentImpactBps(
							tempImpactBps,
							_config.PermanentImpactRatio);

						impactDollars = tradeNotional *
									   ((tempImpactBps + permImpactBps) / 10000.0);
					}
				}

				// Calculate commission
				commissionDollars = CommissionCalculator.CalculateCommissionDollars(
					tradeNotional,
					_config.CommissionRateBps,
					_config.MinCommissionPerTrade,
					_config.MaxCommissionPerTrade);

				commissionBps = CommissionCalculator.ConvertToBps(
					commissionDollars,
					tradeNotional);

				// Calculate spread cost
				spreadCostBps = position.BidAskSpreadBps * _config.SpreadCaptureRate;
				spreadCostDollars = tradeNotional * (spreadCostBps / 10000.0);

				// Calculate borrow cost for short positions
				if (position.IsShort)
				{
					// Use position-specific borrow cost if provided, otherwise use default
					annualBorrowBps = position.BorrowCostAnnualBps > 0
						? position.BorrowCostAnnualBps
						: _config.DefaultBorrowCostAnnualBps;

					// Calculate on the short notional (target weight magnitude)
					double shortNotional = Math.Abs(position.TargetWeight) * _config.PortfolioValueUsd;

					borrowCostDollars = BorrowCostCalculator.CalculateBorrowCostDollars(
						shortNotional,
						annualBorrowBps,
						_config.HoldingPeriodDays);

					// FIX: Express borrowCostBps relative to trade notional for consistency
					borrowCostBps = BorrowCostCalculator.ConvertToBps(
						borrowCostDollars,
						tradeNotional);

					// Warn on hard-to-borrow names
					if (annualBorrowBps > _config.HardToBorrowThresholdAnnualBps)
					{
						double proratedBps = annualBorrowBps * (_config.HoldingPeriodDays / 365.0);
						warnings.Add($"Hard-to-borrow: {annualBorrowBps:F0} bps annual " +
								   $"({proratedBps:F2} bps for {_config.HoldingPeriodDays:F0} day hold)");
					}
				}
			}

			// Total costs (all bps values now on same basis: trade notional)
			double totalImpactBps = tempImpactBps + permImpactBps;
			double totalCostBps = commissionBps + spreadCostBps + totalImpactBps + borrowCostBps;
			double totalCostDollars = commissionDollars + spreadCostDollars + impactDollars + borrowCostDollars;

			// High impact warning
			if (totalImpactBps > 50.0)
			{
				warnings.Add($"High impact trade: {totalImpactBps:F1} bps");
			}

			return new PositionCostResult
			{
				Ticker = position.Ticker,
				WeightChange = weightChange,
				TradeNotionalValue = tradeNotional,
				SharesTraded = shares,
				ParticipationRate = participationRate,
				EstimatedExecutionDays = executionDays,
				IsShort = position.IsShort,

				CommissionBps = commissionBps,
				SpreadCostBps = spreadCostBps,
				TemporaryImpactBps = tempImpactBps,
				PermanentImpactBps = permImpactBps,
				TotalImpactBps = totalImpactBps,
				BorrowCostBps = borrowCostBps,
				BorrowCostAnnualBps = annualBorrowBps,
				TotalCostBps = totalCostBps,

				CommissionDollars = commissionDollars,
				SpreadCostDollars = spreadCostDollars,
				ImpactDollars = impactDollars,
				BorrowCostDollars = borrowCostDollars,
				TotalCostDollars = totalCostDollars,

				Warnings = warnings.ToArray()
			};
		}
	}

	// ============================================================================
	// VALIDATION UTILITIES
	// ============================================================================

	/// <summary>
	/// Validation utilities for cost calculations
	/// </summary>
	public static class ExecutionCostValidator
	{
		/// <summary>
		/// Validate that costs are reasonable for given rebalancing frequency
		/// </summary>
		public static ValidationResult ValidateAnnualCosts(
			double annualCostsBps,
			int rebalancesPerYear)
		{
			var issues = new List<string>();

			// Expected cost ranges by rebalancing frequency
			var (minExpected, maxExpected) = rebalancesPerYear switch
			{
				<= 4 => (20.0, 80.0),      // Quarterly: 20-80 bps
				<= 12 => (40.0, 120.0),    // Monthly: 40-120 bps
				<= 26 => (60.0, 180.0),    // Bi-weekly: 60-180 bps
				<= 52 => (80.0, 250.0),    // Weekly: 80-250 bps
				_ => (100.0, 400.0)        // Daily: 100-400 bps
			};

			if (annualCostsBps < minExpected)
			{
				issues.Add($"Costs unusually low: {annualCostsBps:F1} bps " +
						  $"(expected {minExpected:F0}-{maxExpected:F0} bps)");
			}
			else if (annualCostsBps > maxExpected)
			{
				issues.Add($"Costs unusually high: {annualCostsBps:F1} bps " +
						  $"(expected {minExpected:F0}-{maxExpected:F0} bps)");
			}

			return new ValidationResult
			{
				IsValid = issues.Count == 0,
				Issues = issues.ToArray()
			};
		}

		/// <summary>
		/// Validate that alphas remain stable over time (no compounding)
		/// </summary>
		public static ValidationResult ValidateAlphaStability(
			Dictionary<string, double> initialAlphas,
			Dictionary<string, double> currentAlphas,
			int periodsSinceStart)
		{
			var issues = new List<string>();

			foreach (var ticker in initialAlphas.Keys)
			{
				if (!currentAlphas.ContainsKey(ticker))
					continue;

				double initial = initialAlphas[ticker];
				double current = currentAlphas[ticker];
				double change = Math.Abs(current - initial);

				// Alphas should be identical (within floating point tolerance)
				if (change > 0.0001)
				{
					issues.Add($"{ticker}: Alpha changed from {initial:P2} to {current:P2} " +
							  $"after {periodsSinceStart} periods (should be constant)");
				}
			}

			return new ValidationResult
			{
				IsValid = issues.Count == 0,
				Issues = issues.ToArray()
			};
		}

		/// <summary>
		/// Validate per-position cost components
		/// </summary>
		public static ValidationResult ValidatePositionCosts(PositionCostResult result)
		{
			var issues = new List<string>();

			// Commission should be < 5 bps typically (can be higher for tiny trades due to min)
			if (result.CommissionBps > 5.0 && result.TradeNotionalValue > 10000)
			{
				issues.Add($"{result.Ticker}: Commission {result.CommissionBps:F2} bps " +
						  "is unusually high");
			}

			// Spread should be < 10 bps for liquid stocks
			if (result.SpreadCostBps > 10.0)
			{
				issues.Add($"{result.Ticker}: Spread cost {result.SpreadCostBps:F2} bps " +
						  "is high (illiquid security?)");
			}

			// Impact should be < 100 bps for most trades
			if (result.TotalImpactBps > 100.0)
			{
				issues.Add($"{result.Ticker}: Impact {result.TotalImpactBps:F2} bps " +
						  "is very high");
			}

			// Check annual borrow rate for hard-to-borrow (not the per-trade bps)
			if (result.BorrowCostAnnualBps > 100.0)
			{
				issues.Add($"{result.Ticker}: Borrow rate {result.BorrowCostAnnualBps:F0} bps annual " +
						  "indicates hard-to-borrow");
			}

			// Total cost should be < 150 bps for typical trades
			if (result.TotalCostBps > 150.0)
			{
				issues.Add($"{result.Ticker}: Total cost {result.TotalCostBps:F2} bps " +
						  "is excessive");
			}

			return new ValidationResult
			{
				IsValid = issues.Count == 0,
				Issues = issues.ToArray()
			};
		}
	}

	/// <summary>
	/// Validation result
	/// </summary>
	public sealed class ValidationResult
	{
		public bool IsValid { get; init; }
		public string[] Issues { get; init; } = Array.Empty<string>();

		public override string ToString()
		{
			if (IsValid)
				return "✓ Validation passed";

			return "✗ Validation failed:\n  " + string.Join("\n  ", Issues);
		}
	}
}