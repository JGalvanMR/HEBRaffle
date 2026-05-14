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
    public int Imported   { get; init; }
    public int Skipped    { get; init; }   // duplicados
    public int Errors     { get; init; }
    public List<string> ErrorDetails { get; init; } = [];

    public string Summary =>
        $"✅ {Imported} imported   ⏭ {Skipped} duplicates   ❌ {Errors} errors";
}

// ════════════════════════════════════════════════════════════════════════════
// Interface
// ════════════════════════════════════════════════════════════════════════════

public interface IImportService
{
    /// <summary>
    /// Importa participantes desde un archivo Excel (.xlsx).
    /// Detecta automáticamente las columnas del archivo HEB.
    /// </summary>
    Task<ImportResult> ImportFromExcelAsync(string filePath, CancellationToken ct = default);

    /// <summary>
    /// Permite al usuario seleccionar un archivo .xlsx desde el dispositivo.
    /// Retorna null si cancela.
    /// </summary>
    Task<string?> PickExcelFileAsync();
}

// ════════════════════════════════════════════════════════════════════════════
// Implementation
// ════════════════════════════════════════════════════════════════════════════

public sealed class ImportService : IImportService
{
    private readonly IDatabaseService       _db;
    private readonly ILogger<ImportService> _logger;

    // Nombres de columna aceptados (case-insensitive, sin acentos)
    private static readonly string[] NameCols    = ["nombre", "nombre completo", "name", "full name", "nombre_completo"];
    private static readonly string[] YearsCols   = ["tiempo", "tiempo en heb", "años", "years", "antiguedad", "antigüedad", "tiempo_en_heb"];
    private static readonly string[] StoreCols   = ["tienda", "store", "sucursal", "location"];
    private static readonly string[] EmailCols   = ["correo", "email", "mail", "e-mail", "correo electronico", "correo_electronico"];

    public ImportService(IDatabaseService db, ILogger<ImportService> logger)
    {
        _db     = db;
        _logger = logger;
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
                    { DevicePlatform.Android, ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                                               "application/vnd.ms-excel"] },
                    { DevicePlatform.iOS,     ["com.microsoft.excel.xls",
                                               "org.openxmlformats.spreadsheetml.sheet"] },
                    { DevicePlatform.WinUI,   [".xlsx", ".xls"] },
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

    public async Task<ImportResult> ImportFromExcelAsync(string filePath, CancellationToken ct = default)
    {
        int imported = 0, skipped = 0, errors = 0;
        var errorDetails = new List<string>();

        try
        {
            // Copiar a temp para liberar el file lock en Android
            var tempPath = Path.Combine(Path.GetTempPath(), Path.GetFileName(filePath));
            File.Copy(filePath, tempPath, overwrite: true);

            using var wb = new XLWorkbook(tempPath);

            // Tomar la primera hoja que tenga datos
            var ws = wb.Worksheets.FirstOrDefault(s => s.RowsUsed().Any())
                     ?? throw new InvalidOperationException("The Excel file has no data.");

            var rows = ws.RowsUsed().ToList();
            if (rows.Count < 2)
                return new ImportResult { ErrorDetails = ["File has no data rows."] };

            // ── Detectar columnas por encabezado ─────────────────────────────
            var headerRow = rows[0];
            int colName  = FindColumn(headerRow, NameCols);
            int colYears = FindColumn(headerRow, YearsCols);
            int colStore = FindColumn(headerRow, StoreCols);
            int colEmail = FindColumn(headerRow, EmailCols);  // opcional

            if (colName == -1)
                return new ImportResult { Errors = 1, ErrorDetails = ["Column 'NOMBRE' not found. Check the Excel header row."] };
            if (colStore == -1)
                return new ImportResult { Errors = 1, ErrorDetails = ["Column 'TIENDA' not found. Check the Excel header row."] };

            _logger.LogInformation(
                "Import columns — Name:{N} Years:{Y} Store:{S} Email:{E}",
                colName, colYears, colStore, colEmail);

            // ── Procesar filas de datos ──────────────────────────────────────
            for (int i = 1; i < rows.Count; i++)
            {
                ct.ThrowIfCancellationRequested();

                var row = rows[i];

                try
                {
                    // Nombre completo → FirstName + LastName
                    var fullName = GetCell(row, colName);
                    if (string.IsNullOrWhiteSpace(fullName)) continue;

                    var (firstName, lastName) = SplitFullName(fullName);

                    // Tienda
                    var store = colStore != -1 ? GetCell(row, colStore) : string.Empty;
                    if (string.IsNullOrWhiteSpace(store))
                    {
                        errorDetails.Add($"Row {i + 1} [{fullName}]: Store is empty — skipped.");
                        errors++;
                        continue;
                    }

                    // Años en la empresa (ej: "5 años", "10", "3 years")
                    int years = 0;
                    if (colYears != -1)
                    {
                        var yearsRaw = GetCell(row, colYears);
                        years = ParseYears(yearsRaw);
                    }

                    // Email (opcional)
                    var email = colEmail != -1 ? GetCell(row, colEmail) : string.Empty;

                    // Verificar duplicado
                    var isDupe = await _db.ExistsDuplicateAsync(firstName, lastName, store);
                    if (isDupe)
                    {
                        skipped++;
                        _logger.LogDebug("Duplicate skipped: {Name} / {Store}", fullName, store);
                        continue;
                    }

                    // Insertar
                    var participant = new Participant
                    {
                        FirstName       = firstName,
                        LastName        = lastName,
                        Store           = store,
                        YearsInCompany  = years,
                        Email           = email,
                        RegistrationDate = DateTime.UtcNow
                    };

                    await _db.InsertParticipantAsync(participant);
                    imported++;
                }
                catch (Exception ex)
                {
                    errors++;
                    errorDetails.Add($"Row {i + 1}: {ex.Message}");
                    _logger.LogWarning(ex, "Error importing row {Row}", i + 1);
                }
            }

            // Limpiar temp
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

    /// <summary>Busca la columna cuyo encabezado coincide con alguno de los candidatos.</summary>
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

    private static string GetCell(IXLRow row, int col)
    {
        if (col == -1) return string.Empty;
        return row.Cell(col).GetString().Trim();
    }

    /// <summary>
    /// Normaliza texto: minúsculas + quitar acentos para comparación robusta.
    /// </summary>
    private static string Normalize(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        var lower = input.ToLowerInvariant();
        return lower
            .Replace("á", "a").Replace("é", "e").Replace("í", "i")
            .Replace("ó", "o").Replace("ú", "u").Replace("ü", "u")
            .Trim();
    }

    /// <summary>
    /// "Juan Pérez López" → ("Juan", "Pérez López")
    /// "John" → ("John", "")
    /// </summary>
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

    /// <summary>
    /// Extrae el número de strings como "5 años", "10 years", "3", "2.5".
    /// Redondea si viene decimal.
    /// </summary>
    private static int ParseYears(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return 0;

        // Extraer primer número (entero o decimal) del string
        var match = Regex.Match(raw, @"\d+(\.\d+)?");
        if (!match.Success) return 0;

        return (int)Math.Round(double.Parse(match.Value,
            System.Globalization.CultureInfo.InvariantCulture));
    }
}
