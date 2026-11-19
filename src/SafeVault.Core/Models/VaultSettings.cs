namespace SafeVault.Core.Models;

/// <summary>Paramètres de l'application (non sensibles).</summary>
public sealed record VaultSettings
{
    public const int MinAutoLockMinutes = 1;
    public const int MaxAutoLockMinutes = 120;
    public const int MinClipboardClearSeconds = 5;
    public const int MaxClipboardClearSeconds = 300;

    /// <summary>Verrouillage automatique après ce nombre de minutes d'inactivité.</summary>
    public int AutoLockMinutes { get; init; } = 5;

    /// <summary>Effacement du presse-papiers après ce nombre de secondes.</summary>
    public int ClipboardClearSeconds { get; init; } = 20;

    public static VaultSettings Default { get; } = new();

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (AutoLockMinutes is < MinAutoLockMinutes or > MaxAutoLockMinutes)
            errors.Add($"Le délai de verrouillage doit être compris entre {MinAutoLockMinutes} et {MaxAutoLockMinutes} minutes.");

        if (ClipboardClearSeconds is < MinClipboardClearSeconds or > MaxClipboardClearSeconds)
            errors.Add($"Le délai du presse-papiers doit être compris entre {MinClipboardClearSeconds} et {MaxClipboardClearSeconds} secondes.");

        return errors;
    }
}
