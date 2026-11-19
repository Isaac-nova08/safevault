namespace SafeVault.Security;

/// <summary>
/// Tout ce qu'il faut stocker pour retrouver la clé de coffre à partir du Master Password.
/// Aucune de ces valeurs n'est secrète : sans le bon mot de passe, la clé enrobée est inutilisable.
/// </summary>
/// <param name="Version">Version du format d'enrobage.</param>
/// <param name="Salt">Sel Argon2id propre à ce coffre.</param>
/// <param name="Kdf">Paramètres Argon2id.</param>
/// <param name="WrappedKey">Clé de coffre chiffrée en AES-256-GCM par la clé dérivée du Master Password.</param>
public sealed record VaultKeyEnvelope(int Version, byte[] Salt, KdfParameters Kdf, byte[] WrappedKey);
