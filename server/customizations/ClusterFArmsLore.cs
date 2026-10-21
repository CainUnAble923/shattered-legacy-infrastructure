// cc-P226, F-26 parts 1 to 4 and 6 (Chase 2026-10-02): Arms Lore matters more, and trains to the 200 cap.
//
// OSI's Arms Lore is a crafter's side skill: an exceptional weapon's damage increase and an exceptional armor piece's
// extra resists, plus a coarse inspect. Every job below is ours, registered in shard-migration docs/fidelity-principle.md.
// All of its numbers live here; the pinned files only call in (server/patches):
//
// 1. Crafting, rescaled to 200 (BaseWeapon-arms-lore.patch, BaseArmor-arms-lore.patch). Exceptional weapon damage increase
//    35 + 15 x (Arms Lore / 200): 42 at 100, 50 at 200 (pinned 35 + Arms Lore / 20, BaseWeapon.cs:677). Exceptional
//    armor's extra resist points stay Arms Lore / 20 but stop at 10 a piece (pinned uncapped, BaseArmor.cs:571).
// 2. Gear lasts longer. The wielder's or wearer's Arms Lore saves wear: Arms Lore / 400 of it, so 25% at 100 and 50% at
//    200. One call per item kind, where pinned takes the hit points: BaseWeapon.OnHit's wear point (BaseWeapon.cs:2080),
//    BaseArmor.OnHit's wear (BaseArmor.cs:743) and BaseShield.TryLowerDurability (BaseShield.cs:78). Self Repair is
//    rolled first and is untouched: this only thins the wear that gets past it.
// 3. Field maintenance. Using Arms Lore on your own damaged weapon or armor (in your pack or worn), when the inspect
//    succeeds, restores 2 + Arms Lore / 10 durability (12 at 100, 22 at 200), never above its maximum, with a
//    (200 - Arms Lore) / 400 chance to lose 1 maximum first (50% at 0, 25% at 100, none at 200). Once every 10 seconds.
//    A smith's repair still restores the item to full (Repair.cs:297, :367).
// 4. Better smith repairs. A player repairing weapons or armor with a craft tool (not a repair deed) has the chance to
//    lose maximum durability (Repair.cs:41-42, rolled at :287 and :357) lowered by Arms Lore / 10 points.
// 6. Gain to 200. Pinned's checks are all (0, 100). Past 100, hook G (ClusterFSkillGain, cc-P66) rolls on the item's
//    tier: Difficulty(item) +/- 50, where Difficulty is the material's craft requirement, +25 exceptional, +25 magic
//    (runic or loot properties). Inspect and field maintenance share the use's check (ArmsLore.cs:33, :89, a hook G
//    row); the crafting check calls ClusterFSkillGain's crafted roll, as Detect Hidden's found-something roll does.
//
// Notes: shard-migration notes/cc-P226-arms-lore-matters.md.

using System;
using System.Linq;
using Server.Items;

namespace Server;

public static class ClusterFArmsLore
{
    public const double SkillCeiling = 200.0;

    public const int ExceptionalDamageBase = 35;
    public const int ExceptionalDamageSpread = 15;
    public const int MaxExceptionalResistBonus = 10;

    public const double ExceptionalTier = 25.0;
    public const double MagicTier = 25.0;
    public const double TierHalfWidth = 50.0;

    public static readonly TimeSpan MaintenanceDelay = TimeSpan.FromSeconds(10.0);

    private static double Clamp(double armsLore) => Math.Clamp(armsLore, 0.0, SkillCeiling);

    // ---------------------------------------------------------------- 1. crafting

    /// <summary>What pinned adds to an exceptional weapon's 35: 15 x (Arms Lore / 200), so 7 at 100 and 15 at 200.</summary>
    public static int ExceptionalDamageBonus(double armsLore) =>
        (int)(ExceptionalDamageSpread * Clamp(armsLore) / SkillCeiling);

    /// <summary>Exceptional armor's extra random resist points: Arms Lore / 20, at most 10.</summary>
    public static int ExceptionalResistBonus(double armsLore) =>
        Math.Min((int)(Clamp(armsLore) / 20), MaxExceptionalResistBonus);

    // ---------------------------------------------------------------- 2. wear

    /// <summary>The share of wear the mobile's Arms Lore saves: Arms Lore / 400 (0.25 at 100, 0.5 at 200).</summary>
    public static double WearSaved(Mobile m) => m == null ? 0.0 : Clamp(m.Skills.ArmsLore.Value) / (2 * SkillCeiling);

    /// <summary>The wear left after <paramref name="m"/>'s Arms Lore: each point is saved with chance WearSaved.</summary>
    public static int ReduceWear(Mobile m, int wear)
    {
        var saved = WearSaved(m);

        if (wear <= 0 || saved <= 0.0)
        {
            return wear;
        }

        var left = 0;

        for (var i = 0; i < wear; i++)
        {
            if (Utility.RandomDouble() >= saved)
            {
                left++;
            }
        }

        return left;
    }

    /// <summary>True when the wielder's Arms Lore saves a weapon's single point of wear.</summary>
    public static bool SavesWeaponWear(Mobile wielder) => ReduceWear(wielder, 1) == 0;

    // ---------------------------------------------------------------- 3. field maintenance

    /// <summary>Durability one field maintenance restores: 2 + Arms Lore / 10.</summary>
    public static int MaintenanceRestore(double armsLore) => 2 + (int)(Clamp(armsLore) / 10);

    /// <summary>The chance one field maintenance costs 1 maximum durability: (200 - Arms Lore) / 400.</summary>
    public static double MaintenanceMaxLossChance(double armsLore) =>
        (SkillCeiling - Clamp(armsLore)) / (2 * SkillCeiling);

    /// <summary>
    /// Called by the patched Arms Lore inspect after a successful check on a weapon or armor. Tends the item when it is
    /// the user's own (in the pack or worn) and damaged. True when it restored durability.
    /// </summary>
    public static bool FieldMaintenance(Mobile from, Item item)
    {
        if (from == null || item is not IDurability gear || gear.MaxHitPoints <= 0 ||
            gear.HitPoints >= gear.MaxHitPoints || !(item.IsChildOf(from.Backpack) || item.Parent == from))
        {
            return false;
        }

        if (!from.CanBeginAction(typeof(ClusterFArmsLore)))
        {
            from.SendMessage("You must wait a moment before tending your gear again.");
            return false;
        }

        from.BeginAction(typeof(ClusterFArmsLore));
        Timer.StartTimer(MaintenanceDelay, () => from.EndAction(typeof(ClusterFArmsLore)));

        var armsLore = from.Skills.ArmsLore.Value;

        if (gear.MaxHitPoints > 1 && Utility.RandomDouble() < MaintenanceMaxLossChance(armsLore))
        {
            gear.MaxHitPoints--;
            gear.HitPoints = Math.Min(gear.HitPoints, gear.MaxHitPoints);
            from.SendMessage("Without a forge, your work leaves it slightly weaker.");
        }

        var before = gear.HitPoints;
        gear.HitPoints = Math.Min(gear.MaxHitPoints, gear.HitPoints + MaintenanceRestore(armsLore));

        from.SendMessage(
            gear.HitPoints >= gear.MaxHitPoints
                ? "You tend to the wear, and it is in full repair."
                : $"You tend to the wear, restoring {gear.HitPoints - before} durability."
        );

        return gear.HitPoints > before;
    }

    // ---------------------------------------------------------------- 4. smith repairs

    /// <summary>
    /// Pinned's weaken chance (Repair.cs:41-42, a percent) for a repair: lowered by Arms Lore / 10 when a player repairs
    /// a weapon or armor with a tool; unchanged for a repair deed or anything else.
    /// </summary>
    public static int RepairWeakenChance(Mobile from, bool armsLoreApplies, int pinnedChance) =>
        armsLoreApplies && from != null ? pinnedChance - (int)(Clamp(from.Skills.ArmsLore.Value) / 10) : pinnedChance;

    // ---------------------------------------------------------------- 6. gain to 200

    /// <summary>A weapon or armor (shields included): what Arms Lore works on.</summary>
    public static bool IsGear(object o) => o is BaseWeapon or BaseArmor;

    /// <summary>
    /// The item's tier as an Arms Lore difficulty: its material's craft requirement, +25 exceptional, +25 magic. Iron is 0,
    /// Valorite 99, Celestial 200; a Valorite exceptional runic weapon is 149.
    /// </summary>
    public static double Difficulty(Item item)
    {
        var (resource, exceptional, magic) = item switch
        {
            BaseWeapon w => (w.Resource, w.Quality == WeaponQuality.Exceptional,
                HasMagicAttributes(w.Attributes) || !w.WeaponAttributes.IsEmpty || !w.SkillBonuses.IsEmpty),
            BaseArmor a => (a.Resource, a.Quality == ArmorQuality.Exceptional,
                !a.Attributes.IsEmpty || !a.ArmorAttributes.IsEmpty || !a.SkillBonuses.IsEmpty),
            _ => (CraftResource.None, false, false)
        };

        return MaterialTier(resource) + (exceptional ? ExceptionalTier : 0.0) + (magic ? MagicTier : 0.0);
    }

    // Any attribute but the damage increase, which every exceptional craft carries (BaseWeapon.cs:673) and is not magic.
    private static bool HasMagicAttributes(AosAttributes a) =>
        !a.IsEmpty && Enum.GetValues<AosAttribute>().Any(attr => attr != AosAttribute.WeaponDamage && a[attr] != 0);

    /// <summary>The craft skill the material needs: the shard's metal and wood tables, pinned's leathers and OSI woods.</summary>
    public static double MaterialTier(CraftResource r) =>
        CraftResources.GetType(r) switch
        {
            CraftResourceType.Metal => ClusterFMetalTiers.RequiredSkill(r),
            CraftResourceType.Wood => ClusterFWoodTiers.RequiredSkill(r) is > 0.0 and var shard ? shard : r switch
            {
                // Pinned DefCarpentry.cs:602-607.
                CraftResource.OakWood => 65.0,
                CraftResource.AshWood => 80.0,
                CraftResource.YewWood => 95.0,
                CraftResource.Heartwood or CraftResource.Bloodwood or CraftResource.Frostwood => 100.0,
                _ => 0.0
            },
            // Pinned DefTailoring.cs:663-665.
            CraftResourceType.Leather => r switch
            {
                CraftResource.SpinedLeather => 65.0,
                CraftResource.HornedLeather => 80.0,
                CraftResource.BarbedLeather => 99.0,
                CraftResource.BurrowhideLeather => 45.0, // cc-P36's starter leather
                _ => 0.0
            },
            _ => 0.0
        };

    /// <summary>The window an Arms Lore use on this item rolls on past 100: Difficulty +/- 50.</summary>
    public static (double Min, double Max) Window(Item item)
    {
        var d = Difficulty(item);
        return (d - TierHalfWidth, d + TierHalfWidth);
    }
}
