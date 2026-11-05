# Draws the app icon (a shield with a path running through it) into assets\app.ico.
# Every size is drawn from the same 64-unit shape; small sizes get thicker strokes so they stay readable.
# Usage: powershell -NoProfile -ExecutionPolicy Bypass -File scripts\make-icon.ps1 [-Preview <folder>]
param([string]$Preview)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = Split-Path $PSScriptRoot -Parent
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
public static class AppIconArt {
 static readonly Color Shield=Color.FromArgb(42,99,232), Edge=Color.FromArgb(28,72,190), Road=Color.White, Arrow=Color.FromArgb(159,225,203);
 public static Bitmap Draw(int size) {
  var bitmap=new Bitmap(size,size,System.Drawing.Imaging.PixelFormat.Format32bppArgb);
  using(var g=Graphics.FromImage(bitmap)) {
   g.SmoothingMode=SmoothingMode.AntiAlias; g.PixelOffsetMode=PixelOffsetMode.HighQuality; g.Clear(Color.Transparent);
   // The shield is drawn about 10% larger than its 64-unit grid so it fills the icon like other Windows icons.
   float s=size/64f; g.ScaleTransform(s,s); g.TranslateTransform(32,32); g.ScaleTransform(1.1f,1.1f); g.TranslateTransform(-32,-32.5f);
   using(var shield=new GraphicsPath()) {
    shield.AddLine(32,4,56,12); shield.AddLine(56,12,56,30);
    shield.AddBezier(56,30,56,45,46,55,32,60); shield.AddBezier(32,60,18,55,8,45,8,30);
    shield.AddLine(8,30,8,12); shield.CloseFigure();
    using(var fill=new SolidBrush(Shield)) g.FillPath(fill,shield);
    if(size>=32) using(var edge=new Pen(Edge,1.2f)) g.DrawPath(edge,shield);
   }
   // Strokes are at least about 1.7 device pixels wide.
   float width=Math.Max(4.5f,1.7f/s), bar=size<=20?0:1;
   using(var road=new Pen(Road,width) { StartCap=LineCap.Round,EndCap=LineCap.Round })
   using(var arrow=new Pen(Arrow,width) { StartCap=LineCap.Round,EndCap=LineCap.Round,LineJoin=LineJoin.Round }) {
    if(bar>0) { g.DrawLine(road,14,33,26,33); g.DrawLine(road,38,33,50,33); g.DrawLines(arrow,new[]{new PointF(30,24),new PointF(37,33),new PointF(30,42)}); }
    // At tray sizes the two road halves would blur into one line; a larger arrow reads better there.
    else { g.DrawLine(road,15,33,30,33); g.DrawLines(arrow,new[]{new PointF(29,20),new PointF(42,33),new PointF(29,46)}); }
   }
  }
  return bitmap;
 }
 // Classic DIB entries (not PNG) for every size: GDI+ and old shell code read them reliably.
 public static void Save(string path,int[] sizes) {
  var images=new byte[sizes.Length][];
  for(int i=0;i<sizes.Length;i++) using(var bitmap=Draw(sizes[i])) images[i]=Dib(bitmap);
  using(var stream=File.Create(path)) using(var writer=new BinaryWriter(stream)) {
   writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)sizes.Length);
   int offset=6+16*sizes.Length;
   for(int i=0;i<sizes.Length;i++) {
    writer.Write((byte)(sizes[i]>=256?0:sizes[i])); writer.Write((byte)(sizes[i]>=256?0:sizes[i])); writer.Write((byte)0); writer.Write((byte)0);
    writer.Write((ushort)1); writer.Write((ushort)32); writer.Write(images[i].Length); writer.Write(offset); offset+=images[i].Length;
   }
   foreach(var image in images) writer.Write(image);
  }
 }
 static byte[] Dib(Bitmap bitmap) {
  int w=bitmap.Width,h=bitmap.Height,mask=((w+31)/32)*4*h;
  using(var stream=new MemoryStream()) using(var writer=new BinaryWriter(stream)) {
   writer.Write(40); writer.Write(w); writer.Write(h*2); writer.Write((ushort)1); writer.Write((ushort)32); writer.Write(0); writer.Write(w*h*4+mask); writer.Write(0); writer.Write(0); writer.Write(0); writer.Write(0);
   for(int y=h-1;y>=0;y--) for(int x=0;x<w;x++) { var c=bitmap.GetPixel(x,y); writer.Write(c.B); writer.Write(c.G); writer.Write(c.R); writer.Write(c.A); }
   writer.Write(new byte[mask]);
   return stream.ToArray();
  }
 }
}
'@
$sizes = @(16,20,24,32,48,64,128,256)
[AppIconArt]::Save((Join-Path $root 'assets\app.ico'), [int[]]$sizes)
# The website uses the same art: a small favicon, a 96 px PNG for browsers and the 128 px logo on the page.
[AppIconArt]::Save((Join-Path $root 'docs\favicon.ico'), [int[]]@(16,32,48))
foreach($site in @(@(96,'docs\favicon-96.png'),@(128,'docs\images\icon-128.png'))) { $b=[AppIconArt]::Draw($site[0]); $b.Save((Join-Path $root $site[1]),[Drawing.Imaging.ImageFormat]::Png); $b.Dispose() }
if($Preview) { New-Item -ItemType Directory -Path $Preview -Force | Out-Null; foreach($size in 16,24,32,48,256) { $b=[AppIconArt]::Draw($size); $b.Save((Join-Path $Preview "icon-$size.png")); $b.Dispose() } }
Write-Output "Simge yazıldı: $(Join-Path $root 'assets\app.ico')"
