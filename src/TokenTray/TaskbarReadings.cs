using TokenTray.Core;

namespace TokenTray;

/// <summary>Maps each source independently; compact readings share the panel's binding quota.</summary>
internal static class TaskbarReadings
{
    internal static IReadOnlyList<WidgetRenderer.Row> FromSnapshots(IReadOnlyList<ProviderSnapshot> snapshots, bool remaining, DateTimeOffset now, TimeSpan interval)
    {
        interval = interval < TimeSpan.FromSeconds(120) ? TimeSpan.FromSeconds(120) : interval;
        return snapshots.Select(s =>
        {
            var old = s.Windows.Count > 0 && !s.IsCurrent(now, interval);
            // Poller/Seed enforce source + account identity before retaining numbers. Never infer
            // history from a provider name, an unauthenticated snapshot, or a future timestamp.
            var historical = old && s.Provider == ProviderId.Antigravity && s.Stale &&
                !string.IsNullOrWhiteSpace(s.Account) && s.FetchedAt <= now &&
                s.Status is not (SnapshotStatus.NotSignedIn or SnapshotStatus.TokenExpired);
            var main = s.MainWindows.Where(w => w.ResetsAt is null || w.ResetsAt > now).ToList();
            var session = old ? null : main.FirstOrDefault(w => w.Kind is WindowKind.Session or WindowKind.Model);
            var weekly = old ? null : main.FirstOrDefault(w => w != session);
            Reading? Read(UsageWindow? w) => w is null ? null : Reading.Of(w, remaining, now);
            var binding = !old || historical ? Read(s.Binding) : null;
            var next = ProviderSnapshot.NextReset([session, weekly, binding?.Window], now) is { } reset ? reset - now : (TimeSpan?)null;
            var problem = s.Status switch
            {
                SnapshotStatus.Loading => S.Tr ? "okunuyor…" : "reading…",
                SnapshotStatus.NotSignedIn => S.Tr ? "giriş yok" : "not signed in",
                SnapshotStatus.TokenExpired => S.Tr ? "oturum doldu" : "sign-in expired",
                SnapshotStatus.Offline => S.Tr ? "bağlantı yok" : "offline",
                SnapshotStatus.RateLimited when s.RetryAt is { } at && at > now => S.Tr ? $"okuma {S.ShortDuration(at - now)} içinde" : $"reading in {S.ShortDuration(at - now)}",
                SnapshotStatus.RateLimited => S.Tr ? "okunuyor…" : "reading…",
                _ when old => S.Tr ? "Eski veri" : "Old reading",
                _ when s.Windows.Count > 0 => S.Tr ? "Yenileniyor" : "Refreshing",
                _ => S.Tr ? "okunamadı" : "unavailable",
            };
            return new WidgetRenderer.Row(s.Provider, Read(session), Read(weekly),
                session is not null || weekly is not null ? null : problem, next,
                s.Status is SnapshotStatus.Loading or SnapshotStatus.RateLimited, old, s.Name, binding,
                historical ? s.FetchedAt : null);
        }).ToList();
    }
}
