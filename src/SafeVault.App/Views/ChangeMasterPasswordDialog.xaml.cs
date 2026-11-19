using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SafeVault.App.ViewModels;

namespace SafeVault.App.Views;

public sealed partial class ChangeMasterPasswordDialog : ContentDialog
{
    public ChangeMasterPasswordDialog(ChangeMasterPasswordViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        Opened += (_, _) => CurrentInput.Focus(FocusState.Programmatic);
    }

    public ChangeMasterPasswordViewModel ViewModel { get; }

    private async void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        // La dérivation Argon2id est faite en arrière-plan ; la boîte reste ouverte en cas d'erreur.
        var deferral = args.GetDeferral();
        try
        {
            args.Cancel = !await ViewModel.SubmitAsync();
        }
        finally
        {
            deferral.Complete();
        }
    }
}
