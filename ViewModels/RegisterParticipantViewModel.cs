using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HEBRaffle.Models;
using HEBRaffle.Services;

namespace HEBRaffle.ViewModels;

[QueryProperty(nameof(EditParticipantId), "participantId")]
public sealed partial class RegisterParticipantViewModel : BaseViewModel
{
    private readonly IDatabaseService _db;
    private readonly INavigationService _nav;

    // ─── Form fields ─────────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _firstName = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _lastName = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _store = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _yearsInCompanyText = string.Empty;

    // ─── Validation messages ──────────────────────────────────────────────────
    [ObservableProperty] private string _firstNameError = string.Empty;
    [ObservableProperty] private string _lastNameError = string.Empty;
    [ObservableProperty] private string _storeError = string.Empty;
    [ObservableProperty] private string _yearsError = string.Empty;

    // ─── State ────────────────────────────────────────────────────────────────
    // NotifyPropertyChangedFor(SaveButtonText) → el botón actualiza su etiqueta
    // cuando cambia el modo (nuevo vs edición).
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SaveButtonText))]
    private bool _isEditMode;

    [ObservableProperty] private string _successMessage = string.Empty;
    [ObservableProperty] private bool _showSuccess;

    private int _editId;

    /// <summary>
    /// Etiqueta del botón principal.
    /// Reemplaza el binding roto <c>InvertBool → Text</c> que retornaba "True"/"False".
    /// </summary>
    public string SaveButtonText => IsEditMode ? "Save Changes" : "Register";

    public int EditParticipantId
    {
        set
        {
            _editId = value;
            IsEditMode = value > 0;
            Title = IsEditMode ? "Edit Participant" : "New Participant";
            if (IsEditMode) _ = LoadParticipantAsync(value);
        }
    }

    public RegisterParticipantViewModel(IDatabaseService db, INavigationService nav)
    {
        _db = db;
        _nav = nav;
        Title = "New Participant";
    }

    // ─── Load for edit ────────────────────────────────────────────────────────

    private async Task LoadParticipantAsync(int id)
    {
        var p = await _db.GetParticipantByIdAsync(id);
        if (p is null) return;

        FirstName = p.FirstName;
        LastName = p.LastName;
        Store = p.Store;
        YearsInCompanyText = p.YearsInCompany.ToString();
    }

    // ─── Save ────────────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        if (!Validate()) return;

        await ExecuteSafeAsync(async () =>
        {
            int years = int.Parse(YearsInCompanyText.Trim());

            bool isDuplicate = await _db.ExistsDuplicateAsync(
                FirstName.Trim(), LastName.Trim(), Store.Trim(), _editId);

            if (isDuplicate)
            {
                SetError("A participant with the same Name and Store already exists.");
                return;
            }

            if (IsEditMode)
            {
                var existing = await _db.GetParticipantByIdAsync(_editId);
                if (existing is null) { SetError("Participant not found."); return; }

                existing.FirstName = FirstName.Trim();
                existing.LastName = LastName.Trim();
                existing.Store = Store.Trim();
                existing.YearsInCompany = years;

                await _db.UpdateParticipantAsync(existing);
                ShowToast("Participant updated successfully.");
                await Task.Delay(1200);
                await _nav.GoBackAsync();
            }
            else
            {
                var participant = new Participant
                {
                    FirstName = FirstName.Trim(),
                    LastName = LastName.Trim(),
                    Store = Store.Trim(),
                    YearsInCompany = years,
                };

                await _db.InsertParticipantAsync(participant);
                ShowToast("Participant registered!");
                ClearForm();
            }
        }, "Save failed");
    }

    private bool CanSave() =>
        !string.IsNullOrWhiteSpace(FirstName) &&
        !string.IsNullOrWhiteSpace(LastName) &&
        !string.IsNullOrWhiteSpace(Store) &&
        !string.IsNullOrWhiteSpace(YearsInCompanyText) &&
        !IsBusy;

    // ─── Validation ───────────────────────────────────────────────────────────

    private bool Validate()
    {
        bool ok = true;

        FirstNameError = FirstName.Trim().Length < 2
            ? "First name must be at least 2 characters." : string.Empty;
        if (!string.IsNullOrEmpty(FirstNameError)) ok = false;

        LastNameError = LastName.Trim().Length < 2
            ? "Last name must be at least 2 characters." : string.Empty;
        if (!string.IsNullOrEmpty(LastNameError)) ok = false;

        StoreError = Store.Trim().Length < 2
            ? "Store must be at least 2 characters." : string.Empty;
        if (!string.IsNullOrEmpty(StoreError)) ok = false;

        if (!int.TryParse(YearsInCompanyText.Trim(), out int yrs) || yrs < 0 || yrs > 60)
        {
            YearsError = "Enter a valid number of years (0–60).";
            ok = false;
        }
        else
        {
            YearsError = string.Empty;
        }

        return ok;
    }

    // ─── Clear ───────────────────────────────────────────────────────────────

    [RelayCommand]
    private void ClearForm()
    {
        FirstName = LastName = Store = YearsInCompanyText = string.Empty;
        FirstNameError = LastNameError = StoreError = YearsError = string.Empty;
        ClearError();
    }

    // ─── Toast ────────────────────────────────────────────────────────────────

    private void ShowToast(string message)
    {
        SuccessMessage = message;
        ShowSuccess = true;
        Task.Run(async () =>
        {
            await Task.Delay(2500);
            MainThread.BeginInvokeOnMainThread(() => ShowSuccess = false);
        });
    }
}