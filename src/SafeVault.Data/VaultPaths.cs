namespace SafeVault.Data;

/// <summary>Emplacements des fichiers de SafeVault sur le poste de l'utilisateur.</summary>
public static class VaultPaths
{
    /// <summary><c>%LOCALAPPDATA%\SafeVault</c></summary>
    public static string AppDataDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SafeVault");

    /// <summary><c>%LOCALAPPDATA%\SafeVault\vault.db</c></summary>
    public static string DefaultDatabasePath { get; } = Path.Combine(AppDataDirectory, "vault.db");
}
