// cc-P193: ServUO pub57 Scripts/Services/Expansions/High Seas/Items/Artifacts/EnchantedVortex.cs -> ours, the High Seas
// fishing port. A decorative artifact of both sea champions: a death-vortex addon (14284) and its deed.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class EnchantedVortexAddon : BaseAddon
{
    [Constructible]
    public EnchantedVortexAddon()
    {
        var comp = new AddonComponent(14284) { Name = "an enchanted vortex" };
        AddComponent(comp, 0, 0, 0);
    }

    public override BaseAddonDeed Deed => new EnchantedVortexDeed();
}

[SerializationGenerator(0, false)]
public partial class EnchantedVortexDeed : BaseAddonDeed
{
    [Constructible]
    public EnchantedVortexDeed() => Name = "A deed for an enchanted vortex";

    public override BaseAddon Addon => new EnchantedVortexAddon();
}
