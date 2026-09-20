// ServUO: Items/Quest/ArielHavenWritofMembership.cs (CC6 batch 3). Values verbatim; serialization by the generator.
// The Missing quest's objective item (Quests/Missing.cs:11). Its only source in ServUO is Rotworm.OnKilledBy, a 20%
// drop gated on QuestHelper.HasQuest<Missing>, which needs ServUO's BaseQuest engine (S5); that branch is not ported
// (D-60), so nothing here hands one out yet. Ported so the quest finds its item when it comes.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class ArielHavenWritofMembership : Item
{
    [Constructible]
    public ArielHavenWritofMembership() : base(0x2831)
    {
    }

    public override int LabelNumber => 1094998; // Ariel Haven Writ of Membership
}
