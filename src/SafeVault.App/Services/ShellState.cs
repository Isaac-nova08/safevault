using CommunityToolkit.Mvvm.ComponentModel;

namespace SafeVault.App.Services;

/// <summary>Sections de l'écran principal.</summary>
public enum ShellSection
{
    Dashboard,
    AllCredentials,
    Favorites,
    Generator,
    Settings,
}

/// <summary>
/// État partagé de l'écran principal : texte de la barre de recherche, demandes de navigation
/// venant des pages (ex. « Voir tout » sur le tableau de bord) et notifications brèves.
/// </summary>
public sealed partial class ShellState : ObservableObject
{
    [ObservableProperty]
    public partial string SearchText { get; set; } = "";

    public event EventHandler<ShellSection>? NavigationRequested;

    /// <summary>Message bref à afficher en bas de l'écran (ex. « Mot de passe copié »).</summary>
    public event EventHandler<string>? NotificationRequested;

    public void NavigateTo(ShellSection section) => NavigationRequested?.Invoke(this, section);

    public void Notify(string message) => NotificationRequested?.Invoke(this, message);

    /// <summary>Réinitialise l'état (au verrouillage).</summary>
    public void Reset() => SearchText = "";
}
