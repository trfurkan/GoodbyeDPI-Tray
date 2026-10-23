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
  internal static string Stage=L.T("Hazırlık");
  internal static bool RebootRequired;
  static bool reportProgress;
  internal static void Report(int percent,string message) {
   Stage=message;
   if(!reportProgress) return;
   try { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"progress.txt"),percent+"\n"+message); } catch(IOException) { } catch(UnauthorizedAccessException) { }
  }
  internal static readonly string[] AllFiles={"zapret2/SOURCE.txt","GoodbyeDPI.Tray.exe","GoodbyeDPI.Tray.exe.config","x86/goodbyedpi.exe","x86/WinDivert.dll","x86/WinDivert32.sys","x86/WinDivert64.sys","x86_64/goodbyedpi.exe","x86_64/WinDivert.dll","x86_64/WinDivert64.sys","licenses/LICENSE-getline.txt","licenses/LICENSE-goodbyedpi.txt","licenses/LICENSE-uthash.txt","licenses/LICENSE-windivert.txt","licenses/LICENSE-goodbyedpi-tray.txt","licenses/NOTICE-goodbyedpi-tray.txt","licenses/THIRD_PARTY_NOTICES.md","zapret2/licenses/CYGWIN_LICENSE.txt","zapret2/licenses/GPL-3.0.txt","zapret2/licenses/LGPL-3.0.txt","zapret2/licenses/LICENSE-windivert.txt","zapret2/licenses/LICENSE-zapret2.txt","zapret2/x86/cygwin1.dll","zapret2/x86/WinDivert.dll","zapret2/x86/WinDivert32.sys","zapret2/x86/winws2.exe","zapret2/x86/zapret-antidpi.lua","zapret2/x86/zapret-lib.lua","zapret2/x86_64/cygwin1.dll","zapret2/x86_64/WinDivert.dll","zapret2/x86_64/WinDivert64.sys","zapret2/x86_64/winws2.exe","zapret2/x86_64/zapret-antidpi.lua","zapret2/x86_64/zapret-lib.lua"};
  internal static readonly string[] Files=Array.FindAll(AllFiles,delegate(string file) { return Program.ZapretEnabled || !file.StartsWith("zapret2/",StringComparison.Ordinal); });
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool MoveFileEx(string existing,string destination,int flags);
  static string App { get { return Path.Combine(Program.InstallDir,"GoodbyeDPI.Tray.exe"); } }
  internal static string Target(string relative) {
   string root=Path.GetFullPath(Program.InstallDir)+Path.DirectorySeparatorChar;
   string target=Path.GetFullPath(Path.Combine(root,relative.Replace('/',Path.DirectorySeparatorChar)));
   if(!target.StartsWith(root,StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException(L.T("Geçersiz paket yolu."));
   return target;
  }
  static void ValidateDirectory() {
   var dir=new DirectoryInfo(Program.InstallDir);
   while(dir!=null) { if(dir.Exists && (dir.Attributes & FileAttributes.ReparsePoint)!=0) throw new IOException(L.T("Kurulum yolunda bağlantı noktası var: ")+dir.FullName); dir=dir.Parent; }
   foreach(string file in Files) {
    string path=Target(file); var parent=new DirectoryInfo(Path.GetDirectoryName(path));
    if(parent.Exists && (parent.Attributes & FileAttributes.ReparsePoint)!=0) throw new IOException(L.T("Bağlantılı klasöre kurulum yapılamaz."));
    if(File.Exists(path) && (File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0) throw new IOException(L.T("Bağlantılı dosyaya yazılamaz."));
   }
   foreach(string file in new[]{SetupName,LegacySetupName,UninstallShortcut,"uninstall.ps1","engine-console.txt","engine-console.log"}) if(File.Exists(Target(file)) && (File.GetAttributes(Target(file))&FileAttributes.ReparsePoint)!=0) throw new IOException(L.T("Bağlantılı kurulum dosyası bulundu."));
  }
  static bool Inside(string path,string parent) { if(String.IsNullOrEmpty(parent)) return false; parent=parent.TrimEnd('\\'); return String.Equals(path,parent,StringComparison.OrdinalIgnoreCase) || path.StartsWith(parent+"\\",StringComparison.OrdinalIgnoreCase); }
  static bool UnderProgramFiles(string dir) { return Inside(dir,Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)) || Inside(dir,Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)); }
  // Why a chosen folder cannot be used, or null when it can.
  internal static string CheckLocation(string dir) {
   // Only full local paths like D:\Klasör; network paths and "\Klasör" (no drive letter) are refused.
   if(String.IsNullOrWhiteSpace(dir) || dir.Length<3 || !Char.IsLetter(dir[0]) || dir[1]!=':' || dir[2]!='\\') return L.T("Bu bilgisayardaki bir klasör seç.");
   string full;
   try { full=Path.GetFullPath(dir).TrimEnd('\\'); } catch(Exception) { return L.T("Klasör yolu geçersiz."); }
   string root=Path.GetPathRoot(full);
   if(String.Equals(full,root.TrimEnd('\\'),StringComparison.OrdinalIgnoreCase)) return L.T("Sürücünün köküne kurulamaz, bir klasör seç.");
   try { if(new DriveInfo(root).DriveType!=DriveType.Fixed) return L.T("Yalnızca bu bilgisayardaki sabit disklere kurulabilir."); } catch(ArgumentException) { return L.T("Klasör yolu geçersiz."); }
   if(Inside(full,Environment.GetFolderPath(Environment.SpecialFolder.Windows))) return L.T("Windows klasörüne kurulamaz.");
   if(full.Length>180) return L.T("Klasör yolu çok uzun.");
   if(Directory.Exists(full) && Directory.GetFileSystemEntries(full).Length>0 && !File.Exists(Path.Combine(full,"GoodbyeDPI.Tray.exe"))) return L.T("Bu klasör boş değil. Boş ya da yeni bir klasör seç.");
   return null;
  }
  // The service runs as SYSTEM from this folder. Outside Program Files only admins and SYSTEM may change it;
  // otherwise any program could swap the executable and gain SYSTEM rights.
  static void SecureDirectory(string dir) {
   var admins=new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid,null);
   var security=new System.Security.AccessControl.DirectorySecurity();
   security.SetAccessRuleProtection(true,false); security.SetOwner(admins);
   var inherit=System.Security.AccessControl.InheritanceFlags.ContainerInherit|System.Security.AccessControl.InheritanceFlags.ObjectInherit;
   foreach(var rule in new[]{
    new System.Security.AccessControl.FileSystemAccessRule(admins,System.Security.AccessControl.FileSystemRights.FullControl,inherit,System.Security.AccessControl.PropagationFlags.None,System.Security.AccessControl.AccessControlType.Allow),
    new System.Security.AccessControl.FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid,null),System.Security.AccessControl.FileSystemRights.FullControl,inherit,System.Security.AccessControl.PropagationFlags.None,System.Security.AccessControl.AccessControlType.Allow),
    new System.Security.AccessControl.FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid,null),System.Security.AccessControl.FileSystemRights.ReadAndExecute,inherit,System.Security.AccessControl.PropagationFlags.None,System.Security.AccessControl.AccessControlType.Allow)})
    security.AddAccessRule(rule);
   Directory.SetAccessControl(dir,security);
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
    if(String.Equals(process.MainModule.FileName,App,StringComparison.OrdinalIgnoreCase)) { if(!process.WaitForExit(4000)) { process.Kill(); if(!process.WaitForExit(10000)) throw new IOException(L.T("Arayüz kapatılamadı.")); } }
   }
  }
  static void RunApp(string arguments) {
   using(var process=Process.Start(new ProcessStartInfo(App,arguments) { UseShellExecute=false,CreateNoWindow=true })) { process.WaitForExit(); if(process.ExitCode!=0) throw new InvalidOperationException(L.T("Hizmet ayarlanamadı. Kurulum dosyasıyla tekrar dene.")); }
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
   Report(5,L.T("Kurulum dosyaları kontrol ediliyor…"));
   ValidateDirectory();
   using(var net=Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full")) if(net==null || Convert.ToInt32(net.GetValue("Release",0))<528040) throw new InvalidOperationException(L.T(".NET Framework 4.8 gerekiyor. Windows Update'ten yükleyip tekrar dene."));
   string previousCommand=Program.ServicePath();
   bool existing=previousCommand!=null;
   // With the user's consent, a foreign GoodbyeDPI service is taken over: repointed to this app, restored on failure.
   bool foreign=existing && replaceForeign && Program.ForeignProfile()>=0;
   int previousProfile=foreign?Program.ForeignProfile():existing?Program.UpgradeProfile(Program.OwnedProfile()):0;
   if(foreign) AppInfo.Log("Eski GoodbyeDPI hizmeti devralınıyor: "+previousCommand);
   else if(existing) Program.RequireOwned();
   else if(Process.GetProcessesByName("goodbyedpi").Length>0) throw new InvalidOperationException(L.T("Açık GoodbyeDPI penceresini kapatıp tekrar dene."));
   // Check every embedded resource before stopping an existing installation.
   foreach(string file in Files) using(var input=Assembly.GetExecutingAssembly().GetManifestResourceStream("payload/"+file)) if(input==null) throw new IOException(L.T("Pakette eksik dosya: ")+file);
   var backup=new System.Collections.Generic.Dictionary<string,byte[]>();
   var tracked=new System.Collections.Generic.List<string>(Files); tracked.Add(SetupName); tracked.Add(LegacySetupName); tracked.Add(UninstallShortcut);
   foreach(string file in tracked) { string path=Target(file); backup[file]=File.Exists(path)?File.ReadAllBytes(path):null; }
   bool hadRegistration;
   var registrationValues=new System.Collections.Generic.Dictionary<string,object>();
   var registrationKinds=new System.Collections.Generic.Dictionary<string,RegistryValueKind>();
   using(var registration=Registry.LocalMachine.OpenSubKey(UninstallKey)) { hadRegistration=registration!=null; if(hadRegistration) foreach(string name in registration.GetValueNames()) { registrationValues[name]=registration.GetValue(name,null,RegistryValueOptions.DoNotExpandEnvironmentNames); registrationKinds[name]=registration.GetValueKind(name); } }
   bool running=false; Report(15,L.T("Uygulama güncellemeye hazırlanıyor…"));
   if(existing) { using(var svc=new System.ServiceProcess.ServiceController(Program.ServiceName)) running=svc.Status==System.ServiceProcess.ServiceControllerStatus.Running; Program.Stop(); }
   CloseOwnedUI(); bool createdDir=!Directory.Exists(Program.InstallDir); Directory.CreateDirectory(Program.InstallDir);
   if(!UnderProgramFiles(Program.InstallDir)) SecureDirectory(Program.InstallDir);
   try {
    int copied=0;
    foreach(string file in Files) {
     Report(20+55*copied/Files.Length,L.T("Uygulama dosyaları hazırlanıyor…"));
     string path=Target(file); Directory.CreateDirectory(Path.GetDirectoryName(path));
     using(var input=Assembly.GetExecutingAssembly().GetManifestResourceStream("payload/"+file)) {
      if(!Matches(path,input)) using(var output=new FileStream(path,FileMode.Create,FileAccess.Write,FileShare.None)) input.CopyTo(output);
     }
     copied++;
    }
    CreateShortcut(Target(UninstallShortcut),App,"--uninstall");
    // Older builds left an installer copy and wrote the console output to engine-console.txt.
    foreach(string old in new[]{SetupName,LegacySetupName,"engine-console.txt"}) try { DeleteOrSchedule(Target(old)); } catch(Exception ex) { AppInfo.Log("Eski dosya silinemedi: "+ex.Message); }
    Report(80,L.T("Windows kaydı oluşturuluyor…")); RegisterUninstaller();
    Report(88,L.T("GoodbyeDPI hizmeti hazırlanıyor…"));
    if(existing) {
     Program.Configure(previousProfile);
     if(foreign) { Program.Sc("config GoodbyeDPI DisplayName= \"GoodbyeDPI Tray Engine\""); Program.Sc("description GoodbyeDPI \"GoodbyeDPI Tray tarafından yönetilen Türkiye profili\""); }
     Program.GrantUserControl();
     if(running) Program.Start();
    } else RunApp("--admin install --quiet");
    AppInfo.Log("Kurulum tamamlandı "+AppInfo.Version);
    Report(95,L.T("Kurulum tamamlanıyor…"));
   } catch(Exception failure) {
    AppInfo.Log("Kurulum başarısız: "+Stage+" "+failure);
    try {
     if(Program.OwnedProfile()>=0) Program.Stop();
     if(!existing && Program.OwnedProfile()>=0) Program.Sc("delete GoodbyeDPI");
     if(existing) Program.Sc("config GoodbyeDPI binPath= \""+previousCommand.Replace("\"","\\\"")+"\"");
     if(foreign) { Program.Sc("config GoodbyeDPI DisplayName= \"GoodbyeDPI\""); Program.Sc("sdset GoodbyeDPI "+Program.ServiceSecurity.Replace("(A;;CCLCSWRPWPLOCRRC;;;IU)","(A;;CCLCSWLOCRRC;;;IU)")); }
     foreach(var item in backup) { string path=Target(item.Key); if(item.Value==null) { if(File.Exists(path)) File.Delete(path); } else if(!String.Equals(path,Application.ExecutablePath,StringComparison.OrdinalIgnoreCase)) File.WriteAllBytes(path,item.Value); }
     if(!hadRegistration) Registry.LocalMachine.DeleteSubKeyTree(UninstallKey,false);
     else using(var registration=Registry.LocalMachine.CreateSubKey(UninstallKey)) { foreach(string name in registration.GetValueNames()) if(!registrationValues.ContainsKey(name)) registration.DeleteValue(name,false); foreach(var value in registrationValues) registration.SetValue(value.Key,value.Value,registrationKinds[value.Key]); }
     if(existing && running) using(var restored=new System.ServiceProcess.ServiceController(Program.ServiceName)) { restored.Start(); restored.WaitForStatus(System.ServiceProcess.ServiceControllerStatus.Running,TimeSpan.FromSeconds(20)); }
     if(createdDir && Directory.Exists(Program.InstallDir) && Directory.GetFileSystemEntries(Program.InstallDir).Length==0) Directory.Delete(Program.InstallDir);
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
   Report(5,L.T("Kaldırma hazırlanıyor…"));
   ValidateDirectory();
   if(Program.ServicePath()!=null) { Program.RequireOwned(); Program.Stop(); Program.Sc("delete GoodbyeDPI"); }
   Report(20,L.T("Uygulama kapatılıyor…")); CloseOwnedUI();
   Report(30,L.T("Sürücü kaldırılıyor…")); RemoveOwnedDriver();
   int removed=0;
   foreach(string file in Files) { DeleteOrSchedule(Target(file)); Report(35+45*(++removed)/Files.Length,L.T("Uygulama dosyaları kaldırılıyor…")); }
   DeleteOrSchedule(Target("uninstall.ps1")); DeleteOrSchedule(Target("engine-console.txt")); DeleteOrSchedule(Target("engine-console.log")); DeleteOrSchedule(Target("engine-request-error.log")); DeleteOrSchedule(Target(SetupName)); DeleteOrSchedule(Target(LegacySetupName)); DeleteOrSchedule(Target(UninstallShortcut));
   foreach(string folder in new[]{"x86","x86_64","licenses","zapret2/x86","zapret2/x86_64","zapret2/licenses","zapret2"}) { string path=Target(folder); if(Directory.Exists(path) && Directory.GetFileSystemEntries(path).Length==0) Directory.Delete(path); }
   Registry.LocalMachine.DeleteSubKeyTree(UninstallKey,false);
   // Never recursively delete an installation directory: preserve unknown user files.
   if(Directory.Exists(Program.InstallDir) && Directory.GetFileSystemEntries(Program.InstallDir).Length==0) Directory.Delete(Program.InstallDir);
   foreach(string file in trackedFiles()) if(File.Exists(Target(file)) && !RebootRequired) throw new IOException(L.T("Dosya kaldırılamadı: ")+Target(file));
   AppInfo.Log("Kaldırma tamamlandı. Yeniden başlatma: "+RebootRequired);
   Report(95,L.T("Kaldırma tamamlanıyor…"));
  }
  static string[] trackedFiles() { var list=new System.Collections.Generic.List<string>(Files); list.Add(SetupName); list.Add(LegacySetupName); list.Add(UninstallShortcut); list.Add("uninstall.ps1"); list.Add("engine-console.txt"); list.Add("engine-console.log"); return list.ToArray(); }
  internal static void UserSetup(bool remove,bool preserveStartup=false) {
   Stage=L.T("Kullanıcı başlangıcı ve kısayollar ayarlanıyor");
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
  internal static int Elevate(bool remove,bool replaceForeign,string location,IProgress<Tuple<int,string>> progress) {
   // UAC cannot always see a mapped/shared source drive. Use a local copy.
   string stageDir=Path.Combine(Path.GetTempPath(),"GoodbyeDPI-Setup-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(stageDir);
   string executable=CopySelf(stageDir);
   string progressFile=Path.Combine(stageDir,"progress.txt");
   try { using(var p=Process.Start(new ProcessStartInfo(executable,(remove?"--remove-elevated":"--install-elevated")+(replaceForeign?" --replace-foreign":"")+(location!=null?" --location \""+location+"\"":"")+" --report-progress") { UseShellExecute=true,Verb="runas",WorkingDirectory=stageDir })) {
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
     if(!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator)) throw new UnauthorizedAccessException(L.T("Yönetici izni gerekiyor."));
     // A chosen folder only counts for a first install; an existing installation stays where it is.
     int at=Array.IndexOf(args,"--location");
     if(args[0]=="--install-elevated" && at>0 && at+1<args.Length && !File.Exists(Path.Combine(Program.InstallDir,"GoodbyeDPI.Tray.exe"))) {
      string problem=CheckLocation(args[at+1]); if(problem!=null) throw new InvalidOperationException(problem);
      Program.InstallDir=args[at+1];
     }
     bool created; using(var mutex=new Mutex(true,@"Global\GoodbyeDPI.Tray.Setup",out created)) { if(!created) throw new InvalidOperationException(L.T("Başka bir kurulum işlemi devam ediyor.")); if(args[0]=="--install-elevated") Install(Array.IndexOf(args,"--replace-foreign")>=0); else Uninstall(); } return RebootRequired?3010:0;
    }
    if(args.Length==2 && args[0]=="--preview") { using(var form=new SetupForm(false)) { form.Show(); Application.DoEvents(); using(var bitmap=new Bitmap(form.Width,form.Height)) { form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size)); bitmap.Save(args[1]); } } return 0; }
    bool remove=args.Length>=1 && args[0]=="--uninstall";
    if(remove && args.Length==1 && InsideInstallDir(Application.ExecutablePath)) { RelaunchFromTemp(); return 0; }
    Application.Run(new SetupForm(remove));
    if(Array.IndexOf(args,"--temporary")>=0) DeleteSelfLater();
    return 0;
   } catch(Exception ex) { string message=Stage+"\n"+ex.Message+L.T("\nGünlük: ")+AppInfo.LogPath; AppInfo.Log(message+"\n"+ex); if(Array.IndexOf(args,"--quiet")<0) MessageBox.Show(message,"GoodbyeDPI Kurulum",MessageBoxButtons.OK,MessageBoxIcon.Error); return 1; }
  }
 }
 // Thin rounded progress track in the theme colors.
 class PaletteProgressBar : ProgressBar {
  public PaletteProgressBar() { SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true); }
  internal Color Track;
  protected override void OnPaint(PaintEventArgs e) {
   var g=e.Graphics; g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias; g.Clear(BackColor);
   using(var path=SoftButton.Round(new Rectangle(0,0,Width-1,Height-1),Height)) using(var brush=new SolidBrush(Track)) g.FillPath(brush,path);
   int filled=(Width-1)*(Value-Minimum)/Math.Max(1,Maximum-Minimum);
   if(filled>=Height) using(var path=SoftButton.Round(new Rectangle(0,0,filled,Height-1),Height)) using(var brush=new SolidBrush(ForeColor)) g.FillPath(brush,path);
  }
 }
 // A check box drawn like the rest of the app: rounded box, accent fill and a check mark in the accent's text color.
 class SoftCheck : CheckBox {
  internal Color Box,Accent,AccentInk;
  public SoftCheck() { SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true); Cursor=Cursors.Hand; }
  protected override void OnPaint(PaintEventArgs e) {
   var g=e.Graphics; g.Clear(BackColor); g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
   var box=new Rectangle(1,(Height-20)/2,20,20);
   using(var path=SoftButton.Round(box,7)) {
    if(Checked) using(var fill=new SolidBrush(Accent)) g.FillPath(fill,path);
    else using(var edge=new Pen(Box,1.5f)) g.DrawPath(edge,path);
   }
   if(Checked) using(var pen=new Pen(AccentInk,2f) { StartCap=System.Drawing.Drawing2D.LineCap.Round,EndCap=System.Drawing.Drawing2D.LineCap.Round,LineJoin=System.Drawing.Drawing2D.LineJoin.Round })
    g.DrawLines(pen,new[]{new PointF(box.X+5.5f,box.Y+10.5f),new PointF(box.X+8.8f,box.Y+13.8f),new PointF(box.X+14.8f,box.Y+6.8f)});
   TextRenderer.DrawText(g,Text,Font,new Rectangle(32,0,Width-32,Height),Enabled?ForeColor:Box,TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix);
   if(Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(g,new Rectangle(28,4,Math.Min(Width-30,TextRenderer.MeasureText(Text,Font).Width+8),Height-8),ForeColor,BackColor);
  }
 }
 // Two pages: the welcome page with the notice and consent, then the progress page once setup starts.
 class SetupForm : Form {
  bool busy,completed;
  readonly Panel welcome=new Panel(),working=new Panel();
  readonly Label title=new Label(),info=new Label(),step=new Label(),percentage=new Label();
  readonly UpdateStatusIcon stateIcon=new UpdateStatusIcon();
  readonly PaletteProgressBar progressBar=new PaletteProgressBar();
  readonly SoftButton start=new SoftButton(),cancel=new SoftButton(),open=new SoftButton();
  readonly SoftCheck consent=new SoftCheck();
  Color line;
  // A folder picked for a first install; null keeps Program Files.
  string chosenLocation;
  void ShowWorking() { start.Visible=false; if(working.Visible) return; welcome.Visible=false; working.Visible=true; }
  internal void ShowProgress(int value,string message) {
   if(completed || !busy || value<progressBar.Value) return;
   ShowWorking(); stateIcon.State=UpdateState.Checking;
   progressBar.Visible=true; percentage.Visible=true;
   progressBar.Value=Math.Max(progressBar.Value,Math.Max(0,Math.Min(100,value)));
   progressBar.Invalidate(); percentage.Text="%"+progressBar.Value; step.Text=message;
  }
  internal void ShowCompletion(bool remove,bool reboot) {
   completed=true; ShowWorking(); stateIcon.State=UpdateState.Current;
   progressBar.Visible=true; percentage.Visible=true; progressBar.Value=100; progressBar.Invalidate(); percentage.Text="%100";
   title.Text=remove?L.T("Kaldırma tamamlandı"):L.T("Kurulum tamamlandı");
   step.Text=reboot?L.T("Bitirmek için bilgisayarı yeniden başlat."):L.T("Tamamdır.");
   info.Text=remove?L.T("GoodbyeDPI Tray bilgisayarından tamamen kaldırıldı."):L.T("GoodbyeDPI hazır. Şimdi açabilir ya da sonra Başlat menüsünden bulabilirsin.");
   start.Visible=false; cancel.Text=L.T("Bitir"); cancel.Invalidate(); open.Visible=!remove; AcceptButton=remove?cancel:(IButtonControl)open;
  }
  void ShowFailure(string heading,string detail,string hint) {
   stateIcon.State=UpdateState.Failed; title.Text=heading; info.Text=detail; step.Text=hint;
   progressBar.Visible=false; percentage.Visible=false; start.Text=L.T("Yeniden dene"); start.Visible=true; start.Invalidate();
  }
  public SetupForm(bool remove) {
   bool wasInstalled=File.Exists(Path.Combine(Program.InstallDir,"GoodbyeDPI.Tray.exe"));
   // A first install follows Windows; an update or a removal keeps the theme the user already has.
   string theme=AppPalette.StoredTheme() ?? (wasInstalled || remove?"black":"system");
   bool dark=theme=="system"?AppPalette.WindowsDark():AppPalette.IsDark(theme); AppPalette.Variant=AppPalette.VariantOf(theme);
   bool contrast=SystemInformation.HighContrast;
   Color background=contrast?SystemColors.Window:AppPalette.Background(dark), surface=contrast?SystemColors.Window:AppPalette.Surface(dark), text=contrast?SystemColors.WindowText:AppPalette.Text(dark), muted=contrast?SystemColors.WindowText:AppPalette.Muted(dark);
   Color accent=contrast?SystemColors.Highlight:AppPalette.Accent, accentInk=contrast?SystemColors.HighlightText:AppPalette.AccentInk; line=contrast?SystemColors.WindowText:AppPalette.Border(dark);
   int width=remove?560:600, height=remove?380:600, bottom=height-72;
   Text=remove?L.T("GoodbyeDPI — Kaldır"):L.T("GoodbyeDPI — Kurulum"); ClientSize=new Size(width,height); FormBorderStyle=FormBorderStyle.FixedDialog; MaximizeBox=false; MinimizeBox=true; StartPosition=FormStartPosition.CenterScreen;
   Font=new Font("Segoe UI",10); BackColor=background; ForeColor=text; Icon=Program.AppIcon; DoubleBuffered=true;
   // Header: icon, name and version.
   var logo=new Panel { Bounds=new Rectangle(30,26,52,52),BackColor=background }; logo.Paint+=delegate(object sender,PaintEventArgs e) { e.Graphics.DrawIcon(Program.AppIcon,new Rectangle(0,0,52,52)); }; Controls.Add(logo);
   Controls.Add(new Label { Text="GoodbyeDPI Tray",Font=new Font("Segoe UI",19,FontStyle.Bold),Bounds=new Rectangle(94,24,width-124,38),ForeColor=text });
   Controls.Add(new Label { Text=L.T("Sürüm ")+AppInfo.Version+L.T("  ·  Geliştirici: Furkan"),Bounds=new Rectangle(96,62,width-126,22),ForeColor=muted,Font=new Font("Segoe UI",9.5f) });
   var pages=new Rectangle(0,104,width,bottom-112);
   welcome.Bounds=pages; working.Bounds=pages; welcome.BackColor=working.BackColor=background; Controls.Add(welcome); Controls.Add(working); working.Visible=false;
   // Welcome page.
   welcome.Controls.Add(new Label { Text=remove?L.T("GoodbyeDPI'ı kaldır"):wasInstalled?L.T("Güncellemeye hazır"):L.T("Kuruluma hazır"),Font=new Font("Segoe UI",13,FontStyle.Bold),Bounds=new Rectangle(30,0,width-60,30),ForeColor=text });
   var card=new SoftPanel { Bounds=new Rectangle(30,38,width-60,remove?70:64),Fill=surface,Edge=line,BackColor=background,Padding=Padding.Empty }; welcome.Controls.Add(card);
   if(remove) card.Controls.Add(new Label { Text=L.T("GoodbyeDPI Tray, ayarları ve günlükleriyle birlikte bilgisayarından tamamen kaldırılacak."),Bounds=new Rectangle(18,12,card.Width-36,46),ForeColor=text,BackColor=surface });
   else {
    card.Controls.Add(new Label { Text=L.T("Kurulum konumu"),Bounds=new Rectangle(18,12,card.Width-(wasInstalled?36:150),20),ForeColor=muted,BackColor=surface,Font=new Font("Segoe UI",9) });
    var path=new Label { Text=Program.InstallDir,Bounds=new Rectangle(18,32,card.Width-(wasInstalled?36:150),22),ForeColor=text,BackColor=surface,AutoEllipsis=true }; card.Controls.Add(path);
    if(!wasInstalled) {
     var change=new SoftButton { Text=L.T("Değiştir"),Bounds=new Rectangle(card.Width-122,15,104,34),BackColor=surface,Fill=background,Ink=text,Edge=line,AccessibleName=L.T("Kurulum konumunu değiştir") }; card.Controls.Add(change); change.BringToFront();
     change.Click+=delegate {
      if(busy || completed) return;
      using(var picker=new FolderBrowserDialog { Description=L.T("GoodbyeDPI Tray'in kurulacağı klasörü seç. Seçtiğin yerde \"GoodbyeDPI Tray\" klasörü açılır."),ShowNewFolderButton=true,SelectedPath=Path.GetDirectoryName(Program.InstallDir) }) {
       if(picker.ShowDialog(this)!=DialogResult.OK) return;
       string picked=picker.SelectedPath.TrimEnd('\\');
       string target=String.Equals(Path.GetFileName(picked),"GoodbyeDPI Tray",StringComparison.OrdinalIgnoreCase)?picked:Path.Combine(picked,"GoodbyeDPI Tray");
       string problem=Setup.CheckLocation(target);
       if(problem!=null) { MessageBox.Show(this,problem,L.T("Kurulum konumu"),MessageBoxButtons.OK,MessageBoxIcon.Warning); return; }
       Program.InstallDir=target; path.Text=Program.InstallDir;
       chosenLocation=String.Equals(Program.InstallDir,Program.DefaultInstallDir,StringComparison.OrdinalIgnoreCase)?null:Program.InstallDir;
      }
     };
    }
    welcome.Controls.Add(new Label { Text=L.T("Kullanmadan önce"),Bounds=new Rectangle(30,118,width-60,22),ForeColor=muted,Font=new Font("Segoe UI",9.5f,FontStyle.Bold) });
    var notice=new NoticeView { Text=AppInfo.Notice,ThumbColor=muted,EdgeColor=line,BackColor=surface,ForeColor=text,TabStop=true,FocusFrame=false,Bounds=new Rectangle(30,142,width-60,132) };
    notice.AccessibleName=L.T("Kullanmadan önce"); welcome.Controls.Add(notice);
    consent.Text=L.T("Okudum, anladım."); consent.Bounds=new Rectangle(30,286,width-60,32); consent.Checked=false; consent.BackColor=background; consent.ForeColor=text; consent.Box=muted; consent.Accent=accent; consent.AccentInk=accentInk; welcome.Controls.Add(consent);
   }
   // Progress page.
   stateIcon.Bounds=new Rectangle(30,6,30,30); stateIcon.BackColor=background; stateIcon.State=UpdateState.Checking; working.Controls.Add(stateIcon);
   title.Font=new Font("Segoe UI",16,FontStyle.Bold); title.Bounds=new Rectangle(72,2,width-102,38); title.ForeColor=text; title.Text=remove?L.T("GoodbyeDPI kaldırılıyor"):L.T("GoodbyeDPI kuruluyor"); working.Controls.Add(title);
   info.Bounds=new Rectangle(32,52,width-64,remove?48:64); info.ForeColor=muted; info.Text=remove?L.T("Bu birkaç saniye sürer."):L.T("Dosyalar kopyalanıyor ve GoodbyeDPI hizmeti hazırlanıyor. Bu birkaç saniye sürer."); working.Controls.Add(info);
   int barTop=remove?104:124;
   step.Bounds=new Rectangle(32,barTop,width-150,24); step.ForeColor=text; working.Controls.Add(step);
   percentage.Bounds=new Rectangle(width-118,barTop,86,24); percentage.TextAlign=ContentAlignment.MiddleRight; percentage.ForeColor=muted; percentage.Text="%0"; working.Controls.Add(percentage);
   progressBar.Bounds=new Rectangle(32,barTop+32,width-64,8); progressBar.BackColor=background; progressBar.Track=line; progressBar.ForeColor=accent; progressBar.Style=ProgressBarStyle.Continuous; working.Controls.Add(progressBar);
   progressBar.Visible=false; percentage.Visible=false;
   // Bottom bar: a divider, the developer link and the buttons on the right.
   Paint+=delegate(object sender,PaintEventArgs e) { using(var pen=new Pen(line)) e.Graphics.DrawLine(pen,0,bottom-8,width,bottom-8); };
   if(!remove) { var developer=ProjectLinks.Create("github.com/trfurkan",AppInfo.DeveloperUrl,contrast?SystemColors.HotTrack:dark?AppPalette.Mint:AppPalette.Accent,true); developer.Bounds=new Rectangle(30,bottom+10,220,32); Controls.Add(developer); }
   start.Text=remove?L.T("Kaldır"):wasInstalled?L.T("Güncelle"):L.T("Kur"); start.Bounds=new Rectangle(width-30-140,bottom+6,140,40); Controls.Add(start);
   open.Text=L.T("GoodbyeDPI'ı aç"); open.Bounds=start.Bounds; open.Visible=false; Controls.Add(open);
   cancel.Text=L.T("Vazgeç"); cancel.Bounds=new Rectangle(start.Left-12-120,bottom+6,120,40); cancel.Click+=delegate { Close(); }; Controls.Add(cancel); CancelButton=cancel; AcceptButton=start;
   foreach(var button in new[]{start,open,cancel}) { bool primary=button!=cancel; button.BackColor=background; button.Fill=primary?accent:surface; button.Ink=primary?accentInk:text; button.Edge=primary?accent:line; }
   if(!remove) { start.Enabled=false; consent.CheckedChanged+=delegate { start.Enabled=consent.Checked && !busy && !completed; consent.Invalidate(); }; }
   HandleCreated+=delegate { WindowsIntegration.ThemeCaption(this,dark,background,text); };
   FormClosing+=delegate(object sender,FormClosingEventArgs e) { if(busy) e.Cancel=true; };
   open.Click+=delegate {
    try { Process.Start(new ProcessStartInfo(Path.Combine(Program.InstallDir,"GoodbyeDPI.Tray.exe")) { UseShellExecute=true }); Close(); }
    catch(Exception ex) { info.Text=L.T("Uygulama açılamadı. Başlat menüsünden açmayı dene.\n")+ex.Message; AppInfo.Log(ex.ToString()); }
   };
   start.Click+=async delegate {
    if(busy || completed || (!remove && !consent.Checked)) return;
    bool replaceForeign=false;
    if(!remove && Program.ForeignProfile()>=0) {
     if(MessageBox.Show(this,L.T("Bilgisayarda daha önce kurulmuş başka bir GoodbyeDPI hizmeti var:\n\n")+Program.ServicePath()+L.T("\n\nBu hizmeti GoodbyeDPI Tray'e devredelim mi? Eski hizmet durdurulur ve artık bu uygulama yönetir. Eski klasörü sonra silebilirsin."),"GoodbyeDPI Kurulum",MessageBoxButtons.YesNo,MessageBoxIcon.Question,MessageBoxDefaultButton.Button1)!=DialogResult.Yes) return;
     replaceForeign=true;
    }
    busy=true; progressBar.Value=0; start.Enabled=false; cancel.Enabled=false;
    title.Text=remove?L.T("GoodbyeDPI kaldırılıyor"):L.T("GoodbyeDPI kuruluyor");
    ShowProgress(0,L.T("Windows izin isteyecek, onayla."));
    var progress=new Progress<Tuple<int,string>>(delegate(Tuple<int,string> update) { ShowProgress(update.Item1,update.Item2); });
    try {
     int code=await Task.Run(delegate { return Setup.Elevate(remove,replaceForeign,chosenLocation,progress); });
     if(code!=0 && code!=3010) throw new InvalidOperationException(L.T("Bir sorun çıktı.\nAyrıntı: ")+AppInfo.LogPath);
     ShowProgress(97,remove?L.T("Kısayollar temizleniyor…"):L.T("Kısayollar hazırlanıyor…"));
     Setup.UserSetup(remove,wasInstalled);
     if(remove) Setup.ClearUserSettings();
     else using(var preferences=Registry.CurrentUser.CreateSubKey(@"Software\GoodbyeDPITray")) {
      preferences.SetValue("UsageAcknowledged",1);
      // The app opens in the theme the installer showed.
      if(AppPalette.StoredTheme()==null) preferences.SetValue("Theme",dark?"black":"light");
     }
     ShowCompletion(remove,code==3010);
    } catch(System.ComponentModel.Win32Exception ex) {
     if(ex.NativeErrorCode==1223) ShowFailure(L.T("İzin verilmedi"),L.T("Windows izni onaylanmadı, hiçbir şey değişmedi."),"Tekrar denersen izni onaylaman yeterli.");
     else ShowFailure(L.T("İşlem tamamlanamadı"),ex.Message,L.T("Tekrar dene."));
    } catch(Exception ex) { ShowFailure(L.T("İşlem tamamlanamadı"),ex.Message,L.T("Sorunu giderip tekrar dene.")); }
    finally { busy=false; cancel.Enabled=true; start.Enabled=!completed; }
   };
  }
 }
}
