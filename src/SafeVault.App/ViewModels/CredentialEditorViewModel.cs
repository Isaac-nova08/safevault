using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SafeVault.App.Services;
using SafeVault.Core;
using SafeVault.Core.Models;
using SafeVault.Core.Services;
using SafeVault.Security;

namespace SafeVault.App.ViewModels;

/// <summary>Formulaire d'ajout ou de modification d'un identifiant.</summary>
public sealed partial class CredentialEditorViewModel : ObservableObject
{
    private readonly CredentialService _credentials;
    private readonly GeneratorPreferences _generatorPreferences;
    private readonly Credential? _existing;

    public CredentialEditorViewModel(CredentialService credentials, GeneratorPreferences generatorPreferences, Credential? existing)
    {
        _credentials = credentials;
        _generatorPreferences = generatorPreferences;
        _existing = existing;

        Categories = credentials.GetCategories();

        ServiceName = existing?.ServiceName ?? "";
        Username = existing?.Username ?? "";
        Password = existing?.Password ?? "";
        Url = existing?.Url ?? "";
        Note = existing?.Note ?? "";
        IsFavorite = existing?.IsFavorite ?? false;
        SelectedCategory = Categories.FirstOrDefault(c => c.Id == (existing?.CategoryId ?? Category.OtherId)) ?? Categories.FirstOrDefault();
    }

    public bool IsNew => _existing is null;

    public string Title => IsNew ? "Nouvel identifiant" : "Modifier l'identifiant";

    public IReadOnlyList<Category> Categories { get; }

    [ObservableProperty]
    public partial string ServiceName { get; set; }

    [ObservableProperty]
    public partial string Username { get; set; }

    [ObservableProperty]
    public partial string Password { get; set; }

    [ObservableProperty]
    public partial string Url { get; set; }

    [ObservableProperty]
    public partial string Note { get; set; }

    [ObservableProperty]
    public partial Category? SelectedCategory { get; set; }

    [ObservableProperty]
    public partial bool IsFavorite { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? ErrorMessage { get; set; }

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    /// <summary>Remplit le mot de passe avec les derniers réglages du générateur.</summary>
    [RelayCommand]
    private void GeneratePassword()
    {
        var options = _generatorPreferences.Options;
        if (!(options.IncludeUppercase || options.IncludeLowercase || options.IncludeDigits || options.IncludeSymbols))
        {
            options = new PasswordGeneratorOptions { Length = options.Length };
        }

        Password = PasswordGenerator.Generate(options);
    }

    /// <summary>Enregistre le formulaire. Retourne <c>false</c> (et affiche les erreurs) si la saisie est invalide.</summary>
    public bool TrySave()
    {
        var draft = new CredentialDraft
        {
            ServiceName = ServiceName,
            Username = Username,
            Password = Password,
            Url = Url,
            Note = Note,
            CategoryId = SelectedCategory?.Id ?? Category.OtherId,
            IsFavorite = IsFavorite,
        };

        try
        {
            if (_existing is null)
            {
                _credentials.Add(draft);
            }
            else
            {
                _credentials.Update(_existing.Id, draft);
            }

            ErrorMessage = null;
            Password = "";
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
    }
}
