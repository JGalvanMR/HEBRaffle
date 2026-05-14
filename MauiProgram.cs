using CommunityToolkit.Maui;
using HEBRaffle.Data;
using HEBRaffle.Services;
using HEBRaffle.ViewModels;
using HEBRaffle.Views;
using Microsoft.Extensions.Logging;

namespace HEBRaffle;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-SemiBold.ttf", "OpenSansSemiBold");
                fonts.AddFont("OpenSans-Bold.ttf", "OpenSansBold");
            });

        // ── Logging ───────────────────────────────────────────────────────────
#if DEBUG
        builder.Logging.AddDebug();
#endif

        // ── Data Layer ────────────────────────────────────────────────────────
        builder.Services.AddSingleton<AppDatabase>();

        // ── Services ──────────────────────────────────────────────────────────
        builder.Services.AddSingleton<IDatabaseService, DatabaseService>();
        builder.Services.AddSingleton<IRaffleService, RaffleService>();
        builder.Services.AddSingleton<IExportService, ExportService>();
        builder.Services.AddSingleton<INavigationService, NavigationService>();

        // ── ViewModels ────────────────────────────────────────────────────────
        builder.Services.AddTransient<DashboardViewModel>();
        builder.Services.AddTransient<RegisterParticipantViewModel>();
        builder.Services.AddTransient<ParticipantsViewModel>();
        builder.Services.AddSingleton<RaffleViewModel>();   // singleton = preserves animation state
        builder.Services.AddTransient<WinnersViewModel>();

        // ── Views ─────────────────────────────────────────────────────────────
        builder.Services.AddTransient<DashboardPage>();
        builder.Services.AddTransient<RegisterParticipantPage>();
        builder.Services.AddTransient<ParticipantsPage>();
        builder.Services.AddSingleton<RafflePage>();
        builder.Services.AddTransient<WinnersPage>();
		
		builder.Services.AddSingleton<IImportService, ImportService>();
builder.Services.AddTransient<ImportViewModel>();
builder.Services.AddTransient<ImportPage>();

        return builder.Build();
    }
}
