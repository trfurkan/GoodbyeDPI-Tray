using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Windows.Forms;
using Microsoft.Win32;

namespace GoodbyeTray {
 class SoftButton : Control, IButtonControl {
  internal Color Fill, Ink, Edge;
  internal string Glyph;
  // Drawn in place of a line glyph when set, e.g. the selected theme's color swatch.
  internal Image GlyphImage;
  bool hovered,pressed;
  public DialogResult DialogResult { get; set; }
  public void NotifyDefault(bool value) { Invalidate(); }
  public void PerformClick() { if(Enabled && Visible) OnClick(EventArgs.Empty); }
  public SoftButton() { SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw|ControlStyles.Selectable|ControlStyles.StandardClick,true); Cursor=Cursors.Hand; Height=40; TabStop=true; AccessibleRole=AccessibleRole.PushButton; }
  protected override void OnPaintBackground(PaintEventArgs e) { e.Graphics.Clear(BackColor); }
  protected override void OnMouseEnter(EventArgs e) { hovered=true; Invalidate(); base.OnMouseEnter(e); }
  protected override void OnMouseLeave(EventArgs e) { hovered=false; pressed=false; Invalidate(); base.OnMouseLeave(e); }
  protected override void OnMouseDown(MouseEventArgs e) { if(e.Button==MouseButtons.Left) { pressed=true; Focus(); Invalidate(); } base.OnMouseDown(e); }
  protected override void OnMouseUp(MouseEventArgs e) { pressed=false; Invalidate(); base.OnMouseUp(e); }
  protected override void OnKeyDown(KeyEventArgs e) { if(e.KeyCode==Keys.Space) { pressed=true; Invalidate(); e.Handled=true; } base.OnKeyDown(e); }
  protected override void OnKeyUp(KeyEventArgs e) { if(e.KeyCode==Keys.Space && pressed) { pressed=false; PerformClick(); Invalidate(); e.Handled=true; } base.OnKeyUp(e); }
  protected override bool ProcessDialogKey(Keys keyData) { if(keyData==Keys.Enter) { PerformClick(); return true; } return base.ProcessDialogKey(keyData); }
  protected override void OnLostFocus(EventArgs e) { pressed=false; Invalidate(); base.OnLostFocus(e); }
  protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
  protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
  protected override void OnPaint(PaintEventArgs e) {
   e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
   var bounds=new Rectangle(1,1,Width-3,Height-3);
   if(bounds.Width<2 || bounds.Height<2) return;
   Color paint=Fill;
   if(Enabled && (hovered || pressed)) { int weight=pressed?25:12; paint=Color.FromArgb((Fill.R*(100-weight)+Ink.R*weight)/100,(Fill.G*(100-weight)+Ink.G*weight)/100,(Fill.B*(100-weight)+Ink.B*weight)/100); }
   using(var path=Round(bounds,Math.Min(16,Math.Min(bounds.Width,bounds.Height)))) using(var fill=new SolidBrush(paint)) using(var pen=new Pen(Edge)) {
    e.Graphics.FillPath(fill,path); e.Graphics.DrawPath(pen,path);
   }
   Color ink=Enabled?Ink:Color.FromArgb((Ink.R+Fill.R)/2,(Ink.G+Fill.G)/2,(Ink.B+Fill.B)/2);
   var textBounds=ClientRectangle;
   if(!String.IsNullOrEmpty(Glyph)) {
    int textWidth=TextRenderer.MeasureText(Text,Font).Width; int x=Math.Max(12,(Width-textWidth-28)/2),y=(Height-18)/2;
    var saved=e.Graphics.Save(); e.Graphics.TranslateTransform(x,y);
    using(var pen=new Pen(ink,1.7f)) {
     pen.StartCap=pen.EndCap=LineCap.Round; pen.LineJoin=LineJoin.Round;
     if(Glyph=="restart") { e.Graphics.DrawArc(pen,2,2,14,14,-60,300); e.Graphics.DrawLines(pen,new[]{new Point(10,0),new Point(14,2),new Point(11,5)}); }
     else if(Glyph=="trash") { e.Graphics.DrawLine(pen,2,4,16,4); e.Graphics.DrawRectangle(pen,6,1,6,3); e.Graphics.DrawLines(pen,new[]{new Point(4,5),new Point(5,17),new Point(13,17),new Point(14,5)}); e.Graphics.DrawLine(pen,8,7,8,14); e.Graphics.DrawLine(pen,11,7,11,14); }
     else if(Glyph=="console") { e.Graphics.DrawLines(pen,new[]{new Point(2,3),new Point(8,9),new Point(2,15)}); e.Graphics.DrawLine(pen,10,15,17,15); }
     else if(GlyphImage!=null) e.Graphics.DrawImage(GlyphImage,-1,-1,20,20);
     else if(Glyph=="globe") { e.Graphics.DrawEllipse(pen,1,1,16,16); e.Graphics.DrawEllipse(pen,5.5f,1,7,16); e.Graphics.DrawLine(pen,1,9,17,9); }
     else { e.Graphics.DrawLines(pen,new[]{new Point(12,1),new Point(9,4),new Point(12,7),new Point(16,4),new Point(16,9),new Point(12,11),new Point(9,10),new Point(3,17),new Point(0,14),new Point(7,7),new Point(7,3),new Point(12,1)}); }
    }
    e.Graphics.Restore(saved); textBounds=new Rectangle(x+26,0,Math.Max(1,Width-x-34),Height);
   }
   TextRenderer.DrawText(e.Graphics,Text,Font,textBounds,ink,(String.IsNullOrEmpty(Glyph)?TextFormatFlags.HorizontalCenter:TextFormatFlags.Left)|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
   if(Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics,new Rectangle(8,8,Width-16,Height-16),Ink,Fill);
  }
  internal static GraphicsPath Round(Rectangle r,int diameter) {
   var path=new GraphicsPath(); path.AddArc(r.X,r.Y,diameter,diameter,180,90); path.AddArc(r.Right-diameter,r.Y,diameter,diameter,270,90); path.AddArc(r.Right-diameter,r.Bottom-diameter,diameter,diameter,0,90); path.AddArc(r.X,r.Bottom-diameter,diameter,diameter,90,90); path.CloseFigure(); return path;
  }
 }
 class SoftPanel : Panel {
  internal Color Fill,Edge;
  public SoftPanel() { SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true); Padding=new Padding(22); }
  protected override void OnSizeChanged(EventArgs e) { base.OnSizeChanged(e); Invalidate(); }
  protected override void OnPaintBackground(PaintEventArgs e) { e.Graphics.Clear(BackColor); }
  protected override void OnPaint(PaintEventArgs e) {
   base.OnPaint(e); e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
   if(Width<4 || Height<4) return;
   using(var path=SoftButton.Round(new Rectangle(1,1,Width-3,Height-3),Math.Min(24,Math.Min(Width-3,Height-3)))) using(var brush=new SolidBrush(Fill)) using(var pen=new Pen(Edge)) { e.Graphics.FillPath(brush,path); e.Graphics.DrawPath(pen,path); }
  }
 }
 class ProfilePicker : SoftButton {
  public readonly List<string> Items=new List<string>();
  readonly ContextMenuStrip menu=new ContextMenuStrip { ShowImageMargin=false };
  int selected=-1;
  internal event EventHandler SelectedIndexChanged;
  public int SelectedIndex {
   get { return selected; }
   set { if(value < -1 || value>=Items.Count) throw new ArgumentOutOfRangeException("value"); if(selected==value) return; selected=value; Text=value<0?L.T("Profil seç  ▾"):Items[value]+"  ▾"; if(SelectedIndexChanged!=null) SelectedIndexChanged(this,EventArgs.Empty); Invalidate(); }
  }
  public ProfilePicker() { Click+=delegate { OpenMenu(); }; AccessibleName=L.T("Bağlantı profili"); }
  void OpenMenu() {
   if(IsDisposed || Disposing || !Enabled || menu.Visible) return;
   // The owner keeps the menu alive until disposal. Closed runs before all
   // WinForms dismissal/focus processing has finished; do not dispose there.
   while(menu.Items.Count>0) { var old=menu.Items[0]; menu.Items.RemoveAt(0); old.Dispose(); }
   for(int i=0;i<Items.Count;i++) { int index=i; var item=new ToolStripMenuItem(Items[i]); item.Click+=delegate { SelectedIndex=index; }; menu.Items.Add(item); }
   menu.BackColor=Fill; menu.ForeColor=Ink; menu.Renderer=new MenuRenderer(Fill,Ink,ControlPaint.Light(Fill,0.12f));
   for(int i=0;i<menu.Items.Count;i++) { var item=(ToolStripMenuItem)menu.Items[i]; item.Checked=i==selected; item.BackColor=Fill; item.ForeColor=Ink; }
   menu.Show(this,new Point(0,Height));
  }
  protected override bool ProcessCmdKey(ref Message msg,Keys keyData) { if(Enabled && (keyData==Keys.Down || keyData==(Keys.Alt|Keys.Down))) { OpenMenu(); return true; } return base.ProcessCmdKey(ref msg,keyData); }

  protected override void Dispose(bool disposing) { if(disposing) menu.Dispose(); base.Dispose(disposing); }
 }
 class NoticeView : Control {
  int offset,contentHeight,dragY,dragOffset; bool dragging;
  internal Color ThumbColor,EdgeColor;
  const TextFormatFlags Flags=TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix|TextFormatFlags.TextBoxControl;
  public NoticeView() { SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw|ControlStyles.Selectable,true); TabStop=true; AccessibleRole=AccessibleRole.Text; }
  int ViewHeight { get { return Math.Max(1,Height-24); } }
  int Limit { get { return Math.Max(0,contentHeight-ViewHeight); } }
  void Measure() { contentHeight=TextRenderer.MeasureText(Text,Font,new Size(Math.Max(1,Width-44),Int32.MaxValue),Flags).Height; offset=Math.Min(offset,Limit); }
  Rectangle Thumb { get { int track=Math.Max(1,Height-16),h=Math.Min(track,Math.Max(28,track*ViewHeight/Math.Max(1,contentHeight))); return new Rectangle(Width-13,8+(track-h)*offset/Math.Max(1,Limit),5,h); } }
  void ScrollTo(int value) { offset=Math.Max(0,Math.Min(Limit,value)); Invalidate(); }
  internal void ScrollToEnd() { ScrollTo(Limit); }
  internal void ScrollToTop() { ScrollTo(0); }
  protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Measure(); Invalidate(); }
  protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); Measure(); Invalidate(); }
  protected override void OnSizeChanged(EventArgs e) { base.OnSizeChanged(e); Measure(); }
  protected override void OnPaint(PaintEventArgs e) {
   e.Graphics.Clear(BackColor); using(var pen=new Pen(EdgeColor)) e.Graphics.DrawRectangle(pen,0,0,Width-1,Height-1);
   var saved=e.Graphics.Save(); e.Graphics.SetClip(new Rectangle(12,12,Math.Max(1,Width-44),ViewHeight));
   TextRenderer.DrawText(e.Graphics,Text,Font,new Rectangle(12,12-offset,Math.Max(1,Width-44),Math.Max(contentHeight,ViewHeight)),ForeColor,Flags|TextFormatFlags.PreserveGraphicsClipping); e.Graphics.Restore(saved);
   if(Limit>0) { e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias; using(var path=SoftButton.Round(Thumb,5)) using(var brush=new SolidBrush(ThumbColor)) e.Graphics.FillPath(brush,path); }
   if(Focused && FocusFrame) ControlPaint.DrawFocusRectangle(e.Graphics,new Rectangle(3,3,Width-6,Height-6),ForeColor,BackColor);
  }
  // The installer's notice keeps the keyboard focus frame; read-only viewers like the console do not need it.
  internal bool FocusFrame=true;
  protected override void OnMouseWheel(MouseEventArgs e) { ScrollTo(offset-e.Delta/120*Font.Height*3); base.OnMouseWheel(e); }
  protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); Focus(); if(e.Button!=MouseButtons.Left || Limit==0 || e.X<Width-22) return; if(Thumb.Contains(e.Location)) { dragging=true; dragY=e.Y; dragOffset=offset; Capture=true; } else ScrollTo(offset+(e.Y<Thumb.Top?-ViewHeight:ViewHeight)); }
  protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); if(dragging) ScrollTo(dragOffset+(e.Y-dragY)*Limit/Math.Max(1,Height-16-Thumb.Height)); }
  protected override void OnMouseUp(MouseEventArgs e) { dragging=false; Capture=false; base.OnMouseUp(e); }
  protected override void OnMouseCaptureChanged(EventArgs e) { if(!Capture) dragging=false; base.OnMouseCaptureChanged(e); }
  protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
  protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
  protected override bool IsInputKey(Keys keyData) { return keyData==Keys.Up || keyData==Keys.Down || keyData==Keys.Home || keyData==Keys.End || keyData==Keys.PageUp || keyData==Keys.PageDown || base.IsInputKey(keyData); }
  protected override void OnKeyDown(KeyEventArgs e) { base.OnKeyDown(e); if(e.KeyCode==Keys.Down) ScrollTo(offset+Font.Height); else if(e.KeyCode==Keys.Up) ScrollTo(offset-Font.Height); else if(e.KeyCode==Keys.PageDown) ScrollTo(offset+ViewHeight); else if(e.KeyCode==Keys.PageUp) ScrollTo(offset-ViewHeight); else if(e.KeyCode==Keys.Home) ScrollTo(0); else if(e.KeyCode==Keys.End) ScrollTo(Limit); else if(e.Control && e.KeyCode==Keys.C) { try { Clipboard.SetText(Text); } catch(System.Runtime.InteropServices.ExternalException) {} } else return; e.Handled=true; e.SuppressKeyPress=true; }
 }
 class BufferedLayout : TableLayoutPanel { public BufferedLayout() { DoubleBuffered=true; } }
 partial class MainForm : Form {
  internal const string RevealEvent=@"Local\GoodbyeDPI.Tray.Reveal";
  readonly NotifyIcon tray;
  readonly ToolStripMenuItem trayToggle;
  readonly ToolStripMenuItem trayStop,trayRestart,trayProfiles;
  readonly AppCommands commands;
  int refreshTicks;
  readonly Label serviceNote=new Label();
  readonly EventWaitHandle reveal;
  readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();
  readonly Label status=new Label(), detail=new Label(), notice=new Label(), hint=new Label(), pending=new Label();
  readonly ProfilePicker profiles=new ProfilePicker();
  readonly ProfilePicker motors=new ProfilePicker();
  int SelectedProfile { get { return profiles.SelectedIndex<0?-1:profiles.SelectedIndex+(motors.SelectedIndex==1?Program.ZapretStart:0); } }
  void SelectProfile(int id) { motors.SelectedIndex=id>=Program.ZapretStart?1:0; profiles.SelectedIndex=id>=Program.ZapretStart?id-Program.ZapretStart:id; }
  readonly CheckBox startup=new CheckBox(), engineStartup=new CheckBox();


  readonly SoftButton toggle=new SoftButton(), apply=new SoftButton(), restart=new SoftButton();
  readonly Label trayHelp=LabelOf("",9,false);
  // The first check waits a little: right after logon the network is often not up yet.
  DateTime nextUpdateCheck=DateTime.UtcNow.AddSeconds(30);
  bool updateChecking;
  UpdateState updateState;
  string availableUpdate;
  event Action UpdateStateChanged;


  readonly TableLayoutPanel dashboard=new BufferedLayout();
  readonly TableLayoutPanel footerPanel=new BufferedLayout();

  TableLayoutPanel startupContent;
  bool sizing,stateReadPending;
  Form diagnosticsWindow,accessWindow;
  readonly SoftButton themeButton=new SoftButton(), languageButton=new SoftButton();
  readonly ContextMenuStrip languageMenu=new ContextMenuStrip { ShowImageMargin=true,ShowCheckMargin=true,ImageScalingSize=new Size(20,20),MinimumSize=new Size(200,0),Padding=new Padding(5),Font=new Font("Segoe UI",10) };
  // Every text is read once when windows are built, so a new language takes effect by reopening the app.
  internal void ChangeLanguage(string code) {
   if(busy) return;
   if(code==L.Current) return;
   try { L.Save(code); } catch(Exception ex) { ReportError(ex); return; }
   if(commands==null) return;
   try { Process.Start(new ProcessStartInfo(Application.ExecutablePath,"--relaunch "+Process.GetCurrentProcess().Id) { UseShellExecute=false }).Dispose(); }
   catch(Exception ex) { ReportError(ex); return; }
   quitting=true; Close();
  }
  readonly ContextMenuStrip themeMenu=new ContextMenuStrip { ShowImageMargin=true,ShowCheckMargin=true,ImageScalingSize=new Size(20,20),MinimumSize=new Size(300,0),Padding=new Padding(5),Font=new Font("Segoe UI",10) };
  string themePreference="black";
  static readonly string[] ThemeModes=AppPalette.Modes, ThemeNames={L.T("Aydınlık"),L.T("Karanlık"),L.T("Okyanus"),"Sakura",L.T("Güneş Batımı"),"Android",L.T("Şampanya"),L.T("Sistem")}, ThemeLabels={L.T("Aydınlık"),L.T("Karanlık"),L.T("Okyanus"),"Sakura",L.T("Güneş Batımı"),"Android",L.T("Şampanya"),L.T("Windows'a uy")};
  static string ThemeName(string mode) { int i=Array.IndexOf(ThemeModes,mode); return i<0?ThemeNames[0]:ThemeNames[i]; }
  // A small preview of each theme for the menu: its background with the accent color; "system" is half light, half dark.
  static Bitmap ThemeSwatch(string mode) {
   var bitmap=new Bitmap(20,20); string was=AppPalette.Variant;
   using(var g=Graphics.FromImage(bitmap)) {
    g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
    AppPalette.Variant=AppPalette.VariantOf(mode); Color fill=AppPalette.Background(AppPalette.IsDark(mode) || mode=="system"), dot=AppPalette.Accent; AppPalette.Variant="black";
    using(var brush=new SolidBrush(fill)) g.FillEllipse(brush,1,1,17,17);
    if(mode=="system") using(var light=new SolidBrush(AppPalette.Background(false))) g.FillPie(light,1,1,17,17,90,180);
    using(var accent=new SolidBrush(dot)) g.FillEllipse(accent,6.5f,6.5f,6,6);
    using(var edge=new Pen(Color.FromArgb(120,128,128,140))) g.DrawEllipse(edge,1,1,17,17);
   }
   AppPalette.Variant=was; return bitmap;
  }
  const string PreferencesKey=@"Software\GoodbyeDPITray";
  bool quitting,refreshing,busy,running,available,transition,dark;
  int activeProfile=-1;
  readonly bool? themeOverride;
  Color background,surface,foreground,secondary,border;

  public MainForm(bool withTray) : this(withTray,null) {}
  public MainForm(bool withTray,bool? previewDark) {
   themeOverride=previewDark; Text="GoodbyeDPI"; FormBorderStyle=FormBorderStyle.Sizable; ControlBox=true; MinimizeBox=true; MaximizeBox=true; Font=new Font("Segoe UI",10); AutoScaleMode=AutoScaleMode.Dpi; DoubleBuffered=true; SetStyle(ControlStyles.ResizeRedraw,true);
   themePreference=AppPalette.SavedTheme();
   ClientSize=new Size(900,Math.Min(840,Screen.PrimaryScreen.WorkingArea.Height-70)); MinimumSize=new Size(760,740); StartPosition=FormStartPosition.CenterScreen; Icon=Program.AppIcon;
   var scroll=new Panel { Dock=DockStyle.Fill,AutoScroll=false }; Controls.Add(scroll);
   var layout=dashboard; layout.Dock=DockStyle.Fill; layout.AutoSize=false; layout.Padding=new Padding(20,10,20,10); layout.ColumnCount=1; layout.RowCount=8;
   foreach(int height in new[]{104,112,0,160,164,58}) layout.RowStyles.Add(new RowStyle(SizeType.Absolute,height));
   layout.RowStyles.Add(new RowStyle(SizeType.Absolute,0)); layout.RowStyles.Add(new RowStyle(SizeType.Percent,100)); scroll.Controls.Add(layout);
   var header=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=3,RowCount=1 };
   header.RowStyles.Add(new RowStyle(SizeType.Percent,100)); header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,180)); header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,172));
      var brand=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,RowCount=1 };
   brand.RowStyles.Add(new RowStyle(SizeType.Percent,100)); brand.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,90)); brand.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
   var logo=new Panel { Dock=DockStyle.Fill }; logo.Paint+=delegate(object sender,PaintEventArgs e) { e.Graphics.DrawIcon(Program.AppIcon,new Rectangle(8,2,Math.Min(72,logo.Height-4),Math.Min(80,logo.Height-4))); }; brand.Controls.Add(logo);
   var brandTitle=LabelOf("GoodbyeDPI",27,true); brandTitle.TextAlign=ContentAlignment.MiddleLeft; brand.Controls.Add(brandTitle); header.Controls.Add(brand,0,0);
   for(int i=0;i<ThemeModes.Length;i++) { string choice=ThemeModes[i]; var item=new ToolStripMenuItem(ThemeLabels[i]) { Tag=choice,Image=ThemeSwatch(choice),AutoSize=false,Size=new Size(300,choice=="system"?72:40),Padding=new Padding(8,6,12,6) };
    if(choice=="system") { item.AccessibleDescription=L.T("Temayı (Karanlık/Aydınlık) sistem tercihlerine göre otomatik ayarlar."); item.ImageAlign=ContentAlignment.TopCenter; } item.Click+=delegate { try { SetThemePreference(choice,true); notice.Text=L.T("Tema kaydedildi."); } catch(Exception ex) { ReportError(ex); } }; if(choice=="system") themeMenu.Items.Add(new ToolStripSeparator()); themeMenu.Items.Add(item); }
   themeButton.Dock=DockStyle.Top; themeButton.AccessibleName=L.T("Görünüm teması"); themeButton.Glyph="swatch";
   themeButton.Click+=delegate { foreach(ToolStripItem entry in themeMenu.Items) { var item=entry as ToolStripMenuItem; if(item==null) continue; item.Checked=Convert.ToString(item.Tag)==themePreference; item.BackColor=surface; item.ForeColor=foreground; } themeMenu.BackColor=surface; themeMenu.ForeColor=foreground; themeMenu.Renderer=new MenuRenderer(surface,foreground,border); if(!themeMenu.Visible) themeMenu.Show(themeButton,new Point(0,themeButton.Height)); };
   // Language picker left of the theme button: round flag, language name, a menu of the languages.
   int current=Array.IndexOf(L.Codes,L.Current);
   languageButton.Dock=DockStyle.Top; languageButton.Margin=new Padding(0,3,8,3); languageButton.AccessibleName=L.T("Dil"); languageButton.Glyph="swatch"; languageButton.GlyphImage=L.Flag(L.Current); languageButton.Text=L.Names[current]+"  ▾";
   for(int i=0;i<L.Codes.Length;i++) { string code=L.Codes[i]; var item=new ToolStripMenuItem(L.Names[i]) { Tag=code,Image=L.Flag(code),AutoSize=false,Size=new Size(200,40),Padding=new Padding(8,6,12,6) }; item.Click+=delegate { ChangeLanguage(code); }; languageMenu.Items.Add(item); }
   languageButton.Click+=delegate { foreach(ToolStripItem entry in languageMenu.Items) { var item=(ToolStripMenuItem)entry; item.Checked=Convert.ToString(item.Tag)==L.Current; item.BackColor=surface; item.ForeColor=foreground; } languageMenu.BackColor=surface; languageMenu.ForeColor=foreground; languageMenu.Renderer=new MenuRenderer(surface,foreground,border); if(!languageMenu.Visible) languageMenu.Show(languageButton,new Point(0,languageButton.Height)); };
   header.Controls.Add(languageButton,1,0);
   header.Controls.Add(themeButton,2,0);
   layout.Controls.Add(header);
   var hero=new SoftPanel { Dock=DockStyle.Fill,Margin=new Padding(0,0,0,8),Padding=new Padding(20,12,20,12) }; layout.Controls.Add(hero);
   var heroLayout=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,RowCount=2,BackColor=Color.Transparent };
   heroLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); heroLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,154)); heroLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,34)); heroLayout.RowStyles.Add(new RowStyle(SizeType.Percent,100)); hero.Controls.Add(heroLayout);
   status.Dock=DockStyle.Fill; status.Font=new Font(Font.FontFamily,16,FontStyle.Bold); status.TextAlign=ContentAlignment.MiddleLeft; heroLayout.Controls.Add(status,0,0);
   detail.Dock=DockStyle.Fill; detail.Tag="muted"; heroLayout.Controls.Add(detail,0,1);
   toggle.Dock=DockStyle.Top; toggle.Margin=new Padding(8,10,0,0); toggle.Click+=delegate { Execute(running?"stop":"start",0); }; heroLayout.Controls.Add(toggle,1,0); heroLayout.SetRowSpan(toggle,2);
   // The status line under the hero has no height (its row is 0); errors still show in a message box.
   // It stays visible so the layout keeps its row order.
   notice.Dock=DockStyle.Fill; layout.Controls.Add(notice);
   BuildGeneral(layout); BuildSettings(layout); BuildTools(layout); ConfirmTrayOff=AskTrayOff;
   footerPanel.Dock=DockStyle.Bottom; footerPanel.Height=40; footerPanel.ColumnCount=3; footerPanel.RowCount=1; footerPanel.Margin=Padding.Empty;
   footerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); footerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); footerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,100));
   footerPanel.RowStyles.Add(new RowStyle(SizeType.Percent,100));
   var developer=ProjectLinks.Create(L.T("Geliştirici: trfurkan"),AppInfo.DeveloperUrl,AppPalette.Accent,true); developer.Dock=DockStyle.Fill; footerPanel.Controls.Add(developer,0,0);
   var about=new LinkLabel { Text=L.T("Hakkında"),Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleRight,LinkBehavior=LinkBehavior.HoverUnderline }; about.LinkClicked+=delegate { ShowAbout(); }; footerPanel.Controls.Add(about,2,0);
   layout.Controls.Add(footerPanel,0,7);
   if(withTray) {
    commands=new AppCommands();
    reveal=new EventWaitHandle(false,EventResetMode.AutoReset,RevealEvent);
    var menu=new ContextMenuStrip(); menu.Items.Add(L.T("GoodbyeDPI'ı aç"),null,delegate { Reveal(); }); menu.Items.Add(new ToolStripSeparator());
    trayToggle=new ToolStripMenuItem(L.T("Başlat"),null,delegate { Execute("start",0); }); menu.Items.Add(trayToggle);
    trayStop=new ToolStripMenuItem(L.T("Durdur"),null,delegate { Execute("stop",0); }); menu.Items.Add(trayStop);
    trayRestart=new ToolStripMenuItem(L.T("Yeniden başlat"),null,delegate { Execute("restart",0); }); menu.Items.Add(trayRestart);
    trayPause.DropDownItems.Add(L.T("5 dakika durdur"),null,delegate { Execute("pause5",0); });
    trayPause.DropDownItems.Add(L.T("15 dakika durdur"),null,delegate { Execute("pause15",0); }); menu.Items.Add(trayPause);
    foreach(ToolStripItem item in trayPause.DropDownItems) item.AccessibleDescription=L.T("Süre bitince kendiliğinden devam eder.");
    menu.Items.Add(new ToolStripSeparator());
    trayProfiles=new ToolStripMenuItem(L.T("Profil seçimi"));
    for(int i=0;i<Program.Names.Length;i++) { int index=i; trayProfiles.DropDownItems.Add(new ToolStripMenuItem(Program.Names[i],null,delegate { Execute("profile",index); })); } menu.Items.Add(trayProfiles);
    menu.Items.Add(L.T("Ayarlar"),null,delegate { DispatchCommand("settings"); });
    menu.Items.Add(new ToolStripSeparator());
    menu.Items.Add(L.T("Arayüzden çık"),null,delegate { DispatchCommand("exit"); });
    bool showTray=true; try { showTray=WindowsIntegration.TrayStartupEnabled(); } catch(Exception ex) { AppInfo.Log(ex.ToString()); }
    tray=new NotifyIcon { Icon=Program.AppIcon,Text="GoodbyeDPI",ContextMenuStrip=menu,Visible=showTray }; tray.DoubleClick+=delegate { Reveal(); }; tray.BalloonTipClicked+=delegate { ShowReleaseNotes(); };
    trayHelp.Text=TrayHelp(showTray);
   }
   Shown+=delegate { if(withTray) WindowsIntegration.RegisterJumpList(); if(WindowState==FormWindowState.Minimized && TrayShown) { Hide(); ShowInTaskbar=false; } };
   Resize+=delegate { if(WindowState==FormWindowState.Minimized && TrayShown) HideToTray(); };
   FormClosing+=delegate(object sender,FormClosingEventArgs e) { if(busy && e.CloseReason==CloseReason.UserClosing) { e.Cancel=true; return; } if(!quitting && e.CloseReason==CloseReason.UserClosing && TrayShown) { e.Cancel=true; HideToTray(); } };
   SystemEvents.UserPreferenceChanged+=ThemeChanged;
   timer.Interval=250; timer.Tick+=delegate { if(reveal!=null && reveal.WaitOne(0)) Reveal(); string command=commands==null?null:commands.Poll(); if(command!=null) DispatchCommand(command); if(++refreshTicks>=8) { refreshTicks=0; if(!busy && !sizing) RefreshStateAsync(); if(!sizing && Visible) ApplyTheme(false); if(DateTime.UtcNow>=nextUpdateCheck) CheckForUpdateAsync(); } }; if(withTray) timer.Start();
   SizeChanged+=delegate { if(WindowState!=FormWindowState.Minimized) FitDashboard(); }; ResizeBegin+=delegate { sizing=true; }; ResizeEnd+=delegate { sizing=false; FitDashboard(); };
   ApplyTheme(true); FitDashboard(); if(withTray) { Shown+=delegate { RefreshStateAsync(); }; } else RefreshState();
  }
  static Label LabelOf(string text,float size,bool bold) { return new Label { Text=text,Dock=DockStyle.Fill,Font=new Font("Segoe UI",size,bold?FontStyle.Bold:FontStyle.Regular),AutoEllipsis=true }; }
  SoftButton ButtonOf(string text,EventHandler action) { var button=new SoftButton { Text=text,Width=140 }; button.Click+=action; return button; }
  TableLayoutPanel Section(TableLayoutPanel parent,string title,int[] rows) {
   var box=new SoftPanel { Dock=DockStyle.Fill,Margin=new Padding(0,0,0,10),Padding=new Padding(14,8,14,8) }; parent.Controls.Add(box);
   var content=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=1,RowCount=rows.Length,BackColor=Color.Transparent,Margin=Padding.Empty };
   foreach(int height in rows) content.RowStyles.Add(new RowStyle(SizeType.Absolute,height));
   content.Controls.Add(LabelOf(title,11,true),0,0); box.Controls.Add(content); return content;
  }
  void BuildGeneral(TableLayoutPanel layout) {
   var content=Section(layout,L.T("Bağlantı profili"),new[]{24,44,24,20});
   var selection=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=3,RowCount=1,Margin=Padding.Empty };
   selection.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,Program.ZapretEnabled?150:0)); selection.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); selection.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,122));
   motors.Dock=DockStyle.Top; motors.AccessibleName=L.T("DPI motoru"); motors.Visible=Program.ZapretEnabled; motors.Items.AddRange(Program.ZapretEnabled?new[]{"GoodbyeDPI","Zapret 2"}:new[]{"GoodbyeDPI"});
   motors.SelectedIndexChanged+=delegate { profiles.SelectedIndex=-1; profiles.Items.Clear(); foreach(int id in Program.MotorProfiles(motors.SelectedIndex==1?Program.ZapretStart:0)) profiles.Items.Add(Program.Names[id].Replace("Zapret 2 · ","")); profiles.SelectedIndex=0; UpdateSelection(); };
   selection.Controls.Add(motors,0,0); motors.SelectedIndex=0;
   profiles.Dock=DockStyle.Top;
   profiles.SelectedIndexChanged+=delegate { UpdateSelection(); }; selection.Controls.Add(profiles,1,0);
   apply.Text=L.T("Profili uygula"); apply.Dock=DockStyle.Top; apply.Margin=new Padding(10,3,0,3); apply.Click+=delegate { Execute("profile",SelectedProfile); }; selection.Controls.Add(apply,2,0); content.Controls.Add(selection);
   hint.Dock=DockStyle.Fill; hint.Tag="muted"; content.Controls.Add(hint);
   pending.Dock=DockStyle.Fill; pending.Tag="muted"; content.Controls.Add(pending);
  }
  void BuildSettings(TableLayoutPanel layout) {
   var content=Section(layout,L.T("Otomatik başlangıç"),new[]{28,28,26,28,26}); startupContent=content;
   engineStartup.Text=L.T("Windows açıldığında GoodbyeDPI'ı başlat"); engineStartup.AutoSize=true; content.Controls.Add(engineStartup,0,1); var serviceHelp=LabelOf(L.T("Bilgisayar açılınca GoodbyeDPI ve tepsi simgesi de başlar."),9,false); serviceHelp.Tag="muted"; content.Controls.Add(serviceHelp,0,2);
   startup.Text=L.T("Sistem tepsisinde göster"); startup.AutoSize=true; content.Controls.Add(startup,0,3); trayHelp.Text=TrayHelp(true); trayHelp.Tag="muted"; content.Controls.Add(trayHelp,0,4);
   // Starting with Windows covers both parts: the service and the tray entry in Task Manager follow this box together.
   // Execute changes the tray entry only after the service change worked, so the two never drift apart.
   engineStartup.CheckedChanged+=delegate {
    if(refreshing) return;
    Execute(engineStartup.Checked?"auto-on":"auto-off",0);
   };
   startup.CheckedChanged+=delegate {
    if(refreshing) return;
    // GoodbyeDPI starting with Windows but no icon means nothing on screen tells the user it runs; ask first.
    if(!startup.Checked && engineStartup.Checked && !ConfirmTrayOff()) { refreshing=true; startup.Checked=true; refreshing=false; return; }
    try {
     if(startup.Checked) Program.RequireOwned();
     WindowsIntegration.SetTrayStartup(startup.Checked);
     notice.Text=startup.Checked?L.T("Tepsi simgesi açıldı, oturum açınca da gelecek."):L.T("Tepsi simgesi gizlendi.");
    } catch(Exception ex) { ReportError(ex); } RefreshState();
   };
  }
  // Replaceable so tests can answer without a dialog.
  internal Func<bool> ConfirmTrayOff;
  bool AskTrayOff() { return MessageBox.Show(this,L.T("GoodbyeDPI bilgisayar açılınca arka planda çalışmaya devam edecek, ama simge olmadan çalıştığını göremezsin. Durumunu görmek için uygulamayı Başlat menüsünden açman gerekir.\n\nSimge yine de gizlensin mi?"),L.T("Simgeyi gizle"),MessageBoxButtons.YesNo,MessageBoxIcon.Question,MessageBoxDefaultButton.Button2)==DialogResult.Yes; }
  void BuildTools(TableLayoutPanel layout) {
   var tools=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=4,RowCount=2,Margin=Padding.Empty };
   tools.RowStyles.Add(new RowStyle(SizeType.Absolute,48)); tools.RowStyles.Add(new RowStyle(SizeType.Absolute,48));
   for(int i=0;i<4;i++) tools.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25f));
   restart.Glyph="restart"; restart.Text=L.T("Yeniden başlat"); restart.Dock=DockStyle.Top; restart.Margin=new Padding(0,4,8,4); restart.Click+=delegate { Execute("restart",0); }; tools.Controls.Add(restart);
   var diagnostics=ButtonOf(L.T("Konsol"),delegate { ShowDiagnostics(); }); diagnostics.Glyph="console"; diagnostics.Dock=DockStyle.Top; diagnostics.Margin=new Padding(4,4,8,4); tools.Controls.Add(diagnostics);
   var access=ButtonOf(L.T("Erişim testi"),delegate { ShowAccessTest(); }); access.Glyph="globe"; access.Dock=DockStyle.Top; access.Margin=new Padding(4,4,8,4); tools.Controls.Add(access);
   var uninstall=ButtonOf(L.T("Uygulamayı kaldır"),delegate {
    if(busy) return;
    try { Process.Start(new ProcessStartInfo(Path.Combine(Program.InstallDir,"GoodbyeDPI.Tray.exe"),"--uninstall") { UseShellExecute=true }); }
    catch(Exception ex) { ReportError(ex); }
   }); uninstall.Glyph="trash"; uninstall.Dock=DockStyle.Top; uninstall.Margin=new Padding(4,4,0,4); tools.Controls.Add(uninstall);
   findProfile.Text=L.T("Uygun profil bul"); findProfile.Dock=DockStyle.Top; findProfile.Margin=new Padding(0,4,8,4); findProfile.Click+=delegate { FindProfile(); }; tools.Controls.Add(findProfile,0,1); tools.SetColumnSpan(findProfile,2);
   pauseButton.Text=L.T("Geçici durdur"); pauseButton.Dock=DockStyle.Top; pauseButton.Margin=new Padding(4,4,0,4); pauseButton.Click+=delegate { ShowPauseMenu(); }; tools.Controls.Add(pauseButton,2,1); tools.SetColumnSpan(pauseButton,2);
   layout.Controls.Add(tools);
  }
  bool TrayShown { get { return tray!=null && tray.Visible; } }
  // The tray setting also decides whether the icon shows now. Without it the window acts like a normal app: minimize goes to the taskbar, closing quits.
  static string TrayHelp(bool shown) { return shown?L.T("Simge saatin yanında durur, oturum açınca da kendiliğinden gelir. Pencereyi kapatınca simgeye küçülür."):L.T("Simge gizli. Pencereyi kapatınca uygulama kapanır; GoodbyeDPI çalışıyorsa çalışmaya devam eder."); }
  void SetTrayVisible(bool show) {
   if(tray==null) return;
   trayHelp.Text=TrayHelp(show);
   if(tray.Visible==show) return;
   tray.Visible=show;
   if(!show && !Visible) Reveal();
  }
  void HideToTray() {
   if(!TrayShown) return;
   Hide(); ShowInTaskbar=false;
  }
  // A manual check (from About) runs even when automatic checks are turned off.
  async void CheckForUpdateAsync(bool manual=false) {
   if(updateChecking || IsDisposed) return;
   if(!manual && !UpdateCheck.Enabled) { nextUpdateCheck=DateTime.UtcNow.AddHours(1); return; }
   updateChecking=true; SetUpdateState(UpdateState.Checking);
   try {
    string latest=await Task.Run(delegate { return UpdateCheck.Fetch(); });
    nextUpdateCheck=DateTime.UtcNow.AddDays(1);
    if(IsDisposed) return;
    if(latest!=null) ShowUpdate(latest); else SetUpdateState(UpdateState.Current);
   } catch(Exception ex) { nextUpdateCheck=DateTime.UtcNow.AddHours(1); AppInfo.Log("Güncelleme denetimi: "+ex.Message); if(!IsDisposed) SetUpdateState(UpdateState.Failed); }
   finally { updateChecking=false; }
  }
  void SetUpdateState(UpdateState state) { updateState=state; if(state==UpdateState.Current) availableUpdate=null; var changed=UpdateStateChanged; if(changed!=null) changed(); }
  internal void ShowUpdate(string version) {
   availableUpdate=version; SetUpdateState(UpdateState.Available);
   // Each release is announced once, and only when there is a tray icon to show it.
   if(TrayShown && UpdateCheck.NotifiedVersion!=version) {
    tray.ShowBalloonTip(5000,"GoodbyeDPI Tray "+version+L.T(" çıktı"),L.T("Hakkında")+" → "+L.T("Neler değişti?"),ToolTipIcon.Info);
    try { UpdateCheck.NotifiedVersion=version; } catch(Exception ex) { AppInfo.Log(ex.ToString()); }
   }
  }
  void ShowReleaseNotes() {
   ShowReleaseNotes(availableUpdate ?? AppInfo.Version,false);
  }
  internal void ShowStartupReleaseNotes() {
   if(UpdateCheck.ShouldShowInstalledNotes(UpdateCheck.SeenNotesVersion,AppInfo.Version)) ShowReleaseNotes(AppInfo.Version,true);
  }
  bool notesOpen;
  void ShowReleaseNotes(string version,bool remember) {
   if(notesOpen) return;
   if(!Visible || WindowState==FormWindowState.Minimized) Reveal();
   using(var dialog=new ReleaseNotesForm(dark,version,version==AppInfo.Version?UpdateCheck.InstalledNotes():UpdateCheck.NotesVersion==version?UpdateCheck.ReleaseNotes:null)) {
    if(remember) dialog.Shown+=delegate { try { UpdateCheck.SeenNotesVersion=version; } catch(Exception ex) { AppInfo.Log("Sürüm notları tercihi: "+ex.Message); } };
    notesOpen=true; try { if(dialog.ShowDialog(Form.ActiveForm ?? this)==DialogResult.OK) DownloadUpdate(); } finally { notesOpen=false; }
   }
  }
  void DownloadUpdate() {
   string url=UpdateCheck.DownloadUrl;
   try { Process.Start(new ProcessStartInfo(url) { UseShellExecute=true }); notice.Text=url==UpdateCheck.ReleaseUrl?L.T("Sürüm sayfası açıldı, kurucuyu oradan indirebilirsin."):L.T("Kurucu tarayıcında iniyor. Açıp kurman yeterli, ayarların korunur."); }
   catch(Exception ex) { ReportError(ex); }
  }
  void Reveal() { Show(); ShowInTaskbar=true; WindowState=FormWindowState.Normal; Activate(); }
  internal void DispatchCommand(string command) {
   if(command=="show") Reveal();
   else if(command=="profiles") { Reveal(); profiles.Focus(); }
   else if(command=="settings") { Reveal(); engineStartup.Focus(); }
   else if(command=="exit") { if(busy) { notice.Text=L.T("İşlem sürüyor; tamamlandıktan sonra çıkabilirsin."); return; } quitting=true; Close(); }
   else if(command=="start" || command=="stop" || command=="restart") { if(!available) { Reveal(); notice.Text=L.T("Önce kurulumu tamamla."); } else if(!busy && !transition) Execute(command,0); }
  }
  internal void SetServiceState(int profile,ServiceControllerStatus state) {
   int previous=activeProfile; activeProfile=profile; available=profile>=0;
   running=available && state==ServiceControllerStatus.Running;
   DateTime? resumeAt=running?EngineConsole.PauseUntil():null; bool paused=resumeAt.HasValue;
   if(paused) running=false;
   transition=available && state!=ServiceControllerStatus.Running && state!=ServiceControllerStatus.Stopped;
   if(available && (previous<0 || SelectedProfile==previous)) SelectProfile(profile);
   status.Text=!available?L.T("Kurulum gerekiyor"):running?L.T("GoodbyeDPI çalışıyor"):transition?L.T("Durum değişiyor…"):L.T("GoodbyeDPI durduruldu");
   detail.Text=available?L.T("Etkin profil: ")+Program.Names[profile]:L.T("GoodbyeDPI-Installer.exe'yi açıp kurman gerekiyor.");
   if(paused) { status.Text=L.T("Geçici olarak durduruldu"); detail.Text=L.T("Moladan dönüş: ")+resumeAt.Value.ToLocalTime().ToString("HH:mm:ss"); }
   toggle.Text=busy?L.T("İşlem sürüyor…"):running?L.T("Durdur"):L.T("Başlat");
   toggle.Enabled=available && !busy && !transition;
   restart.Enabled=available && running && !busy && !transition;
   profiles.Enabled=available && !busy && !transition;
   motors.Enabled=profiles.Enabled;
   if(available && profile>=Program.ZapretStart) status.Text=status.Text.Replace("GoodbyeDPI","Zapret 2");
   findProfile.Enabled=available && !busy && !transition && !paused;
   pauseButton.Enabled=available && (running || paused) && !busy && !transition;
   pauseButton.Text=paused?L.T("Molayı yönet"):L.T("Geçici durdur");
   trayPause.Enabled=available && running && !busy && !transition;
   engineStartup.Text=L.T("Windows açıldığında GoodbyeDPI'ı başlat").Replace("GoodbyeDPI",Program.MotorName(activeProfile));
   engineStartup.Enabled=available && !busy && !transition; startup.Enabled=!busy;
   UpdateSelection();
   PaintButton(toggle,true);
   if(tray!=null) {
    tray.Text=available?(running?L.T("GoodbyeDPI • Çalışıyor"):L.T("GoodbyeDPI • Durduruldu")):L.T("GoodbyeDPI • Kurulum gerekiyor");
    if(available) tray.Text=tray.Text.Replace("GoodbyeDPI",Program.MotorName(activeProfile));
    trayToggle.Enabled=available && !running && !busy && !transition; trayStop.Enabled=(running || paused) && !busy && !transition; trayRestart.Enabled=restart.Enabled; trayProfiles.Enabled=available && !busy && !transition;
    for(int i=0;i<trayProfiles.DropDownItems.Count;i++) { var item=(ToolStripMenuItem)trayProfiles.DropDownItems[i]; item.Checked=i==activeProfile; item.Enabled=i!=activeProfile; }
   }
  }
  void UpdateSelection() {
   int i=SelectedProfile;
   hint.Text=(i>=Program.ZapretStart||i==1||i==2||i==6)?L.T("Bu profil DNS'e dokunmaz; gerekirse Windows'tan DNS ayarla."):L.T("DNS'i kendisi yönlendirir, Windows ayarların değişmez.");
   if(i==10) hint.Text=L.T("Superonline için TLS alternatifi; DNS ayarlarını değiştirmez.");
   if(i==11) hint.Text=L.T("QUIC başlangıcını engeller; destekleyen uygulamalar TCP'ye döner. DNS değişmez.");
   bool dirty=available && i!=activeProfile;
   pending.Text=dirty?L.T("Seçtiğin profil henüz uygulanmadı."):available?L.T("Bu profil şu an kullanılıyor."):L.T("Kurulumdan sonra profil seçebilirsin.");
   apply.Enabled=dirty && !busy && !transition;
  }
  async void Execute(string action,int profile) {
   if(busy) return;
   stateRevision++; busy=true; notice.Text=L.T("Bir saniye…"); SetServiceState(activeProfile,running?ServiceControllerStatus.Running:ServiceControllerStatus.Stopped);
   string done=action.StartsWith("pause")?L.T("GoodbyeDPI molada. Süre bitince geri gelecek."):action=="profile"?L.T("Profil uygulandı."):action=="stop"?L.T("GoodbyeDPI durduruldu."):action=="auto-on"||action=="auto-off"?L.T("Başlangıç tercihin kaydedildi."):L.T("GoodbyeDPI başlatıldı.");
   done=done.Replace("GoodbyeDPI",Program.MotorName(action=="profile"?profile:activeProfile));
   try {
    Program.RequireOwned();
    bool applied=await Task.Run(delegate { return UserServiceControl.TryRun(action,profile); });
    if(applied) notice.Text=done;
    else {
     // Installs from before 1.5.4 do not let users control the service yet; the next update fixes that.
     notice.Text=L.T("Windows izin isteyebilir, bekle…");
     using(var process=Process.Start(new ProcessStartInfo(Path.Combine(Program.InstallDir,"GoodbyeDPI.Tray.exe"),"--admin "+action+" "+profile) { UseShellExecute=true,Verb="runas",WorkingDirectory=Program.InstallDir })) {
      await Task.Run(delegate { process.WaitForExit(); });
      applied=process.ExitCode==0; notice.Text=applied?done:L.T("Olmadı. Hata penceresinde nedenini görebilirsin.");
     }
    }
    if(applied && (action=="auto-on" || action=="auto-off")) WindowsIntegration.SetTrayStartup(action=="auto-on");
   } catch(System.ComponentModel.Win32Exception ex) { if(ex.NativeErrorCode==1223) notice.Text=L.T("İzin verilmedi, hiçbir şey değişmedi."); else ReportError(ex); }
   catch(Exception ex) { ReportError(ex); }
   finally { busy=false; if(!IsDisposed) RefreshState(); }
  }
  void ReportError(Exception ex) { AppInfo.Log(ex.ToString()); notice.Text=L.T("İşlem tamamlanamadı: ")+ex.Message; MessageBox.Show(this,ex.Message,L.T("İşlem tamamlanamadı"),MessageBoxButtons.OK,MessageBoxIcon.Warning); }
  void ShowAbout() { using(var dialog=CreateAboutDialog()) dialog.ShowDialog(this); }
  internal Form CreateAboutDialog() {
   var dialog=new Form { Text=L.T("GoodbyeDPI — Hakkında"),ClientSize=new Size(540,444),Font=Font,BackColor=background,ForeColor=foreground,Icon=Program.AppIcon,StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false };
   var heading=new Label { Text="GoodbyeDPI Tray",Font=new Font(Font.FontFamily,20,FontStyle.Bold),Location=new Point(24,20),Size=new Size(490,40) }; dialog.Controls.Add(heading);
   dialog.Controls.Add(new Label { Text=L.T("Sürüm ")+AppInfo.Version+L.T("  ·  Geliştirici: Furkan"),Location=new Point(26,66),Size=new Size(488,26),ForeColor=secondary });
   dialog.Controls.Add(new Label { Text=AppInfo.Purpose,Location=new Point(26,108),Size=new Size(488,82) });
   Color linkColor=SystemInformation.HighContrast?SystemColors.HotTrack:dark?AppPalette.Mint:AppPalette.Accent;
   var developer=ProjectLinks.Create(L.T("Geliştirici · Furkan / trfurkan"),AppInfo.DeveloperUrl,linkColor); developer.SetBounds(26,204,488,34); dialog.Controls.Add(developer);
   dialog.Controls.Add(new Label { Text=L.T("Motor: GoodbyeDPI-Turkey 0.2.3rc3"),Location=new Point(26,260),Size=new Size(488,26),ForeColor=secondary });
   var licenses=new LinkLabel { Text=L.T("Lisanslar"),Location=new Point(26,290),Size=new Size(488,28),LinkBehavior=LinkBehavior.HoverUnderline,LinkColor=linkColor,ActiveLinkColor=linkColor,VisitedLinkColor=linkColor };
   licenses.LinkClicked+=delegate { using(var window=new LicenseForm(Path.Combine(Program.InstallDir,"licenses"),dark,background,surface,foreground,secondary,border)) window.ShowDialog(dialog); };
   dialog.Controls.Add(licenses);
   var notesLink=new LinkLabel { Text=L.T("Neler değişti?"),Location=new Point(26,318),Size=new Size(488,26),LinkBehavior=LinkBehavior.HoverUnderline,LinkColor=linkColor,ActiveLinkColor=linkColor,VisitedLinkColor=linkColor };
   notesLink.LinkClicked+=delegate { ShowReleaseNotes(); }; dialog.Controls.Add(notesLink);
   var statusIcon=new UpdateStatusIcon { Location=new Point(26,350),Size=new Size(22,22),BackColor=background };
   var statusText=new Label { Location=new Point(56,346),Size=new Size(300,30),TextAlign=ContentAlignment.MiddleLeft };
   var checkNow=new LinkLabel { Location=new Point(356,346),Size=new Size(158,30),TextAlign=ContentAlignment.MiddleRight,LinkBehavior=LinkBehavior.HoverUnderline,LinkColor=linkColor,ActiveLinkColor=linkColor,VisitedLinkColor=linkColor,DisabledLinkColor=secondary };
   checkNow.LinkClicked+=delegate { CheckForUpdateAsync(true); };
   Action showState=delegate {
    if(dialog.IsDisposed) return;
    notesLink.Visible=true;
    statusIcon.State=updateState;
    statusText.Text=updateState==UpdateState.Current?L.T("Güncel sürümü kullanıyorsun."):updateState==UpdateState.Available?L.T("Yeni sürüm var: ")+availableUpdate:updateState==UpdateState.Checking?"Kontrol ediliyor…":updateState==UpdateState.Failed?"Kontrol edilemedi, internetini kontrol et.":L.T("Güncellemeler henüz kontrol edilmedi.");
    checkNow.Text=updateState==UpdateState.Failed?L.T("Tekrar dene"):L.T("Şimdi kontrol et");
    checkNow.Enabled=updateState!=UpdateState.Checking;
   };
   showState(); UpdateStateChanged+=showState; dialog.Disposed+=delegate { UpdateStateChanged-=showState; };
   dialog.Controls.Add(statusIcon); dialog.Controls.Add(statusText); dialog.Controls.Add(checkNow);
   var updates=new CheckBox { Text=L.T("Yeni sürüm çıkınca haber ver"),Location=new Point(26,390),Size=new Size(488,30),Checked=UpdateCheck.Enabled };
   updates.CheckedChanged+=delegate { try { UpdateCheck.Enabled=updates.Checked; if(updates.Checked) nextUpdateCheck=DateTime.UtcNow; } catch(Exception ex) { ReportError(ex); } };
   dialog.Controls.Add(updates);
   // Opening About in the running app checks right away if nothing is known yet.
   if(commands!=null && updateState==UpdateState.Unknown) dialog.Shown+=delegate { CheckForUpdateAsync(true); };
   dialog.HandleCreated+=delegate { WindowsIntegration.ThemeCaption(dialog,dark,background,foreground); }; return dialog;
  }
  void FitDashboard() {
   if(dashboard.RowStyles.Count<8 || startupContent==null) return;
   bool compact=ClientSize.Height<860;
   // The tray help stays visible even when compact: it says what closing the window does.
   int[] heights=compact?new[]{64,96,0,144,142,96}:new[]{104,112,0,160,164,106};
   dashboard.SuspendLayout(); startupContent.SuspendLayout(); footerPanel.SuspendLayout();
   try {
    for(int i=0;i<heights.Length;i++) if(dashboard.RowStyles[i].Height!=heights[i]) dashboard.RowStyles[i].Height=heights[i];
    startupContent.RowStyles[2].Height=compact?0:26; var serviceHelp=startupContent.GetControlFromPosition(0,2); if(serviceHelp!=null) serviceHelp.Visible=!compact;
   } finally { startupContent.ResumeLayout(); footerPanel.ResumeLayout(); dashboard.ResumeLayout(); }
  }
  int stateRevision;
  async void RefreshStateAsync() {
   if(stateReadPending || busy || IsDisposed) return;
   stateReadPending=true; int revision=stateRevision;
   try {
    var snapshot=await Task.Run(delegate {
     bool userStart=WindowsIntegration.TrayStartupEnabled(),engineStart;
     using(var key=Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\GoodbyeDPI")) engineStart=key!=null && Convert.ToInt32(key.GetValue("Start",3))==2;
     int profile=Program.Profile(); var state=ServiceControllerStatus.Stopped; if(profile>=0) using(var service=new ServiceController(Program.ServiceName)) state=service.Status;
     return Tuple.Create(userStart,engineStart,profile,state,profile<0 && Program.ServicePath()!=null);
    });
    if(IsDisposed || Disposing || busy || sizing || revision!=stateRevision) return;
    refreshing=true;
    try {
     bool changed=startup.Checked!=snapshot.Item1 || engineStartup.Checked!=snapshot.Item2 || activeProfile!=snapshot.Item3 || running!=(snapshot.Item3>=0 && snapshot.Item4==ServiceControllerStatus.Running) || transition!=(snapshot.Item3>=0 && snapshot.Item4!=ServiceControllerStatus.Running && snapshot.Item4!=ServiceControllerStatus.Stopped);
     startup.Checked=snapshot.Item1; engineStartup.Checked=snapshot.Item2; SetTrayVisible(snapshot.Item1);
     if(changed || String.IsNullOrEmpty(status.Text) || snapshot.Item4==ServiceControllerStatus.Running) SetServiceState(snapshot.Item3,snapshot.Item4);
     if(snapshot.Item5) detail.Text=L.T("Farklı bir GoodbyeDPI kurulumu bulundu.");
    } finally { refreshing=false; }
   } catch(Exception ex) { if(!IsDisposed && !Disposing && !busy) { SetServiceState(-1,ServiceControllerStatus.Stopped); status.Text=L.T("Durum okunamadı"); detail.Text=ex.Message; } }
   finally { stateReadPending=false; }
  }  void RefreshState() {
   stateRevision++;
   refreshing=true;
   try {
    startup.Checked=WindowsIntegration.TrayStartupEnabled(); SetTrayVisible(startup.Checked);
    using(var key=Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\GoodbyeDPI")) engineStartup.Checked=key!=null && Convert.ToInt32(key.GetValue("Start",3))==2;
    int active=Program.Profile(); var state=ServiceControllerStatus.Stopped;
    if(active>=0) using(var svc=new ServiceController(Program.ServiceName)) state=svc.Status;
    SetServiceState(active,state);
    if(active<0 && Program.ServicePath()!=null) detail.Text=L.T("Farklı bir GoodbyeDPI kurulumu bulundu.");
   } catch(Exception ex) { SetServiceState(-1,ServiceControllerStatus.Stopped); status.Text=L.T("Durum okunamadı"); detail.Text=ex.Message; }
   finally { refreshing=false; }
  }
  void ShowDiagnostics() {
   try {
    if(diagnosticsWindow!=null && !diagnosticsWindow.IsDisposed) { diagnosticsWindow.Show(); diagnosticsWindow.Activate(); return; }
    diagnosticsWindow=new EngineConsoleForm(dark); diagnosticsWindow.Show(this);
   } catch(Exception ex) { ReportError(ex); }
  }
  void ShowAccessTest() {
   try {
    if(accessWindow!=null && !accessWindow.IsDisposed) { accessWindow.Show(); accessWindow.Activate(); return; }
    accessWindow=new AccessTestForm(dark,delegate { return running; }); accessWindow.Show(this);
   } catch(Exception ex) { ReportError(ex); }
  }
  void ThemeChanged(object sender,UserPreferenceChangedEventArgs e) { if(IsHandleCreated && !IsDisposed) { try { BeginInvoke((MethodInvoker)delegate { if(!IsDisposed) ApplyTheme(true); }); } catch(InvalidOperationException) {} } }
  bool WindowsDark() { try { using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize")) return key!=null && Convert.ToInt32(key.GetValue("AppsUseLightTheme",1))==0; } catch { return false; } }
  internal void SetThemePreference(string value,bool persist) {
   if(Array.IndexOf(ThemeModes,value)<0) throw new ArgumentException(L.T("Geçersiz tema."));
   if(persist) using(var key=Registry.CurrentUser.CreateSubKey(PreferencesKey)) key.SetValue("Theme",value);
   themePreference=value; ApplyTheme(true);
  }
  void ApplyTheme(bool force) {
   bool next=themeOverride??(themePreference=="system"?WindowsDark():AppPalette.IsDark(themePreference)); if(!force && next==dark && !SystemInformation.HighContrast) return; dark=next;
   AppPalette.Variant=themeOverride==null?AppPalette.VariantOf(themePreference):"black";
   themeButton.Text=ThemeName(themePreference)+"  ▾";
   var oldSwatch=themeButton.GlyphImage; themeButton.GlyphImage=ThemeSwatch(themePreference); if(oldSwatch!=null) oldSwatch.Dispose(); themeButton.Invalidate();
   background=AppPalette.Background(dark); surface=AppPalette.Surface(dark); foreground=AppPalette.Text(dark); secondary=AppPalette.Muted(dark); border=AppPalette.Border(dark);
   if(SystemInformation.HighContrast) { background=SystemColors.Window; surface=SystemColors.Window; foreground=SystemColors.WindowText; secondary=SystemColors.WindowText; border=SystemColors.WindowText; }
   BackColor=background; PaintControls(this); PaintButton(toggle,true); WindowsIntegration.ThemeCaption(this,dark,background,foreground);
   if(diagnosticsWindow!=null && !diagnosticsWindow.IsDisposed) { ((EngineConsoleForm)diagnosticsWindow).ApplyPalette(dark); }
   if(accessWindow!=null && !accessWindow.IsDisposed) ((AccessTestForm)accessWindow).ApplyPalette(dark);
   if(tray!=null) MenuAppearance.Apply(tray.ContextMenuStrip,surface,foreground,border,300);
   Invalidate(true);
  }
  void PaintButton(SoftButton button,bool primary) { button.Fill=primary?AppPalette.Accent:surface; button.Ink=primary?AppPalette.AccentInk:foreground; button.Edge=primary?button.Fill:border; if(SystemInformation.HighContrast) { button.Fill=primary?foreground:background; button.Ink=primary?background:foreground; } Color host=background; for(Control parent=button.Parent;parent!=null;parent=parent.Parent) { if(parent is SoftPanel) { host=surface; break; } } button.BackColor=host; button.ForeColor=button.Ink; button.Invalidate(); }
  protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); ApplyTheme(true); }
  void PaintControls(Control parent) {
   foreach(Control control in parent.Controls) {
    var panel=control as SoftPanel; var button=control as SoftButton;
    control.ForeColor=Convert.ToString(control.Tag)=="muted"?secondary:foreground;
    if(panel!=null) { panel.Fill=surface; panel.Edge=border; panel.BackColor=background; }
    else if(button!=null) PaintButton(button,button==apply);
    else if(control is NoticeView) { var terms=(NoticeView)control; terms.BackColor=surface; terms.ForeColor=foreground; terms.ThumbColor=secondary; terms.EdgeColor=border; terms.Invalidate(); }
    else if(control is LinkLabel) { var link=(LinkLabel)control; link.LinkColor=link.ActiveLinkColor=link.VisitedLinkColor=dark?AppPalette.Mint:AppPalette.Accent; link.BackColor=Color.Transparent; }
    else if(control is ComboBox || control is TextBox) control.BackColor=surface;
    else if(control is Label || control is CheckBox || control is TableLayoutPanel || control is FlowLayoutPanel) control.BackColor=Color.Transparent;
    else control.BackColor=background;
    PaintControls(control);
   }
  }
  bool resourcesDisposed;
  protected override void Dispose(bool disposing) {
   if(disposing && !resourcesDisposed) { resourcesDisposed=true; if(diagnosticsWindow!=null) diagnosticsWindow.Dispose(); if(accessWindow!=null) accessWindow.Dispose(); SystemEvents.UserPreferenceChanged-=ThemeChanged; timer.Dispose(); themeMenu.Dispose(); pauseMenu.Dispose(); if(commands!=null) commands.Dispose(); if(reveal!=null) reveal.Dispose(); if(tray!=null) { tray.Visible=false; tray.ContextMenuStrip.Dispose(); tray.Dispose(); } }
   base.Dispose(disposing);
  }
 }
}
