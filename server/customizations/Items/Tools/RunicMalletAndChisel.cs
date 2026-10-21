// cc-P199: ServUO pub57 Scripts/Items/Tools/RunicMalletAndChisel.cs. The runic mallet and chisel: a masonry runic tool
// (DefMasonry) hued by its metal, labelled 1111796-1111803 for Dull Copper through Valorite (1111795 + the resource
// index 1-8) and 1045128 otherwise, art 4787 (0x12B3), weight 2. A BOD reward in ServUO
// (Services/BulkOrders/Rewards/Rewards.cs); BODRewardCollections.cs here has Placeholder rows for it.
//
// Built on pinned's BaseRunicTool exactly as pinned's RunicDovetailSaw is (Items/Skill Items/Tools/RunicDovetailSaw.cs):
// the same two constructors, hue from the resource, CraftSystem override. As a plain mallet it opens the masonry menu
// and spends a use per craft. Its runic properties only ever land on BaseArmor or BaseWeapon (pinned BaseArmor.cs:611,
// BaseWeapon.cs:666-688), and in ServUO the masonry pieces that are armor are the gargish stone armor
// (ServUO Services/Craft/DefMasonry.cs:197-206). Neither pinned's DefMasonry nor our craft registrations add those, so
// on this shard today the mallet's runic resource changes nothing crafted. Reported for the lead.
//
// One shape difference: ServUO sets Weight = 2.0 in both constructors (RunicMalletAndChisel.cs:27,34); this overrides
// DefaultWeight => 2.0, as pinned's RunicDovetailSaw does. Same weight.

using ModernUO.Serialization;
using Server.Engines.Craft;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class RunicMalletAndChisel : BaseRunicTool
    {
        [Constructible]
        public RunicMalletAndChisel(CraftResource resource) : base(resource, 4787) => Hue = CraftResources.GetHue(resource);

        [Constructible]
        public RunicMalletAndChisel(CraftResource resource, int uses) : base(resource, uses, 4787) =>
            Hue = CraftResources.GetHue(resource);

        public override double DefaultWeight => 2.0;

        public override CraftSystem CraftSystem => DefMasonry.CraftSystem;

        public override int LabelNumber
        {
            get
            {
                var index = CraftResources.GetIndex(Resource);

                if (index >= 1 && index <= 8)
                {
                    return 1111795 + index;
                }

                return 1045128; // mallet and chisel
            }
        }
    }
}
