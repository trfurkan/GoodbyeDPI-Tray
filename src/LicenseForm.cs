using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace GoodbyeTray {
 // Shows the license texts the installer puts in the licenses folder: a list on the left, the selected text on the right.
 sealed class LicenseForm : Form {
  static readonly string[,] Known={
   {"LICENSE-goodbyedpi-tray.txt","GoodbyeDPI Tray (Apache 2.0)"},
   {"NOTICE-goodbyedpi-tray.txt",L.T("GoodbyeDPI Tray bildirimi")},
   {"THIRD_PARTY_NOTICES.md",L.T("Üçüncü taraf bildirimleri")},
   {"LICENSE-goodbyedpi.txt","GoodbyeDPI"},
   {"LICENSE-windivert.txt","WinDivert"},
   {"LICENSE-uthash.txt","uthash"},
   {"LICENSE-getline.txt","getline"}};
  readonly LicenseList list=new LicenseList();
  readonly NoticeView text=new NoticeView();
  readonly List<string> paths=new List<string>();
  public LicenseForm(string folder,bool dark,Color background,Color surface,Color foreground,Color secondary,Color border) {
   Text=L.T("GoodbyeDPI — Lisanslar"); ClientSize=new Size(900,560); MinimumSize=new Size(600,360); StartPosition=FormStartPosition.CenterParent;
   Font=new Font("Segoe UI",10); Icon=Program.AppIcon; BackColor=background; ForeColor=foreground; MaximizeBox=true; MinimizeBox=false; ShowInTaskbar=false;
   var layout=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Padding=new Padding(14),BackColor=background };
   layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,230)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
   list.Dock=DockStyle.Fill; list.Margin=new Padding(0,0,12,0); list.BackColor=background; list.ForeColor=foreground; list.SelectedColor=surface; list.EdgeColor=border;
   // License files are already wrapped at about 80 columns; at this width they fit without wrapping again.
   text.Dock=DockStyle.Fill; text.Margin=Padding.Empty; text.BackColor=surface; text.ForeColor=foreground; text.EdgeColor=border; text.ThumbColor=secondary; text.Font=new Font("Consolas",9.5f);
   text.AccessibleName=L.T("Lisans metni"); text.FocusFrame=false;
   layout.Controls.Add(list,0,0); layout.Controls.Add(text,1,0); Controls.Add(layout);
   if(Directory.Exists(folder)) {
    var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    for(int i=0;i<Known.GetLength(0);i++) { string path=Path.Combine(folder,Known[i,0]); if(File.Exists(path)) { paths.Add(path); list.Items.Add(Known[i,1]); seen.Add(Known[i,0]); } }
    foreach(string path in Directory.GetFiles(folder)) if(!seen.Contains(Path.GetFileName(path))) { paths.Add(path); list.Items.Add(Path.GetFileNameWithoutExtension(path)); }
   }
   string zapret=Path.GetFullPath(Path.Combine(folder,"..","zapret2","licenses"));
   if(Program.ZapretEnabled && Directory.Exists(zapret)) foreach(string path in Directory.GetFiles(zapret)) { paths.Add(path); list.Items.Add("Zapret 2 · "+Path.GetFileNameWithoutExtension(path)); }
   list.SelectedIndexChanged+=delegate { ShowSelected(); };
   if(paths.Count>0) list.SelectedIndex=0;
   else { list.Visible=false; layout.ColumnStyles[0].Width=0; text.Font=Font; text.Text=L.T("Lisans dosyaları bulunamadı.\n\nUygulamayı kurucuyla kurduğunda burada görünür. Lisansları GitHub'daki THIRD_PARTY_NOTICES.md dosyasında da okuyabilirsin."); }
   HandleCreated+=delegate { WindowsIntegration.ThemeCaption(this,dark,background,foreground); };
  }
  void ShowSelected() {
   int i=list.SelectedIndex; if(i<0) return;
   try { text.Text=File.ReadAllText(paths[i]).Replace("\r\n","\n"); text.ScrollToTop(); }
   catch(Exception ex) { text.Text=L.T("Dosya okunamadı: ")+ex.Message; AppInfo.Log(ex.ToString()); }
  }
 }
 // A plain list drawn like the rest of the app: the selected row gets the surface color and a thin border.
 sealed class LicenseList : Control {
  internal readonly List<string> Items=new List<string>();
  internal Color SelectedColor,EdgeColor;
  internal event EventHandler SelectedIndexChanged;
  int selected=-1,first;
  const int RowHeight=34;
  public LicenseList() { SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw|ControlStyles.Selectable,true); TabStop=true; AccessibleRole=AccessibleRole.List; }
  internal int SelectedIndex {
   get { return selected; }
   set { int next=Math.Max(-1,Math.Min(Items.Count-1,value)); if(next==selected) return; selected=next; int visible=Math.Max(1,Height/RowHeight); if(selected<first) first=Math.Max(0,selected); if(selected>=first+visible) first=selected-visible+1; Invalidate(); if(SelectedIndexChanged!=null) SelectedIndexChanged(this,EventArgs.Empty); }
  }
  protected override void OnPaint(PaintEventArgs e) {
   e.Graphics.Clear(BackColor);
   for(int i=first;i<Items.Count;i++) {
    var row=new Rectangle(0,(i-first)*RowHeight,Width-1,RowHeight-4);
    if(i==selected) {
     e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
     using(var path=SoftButton.Round(row,8)) { using(var brush=new SolidBrush(SelectedColor)) e.Graphics.FillPath(brush,path); using(var pen=new Pen(EdgeColor)) e.Graphics.DrawPath(pen,path); }
    }
    TextRenderer.DrawText(e.Graphics,Items[i],Font,new Rectangle(row.X+12,row.Y,row.Width-16,row.Height),ForeColor,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
   }
   if(Focused && selected>=first) ControlPaint.DrawFocusRectangle(e.Graphics,new Rectangle(3,(selected-first)*RowHeight+3,Width-7,RowHeight-10),ForeColor,SelectedColor);
  }
  protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); Focus(); int i=first+e.Y/RowHeight; if(i<Items.Count) SelectedIndex=i; }
  protected override void OnMouseWheel(MouseEventArgs e) { base.OnMouseWheel(e); first=Math.Max(0,Math.Min(Math.Max(0,Items.Count-Math.Max(1,Height/RowHeight)),first-Math.Sign(e.Delta)*3)); Invalidate(); }
  protected override bool IsInputKey(Keys keyData) { return keyData==Keys.Up || keyData==Keys.Down || base.IsInputKey(keyData); }
  protected override void OnKeyDown(KeyEventArgs e) { base.OnKeyDown(e); if(e.KeyCode==Keys.Down) SelectedIndex=selected+1; else if(e.KeyCode==Keys.Up) SelectedIndex=Math.Max(0,selected-1); else if(e.KeyCode==Keys.Home) SelectedIndex=0; else if(e.KeyCode==Keys.End) SelectedIndex=Items.Count-1; }
  protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
  protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
 }
}
