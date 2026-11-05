using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GoodbyeTray {
 // Shows the sites "only blocked sites" covers and lets the user add or remove their own.
 // The window never writes the list: each change goes to the service through Edit, which checks the name again.
 sealed class SiteListForm : Form {
  readonly FlowLayoutPanel defaults=new FlowLayoutPanel(), custom=new FlowLayoutPanel();
  readonly TextBox input=new TextBox();
  readonly SoftButton add=new SoftButton(), close=new SoftButton();
  readonly Label message=new Label(), customTitle=new Label();
  readonly Color surface,ink,muted,edge,accent,background;
  readonly bool contrast;
  bool working;
  // Runs a change in the service; true when it was applied. Replaceable so tests never touch the service.
  internal Func<bool,string,bool> Edit=delegate(bool adding,string host) { return UserServiceControl.TryEditSite(adding,host); };
  internal event Action Changed;
  internal SiteListForm(bool dark) {
   contrast=SystemInformation.HighContrast;
   background=contrast?SystemColors.Window:AppPalette.Background(dark); surface=contrast?SystemColors.Window:AppPalette.Surface(dark); ink=contrast?SystemColors.WindowText:AppPalette.Text(dark);
   muted=contrast?ink:AppPalette.Muted(dark); edge=contrast?ink:AppPalette.Border(dark); accent=contrast?SystemColors.Highlight:AppPalette.Accent;
   BackColor=background; ForeColor=ink; Font=new Font("Segoe UI",10); AutoScaleMode=AutoScaleMode.Dpi; Icon=Program.AppIcon;
   Text=L.T("Engelli siteler"); ClientSize=new Size(620,600); MinimumSize=new Size(520,500); StartPosition=FormStartPosition.CenterParent; MinimizeBox=false; MaximizeBox=false;
   var layout=new TableLayoutPanel { Dock=DockStyle.Fill,Padding=new Padding(24),ColumnCount=1,RowCount=8 };
   foreach(int h in new[]{46,48,26}) layout.RowStyles.Add(new RowStyle(SizeType.Absolute,h));
   layout.RowStyles.Add(new RowStyle(SizeType.Percent,45)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute,30)); layout.RowStyles.Add(new RowStyle(SizeType.Percent,55));
   layout.RowStyles.Add(new RowStyle(SizeType.Absolute,74)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute,56)); Controls.Add(layout);
   layout.Controls.Add(new Label { Text=L.T("Engelli siteler"),Dock=DockStyle.Fill,Font=new Font(Font.FontFamily,20,FontStyle.Bold),AutoEllipsis=true });
   layout.Controls.Add(new Label { Text=L.T("Bu ayar açıkken GoodbyeDPI yalnızca bu sitelere karışır. Açılmayan bir site varsa aşağıdan ekleyebilirsin."),Dock=DockStyle.Fill,ForeColor=muted });
   layout.Controls.Add(Heading(L.T("Hazır gelenler")));
   foreach(var flow in new[]{defaults,custom}) { flow.Dock=DockStyle.Fill; flow.AutoScroll=true; flow.WrapContents=true; flow.Padding=new Padding(0,4,0,4); flow.BackColor=background; }
   layout.Controls.Add(defaults);
   customTitle.Dock=DockStyle.Fill; customTitle.Font=new Font(Font.FontFamily,10,FontStyle.Bold); customTitle.TextAlign=ContentAlignment.BottomLeft; layout.Controls.Add(customTitle);
   layout.Controls.Add(custom);
   var entry=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,RowCount=2,Margin=Padding.Empty };
   entry.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); entry.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,120)); entry.RowStyles.Add(new RowStyle(SizeType.Absolute,44)); entry.RowStyles.Add(new RowStyle(SizeType.Percent,100));
   input.Dock=DockStyle.Fill; input.Font=new Font("Segoe UI",11); input.BackColor=surface; input.ForeColor=ink; input.BorderStyle=BorderStyle.FixedSingle; input.Margin=new Padding(0,8,0,0); input.MaxLength=200; input.AccessibleName=L.T("Eklenecek site");
   input.TextChanged+=delegate { if(!working) message.Text=""; };
   entry.Controls.Add(input,0,0);
   add.Text=L.T("Ekle"); add.Dock=DockStyle.Fill; add.Margin=new Padding(10,4,0,2); Style(add,true); add.Click+=delegate { Change(true,input.Text); }; entry.Controls.Add(add,1,0);
   message.Dock=DockStyle.Fill; message.ForeColor=muted; message.Text=L.T("Örnek: site.com. Adresi tarayıcıdan da yapıştırabilirsin."); entry.Controls.Add(message,0,1); entry.SetColumnSpan(message,2);
   layout.Controls.Add(entry);
   var actions=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty };
   actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,120)); layout.Controls.Add(actions);
   close.Text=L.T("Kapat"); close.Dock=DockStyle.Fill; close.Margin=new Padding(8,10,0,4); Style(close,false); close.Click+=delegate { Close(); }; actions.Controls.Add(close,1,0);
   AcceptButton=add; CancelButton=close;
   foreach(string host in SiteList.Hosts) defaults.Controls.Add(Chip(host,false));
   ShowCustom();
   FormClosing+=delegate(object sender,FormClosingEventArgs e) { if(working) e.Cancel=true; };
   HandleCreated+=delegate { WindowsIntegration.ThemeCaption(this,dark,BackColor,ForeColor); };
  }
  Label Heading(string text) { return new Label { Text=text,Dock=DockStyle.Fill,Font=new Font(Font.FontFamily,10,FontStyle.Bold),TextAlign=ContentAlignment.BottomLeft }; }
  void Style(SoftButton button,bool primary) { button.BackColor=background; button.Fill=primary?accent:surface; button.Ink=primary?(contrast?SystemColors.HighlightText:AppPalette.AccentInk):ink; button.Edge=primary?accent:edge; }
  internal void ShowCustom() {
   custom.SuspendLayout();
   while(custom.Controls.Count>0) { var old=custom.Controls[0]; custom.Controls.RemoveAt(0); old.Dispose(); }
   string[] sites=SiteList.Custom;
   customTitle.Text=L.T("Senin eklediklerin")+" ("+sites.Length+"/"+SiteList.MaxCustom+")";
   if(sites.Length==0) custom.Controls.Add(new Label { Text=L.T("Henüz site eklemedin."),AutoSize=true,ForeColor=muted,Margin=new Padding(2,6,0,0) });
   foreach(string host in sites) custom.Controls.Add(Chip(host,true));
   custom.ResumeLayout();
  }
  // A rounded tag; added sites carry a × that removes them.
  Control Chip(string host,bool removable) {
   var chip=new SoftButton { Text=removable?host+"   ×":host,AutoSize=false,Height=32,Margin=new Padding(0,0,8,8),Cursor=removable?Cursors.Hand:Cursors.Default,TabStop=removable };
   chip.Width=TextRenderer.MeasureText(chip.Text,Font).Width+30;
   chip.BackColor=background; chip.Fill=surface; chip.Ink=removable?ink:muted; chip.Edge=edge;
   chip.AccessibleName=removable?L.T("Kaldır: ")+host:host;
   if(removable) chip.Click+=delegate { Change(false,host); };
   return chip;
  }
  async void Change(bool adding,string text) {
   if(working) return;
   string host=SiteList.Normalize(text);
   if(host==null) { message.ForeColor=AppPalette.Danger; message.Text=L.T("Bu bir site adresi gibi görünmüyor. Örnek: site.com"); input.Focus(); return; }
   if(adding && Array.IndexOf(SiteList.All,host)>=0) { message.ForeColor=muted; message.Text=L.T("Bu site zaten listede."); return; }
   working=true; add.Enabled=input.Enabled=false; message.ForeColor=muted; message.Text=L.T("Kaydediliyor; GoodbyeDPI bir saniyeliğine yeniden başlayabilir…");
   try {
    bool applied=await Task.Run(delegate { return Edit(adding,host); });
    if(!applied) throw new InvalidOperationException(L.T("Site listesini değiştirmek için uygulamayı güncel kurucuyla güncelle."));
    message.Text=adding?host+L.T(" eklendi."):host+L.T(" kaldırıldı."); if(adding) input.Text="";
    ShowCustom(); if(Changed!=null) Changed();
   } catch(Exception ex) { AppInfo.Log("Site listesi: "+ex); message.ForeColor=AppPalette.Danger; message.Text=ex.Message; }
   finally { working=false; if(!IsDisposed) { add.Enabled=input.Enabled=true; input.Focus(); } }
  }
 }
}
