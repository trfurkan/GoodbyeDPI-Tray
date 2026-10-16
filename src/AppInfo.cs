using System;
using System.IO;
using System.Reflection;
[assembly: AssemblyTitle("GoodbyeDPI Tray")]
[assembly: AssemblyDescription("GoodbyeDPI Windows masaüstü uygulaması")]
[assembly: AssemblyProduct("GoodbyeDPI Tray")]
[assembly: AssemblyCompany("Furkan")]
[assembly: AssemblyCopyright("Geliştirici: Furkan. Üçüncü taraf bileşenlerin lisansları saklıdır.")]
[assembly: AssemblyVersion("1.7.2.0")]
[assembly: AssemblyFileVersion("1.7.2.0")]
[assembly: AssemblyInformationalVersion("1.7.2")]
namespace GoodbyeTray {
 static class AppInfo {
  internal const string Version="1.7.2", Developer="Furkan";
  internal const string SourceUrl="https://github.com/cagritaskn/GoodbyeDPI-Turkey", DeveloperUrl="https://github.com/trfurkan";
  internal static string Purpose { get { return L.T("GoodbyeDPI'ı komut satırıyla uğraşmadan açıp kapatmak, profil değiştirmek ve Windows'la birlikte başlatmak için yapıldı."); } }
  internal static string Notice { get { return L.T("Uygulama tamamen deneyseldir ve herhangi bir garanti sunmaz. Her internet ağında çalışmayabilir veya bağlantınızı etkileyebilir. Uygulamayı kullanma kararı ve doğabilecek tüm sorumluluk kullanıcının kendisine aittir."); } }
  internal static string LogPath { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"GoodbyeDPI Tray","Logs","application.log"); } }
  internal static void Log(string message) { try { Directory.CreateDirectory(Path.GetDirectoryName(LogPath)); if(File.Exists(LogPath) && new FileInfo(LogPath).Length>1024*1024) File.WriteAllText(LogPath,""); File.AppendAllText(LogPath,DateTime.Now.ToString("s")+" "+message+Environment.NewLine); } catch {} }
 }
}
