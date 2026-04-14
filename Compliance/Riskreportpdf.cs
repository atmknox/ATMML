using System;
using System.IO;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ATMML.Compliance
{
	public static class RiskReportPdf
	{
		public static string Generate(DailyRiskRecord record)
		{
			try
			{
				QuestPDF.Settings.License = LicenseType.Community;

				var folder = Path.Combine(
					@"C:\Users\Admin\Documents\ATMML\Compliance Reports");
				Directory.CreateDirectory(folder);

				var safeName = (record.PortfolioId ?? "Portfolio")
					.Replace(" ", "_").Replace("\\", "").Replace("/", "");
				var fileName = $"RiskReport_{safeName}_{record.ReportDate:yyyyMMdd}_{DateTime.UtcNow:HHmmss}.pdf";
				var filePath = Path.Combine(folder, fileName);

				Document.Create(container =>
				{
					container.Page(page =>
					{
						page.Size(PageSizes.A4);
						page.Margin(36);
						page.DefaultTextStyle(x => x.FontSize(8).FontFamily("Arial"));

						// ── Header ────────────────────────────────────────
						page.Header().Column(col =>
						{
							col.Item().Background("#0F1E3C").Padding(14).Column(inner =>
							{
								inner.Item()
									.Text("DAILY RISK REPORT")
									.FontSize(18).Bold().FontColor("#FFFFFF");
								inner.Item()
									.Text($"Capital Markets Research  ·  {record.PortfolioId}  ·  {record.ReportDate:MMMM d, yyyy}")
									.FontSize(10).FontColor("#B4C8E6");
							});
							col.Item().Height(3).Background("#3478C8");
						});

						// ── Content ───────────────────────────────────────
						page.Content().PaddingTop(10).Column(col =>
						{
							// Portfolio Overview
							col.Item().Element(c => SectionHeader(c, "PORTFOLIO OVERVIEW"));
							col.Item().Element(c => Table(c, t =>
							{
								Row(t, "Portfolio", record.PortfolioId ?? "—", true);
								Row(t, "Report Date", record.ReportDate.ToString("MMMM d, yyyy"), false);
								Row(t, "Run Type", record.RunType ?? "—", true);
								Row(t, "Model", record.ModelVersion ?? "—", false);
								Row(t, "NAV", FormatNav(record.NAV), true);
								Row(t, "Generated", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm") + " UTC", false);
							}));

							col.Item().Height(8);

							// Exposure Summary
							col.Item().Element(c => SectionHeader(c, "EXPOSURE SUMMARY"));
							col.Item().Element(c => Table(c, t =>
							{
								Row(t, "Gross Exposure", FormatPct(record.GrossExposurePct), true);
								Row(t, "Net Exposure", FormatPct(record.NetExposurePct), false);
								Row(t, "Long Exposure", FormatPct(record.LongExposurePct), true);
								Row(t, "Short Exposure", FormatPct(record.ShortExposurePct), false);
							}));

							col.Item().Height(8);

							// Position Summary
							col.Item().Element(c => SectionHeader(c, "POSITION SUMMARY"));
							col.Item().Element(c => Table(c, t =>
							{
								Row(t, "Long Positions", record.LongCount?.ToString() ?? "—", true);
								Row(t, "Short Positions", record.ShortCount?.ToString() ?? "—", false);
								var total = (record.LongCount ?? 0) + (record.ShortCount ?? 0);
								Row(t, "Total Positions", total > 0 ? total.ToString() : "—", true);
							}));

							col.Item().Height(8);

							// Sector Concentration
							col.Item().Element(c => SectionHeader(c, "SECTOR CONCENTRATION  (top 3 by weight)"));
							col.Item().Element(c => Table(c, t =>
							{
								Row(t, record.TopSector1Name ?? "—", FormatPct(record.TopSector1Pct), true);
								Row(t, record.TopSector2Name ?? "—", FormatPct(record.TopSector2Pct), false);
								Row(t, record.TopSector3Name ?? "—", FormatPct(record.TopSector3Pct), true);
							}));

							col.Item().Height(8);

							// Risk Governor
							col.Item().Element(c => SectionHeader(c, "RISK GOVERNOR STATUS"));
							col.Item().Element(c => Table(c, t =>
							{
								bool cbActive = record.CircuitBreakerActive == true;
								RowColored(t, "Circuit Breaker",
									record.CircuitBreakerState ?? "—",
									cbActive ? "#C83C3C" : "#288C50", true);
								Row(t, "Final Risk Scale", FormatScale(record.FinalRiskScale), false);
								Row(t, "Volatility Scale", FormatScale(record.VolatilityScale), true);
								Row(t, "VIX Scale", FormatScale(record.VixScale), false);
								Row(t, "Active Factors", record.ActiveRiskFactors ?? "—", true);
							}));

							col.Item().Height(8);

							// Audit Integrity
							col.Item().Element(c => SectionHeader(c, "AUDIT INTEGRITY"));
							col.Item().Element(c => Table(c, t =>
							{
								Row(t, "Record ID", record.ReportId.ToString(), true);
								Row(t, "DB Storage", "SHA-256 hashed, append-only (ComplianceDb)", false);
							}));
						});

						// ── Footer ────────────────────────────────────────
						page.Footer().BorderTop(1).BorderColor("#C8D2E1")
							.PaddingTop(4).Row(row =>
							{
								row.RelativeItem().Text(
									$"Capital Markets Research  ·  Compliance Record  ·  {record.ReportId}")
									.FontSize(6.5f).Italic().FontColor("#969696");
								row.AutoItem().Text(x =>
								{
									x.Span("Page ").FontSize(6.5f).Italic().FontColor("#969696");
									x.CurrentPageNumber().FontSize(6.5f).Italic().FontColor("#969696");
								});
							});
					});
				}).GeneratePdf(filePath);

				return filePath;
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"[RiskReportPdf] Generate failed: {ex.Message}");
				return null;
			}
		}
		// ── Layout helpers ──────────────────────────────────────────────────

		private static void SectionHeader(IContainer c, string title)
		{
			c.Background("#3478C8").Padding(5).PaddingLeft(8)
				.Text(title).FontSize(9).Bold().FontColor("#FFFFFF");
		}

		private static void Table(IContainer c, Action<TableDescriptor> rows)
		{
			c.Table(t =>
			{
				t.ColumnsDefinition(cols =>
				{
					cols.RelativeColumn(42);
					cols.RelativeColumn(58);
				});
				rows(t);
			});
		}

		private static void Row(TableDescriptor t, string label, string value, bool alt)
		{
			var bg = alt ? "#F0F4FA" : "#FFFFFF";
			t.Cell().Background(bg).Border(0.5f).BorderColor("#C8D2E1")
				.Padding(5).PaddingLeft(8)
				.Text(label).FontSize(8).Bold().FontColor("#3C5078");
			t.Cell().Background(bg).Border(0.5f).BorderColor("#C8D2E1")
				.Padding(5).PaddingLeft(8)
				.Text(value).FontSize(8).FontColor("#1E1E1E");
		}

		private static void RowColored(TableDescriptor t, string label,
			string value, string valueColor, bool alt)
		{
			var bg = alt ? "#F0F4FA" : "#FFFFFF";
			t.Cell().Background(bg).Border(0.5f).BorderColor("#C8D2E1")
				.Padding(5).PaddingLeft(8)
				.Text(label).FontSize(8).Bold().FontColor("#3C5078");
			t.Cell().Background(bg).Border(0.5f).BorderColor("#C8D2E1")
				.Padding(5).PaddingLeft(8)
				.Text(value).FontSize(8).Bold().FontColor(valueColor);
		}

		// ── Formatters ──────────────────────────────────────────────────────
		private static string FormatNav(decimal? v) => v.HasValue ? $"${v.Value:N0}" : "—";
		private static string FormatPct(decimal? v) => v.HasValue ? $"{v.Value * 100m:F2}%" : "—";
		private static string FormatScale(decimal? v) => v.HasValue ? $"{v.Value:F4}" : "—";
	}
}