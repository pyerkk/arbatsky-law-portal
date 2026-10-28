using System;
using System.Windows;
namespace ArbatskyOverlay;
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e); var window = new MainWindow(); MainWindow = window;
        window.Show();
        if (Array.Exists(e.Args, x => x == "--background")) window.Hide();
    }
}
