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
  internal static readonly string[] Domains={"wattpad.com","pastebin.com","roblox.com","discord.com","torproject.org","grindr.com","pornhub.com","xvideos.com","wikipedia.org","google.com"};
  // Shown instead of the domain where a site needs a softer name (the two adult sites).
  internal static readonly string[] Labels={null,null,null,null,"Torproject.org","Grindr.com","P*rnh*b.com · +18","XV*d*os.com · +18",null,null};
  internal static readonly bool[] Blocked={true,true,true,true,true,true,true,true,false,false};
  internal static readonly string[] Purposes={L.T("Türkiye'de engelli · DPI/SNI engeli kontrolü"),L.T("Türkiye'de engelli · metin paylaşım servisi"),L.T("Türkiye'de engelli · alan adı tabanlı engel kontrolü"),L.T("Türkiye'de engelli · sohbet servisi, SNI engeli kontrolü"),L.T("Türkiye'de engelli · Tor projesinin sitesi"),L.T("Türkiye'de engelli · tanışma uygulaması"),L.T("Türkiye'de engelli · +18, tamamen \"belgesel\" amaçlı"),L.T("Türkiye'de engelli · +18, sadece bilimsel merak"),L.T("Normal site · CDN hatlarının açık olduğunu doğrular"),L.T("Normal site · internet bağlantısının olduğunu doğrular")};
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
    return Failure(ex.Status);
   } catch(Exception ex) { AppInfo.Log("Erişim testi "+domain+": "+ex); return Failed("Test Edilemedi",ex.Message); }
  }
  static Result Reached(Stopwatch watch) { return new Result { Reached=true,Status=L.T("Erişilebilir"),Detail=watch.ElapsedMilliseconds+" ms" }; }
  internal static Result Failure(WebExceptionStatus status) {
   if(status==WebExceptionStatus.Timeout) return Failed(L.T("Zaman Aşımı"),TimeoutMs/1000+L.T(" saniyede cevap gelmedi"));
   if(status==WebExceptionStatus.NameResolutionFailure || status==WebExceptionStatus.ProxyNameResolutionFailure) return Failed(L.T("DNS hatası"),L.T("Adres çözülemedi"));
   if(status==WebExceptionStatus.TrustFailure) return Failed(L.T("Sertifika hatası"),L.T("Sertifika doğrulanamadı."));
   if(status==WebExceptionStatus.SecureChannelFailure) return Failed(L.T("TLS hatası"),L.T("Güvenli bağlantı kurulamadı."));
   return Failed(L.T("Bağlantı hatası"),status==WebExceptionStatus.ConnectFailure?L.T("Sunucuya bağlanılamadı"):L.T("Bağlantı kesildi"));
  }
  static Result Failed(string status,string detail) { return new Result { Status=status,Detail=detail }; }
 }
 sealed class AccessContext {
  internal string Key,Caption;
  internal bool Filtered;
  internal string[] Hosts;
  internal static AccessContext Capture(int profile,bool running) {
   int dns=DnsChoice.Selected; bool filtered=running && SiteList.Uses(profile) && SiteList.Wanted;
   string[] hosts=SiteList.All;
   return new AccessContext {
    Key=profile+"|"+running+"|"+dns+"|"+SiteList.Wanted+"|"+QuicBlock.Wanted+"|"+String.Join(",",hosts),
    Caption=(profile>=0 && profile<Program.Names.Length?Program.Names[profile]:"—")+" · DNS: "+(running && DnsChoice.Uses(profile)?L.T(DnsChoice.Names[dns]):L.T("Sistem DNS'i")),
    Filtered=filtered,Hosts=hosts
   };
  }
  internal bool Covers(string domain) { if(!Filtered) return true; foreach(string host in Hosts) if(domain.Equals(host,StringComparison.OrdinalIgnoreCase) || domain.EndsWith("."+host,StringComparison.OrdinalIgnoreCase)) return true; return false; }
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
   { var bold=Fonts.Get(Font,State==Phase.Idle?FontStyle.Regular:FontStyle.Bold); TextRenderer.DrawText(g,Status,bold,new Rectangle(222,0,170,Height),State==Phase.Idle?Muted:tone,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis); }
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
  int generation;
  AccessContext context;
  internal Func<AccessContext> Context;
  internal Action SummaryChanged;
  internal string Summary="",SummaryTip="";
  internal void InvalidateResults() {
   generation++; pending=0; spinner.Stop(); testAll.Enabled=true; context=null;
   foreach(var row in rows) { row.State=AccessRow.Phase.Idle; row.Status=L.T("Yeniden test gerekli"); row.Detail=""; row.Retry.Enabled=true; row.Invalidate(); }
   SetSummary(L.T("Ayarlar değişti; yeniden test et."));
  }
  void SetSummary(string text) { SetSummary(text,""); }
  // A short line for the card, the full story (advice, profile, DNS) for its tooltip.
  void SetSummary(string text,string tip) { Summary=L.T("Ayrıntılı test")+" · "+text; SummaryTip=tip.Length>0?L.T("Ayrıntılı test")+" · "+tip:""; state.Text=tip.Length>0?tip.Replace("\n"," · "):Summary; tips.SetToolTip(state,SummaryTip.Length>0?SummaryTip:Summary); if(SummaryChanged!=null) SummaryChanged(); }
  AccessContext CurrentContext() { return Context!=null?Context():AccessContext.Capture(Program.Profile(),engineRunning()); }
  readonly TableLayoutPanel outer=new TableLayoutPanel(), siteList=new TableLayoutPanel();
  // Shown inside a card of the main window instead of as its own window: no frame, tighter rows so every site fits.
  internal void Embed() {
   TopLevel=false; FormBorderStyle=FormBorderStyle.None; Dock=DockStyle.Fill; MinimumSize=Size.Empty;
   outer.Padding=new Padding(4,2,4,4); embedded=true; outer.RowStyles[0].Height=Scale(46); heading.Invalidate();
   // 9: rows follow the screen scale, so text and buttons keep their room at 150-200 %.
   for(int i=0;i<rows.Length;i++) { siteList.RowStyles[i].Height=RowHeight; rows[i].Height=RowHeight; }
   // On a short window the list scrolls with a thin themed bar instead of the Windows scroll bar.
   siteList.RowStyles[rows.Length].Height=0; siteList.RowStyles[rows.Length].SizeType=SizeType.Absolute; siteList.AutoScroll=false;
   card.Controls.Remove(siteList);
   view.Dock=DockStyle.Fill; bar.Dock=DockStyle.Right; bar.Width=Scale(16); card.Controls.Add(view); card.Controls.Add(bar);
   siteList.Dock=DockStyle.None; siteList.Location=Point.Empty; view.Controls.Add(siteList);
   view.Resize+=delegate { Place(); }; bar.Scrolled+=delegate(int to) { target=to; offset=to; Place(); };
   glide.Tick+=delegate { float next=offset+(target-offset)*0.28f; if(Math.Abs(target-next)<0.6f) { next=target; glide.Stop(); } offset=next; Place(); };
   Hook(view);
   Place();
  }
  readonly Panel view=new Panel();
  readonly ThinBar bar=new ThinBar();
  readonly System.Windows.Forms.Timer glide=new System.Windows.Forms.Timer { Interval=15 };
  float offset,target;
  bool embedded;
  int Scale(int pixels) { return (int)Math.Round(pixels*DeviceDpi/96f); }
  int RowHeight { get { return Scale(40); } }
  int Content { get { return rows.Length*RowHeight; } }
  // 9: the list scrolls from the keyboard too (Page Up/Down, Home, End, arrows) while the card has focus.
  protected override bool ProcessCmdKey(ref Message msg,Keys keyData) {
   if(embedded && bar.Visible) {
    int step=keyData==Keys.PageDown?view.ClientSize.Height:keyData==Keys.PageUp?-view.ClientSize.Height:keyData==Keys.Down?RowHeight:keyData==Keys.Up?-RowHeight:0;
    if(keyData==Keys.Home) step=-Content; else if(keyData==Keys.End) step=Content;
    if(step!=0) { target+=step; Place(); if(Motion.Enabled) glide.Start(); else { offset=target; Place(); } return true; }
   }
   return base.ProcessCmdKey(ref msg,keyData);
  }
  void Place() {
   int room=Math.Max(1,view.ClientSize.Height), max=Math.Max(0,Content-room);
   target=Math.Max(0,Math.Min(max,target)); offset=Math.Max(0,Math.Min(max,offset));
   siteList.SetBounds(0,-(int)Math.Round(offset),view.ClientSize.Width,Content);
   bar.Visible=max>0; bar.Content=Content; bar.View=room; bar.Offset=(int)Math.Round(offset); bar.AccessibleDescription=bar.Percent+"%"; bar.Invalidate();
  }
  // The wheel works over every part of the list, and eases to its stop instead of jumping.
  void Hook(Control control) { control.MouseWheel+=delegate(object sender,MouseEventArgs e) { target-=e.Delta/120f*80; Place(); if(Motion.Enabled) glide.Start(); else { offset=target; Place(); } }; foreach(Control child in control.Controls) Hook(child); }
  internal AccessTestForm(bool darkTheme,Func<bool> running) {
   engineRunning=running; Text=L.T("GoodbyeDPI — Erişim Testi"); Icon=Program.AppIcon; Font=new Font("Segoe UI",10); AutoScaleMode=AutoScaleMode.Dpi;
   ClientSize=new Size(720,548); MinimumSize=new Size(640,584); StartPosition=FormStartPosition.CenterParent;
   var layout=outer; layout.Dock=DockStyle.Fill; layout.Padding=new Padding(16); layout.ColumnCount=1; layout.RowCount=3;
   layout.RowStyles.Add(new RowStyle(SizeType.Absolute,62)); layout.RowStyles.Add(new RowStyle(SizeType.Percent,100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute,44)); Controls.Add(layout);
   var top=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty };
   top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,168)); top.RowStyles.Add(new RowStyle(SizeType.Percent,100));
   heading.Paint+=PaintHeading; top.Controls.Add(heading,0,0);
   testAll.Dock=DockStyle.Top; testAll.Margin=new Padding(8,8,0,0); testAll.Click+=delegate { TestAll(); }; top.Controls.Add(testAll,1,0); layout.Controls.Add(top);
   var list=siteList; list.Dock=DockStyle.Fill; list.ColumnCount=1; list.RowCount=rows.Length+1; list.Margin=Padding.Empty; list.BackColor=Color.Transparent;
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
   if(embedded) { TextRenderer.DrawText(g,L.T("Siteler sadece sen test edince denenir; sonuçlar hiçbir yere gönderilmez."),Font,new Rectangle(2,0,heading.Width-4,heading.Height),AppPalette.Muted(dark),TextFormatFlags.NoPrefix|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis); return; }
   using(var pen=new Pen(dark?AppPalette.Mint:AppPalette.Accent,2f)) { g.DrawEllipse(pen,2,10,26,26); g.DrawEllipse(pen,9,10,12,26); g.DrawLine(pen,3,23,27,23); g.DrawArc(pen,5,4,20,12,30,120); g.DrawArc(pen,5,30,20,12,210,120); }
   { var title=Fonts.Get(Font.FontFamily,13,FontStyle.Bold); TextRenderer.DrawText(g,L.T("Ağ Tanılama & Erişim Testi"),title,new Point(40,2),ink,TextFormatFlags.NoPrefix); }
   TextRenderer.DrawText(g,L.T("Siteler sadece sen test edince denenir; sonuçlar hiçbir yere gönderilmez."),Font,new Point(41,32),AppPalette.Muted(dark),TextFormatFlags.NoPrefix);
  }
  void TestAll() { for(int i=0;i<rows.Length;i++) Test(i); }
  Task flush;
  async void Test(int index) {
   var current=CurrentContext();
   if(context!=null && context.Key!=current.Key) InvalidateResults();
   context=current; int revision=generation;
   var row=rows[index]; if(row.State==AccessRow.Phase.Testing || disposed) return;
   row.State=AccessRow.Phase.Testing; row.Status=L.T("Test ediliyor…"); row.Detail=""; row.Retry.Enabled=false; row.Invalidate();
   if(pending==0) flush=Task.Run(delegate { AccessProbe.ForgetCachedAnswers(); });
   var ready=flush; pending++; testAll.Enabled=false; spinner.Start(); SetSummary(L.T("Siteler deneniyor…"));
   string domain=row.Domain; AccessProbe.Result result;
   try { result=await Task.Run(delegate { ready.Wait(); return AccessProbe.Run(domain); }); }
   catch(Exception ex) { result=new AccessProbe.Result { Status=L.T("Bağlantı hatası"),Detail=ex.GetBaseException().Message }; }
   AppInfo.Log("Erisim testi: site="+domain+", erisim="+result.Reached+", durum="+result.Status+", ayrinti="+result.Detail);
   if(disposed || IsDisposed) return;
   if(revision!=generation) return;
   if(current.Key!=CurrentContext().Key) { InvalidateResults(); return; }
   row.State=result.Reached?AccessRow.Phase.Reached:AccessRow.Phase.Failed; row.Status=result.Status; row.Detail=result.Detail; row.Retry.Enabled=true; row.Invalidate();
   if(!current.Covers(domain)) row.Detail=L.T("Liste dışında; GoodbyeDPI uygulanmadı.")+" "+row.Detail;
   tips.SetToolTip(row,row.Status+" · "+row.Detail+" · "+current.Caption);
   if(--pending==0) { spinner.Stop(); testAll.Enabled=true; Summarize(); }
  }
  void Summarize() {
   int tested=0,reached=0,blocked=0,blockedReached=0,normal=0,normalReached=0;
   for(int i=0;i<rows.Length;i++) {
    if(rows[i].State!=AccessRow.Phase.Reached && rows[i].State!=AccessRow.Phase.Failed) continue;
    bool ok=rows[i].State==AccessRow.Phase.Reached; tested++; if(ok) reached++;
    if(AccessProbe.Blocked[i]) { blocked++; if(ok) blockedReached++; } else { normal++; if(ok) normalReached++; }
   }
   string time=DateTime.Now.ToString("HH:mm"), advice="";
   if(normal>0 && normalReached==0) advice=L.T(" Normal siteler de açılmıyor; internet bağlantını kontrol et.");
   else if(!engineRunning()) advice=L.T(" GoodbyeDPI kapalı, sonuçlar korumasız bağlantıyı gösteriyor.");
   else if(context!=null && context.Filtered) advice=" "+L.T("Site filtresi açık; liste dışı sitelere uygulanmaz.");
   string text=reached+"/"+tested+" "+L.T("açıldı")+" · "+time+(advice.Length>0?" · "+L.T("ayrıntı için üzerine gel"):"");
   string tip=time+" · "+reached+"/"+tested+L.T(" site açıldı.")+advice+(context!=null?"\n"+context.Caption:"");
   SetSummary(text,tip);
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
   view.BackColor=surface; bar.BackColor=surface; bar.Thumb=muted; bar.Invalidate();
   heading.Invalidate(); card.Invalidate(); testAll.Invalidate();
   WindowsIntegration.ThemeCaption(this,dark,BackColor,ForeColor);
   if(!IsHandleCreated) HandleCreated+=delegate { WindowsIntegration.ThemeCaption(this,dark,BackColor,ForeColor); };
  }
  protected override void Dispose(bool disposing) { if(disposing && !disposed) { disposed=true; spinner.Dispose(); tips.Dispose(); glide.Dispose(); } base.Dispose(disposing); }
 }
 // A thin rounded scroll thumb that widens under the mouse; dragging it scrolls the list.
 sealed class ThinBar : Control {
  internal int Content=1,View=1,Offset;
  internal Color Thumb=Color.Gray;
  internal event Action<int> Scrolled;
  bool hover,dragging; int grabY,grabOffset;
  public ThinBar() { SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true); AccessibleRole=AccessibleRole.ScrollBar; AccessibleName=L.T("Site listesini kaydır"); Cursor=Cursors.Hand; }
  // Screen readers get the position as a percentage (in the description).
  internal int Percent { get { return Content<=View?0:(int)Math.Round(100f*Offset/(Content-View)); } }
  Rectangle ThumbRect { get { float scale=Height/(float)Math.Max(1,Content); int h=Math.Max(28,(int)(View*scale)); int y=(int)((Height-h)*(Offset/(float)Math.Max(1,Content-View))); return new Rectangle(0,y,Width,h); } }
  protected override void OnMouseEnter(EventArgs e) { hover=true; Invalidate(); base.OnMouseEnter(e); }
  protected override void OnMouseLeave(EventArgs e) { hover=false; Invalidate(); base.OnMouseLeave(e); }
  protected override void OnMouseDown(MouseEventArgs e) { var r=ThumbRect; if(!r.Contains(e.Location)) { Move(e.Y-r.Height/2,0,0); r=ThumbRect; } dragging=true; grabY=e.Y; grabOffset=r.Y; Capture=true; base.OnMouseDown(e); }
  protected override void OnMouseMove(MouseEventArgs e) { if(dragging) Move(grabOffset+e.Y-grabY,0,0); base.OnMouseMove(e); }
  protected override void OnMouseUp(MouseEventArgs e) { dragging=false; Capture=false; Invalidate(); base.OnMouseUp(e); }
  void Move(int thumbY,int unused,int unused2) {
   int h=ThumbRect.Height, track=Math.Max(1,Height-h);
   int to=(int)(Math.Max(0,Math.Min(track,thumbY))/(float)track*Math.Max(0,Content-View));
   if(Scrolled!=null) Scrolled(to);
  }
  protected override void OnPaint(PaintEventArgs e) {
   var g=e.Graphics; g.Clear(BackColor); g.SmoothingMode=SmoothingMode.AntiAlias;
   var r=ThumbRect; int w=hover || dragging?10:6; var thumb=new Rectangle(Width-w-2,r.Y+2,w,Math.Max(8,r.Height-4));
   using(var path=SoftButton.Round(thumb,w)) using(var b=new SolidBrush(Color.FromArgb(hover || dragging?200:130,Thumb))) g.FillPath(b,path);
  }
 }}
