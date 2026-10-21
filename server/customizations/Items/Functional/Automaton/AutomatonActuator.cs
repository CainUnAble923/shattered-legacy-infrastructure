// cc-P199: ServUO pub57 Scripts/Items/Functional/Automaton/AutomatonActuator.cs. A Time of Legends crafting part
// (art 0x9CE9, "Automaton Actuator"): one of the three ingredients of ServUO's tinkering recipe for a KotlAutomatonHead
// (DefTinkering.cs:651-656), and a tinkering bulk order reward at 750 points (BulkOrders/Rewards/Rewards.cs:1037).
// Nothing else; no saved state.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class AutomatonActuator : Item
{
    [Constructible]
    public AutomatonActuator() : base(0x9CE9)
    {
    }

    public override int LabelNumber => 1156997; // Automaton Actuator
}
