using System.Text;
using System.Text.RegularExpressions;
using EMECore.Core.Models;

namespace EMECore.Hardware.Services;

public class DaysGoneSaveParser
{
    private static readonly Dictionary<string, (string Name, string Description)> AchievementMap = new()
    {
        { "dg_story_complete", ("Sobrevivente", "Complete a história principal") },
        { "dg_horde_killer", ("Caçador de Hordas", "Derrote 10 hordas") },
        { "dg_horde_slayer", ("Exterminador", "Derrote 25 hordas") },
        { "dg_skills_max", ("Mecânico", "Desbloqueie todas as habilidades da moto") },
        { "dg_camps_all", ("Todos os Acampamentos", "Desbloqueie todos os acampamentos") },
        { "dg_collectibles", ("Colecionador", "Encontre 50 coletáveis") },
        { "dg_bounties", ("Caçador de Recompensas", "Complete 50 caças") },
        { "dg_infected_kills", ("Matador de Infeccionados", "Mate 1000 infeccionados") },
        { "dg_playtime_20h", ("Veterano", "Jogue por 20 horas") },
        { "dg_playtime_50h", ("Lenda", "Jogue por 50 horas") },
    };

    private static readonly Dictionary<string, int> MaxProgressMap = new()
    {
        { "dg_horde_killer", 10 }, { "dg_horde_slayer", 25 },
        { "dg_collectibles", 50 }, { "dg_bounties", 50 },
        { "dg_infected_kills", 1000 },
        { "dg_playtime_20h", 20 }, { "dg_playtime_50h", 50 },
    };

    public string? FindSavePath()
    {
        var savedGames = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Days Gone", "SaveGames");

        if (!Directory.Exists(savedGames)) return null;

        var savFiles = Directory.GetFiles(savedGames, "slot*.sav")
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
            var buffer = File.ReadAllBytes(filePath);
            var text = Encoding.UTF8.GetString(buffer);

            var data = ExtractFromGvas(text);

            return BuildAchievements(data);
        }
        catch
        {
            return CreateDefaultAchievements();
        }
    }

    private static DaysGoneSaveData ExtractFromGvas(string text)
    {
        var data = new DaysGoneSaveData();

        var bikeSkills = Regex.Matches(text, @"Bike\w+");
        data.BikeSkillsUnlocked = bikeSkills.Count;

        var levelMatch = Regex.Match(text, @"[Ll]evel.*?(\d+)");
        if (levelMatch.Success)
            data.PlayerLevel = int.Parse(levelMatch.Groups[1].Value);

        var xpMatch = Regex.Match(text, @"[Xx][Pp].*?(\d+)");
        if (xpMatch.Success)
            data.Experience = int.Parse(xpMatch.Groups[1].Value);

        data.HordeCount = Regex.Matches(text, @"[Hh]orde").Count;
        data.CampCount = Regex.Matches(text, @"[Cc]amp").Count;
        data.MissionCount = Regex.Matches(text, @"[Mm]ission").Count;

        data.HasSave = true;
        return data;
    }

    private static List<Achievement> BuildAchievements(DaysGoneSaveData data)
    {
        var achievements = new List<Achievement>();

        achievements.Add(new Achievement
        {
            Apiname = "dg_story_complete",
            Name = AchievementMap["dg_story_complete"].Name,
            Description = AchievementMap["dg_story_complete"].Description,
            Achieved = data.MissionCount > 20
        });

        achievements.Add(new Achievement
        {
            Apiname = "dg_horde_killer",
            Name = AchievementMap["dg_horde_killer"].Name,
            Description = AchievementMap["dg_horde_killer"].Description,
            Achieved = data.HordeCount >= 10,
            Progress = Math.Min(data.HordeCount, 10),
            MaxProgress = 10
        });

        achievements.Add(new Achievement
        {
            Apiname = "dg_horde_slayer",
            Name = AchievementMap["dg_horde_slayer"].Name,
            Description = AchievementMap["dg_horde_slayer"].Description,
            Achieved = data.HordeCount >= 25,
            Progress = Math.Min(data.HordeCount, 25),
            MaxProgress = 25
        });

        achievements.Add(new Achievement
        {
            Apiname = "dg_skills_max",
            Name = AchievementMap["dg_skills_max"].Name,
            Description = AchievementMap["dg_skills_max"].Description,
            Achieved = data.BikeSkillsUnlocked >= 15,
            Progress = Math.Min(data.BikeSkillsUnlocked, 20),
            MaxProgress = 20
        });

        achievements.Add(new Achievement
        {
            Apiname = "dg_camps_all",
            Name = AchievementMap["dg_camps_all"].Name,
            Description = AchievementMap["dg_camps_all"].Description,
            Achieved = data.CampCount >= 5,
            Progress = Math.Min(data.CampCount, 5),
            MaxProgress = 5
        });

        achievements.Add(new Achievement
        {
            Apiname = "dg_playtime_20h",
            Name = AchievementMap["dg_playtime_20h"].Name,
            Description = AchievementMap["dg_playtime_20h"].Description,
            Achieved = false,
            MaxProgress = 20
        });

        achievements.Add(new Achievement
        {
            Apiname = "dg_playtime_50h",
            Name = AchievementMap["dg_playtime_50h"].Name,
            Description = AchievementMap["dg_playtime_50h"].Description,
            Achieved = false,
            MaxProgress = 50
        });

        return achievements;
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
            MaxProgress = MaxProgressMap.GetValueOrDefault(a.Key, 0)
        }).ToList();
    }
}

public class DaysGoneSaveData
{
    public bool HasSave { get; set; }
    public int PlayerLevel { get; set; }
    public int Experience { get; set; }
    public int BikeSkillsUnlocked { get; set; }
    public int HordeCount { get; set; }
    public int CampCount { get; set; }
    public int MissionCount { get; set; }
}
