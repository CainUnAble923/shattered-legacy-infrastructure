// cc-P33 (F-3): ServUO pub57 Services/CleanUpBritannia/Creatures/TheCleanupOfficer.cs. Double-click within 5 tiles
// opens the Clean Up Britannia store (CleanUpBritanniaRewards.cs). Pinned has no such type. ServUO's SetWearable
// becomes pinned's AddItem with the same hues. Placed by [ClusterFPlaceCleanUp, never at world load.

using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;

namespace Server.Engines.CleanUpBritannia;

[SerializationGenerator(0, false)]
public partial class TheCleanupOfficer : BaseVendor
{
    [Constructible]
    public TheCleanupOfficer() : base("the Cleanup Officer")
    {
    }

    public override bool IsActiveVendor => false;
    public override bool DisallowAllMoves => true;
    public override bool ClickTitle => true;
    public override bool CanTeach => false;

    protected override List<SBInfo> SBInfos { get; } = new();

    public override void InitSBInfo()
    {
    }

    public override void InitBody()
    {
        base.InitBody();

        Name = NameList.RandomName("male");

        Hue = Race.Human.RandomSkinHue();
        Body = 0x190;
        Female = false;
        HairItemID = 0x2044;
        HairHue = 1644;
        FacialHairItemID = 0x203F;
        FacialHairHue = 1644;
    }

    public override void InitOutfit()
    {
        AddItem(new Cloak(337));
        AddItem(new ThighBoots());
        AddItem(new LongPants(1409));
        AddItem(new Doublet(50));
        AddItem(new FancyShirt(1644));
        AddItem(new Necklace());

        if (Backpack == null)
        {
            AddItem(new Backpack { Movable = false });
        }
    }

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);

        list.Add(1151317); // Clean Up Britannia Reward Trader
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (from is PlayerMobile pm && from.InRange(Location, 5))
        {
            pm.SendGump(new CleanUpBritanniaRewardGump(this, pm));
        }
    }
}
