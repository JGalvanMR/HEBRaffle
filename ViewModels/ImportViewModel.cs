using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HEBRaffle.Services;

namespace HEBRaffle.ViewModels;

public sealed partial class ImportViewModel : BaseViewModel
{
    private readonly IImportService     _import;
    private readonly INavigationService _nav;

    // ─── CanImport como propiedad pública ────────────────────────────────────
    public bool CanImport => HasFile && !IsBusy;

    // ─── State ───────────────────────────────────────────────────────────────
    [ObservableProperty] private string _selectedFileName  = "No file selected";
    [ObservableProperty] private bool   _hasFile;
    [ObservableProperty] private string _resultSummary    = string.Empty;
    [ObservableProperty] private bool   _showResult;
    [ObservableProperty] private bool   _resultIsSuccess;
    [ObservableProperty] private string _errorDetails     = string.Empty;
    [ObservableProperty] private bool   _hasErrorDetails;
    [ObservableProperty] private int    _importedCount;
    [ObservableProperty] private int    _skippedCount;
    [ObservableProperty] private int    _errorCount;
    [ObservableProperty] private string _progressMessage  = string.Empty;

    // ─── Template download state ──────────────────────────────────────────────
    [ObservableProperty] private bool   _isDownloadingTemplate;
    [ObservableProperty] private string _templateToast    = string.Empty;
    [ObservableProperty] private bool   _showTemplateToast;

    private string? _filePath;

    public ImportViewModel(IImportService import, INavigationService nav)
    {
        _import = import;
        _nav    = nav;
        Title   = "Import Participants";
    }

    // ─── Change notifications ─────────────────────────────────────────────────

    partial void OnHasFileChanged(bool value)
    {
        ImportCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanImport));
    }

    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName == nameof(IsBusy))
        {
            ImportCommand.NotifyCanExecuteChanged();
            base.OnPropertyChanged(
                new System.ComponentModel.PropertyChangedEventArgs(nameof(CanImport)));
        }
    }

    // ─── Download Template ────────────────────────────────────────────────────

    [RelayCommand]
    private async Task DownloadTemplateAsync()
    {
        if (IsDownloadingTemplate) return;

        try
        {
            IsDownloadingTemplate = true;

            var path = await _import.GenerateTemplateAsync();

            // Compartir via Share Sheet del OS
            await Share.RequestAsync(new ShareFileRequest
            {
                Title = "HEB_Participants_Template.xlsx",
                File  = new ShareFile(path)
            });

            ShowToast("Template ready — fill it in and come back to import!");
        }
        catch (Exception ex)
        {
            ShowToast($"Error: {ex.Message}");
        }
        finally
        {
            IsDownloadingTemplate = false;
        }
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

            ImportedCount   = result.Imported;
            SkippedCount    = result.Skipped;
            ErrorCount      = result.Errors;
            ResultSummary   = result.Summary;
            ResultIsSuccess = result.Imported > 0 || result.Skipped > 0;

            ErrorDetails    = result.ErrorDetails.Count > 0
                ? string.Join("\n", result.ErrorDetails)
                : string.Empty;
            HasErrorDetails = result.ErrorDetails.Count > 0;

            ShowResult      = true;
            ProgressMessage = string.Empty;

            if (result.Imported > 0)
            {
                await Task.Delay(2000);
                await _nav.NavigateToParticipantsAsync();
            }

        }, "Import failed");
    }

    // ─── Navigation ──────────────────────────────────────────────────────────

    [RelayCommand]
    private Task GoBackAsync() => _nav.GoBackAsync();

    // ─── Toast helper ─────────────────────────────────────────────────────────

    private void ShowToast(string msg)
    {
        TemplateToast     = msg;
        ShowTemplateToast = true;
        Task.Run(async () =>
        {
            await Task.Delay(3500);
            MainThread.BeginInvokeOnMainThread(() => ShowTemplateToast = false);
        });
    }
}