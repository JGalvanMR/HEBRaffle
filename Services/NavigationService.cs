namespace HEBRaffle.Services;

public sealed class NavigationService : INavigationService
{
    public Task GoToAsync(string route, IDictionary<string, object>? parameters = null)
    {
        return parameters is null
            ? Shell.Current.GoToAsync(route)
            : Shell.Current.GoToAsync(route, parameters);
    }

    public Task GoBackAsync() =>
        Shell.Current.GoToAsync("..");

    public Task NavigateToDashboardAsync() =>
        GoToAsync("//DashboardPage");

    public Task NavigateToRegisterAsync() =>
        GoToAsync("RegisterParticipantPage");

    public Task NavigateToParticipantsAsync() =>
        GoToAsync("//ParticipantsPage");

    public Task NavigateToRaffleAsync() =>
        GoToAsync("//RafflePage");

    public Task NavigateToWinnersAsync() =>
        GoToAsync("//WinnersPage");
		
	public Task NavigateToImportAsync() =>
		GoToAsync("ImportPage");
}
