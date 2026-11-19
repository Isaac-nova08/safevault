using System.Text;
using SafeVault.Core;
using SafeVault.Core.Models;
using SafeVault.Core.Services;
using SafeVault.Tests.Security;
using SafeVault.Tests.Support;

namespace SafeVault.Tests.Core;

public class BackupServiceTests
{
    private const string BackupPassword = "Sauvegarde-Coffre-2026!";

    private static BackupService Backups(TestVault t) => new(t.Credentials, TestKdf.Fast, t.Time);

    [Fact]
    public void ExportPuisImport_RestitueDesIdentifiantsIdentiques()
    {
        using var source = new TestVault().Created();
        source.Credentials.Add(Drafts.GitHub);
        source.Credentials.Add(Drafts.Google);
        source.Credentials.Add(Drafts.Banque);
        var expected = source.Credentials.GetAll();

        var file = Backups(source).Export(BackupPassword);

        // Restauration dans un autre coffre (autre Master Password, autre clé).
        using var target = new TestVault().Created();
        target.Credentials.Add(Drafts.Google);
        var contents = Backups(target).Open(file, BackupPassword);
        Backups(target).Restore(contents);

        Assert.Equal(source.Time.Now, contents.ExportedAt);
        Assert.Equal(expected, target.Credentials.GetAll());

        // Persisté et lisible après redémarrage.
        target.Reopen();
        target.Vault.Unlock(TestVault.MasterPassword);
        Assert.Equal(expected, target.Credentials.GetAll());
    }

    [Fact]
    public void CoffreVide_PeutEtreSauvegardeEtRestaure()
    {
        using var t = new TestVault().Created();
        var file = Backups(t).Export(BackupPassword);

        t.Credentials.Add(Drafts.GitHub);
        Backups(t).Restore(Backups(t).Open(file, BackupPassword));

        Assert.Empty(t.Credentials.GetAll());
    }

    [Fact]
    public void Fichier_NeContientAucuneDonneeEnClair()
    {
        using var t = new TestVault().Created();
        t.Credentials.Add(Drafts.GitHub);

        var file = Backups(t).Export(BackupPassword);

        foreach (var secret in new[] { "GitHub", "dev@exemple.com", "Gh-Secret-Unique-7731!", "Compte principal" })
        {
            Assert.Equal(-1, file.AsSpan().IndexOf(Encoding.UTF8.GetBytes(secret)));
        }
    }

    [Fact]
    public void MotDePasseDeSauvegardeFaible_EstRefuse()
    {
        using var t = new TestVault().Created();

        Assert.Throws<VaultValidationException>(() => Backups(t).Export("court"));
    }

    [Fact]
    public void Export_ExigeUnCoffreOuvert()
    {
        using var t = new TestVault().Created();
        t.Vault.Lock();

        Assert.Throws<VaultLockedException>(() => Backups(t).Export(BackupPassword));
    }

    [Fact]
    public void MauvaisMotDePasse_EstRejete_EtCoffreIntact()
    {
        using var t = new TestVault().Created();
        t.Credentials.Add(Drafts.GitHub);
        var file = Backups(t).Export(BackupPassword);
        t.Credentials.Add(Drafts.Google);
        var before = t.Credentials.GetAll();

        var ex = Assert.Throws<BackupException>(() => Backups(t).Open(file, "Mauvais-Mot-De-Passe-1!"));

        Assert.Contains("incorrect", ex.Message);
        Assert.Equal(before, t.Credentials.GetAll());
    }

    [Fact]
    public void FichierCorrompu_EstRefuse_EtCoffreIntact()
    {
        using var t = new TestVault().Created();
        t.Credentials.Add(Drafts.GitHub);
        var file = Backups(t).Export(BackupPassword);
        file[file.Length / 2] ^= 0xFF;
        var before = t.Credentials.GetAll();

        Assert.Throws<BackupException>(() => Backups(t).Open(file, BackupPassword));
        Assert.Equal(before, t.Credentials.GetAll());
    }

    [Fact]
    public void FichierNonReconnu_EstRefuse()
    {
        using var t = new TestVault().Created();

        var ex = Assert.Throws<BackupException>(() => Backups(t).Open(File.ReadAllBytes(t.DatabasePath), BackupPassword));
        Assert.Contains("n'est pas une sauvegarde", ex.Message);
    }

    [Fact]
    public void ContenuInvalide_EchecDeRestauration_SansModifierLeCoffre()
    {
        using var t = new TestVault().Created();
        t.Credentials.Add(Drafts.GitHub);
        var before = t.Credentials.GetAll();
        var valid = t.Credentials.GetAll()[0];

        // Une entrée valide suivie d'une entrée invalide (catégorie inconnue) : rien ne doit être écrit.
        var contents = new BackupContents(t.Time.Now,
        [
            valid with { Id = Guid.NewGuid(), ServiceName = "Nouveau" },
            valid with { Id = Guid.NewGuid(), CategoryId = 999 },
        ]);

        Assert.Throws<BackupException>(() => Backups(t).Restore(contents));
        Assert.Equal(before, t.Credentials.GetAll());

        t.Reopen();
        t.Vault.Unlock(TestVault.MasterPassword);
        Assert.Equal(before, t.Credentials.GetAll());
    }

    [Fact]
    public void IdentifiantsEnDouble_SontRefuses()
    {
        using var t = new TestVault().Created();
        var c = t.Credentials.Add(Drafts.GitHub);

        Assert.Throws<BackupException>(() => Backups(t).Restore(new BackupContents(t.Time.Now, [c, c with { ServiceName = "Copie" }])));
        Assert.Single(t.Credentials.GetAll());
    }

    [Fact]
    public void Restauration_DeclencheCredentialsChanged()
    {
        using var t = new TestVault().Created();
        var file = Backups(t).Export(BackupPassword);
        var raised = 0;
        t.Credentials.CredentialsChanged += (_, _) => raised++;

        Backups(t).Restore(Backups(t).Open(file, BackupPassword));

        Assert.Equal(1, raised);
    }

    [Fact]
    public void EntreesRestaurees_SontRechiffreesAvecLaCleDuCoffre()
    {
        using var source = new TestVault().Created();
        source.Credentials.Add(Drafts.Banque);
        var file = Backups(source).Export(BackupPassword);

        using var target = new TestVault().Created();
        Backups(target).Restore(Backups(target).Open(file, BackupPassword));

        Assert.Equal(-1, target.ReadAllRawBytes().AsSpan().IndexOf("Bq-Secret-Unique-9902!"u8));
        Assert.Equal(Category.BankId, target.Credentials.GetAll().Single().CategoryId);
    }
}
