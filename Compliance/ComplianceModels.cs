using System;

namespace ATMML.Compliance
{
	public class AuditRecord
	{
		public Guid AuditId { get; set; } = Guid.NewGuid();
		public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
		public string UserId { get; set; }
		public string Username { get; set; }
		public string UserRole { get; set; }
		public string MachineName { get; set; } = Environment.MachineName;
		public string Action { get; set; }
		public string ObjectType { get; set; }
		public string ObjectId { get; set; }
		public string BeforeValue { get; set; }
		public string AfterValue { get; set; }
		public string Result { get; set; } = "Success";
		public Guid CorrelationId { get; set; } = Guid.NewGuid();
	}

	public class TradeBlotterRecord
	{
		public Guid BlotterId { get; set; } = Guid.NewGuid();
		public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
		public string PortfolioId { get; set; }
		public string Ticker { get; set; }
		public string Side { get; set; }
		public decimal Shares { get; set; }
		public decimal? Price { get; set; }
		public string OrderState { get; set; }
		public string ExecutionVenue { get; set; }
		public string ModelVersion { get; set; }
		public string SignalId { get; set; }
		public bool IsOverride { get; set; } = false;
		public string TraderId { get; set; }
		public Guid CorrelationId { get; set; } = Guid.NewGuid();
	}

	public class ExceptionRecord
	{
		public Guid ExceptionId { get; set; } = Guid.NewGuid();
		public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
		public string PortfolioId { get; set; }
		public string ExceptionType { get; set; }
		public string Severity { get; set; } = "Warning";
		public string Description { get; set; }
		public string ResolutionNote { get; set; }
		public string ResolvedBy { get; set; }
		public Guid CorrelationId { get; set; } = Guid.NewGuid();
	}
	public class DailyRiskRecord
	{
		public Guid ReportId { get; set; } = Guid.NewGuid();
		public DateTime ReportDate { get; set; } = DateTime.UtcNow.Date;
		public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
		public string PortfolioId { get; set; }
		public string RunType { get; set; } = "Backtest";   // "Backtest" or "Live"
		public decimal? NAV { get; set; }
		public decimal? LongExposurePct { get; set; }
		public decimal? ShortExposurePct { get; set; }
		public decimal? NetExposurePct { get; set; }
		public decimal? GrossExposurePct { get; set; }
		public int? LongCount { get; set; }
		public int? ShortCount { get; set; }
		public string TopSector1Name { get; set; }
		public decimal? TopSector1Pct { get; set; }
		public string TopSector2Name { get; set; }
		public decimal? TopSector2Pct { get; set; }
		public string TopSector3Name { get; set; }
		public decimal? TopSector3Pct { get; set; }
		public decimal? VixScale { get; set; }
		public decimal? VolatilityScale { get; set; }
		public decimal? FinalRiskScale { get; set; }
		public string CircuitBreakerState { get; set; }
		public bool? CircuitBreakerActive { get; set; }
		public string ActiveRiskFactors { get; set; }
		public string ModelVersion { get; set; }
	}
}
