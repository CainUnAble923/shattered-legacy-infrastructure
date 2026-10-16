// ServUO: Items/Addons/ParoxysmusSwampDragonStatuette.cs (cc-P129). Chief Paroxysmus' 5% statuette; pinned's
// BaseImprisonedMobile releases its Summon (Mobiles/Normal/ParoxysmusSwampDragon.cs, ported beside it). Values
// verbatim; serialization by the generator.

using ModernUO.Serialization;
using Server.Mobiles;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class ParoxysmusSwampDragonStatuette : BaseImprisonedMobile
{
    [Constructible]
    public ParoxysmusSwampDragonStatuette() : base(0x2619)
    {
        Weight = 1.0;
        Hue = 0x851;
    }

    public override int LabelNumber => 1072084; // Paroxysmus' Swamp Dragon

    public override BaseCreature Summon => new ParoxysmusSwampDragon();
}
