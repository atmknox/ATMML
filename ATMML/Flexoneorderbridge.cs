using System;
using System.Collections.Generic;
using System.Linq;

namespace ATMML
{
	// ─────────────────────────────────────────────────────────────────────────────
	// Trade descriptor — maps your rebalance output to FlexOne order fields.
	// Adjust property names if your internal Trade class differs.
	// ─────────────────────────────────────────────────────────────────────────────

	public enum FlexOneTradeAction
	{
		NewLong,     // Open new long position  → Buy
		NewShort,    // Open new short position → Short
		AddLong,     // Add to existing long    → Buy
		ReduceLong,  // Partial close of long   → Sell
		CloseLong,   // Full close of long      → Sell
		CoverShort,  // Partial/full cover short→ Cover
	}

	public class FlexOneTrade
	{
		public string Ticker { get; set; }
		public FlexOneTradeAction Action { get; set; }
		public int Shares { get; set; }   // always positive
		public double LimitPrice { get; set; }   // 0 = MOC market order
		public string OriginId { get; set; }   // your campaign/order ID
		public string BrokerOverride { get; set; }   // null = use config default
	}

	// ─────────────────────────────────────────────────────────────────────────────
	// Result types
	// ─────────────────────────────────────────────────────────────────────────────

	public class FlexOneRebalanceResult
	{
		public bool Success { get; set; }
		public int OrdersPlaced { get; set; }
		public int OrdersFailed { get; set; }
		public string Description { get; set; }
		public List<FlexOneOrderResult> Details { get; } = new List<FlexOneOrderResult>();
	}

	public class FlexOneOrderResult
	{
		public string Ticker { get; set; }
		public string OrderId { get; set; }
		public bool Success { get; set; }
		public string Message { get; set; }
	}

	// ─────────────────────────────────────────────────────────────────────────────
	// Main bridge
	// ─────────────────────────────────────────────────────────────────────────────

	/// <summary>
	/// Translates ATMML OEX V2 rebalance output into FlexOne OMS orders and submits
	/// them via gRPC.  Designed for Friday MOC execution:
	///   Ft.OrderType.Market + Ft.TimeInForce.Close
	/// </summary>
	public class FlexOneOrderBridge : IFlexOneOrderBridge
	{
		private readonly FlexOneSession _session;
		private readonly FlexOneConfig _cfg;

		public FlexOneOrderBridge(FlexOneSession session, FlexOneConfig config)
		{
			_session = session ?? throw new ArgumentNullException(nameof(session));
			_cfg = config ?? throw new ArgumentNullException(nameof(config));
		}

		// ── Public API ────────────────────────────────────────────────────────────

		/// <summary>
		/// Submits the full rebalance trade list to FlexOne in a single batch.
		/// All orders use MOC execution (Ft.OrderType.Market + Ft.TimeInForce.Close).
		/// </summary>
		public FlexOneRebalanceResult SubmitRebalance(IEnumerable<FlexOneTrade> trades)
		{
			var tradeList = trades?.ToList()
				?? throw new ArgumentNullException(nameof(trades));

			Console.WriteLine($"[FlexOne] Submitting {tradeList.Count} rebalance orders...");

			var result = new FlexOneRebalanceResult();

			try
			{
				using (var client = _session.CreateOrderClient())
				{
					var request = BuildCreateOrdersRequest(tradeList);
					var response = client.CreateOrders(request);

					if (response == null)
						throw new InvalidOperationException("[FlexOne] Null response from CreateOrders.");

					ProcessCreateResponse(response, tradeList, result);
				}
			}
			catch (Exception ex)
			{
				result.Success = false;
				result.Description = $"Batch submission error: {ex.Message}";
				Console.Error.WriteLine($"[FlexOne] {result.Description}");
			}

			Console.WriteLine($"[FlexOne] Rebalance complete. " +
							  $"Placed: {result.OrdersPlaced}  Failed: {result.OrdersFailed}");
			return result;
		}

		/// <summary>Cancels a list of FlexOne order IDs.</summary>
		public bool CancelOrders(IEnumerable<string> orderIds)
		{
			var ids = orderIds?.ToList()
				?? throw new ArgumentNullException(nameof(orderIds));

			Console.WriteLine($"[FlexOne] Cancelling {ids.Count} orders...");

			using (var client = _session.CreateOrderClient())
			{
				var request = new Ft.CancelOrdersRequest { User = _cfg.User };
				request.OrderIds.AddRange(ids);

				var response = client.CancelOrders(request);
				if (response == null)
				{
					Console.Error.WriteLine("[FlexOne] Null response from CancelOrders.");
					return false;
				}

				Console.WriteLine($"[FlexOne] Cancel status: {response.Status.Success} — {response.Status.Description}");
				return response.Status.Success;
			}
		}

		/// <summary>
		/// Fetches today's trade activity from FlexOne (useful for reconciliation).
		/// </summary>
		public IEnumerable<Ft.OrderUpdateResponse> GetTradeActivity(DateTime tradeDate)
		{
			using (var client = _session.CreateOrderClient())
			{
				var dateStr = tradeDate.ToString("yyyy-MM-dd");
				Console.WriteLine($"[FlexOne] Fetching trade activity for {dateStr}...");
				return client.ListTradeActivity(dateStr);
			}
		}

		/// <summary>
		/// Replays all orders from OMS (useful for verifying state on startup).
		/// </summary>
		public IEnumerable<Ft.OrderUpdateResponse> ReplayOrders(int sinceSequenceId = 0)
		{
			using (var client = _session.CreateOrderClient())
				return client.ReplayOrders(sinceSequenceId);
		}

		// ── Order construction ────────────────────────────────────────────────────

		private Ft.CreateOrdersRequest BuildCreateOrdersRequest(List<FlexOneTrade> trades)
		{
			var request = new Ft.CreateOrdersRequest
			{
				User = _cfg.User,
				SendToEms = _cfg.SendToEmsOnCreate,
				ComplianceInputs = new Ft.ComplianceInputs()   // empty = match all rule sets
			};

			foreach (var trade in trades)
			{
				try
				{
					request.Orders.Add(BuildOrder(trade));
				}
				catch (Exception ex)
				{
					Console.Error.WriteLine(
						$"[FlexOne] Skipping {trade.Ticker}: could not build order — {ex.Message}");
				}
			}

			return request;
		}

		private Ft.Order BuildOrder(FlexOneTrade trade)
		{
			if (string.IsNullOrWhiteSpace(trade.Ticker))
				throw new ArgumentException("Ticker is required.");
			if (trade.Shares <= 0)
				throw new ArgumentException($"{trade.Ticker}: Shares must be > 0 (got {trade.Shares}).");

			var (side, posGroup) = MapSideAndPositionGroup(trade.Action);
			var (orderType, price) = MapOrderTypeAndPrice(trade.LimitPrice);

			return new Ft.Order
			{
				OriginId = trade.OriginId ?? GenerateOriginId(trade.Ticker),
				User = _cfg.User,
				Trader = _cfg.Trader,
				Symbol = trade.Ticker,
				Side = side,
				OrderType = orderType,
				Price = price,
				Quantity = trade.Shares,
				PositionGroup = posGroup,
				TimeInForce = Ft.TimeInForce.Close,     // MOC — at Close
				TradingCurrency = _cfg.Currency,
				SettlementCurrency = _cfg.Currency,
				Broker = trade.BrokerOverride ?? _cfg.DefaultBroker,
				AutoRoute = true,
				Notes = $"OEX V2 rebalance — {trade.Action}",
				SecurityDetails = new Ft.OrderSecurityDetails
				{
					SecurityType = "Equity",
					BloombergSymbol = trade.Ticker
				}
			};
		}

		// ── Mapping helpers ───────────────────────────────────────────────────────

		private (Ft.MarketSide side, string posGroup) MapSideAndPositionGroup(FlexOneTradeAction action)
		{
			return action switch
			{
				FlexOneTradeAction.NewLong => (Ft.MarketSide.Buy, _cfg.LongPositionGroup),
				FlexOneTradeAction.AddLong => (Ft.MarketSide.Buy, _cfg.LongPositionGroup),
				FlexOneTradeAction.ReduceLong => (Ft.MarketSide.Sell, _cfg.LongPositionGroup),
				FlexOneTradeAction.CloseLong => (Ft.MarketSide.Sell, _cfg.LongPositionGroup),
				FlexOneTradeAction.NewShort => (Ft.MarketSide.Short, _cfg.ShortPositionGroup),
				FlexOneTradeAction.CoverShort => (Ft.MarketSide.Cover, _cfg.ShortPositionGroup),
				_ => throw new ArgumentOutOfRangeException(nameof(action), $"Unknown action: {action}")
			};
		}

		private (Ft.OrderType orderType, double price) MapOrderTypeAndPrice(double limitPrice)
		{
			// Price > 0 → Limit order (for staged/pre-compliance workflows)
			// Price = 0 → Market at Close = MOC
			if (limitPrice > 0)
				return (Ft.OrderType.Limit, limitPrice);

			return (Ft.OrderType.Market, 0.0);
		}

		private static string GenerateOriginId(string ticker)
		{
			// Stays within FlexOne's OriginId field limit
			var stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
			var guid = Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper();
			return $"ATMML-{ticker}-{stamp}-{guid}";
		}

		// ── Response processing ───────────────────────────────────────────────────

		private void ProcessCreateResponse(
			Ft.CreateOrdersResponse response,
			List<FlexOneTrade> trades,
			FlexOneRebalanceResult result)
		{
			result.Success = response.Status.Success;
			result.Description = response.Status.Description ?? string.Empty;

			var resultList = response.Results.ToList();

			for (int i = 0; i < resultList.Count; i++)
			{
				var r = resultList[i];
				var ticker = i < trades.Count ? trades[i].Ticker : $"Order[{i}]";
				var ok = !string.IsNullOrEmpty(r.OrderId);

				result.Details.Add(new FlexOneOrderResult
				{
					Ticker = ticker,
					OrderId = r.OrderId,
					Success = ok,
					Message = ok
						? $"OrderId={r.OrderId}"
						: $"No OrderId returned; compliance issues: {r.ComplianceIssues.Count}"
				});

				if (ok) result.OrdersPlaced++;
				else
				{
					result.OrdersFailed++;
					Console.Error.WriteLine(
						$"[FlexOne] {ticker} — order not confirmed. " +
						$"Compliance issues: {r.ComplianceIssues.Count}");
				}
			}

			// Warn if FlexOne returned fewer results than orders submitted
			if (resultList.Count < trades.Count)
			{
				int missing = trades.Count - resultList.Count;
				result.OrdersFailed += missing;
				Console.Error.WriteLine($"[FlexOne] WARNING: {missing} orders have no result in response.");
			}
		}
	}
}