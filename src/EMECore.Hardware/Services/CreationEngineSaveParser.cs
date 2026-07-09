using System.Text;
using EMECore.Core.Models;

namespace EMECore.Hardware.Services;

public class CreationEngineSaveParser
{
    public CreationEngineSaveData? ParseFromFile(string filePath)
    {
        try
        {
            var data = File.ReadAllBytes(filePath);
            return ParseFromBytes(data, Path.GetFileName(filePath));
        }
        catch { return null; }
    }

    public CreationEngineSaveData? ParseFromBytes(byte[] data, string fileName = "")
    {
        if (data.Length < 200) return null;

        var save = new CreationEngineSaveData { FileName = fileName, FileSize = data.Length, RawBytes = data };

        var text = Encoding.UTF8.GetString(data);

        if (text.Contains("FO4_SAVEGAME"))
        {
            save.Engine = "Fallout4";
            ParseFallout4Header(data, save);
        }
        else if (text.Contains("SFS_SAVEGAME") || text.Contains("BCPS"))
        {
            save.Engine = "Starfield";
            ParseStarfieldHeader(data, save);
        }
        else if (text.Contains("TESV_SAVEGame") || text.Contains("TES4_SAVE"))
        {
            save.Engine = "Skyrim";
            ParseSkyrimHeader(data, save);
        }
        else if (text.Contains("CKWD_SAVE"))
        {
            save.Engine = "CreationEngine";
            ParseGenericCreationEngine(data, save);
        }
        else
        {
            save.Engine = "Unknown";
            ExtractGenericData(data, save);
        }

        return save;
    }

    private void ParseFallout4Header(byte[] data, CreationEngineSaveData save)
    {
        var text = Encoding.UTF8.GetString(data);
        var offset = text.IndexOf("FO4_SAVEGAME");
        if (offset < 0) return;

        var strings = ExtractAsciiStrings(data, 6);
        save.ExtractedStrings = strings;

        save.StatsData = new Dictionary<string, long>();
        save.QuestData = new Dictionary<string, bool>();

        var statKeywords = new[]
        {
            "Kill", "kill", "murder", "bounty", "gold", "cap",
            "dragon", "Dragon", "word", "Word", "settlement", "Settlement",
            "quest", "Quest", "faction", "Faction", "level", "Level",
            "Perk", "perk", "skill", "Skill", "radiation", "Radiation"
        };

        foreach (var keyword in statKeywords)
        {
            var count = CountOccurrences(text, keyword);
            if (count > 0)
                save.StatsData[$"stat_{keyword}"] = count;
        }

        var questPatterns = new[]
        {
            "MQ201", "MQ202", "MQ203", "MQ204", "MQ205",
            "F400", "F401", "F402", "F403", "F404",
            "RA401", "RA402", "RA403", "RA404",
            "BoS201", "BoS202", "BoS203", "BoS204",
            "RR201", "RR202", "RR203", "RR204",
            "IN201", "IN202", "IN203", "IN204"
        };

        foreach (var pattern in questPatterns)
        {
            if (text.Contains(pattern))
                save.QuestData[$"quest_{pattern}"] = true;
        }
    }

    private void ParseStarfieldHeader(byte[] data, CreationEngineSaveData save)
    {
        var text = Encoding.UTF8.GetString(data);
        var strings = ExtractAsciiStrings(data, 6);
        save.ExtractedStrings = strings;
        save.StatsData = new Dictionary<string, long>();
        save.QuestData = new Dictionary<string, bool>();

        var statKeywords = new[]
        {
            "Kill", "kill", "quest", "Quest", "skill", "Skill",
            "level", "Level", "planet", "Planet", "ship", "Ship",
            "faction", "Faction", "outpost", "Outpost", "craft", "Craft"
        };

        foreach (var keyword in statKeywords)
        {
            var count = CountOccurrences(text, keyword);
            if (count > 0)
                save.StatsData[$"stat_{keyword}"] = count;
        }
    }

    private void ParseSkyrimHeader(byte[] data, CreationEngineSaveData save)
    {
        var text = Encoding.UTF8.GetString(data);
        var strings = ExtractAsciiStrings(data, 6);
        save.ExtractedStrings = strings;
        save.StatsData = new Dictionary<string, long>();
        save.QuestData = new Dictionary<string, bool>();

        var statKeywords = new[]
        {
            "Kill", "kill", "dragon", "Dragon", "word", "Word",
            "stone", "Stone", "dungeon", "Dungeon", "location", "Location",
            "marry", "Marry", "adopt", "Adopt", "house", "House",
            "level", "Level", "skill", "Skill", "shout", "Shout"
        };

        foreach (var keyword in statKeywords)
        {
            var count = CountOccurrences(text, keyword);
            if (count > 0)
                save.StatsData[$"stat_{keyword}"] = count;
        }
    }

    private void ParseGenericCreationEngine(byte[] data, CreationEngineSaveData save)
    {
        var text = Encoding.UTF8.GetString(data);
        var strings = ExtractAsciiStrings(data, 6);
        save.ExtractedStrings = strings;
        save.StatsData = new Dictionary<string, long>();
        save.QuestData = new Dictionary<string, bool>();

        var statKeywords = new[] { "Kill", "quest", "level", "skill", "item", "gold" };
        foreach (var keyword in statKeywords)
        {
            var count = CountOccurrences(text, keyword);
            if (count > 0)
                save.StatsData[$"stat_{keyword}"] = count;
        }
    }

    private void ExtractGenericData(byte[] data, CreationEngineSaveData save)
    {
        var strings = ExtractAsciiStrings(data, 8);
        save.ExtractedStrings = strings;
        save.StatsData = new Dictionary<string, long>();
        save.QuestData = new Dictionary<string, bool>();
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

public class CreationEngineSaveData
{
    public string Engine { get; set; } = "";
    public string FileName { get; set; } = "";
    public long FileSize { get; set; }
    public byte[]? RawBytes { get; set; }
    public List<string> ExtractedStrings { get; set; } = new();
    public Dictionary<string, bool> QuestData { get; set; } = new();
    public Dictionary<string, long> StatsData { get; set; } = new();
}
