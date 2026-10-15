$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[Windows.Forms.Application]::EnableVisualStyles()
$root=Split-Path $PSScriptRoot -Parent
$assembly=[Reflection.Assembly]::LoadFile((Join-Path $root 'dist\GoodbyeDPI-Installer.exe'))
$type=$assembly.GetType('GoodbyeTray.SetupForm')
$flags=[Reflection.BindingFlags]'NonPublic,Instance'
foreach($remove in @($false,$true)) {
 $form=[Activator]::CreateInstance($type,[object[]]@($remove))
 try {
  $form.Show(); [Windows.Forms.Application]::DoEvents()
  if($type.GetField('progressBar',$flags).GetValue($form).Visible -or $type.GetField('percentage',$flags).GetValue($form).Visible){throw 'Progress shown before start'}
  $start=$type.GetField('start',$flags).GetValue($form)
  $consent=$type.GetField('consent',$flags).GetValue($form)
  if(-not $remove) {
   if($start.Enabled -or $consent.Checked){throw 'Consent is not opt-in'}
   $consent.Checked=$true; if(-not $start.Enabled){throw 'Consent did not enable setup'}
   $consent.Checked=$false; if($start.Enabled){throw 'Revoking consent did not disable setup'}
   $consent.Checked=$true
  } elseif(-not $start.Enabled){throw 'Uninstall must not require consent'}
  $type.GetField('busy',$flags).SetValue($form,$true)
  $method=$type.GetMethod('ShowProgress',$flags)
  $method.Invoke($form,[object[]]@(40,'Dosyalar hazirlaniyor...')) | Out-Null
  $method.Invoke($form,[object[]]@(20,'Gecikmis bildirim')) | Out-Null
  $bar=$type.GetField('progressBar',$flags).GetValue($form)
  if(-not $bar.Visible -or -not $type.GetField('percentage',$flags).GetValue($form).Visible){throw 'Progress hidden during operation'}
  if($bar.Value -ne 40){throw 'Yuzde geriye gitti'}
  $type.GetMethod('ShowCompletion',$flags).Invoke($form,[object[]]@($remove,$false)) | Out-Null
  $method.Invoke($form,[object[]]@(95,'Gecikmis bildirim')) | Out-Null
  if($bar.Value -ne 100){throw 'Tamamlanma yuzdesi bozuldu'}
  if($type.GetField('start',$flags).GetValue($form).Visible){throw 'Tamamlanmis kurulum yeniden baslatilabiliyor'}
  if($type.GetField('cancel',$flags).GetValue($form).Text -ne 'Bitir'){throw 'Bitis eylemi eksik'}
  foreach($control in $form.Controls){if($control.Visible -and ($control.Right -gt $form.ClientSize.Width -or $control.Bottom -gt $form.ClientSize.Height)){throw 'Kontrol pencere disinda'}}
  if(-not $remove){$bitmap=[Drawing.Bitmap]::new($form.Width,$form.Height); try{$form.DrawToBitmap($bitmap,[Drawing.Rectangle]::new(0,0,$form.Width,$form.Height)); $bitmap.Save((Join-Path $root 'build\previews\setup-complete.png'))}finally{$bitmap.Dispose()}}
 } finally {$form.Dispose()}
}
Write-Output 'PASS: Kurulum/kaldirma ilerlemesi, yuzdenin gerilememesi, %100 tamamlanma, geciken bildirim ve bitis ekraninin yerlesimi.'