using SafeVault.Core.Models;
using SafeVault.Core.Storage;

namespace SafeVault.Core.Services;

/// <summary>Lecture et enregistrement des paramètres de l'application (mis en cache après la première lecture).</summary>
public sealed class SettingsService(IVaultStore store)
{
    private VaultSettings? _cached;

    public VaultSettings Get()
    {
        if (_cached is not null)
        {
            return _cached;
        }

        if (!store.Exists())
        {
            return VaultSettings.Default; // pas de cache : le coffre n'est pas encore créé
        }

        return _cached = store.GetSettings();
    }

    /// <exception cref="VaultValidationException">Valeurs hors bornes.</exception>
    public void Save(VaultSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var errors = settings.Validate();
        if (errors.Count > 0)
        {
            throw new VaultValidationException(errors);
        }

        store.SaveSettings(settings);
        _cached = settings;
    }
}
