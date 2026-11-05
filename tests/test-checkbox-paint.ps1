$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
$root=Split-Path $PSScriptRoot -Parent
$a=[Reflection.Assembly]::LoadFile((Join-Path $root 'build\app\GoodbyeDPI.Tray.exe'))
$t=$a.GetType('GoodbyeTray.ThemeCheckBox'); $p=$a.GetType('GoodbyeTray.AppPalette')
$static=[Reflection.BindingFlags]'Static,NonPublic'; $instance=[Reflection.BindingFlags]'Instance,NonPublic'
$box=[Activator]::CreateInstance($t); $box.Size=[Drawing.Size]::new(500,28); $box.Text='Setting'
$b=[Drawing.Bitmap]::new(500,28); $g=[Drawing.Graphics]::FromImage($b)
try {
 foreach($theme in @('light','black','ocean','sakura','sunset','android','champagne')) {
  $p.GetField('Variant',$static).SetValue($null,$theme)
  $dark=$p.GetMethod('IsDark',$static).Invoke($null,@($theme))
  $bg=$p.GetMethod('Surface',$static).Invoke($null,@($dark)); $box.BackColor=$bg
  $box.ForeColor=$p.GetMethod('Text',$static).Invoke($null,@($dark))
  foreach($name in @('Accent','Tick','Edge','Muted')) {
   $value=if($name -eq 'Accent'){$p.GetProperty('Accent',$static).GetValue($null)}elseif($name -eq 'Tick'){$p.GetProperty('AccentInk',$static).GetValue($null)}else{$p.GetMethod($(if($name -eq 'Edge'){'Border'}else{'Muted'}),$static).Invoke($null,@($dark))}
   $t.GetField($name,$instance).SetValue($box,$value)
  }
  foreach($enabled in @($true,$false)) { foreach($checked in @($true,$false)) {
   $box.Enabled=$enabled; $box.Checked=$checked
   $g.Clear([Drawing.Color]::Magenta)
   $event=[Windows.Forms.PaintEventArgs]::new($g,[Drawing.Rectangle]::new(0,0,500,28))
   $t.GetMethod('OnPaint',$instance).Invoke($box,@($event)) | Out-Null
   foreach($point in @([Drawing.Point]::new(499,14),[Drawing.Point]::new(250,1),[Drawing.Point]::new(250,27))) {
    if($b.GetPixel($point.X,$point.Y).ToArgb() -ne $bg.ToArgb()){throw "Stale background: $theme / $enabled / $checked"}
   }
  }}
 }
 'PASS: 28 checkbox frames repaint stale pixels in seven palettes, checked/unchecked and enabled/disabled.'
} finally { $g.Dispose(); $b.Dispose(); $box.Dispose() }