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
   link.Text=(githubIcon?"":"↗  ")+text; link.AutoSize=false; link.Height=32; link.LinkColor=link.ActiveLinkColor=link.VisitedLinkColor=color; link.LinkBehavior=LinkBehavior.HoverUnderline; link.AccessibleName=text+L.T(" — tarayıcıda aç"); link.Tag=url; link.TextAlign=ContentAlignment.MiddleLeft;
   link.LinkClicked+=delegate { try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute=true }); } catch(Exception ex) { AppInfo.Log(ex.ToString()); MessageBox.Show(link.FindForm(),L.T("Bağlantı açılamadı. Adresi tarayıcına yazabilirsin:\n")+url,L.T("Bağlantı"),MessageBoxButtons.OK,MessageBoxIcon.Information); } }; return link;
  }
 }
 static class AppPalette {
  internal static readonly Color Danger=Color.FromArgb(159,57,65);
  // Each theme picks its tones and accent here; every window reads them through these helpers.
  internal static string Variant="black";
  internal static readonly string[] Modes={"light","black","ocean","sakura","sunset","android","champagne","system"};
  internal static bool IsDark(string mode) { return mode!="light" && mode!="champagne" && mode!="system"; }
  // Background, surface, text, muted, border, selection, console; then accent and the text color on it.
  static readonly int[] Light={0xEFF4FC,0xF8FAFF,0x0D1F45,0x506384,0xCEDAEE,0xE2EDFF,0xF7F9FD,0x144EE0,0xFFFFFF},   // Aydınlık
   Black={0x060608,0x141418,0xF0F0F4,0x9EA0AA,0x2A2A30,0x1E263C,0x000000,0x144EE0,0xFFFFFF},                       // Karanlık
   Ocean={0x0B1219,0x14222D,0xE0F2FE,0x94B4C8,0x25394A,0x123A4A,0x070D13,0x06B6D4,0x04131A},                       // Okyanus
   Sakura={0x130F14,0x221A24,0xFCE7F3,0xC9A9BC,0x3E2E41,0x4A2033,0x0B080C,0xF43F5E,0xFFFFFF},                      // Sakura
   Sunset={0x16120F,0x261E19,0xFFEDD5,0xC9AE96,0x433429,0x4A2A12,0x0D0A08,0xF97316,0x1A0E05},                      // Güneş Batımı
   Android={0x0D1610,0x16261D,0xECFDF5,0x9CC7B0,0x2A4535,0x123D2C,0x070D09,0x10B981,0x04140C},                     // Android
   Champagne={0xF7F5F0,0xFFFFFF,0x292524,0x78716C,0xE7E2D8,0xFDF0DC,0xFBFAF7,0xD97706,0x1C1917};                   // Şampanya (light)
  static int[] Set(bool dark) { if(!dark) return Variant=="champagne"?Champagne:Light; switch(Variant) { case "ocean": return Ocean; case "sakura": return Sakura; case "sunset": return Sunset; case "android": return Android; default: return Black; } }
  static Color Pick(bool dark,int slot) { return Color.FromArgb(unchecked((int)0xFF000000)|Set(dark)[slot]); }
  internal static Color Background(bool dark) { return Pick(dark,0); }
  internal static Color Surface(bool dark) { return Pick(dark,1); }
  internal static Color Text(bool dark) { return Pick(dark,2); }
  internal static Color Muted(bool dark) { return Pick(dark,3); }
  internal static Color Border(bool dark) { return Pick(dark,4); }
  internal static Color Selection(bool dark) { return Pick(dark,5); }
  internal static Color Console(bool dark) { return Pick(dark,6); }
  // Light accents (cyan, orange, green, amber) carry dark text instead of white.
  internal static Color Accent { get { return Pick(IsDark(Variant),7); } }
  internal static Color AccentInk { get { return Pick(IsDark(Variant),8); } }
  // Links on dark backgrounds: the theme's own accent, a lighter blue on the two blue themes.
  internal static Color Mint { get { return Variant=="black" || Variant=="light"?Color.FromArgb(127,174,255):Accent; } }
  // Karanlık unless the user picked something else; read here too for windows that open before the main one.
  // Themes that were removed (Lacivert, Mor, Titanyum, Saf Bulut, Gümüş Buz, Siber Grafit) also fall back to Karanlık.
  internal static string SavedTheme() { return StoredTheme() ?? "black"; }
  // The saved theme, or null when the user never had one (a first install).
  internal static string StoredTheme() {
   try { using(var key=Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\GoodbyeDPITray")) { string saved=key==null?null:key.GetValue("Theme") as string; if(saved==null) return null; return Array.IndexOf(Modes,saved)>=0?saved:"black"; } }
   catch { return null; }
  }
  internal static bool WindowsDark() {
   try { using(var key=Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize")) return key!=null && Convert.ToInt32(key.GetValue("AppsUseLightTheme",1))==0; }
   catch { return false; }
  }
  internal static string VariantOf(string mode) { return mode=="system"?"black":mode; }
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
  internal static string ShellStatus=L.T("Henüz oluşturulmadı");
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
  internal static readonly string[] Titles={L.T("Uygulamayı aç"),L.T("GoodbyeDPI'ı başlat"),L.T("GoodbyeDPI'ı durdur"),L.T("GoodbyeDPI'ı yeniden başlat"),L.T("Profil seçimi"),L.T("Ayarlar"),L.T("Arayüzden çık")};
  internal static JumpList BuildJumpList(string executable) {
   var list=new JumpList(); list.ShowRecentCategory=false; list.ShowFrequentCategory=false;
   for(int i=0;i<Actions.Length;i++) list.JumpItems.Add(new JumpTask { Title=Titles[i],Description=Titles[i],ApplicationPath=executable,Arguments="--action "+Actions[i],WorkingDirectory=System.IO.Path.GetDirectoryName(executable),IconResourcePath=executable,IconResourceIndex=0 });
   return list;
  }
  internal static void RegisterJumpList() {
   try {
    var list=BuildJumpList(Application.ExecutablePath);
    int rejected=0; list.JumpItemsRejected+=delegate(object sender,JumpItemsRejectedEventArgs e) { rejected+=e.RejectedItems.Count; };
    list.Apply(); ShellStatus=rejected==0?L.T("7 görev kaydedildi"):rejected+L.T(" görev Windows tarafından reddedildi");
   } catch(Exception ex) { ShellStatus=L.T("Kullanılamıyor: ")+ex.Message; AppInfo.Log("Jump List: "+ex.Message); }
  }
 }
 static class MenuAppearance {
  static readonly Font MenuFont=new Font("Segoe UI",10);
  internal static void Apply(ToolStripDropDown menu,Color background,Color foreground,Color border,int minimumWidth) {
   menu.Font=MenuFont; menu.Padding=new Padding(6); menu.BackColor=background; menu.ForeColor=foreground;
   menu.Renderer=new MenuRenderer(background,foreground,border);
   var drop=menu as ToolStripDropDownMenu;
   if(drop!=null) { drop.ShowImageMargin=false; drop.ShowCheckMargin=true; }
   int width=minimumWidth;
   foreach(ToolStripItem item in menu.Items) width=Math.Max(width,TextRenderer.MeasureText(item.Text,MenuFont).Width+80);
   menu.MinimumSize=new Size(width,0);
   foreach(ToolStripItem item in menu.Items) {
    item.Font=MenuFont; item.AutoSize=false; item.Margin=Padding.Empty;
    item.Size=new Size(width-12,item is ToolStripSeparator?12:String.IsNullOrEmpty(item.AccessibleDescription)?Math.Max(38,MenuFont.Height+18):66);
    item.Padding=new Padding(8,4,12,4);
    var parent=item as ToolStripMenuItem;
    if(parent!=null && parent.HasDropDownItems) Apply(parent.DropDown,background,foreground,border,minimumWidth);
   }
  }
 }
 // One shared renderer keeps profile, theme and tray menus consistent.
 class MenuRenderer : ToolStripProfessionalRenderer {
  readonly Color background,foreground,selection,accent,border;
  internal MenuRenderer(Color bg,Color fg,Color selected) { background=bg; foreground=fg; bool dark=bg.GetBrightness()<0.5f; selection=SystemInformation.HighContrast?SystemColors.Highlight:AppPalette.Selection(dark); accent=SystemInformation.HighContrast?SystemColors.HighlightText:dark?AppPalette.Mint:AppPalette.Accent; border=SystemInformation.HighContrast?SystemColors.WindowText:AppPalette.Border(dark); RoundedEdges=false; }
  protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e) { e.Graphics.Clear(background); }
  protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { using(var brush=new SolidBrush(background)) e.Graphics.FillRectangle(brush,e.AffectedBounds); }
  protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e) { using(var pen=new Pen(border)) e.Graphics.DrawRectangle(pen,0,0,e.ToolStrip.Width-1,e.ToolStrip.Height-1); }
  protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e) { var item=e.Item as ToolStripMenuItem; bool active=(e.Item.Enabled && e.Item.Selected) || (item!=null && item.Checked); if(active) { e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias; using(var path=SoftButton.Round(new Rectangle(3,1,e.Item.Width-6,e.Item.Height-2),10)) using(var brush=new SolidBrush(selection)) e.Graphics.FillPath(brush,path); } }
  protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e) { e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias; var r=e.ImageRectangle; using(var pen=new Pen(accent,2)) e.Graphics.DrawLines(pen,new[]{new Point(r.Left+3,r.Top+r.Height/2),new Point(r.Left+7,r.Top+r.Height/2+4),new Point(r.Right-2,r.Top+3)}); }
  protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e) { e.ArrowColor=!e.Item.Enabled?SystemColors.GrayText:SystemInformation.HighContrast && e.Item.Selected?SystemColors.HighlightText:foreground; base.OnRenderArrow(e); }
  protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e) { using(var pen=new Pen(border)) e.Graphics.DrawLine(pen,12,e.Item.Height/2,e.Item.Width-12,e.Item.Height/2); }
  protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e) { var item=e.Item as ToolStripMenuItem; e.TextColor=!e.Item.Enabled?SystemColors.GrayText:SystemInformation.HighContrast && e.Item.Selected?SystemColors.HighlightText:item!=null && item.Checked?accent:foreground;
   // An item with a description (AccessibleDescription) shows it as a smaller, muted second line under its name.
   string hint=e.Item.AccessibleDescription;
   if(String.IsNullOrEmpty(hint) || e.Item.Height<52) { base.OnRenderItemText(e); return; }
   int x=e.TextRectangle.X, width=Math.Min(e.Item.Width,e.ToolStrip.ClientSize.Width)-x-14;
   TextRenderer.DrawText(e.Graphics,e.Text,e.TextFont,new Rectangle(x,8,width,22),e.TextColor,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix);
   using(var small=new Font(e.TextFont.FontFamily,8.5f)) TextRenderer.DrawText(e.Graphics,hint,small,new Rectangle(x,30,width,e.Item.Height-34),Blend(foreground,background,0.62f),TextFormatFlags.Left|TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix);
  }
  static Color Blend(Color a,Color b,float share) { return Color.FromArgb((int)(a.R*share+b.R*(1-share)),(int)(a.G*share+b.G*(1-share)),(int)(a.B*share+b.B*(1-share))); }
 }
}
