// cc-P127: ServUO pub57 Scripts/Services/Monster Stealing/Items/SmugglersLantern.cs -> ours. ServUO's AllowEquipedCast
// override has no pinned counterpart (pinned never stops a cast for an equipped light), so the lantern keeps only its
// Spell Channeling line (notes/cc-P127-port-treasure-maps-monster-stealing.md).

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class SmugglersLantern : Lantern
{
    [Constructible]
    public SmugglersLantern()
    {
        Hue = Utility.RandomMinMax(192, 291);
    }

    public override int LabelNumber => 1071521; // Smuggler's Lantern

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add(1079766); // Spell Channeling
    }
}
