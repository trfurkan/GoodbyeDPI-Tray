# Signs a release installer so the app's updater accepts it. Writes <installer>.sig next to the file; upload it to the
# release together with the installer. The private key lives only in the developer's profile, never in this repository.
# First time:  powershell -NoProfile -ExecutionPolicy Bypass -File scripts\sign-release.ps1 -NewKey
#   creates the key and prints the public key for src\UpdateSignature.cs. Back the key file up somewhere safe:
#   without it no update can be signed, and installed apps will refuse unsigned updates.
# Each release: powershell -NoProfile -ExecutionPolicy Bypass -File scripts\sign-release.ps1 -File dist\GoodbyeDPI-Installer-1.9.0.exe -Version 1.9.0
param([string]$File,[string]$Version,[switch]$NewKey,[string]$KeyPath=(Join-Path $env:USERPROFILE '.goodbyedpi-tray\update-signing-key.xml'))
$ErrorActionPreference='Stop'
if($NewKey) {
 if(Test-Path -LiteralPath $KeyPath) { throw "Anahtar zaten var: $KeyPath. Üzerine yazılmadı." }
 $folder=Split-Path $KeyPath -Parent
 New-Item -ItemType Directory -Path $folder -Force | Out-Null
 # Only the current user can read the folder.
 & icacls.exe $folder /inheritance:r /grant:r "$($env:USERNAME):(OI)(CI)F" | Out-Null
 $rsa=New-Object Security.Cryptography.RSACryptoServiceProvider 3072
 try {
  [IO.File]::WriteAllText($KeyPath,$rsa.ToXmlString($true),(New-Object Text.UTF8Encoding $false))
  Write-Output "Anahtar oluşturuldu: $KeyPath"
  Write-Output "Uygulamaya gömülecek açık anahtar:"
  Write-Output $rsa.ToXmlString($false)
 } finally { $rsa.Dispose() }
 return
}
if(-not $File -or -not $Version) { throw '-File ve -Version gerekli.' }
if($Version -notmatch '^[0-9]+(\.[0-9]+){1,3}$') { throw "Geçersiz sürüm: $Version" }
if(-not (Test-Path -LiteralPath $KeyPath)) { throw "İmzalama anahtarı bulunamadı: $KeyPath" }
$hash=(Get-FileHash -LiteralPath $File -Algorithm SHA256).Hash.ToLowerInvariant()
# Same message the app builds in UpdateSignature.Message: the version is signed too, so an older signed installer cannot pose as a newer one.
$message=[Text.Encoding]::UTF8.GetBytes("GoodbyeDPI-Tray update`n$Version`n$hash")
$rsa=New-Object Security.Cryptography.RSACryptoServiceProvider
try {
 $rsa.FromXmlString([IO.File]::ReadAllText($KeyPath))
 $signature=$rsa.SignData($message,[Security.Cryptography.HashAlgorithmName]::SHA256,[Security.Cryptography.RSASignaturePadding]::Pkcs1)
} finally { $rsa.Dispose() }
$out=$File+'.sig'
[IO.File]::WriteAllText($out,[Convert]::ToBase64String($signature),[Text.Encoding]::ASCII)
Write-Output "İmza yazıldı: $out"
