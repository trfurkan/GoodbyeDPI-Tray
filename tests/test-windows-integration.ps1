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
if($version.CompanyName -ne 'Furkan' -or $version.FileVersion -ne '1.7.2.0') { throw 'EXE metadatası yanlış' }
$type=$assembly.GetType('GoodbyeTray.MainForm')
$form=[Activator]::CreateInstance($type,[object[]]@($false))
try {
 $form.Show()
 foreach($mode in @('light','black','system','light')) {
  $type.GetMethod('SetThemePreference',$instance).Invoke($form,[object[]]@([string]$mode,$false)) | Out-Null
  [Windows.Forms.Application]::DoEvents()
  $dwmArgs=[object[]]@($form.Handle,20,0,4)
  $result=$windows.GetMethod('DwmGetWindowAttribute',$static).Invoke($null,$dwmArgs)
  if($result -eq 0 -and $mode -ne 'system' -and -not [Windows.Forms.SystemInformation]::HighContrast) { if($dwmArgs[2] -ne $(if($mode -eq 'black'){1}else{0})) { throw 'Başlık çubuğu tema değişimini takip etmedi' } }
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
 $setTray=$type.GetMethod('SetTrayVisible',$instance); $tray=$type.GetField('tray',$instance).GetValue($trayForm)
 $setTray.Invoke($trayForm,[object[]]@($true)) | Out-Null
 $type.GetMethod('HideToTray',$instance).Invoke($trayForm,$null) | Out-Null
 if($trayForm.Visible -or $trayForm.ShowInTaskbar) { throw 'Tepsiye gizlenmedi' }
 $setTray.Invoke($trayForm,[object[]]@($false)) | Out-Null
 if($tray.Visible) { throw 'Ayar kapalıyken tepsi simgesi gizlenmedi' }
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
$update=$assembly.GetType('GoodbyeTray.UpdateCheck'); $isNewer=$update.GetMethod('IsNewer',$static)
foreach($case in @(@('1.5.3','1.5.2',$true),@('1.10.0','1.9.9',$true),@('2.0','1.5.2',$true),@('1.5.2','1.5.2',$false),@('1.5.1','1.5.2',$false),@('bozuk','1.5.2',$false))) {
 if($isNewer.Invoke($null,[object[]]@($case[0],$case[1])) -ne $case[2]) { throw "Sürüm karşılaştırması yanlış: $($case[0]) / $($case[1])" }
}
$askForm=[Activator]::CreateInstance($type,[object[]]@($true))
try {
 $type.GetField('timer',$instance).GetValue($askForm).Stop()
 $refreshing=$type.GetField('refreshing',$instance); $engineBox=$type.GetField('engineStartup',$instance).GetValue($askForm); $trayBox=$type.GetField('startup',$instance).GetValue($askForm)
 $refreshing.SetValue($askForm,$true); $engineBox.Checked=$true; $trayBox.Checked=$true; $refreshing.SetValue($askForm,$false)
 $asked=0; $type.GetField('ConfirmTrayOff',$instance).SetValue($askForm,[Func[bool]]{ $script:asked++; $false })
 # Answering "no" keeps the icon: the box flips back and nothing is written.
 $trayBox.Checked=$false
 if($asked -ne 1 -or -not $trayBox.Checked) { throw 'Simge gizlenmeden önce sorulmadı ya da cevap dikkate alınmadı' }
} finally { $type.GetField('quitting',$instance).SetValue($askForm,$true); $askForm.Dispose() }
Write-Output 'PASS: GoodbyeDPI açılışta başlarken simge gizlenmeden önce soruluyor.'
$installerUrl=$update.GetMethod('InstallerUrl',$static)
$own='https://github.com/trfurkan/GoodbyeDPI-Tray/releases/download/v1.5.3/GoodbyeDPI-Installer-1.5.3.exe'
if($installerUrl.Invoke($null,[object[]]@("{`"assets`":[{`"browser_download_url`": `"$own`"}]}")) -ne $own) { throw 'Kurucu bağlantısı okunmadı' }
foreach($bad in @('https://example.com/GoodbyeDPI-Installer.exe','https://github.com/baska/GoodbyeDPI-Tray/releases/download/v1/GoodbyeDPI-Installer.exe','https://github.com/trfurkan/GoodbyeDPI-Tray/releases/download/v1.5.3/baska.exe')) {
 if($installerUrl.Invoke($null,[object[]]@("{`"browser_download_url`":`"$bad`"}")) -ne $null) { throw "Yabancı bağlantı kabul edildi: $bad" }
}
$trayForm=[Activator]::CreateInstance($type,[object[]]@($true))
try {
 $type.GetField('timer',$instance).GetValue($trayForm).Stop()
 # Without a tray icon ShowUpdate shows no balloon and stores nothing in the registry.
 $type.GetMethod('SetTrayVisible',$instance).Invoke($trayForm,[object[]]@($false)) | Out-Null
 $link=$type.GetField('updateLink',$instance).GetValue($trayForm); $menuItem=$type.GetField('trayUpdate',$instance).GetValue($trayForm)
 if($link.Visible -or $menuItem.Available) { throw 'Güncelleme yokken bağlantı görünüyor' }
 $type.GetMethod('ShowUpdate',$instance).Invoke($trayForm,[object[]]@('99.0')) | Out-Null
 if($link.Text -notlike '*99.0*' -or $menuItem.Text -notlike '*99.0*' -or -not $menuItem.Available) { throw 'Yeni sürüm bağlantısı gösterilmedi' }
 $about=$type.GetMethod('CreateAboutDialog',$instance).Invoke($trayForm,$null)
 try {
  if(-not ($about.Controls | Where-Object { $_ -is [Windows.Forms.CheckBox] -and $_.Text -like '*sürüm*' })) { throw 'Hakkında penceresinde güncelleme seçeneği yok' }
  if(-not ($about.Controls | Where-Object { $_.Text -eq 'Yeni sürüm var: 99.0' }) -or -not ($about.Controls | Where-Object { $_ -is [Windows.Forms.LinkLabel] -and $_.Text -eq 'İndir' })) { throw 'Hakkında penceresi yeni sürümü göstermiyor' }
  $icon=$about.Controls | Where-Object { $_.GetType().Name -eq 'UpdateStatusIcon' }; $iconState=$icon.GetType().GetProperty('State',$instance)
  if([string]$iconState.GetValue($icon) -ne 'Available') { throw 'Durum simgesi yanlış' }
  # The window follows later changes too: a finished check that finds nothing shows "current".
  $type.GetMethod('SetUpdateState',$instance).Invoke($trayForm,[object[]]@([Enum]::Parse($assembly.GetType('GoodbyeTray.UpdateState'),'Current'))) | Out-Null
  if(-not ($about.Controls | Where-Object { $_.Text -eq 'Güncel sürümü kullanıyorsun.' }) -or -not ($about.Controls | Where-Object { $_.Text -eq 'Şimdi kontrol et' }) -or [string]$iconState.GetValue($icon) -ne 'Current') { throw 'Hakkında penceresi güncel durumu göstermiyor' }
 } finally { $about.Dispose() }
 if($type.GetField('UpdateStateChanged',$instance).GetValue($trayForm) -ne $null) { throw 'Kapanan Hakkında penceresi dinlemeye devam ediyor' }
} finally { $type.GetField('quitting',$instance).SetValue($trayForm,$true); $trayForm.Dispose() }
$security=$assembly.GetType('GoodbyeTray.Program').GetField('ServiceSecurity',$static).GetValue($null)
$userAce=[regex]::Match($security,'\(A;;([A-Z]+);;;IU\)').Groups[1].Value
if($userAce -notmatch 'RP' -or $userAce -notmatch 'WP' -or $userAce -notmatch 'CR' -or $userAce -match 'DC|WD|WO|SD') { throw "Kullanıcı hizmet izni yanlış: $userAce" }
$apply=$assembly.GetType('GoodbyeTray.EngineRequest').GetMethod('Apply',$static)
# Requests outside the fixed set are refused before anything runs.
foreach($bad in @(@('--set-profile','7'),@('--set-profile','-1'),@('--set-profile','x'),@('--set-start','disabled'),@('--set-binpath','C:\x.exe'))) {
 $refused=$false; try { $apply.Invoke($null,[object[]]@($bad[0],$bad[1])) | Out-Null } catch { $refused=$_.Exception.ToString() -like '*Bilinmeyen hizmet isteği*' }
 if(-not $refused) { throw "Geçersiz hizmet isteği kabul edildi: $($bad -join ' ')" }
}
Write-Output 'PASS: Kullanıcılara yalnızca başlat/durdur/istek izni veriliyor, hizmet geçersiz istekleri reddediyor.'
$licenseType=$assembly.GetType('GoodbyeTray.LicenseForm'); $ctor=$licenseType.GetConstructors()[0]; $colors=@([Drawing.Color]::Black,[Drawing.Color]::DimGray,[Drawing.Color]::White,[Drawing.Color]::Gray,[Drawing.Color]::DarkGray)
$licenseDir=Join-Path $root 'vendor\goodbyedpi\licenses'
$window=$ctor.Invoke([object[]]@([string]$licenseDir,$true,$colors[0],$colors[1],$colors[2],$colors[3],$colors[4]))
try {
 $licenseList=$licenseType.GetField('list',$instance).GetValue($window); $items=$licenseList.GetType().GetField('Items',$instance).GetValue($licenseList); $shown=$licenseType.GetField('text',$instance).GetValue($window).Text
 if($items.Count -lt 4 -or $items -notcontains 'WinDivert' -or $shown.Length -lt 100) { throw 'Lisans penceresi dosyaları göstermiyor' }
} finally { $window.Dispose() }
$window=$ctor.Invoke([object[]]@([string](Join-Path $root "yok-boyle-bir-klasor"),$false,$colors[0],$colors[1],$colors[2],$colors[3],$colors[4]))
try { if($licenseType.GetField('text',$instance).GetValue($window).Text -notlike '*bulunamadı*') { throw 'Lisans klasörü yokken açıklama gösterilmedi' } } finally { $window.Dispose() }
Write-Output 'PASS: Lisans penceresi dosyaları listeliyor ve okuyor, klasör yokken açıklama gösteriyor.'
Write-Output 'PASS: Sürüm karşılaştırması, yeni sürüm bağlantısı ve tepsi menüsü, Hakkında penceresindeki güncelleme seçeneği. GitHub''a istek atılmadı.'
# Every text passed to L.T must have an English and a Russian form.
$lang=$assembly.GetType('GoodbyeTray.L'); $has=$lang.GetMethod('Has',$static); $missing=@()
foreach($file in Get-ChildItem (Join-Path $root 'src') -Filter *.cs) {
 if($file.Name -like 'Lang*.cs') { continue }
 foreach($m in [regex]::Matches([IO.File]::ReadAllText($file.FullName,[Text.Encoding]::UTF8),'L\.T\("((?:[^"\\]|\\.)*)"\)')) {
  $text=[regex]::Unescape($m.Groups[1].Value)
  if(-not $has.Invoke($null,[object[]]@($text))) { $missing+="$($file.Name): $text" }
 }
}
if($missing.Count) { throw "Cevirisi olmayan metinler:`n$($missing -join "`n")" }
foreach($code in 'tr','en','ru') { $flag=$lang.GetMethod('Flag',$static).Invoke($null,[object[]]@($code)); if($flag.Width -ne 20) { throw 'Bayrak cizilemedi' }; $flag.Dispose() }
Write-Output 'PASS: Tum arayuz metinlerinin Ingilizce ve Rusca cevirisi var, uc bayrak ciziliyor.'
