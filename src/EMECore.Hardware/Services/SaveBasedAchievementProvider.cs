using System.Text.Json;
using EMECore.Core.Models;
using EMECore.Core.Services;

namespace EMECore.Hardware.Services;

public class SaveBasedAchievementProvider : IAchievementProvider
{
    public string ProviderName => "SaveBased";

    private readonly SaveDiscoveryService _saveDiscovery;
    private readonly SaveParserService _saveParser;
    private readonly AchievementDatabase? _achievementDb;
    private readonly Dictionary<string, AchievementDatabase> _gameDatabases = new();

    public SaveBasedAchievementProvider(SaveDiscoveryService saveDiscovery, SaveParserService saveParser)
    {
        _saveDiscovery = saveDiscovery;
        _saveParser = saveParser;
        _achievementDb = LoadDefaultDatabase();
    }

    public bool CanHandle(Game game)
    {
        if (IsOnlineOnly(game)) return false;
        if (HasHardcodedDetection(game)) return true;
        try
        {
            var locations = _saveDiscovery.GetKnownSaveLocations(game);
            return locations.Any(l => !string.IsNullOrEmpty(l.DirectoryPath) && Directory.Exists(l.DirectoryPath));
        }
        catch { return false; }
    }

    private static bool HasHardcodedDetection(Game game)
    {
        return IsSkyrim(game) || IsFallout4(game) || IsStarfield(game) ||
               IsWitcher3(game) || IsCyberpunk(game) || IsEldenRing(game) ||
               IsBaldursGate3(game) || IsHogwartsLegacy(game);
    }

    public async Task<List<Achievement>> GetAchievementsAsync(Game game)
    {
        return await CheckAchievementsAsync(game);
    }

    public async Task<List<Achievement>> CheckAchievementsAsync(Game game)
    {
        var achievements = new List<Achievement>();

        if (IsOnlineOnly(game))
            return achievements;

        try
        {
            var gameSaves = await _saveDiscovery.DiscoverSavesAsync(game);
            foreach (var gameSave in gameSaves)
            {
                var saveFiles = await _saveDiscovery.FindSaveFilesAsync(gameSave);
                foreach (var saveFile in saveFiles)
                {
                    try
                    {
                        var parsed = await _saveParser.ParseAsync(saveFile);
                        if (parsed.Count == 0) continue;

                        var gameAchievements = CheckAchievementsForGame(game, parsed);
                        achievements.AddRange(gameAchievements);
                    }
                    catch { }
                }
            }

            if (IsSkyrim(game))
            {
                try
                {
                    var skyrimAchievements = CheckSkyrimAchievements(game);
                    foreach (var a in skyrimAchievements)
                    {
                        if (!achievements.Any(x => x.Apiname == a.Apiname))
                            achievements.Add(a);
                    }
                }
                catch { }
            }

            if (IsFallout4(game))
            {
                try
                {
                    var falloutAchievements = CheckCreationEngineAchievements(game, "Fallout4");
                    foreach (var a in falloutAchievements)
                    {
                        if (!achievements.Any(x => x.Apiname == a.Apiname))
                            achievements.Add(a);
                    }
                }
                catch { }
            }

            if (IsStarfield(game))
            {
                try
                {
                    var starfieldAchievements = CheckCreationEngineAchievements(game, "Starfield");
                    foreach (var a in starfieldAchievements)
                    {
                        if (!achievements.Any(x => x.Apiname == a.Apiname))
                            achievements.Add(a);
                    }
                }
                catch { }
            }

            if (IsWitcher3(game))
            {
                try
                {
                    var witcherAchievements = CheckWitcher3Achievements(game);
                    foreach (var a in witcherAchievements)
                    {
                        if (!achievements.Any(x => x.Apiname == a.Apiname))
                            achievements.Add(a);
                    }
                }
                catch { }
            }

            if (IsCyberpunk(game))
            {
                try
                {
                    var cyberpunkAchievements = CheckCyberpunkAchievements(game);
                    foreach (var a in cyberpunkAchievements)
                    {
                        if (!achievements.Any(x => x.Apiname == a.Apiname))
                            achievements.Add(a);
                    }
                }
                catch { }
            }

            if (IsEldenRing(game))
            {
                try
                {
                    var eldenAchievements = CheckEldenRingAchievements(game);
                    foreach (var a in eldenAchievements)
                    {
                        if (!achievements.Any(x => x.Apiname == a.Apiname))
                            achievements.Add(a);
                    }
                }
                catch { }
            }

            if (IsBaldursGate3(game))
            {
                try
                {
                    var bg3Achievements = CheckBaldursGate3Achievements(game);
                    foreach (var a in bg3Achievements)
                    {
                        if (!achievements.Any(x => x.Apiname == a.Apiname))
                            achievements.Add(a);
                    }
                }
                catch { }
            }

            if (IsHogwartsLegacy(game))
            {
                try
                {
                    var hlAchievements = CheckHogwartsLegacyAchievements(game);
                    foreach (var a in hlAchievements)
                    {
                        if (!achievements.Any(x => x.Apiname == a.Apiname))
                            achievements.Add(a);
                    }
                }
                catch { }
            }

            if (IsRDR2(game))
            {
                try
                {
                    var rdr2Achievements = CheckRDR2Achievements(game);
                    foreach (var a in rdr2Achievements)
                    {
                        if (!achievements.Any(x => x.Apiname == a.Apiname))
                            achievements.Add(a);
                    }
                }
                catch { }
            }
        }
        catch { }

        return achievements;
    }

    public async Task<List<Achievement>> RefreshAchievementsAsync(Game game)
    {
        return await CheckAchievementsAsync(game);
    }

    private List<Achievement> CheckAchievementsForGame(Game game, Dictionary<string, object> saveData)
    {
        var achievements = new List<Achievement>();

        var db = GetAchievementDatabase(game);
        if (db == null || db.Achievements.Count == 0)
        {
            achievements.AddRange(CreateGenericAchievements(game, saveData));
            return achievements;
        }

        foreach (var def in db.Achievements)
        {
            var conditionMet = EvaluateCondition(saveData, def.Condition);
            var progress = CalculateProgress(saveData, def);

            achievements.Add(new Achievement
            {
                GameId = game.Id,
                Apiname = def.Apiname,
                Achieved = conditionMet,
                Unlocktime = conditionMet ? DateTimeOffset.UtcNow.ToUnixTimeSeconds() : 0,
                Name = def.Name,
                Description = def.Description,
                Icon = def.Icon,
                Icongray = def.IconGray,
                Progress = progress,
                MaxProgress = def.MaxProgress
            });
        }

        return achievements;
    }

    private AchievementDatabase? GetAchievementDatabase(Game game)
    {
        if (_gameDatabases.TryGetValue(game.Id, out var cached))
            return cached;

        var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config", "achievements", $"{game.Id}.json");
        if (File.Exists(dbPath))
        {
            var db = AchievementDatabase.LoadFromFile(dbPath);
            if (db != null)
            {
                _gameDatabases[game.Id] = db;
                return db;
            }
        }

        var steamDbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config", "achievements", $"steam_{game.SteamAppId}.json");
        if (File.Exists(steamDbPath))
        {
            var db = AchievementDatabase.LoadFromFile(steamDbPath);
            if (db != null)
            {
                _gameDatabases[game.Id] = db;
                return db;
            }
        }

        return _achievementDb;
    }

    private AchievementDatabase? LoadDefaultDatabase()
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config", "achievements", "database.json");
        return AchievementDatabase.LoadFromFile(path);
    }

    private static bool EvaluateCondition(Dictionary<string, object> data, AchievementCondition condition)
    {
        return condition.Type switch
        {
            ConditionType.KeyExists => data.ContainsKey(condition.SaveKey),
            ConditionType.KeyValue => EvaluateKeyValue(data, condition),
            ConditionType.FlagSet => EvaluateFlagSet(data, condition),
            ConditionType.CounterReached => EvaluateCounterReached(data, condition),
            ConditionType.MultipleConditions => EvaluateMultipleConditions(data, condition),
            _ => false
        };
    }

    private static bool EvaluateKeyValue(Dictionary<string, object> data, AchievementCondition condition)
    {
        if (!TryGetNestedValue(data, condition.SaveKey, out var actualValue))
            return false;

        return CompareValues(actualValue, condition.ExpectedValue, condition.Op);
    }

    private static bool EvaluateFlagSet(Dictionary<string, object> data, AchievementCondition condition)
    {
        if (!TryGetNestedValue(data, condition.SaveKey, out var value))
            return false;

        if (value is bool boolVal)
            return boolVal;
        if (value is int intVal)
            return intVal != 0;
        if (value is long longVal)
            return longVal != 0;
        if (value is JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Number => element.GetInt32() != 0,
                _ => false
            };
        }
        if (value is string strVal)
            return !string.IsNullOrEmpty(strVal) && strVal != "0" && strVal.ToLower() != "false";

        return false;
    }

    private static bool EvaluateCounterReached(Dictionary<string, object> data, AchievementCondition condition)
    {
        if (!TryGetNestedValue(data, condition.SaveKey, out var value))
            return false;

        var actual = Convert.ToDouble(value);
        var expected = Convert.ToDouble(condition.ExpectedValue ?? 0);

        return condition.Op switch
        {
            ConditionOperator.Equals => Math.Abs(actual - expected) < 0.001,
            ConditionOperator.GreaterThan => actual > expected,
            ConditionOperator.GreaterOrEqual => actual >= expected,
            ConditionOperator.LessThan => actual < expected,
            ConditionOperator.LessOrEqual => actual <= expected,
            _ => false
        };
    }

    private static bool EvaluateMultipleConditions(Dictionary<string, object> data, AchievementCondition condition)
    {
        if (condition.SubConditions == null || condition.SubConditions.Count == 0)
            return false;

        foreach (var sub in condition.SubConditions)
        {
            var subCond = new AchievementCondition
            {
                SaveKey = sub.SaveKey,
                Op = sub.Op,
                ExpectedValue = sub.ExpectedValue,
                Type = ConditionType.KeyValue
            };
            if (!EvaluateCondition(data, subCond))
                return false;
        }

        return true;
    }

    private static bool CompareValues(object? actual, object? expected, ConditionOperator op)
    {
        if (actual == null || expected == null)
            return false;

        if (actual is JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Number => CompareNumeric(element.GetDouble(), Convert.ToDouble(expected ?? 0), op),
                JsonValueKind.String => CompareString(element.GetString() ?? "", expected?.ToString() ?? "", op),
                JsonValueKind.True => expected is bool b && b,
                JsonValueKind.False => expected is bool b2 && !b2,
                _ => false
            };
        }

        if (actual is string strActual)
            return CompareString(strActual, expected.ToString() ?? "", op);

        if (double.TryParse(actual.ToString(), out var numActual) && double.TryParse(expected.ToString(), out var numExpected))
            return CompareNumeric(numActual, numExpected, op);

        return false;
    }

    private static bool CompareNumeric(double actual, double expected, ConditionOperator op)
    {
        return op switch
        {
            ConditionOperator.Equals => Math.Abs(actual - expected) < 0.001,
            ConditionOperator.NotEquals => Math.Abs(actual - expected) >= 0.001,
            ConditionOperator.GreaterThan => actual > expected,
            ConditionOperator.GreaterOrEqual => actual >= expected,
            ConditionOperator.LessThan => actual < expected,
            ConditionOperator.LessOrEqual => actual <= expected,
            _ => false
        };
    }

    private static bool CompareString(string actual, string expected, ConditionOperator op)
    {
        return op switch
        {
            ConditionOperator.Equals => string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase),
            ConditionOperator.NotEquals => !string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase),
            ConditionOperator.Contains => actual.Contains(expected, StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static bool TryGetNestedValue(Dictionary<string, object> data, string keyPath, out object? value)
    {
        value = null;
        var keys = keyPath.Split('.');
        object current = data;

        foreach (var key in keys)
        {
            if (current is Dictionary<string, object> dict && dict.TryGetValue(key, out var next))
            {
                current = next;
            }
            else if (current is JsonElement element)
            {
                if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(key, out var prop))
                {
                    current = prop;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        value = current;
        return true;
    }

    private static int CalculateProgress(Dictionary<string, object> data, AchievementDefinition def)
    {
        if (def.MaxProgress <= 0) return 0;

        if (!TryGetNestedValue(data, def.Condition.SaveKey, out var value))
            return 0;

        try
        {
            var numVal = Convert.ToInt32(value);
            return Math.Min(numVal, def.MaxProgress);
        }
        catch
        {
            return 0;
        }
    }

    private static List<Achievement> CreateGenericAchievements(Game game, Dictionary<string, object> saveData)
    {
        var achievements = new List<Achievement>();

        var interestingKeys = FindInterestingKeys(saveData);
        foreach (var key in interestingKeys)
        {
            TryGetNestedValue(saveData, key, out var value);
            achievements.Add(new Achievement
            {
                GameId = game.Id,
                Apiname = $"generic_{key.Replace('.', '_')}",
                Achieved = true,
                Unlocktime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Name = FormatKeyName(key),
                Description = $"Detectado no save: {value}",
                Icon = "",
                Icongray = ""
            });
        }

        return achievements;
    }

    private static List<string> FindInterestingKeys(Dictionary<string, object> data, string prefix = "", int depth = 0)
    {
        var keys = new List<string>();
        if (depth > 5) return keys;

        foreach (var kvp in data)
        {
            if (kvp.Key.StartsWith('_')) continue;
            var fullPath = string.IsNullOrEmpty(prefix) ? kvp.Key : $"{prefix}.{kvp.Key}";

            if (kvp.Value is Dictionary<string, object> nested)
            {
                keys.AddRange(FindInterestingKeys(nested, fullPath, depth + 1));
            }
            else
            {
                keys.Add(fullPath);
            }
        }

        return keys;
    }

    private static string FormatKeyName(string key)
    {
        return key.Replace('.', ' ').Replace('_', ' ')
            .Split(' ')
            .Select(word => char.ToUpper(word[0]) + word[1..])
            .Aggregate((a, b) => $"{a} {b}");
    }

    private static bool IsSkyrim(Game game)
    {
        return game.Name.Contains("Skyrim", StringComparison.OrdinalIgnoreCase) ||
               game.ExecutablePath.Contains("Skyrim", StringComparison.OrdinalIgnoreCase) ||
               game.SteamAppId == "72850" ||
               game.SteamAppId == "489830";
    }

    private List<Achievement> CheckSkyrimAchievements(Game game)
    {
        var achievements = new List<Achievement>();
        var parser = new SkyrimSaveParser();

        var skyrimPaths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games", "Skyrim Special Edition", "Saves"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "My Games", "Skyrim", "Saves"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Skyrim", "Saves"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Skyrim", "Saves")
        };

        var latestSave = (string?)null;
        var latestTime = DateTime.MinValue;

        foreach (var savePath in skyrimPaths)
        {
            if (!Directory.Exists(savePath)) continue;
            try
            {
                foreach (var file in Directory.GetFiles(savePath, "*.ess"))
                {
                    var info = new FileInfo(file);
                    if (info.LastWriteTime > latestTime)
                    {
                        latestTime = info.LastWriteTime;
                        latestSave = file;
                    }
                }
            }
            catch { }
        }

        var exeDir = Path.GetDirectoryName(game.ExecutablePath) ?? "";
        if (!string.IsNullOrEmpty(exeDir))
        {
            try
            {
                foreach (var file in Directory.GetFiles(exeDir, "*.ess", SearchOption.AllDirectories))
                {
                    var info = new FileInfo(file);
                    if (info.LastWriteTime > latestTime)
                    {
                        latestTime = info.LastWriteTime;
                        latestSave = file;
                    }
                }
            }
            catch { }
        }

        if (latestSave == null) return achievements;

        var saveData = parser.ParseFromFile(latestSave);
        if (saveData == null) return achievements;

        achievements.Add(new Achievement
        {
            GameId = game.Id,
            Apiname = "skyrim_level",
            Achieved = saveData.Level > 0,
            Unlocktime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Name = $"Nível {saveData.Level}",
            Description = $"Jogador no nível {saveData.Level}",
            Progress = saveData.Level,
            MaxProgress = 81
        });

        achievements.Add(new Achievement
        {
            GameId = game.Id,
            Apiname = "skyrim_playtime",
            Achieved = saveData.PlayTimeHours > 0,
            Unlocktime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Name = $"{saveData.PlayTimeHours}h jogadas",
            Description = $"Tempo total de jogo: {saveData.PlayTimeHours}h",
            Progress = saveData.PlayTimeHours,
            MaxProgress = 100
        });

        achievements.Add(new Achievement
        {
            GameId = game.Id,
            Apiname = "skyrim_player_name",
            Achieved = !string.IsNullOrEmpty(saveData.PlayerName),
            Unlocktime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Name = saveData.PlayerName,
            Description = $"Personagem: {saveData.PlayerName} - {saveData.RaceSex}",
            Progress = 1,
            MaxProgress = 1
        });

        achievements.Add(new Achievement
        {
            GameId = game.Id,
            Apiname = "skyrim_location",
            Achieved = !string.IsNullOrEmpty(saveData.PlayerLocation),
            Unlocktime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Name = saveData.PlayerLocation,
            Description = $"Última localização: {saveData.PlayerLocation}",
            Progress = 1,
            MaxProgress = 1
        });

        var db = GetAchievementDatabase(game);
        if (db != null)
        {
            foreach (var def in db.Achievements)
            {
                var conditionMet = EvaluateSkyrimCondition(saveData, def.Condition);
                var progress = CalculateSkyrimProgress(saveData, def);

                achievements.Add(new Achievement
                {
                    GameId = game.Id,
                    Apiname = def.Apiname,
                    Achieved = conditionMet,
                    Unlocktime = conditionMet ? DateTimeOffset.UtcNow.ToUnixTimeSeconds() : 0,
                    Name = def.Name,
                    Description = def.Description,
                    Icon = def.Icon,
                    Icongray = def.IconGray,
                    Progress = progress,
                    MaxProgress = def.MaxProgress
                });
            }
        }

        return achievements;
    }

    private static bool EvaluateSkyrimCondition(SkyrimSaveData save, AchievementCondition condition)
    {
        return condition.Type switch
        {
            ConditionType.KeyExists => true,
            ConditionType.KeyValue => EvaluateSkyrimKeyValue(save, condition),
            ConditionType.FlagSet => EvaluateSkyrimKeyValue(save, condition),
            ConditionType.CounterReached => EvaluateSkyrimKeyValue(save, condition),
            _ => false
        };
    }

    private static bool EvaluateSkyrimKeyValue(SkyrimSaveData save, AchievementCondition condition)
    {
        var key = condition.SaveKey.ToLowerInvariant();
        double actual = 0;
        double expected = Convert.ToDouble(condition.ExpectedValue ?? 0);

        if (key.Contains("level")) actual = save.Level;
        else if (key.Contains("kill")) actual = save.TotalKills;
        else if (key.Contains("dragon")) actual = save.DragonsSlain;
        else if (key.Contains("word")) actual = save.WordsLearned;
        else if (key.Contains("stone") && key.Contains("barenziah")) actual = save.BarenziahStones;
        else if (key.Contains("dungeon")) actual = save.DungeonsCleared;
        else if (key.Contains("location")) actual = save.LocationsDiscovered;
        else if (key.Contains("house")) actual = save.HousesOwned;
        else if (key.Contains("married")) actual = save.IsMarried ? 1 : 0;
        else if (key.Contains("adopt")) actual = save.HasAdopted ? 1 : 0;
        else if (key.Contains("playtime") || key.Contains("hours")) actual = save.PlayTimeHours;
        else if (key.Contains("bounty")) actual = save.StatsData.GetValueOrDefault("stat_bounty", 0);
        else if (key.Contains("gold")) actual = save.StatsData.GetValueOrDefault("stat_gold", 0);
        else if (key.Contains("skill") || key.Contains("100"))
        {
            foreach (var skill in save.SkillData)
            {
                if (skill.Value >= 100) { actual = 100; break; }
            }
        }

        return condition.Op switch
        {
            ConditionOperator.Equals => Math.Abs(actual - expected) < 0.001,
            ConditionOperator.GreaterOrEqual => actual >= expected,
            ConditionOperator.GreaterThan => actual > expected,
            ConditionOperator.LessThan => actual < expected,
            ConditionOperator.LessOrEqual => actual <= expected,
            _ => false
        };
    }

    private static int CalculateSkyrimProgress(SkyrimSaveData save, AchievementDefinition def)
    {
        if (def.MaxProgress <= 0) return 0;

        var key = def.Condition.SaveKey.ToLowerInvariant();
        double current = 0;

        if (key.Contains("level")) current = save.Level;
        else if (key.Contains("kill")) current = save.TotalKills;
        else if (key.Contains("dragon")) current = save.DragonsSlain;
        else if (key.Contains("word")) current = save.WordsLearned;
        else if (key.Contains("stone")) current = save.BarenziahStones;
        else if (key.Contains("dungeon")) current = save.DungeonsCleared;
        else if (key.Contains("location")) current = save.LocationsDiscovered;
        else if (key.Contains("house")) current = save.HousesOwned;
        else if (key.Contains("playtime") || key.Contains("hours")) current = save.PlayTimeHours;

        return (int)Math.Min(current, def.MaxProgress);
    }

    private static bool IsFallout4(Game game)
    {
        return game.Name.Contains("Fallout 4", StringComparison.OrdinalIgnoreCase) ||
               game.ExecutablePath.Contains("Fallout4", StringComparison.OrdinalIgnoreCase) ||
               game.SteamAppId == "377160";
    }

    private static bool IsOnlineOnly(Game game)
    {
        var name = game.Name.ToLowerInvariant();
        var onlineOnlyGames = new[]
        {
            "valorant", "league of legends", "fortnite", "apex legends",
            "overwatch", "diablo iv", "diablo 4", "destiny 2", "genshin impact",
            "roblox", "minecraft legends", "the finals", "helldivers",
            "call of duty", "warzone", "destiny", "lost ark", "new world",
            "world of warcraft", "final fantasy xiv", "ffxiv", "elder scrolls online",
            "eso", "guild wars 2", "path of exile", "rocket league",
            "counter-strike", "cs2", "csgo", "dota 2", "pubg", "pubg mobile",
            "naraka bladepoint", "multiVersus", "fall guys", "among us",
            "rust", "dayz", "ark survival", "conan exiles", "valheim"
        };

        foreach (var gameName in onlineOnlyGames)
        {
            if (name.Contains(gameName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool IsStarfield(Game game)
    {
        return game.Name.Contains("Starfield", StringComparison.OrdinalIgnoreCase) ||
               game.ExecutablePath.Contains("Starfield", StringComparison.OrdinalIgnoreCase) ||
               game.SteamAppId == "1716740";
    }

    private static bool IsWitcher3(Game game)
    {
        return game.Name.Contains("Witcher 3", StringComparison.OrdinalIgnoreCase) ||
               game.Name.Contains("The Witcher", StringComparison.OrdinalIgnoreCase) ||
               game.ExecutablePath.Contains("Witcher3", StringComparison.OrdinalIgnoreCase) ||
               game.SteamAppId == "292030";
    }

    private static bool IsCyberpunk(Game game)
    {
        return game.Name.Contains("Cyberpunk", StringComparison.OrdinalIgnoreCase) ||
               game.ExecutablePath.Contains("Cyberpunk2077", StringComparison.OrdinalIgnoreCase) ||
               game.SteamAppId == "1091500";
    }

    private static bool IsEldenRing(Game game)
    {
        return game.Name.Contains("Elden Ring", StringComparison.OrdinalIgnoreCase) ||
               game.ExecutablePath.Contains("elden", StringComparison.OrdinalIgnoreCase) ||
               game.SteamAppId == "1245620";
    }

    private static bool IsBaldursGate3(Game game)
    {
        return game.Name.Contains("Baldur", StringComparison.OrdinalIgnoreCase) ||
               game.Name.Contains("BG3", StringComparison.OrdinalIgnoreCase) ||
               game.ExecutablePath.Contains("bg3", StringComparison.OrdinalIgnoreCase) ||
               game.SteamAppId == "1086940";
    }

    private static bool IsHogwartsLegacy(Game game)
    {
        return game.Name.Contains("Hogwarts", StringComparison.OrdinalIgnoreCase) ||
               game.ExecutablePath.Contains("Hogwarts", StringComparison.OrdinalIgnoreCase) ||
               game.SteamAppId == "990080";
    }

    private static bool IsRDR2(Game game)
    {
        return game.Name.Contains("Red Dead", StringComparison.OrdinalIgnoreCase) ||
               game.Name.Contains("RDR2", StringComparison.OrdinalIgnoreCase) ||
               game.ExecutablePath.Contains("RDR2", StringComparison.OrdinalIgnoreCase) ||
               game.SteamAppId == "1174180";
    }

    private List<Achievement> CheckCreationEngineAchievements(Game game, string engine)
    {
        var achievements = new List<Achievement>();
        var parser = new CreationEngineSaveParser();

        var savePaths = GetSavePathsForGame(game);
        var latestSave = FindLatestSave(savePaths, "*.fos");

        if (engine == "Starfield")
            latestSave = FindLatestSave(savePaths, "*.sfs");

        if (latestSave == null) return achievements;

        var saveData = parser.ParseFromFile(latestSave);
        if (saveData == null) return achievements;

        var db = GetAchievementDatabase(game);
        if (db != null)
        {
            foreach (var def in db.Achievements)
            {
                var conditionMet = EvaluateGenericCondition(saveData.StatsData, saveData.QuestData, def.Condition);
                var progress = CalculateGenericProgress(saveData.StatsData, def);

                achievements.Add(new Achievement
                {
                    GameId = game.Id,
                    Apiname = def.Apiname,
                    Achieved = conditionMet,
                    Unlocktime = conditionMet ? DateTimeOffset.UtcNow.ToUnixTimeSeconds() : 0,
                    Name = def.Name,
                    Description = def.Description,
                    Icon = def.Icon,
                    Icongray = def.IconGray,
                    Progress = progress,
                    MaxProgress = def.MaxProgress
                });
            }
        }

        return achievements;
    }

    private List<Achievement> CheckWitcher3Achievements(Game game)
    {
        var achievements = new List<Achievement>();
        var parser = new Witcher3SaveParser();

        var savePaths = GetSavePathsForGame(game);
        var latestSave = FindLatestSave(savePaths, "*.sav");

        if (latestSave == null) return achievements;

        var saveData = parser.ParseFromFile(latestSave);
        if (saveData == null) return achievements;

        var db = GetAchievementDatabase(game);
        if (db != null)
        {
            foreach (var def in db.Achievements)
            {
                var conditionMet = EvaluateGenericCondition(saveData.StatsData, saveData.QuestData, def.Condition);
                var progress = CalculateGenericProgress(saveData.StatsData, def);

                achievements.Add(new Achievement
                {
                    GameId = game.Id,
                    Apiname = def.Apiname,
                    Achieved = conditionMet,
                    Unlocktime = conditionMet ? DateTimeOffset.UtcNow.ToUnixTimeSeconds() : 0,
                    Name = def.Name,
                    Description = def.Description,
                    Icon = def.Icon,
                    Icongray = def.IconGray,
                    Progress = progress,
                    MaxProgress = def.MaxProgress
                });
            }
        }

        return achievements;
    }

    private List<Achievement> CheckCyberpunkAchievements(Game game)
    {
        var achievements = new List<Achievement>();
        var parser = new CyberpunkSaveParser();

        var savePaths = GetSavePathsForGame(game);
        var latestDir = FindLatestCyberpunkSaveDir(savePaths);

        if (latestDir == null) return achievements;

        var saveData = parser.ParseFromDirectory(latestDir);
        if (saveData == null) return achievements;

        var db = GetAchievementDatabase(game);
        if (db != null)
        {
            foreach (var def in db.Achievements)
            {
                var conditionMet = EvaluateGenericCondition(saveData.StatsData, saveData.QuestData, def.Condition);
                var progress = CalculateGenericProgress(saveData.StatsData, def);

                achievements.Add(new Achievement
                {
                    GameId = game.Id,
                    Apiname = def.Apiname,
                    Achieved = conditionMet,
                    Unlocktime = conditionMet ? DateTimeOffset.UtcNow.ToUnixTimeSeconds() : 0,
                    Name = def.Name,
                    Description = def.Description,
                    Icon = def.Icon,
                    Icongray = def.IconGray,
                    Progress = progress,
                    MaxProgress = def.MaxProgress
                });
            }
        }

        return achievements;
    }

    private List<Achievement> CheckEldenRingAchievements(Game game)
    {
        var achievements = new List<Achievement>();
        var parser = new EldenRingSaveParser();

        var savePaths = GetSavePathsForGame(game);
        var latestSave = FindLatestSave(savePaths, "*.sl2");

        if (latestSave == null) return achievements;

        var saveData = parser.ParseFromFile(latestSave);
        if (saveData == null) return achievements;

        var db = GetAchievementDatabase(game);
        if (db != null)
        {
            foreach (var def in db.Achievements)
            {
                var conditionMet = EvaluateGenericCondition(saveData.StatsData, saveData.QuestData, def.Condition);
                var progress = CalculateGenericProgress(saveData.StatsData, def);

                achievements.Add(new Achievement
                {
                    GameId = game.Id,
                    Apiname = def.Apiname,
                    Achieved = conditionMet,
                    Unlocktime = conditionMet ? DateTimeOffset.UtcNow.ToUnixTimeSeconds() : 0,
                    Name = def.Name,
                    Description = def.Description,
                    Icon = def.Icon,
                    Icongray = def.IconGray,
                    Progress = progress,
                    MaxProgress = def.MaxProgress
                });
            }
        }

        return achievements;
    }

    private List<Achievement> CheckBaldursGate3Achievements(Game game)
    {
        var achievements = new List<Achievement>();
        var parser = new BaldursGate3SaveParser();

        var savePaths = GetSavePathsForGame(game);
        var latestSave = FindLatestSave(savePaths, "*.lsv");

        if (latestSave == null) return achievements;

        var saveData = parser.ParseFromFile(latestSave);
        if (saveData == null) return achievements;

        var db = GetAchievementDatabase(game);
        if (db != null)
        {
            foreach (var def in db.Achievements)
            {
                var conditionMet = EvaluateGenericCondition(saveData.StatsData, saveData.QuestData, def.Condition);
                var progress = CalculateGenericProgress(saveData.StatsData, def);

                achievements.Add(new Achievement
                {
                    GameId = game.Id,
                    Apiname = def.Apiname,
                    Achieved = conditionMet,
                    Unlocktime = conditionMet ? DateTimeOffset.UtcNow.ToUnixTimeSeconds() : 0,
                    Name = def.Name,
                    Description = def.Description,
                    Icon = def.Icon,
                    Icongray = def.IconGray,
                    Progress = progress,
                    MaxProgress = def.MaxProgress
                });
            }
        }

        return achievements;
    }

    private List<Achievement> CheckHogwartsLegacyAchievements(Game game)
    {
        var achievements = new List<Achievement>();
        var parser = new HogwartsLegacySaveParser();

        var savePaths = GetSavePathsForGame(game);
        var latestSave = FindLatestSave(savePaths, "*.sav");

        if (latestSave == null) return achievements;

        var saveData = parser.ParseFromFile(latestSave);
        if (saveData == null) return achievements;

        var db = GetAchievementDatabase(game);
        if (db != null)
        {
            foreach (var def in db.Achievements)
            {
                var conditionMet = EvaluateGenericCondition(saveData.StatsData, saveData.QuestData, def.Condition);
                var progress = CalculateGenericProgress(saveData.StatsData, def);

                achievements.Add(new Achievement
                {
                    GameId = game.Id,
                    Apiname = def.Apiname,
                    Achieved = conditionMet,
                    Unlocktime = conditionMet ? DateTimeOffset.UtcNow.ToUnixTimeSeconds() : 0,
                    Name = def.Name,
                    Description = def.Description,
                    Icon = def.Icon,
                    Icongray = def.IconGray,
                    Progress = progress,
                    MaxProgress = def.MaxProgress
                });
            }
        }

        return achievements;
    }

    private List<Achievement> CheckRDR2Achievements(Game game)
    {
        var achievements = new List<Achievement>();
        var parser = new RDR2SaveParser();

        var savePaths = GetSavePathsForGame(game);
        var latestSave = FindLatestSave(savePaths, "SRDR3*");

        if (latestSave == null) return achievements;

        var saveData = parser.ParseFromFile(latestSave);
        if (saveData == null) return achievements;

        var db = GetAchievementDatabase(game);
        if (db != null)
        {
            foreach (var def in db.Achievements)
            {
                var conditionMet = EvaluateGenericCondition(saveData.StatsData, saveData.QuestData, def.Condition);
                var progress = CalculateGenericProgress(saveData.StatsData, def);

                achievements.Add(new Achievement
                {
                    GameId = game.Id,
                    Apiname = def.Apiname,
                    Achieved = conditionMet,
                    Unlocktime = conditionMet ? DateTimeOffset.UtcNow.ToUnixTimeSeconds() : 0,
                    Name = def.Name,
                    Description = def.Description,
                    Icon = def.Icon,
                    Icongray = def.IconGray,
                    Progress = progress,
                    MaxProgress = def.MaxProgress
                });
            }
        }

        return achievements;
    }

    private static bool EvaluateGenericCondition(Dictionary<string, long> stats, Dictionary<string, bool> quests, AchievementCondition condition)
    {
        return condition.Type switch
        {
            ConditionType.KeyExists => stats.ContainsKey(condition.SaveKey) || quests.ContainsKey(condition.SaveKey),
            ConditionType.KeyValue => EvaluateGenericKeyValue(stats, condition),
            ConditionType.FlagSet => quests.GetValueOrDefault(condition.SaveKey, false),
            ConditionType.CounterReached => EvaluateGenericKeyValue(stats, condition),
            _ => false
        };
    }

    private static bool EvaluateGenericKeyValue(Dictionary<string, long> stats, AchievementCondition condition)
    {
        var key = condition.SaveKey.ToLowerInvariant();
        double actual = 0;

        foreach (var kvp in stats)
        {
            if (kvp.Key.ToLowerInvariant().Contains(key))
            {
                actual = kvp.Value;
                break;
            }
        }

        double expected = Convert.ToDouble(condition.ExpectedValue ?? 0);

        return condition.Op switch
        {
            ConditionOperator.Equals => Math.Abs(actual - expected) < 0.001,
            ConditionOperator.GreaterOrEqual => actual >= expected,
            ConditionOperator.GreaterThan => actual > expected,
            ConditionOperator.LessThan => actual < expected,
            ConditionOperator.LessOrEqual => actual <= expected,
            _ => false
        };
    }

    private static int CalculateGenericProgress(Dictionary<string, long> stats, AchievementDefinition def)
    {
        if (def.MaxProgress <= 0) return 0;

        var key = def.Condition.SaveKey.ToLowerInvariant();
        double current = 0;

        foreach (var kvp in stats)
        {
            if (kvp.Key.ToLowerInvariant().Contains(key))
            {
                current = kvp.Value;
                break;
            }
        }

        return (int)Math.Min(current, def.MaxProgress);
    }

    private List<string> GetSavePathsForGame(Game game)
    {
        var paths = new List<string>();
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (IsFallout4(game))
        {
            paths.Add(Path.Combine(documents, "My Games", "Fallout4", "Saves"));
        }
        else if (IsStarfield(game))
        {
            paths.Add(Path.Combine(documents, "My Games", "Starfield", "Saves"));
        }
        else if (IsWitcher3(game))
        {
            paths.Add(Path.Combine(documents, "The Witcher 3", "gamesaves"));
        }
        else if (IsCyberpunk(game))
        {
            paths.Add(Path.Combine(userProfile, "Saved Games", "CD Projekt Red", "Cyberpunk 2077"));
        }
        else if (IsEldenRing(game))
        {
            paths.Add(Path.Combine(appData, "Roaming", "EldenRing"));
        }
        else if (IsBaldursGate3(game))
        {
            paths.Add(Path.Combine(localAppData, "Larian Studios", "Baldur's Gate 3", "PlayerProfiles", "Public", "Savegames", "Story"));
        }
        else if (IsHogwartsLegacy(game))
        {
            paths.Add(Path.Combine(localAppData, "Hogwarts Legacy", "Saved", "SaveGames"));
        }
        else if (IsRDR2(game))
        {
            var rdrProfiles = Path.Combine(documents, "Rockstar Games", "Red Dead Redemption 2", "Profiles");
            if (Directory.Exists(rdrProfiles))
            {
                foreach (var profile in Directory.GetDirectories(rdrProfiles))
                    paths.Add(profile);
            }
        }

        var exeDir = Path.GetDirectoryName(game.ExecutablePath) ?? "";
        if (!string.IsNullOrEmpty(exeDir))
        {
            paths.Add(Path.Combine(exeDir, "Saves"));
            paths.Add(Path.Combine(exeDir, "SaveGames"));
        }

        return paths;
    }

    private static string? FindLatestSave(List<string> savePaths, string pattern)
    {
        string? latestSave = null;
        var latestTime = DateTime.MinValue;

        foreach (var savePath in savePaths)
        {
            if (!Directory.Exists(savePath)) continue;
            try
            {
                foreach (var file in Directory.GetFiles(savePath, pattern, SearchOption.AllDirectories))
                {
                    var info = new FileInfo(file);
                    if (info.LastWriteTime > latestTime)
                    {
                        latestTime = info.LastWriteTime;
                        latestSave = file;
                    }
                }
            }
            catch { }
        }

        return latestSave;
    }

    private static string? FindLatestCyberpunkSaveDir(List<string> savePaths)
    {
        string? latestDir = null;
        var latestTime = DateTime.MinValue;

        foreach (var savePath in savePaths)
        {
            if (!Directory.Exists(savePath)) continue;
            try
            {
                foreach (var dir in Directory.GetDirectories(savePath))
                {
                    var jsonFiles = Directory.GetFiles(dir, "*.json");
                    if (jsonFiles.Length == 0) continue;

                    var latestJson = jsonFiles.OrderByDescending(f => File.GetLastWriteTime(f)).First();
                    var info = new FileInfo(latestJson);
                    if (info.LastWriteTime > latestTime)
                    {
                        latestTime = info.LastWriteTime;
                        latestDir = dir;
                    }
                }
            }
            catch { }
        }

        return latestDir;
    }
}
