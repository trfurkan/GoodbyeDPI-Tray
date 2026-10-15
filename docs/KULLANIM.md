# GoodbyeDPI Tray — Kullanım

## Kurulum

`GoodbyeDPI-Installer.exe`'yi aç, kutudaki sorumluluk metnini okuyup onay kutusunu işaretle ve **Kur**'a bas. Zaten kuruluysa düğmede **Güncelle** yazar. Ardından Windows'un izin penceresini onayla.

> **Sorumluluk Reddi:** Uygulama tamamen deneyseldir ve herhangi bir garanti sunmaz. Her internet ağında çalışmayabilir veya bağlantınızı etkileyebilir. Uygulamayı kullanma kararı ve doğabilecek tüm sorumluluk kullanıcının kendisine aittir.

Uygulama `C:\Program Files\GoodbyeDPI Tray` klasörüne kurulur ve Başlat menüsüne eklenir. Güncellerken seçtiğin profil ve başlangıç ayarların korunur.

![Kurulum ekranı](images/setup.png)

## Ana pencere

![Ana pencere, koyu tema](images/main-dark.png)

- **Başlat / Durdur:** GoodbyeDPI'ı açar ya da kapatır.
- **Profil → Profili uygula:** Seçtiğin profile geçer. GoodbyeDPI çalışıyorsa yeni profille yeniden başlar.
- **Otomatik başlangıç:** GoodbyeDPI'ın Windows'la birlikte çalışmasını ve simgenin saatin yanında açılmasını ayrı ayrı seçebilirsin.
- **Yeniden başlat:** Bağlantı tuhaflaşınca işe yarar.
- **Konsol:** Motorun canlı çıktısını gösterir .
- **Erişim testi:** GoodbyeDPI'ın işe yarayıp yaramadığını ölçer (ayrıntılar aşağıda).
- **Uygulamayı kaldır:** Kaldırma ekranını açar. İstersen Windows Ayarlar > Uygulamalar'dan da kaldırabilirsin.

Pencereyi kapatınca uygulama saatin yanındaki simgeye küçülür; çift tıklayınca geri gelir. Uygulamadan çıkmak GoodbyeDPI'ı kapatmaz, kapatmak için **Durdur**'a bas.

Sağ üstteki **Tema** düğmesinden Aydınlık, Karanlık ya da Sistem seçebilirsin. Klavyeyle de kullanılabilir: Tab ile gezin, Enter/Boşluk ile bas, profil kutusunda Aşağı ok ile listeyi aç.

## Profiller

Yedi profil, GoodbyeDPI-Turkey'deki orijinal CMD dosyalarıyla birebir aynı ayarları kullanır.

| Profil | DNS'i yönlendirir mi? |
| --- | --- |
| Türkiye · Standart | Evet |
| Alternatif 1 · TTL 3 | Hayır |
| Alternatif 2 · TTL yok | Hayır |
| Alternatif 3 · TTL 3 + DNS | Evet |
| Alternatif 4 · TTL yok + DNS | Evet |
| Alternatif 5 · Mod 9 + DNS | Evet |
| Alternatif 6 · Mod 9 | Hayır |

Önce **Türkiye · Standart**'ı dene. Bazı siteler açılmazsa ya da yavaşsa alternatiflere geç; TTL içermeyen 2 ve 4 numaralı alternatifler genelde yavaşlık sorununu çözer.

DNS'i yönlendirmeyen profillerde internet sağlayıcının DNS'i engelli siteleri engel sayfasına gönderir. Bu profilleri kullanıyorsan Windows'tan DNS'ini değiştirmen gerekir (ör. 1.1.1.1 veya 77.88.8.8).

"Çalışıyor" yazması GoodbyeDPI'ın açık olduğunu gösterir, her sitenin açılacağı anlamına gelmez. Bunu görmek için Erişim testini kullan.

## Erişim testi

![Erişim testi](images/access-dark.png)


Test her siteye gerçek bir HTTPS bağlantısı açar. Ping kullanılmaz, çünkü engel IP düzeyinde değil site adı üzerinden uygulanıyor ve engelli bir site bile ping'e cevap verebiliyor. Her test turundan önce Windows'un DNS önbelleği temizlenir; böylece GoodbyeDPI kapalıyken kaydedilmiş eski, engelli adresler sonucu bozmaz.

En alttaki durum satırı sonucu özetler: engelli siteler açılmıyorsa başka bir profil denemeni, normal siteler de açılmıyorsa internet bağlantını kontrol etmeni, GoodbyeDPI kapalıysa bunu söyler.

Tarayıcında bir site açılırken testte kırmızı görünebilir. Bazı tarayıcılar (Firefox, Zen, Chrome) kendi güvenli DNS'ini ve şifreli site adını (ECH) kullanarak engeli GoodbyeDPI olmadan aşabilir. Test ise Windows'un normal bağlantısını ölçer; Discord uygulaması ve oyunlar gibi diğer programlar da bu bağlantıyı kullanır.

## Tepsi ve görev çubuğu

Saatin yanındaki simgeye sağ tıklayarak profil değiştirebilir, GoodbyeDPI'ı başlatıp durdurabilirsin. Görev çubuğundaki simgeye sağ tıklayınca da aynı kısayollar çıkar.


## Sorun çıkarsa

- **Hiçbir site açılmıyor:** Erişim testini çalıştır. Google ve Wikipedia da kırmızıysa sorun internet bağlantında.
- **Engelli siteler hâlâ açılmıyor:** Önce DNS'i yönlendiren bir profil dene (Standart, Alternatif 3/4/5), sonra diğerlerini.

Hatalar `%LOCALAPPDATA%\GoodbyeDPI Tray\Logs\application.log` dosyasına kaydolur.

## Kaynak ve Lisans

Motor: [GoodbyeDPI-Turkey](https://github.com/cagritaskn/GoodbyeDPI-Turkey) 0.2.3rc3.  
Lisans detayları kurulum klasöründeki `licenses` dizininde yer almaktadır.
