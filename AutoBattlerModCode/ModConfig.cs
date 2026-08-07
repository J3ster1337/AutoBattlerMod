using BaseLib.Config;
using System.Globalization;

namespace AutoBattlerMod.AutoBattlerModCode;

public sealed class ModConfig : SimpleModConfig
{
    [ConfigSection("Relic")]

    [ConfigHoverTip]
    public static bool AddRelicOnRunStart { get; set; } = true;

    [ConfigSlider(0, 10, 1)]
    [ConfigHoverTip]
    public static double RelicGivesXBonusEnergy { get; set; } = 1;

    [ConfigSlider(0, 10, 1)]
    [ConfigHoverTip]
    public static double RelicGivesXBonusDraw { get; set; } = 1;

    [ConfigSection("Automation")]
    [ConfigHoverTip]
    public static bool AutoUsePotionsOnTurnStart { get; set; } = true;

    [ConfigHoverTip]
    public static bool AutoPlayCards { get; set; } = true;

    [ConfigHoverTip]
    public static bool AutoEndTurnWhenNoPlayableCardsLeft { get; set; } = true;

    [ConfigSection("Multiplayer")]
    [ConfigTextInput]
    [ConfigHoverTip]
    public static string Steam64IdsMultiplayerFilter { get; set; } = "";

    [ConfigIgnore]
    public static HashSet<ulong> MultiplayerRecipientIds =>
        Steam64IdsMultiplayerFilter
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => ulong.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out ulong id)
                ? id
                : (ulong?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToHashSet();
}