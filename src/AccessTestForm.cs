using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Net;
using System.Net.Cache;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GoodbyeTray {
 // A real HTTPS handshake is the only reliable signal: DPI drops the TLS session by SNI, while ping to the same IP usually succeeds.
 static class AccessProbe {
  internal sealed class Result { internal bool Reached; internal string Status,Detail; }
  internal static readonly string[] Domains={"wattpad.com","pastebin.com","roblox.com","discord.com","pornhub.com","xvideos.com","wikipedia.org","google.com"};
  // Shown instead of the domain where a site needs a softer name (the two adult sites).
  internal static readonly string[] Labels={null,null,null,null,"P*rnh*b.com · +18","XV*d*os.com · +18",null,null};
  internal static readonly bool[] Blocked={true,true,true,true,true,true,false,false};
  internal static readonly string[] Purposes={L.T("Türkiye'de engelli · DPI/SNI engeli kontrolü"),L.T("Türkiye'de engelli · metin paylaşım servisi"),L.T("Türkiye'de engelli · alan adı tabanlı engel kontrolü"),L.T("Türkiye'de engelli · sohbet servisi, SNI engeli kontrolü"),L.T("Türkiye'de engelli · +18, tamamen \"belgesel\" amaçlı"),L.T("Türkiye'de engelli · +18, sadece bilimsel merak"),L.T("Normal site · CDN hatlarının açık olduğunu doğrular"),L.T("Normal site · internet bağlantısının olduğunu doğrular")};
  const int TimeoutMs=8000;
  // csc builds carry no TargetFramework attribute, so the runtime uses legacy defaults where SystemDefault is rejected; pin TLS 1.2 explicitly.
  static AccessProbe() { ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12; ServicePointManager.DnsRefreshTimeout=0; }
  // Answers cached while the engine was off still point at the block page; each test round must resolve afresh.
  internal static void ForgetCachedAnswers() { NativeConsoleSession.FlushDns(); }
  internal static Result Run(string domain) {
   var watch=Stopwatch.StartNew();
   try {
    var request=(HttpWebRequest)WebRequest.Create("https://"+domain+"/");
    request.Method="HEAD"; request.KeepAlive=false; request.AllowAutoRedirect=false; request.Timeout=TimeoutMs; request.ReadWriteTimeout=TimeoutMs;
    request.UserAgent="Mozilla/5.0 (Windows NT 10.0; Win64; x64) GoodbyeDPI-Tray/"+AppInfo.Version; request.CachePolicy=new RequestCachePolicy(RequestCacheLevel.NoCacheNoStore);
    using(var response=(HttpWebResponse)request.GetResponse()) return Reached(watch);
   } catch(WebException ex) {
    // Any HTTP answer, even 403, means the TLS session got through; DPI blocks never reach the server.
    var response=ex.Response as HttpWebResponse; if(response!=null) using(response) return Reached(watch);
    if(ex.Status==WebExceptionStatus.Timeout) return Failed(L.T("Zaman Aşımı"),TimeoutMs/1000+L.T(" saniyede cevap gelmedi"));
    if(ex.Status==WebExceptionStatus.NameResolutionFailure) return Failed(L.T("Erişim Engeli"),L.T("Adres çözülemedi"));
    if(ex.Status==WebExceptionStatus.TrustFailure) return Failed(L.T("Erişim Engeli"),L.T("DNS engeli"));
    if(ex.Status==WebExceptionStatus.ConnectFailure) return Failed(L.T("Erişim Engeli"),L.T("Sunucuya bağlanılamadı"));
    return Failed(L.T("Erişim Engeli"),L.T("Bağlantı kesildi"));
   } catch(Exception ex) { AppInfo.Log("Erişim testi "+domain+": "+ex); return Failed("Test Edilemedi",ex.Message); }
  }
  static Result Reached(Stopwatch watch) { return new Result { Reached=true,Status=L.T("Erişilebilir"),Detail=watch.ElapsedMilliseconds+" ms" }; }
  static Result Failed(string status,string detail) { return new Result { Status=status,Detail=detail }; }
 }
 sealed class AccessRow : Control {
  internal enum Phase { Idle,Testing,Reached,Failed }
  internal readonly string Domain;
  internal readonly SoftButton Retry=new SoftButton { Glyph="restart",Width=42,Height=34 };
  internal Phase State;
  internal string Status=L.T("Test Yapılmadı"),Detail="";
  internal float Angle;
  internal Color Chip,Edge,Ink,Muted,Good,Bad,Spin;
  internal readonly string Label;
  internal AccessRow(string domain,string label=null) {
   Label=label;
   Domain=domain; SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
   Height=48; Margin=Padding.Empty; Retry.AccessibleName=(label ?? domain)+L.T(" için yeniden test et"); Controls.Add(Retry);
  }
  string Title { get { return Label ?? Char.ToUpperInvariant(Domain[0])+Domain.Substring(1); } }
  protected override void OnLayout(LayoutEventArgs e) { base.OnLayout(e); Retry.Location=new Point(Width-Retry.Width-2,(Height-Retry.Height)/2); }
  protected override void OnPaint(PaintEventArgs e) {
   var g=e.Graphics; g.Clear(BackColor); g.SmoothingMode=SmoothingMode.AntiAlias;
   var chip=new Rectangle(2,8,176,Height-17);
   using(var path=SoftButton.Round(chip,12)) using(var fill=new SolidBrush(Chip)) using(var pen=new Pen(Edge)) { g.FillPath(fill,path); g.DrawPath(pen,path); }
   TextRenderer.DrawText(g,Title,Font,new Rectangle(chip.X+12,chip.Y,chip.Width-16,chip.Height),Ink,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
   int y=Height/2; Color tone=State==Phase.Reached?Good:State==Phase.Failed?Bad:State==Phase.Testing?Spin:Muted;
   if(State==Phase.Testing) using(var pen=new Pen(tone,2.2f)) { pen.StartCap=pen.EndCap=LineCap.Round; g.DrawArc(pen,197,y-7,14,14,Angle,270); }
   else using(var dot=new SolidBrush(tone)) g.FillEllipse(dot,199,y-5,11,11);
   using(var bold=new Font(Font,State==Phase.Idle?FontStyle.Regular:FontStyle.Bold)) TextRenderer.DrawText(g,Status,bold,new Rectangle(222,0,170,Height),State==Phase.Idle?Muted:tone,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
   int detailWidth=Math.Max(1,Width-400-Retry.Width-10);
   if(Detail.Length>0) TextRenderer.DrawText(g,"("+Detail+")",Font,new Rectangle(396,0,detailWidth,Height),Muted,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
  }
 }
 sealed class AccessTestForm : Form {
  readonly AccessRow[] rows=new AccessRow[AccessProbe.Domains.Length];
  readonly SoftButton testAll=new SoftButton { Text=L.T("Tümünü Test Et"),Width=160,Height=38 };
  readonly SoftPanel card=new SoftPanel { Dock=DockStyle.Fill,Padding=new Padding(16,10,12,10) };
  readonly Panel heading=new Panel { Dock=DockStyle.Fill };
  readonly Label state=new Label { Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,AutoEllipsis=true };
  readonly ToolTip tips=new ToolTip();
  readonly System.Windows.Forms.Timer spinner=new System.Windows.Forms.Timer { Interval=40 };
  readonly Func<bool> engineRunning;
  int pending; bool dark,disposed;
  internal AccessTestForm(bool darkTheme,Func<bool> running) {
   engineRunning=running; Text=L.T("GoodbyeDPI — Erişim Testi"); Icon=Program.AppIcon; Font=new Font("Segoe UI",10); AutoScaleMode=AutoScaleMode.Dpi;
   ClientSize=new Size(720,548); MinimumSize=new Size(640,584); StartPosition=FormStartPosition.CenterParent;
   var layout=new TableLayoutPanel { Dock=DockStyle.Fill,Padding=new Padding(16),ColumnCount=1,RowCount=3 };
   layout.RowStyles.Add(new RowStyle(SizeType.Absolute,62)); layout.RowStyles.Add(new RowStyle(SizeType.Percent,100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute,44)); Controls.Add(layout);
   var top=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty };
   top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,168)); top.RowStyles.Add(new RowStyle(SizeType.Percent,100));
   heading.Paint+=PaintHeading; top.Controls.Add(heading,0,0);
   testAll.Dock=DockStyle.Top; testAll.Margin=new Padding(8,8,0,0); testAll.Click+=delegate { TestAll(); }; top.Controls.Add(testAll,1,0); layout.Controls.Add(top);
   var list=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=1,RowCount=rows.Length+1,Margin=Padding.Empty,BackColor=Color.Transparent };
   for(int i=0;i<rows.Length;i++) {
    int index=i; rows[i]=new AccessRow(AccessProbe.Domains[i],AccessProbe.Labels[i]) { Dock=DockStyle.Fill };
    rows[i].Retry.Click+=delegate { Test(index); }; tips.SetToolTip(rows[i],AccessProbe.Purposes[i]); tips.SetToolTip(rows[i].Retry,L.T("Bu siteyi yeniden test et"));
    list.RowStyles.Add(new RowStyle(SizeType.Absolute,48)); list.Controls.Add(rows[i],0,i);
   }
   list.RowStyles.Add(new RowStyle(SizeType.Percent,100));
   card.Controls.Add(list); layout.Controls.Add(card);
   state.Text=L.T("Durum: Testi başlatmak için Tümünü Test Et'e ya da bir sitenin yanındaki yenile simgesine bas."); layout.Controls.Add(state);
   spinner.Tick+=delegate { foreach(var row in rows) if(row.State==AccessRow.Phase.Testing) { row.Angle=(row.Angle+18)%360; row.Invalidate(); } };
   ApplyPalette(darkTheme);
  }
  void PaintHeading(object sender,PaintEventArgs e) {
   var g=e.Graphics; g.SmoothingMode=SmoothingMode.AntiAlias; Color ink=ForeColor;
   using(var pen=new Pen(dark?AppPalette.Mint:AppPalette.Accent,2f)) { g.DrawEllipse(pen,2,10,26,26); g.DrawEllipse(pen,9,10,12,26); g.DrawLine(pen,3,23,27,23); g.DrawArc(pen,5,4,20,12,30,120); g.DrawArc(pen,5,30,20,12,210,120); }
   using(var title=new Font(Font.FontFamily,13,FontStyle.Bold)) TextRenderer.DrawText(g,L.T("Ağ Tanılama & Erişim Testi"),title,new Point(40,2),ink,TextFormatFlags.NoPrefix);
   TextRenderer.DrawText(g,L.T("Siteler sadece sen test edince denenir; sonuçlar hiçbir yere gönderilmez."),Font,new Point(41,32),AppPalette.Muted(dark),TextFormatFlags.NoPrefix);
  }
  void TestAll() { for(int i=0;i<rows.Length;i++) Test(i); }
  Task flush;
  async void Test(int index) {
   var row=rows[index]; if(row.State==AccessRow.Phase.Testing || disposed) return;
   row.State=AccessRow.Phase.Testing; row.Status=L.T("Test ediliyor…"); row.Detail=""; row.Retry.Enabled=false; row.Invalidate();
   if(pending==0) flush=Task.Run(delegate { AccessProbe.ForgetCachedAnswers(); });
   var ready=flush; pending++; testAll.Enabled=false; spinner.Start(); state.Text=L.T("Durum: Siteler test ediliyor…");
   string domain=row.Domain; var result=await Task.Run(delegate { ready.Wait(); return AccessProbe.Run(domain); });
   if(disposed || IsDisposed) return;
   row.State=result.Reached?AccessRow.Phase.Reached:AccessRow.Phase.Failed; row.Status=result.Status; row.Detail=result.Detail; row.Retry.Enabled=true; row.Invalidate();
   if(--pending==0) { spinner.Stop(); testAll.Enabled=true; Summarize(); }
  }
  void Summarize() {
   int tested=0,reached=0,blocked=0,blockedReached=0,normal=0,normalReached=0;
   for(int i=0;i<rows.Length;i++) {
    if(rows[i].State!=AccessRow.Phase.Reached && rows[i].State!=AccessRow.Phase.Failed) continue;
    bool ok=rows[i].State==AccessRow.Phase.Reached; tested++; if(ok) reached++;
    if(AccessProbe.Blocked[i]) { blocked++; if(ok) blockedReached++; } else { normal++; if(ok) normalReached++; }
   }
   string text=L.T("Durum: ")+DateTime.Now.ToString("HH:mm")+" · "+reached+"/"+tested+L.T(" site açıldı.");
   if(normal>0 && normalReached==0) text+=L.T(" Normal siteler de açılmıyor; internet bağlantını kontrol et.");
   else if(!engineRunning()) text+=L.T(" GoodbyeDPI kapalı, sonuçlar korumasız bağlantıyı gösteriyor.");
   else if(blocked>0 && blockedReached<blocked) text+=L.T(" Açılmayan engelli site varsa başka bir profil dene.");
   else if(blocked>0) text+=L.T(" GoodbyeDPI işini yapıyor.");
   state.Text=text;
  }
  internal void ApplyPalette(bool darkTheme) {
   dark=darkTheme; bool contrast=SystemInformation.HighContrast;
   Color background=contrast?SystemColors.Window:AppPalette.Background(dark), surface=contrast?SystemColors.Window:AppPalette.Surface(dark), text=contrast?SystemColors.WindowText:AppPalette.Text(dark), muted=contrast?SystemColors.WindowText:AppPalette.Muted(dark), edge=contrast?SystemColors.WindowText:AppPalette.Border(dark);
   BackColor=background; ForeColor=text; state.ForeColor=muted; heading.BackColor=background; card.BackColor=background; card.Fill=surface; card.Edge=edge;
   testAll.BackColor=background; testAll.Fill=contrast?text:AppPalette.Accent; testAll.Ink=contrast?background:AppPalette.AccentInk; testAll.Edge=testAll.Fill;
   foreach(var row in rows) {
    row.BackColor=surface; row.Chip=AppPalette.Background(dark); row.Edge=edge; row.Ink=text; row.Muted=muted;
    row.Good=dark?Color.FromArgb(74,222,128):Color.FromArgb(22,135,74); row.Bad=dark?Color.FromArgb(248,113,113):Color.FromArgb(196,52,62); row.Spin=dark?AppPalette.Mint:AppPalette.Accent;
    if(contrast) { row.Chip=surface; row.Good=row.Bad=row.Spin=text; }
    row.Retry.BackColor=surface; row.Retry.Fill=surface; row.Retry.Ink=text; row.Retry.Edge=edge; row.Retry.ForeColor=text; row.Invalidate(); row.Retry.Invalidate();
   }
   heading.Invalidate(); card.Invalidate(); testAll.Invalidate();
   WindowsIntegration.ThemeCaption(this,dark,BackColor,ForeColor);
   if(!IsHandleCreated) HandleCreated+=delegate { WindowsIntegration.ThemeCaption(this,dark,BackColor,ForeColor); };
  }
  protected override void Dispose(bool disposing) { if(disposing && !disposed) { disposed=true; spinner.Dispose(); tips.Dispose(); } base.Dispose(disposing); }
 }
}
