# GoodbyeDPI Tray — Kullanım

## Kurulum

Kurucuyu (`GoodbyeDPI-Installer-<sürüm>.exe`) aç, kutudaki sorumluluk metnini okuyup onay kutusunu işaretle ve **Kur**'a bas. Zaten kuruluysa düğmede **Güncelle** yazar. Ardından Windows'un izin penceresini onayla. Yönetici izni yalnızca kurulumda ve kaldırırken istenir.

> **Sorumluluk Reddi:** Uygulama tamamen deneyseldir ve herhangi bir garanti sunmaz. Her internet ağında çalışmayabilir veya bağlantınızı etkileyebilir. Uygulamayı kullanma kararı ve doğabilecek tüm sorumluluk kullanıcının kendisine aittir.

Uygulama varsayılan olarak `C:\Program Files\GoodbyeDPI Tray` klasörüne kurulur ve Başlat menüsüne eklenir. İlk kurulumda **Değiştir** ile başka bir klasör seçebilirsin; o klasörü güvenlik için yalnızca yöneticiler değiştirebilir. Güncellerken uygulama aynı yerde kalır, seçtiğin profil, tema, dil ve başlangıç ayarların korunur.

Daha önce GoodbyeDPI-Turkey'in hizmet kurulum betiğini kullandıysan kurucu o hizmeti bulur ve devralmak için onay ister. Onaylarsan seçtiğin profil korunur, eski klasörü sonra silebilirsin.

![Kurulum ekranı](images/setup.png)

## Ana pencere

![Ana pencere, koyu tema](images/main-dark.png)

- **Başlat / Durdur:** GoodbyeDPI'ı açar ya da kapatır.
- **Profil → Profili uygula:** Seçtiğin profile geçer. GoodbyeDPI çalışıyorsa yeni profille yeniden başlar.
- **Otomatik başlangıç:** **Windows açıldığında GoodbyeDPI'ı başlat** GoodbyeDPI'ı ve tepsi simgesini birlikte açıp kapatır. **Sistem tepsisinde göster** yalnızca simgeyi yönetir; GoodbyeDPI açılışta başlarken simgeyi gizlemek istersen önce sorar.
- **Yeniden başlat:** Bağlantı tuhaflaşınca işe yarar.
- **Konsol:** Motorun canlı çıktısını gösterir.
- **Erişim testi:** GoodbyeDPI'ın işe yarayıp yaramadığını ölçer (ayrıntılar aşağıda).
- **Uygulamayı kaldır:** Kaldırma ekranını açar. İstersen Windows Ayarlar > Uygulamalar'dan da kaldırabilirsin.

Pencereyi kapatınca uygulama saatin yanındaki simgeye küçülür; çift tıklayınca geri gelir. **Sistem tepsisinde göster** seçeneğini kapatırsan simge gizlenir ve pencereyi kapatınca uygulama tamamen kapanır. İki durumda da GoodbyeDPI çalışmaya devam eder, kapatmak için **Durdur**'a bas.

Yeni bir sürüm çıkınca uygulama sana haber verir: saatin yanındaki simgeden bir bildirim gelir, pencerenin altında da **İndir** bağlantısı görünür. Bunun için günde bir kez GitHub'a son sürümü sorar, başka hiçbir bilgi göndermez. **Hakkında** penceresinde güncel olup olmadığını görebilir, **Şimdi kontrol et** ile beklemeden bakabilirsin. Otomatik kontrolü istemezsen aynı pencereden kapatabilirsin.

Başlat, durdur, profil değiştirme ve otomatik başlangıç yönetici izni istemez.

Sağ üstteki **tema** düğmesinden Aydınlık, Karanlık, Okyanus, Sakura, Güneş Batımı, Android ya da Şampanya'yı seçebilirsin; **Windows'a uy** seçilirse tema Windows'un açık/koyu ayarını izler. Hemen solundaki **bayraklı düğmeden** dili Türkçe, İngilizce ya da Rusça yapabilirsin; dil değişince uygulama kendini yeniden açar. Klavyeyle de kullanılabilir: Tab ile gezin, Enter/Boşluk ile bas, profil kutusunda Aşağı ok ile listeyi aç.

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
Lisansları uygulamada **Hakkında → Lisanslar**'dan okuyabilirsin; kurulum klasöründeki `licenses` dizininde de dururlar.
