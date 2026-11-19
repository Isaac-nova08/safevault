using Microsoft.Windows.Storage.Pickers;
using SafeVault.Core.Services;

namespace SafeVault.App.Services;

/// <summary>Sélection des fichiers de sauvegarde (boîtes de dialogue Windows standard).</summary>
public sealed class FilePickerService
{
    private const string FileTypeLabel = "Sauvegarde SafeVault";

    /// <summary>Demande où enregistrer la sauvegarde. Retourne <c>null</c> si l'utilisateur annule.</summary>
    public async Task<string?> PickBackupSaveLocationAsync(string suggestedName)
    {
        var picker = new FileSavePicker(WindowId)
        {
            SuggestedFileName = suggestedName,
            DefaultFileExtension = BackupService.FileExtension,
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
        };
        picker.FileTypeChoices.Add(FileTypeLabel, [BackupService.FileExtension]);

        var result = await picker.PickSaveFileAsync();
        return result?.Path;
    }

    /// <summary>Demande la sauvegarde à restaurer. Retourne <c>null</c> si l'utilisateur annule.</summary>
    public async Task<string?> PickBackupFileAsync()
    {
        var picker = new FileOpenPicker(WindowId)
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
        };
        picker.FileTypeFilter.Add(BackupService.FileExtension);

        var result = await picker.PickSingleFileAsync();
        return result?.Path;
    }

    private static Microsoft.UI.WindowId WindowId =>
        App.CurrentWindow?.AppWindow.Id ?? throw new InvalidOperationException("La fenêtre principale n'est pas prête.");
}
