using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security;
using Microsoft.Win32;
using Windows.Data.Xml.Dom;
using Windows.Foundation;
using Windows.UI.Notifications;

namespace GoodbyeTray {
 // Windows 10/11 notifications that stay in the notification center. The app is not packaged, so its name and icon
 // are registered under HKCU (AppUserModelId) instead of on a Start menu shortcut. Any failure falls back to the tray balloon.
 static class Toast {
  internal const string AppId="trfurkan.GoodbyeDPITray";
  internal static string RegistryPath { get { return @"Software\Classes\AppUserModelId\"+AppId; } }
  internal static string IconPath { get { return Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(AppInfo.LogPath)),"notification.png"); } }
  static bool registered;

  // Shows a notification; clicked runs on the window's thread. Returns false when Windows notifications are unavailable.
  internal static bool Show(string title,string body,System.Windows.Forms.Control owner,Action clicked) {
   try { ShowCore(title,body,owner,clicked); return true; }
   catch(Exception ex) { AppInfo.Log("Bildirim: "+ex.Message); return false; }
  }
  // Kept apart so a missing Windows Runtime only fails here, where Show catches it.
  [MethodImpl(MethodImplOptions.NoInlining)]
  static void ShowCore(string title,string body,System.Windows.Forms.Control owner,Action clicked) {
   // Registered again if something (an uninstall, a cleanup tool) removed it while the app was open.
   if(!registered || !Registered()) { Register(); registered=true; }
   var xml=new XmlDocument();
   xml.LoadXml("<toast><visual><binding template=\"ToastGeneric\"><text>"+SecurityElement.Escape(title)+"</text><text>"+SecurityElement.Escape(body)+"</text></binding></visual></toast>");
   var toast=new ToastNotification(xml) { ExpirationTime=DateTimeOffset.Now.AddDays(1) };
   if(clicked!=null) {
    // The framework compiler cannot use "+=" on Windows Runtime events, so the accessors are called through the marshaler.
    TypedEventHandler<ToastNotification,object> handler=delegate { try { if(!owner.IsDisposed) owner.BeginInvoke(clicked); } catch(InvalidOperationException) { } };
    var add=typeof(ToastNotification).GetMethod("add_Activated"); var remove=typeof(ToastNotification).GetMethod("remove_Activated");
    WindowsRuntimeMarshal.AddEventHandler<TypedEventHandler<ToastNotification,object>>(h => (EventRegistrationToken)add.Invoke(toast,new object[]{h}),t => remove.Invoke(toast,new object[]{t}),handler);
   }
   ToastNotificationManager.CreateToastNotifier(AppId).Show(toast);
  }
  static bool Registered() { using(var key=Registry.CurrentUser.OpenSubKey(RegistryPath)) return key!=null && File.Exists(IconPath); }
  static void Register() {
   string icon=IconPath; Directory.CreateDirectory(Path.GetDirectoryName(icon));
   using(var source=new Icon(Program.AppIcon,64,64)) using(var bitmap=source.ToBitmap()) bitmap.Save(icon,ImageFormat.Png);
   using(var key=Registry.CurrentUser.CreateSubKey(RegistryPath)) { key.SetValue("DisplayName","GoodbyeDPI Tray"); key.SetValue("IconUri",icon); }
  }
  // Uninstall: the registration, the icon and any notifications still in the notification center.
  internal static void Remove() {
   try { ClearHistory(); } catch(Exception ex) { AppInfo.Log("Bildirim geçmişi: "+ex.Message); }
   Registry.CurrentUser.DeleteSubKeyTree(RegistryPath,false);
   if(File.Exists(IconPath)) File.Delete(IconPath);
  }
  [MethodImpl(MethodImplOptions.NoInlining)]
  static void ClearHistory() { ToastNotificationManager.History.Clear(AppId); }
 }
}
