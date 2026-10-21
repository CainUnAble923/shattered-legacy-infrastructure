// ServUO: Items/Addons/NexusAddon.cs (cc-P195): the Nexus machine's component, addon and deed, the base the Exodus
// Encounter's ExodusNexus (the circuit puzzle) builds on and the deed the Keypunch Reader prints. The thirteen
// components are ServUO's, including its doubled piece 19319 at (1, 2); the deed's name is ServUO's string.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class NexusComponent : AddonComponent
{
    public NexusComponent(int itemID) : base(itemID)
    {
    }

    public override int LabelNumber => 1152442; // Nexus
}

[SerializationGenerator(0, false)]
public partial class NexusAddon : BaseAddon
{
    [Constructible]
    public NexusAddon()
    {
        AddComponent(new NexusComponent(19324), -1, 1, 0);
        AddComponent(new NexusComponent(19326), 0, 0, 0);
        AddComponent(new NexusComponent(19319), 1, 2, 0);
        AddComponent(new NexusComponent(19321), 2, 1, 0);
        AddComponent(new NexusComponent(19319), 1, 2, 0);
        AddComponent(new NexusComponent(19323), 0, 1, 0);
        AddComponent(new NexusComponent(19327), 1, -1, 0);
        AddComponent(new NexusComponent(19316), 1, 1, 0);
        AddComponent(new NexusComponent(19322), 0, -1, 0);
        AddComponent(new NexusComponent(19320), 2, 2, 0);
        AddComponent(new NexusComponent(19325), 1, 0, 0);
        AddComponent(new NexusComponent(19318), 0, 2, 0);
        AddComponent(new NexusComponent(19317), -1, 2, 0);
    }

    public override BaseAddonDeed Deed => new NexusAddonDeed();
}

[SerializationGenerator(0, false)]
public partial class NexusAddonDeed : BaseAddonDeed
{
    [Constructible]
    public NexusAddonDeed()
    {
    }

    public override string DefaultName => "Nexus Deed";

    public override BaseAddon Addon => new NexusAddon();
}
