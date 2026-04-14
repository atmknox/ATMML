using System;
using System.Collections.Generic;
using System.IO;
using System.Data.SqlClient;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ATMML.Compliance
{
	public static class ExceptionReportPdf
	{
		private const string ConnStr =
			@"Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=ComplianceDb;Integrated Security=True;";

		public class ExceptionRow
		{
			public DateTime TimestampUtc { get; set; }
			public string PortfolioId { get; set; }
			public string ExceptionType { get; set; }
			public string Severity { get; set; }
			public string Description { get; set; }
			public string ResolvedBy { get; set; }
		}

		/// <summary>
		/// Generates an Exception Report PDF for the given date range.
		/// Pass DateTime.MinValue for fromDate to include all records.
		/// Returns the full path of the saved file, or null on failure.
		/// </summary>
		public static string Generate(DateTime fromDate, DateTime toDate, string portfolioFilter = null)
		{
			try
			{
				QuestPDF.Settings.License = LicenseType.Community;

				var rows = LoadRows(fromDate, toDate, portfolioFilter);

				var folder = @"C:\Users\Admin\Documents\ATMML\Compliance Reports";
				Directory.CreateDirectory(folder);

				var dateStr = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
				var fileName = $"ExceptionReport_{dateStr}.pdf";
				var filePath = Path.Combine(folder, fileName);

				var reportDate = toDate == DateTime.MaxValue ? DateTime.UtcNow : toDate;
				var periodLabel = fromDate == DateTime.MinValue
					? "All Records"
					: $"{fromDate:yyyy-MM-dd}  to  {reportDate:yyyy-MM-dd}";

				int highCount = 0;
				int warningCount = 0;
				foreach (var r in rows)
				{
					if (r.Severity == "High") highCount++;
					if (r.Severity == "Warning") warningCount++;
				}

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
									.Text("EXCEPTION REPORT")
									.FontSize(18).Bold().FontColor("#FFFFFF");
								inner.Item()
									.Text($"Capital Markets Research  ·  {periodLabel}")
									.FontSize(10).FontColor("#B4C8E6");
							});
							col.Item().Height(3).Background("#C83C3C");
						});

						// ── Content ───────────────────────────────────────
						page.Content().PaddingTop(10).Column(col =>
						{
							// Summary
							col.Item().Background("#3478C8").Padding(5).PaddingLeft(8)
								.Text("SUMMARY").FontSize(9).Bold().FontColor("#FFFFFF");

							col.Item().Table(t =>
							{
								t.ColumnsDefinition(c => { c.RelativeColumn(42); c.RelativeColumn(58); });
								TableRow(t, "Report Period", periodLabel, true);
								TableRow(t, "Portfolio Filter", portfolioFilter ?? "All", false);
								TableRow(t, "Total Exceptions", rows.Count.ToString(), true);
								TableRowColored(t, "High Severity", highCount.ToString(),
									highCount > 0 ? "#C83C3C" : "#288C50", false);
								TableRowColored(t, "Warnings", warningCount.ToString(),
									warningCount > 0 ? "#D07820" : "#288C50", true);
								TableRow(t, "Generated",
									DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm") + " UTC", false);
							});

							col.Item().Height(10);

							// Exception detail
							col.Item().Background("#3478C8").Padding(5).PaddingLeft(8)
								.Text("EXCEPTION DETAIL").FontSize(9).Bold().FontColor("#FFFFFF");

							if (rows.Count == 0)
							{
								col.Item().Background("#F0F4FA").Border(0.5f).BorderColor("#C8D2E1")
									.Padding(8)
									.Text("No exceptions found for the selected period.")
									.FontSize(8).FontColor("#3C5078");
							}
							else
							{
								// Column headers
								col.Item().Table(t =>
								{
									t.ColumnsDefinition(c =>
									{
										c.RelativeColumn(20); // Timestamp
										c.RelativeColumn(18); // Portfolio
										c.RelativeColumn(14); // Type
										c.RelativeColumn(10); // Severity
										c.RelativeColumn(38); // Description
									});

									// Header row
									HeaderCell(t, "Timestamp");
									HeaderCell(t, "Portfolio");
									HeaderCell(t, "Type");
									HeaderCell(t, "Severity");
									HeaderCell(t, "Description");

									// Data rows
									bool alt = false;
									foreach (var r in rows)
									{
										var bg = alt ? "#F0F4FA" : "#FFFFFF";
										DataCell(t, r.TimestampUtc.ToString("MM-dd HH:mm"), bg);
										DataCell(t, r.PortfolioId ?? "—", bg);
										DataCell(t, r.ExceptionType ?? "—", bg);
										DataCellColored(t, r.Severity ?? "—",
											r.Severity == "High" ? "#C83C3C" : "#D07820", bg);
										DataCell(t, r.Description ?? "—", bg);
										alt = !alt;
									}
								});
							}
						});

						// ── Footer ────────────────────────────────────────
						page.Footer().BorderTop(1).BorderColor("#C8D2E1")
							.PaddingTop(4).Row(row =>
							{
								row.RelativeItem()
									.Text("Capital Markets Research  ·  Compliance Record  ·  Exception Report")
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
				System.Diagnostics.Debug.WriteLine($"[ExceptionReportPdf] Generate failed: {ex.Message}");
				return null;
			}
		}

		// ── Data loader ─────────────────────────────────────────────────────

		private static List<ExceptionRow> LoadRows(DateTime from, DateTime to, string portfolioFilter)
		{
			var rows = new List<ExceptionRow>();
			try
			{
				using (var conn = new SqlConnection(ConnStr))
				{
					var sql = @"SELECT TOP 500 TimestampUtc, PortfolioId, ExceptionType,
						Severity, Description, ResolvedBy
						FROM ExceptionLog
						WHERE (@From = '0001-01-01' OR TimestampUtc >= @From)
						AND   (@To   = '9999-12-31' OR TimestampUtc <= @To)
						AND   (@Portfolio IS NULL OR PortfolioId = @Portfolio)
						ORDER BY TimestampUtc DESC";

					using (var cmd = new SqlCommand(sql, conn))
					{
						cmd.Parameters.AddWithValue("@From",
							from == DateTime.MinValue ? new DateTime(1, 1, 1) : from);
						cmd.Parameters.AddWithValue("@To",
							to == DateTime.MaxValue ? new DateTime(9999, 12, 31) : to);
						cmd.Parameters.AddWithValue("@Portfolio",
							(object)portfolioFilter ?? DBNull.Value);

						conn.Open();
						using (var reader = cmd.ExecuteReader())
						{
							while (reader.Read())
							{
								rows.Add(new ExceptionRow
								{
									TimestampUtc = reader.GetDateTime(0),
									PortfolioId = reader.IsDBNull(1) ? null : reader.GetString(1),
									ExceptionType = reader.IsDBNull(2) ? null : reader.GetString(2),
									Severity = reader.IsDBNull(3) ? null : reader.GetString(3),
									Description = reader.IsDBNull(4) ? null : reader.GetString(4),
									ResolvedBy = reader.IsDBNull(5) ? null : reader.GetString(5)
								});
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"[ExceptionReportPdf] LoadRows failed: {ex.Message}");
			}
			return rows;
		}

		// ── Table helpers ───────────────────────────────────────────────────

		private static void TableRow(TableDescriptor t, string label, string value, bool alt)
		{
			var bg = alt ? "#F0F4FA" : "#FFFFFF";
			t.Cell().Background(bg).Border(0.5f).BorderColor("#C8D2E1")
				.Padding(5).PaddingLeft(8)
				.Text(label).FontSize(8).Bold().FontColor("#3C5078");
			t.Cell().Background(bg).Border(0.5f).BorderColor("#C8D2E1")
				.Padding(5).PaddingLeft(8)
				.Text(value).FontSize(8).FontColor("#1E1E1E");
		}

		private static void TableRowColored(TableDescriptor t, string label,
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

		private static void HeaderCell(TableDescriptor t, string text)
		{
			t.Cell().Background("#1E3A5F").Border(0.5f).BorderColor("#C8D2E1")
				.Padding(4).PaddingLeft(6)
				.Text(text).FontSize(7.5f).Bold().FontColor("#FFFFFF");
		}

		private static void DataCell(TableDescriptor t, string text, string bg)
		{
			t.Cell().Background(bg).Border(0.5f).BorderColor("#C8D2E1")
				.Padding(4).PaddingLeft(6)
				.Text(text).FontSize(7.5f).FontColor("#1E1E1E");
		}

		private static void DataCellColored(TableDescriptor t, string text,
			string textColor, string bg)
		{
			t.Cell().Background(bg).Border(0.5f).BorderColor("#C8D2E1")
				.Padding(4).PaddingLeft(6)
				.Text(text).FontSize(7.5f).Bold().FontColor(textColor);
		}
	}
}
