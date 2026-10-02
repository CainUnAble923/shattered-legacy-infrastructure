// cc-P32 Part C, F-27: Self Repair becomes an extremely rare property.
//
// Pinned rolls Self Repair from the ordinary random property table: armor draws it from 20 slots, shields
// from 7, hats from 19 (Items/Skill Items/Tools/BaseRunicTool.cs:635, :703-707, :894-898). That table feeds
// monster loot, runic crafting, quest reward bags and treasure chests. server/patches/
// BaseRunicTool-no-random-self-repair.patch takes the slot out, so none of those can roll it any more,
// runic crafting included. Weapons never had it in that table.
//
// This file adds the only random source left: when a high-end creature dies, each magic weapon and each
// piece of magic armor (shields included) in its corpse has a 1 in 500 chance of Self Repair, at intensity
// 1 (70%), 2 (28%), 3 (1.5%), 4 (0.4%) or 5 (0.1%). Named artifacts, set pieces and anything that already
// has Self Repair keep their fixed values.
//
// High-end, decided here (notes/cc-P32-spawner-importer-bods-self-repair.md, Part C):
//   champion bosses (pinned BaseChampion, Mobiles/Special/BaseChampion.cs:11) and the Harrower;
//   paragons (BaseCreature.IsParagon, BaseCreature.cs:442);
//   peerless and SA bosses (our BasePeerless, which BaseSABoss derives from);
//   the revamped dungeon bosses (DespiseBoss, ShameGuardian) and Navrey Night-Eyes;
//   any creature that implements IRareSelfRepairSource (the New Haven dungeon mini champ, when it exists).
// Champion spawn minions are not high-end: a spawn kills thousands, which would make it common there.
//
// It runs on pinned's own BaseCreature.CreatureDeathEvent (BaseCreature.cs:3411), raised after the corpse is
// made and the loot is in it, so no pinned file changes for it. That event is also raised for a bonded pet's
// death (:3281); a controlled or summoned creature never counts.

using ModernUO.CodeGeneratedEvents;
using Server.Engines.Despise;
using Server.Items;
using Server.Mobiles;

namespace Server;

// A creature whose corpse gets the rare Self Repair roll. For our own high-end creatures.
public interface IRareSelfRepairSource
{
}

public static class ClusterFRareSelfRepair
{
    public const int OneIn = 500;

    [OnEvent(nameof(CreatureEvents.CreatureDeathEvent))]
    public static void OnCreatureDeath(BaseCreature bc)
    {
        if (!IsHighEndSource(bc) || bc.Corpse is not { Deleted: false } corpse)
        {
            return;
        }

        // A copy: nothing here moves an item, but the list belongs to the corpse.
        foreach (var item in corpse.Items.ToArray())
        {
            TryRoll(item);
        }
    }

    public static bool IsHighEndSource(BaseCreature bc) =>
        bc is { Deleted: false, Controlled: false, Summoned: false } &&
        (bc.IsParagon || bc is BaseChampion or Harrower or BasePeerless or DespiseBoss or ShameGuardian
             or NavreyNightEyes or IRareSelfRepairSource);

    // A magic weapon or piece of armor with no Self Repair of its own and no fixed values to keep.
    public static bool IsEligible(Item item) =>
        item switch
        {
            BaseWeapon w => w.ArtifactRarity == 0 && w.WeaponAttributes.SelfRepair == 0 &&
                            (!w.Attributes.IsEmpty || !w.WeaponAttributes.IsEmpty || !w.SkillBonuses.IsEmpty),
            BaseArmor a => a.ArtifactRarity == 0 && a.ArmorAttributes.SelfRepair == 0 &&
                           (!a.Attributes.IsEmpty || !a.ArmorAttributes.IsEmpty || !a.SkillBonuses.IsEmpty ||
                            a.PhysicalBonus != 0 || a.FireBonus != 0 || a.ColdBonus != 0 || a.PoisonBonus != 0 ||
                            a.EnergyBonus != 0),
            _ => false
        };

    // The roll for one item. True when it gained Self Repair.
    public static bool TryRoll(Item item)
    {
        if (!IsEligible(item) || Utility.Random(OneIn) != 0)
        {
            return false;
        }

        var intensity = RollIntensity();

        if (item is BaseWeapon weapon)
        {
            weapon.WeaponAttributes.SelfRepair = intensity;
        }
        else
        {
            ((BaseArmor)item).ArmorAttributes.SelfRepair = intensity;
        }

        item.InvalidateProperties();
        return true;
    }

    // 1 and 2 almost always; 3 to 5 between them 1 in 50.
    public static int RollIntensity() =>
        Utility.Random(1000) switch
        {
            < 700 => 1,
            < 980 => 2,
            < 995 => 3,
            < 999 => 4,
            _     => 5
        };
}
