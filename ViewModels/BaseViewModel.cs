using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace HEBRaffle.ViewModels;

/// <summary>
/// Base ViewModel providing common busy-state management and error handling.
/// </summary>
public abstract partial class BaseViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool _isBusy;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _hasError;

    public bool IsNotBusy => !IsBusy;

    protected void SetBusy(bool busy)
    {
        IsBusy = busy;
    }

    protected void SetError(string message)
    {
        ErrorMessage = message;
        HasError = !string.IsNullOrEmpty(message);
    }

    protected void ClearError() => SetError(string.Empty);

    /// <summary>
    /// Executes <paramref name="action"/> safely, setting IsBusy and catching exceptions.
    /// </summary>
    protected async Task ExecuteSafeAsync(Func<Task> action, string? errorContext = null)
    {
        if (IsBusy) return;

        try
        {
            ClearError();
            IsBusy = true;
            await action();
        }
        catch (OperationCanceledException)
        {
            // Ignore cancellation
        }
        catch (Exception ex)
        {
            var msg = errorContext is null ? ex.Message : $"{errorContext}: {ex.Message}";
            SetError(msg);
            System.Diagnostics.Debug.WriteLine($"[VM ERROR] {msg}\n{ex}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Called by Shell navigation when page appears.</summary>
    public virtual Task OnAppearingAsync() => Task.CompletedTask;
}
