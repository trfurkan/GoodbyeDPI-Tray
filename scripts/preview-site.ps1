param([string]$Language)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
# Each language runs in its own process: profile names and other texts are translated once, when the app starts.
if(-not $Language) {
 foreach($lang in @('tr','en','ru')) { & powershell -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath -Language $lang; if($LASTEXITCODE -ne 0) { throw "Görseller üretilemedi: $lang" } }
 'PASS: Refreshed website UI illustrations in TR/EN/RU; no access probes or service changes.'
 return
}
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.ServiceProcess
[Windows.Forms.Application]::EnableVisualStyles()
$a=[Reflection.Assembly]::LoadFile((Join-Path $root 'build/app/GoodbyeDPI.Tray.exe'))
$f=[Reflection.BindingFlags]'NonPublic,Instance'; $s=[Reflection.BindingFlags]'NonPublic,Static'
$a.GetType('GoodbyeTray.L').GetProperty('Current',$s).SetValue($null,$Language)
$t=$a.GetType('GoodbyeTray.MainForm')
$a.GetType('GoodbyeTray.Motion').GetField('Override',$s).SetValue($null,$false)
function Pump($n) { for($i=0;$i -lt $n;$i++) { [Windows.Forms.Application]::DoEvents(); Start-Sleep -Milliseconds 20 } }
# The window draws its own title bar inside the client area, so the picture is taken from the screen, client area only:
# what people see, without the classic frame that DrawToBitmap would paint around it.
function Capture-Window($form) {
 $form.ActiveControl=$null; Pump 15
 $origin=$form.PointToScreen([Drawing.Point]::Empty); $size=$form.ClientSize
 $b=New-Object Drawing.Bitmap($size.Width,$size.Height); $g=[Drawing.Graphics]::FromImage($b)
 try { $g.CopyFromScreen($origin,[Drawing.Point]::Empty,$size) } finally { $g.Dispose() }
 return $b
}
function Save-Control($control,$path) {
 $control.PerformLayout(); $b=New-Object Drawing.Bitmap($control.Width,$control.Height)
 try { $control.DrawToBitmap($b,(New-Object Drawing.Rectangle(0,0,$b.Width,$b.Height))); $b.Save($path) } finally { $b.Dispose() }
}
$folder=Join-Path $root ('docs/images/'+$(if($Language -eq 'tr'){''}else{$Language}))
New-Item -ItemType Directory -Path $folder -Force | Out-Null
$gallery=New-Object Drawing.Bitmap(960,660); $g=[Drawing.Graphics]::FromImage($gallery)
$g.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
try {
 $n=0
 foreach($theme in @('light','black','ocean','sakura','sunset','android','champagne')) {
  $form=[Activator]::CreateInstance($t,[object[]]@($false))
  try {
   $form.StartPosition='Manual'; $form.Location=New-Object Drawing.Point(60,30); $form.TopMost=$true
   $form.ShowInTaskbar=$false; $form.Show(); $form.Activate(); Pump 10
   $t.GetMethod('SetThemePreference',$f).Invoke($form,[object[]]@($theme,$false)) | Out-Null
   $t.GetMethod('SetServiceState',$f).Invoke($form,[object[]]@(0,[ServiceProcess.ServiceControllerStatus]::Running)) | Out-Null
   $t.GetMethod('FitDashboard',$f).Invoke($form,$null) | Out-Null
   $form.PerformLayout(); Pump 10
   $shot=Capture-Window $form
   try {
    if($theme -in @('light','black')) {
     $mode=if($theme -eq 'black'){'dark'}else{'light'}
     $shot.Save((Join-Path $folder "main-$mode.png"))
     if($theme -eq 'black') {
      # The menus open the way the title bar buttons open them, so they carry the app's own menu look.
      foreach($menuName in @('theme','language')) {
       $t.GetField(($menuName+'Button'),$f).GetValue($form).PerformClick(); Pump 5
       $menu=$t.GetField(($menuName+'Menu'),$f).GetValue($form)
       Save-Control $menu (Join-Path $folder "menu-$menuName.png"); $menu.Close(); Pump 5
      }
     }
     $t.GetMethod('ToggleAccess',$f).Invoke($form,$null) | Out-Null; Pump 20
     $open=Capture-Window $form; try { $open.Save((Join-Path $folder "access-$mode.png")) } finally { $open.Dispose() }
     $t.GetMethod('ToggleAccess',$f).Invoke($form,$null) | Out-Null; Pump 10
    }
    if($theme -ne 'black') { $g.DrawImage($shot,(New-Object Drawing.Rectangle(($n%3*320),([Math]::Floor($n/3)*330),320,330))); $n++ }
   } finally { $shot.Dispose() }
  } finally { $form.Dispose() }
 }
 $gallery.Save((Join-Path $folder 'themes.png'))
} finally { $g.Dispose(); $gallery.Dispose() }
"PASS: $Language"
