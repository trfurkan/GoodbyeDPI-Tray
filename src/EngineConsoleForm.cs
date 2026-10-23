using System;
using System.Drawing;
using System.IO;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GoodbyeTray {
 sealed class EngineConsoleForm : Form {
  readonly Label caption=new Label(),state=new Label();
  // Same view as the installer's notice: a thin thumb instead of Windows scroll bars.
  readonly NoticeView terminal=new NoticeView();
  readonly CheckBox follow=new CheckBox();
  readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();
  bool reading,disposed;
  string lastFrame; bool lastRunning;
  internal EngineConsoleForm(bool dark) {
   Text=L.T("GoodbyeDPI — Motor konsolu"); Icon=Program.AppIcon; Font=new Font("Segoe UI",10); ClientSize=new Size(860,480); MinimumSize=new Size(540,300); StartPosition=FormStartPosition.CenterParent;
   var layout=new TableLayoutPanel { Dock=DockStyle.Fill,Padding=new Padding(16),ColumnCount=1,RowCount=4 };
   layout.RowStyles.Add(new RowStyle(SizeType.Absolute,30)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute,36)); layout.RowStyles.Add(new RowStyle(SizeType.Percent,100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute,44)); Controls.Add(layout);
   caption.Dock=DockStyle.Fill; caption.Font=new Font(Font.FontFamily,13,FontStyle.Bold); caption.Text=L.T("Motor çıktısı"); layout.Controls.Add(caption);
   state.Dock=DockStyle.Fill; state.Text=L.T("Konsol çıktısı okunuyor…"); layout.Controls.Add(state);
   terminal.Dock=DockStyle.Fill; terminal.Font=new Font("Consolas",10); terminal.TabIndex=10; terminal.FocusFrame=false; terminal.AccessibleName=L.T("GoodbyeDPI motorunun gerçek konsol çıktısı"); layout.Controls.Add(terminal);
   var tools=new FlowLayoutPanel { Dock=DockStyle.Fill,FlowDirection=FlowDirection.LeftToRight,Padding=new Padding(0,6,0,0) }; layout.Controls.Add(tools);
   follow.Text=L.T("Canlı takip"); follow.Checked=true; follow.AutoSize=true; follow.Margin=new Padding(0,8,18,0); tools.Controls.Add(follow);
   var copy=new SoftButton { Text=L.T("Çıktıyı kopyala"),Width=150,Height=32 }; copy.Click+=delegate { if(terminal.Text.Length>0) try { Clipboard.SetText(terminal.Text); } catch(System.Runtime.InteropServices.ExternalException) { state.Text=L.T("Kopyalanamadı, bir daha dene."); } }; tools.Controls.Add(copy);
   timer.Interval=500; timer.Tick+=delegate { if(follow.Checked) ReadFrame(); };
   Shown+=delegate { ReadFrame(); timer.Start(); }; follow.CheckedChanged+=delegate { if(follow.Checked) { lastFrame=null; ReadFrame(); } };
   ApplyPalette(dark);
  }
  internal void ApplyPalette(bool dark) {
   BackColor=AppPalette.Background(dark); ForeColor=AppPalette.Text(dark); caption.ForeColor=ForeColor; state.ForeColor=AppPalette.Muted(dark);
   terminal.BackColor=AppPalette.Console(dark); terminal.ForeColor=dark?Color.FromArgb(220,231,247):Color.FromArgb(22,36,62);
   terminal.EdgeColor=AppPalette.Border(dark); terminal.ThumbColor=AppPalette.Muted(dark); terminal.Invalidate();
   foreach(Control row in Controls) foreach(Control child in row.Controls) if(child is FlowLayoutPanel) foreach(Control tool in child.Controls) { tool.ForeColor=ForeColor; var button=tool as SoftButton; if(button!=null) { button.BackColor=BackColor; button.Fill=AppPalette.Surface(dark); button.Ink=ForeColor; button.Edge=AppPalette.Border(dark); button.Invalidate(); } }
   if(SystemInformation.HighContrast) { BackColor=terminal.BackColor=SystemColors.Window; ForeColor=caption.ForeColor=state.ForeColor=terminal.ForeColor=SystemColors.WindowText; }
   WindowsIntegration.ThemeCaption(this,dark,BackColor,ForeColor);
   if(!IsHandleCreated) HandleCreated+=delegate { WindowsIntegration.ThemeCaption(this,dark,BackColor,ForeColor); };
  }
  internal static bool TryParse(string frame,out int profile,out string status,out string output) {
   profile=-1; status=""; output=""; if(String.IsNullOrEmpty(frame)) return false;
   int newline=frame.IndexOf('\n'); if(newline<0) return false;
   string[] header=frame.Substring(0,newline).TrimEnd('\r').Split('|');
   int pid; if(header.Length!=4 || header[0]!="GDC1" || !Int32.TryParse(header[1],out profile) || profile<0 || profile>=Program.Names.Length || !Int32.TryParse(header[3],out pid)) return false;
   status=header[2]; if(status!="starting" && status!="switching" && status!="running" && status!="stopped" && status!="exited" && status!="paused") return false;
   output=frame.Substring(newline+1); return true;
  }
  async void ReadFrame() {
   if(reading || disposed) return; reading=true;
   try {
    bool serviceRunning=false;
    string frame=await Task.Run(delegate {
     if(!File.Exists(EngineConsole.PathName)) return null;
     try { using(var service=new ServiceController(Program.ServiceName)) serviceRunning=service.Status==ServiceControllerStatus.Running || service.Status==ServiceControllerStatus.StartPending; } catch(InvalidOperationException) { }
     using(var stream=new FileStream(EngineConsole.PathName,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete)) {
      if(stream.Length>512*1024) throw new IOException(L.T("Konsol kaydı çok büyük."));
      using(var reader=new StreamReader(stream,Encoding.UTF8,true)) return reader.ReadToEnd();
     }
    });
    if(disposed || IsDisposed || !follow.Checked || (frame==lastFrame && serviceRunning==lastRunning)) return;
    if(frame==null) { state.Text=L.T("Henüz çıktı yok. Motoru başlatınca burada görünecek."); return; }
    int profile; string status,output; if(!TryParse(frame,out profile,out status,out output)) return;
    lastFrame=frame; lastRunning=serviceRunning;
    if(status=="paused") output="";
    if(!serviceRunning && (status=="running" || status=="starting" || status=="switching" || status=="paused")) status="stopped";
    caption.Text=(status=="stopped" || status=="exited"?L.T("Son Oturum Günlüğü — "):L.T("Etkin profil: "))+Program.Names[profile];
    Text=Program.MotorName(profile)+" — "+Program.Names[profile]+L.T(" — Konsol");
    state.Text=status=="switching"?L.T("Profil değişiyor, motorun hazır olmasını bekliyoruz…"):status=="paused"?L.T("GoodbyeDPI molada. Süre bitince geri gelecek."):status=="running"?L.T("Canlı çıktı · Bu pencereyi kapatmak motoru durdurmaz."):status=="starting"?L.T("Motor başlatılıyor…"):"";
    if(output!=terminal.Text) { terminal.Text=output; terminal.ScrollToEnd(); }
   } catch(IOException) { } catch(UnauthorizedAccessException) { if(!disposed) state.Text=L.T("Çıktı okunamadı. Kurulum dosyasıyla güncellemeyi dene."); }
   finally { reading=false; }
  }
  protected override void Dispose(bool disposing) { if(disposing && !disposed) { disposed=true; timer.Dispose(); } base.Dispose(disposing); }
 }
}
