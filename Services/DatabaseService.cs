using HEBRaffle.Data;
using HEBRaffle.Models;
using Microsoft.Extensions.Logging;

namespace HEBRaffle.Services;

public sealed class DatabaseService : IDatabaseService
{
    private readonly AppDatabase _db;
    private readonly ILogger<DatabaseService> _logger;

    public DatabaseService(AppDatabase db, ILogger<DatabaseService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        try
        {
            await _db.InitializeAsync(ct);
            _logger.LogInformation("DatabaseService initialized.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize database.");
            throw;
        }
    }

    // DatabaseService.cs
    public async Task<int> ClearAllWinnersAsync()
    {
        await InitializeAsync();
        return await _db.DeleteAllAsync<Winner>();
    }

    public async Task<int> ClearAllParticipantsAsync()
    {
        await InitializeAsync();

        // Primero eliminar ganadores (dependen de participantes)
        await _db.DeleteAllAsync<Winner>();

        // Luego eliminar participantes
        return await _db.DeleteAllAsync<Participant>();
    }

    public async Task<int> ClearEverythingAsync()
    {
        await InitializeAsync();

        int totalDeleted = 0;
        totalDeleted += await _db.DeleteAllAsync<Winner>();
        totalDeleted += await _db.DeleteAllAsync<Participant>();

        return totalDeleted;
    }

    public async Task<(int Participants, int Winners)> GetCountsAsync()
    {
        await InitializeAsync();

        var participants = await _database.Table<Participant>().CountAsync();
        var winners = await _database.Table<Winner>().CountAsync();

        return (participants, winners);
    }

    // ─── Participants ────────────────────────────────────────────────────────

    public async Task<List<Participant>> GetAllParticipantsAsync()
    {
        try { return await _db.GetAllParticipantsAsync(); }
        catch (Exception ex) { _logger.LogError(ex, nameof(GetAllParticipantsAsync)); return []; }
    }

    public async Task<List<Participant>> GetNonWinnerParticipantsAsync()
    {
        try { return await _db.GetNonWinnerParticipantsAsync(); }
        catch (Exception ex) { _logger.LogError(ex, nameof(GetNonWinnerParticipantsAsync)); return []; }
    }

    public async Task<Participant?> GetParticipantByIdAsync(int id)
    {
        try { return await _db.GetParticipantByIdAsync(id); }
        catch (Exception ex) { _logger.LogError(ex, nameof(GetParticipantByIdAsync)); return null; }
    }

    public async Task<bool> ExistsDuplicateAsync(string firstName, string lastName, string store, int excludeId = 0)
    {
        try { return await _db.ExistsDuplicateAsync(firstName, lastName, store, excludeId); }
        catch (Exception ex) { _logger.LogError(ex, nameof(ExistsDuplicateAsync)); return false; }
    }

    public async Task<int> InsertParticipantAsync(Participant participant)
    {
        try
        {
            var id = await _db.InsertParticipantAsync(participant);
            _logger.LogInformation("Inserted participant {Id}: {Name}", id, participant.FullName);
            return id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Insert participant failed.");
            throw;
        }
    }

    public async Task<int> UpdateParticipantAsync(Participant participant)
    {
        try
        {
            var rows = await _db.UpdateParticipantAsync(participant);
            _logger.LogInformation("Updated participant {Id}", participant.Id);
            return rows;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Update participant failed.");
            throw;
        }
    }

    public async Task DeleteParticipantAsync(int id)
    {
        try
        {
            await _db.DeleteParticipantAsync(id);
            _logger.LogInformation("Deleted participant {Id}", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Delete participant failed.");
            throw;
        }
    }

    public Task<int> GetParticipantCountAsync()   => _db.GetParticipantCountAsync();
    public Task<int> GetNonWinnerCountAsync()      => _db.GetNonWinnerCountAsync();

    public async Task<List<Participant>> SearchParticipantsAsync(string query)
    {
        try { return await _db.SearchParticipantsAsync(query); }
        catch (Exception ex) { _logger.LogError(ex, nameof(SearchParticipantsAsync)); return []; }
    }

    // ─── Winners ─────────────────────────────────────────────────────────────

    public async Task<List<Winner>> GetAllWinnersAsync()
    {
        try { return await _db.GetAllWinnersAsync(); }
        catch (Exception ex) { _logger.LogError(ex, nameof(GetAllWinnersAsync)); return []; }
    }

    public Task<int> GetWinnerCountAsync() => _db.GetWinnerCountAsync();

    public async Task<int> InsertWinnerAsync(Winner winner)
    {
        try
        {
            var id = await _db.InsertWinnerAsync(winner);
            _logger.LogInformation("Winner recorded: ParticipantId={Pid} Prize#{Prize}", winner.ParticipantId, winner.PrizeNumber);
            return id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Insert winner failed.");
            throw;
        }
    }

    public async Task ResetRaffleAsync()
    {
        try
        {
            await _db.ResetRaffleAsync();
            _logger.LogInformation("Raffle reset.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Reset raffle failed.");
            throw;
        }
    }


}
