using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using SafeVault.App.ViewModels;
using Windows.System;

namespace SafeVault.App.Views;

public sealed partial class UnlockPage : Page
{
    public UnlockPage()
    {
        InitializeComponent();

        Loaded += (_, _) => PasswordInput.Focus(FocusState.Programmatic);
        ViewModel.FocusPasswordRequested += (_, _) => PasswordInput.Focus(FocusState.Programmatic);
    }

    public UnlockViewModel ViewModel { get; } = App.Services.GetRequiredService<UnlockViewModel>();

    public static bool Not(bool value) => !value;

    private void OnPasswordKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && ViewModel.UnlockCommand.CanExecute(null))
        {
            ViewModel.UnlockCommand.Execute(null);
            e.Handled = true;
        }
    }
}
