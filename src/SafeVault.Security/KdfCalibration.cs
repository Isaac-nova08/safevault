using System.Diagnostics;
using System.Security.Cryptography;

namespace SafeVault.Security;

/// <summary>
/// Ajuste le nombre de passes Argon2id à la machine : on vise une durée de dérivation donnée,
/// sans jamais descendre sous les paramètres par défaut.
/// </summary>
/// <remarks>
/// Les paramètres retenus sont enregistrés avec le coffre (ou la sauvegarde) : un coffre créé sur
/// une machine rapide reste ouvrable sur une machine plus lente, simplement un peu moins vite.
/// </remarks>
public static class KdfCalibration
{
    /// <summary>Durée visée pour une dérivation (création, déverrouillage, sauvegarde).</summary>
    public static readonly TimeSpan DefaultTarget = TimeSpan.FromMilliseconds(500);

    /// <summary>Nombre de passes maximal retenu par la calibration.</summary>
    public const int MaxCalibratedIterations = 10;

    private static readonly Lazy<KdfParameters> Current = new(() => Calibrate(DefaultTarget));

    /// <summary>Paramètres calibrés pour cette machine (mesurés une seule fois par exécution).</summary>
    public static KdfParameters ForThisMachine => Current.Value;

    /// <summary>
    /// Mesure le coût d'une passe avec la mémoire par défaut, puis choisit le nombre de passes
    /// le plus proche de <paramref name="target"/>, entre <see cref="KdfParameters.Default"/> et
    /// <see cref="MaxCalibratedIterations"/>.
    /// </summary>
    public static KdfParameters Calibrate(TimeSpan target)
    {
        var baseline = KdfParameters.Default;
        var probe = baseline with { Iterations = 1 };
        var salt = KeyDerivation.GenerateSalt();
        var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));

        // Première dérivation : échauffement (chargement, allocation) ; la seconde est mesurée.
        KeyDerivation.DeriveKey(password, salt, probe).Dispose();
        var watch = Stopwatch.StartNew();
        KeyDerivation.DeriveKey(password, salt, probe).Dispose();
        var perPass = watch.Elapsed;

        var iterations = perPass <= TimeSpan.Zero
            ? MaxCalibratedIterations
            : (int)Math.Round(target / perPass);

        return baseline with { Iterations = Math.Clamp(iterations, baseline.Iterations, MaxCalibratedIterations) };
    }
}
