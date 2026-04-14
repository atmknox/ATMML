using System;
using System.Collections.Generic;
using System.IO;
using System.Data.SqlClient;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ATMML.Compliance
{
	public static class AuditReportPdf
	{
		private const string ConnStr =
			@"Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=ComplianceDb;Integrated Security=True;";

		public class AuditRow
		{
			public DateTime TimestampUtc { get; set; }
			public string Username { get; set; }
			public string UserRole { get; set; }
			public string Action { get; set; }
			public string ObjectType { get; set; }
			public string ObjectId { get; set; }
			public string Result { get; set; }
			public string AfterValue { get; set; }
		}

		/// <summary>
		/// Generates an Audit Report PDF for the given date range.
		/// Pass DateTime.MinValue for fromDate to include all records.
		/// Returns the full path of the saved file, or null on failure.
		/// </summary>
		public static string Generate(DateTime fromDate, DateTime toDate, string actionFilter = null)
		{
			try
			{
				QuestPDF.Settings.License = LicenseType.Community;

				var rows = LoadRows(fromDate, toDate, actionFilter);

				var folder = @"C:\Users\Admin\Documents\ATMML\Compliance Reports";
				Directory.CreateDirectory(folder);

				var dateStr = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
				var fileName = $"AuditReport_{dateStr}.pdf";
				var filePath = Path.Combine(folder, fileName);

				var reportDate = toDate == DateTime.MaxValue ? DateTime.UtcNow : toDate;
				var periodLabel = fromDate == DateTime.MinValue
					? "All Records"
					: $"{fromDate:yyyy-MM-dd}  to  {reportDate:yyyy-MM-dd}";

				// Count by action type
				int rebalanceCount = 0;
				int tamperCount = 0;
				int paramCount = 0;
				int overrideCount = 0;
				int otherCount = 0;
				foreach (var r in rows)
				{
					switch (r.Action)
					{
						case "REBALANCE_REQUEST": rebalanceCount++; break;
						case "TAMPER_ATTEMPT": tamperCount++; break;
						case "MODEL_PARAMETER_CHANGE": paramCount++; break;
						case "MANUAL_OVERRIDE": overrideCount++; break;
						default: otherCount++; break;
					}
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
									.Text("AUDIT & ACCESS REPORT")
									.FontSize(18).Bold().FontColor("#FFFFFF");
								inner.Item()
									.Text($"Capital Markets Research  ·  {periodLabel}")
									.FontSize(10).FontColor("#B4C8E6");
							});
							col.Item().Height(3).Background("#3478C8");
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
								TableRow(t, "Total Records", rows.Count.ToString(), false);
								TableRow(t, "Rebalance Requests", rebalanceCount.ToString(), true);
								TableRow(t, "Parameter Changes", paramCount.ToString(), false);
								TableRow(t, "Manual Overrides", overrideCount.ToString(), true);
								TableRowColored(t, "Tamper Attempts",
									tamperCount.ToString(),
									tamperCount > 0 ? "#C83C3C" : "#288C50", false);
								TableRow(t, "Other Actions", otherCount.ToString(), true);
								TableRow(t, "Generated",
									DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm") + " UTC", false);
							});

							col.Item().Height(10);

							// Audit detail
							col.Item().Background("#3478C8").Padding(5).PaddingLeft(8)
								.Text("AUDIT DETAIL").FontSize(9).Bold().FontColor("#FFFFFF");

							if (rows.Count == 0)
							{
								col.Item().Background("#F0F4FA").Border(0.5f).BorderColor("#C8D2E1")
									.Padding(8)
									.Text("No audit records found for the selected period.")
									.FontSize(8).FontColor("#3C5078");
							}
							else
							{
								col.Item().Table(t =>
								{
									t.ColumnsDefinition(c =>
									{
										c.RelativeColumn(18); // Timestamp
										c.RelativeColumn(14); // User
										c.RelativeColumn(10); // Role
										c.RelativeColumn(22); // Action
										c.RelativeColumn(18); // Object
										c.RelativeColumn(10); // Result
									});

									// Header row
									HeaderCell(t, "Timestamp");
									HeaderCell(t, "User");
									HeaderCell(t, "Role");
									HeaderCell(t, "Action");
									HeaderCell(t, "Object");
									HeaderCell(t, "Result");

									// Data rows
									bool alt = false;
									foreach (var r in rows)
									{
										var bg = alt ? "#F0F4FA" : "#FFFFFF";
										DataCell(t, r.TimestampUtc.ToString("MM-dd HH:mm"), bg);
										DataCell(t, r.Username ?? "—", bg);
										DataCell(t, r.UserRole ?? "—", bg);

										// Color tamper attempts red
										if (r.Action == "TAMPER_ATTEMPT")
											DataCellColored(t, r.Action ?? "—", "#C83C3C", bg);
										else if (r.Action == "MANUAL_OVERRIDE")
											DataCellColored(t, r.Action ?? "—", "#D07820", bg);
										else
											DataCell(t, r.Action ?? "—", bg);

										DataCell(t, r.ObjectId ?? "—", bg);
										DataCell(t, r.Result ?? "—", bg);
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
									.Text("Capital Markets Research  ·  Compliance Record  ·  Audit & Access Report")
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
				System.Diagnostics.Debug.WriteLine($"[AuditReportPdf] Generate failed: {ex.Message}");
				return null;
			}
		}

		// ── Data loader ─────────────────────────────────────────────────────

		private static List<AuditRow> LoadRows(DateTime from, DateTime to, string actionFilter)
		{
			var rows = new List<AuditRow>();
			try
			{
				using (var conn = new SqlConnection(ConnStr))
				{
					var sql = @"SELECT TOP 500 TimestampUtc, Username, UserRole,
						Action, ObjectType, ObjectId, Result, AfterValue
						FROM AuditLog
						WHERE (@From = '0001-01-01' OR TimestampUtc >= @From)
						AND   (@To   = '9999-12-31' OR TimestampUtc <= @To)
						AND   (@Action IS NULL OR Action = @Action)
						ORDER BY TimestampUtc DESC";

					using (var cmd = new SqlCommand(sql, conn))
					{
						cmd.Parameters.AddWithValue("@From",
							from == DateTime.MinValue ? new DateTime(1, 1, 1) : from);
						cmd.Parameters.AddWithValue("@To",
							to == DateTime.MaxValue ? new DateTime(9999, 12, 31) : to);
						cmd.Parameters.AddWithValue("@Action",
							(object)actionFilter ?? DBNull.Value);

						conn.Open();
						using (var reader = cmd.ExecuteReader())
						{
							while (reader.Read())
							{
								rows.Add(new AuditRow
								{
									TimestampUtc = reader.GetDateTime(0),
									Username = reader.IsDBNull(1) ? null : reader.GetString(1),
									UserRole = reader.IsDBNull(2) ? null : reader.GetString(2),
									Action = reader.IsDBNull(3) ? null : reader.GetString(3),
									ObjectType = reader.IsDBNull(4) ? null : reader.GetString(4),
									ObjectId = reader.IsDBNull(5) ? null : reader.GetString(5),
									Result = reader.IsDBNull(6) ? null : reader.GetString(6),
									AfterValue = reader.IsDBNull(7) ? null : reader.GetString(7)
								});
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"[AuditReportPdf] LoadRows failed: {ex.Message}");
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
