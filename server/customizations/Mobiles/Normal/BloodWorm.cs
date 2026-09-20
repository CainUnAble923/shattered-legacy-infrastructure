// ServUO: Mobiles/Normal/BloodWorm.cs (CC6 batch 3). Values verbatim; serialization by the generator. ServUO sets no
// Fame or Karma; neither does this. The IBloodCreature marker ServUO declares in this file is Mobiles/IBloodCreature.cs
// here. OnAfterMove, the corpse drain, is ported as written: a quarter of the time while hurt it looks one tile
// around for a human corpse (ItemID 0x2006), turns it into a bone pile and heals to full.
//
// Dropped: SetSpecialAbility(SpecialAbility.Anemia) (D-63: Pet Training, declined B3, the Saurosaurus precedent).

using ModernUO.Serialization;
using Server.Items;
using Server.Network;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class BloodWorm : BaseCreature, IBloodCreature
{
    [Constructible]
    public BloodWorm() : base(AIType.AI_Melee)
    {
        Body = 287;

        SetStr(401, 473);
        SetDex(80);
        SetInt(18, 19);

        SetHits(374, 422);

        SetDamage(11, 17);

        SetDamageType(ResistanceType.Physical, 60);
        SetDamageType(ResistanceType.Poison, 40);

        SetResistance(ResistanceType.Physical, 52, 55);
        SetResistance(ResistanceType.Fire, 42, 50);
        SetResistance(ResistanceType.Cold, 29, 31);
        SetResistance(ResistanceType.Poison, 69, 75);
        SetResistance(ResistanceType.Energy, 26, 27);

        SetSkill(SkillName.MagicResist, 35.0);
        SetSkill(SkillName.Tactics, 100.0);
        SetSkill(SkillName.Wrestling, 100.0);
    }

    public override string CorpseName => "a bloodworm corpse";
    public override string DefaultName => "a bloodworm";

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Rich);
        AddLoot(LootPack.Average);
    }

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);

        if (Utility.RandomDouble() < 0.02)
        {
            c.DropItem(new LuckyCoin());
        }
    }

    public override int GetIdleSound() => 1503;
    public override int GetAngerSound() => 1500;
    public override int GetHurtSound() => 1502;
    public override int GetDeathSound() => 1501;

    public override void OnAfterMove(Point3D oldLocation)
    {
        base.OnAfterMove(oldLocation);

        if (Hits >= HitsMax || Map == null || Utility.RandomDouble() >= 0.25)
        {
            return;
        }

        Corpse toAbsorb = null;

        foreach (var c in Map.GetItemsInRange<Corpse>(Location, 1))
        {
            if (c.ItemID == 0x2006)
            {
                toAbsorb = c;
                break;
            }
        }

        if (toAbsorb == null)
        {
            return;
        }

        toAbsorb.ProcessDelta();
        toAbsorb.SendRemovePacket();
        toAbsorb.ItemID = Utility.Random(0xECA, 9); // bone graphic
        toAbsorb.Hue = 0;
        toAbsorb.Direction = Direction.North;
        toAbsorb.ProcessDelta();

        Hits = HitsMax;

        // * The creature drains blood from a nearby corpse to heal itself. *
        PublicOverheadMessage(MessageType.Regular, 0x3B2, 1111699);
    }
}
