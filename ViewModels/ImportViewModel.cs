using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HEBRaffle.Services;

namespace HEBRaffle.ViewModels;

public sealed partial class ImportViewModel : BaseViewModel
{
    private readonly IImportService    _import;
    private readonly INavigationService _nav;
	public bool CanImport => HasFile && !IsBusy;

    // ─── State ───────────────────────────────────────────────────────────────
    [ObservableProperty] private string  _selectedFileName  = "No file selected";
    [ObservableProperty] private bool    _hasFile;
    [ObservableProperty] private string  _resultSummary     = string.Empty;
    [ObservableProperty] private bool    _showResult;
    [ObservableProperty] private bool    _resultIsSuccess;
    [ObservableProperty] private string  _errorDetails      = string.Empty;
    [ObservableProperty] private bool    _hasErrorDetails;
    [ObservableProperty] private int     _importedCount;
    [ObservableProperty] private int     _skippedCount;
    [ObservableProperty] private int     _errorCount;
    [ObservableProperty] private string  _progressMessage   = string.Empty;

    private string? _filePath;

    public ImportViewModel(IImportService import, INavigationService nav)
    {
        _import = import;
        _nav    = nav;
        Title   = "Import Participants";
    }

    // ─── Pick file ────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task PickFileAsync()
    {
        var path = await _import.PickExcelFileAsync();
        if (path is null) return;

        _filePath        = path;
        SelectedFileName = Path.GetFileName(path);
        HasFile          = true;
        ShowResult       = false;
        ResultSummary    = string.Empty;
        ErrorDetails     = string.Empty;
        HasErrorDetails  = false;
    }

    // ─── Import ───────────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(CanImport))]
    private async Task ImportAsync()
    {
        if (_filePath is null) return;

        await ExecuteSafeAsync(async () =>
        {
            ShowResult      = false;
            ProgressMessage = "Reading Excel file…";

            var result = await _import.ImportFromExcelAsync(_filePath);

            ImportedCount  = result.Imported;
            SkippedCount   = result.Skipped;
            ErrorCount     = result.Errors;
            ResultSummary  = result.Summary;
            ResultIsSuccess = result.Imported > 0 || result.Skipped > 0;

            if (result.ErrorDetails.Count > 0)
            {
                ErrorDetails    = string.Join("\n", result.ErrorDetails);
                HasErrorDetails = true;
            }
            else
            {
                ErrorDetails    = string.Empty;
                HasErrorDetails = false;
            }

            ShowResult      = true;
            ProgressMessage = string.Empty;

            // Si importó exitosamente, navegar a participantes después de 2s
            if (result.Imported > 0)
            {
                await Task.Delay(2000);
                await _nav.NavigateToParticipantsAsync();
            }

        }, "Import failed");
    }

partial void OnIsBusyChanged(bool value)
{
    ImportCommand.NotifyCanExecuteChanged();
    OnPropertyChanged(nameof(CanImport));
}

    private bool CanImport() => HasFile && !IsBusy;

    partial void OnHasFileChanged(bool value)
{
    ImportCommand.NotifyCanExecuteChanged();
    OnPropertyChanged(nameof(CanImport));
}

    // ─── Navigation ──────────────────────────────────────────────────────────

    [RelayCommand]
    private Task GoBackAsync() => _nav.GoBackAsync();
}
