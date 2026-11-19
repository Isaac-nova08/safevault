using SafeVault.Security;

namespace SafeVault.Tests.Security;

internal static class TestKdf
{
    /// <summary>Paramètres Argon2id volontairement faibles pour que les tests restent rapides.</summary>
    public static readonly KdfParameters Fast = new(MemoryKiB: 1024, Iterations: 1, Parallelism: 1);
}
