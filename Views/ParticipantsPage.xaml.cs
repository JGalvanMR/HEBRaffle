using HEBRaffle.ViewModels;

namespace HEBRaffle.Views;

public partial class ParticipantsPage : ContentPage
{
    private readonly ParticipantsViewModel _vm;

    public ParticipantsPage(ParticipantsViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.OnAppearingAsync();
    }
}
