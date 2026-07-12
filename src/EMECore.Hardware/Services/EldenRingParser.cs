using System.Text;
using EMECore.Core.Models;

namespace EMECore.Hardware.Services;

public class EldenRingParser
{
    private readonly EldenRingSaveParser _saveParser = new();

    private static readonly Dictionary<string, (string Name, string Description)> AchievementMap = new()
    {
        { "elden_level_10", ("Primeiro Passo", "Alcance nível 10") },
        { "elden_level_50", ("Guerreiro Veterano", "Alcance nível 50") },
        { "elden_level_100", ("Lenda dos Tumbledown", "Alcance nível 100") },
        { "elden_level_150", ("Senhor Prístino", "Alcance nível 150") },
        { "elden_level_200", ("Deus Imperfeito", "Alcance nível 200") },
        { "elden_level_300", ("Lenda Eterna", "Alcance nível 300") },
        { "elden_rune_1m", ("Riqueza Estelar", "Acumule 1.000.000 de Runas") },
        { "elden_rune_10m", ("Cobiça Divina", "Acumule 10.000.000 de Runas") },
        { "elden_rune_100m", ("Deus do Ouro", "Acumule 100.000.000 de Runas") },
        { "elden_playtime_10h", ("Início da Jornada", "Jogue por 10 horas") },
        { "elden_playtime_50h", ("Viajante Dedicado", "Jogue por 50 horas") },
        { "elden_playtime_100h", ("Espinha de Ferro", "Jogue por 100 horas") },
        { "elden_playtime_500h", ("Vício Total", "Jogue por 500 horas") },
        { "elden_stats_vigor_50", ("Vigor Reforçado", "Alcance Vigor 50") },
        { "elden_stats_mind_50", ("Mente Iluminada", "Alcance Mind 50") },
        { "elden_stats_endurance_50", ("Resiliência Infinita", "Alcance Endurance 50") },
        { "elden_stats_strength_50", ("Força Bruta", "Alcance Strength 50") },
        { "elden_stats_dexterity_50", ("Agilidade Suprema", "Alcance Dexterity 50") },
        { "elden_stats_intelligence_50", ("Gênio Arcano", "Alcance Intelligence 50") },
        { "elden_stats_faith_50", ("Fé Inabalável", "Alcance Faith 50") },
        { "elden_stats_arcane_50", ("Mistério Profundo", "Alcance Arcane 50") },
        { "elden_all_stats_50", ("Construção Perfeita", "Todas as stats em 50+") },
        { "elden_completed", ("Platina", "Todas as conquistas desbloqueadas") },
    };

    private static readonly Dictionary<string, int> MaxProgressMap = new()
    {
        { "elden_level_10", 10 },
        { "elden_level_50", 50 },
        { "elden_level_100", 100 },
        { "elden_level_150", 150 },
        { "elden_level_200", 200 },
        { "elden_level_300", 300 },
        { "elden_rune_1m", 1000000 },
        { "elden_rune_10m", 10000000 },
        { "elden_rune_100m", 100000000 },
        { "elden_playtime_10h", 36000 },
        { "elden_playtime_50h", 180000 },
        { "elden_playtime_100h", 360000 },
        { "elden_playtime_500h", 1800000 },
        { "elden_stats_vigor_50", 50 },
        { "elden_stats_mind_50", 50 },
        { "elden_stats_endurance_50", 50 },
        { "elden_stats_strength_50", 50 },
        { "elden_stats_dexterity_50", 50 },
        { "elden_stats_intelligence_50", 50 },
        { "elden_stats_faith_50", 50 },
        { "elden_stats_arcane_50", 50 },
        { "elden_all_stats_50", 8 },
    };

    public string? FindSavePath()
    {
        var basePath = LocalizedPaths.FindAppDataSubPath("EldenRing");
        if (basePath != null)
        {
            try
            {
                foreach (var dir in Directory.GetDirectories(basePath))
                {
                    var sl2 = Path.Combine(dir, "ER0000.sl2");
                    if (File.Exists(sl2)) return sl2;

                    sl2 = Path.Combine(dir, "ER0001.sl2");
                    if (File.Exists(sl2)) return sl2;
                }
            }
            catch { }
        }

        var exeDirs = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "ELDEN RING", "Game"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam", "steamapps", "common", "ELDEN RING", "Game"),
            @"D:\Steam\steamapps\common\ELDEN RING\Game",
            @"E:\Steam\steamapps\common\ELDEN RING\Game",
        };

        foreach (var exeDir in exeDirs)
        {
            var sl2 = Path.Combine(exeDir, "ER0000.sl2");
            if (File.Exists(sl2)) return sl2;
        }

        return null;
    }

    public bool HasSave() => FindSavePath() != null;

    public EldenRingSaveParser GetSaveParser() => _saveParser;

    public List<Achievement> ParseAchievements(string? savePath = null)
    {
        var filePath = savePath ?? FindSavePath();
        if (filePath == null || !File.Exists(filePath))
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

        try
        {
            var saveData = _saveParser.ParseFromFile(filePath);
            if (saveData == null)
                return CreateDefaultAchievements();

            var achievements = new List<Achievement>();
            var bestCharacter = saveData.Slots
                .Where(s => s.IsValid)
                .OrderByDescending(s => s.Level)
                .FirstOrDefault();

            var level = saveData.MaxLevel;
            var runes = bestCharacter?.Runes ?? 0;
            var playTime = bestCharacter?.SecondsPlayed ?? 0;

            AddAchievement(achievements, "elden_level_10", level, 10);
            AddAchievement(achievements, "elden_level_50", level, 50);
            AddAchievement(achievements, "elden_level_100", level, 100);
            AddAchievement(achievements, "elden_level_150", level, 150);
            AddAchievement(achievements, "elden_level_200", level, 200);
            AddAchievement(achievements, "elden_level_300", level, 300);

            AddAchievement(achievements, "elden_rune_1m", (int)runes, 1000000);
            AddAchievement(achievements, "elden_rune_10m", (int)runes, 10000000);
            AddAchievement(achievements, "elden_rune_100m", (int)runes, 100000000);

            AddAchievement(achievements, "elden_playtime_10h", playTime, 36000);
            AddAchievement(achievements, "elden_playtime_50h", playTime, 180000);
            AddAchievement(achievements, "elden_playtime_100h", playTime, 360000);
            AddAchievement(achievements, "elden_playtime_500h", playTime, 1800000);

            if (bestCharacter != null)
            {
                AddAchievement(achievements, "elden_stats_vigor_50", (int)bestCharacter.Vigor, 50);
                AddAchievement(achievements, "elden_stats_mind_50", (int)bestCharacter.Mind, 50);
                AddAchievement(achievements, "elden_stats_endurance_50", (int)bestCharacter.Endurance, 50);
                AddAchievement(achievements, "elden_stats_strength_50", (int)bestCharacter.Strength, 50);
                AddAchievement(achievements, "elden_stats_dexterity_50", (int)bestCharacter.Dexterity, 50);
                AddAchievement(achievements, "elden_stats_intelligence_50", (int)bestCharacter.Intelligence, 50);
                AddAchievement(achievements, "elden_stats_faith_50", (int)bestCharacter.Faith, 50);
                AddAchievement(achievements, "elden_stats_arcane_50", (int)bestCharacter.Arcane, 50);

                var statsAbove50 = new[] {
                    bestCharacter.Vigor, bestCharacter.Mind, bestCharacter.Endurance,
                    bestCharacter.Strength, bestCharacter.Dexterity, bestCharacter.Intelligence,
                    bestCharacter.Faith, bestCharacter.Arcane
                }.Count(s => s >= 50);

                AddAchievement(achievements, "elden_all_stats_50", statsAbove50, 8);
            }

            var totalAchievements = achievements.Count;
            var unlockedAchievements = achievements.Count(a => a.Achieved);
            achievements.Add(new Achievement
            {
                Apiname = "elden_completed",
                Name = AchievementMap["elden_completed"].Name,
                Description = AchievementMap["elden_completed"].Description,
                Achieved = unlockedAchievements >= totalAchievements,
                Progress = unlockedAchievements,
                MaxProgress = totalAchievements
            });

            return achievements;
        }
        catch
        {
            return CreateDefaultAchievements();
        }
    }

    private List<Achievement> CreateDefaultAchievements()
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

    private static void AddAchievement(List<Achievement> achievements, string key, int current, int required)
    {
        achievements.Add(new Achievement
        {
            Apiname = key,
            Name = AchievementMap[key].Name,
            Description = AchievementMap[key].Description,
            Achieved = current >= required,
            Progress = Math.Min(current, required),
            MaxProgress = required
        });
    }
}
