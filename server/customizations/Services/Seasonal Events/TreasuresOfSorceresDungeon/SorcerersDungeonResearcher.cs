// ServUO: Services/Seasonal Events/TreasuresOfSorceresDungeon/SorcerersDungeonResearcher.cs (cc-P408). The
// Researcher, "Artifacts of Enchanted Origin Trader" (1157617), at Ilshenar 536,456,-53 by the Sorcerer's Dungeon
// entrance: a turn-in NPC (Mobiles/NPCs/BaseTurnInMobile.cs, cc-P196's, like Blackthorn's Agent of the Crown) who
// takes artifacts of Enchanted Origin (EnchantedOriginArtifacts.IsEnchantedOrigin; ServUO reads ReforgedSuffix off
// ICombatEquipment) for one point each and opens the reward gump.
//
// His pack holds one of each reward whose properties the reward gump shows (BaseRewardGump reads
// Owner.Backpack.FindItemByType), unmovable, as ServUO packs them; ServUO's three title deeds are not ported (no
// reward-title system on the shard: deviation 2), so they are not packed. The clothes are the shard's SetWearable
// extension (Misc/WearableExtensions.cs). Utility.RandomSkinHue() is Race.Human.RandomSkinHue() in pinned.
//
// Instance: ServUO keeps the one researcher in a static, re-found at load when he stands on Ilshenar; same here.

using ModernUO.Serialization;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;

namespace Server.Engines.SorcerersDungeon;

[SerializationGenerator(0, false)]
public partial class SorcerersDungeonResearcher : BaseTurnInMobile
{
    [Constructible]
    public SorcerersDungeonResearcher() : base("the Researcher")
    {
    }

    public static SorcerersDungeonResearcher Instance { get; set; }

    public override int TitleLocalization => 1157657;  // Artifacts of Enchanted Origin
    public override int CancelLocalization => 1157616; // Bring me items of Enchanted Origin and I will reward you with valuable items.
    public override int TurnInLocalization => 1158763; // Turn In Artifacts of Enchanted Origin
    public override int ClaimLocalization => 1155593;  // Claim Rewards

    public override void InitBody()
    {
        base.InitBody();

        Name = NameList.RandomName("male");

        Hue = Race.Human.RandomSkinHue();
        Body = 0x190;
        HairItemID = 0x2047;
        HairHue = 0x46D;
    }

    public override void InitOutfit()
    {
        this.SetWearable(new FancyShirt(), 1255);
        this.SetWearable(new JinBaori(), 2722);
        this.SetWearable(new Kilt(), 2012);
        this.SetWearable(new LongPants(), 1775);
        this.SetWearable(new ThighBoots(1908));
        this.SetWearable(new Necklace());

        // ServUO packs the three title deeds first (deviation 2: not ported).
        Item item = new SterlingSilverRing();
        item.Movable = false;
        PackItem(item);

        item = new TalonsOfEscaping();
        item.Movable = false;
        PackItem(item);

        item = new BootsOfEscaping();
        item.Movable = false;
        PackItem(item);
    }

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);

        list.Add(1157617); // Artifacts of Enchanted Origin Trader
    }

    public override void AwardPoints(PlayerMobile pm, Item item, int amount)
    {
        EnchantedOriginArtifacts.Remove(item);
        SorcerersDungeonData.Instance.AwardPoints(pm, amount);
    }

    public override bool IsRedeemableItem(Item item) => EnchantedOriginArtifacts.IsEnchantedOrigin(item);

    public override void SendRewardGump(Mobile m)
    {
        if (m is PlayerMobile pm && m.Player && m.CheckAlive())
        {
            m.SendGump(new SorcerersDungeonRewardGump(this, pm));
        }
    }

    [AfterDeserialization]
    private void AfterDeserialization()
    {
        if (Map == Map.Ilshenar)
        {
            Instance = this;
        }
    }

    public override void OnDelete()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        base.OnDelete();
    }
}
