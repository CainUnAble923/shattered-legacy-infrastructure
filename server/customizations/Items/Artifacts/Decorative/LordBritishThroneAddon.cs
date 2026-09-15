using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Decorative/LordBritishThroneAddon.cs (CC9 batch 5). Two-component addon and its
    // blessed deed. ServUO saves the addon at version 1 with no fix-up; version 0 here.
    [SerializationGenerator(0, false)]
    public partial class LordBritishThroneAddon : BaseAddon
    {
        [Constructible]
        public LordBritishThroneAddon()
        {
            AddComponent(new AddonComponent(0x1526), 0, 0, 0);
            AddComponent(new AddonComponent(0x1527), 0, -1, 0);
        }

        public override BaseAddonDeed Deed => new LordBritishThroneDeed();
    }

    [SerializationGenerator(0, false)]
    public partial class LordBritishThroneDeed : BaseAddonDeed
    {
        [Constructible]
        public LordBritishThroneDeed() => LootType = LootType.Blessed;

        public override BaseAddon Addon => new LordBritishThroneAddon();

        public override int LabelNumber => 1073243; // Replica of Lord British's Throne - Museum of Vesper
    }
}
