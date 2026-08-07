using BaseLib.Config;
using System.Globalization;

namespace AutoBattlerMod.AutoBattlerModCode;

public sealed class ModConfig : SimpleModConfig
{
    [ConfigSection("Relic")]
    [ConfigSlider(0, 10, 1)]
    [ConfigHoverTip]
    public static double RelicGivesXBonusEnergy { get; set; } = 1;

    [ConfigHoverTip]
    public static bool AddRelicOnRunStartByDefault { get; set; } = true;

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
    public static string InMultiplayerGiveRelicOnlyToTheseSteam64Ids { get; set; } = "";

    [ConfigIgnore]
    public static HashSet<ulong> MultiplayerRecipientIds =>
        InMultiplayerGiveRelicOnlyToTheseSteam64Ids
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => ulong.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out ulong id)
                ? id
                : (ulong?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToHashSet();
}