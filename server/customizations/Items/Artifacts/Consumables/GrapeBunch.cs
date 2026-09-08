using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Consumables/GrapeBunch.cs (CC9). ServUO's Food(amount, itemID)
    // becomes ModernUO's Food(itemID, amount). IsArtifact dropped (CC9 D-1).
    [SerializationGenerator(0, false)]
    public partial class GrapeBunch : Food
    {
        [Constructible]
        public GrapeBunch() : base(3354)
        {
            FillFactor = 1;
            Stackable = false;
        }

        public override double DefaultWeight => 1.0;

        public override int LabelNumber => 1022513;
    }
}
