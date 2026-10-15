$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[Windows.Forms.Application]::EnableVisualStyles()
$root=Split-Path $PSScriptRoot -Parent
$asm=[Reflection.Assembly]::LoadFile((Join-Path $root 'build\app\GoodbyeDPI.Tray.exe'))
$type=$asm.GetType('GoodbyeTray.EngineConsoleForm'); $flags=[Reflection.BindingFlags]'NonPublic,Instance'; $static=[Reflection.BindingFlags]'NonPublic,Static'
$parser=$type.GetMethod('TryParse',$static)
foreach($profile in 0..6){
 $frame="GDC1|$profile|running|123"+[Environment]::NewLine+'real output'
 $parameters=[object[]]@($frame,0,'','')
 if(-not $parser.Invoke($null,$parameters) -or $parameters[1] -ne $profile -or $parameters[3] -ne 'real output'){throw 'Console frame parsing changed output or profile'}
}
foreach($frame in @('GDC1|7|running|1',"GDC1|8|running|1`nwrong","GDC1|0|bogus|1`nwrong")){$parameters=[object[]]@($frame,0,'',''); if($parser.Invoke($null,$parameters)){throw 'Malformed frame accepted'}}
$logPath=Join-Path $env:ProgramFiles 'GoodbyeDPI Tray\engine-console.txt'
$frame=[IO.File]::ReadAllText($logPath)
$expected=$frame.Substring($frame.IndexOf("`n")+1)
foreach($dark in @($false,$true)){
 $form=[Activator]::CreateInstance($type,[Reflection.BindingFlags]'NonPublic,Instance',$null,[object[]]@($dark),$null)
 try{
  $form.Show()
  $terminal=$type.GetField('terminal',$flags).GetValue($form)
  for($i=0;$i -lt 80 -and $terminal.TextLength -eq 0;$i++){[Windows.Forms.Application]::DoEvents(); Start-Sleep -Milliseconds 25}
  if($terminal.Text -ne $expected){throw 'Viewer does not show the exact real motor output'}
  if($form.Text -notlike '*Konsol'){throw 'Console title missing'}
  $follow=$type.GetField('follow',$flags).GetValue($form); $follow.Checked=$false; if($follow.Checked){throw 'Live follow did not pause'}
  $mode=if($dark){'dark'}else{'light'}
  $bitmap=[Drawing.Bitmap]::new($form.Width,$form.Height)
  try{$form.DrawToBitmap($bitmap,[Drawing.Rectangle]::new(0,0,$form.Width,$form.Height)); $bitmap.Save((Join-Path $root "build\previews\engine-console-$mode.png"))}finally{$bitmap.Dispose()}
 }finally{$form.Dispose()}
}
Write-Output 'PASS: Seven profile frame mappings, invalid-frame rejection, exact real motor text in light/dark console viewer and pause control.'