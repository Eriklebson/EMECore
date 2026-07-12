using System.Text;
using System.Text.Json;
using EMECore.Core.Models;

namespace EMECore.Hardware.Services;

public class GodOfWarSaveParser
{
    private static readonly string[] Realms = { "Midgard", "Asgard", "Vanaheim", "Alfheim", "Helheim", "Muspelheim", "Niflheim", "Jotunheim" };
    private static readonly string[] BossNames = { "Baldur", "Magni", "Modi", "Heimdall", "Thor", "Odin", "Sigrun", "Gná" };
    private static readonly string[] CompanionNames = { "Atreus", "Freya", "Mimir" };

    public GodOfWarSaveData? ParseFromDirectory(string saveDir)
    {
        try
        {
            var savFiles = Directory.GetFiles(saveDir, "game.sav");
            if (savFiles.Length == 0)
            {
                savFiles = Directory.GetFiles(saveDir, "*.sav");
            }
            if (savFiles.Length == 0) return null;

            var latest = savFiles.OrderByDescending(f => File.GetLastWriteTime(f)).First();
            return ParseFromFile(latest);
        }
        catch { return null; }
    }

    public GodOfWarSaveData? ParseFromFile(string savPath)
    {
        try
        {
            var bytes = File.ReadAllBytes(savPath);
            if (bytes.Length < 1024) return null;

            var save = new GodOfWarSaveData
            {
                FileName = Path.GetFileName(savPath),
                FilePath = savPath,
                FileSize = bytes.Length
            };

            ExtractHeaderInfo(bytes, save);
            ExtractRealmData(bytes, save);
            ExtractQuestData(bytes, save);
            ExtractCharacterData(bytes, save);
            DetectCompletion(save);

            return save;
        }
        catch { return null; }
    }

    private static void ExtractHeaderInfo(byte[] bytes, GodOfWarSaveData save)
    {
        if (bytes.Length < 0x100) return;

        save.MagicHeader = $"{bytes[0]:X2}{bytes[1]:X2}{bytes[2]:X2}{bytes[3]:X2}";

        save.DataSize = bytes.Length;

        var ascii = Encoding.ASCII.GetString(bytes, 0, Math.Min(bytes.Length, 0x200));
        save.ExtractedStrings.Add($"Header: {save.MagicHeader}");
        save.ExtractedStrings.Add($"Size: {bytes.Length} bytes ({bytes.Length / 1024 / 1024} MB)");
    }

    private static void ExtractRealmData(byte[] bytes, GodOfWarSaveData save)
    {
        var text = Encoding.ASCII.GetString(bytes, 0, Math.Min(bytes.Length, 5000000));

        foreach (var realm in Realms)
        {
            var matches = System.Text.RegularExpressions.Regex.Matches(text, realm, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (matches.Count > 0)
            {
                save.RealmOccurrences[realm] = matches.Count;
                save.RealmsVisited.Add(realm);
            }
        }
    }

    private static void ExtractQuestData(byte[] bytes, GodOfWarSaveData save)
    {
        var text = Encoding.ASCII.GetString(bytes, 0, Math.Min(bytes.Length, 5000000));

        var questPatterns = new[]
        {
            "AI_Visit", "AI_Shop", "AI_ShopIntro", "AI_TrenchPre", "AI_TrenchPost",
            "AI_BrokShop", "AI_BrokCares", "AI_CavernDark", "AI_ChimneyLow",
            "AI_SummitAscent", "AI_Tow", "Base_AI", "Mid_AI", "Arena_AI"
        };

        foreach (var pattern in questPatterns)
        {
            var count = CountOccurrences(text, pattern);
            if (count > 0)
            {
                save.QuestMarkers[pattern] = count;
            }
        }

        var storyQuests = new[]
        {
            "TheBoysJotunheim", "EscapeFromHelheim", "BaldurFight",
            "ThorsVisit", "OdinVisit", "RagnarokEvent", "Fimbulwinter"
        };

        foreach (var quest in storyQuests)
        {
            if (text.Contains(quest, StringComparison.OrdinalIgnoreCase))
            {
                save.StoryQuestsCompleted.Add(quest);
            }
        }
    }

    private static void ExtractCharacterData(byte[] bytes, GodOfWarSaveData save)
    {
        var text = Encoding.ASCII.GetString(bytes, 0, Math.Min(bytes.Length, 5000000));

        foreach (var boss in BossNames)
        {
            var count = CountOccurrences(text, boss);
            if (count > 0)
            {
                save.BossEncounters[boss] = count;
            }
        }

        foreach (var companion in CompanionNames)
        {
            var count = CountOccurrences(text, companion);
            if (count > 0)
            {
                save.CompanionMentions[companion] = count;
            }
        }
    }

    private static void DetectCompletion(GodOfWarSaveData save)
    {
        save.CompletionScore = 0;

        if (save.RealmsVisited.Count >= 6) save.CompletionScore += 20;
        else if (save.RealmsVisited.Count >= 4) save.CompletionScore += 10;

        if (save.BossEncounters.ContainsKey("Baldur")) save.CompletionScore += 15;
        if (save.BossEncounters.ContainsKey("Thor")) save.CompletionScore += 10;
        if (save.BossEncounters.ContainsKey("Odin")) save.CompletionScore += 10;
        if (save.BossEncounters.ContainsKey("Heimdall")) save.CompletionScore += 5;
        if (save.BossEncounters.ContainsKey("Sigrun")) save.CompletionScore += 10;
        if (save.BossEncounters.ContainsKey("Gná")) save.CompletionScore += 5;

        if (save.CompanionMentions.ContainsKey("Freya") && save.CompanionMentions["Freya"] > 50)
            save.CompletionScore += 10;
        if (save.CompanionMentions.ContainsKey("Mimir") && save.CompanionMentions["Mimir"] > 10)
            save.CompletionScore += 5;

        if (save.StoryQuestsCompleted.Count >= 5) save.CompletionScore += 15;
        else if (save.StoryQuestsCompleted.Count >= 3) save.CompletionScore += 5;

        save.IsCompletionist = save.CompletionScore >= 80;
        save.IsNewGamePlus = save.FileSize > 30 * 1024 * 1024;
    }

    private static int CountOccurrences(string text, string pattern)
    {
        int count = 0, index = 0;
        while ((index = text.IndexOf(pattern, index, StringComparison.OrdinalIgnoreCase)) != -1)
        {
            count++;
            index += pattern.Length;
        }
        return count;
    }

    public string? FindSavePath()
    {
        var homePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var savedGames = Path.Combine(homePath, "Saved Games", "God of War");

        if (!Directory.Exists(savedGames)) return null;

        var dirs = Directory.GetDirectories(savedGames);
        foreach (var dir in dirs.OrderByDescending(d => Directory.GetLastWriteTime(d)))
        {
            var savPath = Path.Combine(dir, "game.sav");
            if (File.Exists(savPath)) return savPath;
        }

        return null;
    }

    public List<Achievement> ParseAchievements()
    {
        var savPath = FindSavePath();
        if (savPath == null) return new List<Achievement>();

        var saveData = ParseFromFile(savPath);
        if (saveData == null) return new List<Achievement>();

        return BuildAchievements(saveData);
    }

    private static List<Achievement> BuildAchievements(GodOfWarSaveData save)
    {
        var achievements = new List<Achievement>();

        achievements.Add(new Achievement
        {
            Apiname = "gow_realms_explored",
            Name = "Explorador dos Reinos",
            Description = $"Explorou {save.RealmsVisited.Count} de 8 reinos",
            Achieved = save.RealmsVisited.Count >= 6,
            Progress = save.RealmsVisited.Count,
            MaxProgress = 8
        });

        achievements.Add(new Achievement
        {
            Apiname = "gow_baldur_defeated",
            Name = "Derrotou Baldur",
            Description = "Enfrentou Baldur na batalha final",
            Achieved = save.BossEncounters.ContainsKey("Baldur")
        });

        achievements.Add(new Achievement
        {
            Apiname = "gow_all_bosses",
            Name = "Caçador de Deuses",
            Description = "Derrotou todos os chefes principais",
            Achieved = save.BossEncounters.ContainsKey("Baldur") &&
                       save.BossEncounters.ContainsKey("Thor") &&
                       save.BossEncounters.ContainsKey("Odin"),
            Progress = new[] { "Baldur", "Thor", "Odin" }.Count(b => save.BossEncounters.ContainsKey(b)),
            MaxProgress = 3
        });

        achievements.Add(new Achievement
        {
            Apiname = "gow_valkyrie_queen",
            Name = "Rainha das Valquírias",
            Description = "Derrotou a Rainha das Valquírias (Sigrun ou Gná)",
            Achieved = save.BossEncounters.ContainsKey("Sigrun") || save.BossEncounters.ContainsKey("Gná")
        });

        achievements.Add(new Achievement
        {
            Apiname = "gow_story_complete",
            Name = "Jornada Completa",
            Description = "Completou a história principal",
            Achieved = save.StoryQuestsCompleted.Count >= 5,
            Progress = save.StoryQuestsCompleted.Count,
            MaxProgress = 7
        });

        achievements.Add(new Achievement
        {
            Apiname = "gow_freya_companion",
            Name = "Aliança com Freya",
            Description = "Viajou com Freya como companheira",
            Achieved = save.CompanionMentions.ContainsKey("Freya") && save.CompanionMentions["Freya"] > 50,
            Progress = save.CompanionMentions.GetValueOrDefault("Freya", 0),
            MaxProgress = 100
        });

        achievements.Add(new Achievement
        {
            Apiname = "gow_completionist",
            Name = "100% Conquistador",
            Description = "Completou o jogo em 100%",
            Achieved = save.IsCompletionist,
            Progress = save.CompletionScore,
            MaxProgress = 100
        });

        achievements.Add(new Achievement
        {
            Apiname = "gow_new_game_plus",
            Name = "New Game Plus",
            Description = "Iniciou um New Game Plus",
            Achieved = save.IsNewGamePlus
        });

        return achievements;
    }
}

public class GodOfWarSaveData
{
    public string FileName { get; set; } = "";
    public string FilePath { get; set; } = "";
    public long FileSize { get; set; }
    public string MagicHeader { get; set; } = "";
    public int DataSize { get; set; }
    public List<string> ExtractedStrings { get; set; } = new();
    public HashSet<string> RealmsVisited { get; set; } = new();
    public Dictionary<string, int> RealmOccurrences { get; set; } = new();
    public Dictionary<string, int> QuestMarkers { get; set; } = new();
    public List<string> StoryQuestsCompleted { get; set; } = new();
    public Dictionary<string, int> BossEncounters { get; set; } = new();
    public Dictionary<string, int> CompanionMentions { get; set; } = new();
    public int CompletionScore { get; set; }
    public bool IsCompletionist { get; set; }
    public bool IsNewGamePlus { get; set; }
}
