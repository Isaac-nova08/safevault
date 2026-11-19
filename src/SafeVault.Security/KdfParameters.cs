namespace SafeVault.Security;

/// <summary>
/// Paramètres Argon2id. Ils ne sont pas secrets et sont stockés avec le coffre
/// pour pouvoir re-dériver la même clé au déverrouillage.
/// </summary>
/// <param name="MemoryKiB">Mémoire utilisée, en Kio.</param>
/// <param name="Iterations">Nombre de passes.</param>
/// <param name="Parallelism">Nombre de voies parallèles.</param>
public sealed record KdfParameters(int MemoryKiB, int Iterations, int Parallelism)
{
    public const int MinMemoryKiB = 1024;          // 1 Mio (tests uniquement)
    public const int MaxMemoryKiB = 1024 * 1024;   // 1 Gio
    public const int MaxIterations = 64;
    public const int MaxParallelism = 16;

    /// <summary>
    /// Paramètres par défaut : 64 Mio, 3 passes, 4 voies.
    /// Au-dessus des recommandations OWASP (19 Mio, 2 passes) ; environ 0,2 s par dérivation sur un PC récent.
    /// </summary>
    public static KdfParameters Default { get; } = new(64 * 1024, 3, 4);

    /// <exception cref="ArgumentOutOfRangeException">Paramètres hors des bornes acceptées.</exception>
    public void Validate()
    {
        if (Parallelism is < 1 or > MaxParallelism)
        {
            throw new ArgumentOutOfRangeException(nameof(Parallelism), Parallelism, "Parallélisme invalide.");
        }

        if (Iterations is < 1 or > MaxIterations)
        {
            throw new ArgumentOutOfRangeException(nameof(Iterations), Iterations, "Nombre d'itérations invalide.");
        }

        if (MemoryKiB < MinMemoryKiB || MemoryKiB > MaxMemoryKiB || MemoryKiB < 8 * Parallelism)
        {
            throw new ArgumentOutOfRangeException(nameof(MemoryKiB), MemoryKiB, "Quantité de mémoire invalide.");
        }
    }
}
