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
  [DllImport("dnsapi.dll")] static extern bool DnsFlushResolverCache();
  // Answers cached before the engine started still point at the block page; drop them so redirected DNS takes effect at once.
  internal static void FlushDns() { try { DnsFlushResolverCache(); } catch(Exception ex) { AppInfo.Log("DNS önbelleği: "+ex.Message); } }
  IntPtr output,job; bool allocated,disposed;
  internal Process Child { get; private set; }
  ConsoleCancelEventHandler cancelHandler;
  internal void Start(string executable,string arguments) {
   if(!AllocConsole()) throw new Win32Exception(Marshal.GetLastWin32Error()); allocated=true;
   ShowWindow(GetConsoleWindow(),0);
   cancelHandler=delegate(object sender,ConsoleCancelEventArgs e) { e.Cancel=true; }; Console.CancelKeyPress+=cancelHandler;
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
   if(!Child.WaitForExit(5000)) { Child.Kill(); if(!Child.WaitForExit(5000)) throw new IOException("GoodbyeDPI motoru durdurulamadı."); }
  }
  public void Dispose() {
   if(disposed) return; disposed=true;
   try { Stop(); } finally {
    if(job!=IntPtr.Zero) CloseHandle(job); if(Child!=null) Child.Dispose();
    if(output!=IntPtr.Zero && output!=new IntPtr(-1)) CloseHandle(output);
    if(cancelHandler!=null) Console.CancelKeyPress-=cancelHandler;
    if(allocated) FreeConsole();
   }
  }
 }
 static class EngineConsole {
  internal static string PathName { get { return Path.Combine(Program.InstallDir,"engine-console.txt"); } }
  internal static string Encode(int profile,string state,int pid,string output) { return "GDC1|"+profile+"|"+state+"|"+pid+Environment.NewLine+output; }
  internal static void Write(string text) {
   if(File.Exists(PathName) && (File.GetAttributes(PathName)&FileAttributes.ReparsePoint)!=0) throw new IOException("Konsol dosyası bağlantı noktası olamaz.");
   using(var stream=new FileStream(PathName,FileMode.Create,FileAccess.Write,FileShare.Read)) using(var writer=new StreamWriter(stream,new UTF8Encoding(false))) writer.Write(text);
  }
 }
 sealed class EngineService : ServiceBase {
  readonly int profile; readonly object sync=new object();
  NativeConsoleSession session; System.Threading.Timer capture;
  string last=""; bool stopping; string output="";
  internal EngineService(int selected) { profile=selected; ServiceName=Program.ServiceName; CanStop=true; CanShutdown=true; AutoLog=false; }
  protected override void OnStart(string[] arguments) {
   if(profile<0 || profile>=Program.Args.Length) throw new ArgumentOutOfRangeException("profile");
   if(!String.Equals(Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory).TrimEnd('\\'),Program.InstallDir,StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Hizmet kurulum klasöründen çalışmalıdır.");
   if(Process.GetProcessesByName("goodbyedpi").Length>0) throw new InvalidOperationException("Başka bir GoodbyeDPI motoru çalışıyor.");
   try {
    EngineConsole.Write(EngineConsole.Encode(profile,"starting",0,""));
    session=new NativeConsoleSession(); session.Start(Program.Engine,Program.Args[profile]);
    if(session.Child.WaitForExit(800)) throw new InvalidOperationException("GoodbyeDPI başlatılamadı: "+session.Read());
    NativeConsoleSession.FlushDns();
    Capture(); capture=new System.Threading.Timer(delegate { Tick(); },null,500,500);
   } catch { if(session!=null) session.Dispose(); throw; }
  }
  void Capture() {
   string current=session.Read(); if(!String.IsNullOrWhiteSpace(current)) output=current;
   string frame=EngineConsole.Encode(profile,session.Child.HasExited?"exited":"running",session.Child.Id,output);
   if(frame!=last) { EngineConsole.Write(frame); last=frame; }
  }
  void Tick() {
   bool exited=false;
   lock(sync) { if(stopping) return; try { Capture(); exited=session.Child.HasExited; } catch(Exception ex) { AppInfo.Log("Motor konsolu: "+ex); } }
   if(exited) { ExitCode=1; Stop(); }
  }
  protected override void OnStop() {
   lock(sync) {
    if(stopping) return; stopping=true; if(capture!=null) capture.Dispose();
    try { if(session!=null) { session.Stop(); Capture(); EngineConsole.Write(EngineConsole.Encode(profile,"stopped",0,output)); } }
    finally { if(session!=null) session.Dispose(); }
   }
  }
  protected override void OnShutdown() { OnStop(); }
 }
}
