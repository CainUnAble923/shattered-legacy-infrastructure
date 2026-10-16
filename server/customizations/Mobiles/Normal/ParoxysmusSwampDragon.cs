// ServUO: Mobiles/Normal/ParoxysmusSwampDragon.cs (cc-P129). The statuette's summon: a barded stock SwampDragon that
// is deleted on release. Pinned SwampDragon's barding fields are its generated BardingResource, BardingExceptional,
// BardingHP and HasBarding properties (SwampDragon.cs:56-100), the same members ServUO sets. GetProperties takes
// pinned's IPropertyList. Values verbatim; serialization by the generator.

using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class ParoxysmusSwampDragon : SwampDragon
{
    [Constructible]
    public ParoxysmusSwampDragon()
    {
        BardingResource = CraftResource.Iron;
        BardingExceptional = true;
        BardingHP = BardingMaxHP;
        HasBarding = true;
        Hue = 1155;
    }

    public override string DefaultName => "Chief Paroxysmus' Swamp Dragon";

    public override bool DeleteOnRelease => true;

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);

        list.Add(1049646); // (summoned)
    }
}
