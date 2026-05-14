using HEBRaffle.Views;

namespace HEBRaffle;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Register modal/push routes (not in TabBar)
        Routing.RegisterRoute("RegisterParticipantPage", typeof(RegisterParticipantPage));
		Routing.RegisterRoute("ImportPage", typeof(ImportPage));
    }
}
