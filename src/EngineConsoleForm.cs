using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GoodbyeTray {
 // The engine's real console output, always live, in a window like "Sorun bildir": a title, one line with the state
 // and a copy button, then the text in a dark terminal card.
 sealed class EngineConsoleForm : Form {
  readonly Label state=new Label();
  // Same view as the installer's notice: a thin thumb instead of Windows scroll bars.
  readonly NoticeView terminal=new NoticeView();
  readonly SoftPanel card=new SoftPanel();
  readonly SoftButton copy=new SoftButton();
  readonly Panel dot=new Panel();
  readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();
  bool reading,disposed;
  string lastFrame; bool lastRunning;
  Color tone=Color.Gray;
  internal EngineConsoleForm(bool dark) {
   Text=L.T("GoodbyeDPI — Motor konsolu"); Icon=Program.AppIcon; Font=new Font(Fonts.Ui,10); AutoScaleMode=AutoScaleMode.Dpi;
   ClientSize=new Size(820,580); MinimumSize=new Size(560,420); StartPosition=FormStartPosition.CenterParent; MinimizeBox=true; MaximizeBox=true; ShowInTaskbar=false;
   var layout=new TableLayoutPanel { Dock=DockStyle.Fill,Padding=new Padding(24),ColumnCount=1,RowCount=3 };
   layout.RowStyles.Add(new RowStyle(SizeType.Absolute,38)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute,46)); layout.RowStyles.Add(new RowStyle(SizeType.Percent,100)); Controls.Add(layout);
   layout.Controls.Add(DialogKit.Title(L.T("Motor konsolu")),0,0);
   var header=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=3,RowCount=1,Margin=Padding.Empty };
   header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,24)); header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,150));
   header.RowStyles.Add(new RowStyle(SizeType.Percent,100)); layout.Controls.Add(header,0,1);
   // The state dot: green while running, amber while starting, switching or on a break, grey when stopped.
   dot.Dock=DockStyle.Fill; dot.Margin=Padding.Empty; dot.Paint+=delegate(object sender,PaintEventArgs e) { e.Graphics.SmoothingMode=SmoothingMode.AntiAlias; int d=10; using(var halo=new SolidBrush(Color.FromArgb(60,tone))) e.Graphics.FillEllipse(halo,1,(dot.Height-d-6)/2,d+6,d+6); using(var brush=new SolidBrush(tone)) e.Graphics.FillEllipse(brush,4,(dot.Height-d)/2,d,d); };
   header.Controls.Add(dot,0,0);
   state.Dock=DockStyle.Fill; state.Text=L.T("Konsol çıktısı okunuyor…"); state.AutoEllipsis=true; state.TextAlign=ContentAlignment.MiddleLeft; state.UseMnemonic=false; header.Controls.Add(state,1,0);
   copy.Text=L.T("Çıktıyı kopyala"); copy.Glyph="ch:"; copy.Dock=DockStyle.Fill; copy.Margin=new Padding(8,3,0,5); copy.Corner=10; header.Controls.Add(copy,2,0);
   copy.Click+=delegate { if(terminal.Text.Length==0) return; try { Clipboard.SetText(terminal.Text); copy.Flash(); } catch(System.Runtime.InteropServices.ExternalException) { state.Text=L.T("Kopyalanamadı, bir daha dene."); } };
   card.Dock=DockStyle.Fill; card.Margin=new Padding(0,6,0,0); card.Padding=new Padding(16,14,8,14); layout.Controls.Add(card,0,2);
   terminal.Dock=DockStyle.Fill; terminal.Font=new Font("Consolas",10); terminal.TabIndex=10; terminal.FocusFrame=false; terminal.AccessibleName=L.T("GoodbyeDPI motorunun gerçek konsol çıktısı"); card.Controls.Add(terminal);
   timer.Interval=500; timer.Tick+=delegate { ReadFrame(); };
   Shown+=delegate { ReadFrame(); timer.Start(); };
   ApplyPalette(dark); Motion.FadeIn(this);
  }
  internal void ApplyPalette(bool dark) {
   bool contrast=SystemInformation.HighContrast;
   BackColor=AppPalette.Background(dark); ForeColor=AppPalette.Text(dark); state.ForeColor=ForeColor; dot.BackColor=BackColor;
   // The terminal stays dark in every theme, like a real console; dark themes use their own deepest tone.
   Color screen=dark?AppPalette.Console(true):Color.FromArgb(17,19,24);
   card.BackColor=BackColor; card.Fill=screen; card.Edge=dark?AppPalette.Border(true):screen;
   terminal.BackColor=screen; terminal.ForeColor=Color.FromArgb(220,231,247); terminal.EdgeColor=screen; terminal.ThumbColor=Color.FromArgb(90,100,120);
   copy.BackColor=BackColor; copy.Fill=AppPalette.Surface(dark); copy.Ink=ForeColor; copy.Edge=AppPalette.Border(dark);
   if(contrast) { BackColor=dot.BackColor=card.BackColor=card.Fill=terminal.BackColor=copy.BackColor=copy.Fill=SystemColors.Window; ForeColor=state.ForeColor=terminal.ForeColor=copy.Ink=SystemColors.WindowText; card.Edge=terminal.EdgeColor=copy.Edge=SystemColors.WindowText; }
   card.Invalidate(); terminal.Invalidate(); copy.Invalidate(); dot.Invalidate();
   foreach(Control row in Controls) foreach(Control child in row.Controls) if(child is Label && child!=state) child.ForeColor=ForeColor;
   WindowsIntegration.ThemeCaption(this,dark,BackColor,ForeColor);
   if(!IsHandleCreated) HandleCreated+=delegate { WindowsIntegration.ThemeCaption(this,dark,BackColor,ForeColor); };
  }
  protected override bool ProcessCmdKey(ref Message msg,Keys keyData) { if(keyData==Keys.Escape) { Close(); return true; } return base.ProcessCmdKey(ref msg,keyData); }
  internal static bool TryParse(string frame,out int profile,out string status,out string output) {
   profile=-1; status=""; output=""; if(String.IsNullOrEmpty(frame)) return false;
   int newline=frame.IndexOf('\n'); if(newline<0) return false;
   string[] header=frame.Substring(0,newline).TrimEnd('\r').Split('|');
   int pid; if(header.Length!=4 || header[0]!="GDC1" || !Int32.TryParse(header[1],out profile) || profile<0 || profile>=Program.Names.Length || !Int32.TryParse(header[3],out pid)) return false;
   status=header[2]; if(status!="starting" && status!="switching" && status!="running" && status!="stopped" && status!="exited" && status!="paused") return false;
   output=frame.Substring(newline+1); return true;
  }
  void SetTone(Color value) { if(tone==value) return; tone=value; dot.Invalidate(); }
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
    if(disposed || IsDisposed || (frame==lastFrame && serviceRunning==lastRunning)) return;
    if(frame==null) { state.Text=L.T("Henüz çıktı yok. Motoru başlatınca burada görünecek."); SetTone(AppPalette.Muted(BackColor.GetBrightness()<0.5f)); return; }
    int profile; string status,output; if(!TryParse(frame,out profile,out status,out output)) return;
    lastFrame=frame; lastRunning=serviceRunning;
    if(status=="paused") output="";
    if(!serviceRunning && (status=="running" || status=="starting" || status=="switching" || status=="paused")) status="stopped";
    Text=Program.MotorName(profile)+" — "+Program.Names[profile]+L.T(" — Konsol");
    state.Text=Program.Names[profile]+"  ·  "+(status=="switching"?L.T("Profil değişiyor, motorun hazır olmasını bekliyoruz…"):status=="paused"?L.T("GoodbyeDPI molada. Süre bitince geri gelecek."):status=="running"?L.T("Canlı çıktı · Bu pencereyi kapatmak motoru durdurmaz."):status=="starting"?L.T("Motor başlatılıyor…"):L.T("Motor çalışmıyor. Son oturumun çıktısı aşağıda."));
    SetTone(status=="running"?StatusLabel.Green:status=="stopped" || status=="exited"?AppPalette.Muted(BackColor.GetBrightness()<0.5f):Color.FromArgb(217,119,6));
    if(output!=terminal.Text) { terminal.Text=output; terminal.ScrollToEnd(); }
   } catch(IOException) { } catch(UnauthorizedAccessException) { if(!disposed) state.Text=L.T("Çıktı okunamadı. Kurulum dosyasıyla güncellemeyi dene."); }
   finally { reading=false; }
  }
  protected override void Dispose(bool disposing) { if(disposing && !disposed) { disposed=true; timer.Dispose(); } base.Dispose(disposing); }
 }
}
