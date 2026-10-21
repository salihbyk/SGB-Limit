# Güvenlik bildirimi / Security reporting

SGB Limit'in güncel sürümündeki güvenlik sorunlarını bildirebilirsin. Proje için bağımsız güvenlik denetimi veya belirli bir müdahale süresi taahhüdü bulunmuyor.

## Özel bildirim

GitHub'da [özel güvenlik bildirimi](https://github.com/salihbyk/SGB-Limit/security/advisories/new) açık olduğunda bu yolu kullan. Depodaki **Security → Report a vulnerability** seçeneği de aynı özel bildirimi açar. Seçenek görünmüyorsa veya bağlantı kullanılamıyorsa açık issue'ya yalnız özel bildirim yolunun açılmasını isteyen kısa bir not yaz. Açığın ayrıntılarını herkese açık paylaşma.

Bildirime etkilenen sürümü, beklenen ve gerçekleşen davranışı, mümkünse sentetik verilerle yeniden üretme adımlarını ekle. Açığı anlatmak için gerçek hesaplara erişim sağlaman gerekmez.

**Parola, erişim/yenileme anahtarı, çerez, kimlik doğrulama dosyası, gerçek e-posta adresi veya hesap önbelleği gönderme.** Ekran görüntülerinde bu bilgileri kaldır. Yanlışlıkla bir anahtar paylaşıldıysa ilgili sağlayıcı üzerinden iptal et veya yenile.

## Güven sınırları

Uygulama mevcut yerel oturumları yalnız okur ve kota için sağlayıcı API'lerine veya yerel IDE servisine istek gönderir. Yerel önbellekler hesap ve kullanım bilgisi içerebilir. Bir SHA-256 eşleşmesi yalnız dosya bütünlüğünü kontrol eder. Kod imzasının veya güvenlik incelemesinin yerine geçmez.

## English

Use [GitHub private vulnerability reporting](https://github.com/salihbyk/SGB-Limit/security/advisories/new) when enabled. If unavailable, open a public issue asking only for a private reporting channel, without vulnerability details. Include affected versions and synthetic reproduction steps in the private report. Never share passwords, tokens, cookies, authentication files, real email addresses or account caches. No independent security audit or response-time guarantee is claimed.
