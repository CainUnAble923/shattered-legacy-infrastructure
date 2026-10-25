// cc-P269: ServUO pub57 Scripts/Services/Expansions/High Seas/Quests/ProfessionFishQuest/FishMonger.cs -> ours, High
// Seas, second half. The fish monger at a port's dock, who gives and takes the profession fish orders. Double-click him
// with your boat near (within 100 tiles): he offers an order for fish to deliver to another port, shows an order you
// took from him, or takes delivery of an order bound for his port (ProfessionalFisherQuest.OnTalk). With no boat he
// says to bring one around. [GenFishMongers places them where ServUO's [DecorateHS does.
//
// Changes from ServUO (shard-migration notes/cc-P269-high-seas-fishing-second-half.md):
// - ServUO's FishMonger is a MondainQuester (its Mondain's Legacy quest framework, not in pinned or ours); here he is a
//   blessed townsperson who answers a double-click within three tiles, as a quester does.
// - He carries his port's name (PortName, set by [GenFishMongers), which names the order and its delivery; ServUO read
//   the region's name, and the Sea Market has no region here.

using ModernUO.Serialization;
using Server.Engines.Quests;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class FishMonger : BaseCreature
{
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    [SerializableField(0)]
    private string _port;

    [Constructible]
    public FishMonger() : base(AIType.AI_Vendor, FightMode.None, 2)
    {
        InitStats(100, 100, 25);

        Female = false;
        Race = Race.Human;
        Body = 0x190;

        Name = NameList.RandomName("male");
        Title = "the fish monger";

        Hue = Race.RandomSkinHue();
        HairItemID = Race.RandomHair(this);
        HairHue = Race.RandomHairHue();

        AddItem(new ShortPants());
        AddItem(new Shirt());
        AddItem(new Sandals());

        Blessed = true;
        FishQuestHelper.AddMonger(this);
    }

    public override bool ClickTitle => true;
    public override bool CanTeach => false;

    /// <summary>The port he stands in: his Port, else his region's name.</summary>
    public string PortName => _port ?? Region?.Name;

    public override void OnDoubleClick(Mobile from)
    {
        if (from is PlayerMobile pm && from.InRange(Location, 3) && from.InLOS(this))
        {
            ProfessionalFisherQuest.OnTalk(this, pm);
        }
        else
        {
            base.OnDoubleClick(from);
        }
    }

    public override void OnAfterDelete()
    {
        FishQuestHelper.RemoveMonger(this);
        base.OnAfterDelete();
    }

    [AfterDeserialization]
    private void AfterDeserialization()
    {
        FishQuestHelper.AddMonger(this);
    }
}
