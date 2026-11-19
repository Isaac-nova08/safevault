using System.Security.Cryptography;

namespace SafeVault.Security;

/// <summary>
/// Clé symétrique de 256 bits conservée en mémoire.
/// Le contenu est effacé (mis à zéro) dès l'appel à <see cref="Dispose"/>.
/// </summary>
public sealed class SecretKey : IDisposable
{
    public const int SizeInBytes = 32;

    private readonly byte[] _bytes;
    private bool _disposed;

    private SecretKey(byte[] bytes)
    {
        if (bytes.Length != SizeInBytes)
        {
            CryptographicOperations.ZeroMemory(bytes);
            throw new ArgumentException($"Une clé doit faire {SizeInBytes} octets.", nameof(bytes));
        }

        _bytes = bytes;
    }

    /// <summary>Génère une nouvelle clé aléatoire avec un générateur cryptographiquement sûr.</summary>
    public static SecretKey Generate() => new(RandomNumberGenerator.GetBytes(SizeInBytes));

    /// <summary>Crée une clé à partir d'une copie des octets fournis.</summary>
    public static SecretKey FromBytes(ReadOnlySpan<byte> bytes) => new(bytes.ToArray());

    /// <summary>Crée une clé qui prend possession du tableau (il sera effacé à la libération).</summary>
    internal static SecretKey TakeOwnership(byte[] bytes) => new(bytes);

    public bool IsDisposed => _disposed;

    /// <summary>Accès en lecture aux octets de la clé.</summary>
    public ReadOnlySpan<byte> Span
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _bytes;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        CryptographicOperations.ZeroMemory(_bytes);
        _disposed = true;
    }
}
