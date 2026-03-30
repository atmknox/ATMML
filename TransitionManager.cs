// TransitionManager.cs
// Manages regime transitions, beta smoothing, position classification, and tolerance tracking

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace RiskEngineMNParity
{
	// Enums (PositionAction, TransitionSpeed) are defined in Enums.cs

	#region Position and Portfolio Classes

	/// <summary>
	/// Represents a current portfolio position with all attributes needed for transition analysis.
	/// </summary>
	public sealed class Position
	{
		public string Ticker { get; init; } = "";
		public BookSide Side { get; init; }
		public double Beta { get; init; }
		public double Volatility { get; init; }
		public double MarketValue { get; init; }
		public double Weight { get; set; }
		public double TargetWeight { get; set; }
		public double AlphaStrength { get; init; }
		public Stock Stock { get; init; } = null!;
		public string Sector { get; init; } = "";
		public string Industry { get; init; } = "";

		public string Key => $"{Side}_{Ticker}";

		public override string ToString()
		{
			return $"[{Side}] {Ticker,-8} β={Beta:F2} σ={Volatility:P1} α={AlphaStrength:F2} w={Weight:P2}";
		}
	}

	/// <summary>
	/// Represents current portfolio state for transition analysis.
	/// </summary>
	public sealed class Portfolio
	{
		public List<Position> Longs { get; init; } = new();
		public List<Position> Shorts { get; init; } = new();
		public double PortfolioValue { get; init; }

		public IEnumerable<Position> AllPositions => Longs.Concat(Shorts);

		public double LongGross => Longs.Sum(p => p.MarketValue);
		public double ShortGross => Shorts.Sum(p => Math.Abs(p.MarketValue));
		public double TotalGross => LongGross + ShortGross;
		public double NetExposure => LongGross - ShortGross;

		public double PortfolioBeta
		{
			get
			{
				double longBeta = Longs.Sum(p => p.MarketValue * p.Beta);
				double shortBeta = Shorts.Sum(p => Math.Abs(p.MarketValue) * p.Beta);
				return (longBeta - shortBeta) / PortfolioValue;
			}
		}
	}

	#endregion

	#region Transition Plan

	/// <summary>
	/// Plan for transitioning portfolio between regimes.
	/// </summary>
	public sealed class TransitionPlan
	{
		public MarketRegime FromRegime { get; init; }
		public MarketRegime ToRegime { get; init; }
		public TransitionSpeed Speed { get; set; }
		public double MaxSingleDayTurnover { get; init; }
		public double TotalTransitionBudget { get; init; }
		public int MaxRebalancesToComplete { get; init; }

		private readonly Dictionary<PositionAction, List<Position>> _positions = new()
		{
			[PositionAction.Keep] = new List<Position>(),
			[PositionAction.Reduce] = new List<Position>(),
			[PositionAction.Tolerate] = new List<Position>(),
			[PositionAction.Exit] = new List<Position>()
		};

		public void AddPosition(Position position, PositionAction action)
		{
			_positions[action].Add(position);
		}

		public IReadOnlyList<Position> GetPositions(PositionAction action) => _positions[action];

		public IEnumerable<Position> KeepPositions => _positions[PositionAction.Keep];
		public IEnumerable<Position> ReducePositions => _positions[PositionAction.Reduce];
		public IEnumerable<Position> ToleratePositions => _positions[PositionAction.Tolerate];
		public IEnumerable<Position> ExitPositions => _positions[PositionAction.Exit];

		public int KeepCount => _positions[PositionAction.Keep].Count;
		public int ReduceCount => _positions[PositionAction.Reduce].Count;
		public int TolerateCount => _positions[PositionAction.Tolerate].Count;
		public int ExitCount => _positions[PositionAction.Exit].Count;

		/// <summary>
		/// Upgrade all tolerated positions to exit (used when entering Stress).
		/// </summary>
		public void UpgradeTolerateToExit()
		{
			_positions[PositionAction.Exit].AddRange(_positions[PositionAction.Tolerate]);
			_positions[PositionAction.Tolerate].Clear();
		}

		public double TotalExitValue => _positions[PositionAction.Exit].Sum(p => Math.Abs(p.MarketValue));
		public double TotalKeepValue => _positions[PositionAction.Keep].Sum(p => Math.Abs(p.MarketValue));

		public void PrintSummary()
		{
			Console.WriteLine($"=== TRANSITION PLAN: {FromRegime} → {ToRegime} ===");
			Console.WriteLine($"Speed: {Speed}");
			Console.WriteLine($"Max single-day turnover: {MaxSingleDayTurnover:P0}");
			Console.WriteLine($"Total budget: {TotalTransitionBudget:P0}");
			Console.WriteLine($"Max rebalances: {MaxRebalancesToComplete}");
			Console.WriteLine();
			Console.WriteLine($"KEEP ({KeepCount}):");
			foreach (var p in KeepPositions) Console.WriteLine($"  {p}");
			Console.WriteLine($"REDUCE ({ReduceCount}):");
			foreach (var p in ReducePositions) Console.WriteLine($"  {p}");
			Console.WriteLine($"TOLERATE ({TolerateCount}):");
			foreach (var p in ToleratePositions) Console.WriteLine($"  {p}");
			Console.WriteLine($"EXIT ({ExitCount}):");
			foreach (var p in ExitPositions) Console.WriteLine($"  {p}");
		}
	}

	#endregion

	#region Tolerance Tracking

	/// <summary>
	/// Tracks state of a tolerated position.
	/// </summary>
	public sealed class ToleranceState
	{
		public string Ticker { get; init; } = "";
		public BookSide Side { get; init; }
		public DateTime ToleratedSince { get; init; }
		public double OriginalWeight { get; init; }
		public int RebalanceCount { get; set; }
		public int MaxGracePeriod { get; init; }

		/// <summary>
		/// Calculate decayed weight based on rebalance count.
		/// </summary>
		public double GetDecayedWeight()
		{
			double decayFactor = 1.0 - (RebalanceCount * 0.25);
			return OriginalWeight * Math.Max(decayFactor, 0.25);
		}

		public bool ShouldForceExit => RebalanceCount >= MaxGracePeriod;

		public override string ToString()
		{
			return $"[{Side}] {Ticker}: Rebal {RebalanceCount}/{MaxGracePeriod}, " +
				   $"Weight {OriginalWeight:P2} → {GetDecayedWeight():P2}";
		}
	}

	#endregion

	#region Position Transition Analyzer

	/// <summary>
	/// Analyzes positions and classifies them for regime transitions.
	/// </summary>
	public sealed class PositionTransitionAnalyzer
	{
		private readonly double _strongAlphaThreshold;

		public PositionTransitionAnalyzer(double strongAlphaThreshold = 0.7)
		{
			_strongAlphaThreshold = strongAlphaThreshold;
		}

		/// <summary>
		/// Classify a position based on new regime requirements.
		/// </summary>
		public PositionAction ClassifyPosition(
			Position position,
			RegimeSelectionConfig newSelectionConfig,
			Config newOptConfig)
		{
			bool fitsNewBetaFilter = newSelectionConfig.PassesBetaFilter(position.Stock);
			bool hasStrongAlpha = position.AlphaStrength >= _strongAlphaThreshold;
			bool isDirectionallyWrong = newSelectionConfig.IsDirectionallyWrong(
				position.Stock, newOptConfig.TargetPortfolioBeta);

			// Decision matrix
			if (fitsNewBetaFilter && hasStrongAlpha)
				return PositionAction.Keep;

			if (fitsNewBetaFilter && !hasStrongAlpha)
				return PositionAction.Reduce;

			if (!fitsNewBetaFilter && hasStrongAlpha && !isDirectionallyWrong)
				return PositionAction.Tolerate;

			return PositionAction.Exit;
		}

		/// <summary>
		/// Create a transition plan for all positions.
		/// </summary>
		public TransitionPlan CreatePlan(
			Portfolio portfolio,
			MarketRegime fromRegime,
			MarketRegime toRegime)
		{
			var newSelectionConfig = RegimeSelectionConfigFactory.Create(toRegime);
			var newOptConfig = RegimeConfigFactory.Create(toRegime);
			var (maxTurnover, totalBudget, maxRebalances) =
				RegimeConfigFactory.GetTurnoverConfig(fromRegime, toRegime);

			int distance = RegimeConfigFactory.GetRegimeDistance(fromRegime, toRegime);

			var plan = new TransitionPlan
			{
				FromRegime = fromRegime,
				ToRegime = toRegime,
				MaxSingleDayTurnover = maxTurnover,
				TotalTransitionBudget = totalBudget,
				MaxRebalancesToComplete = maxRebalances,
				Speed = DetermineSpeed(fromRegime, toRegime, distance)
			};

			foreach (var position in portfolio.AllPositions)
			{
				var action = ClassifyPosition(position, newSelectionConfig, newOptConfig);
				plan.AddPosition(position, action);
			}

			// No tolerance in Stress regime
			if (toRegime == MarketRegime.Stress)
			{
				plan.UpgradeTolerateToExit();
			}

			return plan;
		}

		private TransitionSpeed DetermineSpeed(MarketRegime from, MarketRegime to, int distance)
		{
			if (to == MarketRegime.Stress)
				return TransitionSpeed.Immediate;

			return distance switch
			{
				1 => TransitionSpeed.Gradual,
				2 => TransitionSpeed.Moderate,
				_ => TransitionSpeed.Urgent
			};
		}
	}

	#endregion

	#region Transition Manager

	/// <summary>
	/// Manages regime transitions with smoothing and tolerance tracking.
	/// </summary>
	public sealed class TransitionManager
	{
		private MarketRegime _currentRegime = MarketRegime.HighRiskOn;
		private double _currentTargetBeta = 0.15;
		private readonly Dictionary<string, ToleranceState> _toleratedPositions = new();
		private readonly PositionTransitionAnalyzer _analyzer;
		private readonly bool _verbose;

		public TransitionManager(double strongAlphaThreshold = 0.7, bool verbose = false)
		{
			_analyzer = new PositionTransitionAnalyzer(strongAlphaThreshold);
			_verbose = verbose;
		}

		public MarketRegime CurrentRegime => _currentRegime;
		public double CurrentTargetBeta => _currentTargetBeta;

		/// <summary>
		/// Initialize with a starting regime.
		/// </summary>
		public void Initialize(MarketRegime regime)
		{
			_currentRegime = regime;
			var config = RegimeConfigFactory.Create(regime);
			_currentTargetBeta = config.TargetPortfolioBeta;
			Log($"Initialized with regime {regime}, target beta {_currentTargetBeta:F2}");
		}

		/// <summary>
		/// Process a regime change and return the adjusted optimization configuration.
		/// </summary>
		public Config ProcessRegimeChange(MarketRegime newRegime, Portfolio currentPortfolio)
		{
			var baseConfig = RegimeConfigFactory.Create(newRegime);

			// Calculate smoothed beta target
			double smoothedBeta = CalculateSmoothedBeta(newRegime, baseConfig.TargetPortfolioBeta);

			// Update tolerance tracking
			UpdateToleranceTracking(newRegime);

			// Log transition
			int distance = RegimeConfigFactory.GetRegimeDistance(_currentRegime, newRegime);
			Log($"Regime transition: {_currentRegime} → {newRegime} (distance={distance})");
			Log($"Beta: {_currentTargetBeta:F2} → {smoothedBeta:F2} (raw target={baseConfig.TargetPortfolioBeta:F2})");

			_currentRegime = newRegime;
			_currentTargetBeta = smoothedBeta;

			// Return config with smoothed beta
			return new Config
			{
				PortfolioValue = baseConfig.PortfolioValue,
				Objective = baseConfig.Objective,
				GrossTargetPerSideFraction = baseConfig.GrossTargetPerSideFraction,
				MaxTotalGrossFraction = baseConfig.MaxTotalGrossFraction,
				MinTotalGrossFraction = baseConfig.MinTotalGrossFraction,
				MaxPerNameFraction = baseConfig.MaxPerNameFraction,
				MinPerNameFraction = baseConfig.MinPerNameFraction,
				MaxSectorFraction = baseConfig.MaxSectorFraction,
				MaxIndustryFraction = baseConfig.MaxIndustryFraction,
				MaxSubIndustryFraction = baseConfig.MaxSubIndustryFraction,
				EnforceMarketNeutral = baseConfig.EnforceMarketNeutral,
				MaxMarketImbalanceFraction = baseConfig.MaxMarketImbalanceFraction,
				EnforceBetaTarget = baseConfig.EnforceBetaTarget,
				TargetPortfolioBeta = smoothedBeta,  // Use smoothed
				BetaTolerance = baseConfig.BetaTolerance,
				EnforceVolatilityNeutral = baseConfig.EnforceVolatilityNeutral,
				MaxVolatilityImbalanceFraction = baseConfig.MaxVolatilityImbalanceFraction,
				UseRiskParity = baseConfig.UseRiskParity,
				RiskParityBlend = baseConfig.RiskParityBlend,
				MaxVolDispersionRatio = baseConfig.MaxVolDispersionRatio,
				Verbose = baseConfig.Verbose
			};
		}

		/// <summary>
		/// Create a transition plan for moving from current to new regime.
		/// </summary>
		public TransitionPlan CreateTransitionPlan(Portfolio currentPortfolio, MarketRegime newRegime)
		{
			var plan = _analyzer.CreatePlan(currentPortfolio, _currentRegime, newRegime);

			Log($"Transition plan created:");
			Log($"  Keep: {plan.KeepCount}, Reduce: {plan.ReduceCount}, " +
				$"Tolerate: {plan.TolerateCount}, Exit: {plan.ExitCount}");
			Log($"  Exit value: {plan.TotalExitValue:C0}");

			return plan;
		}

		/// <summary>
		/// Track a tolerated position.
		/// </summary>
		public void TrackToleratedPosition(Position position)
		{
			string key = position.Key;

			if (!_toleratedPositions.TryGetValue(key, out var state))
			{
				var selConfig = RegimeSelectionConfigFactory.Create(_currentRegime);
				state = new ToleranceState
				{
					Ticker = position.Ticker,
					Side = position.Side,
					ToleratedSince = DateTime.Now,
					OriginalWeight = position.Weight,
					MaxGracePeriod = selConfig.ToleranceGracePeriod,
					RebalanceCount = 0
				};
				_toleratedPositions[key] = state;
				Log($"New tolerated position: {key}");
			}

			state.RebalanceCount++;
			position.TargetWeight = state.GetDecayedWeight();

			Log($"Tolerance update: {state}");
		}

		/// <summary>
		/// Check if a tolerated position should be force-exited.
		/// </summary>
		public bool ShouldForceExit(string key)
		{
			if (!_toleratedPositions.TryGetValue(key, out var state))
				return false;

			return state.ShouldForceExit;
		}

		/// <summary>
		/// Remove a position from tolerance tracking.
		/// </summary>
		public void RemoveFromTolerance(string key)
		{
			if (_toleratedPositions.Remove(key))
			{
				Log($"Removed from tolerance: {key}");
			}
		}

		/// <summary>
		/// Get all currently tolerated positions.
		/// </summary>
		public IReadOnlyDictionary<string, ToleranceState> GetToleratedPositions()
		{
			return _toleratedPositions;
		}

		/// <summary>
		/// Clear all tolerance tracking (e.g., when entering Stress).
		/// </summary>
		public void ClearAllTolerances()
		{
			int count = _toleratedPositions.Count;
			_toleratedPositions.Clear();
			Log($"Cleared {count} tolerated positions");
		}

		/// <summary>
		/// Get stocks that need to be added to replace exits.
		/// </summary>
		public List<Stock> GetReplacementCandidates(
			TransitionPlan plan,
			IEnumerable<Stock> universe,
			RegimeSelectionConfig selectionConfig)
		{
			// Get tickers of positions being kept or tolerated
			var keepTickers = plan.KeepPositions.Select(p => p.Ticker).ToHashSet();
			var tolerateTickers = plan.ToleratePositions.Select(p => p.Ticker).ToHashSet();
			var reduceTickers = plan.ReducePositions.Select(p => p.Ticker).ToHashSet();
			var existingTickers = keepTickers.Union(tolerateTickers).Union(reduceTickers).ToHashSet();

			// Filter universe for new candidates that fit the new regime
			return universe
				.Where(s => !existingTickers.Contains(s.Ticker))
				.Where(s => selectionConfig.PassesBetaFilter(s))
				.OrderByDescending(s => s.AlphaScore)
				.ToList();
		}

		private double CalculateSmoothedBeta(MarketRegime newRegime, double rawTarget)
		{
			int distance = RegimeConfigFactory.GetRegimeDistance(_currentRegime, newRegime);

			// Exception: Move immediately INTO Stress
			if (newRegime == MarketRegime.Stress)
			{
				Log($"IMMEDIATE move to Stress: Beta {_currentTargetBeta:F2} → {rawTarget:F2}");
				return rawTarget;
			}

			double blendFactor;

			if (distance <= 1)
			{
				// Adjacent regime: 70% toward target
				blendFactor = 0.70;
			}
			else if (distance == 2)
			{
				// Skip one regime: 50% toward target
				blendFactor = 0.50;
			}
			else
			{
				// Coming OUT of stress: cautious 40% step
				blendFactor = 0.40;
			}

			return (1 - blendFactor) * _currentTargetBeta + blendFactor * rawTarget;
		}

		private void UpdateToleranceTracking(MarketRegime newRegime)
		{
			// Clear all tolerances when entering Stress
			if (newRegime == MarketRegime.Stress)
			{
				ClearAllTolerances();
			}
		}

		private void Log(string message)
		{
			if (_verbose)
			{
				Debug.WriteLine($"[TransitionManager] {message}");
				Console.WriteLine($"[TransitionManager] {message}");
			}
		}
	}

	#endregion
}