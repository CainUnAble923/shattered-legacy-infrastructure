// cc-P199: ServUO pub57 Scripts/Items/Functional/Automaton/InoperativeAutomatonHead.cs. A Time of Legends crafting part
// (art 0x9DB1/0x9DB2, "Inoperative Automaton Head"): one of the three ingredients of ServUO's tinkering recipe for a
// KotlAutomatonHead (DefTinkering.cs:655). ServUO drops it from the Kotl City automatons' treasure (Seasonal Events/
// TreasuresOfKotlCity). Nothing else; no saved state.

using ModernUO.Serialization;

namespace Server.Items;

[Flippable(0x9DB1, 0x9DB2)]
[SerializationGenerator(0, false)]
public partial class InoperativeAutomatonHead : Item
{
    [Constructible]
    public InoperativeAutomatonHead() : base(0x9DB1)
    {
    }

    public override int LabelNumber => 1157002; // Inoperative Automaton Head
}
