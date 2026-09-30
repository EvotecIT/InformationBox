using System.Security.Cryptography;
using AuthIMO.Models;
using AuthIMO.Storage;
using AuthIMO.Vault;
using AuthIMO.Platform.Windows;

namespace InformationBox.Services;

/// <summary>Owns a Windows-user-bound OTP vault and its transactional account store.</summary>
public sealed class OtpVaultSession : IDisposable
{
    private readonly VaultDocument _document;
    private readonly VaultAccountStore _accounts;
    private bool _disposed;

    /// <summary>Gets the opened vault.</summary>
    public AuthIMO.Models.Vault Vault => _document.Vault;

    private OtpVaultSession(FileVaultStorage storage, VaultDocument document)
    {
        _document = document;
        _accounts = new VaultAccountStore(document, storage);
    }

    /// <summary>Opens an existing vault or creates a DPAPI vault for the current Windows user.</summary>
    public static OtpVaultSession OpenOrCreate(string vaultPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(vaultPath);
        var storage = new FileVaultStorage(vaultPath);
        var protector = new DpapiKeyProtector();
        VaultDocument document;
        if (storage.Exists())
        {
            var bytes = storage.Load();
            try { document = VaultFile.OpenWithKeyProtector(bytes, protector); }
            finally { CryptographicOperations.ZeroMemory(bytes); }
        }
        else
        {
            var vault = VaultFactory.CreateNew(DateTimeOffset.UtcNow, displayName: "InformationBox");
            document = VaultFile.CreateNewWindowsConvenience(vault, protector);
            byte[]? bytes = null;
            try
            {
                bytes = document.ToVaultFileBytes();
                storage.Save(bytes);
            }
            catch
            {
                document.Dispose();
                throw;
            }
            finally
            {
                if (bytes is not null) CryptographicOperations.ZeroMemory(bytes);
            }
        }
        return new OtpVaultSession(storage, document);
    }

    /// <summary>Persists an account batch, rolling back on failure.</summary>
    public void AddAccounts(IReadOnlyList<Account> accounts)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _accounts.AddAccounts(accounts);
    }

    /// <summary>Persists removal before clearing the account secret.</summary>
    public bool RemoveAccount(Guid id)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _accounts.RemoveAccount(id);
    }

    /// <summary>Persists the next HOTP counter before returning the consumed code.</summary>
    public string ConsumeHotp(Account account)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _accounts.ConsumeHotp(account);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _document.Dispose();
    }
}
