$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Add-Type -AssemblyName System.Windows.Forms
$assembly=[Reflection.Assembly]::LoadFile((Join-Path $root 'build\app\GoodbyeDPI.Tray.exe'))
# Texts are checked in Turkish whatever language this computer's app is set to.
$assembly.GetType('GoodbyeTray.L').GetProperty('Current',[Reflection.BindingFlags]'NonPublic,Static').SetValue($null,'tr')
$static=[Reflection.BindingFlags]'NonPublic,Static'
$toast=$assembly.GetType('GoodbyeTray.Toast')
$appId=$toast.GetField('AppId',$static).GetValue($null)
$keyPath='HKCU:\'+$toast.GetProperty('RegistryPath',$static).GetValue($null)
$icon=$toast.GetProperty('IconPath',$static).GetValue($null)
# Leave an installed app's registration as it was.
$hadKey=Test-Path $keyPath
$form=(New-Object Windows.Forms.Form).psobject.BaseObject
try {
 $handle=$form.Handle
 $shown=$toast.GetMethod('Show',$static).Invoke($null,[object[]]@('GoodbyeDPI Tray','Bildirim testi',$form,[Action]{}))
 if(-not $shown) { throw 'Windows bildirimi gösterilemedi' }
 if(-not (Test-Path $keyPath) -or (Get-ItemProperty $keyPath).DisplayName -ne 'GoodbyeDPI Tray' -or -not (Test-Path $icon)) { throw 'Bildirim kaydı ya da simgesi yok' }
 [void][Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType=WindowsRuntime]
 Start-Sleep -Milliseconds 800
 # Someone may have turned this app's notifications off in Windows settings; then Windows drops them, as it should.
 $allowed=[string][Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier($appId).Setting -eq 'Enabled'
 if($allowed -and @([Windows.UI.Notifications.ToastNotificationManager]::History.GetHistory($appId)).Count -lt 1) { throw 'Bildirim bildirim merkezinde değil' }
 if(-not $allowed) { Write-Output 'NOT: Bu bilgisayarda GoodbyeDPI Tray bildirimleri Windows ayarlarından kapalı; bildirim merkezi denetimi atlandı.' }
 # Uninstall cleanup.
 $toast.GetMethod('Remove',$static).Invoke($null,$null) | Out-Null; Start-Sleep -Milliseconds 800
 if((Test-Path $keyPath) -or (Test-Path $icon)) { throw 'Kaldırınca bildirim kaydı silinmedi' }
 if(@([Windows.UI.Notifications.ToastNotificationManager]::History.GetHistory($appId)).Count -ne 0) { throw 'Kaldırınca bildirimler temizlenmedi' }
 # Escaping: XML characters in the text must not break the notification.
 if(-not $toast.GetMethod('Show',$static).Invoke($null,[object[]]@('<a & b>','"x" & ''y''',$form,$null))) { throw 'Özel karakterli bildirim gösterilemedi' }
 $toast.GetMethod('Remove',$static).Invoke($null,$null) | Out-Null; Start-Sleep -Milliseconds 800
} finally {
 $form.Dispose()
 if($hadKey) { $toast.GetMethod('Show',$static).Invoke($null,[object[]]@('GoodbyeDPI Tray','',(New-Object Windows.Forms.Form).psobject.BaseObject,$null)) | Out-Null; [Windows.UI.Notifications.ToastNotificationManager]::History.Clear($appId) }
}
# The installer must offer silent install and removal for winget.
$setup=[IO.File]::ReadAllText((Join-Path $root 'src\Setup.cs'),[Text.Encoding]::UTF8)
foreach($text in '"--silent"','QuietUninstallString','--uninstall --silent') { if(-not $setup.Contains($text)) { throw "Sessiz kurulum eksik: $text" } }
Write-Output 'PASS: Windows bildirimi gösteriliyor, bildirim merkezine düşüyor, kaldırınca temizleniyor; sessiz kurulum seçenekleri var.'
