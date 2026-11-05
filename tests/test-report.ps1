$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
$root=Split-Path $PSScriptRoot -Parent
# Checks the problem report and the "only blocked sites" setting without network requests or system changes.
$asm=[Reflection.Assembly]::LoadFile((Join-Path $root 'build\app\GoodbyeDPI.Tray.exe'))
$asm.GetType('GoodbyeTray.L').GetProperty('Current',[Reflection.BindingFlags]'NonPublic,Static').SetValue($null,'tr')
$static=[Reflection.BindingFlags]'NonPublic,Static'; $instance=[Reflection.BindingFlags]'NonPublic,Instance'
$report=$asm.GetType('GoodbyeTray.ProblemReport')
$clean=$report.GetMethod('Clean',$static).Invoke($null,@("C:\Users\$env:USERNAME\AppData\x $env:USERNAME"))
if($env:USERNAME.Length -gt 2 -and $clean.Contains($env:USERNAME)) { throw "Rapor kullanici adini iceriyor: $clean" }
$details=$report.GetMethod('Details',$static).Invoke($null,@('AS0 Test'))
foreach($text in '- Sürüm: ','- Profil: ','- DNS: ','- QUIC engeli: ','- Yalnızca engelli siteler: ','- Operatör: AS0 Test') { if(-not $details.Contains($text)) { throw "Raporda eksik: $text" } }
$body=$report.GetMethod('Body',$static).Invoke($null,@('Discord acilmiyor',$details))
if(-not $body.StartsWith("### Ne oldu?`n`nDiscord acilmiyor")) { throw 'Rapor govdesi yanlis' }
$link=$report.GetMethod('Link',$static).Invoke($null,@($report.GetMethod('Title',$static).Invoke($null,@('Discord acilmiyor')),$body))
if(-not $link.StartsWith('https://github.com/trfurkan/GoodbyeDPI-Tray/issues/new?labels=bug&title=Discord%20acilmiyor&body=')) { throw "Baglanti yanlis: $($link.Substring(0,90))" }
if($link.Length -gt 8000) { throw "Baglanti cok uzun: $($link.Length)" }
if($report.GetMethod('Title',$static).Invoke($null,@('  ')) -ne 'Sorun bildirimi') { throw 'Bos baslik' }
$form=[Activator]::CreateInstance($asm.GetType('GoodbyeTray.ReportForm'),$instance,$null,[object[]]@($false),$null)
try { if($form.Text -ne 'Dert yan') { throw 'Pencere basligi yanlis' } } finally { $form.Dispose() }
$sites=$asm.GetType('GoodbyeTray.SiteList')
$hosts=$sites.GetField('Hosts',$static).GetValue($null)
foreach($h in 'discord.com','discord.gg','discordapp.com','discord.media','roblox.com','rbxcdn.com','wattpad.com') { if($hosts -notcontains $h) { throw "Site listesinde yok: $h" } }
$uses=$sites.GetMethod('Uses',$static)
if(-not $uses.Invoke($null,@(0)) -or $uses.Invoke($null,@(7))) { throw 'Site listesi yalnizca GoodbyeDPI profillerine uygulanmali' }
if(-not (Test-Path 'HKLM:\SOFTWARE\GoodbyeDPITray') -or (Get-ItemProperty 'HKLM:\SOFTWARE\GoodbyeDPITray' -Name SitesOnly -ErrorAction SilentlyContinue) -eq $null) {
 if($sites.GetProperty('Wanted',$static).GetValue($null)) { throw 'Site listesi varsayilan olarak acik' }
 if($sites.GetMethod('EngineArgs',$static).Invoke($null,@(0,'-5')) -ne '-5') { throw 'Kapaliyken motor argumani degisti' }
}
# Site names: what people paste is cleaned up; anything that is not a plain host name is refused.
$normalize=$sites.GetMethod('Normalize',$static)
$cases=@{ 'https://www.Example.com/path?q=1'='example.com'; 'pastebin.com'='pastebin.com'; 'a-b.co.uk.'='a-b.co.uk'; 'xn--bcher-kva.example'='xn--bcher-kva.example'; 'site.com:8080'='site.com' }
foreach($case in $cases.GetEnumerator()) { $got=$normalize.Invoke($null,@($case.Key)); if($got -ne $case.Value) { throw "Site adi yanlis temizlendi: $($case.Key) -> $got" } }
foreach($bad in 'localhost','abc','site com','site.com" --x','-bad.com','bad-.com','a..com','site.c',"site.com`nx.com",'..','',('a'*101)+'.com','ş.com') { if($normalize.Invoke($null,@($bad)) -ne $null) { throw "Gecersiz site kabul edildi: $bad" } }
$refused=$false; try { $sites.GetMethod('Add',$static).Invoke($null,@('bad site')) | Out-Null } catch { $refused=$_.Exception.InnerException -is [ArgumentException] }
if(-not $refused) { throw 'Hizmet gecersiz siteyi reddetmedi' }
$form=[Activator]::CreateInstance($asm.GetType('GoodbyeTray.SiteListForm'),$instance,$null,[object[]]@($false),$null)
try {
 $calls=0; $form.GetType().GetField('Edit',$instance).SetValue($form,[Func[bool,string,bool]]{ param($a,$h) $script:calls++; $true })
 $defaults=$form.GetType().GetField('defaults',$instance).GetValue($form)
 if($defaults.Controls.Count -ne $hosts.Count) { throw 'Hazir siteler listelenmedi' }
 $form.GetType().GetField('input',$instance).GetValue($form).Text='bad site'
 $form.GetType().GetMethod('Change',$instance).Invoke($form,@($true,'bad site')) | Out-Null
 $msg=$form.GetType().GetField('message',$instance).GetValue($form).Text
 if($calls -ne 0 -or $msg -notlike '*site adresi*') { throw "Gecersiz site hizmete gitti ya da uyari yok: $msg" }
} finally { $form.Dispose() }
$main=$asm.GetType('GoodbyeTray.MainForm'); $window=[Activator]::CreateInstance($main,[object[]]@($false))
try { $box=$main.GetField('sitesOnly',$instance).GetValue($window); if($box.Text -ne 'Sadece engelli sitelere karış' -or $box.Parent -eq $null) { throw 'Site ayari arayuzde yok' } } finally { $window.Dispose() }
Write-Output 'PASS: Sorun raporu bilgileri, kullanici adi gizleme, GitHub baglantisi; site listesi yalnizca GoodbyeDPI profillerinde ve varsayilan kapali. Ag istegi yapilmadi.'
