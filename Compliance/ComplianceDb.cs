using System;
using System;
using System.Data.SqlClient;
using System.Security.Cryptography;
using System.Text;

namespace ATMML.Compliance
{
	public static class ComplianceDb
	{
		private const string ConnStr =
			@"Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=ComplianceDb;Integrated Security=True;";

		// ── Audit Log ──────────────────────────────────────────────────────
		public static void WriteAudit(AuditRecord r)
		{
			try
			{
				string hash = ComputeHash(
					r.TimestampUtc + r.UserId + r.Username + r.Action +
					r.ObjectType + r.ObjectId + r.Result);

				using (var conn = new SqlConnection(ConnStr))
				using (var cmd = new SqlCommand(@"
                    INSERT INTO AuditLog
                    (AuditId, TimestampUtc, UserId, Username, UserRole,
                     MachineName, Action, ObjectType, ObjectId,
                     BeforeValue, AfterValue, Result, CorrelationId, RowHash)
                    VALUES
                    (@AuditId, @TimestampUtc, @UserId, @Username, @UserRole,
                     @MachineName, @Action, @ObjectType, @ObjectId,
                     @BeforeValue, @AfterValue, @Result, @CorrelationId, @RowHash)",
					conn))
				{
					cmd.Parameters.AddWithValue("@AuditId", r.AuditId);
					cmd.Parameters.AddWithValue("@TimestampUtc", r.TimestampUtc);
					cmd.Parameters.AddWithValue("@UserId", r.UserId ?? "");
					cmd.Parameters.AddWithValue("@Username", r.Username ?? "");
					cmd.Parameters.AddWithValue("@UserRole", r.UserRole ?? "");
					cmd.Parameters.AddWithValue("@MachineName", r.MachineName ?? "");
					cmd.Parameters.AddWithValue("@Action", r.Action ?? "");
					cmd.Parameters.AddWithValue("@ObjectType", (object)r.ObjectType ?? DBNull.Value);
					cmd.Parameters.AddWithValue("@ObjectId", (object)r.ObjectId ?? DBNull.Value);
					cmd.Parameters.AddWithValue("@BeforeValue", (object)r.BeforeValue ?? DBNull.Value);
					cmd.Parameters.AddWithValue("@AfterValue", (object)r.AfterValue ?? DBNull.Value);
					cmd.Parameters.AddWithValue("@Result", r.Result ?? "Success");
					cmd.Parameters.AddWithValue("@CorrelationId", r.CorrelationId);
					cmd.Parameters.AddWithValue("@RowHash", hash);
					conn.Open();
					cmd.ExecuteNonQuery();
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"[ComplianceDb] WriteAudit failed: {ex.Message}");
			}
		}

		// ── Trade Blotter ──────────────────────────────────────────────────
		public static void WriteTradeBlotter(TradeBlotterRecord r)
		{
			try
			{
				string hash = ComputeHash(
					r.TimestampUtc + r.PortfolioId + r.Ticker +
					r.Side + r.Shares + r.OrderState);

				using (var conn = new SqlConnection(ConnStr))
				using (var cmd = new SqlCommand(@"
                    INSERT INTO TradeBlotter
                    (BlotterId, TimestampUtc, PortfolioId, Ticker, Side,
                     Shares, Price, OrderState, ExecutionVenue, ModelVersion,
                     SignalId, IsOverride, TraderId, CorrelationId, RowHash)
                    VALUES
                    (@BlotterId, @TimestampUtc, @PortfolioId, @Ticker, @Side,
                     @Shares, @Price, @OrderState, @ExecutionVenue, @ModelVersion,
                     @SignalId, @IsOverride, @TraderId, @CorrelationId, @RowHash)",
					conn))
				{
					cmd.Parameters.AddWithValue("@BlotterId", r.BlotterId);
					cmd.Parameters.AddWithValue("@TimestampUtc", r.TimestampUtc);
					cmd.Parameters.AddWithValue("@PortfolioId", r.PortfolioId ?? "");
					cmd.Parameters.AddWithValue("@Ticker", r.Ticker ?? "");
					cmd.Parameters.AddWithValue("@Side", r.Side ?? "");
					cmd.Parameters.AddWithValue("@Shares", r.Shares);
					cmd.Parameters.AddWithValue("@Price", (object)r.Price ?? DBNull.Value);
					cmd.Parameters.AddWithValue("@OrderState", r.OrderState ?? "");
					cmd.Parameters.AddWithValue("@ExecutionVenue", (object)r.ExecutionVenue ?? DBNull.Value);
					cmd.Parameters.AddWithValue("@ModelVersion", (object)r.ModelVersion ?? DBNull.Value);
					cmd.Parameters.AddWithValue("@SignalId", (object)r.SignalId ?? DBNull.Value);
					cmd.Parameters.AddWithValue("@IsOverride", r.IsOverride);
					cmd.Parameters.AddWithValue("@TraderId", (object)r.TraderId ?? DBNull.Value);
					cmd.Parameters.AddWithValue("@CorrelationId", r.CorrelationId);
					cmd.Parameters.AddWithValue("@RowHash", hash);
					conn.Open();
					cmd.ExecuteNonQuery();
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"[ComplianceDb] WriteTradeBlotter failed: {ex.Message}");
			}
		}

		// ── Exception Log ──────────────────────────────────────────────────
		public static void WriteException(ExceptionRecord r)
		{
			try
			{
				string hash = ComputeHash(
					r.TimestampUtc + r.ExceptionType +
					r.Severity + r.Description);

				using (var conn = new SqlConnection(ConnStr))
				using (var cmd = new SqlCommand(@"
                    INSERT INTO ExceptionLog
                    (ExceptionId, TimestampUtc, PortfolioId, ExceptionType,
                     Severity, Description, ResolutionNote, ResolvedBy,
                     CorrelationId, RowHash)
                    VALUES
                    (@ExceptionId, @TimestampUtc, @PortfolioId, @ExceptionType,
                     @Severity, @Description, @ResolutionNote, @ResolvedBy,
                     @CorrelationId, @RowHash)",
					conn))
				{
					cmd.Parameters.AddWithValue("@ExceptionId", r.ExceptionId);
					cmd.Parameters.AddWithValue("@TimestampUtc", r.TimestampUtc);
					cmd.Parameters.AddWithValue("@PortfolioId", (object)r.PortfolioId ?? DBNull.Value);
					cmd.Parameters.AddWithValue("@ExceptionType", r.ExceptionType ?? "");
					cmd.Parameters.AddWithValue("@Severity", r.Severity ?? "Warning");
					cmd.Parameters.AddWithValue("@Description", r.Description ?? "");
					cmd.Parameters.AddWithValue("@ResolutionNote", (object)r.ResolutionNote ?? DBNull.Value);
					cmd.Parameters.AddWithValue("@ResolvedBy", (object)r.ResolvedBy ?? DBNull.Value);
					cmd.Parameters.AddWithValue("@CorrelationId", r.CorrelationId);
					cmd.Parameters.AddWithValue("@RowHash", hash);
					conn.Open();
					cmd.ExecuteNonQuery();
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"[ComplianceDb] WriteException failed: {ex.Message}");
			}
		}
		public static void WriteRiskReport(DailyRiskRecord r)
		{
			try
			{
				string hash = ComputeHash(
					r.TimestampUtc + r.PortfolioId + r.RunType +
					r.NAV + r.GrossExposurePct + r.NetExposurePct);

				using (var conn = new SqlConnection(ConnStr))
				using (var cmd = new SqlCommand(@"
            INSERT INTO DailyRiskReport
            (ReportId, ReportDate, TimestampUtc, PortfolioId, RunType,
             NAV, LongExposurePct, ShortExposurePct, NetExposurePct, GrossExposurePct,
             LongCount, ShortCount,
             TopSector1Name, TopSector1Pct, TopSector2Name, TopSector2Pct,
             TopSector3Name, TopSector3Pct,
             VixScale, VolatilityScale, FinalRiskScale,
             CircuitBreakerState, CircuitBreakerActive, ActiveRiskFactors,
             ModelVersion, RowHash)
            VALUES
            (@ReportId, @ReportDate, @TimestampUtc, @PortfolioId, @RunType,
             @NAV, @LongExposurePct, @ShortExposurePct, @NetExposurePct, @GrossExposurePct,
             @LongCount, @ShortCount,
             @TopSector1Name, @TopSector1Pct, @TopSector2Name, @TopSector2Pct,
             @TopSector3Name, @TopSector3Pct,
             @VixScale, @VolatilityScale, @FinalRiskScale,
             @CircuitBreakerState, @CircuitBreakerActive, @ActiveRiskFactors,
             @ModelVersion, @RowHash)",
					conn))
				{
					cmd.Parameters.AddWithValue("@ReportId", r.ReportId);
					cmd.Parameters.AddWithValue("@ReportDate", r.ReportDate);
					cmd.Parameters.AddWithValue("@TimestampUtc", r.TimestampUtc);
					cmd.Parameters.AddWithValue("@PortfolioId", r.PortfolioId ?? "");
					cmd.Parameters.AddWithValue("@RunType", r.RunType ?? "Backtest");
					cmd.Parameters.AddWithValue("@NAV", (object)r.NAV ?? DBNull.Value);
					cmd.Parameters.AddWithValue("@LongExposurePct", (object)r.LongExposurePct ?? DBNull.Value);
					cmd.Parameters.AddWithValue("@ShortExposurePct", (object)r.ShortExposurePct ?? DBNull.Value);
					cmd.Parameters.AddWithValue("@NetExposurePct", (object)r.NetExposurePct ?? DBNull.Value);
					cmd.Parameters.AddWithValue("@GrossExposurePct", (object)r.GrossExposurePct ?? DBNull.Value);
					cmd.Parameters.AddWithValue("@LongCount", (object)r.LongCount ?? DBNull.Value);
					cmd.Parameters.AddWithValue("@ShortCount", (object)r.ShortCount ?? DBNull.Value);
					cmd.Parameters.AddWithValue("@TopSector1Name", (object)r.TopSector1Name ?? DBNull.Value);
					cmd.Parameters.AddWithValue("@TopSector1Pct", (object)r.TopSector1Pct ?? DBNull.Value);
					cmd.Parameters.AddWithValue("@TopSector2Name", (object)r.TopSector2Name ?? DBNull.Value);
					cmd.Parameters.AddWithValue("@TopSector2Pct", (object)r.TopSector2Pct ?? DBNull.Value);
					cmd.Parameters.AddWithValue("@TopSector3Name", (object)r.TopSector3Name ?? DBNull.Value);
					cmd.Parameters.AddWithValue("@TopSector3Pct", (object)r.TopSector3Pct ?? DBNull.Value);
					cmd.Parameters.AddWithValue("@VixScale", (object)r.VixScale ?? DBNull.Value);
					cmd.Parameters.AddWithValue("@VolatilityScale", (object)r.VolatilityScale ?? DBNull.Value);
					cmd.Parameters.AddWithValue("@FinalRiskScale", (object)r.FinalRiskScale ?? DBNull.Value);
					cmd.Parameters.AddWithValue("@CircuitBreakerState", (object)r.CircuitBreakerState ?? DBNull.Value);
					cmd.Parameters.AddWithValue("@CircuitBreakerActive", (object)r.CircuitBreakerActive ?? DBNull.Value);
					cmd.Parameters.AddWithValue("@ActiveRiskFactors", (object)r.ActiveRiskFactors ?? DBNull.Value);
					cmd.Parameters.AddWithValue("@ModelVersion", (object)r.ModelVersion ?? DBNull.Value);
					cmd.Parameters.AddWithValue("@RowHash", hash);
					conn.Open();
					cmd.ExecuteNonQuery();
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"[ComplianceDb] WriteRiskReport failed: {ex.Message}");
			}
		}

		// ── Row Hashing ────────────────────────────────────────────────────
		private static string ComputeHash(string input)
		{
			using (var sha = SHA256.Create())
			{
				byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
				return BitConverter.ToString(bytes).Replace("-", "").ToLower();
			}
		}
	}
}