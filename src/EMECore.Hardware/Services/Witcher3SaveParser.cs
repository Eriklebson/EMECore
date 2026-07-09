using System.Text;
using EMECore.Core.Models;

namespace EMECore.Hardware.Services;

public class Witcher3SaveParser
{
    private static readonly byte[] MAGIC = Encoding.ASCII.GetBytes("SNFHFZLC");

    public Witcher3SaveData? ParseFromFile(string filePath)
    {
        try
        {
            var data = File.ReadAllBytes(filePath);
            return ParseFromBytes(data, Path.GetFileName(filePath));
        }
        catch { return null; }
    }

    public Witcher3SaveData? ParseFromBytes(byte[] data, string fileName = "")
    {
        if (data.Length < 100) return null;

        var save = new Witcher3SaveData
        {
            FileName = fileName,
            FileSize = data.Length,
            RawBytes = data
        };

        if (!HasWitcher3Magic(data))
        {
            ExtractGenericData(data, save);
            return save;
        }

        var text = Encoding.UTF8.GetString(data);
        save.ExtractedStrings = ExtractAsciiStrings(data, 6);
        save.StatsData = new Dictionary<string, long>();
        save.QuestData = new Dictionary<string, bool>();

        var statKeywords = new[]
        {
            "Kill", "kill", "quest", "Quest", "monster", "Monster",
            "level", "Level", "skill", "Skill", "alchem", "Alchem",
            "craft", "Craft", "guard", "Guard", "witcher", "Witcher",
            "Geralt", "geralt", "Ciri", "ciri", "hors", "Hors"
        };

        foreach (var keyword in statKeywords)
        {
            var count = CountOccurrences(text, keyword);
            if (count > 0)
                save.StatsData[$"stat_{keyword}"] = count;
        }

        var questPatterns = new[]
        {
            "MQ", "SQ", "BG", "GW", "TP", "BC", "WP",
            "quest_", "main_", "side_"
        };

        foreach (var pattern in questPatterns)
        {
            var count = CountOccurrences(text, pattern);
            if (count > 0)
                save.QuestData[$"quest_{pattern}"] = true;
        }

        return save;
    }

    private static bool HasWitcher3Magic(byte[] data)
    {
        if (data.Length < MAGIC.Length) return false;
        for (int i = 0; i < MAGIC.Length; i++)
        {
            if (data[i] != MAGIC[i]) return false;
        }
        return true;
    }

    private void ExtractGenericData(byte[] data, Witcher3SaveData save)
    {
        var text = Encoding.UTF8.GetString(data);
        save.ExtractedStrings = ExtractAsciiStrings(data, 8);
        save.StatsData = new Dictionary<string, long>();
        save.QuestData = new Dictionary<string, bool>();

        var statKeywords = new[] { "Kill", "quest", "level", "skill", "item", "gold", "monster", "witcher" };
        foreach (var keyword in statKeywords)
        {
            var count = CountOccurrences(text, keyword);
            if (count > 0)
                save.StatsData[$"stat_{keyword}"] = count;
        }
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

public class Witcher3SaveData
{
    public string FileName { get; set; } = "";
    public long FileSize { get; set; }
    public byte[]? RawBytes { get; set; }
    public List<string> ExtractedStrings { get; set; } = new();
    public Dictionary<string, bool> QuestData { get; set; } = new();
    public Dictionary<string, long> StatsData { get; set; } = new();
}
