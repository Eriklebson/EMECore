using System.Text;
using System.Text.RegularExpressions;
using EMECore.Core.Models;

namespace EMECore.Hardware.Services;

public class EldenRingParser
{
    private static readonly Dictionary<string, (string Name, string Description)> AchievementMap = new()
    {
        { "elden_level_1", ("Primeiro Passo", "Alcance nível 10") },
        { "elden_level_50", ("Guerreiro Veterano", "Alcance nível 50") },
        { "elden_level_100", ("Lenda dos Tumbledown", "Alcance nível 100") },
        { "elden_level_150", ("Senhor Prístino", "Alcance nível 150") },
        { "elden_level_200", ("Deus Imperfeito", "Alcance nível 200") },
        { "elden_rune_1m", ("Riqueza Estelar", "Acumule 1.000.000 de Runas") },
        { "elden_rune_10m", ("Cobiça Divina", "Acumule 10.000.000 de Runas") },
        { "elden_death_1", ("Primeira Morte", "Morra pela primeira vez") },
        { "elden_death_100", ("Cicatriz de Guerra", "Morra 100 vezes") },
        { "elden_death_500", ("Alma Atormentada", "Morra 500 vezes") },
        { "elden_ng_plus", ("Ciclo Eterno", "Complete o jogo e inicie NG+") },
        { "elden_ng_plus_7", ("Eternidade", "Alcance NG+7") },
        { "elden_weapon_max", ("Arma Suprema", "Forje uma arma até +25") },
        { "elden_spell_learn", ("Estudioso das Artes", "Aprenda 10 feitiços") },
        { "elden_spell_all", ("Mestre Arcano", "Aprenda todos os feitiços") },
        { "elden_boss_margit", ("O Prelúdio", "Derrotar Margit, o Vexador") },
        { "elden_boss_morgott", ("Rei dos Erros", "Derrotar Morgott, o Rei dos Erros") },
        { "elden_boss_radahn", ("Conquistador dos Céus", "Derrotar Radahn Estrela Cadente") },
        { "elden_boss_malenia", ("Lâmina de Miquella", "Derrotar Malenia, a Lâmina de Prata") },
        { "elden_boss_mohg", ("Senhor de Sangue", "Derrotar Mohg, Senhor de Sangue") },
        { "elden_boss_fire_giant", ("Gigante de Fogo", "Derrotar o Gigante de Fogo") },
        { "elden_boss_horax", ("Devorador de Deuses", "Derrotar Hoarah Loux") },
        { "elden_boss_beast", ("Árvore Protetora", "Derrotar a Besta dos Devoradores") },
        { "elden_ending_elden", ("Elden Ring", "Obtenha o final da Restauração") },
        { "elden_ending_siffrin", ("Ordem dos Príncipes", "Obtenha o final da Age of Stars") },
        { "elden_ending_duskborn", ("Crepúsculo Eterno", "Obtenha o final do Lord of Frenzied Flame") },
        { "elden_legacy_dungeon", ("Explorador de Ruínas", "Complete 5 dungeons legados") },
        { "elden_dungeon_20", ("Caçador de Túneis", "Complete 20 túneis") },
        { "elden_npc_quest", ("Historiador", "Complete 5 missões de NPCs") },
        { "elden_mount_summon", ("Chamado do Torrent", "Invogue Torrent pela primeira vez") },
        { "elden_spirit_tear", ("Lágrima de Espírito", "Use 5 Lágrimas de Espírito") },
    };

    private static readonly Dictionary<string, int> MaxProgressMap = new()
    {
        { "elden_level_1", 10 },
        { "elden_level_50", 50 },
        { "elden_level_100", 100 },
        { "elden_level_150", 150 },
        { "elden_level_200", 200 },
        { "elden_rune_1m", 1000000 },
        { "elden_rune_10m", 10000000 },
        { "elden_death_1", 1 },
        { "elden_death_100", 100 },
        { "elden_death_500", 500 },
        { "elden_ng_plus", 8 },
        { "elden_ng_plus_7", 7 },
        { "elden_spell_learn", 10 },
        { "elden_spell_all", 100 },
        { "elden_legacy_dungeon", 5 },
        { "elden_dungeon_20", 20 },
        { "elden_npc_quest", 5 },
        { "elden_spirit_tear", 5 },
    };

    public string? FindSavePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var paths = new[]
        {
            Path.Combine(appData, "EldenRing"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "Roaming", "EldenRing")
        };

        foreach (var basePath in paths)
        {
            if (!Directory.Exists(basePath)) continue;
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
            var buffer = File.ReadAllBytes(filePath);
            var text = Encoding.ASCII.GetString(buffer);
            var achievements = new List<Achievement>();

            var level = ExtractLevel(buffer, text);
            var runes = ExtractRunes(buffer, text);
            var deaths = ExtractDeaths(buffer, text);
            var ngPlus = ExtractNgPlus(buffer, text);
            var spellCount = ExtractSpellCount(text);

            AddAchievement(achievements, "elden_level_1", level, 10);
            AddAchievement(achievements, "elden_level_50", level, 50);
            AddAchievement(achievements, "elden_level_100", level, 100);
            AddAchievement(achievements, "elden_level_150", level, 150);
            AddAchievement(achievements, "elden_level_200", level, 200);
            AddAchievement(achievements, "elden_rune_1m", runes, 1000000);
            AddAchievement(achievements, "elden_rune_10m", runes, 10000000);
            AddAchievement(achievements, "elden_death_1", deaths, 1);
            AddAchievement(achievements, "elden_death_100", deaths, 100);
            AddAchievement(achievements, "elden_death_500", deaths, 500);
            AddAchievement(achievements, "elden_ng_plus", ngPlus, 1);
            AddAchievement(achievements, "elden_ng_plus_7", ngPlus, 7);
            AddAchievement(achievements, "elden_spell_learn", spellCount, 10);
            AddAchievement(achievements, "elden_spell_all", spellCount, 100);

            var bossPatterns = new Dictionary<string, string[]>
            {
                { "elden_boss_margit", new[] { "margit", "Margit" } },
                { "elden_boss_morgott", new[] { "morgott", "Morgott" } },
                { "elden_boss_radahn", new[] { "radahn", "Radahn", "Starscourge" } },
                { "elden_boss_malenia", new[] { "malenia", "Malenia", "Blade of Miquella" } },
                { "elden_boss_mohg", new[] { "mohg", "Mohg", "Lord of Blood" } },
                { "elden_boss_fire_giant", new[] { "fire_giant", "Fire Giant" } },
                { "elden_boss_horax", new[] { "hoarah_loux", "Hoarah Loux" } },
                { "elden_boss_beast", new[] { "beast_clergyman", "Beast Clergyman" } },
            };

            foreach (var boss in bossPatterns)
            {
                var found = boss.Value.Any(p => text.Contains(p, StringComparison.OrdinalIgnoreCase));
                achievements.Add(new Achievement
                {
                    Apiname = boss.Key,
                    Name = AchievementMap[boss.Key].Name,
                    Description = AchievementMap[boss.Key].Description,
                    Achieved = found,
                    Progress = found ? 1 : 0,
                    MaxProgress = 1
                });
            }

            var endingPatterns = new Dictionary<string, string[]>
            {
                { "elden_elden", new[] { "ending_elden", "elden_order" } },
                { "elden_ending_siffrin", new[] { "ending_siffrin", "age_of_stars" } },
                { "elden_ending_duskborn", new[] { "ending_duskborn", "frenzied_flame" } },
            };

            foreach (var ending in endingPatterns)
            {
                var found = ending.Value.Any(p => text.Contains(p, StringComparison.OrdinalIgnoreCase));
                achievements.Add(new Achievement
                {
                    Apiname = ending.Key,
                    Name = AchievementMap[ending.Key].Name,
                    Description = AchievementMap[ending.Key].Description,
                    Achieved = found,
                    Progress = found ? 1 : 0,
                    MaxProgress = 1
                });
            }

            var dungeonCount = Regex.Matches(text, @"[Dd]ungeon|[Tt]omb|[Cc]atacomb|[Ss]tructure").Count;
            AddAchievement(achievements, "elden_legacy_dungeon", dungeonCount, 5);
            AddAchievement(achievements, "elden_dungeon_20", dungeonCount, 20);

            achievements.Add(new Achievement
            {
                Apiname = "elden_weapon_max",
                Name = AchievementMap["elden_weapon_max"].Name,
                Description = AchievementMap["elden_weapon_max"].Description,
                Achieved = text.Contains("+25", StringComparison.Ordinal),
                Progress = text.Contains("+25", StringComparison.Ordinal) ? 1 : 0,
                MaxProgress = 1
            });

            achievements.Add(new Achievement
            {
                Apiname = "elden_mount_summon",
                Name = AchievementMap["elden_mount_summon"].Name,
                Description = AchievementMap["elden_mount_summon"].Description,
                Achieved = true,
                Progress = 1,
                MaxProgress = 1
            });

            return achievements;
        }
        catch
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

    private static int ExtractLevel(byte[] buffer, string text)
    {
        for (var i = 0; i < buffer.Length - 100; i += 4)
        {
            var sum = BitConverter.ToUInt32(buffer, i);
            if (sum >= 10 && sum <= 713)
            {
                var v = BitConverter.ToUInt32(buffer, i + 4);
                var m = BitConverter.ToUInt32(buffer, i + 8);
                var e = BitConverter.ToUInt32(buffer, i + 12);
                var s1 = BitConverter.ToUInt32(buffer, i + 16);
                var d = BitConverter.ToUInt32(buffer, i + 20);
                var in2 = BitConverter.ToUInt32(buffer, i + 24);
                var f = BitConverter.ToUInt32(buffer, i + 28);
                var a = BitConverter.ToUInt32(buffer, i + 32);

                if (v is >= 1 and <= 99 && m is >= 1 and <= 99 && e is >= 1 and <= 99 &&
                    s1 is >= 1 and <= 99 && d is >= 1 and <= 99 && in2 is >= 1 and <= 99 &&
                    f is >= 1 and <= 99 && a is >= 1 and <= 99)
                {
                    var calculated = (int)(v + m + e + s1 + d + in2 + f + a + 79);
                    if (calculated == (int)sum) return (int)sum;
                }
            }
        }
        return 0;
    }

    private static int ExtractRunes(byte[] buffer, string text)
    {
        for (var i = 100; i < buffer.Length - 8; i += 4)
        {
            var val = BitConverter.ToInt32(buffer, i);
            if (val >= 1000000 && val <= 999999999)
            {
                var prev = BitConverter.ToInt32(buffer, i - 4);
                if (prev is >= 1 and <= 713)
                    return val;
            }
        }
        return 0;
    }

    private static int ExtractDeaths(byte[] buffer, string text)
    {
        for (var i = 0; i < buffer.Length - 20; i++)
        {
            if (text.Substring(i, Math.Min(16, text.Length - i)).Contains("death", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 20 < buffer.Length)
                {
                    var val = BitConverter.ToInt32(buffer, i + 16);
                    if (val is >= 0 and <= 999999) return val;
                }
            }
        }
        return 0;
    }

    private static int ExtractNgPlus(byte[] buffer, string text)
    {
        for (var i = 0; i < buffer.Length - 8; i++)
        {
            if (text.Substring(i, Math.Min(20, text.Length - i)).Contains("new_game", StringComparison.OrdinalIgnoreCase) ||
                text.Substring(i, Math.Min(20, text.Length - i)).Contains("game_cycle", StringComparison.OrdinalIgnoreCase))
            {
                var val = BitConverter.ToInt32(buffer, i + 4);
                if (val is >= 0 and <= 10) return val;
            }
        }
        return 0;
    }

    private static int ExtractSpellCount(string text)
    {
        return Regex.Matches(text, @"[Ss]pell|[Ff]eitico|[Ss]orcery|[IIncantation]").Count;
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
