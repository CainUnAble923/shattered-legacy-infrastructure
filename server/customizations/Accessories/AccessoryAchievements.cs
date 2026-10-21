// cc-P262 (spec section 7): the accessory and barefoot achievements. They share P231's hidden category (Mischief, the
// "Noted by the System" achievements: hidden until earned, a snarky flavor line, then the plain "(Earned: ...)" line its
// trigger writes), so there is one such category, not two.
//
//   Plate Bottoms Only   1,000 tiles run barefoot in 4+ armour pieces, legs and chest included, not mounted: toe ring slot 1
//   Iron Soles           25,000 such tiles: toe ring slot 2 and the title "the Unshod"
//   Ring Bearer          all four ring slots filled
//   Fully Accessorized   every jewelry slot filled, toe rings included
//   Smothered            footwear put on over counting toe rings, 10 times
//
// The tile and smother counters are per account, as every achievement counter is (ClusterFAchievementSystem.Increment).

#nullable enable

using Server.Accounting;
using Server.Engines.LeagueRewards;
using Server.Mobiles;

using T = Server.AchievementTrigger;

namespace Server;

public static partial class ClusterFAchievementSystem
{
    public const string PlateBottomsOnlyKey   = "noted.plate_bottoms_only";
    public const string IronSolesKey          = "noted.iron_soles";
    public const string RingBearerKey         = "noted.ring_bearer";
    public const string FullyAccessorizedKey  = "noted.fully_accessorized";
    public const string SmotheredKey          = "noted.smothered";

    public const string BarefootTilesCounter  = "barefoot_armored_tiles";
    public const string SmotheredCounter      = "toe_rings_smothered";

    public const int PlateBottomsOnlyTiles = 1000;
    public const int IronSolesTiles        = 25000;
    public const int SmotheredTimes        = 10;

    private static void RegisterAccessories()
    {
        const AchievementCategory cat = AchievementCategory.Mischief;

        Reg(PlateBottomsOnlyKey,
            "Plate Bottoms Only",
            "Ran 1,000 tiles in plate armor with no shoes.",
            cat, ap: 10, renown: 2,
            trigger:         T.Count(TriggerKind.BarefootArmoredTiles, PlateBottomsOnlyTiles),
            flavor:          "Two hundred pounds of steel protecting everything except the part touching the ground.",
            itemGumpId:      0x1411, // PlateLegs
            hidden:          true,
            progressCounter: BarefootTilesCounter);

        Reg(IronSolesKey,
            "Iron Soles",
            "Ran 25,000 tiles in armor with no shoes.",
            cat, ap: 25, renown: 10,
            trigger:         T.Count(TriggerKind.BarefootArmoredTiles, IronSolesTiles),
            flavor:          "Your feet have filed for guild status.",
            itemGumpId:      0x1411,
            hidden:          true,
            prerequisiteKey: PlateBottomsOnlyKey,
            progressCounter: BarefootTilesCounter);

        Reg(RingBearerKey,
            "Ring Bearer",
            "Wore four rings at once.",
            cat, ap: 10, renown: 2,
            trigger:    T.Event(TriggerKind.RingBearer),
            flavor:     "Your fingers are now load-bearing.",
            itemGumpId: 0x108A, // GoldRing
            hidden:     true);

        Reg(FullyAccessorizedKey,
            "Fully Accessorized",
            "Filled every jewelry slot.",
            cat, ap: 15, renown: 3,
            trigger:    T.Event(TriggerKind.FullyAccessorized),
            flavor:     "The System estimates you are 30% jewelry by weight.",
            itemGumpId: 0x1086, // GoldBracelet
            hidden:     true);

        Reg(SmotheredKey,
            "Smothered",
            "Put shoes on over your toe rings 10 times.",
            cat, ap: 5, renown: 0,
            trigger:         T.Count(TriggerKind.ToeRingsSmothered, SmotheredTimes),
            flavor:          "They trusted you.",
            itemGumpId:      0x170F, // Shoes
            hidden:          true,
            progressCounter: SmotheredCounter);
    }

    /// <summary>One counting barefoot step (ClusterFAccessories.CountsAsBarefootStep said yes).</summary>
    public static void NotifyBarefootStep(PlayerMobile pm)
    {
        if (pm.Account is not IAccount acct)
        {
            return;
        }

        var n = Increment(acct.Username, BarefootTilesCounter);

        // Every hundredth tile: both thresholds are multiples of 100, and a step stays one dictionary write.
        if (n % 100 == 0)
        {
            var hadSlots = ClusterFAccessories.ToeRingSlots(pm);
            GrantCounted(acct, TriggerKind.BarefootArmoredTiles, n);
            SyncUnshodTitle(acct);

            if (ClusterFAccessories.ToeRingSlots(pm) > hadSlots)
            {
                pm.SendMessage(0x59, "The System has approved your feet. A toe ring slot is open. ([accessories)");
                ClusterFAccessories.Refresh(pm);
            }
        }
    }

    /// <summary>Footwear went on over counting toe rings.</summary>
    public static void NotifyToeRingsSmothered(PlayerMobile pm)
    {
        if (pm.Account is not IAccount acct)
        {
            return;
        }

        GrantCounted(acct, TriggerKind.ToeRingsSmothered, Increment(acct.Username, SmotheredCounter));
    }

    public static void NotifyAccessoryEvent(PlayerMobile pm, TriggerKind kind)
    {
        if (pm.Account is IAccount acct)
        {
            GrantEvent(acct, kind);
        }
    }

    /// <summary>Iron Soles grants the title "the Unshod" (held by the account, kept like every League title).</summary>
    public static void SyncUnshodTitle(IAccount acct)
    {
        if (HasEarned(acct, IronSolesKey))
        {
            ClusterFAccountPersistence.GetOrCreate(acct).LeagueTitles.Owned.Add(LeagueTitles.Unshod);
        }
    }
}
