# SGB Pulse 2.2 doğrulama kanıtları

Buradaki görseller sentetik örneklerle üretildi; gerçek hesap verisi, oturum veya e-posta içermez. `TASARIM-KONTROLU.md` son tam tasarım incelemesinin kapsamını ve sınırlarını açıklar.

- Çekirdek testlerin son sonucu: 71 başarılı.
- `smoke-result.json`: kompakt plan rozetleri, ayar kaydetme/yeniden yükleme, eski şema geçişi, üç görünüm ve dar görev çubuğu kontrolleri.
- `native-window/native-result.json`: gösterilmiş gerçek Windows penceresinde hover, fare ayrılması, yenileme sırasında açık kalma, animasyon yolu ve üç yüzen stilin daire geometrisi.
- `native-window/window-start.json`: üst bağlantının başlangıç konumu 0.
- `native-window/window-hover.json`: gerçek fare giriş/ayrılma olayları; yükseklik 40 → 198 → 41. Son küçülme, fare ayrılmasını sağlamak için test penceresi Serbest konuma taşındıktan sonra ölçülür; 1 DIP fark serbest görünümün üst kenarlığıdır.

`native-window` görselleri gösterilmiş üretim WPF pencerelerinin içerik yakalamalarıdır. Diğer PNG'ler üretim bileşenlerinin çizimleridir. Görev çubuğu PNG'leri saydam çizimlerdir, tüm Explorer masaüstünün fotoğrafı değildir. Üstteki örnek veri açıklamasının boşluğu, gerçek pencerenin ekran kenarı boşluğu değildir. %150 görseller farklı DPI çizimini sınar; ikinci bir fiziksel bilgisayar değildir.

İşletim sisteminin hareket tercihi değiştirilmedi. Animasyon yolu yalnız test ortamı delegesiyle etkinleştirildi; normal uygulama Windows hareket tercihine uyar. Gerçek fare testi otomatik başsız CI olarak değerlendirilmemeli.

Gerçek hesap teşhisleri depoya eklenmedi. Başka bilgisayar, çoklu ekran ve bütün Explorer kompozisyonu doğrulanmadı. Kaydedilen test sonuçları 2.2 geliştirme bilgisayarındaki doğrulamadır; yeni makinede tekrar kontrol edilmelidir.
