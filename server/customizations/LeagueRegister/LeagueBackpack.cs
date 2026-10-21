// cc-P280 (Chase, answer 6): backpack upgrades, item count only. Four steps past pinned's 125 (Server/Items/Container.cs,
// GlobalMaxItems): 150 at Bronze, 175 at Verite, 200 at Blaze, 250 at Mythril, each bought in order for Renown and a
// little gold (LeagueRegisterSettings.BackpackTable). Weight is not sold: the shard already ties carry weight to Strength
// with no ceiling (ClusterFBackpack.cs).
//
// How it reaches the pack: as ClusterFBankLimit does for the bank. Container.MaxItems is per container and saved, so the
// bought tier (CharacterLeagueData.PackTier) is written to the backpack's MaxItems at world load, at login and on
// purchase. GlobalMaxItems never changes, so every other container keeps 125.

using System.Collections.Generic;
using ModernUO.CodeGeneratedEvents;
using Server.Accounting;
using Server.Items;
using Server.Mobiles;

namespace Server.Engines.LeagueRegister;

public static class LeagueBackpack
{
    /// <summary>One step: the rank it needs, the items it gives, and its price.</summary>
    public readonly record struct Tier(int Number, int Rank, int Items, int Renown, int Gold);

    public static List<Tier> Tiers
    {
        get
        {
            var tiers = new List<Tier>();
            foreach (var (rank, v) in LeagueRegisterSettings.Rows(LeagueRegisterSettings.BackpackTable))
            {
                if (v.Length >= 3)
                {
                    tiers.Add(new Tier(tiers.Count + 1, rank, v[0], v[1], v[2]));
                }
            }

            return tiers;
        }
    }

    public static void Configure()
    {
        EventSink.WorldLoad += () =>
        {
            foreach (var m in World.Mobiles.Values)
            {
                if (m is PlayerMobile pm)
                {
                    Apply(pm);
                }
            }
        };
    }

    [OnEvent(nameof(PlayerMobile.PlayerLoginEvent))]
    public static void OnLogin(PlayerMobile pm) => Apply(pm);

    public static int BoughtTier(Mobile m) => Data(m)?.PackTier ?? 0;

    /// <summary>The items this character's backpack holds: the bought tier's, or pinned's default (-1 in MaxItems).</summary>
    public static int MaxItemsFor(Mobile m)
    {
        var tier = BoughtTier(m);
        var tiers = Tiers;
        return tier > 0 && tier <= tiers.Count ? tiers[tier - 1].Items : -1;
    }

    /// <summary>Writes the bought tier to the backpack's saved MaxItems (-1 is pinned's default).</summary>
    public static void Apply(Mobile m)
    {
        if (m?.Backpack is not { } pack)
        {
            return;
        }

        var limit = MaxItemsFor(m);
        if (pack.MaxItems != limit && (limit > 0 || pack.MaxItems > 0))
        {
            pack.MaxItems = limit > 0 ? limit : Container.GlobalMaxItems;
        }
    }

    /// <summary>The next step this character can buy, or null (all bought).</summary>
    public static Tier? Next(Mobile m)
    {
        var tiers = Tiers;
        var bought = BoughtTier(m);
        return bought < tiers.Count ? tiers[bought] : null;
    }

    /// <summary>Why the next step cannot be bought now, or null.</summary>
    public static string Refuse(PlayerMobile pm, out Tier tier)
    {
        tier = default;
        if (Next(pm) is not { } next)
        {
            return "Your backpack is as large as the League can make it.";
        }

        tier = next;
        var data = AccountData(pm);
        if (data == null)
        {
            return "Account data unavailable.";
        }

        if (ClusterFLeagueRanks.GetRank(pm) < next.Rank)
        {
            return $"That needs {ClusterFLeagueRanks.RankName(next.Rank)}.";
        }

        return LeaguePrice.Refuse(pm, next.Renown, next.Gold);
    }

    /// <summary>Buys the next step: re-checks, pays (Renown, then gold), records it and widens the pack.</summary>
    public static bool Buy(PlayerMobile pm, out string message)
    {
        message = Refuse(pm, out var tier);
        if (message != null || !LeaguePrice.Pay(pm, tier.Renown, tier.Gold))
        {
            message ??= "You cannot pay for that.";
            return false;
        }

        ClusterFAccountPersistence.GetOrCreate((IAccount)pm.Account).GetOrCreateLeagueData(pm.Serial).PackTier = tier.Number;
        Apply(pm);
        message = $"Your backpack now holds {tier.Items} items.";
        return true;
    }

    private static CharacterLeagueData Data(Mobile m) =>
        m?.Account is IAccount acct ? ClusterFAccountPersistence.Get(acct)?.GetLeagueData(m.Serial) : null;

    private static ClusterFAccountData AccountData(Mobile m) =>
        m?.Account is IAccount acct ? ClusterFAccountPersistence.GetOrCreate(acct) : null;
}

/// <summary>
/// cc-P280 (Chase, answer 8): League goods are paid in Renown, with gold as a small second cost (pack then bank, as the
/// guilds' prices are, CompactGoldHelper). All or nothing: the gold is checked before the Renown is spent.
/// </summary>
public static class LeaguePrice
{
    public static string Refuse(PlayerMobile pm, int renown, int gold)
    {
        var data = pm.Account is IAccount acct ? ClusterFAccountPersistence.GetOrCreate(acct) : null;
        if (data == null)
        {
            return "Account data unavailable.";
        }

        if (data.Renown < renown)
        {
            return $"That costs {renown:N0} Renown; you have {data.Renown:N0}.";
        }

        if (gold > 0 && CompactGoldHelper.GetTotalGold(pm) < gold)
        {
            return $"That also costs {gold:N0} gold.";
        }

        return null;
    }

    public static bool Pay(PlayerMobile pm, int renown, int gold)
    {
        if (Refuse(pm, renown, gold) != null)
        {
            return false;
        }

        var data = ClusterFAccountPersistence.GetOrCreate((IAccount)pm.Account);
        if (gold > 0 && !CompactGoldHelper.ConsumeGold(pm, gold))
        {
            return false;
        }

        return data.SpendRenown(renown);
    }
}
