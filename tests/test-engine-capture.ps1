$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$stage=Join-Path $root 'build\console-test'
New-Item -ItemType Directory -Path $stage -Force|Out-Null
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$fixture=@"
using System;
using System.Threading;
class Fixture { static void Main() { Console.WriteLine("CAPTURE_START_731"); Thread.Sleep(2000); Console.Error.WriteLine("CAPTURE_ERROR_731"); Console.WriteLine("CAPTURE_END_731"); } }
"@
$harness=@"
using System;
using System.IO;
using System.Reflection;
using System.Threading;
class Harness {
 [STAThread] static int Main(string[] args) {
  object session=null;
  try {
   var assembly=Assembly.LoadFile(args[0]); var type=assembly.GetType("GoodbyeTray.NativeConsoleSession"); var flags=BindingFlags.NonPublic|BindingFlags.Instance;
   session=Activator.CreateInstance(type,true); type.GetMethod("Start",flags).Invoke(session,new object[]{args[1],""});
   string output="";
   for(int i=0;i<40;i++) { Thread.Sleep(100); output=(string)type.GetMethod("Read",flags).Invoke(session,null); if(output.Contains("CAPTURE_END_731")) break; }
   File.WriteAllText(args[2],output);
   if(!output.Contains("CAPTURE_START_731") || !output.Contains("CAPTURE_ERROR_731") || !output.Contains("CAPTURE_END_731")) throw new Exception("Missing real stdout/stderr");
   ((IDisposable)session).Dispose(); session=null; return 0;
  } catch(Exception ex) { File.WriteAllText(args[2],ex.ToString()); return 1; }
  finally { if(session!=null) ((IDisposable)session).Dispose(); }
 }
}
"@
$fixtureSource=Join-Path $stage 'Fixture.cs'; $harnessSource=Join-Path $stage 'Harness.cs'
[IO.File]::WriteAllText($fixtureSource,$fixture); [IO.File]::WriteAllText($harnessSource,$harness)
$fixtureExe=Join-Path $stage 'Fixture.exe'; $harnessExe=Join-Path $stage 'Harness.exe'
& $compiler /nologo /target:exe "/out:$fixtureExe" $fixtureSource
if($LASTEXITCODE -ne 0){throw 'Fixture build failed'}
& $compiler /nologo /target:winexe "/out:$harnessExe" $harnessSource
if($LASTEXITCODE -ne 0){throw 'Harness build failed'}
$outputPath=Join-Path $stage 'output.txt'; $application=Join-Path $root 'build\app\GoodbyeDPI.Tray.exe'
$process=Start-Process $harnessExe -ArgumentList ('"'+$application+'" "'+$fixtureExe+'" "'+$outputPath+'"') -WindowStyle Hidden -PassThru
if(-not $process.WaitForExit(20000)){throw 'Capture timed out'}
if($process.ExitCode -ne 0){throw ([IO.File]::ReadAllText($outputPath))}
Write-Output 'PASS: Real console stdout and stderr captured from a child process; no GoodbyeDPI engine started.'