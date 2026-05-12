using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HEBRaffle.Models;
using HEBRaffle.Services;
using System.Collections.ObjectModel;

namespace HEBRaffle.ViewModels;

/// <summary>
/// Drives the raffle screen.
/// Animation: a IDispatcherTimer cycles through random participant names rapidly,
/// decelerating until it lands on the drawn winner.
/// </summary>
public sealed partial class RaffleViewModel : BaseViewModel, IDisposable
{
    private readonly IRaffleService    _raffle;
    private readonly IDatabaseService  _db;
    private readonly IExportService    _export;
    private readonly INavigationService _nav;

    // ─── Configuration ────────────────────────────────────────────────────────
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(CanDraw))]
    private int _numberOfWinners = 1;

    [ObservableProperty] private int    _eligibleCount;
    [ObservableProperty] private int    _currentWinnerCount;
    [ObservableProperty] private string _statusMessage = "Ready to draw";

    // ─── Animation ────────────────────────────────────────────────────────────
    [ObservableProperty] private string  _animatedName  = "---";
    [ObservableProperty] private bool    _isAnimating;
    [ObservableProperty] private bool    _showResult;
    [ObservableProperty] private string  _resultName    = string.Empty;
    [ObservableProperty] private string  _resultStore   = string.Empty;
    [ObservableProperty] private int     _resultPrize;

    // ─── Winners list ─────────────────────────────────────────────────────────
    public ObservableCollection<Winner> SessionWinners { get; } = [];

    // ─── Internal animation state ─────────────────────────────────────────────
    private IDispatcherTimer?     _animTimer;
    private List<string>          _namePool     = [];
    private List<Winner>          _pendingWinners = [];
    private int                   _pendingIndex;
    private int                   _animTick;
    private const int             AnimTotalTicks = 40;   // total frames
    private const int             AnimFastMs     = 40;   // ms per frame (fast)
    private const int             AnimSlowMs     = 220;  // ms per frame (slow)

    private CancellationTokenSource _cts = new();

    public bool CanDraw =>
        !IsBusy              &&
        !IsAnimating         &&
        EligibleCount > 0    &&
        NumberOfWinners >= 1 &&
        NumberOfWinners <= Math.Max(1, EligibleCount);

    public RaffleViewModel(
        IRaffleService raffle,
        IDatabaseService db,
        IExportService export,
        INavigationService nav)
    {
        _raffle = raffle;
        _db     = db;
        _export = export;
        _nav    = nav;
        Title   = "Raffle Draw";
    }

    public override async Task OnAppearingAsync()
    {
        await RefreshStateAsync();
        await LoadSessionWinnersAsync();
    }

    // ─── Refresh ──────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task RefreshStateAsync()
    {
        EligibleCount      = await _raffle.GetEligibleCountAsync();
        CurrentWinnerCount = await _db.GetWinnerCountAsync();

        StatusMessage = EligibleCount == 0
            ? "No eligible participants"
            : $"{EligibleCount} eligible · {CurrentWinnerCount} winner(s) so far";

        OnPropertyChanged(nameof(CanDraw));
    }

    private async Task LoadSessionWinnersAsync()
    {
        var winners = await _db.GetAllWinnersAsync();
        SessionWinners.Clear();
        foreach (var w in winners)
            SessionWinners.Add(w);
    }

    // ─── Draw ─────────────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(CanDraw))]
    private async Task DrawAsync()
    {
        if (!CanDraw) return;

        _cts = new CancellationTokenSource();

        await ExecuteSafeAsync(async () =>
        {
            ShowResult = false;

            // Draw winners from service
            _pendingWinners = await _raffle.DrawWinnersAsync(NumberOfWinners, _cts.Token);

            if (_pendingWinners.Count == 0)
            {
                StatusMessage = "Not enough eligible participants.";
                return;
            }

            // Build name pool for animation from DB
            var all = await _db.GetAllParticipantsAsync();
            _namePool = all
                .Where(p => !_pendingWinners.Any(w => w.ParticipantId == p.Id))
                .Select(p => p.FullName)
                .OrderBy(_ => Guid.NewGuid())
                .ToList();

            // Add winner names to pool too (animation will still show them)
            _namePool.AddRange(_pendingWinners
                .Select(w => w.Participant?.FullName ?? "---"));

            if (_namePool.Count == 0)
                _namePool = ["---"];

            _pendingIndex = 0;
            StartAnimation();
        }, "Draw failed");
    }

    // ─── Animation Engine ─────────────────────────────────────────────────────

    private void StartAnimation()
    {
        IsAnimating = true;
        OnPropertyChanged(nameof(CanDraw));
        DrawCommand.NotifyCanExecuteChanged();

        _animTick = 0;

        _animTimer = Application.Current!.Dispatcher.CreateTimer();
        _animTimer.Interval = TimeSpan.FromMilliseconds(AnimFastMs);
        _animTimer.Tick    += OnAnimTick;
        _animTimer.Start();
    }

    private void OnAnimTick(object? sender, EventArgs e)
    {
        _animTick++;

        // Progress ratio [0..1]: how close to end
        double ratio = (double)_animTick / AnimTotalTicks;

        // Show random name during animation
        if (ratio < 0.85)
        {
            var rnd = _namePool[Random.Shared.Next(_namePool.Count)];
            AnimatedName = rnd;

            // Gradually slow down timer interval
            if (ratio > 0.5)
            {
                var ms = (int)Lerp(AnimFastMs, AnimSlowMs, (ratio - 0.5) * 2.0);
                _animTimer!.Interval = TimeSpan.FromMilliseconds(ms);
            }
        }
        else
        {
            // Show the actual winner name in the last few frames
            if (_pendingIndex < _pendingWinners.Count)
            {
                var winner = _pendingWinners[_pendingIndex];
                AnimatedName = winner.Participant?.FullName ?? "---";
            }
        }

        if (_animTick >= AnimTotalTicks)
        {
            _animTimer?.Stop();
            _ = RevealNextWinnerAsync();
        }
    }

    private async Task RevealNextWinnerAsync()
    {
        if (_pendingIndex >= _pendingWinners.Count)
        {
            FinishAnimation();
            return;
        }

        var winner = _pendingWinners[_pendingIndex];
        _pendingIndex++;

        // Reveal current winner
        ResultName  = winner.Participant?.FullName  ?? "---";
        ResultStore = winner.Participant?.Store     ?? "";
        ResultPrize = winner.PrizeNumber;
        ShowResult  = true;
        AnimatedName = ResultName;

        // Update local collection
        SessionWinners.Add(winner);

        // If more winners to reveal, wait and continue animation
        if (_pendingIndex < _pendingWinners.Count)
        {
            await Task.Delay(1800);
            ShowResult = false;
            await Task.Delay(400);

            _animTick = 0;
            _animTimer!.Interval = TimeSpan.FromMilliseconds(AnimFastMs);
            _animTimer.Start();
        }
        else
        {
            FinishAnimation();
        }
    }

    private void FinishAnimation()
    {
        IsAnimating = false;
        ShowResult  = true;
        DrawCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanDraw));

        _ = RefreshStateAsync();
    }

    // ─── Increment / Decrement winner count ───────────────────────────────────

    [RelayCommand]
    private void IncrementWinners()
    {
        if (NumberOfWinners < 30 && NumberOfWinners < EligibleCount)
            NumberOfWinners++;
        OnPropertyChanged(nameof(CanDraw));
    }

    [RelayCommand]
    private void DecrementWinners()
    {
        if (NumberOfWinners > 1)
            NumberOfWinners--;
        OnPropertyChanged(nameof(CanDraw));
    }

    // ─── Reset ────────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task ResetRaffleAsync()
    {
        bool confirmed = await Shell.Current.DisplayAlert(
            "Reset Raffle",
            "This will clear ALL winners and reset all participants. Are you sure?",
            "Yes, Reset", "Cancel");

        if (!confirmed) return;

        await ExecuteSafeAsync(async () =>
        {
            await _raffle.ResetAsync();
            SessionWinners.Clear();
            ShowResult      = false;
            AnimatedName    = "---";
            NumberOfWinners = 1;
            await RefreshStateAsync();
        });
    }

    // ─── Export ──────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task ExportWinnersAsync()
    {
        await ExecuteSafeAsync(async () =>
        {
            var path = await _export.ExportWinnersAsync();
            await Share.RequestAsync(new ShareFileRequest
            {
                Title = "Winners.xlsx",
                File  = new ShareFile(path)
            });
        }, "Export failed");
    }

    // ─── Navigate to winners ──────────────────────────────────────────────────

    [RelayCommand]
    private Task ViewWinnersAsync() => _nav.NavigateToWinnersAsync();

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static double Lerp(double a, double b, double t) =>
        a + (b - a) * Math.Clamp(t, 0, 1);

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        _animTimer?.Stop();
    }
}
