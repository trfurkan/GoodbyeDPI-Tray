# GoodbyeDPI Tray

[![Sürüm](https://img.shields.io/github/v/release/trfurkan/GoodbyeDPI-Tray?label=s%C3%BCr%C3%BCm)](https://github.com/trfurkan/GoodbyeDPI-Tray/releases/latest)
[![İndirme](https://img.shields.io/github/downloads/trfurkan/GoodbyeDPI-Tray/total?label=indirme&color=2ea44f)](https://github.com/trfurkan/GoodbyeDPI-Tray/releases)
[![Lisans](https://img.shields.io/github/license/trfurkan/GoodbyeDPI-Tray?label=lisans)](LICENSE)
![Windows 10 | 11](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4?logo=windows)
![.NET Framework 4.8](https://img.shields.io/badge/.NET%20Framework-4.8-512BD4)

[GoodbyeDPI](https://github.com/ValdikSS/GoodbyeDPI) için Windows sistem tepsisi uygulaması. [GoodbyeDPI-Turkey](https://github.com/cagritaskn/GoodbyeDPI-Turkey) profillerini komut satırı ya da CMD dosyası olmadan başlatır, durdurur ve Windows'la birlikte çalıştırır.

**[⬇️ Son sürümü indir](https://github.com/trfurkan/GoodbyeDPI-Tray/releases/latest)** · [Tanıtım sayfası](https://trfurkan.github.io/GoodbyeDPI-Tray/) · [Kullanım rehberi](docs/KULLANIM.md)

<img src="docs/images/main-dark.png" width="640" alt="GoodbyeDPI Tray ana penceresi">

## Özellikler

- GoodbyeDPI'ı bir Windows hizmeti olarak çalıştırır; başlatma, durdurma ve profil değiştirme **yönetici izni istemez**.
- GoodbyeDPI-Turkey'deki yedi profil, aynı parametrelerle.
- Sistem tepsisi simgesi, görev çubuğu atlama listesi ve Windows açılışında otomatik başlama.
- **Erişim testi:** Engelli ve normal sitelere gerçek HTTPS bağlantısı açıp sonucu gösterir.
- Motorun canlı çıktısını gösteren konsol penceresi.
- Türkçe, İngilizce ve Rusça arayüz; yedi tema.
- Tek dosyalık kurucu; kaldırınca iz bırakmaz.

> [!NOTE]
> GoodbyeDPI Tray bir VPN değildir. Trafik başka bir sunucudan geçmez; yalnızca internet sağlayıcının DPI ve DNS engelleri bilgisayarın içinde atlatılır. Doğrudan IP adresiyle engellenen siteleri açamaz.

## Kurulum

1. [Releases](https://github.com/trfurkan/GoodbyeDPI-Tray/releases/latest) sayfasından `GoodbyeDPI-Installer-<sürüm>.exe` dosyasını indir.
2. Çalıştır. Windows SmartScreen uyarırsa **Ek bilgi → Yine de çalıştır**. Kurucu imzasız olduğu için bu uyarı çıkıyor.
3. Notu onayla ve **Kur**'a bas. Yönetici izni yalnızca kurulumda ve kaldırırken istenir.

Gereksinimler: Windows 10 / 11 (x86 ya da x64), .NET Framework 4.8.

Daha önce GoodbyeDPI-Turkey'in hizmet betiğini kullandıysan kurucu eski hizmeti bulur ve devralmak için onay ister. Kaldırmak için uygulamadaki **Uygulamayı kaldır** ya da Windows Ayarlar > Uygulamalar yeterli.

## Nasıl çalışır?

| Parça | Ne yapar? |
| --- | --- |
| **Hizmet** (`GoodbyeDPI`) | `GoodbyeDPI.Tray.exe --engine-service <profil>` olarak SYSTEM yetkisiyle çalışır, `goodbyedpi.exe`'yi başlatır ve çıktısını `engine-console.log`'a yazar. |
| **Tepsi uygulaması** | Normal kullanıcı yetkisiyle çalışır. Hizmeti başlatır/durdurur; profil ve başlangıç isteklerini hizmete özel komut olarak gönderir, değişikliği hizmet kendisi uygular. |
| **İzinler** | Kurucu, oturum açmış kullanıcılara hizmet için yalnızca başlatma, durdurma ve istek gönderme izni verir. Hizmet ayarlarını değiştirme izni yöneticide kalır. |
| **Kurulum klasörü** | `Program Files` dışına kurulursa klasörü yalnızca yöneticiler ve SYSTEM değiştirebilir. |
| **Güncelleme denetimi** | Günde bir kez GitHub API'den son sürümü sorar; başka bilgi göndermez. Hakkında penceresinden kapatılabilir. |

## Kaynaktan derleme

Visual Studio gerekmez; Windows'la gelen .NET Framework derleyicisi (`csc.exe`) kullanılır.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\build.ps1
```

| Çıktı | Açıklama |
| --- | --- |
| `build\app\GoodbyeDPI.Tray.exe` | Uygulama, hizmet ve kaldırıcı tek dosyada |
| `dist\GoodbyeDPI-Installer.exe` | Bütün dosyaları içinde taşıyan tek dosyalık kurucu |

### Testler

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tests\verify.ps1
```

`tests\` altındaki diğer betikler pencereleri, kurulum akışını, tepsi davranışını ve çevirileri dener. `tests\system\` altındakiler gerçek kurulumu ve hizmeti değiştirir; yalnızca deneme makinesinde çalıştır.

### Proje yapısı

```text
src/                 C# kaynakları (WinForms, .NET Framework 4.8)
  Program.cs         Giriş noktası, hizmet yönetimi
  MainForm.cs        Ana pencere ve tepsi
  EngineService.cs   Windows hizmeti ve motor konsolu
  Setup.cs           Kurucu ve kaldırıcı
  Lang*.cs           Türkçe / İngilizce / Rusça metinler
scripts/             Derleme betikleri
tests/               Test betikleri
vendor/goodbyedpi/   GoodbyeDPI-Turkey 0.2.3rc3 ikili dosyaları ve lisansları
docs/                Kullanım rehberi, sürüm notları, tanıtım sayfası (GitHub Pages)
```

## Katkı ve geri bildirim

Hata ya da öneri için [issue aç](https://github.com/trfurkan/GoodbyeDPI-Tray/issues/new). Hata bildirirken uygulama günlüğünü (`%LOCALAPPDATA%\GoodbyeDPI Tray\Logs\application.log`) eklemen sorunu bulmayı kolaylaştırır.

Proje işine yaradıysa ⭐ vererek destek olabilirsin.

## Hakkımda

<a href="https://github.com/trfurkan"><img src="https://avatars.githubusercontent.com/u/47176772?s=96&v=4" width="48" height="48" align="left" alt="trfurkan"></a>

Merhaba, ben Furkan. Boş vakitlerimde hobi olarak hayatı kolaylaştıran, sade ve kullanıcı dostu projeler geliştiriyorum. Profesyonel bir iddiam yok; ürettiğim çözümler işinize yaradıysa ne mutlu bana. İlginiz ve desteğiniz için teşekkürler!

## Lisans ve teşekkürler

Uygulama kodu [Apache 2.0](LICENSE) lisanslıdır. Paketteki üçüncü taraf bileşenler kendi lisanslarını korur; ayrıntılar [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) dosyasında.

- [GoodbyeDPI](https://github.com/ValdikSS/GoodbyeDPI) — ValdikSS
- [GoodbyeDPI-Turkey](https://github.com/cagritaskn/GoodbyeDPI-Turkey) — cagritaskn
- [WinDivert](https://github.com/basil00/WinDivert) — basil00

> [!WARNING]
> Uygulama tamamen deneyseldir ve herhangi bir garanti sunmaz. Her internet ağında çalışmayabilir veya bağlantını etkileyebilir. Kullanma kararı ve doğabilecek tüm sorumluluk kullanıcıya aittir. GoodbyeDPI Tray bağımsız bir projedir; ValdikSS/GoodbyeDPI ya da cagritaskn/GoodbyeDPI-Turkey'in resmî sürümü değildir.
