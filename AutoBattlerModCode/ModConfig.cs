using System.Reflection;
using System.Text.Json;

namespace AutoBattlerMod.AutoBattlerModCode;

public class ModConfig
{
    public decimal BonusEnergy { get; set; } = 1m;
    public bool AddRelicOnRunStart { get; set; } = true;
    public bool AutoUsePotions { get; set; } = true;
    public bool AutoPlay { get; set; } = true;
    public bool AutoEndTurn { get; set; } = true;
    public List<ulong> GiveRelicOnlyToNetIds { get; set; } = [];

    private void Validate()
    {
        if (BonusEnergy < 0)
            BonusEnergy = 1m;

        GiveRelicOnlyToNetIds ??= [];
    }

    public static ModConfig Load()
    {
        try
        {
            string path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "config.cfg");

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
                $"{nameof(config.BonusEnergy)}={config.BonusEnergy}, " +
                $"{nameof(config.AddRelicOnRunStart)}={config.AddRelicOnRunStart}, " +
                $"{nameof(config.GiveRelicOnlyToNetIds)}=[{string.Join(",", config.GiveRelicOnlyToNetIds)}], " +
                $"{nameof(config.AutoEndTurn)}={config.AutoEndTurn}, " +
                $"{nameof(config.AutoUsePotions)}={config.AutoUsePotions}, " +
                $"{nameof(config.AutoPlay)}={config.AutoPlay}");

            return config;
        }
        catch (Exception ex)
        {
            AutoBattlerMod.Log($"Failed to load config: {ex.Message}");
            return new ModConfig();
        }
    }
}
