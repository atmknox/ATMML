using System;
using System.Collections.Generic;
using System.Linq;

namespace ATMML
{
	// -----------------------------------------------------------
	// Stock: minimal security descriptor used for exposure math
	// -----------------------------------------------------------
	public class Stock
	{
		public string Ticker { get; set; } = "";
		public string Sector { get; set; } = "";
		public double Price { get; set; }             // last price
		public double Shares { get; set; }            // share count (positive for both long/short)
		public bool IsLong { get; set; }              // true = long, false = short
		public double MarketCorrelation { get; set; } // correlation to SPY (0..1), optional
		public double Mu { get; internal set; }
	}

	// -----------------------------------------------------------
	// HedgeInput: signal snapshot for a bar
	// -----------------------------------------------------------
	public class HedgeInput
	{
		public int Trend { get; set; }               // 1 = Bullish, -1 = Bearish
		public int LongOscDirection { get; set; }    // 1 = Up, -1 = Down
		public int ShortOscDirection { get; set; }   // 1 = Up, -1 = Down
		public bool IsLongPosition { get; set; }     // semantic flag (kept for compatibility)
		public double MarketPrice { get; set; }      // SPY price
		public double ATR { get; set; }              // Average True Range (absolute $)
		public double AvgCorrelation { get; set; }   // weighted/avg corr to SPY (0..1)
		public double TargetPrice { get; set; }      // optional
	}

	// -----------------------------------------------------------
	// HedgeResult: final sizing for the bar
	// -----------------------------------------------------------
	public class HedgeResult
	{
		public int HedgeDirection { get; set; }        // +1 = Buy SPY, -1 = Short SPY, 0 = none
		public string Interpretation { get; set; } = "Neutral";
		public double HedgeRatio { get; set; }         // fraction of NET imbalance to hedge (0..0.25)
		public double AtrFactor { get; set; }          // ATR / MarketPrice (clamped)
		public double CorrelationFactor { get; set; }  // clamped (0..1)
		public double HedgeNotional { get; set; }      // $ notional = HedgeRatio * |Net Imbalance|
		public int SpyShares { get; set; }             // signed SPY shares
	}

	// -----------------------------------------------------------
	// HedgeEngine: computes hedge as % of NET imbalance (not gross!)
	// -----------------------------------------------------------
	public class HedgeEngine
	{
		// kept for compatibility with your app’s constructors; not used in sizing
		private readonly double _portfolioValue;
		private readonly double _spyPrice;
		private readonly double _contractSize; // for compatibility; not used with SPY shares

		public bool Debug { get; set; } = false;

		// Preferred constructor if you don’t need portfolio notionals
		public HedgeEngine(double spyPrice)
		{
			_spyPrice = spyPrice;
			_portfolioValue = 0.0;
			_contractSize = 1.0;
		}

		// Compatibility: (portfolioValue, spyPrice)
		public HedgeEngine(double portfolioValue, double spyPrice)
		{
			_portfolioValue = portfolioValue;
			_spyPrice = spyPrice;
			_contractSize = 1.0;
		}

		// ✅ Compatibility: (portfolioValue, spyPrice, contractSize)
		public HedgeEngine(double portfolioValue, double spyPrice, double contractSize)
		{
			_portfolioValue = portfolioValue;
			_spyPrice = spyPrice;
			_contractSize = contractSize;
		}

		// -------------------------------------------------------
		// CalculateInput: lightweight helper if your caller expects it
		//  - Replace placeholders with your real signal calc as needed.
		// -------------------------------------------------------
		public HedgeInput CalculateInput(
			object barCache,
			List<Stock> stocks,
			Dictionary<string, object>? referenceData,
			string marketTicker,
			string interval,
			DateTime date)
		{
			// Use current SPY price passed in constructor.
			double marketPrice = Math.Max(1.0, _spyPrice);

			// ATR fallback (1% of price) if you don’t inject real ATR.
			double atr = Math.Max(0.01 * marketPrice, 0.50);

			// Average correlation from stocks if present, else 0.7 default.
			double avgCorr = (stocks != null && stocks.Count > 0)
				? Clamp01(stocks.Average(s => s.MarketCorrelation))
				: 0.7;

			// Placeholder directions; replace with your oscillator directions.
			int longOscDir = 1;
			int shortOscDir = 1;

			// Placeholder overall trend.
			int trend = 1;

			return new HedgeInput
			{
				Trend = trend,
				LongOscDirection = longOscDir,
				ShortOscDirection = shortOscDir,
				IsLongPosition = true,
				MarketPrice = marketPrice,
				ATR = atr,
				AvgCorrelation = avgCorr,
				TargetPrice = marketPrice
			};
		}

		// ✅ Backward-compat alias (intentionally mis-spelled to match callers)
		public HedgeInput CalulateInput(
			object barCache,
			List<Stock> stocks,
			Dictionary<string, object> referenceData,
			string marketTicker,
			string interval,
			DateTime date)
		{
			return CalculateInput(barCache, stocks, referenceData, marketTicker, interval, date);
		}

		// -------------------------------------------------------
		// DetermineHedge: compute hedge as % of NET imbalance only
		//  - This function is where we ENFORCE netting longs and shorts.
		// -------------------------------------------------------
		public HedgeResult DetermineHedge(HedgeInput input, List<Stock> stocks)
		{
			var res = new HedgeResult
			{
				HedgeDirection = 0,
				Interpretation = "Neutral",
				AtrFactor = ClampUpper((input.MarketPrice > 0.0) ? (input.ATR / input.MarketPrice) : 0.0, 0.10), // <= 10%
				CorrelationFactor = Clamp01(input.AvgCorrelation)
			};

			// 1) Compute NET exposure (longs - shorts), always non-negative for sizing
			double grossLongs = stocks.Where(s => s.IsLong).Sum(s => s.Price * s.Shares);
			double grossShorts = stocks.Where(s => !s.IsLong).Sum(s => Math.Abs(s.Price * s.Shares));
			double netExposure = grossLongs - grossShorts;               // + = net long, - = net short
			double netAbs = Math.Abs(netExposure);                   // the ONLY base for sizing

			// Direction offsets the imbalance (never hedge the whole book)
			if (netExposure > 0) res.HedgeDirection = -1; // net long → short SPY
			else if (netExposure < 0) res.HedgeDirection = 1;  // net short → long SPY
			else res.HedgeDirection = 0;  // flat → no hedge

			// Early exit: if net is effectively zero, do nothing
			if (netAbs < 1e-6 || res.HedgeDirection == 0)
			{
				res.HedgeRatio = 0.0;
				res.HedgeNotional = 0.0;
				res.SpyShares = 0;

				if (Debug)
				{
					Console.WriteLine("==== Hedge Debug (Flat Net) ====");
					Console.WriteLine($"Gross Longs: {grossLongs:N0}, Gross Shorts: {grossShorts:N0}, Net: {netExposure:N0}");
					Console.WriteLine("Net exposure ~ 0 → no hedge.");
					Console.WriteLine("================================");
				}
				return res;
			}

			// 2) Rule matrix → baseRatio = % of NET imbalance (not of portfolio)
			double baseRatio = 0.0;
			if (input.Trend == 1) // Bullish
			{
				if (input.LongOscDirection == 1 && input.ShortOscDirection == 1)
				{
					res.Interpretation = "Strong Long";
					baseRatio = 0.05; // 5% of net imbalance
				}
				else if (input.LongOscDirection == 1 && input.ShortOscDirection == -1)
				{
					res.Interpretation = "Bullish w/ Retracement";
					baseRatio = 0.10;
				}
				else
				{
					res.Interpretation = "Weak / Vulnerable Long";
					baseRatio = 0.15;
				}
			}
			else if (input.Trend == -1) // Bearish
			{
				if (input.LongOscDirection == -1 && input.ShortOscDirection == -1)
				{
					res.Interpretation = "Strong Short";
					baseRatio = 0.05;
				}
				else if (input.LongOscDirection == -1 && input.ShortOscDirection == 1)
				{
					res.Interpretation = "Bearish w/ Retracement";
					baseRatio = 0.10;
				}
				else
				{
					res.Interpretation = "Weak / Vulnerable Short";
					baseRatio = 0.15;
				}
			}
			else
			{
				// Trend neutral / unknown: small stabilizer
				res.Interpretation = "Trend Neutral";
				baseRatio = 0.05;
			}

			// 3) Scale by ATR and correlation (both clamped safely)
			double scaled = baseRatio;
			scaled *= (1.0 + res.AtrFactor);     // modestly increase with volatility
			scaled *= res.CorrelationFactor;     // reduce if low correlation

			// Final clamp (safety): 0..25% of net imbalance
			res.HedgeRatio = ClampRange(scaled, 0.0, 0.25);

			// 4) Final hedge = % of NET imbalance (never portfolio, never gross)
			res.HedgeNotional = res.HedgeRatio * netAbs;

			// Absolute sanity: never allow hedge notional to exceed netAbs
			if (res.HedgeNotional > netAbs)
				res.HedgeNotional = netAbs;

			// 5) Convert $ notional to SPY shares (signed)
			double spyPriceSafe = Math.Max(1.0, _spyPrice);
			res.SpyShares = (int)Math.Round((res.HedgeNotional / spyPriceSafe) * res.HedgeDirection);

			// Debug trace
			if (Debug)
			{
				Console.WriteLine("==== Hedge Debug (NET-Based) ====");
				Console.WriteLine($"Gross Longs:  {grossLongs:N0}");
				Console.WriteLine($"Gross Shorts: {grossShorts:N0}");
				Console.WriteLine($"Net Exposure: {netExposure:N0} (Abs: {netAbs:N0})");
				Console.WriteLine($"Base Ratio:   {baseRatio:P2}");
				Console.WriteLine($"ATR Factor:   {res.AtrFactor:P2}  (ATR/Price, clamped)");
				Console.WriteLine($"Corr Factor:  {res.CorrelationFactor:P2}");
				Console.WriteLine($"Hedge Ratio:  {res.HedgeRatio:P2}  (of NET imbalance)");
				Console.WriteLine($"Hedge $:      {res.HedgeNotional:N0}  (<= NetAbs: {netAbs:N0})");
				Console.WriteLine($"SPY Price:    {spyPriceSafe:N2}");
				Console.WriteLine($"SPY Shares:   {res.SpyShares:N0}");
				Console.WriteLine($"Interpret:    {res.Interpretation}");
				Console.WriteLine("=================================");
			}
			return res;
		}

		// -------------------------------------------------------
		// Compatibility method expected by your app:
		//  - Returns signed SPY shares (not futures)
		// -------------------------------------------------------
		public int CalculateFuturesContracts(HedgeResult hedge)
		{
			// For SPY-based hedging, just return the computed share count.
			return hedge?.SpyShares ?? 0;
		}

		// ---------------- helpers ----------------
		private static double Clamp01(double x) => (x < 0.0) ? 0.0 : (x > 1.0 ? 1.0 : x);
		private static double ClampUpper(double x, double hi) => (x > hi) ? hi : x;
		private static double ClampRange(double x, double lo, double hi)
		{
			if (x < lo) return lo;
			if (x > hi) return hi;
			return x;
		}
	}
}
