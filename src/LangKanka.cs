using System.Collections.Generic;

namespace GoodbyeTray {
 // Default Turkish voice, in the spirit of Discord's: plain where people act or decide, light where they wait or win.
 // - Plain (no entry here, the text in the code is shown): buttons, menus, settings, titles, consent, errors, warnings.
 //   Anything that cannot be undone says so plainly.
 // - Light, short and never at the cost of clarity: success notices, loading and progress lines, empty states,
 //   the setup's welcome and goodbye.
 // An entry only exists where the shown Turkish differs from the source text. Keep source keys exactly as written in the code.
 static partial class L {
  static readonly Dictionary<string,string> Kanka=new Dictionary<string,string> {
   //  Success: a short, warm line
   {"Profil uygulandı.","Tamamdır! Yeni profil devrede."},
   {"GoodbyeDPI başlatıldı.","GoodbyeDPI iş başında."},
   {"GoodbyeDPI durduruldu.","GoodbyeDPI durduruldu. Lazım olunca bir tık uzağında."},
   {"DNS sunucusu değişti.","Tamamdır! Yeni DNS devrede."},
   {"Tema kaydedildi.","Yeni tema yakıştı. Kaydettik."},
   {"Tepsi simgesi açıldı, oturum açınca da gelecek.","Simge saatin yanında. Oturum açınca da orada olacak."},
   {"Tepsi simgesi gizlendi.","Simge saklandı. Uygulamaya Başlat menüsünden ulaşabilirsin."},
   {"Güncel sürümü kullanıyorsun.","Her şey güncel. En son sürümdesin."},
   {"Yeni sürüm var: ","Yeni sürüm geldi: "},
   {" çıktı"," çıktı!"},
   {"Panoya kopyalandı.","Kopyalandı! Yapıştırman yeter."},
   {"Kurulum tamamlandı","Kurulum tamam!"},
   {"GoodbyeDPI hazır. Şimdi açabilir ya da sonra Başlat menüsünden bulabilirsin.","Hoş geldin! GoodbyeDPI hazır; şimdi açabilir ya da sonra Başlat menüsünden bulabilirsin."},
   {"GoodbyeDPI Tray bilgisayarından tamamen kaldırıldı.","Görüşürüz! GoodbyeDPI Tray bilgisayarından tamamen kaldırıldı."},
   //  Cannot be undone: said first and plainly
   {"GoodbyeDPI Tray, ayarları ve günlükleriyle birlikte bilgisayarından tamamen kaldırılacak.","Bu işlem geri alınamaz. GoodbyeDPI Tray, ayarları ve günlükleriyle birlikte bilgisayarından tamamen kaldırılacak."},
   //  Errors: calm and clear; only the generic title is a little softer
   {"İşlem tamamlanamadı","Bir şeyler ters gitti"},
   //  Waiting: loading lines and progress steps
   {"Test ediliyor…","Deniyoruz…"},
   {"Konsol çıktısı okunuyor…","Konsolu okuyoruz…"},
   {"Motor başlatılıyor…","Motor ısınıyor…"},
   {"Bilgiler toplanıyor…","Bilgileri topluyoruz…"},
   {"Kurulum dosyaları kontrol ediliyor…","Dosyaları bir yokluyoruz…"},
   {"Uygulama güncellemeye hazırlanıyor…","Güncelleme için hazırlanıyoruz…"},
   {"Uygulama dosyaları hazırlanıyor…","Dosyalar yerine konuyor…"},
   {"Windows kaydı oluşturuluyor…","Windows'a tanıtıyoruz…"},
   {"GoodbyeDPI hizmeti hazırlanıyor…","Hizmeti ayağa kaldırıyoruz…"},
   {"Kurulum tamamlanıyor…","Son rötuşlar…"},
   {"Kaldırma hazırlanıyor…","Toparlanıyoruz…"},
   {"Uygulama dosyaları kaldırılıyor…","Dosyaları topluyoruz…"},
   {"Kaldırma tamamlanıyor…","Kapıyı kapatıyoruz…"},
   {"Kısayollar temizleniyor…","Kısayolları siliyoruz…"},
   {"Kısayollar hazırlanıyor…","Kısayolları diziyoruz…"},
   //  Empty states and friendly notes
   {"Henüz çıktı yok. Motoru başlatınca burada görünecek.","Burası şimdilik sessiz. Motoru başlatınca burası şenlenir."},
   {"Siteler sadece sen test edince denenir; sonuçlar hiçbir yere gönderilmez.","Sen başlat, biz kontrol edelim. Test sonuçları hiçbir yere gönderilmez."},
   {"Temayı (Karanlık/Aydınlık) sistem tercihlerine göre otomatik ayarlar.","Windows hangi temadaysa biz de ona uyarız."},
   //  Shorter, plainer wording for the main window and the access test
   {"Windows açıldığında GoodbyeDPI'ı başlat","Windows açılınca başlat"},
   {"Yalnızca engelli sitelere uygula","Seçili sitelerde uygula"},
   {"Bağlantı ayarları","Bağlantı"},
   {"Ayrıntılı test","Detaylı test"},
   {"Sitelerin bu profille açılıp açılmadığını dene.","Siteler bu profille açılıyor mu?"},
   {"Varsayılan liste: Discord, Roblox ve Wattpad. Siteler bölümünden düzenleyebilirsin.","Açıkken GoodbyeDPI yalnızca listedeki sitelere karışır."},
   {"Discord gibi siteler tarayıcıda açılmıyorsa açık kalsın. Yalnızca GoodbyeDPI çalışırken etkilidir.","Erişimi kısıtlı siteler tarayıcıda açılmıyorsa açık tut. Yalnızca GoodbyeDPI çalışırken etkilidir."},
   {"Simge saatin yanında durur, oturum açınca da kendiliğinden gelir. Pencereyi kapatınca simgeye küçülür.","Pencereyi kapatınca uygulama saatin yanındaki simgeye küçülür."},
   {"Tümünü Test Et","Hepsini dene"},
   {"Durum: Testi başlatmak için Tümünü Test Et'e ya da bir sitenin yanındaki yenile simgesine bas.","Durum: Başlamak için Hepsini dene'ye ya da bir sitenin yanındaki yenile simgesine bas."},
   {"Erişilebilir","Açılıyor"},
   {"Zaman Aşımı","Cevap yok"},
   {"Adres çözülemedi","Adres bulunamadı"},
   {"Bağlantı kesildi","Bağlantı koptu"},
   {"Test Yapılmadı","Henüz denenmedi"},
   {"Profilleri senin hattında sırayla dener.","Profilleri senin hattında tek tek dener."},
   {" site, tek tek sonuç ve süre"," site, her birinin sonucu ve süresi"},
  };
  internal static bool HasKanka(string turkish) { return Kanka.ContainsKey(turkish); }
  internal static IEnumerable<string> KankaKeys { get { return Kanka.Keys; } }
 }
}
