using System.Collections.Generic;
using System;
using System.Linq;

namespace ATMML
{
	public class RollingBetaCalculator
	{
		public static List<double> CalculateRollingBeta(List<double> stockPrices, List<double> benchmarkPrices, int windowSize)
		{
			if (stockPrices.Count != benchmarkPrices.Count)
				throw new ArgumentException("Price lists must be of equal length.");

			// Calculate daily returns
			List<double> stockReturns = CalculateReturns(stockPrices);  // get stock prices from BBG
			List<double> benchmarkReturns = CalculateReturns(benchmarkPrices); // get benchmark prices from BBG (Relative Index PR240 or Primary Benchmark FD048

			stockReturns.Insert(0, 0);
			benchmarkReturns.Insert(0, 0);

			List<double> rollingBetas = new List<double>();

			for (int i = 0; i < stockReturns.Count; i++)
			{
				var ws = Math.Min(i + 1, windowSize);
				var sk = Math.Max(0, i - windowSize);
				var stockWindow = stockReturns.Skip(sk).Take(ws).ToList();
				var benchmarkWindow = benchmarkReturns.Skip(sk).Take(ws).ToList();
				double beta = CalculateBeta(stockWindow, benchmarkWindow);
				rollingBetas.Add(beta);
			}

			return rollingBetas;
		}

		private static List<double> CalculateReturns(List<double> prices)
		{
			List<double> returns = new List<double>();
			for (int i = 1; i < prices.Count; i++)
			{
				returns.Add((prices[i] - prices[i - 1]) / prices[i - 1]);
			}
			return returns;
		}

		private static double CalculateBeta(List<double> stockReturns, List<double> benchmarkReturns)
		{
			double avgStock = stockReturns.Average();
			double avgBenchmark = benchmarkReturns.Average();

			double covariance = stockReturns.Zip(benchmarkReturns, (s, b) => (s - avgStock) * (b - avgBenchmark)).Sum();
			double variance = benchmarkReturns.Sum(b => Math.Pow(b - avgBenchmark, 2));

			return covariance / variance;
		}
	}
}