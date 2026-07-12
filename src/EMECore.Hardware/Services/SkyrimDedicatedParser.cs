using System.Text.RegularExpressions;
using EMECore.Core.Models;

namespace EMECore.Hardware.Services;

public class SkyrimDedicatedParser
{
    private readonly SkyrimSaveParser _parser = new();

    private static readonly Dictionary<string, (string Name, string Description)> AchievementMap = new()
    {
        { "skyrim_level_10", ("Primeiro Passo", "Alcance nível 10") },
        { "skyrim_level_25", ("Aprendiz", "Alcance nível 25") },
        { "skyrim_level_50", ("Veterano", "Alcance nível 50") },
        { "skyrim_level_81", ("Lenda Viva", "Alcance nível 81 (máximo sem Legendary)") },
        { "skyrim_kills_100", ("Caçador de Sangue", "Elimine 100 inimigos") },
        { "skyrim_kills_500", ("Carniceiro", "Elimine 500 inimigos") },
        { "skyrim_kills_1000", ("Matador de Massacre", "Elimine 1000 inimigos") },
        { "skyrim_kills_5000", ("Destruidor de Mundos", "Elimine 5000 inimigos") },
        { "skyrim_dragon_1", ("Primeiro Dragão", "Mate seu primeiro dragão") },
        { "skyrim_dragon_10", ("Caçador de Dragões", "Mate 10 dragões") },
        { "skyrim_dragon_50", ("Devorador de Dragões", "Mate 50 dragões") },
        { "skyrim_word_10", ("Palavras de Poder", "Aprenda 10 Palavras de Poder") },
        { "skyrim_word_30", ("Voz do Dragão", "Aprenda 30 Palavras de Poder") },
        { "skyrim_dungeon_10", ("Explorador", "Limpe 10 masmorras") },
        { "skyrim_dungeon_50", ("Lendário", "Limpe 50 masmorras") },
        { "skyrim_dungeon_100", ("Conquistador", "Limpe 100 masmorras") },
        { "skyrim_location_50", ("Cartógrafo", "Descubra 50 locais") },
        { "skyrim_location_100", ("Mochileiro", "Descubra 100 locais") },
        { "skyrim_stone_24", ("Observador", "Encontre as 24 Pedras Ansei") },
        { "skyrim_barenziah_24", ("Joias da Coroa", "Encontre 24 Pedras de Barenziah") },
        { "skyrim_house_1", ("Proprietário", "Compre sua primeira casa") },
        { "skyrim_house_5", ("Magnata Imobiliário", "Compre 5 casas") },
        { "skyrim_marry", ("Casamento", "Case-se com um NPC") },
        { "skyrim_adopt", ("Adoção", "Adote uma criança") },
        { "skyrim_quest_main_5", ("Início da Jornada", "Complete 5 missões da história principal") },
        { "skyrim_quest_main_10", ("Salvador de Skyrim", "Complete 10 missões da história principal") },
        { "skyrim_quest_thieves", ("Ladrão de Guildas", "Complete a Guilda dos Ladrões") },
        { "skyrim_quest_dark", ("Membro da Corvos", "Complete a Irmandade Negra") },
        { "skyrim_quest_mages", ("Arcanista", "Complete a Colégio de Winterhold") },
        { "skyrim_quest_companions", ("Companheiro", "Complete os Companheiros") },
        { "skyrim_playtime_100h", ("Centenário", "Jogue por 100 horas") },
        { "skyrim_playtime_200h", ("Imortal", "Jogue por 200 horas") },
        { "skyrim_skill_100", ("Mestre de Habilidade", "Alcance 100 em qualquer habilidade") },
        { "skyrim_playtime_50h", ("Veterano", "Jogue por 50 horas") },
    };

    private static readonly Dictionary<string, int> MaxProgressMap = new()
    {
        { "skyrim_level_10", 10 }, { "skyrim_level_25", 25 }, { "skyrim_level_50", 50 }, { "skyrim_level_81", 81 },
        { "skyrim_kills_100", 100 }, { "skyrim_kills_500", 500 }, { "skyrim_kills_1000", 1000 }, { "skyrim_kills_5000", 5000 },
        { "skyrim_dragon_1", 1 }, { "skyrim_dragon_10", 10 }, { "skyrim_dragon_50", 50 },
        { "skyrim_word_10", 10 }, { "skyrim_word_30", 30 },
        { "skyrim_dungeon_10", 10 }, { "skyrim_dungeon_50", 50 }, { "skyrim_dungeon_100", 100 },
        { "skyrim_location_50", 50 }, { "skyrim_location_100", 100 },
        { "skyrim_stone_24", 24 }, { "skyrim_barenziah_24", 24 },
        { "skyrim_house_5", 5 }, { "skyrim_quest_main_5", 5 }, { "skyrim_quest_main_10", 10 },
        { "skyrim_playtime_100h", 100 }, { "skyrim_playtime_200h", 200 }, { "skyrim_playtime_50h", 50 },
    };

    public string? FindSavePath()
    {
        var saveDir = LocalizedPaths.FindMyGamesSubPath("Skyrim Special Edition", "Saves")
                   ?? LocalizedPaths.FindMyGamesSubPath("Skyrim", "Saves")
                   ?? LocalizedPaths.FindMyGamesSubPath("Skyrim VR", "Saves");

        if (saveDir == null)
        {
            saveDir = LocalizedPaths.FindLocalAppDataSubPath("Skyrim", "Saves")
                   ?? LocalizedPaths.FindAppDataSubPath("Skyrim", "Saves");
        }

        if (saveDir == null) return null;

        string? latestSave = null;
        var latestTime = DateTime.MinValue;

        try
        {
            foreach (var file in Directory.GetFiles(saveDir, "*.ess"))
            {
                var info = new FileInfo(file);
                if (info.LastWriteTime > latestTime && info.Length > 1000)
                {
                    latestTime = info.LastWriteTime;
                    latestSave = file;
                }
            }
        }
        catch { }

        return latestSave;
    }

    public bool HasSave() => FindSavePath() != null;

    public List<Achievement> ParseAchievements(string? savePath = null)
    {
        var filePath = savePath ?? FindSavePath();
        if (filePath == null || !File.Exists(filePath))
        {
            return CreateLockedAchievements();
        }

        try
        {
            var saveData = _parser.ParseFromFile(filePath);
            if (saveData == null) return CreateLockedAchievements();

            var achievements = new List<Achievement>();
            var level = saveData.Level;
            var kills = saveData.TotalKills;
            var dragons = saveData.DragonsSlain;
            var words = saveData.WordsLearned;
            var dungeons = saveData.DungeonsCleared;
            var locations = saveData.LocationsDiscovered;
            var barenziah = saveData.BarenziahStones;
            var houses = saveData.HousesOwned;
            var married = saveData.IsMarried;
            var adopted = saveData.HasAdopted;
            var playHours = saveData.PlayTimeHours;

            AddLevel(achievements, level);
            AddKills(achievements, kills);
            AddDragons(achievements, dragons);
            AddWords(achievements, words);
            AddDungeons(achievements, dungeons);
            AddLocations(achievements, locations);

            achievements.Add(new Achievement
            {
                Apiname = "skyrim_stone_24",
                Name = AchievementMap["skyrim_stone_24"].Name,
                Description = AchievementMap["skyrim_stone_24"].Description,
                Achieved = barenziah >= 24,
                Progress = Math.Min(barenziah, 24),
                MaxProgress = 24
            });

            achievements.Add(new Achievement
            {
                Apiname = "skyrim_barenziah_24",
                Name = AchievementMap["skyrim_barenziah_24"].Name,
                Description = AchievementMap["skyrim_barenziah_24"].Description,
                Achieved = barenziah >= 24,
                Progress = Math.Min(barenziah, 24),
                MaxProgress = 24
            });

            achievements.Add(new Achievement
            {
                Apiname = "skyrim_house_1",
                Name = AchievementMap["skyrim_house_1"].Name,
                Description = AchievementMap["skyrim_house_1"].Description,
                Achieved = houses >= 1,
                Progress = Math.Min(houses, 1),
                MaxProgress = 1
            });

            achievements.Add(new Achievement
            {
                Apiname = "skyrim_house_5",
                Name = AchievementMap["skyrim_house_5"].Name,
                Description = AchievementMap["skyrim_house_5"].Description,
                Achieved = houses >= 5,
                Progress = Math.Min(houses, 5),
                MaxProgress = 5
            });

            achievements.Add(new Achievement
            {
                Apiname = "skyrim_marry",
                Name = AchievementMap["skyrim_marry"].Name,
                Description = AchievementMap["skyrim_marry"].Description,
                Achieved = married,
                Progress = married ? 1 : 0,
                MaxProgress = 1
            });

            achievements.Add(new Achievement
            {
                Apiname = "skyrim_adopt",
                Name = AchievementMap["skyrim_adopt"].Name,
                Description = AchievementMap["skyrim_adopt"].Description,
                Achieved = adopted,
                Progress = adopted ? 1 : 0,
                MaxProgress = 1
            });

            var mainQuests = saveData.QuestData.Count(q => q.Key.StartsWith("quest_MQ"));
            AddGeneric(achievements, "skyrim_quest_main_5", mainQuests);
            AddGeneric(achievements, "skyrim_quest_main_10", mainQuests);

            achievements.Add(new Achievement
            {
                Apiname = "skyrim_quest_thieves",
                Name = AchievementMap["skyrim_quest_thieves"].Name,
                Description = AchievementMap["skyrim_quest_thieves"].Description,
                Achieved = saveData.QuestData.Keys.Any(q => q.StartsWith("quest_TG")),
                Progress = saveData.QuestData.Keys.Any(q => q.StartsWith("quest_TG")) ? 1 : 0,
                MaxProgress = 1
            });

            achievements.Add(new Achievement
            {
                Apiname = "skyrim_quest_dark",
                Name = AchievementMap["skyrim_quest_dark"].Name,
                Description = AchievementMap["skyrim_quest_dark"].Description,
                Achieved = saveData.QuestData.Keys.Any(q => q.StartsWith("quest_DB")),
                Progress = saveData.QuestData.Keys.Any(q => q.StartsWith("quest_DB")) ? 1 : 0,
                MaxProgress = 1
            });

            achievements.Add(new Achievement
            {
                Apiname = "skyrim_quest_mages",
                Name = AchievementMap["skyrim_quest_mages"].Name,
                Description = AchievementMap["skyrim_quest_mages"].Description,
                Achieved = saveData.QuestData.Keys.Any(q => q.StartsWith("quest_MG")),
                Progress = saveData.QuestData.Keys.Any(q => q.StartsWith("quest_MG")) ? 1 : 0,
                MaxProgress = 1
            });

            achievements.Add(new Achievement
            {
                Apiname = "skyrim_quest_companions",
                Name = AchievementMap["skyrim_quest_companions"].Name,
                Description = AchievementMap["skyrim_quest_companions"].Description,
                Achieved = saveData.QuestData.Keys.Any(q => q.StartsWith("quest_C")),
                Progress = saveData.QuestData.Keys.Any(q => q.StartsWith("quest_C")) ? 1 : 0,
                MaxProgress = 1
            });

            AddGeneric(achievements, "skyrim_playtime_50h", playHours);
            AddGeneric(achievements, "skyrim_playtime_100h", playHours);
            AddGeneric(achievements, "skyrim_playtime_200h", playHours);

            var hasSkill100 = saveData.SkillData.Values.Any(v => v >= 100);
            achievements.Add(new Achievement
            {
                Apiname = "skyrim_skill_100",
                Name = AchievementMap["skyrim_skill_100"].Name,
                Description = AchievementMap["skyrim_skill_100"].Description,
                Achieved = hasSkill100,
                Progress = hasSkill100 ? 1 : 0,
                MaxProgress = 1
            });

            return achievements;
        }
        catch
        {
            return CreateLockedAchievements();
        }
    }

    private static void AddLevel(List<Achievement> achievements, int level)
    {
        foreach (var kvp in AchievementMap.Where(a => a.Key.StartsWith("skyrim_level_")))
        {
            var req = MaxProgressMap[kvp.Key];
            achievements.Add(new Achievement
            {
                Apiname = kvp.Key,
                Name = kvp.Value.Name,
                Description = kvp.Value.Description,
                Achieved = level >= req,
                Progress = Math.Min(level, req),
                MaxProgress = req
            });
        }
    }

    private static void AddKills(List<Achievement> achievements, int kills)
    {
        foreach (var kvp in AchievementMap.Where(a => a.Key.StartsWith("skyrim_kills_")))
        {
            var req = MaxProgressMap[kvp.Key];
            achievements.Add(new Achievement
            {
                Apiname = kvp.Key,
                Name = kvp.Value.Name,
                Description = kvp.Value.Description,
                Achieved = kills >= req,
                Progress = Math.Min(kills, req),
                MaxProgress = req
            });
        }
    }

    private static void AddDragons(List<Achievement> achievements, int dragons)
    {
        foreach (var kvp in AchievementMap.Where(a => a.Key.StartsWith("skyrim_dragon_")))
        {
            var req = MaxProgressMap[kvp.Key];
            achievements.Add(new Achievement
            {
                Apiname = kvp.Key,
                Name = kvp.Value.Name,
                Description = kvp.Value.Description,
                Achieved = dragons >= req,
                Progress = Math.Min(dragons, req),
                MaxProgress = req
            });
        }
    }

    private static void AddWords(List<Achievement> achievements, int words)
    {
        foreach (var kvp in AchievementMap.Where(a => a.Key.StartsWith("skyrim_word_")))
        {
            var req = MaxProgressMap[kvp.Key];
            achievements.Add(new Achievement
            {
                Apiname = kvp.Key,
                Name = kvp.Value.Name,
                Description = kvp.Value.Description,
                Achieved = words >= req,
                Progress = Math.Min(words, req),
                MaxProgress = req
            });
        }
    }

    private static void AddDungeons(List<Achievement> achievements, int dungeons)
    {
        foreach (var kvp in AchievementMap.Where(a => a.Key.StartsWith("skyrim_dungeon_")))
        {
            var req = MaxProgressMap[kvp.Key];
            achievements.Add(new Achievement
            {
                Apiname = kvp.Key,
                Name = kvp.Value.Name,
                Description = kvp.Value.Description,
                Achieved = dungeons >= req,
                Progress = Math.Min(dungeons, req),
                MaxProgress = req
            });
        }
    }

    private static void AddLocations(List<Achievement> achievements, int locations)
    {
        foreach (var kvp in AchievementMap.Where(a => a.Key.StartsWith("skyrim_location_")))
        {
            var req = MaxProgressMap[kvp.Key];
            achievements.Add(new Achievement
            {
                Apiname = kvp.Key,
                Name = kvp.Value.Name,
                Description = kvp.Value.Description,
                Achieved = locations >= req,
                Progress = Math.Min(locations, req),
                MaxProgress = req
            });
        }
    }

    private static void AddGeneric(List<Achievement> achievements, string key, int current)
    {
        if (!AchievementMap.ContainsKey(key)) return;
        var req = MaxProgressMap.GetValueOrDefault(key, 1);
        achievements.Add(new Achievement
        {
            Apiname = key,
            Name = AchievementMap[key].Name,
            Description = AchievementMap[key].Description,
            Achieved = current >= req,
            Progress = Math.Min(current, req),
            MaxProgress = req
        });
    }

    private List<Achievement> CreateLockedAchievements()
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
