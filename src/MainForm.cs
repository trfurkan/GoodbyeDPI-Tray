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
  bool hovered,pressed,spinning;
  // Draws only the glyph, centered; Text stays for screen readers and the tooltip.
  internal bool IconOnly;
  // Fully round instead of the usual rounded rectangle (the header's language and theme buttons).
  internal bool Circle;
  internal bool PrimaryAction;
  Tween flash;
  // While an operation runs the glyph turns into a spinning arc.
  internal bool Spinning { get { return spinning; } set { if(spinning==value) return; spinning=value; Motion.Loop(this,value); Invalidate(); } }
  // A green edge that fades out: the change this control made has been applied.
  internal void Flash() { if(flash==null) flash=new Tween(this,0); flash.Jump(1); flash.To(0,1100); }
  // 0 rest, 12 hover, 25 pressed: how far the fill leans toward the ink color. Press also sinks the button by up to 2 px.
  readonly Tween tint,sink;
  // Glyph motion: "draw" replays the power arc on hover, "turn" spins the restart arrow once per click.
  // Both run only on hover or click, so an idle window costs nothing.
  readonly Tween draw,turn;
  float HoverAmount { get { return Math.Max(0,Math.Min(1,tint.Value/12f)); } }
  protected override void OnClick(EventArgs e) { if(Glyph=="restart") { turn.Jump(0); turn.To(1,650); } base.OnClick(e); }
  void Ease() { tint.To(Enabled && pressed?25:Enabled && hovered?12:0,pressed?70:160); sink.To(Enabled && pressed?1:0,pressed?70:220); }
  public DialogResult DialogResult { get; set; }
  public void NotifyDefault(bool value) { Invalidate(); }
  public void PerformClick() { if(Enabled && Visible) OnClick(EventArgs.Empty); }
  public SoftButton() { SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw|ControlStyles.Selectable|ControlStyles.StandardClick,true); Cursor=Cursors.Hand; Height=40; TabStop=true; AccessibleRole=AccessibleRole.PushButton; tint=new Tween(this,0); sink=new Tween(this,0); draw=new Tween(this,1); turn=new Tween(this,0); }
  protected override void OnPaintBackground(PaintEventArgs e) { e.Graphics.Clear(BackColor); }
  protected override void OnMouseEnter(EventArgs e) { hovered=true; Ease(); if(Glyph=="power" && Enabled) { draw.Jump(0.15f); draw.To(1,520); } base.OnMouseEnter(e); }
  protected override void OnMouseLeave(EventArgs e) { hovered=false; pressed=false; Ease(); base.OnMouseLeave(e); }
  protected override void OnMouseDown(MouseEventArgs e) { if(e.Button==MouseButtons.Left) { pressed=true; Focus(); Ease(); } base.OnMouseDown(e); }
  protected override void OnMouseUp(MouseEventArgs e) { pressed=false; Ease(); base.OnMouseUp(e); }
  protected override void OnKeyDown(KeyEventArgs e) { if(e.KeyCode==Keys.Space) { pressed=true; Ease(); e.Handled=true; } base.OnKeyDown(e); }
  protected override void OnKeyUp(KeyEventArgs e) { if(e.KeyCode==Keys.Space && pressed) { pressed=false; PerformClick(); Ease(); e.Handled=true; } base.OnKeyUp(e); }
  protected override bool ProcessDialogKey(Keys keyData) { if(keyData==Keys.Enter) { PerformClick(); return true; } return base.ProcessDialogKey(keyData); }
  protected override void OnLostFocus(EventArgs e) { pressed=false; Ease(); base.OnLostFocus(e); }
  protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
  protected override void OnEnabledChanged(EventArgs e) { if(!Enabled) { hovered=pressed=false; } Ease(); Invalidate(); base.OnEnabledChanged(e); }
  protected override void OnPaint(PaintEventArgs e) {
   e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
   var bounds=new Rectangle(1,1,Width-3,Height-3);
   if(bounds.Width<2 || bounds.Height<2) return;
   Color paint=Motion.Mix(Fill,Ink,Enabled?tint.Value/100f:0);
   float inset=sink.Value*1.5f;
   using(var path=RoundF(new RectangleF(bounds.X+inset,bounds.Y+inset,bounds.Width-inset*2,bounds.Height-inset*2),Circle || PrimaryAction?Math.Min(bounds.Width,bounds.Height)-inset*2:Math.Min(16,Math.Min(bounds.Width,bounds.Height)))) using(var fill=new SolidBrush(paint)) using(var pen=new Pen(Edge)) {
    e.Graphics.FillPath(fill,path); e.Graphics.DrawPath(pen,path);
   }
   Color ink=Enabled?Ink:Color.FromArgb((Ink.R+Fill.R)/2,(Ink.G+Fill.G)/2,(Ink.B+Fill.B)/2);
   var textBounds=ClientRectangle;
   if(!String.IsNullOrEmpty(Glyph)) {
    int textWidth=TextRenderer.MeasureText(Text,Font).Width; int x=IconOnly?(Width-18)/2:Math.Max(12,(Width-textWidth-28)/2),y=(Height-18)/2;
    var saved=e.Graphics.Save(); e.Graphics.TranslateTransform(x,y);
    using(var pen=new Pen(ink,1.7f)) {
     pen.StartCap=pen.EndCap=LineCap.Round; pen.LineJoin=LineJoin.Round;
     if(spinning) { float a=Motion.Enabled?(Motion.Now%900)/900f*360:0; using(var track=new Pen(Color.FromArgb(60,ink),2f)) e.Graphics.DrawEllipse(track,1,1,16,16); using(var arc=new Pen(ink,2.2f) { StartCap=LineCap.Round,EndCap=LineCap.Round }) e.Graphics.DrawArc(arc,1,1,16,16,a,110); }
     else if(Glyph=="stop" || Glyph=="play") {
      // A small, eased scale replaces the power ring's redraw animation.
      float scale=1+HoverAmount*0.08f-sink.Value*0.12f;
      e.Graphics.TranslateTransform(9,9); e.Graphics.ScaleTransform(scale,scale); e.Graphics.TranslateTransform(-9,-9);
      using(var brush=new SolidBrush(ink)) {
       if(Glyph=="stop") using(var square=RoundF(new RectangleF(3,3,12,12),4)) e.Graphics.FillPath(brush,square);
       else e.Graphics.FillPolygon(brush,new[]{new PointF(5,2),new PointF(16,9),new PointF(5,16)});
      }
     }
     else if(Glyph=="restart") {
      // Leans a quarter turn on hover and spins a full turn when clicked.
      float angle=HoverAmount*90+Motion.EaseInOut(turn.Value)*360;
      e.Graphics.TranslateTransform(9,9); e.Graphics.RotateTransform(angle); e.Graphics.TranslateTransform(-9,-9);
      e.Graphics.DrawArc(pen,2,2,14,14,-60,300); e.Graphics.DrawLines(pen,new[]{new Point(10,0),new Point(14,2),new Point(11,5)});
     }
     else if(Glyph=="trash") { e.Graphics.DrawLine(pen,2,4,16,4); e.Graphics.DrawRectangle(pen,6,1,6,3); e.Graphics.DrawLines(pen,new[]{new Point(4,5),new Point(5,17),new Point(13,17),new Point(14,5)}); e.Graphics.DrawLine(pen,8,7,8,14); e.Graphics.DrawLine(pen,11,7,11,14); }
     else if(Glyph=="console") { e.Graphics.DrawLines(pen,new[]{new Point(2,3),new Point(8,9),new Point(2,15)}); e.Graphics.DrawLine(pen,10,15,17,15); }
     else if(GlyphImage!=null) e.Graphics.DrawImage(GlyphImage,-1,-1,20,20);
     else if(Glyph=="power") {
      // On hover the ring redraws itself from the top and the stem drops in after it.
      float t=Motion.EaseOut(draw.Value);
      e.Graphics.DrawArc(pen,1.5f,2.5f,15,15,-50,280*t); e.Graphics.DrawLine(pen,9,0.5f+(1-t)*4,9,8);
     }
     else if(Glyph=="search") { e.Graphics.DrawEllipse(pen,1,1,12,12); e.Graphics.DrawLine(pen,11.5f,11.5f,17,17); }
     else if(Glyph=="down") e.Graphics.DrawLines(pen,new[]{new Point(3,6),new Point(9,12),new Point(15,6)});
     else if(Glyph=="up") e.Graphics.DrawLines(pen,new[]{new Point(3,12),new Point(9,6),new Point(15,12)});
     else if(Glyph=="back") { e.Graphics.DrawLines(pen,new[]{new Point(8,3),new Point(2,9),new Point(8,15)}); e.Graphics.DrawLine(pen,2,9,17,9); }
     else if(Glyph=="list") { for(int row=0;row<3;row++) { e.Graphics.DrawLine(pen,2,3+row*6,3,3+row*6); e.Graphics.DrawLine(pen,7,3+row*6,17,3+row*6); } }
     else if(Glyph=="wand") { e.Graphics.DrawLine(pen,2,16,11,7); e.Graphics.DrawLine(pen,13,1,13,5); e.Graphics.DrawLine(pen,11,3,15,3); e.Graphics.DrawLine(pen,16.5f,8,16.5f,11); e.Graphics.DrawLine(pen,15,9.5f,18,9.5f); e.Graphics.DrawLine(pen,6,2,6,4); e.Graphics.DrawLine(pen,5,3,7,3); }
     else if(Glyph=="pause") {
      // The bars step apart and shorten a little on hover, like a breath held.
      float h=HoverAmount; e.Graphics.DrawLine(pen,6-h*1.2f,2+h*2,6-h*1.2f,16-h*2); e.Graphics.DrawLine(pen,12+h*1.2f,2+h*2,12+h*1.2f,16-h*2);
     }
     else if(Glyph=="bug") { e.Graphics.DrawEllipse(pen,5,5,8,11); e.Graphics.DrawArc(pen,6,1,6,6,180,180); e.Graphics.DrawLine(pen,9,8,9,16); for(int leg=0;leg<3;leg++) { float ly=7+leg*3.5f; e.Graphics.DrawLine(pen,5,ly,1.5f,ly-1+leg); e.Graphics.DrawLine(pen,13,ly,16.5f,ly-1+leg); } }
     else if(Glyph=="info") { e.Graphics.DrawEllipse(pen,1,1,16,16); e.Graphics.DrawLine(pen,9,8,9,13); using(var dot=new SolidBrush(ink)) e.Graphics.FillEllipse(dot,7.9f,4.2f,2.2f,2.2f); }
     else if(Glyph=="globe") { e.Graphics.DrawEllipse(pen,1,1,16,16); e.Graphics.DrawEllipse(pen,5.5f,1,7,16); e.Graphics.DrawLine(pen,1,9,17,9); }
     else { e.Graphics.DrawLines(pen,new[]{new Point(12,1),new Point(9,4),new Point(12,7),new Point(16,4),new Point(16,9),new Point(12,11),new Point(9,10),new Point(3,17),new Point(0,14),new Point(7,7),new Point(7,3),new Point(12,1)}); }
    }
    e.Graphics.Restore(saved); textBounds=new Rectangle(x+26,0,Math.Max(1,Width-x-34),Height);
   }
   if(!IconOnly || String.IsNullOrEmpty(Glyph)) TextRenderer.DrawText(e.Graphics,Text,Font,textBounds,ink,(String.IsNullOrEmpty(Glyph)?TextFormatFlags.HorizontalCenter:TextFormatFlags.Left)|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
   if(flash!=null && flash.Value>0.01f) using(var path=RoundF(new RectangleF(1.5f,1.5f,Width-4,Height-4),Math.Min(PrimaryAction?Height-4:16,Math.Min(Width-4,Height-4)))) using(var glow=new Pen(Color.FromArgb((int)(230*flash.Value),PrimaryAction?Ink:StatusLabel.Green),2.4f)) e.Graphics.DrawPath(glow,path);
   if(Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics,new Rectangle(8,8,Width-16,Height-16),Ink,Fill);
  }
  internal static GraphicsPath RoundF(RectangleF r,float diameter) {
   var path=new GraphicsPath(); path.AddArc(r.X,r.Y,diameter,diameter,180,90); path.AddArc(r.Right-diameter,r.Y,diameter,diameter,270,90); path.AddArc(r.Right-diameter,r.Bottom-diameter,diameter,diameter,0,90); path.AddArc(r.X,r.Bottom-diameter,diameter,diameter,90,90); path.CloseFigure(); return path;
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
 // One rounded frame around related buttons (restart, break, start/stop), with a thin line between the plain ones,
 // so they read as a single control. The buttons inside draw no frame of their own.
 class ActionGroup : FlowLayoutPanel {
  internal Color Fill,Edge,Host;
  public ActionGroup() { SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true); FlowDirection=FlowDirection.LeftToRight; WrapContents=false; AutoSize=true; AutoSizeMode=AutoSizeMode.GrowAndShrink; Padding=new Padding(4); }
  protected override void OnPaintBackground(PaintEventArgs e) { e.Graphics.Clear(Host); }
  protected override void OnPaint(PaintEventArgs e) {
   var g=e.Graphics; g.SmoothingMode=SmoothingMode.AntiAlias;
   if(Width<6 || Height<6) return;
   using(var path=SoftButton.Round(new Rectangle(0,0,Width-1,Height-1),Math.Min(20,Height-1))) using(var brush=new SolidBrush(Fill)) using(var pen=new Pen(Edge)) { g.FillPath(brush,path); g.DrawPath(pen,path); }
   using(var line=new Pen(Edge)) for(int i=0;i<Controls.Count-1;i++) { var left=Controls[i]; if(!left.Visible || Controls[i+1] is SoftButton && ((SoftButton)Controls[i+1]).Fill!=Fill) continue; int x=left.Right+left.Margin.Right/2; g.DrawLine(line,x,10,x,Height-11); }
  }
 }
 class ProfilePicker : SoftButton {
  public readonly List<string> Items=new List<string>();
  readonly ContextMenuStrip menu=new ContextMenuStrip { ShowImageMargin=false,ShowCheckMargin=true,DropShadowEnabled=true };
  // An optional action under the choices, set apart by a line (the profile list uses it for "find a profile").
  internal string ExtraText,ExtraHint; internal Action Extra; internal Func<bool> ExtraEnabled;
  // Hover text for the choice at a position, shown on the menu rows and on the picker for the selected one.
  internal Func<int,string> Describe;
  readonly ToolTip tip=new ToolTip { AutoPopDelay=20000,InitialDelay=400,ReshowDelay=200 };
  internal void RefreshTip() { tip.SetToolTip(this,Describe!=null && selected>=0?Describe(selected):null); }
  int selected=-1;
  internal event EventHandler SelectedIndexChanged;
  // Only a choice the user makes in the menu; setting SelectedIndex from code does not raise it.
  internal event EventHandler Picked;
  public int SelectedIndex {
   get { return selected; }
   set { if(value < -1 || value>=Items.Count) throw new ArgumentOutOfRangeException("value"); if(selected==value) return; selected=value; Text=value<0?L.T("Profil seç  ▾"):Items[value]+"  ▾"; RefreshTip(); if(SelectedIndexChanged!=null) SelectedIndexChanged(this,EventArgs.Empty); Invalidate(); }
  }
  public ProfilePicker() { Click+=delegate { OpenMenu(); }; AccessibleName=L.T("Bağlantı profili"); }
  void OpenMenu() {
   if(IsDisposed || Disposing || !Enabled || menu.Visible) return;
   // The owner keeps the menu alive until disposal. Closed runs before all
   // WinForms dismissal/focus processing has finished; do not dispose there.
   while(menu.Items.Count>0) { var old=menu.Items[0]; menu.Items.RemoveAt(0); old.Dispose(); }
   // The menu is as wide as the picker, with roomy rows and rounded corners.
   var font=new Font("Segoe UI",10.5f); int width=Math.Max(Width,220);
   menu.Font=font; menu.Padding=new Padding(6,8,6,8); menu.MinimumSize=new Size(width,0); menu.BackColor=Fill; menu.ForeColor=Ink; menu.Renderer=new MenuRenderer(Fill,Ink,ControlPaint.Light(Fill,0.12f)) { Rounded=true };
   for(int i=0;i<Items.Count;i++) { int index=i; var item=new ToolStripMenuItem(Items[i]) { Checked=i==selected,BackColor=Fill,ForeColor=Ink,Font=font,AutoSize=false,Size=new Size(width-12,38),Margin=Padding.Empty,ToolTipText=Describe==null?null:Describe(i) }; item.Click+=delegate { SelectedIndex=index; if(Picked!=null) Picked(this,EventArgs.Empty); }; menu.Items.Add(item); }
   if(Extra!=null) {
    menu.Items.Add(new ToolStripSeparator { Margin=new Padding(0,4,0,4) });
    var extra=new ToolStripMenuItem("✦   "+ExtraText) { Tag="accent",AccessibleDescription=ExtraHint,Enabled=ExtraEnabled==null || ExtraEnabled(),BackColor=Fill,Font=new Font("Segoe UI",10.5f,FontStyle.Bold),AutoSize=false,Size=new Size(width-12,String.IsNullOrEmpty(ExtraHint)?40:58),Margin=Padding.Empty };
    extra.Click+=delegate { BeginInvoke(Extra); }; menu.Items.Add(extra);
   }
   menu.ShowItemToolTips=Describe!=null;
   menu.Show(this,new Point(0,Height+4));
  }
  protected override bool ProcessCmdKey(ref Message msg,Keys keyData) { if(Enabled && (keyData==Keys.Down || keyData==(Keys.Alt|Keys.Down))) { OpenMenu(); return true; } return base.ProcessCmdKey(ref msg,keyData); }

  protected override void Dispose(bool disposing) { if(disposing) { menu.Dispose(); tip.Dispose(); } base.Dispose(disposing); }
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
  readonly StatusLabel status=new StatusLabel();
  readonly Label detail=new Label(), notice=new Label(), hint=new Label(), pending=new Label(), dnsHelp=new Label();
  readonly ProfilePicker profiles=new ProfilePicker();
  readonly ProfilePicker motors=new ProfilePicker();
  // The picker lists a motor's profiles in display order, so its position maps to an id through MotorProfiles.
  int SelectedProfile { get { return profiles.SelectedIndex<0?-1:Program.MotorProfiles(motors.SelectedIndex==1?Program.ZapretStart:0)[profiles.SelectedIndex]; } }
  void SelectProfile(int id) { motors.SelectedIndex=id>=Program.ZapretStart?1:0; profiles.SelectedIndex=Array.IndexOf(Program.MotorProfiles(id),id); }
  readonly CheckBox startup=new ThemeCheckBox(), engineStartup=new ThemeCheckBox(), quicBlock=new ThemeCheckBox(), sitesOnly=new ThemeCheckBox();
  readonly ProfilePicker dnsServer=new ProfilePicker();


  readonly SoftButton toggle=new SoftButton(), restart=new SoftButton(), consoleButton=new SoftButton(), reportButton=new SoftButton(), aboutButton=new SoftButton();
  readonly LinkLabel versionLink=new LinkLabel();
  readonly ActionGroup actionGroup=new ActionGroup();
  readonly LiveLogo liveLogo=new LiveLogo();
  readonly WordMark brandTitle=new WordMark();
  // The group's frame is the page color on the card; its plain buttons blend into it and only tint on hover.
  void PaintActionGroup() {
   actionGroup.Host=surface; actionGroup.Fill=SystemInformation.HighContrast?background:background; actionGroup.Edge=border; actionGroup.BackColor=surface;
   foreach(var button in new[]{restart,pauseButton}) { button.Fill=actionGroup.Fill; button.Edge=actionGroup.Fill; button.Ink=foreground; button.BackColor=actionGroup.Fill; button.Invalidate(); }
   toggle.BackColor=actionGroup.Fill; actionGroup.Invalidate();
  }
  // "v1.9.0 · güncel" in the footer; a new release turns it into the accent-colored download link.
  void ShowVersion() {
   string state=updateState==UpdateState.Current?L.T("güncel"):updateState==UpdateState.Available?L.T("yeni sürüm var"):null;
   versionLink.Text="v"+AppInfo.Version+(state==null?"":" · "+state);
   Color muted=secondary.IsEmpty?SystemColors.GrayText:secondary, accent=dark?AppPalette.Mint:AppPalette.Accent;
   Color color=updateState==UpdateState.Available?accent:muted; versionLink.LinkColor=versionLink.ActiveLinkColor=versionLink.VisitedLinkColor=color;
  }
  readonly Label trayHelp=LabelOf("",9,false);
  // The first check waits a little: right after logon the network is often not up yet.
  DateTime nextUpdateCheck=DateTime.UtcNow.AddSeconds(30);
  bool updateChecking;
  bool startupNoticePending, updateBalloon;
  DateTime startupNoticeAfter, startupNoticeDeadline;
  internal void BeginStartupNotification() { startupNoticePending=true; startupNoticeAfter=DateTime.UtcNow.AddSeconds(8); startupNoticeDeadline=DateTime.UtcNow.AddSeconds(45); }
  internal void TryStartupNotification() {
   if(!startupNoticePending || DateTime.UtcNow<startupNoticeAfter || stateReadPending || notesOpen || !TrayShown) return;
   if((!available || transition || !running) && DateTime.UtcNow<startupNoticeDeadline) return;
   startupNoticePending=false; updateBalloon=false;
   Notify("GoodbyeDPI Tray",status.Text,available?ToolTipIcon.Info:ToolTipIcon.Warning);
  }
  void NotificationClicked() { if(updateBalloon) ShowReleaseNotes(); else Reveal(); }
  // A Windows notification when the system offers one, the tray balloon otherwise.
  void Notify(string title,string body,ToolTipIcon icon) { if(!Toast.Show(title,body,this,NotificationClicked)) tray.ShowBalloonTip(5000,title,body,icon); }

  UpdateState updateState;
  string availableUpdate;
  event Action UpdateStateChanged;


  readonly TableLayoutPanel dashboard=new BufferedLayout();
  readonly TableLayoutPanel footerPanel=new BufferedLayout();

  TableLayoutPanel startupContent,heroRows;

  readonly ToolTip tips=new ToolTip { InitialDelay=300 };
  readonly GlowPanel heroCard=new GlowPanel();
  bool sizing,stateReadPending;
  Form diagnosticsWindow;
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
   ClientSize=new Size(900,Math.Min(910,Screen.PrimaryScreen.WorkingArea.Height-70)); MinimumSize=new Size(760,740); StartPosition=FormStartPosition.CenterScreen; Icon=Program.AppIcon;
   var scroll=dashboardHost; scroll.Dock=DockStyle.Fill; scroll.AutoScroll=false; Controls.Add(scroll); BuildPageHost();
   var layout=dashboard; layout.Dock=DockStyle.Fill; layout.AutoSize=false; layout.Padding=new Padding(20,10,20,10); layout.ColumnCount=1; layout.RowCount=8;
   foreach(int height in new[]{104,264,0,0,292,118}) layout.RowStyles.Add(new RowStyle(SizeType.Absolute,height));
   layout.RowStyles.Add(new RowStyle(SizeType.Absolute,0)); layout.RowStyles.Add(new RowStyle(SizeType.Percent,100)); scroll.Controls.Add(layout);
   var header=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=3,RowCount=1 };
   // Header: a live logo whose ring shows the engine's state, the two-tone name, and round language and theme buttons.
   header.RowStyles.Add(new RowStyle(SizeType.Percent,100)); header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,54)); header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,48));
      var brand=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,RowCount=1 };
   brand.RowStyles.Add(new RowStyle(SizeType.Percent,100)); brand.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,84)); brand.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
   liveLogo.Dock=DockStyle.Fill; liveLogo.Margin=Padding.Empty; liveLogo.AccessibleName="GoodbyeDPI"; brand.Controls.Add(liveLogo);
   brandTitle.Dock=DockStyle.Fill; brandTitle.Font=new Font(Font.FontFamily,24,FontStyle.Bold); brandTitle.Margin=new Padding(8,0,0,0); brand.Controls.Add(brandTitle); header.Controls.Add(brand,0,0);
   for(int i=0;i<ThemeModes.Length;i++) { string choice=ThemeModes[i]; var item=new ToolStripMenuItem(ThemeLabels[i]) { Tag=choice,Image=ThemeSwatch(choice),AutoSize=false,Size=new Size(300,choice=="system"?72:40),Padding=new Padding(8,6,12,6) };
    if(choice=="system") { item.AccessibleDescription=L.T("Temayı (Karanlık/Aydınlık) sistem tercihlerine göre otomatik ayarlar."); item.ImageAlign=ContentAlignment.TopCenter; } item.Click+=delegate { try { SetThemePreference(choice,true); notice.Text=L.T("Tema kaydedildi."); } catch(Exception ex) { ReportError(ex); } }; if(choice=="system") themeMenu.Items.Add(new ToolStripSeparator()); themeMenu.Items.Add(item); }
   themeButton.Size=new Size(42,42); themeButton.Anchor=AnchorStyles.Right; themeButton.Margin=new Padding(0,0,2,0); themeButton.Circle=true; themeButton.IconOnly=true; themeButton.AccessibleName=L.T("Görünüm teması"); themeButton.Glyph="swatch";
   themeButton.Click+=delegate { foreach(ToolStripItem entry in themeMenu.Items) { var item=entry as ToolStripMenuItem; if(item==null) continue; item.Checked=Convert.ToString(item.Tag)==themePreference; item.BackColor=surface; item.ForeColor=foreground; } themeMenu.BackColor=surface; themeMenu.ForeColor=foreground; themeMenu.Renderer=new MenuRenderer(surface,foreground,border); if(!themeMenu.Visible) themeMenu.Show(themeButton,new Point(0,themeButton.Height)); };
   // Language picker left of the theme button: round flag, language name, a menu of the languages.
   int current=Array.IndexOf(L.Codes,L.Current);
   languageButton.Size=new Size(42,42); languageButton.Anchor=AnchorStyles.Right; languageButton.Margin=new Padding(0,0,8,0); languageButton.Circle=true; languageButton.IconOnly=true; languageButton.AccessibleName=L.T("Dil"); languageButton.Glyph="swatch"; languageButton.GlyphImage=L.Flag(L.Current); languageButton.Text=L.Names[current]+"  ▾"; tips.SetToolTip(languageButton,L.T("Dil")+": "+L.Names[current]);
   for(int i=0;i<L.Codes.Length;i++) { string code=L.Codes[i]; var item=new ToolStripMenuItem(L.Names[i]) { Tag=code,Image=L.Flag(code),AutoSize=false,Size=new Size(200,40),Padding=new Padding(8,6,12,6) }; item.Click+=delegate { ChangeLanguage(code); }; languageMenu.Items.Add(item); }
   languageButton.Click+=delegate { foreach(ToolStripItem entry in languageMenu.Items) { var item=(ToolStripMenuItem)entry; item.Checked=Convert.ToString(item.Tag)==L.Current; item.BackColor=surface; item.ForeColor=foreground; } languageMenu.BackColor=surface; languageMenu.ForeColor=foreground; languageMenu.Renderer=new MenuRenderer(surface,foreground,border); if(!languageMenu.Visible) languageMenu.Show(languageButton,new Point(0,languageButton.Height)); };
   header.Controls.Add(languageButton,1,0);
   header.Controls.Add(themeButton,2,0);
   layout.Controls.Add(header);
   var hero=heroCard; hero.Dock=DockStyle.Fill; hero.Margin=new Padding(0,0,0,12); hero.Padding=new Padding(26,18,26,16); layout.Controls.Add(hero);
   var heroLayout=new BufferedLayout { Dock=DockStyle.Fill,ColumnCount=2,RowCount=2,BackColor=Color.Transparent };
   heroLayout.RowCount=3; heroRows=heroLayout; heroLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); heroLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,304)); heroLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,64)); heroLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,0)); heroLayout.RowStyles.Add(new RowStyle(SizeType.Percent,100)); hero.Controls.Add(heroLayout);
   status.Dock=DockStyle.Fill; status.Font=new Font(Font.FontFamily,19,FontStyle.Bold); status.TextAlign=ContentAlignment.MiddleLeft; heroLayout.Controls.Add(status,0,0);
   // Only notices go under the status (a break, a missing install); the active profile is already in the picker below.
   detail.Dock=DockStyle.Fill; detail.Tag="muted"; detail.AutoEllipsis=true; heroLayout.Controls.Add(detail,0,1); detail.TextChanged+=delegate { FitDashboard(); };
   // The main button, with restart and pause as small round icon buttons beside it.
   var actions=new FlowLayoutPanel { Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,WrapContents=false,Margin=Padding.Empty,Padding=new Padding(0,7,0,0),BackColor=Color.Transparent };
   // Restart, break and start/stop sit in one frame, right-aligned; the start/stop button is the filled part of it.
   restart.Glyph="restart"; restart.IconOnly=true; restart.Text=L.T("Yeniden başlat"); restart.Size=new Size(46,44); restart.Margin=new Padding(0,0,5,0); restart.Click+=delegate { Execute("restart",0); }; actionGroup.Controls.Add(restart);
   pauseButton.Glyph="pause"; pauseButton.IconOnly=true; pauseButton.Text=L.T("Geçici durdur"); pauseButton.Size=new Size(46,44); pauseButton.Margin=new Padding(0,0,5,0); pauseButton.Click+=delegate { ShowPauseMenu(); }; actionGroup.Controls.Add(pauseButton);
   toggle.PrimaryAction=true; toggle.Glyph="play"; toggle.Size=new Size(160,44); toggle.Margin=Padding.Empty; toggle.Click+=delegate { Execute(running?"stop":"start",0); }; actionGroup.Controls.Add(toggle);
   actionGroup.Margin=Padding.Empty; actions.Controls.Add(actionGroup);
   tips.SetToolTip(pauseButton,pauseButton.Text); tips.SetToolTip(restart,restart.Text); pauseButton.AccessibleName=pauseButton.Text; restart.AccessibleName=restart.Text;
   heroLayout.Controls.Add(actions,1,0); heroLayout.SetRowSpan(actions,2); BuildChoices(heroLayout);
   // The status line under the hero has no height (its row is 0); errors still show in a message box.
   // It stays visible so the layout keeps its row order.
   notice.Dock=DockStyle.Fill; layout.Controls.Add(notice);
   // Row 3 used to hold the profile card; the choices now live in the status card and the row stays empty.
   layout.Controls.Add(new Panel { Dock=DockStyle.Fill,Margin=Padding.Empty }); BuildSettings(layout);
   BuildAccess(layout); ConfirmTrayOff=AskTrayOff;
   // Footer: developer link on the left, then the version with its update state, then three icon buttons with tooltips.
   footerPanel.Dock=DockStyle.Bottom; footerPanel.Height=44; footerPanel.ColumnCount=5; footerPanel.RowCount=1; footerPanel.Margin=Padding.Empty;
   footerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); footerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
   for(int i=0;i<3;i++) footerPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,46));
   footerPanel.RowStyles.Add(new RowStyle(SizeType.Percent,100));
   var developer=ProjectLinks.Create(L.T("Geliştirici: trfurkan"),AppInfo.DeveloperUrl,AppPalette.Accent,true); developer.Dock=DockStyle.Fill; footerPanel.Controls.Add(developer,0,0);
   versionLink.AutoSize=true; versionLink.Anchor=AnchorStyles.Right; versionLink.Margin=new Padding(0,0,12,0); versionLink.Tag="muted"; versionLink.LinkBehavior=LinkBehavior.HoverUnderline; versionLink.LinkClicked+=delegate { if(updateState==UpdateState.Available) DownloadUpdate(); else ShowAbout(); }; footerPanel.Controls.Add(versionLink,1,0); ShowVersion();
   int column=2;
   foreach(var entry in new[]{Tuple.Create(consoleButton,"console",L.T("Konsol"),L.T("Motor konsolu")),Tuple.Create(reportButton,"bug",L.T("Sorun bildir"),L.T("Sorun bildir")),Tuple.Create(aboutButton,"info",L.T("Hakkında"),L.T("Hakkında"))}) {
    var button=entry.Item1; button.Glyph=entry.Item2; button.IconOnly=true; button.Text=entry.Item3; button.AccessibleName=entry.Item3; button.Size=new Size(40,34); button.Anchor=AnchorStyles.Right; button.Margin=new Padding(6,0,0,0); tips.SetToolTip(button,entry.Item4); footerPanel.Controls.Add(button,column++,0);
   }
   consoleButton.Click+=delegate { ShowDiagnostics(); }; reportButton.Click+=delegate { ShowReport(); }; aboutButton.Click+=delegate { ShowAbout(); };
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
    foreach(int id in Program.DisplayOrder()) { int index=id; trayProfiles.DropDownItems.Add(new ToolStripMenuItem(Program.Names[id],null,delegate { Execute("profile",index); }) { Tag=id,ToolTipText=Program.Describe(id) }); } trayProfiles.DropDown.ShowItemToolTips=true; menu.Items.Add(trayProfiles);
    menu.Items.Add(L.T("Ayarlar"),null,delegate { DispatchCommand("settings"); });
    menu.Items.Add(new ToolStripSeparator());
    menu.Items.Add(L.T("Arayüzden çık"),null,delegate { DispatchCommand("exit"); });
    bool showTray=true; try { showTray=WindowsIntegration.TrayStartupEnabled(); } catch(Exception ex) { AppInfo.Log(ex.ToString()); }
    tray=new NotifyIcon { Icon=Program.AppIcon,Text="GoodbyeDPI",ContextMenuStrip=menu,Visible=showTray }; tray.DoubleClick+=delegate { Reveal(); }; tray.BalloonTipClicked+=delegate { NotificationClicked(); };
    trayHelp.Text=TrayHelp(showTray);
   }
   Shown+=delegate { if(withTray) WindowsIntegration.RegisterJumpList(); if(WindowState==FormWindowState.Minimized && TrayShown) { Hide(); ShowInTaskbar=false; } };
   Resize+=delegate { if(WindowState==FormWindowState.Minimized && TrayShown) HideToTray(); };
   FormClosing+=delegate(object sender,FormClosingEventArgs e) { if(busy && e.CloseReason==CloseReason.UserClosing) { e.Cancel=true; return; } if(!quitting && e.CloseReason==CloseReason.UserClosing && TrayShown) { e.Cancel=true; HideToTray(); } };
   SystemEvents.UserPreferenceChanged+=ThemeChanged;
   timer.Interval=250; timer.Tick+=delegate { if(reveal!=null && reveal.WaitOne(0)) Reveal(); string command=commands==null?null:commands.Poll(); if(command!=null) DispatchCommand(command); TryStartupNotification(); if(++refreshTicks>=8) { refreshTicks=0; if(!busy && !sizing) RefreshStateAsync(); if(!sizing && Visible) ApplyTheme(false); if(DateTime.UtcNow>=nextUpdateCheck && !startupNoticePending) CheckForUpdateAsync(); } }; if(withTray) timer.Start();
   SizeChanged+=delegate { if(WindowState!=FormWindowState.Minimized) FitDashboard(); }; ResizeBegin+=delegate { sizing=true; }; ResizeEnd+=delegate { sizing=false; FitDashboard(); };
   ApplyTheme(true); FitDashboard(); if(withTray) { Shown+=delegate { RefreshStateAsync(); }; } else RefreshState();
  }
  static Label LabelOf(string text,float size,bool bold) { return new Label { Text=text,Dock=DockStyle.Fill,Font=new Font("Segoe UI",size,bold?FontStyle.Bold:FontStyle.Regular),AutoEllipsis=true }; }
  SoftButton ButtonOf(string text,EventHandler action) { var button=new SoftButton { Text=text,Width=140 }; button.Click+=action; return button; }
  // A section heading: an accent icon and the title, like the captions in the status card but stronger.
  static Control SectionTitle(string glyph,string text) {
   var row=new FlowLayoutPanel { Dock=DockStyle.Fill,FlowDirection=FlowDirection.LeftToRight,WrapContents=false,Margin=Padding.Empty,Padding=Padding.Empty };
   string family=Array.Exists(System.Drawing.FontFamily.Families,f=>f.Name=="Segoe Fluent Icons")?"Segoe Fluent Icons":"Segoe MDL2 Assets";
   row.Controls.Add(new Label { Text=glyph,Font=new Font(family,13),AutoSize=true,Tag="accent",Margin=new Padding(0,5,8,0) });
   row.Controls.Add(new Label { Text=text,Font=new Font("Segoe UI",12,FontStyle.Bold),AutoSize=true,Margin=new Padding(0,1,0,0) });
   return row;
  }
  TableLayoutPanel Section(TableLayoutPanel parent,string glyph,string title,int[] rows) {
   var box=new SoftPanel { Dock=DockStyle.Fill,Margin=new Padding(0,0,0,12),Padding=new Padding(26,14,26,10) }; parent.Controls.Add(box);
   var content=new BufferedLayout { Dock=DockStyle.Fill,ColumnCount=1,RowCount=rows.Length,BackColor=Color.Transparent,Margin=Padding.Empty };
   foreach(int height in rows) content.RowStyles.Add(new RowStyle(SizeType.Absolute,height));
   content.Controls.Add(SectionTitle(glyph,title),0,0); box.Controls.Add(content); return content;
  }
  // A quick check of the blocked sites with the current profile: each site's icon with a small result badge.
  // Google is checked too, without a tag, to tell a blocked site from a missing internet connection.
  static readonly string[] QuickSites={"discord.com","roblox.com","wattpad.com"};
  static readonly Color[] QuickColors={Color.FromArgb(88,101,242),Color.FromArgb(57,59,61),Color.FromArgb(255,80,10)};
  const string ControlSite="google.com";
  readonly AccessChip[] accessChips=new AccessChip[QuickSites.Length];
  readonly SoftButton accessRun=new SoftButton();
  readonly DetailTile accessMore=new DetailTile();
  readonly Label accessSummary=new Label();
  bool accessTesting;
  void BuildAccess(TableLayoutPanel layout) {
   var box=new SoftPanel { Dock=DockStyle.Fill,Margin=new Padding(0,0,0,12),Padding=new Padding(26,12,26,10) }; layout.Controls.Add(box);
   var content=new BufferedLayout { Dock=DockStyle.Fill,ColumnCount=2,RowCount=2,BackColor=Color.Transparent,Margin=Padding.Empty };
   content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,190));
   content.RowStyles.Add(new RowStyle(SizeType.Absolute,38)); content.RowStyles.Add(new RowStyle(SizeType.Percent,100)); box.Controls.Add(content); accessBox=box;
   var head=new FlowLayoutPanel { Dock=DockStyle.Fill,WrapContents=false,Margin=Padding.Empty,BackColor=Color.Transparent };
   head.Controls.Add(SectionTitle("\uE774",L.T("Erişim"))); head.Controls[0].Dock=DockStyle.None; head.Controls[0].AutoSize=true;
   accessSummary.AutoSize=true; accessSummary.Tag="muted"; accessSummary.Font=new Font("Segoe UI",9); accessSummary.Margin=new Padding(14,9,0,0); accessSummary.Text=L.T("Sitelerin bu profille açılıp açılmadığını dene."); head.Controls.Add(accessSummary);
   content.Controls.Add(head,0,0);
   var buttons=new FlowLayoutPanel { Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,WrapContents=false,Margin=Padding.Empty,BackColor=Color.Transparent };
   accessRun.Glyph="restart"; accessRun.Text=L.T("Test et"); accessRun.Size=new Size(132,34); accessRun.Margin=Padding.Empty; accessRun.Click+=delegate { RunAccessCheck(); }; buttons.Controls.Add(accessRun);
   content.Controls.Add(buttons,1,0);
   // The site icons on the left, and filling the rest of the row a tile that opens the detailed test.
   var row=new BufferedLayout { Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=new Padding(0,6,0,0),BackColor=Color.Transparent };
   row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); row.RowStyles.Add(new RowStyle(SizeType.Absolute,42));
   var chips=new FlowLayoutPanel { AutoSize=true,WrapContents=false,Margin=Padding.Empty,BackColor=Color.Transparent };
   for(int i=0;i<QuickSites.Length;i++) { accessChips[i]=new AccessChip(QuickSites[i],QuickColors[i]) { Font=new Font("Segoe UI",10) }; accessChips[i].Size=accessChips[i].GetPreferredSize(Size.Empty); tips.SetToolTip(accessChips[i],L.T("Henüz denenmedi")); chips.Controls.Add(accessChips[i]); }
   row.Controls.Add(chips,0,0);
   accessMore.Dock=DockStyle.Fill; accessMore.Margin=new Padding(4,1,0,1); accessMore.Font=new Font("Segoe UI",10); accessMore.Click+=delegate { ToggleAccess(); }; row.Controls.Add(accessMore,1,0);
   UpdateAccessTile();
   content.Controls.Add(row,0,1); content.SetColumnSpan(row,2); accessChipRow=row;
   accessHost.Dock=DockStyle.Fill; accessHost.Visible=false; accessHost.Margin=new Padding(0,6,0,0); content.Controls.Add(accessHost,0,1); content.SetColumnSpan(accessHost,2);
   accessCollapse.Glyph="up"; accessCollapse.IconOnly=true; accessCollapse.Text=L.T("Küçült"); accessCollapse.AccessibleName=accessCollapse.Text; accessCollapse.Size=new Size(40,34); accessCollapse.Margin=new Padding(8,0,0,0); accessCollapse.Visible=false; accessCollapse.Click+=delegate { ToggleAccess(); }; tips.SetToolTip(accessCollapse,accessCollapse.Text); buttons.Controls.Add(accessCollapse); buttons.Controls.SetChildIndex(accessCollapse,0);
  }
  void UpdateAccessTile() {
   accessMore.Caption=L.T("Ayrıntılı test"); accessMore.Hint=AccessProbe.Domains.Length+L.T(" site, tek tek sonuç ve süre"); accessMore.AccessibleName=accessMore.Caption; accessMore.Invalidate();
  }
  // Expanded, the card holds the full access test (every site, retry buttons and details) in place of the icons;
  // the settings card steps aside to make room and comes back when the card is folded again.
  readonly SoftButton accessCollapse=new SoftButton();
  readonly Panel accessHost=new Panel();
  Control accessBox,accessChipRow;
  AccessTestForm accessFull;
  bool accessExpanded;
  readonly System.Windows.Forms.Timer accessAnimation=new System.Windows.Forms.Timer { Interval=15 };
  float accessProgress,accessFrom;
  int accessStarted;
  void AnimateAccess(object sender,EventArgs e) {
   float t=Motion.Enabled?Math.Min(1,(Motion.Now-accessStarted)/300f):1;
   accessProgress=accessFrom+((accessExpanded?1:0)-accessFrom)*Motion.EaseInOut(t);
   FitDashboard();
   if(t>=1) { accessAnimation.Stop(); AccessVisibility(); }
  }
  void AccessVisibility() {
   bool showing=accessExpanded || accessProgress>0;
   accessHost.Visible=showing; accessChipRow.Visible=!showing; accessRun.Visible=!showing; accessCollapse.Visible=showing;
   if(!showing) accessMore.Focus();
  }
  void ToggleAccess() {
   accessExpanded=!accessExpanded;
   if(accessExpanded && accessFull==null) { accessFull=new AccessTestForm(dark,delegate { return running; }); accessFull.Embed(); accessHost.Controls.Add(accessFull); accessFull.Show(); }
   accessFrom=accessProgress; accessStarted=Motion.Now;
   accessAnimation.Tick-=AnimateAccess; accessAnimation.Tick+=AnimateAccess;
   AccessVisibility();
   if(Motion.Enabled) accessAnimation.Start(); else AnimateAccess(this,EventArgs.Empty);
   ApplyTheme(false);
  }
  void PaintAccessChips() {
   bool contrast=SystemInformation.HighContrast;
   foreach(var chip in accessChips) { if(chip==null) continue;
    chip.BackColor=surface; chip.Fill=contrast?surface:background; chip.Edge=border; chip.Ink=foreground; chip.Muted=secondary;
    chip.Good=contrast?foreground:dark?Color.FromArgb(74,222,128):Color.FromArgb(22,135,74); chip.Bad=contrast?foreground:dark?Color.FromArgb(248,113,113):Color.FromArgb(196,52,62); chip.Spin=contrast?foreground:dark?AppPalette.Mint:AppPalette.Accent; chip.Invalidate(); }
   accessMore.BackColor=surface; accessMore.Fill=contrast?surface:background; accessMore.Edge=border; accessMore.Ink=foreground; accessMore.Muted=secondary; accessMore.Accent=contrast?foreground:dark?AppPalette.Mint:AppPalette.Accent; accessMore.Invalidate();
  }
  // Opens a real HTTPS connection to each site at once; nothing is sent anywhere else.
  async void RunAccessCheck() {
   if(accessTesting || IsDisposed) return;
   accessTesting=true; accessRun.Enabled=false; accessRun.Spinning=true; accessSummary.Text=L.T("Siteler deneniyor…");
   foreach(var chip in accessChips) { chip.State=AccessChip.Phase.Testing; tips.SetToolTip(chip,L.T("Test ediliyor…")); }
   int reached=0;
   try {
    await Task.Run(delegate { AccessProbe.ForgetCachedAnswers(); });
    var control=Task.Run(delegate { return AccessProbe.Run(ControlSite); });
    var jobs=new Task<AccessProbe.Result>[QuickSites.Length];
    for(int i=0;i<QuickSites.Length;i++) { string domain=QuickSites[i]; jobs[i]=Task.Run(delegate { return AccessProbe.Run(domain); }); }
    for(int i=0;i<jobs.Length;i++) {
     var result=await jobs[i]; if(IsDisposed) return;
     var chip=accessChips[i];
     chip.State=result.Reached?AccessChip.Phase.Reached:AccessChip.Phase.Failed;
     tips.SetToolTip(chip,result.Status+(String.IsNullOrEmpty(result.Detail)?"":" · "+result.Detail));
     if(result.Reached) {
      reached++;
      // The first time a site opens, its own icon replaces the letter.
      if(SiteIcons.Load(chip.Domain)==null) { string domain=chip.Domain; var picture=await Task.Run(delegate { return SiteIcons.Fetch(domain); }); if(!IsDisposed) chip.Icon=picture; }
     }
    }
    bool online=(await control).Reached; if(IsDisposed) return;
    string text=DateTime.Now.ToString("HH:mm")+" · "+reached+"/"+QuickSites.Length+L.T(" engelli site açıldı.");
    if(!online && reached==0) text+=L.T(" İnternet bağlantını kontrol et.");
    else if(!running) text+=L.T(" GoodbyeDPI kapalıyken denendi.");
    else if(reached<QuickSites.Length) text+=L.T(" Açılmayan varsa başka bir profil dene.");
    accessSummary.Text=text;
   } catch(Exception ex) { AppInfo.Log("Hızlı erişim testi: "+ex); accessSummary.Text=L.T("Test tamamlanamadı."); foreach(var chip in accessChips) if(chip.State==AccessChip.Phase.Testing) chip.State=AccessChip.Phase.Idle; }
   finally { accessTesting=false; if(!IsDisposed) { accessRun.Enabled=true; accessRun.Spinning=false; } }
  }
  // After a change that restarts the engine, check again once it had a moment to come up.
  async void RecheckAccessSoon() { if(commands==null) return; await Task.Delay(2500); if(!IsDisposed && running && !busy) RunAccessCheck(); }
  // A small accent icon and a quiet caption above each choice.
  static Control ChoiceTitle(string glyph,string text) {
   var row=new FlowLayoutPanel { Dock=DockStyle.Fill,FlowDirection=FlowDirection.LeftToRight,WrapContents=false,Margin=Padding.Empty,Padding=Padding.Empty };
   string family=Array.Exists(System.Drawing.FontFamily.Families,f=>f.Name=="Segoe Fluent Icons")?"Segoe Fluent Icons":"Segoe MDL2 Assets";
   row.Controls.Add(new Label { Text=glyph,Font=new Font(family,12),AutoSize=true,Tag="accent",Margin=new Padding(0,5,6,0) });
   row.Controls.Add(new Label { Text=text,Font=new Font("Segoe UI",10.5f),AutoSize=true,Tag="muted",Margin=new Padding(0,4,0,0) });
   return row;
  }
  // Under the status line: the connection profile on the left, the DNS server on the right.
  void BuildChoices(TableLayoutPanel heroLayout) {
   var choices=new BufferedLayout { Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=new Padding(0,12,0,0),Padding=new Padding(0,16,0,0),BackColor=Color.Transparent };
   // A hairline separates the status from the choices.
   choices.Paint+=delegate(object sender,PaintEventArgs e) { using(var pen=new Pen(border)) e.Graphics.DrawLine(pen,0,0,choices.Width,0); };
   choices.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,58)); choices.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,42)); choices.RowStyles.Add(new RowStyle(SizeType.Percent,100));
   heroLayout.Controls.Add(choices,0,2); heroLayout.SetColumnSpan(choices,2);
   var left=new BufferedLayout { Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,Margin=new Padding(0,0,12,0),BackColor=Color.Transparent };
   foreach(int h in new[]{30,50,22,22}) left.RowStyles.Add(new RowStyle(SizeType.Absolute,h));
   left.Controls.Add(ChoiceTitle("\uE945",L.T("Bağlantı profili")),0,0);
   var selection=new BufferedLayout { Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty };
   selection.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,Program.ZapretEnabled?130:0)); selection.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
   motors.Dock=DockStyle.Top; motors.AccessibleName=L.T("DPI motoru"); motors.Visible=Program.ZapretEnabled; motors.Items.AddRange(Program.ZapretEnabled?new[]{"GoodbyeDPI","Zapret 2"}:new[]{"GoodbyeDPI"});
   motors.SelectedIndexChanged+=delegate { profiles.SelectedIndex=-1; profiles.Items.Clear(); foreach(int id in Program.MotorProfiles(motors.SelectedIndex==1?Program.ZapretStart:0)) profiles.Items.Add(Program.Names[id].Replace("Zapret 2 · ","")); profiles.SelectedIndex=0; UpdateSelection(); };
   selection.Controls.Add(motors,0,0); motors.SelectedIndex=0;
   profiles.Dock=DockStyle.Top; profiles.Height=46; motors.Height=46;
   profiles.Describe=delegate(int position) { var ids=Program.MotorProfiles(motors.SelectedIndex==1?Program.ZapretStart:0); return position>=0 && position<ids.Length?Program.Describe(ids[position]):null; };
   profiles.SelectedIndexChanged+=delegate { UpdateSelection(); }; selection.Controls.Add(profiles,1,0);
   // A profile picked from the menu is applied at once, like the DNS server; picking an engine applies its first profile.
   profiles.Picked+=delegate { ApplyPicked(); }; motors.Picked+=delegate { ApplyPicked(); };
   // The last entry of the profile menu lets the app find a profile that works on this line.
   findProfile.Text=L.T("Uygun profil bul"); findProfile.Click+=delegate { FindProfile(); };
   profiles.ExtraText=findProfile.Text; profiles.ExtraHint=L.T("Profilleri senin hattında sırayla dener."); profiles.ExtraEnabled=delegate { return findProfile.Enabled; }; profiles.Extra=delegate { if(findProfile.Enabled) FindProfile(); }; left.Controls.Add(selection,0,1);
   hint.Dock=DockStyle.Fill; hint.Tag="muted"; hint.AutoEllipsis=true; left.Controls.Add(hint,0,2);
   pending.Dock=DockStyle.Fill; pending.Tag="muted"; pending.AutoEllipsis=true; left.Controls.Add(pending,0,3);
   choices.Controls.Add(left,0,0);
   var right=new BufferedLayout { Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Margin=new Padding(12,0,0,0),BackColor=Color.Transparent };
   foreach(int h in new[]{30,50,22}) right.RowStyles.Add(new RowStyle(SizeType.Absolute,h));
   right.Controls.Add(ChoiceTitle("\uE774",L.T("DNS sunucusu")),0,0);
   dnsServer.Dock=DockStyle.Top; dnsServer.Height=46; dnsServer.Margin=new Padding(0,2,0,2); dnsServer.AccessibleName=L.T("DNS sunucusu");
   foreach(int id in DnsChoice.Order) dnsServer.Items.Add(L.T(DnsChoice.Names[id]));
   dnsServer.SelectedIndex=DnsChoice.Position; right.Controls.Add(dnsServer,0,1);
   dnsHelp.Text=L.T("Standart ve \"DNS ile\" profillerinde kullanılır."); dnsHelp.Dock=DockStyle.Fill; dnsHelp.Font=new Font("Segoe UI",9); dnsHelp.AutoEllipsis=true; dnsHelp.Tag="muted"; right.Controls.Add(dnsHelp,0,2);
   dnsServer.SelectedIndexChanged+=delegate { if(!refreshing && dnsServer.SelectedIndex>=0) Execute("dns",DnsChoice.Order[dnsServer.SelectedIndex]); };
   choices.Controls.Add(right,1,0);
  }
  Label[] settingsHelp;
  void BuildSettings(TableLayoutPanel layout) {
   // Standard checkboxes with their explanations underneath.
   var content=Section(layout,"\uE713",L.T("Ayarlar"),new[]{38,0}); startupContent=content; content.RowStyles[1].SizeType=SizeType.Percent; content.RowStyles[1].Height=100;
   var grid=new BufferedLayout { Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,Margin=Padding.Empty,BackColor=Color.Transparent };
   grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
   for(int i=0;i<4;i++) grid.RowStyles.Add(new RowStyle(SizeType.Percent,25));
   content.Controls.Add(grid,0,1);
   engineStartup.Text=L.T("Windows açıldığında GoodbyeDPI'ı başlat");
   startup.Text=L.T("Sistem tepsisinde göster");
   quicBlock.Text=L.T("Tarayıcılarda QUIC'i engelle");
   sitesOnly.Text=L.T("Yalnızca engelli sitelere uygula");
   var sitesLink=new LinkLabel { Text=L.T("Siteler")+" ›",AutoSize=true,LinkBehavior=LinkBehavior.HoverUnderline,Margin=new Padding(0,0,8,0) }; sitesLink.LinkClicked+=delegate { ShowSiteList(); }; tips.SetToolTip(sitesLink,L.T("Siteleri düzenle"));
   var boxes=new[]{engineStartup,startup,quicBlock,sitesOnly};
   var helpLabels=new[]{new Label(),trayHelp,new Label(),new Label()}; settingsHelp=helpLabels;
   helpLabels[0].Text=L.T("Bilgisayar açılınca GoodbyeDPI ve tepsi simgesi de başlar.");
   trayHelp.Text=TrayHelp(true);
   helpLabels[2].Text=L.T("Discord gibi siteler tarayıcıda açılmıyorsa açık kalsın. Yalnızca GoodbyeDPI çalışırken etkilidir.");
   helpLabels[3].Text=L.T("Discord, Roblox ve Wattpad dışındaki sitelere dokunmaz.");
   for(int i=0;i<boxes.Length;i++) {
    var row=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,RowCount=2,Margin=new Padding(0,0,0,4),BackColor=Color.Transparent };
    row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
    row.RowStyles.Add(new RowStyle(SizeType.Absolute,28)); row.RowStyles.Add(new RowStyle(SizeType.Percent,100));
    var box=boxes[i]; box.Dock=DockStyle.Fill; box.AutoSize=false; box.UseVisualStyleBackColor=true; box.Margin=Padding.Empty;
    row.Controls.Add(box,0,0);
    if(i==3) { sitesLink.Anchor=AnchorStyles.Right; row.Controls.Add(sitesLink,1,0); }
    else row.SetColumnSpan(box,2);
    var help=helpLabels[i]; help.Font=new Font("Segoe UI",9); help.Tag="muted"; help.Dock=DockStyle.Fill; help.Margin=new Padding(18,0,0,0);
    row.Controls.Add(help,0,1); row.SetColumnSpan(help,2);
    tips.SetToolTip(box,help.Text); grid.Controls.Add(row,0,i);
   }
   trayHelp.TextChanged+=delegate { tips.SetToolTip(startup,trayHelp.Text); };
   quicBlock.CheckedChanged+=delegate { if(!refreshing) Execute(quicBlock.Checked?"quic-on":"quic-off",0); };
   sitesOnly.CheckedChanged+=delegate { if(!refreshing) Execute(sitesOnly.Checked?"sites-on":"sites-off",0); };   // Starting with Windows covers both parts: the service and the tray entry in Task Manager follow this box together.
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
  void Uninstall() {
   if(busy) return;
   try { Process.Start(new ProcessStartInfo(Path.Combine(Program.InstallDir,"GoodbyeDPI.Tray.exe"),"--uninstall") { UseShellExecute=true }); }
   catch(Exception ex) { ReportError(ex); }
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
  async void CheckForUpdateAsync(bool manual=false,bool openNotes=false) {
   if(updateChecking || IsDisposed) return;
   if(!manual && !UpdateCheck.Enabled) { nextUpdateCheck=DateTime.UtcNow.AddHours(1); return; }
   updateChecking=true; SetUpdateState(UpdateState.Checking);
   try {
    string latest=await Task.Run(delegate { return UpdateCheck.Fetch(); });
    nextUpdateCheck=DateTime.UtcNow.AddDays(1);
    if(IsDisposed) return;
    if(latest!=null) { ShowUpdate(latest); if(openNotes) ShowReleaseNotes(); } else SetUpdateState(UpdateState.Current);
   } catch(Exception ex) { nextUpdateCheck=DateTime.UtcNow.AddHours(1); AppInfo.Log("Güncelleme denetimi: "+ex.Message); if(!IsDisposed) SetUpdateState(UpdateState.Failed); }
   finally { updateChecking=false; }
  }
  void SetUpdateState(UpdateState state) { updateState=state; if(state==UpdateState.Current) availableUpdate=null; ShowVersion(); var changed=UpdateStateChanged; if(changed!=null) changed(); }
  internal void ShowUpdate(string version) {
   availableUpdate=version; SetUpdateState(UpdateState.Available);
   // Each release is announced once, and only when there is a tray icon to show it.
   if(TrayShown && UpdateCheck.NotifiedVersion!=version) {
    updateBalloon=true;
    Notify("GoodbyeDPI Tray "+version+L.T(" çıktı"),L.T("Hakkında")+" → "+L.T("Neler değişti?"),ToolTipIcon.Info);
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
   try {
    var asset=UpdateCheck.DownloadAsset;
    if(asset==null) { Process.Start(new ProcessStartInfo(UpdateCheck.ReleaseUrl) { UseShellExecute=true }); notice.Text=L.T("Sürüm sayfası açıldı, kurucuyu oradan indirebilirsin."); return; }
    using(var dialog=new UpdateDownloadForm(dark,asset)) if(dialog.ShowDialog(Form.ActiveForm ?? this)==DialogResult.OK) notice.Text=L.T("Kurucu açıldı. Güncellemeyi kurulum penceresinden tamamlayabilirsin.");
   }
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
   detail.Text=available?"":L.T("GoodbyeDPI-Installer.exe'yi açıp kurman gerekiyor.");
   if(paused) { status.Text=L.T("Geçici olarak durduruldu"); detail.Text=L.T("Moladan dönüş: ")+resumeAt.Value.ToLocalTime().ToString("HH:mm:ss"); }
   toggle.Text=busy?L.T("İşlem sürüyor…"):running?L.T("Durdur"):L.T("Başlat");
   toggle.Glyph=running?"stop":"play";
   toggle.Enabled=available && !busy && !transition;
   restart.Enabled=available && running && !busy && !transition;
   profiles.Enabled=available && !busy && !transition;
   motors.Enabled=profiles.Enabled;
   if(available && profile>=Program.ZapretStart) status.Text=status.Text.Replace("GoodbyeDPI","Zapret 2");
   liveLogo.State=!available?StatusLabel.Mark.Stopped:busy || transition?StatusLabel.Mark.Busy:running?StatusLabel.Mark.Running:paused?StatusLabel.Mark.Paused:StatusLabel.Mark.Stopped;
   status.State=!available?StatusLabel.Mark.Stopped:busy || transition?StatusLabel.Mark.Busy:running?StatusLabel.Mark.Running:paused?StatusLabel.Mark.Paused:StatusLabel.Mark.Stopped;
   heroCard.Glow=status.State==StatusLabel.Mark.Running?StatusLabel.Green:status.State==StatusLabel.Mark.Paused?StatusLabel.Amber:status.State==StatusLabel.Mark.Busy?AppPalette.Accent:Color.Empty;
   heroCard.Breathing=status.State==StatusLabel.Mark.Running; toggle.Spinning=busy || transition;
   findProfile.Enabled=available && !busy && !transition && !paused;
   pauseButton.Enabled=available && (running || paused) && !busy && !transition;
   pauseButton.Text=paused?L.T("Molayı yönet"):L.T("Geçici durdur"); pauseButton.AccessibleName=pauseButton.Text; tips.SetToolTip(pauseButton,pauseButton.Text);
   trayPause.Enabled=available && running && !busy && !transition;
   engineStartup.Text=L.T("Windows açıldığında GoodbyeDPI'ı başlat").Replace("GoodbyeDPI",Program.MotorName(activeProfile));
   engineStartup.Enabled=available && !busy && !transition; quicBlock.Enabled=available && !busy && !transition; sitesOnly.Enabled=quicBlock.Enabled; startup.Enabled=!busy;
   UpdateSelection();
   PaintButton(toggle,true); PaintActionGroup();
   if(tray!=null) {
    tray.Text=available?(running?L.T("GoodbyeDPI • Çalışıyor"):L.T("GoodbyeDPI • Durduruldu")):L.T("GoodbyeDPI • Kurulum gerekiyor");
    if(available) tray.Text=tray.Text.Replace("GoodbyeDPI",Program.MotorName(activeProfile));
    trayToggle.Enabled=available && !running && !busy && !transition; trayStop.Enabled=(running || paused) && !busy && !transition; trayRestart.Enabled=restart.Enabled; trayProfiles.Enabled=available && !busy && !transition;
    for(int i=0;i<trayProfiles.DropDownItems.Count;i++) { var item=(ToolStripMenuItem)trayProfiles.DropDownItems[i]; int id=(int)item.Tag; item.Checked=id==activeProfile; item.Enabled=id!=activeProfile; }
   }
  }
  // Only the real app (with its tray icon) changes the service; previews and tests never do.
  void ApplyPicked() { int id=SelectedProfile; if(commands!=null && available && !busy && !transition && id>=0 && id!=activeProfile) Execute("profile",id); }
  void UpdateSelection() {
   // A profile that leaves DNS alone does not use the chosen server: the picker goes quiet and says why.
   bool usesDns=DnsChoice.Uses(SelectedProfile);
   dnsServer.Enabled=available && !busy && !transition && usesDns;
   dnsHelp.Text=usesDns || SelectedProfile<0?L.T("Standart ve \"DNS ile\" profillerinde kullanılır."):L.T("Bu profil DNS'e dokunmaz, seçim şu an kullanılmıyor.");
   int i=SelectedProfile;
   hint.Text=(i>=Program.ZapretStart||i==1||i==2||i==6)?L.T("Bu profil DNS'e dokunmaz; gerekirse Windows'tan DNS ayarla."):L.T("DNS'i kendisi yönlendirir, Windows ayarların değişmez.");
   if(i==10) hint.Text=L.T("Superonline için TLS alternatifi; DNS ayarlarını değiştirmez.");
   if(i==11) hint.Text=L.T("QUIC başlangıcını engeller; destekleyen uygulamalar TCP'ye döner. DNS değişmez.");   bool dirty=available && i!=activeProfile;
   // Silent while the chosen profile is the active one; it only speaks up when there is something to apply.
   pending.Text=dirty?L.T("Seçtiğin profil henüz uygulanmadı."):available?"":L.T("Kurulumdan sonra profil seçebilirsin.");
  }
  async void Execute(string action,int profile) {
   if(busy) return;
   stateRevision++; busy=true; notice.Text=L.T("Bir saniye…"); SetServiceState(activeProfile,running?ServiceControllerStatus.Running:ServiceControllerStatus.Stopped);
   string done=action.StartsWith("pause")?L.T("GoodbyeDPI molada. Süre bitince geri gelecek."):action=="profile"?L.T("Profil uygulandı."):action=="stop"?L.T("GoodbyeDPI durduruldu."):action=="auto-on"||action=="auto-off"?L.T("Başlangıç tercihin kaydedildi."):action.StartsWith("quic")?L.T("Tarayıcı ayarın kaydedildi."):action=="dns"?L.T("DNS sunucusu değişti."):action.StartsWith("sites")?L.T("Site ayarın kaydedildi."):L.T("GoodbyeDPI başlatıldı.");
   done=done.Replace("GoodbyeDPI",Program.MotorName(action=="profile"?profile:activeProfile));
   try {
    Program.RequireOwned();
    bool applied=await Task.Run(delegate { return UserServiceControl.TryRun(action,profile); });
    if(applied) { notice.Text=done; if(action=="profile") profiles.Flash(); else if(action=="dns") dnsServer.Flash(); if(action=="profile" || action=="dns" || action=="start" || action=="restart" || action.StartsWith("sites")) RecheckAccessSoon(); }
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
  void ShowSiteList() {
   if(busy) return;
   using(var dialog=new SiteListForm(dark)) {
    // Previews and tests open the window too; only the real app sends changes to the service.
    if(commands==null) dialog.Edit=delegate(bool adding,string host) { throw new InvalidOperationException(L.T("Önizlemede site listesi değiştirilemez.")); };
    dialog.ShowDialog(this);
   }
   RefreshState();
  }
  void ShowReport() { using(var dialog=new ReportForm(dark)) dialog.ShowDialog(this); }
  void ShowAbout() { using(var dialog=CreateAboutDialog()) dialog.ShowDialog(this); }
  internal Form CreateAboutDialog() {
   var dialog=new Form { Text=L.T("GoodbyeDPI — Hakkında"),ClientSize=new Size(540,480),Font=Font,BackColor=background,ForeColor=foreground,Icon=Program.AppIcon,StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false };
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
   checkNow.LinkClicked+=delegate { if(updateState==UpdateState.Available) DownloadUpdate(); else CheckForUpdateAsync(true,true); };
   Action showState=delegate {
    if(dialog.IsDisposed) return;
    notesLink.Visible=true;
    statusIcon.State=updateState;
    statusText.Text=updateState==UpdateState.Current?L.T("Güncel sürümü kullanıyorsun."):updateState==UpdateState.Available?L.T("Yeni sürüm var: ")+availableUpdate:updateState==UpdateState.Checking?"Kontrol ediliyor…":updateState==UpdateState.Failed?"Kontrol edilemedi, internetini kontrol et.":L.T("Güncellemeler henüz kontrol edilmedi.");
    checkNow.Text=updateState==UpdateState.Available?L.T("Yeni sürümü indir"):updateState==UpdateState.Failed?L.T("Tekrar dene"):L.T("Şimdi kontrol et");
    checkNow.Enabled=updateState!=UpdateState.Checking;
   };
   showState(); UpdateStateChanged+=showState; dialog.Disposed+=delegate { UpdateStateChanged-=showState; };
   dialog.Controls.Add(statusIcon); dialog.Controls.Add(statusText); dialog.Controls.Add(checkNow);
   var updates=new CheckBox { Text=L.T("Yeni sürüm çıkınca haber ver"),Location=new Point(26,390),Size=new Size(488,30),Checked=UpdateCheck.Enabled };
   updates.CheckedChanged+=delegate { try { UpdateCheck.Enabled=updates.Checked; if(updates.Checked) nextUpdateCheck=DateTime.UtcNow; } catch(Exception ex) { ReportError(ex); } };
   dialog.Controls.Add(updates);
   var removeApp=new LinkLabel { Text=L.T("Uygulamayı kaldır"),Location=new Point(26,432),Size=new Size(488,28),LinkBehavior=LinkBehavior.HoverUnderline }; removeApp.LinkColor=removeApp.ActiveLinkColor=removeApp.VisitedLinkColor=SystemInformation.HighContrast?SystemColors.HotTrack:dark?Color.FromArgb(255,128,128):AppPalette.Danger;
   removeApp.LinkClicked+=delegate { dialog.Close(); Uninstall(); }; dialog.Controls.Add(removeApp);
   // Opening About in the running app checks right away if nothing is known yet.
   if(commands!=null && updateState==UpdateState.Unknown) dialog.Shown+=delegate { CheckForUpdateAsync(true); };
   dialog.HandleCreated+=delegate { WindowsIntegration.ThemeCaption(dialog,dark,background,foreground); }; return dialog;
  }
  void FitDashboard() {
   if(dashboard.RowStyles.Count<8 || startupContent==null) return;
   // The full layout needs about 870 px. Below that (small laptop screens) the settings drop their explanations,
   // the header shrinks and the status card loses its spare line, so every card still fits without scrolling.
   bool compact=ClientSize.Height<870;
   int[] heights=compact?new[]{64,0,0,0,204,118}:new[]{104,0,0,0,292,118};
   // The status card grows by one line while it shows a notice.
   bool note=!String.IsNullOrEmpty(detail.Text); heights[1]=(compact?242:264)+(note?24:0); if(heroRows!=null) heroRows.RowStyles[1].Height=note?24:0;
   int expandedHero=242+(note?24:0);
   int expandedAccess=Math.Max(300,ClientSize.Height-64-expandedHero-dashboard.Padding.Vertical-footerPanel.Height-8);
   heights[0]=(int)Math.Round(heights[0]+(64-heights[0])*accessProgress);
   heights[1]=(int)Math.Round(heights[1]+(expandedHero-heights[1])*accessProgress);
   heights[4]=(int)Math.Round(heights[4]*(1-accessProgress));
   heights[5]=(int)Math.Round(heights[5]+(expandedAccess-heights[5])*accessProgress);
   dashboard.SuspendLayout(); startupContent.SuspendLayout(); footerPanel.SuspendLayout();
   try {
    for(int i=0;i<heights.Length;i++) if(dashboard.RowStyles[i].Height!=heights[i]) dashboard.RowStyles[i].Height=heights[i];
    if(settingsHelp!=null) foreach(var help in settingsHelp) help.Visible=!compact;
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
     startup.Checked=snapshot.Item1; engineStartup.Checked=snapshot.Item2; quicBlock.Checked=QuicBlock.Wanted; sitesOnly.Checked=SiteList.Wanted; dnsServer.SelectedIndex=DnsChoice.Position; SetTrayVisible(snapshot.Item1);
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
    quicBlock.Checked=QuicBlock.Wanted; sitesOnly.Checked=SiteList.Wanted; dnsServer.SelectedIndex=DnsChoice.Position;
    int active=Program.Profile(); var state=ServiceControllerStatus.Stopped;
    if(active>=0) using(var svc=new ServiceController(Program.ServiceName)) state=svc.Status;
    SetServiceState(active,state);
    if(active<0 && Program.ServicePath()!=null) detail.Text=L.T("Farklı bir GoodbyeDPI kurulumu bulundu.");
   } catch(Exception ex) { SetServiceState(-1,ServiceControllerStatus.Stopped); status.Text=L.T("Durum okunamadı"); detail.Text=ex.Message; }
   finally { refreshing=false; }
  }
  void ShowDiagnostics() {
   try {
    if(diagnosticsWindow==null || diagnosticsWindow.IsDisposed) diagnosticsWindow=new EngineConsoleForm(dark);
    ShowPage(diagnosticsWindow,L.T("Motor konsolu"));
   } catch(Exception ex) { ReportError(ex); }
  }
  // A page fills the window in place of the dashboard; Back or Esc returns. The console and the access test are
  // ordinary forms shown here as child windows, so they behave exactly as on their own.
  readonly Panel dashboardHost=new Panel(), pageHost=new Panel(), pageBody=new Panel();
  readonly SoftButton pageBack=new SoftButton();
  readonly Label pageTitle=new Label();
  Form currentPage;
  void BuildPageHost() {
   pageHost.Dock=DockStyle.Fill; pageHost.Visible=false; pageHost.Padding=new Padding(20,14,20,24); Controls.Add(pageHost);
   var bar=new TableLayoutPanel { Dock=DockStyle.Top,Height=56,ColumnCount=2,RowCount=1,Margin=Padding.Empty,BackColor=Color.Transparent };
   bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,130)); bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
   pageBack.Glyph="back"; pageBack.Text=L.T("Geri"); pageBack.Size=new Size(118,44); pageBack.Margin=new Padding(0,4,12,4); pageBack.Click+=delegate { ClosePage(); }; tips.SetToolTip(pageBack,L.T("Ana ekrana dön (Esc)")); bar.Controls.Add(pageBack,0,0);
   pageTitle.Dock=DockStyle.Fill; pageTitle.Font=new Font("Segoe UI",16,FontStyle.Bold); pageTitle.TextAlign=ContentAlignment.MiddleLeft; bar.Controls.Add(pageTitle,1,0);
   pageBody.Dock=DockStyle.Fill; pageHost.Controls.Add(pageBody); pageHost.Controls.Add(bar);
  }
  void ShowPage(Form page,string title) {
   if(!Visible || WindowState==FormWindowState.Minimized) Reveal();
   if(currentPage!=null && currentPage!=page) currentPage.Hide();
   page.TopLevel=false; page.FormBorderStyle=FormBorderStyle.None; page.Dock=DockStyle.Fill;
   if(page.Parent!=pageBody) pageBody.Controls.Add(page);
   currentPage=page; pageTitle.Text=title; page.Show(); page.BringToFront();
   pageHost.Visible=true; pageHost.BringToFront(); dashboardHost.Visible=false; ApplyTheme(false); pageBack.Focus();
  }
  void ClosePage() {
   if(currentPage==null) return;
   // The console stops reading the engine output when it is left.
   var page=currentPage; currentPage=null;
   if(page==diagnosticsWindow) { page.Dispose(); diagnosticsWindow=null; } else page.Hide();
   dashboardHost.Visible=true; dashboardHost.BringToFront(); pageHost.Visible=false;
  }
  protected override bool ProcessCmdKey(ref Message msg,Keys keyData) { if(keyData==Keys.Escape && currentPage!=null) { ClosePage(); return true; } return base.ProcessCmdKey(ref msg,keyData); }
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
   themeButton.Text=ThemeName(themePreference)+"  ▾"; tips.SetToolTip(themeButton,L.T("Tema")+": "+ThemeName(themePreference));
   brandTitle.Accent=dark?AppPalette.Mint:AppPalette.Accent; brandTitle.Invalidate();
   var oldSwatch=themeButton.GlyphImage; themeButton.GlyphImage=ThemeSwatch(themePreference); if(oldSwatch!=null) oldSwatch.Dispose(); themeButton.Invalidate();
   background=AppPalette.Background(dark); surface=AppPalette.Surface(dark); foreground=AppPalette.Text(dark); secondary=AppPalette.Muted(dark); border=AppPalette.Border(dark);
   if(SystemInformation.HighContrast) { background=SystemColors.Window; surface=SystemColors.Window; foreground=SystemColors.WindowText; secondary=SystemColors.WindowText; border=SystemColors.WindowText; }
   BackColor=background; PaintControls(this); ShowVersion();PaintButton(toggle,true); PaintActionGroup(); WindowsIntegration.ThemeCaption(this,dark,background,foreground);
   if(diagnosticsWindow!=null && !diagnosticsWindow.IsDisposed) { ((EngineConsoleForm)diagnosticsWindow).ApplyPalette(dark); }
   PaintAccessChips(); if(accessFull!=null && !accessFull.IsDisposed) accessFull.ApplyPalette(dark);
   if(tray!=null) MenuAppearance.Apply(tray.ContextMenuStrip,surface,foreground,border,230,true);
   if(pauseMenu.Visible) pauseMenu.Close();
   heroCard.Glow=status.State==StatusLabel.Mark.Running?StatusLabel.Green:status.State==StatusLabel.Mark.Paused?StatusLabel.Amber:status.State==StatusLabel.Mark.Busy?AppPalette.Accent:Color.Empty;
   Invalidate(true);
  }
  // The color behind a control: the card's surface inside a SoftPanel, else the window background.
  Color HostColor(Control control) { for(Control parent=control.Parent;parent!=null;parent=parent.Parent) if(parent is SoftPanel) return surface; return background; }
  void PaintButton(SoftButton button,bool primary) { button.Fill=primary?AppPalette.Accent:surface; button.Ink=primary?AppPalette.AccentInk:foreground; button.Edge=primary?button.Fill:border; if(SystemInformation.HighContrast) { button.Fill=primary?foreground:background; button.Ink=primary?background:foreground; } Color host=background; for(Control parent=button.Parent;parent!=null;parent=parent.Parent) { if(parent is SoftPanel) { host=surface; break; } } button.BackColor=host; button.ForeColor=button.Ink; button.Invalidate(); }
  protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); ApplyTheme(true); }
  void PaintControls(Control parent) {
   foreach(Control control in parent.Controls) {
    var panel=control as SoftPanel; var button=control as SoftButton;
    control.ForeColor=Convert.ToString(control.Tag)=="muted"?secondary:Convert.ToString(control.Tag)=="accent" && !SystemInformation.HighContrast?(dark?AppPalette.Mint:AppPalette.Accent):foreground;
    if(panel!=null) { panel.Fill=surface; panel.Edge=border; panel.BackColor=background; }
    else if(button!=null) PaintButton(button,false);
    else if(control is ThemeCheckBox) { var box=(ThemeCheckBox)control; box.Accent=AppPalette.Accent; box.Tick=AppPalette.AccentInk; box.Edge=border; box.Muted=secondary; box.BackColor=HostColor(box); box.Invalidate(); }
    else if(control is SoftSwitch) { var sw=(SoftSwitch)control; sw.On=SystemInformation.HighContrast?SystemColors.Highlight:AppPalette.Accent; sw.Off=SystemInformation.HighContrast?SystemColors.GrayText:dark?Color.FromArgb(78,86,102):Color.FromArgb(190,196,206); sw.Knob=Color.White; sw.BackColor=HostColor(sw); sw.Invalidate(); }
    else if(control is NoticeView) { var terms=(NoticeView)control; terms.BackColor=surface; terms.ForeColor=foreground; terms.ThumbColor=secondary; terms.EdgeColor=border; terms.Invalidate(); }
    else if(control is LinkLabel) { var link=(LinkLabel)control; link.LinkColor=link.ActiveLinkColor=link.VisitedLinkColor=SystemInformation.HighContrast?SystemColors.HotTrack:dark?AppPalette.Mint:AppPalette.Accent; link.BackColor=Color.Transparent; }
    else if(control is ComboBox || control is TextBox) control.BackColor=surface;
    else if(control is Label || control is CheckBox || control is TableLayoutPanel || control is FlowLayoutPanel) control.BackColor=Color.Transparent;
    else control.BackColor=background;
    PaintControls(control);
   }
  }
  bool resourcesDisposed;
  protected override void Dispose(bool disposing) {
   if(disposing) accessAnimation.Dispose();
   if(disposing && !resourcesDisposed) { resourcesDisposed=true; if(diagnosticsWindow!=null) diagnosticsWindow.Dispose(); if(accessFull!=null) accessFull.Dispose(); SystemEvents.UserPreferenceChanged-=ThemeChanged; timer.Dispose(); themeMenu.Dispose(); pauseMenu.Dispose(); if(commands!=null) commands.Dispose(); if(reveal!=null) reveal.Dispose(); if(tray!=null) { tray.Visible=false; tray.ContextMenuStrip.Dispose(); tray.Dispose(); } }
   base.Dispose(disposing);
  }
 }
}
