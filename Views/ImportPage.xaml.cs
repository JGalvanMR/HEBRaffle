// ════════════════════════════════════════════════════════════════════════════
// FILE: Views/ImportPage.xaml.cs
// ════════════════════════════════════════════════════════════════════════════
/*
using HEBRaffle.ViewModels;

namespace HEBRaffle.Views;

public partial class ImportPage : ContentPage
{
    private readonly ImportViewModel _vm;

    public ImportPage(ImportViewModel vm)
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
*/

// ════════════════════════════════════════════════════════════════════════════
// CAMBIOS EN: MauiProgram.cs
// Agregar DESPUÉS de los servicios existentes:
// ════════════════════════════════════════════════════════════════════════════
/*
    builder.Services.AddSingleton<IImportService, ImportService>();
    builder.Services.AddTransient<ImportViewModel>();
    builder.Services.AddTransient<ImportPage>();
*/

// ════════════════════════════════════════════════════════════════════════════
// CAMBIOS EN: AppShell.xaml.cs
// Agregar en el constructor, junto a RegisterParticipantPage:
// ════════════════════════════════════════════════════════════════════════════
/*
    Routing.RegisterRoute("ImportPage", typeof(ImportPage));
*/

// ════════════════════════════════════════════════════════════════════════════
// CAMBIOS EN: Services/NavigationService.cs
// Agregar este método:
// ════════════════════════════════════════════════════════════════════════════
/*
    public Task NavigateToImportAsync() =>
        GoToAsync("ImportPage");
*/

// ════════════════════════════════════════════════════════════════════════════
// CAMBIOS EN: Services/IServices.cs — Agregar a INavigationService:
// ════════════════════════════════════════════════════════════════════════════
/*
    Task NavigateToImportAsync();
*/

// ════════════════════════════════════════════════════════════════════════════
// CAMBIOS EN: ViewModels/DashboardViewModel.cs
// Agregar inyección de INavigationService (ya la tiene) y este comando:
// ════════════════════════════════════════════════════════════════════════════
/*
    [RelayCommand]
    private Task NavigateToImportAsync() => _nav.NavigateToImportAsync();
*/

// ════════════════════════════════════════════════════════════════════════════
// CAMBIOS EN: Platforms/Android/AndroidManifest.xml
// Agregar permiso de lectura de almacenamiento externo:
// ════════════════════════════════════════════════════════════════════════════
/*
    <uses-permission android:name="android.permission.READ_EXTERNAL_STORAGE"
                     android:maxSdkVersion="32" />
    <uses-permission android:name="android.permission.READ_MEDIA_IMAGES" />

    <!-- Para Android 13+ el FilePicker usa el sistema SAF, no necesita permisos adicionales -->
*/

// ════════════════════════════════════════════════════════════════════════════
// CAMBIOS EN: Views/DashboardPage.xaml
// Agregar tarjeta de Import en el Action Grid (Row 1 → Row 2, o añadir Row 2)
// ════════════════════════════════════════════════════════════════════════════
/*
    <!-- Agrega Row="2" al Grid y añade esta tarjeta que ocupa las 2 columnas -->

    <!-- Import — full width -->
    <Border Grid.Row="2" Grid.Column="0" Grid.ColumnSpan="2"
            Style="{StaticResource Card}"
            Padding="0">
        <Border.GestureRecognizers>
            <TapGestureRecognizer Command="{Binding NavigateToImportCommand}" />
        </Border.GestureRecognizers>
        <Grid RowDefinitions="Auto,*" RowSpacing="0">
            <BoxView Grid.Row="0"
                     HeightRequest="6"
                     CornerRadius="16,16,0,0"
                     Color="#7B1FA2" />
            <Grid Grid.Row="1"
                  ColumnDefinitions="Auto,*"
                  ColumnSpacing="16"
                  Padding="20,16">
                <Label Grid.Column="0"
                       Text="📂"
                       FontSize="32"
                       VerticalOptions="Center" />
                <VerticalStackLayout Grid.Column="1" Spacing="4" VerticalOptions="Center">
                    <Label Text="Import from Excel"
                           FontSize="17"
                           FontAttributes="Bold"
                           TextColor="{StaticResource OnSurface900}" />
                    <Label Text="Load participants from .xlsx file"
                           Style="{StaticResource CaptionText}" />
                </VerticalStackLayout>
            </Grid>
        </Grid>
    </Border>
*/
