using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace GoodbyeTray {
 // Every release installer is signed with the developer's private key (scripts\sign-release.ps1), which never leaves the
 // developer's computer. The updater opens an installer only when its signature matches this public key, so a stolen
 // GitHub account or a swapped release file cannot push a fake update. The version is part of the signed message, so an
 // older signed installer cannot be passed off as a newer release.
 static class UpdateSignature {
  internal const string PublicKey="<RSAKeyValue><Modulus>qRLOht9PuGW0iN7B8u04yPyWzo2fLptCSbRjxm4gtnYDrqkJmYmEH65z/qsUsvEUuJ3MS0GqjPS6g7qptoe3mCqx0urivpsZEsccWFcD2oF+vvZ33/3YSn9/FcsXDj64ToLxqA1o4nBROsp84Is+aojwVCAVBHX+RWgyBYBPil3qDxpC4Ppxl2brmcJzwHzjJuF6fkAklxi8Rt7fL1wcGLaPoP30mvzTlEra8132d6JKiAQQCTpYHW1M/7aJ69gM01/tJVyPIpM0vnLt+jEc2Ke3omNUFd65YJV1OG18dJpd60lqrV9l/mxYxkvEZh4KVQhxFwv2eBnc7DDito/xKWXPzZNh9QxrvbclV4Z2IzzTSiz86hZV48Krc7j17IcA58pE5ff7DCOs2WwH76rFYT4em8rA8aopHH609ZJIJI60dB7G6c2SK96nYxd8Grkk2fPS5TD+B7RIJOdm3zeHr0KgVmmfhfgrJX6RsjQGjgRhfiqStRBdBccWJlRCk6ip</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";
  internal static byte[] Message(string version,string sha256) { return Encoding.UTF8.GetBytes("GoodbyeDPI-Tray update\n"+version+"\n"+sha256.ToLowerInvariant()); }
  internal static bool Verify(string version,string sha256,string signature) { return Verify(version,sha256,signature,PublicKey); }
  internal static bool Verify(string version,string sha256,string signature,string publicKey) {
   if(String.IsNullOrEmpty(version) || String.IsNullOrEmpty(sha256) || String.IsNullOrEmpty(signature)) return false;
   byte[] bytes;
   try { bytes=Convert.FromBase64String(signature.Trim()); } catch(FormatException) { return false; }
   using(var rsa=new RSACryptoServiceProvider()) {
    rsa.FromXmlString(publicKey);
    return rsa.VerifyData(Message(version,sha256),bytes,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
   }
  }
  // The signature sits next to the installer in the same release: GoodbyeDPI-Installer-x.y.z.exe.sig.
  internal static bool ValidUrl(string url) {
   Uri uri; return Uri.TryCreate(url,UriKind.Absolute,out uri) && uri.Scheme=="https" && uri.Host=="github.com" && uri.IsDefaultPort && String.IsNullOrEmpty(uri.UserInfo) && String.IsNullOrEmpty(uri.Query) && String.IsNullOrEmpty(uri.Fragment) && Regex.IsMatch(uri.AbsolutePath,@"^/trfurkan/GoodbyeDPI-Tray/releases/download/[^/]+/GoodbyeDPI-Installer[^/]*\.exe\.sig$");
  }
  // A signature is a few hundred bytes; anything larger is not one.
  internal static string Download(string url) {
   if(!ValidUrl(url)) throw new UpdateSignatureException();
   ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
   var request=(HttpWebRequest)WebRequest.Create(url); request.UserAgent="GoodbyeDPI-Tray/"+AppInfo.Version; request.Timeout=15000; request.ReadWriteTimeout=15000;
   using(var response=(HttpWebResponse)request.GetResponse()) {
    if(response.ResponseUri.Scheme!="https") throw new UpdateSignatureException();
    using(var input=response.GetResponseStream()) using(var buffer=new MemoryStream()) {
     var chunk=new byte[1024]; int count;
     while((count=input.Read(chunk,0,chunk.Length))>0) { buffer.Write(chunk,0,count); if(buffer.Length>4096) throw new UpdateSignatureException(); }
     return Encoding.ASCII.GetString(buffer.ToArray());
    }
   }
  }
 }
 sealed class UpdateSignatureException : IOException {
  internal UpdateSignatureException() : base(L.T("Güncellemenin imzası doğrulanamadı.")) {}
 }
}
