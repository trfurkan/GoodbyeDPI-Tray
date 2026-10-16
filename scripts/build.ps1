$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
$root = Split-Path $PSScriptRoot -Parent
$sourceDir = Join-Path $root 'src'
$icon = Join-Path $root 'assets\app.ico'
if (-not (Test-Path -LiteralPath $icon)) { & (Join-Path $PSScriptRoot 'create-icon.ps1') }
$branding = @("/win32icon:$icon", "/resource:$icon,GoodbyeTray.AppIcon")
$vendorDir = Join-Path $root 'vendor\goodbyedpi'
$appDir = Join-Path $root 'build\app'
New-Item -ItemType Directory -Path $appDir -Force | Out-Null
$shared = @("$sourceDir\AppInfo.cs", "$sourceDir\WindowsIntegration.cs", "$sourceDir\AppCommands.cs", "$sourceDir\EngineService.cs", "$sourceDir\EngineConsoleForm.cs", "$sourceDir\AccessTestForm.cs", "$sourceDir\UpdateCheck.cs", "$sourceDir\LicenseForm.cs", "$sourceDir\Lang.cs", "$sourceDir\LangTable.cs")
$wpf = Join-Path (Split-Path $compiler -Parent) 'WPF'
$references = @("/reference:$wpf\PresentationFramework.dll", "/reference:$wpf\PresentationCore.dll", "/reference:$wpf\WindowsBase.dll")
$output = Join-Path $appDir 'GoodbyeDPI.Tray.exe'
& $compiler /nologo /target:winexe /platform:anycpu /optimize+ @branding @references "/out:$output" "/win32manifest:$sourceDir\app.manifest" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.ServiceProcess.dll /main:GoodbyeTray.Program "$sourceDir\Program.cs" "$sourceDir\MainForm.cs" "$sourceDir\Setup.cs" @shared
if ($LASTEXITCODE -ne 0) { throw 'Derleme başarısız.' }
Copy-Item -LiteralPath (Join-Path $sourceDir 'App.config') -Destination ($output + '.config') -Force
Write-Output "Derlendi: $output"
$root = Split-Path $PSScriptRoot -Parent
$dist = Join-Path $root 'dist'
New-Item -ItemType Directory -Path $dist -Force | Out-Null
$setup = Join-Path $dist 'GoodbyeDPI-Installer.exe'
$resources = @("/resource:$output,payload/GoodbyeDPI.Tray.exe", "/resource:$output.config,payload/GoodbyeDPI.Tray.exe.config")
$resources += "/resource:$root\LICENSE,payload/licenses/LICENSE-goodbyedpi-tray.txt"
$resources += "/resource:$root\NOTICE,payload/licenses/NOTICE-goodbyedpi-tray.txt"
$resources += "/resource:$root\THIRD_PARTY_NOTICES.md,payload/licenses/THIRD_PARTY_NOTICES.md"
foreach ($folder in @('x86','x86_64','licenses')) {
 foreach ($file in Get-ChildItem -LiteralPath (Join-Path $vendorDir $folder) -File) {
  $resources += '/resource:{0},payload/{1}/{2}' -f $file.FullName,$folder,$file.Name
 }
}
& $compiler /nologo /target:winexe /platform:anycpu /optimize+ @branding @references /main:GoodbyeTray.Setup "/out:$setup" "/win32manifest:$sourceDir\app.manifest" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.ServiceProcess.dll @resources "$sourceDir\Program.cs" "$sourceDir\MainForm.cs" "$sourceDir\Setup.cs" @shared
if ($LASTEXITCODE -ne 0) { throw 'Kurulum paketi derlenemedi.' }
Get-FileHash -LiteralPath $setup -Algorithm SHA256 | ForEach-Object { '{0}  GoodbyeDPI-Installer.exe' -f $_.Hash } | Set-Content -LiteralPath (Join-Path $dist 'SHA256.txt')
Write-Output "Tek dosya kurucu: $setup"
