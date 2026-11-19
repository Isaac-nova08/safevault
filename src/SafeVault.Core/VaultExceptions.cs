namespace SafeVault.Core;

/// <summary>Opération impossible : le coffre est verrouillé.</summary>
public sealed class VaultLockedException() : InvalidOperationException("Le coffre est verrouillé.");

/// <summary>Le contenu du coffre ne peut pas être déchiffré (fichier altéré ou corrompu).</summary>
public sealed class VaultDataCorruptedException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>Données saisies invalides. <see cref="Errors"/> contient les messages à afficher.</summary>
public sealed class VaultValidationException(IReadOnlyList<string> errors)
    : ArgumentException(string.Join(Environment.NewLine, errors))
{
    public IReadOnlyList<string> Errors { get; } = errors;
}
