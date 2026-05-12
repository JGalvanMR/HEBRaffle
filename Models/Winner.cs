using SQLite;

namespace HEBRaffle.Models;

[Table("Winners")]
public class Winner
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [NotNull]
    public int ParticipantId { get; set; }

    public DateTime DrawDate { get; set; } = DateTime.UtcNow;

    public int PrizeNumber { get; set; }

    // ─── Hydrated after query join (not stored) ───────────────────────────────
    [Ignore]
    public Participant? Participant { get; set; }

    [Ignore]
    public string DrawDateLocal =>
        DrawDate.ToLocalTime().ToString("MMM dd, yyyy  hh:mm tt");

    [Ignore]
    public string PrizeLabel => $"Prize #{PrizeNumber}";
}
