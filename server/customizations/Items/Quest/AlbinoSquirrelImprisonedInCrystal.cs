// ServUO: Items/Quest/AlbinoSquirrelImprisonedInCrystal.cs (cc-P129): the crystal and the AlbinoSquirrel it releases,
// one file as in ServUO. Lady Melisande's rare drop. Pinned has Squirrel and BaseImprisonedMobile; GetProperties
// takes an IPropertyList. Values verbatim; serialization by the generator.

using ModernUO.Serialization;
using Server.Mobiles;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class AlbinoSquirrelImprisonedInCrystal : BaseImprisonedMobile
    {
        [Constructible]
        public AlbinoSquirrelImprisonedInCrystal() : base(0x1F1C)
        {
            Weight = 1.0;
            Hue = 0x482;
        }

        public override int LabelNumber => 1075004; // An Albino Squirrel Imprisoned in a Crystal

        public override BaseCreature Summon => new AlbinoSquirrel();
    }
}

namespace Server.Mobiles
{
    [SerializationGenerator(0, false)]
    public partial class AlbinoSquirrel : Squirrel
    {
        [Constructible]
        public AlbinoSquirrel()
        {
            Hue = 0x482;
        }

        public override bool DeleteOnRelease => true;

        public override void GetProperties(IPropertyList list)
        {
            base.GetProperties(list);

            list.Add(1049646); // (summoned)
        }
    }
}
