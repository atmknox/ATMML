using System;
using System.Collections.Generic;
using System.Linq;
using OPTANO.Modeling.Optimization;
using OPTANO.Modeling.Optimization.Enums;
using OPTANO.Modeling.Optimization.Solver.Z3;
//using MathNet.Numerics.Distributions;
//using MathNet.Numerics.Random;

namespace RiskEngineMN
{
	// ---------------------------------------
	// Basic inputs (no risk parity, no targets, no per-stock vol)
	// ---------------------------------------
	public enum Side { Long, Short }

	public sealed class Security
	{
		public string Ticker { get; init; } = "";
		public string Sector { get; init; } = "";
		public Side CandidateSide { get; init; }

		// factor stat
		public double Beta { get; init; }
		public double BetaTStat { get; init; }

		// direct expected annualized return (user-supplied or defaulted)
		public double ExpectedReturnAnnual { get; init; } = 0.0;
	}

	public sealed class Config
	{
		public double PortfolioValue { get; init; } = 100_000_000;

		// Hard limits
		public double MaxPositionPct { get; init; } = 0.10;     // per-name gross |w|
		public double SectorCapPct { get; init; } = 0.12;       // per-sector gross cap
		public double PortfolioVaR95LimitPct { get; init; } = 0.010;
		public double PortfolioCVaR95LimitPct { get; init; } = 0.015;
		public double PredictedVolLimitPct { get; init; } = 0.12; // used with CVaR ≤ 1.91 σ proxy

		// Single-name VaR cap (computed from |beta| * marketVol)
		public double SingleNameVaR95LimitPct { get; init; } = 0.15;

		// Neutrality
		public bool EnforceExactDollarNeutral { get; init; } = true;
		public bool EnforceExactBetaNeutral { get; init; } = true;
		public double NeutralityEps { get; init; } = 1e-6;

		// Sector beta neutrality
		public bool EnforceSectorBetaNeutral { get; init; } = true;
		public double SectorBetaNeutralEps { get; init; } = 1e-4;

		// Objective weights (μ − λ·CVaR95 − ε·||w||1)
		public double RiskAversionLambda { get; init; } = 1.0;
		public double L1RegularizerEps { get; init; } = 1e-4;

		// Scenario engine (single-factor)
		public int NumScenarios { get; init; } = 4000;
		public int HorizonDays { get; init; } = 1;
		public int RandomSeed { get; init; } = 1337;

		// Market factor assumptions (no per-name vols used anywhere)
		public double MarketVolAnnual { get; init; } = 0.20; // 20% annualized market vol
		public double MarketMuAnnual { get; init; } = 0.00; // market drift (optional)
	}

	// ---------------------------------------
	// One-factor scenario engine: r_i = μ_i*H/252 + β_i * σ_mkt_daily * √H * Z
	// ---------------------------------------

	internal static class Gauss
	{
		// Standard normal via Box–Muller
		public static double NextZ(Random rng)
		{
			// ensure (0,1] to avoid log(0)
			double u1 = 1.0 - rng.NextDouble();
			double u2 = 1.0 - rng.NextDouble();
			return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
		}
	}


	public static class ScenarioFactory
	{
		// r_i = μ_i*H/252 + β_i * σ_mkt_daily * √H * Z, single-factor
		public static double[,] BuildReturns(IReadOnlyList<Security> names, Config cfg)
		{
			var rng = new Random(cfg.RandomSeed);

			int S = cfg.NumScenarios;
			int N = names.Count;

			double dailyScale = 1.0 / Math.Sqrt(252.0);
			double horizonScale = Math.Sqrt(cfg.HorizonDays);
			double mktVolDaily = cfg.MarketVolAnnual * dailyScale;
			double mktMuDaily = cfg.MarketMuAnnual / 252.0;

			var R = new double[S, N];

			for (int s = 0; s < S; s++)
			{
				double Z = Gauss.NextZ(rng);                    // ~N(0,1)
				double marketShock = mktVolDaily * horizonScale * Z;

				for (int i = 0; i < N; i++)
				{
					var a = names[i];
					double muDaily_i = a.ExpectedReturnAnnual / 252.0;
					double drift = (muDaily_i + mktMuDaily) * cfg.HorizonDays;
					R[s, i] = drift + a.Beta * marketShock;
				}
			}
			return R;
		}
	}


	// ---------------------------------------
	// Output
	// ---------------------------------------
	public sealed class SolveResult
	{
		public record Position(string Ticker, string Sector, double Weight, double Dollars);

		public string SolverStatus { get; init; } = "";
		public double ObjectiveValue { get; init; }
		public List<Position> Positions { get; init; } = new();

		public double NetExposurePct { get; init; }
		public double GrossExposurePct { get; init; }
		public double BetaExposure { get; init; }

		public double ExpectedReturnPct { get; init; } // per-horizon μ (not annual)
		public double VaR95Pct { get; init; }
		public double CVaR95Pct { get; init; }
		public double VolProxyPct { get; init; } // ≈ CVaR95 / 1.910
	}

	// ---------------------------------------
	// Optimizer (LP with RU-CVaR on factor scenarios)
	// ---------------------------------------
	public static class RiskEngine
	{
		public static SolveResult Solve(IReadOnlyList<Security> universe, Config cfg)
		{
			if (universe == null || universe.Count == 0)
				throw new ArgumentException("Universe is empty.");

			int N = universe.Count;
			int S = cfg.NumScenarios;

			// Scenarios
			var R = ScenarioFactory.BuildReturns(universe, cfg);

			// Constants
			const double alpha = 0.95;
			double oneMinusAlpha = 1.0 - alpha;
			double z95 = 1.645;
			double dailyScale = 1.0 / Math.Sqrt(252.0);
			double horizonScale = Math.Sqrt(cfg.HorizonDays);
			const double cvar95_over_sigma_normal = 1.910;

			double mktVolDaily = cfg.MarketVolAnnual * dailyScale;

			var model = new Model() { Name = "MN_ZeroDrift_SectorBetaNeutral_LP_NoTargets_NoStockVol" };

			// Decision variables: w = wPlus - wMinus, and uAbs >= |w|
			var wPlus = new Variable[N];
			var wMinus = new Variable[N];
			var uAbs = new Variable[N];

			for (int i = 0; i < N; i++)
			{
				wPlus[i] = new Variable($"wPlus_{i}", 0.0, cfg.MaxPositionPct, VariableType.Continuous);
				wMinus[i] = new Variable($"wMinus_{i}", 0.0, cfg.MaxPositionPct, VariableType.Continuous);
				uAbs[i] = new Variable($"uAbs_{i}", 0.0, cfg.MaxPositionPct, VariableType.Continuous);

				model.AddVariable(wPlus[i]);
				model.AddVariable(wMinus[i]);
				model.AddVariable(uAbs[i]);

				// Respect candidate side
				if (universe[i].CandidateSide == Side.Long) wMinus[i].UpperBound = 0.0;
				if (universe[i].CandidateSide == Side.Short) wPlus[i].UpperBound = 0.0;

				// |w| epigraph (avoid unary minus)
				model.AddConstraint(uAbs[i] >= (wPlus[i] - wMinus[i]), $"abs1_{i}");
				model.AddConstraint(uAbs[i] >= (wMinus[i] - wPlus[i]), $"abs2_{i}");
			}

			// Helpers (expressions)
			Expression Net = Expression.Sum(Enumerable.Range(0, N).Select(i => wPlus[i] - wMinus[i]));
			Expression Gross = Expression.Sum(Enumerable.Range(0, N).Select(i => uAbs[i]));
			Expression BetaE = Expression.Sum(Enumerable.Range(0, N).Select(i => (wPlus[i] - wMinus[i]) * universe[i].Beta));

			// Per-horizon expected return: μ_i,h = ExpectedReturnAnnual/252 * H
			Expression MuE = Expression.Sum(Enumerable.Range(0, N).Select(i =>
				(wPlus[i] - wMinus[i]) * (universe[i].ExpectedReturnAnnual / 252.0 * cfg.HorizonDays)));

			// Neutrality constraints
			double eps = cfg.NeutralityEps;
			if (cfg.EnforceExactDollarNeutral)
			{
				model.AddConstraint(Net <= eps, "net_zero_upper");
				model.AddConstraint(((-1.0) * Net) <= eps, "net_zero_lower");
			}
			else
			{
				model.AddConstraint(Net <= 1e-4, "net_tight_upper");
				model.AddConstraint(((-1.0) * Net) <= 1e-4, "net_tight_lower");
			}

			if (cfg.EnforceExactBetaNeutral)
			{
				model.AddConstraint(BetaE <= eps, "beta_zero_upper");
				model.AddConstraint(((-1.0) * BetaE) <= eps, "beta_zero_lower");
			}
			else
			{
				model.AddConstraint(BetaE <= 2e-3, "beta_tight_upper");
				model.AddConstraint(((-1.0) * BetaE) <= 2e-3, "beta_tight_lower");
			}

			// Sector constraints (gross and (optional) sector beta neutrality)
			var sectors = universe.Select(x => x.Sector).Distinct().ToList();
			foreach (var sec in sectors)
			{
				var idx = Enumerable.Range(0, N).Where(i => universe[i].Sector == sec).ToList();

				model.AddConstraint(
					Expression.Sum(idx.Select(i => uAbs[i])) <= cfg.SectorCapPct,
					$"sector_cap_{sec}"
				);

				if (cfg.EnforceSectorBetaNeutral)
				{
					var secBeta = Expression.Sum(idx.Select(i => (wPlus[i] - wMinus[i]) * universe[i].Beta));
					model.AddConstraint(secBeta <= cfg.SectorBetaNeutralEps, $"sector_beta_upper_{sec}");
					model.AddConstraint(((-1.0) * secBeta) <= cfg.SectorBetaNeutralEps, $"sector_beta_lower_{sec}");
				}
			}

			// Single-name VaR95 caps using |beta| * market volatility (no per-name vol)
			for (int i = 0; i < N; i++)
			{
				double denom = z95 * Math.Max(1e-12, Math.Abs(universe[i].Beta) * mktVolDaily) * horizonScale;
				// weight cap s.t. VaR_i ≤ limit ⇒ |w_i| ≤ limit / denom
				double maxAbsFromVaR = cfg.SingleNameVaR95LimitPct / denom;
				double hardCap = Math.Min(cfg.MaxPositionPct, maxAbsFromVaR);
				model.AddConstraint(uAbs[i] <= hardCap, $"name_var95_{i}");
			}

			// RU-CVaR setup
			var nu = new Variable("nu", -0.5, 0.5, VariableType.Continuous);
			model.AddVariable(nu);

			var z = new Variable[S];
			for (int s = 0; s < S; s++)
			{
				z[s] = new Variable($"z_{s}", 0.0, 2.0, VariableType.Continuous);
				model.AddVariable(z[s]);

				// Loss_s = - Σ_i w_i r_{s,i} == Σ_i (wMinus - wPlus) * R[s,i]
				Expression Ls = Expression.Sum(
					Enumerable.Range(0, N).Select(i => (wMinus[i] - wPlus[i]) * R[s, i])
				);
				model.AddConstraint(z[s] >= (Ls - nu), $"cvar_epi_{s}");
			}

			Expression CVaR95 = nu + (1.0 / (oneMinusAlpha * S)) *
				Expression.Sum(Enumerable.Range(0, S).Select(s => (Expression)z[s]));

			// Portfolio risk caps
			model.AddConstraint(CVaR95 <= cfg.PortfolioCVaR95LimitPct, "cvar_cap");
			model.AddConstraint(nu <= cfg.PortfolioVaR95LimitPct, "var_cap");

			// Vol proxy: CVaR95 ≥ 1.91 σ ⇒ enforce σ ≤ PredictedVolLimit ⇒ CVaR95 ≤ 1.91 σ_max
			model.AddConstraint(CVaR95 <= cvar95_over_sigma_normal * cfg.PredictedVolLimitPct, "vol_proxy");

			// Objective: maximize μ − λ·CVaR95 − ε·||w||1
			var objExpr = MuE - cfg.RiskAversionLambda * CVaR95
						- cfg.L1RegularizerEps * Expression.Sum(Enumerable.Range(0, N).Select(i => (Expression)uAbs[i]));
			model.AddObjective(new Objective(objExpr, "max_mu_min_tail", ObjectiveSense.Maximize));

			// Solve
			using var solver = new Z3Solver();
			var result = solver.Solve(model);

			// Reconstruct values (no Expression.Value usage)
			var weights = new double[N];
			var absW = new double[N];
			for (int i = 0; i < N; i++)
			{
				weights[i] = wPlus[i].Value - wMinus[i].Value;
				absW[i] = uAbs[i].Value;
			}

			double net = weights.Sum();
			double gross = absW.Sum();
			double beta = Enumerable.Range(0, N).Sum(i => weights[i] * universe[i].Beta);
			double mu = Enumerable.Range(0, N).Sum(i => weights[i] * (universe[i].ExpectedReturnAnnual / 252.0 * cfg.HorizonDays));

			double nuVal = nu.Value;
			double cvar = nuVal + (1.0 / (oneMinusAlpha * S)) * Enumerable.Range(0, S).Sum(s => z[s].Value);
			double var95 = nuVal;
			double sigma = cvar / cvar95_over_sigma_normal;

			double l1 = absW.Sum();
			double objectiveValue = mu - cfg.RiskAversionLambda * cvar - cfg.L1RegularizerEps * l1;

			var positions = new List<SolveResult.Position>(N);
			for (int i = 0; i < N; i++)
			{
				positions.Add(new SolveResult.Position(
					universe[i].Ticker,
					universe[i].Sector,
					weights[i],
					weights[i] * cfg.PortfolioValue
				));
			}

			return new SolveResult
			{
				SolverStatus = result.Status.ToString(),
				ObjectiveValue = objectiveValue,
				Positions = positions.OrderByDescending(p => Math.Abs(p.Weight)).ToList(),
				NetExposurePct = net,
				GrossExposurePct = gross,
				BetaExposure = beta,
				ExpectedReturnPct = mu,
				VaR95Pct = var95,
				CVaR95Pct = cvar,
				VolProxyPct = sigma
			};
		}
	}

	// ---------------------------------------
	// Example usage (toy demo)
	// ---------------------------------------
	internal static class Example
	{
		public static void Run()
		{
			var rnd = new Random(7);

			// simple helpers (no targets, no per-name vol)
			Security L(string t, string s, double beta, double muAnn) =>
				new Security { Ticker = t, Sector = s, CandidateSide = Side.Long, Beta = beta, BetaTStat = 5, ExpectedReturnAnnual = muAnn };
			Security S(string t, string s, double beta, double muAnn) =>
				new Security { Ticker = t, Sector = s, CandidateSide = Side.Short, Beta = beta, BetaTStat = 4, ExpectedReturnAnnual = muAnn };

			string[] secs = { "Tech", "Health", "Industrials", "Financials", "Energy", "Staples", "Discretionary", "Utilities", "Materials", "Communications" };

			var longs = new List<Security>();
			for (int k = 0; k < 30; k++)
			{
				string sec = secs[k % secs.Length];
				double beta = 1.2 + 0.6 * rnd.NextDouble();   // higher beta longs
				double muAnn = 0.05 + 0.10 * rnd.NextDouble(); // +5% to +15% annual
				longs.Add(L($"L{k + 1}", sec, beta, muAnn));
			}

			var shorts = new List<Security>();
			for (int k = 0; k < 20; k++)
			{
				string sec = secs[(k * 3) % secs.Length];
				double beta = 0.2 + 0.6 * rnd.NextDouble();   // lower beta shorts
				double muAnn = -0.02 - 0.08 * rnd.NextDouble(); // -2% to -10% annual
				shorts.Add(S($"S{k + 1}", sec, beta, muAnn));
			}

			var universe = longs.Concat(shorts).ToList();

			var cfg = new Config
			{
				PortfolioValue = 100_000_000,
				MaxPositionPct = 0.10,
				SectorCapPct = 0.12,
				PortfolioVaR95LimitPct = 0.010,
				PortfolioCVaR95LimitPct = 0.015,
				PredictedVolLimitPct = 0.12,
				SingleNameVaR95LimitPct = 0.15,

				EnforceExactDollarNeutral = true,
				EnforceExactBetaNeutral = true,
				NeutralityEps = 1e-6,

				EnforceSectorBetaNeutral = true,
				SectorBetaNeutralEps = 1e-4,

				RiskAversionLambda = 1.0,
				L1RegularizerEps = 1e-4,

				NumScenarios = 4000,
				HorizonDays = 1,
				RandomSeed = 1337,

				// market factor only
				MarketVolAnnual = 0.20,
				MarketMuAnnual = 0.00
			};

			var res = RiskEngine.Solve(universe, cfg);

			Console.WriteLine($"Status: {res.SolverStatus}");
			Console.WriteLine($"Obj: {res.ObjectiveValue:0.000000}");
			Console.WriteLine($"Net: {res.NetExposurePct:P4}  Gross: {res.GrossExposurePct:P2}  Beta: {res.BetaExposure:0.000000}");
			Console.WriteLine($"Mu(h): {res.ExpectedReturnPct:P4}  VaR95: {res.VaR95Pct:P3}  CVaR95: {res.CVaR95Pct:P3}  Sigma(proxy): {res.VolProxyPct:P3}");

			foreach (var p in res.Positions.Take(12))
				Console.WriteLine($"{p.Ticker,-6} {p.Sector,-14} w={p.Weight,8:P2}  ${p.Dollars / 1_000_000,6:0.00}M");
		}
	}
}
