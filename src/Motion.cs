using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace GoodbyeTray {
 // One UI-thread timer drives every animation: tweens that ease to a target and controls that loop (pulse, spin, shimmer).
 // With Windows' "Animation effects" off, tweens jump to their target and loops draw a still frame.
 static class Motion {
  [DllImport("user32.dll")] static extern bool SystemParametersInfo(int action,int param,out bool value,int ignored);
  const int GetClientAreaAnimation=0x1042;
  static readonly List<Tween> tweens=new List<Tween>();
  static readonly List<Control> loops=new List<Control>();
  static Timer timer;
  // Tests may switch animations off to read final colors at once.
  internal static bool? Override=null;
  internal static bool Enabled {
   get {
    if(Override.HasValue) return Override.Value;
    if(SystemInformation.HighContrast || !SystemInformation.UIEffectsEnabled) return false;
    bool on; return !SystemParametersInfo(GetClientAreaAnimation,0,out on,0) || on;
   }
  }
  // Milliseconds since start; loops derive their phase from it so every control stays in step.
  internal static int Now { get { return Environment.TickCount; } }
  internal static float EaseOut(float t) { t=Math.Max(0,Math.Min(1,t)); float u=1-t; return 1-u*u*u; }
  internal static float EaseInOut(float t) { t=Math.Max(0,Math.Min(1,t)); return t<0.5f?4*t*t*t:1-(float)Math.Pow(-2*t+2,3)/2; }
  internal static Color Mix(Color a,Color b,float t) { t=Math.Max(0,Math.Min(1,t)); return Color.FromArgb((int)(a.A+(b.A-a.A)*t),(int)(a.R+(b.R-a.R)*t),(int)(a.G+(b.G-a.G)*t),(int)(a.B+(b.B-a.B)*t)); }
  internal static void Loop(Control control,bool on) {
   if(on && Enabled) { if(!loops.Contains(control)) { loops.Add(control); Ensure(); } }
   else loops.Remove(control);
  }
  internal static void Add(Tween tween) { if(!tweens.Contains(tween)) tweens.Add(tween); Ensure(); }
  static void Ensure() {
   if(timer==null) { timer=new Timer { Interval=15 }; timer.Tick+=delegate { Frame(); }; }
   if(!timer.Enabled) timer.Start();
  }
  static void Frame() {
   for(int i=tweens.Count-1;i>=0;i--) { var tween=tweens[i]; if(!tween.Step()) tweens.RemoveAt(i); }
   for(int i=loops.Count-1;i>=0;i--) {
    var control=loops[i];
    if(control.IsDisposed) { loops.RemoveAt(i); continue; }
    if(control.Visible && control.IsHandleCreated) control.Invalidate();
   }
   if(tweens.Count==0 && loops.Count==0) timer.Stop();
  }
 }
 // A number that eases from where it is to a target and repaints its owner on each frame.
 sealed class Tween {
  readonly Control owner;
  float from,to,value; int start,duration;
  internal Tween(Control owner,float initial) { this.owner=owner; value=to=from=initial; }
  internal float Value { get { return value; } }
  internal float Target { get { return to; } }
  internal void To(float target,int milliseconds) {
   if(target==to) return;
   from=value; to=target; start=Motion.Now; duration=milliseconds;
   if(!Motion.Enabled || milliseconds<=0) { value=to; Repaint(); return; }
   Motion.Add(this);
  }
  internal void Jump(float target) { from=to=value=target; Repaint(); }
  internal bool Step() {
   if(owner.IsDisposed) return false;
   float t=(Motion.Now-start)/(float)Math.Max(1,duration);
   value=t>=1?to:from+(to-from)*Motion.EaseOut(t);
   Repaint(); return t<1;
  }
  void Repaint() { if(!owner.IsDisposed && owner.IsHandleCreated) owner.Invalidate(); }
 }
 // Standard square checkboxes, colored by the app rather than Windows' accent.
 sealed class ThemeCheckBox : CheckBox {
  internal Color Accent,Tick,Edge,Muted;
  bool hovered;
  public ThemeCheckBox() { SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true); }
  protected override void OnMouseEnter(EventArgs e) { hovered=true; Invalidate(); base.OnMouseEnter(e); }
  protected override void OnMouseLeave(EventArgs e) { hovered=false; Invalidate(); base.OnMouseLeave(e); }
  protected override void OnPaint(PaintEventArgs e) {
   if(SystemInformation.HighContrast) { base.OnPaint(e); return; }
   // Own the entire back buffer so previous window pixels cannot show through.
   e.Graphics.Clear(BackColor);
   int side=Math.Max(13,(int)Math.Round(Font.Height*0.8)), top=(Height-side)/2;
   var box=new Rectangle(1,top,side,side);
   Color outline=Enabled?(hovered?Accent:Muted):Edge;
   using(var fill=new SolidBrush(Checked?(Enabled?Accent:Edge):BackColor)) e.Graphics.FillRectangle(fill,box);
   using(var pen=new Pen(Checked && Enabled?Accent:outline)) e.Graphics.DrawRectangle(pen,box);
   if(Checked) {
    e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
    using(var pen=new Pen(Enabled?Tick:Muted,1.8f)) e.Graphics.DrawLines(pen,new[]{new PointF(box.Left+side*0.2f,top+side*0.5f),new PointF(box.Left+side*0.43f,top+side*0.72f),new PointF(box.Left+side*0.82f,top+side*0.27f)});
   }
   var text=new Rectangle(side+7,0,Math.Max(1,Width-side-7),Height);
   TextRenderer.DrawText(e.Graphics,Text,Font,text,Enabled?ForeColor:Muted,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
   if(Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics,text,ForeColor,BackColor);
  }
 }
 // A check box drawn as a sliding switch. It stays a CheckBox, so Checked, CheckedChanged and keyboard use are unchanged.
 class SoftSwitch : CheckBox {
  internal Color On=Color.FromArgb(37,99,235),Off=Color.FromArgb(180,186,196),Knob=Color.White;
  readonly Tween slide,hover;
  public SoftSwitch() {
   SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
   Cursor=Cursors.Hand; slide=new Tween(this,0); hover=new Tween(this,0);
  }
  const int TrackWidth=38,TrackHeight=22;
  // Draws only the switch; the text stays for screen readers while a label elsewhere shows it.
  internal bool TrackOnly;
  public override Size GetPreferredSize(Size proposed) { if(TrackOnly) return new Size(TrackWidth+4,TrackHeight+6); var text=TextRenderer.MeasureText(Text,Font); return new Size(TrackWidth+12+text.Width+4,Math.Max(TrackHeight+4,text.Height+4)); }
  protected override void OnCheckedChanged(EventArgs e) { slide.To(Checked?1:0,220); base.OnCheckedChanged(e); }
  protected override void OnHandleCreated(EventArgs e) { slide.Jump(Checked?1:0); base.OnHandleCreated(e); }
  protected override void OnMouseEnter(EventArgs e) { hover.To(1,140); base.OnMouseEnter(e); }
  protected override void OnMouseLeave(EventArgs e) { hover.To(0,180); base.OnMouseLeave(e); }
  protected override void OnPaint(PaintEventArgs e) {
   var g=e.Graphics; g.Clear(BackColor); g.SmoothingMode=SmoothingMode.AntiAlias;
   // Checked can change before the handle exists or while hidden; never draw a stale position.
   if(slide.Target!=(Checked?1:0)) slide.Jump(Checked?1:0);
   float t=slide.Value, lift=Enabled?hover.Value:0;
   var track=new RectangleF(1,(Height-TrackHeight)/2f,TrackWidth,TrackHeight);
   Color off=Enabled?Off:Motion.Mix(Off,BackColor,0.4f), on=Enabled?On:Motion.Mix(On,Off,0.6f);
   Color fill=Motion.Mix(Motion.Mix(off,on,t),Color.White,lift*0.08f);
   using(var path=Pill(track)) using(var brush=new SolidBrush(fill)) g.FillPath(brush,path);
   float knob=TrackHeight-6, x=track.X+3+(TrackWidth-6-knob)*Motion.EaseInOut(t);
   // The knob stretches slightly mid-slide, which reads as movement rather than a jump.
   float stretch=(float)Math.Sin(Math.PI*t)*5;
   var shadow=new RectangleF(x-stretch/2,track.Y+4,knob+stretch,knob);
   using(var path=Pill(shadow)) using(var brush=new SolidBrush(Color.FromArgb(40,0,0,0))) { g.TranslateTransform(0,1); g.FillPath(brush,path); g.ResetTransform(); }
   using(var path=Pill(new RectangleF(x-stretch/2,track.Y+3,knob+stretch,knob))) using(var brush=new SolidBrush(Enabled?Knob:Motion.Mix(Knob,off,0.3f))) g.FillPath(brush,path);
   if(TrackOnly) { if(Focused && ShowFocusCues) using(var focus=new Pen(ForeColor) { DashStyle=DashStyle.Dot }) g.DrawRectangle(focus,0,0,Width-1,Height-1); return; }
   TextRenderer.DrawText(g,Text,Font,new Rectangle(TrackWidth+12,0,Math.Max(1,Width-TrackWidth-12),Height),Enabled?ForeColor:Motion.Mix(ForeColor,Off,0.5f),TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix|TextFormatFlags.EndEllipsis);
   if(Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(g,new Rectangle(TrackWidth+8,2,Math.Min(Width-TrackWidth-10,TextRenderer.MeasureText(Text,Font).Width+8),Height-4),ForeColor,Color.Transparent);
  }
  internal static GraphicsPath Pill(RectangleF r) {
   var path=new GraphicsPath(); float d=Math.Min(r.Height,r.Width);
   path.AddArc(r.X,r.Y,d,d,90,180); path.AddArc(r.Right-d,r.Y,d,d,270,180); path.CloseFigure(); return path;
  }
 }
 // The large status line with a live mark before it: a green shield with packets flowing through while running, a spinning ring while
 // starting or switching, an amber shield during a break and a grey one when stopped.
 sealed class StatusLabel : Label {
  internal enum Mark { None, Stopped, Running, Busy, Paused }
  Mark mark=Mark.None;
  readonly Tween appear;
  internal static readonly Color Green=Color.FromArgb(31,153,92),Amber=Color.FromArgb(214,148,30),Grey=Color.FromArgb(128,140,160);
  public StatusLabel() { SetStyle(ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint|ControlStyles.SupportsTransparentBackColor,true); appear=new Tween(this,1); Padding=new Padding(54,0,0,0); }
  internal Mark State {
   get { return mark; }
   set {
    if(value==mark) return;
    bool first=mark==Mark.None; mark=value;
    // A new state pops in: the mark grows from small with a light overshoot.
    if(!first) { appear.Jump(0.35f); appear.To(1,420); }
    Motion.Loop(this,mark==Mark.Running || mark==Mark.Busy); Invalidate();
   }
  }
  protected override void OnVisibleChanged(EventArgs e) { Motion.Loop(this,Visible && (mark==Mark.Running || mark==Mark.Busy)); base.OnVisibleChanged(e); }
  protected override void OnPaint(PaintEventArgs e) {
   base.OnPaint(e);
   if(mark==Mark.None) return;
   var g=e.Graphics; g.SmoothingMode=SmoothingMode.AntiAlias;
   float cx=23, cy=Height/2f, pop=appear.Value;
   // Overshoot: 0.35 → about 1.12 → 1, so the mark settles instead of stopping dead.
   float scale=pop>=1?1:(float)(pop+Math.Sin(Math.PI*pop)*0.25);
   bool still=!Motion.Enabled;
   Color color=mark==Mark.Running?Green:mark==Mark.Paused?Amber:mark==Mark.Busy?AppPalette.Accent:Grey;
   if(SystemInformation.HighContrast) color=ForeColor;
   if(mark==Mark.Busy) {
    float angle=still?0:(Motion.Now%900)/900f*360;
    float sweep=still?270:200+(float)Math.Sin(Motion.Now/300.0)*60;
    using(var track=new Pen(Color.FromArgb(50,color),2.6f)) g.DrawEllipse(track,cx-11*scale,cy-11*scale,22*scale,22*scale);
    using(var pen=new Pen(color,2.6f) { StartCap=LineCap.Round,EndCap=LineCap.Round }) g.DrawArc(pen,cx-11*scale,cy-11*scale,22*scale,22*scale,angle,sweep);
    return;
   }
   // The app's shield; while running, packets flow through it from left to right.
   float h=30*scale;
   // A soft halo in the state color sits behind the shield.
   using(var halo=new SolidBrush(Color.FromArgb(SystemInformation.HighContrast?0:38,color))) g.FillEllipse(halo,cx-21*scale,cy-21*scale,42*scale,42*scale);
   using(var shield=Shield(cx,cy,h)) {
    using(var brush=new SolidBrush(color)) g.FillPath(brush,shield);
    float left=cx-h*24/56f, right=cx+h*24/56f;
    if(mark==Mark.Running) {
     int count=still?2:3;
     for(int i=0;i<count;i++) {
      // Each packet crosses the mark in 1.5 s; three of them a third apart keep the flow steady.
      float phase=still?(i+1)/3f:((Motion.Now+i*500)%1500)/1500f;
      float x=1+phase*(cx*2-2), fade=Math.Min(1,Math.Min(phase,1-phase)*5);
      Color dot=x>left+1 && x<right-1?Color.White:color;
      using(var b=new SolidBrush(Color.FromArgb((int)(255*fade),dot))) g.FillEllipse(b,x-2.8f,cy-2.8f,5.6f,5.6f);
     }
    } else using(var line=new Pen(Color.FromArgb(220,Color.White),2.6f) { StartCap=LineCap.Round,EndCap=LineCap.Round }) {
     // Stopped and paused: the road through the shield is cut in the middle.
     g.DrawLine(line,left+3,cy,cx-2.5f,cy); g.DrawLine(line,cx+2.5f,cy,right-3,cy);
    }
   }
  }
  static GraphicsPath Shield(float cx,float cy,float height) {
   // The icon's 64-unit shield (x 8..56, y 4..60), scaled to the given height and centered.
   float s=height/56f, ox=cx-32*s, oy=cy-32*s;
   Func<float,float,PointF> p=(x,y)=>new PointF(ox+x*s,oy+y*s);
   var path=new GraphicsPath();
   path.AddLine(p(32,4),p(56,12)); path.AddLine(p(56,12),p(56,30));
   path.AddBezier(p(56,30),p(56,45),p(46,55),p(32,60)); path.AddBezier(p(32,60),p(18,55),p(8,45),p(8,30));
   path.AddLine(p(8,30),p(8,12)); path.CloseFigure(); return path;
  }
 }
 // The status card: a soft wash of the state color rises from its left edge and crossfades when the state changes.
 // While running it breathes slowly so the card feels alive without drawing the eye.
 sealed class GlowPanel : SoftPanel {
  Color from=Color.Empty,to=Color.Empty; bool breathing;
  readonly Tween fade;
  // Breathing repaints the whole card, children included, so it runs on its own slower timer instead of every frame.
  readonly Timer beat=new Timer { Interval=70 };
  public GlowPanel() { fade=new Tween(this,1); beat.Tick+=delegate { if(Visible && IsHandleCreated) Invalidate(); }; }
  // DoubleBuffer covers only this panel's paint. Transparent child HWNDs must
  // also finish painting before Windows presents a new animation frame.
  protected override CreateParams CreateParams {
   get { var cp=base.CreateParams; cp.ExStyle|=0x02000000; return cp; } // WS_EX_COMPOSITED, scoped to the animated card.
  }
  // The labels and layouts on the card are transparent: they show the glow only when they repaint too.
  protected override void OnInvalidated(InvalidateEventArgs e) { base.OnInvalidated(e); foreach(Control child in Controls) child.Invalidate(true); }
  protected override void Dispose(bool disposing) { if(disposing) beat.Dispose(); base.Dispose(disposing); }
  void Pace() { beat.Enabled=breathing && Visible && Motion.Enabled; }
  internal Color Glow { get { return to; } set { if(value==to) return; from=Current; to=value; fade.Jump(0); fade.To(1,600); } }
  internal bool Breathing { get { return breathing; } set { if(breathing==value) return; breathing=value; Pace(); } }
  protected override void OnVisibleChanged(EventArgs e) { Pace(); base.OnVisibleChanged(e); }
  protected override void OnHandleDestroyed(EventArgs e) { beat.Enabled=false; base.OnHandleDestroyed(e); }
  Color Current { get { float t=fade.Value; Color a=from.IsEmpty?Color.FromArgb(0,to):from, b=to.IsEmpty?Color.FromArgb(0,a):to; return Color.FromArgb((int)(a.A+(b.A-a.A)*t),(int)(a.R+(b.R-a.R)*t),(int)(a.G+(b.G-a.G)*t),(int)(a.B+(b.B-a.B)*t)); } }
  protected override void OnPaint(PaintEventArgs e) {
   base.OnPaint(e);
   if(SystemInformation.HighContrast || Width<40 || Height<40) return;
   Color c=Current; if(c.A==0) return;
   float beat=breathing && Motion.Enabled?(float)(0.5+0.5*Math.Sin(Motion.Now/1100.0)):0.6f;
   int alpha=(int)(c.A/255f*(26+beat*20));
   var g=e.Graphics; g.SmoothingMode=SmoothingMode.AntiAlias;
   using(var clip=SoftButton.Round(new Rectangle(2,2,Width-5,Height-5),Math.Min(22,Math.Min(Width-5,Height-5)))) {
    var saved=g.Save(); g.SetClip(clip);
    // A wide ellipse behind the status line, fading to nothing toward the middle of the card.
    using(var glow=new GraphicsPath()) {
     glow.AddEllipse(-Width*0.35f,-Height*0.9f,Width*1.0f,Height*1.5f);
     using(var brush=new PathGradientBrush(glow) { CenterColor=Color.FromArgb(alpha,c),SurroundColors=new[]{Color.FromArgb(0,c)},CenterPoint=new PointF(Width*0.08f,Height*0.22f) }) g.FillPath(brush,glow);
    }
    g.Restore(saved);
   }
  }
 }
 // The header logo: the app icon inside a ring that takes the engine's state color (green running, amber on a break,
 // accent while busy, grey stopped). A change crossfades the color and sends one ripple out; only the busy state
 // keeps animating (a short spinning arc), so a quiet window costs nothing.
 sealed class LiveLogo : Control {
  StatusLabel.Mark mark=StatusLabel.Mark.None;
  Color from=StatusLabel.Grey,to=StatusLabel.Grey;
  readonly Tween blend,ripple;
  public LiveLogo() { SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true); blend=new Tween(this,1); ripple=new Tween(this,1); AccessibleRole=AccessibleRole.Graphic; }
  static Color ColorOf(StatusLabel.Mark state) { return state==StatusLabel.Mark.Running?StatusLabel.Green:state==StatusLabel.Mark.Paused?StatusLabel.Amber:state==StatusLabel.Mark.Busy?AppPalette.Accent:StatusLabel.Grey; }
  internal StatusLabel.Mark State {
   get { return mark; }
   set {
    if(value==mark) return;
    bool first=mark==StatusLabel.Mark.None; mark=value;
    from=Motion.Mix(from,to,blend.Value); to=ColorOf(value);
    if(first) { from=to; blend.Jump(1); } else { blend.Jump(0); blend.To(1,450); ripple.Jump(0); ripple.To(1,900); }
    Motion.Loop(this,mark==StatusLabel.Mark.Busy); Invalidate();
   }
  }
  protected override void OnVisibleChanged(EventArgs e) { Motion.Loop(this,Visible && mark==StatusLabel.Mark.Busy); base.OnVisibleChanged(e); }
  protected override void OnPaint(PaintEventArgs e) {
   var g=e.Graphics; g.Clear(BackColor); g.SmoothingMode=SmoothingMode.AntiAlias;
   float size=Math.Min(Width,Height)-12, x=(Width-size)/2f, y=(Height-size)/2f;
   if(size<10) return;
   Color color=SystemInformation.HighContrast?ForeColor:Motion.Mix(from,to,blend.Value);
   // One ripple per change: a ring that grows past the logo and fades.
   if(ripple.Value<1) { float grow=ripple.Value*6; using(var pen=new Pen(Color.FromArgb((int)(150*(1-ripple.Value)),color),2f)) g.DrawEllipse(pen,x-grow,y-grow,size+grow*2,size+grow*2); }
   using(var halo=new SolidBrush(Color.FromArgb(28,color))) g.FillEllipse(halo,x,y,size,size);
   if(mark==StatusLabel.Mark.Busy && Motion.Enabled) {
    float angle=(Motion.Now%1000)/1000f*360;
    using(var track=new Pen(Color.FromArgb(60,color),3f)) g.DrawEllipse(track,x+1.5f,y+1.5f,size-3,size-3);
    using(var arc=new Pen(color,3f) { StartCap=LineCap.Round,EndCap=LineCap.Round }) g.DrawArc(arc,x+1.5f,y+1.5f,size-3,size-3,angle,110);
   } else using(var ring=new Pen(color,3f)) g.DrawEllipse(ring,x+1.5f,y+1.5f,size-3,size-3);
   int icon=(int)(size*0.62f); g.DrawImageUnscaled(Program.Logo(icon),(int)Math.Round(Width/2f-icon/2f),(int)Math.Round(Height/2f-icon/2f));
  }
 }
 // "Goodbye" in the text color, "DPI" in the accent color.
 sealed class WordMark : Control {
  internal Color Accent=AppPalette.Accent;
  public WordMark() { SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true); Text="GoodbyeDPI"; AccessibleRole=AccessibleRole.StaticText; }
  protected override void OnPaint(PaintEventArgs e) {
   var g=e.Graphics; g.Clear(BackColor);
   const TextFormatFlags flags=TextFormatFlags.NoPadding|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix;
   int first=TextRenderer.MeasureText(g,"Goodbye",Font,Size.Empty,flags).Width;
   TextRenderer.DrawText(g,"Goodbye",Font,new Rectangle(0,0,first+2,Height),ForeColor,flags);
   TextRenderer.DrawText(g,"DPI",Font,new Rectangle(first,0,Math.Max(1,Width-first),Height),SystemInformation.HighContrast?ForeColor:Accent,flags);
  }
 }
 // One setting as a tile: an icon, the title and the switch on top, the explanation below. The whole tile toggles the
 // switch; an "on" tile gets an accent edge.
 sealed class SettingTile : Panel {
  internal readonly SoftSwitch Switch;
  internal readonly Label Icon=new Label(), Title=new Label(), Help;
  internal Color Host,Fill,Edge,Accent;
  readonly Tween on;
  internal SettingTile(SoftSwitch control,string glyph,Control extra,Label help=null) {
   Switch=control; Help=help ?? new Label(); SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
   on=new Tween(this,control.Checked?1:0); Padding=new Padding(14,10,12,10); Cursor=Cursors.Hand;
   var grid=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=4,RowCount=2,Margin=Padding.Empty,BackColor=Color.Transparent };
   grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,42)); grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,48));
   grid.RowStyles.Add(new RowStyle(SizeType.Absolute,36)); grid.RowStyles.Add(new RowStyle(SizeType.Percent,100));
   string family=Array.Exists(FontFamily.Families,f=>f.Name=="Segoe Fluent Icons")?"Segoe Fluent Icons":"Segoe MDL2 Assets";
   Icon.Text=glyph; Icon.Font=new Font(family,14); Icon.Tag="accent"; Icon.TextAlign=ContentAlignment.MiddleCenter; Icon.Size=new Size(34,34); Icon.Margin=new Padding(0,1,0,0); Icon.Paint+=PaintIconBack; grid.Controls.Add(Icon,0,0);
   Title.Font=new Font("Segoe UI",10,FontStyle.Bold); Title.Dock=DockStyle.Fill; Title.TextAlign=ContentAlignment.MiddleLeft; Title.AutoEllipsis=true; Title.Margin=new Padding(6,0,4,0); Title.Text=control.Text; grid.Controls.Add(Title,1,0);
   if(extra!=null) { extra.Anchor=AnchorStyles.Right; grid.Controls.Add(extra,2,0); }
   control.TrackOnly=true; control.AutoSize=false; control.Size=control.GetPreferredSize(Size.Empty); control.Anchor=AnchorStyles.Right; control.Margin=Padding.Empty; grid.Controls.Add(control,3,0);
   Help.Font=new Font("Segoe UI",9); Help.Tag="muted"; Help.Dock=DockStyle.Fill; Help.Padding=Padding.Empty; Help.AutoEllipsis=true; Help.Margin=new Padding(48,2,4,0); Help.TextAlign=ContentAlignment.TopLeft; grid.Controls.Add(Help,0,1); grid.SetColumnSpan(Help,4);
   Controls.Add(grid);
   control.TextChanged+=delegate { Title.Text=control.Text; };
   control.CheckedChanged+=delegate { on.To(control.Checked?1:0,220); };
   control.EnabledChanged+=delegate { Cursor=control.Enabled?Cursors.Hand:Cursors.Default; Invalidate(true); };
   // A click anywhere on the tile flips the switch, as if the switch itself was clicked.
   EventHandler flip=delegate { if(Switch.Enabled) Switch.Checked=!Switch.Checked; };
   Click+=flip; grid.Click+=flip; Title.Click+=flip; Help.Click+=flip; Icon.Click+=flip;
  }
  void PaintIconBack(object sender,PaintEventArgs e) {
   // A rounded square of the accent color behind the icon glyph.
   e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
   using(var path=SoftButton.Round(new Rectangle(0,0,Icon.Width-1,Icon.Height-1),12)) using(var b=new SolidBrush(Color.FromArgb(SystemInformation.HighContrast?0:40,Accent))) e.Graphics.FillPath(b,path);
   TextRenderer.DrawText(e.Graphics,Icon.Text,Icon.Font,Icon.ClientRectangle,Icon.ForeColor,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPadding);
  }
  protected override void OnPaintBackground(PaintEventArgs e) {
   var g=e.Graphics; g.Clear(Host); g.SmoothingMode=SmoothingMode.AntiAlias;
   float t=Switch.Enabled?on.Value:0;
   using(var path=SoftButton.Round(new Rectangle(1,1,Width-3,Height-3),16)) using(var b=new SolidBrush(Fill)) using(var pen=new Pen(Motion.Mix(Edge,Accent,0.75f*t),1+t*0.6f)) { g.FillPath(b,path); g.DrawPath(pen,path); }
  }
  protected override void OnPaint(PaintEventArgs e) {}
  // Labels on the tile are transparent and show its wash only when they repaint with it.
  protected override void OnInvalidated(InvalidateEventArgs e) { base.OnInvalidated(e); foreach(Control child in Controls) child.Invalidate(true); }
 }}
