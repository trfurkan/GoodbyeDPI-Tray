using System;
using System.Drawing;
using System.Windows.Forms;

namespace GoodbyeTray {
 sealed class ProfileSearchForm : Form {
  readonly Label heading=new Label(),description=new Label(),phase=new Label();
  readonly TableLayoutPanel rows=new TableLayoutPanel();
  readonly SoftButton primary=new SoftButton(),secondary=new SoftButton();
  readonly Panel track=new Panel(),fill=new Panel();
  readonly Color surface,ink,muted,edge,accent;
  bool testing,finished,cancelling; int completed;
  internal event Action StartRequested,CancelRequested;
  readonly int[] candidates;
  internal ProfileSearchForm(bool dark) : this(dark,null) {}
  internal ProfileSearchForm(bool dark,int[] profiles) {
   candidates=profiles ?? Program.MotorProfiles(0);
   bool contrast=SystemInformation.HighContrast;
   BackColor=contrast?SystemColors.Window:AppPalette.Background(dark);
   surface=contrast?SystemColors.Window:AppPalette.Surface(dark); ink=contrast?SystemColors.WindowText:AppPalette.Text(dark);
   muted=contrast?ink:AppPalette.Muted(dark); edge=contrast?SystemColors.WindowText:AppPalette.Border(dark); accent=contrast?SystemColors.Highlight:AppPalette.Accent;
   Text=L.T("Uygun profil bul"); Icon=Program.AppIcon; Font=new Font("Segoe UI",10); ForeColor=ink; AutoScaleMode=AutoScaleMode.Dpi;
   ClientSize=new Size(720,680); MinimumSize=new Size(650,650); StartPosition=FormStartPosition.CenterParent; MinimizeBox=false; MaximizeBox=false;
   var layout=new TableLayoutPanel { Dock=DockStyle.Fill,Padding=new Padding(24),ColumnCount=1,RowCount=6 };
   foreach(int h in new[]{46,104,54,14}) layout.RowStyles.Add(new RowStyle(SizeType.Absolute,h));
   layout.RowStyles.Add(new RowStyle(SizeType.Percent,100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute,58)); Controls.Add(layout);
   heading.Text=L.T("Sana uygun profili bulalım"); heading.Font=new Font(Font.FontFamily,20,FontStyle.Bold); heading.Dock=DockStyle.Fill; layout.Controls.Add(heading);
   description.Text=L.T("Seçtiğin motorun profilleriyle Discord, Roblox ve Wattpad erişimini deneyeceğiz. Google ile bağlantını kontrol edeceğiz. Test sonunda eski motorun ve profilin geri yüklenir."); description.Dock=DockStyle.Fill; description.ForeColor=muted; layout.Controls.Add(description);
   phase.Text=L.T("Hazırsan başlayalım. Bu işlem birkaç dakika sürebilir."); phase.Dock=DockStyle.Fill; phase.ForeColor=muted; layout.Controls.Add(phase);
   track.Dock=DockStyle.Top; track.Height=5; track.BackColor=edge; fill.Dock=DockStyle.Left; fill.Width=0; fill.BackColor=accent; track.Controls.Add(fill); track.SizeChanged+=delegate { UpdateBar(); }; layout.Controls.Add(track);
   var card=new SoftPanel { Dock=DockStyle.Fill,Padding=new Padding(14),Margin=new Padding(0,12,0,10),Fill=surface,Edge=edge,BackColor=BackColor };
   rows.Dock=DockStyle.Fill; rows.BackColor=surface; rows.ColumnCount=2; rows.RowCount=candidates.Length+1;
   rows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,65)); rows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,35));
   for(int i=0;i<=candidates.Length;i++) rows.RowStyles.Add(new RowStyle(SizeType.Percent,100f/(candidates.Length+1))); card.Controls.Add(rows); layout.Controls.Add(card); RenderRows(null);
   var actions=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=3,RowCount=1,Margin=Padding.Empty };
   actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,132)); actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,210)); layout.Controls.Add(actions);
   secondary.Text=L.T("Vazgeç"); primary.Text=L.T("Testi başlat");
   foreach(var button in new[]{secondary,primary}) { button.Dock=DockStyle.Fill; button.Margin=new Padding(8,8,0,4); button.BackColor=BackColor; button.Fill=button==primary?accent:surface; button.Ink=button==primary?(contrast?SystemColors.HighlightText:AppPalette.AccentInk):ink; button.Edge=button==primary?accent:edge; }
   actions.Controls.Add(secondary,1,0); actions.Controls.Add(primary,2,0);
   primary.Click+=delegate { if(finished) { DialogResult=DialogResult.OK; Close(); } else if(!testing && StartRequested!=null) StartRequested(); };
   secondary.Click+=delegate { if(testing) CancelTest(); else Close(); };
   AcceptButton=primary; CancelButton=secondary;
   FormClosing+=delegate(object sender,FormClosingEventArgs e) { if(testing && e.CloseReason==CloseReason.UserClosing) { e.Cancel=true; CancelTest(); } };
   HandleCreated+=delegate { WindowsIntegration.ThemeCaption(this,dark,BackColor,ink); };
  }
  void CancelTest() { if(cancelling) return; cancelling=true; secondary.Enabled=false; phase.Text=L.T("Testi bitirip eski ayarlarına dönüyoruz. Biraz sürebilir."); if(CancelRequested!=null) CancelRequested(); }
  void UpdateBar() { fill.Width=track.ClientSize.Width*completed/candidates.Length; }
  internal void BeginTest() { testing=true; heading.Text=L.T("Profilleri deniyoruz"); primary.Visible=false; secondary.Text=L.T("Testi durdur"); }
  internal void SetProgress(int step,int profile) {
   if(IsDisposed || finished || cancelling) return;
   if(step==0) phase.Text=L.T("Testi bitirip eski ayarlarına dönüyoruz. Biraz sürebilir.");
   else { phase.Text=Program.Names[profile]+Environment.NewLine+(step<0?L.T("Siteleri kontrol ediyoruz…"):L.T("Profil değişiyor, motorun hazır olmasını bekliyoruz…")); if(step>0) { completed=step-1; UpdateBar(); } }
  }
  internal void ShowResults(ProfileSearch.Result result,bool cancelled,Exception failure) {
   testing=false; finished=true; completed=result.Scores.Count; UpdateBar(); RenderRows(result);
   heading.Text=failure!=null?L.T("Test tamamlanamadı"):cancelled?L.T("Testi durdurduk"):L.T("Sonuçlar hazır");
   description.Text=failure!=null?failure.Message:L.T("Eski profilin ve çalışma durumun geri yüklendi. Sonuçlar bu testteki erişimi gösterir; her site için garanti değildir.");
   phase.Text=failure!=null?L.T("Profilini ve motorun durumunu kontrol edip yeniden deneyebilirsin."):cancelled?L.T("Tamamlanan denemeleri aşağıda görebilirsin."):result.Profile<0?L.T("Bu turda uygun profil bulamadık. İnternet bağlantını kontrol edip tekrar dene."):L.T("Önerilen profil: ")+Program.Names[result.Profile];
   primary.Text=L.T("Önerilen profili uygula"); primary.Visible=failure==null && !cancelled && result.Profile>=0;
   secondary.Text=L.T("Kapat"); secondary.Enabled=true; AcceptButton=primary.Visible?primary:secondary; secondary.Focus();
  }
  void RenderRows(ProfileSearch.Result result) {
   while(rows.Controls.Count>0) { var old=rows.Controls[0]; rows.Controls.Remove(old); old.Dispose(); }
   AddCell(L.T("Bağlantı profili"),0,0,true); AddCell(L.T("Erişim sonucu"),1,0,true);
   for(int i=0;i<candidates.Length;i++) {
    int score=0; bool tested=result!=null && result.Scores.TryGetValue(candidates[i],out score); bool best=tested && result.Profile==candidates[i];
    AddCell(Program.Names[candidates[i]],0,i+1,best);
    AddCell(!tested?L.T("Henüz denenmedi"):score<0?L.T("İnternet kontrolü başarısız"):score+" / 3"+(best?" · "+L.T("Önerilen"):""),1,i+1,best);
   }
  }
  void AddCell(string text,int column,int row,bool bold) {
   rows.Controls.Add(new Label { Text=text,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,AutoEllipsis=true,ForeColor=bold?ink:muted,Font=new Font(Font,bold?FontStyle.Bold:FontStyle.Regular),Margin=new Padding(4,0,4,0) },column,row);
  }
 }
}
