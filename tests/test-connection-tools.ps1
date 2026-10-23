$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
# Compile the actual search and service implementations against fake engine dependencies.
# No Windows service, network request or installed application is changed.
$service=[IO.File]::ReadAllText((Join-Path $root 'src\EngineService.cs'))
$clientStart=$service.IndexOf(' static class UserServiceControl')
$client=$service.Substring($clientStart,$service.IndexOf(' sealed class EngineService : ServiceBase')-$clientStart)
$service=$service.Substring($service.IndexOf(' sealed class EngineService : ServiceBase'))
$search=[IO.File]::ReadAllText((Join-Path $root 'src\ProfileSearch.cs'))
$fakes=@'
using System;
using System.IO;
using System.Threading;
using System.Reflection;
using System.ServiceProcess;
using System.Diagnostics;
using System.ComponentModel;
using System.Collections.Generic;
namespace GoodbyeTray {
 static class Program {
  internal static string[] Args={"a","b","c","d","e","f","g","z1","z2","z3"};
  internal static string ServiceName="TestOnly",Engine="fake";
  internal const int ZapretStart=7;
  internal static string EngineFor(int p) { return p>=7?"zapret":"fake"; }
  internal static string MotorName(int p) { return p>=7?"Zapret 2":"GoodbyeDPI"; }
  internal static void ValidateEngine(int p) {}
  internal static void CheckOtherEngines() {}
  internal static string InstallDir=AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
  internal static int Selected=0;
  internal static int Profile() { return Selected; }
  internal static void Start() { ServiceController.Current=ServiceControllerStatus.Running; }
  internal static void WaitForServiceHostExit() {}
 }
 static class L { internal static string T(string s) { return s; } }
 static class AppInfo { internal static void Log(string s) {} }
 static class EngineRequest {
  internal const int ProfileBase=128, AutoOn=150, AutoOff=151, Pause5=160, Pause15=161;
  internal const string SetStart="start",SetProfile="profile";
  internal static void Apply(string name,string value) { if(name==SetProfile) Program.Selected=Int32.Parse(value); }
 }
 static class EngineConsole {
  internal static string Last,Error;
  internal static void Reject(int p,Exception ex) { Error=ex.Message; }
  internal static string RequestError(int p,DateTime since) { return null; }
  internal static int ReadyAfter,Checks;
  internal static bool IsRunning(int p) { Checks++; return --ReadyAfter<=0 && p==Program.Selected; }
  internal static DateTime? PauseUntil() { return null; }
  internal static string Encode(int p,string state,int pid,string output) { return state+"|"+output; }
  internal static void Write(string s) { Last=s; }
 }
 sealed class ServiceController : IDisposable {
  internal static ServiceControllerStatus Current=ServiceControllerStatus.Running;
  internal static int Requests;
  internal ServiceController(string name) {}
  internal ServiceControllerStatus Status { get { return Current; } }
  internal void Refresh() {}
  internal void WaitForStatus(ServiceControllerStatus desired,TimeSpan timeout) {
   if(Current==ServiceControllerStatus.StartPending && desired!=ServiceControllerStatus.Running) throw new Exception("StartPending incorrectly waits for stop");
   Current=desired;
  }
  internal void ExecuteCommand(int command) { Requests++; Program.Selected=command-EngineRequest.ProfileBase; EngineConsole.ReadyAfter=4; }
  internal void Start(string[] args) { Program.Selected=Int32.Parse(args[1]); Current=ServiceControllerStatus.Stopped; }
  internal void Stop() { Current=ServiceControllerStatus.Stopped; }
  public void Dispose() {}
 }
 sealed class FakeChild { internal bool HasExited; internal int Id=1; internal bool WaitForExit(int ms) { return false; } }
 sealed class NativeConsoleSession : IDisposable {
  internal static int Starts,Stops;
  internal static string FailArgument,LastEngine;
  internal FakeChild Child=new FakeChild();
  internal void Start(string exe,string args) { Starts++; LastEngine=exe; if(args==FailArgument) throw new IOException("simulated engine failure"); }
  internal void Stop() { Child.HasExited=true; }
  public void Dispose() { Stops++; Child.HasExited=true; }
  internal string Read() { return "engine output"; }
  internal static void FlushDns() {}
 }
 public static class ConnectionToolsTests {
  static void Check(bool condition,string message) { if(!condition) throw new Exception(message); }
  public static void Run() {
   ServiceController.Current=ServiceControllerStatus.StartPending; Program.Selected=0;
   UserServiceControl.TryRun("profile",3);
   Check(Program.Selected==3 && EngineConsole.Checks>=4 && EngineConsole.ReadyAfter<=0,"wait for live engine acknowledgement, not only saved profile");
   int requests=ServiceController.Requests;
   UserServiceControl.TryRun("profile",3);
   Check(ServiceController.Requests==requests,"same active profile is a no-op");
   int selected=2; bool running=false; int[] scores={1,3,1,0,2,0,0}; int visits=0;
   var result=ProfileSearch.Run(7,2,false,p=>selected=p,on=>running=on,()=>{visits++;return scores[selected];},(n,p)=>{},CancellationToken.None);
   Check(result.Profile==1 && result.Score==3 && visits==7,"best profile selection");
   Check(result.Scores.Count==7 && result.Scores[1]==3 && result.Scores[3]==0,"retain every profile score for results screen");
   Check(selected==2 && !running,"restore stopped state");
   result=ProfileSearch.Run(7,2,true,p=>selected=p,on=>running=on,()=>2,(n,p)=>{},CancellationToken.None);
   Check(result.Profile==2 && selected==2 && running,"tie keeps original and restores running state");
   result=ProfileSearch.Run(7,2,false,p=>selected=p,on=>running=on,()=>-1,(n,p)=>{},CancellationToken.None);
   Check(result.Profile==-1,"offline control must not recommend");
   using(var cancel=new CancellationTokenSource()) {
    bool cancelled=false;
    try { ProfileSearch.Run(7,2,true,p=>selected=p,on=>running=on,()=>{cancel.Cancel();return 3;},(n,p)=>{},cancel.Token); }
    catch(OperationCanceledException) { cancelled=true; }
    Check(cancelled && selected==2 && running,"cancellation rollback");
   }
   bool failed=false;
   try { ProfileSearch.Run(7,2,false,p=>selected=p,on=>running=on,()=>{throw new IOException("probe failed");},(n,p)=>{},CancellationToken.None); }
   catch(IOException) { failed=true; }
   Check(failed && selected==2 && !running,"failure rollback");
   bool restoring=false,restoredPower=false; AggregateException recovery=null;
   try { ProfileSearch.Run(7,2,false,p=>{if(restoring)throw new IOException("restore failed");},on=>{if(restoring)restoredPower=true;},()=>{throw new IOException("original probe failure");},(n,p)=>{if(n==0)restoring=true;},CancellationToken.None); }
   catch(AggregateException ex) { recovery=ex; }
   Check(recovery!=null && recovery.InnerExceptions.Count==2 && recovery.InnerExceptions[0].Message=="original probe failure" && restoredPower,"preserve original failure and attempt power restoration");
   var visited=new List<int>();
   result=ProfileSearch.Run(10,2,true,p=>selected=p,on=>running=on,()=>{visited.Add(selected);return selected==8?3:1;},(n,p)=>{},CancellationToken.None,null,new[]{7,8,9});
   Check(visited.Count==3 && visited[0]==7 && visited[2]==9 && result.Profile==8 && selected==2 && running,"selected engine only, restore original engine");
   var type=typeof(EngineService); var flags=BindingFlags.Instance|BindingFlags.NonPublic;
   using(var engine=new EngineService(0)) {
    Action<string,object[]> call=(name,args)=>type.GetMethod(name,flags).Invoke(engine,args);
    // Avoid SCM and start the fake engine directly.
    call("StartEngine",null);
    call("OnCustomCommand",new object[]{135});
    Check(Program.Selected==7 && NativeConsoleSession.LastEngine=="zapret","switch from GoodbyeDPI to Zapret");
    NativeConsoleSession.FailArgument="z2";
    call("OnCustomCommand",new object[]{136});
    Check(Program.Selected==7 && (int)type.GetField("profile",flags).GetValue(engine)==7 && EngineConsole.Error!=null && EngineConsole.Last.StartsWith("running|"),"failed switch restores previous running engine");
    NativeConsoleSession.FailArgument=null;
    call("OnCustomCommand",new object[]{128});
    Check(Program.Selected==0 && NativeConsoleSession.LastEngine=="fake","switch back to GoodbyeDPI");
    call("OnCustomCommand",new object[]{160});
    var until=(DateTime?)type.GetField("pauseUntil",flags).GetValue(engine);
    Check(until.HasValue && (until.Value-DateTime.UtcNow).TotalMinutes>4.9,"five minute deadline");
    Check(type.GetField("session",flags).GetValue(engine)==null && EngineConsole.Last.StartsWith("paused|"),"pause stops engine and publishes status");
    int starts=NativeConsoleSession.Starts;
    call("Tick",null); Check(NativeConsoleSession.Starts==starts,"does not resume early");
    type.GetField("pauseUntil",flags).SetValue(engine,DateTime.UtcNow.AddSeconds(-1));
    call("Tick",null); Check(NativeConsoleSession.Starts==starts+1 && EngineConsole.Last.StartsWith("running|"),"deadline resumes engine");
    call("OnCustomCommand",new object[]{161});
    until=(DateTime?)type.GetField("pauseUntil",flags).GetValue(engine);
    Check(until.HasValue && (until.Value-DateTime.UtcNow).TotalMinutes>14.9,"fifteen minute deadline");
    call("OnStop",null); starts=NativeConsoleSession.Starts;
    call("Tick",null);
    Check(NativeConsoleSession.Starts==starts && EngineConsole.Last.StartsWith("stopped|"),"manual stop cancels automatic resume");
   }
  }
 }
}
'@
# The service class includes its namespace closing brace.
$unit=$fakes+"`nnamespace GoodbyeTray {`n"+$client+$service
# Compile together so internal search members remain accessible to the harness.
$searchBody=$search.Substring($search.IndexOf('namespace GoodbyeTray'))
Add-Type -TypeDefinition ($unit+"`n"+$searchBody) -ReferencedAssemblies @('System.dll','System.Core.dll','System.ServiceProcess.dll')
[GoodbyeTray.ConnectionToolsTests]::Run()
$assembly=[Reflection.Assembly]::LoadFile((Join-Path $root 'build\app\GoodbyeDPI.Tray.exe'))
$stable=$assembly.GetType('GoodbyeTray.UserServiceControl').GetMethod('StableTarget',[Reflection.BindingFlags]'NonPublic,Static')
foreach($pair in @(@('StartPending','Running'),@('StopPending','Stopped'),@('Running','Running'),@('Stopped','Stopped'))) {
 $actual=$stable.Invoke($null,[object[]]@([ServiceProcess.ServiceControllerStatus]$pair[0]))
 if($actual.ToString() -ne $pair[1]) { throw "Wrong transition target: $($pair[0]) -> $actual" }
}
Write-Output 'PASS: Profile ranking, ties, offline results, cancellation/failure rollback; 5/15 minute pause, deadline resume and manual stop cancellation. Fake engine only.'
