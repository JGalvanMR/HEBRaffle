using HEBRaffle.ViewModels;

namespace HEBRaffle.Views;

public partial class RafflePage : ContentPage
{
    private readonly RaffleViewModel _vm;
    private bool _pulseRunning;

    public RafflePage(RaffleViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.OnAppearingAsync();

        // Observe IsAnimating to drive the pulse animation on the border
        _vm.PropertyChanged += OnVmPropertyChanged;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _vm.PropertyChanged -= OnVmPropertyChanged;
        _pulseRunning = false;
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RaffleViewModel.IsAnimating))
        {
            if (_vm.IsAnimating && !_pulseRunning)
                _ = RunPulseAsync();
            else
                _pulseRunning = false;
        }
    }

    /// <summary>
    /// Drives a subtle scale pulse on the animation border while drawing.
    /// </summary>
    private async Task RunPulseAsync()
    {
        _pulseRunning = true;
        while (_pulseRunning && _vm.IsAnimating)
        {
            await AnimationBorder.ScaleTo(1.03, 180, Easing.CubicOut);
            await AnimationBorder.ScaleTo(1.00, 180, Easing.CubicIn);
        }
        AnimationBorder.Scale = 1.0;
    }
}
