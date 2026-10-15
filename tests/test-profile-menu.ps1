$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$vendorDir = Join-Path $root 'vendor\goodbyedpi'
Add-Type -AssemblyName System.Windows.Forms
[Windows.Forms.Application]::SetUnhandledExceptionMode([Windows.Forms.UnhandledExceptionMode]::ThrowException)
[Windows.Forms.Application]::EnableVisualStyles()
$assembly = [Reflection.Assembly]::LoadFile((Join-Path $root 'build\app\GoodbyeDPI.Tray.exe'))
$flags = [Reflection.BindingFlags]'NonPublic,Instance'
$formType = $assembly.GetType('GoodbyeTray.MainForm')
$form = [Activator]::CreateInstance($formType,[object[]]@($false))
$menu = $null
try {
 $form.Show()
 $formType.GetMethod('SetServiceState',$flags).Invoke($form,[object[]]@(0,[ServiceProcess.ServiceControllerStatus]::Running)) | Out-Null
 $picker = $formType.GetField('profiles',$flags).GetValue($form)
 $menu = $picker.GetType().GetField('menu',$flags).GetValue($picker)
 $open = $picker.GetType().GetMethod('OpenMenu',$flags)
 for ($i=0; $i -lt 30; $i++) {
  $open.Invoke($picker,$null) | Out-Null
  [Windows.Forms.Application]::DoEvents()
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
  if ($menu.Items.Count -ne 7) { throw 'Menü öğeleri çoğaldı.' }
 }
 $open.Invoke($picker,$null) | Out-Null
 $form.Dispose()
 [Windows.Forms.Application]::DoEvents()
 if (-not $menu.IsDisposed) { throw 'Pencere kapatılırken menü temizlenmedi.' }
 Write-Output 'PASS: 60 menü açma/kapatma, 30 profil seçimi, klavyeyle kapanma ve menü açıkken pencere temizliği. Hizmet değiştirilmedi.'
} finally { $form.Dispose() }
