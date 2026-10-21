// cc-P199: ServUO pub57 Scripts/Quests/Eodon/MyrmidexThreat/Mobiles/Questers.cs:205-268, the Collector of Oddities, who
// sells the three trap training kits (99,375 gold each). ServUO spawns him at Trammel 4305,1003 (Moonglow) from the
// Myrmidex Threat generator (Generate.cs:207-211); here [ClusterFPlaceCollectorOfOddities puts him there, dry run first.
//
// Left out of his shop: the Stasis Chamber Power Core (QuestItems.cs:224), a Myrmidex Threat quest item; that quest is
// not ported. ServUO's SetWearable is pinned's AddItem with the hue set; his outfit replaces the vendor's random one.

using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles
{
    [SerializationGenerator(0, false)]
    public partial class CollectorOfOddities : BaseVendor
    {
        private readonly List<SBInfo> _sbInfos = new();

        [Constructible]
        public CollectorOfOddities() : base("the collector of oddities")
        {
        }

        protected override List<SBInfo> SBInfos => _sbInfos;

        public override void InitSBInfo()
        {
            _sbInfos.Add(new SBCollectorOfOddities());
        }

        public override void InitOutfit()
        {
            AddItem(new FancyShirt(1156));
            AddItem(new Doublet(1316));
            AddItem(new ElvenPants(1151));
            AddItem(new Cloak(1151));
            AddItem(new ElvenBoots(2007));
        }
    }

    public class SBCollectorOfOddities : SBInfo
    {
        public override IShopSellInfo SellInfo { get; } = new InternalSellInfo();

        public override List<GenericBuyInfo> BuyInfo { get; } = new InternalBuyInfo();

        public class InternalBuyInfo : List<GenericBuyInfo>
        {
            public InternalBuyInfo()
            {
                Add(new GenericBuyInfo("1159014", typeof(CircuitTrapTrainingKit), 99375, 500, 41875, 0));
                Add(new GenericBuyInfo("1159015", typeof(CylinderTrapTrainingKit), 99375, 500, 41875, 0));
                Add(new GenericBuyInfo("1159016", typeof(SliderTrapTrainingKit), 99375, 500, 41875, 0));
            }
        }

        public class InternalSellInfo : GenericSellInfo
        {
        }
    }
}
