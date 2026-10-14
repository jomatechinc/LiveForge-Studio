using System.Windows;
using LiveForge_Studio.ViewModels;

namespace LiveForge_Studio;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var window = new MainWindow
        {
            DataContext = new MainViewModel()
        };
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (MainWindow?.DataContext is IDisposable disposable)
        {
            disposable.Dispose();
        }

        base.OnExit(e);
    }
}
