using Microsoft.UI.Xaml;
using EMECore.WinUI.Theme;

namespace EMECore;

public partial class App : Application
{
    internal Window? m_window;

    public App()
    {
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        SteamColors.ApplyToApplication(this);

        var cmdLine = Environment.GetCommandLineArgs();
        if (cmdLine.Contains("--monitor"))
        {
            m_window = new WinUI.Views.MonitorWindow();
            m_window.Activate();
            return;
        }

        m_window = new WinUI.MainWindow();
        m_window.Activate();
    }
}
