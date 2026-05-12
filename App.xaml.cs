using HEBRaffle.Services;

namespace HEBRaffle;

public partial class App : Application
{
    private readonly IDatabaseService _db;

    public App(IDatabaseService db)
    {
        InitializeComponent();
        _db = db;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell());
    }

    protected override async void OnStart()
    {
        base.OnStart();

        try
        {
            await _db.InitializeAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[App] DB init failed: {ex.Message}");

            if (Windows.Count > 0 && Windows[0].Page != null)
            {
                await Windows[0].Page.DisplayAlert(
                    "Startup Error",
                    "Failed to initialize the database. Please restart the app.",
                    "OK");
            }
        }
    }
}