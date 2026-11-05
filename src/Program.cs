using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.ServiceProcess;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace GoodbyeTray {
 static class Program {
  internal static readonly Icon AppIcon=LoadIcon();
  // The logo at an exact pixel size, for drawing inside the window. Graphics.DrawIcon shrinks the 128 px frame with
  // nearest-neighbor sampling, which looks jagged; this takes the closest frame of the .ico and scales it smoothly.
  static readonly System.Collections.Generic.Dictionary<int,Bitmap> logos=new System.Collections.Generic.Dictionary<int,Bitmap>();
  internal static Bitmap Logo(int size) {
   size=Math.Max(8,Math.Min(256,size)); Bitmap cached;
   if(logos.TryGetValue(size,out cached)) return cached;
   var bitmap=new Bitmap(size,size,System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
   using(var stream=typeof(Program).Assembly.GetManifestResourceStream("GoodbyeTray.AppIcon")) using(var icon=new Icon(stream,size,size)) using(var frame=icon.ToBitmap()) using(var g=Graphics.FromImage(bitmap)) {
    g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic; g.PixelOffsetMode=System.Drawing.Drawing2D.PixelOffsetMode.HighQuality; g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
    g.DrawImage(frame,new Rectangle(0,0,size,size));
   }
   logos[size]=bitmap; return bitmap;
  }
  static Icon LoadIcon() { using(var stream=typeof(Program).Assembly.GetManifestResourceStream("GoodbyeTray.AppIcon")) using(var icon=new Icon(stream,128,128)) return (Icon)icon.Clone(); }
  internal const string ServiceName = "GoodbyeDPI";
  internal const string Dns = " --dns-addr 77.88.8.8 --dns-port 1253 --dnsv6-addr 2a02:6b8::feed:0ff --dnsv6-port 1253";
  // IDs 0..6 are persisted by older installations; never reorder them.
  internal const int ZapretStart=7;
  static readonly string[] AllNames = {L.T("Arx · önerilen"), L.T("Nyx · DNS'siz"), L.T("Kitsune · DNS'siz"), L.T("Nyx · DNS ile"), L.T("Kitsune · DNS ile"), L.T("Raijin · DNS ile"), L.T("Raijin · DNS'siz"), "Zapret 2 · Split", "Zapret 2 · Disorder", "Zapret 2 · Fake + Split", "Zapret 2 · Superonline", "Zapret 2 · Superonline TCP"};
  const string ZapretCommon="--wf-tcp-out=80,443 --wf-tcp-in=80,443 --lua-init=@zapret-lib.lua --lua-init=@zapret-antidpi.lua --filter-tcp=80 --filter-l7=http --lua-desync=http_hostcase --new --filter-tcp=443 --filter-l7=tls ";
  // Carrier preset used by zapret-win-turkey; effectiveness still depends on the line.
  const string Superonline="--payload=tls_client_hello --lua-desync=multidisorder:pos=2:seqovl=1";
  static readonly string[] AllArgs = {"-5 --set-ttl 5" + Dns, "--set-ttl 3", "-5", "--set-ttl 3" + Dns, "-5" + Dns, "-9" + Dns, "-9",
   ZapretCommon+"--lua-desync=multisplit:pos=1,midsld",
   ZapretCommon+"--lua-desync=multidisorder:pos=1,midsld",
   ZapretCommon+"--lua-desync=fake:blob=fake_default_tls:tcp_md5 --lua-desync=multisplit:pos=1,midsld",
   ZapretCommon+Superonline,
   // Only this opt-in profile drops outbound QUIC Initial packets to permit TCP fallback.
   ZapretCommon+Superonline+" --wf-udp-out=443 --new --filter-udp=443 --filter-l7=quic --payload=quic_initial --lua-desync=drop"};
#if ENABLE_ZAPRET
  internal static readonly bool ZapretEnabled=true;
#else
  internal static readonly bool ZapretEnabled=false;
#endif
  internal static readonly string[] Names=Available(AllNames);
  internal static readonly string[] Args=Available(AllArgs);
  static string[] Available(string[] catalog) { if(ZapretEnabled) return catalog; var result=new string[ZapretStart]; Array.Copy(catalog,result,result.Length); return result; }
  internal static int UpgradeProfile(int profile) { return profile>=0 && profile<Args.Length?profile:0; }
  internal static string MotorName(int profile) { return profile>=ZapretStart?"Zapret 2":"GoodbyeDPI"; }
  // GoodbyeDPI profiles are shown light to strong, each with DNS first. The ids themselves never change (see above).
  static readonly int[] GoodbyeOrder={0,3,1,4,2,5,6};
  internal static int[] MotorProfiles(int profile) { if(profile<ZapretStart) return (int[])GoodbyeOrder.Clone(); int count=Args.Length-ZapretStart; var ids=new int[count]; for(int i=0;i<count;i++) ids[i]=ZapretStart+i; return ids; }
  // The hover text for a GoodbyeDPI profile: where its name comes from, what it does and how it treats DNS. Null for Zapret.
  internal static string Describe(int profile) {
   if(profile<0 || profile>=ZapretStart) return null;
   string name=profile==0?L.T("Arx: Latince \"kale\"."):profile==1 || profile==3?L.T("Nyx: Yunan gece tanrıçası."):profile==2 || profile==4?L.T("Kitsune: Japon dokuz kuyruklu tilki ruhu."):L.T("Raijin: Japon gök gürültüsü tanrısı.");
   string work=profile==0?L.T("GoodbyeDPI-Turkey'nin standart ayarı (Mod 5, TTL 5). Önce bunu dene."):profile==1 || profile==3?L.T("Sahte paketleri kısa ömürlü gönderir (TTL 3)."):profile==2 || profile==4?L.T("TTL kullanmaz (Mod 5). Sayfalar yavaş açılıyorsa dene."):L.T("En kapsamlı ayar (Mod 9). Diğerleri işe yaramazsa dene.");
   return name+"\n"+work+"\n"+(Args[profile].EndsWith(Dns)?L.T("DNS'i kendisi yönlendirir."):L.T("DNS'e dokunmaz."));
  }
  // Every profile in the order menus show them.
  internal static int[] DisplayOrder() { var list=new System.Collections.Generic.List<int>(GoodbyeOrder); if(Args.Length>ZapretStart) list.AddRange(MotorProfiles(ZapretStart)); return list.ToArray(); }
  internal static string EngineFor(int profile) { if(profile<0 || profile>=Args.Length) throw new ArgumentOutOfRangeException("profile"); return profile<ZapretStart?Engine:Path.Combine(InstallDir,"zapret2",Environment.Is64BitOperatingSystem?"x86_64":"x86","winws2.exe"); }
  internal static void ValidateEngine(int profile) { string path=EngineFor(profile); if(!File.Exists(path)) throw new FileNotFoundException(L.T("Motor dosyaları eksik. Güncel kurucuyla uygulamayı onar."),path); if(profile>=ZapretStart) foreach(string file in new[]{"cygwin1.dll","WinDivert.dll",Environment.Is64BitOperatingSystem?"WinDivert64.sys":"WinDivert32.sys","zapret-lib.lua","zapret-antidpi.lua"}) if(!File.Exists(Path.Combine(Path.GetDirectoryName(path),file))) throw new FileNotFoundException(L.T("Motor dosyaları eksik. Güncel kurucuyla uygulamayı onar."),file); }
  internal static void CheckOtherEngines() { foreach(string name in new[]{"goodbyedpi","winws","winws2"}) foreach(var process in Process.GetProcessesByName(name)) using(process) { if(!process.HasExited) throw new InvalidOperationException(L.T("Başka bir DPI motoru çalışıyor. Önce onu kapatıp tekrar dene.")); } }
  internal static string DefaultInstallDir { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "GoodbyeDPI Tray"); } }
  static string installDir;
  // The folder the installer recorded (HKLM, admin-only), else Program Files. A first install with a chosen folder sets it before copying.
  internal static string InstallDir {
   get { if(installDir==null) installDir=RegisteredInstallDir() ?? DefaultInstallDir; return installDir; }
   set { installDir=Path.GetFullPath(value).TrimEnd('\\'); }
  }
  static string RegisteredInstallDir() {
   try {
    using(var key=Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\GoodbyeDPITray")) {
     string location=key==null?null:key.GetValue("InstallLocation") as string;
     if(String.IsNullOrEmpty(location) || !Path.IsPathRooted(location) || location.StartsWith(@"\\")) return null;
     location=Path.GetFullPath(location).TrimEnd('\\');
     return File.Exists(Path.Combine(location,"GoodbyeDPI.Tray.exe"))?location:null;
    }
   } catch(Exception ex) { AppInfo.Log("Kurulum konumu okunamadı: "+ex.Message); return null; }
  }
  internal static string Engine { get { return Path.Combine(InstallDir, Environment.Is64BitOperatingSystem ? "x86_64" : "x86", "goodbyedpi.exe"); } }
  internal static string Binary(int profile) { return "\""+Path.Combine(InstallDir,"GoodbyeDPI.Tray.exe")+"\" --engine-service "+profile; }
  internal static string LegacyBinary(int profile) { return profile<ZapretStart?"\""+Engine+"\" "+Args[profile]:null; }
  internal static void Configure(int profile) { ValidateEngine(profile); Sc("config GoodbyeDPI binPath= \"\\\""+Path.Combine(InstallDir,"GoodbyeDPI.Tray.exe")+"\\\" --engine-service "+profile+"\""); }
  // A key without ImagePath is a leftover (e.g. a service deleted before reboot), not an installed service.
  // Windows' default service permissions plus start (RP) and stop (WP) for signed-in users (IU). They already have
  // user-defined control (CR), which carries profile and startup requests. Changing the configuration stays admin-only.
  internal const string ServiceSecurity="D:(A;;CCLCSWRPWPDTLOCRRC;;;SY)(A;;CCDCLCSWRPWPDTLOCRSDRCWDWO;;;BA)(A;;CCLCSWRPWPLOCRRC;;;IU)(A;;CCLCSWLOCRRC;;;SU)";
  internal static void GrantUserControl() { Sc("sdset GoodbyeDPI "+ServiceSecurity); }
  internal static string ServicePath() { using(var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\GoodbyeDPI")) { string path = key == null ? null : Convert.ToString(key.GetValue("ImagePath", null)); return String.IsNullOrWhiteSpace(path) ? null : path; } }
  internal static int OwnedProfile() { return OwnedProfileFromPath(ServicePath()); }
  internal static int Profile() { int profile=OwnedProfile(); return profile<Args.Length?profile:-1; }
  internal static int OwnedProfileFromPath(string path) { if(path==null) return -1; for(int i=0;i<AllArgs.Length;i++) if(String.Equals(path,Binary(i),StringComparison.OrdinalIgnoreCase) || String.Equals(path,LegacyBinary(i),StringComparison.OrdinalIgnoreCase)) return i; return -1; }
  // Another GoodbyeDPI service (e.g. GoodbyeDPI-Turkey's service_install script). Returns the matching profile so a takeover keeps the user's choice.
  internal static int ForeignProfile() { string path = ServicePath(); if(path == null || OwnedProfile() >= 0) return -1; for(int i=0;i<ZapretStart;i++) if(path.EndsWith(" "+Args[i],StringComparison.OrdinalIgnoreCase)) return i; return 0; }
  internal static void RequireOwned() { if(OwnedProfile()<0) { string path = ServicePath(); throw new InvalidOperationException(path == null ? L.T("Bu uygulamanın hizmeti bulunamadı. GoodbyeDPI-Installer.exe ile kur ya da onar.") : L.T("Bilgisayarda başka bir GoodbyeDPI hizmeti var, ona dokunulmadı. Önce onu kaldır, sonra tekrar dene.\nHizmetin yolu: ") + path); } }
  internal static void Sc(string args) {
   using(var p = Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory,"sc.exe"), args) { UseShellExecute=false, CreateNoWindow=true, RedirectStandardOutput=true, RedirectStandardError=true })) {
    string output=p.StandardOutput.ReadToEnd(); string error=p.StandardError.ReadToEnd(); p.WaitForExit();
    if(p.ExitCode!=0) throw new InvalidOperationException(output + error);
   }
  }
  internal static void Stop() {
   using(var svc=new ServiceController(ServiceName)) {
    svc.Refresh();
    if(svc.Status!=ServiceControllerStatus.Stopped) {
     if(svc.Status!=ServiceControllerStatus.StopPending) try { svc.Stop(); } catch(InvalidOperationException ex) { var native=ex.InnerException as System.ComponentModel.Win32Exception; if(native==null || native.NativeErrorCode!=109) throw; }
     svc.WaitForStatus(ServiceControllerStatus.Stopped,TimeSpan.FromSeconds(20));
    }
   }
   WaitForServiceHostExit();
  }
  internal static void WaitForServiceHostExit() {
   // SCM may report STOPPED just before the service host releases its executable.
   foreach(var process in Process.GetProcessesByName("GoodbyeDPI.Tray")) using(process) {
    try {
     if(process.SessionId==0 && String.Equals(process.MainModule.FileName,Path.Combine(InstallDir,"GoodbyeDPI.Tray.exe"),StringComparison.OrdinalIgnoreCase) && !process.WaitForExit(5000)) throw new IOException(L.T("Hizmet işleminin kapanması bekleniyor. Yeniden deneyin."));
    } catch(System.ComponentModel.Win32Exception ex) { if(ex.NativeErrorCode!=5) throw; } // Normal users may not inspect the SYSTEM host; SCM still serializes its state.
    catch(InvalidOperationException) { } // The process exited between enumeration and inspection.
   }
  }
  internal static void Start() { using(var svc=new ServiceController(ServiceName)) { svc.Refresh(); if(svc.Status==ServiceControllerStatus.Running) return; if(svc.Status==ServiceControllerStatus.StopPending) svc.WaitForStatus(ServiceControllerStatus.Stopped,TimeSpan.FromSeconds(20)); svc.Refresh(); if(svc.Status!=ServiceControllerStatus.StartPending) { WaitForServiceHostExit(); CheckOtherEngines(); ValidateEngine(Profile()); svc.Start(); } svc.WaitForStatus(ServiceControllerStatus.Running,TimeSpan.FromSeconds(20)); } }
  internal static void Admin(string action, int profile) {
   using(var gate=new Mutex(false,@"Global\GoodbyeDPI.Tray.ServiceOperation")) {
    bool acquired=false;
    try { try { acquired=gate.WaitOne(0); } catch(AbandonedMutexException) { acquired=true; }
     if(!acquired) throw new InvalidOperationException(L.T("Başka bir işlem sürüyor, bitmesini bekle."));
     AdminCore(action,profile);
    } finally { if(acquired) gate.ReleaseMutex(); }
   }
  }
  internal static void AdminCore(string action,int profile) {
   if(!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator)) throw new InvalidOperationException(L.T("Yönetici izni gerekiyor."));
   if(!String.Equals(Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory).TrimEnd('\\'),InstallDir,StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException(L.T("Önce GoodbyeDPI-Installer.exe ile kurman gerekiyor."));
   if(action=="install") {
    if(ServicePath()!=null) throw new InvalidOperationException(L.T("GoodbyeDPI hizmeti zaten kurulu. Eskisini kaldır ya da kurulu uygulamayı aç."));
    if(Process.GetProcessesByName("goodbyedpi").Length>0) throw new InvalidOperationException(L.T("Açık GoodbyeDPI penceresini kapatıp tekrar dene."));
    Sc("create GoodbyeDPI binPath= \"\\\""+Path.Combine(InstallDir,"GoodbyeDPI.Tray.exe")+"\\\" --engine-service 0\" start= auto DisplayName= \"GoodbyeDPI Tray Engine\"");
    Sc("description GoodbyeDPI \"GoodbyeDPI Tray tarafından yönetilen Türkiye profili\"");
    GrantUserControl();
    Start(); return;
   }
   RequireOwned();
   if(action=="pause5" || action=="pause15" || (action=="start" && EngineConsole.PauseUntil()!=null)) { UserServiceControl.TryRun(action,profile); }
   else if(action=="start") Start();
   else if(action=="stop") Stop();
   else if(action=="restart") { Stop(); Start(); }
   else if(action=="profile") {
    if(profile<0 || profile>=Args.Length) throw new ArgumentOutOfRangeException("profile");
    int previous=Profile(); bool wasRunning;
    using(var svc=new ServiceController(ServiceName)) wasRunning=svc.Status==ServiceControllerStatus.Running;
    Stop();
    try { Configure(profile); if(wasRunning) Start(); }
    catch { Configure(previous); if(wasRunning) Start(); throw; }
   } else if(action=="auto-on") Sc("config GoodbyeDPI start= auto");
   else if(action=="auto-off") Sc("config GoodbyeDPI start= demand");
   else if(action=="quic-on" || action=="quic-off") { QuicBlock.Wanted=action=="quic-on"; using(var svc=new ServiceController(ServiceName)) if(svc.Status==ServiceControllerStatus.Running && EngineConsole.PauseUntil()==null) QuicBlock.Apply(); }
   else if(action=="dns") {
    if(profile<0 || profile>=DnsChoice.Names.Length) throw new ArgumentOutOfRangeException("profile");
    DnsChoice.Selected=profile;
    bool restart; using(var svc=new ServiceController(ServiceName)) restart=svc.Status==ServiceControllerStatus.Running && EngineConsole.PauseUntil()==null && DnsChoice.Uses(Profile());
    if(restart) { Stop(); Start(); }
   }
   else if(action=="sites-on" || action=="sites-off") {
    SiteList.Wanted=action=="sites-on";
    bool restart; using(var svc=new ServiceController(ServiceName)) restart=svc.Status==ServiceControllerStatus.Running && EngineConsole.PauseUntil()==null && SiteList.Uses(Profile());
    if(restart) { Stop(); Start(); }
   }
   else if(action=="remove") { Stop(); Sc("delete GoodbyeDPI"); }
   else throw new ArgumentException(L.T("Bilinmeyen işlem."));
  }
  [STAThread] static int Main(string[] args) {
   if(args.Length==2 && args[0]=="--engine-service") { int profile; if(!Int32.TryParse(args[1],out profile) || profile<0 || profile>=Args.Length) return 1; ServiceBase.Run(new EngineService(profile)); return 0; }
   // The app uninstalls itself, so no installer copy has to stay in the install folder.
   if(args.Length>=1 && (args[0]=="--uninstall" || args[0]=="--remove-elevated")) return Setup.Main(args);
   Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
   // After a language change the old window starts this copy and quits; wait for it so the single-instance lock is free.
   if(args.Length==2 && args[0]=="--relaunch") {
    int pid; if(Int32.TryParse(args[1],out pid)) try { using(var previous=Process.GetProcessById(pid)) previous.WaitForExit(10000); } catch(ArgumentException) { }
    args=new string[0];
   }
   try {
    if(args.Length>1 && args[0]=="--admin") { int profile=0; if(args.Length>2 && args[2]!="--quiet") profile=Int32.Parse(args[2]); Admin(args[1],profile); return 0; }
    if(args.Length==2 && (args[0]=="--preview" || args[0]=="--preview-dark" || args[0]=="--preview-light")) { using(var form=new MainForm(false,args[0]=="--preview"?(bool?)null:args[0]=="--preview-dark")) { form.Show(); Application.DoEvents(); using(var bitmap=new Bitmap(form.Width,form.Height)) { form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size)); bitmap.Save(args[1]); } } return 0; }
    if(args.Length==2 && args[0]=="--shell-report") { WindowsIntegration.RegisterJumpList(); File.WriteAllText(args[1],WindowsIntegration.ShellStatus); return 0; }
    string command=args.Length==2 && args[0]=="--action"?args[1]:"show";
    if(!AppCommands.IsValid(command)) throw new ArgumentException(L.T("Bilinmeyen uygulama komutu."));
    // A tray start with the tray turned off would leave a running app with nothing on screen.
    if(Array.IndexOf(args,"--tray")>=0 && !WindowsIntegration.TrayStartupEnabled()) return 0;
    bool created; using(var mutex=new Mutex(true,@"Local\GoodbyeDPI.Tray.UI",out created)) {
     if(!created) { if(Array.IndexOf(args,"--tray")<0 && !AppCommands.Send(command)) { try { using(var signal=EventWaitHandle.OpenExisting(MainForm.RevealEvent)) signal.Set(); } catch(WaitHandleCannotBeOpenedException) { MessageBox.Show(L.T("Uygulama açılıyor, saatin yanındaki simgeden ulaşabilirsin."),"GoodbyeDPI"); } } return 0; }
     if(command=="exit") return 0;
     System.Threading.Tasks.Task.Run(delegate { UpdateDownload.CleanOld(); });
     using(var form=new MainForm(true)) { if(Array.IndexOf(args,"--tray")>=0) { form.WindowState=FormWindowState.Minimized; form.ShowInTaskbar=false; form.BeginStartupNotification(); } else form.Shown+=delegate { form.DispatchCommand(command); }; form.Shown+=delegate { form.BeginInvoke(new Action(form.ShowStartupReleaseNotes)); }; Application.Run(form); }
    }
    return 0;
   } catch(Exception ex) { AppInfo.Log(ex.ToString()); if(Array.IndexOf(args,"--quiet")<0) MessageBox.Show(ex.Message,"GoodbyeDPI Tray",MessageBoxButtons.OK,MessageBoxIcon.Error); return 1; }
  }
 }

}
