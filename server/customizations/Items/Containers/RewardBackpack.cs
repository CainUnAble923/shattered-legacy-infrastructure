// cc-P199: ServUO pub57 Scripts/Items/Containers/RewardBackpack.cs. The three dusty backpacks Percolem's tiered quests
// give (Quests/TieredQuests/PercolemTieredQuests.cs:22-584): Dusty Adventurer's (1113189, level 1), Dusty Explorer's
// (1113190, level 2) and Dusty Hunter's Backpack (1113191, level 3). Hue 1127; level x 2000 gold; three pieces of
// magic equipment (five at level 3) with one to five properties; two or three gems, rare gems above level 1, stacked;
// then 20 magical residue, 10 enchanted essence or one relic fragment by level.
//
// Differences from ServUO:
//   - No Ter Mur quest reward book. ServUO drops a TerMurQuestRewardBook (RewardBackpack.cs:17), a type neither pinned
//     nor ours has.
//   - The equipment pool. ServUO calls Loot.RandomArmorOrShieldOrWeaponOrJewelry(false, false, true), its Stygian Abyss
//     pool (Misc/Loot.cs:783-806). Pinned's version has no Stygian argument and no SA armor, shield or jewelry tables, so
//     the pool is built here from pinned's tables in ServUO's order, without ServUO's m_SAArmorTypes, m_SAShieldTypes and
//     m_SAJewelryTypes (Loot.cs:27-33, 157-160, 194-197: gargish armor, shields, rings and bracelets).
//   - Properties: BaseRewardBag.ApplyMods, ServUO's BaseReward.ApplyMods without its RunicReforging branch (see
//     RewardBag.cs).
//   Loot.RandomRareGem and DropItemStacked are ServUO's and live on BaseRewardBag (RewardBag.cs).

using System;
using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class BaseRewardBackpack : Backpack
{
    private static readonly Type[][] _equipment =
    {
        Loot.SAWeaponTypes, Loot.AosWeaponTypes, Loot.WeaponTypes,
        Loot.SARangedWeaponTypes, Loot.AosRangedWeaponTypes, Loot.RangedWeaponTypes,
        Loot.ArmorTypes, Loot.AosHatTypes, Loot.HatTypes,
        Loot.AosShieldTypes, Loot.ShieldTypes, Loot.JewelryTypes
    };

    public BaseRewardBackpack()
    {
        Hue = 1127;

        DropItem(new Gold(Level * 2000));

        // ServUO: DropItem(new TerMurQuestRewardBook()); no such type here.

        var itemDrop = Level switch
        {
            3 => 5,
            _ => 3
        };

        for (var i = 0; i < itemDrop; i++)
        {
            var item = Loot.Construct(_equipment);

            if (item != null)
            {
                BaseRewardBag.ApplyMods(item);
                DropItem(item);
            }
        }

        itemDrop = Utility.RandomMinMax(2, 3);

        for (var i = 0; i < itemDrop; i++)
        {
            if (Level == 1)
            {
                BaseRewardBag.DropItemStacked(this, Loot.RandomGem());
            }
            else
            {
                BaseRewardBag.DropItemStacked(this, BaseRewardBag.RandomRareGem());
            }
        }

        switch (Level)
        {
            case 1:
                DropItem(new MagicalResidue(20));
                break;
            case 2:
                DropItem(new EnchantedEssence(10));
                break;
            case 3:
                DropItem(new RelicFragment());
                break;
        }
    }

    public virtual int Level => 1;
}

[SerializationGenerator(0, false)]
public partial class DustyAdventurersBackpack : BaseRewardBackpack
{
    [Constructible]
    public DustyAdventurersBackpack()
    {
    }

    public override int LabelNumber => 1113189; // Dusty Adventurer's Backpack
}

[SerializationGenerator(0, false)]
public partial class DustyExplorersBackpack : BaseRewardBackpack
{
    [Constructible]
    public DustyExplorersBackpack()
    {
    }

    public override int LabelNumber => 1113190; // Dusty Explorer's Backpack

    public override int Level => 2;
}

[SerializationGenerator(0, false)]
public partial class DustyHuntersBackpack : BaseRewardBackpack
{
    [Constructible]
    public DustyHuntersBackpack()
    {
    }

    public override int LabelNumber => 1113191; // Dusty Hunter's Backpack

    public override int Level => 3;
}
