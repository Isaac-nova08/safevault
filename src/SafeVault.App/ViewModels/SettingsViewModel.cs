using System.Diagnostics;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SafeVault.App.Services;
using SafeVault.App.Views;
using SafeVault.Core;
using SafeVault.Core.Models;
using SafeVault.Core.Services;
using SafeVault.Core.Storage;
using SafeVault.Data;
using SafeVault.Security;
using Microsoft.UI.Xaml.Controls;

namespace SafeVault.App.ViewModels;

/// <summary>Valeur proposée dans une liste de délais.</summary>
public sealed record DelayOption(int Value, string Label);

/// <summary>Page Paramètres : sécurité, presse-papiers, Master Password et informations.</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settings;
    private readonly VaultService _vault;
    private readonly DialogService _dialogs;
    private readonly ShellState _shell;
    private readonly BackupService _backups;
    private readonly CredentialService _credentials;
    private readonly FilePickerService _pickers;
    private bool _loading = true;

    public SettingsViewModel(
        SettingsService settings,
        VaultService vault,
        DialogService dialogs,
        ShellState shell,
        IVaultStore store,
        BackupService backups,
        CredentialService credentials,
        FilePickerService pickers)
    {
        _settings = settings;
        _vault = vault;
        _dialogs = dialogs;
        _shell = shell;
        _backups = backups;
        _credentials = credentials;
        _pickers = pickers;

        DatabasePath = store is SqliteVaultStore sqlite ? sqlite.DatabasePath : VaultPaths.DefaultDatabasePath;
        Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

        var current = settings.Get();
        SelectedAutoLock = Pick(AutoLockOptions, current.AutoLockMinutes);
        SelectedClipboardDelay = Pick(ClipboardOptions, current.ClipboardClearSeconds);
        _loading = false;
    }

    public IReadOnlyList<DelayOption> AutoLockOptions { get; } =
    [
        new(1, "1 minute"),
        new(2, "2 minutes"),
        new(5, "5 minutes"),
        new(10, "10 minutes"),
        new(15, "15 minutes"),
        new(30, "30 minutes"),
        new(60, "1 heure"),
    ];

    public IReadOnlyList<DelayOption> ClipboardOptions { get; } =
    [
        new(10, "10 secondes"),
        new(20, "20 secondes"),
        new(30, "30 secondes"),
        new(60, "1 minute"),
        new(120, "2 minutes"),
    ];

    [ObservableProperty]
    public partial DelayOption? SelectedAutoLock { get; set; }

    [ObservableProperty]
    public partial DelayOption? SelectedClipboardDelay { get; set; }

    public string DatabasePath { get; }

    public string Version { get; }

    partial void OnSelectedAutoLockChanged(DelayOption? value) => Save();

    partial void OnSelectedClipboardDelayChanged(DelayOption? value) => Save();

    [RelayCommand]
    private async Task ChangeMasterPasswordAsync()
    {
        var viewModel = new ChangeMasterPasswordViewModel(_vault);
        if (await _dialogs.ShowAsync(new ChangeMasterPasswordDialog(viewModel)) == ContentDialogResult.Primary)
        {
            _shell.Notify("Master Password modifié. Utilisez-le dès le prochain déverrouillage.");
        }
    }

    /// <summary>Export : emplacement, puis mot de passe de la sauvegarde ; le fichier n'est écrit qu'une fois chiffré.</summary>
    [RelayCommand]
    private async Task ExportBackupAsync()
    {
        var path = await _pickers.PickBackupSaveLocationAsync($"SafeVault-{DateTime.Now:yyyy-MM-dd}");
        if (path is null)
        {
            return;
        }

        var viewModel = new BackupPasswordViewModel(isExport: true, Path.GetFileName(path), async password =>
        {
            try
            {
                await Task.Run(() => WriteFileAtomically(path, _backups.Export(password)));
                return null;
            }
            catch (VaultValidationException ex)
            {
                return string.Join(Environment.NewLine, ex.Errors);
            }
            catch (VaultLockedException)
            {
                return "Le coffre a été verrouillé.";
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return $"Impossible d'écrire le fichier : {ex.Message}";
            }
        });

        if (await _dialogs.ShowAsync(new BackupPasswordDialog(viewModel)) == ContentDialogResult.Primary)
        {
            _shell.Notify($"Sauvegarde chiffrée exportée : {Path.GetFileName(path)}");
        }
    }

    /// <summary>
    /// Restauration : le fichier est entièrement vérifié et déchiffré, puis l'utilisateur confirme le remplacement.
    /// Le coffre n'est modifié qu'à la toute fin, en une seule transaction.
    /// </summary>
    [RelayCommand]
    private async Task RestoreBackupAsync()
    {
        var path = await _pickers.PickBackupFileAsync();
        if (path is null)
        {
            return;
        }

        BackupContents? contents = null;
        var viewModel = new BackupPasswordViewModel(isExport: false, Path.GetFileName(path), async password =>
        {
            try
            {
                contents = await Task.Run(() =>
                {
                    if (new FileInfo(path).Length > BackupProtector.MaxFileSize)
                    {
                        throw new BackupException("Le fichier est trop volumineux pour être une sauvegarde SafeVault.");
                    }

                    return _backups.Open(File.ReadAllBytes(path), password);
                });
                return null;
            }
            catch (BackupException ex)
            {
                return ex.Message;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return $"Impossible de lire le fichier : {ex.Message}";
            }
        });

        if (await _dialogs.ShowAsync(new BackupPasswordDialog(viewModel)) != ContentDialogResult.Primary || contents is null)
        {
            return;
        }

        var message =
            $"Sauvegarde du {contents.ExportedAt.ToLocalTime():dd/MM/yyyy à HH:mm} : intégrité vérifiée, {Plural(contents.Credentials.Count)}.{Environment.NewLine}{Environment.NewLine}" +
            $"Le contenu actuel du coffre ({Plural(_credentials.Count)}) sera remplacé. Cette action est irréversible.";

        if (!await _dialogs.ConfirmAsync("Restaurer cette sauvegarde ?", message, "Remplacer le contenu"))
        {
            return;
        }

        var restored = contents;
        try
        {
            await Task.Run(() => _backups.Restore(restored));
            _shell.Notify($"Sauvegarde restaurée : {Plural(restored.Credentials.Count)}.");
        }
        catch (BackupException ex)
        {
            _shell.Notify($"Restauration annulée, le coffre n'a pas été modifié. {ex.Message}");
        }
        catch (VaultLockedException)
        {
            // Le coffre s'est verrouillé entre-temps : rien n'a été modifié.
        }
    }

    [RelayCommand]
    private void OpenVaultFolder()
    {
        if (File.Exists(DatabasePath))
        {
            Process.Start("explorer.exe", $"/select,\"{DatabasePath}\"");
        }
    }

    private void Save()
    {
        if (_loading || SelectedAutoLock is null || SelectedClipboardDelay is null)
        {
            return;
        }

        try
        {
            _settings.Save(new VaultSettings
            {
                AutoLockMinutes = SelectedAutoLock.Value,
                ClipboardClearSeconds = SelectedClipboardDelay.Value,
            });
            _shell.Notify("Paramètres enregistrés.");
        }
        catch (VaultValidationException ex)
        {
            _shell.Notify(string.Join(" ", ex.Errors));
        }
    }

    private static string Plural(int count) => count switch
    {
        0 => "aucun identifiant",
        1 => "1 identifiant",
        _ => $"{count} identifiants",
    };

    /// <summary>Écrit d'abord un fichier temporaire : une sauvegarde existante n'est jamais laissée à moitié écrite.</summary>
    private static void WriteFileAtomically(string path, byte[] content)
    {
        var temp = path + ".tmp";
        File.WriteAllBytes(temp, content);
        File.Move(temp, path, overwrite: true);
    }

    /// <summary>Option correspondant à la valeur enregistrée (ou la plus proche).</summary>
    private static DelayOption Pick(IReadOnlyList<DelayOption> options, int value) =>
        options.OrderBy(o => Math.Abs(o.Value - value)).First();
}
