using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace GoodbyeTray {
 // Pieces every secondary window shares with the main one, so no window falls back to Windows' own look:
 // the heading style, a rounded text field and a message box drawn in the app's theme.
 static class DialogKit {
  // The theme of the window a dialog opens over; without one, the saved theme (the installer and early errors).
  internal static bool IsDark(IWin32Window owner) {
   var form=owner as Control;
   if(form!=null && !form.IsDisposed) return form.BackColor.GetBrightness()<0.5f;
   string theme=AppPalette.SavedTheme(); AppPalette.Variant=AppPalette.VariantOf(theme);
   return theme=="system"?AppPalette.WindowsDark():AppPalette.IsDark(theme);
  }
  // The heading of a secondary window: the same weight and size as the main window's status line, not a poster title.
  internal static Label Title(string text) { return new Label { Text=text,Dock=DockStyle.Fill,Font=Fonts.Get(Fonts.Ui,15,FontStyle.Bold),AutoEllipsis=true,TextAlign=ContentAlignment.MiddleLeft,UseMnemonic=false }; }
  // A scrolling flow of chips without Windows' grey scroll bar: the flow is a little wider than its frame, so the bar
  // falls outside and only the wheel scrolls it.
  internal static Panel HiddenScroll(FlowLayoutPanel flow,Color background) {
   var frame=new Panel { Dock=DockStyle.Fill,BackColor=background,Margin=flow.Margin };
   flow.Dock=DockStyle.None; flow.AutoScroll=true; flow.Location=Point.Empty; frame.Controls.Add(flow);
   frame.Resize+=delegate { flow.Size=new Size(frame.Width+SystemInformation.VerticalScrollBarWidth,frame.Height); };
   return frame;
  }
  internal static Label Section(string text) { return new Label { Text=text,Dock=DockStyle.Fill,Font=Fonts.Get(Fonts.Ui,10,FontStyle.Bold),TextAlign=ContentAlignment.BottomLeft,AutoEllipsis=true,UseMnemonic=false }; }
 }

 // A text box inside a rounded frame like the profile and DNS pickers; the frame takes the accent color while typing.
 sealed class FieldBox : Panel {
  internal readonly TextBox Box=new TextBox { BorderStyle=BorderStyle.None };
  internal Color Fill,Edge,Accent;
  public FieldBox(bool multiline) {
   SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
   // No Windows scroll bar: the wheel and the keyboard still scroll a long text.
   Box.Multiline=multiline; if(multiline) { Box.ScrollBars=ScrollBars.None; Box.AcceptsReturn=true; Box.WordWrap=true; }
   Box.Dock=DockStyle.Fill; Padding=multiline?new Padding(12,10,6,10):new Padding(12,0,12,0); Controls.Add(Box);
   Box.GotFocus+=delegate { Invalidate(); }; Box.LostFocus+=delegate { Invalidate(); };
   Cursor=Cursors.IBeam; Click+=delegate { Box.Focus(); };
  }
  internal void Theme(Color background,Color fill,Color ink,Color edge,Color accent) { BackColor=background; Fill=fill; Edge=edge; Accent=accent; Box.BackColor=fill; Box.ForeColor=ink; Invalidate(); }
  // A single-line box sits in the vertical middle of the frame, whatever height the layout gives it.
  protected override void OnLayout(LayoutEventArgs e) {
   if(!Box.Multiline) { Box.Dock=DockStyle.None; int h=Box.PreferredHeight; Box.SetBounds(Padding.Left,Math.Max(0,(Height-h)/2),Math.Max(1,Width-Padding.Horizontal),h); }
   base.OnLayout(e);
  }
  protected override void OnPaintBackground(PaintEventArgs e) { e.Graphics.Clear(BackColor); }
  protected override void OnPaint(PaintEventArgs e) {
   if(Width<4 || Height<4) return;
   e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
   using(var path=SoftButton.Round(new Rectangle(1,1,Width-3,Height-3),Math.Min(16,Math.Min(Width-3,Height-3)))) using(var brush=new SolidBrush(Fill)) using(var pen=new Pen(Box.Focused?Accent:Edge,Box.Focused?1.6f:1f)) { e.Graphics.FillPath(brush,path); e.Graphics.DrawPath(pen,path); }
  }
 }

 // MessageBox in the app's own look: a round mark, a short title, the message and themed buttons.
 static class AppMessage {
  internal static DialogResult Show(IWin32Window owner,string text,string caption,MessageBoxButtons buttons=MessageBoxButtons.OK,MessageBoxIcon icon=MessageBoxIcon.None,MessageBoxDefaultButton defaultButton=MessageBoxDefaultButton.Button1,string extraText=null,Action extra=null) {
   using(var form=new MessageForm(owner,text,caption,buttons,icon,defaultButton,extraText,extra)) {
    var parent=owner as Form;
    if(parent!=null && !parent.IsDisposed && parent.Visible) return form.ShowDialog(parent);
    form.StartPosition=FormStartPosition.CenterScreen; return form.ShowDialog();
   }
  }
  internal static DialogResult Show(string text,string caption,MessageBoxButtons buttons=MessageBoxButtons.OK,MessageBoxIcon icon=MessageBoxIcon.None) { return Show(null,text,caption,buttons,icon); }
 }

 sealed class MessageForm : Form {
  readonly MessageBoxIcon icon; readonly Color tone;
  internal MessageForm(IWin32Window owner,string text,string caption,MessageBoxButtons buttons,MessageBoxIcon icon,MessageBoxDefaultButton defaultButton,string extraText,Action extra) {
   this.icon=icon; bool dark=DialogKit.IsDark(owner), contrast=SystemInformation.HighContrast;
   Color background=contrast?SystemColors.Window:AppPalette.Background(dark),surface=contrast?SystemColors.Window:AppPalette.Surface(dark),ink=contrast?SystemColors.WindowText:AppPalette.Text(dark),muted=contrast?ink:AppPalette.Muted(dark),edge=contrast?ink:AppPalette.Border(dark),accent=contrast?SystemColors.Highlight:AppPalette.Accent,accentInk=contrast?SystemColors.HighlightText:AppPalette.AccentInk;
   tone=icon==MessageBoxIcon.Error?AppPalette.Danger:icon==MessageBoxIcon.Warning?Color.FromArgb(217,119,6):accent;
   BackColor=background; ForeColor=ink; Font=new Font(Fonts.Ui,10); AutoScaleMode=AutoScaleMode.Dpi; Icon=Program.AppIcon;
   Text=String.IsNullOrEmpty(caption)?"GoodbyeDPI":caption; FormBorderStyle=FormBorderStyle.FixedDialog; MinimizeBox=false; MaximizeBox=false; ShowInTaskbar=owner==null; StartPosition=FormStartPosition.CenterParent;
   bool marked=icon!=MessageBoxIcon.None; int left=marked?84:28, width=500;
   // The first line is the headline when the message has more than one paragraph; otherwise the caption is.
   string title=caption, body=text??"";
   int split=body.IndexOf("\n\n",StringComparison.Ordinal);
   if(split>0 && split<90 && body.IndexOf('\n')==split) { title=body.Substring(0,split).Trim(); body=body.Substring(split+2).Trim(); }
   var titleFont=Fonts.Get(Fonts.Ui,13,FontStyle.Bold);
   int textWidth=width-left-28;
   int titleHeight=TextRenderer.MeasureText(title,titleFont,new Size(textWidth,1000),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix).Height;
   int bodyHeight=String.IsNullOrEmpty(body)?0:TextRenderer.MeasureText(body,Font,new Size(textWidth,2000),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix).Height;
   var heading=new Label { Text=title,Font=titleFont,Location=new Point(left,26),Size=new Size(textWidth,titleHeight+2),UseMnemonic=false };
   var message=new Label { Text=body,ForeColor=muted,Location=new Point(left,26+titleHeight+8),Size=new Size(textWidth,bodyHeight+4),UseMnemonic=false };
   Controls.Add(heading); Controls.Add(message);
   int top=Math.Max(26+titleHeight+8+bodyHeight+24,marked?96:0);
   var footer=new Panel { Location=new Point(0,top),Size=new Size(width,72),BackColor=surface,Anchor=AnchorStyles.Left|AnchorStyles.Right|AnchorStyles.Top };
   footer.Paint+=delegate(object s,PaintEventArgs e) { using(var pen=new Pen(edge)) e.Graphics.DrawLine(pen,0,0,footer.Width,0); };
   Controls.Add(footer);
   string[] labels; DialogResult[] results;
   switch(buttons) {
    case MessageBoxButtons.YesNo: labels=new[]{L.T("Evet"),L.T("Hayır")}; results=new[]{DialogResult.Yes,DialogResult.No}; break;
    case MessageBoxButtons.OKCancel: labels=new[]{L.T("Tamam"),L.T("Vazgeç")}; results=new[]{DialogResult.OK,DialogResult.Cancel}; break;
    case MessageBoxButtons.YesNoCancel: labels=new[]{L.T("Evet"),L.T("Hayır"),L.T("Vazgeç")}; results=new[]{DialogResult.Yes,DialogResult.No,DialogResult.Cancel}; break;
    case MessageBoxButtons.RetryCancel: labels=new[]{L.T("Tekrar dene"),L.T("Vazgeç")}; results=new[]{DialogResult.Retry,DialogResult.Cancel}; break;
    default: labels=new[]{L.T("Tamam")}; results=new[]{DialogResult.OK}; break;
   }
   int primary=defaultButton==MessageBoxDefaultButton.Button2?1:defaultButton==MessageBoxDefaultButton.Button3?2:0; if(primary>=labels.Length) primary=0;
   int x=width-20;
   for(int i=labels.Length-1;i>=0;i--) {
    int w=Math.Max(104,TextRenderer.MeasureText(labels[i],Font).Width+40); x-=w;
    var result=results[i];
    var button=new SoftButton { Text=labels[i],Location=new Point(x,16),Size=new Size(w,40),BackColor=surface,Fill=i==primary?accent:background,Ink=i==primary?accentInk:ink,Edge=i==primary?accent:edge,DialogResult=result };
    button.Click+=delegate { DialogResult=result; Close(); };
    footer.Controls.Add(button); if(i==primary) AcceptButton=button;
    if(result==DialogResult.Cancel || result==DialogResult.No || labels.Length==1) CancelButton=button;
    x-=8;
   }
   // An optional way forward beside the answers, e.g. opening the Services window for a conflicting tool.
   if(extra!=null && !String.IsNullOrEmpty(extraText)) {
    var link=new LinkLabel { Text=extraText,AutoSize=false,Location=new Point(24,16),Size=new Size(Math.Max(60,x-32),40),TextAlign=ContentAlignment.MiddleLeft,LinkBehavior=LinkBehavior.HoverUnderline,BackColor=surface,UseMnemonic=false };
    link.LinkColor=link.ActiveLinkColor=link.VisitedLinkColor=contrast?SystemColors.HotTrack:accent;
    link.LinkClicked+=delegate { try { extra(); } catch(Exception ex) { AppInfo.Log(ex.ToString()); } };
    footer.Controls.Add(link);
   }
   ClientSize=new Size(width,top+72);
   HandleCreated+=delegate { WindowsIntegration.ThemeCaption(this,dark,background,ink); }; Motion.FadeIn(this);
   switch(icon) { case MessageBoxIcon.Error: System.Media.SystemSounds.Hand.Play(); break; case MessageBoxIcon.Warning: System.Media.SystemSounds.Exclamation.Play(); break; }
  }
  // The mark: a tinted disc with a drawn symbol, the same language as the access test's status dots.
  protected override void OnPaint(PaintEventArgs e) {
   base.OnPaint(e);
   if(icon==MessageBoxIcon.None) return;
   var g=e.Graphics; g.SmoothingMode=SmoothingMode.AntiAlias;
   const float s=1f; var disc=new RectangleF(28*s,26*s,40*s,40*s);
   using(var fill=new SolidBrush(Color.FromArgb(38,tone))) g.FillEllipse(fill,disc);
   float cx=disc.X+disc.Width/2, cy=disc.Y+disc.Height/2;
   using(var pen=new Pen(tone,2.4f*s) { StartCap=LineCap.Round,EndCap=LineCap.Round,LineJoin=LineJoin.Round }) using(var dot=new SolidBrush(tone)) {
    if(icon==MessageBoxIcon.Error) { g.DrawLine(pen,cx-6*s,cy-6*s,cx+6*s,cy+6*s); g.DrawLine(pen,cx+6*s,cy-6*s,cx-6*s,cy+6*s); }
    else if(icon==MessageBoxIcon.Warning) { g.DrawLine(pen,cx,cy-8*s,cx,cy+2*s); g.FillEllipse(dot,cx-1.8f*s,cy+5.5f*s,3.6f*s,3.6f*s); }
    else if(icon==MessageBoxIcon.Question) { g.DrawArc(pen,cx-5.5f*s,cy-10*s,11*s,10*s,190,250); g.DrawLine(pen,cx,cy,cx,cy+2.5f*s); g.FillEllipse(dot,cx-1.8f*s,cy+6*s,3.6f*s,3.6f*s); }
    else { g.FillEllipse(dot,cx-1.8f*s,cy-9*s,3.6f*s,3.6f*s); g.DrawLine(pen,cx,cy-2*s,cx,cy+8*s); }
   }
  }
 }
}
