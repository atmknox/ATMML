using System;
using System.Collections.Generic;
using System.Linq;

namespace ATMML
{
	/// <summary>
	/// Converts the Friday rebalance trade lists into FlexOneTrade objects
	/// ready for submission via IFlexOneOrderBridge.SubmitRebalance().
	///
	/// Direction codes (from Trade.Direction):
	///   +2 = NewLong      +3 = ExitLong     +4 = AddLong      +5 = ReduceLong
	///   -2 = NewShort     -3 = CoverShort   -4 = AddShort     -5 = ReduceShort
	///
	/// Share calculation mirrors the portfolio grid logic exactly:
	///   Exit/Cover  (|dir|==3) : full confirmed position at tradesTime1
	///   Add/Reduce  (|dir|==4,5): delta = tradesTime2 shares − tradesTime1 shares
	///   New entry   (|dir|==2)  : full tradesTime2 size
	/// </summary>
	public static class FlexOneTradeAdapter
	{
		/// <summary>
		/// Builds the complete FlexOne order list from the four Friday rebalance buckets.
		/// Pass the same four lists and timestamps used to draw the portfolio grid.
		/// </summary>
		public static List<FlexOneTrade> BuildFlexOneTrades(
			IEnumerable<Trade> enteredTrades,
			IEnumerable<Trade> exitedTrades,
			IEnumerable<Trade> addTrades,
			IEnumerable<Trade> reduceTrades,
			DateTime tradesTime1,
			DateTime tradesTime2,
			Dictionary<string, double> shares,
			Dictionary<string, double> price)
		{
			var result = new List<FlexOneTrade>();

			var proposedOrders = new List<Trade>();
			proposedOrders.AddRange(enteredTrades);
			proposedOrders.AddRange(exitedTrades);
			proposedOrders.AddRange(addTrades);
			proposedOrders.AddRange(reduceTrades);

			foreach (var ot in proposedOrders)
			{
				var ticker = ot.Ticker;
				if (!shares.ContainsKey(ticker) || !price.ContainsKey(ticker))
					continue;

				int dir = (int)ot.Direction;

				// ── Share calculation (mirrors portfolio grid logic exactly) ────────
				var ot2Key = ot.Shares.Keys
					.Where(k => k.Date == tradesTime2.Date && ot.Shares[k] != 0)
					.OrderByDescending(k => k).FirstOrDefault();
				var ot2Shares = ot2Key != default(DateTime) ? ot.Shares[ot2Key] : 0;

				var ot1Key = ot.Shares.Keys
					.Where(k => k.Date == tradesTime1.Date)
					.OrderByDescending(k => k).FirstOrDefault();
				var ot1Shares = ot1Key != default(DateTime) ? ot.Shares[ot1Key] : 0;

				var units = (Math.Abs(dir) == 3)
					? Math.Abs(shares[ticker])              // exit: full confirmed position
					: (Math.Abs(dir) == 4 || Math.Abs(dir) == 5)
						? Math.Abs(ot2Shares - ot1Shares)   // add/reduce: t2 minus t1 delta
						: Math.Abs(ot2Shares);              // new entry: full new size

				int intUnits = (int)Math.Round(units);
				if (intUnits <= 0)
				{
					Console.WriteLine($"[FlexOneAdapter] Skipping {ticker} — zero shares computed (dir={dir}).");
					continue;
				}

				// ── Direction → FlexOneTradeAction ───────────────────────────────
				FlexOneTradeAction? action = dir switch
				{
					2 => FlexOneTradeAction.NewLong,
					3 => FlexOneTradeAction.CloseLong,
					4 => FlexOneTradeAction.AddLong,
					5 => FlexOneTradeAction.ReduceLong,
					-2 => FlexOneTradeAction.NewShort,
					-3 => FlexOneTradeAction.CoverShort,
					-4 => FlexOneTradeAction.NewShort,      // AddShort = more short exposure
					-5 => FlexOneTradeAction.CoverShort,    // ReduceShort = partial cover
					_ => (FlexOneTradeAction?)null
				};

				if (action == null)
				{
					Console.WriteLine($"[FlexOneAdapter] Skipping {ticker} — unknown direction {dir}.");
					continue;
				}

				result.Add(new FlexOneTrade
				{
					Ticker = ticker,
					Action = action.Value,
					Shares = intUnits,
					LimitPrice = 0.0,   // 0 = MOC market order (Friday close execution)
					OriginId = GenerateOriginId(ticker, dir)
				});
			}

			Console.WriteLine($"[FlexOneAdapter] Built {result.Count} orders from {proposedOrders.Count} proposed trades.");
			return result;
		}

		private static string GenerateOriginId(string ticker, int dir)
		{
			var stamp = DateTime.UtcNow.ToString("yyyyMMddHHmm");
			return $"ATMML-{ticker}-{dir}-{stamp}";
		}
	}
}