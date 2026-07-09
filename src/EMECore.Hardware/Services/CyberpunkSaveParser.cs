using System.Text;
using System.Text.Json;
using EMECore.Core.Models;

namespace EMECore.Hardware.Services;

public class CyberpunkSaveParser
{
    public CyberpunkSaveData? ParseFromDirectory(string saveDir)
    {
        try
        {
            var jsonFiles = Directory.GetFiles(saveDir, "*.json");
            if (jsonFiles.Length == 0) return null;

            var latestJson = jsonFiles.OrderByDescending(f => File.GetLastWriteTime(f)).First();
            return ParseFromFile(latestJson);
        }
        catch { return null; }
    }

    public CyberpunkSaveData? ParseFromFile(string jsonPath)
    {
        try
        {
            var json = File.ReadAllText(jsonPath);
            var dir = Path.GetDirectoryName(jsonPath) ?? "";
            var datPath = Path.ChangeExtension(jsonPath, ".dat");

            var save = new CyberpunkSaveData
            {
                FileName = Path.GetFileName(jsonPath),
                JsonPath = jsonPath
            };

            if (File.Exists(datPath))
            {
                save.DatBytes = File.ReadAllBytes(datPath);
                save.DatSize = save.DatBytes.Length;
            }

            try
            {
                var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        save.JsonData[prop.Name] = prop.Value;
                    }
                }
            }
            catch { }

            ExtractStatsFromJson(save);
            ExtractStatsFromDat(save);

            return save;
        }
        catch { return null; }
    }

    private void ExtractStatsFromJson(CyberpunkSaveData save)
    {
        save.StatsData = new Dictionary<string, long>();
        save.QuestData = new Dictionary<string, bool>();

        foreach (var kvp in save.JsonData)
        {
            var key = kvp.Key.ToLowerInvariant();
            var value = kvp.Value;

            if (value.ValueKind == JsonValueKind.Number)
            {
                save.StatsData[$"json_{key}"] = value.GetInt64();
            }
            else if (value.ValueKind == JsonValueKind.String)
            {
                var str = value.GetString() ?? "";
                if (!string.IsNullOrEmpty(str))
                    save.StatsData[$"json_{key}_str"] = str.Length;
            }
            else if (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False)
            {
                save.QuestData[$"json_{key}"] = value.GetBoolean();
            }
        }
    }

    private void ExtractStatsFromDat(CyberpunkSaveData save)
    {
        if (save.DatBytes == null || save.DatBytes.Length < 100) return;

        var text = Encoding.UTF8.GetString(save.DatBytes);

        var statKeywords = new[]
        {
            "Kill", "kill", "quest", "Quest", "hack", "Hack",
            "level", "Level", "perk", "Perk", "skill", "Skill",
            "cyberware", "Cyberware", "weapon", "Weapon", "armor", "Armor",
            "eddi", "Eddi", "side", "Side", "gig", "Gig"
        };

        foreach (var keyword in statKeywords)
        {
            var count = CountOccurrences(text, keyword);
            if (count > 0)
                save.StatsData[$"dat_{keyword}"] = count;
        }
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

public class CyberpunkSaveData
{
    public string FileName { get; set; } = "";
    public string JsonPath { get; set; } = "";
    public byte[]? DatBytes { get; set; }
    public long DatSize { get; set; }
    public Dictionary<string, JsonElement> JsonData { get; set; } = new();
    public Dictionary<string, long> StatsData { get; set; } = new();
    public Dictionary<string, bool> QuestData { get; set; } = new();
}
