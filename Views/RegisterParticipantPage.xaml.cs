using HEBRaffle.ViewModels;

namespace HEBRaffle.Views;

public partial class RegisterParticipantPage : ContentPage
{
    private readonly RegisterParticipantViewModel _vm;

    public RegisterParticipantPage(RegisterParticipantViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.OnAppearingAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _vm.ClearFormCommand.Execute(null);
    }
}
