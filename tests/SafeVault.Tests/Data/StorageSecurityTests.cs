using System.Text;
using Microsoft.Data.Sqlite;
using SafeVault.Core;
using SafeVault.Core.Services;
using SafeVault.Tests.Security;
using SafeVault.Tests.Support;

namespace SafeVault.Tests.Data;

/// <summary>
/// Critère de réussite du MVP : aucune donnée sensible en clair sur le disque,
/// et toute altération du fichier est détectée.
/// </summary>
public class StorageSecurityTests
{
    [Fact]
    public void FichierSQLite_NeContientAucuneDonneeSensibleEnClair()
    {
        using var t = new TestVault().Created();
        t.Credentials.Add(Drafts.GitHub);
        t.Credentials.Add(Drafts.Google);
        var banque = t.Credentials.Add(Drafts.Banque);
        t.Credentials.Update(banque.Id, Drafts.Banque with { Note = "Code agence 4471-ZX" });
        t.Vault.Lock();

        var raw = t.ReadAllRawBytes();

        string[] secrets =
        [
            TestVault.MasterPassword,
            "Gh-Secret-Unique-7731!", "Gg-Secret-Unique-4410!", "Bq-Secret-Unique-9902!",
            "GitHub", "Google", "cobank",
            "dev@exemple.com", "moi@gmail.com", "client-001",
            "github.com", "accounts.google.com",
            "Compte principal", "Code agence 4471-ZX",
        ];

        foreach (var secret in secrets)
        {
            Assert.False(Contains(raw, Encoding.UTF8.GetBytes(secret)), $"« {secret} » trouvé en clair (UTF-8).");
            Assert.False(Contains(raw, Encoding.Unicode.GetBytes(secret)), $"« {secret} » trouvé en clair (UTF-16).");
        }
    }

    [Fact]
    public void AncienneValeurModifiee_NeResteNullePartDansLeFichier()
    {
        // Même chiffrée, une ancienne version ne doit pas traîner : secure_delete écrase les pages libérées.
        using var t = new TestVault().Created();
        var github = t.Credentials.Add(Drafts.GitHub);
        var oldPayload = t.Store.GetEntries().Single().Payload;

        t.Credentials.Update(github.Id, Drafts.GitHub with { Password = "Autre-Secret-0001!" });
        t.Credentials.Delete(github.Id);
        t.Vault.Lock();

        Assert.False(Contains(t.ReadAllRawBytes(), oldPayload));
    }

    [Fact]
    public void PayloadAltere_EstDetecte_EtIgnoreSansBloquerLeCoffre()
    {
        using var t = new TestVault().Created();
        var github = t.Credentials.Add(Drafts.GitHub);
        t.Credentials.Add(Drafts.Google);
        t.Vault.Lock();

        ExecuteSql(t.DatabasePath, $"UPDATE entries SET payload = substr(payload, 1, 20) || X'FF' || substr(payload, 22) WHERE id = '{github.Id}';");

        t.Reopen();
        Assert.True(t.Vault.Unlock(TestVault.MasterPassword).Succeeded);
        Assert.Equal(["Google"], t.Credentials.GetAll().Select(c => c.ServiceName));
        Assert.Equal(1, t.Credentials.UnreadableCount);
    }

    [Fact]
    public void EntreeIllisible_EstRemplaceeParUneRestauration()
    {
        using var t = new TestVault().Created();
        var github = t.Credentials.Add(Drafts.GitHub);
        var backup = new BackupService(t.Credentials, TestKdf.Fast, t.Time);
        var file = backup.Export("Sauvegarde-Coffre-2026!");
        t.Vault.Lock();

        ExecuteSql(t.DatabasePath, $"UPDATE entries SET payload = substr(payload, 1, 20) || X'FF' || substr(payload, 22) WHERE id = '{github.Id}';");

        t.Reopen();
        t.Vault.Unlock(TestVault.MasterPassword);
        Assert.Equal(1, t.Credentials.UnreadableCount);

        backup = new BackupService(t.Credentials, TestKdf.Fast, t.Time);
        backup.Restore(backup.Open(file, "Sauvegarde-Coffre-2026!"));

        Assert.Equal(0, t.Credentials.UnreadableCount);
        Assert.Equal("Gh-Secret-Unique-7731!", t.Credentials.GetAll().Single().Password);
    }

    [Fact]
    public void PayloadsEchangesEntreDeuxEntrees_SontDetectes()
    {
        using var t = new TestVault().Created();
        var github = t.Credentials.Add(Drafts.GitHub);
        var google = t.Credentials.Add(Drafts.Google);
        t.Vault.Lock();

        ExecuteSql(t.DatabasePath, $"""
            UPDATE entries SET payload = (
                SELECT payload FROM entries WHERE id = '{google.Id}'
            ) WHERE id = '{github.Id}';
            """);

        t.Reopen();
        t.Vault.Unlock(TestVault.MasterPassword);
        Assert.Equal(["Google"], t.Credentials.GetAll().Select(c => c.ServiceName));
        Assert.Equal(1, t.Credentials.UnreadableCount);
    }

    [Fact]
    public void CleDeCoffreAlteree_EmpecheLeDeverrouillage()
    {
        using var t = new TestVault().Created();
        t.Vault.Lock();

        ExecuteSql(t.DatabasePath, "UPDATE vault_meta SET wrapped_vault_key = X'00' || substr(wrapped_vault_key, 2);");

        t.Reopen();
        Assert.False(t.Vault.Unlock(TestVault.MasterPassword).Succeeded);
    }

    private static void ExecuteSql(string databasePath, string sql)
    {
        using var connection = new SqliteConnection($"Data Source={databasePath};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        Assert.Equal(1, command.ExecuteNonQuery());
    }

    private static bool Contains(byte[] haystack, byte[] needle) => haystack.AsSpan().IndexOf(needle) >= 0;
}
