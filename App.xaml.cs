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
        System.Diagnostics.Debug.WriteLine($"[DB ERROR] {ex.GetType().Name}: {ex.Message}");
        System.Diagnostics.Debug.WriteLine($"[DB PATH] {Data.AppDatabase.DatabasePath}");
        System.Diagnostics.Debug.WriteLine(ex.StackTrace);

        await (Windows[0].Page?.DisplayAlert(
            "Startup Error",
            $"DB Error: {ex.Message}",
            "OK") ?? Task.CompletedTask);
    }
}
}