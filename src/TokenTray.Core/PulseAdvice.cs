namespace TokenTray.Core;

public static class PulseAdvice
{
    public static string For(ProviderSnapshot snapshot, DateTimeOffset now)
    {
        if (!snapshot.IsCurrent(now, TimeSpan.FromMinutes(15))) return "Güncel ölçüm olmadan öneri hesaplanamaz. Önce bağlantıyı yenile.";
        var windows = snapshot.Windows.Where(w => w.ResetsAt is null || w.ResetsAt > now).ToList();
        if (windows.Count == 0) return "Limit yenileniyor. Yeni ölçüm geldiğinde öneri de güncellenecek.";
        var window = windows.OrderByDescending(w => w.UsedPercent).First();
        var label = window.Kind == WindowKind.Session ? "Oturum hakkın" : window.Kind == WindowKind.Weekly ? "Haftalık hakkın" : window.Kind == WindowKind.Period ? "Dönem hakkın" : window.Label;
        string Reset() => window.ResetsAt is { } reset ? $" {reset.ToLocalTime():dd MMM HH:mm} tarihinde yenilenir." : " Servis yenilenme zamanı bildirmedi.";
        if (window.UsedPercent >= 100) return label + " tükendi." + Reset() + " Diğer ürünlerin limitlerini kendi göstergelerinden kontrol et.";
        if (Pace.Of(window, now).ProjectedEmptyAt is { } at && at > now)
            return $"Dönem ortalaması sürerse bu hak {at.ToLocalTime():dd MMM HH:mm} civarında biter. Daha kısa oturumlarla yenilenmeye kadar yayabilirsin. Bu, kullanımına göre değişen bir tahmindir.";
        if (window.UsedPercent >= 95) return $"{label} sınırda; %{window.RemainingPercent:0.#} kaldı. Büyük bir istek için yeterli olmayabilir." + Reset();
        if (window.StartsAt is null) return $"{label} için %{window.RemainingPercent:0.#} kalan hak var. Dönem başlangıcı bildirilmediği için bitiş günü tahmin edilmiyor.";
        if (Pace.Of(window, now).ElapsedPercent < 10 || window.UsedPercent < 10)
            return $"{label} için %{window.RemainingPercent:0.#} kaldı. Güvenilir bir hız tahmini için henüz erken." + Reset();
        return $"Şimdiki dönem ortalamasıyla hakkın yenilenmeye kadar yetiyor." + Reset() + " Kullanım hızın artarsa bu değerlendirme değişir.";
    }
}
