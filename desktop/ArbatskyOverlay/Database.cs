using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ArbatskyOverlay;
public sealed class Entry
{
 public string Id { get; set; } = ""; public string Section { get; set; } = ""; public string Number { get; set; } = "";
 public string Part { get; set; } = ""; public string Title { get; set; } = ""; public string Summary { get; set; } = "";
 public string Stars { get; set; } = ""; public string Penalty { get; set; } = ""; public string Jurisdiction { get; set; } = "";
 public string Source { get; set; } = ""; public string Keywords { get; set; } = ""; public bool Popular { get; set; }
 public string Code => Section switch { "uk" => "УК", "upk" => "УПК", "koap" => "КоАП", "pgo" => "ПГО", "fsb" => "ФСБ", "cheats" => "Шпаргалка", _ => "Памятка" };
 public string Label => Code + (Number == "" ? "" : " " + Number) + (Part == "" ? "" : " · ч. " + Part);
 public string SearchText => (Label + " " + Title + " " + Summary + " " + Keywords + " " + Jurisdiction).ToLowerInvariant().Replace('ё','е');
}
public sealed class Database
{
 const string Root = "https://pyerkk.github.io/arbatsky-law-portal/";
 static readonly string[] Files = { "extended.json", "catalog.json", "references.json", "summaries.json", "pgo.json", "cheats.json", "general.json" };
 const string Uk = "https://forum.russia.online/threads/ugolovnyi-kodeks-ro.39487/", Upk = "https://forum.russia.online/threads/ugolovno-protsessual-nyi-kodeks-ro.39485/", Koap = "https://forum.russia.online/threads/kodeks-ob-administrativnykh-pravonarusheniyakh-ro.39488/", Fsb = "https://forum.russia.online/threads/federal-nyi-zakon-o-federal-noi-sluzhbe-bezopasnosti.40142/";
 static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ArbatskyOverlay");
 static string Cache => Path.Combine(Folder, "database.json");
 public List<Entry> Entries { get; private set; } = new();
 public string Status { get; private set; } = "Встроенная копия базы";
 static string S(JsonElement el,string key) => el.ValueKind == JsonValueKind.Object && el.TryGetProperty(key,out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
 static JsonElement Prop(JsonElement el,string key) => el.GetProperty(key);
 static string At(string url,string n) => string.IsNullOrWhiteSpace(url) ? "" : url.Split('#')[0]+"#:~:text="+Uri.EscapeDataString("Статья "+n+".");
 static string Lookup(JsonElement map,string key) => map.TryGetProperty(key,out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
 public void Load()
 {
  Dictionary<string,string>? docs = null;
  if (File.Exists(Cache)) try { docs=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Cache)); Parse(docs!); Status="База обновлена: "+File.GetLastWriteTime(Cache).ToString("dd.MM.yyyy HH:mm"); return; } catch { }
  docs=new(); var assembly=Assembly.GetExecutingAssembly();
  foreach (var f in Files) { using var stream=assembly.GetManifestResourceStream("Data."+f) ?? throw new InvalidDataException("Нет встроенного файла " + f); using var reader=new StreamReader(stream); docs[f]=reader.ReadToEnd(); }
  Parse(docs); Status="Встроенная копия базы · обновления ещё не загружены";
 }
 public async Task UpdateAsync(CancellationToken token)
 {
  using var client=new HttpClient { Timeout=TimeSpan.FromSeconds(20) }; var candidate=new Dictionary<string,string>();
  foreach (var f in Files) candidate[f]=await client.GetStringAsync(Root+f,token);
  var previous=Entries;
  try {
   Parse(candidate); if(Entries.Count < 250) throw new InvalidDataException("Получена неполная база.");
   Directory.CreateDirectory(Folder); var temp=Cache+".tmp"; await File.WriteAllTextAsync(temp,JsonSerializer.Serialize(candidate),token);
   File.Move(temp,Cache,true); Status="База обновлена: "+DateTime.Now.ToString("dd.MM.yyyy HH:mm");
  } catch { Entries=previous; throw; }
 }
 void Parse(Dictionary<string,string> docs)
 {
  foreach(var f in Files) if(!docs.ContainsKey(f)) throw new InvalidDataException("Нет файла "+f);
  using var ext=JsonDocument.Parse(docs["extended.json"]); using var cat=JsonDocument.Parse(docs["catalog.json"]); using var refs=JsonDocument.Parse(docs["references.json"]); using var sum=JsonDocument.Parse(docs["summaries.json"]); using var pgo=JsonDocument.Parse(docs["pgo.json"]); using var cheats=JsonDocument.Parse(docs["cheats.json"]); using var general=JsonDocument.Parse(docs["general.json"]);
  var result=new List<Entry>();
  void AddUk(JsonElement a,bool popular) {
   var n=S(a,"number"); var part=S(a,"part"); if(n=="" || S(a,"title")=="") throw new InvalidDataException("Некорректная статья УК");
   string stars=""; if(a.TryGetProperty("stars",out var star) && star.ValueKind==JsonValueKind.Number && star.TryGetInt32(out int v)) stars=v>0?new string('★',Math.Min(v,5)):(S(a,"starsText") is string s && s!="" ? s : "★–★★★★★");
   result.Add(new Entry{Id="uk:"+n+":"+part,Section="uk",Number=n,Part=part,Title=S(a,"title"),Summary=S(a,"other"),Stars=stars,Penalty=S(a,"term"),Jurisdiction=S(a,"jurisdiction"),Source=At(Uk,n),Popular=popular});
  }
  foreach(var a in Prop(ext.RootElement,"popular").EnumerateArray()) AddUk(a,true);
  foreach(var a in cat.RootElement.EnumerateArray()) AddUk(a,false);
  foreach(var a in Prop(ext.RootElement,"index").EnumerateArray()) { var n=S(a,"number");var text=Lookup(general.RootElement,n); if(text!="") result.Add(new Entry{Id="uk:general:"+n,Section="uk",Number=n,Title=S(a,"title"),Summary=text,Source=At(Uk,n)}); }
  var detailedUpk=Prop(refs.RootElement,"upk").EnumerateArray().ToDictionary(x=>S(x,"source"),x=>S(x,"text"));
  foreach(var a in Prop(refs.RootElement,"upkIndex").EnumerateArray()) { var n=S(a,"number"); var detail=detailedUpk.TryGetValue("Ст. "+n,out var t)?t:Lookup(Prop(sum.RootElement,"upk"),n); result.Add(new Entry{Id="upk:"+n,Section="upk",Number=n,Title=S(a,"title"),Summary=detail,Source=At(Upk,n)}); }
  var admin=Prop(refs.RootElement,"admin").EnumerateArray().ToDictionary(x=>S(x,"number"),x=>S(x,"term"));
  foreach(var a in Prop(refs.RootElement,"adminIndex").EnumerateArray()) { var n=S(a,"number");result.Add(new Entry{Id="koap:"+n,Section="koap",Number=n,Title=S(a,"title"),Summary=Lookup(Prop(sum.RootElement,"koap"),n),Penalty=admin.TryGetValue(n,out var v)?v:"",Source=At(Koap,n)}); }
  foreach(var a in pgo.RootElement.EnumerateArray()) { var n=S(a,"number"); result.Add(new Entry{Id="pgo:"+n,Section="pgo",Number=n,Title=S(a,"title"),Summary=S(a,"summary"),Source=S(a,"url"),Keywords=S(a,"category")}); }
  string[] fsb={"Назначение ФСБ","Правовая основа","Принципы деятельности","Соблюдение прав и свобод","Направления деятельности","Борьба с терроризмом","Борьба с преступностью","Разведывательная деятельность","Обязанности ФСБ","Права ФСБ","Применение оружия, спецсредств и силы","Проникновение в помещения и на территории","Применение оружия","Применение специальных средств","Применение физической силы","Гарантии правовой защиты служащего","Контроль деятельности ФСБ","Прокурорский надзор"};
  for(int i=0;i<fsb.Length;i++){var n=(i+1).ToString();result.Add(new Entry{Id="fsb:law:"+n,Section="fsb",Number=n,Title=fsb[i],Summary=Lookup(Prop(sum.RootElement,"fsb"),n),Source=At(Fsb,n)});}
  string charterUrl="https://forum.russia.online/threads/vnutrennii-ustav-federal-noi-sluzhby-bezopasnosti-ro.41645/";
  (string n,string title,string summary)[] charter={
   ("5.13.1","Соблюдение законов и приказов","Сотрудник соблюдает законодательство, устав, внутренние акты и установленные приказы."),
   ("5.13.2","Субординация и общение","Сотрудник соблюдает субординацию и корректно общается с коллегами и гражданами."),
   ("5.13.3","Реагирование на вызовы","Реагирование на вызовы, нарушения и чрезвычайные ситуации осуществляется в пределах компетенции."),
   ("5.13.4","Документация и отчёты","Сотрудник фиксирует результаты работы и сдаёт предусмотренную регламентом отчётность."),
   ("5.13.5","Подготовка сотрудников","Устав предусматривает участие в служебной подготовке и повышение квалификации."),
   ("5.14.1","Защита своих прав","Сотрудник может защищать свои законные права, достоинство и личную безопасность."),
   ("5.14.3","Перерывы и отпуск","Перерывы и отпуск предоставляются по внутреннему распорядку и трудовому кодексу."),
   ("5.14.4","Обжалование приказов","Противоречащие законодательству или уставу приказы могут обжаловаться в установленном порядке.")};
  foreach(var x in charter)result.Add(new Entry{Id="fsb:charter:"+x.n,Section="fsb",Number=x.n,Title="Устав ФСБ · "+x.title,Summary=x.summary,Source=charterUrl+"#:~:text="+Uri.EscapeDataString(x.n+" ")});
  int ix=0;foreach(var a in Prop(refs.RootElement,"fsb").EnumerateArray())result.Add(new Entry{Id="fsb:ref:"+(ix++),Section="fsb",Title=S(a,"title"),Summary=S(a,"text"),Source=S(a,"url")});
  foreach(var a in cheats.RootElement.EnumerateArray()) { var text=string.Join("\n",Prop(a,"steps").EnumerateArray().Select(x=>"• "+x.GetString())); var n=S(a,"number");result.Add(new Entry{Id="cheat:"+n,Section="cheats",Number=n,Title=S(a,"title"),Summary=text,Source=S(a,"url")}); }
  result.Add(new Entry{Id="detention:rights",Section="detention",Title="Права задержанного · текст для зачитывания",Summary="Вы вправе отказаться от дачи показаний, не свидетельствовать против себя, супруга и близких родственников, вызвать частного или государственного адвоката и воспользоваться телефонным звонком. Ваши показания могут быть использованы как доказательства. Права понятны?\n\nИгровая формулировка; уточняйте порядок и условия по УПК и ПГО.",Source=At(Upk,"92"),Keywords="миранда права"});
  if(result.Any(x=>string.IsNullOrWhiteSpace(x.Id)) || result.GroupBy(x=>x.Id).Any(g=>g.Count()>1)) throw new InvalidDataException("Обнаружены пустые или повторяющиеся идентификаторы");
  Entries=result;
 }
}
