using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using SafeVault.App.Services;
using SafeVault.Core.Services;
using Windows.Graphics;

namespace SafeVault.App;

public sealed partial class MainWindow : Window
{
    private const int DefaultWidth = 1200;
    private const int DefaultHeight = 780;

    private readonly VaultService _vault;
    private readonly NavigationService _navigation;
    private readonly DialogService _dialogs;
    private readonly ShellState _shellState;

    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        ConfigureWindowSize();
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "SafeVault.ico"));

        _vault = App.Services.GetRequiredService<VaultService>();
        _navigation = App.Services.GetRequiredService<NavigationService>();
        _navigation.Initialize(RootFrame);

        _dialogs = App.Services.GetRequiredService<DialogService>();
        _shellState = App.Services.GetRequiredService<ShellState>();
        RootFrame.Loaded += (_, _) => _dialogs.Initialize(RootFrame.XamlRoot);

        var autoLock = App.Services.GetRequiredService<AutoLockService>();
        autoLock.Track(RootFrame);
        autoLock.Start();

        // Dès que le coffre est verrouillé (bouton, raccourci ou inactivité), retour à l'écran de déverrouillage.
        _vault.StateChanged += OnVaultStateChanged;
        Closed += (_, _) => _vault.StateChanged -= OnVaultStateChanged;

        if (_vault.VaultExists)
        {
            _navigation.ShowUnlock();
        }
        else
        {
            _navigation.ShowCreateVault();
        }
    }

    private void OnVaultStateChanged(object? sender, EventArgs e)
    {
        if (!_vault.IsUnlocked)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                // Un formulaire ouvert est fermé et la recherche effacée avant d'afficher l'écran de déverrouillage.
                _dialogs.CloseCurrent();
                _shellState.Reset();
                App.Services.GetRequiredService<ClipboardService>().ClearIfOwned();
                _navigation.ShowUnlock();
            });
        }
    }

    /// <summary>Taille initiale adaptée à la mise à l'échelle de l'écran, fenêtre centrée.</summary>
    private void ConfigureWindowSize()
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var scale = GetDpiForWindow(hwnd) / 96.0;

        var width = (int)(DefaultWidth * scale);
        var height = (int)(DefaultHeight * scale);

        var workArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        width = Math.Min(width, workArea.Width);
        height = Math.Min(height, workArea.Height);

        AppWindow.MoveAndResize(new RectInt32(
            workArea.X + (workArea.Width - width) / 2,
            workArea.Y + (workArea.Height - height) / 2,
            width,
            height));

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = (int)(800 * scale);
            presenter.PreferredMinimumHeight = (int)(560 * scale);
        }
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);
}
