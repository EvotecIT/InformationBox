using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using AuthIMO.Models;
using AuthIMO.Provisioning;
using InformationBox.Config;
using InformationBox.Services;
using InformationBox.UI.ViewModels;
using Xunit;

namespace InformationBox.Tests;

public class OtpWorkflowTests
{
    private const string FixtureSecret = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";

    [Fact]
    public void PopulatedOtpPanel_RendersCompactAndManualViews_AtSmallWindowSize() => OnSta(() =>
    {
        using var fixture = new Fixture();
        using var vm = new OtpMiniViewModel(fixture.Settings);
        vm.ManualIssuer = "Fixture";
        vm.ManualLabel = "Disposable";
        vm.ManualSecretBase32 = FixtureSecret;
        vm.AddManualCommand.Execute(null);
        var host = new MainWindow();
        try
        {
            host.Resources.MergedDictionaries.Add(new System.Windows.ResourceDictionary
            {
                Source = new Uri("/InformationBox;component/Themes/Light.xaml", UriKind.Relative)
            });
            var view = new UI.Views.OtpPanelView { DataContext = vm, Width = 590, Height = 280 };
            host.Content = view;
            view.Measure(new System.Windows.Size(590, 280));
            view.Arrange(new System.Windows.Rect(0, 0, 590, 280));
            view.UpdateLayout();
            PumpDispatcher();
            Assert.True(view.ActualWidth > 0);
            vm.ToggleManualCommand.Execute(null);
            view.UpdateLayout();
            PumpDispatcher();
            var text = Descendants(view).OfType<System.Windows.Controls.TextBlock>().Select(t => t.Text).ToArray();
            Assert.Contains(vm.SelectedPreset!.Name, text);
        }
        finally { host.ForceClose(); }
    });

    private static IEnumerable<System.Windows.DependencyObject> Descendants(System.Windows.DependencyObject parent)
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    [Fact]
    public void CorruptVault_IsIsolated_AndCanBeReloadedWithoutOverwritingIt() => OnSta(() =>
    {
        using var fixture = new Fixture();
        File.WriteAllText(fixture.VaultPath, "broken fixture");
        using var vm = new OtpMiniViewModel(fixture.Settings);
        Assert.False(vm.IsAvailable);
        Assert.NotEmpty(vm.ErrorMessage);
        Assert.False(vm.AddManualCommand.CanExecute(null));
        Assert.Equal("broken fixture", File.ReadAllText(fixture.VaultPath));

        File.Delete(fixture.VaultPath);
        vm.ReloadVaultCommand.Execute(null);
        Assert.True(vm.IsAvailable);
        Assert.Empty(vm.ErrorMessage);
    });

    [Fact]
    public void ManualAdd_RejectsEmptyDecodedSecrets_AndPersistsValidAccounts() => OnSta(() =>
    {
        using var fixture = new Fixture();
        using var vm = new OtpMiniViewModel(fixture.Settings);
        vm.ManualIssuer = "Fixture";
        vm.ManualLabel = "Disposable";
        foreach (string invalid in new[] { "A", "====", "   " })
        {
            vm.ManualSecretBase32 = invalid;
            vm.AddManualCommand.Execute(null);
            Assert.NotEmpty(vm.ManualErrorMessage);
            Assert.Empty(vm.Accounts);
        }
        vm.ManualSecretBase32 = FixtureSecret;
        vm.AddManualCommand.Execute(null);
        Assert.Single(vm.Accounts);
        Assert.Empty(vm.ManualSecretBase32);
        using var reopened = OtpVaultSession.OpenOrCreate(fixture.VaultPath);
        Assert.Equal("Disposable", Assert.Single(reopened.Vault.Accounts).Label);
    });

    [Fact]
    public void StaleAddAndRemove_LeaveUiAndDiskUnchanged_UntilReload() => OnSta(() =>
    {
        using var fixture = new Fixture();
        using var vm = new OtpMiniViewModel(fixture.Settings);
        vm.ManualIssuer = "Fixture";
        vm.ManualLabel = "Original";
        vm.ManualSecretBase32 = FixtureSecret;
        vm.AddManualCommand.Execute(null);
        var original = Assert.Single(vm.Accounts).Account;
        using (var other = OtpVaultSession.OpenOrCreate(fixture.VaultPath))
            other.AddAccounts(new[] { CreateAccount("Other instance") });
        vm.DeleteAccount(original);
        Assert.Single(vm.Accounts);
        Assert.NotEmpty(original.Secret);
        Assert.Contains("Reload", vm.ErrorMessage);
        vm.ManualIssuer = "Fixture";
        vm.ManualLabel = "Rejected addition";
        vm.ManualSecretBase32 = FixtureSecret;
        vm.AddManualCommand.Execute(null);
        Assert.Single(vm.Accounts);
        Assert.Contains("Reload", vm.ManualErrorMessage);
        using (var reopened = OtpVaultSession.OpenOrCreate(fixture.VaultPath))
        {
            Assert.Equal(2, reopened.Vault.Accounts.Count);
            Assert.DoesNotContain(reopened.Vault.Accounts, a => a.Label == "Rejected addition");
        }
        vm.ReloadVaultCommand.Execute(null);
        Assert.Equal(2, vm.Accounts.Count);
        Assert.Empty(original.Secret);
    });

    [Fact]
    public void RapidHotpCopies_UseDistinctPersistedCounters_AndRefreshImmediately() => OnSta(() =>
    {
        using var fixture = new Fixture();
        using var session = OtpVaultSession.OpenOrCreate(fixture.VaultPath);
        var account = CreateAccount("HOTP", OtpProvisioningProfile.DefaultHotp);
        session.AddAccounts(new[] { account });
        var copied = new List<string>();
        string error = string.Empty;
        var row = new OtpAccountRowViewModel(account, session, text => error = text, copied.Add);
        row.Refresh(0, true);
        row.CopyCommand.Execute(null);
        Assert.Equal("287082", row.DisplayCode);
        row.CopyCommand.Execute(null);
        Assert.Equal(new[] { "755224", "287082" }, copied);
        Assert.Empty(error);
        using var reopened = OtpVaultSession.OpenOrCreate(fixture.VaultPath);
        Assert.Equal(2, Assert.Single(reopened.Vault.Accounts).Counter);
        using (var other = OtpVaultSession.OpenOrCreate(fixture.VaultPath))
            other.AddAccounts(new[] { CreateAccount("Conflict") });
        row.CopyCommand.Execute(null);
        Assert.Equal(2, copied.Count);
        Assert.Equal(2, account.Counter);
        Assert.Contains("Reload", error);
    });

    [Fact]
    public void ClipboardFailure_AfterHotpSave_ReportsConsumptionWithoutRollback() => OnSta(() =>
    {
        using var fixture = new Fixture();
        using var session = OtpVaultSession.OpenOrCreate(fixture.VaultPath);
        var account = CreateAccount("HOTP", OtpProvisioningProfile.DefaultHotp);
        session.AddAccounts(new[] { account });
        string error = string.Empty;
        var row = new OtpAccountRowViewModel(account, session, text => error = text,
            _ => throw new ExternalException("Fixture clipboard unavailable"));
        row.Refresh(0, true);
        row.CopyCommand.Execute(null);
        Assert.Equal(1, account.Counter);
        Assert.Equal("287082", row.DisplayCode);
        Assert.Contains("counter was saved", error);
        using var reopened = OtpVaultSession.OpenOrCreate(fixture.VaultPath);
        Assert.Equal(1, Assert.Single(reopened.Vault.Accounts).Counter);
    });

    [Fact]
    public void ClearAndDispose_PreventLateScanResultsFromReturning() => OnSta(() =>
    {
        using var fixture = new Fixture();
        var scan = new DeferredScan();
        using var vm = new OtpMiniViewModel(fixture.Settings, scan);
        vm.StartAutoScanCommand.Execute(null);
        Assert.True(vm.IsQrBusy);
        vm.ClearQrCommand.Execute(null);
        scan.Complete();
        PumpDispatcher();
        Assert.Empty(vm.QrCandidates);
        Assert.Null(vm.QrPreview);
        Assert.False(vm.IsQrBusy);
        scan.Reset();
        vm.StartAutoScanCommand.Execute(null);
        vm.Dispose();
        scan.Complete();
        PumpDispatcher();
        Assert.Empty(vm.QrCandidates);
        Assert.Null(vm.QrPreview);
        Assert.False(vm.IsAvailable);
    });

    private static Account CreateAccount(string label, OtpProvisioningProfile? profile = null) =>
        OtpProvisioning.CreateAccount("Fixture", label, profile ?? OtpProvisioningProfile.DefaultTotp, FixtureSecret);

    private static void PumpDispatcher() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

    private static void OnSta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
            try { action(); } catch (Exception ex) { error = ex; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "OTP fixture thread timed out.");
        if (error is not null) ExceptionDispatchInfo.Capture(error).Throw();
    }

    private sealed class DeferredScan : IOtpQrScanService
    {
        private TaskCompletionSource<OtpQrScanResult> _completion = new();
        public Task<OtpQrScanResult> AutoScanAsync(CancellationToken ct) => _completion.Task;
        public Task<OtpQrScanResult> ScanRegionAsync(CancellationToken ct) => _completion.Task;
        public void Reset() => _completion = new();
        public void Complete() => _completion.SetResult(new OtpQrScanResult(new[]
        {
            new OtpQrCandidate($"otpauth://totp/Fixture:Disposable?secret={FixtureSecret}&issuer=Fixture", null!, "Fixture")
        }));
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), $"InformationBox-otp-test-{Guid.NewGuid():N}");
        public string VaultPath => Path.Combine(_folder, "fixture.authimo");
        public UserSettings Settings { get; }
        public Fixture()
        {
            Directory.CreateDirectory(_folder);
            Settings = UserSettings.Load(Path.Combine(_folder, "settings.json"));
            Settings.OtpVaultPath = VaultPath;
        }
        public void Dispose() => Directory.Delete(_folder, recursive: true);
    }
}
