using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HEBRaffle.Services;

namespace HEBRaffle.ViewModels;

public sealed partial class DashboardViewModel : BaseViewModel
{
    private readonly IDatabaseService _db;
    private readonly INavigationService _nav;

    [ObservableProperty] private int _totalParticipants;
    [ObservableProperty] private int _totalWinners;
    [ObservableProperty] private int _eligibleParticipants;
    [ObservableProperty] private string _eventStatus = "Registration Open";

    public DashboardViewModel(IDatabaseService db, INavigationService nav)
    {
        _db = db;
        _nav = nav;
        Title = "HEB Raffle — Dashboard";
    }

    public override async Task OnAppearingAsync()
    {
        await LoadStatsAsync();
    }

    [RelayCommand]
    private async Task LoadStatsAsync()
    {
        await ExecuteSafeAsync(async () =>
        {
            TotalParticipants = await _db.GetParticipantCountAsync();
            TotalWinners = await _db.GetWinnerCountAsync();
            EligibleParticipants = await _db.GetNonWinnerCountAsync();

            EventStatus = TotalWinners > 0
                ? $"Raffle in progress — {TotalWinners} winner(s)"
                : "Registration Open";
        });
    }

    // ─── Navigation ──────────────────────────────────────────────────────────

    [RelayCommand]
    private Task NavigateToRegisterAsync() => _nav.NavigateToRegisterAsync();

    [RelayCommand]
    private Task NavigateToParticipantsAsync() => _nav.NavigateToParticipantsAsync();

    [RelayCommand]
    private Task NavigateToRaffleAsync() => _nav.NavigateToRaffleAsync();

    [RelayCommand]
    private Task NavigateToWinnersAsync() => _nav.NavigateToWinnersAsync();

    [RelayCommand]
    private Task NavigateToImportAsync() => _nav.NavigateToImportAsync();
}
