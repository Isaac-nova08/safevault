using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SafeVault.App.ViewModels;

namespace SafeVault.App.Views;

public sealed partial class GeneratorPage : Page
{
    public GeneratorPage()
    {
        InitializeComponent();
    }

    public GeneratorViewModel ViewModel { get; } = App.Services.GetRequiredService<GeneratorViewModel>();

    public static Brush StrengthBrush(double strength) => (Brush)Application.Current.Resources[strength switch
    {
        <= 1 => "SystemFillColorCriticalBrush",
        2 => "SystemFillColorCautionBrush",
        _ => "SystemFillColorSuccessBrush",
    }];
}
