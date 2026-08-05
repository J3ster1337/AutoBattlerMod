using System.Reflection;
using System.Text.Json;

namespace AutoBattlerMod.AutoBattlerModCode;

public class ModConfig
{
    public decimal RelicGivesXBonusEnergy { get; set; } = 1m;
    public bool AddRelicOnRunStartByDefault { get; set; } = true;
    public bool AutoUsePotionsOnTurnStart { get; set; } = true;
    public bool AutoPlayCards { get; set; } = true;
    public bool AutoEndTurnWhenNoPlayableCardsLeft { get; set; } = true;
    public List<ulong> InMultiplayerGiveRelicOnlyToTheseSteam64Ids { get; set; } = [];

    private void Validate()
    {
        if (RelicGivesXBonusEnergy < 0)
            RelicGivesXBonusEnergy = 1m;

        InMultiplayerGiveRelicOnlyToTheseSteam64Ids ??= [];
    }

    public static ModConfig Load()
    {
        try
        {
            string path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "config.txt");

            if (!File.Exists(path))
            {
                File.WriteAllText(path, JsonSerializer.Serialize(new ModConfig(), new JsonSerializerOptions { WriteIndented = true }));
                AutoBattlerMod.Log($"Created default config: {path}");
                return new();
            }

            ModConfig config = JsonSerializer.Deserialize<ModConfig>(File.ReadAllText(path))
                ?? new ModConfig();

            config.Validate();

            AutoBattlerMod.Log(
                $"Loaded config:" +
                $"{nameof(config.RelicGivesXBonusEnergy)}={config.RelicGivesXBonusEnergy}, " +
                $"{nameof(config.AddRelicOnRunStartByDefault)}={config.AddRelicOnRunStartByDefault}, " +
                $"{nameof(config.InMultiplayerGiveRelicOnlyToTheseSteam64Ids)}=[{string.Join(",", config.InMultiplayerGiveRelicOnlyToTheseSteam64Ids)}], " +
                $"{nameof(config.AutoEndTurnWhenNoPlayableCardsLeft)}={config.AutoEndTurnWhenNoPlayableCardsLeft}, " +
                $"{nameof(config.AutoUsePotionsOnTurnStart)}={config.AutoUsePotionsOnTurnStart}, " +
                $"{nameof(config.AutoPlayCards)}={config.AutoPlayCards}");

            return config;
        }
        catch (Exception ex)
        {
            AutoBattlerMod.Log($"Failed to load config: {ex.Message}");
            return new ModConfig();
        }
    }
}
