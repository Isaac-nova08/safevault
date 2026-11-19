using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SafeVault.App.Services;
using SafeVault.Core.Services;

namespace SafeVault.App.ViewModels;

/// <summary>Écran de déverrouillage. Les messages d'erreur restent volontairement neutres.</summary>
public sealed partial class UnlockViewModel(VaultService vault, NavigationService navigation) : ObservableObject
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UnlockCommand))]
    public partial string Password { get; set; } = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UnlockCommand))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UnlockCommand))]
    public partial bool IsWaiting { get; set; }

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    /// <summary>Déclenché quand le champ doit reprendre le focus (après un échec).</summary>
    public event EventHandler? FocusPasswordRequested;

    private bool CanUnlock() => !IsBusy && !IsWaiting && Password.Length > 0;

    [RelayCommand(CanExecute = nameof(CanUnlock))]
    private async Task UnlockAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        var password = Password;

        try
        {
            var result = await Task.Run(() => vault.Unlock(password));

            switch (result.Status)
            {
                case UnlockStatus.Success:
                    Password = "";
                    navigation.ShowShell();
                    return;

                case UnlockStatus.InvalidPassword:
                    Password = "";
                    ErrorMessage = "Mot de passe incorrect.";
                    break;

                case UnlockStatus.Throttled:
                    break;
            }

            if (result.RetryAfter > TimeSpan.Zero)
            {
                await WaitBeforeRetryAsync(result.RetryAfter);
            }
        }
        catch (Exception)
        {
            ErrorMessage = "Le coffre n'a pas pu être ouvert.";
        }
        finally
        {
            IsBusy = false;
            FocusPasswordRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Compte à rebours affiché pendant le délai imposé après plusieurs échecs.</summary>
    private async Task WaitBeforeRetryAsync(TimeSpan delay)
    {
        IsWaiting = true;
        var end = DateTime.UtcNow + delay;

        while (DateTime.UtcNow < end)
        {
            var seconds = (int)Math.Ceiling((end - DateTime.UtcNow).TotalSeconds);
            ErrorMessage = $"Mot de passe incorrect. Trop de tentatives : réessayez dans {seconds} s.";
            await Task.Delay(250);
        }

        ErrorMessage = "Mot de passe incorrect.";
        IsWaiting = false;
    }
}
