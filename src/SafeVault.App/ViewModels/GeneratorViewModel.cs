using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SafeVault.App.Services;
using SafeVault.Security;

namespace SafeVault.App.ViewModels;

/// <summary>Page Générateur : réglages, mot de passe généré, robustesse et copie.</summary>
public sealed partial class GeneratorViewModel : ObservableObject
{
    public const int MinSliderLength = 8;
    public const int MaxSliderLength = 64;

    private readonly GeneratorPreferences _preferences;
    private readonly ClipboardService _clipboard;
    private readonly ShellState _shell;
    private bool _initializing = true;

    public GeneratorViewModel(GeneratorPreferences preferences, ClipboardService clipboard, ShellState shell)
    {
        _preferences = preferences;
        _clipboard = clipboard;
        _shell = shell;

        var options = preferences.Options;
        Length = Math.Clamp(options.Length, MinSliderLength, MaxSliderLength);
        IncludeUppercase = options.IncludeUppercase;
        IncludeLowercase = options.IncludeLowercase;
        IncludeDigits = options.IncludeDigits;
        IncludeSymbols = options.IncludeSymbols;

        _initializing = false;
        Generate();
    }

    [ObservableProperty]
    public partial double Length { get; set; }

    [ObservableProperty]
    public partial bool IncludeUppercase { get; set; }

    [ObservableProperty]
    public partial bool IncludeLowercase { get; set; }

    [ObservableProperty]
    public partial bool IncludeDigits { get; set; }

    [ObservableProperty]
    public partial bool IncludeSymbols { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CopyCommand))]
    public partial string GeneratedPassword { get; private set; } = "";

    [ObservableProperty]
    public partial string? ErrorMessage { get; private set; }

    [ObservableProperty]
    public partial double EntropyBits { get; private set; }

    public string LengthLabel => $"{(int)Length} caractères";

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    /// <summary>Jauge de robustesse (0 à 4).</summary>
    public double StrengthValue => EntropyBits switch
    {
        <= 0 => 0,
        < 50 => 1,
        < 75 => 2,
        < 100 => 3,
        _ => 4,
    };

    public string StrengthLabel => EntropyBits switch
    {
        <= 0 => "",
        < 50 => $"Faible · {EntropyBits:0} bits",
        < 75 => $"Moyen · {EntropyBits:0} bits",
        < 100 => $"Fort · {EntropyBits:0} bits",
        _ => $"Très fort · {EntropyBits:0} bits",
    };

    [RelayCommand]
    private void Generate()
    {
        var options = new PasswordGeneratorOptions
        {
            Length = (int)Length,
            IncludeUppercase = IncludeUppercase,
            IncludeLowercase = IncludeLowercase,
            IncludeDigits = IncludeDigits,
            IncludeSymbols = IncludeSymbols,
        };

        _preferences.Options = options;

        if (!(IncludeUppercase || IncludeLowercase || IncludeDigits || IncludeSymbols))
        {
            GeneratedPassword = "";
            EntropyBits = 0;
            ErrorMessage = "Sélectionnez au moins un type de caractères.";
            return;
        }

        ErrorMessage = null;
        GeneratedPassword = PasswordGenerator.Generate(options);
        EntropyBits = PasswordGenerator.EstimateEntropyBits(options);
    }

    private bool CanCopy() => GeneratedPassword.Length > 0;

    [RelayCommand(CanExecute = nameof(CanCopy))]
    private void Copy()
    {
        var seconds = _clipboard.CopySecret(GeneratedPassword);
        _shell.Notify($"Mot de passe copié. Il sera effacé du presse-papiers dans {seconds} s.");
    }

    partial void OnLengthChanged(double value)
    {
        OnPropertyChanged(nameof(LengthLabel));
        Regenerate();
    }

    partial void OnIncludeUppercaseChanged(bool value) => Regenerate();
    partial void OnIncludeLowercaseChanged(bool value) => Regenerate();
    partial void OnIncludeDigitsChanged(bool value) => Regenerate();
    partial void OnIncludeSymbolsChanged(bool value) => Regenerate();

    partial void OnErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasError));

    partial void OnEntropyBitsChanged(double value)
    {
        OnPropertyChanged(nameof(StrengthValue));
        OnPropertyChanged(nameof(StrengthLabel));
    }

    private void Regenerate()
    {
        if (!_initializing)
        {
            Generate();
        }
    }
}
