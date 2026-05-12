using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HEBRaffle.Models;
using HEBRaffle.Services;
using System.Collections.ObjectModel;

namespace HEBRaffle.ViewModels;

public sealed partial class ParticipantsViewModel : BaseViewModel
{
    private readonly IDatabaseService  _db;
    private readonly INavigationService _nav;
    private readonly IExportService    _export;

    // ─── Collections ─────────────────────────────────────────────────────────
    public ObservableCollection<Participant> Participants { get; } = [];

    // ─── State ───────────────────────────────────────────────────────────────
    [ObservableProperty] private string  _searchQuery     = string.Empty;
    [ObservableProperty] private int     _totalCount;
    [ObservableProperty] private bool    _isEmpty;
    [ObservableProperty] private string  _sortLabel       = "Sort: Name";
    [ObservableProperty] private string  _exportMessage   = string.Empty;
    [ObservableProperty] private bool    _showExportToast;

    private SortMode _currentSort = SortMode.Name;
    private List<Participant> _allParticipants = [];

    public ParticipantsViewModel(
        IDatabaseService db,
        INavigationService nav,
        IExportService export)
    {
        _db     = db;
        _nav    = nav;
        _export = export;
        Title   = "Participants";
    }

    public override async Task OnAppearingAsync() => await LoadAsync();

    // ─── Load / Search ───────────────────────────────────────────────────────

    [RelayCommand]
    private async Task LoadAsync()
    {
        await ExecuteSafeAsync(async () =>
        {
            _allParticipants = await _db.GetAllParticipantsAsync();
            ApplyFilterAndSort();
        });
    }

    partial void OnSearchQueryChanged(string value) => ApplyFilterAndSort();

    private void ApplyFilterAndSort()
    {
        var filtered = string.IsNullOrWhiteSpace(SearchQuery)
            ? _allParticipants
            : _allParticipants
                .Where(p =>
                    p.FullName.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                    p.Store.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase))
                .ToList();

        var sorted = _currentSort switch
        {
            SortMode.Name     => filtered.OrderBy(p => p.FirstName).ThenBy(p => p.LastName),
            SortMode.Store    => filtered.OrderBy(p => p.Store).ThenBy(p => p.FirstName),
            SortMode.Seniority => filtered.OrderByDescending(p => p.YearsInCompany),
            _                 => filtered.OrderBy(p => p.FirstName)
        };

        Participants.Clear();
        foreach (var p in sorted)
            Participants.Add(p);

        TotalCount = Participants.Count;
        IsEmpty    = TotalCount == 0;
    }

    // ─── Sort ────────────────────────────────────────────────────────────────

    [RelayCommand]
    private void CycleSort()
    {
        _currentSort = _currentSort switch
        {
            SortMode.Name      => SortMode.Store,
            SortMode.Store     => SortMode.Seniority,
            SortMode.Seniority => SortMode.Name,
            _                  => SortMode.Name
        };

        SortLabel = _currentSort switch
        {
            SortMode.Name      => "Sort: Name",
            SortMode.Store     => "Sort: Store",
            SortMode.Seniority => "Sort: Seniority",
            _                  => "Sort: Name"
        };

        ApplyFilterAndSort();
    }

    // ─── Edit / Delete ───────────────────────────────────────────────────────

    [RelayCommand]
    private async Task EditParticipantAsync(Participant participant)
    {
        await _nav.GoToAsync("RegisterParticipantPage",
            new Dictionary<string, object> { ["participantId"] = participant.Id });
    }

    [RelayCommand]
    private async Task DeleteParticipantAsync(Participant participant)
    {
        bool confirmed = await Shell.Current.DisplayAlert(
            "Delete Participant",
            $"Remove {participant.FullName} from the list? This cannot be undone.",
            "Delete", "Cancel");

        if (!confirmed) return;

        await ExecuteSafeAsync(async () =>
        {
            await _db.DeleteParticipantAsync(participant.Id);
            _allParticipants.Remove(participant);
            ApplyFilterAndSort();
        });
    }

    // ─── Export ──────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task ExportAsync()
    {
        await ExecuteSafeAsync(async () =>
        {
            var path = await _export.ExportParticipantsAsync();
            ShowExport($"Saved: {Path.GetFileName(path)}");
            await ShareFileAsync(path, "Participants.xlsx");
        }, "Export failed");
    }

    private void ShowExport(string msg)
    {
        ExportMessage   = msg;
        ShowExportToast = true;
        Task.Run(async () =>
        {
            await Task.Delay(3000);
            MainThread.BeginInvokeOnMainThread(() => ShowExportToast = false);
        });
    }

    private static async Task ShareFileAsync(string path, string fileName)
    {
        if (!File.Exists(path)) return;

        await Share.RequestAsync(new ShareFileRequest
        {
            Title = fileName,
            File  = new ShareFile(path)
        });
    }

    // ─── Enums ───────────────────────────────────────────────────────────────

    private enum SortMode { Name, Store, Seniority }
}
