// Enums.cs
// Shared enumerations for the RiskEngineMNParity system

namespace RiskEngineMNParity
{
	/// <summary>
	/// Market regime classification based on risk conditions.
	/// </summary>
	public enum MarketRegime
	{
		None,

		/// <summary>Very bullish - target strong positive beta</summary>
		HighRiskOn,

		/// <summary>Bullish - target moderate positive beta</summary>
		RiskOn,

		/// <summary>Bearish - target moderate negative beta</summary>
		RiskOff,

		/// <summary>Very bearish / crisis - target strong negative beta</summary>
		Stress
	}

	/// <summary>
	/// Optimization objective function.
	/// </summary>
	public enum OptimizationObjective
	{
		/// <summary>Maximize weighted alpha exposure</summary>
		MaximizeAlpha,

		/// <summary>Equal weight within books (default)</summary>
		EqualWeight,

		/// <summary>Weight inversely to volatility</summary>
		RiskParity,

		/// <summary>Minimize portfolio volatility</summary>
		MinimizeVolatility
	}

	/// <summary>
	/// Action to take on a position during regime transition.
	/// </summary>
	public enum PositionAction
	{
		/// <summary>Fits new regime, alpha still valid - keep position</summary>
		Keep,

		/// <summary>Partially fits, scale down weight</summary>
		Reduce,

		/// <summary>Doesn't fit but alpha strong, hold temporarily with decay</summary>
		Tolerate,

		/// <summary>Must exit - wrong direction AND/OR weak alpha</summary>
		Exit
	}

	/// <summary>
	/// Transition speed based on regime distance.
	/// </summary>
	public enum TransitionSpeed
	{
		/// <summary>2-3 rebalances</summary>
		Gradual,

		/// <summary>1-2 rebalances</summary>
		Moderate,

		/// <summary>1 rebalance</summary>
		Urgent,

		/// <summary>Same day (into Stress)</summary>
		Immediate
	}
}