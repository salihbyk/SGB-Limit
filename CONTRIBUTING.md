# Katkıda bulunma

Küçük, belirli bir sorunu çözen değişikliklerle başlayabilirsin. Güncel davranış için `README.md`, ürün kararları için `PRODUCT.md`, arayüz için `DESIGN.md` dosyalarını oku. Lisans ve TokenTray kaynak bildirimlerini koru.

## Geliştirme ve çekirdek testleri

Windows ve .NET 10 SDK gerekir. Normal uygulamayı çalıştırmadan önce açık SGB Limit'ten çık.

```powershell
dotnet restore .\TokenTray.slnx
dotnet test .\tests\TokenTray.Tests\TokenTray.Tests.csproj -c Release
dotnet build .\src\TokenTray\TokenTray.csproj -c Release -r win-x64
```

Sağlayıcı değişikliklerinde sentetik yanıtlar ve sahte HTTP işleyicileri kullan. Eksik alanları sıfır kota olarak yorumlama. Farklı hesapları ve ürünleri birleştirme. Giriş dosyalarını değiştirme, gerçek sağlayıcıya otomatik test isteği gönderme ve mevcut istek bekleme sürelerini koru.

## Sentetik WPF doğrulaması

`--verify` normal oturum keşfinden önce çalışır. Kişisel hesapları okumadan örnek verilerle çizimleri ve görünüm kontrollerini üretir. WinExe işlemini beklemek ve çıkış kodunu okumak için PowerShell'de şunu kullan:

```powershell
$exe = (Resolve-Path .\src\TokenTray\bin\Release\net10.0-windows10.0.17763.0\win-x64\SGBLimit.exe).Path
$proofDir = Join-Path (Get-Location).Path 'artifacts\synthetic-proof'
$probe = Start-Process -FilePath $exe -ArgumentList @('--verify', ('"{0}"' -f $proofDir)) -WindowStyle Hidden -Wait -PassThru
if ($probe.ExitCode -ne 0) { throw "WPF doğrulaması başarısız: $($probe.ExitCode)" }
```

CI çekirdek testlerini, Release derlemesini ve bu sentetik doğrulamayı çalıştırır. Üretilen görüntüler gerçek WPF çizimleridir. Bunlar görünür pencere, gerçek görev çubuğu ve fare etkileşimi testlerinin yerine geçmez.

## Yerel masaüstü kontrolleri

Görünüm veya yerleşim değiştiğinde aşağıdaki kontrolleri Windows masaüstünde çalıştır. Hepsi sentetik veriler içindir. `$exe` yukarıdaki derlenmiş dosyayı, çıktı yolu ise çalışma klasörünün altındaki tam yolu göstermelidir.

| Seçenek | Kontrol |
| --- | --- |
| `--verify-window <tam-klasör-yolu>` | Gösterilmiş Ada penceresi, fareyle girme/çıkma, boyut ve geçişler. Gerçek fare girişi gerekir. |
| `--verify-alignment <tam-klasör-yolu>` | Gösterilmiş Ada'nın ekran kenarlarına hizalanması. |
| `--verify-taskbar <tam-klasör-yolu>` | Gerçek görev çubuğunda sentetik görünüm, daralma, gizlenme ve geri gelme. Önce çalışan SGB Limit'ten çık. |
| `--verify-settings-window <tam-klasör-yolu>` | Örnek verili ayar penceresinde gerçek tıklama kontrolü. Kaydet, çıktı klasöründeki test dosyasına yazar. |

Bu komutları da `Start-Process -Wait -PassThru` ile çalıştır ve çıkış kodunu denetle. Çıktıları `artifacts/` altında tut. Farklı DPI ölçeklerini ve etkilenen yerleşimleri incele. Hangi Windows, ekran ölçeği ve ekran düzeninde kontrol ettiğini PR'da belirt. Yapmadığın testi yapılmış gibi yazma.

## Pull request ve hata bildirimi

Değişen davranışı, nedenini ve yaptığın kontrolleri kısa biçimde açıkla. Arayüz değişikliğinde yalnız sentetik verili önce/sonra görüntülerini paylaş. Gerçek hesap ekranlarını, kullanıcı profili yollarını, e-posta adreslerini, giriş dosyalarını, önbellekleri veya anahtarları ekleme. `--capture-live` çıktıları katkı kanıtı olarak paylaşılmamalıdır.

Güvenlik açığı için açık issue yerine [SECURITY.md](SECURITY.md) içindeki bildirim yolunu izle.

## GitHub test akışı

Hazır yapılandırma docs/automation/windows-ci.yml içindedir. İlk yayın oturumunun workflow yetkisi olmadığından henüz .github/workflows altında etkin değildir. Yetkili bir bakımcı dosyayı bu konuma taşıyarak Windows test ve sentetik WPF doğrulamasını etkinleştirebilir. Yerel test sonucu GitHub CI sonucu sayılmaz.
