using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace AutoBattlerMod.AutoBattlerModCode.TurnPlayer;

public static class TurnEnder
{
    public static void EndTurn(Player player)
    {
        TryHidingEndTurnButton();

        if (!CombatManager.Instance.IsOverOrEnding && !CombatManager.Instance.IsPlayerReadyToEndTurn(player))
            RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue(
                new EndPlayerTurnAction(player, player.PlayerCombatState!.TurnNumber));
    }

    public static NEndTurnButton? cachedEndTurnButton;
    public static void TryHidingEndTurnButton()
    {
        try
        {
            NEndTurnButton? button = GetEndTurnButton();

            if (button == null)
            {
                AutoBattlerMod.Log("TryHidingEndTurnButton: Could not find NEndTurnButton.");
                return;
            }

            button.Hide();

            AutoBattlerMod.Log($"TryHidingEndTurnButton: Successfully called Hide() on button at '{button.GetPath()}'.");
        }
        catch (Exception ex)
        {
            cachedEndTurnButton = null;
            AutoBattlerMod.Log($"TryHidingEndTurnButton: Unexpected error: {ex}");
        }
    }

    public static NEndTurnButton? GetEndTurnButton()
    {
        if (cachedEndTurnButton != null && GodotObject.IsInstanceValid(cachedEndTurnButton))
            return cachedEndTurnButton;

        cachedEndTurnButton = FindEndTurnButton(NCombatRoom.Instance!.Ui);
        AutoBattlerMod.Log($"Cached EndTurnButton at '{cachedEndTurnButton?.GetPath()}'.");
        return cachedEndTurnButton;
    }

    public static NEndTurnButton? FindEndTurnButton(Node node)
    {
        if (node is NEndTurnButton button)
            return button;

        foreach (Node child in node.GetChildren())
        {
            NEndTurnButton? found = FindEndTurnButton(child);
            if (found != null)
                return found;
        }

        return null;
    }
}
