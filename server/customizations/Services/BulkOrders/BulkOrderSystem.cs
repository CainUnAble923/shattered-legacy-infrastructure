// cc-P122: Bulk orders part 1, the 2016 core (Chase's option A, 2026-10-15). Ported from ServUO
// Scripts/Services/BulkOrders/BulkOrderSystem.cs (944 lines) and the bribe half of ServUO BaseVendor (:1350-1460).
//
// OSI's 2016 bulk-order rules, which ServUO switches on with NewSystemEnabled = Core.TOL (this shard runs Endless Journey):
//  - offers: per player and per craft, up to 3 at once (2 cached and 1 more when the last was over 6 hours ago), one
//    added back every 6 hours (BODEntry.CheckCache);
//  - turn-in at a town vendor: gold to the bank, fame, and reward points: claimed now in the rewards gump, or banked at 2%
//    (small) or 20% (large) of their value, by the player's points mode (Enabled asks, Disabled claims, Automatic banks);
//    a turn-in whose points are not yet claimed holds every vendor until they are;
//  - bribes: pay a vendor to upgrade an untouched deed (more items, exceptional, or one material up).
//
// How it meets pinned's engine. Pinned keeps the pre-2016 engine (Engines/Bulk Orders/, smith and tailor). Its deed and
// book types are kept as they are, so every deed and book in the world keeps its exact saved shape and the shard's eight
// BOD patches and replacements keep applying unchanged; this file and BulkOrderVendors.cs add the 2016 rules around them.
// Points use pinned's own calculators (their ComputePoints is ServUO's, line for line). Per-player data and the vendors'
// bribe state save in their own file (Saves/BulkOrderSystem, pinned's GenericPersistence), not in PlayerMobile or
// BaseVendor, whose save formats are untouched. A world saved before this file existed has none and loads with every
// player's offers full, as ServUO's first-contact rule (LastBulkOrder unset) gives them.
//
// cc-P123 adds the six other 2016 crafts: their deeds (CraftBOD.cs and one file per ServUO deed), lists
// (CraftBulkEntries.cs), calculators (CraftRewardCalculators.cs), the book's share (BulkOrderBooks.cs) and the guild masters
// who are not shopkeepers (BulkOrderGivers.cs); here, the crafts in CreateBulkOrder, GetBODType, GetCalculator and the
// exceptional and material rules (notes/cc-P123-port-bulk-orders-crafts.md).
//
// Changes from ServUO, each argued in notes/cc-P122-port-bulk-orders-core.md:
//  - the book filter stays on the book (pinned's BOBFilter is per book, ServUO's per player);
//  - ServUO's GenericBuyInfo.BuyPrices (filled by whichever vendor was constructed last) is built here once, from every
//    shop list, the lowest price for each type;
//  - the bribe's material step stops at Valorite, Barbed and Frostwood as ServUO's does, and at Celestial for the shard's
//    post-Valorite metals (a step past Celestial would land on a wood);
//  - post-Valorite deeds earn iron's points, as pinned's calculator and today's turn-in already pay them (cc-P56).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Server.Items;
using Server.Mobiles;

namespace Server.Engines.BulkOrders;

public enum PointsMode
{
    Enabled,
    Disabled,
    Automatic
}

public static class BulkOrderSystem
{
    // Logic (EA says 3 cached): 2 cached, 1 in the pipe if the last bod > 6 hours = 3
    public const int MaxCachedDeeds = 2;
    public const int Delay = 6;

    // ServUO: Core.TOL. The shard runs Endless Journey (Configuration/expansion.json).
    public static bool NewSystemEnabled => Core.TOL;

    private static readonly Dictionary<PlayerMobile, BODContext> _contexts = new();

    public static IReadOnlyDictionary<PlayerMobile, BODContext> Contexts => _contexts;

    public static BODContext GetContext(Mobile m, bool create = true)
    {
        if (m is not PlayerMobile pm)
        {
            return null;
        }

        if (!_contexts.TryGetValue(pm, out var context) && create)
        {
            _contexts[pm] = context = new BODContext();
        }

        return context;
    }

    internal static void SetContext(PlayerMobile pm, BODContext context) => _contexts[pm] = context;

    internal static void ClearContexts() => _contexts.Clear();

    public static double GetPoints(Mobile m, BODType type) => GetContext(m)?.Entries[type].BankedPoints ?? 0;

    // ServUO's SetPoints adds; the name is kept.
    public static void SetPoints(Mobile m, BODType type, double points)
    {
        var context = GetContext(m);

        if (context != null)
        {
            context.Entries[type].BankedPoints += Math.Max(0.0, points);
        }
    }

    public static void DeductPoints(Mobile m, BODType type, double points)
    {
        var context = GetContext(m);

        if (context != null)
        {
            var entry = context.Entries[type];
            entry.BankedPoints = Math.Max(0.0, entry.BankedPoints - Math.Max(0.0, points));
        }
    }

    // -- Offers ------------------------------------------------------------------------------------------------------

    /// <summary>Takes an offer if one is available (ServUO :168-196).</summary>
    public static bool CanGetBulkOrder(Mobile m, BODType type)
    {
        var entry = GetContext(m)?.Entries[type];

        if (entry == null)
        {
            return false;
        }

        entry.CheckCache();

        if (entry.LastBulkOrder + TimeSpan.FromHours(Delay) < Core.Now)
        {
            entry.LastBulkOrder = Core.Now;
            return true;
        }

        if (entry.CachedDeeds > 0)
        {
            entry.CachedDeeds--;
            return true;
        }

        return false;
    }

    /// <summary>Whether an offer is available, without taking it.</summary>
    public static bool HasBulkOrder(Mobile m, BODType type)
    {
        var entry = GetContext(m)?.Entries[type];

        if (entry == null)
        {
            return false;
        }

        entry.CheckCache();
        return entry.LastBulkOrder + TimeSpan.FromHours(Delay) < Core.Now || entry.CachedDeeds > 0;
    }

    public static TimeSpan GetNextBulkOrder(BODType type, PlayerMobile pm)
    {
        var context = GetContext(pm);

        if (context == null)
        {
            return TimeSpan.MaxValue;
        }

        var last = context.Entries[type].LastBulkOrder;
        return last + TimeSpan.FromHours(Delay) - Core.Now;
    }

    public static SkillName GetSkillForBOD(BODType type) =>
        type switch
        {
            BODType.Tailor      => SkillName.Tailoring,
            BODType.Alchemy     => SkillName.Alchemy,
            BODType.Inscription => SkillName.Inscribe,
            BODType.Tinkering   => SkillName.Tinkering,
            BODType.Cooking     => SkillName.Cooking,
            BODType.Fletching   => SkillName.Fletching,
            BODType.Carpentry   => SkillName.Carpentry,
            _                   => SkillName.Blacksmith
        };

    public static int GetBodHue(BODType type) =>
        type switch
        {
            BODType.Tailor      => 0x483,
            BODType.Alchemy     => 2505,
            BODType.Inscription => 2598,
            BODType.Tinkering   => 1109,
            BODType.Cooking     => 1169,
            BODType.Fletching   => 1425,
            BODType.Carpentry   => 1512,
            _                   => 0x44E
        };

    /// <summary>
    /// A new deed of this craft for the player (ServUO :247-289): large at 70.1 with ServUO's roll, else small. The six
    /// 2016 crafts' deeds are cc-P123's (CraftBODs.cs).
    /// </summary>
    public static Item CreateBulkOrder(Mobile m, BODType type, bool fromContextMenu)
    {
        if (m is not PlayerMobile pm)
        {
            return null;
        }

        if (pm.AccessLevel > AccessLevel.Player || fromContextMenu || 0.2 > Utility.RandomDouble())
        {
            var theirSkill = pm.Skills[GetSkillForBOD(type)].Base;
            var doLarge = theirSkill >= 70.1 && (theirSkill - 40.0) / 300.0 > Utility.RandomDouble();

            switch (type)
            {
                case BODType.Smith:
                    return doLarge ? new LargeSmithBOD() : SmallSmithBOD.CreateRandomFor(pm);
                case BODType.Tailor:
                    return doLarge ? new LargeTailorBOD() : SmallTailorBOD.CreateRandomFor(pm);
                case BODType.Alchemy:
                    return doLarge ? new LargeAlchemyBOD() : SmallAlchemyBOD.CreateRandomFor(pm);
                case BODType.Inscription:
                    return doLarge ? new LargeInscriptionBOD() : SmallInscriptionBOD.CreateRandomFor(pm);
                case BODType.Tinkering:
                    return doLarge ? new LargeTinkerBOD() : SmallTinkerBOD.CreateRandomFor(pm);
                case BODType.Cooking:
                    return doLarge ? new LargeCookingBOD() : SmallCookingBOD.CreateRandomFor(pm);
                case BODType.Fletching:
                    return doLarge ? new LargeFletchingBOD() : SmallFletchingBOD.CreateRandomFor(pm);
                case BODType.Carpentry:
                    return doLarge ? new LargeCarpentryBOD() : SmallCarpentryBOD.CreateRandomFor(pm);
            }
        }

        return null;
    }

    // -- What a deed is ----------------------------------------------------------------------------------------------

    public static BODType? GetBODType(Item bod) =>
        bod switch
        {
            SmallSmithBOD or LargeSmithBOD             => BODType.Smith,
            SmallTailorBOD or LargeTailorBOD           => BODType.Tailor,
            SmallAlchemyBOD or LargeAlchemyBOD         => BODType.Alchemy,
            SmallInscriptionBOD or LargeInscriptionBOD => BODType.Inscription,
            SmallTinkerBOD or LargeTinkerBOD           => BODType.Tinkering,
            SmallCookingBOD or LargeCookingBOD         => BODType.Cooking,
            SmallFletchingBOD or LargeFletchingBOD     => BODType.Fletching,
            SmallCarpentryBOD or LargeCarpentryBOD     => BODType.Carpentry,
            _                                          => null
        };

    public static RewardCalculator GetCalculator(BODType type) =>
        type switch
        {
            BODType.Tailor      => TailorRewardCalculator.Instance,
            BODType.Alchemy     => AlchemyRewardCalculator.Instance,
            BODType.Inscription => InscriptionRewardCalculator.Instance,
            BODType.Tinkering   => TinkeringRewardCalculator.Instance,
            BODType.Cooking     => CookingRewardCalculator.Instance,
            BODType.Fletching   => FletchingRewardCalculator.Instance,
            BODType.Carpentry   => CarpentryRewardCalculator.Instance,
            _                   => SmithRewardCalculator.Instance
        };

    // -- Points and gold ---------------------------------------------------------------------------------------------

    public static int ComputePoints(BaseBOD bod)
    {
        var type = GetBODType(bod);

        if (type == null)
        {
            return 0;
        }

        var calc = GetCalculator(type.Value);

        return bod switch
        {
            SmallBOD small => calc.ComputePoints(small),
            LargeBOD large => calc.ComputePoints(large),
            _              => 0
        };
    }

    /// <summary>ServUO :378-418: the points a turn-in is worth, and what they bank at (2% small, 20% large).</summary>
    public static void ComputePoints(BaseBOD bod, out int points, out double banked)
    {
        points = ComputePoints(bod);
        banked = points * (bod is LargeBOD ? 0.2 : 0.02);
    }

    /// <summary>
    /// The gold a turn-in pays. ServUO's 2016 calculators pay a small deed half the shop price of what it asks for
    /// (quantity * price / 2) when a shop sells it, and the old table otherwise; a large deed always pays the table.
    /// </summary>
    public static int ComputeGold(BaseBOD bod)
    {
        if (bod is SmallBOD small && ComputeGold(small.Type, small.AmountMax, out var gold))
        {
            return gold;
        }

        return bod.ComputeGold();
    }

    public static bool ComputeGold(Type type, int quantity, out int gold)
    {
        if (type != null && BuyPrices.TryGetValue(type, out var price))
        {
            gold = quantity * price / 2;
            return true;
        }

        gold = 0;
        return false;
    }

    private static Dictionary<Type, int> _buyPrices;

    /// <summary>The lowest price any shop asks for each type, from every shop list (ServUO's GenericBuyInfo.BuyPrices).</summary>
    public static IReadOnlyDictionary<Type, int> BuyPrices => _buyPrices ??= BuildBuyPrices();

    private static Dictionary<Type, int> BuildBuyPrices()
    {
        var prices = new Dictionary<Type, int>();

        foreach (var t in typeof(SBInfo).Assembly.GetTypes())
        {
            if (t.IsAbstract || !typeof(SBInfo).IsAssignableFrom(t) || t.GetConstructor(Type.EmptyTypes) == null)
            {
                continue;
            }

            List<GenericBuyInfo> buy;

            try
            {
                buy = ((SBInfo)Activator.CreateInstance(t))?.BuyInfo;
            }
            catch
            {
                continue; // a shop list that cannot be built outside a vendor has no price to give
            }

            if (buy == null)
            {
                continue;
            }

            foreach (var info in buy)
            {
                if (info?.Type == null || info.Price <= 0)
                {
                    continue;
                }

                prices[info.Type] = prices.TryGetValue(info.Type, out var p) ? Math.Min(p, info.Price) : info.Price;
            }
        }

        return prices;
    }

    // -- Pending rewards ---------------------------------------------------------------------------------------------

    public static void AddToPending(Mobile m, BODType type, int points) => GetContext(m)?.AddPending(type, points);

    public static void RemovePending(Mobile m, BODType type) => GetContext(m)?.RemovePending(type);

    public static bool CanClaimRewards(Mobile m) => GetContext(m)?.CanClaimRewards() ?? true;

    // -- Bribes (ServUO :452-646) ------------------------------------------------------------------------------------

    public static Type GetTypeFromBOD(BaseBOD bod) =>
        bod switch
        {
            SmallBOD small                                 => small.Type,
            LargeBOD { Entries.Length: > 0 } large => large.Entries[0].Details.Type,
            _                                              => null
        };

    /// <summary>
    /// ServUO :503-515: alchemy and inscription never; tinkering, cooking and fletching unless the item is on ServUO's
    /// excluded list. cc-P123: and never for an item that cannot be exceptional on this shard (CraftBOD.CanBeExceptional),
    /// which ServUO's own IQuality check would refuse at the combine.
    /// </summary>
    public static bool CanBeExceptional(BaseBOD bod) =>
        GetBODType(bod) switch
        {
            BODType.Smith or BODType.Tailor             => true,
            BODType.Alchemy or BODType.Inscription      => false,
            BODType.Tinkering or BODType.Cooking or BODType.Fletching =>
                !IsInExceptionalExcludeList(bod) && CraftBOD.CanBeExceptional(GetTypeFromBOD(bod)),
            _ => CraftBOD.CanBeExceptional(GetTypeFromBOD(bod))
        };

    /// <summary>ServUO :529-543, and (cc-P123) only for an item that records its material on this shard.</summary>
    public static bool CanUseMaterial(BaseBOD bod) =>
        GetBODType(bod) switch
        {
            BODType.Smith                                                => true,
            BODType.Tailor => BGTClassifier.Classify(BODType.Tailor, GetTypeFromBOD(bod)) == BulkGenericType.Leather,
            BODType.Alchemy or BODType.Inscription or BODType.Cooking    => false,
            BODType.Tinkering or BODType.Fletching =>
                !IsInExceptionalExcludeList(bod) && CraftBOD.CanUseMaterial(GetTypeFromBOD(bod)),
            _ => CraftBOD.CanUseMaterial(GetTypeFromBOD(bod))
        };

    private static bool IsInExceptionalExcludeList(BaseBOD bod) =>
        GetTypeFromBOD(bod) is { } t && Array.IndexOf(_exceptionalExcluded, t) >= 0;

    // ServUO :545-555. Not on the shard, so not listed: EnchantedApple, GrapesOfWrath, MetalContainerEngraver.
    private static readonly Type[] _exceptionalExcluded =
    [
        typeof(Arrow), typeof(Bolt), typeof(Kindling), typeof(Shaft),
        typeof(TribalPaint), typeof(EggBomb), typeof(CookedBird), typeof(FishSteak), typeof(FriedEggs), typeof(LambLeg),
        typeof(Ribs),
        typeof(Gears), typeof(Axle), typeof(Springs), typeof(AxleGears), typeof(ClockParts), typeof(Clock), typeof(PotionKeg),
        typeof(ClockFrame)
    ];

    /// <summary>The top of each material line: a bribe never moves a deed past it.</summary>
    public static bool IsTopMaterial(BulkMaterialType m) =>
        m is BulkMaterialType.Valorite or BulkMaterialType.Barbed or BulkMaterialType.Frostwood
            or BulkMaterialType.Celestial;

    /// <summary>True for a material a bribe may raise: the shard's post-Valorite metals stay where they are.</summary>
    private static bool CanRaiseMaterial(BulkMaterialType m) =>
        !IsTopMaterial(m) && m is < BulkMaterialType.Platinum or > BulkMaterialType.Celestial;

    public static bool CanExchangeBOD(Mobile from, BaseVendor vendor, BaseBOD bod, int cost)
    {
        if (GetBODType(bod) != BulkOrderVendors.GetVendorBODType(vendor))
        {
            vendor.SayTo(from, 1152298, 0x3B2); // I don't deal in those goods.
            return false;
        }

        if (bod is SmallBOD { AmountCur: > 0 } || bod is LargeBOD large && large.Entries?.Any(e => e.Amount > 0) == true)
        {
            vendor.SayTo(from, 1152299, 0x3B2); // I am sorry to say I cannot work with a deed that is even partially filled.
            return false;
        }

        if (bod.AmountMax == 20 && (!CanBeExceptional(bod) || bod.RequireExceptional) &&
            (!CanUseMaterial(bod) || !CanRaiseMaterial(bod.Material)))
        {
            vendor.SayTo(from, 1152291, 0x3B2); // I won't be able to replace that bulk order with a better one.
            return false;
        }

        if (cost > -1 && !Banker.Withdraw(from, cost))
        {
            // I am afraid your bank box does not contain the funds needed to complete this transaction.
            vendor.SayTo(from, 1152302, 0x3B2);
            return false;
        }

        return true;
    }

    public static void MutateBOD(BaseBOD bod)
    {
        var picker = new List<int>();

        if (!bod.RequireExceptional && CanBeExceptional(bod))
        {
            picker.Add(0);
        }

        if (bod.AmountMax < 20)
        {
            picker.Add(1);
        }

        if (CanUseMaterial(bod) && CanRaiseMaterial(bod.Material))
        {
            picker.Add(2);
        }

        if (picker.Count == 0)
        {
            return;
        }

        switch (picker.RandomElement())
        {
            case 0:
                bod.RequireExceptional = true;
                break;
            case 1:
                bod.AmountMax += 5;
                break;
            case 2:
                if (bod.Material == BulkMaterialType.None)
                {
                    switch (BGTClassifier.Classify(GetBODType(bod) ?? BODType.Smith, null))
                    {
                        case BulkGenericType.Iron:
                            bod.Material = BulkMaterialType.DullCopper;
                            break;
                        case BulkGenericType.Leather:
                            bod.Material = BulkMaterialType.Spined;
                            break;
                        case BulkGenericType.Wood:
                            bod.Material = BulkMaterialType.OakWood;
                            break;
                    }
                }
                else
                {
                    bod.Material++;
                }

                break;
        }
    }

    public static int GetBribe(BaseBOD bod)
    {
        var worth = 0;

        if (bod.RequireExceptional)
        {
            worth += 400;
        }

        worth += bod.Material switch
        {
            BulkMaterialType.DullCopper => 25,
            BulkMaterialType.ShadowIron => 50,
            BulkMaterialType.Copper     => 100,
            BulkMaterialType.Bronze     => 200,
            BulkMaterialType.Gold       => 300,
            BulkMaterialType.Agapite    => 400,
            BulkMaterialType.Verite     => 500,
            BulkMaterialType.Valorite   => 600,
            BulkMaterialType.Spined     => 100,
            BulkMaterialType.Horned     => 250,
            BulkMaterialType.Barbed     => 500,
            BulkMaterialType.OakWood    => 100,
            BulkMaterialType.AshWood    => 200,
            BulkMaterialType.YewWood    => 300,
            BulkMaterialType.Heartwood  => 400,
            BulkMaterialType.Bloodwood  => 500,
            BulkMaterialType.Frostwood  => 600,
            _                           => 0
        };

        worth += bod.AmountMax == 20 ? 250 : 100;

        if (bod is LargeBOD { Entries: not null } large)
        {
            worth *= Math.Min(4, large.Entries.Length);
        }

        return worth;
    }

    // -- Vendors' bribe state (ServUO BaseVendor :1350-1460; saved here, not on the vendor) --------------------------

    private static readonly Dictionary<BaseVendor, VendorBribeState> _vendors = new();

    public static IReadOnlyDictionary<BaseVendor, VendorBribeState> VendorStates => _vendors;

    public static VendorBribeState GetVendorState(BaseVendor vendor)
    {
        if (!_vendors.TryGetValue(vendor, out var state))
        {
            _vendors[vendor] = state = new VendorBribeState { BribeMultiplier = Utility.Random(10) };
        }

        state.CheckDecay();
        return state;
    }

    internal static void SetVendorState(BaseVendor vendor, VendorBribeState state) => _vendors[vendor] = state;

    internal static void ClearVendorStates() => _vendors.Clear();

    // -- The minute tick (ServUO :648-674) ---------------------------------------------------------------------------

    public static void Initialize()
    {
        if (NewSystemEnabled)
        {
            Timer.StartTimer(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1), OnTick);
        }

        Console.WriteLine(Census());
    }

    /// <summary>
    /// The deeds and books in the world, read once the world has loaded (Initialize runs after World.Load): the start log's
    /// proof that a world saved before cc-P122 kept every deed and book.
    /// </summary>
    public static string Census()
    {
        int smith = 0, tailor = 0, crafts = 0, books = 0, entries = 0;

        foreach (var item in World.Items.Values)
        {
            switch (item)
            {
                case BaseBOD bod when GetBODType(bod) == BODType.Smith:
                    smith++;
                    break;
                case BaseBOD bod when GetBODType(bod) == BODType.Tailor:
                    tailor++;
                    break;
                case BaseBOD bod when GetBODType(bod) != null: // cc-P123: the six 2016 crafts
                    crafts++;
                    break;
                case BulkOrderBook book:
                    books++;
                    entries += book.Entries.Count;
                    break;
            }
        }

        return $"[BulkOrders] 2016 rules {(NewSystemEnabled ? "on" : "off")}: {smith} smith and {tailor} tailor deeds, " +
               $"{crafts} of the six other crafts', " +
               $"{books} books holding {entries} deeds, {_contexts.Count} players' offers on file.";
    }

    public static void OnTick()
    {
        foreach (var context in _contexts.Values)
        {
            context.CheckCache();
        }
    }

    public static void RemovePlayer(PlayerMobile pm) => _contexts.Remove(pm);

    public static void RemoveVendor(BaseVendor vendor) => _vendors.Remove(vendor);
}

public class BODContext
{
    private static readonly BODType[] _types = Enum.GetValues<BODType>();

    public Dictionary<BODType, BODEntry> Entries { get; } = new();

    [CommandProperty(AccessLevel.GameMaster)]
    public PointsMode PointsMode { get; set; }

    public BODContext()
    {
        foreach (var type in _types)
        {
            Entries[type] = new BODEntry();
        }
    }

    public void AddPending(BODType type, int points) => Entries[type].PendingRewardPoints = points;

    public void RemovePending(BODType type) => Entries[type].PendingRewardPoints = 0;

    public int GetPendingRewardFor(BODType type) => Entries[type].PendingRewardPoints;

    public bool CanClaimRewards() => Entries.Values.All(e => e.PendingRewardPoints <= 0);

    public void CheckCache()
    {
        foreach (var entry in Entries.Values)
        {
            entry.CheckCache();
        }
    }

    public void Serialize(IGenericWriter writer)
    {
        writer.WriteEncodedInt(0); // version
        writer.WriteEncodedInt((int)PointsMode);

        // By craft name, so a craft added or reordered later finds its own entry.
        writer.WriteEncodedInt(Entries.Count);

        foreach (var (type, entry) in Entries)
        {
            writer.Write(type.ToString());
            entry.Serialize(writer);
        }
    }

    public void Deserialize(IGenericReader reader)
    {
        reader.ReadEncodedInt(); // version
        PointsMode = (PointsMode)reader.ReadEncodedInt();

        var count = reader.ReadEncodedInt();

        for (var i = 0; i < count; i++)
        {
            var name = reader.ReadString();
            var entry = new BODEntry();
            entry.Deserialize(reader);

            if (Enum.TryParse<BODType>(name, out var type))
            {
                Entries[type] = entry;
            }
        }
    }
}

[PropertyObject]
public class BODEntry
{
    private int _cachedDeeds = BulkOrderSystem.MaxCachedDeeds;

    [CommandProperty(AccessLevel.GameMaster)]
    public int CachedDeeds
    {
        get => _cachedDeeds;
        set => _cachedDeeds = Math.Clamp(value, 0, BulkOrderSystem.MaxCachedDeeds);
    }

    [CommandProperty(AccessLevel.GameMaster)]
    public DateTime LastBulkOrder { get; set; }

    [CommandProperty(AccessLevel.GameMaster)]
    public double BankedPoints { get; set; }

    [CommandProperty(AccessLevel.GameMaster)]
    public int PendingRewardPoints { get; set; }

    public override string ToString() => "...";

    public void CheckCache()
    {
        if (_cachedDeeds >= BulkOrderSystem.MaxCachedDeeds)
        {
            return;
        }

        if (LastBulkOrder == DateTime.MinValue)
        {
            CachedDeeds = BulkOrderSystem.MaxCachedDeeds;
            LastBulkOrder = Core.Now - TimeSpan.FromHours(BulkOrderSystem.Delay);
        }
        else if (LastBulkOrder + TimeSpan.FromHours(BulkOrderSystem.Delay) < Core.Now)
        {
            CachedDeeds++;
            LastBulkOrder += TimeSpan.FromHours(BulkOrderSystem.Delay);
        }
    }

    public bool IsDefault =>
        _cachedDeeds == BulkOrderSystem.MaxCachedDeeds && LastBulkOrder == DateTime.MinValue && BankedPoints == 0 &&
        PendingRewardPoints == 0;

    public void Serialize(IGenericWriter writer)
    {
        writer.WriteEncodedInt(0); // version
        writer.WriteEncodedInt(_cachedDeeds);
        writer.Write(LastBulkOrder);
        writer.Write(BankedPoints);
        writer.WriteEncodedInt(PendingRewardPoints);
    }

    public void Deserialize(IGenericReader reader)
    {
        reader.ReadEncodedInt(); // version
        CachedDeeds = reader.ReadEncodedInt();
        LastBulkOrder = reader.ReadDateTime();
        BankedPoints = reader.ReadDouble();
        PendingRewardPoints = reader.ReadEncodedInt();
    }
}

public class PendingBribe
{
    public PendingBribe(BaseBOD bod, int amount)
    {
        BOD = bod;
        Amount = amount;
    }

    public BaseBOD BOD { get; set; }
    public int Amount { get; set; }
}

/// <summary>A vendor's bribe state (ServUO keeps it on BaseVendor).</summary>
public class VendorBribeState
{
    // ServUO Config "Vendors.BribeDecayMinTime"/"MaxTime" defaults.
    public const int DecayMinDays = 25;
    public const int DecayMaxDays = 30;

    public int BribeMultiplier { get; set; }
    public DateTime NextMultiplierDecay { get; set; }
    public int RecentBribes { get; set; }

    // Not saved, as in ServUO.
    public DateTime WatchEnds { get; set; }
    public Dictionary<Mobile, PendingBribe> Bribes { get; } = new();

    public bool UnderWatch => WatchEnds > DateTime.MinValue;

    public void ResetDecay() =>
        NextMultiplierDecay = Core.Now + TimeSpan.FromDays(Utility.RandomMinMax(DecayMinDays, DecayMaxDays));

    /// <summary>ServUO halves the multiplier when its decay date has passed (BaseVendor.Serialize :2308-2317).</summary>
    public void CheckDecay()
    {
        if (NextMultiplierDecay != DateTime.MinValue && NextMultiplierDecay < Core.Now)
        {
            BribeMultiplier /= 2;
            ResetDecay();
        }
    }

    public void Serialize(IGenericWriter writer)
    {
        writer.WriteEncodedInt(0); // version
        writer.WriteEncodedInt(BribeMultiplier);
        writer.Write(NextMultiplierDecay);
        writer.WriteEncodedInt(RecentBribes);
    }

    public void Deserialize(IGenericReader reader)
    {
        reader.ReadEncodedInt(); // version
        BribeMultiplier = reader.ReadEncodedInt();
        NextMultiplierDecay = reader.ReadDateTime();
        RecentBribes = reader.ReadEncodedInt();
    }
}

/// <summary>The save file: every player's offers and points, and every vendor's bribe state.</summary>
public class BulkOrderStore : GenericPersistence
{
    // 0: cc-P122.
    public const int CurrentVersion = 0;

    private static BulkOrderStore _instance;

    public BulkOrderStore() : base("BulkOrderSystem", 10)
    {
    }

    public static void Configure()
    {
        _instance ??= new BulkOrderStore();
    }

    public static BulkOrderStore Instance => _instance;

    public override void Serialize(IGenericWriter writer)
    {
        writer.WriteEncodedInt(CurrentVersion);

        var players = BulkOrderSystem.Contexts.Where(p => !p.Key.Deleted).ToList();
        writer.WriteEncodedInt(players.Count);

        foreach (var (pm, context) in players)
        {
            writer.Write(pm);
            context.Serialize(writer);
        }

        var vendors = BulkOrderSystem.VendorStates.Where(p => !p.Key.Deleted).ToList();
        writer.WriteEncodedInt(vendors.Count);

        foreach (var (vendor, state) in vendors)
        {
            writer.Write(vendor);
            state.Serialize(writer);
        }
    }

    public override void Deserialize(IGenericReader reader)
    {
        var version = reader.ReadEncodedInt();

        if (version is < 0 or > CurrentVersion)
        {
            throw new InvalidDataException($"BulkOrderStore version {version} is not one this build reads.");
        }

        BulkOrderSystem.ClearContexts();
        BulkOrderSystem.ClearVendorStates();

        var count = reader.ReadEncodedInt();

        for (var i = 0; i < count; i++)
        {
            var pm = reader.ReadEntity<PlayerMobile>();
            var context = new BODContext();
            context.Deserialize(reader); // read in full either way

            if (pm is { Deleted: false })
            {
                BulkOrderSystem.SetContext(pm, context);
            }
        }

        count = reader.ReadEncodedInt();

        for (var i = 0; i < count; i++)
        {
            var vendor = reader.ReadEntity<BaseVendor>();
            var state = new VendorBribeState();
            state.Deserialize(reader);

            if (vendor is { Deleted: false })
            {
                BulkOrderSystem.SetVendorState(vendor, state);
            }
        }
    }
}
