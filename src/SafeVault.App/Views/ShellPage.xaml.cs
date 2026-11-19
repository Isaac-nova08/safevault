using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using SafeVault.App.Services;
using SafeVault.App.ViewModels;

namespace SafeVault.App.Views;

public sealed partial class ShellPage : Page
{
    private ShellSection? _currentSection;
    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _notificationTimer;

    public ShellPage()
    {
        InitializeComponent();
        Loaded += (_, _) => NavView.SelectedItem ??= DashboardItem;
    }

    public ShellViewModel ViewModel { get; } = App.Services.GetRequiredService<ShellViewModel>();

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.State.NavigationRequested += OnNavigationRequested;
        ViewModel.State.NotificationRequested += OnNotificationRequested;
        ViewModel.Activate();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        ViewModel.State.NavigationRequested -= OnNavigationRequested;
        ViewModel.State.NotificationRequested -= OnNotificationRequested;
        ViewModel.Deactivate();
        _notificationTimer?.Stop();

        // Décharge la page affichée pour qu'elle libère ses données.
        ContentFrame.Content = null;
    }

    private void OnNavigationRequested(object? sender, ShellSection section) => NavView.SelectedItem = ItemFor(section);

    private void OnSearchAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        SearchBox.Focus(FocusState.Keyboard);
        args.Handled = true;
    }

    private void OnLockAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        ViewModel.LockCommand.Execute(null);
        args.Handled = true;
    }

    private void OnOpenSettings(object sender, RoutedEventArgs e) => NavView.SelectedItem = SettingsItem;

    private void OnNotificationRequested(object? sender, string message)
    {
        Notification.Message = message;
        Notification.IsOpen = true;

        if (_notificationTimer is null)
        {
            _notificationTimer = DispatcherQueue.CreateTimer();
            _notificationTimer.IsRepeating = false;
            _notificationTimer.Interval = TimeSpan.FromSeconds(4);
            _notificationTimer.Tick += (_, _) => Notification.IsOpen = false;
        }

        _notificationTimer.Stop();
        _notificationTimer.Start();
    }

    private void OnItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.InvokedItemContainer?.Tag as string == "Lock")
        {
            ViewModel.LockCommand.Execute(null);
        }
    }

    private void OnSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer?.Tag is not string tag || !Enum.TryParse<ShellSection>(tag, out var section))
        {
            return;
        }

        if (section == _currentSection)
        {
            return;
        }

        _currentSection = section;

        var transition = new EntranceNavigationTransitionInfo();
        switch (section)
        {
            case ShellSection.Dashboard:
                ContentFrame.Navigate(typeof(DashboardPage), null, transition);
                break;
            case ShellSection.AllCredentials:
                ContentFrame.Navigate(typeof(CredentialsPage), false, transition);
                break;
            case ShellSection.Favorites:
                ContentFrame.Navigate(typeof(CredentialsPage), true, transition);
                break;
            case ShellSection.Generator:
                ContentFrame.Navigate(typeof(GeneratorPage), null, transition);
                break;
            case ShellSection.Settings:
                ContentFrame.Navigate(typeof(SettingsPage), null, transition);
                break;
        }
    }

    /// <summary>Une recherche lancée depuis un écran sans liste affiche « Tous les identifiants ».</summary>
    private void OnSearchTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput || string.IsNullOrWhiteSpace(sender.Text))
        {
            return;
        }

        if (_currentSection is not (ShellSection.AllCredentials or ShellSection.Favorites))
        {
            NavView.SelectedItem = AllItem;
        }
    }

    private NavigationViewItem ItemFor(ShellSection section) => section switch
    {
        ShellSection.AllCredentials => AllItem,
        ShellSection.Favorites => FavoritesItem,
        ShellSection.Generator => GeneratorItem,
        ShellSection.Settings => SettingsItem,
        _ => DashboardItem,
    };
}
