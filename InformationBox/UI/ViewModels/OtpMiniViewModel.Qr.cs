using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using AuthIMO.Models;
using AuthIMO.OtpAuth;
using InformationBox.Services;

namespace InformationBox.UI.ViewModels;

public sealed partial class OtpMiniViewModel
{
    private async Task StartAutoScanAsync()
    {
        IsQrOpen = true;
        IsManualOpen = false;
        ClearManualFields();
        await RunQrScanAsync(async ct => await _qrScanService.AutoScanAsync(ct));
    }

    private async Task PickRegionScanAsync()
    {
        IsQrOpen = true;
        IsManualOpen = false;
        ClearManualFields();
        await RunQrScanAsync(async ct => await _qrScanService.ScanRegionAsync(ct));
    }

    private async Task RunQrScanAsync(Func<CancellationToken, Task<OtpQrScanResult>> scan)
    {
        if (!IsAvailable) return;
        ClearQrResults();
        var cts = new CancellationTokenSource();
        _qrCts = cts;
        IsQrBusy = true;
        QrStatusMessage = "Scanning for QR codes...";
        QrErrorMessage = string.Empty;
        try
        {
            var result = await scan(cts.Token);
            if (!_disposed && !cts.IsCancellationRequested && ReferenceEquals(_qrCts, cts))
                ApplyQrResult(result);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (IsExpectedFailure(ex))
        {
            if (!_disposed && !cts.IsCancellationRequested && ReferenceEquals(_qrCts, cts))
            {
                QrErrorMessage = "The screen could not be scanned. Try a smaller region.";
                QrStatusMessage = "Scan failed.";
            }
        }
        finally
        {
            if (ReferenceEquals(_qrCts, cts))
            {
                _qrCts = null;
                IsQrBusy = false;
            }
            cts.Dispose();
        }
    }

    private void ApplyQrResult(OtpQrScanResult result)
    {
        QrCandidates.Clear();
        QrPreview = null;

        foreach (var candidate in result.Candidates)
        {
            if (TryBuildCandidate(candidate, out var vm))
            {
                QrCandidates.Add(vm);
            }
        }

        if (QrCandidates.Count > 0)
        {
            SelectedQrCandidate = QrCandidates[0];
            QrStatusMessage = $"Found {QrCandidates.Count} QR code(s). Select one to add.";
        }
        else
        {
            QrStatusMessage = "No OTP QR codes found. Try Pick Region.";
        }
    }

    private bool TryBuildCandidate(OtpQrCandidate candidate, out OtpQrCandidateViewModel vm)
    {
        var payload = candidate.Payload;
        if (OtpAuthParser.TryParse(payload, out var uri, out _))
        {
            var issuer = string.IsNullOrWhiteSpace(uri!.Issuer) ? "(no issuer)" : uri.Issuer;
            var label = string.IsNullOrWhiteSpace(uri.Label) ? "(no label)" : uri.Label;
            var title = $"{issuer} - {label}";
            var desc = $"{uri.Type} • {uri.Algorithm} • {uri.Digits} digits • {(uri.Type == OtpType.Totp ? $"{uri.PeriodSeconds}s" : $"counter {uri.Counter}")}";
            vm = new OtpQrCandidateViewModel(payload, title, desc, 1, false, candidate.SourceLabel, candidate.PreviewImage);
            return true;
        }

        if (OtpAuthMigrationParser.TryParse(payload, out var document, out _))
        {
            try
            {
                var count = document!.Entries.Count;
                var title = $"Google Authenticator export ({count} account{(count == 1 ? string.Empty : "s")})";
                vm = new OtpQrCandidateViewModel(payload, title, "otpauth-migration", count, true, candidate.SourceLabel, candidate.PreviewImage);
                return true;
            }
            finally
            {
                document?.Dispose();
            }
        }

        vm = null!;
        return false;
    }

    private void ConfirmSelectedQr()
    {
        if (SelectedQrCandidate is null)
        {
            return;
        }

        QrErrorMessage = string.Empty;

        try
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            var payload = SelectedQrCandidate.Payload;

            if (OtpAuthParser.TryParse(payload, out var uri, out var error))
            {
                var account = uri!.ToAccount(now);
                AddAccount(account);
            }
            else if (OtpAuthMigrationParser.TryParse(payload, out var document, out var migrationError))
            {
                try
                {
                    var accounts = document!.ToAccounts(now);
                    AddAccounts(accounts);
                }
                finally
                {
                    document?.Dispose();
                }
            }
            else
            {
                throw new ArgumentException(error ?? migrationError ?? "Invalid OTP QR payload.");
            }

            ClearQrResults();
            IsQrOpen = false;
        }
        catch (Exception ex) when (IsExpectedFailure(ex))
        {
            QrErrorMessage = ex.Message;
        }
    }

    private void ClearQrResults()
    {
        CancelQrScan();
        ClearQrResults(keepStatus: false);
        QrErrorMessage = string.Empty;
    }

    private void ClearQrResults(bool keepStatus)
    {
        QrCandidates.Clear();
        SelectedQrCandidate = null;
        QrPreview = null;
        if (!keepStatus)
        {
            QrStatusMessage = "Click \"Add QR\" to scan.";
        }
    }

    private void CancelQrScan()
    {
        var cts = _qrCts;
        _qrCts = null;
        cts?.Cancel();
        // The awaiting operation owns disposal of its cancellation source.
        IsQrBusy = false;
    }

}
