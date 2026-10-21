// cc-P199: ServUO pub57 Scripts/Items/Functional/Automaton/BlackrockAutomatonHead.cs. The blackrock variant of the
// rebuilt automaton head (hue 1175, "Rebuilt Blackrock Automaton Head"): activated the same way, it builds a
// BlackrockAutomaton. ServUO pays it as a tinkering bulk order reward at 1200 points (BulkOrders/Rewards/
// Rewards.cs:1045); it is not crafted. Everything else is KotlAutomatonHead's.
//
// One difference, inherited: the head's Resource is saved by KotlAutomatonHead (see that file). The blackrock head's
// hue is set after the base constructor's Resource = Iron, as in ServUO, so it stays 1175 until a GM sets a Resource.

using ModernUO.Serialization;

namespace Server.Items;

[Flippable(0x9DB1, 0x9DB2)]
[SerializationGenerator(0, false)]
public partial class BlackrockAutomatonHead : KotlAutomatonHead
{
    [Constructible]
    public BlackrockAutomatonHead() => Hue = 1175;

    public override int LabelNumber => 1157220; // Blackrock Automaton Head

    public override void AddNameProperty(IPropertyList list)
    {
        list.Add(1157022, "#1157178"); // Rebuilt ~1_MATTYPE~ Automaton Head
    }

    public override KotlAutomaton GetAutomaton(Mobile master) => new BlackrockAutomaton();
}
