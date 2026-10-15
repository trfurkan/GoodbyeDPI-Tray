$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$assetDir = Join-Path $root 'assets'
New-Item -ItemType Directory -Path $assetDir -Force | Out-Null
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Collections.Generic;
public static class AppIconBuilder {
 static byte[] Dib(Bitmap bitmap) {
  using(var stream=new MemoryStream()) using(var writer=new BinaryWriter(stream)) {
   int n=bitmap.Width, maskStride=((n+31)/32)*4;
   writer.Write(40); writer.Write(n); writer.Write(n*2); writer.Write((ushort)1); writer.Write((ushort)32);
   writer.Write(0); writer.Write(n*n*4+maskStride*n); writer.Write(0); writer.Write(0); writer.Write(0); writer.Write(0);
   for(int y=n-1;y>=0;y--) for(int x=0;x<n;x++) { Color c=bitmap.GetPixel(x,y); writer.Write(c.B); writer.Write(c.G); writer.Write(c.R); writer.Write(c.A); }
   for(int y=n-1;y>=0;y--) { var row=new byte[maskStride]; for(int x=0;x<n;x++) if(bitmap.GetPixel(x,y).A==0) row[x/8]|=(byte)(128>>(x%8)); writer.Write(row); }
   return stream.ToArray();
  }
 }
 public static void Build(string output) {
  int[] sizes={16,20,24,32,48,64,128,256}; var frames=new List<byte[]>();
  foreach(int size in sizes) using(var bitmap=new Bitmap(size,size,PixelFormat.Format32bppArgb)) {
   using(var g=Graphics.FromImage(bitmap)) {
    g.SmoothingMode=SmoothingMode.AntiAlias; g.Clear(Color.Transparent); g.ScaleTransform(size/256f,size/256f);
    using(var shield=new GraphicsPath()) {
     shield.AddPolygon(new[]{new Point(128,8),new Point(232,48),new Point(216,164),new Point(180,214),new Point(128,248),new Point(76,214),new Point(40,164),new Point(24,48)});
     using(var fill=new LinearGradientBrush(new Rectangle(20,8,216,240),Color.FromArgb(35,151,255),Color.FromArgb(12,41,184),70)) g.FillPath(fill,shield);
     using(var facet=new SolidBrush(Color.FromArgb(45,255,255,255))) g.FillPolygon(facet,new[]{new Point(128,8),new Point(232,48),new Point(128,116),new Point(24,48)});
    }
    using(var mark=new Pen(Color.White,24)) { mark.LineJoin=LineJoin.Miter; g.DrawLines(mark,new[]{new Point(179,80),new Point(126,57),new Point(76,86),new Point(76,152),new Point(126,184),new Point(174,157),new Point(174,124),new Point(130,124)}); }
   }
   frames.Add(Dib(bitmap));
  }
  using(var writer=new BinaryWriter(File.Create(output))) {
   writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)sizes.Length);
   int offset=6+16*sizes.Length;
   for(int i=0;i<sizes.Length;i++) { writer.Write((byte)(sizes[i]==256?0:sizes[i])); writer.Write((byte)(sizes[i]==256?0:sizes[i])); writer.Write((byte)0); writer.Write((byte)0); writer.Write((ushort)1); writer.Write((ushort)32); writer.Write(frames[i].Length); writer.Write(offset); offset+=frames[i].Length; }
   foreach(var frame in frames) writer.Write(frame);
  }
 }
}
'@
[AppIconBuilder]::Build((Join-Path $assetDir 'app.ico'))
