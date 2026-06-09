using HEBRaffle.ViewModels;

#if IOS
using UIKit;
#endif

namespace HEBRaffle.Views;

public partial class DashboardPage : ContentPage
{
    private readonly DashboardViewModel _vm;

    public DashboardPage(DashboardViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.OnAppearingAsync();

        // Ajusta el padding del header DESPUÉS de que la página esté en la jerarquía de vistas,
        // momento en el que el sistema ya conoce los safe area insets reales del dispositivo.
        AdjustHeaderForSafeArea();
    }

    /// <summary>
    /// Calcula el inset superior real del dispositivo (status bar + Dynamic Island / notch)
    /// y lo aplica al padding del encabezado en iOS. En Android el padding XAML estático (52pt)
    /// ya es correcto y este método no hace nada.
    ///
    /// Valores de referencia:
    ///   iPhone SE / 8       → top ≈ 20 pt
    ///   iPhone X / 11–13    → top ≈ 44 pt
    ///   iPhone 14 Pro / 15+ → top ≈ 59 pt (Dynamic Island)
    /// </summary>
    private void AdjustHeaderForSafeArea()
    {
#if IOS
        try
        {
            double topInset = 0;

            // UIWindowScene API — compatible con iOS 13+ (sin deprecations)
            var scene = UIApplication.SharedApplication
                .ConnectedScenes
                .OfType<UIWindowScene>()
                .FirstOrDefault();

            if (scene is not null)
            {
                var keyWindow = scene.Windows.FirstOrDefault(w => w.IsKeyWindow)
                             ?? scene.Windows.FirstOrDefault();

                topInset = (double)(keyWindow?.SafeAreaInsets.Top ?? 0);
            }

            // Fallback defensivo: si no se obtuvo un inset válido (raro en producción),
            // usamos 44pt — mínimo seguro para todos los iPhone con Face ID.
            if (topInset < 1)
                topInset = 44;

            // 16pt de padding de diseño + el inset del sistema = separación confortable
            // entre el contenido y la barra de estado / Dynamic Island.
            HeaderGrid.Padding = new Thickness(24, topInset + 16, 24, 32);
        }
        catch (Exception ex)
        {
            // En caso de error inesperado, el padding XAML estático (52pt) sirve como fallback.
            System.Diagnostics.Debug.WriteLine($"[SafeArea] {ex.Message}");
        }
#endif
    }
}