using SafeVault.Security;

namespace SafeVault.Tests.Security;

public class PasswordGeneratorTests
{
    [Theory]
    [InlineData(PasswordGenerator.MinLength)]
    [InlineData(12)]
    [InlineData(20)]
    [InlineData(PasswordGenerator.MaxLength)]
    public void RespecteLaLongueurDemandee(int length)
    {
        var password = PasswordGenerator.Generate(new PasswordGeneratorOptions { Length = length });
        Assert.Equal(length, password.Length);
    }

    [Theory]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, false, true, false)]
    [InlineData(false, false, false, true)]
    [InlineData(true, true, false, false)]
    [InlineData(false, true, true, false)]
    [InlineData(true, true, true, true)]
    public void UtiliseUniquementEtObligatoirementLesJeuxSelectionnes(bool upper, bool lower, bool digits, bool symbols)
    {
        var options = new PasswordGeneratorOptions
        {
            Length = 8,
            IncludeUppercase = upper,
            IncludeLowercase = lower,
            IncludeDigits = digits,
            IncludeSymbols = symbols,
        };

        for (var run = 0; run < 200; run++)
        {
            var password = PasswordGenerator.Generate(options);

            Assert.Equal(upper, password.Any(c => PasswordGenerator.Uppercase.Contains(c)));
            Assert.Equal(lower, password.Any(c => PasswordGenerator.Lowercase.Contains(c)));
            Assert.Equal(digits, password.Any(c => PasswordGenerator.Digits.Contains(c)));
            Assert.Equal(symbols, password.Any(c => PasswordGenerator.Symbols.Contains(c)));
        }
    }

    [Fact]
    public void LongueurMinimale_ContientUnCaractereDeChaqueJeu()
    {
        for (var run = 0; run < 500; run++)
        {
            var password = PasswordGenerator.Generate(new PasswordGeneratorOptions { Length = 4 });

            Assert.Contains(password, c => PasswordGenerator.Uppercase.Contains(c));
            Assert.Contains(password, c => PasswordGenerator.Lowercase.Contains(c));
            Assert.Contains(password, c => PasswordGenerator.Digits.Contains(c));
            Assert.Contains(password, c => PasswordGenerator.Symbols.Contains(c));
        }
    }

    [Fact]
    public void ProduitDesMotsDePasseDifferents()
    {
        var passwords = Enumerable.Range(0, 1000).Select(_ => PasswordGenerator.Generate()).ToHashSet();
        Assert.Equal(1000, passwords.Count);
    }

    [Fact]
    public void LesCaracteresGarantisNeSontPasToujoursEnTete()
    {
        // Sans mélange, le 1er caractère serait toujours une majuscule.
        var firstChars = Enumerable.Range(0, 300)
            .Select(_ => PasswordGenerator.Generate(new PasswordGeneratorOptions { Length = 16 })[0]);

        Assert.Contains(firstChars, c => !PasswordGenerator.Uppercase.Contains(c));
    }

    [Fact]
    public void AucunJeuSelectionne_EstRefuse()
    {
        var options = new PasswordGeneratorOptions
        {
            IncludeUppercase = false,
            IncludeLowercase = false,
            IncludeDigits = false,
            IncludeSymbols = false,
        };

        Assert.Throws<ArgumentException>(() => PasswordGenerator.Generate(options));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(PasswordGenerator.MinLength - 1)]
    [InlineData(PasswordGenerator.MaxLength + 1)]
    public void LongueurHorsBornes_EstRefusee(int length)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PasswordGenerator.Generate(new PasswordGeneratorOptions { Length = length }));
    }

    [Fact]
    public void EstimationEntropie_ParDefautSuperieureA120Bits()
    {
        // 20 caractères parmi 89 symboles ≈ 129 bits
        Assert.True(PasswordGenerator.EstimateEntropyBits(new PasswordGeneratorOptions()) > 120);
    }
}
