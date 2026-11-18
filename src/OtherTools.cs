using System;
using System.Diagnostics;
using System.ServiceProcess;
using System.Windows.Forms;
using Microsoft.Win32;

namespace GoodbyeTray {
 // Other DPI tools (another app's engine, a service, a .cmd profile) that clash with ours: two engines on the same
 // traffic break each other. Finds them with what a normal user may read and says how to turn them off.
 static class OtherTools {
  internal enum Kind { Script, Service, App }
  internal sealed class Conflict { internal string Name,Service; internal Kind Kind; internal bool Running; }
  static readonly string[] Engines={"goodbyedpi","winws","winws2"};
  static readonly string[] Marks={"splitwire","zapret","winws","goodbyedpi"};

  // An engine another tool runs right now, or null.
  internal static Conflict Running() {
   bool oursRunning=OurServiceRunning();
   foreach(string engine in Engines) foreach(var process in Process.GetProcessesByName(engine)) using(process) {
    try { if(process.HasExited) continue; } catch(Exception) { } // A SYSTEM process cannot be opened; it is still running.
    var owner=EngineOwner.Find(process.Id);
    if(owner!=null && owner.Ours) continue;
    // With no owner found, a running service of ours most likely started it (for example while it switches profiles).
    if(owner==null && oursRunning) continue;
    return Make(owner==null?engine:owner.Name,owner==null?null:owner.Service,owner!=null && owner.Script,engine,true);
   }
   return null;
  }
  // A service of another tool set to start with Windows, even if it is not running now; null when there is none.
  internal static Conflict Installed() {
   ServiceController[] services;
   try { services=ServiceController.GetServices(); } catch(Exception) { return null; }
   try {
    foreach(var service in services) {
     if(String.Equals(service.ServiceName,Program.ServiceName,StringComparison.OrdinalIgnoreCase)) continue;
     string image=ImagePath(service.ServiceName); if(image==null) continue;
     string text=(service.ServiceName+" "+image).ToLowerInvariant();
     if(!Array.Exists(Marks,m => text.Contains(m))) continue;
     // WinDivert's own driver service is shared by every tool, ours included.
     if(text.Contains("windivert") && !text.Contains("winws") && !text.Contains("goodbyedpi.exe")) continue;
     bool running; try { running=service.Status==ServiceControllerStatus.Running || service.Status==ServiceControllerStatus.StartPending; } catch(Exception) { running=false; }
     if(!running && StartType(service.ServiceName)!=2) continue; // 2: starts with Windows
     string name; try { name=service.DisplayName; } catch(Exception) { name=service.ServiceName; }
     return Make(name,service.ServiceName,false,image,running);
    }
   } finally { foreach(var service in services) service.Dispose(); }
   return null;
  }
  static bool OurServiceRunning() { try { using(var service=new ServiceController(Program.ServiceName)) return service.Status!=ServiceControllerStatus.Stopped; } catch(Exception) { return false; } }
  static string ImagePath(string service) { try { using(var key=Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\"+service)) return key==null?null:key.GetValue("ImagePath") as string; } catch(Exception) { return null; } }
  static int StartType(string service) { try { using(var key=Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\"+service)) return key==null?-1:Convert.ToInt32(key.GetValue("Start",-1)); } catch(Exception) { return -1; } }
  internal static Conflict Make(string name,string service,bool script,string clue,bool running) {
   Kind kind=script?Kind.Script:service!=null?Kind.Service:Kind.App;
   return new Conflict { Name=String.IsNullOrWhiteSpace(name)?clue:name.Trim(),Service=service,Kind=kind,Running=running };
  }
  // The headline and the way out. No product is named: the user knows which tool they installed, and the advice is
  // the same for all of them. The name only goes to the log.
  internal static string Message(Conflict c) {
   string why=c.Running?L.T("GoodbyeDPI Tray ile aynı anda çalışırsa ikisi de düzgün çalışmaz."):L.T("Bilgisayar açılınca kendiliğinden başlayabilir ve GoodbyeDPI Tray ile çakışabilir.");
   string fix=c.Kind==Kind.Script?L.T("Motoru bir .bat ya da .cmd dosyası başlatmış. Açık komut penceresini kapatman yeterli."):c.Kind==Kind.Service?L.T("Bir Windows hizmeti olarak çalışıyor. Onu kuran uygulamadan kapat ya da Hizmetler penceresinden durdur; artık kullanmayacaksan uygulamayı kaldır."):L.T("Onu başlatan uygulamayı kapat ya da kaldır, sonra tekrar dene.");
   return Short(c)+"\n\n"+why+" "+fix;
  }
  internal static string Short(Conflict c) { return c.Running?L.T("Başka bir DPI motoru çalışıyor"):L.T("Başka bir DPI motoru kurulu"); }
  internal static void Show(IWin32Window owner,Conflict c) {
   Action services=null; if(c.Service!=null) services=delegate { Process.Start(new ProcessStartInfo("services.msc") { UseShellExecute=true }); };
   AppMessage.Show(owner,Message(c),L.T("Çakışan uygulama"),MessageBoxButtons.OK,c.Running?MessageBoxIcon.Warning:MessageBoxIcon.Information,MessageBoxDefaultButton.Button1,services==null?null:L.T("Hizmetler penceresini aç"),services);
  }
 }
}
