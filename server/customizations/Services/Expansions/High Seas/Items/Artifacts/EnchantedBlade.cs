// cc-P193: ServUO pub57 Scripts/Services/Expansions/High Seas/Items/Artifacts/EnchantedBlade.cs -> ours, the High Seas
// fishing port. A decorative artifact of both sea champions: a field-of-blades addon (14240) and its deed.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class EnchantedBladeAddon : BaseAddon
{
    [Constructible]
    public EnchantedBladeAddon()
    {
        AddComponent(new LocalizedAddonComponent(14240, 1034240), 0, 0, 0);
    }

    public override BaseAddonDeed Deed => new EnchantedBladeDeed();
}

[SerializationGenerator(0, false)]
public partial class EnchantedBladeDeed : BaseAddonDeed
{
    [Constructible]
    public EnchantedBladeDeed() => Name = "A deed for an enchanted blade";

    public override BaseAddon Addon => new EnchantedBladeAddon();
}
