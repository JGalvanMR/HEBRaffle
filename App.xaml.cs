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
            System.Diagnostics.Debug.WriteLine($"[DB PATH]  {Data.AppDatabase.DatabasePath}");
            System.Diagnostics.Debug.WriteLine(ex.StackTrace);

            // MainThread garantiza que el DisplayAlert se ejecute en el hilo de UI.
            // La guarda de Windows.Count > 0 evita IndexOutOfRangeException en iOS
            // cuando OnStart se invoca antes de que la ventana esté completamente lista.
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                try
                {
                    var page = Windows.Count > 0 ? Windows[0].Page : null;
                    if (page is not null)
                    {
                        await page.DisplayAlert(
                            "Startup Error",
                            $"Failed to initialize the database.\n\n{ex.Message}",
                            "OK");
                    }
                }
                catch
                {
                    // Si tampoco se puede mostrar el alert, el Debug.WriteLine ya registró el error.
                }
            });
        }
    }
}