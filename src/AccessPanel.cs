using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net;
using System.Windows.Forms;

namespace GoodbyeTray {
 // One site in the quick access check on the main window: the site's own icon, its name and a small result badge.
 // While a test runs a ring turns around the icon; afterwards a green check or a red cross sits on its corner.
 sealed class AccessChip : Control {
  internal enum Phase { Idle,Testing,Reached,Failed }
  internal readonly string Domain;
  readonly Color brand;
  Phase phase;
  Image icon;
  internal Color Fill,Edge,Ink,Muted,Good,Bad,Spin;
  internal AccessChip(string domain,Color brandColor) {
   Domain=domain; brand=brandColor; SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw|ControlStyles.SupportsTransparentBackColor,true);
   Text=Char.ToUpperInvariant(domain[0])+domain.Substring(1,domain.IndexOf('.')-1); Height=40; Margin=new Padding(0,0,10,0);
   AccessibleRole=AccessibleRole.StaticText; AccessibleName=Text; icon=SiteIcons.Load(domain);
  }
  internal Phase State { get { return phase; } set { phase=value; Motion.Loop(this,value==Phase.Testing); Invalidate(); } }
  internal Image Icon { set { if(value==null) return; icon=value; Invalidate(); } }
  public override Size GetPreferredSize(Size proposed) { return new Size(TextRenderer.MeasureText(Text,Font).Width+58,40); }
  protected override void OnHandleDestroyed(EventArgs e) { Motion.Loop(this,false); base.OnHandleDestroyed(e); }
  protected override void OnPaint(PaintEventArgs e) {
   var g=e.Graphics; g.Clear(BackColor); g.SmoothingMode=SmoothingMode.AntiAlias; g.InterpolationMode=InterpolationMode.HighQualityBicubic;
   Color tone=phase==Phase.Reached?Good:phase==Phase.Failed?Bad:Spin;
   bool done=phase==Phase.Reached || phase==Phase.Failed;
   using(var path=SoftButton.Round(new Rectangle(1,1,Width-3,Height-3),Height-3)) using(var brush=new SolidBrush(done?Motion.Mix(Fill,tone,0.10f):Fill)) using(var pen=new Pen(done?Motion.Mix(Edge,tone,0.45f):Edge)) { g.FillPath(brush,path); g.DrawPath(pen,path); }
   // The site's icon, or its first letter on the site's color until the icon has been fetched.
   var box=new RectangleF(10,(Height-22)/2f,22,22);
   if(icon!=null) g.DrawImage(icon,box);
   else {
    using(var b=new SolidBrush(brand)) g.FillEllipse(b,box);
    { var font=Fonts.Get("Segoe UI",9.5f,FontStyle.Bold); TextRenderer.DrawText(g,Text.Substring(0,1),font,Rectangle.Round(box),Color.White,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPadding); }
   }
   if(phase==Phase.Testing) using(var pen=new Pen(tone,2f) { StartCap=LineCap.Round,EndCap=LineCap.Round }) g.DrawArc(pen,box.X-3,box.Y-3,box.Width+6,box.Height+6,Motion.Enabled?(Motion.Now%900)/900f*360:0,250);
   if(done) {
    // A small round badge on the icon's lower right corner.
    var badge=new RectangleF(box.Right-8,box.Bottom-8,12,12);
    using(var ring=new SolidBrush(BackColor)) g.FillEllipse(ring,badge.X-1.5f,badge.Y-1.5f,badge.Width+3,badge.Height+3);
    using(var b=new SolidBrush(tone)) g.FillEllipse(b,badge);
    using(var pen=new Pen(Color.White,1.6f) { StartCap=LineCap.Round,EndCap=LineCap.Round }) {
     float cx=badge.X+6, cy=badge.Y+6;
     if(phase==Phase.Reached) g.DrawLines(pen,new[]{new PointF(cx-3,cy),new PointF(cx-1,cy+2),new PointF(cx+3,cy-2.5f)});
     else { g.DrawLine(pen,cx-2.3f,cy-2.3f,cx+2.3f,cy+2.3f); g.DrawLine(pen,cx+2.3f,cy-2.3f,cx-2.3f,cy+2.3f); }
    }
   }
   TextRenderer.DrawText(g,Text,Font,new Rectangle(40,0,Width-46,Height),phase==Phase.Idle?Muted:Ink,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix|TextFormatFlags.EndEllipsis);
  }
 }
 // Site icons for the quick check, taken from each site itself (its /favicon.ico) the first time it opens in a test,
 // then kept in the user's app data. Nothing is asked of any other server.
 static class SiteIcons {
  static string Folder { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"GoodbyeDPI Tray","Icons"); } }
  static string PathFor(string domain) { return Path.Combine(Folder,"site-"+domain+".png"); }
  internal static Image Load(string domain) {
   try { string path=PathFor(domain); if(!File.Exists(path)) return null; using(var stream=new MemoryStream(File.ReadAllBytes(path))) return new Bitmap(Image.FromStream(stream)); } catch(Exception) { return null; }
  }
  // Runs off the UI thread; returns null when the site gives no usable icon.
  // Tries the usual icon locations on the site's own addresses (some keep it under www. or /assets/).
  internal static Image Fetch(string domain) {
   foreach(string url in new[]{"https://"+domain+"/favicon.ico","https://www."+domain+"/favicon.ico","https://"+domain+"/assets/favicon.ico"}) {
    var picture=TryFetch(url);
    if(picture!=null) {
     try { Directory.CreateDirectory(Folder); picture.Save(PathFor(domain),System.Drawing.Imaging.ImageFormat.Png); } catch(Exception ex) { AppInfo.Log("Site simgesi kaydedilemedi: "+ex.Message); }
     return picture;
    }
   }
   AppInfo.Log("Site simgesi bulunamadı: "+domain); return null;
  }
  static Image TryFetch(string url) {
   try {
    var request=(HttpWebRequest)WebRequest.Create(url);
    request.Timeout=request.ReadWriteTimeout=5000; request.UserAgent="Mozilla/5.0 (Windows NT 10.0; Win64; x64) GoodbyeDPI-Tray/"+AppInfo.Version; request.AllowAutoRedirect=false;
    using(var response=(HttpWebResponse)request.GetResponse()) {
     // Only a real image; a page that answers every address with HTML is not an icon.
     if(response.StatusCode!=HttpStatusCode.OK || response.ContentType==null || response.ContentType.IndexOf("image",StringComparison.OrdinalIgnoreCase)<0) return null;
     using(var body=response.GetResponseStream()) using(var data=new MemoryStream()) {
      var buffer=new byte[8192]; int read;
      while((read=body.Read(buffer,0,buffer.Length))>0) { data.Write(buffer,0,read); if(data.Length>256*1024) return null; }
      data.Position=0;
      try { using(var ico=new Icon(data,32,32)) return ico.ToBitmap(); } catch(Exception) { data.Position=0; using(var raw=Image.FromStream(data)) return new Bitmap(raw,32,32); }
     }
    }
   } catch(Exception) { return null; }
  } }
}
