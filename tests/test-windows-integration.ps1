$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[Windows.Forms.Application]::EnableVisualStyles()
$root=Split-Path $PSScriptRoot -Parent
$assembly=[Reflection.Assembly]::LoadFile((Join-Path $root 'build\app\GoodbyeDPI.Tray.exe'))
$static=[Reflection.BindingFlags]'NonPublic,Static'
$instance=[Reflection.BindingFlags]'NonPublic,Instance'
$windows=$assembly.GetType('GoodbyeTray.WindowsIntegration')
$commands=$assembly.GetType('GoodbyeTray.AppCommands')
$list=$windows.GetMethod('BuildJumpList',$static).Invoke($null,[object[]]@([string](Join-Path $root 'build\app\GoodbyeDPI.Tray.exe')))
if($list.JumpItems.Count -ne 7) { throw 'Jump List görevleri eksik' }
foreach($task in $list.JumpItems) {
 $command=$task.Arguments.Substring(9)
 if(-not $commands.GetMethod('IsValid',$static).Invoke($null,[object[]]@([string]$command))) { throw "İşlemsiz Jump List girdisi: $command" }
}
$version=[Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $root 'dist\GoodbyeDPI-Installer.exe'))
if($version.CompanyName -ne 'Furkan' -or $version.FileVersion -ne '1.5.2.0') { throw 'EXE metadatası yanlış' }
$type=$assembly.GetType('GoodbyeTray.MainForm')
$form=[Activator]::CreateInstance($type,[object[]]@($false))
try {
 $form.Show()
 foreach($mode in @('light','dark','system','light')) {
  $type.GetMethod('SetThemePreference',$instance).Invoke($form,[object[]]@([string]$mode,$false)) | Out-Null
  [Windows.Forms.Application]::DoEvents()
  $dwmArgs=[object[]]@($form.Handle,20,0,4)
  $result=$windows.GetMethod('DwmGetWindowAttribute',$static).Invoke($null,$dwmArgs)
  if($result -eq 0 -and $mode -ne 'system' -and -not [Windows.Forms.SystemInformation]::HighContrast) { if($dwmArgs[2] -ne $(if($mode -eq 'dark'){1}else{0})) { throw 'Başlık çubuğu tema değişimini takip etmedi' } }
  Write-Output "DWM tema $mode : HRESULT=$result, dark=$($dwmArgs[2])"
 }
 foreach($size in @([Drawing.Size]::new(640,640),[Drawing.Size]::new(1000,850))) {
  $form.Size=$size; [Windows.Forms.Application]::DoEvents()
  foreach($name in @('toggle','profiles','startup','engineStartup','themeButton')) { $control=$type.GetField($name,$instance).GetValue($form); if(-not $control.Visible -or $control.Width -lt 30) { throw "Yeniden boyutlandırmada kontrol kayboldu: $name" } }
 }
} finally { $form.Dispose() }
Write-Output 'PASS: 7 Jump List komut eşlemesi, EXE metadatası, DWM tema geçişleri ve pencere boyutlandırma.'
$trayForm=[Activator]::CreateInstance($type,[object[]]@($true))
try {
 $timer=$type.GetField('timer',$instance).GetValue($trayForm); $timer.Stop()
 $channel=$type.GetField('commands',$instance).GetValue($trayForm)
 foreach($action in @('show','start','stop','restart','profiles','settings','exit')) {
  if(-not $commands.GetMethod('Send',$static).Invoke($null,[object[]]@($action))) { throw "Komut iletilemedi: $action" }
  if($commands.GetMethod('Poll',$instance).Invoke($channel,$null) -ne $action) { throw "Komut alınamadı: $action" }
 }
 foreach($state in @([ServiceProcess.ServiceControllerStatus]::Running,[ServiceProcess.ServiceControllerStatus]::Stopped)) {
  $type.GetMethod('SetServiceState',$instance).Invoke($trayForm,[object[]]@(0,$state)) | Out-Null
  $start=$type.GetField('trayToggle',$instance).GetValue($trayForm)
  $stop=$type.GetField('trayStop',$instance).GetValue($trayForm)
  if($start.Enabled -ne ($state -eq 'Stopped') -or $stop.Enabled -ne ($state -eq 'Running')) { throw 'Tepsi etkinlik durumu yanlış' }
 }
 $profiles=$type.GetField('trayProfiles',$instance).GetValue($trayForm)
 if($profiles.DropDownItems.Count -ne 7) { throw 'Tepside profil eksik' }
 foreach($action in @('show','profiles','settings')) { $type.GetMethod('DispatchCommand',$instance).Invoke($trayForm,[object[]]@($action)) | Out-Null; if(-not $trayForm.Visible) { throw 'Pencere açılmadı' } }
 $setTray=$type.GetMethod('SetTrayVisible',$instance); $tray=$type.GetField('tray',$instance).GetValue($trayForm); $trayButton=$type.GetField('trayButton',$instance).GetValue($trayForm)
 $setTray.Invoke($trayForm,[object[]]@($true)) | Out-Null
 $type.GetMethod('HideToTray',$instance).Invoke($trayForm,$null) | Out-Null
 if($trayForm.Visible -or $trayForm.ShowInTaskbar) { throw 'Tepsiye gizlenmedi' }
 $setTray.Invoke($trayForm,[object[]]@($false)) | Out-Null
 if($tray.Visible -or $trayButton.Visible) { throw 'Ayar kapalıyken tepsi simgesi gizlenmedi' }
 $trayHelp=$type.GetField('trayHelp',$instance).GetValue($trayForm)
 if($trayHelp.Text -notlike '*uygulama kapanır*' -or -not $trayHelp.Visible) { throw 'Ayar kapalıyken kapatma açıklaması görünmüyor' }
 if(-not $trayForm.Visible -or -not $trayForm.ShowInTaskbar) { throw 'Tepsideki gizli pencere simge kalkınca açılmadı' }
 $type.GetMethod('HideToTray',$instance).Invoke($trayForm,$null) | Out-Null
 if(-not $trayForm.Visible) { throw 'Simge yokken pencere gizlendi' }
 $trayForm.Close()
 if(-not $trayForm.IsDisposed) { throw 'Simge yokken kapatınca arayüz kapanmadı' }
} finally { $trayForm.Dispose() }
$trayForm=[Activator]::CreateInstance($type,[object[]]@($true))
try {
 $type.GetField('timer',$instance).GetValue($trayForm).Stop()
 $type.GetMethod('SetTrayVisible',$instance).Invoke($trayForm,[object[]]@($true)) | Out-Null
 $type.GetMethod('DispatchCommand',$instance).Invoke($trayForm,[object[]]@('show')) | Out-Null
 $trayForm.Close()
 if($trayForm.IsDisposed -or $trayForm.Visible) { throw 'Simge varken kapatınca tepsiye gizlenmedi' }
 $type.GetMethod('DispatchCommand',$instance).Invoke($trayForm,[object[]]@('exit')) | Out-Null
 if(-not $trayForm.IsDisposed) { throw 'Arayüz kapanmadı' }
} finally { $trayForm.Dispose() }
Write-Output 'PASS: 7 komutun IPC aktarımı, tepsi durumu ve profilleri, göster/ayarlar/profil/gizle/çık davranışları, tepsi ayarına göre simge ve kapatma. Motor değiştirilmedi.'
