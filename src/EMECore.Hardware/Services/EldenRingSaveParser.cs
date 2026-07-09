using System.Text;
using EMECore.Core.Models;

namespace EMECore.Hardware.Services;

public class EldenRingSaveParser
{
    private static readonly byte[] BND4_MAGIC = Encoding.ASCII.GetBytes("BND4");

    public EldenRingSaveData? ParseFromFile(string filePath)
    {
        try
        {
            var data = File.ReadAllBytes(filePath);
            return ParseFromBytes(data, Path.GetFileName(filePath));
        }
        catch { return null; }
    }

    public EldenRingSaveData? ParseFromBytes(byte[] data, string fileName = "")
    {
        if (data.Length < 100) return null;

        var save = new EldenRingSaveData
        {
            FileName = fileName,
            FileSize = data.Length,
            RawBytes = data
        };

        var isBnd4 = HasBnd4Magic(data);
        save.IsEncrypted = !isBnd4;

        var text = Encoding.UTF8.GetString(data);
        save.ExtractedStrings = ExtractAsciiStrings(data, 6);
        save.StatsData = new Dictionary<string, long>();
        save.QuestData = new Dictionary<string, bool>();

        var statKeywords = new[]
        {
            "Kill", "kill", "death", "Death", "boss", "Boss",
            "level", "Level", "runes", "Runes", "weapon", "Weapon",
            "armor", "Armor", "spell", "Spell", "incant", "Incant",
            "summon", "Summon", "guardian", "Guardian", "maiden", "Maiden",
            "site", "Site", "grace", "Grace", "flask", "Flask"
        };

        foreach (var keyword in statKeywords)
        {
            var count = CountOccurrences(text, keyword);
            if (count > 0)
                save.StatsData[$"stat_{keyword}"] = count;
        }

        var questPatterns = new[]
        {
            "MQ", "SQ", "CG", "TD", "EH", "FK", "GF",
            "main_", "side_", "covenant", "ending"
        };

        foreach (var pattern in questPatterns)
        {
            var count = CountOccurrences(text, pattern);
            if (count > 0)
                save.QuestData[$"quest_{pattern}"] = true;
        }

        return save;
    }

    private static bool HasBnd4Magic(byte[] data)
    {
        if (data.Length < BND4_MAGIC.Length) return false;
        for (int i = 0; i < BND4_MAGIC.Length; i++)
        {
            if (data[i] != BND4_MAGIC[i]) return false;
        }
        return true;
    }

    private static List<string> ExtractAsciiStrings(byte[] data, int minLength)
    {
        var strings = new List<string>();
        var current = new StringBuilder();

        foreach (var b in data)
        {
            if (b >= 32 && b < 127)
            {
                current.Append((char)b);
            }
            else
            {
                if (current.Length >= minLength)
                    strings.Add(current.ToString());
                current.Clear();
            }
        }

        if (current.Length >= minLength)
            strings.Add(current.ToString());

        return strings;
    }

    private static int CountOccurrences(string text, string pattern)
    {
        int count = 0, index = 0;
        while ((index = text.IndexOf(pattern, index, StringComparison.OrdinalIgnoreCase)) != -1)
        {
            count++;
            index += pattern.Length;
        }
        return count;
    }
}

public class EldenRingSaveData
{
    public string FileName { get; set; } = "";
    public long FileSize { get; set; }
    public byte[]? RawBytes { get; set; }
    public bool IsEncrypted { get; set; }
    public List<string> ExtractedStrings { get; set; } = new();
    public Dictionary<string, bool> QuestData { get; set; } = new();
    public Dictionary<string, long> StatsData { get; set; } = new();
}
