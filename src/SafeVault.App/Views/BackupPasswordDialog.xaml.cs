using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SafeVault.App.ViewModels;

namespace SafeVault.App.Views;

public sealed partial class BackupPasswordDialog : ContentDialog
{
    public BackupPasswordDialog(BackupPasswordViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        Opened += (_, _) => PasswordInput.Focus(FocusState.Programmatic);
    }

    public BackupPasswordViewModel ViewModel { get; }

    private async void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        // Dérivation Argon2id et chiffrement en arrière-plan ; la boîte reste ouverte en cas d'erreur.
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
