using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SafeVault.App.Services;
using SafeVault.Core;
using SafeVault.Core.Services;

namespace SafeVault.App.ViewModels;

/// <summary>Tableau de bord : chiffres clés et derniers ajouts.</summary>
public sealed partial class DashboardViewModel(CredentialService credentials, CredentialActions actions, ShellState shell)
    : ObservableObject
{
    public const int RecentCount = 5;

    public ObservableCollection<CredentialItemViewModel> RecentItems { get; } = [];

    [ObservableProperty]
    public partial int TotalCount { get; private set; }

    [ObservableProperty]
    public partial int FavoriteCount { get; private set; }

    [ObservableProperty]
    public partial int CategoryCount { get; private set; }

    [ObservableProperty]
    public partial bool IsEmpty { get; private set; }

    public bool HasItems => !IsEmpty;

    public void Activate()
    {
        credentials.CredentialsChanged += OnCredentialsChanged;
        Refresh();
    }

    public void Deactivate()
    {
        credentials.CredentialsChanged -= OnCredentialsChanged;
        RecentItems.Clear();
    }

    [RelayCommand]
    private Task AddAsync() => actions.AddAsync();

    [RelayCommand]
    private void ViewAll() => shell.NavigateTo(ShellSection.AllCredentials);

    [RelayCommand]
    private void ViewFavorites() => shell.NavigateTo(ShellSection.Favorites);

    partial void OnIsEmptyChanged(bool value) => OnPropertyChanged(nameof(HasItems));

    private void OnCredentialsChanged(object? sender, EventArgs e) => Refresh();

    private void Refresh()
    {
        try
        {
            var all = credentials.GetAll();
            var categoryNames = credentials.GetCategories().ToDictionary(c => c.Id, c => c.Name);

            TotalCount = all.Count;
            FavoriteCount = all.Count(c => c.IsFavorite);
            CategoryCount = all.Select(c => c.CategoryId).Distinct().Count();
            IsEmpty = all.Count == 0;

            RecentItems.Clear();
            foreach (var credential in credentials.GetRecent(RecentCount))
            {
                RecentItems.Add(new CredentialItemViewModel(credential, categoryNames.GetValueOrDefault(credential.CategoryId, ""), actions));
            }
        }
        catch (VaultLockedException)
        {
            // l'écran de déverrouillage va s'afficher
        }
    }
}
