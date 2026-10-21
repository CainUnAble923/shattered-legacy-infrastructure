// cc-P199: ServUO pub57 Scripts/Items/Functional/Automaton/BlackrockMoonstone.cs. A Time of Legends crafting part
// (art 0x9CAA, hue 1175, "Black Moonstone"): ServUO uses it as an ingredient of the KotlBlackRod (DefCarpentry.cs:471)
// and DrSpectorsLenses (DefTinkering.cs:768) recipes, and pays it as a tinkering bulk order reward at 1000 points
// (BulkOrders/Rewards/Rewards.cs:1042). Nothing else; no saved state.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class BlackrockMoonstone : Item
{
    [Constructible]
    public BlackrockMoonstone() : base(0x9CAA) => Hue = 1175;

    public override int LabelNumber => 1156993; // Black Moonstone
}
