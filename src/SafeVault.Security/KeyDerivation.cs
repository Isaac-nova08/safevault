using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace SafeVault.Security;

/// <summary>Dérivation d'une clé de 256 bits à partir d'un mot de passe avec Argon2id.</summary>
public static class KeyDerivation
{
    public const int SaltSize = 16;

    public static byte[] GenerateSalt() => RandomNumberGenerator.GetBytes(SaltSize);

    /// <summary>
    /// Dérive une clé à partir du mot de passe. Le mot de passe est normalisé (Unicode NFC)
    /// pour qu'une même saisie produise toujours la même clé.
    /// </summary>
    public static SecretKey DeriveKey(string password, ReadOnlySpan<byte> salt, KdfParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(parameters);

        if (password.Length == 0)
        {
            throw new ArgumentException("Le mot de passe ne peut pas être vide.", nameof(password));
        }

        if (salt.Length < SaltSize)
        {
            throw new ArgumentException($"Le sel doit faire au moins {SaltSize} octets.", nameof(salt));
        }

        parameters.Validate();

        var passwordBytes = Encoding.UTF8.GetBytes(password.Normalize(NormalizationForm.FormC));
        try
        {
            using var argon2 = new Argon2id(passwordBytes)
            {
                Salt = salt.ToArray(),
                MemorySize = parameters.MemoryKiB,
                Iterations = parameters.Iterations,
                DegreeOfParallelism = parameters.Parallelism,
            };

            return SecretKey.TakeOwnership(argon2.GetBytes(SecretKey.SizeInBytes));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
        }
    }
}
