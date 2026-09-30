using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CodeGlyphX;
using System.Security.Cryptography;
using Screen = System.Windows.Forms.Screen;

namespace InformationBox.Services;

internal interface IOtpQrScanService
{
    Task<OtpQrScanResult> AutoScanAsync(CancellationToken ct);
    Task<OtpQrScanResult> ScanRegionAsync(CancellationToken ct);
}

internal sealed class OtpQrScanService : IOtpQrScanService
{
    private const int AutoScanDurationMs = 2500;
    private const int AutoScanDelayMs = 200;

    public Task<OtpQrScanResult> AutoScanAsync(CancellationToken ct) => Task.Run(() => AutoScanCoreAsync(ct), ct);

    private static async Task<OtpQrScanResult> AutoScanCoreAsync(CancellationToken ct)
    {
        var candidates = new List<OtpQrCandidate>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < AutoScanDurationMs && !ct.IsCancellationRequested)
        {
            foreach (var screen in Screen.AllScreens)
            {
                ct.ThrowIfCancellationRequested();
                CaptureAndDecode(screen.Bounds, screen, candidates, seen, ct);
            }

            if (candidates.Count > 0)
            {
                break;
            }

            await Task.Delay(AutoScanDelayMs, ct);
        }

        return new OtpQrScanResult(candidates);
    }

    public async Task<OtpQrScanResult> ScanRegionAsync(CancellationToken ct)
    {
        if (!ScreenRegionPicker.TryPick(out var region))
        {
            return new OtpQrScanResult(Array.Empty<OtpQrCandidate>());
        }

        var candidates = new List<OtpQrCandidate>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        await Task.Run(() => CaptureAndDecode(region, null, candidates, seen, ct), ct);

        return new OtpQrScanResult(candidates);
    }

    private static void CaptureAndDecode(Rectangle region, Screen? sourceScreen, List<OtpQrCandidate> candidates, HashSet<string> seen, CancellationToken ct)
    {
        if (region.Width <= 0 || region.Height <= 0)
        {
            return;
        }

        using var capture = new ScreenCapture.Session(region.Width, region.Height);
        capture.Capture(region.X, region.Y);

        var pixels = capture.Buffer;
        var stride = capture.Stride;

        if (!QrImageDecoder.TryDecodeAll(
                pixels,
                region.Width,
                region.Height,
                stride,
                CodeGlyphX.PixelFormat.Bgra32,
                options: null,
                ct,
                out var decoded))
        {
            return;
        }

        if (decoded.Length == 0)
        {
            return;
        }

        var label = sourceScreen is null
            ? $"Region {region.Width}x{region.Height}"
            : $"{sourceScreen.DeviceName} {region.Width}x{region.Height}";

        try
        {
            foreach (var item in decoded)
            {
                ct.ThrowIfCancellationRequested();
                var text = item.Text ?? string.Empty;
                if (text.Length == 0 || !IsOtpPayload(text)) continue;
                if (seen.Add(text))
                    candidates.Add(new OtpQrCandidate(text, CreatePreviewImage(text), label));
            }
        }
        finally
        {
            foreach (var item in decoded) CryptographicOperations.ZeroMemory(item.Bytes);
        }
    }

    private static bool IsOtpPayload(string text)
    {
        return text.StartsWith("otpauth://", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("otpauth-migration://", StringComparison.OrdinalIgnoreCase);
    }

    private static ImageSource CreatePreviewImage(string payload)
    {
        // Render only the decoded symbol. Do not retain unrelated desktop pixels.
        var modules = QR.Encode(payload).Modules;
        const int quietZone = 4;
        int width = modules.Width + quietZone * 2;
        byte[] pixels = new byte[width * width];
        Array.Fill(pixels, (byte)255);
        for (int y = 0; y < modules.Height; y++)
            for (int x = 0; x < modules.Width; x++)
                if (modules[x, y]) pixels[(y + quietZone) * width + x + quietZone] = 0;
        var source = BitmapSource.Create(width, width, 96, 96, PixelFormats.Gray8, null, pixels, width);
        source.Freeze();
        CryptographicOperations.ZeroMemory(pixels);
        return source;
    }

}

internal sealed class OtpQrScanResult
{
    public IReadOnlyList<OtpQrCandidate> Candidates { get; }

    public OtpQrScanResult(IReadOnlyList<OtpQrCandidate> candidates)
    {
        Candidates = candidates;
    }
}

internal sealed class OtpQrCandidate
{
    public string Payload { get; }
    public ImageSource PreviewImage { get; }
    public string SourceLabel { get; }

    public OtpQrCandidate(string payload, ImageSource previewImage, string sourceLabel)
    {
        Payload = payload;
        PreviewImage = previewImage;
        SourceLabel = sourceLabel;
    }
}
