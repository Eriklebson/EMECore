using System.Text;
using EMECore.Core.Models;

namespace EMECore.Hardware.Services;

public class RDR2SaveParser
{
    public RDR2SaveData? ParseFromFile(string filePath)
    {
        try
        {
            var data = File.ReadAllBytes(filePath);
            return ParseFromBytes(data, Path.GetFileName(filePath));
        }
        catch { return null; }
    }

    public RDR2SaveData? ParseFromBytes(byte[] data, string fileName = "")
    {
        if (data.Length < 100) return null;

        var save = new RDR2SaveData
        {
            FileName = fileName,
            FileSize = data.Length,
            RawBytes = data
        };

        var text = Encoding.UTF8.GetString(data);
        save.ExtractedStrings = ExtractAsciiStrings(data, 6);
        save.StatsData = new Dictionary<string, long>();
        save.QuestData = new Dictionary<string, bool>();

        var statKeywords = new[]
        {
            "Kill", "kill", "hunt", "Hunt", "fish", "Fish",
            "horse", "Horse", "camp", "Camp", "gang", "Gang",
            "honor", "Honor", "dead", "Dead", "eye", "Eye",
            "chapter", "Chapter", "mission", "Mission", "stranger", "Stranger"
        };

        foreach (var keyword in statKeywords)
        {
            var count = CountOccurrences(text, keyword);
            if (count > 0)
                save.StatsData[$"stat_{keyword}"] = count;
        }

        var questPatterns = new[]
        {
            "CH01", "CH02", "CH03", "CH04", "CH05", "CH06",
            "DRPE", "HHNT", "RDRP", "TMNT",
            "chapter_", "mission_", "stranger_"
        };

        foreach (var pattern in questPatterns)
        {
            var count = CountOccurrences(text, pattern);
            if (count > 0)
                save.QuestData[$"quest_{pattern}"] = true;
        }

        return save;
    }

    private static List<string> ExtractAsciiStrings(byte[] data, int minLength)
    {
        var strings = new List<string>();
        var current = new StringBuilder();

        foreach (var b in data)
        {
            if (b >= 32 && b < 127)
            {
                current.Append((char)b);
            }
            else
            {
                if (current.Length >= minLength)
                    strings.Add(current.ToString());
                current.Clear();
            }
        }

        if (current.Length >= minLength)
            strings.Add(current.ToString());

        return strings;
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
}

public class RDR2SaveData
{
    public string FileName { get; set; } = "";
    public long FileSize { get; set; }
    public byte[]? RawBytes { get; set; }
    public List<string> ExtractedStrings { get; set; } = new();
    public Dictionary<string, bool> QuestData { get; set; } = new();
    public Dictionary<string, long> StatsData { get; set; } = new();
}
