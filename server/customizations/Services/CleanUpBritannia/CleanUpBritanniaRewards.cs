// cc-P33 (F-3): the one Clean Up Britannia store. Both the Cleanup Officer (TheCleanupOfficer.cs) and the Sanitation
// Warden (SanitationWardenGump.cs) open it.
//
// OSI's section: ServUO pub57 Services/CleanUpBritannia/CleanUpBritanniaRewards.cs, its 134 rewards at OSI's prices,
// generated from that file line by line. 49 are items pinned or ours has (ScrollOfAlacrity is pinned's
// ScrollofAlacrity); they are below with ServUO's line number. 85 are not in either tree: they are left out of the
// store, kept as MISSING comments, and listed in shard-migration notes/cc-P33-clean-up-britannia.md for Chase (the
// D32/D33 reachability question). Core.HS and Core.SA, which gate ServUO's last 39, are both true on this shard.
//
// Ours, in their own section (registered): the Custodian supplies at the prices Chase approved 2026-09-30.
// The only change to the store itself: each section is listed by price (stable, so equal prices keep OSI's order;
// OSI's list is already in rising order).
//
// Buying is ServUO's BaseRewardGump.OnConfirmed (Gumps/BaseRewardGump.cs:172-193) and the Clean Up gump's
// OnItemCreated (Services/CleanUpBritannia/Gumps.cs:36-42, a random skill on a Scroll of Alacrity).

using System;
using System.Collections.Generic;
using System.Linq;
using Server.Engines.Points;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Server.Engines.CleanUpBritannia;

public sealed class CleanUpReward
{
    public CleanUpReward(Type type, int itemID, int tooltip, int hue, double points)
    {
        Type = type;
        ItemID = itemID;
        Tooltip = tooltip;
        Hue = hue;
        Points = points;
    }

    // Ours: a label and a stack or several copies in place of a cliloc.
    public CleanUpReward(Type type, int itemID, string label, double points, int amount, bool stack)
        : this(type, itemID, 0, 0, points)
    {
        Label = label;
        Amount = amount;
        Stack = stack;
        Ours = true;
    }

    public Type Type { get; }
    public int ItemID { get; }
    public int Tooltip { get; }
    public int Hue { get; }
    public double Points { get; }
    public string Label { get; }
    public int Amount { get; } = 1;
    public bool Stack { get; }
    public bool Ours { get; }

    public List<Item> Create()
    {
        var items = new List<Item>();

        if (Stack)
        {
            var item = Type.CreateInstance<Item>();
            item.Amount = Amount;
            items.Add(item);
        }
        else
        {
            for (var i = 0; i < Amount; i++)
            {
                items.Add(Type.CreateInstance<Item>());
            }
        }

        return items;
    }
}

public static class CleanUpBritanniaRewards
{
    public static readonly CleanUpReward[] OsiRewards =
    [
        // MISSING :17 Mailbox 0x4142 1113927 0 1000
        // MISSING :18 HumansAndElvesRobe 0x1F03 1151202 0 1000
        // MISSING :19 GargoylesAreOurFriendsRobe 0x1F03 1151203 0 1000
        // MISSING :20 WeArePiratesRobe 0x1F03 1151204 0 1000
        // MISSING :21 FollowerOfBaneRobe 0x1F03 1151205 0 1000
        // MISSING :22 QueenDawnForeverRobe 0x1F03 1151206 0 1000
        // MISSING :24 LillyPad 0xDBC 1023516 0 5000
        // MISSING :25 LillyPads 0xDBE 1023518 0 5000
        // MISSING :26 Mushrooms1 0x0D0F 1023340 0 5000
        // MISSING :27 Mushrooms2 0x0D12 1023340 0 5000
        // MISSING :28 Mushrooms3 0x0D10 1023340 0 5000
        // MISSING :29 Mushrooms4 0x0D13 1023340 0 5000
        // MISSING :30 NocturneEarrings 0x1F07 1151243 0x3E5 5000
        // MISSING :32 SherryTheMouseStatue 0x20D0 1080171 0 10000
        // MISSING :33 RefinementAmalgamator 0x9966 1154340 0x480 10000
        // MISSING :34 ChaosTileDeed 0x14EF 1080490 0 10000
        // MISSING :35 HonestyVirtueTileDeed 0x14EF 1080488 0 10000
        // MISSING :36 CompassionVirtueTileDeed 0x14EF 1080481 0 10000
        // MISSING :37 ValorVirtueTileDeed 0x14EF 1080486 0 10000
        // MISSING :38 SpiritualityVirtueTileDeed 0x14EF 1080484 0 10000
        // MISSING :39 HonorVirtueTileDeed 0x14EF 1080485 0 10000
        // MISSING :40 HumilityVirtueTileDeed 0x14EF 1080483 0 10000
        // MISSING :41 SacrificeVirtueTileDeed 0x14EF 1080482 0 10000
        // MISSING :42 JusticeVirtueTileDeed 0x14EF 1080487 0 10000
        // MISSING :43 StewardDeed 0x14F0 1153344 0 10000
        new(typeof(global::Server.Items.KnightsBascinet), 0x140C, 1151247, 1150, 10000), // :45
        new(typeof(global::Server.Items.KnightsCloseHelm), 0x1408, 1151244, 1150, 10000), // :46
        new(typeof(global::Server.Items.KnightsFemalePlateChest), 0x1C04, 1151253, 1150, 10000), // :47
        new(typeof(global::Server.Items.KnightsNorseHelm), 0x140E, 1151245, 1150, 10000), // :48
        new(typeof(global::Server.Items.KnightsPlateArms), 0x1410, 1151250, 1150, 10000), // :49
        new(typeof(global::Server.Items.KnightsPlateChest), 0x1415, 1151252, 1150, 10000), // :50
        new(typeof(global::Server.Items.KnightsPlateGloves), 0x1414, 1151249, 1150, 10000), // :51
        new(typeof(global::Server.Items.KnightsPlateGorget), 0x1413, 1151248, 1150, 10000), // :52
        new(typeof(global::Server.Items.KnightsPlateHelm), 0x1412, 1151246, 1150, 10000), // :53
        new(typeof(global::Server.Items.KnightsPlateLegs), 0x1411, 1151251, 1150, 10000), // :54
        new(typeof(global::Server.Items.ScoutArms), 0x13DC, 1151257, 1148, 10000), // :56
        new(typeof(global::Server.Items.ScoutBustier), 0x1C0C, 1151262, 1148, 10000), // :57
        new(typeof(global::Server.Items.ScoutChest), 0x13DB, 1151258, 1148, 10000), // :58
        new(typeof(global::Server.Items.ScoutCirclet), 0x2B6E, 1151254, 1148, 10000), // :59
        new(typeof(global::Server.Items.ScoutFemaleChest), 0x1C02, 1151261, 1148, 10000), // :60
        new(typeof(global::Server.Items.ScoutGloves), 0x13D5, 1151259, 1148, 10000), // :61
        new(typeof(global::Server.Items.ScoutGorget), 0x13D6, 1151256, 1148, 10000), // :62
        new(typeof(global::Server.Items.ScoutLegs), 0x13DA, 1151260, 1148, 10000), // :63
        new(typeof(global::Server.Items.ScoutSmallPlateJingasa), 0x2784, 1151255, 1148, 10000), // :64
        new(typeof(global::Server.Items.SorcererArms), 0x13CD, 1151265, 1165, 10000), // :66
        new(typeof(global::Server.Items.SorcererChest), 0x13CC, 1151266, 1165, 10000), // :67
        new(typeof(global::Server.Items.SorcererFemaleChest), 0x1C06, 1151267, 1165, 10000), // :68
        new(typeof(global::Server.Items.SorcererGloves), 0x13C6, 1151268, 1165, 10000), // :69
        new(typeof(global::Server.Items.SorcererGorget), 0x13C7, 1151264, 1165, 10000), // :70
        new(typeof(global::Server.Items.SorcererHat), 0x1718, 1151263, 1165, 10000), // :71
        new(typeof(global::Server.Items.SorcererLegs), 0x13CB, 1151270, 1165, 10000), // :72
        new(typeof(global::Server.Items.SorcererSkirt), 0x1C08, 1151269, 1165, 10000), // :73
        // MISSING :75 YuccaTree 0x0D37 1023383 0 15000
        // MISSING :76 TableLamp 0x49C1 1151220 0 15000
        // MISSING :77 Bamboo 0x246D 1029324 0 15000
        // MISSING :79 HorseBardingDeed 0x14EF 1080212 0 20000
        new(typeof(global::Server.Items.ScrollofAlacrity), 0x14EF, 1078604, 1195, 20000), // :80 (ServUO ScrollOfAlacrity; pinned spells it ScrollofAlacrity)
        // MISSING :82 SnakeSkinBoots 0x170B 1151224 0x7D9 20000
        // MISSING :83 BootsOfTheLavaLizard 0x170B 1151223 0x674 20000
        // MISSING :84 BootsOfTheIceWyrm 0x170B 1151225 0x482 20000
        // MISSING :85 BootsOfTheCrystalHydra 0x170B 1151226 0x47E 20000
        // MISSING :86 BootsOfTheThrasher 0x170B 1151227 0x497 20000
        // MISSING :88 NaturesTears 0x0E9C 1154374 2075 20000
        // MISSING :89 PrimordialDecay 0x0E9C 1154737 1927 20000
        // MISSING :90 ArachnidDoom 0x0E9C 1154738 1944 20000
        // MISSING :92 SophisticatedElvenTapestry 0x2D70 1151222 0 50000
        // MISSING :93 OrnateElvenTapestry 0x2D72 1031633 0 50000
        // MISSING :94 ChestOfDrawers 0x0A2C 1022604 0 50000
        // MISSING :95 FootedChestOfDrawers 0x0A30 1151221 0 50000
        // MISSING :97 DragonHeadAddonDeed 0x2234 1028756 0 50000
        // MISSING :98 NestWithEggs 0x1AD4 1026868 2415 50000
        // MISSING :102 FishermansHat 0x1716 1151238 2578 50000
        // MISSING :103 FishermansTrousers 0x13DA 1151239 2578 50000
        // MISSING :104 FishermansVest 0x13CC 1151240 2578 50000
        // MISSING :105 FishermansEelskinGloves 0x13C6 1151237 2578 50000
        // MISSING :106 FishermansChestguard 0x4052 1151578 2578 50000
        // MISSING :107 FishermansKilt 0x0408 1151579 2578 50000
        // MISSING :108 FishermansArms 0x0302 1151580 2578 50000
        // MISSING :109 FishermansEarrings 0x4213 1151581 2578 50000
        new(typeof(global::Server.Items.BestialArms), 0x0302, 1151549, 2010, 50000), // :112
        new(typeof(global::Server.Items.BestialEarrings), 0x4213, 1151547, 2010, 50000), // :113
        new(typeof(global::Server.Items.BestialGloves), 0x13C6, 1151230, 2010, 50000), // :114
        new(typeof(global::Server.Items.BestialGorget), 0x13D6, 1151232, 2010, 50000), // :115
        new(typeof(global::Server.Items.BestialHelm), 0x1545, 1151229, 2010, 50000), // :116
        new(typeof(global::Server.Items.BestialKilt), 0x0408, 1151550, 2010, 50000), // :117
        new(typeof(global::Server.Items.BestialLegs), 0x13CB, 1151231, 2010, 50000), // :118
        new(typeof(global::Server.Items.BestialNecklace), 0x4210, 1151548, 2010, 50000), // :119
        new(typeof(global::Server.Items.VirtuososArmbands), 0x0308, 1151563, 1374, 50000), // :121
        new(typeof(global::Server.Items.VirtuososCap), 0x171C, 1151324, 1374, 50000), // :122
        new(typeof(global::Server.Items.VirtuososCollar), 0x13C7, 1151323, 1374, 50000), // :123
        new(typeof(global::Server.Items.VirtuososEarpieces), 0x4213, 1151562, 1374, 50000), // :124
        new(typeof(global::Server.Items.VirtuososKidGloves), 0x13C6, 1151560, 1374, 50000), // :125
        new(typeof(global::Server.Items.VirtuososKilt), 0x0408, 1151564, 1374, 50000), // :126
        new(typeof(global::Server.Items.VirtuososNecklace), 0x4213, 1151561, 1374, 50000), // :127
        new(typeof(global::Server.Items.VirtuososTunic), 0x13CC, 1151325, 1374, 50000), // :128
        // MISSING :130 FirePitDeed 0x29FD 1080206 0 75000
        // MISSING :131 PresentationStone 0x32F2 1154745 0 75000
        // MISSING :132 Beehive 0x091A 1080263 0 80000
        new(typeof(global::Server.Items.ArcheryButteDeed), 0x100B, 1024106, 0, 80000), // :133
        new(typeof(global::Server.Items.NovoBleue), 0x1086, 1151242, 1165, 150000), // :135
        new(typeof(global::Server.Items.EtoileBleue), 0x108A, 1151241, 1165, 150000), // :136
        new(typeof(global::Server.Items.SoleilRouge), 0x1086, 1154382, 1166, 150000), // :137
        new(typeof(global::Server.Items.LuneRouge), 0x108A, 1154380, 1166, 150000), // :138
        // MISSING :142 IntenseTealPigment 0xEFF 1154732 2691 250000
        // MISSING :143 TyrianPurplePigment 0xEFF 1154735 2716 250000
        // MISSING :144 MottledSunsetBluePigment 0xEFF 1154734 2714 250000
        // MISSING :145 MossyGreenPigment 0xEFF 1154731 2684 250000
        // MISSING :146 VibrantOcherPigment 0xEFF 1154736 2725 250000
        // MISSING :147 OliveGreenPigment 0xEFF 1154733 2709 250000
        // MISSING :148 PolishedBronzePigment 0xEFF 1151909 1944 250000
        // MISSING :149 GlossyBluePigment 0xEFF 1151910 1916 250000
        // MISSING :150 BlackAndGreenPigment 0xEFF 1151911 1979 250000
        // MISSING :151 DeepVioletPigment 0xEFF 1151912 1929 250000
        // MISSING :152 AuraOfAmberPigment 0xEFF 1152308 1967 250000
        // MISSING :153 MurkySeagreenPigment 0xEFF 1152309 1992 250000
        // MISSING :154 ShadowyBluePigment 0xEFF 1152310 1960 250000
        // MISSING :155 GleamingFuchsiaPigment 0xEFF 1152311 1930 250000
        // MISSING :156 GlossyFuchsiaPigment 0xEFF 1152347 1919 250000
        // MISSING :157 DeepBluePigment 0xEFF 1152348 1939 250000
        // MISSING :158 VibranSeagreenPigment 0xEFF 1152349 1970 250000
        // MISSING :159 MurkyAmberPigment 0xEFF 1152350 1989 250000
        // MISSING :160 VibrantCrimsonPigment 0xEFF 1153386 1964 250000
        // MISSING :161 ReflectiveShadowPigment 0xEFF 1153387 1910 250000
        // MISSING :162 StarBluePigment 0xEFF 1154121 2723 250000
        // MISSING :163 MotherOfPearlPigment 0xEFF 1154120 2720 250000
        // MISSING :164 LiquidSunshinePigment 0xEFF 1154213 1923 250000
        // MISSING :165 DarkVoidPigment 0xEFF 1154214 2068 250000
        // MISSING :167 LuckyCharm 0x2F5B 1154739 1923 300000
        // MISSING :168 SoldiersMedal 0x2F5B 1154740 1902 300000
        // MISSING :169 DuelistsEdge 0x2F58 1154741 1902 300000
        // MISSING :170 NecromancersPhylactery 0x2F5A 1154742 1912 300000
        // MISSING :171 WizardsCurio 0x2F58 1154743 1912 300000
        // MISSING :172 MysticsMemento 0x2F5B 1154744 1912 300000
        // MISSING :174 VollemHeldInCrystal 0x1f19 1113629 1154 500000
    ];

    public static readonly CleanUpReward[] CustodianSupplies =
    [
        new(typeof(Bandage), 0xE21, "100 bandages", 15, 100, true),
        new(typeof(RefreshPotion), 0xF0B, "5 refresh potions", 3, 5, false),
        new(typeof(GreaterHealPotion), 0xF0C, "3 greater heal potions", 5, 3, false)
    ];

    // OrderBy is stable, so equal prices keep their listed order.
    public static IReadOnlyList<CleanUpReward> SortedOsi { get; } = OsiRewards.OrderBy(r => r.Points).ToArray();

    public static IReadOnlyList<CleanUpReward> SortedCustodian { get; } = CustodianSupplies.OrderBy(r => r.Points).ToArray();

    // The store's rows, top to bottom: OSI's section, then ours.
    public static IReadOnlyList<CleanUpReward> Store { get; } = SortedOsi.Concat(SortedCustodian).ToArray();

    public enum PurchaseResult
    {
        Bought,
        NotEnoughPoints,
        NoRoom
    }

    public static PurchaseResult Purchase(PlayerMobile pm, CleanUpReward reward)
    {
        var cub = CleanUpBritanniaData.Instance;

        if (cub.GetPoints(pm) < reward.Points)
        {
            pm.SendLocalizedMessage(1073122); // You don't have enough points for that!
            return PurchaseResult.NotEnoughPoints;
        }

        var items = reward.Create();
        var pack = pm.Backpack;

        if (pack == null || !CanHoldAll(pm, pack, items))
        {
            foreach (var item in items)
            {
                item.Delete();
            }

            pm.SendLocalizedMessage(1074361); // The reward could not be given.  Make sure you have room in your pack.
            return PurchaseResult.NoRoom;
        }

        foreach (var item in items)
        {
            if (item is ScrollofAlacrity scroll)
            {
                scroll.Skill = (SkillName)Utility.Random(SkillInfo.Table.Length);
            }

            pack.DropItem(item);
            item.InvalidateProperties();
        }

        pm.SendLocalizedMessage(1073621); // Your reward has been placed in your backpack.
        cub.DeductPoints(pm, reward.Points);
        pm.PlaySound(0x5A7);

        return PurchaseResult.Bought;
    }

    // ServUO tries one TryDropItem; ours can be several items, so check them all before giving any.
    private static bool CanHoldAll(PlayerMobile pm, Container pack, List<Item> items)
    {
        var weight = items.Sum(i => i.TotalWeight + i.PileWeight);
        var count = items.Count;

        return pack.CheckHold(pm, items[0], false, true, count - 1, weight - items[0].TotalWeight - items[0].PileWeight);
    }
}

// ServUO Gumps/BaseRewardGump.cs and Services/CleanUpBritannia/Gumps.cs, on pinned's legacy gump. ServUO sizes each
// row from the item art (CollectionItem); pinned has no such table, so rows are a fixed height.
public class CleanUpBritanniaRewardGump : Gump
{
    private const int RowHeight = 40;
    private const int RowsPerPage = 5;

    private readonly Mobile _owner;
    private readonly PlayerMobile _user;

    public CleanUpBritanniaRewardGump(Mobile owner, PlayerMobile user) : base(50, 50)
    {
        user.CloseGump<CleanUpBritanniaRewardGump>();

        _owner = owner;
        _user = user;

        Closable = true;
        Disposable = true;
        Draggable = true;
        Resizable = false;

        var points = CleanUpBritanniaData.Instance.GetPoints(user);
        Points = points;

        AddPage(0);

        AddImage(0, 0, 0x1F40);
        AddImageTiled(20, 37, 300, 308, 0x1F42);
        AddImage(20, 325, 0x1F43);
        AddImage(35, 8, 0x39);
        AddImageTiled(65, 8, 257, 10, 0x3A);
        AddImage(290, 8, 0x3B);
        AddImage(32, 33, 0x2635);
        AddImageTiled(70, 55, 230, 2, 0x23C5);

        AddHtmlLocalized(70, 35, 270, 20, 1151316, 0x1); // the Clean Up gump's title (Gumps.cs:14)
        AddHtmlLocalized(50, 65, 150, 20, 1072843, 0x1); // Your Reward Points:
        AddLabel(230, 65, 0x64, ((int)points).ToString());
        AddImageTiled(35, 85, 270, 2, 0x23C5);
        AddHtmlLocalized(35, 90, 270, 20, 1072844, 0x1); // Please Choose a Reward:

        var store = CleanUpBritanniaRewards.Store;
        var pages = (store.Count + RowsPerPage - 1) / RowsPerPage;

        for (var page = 0; page < pages; page++)
        {
            AddPage(page + 1);

            var offset = 110;

            for (var i = page * RowsPerPage; i < Math.Min(store.Count, (page + 1) * RowsPerPage); i++)
            {
                var reward = store[i];
                var affordable = points >= reward.Points;

                if (affordable)
                {
                    AddButton(35, offset + RowHeight / 2 - 5, 0x837, 0x838, 200 + i);
                }

                // ServUO GetItemHue (:236-244): grey until affordable.
                AddItem(60, offset, reward.ItemID, affordable ? reward.Hue : 0x3E9);

                if (reward.Ours)
                {
                    AddLabel(130, offset + RowHeight / 2 - 18, 0x35, "Custodian supplies");
                    AddLabel(130, offset + RowHeight / 2 - 2, 0x481, reward.Label);
                }
                else if (reward.Tooltip > 0)
                {
                    AddTooltip(reward.Tooltip);
                }

                AddLabel(250, offset + RowHeight / 2 - 10, affordable ? 0x64 : 0x21, reward.Points.ToString("N0"));

                offset += RowHeight + 2;
            }

            if (page > 0)
            {
                AddButton(150, 335, 0x15E3, 0x15E7, 0, GumpButtonType.Page, page);
                AddHtmlLocalized(170, 335, 60, 20, 1074880, 0x1); // Previous
            }

            if (page < pages - 1)
            {
                AddButton(300, 335, 0x15E1, 0x15E5, 0, GumpButtonType.Page, page + 2);
                AddHtmlLocalized(240, 335, 60, 20, 1072854, 0x1); // <div align=right>Next</div>
            }
        }
    }

    public double Points { get; }

    public override void OnResponse(NetState state, in RelayInfo info)
    {
        if (info.ButtonID < 200 || state.Mobile is not PlayerMobile from)
        {
            return;
        }

        var store = CleanUpBritanniaRewards.Store;
        var index = info.ButtonID - 200;

        if (index >= store.Count)
        {
            return;
        }

        var reward = store[index];

        if (reward.Points <= CleanUpBritanniaData.Instance.GetPoints(from))
        {
            from.SendGump(new ConfirmCleanUpRewardGump(_owner, reward));
        }
        else
        {
            from.SendLocalizedMessage(1073122); // You don't have enough points for that!
        }
    }
}

// ServUO aConfirmRewardGump (Gumps/BaseRewardGump.cs:218-246).
public class ConfirmCleanUpRewardGump : BaseConfirmGump
{
    private readonly Mobile _owner;
    private readonly CleanUpReward _reward;

    public ConfirmCleanUpRewardGump(Mobile owner, CleanUpReward reward)
    {
        _owner = owner;
        _reward = reward;
    }

    public override int TitleNumber => 1074974;
    public override int LabelNumber => 1074975;

    public override void Confirm(Mobile from)
    {
        if (from is PlayerMobile pm && from.InRange(_owner.Location, 5))
        {
            CleanUpBritanniaRewards.Purchase(pm, _reward);
        }
    }
}
