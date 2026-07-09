using System.Text;
using EMECore.Core.Models;

namespace EMECore.Hardware.Services;

public class HogwartsLegacySaveParser
{
    private static readonly byte[] GVAS_MAGIC = Encoding.ASCII.GetBytes("GVAS");

    public HogwartsLegacySaveData? ParseFromFile(string filePath)
    {
        try
        {
            var data = File.ReadAllBytes(filePath);
            return ParseFromBytes(data, Path.GetFileName(filePath));
        }
        catch { return null; }
    }

    public HogwartsLegacySaveData? ParseFromBytes(byte[] data, string fileName = "")
    {
        if (data.Length < 100) return null;

        var save = new HogwartsLegacySaveData
        {
            FileName = fileName,
            FileSize = data.Length,
            RawBytes = data
        };

        var isGvas = HasGvasMagic(data);
        save.IsCompressed = !isGvas;

        var text = Encoding.UTF8.GetString(data);
        save.ExtractedStrings = ExtractAsciiStrings(data, 6);
        save.StatsData = new Dictionary<string, long>();
        save.QuestData = new Dictionary<string, bool>();

        var statKeywords = new[]
        {
            "Kill", "kill", "quest", "Quest", "spell", "Spell",
            "level", "Level", "skill", "Skill", "house", "House",
            "potion", "Potion", "plant", "Plant", "room", "Room",
            "talent", "Talent", "gear", "Gear", "collection", "Collection",
            "trial", "Trial", " Merlin", "merlin", " conjuration", "Conjuration"
        };

        foreach (var keyword in statKeywords)
        {
            var count = CountOccurrences(text, keyword);
            if (count > 0)
                save.StatsData[$"stat_{keyword}"] = count;
        }

        var questPatterns = new[]
        {
            "Main", "Main", "Side", "Side", "Companion", "Companion",
            "main_", "side_", "companion_", "Assignment"
        };

        foreach (var pattern in questPatterns)
        {
            var count = CountOccurrences(text, pattern);
            if (count > 0)
                save.QuestData[$"quest_{pattern}"] = true;
        }

        return save;
    }

    private static bool HasGvasMagic(byte[] data)
    {
        if (data.Length < GVAS_MAGIC.Length) return false;
        for (int i = 0; i < GVAS_MAGIC.Length; i++)
        {
            if (data[i] != GVAS_MAGIC[i]) return false;
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

public class HogwartsLegacySaveData
{
    public string FileName { get; set; } = "";
    public long FileSize { get; set; }
    public byte[]? RawBytes { get; set; }
    public bool IsCompressed { get; set; }
    public List<string> ExtractedStrings { get; set; } = new();
    public Dictionary<string, bool> QuestData { get; set; } = new();
    public Dictionary<string, long> StatsData { get; set; } = new();
}
