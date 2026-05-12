using SQLite;

namespace HEBRaffle.Models;

[Table("Participants")]
public class Participant
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [NotNull, MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [NotNull, MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [NotNull, MaxLength(150)]
    public string Store { get; set; } = string.Empty;

    [NotNull]
    public int YearsInCompany { get; set; }

    public DateTime RegistrationDate { get; set; } = DateTime.UtcNow;

    public bool IsWinner { get; set; } = false;

    // ─── Computed (not stored) ────────────────────────────────────────────────
    [Ignore]
    public string FullName => $"{FirstName} {LastName}".Trim();

    [Ignore]
    public string RegistrationDateLocal =>
        RegistrationDate.ToLocalTime().ToString("MMM dd, yyyy  hh:mm tt");

    [Ignore]
    public string YearsLabel =>
        YearsInCompany == 1 ? "1 year" : $"{YearsInCompany} years";

    // Nueva propiedad para el Avatar/Inicial
    [Ignore]
    public string Initial => !string.IsNullOrWhiteSpace(FirstName)
                             ? FirstName[0].ToString().ToUpper()
                             : "?";
}
