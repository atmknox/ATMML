using System;
using System.Collections.Generic;
using System.Linq;

namespace ATMML
{
	public sealed class GrossThrottleConfig
	{
		public int WindowDays { get; set; } = 10;
		public int PeriodsPerYear { get; set; } = 252;
		public double RiskFreeAnnual { get; set; } = 0.0;

		public double GrossMin { get; set; } = 0.45;

		// Hysteresis thresholds on rolling Sharpe
		public double OffThreshold { get; set; } = 0.0;
		public double OnThreshold { get; set; } = 0.5;

		// Ramp speeds per DAY of NAV updates (i.e., per trading day)
		public double RampDownPerDay { get; set; } = 0.15;
		public double RampUpPerDay { get; set; } = 0.10;

		public bool RequireFullWindow { get; set; } = true;

		public void Validate()
		{
			if (WindowDays < 2) throw new ArgumentException("WindowDays must be >= 2.");
			if (PeriodsPerYear <= 0) throw new ArgumentException("PeriodsPerYear must be > 0.");
			if (GrossMin <= 0 || GrossMin > 1.0) throw new ArgumentException("GrossMin must be in (0,1].");
			if (OnThreshold < OffThreshold) throw new ArgumentException("OnThreshold should be >= OffThreshold.");
			if (RampDownPerDay <= 0 || RampDownPerDay > 1.0) throw new ArgumentException("RampDownPerDay must be in (0,1].");
			if (RampUpPerDay <= 0 || RampUpPerDay > 1.0) throw new ArgumentException("RampUpPerDay must be in (0,1].");
		}
	}

	public sealed class NavUpdateResult
	{
		public DateTime Date { get; init; }
		public double Nav { get; init; }
		public double DailyPnL { get; init; }
		public double DailyReturn { get; init; }
		public double RollingSharpe { get; init; } // NaN until enough data
		public double GrossScaler { get; init; }   // current g_t after update
	}

	/// <summary>
	/// Rebalance-aware throttle:
	/// - Call UpdateNavDaily(...) whenever you have a new daily close vector.
	/// - Call ScaleOnRebalance(...) ONLY on rebalance dates to adjust target shares.
	///
	/// This avoids "filtering winners": you keep your trade list; you just size the whole book down/up.
	/// </summary>
	public sealed class RebalanceAwareGrossThrottle
	{
		private readonly GrossThrottleConfig _cfg;

		private DateTime? _prevDate;
		private Dictionary<string, double>? _prevCloses;

		private double _nav;
		private double _g = 1.0;

		// rolling return history (first element is 0 placeholder)
		private readonly List<double> _rets = new();
		private readonly List<DateTime> _dates = new();

		public RebalanceAwareGrossThrottle(GrossThrottleConfig cfg, double initialNav)
		{
			_cfg = cfg ?? throw new ArgumentNullException(nameof(cfg));
			_cfg.Validate();

			if (initialNav <= 0) throw new ArgumentException("initialNav must be > 0.");
			_nav = initialNav;
		}

		public double CurrentNav => _nav;
		public double CurrentGrossScaler => _g;

		/// <summary>
		/// Update NAV and rolling Sharpe using TODAY closes and the shares that were held over the interval (prev->today).
		///
		/// heldSharesOvernight: ticker->shares held from previous close to today's close (usually yesterday EOD shares).
		/// todaysCloses: ticker->today close for pricing (include at least tickers in heldSharesOvernight).
		///
		/// If called for the very first date, it initializes state and returns pnl=0.
		/// </summary>
		public NavUpdateResult UpdateNavDaily(
			DateTime dateTime,
			Dictionary<string, double> heldSharesOvernight,
			Dictionary<string, double> todaysCloses)
		{
			if (heldSharesOvernight == null) throw new ArgumentNullException(nameof(heldSharesOvernight));
			if (todaysCloses == null) throw new ArgumentNullException(nameof(todaysCloses));

			var date = dateTime.Date;

			// First day init
			if (_prevDate == null)
			{
				_prevDate = date;
				_prevCloses = new Dictionary<string, double>(todaysCloses, StringComparer.OrdinalIgnoreCase);

				_dates.Add(date);
				_rets.Add(0.0);

				return new NavUpdateResult
				{
					Date = date,
					Nav = _nav,
					DailyPnL = 0.0,
					DailyReturn = 0.0,
					RollingSharpe = double.NaN,
					GrossScaler = _g
				};
			}

			// Guard: require forward time
			if (date <= _prevDate.Value)
				throw new InvalidOperationException($"UpdateNavDaily out of order. Last={_prevDate:yyyy-MM-dd}, New={date:yyyy-MM-dd}");

			double pnl = 0.0;

			// Compute PnL using held shares over prev close -> today close
			foreach (var kv in heldSharesOvernight)
			{
				var tkr = kv.Key?.Trim();
				if (string.IsNullOrEmpty(tkr)) continue;

				double sh = kv.Value;
				if (sh == 0.0) continue;

				if (_prevCloses == null || !_prevCloses.TryGetValue(tkr, out var p0)) continue;
				if (!todaysCloses.TryGetValue(tkr, out var p1)) continue;

				if (double.IsNaN(p0) || double.IsNaN(p1)) continue;

				pnl += sh * (p1 - p0);
			}

			double priorNav = _nav;
			_nav = priorNav + pnl;
			double r = priorNav != 0.0 ? pnl / priorNav : 0.0;

			_dates.Add(date);
			_rets.Add(r);

			// Update throttle state (g) based on rolling Sharpe
			UpdateScalerFromSharpe();

			// Roll forward closes
			_prevDate = date;
			_prevCloses = new Dictionary<string, double>(todaysCloses, StringComparer.OrdinalIgnoreCase);

			return new NavUpdateResult
			{
				Date = date,
				Nav = _nav,
				DailyPnL = pnl,
				DailyReturn = r,
				RollingSharpe = RollingSharpeAt(_rets.Count - 1),
				GrossScaler = _g
			};
		}

		/// <summary>
		/// Call ONLY when you rebalance (daily/weekly/monthly).
		/// It scales your target shares by current g_t.
		/// </summary>
		public Dictionary<string, double> ScaleOnRebalance(Dictionary<string, double> targetShares)
		{
			if (targetShares == null) throw new ArgumentNullException(nameof(targetShares));

			// Fast path
			if (Math.Abs(_g - 1.0) < 1e-12)
				return new Dictionary<string, double>(targetShares, StringComparer.OrdinalIgnoreCase);

			var scaled = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
			foreach (var kv in targetShares)
			{
				var tkr = kv.Key?.Trim();
				if (string.IsNullOrEmpty(tkr)) continue;
				scaled[tkr] = _g * kv.Value;
			}
			return scaled;
		}

		private void UpdateScalerFromSharpe()
		{
			double s = RollingSharpeAt(_rets.Count - 1);
			bool haveWindow = !double.IsNaN(s);

			if (_cfg.RequireFullWindow && !haveWindow)
			{
				_g = 1.0;
				return;
			}

			if (haveWindow)
			{
				if (s <= _cfg.OffThreshold)
					_g = Math.Max(_cfg.GrossMin, _g - _cfg.RampDownPerDay);
				else if (s >= _cfg.OnThreshold)
					_g = Math.Min(1.0, _g + _cfg.RampUpPerDay);
				// else hold in hysteresis band
			}

			if (_g < _cfg.GrossMin) _g = _cfg.GrossMin;
			if (_g > 1.0) _g = 1.0;
		}

		private double RollingSharpeAt(int endIndexInclusive)
		{
			int w = _cfg.WindowDays;
			int start = endIndexInclusive - w + 1;

			// index 0 is the artificial 0 return; require start >= 1
			if (start < 1) return double.NaN;

			double rfDaily = _cfg.RiskFreeAnnual / _cfg.PeriodsPerYear;

			int n = 0;
			double sum = 0.0;
			double sumSq = 0.0;

			for (int i = start; i <= endIndexInclusive; i++)
			{
				double ex = _rets[i] - rfDaily;
				if (double.IsNaN(ex) || double.IsInfinity(ex)) continue;
				n++;
				sum += ex;
				sumSq += ex * ex;
			}

			if (n < 2) return double.NaN;

			double mean = sum / n;
			double var = (sumSq - n * mean * mean) / (n - 1);
			if (var <= 0.0) return double.NaN;

			double vol = Math.Sqrt(var);
			return Math.Sqrt(_cfg.PeriodsPerYear) * (mean / vol);
		}
	}
}
