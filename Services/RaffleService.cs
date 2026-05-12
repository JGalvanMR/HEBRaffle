using HEBRaffle.Models;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

namespace HEBRaffle.Services;

/// <summary>
/// Uses <see cref="RandomNumberGenerator"/> (cryptographically secure) for fair draws.
/// Fisher-Yates shuffle ensures every permutation is equally likely.
/// </summary>
public sealed class RaffleService : IRaffleService
{
    private readonly IDatabaseService _db;
    private readonly ILogger<RaffleService> _logger;

    public RaffleService(IDatabaseService db, ILogger<RaffleService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public Task<int> GetEligibleCountAsync() => _db.GetNonWinnerCountAsync();

    public async Task<List<Winner>> DrawWinnersAsync(int count, CancellationToken ct = default)
    {
        if (count <= 0)
            throw new ArgumentOutOfRangeException(nameof(count), "Must draw at least 1 winner.");

        var eligible = await _db.GetNonWinnerParticipantsAsync();

        if (eligible.Count == 0)
        {
            _logger.LogWarning("No eligible participants for draw.");
            return [];
        }

        // Cap at available count
        int actualCount = Math.Min(count, eligible.Count);

        // Fisher-Yates cryptographic shuffle
        Shuffle(eligible);

        var drawn    = eligible.Take(actualCount).ToList();
        var winners  = new List<Winner>(actualCount);

        // Determine next prize number
        int existingWinners = await _db.GetWinnerCountAsync();

        for (int i = 0; i < drawn.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var winner = new Winner
            {
                ParticipantId = drawn[i].Id,
                PrizeNumber   = existingWinners + i + 1,
                DrawDate      = DateTime.UtcNow
            };

            await _db.InsertWinnerAsync(winner);
            winner.Participant = drawn[i];
            winners.Add(winner);

            _logger.LogInformation(
                "Winner drawn: {Name} | Prize #{Prize}",
                drawn[i].FullName,
                winner.PrizeNumber);
        }

        return winners;
    }

    public async Task ResetAsync(CancellationToken ct = default)
    {
        await _db.ResetRaffleAsync();
        _logger.LogInformation("Raffle reset completed.");
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static void Shuffle<T>(List<T> list)
    {
        int n = list.Count;
        while (n > 1)
        {
            // Unbiased random index in [0, n)
            int k = RandomNumberGenerator.GetInt32(n--);
            (list[n], list[k]) = (list[k], list[n]);
        }
    }
}
