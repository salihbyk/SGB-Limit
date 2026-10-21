using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media.Imaging;
using TokenTray.Core;

namespace TokenTray;

internal static class SignalReadingProbe
{
    internal static void Verify(string folder)
    {
        Directory.CreateDirectory(folder);
        var now = DateTimeOffset.Now;
        var palette = new Palette(true);
        void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
        BitmapSource Render(double used, bool remaining, int bars)
        {
            var window = new UsageWindow(WindowKind.Weekly, "Weekly", used, now.AddDays(2), TimeSpan.FromDays(7));
            var row = new WidgetRenderer.Row(ProviderId.Claude, null, Reading.Of(window, remaining, now), null, null);
            return WidgetRenderer.Render([row], new(WidgetStyle.Signal, false, true, true, false, remaining, SignalBars: bars), palette, 1, 48, false, out _);
        }
        byte[] BarPixels(BitmapSource bitmap)
        {
            var pixels = new byte[30 * 19 * 4];
            bitmap.CopyPixels(new Int32Rect(30, 15, 30, 19), pixels, 30 * 4, 0);
            return pixels;
        }
        bool Equal(BitmapSource a, BitmapSource b)
        {
            if (a.PixelWidth != b.PixelWidth || a.PixelHeight != b.PixelHeight) return false;
            var one = new byte[a.PixelWidth * a.PixelHeight * 4];
            var two = new byte[one.Length];
            a.CopyPixels(one, a.PixelWidth * 4, 0); b.CopyPixels(two, b.PixelWidth * 4, 0);
            return one.SequenceEqual(two);
        }
        var sample = new ProviderSnapshot(ProviderId.Claude, SnapshotStatus.Ok,
            [new(WindowKind.Session, "5h", 33, now.AddHours(2), TimeSpan.FromHours(5)),
             new(WindowKind.Weekly, "Weekly", 18, now.AddDays(2), TimeSpan.FromDays(7)),
             new(WindowKind.WeeklyModel, "Fable", 44, now.AddDays(2), TimeSpan.FromDays(7))], now);
        WidgetRenderer.Row Map(ProviderSnapshot snapshot, bool remaining = false) =>
            TaskbarReadings.FromSnapshots([snapshot], remaining, now, TimeSpan.FromMinutes(2)).Single();
        BitmapSource RenderRow(WidgetRenderer.Row row, bool remaining, TaskbarDensity density = TaskbarDensity.Full, bool session = true, bool weekly = true) =>
            WidgetRenderer.Render([row], new(WidgetStyle.Signal, false, session, weekly, false, remaining), palette, 1, 48, false, out _, density: density);
        foreach (var remaining in new[] { false, true })
        {
            var row = Map(sample, remaining);
            Check(row.Binding?.Window == sample.Binding && row.Binding?.Percent == (remaining ? 56 : 44),
                "Taskbar binding must match the panel's model-specific 44% used / 56% remaining quota.");
            var expected = row with { Session = null, Weekly = null };
            foreach (var density in new[] { TaskbarDensity.Full, TaskbarDensity.Icons })
            {
                Check(Equal(RenderRow(row, remaining, density), RenderRow(expected, remaining, density)),
                    "Signal and compact icons must render the binding number, independently of the first two main windows.");
                Check(Equal(RenderRow(row, remaining, density), RenderRow(row, remaining, density, false, true)),
                    "Hiding the session detail must not replace the primary binding quota.");
                // Flip a row made in the other mode to catch a cached Binding.Percent.
                Check(Equal(RenderRow(row, remaining, density), RenderRow(Map(sample, !remaining), remaining, density)),
                    "Changing numeric mode recomputes the binding reading before rendering.");
            }
        }
        foreach (var bars in new[] { 3, 5 })
        {
            Check(!BarPixels(Render(0, false, bars)).SequenceEqual(BarPixels(Render(0, true, bars))),
                "Signal bars must follow used/remaining mode: 0% used is empty, 100% remaining is full.");
            Check(!BarPixels(Render(0.1, false, bars)).SequenceEqual(BarPixels(Render(19, false, bars))),
                "A tiny quota fraction must not be rounded up to the same complete first bar as 19%.");
            foreach (var fraction in new[] { 0, .001, .1, .19999, .2, .20001, 1d / 3, .5, .999, 1 })
            {
                var fills = Enumerable.Range(0, bars).Select(i => WidgetRenderer.SignalBarAmount(fraction, bars, i)).ToArray();
                Check(Math.Abs(fills.Sum() / bars - fraction) < 1e-10,
                    "The sum of partial signal bars must preserve the displayed quota fraction at every boundary.");
                Check(fills.All(f => f is >= 0 and <= 1), "Every signal bar must stay within its own bounds.");
            }
            MediaRenderer.Save(Render(44, false, bars), Path.Combine(folder, $"signal-{bars}-used.png"));
            MediaRenderer.Save(Render(44, true, bars), Path.Combine(folder, $"signal-{bars}-remaining.png"));
        }
        var remembered = new ProviderSnapshot(ProviderId.Antigravity, SnapshotStatus.Offline,
            [new(WindowKind.Model, "Claude", 44, now.AddHours(2), null)], now.AddMinutes(-8), Stale: true, Key: "Antigravity:work", Account: "work-account");
        var rememberedRow = Map(remembered);
        Check(rememberedRow is { Stale: true, ReadAt: not null, Binding.Percent: 44 },
            "An identified retained Antigravity snapshot exposes a historical binding reading.");
        var missing = Map(remembered with { Windows = [] });
        foreach (var density in new[] { TaskbarDensity.Full, TaskbarDensity.Icons })
        {
            Check(!Equal(RenderRow(rememberedRow, false, density), RenderRow(missing, false, density)),
                "A retained Antigravity quota must visibly differ from an unavailable quota.");
            Check(!Equal(RenderRow(rememberedRow, false, density), RenderRow(Map(remembered with { Status = SnapshotStatus.Ok, Stale = false, FetchedAt = now }), false, density)),
                "A historical reading must not look fresh: use asterisk, muted quota, and amber status dot.");
        }
        foreach (var untrusted in new[] {
            remembered with { Account = null }, remembered with { Account = " " },
            remembered with { Status = SnapshotStatus.NotSignedIn }, remembered with { Status = SnapshotStatus.TokenExpired },
            remembered with { FetchedAt = now.AddMinutes(1) }, remembered with { Stale = false },
            remembered with { Provider = ProviderId.Codex } })
            Check(Map(untrusted).Binding is null, "Sign-out, unknown identity, future timestamps, and other stale providers must not expose retained taskbar percentages.");
        var expired = remembered with { Windows = [remembered.Windows[0] with { ResetsAt = now.AddMinutes(-1) }] };
        Check(Map(expired).Binding is { PastReset: true, Fraction: 0 }, "A past-reset historical value never gets a fresh quota bar.");
        Check(BarPixels(RenderRow(Map(expired), false)).SequenceEqual(BarPixels(RenderRow(missing, false))),
            "Past-reset historical taskbar bars stay empty.");
        Check(!Equal(RenderRow(Map(expired), false), RenderRow(missing, false)), "Past-reset history stays explicitly marked as a previous measurement.");
        var separate = TaskbarReadings.FromSnapshots([remembered, remembered with { Key = "Antigravity:personal", Account = "other-account", Windows = [] }], false, now, TimeSpan.FromMinutes(2));
        Check(separate[0].Binding?.Percent == 44 && separate[1].Binding is null,
            "A second source must never inherit another account's retained reading.");
        MediaRenderer.Save(RenderRow(rememberedRow, false), Path.Combine(folder, "signal-last-reading.png"));
        MediaRenderer.Save(RenderRow(Map(expired), false), Path.Combine(folder, "signal-expired-last-reading.png"));
        File.WriteAllText(Path.Combine(folder, "signal-result.json"), JsonSerializer.Serialize(new { passed = true, data = "synthetic", bindingUsed = 44, bindingRemaining = 56, barCounts = new[] { 3, 5 }, checks = new[] { "binding render", "numeric mode", "partial bar boundaries", "retained account", "historical style", "past reset", "source isolation" } }));
    }
}
