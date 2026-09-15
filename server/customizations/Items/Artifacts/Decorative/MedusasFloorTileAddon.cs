using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Decorative/MedusasFloorTileAddon.cs (CC9 batch 5). A 5x5 floor of 25 localized
    // components (all cliloc 1113922) and its deed. The TypeAlias entries carry ServUO's earlier spellings.
    [TypeAlias("Server.Items.MedusasFloortileAddon")]
    [SerializationGenerator(0, false)]
    public partial class MedusaFloorTileAddon : BaseAddon
    {
        [Constructible]
        public MedusaFloorTileAddon()
        {
            AddComponent(new LocalizedAddonComponent(16589, 1113922), 0, 0, 0);   // 1
            AddComponent(new LocalizedAddonComponent(16591, 1113922), 2, 1, 0);   // 2
            AddComponent(new LocalizedAddonComponent(16577, 1113922), -2, 2, 0);  // 3
            AddComponent(new LocalizedAddonComponent(16578, 1113922), -1, -2, 0); // 4
            AddComponent(new LocalizedAddonComponent(16581, 1113922), 2, -2, 0);  // 5
            AddComponent(new LocalizedAddonComponent(16598, 1113922), -1, 2, 0);  // 6
            AddComponent(new LocalizedAddonComponent(16579, 1113922), 0, -2, 0);  // 7
            AddComponent(new LocalizedAddonComponent(16601, 1113922), -2, -2, 0); // 8
            AddComponent(new LocalizedAddonComponent(16580, 1113922), 1, -2, 0);  // 9
            AddComponent(new LocalizedAddonComponent(16582, 1113922), -2, 0, 0);  // 10
            AddComponent(new LocalizedAddonComponent(16586, 1113922), 2, -1, 0);  // 11
            AddComponent(new LocalizedAddonComponent(16595, 1113922), 1, -1, 0);  // 12
            AddComponent(new LocalizedAddonComponent(16590, 1113922), 1, 1, 0);   // 13
            AddComponent(new LocalizedAddonComponent(16585, 1113922), 1, 0, 0);   // 14
            AddComponent(new LocalizedAddonComponent(16587, 1113922), -2, -1, 0); // 15
            AddComponent(new LocalizedAddonComponent(16593, 1113922), -1, 1, 0);  // 16
            AddComponent(new LocalizedAddonComponent(16594, 1113922), 0, 1, 0);   // 17
            AddComponent(new LocalizedAddonComponent(16596, 1113922), 2, 0, 0);   // 18
            AddComponent(new LocalizedAddonComponent(16588, 1113922), -1, -1, 0); // 19
            AddComponent(new LocalizedAddonComponent(16597, 1113922), 2, 2, 0);   // 20
            AddComponent(new LocalizedAddonComponent(16592, 1113922), -2, 1, 0);  // 21
            AddComponent(new LocalizedAddonComponent(16584, 1113922), 0, -1, 0);  // 22
            AddComponent(new LocalizedAddonComponent(16583, 1113922), -1, 0, 0);  // 23
            AddComponent(new LocalizedAddonComponent(16599, 1113922), 0, 2, 0);   // 24
            AddComponent(new LocalizedAddonComponent(16600, 1113922), 1, 2, 0);   // 25
        }

        public override BaseAddonDeed Deed => new MedusaFloorTileAddonDeed();
    }

    [TypeAlias("Server.Items.MedusasFloortileAddonDeed")]
    [SerializationGenerator(0, false)]
    public partial class MedusaFloorTileAddonDeed : BaseAddonDeed
    {
        [Constructible]
        public MedusaFloorTileAddonDeed()
        {
        }

        public override BaseAddon Addon => new MedusaFloorTileAddon();

        public override int LabelNumber => 1113918; // a Medusa Floor deed
    }
}
