using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using SafeVault.Core.Services;
using Windows.ApplicationModel.DataTransfer;

namespace SafeVault.App.Services;

/// <summary>
/// Copie de données sensibles dans le presse-papiers :
/// exclues de l'historique Windows (Win + V) et de la synchronisation cloud,
/// puis effacées automatiquement après le délai configuré.
/// </summary>
/// <remarks>
/// Le presse-papiers n'est effacé que s'il contient encore la valeur copiée par SafeVault
/// (numéro de séquence inchangé) : une copie faite entre-temps dans une autre application est préservée.
/// </remarks>
public sealed class ClipboardService(SettingsService settings)
{
    private DispatcherQueueTimer? _timer;
    private uint? _ownedSequence;

    /// <summary>Copie <paramref name="text"/> et programme son effacement.</summary>
    /// <returns>Délai avant effacement, en secondes.</returns>
    public int CopySecret(string text)
    {
        var package = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
        package.SetText(text);

        Clipboard.SetContentWithOptions(package, new ClipboardContentOptions
        {
            IsAllowedInHistory = false,
            IsRoamable = false,
        });

        _ownedSequence = GetClipboardSequenceNumber();

        var seconds = settings.Get().ClipboardClearSeconds;
        _timer ??= CreateTimer();
        _timer.Stop();
        _timer.Interval = TimeSpan.FromSeconds(seconds);
        _timer.Start();

        return seconds;
    }

    /// <summary>Efface le presse-papiers s'il contient encore une valeur copiée par SafeVault.</summary>
    public void ClearIfOwned()
    {
        _timer?.Stop();

        if (_ownedSequence is not { } owned)
        {
            return;
        }

        _ownedSequence = null;

        if (GetClipboardSequenceNumber() != owned)
        {
            return; // l'utilisateur a copié autre chose entre-temps
        }

        for (var attempt = 0; attempt < 5; attempt++)
        {
            if (OpenClipboard(IntPtr.Zero))
            {
                try
                {
                    EmptyClipboard();
                    return;
                }
                finally
                {
                    CloseClipboard();
                }
            }

            Thread.Sleep(50); // presse-papiers momentanément ouvert par une autre application
        }
    }

    private DispatcherQueueTimer CreateTimer()
    {
        var timer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        timer.IsRepeating = false;
        timer.Tick += (_, _) => ClearIfOwned();
        return timer;
    }

    [DllImport("user32.dll")]
    private static extern uint GetClipboardSequenceNumber();

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseClipboard();
}
