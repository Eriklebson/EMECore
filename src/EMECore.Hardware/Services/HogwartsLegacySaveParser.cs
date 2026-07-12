using System.Text;
using EMECore.Core.Models;

namespace EMECore.Hardware.Services;

public class HogwartsLegacySaveParser
{
    private static readonly byte[] GVAS_MAGIC = Encoding.ASCII.GetBytes("GVAS");

    private static readonly Dictionary<string, (string Name, string Description)> AchievementMap = new()
    {
        { "hl_story_complete", ("Bruxo do Ano", "Complete a história principal") },
        { "hl_housecup", ("Taça das Casas", "Vença a Taça das Casas") },
        { "hl_merlin_30", ("Desbravador", "Complete 30 desafios Merlin") },
        { "hl_merlin_60", ("Explorador", "Complete 60 desafios Merlin") },
        { "hl_merlin_all", ("Mestre Explorador", "Complete todos os desafios Merlin") },
        { "hl_level_40", ("Nível Máximo", "Alcance o nível 40") },
        { "hl_spells_all", ("Colecionador de Feitiços", "Desbloqueie todos os feitiços") },
        { "hl_collection_100", ("Colecionador Completo", "Alcance 100% de coletáveis") },
        { "hl_house_slytherin", ("Serpentis", "Seja colocada em Sonserina") },
        { "hl_house_gryffindor", ("Grifinória", "Seja colocada em Grifinória") },
        { "hl_house_ravenclaw", ("Corvinal", "Seja colocada em Corvinal") },
        { "hl_house_hufflepuff", ("Lufa-Lufa", "Seja colocada em Lufa-Lufa") },
        { "hl_playtime_30h", ("Aprendiz Dedicado", "Jogue por 30 horas") },
        { "hl_playtime_60h", ("Mestre Feiticeiro", "Jogue por 60 horas") },
    };

    public string? FindSavePath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var hlPath = Path.Combine(localAppData, "Hogwarts Legacy", "Saved", "SaveGames");

        if (!Directory.Exists(hlPath)) return null;

        var savFiles = Directory.GetFiles(hlPath, "*.sav", SearchOption.AllDirectories)
            .Where(f => !f.Contains("UserSettingSaveGame") && !f.Contains("ArchiveSaveFile"))
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
            "trial", "Trial", "Merlin", "merlin", "Conjuration"
        };

        foreach (var keyword in statKeywords)
        {
            var count = CountOccurrences(text, keyword);
            if (count > 0)
                save.StatsData[$"stat_{keyword}"] = count;
        }

        var housePatterns = new[] { "Slytherin", "Gryffindor", "Ravenclaw", "Hufflepuff", "Sonserina", "Grifinória", "Corvinal", "Lufa-Lufa" };
        foreach (var house in housePatterns)
        {
            if (text.Contains(house, StringComparison.OrdinalIgnoreCase))
                save.House = house;
        }

        var levelMatch = System.Text.RegularExpressions.Regex.Match(text, @"[Ll]evel.*?(\d+)");
        if (levelMatch.Success)
            save.PlayerLevel = int.Parse(levelMatch.Groups[1].Value);

        var merlinCount = System.Text.RegularExpressions.Regex.Matches(text, @"[Mm]erlin").Count;
        save.MerlinChallenges = merlinCount;

        var questCount = System.Text.RegularExpressions.Regex.Matches(text, @"[Qq]uest").Count;
        save.QuestCount = questCount;

        return save;
    }

    private static List<Achievement> BuildAchievements(HogwartsLegacySaveData data)
    {
        var achievements = new List<Achievement>();

        achievements.Add(new Achievement
        {
            Apiname = "hl_story_complete",
            Name = AchievementMap["hl_story_complete"].Name,
            Description = AchievementMap["hl_story_complete"].Description,
            Achieved = data.QuestCount > 50
        });

        achievements.Add(new Achievement
        {
            Apiname = "hl_merlin_30",
            Name = AchievementMap["hl_merlin_30"].Name,
            Description = AchievementMap["hl_merlin_30"].Description,
            Achieved = data.MerlinChallenges >= 30,
            Progress = Math.Min(data.MerlinChallenges, 30),
            MaxProgress = 30
        });

        achievements.Add(new Achievement
        {
            Apiname = "hl_merlin_60",
            Name = AchievementMap["hl_merlin_60"].Name,
            Description = AchievementMap["hl_merlin_60"].Description,
            Achieved = data.MerlinChallenges >= 60,
            Progress = Math.Min(data.MerlinChallenges, 60),
            MaxProgress = 60
        });

        achievements.Add(new Achievement
        {
            Apiname = "hl_merlin_all",
            Name = AchievementMap["hl_merlin_all"].Name,
            Description = AchievementMap["hl_merlin_all"].Description,
            Achieved = data.MerlinChallenges >= 95,
            Progress = Math.Min(data.MerlinChallenges, 95),
            MaxProgress = 95
        });

        achievements.Add(new Achievement
        {
            Apiname = "hl_level_40",
            Name = AchievementMap["hl_level_40"].Name,
            Description = AchievementMap["hl_level_40"].Description,
            Achieved = data.PlayerLevel >= 40,
            Progress = data.PlayerLevel,
            MaxProgress = 40
        });

        achievements.Add(new Achievement
        {
            Apiname = "hl_playtime_30h",
            Name = AchievementMap["hl_playtime_30h"].Name,
            Description = AchievementMap["hl_playtime_30h"].Description,
            Achieved = false,
            MaxProgress = 30
        });

        return achievements;
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

public class HogwartsLegacySaveData
{
    public string FileName { get; set; } = "";
    public long FileSize { get; set; }
    public byte[]? RawBytes { get; set; }
    public bool IsCompressed { get; set; }
    public List<string> ExtractedStrings { get; set; } = new();
    public Dictionary<string, bool> QuestData { get; set; } = new();
    public Dictionary<string, long> StatsData { get; set; } = new();
    public string House { get; set; } = "";
    public int PlayerLevel { get; set; }
    public int MerlinChallenges { get; set; }
    public int QuestCount { get; set; }
}
