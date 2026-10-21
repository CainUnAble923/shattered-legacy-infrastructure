// cc-P199: ServUO pub57 Scripts/Items/Resource/PolishedMeteorite.cs. Polished meteorite (1158693), weight 3, on one of
// the twelve meteorite arts 41422-41433 (0xA1CE-0xA1D9) picked at random. Not stackable, no behavior.
//
// One shape difference: ServUO sets Weight = 3 in the constructor (PolishedMeteorite.cs:13); this overrides
// DefaultWeight => 3.0, pinned's idiom, so the weight is not a saved field. Same weight.

using ModernUO.Serialization;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class PolishedMeteorite : Item
    {
        [Constructible]
        public PolishedMeteorite() : base(41422 + Utility.Random(12))
        {
        }

        public override int LabelNumber => 1158693; // polished meteorite
        public override double DefaultWeight => 3.0;
    }
}
