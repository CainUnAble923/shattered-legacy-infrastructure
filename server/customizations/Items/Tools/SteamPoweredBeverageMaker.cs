// cc-P199: ServUO pub57 Scripts/Items/Tools/SteamPoweredBeverageMaker.cs. The steam powered beverage maker (1123598,
// art 0x9A96, tiledata "Machine Coffee"), blessed. A tool only by folder: the file has no behavior, and nothing else in
// ServUO names the type (no craft, vendor, reward or loot line), so in ServUO only staff make one. Same values.
//
// No deliberate differences from ServUO.

using ModernUO.Serialization;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class SteamPoweredBeverageMaker : Item
    {
        [Constructible]
        public SteamPoweredBeverageMaker() : base(0x9A96) => LootType = LootType.Blessed;

        public override int LabelNumber => 1123598; // Steam Powered Beverage Maker
    }
}
