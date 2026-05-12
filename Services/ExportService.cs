using ClosedXML.Excel;
using HEBRaffle.Models;
using Microsoft.Extensions.Logging;

namespace HEBRaffle.Services;

public sealed class ExportService : IExportService
{
    private readonly IDatabaseService _db;
    private readonly ILogger<ExportService> _logger;

    private static readonly string ExportFolder =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "HEBRaffle");

    public ExportService(IDatabaseService db, ILogger<ExportService> logger)
    {
        _db = db;
        _logger = logger;

        if (!Directory.Exists(ExportFolder))
            Directory.CreateDirectory(ExportFolder);
    }

    // ─── Participants ────────────────────────────────────────────────────────

    public async Task<string> ExportParticipantsAsync(CancellationToken ct = default)
    {
        var participants = await _db.GetAllParticipantsAsync();
        var filePath = Path.Combine(ExportFolder, $"Participants_{Timestamp()}.xlsx");

        await Task.Run(() =>
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Participants");

            // ── Header row ───────────────────────────────────────────────────
            SetupHeader(ws, ["#", "First Name", "Last Name", "Full Name", "Store",
                             "Years in Company", "Registration Date", "Winner"]);

            // ── Data rows ────────────────────────────────────────────────────
            for (int i = 0; i < participants.Count; i++)
            {
                var p   = participants[i];
                int row = i + 2;

                ws.Cell(row, 1).Value = i + 1;
                ws.Cell(row, 2).Value = p.FirstName;
                ws.Cell(row, 3).Value = p.LastName;
                ws.Cell(row, 4).Value = p.FullName;
                ws.Cell(row, 5).Value = p.Store;
                ws.Cell(row, 6).Value = p.YearsInCompany;
                ws.Cell(row, 7).Value = p.RegistrationDate.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
                ws.Cell(row, 8).Value = p.IsWinner ? "YES" : "NO";

                if (p.IsWinner)
                {
                    ws.Range(row, 1, row, 8)
                      .Style.Fill.SetBackgroundColor(XLColor.LightGreen);
                }
            }

            ApplyTableStyle(ws, participants.Count);
            wb.SaveAs(filePath);

        }, ct);

        _logger.LogInformation("Participants exported to {Path}", filePath);
        return filePath;
    }

    // ─── Winners ─────────────────────────────────────────────────────────────

    public async Task<string> ExportWinnersAsync(CancellationToken ct = default)
    {
        var winners = await _db.GetAllWinnersAsync();
        var filePath = Path.Combine(ExportFolder, $"Winners_{Timestamp()}.xlsx");

        await Task.Run(() =>
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Winners");

            SetupHeader(ws, ["Prize #", "First Name", "Last Name", "Full Name",
                             "Store", "Years in Company", "Draw Date"]);

            for (int i = 0; i < winners.Count; i++)
            {
                var w   = winners[i];
                int row = i + 2;

                ws.Cell(row, 1).Value = w.PrizeNumber;
                ws.Cell(row, 2).Value = w.Participant?.FirstName ?? "";
                ws.Cell(row, 3).Value = w.Participant?.LastName  ?? "";
                ws.Cell(row, 4).Value = w.Participant?.FullName  ?? "";
                ws.Cell(row, 5).Value = w.Participant?.Store     ?? "";
                ws.Cell(row, 6).Value = w.Participant?.YearsInCompany ?? 0;
                ws.Cell(row, 7).Value = w.DrawDate.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

                // Alternate row shading
                if (i % 2 == 1)
                {
                    ws.Range(row, 1, row, 7)
                      .Style.Fill.SetBackgroundColor(XLColor.FromHtml("#FFF5F5"));
                }
            }

            ApplyTableStyle(ws, winners.Count, 7);
            wb.SaveAs(filePath);

        }, ct);

        _logger.LogInformation("Winners exported to {Path}", filePath);
        return filePath;
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static void SetupHeader(IXLWorksheet ws, string[] headers)
    {
        for (int col = 0; col < headers.Length; col++)
        {
            var cell = ws.Cell(1, col + 1);
            cell.Value = headers[col];
            cell.Style.Font.Bold      = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.SetBackgroundColor(XLColor.FromHtml("#CC0000"));
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }
    }

    private static void ApplyTableStyle(IXLWorksheet ws, int dataRows, int cols = 8)
    {
        // Auto-fit columns
        ws.Columns(1, cols).AdjustToContents();

        // Freeze header row
        ws.SheetView.FreezeRows(1);

        // Border on all data cells
        if (dataRows > 0)
        {
            ws.Range(1, 1, dataRows + 1, cols)
              .Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin)
              .Border.SetInsideBorder(XLBorderStyleValues.Thin);
        }
    }

    private static string Timestamp() =>
        DateTime.Now.ToString("yyyyMMdd_HHmmss");
}
