// =============================================================================
// RewardMounts.cs
// cc-P365: eighteen reward mounts from art on disk (notes: shard-migration notes/cc-P365-reward-mounts-and-evo-stages.md),
// each on our own body and mount item (registry.csv, records.json bodyAnims and tiledata; ShardBodyTypes), wired as
// P277's dragons are: BaseMount(body, mount item), the mount item's tiledata Animation names the body TazUO draws under
// the rider (Item.GetGraphicForAnimation).
//
// Two shapes:
//   - one body: the creature walks and is ridden on the same body (the raptors, the warhorse, the charger, the unicorn,
//     the bonewalker, the emberhoof, the bog strider, the war wolf);
//   - a ground body and a ridden look: Thagoras' four draft horses walk on the plain horse and are ridden as its saddled
//     file (walk, run, stand and eat only, which is all a ridden mount plays); Krazy Zacks' drake and bear, Prapilk's
//     riding wolf and Socrarr's saddled horse are people-layout files that hold the whole animal in the rider's mounted
//     actions (23-29), so they are only ever a ridden look, and the creature walks on the matching stock body (drake 0x3C,
//     polar bear 0xD5, timber wolf 0xE1, horse 0xC8). Krazy Zacks' files ship exactly as he made them ("Do not modify").
//
// Every mount is a reward, never tamed: Tamable false, one control slot, a horse's numbers (P277's placeholder), bonded
// to its owner when a statuette summons it. The routes (Route below) are proposals; Chase can move any of them:
//   League shop (1750 AP)  the four draft horses, the barded warhorse, the gilded charger      (LeagueShop.Stock)
//   achievement            the bonewalker, the emberhoof, the unicorn, the war wolf           (ClusterFAchievements)
//   loot box               Tamer tier 5-6: drake, bear, riding wolf, saddled horse;
//                          Boss tier 6: the three raptors and the bog strider                  (LootBoxRoller.ThemeSlot)
// =============================================================================

#nullable enable

using System;
using ModernUO.Serialization;
using Server.Engines.LeagueRewards;
using Server.Items;
using Server.LootBoxes;

namespace Server.Mobiles;

public enum RewardMountRoute
{
    LeagueShop,
    Achievement,
    LootBox
}

/// <summary>One reward mount: its key, name, the body it walks on, its mount item and what earns it.</summary>
public sealed record RewardMountDef(
    string Key, string Name, int Body, int MountItem, int RiddenBody, int Sound, RewardMountRoute Route, string RouteDetail,
    Type MountType
)
{
    public BaseMount Create() => (BaseMount)Activator.CreateInstance(MountType)!;
}

public static class RewardMounts
{
    public const int FirstMountItem = 0xB650;
    public const int LastMountItem = 0xB661;

    // Renown prices of the League shop rows (all at 1750 AP: past the Gold box's 1000, so no box hands one out).
    public const int ShopAp = 1750;

    public static readonly RewardMountDef[] Defs =
    [
        new("marsh-raptor", "a marsh raptor", 2088, 0xB650, 2088, 0x5A, RewardMountRoute.LootBox, "Boss box, tier 6", typeof(RewardMarshRaptor)),
        new("dusk-raptor", "a dusk raptor", 2089, 0xB651, 2089, 0x5A, RewardMountRoute.LootBox, "Boss box, tier 6", typeof(RewardDuskRaptor)),
        new("sky-raptor", "a sky raptor", 2090, 0xB652, 2090, 0x5A, RewardMountRoute.LootBox, "Boss box, tier 6", typeof(RewardSkyRaptor)),
        new("chestnut-draft", "a chestnut draft horse", 2091, 0xB653, 2104, 0xA8, RewardMountRoute.LeagueShop, "1750 AP, 750 Renown", typeof(RewardChestnutDraftHorse)),
        new("bay-draft", "a bay draft horse", 2092, 0xB654, 2105, 0xA8, RewardMountRoute.LeagueShop, "1750 AP, 750 Renown", typeof(RewardBayDraftHorse)),
        new("grey-draft", "a grey draft horse", 2093, 0xB655, 2106, 0xA8, RewardMountRoute.LeagueShop, "1750 AP, 750 Renown", typeof(RewardGreyDraftHorse)),
        new("palomino-draft", "a palomino draft horse", 2094, 0xB656, 2108, 0xA8, RewardMountRoute.LeagueShop, "1750 AP, 750 Renown", typeof(RewardPalominoDraftHorse)),
        new("barded-warhorse", "a barded warhorse", 2095, 0xB657, 2095, 0xA8, RewardMountRoute.LeagueShop, "1750 AP, 1000 Renown", typeof(RewardBardedWarhorse)),
        new("gilded-charger", "a gilded charger", 2096, 0xB658, 2096, 0xA8, RewardMountRoute.LeagueShop, "1750 AP, 1500 Renown", typeof(RewardGildedCharger)),
        new("moonlit-unicorn", "a moonlit unicorn", 2097, 0xB659, 2097, 0x4BC, RewardMountRoute.Achievement, "exploration.all_facets", typeof(RewardMoonlitUnicorn)),
        new("bonewalker-steed", "a bonewalker steed", 2098, 0xB65A, 2098, 0xA8, RewardMountRoute.Achievement, "combat.undead_hunter", typeof(RewardBonewalkerSteed)),
        new("emberhoof", "an emberhoof", 2099, 0xB65B, 2099, 0xA8, RewardMountRoute.Achievement, "combat.ten_thousand", typeof(RewardEmberhoof)),
        new("bog-strider", "a bog strider", 2100, 0xB65C, 2100, 0x5A, RewardMountRoute.LootBox, "Boss box, tier 6", typeof(RewardBogStrider)),
        new("war-wolf", "a war wolf", 2102, 0xB65D, 2102, 0xE5, RewardMountRoute.Achievement, "league.ap_500", typeof(RewardWarWolf)),
        new("riding-drake", "a riding drake", 0x3C, 0xB65E, 2109, 362, RewardMountRoute.LootBox, "Tamer box, tier 5-6", typeof(RewardRidingDrake)),
        new("riding-bear", "a riding bear", 0xD5, 0xB65F, 2110, 0xA3, RewardMountRoute.LootBox, "Tamer box, tier 5-6", typeof(RewardRidingBear)),
        new("riding-wolf", "a riding wolf", 0xE1, 0xB660, 2111, 0xE5, RewardMountRoute.LootBox, "Tamer box, tier 5-6", typeof(RewardRidingWolf)),
        new("saddled-horse", "a saddled horse", 0xC8, 0xB661, 2112, 0xA8, RewardMountRoute.LootBox, "Tamer box, tier 5-6", typeof(RewardSaddledHorse))
    ];

    public static RewardMountDef? Find(string? key) => Array.Find(Defs, d => d.Key == key);

    public static RewardMountDef Of(Type mountType) =>
        Array.Find(Defs, d => d.MountType == mountType) ?? throw new ArgumentException($"{mountType.Name} is not a reward mount");

    // A stock horse's numbers (pinned Horse.cs), P277's placeholders until Chase decides what each mount is worth.
    internal static void SetStats(BaseMount m)
    {
        m.SetStr(22, 98);
        m.SetDex(56, 75);
        m.SetInt(6, 10);
        m.SetHits(28, 45);
        m.SetMana(0);
        m.SetDamage(3, 4);
        m.SetDamageType(ResistanceType.Physical, 100);
        m.SetResistance(ResistanceType.Physical, 15, 20);
        m.SetSkill(SkillName.MagicResist, 25.1, 30.0);
        m.SetSkill(SkillName.Tactics, 29.3, 44.0);
        m.SetSkill(SkillName.Wrestling, 29.3, 44.0);
        m.Fame = 300;
        m.Karma = 300;
        m.Tamable = false;
        m.ControlSlots = 1;
    }

    /// <summary>
    /// The mount, bonded to this player and following, beside them (PackMule.SummonFor's steps); null when the player is
    /// dead or has no free follower slot, with the line why.
    /// </summary>
    public static BaseMount? SummonFor(PlayerMobile pm, RewardMountDef def)
    {
        if (!pm.Alive)
        {
            pm.SendMessage(0x22, "You cannot call a mount while dead.");
            return null;
        }

        var mount = def.Create();

        if (pm.Followers + mount.ControlSlots > pm.FollowersMax)
        {
            mount.Delete();
            pm.SendMessage(0x22, "You have too many followers to call your mount. Stable or dismiss a current follower first.");
            return null;
        }

        if (!mount.SetControlMaster(pm))
        {
            mount.Delete();
            return null;
        }

        mount.Owners.Add(pm);
        mount.IsBonded = true;
        mount.BondingBegin = DateTime.UtcNow;
        mount.OwnerAbandonTime = DateTime.MinValue;
        mount.ControlTarget = pm;
        mount.ControlOrder = OrderType.Follow;
        mount.MoveToWorld(pm.Location, pm.Map);
        pm.PlaySound(def.Sound);
        pm.SendMessage(0x44, $"System: {Capital(def.Name)} answers your call. It is bonded to you.");
        return mount;
    }

    private static string Capital(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    // -- Routes ---------------------------------------------------------------------------------------------------------

    /// <summary>The League shop rows: every mount routed there, as a statuette at 1750 AP.</summary>
    public static LeagueShopEntry[] ShopRows()
    {
        var rows = new System.Collections.Generic.List<LeagueShopEntry>();

        foreach (var d in Defs)
        {
            if (d.Route == RewardMountRoute.LeagueShop)
            {
                var key = d.Key;
                rows.Add(new LeagueShopEntry($"mount.{key}", $"Mount: {d.Name[(d.Name.IndexOf(' ') + 1)..]}", ShopAp, ShopRenown(key),
                    LeagueShopKind.Mount, d.MountItem, 0, _ => [new RewardMountStatuette(key)]));
            }
        }

        return [.. rows];
    }

    public static int ShopRenown(string key) => key switch
    {
        "gilded-charger"  => 1500,
        "barded-warhorse" => 1000,
        _                 => 750
    };

    public const string TamerChanceSetting = "clusterf.rewardMounts.tamerBoxChance";
    public const string BossChanceSetting = "clusterf.rewardMounts.bossBoxChance";

    /// <summary>Tamer boxes of tier 5 and 6 (by default 10% and 20%) and Boss boxes of tier 6 (15%) may hold a mount.</summary>
    public static RewardMountStatuette? LootBoxMount(BoxTheme theme, int tier, double roll, int pick)
    {
        var chance = theme switch
        {
            BoxTheme.Tamer when tier >= 5 => ServerConfiguration.GetOrUpdateSetting(TamerChanceSetting, 0.10) * (tier - 4),
            BoxTheme.Boss when tier >= 6  => ServerConfiguration.GetOrUpdateSetting(BossChanceSetting, 0.15),
            _                             => 0.0
        };

        if (roll >= chance)
        {
            return null;
        }

        var detail = theme == BoxTheme.Tamer ? "Tamer box, tier 5-6" : "Boss box, tier 6";
        var pool = Array.FindAll(Defs, d => d.Route == RewardMountRoute.LootBox && d.RouteDetail == detail);
        return pool.Length == 0 ? null : new RewardMountStatuette(pool[Math.Abs(pick) % pool.Length].Key);
    }
}

/// <summary>A reward mount; each kind below is one row of RewardMounts.Defs.</summary>
[SerializationGenerator(0, false)]
public abstract partial class RewardMount : BaseMount
{
    protected RewardMount(RewardMountDef def) : base(def.Body, def.MountItem, AIType.AI_Animal, FightMode.Aggressor)
    {
        BaseSoundID = def.Sound;
        RewardMounts.SetStats(this);
    }

    public RewardMountDef Def => RewardMounts.Of(GetType());

    public override string DefaultName => Def.Name;

    public override string CorpseName => $"{Def.Name} corpse";

    public override FoodType FavoriteFood => FoodType.FruitsAndVeggies | FoodType.GrainsAndHay;
}

[SerializationGenerator(0, false)]
public partial class RewardMarshRaptor : RewardMount
{
    [Constructible(AccessLevel.GameMaster)]
    public RewardMarshRaptor() : base(RewardMounts.Defs[0]) { }

    public override FoodType FavoriteFood => FoodType.Meat;
}

[SerializationGenerator(0, false)]
public partial class RewardDuskRaptor : RewardMount
{
    [Constructible(AccessLevel.GameMaster)]
    public RewardDuskRaptor() : base(RewardMounts.Defs[1]) { }

    public override FoodType FavoriteFood => FoodType.Meat;
}

[SerializationGenerator(0, false)]
public partial class RewardSkyRaptor : RewardMount
{
    [Constructible(AccessLevel.GameMaster)]
    public RewardSkyRaptor() : base(RewardMounts.Defs[2]) { }

    public override FoodType FavoriteFood => FoodType.Meat;
}

[SerializationGenerator(0, false)]
public partial class RewardChestnutDraftHorse : RewardMount
{
    [Constructible(AccessLevel.GameMaster)]
    public RewardChestnutDraftHorse() : base(RewardMounts.Defs[3]) { }
}

[SerializationGenerator(0, false)]
public partial class RewardBayDraftHorse : RewardMount
{
    [Constructible(AccessLevel.GameMaster)]
    public RewardBayDraftHorse() : base(RewardMounts.Defs[4]) { }
}

[SerializationGenerator(0, false)]
public partial class RewardGreyDraftHorse : RewardMount
{
    [Constructible(AccessLevel.GameMaster)]
    public RewardGreyDraftHorse() : base(RewardMounts.Defs[5]) { }
}

[SerializationGenerator(0, false)]
public partial class RewardPalominoDraftHorse : RewardMount
{
    [Constructible(AccessLevel.GameMaster)]
    public RewardPalominoDraftHorse() : base(RewardMounts.Defs[6]) { }
}

[SerializationGenerator(0, false)]
public partial class RewardBardedWarhorse : RewardMount
{
    [Constructible(AccessLevel.GameMaster)]
    public RewardBardedWarhorse() : base(RewardMounts.Defs[7]) { }
}

[SerializationGenerator(0, false)]
public partial class RewardGildedCharger : RewardMount
{
    [Constructible(AccessLevel.GameMaster)]
    public RewardGildedCharger() : base(RewardMounts.Defs[8]) { }
}

[SerializationGenerator(0, false)]
public partial class RewardMoonlitUnicorn : RewardMount
{
    [Constructible(AccessLevel.GameMaster)]
    public RewardMoonlitUnicorn() : base(RewardMounts.Defs[9]) { }
}

[SerializationGenerator(0, false)]
public partial class RewardBonewalkerSteed : RewardMount
{
    [Constructible(AccessLevel.GameMaster)]
    public RewardBonewalkerSteed() : base(RewardMounts.Defs[10]) { }
}

[SerializationGenerator(0, false)]
public partial class RewardEmberhoof : RewardMount
{
    [Constructible(AccessLevel.GameMaster)]
    public RewardEmberhoof() : base(RewardMounts.Defs[11]) { }
}

[SerializationGenerator(0, false)]
public partial class RewardBogStrider : RewardMount
{
    [Constructible(AccessLevel.GameMaster)]
    public RewardBogStrider() : base(RewardMounts.Defs[12]) { }

    public override FoodType FavoriteFood => FoodType.Meat;
}

[SerializationGenerator(0, false)]
public partial class RewardWarWolf : RewardMount
{
    [Constructible(AccessLevel.GameMaster)]
    public RewardWarWolf() : base(RewardMounts.Defs[13]) { }

    public override FoodType FavoriteFood => FoodType.Meat;
}

[SerializationGenerator(0, false)]
public partial class RewardRidingDrake : RewardMount
{
    [Constructible(AccessLevel.GameMaster)]
    public RewardRidingDrake() : base(RewardMounts.Defs[14]) { }

    public override FoodType FavoriteFood => FoodType.Meat;
}

[SerializationGenerator(0, false)]
public partial class RewardRidingBear : RewardMount
{
    [Constructible(AccessLevel.GameMaster)]
    public RewardRidingBear() : base(RewardMounts.Defs[15]) { }

    public override FoodType FavoriteFood => FoodType.Fish | FoodType.Meat;
}

[SerializationGenerator(0, false)]
public partial class RewardRidingWolf : RewardMount
{
    [Constructible(AccessLevel.GameMaster)]
    public RewardRidingWolf() : base(RewardMounts.Defs[16]) { }

    public override FoodType FavoriteFood => FoodType.Meat;
}

[SerializationGenerator(0, false)]
public partial class RewardSaddledHorse : RewardMount
{
    [Constructible(AccessLevel.GameMaster)]
    public RewardSaddledHorse() : base(RewardMounts.Defs[17]) { }
}
