using System.Text.Json;
using EMECore.Core.Services;

namespace EMECore.WinUI.Services;

public class CardLayoutService
{
    private const string OrderKey = "hardware_card_order";
    private const string ShownKey = "hardware_card_shown_optional";

    private static readonly string[] DefaultOrder = new[]
    {
        "mb", "ram", "cpu", "gpu", "disk", "net", "fps"
    };

    private static readonly string[] OptionalCards = new[]
    {
        "gamepad"
    };

    public static List<string> GetOrder()
    {
        var json = SettingsService.Get(OrderKey, "");
        if (string.IsNullOrEmpty(json))
            return DefaultOrder.Concat(OptionalCards).ToList();

        try
        {
            var order = JsonSerializer.Deserialize<List<string>>(json);
            if (order != null && order.Count > 0)
            {
                var result = new List<string>();
                foreach (var key in DefaultOrder)
                    if (!result.Contains(key)) result.Add(key);
                foreach (var key in OptionalCards)
                    if (!result.Contains(key)) result.Add(key);
                foreach (var key in order)
                    if (!result.Contains(key)) result.Add(key);
                return result;
            }
        }
        catch { }

        return DefaultOrder.Concat(OptionalCards).ToList();
    }

    public static void SaveOrder(List<string> order)
    {
        var json = JsonSerializer.Serialize(order);
        SettingsService.Set(OrderKey, json);
    }

    public static List<string> GetShownOptionalKeys()
    {
        var json = SettingsService.Get(ShownKey, "");
        if (string.IsNullOrEmpty(json))
            return new List<string>();

        try
        {
            var list = JsonSerializer.Deserialize<List<string>>(json);
            if (list != null) return list;
        }
        catch { }

        return new List<string>();
    }

    public static void SaveShownOptionalKeys(List<string> shown)
    {
        var json = JsonSerializer.Serialize(shown);
        SettingsService.Set(ShownKey, json);
    }

    public static bool IsCardVisible(string key)
    {
        if (!IsOptional(key)) return true;
        return GetShownOptionalKeys().Contains(key);
    }

    public static void ShowCard(string key)
    {
        var shown = GetShownOptionalKeys();
        if (!shown.Contains(key)) shown.Add(key);
        SaveShownOptionalKeys(shown);
    }

    public static void HideCard(string key)
    {
        var shown = GetShownOptionalKeys();
        shown.Remove(key);
        SaveShownOptionalKeys(shown);
    }

    public static List<string> GetAddableCards()
    {
        var shown = GetShownOptionalKeys();
        return OptionalCards.Where(k => !shown.Contains(k)).ToList();
    }

    public static bool IsOptional(string key) => OptionalCards.Contains(key);

    public static string GetCardDisplayName(string key) => key switch
    {
        "cpu" => "CPU",
        "gpu" => "GPU",
        "mb" => "Placa-Mãe",
        "ram" => "RAM",
        "disk" => "Disco",
        "net" => "Rede",
        "fps" => "FPS",
        "gamepad" => "Controle",
        _ => key.ToUpper()
    };
}
