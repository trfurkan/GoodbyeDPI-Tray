$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$resultsPath=Join-Path $root 'build\engine-service-test.txt'
Start-Transcript -LiteralPath $resultsPath -Force
if(-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)){throw 'Administrator required'}
$setup=Join-Path $root 'dist\GoodbyeDPI-Installer.exe'
$install=Join-Path $env:ProgramFiles 'GoodbyeDPI Tray'
$app=Join-Path $install 'GoodbyeDPI.Tray.exe'
$serviceKey='HKLM:\SYSTEM\CurrentControlSet\Services\GoodbyeDPI'
$original=Get-ItemProperty -LiteralPath $serviceKey -ErrorAction SilentlyContinue
$originalState=if($original){(Get-Service GoodbyeDPI).Status.ToString()}else{'Absent'}
if($original -and -not $original.ImagePath.StartsWith(('"'+$install+'\'),[StringComparison]::OrdinalIgnoreCase)){throw 'Foreign service'}
$backup=Join-Path $root ('build\engine-backup-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $backup -Force|Out-Null
if(Test-Path $install){Copy-Item -LiteralPath $install -Destination (Join-Path $backup 'installed') -Recurse}
[pscustomobject]@{Service=$original;State=$originalState}|Export-Clixml (Join-Path $backup 'state.xml')
$asm=[Reflection.Assembly]::LoadFile((Join-Path $root 'build\app\GoodbyeDPI.Tray.exe'))
$program=$asm.GetType('GoodbyeTray.Program'); $flags=[Reflection.BindingFlags]'NonPublic,Static'
$oldProfile=0
if($original){
 $oldProfile=-1
 for($i=0;$i -lt 7;$i++){foreach($method in @('Binary','LegacyBinary')){if($program.GetMethod($method,$flags).Invoke($null,[object[]]@($i)) -eq $original.ImagePath){$oldProfile=$i}}}
 if($oldProfile -lt 0){throw 'Unknown original profile'}
}
function Run-Checked([string]$exe,[string]$arguments){$p=Start-Process $exe -ArgumentList $arguments -WindowStyle Hidden -PassThru; if(-not $p.WaitForExit(60000)){throw "Timeout: $arguments"}; if($p.ExitCode -ne 0){throw "Failed: $arguments (exit $($p.ExitCode))"}}
function Check([bool]$value,[string]$message){if(-not $value){throw $message}; Write-Output ('PASS: '+$message)}
$installed=$false
try {
 Run-Checked $setup '--install-elevated --quiet'; $installed=$true
 Check ($program.GetMethod('Profile',$flags).Invoke($null,$null) -eq $oldProfile) 'Upgrade preserved profile'
 if($original){Check ((Get-ItemProperty $serviceKey).Start -eq $original.Start) 'Upgrade preserved startup preference'}
 Run-Checked $app '--admin start --quiet'
 for($profile=0;$profile -lt 7;$profile++){
  Run-Checked $app ("--admin profile $profile --quiet")
  $frame=''
  for($attempt=0;$attempt -lt 40;$attempt++){
   try{$frame=[IO.File]::ReadAllText((Join-Path $install 'engine-console.txt'))}catch [IO.IOException]{}
   if($frame.StartsWith("GDC1|$profile|running|") -and $frame.Contains('Filter activated')){break}
   Start-Sleep -Milliseconds 250
  }
  Check ($frame.StartsWith("GDC1|$profile|running|") -and $frame.Contains('Filter activated')) "Profile $profile real console output"
  $engines=@(Get-CimInstance Win32_Process -Filter "Name='goodbyedpi.exe'")
  Check ($engines.Count -eq 1) "Profile $profile single engine"
  $expected=$program.GetField('Args',$flags).GetValue($null)[$profile]
  Check ($engines[0].CommandLine.EndsWith($expected)) "Profile $profile unchanged parameters"
  [IO.File]::WriteAllText((Join-Path $backup "profile-$profile.txt"),$frame)
 }
 Run-Checked $app '--admin stop --quiet'
 Check (@(Get-Process goodbyedpi -ErrorAction SilentlyContinue).Count -eq 0) 'Stop removed child engine'
 Check ([IO.File]::ReadAllText((Join-Path $install 'engine-console.txt')).Contains('|stopped|')) 'Stopped console state recorded'
 Run-Checked $app '--admin start --quiet'
 $before=(Get-Process goodbyedpi).Id
 Add-Type -AssemblyName System.Windows.Forms
 $console=[Activator]::CreateInstance($asm.GetType('GoodbyeTray.EngineConsoleForm'),[Reflection.BindingFlags]'NonPublic,Instance',$null,[object[]]@($true),$null)
 try{
  $console.Show(); for($i=0;$i -lt 30;$i++){[Windows.Forms.Application]::DoEvents(); Start-Sleep -Milliseconds 50}
  Check ((Get-Process goodbyedpi).Id -eq $before) 'Opening console does not start a second engine'
  $console.Close(); [Windows.Forms.Application]::DoEvents()
  Check ((Get-Process goodbyedpi).Id -eq $before) 'Closing console leaves engine running'
 }finally{$console.Dispose()}
 Run-Checked $setup '--install-elevated --quiet'
 Check ((Get-Service GoodbyeDPI).Status -eq 'Running') 'Upgrade over capture service restarts engine'
 Write-Output 'PASS: Engine capture service integration'
} finally {
 if($installed -and $original){
  try{
   Run-Checked $app ("--admin profile $oldProfile --quiet")
   if($originalState -ne 'Running'){Run-Checked $app '--admin stop --quiet'}
   Check ($program.GetMethod('Profile',$flags).Invoke($null,$null) -eq $oldProfile) 'Original profile restored'
   Check ((Get-Service GoodbyeDPI).Status.ToString() -eq $originalState) 'Original service state restored'
  }catch{
   Write-Output ('RESTORE ISSUE: '+$_)
   # Keep a working original engine available if the new host cannot start.
   $command='config GoodbyeDPI binPath= "'+$original.ImagePath.Replace('"','\"')+'"'
   try{$program.GetMethod('Stop',$flags).Invoke($null,$null)|Out-Null}catch{}
   $program.GetMethod('Sc',$flags).Invoke($null,[object[]]@($command))|Out-Null
   if($originalState -eq 'Running'){$program.GetMethod('Start',$flags).Invoke($null,$null)|Out-Null}
   Write-Output 'Original service command restored after test failure'
  }
 }
 Write-Output ('Backup: '+$backup)
 Stop-Transcript
}