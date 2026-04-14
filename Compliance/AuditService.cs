using System;
using System.Collections.Generic;
using System.Linq;

namespace ATMML.Compliance
{
	public static class AuditService
	{
		private static string _currentUserId = "SYSTEM";
		private static string _currentUsername = "SYSTEM";
		private static string _currentRole = "SYSTEM";

		// Set by BetaNeutralRiskEngine after each Solve() — read by LogDailyRisk
		public static object LastRiskGovernorResult { get; set; }
		private static string _lastPdfKey = "";

		// Call this on login
		public static void SetSession(string userId, string username, string role)
		{
			_currentUserId = userId;
			_currentUsername = username;
			_currentRole = role;
		}

		public static void LogLogin(string username)
		{
			ComplianceDb.WriteAudit(new AuditRecord
			{
				UserId = _currentUserId,
				Username = username,
				UserRole = _currentRole,
				Action = "LOGIN",
				ObjectType = "Session",
				Result = "Success"
			});
		}

		public static void LogRebalanceRequest(string portfolioId)
		{
			ComplianceDb.WriteAudit(new AuditRecord
			{
				UserId = _currentUserId,
				Username = _currentUsername,
				UserRole = _currentRole,
				Action = "REBALANCE_REQUEST",
				ObjectType = "Portfolio",
				ObjectId = portfolioId
			});
		}

		public static void LogTradeSubmitted(string portfolioId, string ticker,
			string side, decimal shares, decimal? price, string orderState,
			string modelVersion = null, bool isOverride = false)
		{
			ComplianceDb.WriteTradeBlotter(new TradeBlotterRecord
			{
				PortfolioId = portfolioId,
				Ticker = ticker,
				Side = side,
				Shares = shares,
				Price = price,
				OrderState = orderState,
				ModelVersion = modelVersion,
				IsOverride = isOverride,
				TraderId = _currentUserId
			});
		}

		public static void LogConstraintBreach(string portfolioId,
			string description, string severity = "Warning")
		{
			ComplianceDb.WriteException(new ExceptionRecord
			{
				PortfolioId = portfolioId,
				ExceptionType = "ConstraintBreach",
				Severity = severity,
				Description = description
			});
		}

		public static void LogModelOverride(string portfolioId,
			string description)
		{
			ComplianceDb.WriteAudit(new AuditRecord
			{
				UserId = _currentUserId,
				Username = _currentUsername,
				UserRole = _currentRole,
				Action = "MANUAL_OVERRIDE",
				ObjectType = "Portfolio",
				ObjectId = portfolioId,
				AfterValue = description
			});

			ComplianceDb.WriteException(new ExceptionRecord
			{
				PortfolioId = portfolioId,
				ExceptionType = "ManualOverride",
				Severity = "High",
				Description = description
			});
		}

		public static void LogAction(string action, string objectType = null,
			string objectId = null, string before = null, string after = null)
		{
			ComplianceDb.WriteAudit(new AuditRecord
			{
				UserId = _currentUserId,
				Username = _currentUsername,
				UserRole = _currentRole,
				Action = action,
				ObjectType = objectType,
				ObjectId = objectId,
				BeforeValue = before,
				AfterValue = after
			});
		}

		public static void LogDailyRisk(
			string portfolioId,
			bool isLive,
			double nav,
			double longExposure,
			double shortExposure,
			double netExposure,
			double grossExposure,
			int longCount,
			int shortCount,
			Dictionary<string, string> sectorPercents,
			string modelVersion)
		{
			var record = new DailyRiskRecord
			{
				PortfolioId = portfolioId,
				RunType = isLive ? "Live" : "Backtest",
				NAV = (decimal)nav,
				LongExposurePct = (decimal)longExposure,
				ShortExposurePct = (decimal)shortExposure,
				NetExposurePct = (decimal)netExposure,
				GrossExposurePct = (decimal)grossExposure,
				LongCount = longCount,
				ShortCount = shortCount,
				ModelVersion = modelVersion
			};

			// Parse top 3 sectors from sectorPercents dictionary
			if (sectorPercents != null)
			{
				var parsed = sectorPercents
					.Select(kv => {
						double v = 0;
						double.TryParse(kv.Value.TrimEnd('%'), out v);
						return (Name: kv.Key, Pct: v / 100.0);
					})
					.Where(x => x.Pct > 0)
					.OrderByDescending(x => x.Pct)
					.ToList();

				if (parsed.Count > 0) { record.TopSector1Name = parsed[0].Name; record.TopSector1Pct = (decimal)parsed[0].Pct; }
				if (parsed.Count > 1) { record.TopSector2Name = parsed[1].Name; record.TopSector2Pct = (decimal)parsed[1].Pct; }
				if (parsed.Count > 2) { record.TopSector3Name = parsed[2].Name; record.TopSector3Pct = (decimal)parsed[2].Pct; }
			}

			// Pull risk governor state if available
			if (LastRiskGovernorResult is BetaNeutralRiskEngine.RiskGovernorResult rg)
			{
				record.VixScale = (decimal)rg.VixScale;
				record.VolatilityScale = (decimal)rg.VolatilityScale;
				record.FinalRiskScale = (decimal)rg.FinalScale;
				record.CircuitBreakerState = rg.NewCircuitBreakerState.ToString();
				record.CircuitBreakerActive = rg.CircuitBreakerActive;
				record.ActiveRiskFactors = rg.ActiveFactors;
			}

			ComplianceDb.WriteRiskReport(record);
			var pdfKey = $"{record.PortfolioId}_{record.ReportDate:yyyyMMdd}_{record.RunType}";
			if (_lastPdfKey != pdfKey)
			{
				_lastPdfKey = pdfKey;
				RiskReportPdf.Generate(record);
			}
		}
	}
}