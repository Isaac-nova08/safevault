using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SafeVault.App.Services;
using SafeVault.Core.Services;

namespace SafeVault.App.ViewModels;

/// <summary>Écran principal : barre latérale, recherche et ajout. Le retour à l'écran de déverrouillage est géré par la fenêtre.</summary>
public sealed partial class ShellViewModel(VaultService vault, CredentialService credentials, CredentialActions actions, ShellState state) : ObservableObject
{
    public ShellState State { get; } = state;

    /// <summary>Entrées du fichier impossibles à déchiffrer (fichier altéré ou endommagé).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnreadableEntries), nameof(UnreadableMessage))]
    public partial int UnreadableCount { get; private set; }

    public bool HasUnreadableEntries => UnreadableCount > 0;

    public string UnreadableMessage => UnreadableCount == 1
        ? "1 entrée du coffre est illisible : le fichier a été altéré ou endommagé. Elle est ignorée ; restaurez une sauvegarde pour la récupérer."
        : $"{UnreadableCount} entrées du coffre sont illisibles : le fichier a été altéré ou endommagé. Elles sont ignorées ; restaurez une sauvegarde pour les récupérer.";

    public void Activate()
    {
        credentials.CredentialsChanged += OnCredentialsChanged;
        Refresh();
    }

    public void Deactivate() => credentials.CredentialsChanged -= OnCredentialsChanged;

    [RelayCommand]
    private Task AddAsync() => actions.AddAsync();

    [RelayCommand]
    private void Lock() => vault.Lock();

    private void OnCredentialsChanged(object? sender, EventArgs e) => Refresh();

    private void Refresh() => UnreadableCount = vault.IsUnlocked ? credentials.UnreadableCount : 0;
}
