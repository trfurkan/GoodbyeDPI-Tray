param([string]$RestoreSnapshot)
$ErrorActionPreference = 'Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$setup = Join-Path $root 'dist\GoodbyeDPI-Installer.exe'
$installDir = Join-Path $env:ProgramFiles 'GoodbyeDPI Tray'
$app = Join-Path $installDir 'GoodbyeDPI.Tray.exe'
$serviceKey = 'HKLM:\SYSTEM\CurrentControlSet\Services\GoodbyeDPI'
$uninstallKey = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\GoodbyeDPITray'
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$results = [Collections.Generic.List[string]]::new()
$snapshot = Join-Path $root ('build\lifecycle-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $snapshot -Force | Out-Null
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Gerçek kurulum testi yönetici yetkisi gerektirir.' }
$original = Get-ItemProperty -LiteralPath $serviceKey -ErrorAction SilentlyContinue
$originalState = if ($original) { (Get-Service GoodbyeDPI).Status.ToString() } else { 'Absent' }
$originalRun = (Get-ItemProperty -LiteralPath $runKey -ErrorAction SilentlyContinue).GoodbyeDPITray
if ($original -and -not $original.ImagePath.StartsWith(('"' + $installDir + '\'),[StringComparison]::OrdinalIgnoreCase)) { throw 'Başka uygulamanın hizmetine dokunulmaz.' }
if (Test-Path -LiteralPath $installDir) { Copy-Item -LiteralPath $installDir -Destination (Join-Path $snapshot 'installed-backup') -Recurse }
[pscustomobject]@{ Service=$original; State=$originalState; Startup=$originalRun } | Export-Clixml -LiteralPath (Join-Path $snapshot 'original-state.xml')
function Run-Checked([string]$exe,[string]$arguments) {
 $p=Start-Process -FilePath $exe -ArgumentList $arguments -WorkingDirectory (Split-Path $exe -Parent) -WindowStyle Hidden -PassThru
 if (-not $p.WaitForExit(90000)) { throw "İşlem zaman aşımı: $arguments" }
 if ($p.ExitCode -ne 0) { throw "İşlem başarısız: $arguments (kod $($p.ExitCode)); uygulama günlüğüne bakın." }
}
function Check([bool]$condition,[string]$message) { if(-not $condition) { throw $message }; $results.Add('PASS: '+$message) }
function Remove-Installed {
 Run-Checked $setup '--remove-elevated --quiet'
 Run-Checked $setup '--user-remove'
 Check (-not (Test-Path -LiteralPath $serviceKey)) 'Hizmet kaydı kaldırıldı'
 Check (-not (Test-Path -LiteralPath $uninstallKey)) 'Windows kaldırma kaydı kaldırıldı'
 Check (-not (Test-Path -LiteralPath $app)) 'Uygulama EXE dosyası silindi'
 Check (-not (Test-Path -LiteralPath (Join-Path $installDir 'GoodbyeDPI-Installer.exe'))) 'Kurucu/kaldırıcı dosyası silindi'
 $run=(Get-ItemProperty -LiteralPath $runKey -ErrorAction SilentlyContinue).GoodbyeDPITray
 Check ($null -eq $run) 'Kullanıcı başlangıç girdisi kaldırıldı'
}
if ($RestoreSnapshot) { $saved=Import-Clixml -LiteralPath (Join-Path $RestoreSnapshot 'original-state.xml'); $original=$saved.Service; $originalState=$saved.State; $originalRun=$saved.Startup }
$sentinel = Join-Path $installDir ('.lifecycle-personal-' + [Guid]::NewGuid().ToString('N') + '.txt')
try {
 Run-Checked $setup '--install-elevated --quiet'
 Run-Checked $setup $(if($original){'--user-update'}else{'--user-install'})
 Check ((Get-ItemProperty -LiteralPath $uninstallKey).Publisher -eq 'Furkan') 'Windows yayıncı metadatası Furkan'
  $testAssembly=[Reflection.Assembly]::LoadFile((Join-Path $root 'build\app\GoodbyeDPI.Tray.exe'))
 $testProgram=$testAssembly.GetType('GoodbyeTray.Program'); $staticFlags=[Reflection.BindingFlags]'NonPublic,Static'
 if($original){
  $oldIndex=-1
  for($i=0;$i -lt 7;$i++){foreach($method in @('Binary','LegacyBinary')){if($testProgram.GetMethod($method,$staticFlags).Invoke($null,[object[]]@($i)) -eq $original.ImagePath){$oldIndex=$i}}}
  Check ($testProgram.GetMethod('Profile',$staticFlags).Invoke($null,$null) -eq $oldIndex) 'Guncellemede mevcut profil korundu'
 }
 Run-Checked $app '--admin stop --quiet'
 Run-Checked $app '--admin start --quiet'
 Check ((Get-Service GoodbyeDPI).Status -eq 'Running') 'Hizmet başlatma'
 Run-Checked $app '--admin restart --quiet'
 Check (@(Get-Process goodbyedpi -ErrorAction SilentlyContinue).Count -eq 1) 'Yeniden başlatma sonrası tek motor'
 Run-Checked $app '--admin profile 4 --quiet'
 Check ($testProgram.GetMethod('Profile',$staticFlags).Invoke($null,$null) -eq 4) 'Profil 4 uygulandı'
 $profileBeforeUpdate=(Get-ItemProperty -LiteralPath $serviceKey).ImagePath
 Run-Checked $app '--admin auto-off --quiet'
 Run-Checked $setup '--install-elevated --quiet'
 Check ((Get-ItemProperty -LiteralPath $serviceKey).ImagePath -eq $profileBeforeUpdate) 'İkinci güncellemede profil korundu'
 Check ((Get-ItemProperty -LiteralPath $serviceKey).Start -eq 3) 'Güncellemede elle başlangıç tercihi korundu'
 [IO.File]::WriteAllText($sentinel,'Kişisel dosya korunma testi')
 Remove-Installed
 Check (Test-Path -LiteralPath $sentinel) 'Kaldırmada tanınmayan kişisel dosya korundu'
 Run-Checked $setup '--install-elevated --quiet'
 Run-Checked $setup '--user-install'
 Check ((Get-Service GoodbyeDPI).Status -eq 'Running') 'Temiz hizmet kurulumu çalışıyor'
 Remove-Installed
 Run-Checked $setup '--install-elevated --quiet'
 Run-Checked $setup '--user-install'
 Check (Test-Path -LiteralPath $app) 'Kaldırma sonrası yeniden kurulum'
 # Leave the improved application installed, with the original service settings.
 if ($original) {
  Run-Checked $app '--admin stop --quiet'
  $installedAssembly=$testAssembly
  $programType=$installedAssembly.GetType('GoodbyeTray.Program')
  $binaryMethod=$programType.GetMethod('Binary',[Reflection.BindingFlags]'NonPublic,Static')
  $oldProfile=-1
  for($profileIndex=0;$profileIndex -lt 7;$profileIndex++) { if($binaryMethod.Invoke($null,[object[]]@($profileIndex)) -eq $original.ImagePath -or $programType.GetMethod('LegacyBinary',$staticFlags).Invoke($null,[object[]]@($profileIndex)) -eq $original.ImagePath) { $oldProfile=$profileIndex } }
  if($oldProfile -lt 0) { throw 'Yedekteki profil tanınmıyor' }
  Run-Checked $app ("--admin profile $oldProfile --quiet")
  & sc.exe config GoodbyeDPI start= $(if($original.Start -eq 2){'auto'}elseif($original.Start -eq 4){'disabled'}else{'demand'}) | Out-Null
  if ($LASTEXITCODE -ne 0) { throw 'Eski başlangıç tercihi geri yüklenemedi' }
  if($originalState -eq 'Running') { Run-Checked $app '--admin start --quiet' }
 }
 if ($null -eq $originalRun) { Remove-ItemProperty -LiteralPath $runKey -Name GoodbyeDPITray -ErrorAction SilentlyContinue } else { Set-ItemProperty -LiteralPath $runKey -Name GoodbyeDPITray -Value $originalRun }
 Check ($true) 'Önceki hizmet ve kullanıcı başlangıç tercihleri geri yüklendi'
} catch {
 $results.Add('FAIL: '+$_.Exception.Message)
 $results.Add('Yedek: '+$snapshot)
 throw
} finally {
 if ([IO.Path]::GetFullPath($sentinel).StartsWith([IO.Path]::GetFullPath($installDir)+'\',[StringComparison]::OrdinalIgnoreCase) -and (Test-Path -LiteralPath $sentinel)) { Remove-Item -LiteralPath $sentinel }
 $results | Set-Content -LiteralPath (Join-Path $snapshot 'results.txt') -Encoding UTF8
 $results | Write-Output
}
