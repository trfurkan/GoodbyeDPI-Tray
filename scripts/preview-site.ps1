$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[Windows.Forms.Application]::EnableVisualStyles()
$a=[Reflection.Assembly]::LoadFile((Join-Path $root 'build/app/GoodbyeDPI.Tray.exe'))
$f=[Reflection.BindingFlags]'NonPublic,Instance'; $s=[Reflection.BindingFlags]'NonPublic,Static'
$t=$a.GetType('GoodbyeTray.MainForm')
$a.GetType('GoodbyeTray.Motion').GetField('Override',$s).SetValue($null,$false)
function Save-Control($control,$path) {
 $control.PerformLayout(); $b=New-Object Drawing.Bitmap($control.Width,$control.Height)
 try { $control.DrawToBitmap($b,(New-Object Drawing.Rectangle(0,0,$b.Width,$b.Height))); $b.Save($path) } finally { $b.Dispose() }
}
foreach($lang in @('tr','en','ru')) {
 $a.GetType('GoodbyeTray.L').GetProperty('Current',$s).SetValue($null,$lang)
 $folder=Join-Path $root ('docs/images/'+$(if($lang -eq 'tr'){''}else{$lang}))
 New-Item -ItemType Directory -Path $folder -Force | Out-Null
 $gallery=New-Object Drawing.Bitmap(960,660); $g=[Drawing.Graphics]::FromImage($gallery)
 try {
 $n=0
 foreach($theme in @('light','black','ocean','sakura','sunset','android','champagne')) {
  $form=[Activator]::CreateInstance($t,[object[]]@($false))
  try {
   $form.ClientSize=New-Object Drawing.Size(900,940)
   $form.ShowInTaskbar=$false; $form.Show(); [Windows.Forms.Application]::DoEvents()
   $t.GetMethod('SetThemePreference',$f).Invoke($form,[object[]]@($theme,$false)) | Out-Null
   $t.GetMethod('SetServiceState',$f).Invoke($form,[object[]]@(0,[ServiceProcess.ServiceControllerStatus]::Running)) | Out-Null
   $t.GetField('detail',$f).GetValue($form).Text=''
   $t.GetMethod('FitDashboard',$f).Invoke($form,$null) | Out-Null
   $form.PerformLayout()
   if($theme -in @('light','black')) {
    $mode=if($theme -eq 'black'){'dark'}else{'light'}
    Save-Control $form (Join-Path $folder "main-$mode.png")
    if($theme -eq 'black') {
     foreach($menuName in @('theme','language')) {
      $menu=$t.GetField(($menuName+'Menu'),$f).GetValue($form)
      $menu.BackColor=$form.BackColor; $menu.ForeColor=$form.ForeColor
      $rendererType=$a.GetType('GoodbyeTray.MenuRenderer')
      $renderer=[Activator]::CreateInstance($rendererType,[Reflection.BindingFlags]'NonPublic,Instance',$null,[object[]]@($form.BackColor,$form.ForeColor,$form.ForeColor),$null)
      $menu.Renderer=$renderer
      Save-Control $menu (Join-Path $folder "menu-$menuName.png")
     }
    }
    $t.GetMethod('ToggleAccess',$f).Invoke($form,$null) | Out-Null
    Save-Control $form (Join-Path $folder "access-$mode.png")
    $t.GetMethod('ToggleAccess',$f).Invoke($form,$null) | Out-Null
   }
   if($theme -ne 'black') {
    $b=New-Object Drawing.Bitmap($form.Width,$form.Height)
    try { $form.DrawToBitmap($b,(New-Object Drawing.Rectangle(0,0,$b.Width,$b.Height))); $g.DrawImage($b,(New-Object Drawing.Rectangle(($n%3*320),([Math]::Floor($n/3)*330),320,330))) } finally { $b.Dispose() }
    $n++
   }
  } finally { $form.Dispose() }
 }
 $gallery.Save((Join-Path $folder 'themes.png'))
 } finally { $g.Dispose(); $gallery.Dispose() }
}
'PASS: Refreshed website UI illustrations in TR/EN/RU; no access probes or service changes.'