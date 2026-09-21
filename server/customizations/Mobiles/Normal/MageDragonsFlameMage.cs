// ServUO: Mobiles/Normal/DragonsFlameGrandMage.cs (CC6 batch 7). Values verbatim; serialization by the generator. The
// Citadel's Black Order Grand Mage, a subclass of our DragonsFlameMage (batch 2), so batch 4 §2's rule applies: this
// level carries its own generator attribute, its own (Serial) constructor and its own version slot after the parent's.
//
// The type is named by the spawn-data spelling: shared/malas/Citadel.json asks for "magedragonsflamemage" at four
// spawners ([134,1947] x2, [185,1920] x2, [140,1974] x2, [139,1868] x7) where ServUO's Spawns/malas.xml names
// DragonsFlameGrandMage with the same counts. Neither spelling is OSI's (the player sees "Black Order Grand Mage");
// the spawn data is the consumer and upstream cannot correct an entry to a type it lacks, so the type takes the
// spelling that has to resolve and no alias is needed on a type we author: Q-056, the naming rule. ServUO's name is
// here for grep.
//
// ServUO's Title, AlwaysMurderer and ShowFameTitle repeat the parent's; kept as written. Its DragonFlameKey drop is
// ported with the key (Items/Quest/DragonFlameKey.cs): on ServUO the Citadel altar consumes it, and no altar is here
// (P9's shape, batch 4's SerpentFangKey), so it is a one-week blessed drop from every death.

using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class MageDragonsFlameMage : DragonsFlameMage
{
    [Constructible]
    public MageDragonsFlameMage()
    {
        Title = "of the Dragon's Flame Sect";
        SetStr(340, 360);
        SetDex(200, 215);
        SetInt(500, 515);

        SetHits(800);

        SetDamage(15, 20);

        Fame = 25000;
        Karma = -25000;

        VirtualArmor = 60;
    }

    public override string CorpseName => "a black order grand mage corpse";
    public override string DefaultName => "Black Order Grand Mage";

    public override bool AlwaysMurderer => true;
    public override bool ShowFameTitle => false;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.AosFilthyRich, 6);
    }

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);

        c.DropItem(new DragonFlameKey());

        if (Utility.RandomDouble() < 0.5)
        {
            c.DropItem(new DragonFlameSectBadge());
        }
    }
}
