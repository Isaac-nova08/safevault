using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SafeVault.App.ViewModels;

namespace SafeVault.App.Views;

/// <summary>Formulaire d'ajout / modification d'un identifiant.</summary>
public sealed partial class CredentialEditorDialog : ContentDialog
{
    public CredentialEditorDialog(CredentialEditorViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        Opened += (_, _) => ServiceNameInput.Focus(FocusState.Programmatic);
    }

    public CredentialEditorViewModel ViewModel { get; }

    private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        // Saisie invalide : la boîte reste ouverte et affiche les erreurs.
        if (!ViewModel.TrySave())
        {
            args.Cancel = true;
        }
    }
}
