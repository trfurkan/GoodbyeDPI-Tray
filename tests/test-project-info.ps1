$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[Windows.Forms.Application]::EnableVisualStyles()
$root=Split-Path $PSScriptRoot -Parent
$asm=[Reflection.Assembly]::LoadFile((Join-Path $root 'dist\GoodbyeDPI-Installer.exe'))
$type=$asm.GetType('GoodbyeTray.MainForm'); $flags=[Reflection.BindingFlags]'NonPublic,Instance'
$form=[Activator]::CreateInstance($type,[object[]]@($false))
try {
 $footer=$type.GetField('footerPanel',$flags).GetValue($form)
 if($footer.Controls.Count -ne 2){throw 'Unexpected footer content'}
 $profile=@($footer.Controls | Where-Object {$_.Text -eq 'Geliştirici: trfurkan'})
 if($profile.Count -ne 1 -or $profile[0].Tag -ne 'https://github.com/trfurkan' -or $profile[0].GetType().Name -ne 'GitHubProfileLink'){throw 'Profile link or icon missing'}
 if(@($footer.Controls | Where-Object {$_.Text -eq 'Hakkında'}).Count -ne 1){throw 'About link missing'}
 $dialog=$type.GetMethod('CreateAboutDialog',$flags).Invoke($form,$null)
 try {
  $dialog.Show(); [Windows.Forms.Application]::DoEvents()
  $links=@($dialog.Controls | Where-Object {$_ -is [Windows.Forms.LinkLabel]})
  if($links.Count -ne 2 -or $links.Tag -notcontains 'https://github.com/trfurkan' -or $links.Tag -notcontains 'https://github.com/cagritaskn/GoodbyeDPI-Turkey'){throw 'Wrong project URLs'}
  $bitmap=[Drawing.Bitmap]::new($dialog.Width,$dialog.Height)
  try{$dialog.DrawToBitmap($bitmap,[Drawing.Rectangle]::new(0,0,$dialog.Width,$dialog.Height)); $bitmap.Save((Join-Path $root 'build\previews\about-links.png'))}finally{$bitmap.Dispose()}
 }finally{$dialog.Dispose()}
}finally{$form.Dispose()}
$setup=[Activator]::CreateInstance($asm.GetType('GoodbyeTray.SetupForm'),[object[]]@($false))
try{$setup.Show(); [Windows.Forms.Application]::DoEvents(); $bitmap=[Drawing.Bitmap]::new($setup.Width,$setup.Height); try{$setup.DrawToBitmap($bitmap,[Drawing.Rectangle]::new(0,0,$setup.Width,$setup.Height)); $bitmap.Save((Join-Path $root 'build\previews\setup-consent.png'))}finally{$bitmap.Dispose()}}finally{$setup.Dispose()}
Write-Output 'PASS: About link destinations and setup consent preview. Browser navigation was not performed.'
$noticeType=$asm.GetType('GoodbyeTray.NoticeView')
$notice=[Activator]::CreateInstance($noticeType)
try {
 $notice.Size=[Drawing.Size]::new(260,100); $notice.Text=('Long notice text to verify scrolling. ' * 60)
 $key=$noticeType.GetMethod('OnKeyDown',$flags)
 $key.Invoke($notice,[object[]]@([Windows.Forms.KeyEventArgs]::new([Windows.Forms.Keys]::End)))|Out-Null
 $offset=$noticeType.GetField('offset',$flags)
 if($offset.GetValue($notice) -le 0){throw 'End key did not scroll'}
 $key.Invoke($notice,[object[]]@([Windows.Forms.KeyEventArgs]::new([Windows.Forms.Keys]::Home)))|Out-Null
 if($offset.GetValue($notice) -ne 0){throw 'Home key did not reset scroll'}
 $noticeType.GetMethod('OnMouseWheel',$flags).Invoke($notice,[object[]]@([Windows.Forms.MouseEventArgs]::new([Windows.Forms.MouseButtons]::None,0,10,10,-120)))|Out-Null
 if($offset.GetValue($notice) -le 0){throw 'Mouse wheel did not scroll'}
 $notice.Text='Short notice'; if($noticeType.GetProperty('Limit',$flags).GetValue($notice,$null) -ne 0){throw 'Scrollbar shown unnecessarily'}
}finally{$notice.Dispose()}
Write-Output 'PASS: Notice keyboard/wheel scrolling and no overflow for short text.'