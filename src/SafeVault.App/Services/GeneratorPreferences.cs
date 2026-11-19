using SafeVault.Security;

namespace SafeVault.App.Services;

/// <summary>
/// Derniers réglages du générateur (en mémoire uniquement), réutilisés par le bouton
/// « Générer » du formulaire d'identifiant.
/// </summary>
public sealed class GeneratorPreferences
{
    public PasswordGeneratorOptions Options { get; set; } = new();
}
