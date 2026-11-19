using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SafeVault.App.Services;
using SafeVault.Core;
using SafeVault.Core.Models;
using SafeVault.Core.Services;

namespace SafeVault.App.ViewModels;

/// <summary>Option du filtre par catégorie (<see cref="Id"/> nul = toutes).</summary>
public sealed record CategoryOption(int? Id, string Name);

/// <summary>Pages « Tous les identifiants » et « Favoris » : liste filtrée par la recherche et la catégorie.</summary>
public sealed partial class CredentialsViewModel(CredentialService credentials, CredentialActions actions, ShellState shell)
    : ObservableObject
{
    private Dictionary<int, string> _categoryNames = [];
    private bool _active;

    public ObservableCollection<CredentialItemViewModel> Items { get; } = [];

    public IReadOnlyList<CategoryOption> CategoryOptions { get; private set; } = [];

    [ObservableProperty]
    public partial CategoryOption? SelectedCategory { get; set; }

    [ObservableProperty]
    public partial bool FavoritesOnly { get; private set; }

    [ObservableProperty]
    public partial string Title { get; private set; } = "";

    [ObservableProperty]
    public partial string Summary { get; private set; } = "";

    [ObservableProperty]
    public partial bool IsEmpty { get; private set; }

    [ObservableProperty]
    public partial string EmptyTitle { get; private set; } = "";

    [ObservableProperty]
    public partial string EmptyMessage { get; private set; } = "";

    public bool HasItems => !IsEmpty;

    public void Activate(bool favoritesOnly)
    {
        FavoritesOnly = favoritesOnly;
        Title = favoritesOnly ? "Favoris" : "Tous les identifiants";

        var categories = credentials.GetCategories();
        _categoryNames = categories.ToDictionary(c => c.Id, c => c.Name);
        CategoryOptions = [new CategoryOption(null, "Toutes les catégories"), .. categories.Select(c => new CategoryOption(c.Id, c.Name))];
        OnPropertyChanged(nameof(CategoryOptions));
        SelectedCategory = CategoryOptions[0];

        credentials.CredentialsChanged += OnCredentialsChanged;
        shell.PropertyChanged += OnShellPropertyChanged;
        _active = true;

        Refresh();
    }

    public void Deactivate()
    {
        _active = false;
        credentials.CredentialsChanged -= OnCredentialsChanged;
        shell.PropertyChanged -= OnShellPropertyChanged;
        Items.Clear();
    }

    [RelayCommand]
    private Task AddAsync() => actions.AddAsync();

    partial void OnSelectedCategoryChanged(CategoryOption? value) => Refresh();

    partial void OnIsEmptyChanged(bool value) => OnPropertyChanged(nameof(HasItems));

    private void OnCredentialsChanged(object? sender, EventArgs e) => Refresh();

    private void OnShellPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellState.SearchText))
        {
            Refresh();
        }
    }

    private void Refresh()
    {
        if (!_active)
        {
            return;
        }

        IReadOnlyList<Credential> results;
        try
        {
            results = credentials.Search(shell.SearchText, SelectedCategory?.Id, FavoritesOnly);
        }
        catch (VaultLockedException)
        {
            return; // l'écran de déverrouillage va s'afficher
        }

        Items.Clear();
        foreach (var credential in results)
        {
            Items.Add(new CredentialItemViewModel(credential, _categoryNames.GetValueOrDefault(credential.CategoryId, ""), actions));
        }

        var isFiltered = !string.IsNullOrWhiteSpace(shell.SearchText) || SelectedCategory?.Id is not null;

        Summary = results.Count switch
        {
            0 => "",
            1 => "1 identifiant",
            _ => $"{results.Count} identifiants",
        };

        IsEmpty = results.Count == 0;
        (EmptyTitle, EmptyMessage) = (isFiltered, FavoritesOnly) switch
        {
            (true, _) => ("Aucun résultat", "Aucun identifiant ne correspond à votre recherche."),
            (false, true) => ("Aucun favori", "Cliquez sur l'étoile d'un identifiant pour le retrouver ici."),
            _ => ("Votre coffre est vide", "Ajoutez votre premier identifiant pour commencer."),
        };
    }
}
