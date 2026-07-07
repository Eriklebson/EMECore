namespace EMECore.Core.Helpers;

public static class FormatHelpers
{
    public static string FormatBytes(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1) { order++; size /= 1024; }
        return $"{size:0.##} {sizes[order]}";
    }

    public static string FormatSpeed(long bytesPerSecond)
    {
        return $"{FormatBytes(bytesPerSecond)}/s";
    }

    public static string FormatMinutes(int minutes)
    {
        var ts = TimeSpan.FromMinutes(minutes);
        if (ts.TotalHours >= 1)
            return $"{(int)ts.TotalHours}h {ts.Minutes}m";
        return $"{ts.Minutes}m";
    }
}
