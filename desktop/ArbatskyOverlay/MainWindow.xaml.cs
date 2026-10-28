using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace ArbatskyOverlay;
public sealed class Preferences
{
 public string ToggleHotkey {get;set;}="Ctrl+Shift+Space"; public string PassHotkey {get;set;}="Ctrl+Shift+F2";
 public bool Topmost {get;set;}=true; public double Opacity {get;set;}=0.96;
 public double Left {get;set;}=double.NaN; public double Top {get;set;}=double.NaN; public double Width {get;set;}=760; public double Height {get;set;}=720;
 public List<string> Favorites {get;set;}=new(); public List<string> Collection {get;set;}=new(); public bool CollectionInitialized {get;set;}
}
public partial class MainWindow : Window
{
 const int WM_HOTKEY=0x0312, GWL_EXSTYLE=-20, WS_EX_TRANSPARENT=0x20, WS_EX_LAYERED=0x80000;
 const string RunName="ArbatskyOverlay";
 [DllImport("user32.dll", SetLastError=true)] static extern bool RegisterHotKey(IntPtr hWnd,int id,uint modifiers,uint key);
 [DllImport("user32.dll", SetLastError=true)] static extern bool UnregisterHotKey(IntPtr hWnd,int id);
 [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hWnd,int index);
 [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr hWnd,int index,int value);
 [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hWnd);
 readonly string folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ArbatskyOverlay");
 string SettingsPath => Path.Combine(folder,"settings.json");
 Preferences prefs; readonly Database db=new(); readonly DispatcherTimer saveTimer=new(){Interval=TimeSpan.FromMilliseconds(600)};
 readonly Forms.NotifyIcon tray; HwndSource? source; IntPtr hwnd,lastForeground; bool exiting,passThrough,ready; string section="popular";
 static readonly string[] Preset={"uk:317:","uk:318:1","uk:318:2","uk:319:1","uk:319.1:2","uk:319.2:1","uk:105:1","uk:161:1","uk:213:1","uk:139:1","uk:222:1","uk:228:1","uk:228.1:1","uk:231:1","uk:314:"};
 public MainWindow()
 {
  prefs=LoadPreferences(); InitializeComponent();
  Width=Math.Clamp(prefs.Width,560,1800); Height=Math.Clamp(prefs.Height,400,1400); Topmost=prefs.Topmost; Opacity=Math.Clamp(prefs.Opacity,0.45,1);
  if(double.IsFinite(prefs.Left)&&double.IsFinite(prefs.Top)&&prefs.Left>=SystemParameters.VirtualScreenLeft-Width+100&&prefs.Left<SystemParameters.VirtualScreenLeft+SystemParameters.VirtualScreenWidth-100&&prefs.Top>=SystemParameters.VirtualScreenTop&&prefs.Top<SystemParameters.VirtualScreenTop+SystemParameters.VirtualScreenHeight-60){WindowStartupLocation=WindowStartupLocation.Manual; Left=prefs.Left; Top=prefs.Top;}
  else WindowStartupLocation=WindowStartupLocation.CenterScreen;
  saveTimer.Tick+=(_,__)=>{saveTimer.Stop();SavePreferences();};
  var menu=new Forms.ContextMenuStrip(); menu.Items.Add("Открыть / скрыть",null,(_,__)=>Dispatcher.Invoke(ToggleWindow));menu.Items.Add("Режим просмотра: включить / выключить",null,(_,__)=>Dispatcher.Invoke(TogglePass));menu.Items.Add("Настройки",null,(_,__)=>Dispatcher.Invoke(()=>{ShowOverlay();ShowSettings();}));menu.Items.Add("Выход",null,(_,__)=>Dispatcher.Invoke(ExitApp));
  tray=new Forms.NotifyIcon { Icon=System.Drawing.SystemIcons.Shield, Text="Правовой портал | Арбатский", Visible=true, ContextMenuStrip=menu }; tray.DoubleClick+=(_,__)=>Dispatcher.Invoke(ToggleWindow);
  SourceInitialized+=OnSourceInitialized; Loaded+=OnLoaded;
 }
 Preferences LoadPreferences(){try{return JsonSerializer.Deserialize<Preferences>(File.ReadAllText(SettingsPath),new JsonSerializerOptions{NumberHandling=System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals})??new();}catch{return new();}}
 void SavePreferences(){try{Directory.CreateDirectory(folder); var tmp=SettingsPath+".tmp";File.WriteAllText(tmp,JsonSerializer.Serialize(prefs,new JsonSerializerOptions{WriteIndented=true,NumberHandling=System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals}));File.Move(tmp,SettingsPath,true);}catch(Exception e){Status.Text="Не удалось сохранить настройки: "+e.Message;}}
 void OnSourceInitialized(object? sender,EventArgs e){hwnd=new WindowInteropHelper(this).Handle;source=HwndSource.FromHwnd(hwnd);source?.AddHook(WndProc);if(!RegisterBoth(prefs.ToggleHotkey,prefs.PassHotkey)){UnregisterHotKey(hwnd,1);UnregisterHotKey(hwnd,2);prefs.ToggleHotkey="Ctrl+Shift+Space";prefs.PassHotkey="Ctrl+Shift+F2";if(!RegisterBoth(prefs.ToggleHotkey,prefs.PassHotkey)) System.Windows.MessageBox.Show("Не удалось зарегистрировать горячие клавиши. Откройте настройки через значок в трее и назначьте свободные сочетания.","Арбатский");}}
 void OnLoaded(object sender,RoutedEventArgs e){try{db.Load(); if(!prefs.CollectionInitialized){prefs.Collection=Preset.ToList();prefs.CollectionInitialized=true;SavePreferences();} Filter.ItemsSource=new[]{"Все документы","УК","УПК","КоАП","ПГО","ФСБ","Памятки"};Filter.SelectedIndex=0;ready=true;Render();}catch(Exception ex){Status.Text="Не удалось загрузить встроенную базу: "+ex.Message;}}
 static bool ParseKey(string text,out uint mod,out uint vk){mod=0;vk=0;var tokens=text.Split('+',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);foreach(var raw in tokens){var t=raw.ToLowerInvariant();if(t is "ctrl" or "control")mod|=2;else if(t=="shift")mod|=4;else if(t=="alt")mod|=1;else if(t is "win" or "windows")mod|=8;else{if(vk!=0)return false;if(!Enum.TryParse<Key>(raw,true,out var key))return false;vk=(uint)KeyInterop.VirtualKeyFromKey(key);}}return mod!=0&&vk!=0;}
 bool RegisterBoth(string toggle,string pass){if(!ParseKey(toggle,out var m1,out var k1)||!ParseKey(pass,out var m2,out var k2)||(m1==m2&&k1==k2))return false;if(!RegisterHotKey(hwnd,1,m1|0x4000,k1))return false;if(!RegisterHotKey(hwnd,2,m2|0x4000,k2)){UnregisterHotKey(hwnd,1);return false;}return true;}
 IntPtr WndProc(IntPtr h,int msg,IntPtr wp,IntPtr lp,ref bool handled){if(msg==WM_HOTKEY){int id=wp.ToInt32();if(IsVisible&&GetForegroundWindow()==hwnd&&Keyboard.FocusedElement is TextBox){handled=true;return IntPtr.Zero;}if(id==1)ToggleWindow();else if(id==2)TogglePass();handled=true;}return IntPtr.Zero;}
 void ShowOverlay(){if(!IsVisible){lastForeground=GetForegroundWindow();Show();}if(passThrough) SetPass(false);WindowState=WindowState.Normal;Activate();}
 void ToggleWindow(){if(IsVisible){Hide();if(lastForeground!=IntPtr.Zero)SetForegroundWindow(lastForeground);}else ShowOverlay();}
 void SetPass(bool enabled){passThrough=enabled;int flags=GetWindowLong(hwnd,GWL_EXSTYLE);SetWindowLong(hwnd,GWL_EXSTYLE,enabled?flags|WS_EX_LAYERED|WS_EX_TRANSPARENT:flags&~WS_EX_TRANSPARENT);Status.Text=enabled?"РЕЖИМ ПРОСМОТРА · нажатия проходят сквозь окно · "+prefs.PassHotkey+" для управления":"Сверяйте условия и актуальность на форуме.";if(enabled&&lastForeground!=IntPtr.Zero)SetForegroundWindow(lastForeground);else if(!enabled&&IsVisible)Activate();}
 void TogglePass(){if(!IsVisible){ShowOverlay();SetPass(true);}else SetPass(!passThrough);}
 void ExitApp(){exiting=true;saveTimer.Stop();if(ready){prefs.Left=Left;prefs.Top=Top;prefs.Width=Width;prefs.Height=Height;SavePreferences();}UnregisterHotKey(hwnd,1);UnregisterHotKey(hwnd,2);tray.Visible=false;tray.Dispose();Close();Application.Current.Shutdown();}
 void Window_Closing(object? s,CancelEventArgs e){if(!exiting){e.Cancel=true;Hide();if(lastForeground!=IntPtr.Zero)SetForegroundWindow(lastForeground);}}
 void Window_KeyDown(object sender,System.Windows.Input.KeyEventArgs e){if(e.Key==Key.Escape){if(SettingsPanel.Visibility==Visibility.Visible)Back_Click(sender,e);else ToggleWindow();e.Handled=true;}}
 void Header_MouseLeftButtonDown(object sender,MouseButtonEventArgs e){if(e.ButtonState==MouseButtonState.Pressed)try{DragMove();}catch(InvalidOperationException){}}
 void GeometryChanged(object? sender,EventArgs e){if(!ready||WindowState!=WindowState.Normal)return;prefs.Left=Left;prefs.Top=Top;prefs.Width=Width;prefs.Height=Height;saveTimer.Stop();saveTimer.Start();}
 void Hide_Click(object s,RoutedEventArgs e)=>ToggleWindow();
 void Settings_Click(object s,RoutedEventArgs e)=>ShowSettings();
 void ShowSettings(){ToggleKey.Text=prefs.ToggleHotkey;PassKey.Text=prefs.PassHotkey;OpacitySlider.Value=prefs.Opacity;AlwaysOnTop.IsChecked=prefs.Topmost;StartWithWindows.IsChecked=IsAutoStart();DataStatus.Text=db.Status;ListScroll.Visibility=Visibility.Collapsed;SettingsPanel.Visibility=Visibility.Visible;}
 void Back_Click(object s,RoutedEventArgs e){SettingsPanel.Visibility=Visibility.Collapsed;ListScroll.Visibility=Visibility.Visible;Render();}
 bool IsAutoStart(){try{using var k=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");return k?.GetValue(RunName)!=null;}catch{return false;}}
 void SaveSettings_Click(object s,RoutedEventArgs e){var t=ToggleKey.Text.Trim();var p=PassKey.Text.Trim();UnregisterHotKey(hwnd,1);UnregisterHotKey(hwnd,2);if(!RegisterBoth(t,p)){RegisterBoth(prefs.ToggleHotkey,prefs.PassHotkey);DataStatus.Text="Клавиша занята или формат неверный. Укажите разные сочетания, например Ctrl+Shift+Space и Ctrl+Shift+F2.";return;}prefs.ToggleHotkey=t;prefs.PassHotkey=p;prefs.Opacity=OpacitySlider.Value;prefs.Topmost=AlwaysOnTop.IsChecked==true;Opacity=prefs.Opacity;Topmost=prefs.Topmost;try{using var key=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");if(StartWithWindows.IsChecked==true)key.SetValue(RunName,"\""+Environment.ProcessPath+"\" --background");else key.DeleteValue(RunName,false);}catch(Exception ex){DataStatus.Text="Не удалось изменить автозапуск: "+ex.Message;SavePreferences();return;}SavePreferences();DataStatus.Text="Настройки сохранены. "+db.Status;}
 async void Update_Click(object s,RoutedEventArgs e){UpdateButton.IsEnabled=false;DataStatus.Text="Загружаем базу…";try{using var cts=new CancellationTokenSource(TimeSpan.FromSeconds(90));await db.UpdateAsync(cts.Token);DataStatus.Text=db.Status;}catch(Exception ex){DataStatus.Text="Обновление не удалось. Прежняя база сохранена. "+ex.Message;}finally{UpdateButton.IsEnabled=true;}}
 void Section_Click(object s,RoutedEventArgs e){section=(string)((Button)s).Tag; SettingsPanel.Visibility=Visibility.Collapsed;ListScroll.Visibility=Visibility.Visible;Search.Text="";Filter.SelectedIndex=0;Render();}
 void Search_Changed(object s,RoutedEventArgs e){if(ready)Render();}
 static string Clip(Entry x)=>x.Label+" — "+x.Title+(x.Stars==""?"":"\nРозыск: "+x.Stars)+(x.Penalty==""?"":"\nНаказание: "+x.Penalty)+(x.Jurisdiction==""?"":"\nЮрисдикция: "+x.Jurisdiction)+(x.Summary==""?"":"\n"+x.Summary)+(x.Source==""?"":"\n"+x.Source);
 void Render()
 {
  if(!ready)return;ItemsPanel.Children.Clear();IEnumerable<Entry> list=db.Entries;
  list=section switch {"popular"=>list.Where(x=>x.Popular),"favorites"=>list.Where(x=>prefs.Favorites.Contains(x.Id)),"template"=>Preset.Select(id=>db.Entries.FirstOrDefault(x=>x.Id==id)).Where(x=>x!=null).Select(x=>x!),"collection"=>prefs.Collection.Select(id=>db.Entries.FirstOrDefault(x=>x.Id==id)).Where(x=>x!=null).Select(x=>x!),_=>list.Where(x=>x.Section==section)};
  string q=Search.Text.ToLowerInvariant().Replace('ё','е').Trim();if(q!="" && section is not ("favorites" or "collection" or "template"))list=db.Entries;
  var filter=Filter.SelectedItem as string;list=filter switch {"УК"=>list.Where(x=>x.Section=="uk"),"УПК"=>list.Where(x=>x.Section=="upk"),"КоАП"=>list.Where(x=>x.Section=="koap"),"ПГО"=>list.Where(x=>x.Section=="pgo"),"ФСБ"=>list.Where(x=>x.Section=="fsb"),"Памятки"=>list.Where(x=>x.Section is "cheats" or "detention"),_=>list};
  if(q!="")list=list.Where(x=>q.Split(' ',StringSplitOptions.RemoveEmptyEntries).All(word=>x.SearchText.Contains(word)));
  var found=list.ToList(); if(found.Count==0){ItemsPanel.Children.Add(new TextBlock{Text="Ничего не найдено. Измените запрос или раздел.",Foreground=Brushes.LightGray,Margin=new Thickness(8)});return;}
  if(section is "collection" or "template") { var h=new Grid{Margin=new Thickness(12,0,12,6)};foreach(var width in new[]{80d,75d,0d,95d})h.ColumnDefinitions.Add(new ColumnDefinition{Width=width==0?new GridLength(1,GridUnitType.Star):new GridLength(width)});var headings=new[]{"СТАТЬЯ","ЗВЁЗДЫ","ТРАКТОВКА","СРОК / ШТРАФ"};for(int i=0;i<4;i++){var t=new TextBlock{Text=headings[i],FontSize=10,Foreground=Brushes.Gray};Grid.SetColumn(t,i);h.Children.Add(t);}ItemsPanel.Children.Add(h);foreach(var item in found)ItemsPanel.Children.Add(CompactCard(item)); }else foreach(var item in found)ItemsPanel.Children.Add(Card(item));
  Status.Text=(passThrough?"РЕЖИМ ПРОСМОТРА · "+prefs.PassHotkey+" для управления · ":"")+"Записей: "+found.Count+" · сверяйте условия на форуме.";
 }
 UIElement CompactCard(Entry x)
 {
  var grid=new Grid{Margin=new Thickness(12,8,12,2)};foreach(var width in new[]{80d,75d,0d,95d})grid.ColumnDefinitions.Add(new ColumnDefinition{Width=width==0?new GridLength(1,GridUnitType.Star):new GridLength(width)});
  string[] cells={x.Label,x.Stars==""?"—":x.Stars,x.Title,x.Penalty==""?"—":x.Penalty};for(int i=0;i<4;i++){var label=new TextBlock{Text=cells[i],FontSize=11,TextWrapping=TextWrapping.Wrap,Foreground=i==1?Brushes.Tan:Brushes.White,Margin=new Thickness(0,0,8,0)};Grid.SetColumn(label,i);grid.Children.Add(label);}
  var detail=new Expander{Header=grid,Foreground=Brushes.LightGray,Background=new SolidColorBrush(Color.FromRgb(34,43,38)),Margin=new Thickness(0,0,0,6),Content=Card(x)};return detail;
 }
 UIElement Card(Entry x)
 {
  var border=new Border{Background=new SolidColorBrush(Color.FromRgb(34,43,38)),BorderBrush=new SolidColorBrush(Color.FromRgb(67,82,72)),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(5),Margin=new Thickness(0,0,0,8),Padding=new Thickness(12)};
  var box=new StackPanel();var head=new DockPanel();var fav=new Button{Content=prefs.Favorites.Contains(x.Id)?"★":"☆",ToolTip="Избранное",Tag=x.Id,Padding=new Thickness(8,3,8,3)};fav.Click+=(_,__)=>{if(!prefs.Favorites.Remove(x.Id))prefs.Favorites.Add(x.Id);SavePreferences();Render();};DockPanel.SetDock(fav,Dock.Right);head.Children.Add(fav);
  var add=new Button{Content=prefs.Collection.Contains(x.Id)?"✓":"+",ToolTip="Добавить / убрать из подборки",Padding=new Thickness(8,3,8,3)};add.Click+=(_,__)=>{if(!prefs.Collection.Remove(x.Id))prefs.Collection.Add(x.Id);SavePreferences();Render();};DockPanel.SetDock(add,Dock.Right);head.Children.Add(add);
  if(section=="collection"){foreach(var direction in new[]{-1,1}){var b=new Button{Content=direction<0?"↑":"↓",ToolTip="Изменить порядок",Padding=new Thickness(8,3,8,3)};b.Click+=(_,__)=>{int at=prefs.Collection.IndexOf(x.Id),to=at+direction;if(at>=0&&to>=0&&to<prefs.Collection.Count){(prefs.Collection[at],prefs.Collection[to])=(prefs.Collection[to],prefs.Collection[at]);SavePreferences();Render();}};DockPanel.SetDock(b,Dock.Right);head.Children.Add(b);}}
  var title=new StackPanel();title.Children.Add(new TextBlock{Text=x.Label+(x.Stars==""?"":"   "+x.Stars),Foreground=new SolidColorBrush(Color.FromRgb(218,169,153)),FontWeight=FontWeights.Bold,FontSize=12});title.Children.Add(new TextBlock{Text=x.Title,TextWrapping=TextWrapping.Wrap,FontSize=14,Margin=new Thickness(0,5,0,4)});head.Children.Add(title);box.Children.Add(head);
  var meta=string.Join("  ·  ",new[]{x.Penalty,x.Jurisdiction==""?"":"Юрисдикция: "+x.Jurisdiction}.Where(v=>v!=""));if(meta!="")box.Children.Add(new TextBlock{Text=meta,Foreground=Brushes.LightGray,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,4,0,4)});
  var details=new Expander{Header="Подробнее",Foreground=Brushes.LightGray,Margin=new Thickness(0,5,0,0)};var detailsBox=new StackPanel();detailsBox.Children.Add(new TextBlock{Text=x.Summary==""?"Подробности и условия — в первоисточнике.":x.Summary,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,7,0,8)});var actions=new WrapPanel();var copy=new Button{Content="Копировать"};copy.Click+=(_,__)=>{try{System.Windows.Clipboard.SetText(Clip(x));Status.Text="Скопировано в буфер обмена.";}catch{Status.Text="Не удалось скопировать текст.";}};actions.Children.Add(copy);if(x.Source!=""){var link=new Button{Content="Источник ↗"};link.Click+=(_,__)=>{try{Process.Start(new ProcessStartInfo(x.Source){UseShellExecute=true});}catch{Status.Text="Не удалось открыть источник.";}};actions.Children.Add(link);}detailsBox.Children.Add(actions);details.Content=detailsBox;box.Children.Add(details);border.Child=box;return border;
 }
}
