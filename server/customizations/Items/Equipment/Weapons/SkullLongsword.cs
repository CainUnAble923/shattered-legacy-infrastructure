using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Weapons/SkullLongsword.cs (CC9 batch 5).
    // Dropped: [Alterable] (D-20: pinned ModernUO has no alter-item system, zero consumers of the name).
    [Flippable(41793, 41794)]
    [SerializationGenerator(0, false)]
    public partial class SkullLongsword : Longsword
    {
        [Constructible]
        public SkullLongsword()
        {
            ItemID = 41793;
        }

        public override int LabelNumber => 1125817;
    }

    // ServUO: Items/Equipment/Weapons/SkullLongsword.cs (CC9 batch 5).
    // Dropped: CanBeWornByGargoyles (D-2).
    [Flippable(41797, 41798)]
    [SerializationGenerator(0, false)]
    public partial class GargishSkullLongsword : Longsword
    {
        [Constructible]
        public GargishSkullLongsword()
        {
            ItemID = 41797;
        }

        public override int LabelNumber => 1125821;
        public override int RequiredRaces => Race.AllowGargoylesOnly;
    }
}
