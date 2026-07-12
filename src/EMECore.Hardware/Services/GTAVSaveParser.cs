using System.Text;
using System.Buffers.Binary;

namespace EMECore.Hardware.Services;

/// <summary>
/// Parser para saves do GTA V (formato SGTA5####).
/// Baseado no código: https://github.com/Syping/gta5sync/blob/master/SavegameData.cpp
/// </summary>
public class GTAVSaveParser
{
    private const int HEADER_LENGTH = 260;
    private static readonly byte[] VerificationValue = { 0x00, 0x00, 0x00, 0x01 };
    private const byte SEPARATOR = 0x01;

    public GTAVSaveData? ParseFromFile(string filePath)
    {
        try
        {
            var data = File.ReadAllBytes(filePath);
            var result = ParseFromBytes(data);
            if (result != null)
                result.FilePath = filePath;
            return result;
        }
        catch { return null; }
    }

    public GTAVSaveData? ParseFromBytes(byte[] data)
    {
        if (data.Length < HEADER_LENGTH)
            return null;

        if (data[0] != 0x00 || data[1] != 0x00 || data[2] != 0x00 || data[3] != 0x01)
            return null;

        var save = new GTAVSaveData
        {
            FileSize = data.Length
        };

        try
        {
            var headerBytes = new byte[HEADER_LENGTH];
            Array.Copy(data, 0, headerBytes, 0, HEADER_LENGTH);

            var nameBytes = ExtractNameFromHeader(headerBytes);
            save.CharacterName = Encoding.ASCII.GetString(nameBytes).TrimEnd('\0');

            var stats = ExtractStatsFromData(data);
            save.Stats = stats;

            save.HasData = true;

            return save;
        }
        catch
        {
            return null;
        }
    }

    private static byte[] ExtractNameFromHeader(byte[] header)
    {
        var parts = new List<byte[]>();
        var current = new List<byte>();

        for (int i = 4; i < header.Length; i++)
        {
            if (header[i] == SEPARATOR)
            {
                if (current.Count > 0)
                {
                    parts.Add(current.ToArray());
                    current.Clear();
                }
            }
            else
            {
                current.Add(header[i]);
            }
        }

        if (current.Count > 0)
            parts.Add(current.ToArray());

        if (parts.Count >= 2)
            return parts[1];

        return parts.Count > 0 ? parts[0] : Array.Empty<byte>();
    }

    private static Dictionary<string, long> ExtractStatsFromData(byte[] data)
    {
        var stats = new Dictionary<string, long>();

        var text = Encoding.ASCII.GetString(data);

        stats["file_size"] = data.Length;

        var moneyMatches = System.Text.RegularExpressions.Regex.Matches(text, @"\$[\d,]+");
        if (moneyMatches.Count > 0)
        {
            var maxMoney = 0L;
            foreach (System.Text.RegularExpressions.Match m in moneyMatches)
            {
                var numStr = m.Value.Replace("$", "").Replace(",", "");
                if (long.TryParse(numStr, out var num) && num > maxMoney)
                    maxMoney = num;
            }
            stats["money"] = maxMoney;
        }

        return stats;
    }

    public string? FindSavePath()
    {
        var profilesPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Rockstar Games", "GTA V", "Profiles");

        if (!Directory.Exists(profilesPath))
        {
            profilesPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "AppData", "Roaming", "Rockstar Games", "GTA V", "Profiles");
        }

        if (!Directory.Exists(profilesPath)) return null;

        try
        {
            foreach (var profileDir in Directory.GetDirectories(profilesPath))
            {
                var sgtaFiles = Directory.GetFiles(profileDir, "SGTA*")
                    .Where(f => !f.EndsWith(".bak"))
                    .OrderByDescending(f => new FileInfo(f).LastWriteTime)
                    .ToArray();

                if (sgtaFiles.Length > 0) return sgtaFiles[0];
            }
        }
        catch { }

        return null;
    }

    public bool HasSave() => FindSavePath() != null;
}

public class GTAVSaveData
{
    public string FilePath { get; set; } = "";
    public long FileSize { get; set; }
    public string CharacterName { get; set; } = "";
    public bool HasData { get; set; }
    public Dictionary<string, long> Stats { get; set; } = new();

    public Dictionary<string, long> StatsData => Stats;
    public Dictionary<string, bool> QuestData { get; set; } = new();
}
