using System;
using System.Drawing;
using System.Windows.Forms;

namespace GoodbyeTray {
 sealed class ReleaseNotesForm : Form {
  internal ReleaseNotesForm(bool dark,string version,string notes) {
   bool contrast=SystemInformation.HighContrast;
   Color background=contrast?SystemColors.Window:AppPalette.Background(dark),surface=contrast?SystemColors.Window:AppPalette.Surface(dark),ink=contrast?SystemColors.WindowText:AppPalette.Text(dark),muted=contrast?ink:AppPalette.Muted(dark),edge=contrast?ink:AppPalette.Border(dark);
   BackColor=background; ForeColor=ink; Font=new Font("Segoe UI",10); AutoScaleMode=AutoScaleMode.Dpi; Icon=Program.AppIcon;
   Text=L.T("Neler değişti?"); ClientSize=new Size(640,520); MinimumSize=new Size(550,440); StartPosition=FormStartPosition.CenterParent; MinimizeBox=false; MaximizeBox=false;
   var layout=new TableLayoutPanel { Dock=DockStyle.Fill,Padding=new Padding(24),ColumnCount=1,RowCount=4 };
   layout.RowStyles.Add(new RowStyle(SizeType.Absolute,48)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute,52)); layout.RowStyles.Add(new RowStyle(SizeType.Percent,100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute,60)); Controls.Add(layout);
   layout.Controls.Add(new Label { Text=L.T("Yeni sürümde neler var?"),Dock=DockStyle.Fill,Font=new Font(Font.FontFamily,20,FontStyle.Bold),AutoEllipsis=true });
   layout.Controls.Add(new Label { Text="GoodbyeDPI Tray "+version+Environment.NewLine+L.T("GitHub sürüm notlarından kısa bir özet."),Dock=DockStyle.Fill,ForeColor=muted });
   var content=new NoticeView { Dock=DockStyle.Fill,Text=String.IsNullOrWhiteSpace(notes)?L.T("Bu sürüm için henüz yenilik notu paylaşılmamış. Ayrıntıları sürüm sayfasında kontrol edebilirsin."):notes,BackColor=surface,ForeColor=ink,EdgeColor=edge,ThumbColor=muted,AccessibleName=L.T("Sürüm notları") }; layout.Controls.Add(content);
   var actions=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=3,RowCount=1,Margin=Padding.Empty };
   actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,100)); actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,168)); layout.Controls.Add(actions);
   var full=ProjectLinks.Create(L.T("Tüm sürüm notları"),UpdateCheck.ReleaseUrl,contrast?SystemColors.HotTrack:AppPalette.Accent,false); full.Dock=DockStyle.Fill; actions.Controls.Add(full,0,0);
   var close=new SoftButton { Text=L.T("Kapat"),Dock=DockStyle.Fill,Margin=new Padding(8,10,0,4),BackColor=background,Fill=surface,Ink=ink,Edge=edge };
   var download=new SoftButton { Text=L.T("Yeni sürümü indir"),Dock=DockStyle.Fill,Margin=new Padding(8,10,0,4),BackColor=background,Fill=contrast?SystemColors.Highlight:AppPalette.Accent,Ink=contrast?SystemColors.HighlightText:AppPalette.AccentInk,Edge=edge };
   close.Click+=delegate { Close(); }; download.Click+=delegate { DialogResult=DialogResult.OK; Close(); };
   actions.Controls.Add(close,1,0); actions.Controls.Add(download,2,0); bool newer=UpdateCheck.IsNewer(version,AppInfo.Version); download.Visible=newer; actions.ColumnStyles[2].Width=newer?168:0; AcceptButton=newer?download:close; CancelButton=close;
   HandleCreated+=delegate { WindowsIntegration.ThemeCaption(this,dark,BackColor,ForeColor); };
  }
 }
}
