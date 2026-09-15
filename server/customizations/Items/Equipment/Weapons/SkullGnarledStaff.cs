using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Weapons/SkullGnarledStaff.cs (CC9 batch 5).
    // Dropped: [Alterable] (D-20: pinned ModernUO has no alter-item system, zero consumers of the name).
    [Flippable(41795, 41796)]
    [SerializationGenerator(0, false)]
    public partial class SkullGnarledStaff : GnarledStaff
    {
        [Constructible]
        public SkullGnarledStaff()
        {
            ItemID = 41795;
        }

        public override int LabelNumber => 1125819;
    }

    // ServUO: Items/Equipment/Weapons/SkullGnarledStaff.cs (CC9 batch 5).
    // Dropped: CanBeWornByGargoyles (D-2).
    [Flippable(41799, 41800)]
    [SerializationGenerator(0, false)]
    public partial class GargishSkullGnarledStaff : GnarledStaff
    {
        [Constructible]
        public GargishSkullGnarledStaff()
        {
            ItemID = 41799;
        }

        public override int LabelNumber => 1125823;
        public override int RequiredRaces => Race.AllowGargoylesOnly;
    }
}
