// cc-P763: the items the diversified loot boxes give that the shard had no class for. Notes: shard-migration
// notes/cc-P763-cont-loot-boxes-diversified.md (the plan: notes/cc-P759-loot-plan.md, section 6).
//
// BagOfHolding     TheArt's "Bags of Holding" (servuo.dev 1694, reference only in D:\UO\third-party\code\bags-of-holding),
//                  ported: five sizes in one class, Lesser to Grand, an item cap (LootTable.BagOfHoldingCaps: 12, 24, 36,
//                  48, 125) and no weight cap of its own. Changes from TheArt's, as the approved plan sets them: what is
//                  inside still weighs what it weighs on the carrier (TheArt's counted it as nothing); it is not blessed;
//                  the fifth, TheArt's "Rare Bag of Holding", is the "Grand Bag of Holding", so "Rare" means only the
//                  rarity roll. The hues are TheArt's.
// ReagentBag       Bittiez's "Regeant Bag" (servuo.dev 1895, reference only in D:\UO\third-party\code\reagent-bag), ported:
//                  holds only reagents and cuts their weight by its percent (25, 50, 75 or 90 by the box's rarity,
//                  LootTable.ReagentBagCut). Bittiez's Reduction setter never stored its value and its load read an int
//                  for a double; the port stores the cut as a whole percent. The weight change it passes up to the
//                  carrier is the reduced one, so the cut reaches the backpack (stock Container passes every child's
//                  full change up, pinned Server/Items/Container.cs UpdateTotal).
// LootDecor        a piece of registered art (player-package registry.csv) as a house item: a painting, a crystal ball,
//                  a trophy. A wall piece with two facings turns when double-clicked in the pack.
// LeagueTitleDeed  a League title (LeagueTitles) for the account; used, it adds the title to the account's titles.

#nullable enable

using System;
using ModernUO.Serialization;
using Server.Engines.LeagueRewards;
using Server.LootBoxes;
using Server.Mobiles;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class BagOfHolding : Bag
{
    // TheArt's hues, Lesser to Rare (here Grand).
    private static readonly int[] Hues = [1925, 1923, 1927, 1922, 1919];

    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private int _size;

    [Constructible]
    public BagOfHolding() : this(2)
    {
    }

    [Constructible]
    public BagOfHolding(int size)
    {
        _size = Math.Clamp(size, 1, 5);
        Hue = Hues[_size - 1];
        Weight = 1.0;
    }

    private int Step => Math.Clamp(_size, 1, 5);

    public override string DefaultName => LootTable.Current.BagOfHoldingNames[Step - 1];

    public override int DefaultMaxItems => LootTable.Current.BagOfHoldingCaps[Step - 1];

    // No weight cap of its own; the carrier's still counts what is inside (Container.CheckHold asks the parents).
    public override int DefaultMaxWeight => 0;

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add($"Holds up to {DefaultMaxItems} items, of any weight");
    }
}

[SerializationGenerator(0, false)]
public partial class ReagentBag : Bag
{
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private int _weightCut;

    // Not saved: the uncut weight of what is inside, rebuilt by UpdateTotals at load.
    private int _rawWeight;

    [Constructible]
    public ReagentBag() : this(90)
    {
    }

    [Constructible]
    public ReagentBag(int weightCut)
    {
        _weightCut = Math.Clamp(weightCut, 0, 100);
        Hue = 673; // Bittiez's
        Weight = 1.0;
    }

    public override string DefaultName => "a reagent bag";

    public int Cut(int raw) => raw - raw * Math.Clamp(_weightCut, 0, 100) / 100;

    public override bool CheckHold(Mobile m, Item item, bool message, bool checkItems, int plusItems, int plusWeight)
    {
        if (item is not BaseReagent)
        {
            if (message)
            {
                m.SendMessage("Only reagents go in this bag.");
            }

            return false;
        }

        return base.CheckHold(m, item, message, checkItems, plusItems, plusWeight);
    }

    public override int GetTotal(TotalType type) => type == TotalType.Weight ? Cut(_rawWeight) : base.GetTotal(type);

    public override void UpdateTotal(Item sender, TotalType type, int delta)
    {
        if (type == TotalType.Weight && sender != this && delta != 0 && !sender.IsVirtualItem)
        {
            var before = Cut(_rawWeight);
            _rawWeight += delta;
            base.UpdateTotal(sender, type, Cut(_rawWeight) - before);
            return;
        }

        base.UpdateTotal(sender, type, delta);
    }

    public override void UpdateTotals()
    {
        base.UpdateTotals();
        _rawWeight = base.GetTotal(TotalType.Weight);
    }

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add($"Reagents only; they weigh {_weightCut}% less inside");
    }
}

[SerializationGenerator(0, false)]
public partial class LootDecor : Item
{
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private int _altId;

    [Constructible]
    public LootDecor() : this(0x3C9E, 0, "a painting")
    {
    }

    public LootDecor(int itemId, int altId, string name) : base(itemId)
    {
        _altId = altId;
        Name = name;
        Weight = 5.0;
    }

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add(_altId != 0 ? "House decor; double-click in your pack to turn it" : "House decor");
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (_altId == 0)
        {
            return;
        }

        if (!IsChildOf(from.Backpack))
        {
            from.SendLocalizedMessage(1042001); // That must be in your pack for you to use it.
            return;
        }

        var id = ItemID;
        ItemID = _altId;
        AltId = id;
    }
}

[SerializationGenerator(0, false)]
public partial class LeagueTitleDeed : Item
{
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private string _titleKey;

    [Constructible]
    public LeagueTitleDeed() : this(LeagueTitles.RegisteredAdventurer)
    {
    }

    public LeagueTitleDeed(string titleKey) : base(0x14F0)
    {
        _titleKey = titleKey;
        Hue = 1153;
        Weight = 1.0;
    }

    public override string DefaultName => $"a League title deed: {LeagueTitles.Display(_titleKey)}";

    /// <summary>A deed for a League title the account can buy and does not hold; null when it holds them all.</summary>
    public static LeagueTitleDeed? ForAccount(PlayerMobile pm)
    {
        var data = pm.Account is Accounting.IAccount acct ? ClusterFAccountPersistence.GetOrCreate(acct) : null;
        var open = new System.Collections.Generic.List<string>();
        foreach (var t in LeagueTitles.All)
        {
            if (t.FreeAtAp < 0 && !t.HiddenUntilHeld && (data == null || !LeagueTitles.Holds(data, t.Key)))
            {
                open.Add(t.Key);
            }
        }

        return open.Count > 0 ? new LeagueTitleDeed(open[Utility.Random(open.Count)]) : null;
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (!IsChildOf(from.Backpack))
        {
            from.SendLocalizedMessage(1042001); // That must be in your pack for you to use it.
            return;
        }

        if (from.Account is not Accounting.IAccount acct)
        {
            return;
        }

        var data = ClusterFAccountPersistence.GetOrCreate(acct);
        if (LeagueTitles.Holds(data, _titleKey))
        {
            from.SendMessage(0x22, $"System: Your account already holds {LeagueTitles.Display(_titleKey)}.");
            return;
        }

        data.LeagueTitles.Owned.Add(_titleKey);
        from.SendMessage(0x44, $"System: {LeagueTitles.Display(_titleKey)} is now one of your account's League titles. Choose it in the League shop.");
        Delete();
    }
}
