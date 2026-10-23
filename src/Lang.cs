using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using Microsoft.Win32;

namespace GoodbyeTray {
 // Interface language. Turkish text in the code is the key; Table holds its English and Russian forms.
 // A missing entry falls back to Turkish, and tests check that every L.T text has one.
 // Turkish uses friendly UI and notification copy (LangKanka.cs).
 static partial class L {
  internal static readonly string[] Codes={"tr","en","ru"};
  internal static readonly string[] Names={"Türkçe","English","Русский"};
  const string PreferencesKey=@"Software\GoodbyeDPITray";
  static string current;
  internal static string Current { get { if(current==null) current=Stored() ?? FromWindows(); return current; } set { current=Array.IndexOf(Codes,value)>=0?value:"tr"; } }
  // The saved choice, or null when the user never picked one.
  internal static string Stored() {
   try { using(var key=Registry.CurrentUser.OpenSubKey(PreferencesKey)) { string saved=key==null?null:key.GetValue("Language") as string; return saved=="kanka"?"tr":Array.IndexOf(Codes,saved)>=0?saved:null; } }
   catch { return null; }
  }
  // First run follows the Windows display language; anything other than Turkish or Russian gets English.
  static string FromWindows() { string code=CultureInfo.CurrentUICulture.TwoLetterISOLanguageName; return code=="tr"?"tr":code=="ru"?"ru":"en"; }
  internal static void Save(string code) { Current=code; using(var key=Registry.CurrentUser.CreateSubKey(PreferencesKey)) key.SetValue("Language",Current); }
  internal static string T(string turkish) {
   if(turkish==null) return null;
   if(Current=="tr") { string friendly; return Kanka.TryGetValue(turkish,out friendly)?friendly:turkish; }
   string[] forms; return Table.TryGetValue(turkish,out forms)?forms[Current=="en"?0:1]:turkish;
  }
  internal static bool Has(string turkish) { return Table.ContainsKey(turkish); }
  // Round flags for the language picker: Turkey, United Kingdom and Russia.
  internal static Bitmap Flag(string code) {
   var bitmap=new Bitmap(20,20);
   using(var g=Graphics.FromImage(bitmap)) using(var circle=new GraphicsPath()) {
    g.SmoothingMode=SmoothingMode.AntiAlias; circle.AddEllipse(1,1,17,17); g.SetClip(circle);
    if(code=="tr") {
     g.Clear(Color.FromArgb(0xE3,0x0A,0x17));
     using(var white=new SolidBrush(Color.White)) g.FillEllipse(white,3.2f,5f,9.6f,9.6f);
     using(var red=new SolidBrush(Color.FromArgb(0xE3,0x0A,0x17))) g.FillEllipse(red,5.2f,6f,7.8f,7.8f);
     var star=new PointF[10];
     for(int i=0;i<10;i++) { double angle=Math.PI/2+i*Math.PI/5; float r=i%2==0?2.9f:1.2f; star[i]=new PointF(13.6f-(float)(Math.Cos(angle)*r),9.9f-(float)(Math.Sin(angle)*r)); }
     using(var white=new SolidBrush(Color.White)) g.FillPolygon(white,star);
    } else if(code=="ru") {
     using(var white=new SolidBrush(Color.White)) g.FillRectangle(white,0,0,20,7);
     using(var blue=new SolidBrush(Color.FromArgb(0x00,0x39,0xA6))) g.FillRectangle(blue,0,7,20,6);
     using(var red=new SolidBrush(Color.FromArgb(0xD5,0x2B,0x1E))) g.FillRectangle(red,0,13,20,7);
    } else {
     g.Clear(Color.FromArgb(0x01,0x21,0x69));
     using(var white=new Pen(Color.White,4f)) { g.DrawLine(white,0,0,20,20); g.DrawLine(white,20,0,0,20); }
     using(var red=new Pen(Color.FromArgb(0xC8,0x10,0x2E),1.4f)) { g.DrawLine(red,0,0,20,20); g.DrawLine(red,20,0,0,20); }
     using(var white=new SolidBrush(Color.White)) { g.FillRectangle(white,7.5f,0,5,20); g.FillRectangle(white,0,7.5f,20,5); }
     using(var red=new SolidBrush(Color.FromArgb(0xC8,0x10,0x2E))) { g.FillRectangle(red,8.5f,0,3,20); g.FillRectangle(red,0,8.5f,20,3); }
    }
    g.ResetClip();
    using(var edge=new Pen(Color.FromArgb(120,128,128,140))) g.DrawEllipse(edge,1,1,17,17);
   }
   return bitmap;
  }
 }
}
