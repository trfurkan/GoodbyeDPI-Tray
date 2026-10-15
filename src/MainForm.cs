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
   set { if(value < -1 || value>=Items.Count) throw new ArgumentOutOfRangeException("value"); if(selected==value) return; selected=value; Text=value<0?"Profil seç  ▾":Items[value]+"  ▾"; if(SelectedIndexChanged!=null) SelectedIndexChanged(this,EventArgs.Empty); Invalidate(); }
  }
  public ProfilePicker() { Click+=delegate { OpenMenu(); }; AccessibleName="Bağlantı profili"; }
  void OpenMenu() {
   if(IsDisposed || Disposing || !Enabled || menu.Visible) return;
   // The owner keeps the menu alive until disposal. Closed runs before all
   // WinForms dismissal/focus processing has finished; do not dispose there.
   if(menu.Items.Count==0) for(int i=0;i<Items.Count;i++) { int index=i; var item=new ToolStripMenuItem(Items[i]); item.Click+=delegate { SelectedIndex=index; }; menu.Items.Add(item); }
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
  protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Measure(); Invalidate(); }
  protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); Measure(); Invalidate(); }
  protected override void OnSizeChanged(EventArgs e) { base.OnSizeChanged(e); Measure(); }
  protected override void OnPaint(PaintEventArgs e) {
   e.Graphics.Clear(BackColor); using(var pen=new Pen(EdgeColor)) e.Graphics.DrawRectangle(pen,0,0,Width-1,Height-1);
   var saved=e.Graphics.Save(); e.Graphics.SetClip(new Rectangle(12,12,Math.Max(1,Width-44),ViewHeight));
   TextRenderer.DrawText(e.Graphics,Text,Font,new Rectangle(12,12-offset,Math.Max(1,Width-44),Math.Max(contentHeight,ViewHeight)),ForeColor,Flags|TextFormatFlags.PreserveGraphicsClipping); e.Graphics.Restore(saved);
   if(Limit>0) { e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias; using(var path=SoftButton.Round(Thumb,5)) using(var brush=new SolidBrush(ThumbColor)) e.Graphics.FillPath(brush,path); }
   if(Focused) ControlPaint.DrawFocusRectangle(e.Graphics,new Rectangle(3,3,Width-6,Height-6),ForeColor,BackColor);
  }
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
 class MainForm : Form {
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
  readonly CheckBox startup=new CheckBox(), engineStartup=new CheckBox();
  readonly SoftButton toggle=new SoftButton(), apply=new SoftButton(), restart=new SoftButton();


  readonly TableLayoutPanel dashboard=new BufferedLayout();
  readonly TableLayoutPanel footerPanel=new BufferedLayout();

  TableLayoutPanel startupContent;
  bool sizing,stateReadPending;
  Form diagnosticsWindow,accessWindow;
  readonly SoftButton themeButton=new SoftButton();
  readonly ContextMenuStrip themeMenu=new ContextMenuStrip { ShowImageMargin=false,ShowCheckMargin=true,MinimumSize=new Size(240,0),Padding=new Padding(5),Font=new Font("Segoe UI",10) };
  string themePreference="system";
  const string PreferencesKey=@"Software\GoodbyeDPITray";
  bool quitting,refreshing,busy,running,available,transition,hideNotified,dark;
  int activeProfile=-1;
  readonly bool? themeOverride;
  Color background,surface,foreground,secondary,border;

  public MainForm(bool withTray) : this(withTray,null) {}
  public MainForm(bool withTray,bool? previewDark) {
   themeOverride=previewDark; Text="GoodbyeDPI"; FormBorderStyle=FormBorderStyle.Sizable; ControlBox=true; MinimizeBox=true; MaximizeBox=true; Font=new Font("Segoe UI",10); AutoScaleMode=AutoScaleMode.Dpi; DoubleBuffered=true; SetStyle(ControlStyles.ResizeRedraw,true);
   try { using(var key=Registry.CurrentUser.OpenSubKey(PreferencesKey)) { string saved=key==null?"system":Convert.ToString(key.GetValue("Theme","system")); if(saved=="light" || saved=="dark") themePreference=saved; } } catch { }
   ClientSize=new Size(900,Math.Min(840,Screen.PrimaryScreen.WorkingArea.Height-70)); MinimumSize=new Size(760,740); StartPosition=FormStartPosition.CenterScreen; Icon=Program.AppIcon;
   var scroll=new Panel { Dock=DockStyle.Fill,AutoScroll=false }; Controls.Add(scroll);
   var layout=dashboard; layout.Dock=DockStyle.Fill; layout.AutoSize=false; layout.Padding=new Padding(20,10,20,10); layout.ColumnCount=1; layout.RowCount=8;
   foreach(int height in new[]{104,112,30,160,164,58}) layout.RowStyles.Add(new RowStyle(SizeType.Absolute,height));
   layout.RowStyles.Add(new RowStyle(SizeType.Absolute,0)); layout.RowStyles.Add(new RowStyle(SizeType.Percent,100)); scroll.Controls.Add(layout);
   var header=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=3,RowCount=1 };
   header.RowStyles.Add(new RowStyle(SizeType.Percent,100)); header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,140)); header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,126));
      var brand=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,RowCount=1 };
   brand.RowStyles.Add(new RowStyle(SizeType.Percent,100)); brand.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,90)); brand.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
   var logo=new Panel { Dock=DockStyle.Fill }; logo.Paint+=delegate(object sender,PaintEventArgs e) { e.Graphics.DrawIcon(Program.AppIcon,new Rectangle(8,2,Math.Min(72,logo.Height-4),Math.Min(80,logo.Height-4))); }; brand.Controls.Add(logo);
   var brandTitle=LabelOf("GoodbyeDPI",27,true); brandTitle.TextAlign=ContentAlignment.MiddleLeft; brand.Controls.Add(brandTitle); header.Controls.Add(brand,0,0);
   string[] modes={"system","light","dark"}; string[] labels={"Sistem teması","Aydınlık","Karanlık"};
   for(int i=0;i<modes.Length;i++) { string choice=modes[i]; var item=new ToolStripMenuItem(labels[i]) { Tag=choice,AutoSize=false,Size=new Size(220,40),Padding=new Padding(8,6,12,6) }; item.Click+=delegate { try { SetThemePreference(choice,true); notice.Text="Tema kaydedildi."; } catch(Exception ex) { ReportError(ex); } }; themeMenu.Items.Add(item); }
   themeButton.Dock=DockStyle.Top; themeButton.AccessibleName="Görünüm teması";
   themeButton.Click+=delegate { foreach(ToolStripMenuItem item in themeMenu.Items) { item.Checked=Convert.ToString(item.Tag)==themePreference; item.BackColor=surface; item.ForeColor=foreground; } themeMenu.BackColor=surface; themeMenu.ForeColor=foreground; themeMenu.Renderer=new MenuRenderer(surface,foreground,border); if(!themeMenu.Visible) themeMenu.Show(themeButton,new Point(0,themeButton.Height)); };
   header.Controls.Add(themeButton,1,0);
   var trayButton=ButtonOf("Tepsiye gizle",delegate { HideToTray(); }); trayButton.Dock=DockStyle.Top; header.Controls.Add(trayButton,2,0); trayButton.Visible=withTray; layout.Controls.Add(header);
   var hero=new SoftPanel { Dock=DockStyle.Fill,Margin=new Padding(0,0,0,8),Padding=new Padding(20,12,20,12) }; layout.Controls.Add(hero);
   var heroLayout=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,RowCount=2,BackColor=Color.Transparent };
   heroLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); heroLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,154)); heroLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,34)); heroLayout.RowStyles.Add(new RowStyle(SizeType.Percent,100)); hero.Controls.Add(heroLayout);
   status.Dock=DockStyle.Fill; status.Font=new Font(Font.FontFamily,16,FontStyle.Bold); status.TextAlign=ContentAlignment.MiddleLeft; heroLayout.Controls.Add(status,0,0);
   detail.Dock=DockStyle.Fill; detail.Tag="muted"; heroLayout.Controls.Add(detail,0,1);
   toggle.Dock=DockStyle.Top; toggle.Margin=new Padding(8,10,0,0); toggle.Click+=delegate { Execute(running?"stop":"start",0); }; heroLayout.Controls.Add(toggle,1,0); heroLayout.SetRowSpan(toggle,2);
   notice.Dock=DockStyle.Fill; notice.Text="Profilini seç, gerisini uygulama halleder."; notice.Tag="muted"; notice.Padding=new Padding(2,4,2,0); layout.Controls.Add(notice);
   BuildGeneral(layout); BuildSettings(layout); BuildTools(layout);
   footerPanel.Dock=DockStyle.Bottom; footerPanel.Height=40; footerPanel.ColumnCount=2; footerPanel.RowCount=1; footerPanel.Margin=Padding.Empty;
   footerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); footerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,100));
   footerPanel.RowStyles.Add(new RowStyle(SizeType.Percent,100));
   var developer=ProjectLinks.Create("Geliştirici: trfurkan",AppInfo.DeveloperUrl,AppPalette.Accent,true); developer.Dock=DockStyle.Fill; footerPanel.Controls.Add(developer,0,0);
   var about=new LinkLabel { Text="Hakkında",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleRight,LinkBehavior=LinkBehavior.HoverUnderline }; about.LinkClicked+=delegate { ShowAbout(); }; footerPanel.Controls.Add(about,1,0);
   layout.Controls.Add(footerPanel,0,7);
   if(withTray) {
    commands=new AppCommands();
    reveal=new EventWaitHandle(false,EventResetMode.AutoReset,RevealEvent);
    var menu=new ContextMenuStrip(); menu.Items.Add("GoodbyeDPI'ı aç",null,delegate { Reveal(); });
    trayToggle=new ToolStripMenuItem("Başlat",null,delegate { Execute("start",0); }); menu.Items.Add(trayToggle);
    trayStop=new ToolStripMenuItem("Durdur",null,delegate { Execute("stop",0); }); menu.Items.Add(trayStop);
    trayRestart=new ToolStripMenuItem("Yeniden başlat",null,delegate { Execute("restart",0); }); menu.Items.Add(trayRestart);
    trayProfiles=new ToolStripMenuItem("Profil seçimi");
    for(int i=0;i<Program.Names.Length;i++) { int index=i; trayProfiles.DropDownItems.Add(new ToolStripMenuItem(Program.Names[i],null,delegate { Execute("profile",index); })); } menu.Items.Add(trayProfiles);
    menu.Items.Add("Ayarlar",null,delegate { DispatchCommand("settings"); });
    menu.Items.Add(new ToolStripSeparator());
    menu.Items.Add("Arayüzden çık",null,delegate { DispatchCommand("exit"); });
    tray=new NotifyIcon { Icon=Program.AppIcon,Text="GoodbyeDPI",ContextMenuStrip=menu,Visible=true }; tray.DoubleClick+=delegate { Reveal(); };
   }
   Shown+=delegate { if(withTray) WindowsIntegration.RegisterJumpList(); if(WindowState==FormWindowState.Minimized && tray!=null) { Hide(); ShowInTaskbar=false; } };
   Resize+=delegate { if(WindowState==FormWindowState.Minimized && tray!=null) HideToTray(); };
   FormClosing+=delegate(object sender,FormClosingEventArgs e) { if(!quitting && e.CloseReason==CloseReason.UserClosing && tray!=null) { e.Cancel=true; HideToTray(); } };
   SystemEvents.UserPreferenceChanged+=ThemeChanged;
   timer.Interval=250; timer.Tick+=delegate { if(reveal!=null && reveal.WaitOne(0)) Reveal(); string command=commands==null?null:commands.Poll(); if(command!=null) DispatchCommand(command); if(++refreshTicks>=8) { refreshTicks=0; if(!busy && !sizing) RefreshStateAsync(); if(!sizing && Visible) ApplyTheme(false); } }; if(withTray) timer.Start();
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
   var content=Section(layout,"Bağlantı profili",new[]{24,44,24,20});
   var selection=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty };
   selection.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); selection.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,142));
   profiles.Dock=DockStyle.Top; profiles.Items.AddRange(Program.Names); profiles.SelectedIndex=0;
   profiles.SelectedIndexChanged+=delegate { UpdateSelection(); }; selection.Controls.Add(profiles);
   apply.Text="Profili uygula"; apply.Dock=DockStyle.Top; apply.Margin=new Padding(10,3,0,3); apply.Click+=delegate { Execute("profile",profiles.SelectedIndex); }; selection.Controls.Add(apply); content.Controls.Add(selection);
   hint.Dock=DockStyle.Fill; hint.Tag="muted"; content.Controls.Add(hint);
   pending.Dock=DockStyle.Fill; pending.Tag="muted"; content.Controls.Add(pending);
  }
  void BuildSettings(TableLayoutPanel layout) {
   var content=Section(layout,"Otomatik başlangıç",new[]{28,28,26,28,26}); startupContent=content;
   engineStartup.Text="Windows açıldığında GoodbyeDPI'ı başlat"; engineStartup.AutoSize=true; content.Controls.Add(engineStartup,0,1); var serviceHelp=LabelOf("Bilgisayar açılınca GoodbyeDPI da çalışır.",9,false); serviceHelp.Tag="muted"; content.Controls.Add(serviceHelp,0,2);
   startup.Text="Oturum açtığımda sistem tepsisinde göster"; startup.AutoSize=true; content.Controls.Add(startup,0,3); var trayHelp=LabelOf("Oturum açınca simge saatin yanında durur.",9,false); trayHelp.Tag="muted"; content.Controls.Add(trayHelp,0,4);
   engineStartup.CheckedChanged+=delegate { if(!refreshing) Execute(engineStartup.Checked?"auto-on":"auto-off",0); };
   startup.CheckedChanged+=delegate {
    if(refreshing) return;
    try {
     if(startup.Checked) Program.RequireOwned();
     WindowsIntegration.SetTrayStartup(startup.Checked);
     notice.Text=startup.Checked?"Oturum açınca tepsi simgesi gelecek.":"Tepsi simgesi artık oturum açınca gelmeyecek.";
    } catch(Exception ex) { ReportError(ex); } RefreshState();
   };
  }
  void BuildTools(TableLayoutPanel layout) {
   var tools=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=4,RowCount=1,Margin=Padding.Empty };
   for(int i=0;i<4;i++) tools.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25f));
   restart.Glyph="restart"; restart.Text="Yeniden başlat"; restart.Dock=DockStyle.Top; restart.Margin=new Padding(0,4,8,4); restart.Click+=delegate { Execute("restart",0); }; tools.Controls.Add(restart);
   var diagnostics=ButtonOf("Konsol",delegate { ShowDiagnostics(); }); diagnostics.Glyph="console"; diagnostics.Dock=DockStyle.Top; diagnostics.Margin=new Padding(4,4,8,4); tools.Controls.Add(diagnostics);
   var access=ButtonOf("Erişim testi",delegate { ShowAccessTest(); }); access.Glyph="globe"; access.Dock=DockStyle.Top; access.Margin=new Padding(4,4,8,4); tools.Controls.Add(access);
   var uninstall=ButtonOf("Uygulamayı kaldır",delegate {
    if(busy) return;
    try { Process.Start(new ProcessStartInfo(Path.Combine(Program.InstallDir,"GoodbyeDPI.Tray.exe"),"--uninstall") { UseShellExecute=true }); }
    catch(Exception ex) { ReportError(ex); }
   }); uninstall.Glyph="trash"; uninstall.Dock=DockStyle.Top; uninstall.Margin=new Padding(4,4,0,4); tools.Controls.Add(uninstall); layout.Controls.Add(tools);
  }
  void HideToTray() {
   if(tray==null) return;
   Hide(); ShowInTaskbar=false;
   if(!hideNotified) { hideNotified=true; tray.ShowBalloonTip(3000,"GoodbyeDPI sistem tepsisinde","Açmak için saatin yanındaki simgeye çift tıkla.",ToolTipIcon.Info); }
  }
  void Reveal() { Show(); ShowInTaskbar=true; WindowState=FormWindowState.Normal; Activate(); }
  internal void DispatchCommand(string command) {
   if(command=="show") Reveal();
   else if(command=="profiles") { Reveal(); profiles.Focus(); }
   else if(command=="settings") { Reveal(); engineStartup.Focus(); }
   else if(command=="exit") { if(busy) { notice.Text="İşlem sürüyor; tamamlandıktan sonra çıkabilirsin."; return; } quitting=true; Close(); }
   else if(command=="start" || command=="stop" || command=="restart") { if(!available) { Reveal(); notice.Text="Önce kurulumu tamamla."; } else if(!busy && !transition) Execute(command,0); }
  }
  internal void SetServiceState(int profile,ServiceControllerStatus state) {
   int previous=activeProfile; activeProfile=profile; available=profile>=0;
   running=available && state==ServiceControllerStatus.Running;
   transition=available && state!=ServiceControllerStatus.Running && state!=ServiceControllerStatus.Stopped;
   if(available && (previous<0 || profiles.SelectedIndex==previous)) profiles.SelectedIndex=profile;
   status.Text=!available?"Kurulum gerekiyor":running?"GoodbyeDPI çalışıyor":transition?"Durum değişiyor…":"GoodbyeDPI durduruldu";
   detail.Text=available?"Etkin profil: "+Program.Names[profile]:"GoodbyeDPI-Installer.exe'yi açıp kurman gerekiyor.";
   toggle.Text=busy?"İşlem sürüyor…":running?"Durdur":"Başlat";
   toggle.Enabled=available && !busy && !transition;
   restart.Enabled=available && running && !busy && !transition;
   profiles.Enabled=available && !busy && !transition;
   engineStartup.Enabled=available && !busy && !transition; startup.Enabled=!busy;
   UpdateSelection();
   PaintButton(toggle,true);
   if(tray!=null) {
    tray.Text=available?(running?"GoodbyeDPI • Çalışıyor":"GoodbyeDPI • Durduruldu"):"GoodbyeDPI • Kurulum gerekiyor";
    trayToggle.Enabled=available && !running && !busy && !transition; trayStop.Enabled=running && !busy && !transition; trayRestart.Enabled=restart.Enabled; trayProfiles.Enabled=available && !busy && !transition;
    for(int i=0;i<trayProfiles.DropDownItems.Count;i++) { var item=(ToolStripMenuItem)trayProfiles.DropDownItems[i]; item.Checked=i==activeProfile; item.Enabled=i!=activeProfile; }
   }
  }
  void UpdateSelection() {
   int i=profiles.SelectedIndex;
   hint.Text=(i==1||i==2||i==6)?"Bu profil DNS'e dokunmaz; gerekirse Windows'tan DNS ayarla.":"DNS'i kendisi yönlendirir, Windows ayarların değişmez.";
   bool dirty=available && i!=activeProfile;
   pending.Text=dirty?"Seçtiğin profil henüz uygulanmadı.":available?"Bu profil şu an kullanılıyor.":"Kurulumdan sonra profil seçebilirsin.";
   apply.Enabled=dirty && !busy && !transition;
  }
  async void Execute(string action,int profile) {
   if(busy) return;
   stateRevision++; busy=true; notice.Text="Windows izin isteyebilir, bekle…"; SetServiceState(activeProfile,running?ServiceControllerStatus.Running:ServiceControllerStatus.Stopped);
   try {
    Program.RequireOwned();
    using(var process=Process.Start(new ProcessStartInfo(Path.Combine(Program.InstallDir,"GoodbyeDPI.Tray.exe"),"--admin "+action+" "+profile) { UseShellExecute=true,Verb="runas",WorkingDirectory=Program.InstallDir })) {
     await Task.Run(delegate { process.WaitForExit(); });
     if(process.ExitCode!=0) notice.Text="Olmadı. Hata penceresinde nedenini görebilirsin.";
     else notice.Text=action=="profile"?"Profil uygulandı.":action=="stop"?"GoodbyeDPI durduruldu.":action=="auto-on"||action=="auto-off"?"Başlangıç tercihin kaydedildi.":"GoodbyeDPI başlatıldı.";
    }
   } catch(System.ComponentModel.Win32Exception ex) { if(ex.NativeErrorCode==1223) notice.Text="İzin verilmedi, hiçbir şey değişmedi."; else ReportError(ex); }
   catch(Exception ex) { ReportError(ex); }
   finally { busy=false; if(!IsDisposed) RefreshState(); }
  }
  void ReportError(Exception ex) { AppInfo.Log(ex.ToString()); notice.Text="İşlem tamamlanamadı: "+ex.Message; MessageBox.Show(this,ex.Message,"İşlem tamamlanamadı",MessageBoxButtons.OK,MessageBoxIcon.Warning); }
  void ShowAbout() { using(var dialog=CreateAboutDialog()) dialog.ShowDialog(this); }
  internal Form CreateAboutDialog() {
   var dialog=new Form { Text="GoodbyeDPI — Hakkında",ClientSize=new Size(540,420),Font=Font,BackColor=background,ForeColor=foreground,Icon=Program.AppIcon,StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false };
   var heading=new Label { Text="GoodbyeDPI Tray",Font=new Font(Font.FontFamily,20,FontStyle.Bold),Location=new Point(24,20),Size=new Size(490,40) }; dialog.Controls.Add(heading);
   dialog.Controls.Add(new Label { Text="Sürüm "+AppInfo.Version+"  ·  Geliştirici: Furkan",Location=new Point(26,66),Size=new Size(488,26),ForeColor=secondary });
   dialog.Controls.Add(new Label { Text=AppInfo.Purpose,Location=new Point(26,108),Size=new Size(488,82) });
   Color linkColor=SystemInformation.HighContrast?SystemColors.HotTrack:dark?AppPalette.Mint:AppPalette.Accent;
   var source=ProjectLinks.Create("Kaynak proje · GoodbyeDPI-Turkey",AppInfo.SourceUrl,linkColor); source.SetBounds(26,204,488,34); dialog.Controls.Add(source);
   var developer=ProjectLinks.Create("Geliştirici · Furkan / trfurkan",AppInfo.DeveloperUrl,linkColor); developer.SetBounds(26,246,488,34); dialog.Controls.Add(developer);
   dialog.Controls.Add(new Label { Text="Motor: GoodbyeDPI-Turkey 0.2.3rc3\nLisans: Lisans detayları kurulum klasöründeki licenses dizininde yer almaktadır.",Location=new Point(26,302),Size=new Size(488,76),ForeColor=secondary });
   dialog.HandleCreated+=delegate { WindowsIntegration.ThemeCaption(dialog,dark,background,foreground); }; return dialog;
  }
  void FitDashboard() {
   if(dashboard.RowStyles.Count<8 || startupContent==null) return;
   bool compact=ClientSize.Height<860;
   int[] heights=compact?new[]{64,96,24,144,116,48}:new[]{104,112,30,160,164,58};
   dashboard.SuspendLayout(); startupContent.SuspendLayout(); footerPanel.SuspendLayout();
   try {
    for(int i=0;i<heights.Length;i++) if(dashboard.RowStyles[i].Height!=heights[i]) dashboard.RowStyles[i].Height=heights[i];
    foreach(int row in new[]{2,4}) { startupContent.RowStyles[row].Height=compact?0:26; var help=startupContent.GetControlFromPosition(0,row); if(help!=null) help.Visible=!compact; }
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
     startup.Checked=snapshot.Item1; engineStartup.Checked=snapshot.Item2;
     if(changed || String.IsNullOrEmpty(status.Text)) SetServiceState(snapshot.Item3,snapshot.Item4);
     if(snapshot.Item5) detail.Text="Farklı bir GoodbyeDPI kurulumu bulundu.";
    } finally { refreshing=false; }
   } catch(Exception ex) { if(!IsDisposed && !Disposing && !busy) { SetServiceState(-1,ServiceControllerStatus.Stopped); status.Text="Durum okunamadı"; detail.Text=ex.Message; } }
   finally { stateReadPending=false; }
  }  void RefreshState() {
   stateRevision++;
   refreshing=true;
   try {
    startup.Checked=WindowsIntegration.TrayStartupEnabled();
    using(var key=Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\GoodbyeDPI")) engineStartup.Checked=key!=null && Convert.ToInt32(key.GetValue("Start",3))==2;
    int active=Program.Profile(); var state=ServiceControllerStatus.Stopped;
    if(active>=0) using(var svc=new ServiceController(Program.ServiceName)) state=svc.Status;
    SetServiceState(active,state);
    if(active<0 && Program.ServicePath()!=null) detail.Text="Farklı bir GoodbyeDPI kurulumu bulundu.";
   } catch(Exception ex) { SetServiceState(-1,ServiceControllerStatus.Stopped); status.Text="Durum okunamadı"; detail.Text=ex.Message; }
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
   if(value!="system" && value!="light" && value!="dark") throw new ArgumentException("Geçersiz tema.");
   if(persist) using(var key=Registry.CurrentUser.CreateSubKey(PreferencesKey)) key.SetValue("Theme",value);
   themePreference=value; ApplyTheme(true);
  }
  void ApplyTheme(bool force) {
   bool next=themeOverride??(themePreference=="system"?WindowsDark():themePreference=="dark"); if(!force && next==dark && !SystemInformation.HighContrast) return; dark=next;
   themeButton.Text="Tema: "+(themePreference=="light"?"Aydınlık":themePreference=="dark"?"Karanlık":"Sistem")+" ▾";
   background=AppPalette.Background(dark); surface=AppPalette.Surface(dark); foreground=AppPalette.Text(dark); secondary=AppPalette.Muted(dark); border=AppPalette.Border(dark);
   if(SystemInformation.HighContrast) { background=SystemColors.Window; surface=SystemColors.Window; foreground=SystemColors.WindowText; secondary=SystemColors.WindowText; border=SystemColors.WindowText; }
   BackColor=background; PaintControls(this); PaintButton(toggle,true); WindowsIntegration.ThemeCaption(this,dark,background,foreground);
   if(diagnosticsWindow!=null && !diagnosticsWindow.IsDisposed) { ((EngineConsoleForm)diagnosticsWindow).ApplyPalette(dark); }
   if(accessWindow!=null && !accessWindow.IsDisposed) ((AccessTestForm)accessWindow).ApplyPalette(dark);
   if(tray!=null) { tray.ContextMenuStrip.Renderer=new MenuRenderer(surface,foreground,border); tray.ContextMenuStrip.BackColor=surface; tray.ContextMenuStrip.ForeColor=foreground; trayProfiles.DropDown.Renderer=tray.ContextMenuStrip.Renderer; }
   Invalidate(true);
  }
  void PaintButton(SoftButton button,bool primary) { button.Fill=primary?AppPalette.Accent:surface; button.Ink=primary?Color.White:foreground; button.Edge=primary?button.Fill:border; if(SystemInformation.HighContrast) { button.Fill=primary?foreground:background; button.Ink=primary?background:foreground; } Color host=background; for(Control parent=button.Parent;parent!=null;parent=parent.Parent) { if(parent is SoftPanel) { host=surface; break; } } button.BackColor=host; button.ForeColor=button.Ink; button.Invalidate(); }
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
   if(disposing && !resourcesDisposed) { resourcesDisposed=true; if(diagnosticsWindow!=null) diagnosticsWindow.Dispose(); if(accessWindow!=null) accessWindow.Dispose(); SystemEvents.UserPreferenceChanged-=ThemeChanged; timer.Dispose(); themeMenu.Dispose(); if(commands!=null) commands.Dispose(); if(reveal!=null) reveal.Dispose(); if(tray!=null) { tray.Visible=false; tray.ContextMenuStrip.Dispose(); tray.Dispose(); } }
   base.Dispose(disposing);
  }
 }
}
