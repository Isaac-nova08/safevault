using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using SafeVault.Core.Services;

namespace SafeVault.App.Services;

/// <summary>
/// Verrouillage automatique : le coffre est verrouillé après le délai configuré
/// sans aucune action (souris, clavier) dans SafeVault.
/// </summary>
public sealed class AutoLockService(VaultService vault, SettingsService settings)
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(5);

    private DispatcherQueueTimer? _timer;
    private long _lastActivity = Environment.TickCount64;

    /// <summary>Démarre la surveillance (à appeler depuis le thread d'interface).</summary>
    public void Start()
    {
        if (_timer is not null)
        {
            return;
        }

        _timer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _timer.Interval = CheckInterval;
        _timer.IsRepeating = true;
        _timer.Tick += (_, _) => Check();
        _timer.Start();
    }

    /// <summary>
    /// Écoute l'activité sur un élément (fenêtre, boîte de dialogue),
    /// y compris les événements déjà traités par les contrôles.
    /// </summary>
    public void Track(UIElement element)
    {
        element.AddHandler(UIElement.PointerMovedEvent, new PointerEventHandler((_, _) => RegisterActivity()), true);
        element.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler((_, _) => RegisterActivity()), true);
        element.AddHandler(UIElement.PointerWheelChangedEvent, new PointerEventHandler((_, _) => RegisterActivity()), true);
        element.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler((_, _) => RegisterActivity()), true);
    }

    public void RegisterActivity() => _lastActivity = Environment.TickCount64;

    private void Check()
    {
        if (!vault.IsUnlocked)
        {
            // Le délai repart de zéro au prochain déverrouillage.
            RegisterActivity();
            return;
        }

        var idle = TimeSpan.FromMilliseconds(Environment.TickCount64 - _lastActivity);
        if (idle >= TimeSpan.FromMinutes(settings.Get().AutoLockMinutes))
        {
            vault.Lock();
        }
    }
}
