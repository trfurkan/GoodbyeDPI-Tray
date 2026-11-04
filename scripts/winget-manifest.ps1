# Writes the winget manifests for a published release into build\winget\<version>.
# The installer is downloaded from the GitHub release so the SHA256 matches the file users get.
# Usage: powershell -NoProfile -ExecutionPolicy Bypass -File scripts\winget-manifest.ps1 -Version 1.8.1
param([Parameter(Mandatory=$true)][string]$Version)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$id = 'trfurkan.GoodbyeDPITray'
$repo = 'https://github.com/trfurkan/GoodbyeDPI-Tray'
$url = "$repo/releases/download/v$Version/GoodbyeDPI-Installer-$Version.exe"
$out = Join-Path $root "build\winget\$Version"
New-Item -ItemType Directory -Path $out -Force | Out-Null

[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
$release = Invoke-RestMethod "https://api.github.com/repos/trfurkan/GoodbyeDPI-Tray/releases/tags/v$Version" -Headers @{ 'User-Agent' = 'GoodbyeDPI-Tray-winget' }
$installer = Join-Path $out 'installer.exe'
Invoke-WebRequest $url -OutFile $installer -UseBasicParsing
$sha = (Get-FileHash $installer -Algorithm SHA256).Hash
Remove-Item $installer
$date = ([datetime]$release.published_at).ToString('yyyy-MM-dd')

$schema = '1.12.0'
# Each file starts with the schema line winget-pkgs expects, then the package identity.
function Header([string]$type) { "# yaml-language-server: `$schema=https://aka.ms/winget-manifest.$type.$schema.schema.json`n# Created with scripts/winget-manifest.ps1`n`nPackageIdentifier: $id`nPackageVersion: $Version" }
$files = @{
 "$id.yaml" = @"
$(Header version)
DefaultLocale: tr-TR
ManifestType: version
ManifestVersion: $schema
"@
 "$id.installer.yaml" = @"
$(Header installer)
InstallerType: exe
Scope: machine
ElevationRequirement: elevatesSelf
InstallModes:
- silent
- silentWithProgress
InstallerSwitches:
  Silent: --silent
  SilentWithProgress: --silent
  InstallLocation: --location "<INSTALLPATH>"
ExpectedReturnCodes:
- InstallerReturnCode: 1602
  ReturnResponse: cancelledByUser
- InstallerReturnCode: 3010
  ReturnResponse: rebootRequiredToFinish
UpgradeBehavior: install
ProductCode: GoodbyeDPITray
ReleaseDate: $date
AppsAndFeaturesEntries:
- DisplayName: GoodbyeDPI Tray
  Publisher: Furkan
  ProductCode: GoodbyeDPITray
Installers:
- Architecture: neutral
  InstallerUrl: $url
  InstallerSha256: $sha
ManifestType: installer
ManifestVersion: $schema
"@
 "$id.locale.tr-TR.yaml" = @"
$(Header defaultLocale)
PackageLocale: tr-TR
Publisher: Furkan
PublisherUrl: https://github.com/trfurkan
PublisherSupportUrl: $repo/issues
PackageName: GoodbyeDPI Tray
PackageUrl: https://trfurkan.github.io/GoodbyeDPI-Tray/
License: Apache-2.0
LicenseUrl: $repo/blob/main/LICENSE
ShortDescription: Discord ve erişimi engellenen sitelere VPN kullanmadan girmeni sağlayan GoodbyeDPI tepsi uygulaması.
Description: GoodbyeDPI-Turkey profillerini komut satırı olmadan başlatır, durdurur ve Windows'la birlikte çalıştırır. Başlatma, durdurma ve profil değiştirme yönetici izni istemez.
Tags:
- dpi
- goodbyedpi
- discord
- tray
- turkey
ReleaseNotesUrl: $repo/releases/tag/v$Version
ManifestType: defaultLocale
ManifestVersion: $schema
"@
 "$id.locale.en-US.yaml" = @"
$(Header locale)
PackageLocale: en-US
Publisher: Furkan
PublisherUrl: https://github.com/trfurkan
PublisherSupportUrl: $repo/issues
PackageName: GoodbyeDPI Tray
PackageUrl: https://trfurkan.github.io/GoodbyeDPI-Tray/en/
License: Apache-2.0
LicenseUrl: $repo/blob/main/LICENSE
ShortDescription: A system tray app for GoodbyeDPI that opens Discord and blocked sites without a VPN.
Description: Starts, stops and runs GoodbyeDPI-Turkey profiles with Windows, no command line needed. Starting, stopping and switching profiles do not ask for administrator rights.
Tags:
- dpi
- goodbyedpi
- discord
- tray
- turkey
ReleaseNotesUrl: $repo/releases/tag/v$Version
ManifestType: locale
ManifestVersion: $schema
"@
}
$utf8 = New-Object Text.UTF8Encoding $false
foreach ($name in $files.Keys) { [IO.File]::WriteAllText((Join-Path $out $name), $files[$name].Replace("`r`n", "`n") + "`n", $utf8) }
Write-Output "winget dosyaları: $out"
if (Get-Command winget -ErrorAction SilentlyContinue) { winget validate --manifest $out }
