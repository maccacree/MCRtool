using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Xml.Serialization;
using Microsoft.Win32;

public class Entry {
 public string Path, Name, Label, Kind, Value;
 public bool Exists;
 public override string ToString() { return Label; }
}
public class Snapshot {
 public string User;
 public List<Entry> Entries = new List<Entry>();
}
public class MainForm : Form {
 CheckedListBox options = new CheckedListBox();
 TextBox output = new TextBox();
 List<Entry> targets = new List<Entry>();
 string data;
 public MainForm() {
  Text = "MCRtool | Portable 0.2"; Size = new Size(1000,740); MinimumSize = new Size(800,600);
  Font = new Font("Segoe UI",10); StartPosition = FormStartPosition.CenterScreen;
  data = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"MCRtool-data");
  var top = new Label { Text = "MCRtool — восстановление настроек Windows\r\nВыберите исправления после диагностики. Изменённая настройка сама по себе не доказывает заражение.", Dock = DockStyle.Top, Height = 65, Padding = new Padding(12) };
  var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 88, Padding = new Padding(8) };
  AddButton(buttons,"Диагностика",Scan); AddButton(buttons,"Исправить выбранное",Repair);
  AddButton(buttons,"Восстановить копию",Restore); AddButton(buttons,"Сохранить отчёт",SaveReport);
  AddButton(buttons,"Поиск файлов",delegate { new FileSearchForm().Show(this); });
  AddButton(buttons,"Планировщик",delegate { new TasksForm().Show(this); });
  options.Dock = DockStyle.Top; options.Height = 230; options.CheckOnClick = true;
  output.Dock = DockStyle.Fill; output.Multiline = true; output.ReadOnly = true; output.ScrollBars = ScrollBars.Both; output.WordWrap = false;
  Controls.Add(output); Controls.Add(options); Controls.Add(buttons); Controls.Add(top);
  string sys = @"Software\Microsoft\Windows\CurrentVersion\Policies\System";
  string exp = @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer";
  Add(sys,"DisableTaskMgr","Снять запрет Диспетчера задач");
  Add(sys,"DisableRegistryTools","Снять запрет редактора реестра");
  Add(@"Software\Policies\Microsoft\Windows\System","DisableCMD","Снять запрет командной строки");
  Add(exp,"NoControlPanel","Снять запрет Панели управления");
  Add(exp,"NoRun","Снять запрет окна «Выполнить»");
  Add(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings","ProxyEnable","Отключить ручной прокси текущего пользователя");
  Add(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings","AutoConfigURL","Удалить адрес сценария настройки прокси (PAC)");
  Shown += delegate { Scan(); };
 }
 void Add(string path,string name,string label) { var e = new Entry {Path=path,Name=name,Label=label}; targets.Add(e); options.Items.Add(e,false); }
 void AddButton(FlowLayoutPanel p,string title,Action action) { var b = new Button {Text=title,AutoSize=true}; b.Click += delegate { try { action(); } catch(Exception ex) { Log("ОШИБКА: "+ex.Message); MessageBox.Show(ex.Message,"MCRtool",MessageBoxButtons.OK,MessageBoxIcon.Error); } }; p.Controls.Add(b); }
 void Log(string text) { output.AppendText(text+Environment.NewLine); }
 RegistryKey Root() { return RegistryKey.OpenBaseKey(RegistryHive.CurrentUser,Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Registry32); }
 Entry Read(Entry source) {
  var e = new Entry {Path=source.Path,Name=source.Name,Label=source.Label};
  using(var root=Root()) using(var key=root.OpenSubKey(e.Path)) {
   if(key==null || Array.IndexOf(key.GetValueNames(),e.Name)<0) return e;
   var kind=key.GetValueKind(e.Name); if(kind!=RegistryValueKind.DWord && kind!=RegistryValueKind.String && kind!=RegistryValueKind.ExpandString) throw new InvalidOperationException("Неожиданный тип значения: "+e.Name);
   e.Exists=true; e.Kind=kind.ToString(); e.Value=Convert.ToString(key.GetValue(e.Name,null,RegistryValueOptions.DoNotExpandEnvironmentNames),System.Globalization.CultureInfo.InvariantCulture);
  } return e;
 }
 void Write(Entry e) {
  using(var root=Root()) {
   if(!e.Exists) { using(var key=root.OpenSubKey(e.Path,true)) { if(key!=null) key.DeleteValue(e.Name,false); } return; }
   using(var key=root.CreateSubKey(e.Path)) { var kind=(RegistryValueKind)Enum.Parse(typeof(RegistryValueKind),e.Kind); object val=e.Value; if(kind==RegistryValueKind.DWord) val=Int32.Parse(e.Value,System.Globalization.CultureInfo.InvariantCulture); key.SetValue(e.Name,val,kind); }
  }
 }
 string User() { return System.Security.Principal.WindowsIdentity.GetCurrent().User.Value; }
 string Backup(List<Entry> entries) {
  Directory.CreateDirectory(data);
  string path=Path.Combine(data,"backup-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")+".xml");
  using(var f=new FileStream(path,FileMode.CreateNew)) new XmlSerializer(typeof(Snapshot)).Serialize(f,new Snapshot {User=User(),Entries=entries});
  Log("Резервная копия: "+path); return path;
 }
 void Scan() {
  output.Clear(); Log("MCRtool 0.2 | "+DateTime.Now+" | "+Environment.OSVersion); Log("Область исправлений: текущий пользователь. Проверяйте корпоративные политики с администратором.");
  foreach(var e in targets) { try { var state=Read(e); Log(e.Label+": "+(state.Exists ? state.Value : "не задано")); } catch(Exception ex) { Log(e.Name+": "+ex.Message); } }
  Log("\r\nАвтозагрузка (только просмотр):");
  foreach(var hive in new RegistryHive[] {RegistryHive.CurrentUser,RegistryHive.LocalMachine}) {
   foreach(var view in Environment.Is64BitOperatingSystem ? new RegistryView[] {RegistryView.Registry64,RegistryView.Registry32} : new RegistryView[] {RegistryView.Registry32}) {
    foreach(string branch in new string[] {"Run","RunOnce"}) {
     try { using(var root=RegistryKey.OpenBaseKey(hive,view)) using(var key=root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\"+branch)) { if(key!=null) foreach(string n in key.GetValueNames()) Log(hive+"/"+view+"/"+branch+" | "+n+" = "+key.GetValue(n)); } } catch(Exception ex) { Log(ex.Message); }
    }
   }
  }
  foreach(var folder in new Environment.SpecialFolder[] {Environment.SpecialFolder.Startup,Environment.SpecialFolder.CommonStartup}) {
   try { string dir=Environment.GetFolderPath(folder); if(Directory.Exists(dir)) foreach(string f in Directory.GetFiles(dir)) Log("Startup: "+f); } catch(Exception ex) { Log(ex.Message); }
  }
  Log("\r\nhosts (только просмотр):");
  try { Log(File.ReadAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),@"drivers\etc\hosts"))); } catch(Exception ex) { Log(ex.Message); }
 }
 void Repair() {
  var before=new List<Entry>(); foreach(Entry e in options.CheckedItems) before.Add(Read(e));
  if(before.Count==0) { MessageBox.Show("Выберите исправления."); return; }
  var plan=new StringBuilder(); foreach(var e in before) plan.AppendLine(e.Label+" | было: "+(e.Exists ? e.Value : "не задано"));
  if(MessageBox.Show(plan+"\r\nПрименить с резервной копией?","MCRtool",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes) return;
  Backup(before);
  foreach(var e in before) {
   try {
    if(e.Name=="ProxyEnable") Write(new Entry {Path=e.Path,Name=e.Name,Exists=true,Kind="DWord",Value="0"});
    else Write(new Entry {Path=e.Path,Name=e.Name,Exists=false});
    var check=Read(e); if(e.Name=="ProxyEnable" ? (!check.Exists || check.Value!="0") : check.Exists) throw new IOException("Проверка записи не пройдена");
    Log("Исправлено: "+e.Label);
   } catch(Exception ex) { Log("Не исправлено: "+e.Label+" | "+ex.Message); }
  }
  Log("Закройте и снова откройте затронутые приложения. Для применения прокси может потребоваться выход из учётной записи.");
 }
 void Restore() {
  using(var d=new OpenFileDialog {Filter="MCRtool backup (*.xml)|*.xml",InitialDirectory=Directory.Exists(data) ? data : AppDomain.CurrentDomain.BaseDirectory}) {
   if(d.ShowDialog()!=DialogResult.OK) return;
   Snapshot s; using(var reader=System.Xml.XmlReader.Create(d.FileName,new System.Xml.XmlReaderSettings {DtdProcessing=System.Xml.DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=1048576})) s=(Snapshot)new XmlSerializer(typeof(Snapshot)).Deserialize(reader);
   if(s.User!=User()) throw new InvalidOperationException("Копия принадлежит другой учётной записи.");
   if(s.Entries==null || s.Entries.Count==0 || s.Entries.Count>targets.Count) throw new InvalidOperationException("Некорректная копия.");
   var seen=new HashSet<string>();
   foreach(var e in s.Entries) { if(!targets.Exists(t=>t.Path==e.Path && t.Name==e.Name) || !seen.Add(e.Path+"|"+e.Name)) throw new InvalidOperationException("Копия содержит неподдерживаемое значение."); if(e.Exists) { if(e.Kind=="DWord") Int32.Parse(e.Value,System.Globalization.CultureInfo.InvariantCulture); else if(e.Kind!="String" && e.Kind!="ExpandString") throw new InvalidOperationException("Неподдерживаемый тип."); } }
   if(MessageBox.Show("Восстановить "+s.Entries.Count+" значений из копии?","MCRtool",MessageBoxButtons.YesNo)!=DialogResult.Yes) return;
   var current=new List<Entry>(); foreach(var e in s.Entries) current.Add(Read(e)); Backup(current);
   foreach(var e in s.Entries) { try { Write(e); var v=Read(e); if(v.Exists!=e.Exists || (e.Exists && (v.Kind!=e.Kind || v.Value!=e.Value))) throw new IOException("Проверка восстановления не пройдена"); Log("Восстановлено: "+e.Name); } catch(Exception ex) { Log("Не восстановлено: "+e.Name+" | "+ex.Message); } }
  }
 }
 void SaveReport() { using(var d=new SaveFileDialog {Filter="Text (*.txt)|*.txt",FileName="MCRtool-report.txt"}) if(d.ShowDialog()==DialogResult.OK) { File.WriteAllText(d.FileName,output.Text,Encoding.UTF8); Log("Отчёт сохранён."); } }
 [STAThread] public static void Main() { Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); Application.Run(new MainForm()); }
}
