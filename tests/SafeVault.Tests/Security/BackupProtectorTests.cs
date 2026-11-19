using System.Security.Cryptography;
using System.Text;
using SafeVault.Security;

namespace SafeVault.Tests.Security;

public class BackupProtectorTests
{
    private const string Password = "Sauvegarde-Test-2026!";
    private static readonly byte[] Content = Encoding.UTF8.GetBytes("""{"contenu":"secret"}""");

    [Fact]
    public void ChiffrementPuisDechiffrement_RestitueLeContenu()
    {
        var file = BackupProtector.Protect(Content, Password, TestKdf.Fast);

        Assert.Equal(Content, BackupProtector.Unprotect(file, Password));
    }

    [Fact]
    public void Entete_ContientFormatVersionParametresEtSel()
    {
        var file = BackupProtector.Protect(Content, Password, TestKdf.Fast);

        Assert.Equal("SVBACKUP"u8.ToArray(), file[..8]);
        Assert.Equal(BackupProtector.CurrentVersion, BitConverter.ToUInt16(file, 8));
        Assert.Equal(TestKdf.Fast.MemoryKiB, BitConverter.ToInt32(file, 10));
        Assert.Equal(TestKdf.Fast.Iterations, BitConverter.ToInt32(file, 14));
        Assert.Equal(TestKdf.Fast.Parallelism, BitConverter.ToInt32(file, 18));
        Assert.Equal(BackupProtector.MinFileSize + Content.Length, file.Length);
    }

    [Fact]
    public void ContenuEnClair_AbsentDuFichier()
    {
        var file = BackupProtector.Protect(Content, Password, TestKdf.Fast);

        Assert.Equal(-1, file.AsSpan().IndexOf("secret"u8));
    }

    [Fact]
    public void DeuxExports_UtilisentSelEtNonceDifferents()
    {
        var a = BackupProtector.Protect(Content, Password, TestKdf.Fast);
        var b = BackupProtector.Protect(Content, Password, TestKdf.Fast);

        var saltA = a.AsSpan(22, KeyDerivation.SaltSize).ToArray();
        var saltB = b.AsSpan(22, KeyDerivation.SaltSize).ToArray();
        var nonceA = a.AsSpan(BackupProtector.HeaderSize, AesGcmCipher.NonceSize).ToArray();
        var nonceB = b.AsSpan(BackupProtector.HeaderSize, AesGcmCipher.NonceSize).ToArray();

        Assert.NotEqual(saltA, saltB);
        Assert.NotEqual(nonceA, nonceB);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void MauvaisMotDePasse_EstRejete()
    {
        var file = BackupProtector.Protect(Content, Password, TestKdf.Fast);

        Assert.ThrowsAny<CryptographicException>(() => BackupProtector.Unprotect(file, "Autre-Mot-De-Passe-2026!"));
    }

    [Theory]
    [InlineData(12)]   // paramètre KDF (itérations)
    [InlineData(25)]   // sel
    [InlineData(40)]   // nonce
    [InlineData(55)]   // contenu chiffré
    [InlineData(-1)]   // tag
    public void OctetAltere_EstDetecte(int position)
    {
        var file = BackupProtector.Protect(Content, Password, TestKdf.Fast);
        var index = position < 0 ? file.Length + position : position;
        file[index] ^= 0x01;

        var ex = Record.Exception(() => BackupProtector.Unprotect(file, Password));

        Assert.True(ex is CryptographicException or InvalidBackupFormatException, ex?.GetType().Name);
    }

    [Fact]
    public void FichierTronque_EstRejete()
    {
        var file = BackupProtector.Protect(Content, Password, TestKdf.Fast);

        Assert.ThrowsAny<CryptographicException>(() => BackupProtector.Unprotect(file[..^1], Password));
        Assert.Throws<InvalidBackupFormatException>(() => BackupProtector.Unprotect(file[..30], Password));
    }

    [Fact]
    public void FichierEtranger_EstRejete()
    {
        Assert.Throws<InvalidBackupFormatException>(() => BackupProtector.Unprotect(Encoding.UTF8.GetBytes("SQLite format 3\0..."), Password));
        Assert.Throws<InvalidBackupFormatException>(() => BackupProtector.Unprotect([], Password));
    }

    [Fact]
    public void VersionInconnue_EstRejetee()
    {
        var file = BackupProtector.Protect(Content, Password, TestKdf.Fast);
        file[8] = 99;

        var ex = Assert.Throws<InvalidBackupFormatException>(() => BackupProtector.Unprotect(file, Password));
        Assert.Contains("99", ex.Message);
    }

    [Fact]
    public void ParametresKdfDemesures_SontRejetesSansDerivation()
    {
        var file = BackupProtector.Protect(Content, Password, TestKdf.Fast);
        BitConverter.GetBytes(int.MaxValue).CopyTo(file, 10); // mémoire Argon2id absurde

        Assert.Throws<InvalidBackupFormatException>(() => BackupProtector.Unprotect(file, Password));
    }
}
