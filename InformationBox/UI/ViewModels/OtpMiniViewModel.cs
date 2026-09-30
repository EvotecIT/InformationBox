using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Input;
using System.Security.Cryptography;
using AuthIMO.Storage;
using AuthIMO.Models;
using AuthIMO.Otp;
using AuthIMO.OtpAuth;
using AuthIMO.Provisioning;
using InformationBox.Config;
using InformationBox.Services;
using InformationBox.UI.Commands;

namespace InformationBox.UI.ViewModels;

/// <summary>
/// Minimal OTP view model for InformationBox tab.
/// </summary>
public sealed partial class OtpMiniViewModel : IDisposable, System.ComponentModel.INotifyPropertyChanged
{
    private readonly UserSettings _userSettings;
    private OtpVaultSession? _session;
    private bool _disposed;
    private string _errorMessage = string.Empty;
    private readonly IOtpQrScanService _qrScanService;
    private readonly DispatcherTimer _timer;
    private readonly EventHandler _timerTickHandler;

    private string _manualIssuer = string.Empty;
    private string _manualLabel = string.Empty;
    private string _manualSecretBase32 = string.Empty;
    private string _manualCounterText = "0";
    private OtpPreset? _selectedPreset;
    private string _manualErrorMessage = string.Empty;

    private bool _isManualOpen;
    private bool _isQrOpen;

    private bool _isCompactMode;
    private OtpAccountRowViewModel? _selectedAccount;

    private bool _isQrBusy;
    private string _qrStatusMessage = "Click \"Add QR\" to scan.";
    private string _qrErrorMessage = string.Empty;
    private ImageSource? _qrPreview;
    private OtpQrCandidateViewModel? _selectedQrCandidate;
    private CancellationTokenSource? _qrCts;

    public ObservableCollection<OtpAccountRowViewModel> Accounts { get; } = new();
    public ObservableCollection<OtpQrCandidateViewModel> QrCandidates { get; } = new();

    /// <summary>Gets whether a vault is available for account operations.</summary>
    public bool IsAvailable => !_disposed && _session is not null;
    /// <summary>Gets the last vault or clipboard error.</summary>
    public string ErrorMessage
    {
        get => _errorMessage;
        private set => SetField(ref _errorMessage, value);
    }
    /// <summary>Reloads the vault after a disk conflict or an opening failure.</summary>
    public RelayCommand ReloadVaultCommand { get; }

    public bool HasNoAccounts => Accounts.Count == 0;
    public bool HasQrCandidates => QrCandidates.Count > 0;

    public bool IsCompactMode
    {
        get => _isCompactMode;
        set
        {
            if (SetField(ref _isCompactMode, value))
            {
                _userSettings.OtpCompactMode = value;
                _userSettings.Save();
            }
        }
    }

    public OtpAccountRowViewModel? SelectedAccount
    {
        get => _selectedAccount;
        set
        {
            if (SetField(ref _selectedAccount, value))
            {
                OnPropertyChanged(nameof(HasSelectedAccount));
            }
        }
    }

    public bool HasSelectedAccount => SelectedAccount is not null;

    public bool IsManualOpen
    {
        get => _isManualOpen;
        set => SetField(ref _isManualOpen, value);
    }

    public bool IsQrOpen
    {
        get => _isQrOpen;
        set => SetField(ref _isQrOpen, value);
    }

    public string ManualIssuer
    {
        get => _manualIssuer;
        set => SetField(ref _manualIssuer, value);
    }

    public string ManualLabel
    {
        get => _manualLabel;
        set => SetField(ref _manualLabel, value);
    }

    public string ManualSecretBase32
    {
        get => _manualSecretBase32;
        set => SetField(ref _manualSecretBase32, value);
    }

    public string ManualCounterText
    {
        get => _manualCounterText;
        set => SetField(ref _manualCounterText, value);
    }

    public IReadOnlyList<OtpPreset> AvailablePresets { get; } = new[]
    {
        new OtpPreset(
            name: "Recommended (most providers)",
            profile: OtpProvisioningProfile.DefaultTotp,
            description: "TOTP • SHA1 • 6 digits • 30s"),
        new OtpPreset(
            name: "Microsoft Entra / M365 (OATH TOTP)",
            profile: OtpProvisioningProfile.MicrosoftOathTotp,
            description: "TOTP • SHA1 • 6 digits • 30s"),
        new OtpPreset(
            name: "Google Authenticator (TOTP)",
            profile: OtpProvisioningProfile.GoogleAuthenticatorTotp,
            description: "TOTP • SHA1 • 6 digits • 30s"),
        new OtpPreset(
            name: "HOTP (counter-based)",
            profile: OtpProvisioningProfile.DefaultHotp,
            description: "HOTP • SHA1 • 6 digits • counter-based"),
    };

    public OtpPreset? SelectedPreset
    {
        get => _selectedPreset;
        set
        {
            if (SetField(ref _selectedPreset, value))
            {
                OnPropertyChanged(nameof(SelectedPresetDescription));
                OnPropertyChanged(nameof(IsHotpPreset));
            }
        }
    }

    public string SelectedPresetDescription => SelectedPreset?.Description ?? string.Empty;
    public bool IsHotpPreset => SelectedPreset?.Profile.Type == OtpType.Hotp;

    public string ManualErrorMessage
    {
        get => _manualErrorMessage;
        private set => SetField(ref _manualErrorMessage, value);
    }

    public bool IsQrBusy
    {
        get => _isQrBusy;
        private set
        {
            if (SetField(ref _isQrBusy, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string QrStatusMessage
    {
        get => _qrStatusMessage;
        private set => SetField(ref _qrStatusMessage, value);
    }

    public string QrErrorMessage
    {
        get => _qrErrorMessage;
        private set => SetField(ref _qrErrorMessage, value);
    }

    public ImageSource? QrPreview
    {
        get => _qrPreview;
        private set => SetField(ref _qrPreview, value);
    }

    public OtpQrCandidateViewModel? SelectedQrCandidate
    {
        get => _selectedQrCandidate;
        set
        {
            if (SetField(ref _selectedQrCandidate, value))
            {
                QrPreview = value?.PreviewImage;
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public RelayCommand ToggleManualCommand { get; }
    public RelayCommand AddManualCommand { get; }
    public RelayCommand GenerateSecretCommand { get; }
    public AsyncRelayCommand StartAutoScanCommand { get; }
    public AsyncRelayCommand PickRegionCommand { get; }
    public RelayCommand ConfirmQrCommand { get; }
    public RelayCommand ClearQrCommand { get; }

    public event EventHandler<Account>? RequestDeleteAccount;

    public OtpMiniViewModel(UserSettings userSettings) : this(userSettings, new OtpQrScanService()) { }

    internal OtpMiniViewModel(UserSettings userSettings, IOtpQrScanService qrScanService)
    {
        _userSettings = userSettings ?? throw new ArgumentNullException(nameof(userSettings));
        _qrScanService = qrScanService ?? throw new ArgumentNullException(nameof(qrScanService));

        _isCompactMode = _userSettings.OtpCompactMode;

        ReloadVaultCommand = new RelayCommand(OpenVault, () => !_disposed && !IsQrBusy);
        ToggleManualCommand = new RelayCommand(ToggleManualPanel, () => IsAvailable);
        AddManualCommand = new RelayCommand(AddManualAccount, () => IsAvailable);
        GenerateSecretCommand = new RelayCommand(GenerateSecret, () => IsAvailable);
        StartAutoScanCommand = new AsyncRelayCommand(StartAutoScanAsync, () => IsAvailable && !IsQrBusy);
        PickRegionCommand = new AsyncRelayCommand(PickRegionScanAsync, () => IsAvailable && !IsQrBusy);
        ConfirmQrCommand = new RelayCommand(ConfirmSelectedQr, () => IsAvailable && !IsQrBusy && SelectedQrCandidate is not null);
        ClearQrCommand = new RelayCommand(ClearQrResults);

        SelectedPreset = AvailablePresets[0];

        Accounts.CollectionChanged += OnAccountsChanged;
        QrCandidates.CollectionChanged += OnQrCandidatesChanged;

        OpenVault();

        _timerTickHandler = (_, _) => RefreshCodes();
        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += _timerTickHandler;
        _timer.Start();
    }

    private void OpenVault()
    {
        if (_disposed) return;
        ClearQrResults();
        ClearManualFields();
        IsManualOpen = false;
        IsQrOpen = false;
        foreach (var row in Accounts) row.Clear();
        Accounts.Clear();
        _session?.Dispose();
        _session = null;
        ErrorMessage = string.Empty;
        try
        {
            _session = OtpVaultSession.OpenOrCreate(ResolveVaultPath(_userSettings));
            LoadAccounts();
            RefreshCodes();
        }
        catch (Exception ex) when (IsExpectedFailure(ex))
        {
            _session?.Dispose();
            _session = null;
            ErrorMessage = "Cannot open the OTP vault. Check its path, permissions and Windows account, then Reload. Your other InformationBox tabs remain available.";
        }
        OnPropertyChanged(nameof(IsAvailable));
        CommandManager.InvalidateRequerySuggested();
    }

    internal static bool IsExpectedFailure(Exception ex) => ex is IOException or UnauthorizedAccessException
        or CryptographicException or ArgumentException or FormatException or InvalidOperationException
        or NotSupportedException or OverflowException;

    private void ReportError(string message) => ErrorMessage = message;

    private static string ResolveVaultPath(UserSettings settings)
    {
        var existing = settings.OtpVaultPath;
        if (!string.IsNullOrWhiteSpace(existing))
        {
            return existing;
        }

        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "InformationBox");
        var path = Path.Combine(folder, "otp-vault.authimo");
        settings.OtpVaultPath = path;
        settings.Save();
        return path;
    }

    private void ToggleManualPanel()
    {
        IsManualOpen = !IsManualOpen;
        if (IsManualOpen)
        {
            ClearQrResults();
            IsQrOpen = false;
        }
        else ClearManualFields();
    }

    private void GenerateSecret()
    {
        try
        {
            int length = SelectedPreset?.Profile.SecretLengthBytes ?? OtpProvisioningProfile.DefaultTotp.SecretLengthBytes;
            ManualSecretBase32 = OtpProvisioning.GenerateSecretBase32(length);
            ManualErrorMessage = string.Empty;
        }
        catch (Exception ex) when (ex is ArgumentOutOfRangeException or InvalidOperationException)
        {
            ManualErrorMessage = ex.Message;
        }
    }

    private void AddManualAccount()
    {
        ManualErrorMessage = string.Empty;

        try
        {
            var issuer = ManualIssuer.Trim();
            var label = ManualLabel.Trim();
            var secretBase32 = ManualSecretBase32.Trim();

            if (issuer.Length == 0)
            {
                throw new ArgumentException("Issuer is required.");
            }

            if (label.Length == 0)
            {
                throw new ArgumentException("Label is required.");
            }

            if (secretBase32.Length == 0)
            {
                throw new ArgumentException("Secret is required.");
            }

            var profile = SelectedPreset?.Profile ?? OtpProvisioningProfile.DefaultTotp;
            long counter = 0;

            if (profile.Type == OtpType.Hotp)
            {
                if (string.IsNullOrWhiteSpace(ManualCounterText))
                {
                    throw new ArgumentException("Counter is required for HOTP.");
                }

                if (!long.TryParse(ManualCounterText.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out counter))
                {
                    throw new ArgumentException("Counter must be a number.");
                }

                if (counter < 0)
                {
                    throw new ArgumentException("Counter must be non-negative.");
                }
            }

            var account = OtpProvisioning.CreateAccount(issuer, label, profile, secretBase32, counter);

            AddAccount(account);
            ClearManualFields();
            IsManualOpen = false;
        }
        catch (Exception ex) when (IsExpectedFailure(ex))
        {
            ManualErrorMessage = ex.Message;
        }
    }

    private void LoadAccounts()
    {
        Accounts.Clear();
        foreach (var account in _session!.Vault.Accounts)
        {
            var row = new OtpAccountRowViewModel(account, _session!, ReportError);
            row.DeleteRequested += (_, _) => RequestDeleteAccount?.Invoke(this, account);
            Accounts.Add(row);
        }
    }

    private void EnsureSelectedAccount()
    {
        if (SelectedAccount is not null && Accounts.Contains(SelectedAccount))
        {
            return;
        }

        SelectedAccount = Accounts.FirstOrDefault();
    }

    public void DeleteAccount(Account account)
    {
        if (!IsAvailable) return;
        try
        {
            if (!_session!.RemoveAccount(account.Id)) return;
            var row = Accounts.FirstOrDefault(r => r.Account.Id == account.Id);
            if (row is not null)
            {
                row.Clear();
                Accounts.Remove(row);
            }
            ErrorMessage = string.Empty;
            RefreshCodes();
        }
        catch (Exception ex) when (IsExpectedFailure(ex))
        {
            ErrorMessage = DescribeSaveFailure(ex);
        }
    }

    private static string DescribeSaveFailure(Exception ex) => ex is VaultStorageConflictException
        ? "Another instance changed the vault. Reload before making more changes."
        : "The vault could not be saved. Check permissions and disk space, then try again.";

    private void AddAccount(Account account) => AddAccounts(new[] { account });

    private void AddAccounts(IReadOnlyList<Account> accounts)
    {
        try
        {
            if (!IsAvailable) throw new InvalidOperationException("Reload the vault before adding accounts.");
            _session!.AddAccounts(accounts);
        }
        catch (Exception ex)
        {
            foreach (var account in accounts)
            {
                CryptographicOperations.ZeroMemory(account.Secret);
                account.Secret = Array.Empty<byte>();
            }
            if (ex is IOException or UnauthorizedAccessException)
                throw new InvalidOperationException(DescribeSaveFailure(ex), ex);
            throw;
        }
        foreach (var account in accounts)
        {
            var row = new OtpAccountRowViewModel(account, _session!, ReportError);
            row.DeleteRequested += (_, _) => RequestDeleteAccount?.Invoke(this, account);
            Accounts.Add(row);
        }
        ErrorMessage = string.Empty;
        RefreshCodes();
    }

    private void RefreshCodes()
    {
        var unixTimeSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        foreach (var row in Accounts)
        {
            row.Refresh(unixTimeSeconds, showCodes: true);
        }
    }

    private void ClearManualFields()
    {
        ManualIssuer = string.Empty;
        ManualLabel = string.Empty;
        ManualSecretBase32 = string.Empty;
        ManualCounterText = "0";
        ManualErrorMessage = string.Empty;
    }

    private void OnAccountsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasNoAccounts));
        EnsureSelectedAccount();
    }

    private void OnQrCandidatesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasQrCandidates));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
        _timer.Tick -= _timerTickHandler;
        ClearQrResults();
        ClearManualFields();
        foreach (var row in Accounts) row.Clear();
        Accounts.Clear();
        _session?.Dispose();
        _session = null;
        Accounts.CollectionChanged -= OnAccountsChanged;
        QrCandidates.CollectionChanged -= OnQrCandidatesChanged;
    }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));

    private bool SetField<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
