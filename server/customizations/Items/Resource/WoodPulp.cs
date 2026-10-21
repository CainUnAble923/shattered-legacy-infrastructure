// cc-P199: ServUO pub57 Scripts/Items/Resource/WoodPulp.cs. Wood pulp (1113136), stackable, on the dough art 0x103D.
// Not a commodity in ServUO either.
//
// No deliberate differences from ServUO.

using ModernUO.Serialization;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class WoodPulp : Item
    {
        [Constructible]
        public WoodPulp(int amount = 1) : base(0x103D)
        {
            Stackable = true;
            Amount = amount;
        }

        public override int LabelNumber => 1113136; // wood pulp
    }
}
