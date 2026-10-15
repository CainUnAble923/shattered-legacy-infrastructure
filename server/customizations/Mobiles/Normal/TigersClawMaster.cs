// ServUO: Mobiles/Normal/TigersClawMaster.cs (cc-P119). Values verbatim; serialization by the generator; range and speed constructor arguments
// dropped per the port recipe (both fidelity traps).
//
// A subclass of our TigersClawThief (CC6 batch 2), batch 4 section 2's rule: own attribute, own (Serial) constructor, own
// version slot. ServUO's Title "of the Serpent's Fang Sect" on a Tiger's Claw master is ServUO's own and kept as written
// (the bug-list section 3 shape). The key is ported with it (Items/Quest/TigerClawKey.cs). Pinned's Citadel spawn data
// names no master; ServUO's malas.xml has two spawners for it.

using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class TigersClawMaster : TigersClawThief
{
    [Constructible]
    public TigersClawMaster()
    {
        Title = "of the Serpent's Fang Sect";
        SetStr(440, 460);
        SetDex(400, 415);
        SetInt(200, 215);

        SetHits(850, 875);

        SetDamage(15, 20);

        Fame = 25000;
        Karma = -25000;

        VirtualArmor = 60;
    }

    public override string CorpseName => "a black order master corpse";
    public override string DefaultName => "Black Order Master";

    public override bool AlwaysMurderer => true;
    public override bool ShowFameTitle => false;
    
    public override void GenerateLoot()
    {
        AddLoot(LootPack.AosFilthyRich, 6);
    }

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);

        c.DropItem(new TigerClawKey());

        if (Utility.RandomDouble() < 0.5)
        {
            c.DropItem(new TigerClawSectBadge());
        }
    }
}
