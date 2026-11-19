using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using SafeVault.App.ViewModels;
using SafeVault.Core.Services;
using Windows.System;

namespace SafeVault.App.Views;

public sealed partial class CreateVaultPage : Page
{
    public CreateVaultPage()
    {
        InitializeComponent();
        Loaded += (_, _) => PasswordInput.Focus(FocusState.Programmatic);
    }

    public CreateVaultViewModel ViewModel { get; } = App.Services.GetRequiredService<CreateVaultViewModel>();

    public static bool Not(bool value) => !value;

    /// <summary>Couleur de la jauge de force du Master Password.</summary>
    public static Brush StrengthBrush(PasswordStrength strength) => (Brush)Application.Current.Resources[strength switch
    {
        PasswordStrength.TooWeak => "SystemFillColorCriticalBrush",
        PasswordStrength.Weak => "SystemFillColorCautionBrush",
        PasswordStrength.Medium => "AccentFillColorDefaultBrush",
        _ => "SystemFillColorSuccessBrush",
    }];

    private void OnConfirmationKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && ViewModel.CreateCommand.CanExecute(null))
        {
            ViewModel.CreateCommand.Execute(null);
            e.Handled = true;
        }
    }
}
