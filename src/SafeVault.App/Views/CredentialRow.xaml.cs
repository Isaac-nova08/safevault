using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SafeVault.App.ViewModels;

namespace SafeVault.App.Views;

/// <summary>Ligne d'identifiant réutilisée par le tableau de bord et les listes.</summary>
public sealed partial class CredentialRow : UserControl
{
    public static readonly DependencyProperty ItemProperty = DependencyProperty.Register(
        nameof(Item), typeof(CredentialItemViewModel), typeof(CredentialRow), new PropertyMetadata(null));

    public CredentialRow()
    {
        InitializeComponent();
    }

    public CredentialItemViewModel? Item
    {
        get => (CredentialItemViewModel?)GetValue(ItemProperty);
        set => SetValue(ItemProperty, value);
    }

    public static Brush FavoriteBrush(bool isFavorite) => (Brush)Application.Current.Resources[
        isFavorite ? "SystemFillColorCautionBrush" : "TextFillColorSecondaryBrush"];
}
