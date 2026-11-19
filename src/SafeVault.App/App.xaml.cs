using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using SafeVault.App.Services;
using SafeVault.App.ViewModels;
using SafeVault.Core.Services;
using SafeVault.Core.Storage;
using SafeVault.Data;

namespace SafeVault.App;

public partial class App : Application
{
    private MainWindow? _window;

    public App()
    {
        InitializeComponent();
        Services = ConfigureServices();
        UnhandledException += OnUnhandledException;
    }

    /// <summary>Conteneur d'injection de dépendances de l'application.</summary>
    public static IServiceProvider Services { get; private set; } = null!;

    /// <summary>Fenêtre principale (nécessaire pour rattacher les sélecteurs de fichiers).</summary>
    public static Window? CurrentWindow { get; private set; }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        CurrentWindow = _window;
        _window.Closed += (_, _) =>
        {
            // Fermeture de l'application : la clé de coffre est effacée de la mémoire
            // et un mot de passe encore présent dans le presse-papiers est retiré.
            Services.GetRequiredService<ClipboardService>().ClearIfOwned();
            Services.GetRequiredService<VaultService>().Lock();
        };
        _window.Activate();
    }

    /// <summary>
    /// Filet de sécurité : une erreur imprévue affiche un message neutre au lieu de fermer l'application.
    /// Aucun détail n'est journalisé ni affiché (il pourrait contenir des données du coffre).
    /// </summary>
    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        _ = Services.GetRequiredService<DialogService>().ShowAsync(new Microsoft.UI.Xaml.Controls.ContentDialog
        {
            Title = "Une erreur inattendue s'est produite",
            Content = "L'opération en cours a été annulée. Vos données enregistrées ne sont pas affectées. Si le problème persiste, redémarrez SafeVault.",
            CloseButtonText = "OK",
        });
    }

    private static ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Cœur (bibliothèques Core / Data)
        services.AddSingleton<IVaultStore>(_ => new SqliteVaultStore(GetDatabasePath()));
        services.AddSingleton(sp => new VaultService(sp.GetRequiredService<IVaultStore>()));
        services.AddSingleton(sp => new CredentialService(sp.GetRequiredService<VaultService>(), sp.GetRequiredService<IVaultStore>()));
        services.AddSingleton<SettingsService>();
        services.AddSingleton(sp => new BackupService(sp.GetRequiredService<CredentialService>()));

        // Interface
        services.AddSingleton<NavigationService>();
        services.AddSingleton<DialogService>();
        services.AddSingleton<ShellState>();
        services.AddSingleton<CredentialActions>();
        services.AddSingleton<ClipboardService>();
        services.AddSingleton<GeneratorPreferences>();
        services.AddSingleton<AutoLockService>();
        services.AddSingleton<FilePickerService>();
        services.AddTransient<GeneratorViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<CreateVaultViewModel>();
        services.AddTransient<UnlockViewModel>();
        services.AddTransient<ShellViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<CredentialsViewModel>();

        return services.BuildServiceProvider();
    }

    private static string GetDatabasePath()
    {
#if DEBUG
        // En développement uniquement : permet de tester avec un coffre jetable sans toucher au vrai coffre.
        var overridePath = Environment.GetEnvironmentVariable("SAFEVAULT_DEV_DB");
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            return overridePath;
        }
#endif
        return VaultPaths.DefaultDatabasePath;
    }
}
