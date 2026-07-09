using System.Text;
using System.Text.RegularExpressions;
using EMECore.Core.Models;

namespace EMECore.Hardware.Services;

public class GTAVParser
{
    private static readonly Dictionary<string, (string Name, string Description)> AchievementMap = new()
    {
        { "gta_mission_1", ("Primeiro Trabalho", "Complete a primeira missão principal") },
        { "gta_mission_10", ("Profissional", "Complete 10 missões principais") },
        { "gta_mission_all", ("Rei do Crime", "Complete todas as missões principais") },
        { "gta_strangers_5", ("Estranhos", "Complete 5 missões de estranhos") },
        { "gta_strangers_10", ("Rede de Contatos", "Complete 10 missões de estranhos") },
        { "gta_collectible_1", ("Caçador de Coleccionáveis", "Encontre 1 de cada tipo de colecionável") },
        { "gta_collectible_10", ("Colecionador", "Encontre 10 colecionáveis") },
        { "gta_stunt_1", ("Adrenalina", "Complete 1 stunt jump") },
        { "gta_stunt_10", ("Temerário", "Complete 10 stunt jumps") },
        { "gta_stunt_50", ("Morte Certa", "Complete 50 stunt jumps") },
        { "gta_property_1", ("Investidor", "Compre uma propriedade") },
        { "gta_property_5", ("Magnata", "Compre 5 propriedades") },
        { "gta_gold_1", ("Ouro", "Ganhe 1 medalha de ouro em uma missão") },
        { "gta_gold_10", ("Colecionador de Ouro", "Ganhe 10 medalhas de ouro") },
        { "gta_gold_50", ("Perfeccionista", "Ganhe 50 medalhas de ouro") },
        { "gta_distance_100", ("Viajante", "Viaje 100km") },
        { "gta_distance_1000", ("Nômade", "Viaje 1000km") },
        { "gta_distance_10000", ("Mundo Aberto", "Viaje 10.000km") },
        { "gta_kills_100", ("Atirador", "Elimine 100 inimigos") },
        { "gta_kills_1000", ("Veterano de Guerra", "Elimine 1000 inimigos") },
        { "gta_wanted_5stars", ("Most Wanted", "Alcance 5 estrelas de procurado") },
        { "gta_stock_invest", ("Wall Street", "Ganhe dinheiro na bolsa") },
        { "gta_heist_1", ("Assalto Perfeito", "Complete um heist") },
        { "gta_heist_all", ("Lenda dos Heists", "Complete todos os heists") },
        { "gta_playtime_24h", ("Vício Total", "Jogue por 24 horas") },
    };

    private static readonly Dictionary<string, int> MaxProgressMap = new()
    {
        { "gta_mission_10", 10 }, { "gta_strangers_5", 5 }, { "gta_strangers_10", 10 },
        { "gta_collectible_10", 10 }, { "gta_stunt_1", 1 }, { "gta_stunt_10", 10 }, { "gta_stunt_50", 50 },
        { "gta_property_5", 5 }, { "gta_gold_1", 1 }, { "gta_gold_10", 10 }, { "gta_gold_50", 50 },
        { "gta_distance_100", 100 }, { "gta_distance_1000", 1000 }, { "gta_distance_10000", 10000 },
        { "gta_kills_100", 100 }, { "gta_kills_1000", 1000 },
        { "gta_playtime_24h", 24 },
    };

    public string? FindSavePath()
    {
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var profilesPath = Path.Combine(documents, "Rockstar Games", "GTA V", "Profiles");

        if (!Directory.Exists(profilesPath)) return null;

        try
        {
            foreach (var profileDir in Directory.GetDirectories(profilesPath))
            {
                var sgtaFiles = Directory.GetFiles(profileDir, "SGTA*")
                    .Where(f => !f.EndsWith(".bak"))
                    .OrderByDescending(f => new FileInfo(f).LastWriteTime)
                    .ToArray();

                if (sgtaFiles.Length > 0) return sgtaFiles[0];
            }
        }
        catch { }

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
            var text = Encoding.ASCII.GetString(buffer);
            var achievements = new List<Achievement>();

            var missionProgress = ExtractMissionProgress(buffer, text);
            var stats = ExtractStats(buffer, text);

            AddAchievement(achievements, "gta_mission_1", missionProgress > 0 ? 1 : 0);
            AddAchievement(achievements, "gta_mission_10", missionProgress);
            AddAchievement(achievements, "gta_mission_all", missionProgress >= 69 ? 1 : 0);

            var stuntJumps = stats.GetValueOrDefault("stunt_jumps", 0);
            AddAchievement(achievements, "gta_stunt_1", stuntJumps);
            AddAchievement(achievements, "gta_stunt_10", stuntJumps);
            AddAchievement(achievements, "gta_stunt_50", stuntJumps);

            var kills = stats.GetValueOrDefault("kills", 0);
            AddAchievement(achievements, "gta_kills_100", kills);
            AddAchievement(achievements, "gta_kills_1000", kills);

            var distance = stats.GetValueOrDefault("distance", 0);
            AddAchievement(achievements, "gta_distance_100", distance);
            AddAchievement(achievements, "gta_distance_1000", distance);
            AddAchievement(achievements, "gta_distance_10000", distance);

            var wantedLevel = stats.GetValueOrDefault("max_wanted", 0);
            achievements.Add(new Achievement
            {
                Apiname = "gta_wanted_5stars",
                Name = AchievementMap["gta_wanted_5stars"].Name,
                Description = AchievementMap["gta_wanted_5stars"].Description,
                Achieved = wantedLevel >= 5,
                Progress = Math.Min(wantedLevel, 5),
                MaxProgress = 5
            });

            var heists = Regex.Matches(text, @"[Hh]eist|[Aa]ssoalto").Count;
            AddAchievement(achievements, "gta_heist_1", heists > 0 ? 1 : 0);

            var playTime = stats.GetValueOrDefault("play_time", 0);
            var hours = playTime / 3600;
            AddAchievement(achievements, "gta_playtime_24h", hours);

            var goldMedals = Regex.Matches(text, @"[Gg]old|[Oo]uro").Count;
            AddAchievement(achievements, "gta_gold_1", goldMedals);
            AddAchievement(achievements, "gta_gold_10", goldMedals);
            AddAchievement(achievements, "gta_gold_50", goldMedals);

            return achievements;
        }
        catch
        {
            return CreateLockedAchievements();
        }
    }

    private static int ExtractMissionProgress(byte[] buffer, string text)
    {
        var match = Regex.Match(text, @"[Mm]ission.*?[Pp]rogress.*?(\d+)");
        if (match.Success) return int.Parse(match.Groups[1].Value);

        for (var i = 0; i < buffer.Length - 4; i++)
        {
            var val = BitConverter.ToInt32(buffer, i);
            if (val is >= 1 and <= 100)
            {
                var prev = BitConverter.ToInt32(buffer, i - 4);
                if (prev is >= 0 and <= 10)
                    return val;
            }
        }
        return 0;
    }

    private static Dictionary<string, int> ExtractStats(byte[] buffer, string text)
    {
        var stats = new Dictionary<string, int>();

        var patterns = new Dictionary<string, string>
        {
            { "stunt_jumps", @"stunt.*?jump.*?(\d+)" },
            { "kills", @"kills?.*?(\d+)" },
            { "distance", @"distance.*?(\d+)" },
            { "max_wanted", @"wanted.*?level.*?(\d+)" },
            { "play_time", @"play.*?time.*?(\d+)" },
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(text, pattern.Value, RegexOptions.IgnoreCase);
            if (match.Success)
                stats[pattern.Key] = int.Parse(match.Groups[1].Value);
        }

        return stats;
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
