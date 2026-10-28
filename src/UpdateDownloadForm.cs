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
  internal string Url,Digest;
  internal long Size;
 }
 static class UpdateDownload {
  internal static bool ValidUrl(string url) { Uri uri; return Uri.TryCreate(url,UriKind.Absolute,out uri) && uri.Scheme=="https" && uri.Host=="github.com" && uri.IsDefaultPort && String.IsNullOrEmpty(uri.UserInfo) && String.IsNullOrEmpty(uri.Query) && String.IsNullOrEmpty(uri.Fragment) && System.Text.RegularExpressions.Regex.IsMatch(uri.AbsolutePath,@"^/trfurkan/GoodbyeDPI-Tray/releases/download/[^/]+/GoodbyeDPI-Installer[^/]*\.exe$"); }
  internal static void CopyVerified(Stream input,Stream output,long size,string digest,Action<long> progress,CancellationToken token) {
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
    string actual="sha256:"+BitConverter.ToString(sha.Hash).Replace("-","").ToLowerInvariant();
    if(total!=size || (!String.IsNullOrEmpty(digest) && !String.Equals(actual,digest,StringComparison.OrdinalIgnoreCase))) throw new InvalidDataException(L.T("Kurucu dosyası doğrulanamadı."));
   }
  }
  internal static string Download(UpdateAsset asset,IProgress<long> progress,CancellationToken token) {
   if(asset==null || !ValidUrl(asset.Url)) throw new InvalidDataException(L.T("Kurucu bağlantısı geçersiz."));
   string folder=Path.Combine(Path.GetTempPath(),"GoodbyeDPI-update-"+Guid.NewGuid().ToString("N"));
   Directory.CreateDirectory(folder);
   string partial=Path.Combine(folder,"installer.part"),complete=Path.Combine(folder,"GoodbyeDPI-Installer.exe");
   try {
    ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
    var request=(HttpWebRequest)WebRequest.Create(asset.Url); request.UserAgent="GoodbyeDPI-Tray/"+AppInfo.Version; request.Timeout=15000; request.ReadWriteTimeout=15000;
    using(token.Register(request.Abort)) using(var response=(HttpWebResponse)request.GetResponse()) {
     if(response.ResponseUri.Scheme!="https") throw new InvalidDataException(L.T("Kurucu bağlantısı geçersiz."));
     using(var input=response.GetResponseStream()) using(var output=new FileStream(partial,FileMode.CreateNew,FileAccess.Write,FileShare.None)) CopyVerified(input,output,asset.Size,asset.Digest,progress.Report,token);
    }
    token.ThrowIfCancellationRequested(); File.Move(partial,complete); return complete;
   } catch {
    if(File.Exists(partial)) File.Delete(partial);
    if(File.Exists(complete)) File.Delete(complete);
    Directory.Delete(folder); token.ThrowIfCancellationRequested(); throw;
   }
  }
 }
 sealed class UpdateDownloadForm : Form {
  readonly UpdateAsset asset;
  readonly Label status=new Label(),amount=new Label();
  readonly ProgressBar progress=new ProgressBar();
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
   progress.Dock=DockStyle.Fill; progress.Style=ProgressBarStyle.Continuous; layout.Controls.Add(progress);
   amount.Dock=DockStyle.Fill; amount.TextAlign=ContentAlignment.MiddleLeft; layout.Controls.Add(amount);
   var buttons=new FlowLayoutPanel { Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft }; layout.Controls.Add(buttons);
   foreach(var button in new[]{cancel,retry}) { button.Size=new Size(150,38); button.BackColor=BackColor; button.Fill=contrast?SystemColors.Control:AppPalette.Surface(dark); button.Ink=ForeColor; button.Edge=contrast?SystemColors.WindowText:AppPalette.Border(dark); buttons.Controls.Add(button); }
   cancel.Text=L.T("İptal"); retry.Text=L.T("Tekrar dene"); retry.Visible=false;
   cancel.Click+=delegate { Close(); }; retry.Click+=delegate { BeginDownload(); }; CancelButton=cancel;
   FormClosing+=delegate(object sender,FormClosingEventArgs e) { if(active) { e.Cancel=true; closeRequested=true; cancel.Enabled=false; cancellation.Cancel(); } };
   Shown+=delegate { BeginDownload(); }; HandleCreated+=delegate { WindowsIntegration.ThemeCaption(this,dark,BackColor,ForeColor); };
  }
  async void BeginDownload() {
   if(active) return;
   active=true; closeRequested=false; retry.Visible=false; cancel.Text=L.T("İptal"); progress.Value=0; amount.Text=""; status.Text=L.T("Kurucu indiriliyor. Tamamlanınca açılacak.");
   cancellation=new CancellationTokenSource(); string file=null;
   try {
    var reporter=new Progress<long>(delegate(long bytes) { if(IsDisposed || !active) return; progress.Value=(int)Math.Min(100,bytes*100/Math.Max(1,asset.Size)); amount.Text=String.Format("{0:0.0} / {1:0.0} MB · %{2}",bytes/1048576.0,asset.Size/1048576.0,progress.Value); });
    file=await Task.Run(delegate { return UpdateDownload.Download(asset,reporter,cancellation.Token); });
    cancellation.Token.ThrowIfCancellationRequested();
    status.Text=L.T("Kurucu açılıyor…"); cancel.Enabled=false;
    // Windows checks a downloaded file (SmartScreen) before it starts, which can take seconds; keep that off the window's thread.
    string installer=file; await Task.Run(delegate { Process.Start(new ProcessStartInfo(installer) { UseShellExecute=true }).Dispose(); }); file=null; active=false; DialogResult=DialogResult.OK; Close();
   } catch(OperationCanceledException) { active=false; Close(); }
   catch(Exception ex) { AppInfo.Log("Güncelleme indirme: "+ex); status.Text=L.T("İndirme veya kurucu açma başarısız. Tekrar deneyebilirsin."); retry.Visible=true; cancel.Text=L.T("Kapat"); }
   finally {
    active=false; cancellation.Dispose(); cancellation=null;
    if(file!=null) try { File.Delete(file); Directory.Delete(Path.GetDirectoryName(file)); } catch(Exception ex) { AppInfo.Log(ex.Message); }
    if(closeRequested && !IsDisposed) Close();
   }
  }
 }
}
