param([switch]$NativeOnly)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$asm=[Reflection.Assembly]::LoadFile((Join-Path $root 'build/experimental/app/GoodbyeDPI.Tray.exe'))
$static=[Reflection.BindingFlags]'NonPublic,Static'
$instance=[Reflection.BindingFlags]'NonPublic,Instance'
$program=$asm.GetType('GoodbyeTray.Program')
$arguments=$program.GetField('Args',$static).GetValue($null)
if($arguments.Count -ne 12) { throw 'Unexpected profile catalog' }
if($NativeOnly) {
 $previous=$env:__COMPAT_LAYER
 try {
  # Only parse options/files; never open WinDivert or change network traffic.
  $env:__COMPAT_LAYER='RunAsInvoker'
  foreach($arch in @('x86','x86_64')) {
   Push-Location (Join-Path $root "vendor/zapret2/$arch")
   try {
    for($id=7;$id -lt $arguments.Count;$id++) {
     $profileArgs=@($arguments[$id].Split(' ')) + '--dry-run'
     $output=& ./winws2.exe @profileArgs 2>&1
     if($LASTEXITCODE -ne 0) { throw "Profile $id ($arch): $output" }
    }
   } finally { Pop-Location }
  }
 } finally { $env:__COMPAT_LAYER=$previous }
 Write-Output 'PASS: All Zapret profiles parse with official x86/x64 binaries. Dry-run only, no driver started.'
 exit
}
$setup=[Reflection.Assembly]::LoadFile((Join-Path $root 'dist/experimental/GoodbyeDPI-Installer.exe'))
$files=$setup.GetType('GoodbyeTray.Setup').GetField('Files',$static).GetValue($null)
$resources=$setup.GetManifestResourceNames()
foreach($file in $files) { if($resources -notcontains "payload/$file") { throw "Missing resource: $file" } }
foreach($file in Get-ChildItem (Join-Path $root 'vendor/zapret2') -Recurse -File) {
 $relative='zapret2/'+$file.FullName.Substring((Join-Path $root 'vendor/zapret2').Length+1).Replace('\','/')
 if($files -notcontains $relative) { throw "Missing installer entry: $relative" }
 $stream=$setup.GetManifestResourceStream("payload/$relative")
 $sha=[Security.Cryptography.SHA256]::Create()
 try { $hash=([BitConverter]::ToString($sha.ComputeHash($stream))).Replace('-',''); if($hash -ne (Get-FileHash $file.FullName -Algorithm SHA256).Hash) { throw "Payload changed: $relative" } } finally { $stream.Dispose(); $sha.Dispose() }
}
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[Windows.Forms.Application]::EnableVisualStyles()
$type=$asm.GetType('GoodbyeTray.MainForm')
$form=[Activator]::CreateInstance($type,[object[]]@($false))
try {
 $type.GetField('timer',$instance).GetValue($form).Stop()
 $form.Show()
 $set=$type.GetMethod('SetServiceState',$instance)
 $set.Invoke($form,[object[]]@(7,[ServiceProcess.ServiceControllerStatus]::Running)) | Out-Null
 $motors=$type.GetField('motors',$instance).GetValue($form)
 $profiles=$type.GetField('profiles',$instance).GetValue($form)
 if($motors.SelectedIndex -ne 1 -or $profiles.Items.Count -ne 5) { throw 'Zapret selection not restored' }
 $profiles.SelectedIndex=2
 if($type.GetProperty('SelectedProfile',$instance).GetValue($form) -ne 9) { throw 'Wrong global profile ID' }
 $set.Invoke($form,[object[]]@(7,[ServiceProcess.ServiceControllerStatus]::Running)) | Out-Null
 if($profiles.SelectedIndex -ne 2) { throw 'Refresh discarded pending selection' }
 $motors.SelectedIndex=0
 if($profiles.Items.Count -ne 7) { throw 'GoodbyeDPI profiles not preserved' }
 $motors.SelectedIndex=1
 $set.Invoke($form,[object[]]@(7,[ServiceProcess.ServiceControllerStatus]::Running)) | Out-Null
 [Windows.Forms.Application]::DoEvents()
 $bitmap=[Drawing.Bitmap]::new($form.Width,$form.Height)
 try { $form.DrawToBitmap($bitmap,[Drawing.Rectangle]::new(0,0,$form.Width,$form.Height)); $bitmap.Save((Join-Path $root 'build/previews/zapret-dashboard.png')) } finally { $bitmap.Dispose() }
} finally { $form.Dispose() }
$dialog=[Activator]::CreateInstance($asm.GetType('GoodbyeTray.ProfileSearchForm'),[Reflection.BindingFlags]'Instance,NonPublic',$null,[object[]]@($true,[int[]]@(7,8,9,10,11)),$null)
try {
 $rows=$dialog.GetType().GetField('rows',$instance).GetValue($dialog)
 if($rows.Controls.Count -ne 12 -or $rows.GetControlFromPosition(0,1).Text -notlike 'Zapret 2*') { throw 'Search results include other engine' }
} finally { $dialog.Dispose() }
Write-Output 'PASS: Payload hashes, package completeness, engine selector, pending selection, per-engine search results. No service changes.'
