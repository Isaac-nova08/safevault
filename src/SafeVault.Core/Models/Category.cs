namespace SafeVault.Core.Models;

/// <summary>Catégorie d'identifiants (non sensible, stockée en clair).</summary>
public sealed record Category(int Id, string Name)
{
    public const int SocialNetworksId = 1;
    public const int WorkId = 2;
    public const int BankId = 3;
    public const int DevelopmentId = 4;
    public const int OtherId = 5;

    /// <summary>Catégories créées au premier lancement.</summary>
    public static IReadOnlyList<Category> Defaults { get; } =
    [
        new(SocialNetworksId, "Réseaux sociaux"),
        new(WorkId, "Travail"),
        new(BankId, "Banque"),
        new(DevelopmentId, "Développement"),
        new(OtherId, "Autres"),
    ];
}
