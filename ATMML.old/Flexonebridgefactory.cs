using System;

namespace ATMML
{
	/// <summary>
	/// Creates the appropriate IFlexOneOrderBridge implementation based on config.
	///
	/// In your WPF app startup or rebalance execution path:
	///
	///   var bridge = FlexOneBridgeFactory.Create(flexOneConfig, useMock: AppConfig.UseMockFlexOne);
	///   var result = bridge.SubmitRebalance(trades);
	///
	/// When useMock=false the factory also manages the FlexOneSession lifetime —
	/// dispose the bridge when done (it wraps the session).
	/// </summary>
	public static class FlexOneBridgeFactory
	{
		/// <summary>
		/// Creates a live or mock bridge depending on the useMock flag.
		/// </summary>
		/// <param name="config">FlexOne connection config. Only used when useMock=false.</param>
		/// <param name="useMock">True during development/testing; false for live execution.</param>
		public static IFlexOneOrderBridge Create(FlexOneConfig config, bool useMock)
		{
			if (useMock)
			{
				Console.WriteLine("[FlexOne] Using MOCK bridge — no orders will be sent to OMS.");
				return new FlexOneMockBridge();
			}

			Console.WriteLine($"[FlexOne] Using LIVE bridge — connecting to {config.Host}:{config.Port}");
			var session = new FlexOneSession(config);
			session.Connect();
			return new FlexOneOrderBridge(session, config);
		}

		/// <summary>
		/// Creates a mock bridge pre-configured to fail specific tickers.
		/// Useful for testing partial failure handling.
		/// </summary>
		public static FlexOneMockBridge CreateMock(bool simulateFailure = false, params string[] failTickers)
		{
			var mock = new FlexOneMockBridge { SimulateFailure = simulateFailure };
			foreach (var ticker in failTickers)
				mock.FailTickers.Add(ticker);
			return mock;
		}
	}
}