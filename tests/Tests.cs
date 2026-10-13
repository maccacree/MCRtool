using System;
using System.IO;
using System.Reflection;
using System.Xml;
using System.Xml.Serialization;
class Tests {
 static int passed;
 static void Check(bool condition,string message){if(!condition)throw new Exception(message);passed++;Console.WriteLine("PASS "+message);}
 static string TaskXml(string enabled,string command){return "<Task xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\"><Settings><Enabled>"+enabled+"</Enabled></Settings><Actions><Exec><Command>"+command+"</Command></Exec></Actions></Task>";}
 static void Main(){
  Check(Scheduler.WithoutEnabled(TaskXml("true","safe.exe"))==Scheduler.WithoutEnabled(TaskXml("false","safe.exe")),"toggle state comparison");
  Check(Scheduler.WithoutEnabled(TaskXml("true","safe.exe"))!=Scheduler.WithoutEnabled(TaskXml("true","changed.exe")),"changed command blocks old state restore");
  bool dtd=false;try{Scheduler.WithoutEnabled("<!DOCTYPE a [<!ENTITY x SYSTEM 'file:///etc/passwd'>]><Task>&x;</Task>");}catch(XmlException){dtd=true;}Check(dtd,"DTD rejected");
  bool malformed=false;try{Scheduler.WithoutEnabled("<Task>");}catch(XmlException){malformed=true;}Check(malformed,"malformed XML rejected");
  var method=typeof(FileSearchForm).GetMethod("Csv",BindingFlags.NonPublic|BindingFlags.Static);
  Check((string)method.Invoke(null,new object[]{"=1+2"})=="\"'=1+2\"","CSV formula neutralized");
  Check((string)method.Invoke(null,new object[]{"a,\"b"})=="\"a,\"\"b\"","CSV quotes escaped");
  var b=new TaskStateBackup {Machine="test",Path="\\folder\\тест",DefinitionXml=TaskXml("true","safe.exe"),Enabled=true};
  var s=new XmlSerializer(typeof(TaskStateBackup));var w=new StringWriter();s.Serialize(w,b);var b2=(TaskStateBackup)s.Deserialize(new StringReader(w.ToString()));Check(b2.Path==b.Path&&b2.DefinitionXml==b.DefinitionXml&&b2.Enabled,"task state backup roundtrip");
  var snapshot=new Snapshot {User="test"};snapshot.Entries.Add(new Entry {Path="test",Name="value",Kind="DWord",Exists=true,Value="1"});var serial=new XmlSerializer(typeof(Snapshot));var output=new StringWriter();serial.Serialize(output,snapshot);var copy=(Snapshot)serial.Deserialize(new StringReader(output.ToString()));Check(copy.Entries[0].Value=="1"&&copy.Entries[0].Exists,"registry snapshot roundtrip");
  Console.WriteLine("Passed: "+passed);
 }
}
