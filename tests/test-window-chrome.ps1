$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class ChromeProbe {
 [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h,int m,IntPtr w,IntPtr l);
 [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr h,int index);
}
"@
$root=Split-Path $PSScriptRoot -Parent
$asm=[Reflection.Assembly]::LoadFile((Join-Path $root 'build\app\GoodbyeDPI.Tray.exe'))
# Texts are checked in Turkish whatever language this computer's app is set to.
$asm.GetType('GoodbyeTray.L').GetProperty('Current',[Reflection.BindingFlags]'NonPublic,Static').SetValue($null,'tr')
$type=$asm.GetType('GoodbyeTray.MainForm')
$form=[Activator]::CreateInstance($type,[object[]]@($false))
try {
 $form.Show(); [Windows.Forms.Application]::DoEvents()
 if($form.FormBorderStyle -ne 'Sizable' -or -not $form.ControlBox -or -not $form.MaximizeBox -or -not $form.MinimizeBox){throw 'Native Windows controls missing'}
 $style=[ChromeProbe]::GetWindowLong($form.Handle,-16)
 foreach($flag in @(0xC00000,0x40000,0x80000,0x20000,0x10000)){if(($style -band $flag) -ne $flag){throw 'Native caption/resize/system menu style missing'}}
 if([Windows.Forms.Form].GetProperty('MaximizedBounds',[Reflection.BindingFlags]'NonPublic,Instance').GetValue($form,$null) -ne [Drawing.Rectangle]::Empty){throw 'Custom maximize bounds still override Windows'}
 foreach($entry in @(@(0xF030,'Maximized'),@(0xF120,'Normal'),@(0xF020,'Minimized'),@(0xF120,'Normal'))) {
  for($settle=0;$settle -lt 20;$settle++){[Windows.Forms.Application]::DoEvents(); Start-Sleep -Milliseconds 20}
  [ChromeProbe]::SendMessage($form.Handle,0x112,[IntPtr]::new([int]$entry[0]),[IntPtr]::Zero)|Out-Null
  [Windows.Forms.Application]::DoEvents()
  for($attempt=0;$attempt -lt 100 -and $form.WindowState.ToString() -ne $entry[1];$attempt++){[Windows.Forms.Application]::DoEvents(); Start-Sleep -Milliseconds 20}
  if($form.WindowState.ToString() -ne $entry[1]){throw ('Native system command failed: expected '+$entry[1]+' actual '+$form.WindowState)}
 }
 [ChromeProbe]::SendMessage($form.Handle,0x112,[IntPtr]::new(0xF060),[IntPtr]::Zero)|Out-Null
 [Windows.Forms.Application]::DoEvents()
 if(-not $form.IsDisposed){throw 'Native close failed'}
} finally {$form.Dispose()}
Write-Output 'PASS: Native Windows caption, sizing frame, system menu and maximize/restore/minimize/close commands.'