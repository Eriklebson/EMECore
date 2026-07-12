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
            var jsonFiles = Directory.GetFiles(saveDir, "metadata*.json");
            if (jsonFiles.Length == 0)
                jsonFiles = Directory.GetFiles(saveDir, "*.json");
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
            var datPath = Path.Combine(dir, "sav.dat");

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
                if (doc.RootElement.TryGetProperty("Data", out var data) &&
                    data.TryGetProperty("metadata", out var meta))
                {
                    ExtractMetadata(save, meta);
                }
                else if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        save.JsonData[prop.Name] = prop.Value;
                    }
                }
            }
            catch { }

            ExtractStatsFromDat(save);
            return save;
        }
        catch { return null; }
    }

    private static void ExtractMetadata(CyberpunkSaveData save, JsonElement meta)
    {
        foreach (var prop in meta.EnumerateObject())
        {
            save.JsonData[prop.Name] = prop.Value;
        }

        if (meta.TryGetProperty("level", out var level))
            save.Level = (int)level.GetDouble();
        if (meta.TryGetProperty("streetCred", out var streetCred))
            save.StreetCred = (int)streetCred.GetDouble();
        if (meta.TryGetProperty("lifePath", out var lifePath))
            save.LifePath = lifePath.GetString() ?? "";
        if (meta.TryGetProperty("difficulty", out var difficulty))
            save.Difficulty = difficulty.GetString() ?? "";
        if (meta.TryGetProperty("playTime", out var playTime))
            save.PlayTimeSeconds = (long)playTime.GetDouble();
        if (meta.TryGetProperty("bodyGender", out var gender))
            save.Gender = gender.GetString() ?? "";
        if (meta.TryGetProperty("buildPatch", out var patch))
            save.BuildPatch = patch.GetString() ?? "";
        if (meta.TryGetProperty("timestampString", out var timestamp))
            save.Timestamp = timestamp.GetString() ?? "";
        if (meta.TryGetProperty("name", out var name))
            save.SaveName = name.GetString() ?? "";

        save.Attributes.Strength = GetDouble(meta, "strength");
        save.Attributes.Intelligence = GetDouble(meta, "intelligence");
        save.Attributes.Reflexes = GetDouble(meta, "reflexes");
        save.Attributes.TechnicalAbility = GetDouble(meta, "technicalAbility");
        save.Attributes.Cool = GetDouble(meta, "cool");

        save.Skills.Athletics = GetDouble(meta, "athletics");
        save.Skills.Stealth = GetDouble(meta, "stealth");
        save.Skills.Gunslinger = GetDouble(meta, "gunslinger");
        save.Skills.Assault = GetDouble(meta, "assault");
        save.Skills.Demolition = GetDouble(meta, "demolition");
        save.Skills.Brawling = GetDouble(meta, "brawling");
        save.Skills.ColdBlood = GetDouble(meta, "coldBlood");
        save.Skills.Engineering = GetDouble(meta, "engineering");
        save.Skills.Crafting = GetDouble(meta, "crafting");
        save.Skills.Hacking = GetDouble(meta, "hacking");
        save.Skills.CombatHacking = GetDouble(meta, "combatHacking");

        if (meta.TryGetProperty("finishedQuests", out var quests))
        {
            var questStr = quests.GetString() ?? "";
            save.FinishedQuests = questStr.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        save.IsModded = GetBool(meta, "isModded");
        save.IsEndGameSave = GetBool(meta, "isEndGameSave");

        if (meta.TryGetProperty("playerPosition", out var pos))
        {
            save.PlayerPosition = new Position
            {
                X = GetDouble(pos, "X"),
                Y = GetDouble(pos, "Y"),
                Z = GetDouble(pos, "Z")
            };
        }
    }

    private static double GetDouble(JsonElement parent, string name)
    {
        return parent.TryGetProperty(name, out var val) && val.ValueKind == JsonValueKind.Number
            ? val.GetDouble() : 0;
    }

    private static bool GetBool(JsonElement parent, string name)
    {
        return parent.TryGetProperty(name, out var val) && val.ValueKind == JsonValueKind.True;
    }

    private void ExtractStatsFromDat(CyberpunkSaveData save)
    {
        if (save.DatBytes == null || save.DatBytes.Length < 100) return;

        var text = Encoding.UTF8.GetString(save.DatBytes);

        var statKeywords = new[]
        {
            "Kill", "quest", "hack", "level", "perk", "skill",
            "cyberware", "weapon", "armor", "eddi", "side", "gig"
        };

        foreach (var keyword in statKeywords)
        {
            var count = CountOccurrences(text, keyword);
            if (count > 0)
                save.StatsData[$"dat_{keyword.ToLowerInvariant()}"] = count;
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

    public string? FindSavePath()
    {
        var savedGames = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Saved Games", "CD Projekt Red", "Cyberpunk 2077");

        if (!Directory.Exists(savedGames)) return null;

        var saveDirs = Directory.GetDirectories(savedGames)
            .OrderByDescending(d => Directory.GetLastWriteTime(d));

        foreach (var dir in saveDirs)
        {
            var jsonFiles = Directory.GetFiles(dir, "metadata*.json");
            if (jsonFiles.Length > 0)
            {
                var latest = jsonFiles.OrderByDescending(f => File.GetLastWriteTime(f)).First();
                return latest;
            }
        }

        return null;
    }

    public List<Achievement> ParseAchievements(string? savePath = null)
    {
        var filePath = savePath ?? FindSavePath();
        if (filePath == null || !File.Exists(filePath))
            return CreateDefaultAchievements();

        var data = ParseFromFile(filePath);
        if (data == null) return CreateDefaultAchievements();

        return BuildAchievements(data);
    }

    private static List<Achievement> BuildAchievements(CyberpunkSaveData save)
    {
        var achievements = new List<Achievement>();

        achievements.Add(new Achievement
        {
            Apiname = "cp_level_50",
            Name = "Lenda de Night City",
            Description = "Alcance o nível 50",
            Achieved = save.Level >= 50,
            Progress = save.Level,
            MaxProgress = 50
        });

        achievements.Add(new Achievement
        {
            Apiname = "cp_streetcred_50",
            Name = "Reconhecido",
            Description = "Alcance Street Cred 50",
            Achieved = save.StreetCred >= 50,
            Progress = save.StreetCred,
            MaxProgress = 50
        });

        var mainQuests = save.FinishedQuests.Count(q => q.StartsWith("mq"));
        achievements.Add(new Achievement
        {
            Apiname = "cp_main_quest",
            Name = "Caminho dos Mercenários",
            Description = "Complete as quests principais",
            Achieved = mainQuests >= 10,
            Progress = mainQuests,
            MaxProgress = 20
        });

        var sideQuests = save.FinishedQuests.Count(q => q.StartsWith("sq"));
        achievements.Add(new Achievement
        {
            Apiname = "cp_side_quests",
            Name = "Mercenário Completo",
            Description = "Complete 30 side quests",
            Achieved = sideQuests >= 30,
            Progress = sideQuests,
            MaxProgress = 50
        });

        var totalQuests = save.FinishedQuests.Count;
        achievements.Add(new Achievement
        {
            Apiname = "cp_all_quests",
            Name = "Fantasma de Night City",
            Description = "Complete 100 quests no total",
            Achieved = totalQuests >= 100,
            Progress = totalQuests,
            MaxProgress = 100
        });

        achievements.Add(new Achievement
        {
            Apiname = "cp_max_stats",
            Name = "Cromado",
            Description = "Maximize todas as 5 atributos",
            Achieved = save.Attributes.Strength >= 20 &&
                       save.Attributes.Intelligence >= 20 &&
                       save.Attributes.Reflexes >= 20 &&
                       save.Attributes.TechnicalAbility >= 20 &&
                       save.Attributes.Cool >= 20,
            Progress = new[] { save.Attributes.Strength, save.Attributes.Intelligence,
                             save.Attributes.Reflexes, save.Attributes.TechnicalAbility,
                             save.Attributes.Cool }.Count(a => a >= 20),
            MaxProgress = 5
        });

        achievements.Add(new Achievement
        {
            Apiname = "cp_nomad",
            Name = "Filho da Areia",
            Description = "Jogue como Nomad",
            Achieved = save.LifePath == "Nomad"
        });

        achievements.Add(new Achievement
        {
            Apiname = "cp_endgame",
            Name = "Fim de Linha",
            Description = "Chegue ao ponto de não retorno",
            Achieved = save.IsEndGameSave
        });

        achievements.Add(new Achievement
        {
            Apiname = "cp_playtime_50h",
            Name = "Veterano",
            Description = "Jogue por 50 horas",
            Achieved = save.PlayTimeSeconds >= 180000,
            Progress = (int)(save.PlayTimeSeconds / 3600),
            MaxProgress = 50
        });

        return achievements;
    }

    private static List<Achievement> CreateDefaultAchievements()
    {
        return new List<Achievement>
        {
            new() { Apiname = "cp_level_50", Name = "Lenda de Night City", Description = "Alcance o nível 50", Achieved = false, MaxProgress = 50 },
            new() { Apiname = "cp_streetcred_50", Name = "Reconhecido", Description = "Alcance Street Cred 50", Achieved = false, MaxProgress = 50 },
            new() { Apiname = "cp_main_quest", Name = "Caminho dos Mercenários", Description = "Complete as quests principais", Achieved = false, MaxProgress = 20 },
            new() { Apiname = "cp_side_quests", Name = "Mercenário Completo", Description = "Complete 30 side quests", Achieved = false, MaxProgress = 50 },
            new() { Apiname = "cp_all_quests", Name = "Fantasma de Night City", Description = "Complete 100 quests", Achieved = false, MaxProgress = 100 },
            new() { Apiname = "cp_max_stats", Name = "Cromado", Description = "Maximize todos os atributos", Achieved = false, MaxProgress = 5 },
            new() { Apiname = "cp_nomad", Name = "Filho da Areia", Description = "Jogue como Nomad", Achieved = false },
            new() { Apiname = "cp_endgame", Name = "Fim de Linha", Description = "Chegue ao ponto de não retorno", Achieved = false },
            new() { Apiname = "cp_playtime_50h", Name = "Veterano", Description = "Jogue por 50 horas", Achieved = false, MaxProgress = 50 },
        };
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

    public int Level { get; set; }
    public int StreetCred { get; set; }
    public string LifePath { get; set; } = "";
    public string Difficulty { get; set; } = "";
    public long PlayTimeSeconds { get; set; }
    public string Gender { get; set; } = "";
    public string BuildPatch { get; set; } = "";
    public string Timestamp { get; set; } = "";
    public string SaveName { get; set; } = "";
    public bool IsModded { get; set; }
    public bool IsEndGameSave { get; set; }
    public List<string> FinishedQuests { get; set; } = new();
    public Position PlayerPosition { get; set; } = new();
    public CyberpunkAttributes Attributes { get; set; } = new();
    public CyberpunkSkills Skills { get; set; } = new();
}

public class Position
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
}

public class CyberpunkAttributes
{
    public double Strength { get; set; }
    public double Intelligence { get; set; }
    public double Reflexes { get; set; }
    public double TechnicalAbility { get; set; }
    public double Cool { get; set; }
}

public class CyberpunkSkills
{
    public double Athletics { get; set; }
    public double Stealth { get; set; }
    public double Gunslinger { get; set; }
    public double Assault { get; set; }
    public double Demolition { get; set; }
    public double Brawling { get; set; }
    public double ColdBlood { get; set; }
    public double Engineering { get; set; }
    public double Crafting { get; set; }
    public double Hacking { get; set; }
    public double CombatHacking { get; set; }
}
