// ServUO: Services/Revamped Dungeons/TheExodusEncounter/Resources/NexusCore.cs (cc-P195): the Alchemy product (90
// skill) the Summoning Altar deed takes one of. Not stackable (ServUO's version 1 made it so).

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class NexusCore : Item
{
    [Constructible]
    public NexusCore() : base(0x4B82)
    {
        Stackable = false;
    }

    public override int LabelNumber => 1153501; // nexus core
}
