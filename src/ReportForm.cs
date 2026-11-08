using System;
using System.Drawing;
using System.IO;
using System.Net;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GoodbyeTray {
 // Collects what a bug report needs and opens a filled-in GitHub issue. The user sees everything before it is
 // sent and GitHub shows it again before submitting; nothing is sent from the app itself.
 static class ProblemReport {
  internal const string IssueUrl="https://github.com/trfurkan/GoodbyeDPI-Tray/issues/new";
  // GitHub refuses very long addresses; logs are cut so the whole link stays well under its limit.
  const int MaxLog=2500;
  internal static string Operator() {
   try {
    var request=(HttpWebRequest)WebRequest.Create("https://ipinfo.io/org");
    request.Timeout=request.ReadWriteTimeout=4000; request.UserAgent="GoodbyeDPI-Tray/"+AppInfo.Version;
    using(var response=request.GetResponse()) using(var reader=new StreamReader(response.GetResponseStream(),Encoding.UTF8)) {
     string org=reader.ReadToEnd().Trim();
     return org.Length>0 && org.Length<120?org:"bilinmiyor";
    }
   } catch(Exception ex) { AppInfo.Log("Operatör bilgisi: "+ex.Message); return "bilinmiyor"; }
  }
  static string Safe(Func<string> read) { try { return read(); } catch(Exception ex) { return "okunamadı ("+ex.Message+")"; } }
  // Folders carry the Windows user name; replace it so the report does not.
  internal static string Clean(string text) {
   if(String.IsNullOrEmpty(text)) return text;
   string user=Environment.UserName, home=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
   if(!String.IsNullOrEmpty(home)) text=text.Replace(home,"%USERPROFILE%");
   if(user.Length>2) text=text.Replace(user,"<kullanıcı>");
   return text;
  }
  static string Tail(string text,int max) {
   if(String.IsNullOrEmpty(text)) return "";
   text=text.TrimEnd();
   if(text.Length<=max) return text;
   int cut=text.IndexOf('\n',text.Length-max); return "…"+text.Substring(cut<0?text.Length-max:cut);
  }
  static string EngineOutput() {
   if(!File.Exists(EngineConsole.PathName)) return "";
   using(var stream=new FileStream(EngineConsole.PathName,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete)) using(var reader=new StreamReader(stream,Encoding.UTF8,true)) {
    int profile; string state,output; return EngineConsoleForm.TryParse(reader.ReadToEnd(),out profile,out state,out output)?output:"";
   }
  }
  static string AppLog() { return File.Exists(AppInfo.LogPath)?File.ReadAllText(AppInfo.LogPath):""; }
  // The facts as a Markdown list; read off the UI thread because it asks Windows and the network.
  internal static string Details(string carrier) {
   var text=new StringBuilder();
   int profile=-1; try { profile=Program.Profile(); } catch(Exception) {}
   string state=Safe(delegate {
    if(profile<0) return "kurulu değil";
    if(EngineConsole.PauseUntil()!=null) return "molada";
    using(var service=new ServiceController(Program.ServiceName)) return service.Status==ServiceControllerStatus.Running?"çalışıyor":service.Status==ServiceControllerStatus.Stopped?"durduruldu":service.Status.ToString();
   });
   text.AppendLine("- Sürüm: "+AppInfo.Version+(Program.ZapretEnabled?" (Zapret dahil)":""));
   text.AppendLine("- Windows: "+Environment.OSVersion.Version+(Environment.Is64BitOperatingSystem?" (64 bit)":" (32 bit)"));
   text.AppendLine("- Dil: "+L.Current);
   text.AppendLine("- Profil: "+(profile>=0?Program.Names[profile]:"yok"));
   text.AppendLine("- Durum: "+state);
   text.AppendLine("- DNS: "+Safe(delegate { return DnsChoice.Names[DnsChoice.Selected]; }));
   text.AppendLine("- QUIC engeli: "+Safe(delegate { return QuicBlock.Wanted?"açık":"kapalı"; }));
   text.AppendLine("- Yalnızca engelli siteler: "+Safe(delegate { return SiteList.Wanted?"açık":"kapalı"; }));
   text.AppendLine("- Operatör: "+carrier);
   string engine=Tail(Safe(EngineOutput),MaxLog), log=Tail(Safe(AppLog),MaxLog);
   if(engine.Length>0) text.AppendLine().AppendLine("<details><summary>Motor çıktısı</summary>").AppendLine().AppendLine("```text").AppendLine(engine).AppendLine("```").AppendLine("</details>");
   if(log.Length>0) text.AppendLine().AppendLine("<details><summary>Uygulama kaydı</summary>").AppendLine().AppendLine("```text").AppendLine(log).AppendLine("```").AppendLine("</details>");
   return Clean(text.ToString());
  }
  internal static string Body(string what,string details) {
   return "### Ne oldu?\n\n"+(String.IsNullOrWhiteSpace(what)?"_(Buraya yaz: ne yapmaya çalıştın, ne oldu?)_":what.Trim())+"\n\n### Bilgiler\n\n"+details.Replace("\r\n","\n");
  }
  internal static string Link(string title,string body) {
   return IssueUrl+"?labels=bug&title="+Uri.EscapeDataString(title)+"&body="+Uri.EscapeDataString(body);
  }
  internal static string Title(string what) {
   string line=String.IsNullOrWhiteSpace(what)?"":what.Trim().Split('\n')[0].Trim();
   if(line.Length>70) line=line.Substring(0,70).TrimEnd()+"…";
   return line.Length>0?line:"Sorun bildirimi";
  }
 }
 sealed class ReportForm : Form {
  readonly TextBox what=new TextBox(), details=new TextBox();
  readonly SoftButton open=new SoftButton(), copy=new SoftButton(), close=new SoftButton();
  readonly Label state=new Label();
  bool ready;
  internal ReportForm(bool dark) {
   bool contrast=SystemInformation.HighContrast;
   Color background=contrast?SystemColors.Window:AppPalette.Background(dark),surface=contrast?SystemColors.Window:AppPalette.Surface(dark),ink=contrast?SystemColors.WindowText:AppPalette.Text(dark),muted=contrast?ink:AppPalette.Muted(dark),edge=contrast?ink:AppPalette.Border(dark),accent=contrast?SystemColors.Highlight:AppPalette.Accent;
   BackColor=background; ForeColor=ink; Font=new Font("Segoe UI",10); AutoScaleMode=AutoScaleMode.Dpi; Icon=Program.AppIcon;
   Text=L.T("Sorun bildir"); ClientSize=new Size(660,600); MinimumSize=new Size(560,520); StartPosition=FormStartPosition.CenterParent; MinimizeBox=false; MaximizeBox=false;
   var layout=new TableLayoutPanel { Dock=DockStyle.Fill,Padding=new Padding(24),ColumnCount=1,RowCount=7 };
   foreach(int h in new[]{46,48,26}) layout.RowStyles.Add(new RowStyle(SizeType.Absolute,h));
   layout.RowStyles.Add(new RowStyle(SizeType.Percent,40)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute,32)); layout.RowStyles.Add(new RowStyle(SizeType.Percent,60)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute,60)); Controls.Add(layout);
   layout.Controls.Add(new Label { Text=L.T("Sorun bildir"),Dock=DockStyle.Fill,Font=Fonts.Serif(22),AutoEllipsis=true });
   layout.Controls.Add(new Label { Text=L.T("Ne olduğunu yaz, gerisini biz ekleyelim. GitHub'da bir hata kaydı açılır; göndermeden önce orada da görüp düzenleyebilirsin."),Dock=DockStyle.Fill,ForeColor=muted });
   layout.Controls.Add(new Label { Text=L.T("Ne oldu?"),Dock=DockStyle.Fill,Font=new Font(Font.FontFamily,10,FontStyle.Bold),TextAlign=ContentAlignment.BottomLeft });
   what.Multiline=true; what.ScrollBars=ScrollBars.Vertical; what.AcceptsReturn=true; what.Dock=DockStyle.Fill; what.BackColor=surface; what.ForeColor=ink; what.BorderStyle=BorderStyle.FixedSingle; what.AccessibleName=L.T("Ne oldu?"); layout.Controls.Add(what);
   state.Text=L.T("Bilgiler toplanıyor…"); state.Dock=DockStyle.Fill; state.Font=new Font(Font.FontFamily,10,FontStyle.Bold); state.TextAlign=ContentAlignment.BottomLeft; layout.Controls.Add(state);
   details.Multiline=true; details.ReadOnly=true; details.ScrollBars=ScrollBars.Vertical; details.WordWrap=true; details.Dock=DockStyle.Fill; details.BackColor=surface; details.ForeColor=muted; details.BorderStyle=BorderStyle.FixedSingle; details.Font=new Font("Consolas",9); details.AccessibleName=L.T("Eklenecek bilgiler"); layout.Controls.Add(details);
   var actions=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=4,RowCount=1,Margin=Padding.Empty };
   actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,100)); actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,110)); actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,168)); layout.Controls.Add(actions);
   close.Text=L.T("Kapat"); copy.Text=L.T("Kopyala"); open.Text=L.T("GitHub'da aç");
   foreach(var button in new[]{close,copy,open}) { button.Dock=DockStyle.Fill; button.Margin=new Padding(8,10,0,4); button.BackColor=background; button.Fill=button==open?accent:surface; button.Ink=button==open?(contrast?SystemColors.HighlightText:AppPalette.AccentInk):ink; button.Edge=button==open?accent:edge; }
   actions.Controls.Add(close,1,0); actions.Controls.Add(copy,2,0); actions.Controls.Add(open,3,0);
   open.Enabled=copy.Enabled=false; CancelButton=close;
   close.Click+=delegate { Close(); };
   copy.Click+=delegate { try { Clipboard.SetText(ProblemReport.Body(what.Text,details.Text)); state.Text=L.T("Panoya kopyalandı."); } catch(Exception ex) { AppInfo.Log(ex.ToString()); } };
   open.Click+=delegate { Open(); };
   Shown+=delegate { what.Focus(); Collect(); };
   HandleCreated+=delegate { WindowsIntegration.ThemeCaption(this,dark,BackColor,ForeColor); };
  }
  internal string Details { get { return details.Text; } }
  async void Collect() {
   try {
    string text=await Task.Run(delegate { return ProblemReport.Details(ProblemReport.Operator()); });
    if(IsDisposed) return;
    details.Text=text.Replace("\r\n","\n").Replace("\n","\r\n"); details.SelectionStart=0; details.ScrollToCaret(); state.Text=L.T("Eklenecek bilgiler"); ready=true; open.Enabled=copy.Enabled=true;
   } catch(Exception ex) { AppInfo.Log(ex.ToString()); if(!IsDisposed) state.Text=L.T("Bilgiler toplanamadı: ")+ex.Message; }
  }
  void Open() {
   if(!ready) return;
   string url=ProblemReport.Link(ProblemReport.Title(what.Text),ProblemReport.Body(what.Text,details.Text));
   try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute=true }); Close(); }
   catch(Exception ex) {
    AppInfo.Log(ex.ToString());
    try { Clipboard.SetText(ProblemReport.Body(what.Text,details.Text)); } catch(Exception) {}
    MessageBox.Show(this,L.T("Tarayıcı açılamadı. Bilgileri panoya kopyaladık; şu adreste yeni bir kayıt açıp yapıştırabilirsin:\n")+ProblemReport.IssueUrl,L.T("Sorun bildir"),MessageBoxButtons.OK,MessageBoxIcon.Information);
   }
  }
 }
}
