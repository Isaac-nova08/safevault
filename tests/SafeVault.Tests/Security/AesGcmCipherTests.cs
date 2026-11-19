using System.Security.Cryptography;
using System.Text;
using SafeVault.Security;

namespace SafeVault.Tests.Security;

public class AesGcmCipherTests
{
    private static readonly byte[] Message = Encoding.UTF8.GetBytes("MonMotDePasse!2026 — données secrètes");

    [Fact]
    public void ChiffrementPuisDechiffrement_RestitueLeTexteOriginal()
    {
        using var key = SecretKey.Generate();

        var payload = AesGcmCipher.Encrypt(key, Message);
        var decrypted = AesGcmCipher.Decrypt(key, payload);

        Assert.Equal(Message, decrypted);
    }

    [Fact]
    public void Chiffrement_AjouteNonceEtTag()
    {
        using var key = SecretKey.Generate();
        var payload = AesGcmCipher.Encrypt(key, Message);
        Assert.Equal(Message.Length + AesGcmCipher.Overhead, payload.Length);
    }

    [Fact]
    public void Chiffrement_NeContientPasLeTexteEnClair()
    {
        using var key = SecretKey.Generate();
        var payload = AesGcmCipher.Encrypt(key, Message);

        var fragment = Message.AsSpan(0, 8).ToArray();
        Assert.Equal(-1, payload.AsSpan().IndexOf(fragment));
    }

    [Fact]
    public void TexteVide_FonctionneAussi()
    {
        using var key = SecretKey.Generate();
        var payload = AesGcmCipher.Encrypt(key, ReadOnlySpan<byte>.Empty);
        Assert.Empty(AesGcmCipher.Decrypt(key, payload));
    }

    [Fact]
    public void NonceJamaisReutilise_SurDixMilleChiffrements()
    {
        using var key = SecretKey.Generate();
        var nonces = new HashSet<string>();

        for (var i = 0; i < 10_000; i++)
        {
            var payload = AesGcmCipher.Encrypt(key, Message);
            Assert.True(nonces.Add(Convert.ToHexString(payload, 0, AesGcmCipher.NonceSize)), "Nonce réutilisé.");
        }
    }

    [Fact]
    public void MemeMessageChiffreDeuxFois_DonneDesResultatsDifferents()
    {
        using var key = SecretKey.Generate();
        Assert.NotEqual(AesGcmCipher.Encrypt(key, Message), AesGcmCipher.Encrypt(key, Message));
    }

    [Fact]
    public void MauvaiseCle_EstRejetee()
    {
        using var key = SecretKey.Generate();
        using var otherKey = SecretKey.Generate();
        var payload = AesGcmCipher.Encrypt(key, Message);

        Assert.ThrowsAny<CryptographicException>(() => AesGcmCipher.Decrypt(otherKey, payload));
    }

    [Theory]
    [InlineData(0)]   // nonce
    [InlineData(20)]  // texte chiffré
    [InlineData(-1)]  // tag (dernier octet)
    public void DonneesAlterees_SontRejetees(int position)
    {
        using var key = SecretKey.Generate();
        var payload = AesGcmCipher.Encrypt(key, Message);

        var index = position >= 0 ? position : payload.Length + position;
        payload[index] ^= 0x01;

        Assert.ThrowsAny<CryptographicException>(() => AesGcmCipher.Decrypt(key, payload));
    }

    [Fact]
    public void DonneesAssocieesDifferentes_SontRejetees()
    {
        using var key = SecretKey.Generate();
        var payload = AesGcmCipher.Encrypt(key, Message, "entree-1"u8);

        Assert.Equal(Message, AesGcmCipher.Decrypt(key, payload, "entree-1"u8));
        Assert.ThrowsAny<CryptographicException>(() => AesGcmCipher.Decrypt(key, payload, "entree-2"u8));
    }

    [Fact]
    public void PayloadTropCourt_EstRejete()
    {
        using var key = SecretKey.Generate();
        Assert.ThrowsAny<CryptographicException>(() => AesGcmCipher.Decrypt(key, new byte[AesGcmCipher.Overhead - 1]));
    }
}
