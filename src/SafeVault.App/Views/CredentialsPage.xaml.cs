using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SafeVault.App.ViewModels;

namespace SafeVault.App.Views;

/// <summary>« Tous les identifiants » (paramètre <c>false</c>) ou « Favoris » (paramètre <c>true</c>).</summary>
public sealed partial class CredentialsPage : Page
{
    public CredentialsPage()
    {
        InitializeComponent();
    }

    public CredentialsViewModel ViewModel { get; } = App.Services.GetRequiredService<CredentialsViewModel>();

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.Activate(favoritesOnly: e.Parameter is true);
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        ViewModel.Deactivate();
    }

    private void OnItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is CredentialItemViewModel item)
        {
            item.EditCommand.Execute(null);
        }
    }
}
