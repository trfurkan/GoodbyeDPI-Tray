param([int[]]$Profiles=@(7,8,9))
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$reportDir=Join-Path $root 'build/dns-trial'
New-Item -ItemType Directory -Path $reportDir -Force | Out-Null
$install=(Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\GoodbyeDPITray').InstallLocation
if(-not $install) { throw 'Installed app not found' }
$asm=[Reflection.Assembly]::LoadFile((Join-Path $install 'GoodbyeDPI.Tray.exe'))
$flags=[Reflection.BindingFlags]'Static,NonPublic'
$program=$asm.GetType('GoodbyeTray.Program')
$control=$asm.GetType('GoodbyeTray.UserServiceControl').GetMethod('TryRun',$flags)
$console=$asm.GetType('GoodbyeTray.EngineConsole')
$original=[int]$program.GetMethod('Profile',$flags).Invoke($null,$null)
$count=$program.GetField('Args',$flags).GetValue($null).Length
if($original -lt 0) { throw 'Service is not owned by this app' }
if($console.GetMethod('PauseUntil',$flags).Invoke($null,$null)) { throw 'Resume the paused engine before testing' }
foreach($id in $Profiles) { if($id -lt 7 -or $id -ge $count) { throw "Profile $id is not installed" } }
$wasRunning=(Get-Service GoodbyeDPI).Status -eq 'Running'
$snapshot=[ordered]@{ Profile=$original; Running=$wasRunning; Started=(Get-Date).ToString('o'); Dns=@(Get-DnsClientServerAddress | Select-Object InterfaceIndex,AddressFamily,ServerAddresses) }
$snapshot | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $reportDir 'before.json') -Encoding UTF8
$results=New-Object 'Collections.Generic.List[object]'
function Control-Engine([string]$Action,[int]$Id=0) {
 if(-not $control.Invoke($null,[object[]]@($Action,$Id))) { throw 'Service denied user control; no elevation attempted' }
}
function Curl-Request([string[]]$Arguments) {
 $start=New-Object Diagnostics.ProcessStartInfo
 $start.FileName=(Get-Command curl.exe).Source
 $start.Arguments=($Arguments | ForEach-Object { '"'+$_.Replace('"','\"')+'"' }) -join ' '
 $start.UseShellExecute=$false; $start.CreateNoWindow=$true
 $start.RedirectStandardOutput=$true; $start.RedirectStandardError=$true
 $process=[Diagnostics.Process]::Start($start)
 try {
  $stdout=$process.StandardOutput.ReadToEndAsync(); $stderr=$process.StandardError.ReadToEndAsync()
  if(-not $process.WaitForExit(12000)) { $process.Kill(); $process.WaitForExit(); throw 'curl exceeded its deadline' }
  return [pscustomobject]@{ Exit=$process.ExitCode; Output=$stdout.Result.Trim(); Error=$stderr.Result.Trim() }
 } finally { $process.Dispose() }
}
$common=@('--ipv4','--noproxy','*','--max-time','8','--connect-timeout','5','--silent','--show-error')
$domains=@('google.com','discord.com','roblox.com','wattpad.com')
$mutated=$false
try {
 foreach($id in $Profiles) {
  $mutated=$true
  Control-Engine 'profile' $id
  Control-Engine 'start'
  $asm.GetType('GoodbyeTray.NativeConsoleSession').GetMethod('FlushDns',$flags).Invoke($null,$null) | Out-Null
  Write-Output "Testing profile $id (system DNS versus Google DoH, IPv4)"
  foreach($domain in $domains) {
   $normal=Curl-Request ($common+@('--head','--output','NUL','--write-out','%{http_code}',"https://$domain/"))
   # Bootstrap the resolver without trusting the system DNS. TLS verification stays enabled.
   $dns=Curl-Request ($common+@('--resolve','dns.google:443:8.8.8.8',"https://dns.google/resolve?name=$domain&type=A"))
   $ip=$null; $secure=$null
   if($dns.Exit -eq 0) {
    $answer=$dns.Output | ConvertFrom-Json
    if($answer.Status -eq 0) { $ip=@($answer.Answer | Where-Object { $_.type -eq 1 -and $_.data -match '^\d{1,3}(\.\d{1,3}){3}$' } | Select-Object -First 1).data }
   }
   if($ip) { $secure=Curl-Request ($common+@('--resolve',"${domain}:443:$ip",'--head','--output','NUL','--write-out','%{http_code}',"https://$domain/")) }
   $row=[pscustomobject]@{ Profile=$id; Domain=$domain; SystemDns=$normal; DohQuery=$dns.Exit; DohAddress=$ip; SecureDns=$secure }
   $results.Add($row)
   $results | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $reportDir 'results.json') -Encoding UTF8
   Write-Output ("  {0}: system={1}/{2}, DoH={3}, resolved={4}/{5}" -f $domain,$normal.Exit,$normal.Output,$dns.Exit,$secure.Exit,$secure.Output)
  }
 }
} finally {
 if($mutated) {
  $errors=New-Object 'Collections.Generic.List[string]'
  try { Control-Engine 'profile' $original } catch { $errors.Add($_.Exception.Message) }
  try { if($wasRunning) { Control-Engine 'start' } else { Control-Engine 'stop' } } catch { $errors.Add($_.Exception.Message) }
  if($errors.Count) { throw ('RESTORE FAILED: '+($errors -join '; ')) }
  Write-Output "RESTORED: profile=$original, running=$wasRunning. System DNS settings were never changed."
 }
}
