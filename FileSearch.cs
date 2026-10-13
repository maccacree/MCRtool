using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

public class FileRecord {
 public string Name, Path;
 public long Size;
 public DateTime Modified;
}
public class FileSearchForm : Form {
 const int MaxIndex = 250000, MaxVisible = 5000;
 List<FileRecord> index = new List<FileRecord>();
 TextBox root = new TextBox { Width=300 }, filter = new TextBox { Width=250 };
 ListView list = new ListView { Dock=DockStyle.Fill, View=View.Details, FullRowSelect=true, MultiSelect=false };
 Label status = new Label { Dock=DockStyle.Bottom,Height=50 };
 Button start, cancel;
 BackgroundWorker worker = new BackgroundWorker { WorkerSupportsCancellation=true,WorkerReportsProgress=true };
 Timer debounce = new Timer { Interval=200 };
 bool closing, partial;
 int skipped;
 string indexedRoot = "";
 public FileSearchForm() {
  Text="MCRtool — поиск файлов"; Size=new Size(1050,650); MinimumSize=new Size(800,500); Font=new Font("Segoe UI",10);
  var bar=new FlowLayoutPanel {Dock=DockStyle.Top,Height=92,Padding=new Padding(8)};
  bar.Controls.Add(new Label {Text="Папка:",AutoSize=true}); root.Text=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile); bar.Controls.Add(root);
  Button browse=Button(bar,"Выбрать",delegate { using(var d=new FolderBrowserDialog()) if(d.ShowDialog()==DialogResult.OK) root.Text=d.SelectedPath; });
  start=Button(bar,"Построить индекс",Build); cancel=Button(bar,"Остановить",delegate { worker.CancelAsync(); }); cancel.Enabled=false;
  bar.Controls.Add(new Label {Text="Имя содержит:",AutoSize=true}); bar.Controls.Add(filter);
  Button(bar,"Показать в папке",Reveal); Button(bar,"SHA-256",Hash); Button(bar,"Экспорт CSV",Export);
  list.Columns.Add("Имя",220); list.Columns.Add("Размер, байт",110); list.Columns.Add("Изменён",155); list.Columns.Add("Полный путь",500);
  Controls.Add(list); Controls.Add(status); Controls.Add(bar);
  filter.TextChanged+=delegate {debounce.Stop();debounce.Start();}; debounce.Tick+=delegate {debounce.Stop();Render();};
  list.DoubleClick+=delegate { Safe(Reveal); };
  worker.DoWork+=delegate(object sender,DoWorkEventArgs args) {
   string path=(string)args.Argument; var found=new List<FileRecord>(); var dirs=new Stack<string>(); dirs.Push(path); int errors=0; bool limited=false;
   while(dirs.Count>0 && !worker.CancellationPending && !limited) {
    string dir=dirs.Pop();
    try {
     if((File.GetAttributes(dir)&FileAttributes.ReparsePoint)!=0) {errors++;continue;}
     foreach(string file in Directory.EnumerateFiles(dir)) {
      if(worker.CancellationPending) break;
      try { var info=new FileInfo(file); if((info.Attributes&FileAttributes.ReparsePoint)!=0) {errors++;continue;} found.Add(new FileRecord {Name=info.Name,Path=info.FullName,Size=info.Length,Modified=info.LastWriteTime}); } catch(IOException) {errors++;} catch(UnauthorizedAccessException) {errors++;}
      if(found.Count>=MaxIndex) {limited=true;break;}
      if(found.Count%1000==0) worker.ReportProgress(0,found.Count);
     }
     if(!limited && !worker.CancellationPending) foreach(string child in Directory.EnumerateDirectories(dir)) {dirs.Push(child);}
    } catch(IOException) {errors++;} catch(UnauthorizedAccessException) {errors++;}
   }
   args.Result=new object[] {found,errors,limited || worker.CancellationPending,path};
  };
  worker.ProgressChanged+=delegate(object sender,ProgressChangedEventArgs args) { if(!closing) status.Text="Индексирование: "+args.UserState+" файлов. Фильтр использует предыдущий индекс до завершения."; };
  worker.RunWorkerCompleted+=delegate(object sender,RunWorkerCompletedEventArgs args) {
   if(closing) return; start.Enabled=true; cancel.Enabled=false; root.Enabled=true; browse.Enabled=true;
   if(args.Error!=null) {MessageBox.Show(args.Error.Message,"Ошибка поиска");return;}
   object[] result=(object[])args.Result; index=(List<FileRecord>)result[0]; skipped=(int)result[1]; partial=(bool)result[2]; indexedRoot=(string)result[3]; Render();
  };
  FormClosing+=delegate {closing=true;worker.CancelAsync();debounce.Stop();};
  status.Text="Индекс хранится в памяти. Выберите папку и начните обход. Символические ссылки и точки соединения пропускаются.";
 }
 Button Button(FlowLayoutPanel p,string title,Action action) {var b=new Button {Text=title,AutoSize=true};b.Click+=delegate {Safe(action);};p.Controls.Add(b);return b;}
 void Safe(Action action) {try {action();}catch(Exception ex){MessageBox.Show(ex.Message,"MCRtool",MessageBoxButtons.OK,MessageBoxIcon.Error);}}
 void Build() {if(worker.IsBusy)return;string path=System.IO.Path.GetFullPath(root.Text.Trim());if(!Directory.Exists(path))throw new IOException("Папка не найдена.");start.Enabled=false;cancel.Enabled=true;root.Enabled=false;foreach(Control c in start.Parent.Controls) if(c.Text=="Выбрать")c.Enabled=false;status.Text="Индексирование…";worker.RunWorkerAsync(path);}
 bool Matches(FileRecord f) {return f.Name.IndexOf(filter.Text,StringComparison.OrdinalIgnoreCase)>=0;}
 void Render() {
  list.BeginUpdate();list.Items.Clear();int count=0;
  foreach(var f in index) if(Matches(f)) {count++;if(list.Items.Count<MaxVisible){var row=new ListViewItem(new string[] {f.Name,f.Size.ToString(),f.Modified.ToString("yyyy-MM-dd HH:mm:ss"),f.Path});row.Tag=f;list.Items.Add(row);}}
  list.EndUpdate(); if(!worker.IsBusy) status.Text="Индекс: "+index.Count+" | найдено: "+count+" | показано: "+list.Items.Count+" | пропущено/ошибок: "+skipped+(partial ? " | НЕПОЛНЫЙ (остановка или лимит 250 000)" : "")+"\r\n"+indexedRoot+" — изменения на диске появятся после повторного индексирования.";
 }
 FileRecord Selected() {if(list.SelectedItems.Count!=1)throw new InvalidOperationException("Выберите один файл.");return (FileRecord)list.SelectedItems[0].Tag;}
 void Reveal() {string path=Selected().Path;Process.Start(new ProcessStartInfo {FileName=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"explorer.exe"),Arguments="/select,\""+path+"\"",UseShellExecute=true});}
 void Hash() {
  string path=Selected().Path;var hashWorker=new BackgroundWorker();status.Text="Вычисление SHA-256: "+path;
  hashWorker.DoWork+=delegate(object sender,DoWorkEventArgs args) {using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)) using(var sha=SHA256.Create()) args.Result=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();};
  hashWorker.RunWorkerCompleted+=delegate(object sender,RunWorkerCompletedEventArgs args) {if(!closing) MessageBox.Show(args.Error==null ? path+"\r\n\r\n"+args.Result : args.Error.Message,"SHA-256");hashWorker.Dispose();};hashWorker.RunWorkerAsync();
 }
 static string Csv(string s) {if(s.Length>0 && "=+-@\t\r\n".IndexOf(s[0])>=0)s="'"+s;return "\""+s.Replace("\"","\"\"")+"\"";}
 void Export() {using(var d=new SaveFileDialog {Filter="CSV (*.csv)|*.csv",FileName="MCRtool-files.csv"}) if(d.ShowDialog()==DialogResult.OK) using(var writer=new StreamWriter(d.FileName,false,new UTF8Encoding(true))) {writer.WriteLine("Name,Bytes,Modified,Path");foreach(var f in index)if(Matches(f))writer.WriteLine(Csv(f.Name)+","+f.Size+","+Csv(f.Modified.ToString("o"))+","+Csv(f.Path));}}
}
