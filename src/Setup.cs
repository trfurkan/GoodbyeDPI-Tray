using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace GoodbyeTray {
 static class Setup {
  const string UninstallKey=@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\GoodbyeDPITray";
  // Earlier builds kept a copy of the installer in the install folder as the uninstaller; GoodbyeDPI.Tray.exe uninstalls now, so updates remove those copies.
  const string SetupName="GoodbyeDPI-Installer.exe";
  const string LegacySetupName="GoodbyeDPI-Kurulum.exe";
  const string UninstallShortcut="GoodbyeDPI'ı Kaldır.lnk";
  internal static string Stage="Hazırlık";
  internal static bool RebootRequired;
  static bool reportProgress;
  internal static void Report(int percent,string message) {
   Stage=message;
   if(!reportProgress) return;
   try { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"progress.txt"),percent+"\n"+message); } catch(IOException) { } catch(UnauthorizedAccessException) { }
  }
  internal static readonly string[] Files={"GoodbyeDPI.Tray.exe","GoodbyeDPI.Tray.exe.config","x86/goodbyedpi.exe","x86/WinDivert.dll","x86/WinDivert32.sys","x86/WinDivert64.sys","x86_64/goodbyedpi.exe","x86_64/WinDivert.dll","x86_64/WinDivert64.sys","licenses/LICENSE-getline.txt","licenses/LICENSE-goodbyedpi.txt","licenses/LICENSE-uthash.txt","licenses/LICENSE-windivert.txt","licenses/LICENSE-goodbyedpi-tray.txt","licenses/NOTICE-goodbyedpi-tray.txt","licenses/THIRD_PARTY_NOTICES.md"};
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool MoveFileEx(string existing,string destination,int flags);
  static string App { get { return Path.Combine(Program.InstallDir,"GoodbyeDPI.Tray.exe"); } }
  internal static string Target(string relative) {
   string root=Path.GetFullPath(Program.InstallDir)+Path.DirectorySeparatorChar;
   string target=Path.GetFullPath(Path.Combine(root,relative.Replace('/',Path.DirectorySeparatorChar)));
   if(!target.StartsWith(root,StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Geçersiz paket yolu.");
   return target;
  }
  static void ValidateDirectory() {
   var dir=new DirectoryInfo(Program.InstallDir);
   while(dir!=null) { if(dir.Exists && (dir.Attributes & FileAttributes.ReparsePoint)!=0) throw new IOException("Kurulum yolunda bağlantı noktası var: "+dir.FullName); dir=dir.Parent; }
   foreach(string file in Files) {
    string path=Target(file); var parent=new DirectoryInfo(Path.GetDirectoryName(path));
    if(parent.Exists && (parent.Attributes & FileAttributes.ReparsePoint)!=0) throw new IOException("Bağlantılı klasöre kurulum yapılamaz.");
    if(File.Exists(path) && (File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0) throw new IOException("Bağlantılı dosyaya yazılamaz.");
   }
   foreach(string file in new[]{SetupName,LegacySetupName,UninstallShortcut,"uninstall.ps1","engine-console.txt"}) if(File.Exists(Target(file)) && (File.GetAttributes(Target(file))&FileAttributes.ReparsePoint)!=0) throw new IOException("Bağlantılı kurulum dosyası bulundu.");
  }
  static bool Matches(string path,Stream input) {
   if(!File.Exists(path)) return false;
   using(var sha=System.Security.Cryptography.SHA256.Create()) using(var disk=File.OpenRead(path)) {
    string expected=Convert.ToBase64String(sha.ComputeHash(input)); input.Position=0;
    return expected==Convert.ToBase64String(sha.ComputeHash(disk));
   }
  }
  static void CloseOwnedUI() {
   AppCommands.Send("exit");
   foreach(var process in Process.GetProcessesByName("GoodbyeDPI.Tray")) using(process) {
    if(process.SessionId==0) continue;
    if(String.Equals(process.MainModule.FileName,App,StringComparison.OrdinalIgnoreCase)) { if(!process.WaitForExit(4000)) { process.Kill(); if(!process.WaitForExit(10000)) throw new IOException("Arayüz kapatılamadı."); } }
   }
  }
  static void RunApp(string arguments) {
   using(var process=Process.Start(new ProcessStartInfo(App,arguments) { UseShellExecute=false,CreateNoWindow=true })) { process.WaitForExit(); if(process.ExitCode!=0) throw new InvalidOperationException("Hizmet ayarlanamadı. Kurulum dosyasıyla tekrar dene."); }
  }
  static void RegisterUninstaller() {
   using(var key=Registry.LocalMachine.CreateSubKey(UninstallKey)) {
    key.SetValue("DisplayName","GoodbyeDPI Tray"); key.SetValue("DisplayVersion",AppInfo.Version); key.SetValue("Publisher",AppInfo.Developer);
    key.SetValue("InstallLocation",Program.InstallDir); key.SetValue("DisplayIcon",App);
    key.SetValue("UninstallString","\""+App+"\" --uninstall"); key.SetValue("NoModify",1,RegistryValueKind.DWord); key.SetValue("NoRepair",1,RegistryValueKind.DWord);
    // Settings > Apps reads the size from EstimatedSize (KB); without it the list shows none.
    long bytes=0; foreach(var file in new DirectoryInfo(Program.InstallDir).GetFiles("*",SearchOption.AllDirectories)) bytes+=file.Length;
    key.SetValue("EstimatedSize",(int)Math.Min(Int32.MaxValue,(bytes+1023)/1024),RegistryValueKind.DWord);
    key.SetValue("InstallDate",DateTime.Now.ToString("yyyyMMdd")); key.SetValue("Comments",AppInfo.Purpose);
    key.SetValue("URLInfoAbout","https://github.com/trfurkan/GoodbyeDPI-Tray"); key.SetValue("URLUpdateInfo","https://github.com/trfurkan/GoodbyeDPI-Tray/releases"); key.SetValue("HelpLink","https://github.com/trfurkan/GoodbyeDPI-Tray/issues");
   }
  }
  static void Install(bool replaceForeign) {
   Report(5,"Kurulum dosyaları kontrol ediliyor…");
   ValidateDirectory();
   using(var net=Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full")) if(net==null || Convert.ToInt32(net.GetValue("Release",0))<528040) throw new InvalidOperationException(".NET Framework 4.8 gerekiyor. Windows Update'ten yükleyip tekrar dene.");
   string previousCommand=Program.ServicePath();
   bool existing=previousCommand!=null;
   // With the user's consent, a foreign GoodbyeDPI service is taken over: repointed to this app, restored on failure.
   bool foreign=existing && replaceForeign && Program.ForeignProfile()>=0;
   int previousProfile=foreign?Program.ForeignProfile():existing?Program.Profile():0;
   if(foreign) AppInfo.Log("Eski GoodbyeDPI hizmeti devralınıyor: "+previousCommand);
   else if(existing) Program.RequireOwned();
   else if(Process.GetProcessesByName("goodbyedpi").Length>0) throw new InvalidOperationException("Açık GoodbyeDPI penceresini kapatıp tekrar dene.");
   // Check every embedded resource before stopping an existing installation.
   foreach(string file in Files) using(var input=Assembly.GetExecutingAssembly().GetManifestResourceStream("payload/"+file)) if(input==null) throw new IOException("Pakette eksik dosya: "+file);
   var backup=new System.Collections.Generic.Dictionary<string,byte[]>();
   var tracked=new System.Collections.Generic.List<string>(Files); tracked.Add(SetupName); tracked.Add(LegacySetupName); tracked.Add(UninstallShortcut);
   foreach(string file in tracked) { string path=Target(file); backup[file]=File.Exists(path)?File.ReadAllBytes(path):null; }
   bool hadRegistration;
   var registrationValues=new System.Collections.Generic.Dictionary<string,object>();
   var registrationKinds=new System.Collections.Generic.Dictionary<string,RegistryValueKind>();
   using(var registration=Registry.LocalMachine.OpenSubKey(UninstallKey)) { hadRegistration=registration!=null; if(hadRegistration) foreach(string name in registration.GetValueNames()) { registrationValues[name]=registration.GetValue(name,null,RegistryValueOptions.DoNotExpandEnvironmentNames); registrationKinds[name]=registration.GetValueKind(name); } }
   bool running=false; Report(15,"Uygulama güncellemeye hazırlanıyor…");
   if(existing) { using(var svc=new System.ServiceProcess.ServiceController(Program.ServiceName)) running=svc.Status==System.ServiceProcess.ServiceControllerStatus.Running; Program.Stop(); }
   CloseOwnedUI(); Directory.CreateDirectory(Program.InstallDir);
   try {
    int copied=0;
    foreach(string file in Files) {
     Report(20+55*copied/Files.Length,"Uygulama dosyaları hazırlanıyor…");
     string path=Target(file); Directory.CreateDirectory(Path.GetDirectoryName(path));
     using(var input=Assembly.GetExecutingAssembly().GetManifestResourceStream("payload/"+file)) {
      if(!Matches(path,input)) using(var output=new FileStream(path,FileMode.Create,FileAccess.Write,FileShare.None)) input.CopyTo(output);
     }
     copied++;
    }
    CreateShortcut(Target(UninstallShortcut),App,"--uninstall");
    foreach(string old in new[]{SetupName,LegacySetupName}) try { DeleteOrSchedule(Target(old)); } catch(Exception ex) { AppInfo.Log("Eski kurucu silinemedi: "+ex.Message); }
    Report(80,"Windows kaydı oluşturuluyor…"); RegisterUninstaller();
    Report(88,"GoodbyeDPI hizmeti hazırlanıyor…");
    if(existing) {
     Program.Configure(previousProfile);
     if(foreign) { Program.Sc("config GoodbyeDPI DisplayName= \"GoodbyeDPI Tray Engine\""); Program.Sc("description GoodbyeDPI \"GoodbyeDPI Tray tarafından yönetilen Türkiye profili\""); }
     if(running) Program.Start();
    } else RunApp("--admin install --quiet");
    AppInfo.Log("Kurulum tamamlandı "+AppInfo.Version);
    Report(95,"Kurulum tamamlanıyor…");
   } catch(Exception failure) {
    AppInfo.Log("Kurulum başarısız: "+Stage+" "+failure);
    try {
     if(Program.Profile()>=0) Program.Stop();
     if(!existing && Program.Profile()>=0) Program.Sc("delete GoodbyeDPI");
     if(existing) Program.Sc("config GoodbyeDPI binPath= \""+previousCommand.Replace("\"","\\\"")+"\"");
     if(foreign) Program.Sc("config GoodbyeDPI DisplayName= \"GoodbyeDPI\"");
     foreach(var item in backup) { string path=Target(item.Key); if(item.Value==null) { if(File.Exists(path)) File.Delete(path); } else if(!String.Equals(path,Application.ExecutablePath,StringComparison.OrdinalIgnoreCase)) File.WriteAllBytes(path,item.Value); }
     if(!hadRegistration) Registry.LocalMachine.DeleteSubKeyTree(UninstallKey,false);
     else using(var registration=Registry.LocalMachine.CreateSubKey(UninstallKey)) { foreach(string name in registration.GetValueNames()) if(!registrationValues.ContainsKey(name)) registration.DeleteValue(name,false); foreach(var value in registrationValues) registration.SetValue(value.Key,value.Value,registrationKinds[value.Key]); }
     if(existing && running) Program.Start();
    } catch(Exception rollback) { AppInfo.Log("Geri yükleme: "+rollback); throw new IOException(failure.Message+"\nGeri yükleme tamamlanamadı: "+rollback.Message,failure); }
    throw;
   }
  }
  static void DeleteOrSchedule(string path) {
   if(!File.Exists(path)) return;
   try { File.Delete(path); }
   catch(IOException) { if(!MoveFileEx(path,null,4)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()); RebootRequired=true; AppInfo.Log("Yeniden başlatmada silinecek: "+path); }
   catch(UnauthorizedAccessException) { if(!MoveFileEx(path,null,4)) throw; RebootRequired=true; AppInfo.Log("Yeniden başlatmada silinecek: "+path); }
  }
  static void RemoveOwnedDriver() {
   using(var key=Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\WinDivert")) {
    if(key==null) return;
    string path=Convert.ToString(key.GetValue("ImagePath","")).Trim('"');
    if(path.StartsWith(@"\??\")) path=path.Substring(4);
    bool owned=false;
    foreach(string file in Files) if(file.EndsWith(".sys",StringComparison.OrdinalIgnoreCase) && String.Equals(path,Target(file),StringComparison.OrdinalIgnoreCase)) owned=true;
    if(!owned) return;
   }
   try { using(var driver=new System.ServiceProcess.ServiceController("WinDivert")) {
    if(driver.Status!=System.ServiceProcess.ServiceControllerStatus.Stopped) { driver.Stop(); driver.WaitForStatus(System.ServiceProcess.ServiceControllerStatus.Stopped,TimeSpan.FromSeconds(20)); }
   } } catch(InvalidOperationException ex) { var native=ex.InnerException as System.ComponentModel.Win32Exception; if(native==null || native.NativeErrorCode!=1060) throw; }
   using(var remaining=Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\WinDivert")) if(remaining!=null) Program.Sc("delete WinDivert");
  }
  static void Uninstall() {
   Report(5,"Kaldırma hazırlanıyor…");
   ValidateDirectory();
   if(Program.ServicePath()!=null) { Program.RequireOwned(); Program.Stop(); Program.Sc("delete GoodbyeDPI"); }
   Report(20,"Uygulama kapatılıyor…"); CloseOwnedUI();
   Report(30,"Sürücü kaldırılıyor…"); RemoveOwnedDriver();
   int removed=0;
   foreach(string file in Files) { DeleteOrSchedule(Target(file)); Report(35+45*(++removed)/Files.Length,"Uygulama dosyaları kaldırılıyor…"); }
   DeleteOrSchedule(Target("uninstall.ps1")); DeleteOrSchedule(Target("engine-console.txt")); DeleteOrSchedule(Target(SetupName)); DeleteOrSchedule(Target(LegacySetupName)); DeleteOrSchedule(Target(UninstallShortcut));
   foreach(string folder in new[]{"x86","x86_64","licenses"}) { string path=Target(folder); if(Directory.Exists(path) && Directory.GetFileSystemEntries(path).Length==0) Directory.Delete(path); }
   Registry.LocalMachine.DeleteSubKeyTree(UninstallKey,false);
   // Never recursively delete an installation directory: preserve unknown user files.
   if(Directory.Exists(Program.InstallDir) && Directory.GetFileSystemEntries(Program.InstallDir).Length==0) Directory.Delete(Program.InstallDir);
   foreach(string file in trackedFiles()) if(File.Exists(Target(file)) && !RebootRequired) throw new IOException("Dosya kaldırılamadı: "+Target(file));
   AppInfo.Log("Kaldırma tamamlandı. Yeniden başlatma: "+RebootRequired);
   Report(95,"Kaldırma tamamlanıyor…");
  }
  static string[] trackedFiles() { var list=new System.Collections.Generic.List<string>(Files); list.Add(SetupName); list.Add(LegacySetupName); list.Add(UninstallShortcut); list.Add("uninstall.ps1"); list.Add("engine-console.txt"); return list.ToArray(); }
  internal static void UserSetup(bool remove,bool preserveStartup=false) {
   Stage="Kullanıcı başlangıcı ve kısayollar ayarlanıyor";
   if(remove) WindowsIntegration.RemoveTrayStartup(); else if(!preserveStartup) WindowsIntegration.SetTrayStartup(true);
   string shortcut=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs),"GoodbyeDPI Tray.lnk");
   if(remove) { if(File.Exists(shortcut)) File.Delete(shortcut); }
   else { Directory.CreateDirectory(Path.GetDirectoryName(shortcut)); CreateShortcut(shortcut,App,null); }
  }
  static void CreateShortcut(string path,string target,string arguments) {
   Type shellType=Type.GetTypeFromProgID("WScript.Shell"); object shell=Activator.CreateInstance(shellType); object link=null;
   try {
    link=shellType.InvokeMember("CreateShortcut",BindingFlags.InvokeMethod,null,shell,new object[]{path}); var type=link.GetType();
    type.InvokeMember("TargetPath",BindingFlags.SetProperty,null,link,new object[]{target});
    if(arguments!=null) type.InvokeMember("Arguments",BindingFlags.SetProperty,null,link,new object[]{arguments});
    type.InvokeMember("WorkingDirectory",BindingFlags.SetProperty,null,link,new object[]{Program.InstallDir});
    type.InvokeMember("Save",BindingFlags.InvokeMethod,null,link,null);
   } finally { if(link!=null) Marshal.FinalReleaseComObject(link); Marshal.FinalReleaseComObject(shell); }
  }
  internal static void ClearUserSettings() {
   Registry.CurrentUser.DeleteSubKeyTree(@"Software\GoodbyeDPITray",false);
   if(File.Exists(AppInfo.LogPath)) File.Delete(AppInfo.LogPath);
   string logs=Path.GetDirectoryName(AppInfo.LogPath),data=Path.GetDirectoryName(logs);
   foreach(string folder in new[]{logs,data}) if(Directory.Exists(folder) && Directory.GetFileSystemEntries(folder).Length==0) Directory.Delete(folder);
  }
  // A running exe cannot delete itself, so an uninstall started from the install folder continues from a temporary copy.
  static bool InsideInstallDir(string path) { return Path.GetFullPath(path).StartsWith(Path.GetFullPath(Program.InstallDir)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase); }
  // Copies this exe (installer or GoodbyeDPI.Tray.exe) with its .config so the copy runs with the same runtime settings.
  static string CopySelf(string dir) {
   string copy=Path.Combine(dir,Path.GetFileName(Application.ExecutablePath)); File.Copy(Application.ExecutablePath,copy);
   if(File.Exists(Application.ExecutablePath+".config")) File.Copy(Application.ExecutablePath+".config",copy+".config");
   return copy;
  }
  static void RelaunchFromTemp() {
   string dir=Path.Combine(Path.GetTempPath(),"GoodbyeDPI-Uninstall-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
   Process.Start(new ProcessStartInfo(CopySelf(dir),"--uninstall --temporary") { UseShellExecute=false,WorkingDirectory=dir }).Dispose();
  }
  static void DeleteSelfLater() {
   string exe=Application.ExecutablePath,dir=Path.GetDirectoryName(exe);
   if(!Path.GetFullPath(dir).StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase)) return;
   Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory,"cmd.exe"),"/d /c ping -n 3 127.0.0.1 >nul & del /f /q \""+exe+"\" \""+exe+".config\" & rmdir \""+dir+"\"") { UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=Path.GetTempPath() }).Dispose();
  }
  internal static int Elevate(bool remove,bool replaceForeign,IProgress<Tuple<int,string>> progress) {
   // UAC cannot always see a mapped/shared source drive. Use a local copy.
   string stageDir=Path.Combine(Path.GetTempPath(),"GoodbyeDPI-Setup-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(stageDir);
   string executable=CopySelf(stageDir);
   string progressFile=Path.Combine(stageDir,"progress.txt");
   try { using(var p=Process.Start(new ProcessStartInfo(executable,(remove?"--remove-elevated":"--install-elevated")+(replaceForeign?" --replace-foreign":"")+" --report-progress") { UseShellExecute=true,Verb="runas",WorkingDirectory=stageDir })) {
    int last=-1;
    do { try { string[] lines=File.ReadAllLines(progressFile); int value; if(lines.Length==2 && Int32.TryParse(lines[0],out value) && value>last && value>=0 && value<=95) { last=value; progress.Report(Tuple.Create(value,lines[1])); } } catch(IOException) { } catch(UnauthorizedAccessException) { } } while(!p.WaitForExit(100));
    return p.ExitCode;
   } }
   finally { try { File.Delete(progressFile); File.Delete(executable); File.Delete(executable+".config"); Directory.Delete(stageDir); } catch {} }
  }
  [STAThread] internal static int Main(string[] args) {
   Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
   try {
    if(args.Length==1 && (args[0]=="--user-install" || args[0]=="--user-update" || args[0]=="--user-remove")) { UserSetup(args[0]=="--user-remove",args[0]=="--user-update"); return 0; }
    if(args.Length>=1 && (args[0]=="--install-elevated" || args[0]=="--remove-elevated")) {
     reportProgress=Array.IndexOf(args,"--report-progress")>=0;
     if(!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator)) throw new UnauthorizedAccessException("Yönetici izni gerekiyor.");
     bool created; using(var mutex=new Mutex(true,@"Global\GoodbyeDPI.Tray.Setup",out created)) { if(!created) throw new InvalidOperationException("Başka bir kurulum işlemi devam ediyor."); if(args[0]=="--install-elevated") Install(Array.IndexOf(args,"--replace-foreign")>=0); else Uninstall(); } return RebootRequired?3010:0;
    }
    if(args.Length==2 && args[0]=="--preview") { using(var form=new SetupForm(false)) { form.Show(); Application.DoEvents(); using(var bitmap=new Bitmap(form.Width,form.Height)) { form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size)); bitmap.Save(args[1]); } } return 0; }
    bool remove=args.Length>=1 && args[0]=="--uninstall";
    if(remove && args.Length==1 && InsideInstallDir(Application.ExecutablePath)) { RelaunchFromTemp(); return 0; }
    Application.Run(new SetupForm(remove));
    if(Array.IndexOf(args,"--temporary")>=0) DeleteSelfLater();
    return 0;
   } catch(Exception ex) { string message=Stage+"\n"+ex.Message+"\nGünlük: "+AppInfo.LogPath; AppInfo.Log(message+"\n"+ex); if(Array.IndexOf(args,"--quiet")<0) MessageBox.Show(message,"GoodbyeDPI Kurulum",MessageBoxButtons.OK,MessageBoxIcon.Error); return 1; }
  }
 }
 class PaletteProgressBar : ProgressBar {
  public PaletteProgressBar() { SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true); }
  protected override void OnPaint(PaintEventArgs e) { e.Graphics.Clear(BackColor); using(var brush=new SolidBrush(ForeColor)) e.Graphics.FillRectangle(brush,0,0,Width*(Value-Minimum)/Math.Max(1,Maximum-Minimum),Height); }
 }
 class SetupForm : Form {
  bool busy,completed;
  readonly Label title=new Label(),info=new Label(),step=new Label(),percentage=new Label();
  readonly ProgressBar progressBar=new PaletteProgressBar();
  readonly SoftButton start=new SoftButton(),cancel=new SoftButton();
  readonly CheckBox consent=new CheckBox();
  internal void ShowProgress(int value,string message) {
   if(completed || !busy || value<progressBar.Value) return;
   progressBar.Visible=true; percentage.Visible=true;
   progressBar.Value=Math.Max(progressBar.Value,Math.Max(0,Math.Min(100,value)));
   progressBar.Invalidate(); percentage.Text="%"+progressBar.Value; step.Text=message;
  }
  internal void ShowCompletion(bool remove,bool reboot) {
   completed=true; progressBar.Visible=true; percentage.Visible=true; progressBar.Maximum=101; progressBar.Value=101; progressBar.Value=100; progressBar.Maximum=100; progressBar.Invalidate(); percentage.Text="%100";
   title.Text=remove?"Kaldırma tamamlandı":"Kurulum tamamlandı";
   step.Text=reboot?"Bitirmek için bilgisayarı yeniden başlat.":"Tamamdır.";
   info.Text=remove?"GoodbyeDPI Tray bilgisayarından tamamen kaldırıldı.":"GoodbyeDPI hazır. Şimdi açabilir ya da sonra Başlat menüsünden bulabilirsin.";
   start.Visible=false; cancel.Text="Bitir";
  }
  public SetupForm(bool remove) {
   bool wasInstalled=File.Exists(Path.Combine(Program.InstallDir,"GoodbyeDPI.Tray.exe"));
   Text=remove?"GoodbyeDPI — Kaldır":"GoodbyeDPI — Kurulum"; ClientSize=new Size(580,410); FormBorderStyle=FormBorderStyle.FixedDialog; MaximizeBox=false; StartPosition=FormStartPosition.CenterScreen; Font=new Font("Segoe UI",10); BackColor=Color.FromArgb(248,248,248); Icon=Program.AppIcon;
   title.Text=remove?"GoodbyeDPI'ı kaldır":"GoodbyeDPI kurulumu"; title.Font=new Font("Segoe UI",22,FontStyle.Bold); title.SetBounds(28,24,524,46); Controls.Add(title);
   info.SetBounds(30,88,520,125); info.Text=remove?"GoodbyeDPI Tray, ayarları ve günlükleriyle birlikte bilgisayarından tamamen kaldırılacak.":"Sürüm "+AppInfo.Version+"  ·  Geliştirici: Furkan\n\nKurulum konumu\n"+Program.InstallDir+"\n\nProfilin ve başlangıç ayarların korunur."; Controls.Add(info);
   step.SetBounds(30,256,455,24); step.Text=remove?"Kaldırmaya hazır.":"Kuruluma hazır."; Controls.Add(step);
   percentage.SetBounds(485,256,65,24); percentage.Text="%0"; percentage.TextAlign=ContentAlignment.MiddleRight; Controls.Add(percentage);
   progressBar.SetBounds(30,286,520,10); progressBar.Style=ProgressBarStyle.Continuous; Controls.Add(progressBar); progressBar.Visible=false; percentage.Visible=false;
   start.Text=remove?"Kaldır":wasInstalled?"Güncelle":"Kur"; start.SetBounds(30,334,150,42); Controls.Add(start);
   cancel.Text="Vazgeç"; cancel.SetBounds(194,334,110,42); cancel.Click+=delegate { Close(); }; Controls.Add(cancel); CancelButton=cancel; AcceptButton=start;
   bool dark=false;
   try { using(var settings=Registry.CurrentUser.OpenSubKey(@"Software\GoodbyeDPITray")) {
    string preference=settings==null?"system":Convert.ToString(settings.GetValue("Theme","system"));
    using(var windows=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize")) dark=preference=="dark" || (preference=="system" && windows!=null && Convert.ToInt32(windows.GetValue("AppsUseLightTheme",1))==0);
   } } catch(System.Security.SecurityException) { } catch(UnauthorizedAccessException) { }
   BackColor=SystemInformation.HighContrast?SystemColors.Window:AppPalette.Background(dark); ForeColor=SystemInformation.HighContrast?SystemColors.WindowText:AppPalette.Text(dark);
   progressBar.BackColor=SystemInformation.HighContrast?SystemColors.Window:AppPalette.Border(dark); progressBar.ForeColor=SystemInformation.HighContrast?SystemColors.Highlight:AppPalette.Accent;
   foreach(var button in new[]{start,cancel}) { button.BackColor=BackColor; button.Fill=SystemInformation.HighContrast?SystemColors.Window:button==start?AppPalette.Accent:AppPalette.Surface(dark); button.Ink=SystemInformation.HighContrast?SystemColors.WindowText:button==start?Color.White:ForeColor; button.Edge=SystemInformation.HighContrast?SystemColors.WindowText:AppPalette.Border(dark); }
   info.ForeColor=step.ForeColor=SystemInformation.HighContrast?ForeColor:AppPalette.Muted(dark);
   if(remove) {
    ClientSize=new Size(500,256); title.Font=new Font("Segoe UI",18,FontStyle.Bold); title.SetBounds(28,22,444,40);
    info.SetBounds(30,70,440,48); step.SetBounds(30,128,370,24); percentage.SetBounds(400,128,70,24); progressBar.SetBounds(30,156,440,8);
    start.SetBounds(30,186,130,42); cancel.SetBounds(172,186,110,42);
   }
   if(!remove) {
    ClientSize=new Size(580,620);
    var developer=ProjectLinks.Create("Arayüz geliştiricisi: Furkan · github.com/trfurkan",AppInfo.DeveloperUrl,SystemInformation.HighContrast?SystemColors.HotTrack:dark?AppPalette.Mint:AppPalette.Accent);
    developer.SetBounds(30,216,520,30); Controls.Add(developer);
    var notice=new NoticeView { Text=AppInfo.Notice,ThumbColor=SystemInformation.HighContrast?SystemColors.Highlight:AppPalette.Muted(dark),EdgeColor=SystemInformation.HighContrast?SystemColors.WindowText:AppPalette.Border(dark),BackColor=SystemInformation.HighContrast?SystemColors.Window:AppPalette.Surface(dark),ForeColor=ForeColor,TabStop=true };
    notice.SetBounds(30,256,520,150); notice.AccessibleName="Kullanmadan önce"; Controls.Add(notice);
    consent.Text="Okudum, anladım."; consent.SetBounds(30,416,520,36); consent.Checked=false; Controls.Add(consent);
    start.Enabled=false;
    consent.CheckedChanged+=delegate { start.Enabled=consent.Checked && !busy && !completed; };
    step.Top+=210; percentage.Top+=210; progressBar.Top+=210; start.Top+=210; cancel.Top+=210;
   }
   HandleCreated+=delegate { WindowsIntegration.ThemeCaption(this,dark,BackColor,ForeColor); };
   FormClosing+=delegate(object sender,FormClosingEventArgs e) { if(busy) e.Cancel=true; };
   start.Click+=async delegate {
    if(busy || completed || (!remove && !consent.Checked)) return;
    bool replaceForeign=false;
    if(!remove && Program.ForeignProfile()>=0) {
     if(MessageBox.Show(this,"Bilgisayarda daha önce kurulmuş başka bir GoodbyeDPI hizmeti var:\n\n"+Program.ServicePath()+"\n\nBu hizmeti GoodbyeDPI Tray'e devredelim mi? Eski hizmet durdurulur ve artık bu uygulama yönetir. Eski klasörü sonra silebilirsin.","GoodbyeDPI Kurulum",MessageBoxButtons.YesNo,MessageBoxIcon.Question,MessageBoxDefaultButton.Button1)!=DialogResult.Yes) { step.Text="Kurulum için eski hizmetin devredilmesi gerekiyor."; return; }
     replaceForeign=true;
    }
    busy=true; progressBar.Value=0; start.Enabled=false; cancel.Enabled=false; consent.Enabled=false;
    title.Text=remove?"GoodbyeDPI kaldırılıyor":"GoodbyeDPI kuruluyor";
    ShowProgress(0,"Windows izin isteyecek, onayla.");
    var progress=new Progress<Tuple<int,string>>(delegate(Tuple<int,string> update) { ShowProgress(update.Item1,update.Item2); });
    try {
     int code=await Task.Run(delegate { return Setup.Elevate(remove,replaceForeign,progress); });
     if(code!=0 && code!=3010) throw new InvalidOperationException("Bir sorun çıktı, tekrar dene.\nAyrıntı: "+AppInfo.LogPath);
     ShowProgress(97,remove?"Kısayollar temizleniyor…":"Kısayollar hazırlanıyor…");
     Setup.UserSetup(remove,wasInstalled); if(!remove) using(var preferences=Registry.CurrentUser.CreateSubKey(@"Software\GoodbyeDPITray")) preferences.SetValue("UsageAcknowledged",1); if(remove) Setup.ClearUserSettings();
     ShowCompletion(remove,code==3010); AcceptButton=cancel;
    } catch(System.ComponentModel.Win32Exception ex) { title.Text=ex.NativeErrorCode==1223?"İzin verilmedi":"İşlem tamamlanamadı"; step.Text=ex.NativeErrorCode==1223?"Tekrar denersen izni onaylaman yeterli.":"Tekrar dene."; info.Text=ex.NativeErrorCode==1223?"İzin verilmedi, hiçbir şey yapılmadı.":ex.Message; }
    catch(Exception ex) { title.Text="İşlem tamamlanamadı"; step.Text="Sorunu giderip tekrar dene."; info.Text=ex.Message; }
    finally { busy=false; if(!completed) { progressBar.Visible=false; percentage.Visible=false; } cancel.Enabled=true; start.Enabled=!completed && (remove || consent.Checked); consent.Enabled=!completed; if(!completed) start.Text="Yeniden dene"; }
    if(completed && !remove && MessageBox.Show(this,"Kurulum bitti.\n\nGoodbyeDPI'ı şimdi açalım mı?","GoodbyeDPI",MessageBoxButtons.YesNo,MessageBoxIcon.Information)==DialogResult.Yes) {
     try { Process.Start(new ProcessStartInfo(Path.Combine(Program.InstallDir,"GoodbyeDPI.Tray.exe")) { UseShellExecute=true }); Close(); }
     catch(Exception ex) { info.Text="Kurulum bitti ama uygulama açılamadı. Başlat menüsünden açmayı dene.\n"+ex.Message; AppInfo.Log(ex.ToString()); }
    }
   };
  }
 }
}
