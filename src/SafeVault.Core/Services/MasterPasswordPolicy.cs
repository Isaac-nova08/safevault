namespace SafeVault.Core.Services;

public enum PasswordStrength
{
    TooWeak,
    Weak,
    Medium,
    Strong,
}

/// <summary>
/// Règles du Master Password : c'est lui qui protège tout le coffre,
/// la force du chiffrement dépend directement de lui.
/// </summary>
public static class MasterPasswordPolicy
{
    public const int MinLength = 10;

    /// <summary>À partir de cette longueur, une phrase de passe est acceptée sans exigence de variété.</summary>
    public const int PassphraseLength = 16;

    /// <summary>Retourne les raisons pour lesquelles le mot de passe est refusé (vide s'il est accepté).</summary>
    public static IReadOnlyList<string> Validate(string? password)
    {
        var errors = new List<string>();

        if (string.IsNullOrEmpty(password))
        {
            errors.Add("Le Master Password est obligatoire.");
            return errors;
        }

        if (password.Length < MinLength)
        {
            errors.Add($"Le Master Password doit contenir au moins {MinLength} caractères.");
        }
        else if (password.Length < PassphraseLength && CountCharacterClasses(password) < 3)
        {
            errors.Add($"Utilisez au moins 3 types de caractères (majuscules, minuscules, chiffres, symboles) " +
                       $"ou une phrase de passe d'au moins {PassphraseLength} caractères.");
        }

        if (password.Distinct().Count() <= 2)
        {
            errors.Add("Le Master Password est trop répétitif.");
        }

        return errors;
    }

    /// <summary>Estimation simple de la force, pour l'indicateur visuel de l'interface.</summary>
    public static PasswordStrength Evaluate(string? password)
    {
        if (Validate(password).Count > 0)
        {
            return PasswordStrength.TooWeak;
        }

        var score = password!.Length + 2 * CountCharacterClasses(password);
        return score switch
        {
            >= 26 => PasswordStrength.Strong,
            >= 18 => PasswordStrength.Medium,
            _ => PasswordStrength.Weak,
        };
    }

    private static int CountCharacterClasses(string password)
    {
        var classes = 0;
        if (password.Any(char.IsUpper)) classes++;
        if (password.Any(char.IsLower)) classes++;
        if (password.Any(char.IsDigit)) classes++;
        if (password.Any(c => !char.IsLetterOrDigit(c))) classes++;
        return classes;
    }
}
