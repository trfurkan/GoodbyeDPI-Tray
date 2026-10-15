$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class ResizeDamage {
 [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left,Top,Right,Bottom; }
 [DllImport("user32.dll")] public static extern bool GetUpdateRect(IntPtr hwnd,out Rect rect,bool erase);
}
"@
[Windows.Forms.Application]::EnableVisualStyles()
$root=Split-Path $PSScriptRoot -Parent
$assembly=[Reflection.Assembly]::LoadFile((Join-Path $root 'build\app\GoodbyeDPI.Tray.exe'))
$flags=[Reflection.BindingFlags]'NonPublic,Instance'
$panelType=$assembly.GetType('GoodbyeTray.SoftPanel')
$hostForm=[Windows.Forms.Form]::new(); $hostForm.ClientSize=[Drawing.Size]::new(600,300)
$panel=[Activator]::CreateInstance($panelType)
try {
 $panel.Size=[Drawing.Size]::new(280,120); $hostForm.Controls.Add($panel); $hostForm.Show(); [Windows.Forms.Application]::DoEvents()
 $panel.Width=380
 $damage=[ResizeDamage+Rect]::new()
 if(-not [ResizeDamage]::GetUpdateRect($panel.Handle,[ref]$damage,$false) -or $damage.Left -ne 0 -or $damage.Right -lt $panel.ClientSize.Width){throw 'Resize did not invalidate the entire card'}
} finally {$hostForm.Dispose()}
$type=$assembly.GetType('GoodbyeTray.MainForm')
foreach($theme in @('light','dark')) {
 $form=[Activator]::CreateInstance($type,[object[]]@($false))
 try {
  $type.GetMethod('SetThemePreference',$flags).Invoke($form,[object[]]@($theme,$false))|Out-Null
  $form.Show(); [Windows.Forms.Application]::DoEvents()
  for($cycle=0;$cycle -lt 5;$cycle++) {foreach($size in @([Drawing.Size]::new(640,640),[Drawing.Size]::new(1420,1000),[Drawing.Size]::new(800,720),[Drawing.Size]::new(1100,680))) {$form.Size=$size; [Windows.Forms.Application]::DoEvents()
    foreach($name in @('toggle','profiles','engineStartup','startup','restart','apply','themeButton','detail')) {
     $control=$type.GetField($name,$flags).GetValue($form)
     if(-not $control.Visible -or $control.Height -lt $control.Font.Height){throw "Clipped control: $name"}
     $bounds=$form.RectangleToClient($control.RectangleToScreen($control.ClientRectangle))
     if($bounds.Bottom -gt $form.ClientSize.Height -or $bounds.Right -gt $form.ClientSize.Width){throw "Outside viewport: $name"}
    }
    foreach($hostPanel in $form.Controls){if($hostPanel -is [Windows.Forms.ScrollableControl] -and ($hostPanel.AutoScroll -or $hostPanel.VerticalScroll.Visible)){throw 'Outer scrollbar active'}}}}
  $form.WindowState='Maximized'; [Windows.Forms.Application]::DoEvents(); $form.WindowState='Normal'; [Windows.Forms.Application]::DoEvents()
  $form.Size=[Drawing.Size]::new(1000,800); [Windows.Forms.Application]::DoEvents()
  function Find-Developer($parent){foreach($child in $parent.Controls){if($child.Text -match 'Geliştirici: trfurkan'){return $child}; $found=Find-Developer $child; if($found){return $found}}}
  $footer=Find-Developer $form
  if(-not $footer -or -not $footer.Visible -or $footer.Height -lt $footer.Font.Height){throw 'Developer footer is missing or clipped'}
  $bitmap=[Drawing.Bitmap]::new($form.Width,$form.Height)
  try{$form.DrawToBitmap($bitmap,[Drawing.Rectangle]::new(0,0,$form.Width,$form.Height)); $bitmap.Save((Join-Path $root "build\previews\resize-$theme.png"))}finally{$bitmap.Dispose()}
 }finally{$form.Dispose()}
}
Write-Output 'PASS: Full-card resize invalidation; 40 resize transitions, maximize/restore, light/dark themes and developer footer.'