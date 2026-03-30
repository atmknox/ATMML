using System;
using System.Collections.Generic;
using Grpc.Core.Utils;

namespace ATMML
{
	/// <summary>
	/// Manages the gRPC channel, authentication token, and exposes a ready-to-use
	/// OrderServiceClient for the lifetime of a rebalance run.
	///
	/// Usage:
	///   using (var session = new FlexOneSession(config))
	///   {
	///       session.Connect();
	///       var bridge = new FlexOneOrderBridge(session, config);
	///       bridge.SubmitRebalance(trades);
	///   }
	/// </summary>
	public class FlexOneSession : IDisposable
	{
		// ── Fields ────────────────────────────────────────────────────────────────
		private readonly FlexOneConfig _cfg;
		private Grpc.Core.Channel _channel;
		private string _token;
		private bool _disposed;

		// ── Properties ────────────────────────────────────────────────────────────
		public bool IsConnected => _channel?.State == Grpc.Core.ChannelState.Ready ||
									 _channel?.State == Grpc.Core.ChannelState.Idle;
		public string Token => _token;

		// ── Constructor ───────────────────────────────────────────────────────────
		public FlexOneSession(FlexOneConfig config)
		{
			_cfg = config ?? throw new ArgumentNullException(nameof(config));
		}

		// ── Public API ────────────────────────────────────────────────────────────

		/// <summary>Opens the gRPC channel and authenticates, obtaining a session token.</summary>
		public void Connect()
		{
			_channel = BuildChannel();
			_token = Authenticate(_channel);
			Console.WriteLine($"[FlexOne] Session established. Token: {_token?.Substring(0, Math.Min(8, _token?.Length ?? 0))}...");
		}

		/// <summary>Returns a ready-to-use Ft.OrderServiceClient for one operation batch.</summary>
		public Ft.OrderServiceClient CreateOrderClient()
		{
			EnsureConnected();
			return new Ft.OrderServiceClient(_cfg.Host, _cfg.Port);
		}

		// ── Private helpers ───────────────────────────────────────────────────────

		private Grpc.Core.Channel BuildChannel()
		{
			var options = new List<Grpc.Core.ChannelOption>
			{
				new Grpc.Core.ChannelOption("grpc.max_message_length",             _cfg.MaxMessageLengthBytes),
				new Grpc.Core.ChannelOption("grpc.keepalive_time_ms",              _cfg.KeepaliveTimeMs),
				new Grpc.Core.ChannelOption("grpc.keepalive_timeout_ms",           _cfg.KeepaliveTimeoutMs),
				new Grpc.Core.ChannelOption("grpc.http2.max_pings_without_data",   0),
				new Grpc.Core.ChannelOption("grpc.keepalive_permit_without_calls", 1)
			};
			return new Grpc.Core.Channel(
				$"{_cfg.Host}:{_cfg.Port}",
				Grpc.Core.ChannelCredentials.Insecure,
				options);
		}

		private string Authenticate(Grpc.Core.Channel channel)
		{
			var stub = new Ft.AuthenticationService.AuthenticationServiceClient(channel);
			var request = new Ft.AuthenticationRequest { User = _cfg.User, Password = _cfg.Password };

			var call = stub.Authenticate(request);
			var results = call.ResponseStream.ToListAsync().Result;

			if (results.Count == 0)
				throw new InvalidOperationException("[FlexOne] Authentication returned no response.");

			var response = results[0];
			if (response.Status != Ft.AuthenticationStatusCode.AuthenticationSuccess)
				throw new InvalidOperationException($"[FlexOne] Authentication failed: {response.Status}");

			return response.Token;
		}

		private void EnsureConnected()
		{
			if (_channel == null)
				throw new InvalidOperationException("[FlexOne] Session not connected. Call Connect() first.");
		}

		// ── IDisposable ───────────────────────────────────────────────────────────

		public void Dispose()
		{
			if (_disposed) return;
			_disposed = true;
			try { _channel?.ShutdownAsync().Wait(TimeSpan.FromSeconds(5)); }
			catch (Exception ex) { Console.Error.WriteLine($"[FlexOne] Channel shutdown error: {ex.Message}"); }
		}
	}
}