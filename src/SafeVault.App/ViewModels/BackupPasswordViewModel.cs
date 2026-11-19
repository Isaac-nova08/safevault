using CommunityToolkit.Mvvm.ComponentModel;
using SafeVault.Core.Services;

namespace SafeVault.App.ViewModels;

/// <summary>
/// Saisie du mot de passe d'une sauvegarde : choix d'un nouveau mot de passe (export)
/// ou saisie du mot de passe existant (restauration).
/// </summary>
/// <param name="isExport">Export : mot de passe + confirmation, soumis à la même politique que le Master Password.</param>
/// <param name="submit">Opération à exécuter avec le mot de passe ; retourne un message d'erreur ou <c>null</c> si elle a réussi.</param>
public sealed partial class BackupPasswordViewModel(bool isExport, string fileName, Func<string, Task<string?>> submit) : ObservableObject
{
    public bool IsExport { get; } = isExport;

    public bool IsRestore => !IsExport;

    public string FileName { get; } = fileName;

    public string Title => IsExport ? "Exporter une sauvegarde chiffrée" : "Restaurer une sauvegarde";

    public string PrimaryText => IsExport ? "Exporter" : "Vérifier";

    public string Explanation => IsExport
        ? "Choisissez le mot de passe qui protégera ce fichier. Il sera demandé pour le restaurer ; il peut être différent de votre Master Password. Sans lui, la sauvegarde est définitivement illisible."
        : "Saisissez le mot de passe choisi lors de l'export. L'intégrité du fichier est vérifiée avant toute modification du coffre.";

    public string PasswordHeader => "Mot de passe de la sauvegarde";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSubmit), nameof(Strength), nameof(StrengthValue), nameof(StrengthLabel), nameof(PolicyHint), nameof(MismatchHint))]
    public partial string Password { get; set; } = "";

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

    public PasswordStrength Strength => MasterPasswordPolicy.Evaluate(Password);

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

    public string PolicyHint => Password.Length == 0 ? "" : MasterPasswordPolicy.Validate(Password).FirstOrDefault() ?? "";

    public string MismatchHint => Confirmation.Length > 0 && Confirmation != Password
        ? "Les deux mots de passe ne correspondent pas."
        : "";

    public bool CanSubmit => !IsBusy && Password.Length > 0 && (IsRestore
        || (Password == Confirmation && MasterPasswordPolicy.Validate(Password).Count == 0));

    /// <summary>Exécute l'opération. Retourne <c>false</c> (et affiche l'erreur) en cas d'échec.</summary>
    public async Task<bool> SubmitAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            ErrorMessage = await submit(Password);
            if (ErrorMessage is null)
            {
                Password = Confirmation = "";
                return true;
            }

            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
