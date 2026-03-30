using System;
using System.Collections.Generic;

namespace ATMML
{
	/// <summary>
	/// Abstraction over the FlexOne OMS order submission layer.
	/// Swap between FlexOneOrderBridge (live) and FlexOneMockBridge (testing)
	/// via AppConfig.UseMockFlexOne without changing any calling code.
	/// </summary>
	public interface IFlexOneOrderBridge
	{
		/// <summary>
		/// Submits the full rebalance trade list as a single batch.
		/// </summary>
		FlexOneRebalanceResult SubmitRebalance(IEnumerable<FlexOneTrade> trades);

		/// <summary>
		/// Cancels a list of previously submitted FlexOne order IDs.
		/// </summary>
		bool CancelOrders(IEnumerable<string> orderIds);

		/// <summary>
		/// Returns all trade activity for the given date from the OMS.
		/// TradeDate is used for end-of-day reconciliation.
		/// </summary>
		IEnumerable<Ft.OrderUpdateResponse> GetTradeActivity(DateTime tradeDate);

		/// <summary>
		/// Replays all orders from the OMS since a given sequence ID.
		/// Useful for verifying OMS state on startup.
		/// </summary>
		IEnumerable<Ft.OrderUpdateResponse> ReplayOrders(int sinceSequenceId = 0);
	}
}