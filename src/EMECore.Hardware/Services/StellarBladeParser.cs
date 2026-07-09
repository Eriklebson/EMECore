using System.Text.RegularExpressions;
using EMECore.Core.Models;

namespace EMECore.Hardware.Services;

public class StellarBladeParser
{
    private static readonly Dictionary<string, (string English, string Portuguese, string Description)> TrophyMap = new()
    {
        { "01_Platinum", ("EVE Protocol", "Protocolo EVE", "Adquira todos os troféus") },
        { "02_Activate_FirstCamp", ("Camp Preparation", "Preparação de Acampamento", "Ative o primeiro acampamento") },
        { "26_Activate_AllCamp", ("Meticulous Explorer", "Explorador Metódico", "Ative todos os acampamentos") },
        { "43_KillCharacter", ("Cruel Liberator", "Libertador Cruel", "Derrote 1500 inimigos") },
        { "06_KillCharacter_Brute", ("Brute", "Bruto", "Derrote o Bruto") },
        { "25_KillCharacter_AllNative", ("Naytiba Researcher", "Pesquisador Naytiba", "Obtenha informações sobre todos os Naytibas") },
        { "21_Acquire_AllNanoSuit", ("Nano Suit Collector", "Coletraje de Nano Trajes", "Adquira 30 Nano Trajes") },
        { "32_Acquire_AllSkill", ("Thorough Technician", "Técnico Completo", "Aprenda todas as habilidades") },
        { "45_Acquire_AllSkill_NewGamePlus", ("Infinite Blade", "Lâmina Infinita", "Aprenda todas as habilidades no New Game+") },
        { "20_Acquire_AllCan", ("Can Collector", "Colecionador de Latas", "Colete todas as latas") },
        { "22_Acquire_AllRecords", ("Records Collector", "Colecionador de Registros", "Colete 200 entradas do Banco de Dados") },
        { "24_Open_AllBox", ("Box Hunter", "Caçador de Caixas", "Abra 200 caixas") },
        { "07_CompleteLevel_AltesLabor", ("Altess Levoire", "Altess Levoire", "Recupere a Célula Hiper de Altess Levoire") },
        { "27_LevelUpMax_AllExoSpine", ("Perfect Exospine", "Exoespina Perfeita", "Aprimore 10 Exoespinas ao máximo") },
        { "28_WeaponMaxUpgrade", ("Perfect Blood Edge", "Sangue Perfeito", "Aprimore Blood Edge ao máximo") },
        { "29_TumblerMaxUpgrade", ("Perfect Rechargeable Tumbler", "Cantil Recarregável Perfeito", "Aprimore o Cantil Recarregável ao máximo") },
        { "30_BodyMaxUpgrade", ("Perfect Physical Enhancement", "Aprimoramento Físico Perfeito", "Aprimore o HP ao máximo") },
        { "31_BetaMaxUpgrade", ("Perfect Beta Energy Enhancement", "Aprimoramento de Energia Beta Perfeito", "Aprimore a Energia Beta ao máximo") },
        { "39_CharKill_BetaSkill", ("Naytiba Hunter", "Caçador Naytiba", "Derrote 100 inimigos com Habilidades Beta") },
        { "40_CharKill_BurstSkill", ("Relentless Destroyer", "Destruidor Implacável", "Derrote 50 inimigos com Habilidades Burst") },
        { "42_CharKill_RangeSkill", ("Cold-blooded Sniper", "Atirador Frio", "Derrote 150 inimigos com ataques à distância") },
        { "38_CharKill_AssassinationSkills", ("Silent Executioner", "Executor Silencioso", "Derrote 50 inimigos por execução") },
        { "36_JustEvade", ("Battlefield Martial Artist", "Artista Marcial do Campo de Batalha", "Desvie perfeitamente de 200 ataques inimigos") },
        { "37_JustParry", ("Agile Gladiator", "Gladiador Ágil", "Parou perfeitamente 300 ataques inimigos") },
    };

    // Mapeamento: nome no save file -> nome Steam API
    private static readonly Dictionary<string, string> SaveNameToSteamName = new()
    {
        { "Trophy_Platinum", "01_Platinum" },
        { "Trophy_Activate_FirstCamp", "02_Activate_FirstCamp" },
        { "Trophy_Activate_AllCamp", "26_Activate_AllCamp" },
        { "Trophy_KillCharacter", "43_KillCharacter" },
        { "Trophy_KillCharacter_Brute", "06_KillCharacter_Brute" },
        { "Trophy_KillCharacter_AllNative", "25_KillCharacter_AllNative" },
        { "Trophy_Acquire_AllNanoSuit", "21_Acquire_AllNanoSuit" },
        { "Trophy_Acquire_AllSkill", "32_Acquire_AllSkill" },
        { "Trophy_Acquire_AllSkill_v2", "45_Acquire_AllSkill_NewGamePlus" },
        { "Trophy_Acquire_AllCan", "20_Acquire_AllCan" },
        { "Trophy_Acquire_AllRecords", "22_Acquire_AllRecords" },
        { "Trophy_Open_AllBox", "24_Open_AllBox" },
        { "Trophy_CompleteLevel_AltesLabor", "07_CompleteLevel_AltesLabor" },
        { "Trophy_LevelUpMax_AllExoSpine", "27_LevelUpMax_AllExoSpine" },
        { "Trophy_WeaponMaxUpgrade", "28_WeaponMaxUpgrade" },
        { "Trophy_TumblerMaxUpgrade", "29_TumblerMaxUpgrade" },
        { "Trophy_BodyMaxUpgrade", "30_BodyMaxUpgrade" },
        { "Trophy_BetaMaxUpgrade", "31_BetaMaxUpgrade" },
        { "Trophy_CharKill_BetaSkill", "39_CharKill_BetaSkill" },
        { "Trophy_CharKill_BurstSkill", "40_CharKill_BurstSkill" },
        { "Trophy_CharKill_RangeSkill", "42_CharKill_RangeSkill" },
        { "Trophy_CharKill_AssassinationSkills", "38_CharKill_AssassinationSkills" },
        { "Trophy_JustEvade", "36_JustEvade" },
        { "Trophy_JustParry", "37_JustParry" },
    };

    private static readonly Dictionary<string, int> TrophyMaxProgress = new()
    {
        { "43_KillCharacter", 1500 },
        { "21_Acquire_AllNanoSuit", 30 },
        { "20_Acquire_AllCan", 49 },
        { "22_Acquire_AllRecords", 200 },
        { "24_Open_AllBox", 200 },
        { "27_LevelUpMax_AllExoSpine", 10 },
        { "39_CharKill_BetaSkill", 100 },
        { "40_CharKill_BurstSkill", 50 },
        { "42_CharKill_RangeSkill", 150 },
        { "38_CharKill_AssassinationSkills", 50 },
        { "36_JustEvade", 200 },
        { "37_JustParry", 300 },
    };

    private static readonly Dictionary<string, string> QuestTranslations = new()
    {
        { "Complete_Quest_Quest_Sub_032", "Além do Destino" },
        { "Complete_Quest_Quest_Sub_033", "Amor Fraternal" },
        { "Complete_Quest_Quest_Sub_043", "Bip!" },
        { "Complete_Quest_Quest_Epic_01", "História Principal 1" },
        { "Complete_Quest_Quest_Epic_02", "História Principal 2" },
        { "Complete_Quest_Quest_Epic_03", "História Principal 3" },
        { "Complete_Quest_Quest_Epic_04", "História Principal 4" },
        { "Complete_Quest_Quest_Epic_05", "História Principal 5" },
        { "Complete_Quest_Quest_Epic_06", "História Principal 6" },
        { "Complete_Quest_Quest_Epic_07", "História Principal 7" },
        { "Complete_Quest_Quest_Sub_001", "Missão Secundária 1" },
        { "Complete_Quest_Quest_Sub_002", "Missão Secundária 2" },
        { "Complete_Quest_Quest_Sub_005", "Missão Secundária 5" },
        { "Complete_Quest_Quest_Sub_006", "Missão Secundária 6" },
        { "Complete_Quest_Quest_Sub_011", "Missão Secundária 11" },
        { "Complete_Quest_Quest_Sub_016", "Missão Secundária 16" },
        { "Complete_Quest_Quest_Sub_017", "Missão Secundária 17" },
        { "Complete_Quest_Quest_Sub_018", "Missão Secundária 18" },
        { "Complete_Quest_Quest_Sub_019", "Missão Secundária 19" },
        { "Complete_Quest_Quest_Sub_020", "Missão Secundária 20" },
        { "Complete_Quest_Quest_Sub_023", "Missão Secundária 23" },
        { "Complete_Quest_Quest_Sub_024", "Missão Secundária 24" },
        { "Complete_Quest_Quest_Sub_025", "Missão Secundária 25" },
        { "Complete_Quest_Quest_Sub_026", "Missão Secundária 26" },
        { "Complete_Quest_Quest_Sub_027", "Missão Secundária 27" },
        { "Complete_Quest_Quest_Sub_028", "Missão Secundária 28" },
        { "Complete_Quest_Quest_Sub_029", "Missão Secundária 29" },
        { "Complete_Quest_Quest_Sub_030", "Missão Secundária 30" },
        { "Complete_Quest_Quest_Sub_031", "Missão Secundária 31" },
        { "Complete_Quest_Quest_Sub_034", "Missão Secundária 34" },
        { "Complete_Quest_Quest_Sub_036", "Missão Secundária 36" },
        { "Complete_Quest_Quest_Sub_037", "Missão Secundária 37" },
        { "Complete_Quest_Quest_Sub_038", "Missão Secundária 38" },
        { "Complete_Quest_Quest_Sub_039", "Missão Secundária 39" },
        { "Complete_Quest_Quest_Sub_040", "Missão Secundária 40" },
        { "Complete_Quest_Quest_Sub_041", "Missão Secundária 41" },
        { "Complete_Quest_Quest_Sub_042", "Missão Secundária 42" },
        { "Complete_Quest_Quest_Request_033", "Solicitação 33" },
        { "Complete_Quest_Quest_Request_034", "Solicitação 34" },
        { "Complete_Quest_Quest_Request_035", "Solicitação 35" },
        { "Complete_Quest_Quest_Request_036", "Solicitação 36" },
        { "Complete_Quest_Quest_Request_037", "Solicitação 37" },
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
                Name = t.Value.Item2,
                Description = t.Value.Item3,
                Achieved = false,
                Progress = 0,
                MaxProgress = TrophyMaxProgress.GetValueOrDefault(t.Key, 0)
            }).ToList();
        }

        var achievements = new List<Achievement>();

        foreach (var trophy in data.Trophies)
        {
            var trophyInfo = TrophyMap.GetValueOrDefault(trophy.Name, (trophy.SteamAchievement, trophy.SteamAchievement, $"Trophy: {trophy.Name}"));
            var maxProgress = TrophyMaxProgress.GetValueOrDefault(trophy.Name, 0);
            
            achievements.Add(new Achievement
            {
                Apiname = trophy.Name,
                Name = trophyInfo.Item2,
                Description = trophyInfo.Item3,
                Achieved = trophy.BCompleted,
                Progress = trophy.ProgressValue,
                MaxProgress = maxProgress
            });
        }

        foreach (var quest in data.QuestCompletions)
        {
            var questName = QuestTranslations.GetValueOrDefault(quest, quest);
            achievements.Add(new Achievement
            {
                Apiname = quest,
                Name = questName,
                Description = $"Quest completada",
                Achieved = true,
                Progress = 1,
                MaxProgress = 1
            });
        }

        if (data.KillElderEnding)
            achievements.Add(new Achievement { Apiname = "Ending_KillElder", Name = "Novas Memórias", Description = "Final: Mate o Elder", Achieved = true, Progress = 1, MaxProgress = 1 });
        if (data.KillLilyEnding)
            achievements.Add(new Achievement { Apiname = "Ending_KillLily", Name = "Custo das Memórias Perdidas", Description = "Final: Mate Lily", Achieved = true, Progress = 1, MaxProgress = 1 });
        if (data.SaveLilyEnding)
            achievements.Add(new Achievement { Apiname = "Ending_SaveLily", Name = "Retorno à Colônia", Description = "Final: Salve Lily", Achieved = true, Progress = 1, MaxProgress = 1 });

        if (data.NewGamePlusCount > 0)
            achievements.Add(new Achievement { Apiname = "NewGamePlus", Name = "New Game+", Description = $"NG+ {data.NewGamePlusCount}x", Achieved = true, Progress = data.NewGamePlusCount, MaxProgress = 1 });

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

        foreach (var (saveName, steamName) in SaveNameToSteamName)
        {
            var trophyInfo = TrophyMap.GetValueOrDefault(steamName, (steamName, steamName, $"Trophy: {saveName}"));
            var idx = text.IndexOf(saveName + '\0', StringComparison.Ordinal);

            if (idx >= 0)
            {
                var searchStart = idx + saveName.Length + 1;
                var bCompleted = ExtractBoolValue(buffer, searchStart);
                
                var valueOffset = idx + saveName.Length + 47;
                int progressValue = 0;
                
                if (valueOffset + 4 <= buffer.Length)
                {
                    progressValue = BitConverter.ToInt32(buffer, valueOffset);
                }

                trophies.Add(new StellarBladeTrophy
                {
                    Name = steamName,
                    SteamAchievement = trophyInfo.Item2,
                    BCompleted = bCompleted,
                    ProgressValue = progressValue
                });
            }
            else
            {
                trophies.Add(new StellarBladeTrophy
                {
                    Name = steamName,
                    SteamAchievement = trophyInfo.Item2,
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

        // Estrutura: "UInt32Property" (14 bytes) + null (1) + size (4) + padding (4) + value (4)
        var valueOffset = idx + 28;
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
