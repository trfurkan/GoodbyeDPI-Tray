$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[Windows.Forms.Application]::EnableVisualStyles()
$asm=[Reflection.Assembly]::LoadFile((Join-Path $root 'build\app\GoodbyeDPI.Tray.exe'))
$flags=[Reflection.BindingFlags]'NonPublic,Instance'
$static=[Reflection.BindingFlags]'NonPublic,Static'
$lang=$asm.GetType('GoodbyeTray.L')
$lang.GetProperty('Current',$static).SetValue($null,'tr')
$type=$asm.GetType('GoodbyeTray.ProfileSearchForm')
$resultType=$asm.GetType('GoodbyeTray.ProfileSearch+Result')
$result=[Activator]::CreateInstance($resultType,$true)
$scores=$resultType.GetField('Scores',$flags).GetValue($result)
foreach($pair in @(@(0,1),@(1,2),@(2,3),@(3,0),@(4,-1),@(5,2),@(6,1))) { $scores.Add([int]$pair[0],[int]$pair[1]) }
$resultType.GetField('Profile',$flags).SetValue($result,2)
$resultType.GetField('Score',$flags).SetValue($result,3)
$output=Join-Path $root 'build\previews'
New-Item -ItemType Directory -Path $output -Force | Out-Null
foreach($dark in @($false,$true)) {
 $form=[Activator]::CreateInstance($type,[Reflection.BindingFlags]'Instance,NonPublic',$null,[object[]]@($dark),$null)
 try {
  $form.Show(); [Windows.Forms.Application]::DoEvents()
  $mode=if($dark){'dark'}else{'light'}
  function Save-View($name) {
   [Windows.Forms.Application]::DoEvents()
   $bitmap=[Drawing.Bitmap]::new($form.Width,$form.Height)
   try { $form.DrawToBitmap($bitmap,[Drawing.Rectangle]::new(0,0,$form.Width,$form.Height)); $bitmap.Save((Join-Path $output "profile-search-$mode-$name.png")) } finally { $bitmap.Dispose() }
  }
  Save-View 'intro'
  $type.GetMethod('BeginTest',$flags).Invoke($form,$null) | Out-Null
  $type.GetMethod('SetProgress',$flags).Invoke($form,[object[]]@(3,2)) | Out-Null
  $primary=$type.GetField('primary',$flags).GetValue($form)
  if($primary.Visible) { throw 'Apply is available during test' }
  Save-View 'progress'
  $form.Close()
  if($form.IsDisposed -or -not $type.GetField('cancelling',$flags).GetValue($form)) { throw 'Close did not request cancellation and await recovery' }
  $type.GetMethod('ShowResults',$flags).Invoke($form,[object[]]@($result,$false,$null)) | Out-Null
  if(-not $primary.Visible) { throw 'Successful result has no apply action' }
  $rows=$type.GetField('rows',$flags).GetValue($form)
  if($rows.Controls.Count -ne 16) { throw 'Profile results missing' }
  if($rows.GetControlFromPosition(1,3).Text -notlike '*Önerilen*') { throw 'Recommendation missing' }
  Save-View 'results'
  $form.Size=$form.MinimumSize; [Windows.Forms.Application]::DoEvents()
  foreach($control in @($primary,$type.GetField('secondary',$flags).GetValue($form),$rows)) {
   $bounds=$form.RectangleToClient($control.RectangleToScreen($control.ClientRectangle))
   if($bounds.Bottom -gt $form.ClientSize.Height -or $bounds.Right -gt $form.ClientSize.Width) { throw 'Result content outside window' }
  }
  $type.GetMethod('ShowResults',$flags).Invoke($form,[object[]]@($result,$true,$null)) | Out-Null
  if($primary.Visible) { throw 'Cancelled test permits apply' }
  $type.GetMethod('ShowResults',$flags).Invoke($form,[object[]]@($result,$false,[Exception]::new('Deneme hatası: motor başlatılamadı.'))) | Out-Null
  if($primary.Visible) { throw 'Failed test permits apply' }
  Save-View 'error'
 } finally { $form.Dispose() }
}
Write-Output 'PASS: Themed intro/progress/results/error views, seven profile rows, recommendation, cancellation recovery, apply guards and minimum window bounds. No network or service changes.'
