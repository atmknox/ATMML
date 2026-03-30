using System;

namespace ATMML
{
	/// <summary>
	/// Configuration for the FlexOne OMS gRPC connection.
	/// Populate from your app.config or appsettings before constructing FlexOneSession.
	/// </summary>
	public class FlexOneConfig
	{
		// ── Connection ────────────────────────────────────────────────────────────
		public string Host { get; set; } = "localhost";
		public int Port { get; set; } = 8080;

		// ── Credentials ───────────────────────────────────────────────────────────
		public string User { get; set; } = "PFA";
		public string Password { get; set; } = "";

		// ── Order defaults ────────────────────────────────────────────────────────
		/// <summary>Trader / owner field on every order (usually same as User).</summary>
		public string Trader { get; set; } = "PFA";

		/// <summary>Default broker sent on every order (e.g. "CITI", "MLCO").</summary>
		public string DefaultBroker { get; set; } = "CITI";

		/// <summary>Trading & settlement currency.</summary>
		public string Currency { get; set; } = "USD";

		/// <summary>PositionGroup name for the long book.</summary>
		public string LongPositionGroup { get; set; } = "Equity-Long";

		/// <summary>PositionGroup name for the short book.</summary>
		public string ShortPositionGroup { get; set; } = "Equity-Short";

		/// <summary>
		/// When true, orders are created + immediately sent to EMS in one shot
		/// (CreateOrdersRequest.SendToEms = true).
		/// When false, orders are staged first; caller must invoke SendToEms separately.
		/// </summary>
		public bool SendToEmsOnCreate { get; set; } = true;

		// ── gRPC channel tuning ───────────────────────────────────────────────────
		public int MaxMessageLengthBytes { get; set; } = 10 * 1024 * 1024;
		public int KeepaliveTimeMs { get; set; } = 10_000;
		public int KeepaliveTimeoutMs { get; set; } = 10_000;
	}
}