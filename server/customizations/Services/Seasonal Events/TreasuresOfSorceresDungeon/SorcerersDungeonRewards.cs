// ServUO: Services/Seasonal Events/TreasuresOfSorceresDungeon/SorcerersDungeonRewards.cs and RewardGump.cs (cc-P408).
// The researcher's reward list, in ServUO's order and at ServUO's prices, and its gump: the shard's BaseRewardGump
// (Gumps/BaseRewardGump.cs, cc-P194) titled 1158744, paying with the Sorcerer's Dungeon points.
//
//   Jack's Bag of Tricks                75     Hero of the Unloved (title deed)      20  NOT PORTED
//   Serpent's Jawbone                   50     Savior of the Demented (title deed)   30  NOT PORTED
//   Jack in the Box                     20     Slayer of the Pumpkin King (deed)     50  NOT PORTED
//   Shackled Heart of the Pumpkin King  20     Sterling Silver Ring                  50
//                                              Talons of Escaping                    50
//                                              Boots of Escaping                     50
//
// The three title deeds need a reward-title system (ServUO's PlayerMobile.AddRewardTitle and the title menu), which
// neither pinned nor the shard has: the BOD rewards met the same gap (Services/BulkOrders/BODRewardCollections.cs) and
// left theirs out. Here they are left out of the list rather than shown and refused (deviation 2). SerpentsJawbone is
// the shard's (Items/Functional/SerpentsJawbone.cs). IsTokunoDyable is kept for the pigments of Tokuno when they come.

using System;
using System.Collections.Generic;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;

namespace Server.Engines.SorcerersDungeon;

public static class SorcerersDungeonRewards
{
    public static List<CollectionItem> Rewards { get; private set; }

    public static void Initialize()
    {
        Rewards = new List<CollectionItem>
        {
            new(typeof(JacksBagOfTricks), 0x9F85, 1157656, 0, 75),
            new(typeof(SerpentsJawbone), 0x9F74, 1157654, 0, 50),
            new(typeof(JackInTheBox), 0x9F64, 1157655, 0, 20),
            new(typeof(ShackledHeartOfThePumpkinKing), 0x4A9C, 1157653, 0, 20),
            // ServUO: HeroOfTheUnlovedTitleDeed 20, SaviorOfTheDementedTitleDeed 30, SlayerOfThePumpkinKingTitleDeed 50.
            new(typeof(SterlingSilverRing), 0x1F09, 1155606, 0, 50),
            new(typeof(TalonsOfEscaping), 0x41D8, 1155682, 0, 50),
            new(typeof(BootsOfEscaping), 0x1711, 1155607, 0, 50)
        };
    }

    public static bool IsTokunoDyable(Type t) => Rewards?.Exists(item => item.Type == t) == true;
}

public class SorcerersDungeonRewardGump : BaseRewardGump
{
    public SorcerersDungeonRewardGump(Mobile owner, PlayerMobile user)
        : base(owner, user, SorcerersDungeonRewards.Rewards, 1158744)
    {
    }

    public override double GetPoints(Mobile m) => SorcerersDungeonData.Instance.GetPoints(m);

    public override void RemovePoints(double points) => SorcerersDungeonData.Instance.DeductPoints(User, points);
}
