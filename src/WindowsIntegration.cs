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
   Champagne={0xF7F5F0,0xFFFFFF,0x292524,0x6B645F,0xE7E2D8,0xFDF0DC,0xFBFAF7,0xD97706,0x1C1917};                   // Şampanya (light)
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
   // A form shown as a page inside the main window has no title bar of its own.
   if(!form.IsHandleCreated || !form.TopLevel) return;
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
  // The jump list keeps only the tasks that do something without opening the window; each has its own icon.
  internal static readonly string[] JumpActions={"start","stop","restart"};
  internal static readonly string[] JumpTitles={L.T("GoodbyeDPI'ı başlat"),L.T("GoodbyeDPI'ı durdur"),L.T("GoodbyeDPI'ı yeniden başlat")};
  internal static JumpList BuildJumpList(string executable) {
   var list=new JumpList(); list.ShowRecentCategory=false; list.ShowFrequentCategory=false;
   for(int i=0;i<JumpActions.Length;i++) {
    string icon=JumpIcons.PathFor(JumpActions[i]);
    list.JumpItems.Add(new JumpTask { Title=JumpTitles[i],Description=JumpTitles[i],ApplicationPath=executable,Arguments="--action "+JumpActions[i],WorkingDirectory=System.IO.Path.GetDirectoryName(executable),IconResourcePath=icon ?? executable,IconResourceIndex=0 });
   }
   return list;
  }
  internal static void RegisterJumpList() {
   try {
    var list=BuildJumpList(Application.ExecutablePath);
    int rejected=0; list.JumpItemsRejected+=delegate(object sender,JumpItemsRejectedEventArgs e) { rejected+=e.RejectedItems.Count; };
    list.Apply(); ShellStatus=rejected==0?L.T("Görevler kaydedildi"):rejected+L.T(" görev Windows tarafından reddedildi");
   } catch(Exception ex) { ShellStatus=L.T("Kullanılamıyor: ")+ex.Message; AppInfo.Log("Jump List: "+ex.Message); }
  }
 }
 // Round icons for the jump list tasks: green play, red stop, blue restart arrow. The shell wants a file,
 // so they are drawn once into the user's app data folder (the uninstall removes that folder).
 static class JumpIcons {
  internal static string Folder { get { return System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.IO.Path.GetDirectoryName(AppInfo.LogPath)),"Icons"); } }
  // Bump the suffix when the drawing changes, so Windows does not keep showing its cached copy.
  internal static string PathFor(string action) {
   try {
    string path=System.IO.Path.Combine(Folder,action+"-2.ico");
    if(!System.IO.File.Exists(path)) { System.IO.Directory.CreateDirectory(Folder); System.IO.File.WriteAllBytes(path,Icon(action)); }
    return path;
   } catch(Exception ex) { AppInfo.Log("Jump List simgesi: "+ex.Message); return null; }
  }
  internal static byte[] Icon(string action) {
   int[] sizes={16,20,24,32,48,64};
   var images=new byte[sizes.Length][];
   for(int i=0;i<sizes.Length;i++) using(var bitmap=Draw(action,sizes[i])) images[i]=Dib(bitmap);
   // Classic ICO container with 32-bit images, readable by every part of Windows.
   using(var stream=new System.IO.MemoryStream()) using(var writer=new System.IO.BinaryWriter(stream)) {
    writer.Write((short)0); writer.Write((short)1); writer.Write((short)sizes.Length);
    int offset=6+16*sizes.Length;
    for(int i=0;i<sizes.Length;i++) { writer.Write((byte)sizes[i]); writer.Write((byte)sizes[i]); writer.Write((byte)0); writer.Write((byte)0); writer.Write((short)1); writer.Write((short)32); writer.Write(images[i].Length); writer.Write(offset); offset+=images[i].Length; }
    foreach(byte[] image in images) writer.Write(image);
    writer.Flush(); return stream.ToArray();
   }
  }
  // Icon image: BITMAPINFOHEADER (height doubled for the mask), bottom-up BGRA pixels, then an empty AND mask.
  static byte[] Dib(Bitmap bitmap) {
   int size=bitmap.Width, maskStride=((size+31)/32)*4;
   using(var stream=new System.IO.MemoryStream()) using(var writer=new System.IO.BinaryWriter(stream)) {
    writer.Write(40); writer.Write(size); writer.Write(size*2); writer.Write((short)1); writer.Write((short)32); writer.Write(0); writer.Write(size*size*4+maskStride*size); writer.Write(0); writer.Write(0); writer.Write(0); writer.Write(0);
    for(int y=size-1;y>=0;y--) for(int x=0;x<size;x++) { Color c=bitmap.GetPixel(x,y); writer.Write(c.B); writer.Write(c.G); writer.Write(c.R); writer.Write(c.A); }
    writer.Write(new byte[maskStride*size]);
    writer.Flush(); return stream.ToArray();
   }
  }
  static Bitmap Draw(string action,int size) {
   var bitmap=new Bitmap(size,size,System.Drawing.Imaging.PixelFormat.Format32bppArgb);
   using(var g=Graphics.FromImage(bitmap)) {
    g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias; g.PixelOffsetMode=System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
    Color fill=action=="start"?Color.FromArgb(0x16,0xA3,0x4A):action=="stop"?Color.FromArgb(0xDC,0x26,0x26):Color.FromArgb(0x25,0x63,0xEB);
    float s=size;
    using(var brush=new SolidBrush(fill)) g.FillEllipse(brush,0.5f,0.5f,s-1,s-1);
    using(var white=new SolidBrush(Color.White)) {
     if(action=="start") g.FillPolygon(white,new[]{new PointF(s*0.40f,s*0.29f),new PointF(s*0.40f,s*0.71f),new PointF(s*0.74f,s*0.5f)});
     else if(action=="stop") using(var square=SoftButton.Round(Rectangle.Round(new RectangleF(s*0.31f,s*0.31f,s*0.38f,s*0.38f)),Math.Max(2,size/8))) g.FillPath(white,square);
     else {
      using(var pen=new Pen(Color.White,Math.Max(1.6f,s*0.11f))) { pen.StartCap=System.Drawing.Drawing2D.LineCap.Round; g.DrawArc(pen,s*0.27f,s*0.27f,s*0.46f,s*0.46f,-60,290); }
      // Arrow head at the open end of the arc.
      double angle=-60*Math.PI/180; float cx=s*0.5f+(float)Math.Cos(angle)*s*0.23f, cy=s*0.5f+(float)Math.Sin(angle)*s*0.23f, h=s*0.17f;
      g.FillPolygon(white,new[]{new PointF(cx-h*0.2f,cy-h),new PointF(cx+h*0.95f,cy+h*0.05f),new PointF(cx-h*0.45f,cy+h*0.7f)});
     }
    }
   }
   return bitmap;
  }
 }
 // One shared renderer keeps the menus consistent. The tray menus keep the classic look; the menus that open from
 // the main window (Modern) share one design: Windows 11's rounded window, a soft fill under the mouse, the chosen
 // item marked by an accent check on the right, icons in one column on the left and descriptions as a muted line.
 class MenuRenderer : ToolStripProfessionalRenderer {
  readonly Color background,foreground,selection,accent,border,muted,hover;
  // Rounded corners for the window of the menu (the drop-down pickers); the tray menus stay square.
  internal bool Rounded;
  // The main window's menus: no check or image column, everything laid out by this renderer.
  internal bool Modern;
  bool systemCorners;
  [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);
  protected override void Initialize(ToolStrip toolStrip) {
   base.Initialize(toolStrip);
   if(!Rounded) return;
   toolStrip.SizeChanged+=delegate { Shape(toolStrip); };
   // Windows 11 rounds the menu window itself, with a smooth edge and its own border; older Windows clips a region.
   if(toolStrip.IsHandleCreated) RoundWindow(toolStrip); else toolStrip.HandleCreated+=delegate { RoundWindow(toolStrip); };
  }
  void RoundWindow(ToolStrip strip) {
   try {
    int corner=3; systemCorners=DwmSetWindowAttribute(strip.Handle,33,ref corner,4)==0; // DWMWA_WINDOW_CORNER_PREFERENCE: small round
    if(systemCorners) { int edge=ColorTranslator.ToWin32(border); DwmSetWindowAttribute(strip.Handle,34,ref edge,4); strip.Region=null; } // DWMWA_BORDER_COLOR
   } catch(DllNotFoundException) { systemCorners=false; } catch(EntryPointNotFoundException) { systemCorners=false; }
  }
  void Shape(ToolStrip strip) { if(systemCorners || strip.Width<20 || strip.Height<20) return; using(var path=SoftButton.Round(new Rectangle(0,0,strip.Width,strip.Height),16)) strip.Region=new Region(path); }
  internal MenuRenderer(Color bg,Color fg,Color selected) {
   background=bg; foreground=fg; bool dark=bg.GetBrightness()<0.5f;
   selection=SystemInformation.HighContrast?SystemColors.Highlight:AppPalette.Selection(dark);
   accent=SystemInformation.HighContrast?SystemColors.HighlightText:dark?AppPalette.Mint:AppPalette.Accent;
   border=SystemInformation.HighContrast?SystemColors.WindowText:AppPalette.Border(dark);
   muted=SystemInformation.HighContrast?fg:Blend(fg,bg,0.6f);
   hover=SystemInformation.HighContrast?SystemColors.Highlight:Blend(fg,bg,dark?0.09f:0.06f);
   RoundedEdges=false;
  }
  protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e) { e.Graphics.Clear(background); }
  protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { using(var brush=new SolidBrush(background)) e.Graphics.FillRectangle(brush,e.AffectedBounds); }
  protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e) {
   if(Rounded && systemCorners) return;
   if(Rounded) { Shape(e.ToolStrip); e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias; using(var path=SoftButton.Round(new Rectangle(0,0,e.ToolStrip.Width-1,e.ToolStrip.Height-1),16)) using(var pen=new Pen(border)) e.Graphics.DrawPath(pen,path); return; }
   using(var pen=new Pen(border)) e.Graphics.DrawRectangle(pen,0,0,e.ToolStrip.Width-1,e.ToolStrip.Height-1);
  }
  protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e) {
   var item=e.Item as ToolStripMenuItem;
   if(Modern) {
    // Only the row under the mouse (or keyboard focus) gets a fill; the chosen row shows its check instead.
    if(!e.Item.Enabled || !e.Item.Selected) return;
    e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
    using(var path=SoftButton.Round(new Rectangle(2,1,e.Item.Width-4,e.Item.Height-2),12)) using(var brush=new SolidBrush(hover)) e.Graphics.FillPath(brush,path);
    return;
   }
   bool active=(e.Item.Enabled && e.Item.Selected) || (item!=null && item.Checked); if(active) { e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias; using(var path=SoftButton.Round(new Rectangle(3,1,e.Item.Width-6,e.Item.Height-2),10)) using(var brush=new SolidBrush(selection)) e.Graphics.FillPath(brush,path); }
  }
  protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e) { if(Modern) return; e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias; var r=e.ImageRectangle; using(var pen=new Pen(accent,2)) e.Graphics.DrawLines(pen,new[]{new Point(r.Left+3,r.Top+r.Height/2),new Point(r.Left+7,r.Top+r.Height/2+4),new Point(r.Right-2,r.Top+3)}); }
  protected override void OnRenderItemImage(ToolStripItemImageRenderEventArgs e) { if(!Modern) base.OnRenderItemImage(e); }
  protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e) { e.ArrowColor=!e.Item.Enabled?SystemColors.GrayText:SystemInformation.HighContrast && e.Item.Selected?SystemColors.HighlightText:foreground; base.OnRenderArrow(e); }
  protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e) { int inset=Modern?8:12; using(var pen=new Pen(border)) e.Graphics.DrawLine(pen,inset,e.Item.Height/2,e.Item.Width-inset,e.Item.Height/2); }
  // Draws a row of a Modern menu: icon column, name, optional muted description, and the check on the right.
  protected void DrawModernRow(ToolStripItemTextRenderEventArgs e,Action<Graphics,Rectangle,Color> icon) {
   var g=e.Graphics; var item=e.Item as ToolStripMenuItem; g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
   bool selected=e.Item.Selected && e.Item.Enabled, chosen=item!=null && item.Checked, find=Convert.ToString(e.Item.Tag)=="accent";
   Color ink=!e.Item.Enabled?Blend(foreground,background,0.45f):SystemInformation.HighContrast && selected?SystemColors.HighlightText:find?accent:foreground;
   string hint=e.Item.AccessibleDescription; bool twoLines=!String.IsNullOrEmpty(hint) && e.Item.Height>=50;
   int width=Math.Min(e.Item.Width,e.ToolStrip.ClientSize.Width), x=12, top=twoLines?8:0, line=twoLines?22:e.Item.Height;
   var iconBox=new Rectangle(x,top+(line-18)/2,18,18);
   bool hasIcon=icon!=null || e.Item.Image!=null || find;
   if(icon!=null) icon(g,iconBox,ink);
   else if(e.Item.Image!=null) { g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic; g.DrawImage(e.Item.Image,iconBox); }
   else if(find) using(var pen=new Pen(ink,1.7f) { StartCap=System.Drawing.Drawing2D.LineCap.Round,EndCap=System.Drawing.Drawing2D.LineCap.Round }) { g.DrawEllipse(pen,iconBox.X+1,iconBox.Y+1,11,11); g.DrawLine(pen,iconBox.X+11,iconBox.Y+11,iconBox.Right-1,iconBox.Bottom-1); }
   int textX=hasIcon?x+30:x+4, checkSpace=28, textWidth=Math.Max(1,width-textX-checkSpace);
   var font=chosen || find?Fonts.Get(e.TextFont,FontStyle.Bold):e.TextFont;
   TextRenderer.DrawText(g,e.Text,font,new Rectangle(textX,top,textWidth,line),ink,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix|TextFormatFlags.EndEllipsis);
   if(twoLines) TextRenderer.DrawText(g,hint,Fonts.Get(e.TextFont.FontFamily,8.5f),new Rectangle(textX,top+line,textWidth+checkSpace-8,e.Item.Height-top-line-4),SystemInformation.HighContrast && selected?SystemColors.HighlightText:muted,TextFormatFlags.Left|TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix);
   if(chosen) { int cx=width-22, cy=top+line/2; using(var pen=new Pen(SystemInformation.HighContrast && selected?SystemColors.HighlightText:accent,2f) { StartCap=System.Drawing.Drawing2D.LineCap.Round,EndCap=System.Drawing.Drawing2D.LineCap.Round,LineJoin=System.Drawing.Drawing2D.LineJoin.Round }) g.DrawLines(pen,new[]{new PointF(cx-5,cy),new PointF(cx-1.5f,cy+3.5f),new PointF(cx+5,cy-4)}); }
  }
  protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e) {
   if(Modern && e.Item is ToolStripMenuItem) { DrawModernRow(e,null); return; }
   var item=e.Item as ToolStripMenuItem; e.TextColor=!e.Item.Enabled?SystemColors.GrayText:SystemInformation.HighContrast && e.Item.Selected?SystemColors.HighlightText:item!=null && (item.Checked || Convert.ToString(item.Tag)=="accent")?accent:foreground;
   // An item with a description (AccessibleDescription) shows it as a smaller, muted second line under its name.
   string hint=e.Item.AccessibleDescription;
   if(String.IsNullOrEmpty(hint) || e.Item.Height<52) { base.OnRenderItemText(e); return; }
   int x=e.TextRectangle.X, width=Math.Min(e.Item.Width,e.ToolStrip.ClientSize.Width)-x-14;
   TextRenderer.DrawText(e.Graphics,e.Text,e.TextFont,new Rectangle(x,8,width,22),e.TextColor,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix);
   { var small=Fonts.Get(e.TextFont.FontFamily,8.5f); TextRenderer.DrawText(e.Graphics,hint,small,new Rectangle(x,30,width,e.Item.Height-34),Blend(foreground,background,0.62f),TextFormatFlags.Left|TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix); }
  }
  internal static Color Blend(Color a,Color b,float share) { return Color.FromArgb((int)(a.R*share+b.R*(1-share)),(int)(a.G*share+b.G*(1-share)),(int)(a.B*share+b.B*(1-share))); }
  // The tray menu and its submenus in the same look, with tighter rows (32 px) since it lists more commands.
  internal static void PrepareTree(ToolStripDropDownMenu menu,Color surface,Color ink,Color border,int width) {
   // Sizes are written at 100 %; the menu's own screen scale grows them with the text.
   float f=menu.DeviceDpi/96f; Func<int,int> s=delegate(int px) { return (int)Math.Round(px*f); }; width=s(width);
   menu.ShowCheckMargin=false; menu.ShowImageMargin=false; menu.Padding=new Padding(4); menu.DropShadowEnabled=true;
   menu.BackColor=surface; menu.ForeColor=ink; menu.Font=Fonts.Get("Segoe UI",10f); menu.MinimumSize=new Size(width,0);
   menu.Renderer=new MenuRenderer(surface,ink,border) { Rounded=true,Modern=true };
   foreach(ToolStripItem item in menu.Items) {
    item.AutoSize=false; item.Margin=Padding.Empty; item.BackColor=surface; item.ForeColor=ink; item.Font=Fonts.Get("Segoe UI",10f);
    if(item is ToolStripSeparator) { item.Size=new Size(width-s(8),s(9)); continue; }
    int height=s(32);
    if(!String.IsNullOrEmpty(item.AccessibleDescription)) height=Math.Max(s(60),s(8+22)+TextRenderer.MeasureText(item.AccessibleDescription,Fonts.Get("Segoe UI",8.5f),new Size(Math.Max(40,width-s(8+16+20)),Int32.MaxValue),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix).Height+s(8));
    item.Size=new Size(width-s(8),height);
    var parent=item as ToolStripMenuItem;
    if(parent!=null && parent.HasDropDownItems) { var sub=parent.DropDown as ToolStripDropDownMenu; if(sub!=null) PrepareTree(sub,surface,ink,border,(int)Math.Round(width/f)); }
   }
  }
  // Sets up a main-window menu for the Modern look: one padding, one font, roomy rows, no check or image column.
  internal static void Prepare(ToolStripDropDownMenu menu,Color surface,Color ink,Color border,int width) {
   float f=menu.DeviceDpi/96f; Func<int,int> s=delegate(int px) { return (int)Math.Round(px*f); }; width=Math.Max(width,s(width));
   menu.ShowCheckMargin=false; menu.ShowImageMargin=false; menu.Padding=new Padding(5); menu.DropShadowEnabled=true;
   menu.BackColor=surface; menu.ForeColor=ink; menu.MinimumSize=new Size(width,0);
   menu.Renderer=new MenuRenderer(surface,ink,border) { Rounded=true,Modern=true };
   foreach(ToolStripItem item in menu.Items) {
    item.AutoSize=false; item.Margin=Padding.Empty; item.BackColor=surface; item.ForeColor=ink;
    if(item is ToolStripSeparator) { item.Size=new Size(width-s(10),s(11)); continue; }
    if(item is ToolStripMenuItem) item.Font=Fonts.Get("Segoe UI",10f,Convert.ToString(item.Tag)=="accent"?FontStyle.Bold:FontStyle.Regular);
    bool described=!String.IsNullOrEmpty(item.AccessibleDescription) && !(item is ToolStripLabel);
    if(item is ToolStripLabel) continue;
    // A description wraps to as many lines as it needs under the name.
    int height=s(36);
    if(described) { bool icon=item.Image!=null || Convert.ToString(item.Tag)=="accent"; int textWidth=Math.Max(40,width-s(10+(icon?42:16)+20)); height=s(8+22)+TextRenderer.MeasureText(item.AccessibleDescription,Fonts.Get("Segoe UI",8.5f),new Size(textWidth,Int32.MaxValue),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix).Height+s(8); }
    item.Size=new Size(width-s(10),height);
   }
  }
 }
}
