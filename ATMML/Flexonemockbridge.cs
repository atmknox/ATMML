using System;
using System.Collections.Generic;
using System.Linq;

namespace ATMML
{
	/// <summary>
	/// Drop-in replacement for FlexOneOrderBridge used during development and testing
	/// when no live FlexOne connection is available.
	///
	/// Implements IFlexOneOrderBridge identically to the real bridge so all calling
	/// code is exercised without any gRPC/network dependency.
	///
	/// Usage:
	///   IFlexOneOrderBridge bridge = AppConfig.UseMockFlexOne
	///       ? new FlexOneMockBridge()
	///       : new FlexOneOrderBridge(session, config);
	///
	/// After SubmitRebalance(), inspect SubmittedTrades to assert what was sent.
	/// Set SimulateFailure = true to test your error-handling paths.
	/// </summary>
	public class FlexOneMockBridge : IFlexOneOrderBridge
	{
		// ── Test control ──────────────────────────────────────────────────────────

		/// <summary>
		/// When true, all operations return failure responses.
		/// Use to verify your UI / logging handles submission failures correctly.
		/// </summary>
		public bool SimulateFailure { get; set; } = false;

		/// <summary>
		/// When set, SubmitRebalance throws this exception.
		/// Use to verify your exception-handling path.
		/// </summary>
		public Exception SimulateException { get; set; } = null;

		/// <summary>
		/// Simulates a partial failure: any ticker in this list will fail.
		/// All others will succeed regardless of SimulateFailure.
		/// </summary>
		public HashSet<string> FailTickers { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		// ── Inspection ────────────────────────────────────────────────────────────

		/// <summary>All trades received by SubmitRebalance, in submission order.</summary>
		public List<FlexOneTrade> SubmittedTrades { get; } = new List<FlexOneTrade>();

		/// <summary>All order IDs passed to CancelOrders.</summary>
		public List<string> CancelledOrderIds { get; } = new List<string>();

		/// <summary>All results returned by SubmitRebalance, in submission order.</summary>
		public List<FlexOneOrderResult> LastResults { get; } = new List<FlexOneOrderResult>();

		/// <summary>Number of times SubmitRebalance has been called.</summary>
		public int SubmitCallCount { get; private set; }

		// ── IFlexOneOrderBridge ───────────────────────────────────────────────────

		public FlexOneRebalanceResult SubmitRebalance(IEnumerable<FlexOneTrade> trades)
		{
			if (SimulateException != null)
				throw SimulateException;

			var tradeList = trades?.ToList()
				?? throw new ArgumentNullException(nameof(trades));

			SubmitCallCount++;
			SubmittedTrades.AddRange(tradeList);
			LastResults.Clear();

			Console.WriteLine($"[FlexOneMock] SubmitRebalance called — {tradeList.Count} trades.");

			var result = new FlexOneRebalanceResult();

			foreach (var trade in tradeList)
			{
				bool ok = !SimulateFailure && !FailTickers.Contains(trade.Ticker);

				var orderResult = new FlexOneOrderResult
				{
					Ticker = trade.Ticker,
					OrderId = ok ? GenerateMockOrderId(trade.Ticker) : null,
					Success = ok,
					Message = ok
						? $"[Mock] Accepted — {trade.Action} {trade.Shares} {trade.Ticker}"
						: $"[Mock] Rejected — SimulateFailure={SimulateFailure} FailTicker={FailTickers.Contains(trade.Ticker)}"
				};

				result.Details.Add(orderResult);
				LastResults.Add(orderResult);

				if (ok)
				{
					result.OrdersPlaced++;
					Console.WriteLine($"[FlexOneMock]   ✓ {trade.Action,-12} {trade.Shares,6} {trade.Ticker,-6}  OrderId={orderResult.OrderId}");
				}
				else
				{
					result.OrdersFailed++;
					Console.WriteLine($"[FlexOneMock]   ✗ {trade.Action,-12} {trade.Shares,6} {trade.Ticker,-6}  FAILED");
				}
			}

			result.Success = result.OrdersFailed == 0;
			result.Description = result.Success
				? $"[Mock] All {result.OrdersPlaced} orders accepted"
				: $"[Mock] {result.OrdersFailed} of {tradeList.Count} orders failed";

			Console.WriteLine($"[FlexOneMock] Result: {result.Description}");
			return result;
		}

		public bool CancelOrders(IEnumerable<string> orderIds)
		{
			var ids = orderIds?.ToList()
				?? throw new ArgumentNullException(nameof(orderIds));

			CancelledOrderIds.AddRange(ids);
			Console.WriteLine($"[FlexOneMock] CancelOrders: {string.Join(", ", ids)}");

			return !SimulateFailure;
		}

		public IEnumerable<Ft.OrderUpdateResponse> GetTradeActivity(DateTime tradeDate)
		{
			Console.WriteLine($"[FlexOneMock] GetTradeActivity: {tradeDate:yyyy-MM-dd} — returning empty list");
			return Enumerable.Empty<Ft.OrderUpdateResponse>();
		}

		public IEnumerable<Ft.OrderUpdateResponse> ReplayOrders(int sinceSequenceId = 0)
		{
			Console.WriteLine($"[FlexOneMock] ReplayOrders(sinceSeqId={sinceSequenceId}) — returning empty list");
			return Enumerable.Empty<Ft.OrderUpdateResponse>();
		}

		// ── Helpers ───────────────────────────────────────────────────────────────

		/// <summary>Clears all recorded state between test runs.</summary>
		public void Reset()
		{
			SubmittedTrades.Clear();
			CancelledOrderIds.Clear();
			LastResults.Clear();
			SubmitCallCount = 0;
			FailTickers.Clear();
			SimulateFailure = false;
			SimulateException = null;
		}

		private static string GenerateMockOrderId(string ticker)
		{
			// Mimics the format FlexOne returns — short alphanumeric string
			var guid = Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper();
			return $"MOCK-{ticker}-{guid}";
		}
	}
}