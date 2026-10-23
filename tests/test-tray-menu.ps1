$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[Windows.Forms.Application]::EnableVisualStyles()
$asm=[Reflection.Assembly]::LoadFile((Join-Path $root 'build\app\GoodbyeDPI.Tray.exe'))
$flags=[Reflection.BindingFlags]'NonPublic,Instance'
$asm.GetType('GoodbyeTray.L').GetProperty('Current',[Reflection.BindingFlags]'NonPublic,Static').SetValue($null,'tr')
$type=$asm.GetType('GoodbyeTray.MainForm')
$form=[Activator]::CreateInstance($type,[object[]]@($true))
$output=Join-Path $root 'build\previews'; New-Item -ItemType Directory -Path $output -Force | Out-Null
try {
 $type.GetField('timer',$flags).GetValue($form).Stop()
 $form.Show()
 function Save-Menu($menu,$name) {
  $menu.Show($form,[Drawing.Point]::new(30,60)); [Windows.Forms.Application]::DoEvents()
  $bitmap=[Drawing.Bitmap]::new($menu.Width,$menu.Height)
  try { $menu.DrawToBitmap($bitmap,[Drawing.Rectangle]::new(0,0,$menu.Width,$menu.Height)); $bitmap.Save((Join-Path $output ($name+'.png'))) } finally { $bitmap.Dispose(); $menu.Close() }
 }
 foreach($mode in @('ocean','light')) {
  $type.GetMethod('SetThemePreference',$flags).Invoke($form,[object[]]@($mode,$false)) | Out-Null
  $tray=$type.GetField('tray',$flags).GetValue($form)
  $menu=$tray.ContextMenuStrip
  if($menu.Font.Size -lt 10 -or $menu.Items[0].Height -lt 38) { throw 'Compact legacy tray layout remains' }
  Save-Menu $menu ('tray-menu-'+$mode)
  $sub=$type.GetField('trayPause',$flags).GetValue($form).DropDown
  if($sub.BackColor -ne $menu.BackColor -or $sub.Items[0].Height -lt 60) { throw 'Pause submenu style missing' }
  Save-Menu $sub ('tray-pause-'+$mode)
  for($cycle=0;$cycle -lt 3;$cycle++) {
   $pause=$type.GetMethod('PreparePauseMenu',$flags).Invoke($form,[object[]]@($false))
   Save-Menu $pause ('pause-menu-'+$mode)
   if($pause.IsDisposed -or $pause.Items.Count -ne 2) { throw 'Pause menu lifetime/rebuild failed' }
  }
  $pause=$type.GetMethod('PreparePauseMenu',$flags).Invoke($form,[object[]]@($true))
  if($pause.Items.Count -ne 2 -or $pause.Items[0].Height -lt 38) { throw 'Resume actions missing' }
 }
} finally { $type.GetField('quitting',$flags).SetValue($form,$true); $form.Dispose() }
Write-Output 'PASS: Tray/pause menu sizes, recursive theme, resume actions, repeated opening and previews. No service commands sent.'
