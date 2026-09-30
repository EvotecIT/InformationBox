using System;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.IO;
using System.Runtime.InteropServices;
using AuthIMO.Storage;
using System.Windows.Input;
using AuthIMO.Models;
using AuthIMO.Otp;
using InformationBox.UI.Commands;
using InformationBox.Services;

namespace InformationBox.UI.ViewModels;

/// <summary>
/// Lightweight OTP row view model for the InformationBox tab.
/// </summary>
public sealed class OtpAccountRowViewModel : INotifyPropertyChanged
{
    private readonly OtpVaultSession _session;
    private readonly Action<string> _reportError;
    private bool _cleared;
    private readonly Action<string> _copyToClipboard;
    private string _code = string.Empty;
    private string _displayCode = string.Empty;
    private int _timeRemainingSeconds;
    private long _lastTotpTimeStep = long.MinValue;
    private long _lastHotpCounter = long.MinValue;

    public Account Account { get; }

    public string Issuer => Account.Issuer;
    public string Label => Account.Label;

    public string DisplayCode
    {
        get => _displayCode;
        private set => SetField(ref _displayCode, value);
    }

    public int TimeRemainingSeconds
    {
        get => _timeRemainingSeconds;
        private set => SetField(ref _timeRemainingSeconds, value);
    }

    public string CountdownDisplay => Account.Type == OtpType.Totp
        ? TimeRemainingSeconds.ToString(CultureInfo.InvariantCulture)
        : $"C:{Account.Counter.ToString(CultureInfo.InvariantCulture)}";

    public ICommand CopyCommand { get; }
    public ICommand DeleteCommand { get; }

    public event EventHandler? DeleteRequested;

    public OtpAccountRowViewModel(Account account, OtpVaultSession session, Action<string> reportError)
        : this(account, session, reportError, System.Windows.Clipboard.SetText) { }

    internal OtpAccountRowViewModel(Account account, OtpVaultSession session, Action<string> reportError, Action<string> copyToClipboard)
    {
        Account = account ?? throw new ArgumentNullException(nameof(account));
        _session = session ?? throw new ArgumentNullException(nameof(session));

        _reportError = reportError ?? throw new ArgumentNullException(nameof(reportError));

        _copyToClipboard = copyToClipboard ?? throw new ArgumentNullException(nameof(copyToClipboard));
        CopyCommand = new RelayCommand(Copy, () => !_cleared && !string.IsNullOrEmpty(_code));
        DeleteCommand = new RelayCommand(() => DeleteRequested?.Invoke(this, EventArgs.Empty), () => !_cleared);
    }

    public void Refresh(long unixTimeSeconds, bool showCodes)
    {
        if (_cleared) return;
        if (Account.Secret is null || Account.Secret.Length == 0)
        {
            _code = string.Empty;
            DisplayCode = "INVALID";
            TimeRemainingSeconds = 0;
            OnPropertyChanged(nameof(CountdownDisplay));
            return;
        }

        try
        {
            if (Account.Type == OtpType.Totp)
            {
                TimeRemainingSeconds = Totp.GetTimeRemainingSeconds(unixTimeSeconds, Account.PeriodSeconds);

                if (showCodes)
                {
                    long timeStep = Account.PeriodSeconds > 0 ? unixTimeSeconds / Account.PeriodSeconds : -1;
                    if (timeStep != _lastTotpTimeStep || string.IsNullOrEmpty(_code))
                    {
                        _code = Totp.ComputeCode(Account.Secret, unixTimeSeconds, Account.PeriodSeconds, Account.Digits, Account.Algorithm);
                        _lastTotpTimeStep = timeStep;
                    }
                }
                else
                {
                    _lastTotpTimeStep = long.MinValue;
                    _code = string.Empty;
                }
            }
            else
            {
                TimeRemainingSeconds = 0;
                if (showCodes)
                {
                    long counter = Account.Counter;
                    if (counter != _lastHotpCounter || string.IsNullOrEmpty(_code))
                    {
                        _code = Hotp.ComputeCode(Account.Secret, counter, Account.Digits, Account.Algorithm);
                        _lastHotpCounter = counter;
                    }
                }
                else
                {
                    _lastHotpCounter = long.MinValue;
                    _code = string.Empty;
                }
            }

            DisplayCode = showCodes ? _code : new string('•', Math.Max(4, Account.Digits));
            OnPropertyChanged(nameof(CountdownDisplay));
        }
        catch (Exception ex) when (ex is ArgumentException or CryptographicException or FormatException)
        {
            _code = string.Empty;
            DisplayCode = "INVALID";
            TimeRemainingSeconds = 0;
            _lastTotpTimeStep = long.MinValue;
            _lastHotpCounter = long.MinValue;
            OnPropertyChanged(nameof(CountdownDisplay));
        }
    }

    private void Copy()
    {
        if (_cleared) return;
        bool consumed = false;
        try
        {
            string code;
            if (Account.Type == OtpType.Hotp)
            {
                code = _session.ConsumeHotp(Account);
                consumed = true;
            }
            else
            {
                code = Totp.ComputeCode(Account.Secret, DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    Account.PeriodSeconds, Account.Digits, Account.Algorithm);
            }
            _copyToClipboard(code);
            _reportError(string.Empty);
        }
        catch (Exception ex) when (OtpMiniViewModel.IsExpectedFailure(ex) || ex is ExternalException)
        {
            _reportError(consumed
                ? "The HOTP counter was saved, but the clipboard is busy. Try Copy again to use the next code."
                : ex is VaultStorageConflictException
                    ? "Another instance changed the vault. Reload before copying a HOTP code."
                    : "The code could not be copied. Check vault access and the clipboard, then try again.");
        }
        finally
        {
            Refresh(DateTimeOffset.UtcNow.ToUnixTimeSeconds(), showCodes: true);
            CommandManager.InvalidateRequerySuggested();
        }
    }

    /// <summary>Releases displayed codes when the row is removed or its vault is closed.</summary>
    public void Clear()
    {
        _cleared = true;
        _code = string.Empty;
        DisplayCode = string.Empty;
        TimeRemainingSeconds = 0;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value))
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
    }
}
