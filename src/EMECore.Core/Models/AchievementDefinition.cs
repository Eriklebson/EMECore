using System.Text.Json;

namespace EMECore.Core.Models;

public class AchievementDefinition
{
    public string GameId { get; set; } = string.Empty;
    public string GameName { get; set; } = string.Empty;
    public string Apiname { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string IconGray { get; set; } = string.Empty;
    public AchievementCondition Condition { get; set; } = new();
    public int MaxProgress { get; set; }
}

public class AchievementCondition
{
    public ConditionType Type { get; set; }
    public string SaveKey { get; set; } = string.Empty;
    public ConditionOperator Op { get; set; } = ConditionOperator.GreaterOrEqual;
    public object? ExpectedValue { get; set; }
    public List<SubCondition>? SubConditions { get; set; }
    public string Description { get; set; } = string.Empty;
}

public enum ConditionType
{
    KeyExists,
    KeyValue,
    FlagSet,
    CounterReached,
    MultipleConditions
}

public enum ConditionOperator
{
    Equals,
    NotEquals,
    GreaterThan,
    GreaterOrEqual,
    LessThan,
    LessOrEqual,
    Contains
}

public class SubCondition
{
    public string SaveKey { get; set; } = string.Empty;
    public ConditionOperator Op { get; set; }
    public object? ExpectedValue { get; set; }
}

public class AchievementDatabase
{
    public List<AchievementDefinition> Achievements { get; set; } = new();
    public string SourceFile { get; set; } = string.Empty;

    public static AchievementDatabase? LoadFromFile(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AchievementDatabase>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });
        }
        catch { return null; }
    }

    public static AchievementDatabase LoadFromResource(string resourceName)
    {
        var assembly = typeof(AchievementDatabase).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null) return new AchievementDatabase();
        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();
        return JsonSerializer.Deserialize<AchievementDatabase>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        }) ?? new AchievementDatabase();
    }
}
