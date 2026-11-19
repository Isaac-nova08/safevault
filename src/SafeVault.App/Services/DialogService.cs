using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace SafeVault.App.Services;

/// <summary>Affichage des boîtes de dialogue (une seule à la fois, comme l'exige WinUI).</summary>
public sealed class DialogService(AutoLockService autoLock)
{
    private XamlRoot? _xamlRoot;
    private ContentDialog? _current;

    public void Initialize(XamlRoot xamlRoot) => _xamlRoot = xamlRoot;

    public bool IsDialogOpen => _current is not null;

    /// <summary>Affiche une boîte de dialogue. Retourne <see cref="ContentDialogResult.None"/> si une autre est déjà ouverte.</summary>
    public async Task<ContentDialogResult> ShowAsync(ContentDialog dialog)
    {
        if (_xamlRoot is null || _current is not null)
        {
            return ContentDialogResult.None;
        }

        dialog.XamlRoot = _xamlRoot;
        dialog.Style ??= (Style)Application.Current.Resources["DefaultContentDialogStyle"];
        dialog.RequestedTheme = ElementTheme.Dark;

        // La saisie dans une boîte de dialogue compte comme de l'activité (verrouillage automatique).
        autoLock.Track(dialog);

        _current = dialog;
        try
        {
            return await dialog.ShowAsync();
        }
        finally
        {
            _current = null;
        }
    }

    /// <summary>Demande une confirmation. Retourne <c>true</c> si l'utilisateur valide.</summary>
    public async Task<bool> ConfirmAsync(string title, string message, string confirmText)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
            PrimaryButtonText = confirmText,
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Close,
        };

        return await ShowAsync(dialog) == ContentDialogResult.Primary;
    }

    /// <summary>Ferme la boîte de dialogue ouverte (par exemple au verrouillage du coffre).</summary>
    public void CloseCurrent() => _current?.Hide();
}
