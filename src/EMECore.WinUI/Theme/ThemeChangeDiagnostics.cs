using System.Text;

namespace EMECore.WinUI.Theme;

/// <summary>
/// Diagnóstico síncrono da troca de tema. Cada marcador é descarregado no disco
/// imediatamente para sobreviver a falhas nativas do compositor WinUI.
/// </summary>
public static class ThemeChangeDiagnostics
{
    private static readonly object Sync = new();

    public static string LogPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EMECore", "logs", "theme-change.log");

    public static void StartSession(string themeName)
    {
        Write($"========== NOVA TROCA: {themeName} | PID {Environment.ProcessId} ==========");
    }

    public static void Write(string message)
    {
        try
        {
            lock (Sync)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                using var stream = new FileStream(LogPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                using var writer = new StreamWriter(stream, new UTF8Encoding(false));
                writer.WriteLine($"{DateTime.Now:O} | {message}");
                writer.Flush();
                stream.Flush(true);
            }
        }
        catch
        {
            // O diagnóstico nunca pode impedir a execução normal do aplicativo.
        }
    }
}
