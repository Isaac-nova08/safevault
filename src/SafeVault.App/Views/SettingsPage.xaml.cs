using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using SafeVault.App.ViewModels;

namespace SafeVault.App.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();
    }

    public SettingsViewModel ViewModel { get; } = App.Services.GetRequiredService<SettingsViewModel>();
}
