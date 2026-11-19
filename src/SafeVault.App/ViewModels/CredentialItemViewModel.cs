using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SafeVault.App.Services;
using SafeVault.Core.Models;

namespace SafeVault.App.ViewModels;

/// <summary>
/// Une ligne de la liste des identifiants. Le mot de passe est masqué par défaut
/// et ne s'affiche que temporairement, sur action explicite de l'utilisateur.
/// </summary>
public sealed partial class CredentialItemViewModel(Credential credential, string categoryName, CredentialActions actions)
    : ObservableObject
{
    /// <summary>Durée d'affichage d'un mot de passe révélé.</summary>
    public static readonly TimeSpan RevealDuration = TimeSpan.FromSeconds(10);

    private const string Mask = "••••••••••";

    private CancellationTokenSource? _revealCts;

    public Credential Credential { get; } = credential;

    public string ServiceName => Credential.ServiceName;
    public string Username => Credential.Username;
    public string? Url => Credential.Url;
    public string CategoryName { get; } = categoryName;
    public bool IsFavorite => Credential.IsFavorite;

    /// <summary>Initiale affichée dans la pastille du service.</summary>
    public string Initial => ServiceName.Length > 0 ? char.ToUpperInvariant(ServiceName[0]).ToString() : "?";

    public string FavoriteGlyph => IsFavorite ? "" : "";
    public string FavoriteTooltip => IsFavorite ? "Retirer des favoris" : "Ajouter aux favoris";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayedPassword), nameof(RevealGlyph), nameof(RevealTooltip))]
    public partial bool IsRevealed { get; private set; }

    public string DisplayedPassword => IsRevealed ? Credential.Password : Mask;
    public string RevealGlyph => IsRevealed ? "" : "";
    public string RevealTooltip => IsRevealed ? "Masquer le mot de passe" : "Afficher le mot de passe (10 s)";

    [RelayCommand]
    private Task EditAsync() => actions.EditAsync(Credential);

    [RelayCommand]
    private Task DeleteAsync() => actions.DeleteAsync(Credential);

    [RelayCommand]
    private void ToggleFavorite() => actions.ToggleFavorite(Credential);

    [RelayCommand]
    private void CopyPassword() => actions.CopyPassword(Credential);

    [RelayCommand]
    private void CopyUsername() => actions.CopyUsername(Credential);

    /// <summary>Affiche le mot de passe pendant <see cref="RevealDuration"/>, ou le masque s'il est déjà visible.</summary>
    // Exécutions concurrentes autorisées : sinon le bouton reste désactivé pendant l'attente
    // et l'utilisateur ne peut pas masquer le mot de passe avant la fin du délai.
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task ToggleRevealAsync()
    {
        _revealCts?.Cancel();

        if (IsRevealed)
        {
            IsRevealed = false;
            return;
        }

        var cts = _revealCts = new CancellationTokenSource();
        IsRevealed = true;
        try
        {
            await Task.Delay(RevealDuration, cts.Token);
            IsRevealed = false;
        }
        catch (TaskCanceledException)
        {
            // masqué manuellement ou ré-affiché entre-temps
        }
    }
}
