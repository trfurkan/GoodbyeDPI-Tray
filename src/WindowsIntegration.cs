using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows.Shell;
namespace GoodbyeTray {
 class GitHubProfileLink : LinkLabel {
  public GitHubProfileLink() { Padding=new Padding(28,0,0,0); }
  protected override void OnPaint(PaintEventArgs e) {
   base.OnPaint(e);
   var saved=e.Graphics.Save(); e.Graphics.TranslateTransform(1,(Height-20)/2f); e.Graphics.ScaleTransform(20f/24,20f/24); e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
   Color background=Parent==null?SystemColors.Window:Parent.BackColor; for(Control host=Parent;host!=null;host=host.Parent) if(host.BackColor.A==255) { background=host.BackColor; break; }
   using(var ink=new SolidBrush(LinkColor)) using(var cutout=new SolidBrush(background)) {
    e.Graphics.FillEllipse(ink,0,0,24,24);
    using(var cat=new System.Drawing.Drawing2D.GraphicsPath()) {
     cat.AddBezier(5,10,4,7,5,4,6,4); cat.AddLine(6,4,10,6); cat.AddBezier(10,6,11,5,13,5,14,6); cat.AddLine(14,6,18,4); cat.AddBezier(18,4,19,5,20,8,19,10);
     cat.AddBezier(19,10,21,16,17,18,14,18); cat.AddBezier(14,18,16,19,16,22,16,24); cat.AddLine(16,24,9,24); cat.AddLine(9,24,9,20);
     cat.AddBezier(9,20,5,22,4,17,2,17); cat.AddBezier(2,17,5,16,5,20,9,18); cat.AddBezier(9,18,4,18,3,13,5,10); cat.CloseFigure(); e.Graphics.FillPath(cutout,cat);
    }
   }
   e.Graphics.Restore(saved);
  }
 }
 static class ProjectLinks {
  internal static LinkLabel Create(string text,string url,Color color,bool githubIcon=false) {
   LinkLabel link=githubIcon?(LinkLabel)new GitHubProfileLink():new LinkLabel();
   link.Text=(githubIcon?"":"↗  ")+text; link.AutoSize=false; link.Height=32; link.LinkColor=link.ActiveLinkColor=link.VisitedLinkColor=color; link.LinkBehavior=LinkBehavior.HoverUnderline; link.AccessibleName=text+" — tarayıcıda aç"; link.Tag=url; link.TextAlign=ContentAlignment.MiddleLeft;
   link.LinkClicked+=delegate { try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute=true }); } catch(Exception ex) { AppInfo.Log(ex.ToString()); MessageBox.Show(link.FindForm(),"Bağlantı açılamadı. Adresi tarayıcına yazabilirsin:\n"+url,"Bağlantı",MessageBoxButtons.OK,MessageBoxIcon.Information); } }; return link;
  }
 }
 static class AppPalette {
  internal static readonly Color Accent=Color.FromArgb(20,78,224), Mint=Color.FromArgb(127,174,255), Danger=Color.FromArgb(159,57,65);
  internal static Color Background(bool dark) { return dark?Color.FromArgb(19,27,45):Color.FromArgb(239,244,252); }
  internal static Color Surface(bool dark) { return dark?Color.FromArgb(29,40,62):Color.FromArgb(248,250,255); }
  internal static Color Text(bool dark) { return dark?Color.FromArgb(237,243,255):Color.FromArgb(13,31,69); }
  internal static Color Muted(bool dark) { return dark?Color.FromArgb(165,183,214):Color.FromArgb(80,99,132); }
  internal static Color Border(bool dark) { return dark?Color.FromArgb(54,72,103):Color.FromArgb(206,218,238); }
  internal static Color Selection(bool dark) { return dark?Color.FromArgb(37,62,109):Color.FromArgb(226,237,255); }
 }
 static class WindowsIntegration {
  const string RunKey=@"Software\Microsoft\Windows\CurrentVersion\Run", ApprovedKey=@"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run", TrayValue="GoodbyeDPITray";
  // Task Manager and Settings record a disabled startup entry under StartupApproved; it outlives the Run value, so the Run value alone does not mean Windows will start the tray.
  internal static bool TrayStartupEnabled() {
   using(var run=Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey)) if(run==null || run.GetValue(TrayValue)==null) return false;
   using(var approved=Microsoft.Win32.Registry.CurrentUser.OpenSubKey(ApprovedKey)) { var state=approved==null?null:approved.GetValue(TrayValue) as byte[]; return state==null || state.Length==0 || (state[0]&1)==0; }
  }
  // Turning startup off keeps the Run entry and marks it disabled the same way Task Manager does, so both show the same state.
  internal static void SetTrayStartup(bool enabled) {
   using(var run=Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RunKey)) run.SetValue(TrayValue,"\""+System.IO.Path.Combine(Program.InstallDir,"GoodbyeDPI.Tray.exe")+"\" --tray");
   using(var approved=Microsoft.Win32.Registry.CurrentUser.CreateSubKey(ApprovedKey)) {
    if(enabled) approved.DeleteValue(TrayValue,false);
    else { var state=new byte[12]; state[0]=3; BitConverter.GetBytes(DateTime.UtcNow.ToFileTimeUtc()).CopyTo(state,4); approved.SetValue(TrayValue,state,Microsoft.Win32.RegistryValueKind.Binary); }
   }
  }
  internal static void RemoveTrayStartup() {
   using(var run=Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey,true)) if(run!=null) run.DeleteValue(TrayValue,false);
   using(var approved=Microsoft.Win32.Registry.CurrentUser.OpenSubKey(ApprovedKey,true)) if(approved!=null) approved.DeleteValue(TrayValue,false);
  }
  [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);
  [DllImport("dwmapi.dll")] internal static extern int DwmGetWindowAttribute(IntPtr hwnd,int attribute,out int value,int size);
  internal static int LastCaptionResult;
  internal static string ShellStatus="Henüz oluşturulmadı";
  internal static void ThemeCaption(Form form,bool dark,Color background,Color foreground) {
   if(!form.IsHandleCreated) return;
   try {
    int mode=dark && !SystemInformation.HighContrast?1:0;
    int corners=2; DwmSetWindowAttribute(form.Handle,33,ref corners,4);
    LastCaptionResult=DwmSetWindowAttribute(form.Handle,20,ref mode,4);
    // Color attributes are supported on Windows 11. Older Windows returns
    // E_INVALIDARG; keep its native frame rather than replace the title bar.
    int caption=SystemInformation.HighContrast?-1:ColorTranslator.ToWin32(background);
    int text=SystemInformation.HighContrast?-1:ColorTranslator.ToWin32(foreground);
    int border=-1;
    DwmSetWindowAttribute(form.Handle,35,ref caption,4);
    DwmSetWindowAttribute(form.Handle,36,ref text,4);
    DwmSetWindowAttribute(form.Handle,34,ref border,4);
   } catch(DllNotFoundException) { LastCaptionResult=-1; } catch(EntryPointNotFoundException) { LastCaptionResult=-1; }
  }
  internal static readonly string[] Actions={"show","start","stop","restart","profiles","settings","exit"};
  internal static readonly string[] Titles={"Uygulamayı aç","GoodbyeDPI'ı başlat","GoodbyeDPI'ı durdur","GoodbyeDPI'ı yeniden başlat","Profil seçimi","Ayarlar","Arayüzden çık"};
  internal static JumpList BuildJumpList(string executable) {
   var list=new JumpList(); list.ShowRecentCategory=false; list.ShowFrequentCategory=false;
   for(int i=0;i<Actions.Length;i++) list.JumpItems.Add(new JumpTask { Title=Titles[i],Description=Titles[i],ApplicationPath=executable,Arguments="--action "+Actions[i],WorkingDirectory=System.IO.Path.GetDirectoryName(executable),IconResourcePath=executable,IconResourceIndex=0 });
   return list;
  }
  internal static void RegisterJumpList() {
   try {
    var list=BuildJumpList(Application.ExecutablePath);
    int rejected=0; list.JumpItemsRejected+=delegate(object sender,JumpItemsRejectedEventArgs e) { rejected+=e.RejectedItems.Count; };
    list.Apply(); ShellStatus=rejected==0?"7 görev kaydedildi":rejected+" görev Windows tarafından reddedildi";
   } catch(Exception ex) { ShellStatus="Kullanılamıyor: "+ex.Message; AppInfo.Log("Jump List: "+ex.Message); }
  }
 }
 // One shared renderer keeps profile, theme and tray menus consistent.
 class MenuRenderer : ToolStripProfessionalRenderer {
  readonly Color background,foreground,selection,accent,border;
  internal MenuRenderer(Color bg,Color fg,Color selected) { background=bg; foreground=fg; bool dark=bg.GetBrightness()<0.5f; selection=SystemInformation.HighContrast?SystemColors.Highlight:AppPalette.Selection(dark); accent=SystemInformation.HighContrast?SystemColors.HighlightText:dark?AppPalette.Mint:AppPalette.Accent; border=SystemInformation.HighContrast?SystemColors.WindowText:AppPalette.Border(dark); RoundedEdges=false; }
  protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e) { e.Graphics.Clear(background); }
  protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { using(var brush=new SolidBrush(background)) e.Graphics.FillRectangle(brush,e.AffectedBounds); }
  protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e) { using(var pen=new Pen(border)) e.Graphics.DrawRectangle(pen,0,0,e.ToolStrip.Width-1,e.ToolStrip.Height-1); }
  protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e) { var item=e.Item as ToolStripMenuItem; bool active=e.Item.Enabled && (e.Item.Selected || (item!=null && item.Checked)); if(active) { e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias; using(var path=SoftButton.Round(new Rectangle(3,1,e.Item.Width-6,e.Item.Height-2),10)) using(var brush=new SolidBrush(selection)) e.Graphics.FillPath(brush,path); } }
  protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e) { e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias; var r=e.ImageRectangle; using(var pen=new Pen(accent,2)) e.Graphics.DrawLines(pen,new[]{new Point(r.Left+3,r.Top+r.Height/2),new Point(r.Left+7,r.Top+r.Height/2+4),new Point(r.Right-2,r.Top+3)}); }
  protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e) { e.ArrowColor=foreground; base.OnRenderArrow(e); }
  protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e) { using(var pen=new Pen(border)) e.Graphics.DrawLine(pen,12,e.Item.Height/2,e.Item.Width-12,e.Item.Height/2); }
  protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e) { var item=e.Item as ToolStripMenuItem; e.TextColor=!e.Item.Enabled?SystemColors.GrayText:SystemInformation.HighContrast && e.Item.Selected?SystemColors.HighlightText:item!=null && item.Checked?accent:foreground; base.OnRenderItemText(e); }
 }
}
