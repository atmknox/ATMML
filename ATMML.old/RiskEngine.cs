using MathNet.Numerics.Distributions;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.Statistics;
using System;
using System.Collections.Generic;
using System.Linq;
using Complex = System.Numerics.Complex;

namespace ATMML
{
	using DoubleMatrix = MathNet.Numerics.LinearAlgebra.Matrix<double>;
	using DoubleVector = MathNet.Numerics.LinearAlgebra.Vector<double>;

	public static class Extensions
	{
		public static double AbsoluteSum(this DoubleVector v)
		{
			return v.Select(Math.Abs).Sum();
		}
	}

	public class RiskEngine
	{
		public List<string> Assets { get; }
		public double MaxVaRThreshold;
		public double CorrelationPenaltyWeight = 0.1;
		public int MonteCarloPaths = 10000;

		private Dictionary<string, double[]> Returns;
		private Dictionary<string, double[]> ATR;
		private Dictionary<string, IdeaCalculator.BarData> BarData;
		private Dictionary<string, string> SectorMap = new Dictionary<string, string>();

		private DoubleMatrix CovMatrix;
		private DoubleMatrix CorrMatrix;

		public RiskEngine(Dictionary<string, IdeaCalculator.BarData> assets, double maxVaR)
		{
			Assets = assets.Keys.ToList();
			MaxVaRThreshold = maxVaR;
			BarData = assets;
			SectorMap = new Dictionary<string, string>();
			LoadReturnData(assets);
			ComputeATR();
		}

		private void LoadReturnData(Dictionary<string, IdeaCalculator.BarData> bars)
		{
			Returns = new Dictionary<string, double[]>();
			foreach (var kvp in bars)
			{
				var asset = kvp.Key;
				var barData = kvp.Value;
				var priceSeries = barData.Close;
				if (priceSeries == null || priceSeries.Count < 2) continue;
				double[] returns = new double[priceSeries.Count - 1];
				for (int i = 1; i < priceSeries.Count; i++)
				{
					double prev = priceSeries[i - 1];
					double curr = priceSeries[i];
					returns[i - 1] = (!double.IsNaN(prev) && !double.IsNaN(curr) && prev != 0) ? (curr - prev) / prev : 0;
				}
				Returns[asset] = returns;
			}
		}

		private void ComputeATR(int atm = 14, double smoothing = 0.85)
		{
			ATR = new Dictionary<string, double[]>();
			foreach (var asset in Assets)
			{
				var bars = BarData[asset];
				var prices = Enumerable.Range(0, bars.Close.Count).Select(i => (bars.High[i], bars.Low[i], bars.Close[i])).ToArray();
				double[] dailyTR = ComputeTrueRange(prices);
				ATR[asset] = SmoothATR(dailyTR, atm, smoothing);
			}
		}

		private double[] ComputeTrueRange((double High, double Low, double Close)[] series)
		{
			double[] tr = new double[series.Length];
			for (int i = 1; i < series.Length; i++)
			{
				tr[i] = Math.Max(series[i].High - series[i].Low, Math.Max(Math.Abs(series[i].High - series[i - 1].Close), Math.Abs(series[i].Low - series[i - 1].Close)));
			}
			return tr;
		}

		private double[] SmoothATR(double[] tr, int period, double alpha)
		{
			double[] atr = new double[tr.Length];
			atr[period - 1] = tr.Take(period).Average();
			for (int i = period; i < tr.Length; i++)
				atr[i] = alpha * atr[i - 1] + (1 - alpha) * tr[i];
			return atr;
		}

		private void BuildCovAndCorrMatrices(int index)
		{
			var returnColumns = Assets.Select(asset => Returns[asset].Take(index).Where(x => !double.IsNaN(x)).ToList()).ToList();
			var retMat = Matrix<double>.Build.DenseOfColumns(returnColumns);
			CovMatrix = Matrix<double>.Build.Dense(retMat.ColumnCount, retMat.ColumnCount);
			CorrMatrix = Matrix<double>.Build.Dense(retMat.ColumnCount, retMat.ColumnCount);
			for (int i = 0; i < retMat.ColumnCount; i++)
			{
				for (int j = 0; j < retMat.ColumnCount; j++)
				{
					CovMatrix[i, j] = Statistics.Covariance(retMat.Column(i), retMat.Column(j));
					CorrMatrix[i, j] = Correlation.Pearson(retMat.Column(i).ToArray(), retMat.Column(j).ToArray());
					if (double.IsNaN(CorrMatrix[i, j])) CorrMatrix[i, j] = 0;
				}
			}
		}

		public double CovarianceVaR(DoubleVector weights, double notional, double confidence)
		{
			double portSigma = Math.Sqrt(weights * CovMatrix * weights);
			double z = Normal.InvCDF(0, 1, 1 - confidence);
			return z * portSigma * notional;
		}

		public (Dictionary<string, double> Sizes, double PortfolioExposure, double PortfolioVolatility, double Leverage) Allocate(int index, int openTradeCount, decimal totalCapital)
		{
			BuildCovAndCorrMatrices(index);
			var sizes = new Dictionary<string, double>();
			int n = Assets.Count;

			double riskBudgetPerPosition = 0.02;
			double maxSectorPct = 0.30;
			double atrMultiple = 1.0;
			var sectorAllocations = new Dictionary<string, double>();
			var positionValues = new double[n];

			var weights = DoubleVector.Build.Dense(n, 1.0 / n);
			double portVaR = CovarianceVaR(weights, (double)totalCapital, 0.95);
			if (portVaR > MaxVaRThreshold)
			{
				return (Assets.ToDictionary(a => a, a => 0.0), 0, 0, 0);
			}

			var dollarAllocSum = 0.0;
			for (int i = 0; i < n; i++)
			{
				string asset = Assets[i];
				var priceSeries = BarData[asset].Close;
				if (index >= priceSeries.Count)
				{
					sizes[asset] = 0;
					continue;
				}

				double price = priceSeries[index];
				double atr = ATR[asset][index];
				if (price <= 0 || atr <= 0 || double.IsNaN(price) || double.IsNaN(atr))
				{
					sizes[asset] = 0;
					continue;
				}

				double corrSum = CorrMatrix.Row(i).Select(x => double.IsNaN(x) ? 0 : Math.Abs(x)).Sum();
				double riskPerShare = atrMultiple * atr * price * (1 + CorrelationPenaltyWeight * corrSum);
				if (riskPerShare <= 0 || double.IsNaN(riskPerShare))
				{
					sizes[asset] = 0;
					continue;
				}

				double dollarRiskBudget = (double)totalCapital * riskBudgetPerPosition;
				double shares = dollarRiskBudget / riskPerShare;
				double dollarAlloc = shares * price;
				dollarAllocSum += dollarAlloc;

				string sector = SectorMap.ContainsKey(asset) ? SectorMap[asset] : "Unknown";
				if (!sectorAllocations.ContainsKey(sector)) sectorAllocations[sector] = 0;
				if (sectorAllocations[sector] + dollarAlloc > maxSectorPct * (double)totalCapital)
				{
					sizes[asset] = 0;
					continue;
				}

				sectorAllocations[sector] += dollarAlloc;
				sizes[asset] = shares;
				positionValues[i] = dollarAlloc;
			}

			var totalInvestment = (double)totalCapital * Math.Max(riskBudgetPerPosition, Math.Min(1.0, openTradeCount * riskBudgetPerPosition));
			var investmentPerTrade = totalInvestment / Math.Max(1, openTradeCount);

			var scale = (n * investmentPerTrade) / dollarAllocSum;
			var adjustedSizes = new Dictionary<string, double>();
			sizes.ToList().ForEach(kvp => adjustedSizes[kvp.Key] = kvp.Value * scale);

			double portfolioExposure = positionValues.Sum();
			if (portfolioExposure <= 0) return (Assets.ToDictionary(a => a, a => 0.0), 0, 0, 0);
			DoubleVector finalWeights = DoubleVector.Build.Dense(positionValues.Select(v => v / portfolioExposure).ToArray());
			double portfolioVolatility = Math.Sqrt(finalWeights * CovMatrix * finalWeights);
			double leverage = portfolioExposure / (double)totalCapital;

			return (adjustedSizes, portfolioExposure, portfolioVolatility, leverage);
		}
	}
}
