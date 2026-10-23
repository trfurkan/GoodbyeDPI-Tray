$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$asm=[Reflection.Assembly]::LoadFile((Join-Path $root 'build/app/GoodbyeDPI.Tray.exe'))
$flags=[Reflection.BindingFlags]'NonPublic,Static'
$program=$asm.GetType('GoodbyeTray.Program')
if($program.GetField('ZapretEnabled',$flags).GetValue($null)) { throw 'Experimental engine enabled' }
foreach($name in @('Args','Names')) {
 if($program.GetField($name,$flags).GetValue($null).Length -ne 7) { throw "Unexpected $name catalog" }
}
$parse=$program.GetMethod('OwnedProfileFromPath',$flags)
$upgrade=$program.GetMethod('UpgradeProfile',$flags)
for($id=0;$id -lt 12;$id++) {
 $path=$program.GetMethod('Binary',$flags).Invoke($null,@($id))
 if($parse.Invoke($null,@($path)) -ne $id) { throw "Lost ownership of profile $id" }
 $expected=if($id -lt 7) { $id } else { 0 }
 if($upgrade.Invoke($null,@($id)) -ne $expected) { throw "Wrong migration for profile $id" }
}
foreach($path in @($null,'"C:\foreign\app.exe" --engine-service 9')) {
 if($parse.Invoke($null,[object[]]@($path)) -ne -1) { throw 'Foreign service accepted' }
}
$setup=[Reflection.Assembly]::LoadFile((Join-Path $root 'dist/GoodbyeDPI-Installer.exe'))
if(@($setup.GetManifestResourceNames() | Where-Object { $_ -like 'payload/zapret2/*' }).Count) { throw 'Experimental payload bundled' }
$files=$setup.GetType('GoodbyeTray.Setup').GetField('Files',$flags).GetValue($null)
foreach($file in $files) { if($setup.GetManifestResourceNames() -notcontains "payload/$file") { throw "Missing $file" } }
if($files -match '^zapret2/') { throw 'Experimental install entry retained' }
Add-Type -AssemblyName System.Windows.Forms
$form=[Activator]::CreateInstance($asm.GetType('GoodbyeTray.MainForm'),[object[]]@($false))
try {
 $instance=[Reflection.BindingFlags]'NonPublic,Instance'
 $form.GetType().GetField('timer',$instance).GetValue($form).Stop()
 $form.Show()
 $motors=$form.GetType().GetField('motors',$instance).GetValue($form)
 if($motors.Visible -or $motors.Items.Count -ne 1) { throw 'Experimental selector visible' }
 if($form.GetType().GetField('profiles',$instance).GetValue($form).Items.Count -ne 7) { throw 'Missing standard profiles' }
} finally { $form.Dispose() }
Write-Output 'PASS: Standard payload, seven profiles, hidden motor selector, all twelve legacy service IDs and migration. No service changes.'
