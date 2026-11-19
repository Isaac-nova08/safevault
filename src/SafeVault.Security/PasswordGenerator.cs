using System.Security.Cryptography;

namespace SafeVault.Security;

/// <summary>Options du générateur de mots de passe.</summary>
public sealed record PasswordGeneratorOptions
{
    public int Length { get; init; } = 20;
    public bool IncludeUppercase { get; init; } = true;
    public bool IncludeLowercase { get; init; } = true;
    public bool IncludeDigits { get; init; } = true;
    public bool IncludeSymbols { get; init; } = true;
}

/// <summary>
/// Générateur de mots de passe basé sur <see cref="RandomNumberGenerator"/> (cryptographiquement sûr).
/// Chaque jeu de caractères sélectionné apparaît au moins une fois.
/// </summary>
public static class PasswordGenerator
{
    public const int MinLength = 4;
    public const int MaxLength = 128;

    public const string Uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    public const string Lowercase = "abcdefghijklmnopqrstuvwxyz";
    public const string Digits = "0123456789";
    public const string Symbols = "!@#$%^&*()-_=+[]{};:,.<>?/~";

    /// <exception cref="ArgumentException">Aucun jeu de caractères sélectionné.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Longueur hors de [<see cref="MinLength"/>, <see cref="MaxLength"/>].</exception>
    public static string Generate(PasswordGeneratorOptions? options = null)
    {
        options ??= new PasswordGeneratorOptions();

        if (options.Length is < MinLength or > MaxLength)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.Length,
                $"La longueur doit être comprise entre {MinLength} et {MaxLength}.");
        }

        var sets = GetCharacterSets(options);
        if (sets.Count == 0)
        {
            throw new ArgumentException("Sélectionnez au moins un type de caractères.", nameof(options));
        }

        var pool = string.Concat(sets);
        var buffer = new char[options.Length];

        try
        {
            // Un caractère garanti par jeu sélectionné…
            for (var i = 0; i < sets.Count; i++)
            {
                buffer[i] = PickRandom(sets[i]);
            }

            // …le reste tiré dans l'ensemble des jeux…
            for (var i = sets.Count; i < buffer.Length; i++)
            {
                buffer[i] = PickRandom(pool);
            }

            // …puis mélange (Fisher-Yates) pour que les caractères garantis ne soient pas en tête.
            for (var i = buffer.Length - 1; i > 0; i--)
            {
                var j = RandomNumberGenerator.GetInt32(i + 1);
                (buffer[i], buffer[j]) = (buffer[j], buffer[i]);
            }

            return new string(buffer);
        }
        finally
        {
            Array.Clear(buffer);
        }
    }

    /// <summary>Entropie approximative, en bits, d'un mot de passe généré avec ces options.</summary>
    public static double EstimateEntropyBits(PasswordGeneratorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var poolSize = GetCharacterSets(options).Sum(s => s.Length);
        return poolSize == 0 ? 0 : options.Length * Math.Log2(poolSize);
    }

    private static List<string> GetCharacterSets(PasswordGeneratorOptions options)
    {
        var sets = new List<string>(4);
        if (options.IncludeUppercase) sets.Add(Uppercase);
        if (options.IncludeLowercase) sets.Add(Lowercase);
        if (options.IncludeDigits) sets.Add(Digits);
        if (options.IncludeSymbols) sets.Add(Symbols);
        return sets;
    }

    private static char PickRandom(string set) => set[RandomNumberGenerator.GetInt32(set.Length)];
}
