using System;
using System.Collections.Generic;
using System.Threading;
namespace GoodbyeTray {
 sealed class AppCommands : IDisposable {
  readonly Dictionary<string,EventWaitHandle> events=new Dictionary<string,EventWaitHandle>();
  internal static bool IsValid(string command) { return Array.IndexOf(WindowsIntegration.Actions,command)>=0; }
  static string Name(string command) { return @"Local\GoodbyeDPI.Tray.Command."+command; }
  internal AppCommands() { foreach(string command in WindowsIntegration.Actions) events.Add(command,new EventWaitHandle(false,EventResetMode.AutoReset,Name(command))); }
  internal static bool Send(string command) {
   if(!IsValid(command)) return false;
   // The first instance may still be creating its form.
   for(int i=0;i<20;i++) { try { using(var handle=EventWaitHandle.OpenExisting(Name(command))) return handle.Set(); } catch(WaitHandleCannotBeOpenedException) { Thread.Sleep(50); } }
   return false;
  }
  internal string Poll() { foreach(var item in events) if(item.Value.WaitOne(0)) return item.Key; return null; }
  public void Dispose() { foreach(var handle in events.Values) handle.Dispose(); events.Clear(); }
 }
}
