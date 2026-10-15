param([string]$RestoreSnapshot)
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Start-Transcript -LiteralPath (Join-Path $root 'build/lifecycle-transcript.txt') -Force
try { & (Join-Path $PSScriptRoot 'test-install-lifecycle.ps1') -RestoreSnapshot $RestoreSnapshot } finally { Stop-Transcript }
