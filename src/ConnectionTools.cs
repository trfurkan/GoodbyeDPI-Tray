using System;
using System.Drawing;
using System.IO;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GoodbyeTray {
 partial class MainForm {
  readonly SoftButton findProfile=new SoftButton(),pauseButton=new SoftButton();
  readonly ToolStripMenuItem trayPause=new ToolStripMenuItem(L.T("Geçici durdur"));
  readonly ContextMenuStrip pauseMenu=new ContextMenuStrip();
  void ShowPauseMenu() {
   if(busy || !available) return;
   bool paused=EngineConsole.PauseUntil()!=null;
   if(!paused && !running) return;
   var popup=PreparePauseMenu(paused);
   popup.Show(toggle,new Point(0,toggle.Height+6));
  }
  internal ContextMenuStrip PreparePauseMenu(bool paused) {
   var menu=pauseMenu;
   while(menu.Items.Count>0) { var item=menu.Items[0]; menu.Items.RemoveAt(0); item.Dispose(); }
   menu.Items.Add(new ToolStripLabel(L.T(paused?"Molayı yönet":"Geçici durdur")));
   if(paused) {
    menu.Items.Add(L.T("Şimdi devam et"),null,delegate { Execute("start",0); });
    menu.Items.Add(L.T("Molayı iptal et, kapalı kalsın"),null,delegate { Execute("stop",0); });
   } else {
    menu.Items.Add(L.T("5 dakika durdur"),null,delegate { Execute("pause5",0); });
    menu.Items.Add(L.T("15 dakika durdur"),null,delegate { Execute("pause15",0); });
    foreach(ToolStripItem item in menu.Items) item.AccessibleDescription=L.T("Süre bitince kendiliğinden devam eder.");
   }
   // The same look as the other menus: a title (with a muted line on what happens), then rows with a clock icon.
   int width=Math.Max(260,toggle.Width);
   MenuRenderer.Prepare(menu,surface,foreground,border,width);
   menu.Renderer=new PauseMenuRenderer(surface,foreground,border,paused) { Rounded=true,Modern=true };
   foreach(ToolStripItem item in menu.Items) {
    item.Font=Fonts.Get("Segoe UI",10f);
    float f=menu.DeviceDpi/96f; item.Size=new Size(Math.Max(width,(int)Math.Round(width*f))-(int)Math.Round(10*f),(int)Math.Round((item is ToolStripLabel?(paused?34:56):38)*f));
   }
   return menu;
  }
  static void SearchCommand(string action,int profile) {
   Program.RequireOwned();
   if(!UserServiceControl.TryRun(action,profile)) throw new InvalidOperationException(L.T("Profil testi için kurulum dosyasıyla uygulamayı güncelle."));
  }
  static int ProbeProfile() {
   // Exclude adult sites; all targets are named before the user starts the test.
   string[] domains={"discord.com","roblox.com","wattpad.com","google.com"};
   AccessProbe.ForgetCachedAnswers();
   var tasks=new Task<AccessProbe.Result>[domains.Length];
   for(int i=0;i<domains.Length;i++) { string domain=domains[i]; tasks[i]=Task.Run(delegate { return AccessProbe.Run(domain); }); }
   Task.WaitAll(tasks);
   int active=Program.Profile();
   for(int i=0;i<domains.Length;i++) AppInfo.Log("Profil testi: motor="+Program.MotorName(active)+", profil="+active+", site="+domains[i]+", erisim="+tasks[i].Result.Reached+", durum="+tasks[i].Result.Status+", ayrinti="+tasks[i].Result.Detail);
   if(!tasks[3].Result.Reached) return -1;
   int score=0; for(int i=0;i<3;i++) if(tasks[i].Result.Reached) score++;
   return score;
  }
  static void WaitForEngine(int profile) {
   var deadline=DateTime.UtcNow.AddSeconds(20);
   while(DateTime.UtcNow<deadline) {
    try {
     int found; string state,output;
     if(EngineConsoleForm.TryParse(File.ReadAllText(EngineConsole.PathName),out found,out state,out output) && found==profile && state=="running") return;
    } catch(IOException) { }
    Thread.Sleep(200);
   }
   throw new InvalidOperationException(L.T("Motor teste hazır olamadı. Birazdan tekrar dene."));
  }
  void FindProfile() {
   if(busy || !available || transition || EngineConsole.PauseUntil()!=null) return;
   int[] candidates=Program.MotorProfiles(SelectedProfile);
   using(var dialog=new ProfileSearchForm(dark,candidates))
   using(var cancellation=new CancellationTokenSource()) {
    var result=new ProfileSearch.Result();
    dialog.CancelRequested+=delegate { cancellation.Cancel(); };
    dialog.StartRequested+=async delegate {
     if(busy) return;
     bool wasRunning=running; int original=activeProfile;
     stateRevision++; busy=true; SetServiceState(activeProfile,wasRunning?ServiceControllerStatus.Running:ServiceControllerStatus.Stopped);
     dialog.BeginTest();
     var progress=new Progress<Tuple<int,int>>(delegate(Tuple<int,int> value) { dialog.SetProgress(value.Item1,value.Item2); });
     Exception failure=null; bool cancelled=false;
     try {
      await Task.Run(delegate { return ProfileSearch.Run(Program.Args.Length,original,wasRunning,
       delegate(int profile) { if(Program.Profile()!=profile) SearchCommand("profile",profile); },
       delegate(bool on) { SearchCommand(on?"start":"stop",0); if(on) WaitForEngine(Program.Profile()); },
       delegate { ((IProgress<Tuple<int,int>>)progress).Report(Tuple.Create(-1,Program.Profile())); return ProbeProfile(); },
       delegate(int step,int profile) { ((IProgress<Tuple<int,int>>)progress).Report(Tuple.Create(step,profile)); },cancellation.Token,result,candidates); });
     } catch(OperationCanceledException) { cancelled=true; }
     catch(Exception ex) { failure=ex; AppInfo.Log(ex.ToString()); }
     finally { busy=false; if(!IsDisposed) RefreshState(); }
     if(!dialog.IsDisposed) dialog.ShowResults(result,cancelled,failure);
    };
    if(dialog.ShowDialog(this)==DialogResult.OK && result.Profile>=0) Execute("profile",result.Profile);
   }
  }
 }
 // Keep native menu keyboard navigation and dismissal, with a single explanatory header.
 sealed class PauseMenuRenderer : MenuRenderer {
  readonly Color ink,muted,accent;
  readonly bool paused;
  internal PauseMenuRenderer(Color bg,Color fg,Color border,bool isPaused) : base(bg,fg,border) {
   Rounded=true; paused=isPaused; ink=fg;
   bool dark=bg.GetBrightness()<0.5f;
   muted=SystemInformation.HighContrast?fg:AppPalette.Muted(dark);
   accent=SystemInformation.HighContrast?fg:dark?AppPalette.Mint:AppPalette.Accent;
  }
  protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e) {
   if(e.Item is ToolStripLabel) {
    int width=e.Item.Width-24;
    var title=Fonts.Get(e.TextFont,FontStyle.Bold);
    TextRenderer.DrawText(e.Graphics,e.Text,title,new Rectangle(12,6,width,22),ink,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix);
    if(!paused) TextRenderer.DrawText(e.Graphics,L.T("Süre bitince kendiliğinden devam eder."),Fonts.Get(e.TextFont.FontFamily,8.5f),new Rectangle(12,28,width,24),muted,TextFormatFlags.Left|TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix);
    return;
   }
   // Rows: a small clock in the icon column, like the flags and swatches in the other menus.
   DrawModernRow(e,delegate(Graphics g,Rectangle box,Color color) {
    using(var pen=new Pen(SystemInformation.HighContrast?color:accent,1.6f) { StartCap=System.Drawing.Drawing2D.LineCap.Round,EndCap=System.Drawing.Drawing2D.LineCap.Round }) {
     g.DrawEllipse(pen,box.X+1,box.Y+1,box.Width-2,box.Height-2);
     g.DrawLines(pen,new[]{new Point(box.X+9,box.Y+5),new Point(box.X+9,box.Y+9),new Point(box.X+12,box.Y+11)});
    }
   });
  }
 }
}
