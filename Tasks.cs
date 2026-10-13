using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;

public class TaskRecord {public string Path,Xml;public bool Enabled;public int State;}
public class TaskStateBackup {public string Machine,Path,DefinitionXml;public bool Enabled;}
public static class Scheduler {
 public static void Release(object o) {if(o!=null && Marshal.IsComObject(o))Marshal.FinalReleaseComObject(o);}
 public static dynamic Connect() {dynamic s=Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service",true));try{s.Connect();return s;}catch{Release(s);throw;}}
 public static TaskRecord Read(dynamic task) {return new TaskRecord {Path=(string)task.Path,Xml=(string)task.Xml,Enabled=(bool)task.Enabled,State=(int)task.State};}
 public static void Walk(dynamic folder,List<TaskRecord> rows,List<string> errors) {
  object collection=null,folders=null;
  try {
   collection=folder.GetTasks(1);dynamic tasks=collection;
   for(int i=1;i<=tasks.Count;i++){object t=null;try{t=tasks[i];rows.Add(Read(t));}catch(Exception ex){errors.Add(ex.Message);}finally{Release(t);}}
  } catch(Exception ex){errors.Add(ex.Message);}finally{Release(collection);}
  try {
   folders=folder.GetFolders(0);dynamic children=folders;
   for(int i=1;i<=children.Count;i++){object f=null;try{f=children[i];Walk(f,rows,errors);}catch(Exception ex){errors.Add(ex.Message);}finally{Release(f);}}
  }catch(Exception ex){errors.Add(ex.Message);}finally{Release(folders);}
 }
 public static string WithoutEnabled(string xml) {
  var doc=new XmlDocument {XmlResolver=null};using(var r=XmlReader.Create(new StringReader(xml),new XmlReaderSettings {DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=4194304}))doc.Load(r);
  var ns=new XmlNamespaceManager(doc.NameTable);ns.AddNamespace("t","http://schemas.microsoft.com/windows/2004/02/mit/task");
  var enabled=doc.SelectSingleNode("/t:Task/t:Settings/t:Enabled",ns);if(enabled!=null)enabled.ParentNode.RemoveChild(enabled);return doc.OuterXml;
 }
}
public class TasksForm : Form {
 ListView list=new ListView {Dock=DockStyle.Fill,View=View.Details,FullRowSelect=true,MultiSelect=false};
 TextBox detail=new TextBox {Dock=DockStyle.Bottom,Height=190,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Both,WordWrap=false};
 TextBox filter=new TextBox {Width=250}; Label status=new Label {Dock=DockStyle.Bottom,Height=45};
 List<TaskRecord> rows=new List<TaskRecord>();BackgroundWorker worker=new BackgroundWorker();bool closing;
 string data=System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"MCRtool-data","tasks");
 public TasksForm() {
  Text="MCRtool — планировщик заданий";Size=new Size(1050,730);MinimumSize=new Size(850,550);Font=new Font("Segoe UI",10);
  var bar=new FlowLayoutPanel {Dock=DockStyle.Top,Height=90,Padding=new Padding(8)};
  Button(bar,"Обновить",RefreshTasks);bar.Controls.Add(filter);Button(bar,"Отключить",delegate{Toggle(false);});Button(bar,"Включить",delegate{Toggle(true);});
  Button(bar,"Экспорт XML",Export);Button(bar,"Восстановить состояние",Restore);Button(bar,"Открыть системный редактор",delegate {Process.Start(new ProcessStartInfo {FileName=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"mmc.exe"),Arguments="taskschd.msc",UseShellExecute=true});});
  list.Columns.Add("Путь задания",650);list.Columns.Add("Включено",100);list.Columns.Add("Состояние",140);
  Controls.Add(list);Controls.Add(detail);Controls.Add(status);Controls.Add(bar);filter.TextChanged+=delegate{Render();};
  list.SelectedIndexChanged+=delegate{detail.Text=list.SelectedItems.Count==1 ? ((TaskRecord)list.SelectedItems[0].Tag).Xml : "";};
  worker.DoWork+=delegate(object sender,DoWorkEventArgs args) {object service=null,folder=null;try {service=Scheduler.Connect();folder=((dynamic)service).GetFolder("\\");var found=new List<TaskRecord>();var errors=new List<string>();Scheduler.Walk(folder,found,errors);args.Result=new object[] {found,errors};}finally{Scheduler.Release(folder);Scheduler.Release(service);}};
  worker.RunWorkerCompleted+=delegate(object sender,RunWorkerCompletedEventArgs args) {if(closing)return;if(args.Error!=null){status.Text="Ошибка: "+args.Error.Message;return;}var a=(object[])args.Result;rows=(List<TaskRecord>)a[0];var errors=(List<string>)a[1];Render();status.Text="Заданий: "+rows.Count+" | ошибок доступа: "+errors.Count+". Отключение не останавливает уже запущенное задание.";if(errors.Count>0)detail.Text=String.Join(Environment.NewLine,errors.ToArray());};
  Shown+=delegate{RefreshTasks();};FormClosing+=delegate{closing=true;};
 }
 void Button(FlowLayoutPanel p,string title,Action action){var b=new Button{Text=title,AutoSize=true};b.Click+=delegate{try{action();}catch(Exception ex){MessageBox.Show(ex.Message,"MCRtool",MessageBoxButtons.OK,MessageBoxIcon.Error);}};p.Controls.Add(b);}
 void RefreshTasks(){if(worker.IsBusy)return;status.Text="Чтение заданий, включая скрытые…";worker.RunWorkerAsync();}
 void Render(){list.BeginUpdate();list.Items.Clear();foreach(var r in rows)if(r.Path.IndexOf(filter.Text,StringComparison.OrdinalIgnoreCase)>=0){var item=new ListViewItem(new string[]{r.Path,r.Enabled?"Да":"Нет",State(r.State)});item.Tag=r;list.Items.Add(item);}list.EndUpdate();}
 static string State(int s){switch(s){case 1:return "Отключено";case 2:return "В очереди";case 3:return "Готово";case 4:return "Выполняется";default:return "Неизвестно";}}
 TaskRecord Selected(){if(list.SelectedItems.Count!=1)throw new InvalidOperationException("Выберите одно задание.");return (TaskRecord)list.SelectedItems[0].Tag;}
 void SaveState(TaskRecord r){Directory.CreateDirectory(data);string path=System.IO.Path.Combine(data,"task-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")+".mcrtask");using(var f=new FileStream(path,FileMode.CreateNew))new XmlSerializer(typeof(TaskStateBackup)).Serialize(f,new TaskStateBackup {Machine=Environment.MachineName,Path=r.Path,Enabled=r.Enabled,DefinitionXml=r.Xml});status.Text="Копия состояния: "+path;}
 void Change(string path,bool enabled,string expectedXml) {
  object service=null,folder=null,task=null;
  try {
   service=Scheduler.Connect();folder=((dynamic)service).GetFolder("\\");task=((dynamic)folder).GetTask(path);TaskRecord current=Scheduler.Read(task);
   if(Scheduler.WithoutEnabled(current.Xml)!=Scheduler.WithoutEnabled(expectedXml))throw new InvalidOperationException("Задание изменилось. Обновите список и проверьте его определение.");
   if(current.Enabled==enabled){MessageBox.Show("Задание уже в выбранном состоянии.");return;}
   if(MessageBox.Show(path+"\r\n"+(enabled?"Включить":"Отключить")+" задание? Будет сохранена копия текущего состояния.","MCRtool",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
   TaskRecord latest=Scheduler.Read(task);
   if(latest.Xml!=current.Xml || latest.Enabled!=current.Enabled)throw new InvalidOperationException("Задание изменилось во время подтверждения. Обновите список.");
   SaveState(latest);((dynamic)task).Enabled=enabled;
   if((bool)((dynamic)task).Enabled!=enabled)throw new IOException("Проверка состояния не пройдена.");
   MessageBox.Show("Состояние изменено.\r\n"+status.Text,"MCRtool");
  }finally{Scheduler.Release(task);Scheduler.Release(folder);Scheduler.Release(service);}
  RefreshTasks();
 }
 void Toggle(bool enabled){var r=Selected();Change(r.Path,enabled,r.Xml);}
 void Export(){var r=Selected();using(var d=new SaveFileDialog{Filter="Task XML (*.xml)|*.xml",FileName="task.xml"})if(d.ShowDialog()==DialogResult.OK)File.WriteAllText(d.FileName,r.Xml,Encoding.Unicode);}
 void Restore(){using(var d=new OpenFileDialog{Filter="MCRtool task state (*.mcrtask)|*.mcrtask",InitialDirectory=data})if(d.ShowDialog()==DialogResult.OK){TaskStateBackup b;using(var reader=XmlReader.Create(d.FileName,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=4194304}))b=(TaskStateBackup)new XmlSerializer(typeof(TaskStateBackup)).Deserialize(reader);if(b.Machine!=Environment.MachineName || String.IsNullOrEmpty(b.Path)||String.IsNullOrEmpty(b.DefinitionXml))throw new InvalidOperationException("Копия другого компьютера или неверный формат.");Change(b.Path,b.Enabled,b.DefinitionXml);}}
}
