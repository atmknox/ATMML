using System;

namespace ATMML
{
    /// <summary>
    /// Centralized helper that converts a FlexOneTrade into a WorkingOrder. Used both
    /// for staging (pre-submission, status = Staged) and post-submission (status = New).
    /// </summary>
    internal static class WorkingOrderFactory
    {
        public static OrderSide ToOrderSide(FlexOneTradeAction a)
        {
            switch (a)
            {
                case FlexOneTradeAction.NewLong:    return OrderSide.Buy;
                case FlexOneTradeAction.AddLong:    return OrderSide.Buy;
                case FlexOneTradeAction.ReduceLong: return OrderSide.Sell;
                case FlexOneTradeAction.CloseLong:  return OrderSide.Sell;
                case FlexOneTradeAction.NewShort:   return OrderSide.SellShort;
                case FlexOneTradeAction.CoverShort: return OrderSide.BuyToCover;
                default:
                    throw new ArgumentOutOfRangeException(nameof(a), a, "Unknown FlexOneTradeAction");
            }
        }

        /// <summary>
        /// Build a Staged WorkingOrder from a trade. No OrderId yet (assigned at submit
        /// time by the OMS). ClOrdId is generated locally so the row can be referenced
        /// in the blotter for cancel/modify before submission.
        /// </summary>
        public static WorkingOrder StageFromTrade(FlexOneTrade trade, string strategyName,
                                                   string clOrdId)
        {
            if (trade == null) throw new ArgumentNullException(nameof(trade));

            bool isLimit = trade.LimitPrice > 0.0;
            return new WorkingOrder
            {
                ClOrdId     = clOrdId,
                OrderId     = null,
                Symbol      = trade.Ticker,
                Side        = ToOrderSide(trade.Action),
                Type        = isLimit ? OrderType.Limit : OrderType.MarketOnClose,
                Qty         = trade.Shares,
                LeavesQty   = trade.Shares,
                FilledQty   = 0,
                LimitPrice  = isLimit ? (decimal?)Convert.ToDecimal(trade.LimitPrice) : null,
                AvgPx       = null,
                Status      = nameof(OrderStatus.Staged),
                Strategy    = strategyName ?? "(unspecified)",
                Destination = "FlexOne",
                SubmittedAt = DateTime.Now,
                LastMessage = $"Staged: {trade.Action} {trade.Shares} {trade.Ticker}",
                SourceTrade = trade
            };
        }

        /// <summary>
        /// Build a New WorkingOrder directly from a submitted trade and its result.
        /// Used as a fallback when staging was bypassed (e.g. legacy submission path).
        /// </summary>
        public static WorkingOrder Build(FlexOneTrade trade, FlexOneOrderResult result,
                                          string strategyName)
        {
            if (trade == null) throw new ArgumentNullException(nameof(trade));
            if (result == null) throw new ArgumentNullException(nameof(result));

            bool isLimit = trade.LimitPrice > 0.0;
            return new WorkingOrder
            {
                ClOrdId     = result.OrderId ?? Guid.NewGuid().ToString("N").Substring(0, 12),
                OrderId     = result.OrderId,
                Symbol      = trade.Ticker,
                Side        = ToOrderSide(trade.Action),
                Type        = isLimit ? OrderType.Limit : OrderType.MarketOnClose,
                Qty         = trade.Shares,
                LeavesQty   = trade.Shares,
                FilledQty   = 0,
                LimitPrice  = isLimit ? (decimal?)Convert.ToDecimal(trade.LimitPrice) : null,
                AvgPx       = null,
                Status      = nameof(OrderStatus.New),
                Strategy    = strategyName ?? "(unspecified)",
                Destination = "FlexOne",
                SubmittedAt = DateTime.Now,
                LastMessage = result.Message,
                SourceTrade = trade
            };
        }
    }
}
