using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace GoodbyeTray {
 // Fonts used while painting are made once and kept: creating a GDI font on every frame wastes handles and time.
 static class Fonts {
  static readonly Dictionary<string,Font> cache=new Dictionary<string,Font>();
  internal static Font Get(string family,float size,FontStyle style=FontStyle.Regular,GraphicsUnit unit=GraphicsUnit.Point) {
   string key=family+"|"+size+"|"+(int)style+"|"+(int)unit; Font font;
   if(!cache.TryGetValue(key,out font)) { font=new Font(family,size,style,unit); cache[key]=font; }
   return font;
  }
  internal static Font Get(FontFamily family,float size,FontStyle style) { return Get(family.Name,size,style); }
  internal static Font Get(FontFamily family,float size) { return Get(family.Name,size); }
  internal static Font Get(string family,float size,GraphicsUnit unit) { return Get(family,size,FontStyle.Regular,unit); }
  internal static Font Get(Font prototype,FontStyle style) { return Get(prototype.FontFamily.Name,prototype.Size,style,prototype.Unit); }
  // Dialog headings in a serif face that ships with Windows; Segoe UI where neither is installed.
  static string serifFamily;
  internal static Font Serif(float size) {
   if(serifFamily==null) serifFamily=Array.Exists(FontFamily.Families,f=>f.Name=="Georgia")?"Georgia":Array.Exists(FontFamily.Families,f=>f.Name=="Cambria")?"Cambria":"Segoe UI";
   return Get(serifFamily,size,FontStyle.Bold);
  }
 } // One UI-thread timer drives every animation: tweens that ease to a target and controls that loop (pulse, spin, shimmer).
 // With Windows' "Animation effects" off, tweens jump to their target and loops draw a still frame.
 static class Motion {
  [DllImport("user32.dll")] static extern bool SystemParametersInfo(int action,int param,out bool value,int ignored);
  const int GetClientAreaAnimation=0x1042;
  static readonly List<Tween> tweens=new List<Tween>();
  static readonly List<Control> loops=new List<Control>();
  static Timer timer;
  // Endless animations (spinners, steam, the flowing packets) repaint at this pace: about 30 frames a second while
  // the window is in front, 10 behind another window, none while it is minimized or hidden. Tweens always run at full rate.
  internal const int Front=33, Behind=100, Stopped=Int32.MaxValue;
  static int pace=Front, lastLoop;
  internal static int Pace { get { return pace; } }
  internal static void SetPace(int interval) { if(pace==interval) return; pace=interval; if(pace!=Stopped && loops.Count>0) Ensure(); }
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
   if(pace!=Stopped && unchecked(Now-lastLoop)>=pace) {
    lastLoop=Now;
    for(int i=loops.Count-1;i>=0;i--) {
     var control=loops[i];
     if(control.IsDisposed) { loops.RemoveAt(i); continue; }
     if(control.Visible && control.IsHandleCreated) control.Invalidate();
    }
   }
   // Nothing to ease and the loops are paused or gone: let the timer sleep until something asks for frames again.
   if(tweens.Count==0 && (loops.Count==0 || pace==Stopped)) timer.Stop();
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
 // The settings check box in the app's style: a rounded square that fills with the accent color, a tick that draws
 // itself in, and a soft tint under the mouse. It stays a CheckBox, so Checked, CheckedChanged and keys are unchanged.
 sealed class ThemeCheckBox : CheckBox {
  internal Color Accent,Tick,Edge,Muted;
  readonly Tween mark,hover;
  public ThemeCheckBox() { UseVisualStyleBackColor=false; SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true); Cursor=Cursors.Hand; mark=new Tween(this,0); hover=new Tween(this,0); }
  protected override void OnPaintBackground(PaintEventArgs e) { e.Graphics.Clear(BackColor); }
  protected override void OnMouseEnter(EventArgs e) { hover.To(1,140); base.OnMouseEnter(e); }
  protected override void OnMouseLeave(EventArgs e) { hover.To(0,200); base.OnMouseLeave(e); }
  protected override void OnCheckedChanged(EventArgs e) { mark.To(Checked?1:0,Checked?260:140); base.OnCheckedChanged(e); }
  protected override void OnHandleCreated(EventArgs e) { mark.Jump(Checked?1:0); base.OnHandleCreated(e); }
  protected override void OnPaint(PaintEventArgs e) {
   // Native CheckBox background painting can leave an uninitialized buffer
   // when visual styles and custom painting are mixed. Always paint the row.
   var g=e.Graphics; g.Clear(BackColor);
   if(SystemInformation.HighContrast) { base.OnPaint(e); return; }
   if(mark.Target!=(Checked?1:0)) mark.Jump(Checked?1:0);
   float t=mark.Value, lift=Enabled?hover.Value:0;
   int side=Math.Max(16,(int)Math.Round(Font.Height*0.95)), top=(Height-side)/2;
   var box=new RectangleF(1.5f,top+0.5f,side-1,side-1);
   g.SmoothingMode=SmoothingMode.AntiAlias;
   Color on=Enabled?Accent:Edge, off=Motion.Mix(BackColor,Accent,lift*0.14f);
   using(var path=SoftButton.RoundF(box,side*0.42f)) {
    using(var fill=new SolidBrush(Motion.Mix(off,on,Math.Min(1,t*1.6f)))) g.FillPath(fill,path);
    using(var pen=new Pen(Motion.Mix(Enabled?Motion.Mix(Muted,Accent,lift):Edge,on,t),1.4f)) g.DrawPath(pen,path);
   }
   if(t>0.01f) {
    // The tick is drawn along its length as the box fills.
    var a=new PointF(box.Left+side*0.24f,top+side*0.52f); var b=new PointF(box.Left+side*0.43f,top+side*0.70f); var c=new PointF(box.Left+side*0.76f,top+side*0.32f);
    float first=Math.Min(1,t*2.2f), second=Math.Max(0,Math.Min(1,(t-0.4f)/0.6f));
    using(var pen=new Pen(Enabled?Tick:Muted,2f) { StartCap=LineCap.Round,EndCap=LineCap.Round,LineJoin=LineJoin.Round }) {
     var mid=new PointF(a.X+(b.X-a.X)*first,a.Y+(b.Y-a.Y)*first); g.DrawLine(pen,a,mid);
     if(second>0) g.DrawLine(pen,b,new PointF(b.X+(c.X-b.X)*second,b.Y+(c.Y-b.Y)*second));
    }
   }
   var text=new Rectangle(side+10,0,Math.Max(1,Width-side-10),Height);
   TextRenderer.DrawText(g,Text,Font,text,Enabled?ForeColor:Muted,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
   if(Focused && ShowFocusCues) using(var focus=new Pen(Accent) { DashStyle=DashStyle.Dot }) g.DrawRectangle(focus,text.X-3,2,Math.Min(text.Width,TextRenderer.MeasureText(Text,Font).Width+6),Height-5);
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
  int lastBeat;
  public GlowPanel() { fade=new Tween(this,1); beat.Tick+=delegate { // The breathing is slow by nature: about 8 frames a second in front is enough, and behind other windows it rests.
   if(Motion.Pace!=Motion.Front || unchecked(Motion.Now-lastBeat)<120) return; lastBeat=Motion.Now; if(Visible && IsHandleCreated) Invalidate(); }; }
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
 // The engine's controls as one drawn pill: a break part with a steaming cup on the left and the main part
 // (Aç / Kapat / Devam et) filling the rest. Drawn as a single control so no inner button corners show through.
 sealed class SessionPill : Control {
  internal enum Mode { Stopped, Running, Paused }
  internal Color Host,Frame,Edge,Ink,Muted,Accent,AccentInk,Danger,Amber;
  internal event Action PrimaryClick,BreakClick;
  internal event Action<int> HoverChanged;
  internal string BreakText="";
  Mode mode=Mode.Stopped; bool busy,breakEnabled=true;
  int hot=-1,pressed=-1,focus=1;
  Color fromFill,toFill,fromInk,toInk;
  readonly Tween fade,hoverBreak,hoverMain,sink,ring;
  public SessionPill() {
   SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw|ControlStyles.Selectable,true);
   TabStop=true; Cursor=Cursors.Hand; AccessibleRole=AccessibleRole.PushButton;
   fade=new Tween(this,1); hoverBreak=new Tween(this,0); hoverMain=new Tween(this,0); sink=new Tween(this,0); ring=new Tween(this,1);
  }
  internal Mode State { get { return mode; } set { if(mode==value) return; mode=value; Recolor(true); Loop(); } }
  internal bool Busy { get { return busy; } set { if(busy==value) return; busy=value; Loop(); Invalidate(); } }
  internal bool BreakEnabled { get { return breakEnabled; } set { if(breakEnabled==value) return; breakEnabled=value; Loop(); Invalidate(); } }
  // Colors of the main part for the current state; a state change crossfades to them.
  void Targets(out Color fill,out Color ink) {
   if(SystemInformation.HighContrast) { fill=Ink; ink=Frame; return; }
   bool dark=Frame.GetBrightness()<0.5f;
   if(mode==Mode.Running) { fill=Motion.Mix(Frame,Danger,dark?0.18f:0.11f); ink=Danger; }
   else if(mode==Mode.Paused) { fill=Motion.Mix(Frame,Amber,dark?0.20f:0.14f); ink=dark?Amber:Motion.Mix(Amber,Color.Black,0.35f); }
   else { fill=Accent; ink=AccentInk; }
  }
  internal void Recolor(bool animate) {
   Color fill,ink; Targets(out fill,out ink);
   if(animate && Motion.Enabled && IsHandleCreated) { fromFill=CurrentFill; fromInk=CurrentInk; toFill=fill; toInk=ink; fade.Jump(0); fade.To(1,320); }
   else { fromFill=toFill=fill; fromInk=toInk=ink; fade.Jump(1); }
   Invalidate();
  }
  // New theme colors without restarting a state crossfade that is still running.
  internal void Retheme() { Color fill,ink; Targets(out fill,out ink); toFill=fill; toInk=ink; if(fade.Value>=1) { fromFill=fill; fromInk=ink; } Invalidate(); }
  Color CurrentFill { get { return Motion.Mix(fromFill,toFill,fade.Value); } }
  Color CurrentInk { get { return Motion.Mix(fromInk,toInk,fade.Value); } }
  // Steam and the spinner need frames; everything else is driven by tweens.
  void Loop() { Motion.Loop(this,Visible && (busy || (breakEnabled && (mode==Mode.Paused || hot==0)))); }
  int Pad { get { return 4; } }
  Rectangle BreakRect { get { int h=Height-Pad*2; return new Rectangle(Pad,Pad,Math.Min(56,h+8),h); } }
  Rectangle MainRect { get { var b=BreakRect; return new Rectangle(b.Right+4,Pad,Width-b.Right-4-Pad,Height-Pad*2); } }
  int PartAt(Point p) { if(BreakRect.Contains(p)) return 0; if(MainRect.Contains(p)) return 1; return -1; }
  bool PartEnabled(int part) { return Enabled && (part==1 || breakEnabled); }
  protected override void OnMouseMove(MouseEventArgs e) { int part=PartAt(e.Location); if(part!=hot) { hot=part; hoverBreak.To(hot==0 && PartEnabled(0)?1:0,150); hoverMain.To(hot==1 && PartEnabled(1)?1:0,150); Cursor=hot>=0 && PartEnabled(hot)?Cursors.Hand:Cursors.Default; Loop(); if(HoverChanged!=null) HoverChanged(hot); } base.OnMouseMove(e); }
  protected override void OnMouseLeave(EventArgs e) { hot=-1; pressed=-1; hoverBreak.To(0,200); hoverMain.To(0,200); sink.To(0,160); Loop(); if(HoverChanged!=null) HoverChanged(-1); base.OnMouseLeave(e); }
  protected override void OnMouseDown(MouseEventArgs e) { if(e.Button==MouseButtons.Left) { pressed=PartAt(e.Location); if(pressed>=0 && PartEnabled(pressed)) { focus=pressed; sink.To(1,70); Focus(); } else pressed=-1; } base.OnMouseDown(e); }
  protected override void OnMouseUp(MouseEventArgs e) { int part=pressed; pressed=-1; sink.To(0,180); if(part>=0 && part==PartAt(e.Location)) Activate(part); base.OnMouseUp(e); }
  void Activate(int part) {
   if(!PartEnabled(part)) return;
   if(part==1) { ring.Jump(0.15f); ring.To(1,520); if(PrimaryClick!=null) PrimaryClick(); }
   else if(BreakClick!=null) BreakClick();
  }
  // Keyboard: Left/Right pick the part, Enter or Space presses it.
  protected override bool IsInputKey(Keys keyData) { return keyData==Keys.Left || keyData==Keys.Right || base.IsInputKey(keyData); }
  protected override void OnKeyDown(KeyEventArgs e) {
   if(e.KeyCode==Keys.Left) { focus=0; Invalidate(); e.Handled=true; }
   else if(e.KeyCode==Keys.Right) { focus=1; Invalidate(); e.Handled=true; }
   else if(e.KeyCode==Keys.Space || e.KeyCode==Keys.Enter) { Activate(focus); e.Handled=true; }
   base.OnKeyDown(e);
  }
  protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
  protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
  protected override void OnEnabledChanged(EventArgs e) { Loop(); Invalidate(); base.OnEnabledChanged(e); }
  protected override void OnVisibleChanged(EventArgs e) { Loop(); base.OnVisibleChanged(e); }
  protected override void OnHandleCreated(EventArgs e) { Recolor(false); base.OnHandleCreated(e); }
  protected override void OnHandleDestroyed(EventArgs e) { Motion.Loop(this,false); base.OnHandleDestroyed(e); }
  protected override void OnTextChanged(EventArgs e) { AccessibleName=Text; Invalidate(); base.OnTextChanged(e); }
  protected override void OnPaintBackground(PaintEventArgs e) { e.Graphics.Clear(Host); }
  protected override void OnPaint(PaintEventArgs e) {
   var g=e.Graphics; g.SmoothingMode=SmoothingMode.AntiAlias; g.PixelOffsetMode=PixelOffsetMode.HighQuality;
   if(Width<80 || Height<20) return;
   // The frame.
   using(var path=SoftButton.RoundF(new RectangleF(0.5f,0.5f,Width-1.5f,Height-1.5f),Math.Min(Height-1.5f,(12+Pad)*2))) using(var b=new SolidBrush(Frame)) using(var pen=new Pen(Edge)) { g.FillPath(b,path); g.DrawPath(pen,path); }
   float press=sink.Value;
   // Break part: a soft tint under the mouse, the cup in the middle.
   var br=BreakRect; float hb=hoverBreak.Value; bool breakOn=PartEnabled(0);
   if(hb>0.01f) using(var path=SoftButton.RoundF(Inset(br,pressed==0?press:0),24)) using(var b=new SolidBrush(Color.FromArgb((int)(28*hb),Ink))) g.FillPath(b,path);
   DrawCup(g,br,breakOn?Ink:Muted,breakOn && (mode==Mode.Paused || hot==0));
   // Main part: state color, lifted a little on hover, sunk while pressed.
   var mr=MainRect; Color fill=CurrentFill, ink=CurrentInk;
   if(!Enabled) { fill=Motion.Mix(fill,Frame,0.45f); ink=Motion.Mix(ink,Frame,0.45f); }
   fill=Motion.Mix(fill,mode==Mode.Stopped?Color.White:ink,hoverMain.Value*(mode==Mode.Stopped?0.10f:0.08f));
   using(var path=SoftButton.RoundF(Inset(mr,pressed==1?press:0),24)) using(var b=new SolidBrush(fill)) using(var pen=new Pen(mode==Mode.Stopped?fill:Motion.Mix(fill,ink,0.35f))) { g.FillPath(b,path); g.DrawPath(pen,path); }
   int textWidth=TextRenderer.MeasureText(Text,Font).Width, x=mr.X+Math.Max(10,(mr.Width-textWidth-28)/2), y=mr.Y+(mr.Height-18)/2;
   using(var pen=new Pen(ink,2f) { StartCap=LineCap.Round,EndCap=LineCap.Round }) {
    if(busy) { float a=Motion.Enabled?(Motion.Now%900)/900f*360:0; using(var track=new Pen(Color.FromArgb(60,ink),2f)) g.DrawEllipse(track,x+1,y+1,16,16); g.DrawArc(pen,x+1,y+1,16,16,a,110); }
    else if(mode==Mode.Paused) { var pts=new[]{new PointF(x+5,y+3),new PointF(x+15,y+9),new PointF(x+5,y+15)}; using(var b=new SolidBrush(ink)) g.FillPolygon(b,pts); }
    else { float r=Motion.EaseOut(ring.Value); g.DrawArc(pen,x+1.5f,y+2.5f,15,15,-50,280*r); g.DrawLine(pen,x+9,y+0.5f+(1-r)*4,x+9,y+8); }
   }
   TextRenderer.DrawText(g,Text,Font,new Rectangle(x+26,mr.Y,Math.Max(1,mr.Right-x-30),mr.Height),ink,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix|TextFormatFlags.EndEllipsis);
   if(Focused && ShowFocusCues) using(var dot=new Pen(Ink) { DashStyle=DashStyle.Dot }) g.DrawRectangle(dot,Rectangle.Inflate(focus==0?br:mr,-4,-4));
  }
  static RectangleF Inset(Rectangle r,float press) { float i=press*1.5f; return new RectangleF(r.X+i,r.Y+i,r.Width-i*2,r.Height-i*2); }
  // A cup on a saucer; with steam rising in two soft waves while the break is on offer or running.
  void DrawCup(Graphics g,Rectangle area,Color color,bool steam) {
   float cx=area.X+area.Width/2f-2, cy=area.Y+area.Height/2f+1;
   using(var pen=new Pen(color,1.9f) { StartCap=LineCap.Round,EndCap=LineCap.Round,LineJoin=LineJoin.Round }) {
    using(var body=new GraphicsPath()) {
     body.AddLine(cx-7,cy-5,cx+7,cy-5); body.AddBezier(cx+7,cy-5,cx+7,cy+3,cx+4,cy+5,cx,cy+5); body.AddBezier(cx,cy+5,cx-4,cy+5,cx-7,cy+3,cx-7,cy-5); body.CloseFigure();
     g.DrawPath(pen,body);
    }
    g.DrawArc(pen,cx+5,cy-4,6,6,-80,170);
    g.DrawLine(pen,cx-9,cy+8,cx+9,cy+8);
    if(steam) {
     float phase=Motion.Enabled?(Motion.Now%1600)/1600f:0.5f;
     for(int i=0;i<2;i++) {
      float p=(phase+i*0.5f)%1f, rise=p*5, alpha=(float)Math.Sin(Math.PI*p);
      using(var wisp=new Pen(Color.FromArgb((int)(220*alpha),color),1.6f) { StartCap=LineCap.Round,EndCap=LineCap.Round }) {
       float sx=cx-3+i*6, sy=cy-8-rise;
       g.DrawBezier(wisp,sx,sy,sx-2.2f,sy-2.5f,sx+2.2f,sy-4.5f,sx,sy-7);
      }
     }
    }
   }
  }
 }}
