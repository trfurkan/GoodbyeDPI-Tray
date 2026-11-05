using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GoodbyeTray {
 sealed class UpdateAsset {
  internal string Url,Digest,Version,SignatureUrl;
  internal long Size;
 }
 static class UpdateDownload {
  internal static bool ValidUrl(string url) { Uri uri; return Uri.TryCreate(url,UriKind.Absolute,out uri) && uri.Scheme=="https" && uri.Host=="github.com" && uri.IsDefaultPort && String.IsNullOrEmpty(uri.UserInfo) && String.IsNullOrEmpty(uri.Query) && String.IsNullOrEmpty(uri.Fragment) && System.Text.RegularExpressions.Regex.IsMatch(uri.AbsolutePath,@"^/trfurkan/GoodbyeDPI-Tray/releases/download/[^/]+/GoodbyeDPI-Installer[^/]*\.exe$"); }
  // Copies the download while hashing it and returns the SHA256 as lowercase hex, which the signature check then uses.
  internal static string CopyVerified(Stream input,Stream output,long size,string digest,Action<long> progress,CancellationToken token) {
   if(size<=0 || size>200*1024*1024) throw new InvalidDataException(L.T("Kurucu dosyası doğrulanamadı."));
   using(var sha=SHA256.Create()) {
    byte[] buffer=new byte[65536]; long total=0; int count,reported=-1;
    while((count=input.Read(buffer,0,buffer.Length))>0) {
     token.ThrowIfCancellationRequested(); total+=count;
     if(total>size) throw new InvalidDataException(L.T("Kurucu dosyası doğrulanamadı."));
     sha.TransformBlock(buffer,0,count,null,0); output.Write(buffer,0,count);
     // One report per percent: a report per buffer floods the window with thousands of queued redraws.
     int percent=(int)(total*100/size); if(percent!=reported) { reported=percent; progress(total); }
    }
    token.ThrowIfCancellationRequested(); sha.TransformFinalBlock(new byte[0],0,0);
    string hex=BitConverter.ToString(sha.Hash).Replace("-","").ToLowerInvariant();
    if(total!=size || (!String.IsNullOrEmpty(digest) && !String.Equals("sha256:"+hex,digest,StringComparison.OrdinalIgnoreCase))) throw new InvalidDataException(L.T("Kurucu dosyası doğrulanamadı."));
    return hex;
   }
  }
  internal static string Download(UpdateAsset asset,IProgress<long> progress,CancellationToken token) {
   if(asset==null || !ValidUrl(asset.Url)) throw new InvalidDataException(L.T("Kurucu bağlantısı geçersiz."));
   string folder=Path.Combine(Path.GetTempPath(),FolderPrefix+Guid.NewGuid().ToString("N"));
   Directory.CreateDirectory(folder);
   string partial=Path.Combine(folder,"installer.part"),complete=Path.Combine(folder,"GoodbyeDPI-Installer.exe"),hash;
   try {
    ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
    var request=(HttpWebRequest)WebRequest.Create(asset.Url); request.UserAgent="GoodbyeDPI-Tray/"+AppInfo.Version; request.Timeout=15000; request.ReadWriteTimeout=15000;
    using(token.Register(request.Abort)) using(var response=(HttpWebResponse)request.GetResponse()) {
     if(response.ResponseUri.Scheme!="https") throw new InvalidDataException(L.T("Kurucu bağlantısı geçersiz."));
     using(var input=response.GetResponseStream()) using(var output=new FileStream(partial,FileMode.CreateNew,FileAccess.Write,FileShare.None)) hash=CopyVerified(input,output,asset.Size,asset.Digest,progress.Report,token);
    }
    token.ThrowIfCancellationRequested();
    // Only a file signed with the developer's key becomes an installer; without a valid signature it is deleted below.
    if(asset.SignatureUrl==null || !UpdateSignature.Verify(asset.Version,hash,UpdateSignature.Download(asset.SignatureUrl))) throw new UpdateSignatureException();
    token.ThrowIfCancellationRequested(); File.Move(partial,complete); return complete;
   } catch {
    // Cleanup failures are only logged, so the original error is the one reported.
    Discard(folder); token.ThrowIfCancellationRequested(); throw;
   }
  }
  internal const string FolderPrefix="GoodbyeDPI-update-";
  internal static void Discard(string folder) {
   try { if(Directory.Exists(folder)) Directory.Delete(folder,true); } catch(Exception ex) { AppInfo.Log("Güncelleme klasörü silinemedi: "+ex.Message); }
  }
  // Installers from earlier updates; one still running is locked and simply waits for the next start.
  internal static void CleanOld() {
   try { foreach(string folder in Directory.GetDirectories(Path.GetTempPath(),FolderPrefix+"*")) try { Directory.Delete(folder,true); } catch(IOException) { } catch(UnauthorizedAccessException) { } }
   catch(Exception ex) { AppInfo.Log("Eski güncelleme klasörleri: "+ex.Message); }
  }
 }
 sealed class UpdateDownloadForm : Form {
  readonly UpdateAsset asset;
  readonly Label status=new Label(),amount=new Label();
  readonly PaletteProgressBar progress=new PaletteProgressBar();
  readonly SoftButton cancel=new SoftButton(),retry=new SoftButton();
  CancellationTokenSource cancellation;
  bool active,closeRequested;
  internal UpdateDownloadForm(bool dark,UpdateAsset download) {
   asset=download; Text=L.T("Güncelleme indiriliyor"); Icon=Program.AppIcon; Font=new Font("Segoe UI",10); AutoScaleMode=AutoScaleMode.Dpi;
   ClientSize=new Size(540,260); FormBorderStyle=FormBorderStyle.FixedDialog; MaximizeBox=false; MinimizeBox=false; StartPosition=FormStartPosition.CenterParent;
   bool contrast=SystemInformation.HighContrast; BackColor=contrast?SystemColors.Window:AppPalette.Background(dark); ForeColor=contrast?SystemColors.WindowText:AppPalette.Text(dark);
   var layout=new TableLayoutPanel { Dock=DockStyle.Fill,Padding=new Padding(24),ColumnCount=1,RowCount=4 };
   layout.RowStyles.Add(new RowStyle(SizeType.Percent,100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute,28)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute,32)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute,48)); Controls.Add(layout);
   status.Dock=DockStyle.Fill; status.Text=L.T("İndirme hazırlanıyor…"); layout.Controls.Add(status);
   progress.Dock=DockStyle.Top; progress.Height=8; progress.Margin=new Padding(0,10,0,0); progress.Style=ProgressBarStyle.Continuous; progress.BackColor=BackColor; progress.ForeColor=contrast?SystemColors.Highlight:AppPalette.Accent; progress.Track=contrast?SystemColors.GrayText:AppPalette.Border(dark); layout.Controls.Add(progress);
   amount.Dock=DockStyle.Fill; amount.TextAlign=ContentAlignment.MiddleLeft; layout.Controls.Add(amount);
   var buttons=new FlowLayoutPanel { Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft }; layout.Controls.Add(buttons);
   foreach(var button in new[]{cancel,retry}) { button.Size=new Size(150,38); button.BackColor=BackColor; button.Fill=contrast?SystemColors.Control:AppPalette.Surface(dark); button.Ink=ForeColor; button.Edge=contrast?SystemColors.WindowText:AppPalette.Border(dark); buttons.Controls.Add(button); }
   cancel.Text=L.T("İptal"); retry.Text=L.T("Tekrar dene"); retry.Visible=false;
   cancel.Click+=delegate { Close(); }; retry.Click+=delegate { BeginDownload(); }; CancelButton=cancel;
   FormClosing+=delegate(object sender,FormClosingEventArgs e) { if(active) { e.Cancel=true; closeRequested=true; cancel.Enabled=false; cancellation.Cancel(); } };
   Shown+=delegate { BeginDownload(); }; HandleCreated+=delegate { WindowsIntegration.ThemeCaption(this,dark,BackColor,ForeColor); };
  }
  // A verified installer that has not been opened yet: "Tekrar dene" opens it again instead of downloading it again.
  string downloaded;
  bool launched;
  async void BeginDownload() {
   if(active) return;
   active=true; closeRequested=false; retry.Visible=false; cancel.Text=L.T("İptal"); cancel.Enabled=true;
   cancellation=new CancellationTokenSource();
   try {
    if(downloaded==null) {
     progress.Value=0; amount.Text=""; status.Text=L.T("Kurucu indiriliyor. Tamamlanınca açılacak.");
     var reporter=new Progress<long>(delegate(long bytes) { if(IsDisposed || !active) return; progress.Value=(int)Math.Min(100,bytes*100/Math.Max(1,asset.Size)); amount.Text=String.Format("{0:0.0} / {1:0.0} MB · %{2}",bytes/1048576.0,asset.Size/1048576.0,progress.Value); });
     downloaded=await Task.Run(delegate { return UpdateDownload.Download(asset,reporter,cancellation.Token); });
     cancellation.Token.ThrowIfCancellationRequested();
    }
    status.Text=L.T("Kurucu açılıyor…"); cancel.Enabled=false;
    // Windows checks a downloaded file (SmartScreen) before it starts, which can take seconds; keep that off the window's thread.
    string installer=downloaded; await Task.Run(delegate { using(Process.Start(new ProcessStartInfo(installer) { UseShellExecute=true })) { } });
    launched=true; active=false; DialogResult=DialogResult.OK; Close();
   } catch(OperationCanceledException) { active=false; Close(); }
   catch(Exception ex) {
    AppInfo.Log("Güncelleme indirme: "+ex);
    var refused=ex as System.ComponentModel.Win32Exception;
    // 1223: the Windows warning (SmartScreen or UAC) was answered with "don't run"; the download itself is fine.
    // A bad signature is not a network problem: trying again would give the same file, so no retry is offered.
    if(ex is UpdateSignatureException) { status.Text=L.T("Güncellemenin imzası doğrulanamadı, güvenliğin için kurulmadı. Yeni sürümü GitHub sayfasından indirebilirsin."); cancel.Text=L.T("Kapat"); return; }
    status.Text=refused!=null && refused.NativeErrorCode==1223 && downloaded!=null?L.T("Kurucu açılmadı. Tekrar dene'ye basınca yeniden indirmeden açılır."):downloaded!=null?L.T("Kurucu açılamadı. Tekrar deneyebilirsin."):L.T("İndirme başarısız. Tekrar deneyebilirsin.");
    retry.Visible=true; cancel.Text=L.T("Kapat");
   }
   finally {
    active=false; cancel.Enabled=true; cancellation.Dispose(); cancellation=null;
    if(closeRequested && !IsDisposed) Close();
   }
  }
  protected override void OnFormClosed(FormClosedEventArgs e) {
   // An installer that never opened is not needed any more; an opened one is cleaned up at a later start.
   if(downloaded!=null && !launched) UpdateDownload.Discard(Path.GetDirectoryName(downloaded));
   base.OnFormClosed(e);
  }
 }
}
