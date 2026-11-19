namespace SafeVault.Core.Models;

/// <summary>Identifiant déchiffré, tel qu'il est manipulé en mémoire quand le coffre est ouvert.</summary>
public sealed record Credential
{
    public required Guid Id { get; init; }
    public required string ServiceName { get; init; }
    public required string Username { get; init; }
    public required string Password { get; init; }
    public string? Url { get; init; }
    public string? Note { get; init; }
    public required int CategoryId { get; init; }
    public bool IsFavorite { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>Données saisies par l'utilisateur pour créer ou modifier un identifiant.</summary>
public sealed record CredentialDraft
{
    public const int MaxServiceNameLength = 200;
    public const int MaxUsernameLength = 320;
    public const int MaxPasswordLength = 1024;
    public const int MaxUrlLength = 2048;
    public const int MaxNoteLength = 10_000;

    public string ServiceName { get; init; } = "";
    public string Username { get; init; } = "";
    public string Password { get; init; } = "";
    public string? Url { get; init; }
    public string? Note { get; init; }
    public int CategoryId { get; init; } = Category.OtherId;
    public bool IsFavorite { get; init; }

    /// <summary>Retourne la liste des erreurs de saisie (vide si le brouillon est valide).</summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(ServiceName))
            errors.Add("Le nom du service est obligatoire.");
        else if (ServiceName.Trim().Length > MaxServiceNameLength)
            errors.Add($"Le nom du service ne doit pas dépasser {MaxServiceNameLength} caractères.");

        if (string.IsNullOrWhiteSpace(Username))
            errors.Add("Le nom d'utilisateur ou l'email est obligatoire.");
        else if (Username.Trim().Length > MaxUsernameLength)
            errors.Add($"Le nom d'utilisateur ne doit pas dépasser {MaxUsernameLength} caractères.");

        if (string.IsNullOrEmpty(Password))
            errors.Add("Le mot de passe est obligatoire.");
        else if (Password.Length > MaxPasswordLength)
            errors.Add($"Le mot de passe ne doit pas dépasser {MaxPasswordLength} caractères.");

        if (Url is { Length: > MaxUrlLength })
            errors.Add($"L'URL ne doit pas dépasser {MaxUrlLength} caractères.");

        if (Note is { Length: > MaxNoteLength })
            errors.Add($"La note ne doit pas dépasser {MaxNoteLength} caractères.");

        return errors;
    }
}
