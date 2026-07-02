using System.Text.RegularExpressions;
using EMECore.Core.Models;

namespace EMECore.Hardware.Services;

public class StellarBladeParser
{
    private static readonly Dictionary<string, string> TrophyMap = new()
    {
        { "Trophy_Platinum", "EVE Protocol" },
        { "Trophy_Activate_FirstCamp", "Camp Preparation" },
        { "Trophy_Activate_AllCamp", "Meticulous Explorer" },
        { "Trophy_KillCharacter", "Cruel Liberator" },
        { "Trophy_KillCharacter_Brute", "Brute" },
        { "Trophy_KillCharacter_AllNative", "Naytiba Researcher" },
        { "Trophy_Acquire_AllNanoSuit", "Nano Suit Collector" },
        { "Trophy_Acquire_AllSkill", "Thorough Technician" },
        { "Trophy_Acquire_AllSkill_v2", "Infinite Blade" },
        { "Trophy_Acquire_AllCan", "Can Collector" },
        { "Trophy_Acquire_AllRecords", "Records Collector" },
        { "Trophy_Open_AllBox", "Box Hunter" },
        { "Trophy_CompleteLevel_AltesLabor", "Altess Levoire" },
        { "Trophy_LevelUpMax_AllExoSpine", "Perfect Exospine" },
        { "Trophy_WeaponMaxUpgrade", "Perfect Blood Edge" },
        { "Trophy_TumblerMaxUpgrade", "Perfect Rechargeable Tumbler" },
        { "Trophy_BodyMaxUpgrade", "Perfect Physical Enhancement" },
        { "Trophy_BetaMaxUpgrade", "Perfect Beta Energy Enhancement" },
        { "Trophy_UseItem_Gold_At_Shop", "Shopper" },
        { "Trophy_CharKill_BetaSkill", "Naytiba Hunter" },
        { "Trophy_CharKill_BurstSkill", "Relentless Destroyer" },
        { "Trophy_CharKill_RangeSkill", "Cold-blooded Sniper" },
        { "Trophy_CharKill_AssassinationSkills", "Silent Executioner" },
        { "Trophy_JustEvade", "Battlefield Martial Artist" },
        { "Trophy_JustParry", "Agile Gladiator" },
    };

    public string? FindSavePath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var basePath = Path.Combine(localAppData, "SB", "Saved", "SaveGames");

        if (!Directory.Exists(basePath)) return null;

        foreach (var dir in Directory.GetDirectories(basePath))
        {
            var dirName = Path.GetFileName(dir);
            if (Regex.IsMatch(dirName, @"^\d{17}$"))
            {
                var saveFile = Path.Combine(dir, "StellarBladeSave00.sav");
                if (File.Exists(saveFile)) return saveFile;
            }
        }

        return null;
    }

    public bool HasSave() => FindSavePath() != null;

    public StellarBladeSaveData? ParseSave(string? savePath = null)
    {
        var filePath = savePath ?? FindSavePath();
        if (filePath == null || !File.Exists(filePath)) return null;

        var buffer = File.ReadAllBytes(filePath);
        var info = new FileInfo(filePath);

        var steamId = ExtractSteamId(buffer, filePath);

        var trophies = ParseTrophies(buffer);

        var questCompletions = ParseQuestCompletions(buffer);

        var endings = (killElder: ContainsValue(buffer, "EndingTimeStamp_KillElder"),
                       killLily: ContainsValue(buffer, "EndingTimeStamp_KillLily"),
                       saveLily: ContainsValue(buffer, "EndingTimeStamp_SaveLily"));

        var newGamePlusCount = ExtractNewGamePlusCount(buffer);

        return new StellarBladeSaveData
        {
            SteamId = steamId,
            SavePath = filePath,
            LastModified = info.LastWriteTimeUtc.ToString("o"),
            FileSize = info.Length,
            Trophies = trophies,
            QuestCompletions = questCompletions,
            KillElderEnding = endings.killElder,
            KillLilyEnding = endings.killLily,
            SaveLilyEnding = endings.saveLily,
            NewGamePlusCount = newGamePlusCount
        };
    }

    public List<Achievement> ParseAchievements(string? savePath = null)
    {
        var data = ParseSave(savePath);

        if (data == null)
        {
            return TrophyMap.Select(t => new Achievement
            {
                Apiname = t.Key,
                Name = t.Value,
                Description = "Trophy nao iniciado",
                Achieved = false
            }).ToList();
        }

        var achievements = new List<Achievement>();

        foreach (var trophy in data.Trophies)
        {
            achievements.Add(new Achievement
            {
                Apiname = trophy.Name,
                Name = trophy.SteamAchievement,
                Description = $"Trophy: {trophy.Name}",
                Achieved = trophy.BCompleted
            });
        }

        foreach (var quest in data.QuestCompletions)
        {
            var questName = quest switch
            {
                "Complete_Quest_Quest_Sub_032" => "Beyond Fate",
                "Complete_Quest_Quest_Sub_033" => "Sisterly Love",
                "Complete_Quest_Quest_Sub_043" => "Beep!",
                _ => quest
            };

            achievements.Add(new Achievement
            {
                Apiname = quest,
                Name = questName,
                Description = $"Quest completada: {quest}",
                Achieved = true
            });
        }

        if (data.KillElderEnding)
            achievements.Add(new Achievement { Apiname = "Ending_KillElder", Name = "Making New Memories", Description = "Elder ending", Achieved = true });
        if (data.KillLilyEnding)
            achievements.Add(new Achievement { Apiname = "Ending_KillLily", Name = "Cost of Lost Memories", Description = "Lily ending", Achieved = true });
        if (data.SaveLilyEnding)
            achievements.Add(new Achievement { Apiname = "Ending_SaveLily", Name = "Return to the Colony", Description = "Save Lily ending", Achieved = true });

        if (data.NewGamePlusCount > 0)
            achievements.Add(new Achievement { Apiname = "NewGamePlus", Name = "New Game+", Description = $"NG+ {data.NewGamePlusCount}x", Achieved = true });

        return achievements;
    }

    private static string ExtractSteamId(byte[] buffer, string filePath)
    {
        var text = System.Text.Encoding.ASCII.GetString(buffer);
        var match = Regex.Match(text[^Math.Min(200, text.Length)..], @"\d{17}");
        if (match.Success) return match.Value;

        var pathParts = filePath.Replace('\\', '/').Split('/');
        foreach (var part in pathParts)
        {
            if (Regex.IsMatch(part, @"^\d{17}$")) return part;
        }

        return "";
    }

    private List<StellarBladeTrophy> ParseTrophies(byte[] buffer)
    {
        var trophies = new List<StellarBladeTrophy>();
        var text = System.Text.Encoding.ASCII.GetString(buffer);

        foreach (var (name, steamAchievement) in TrophyMap)
        {
            var idx = text.IndexOf(name + '\0', StringComparison.Ordinal);

            if (idx >= 0)
            {
                var searchStart = idx + name.Length + 1;
                var bCompleted = ExtractBoolValue(buffer, searchStart);
                var progressValue = ExtractUInt32Value(buffer, searchStart);

                trophies.Add(new StellarBladeTrophy
                {
                    Name = name,
                    SteamAchievement = steamAchievement,
                    BCompleted = bCompleted,
                    ProgressValue = progressValue
                });
            }
            else
            {
                trophies.Add(new StellarBladeTrophy
                {
                    Name = name,
                    SteamAchievement = steamAchievement,
                    BCompleted = false,
                    ProgressValue = 0
                });
            }
        }

        return trophies;
    }

    private List<string> ParseQuestCompletions(byte[] buffer)
    {
        var text = System.Text.Encoding.ASCII.GetString(buffer);
        var matches = Regex.Matches(text, @"Complete_Quest_[A-Za-z0-9_]+");
        return matches.Select(m => m.Value).Distinct().ToList();
    }

    private static bool ExtractBoolValue(byte[] buffer, int searchStart)
    {
        var idx = FindPatternAscii(buffer, "BoolProperty", searchStart);
        if (idx < 0 || idx > searchStart + 300) return false;

        var valueOffset = idx + 21;
        if (valueOffset >= buffer.Length) return false;

        return buffer[valueOffset] == 1;
    }

    private static int ExtractUInt32Value(byte[] buffer, int searchStart)
    {
        var idx = FindPatternAscii(buffer, "UInt32Property", searchStart);
        if (idx < 0 || idx > searchStart + 200) return 0;

        var valueOffset = idx + 23;
        if (valueOffset + 4 > buffer.Length) return 0;

        return BitConverter.ToInt32(buffer, valueOffset);
    }

    private static int ExtractNewGamePlusCount(byte[] buffer)
    {
        var text = System.Text.Encoding.ASCII.GetString(buffer);
        var ngpIdx = text.IndexOf("NewGamePlusPlayCount", StringComparison.Ordinal);
        if (ngpIdx < 0) return 0;

        var uintIdx = FindPatternAscii(buffer, "UInt32Property", ngpIdx);
        if (uintIdx < 0 || uintIdx > ngpIdx + 200) return 0;

        var valueOffset = uintIdx + 23;
        if (valueOffset + 4 > buffer.Length) return 0;

        return BitConverter.ToInt32(buffer, valueOffset);
    }

    private static bool ContainsValue(byte[] buffer, string pattern)
    {
        var text = System.Text.Encoding.ASCII.GetString(buffer);
        var idx = text.IndexOf(pattern, StringComparison.Ordinal);
        return idx >= 0 && idx > 0 && buffer[idx - 1] != 0;
    }

    private static int FindPatternAscii(byte[] buffer, string pattern, int startOffset)
    {
        var text = System.Text.Encoding.ASCII.GetString(buffer);
        return text.IndexOf(pattern, startOffset, StringComparison.Ordinal);
    }
}
