using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using BetaNeutralRiskEngine;

namespace ATMML
{
	public class AlphaWeightingDiagnostic
	{
		// ============================================================
		// Data structure to hold position records
		// ============================================================
		public class PositionRecord
		{
			public DateTime Date { get; set; }
			public string Ticker { get; set; }
			public int Direction { get; set; }  // 1 = Long, -1 = Short
			public double SignalScore { get; set; }
			public double Weight { get; set; }  // Fraction of portfolio
			public double DollarSize { get; set; }
		}

		// Storage for logged records
		private List<PositionRecord> _log = new List<PositionRecord>();

		// ============================================================
		// Call this to log each position after optimization
		// ============================================================
		public void LogPosition(DateTime date, string ticker, BookSide bookSide, double signalScore, double weight, double dollarSize)
		{
			_log.Add(new PositionRecord
			{
				Date = date,
				Ticker = ticker,
				Direction = bookSide == BookSide.Long ? 1 : -1,
				SignalScore = signalScore,
				Weight = weight,
				DollarSize = dollarSize
			});
		}

		// Overload for direct direction value
		public void LogPosition(DateTime date, string ticker, int direction, double signalScore, double weight, double dollarSize)
		{
			_log.Add(new PositionRecord
			{
				Date = date,
				Ticker = ticker,
				Direction = direction,
				SignalScore = signalScore,
				Weight = weight,
				DollarSize = dollarSize
			});
		}

		// ============================================================
		// Call this after the main loop to output analysis
		// ============================================================
		public void OutputAnalysis(bool alphaWeightingEnabled)
		{
			Debug.WriteLine("");
			Debug.WriteLine("============================================================");
			Debug.WriteLine("[DIAGNOSTIC] ALPHAWEIGHTING SIGNAL SCORE ANALYSIS");
			Debug.WriteLine("============================================================");

			if (_log.Count == 0)
			{
				Debug.WriteLine("[AlphaWeighting] No data logged - is useBetaNeutralRiskEngine enabled?");
				Debug.WriteLine("============================================================");
				return;
			}

			Debug.WriteLine($"[AlphaWeighting] Enabled: {alphaWeightingEnabled}");
			Debug.WriteLine($"[AlphaWeighting] Total position records: {_log.Count}");

			OutputScoreDistribution();
			OutputScoreWeightCorrelation(alphaWeightingEnabled);
			OutputSampleDateComparison();
			OutputSummary(alphaWeightingEnabled);

			Debug.WriteLine("============================================================");
		}

		// ============================================================
		// Score Distribution Analysis
		// ============================================================
		private void OutputScoreDistribution()
		{
			var allScores = _log
				.Select(r => r.SignalScore)
				.Where(s => !double.IsNaN(s))
				.ToList();

			if (allScores.Count == 0)
			{
				Debug.WriteLine("");
				Debug.WriteLine("[AlphaWeighting] ⚠ WARNING: No valid SignalScore values found!");
				return;
			}

			Debug.WriteLine("");
			Debug.WriteLine("[AlphaWeighting] SIGNAL SCORE DISTRIBUTION:");
			Debug.WriteLine($"  Min Score:    {allScores.Min():F4}");
			Debug.WriteLine($"  Max Score:    {allScores.Max():F4}");
			Debug.WriteLine($"  Avg Score:    {allScores.Average():F4}");
			Debug.WriteLine($"  Median Score: {allScores.OrderBy(s => s).ElementAt(allScores.Count / 2):F4}");

			// Check for zero/constant scores
			var uniqueScores = allScores.Distinct().Count();
			if (uniqueScores < 5)
			{
				Debug.WriteLine($"  ⚠ WARNING: Only {uniqueScores} unique score values - scores may not be varying!");
			}
			else
			{
				Debug.WriteLine($"  Unique values: {uniqueScores}");
			}

			// Histogram
			Debug.WriteLine("");
			Debug.WriteLine("[AlphaWeighting] SCORE HISTOGRAM:");
			var min = allScores.Min();
			var max = allScores.Max();
			var range = max - min;
			if (range > 0)
			{
				var buckets = new int[10];
				foreach (var score in allScores)
				{
					var bucket = Math.Min(9, (int)((score - min) / range * 10));
					buckets[bucket]++;
				}
				for (int i = 0; i < 10; i++)
				{
					var bucketMin = min + (range * i / 10);
					var bucketMax = min + (range * (i + 1) / 10);
					var bar = new string('█', Math.Min(50, buckets[i] * 50 / allScores.Count));
					Debug.WriteLine($"  [{bucketMin,6:F2} - {bucketMax,6:F2}]: {bar} ({buckets[i]})");
				}
			}
		}

		// ============================================================
		// Score-Weight Correlation Analysis
		// ============================================================
		private void OutputScoreWeightCorrelation(bool alphaWeightingEnabled)
		{
			var validRecords = _log
				.Where(r => !double.IsNaN(r.SignalScore) && r.Weight > 0)
				.ToList();

			if (validRecords.Count < 10)
			{
				Debug.WriteLine("");
				Debug.WriteLine("[AlphaWeighting] Insufficient data for correlation analysis");
				return;
			}

			var longs = validRecords.Where(r => r.Direction > 0).ToList();
			var shorts = validRecords.Where(r => r.Direction < 0).ToList();

			Debug.WriteLine("");
			Debug.WriteLine("[AlphaWeighting] SCORE-WEIGHT CORRELATION:");

			// Long positions correlation
			if (longs.Count > 5)
			{
				var longCorr = CalculateCorrelation(
					longs.Select(r => r.SignalScore).ToList(),
					longs.Select(r => r.Weight).ToList()
				);
				Debug.WriteLine($"  Long positions:  r = {longCorr:F3} ({longs.Count} records)");

				if (alphaWeightingEnabled)
				{
					if (longCorr > 0.3)
						Debug.WriteLine($"    ✓ Positive correlation - higher scores get larger weights");
					else if (longCorr > 0)
						Debug.WriteLine($"    ⚠ Weak positive correlation - AlphaWeighting effect is minimal");
					else
						Debug.WriteLine($"    ✗ No positive correlation - AlphaWeighting may not be working");
				}
			}

			// Short positions correlation
			if (shorts.Count > 5)
			{
				var shortCorr = CalculateCorrelation(
					shorts.Select(r => r.SignalScore).ToList(),
					shorts.Select(r => r.Weight).ToList()
				);
				Debug.WriteLine($"  Short positions: r = {shortCorr:F3} ({shorts.Count} records)");

				if (alphaWeightingEnabled)
				{
					if (shortCorr > 0.3)
						Debug.WriteLine($"    ✓ Positive correlation - higher scores get larger weights");
					else if (shortCorr > 0)
						Debug.WriteLine($"    ⚠ Weak positive correlation - AlphaWeighting effect is minimal");
					else
						Debug.WriteLine($"    ✗ No positive correlation - AlphaWeighting may not be working");
				}
			}
		}

		// ============================================================
		// Sample Date Comparison
		// ============================================================
		private void OutputSampleDateComparison()
		{
			Debug.WriteLine("");
			Debug.WriteLine("[AlphaWeighting] SAMPLE DATE COMPARISON:");

			var sampleDate = _log.Select(r => r.Date).Distinct().Skip(10).FirstOrDefault();
			if (sampleDate == default)
			{
				sampleDate = _log.Select(r => r.Date).FirstOrDefault();
			}

			if (sampleDate == default)
			{
				Debug.WriteLine("  No sample date available");
				return;
			}

			var sampleRecords = _log.Where(r => r.Date == sampleDate).ToList();
			var sampleLongs = sampleRecords.Where(r => r.Direction > 0).OrderByDescending(r => r.SignalScore).ToList();
			var sampleShorts = sampleRecords.Where(r => r.Direction < 0).OrderByDescending(r => r.SignalScore).ToList();

			Debug.WriteLine($"  Date: {sampleDate:yyyy-MM-dd}");
			Debug.WriteLine($"  Positions: {sampleLongs.Count} longs, {sampleShorts.Count} shorts");
			Debug.WriteLine("");

			if (sampleLongs.Count > 0)
			{
				Debug.WriteLine("  TOP 5 LONG POSITIONS (by SignalScore):");
				Debug.WriteLine("  Ticker              | Score  | Weight | Dollar Size");
				Debug.WriteLine("  --------------------|--------|--------|------------");
				foreach (var rec in sampleLongs.Take(5))
				{
					Debug.WriteLine($"  {rec.Ticker,-20}| {rec.SignalScore,6:F2} | {rec.Weight,6:P2} | ${rec.DollarSize,10:N0}");
				}

				if (sampleLongs.Count > 5)
				{
					Debug.WriteLine("");
					Debug.WriteLine("  BOTTOM 5 LONG POSITIONS (by SignalScore):");
					Debug.WriteLine("  Ticker              | Score  | Weight | Dollar Size");
					Debug.WriteLine("  --------------------|--------|--------|------------");
					foreach (var rec in sampleLongs.TakeLast(5).Reverse())
					{
						Debug.WriteLine($"  {rec.Ticker,-20}| {rec.SignalScore,6:F2} | {rec.Weight,6:P2} | ${rec.DollarSize,10:N0}");
					}
				}
			}

			Debug.WriteLine("");

			if (sampleShorts.Count > 0)
			{
				Debug.WriteLine("  TOP 5 SHORT POSITIONS (by SignalScore):");
				Debug.WriteLine("  Ticker              | Score  | Weight | Dollar Size");
				Debug.WriteLine("  --------------------|--------|--------|------------");
				foreach (var rec in sampleShorts.Take(5))
				{
					Debug.WriteLine($"  {rec.Ticker,-20}| {rec.SignalScore,6:F2} | {rec.Weight,6:P2} | ${rec.DollarSize,10:N0}");
				}

				if (sampleShorts.Count > 5)
				{
					Debug.WriteLine("");
					Debug.WriteLine("  BOTTOM 5 SHORT POSITIONS (by SignalScore):");
					Debug.WriteLine("  Ticker              | Score  | Weight | Dollar Size");
					Debug.WriteLine("  --------------------|--------|--------|------------");
					foreach (var rec in sampleShorts.TakeLast(5).Reverse())
					{
						Debug.WriteLine($"  {rec.Ticker,-20}| {rec.SignalScore,6:F2} | {rec.Weight,6:P2} | ${rec.DollarSize,10:N0}");
					}
				}
			}

			// Check if weights are equal (indicating AlphaWeighting not working)
			var uniqueWeights = sampleRecords.Select(r => Math.Round(r.Weight, 6)).Distinct().Count();
			if (uniqueWeights < 3)
			{
				Debug.WriteLine("");
				Debug.WriteLine($"  ⚠ WARNING: Only {uniqueWeights} unique weight values on this date!");
				Debug.WriteLine($"     This suggests AlphaWeighting may not be affecting position sizes.");
			}
		}

		// ============================================================
		// Summary
		// ============================================================
		private void OutputSummary(bool alphaWeightingEnabled)
		{
			Debug.WriteLine("");
			Debug.WriteLine("[AlphaWeighting] SUMMARY:");

			if (!alphaWeightingEnabled)
			{
				Debug.WriteLine("  AlphaWeighting is DISABLED - all positions use equal weighting (subject to constraints)");
				return;
			}

			var allScores = _log.Select(r => r.SignalScore).Where(s => !double.IsNaN(s)).ToList();
			if (allScores.Count == 0)
			{
				Debug.WriteLine("  ⚠ No valid SignalScore data available");
				return;
			}

			var avgScore = allScores.Average();
			var scoreVariance = allScores.Select(s => (s - avgScore) * (s - avgScore)).Average();

			if (scoreVariance < 0.01)
			{
				Debug.WriteLine("  ⚠ SignalScore has very low variance - consider improving score differentiation");
			}
			else
			{
				Debug.WriteLine($"  SignalScore variance: {scoreVariance:F4} (appears reasonable for alpha weighting)");
			}

			// Check weight variance
			var allWeights = _log.Select(r => r.Weight).Where(w => w > 0).ToList();
			if (allWeights.Count > 0)
			{
				var avgWeight = allWeights.Average();
				var weightVariance = allWeights.Select(w => (w - avgWeight) * (w - avgWeight)).Average();

				if (weightVariance < 0.0001)
				{
					Debug.WriteLine("  ⚠ Position weights have very low variance - AlphaWeighting may not be having effect");
				}
				else
				{
					Debug.WriteLine($"  Weight variance: {weightVariance:F6} (weights are varying)");
				}
			}
		}

		// ============================================================
		// Helper: Pearson Correlation
		// ============================================================
		private static double CalculateCorrelation(List<double> x, List<double> y)
		{
			if (x.Count != y.Count || x.Count < 2)
				return 0;

			var n = x.Count;
			var avgX = x.Average();
			var avgY = y.Average();

			var covariance = x.Zip(y, (xi, yi) => (xi - avgX) * (yi - avgY)).Sum();
			var varX = x.Sum(xi => (xi - avgX) * (xi - avgX));
			var varY = y.Sum(yi => (yi - avgY) * (yi - avgY));

			if (varX <= 0 || varY <= 0)
				return 0;

			return covariance / Math.Sqrt(varX * varY);
		}

		// ============================================================
		// Clear log (if reusing instance)
		// ============================================================
		public void Clear()
		{
			_log.Clear();
		}

		// ============================================================
		// Get logged records (for external analysis)
		// ============================================================
		public IReadOnlyList<PositionRecord> GetRecords()
		{
			return _log.AsReadOnly();
		}
	}
}


// ============================================================
// HOW TO USE IN IdeaCalculator.createTrades()
// ============================================================
//
// STEP 1: Declare instance BEFORE the main loop
// ---------------------------------------------
// Find: var prvDate = startDate;
// Add AFTER:
//
//     var alphaWeightingDiag = new AlphaWeightingDiagnostic();
//
//
// STEP 2: Log positions inside the BetaNeutralRiskEngine result processing
// ------------------------------------------------------------------------
// Find the result.DollarWeights.ForEach block and add logging inside:
//
//     result.DollarWeights.ForEach(p =>
//     {
//         var riskKey = p.Key;
//         var f = riskKey.Split('\t');
//         var groupId = (f.Length >= 2) ? f[0] : "";
//         var ticker = (f.Length >= 2) ? f[1] : f[0];
//         var fractionSize = p.Value;
//         var stock = stocks.FirstOrDefault(s => s.Ticker == ticker && s.GroupId == groupId);
//
//         if (!double.IsNaN(fractionSize))
//         {
//             if (stock != null && stock.Price > 0)
//             {
//                 var dollarSize = Math.Abs(fractionSize) * cfg.PortfolioValue;
//                 var shareCount = dollarSize / stock.Price;
//
//                 riskSizes[riskKey] = shareCount;
//
//                 // ... existing code for longGross, shortGross, portfolioBeta ...
//
//                 // ADD THIS LINE:
//                 alphaWeightingDiag.LogPosition(date, ticker, stock.RequiredBook, stock.SignalScore, Math.Abs(fractionSize), dollarSize);
//             }
//         }
//     });
//
//
// STEP 3: Output analysis AFTER the main loop
// -------------------------------------------
// Find: string path3 = @"portfolios\trades\" + _model.Name;
// Add BEFORE:
//
//     var alphaWeightingEnabled = _model.getConstraint("AlphaWeighting") != null 
//                                 && _model.getConstraint("AlphaWeighting").Enable;
//     alphaWeightingDiag.OutputAnalysis(alphaWeightingEnabled);
//
// ============================================================