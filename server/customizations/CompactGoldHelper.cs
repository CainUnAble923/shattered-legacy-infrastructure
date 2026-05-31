using System;
using Server.Items;
using Server.Mobiles;

namespace Server.Items;

/// <summary>
/// Gold balance and consumption helper for Miners' Compact upgrade and restoration flows.
///
/// All Compact gold costs are paid from the combined backpack + bank balance so that
/// gold deposited by the Survey Archivist (Velara Thorne) is immediately spendable
/// on upgrades and restorations without requiring the player to visit a bank first.
///
/// Consumption order: backpack gold is consumed first; any shortfall is withdrawn
/// from the bank box via Banker.Withdraw.
/// </summary>
internal static class CompactGoldHelper
{
    /// <summary>
    /// Returns the player's total available gold: backpack items + bank balance
    /// (bank box gold, bank checks, and account ledger gold if AccountGold is enabled).
    /// </summary>
    public static int GetTotalGold(PlayerMobile pm)
    {
        var packGold = pm.Backpack?.GetAmount(typeof(Gold)) ?? 0;
        var bankGold = Banker.GetBalance(pm);
        return (int)Math.Min((long)packGold + bankGold, int.MaxValue);
    }

    /// <summary>
    /// Attempts to consume <paramref name="amount"/> gold, drawing from backpack first
    /// and then from the bank for any shortfall. Returns <c>true</c> on success.
    /// Returns <c>false</c> without touching anything if total gold is insufficient.
    /// </summary>
    public static bool ConsumeGold(PlayerMobile pm, int amount)
    {
        if (amount <= 0) return true;
        if (GetTotalGold(pm) < amount) return false;

        var pack     = pm.Backpack;
        var packGold = pack?.GetAmount(typeof(Gold)) ?? 0;

        if (packGold >= amount)
        {
            // Entire cost covered by backpack
            pack!.ConsumeTotal(typeof(Gold), amount);
        }
        else
        {
            // Drain backpack first, withdraw remainder from bank
            if (packGold > 0)
                pack!.ConsumeTotal(typeof(Gold), packGold);

            Banker.Withdraw(pm, amount - packGold);
        }

        return true;
    }
}
