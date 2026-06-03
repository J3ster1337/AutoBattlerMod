using System.Reflection;
using System.Text.Json;

namespace AutoBattlerMod.AutoBattlerModCode
{
    public class ModConfig
    {
        public decimal BonusEnergy { get; set; } = 1m;
        public bool AddRelicToAllCharacters { get; set; } = true;
        public bool AutoEndTurn { get; set; } = true;
        public bool AutoUsePotions { get; set; } = true;

        private void Validate()
        {
            if (BonusEnergy < 0)
                BonusEnergy = 1m;
        }

        public static ModConfig Load(Action<string>? log = null)
        {
            try
            {   
                string path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "config.cfg");

                if (!File.Exists(path))
                {
                    File.WriteAllText(path, JsonSerializer.Serialize(new ModConfig(), new JsonSerializerOptions { WriteIndented = true }));
                    log?.Invoke($"Created default config: {path}");
                    return new();
                }

                ModConfig config = JsonSerializer.Deserialize<ModConfig>(File.ReadAllText(path)) 
                    ?? new ModConfig();

                config.Validate();

                log?.Invoke(
                    $"Loaded config: BonusEnergy={config.BonusEnergy}, " +
                    $"AddRelicToAllCharacters={config.AddRelicToAllCharacters}, " +
                    $"AutoEndTurn={config.AutoEndTurn}, " +
                    $"AutoUsePotions={config.AutoUsePotions}");

                return config;
            }
            catch (Exception ex)
            {
                log?.Invoke($"Failed to load config: {ex.Message}");
                return new ModConfig();
            }
        }
    }
}
