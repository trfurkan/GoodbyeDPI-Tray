$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[Windows.Forms.Application]::EnableVisualStyles()
$a=[Reflection.Assembly]::LoadFile((Join-Path $root 'build/app/GoodbyeDPI.Tray.exe'))
$f=[Reflection.BindingFlags]'NonPublic,Instance'; $s=[Reflection.BindingFlags]'NonPublic,Static'
$motion=$a.GetType('GoodbyeTray.Motion').GetField('Override',$s)
$t=$a.GetType('GoodbyeTray.MainForm')
function Pump([int]$ms) { $watch=[Diagnostics.Stopwatch]::StartNew(); while($watch.ElapsedMilliseconds -lt $ms) { [Windows.Forms.Application]::DoEvents(); Start-Sleep -Milliseconds 5 } }
foreach($theme in @('light','black')) {
 $form=[Activator]::CreateInstance($t,[object[]]@($false))
 try {
  $motion.SetValue($null,$true)
  $form.Size=New-Object Drawing.Size(900,800); $form.Show()
  $t.GetMethod('SetThemePreference',$f).Invoke($form,[object[]]@($theme,$false)) | Out-Null
  $toggle=$t.GetMethod('ToggleAccess',$f); $progress=$t.GetField('accessProgress',$f)
  $toggle.Invoke($form,$null) | Out-Null
  $t.GetField('accessAnimation',$f).GetValue($form).Stop()
  $t.GetField('accessStarted',$f).SetValue($form,[Environment]::TickCount-100)
  $t.GetMethod('AnimateAccess',$f).Invoke($form,[object[]]@($form,[EventArgs]::Empty)) | Out-Null
  $mid=$progress.GetValue($form)
  if($mid -le 0 -or $mid -ge 1) { throw 'Expansion has no intermediate frame' }
  $toggle.Invoke($form,$null) | Out-Null
  if([Math]::Abs($progress.GetValue($form)-$mid) -gt 0.01) { throw 'Reversal jumps' }
  Pump 350
  if($progress.GetValue($form) -ne 0) { throw 'Collapse did not finish' }
  $toggle.Invoke($form,$null) | Out-Null; Pump 350
  if($progress.GetValue($form) -ne 1) { throw 'Expansion did not finish' }
  $full=$t.GetField('accessFull',$f).GetValue($form); $ft=$full.GetType()
  $view=$ft.GetField('viewport',$f).GetValue($full); $vt=$view.GetType()
  $list=$ft.GetField('siteList',$f).GetValue($full)
  if($view.AutoScroll -or $list.AutoScroll -or $view.VerticalScroll.Visible -or $list.VerticalScroll.Visible) { throw 'Native scrollbar visible' }
  $limit=$vt.GetProperty('Limit',$f).GetValue($view)
  if($limit -le 0) { throw 'Short window should scroll' }
  $vt.GetMethod('ScrollTo',$f).Invoke($view,[object[]]@([single]$limit,$true)) | Out-Null; Pump 60
  $offset=$vt.GetProperty('Offset',$f).GetValue($view)
  if($offset -le 0 -or $offset -ge $limit) { throw 'Scroll has no intermediate frame' }
  Pump 180
  if($vt.GetProperty('Offset',$f).GetValue($view) -ne $limit) { throw 'Last rows unreachable' }
  $motion.SetValue($null,$false)
  $toggle.Invoke($form,$null) | Out-Null
  if($progress.GetValue($form) -ne 0) { throw 'Reduced motion did not snap' }
  $toggle.Invoke($form,$null) | Out-Null
  $view.Dock='None'; $view.Height=600
  if($vt.GetProperty('Limit',$f).GetValue($view) -ne 0 -or $vt.GetProperty('Offset',$f).GetValue($view) -ne 0) { throw 'Fitting list retains scroll' }
 } finally { $form.Dispose() }
}
$motion.SetValue($null,$null)
'PASS: Animation frames, reversal, reduced motion, smooth scroll bounds and no native scrollbars in light/dark themes. No network or service changes.'
