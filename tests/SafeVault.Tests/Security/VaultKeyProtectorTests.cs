using SafeVault.Security;

namespace SafeVault.Tests.Security;

public class VaultKeyProtectorTests
{
    private const string MasterPassword = "Un-Master-Password-Solide-2026";

    [Fact]
    public void BonMotDePasse_DeverrouilleLaMemeCleDeCoffre()
    {
        var (envelope, vaultKey) = VaultKeyProtector.CreateVault(MasterPassword, TestKdf.Fast);
        using (vaultKey)
        {
            Assert.True(VaultKeyProtector.TryUnlock(MasterPassword, envelope, out var unlocked));
            using (unlocked)
            {
                Assert.True(vaultKey.Span.SequenceEqual(unlocked.Span));
            }
        }
    }

    [Theory]
    [InlineData("un-master-password-solide-2026")]
    [InlineData("Un-Master-Password-Solide-2026 ")]
    [InlineData("x")]
    public void MauvaisMotDePasse_EstRejete(string wrongPassword)
    {
        var (envelope, vaultKey) = VaultKeyProtector.CreateVault(MasterPassword, TestKdf.Fast);
        vaultKey.Dispose();

        Assert.False(VaultKeyProtector.TryUnlock(wrongPassword, envelope, out var unlocked));
        Assert.Null(unlocked);
    }

    [Fact]
    public void CleEnrobeeAlteree_EstRejetee()
    {
        var (envelope, vaultKey) = VaultKeyProtector.CreateVault(MasterPassword, TestKdf.Fast);
        vaultKey.Dispose();

        envelope.WrappedKey[^1] ^= 0x01;

        Assert.False(VaultKeyProtector.TryUnlock(MasterPassword, envelope, out _));
    }

    [Fact]
    public void SelAltere_EstRejete()
    {
        var (envelope, vaultKey) = VaultKeyProtector.CreateVault(MasterPassword, TestKdf.Fast);
        vaultKey.Dispose();

        envelope.Salt[0] ^= 0x01;

        Assert.False(VaultKeyProtector.TryUnlock(MasterPassword, envelope, out _));
    }

    [Fact]
    public void ParametresKdfModifies_SontRejetes()
    {
        // Un attaquant qui baisse les paramètres Argon2id dans la base ne doit pas obtenir un coffre valide.
        var (envelope, vaultKey) = VaultKeyProtector.CreateVault(MasterPassword, TestKdf.Fast with { Iterations = 2 });
        vaultKey.Dispose();

        var weakened = envelope with { Kdf = envelope.Kdf with { Iterations = 1 } };

        Assert.False(VaultKeyProtector.TryUnlock(MasterPassword, weakened, out _));
    }

    [Fact]
    public void VersionInconnue_LeveUneException()
    {
        var (envelope, vaultKey) = VaultKeyProtector.CreateVault(MasterPassword, TestKdf.Fast);
        vaultKey.Dispose();

        Assert.Throws<NotSupportedException>(() =>
            VaultKeyProtector.TryUnlock(MasterPassword, envelope with { Version = 99 }, out _));
    }

    [Fact]
    public void DeuxCoffres_OntDesSelsEtDesClesDifferents()
    {
        var (env1, key1) = VaultKeyProtector.CreateVault(MasterPassword, TestKdf.Fast);
        var (env2, key2) = VaultKeyProtector.CreateVault(MasterPassword, TestKdf.Fast);
        using (key1)
        using (key2)
        {
            Assert.NotEqual(env1.Salt, env2.Salt);
            Assert.False(key1.Span.SequenceEqual(key2.Span));
        }
    }

    [Fact]
    public void ChangementDeMasterPassword_GardeLaMemeCleDeCoffre()
    {
        var (oldEnvelope, vaultKey) = VaultKeyProtector.CreateVault(MasterPassword, TestKdf.Fast);
        using (vaultKey)
        {
            var newEnvelope = VaultKeyProtector.Wrap(vaultKey, "Nouveau-Master-Password!", TestKdf.Fast);

            Assert.NotEqual(oldEnvelope.Salt, newEnvelope.Salt);
            Assert.False(VaultKeyProtector.TryUnlock(MasterPassword, newEnvelope, out _));
            Assert.True(VaultKeyProtector.TryUnlock("Nouveau-Master-Password!", newEnvelope, out var unlocked));
            using (unlocked)
            {
                Assert.True(vaultKey.Span.SequenceEqual(unlocked.Span));
            }
        }
    }

    [Fact]
    public void Enveloppe_NeContientPasLaCleDeCoffreEnClair()
    {
        var (envelope, vaultKey) = VaultKeyProtector.CreateVault(MasterPassword, TestKdf.Fast);
        using (vaultKey)
        {
            Assert.Equal(-1, envelope.WrappedKey.AsSpan().IndexOf(vaultKey.Span));
        }
    }
}
