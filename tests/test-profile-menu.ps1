$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$vendorDir = Join-Path $root 'vendor\goodbyedpi'
Add-Type -AssemblyName System.Windows.Forms
[Windows.Forms.Application]::SetUnhandledExceptionMode([Windows.Forms.UnhandledExceptionMode]::ThrowException)
[Windows.Forms.Application]::EnableVisualStyles()
$assembly = [Reflection.Assembly]::LoadFile((Join-Path $root 'build\app\GoodbyeDPI.Tray.exe'))
# Texts are checked in Turkish whatever language this computer's app is set to.
$assembly.GetType('GoodbyeTray.L').GetProperty('Current',[Reflection.BindingFlags]'NonPublic,Static').SetValue($null,'tr')
$flags = [Reflection.BindingFlags]'NonPublic,Instance'
$formType = $assembly.GetType('GoodbyeTray.MainForm')
$form = [Activator]::CreateInstance($formType,[object[]]@($false))
$menu = $null
try {
 $form.Show()
 # A drop-down closes at once when its window is not the active one (another app in front during the test run),
 # so bring the test window forward first; TopMost briefly lets it take the foreground.
 $form.TopMost=$true; $form.Activate(); [Windows.Forms.Application]::DoEvents(); $form.TopMost=$false; $form.Activate(); [Windows.Forms.Application]::DoEvents()
 $formType.GetMethod('SetServiceState',$flags).Invoke($form,[object[]]@(0,[ServiceProcess.ServiceControllerStatus]::Running)) | Out-Null
 $picker = $formType.GetField('profiles',$flags).GetValue($form)
 $menu = $picker.GetType().GetField('menu',$flags).GetValue($picker)
 $open = $picker.GetType().GetMethod('OpenMenu',$flags)
 for ($i=0; $i -lt 30; $i++) {
  # Windows may refuse to bring a background test window forward; a drop-down then closes at once. Try a few times.
  for ($try=0; $try -lt 5 -and -not $menu.Visible; $try++) { if ($try -gt 0) { $form.TopMost=$true; $form.Activate(); $form.TopMost=$false; Start-Sleep -Milliseconds 80 }; $open.Invoke($picker,$null) | Out-Null; [Windows.Forms.Application]::DoEvents() }
  if (-not $menu.Visible -or $menu.IsDisposed) { throw 'Profil menüsü açılmadı.' }
  $index = $i % 7
  $menu.Items[$index].PerformClick()
  if ($picker.SelectedIndex -ne $index) { throw 'Profil seçimi başarısız.' }
  $menu.Close([Windows.Forms.ToolStripDropDownCloseReason]::ItemClicked)
  [Windows.Forms.Application]::DoEvents()
  if ($menu.IsDisposed) { throw 'Menü kapanırken erken silindi.' }
  $open.Invoke($picker,$null) | Out-Null
  if (-not $menu.Items[$index].Checked) { throw 'Seçili profil işareti güncellenmedi.' }
  $menu.Close([Windows.Forms.ToolStripDropDownCloseReason]::Keyboard)
  [Windows.Forms.Application]::DoEvents()
  # Seven profiles, a separator and the find-a-profile entry.
  if ($menu.Items.Count -ne 9 -or $menu.Items[7] -isnot [Windows.Forms.ToolStripSeparator] -or $menu.Items[8].Text -notlike '*Uygun profil bul*') { throw 'Menü öğeleri çoğaldı.' }
 }
 $open.Invoke($picker,$null) | Out-Null
 $form.Dispose()
 [Windows.Forms.Application]::DoEvents()
 if (-not $menu.IsDisposed) { throw 'Pencere kapatılırken menü temizlenmedi.' }
 Write-Output 'PASS: 60 menü açma/kapatma, 30 profil seçimi, klavyeyle kapanma ve menü açıkken pencere temizliği. Hizmet değiştirilmedi.'
} finally { $form.Dispose() }
