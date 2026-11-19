using SafeVault.Core;
using SafeVault.Core.Models;
using SafeVault.Tests.Support;

namespace SafeVault.Tests.Core;

public class CredentialServiceTests
{
    [Fact]
    public void CategoriesParDefaut_SontCreees()
    {
        using var t = new TestVault().Created();
        Assert.Equal(
            ["Réseaux sociaux", "Travail", "Banque", "Développement", "Autres"],
            t.Credentials.GetCategories().Select(c => c.Name));
    }

    [Fact]
    public void Ajout_PuisLecture_RestitueToutesLesDonnees()
    {
        using var t = new TestVault().Created();

        var added = t.Credentials.Add(Drafts.GitHub);
        var read = t.Credentials.GetById(added.Id);

        Assert.NotNull(read);
        Assert.Equal("GitHub", read.ServiceName);
        Assert.Equal("dev@exemple.com", read.Username);
        Assert.Equal("Gh-Secret-Unique-7731!", read.Password);
        Assert.Equal("https://github.com", read.Url);
        Assert.Equal("Compte principal", read.Note);
        Assert.Equal(Category.DevelopmentId, read.CategoryId);
        Assert.Equal(t.Time.Now, read.CreatedAt);
    }

    [Fact]
    public void Identifiants_SontConservesApresRedemarrage()
    {
        using var t = new TestVault().Created();
        var added = t.Credentials.Add(Drafts.GitHub);

        t.Reopen();
        t.Vault.Unlock(TestVault.MasterPassword);

        Assert.Equal(added, Assert.Single(t.Credentials.GetAll()));
    }

    [Fact]
    public void Ajout_SupprimeLesEspacesInutiles()
    {
        using var t = new TestVault().Created();
        var added = t.Credentials.Add(Drafts.GitHub with { ServiceName = "  GitHub  ", Username = " dev@exemple.com ", Url = "   ", Note = "" });

        Assert.Equal("GitHub", added.ServiceName);
        Assert.Equal("dev@exemple.com", added.Username);
        Assert.Null(added.Url);
        Assert.Null(added.Note);
    }

    [Fact]
    public void Ajout_ConserveLeMotDePasseTelQuel()
    {
        using var t = new TestVault().Created();
        var added = t.Credentials.Add(Drafts.GitHub with { Password = "  espaces voulus  " });
        Assert.Equal("  espaces voulus  ", added.Password);
    }

    [Fact]
    public void Ajout_SansChampsObligatoires_EstRefuse()
    {
        using var t = new TestVault().Created();

        var ex = Assert.Throws<VaultValidationException>(() =>
            t.Credentials.Add(new CredentialDraft { ServiceName = " ", Username = "", Password = "" }));

        Assert.Equal(3, ex.Errors.Count);
        Assert.Equal(0, t.Credentials.Count);
    }

    [Fact]
    public void Ajout_AvecCategorieInconnue_EstRefuse()
    {
        using var t = new TestVault().Created();
        Assert.Throws<VaultValidationException>(() => t.Credentials.Add(Drafts.GitHub with { CategoryId = 99 }));
    }

    [Fact]
    public void Modification_MetAJourLesDonneesEtLaDate()
    {
        using var t = new TestVault().Created();
        var added = t.Credentials.Add(Drafts.GitHub);
        t.Time.Advance(TimeSpan.FromHours(1));

        var updated = t.Credentials.Update(added.Id, Drafts.GitHub with { Password = "Nouveau-Secret-123!" });

        Assert.Equal("Nouveau-Secret-123!", updated.Password);
        Assert.Equal(added.CreatedAt, updated.CreatedAt);
        Assert.Equal(added.CreatedAt.AddHours(1), updated.UpdatedAt);

        t.Reopen();
        t.Vault.Unlock(TestVault.MasterPassword);
        Assert.Equal("Nouveau-Secret-123!", t.Credentials.GetById(added.Id)!.Password);
    }

    [Fact]
    public void Modification_DunIdentifiantInconnu_EstRefusee()
    {
        using var t = new TestVault().Created();
        Assert.Throws<KeyNotFoundException>(() => t.Credentials.Update(Guid.NewGuid(), Drafts.GitHub));
    }

    [Fact]
    public void Suppression_RetireLIdentifiantDefinitivement()
    {
        using var t = new TestVault().Created();
        var github = t.Credentials.Add(Drafts.GitHub);
        t.Credentials.Add(Drafts.Google);

        t.Credentials.Delete(github.Id);
        t.Reopen();
        t.Vault.Unlock(TestVault.MasterPassword);

        Assert.Null(t.Credentials.GetById(github.Id));
        Assert.Equal("Google", Assert.Single(t.Credentials.GetAll()).ServiceName);
    }

    [Fact]
    public void Favoris_PeuventEtreAjoutesEtRetires()
    {
        using var t = new TestVault().Created();
        var github = t.Credentials.Add(Drafts.GitHub);

        t.Credentials.SetFavorite(github.Id, true);
        Assert.True(t.Credentials.GetById(github.Id)!.IsFavorite);

        t.Reopen();
        t.Vault.Unlock(TestVault.MasterPassword);
        Assert.Single(t.Credentials.Search(null, favoritesOnly: true));

        t.Credentials.SetFavorite(github.Id, false);
        Assert.Empty(t.Credentials.Search(null, favoritesOnly: true));
    }

    [Fact]
    public void Liste_EstTrieeParOrdreAlphabetique()
    {
        using var t = new TestVault().Created();
        t.Credentials.Add(Drafts.Google);
        t.Credentials.Add(Drafts.Banque);
        t.Credentials.Add(Drafts.GitHub with { ServiceName = "amazon" });
        t.Credentials.Add(Drafts.GitHub);

        Assert.Equal(["amazon", "Écobank", "GitHub", "Google"], t.Credentials.GetAll().Select(c => c.ServiceName));
    }

    [Theory]
    [InlineData("git", "GitHub")]
    [InlineData("GITHUB", "GitHub")]
    [InlineData("ecobank", "Écobank")]   // sans accent
    [InlineData("gmail", "Google")]      // dans l'identifiant
    [InlineData("accounts.google", "Google")] // dans l'URL
    public void Recherche_TrouveSansTenirCompteDeLaCasseNiDesAccents(string query, string expected)
    {
        using var t = new TestVault().Created();
        t.Credentials.Add(Drafts.GitHub);
        t.Credentials.Add(Drafts.Google);
        t.Credentials.Add(Drafts.Banque);

        Assert.Equal(expected, Assert.Single(t.Credentials.Search(query)).ServiceName);
    }

    [Fact]
    public void Recherche_NeCherchePasDansLesMotsDePasse()
    {
        using var t = new TestVault().Created();
        t.Credentials.Add(Drafts.GitHub);
        Assert.Empty(t.Credentials.Search("7731"));
    }

    [Fact]
    public void Recherche_ParCategorie()
    {
        using var t = new TestVault().Created();
        t.Credentials.Add(Drafts.GitHub);
        t.Credentials.Add(Drafts.Google);
        t.Credentials.Add(Drafts.Banque);

        Assert.Equal("Écobank", Assert.Single(t.Credentials.Search(null, categoryId: Category.BankId)).ServiceName);
    }

    [Fact]
    public void ChaqueModification_DeclencheCredentialsChanged()
    {
        using var t = new TestVault().Created();
        var raised = 0;
        t.Credentials.CredentialsChanged += (_, _) => raised++;

        var github = t.Credentials.Add(Drafts.GitHub);                     // 1
        t.Credentials.Update(github.Id, Drafts.GitHub with { Note = "x" }); // 2
        t.Credentials.SetFavorite(github.Id, true);                        // 3
        t.Credentials.SetFavorite(github.Id, true);                        // inchangé : pas d'événement
        t.Credentials.Delete(github.Id);                                   // 4
        t.Credentials.Delete(github.Id);                                   // déjà supprimé : pas d'événement

        Assert.Equal(4, raised);
    }

    [Fact]
    public void DerniersAjouts_SontDuPlusRecentAuPlusAncien()
    {
        using var t = new TestVault().Created();
        t.Credentials.Add(Drafts.GitHub);
        t.Time.Advance(TimeSpan.FromMinutes(1));
        t.Credentials.Add(Drafts.Google);
        t.Time.Advance(TimeSpan.FromMinutes(1));
        t.Credentials.Add(Drafts.Banque);

        Assert.Equal(["Écobank", "Google"], t.Credentials.GetRecent(2).Select(c => c.ServiceName));
        Assert.Equal(3, t.Credentials.Count);
    }
}
