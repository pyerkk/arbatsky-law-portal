using System;
using System.Linq;
using System.Windows;
namespace ArbatskyOverlay;
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (Array.Exists(e.Args, x => x == "--self-test"))
        {
            try
            {
                var db = new Database(); db.Load();
                if (db.Entries.Count < 500 || !db.Entries.Any(x => x.Id == "uk:319.2:1" && x.SearchText.Contains("помеха")) || !db.Entries.Any(x => x.Id == "upk:92") || !db.Entries.Any(x => x.Id == "koap:12.1") || !db.Entries.Any(x => x.Id == "pgo:1.8") || !db.Entries.Any(x => x.Id == "detention:rights")) throw new InvalidOperationException("Некорректная встроенная база");
                Shutdown(0);
            }
            catch { Shutdown(1); }
            return;
        }
        var window = new MainWindow(); MainWindow = window;
        window.Show();
        if (Array.Exists(e.Args, x => x == "--background")) window.Hide();
    }
}
