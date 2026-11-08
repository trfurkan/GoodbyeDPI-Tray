$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Add-Type -AssemblyName System.Windows.Forms
$a=[Reflection.Assembly]::LoadFile((Join-Path $root 'build/app/GoodbyeDPI.Tray.exe'))
$s=[Reflection.BindingFlags]'NonPublic,Static'; $f=[Reflection.BindingFlags]'NonPublic,Instance'
$a.GetType('GoodbyeTray.L').GetProperty('Current',$s).SetValue($null,'en')
$probe=$a.GetType('GoodbyeTray.AccessProbe')
$cases=@{Timeout='Timed out';NameResolutionFailure='DNS error';TrustFailure='Certificate error';SecureChannelFailure='TLS error';ConnectFailure='Connection error';ReceiveFailure='Connection error'}
foreach($name in $cases.Keys) {
 $result=$probe.GetMethod('Failure',$s).Invoke($null,[object[]]@([Net.WebExceptionStatus]::$name))
 $actual=$result.GetType().GetField('Status',$f).GetValue($result)
 if($actual -ne $cases[$name]) { throw "Wrong status for ${name}: $actual" }
}
$ct=$a.GetType('GoodbyeTray.AccessContext'); $ctx=[Activator]::CreateInstance($ct,$true)
$ct.GetField('Filtered',$f).SetValue($ctx,$true); $ct.GetField('Hosts',$f).SetValue($ctx,[string[]]@('discord.com'))
foreach($domain in @('discord.com','cdn.discord.com')) { if(-not $ct.GetMethod('Covers',$f).Invoke($ctx,@($domain))) { throw 'Listed domain excluded' } }
foreach($domain in @('notdiscord.com','discord.com.evil.test','pastebin.com')) { if($ct.GetMethod('Covers',$f).Invoke($ctx,@($domain))) { throw 'Unlisted domain included' } }
$ct.GetField('Filtered',$f).SetValue($ctx,$false)
if(-not $ct.GetMethod('Covers',$f).Invoke($ctx,@('pastebin.com'))) { throw 'Disabled filter restricted domain' }
$t=$a.GetType('GoodbyeTray.MainForm'); $form=[Activator]::CreateInstance($t,[object[]]@($false))
try {
 $a.GetType('GoodbyeTray.Motion').GetField('Override',$s).SetValue($null,$false)
 $form.Show()
 # A finished quick test: its summary, and the mark that a test ran (only then does a change ask for a retest).
 $t.GetField('quickSummary',$f).SetValue($form,'Quick test 3/3 old'); $t.GetField('accessTested',$f).SetValue($form,$true)
 $t.GetMethod('ToggleAccess',$f).Invoke($form,$null) | Out-Null
 $full=$t.GetField('accessFull',$f).GetValue($form); $ft=$full.GetType()
 $ft.GetMethod('SetSummary',$f,$null,[Type[]]@([string]),$null).Invoke($full,@('6/10 new')) | Out-Null
 $label=$t.GetField('accessSummary',$f).GetValue($form)
 if($label.Text -notlike '*6/10*' -or $label.Text -like '*3/3*') { throw 'Detailed view shows old quick summary' }
 $t.GetMethod('ToggleAccess',$f).Invoke($form,$null) | Out-Null
 if($label.Text -notlike '*Quick test 3/3*') { throw 'Quick scope lost' }
 $before=$ft.GetField('generation',$f).GetValue($full)
 $t.GetMethod('InvalidateAccessResults',$f).Invoke($form,$null) | Out-Null
 if($ft.GetField('generation',$f).GetValue($full) -le $before) { throw 'In-flight results not invalidated' }
 foreach($row in $ft.GetField('rows',$f).GetValue($full)) { if($row.GetType().GetField('State',$f).GetValue($row).ToString() -ne 'Idle') { throw 'Stale detailed row retained' } }
 if($label.Text -notlike '*Settings changed*') { throw 'Retest message missing' }
 $before=$t.GetField('accessGeneration',$f).GetValue($form)
 $t.GetField('accessContextKey',$f).SetValue($form,'previous settings')
 $t.GetMethod('CheckAccessContext',$f).Invoke($form,$null) | Out-Null
 if($t.GetField('accessGeneration',$f).GetValue($form) -le $before) { throw 'Context change ignored' }
} finally { $form.Dispose() }
'PASS: Error classification, list boundaries, separate summary scopes, stale row reset and generation invalidation; no network/service changes.'