using SafeVault.App.ViewModels;
using SafeVault.App.Views;
using SafeVault.Core.Models;
using SafeVault.Core.Services;

namespace SafeVault.App.Services;

/// <summary>Actions sur les identifiants partagées entre le tableau de bord et les listes.</summary>
public sealed class CredentialActions(
    CredentialService credentials,
    DialogService dialogs,
    ClipboardService clipboard,
    GeneratorPreferences generatorPreferences,
    ShellState shell)
{
    /// <summary>Ouvre le formulaire d'ajout.</summary>
    public Task AddAsync() => ShowEditorAsync(null);

    /// <summary>Ouvre le formulaire de modification.</summary>
    public Task EditAsync(Credential credential) => ShowEditorAsync(credential);

    public void ToggleFavorite(Credential credential) => credentials.SetFavorite(credential.Id, !credential.IsFavorite);

    public void CopyPassword(Credential credential)
    {
        var seconds = clipboard.CopySecret(credential.Password);
        shell.Notify($"Mot de passe de {credential.ServiceName} copié. Il sera effacé du presse-papiers dans {seconds} s.");
    }

    public void CopyUsername(Credential credential)
    {
        var seconds = clipboard.CopySecret(credential.Username);
        shell.Notify($"Identifiant copié. Il sera effacé du presse-papiers dans {seconds} s.");
    }

    /// <summary>Supprime l'identifiant après confirmation.</summary>
    public async Task DeleteAsync(Credential credential)
    {
        var confirmed = await dialogs.ConfirmAsync(
            $"Supprimer « {credential.ServiceName} » ?",
            $"L'identifiant {credential.Username} sera supprimé définitivement du coffre.",
            "Supprimer");

        if (confirmed)
        {
            credentials.Delete(credential.Id);
        }
    }

    private async Task ShowEditorAsync(Credential? existing)
    {
        var viewModel = new CredentialEditorViewModel(credentials, generatorPreferences, existing);
        await dialogs.ShowAsync(new CredentialEditorDialog(viewModel));
    }
}
