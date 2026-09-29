// ServUO: Items/Quest/FoolishGold.cs (CC9 close, 2026-09-29), the file's one type, extracted under its own name.
// The gold Niporailem throws into an attacker's pack mid-fight (Niporailem.ThrowTreasure), 100 stones of it.
// Dropped anywhere but the pack it turns to sand (0x11EA, 25 stones). It decays after 15 minutes once its boss is
// gone. batch 8 section 9 called it "dropped on death"; it is not, it is thrown during the fight.
//
// What changed: ServUO saves Link at version 1 with no version-0 branch; here it is the generator's version 0.
// [CommandProperty] on the two overrides is dropped, as pinned's own Decays/DecayTime carry none. Link is
// AccessLevel.Decorator in ServUO; pinned has no Decorator (Mobile.cs:135), so it is GameMaster, the next level up.

using System;
using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class NiporailemsTreasure : Item
{
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private Mobile _link;

    public NiporailemsTreasure(Mobile link) : base(0xEEF)
    {
        _link = link;
        Weight = 100.0;
    }

    public override int LabelNumber => ItemID == 0x11EA ? 1112115 : 1112113; // Niporailem's Treasure : Treasure Sand

    public override bool Decays => _link?.Deleted == false ? base.Decays : true;

    public override TimeSpan DecayTime => TimeSpan.FromMinutes(15);

    public override bool DropToWorld(Mobile from, Point3D p)
    {
        var convert = base.DropToWorld(from, p);

        if (convert)
        {
            ConvertItem(from);
        }

        return convert;
    }

    public override bool DropToMobile(Mobile from, Mobile target, Point3D p)
    {
        var convert = base.DropToMobile(from, target, p);

        if (convert)
        {
            ConvertItem(from);
        }

        return convert;
    }

    public override bool DropToItem(Mobile from, Item target, Point3D p)
    {
        var convert = base.DropToItem(from, target, p);

        if (convert && Parent != from.Backpack)
        {
            ConvertItem(from);
        }

        return convert;
    }

    public virtual void ConvertItem(Mobile from)
    {
        from.SendLocalizedMessage(1112112); // To carry the burden of greed!

        ItemID = 0x11EA;
        Weight = 25.0;
    }
}
