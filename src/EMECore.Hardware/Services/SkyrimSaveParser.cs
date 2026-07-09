using System.Text;
using EMECore.Core.Models;

namespace EMECore.Hardware.Services;

public class SkyrimSaveParser
{
    private const string MAGIC = "TESV_SAVEGame";
    private const uint SKYRIM_SPECIAL_EDITION_MAGIC = 0x41535952; // "ASYR" - SSE header marker

    public SkyrimSaveData? ParseFromFile(string filePath)
    {
        try
        {
            var data = File.ReadAllBytes(filePath);
            return ParseFromBytes(data);
        }
        catch { return null; }
    }

    public SkyrimSaveData? ParseFromBytes(byte[] data)
    {
        if (data.Length < 100) return null;
        if (Encoding.ASCII.GetString(data, 0, MAGIC.Length) != MAGIC) return null;

        var save = new SkyrimSaveData();
        var offset = MAGIC.Length;

        try
        {
            save.HeaderVersion = BitConverter.ToUInt32(data, offset); offset += 4;
            save.SaveNumber = BitConverter.ToUInt32(data, offset); offset += 4;
            save.PlayerName = ReadLengthPrefixedString(data, ref offset);
            save.PlayerLevel = BitConverter.ToUInt32(data, offset); offset += 4;
            save.PlayerLocation = ReadLengthPrefixedString(data, ref offset);
            save.GameDate = ReadLengthPrefixedString(data, ref offset);
            save.RaceSex = ReadLengthPrefixedString(data, ref offset);
            save.PlayTimeSeconds = BitConverter.ToUInt32(data, offset); offset += 4;

            save.FileName = "";
            save.FileSize = data.Length;
            save.RawBytes = data;

            ExtractAchievements(save, data, offset);
        }
        catch { }

        return save;
    }

    private void ExtractAchievements(SkyrimSaveData save, byte[] data, int startOffset)
    {
        var text = Encoding.UTF8.GetString(data);
        save.MagicBytes = MAGIC;

        var strings = ExtractAsciiStrings(data, 8);
        save.ExtractedStrings = strings;

        save.QuestData = new Dictionary<string, bool>();
        save.SkillData = new Dictionary<string, int>();
        save.StatsData = new Dictionary<string, long>();

        var questPatterns = new[]
        {
            "MQ101", "MQ102", "MQ103", "MQ104", "MQ105",
            "TG00", "TG01", "TG02", "TG03", "TG04", "TG05",
            "DB01", "DB02", "DB03", "DB04", "DB05", "DB06", "DB07", "DB08",
            "CW01", "CW02", "CW03", "CW04",
            "DA01", "DA02", "DA03", "DA04", "DA05", "DA06", "DA07", "DA08",
            "MG01", "MG02", "MG03", "MG04", "MG05", "MG06", "MG07", "MG08",
            "Favor", "C00", "C01", "C02", "C03", "C04", "C05", "C06"
        };

        foreach (var pattern in questPatterns)
        {
            if (text.Contains(pattern))
            {
                save.QuestData[$"quest_{pattern}"] = true;
            }
        }

        var skillNames = new[]
        {
            "Archery", "Block", "Destruction", "Illusion", "Conjuration",
            "Restoration", "Alteration", "OneHanded", "TwoHanded", "HeavyArmor",
            "LightArmor", "Sneak", "Lockpicking", "Pickpocket", "Speech",
            "Alchemy", "Enchanting", "Smithing", "Crafting"
        };

        foreach (var skill in skillNames)
        {
            if (text.Contains(skill, StringComparison.OrdinalIgnoreCase))
            {
                save.SkillData[$"skill_{skill}"] = 1;
            }
        }

        var statKeywords = new[]
        {
            "Kill", "killed", "murder", "bounty", "gold", "Septim",
            "dragon", "Dragon", "word", "Word", "stone", "Stone",
            "dungeon", "Dungeon", "location", "Location",
            "marry", "Marry", "adopt", "Adopt", "house", "House",
            "playtime", "hours", "level", "Level", "skill", "Skill"
        };

        foreach (var keyword in statKeywords)
        {
            var count = CountOccurrences(text, keyword);
            if (count > 0)
            {
                save.StatsData[$"stat_{keyword}"] = count;
            }
        }

        save.Level = (int)save.PlayerLevel;
        save.PlayTimeHours = (int)(save.PlayTimeSeconds / 3600);
        save.TotalKills = (int)save.StatsData.GetValueOrDefault("stat_Kill", 0);
        save.DragonsSlain = (int)save.StatsData.GetValueOrDefault("stat_dragon", 0);
        save.WordsLearned = (int)save.StatsData.GetValueOrDefault("stat_word", 0);
        save.BarenziahStones = (int)save.StatsData.GetValueOrDefault("stat_stone", 0);
        save.DungeonsCleared = (int)save.StatsData.GetValueOrDefault("stat_dungeon", 0);
        save.LocationsDiscovered = (int)save.StatsData.GetValueOrDefault("stat_location", 0);
        save.HousesOwned = (int)save.StatsData.GetValueOrDefault("stat_house", 0);
        save.IsMarried = text.Contains("marry", StringComparison.OrdinalIgnoreCase);
        save.HasAdopted = text.Contains("adopt", StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadLengthPrefixedString(byte[] data, ref int offset)
    {
        if (offset + 4 > data.Length) return "";
        var length = BitConverter.ToInt32(data, offset); offset += 4;
        if (length <= 0 || offset + length > data.Length) return "";
        var result = Encoding.UTF8.GetString(data, offset, length);
        offset += length;
        return result;
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
                {
                    strings.Add(current.ToString());
                }
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

public class SkyrimSaveData
{
    public uint HeaderVersion { get; set; }
    public uint SaveNumber { get; set; }
    public string PlayerName { get; set; } = "";
    public uint PlayerLevel { get; set; }
    public string PlayerLocation { get; set; } = "";
    public string GameDate { get; set; } = "";
    public string RaceSex { get; set; } = "";
    public uint PlayTimeSeconds { get; set; }
    public string FileName { get; set; } = "";
    public long FileSize { get; set; }
    public byte[]? RawBytes { get; set; }
    public string MagicBytes { get; set; } = "";
    public List<string> ExtractedStrings { get; set; } = new();
    public Dictionary<string, bool> QuestData { get; set; } = new();
    public Dictionary<string, int> SkillData { get; set; } = new();
    public Dictionary<string, long> StatsData { get; set; } = new();
    public int Level { get; set; }
    public int PlayTimeHours { get; set; }
    public int TotalKills { get; set; }
    public int DragonsSlain { get; set; }
    public int WordsLearned { get; set; }
    public int BarenziahStones { get; set; }
    public int DungeonsCleared { get; set; }
    public int LocationsDiscovered { get; set; }
    public int HousesOwned { get; set; }
    public bool IsMarried { get; set; }
    public bool HasAdopted { get; set; }
}
