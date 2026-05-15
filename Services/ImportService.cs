using ClosedXML.Excel;
using HEBRaffle.Models;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace HEBRaffle.Services;

// ════════════════════════════════════════════════════════════════════════════
// Resultado del import
// ════════════════════════════════════════════════════════════════════════════

public sealed class ImportResult
{
    public int Imported  { get; init; }
    public int Skipped   { get; init; }
    public int Errors    { get; init; }
    public List<string> ErrorDetails { get; init; } = [];

    public string Summary =>
        $"✅ {Imported} imported   ⏭ {Skipped} duplicates   ❌ {Errors} errors";
}

// ════════════════════════════════════════════════════════════════════════════
// Interface
// ════════════════════════════════════════════════════════════════════════════

public interface IImportService
{
    /// <summary>Importa participantes desde un archivo Excel.</summary>
    Task<ImportResult> ImportFromExcelAsync(string filePath, CancellationToken ct = default);

    /// <summary>Abre el file picker para seleccionar un .xlsx.</summary>
    Task<string?> PickExcelFileAsync();

    /// <summary>
    /// Genera el template Excel con encabezados y filas de ejemplo,
    /// lo guarda en temp y retorna la ruta para compartirlo.
    /// </summary>
    Task<string> GenerateTemplateAsync();
}

// ════════════════════════════════════════════════════════════════════════════
// Implementation
// ════════════════════════════════════════════════════════════════════════════

public sealed class ImportService : IImportService
{
    private readonly IDatabaseService       _db;
    private readonly ILogger<ImportService> _logger;

    private static readonly string[] NameCols  = ["nombre", "nombre completo", "name", "full name", "nombre_completo"];
    private static readonly string[] YearsCols = ["tiempo", "tiempo en heb", "años", "years", "antiguedad", "antigüedad", "tiempo_en_heb"];
    private static readonly string[] StoreCols = ["tienda", "store", "sucursal", "location"];
    private static readonly string[] EmailCols = ["correo", "email", "mail", "e-mail", "correo electronico", "correo_electronico"];

    public ImportService(IDatabaseService db, ILogger<ImportService> logger)
    {
        _db     = db;
        _logger = logger;
    }

    // ─── Template generation ─────────────────────────────────────────────────

    public Task<string> GenerateTemplateAsync()
    {
        var filePath = Path.Combine(
            Path.GetTempPath(),
            "HEB_Participants_Template.xlsx");

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("HEB");

        // ── Column widths ─────────────────────────────────────────────────
        ws.Column(1).Width = 30;   // NOMBRE completo
        ws.Column(2).Width = 18;   // Tiempo en HEB
        ws.Column(3).Width = 28;   // Tienda
        ws.Column(4).Width = 32;   // CORREO

        // ── Header row ────────────────────────────────────────────────────
        var headers = new[] { "NOMBRE completo", "Tiempo en HEB", "Tienda", "CORREO" };
        for (int col = 1; col <= headers.Length; col++)
        {
            var cell = ws.Cell(1, col);
            cell.Value = headers[col - 1];
            cell.Style.Font.Bold      = true;
            cell.Style.Font.FontSize  = 12;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.SetBackgroundColor(XLColor.FromHtml("#CC0000"));
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Alignment.Vertical   = XLAlignmentVerticalValues.Center;
            cell.Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin);
        }
        ws.Row(1).Height = 22;

        // ── Example rows (3 samples, gray italic) ────────────────────────
        var examples = new[]
        {
            new[] { "Juan Pérez López",   "5 años",  "HEB San Antonio / 3",   "juan.perez@heb.com"   },
            new[] { "Maria García Ruiz",  "12 años", "HEB Austin Central",     "maria.garcia@heb.com" },
            new[] { "Carlos Mendoza",     "3 años",  "HEB Houston / 7",        "carlos.m@heb.com"     },
        };

        for (int r = 0; r < examples.Length; r++)
        {
            int row = r + 2;
            for (int col = 1; col <= 4; col++)
            {
                var cell = ws.Cell(row, col);
                cell.Value = examples[r][col - 1];
                cell.Style.Font.Italic    = true;
                cell.Style.Font.FontColor = XLColor.FromHtml("#9E9E9E");
                cell.Style.Fill.SetBackgroundColor(XLColor.FromHtml("#F5F5F5"));
                cell.Style.Border.SetOutsideBorder(XLBorderStyleValues.Hair);
            }
        }

        // ── Instructions in row 6 ─────────────────────────────────────────
        var note = ws.Cell(6, 1);
        note.Value = "⚠  Delete the gray example rows before uploading. " +
                     "Required columns: NOMBRE completo, Tienda. " +
                     "Optional: Tiempo en HEB, CORREO.";
        note.Style.Font.Italic    = true;
        note.Style.Font.FontSize  = 10;
        note.Style.Font.FontColor = XLColor.FromHtml("#F57C00");
        ws.Range("A6:D6").Merge();
        ws.Row(6).Height = 18;

        // ── Freeze header ─────────────────────────────────────────────────
        ws.SheetView.FreezeRows(1);

        wb.SaveAs(filePath);

        _logger.LogInformation("Template generated at {Path}", filePath);
        return Task.FromResult(filePath);
    }

    // ─── File picker ─────────────────────────────────────────────────────────

    public async Task<string?> PickExcelFileAsync()
    {
        try
        {
            var options = new PickOptions
            {
                PickerTitle = "Select Excel File (.xlsx)",
                FileTypes   = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.Android,     ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                                                    "application/vnd.ms-excel"] },
                    { DevicePlatform.iOS,         ["com.microsoft.excel.xls",
                                                    "org.openxmlformats.spreadsheetml.sheet"] },
                    { DevicePlatform.WinUI,       [".xlsx", ".xls"] },
                    { DevicePlatform.MacCatalyst, ["xlsx"] },
                })
            };

            var result = await FilePicker.Default.PickAsync(options);
            return result?.FullPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "File picker failed.");
            return null;
        }
    }

    // ─── Import ──────────────────────────────────────────────────────────────

    public async Task<ImportResult> ImportFromExcelAsync(
        string filePath, CancellationToken ct = default)
    {
        int imported = 0, skipped = 0, errors = 0;
        var errorDetails = new List<string>();

        try
        {
            var tempPath = Path.Combine(Path.GetTempPath(), Path.GetFileName(filePath));
            File.Copy(filePath, tempPath, overwrite: true);

            using var wb = new XLWorkbook(tempPath);

            var ws = wb.Worksheets.FirstOrDefault(s => s.RowsUsed().Any())
                     ?? throw new InvalidOperationException("The Excel file has no data.");

            var rows = ws.RowsUsed().ToList();
            if (rows.Count < 2)
                return new ImportResult { ErrorDetails = ["File has no data rows."] };

            var headerRow = rows[0];
            int colName  = FindColumn(headerRow, NameCols);
            int colYears = FindColumn(headerRow, YearsCols);
            int colStore = FindColumn(headerRow, StoreCols);
            int colEmail = FindColumn(headerRow, EmailCols);

            if (colName == -1)
                return new ImportResult { Errors = 1,
                    ErrorDetails = ["Column 'NOMBRE' not found. Download the template for reference."] };
            if (colStore == -1)
                return new ImportResult { Errors = 1,
                    ErrorDetails = ["Column 'TIENDA' not found. Download the template for reference."] };

            _logger.LogInformation(
                "Import columns — Name:{N} Years:{Y} Store:{S} Email:{E}",
                colName, colYears, colStore, colEmail);

            for (int i = 1; i < rows.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var row = rows[i];

                try
                {
                    var fullName = GetCell(row, colName);
                    if (string.IsNullOrWhiteSpace(fullName)) continue;

                    var (firstName, lastName) = SplitFullName(fullName);

                    var store = colStore != -1 ? GetCell(row, colStore) : string.Empty;
                    if (string.IsNullOrWhiteSpace(store))
                    {
                        errorDetails.Add($"Row {i + 1} [{fullName}]: Store is empty — skipped.");
                        errors++;
                        continue;
                    }

                    int years = colYears != -1 ? ParseYears(GetCell(row, colYears)) : 0;
                    var email = colEmail != -1 ? GetCell(row, colEmail) : string.Empty;

                    var isDupe = await _db.ExistsDuplicateAsync(firstName, lastName, store);
                    if (isDupe) { skipped++; continue; }

                    await _db.InsertParticipantAsync(new Participant
                    {
                        FirstName        = firstName,
                        LastName         = lastName,
                        Store            = store,
                        YearsInCompany   = years,
                        Email            = email,
                        RegistrationDate = DateTime.UtcNow
                    });
                    imported++;
                }
                catch (Exception ex)
                {
                    errors++;
                    errorDetails.Add($"Row {i + 1}: {ex.Message}");
                    _logger.LogWarning(ex, "Error importing row {Row}", i + 1);
                }
            }

            try { File.Delete(tempPath); } catch { /* ignore */ }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Import failed for file: {File}", filePath);
            errorDetails.Add($"Fatal error: {ex.Message}");
            errors++;
        }

        _logger.LogInformation(
            "Import complete — Imported:{I} Skipped:{S} Errors:{E}",
            imported, skipped, errors);

        return new ImportResult
        {
            Imported     = imported,
            Skipped      = skipped,
            Errors       = errors,
            ErrorDetails = errorDetails
        };
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static int FindColumn(IXLRow headerRow, string[] candidates)
    {
        foreach (var cell in headerRow.CellsUsed())
        {
            var val = Normalize(cell.GetString());
            if (candidates.Any(c => val.Contains(c)))
                return cell.Address.ColumnNumber;
        }
        return -1;
    }

    private static string GetCell(IXLRow row, int col) =>
        col == -1 ? string.Empty : row.Cell(col).GetString().Trim();

    private static string Normalize(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        return input.ToLowerInvariant()
            .Replace("á","a").Replace("é","e").Replace("í","i")
            .Replace("ó","o").Replace("ú","u").Replace("ü","u")
            .Trim();
    }

    private static (string First, string Last) SplitFullName(string fullName)
    {
        var parts = fullName.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            0 => ("Unknown", ""),
            1 => (parts[0], ""),
            _ => (parts[0], parts[1])
        };
    }

    private static int ParseYears(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return 0;
        var match = Regex.Match(raw, @"\d+(\.\d+)?");
        if (!match.Success) return 0;
        return (int)Math.Round(double.Parse(
            match.Value, System.Globalization.CultureInfo.InvariantCulture));
    }
}