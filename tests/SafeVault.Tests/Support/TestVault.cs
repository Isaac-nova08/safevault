using SafeVault.Core.Services;
using SafeVault.Data;
using SafeVault.Tests.Security;

namespace SafeVault.Tests.Support;

/// <summary>Horloge contrôlée par les tests.</summary>
internal sealed class ManualTimeProvider : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 11, 19, 10, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => Now;

    public void Advance(TimeSpan delta) => Now += delta;
}

/// <summary>
/// Coffre complet (SQLite réel + services) dans un dossier temporaire, supprimé à la fin du test.
/// </summary>
internal sealed class TestVault : IDisposable
{
    public const string MasterPassword = "Master-Password-De-Test-2026!";

    public TestVault()
    {
        Directory = Path.Combine(Path.GetTempPath(), "SafeVaultTests", Guid.NewGuid().ToString("N"));
        DatabasePath = Path.Combine(Directory, "vault.db");
        Reopen();
    }

    public string Directory { get; }
    public string DatabasePath { get; }
    public ManualTimeProvider Time { get; } = new();

    public SqliteVaultStore Store { get; private set; } = null!;
    public VaultService Vault { get; private set; } = null!;
    public CredentialService Credentials { get; private set; } = null!;

    /// <summary>Simule un redémarrage de l'application : nouveaux services sur le même fichier.</summary>
    public void Reopen()
    {
        Vault?.Dispose();
        Store = new SqliteVaultStore(DatabasePath);
        Vault = new VaultService(Store, TestKdf.Fast, Time);
        Credentials = new CredentialService(Vault, Store, Time);
    }

    public TestVault Created()
    {
        Vault.Create(MasterPassword);
        return this;
    }

    /// <summary>Contenu brut de tous les fichiers du dossier du coffre (base + éventuel journal).</summary>
    public byte[] ReadAllRawBytes() =>
        System.IO.Directory.GetFiles(Directory).SelectMany(File.ReadAllBytes).ToArray();

    public void Dispose()
    {
        Vault.Dispose();
        try
        {
            System.IO.Directory.Delete(Directory, recursive: true);
        }
        catch (IOException)
        {
            // Nettoyage au mieux : un antivirus peut retenir le fichier un instant.
        }
    }
}
