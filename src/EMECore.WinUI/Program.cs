using System;
using System.Runtime.InteropServices;

namespace EMECore.WinUI;

public static class Program
{
    [STAThread]
    [DllImport("combase.dll")]
    private static extern int RoInitialize(int initType);

    static void Main(string[] args)
    {
        RoInitialize(2);
        Microsoft.UI.Xaml.Application.Start((p) =>
        {
            var context = new Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(
                Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            new App();
        });
    }
}
