using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/Sets/Phoenix/PhoenixGorget.cs (CC9 batch 4).
    // Not a set piece: ServUO puts the Phoenix suit under Sets/ but it has no SetID, so it stays on its stock base.
    [SerializationGenerator(0, false)]
    public partial class PhoenixGorget : StuddedGorget
    {
        [Constructible]
        public PhoenixGorget() : base()
        {
            Hue = 0x8E;
            LootType = LootType.Blessed;
        }

        public override int LabelNumber => 1041604;
    }
}
