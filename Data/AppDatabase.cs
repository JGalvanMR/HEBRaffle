using HEBRaffle.Models;
using SQLite;
using System.Diagnostics;

namespace HEBRaffle.Data;

/// <summary>
/// Thin wrapper around SQLiteAsyncConnection.
/// All public methods are thread-safe and fully async.
/// </summary>
public sealed class AppDatabase : IAsyncDisposable
{
    private SQLiteAsyncConnection? _db;
    private static readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;

    // ─── Path ────────────────────────────────────────────────────────────────

    public static string DatabasePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "hebraffle.db3");

    // ─── Initialization ──────────────────────────────────────────────────────

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_initialized) return;

        await _initLock.WaitAsync(ct);
        try
        {
            if (_initialized) return;

            _db = new SQLiteAsyncConnection(
                DatabasePath,
                SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.FullMutex);

            // PRAGMAs — individual try/catch para no romper el init completo
            try { await _db.ExecuteAsync("PRAGMA journal_mode=WAL;"); }
            catch (Exception ex) { Debug.WriteLine($"WAL pragma failed: {ex.Message}"); }

            try { await _db.ExecuteAsync("PRAGMA foreign_keys=ON;"); }
            catch (Exception ex) { Debug.WriteLine($"FK pragma failed: {ex.Message}"); }

            try { await _db.ExecuteAsync("PRAGMA synchronous=NORMAL;"); }
            catch (Exception ex) { Debug.WriteLine($"Synchronous pragma failed: {ex.Message}"); }

            // ── Crear tablas ──────────────────────────────────────────────────
            await _db.CreateTableAsync<Participant>();
            await _db.CreateTableAsync<Winner>();

            // ── Migraciones para DBs existentes ──────────────────────────────

            // Email (agregado en v1.1 con módulo de importación)
            try
            {
                await _db.ExecuteAsync("ALTER TABLE Participants ADD COLUMN Email TEXT DEFAULT '';");
                Debug.WriteLine("[DB] Migration: Email column added.");
            }
            catch
            {
                // Columna ya existe — ignorar (SQLite lanza error al duplicar)
            }

            // ── Índices ───────────────────────────────────────────────────────

            // Unique index: evita duplicados Nombre+Apellido+Tienda
            try
            {
                await _db.ExecuteAsync("""
                    CREATE UNIQUE INDEX IF NOT EXISTS IX_Participants_Unique
                    ON Participants (FirstName COLLATE NOCASE, LastName COLLATE NOCASE, Store COLLATE NOCASE);
                    """);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"IX_Participants_Unique: {ex.Message}");
            }

            // Index for winner lookups
            try
            {
                await _db.ExecuteAsync("""
                    CREATE INDEX IF NOT EXISTS IX_Winners_ParticipantId
                    ON Winners (ParticipantId);
                    """);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"IX_Winners_ParticipantId: {ex.Message}");
            }

            _initialized = true;
            Debug.WriteLine($"[DB] Initialized at: {DatabasePath}");
        }
        finally
        {
            _initLock.Release();
        }
    }

    private SQLiteAsyncConnection Db =>
        _initialized && _db is not null
            ? _db
            : throw new InvalidOperationException("Database not initialized. Call InitializeAsync first.");

    // ─── Participants ────────────────────────────────────────────────────────

    public Task<List<Participant>> GetAllParticipantsAsync() =>
        Db.Table<Participant>()
          .OrderBy(p => p.FirstName)
          .ToListAsync();

    public Task<List<Participant>> GetNonWinnerParticipantsAsync() =>
        Db.Table<Participant>()
          .Where(p => !p.IsWinner)
          .ToListAsync();

    public async Task<Participant?> GetParticipantByIdAsync(int id)
    {
        return await Db.Table<Participant>()
                       .Where(p => p.Id == id)
                       .FirstOrDefaultAsync();
    }

    public async Task<bool> ExistsDuplicateAsync(
        string firstName, string lastName, string store, int excludeId = 0)
    {
        var count = await Db.ExecuteScalarAsync<int>("""
            SELECT COUNT(*) FROM Participants
            WHERE FirstName = ? COLLATE NOCASE
              AND LastName  = ? COLLATE NOCASE
              AND Store     = ? COLLATE NOCASE
              AND Id        != ?
            """, firstName.Trim(), lastName.Trim(), store.Trim(), excludeId);
        return count > 0;
    }

    public async Task<int> InsertParticipantAsync(Participant participant)
    {
        participant.RegistrationDate = DateTime.UtcNow;
        await Db.InsertAsync(participant);
        return participant.Id;
    }

    public Task<int> UpdateParticipantAsync(Participant participant) =>
        Db.UpdateAsync(participant);

    public async Task DeleteParticipantAsync(int id)
    {
        await Db.ExecuteAsync("DELETE FROM Winners WHERE ParticipantId = ?", id);
        await Db.ExecuteAsync("DELETE FROM Participants WHERE Id = ?", id);
    }

    public Task<int> GetParticipantCountAsync() =>
        Db.Table<Participant>().CountAsync();

    public Task<int> GetNonWinnerCountAsync() =>
        Db.Table<Participant>().Where(p => !p.IsWinner).CountAsync();

    public Task<List<Participant>> SearchParticipantsAsync(string query) =>
        Db.QueryAsync<Participant>("""
            SELECT * FROM Participants
            WHERE FirstName LIKE ? OR LastName LIKE ? OR Store LIKE ?
            ORDER BY FirstName
            """, $"%{query}%", $"%{query}%", $"%{query}%");

    // ─── Winners ─────────────────────────────────────────────────────────────

    public async Task<List<Winner>> GetAllWinnersAsync()
    {
        var winners = await Db.Table<Winner>()
                               .OrderBy(w => w.PrizeNumber)
                               .ToListAsync();

        foreach (var w in winners)
            w.Participant = await GetParticipantByIdAsync(w.ParticipantId);

        return winners;
    }

    public Task<int> GetWinnerCountAsync() =>
        Db.Table<Winner>().CountAsync();

    public async Task<int> InsertWinnerAsync(Winner winner)
    {
        winner.DrawDate = DateTime.UtcNow;
        await Db.InsertAsync(winner);

        await Db.ExecuteAsync(
            "UPDATE Participants SET IsWinner = 1 WHERE Id = ?",
            winner.ParticipantId);

        return winner.Id;
    }

    public async Task ResetRaffleAsync()
    {
        await Db.ExecuteAsync("DELETE FROM Winners;");
        await Db.ExecuteAsync("UPDATE Participants SET IsWinner = 0;");
    }

    // ─── Disposal ────────────────────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        if (_db is not null)
        {
            await _db.CloseAsync();
            _db = null;
        }
    }
}