using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SafeVault.App.Services;
using SafeVault.Core;
using SafeVault.Core.Services;

namespace SafeVault.App.ViewModels;

/// <summary>Premier lancement : définition du Master Password et création du coffre.</summary>
public sealed partial class CreateVaultViewModel(VaultService vault, NavigationService navigation) : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Strength), nameof(StrengthValue), nameof(StrengthLabel), nameof(PolicyHint), nameof(MismatchHint))]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    public partial string Password { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MismatchHint))]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    public partial string Confirmation { get; set; } = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    public partial bool RiskAcknowledged { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? ErrorMessage { get; set; }

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public PasswordStrength Strength => MasterPasswordPolicy.Evaluate(Password);

    /// <summary>Valeur de la jauge (0 à 4).</summary>
    public double StrengthValue => Password.Length == 0 ? 0 : (int)Strength + 1;

    public string StrengthLabel => Password.Length == 0
        ? ""
        : Strength switch
        {
            PasswordStrength.TooWeak => "Trop faible",
            PasswordStrength.Weak => "Faible",
            PasswordStrength.Medium => "Moyen",
            _ => "Fort",
        };

    /// <summary>Première règle non respectée, affichée sous le champ.</summary>
    public string PolicyHint => Password.Length == 0
        ? $"Au moins {MasterPasswordPolicy.MinLength} caractères, avec majuscules, minuscules, chiffres et symboles, " +
          $"ou une phrase de passe d'au moins {MasterPasswordPolicy.PassphraseLength} caractères."
        : MasterPasswordPolicy.Validate(Password).FirstOrDefault() ?? "";

    public string MismatchHint => Confirmation.Length > 0 && Confirmation != Password
        ? "Les deux mots de passe ne correspondent pas."
        : "";

    private bool CanCreate() =>
        !IsBusy
        && RiskAcknowledged
        && Password.Length > 0
        && Password == Confirmation
        && MasterPasswordPolicy.Validate(Password).Count == 0;

    [RelayCommand(CanExecute = nameof(CanCreate))]
    private async Task CreateAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        var password = Password;

        try
        {
            // La dérivation Argon2id prend quelques centaines de ms : hors du thread d'interface.
            await Task.Run(() => vault.Create(password));

            Password = "";
            Confirmation = "";
            navigation.ShowShell();
        }
        catch (VaultValidationException ex)
        {
            ErrorMessage = string.Join(Environment.NewLine, ex.Errors);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Le coffre n'a pas pu être créé : {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
