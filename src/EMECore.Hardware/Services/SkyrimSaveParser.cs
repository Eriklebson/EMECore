using System.Text;
using EMECore.Core.Models;

namespace EMECore.Hardware.Services;

public class SkyrimSaveParser
{
    private const string MAGIC = "TESV_SAVEGAME";

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
        var offset = MAGIC.Length; // 13

        try
        {
            // Read header size (uint32 at offset 13)
            var headerSize = BitConverter.ToUInt32(data, offset); offset += 4;

            // Header data starts at offset 17
            var headerEnd = offset + (int)headerSize;

            // Version and save number are inside the header data
            save.HeaderVersion = BitConverter.ToUInt32(data, offset); offset += 4;
            save.SaveNumber = BitConverter.ToUInt32(data, offset); offset += 4;

            // Player name: uint16 length + ASCII bytes (NOT Unicode)
            save.PlayerName = ReadAsciiString(data, ref offset);

            // Player level: uint32
            save.PlayerLevel = BitConverter.ToUInt32(data, offset); offset += 4;

            // Player location: uint16 length + ASCII bytes
            save.PlayerLocation = ReadAsciiString(data, ref offset);

            // Game date: uint16 length + ASCII bytes
            save.GameDate = ReadAsciiString(data, ref offset);

            // Race/Sex: uint16 length + ASCII bytes
            save.RaceSex = ReadAsciiString(data, ref offset);

            // Player sex: uint16
            var playerSex = BitConverter.ToUInt16(data, offset); offset += 2;

            // Current/Level-up XP: float32 * 2
            offset += 8;

            // FILETIME: 8 bytes
            offset += 8;

            // Screenshot dimensions: uint32 * 2
            offset += 8;

            // Compression type: uint16
            offset += 2;

            // PlayTimeSeconds is NOT in the header — it's in the save data.
            // For now, estimate from the FILETIME or leave as 0.
            save.PlayTimeSeconds = 0;

            // Skip to end of header (data section starts after header)
            offset = headerEnd;

            save.FileName = "";
            save.FileSize = data.Length;
            save.RawBytes = data;

            ExtractStatsFromBinary(save, data);
        }
        catch { }

        return save;
    }

    private void ExtractStatsFromBinary(SkyrimSaveData save, byte[] data)
    {
        var text = Encoding.UTF8.GetString(data);

        save.QuestData = new Dictionary<string, bool>();
        save.SkillData = new Dictionary<string, int>();
        save.StatsData = new Dictionary<string, long>();

        save.Level = (int)save.PlayerLevel;
        save.PlayTimeHours = (int)(save.PlayTimeSeconds / 3600);

        ExtractMiscStats(save, data);
        ExtractSkills(save, data);
        ExtractQuests(save, data);

        // Fallback: if binary extraction failed, try text-based estimates
        if (save.TotalKills == 0 && save.DragonsSlain == 0 && save.WordsLearned == 0)
        {
            ExtractStatsFromText(save, text);
        }
    }

    private void ExtractMiscStats(SkyrimSaveData save, byte[] data)
    {
        // Search for known stat name strings in the binary data
        // The save file stores stats as wstring name + int32 value pairs
        // We search for the stat names and read the values after them

        var statMappings = new Dictionary<string, Action<int>>
        {
            { "Total Kills", v => save.TotalKills = v },
            { "People Killed", v => save.PeopleKilled = v },
            { "Creatures Killed", v => save.CreaturesKilled = v },
            { "Dragons Slain", v => save.DragonsSlain = v },
            { "Words Learned", v => save.WordsLearned = v },
            { "Dragon Souls Absorbed", v => save.DragonSoulsAbsorbed = v },
            { "Locations Discovered", v => save.LocationsDiscovered = v },
            { "Dungeons Cleared", v => save.DungeonsCleared = v },
            { "Barenziah Stones Found", v => save.BarenziahStones = v },
            { "Houses Owned", v => save.HousesOwned = v },
            { "Spells Learned", v => save.SpellsLearned = v },
            { "Battles Won", v => save.BattlesWon = v },
            { "Bodies Looted", v => save.BodiesLooted = v },
            { "Ingredients Harvested", v => save.IngredientsHarvested = v },
        };

        // Also check for Married/Adopted as wstring "1" = true
        var boolStats = new Dictionary<string, Action<bool>>
        {
            { "Married", v => save.IsMarried = v },
            { "Adopted Children", v => save.HasAdopted = v },
        };

        foreach (var mapping in statMappings)
        {
            var value = FindStatValue(data, mapping.Key);
            if (value.HasValue)
            {
                mapping.Value(value.Value);
            }
        }

        foreach (var mapping in boolStats)
        {
            var value = FindStatValue(data, mapping.Key);
            if (value.HasValue)
            {
                mapping.Value(value.Value > 0);
            }
        }
    }

    private int? FindStatValue(byte[] data, string statName)
    {
        var nameBytes = Encoding.Unicode.GetBytes(statName);

        for (int i = 0; i < data.Length - nameBytes.Length - 8; i++)
        {
            bool match = true;
            for (int j = 0; j < nameBytes.Length; j++)
            {
                if (data[i + j] != nameBytes[j])
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                // The value should be after the string
                // Format: wstring (uint16 length + chars) then int32 value
                int valueOffset = i + nameBytes.Length;
                if (valueOffset + 4 <= data.Length)
                {
                    return BitConverter.ToInt32(data, valueOffset);
                }
            }
        }

        return null;
    }

    private void ExtractSkills(SkyrimSaveData save, byte[] data)
    {
        // Skills are stored in the player's NPC_ ChangeForm
        // The skill levels are stored as uint8[18] array
        // We search for the skill data pattern in the binary

        var skillNames = new[]
        {
            "OneHanded", "TwoHanded", "Marksman", "Block", "Smithing",
            "HeavyArmor", "LightArmor", "Pickpocket", "Lockpicking", "Sneak",
            "Alchemy", "Speechcraft", "Alteration", "Conjuration", "Destruction",
            "Illusion", "Restoration", "Enchanting"
        };

        // For now, use a simpler approach: search for skill-related patterns
        // The actual skill levels are in the NPC_ ChangeForm which requires full parsing
        // As a heuristic, we can check if the skill name appears in the save data
        // and estimate based on context

        foreach (var skill in skillNames)
        {
            // Check if skill name exists in the save data
            if (data.Length > 0)
            {
                var skillBytes = Encoding.UTF8.GetBytes(skill);
                bool found = false;
                for (int i = 0; i < data.Length - skillBytes.Length; i++)
                {
                    bool match = true;
                    for (int j = 0; j < skillBytes.Length; j++)
                    {
                        if (data[i + j] != skillBytes[j])
                        {
                            match = false;
                            break;
                        }
                    }
                    if (match) { found = true; break; }
                }

                if (found)
                {
                    save.SkillData[$"skill_{skill}"] = 1;
                }
            }
        }
    }

    private void ExtractQuests(SkyrimSaveData save, byte[] data)
    {
        // Quest completion is stored in QUST ChangeForms
        // For now, we search for quest ID patterns in the binary data

        var questPatterns = new Dictionary<string, string[]>
        {
            { "quest_MQ", new[] { "MQ101", "MQ102", "MQ103", "MQ104", "MQ105" } },
            { "quest_TG", new[] { "TG00", "TG01", "TG02", "TG03", "TG04", "TG05" } },
            { "quest_DB", new[] { "DB01", "DB02", "DB03", "DB04", "DB05", "DB06", "DB07", "DB08" } },
            { "quest_CW", new[] { "CW01", "CW02", "CW03", "CW04" } },
            { "quest_DA", new[] { "DA01", "DA02", "DA03", "DA04", "DA05", "DA06", "DA07", "DA08" } },
            { "quest_MG", new[] { "MG01", "MG02", "MG03", "MG04", "MG05", "MG06", "MG07", "MG08" } },
            { "quest_C", new[] { "C00", "C01", "C02", "C03", "C04", "C05", "C06" } },
        };

        foreach (var questGroup in questPatterns)
        {
            foreach (var questId in questGroup.Value)
            {
                var questBytes = Encoding.UTF8.GetBytes(questId);
                for (int i = 0; i < data.Length - questBytes.Length; i++)
                {
                    bool match = true;
                    for (int j = 0; j < questBytes.Length; j++)
                    {
                        if (data[i + j] != questBytes[j])
                        {
                            match = false;
                            break;
                        }
                    }
                    if (match)
                    {
                        save.QuestData[$"{questGroup.Key}_{questId}"] = true;
                        break;
                    }
                }
            }
        }
    }

    private void ExtractStatsFromText(SkyrimSaveData save, string text)
    {
        // Fallback: count keyword occurrences as rough estimates
        // This is less accurate but better than nothing
        save.TotalKills = CountOccurrences(text, "Kill") + CountOccurrences(text, "killed");
        save.DragonsSlain = CountOccurrences(text, "dragon") + CountOccurrences(text, "Dragon");
        save.WordsLearned = CountOccurrences(text, "word") + CountOccurrences(text, "Word");
        save.BarenziahStones = CountOccurrences(text, "stone") + CountOccurrences(text, "Stone");
        save.DungeonsCleared = CountOccurrences(text, "dungeon") + CountOccurrences(text, "Dungeon");
        save.LocationsDiscovered = CountOccurrences(text, "location") + CountOccurrences(text, "Location");
        save.HousesOwned = CountOccurrences(text, "house") + CountOccurrences(text, "House");
        save.IsMarried = text.Contains("marry", StringComparison.OrdinalIgnoreCase);
        save.HasAdopted = text.Contains("adopt", StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadAsciiString(byte[] data, ref int offset)
    {
        if (offset + 2 > data.Length) return "";
        var length = BitConverter.ToUInt16(data, offset); offset += 2;
        if (length == 0 || offset + length > data.Length) return "";
        var result = Encoding.ASCII.GetString(data, offset, length);
        offset += length;
        return result;
    }

    private static string ReadWString(byte[] data, ref int offset)
    {
        if (offset + 2 > data.Length) return "";
        var length = BitConverter.ToUInt16(data, offset); offset += 2;
        if (length == 0 || offset + length * 2 > data.Length) return "";
        var result = Encoding.Unicode.GetString(data, offset, length * 2);
        offset += length * 2;
        return result;
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
    public int PeopleKilled { get; set; }
    public int CreaturesKilled { get; set; }
    public int DragonsSlain { get; set; }
    public int DragonSoulsAbsorbed { get; set; }
    public int WordsLearned { get; set; }
    public int BarenziahStones { get; set; }
    public int DungeonsCleared { get; set; }
    public int LocationsDiscovered { get; set; }
    public int HousesOwned { get; set; }
    public bool IsMarried { get; set; }
    public bool HasAdopted { get; set; }
    public int SpellsLearned { get; set; }
    public int BattlesWon { get; set; }
    public int BodiesLooted { get; set; }
    public int IngredientsHarvested { get; set; }
}
