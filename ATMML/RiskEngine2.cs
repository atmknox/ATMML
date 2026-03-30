using System;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Factorization;
using MathNet.Numerics.Statistics;
//using SolStatus = OPTANO.Modeling.Optimization.SolutionStatus;
using OPTANO.Modeling.Optimization;
using OPTANO.Modeling.Optimization.Enums;
using OPTANO.Modeling.Optimization.Interfaces;

namespace RiskEngine2
{
	// -----------------------------
	// Config / inputs
	// -----------------------------
	public enum HardSoft { Hard, Soft }
	public record Bound(double Value, HardSoft Mode = HardSoft.Hard, double PenaltyLambda = 0.0);
	public record BetaNeutralSpec(double ToleranceAbs = 0.0, HardSoft Mode = HardSoft.Hard, double PenaltyLambda = 0.0);
	public record ObjectiveWeights(double Return = 1.0, double Vol = 0.0, double RiskParity = 0.0);

	public record HedgeSpec(
		string Ticker = "SPY",
		double Beta = 1.0,
		double[] CovWithStocks = null,
		double Variance = 0.015 * 0.015,
		double BoundAbs = 5.0
	);

	public class RiskConstraintInputs
	{
		// Book
		public Bound? GrossBookAbs { get; set; } = new(2.0, HardSoft.Soft, 50);
		public Bound? NetBookAbs { get; set; } = new(0.10, HardSoft.Hard, 0);

		// Single-name
		public Bound? SingleNameCapAbs { get; set; } = new(0.10, HardSoft.Hard, 0);
		public Bound? Under5LongCap { get; set; } = new(0.02, HardSoft.Hard, 0);
		public Bound? Under5ShortCap { get; set; } = new(0.00, HardSoft.Hard, 0);

		// Liquidity vs ADV
		public Bound? LiquidityADV20 { get; set; } = new(0.30, HardSoft.Hard, 0);
		public Bound? LiquidityADV50 { get; set; } = null;
		public Bound? LiquidityADV100 { get; set; } = null;

		// Groups
		public Bound? SectorGross { get; set; } = new(1.50, HardSoft.Soft, 25);
		public Bound? SectorNetAbs { get; set; } = new(0.12, HardSoft.Soft, 25);
		public Bound? IndustryGross { get; set; } = new(1.00, HardSoft.Soft, 25);
		public Bound? IndustryNetAbs { get; set; } = new(0.12, HardSoft.Soft, 25);
		public Bound? SubIndGross { get; set; } = new(0.75, HardSoft.Soft, 25);
		public Bound? SubIndNetAbs { get; set; } = new(0.12, HardSoft.Soft, 25);

		// Currency & Earnings
		public Bound? CurrencyNetAbs { get; set; } = new(0.10, HardSoft.Soft, 15);
		public Bound? EarningsGross { get; set; } = new(0.20, HardSoft.Soft, 25);
		public Bound? EarningsNetAbs { get; set; } = new(0.10, HardSoft.Soft, 25);

		// Market-cap buckets (net)
		public Bound? MktCapNetGt5BAbs { get; set; } = null;
		public Bound? MktCapNet1to5BAbs { get; set; } = null;
		public Bound? MktCapNet500Mto1BAbs { get; set; } = null;
		public Bound? MktCapNetLt500MAbs { get; set; } = null;

		// Factors
		public Dictionary<string, Bound> FactorRiskAbs { get; set; } = new();

		// Beta neutrality
		public BetaNeutralSpec? BetaNeutral { get; set; } = new(0.0, HardSoft.Hard, 0);
	}

	// -----------------------------
	// Domain
	// -----------------------------
	public class Stock
	{
		public string Ticker { get; set; } = "";
		public List<double> Prices { get; set; } = new();
		public string Sector { get; set; } = "";
		public string Industry { get; set; } = "";
		public string SubIndustry { get; set; } = "";
		public double MarketCap { get; set; }
		public double AvgVolume { get; set; }  // shares/day
		public double Beta { get; set; }
		public string Currency { get; set; } = "USD";
		public bool IsEarningsPlay { get; set; }
		public bool IsLong { get; set; } = true;

		public double Mu { get; set; }
		public double Sigma { get; set; }

		public void ComputeReturnsAndStats()
		{
			if (Prices == null || Prices.Count < 2)
				throw new InvalidOperationException($"Insufficient price history for {Ticker}");

			var rets = new double[Prices.Count - 1];
			for (int t = 1; t < Prices.Count; t++)
			{
				var p0 = Prices[t - 1];
				var p1 = Prices[t];
				double r = (p0 > 0 && p1 > 0) ? Math.Log(p1 / p0) : 0.0;
				rets[t - 1] = IsFinite(r) ? r : 0.0;
			}
			var avg = rets.Average();
			var sd = Statistics.StandardDeviation(rets);

			Mu = IsFinite(avg) ? avg : 0.0;
			Sigma = (IsFinite(sd) && sd >= 0.0) ? sd : 0.0;
			if (!IsFinite(Beta)) Beta = 0.0;
		}

		public static Matrix<double> BuildCovarianceMatrix(List<Stock> stocks)
		{
			int N = stocks.Count;
			int len = stocks[0].Prices.Count;
			if (stocks.Any(s => s.Prices.Count != len))
				throw new ArgumentException("All stocks must share the same price length.");

			var R = Matrix<double>.Build.Dense(len - 1, N, 0.0);
			for (int j = 0; j < N; j++)
				for (int t = 1; t < len; t++)
				{
					var p0 = stocks[j].Prices[t - 1]; var p1 = stocks[j].Prices[t];
					double r = (p0 > 0 && p1 > 0) ? Math.Log(p1 / p0) : 0.0;
					R[t - 1, j] = IsFinite(r) ? r : 0.0;
				}

			var C = Matrix<double>.Build.Dense(N, N, 0.0);
			for (int i = 0; i < N; i++)
				for (int j = i; j < N; j++)
				{
					double c = Statistics.Covariance(R.Column(i).ToArray(), R.Column(j).ToArray());
					c = IsFinite(c) ? c : 0.0;
					C[i, j] = C[j, i] = c;
				}
			return C;
		}

		public static List<double> GenPrices(int n, double start, double mu, double sigma, Random rnd)
		{
			var p = new List<double>(n) { start };
			for (int t = 1; t < n; t++)
			{
				var u1 = 1.0 - rnd.NextDouble();
				var u2 = 1.0 - rnd.NextDouble();
				var eps = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
				p.Add(p[^1] * Math.Exp(mu + sigma * eps));
			}
			return p;
		}

		internal static bool IsFinite(double x) => !(double.IsNaN(x) || double.IsInfinity(x));
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

		public virtual double[] GetFactorCoefficients(string factor) => new double[Stocks.Count];
	}

	// -----------------------------
	// Engine (linear objective; penalty-ized constraints)
	// -----------------------------
	public class RiskEngine2
	{
		private readonly Portfolio2 _pf;
		private readonly double _totalCap;
		private readonly ObjectiveWeights _objW;
		private readonly HedgeSpec _hedge;
		private readonly RiskConstraintInputs _limits;
		private readonly double _ridge, _eigFloor;

		private const double AUX_UB_DEFAULT = 1e3;

		public RiskEngine2(
			Portfolio2 portfolio,
			double totalCapital,
			ObjectiveWeights objectiveWeights,
			RiskConstraintInputs limits,
			HedgeSpec hedge = null,
			double ridge = 1e-8,
			double eigenFloor = 1e-12)
		{
			_pf = portfolio;
			_totalCap = totalCapital;
			_objW = objectiveWeights;
			_limits = limits ?? new();
			_hedge = hedge;
			_ridge = ridge;
			_eigFloor = eigenFloor;
		}
		public (Dictionary<string, double> DollarWeights, double HedgeNotional) Solve()
		{
			// --- sanitize inputs ---
			SanitizeMus(_pf.Stocks);

			// (Optional) keep PSD clean-up; not required by the LP, but harmless.
			var _ = SanitizeCovariance(_pf.Cov, _ridge, _eigFloor); // result unused in LP
			bool hasHedge = _hedge != null;

			int n = _pf.Stocks.Count;
			var model = new OPTANO.Modeling.Optimization.Model { Name = "RiskSolve_LP" };

			// --- decision vars ---
			var w = new OPTANO.Modeling.Optimization.Variable[n];
			for (int i = 0; i < n; i++)
			{
				double lb = _pf.Stocks[i].IsLong ? 0.0 : -1.0;
				double ub = _pf.Stocks[i].IsLong ? 1.0 : 0.0;
				w[i] = new OPTANO.Modeling.Optimization.Variable(
					$"w[{i}]", lb, ub, OPTANO.Modeling.Optimization.Enums.VariableType.Continuous);
				model.AddVariable(w[i]);
			}

			OPTANO.Modeling.Optimization.Variable h = null;
			if (hasHedge)
			{
				double B = Math.Max(0.1, _hedge!.BoundAbs);
				h = new OPTANO.Modeling.Optimization.Variable(
					"h", -B, B, OPTANO.Modeling.Optimization.Enums.VariableType.Continuous);
				model.AddVariable(h);
			}

			// --- objective (linear): minimize -alpha * sum(mu_i * w_i) + penalties ---
			var mu = _pf.Stocks.Select(s => Stock.IsFinite(s.Mu) ? s.Mu : 0.0).ToArray();
			double alpha = Math.Max(0.0, _objW.Return);
			var obj = OPTANO.Modeling.Optimization.Expression.EmptyExpression;

			var ret = OPTANO.Modeling.Optimization.Expression.EmptyExpression;
			for (int i = 0; i < n; i++) ret += mu[i] * w[i];
			obj += (0.0 - alpha) * ret;

			// constraints → linear penalties (soft) or hard caps inside factory
			obj += ConstraintFactory.ApplyAll(model, w, h, _pf, _totalCap, _limits);

			// beta-neutrality (hard = equality; soft = |betaExp| ≤ tol with penalty)
			if (hasHedge && _limits.BetaNeutral is not null)
			{
				var spec = _limits.BetaNeutral!;
				var betaExp = OPTANO.Modeling.Optimization.Expression.EmptyExpression;
				for (int i = 0; i < n; i++) betaExp += _pf.Stocks[i].Beta * w[i];
				if (h != null) betaExp += _hedge!.Beta * h;

				double tol = Math.Max(0.0, spec.ToleranceAbs);
				if (spec.Mode == HardSoft.Hard && tol == 0.0)
				{
					// exact equality via two tight inequalities
					model.AddConstraint(betaExp <= 0.0, "BetaNeutral_le0");
					model.AddConstraint((0.0 - betaExp) <= 0.0, "BetaNeutral_ge0");
				}
				else
				{
					obj += RiskEngine2.AddAbsLinear(
						model, betaExp, "BetaNeutral",
						new Bound(tol, spec.Mode, spec.PenaltyLambda),
						ubHint: Math.Max(1.0, n));
				}
			}

			// tiny |w| tick so all columns have a cost
			obj += LinearTickOnW(model, w, 1e-9);

			model.AddObjective(new OPTANO.Modeling.Optimization.Objective(
				obj, "Min_Lin", OPTANO.Modeling.Optimization.Enums.ObjectiveSense.Minimize));

			// ---- SOLVE (Z3, fully-qualified) ----
			OPTANO.Modeling.Optimization.Interfaces.ISolver solver =
				new OPTANO.Modeling.Optimization.Solver.Z3.Z3Solver();

			var solution = solver.Solve(model);   // <-- use 'solution' consistently

			// ---- STATUS CHECK (string-based; no enum dependency) ----
			var statusStr = solution.Status.ToString();   // "Optimal", "Feasible", etc.
			if (statusStr != "Optimal" && statusStr != "Feasible")
			{
				throw new Exception($"No feasible solution. Status: {statusStr}");
			}

			// ---- EXTRACT (use Variable.Value; no dictionaries needed) ----
			var dollars = new Dictionary<string, double>(capacity: n);
			for (int i = 0; i < n; i++)
			{
				double wi = w[i].Value;                      // value populated by solver
				dollars[_pf.Stocks[i].Ticker] = wi * _totalCap;
			}

			double hedgeNotional = 0.0;
			if (h != null)
			{
				double hv = h.Value;
				hedgeNotional = hv * _totalCap;
			}

			return (dollars, hedgeNotional);
		}

		// ---------- sanitize / helpers ----------
		private static void SanitizeMus(List<Stock> stocks)
		{
			foreach (var s in stocks)
			{
				if (!Stock.IsFinite(s.Mu)) s.Mu = 0.0;
				if (!Stock.IsFinite(s.Sigma) || s.Sigma < 0.0) s.Sigma = 0.0;
				if (!Stock.IsFinite(s.Beta)) s.Beta = 0.0;
			}
		}

		private static Matrix<double> SanitizeCovariance(Matrix<double> C, double ridge, double eigFloor)
		{
			int n = C.RowCount;
			var A = Matrix<double>.Build.Dense(n, n, (i, j) =>
			{
				var v = C[i, j];
				return Stock.IsFinite(v) ? v : 0.0;
			});
			A = 0.5 * (A + A.Transpose());
			for (int i = 0; i < n; i++)
				if (!Stock.IsFinite(A[i, i]) || A[i, i] < 0.0) A[i, i] = 0.0;

			var evd = GetEvdSymmetric(A);
			var V = evd.EigenVectors;
			var evals = evd.EigenValues.Select(z =>
			{
				double r = z.Real;
				if (!Stock.IsFinite(r)) r = 0.0;
				return (r < eigFloor) ? eigFloor : r;
			}).ToArray();

			var D = Matrix<double>.Build.DiagonalOfDiagonalArray(evals);
			var Apsd = V * D * V.Transpose();
			for (int i = 0; i < n; i++) Apsd[i, i] += ridge;
			Apsd = 0.5 * (Apsd + Apsd.Transpose());
			return Apsd;
		}

		private static (Matrix<double>, bool) BuildAugmentedCov(Matrix<double> covClean, HedgeSpec hedge, List<Stock> stocks, double ridge, double eigFloor)
		{
			int n = stocks.Count;
			if (hedge is null) return (covClean, false);

			var S = Matrix<double>.Build.Dense(n + 1, n + 1, 0.0);
			for (int i = 0; i < n; i++)
				for (int j = 0; j < n; j++)
					S[i, j] = covClean[i, j];

			double[] covH = hedge.CovWithStocks ?? stocks.Select(s => s.Beta * hedge.Variance).ToArray();
			for (int i = 0; i < n; i++)
			{
				double v = Stock.IsFinite(covH[i]) ? covH[i] : 0.0;
				S[i, n] = S[n, i] = v;
			}
			S[n, n] = (Stock.IsFinite(hedge.Variance) && hedge.Variance > 0.0) ? hedge.Variance : ridge;

			var Spsd = SanitizeCovariance(S, ridge, eigFloor);
			return (Spsd, true);
		}

		private static Evd<double> GetEvdSymmetric(Matrix<double> A)
		{
			var matrixType = typeof(Matrix<double>);
			var symmType = matrixType.Assembly.GetType("MathNet.Numerics.LinearAlgebra.Factorization.Symmetricity");
			if (symmType != null)
			{
				var evdMethod = matrixType.GetMethod("Evd", new[] { symmType });
				if (evdMethod != null)
				{
					var symmetricValue = Enum.Parse(symmType, "Symmetric");
					return (Evd<double>)evdMethod.Invoke(A, new[] { symmetricValue });
				}
			}

			var evdBool = matrixType.GetMethod("Evd", new[] { typeof(bool) });
			if (evdBool != null)
				return (Evd<double>)evdBool.Invoke(A, new object[] { true });

			return A.Evd();
		}

		// |expr| ≤ b.Value (hard) or ≤ b.Value + slack (soft) → linear penalty
		internal static OPTANO.Modeling.Optimization.Expression AddAbsLinear(
			OPTANO.Modeling.Optimization.Model m,
			OPTANO.Modeling.Optimization.Expression expr,
			string name,
			Bound b,
			double ubHint = AUX_UB_DEFAULT)
		{
			var penalty = OPTANO.Modeling.Optimization.Expression.EmptyExpression;

			var z = new OPTANO.Modeling.Optimization.Variable($"{name}_abs", 0.0, ubHint, OPTANO.Modeling.Optimization.Enums.VariableType.Continuous);
			m.AddVariable(z);
			m.AddConstraint(z >= expr, $"{name}_pos");
			m.AddConstraint(z >= (0.0 - expr), $"{name}_neg");

			if (b.Mode == HardSoft.Hard)
			{
				m.AddConstraint(z <= b.Value, $"{name}_cap");
			}
			else
			{
				var s = new OPTANO.Modeling.Optimization.Variable($"{name}_slack", 0.0, ubHint, OPTANO.Modeling.Optimization.Enums.VariableType.Continuous);
				m.AddVariable(s);
				m.AddConstraint(z <= b.Value + s, $"{name}_cap");
				if (b.PenaltyLambda > 0) penalty += (0.0 - b.PenaltyLambda) * s;
			}
			return penalty;
		}

		// Tiny |w_i| regularizer so columns have costs
		private static OPTANO.Modeling.Optimization.Expression LinearTickOnW(
			OPTANO.Modeling.Optimization.Model m,
			OPTANO.Modeling.Optimization.Variable[] w,
			double epsilon = 1e-9)
		{
			if (epsilon <= 0) return OPTANO.Modeling.Optimization.Expression.EmptyExpression;

			var reg = OPTANO.Modeling.Optimization.Expression.EmptyExpression;
			for (int i = 0; i < w.Length; i++)
			{
				var a = new OPTANO.Modeling.Optimization.Variable($"tick_abs_{i}", 0.0, 1.0, OPTANO.Modeling.Optimization.Enums.VariableType.Continuous);
				m.AddVariable(a);
				m.AddConstraint(a >= w[i], $"tick_pos_{i}");
				m.AddConstraint(a >= (0.0 - w[i]), $"tick_neg_{i}");
				reg += epsilon * a;
			}
			return reg;
		}
	}

	// -----------------------------
	// Constraint Factory
	// -----------------------------
	public static class ConstraintFactory
	{
		public static OPTANO.Modeling.Optimization.Expression ApplyAll(
			OPTANO.Modeling.Optimization.Model model,
			OPTANO.Modeling.Optimization.Variable[] w,
			OPTANO.Modeling.Optimization.Variable h,
			Portfolio2 pf,
			double totalCap,
			RiskConstraintInputs L)
		{
			var penalty = OPTANO.Modeling.Optimization.Expression.EmptyExpression;
			int n = pf.Stocks.Count;

			// Gross book: sum |w_i| ≤ cap
			if (L.GrossBookAbs is Bound gb)
			{
				var abs = new List<OPTANO.Modeling.Optimization.Variable>(n);
				for (int i = 0; i < n; i++)
				{
					var a = new OPTANO.Modeling.Optimization.Variable($"Gross_abs_{i}", 0.0, 1.0, OPTANO.Modeling.Optimization.Enums.VariableType.Continuous);
					model.AddVariable(a);
					model.AddConstraint(a >= w[i], $"Gross_pos_{i}");
					model.AddConstraint(a >= (0.0 - w[i]), $"Gross_neg_{i}");
					abs.Add(a);
				}
				var sumAbs = OPTANO.Modeling.Optimization.Expression.EmptyExpression;
				foreach (var v in abs) sumAbs += v;

				if (gb.Mode == HardSoft.Hard) model.AddConstraint(sumAbs <= gb.Value, "Gross_cap");
				else
				{
					var s = new OPTANO.Modeling.Optimization.Variable("Gross_slack", 0.0, Math.Max(1.0, n), OPTANO.Modeling.Optimization.Enums.VariableType.Continuous);
					model.AddVariable(s);
					model.AddConstraint(sumAbs <= gb.Value + s, "Gross_cap");
					if (gb.PenaltyLambda > 0) penalty += (0.0 - gb.PenaltyLambda) * s;
				}
			}

			// Net book: |sum w_i| ≤ cap
			if (L.NetBookAbs is Bound nb)
			{
				var netSum = OPTANO.Modeling.Optimization.Expression.EmptyExpression;
				for (int i = 0; i < n; i++) netSum += w[i];

				penalty += RiskEngine2.AddAbsLinear(model, netSum, "NetBook", nb, ubHint: Math.Max(1.0, n));
			}

			// Single-name |w_i| caps
			if (L.SingleNameCapAbs is Bound sn)
				for (int i = 0; i < n; i++)
					penalty += RiskEngine2.AddAbsLinear(model, w[i], $"Single_{i}", sn, ubHint: 1.0);

			// Under $5 specials
			for (int i = 0; i < n; i++)
			{
				var s = pf.Stocks[i];
				if (s.Prices.Count == 0) continue;
				double px = s.Prices[^1];
				if (px < 5.0)
				{
					if (s.IsLong && L.Under5LongCap is Bound u5L)
						penalty += RiskEngine2.AddAbsLinear(model, w[i], $"Under5_Long_{i}", u5L, ubHint: 1.0);
					if (!s.IsLong && L.Under5ShortCap is Bound u5S)
						penalty += RiskEngine2.AddAbsLinear(model, w[i], $"Under5_Short_{i}", u5S, ubHint: 1.0);
				}
			}

			// Liquidity (ADV)
			void AddLiq(Bound? b, string tag)
			{
				if (b is not Bound q) return;
				for (int i = 0; i < n; i++)
				{
					var s = pf.Stocks[i];
					if (s.AvgVolume <= 0 || s.Prices.Count == 0) continue;
					double px = s.Prices[^1];
					double notionalADV = s.AvgVolume * px;
					double capWeight = (notionalADV / totalCap) * q.Value;
					penalty += RiskEngine2.AddAbsLinear(model, w[i], $"Liq_{tag}_{i}",
						new Bound(capWeight, q.Mode, q.PenaltyLambda), ubHint: 1.0);
				}
			}
			AddLiq(L.LiquidityADV20, "ADV20");
			AddLiq(L.LiquidityADV50, "ADV50");
			AddLiq(L.LiquidityADV100, "ADV100");

			// Sector: gross & net
			if (L.SectorGross is Bound sg)
				foreach (var g in pf.Stocks.Select((s, i) => (s, i)).GroupBy(t => t.s.Sector ?? "Unknown"))
					penalty += AddGroupGross(model, w, g.Select(t => t.i), $"SectorGross_{San(g.Key)}", sg);

			if (L.SectorNetAbs is Bound snet)
				foreach (var g in pf.Stocks.Select((s, i) => (s, i)).GroupBy(t => t.s.Sector ?? "Unknown"))
				{
					var groupSum = OPTANO.Modeling.Optimization.Expression.EmptyExpression;
					foreach (var t in g) groupSum += w[t.i];
					penalty += RiskEngine2.AddAbsLinear(model, groupSum, $"SectorNet_{San(g.Key)}", snet, ubHint: Math.Max(1.0, g.Count()));
				}

			// Industry: gross & net
			if (L.IndustryGross is Bound ig)
				foreach (var g in pf.Stocks.Select((s, i) => (s, i)).GroupBy(t => t.s.Industry ?? "Unknown"))
					penalty += AddGroupGross(model, w, g.Select(t => t.i), $"IndGross_{San(g.Key)}", ig);

			if (L.IndustryNetAbs is Bound inet)
				foreach (var g in pf.Stocks.Select((s, i) => (s, i)).GroupBy(t => t.s.Industry ?? "Unknown"))
				{
					var groupSum = OPTANO.Modeling.Optimization.Expression.EmptyExpression;
					foreach (var t in g) groupSum += w[t.i];
					penalty += RiskEngine2.AddAbsLinear(model, groupSum, $"IndNet_{San(g.Key)}", inet, ubHint: Math.Max(1.0, g.Count()));
				}

			// Sub-industry: gross & net
			if (L.SubIndGross is Bound sig)
				foreach (var g in pf.Stocks.Select((s, i) => (s, i)).GroupBy(t => t.s.SubIndustry ?? "Unknown"))
					penalty += AddGroupGross(model, w, g.Select(t => t.i), $"SubIndGross_{San(g.Key)}", sig);

			if (L.SubIndNetAbs is Bound sinet)
				foreach (var g in pf.Stocks.Select((s, i) => (s, i)).GroupBy(t => t.s.SubIndustry ?? "Unknown"))
				{
					var groupSum = OPTANO.Modeling.Optimization.Expression.EmptyExpression;
					foreach (var t in g) groupSum += w[t.i];
					penalty += RiskEngine2.AddAbsLinear(model, groupSum, $"SubIndNet_{San(g.Key)}", sinet, ubHint: Math.Max(1.0, g.Count()));
				}

			// Currency net
			if (L.CurrencyNetAbs is Bound cnet)
				foreach (var g in pf.Stocks.Select((s, i) => (s, i)).GroupBy(t => t.s.Currency ?? "UNK"))
				{
					var groupSum = OPTANO.Modeling.Optimization.Expression.EmptyExpression;
					foreach (var t in g) groupSum += w[t.i];
					penalty += RiskEngine2.AddAbsLinear(model, groupSum, $"CcyNet_{San(g.Key)}", cnet, ubHint: Math.Max(1.0, g.Count()));
				}

			// Earnings group
			var earnIdx = pf.Stocks.Select((s, i) => (s, i)).Where(t => t.s.IsEarningsPlay).Select(t => t.i).ToList();
			if (earnIdx.Count > 0)
			{
				if (L.EarningsGross is Bound eg)
					penalty += AddGroupGross(model, w, earnIdx, "EarningsGross", eg);

				if (L.EarningsNetAbs is Bound en)
				{
					var earnSum = OPTANO.Modeling.Optimization.Expression.EmptyExpression;
					foreach (var i in earnIdx) earnSum += w[i];
					penalty += RiskEngine2.AddAbsLinear(model, earnSum, "EarningsNet", en, ubHint: Math.Max(1.0, earnIdx.Count));
				}
			}

			// Market-cap buckets (net)
			static bool In(double mc, double lo, double hi) => mc >= lo && mc < hi;
			const double M = 1_000_000.0, B = 1_000_000_000.0;

			if (L.MktCapNetGt5BAbs is Bound m5)
			{
				var ixs = pf.Stocks.Select((s, i) => (s, i)).Where(t => t.s.MarketCap >= 5 * B).Select(t => t.i).ToList();
				if (ixs.Count > 0)
				{
					var sum = OPTANO.Modeling.Optimization.Expression.EmptyExpression;
					foreach (var i in ixs) sum += w[i];
					penalty += RiskEngine2.AddAbsLinear(model, sum, "MktCap_Gt5B", m5, ubHint: Math.Max(1.0, ixs.Count));
				}
			}
			if (L.MktCapNet1to5BAbs is Bound m15)
			{
				var ixs = pf.Stocks.Select((s, i) => (s, i)).Where(t => In(t.s.MarketCap, 1 * B, 5 * B)).Select(t => t.i).ToList();
				if (ixs.Count > 0)
				{
					var sum = OPTANO.Modeling.Optimization.Expression.EmptyExpression;
					foreach (var i in ixs) sum += w[i];
					penalty += RiskEngine2.AddAbsLinear(model, sum, "MktCap_1to5B", m15, ubHint: Math.Max(1.0, ixs.Count));
				}
			}
			if (L.MktCapNet500Mto1BAbs is Bound m51)
			{
				var ixs = pf.Stocks.Select((s, i) => (s, i)).Where(t => In(t.s.MarketCap, 500 * M, 1 * B)).Select(t => t.i).ToList();
				if (ixs.Count > 0)
				{
					var sum = OPTANO.Modeling.Optimization.Expression.EmptyExpression;
					foreach (var i in ixs) sum += w[i];
					penalty += RiskEngine2.AddAbsLinear(model, sum, "MktCap_500Mto1B", m51, ubHint: Math.Max(1.0, ixs.Count));
				}
			}
			if (L.MktCapNetLt500MAbs is Bound ml)
			{
				var ixs = pf.Stocks.Select((s, i) => (s, i)).Where(t => t.s.MarketCap > 0 && t.s.MarketCap < 500 * M).Select(t => t.i).ToList();
				if (ixs.Count > 0)
				{
					var sum = OPTANO.Modeling.Optimization.Expression.EmptyExpression;
					foreach (var i in ixs) sum += w[i];
					penalty += RiskEngine2.AddAbsLinear(model, sum, "MktCap_Lt500M", ml, ubHint: Math.Max(1.0, ixs.Count));
				}
			}

			// Factor exposures: |sum f_i w_i| ≤ bound
			if (L.FactorRiskAbs is not null)
				foreach (var kv in L.FactorRiskAbs)
				{
					var f = pf.GetFactorCoefficients(kv.Key) ?? new double[n];
					var fac = OPTANO.Modeling.Optimization.Expression.EmptyExpression;
					for (int i = 0; i < n; i++) fac += f[i] * w[i];
					penalty += RiskEngine2.AddAbsLinear(model, fac, $"Factor_{San(kv.Key)}", kv.Value, ubHint: Math.Max(1.0, n));
				}

			return penalty;
		}

		private static OPTANO.Modeling.Optimization.Expression AddGroupGross(
			OPTANO.Modeling.Optimization.Model m,
			OPTANO.Modeling.Optimization.Variable[] w,
			IEnumerable<int> idxs,
			string name,
			Bound b)
		{
			var penalty = OPTANO.Modeling.Optimization.Expression.EmptyExpression;
			var I = idxs.ToArray();
			int k = I.Length;

			var absVars = new List<OPTANO.Modeling.Optimization.Variable>(k);
			for (int t = 0; t < k; t++)
			{
				int i = I[t];
				var ai = new OPTANO.Modeling.Optimization.Variable($"{name}_abs_i{i}", 0.0, 1.0, OPTANO.Modeling.Optimization.Enums.VariableType.Continuous);
				m.AddVariable(ai);
				m.AddConstraint(ai >= w[i], $"{name}_pos_i{i}");
				m.AddConstraint(ai >= (0.0 - w[i]), $"{name}_neg_i{i}");
				absVars.Add(ai);
			}

			var sumAbs = OPTANO.Modeling.Optimization.Expression.EmptyExpression;
			foreach (var v in absVars) sumAbs += v;

			if (b.Mode == HardSoft.Hard)
			{
				m.AddConstraint(sumAbs <= b.Value, $"{name}_cap");
			}
			else
			{
				var s = new OPTANO.Modeling.Optimization.Variable($"{name}_slack", 0.0, Math.Max(1.0, k), OPTANO.Modeling.Optimization.Enums.VariableType.Continuous);
				m.AddVariable(s);
				m.AddConstraint(sumAbs <= b.Value + s, $"{name}_cap");
				if (b.PenaltyLambda > 0) penalty += (0.0 - b.PenaltyLambda) * s;
			}
			return penalty;
		}

		private static string San(string raw)
			=> string.Concat((raw ?? "UNK").Select(c => char.IsLetterOrDigit(c) ? c : '_'));
	}

	// -----------------------------
	// Example (no Main) — call ExampleRun.Run() from your Program.cs
	// -----------------------------
	public static class ExampleRun
	{
		public static void Run()
		{
			var rnd = new Random(7);
			var stocks = new List<Stock>
			{
				new Stock { Ticker="AAPL", Sector="Tech", Industry="HW",  SubIndustry="Phones",  Beta=1.20, MarketCap=3_000_000_000_000, AvgVolume=8.0e7, Prices=Stock.GenPrices(260, 185, 0.0005, 0.02, rnd) },
				new Stock { Ticker="MSFT", Sector="Tech", Industry="SW",  SubIndustry="Systems", Beta=1.05, MarketCap=3_000_000_000_000, AvgVolume=3.5e7, Prices=Stock.GenPrices(260, 330, 0.0004, 0.018, rnd) },
				new Stock { Ticker="XOM",  Sector="Energy",Industry="Oil", SubIndustry="Integrated", Beta=0.90, MarketCap=4.5e11, AvgVolume=2.2e7, Prices=Stock.GenPrices(260, 110, 0.0003, 0.022, rnd) },
				new Stock { Ticker="JNJ",  Sector="Health",Industry="Pharma",SubIndustry="Divers",  Beta=0.70, MarketCap=4.4e11, AvgVolume=7.0e6, Prices=Stock.GenPrices(260, 158, 0.0002, 0.017, rnd) },
				new Stock { Ticker="TSLA", Sector="Auto", Industry="EV",  SubIndustry="AutoMfr",    Beta=1.80, MarketCap=8.0e11, AvgVolume=1.1e8, Prices=Stock.GenPrices(260, 230, 0.0008, 0.035, rnd), IsLong=false }
			};
			foreach (var s in stocks) s.ComputeReturnsAndStats();
			var cov = Stock.BuildCovarianceMatrix(stocks);
			var pf = new Portfolio2(stocks, cov);

			var ow = new ObjectiveWeights(Return: 1.0, Vol: 0.0, RiskParity: 0.0); // linear-only for Z3
			var hedge = new HedgeSpec(Ticker: "SPY", Beta: 1.0, CovWithStocks: null, Variance: 0.015 * 0.015, BoundAbs: 5.0);

			var limits = new RiskConstraintInputs
			{
				GrossBookAbs = new(2.0, HardSoft.Soft, 50),
				NetBookAbs = new(0.05, HardSoft.Soft, 50),
				SingleNameCapAbs = new(0.10, HardSoft.Soft, 50),
				Under5LongCap = new(0.02, HardSoft.Soft, 50),
				Under5ShortCap = new(0.00, HardSoft.Soft, 50),
				LiquidityADV20 = new(0.30, HardSoft.Soft, 50),
				SectorGross = new(1.50, HardSoft.Soft, 25),
				SectorNetAbs = new(0.12, HardSoft.Soft, 25),
				IndustryGross = new(1.00, HardSoft.Soft, 25),
				IndustryNetAbs = new(0.12, HardSoft.Soft, 25),
				SubIndGross = new(0.75, HardSoft.Soft, 25),
				SubIndNetAbs = new(0.12, HardSoft.Soft, 25),
				CurrencyNetAbs = new(0.10, HardSoft.Soft, 15),
				EarningsGross = new(0.20, HardSoft.Soft, 25),
				EarningsNetAbs = new(0.10, HardSoft.Soft, 25),
				FactorRiskAbs = new Dictionary<string, Bound>(),
				BetaNeutral = new BetaNeutralSpec(0.0, HardSoft.Soft, 50)
			};

			var engine = new RiskEngine2(
				portfolio: pf,
				totalCapital: 100_000_000.0,
				objectiveWeights: ow,
				limits: limits,
				hedge: hedge,
				ridge: 1e-8,
				eigenFloor: 1e-12
			);

			var (dollars, hedgeNotional) = engine.Solve();

			Console.WriteLine("Ticker\tNotional ($)");
			foreach (var kv in dollars) Console.WriteLine($"{kv.Key}\t{kv.Value,15:N0}");
			Console.WriteLine($"Hedge({hedge.Ticker})\t{hedgeNotional,15:N0}");
		}
	}
}
