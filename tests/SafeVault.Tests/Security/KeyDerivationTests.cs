using SafeVault.Security;

namespace SafeVault.Tests.Security;

public class KeyDerivationTests
{
    private readonly byte[] _salt = KeyDerivation.GenerateSalt();

    [Fact]
    public void MemesEntrees_DonnentLaMemeCle()
    {
        using var a = KeyDerivation.DeriveKey("Correct-Horse-Battery", _salt, TestKdf.Fast);
        using var b = KeyDerivation.DeriveKey("Correct-Horse-Battery", _salt, TestKdf.Fast);
        Assert.True(a.Span.SequenceEqual(b.Span));
    }

    [Fact]
    public void MotDePasseDifferent_DonneUneCleDifferente()
    {
        using var a = KeyDerivation.DeriveKey("Correct-Horse-Battery", _salt, TestKdf.Fast);
        using var b = KeyDerivation.DeriveKey("correct-Horse-Battery", _salt, TestKdf.Fast);
        Assert.False(a.Span.SequenceEqual(b.Span));
    }

    [Fact]
    public void SelDifferent_DonneUneCleDifferente()
    {
        using var a = KeyDerivation.DeriveKey("Correct-Horse-Battery", _salt, TestKdf.Fast);
        using var b = KeyDerivation.DeriveKey("Correct-Horse-Battery", KeyDerivation.GenerateSalt(), TestKdf.Fast);
        Assert.False(a.Span.SequenceEqual(b.Span));
    }

    [Fact]
    public void ParametresDifferents_DonnentUneCleDifferente()
    {
        using var a = KeyDerivation.DeriveKey("Correct-Horse-Battery", _salt, TestKdf.Fast);
        using var b = KeyDerivation.DeriveKey("Correct-Horse-Battery", _salt, TestKdf.Fast with { Iterations = 2 });
        Assert.False(a.Span.SequenceEqual(b.Span));
    }

    [Fact]
    public void SaisiesUnicodeEquivalentes_DonnentLaMemeCle()
    {
        // "é" précomposé (U+00E9) et "e" + accent combinant (U+0065 U+0301)
        using var a = KeyDerivation.DeriveKey("café", _salt, TestKdf.Fast);
        using var b = KeyDerivation.DeriveKey("café", _salt, TestKdf.Fast);
        Assert.True(a.Span.SequenceEqual(b.Span));
    }

    [Fact]
    public void Argon2id_VecteurDeReference()
    {
        // Valeur fixée à l'écriture du test : détecte toute modification involontaire
        // de l'algorithme ou des paramètres (qui rendrait les coffres existants illisibles).
        var salt = Convert.FromHexString("000102030405060708090A0B0C0D0E0F");
        using var key = KeyDerivation.DeriveKey("password", salt, TestKdf.Fast);
        Assert.Equal(ReferenceVector, Convert.ToHexString(key.Span));
    }

    private const string ReferenceVector = "FC224B9E25174C45B138824474630E16132CFFFB6712BC641CA36DD6F8F99086";

    [Fact]
    public void BibliothequeArgon2id_RespecteLeVecteurOfficielRfc9106()
    {
        // RFC 9106, section 5.3 : vérifie que la bibliothèque implémente bien Argon2id v1.3.
        using var argon2 = new Konscious.Security.Cryptography.Argon2id(Enumerable.Repeat((byte)0x01, 32).ToArray())
        {
            Salt = Enumerable.Repeat((byte)0x02, 16).ToArray(),
            KnownSecret = Enumerable.Repeat((byte)0x03, 8).ToArray(),
            AssociatedData = Enumerable.Repeat((byte)0x04, 12).ToArray(),
            MemorySize = 32,
            Iterations = 3,
            DegreeOfParallelism = 4,
        };

        Assert.Equal(
            "0D640DF58D78766C08C037A34A8B53C9D01EF0452D75B65EB52520E96B01E659",
            Convert.ToHexString(argon2.GetBytes(32)));
    }

    [Fact]
    public void MotDePasseVide_EstRefuse()
    {
        Assert.Throws<ArgumentException>(() => KeyDerivation.DeriveKey("", _salt, TestKdf.Fast));
    }

    [Fact]
    public void SelTropCourt_EstRefuse()
    {
        Assert.Throws<ArgumentException>(() => KeyDerivation.DeriveKey("password", new byte[8], TestKdf.Fast));
    }

    [Theory]
    [InlineData(512, 1, 1)]      // mémoire trop faible
    [InlineData(1024, 0, 1)]     // aucune itération
    [InlineData(1024, 1, 0)]     // aucun parallélisme
    [InlineData(1024, 1, 17)]    // parallélisme excessif
    public void ParametresInvalides_SontRefuses(int memory, int iterations, int parallelism)
    {
        var kdf = new KdfParameters(memory, iterations, parallelism);
        Assert.Throws<ArgumentOutOfRangeException>(() => KeyDerivation.DeriveKey("password", _salt, kdf));
    }

    [Fact]
    public void ParametresParDefaut_SontValides()
    {
        KdfParameters.Default.Validate();
    }
}
