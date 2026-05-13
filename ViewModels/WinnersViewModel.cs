using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HEBRaffle.Models;
using HEBRaffle.Services;
using System.Collections.ObjectModel;

namespace HEBRaffle.ViewModels;

public sealed partial class WinnersViewModel : BaseViewModel
{
    private readonly IDatabaseService  _db;
    private readonly IExportService    _export;
    private readonly INavigationService _nav;

    public ObservableCollection<Winner> Winners { get; } = [];

    [ObservableProperty] private int    _totalWinners;
    [ObservableProperty] private bool   _isEmpty;
    [ObservableProperty] private string _exportMessage   = string.Empty;
    [ObservableProperty] private bool   _showExportToast;

    public WinnersViewModel(
        IDatabaseService db,
        IExportService export,
        INavigationService nav)
    {
        _db     = db;
        _export = export;
        _nav    = nav;
        Title   = "Winners";
    }

    public override async Task OnAppearingAsync() => await LoadAsync();

    [RelayCommand]
    private async Task LoadAsync()
    {
        await ExecuteSafeAsync(async () =>
        {
            var winners = await _db.GetAllWinnersAsync();
            Winners.Clear();
            foreach (var w in winners)
                Winners.Add(w);

            TotalWinners = Winners.Count;
            IsEmpty      = TotalWinners == 0;
        });
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        await ExecuteSafeAsync(async () =>
        {
            var path = await _export.ExportWinnersAsync();
            ExportMessage   = $"Saved: {Path.GetFileName(path)}";
            ShowExportToast = true;

            _ = Task.Run(async () =>
            {
                await Task.Delay(3000);
                MainThread.BeginInvokeOnMainThread(() => ShowExportToast = false);
            });

            await Share.RequestAsync(new ShareFileRequest
            {
                Title = "Winners.xlsx",
                File  = new ShareFile(path)
            });
        }, "Export failed");
    }

    [RelayCommand]
    private Task GoToRaffleAsync() => _nav.NavigateToRaffleAsync();
}
