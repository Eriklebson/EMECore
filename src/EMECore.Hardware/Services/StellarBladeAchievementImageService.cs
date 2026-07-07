using System.Text.Json;

namespace EMECore.Hardware.Services;

public class StellarBladeAchievementImageService
{
    private static readonly HttpClient _http = new();
    private static readonly Dictionary<string, string> _imageCache = new();
    private static readonly string _imagesPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EMECore", "AchievementImages", "StellarBlade");

    // Mapeamento de conquistas do Stellar Blade para imagens da Epic Games Store
    private static readonly Dictionary<string, string> AchievementImageMap = new()
    {
        { "Trophy_Activate_FirstCamp", "80f71ceb5b8a31fbf4ee87e1897bc4fb" },
        { "Trophy_Activate_AllCamp", "13da3cd1648b3d5e65a4a7251c441411" },
        { "Trophy_KillCharacter", "59a62fb6292ece51dc4aa920258becec" },
        { "Trophy_KillCharacter_Brute", "99f198da218b1dba7f6d19bab742dc9e" },
        { "Trophy_KillCharacter_AllNative", "a2348b3e40cf85782904b87cdf42f030" },
        { "Trophy_Acquire_AllNanoSuit", "0cf60f806f52732d27494f8ee03fa631" },
        { "Trophy_Acquire_AllSkill", "09eb3be1544c6d93ed55311c0eb76ae1" },
        { "Trophy_Acquire_AllSkill_v2", "7b4a9964d8df5742edac7bca1dcfd5d0" },
        { "Trophy_Acquire_AllCan", "500792d2fd6eb046408cdc5742a83105" },
        { "Trophy_Acquire_AllRecords", "4a5095797934a64584f978b92e6c28f6" },
        { "Trophy_Open_AllBox", "0e408d7d58510e86be94d3a8f05d6c94" },
        { "Trophy_CompleteLevel_AltesLabor", "2455f667ec04200ac755cdb3f0a1c291" },
        { "Trophy_LevelUpMax_AllExoSpine", "2455f667ec04200ac755cdb3f0a1c291" },
        { "Trophy_WeaponMaxUpgrade", "8efbec27ac7ad6e20c485a5dd37f6ae2" },
        { "Trophy_TumblerMaxUpgrade", "7c2831cf941268236616b5d310cbb096" },
        { "Trophy_BodyMaxUpgrade", "b33914125a11a8aae14b9a91cf9822d9" },
        { "Trophy_BetaMaxUpgrade", "86bc06c13557981898df4e17e53bdeaf" },
        { "Trophy_CharKill_BetaSkill", "99f198da218b1dba7f6d19bab742dc9e" },
        { "Trophy_CharKill_BurstSkill", "7f3a6d99086bba3c7c027743f30b938a" },
        { "Trophy_CharKill_RangeSkill", "ad83601c8576e826a0204f49845f8d21" },
        { "Trophy_CharKill_AssassinationSkills", "cfe7c367dcdf4a2d90cc17a87a4f6bc9" },
        { "Trophy_JustEvade", "2ae7316d95aaabae76d88aa7ccdd93c9" },
        { "Trophy_JustParry", "1b0973e8cbff1fc8452d84802a99730a" },
        { "Trophy_Platinum", "2139bbcdb215f1a02e4c12572c055c0a" },
        { "Trophy_Fishing", "9ed83962a8e46a0e13fb4b5e986d1ae6" },
        { "Trophy_NGPlus", "aa57541cf6ec1f96522b6955174094b5" }
    };

    public StellarBladeAchievementImageService()
    {
        // Criar diretório se não existir
        Directory.CreateDirectory(_imagesPath);
    }

    public async Task<string?> GetAchievementImageAsync(string achievementName)
    {
        var cacheKey = achievementName;
        if (_imageCache.TryGetValue(cacheKey, out var cached))
            return cached;

        // Verificar se a imagem já foi baixada
        var localPath = Path.Combine(_imagesPath, $"{achievementName}.png");
        if (File.Exists(localPath))
        {
            _imageCache[cacheKey] = localPath;
            return localPath;
        }

        // Verificar se temos o hash da imagem
        if (!AchievementImageMap.TryGetValue(achievementName, out var imageHash))
            return null;

        try
        {
            // URL da imagem na Epic Games Store
            var imageUrl = $"https://shared-static-prod.epicgames.com/epic-achievements/5013c0fbf0aa4a5e84d948ab5bfe99c6/321685d747d24252b113786aecadd53e/icons/{imageHash}";
            
            // Baixar a imagem
            var response = await _http.GetAsync(imageUrl);
            response.EnsureSuccessStatusCode();
            
            var imageBytes = await response.Content.ReadAsByteArrayAsync();
            await File.WriteAllBytesAsync(localPath, imageBytes);
            
            _imageCache[cacheKey] = localPath;
            return localPath;
        }
        catch
        {
            return null;
        }
    }

    public async Task DownloadAllAchievementImagesAsync()
    {
        var tasks = AchievementImageMap.Select(async kvp =>
        {
            await GetAchievementImageAsync(kvp.Key);
        });

        await Task.WhenAll(tasks);
    }

    public static string GetDefaultAchievementIcon(bool achieved)
    {
        return achieved 
            ? "ms-appx:///Assets/Achievements/achieved.png"
            : "ms-appx:///Assets/Achievements/locked.png";
    }
}
