using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;
using System.Threading;

namespace GoodbyeTray {
 // The child inherits a real console, so native printf output is not held in a pipe buffer.
 sealed class NativeConsoleSession : IDisposable {
  [StructLayout(LayoutKind.Sequential)] struct Coord { public short X,Y; public Coord(short x,short y) { X=x; Y=y; } }
  [StructLayout(LayoutKind.Sequential)] struct Rect { public short Left,Top,Right,Bottom; }
  [StructLayout(LayoutKind.Sequential)] struct BufferInfo { public Coord Size,Cursor; public short Attributes; public Rect Window; public Coord Maximum; }
  [StructLayout(LayoutKind.Sequential)] struct BasicLimits { public long ProcessTime,JobTime; public uint Flags; public UIntPtr Minimum,Maximum; public uint ActiveProcesses; public UIntPtr Affinity; public uint Priority,Scheduling; }
  [StructLayout(LayoutKind.Sequential)] struct IoCounters { public ulong ReadOperations,WriteOperations,OtherOperations,ReadBytes,WriteBytes,OtherBytes; }
  [StructLayout(LayoutKind.Sequential)] struct ExtendedLimits { public BasicLimits Basic; public IoCounters Io; public UIntPtr ProcessMemory,JobMemory,PeakProcessMemory,PeakJobMemory; }
  [DllImport("kernel32.dll",SetLastError=true)] static extern bool AllocConsole();
  [DllImport("kernel32.dll")] static extern bool FreeConsole();
  [DllImport("kernel32.dll")] static extern IntPtr GetConsoleWindow();
  [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr window,int command);
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern IntPtr CreateFile(string name,uint access,uint share,IntPtr security,uint disposition,uint flags,IntPtr template);
  [DllImport("kernel32.dll",SetLastError=true)] static extern bool SetConsoleScreenBufferSize(IntPtr output,Coord size);
  [DllImport("kernel32.dll",SetLastError=true)] static extern bool GetConsoleScreenBufferInfo(IntPtr output,out BufferInfo info);
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool ReadConsoleOutputCharacter(IntPtr output,StringBuilder text,uint length,Coord origin,out uint read);
  [DllImport("kernel32.dll",SetLastError=true)] static extern IntPtr CreateJobObject(IntPtr security,string name);
  [DllImport("kernel32.dll",SetLastError=true)] static extern bool SetInformationJobObject(IntPtr job,int type,ref ExtendedLimits limits,uint length);
  [DllImport("kernel32.dll",SetLastError=true)] static extern bool AssignProcessToJobObject(IntPtr job,IntPtr process);
  [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
  [DllImport("kernel32.dll",SetLastError=true)] static extern bool GenerateConsoleCtrlEvent(uint signal,uint group);
  delegate bool ConsoleControlHandler(uint signal);
  [DllImport("kernel32.dll",SetLastError=true)] static extern bool SetConsoleCtrlHandler(ConsoleControlHandler handler,bool add);
  // AllocConsole/FreeConsole resets Windows' handler table. Console.CancelKeyPress caches
  // its registration across sessions, so it cannot protect a repeatedly reattached host.
  // Keep the native delegate alive and register it for every new console. Unlike the
  // NULL/ignore handler, this does not make the child inherit an ignored Ctrl+C state.
  static readonly ConsoleControlHandler KeepHostAlive=delegate(uint signal) { return signal==0 || signal==1; };
  [DllImport("dnsapi.dll")] static extern bool DnsFlushResolverCache();
  // Answers cached before the engine started still point at the block page; drop them so redirected DNS takes effect at once.
  internal static void FlushDns() { try { DnsFlushResolverCache(); } catch(Exception ex) { AppInfo.Log("DNS önbelleği: "+ex.Message); } }
  IntPtr output,job; bool allocated,disposed;
  internal Process Child { get; private set; }
  internal void Start(string executable,string arguments) {
   if(!AllocConsole()) throw new Win32Exception(Marshal.GetLastWin32Error()); allocated=true;
   ShowWindow(GetConsoleWindow(),0);
   if(!SetConsoleCtrlHandler(KeepHostAlive,true)) throw new Win32Exception(Marshal.GetLastWin32Error());
   output=CreateFile("CONOUT$",0xC0000000,3,IntPtr.Zero,3,0,IntPtr.Zero);
   if(output==new IntPtr(-1)) throw new Win32Exception(Marshal.GetLastWin32Error());
   if(!SetConsoleScreenBufferSize(output,new Coord(160,500))) throw new Win32Exception(Marshal.GetLastWin32Error());
   job=CreateJobObject(IntPtr.Zero,null); if(job==IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
   var limits=new ExtendedLimits(); limits.Basic.Flags=0x2000;
   if(!SetInformationJobObject(job,9,ref limits,(uint)Marshal.SizeOf(limits))) throw new Win32Exception(Marshal.GetLastWin32Error());
   Child=Process.Start(new ProcessStartInfo(executable,arguments) { UseShellExecute=false,CreateNoWindow=false,WorkingDirectory=Path.GetDirectoryName(executable),WindowStyle=ProcessWindowStyle.Hidden });
   if(!AssignProcessToJobObject(job,Child.Handle)) { int error=Marshal.GetLastWin32Error(); if(!Child.HasExited) Child.Kill(); throw new Win32Exception(error); }
  }
  internal string Read() {
   BufferInfo info; if(output==IntPtr.Zero || !GetConsoleScreenBufferInfo(output,out info)) return "";
   int width=info.Size.X,rows=Math.Min(info.Size.Y,info.Cursor.Y+1); uint read;
   var cells=new StringBuilder(width*rows);
   if(!ReadConsoleOutputCharacter(output,cells,(uint)(width*rows),new Coord(0,0),out read)) throw new Win32Exception(Marshal.GetLastWin32Error());
   string text=cells.ToString(); var result=new StringBuilder();
   for(int start=0;start<text.Length;start+=width) result.AppendLine(text.Substring(start,Math.Min(width,text.Length-start)).TrimEnd(' ','\0'));
   return result.ToString().TrimEnd('\r','\n')+Environment.NewLine;
  }
  internal void Stop() {
   if(Child==null || Child.HasExited) return;
   GenerateConsoleCtrlEvent(0,0);
   if(!Child.WaitForExit(5000)) { Child.Kill(); if(!Child.WaitForExit(5000)) throw new IOException(L.T("GoodbyeDPI motoru durdurulamadı.")); }
  }
  public void Dispose() {
   if(disposed) return; disposed=true;
   try { Stop(); } finally {
    if(job!=IntPtr.Zero) CloseHandle(job); if(Child!=null) Child.Dispose();
    if(output!=IntPtr.Zero && output!=new IntPtr(-1)) CloseHandle(output);
    // FreeConsole removes the handler; do not unregister while Ctrl+C is in flight.
    if(allocated) FreeConsole();
   }
  }
 }
 static class EngineConsole {
  internal static string ErrorPath { get { return Path.Combine(Program.InstallDir,"engine-request-error.log"); } }
  internal static void Reject(int profile,Exception error) {
   if(File.Exists(ErrorPath) && (File.GetAttributes(ErrorPath)&FileAttributes.ReparsePoint)!=0) throw new IOException("Invalid request log");
   File.WriteAllText(ErrorPath,DateTime.UtcNow.Ticks+"|"+profile+Environment.NewLine+error.Message);
  }
  internal static string RequestError(int profile,DateTime since) {
   try { string text=File.ReadAllText(ErrorPath); int end=text.IndexOf('\n'); if(end<0) return null; var parts=text.Substring(0,end).Trim().Split('|'); long ticks; int id; if(parts.Length==2 && Int64.TryParse(parts[0],out ticks) && Int32.TryParse(parts[1],out id) && id==profile && ticks>=since.Ticks) return text.Substring(end+1); } catch(IOException) {} catch(UnauthorizedAccessException) {} return null;
  }
  internal static bool IsRunning(int expectedProfile) {
   try {
    int found; string state,output;
    return EngineConsoleForm.TryParse(File.ReadAllText(PathName),out found,out state,out output) && found==expectedProfile && state=="running";
   } catch(IOException) { return false; } catch(UnauthorizedAccessException) { return false; }
  }
  internal static DateTime? PauseUntil() {
   try {
    int profile; string state,output;
    if(!EngineConsoleForm.TryParse(File.ReadAllText(PathName),out profile,out state,out output) || state!="paused") return null;
    long ticks; if(!Int64.TryParse(output.Trim(),out ticks) || ticks<DateTime.MinValue.Ticks || ticks>DateTime.MaxValue.Ticks) return null;
    return new DateTime(ticks,DateTimeKind.Utc);
   } catch(IOException) { return null; } catch(UnauthorizedAccessException) { return null; }
  }
  internal static string PathName { get { return Path.Combine(Program.InstallDir,"engine-console.log"); } }
  internal static string Encode(int profile,string state,int pid,string output) { return "GDC1|"+profile+"|"+state+"|"+pid+Environment.NewLine+output; }
  internal static void Write(string text) {
   if(File.Exists(PathName) && (File.GetAttributes(PathName)&FileAttributes.ReparsePoint)!=0) throw new IOException(L.T("Konsol dosyası bağlantı noktası olamaz."));
   using(var stream=new FileStream(PathName,FileMode.Create,FileAccess.Write,FileShare.Read)) using(var writer=new StreamWriter(stream,new UTF8Encoding(false))) writer.Write(text);
  }
 }
 // Requests a signed-in user may send without admin rights (the installer grants start, stop and user-defined control).
 // The service applies them itself, so users never get the right to change the service configuration.
 static class EngineRequest {
  internal const int ProfileBase=128, AutoOn=150, AutoOff=151, Pause5=160, Pause15=161;
  internal const string SetProfile="--set-profile", SetStart="--set-start";
  // Applies a configuration request as the service. Only these fixed changes are possible.
  internal static void Apply(string name,string value) {
   int profile;
   if(name==SetProfile && Int32.TryParse(value,out profile) && profile>=0 && profile<Program.Args.Length) Program.Configure(profile);
   else if(name==SetStart && (value=="auto" || value=="demand")) Program.Sc("config GoodbyeDPI start= "+value);
   else throw new ArgumentException(L.T("Bilinmeyen hizmet isteği."));
  }
 }
 // Runs start, stop, profile and startup requests as the signed-in user, without a UAC prompt.
 static class UserServiceControl {
  static readonly TimeSpan Wait=TimeSpan.FromSeconds(20);
  static readonly object operation=new object();
  // False when Windows refuses (a service installed before users were given these rights); the caller then asks for admin rights.
  internal static bool TryRun(string action,int profile) {
   try { lock(operation) Run(action,profile); return true; }
   catch(InvalidOperationException ex) { var native=ex.InnerException as Win32Exception; if(native!=null && native.NativeErrorCode==5) return false; throw; }
  }
  static void Run(string action,int profile) {
   using(var service=new ServiceController(Program.ServiceName)) {
    WaitUntilStable(service);
    if(action=="start") { if(service.Status==ServiceControllerStatus.Running && EngineConsole.PauseUntil()!=null) StopAndWait(service); Program.Start(); }
    else if(action=="stop") StopAndWait(service);
    else if(action=="pause5" || action=="pause15") {
     if(service.Status!=ServiceControllerStatus.Running) throw new InvalidOperationException(L.T("Mola vermek için önce GoodbyeDPI'ı başlat."));
     service.ExecuteCommand(action=="pause5"?EngineRequest.Pause5:EngineRequest.Pause15);
     var deadline=DateTime.UtcNow+Wait;
     while(EngineConsole.PauseUntil()==null && DateTime.UtcNow<deadline) Thread.Sleep(200);
     if(EngineConsole.PauseUntil()==null) throw new InvalidOperationException(L.T("Mola başlayamadı. Kurulum dosyasıyla uygulamayı güncellemeyi dene."));
    }
    else if(action=="restart") { StopAndWait(service); Program.Start(); }
    else if(action=="profile") {
     if(profile<0 || profile>=Program.Args.Length) throw new ArgumentOutOfRangeException("profile");
     bool wasRunning=service.Status==ServiceControllerStatus.Running;
     if(Program.Profile()==profile && (!wasRunning || EngineConsole.IsRunning(profile))) return;
     Request(service,EngineRequest.ProfileBase+profile,new[]{EngineRequest.SetProfile,profile.ToString()},delegate { return Program.Profile()==profile && (!wasRunning || EngineConsole.IsRunning(profile)); });
    } else if(action=="auto-on" || action=="auto-off") {
     bool auto=action=="auto-on";
     Request(service,auto?EngineRequest.AutoOn:EngineRequest.AutoOff,new[]{EngineRequest.SetStart,auto?"auto":"demand"},delegate { return StartsWithWindows()==auto; });
    } else throw new ArgumentException(L.T("Bilinmeyen işlem."));
   }
  }
  static void StopAndWait(ServiceController service) {
   WaitUntilStable(service);
   if(service.Status==ServiceControllerStatus.Stopped) return;
   if(service.Status!=ServiceControllerStatus.StopPending) service.Stop();
   service.WaitForStatus(ServiceControllerStatus.Stopped,Wait);
  }
  internal static ServiceControllerStatus StableTarget(ServiceControllerStatus state) {
   return state==ServiceControllerStatus.StartPending?ServiceControllerStatus.Running:state==ServiceControllerStatus.StopPending?ServiceControllerStatus.Stopped:state;
  }
  static void WaitUntilStable(ServiceController service) {
   service.Refresh(); var target=StableTarget(service.Status);
   if(target!=service.Status) service.WaitForStatus(target,Wait);
   service.Refresh();
  }
  // A running service applies the request itself; a stopped one is started only to apply it and stops again.
  static void Request(ServiceController service,int command,string[] startArguments,Func<bool> applied) {
   WaitUntilStable(service);
   DateTime requested=DateTime.UtcNow;
   bool running=service.Status==ServiceControllerStatus.Running;
   if(running) service.ExecuteCommand(command);
   else { Program.WaitForServiceHostExit(); service.Start(startArguments); }
   var deadline=DateTime.UtcNow+Wait;
   while(!applied() && DateTime.UtcNow<deadline) {
    if(startArguments[0]==EngineRequest.SetProfile) { string error=EngineConsole.RequestError(Int32.Parse(startArguments[1]),requested); if(error!=null) throw new InvalidOperationException(error); }
    service.Refresh();
    if(running && service.Status==ServiceControllerStatus.Stopped) throw new InvalidOperationException(L.T("Profil geçişinde motor durdu. Yeniden başlatıp tekrar dene."));
    Thread.Sleep(200);
   }
   if(!applied()) throw new InvalidOperationException(L.T("GoodbyeDPI hizmeti isteği uygulamadı. Kurulum dosyasıyla güncellemeyi dene."));
   service.WaitForStatus(running?ServiceControllerStatus.Running:ServiceControllerStatus.Stopped,Wait);
  }
  static bool StartsWithWindows() { using(var key=Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\GoodbyeDPI")) return key!=null && Convert.ToInt32(key.GetValue("Start",3))==2; }
 }
 sealed class EngineService : ServiceBase {
  int profile; readonly object sync=new object();
  NativeConsoleSession session; System.Threading.Timer capture;
  string last=""; bool stopping; string output=""; DateTime? pauseUntil;
  internal EngineService(int selected) { profile=selected; ServiceName=Program.ServiceName; CanStop=true; CanShutdown=true; AutoLog=false; }
  protected override void OnStart(string[] arguments) {
   if(profile<0 || profile>=Program.Args.Length) throw new ArgumentOutOfRangeException("profile");
   if(!String.Equals(Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory).TrimEnd('\\'),Program.InstallDir,StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException(L.T("Hizmet kurulum klasöründen çalışmalıdır."));
   // A start with a request only changes the configuration while GoodbyeDPI stays off, then the service stops again.
   if(arguments!=null && arguments.Length==2) {
    EngineRequest.Apply(arguments[0],arguments[1]); stopping=true;
    new Thread(delegate() { Thread.Sleep(200); Stop(); }) { IsBackground=true }.Start();
    return;
   }
   StartEngine();
  }
  void StartEngine() {
   try {
    Program.ValidateEngine(profile); Program.CheckOtherEngines();
    last=""; output="";
    EngineConsole.Write(EngineConsole.Encode(profile,"starting",0,""));
    session=new NativeConsoleSession(); session.Start(Program.EngineFor(profile),Program.Args[profile]);
    if(session.Child.WaitForExit(profile>=Program.ZapretStart?2000:800)) throw new InvalidOperationException(L.T("GoodbyeDPI başlatılamadı: ").Replace("GoodbyeDPI",Program.MotorName(profile))+session.Read());
    NativeConsoleSession.FlushDns();
    Capture(); capture=new System.Threading.Timer(delegate { Tick(); },null,500,500);
   } catch { if(session!=null) session.Dispose(); session=null; throw; }
  }
  protected override void OnCustomCommand(int command) {
   try {
    if(command==EngineRequest.Pause5 || command==EngineRequest.Pause15) {
     lock(sync) {
      if(stopping) return;
      if(session!=null) { session.Dispose(); session=null; }
      pauseUntil=DateTime.UtcNow.AddMinutes(command==EngineRequest.Pause5?5:15);
      EngineConsole.Write(EngineConsole.Encode(profile,"paused",0,pauseUntil.Value.Ticks.ToString()));
      NativeConsoleSession.FlushDns();
     }
     return;
    }
    if(command==EngineRequest.AutoOn || command==EngineRequest.AutoOff) { EngineRequest.Apply(EngineRequest.SetStart,command==EngineRequest.AutoOn?"auto":"demand"); return; }
    int next=command-EngineRequest.ProfileBase; if(next<0 || next>=Program.Args.Length) return;
    lock(sync) {
     if(stopping) return;
     int previous=profile; DateTime? previousPause=pauseUntil;
     try { Program.ValidateEngine(next); }
     catch(Exception ex) { EngineConsole.Reject(next,ex); return; }
     EngineConsole.Write(EngineConsole.Encode(next,"switching",0,""));
     // Switch the engine in place: stop the old one, start it again with the new arguments.
     if(capture!=null) capture.Dispose(); capture=null;
     try {
      if(session!=null) { session.Dispose(); session=null; }
      EngineRequest.Apply(EngineRequest.SetProfile,next.ToString());
      profile=next; last=""; pauseUntil=null; StartEngine();
     } catch(Exception ex) {
      AppInfo.Log("Profil değiştirilemedi: "+ex);
      try {
       if(session!=null) { session.Dispose(); session=null; }
       profile=previous; EngineRequest.Apply(EngineRequest.SetProfile,previous.ToString()); pauseUntil=previousPause;
       if(previousPause.HasValue) { EngineConsole.Write(EngineConsole.Encode(profile,"paused",0,previousPause.Value.Ticks.ToString())); capture=new System.Threading.Timer(delegate { Tick(); },null,500,500); }
       else StartEngine();
       EngineConsole.Reject(next,new InvalidOperationException(L.T("Yeni motor başlatılamadı; önceki ayarlarına döndük.")+" "+ex.Message));
      } catch(Exception rollback) { EngineConsole.Reject(next,new AggregateException(ex,rollback)); ExitCode=1; new Thread(delegate() { Stop(); }) { IsBackground=true }.Start(); }
     }
    }
   } catch(Exception ex) { AppInfo.Log("Hizmet isteği: "+ex); }
  }
  void Capture() {
   string current=session.Read(); if(!String.IsNullOrWhiteSpace(current)) output=current;
   string frame=EngineConsole.Encode(profile,session.Child.HasExited?"exited":"running",session.Child.Id,output);
   if(frame!=last) { EngineConsole.Write(frame); last=frame; }
  }
  void Tick() {
   bool exited=false;
   lock(sync) {
    if(stopping) return;
    try {
     if(pauseUntil.HasValue) {
      if(DateTime.UtcNow<pauseUntil.Value) return;
      pauseUntil=null; if(capture!=null) capture.Dispose(); capture=null;
      StartEngine(); return;
     }
     Capture(); exited=session.Child.HasExited;
    } catch(Exception ex) { AppInfo.Log("Motor konsolu: "+ex); if(session==null) exited=true; }
   }
   if(exited) { ExitCode=1; Stop(); }
  }
  protected override void OnStop() {
   lock(sync) {
    if(stopping) return; stopping=true; if(capture!=null) capture.Dispose();
    try { if(session!=null) { session.Stop(); Capture(); } pauseUntil=null; EngineConsole.Write(EngineConsole.Encode(profile,"stopped",0,output)); }
    finally { if(session!=null) session.Dispose(); }
   }
  }
  protected override void OnShutdown() { OnStop(); }
 }
}
