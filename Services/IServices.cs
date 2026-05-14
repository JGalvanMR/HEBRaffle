using HEBRaffle.Models;

namespace HEBRaffle.Services;

// ════════════════════════════════════════════════════════════════════════════
// IDatabaseService
// ════════════════════════════════════════════════════════════════════════════

public interface IDatabaseService
{
    Task InitializeAsync(CancellationToken ct = default);

    // Participants
    Task<List<Participant>> GetAllParticipantsAsync();
    Task<List<Participant>> GetNonWinnerParticipantsAsync();
    Task<Participant?> GetParticipantByIdAsync(int id);
    Task<bool> ExistsDuplicateAsync(string firstName, string lastName, string store, int excludeId = 0);
    Task<int> InsertParticipantAsync(Participant participant);
    Task<int> UpdateParticipantAsync(Participant participant);
    Task DeleteParticipantAsync(int id);
    Task<int> GetParticipantCountAsync();
    Task<int> GetNonWinnerCountAsync();
    Task<List<Participant>> SearchParticipantsAsync(string query);

    // Winners
    Task<List<Winner>> GetAllWinnersAsync();
    Task<int> GetWinnerCountAsync();
    Task<int> InsertWinnerAsync(Winner winner);
    Task ResetRaffleAsync();
}

// ════════════════════════════════════════════════════════════════════════════
// IRaffleService
// ════════════════════════════════════════════════════════════════════════════

public interface IRaffleService
{
    /// <summary>
    /// Draws <paramref name="count"/> unique winners from eligible participants.
    /// Returns empty list if not enough eligible participants exist.
    /// </summary>
    Task<List<Winner>> DrawWinnersAsync(int count, CancellationToken ct = default);

    /// <summary>Deletes all raffle data and resets winner flags.</summary>
    Task ResetAsync(CancellationToken ct = default);

    /// <summary>Returns how many non-winner participants are available.</summary>
    Task<int> GetEligibleCountAsync();
}

// ════════════════════════════════════════════════════════════════════════════
// IExportService
// ════════════════════════════════════════════════════════════════════════════

public interface IExportService
{
    /// <summary>Exports all participants to Participants.xlsx and returns the file path.</summary>
    Task<string> ExportParticipantsAsync(CancellationToken ct = default);

    /// <summary>Exports all winners to Winners.xlsx and returns the file path.</summary>
    Task<string> ExportWinnersAsync(CancellationToken ct = default);
}

// ════════════════════════════════════════════════════════════════════════════
// INavigationService
// ════════════════════════════════════════════════════════════════════════════

public interface INavigationService
{
    Task GoToAsync(string route, IDictionary<string, object>? parameters = null);
    Task GoBackAsync();
    Task NavigateToDashboardAsync();
    Task NavigateToRegisterAsync();
    Task NavigateToParticipantsAsync();
    Task NavigateToRaffleAsync();
    Task NavigateToWinnersAsync();
	Task NavigateToImportAsync();
}
