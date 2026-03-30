using OPTANO.Modeling.Optimization;
using OPTANO.Modeling.Optimization.Solver.Highs14x;
using MathNet.Numerics.LinearAlgebra;
using System.Diagnostics;
using MathNet.Numerics.Statistics;
using System.Collections.Generic;
using System.Linq;
using System;

namespace RiskEngine2
{
	public class Stock
	{
		public string Ticker { get; set; }
		public List<DateTime> Dates { get; set; } = new();
		public List<double> Prices { get; set; } = new();
		public string Sector { get; set; }
		public string Industry { get; set; }
		public string SubIndustry { get; set; }
		public double MarketCap { get; set; }
		public double AvgVolume { get; set; }
		public double Beta { get; set; }
		public double EarningsExposure { get; set; }
		public string Currency { get; set; }
		public bool IsEarningsPlay { get; set; }

		// calculated statistics
		public double Mu { get; set; }
		public double Sigma { get; set; }
		public bool IsLong { get; set; } = true;

		public void ComputeReturnsAndStats()
		{
			int T = Prices.Count;
			if (T < 2) throw new InvalidOperationException($"Insufficient data for {Ticker}");

			var returns = new double[T - 1];
			for (int t = 1; t < T; t++)
				returns[t - 1] = Math.Log(Prices[t] / Prices[t - 1]);

			Mu = returns.Average();
			Sigma = Statistics.StandardDeviation(returns);
		}
		public static Matrix<double> BuildCovarianceMatrix(List<Stock> stocks)
		{
			int N = stocks.Count;
			if (N == 0) throw new ArgumentException("No stocks provided");

			// Ensure consistent length:
			int length = stocks[0].Prices.Count;
			if (stocks.Any(s => s.Prices.Count != length))
				throw new ArgumentException("All stocks must have same number of price points");

			// Compute log return vectors per stock
			var returnVecs = stocks.Select(s =>
				Vector<double>.Build.Dense(s.Prices.Count - 1, i =>
					Math.Log(s.Prices[i + 1] / s.Prices[i]))
			).ToArray();

			// Initialize covariance matrix
			var covarianceMatrix = Matrix<double>.Build.Dense(N, N);

			// Compute covariance for each pair of stocks
			for (int i = 0; i < N; i++)
			{
				for (int j = i; j < N; j++)
				{
					var cov = Statistics.Covariance(returnVecs[i].ToArray(), returnVecs[j].ToArray());
					covarianceMatrix[i, j] = cov;
					covarianceMatrix[j, i] = cov; // Covariance matrix is symmetric
				}
			}

			return covarianceMatrix;
		}
	}

	public class Portfolio2
	{
		public List<Stock> Stocks { get; }
		public Matrix<double> Cov { get; }

		public Portfolio2(List<Stock> stocks, Matrix<double> covarianceMatrix)
		{
			Stocks = stocks;
			Cov = covarianceMatrix;
		}

		public double[] GetFactorCoefficients(string factor)
		{
			// TODO: Supply real factor loadings per stock for each factor.
			// For demo, return zeros.
			return new double[Stocks.Count];
		}
	}

	public class RiskEngine2
	{
		private readonly Portfolio2 _pf;
		private readonly double _totalCap, _targetVol, _riskBudget;
		private readonly double
			_utilizationThreshold,
			_maxGrossBook,
			_maxNetBook,
			_maxGrossSector,
			_maxNetSector,
			_maxGrossIndustry,
			_maxNetIndustry,
			_maxGrossSubIndustry,
			_maxNetSubIndustry,
			_maxPosition,
			_maxTop10Long,
			_maxTop10Short,
			_maxTop5Long,
			_maxTop5Short,
			_maxPosUnder5long,
			_maxPosUnder5short,
			_maxPosVsVolume20,
			_maxPosVsVolume50,
			_maxPosVsVolume100,
			_maxPosVsMarketCap5B,
			_maxNetPosVsMarketCap5B,
			_maxPosVsMarketCap1B5B,
			_maxNetPosVsMarketCap1B5B,
			_maxPosVsMarketCap500M1B,
			_maxNetPosVsMarketCap500M1B,
			_maxPosVsMarketCaplessthan500M,
			_maxNetPosVsMarketCaplessthan500M,
			_maxNetCurrency,
			_maxGrossEarnings,
			_maxNetEarnings;
		private readonly double _maxVaR95, _maxCVaR95, _minIdioRisk, _maxPredictedVol;
		private readonly Dictionary<string, double> _maxFactorRisk;

		public RiskEngine2(
			Portfolio2 p,
			double cap = 1e8,
			double tVol = 0.20, // 20% target volatility
			double rBud = 0.02, // 2% risk budget of total capital
								//double utilizationThreshold = 0.50, // 50% utilization threshold The denominator for limit is UCAP. If UCAP is too low, we use UT
								//EXPOSURE
			double maxGrossBook = 2.0,   // 200%
			double maxNetBook = 0.1,     // 10% PredBeta-Adjusted Net Book
										 //SECTOR 
			double maxGrossSector = 2.0, // 200%
										 //double maxNetSector = 0.12, // 12% PredBeta-Adjusted Net Sector
										 //INDUSTRY
			double maxGrossIndustry = 1.50, // 150%
											//double maxNetIndustry = 0.12, // 12% PredBeta-Adjusted Net Industry
											//SUBINDUSTRY 
			double maxGrossSubIndustry = 1.00, // 100%
											   //double maxNetSubIndustry = 0.12, // 12% PredBeta-Adjusted Net SubIndustry
											   //COUNTRY
											   //SINGLE NAME
			double maxPosition = 0.10, // 10% Max Position Single Stock
									   //TOP POSITIONS
									   //double maxTop10Long = 0.75, // 75% Max Top 10 Positions Combined
									   //double maxTop10Short = 0.65, // 65% Max Top 10 Positions Combined
									   //double maxTop5Long = 0.40, // 40% Max Top 5 Positions Combined
									   //double maxTop5Short = 0.35, // 35% Max Top 5 Positions Combined
									   //STOCK PRICE
			double maxPosUnder5long = 0.02, // 2% Max Position Long for Stocks under $5
			double maxPosUnder5short = 0.00, // 0% Max Position Short for Stocks under $5
											 //LIQUIDITY
			double maxPosVsVolume20 = 0.3, // 30% Max Position vs daily Volume 20%
										   //double maxPosVsVolume50 = 0.1, // 10% Max Position vs daily Volume 50%
										   //double maxPosVsVolume100 = 0.00, // 0% Max Position vs daily Volume 100%
			double maxPosVsMarketCap5B = 1.75, // 175% Max Position vs Market Cap 5B
											   //MKT CAP
											   //double maxNetPosVsMarketCap5B = 0.15, // 15% Max Net Position vs Market Cap 5B
											   //double maxPosVsMarketCap1B5B = 1.00, // 100% Max Position vs Market Cap 1B5B
											   //double maxNetPosVsMarketCap1B5B = 0.15, // 15% Max Net Position vs Market Cap 1B5B
											   //double maxPosVsMarketCap500M1B = 0.25, // 25% Max Position vs Market Cap 500M1B
											   //double maxNetPosVsMarketCap500M1B = 0.025, // 2.5% Max Net Position vs Market Cap 500M1B
											   //double maxPosVsMarketCaplessthan500M = 0.25, // 25% Max Position vs Market Cap lessthan500M
											   //double maxNetPosVsMarketCaplessthan500M = 0.025, // 2.5% Max Net Position vs Market Cap lessthan500M
											   //CURRENCY
			double maxNetCurrency = 0.10,
			double maxGrossEarnings = 0.20,
			//EARNINGS
			double maxNetEarnings = 0.10,

			//STATISTICAL LIMITS
			double maxVaR95 = 0.01, // Max VaR95 Single Position 1%
			double maxCVaR95 = 0.015, // Max CVaR95 Single Position 1.5%
									  //double maxMCVaR95Single = 0.15,      //Max Marginal CVaR95 Single Position 15%
			double minIdioRisk = 0.70, // Minimum Portfolio Idiosyncratic risk if 70%
										//double maxFactorRiskPerFactor = 18.0,  // Max Factor Risk of any Factor 18%
										//double maxPredictedVol = 0.12, //Max Portfolio Predicted Volatility 12%
										//double maxLossEquity5 = 0.02,         //If Market moves by +/- 5%, Max Portfolio Loss Equity 2.0%
										//double maxLossEquity10 = 0.035,   //If Market moves by +/- 10%, Max Portfolio Loss Equity 3.5%
			Dictionary<string, double> maxFactorRisk = null)   //Max Factor Risk per Factor 18%
		{
			_pf = p;
			_totalCap = cap;
			_targetVol = tVol;
			_riskBudget = rBud;
			//_utilizationThreshold = utilizationThreshold;
			_maxGrossBook = maxGrossBook;
			_maxNetBook = maxNetBook;
			_maxGrossSector = maxGrossSector;
			//_maxNetSector = maxNetSector;
			_maxGrossIndustry = maxGrossIndustry;
			//_maxNetIndustry = maxNetIndustry;
			_maxGrossSubIndustry = maxGrossSubIndustry;
			//_maxNetSubIndustry = maxNetSubIndustry;
			_maxPosition = maxPosition;
			//_maxTop10Long = maxTop10Long;
			//_maxTop10Short = maxTop10Short;
			//_maxTop5Long = maxTop5Long;
			//_maxTop5Short = maxTop5Short;
			_maxPosVsVolume20 = maxPosVsVolume20;
			//_maxPosVsVolume50 = maxPosVsVolume50;
			//_maxPosVsVolume100 = maxPosVsVolume100;
			_maxPosVsMarketCap5B = maxPosVsMarketCap5B;
			//_maxNetPosVsMarketCap5B = maxNetPosVsMarketCap5B;
			//_maxPosVsMarketCap1B5B = maxPosVsMarketCap1B5B;
			//_maxNetPosVsMarketCap1B5B = maxNetPosVsMarketCap1B5B;
			//_maxPosVsMarketCap500M1B = maxPosVsMarketCap500M1B;
			//_maxNetPosVsMarketCap500M1B = maxNetPosVsMarketCap500M1B;
			//_maxPosVsMarketCaplessthan500M = maxPosVsMarketCaplessthan500M;
			//_maxNetPosVsMarketCaplessthan500M = maxNetPosVsMarketCaplessthan500M;
			_maxNetCurrency = maxNetCurrency;
			_maxGrossEarnings = maxGrossEarnings;
			_maxNetEarnings = maxNetEarnings;
			_maxVaR95 = maxVaR95;
			_maxCVaR95 = maxCVaR95;
			//_maxMCVaR95 = maxMCVaR95;
			//_maxMCVaR95Single = maxMCVaR95Single;
			_minIdioRisk = minIdioRisk;
			//_maxPredictedVol = maxPredictedVol;
			//_maxLossEquity5= maxLossEquity5;
			//_maxLossEquity10 = _maxLossEquity10;
			_maxFactorRisk = maxFactorRisk
				?? new Dictionary<string, double>
				{ { "Mkt", 0.1 }, { "SMB", 0.05 }, { "HML", 0.05 } };
		}

		public Dictionary<string, double> Solve()
		{
			var model = new OPTANO.Modeling.Optimization.Model();
			int n = _pf.Stocks.Count;

			var w = _pf.Stocks.Select((stock, i) =>
			{
				double lb = stock.IsLong ? 0 : -_maxPosition;
				double ub = stock.IsLong ? _maxPosition : 0;
				return new Variable($"w[{i}]", lb, ub);
			}).ToArray();

			// VaR/CVaR variables
			const int S = 500;
			var var95 = new Variable("VaR95", 0, double.PositiveInfinity);
			var lambda = new Variable("lambda", 0, double.PositiveInfinity);
			var L = Enumerable.Range(0, S)
				.Select(s => new Variable($"L[{s}]", double.NegativeInfinity, double.PositiveInfinity))
				.ToArray();

			var mu = _pf.Stocks.Select(s => s.Mu).ToArray();
			var Sigma = _pf.Cov;
			// Initialize an empty expression
			Expression obj = Expression.EmptyExpression;

			// Add each term to the expression
			for (int i = 0; i < n; i++)
			{
				obj += mu[i] * w[i];
			}

			for (int i = 0; i < n; i++)
			{
				obj += mu[i] * w[i];
			}

			// Volatility constraint
			var volatilityTerm = Expression.Sum(Enumerable.Range(0, n).Select(i => w[i] * Sigma[i, i] * w[i]));

			//model.AddConstraint(volatilityTerm == _targetVol * _targetVol);

			// Exposure constraints
			model.AddConstraint(Expression.Sum(w) <= _maxGrossBook);
			model.AddConstraint(Expression.Sum(w) >= -_maxGrossBook);
			model.AddConstraint(Expression.Sum(w.Select(x => Expression.Absolute(x))) <= _maxGrossBook);
			//model.AddConstraint(Expression.Sum(w) <= _maxNetBook);
			//model.AddConstraint(Expression.Sum(w) >= -_maxNetBook);
			//model.AddConstraint(Expression.Sum(w.Select(x => Expression.Absolute(x))) <= _maxNetBook);

			//model.AddConstraint(Expression.Sum(w) <= _maxGrossSector);
			//model.AddConstraint(Expression.Sum(w) >= -_maxGrossSector);
			//model.AddConstraint(Expression.Sum(w.Select(x => Expression.Absolute(x))) <= _maxGrossSector);
			//model.AddConstraint(Expression.Sum(w) <= _maxNetSector);
			//model.AddConstraint(Expression.Sum(w) >= -_maxNetSector);
			//model.AddConstraint(Expression.Sum(w.Select(x => Expression.Absolute(x))) <= _maxNetSector);

			//model.AddConstraint(Expression.Sum(w) <= _maxGrossIndustry);
			//model.AddConstraint(Expression.Sum(w) >= -_maxGrossIndustry);
			//model.AddConstraint(Expression.Sum(w.Select(x => Expression.Absolute(x))) <= _maxGrossIndustry);
			//model.AddConstraint(Expression.Sum(w) <= _maxNetIndustry);
			//model.AddConstraint(Expression.Sum(w) >= -_maxNetIndustry);
			//model.AddConstraint(Expression.Sum(w.Select(x => Expression.Absolute(x))) <= _maxNetIndustry);

			//model.AddConstraint(Expression.Sum(w) <= _maxGrossSubIndustry);
			//model.AddConstraint(Expression.Sum(w) >= -_maxGrossSubIndustry);
			//model.AddConstraint(Expression.Sum(w.Select(x => Expression.Absolute(x))) <= _maxGrossSubIndustry);
			//model.AddConstraint(Expression.Sum(w) <= _maxNetSubIndustry);
			//model.AddConstraint(Expression.Sum(w) >= -_maxNetSubIndustry);
			//model.AddConstraint(Expression.Sum(w.Select(x => Expression.Absolute(x))) <= _maxNetSubIndustry);

			// Position limits
			for (int i = 0; i < n; i++)
			{
				model.AddConstraint(Expression.Absolute(w[i]) <= _maxPosition);

				//if (_pf.Stocks[i].AvgVolume > 0)
				//	model.AddConstraint(Expression.Absolute(w[i]) <=
				//		_maxPosVsVolume20 * _pf.Stocks[i].AvgVolume / _totalCap);

				//if (_pf.Stocks[i].AvgVolume > 0)
				//	model.AddConstraint(Expression.Absolute(w[i]) <=
				//		_maxPosVsVolume50 * _pf.Stocks[i].AvgVolume / _totalCap);

				//if (_pf.Stocks[i].AvgVolume > 0)
				//	model.AddConstraint(Expression.Absolute(w[i]) <=
				//		_maxPosVsVolume100 * _pf.Stocks[i].AvgVolume / _totalCap);

				//if (_pf.Stocks[i].MarketCap > 0)
				//	model.AddConstraint(Expression.Absolute(w[i]) <=
				//		_maxPosVsMarketCap5B * _pf.Stocks[i].MarketCap / _totalCap);

				//if (_pf.Stocks[i].MarketCap > 0)
				//	model.AddConstraint(Expression.Absolute(w[i]) <=
				//		_maxNetPosVsMarketCap5B * _pf.Stocks[i].MarketCap / _totalCap);

				//if (_pf.Stocks[i].MarketCap > 0)
				//	model.AddConstraint(Expression.Absolute(w[i]) <=
				//		_maxPosVsMarketCap1B5B * _pf.Stocks[i].MarketCap / _totalCap);

				//if (_pf.Stocks[i].MarketCap > 0)
				//	model.AddConstraint(Expression.Absolute(w[i]) <=
				//		_maxNetPosVsMarketCap1B5B * _pf.Stocks[i].MarketCap / _totalCap);

				//if (_pf.Stocks[i].MarketCap > 0)
				//	model.AddConstraint(Expression.Absolute(w[i]) <=
				//		_maxPosVsMarketCap500M1B * _pf.Stocks[i].MarketCap / _totalCap);

				//if (_pf.Stocks[i].MarketCap > 0)
				//	model.AddConstraint(Expression.Absolute(w[i]) <=
				//		_maxNetPosVsMarketCap500M1B * _pf.Stocks[i].MarketCap / _totalCap);

				//if (_pf.Stocks[i].MarketCap > 0)
				//	model.AddConstraint(Expression.Absolute(w[i]) <=
				//		_maxPosVsMarketCaplessthan500M * _pf.Stocks[i].MarketCap / _totalCap);

				//if (_pf.Stocks[i].MarketCap > 0)
				//	model.AddConstraint(Expression.Absolute(w[i]) <=
				//		_maxNetPosVsMarketCaplessthan500M * _pf.Stocks[i].MarketCap / _totalCap);

			}

			// Categorical caps
			//foreach (var tuple in new[]
			//{
			//	("Sector", _maxGrossSector),
			//	("Industry", _maxGrossIndustry),
			//	("SubIndustry", _maxGrossSubIndustry)
			//})
			//{
			//	foreach (var grp in _pf.Stocks.GroupBy(s => s.GetType()
			//		.GetProperty(tuple.Item1).GetValue(s)))
			//	{
			//		var idxs = grp.Select(s => _pf.Stocks.IndexOf(s));
			//		model.AddConstraint(Expression.Sum(idxs.Select(i => Expression.Absolute(w[i]))) <= tuple.Item2);
			//	}
			//}

			// Currency exposure
			//foreach (var ccyGroup in _pf.Stocks.GroupBy(s => s.Currency))
			//{
			//	var idxs = ccyGroup.Select(s => _pf.Stocks.IndexOf(s));
			//	model.AddConstraint(Expression.Absolute(Expression.Sum(idxs.Select(i => w[i]))) <= _maxNetCurrency);
			//}

			// Earnings exposure
			//var earningsIdx = _pf.Stocks
			//	.Select((s, i) => (s, i))
			//	.Where(si => si.s.IsEarningsPlay)
			//	.Select(si => si.i)
			//	.ToList();
			//if (earningsIdx.Any())
			//{
			//	model.AddConstraint(Expression.Sum(earningsIdx.Select(i => Expression.Absolute(w[i]))) <= _maxGrossEarnings);
			//	model.AddConstraint(Expression.Absolute(Expression.Sum(earningsIdx.Select(i => w[i]))) <= _maxNetEarnings);
			//}

			// Factor risk
			foreach (var kv in _maxFactorRisk)
			{
				var coefs = _pf.GetFactorCoefficients(kv.Key);
				// Build linear factor exposure: fac = sum_i coefs[i] * w[i]
				Expression fac = Expression.Sum(
					Enumerable.Range(0, n).Select(i => coefs[i] * w[i])
				);

				// Add absolute-value constraint: |fac| ≤ kv.Value
				model.AddConstraint(
					Expression.Absolute(fac, null) <= kv.Value
				);
			}

			// Min idiosyncratic variance
			//Expression idio = Expression.Sum(Enumerable.Range(0, n).Select(i => _pf.Stocks[i].Sigma * _pf.Stocks[i].Sigma * w[i] * w[i]));
			//model.AddConstraint(idio >= _minIdioRisk);

			// VaR/CVaR constraints
			//double α = 0.95;

			//// 1. Loss definitions per scenario
			//for (int s = 0; s < S; s++)
			//{
			//	var scenario = ReturnsScenario(s);  // double[]

			//	// build weighted return expression: ∑ scenario[i] * w[i]
			//	var weightedReturnExpr = Expression.Sum(
			//		Enumerable.Range(0, n)
			//				  .Select(i => scenario[i] * w[i])
			//	);
			//	// negate return correctly
			//	var negated = (-1) * weightedReturnExpr;
			//	model.AddConstraint(L[s] >= negated - lambda);
			//}

			//// 2. Sum of scenario losses
			//var sumL = Expression.Sum(L);

			//// 3. CVaR definition constraint
			//model.AddConstraint(
			//	var95 == lambda + (1.0 / (S * (1 - α))) * sumL
			//);

			//// 4. Risk limit constraints for VaR and CVaR
			//model.AddConstraint(var95 <= _maxVaR95);
			//model.AddConstraint(var95 <= _maxCVaR95);
			////model.AddConstraint(var95 <= _maxMCVaR95Single);


			// Solve
			using var solver = new HighsSolver14x();
			var sol = solver.Solve(model);

			var results = new Dictionary<string, double>();

			// Reporting
			Console.WriteLine("Ticker\tAlloc\tInvested ($)");
			for (int i = 0; i < n; i++)
			{
				double wi = sol.VariableValues[w[i].Name];
				Trace.WriteLine($"{_pf.Stocks[i].Ticker}\t{wi:F4}\t{wi * _totalCap:C0}");
				results[_pf.Stocks[i].Ticker] = wi * _totalCap;
			}
			//Trace.WriteLine($"VaR95: {sol.VariableValues[var95.Name]:P3}");
			//Trace.WriteLine($"Lambda: {sol.VariableValues[lambda.Name]:P3}");

			return results;
		}

		private Vector<double> ReturnsScenario(int s)
		{
			int n = _pf.Stocks.Count;
			return Vector<double>.Build.Random(n,
				new MathNet.Numerics.Distributions.Normal(0, 0.02));
		}
	}
}
