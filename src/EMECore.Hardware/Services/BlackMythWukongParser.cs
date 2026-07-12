using System.Text;
using System.Text.RegularExpressions;
using EMECore.Core.Models;

namespace EMECore.Hardware.Services;

public class BlackMythWukongParser
{
    private static readonly Dictionary<string, (string Name, string Description)> AchievementMap = new()
    {
        { "bmw_chapter_1", ("Capítulo 1", "Complete o Capítulo 1") },
        { "bmw_chapter_2", ("Capítulo 2", "Complete o Capítulo 2") },
        { "bmw_chapter_3", ("Capítulo 3", "Complete o Capítulo 3") },
        { "bmw_chapter_4", ("Capítulo 4", "Complete o Capítulo 4") },
        { "bmw_chapter_5", ("Capítulo 5", "Complete o Capítulo 5") },
        { "bmw_chapter_6", ("Capítulo 6", "Complete o Capítulo 6") },
        { "bmw_chapter_all", ("A Jornada Completa", "Complete todos os 6 capítulos") },
        { "bmw_boss_10", ("Caçador de Demônios", "Derrote 10 bosses") },
        { "bmw_boss_30", ("Lenda das Montanhas", "Derrote 30 bosses") },
        { "bmw_boss_all", ("Mestre dos Macacos", "Derrote todos os bosses") },
        { "bmw_shrine_10", ("Peregrino", "Desbloqueie 10 santuários") },
        { "bmw_shrine_30", ("Caminho Espiritual", "Desbloqueie 30 santuários") },
        { "bmw_shrine_all", ("Iluminação", "Desbloqueie todos os santuários") },
        { "bmw_death_10", ("Aprendiz Dedicado", "Morra 10 vezes") },
        { "bmw_death_50", ("Espírito Incansável", "Morra 50 vezes") },
        { "bmw_death_100", ("Imortal", "Morra 100 vezes") },
        { "bmw_treasure_10", ("Caçador de Tesouros", "Encontre 10 tesouros") },
        { "bmw_treasure_30", ("Aventureiro", "Encontre 30 tesouros") },
        { "bmw_level_30", ("Discípulo", "Alcance nível 30") },
        { "bmw_level_60", ("Monge Guerreiro", "Alcance nível 60") },
        { "bmw_level_100", ("Buda", "Alcance nível 100") },
        { "bmw_spell_20", ("Mago", "Desbloqueie 20 feitiços") },
        { "bmw_spell_all", ("Arcano Supremo", "Desbloqueie todos os feitiços") },
        { "bmw_transformation_5", ("Metamorfose", "Use 5 transformações") },
        { "bmw_playtime_20h", ("Devoto", "Jogue por 20 horas") },
    };

    private static readonly Dictionary<string, int> MaxProgressMap = new()
    {
        { "bmw_chapter_all", 6 }, { "bmw_boss_10", 10 }, { "bmw_boss_30", 30 },
        { "bmw_shrine_10", 10 }, { "bmw_shrine_30", 30 },
        { "bmw_death_10", 10 }, { "bmw_death_50", 50 }, { "bmw_death_100", 100 },
        { "bmw_treasure_10", 10 }, { "bmw_treasure_30", 30 },
        { "bmw_level_30", 30 }, { "bmw_level_60", 60 }, { "bmw_level_100", 100 },
        { "bmw_spell_20", 20 }, { "bmw_transformation_5", 5 },
        { "bmw_playtime_20h", 20 },
    };

    public string? FindSavePath()
    {
        var basePath = LocalizedPaths.FindLocalAppDataSubPath("B1", Path.Combine("Saved", "SaveGames"));

        if (basePath != null)
        {
            try
            {
                foreach (var userDir in Directory.GetDirectories(basePath))
                {
                    var savFiles = Directory.GetFiles(userDir, "ArchiveSaveFile.*.sav")
                        .OrderByDescending(f => new FileInfo(f).LastWriteTime)
                        .ToArray();
                    if (savFiles.Length > 0) return savFiles[0];
                }
            }
            catch { }
        }

        var steamPaths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "BlackMythWukong", "b1", "Saved", "SaveGames"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam", "steamapps", "common", "BlackMythWukong", "b1", "Saved", "SaveGames"),
            @"D:\Steam\steamapps\common\BlackMythWukong\b1\Saved\SaveGames",
        };

        foreach (var steamPath in steamPaths)
        {
            if (!Directory.Exists(steamPath)) continue;
            try
            {
                foreach (var userDir in Directory.GetDirectories(steamPath))
                {
                    var savFiles = Directory.GetFiles(userDir, "ArchiveSaveFile.*.sav")
                        .OrderByDescending(f => new FileInfo(f).LastWriteTime)
                        .ToArray();
                    if (savFiles.Length > 0) return savFiles[0];
                }
            }
            catch { }
        }

        return null;
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
            var buffer = File.ReadAllBytes(filePath);
            var text = Encoding.UTF8.GetString(buffer);
            var achievements = new List<Achievement>();

            var chapterProgress = ExtractChapterProgress(text);
            var bossCount = ExtractBossCount(text);
            var shrineCount = ExtractShrineCount(text);
            var deathCount = ExtractDeathCount(text);
            var level = ExtractLevel(text);

            for (var i = 1; i <= 6; i++)
            {
                achievements.Add(new Achievement
                {
                    Apiname = $"bmw_chapter_{i}",
                    Name = AchievementMap[$"bmw_chapter_{i}"].Name,
                    Description = AchievementMap[$"bmw_chapter_{i}"].Description,
                    Achieved = chapterProgress >= i,
                    Progress = Math.Min(chapterProgress, i),
                    MaxProgress = 1
                });
            }

            achievements.Add(new Achievement
            {
                Apiname = "bmw_chapter_all",
                Name = AchievementMap["bmw_chapter_all"].Name,
                Description = AchievementMap["bmw_chapter_all"].Description,
                Achieved = chapterProgress >= 6,
                Progress = Math.Min(chapterProgress, 6),
                MaxProgress = 6
            });

            AddAchievement(achievements, "bmw_boss_10", bossCount);
            AddAchievement(achievements, "bmw_boss_30", bossCount);
            AddAchievement(achievements, "bmw_boss_all", bossCount);

            AddAchievement(achievements, "bmw_shrine_10", shrineCount);
            AddAchievement(achievements, "bmw_shrine_30", shrineCount);

            AddAchievement(achievements, "bmw_death_10", deathCount);
            AddAchievement(achievements, "bmw_death_50", deathCount);
            AddAchievement(achievements, "bmw_death_100", deathCount);

            AddAchievement(achievements, "bmw_level_30", level);
            AddAchievement(achievements, "bmw_level_60", level);
            AddAchievement(achievements, "bmw_level_100", level);

            var spellCount = Regex.Matches(text, @"[Ss]pell|[Ff]eitico|[Ss]kill").Count;
            AddAchievement(achievements, "bmw_spell_20", spellCount);

            var treasureCount = Regex.Matches(text, @"[Tt]reasure|[Tt]esouro").Count;
            AddAchievement(achievements, "bmw_treasure_10", treasureCount);
            AddAchievement(achievements, "bmw_treasure_30", treasureCount);

            return achievements;
        }
        catch
        {
            return CreateLockedAchievements();
        }
    }

    private static int ExtractChapterProgress(string text)
    {
        var match = Regex.Match(text, @"[Cc]hapter.*?(\d+)");
        if (match.Success) return int.Parse(match.Groups[1].Value);

        for (var i = 6; i >= 1; i--)
        {
            if (text.Contains($"Chapter{i}", StringComparison.OrdinalIgnoreCase) ||
                text.Contains($"Capitulo_{i}", StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return 0;
    }

    private static int ExtractBossCount(string text)
    {
        return Regex.Matches(text, @"[Bb]oss").Count;
    }

    private static int ExtractShrineCount(string text)
    {
        return Regex.Matches(text, @"[Ss]hrine|[Ss]antuari|[Tt]emple").Count;
    }

    private static int ExtractDeathCount(string text)
    {
        var match = Regex.Match(text, @"[Dd]eath.*?(\d+)");
        return match.Success ? int.Parse(match.Groups[1].Value) : 0;
    }

    private static int ExtractLevel(string text)
    {
        var match = Regex.Match(text, @"[Ll]evel.*?(\d+)");
        return match.Success ? int.Parse(match.Groups[1].Value) : 0;
    }

    private static void AddAchievement(List<Achievement> achievements, string key, int current)
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
