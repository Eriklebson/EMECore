using System.Text;
using EMECore.Core.Models;

namespace EMECore.Hardware.Services;

public class Witcher3SaveParser
{
    private static readonly byte[] MAGIC = Encoding.ASCII.GetBytes("SNFHFZLC");

    private static readonly Dictionary<string, (string Name, string Description)> AchievementMap = new()
    {
        { "w3_story_complete", ("O Branco Bruxo", "Complete a história principal") },
        { "w3_blood_and_wine", ("Sangue e Vinho", "Complete a expansão Sangue e Vinho") },
        { "w3_hearts_of_stone", ("Corações de Pedra", "Complete a expansão Corações de Pedra") },
        { "w3_level_50", ("Veterano", "Alcance o nível 50") },
        { "w3_monster_kills_500", ("Caçador de Monstros", "Mate 500 monstros") },
        { "w3 Contracts_20", ("Caçador de Contratos", "Complete 20 contratos") },
        { "w3_gwent_30", ("Mestre do Gwent", "Vença 30 partidas de Gwent") },
        { "w3_collect_100", ("Colecionador", "Colete 100 itens de runas") },
        { "w3_playtime_50h", ("Lenda", "Jogue por 50 horas") },
        { "w3_playtime_100h", ("Imortal", "Jogue por 100 horas") },
    };

    public string? FindSavePath()
    {
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var witcherPath = Path.Combine(documents, "The Witcher 3", "gamesaves");

        if (!Directory.Exists(witcherPath)) return null;

        var savFiles = Directory.GetFiles(witcherPath, "*.sav")
            .OrderByDescending(f => new FileInfo(f).LastWriteTime)
            .ToArray();

        return savFiles.Length > 0 ? savFiles[0] : null;
    }

    public bool HasSave() => FindSavePath() != null;

    public List<Achievement> ParseAchievements(string? savePath = null)
    {
        var filePath = savePath ?? FindSavePath();
        if (filePath == null || !File.Exists(filePath))
            return CreateDefaultAchievements();

        try
        {
            var data = File.ReadAllBytes(filePath);
            var saveData = ParseFromBytes(data, Path.GetFileName(filePath));
            if (saveData == null) return CreateDefaultAchievements();

            return BuildAchievements(saveData);
        }
        catch
        {
            return CreateDefaultAchievements();
        }
    }

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
            "Geralt", "geralt", "Ciri", "ciri", "hors", "Hors",
            "Gwent", "gwent", "contract", "Contract", "bounty", "Bounty"
        };

        foreach (var keyword in statKeywords)
        {
            var count = CountOccurrences(text, keyword);
            if (count > 0)
                save.StatsData[$"stat_{keyword}"] = count;
        }

        var questPatterns = new[] { "MQ", "SQ", "BG", "GW", "TP", "BC", "WP" };
        foreach (var pattern in questPatterns)
        {
            var count = CountOccurrences(text, pattern);
            if (count > 0)
                save.QuestData[$"quest_{pattern}"] = true;
        }

        var levelMatch = System.Text.RegularExpressions.Regex.Match(text, @"[Ll]evel.*?(\d+)");
        if (levelMatch.Success)
            save.PlayerLevel = int.Parse(levelMatch.Groups[1].Value);

        save.MonsterKills = (int)(save.StatsData.GetValueOrDefault("stat_kill", 0) + save.StatsData.GetValueOrDefault("stat_Kill", 0));
        save.GwentWins = (int)CountOccurrences(text, "Gwent");
        save.ContractsCompleted = (int)CountOccurrences(text, "contract");

        var hasBaw = text.Contains("baw", StringComparison.OrdinalIgnoreCase) || text.Contains("Blood and Wine", StringComparison.OrdinalIgnoreCase);
        var hasHos = text.Contains("hos", StringComparison.OrdinalIgnoreCase) || text.Contains("Hearts of Stone", StringComparison.OrdinalIgnoreCase);
        save.HasBloodAndWine = hasBaw;
        save.HasHeartsOfStone = hasHos;

        return save;
    }

    private static List<Achievement> BuildAchievements(Witcher3SaveData data)
    {
        var achievements = new List<Achievement>();

        achievements.Add(new Achievement
        {
            Apiname = "w3_story_complete",
            Name = AchievementMap["w3_story_complete"].Name,
            Description = AchievementMap["w3_story_complete"].Description,
            Achieved = data.QuestCount > 30
        });

        achievements.Add(new Achievement
        {
            Apiname = "w3_blood_and_wine",
            Name = AchievementMap["w3_blood_and_wine"].Name,
            Description = AchievementMap["w3_blood_and_wine"].Description,
            Achieved = data.HasBloodAndWine
        });

        achievements.Add(new Achievement
        {
            Apiname = "w3_hearts_of_stone",
            Name = AchievementMap["w3_hearts_of_stone"].Name,
            Description = AchievementMap["w3_hearts_of_stone"].Description,
            Achieved = data.HasHeartsOfStone
        });

        achievements.Add(new Achievement
        {
            Apiname = "w3_level_50",
            Name = AchievementMap["w3_level_50"].Name,
            Description = AchievementMap["w3_level_50"].Description,
            Achieved = data.PlayerLevel >= 50,
            Progress = data.PlayerLevel,
            MaxProgress = 50
        });

        achievements.Add(new Achievement
        {
            Apiname = "w3_monster_kills_500",
            Name = AchievementMap["w3_monster_kills_500"].Name,
            Description = AchievementMap["w3_monster_kills_500"].Description,
            Achieved = data.MonsterKills >= 500,
            Progress = Math.Min(data.MonsterKills, 500),
            MaxProgress = 500
        });

        achievements.Add(new Achievement
        {
            Apiname = "w3_contracts_20",
            Name = AchievementMap["w3_Contracts_20"].Name,
            Description = AchievementMap["w3_Contracts_20"].Description,
            Achieved = data.ContractsCompleted >= 20,
            Progress = Math.Min(data.ContractsCompleted, 20),
            MaxProgress = 20
        });

        achievements.Add(new Achievement
        {
            Apiname = "w3_gwent_30",
            Name = AchievementMap["w3_gwent_30"].Name,
            Description = AchievementMap["w3_gwent_30"].Description,
            Achieved = data.GwentWins >= 30,
            Progress = Math.Min(data.GwentWins, 30),
            MaxProgress = 30
        });

        achievements.Add(new Achievement
        {
            Apiname = "w3_playtime_50h",
            Name = AchievementMap["w3_playtime_50h"].Name,
            Description = AchievementMap["w3_playtime_50h"].Description,
            Achieved = false,
            MaxProgress = 50
        });

        return achievements;
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

    private static List<Achievement> CreateDefaultAchievements()
    {
        return AchievementMap.Select(a => new Achievement
        {
            Apiname = a.Key,
            Name = a.Value.Name,
            Description = a.Value.Description,
            Achieved = false,
            Progress = 0,
            MaxProgress = 0
        }).ToList();
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
    public int PlayerLevel { get; set; }
    public int MonsterKills { get; set; }
    public int GwentWins { get; set; }
    public int ContractsCompleted { get; set; }
    public int QuestCount { get; set; }
    public bool HasBloodAndWine { get; set; }
    public bool HasHeartsOfStone { get; set; }
}
