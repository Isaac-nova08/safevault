using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using SafeVault.App.Views;

namespace SafeVault.App.Services;

/// <summary>
/// Navigation entre les trois écrans racine : création du coffre, déverrouillage et application.
/// L'historique est vidé à chaque changement pour qu'aucun retour arrière ne contourne le verrouillage.
/// </summary>
public sealed class NavigationService
{
    private Frame? _rootFrame;

    public void Initialize(Frame rootFrame) => _rootFrame = rootFrame;

    public void ShowCreateVault() => NavigateRoot(typeof(CreateVaultPage));

    public void ShowUnlock() => NavigateRoot(typeof(UnlockPage));

    public void ShowShell() => NavigateRoot(typeof(ShellPage));

    private void NavigateRoot(Type pageType)
    {
        if (_rootFrame is null)
        {
            throw new InvalidOperationException("La navigation n'est pas initialisée.");
        }

        if (_rootFrame.CurrentSourcePageType == pageType)
        {
            return;
        }

        _rootFrame.Navigate(pageType, null, new DrillInNavigationTransitionInfo());
        _rootFrame.BackStack.Clear();
    }
}
