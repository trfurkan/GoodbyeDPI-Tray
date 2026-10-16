using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Win32;

namespace GoodbyeTray {
 // Asks GitHub which release is the latest. Only that request is sent; nothing about the user or the computer.
 static class UpdateCheck {
  internal const string ReleaseUrl="https://github.com/trfurkan/GoodbyeDPI-Tray/releases/latest";
  // Installer file names carry the version (GoodbyeDPI-Installer-1.5.3.exe), so the link comes from the release itself.
  internal static string DownloadUrl=ReleaseUrl;
  const string ApiUrl="https://api.github.com/repos/trfurkan/GoodbyeDPI-Tray/releases/latest";
  const string PreferencesKey=@"Software\GoodbyeDPITray";
  internal static bool Enabled {
   get { try { using(var key=Registry.CurrentUser.OpenSubKey(PreferencesKey)) return key==null || Convert.ToInt32(key.GetValue("CheckUpdates",1))!=0; } catch { return true; } }
   set { using(var key=Registry.CurrentUser.CreateSubKey(PreferencesKey)) key.SetValue("CheckUpdates",value?1:0,RegistryValueKind.DWord); }
  }
  // The version the tray already announced, so each release is announced once.
  internal static string NotifiedVersion {
   get { try { using(var key=Registry.CurrentUser.OpenSubKey(PreferencesKey)) return key==null?null:key.GetValue("NotifiedUpdate") as string; } catch { return null; } }
   set { using(var key=Registry.CurrentUser.CreateSubKey(PreferencesKey)) key.SetValue("NotifiedUpdate",value); }
  }
  // Returns the latest version when it is newer than this one, otherwise null. Throws when GitHub cannot be reached.
  internal static string Fetch() {
   // csc builds run in legacy TLS mode; GitHub needs TLS 1.2.
   ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
   var request=(HttpWebRequest)WebRequest.Create(ApiUrl);
   request.UserAgent="GoodbyeDPI-Tray/"+AppInfo.Version; request.Accept="application/vnd.github+json"; request.Timeout=15000; request.ReadWriteTimeout=15000;
   using(var response=request.GetResponse()) using(var reader=new StreamReader(response.GetResponseStream(),Encoding.UTF8)) {
    string json=reader.ReadToEnd();
    var match=Regex.Match(json,"\"tag_name\"\\s*:\\s*\"v?([0-9]+(?:\\.[0-9]+){1,3})\"");
    if(!match.Success || !IsNewer(match.Groups[1].Value,AppInfo.Version)) return null;
    DownloadUrl=InstallerUrl(json) ?? ReleaseUrl;
    return match.Groups[1].Value;
   }
  }
  // Only an .exe from this repository's releases is accepted; anything else falls back to the release page.
  internal static string InstallerUrl(string json) {
   var match=Regex.Match(json,"\"browser_download_url\"\\s*:\\s*\"(https://github\\.com/trfurkan/GoodbyeDPI-Tray/releases/download/[^\"/]+/GoodbyeDPI-Installer[^\"/]*\\.exe)\"");
   return match.Success?match.Groups[1].Value:null;
  }
  internal static bool IsNewer(string candidate,string current) { Version latest,installed; return Version.TryParse(candidate,out latest) && Version.TryParse(current,out installed) && latest>installed; }
 }
 enum UpdateState { Unknown, Checking, Current, Available, Failed }
 // Round status mark next to the update line in About: check, arrow, exclamation or dots.
 sealed class UpdateStatusIcon : Control {
  UpdateState state;
  internal UpdateState State { get { return state; } set { state=value; Invalidate(); } }
  public UpdateStatusIcon() { SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw|ControlStyles.UserPaint,true); }
  protected override void OnPaint(PaintEventArgs e) {
   var g=e.Graphics; g.SmoothingMode=SmoothingMode.AntiAlias; g.Clear(BackColor);
   Color fill=SystemInformation.HighContrast?SystemColors.Highlight:state==UpdateState.Current?Color.FromArgb(31,153,92):state==UpdateState.Available?AppPalette.Accent:state==UpdateState.Failed?AppPalette.Danger:Color.FromArgb(128,140,160);
   float size=Math.Min(Width,Height)-2;
   using(var brush=new SolidBrush(fill)) g.FillEllipse(brush,1,1,size,size);
   float c=1+size/2, u=size/10;
   using(var pen=new Pen(SystemInformation.HighContrast?SystemColors.HighlightText:Color.White,Math.Max(1.6f,u*1.3f)) { StartCap=LineCap.Round,EndCap=LineCap.Round,LineJoin=LineJoin.Round }) {
    if(state==UpdateState.Current) g.DrawLines(pen,new[]{new PointF(c-2.6f*u,c+0.1f*u),new PointF(c-0.7f*u,c+2f*u),new PointF(c+2.8f*u,c-1.8f*u)});
    else if(state==UpdateState.Available) { g.DrawLine(pen,c,c+2.8f*u,c,c-2.6f*u); g.DrawLines(pen,new[]{new PointF(c-2.2f*u,c-0.5f*u),new PointF(c,c-2.7f*u),new PointF(c+2.2f*u,c-0.5f*u)}); }
    else if(state==UpdateState.Failed) { g.DrawLine(pen,c,c-2.6f*u,c,c+0.6f*u); g.DrawLine(pen,c,c+2.6f*u,c,c+2.7f*u); }
    else for(int i=-1;i<=1;i++) g.DrawLine(pen,c+i*2.4f*u,c,c+i*2.4f*u+0.1f,c);
   }
  }
 }
}
