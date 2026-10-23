$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[Windows.Forms.Application]::EnableVisualStyles()
$asm=[Reflection.Assembly]::LoadFile((Join-Path $root 'build\app\GoodbyeDPI.Tray.exe'))
$static=[Reflection.BindingFlags]'Static,NonPublic'
$instance=[Reflection.BindingFlags]'Instance,NonPublic'
$asm.GetType('GoodbyeTray.L').GetProperty('Current',$static).SetValue($null,'tr')
$parse=$asm.GetType('GoodbyeTray.UpdateCheck').GetMethod('ExtractNotes',$static)
$json='{"body":"## Changes\n- **Profile finder** added.\n- [Pause](https://example.com) improved.\n- Unicode: \u00e7 and \"quotes\".\n```\nignored code\n```","assets":[{"body":"wrong nested body"}]}'
$notes=$parse.Invoke($null,[object[]]@($json))
if($notes -notlike '*Profile finder added.*' -or $notes -notlike '*Pause improved.*' -or $notes -like '*ignored code*' -or $notes -like '*nested*' -or $notes -like '*https:*' -or -not $notes.Contains([string][char]0xe7)) { throw 'Markdown/JSON handling failed' }
foreach($empty in @('{"body":null}','{}','{"body":""}')) { if($parse.Invoke($null,[object[]]@($empty)) -ne '') { throw 'Empty release notes should be empty' } }
$many=@{body=((1..20|ForEach-Object { '- Item '+$_ }) -join "`n")}|ConvertTo-Json
$short=$parse.Invoke($null,[object[]]@([string]$many))
if(($short -split ([Environment]::NewLine+[Environment]::NewLine)).Count -ne 8) { throw 'Summary not bounded' }
$type=$asm.GetType('GoodbyeTray.ReleaseNotesForm')
$output=Join-Path $root 'build\previews'; New-Item -ItemType Directory -Path $output -Force | Out-Null
foreach($dark in @($false,$true)) {
 $form=[Activator]::CreateInstance($type,$instance,$null,[object[]]@($dark,'9.9.0',$notes),$null)
 try {
  $form.Show(); [Windows.Forms.Application]::DoEvents()
  $bitmap=[Drawing.Bitmap]::new($form.Width,$form.Height)
  try { $form.DrawToBitmap($bitmap,[Drawing.Rectangle]::new(0,0,$form.Width,$form.Height)); $bitmap.Save((Join-Path $output ('release-notes-'+$dark+'.png'))) } finally { $bitmap.Dispose() }
  $form.Size=$form.MinimumSize; [Windows.Forms.Application]::DoEvents()
  foreach($button in @($form.AcceptButton,$form.CancelButton)) {
   $bounds=$form.RectangleToClient($button.RectangleToScreen($button.ClientRectangle))
   if($bounds.Right -gt $form.ClientSize.Width -or $bounds.Bottom -gt $form.ClientSize.Height) { throw 'Button outside window' }
  }
  $form.AcceptButton.PerformClick()
  if($form.DialogResult -ne 'OK') { throw 'Download selection not returned' }
 } finally { $form.Dispose() }
}
$emptyForm=[Activator]::CreateInstance($type,$instance,$null,[object[]]@($true,'9.9.0',''),$null)
try { if($emptyForm.Controls[0].Controls[2].Text.Length -lt 20) { throw 'Missing empty-state explanation' } } finally { $emptyForm.Dispose() }
Write-Output 'PASS: Release-note JSON escapes, Markdown cleanup, empty notes, bounded summary, light/dark layout and download action. No network request or download.'

# Regression: the latest installed version still supplies notes, and clicking a
# Windows notification opens the modal even while the main window is hidden.
$update=$asm.GetType('GoodbyeTray.UpdateCheck')
$current=$asm.GetType('GoodbyeTray.AppInfo').GetField('Version',$static).GetRawConstantValue()
$payload=@{tag_name=('v'+$current);body='- Current release notes';assets=@()} | ConvertTo-Json
if($update.GetMethod('ReadRelease',$static).Invoke($null,@([string]$payload))) { throw 'Current version reported as an update' }
if($update.GetField('ReleaseNotes',$static).GetValue($null) -notlike '*Current release notes*') { throw 'Current release notes discarded' }
$mainType=$asm.GetType('GoodbyeTray.MainForm')
$main=[Activator]::CreateInstance($mainType,[object[]]@($true))
try {
 $mainType.GetField('timer',$instance).GetValue($main).Stop()
 $main.Show()
 $mainType.GetMethod('SetUpdateState',$instance).Invoke($main,@([Enum]::Parse($asm.GetType('GoodbyeTray.UpdateState'),'Current'))) | Out-Null
 if($mainType.GetField('updateLink',$instance) -or $mainType.GetField('trayUpdate',$instance)) { throw 'Notes entry remains outside About' }
 $script:seenNotes=$false
 $closer=New-Object Windows.Forms.Timer
 $closer.Interval=100
 $closer.add_Tick({
  foreach($window in @([Windows.Forms.Application]::OpenForms)) {
   if($window.GetType().FullName -eq 'GoodbyeTray.ReleaseNotesForm') {
    $script:seenNotes=$true
    if($window.AcceptButton -ne $window.CancelButton) { $script:unexpectedDownload=$true }
    $window.Close()
   }
  }
 })
 $script:unexpectedDownload=$false
 $main.Hide(); $closer.Start()
 try {
  $tray=$mainType.GetField('tray',$instance).GetValue($main)
  $tray.GetType().GetMethod('OnBalloonTipClicked',$instance).Invoke($tray,$null) | Out-Null
 } finally { $closer.Stop(); $closer.Dispose() }
 if(-not $script:seenNotes -or -not $main.Visible) { throw 'Notification did not reveal release notes' }
 if($script:unexpectedDownload) { throw 'Installed release offers a new-version download' }
} finally { $main.Dispose() }
Write-Output 'PASS: Installed-version notes retained, entry visible, notification click reveals dialog, no redundant download. No network or service changes.'
$decision=$update.GetMethod('ShouldShowInstalledNotes',$static)
foreach($case in @(@($null,'1.8.0',$true),@('1.7.2','1.8.0',$true),@('1.8.0','1.8.0',$false),@('1.9.0','1.8.0',$false))) {
 if($decision.Invoke($null,[object[]]@($case[0],$case[1])) -ne $case[2]) { throw 'First launch decision incorrect' }
}
if($update.GetMethod('InstalledNotes',$static).Invoke($null,$null).Length -lt 80) { throw 'Offline notes missing' }
Write-Output 'PASS: First launch, upgrade, repeat launch, downgrade and bundled offline notes.'
