using SafeVault.Core;
using SafeVault.Core.Models;
using SafeVault.Core.Services;
using SafeVault.Tests.Support;

namespace SafeVault.Tests.Core;

public class VaultServiceTests
{
    [Fact]
    public void AuDepart_AucunCoffreNExiste()
    {
        using var t = new TestVault();
        Assert.False(t.Vault.VaultExists);
        Assert.False(t.Vault.IsUnlocked);
    }

    [Fact]
    public void Creation_CreeLeCoffreEtLeLaisseDeverrouille()
    {
        using var t = new TestVault().Created();
        Assert.True(t.Vault.VaultExists);
        Assert.True(t.Vault.IsUnlocked);
        Assert.True(File.Exists(t.DatabasePath));
    }

    [Fact]
    public void Creation_DeuxFois_EstRefusee()
    {
        using var t = new TestVault().Created();
        Assert.Throws<InvalidOperationException>(() => t.Vault.Create("Un-Autre-Password-2026!"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("court1!")]
    [InlineData("motdepasse")]          // 10 caractères, un seul type
    [InlineData("aaaaaaaaaaaaaaaaaaaa")] // répétitif
    public void Creation_AvecMasterPasswordFaible_EstRefusee(string weak)
    {
        using var t = new TestVault();
        var ex = Assert.Throws<VaultValidationException>(() => t.Vault.Create(weak));
        Assert.NotEmpty(ex.Errors);
        Assert.False(t.Vault.VaultExists);
    }

    [Fact]
    public void Deverrouillage_AvecLeBonMotDePasse_Reussit()
    {
        using var t = new TestVault().Created();
        t.Reopen();
        Assert.False(t.Vault.IsUnlocked);

        var result = t.Vault.Unlock(TestVault.MasterPassword);

        Assert.True(result.Succeeded);
        Assert.True(t.Vault.IsUnlocked);
    }

    [Theory]
    [InlineData("")]
    [InlineData("mauvais-mot-de-passe")]
    [InlineData("master-password-de-test-2026!")]
    public void Deverrouillage_AvecUnMauvaisMotDePasse_Echoue(string wrong)
    {
        using var t = new TestVault().Created();
        t.Reopen();

        var result = t.Vault.Unlock(wrong);

        Assert.Equal(UnlockStatus.InvalidPassword, result.Status);
        Assert.False(t.Vault.IsUnlocked);
    }

    [Fact]
    public void Verrouillage_RendLesIdentifiantsInaccessibles()
    {
        using var t = new TestVault().Created();
        t.Credentials.Add(Drafts.GitHub);

        t.Vault.Lock();

        Assert.False(t.Vault.IsUnlocked);
        Assert.Throws<VaultLockedException>(() => t.Credentials.GetAll());
        Assert.Throws<VaultLockedException>(() => t.Credentials.Add(Drafts.Google));
    }

    [Fact]
    public void Verrouillage_DeclencheStateChanged()
    {
        using var t = new TestVault().Created();
        var raised = 0;
        t.Vault.StateChanged += (_, _) => raised++;

        t.Vault.Lock();
        t.Vault.Lock(); // déjà verrouillé : pas de nouvel événement

        Assert.Equal(1, raised);
    }

    [Fact]
    public void ApresTroisEchecs_UnDelaiCroissantEstImpose()
    {
        using var t = new TestVault().Created();
        t.Reopen();

        Assert.Equal(TimeSpan.Zero, t.Vault.Unlock("faux-1").RetryAfter);
        Assert.Equal(TimeSpan.Zero, t.Vault.Unlock("faux-2").RetryAfter);
        Assert.Equal(TimeSpan.FromSeconds(1), t.Vault.Unlock("faux-3").RetryAfter);

        // Pendant le délai, même le bon mot de passe est refusé sans être testé.
        var throttled = t.Vault.Unlock(TestVault.MasterPassword);
        Assert.Equal(UnlockStatus.Throttled, throttled.Status);
        Assert.False(t.Vault.IsUnlocked);

        t.Time.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(TimeSpan.FromSeconds(2), t.Vault.Unlock("faux-4").RetryAfter);

        t.Time.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(TimeSpan.FromSeconds(4), t.Vault.Unlock("faux-5").RetryAfter);
    }

    [Fact]
    public void DelaiEntreTentatives_EstPlafonneAUneMinute()
    {
        using var t = new TestVault().Created();
        t.Reopen();

        UnlockResult last = null!;
        for (var i = 0; i < 15; i++)
        {
            last = t.Vault.Unlock("faux");
            t.Time.Advance(last.RetryAfter);
        }

        Assert.Equal(VaultService.MaxThrottleDelay, last.RetryAfter);
    }

    [Fact]
    public void DeverrouillageReussi_RemetLeCompteurAZero()
    {
        using var t = new TestVault().Created();
        t.Reopen();

        t.Vault.Unlock("faux-1");
        t.Vault.Unlock("faux-2");
        Assert.True(t.Vault.Unlock(TestVault.MasterPassword).Succeeded);
        t.Vault.Lock();

        Assert.Equal(TimeSpan.Zero, t.Vault.Unlock("faux-3").RetryAfter);
    }

    [Fact]
    public void ChangementDeMasterPassword_ConserveLesIdentifiants()
    {
        using var t = new TestVault().Created();
        t.Credentials.Add(Drafts.GitHub);

        t.Vault.ChangeMasterPassword(TestVault.MasterPassword, "Nouveau-Master-Password-2026!");
        t.Reopen();

        Assert.Equal(UnlockStatus.InvalidPassword, t.Vault.Unlock(TestVault.MasterPassword).Status);
        Assert.True(t.Vault.Unlock("Nouveau-Master-Password-2026!").Succeeded);
        Assert.Equal("GitHub", Assert.Single(t.Credentials.GetAll()).ServiceName);
    }

    [Fact]
    public void ChangementDeMasterPassword_AvecMauvaisMotDePasseActuel_EstRefuse()
    {
        using var t = new TestVault().Created();
        Assert.Throws<VaultValidationException>(() =>
            t.Vault.ChangeMasterPassword("pas-le-bon", "Nouveau-Master-Password-2026!"));
    }

    [Fact]
    public void ChangementDeMasterPassword_VersUnMotDePasseFaible_EstRefuse()
    {
        using var t = new TestVault().Created();
        Assert.Throws<VaultValidationException>(() =>
            t.Vault.ChangeMasterPassword(TestVault.MasterPassword, "faible"));
    }

    [Fact]
    public void Parametres_ParDefautPuisModifies_SontPersistes()
    {
        using var t = new TestVault().Created();
        var settings = new SettingsService(t.Store);

        Assert.Equal(VaultSettings.Default, settings.Get());

        settings.Save(new VaultSettings { AutoLockMinutes = 15, ClipboardClearSeconds = 30 });
        t.Reopen();

        Assert.Equal(new VaultSettings { AutoLockMinutes = 15, ClipboardClearSeconds = 30 }, new SettingsService(t.Store).Get());
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(5, 1)]
    [InlineData(500, 20)]
    public void Parametres_HorsBornes_SontRefuses(int autoLock, int clipboard)
    {
        using var t = new TestVault().Created();
        Assert.Throws<VaultValidationException>(() =>
            new SettingsService(t.Store).Save(new VaultSettings { AutoLockMinutes = autoLock, ClipboardClearSeconds = clipboard }));
    }
}
