using Microsoft.UI.Xaml;
using EMECore.WinUI.Theme;
using System.Diagnostics;
using System.Security.Principal;

namespace EMECore;

public partial class App : Application
{
    internal Window? m_window;

    public App()
    {
    }

    private static bool IsRunningAsStoreApp()
    {
        try
        {
            return Windows.ApplicationModel.Package.Current != null;
        }
        catch
        {
            return false;
        }
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        if (!IsRunningAsStoreApp())
        {
            try
            {
                var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                if (!principal.IsInRole(WindowsBuiltInRole.Administrator))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = Environment.ProcessPath!,
                        Verb = "runas",
                        UseShellExecute = true
                    });
                    Environment.Exit(0);
                    return;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[App] Erro ao verificar elevar privilegios: {ex.Message}");
            }
        }

        SteamColors.ApplyToApplication(this);

        m_window = new WinUI.MainWindow();
        m_window.Activate();
    }
}
