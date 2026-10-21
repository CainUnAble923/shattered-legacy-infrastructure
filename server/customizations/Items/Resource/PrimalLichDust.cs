// cc-P199: ServUO pub57 Scripts/Items/Resource/PrimalLichDust.cs. Primeval lich dust (1031701), stackable. Not a
// commodity in ServUO either.
//
// No deliberate differences from ServUO.

using ModernUO.Serialization;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class PrimalLichDust : Item
    {
        [Constructible]
        public PrimalLichDust(int amount = 1) : base(0x2DB5)
        {
            Stackable = true;
            Amount = amount;
        }

        public override int LabelNumber => 1031701; // Primeval Lich Dust
    }
}
