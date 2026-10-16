$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$vendorDir = Join-Path $root 'vendor\goodbyedpi'
$assembly = [Reflection.Assembly]::LoadFile((Join-Path $root 'build\app\GoodbyeDPI.Tray.exe'))
$type = $assembly.GetType('GoodbyeTray.Program')
$flags = [Reflection.BindingFlags]'NonPublic,Static'
$arguments = $type.GetField('Args',$flags).GetValue($null)
$names = @('turkey_dnsredir.cmd','turkey_dnsredir_alternative_superonline.cmd','turkey_dnsredir_alternative2_superonline.cmd','turkey_dnsredir_alternative3_superonline.cmd','turkey_dnsredir_alternative4_superonline.cmd','turkey_dnsredir_alternative5_superonline.cmd','turkey_dnsredir_alternative6_superonline.cmd')
for ($i=0; $i -lt $names.Count; $i++) {
 $source = Get-Content -LiteralPath (Join-Path $vendorDir $names[$i]) -Raw
 $expected = [regex]::Match($source,'goodbyedpi\.exe ([^\r\n]+)').Groups[1].Value.Trim()
 if ($expected -ne $arguments[$i]) { throw "Profil uyuşmuyor: $($names[$i])" }
}
foreach ($script in Get-ChildItem -LiteralPath @($PSScriptRoot,(Join-Path $PSScriptRoot 'system'),(Join-Path $root 'scripts')) -Filter '*.ps1') {
 $tokens=$null; $parseErrors=$null
 [Management.Automation.Language.Parser]::ParseFile($script.FullName,[ref]$tokens,[ref]$parseErrors) | Out-Null
 if ($parseErrors.Count -gt 0) { throw $parseErrors[0] }
}
$manifest = [xml](Get-Content -LiteralPath (Join-Path $root 'src\app.manifest') -Raw)
if ($manifest.assembly.trustInfo.security.requestedPrivileges.requestedExecutionLevel.level -ne 'asInvoker') { throw 'Arayüz gereksiz yükseltme istiyor.' }
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[Windows.Forms.Application]::EnableVisualStyles()
$formType = $assembly.GetType('GoodbyeTray.MainForm')
$form = [Activator]::CreateInstance($formType, [object[]]@($false))
try {
 $instanceFlags = [Reflection.BindingFlags]'NonPublic,Instance'
 $combo = $formType.GetField('profiles',$instanceFlags).GetValue($form)
 if ($combo.Items.Count -ne 7 -or $combo.SelectedIndex -lt 0) { throw 'Profil seçimi oluşturulamadı.' }
 $stateMethod = $formType.GetMethod('SetServiceState',$instanceFlags)
 $toggle = $formType.GetField('toggle',$instanceFlags).GetValue($form)
 $apply = $formType.GetField('apply',$instanceFlags).GetValue($form)
 $restart = $formType.GetField('restart',$instanceFlags).GetValue($form)
 $notice = $formType.GetField('notice',$instanceFlags).GetValue($form)
 $stateMethod.Invoke($form,[object[]]@(0,[ServiceProcess.ServiceControllerStatus]::Stopped)) | Out-Null
 if ($toggle.Text -ne 'Başlat' -or -not $toggle.Enabled -or $restart.Enabled -or $apply.Enabled) { throw 'Durdurulmuş hizmet düğmeleri yanlış.' }
 $combo.SelectedIndex = 4
 if (-not $apply.Enabled) { throw 'Uygulanmamış profil etkinleştirilemiyor.' }
 $notice.Text = 'Kalıcı işlem sonucu'
 $stateMethod.Invoke($form,[object[]]@(0,[ServiceProcess.ServiceControllerStatus]::Running)) | Out-Null
 if ($toggle.Text -ne 'Durdur' -or -not $restart.Enabled -or $combo.SelectedIndex -ne 4 -or $notice.Text -ne 'Kalıcı işlem sonucu') { throw 'Çalışan hizmet veya bekleyen seçim durumu yanlış.' }
 $stateMethod.Invoke($form,[object[]]@(4,[ServiceProcess.ServiceControllerStatus]::Running)) | Out-Null
 if ($apply.Enabled) { throw 'Zaten etkin olan profil tekrar uygulanabiliyor.' }
 $formType.GetField('busy',$instanceFlags).SetValue($form,$true)
 $stateMethod.Invoke($form,[object[]]@(4,[ServiceProcess.ServiceControllerStatus]::Running)) | Out-Null
 if ($toggle.Enabled -or $apply.Enabled -or $restart.Enabled -or $combo.Enabled) { throw 'İşlem sırasında kontroller kilitlenmedi.' }
 $formType.GetField('busy',$instanceFlags).SetValue($form,$false)
 $stateMethod.Invoke($form,[object[]]@(-1,[ServiceProcess.ServiceControllerStatus]::Stopped)) | Out-Null
 if ($toggle.Enabled -or $apply.Enabled -or $restart.Enabled) { throw 'Kurulum yokken hizmet düğmeleri etkin.' }
 $setTheme = $formType.GetMethod('SetThemePreference',$instanceFlags)
 $setTheme.Invoke($form,[object[]]@('light',$false)) | Out-Null
 $lightColor = $form.BackColor
 $themeButton = $formType.GetField('themeButton',$instanceFlags).GetValue($form)
 if (-not $themeButton.Text.Contains('Aydınlık')) { throw 'Aydınlık tema düğmesi güncellenmedi.' }
 $setTheme.Invoke($form,[object[]]@('ocean',$false)) | Out-Null
 if (-not [Windows.Forms.SystemInformation]::HighContrast -and $form.BackColor.ToArgb() -eq $lightColor.ToArgb()) { throw 'Tema renkleri değişmedi.' }
 if (-not $themeButton.Text.Contains('Okyanus')) { throw 'Okyanus tema düğmesi güncellenmedi.' }
 $setTheme.Invoke($form,[object[]]@('black',$false)) | Out-Null
 if (-not $themeButton.Text.Contains('Karanlık')) { throw 'Karanlık tema düğmesi güncellenmedi.' }
 $setTheme.Invoke($form,[object[]]@('system',$false)) | Out-Null
 if (-not $themeButton.Text.Contains('Sistem')) { throw 'Sistem teması seçilemedi.' }
} finally { $form.Dispose() }
Write-Output 'PASS: Aydınlık, karanlık ve sistem teması; kullanıcı tercihi değiştirilmeden doğrulandı.'
Write-Output 'PASS: Başlat/durdur, bekleyen profil, işlem kilidi, kalıcı sonuç ve kurulum eksikliği durumları.'
Write-Output 'PASS: 7 profil, PowerShell sözdizimi, normal kullanıcı manifesti ve arayüz profil seçimi.'
$root = Split-Path $PSScriptRoot -Parent
$setupAssembly = [Reflection.Assembly]::LoadFile((Join-Path $root 'dist\GoodbyeDPI-Installer.exe'))
$setupType = $setupAssembly.GetType('GoodbyeTray.Setup')
foreach ($binary in @($assembly,$setupAssembly)) {
 $iconStream=$binary.GetManifestResourceStream('GoodbyeTray.AppIcon')
 if ($null -eq $iconStream) { throw 'Gömülü uygulama simgesi yok.' }
 try { $icon=[Drawing.Icon]::new($iconStream,32,32); $icon.Dispose() } finally { $iconStream.Dispose() }
}
$iconBytes=[IO.File]::ReadAllBytes((Join-Path $root 'assets\app.ico'))
if ([BitConverter]::ToUInt16($iconBytes,4) -ne 8) { throw 'Simge boyutları eksik.' }
Write-Output 'PASS: Uygulama/kurucu gömülü simgesi ve sekiz ICO boyutu.'
$files = $setupType.GetField('Files',$flags).GetValue($null)
foreach ($file in $files) {
 $stream = $setupAssembly.GetManifestResourceStream("payload/$file")
 if ($null -eq $stream) { throw "Eksik paket dosyası: $file" }
 try {
  $sha = [Security.Cryptography.SHA256]::Create()
  try { $actual = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','') } finally { $sha.Dispose() }
  $source = if ($file.StartsWith('GoodbyeDPI.Tray.exe')) { Join-Path (Join-Path $root 'build\app') $file } else { Join-Path $vendorDir $file }
  $rootNotices=@{'licenses/LICENSE-goodbyedpi-tray.txt'='LICENSE';'licenses/NOTICE-goodbyedpi-tray.txt'='NOTICE';'licenses/THIRD_PARTY_NOTICES.md'='THIRD_PARTY_NOTICES.md'}
  if($rootNotices.ContainsKey($file)){$source=Join-Path $root $rootNotices[$file]}
  if ($actual -ne (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash) { throw "Paket dosyası değişmiş: $file" }
 } finally { $stream.Dispose() }
}
$blocked = $false
try { $setupType.GetMethod('Target',$flags).Invoke($null,[object[]]@('..\outside.txt')) | Out-Null } catch { $blocked = $true }
if (-not $blocked) { throw 'Paket yol denetimi başarısız.' }
foreach ($remove in @($false,$true)) {
 $setupForm = [Activator]::CreateInstance($setupAssembly.GetType('GoodbyeTray.SetupForm'),[object[]]@($remove))
 $setupForm.Dispose()
}
Write-Output "PASS: $($files.Count) gömülü dosyanın SHA256 eşleşmesi, paket yolu sınırı, kurulum ve kaldırma ekranları."
