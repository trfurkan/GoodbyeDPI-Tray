$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Add-Type -AssemblyName System.Windows.Forms
$asm=[Reflection.Assembly]::LoadFile((Join-Path $root 'build/app/GoodbyeDPI.Tray.exe'))
$flags=[Reflection.BindingFlags]'NonPublic,Static'
$download=$asm.GetType('GoodbyeTray.UpdateDownload')
$valid=$download.GetMethod('ValidUrl',$flags)
$url='https://github.com/trfurkan/GoodbyeDPI-Tray/releases/download/v1.8.0/GoodbyeDPI-Installer-1.8.0.exe'
if(-not $valid.Invoke($null,@($url))) { throw 'Valid URL rejected' }
foreach($bad in @('http://github.com/trfurkan/GoodbyeDPI-Tray/releases/download/v1/test.exe',($url+'?x=1'),$url.Replace('github.com','github.com.evil.test'),$url.Replace('trfurkan','other'))) { if($valid.Invoke($null,@($bad))) { throw "Foreign URL accepted: $bad" } }
$copy=$download.GetMethod('CopyVerified',$flags)
$bytes=[Text.Encoding]::UTF8.GetBytes('test installer bytes')
$sha=[Security.Cryptography.SHA256]::Create()
try { $digest='sha256:'+([BitConverter]::ToString($sha.ComputeHash($bytes))).Replace('-','').ToLowerInvariant() } finally { $sha.Dispose() }
$progress=[Action[long]]{param($n) $script:received=$n}
foreach($scenario in @('success','truncated','oversized','hash','cancelled')) {
 $inputStream=[IO.MemoryStream]::new($bytes); $output=[IO.MemoryStream]::new();$cancel=[Threading.CancellationTokenSource]::new()
 $size=[long]$bytes.Length; $hash=$digest
 if($scenario -eq 'truncated') { $size++ }
 if($scenario -eq 'oversized') { $size-- }
 if($scenario -eq 'hash') { $hash='sha256:'+('0'*64) }
 if($scenario -eq 'cancelled') { $cancel.Cancel() }
 try {
  $failed=$false
  try { $copy.Invoke($null,[object[]]@($inputStream,$output,$size,$hash,$progress,$cancel.Token)) | Out-Null } catch { $failed=$true }
  if($failed -eq ($scenario -eq 'success')) { throw "Unexpected outcome: $scenario" }
  if($scenario -eq 'success' -and ($output.Length -ne $bytes.Length -or $script:received -ne $size)) { throw 'Progress or content incorrect' }
 } finally { $inputStream.Dispose();$output.Dispose();$cancel.Dispose() }
}
$update=$asm.GetType('GoodbyeTray.UpdateCheck')
$json=@{tag_name='v99.0';body='- test';assets=@(@{browser_download_url=$url;size=$bytes.Length;digest=$digest})}|ConvertTo-Json -Depth 4
$update.GetMethod('ReadRelease',$flags).Invoke($null,@([string]$json)) | Out-Null
$asset=$update.GetField('DownloadAsset',$flags).GetValue($null)
if(!$asset) { throw 'Release metadata missing' }
$instance=[Reflection.BindingFlags]'NonPublic,Instance'
$asset.GetType().GetField('Url',$instance).SetValue($asset,'https://invalid.test/installer.exe')
foreach($dark in @($false,$true)) {
 $form=[Activator]::CreateInstance($asm.GetType('GoodbyeTray.UpdateDownloadForm'),$instance,$null,[object[]]@($dark,$asset),$null)
 try {
  $form.Show(); [Windows.Forms.Application]::DoEvents()
  $until=[DateTime]::UtcNow.AddSeconds(5)
  while($form.GetType().GetField('active',$instance).GetValue($form) -and [DateTime]::UtcNow -lt $until) { [Windows.Forms.Application]::DoEvents(); Start-Sleep -Milliseconds 10 }
  if(-not $form.GetType().GetField('retry',$instance).GetValue($form).Visible) { throw ('Retry unavailable: '+$form.GetType().GetField('status',$instance).GetValue($form).Text) }
  if($form.GetType().GetField('status',$instance).GetValue($form).Text -ne 'İndirme başarısız. Tekrar deneyebilirsin.') { throw 'Download failure message wrong' }
  $bitmap=[Drawing.Bitmap]::new($form.Width,$form.Height)
  try { $form.DrawToBitmap($bitmap,[Drawing.Rectangle]::new(0,0,$form.Width,$form.Height));$bitmap.Save((Join-Path $root "build/previews/update-download-$dark.png")) } finally { $bitmap.Dispose() }
 } finally { $form.Dispose() }
}
# A downloaded installer that cannot be opened: retry opens it again without a download, closing the window deletes it.
$prefix=$asm.GetType('GoodbyeTray.UpdateDownload').GetField('FolderPrefix',$flags).GetValue($null)
$folder=Join-Path ([IO.Path]::GetTempPath()) ($prefix+'test-'+[Guid]::NewGuid().ToString('N')); New-Item -ItemType Directory $folder | Out-Null
$form=[Activator]::CreateInstance($asm.GetType('GoodbyeTray.UpdateDownloadForm'),$instance,$null,[object[]]@($false,$asset),$null)
try {
 $form.GetType().GetField('downloaded',$instance).SetValue($form,(Join-Path $folder 'missing.exe'))
 $form.Show(); $until=[DateTime]::UtcNow.AddSeconds(5)
 do { [Windows.Forms.Application]::DoEvents(); Start-Sleep -Milliseconds 10 } while($form.GetType().GetField('active',$instance).GetValue($form) -and [DateTime]::UtcNow -lt $until)
 if($form.GetType().GetField('status',$instance).GetValue($form).Text -ne 'Kurucu açılamadı. Tekrar deneyebilirsin.') { throw ('Open failure message wrong: '+$form.GetType().GetField('status',$instance).GetValue($form).Text) }
 if(-not (Test-Path $folder)) { throw 'Downloaded installer discarded before the window closed' }
 $form.Close()
 if(Test-Path $folder) { throw 'Unopened installer left in Temp' }
} finally { $form.Dispose() }
# Earlier update folders are removed at start.
$old=Join-Path ([IO.Path]::GetTempPath()) ($prefix+'test-'+[Guid]::NewGuid().ToString('N')); New-Item -ItemType Directory $old | Out-Null; Set-Content (Join-Path $old 'GoodbyeDPI-Installer.exe') 'x'
$asm.GetType('GoodbyeTray.UpdateDownload').GetMethod('CleanOld',$flags).Invoke($null,$null)
if(Test-Path $old) { throw 'Old update folder not cleaned' }
# Signatures: the release script signs with a throwaway key here; the app accepts only the matching version, hash and key.
$signature=$asm.GetType('GoodbyeTray.UpdateSignature')
$verify=$signature.GetMethods($flags) | Where-Object { $_.Name -eq 'Verify' -and $_.GetParameters().Count -eq 4 }
$work=Join-Path ([IO.Path]::GetTempPath()) ('gdt-sign-'+[Guid]::NewGuid().ToString('N')); New-Item -ItemType Directory $work | Out-Null
try {
 $key=Join-Path $work 'key\test-key.xml'
 $created=& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root 'scripts\sign-release.ps1') -NewKey -KeyPath $key
 $public=[string]($created | Where-Object { $_ -like '<RSAKeyValue>*' } | Select-Object -First 1)
 if(-not $public -or $public -match '<D>') { throw 'Public key not printed, or private parts leaked into it' }
 $file=Join-Path $work 'GoodbyeDPI-Installer-9.9.9.exe'; [IO.File]::WriteAllBytes($file,$bytes)
 & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root 'scripts\sign-release.ps1') -File $file -Version 9.9.9 -KeyPath $key | Out-Null
 $sig=[string][IO.File]::ReadAllText($file+'.sig'); $hex=$digest.Substring(7)
 if(-not $verify.Invoke($null,[object[]]@('9.9.9',$hex,$sig,$public))) { throw 'Valid signature rejected' }
 if($verify.Invoke($null,[object[]]@('9.9.10',$hex,$sig,$public))) { throw 'Signature accepted for another version' }
 if($verify.Invoke($null,[object[]]@('9.9.9',('0'*64),$sig,$public))) { throw 'Signature accepted for another file' }
 if($verify.Invoke($null,[object[]]@('9.9.9',$hex,'bm90IGEgc2lnbmF0dXJl',$public))) { throw 'Garbage signature accepted' }
 if($verify.Invoke($null,[object[]]@('9.9.9',$hex,'',$public))) { throw 'Missing signature accepted' }
 # The embedded key is a different key, so a test signature must not pass as a real release.
 $embedded=$signature.GetField('PublicKey',$flags).GetValue($null)
 if($embedded -notlike '<RSAKeyValue><Modulus>*' -or $embedded -match '<D>' -or $verify.Invoke($null,[object[]]@('9.9.9',$hex,$sig,$embedded))) { throw 'Embedded key wrong or accepts a foreign signature' }
} finally { Remove-Item -Recurse -Force $work -ErrorAction SilentlyContinue }
$sigValid=$signature.GetMethod('ValidUrl',$flags)
if(-not $sigValid.Invoke($null,@($url+'.sig')) -or $sigValid.Invoke($null,@($url)) -or $sigValid.Invoke($null,@(($url+'.sig').Replace('trfurkan','other')))) { throw 'Signature URL rules wrong' }
# The release metadata links the installer to its .sig and its version; without a .sig there is no signature URL.
$json=@{tag_name='v9.9.9';body='- test';assets=@(@{browser_download_url=$url;size=$bytes.Length;digest=$digest},@{browser_download_url=($url+'.sig');size=512})}|ConvertTo-Json -Depth 4
$update.GetMethod('ReadRelease',$flags).Invoke($null,@([string]$json)) | Out-Null
$asset=$update.GetField('DownloadAsset',$flags).GetValue($null)
if($asset.GetType().GetField('SignatureUrl',$instance).GetValue($asset) -ne ($url+'.sig') -or $asset.GetType().GetField('Version',$instance).GetValue($asset) -ne '9.9.9') { throw 'Signature metadata missing' }
$json=@{tag_name='v9.9.9';body='- test';assets=@(@{browser_download_url=$url;size=$bytes.Length;digest=$digest})}|ConvertTo-Json -Depth 4
$update.GetMethod('ReadRelease',$flags).Invoke($null,@([string]$json)) | Out-Null
$asset=$update.GetField('DownloadAsset',$flags).GetValue($null)
if($asset.GetType().GetField('SignatureUrl',$instance).GetValue($asset)) { throw 'Unsigned release got a signature URL' }
Write-Output 'PASS: Update signatures: valid accepted; other version, other file, garbage, missing and foreign key rejected; .sig found in release metadata.'
Write-Output 'PASS: Download and open failures told apart, unopened installer deleted on close, old update folders cleaned.'
Write-Output 'PASS: Repository URL restrictions, progress, complete/truncated/oversized files, SHA256 mismatch, cancellation, asset metadata, themed error/retry UI. No installer launched.'
