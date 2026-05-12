using HEBRaffle.ViewModels;

namespace HEBRaffle.Views;

public partial class WinnersPage : ContentPage
{
    private readonly WinnersViewModel _vm;

    public WinnersPage(WinnersViewModel vm)
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
