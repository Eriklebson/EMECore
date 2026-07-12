using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using EMECore.Core.Models;

namespace EMECore.Hardware.Services;

public class PalworldParser
{
    private static readonly Dictionary<string, (string Name, string Description)> AchievementMap = new()
    {
        { "pal_level_10", ("Treinador Iniciante", "Alcance nível 10") },
        { "pal_level_20", ("Treinador Dedicado", "Alcance nível 20") },
        { "pal_level_30", ("Treinador Experiente", "Alcance nível 30") },
        { "pal_level_40", ("Treinador de Elite", "Alcance nível 40") },
        { "pal_level_50", ("Mestre dos Pals", "Alcance nível 50 (máximo)") },
        { "pal_catch_10", ("Caçador de Pals", "Capture 10 Pals") },
        { "pal_catch_50", ("Colecionador Ávido", "Capture 50 Pals") },
        { "pal_catch_100", ("Catálogo Completo", "Capture 100 Pals diferentes") },
        { "pal_catch_all", ("Mestre da Captura", "Capture todos os Pals (1-111)") },
        { "pal_base_1", ("Fundador", "Construa sua primeira base") },
        { "pal_base_3", ("Império em Crescimento", "Construa 3 bases") },
        { "pal_build_50", ("Arquiteto", "Construa 50 estruturas") },
        { "pal_build_200", ("Engenheiro Mestre", "Construa 200 estruturas") },
        { "pal_boss_1", ("Primeiro Desafio", "Derrote seu primeiro Boss") },
        { "pal_boss_10", ("Caçador de Chefes", "Derrote 10 Bosses") },
        { "pal_boss_50", ("Lenda dos Bosses", "Derrote 50 Bosses") },
        { "pal_item_100", ("Apanhador", "Colete 100 itens") },
        { "pal_item_1000", ("Horda", "Colete 1000 itens") },
        { "pal_eggs_10", ("Chocador", "Incube 10 ovos") },
        { "pal_eggs_50", ("Mestre Incubador", "Incube 50 ovos") },
        { "pal_playtime_10h", ("Viciado", "Jogue por 10 horas") },
        { "pal_playtime_50h", ("Nômade Digital", "Jogue por 50 horas") },
        { "pal_playtime_100h", ("Vida de Pal", "Jogue por 100 horas") },
        { "pal_cook_10", ("Chef Palworld", "Cozinhe 10 pratos") },
        { "pal_tech_50", ("Tecnólogo", "Desbloqueie 50 tecnologias") },
    };

    private static readonly Dictionary<string, int> MaxProgressMap = new()
    {
        { "pal_level_10", 10 }, { "pal_level_20", 20 }, { "pal_level_30", 30 },
        { "pal_level_40", 40 }, { "pal_level_50", 50 },
        { "pal_catch_10", 10 }, { "pal_catch_50", 50 }, { "pal_catch_100", 100 }, { "pal_catch_all", 111 },
        { "pal_base_3", 3 }, { "pal_build_50", 50 }, { "pal_build_200", 200 },
        { "pal_boss_1", 1 }, { "pal_boss_10", 10 }, { "pal_boss_50", 50 },
        { "pal_item_100", 100 }, { "pal_item_1000", 1000 },
        { "pal_eggs_10", 10 }, { "pal_eggs_50", 50 },
        { "pal_playtime_10h", 10 }, { "pal_playtime_50h", 50 }, { "pal_playtime_100h", 100 },
        { "pal_cook_10", 10 }, { "pal_tech_50", 50 },
    };

    public string? FindSavePath()
    {
        var basePath = LocalizedPaths.FindLocalAppDataSubPath("Pal", Path.Combine("Saved", "SaveGames"));

        if (basePath == null) return null;

        try
        {
            foreach (var userDir in Directory.GetDirectories(basePath))
            {
                foreach (var worldDir in Directory.GetDirectories(userDir))
                {
                    var levelSav = Path.Combine(worldDir, "Level.sav");
                    if (File.Exists(levelSav)) return levelSav;
                }
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
            var rawBytes = File.ReadAllBytes(filePath);
            var decompressed = DecompressSav(rawBytes);
            if (decompressed == null)
                return CreateLockedAchievements();

            var text = Encoding.UTF8.GetString(decompressed);
            var achievements = new List<Achievement>();

            var level = ExtractJsonInt(text, "\"Level\"\\s*:\\s*\\{\"Int\"\\s*:\\s*\\{\"value\"\\s*:\\s*(\\d+)") ?? 0;
            var palCount = Regex.Matches(text, "\"IsPlayer\"\\s*:\\s*\\{\"Bool\"\\s*:\\s*\\{\"value\"\\s*:\\s*false").Count;
            var uniquePals = Regex.Matches(text, "\"CharacterID\"\\s*:\\s*\\{\"Name\"\\s*:\\s*\\{\"value\"\\s*:\\s*\"([^\"]+)\"").Count;

            AddLevel(achievements, level);
            AddCatch(achievements, uniquePals);
            AddGeneric(achievements, "pal_boss_1", palCount > 0 ? 1 : 0);
            AddPlaytime(achievements, text);

            var baseCount = Regex.Matches(text, "\"BaseCampSaveData\"").Count;
            AddGeneric(achievements, "pal_base_1", baseCount > 0 ? 1 : 0);
            AddGeneric(achievements, "pal_base_3", baseCount);

            var buildCount = Regex.Matches(text, "\"MapObjectSaveData\"").Count;
            AddGeneric(achievements, "pal_build_50", buildCount);
            AddGeneric(achievements, "pal_build_200", buildCount);

            var eggCount = Regex.Matches(text, @"[Ee]gg|[Oo]vo").Count;
            AddGeneric(achievements, "pal_eggs_10", eggCount);
            AddGeneric(achievements, "pal_eggs_50", eggCount);

            return achievements;
        }
        catch
        {
            return CreateLockedAchievements();
        }
    }

    private static byte[]? DecompressSav(byte[] data)
    {
        if (data.Length < 12) return null;

        var uncompressedLen = BitConverter.ToInt32(data, 0);
        var compressedLen = BitConverter.ToInt32(data, 4);
        var magic = Encoding.ASCII.GetString(data, 8, 3);

        int dataStart;
        if (magic == "CNK")
        {
            if (data.Length < 24) return null;
            uncompressedLen = BitConverter.ToInt32(data, 12);
            compressedLen = BitConverter.ToInt32(data, 16);
            magic = Encoding.ASCII.GetString(data, 20, 3);
            var saveType = data[23];
            dataStart = 24;
            if (magic != "PlZ" || saveType is not (0x31 or 0x32)) return null;
        }
        else
        {
            var saveType = data[11];
            dataStart = 12;
            if (magic != "PlZ" || saveType is not (0x31 or 0x32)) return null;

            if (saveType == 0x31)
            {
                using var ms = new MemoryStream(data, dataStart, data.Length - dataStart);
                using var ds = new DeflateStream(ms, CompressionMode.Decompress);
                var output = new byte[uncompressedLen];
                var read = ds.Read(output, 0, uncompressedLen);
                return read == uncompressedLen ? output : null;
            }
            else
            {
                using var ms = new MemoryStream(data, dataStart, data.Length - dataStart);
                using var ds = new DeflateStream(ms, CompressionMode.Decompress);
                var innerLenBuf = new byte[4];
                ds.Read(innerLenBuf, 0, 4);
                var innerLen = BitConverter.ToInt32(innerLenBuf, 0);
                var inner = new byte[innerLen];
                ds.Read(inner, 0, innerLen);
                using var ms3 = new MemoryStream(inner);
                using var ds2 = new DeflateStream(ms3, CompressionMode.Decompress);
                var output = new byte[uncompressedLen];
                var read = ds2.Read(output, 0, uncompressedLen);
                return read == uncompressedLen ? output : null;
            }
        }
        return null;
    }

    private static int? ExtractJsonInt(string text, string pattern)
    {
        var match = Regex.Match(text, pattern);
        return match.Success ? int.Parse(match.Groups[1].Value) : null;
    }

    private static void AddLevel(List<Achievement> achievements, int level)
    {
        foreach (var kvp in AchievementMap.Where(a => a.Key.StartsWith("pal_level_")))
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

    private static void AddCatch(List<Achievement> achievements, int count)
    {
        foreach (var kvp in AchievementMap.Where(a => a.Key.StartsWith("pal_catch_")))
        {
            var req = MaxProgressMap[kvp.Key];
            achievements.Add(new Achievement
            {
                Apiname = kvp.Key,
                Name = kvp.Value.Name,
                Description = kvp.Value.Description,
                Achieved = count >= req,
                Progress = Math.Min(count, req),
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

    private static void AddPlaytime(List<Achievement> achievements, string text)
    {
        var totalMinutes = 0;
        var match = Regex.Match(text, "\"TotalPlayMinutes\"\\s*:\\s*\\{\"Int\"\\s*:\\s*\\{\"value\"\\s*:\\s*(\\d+)");
        if (match.Success) totalMinutes = int.Parse(match.Groups[1].Value);
        else
        {
            var match2 = Regex.Match(text, "\"PlayTime\"\\s*:\\s*\\{\"Int\"\\s*:\\s*\\{\"value\"\\s*:\\s*(\\d+)");
            if (match2.Success) totalMinutes = int.Parse(match2.Groups[1].Value);
        }

        var hours = totalMinutes / 60;
        foreach (var kvp in AchievementMap.Where(a => a.Key.StartsWith("pal_playtime_")))
        {
            var req = MaxProgressMap[kvp.Key];
            achievements.Add(new Achievement
            {
                Apiname = kvp.Key,
                Name = kvp.Value.Name,
                Description = kvp.Value.Description,
                Achieved = hours >= req,
                Progress = Math.Min(hours, req),
                MaxProgress = req
            });
        }
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
