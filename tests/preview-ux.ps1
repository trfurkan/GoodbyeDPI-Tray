$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$vendorDir = Join-Path $root 'vendor\goodbyedpi'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[Windows.Forms.Application]::EnableVisualStyles()
$assembly = [Reflection.Assembly]::LoadFile((Join-Path $root 'build\app\GoodbyeDPI.Tray.exe'))
# Texts are checked in Turkish whatever language this computer's app is set to.
$assembly.GetType('GoodbyeTray.L').GetProperty('Current',[Reflection.BindingFlags]'NonPublic,Static').SetValue($null,'tr')
$formType = $assembly.GetType('GoodbyeTray.MainForm')
$previewDir = Join-Path $root 'build\previews'
New-Item -ItemType Directory -Path $previewDir -Force | Out-Null
$flags = [Reflection.BindingFlags]'NonPublic,Instance'
foreach ($dark in @($false,$true)) {
 $form = [Activator]::CreateInstance($formType,[object[]]@($false))
 try {
  $formType.GetMethod('SetThemePreference',$flags).Invoke($form,[object[]]@($(if($dark){'black'}else{'light'}),$false)) | Out-Null
  $form.Show()
  $formType.GetMethod('SetServiceState',$flags).Invoke($form,[object[]]@(0,[ServiceProcess.ServiceControllerStatus]::Running)) | Out-Null
  [Windows.Forms.Application]::DoEvents()
  $mode = if ($dark) { 'dark' } else { 'light' }
  foreach ($fieldName in @('toggle','profiles','engineStartup','startup','restart','apply','findProfile','pauseButton','themeButton')) {
   $control = $formType.GetField($fieldName,$flags).GetValue($form)
   if (-not $control.Visible) { throw "Ana panelde görünmeyen kontrol: $fieldName" }
   $rect = $form.RectangleToClient($control.RectangleToScreen($control.ClientRectangle))
   if ($rect.Bottom -gt $form.ClientSize.Height -or $rect.Right -gt $form.ClientSize.Width) { throw "Ana panel dışına taşan kontrol: $fieldName" }
  }
  $themeButton=$formType.GetField('themeButton',$flags).GetValue($form)
  $themeMenu=$formType.GetField('themeMenu',$flags).GetValue($form)
  for ($cycle=0; $cycle -lt 5; $cycle++) {
   $themeButton.PerformClick()
   [Windows.Forms.Application]::DoEvents()
   if (-not $themeMenu.Visible -or $themeMenu.Items.Count -ne 9) { throw 'Tema menüsü açılmadı.' }
   if($cycle -eq 0) {
    $menuBitmap=[Drawing.Bitmap]::new($themeMenu.Width,$themeMenu.Height)
    try { $themeMenu.DrawToBitmap($menuBitmap,[Drawing.Rectangle]::new(0,0,$themeMenu.Width,$themeMenu.Height)); $menuBitmap.Save((Join-Path $previewDir "theme-menu-$mode.png")) } finally { $menuBitmap.Dispose() }
   }
   $themeMenu.Close()
   [Windows.Forms.Application]::DoEvents()
   if ($themeMenu.IsDisposed) { throw 'Tema menüsü erken temizlendi.' }
  }
  $button = $formType.GetField('toggle',$flags).GetValue($form)
  foreach ($state in @('normal','hover','pressed','disabled')) {
   $button.Enabled = $state -ne 'disabled'
   $button.GetType().GetField('hovered',$flags).SetValue($button,($state -eq 'hover'))
   $button.GetType().GetField('pressed',$flags).SetValue($button,($state -eq 'pressed'))
   $bitmap = [Drawing.Bitmap]::new($button.Width,$button.Height)
   try {
    $button.DrawToBitmap($bitmap,[Drawing.Rectangle]::new(0,0,$button.Width,$button.Height))
    foreach ($point in @([Drawing.Point]::new(0,0),[Drawing.Point]::new($button.Width-1,$button.Height-1))) {
     if ($bitmap.GetPixel($point.X,$point.Y).ToArgb() -ne $button.BackColor.ToArgb()) { throw "Düğme köşesi temizlenmedi: $mode / $state" }
    }
   } finally { $bitmap.Dispose() }
  }
  $button.Enabled=$true
  $button.GetType().GetField('hovered',$flags).SetValue($button,$false)
  $button.GetType().GetField('pressed',$flags).SetValue($button,$false)
  $bitmap = [Drawing.Bitmap]::new($form.Width,$form.Height)
  try { $form.DrawToBitmap($bitmap,[Drawing.Rectangle]::new(0,0,$form.Width,$form.Height)); $bitmap.Save((Join-Path $previewDir "single-panel-$mode.png")) } finally { $bitmap.Dispose() }
  $formType.GetMethod('ShowDiagnostics',$flags).Invoke($form,$null) | Out-Null
  $console=$formType.GetField('diagnosticsWindow',$flags).GetValue($form)
  if(-not $console.Visible -or $console.GetType().Name -ne 'EngineConsoleForm'){throw 'Motor konsolu acilmadi'}
  $formType.GetMethod('ShowDiagnostics',$flags).Invoke($form,$null)|Out-Null
  if(-not [object]::ReferenceEquals($console,$formType.GetField('diagnosticsWindow',$flags).GetValue($form))){throw 'Ikinci konsol penceresi olusturuldu'}
  $console.Close()
  if(-not $console.IsDisposed){throw 'Motor konsolu kapanmadi'}
 } finally { $form.Dispose() }
}
Write-Output 'PASS: Açık/koyu tema, tek panel görünürlüğü, dört düğme durumunda köşe pikselleri ve motor konsolu.'
$iconStream=[IO.File]::OpenRead((Join-Path $root 'assets\app.ico'))
try { $icon=[Drawing.Icon]::new($iconStream,128,128); try { $image=[Drawing.Bitmap]::new(128,128); try { $graphics=[Drawing.Graphics]::FromImage($image); try { $graphics.Clear([Drawing.Color]::Transparent); $graphics.DrawIcon($icon,[Drawing.Rectangle]::new(0,0,128,128)) } finally { $graphics.Dispose() }; $image.Save((Join-Path $previewDir 'app-icon.png')) } finally { $image.Dispose() } } finally { $icon.Dispose() } } finally { $iconStream.Dispose() }
