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

        m_window = new WinUI.MainWindow();
        m_window.Activate();
    }
}
