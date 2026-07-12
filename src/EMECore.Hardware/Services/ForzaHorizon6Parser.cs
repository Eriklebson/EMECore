using System.Text;
using System.Text.RegularExpressions;
using EMECore.Core.Models;

namespace EMECore.Hardware.Services;

public class ForzaHorizon6Parser
{
    private static readonly Dictionary<string, (string Name, string Description)> AchievementMap = new()
    {
        { "fh6_cars_10", ("Primeira Coleção", "Colete 10 carros") },
        { "fh6_cars_50", ("Colecionador", "Colete 50 carros") },
        { "fh6_cars_100", ("Concessionária", "Colete 100 carros") },
        { "fh6_cars_200", ("Garagem Premium", "Colete 200 carros") },
        { "fh6_cars_all", ("Coleção Completa", "Desbloqueie todos os carros") },
        { "fh6_stars_50", ("Estrela em Ascensão", "Ganhe 50 estrelas em desafios") },
        { "fh6_stars_100", ("Céu de Estrelas", "Ganhe 100 estrelas em desafios") },
        { "fh6_stars_200", ("Constelação", "Ganhe 200 estrelas em desafios") },
        { "fh6_races_50", ("Piloto", "Complete 50 corridas") },
        { "fh6_races_100", ("Veterano das Pistas", "Complete 100 corridas") },
        { "fh6_races_500", ("Lenda das Corridas", "Complete 500 corridas") },
        { "fh6_photo_10", ("Fotógrafo", "Tire 10 fotos") },
        { "fh6_photo_50", ("Artista Visual", "Tire 50 fotos") },
        { "fh6_photo_100", ("Mestre da Fotografia", "Tire 100 fotos") },
        { "fh6_share_10", ("Compartilhador", "Compartilhe 10 criações") },
        { "fh6_share_50", ("Criador de Conteúdo", "Compartilhe 50 criações") },
        { "fh6_distance_1000", ("Viajante", "Viaje 1000km") },
        { "fh6_distance_10000", ("Nômade", "Viaje 10.000km") },
        { "fh6_distance_100000", ("Mundo Aberto", "Viaje 100.000km") },
        { "fh6_skill_100000", ("Habilidoso", "Acumule 100.000 pontos de habilidade") },
        { "fh6_skill_1000000", ("Mestre de Habilidade", "Acumule 1.000.000 pontos de habilidade") },
        { "fh6_season_1", ("Primeira Temporada", "Complete uma temporada") },
        { "fh6_season_4", ("Ano Completo", "Complete 4 temporadas") },
        { "fh6_apex_1", ("Desafio Apex", "Complete um desafio Apex") },
        { "fh6_apex_10", ("Caçador de Apex", "Complete 10 desafios Apex") },
        { "fh6_playtime_50h", ("Vício Total", "Jogue por 50 horas") },
        { "fh6_playtime_100h", ("Piloto Eterno", "Jogue por 100 horas") },
        { "fh6_speed_300", ("Velocista", "Alcance 300 km/h") },
        { "fh6_speed_400", ("Ultrassônico", "Alcance 400 km/h") },
        { "fh6_speed_500", ("Hiper Velocidade", "Alcance 500 km/h") },
    };

    private static readonly Dictionary<string, int> MaxProgressMap = new()
    {
        { "fh6_cars_10", 10 }, { "fh6_cars_50", 50 }, { "fh6_cars_100", 100 }, { "fh6_cars_200", 200 },
        { "fh6_stars_50", 50 }, { "fh6_stars_100", 100 }, { "fh6_stars_200", 200 },
        { "fh6_races_50", 50 }, { "fh6_races_100", 100 }, { "fh6_races_500", 500 },
        { "fh6_photo_10", 10 }, { "fh6_photo_50", 50 }, { "fh6_photo_100", 100 },
        { "fh6_share_10", 10 }, { "fh6_share_50", 50 },
        { "fh6_distance_1000", 1000 }, { "fh6_distance_10000", 10000 }, { "fh6_distance_100000", 100000 },
        { "fh6_skill_100000", 100000 }, { "fh6_skill_1000000", 1000000 },
        { "fh6_season_4", 4 }, { "fh6_apex_10", 10 },
        { "fh6_playtime_50h", 50 }, { "fh6_playtime_100h", 100 },
        { "fh6_speed_300", 300 }, { "fh6_speed_400", 400 }, { "fh6_speed_500", 500 },
    };

    public string? FindSavePath()
    {
        var possiblePaths = new[]
        {
            LocalizedPaths.FindLocalAppDataSubPath("ForzaHorizon6", "LocalStorage_Shared"),
            LocalizedPaths.FindLocalAppDataSubPath("Microsoft.ForzaHorizon6", "LocalStorage_Shared"),
        };

        foreach (var basePath in possiblePaths)
        {
            if (basePath == null) continue;
            try
            {
                foreach (var userDir in Directory.GetDirectories(basePath, "User_*"))
                {
                    var profileData = Path.Combine(userDir, "C_ProfileData");
                    if (File.Exists(profileData)) return profileData;

                    var profileBackup = Path.Combine(userDir, "C_ProfileBackup");
                    if (File.Exists(profileBackup)) return profileBackup;
                }
            }
            catch { }
        }

        var xboxPaths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "XboxGames"),
            @"C:\XboxGames",
            @"D:\XboxGames",
        };

        foreach (var xboxBase in xboxPaths)
        {
            var pgsPath = Path.Combine(xboxBase, "GameSave", "pgs");
            if (!Directory.Exists(pgsPath)) continue;
            try
            {
                foreach (var userDir in Directory.GetDirectories(pgsPath, "u_*"))
                {
                    var containersRoot = Path.Combine(userDir, "ContainersRoot");
                    if (!Directory.Exists(containersRoot)) continue;

                    foreach (var containerDir in Directory.GetDirectories(containersRoot, "User_*"))
                    {
                        var profileData = Path.Combine(containerDir, "C_ProfileData");
                        if (File.Exists(profileData)) return profileData;
                    }
                }
            }
            catch { }
        }

        var packagesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages");
        if (Directory.Exists(packagesPath))
        {
            try
            {
                foreach (var pkgDir in Directory.GetDirectories(packagesPath, "*ForzaHorizon6*"))
                {
                    var wgsPath = Path.Combine(pkgDir, "SystemAppData", "wgs");
                    if (!Directory.Exists(wgsPath)) continue;

                    foreach (var indexDir in Directory.GetDirectories(wgsPath))
                    {
                        foreach (var containerDir in Directory.GetDirectories(indexDir))
                        {
                            var files = Directory.GetFiles(containerDir);
                            if (files.Length > 0) return files[0];
                        }
                    }
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

            var carCount = ExtractCarCount(buffer, text);
            var raceCount = ExtractRaceCount(buffer, text);
            var starCount = ExtractStarCount(buffer, text);
            var photoCount = ExtractPhotoCount(buffer, text);
            var shareCount = ExtractShareCount(buffer, text);
            var distance = ExtractDistance(buffer, text);
            var skillPoints = ExtractSkillPoints(buffer, text);
            var speed = ExtractMaxSpeed(buffer, text);
            var seasonCount = ExtractSeasonCount(buffer, text);
            var apexCount = ExtractApexCount(buffer, text);

            AddGeneric(achievements, "fh6_cars_10", carCount);
            AddGeneric(achievements, "fh6_cars_50", carCount);
            AddGeneric(achievements, "fh6_cars_100", carCount);
            AddGeneric(achievements, "fh6_cars_200", carCount);

            AddGeneric(achievements, "fh6_races_50", raceCount);
            AddGeneric(achievements, "fh6_races_100", raceCount);
            AddGeneric(achievements, "fh6_races_500", raceCount);

            AddGeneric(achievements, "fh6_stars_50", starCount);
            AddGeneric(achievements, "fh6_stars_100", starCount);
            AddGeneric(achievements, "fh6_stars_200", starCount);

            AddGeneric(achievements, "fh6_photo_10", photoCount);
            AddGeneric(achievements, "fh6_photo_50", photoCount);
            AddGeneric(achievements, "fh6_photo_100", photoCount);

            AddGeneric(achievements, "fh6_share_10", shareCount);
            AddGeneric(achievements, "fh6_share_50", shareCount);

            AddGeneric(achievements, "fh6_distance_1000", distance);
            AddGeneric(achievements, "fh6_distance_10000", distance);
            AddGeneric(achievements, "fh6_distance_100000", distance);

            AddGeneric(achievements, "fh6_skill_100000", skillPoints);
            AddGeneric(achievements, "fh6_skill_1000000", skillPoints);

            AddGeneric(achievements, "fh6_speed_300", speed);
            AddGeneric(achievements, "fh6_speed_400", speed);
            AddGeneric(achievements, "fh6_speed_500", speed);

            AddGeneric(achievements, "fh6_season_1", seasonCount);
            AddGeneric(achievements, "fh6_season_4", seasonCount);

            AddGeneric(achievements, "fh6_apex_1", apexCount);
            AddGeneric(achievements, "fh6_apex_10", apexCount);

            return achievements;
        }
        catch
        {
            return CreateLockedAchievements();
        }
    }

    private static int ExtractCarCount(byte[] buffer, string text)
    {
        var match = Regex.Match(text, @"[Cc]ars?\s*[:=]?\s*(\d+)");
        if (match.Success) return int.Parse(match.Groups[1].Value);

        var carPattern = Encoding.ASCII.GetBytes("CAR_LIST");
        var idx = FindPattern(buffer, carPattern);
        if (idx >= 0 && idx + 12 < buffer.Length)
            return BitConverter.ToInt32(buffer, idx + 8);

        return 0;
    }

    private static int ExtractRaceCount(byte[] buffer, string text)
    {
        var match = Regex.Match(text, @"[Rr]aces?\s*[:=]?\s*(\d+)");
        if (match.Success) return int.Parse(match.Groups[1].Value);
        return 0;
    }

    private static int ExtractStarCount(byte[] buffer, string text)
    {
        var match = Regex.Match(text, @"[Ss]tars?\s*[:=]?\s*(\d+)");
        if (match.Success) return int.Parse(match.Groups[1].Value);
        return 0;
    }

    private static int ExtractPhotoCount(byte[] buffer, string text)
    {
        var match = Regex.Match(text, @"[Pp]hotos?\s*[:=]?\s*(\d+)");
        if (match.Success) return int.Parse(match.Groups[1].Value);
        return 0;
    }

    private static int ExtractShareCount(byte[] buffer, string text)
    {
        var match = Regex.Match(text, @"[Ss]hares?\s*[:=]?\s*(\d+)");
        if (match.Success) return int.Parse(match.Groups[1].Value);
        return 0;
    }

    private static int ExtractDistance(byte[] buffer, string text)
    {
        var match = Regex.Match(text, @"[Dd]istance\s*[:=]?\s*(\d+)");
        if (match.Success) return int.Parse(match.Groups[1].Value);
        return 0;
    }

    private static int ExtractSkillPoints(byte[] buffer, string text)
    {
        var match = Regex.Match(text, @"[Ss]kill\s*[Pp]oints?\s*[:=]?\s*(\d+)");
        if (match.Success) return int.Parse(match.Groups[1].Value);
        return 0;
    }

    private static int ExtractMaxSpeed(byte[] buffer, string text)
    {
        var match = Regex.Match(text, @"[Mm]ax\s*[Ss]peed\s*[:=]?\s*(\d+)");
        if (match.Success) return int.Parse(match.Groups[1].Value);

        for (var i = 0; i < buffer.Length - 4; i++)
        {
            var val = BitConverter.ToInt32(buffer, i);
            if (val is >= 100 and <= 999)
            {
                var prev = BitConverter.ToInt32(buffer, i - 4);
                if (prev is >= 1000 and <= 99999)
                    return val;
            }
        }
        return 0;
    }

    private static int ExtractSeasonCount(byte[] buffer, string text)
    {
        var match = Regex.Match(text, @"[Ss]eason\s*[:=]?\s*(\d+)");
        if (match.Success) return int.Parse(match.Groups[1].Value);
        return 0;
    }

    private static int ExtractApexCount(byte[] buffer, string text)
    {
        var match = Regex.Match(text, @"[Aa]pex\s*[:=]?\s*(\d+)");
        if (match.Success) return int.Parse(match.Groups[1].Value);
        return 0;
    }

    private static int FindPattern(byte[] buffer, byte[] pattern)
    {
        for (var i = 0; i <= buffer.Length - pattern.Length; i++)
        {
            var found = true;
            for (var j = 0; j < pattern.Length; j++)
            {
                if (buffer[i + j] != pattern[j])
                {
                    found = false;
                    break;
                }
            }
            if (found) return i;
        }
        return -1;
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
