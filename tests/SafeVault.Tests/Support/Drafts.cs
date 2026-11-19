using SafeVault.Core.Models;

namespace SafeVault.Tests.Support;

internal static class Drafts
{
    public static CredentialDraft GitHub => new()
    {
        ServiceName = "GitHub",
        Username = "dev@exemple.com",
        Password = "Gh-Secret-Unique-7731!",
        Url = "https://github.com",
        Note = "Compte principal",
        CategoryId = Category.DevelopmentId,
    };

    public static CredentialDraft Google => new()
    {
        ServiceName = "Google",
        Username = "moi@gmail.com",
        Password = "Gg-Secret-Unique-4410!",
        Url = "https://accounts.google.com",
        CategoryId = Category.OtherId,
    };

    public static CredentialDraft Banque => new()
    {
        ServiceName = "Écobank",
        Username = "client-001",
        Password = "Bq-Secret-Unique-9902!",
        CategoryId = Category.BankId,
        IsFavorite = true,
    };
}
