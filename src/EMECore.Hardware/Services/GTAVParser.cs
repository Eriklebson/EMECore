using EMECore.Core.Models;

namespace EMECore.Hardware.Services;

public class GTAVParser
{
    private readonly GTAVSaveParser _saveParser = new();

    private static readonly Dictionary<string, (string Name, string Description)> AchievementMap = new()
    {
        { "gta_mission_1", ("Primeiro Trabalho", "Complete a primeira missão principal") },
        { "gta_mission_10", ("Profissional", "Complete 10 missões principais") },
        { "gta_mission_all", ("Rei do Crime", "Complete todas as 69 missões principais") },
        { "gta_strangers_5", ("Estranhos", "Complete 5 missões de estranhos") },
        { "gta_strangers_10", ("Rede de Contatos", "Complete 10 missões de estranhos") },
        { "gta_strangers_all", ("Amigos de Todos", "Complete todas as 20 missões de estranhos") },
        { "gta_heist_1", ("Assalto Perfeito", "Complete um heist") },
        { "gta_heist_all", ("Lenda dos Heists", "Complete todos os 5 heists") },
        { "gta_stunt_50", ("Morte Certa", "Complete 50 stunt jumps") },
        { "gta_stunt_all", ("Adrenalina Pura", "Complete todos os 50 stunt jumps") },
        { "gta_property_5", ("Magnata", "Compre 5 propriedades") },
        { "gta_property_all", ("Imobiliário", "Compre todas as propriedades") },
        { "gta_letter_1", ("Carta Misteriosa", "Encontre 1 letra coletável") },
        { "gta_letter_10", ("Cartógrafo", "Encontre 10 letras coletáveis") },
        { "gta_letter_50", ("Caçador de Tesouros", "Encontre todas as 50 letras") },
        { "gta_sub_1", ("Mergulhador", "Encontre 1 peça de submarine part") },
        { "gta_sub_30", ("Mergulhador Veterano", "Encontre 30 peças de submarine") },
        { "gta_sub_all", ("Fundo do Mar", "Encontre todas as 30 peças") },
        { "gta_kills_100", ("Atirador", "Elimine 100 inimigos") },
        { "gta_kills_1000", ("Veterano de Guerra", "Elimine 1000 inimigos") },
        { "gta_kills_10000", ("Exterminador", "Elimine 10.000 inimigos") },
        { "gta_distance_100", ("Viajante", "Viaje 100km") },
        { "gta_distance_1000", ("Nômade", "Viaje 1000km") },
        { "gta_distance_10000", ("Mundo Aberto", "Viaje 10.000km") },
        { "gta_wanted_5stars", ("Most Wanted", "Alcance 5 estrelas de procurado") },
        { "gta_stock_profit", ("Wall Street", "Ganhe $1.000.000 na bolsa") },
        { "gta_gold_all", ("Perfeccionista", "Ganhe medalha de ouro em todas as missões") },
        { "gta_playtime_24h", ("Vício Total", "Jogue por 24 horas") },
        { "gta_playtime_100h", ("Vida GTA", "Jogue por 100 horas") },
        { "gta_heli_50", ("Piloto de Helicóptero", "Viaje 50km de helicóptero") },
        { "gta_plane_50", ("Piloto de Avião", "Viaje 50km de avião") },
        { "gta_vehicle_collect", ("Colecionador de Veículos", "Encontre todos os veículos únicos") },
        { "gta_creative", ("Modo Criativo", "Use o modo criativo") },
        { "gta_completed", ("Platina GTA", "Todas as conquistas desbloqueadas") },
    };

    private static readonly Dictionary<string, int> MaxProgressMap = new()
    {
        { "gta_mission_10", 10 }, { "gta_mission_all", 69 },
        { "gta_strangers_5", 5 }, { "gta_strangers_10", 10 }, { "gta_strangers_all", 20 },
        { "gta_heist_all", 5 },
        { "gta_stunt_50", 50 }, { "gta_stunt_all", 50 },
        { "gta_property_5", 5 }, { "gta_property_all", 20 },
        { "gta_letter_1", 1 }, { "gta_letter_10", 10 }, { "gta_letter_50", 50 },
        { "gta_sub_1", 1 }, { "gta_sub_30", 30 }, { "gta_sub_all", 30 },
        { "gta_kills_100", 100 }, { "gta_kills_1000", 1000 }, { "gta_kills_10000", 10000 },
        { "gta_distance_100", 100 }, { "gta_distance_1000", 1000 }, { "gta_distance_10000", 10000 },
        { "gta_wanted_5stars", 5 },
        { "gta_stock_profit", 1000000 },
        { "gta_gold_all", 70 },
        { "gta_playtime_24h", 24 }, { "gta_playtime_100h", 100 },
        { "gta_heli_50", 50 }, { "gta_plane_50", 50 },
    };

    public string? FindSavePath() => _saveParser.FindSavePath();

    public bool HasSave() => _saveParser.HasSave();

    public GTAVSaveParser GetSaveParser() => _saveParser;

    public List<Achievement> ParseAchievements(string? savePath = null)
    {
        var filePath = savePath ?? FindSavePath();
        if (filePath == null || !File.Exists(filePath))
        {
            return CreateDefaultAchievements();
        }

        try
        {
            var saveData = _saveParser.ParseFromFile(filePath);
            if (saveData == null)
                return CreateDefaultAchievements();

            var achievements = new List<Achievement>();

            var fileMB = saveData.FileSize / (1024 * 1024);

            AddAchievement(achievements, "gta_mission_1", 1);
            AddAchievement(achievements, "gta_mission_10", 10);
            AddAchievement(achievements, "gta_mission_all", 69);

            AddAchievement(achievements, "gta_stunt_50", 50);
            AddAchievement(achievements, "gta_stunt_all", 50);

            AddAchievement(achievements, "gta_kills_100", 100);
            AddAchievement(achievements, "gta_kills_1000", 1000);
            AddAchievement(achievements, "gta_kills_10000", 10000);

            AddAchievement(achievements, "gta_distance_100", 100);
            AddAchievement(achievements, "gta_distance_1000", 1000);
            AddAchievement(achievements, "gta_distance_10000", 10000);

            achievements.Add(new Achievement
            {
                Apiname = "gta_completed",
                Name = AchievementMap["gta_completed"].Name,
                Description = AchievementMap["gta_completed"].Description,
                Achieved = false,
                Progress = 0,
                MaxProgress = 34
            });

            return achievements;
        }
        catch
        {
            return CreateDefaultAchievements();
        }
    }

    private List<Achievement> CreateDefaultAchievements()
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

    private static void AddAchievement(List<Achievement> achievements, string key, int required)
    {
        achievements.Add(new Achievement
        {
            Apiname = key,
            Name = AchievementMap[key].Name,
            Description = AchievementMap[key].Description,
            Achieved = false,
            Progress = 0,
            MaxProgress = required
        });
    }
}
