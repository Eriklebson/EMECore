using System.Text;
using System.Text.RegularExpressions;
using EMECore.Core.Models;

namespace EMECore.Hardware.Services;

public class MonsterHunterWildsParser
{
    private static readonly Dictionary<string, (string Name, string Description)> AchievementMap = new()
    {
        { "mhw_hunter_rank_10", ("Caçador Novato", "Alcance Hunter Rank 10") },
        { "mhw_hunter_rank_50", ("Caçador Veterano", "Alcance Hunter Rank 50") },
        { "mhw_hunter_rank_100", ("Lenda da Caça", "Alcance Hunter Rank 100") },
        { "mhw_hunter_rank_200", ("Mestre Caçador", "Alcance Hunter Rank 200") },
        { "mhw_hunter_rank_500", ("Caçador Supremo", "Alcance Hunter Rank 500") },
        { "mhw_monster_10", ("Estudioso de Monstros", "Encontre 10 monstros diferentes") },
        { "mhw_monster_30", ("Naturalista", "Encontre 30 monstros diferentes") },
        { "mhw_monster_all", ("Zoólogo Completo", "Encontre todos os monstros") },
        { "mhw_quest_50", ("Caçador de Missões", "Complete 50 missões") },
        { "mhw_quest_100", ("Veterano de Campo", "Complete 100 missões") },
        { "mhw_quest_500", ("Lenda Viva", "Complete 500 missões") },
        { "mhw_weapon_5", ("Artesão", "Forje 5 armas diferentes") },
        { "mhw_weapon_15", ("Mestre Armeiro", "Forje 15 armas diferentes") },
        { "mhw_armor_20", ("Armadura Completa", "Forje 20 peças de armadura") },
        { "mhw_armor_50", ("Tanque", "Forje 50 peças de armadura") },
        { "mhw_collect_100", ("Colecionador", "Colete 100 itens diferentes") },
        { "mhw_collect_500", ("Horda", "Colete 500 itens diferentes") },
        { "mhw_palico_10", ("Companheiro Leal", "Desbloqueie 10 habilidades do Palico") },
        { "mhw_palico_30", ("Mestre do Palico", "Desbloqueie 30 habilidades do Palico") },
        { "mhw_endergon_10", ("Explorador", "Encontre 10 Endregons") },
        { "mhw_endergon_30", ("Caçador de Endregons", "Encontre 30 Endregons") },
        { "mhw_playtime_50h", ("Vício Total", "Jogue por 50 horas") },
        { "mhw_playtime_100h", ("Vida de Caçador", "Jogue por 100 horas") },
        { "mhw_playtime_200h", ("Eterno Caçador", "Jogue por 200 horas") },
        { "mhw_difficulty_hard", ("Sobrevivente", "Complete uma missão no modo difícil") },
    };

    private static readonly Dictionary<string, int> MaxProgressMap = new()
    {
        { "mhw_hunter_rank_10", 10 }, { "mhw_hunter_rank_50", 50 }, { "mhw_hunter_rank_100", 100 },
        { "mhw_hunter_rank_200", 200 }, { "mhw_hunter_rank_500", 500 },
        { "mhw_monster_10", 10 }, { "mhw_monster_30", 30 }, { "mhw_monster_all", 50 },
        { "mhw_quest_50", 50 }, { "mhw_quest_100", 100 }, { "mhw_quest_500", 500 },
        { "mhw_weapon_5", 5 }, { "mhw_weapon_15", 15 },
        { "mhw_armor_20", 20 }, { "mhw_armor_50", 50 },
        { "mhw_collect_100", 100 }, { "mhw_collect_500", 500 },
        { "mhw_palico_10", 10 }, { "mhw_palico_30", 30 },
        { "mhw_endergon_10", 10 }, { "mhw_endergon_30", 30 },
        { "mhw_playtime_50h", 50 }, { "mhw_playtime_100h", 100 }, { "mhw_playtime_200h", 200 },
    };

    public string? FindSavePath()
    {
        var basePath = LocalizedPaths.FindLocalAppDataSubPath("Capcom", "MonsterHunterWilds");

        if (basePath != null)
        {
            try
            {
                var dataFiles = Directory.GetFiles(basePath, "data001Slot*.bin")
                    .OrderByDescending(f => new FileInfo(f).LastWriteTime)
                    .ToArray();
                if (dataFiles.Length > 0) return dataFiles[0];
            }
            catch { }
        }

        var steamPaths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steamapps", "common", "Monster Hunter Wilds"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam", "steamapps", "common", "Monster Hunter Wilds"),
        };

        foreach (var steamPath in steamPaths)
        {
            if (!Directory.Exists(steamPath)) continue;
            try
            {
                var dataFiles = Directory.GetFiles(steamPath, "data001Slot*.bin", SearchOption.AllDirectories)
                    .OrderByDescending(f => new FileInfo(f).LastWriteTime)
                    .ToArray();
                if (dataFiles.Length > 0) return dataFiles[0];
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

            var hunterRank = ExtractHunterRank(buffer, text);
            var questCount = ExtractQuestCount(text);
            var monsterCount = ExtractMonsterCount(text);
            var weaponCount = ExtractWeaponCount(text);
            var armorCount = ExtractArmorCount(text);

            AddAchievement(achievements, "mhw_hunter_rank_10", hunterRank);
            AddAchievement(achievements, "mhw_hunter_rank_50", hunterRank);
            AddAchievement(achievements, "mhw_hunter_rank_100", hunterRank);
            AddAchievement(achievements, "mhw_hunter_rank_200", hunterRank);
            AddAchievement(achievements, "mhw_hunter_rank_500", hunterRank);

            AddAchievement(achievements, "mhw_monster_10", monsterCount);
            AddAchievement(achievements, "mhw_monster_30", monsterCount);
            AddAchievement(achievements, "mhw_monster_all", monsterCount);

            AddAchievement(achievements, "mhw_quest_50", questCount);
            AddAchievement(achievements, "mhw_quest_100", questCount);
            AddAchievement(achievements, "mhw_quest_500", questCount);

            AddAchievement(achievements, "mhw_weapon_5", weaponCount);
            AddAchievement(achievements, "mhw_weapon_15", weaponCount);
            AddAchievement(achievements, "mhw_armor_20", armorCount);
            AddAchievement(achievements, "mhw_armor_50", armorCount);

            var collectCount = Regex.Matches(text, @"[Ii]tem|[Cc]ollectible").Count;
            AddAchievement(achievements, "mhw_collect_100", collectCount);
            AddAchievement(achievements, "mhw_collect_500", collectCount);

            var palicoCount = Regex.Matches(text, @"[Pp]alico|[Cc]at").Count;
            AddAchievement(achievements, "mhw_palico_10", palicoCount);
            AddAchievement(achievements, "mhw_palico_30", palicoCount);

            var endergonCount = Regex.Matches(text, @"[Ee]ndergon").Count;
            AddAchievement(achievements, "mhw_endergon_10", endergonCount);
            AddAchievement(achievements, "mhw_endergon_30", endergonCount);

            return achievements;
        }
        catch
        {
            return CreateLockedAchievements();
        }
    }

    private static int ExtractHunterRank(byte[] buffer, string text)
    {
        for (var i = 0; i < buffer.Length - 8; i++)
        {
            var val = BitConverter.ToUInt32(buffer, i);
            if (val is >= 1 and <= 999)
            {
                var next = BitConverter.ToUInt32(buffer, i + 4);
                if (next is >= 0 and <= 99999)
                    return (int)val;
            }
        }

        var match = Regex.Match(text, @"[Hh]unter.*?[Rr]ank.*?(\d+)");
        return match.Success ? int.Parse(match.Groups[1].Value) : 0;
    }

    private static int ExtractQuestCount(string text)
    {
        return Regex.Matches(text, @"[Qq]uest|[Mm]issao").Count;
    }

    private static int ExtractMonsterCount(string text)
    {
        return Regex.Matches(text, @"[Mm]onster|[Cc]reature").Count;
    }

    private static int ExtractWeaponCount(string text)
    {
        return Regex.Matches(text, @"[Ww]eapon|[Aa]rma").Count;
    }

    private static int ExtractArmorCount(string text)
    {
        return Regex.Matches(text, @"[Aa]rmor|[Aa]rmadura").Count;
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
