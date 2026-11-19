using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace SafeVault.App.Views;

/// <summary>
/// Couleur de la pastille d'un service, toujours la même pour un même nom.
/// Aucune icône n'est téléchargée : SafeVault ne fait aucun appel réseau.
/// </summary>
public static class AvatarPalette
{
    private static readonly Color[] Colors =
    [
        ColorHelper.FromArgb(255, 0x3B, 0x82, 0xF6), // bleu
        ColorHelper.FromArgb(255, 0x8B, 0x5C, 0xF6), // violet
        ColorHelper.FromArgb(255, 0xEC, 0x48, 0x99), // rose
        ColorHelper.FromArgb(255, 0xF5, 0x9E, 0x0B), // ambre
        ColorHelper.FromArgb(255, 0x10, 0xB9, 0x81), // émeraude
        ColorHelper.FromArgb(255, 0x06, 0xB6, 0xD4), // cyan
        ColorHelper.FromArgb(255, 0xEF, 0x44, 0x44), // rouge
        ColorHelper.FromArgb(255, 0x64, 0x74, 0x8B), // ardoise
    ];

    private static readonly Dictionary<Color, SolidColorBrush> Brushes = [];

    public static SolidColorBrush BrushFor(string? serviceName)
    {
        // Hachage stable (string.GetHashCode change à chaque lancement).
        var hash = 0;
        foreach (var c in (serviceName ?? "").ToUpperInvariant())
        {
            hash = unchecked(hash * 31 + c);
        }

        var color = Colors[(int)((uint)hash % Colors.Length)];
        if (!Brushes.TryGetValue(color, out var brush))
        {
            brush = new SolidColorBrush(color);
            Brushes[color] = brush;
        }

        return brush;
    }
}
