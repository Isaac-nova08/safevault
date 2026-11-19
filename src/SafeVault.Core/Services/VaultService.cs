using SafeVault.Core.Storage;
using SafeVault.Security;

namespace SafeVault.Core.Services;

public enum UnlockStatus
{
    Success,
    InvalidPassword,
    Throttled,
}

/// <param name="RetryAfter">Délai à attendre avant la prochaine tentative (zéro si aucun).</param>
public sealed record UnlockResult(UnlockStatus Status, TimeSpan RetryAfter)
{
    public bool Succeeded => Status == UnlockStatus.Success;
}

/// <summary>
/// Cycle de vie du coffre : création, déverrouillage, verrouillage, changement de Master Password.
/// La clé de coffre n'existe en mémoire qu'entre <see cref="Unlock"/> et <see cref="Lock"/>.
/// </summary>
/// <remarks>Les opérations de dérivation de clé prennent quelques centaines de millisecondes : les appeler hors du thread d'interface.</remarks>
public sealed class VaultService : IDisposable
{
    /// <summary>Nombre d'échecs tolérés avant d'imposer un délai entre les tentatives.</summary>
    public const int FreeAttempts = 3;
    public static readonly TimeSpan MaxThrottleDelay = TimeSpan.FromMinutes(1);

    private readonly IVaultStore _store;
    private readonly KdfParameters? _kdf;
    private readonly TimeProvider _time;
    private readonly Lock _sync = new();

    private SecretKey? _vaultKey;
    private int _failedAttempts;
    private DateTimeOffset _retryNotBefore = DateTimeOffset.MinValue;

    public VaultService(IVaultStore store, KdfParameters? kdf = null, TimeProvider? time = null)
    {
        _store = store;
        _kdf = kdf;
        _time = time ?? TimeProvider.System;
    }

    /// <summary>Paramètres Argon2id des nouvelles dérivations : ceux fournis, sinon calibrés pour cette machine.</summary>
    private KdfParameters Kdf => _kdf ?? KdfCalibration.ForThisMachine;

    /// <summary>Déclenché après un déverrouillage ou un verrouillage.</summary>
    public event EventHandler? StateChanged;

    public bool VaultExists => _store.Exists();

    public bool IsUnlocked
    {
        get { lock (_sync) return _vaultKey is not null; }
    }

    /// <summary>Crée le coffre et le laisse déverrouillé.</summary>
    /// <exception cref="VaultValidationException">Master Password trop faible.</exception>
    /// <exception cref="InvalidOperationException">Un coffre existe déjà.</exception>
    public void Create(string masterPassword)
    {
        var errors = MasterPasswordPolicy.Validate(masterPassword);
        if (errors.Count > 0)
        {
            throw new VaultValidationException(errors);
        }

        if (_store.Exists())
        {
            throw new InvalidOperationException("Un coffre existe déjà.");
        }

        var (envelope, vaultKey) = VaultKeyProtector.CreateVault(masterPassword, Kdf);
        try
        {
            _store.Initialize(envelope);
        }
        catch
        {
            vaultKey.Dispose();
            throw;
        }

        SetKey(vaultKey);
    }

    /// <summary>
    /// Tente de déverrouiller le coffre. Après <see cref="FreeAttempts"/> échecs, un délai croissant
    /// (1 s, 2 s, 4 s… jusqu'à 1 min) est imposé entre deux tentatives.
    /// </summary>
    public UnlockResult Unlock(string masterPassword)
    {
        var now = _time.GetUtcNow();
        lock (_sync)
        {
            if (now < _retryNotBefore)
            {
                return new UnlockResult(UnlockStatus.Throttled, _retryNotBefore - now);
            }
        }

        var envelope = _store.LoadEnvelope() ?? throw new InvalidOperationException("Aucun coffre n'a été créé.");

        if (!string.IsNullOrEmpty(masterPassword) && VaultKeyProtector.TryUnlock(masterPassword, envelope, out var vaultKey))
        {
            lock (_sync)
            {
                _failedAttempts = 0;
                _retryNotBefore = DateTimeOffset.MinValue;
            }

            SetKey(vaultKey);
            return new UnlockResult(UnlockStatus.Success, TimeSpan.Zero);
        }

        lock (_sync)
        {
            _failedAttempts++;
            var delay = ComputeDelay(_failedAttempts);
            _retryNotBefore = _time.GetUtcNow() + delay;
            return new UnlockResult(UnlockStatus.InvalidPassword, delay);
        }
    }

    /// <summary>Verrouille le coffre : la clé est effacée de la mémoire.</summary>
    public void Lock()
    {
        lock (_sync)
        {
            if (_vaultKey is null)
            {
                return;
            }

            _vaultKey.Dispose();
            _vaultKey = null;
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Change le Master Password. Les entrées ne sont pas rechiffrées : seule la clé de coffre est ré-enrobée.</summary>
    /// <exception cref="VaultValidationException">Mot de passe actuel incorrect ou nouveau mot de passe trop faible.</exception>
    public void ChangeMasterPassword(string currentPassword, string newPassword)
    {
        var vaultKey = GetKey();

        var envelope = _store.LoadEnvelope() ?? throw new InvalidOperationException("Aucun coffre n'a été créé.");
        if (string.IsNullOrEmpty(currentPassword) || !VaultKeyProtector.TryUnlock(currentPassword, envelope, out var check))
        {
            throw new VaultValidationException(["Le Master Password actuel est incorrect."]);
        }

        check.Dispose();

        var errors = MasterPasswordPolicy.Validate(newPassword);
        if (errors.Count > 0)
        {
            throw new VaultValidationException(errors);
        }

        _store.UpdateEnvelope(VaultKeyProtector.Wrap(vaultKey, newPassword, Kdf));
    }

    /// <summary>Clé de coffre courante.</summary>
    /// <exception cref="VaultLockedException">Le coffre est verrouillé.</exception>
    internal SecretKey GetKey()
    {
        lock (_sync)
        {
            return _vaultKey ?? throw new VaultLockedException();
        }
    }

    public void Dispose() => Lock();

    private void SetKey(SecretKey vaultKey)
    {
        lock (_sync)
        {
            _vaultKey?.Dispose();
            _vaultKey = vaultKey;
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private static TimeSpan ComputeDelay(int failedAttempts)
    {
        if (failedAttempts < FreeAttempts)
        {
            return TimeSpan.Zero;
        }

        var exponent = Math.Min(failedAttempts - FreeAttempts, 10);
        var delay = TimeSpan.FromSeconds(1 << exponent);
        return delay < MaxThrottleDelay ? delay : MaxThrottleDelay;
    }
}
