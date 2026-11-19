using CommunityToolkit.Mvvm.ComponentModel;
using SafeVault.Core;
using SafeVault.Core.Services;

namespace SafeVault.App.ViewModels;

/// <summary>Changement du Master Password (la clé de coffre est ré-enrobée, les entrées ne sont pas rechiffrées).</summary>
public sealed partial class ChangeMasterPasswordViewModel(VaultService vault) : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSubmit))]
    public partial string CurrentPassword { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSubmit), nameof(Strength), nameof(StrengthValue), nameof(StrengthLabel), nameof(PolicyHint), nameof(MismatchHint))]
    public partial string NewPassword { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSubmit), nameof(MismatchHint))]
    public partial string Confirmation { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSubmit))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? ErrorMessage { get; set; }

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public PasswordStrength Strength => MasterPasswordPolicy.Evaluate(NewPassword);

    public double StrengthValue => NewPassword.Length == 0 ? 0 : (int)Strength + 1;

    public string StrengthLabel => NewPassword.Length == 0
        ? ""
        : Strength switch
        {
            PasswordStrength.TooWeak => "Trop faible",
            PasswordStrength.Weak => "Faible",
            PasswordStrength.Medium => "Moyen",
            _ => "Fort",
        };

    public string PolicyHint => NewPassword.Length == 0 ? "" : MasterPasswordPolicy.Validate(NewPassword).FirstOrDefault() ?? "";

    public string MismatchHint => Confirmation.Length > 0 && Confirmation != NewPassword
        ? "Les deux mots de passe ne correspondent pas."
        : "";

    public bool CanSubmit =>
        !IsBusy
        && CurrentPassword.Length > 0
        && NewPassword.Length > 0
        && NewPassword == Confirmation
        && MasterPasswordPolicy.Validate(NewPassword).Count == 0;

    /// <summary>Applique le changement. Retourne <c>false</c> (et affiche l'erreur) en cas d'échec.</summary>
    public async Task<bool> SubmitAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        var (current, next) = (CurrentPassword, NewPassword);

        try
        {
            await Task.Run(() => vault.ChangeMasterPassword(current, next));
            CurrentPassword = NewPassword = Confirmation = "";
            return true;
        }
        catch (VaultValidationException ex)
        {
            ErrorMessage = string.Join(Environment.NewLine, ex.Errors);
            return false;
        }
        catch (VaultLockedException)
        {
            ErrorMessage = "Le coffre a été verrouillé.";
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
