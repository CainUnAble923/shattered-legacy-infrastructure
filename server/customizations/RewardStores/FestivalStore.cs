// cc-P760 (plan section 2): the Festival store. Festival Tokens, earned only while a season is on, spent at a Festival
// Peddler whose small market stall stands in New Haven only while Halloween, winter or spring is on.
//
// The seasons (FestivalSeasons): the shared seasonal switch's own windows. Halloween is ClusterFHalloweenDecor.Season
// (clusterf.seasonal.halloween.*), winter ClusterFWinterDecor.Season (clusterf.seasonal.winter.*), spring the switch named
// "spring" when one is registered (cc-P763 adds it, clusterf.seasonal.spring.*, 03-20 to 04-20), else the same window
// built in here. One season at a time, in that order.
//
// The currency (FestivalTokens): an account balance, not an item, so it cannot be lost on death or sold for gold. Kept in
// the account's achievement counters (saved with them, as the loot boxes' counters are): "festival.tokens.earned" only
// rises, "festival.tokens.spent" counts what the store took, and the balance is the difference.
//
// Earned only in season:
//   - a creature of 1,000 fame or more (not a pet, not a summon) drops 1 token to the player who dealt it the most damage
//     (a pet's or summon's master), at 10% plus 1% per 1,000 fame, at most 25%;
//   - each Treasures of Sorcerer's Dungeon artifact handed to the researcher: 2;
//   - the first login of each day: 1.
//
// The stall (FestivalMarket): the peddler, a counter, a crate and a signpost, placed when a season starts and taken away
// when it ends, on the world load and the hourly check, every piece tracked by serial in ClusterFSeasonalState (as the
// Halloween decor is) so take-down deletes exactly what it placed. Where: notes/cc-P760-review/placed.md.
// [FestivalMarket status|place|clear|season <halloween|winter|spring|none|auto>] for staff.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using ModernUO.CodeGeneratedEvents;
using ModernUO.Serialization;
using Server.Accounting;
using Server.Commands;
using Server.Engines.LeagueRewards;
using Server.Engines.Plants;
using Server.Gumps;
using Server.Items.Holiday;
using Server.Items;
using Server.Mobiles;

namespace Server.Engines.RewardStores;

public static class FestivalSeasons
{
    public const string Halloween = "halloween";
    public const string Winter = "winter";
    public const string Spring = "spring";

    public static readonly string[] All = [Halloween, Winter, Spring];

    /// <summary>The shard's local date, as the seasonal switch reads it. Tests replace it.</summary>
    public static Func<DateTime> Clock = () => Core.Now.ToLocalTime();

    /// <summary>Staff or a test: a season name forced on, "none" forced off, null to follow the dates.</summary>
    public static string? Forced { get; set; }

    // cc-P763's spring window (clusterf.seasonal.spring.*), used while no "spring" switch is registered.
    public static readonly SeasonWindow SpringFallback = SeasonWindow.Parse("03-20", "04-20");

    public static string? Current() => At(Clock());

    public static string? At(DateTime local)
    {
        if (Forced != null)
        {
            return Forced == "none" ? null : Forced;
        }

        if (ClusterFHalloweenDecor.Season.IsInSeason(local))
        {
            return Halloween;
        }

        if (ClusterFWinterDecor.Season.IsInSeason(local))
        {
            return Winter;
        }

        var spring = ClusterFSeasonalSwitch.All.GetValueOrDefault(Spring);
        return (spring?.IsInSeason(local) ?? SpringFallback.Contains(local)) ? Spring : null;
    }

    public static bool InSeason => Current() != null;

    public static string Label(string? season) => season switch
    {
        Halloween => "Halloween",
        Winter    => "Winter",
        Spring    => "Spring",
        _         => "no season"
    };
}

/// <summary>Festival Tokens: an account balance, earned only in season, spent at the Festival Peddler.</summary>
public sealed class FestivalTokens : IRewardCurrency
{
    public static readonly FestivalTokens Instance = new();

    public const string EarnedCounter = "festival.tokens.earned";
    public const string SpentCounter = "festival.tokens.spent";
    public const string LoginDayCounter = "festival.login.day";

    public const int FameFloor = 1000;
    public const int ArtifactTokens = 2;
    public const int DailyLoginTokens = 1;

    public string Name => "Festival Tokens";

    public static int Earned(string username) => ClusterFAchievementSystem.GetCounter(username, EarnedCounter);

    public static int Spent(string username) => ClusterFAchievementSystem.GetCounter(username, SpentCounter);

    public static int BalanceOf(string username) => Math.Max(0, Earned(username) - Spent(username));

    public int Balance(PlayerMobile pm) => pm.Account is IAccount acct ? BalanceOf(acct.Username) : 0;

    public bool TrySpend(PlayerMobile pm, int amount)
    {
        if (amount < 0 || pm.Account is not IAccount acct || BalanceOf(acct.Username) < amount)
        {
            return false;
        }

        ClusterFAchievementSystem.AddToCounter(acct.Username, SpentCounter, amount);
        return true;
    }

    /// <summary>Adds tokens to the account, and says so to the character. Returns the new balance.</summary>
    public static int Grant(PlayerMobile pm, int amount, string why)
    {
        if (amount <= 0 || pm.Account is not IAccount acct)
        {
            return 0;
        }

        ClusterFAchievementSystem.AddToCounter(acct.Username, EarnedCounter, amount);
        var balance = BalanceOf(acct.Username);
        pm.SendMessage(0x44, $"+{amount} Festival Token{(amount == 1 ? "" : "s")} ({why}). You have {balance:N0}.");
        return balance;
    }

    // -- Earning ------------------------------------------------------------------------------------------------------

    /// <summary>A creature's drop chance: none under 1,000 fame; 10% plus 1% per 1,000 fame, at most 25%.</summary>
    public static double DropChance(int fame) =>
        fame < FameFloor ? 0.0 : Math.Min(0.25, 0.10 + 0.01 * (fame / 1000));

    /// <summary>Who a creature's token goes to: the most damage dealt, a pet's or summon's master standing in.</summary>
    public static PlayerMobile? TopDamager(BaseCreature bc)
    {
        var top = bc.FindMostTotalDamager(false);
        if (top is BaseCreature pet)
        {
            top = pet.GetMaster();
        }

        return top as PlayerMobile;
    }

    /// <summary>The kill's roll, given: true when the top damager got a token.</summary>
    public static bool TryDrop(BaseCreature bc, double roll)
    {
        if (FestivalSeasons.Current() == null || bc.Controlled || bc.Summoned || roll >= DropChance(bc.Fame))
        {
            return false;
        }

        if (TopDamager(bc) is not { } pm)
        {
            return false;
        }

        Grant(pm, 1, $"{FestivalSeasons.Label(FestivalSeasons.Current())} festival");
        return true;
    }

    [OnEvent(nameof(CreatureEvents.CreatureDeathEvent))]
    public static void OnCreatureDeath(BaseCreature bc) => TryDrop(bc, Utility.RandomDouble());

    /// <summary>A Treasures of Sorcerer's Dungeon artifact handed in (SorcerersDungeonResearcher.AwardPoints).</summary>
    public static void OnSorcerersArtifact(PlayerMobile pm)
    {
        if (FestivalSeasons.Current() != null)
        {
            Grant(pm, ArtifactTokens, "an artifact handed in");
        }
    }

    public static int DayIndex(DateTime local) => (int)(local.Date - new DateTime(2000, 1, 1)).TotalDays;

    /// <summary>The first login of a day in season pays 1. Returns whether it paid.</summary>
    public static bool OnDailyLogin(PlayerMobile pm, DateTime local)
    {
        if (FestivalSeasons.At(local) == null || pm.Account is not IAccount acct)
        {
            return false;
        }

        var today = DayIndex(local);
        if (ClusterFAchievementSystem.GetCounter(acct.Username, LoginDayCounter) == today)
        {
            return false;
        }

        ClusterFAchievementSystem.AddToCounter(acct.Username, LoginDayCounter,
            today - ClusterFAchievementSystem.GetCounter(acct.Username, LoginDayCounter));
        Grant(pm, DailyLoginTokens, "the day's first login");
        return true;
    }

    [OnEvent(nameof(PlayerMobile.PlayerLoginEvent))]
    public static void OnLogin(PlayerMobile pm) => OnDailyLogin(pm, FestivalSeasons.Clock());
}

public sealed class FestivalStoreDef : RewardStoreDef
{
    public const int Range = 8;

    public FestivalStoreDef(Mobile? peddler) => Peddler = peddler;

    public Mobile? Peddler { get; }

    /// <summary>False only in tests: the store then sells away from the peddler.</summary>
    public bool RequirePeddler { get; init; } = true;

    public override string Key => "festival";

    public override string Title => "The Festival Peddler";

    public override string Blurb => $"{FestivalSeasons.Label(FestivalSeasons.Current())} wares, while the season lasts.";

    public override IRewardCurrency Currency => FestivalTokens.Instance;

    public override IReadOnlyList<RewardStoreRow> Rows() =>
        FestivalSeasons.Current() is { } season ? FestivalStock.For(season) : [];

    public override IReadOnlyList<RewardStoreRow> AllRows() => FestivalSeasons.All.SelectMany(FestivalStock.For).ToArray();

    public override int Progress(PlayerMobile pm) => 0;

    public override string OpensText(int opensAt) => "Open to all";

    public override string ProgressText(PlayerMobile pm) => FestivalSeasons.Label(FestivalSeasons.Current());

    public override string SpendNote => "Spending tokens never lowers the count you have earned.";

    public override bool CanShop(PlayerMobile pm, out string why)
    {
        if (FestivalSeasons.Current() == null)
        {
            why = "The festival is over. The peddler will be back next season.";
            return false;
        }

        if (RequirePeddler && (Peddler == null || Peddler.Deleted || Peddler.Map != pm.Map || !pm.InRange(Peddler, Range)))
        {
            why = "You must be at the Festival Peddler to buy.";
            return false;
        }

        why = "";
        return true;
    }

    public override RewardStoreDef Refresh() => new FestivalStoreDef(Peddler) { RequirePeddler = RequirePeddler };
}

public static class FestivalStock
{
    private static readonly Dictionary<string, RewardStoreRow[]> _rows = new();

    public static IReadOnlyList<RewardStoreRow> For(string season)
    {
        if (!_rows.TryGetValue(season, out var rows))
        {
            _rows[season] = rows = Build(season).ToArray();
        }

        return rows;
    }

    private static RewardStoreRow R(string season, string key, string label, int price, int itemID,
        Func<PlayerMobile, List<Item>> create, int hue = 0) =>
        new($"festival.{season}.{key}", label, price, 0, itemID, hue, create);

    private static Func<PlayerMobile, List<Item>> Decor(int itemID, string name) =>
        RewardStore.One(() => new RewardDecor(itemID, name));

    private static IEnumerable<RewardStoreRow> Build(string season)
    {
        var items = season switch
        {
            FestivalSeasons.Halloween => Halloween(),
            FestivalSeasons.Winter    => Winter(),
            _                         => Spring()
        };

        var list = items.ToList();
        foreach (var row in list)
        {
            yield return row;
        }

        // Every season: its title and a seasonal loot box drawing from that season's own rows.
        var titleKey = season switch
        {
            FestivalSeasons.Halloween => RewardStoreTitles.Halloween,
            FestivalSeasons.Winter    => RewardStoreTitles.Winter,
            _                         => RewardStoreTitles.Spring
        };
        yield return new RewardStoreRow($"festival.{season}.title", $"Title: {LeagueTitles.Display(titleKey)}", 40, 0,
            LeagueShop.TitleArt, 0, titleKey: titleKey);

        var label = FestivalSeasons.Label(season);
        yield return R(season, "box", $"{label} Festival loot box (3 draws)", 15, LeagueShop.BoxArt,
            pm => [MakeBox(pm, label, list)]);
    }

    public static Item MakeBox(PlayerMobile pm, string label, List<RewardStoreRow> rows)
    {
        var pool = rows.Where(r => r.Create != null).Select(r => (Func<PlayerMobile, Item>)(p => r.Create!(p)[0])).ToList();
        return LeagueShop.MakeBox(pm, $"{label} Festival Loot", 2, 3, pool, "the Festival Peddler");
    }

    private static IEnumerable<RewardStoreRow> Halloween()
    {
        const string s = FestivalSeasons.Halloween;
        yield return R(s, "treats", "Bag of treats (8)", 2, 0x469E, _ => [Treats()]);
        yield return R(s, "mask.clown", "Painted evil clown mask", 10, 0x4A90, RewardStore.One(() => new PaintedEvilClownMask()));
        yield return R(s, "mask.daemon", "Painted daemon mask", 10, 0x4A92, RewardStore.One(() => new PaintedDaemonMask()));
        yield return R(s, "mask.plague", "Painted plague mask", 10, 0x4A8E, RewardStore.One(() => new PaintedPlagueMask()));
        yield return R(s, "mask.jester", "Painted evil jester mask", 10, 0x4BA5, RewardStore.One(() => new PaintedEvilJesterMask()));
        yield return R(s, "mask.porcelain", "Painted porcelain mask", 10, 0x4BA7, RewardStore.One(() => new PaintedPorcelainMask()));
        yield return R(s, "pumpkin", "Halloween pumpkin", 10, 0xC6A, RewardStore.One(() => new HalloweenPumpkin()));
        yield return R(s, "webs", "Colored small webs", 10, 0x10D6, RewardStore.One(() => new ColoredSmallWebs()));
        yield return R(s, "skulls", "Skulls on a pike", 10, 0x42B5, RewardStore.One(() => new SkullsOnPike()));
        yield return R(s, "ghostchair", "Chair in a ghost costume", 10, 0x3F26, RewardStore.One(() => new ChairInAGhostCostume()));
        yield return R(s, "carving", "Pumpkin carving kit", 10, 0x992D, RewardStore.One(() => new PumpkinCarvingKit()));
        yield return R(s, "scarecrow", "Pumpkin scarecrow", 25, 0x469B, RewardStore.One(() => new PumpkinScarecrow()));
        yield return R(s, "guillotine", "Halloween guillotine", 25, 0x3F27, RewardStore.One(() => new HalloweenGuillotine()));
    }

    private static IEnumerable<RewardStoreRow> Winter()
    {
        const string s = FestivalSeasons.Winter;
        yield return R(s, "candycanes", "Candy canes (5)", 2, 0x2BDD, RewardStore.Bagged<CandyCane>("a bag of candy canes", 5));
        yield return R(s, "snowglobe", "Snow globe", 10, 0xE2F, RewardStore.One(() => new SnowGlobe()));
        yield return R(s, "snowman", "Snowman", 10, 0x2328, RewardStore.One(() => new Snowman()));
        yield return R(s, "wreath", "Holiday wreath (deed)", 10, 0x14F0, RewardStore.One(() => new WreathDeed()));
        yield return R(s, "rockinghorse", "Rocking horse", 10, 0x4214, RewardStore.One(() => new RockingHorse()));
        yield return R(s, "fireflies", "Fireflies (deed)", 10, 0x14F0, RewardStore.One(() => new FirefliesDeed()));
        yield return R(s, "tree", "Holiday tree (deed)", 25, 0x14F0, RewardStore.One(() => new HolidayTreeDeed()));
        // F-22's registered winter art (registry.csv 15424-15470: MrRiots, Vrark), as house decor.
        yield return R(s, "fatherchristmas", "Father Christmas statue (decor)", 25, 0x3C40, Decor(0x3C40, "a Father Christmas statue"));
        yield return R(s, "xmastree", "Decorated Christmas tree (decor)", 25, 0x3C52, Decor(0x3C52, "a decorated Christmas tree"));
        yield return R(s, "present.orange", "Present, orange (decor)", 10, 0x3C54, Decor(0x3C54, "a present"));
        yield return R(s, "present.pink", "Present, pink (decor)", 10, 0x3C55, Decor(0x3C55, "a present"));
        yield return R(s, "present.green", "Present, green (decor)", 10, 0x3C56, Decor(0x3C56, "a present"));
        yield return R(s, "gingerbread", "Gingerbread house (decor)", 10, 0x3C5F, Decor(0x3C5F, "a gingerbread house"));
        yield return R(s, "painting.a", "Holiday painting A (decor)", 10, 0x3C63, Decor(0x3C63, "a holiday painting"));
        yield return R(s, "painting.b", "Holiday painting B (decor)", 10, 0x3C65, Decor(0x3C65, "a holiday painting"));
    }

    private static IEnumerable<RewardStoreRow> Spring()
    {
        const string s = FestivalSeasons.Spring;
        yield return R(s, "chocolate", "Valentine chocolates (3)", 2, 2538, RewardStore.Bagged<ValentineChocolate>("a bag of chocolates", 3));
        yield return R(s, "seeds", "Plant seeds (5)", 2, 0xDCF, RewardStore.Bagged<Seed>("a bag of seeds", 5));
        yield return R(s, "garland", "Flower garland", 10, 0x2306, RewardStore.One(() => new FlowerGarland(0x2B)));
        yield return R(s, "pottedplant", "Potted plant", 10, 0x11CA, RewardStore.One(() => new PottedPlant()));
        yield return R(s, "heartbox", "Heart-shaped box", 10, 0x49CA, RewardStore.One(() => new HeartShapedBox()));
    }

    /// <summary>Halloween's treats: a bag of eight of pinned's Halloween candies.</summary>
    public static Item Treats()
    {
        var bag = new Bag { Name = "a bag of treats", Hue = 0x2B };
        bag.DropItem(new WrappedCandy(2));
        bag.DropItem(new Lollipops(2));
        bag.DropItem(new NougatSwirl(2));
        bag.DropItem(new Taffy(2));
        return bag;
    }
}

/// <summary>The Festival Peddler: a traveling vendor who stands at the stall in season. Double-click to shop.</summary>
[SerializationGenerator(0, false)]
public partial class FestivalPeddler : BaseCreature
{
    [Constructible]
    public FestivalPeddler() : base(AIType.AI_Animal, FightMode.None, 10, 1)
    {
        Name = "Tamsin Brightwick";
        Title = "the Festival Peddler";
        Body = 0x191;
        Hue = 0x83EA;
        Female = true;
        CantWalk = true;
        Blessed = true;

        SetStr(50);
        SetDex(50);
        SetInt(50);
        SetHits(100);

        AddItem(new FancyDress { Movable = false, Hue = 0x2B });
        AddItem(new Shoes { Movable = false, Hue = 0x455 });
        AddItem(new FeatheredHat { Movable = false, Hue = 0x2B });
        AddItem(new HalfApron { Movable = false, Hue = 0x8AB });
        AddItem(new Item(0x203C) { Movable = false, Hue = 0x45E, Layer = Layer.Hair });
    }

    public override bool IsInvulnerable => true;
    public override bool ClickTitle => true;
    public override bool ShowFameTitle => false;

    public override void OnDoubleClick(Mobile from)
    {
        if (from is not PlayerMobile pm)
        {
            return;
        }

        var store = new FestivalStoreDef(this);
        if (!store.CanShop(pm, out var why))
        {
            pm.SendMessage(0x22, why);
            return;
        }

        SayTo(pm, "Festival wares! Tokens only, friend; gold spends at the bank.");
        pm.SendGump(new RewardStoreGump(pm, store));
    }

    public override void OnSpeech(SpeechEventArgs e)
    {
        if (!e.Handled && e.Mobile is PlayerMobile pm && pm.InRange(this, 4) &&
            (e.Speech.Contains("buy", StringComparison.OrdinalIgnoreCase) ||
             e.Speech.Contains("festival", StringComparison.OrdinalIgnoreCase) ||
             e.Speech.Contains("token", StringComparison.OrdinalIgnoreCase)))
        {
            e.Handled = true;
            OnDoubleClick(pm);
            return;
        }

        base.OnSpeech(e);
    }
}

/// <summary>A piece of the Festival stall: fixed decor, placed and taken away with the season.</summary>
[SerializationGenerator(0, false)]
public partial class FestivalStallPiece : Item
{
    [Constructible]
    public FestivalStallPiece() : this(0xE3D, "a crate")
    {
    }

    public FestivalStallPiece(int itemID, string name) : base(itemID)
    {
        Name = name;
        Movable = false;
    }
}

/// <summary>The stall in New Haven, up in season and down out of it (the seasonal switch's pattern, by serial).</summary>
public static class FestivalMarket
{
    public const string StateName = "festival-peddler";
    public const string AnchorSetting = "clusterf.festival.peddlerAt";

    // The peddler's tile; the stall's pieces stand around it (Pieces). Chosen in cc-P760's review (placed.md).
    public const string DefaultAnchor = "3636,2627,0";

    public static Map Facet => Map.Trammel;

    public sealed record Piece(int Dx, int Dy, int Dz, int ItemID, string Name);

    // Relative to the peddler, who faces south: a long table in front of her (0x0B7C and 0x0B7D join east-west into
    // one, rendered in cc-P760's review), a crate and a barrel of stock behind her, and the signpost north-west of the
    // stall, on the north edge of the dirt path that runs into the stall's west side (y 2627), reading from the south
    // (0xA854) as the building guide asks. See notes/cc-P760-review/placed.md.
    public static readonly Piece[] Pieces =
    [
        new(0, 1, 0, 0x0B7C, "the Festival Peddler's table"),
        new(1, 1, 0, 0x0B7D, "the Festival Peddler's table"),
        new(-1, 0, 0, 0x0E3D, "a crate of festival wares"),
        new(1, 0, 0, 0x0E77, "a barrel of festival wares"),
        new(-2, -1, 0, 0xA854, "The Festival Peddler: festival wares for tokens")
    ];

    private static Point3D? _anchor;

    public static Point3D Anchor
    {
        get => _anchor ??= ParseAnchor(DefaultAnchor)!.Value;
        set => _anchor = value;
    }

    public static Point3D? ParseAnchor(string text)
    {
        var parts = (text ?? "").Split(',', StringSplitOptions.TrimEntries);
        return parts.Length == 3 && int.TryParse(parts[0], out var x) && int.TryParse(parts[1], out var y) &&
               int.TryParse(parts[2], out var z)
            ? new Point3D(x, y, z)
            : null;
    }

    // cc-P788 (isle release only): the stall's tile is New Haven on the swapped map, which the isle release does not
    // have, so on this branch the stall is not put up by itself at world load (no NPC, no error). Staff can still
    // [FestivalMarket place it anywhere for testing after setting the anchor; true here restores main's behaviour.
    public const string OnWorldLoadSetting = "clusterf.festival.stallOnWorldLoad";

    public static bool StallOnWorldLoad { get; private set; }

    public static void Configure()
    {
        StallOnWorldLoad = ServerConfiguration.GetOrUpdateSetting(OnWorldLoadSetting, false);
        var setting = ServerConfiguration.GetOrUpdateSetting(AnchorSetting, DefaultAnchor);
        if (ParseAnchor(setting) is { } at)
        {
            Anchor = at;
        }
        else
        {
            Console.WriteLine($"[FestivalMarket] {AnchorSetting} = '{setting}' is not x,y,z; using {DefaultAnchor}.");
        }

        CommandSystem.Register("FestivalMarket", AccessLevel.GameMaster, OnCommand);

        if (!StallOnWorldLoad)
        {
            return;
        }

        EventSink.WorldLoad += () => Timer.DelayCall(TimeSpan.Zero, () =>
        {
            foreach (var line in Sync())
            {
                Console.WriteLine(line);
            }

            Timer.DelayCall(TimeSpan.FromHours(1), TimeSpan.FromHours(1), () =>
            {
                foreach (var line in Sync())
                {
                    Console.WriteLine(line);
                }
            });
        });
    }

    [Usage("FestivalMarket [status|place|clear|season <halloween|winter|spring|none|auto>]")]
    [Description("The Festival Peddler's stall in New Haven (cc-P760). status says the season and whether the stall is up. place puts up what is missing of it (any time, for testing); clear removes exactly what it placed, by serial. season forces a festival season on or off for testing (auto follows the dates again) and brings the stall up or down to match.")]
    [ShardCommand(CommandCategory.WorldSetup, Rerun = CommandRerun.Skips, Shard = CommandShard.TestFirst, NoDryRun = true, Summary = "Puts up (or clears) the Festival Peddler's stall in New Haven. It goes up by itself while Halloween, winter or spring is on and comes down after; clear removes only what it placed, by serial.")]
    private static void OnCommand(CommandEventArgs e)
    {
        foreach (var line in Run(e.Arguments))
        {
            e.Mobile.SendMessage(line);
        }
    }

    public static List<string> Run(string[] args)
    {
        var verb = args.Length > 0 ? args[0].ToLowerInvariant() : "status";
        switch (verb)
        {
            case "place":
                return Up();
            case "clear":
                return Down();
            case "season":
                var s = args.Length > 1 ? args[1].ToLowerInvariant() : "auto";
                FestivalSeasons.Forced = s == "auto" ? null : s is "none" || FestivalSeasons.All.Contains(s) ? s : FestivalSeasons.Forced;
                var lines = new List<string> { $"Festival season: {FestivalSeasons.Label(FestivalSeasons.Current())} ({(FestivalSeasons.Forced == null ? "by the dates" : "forced by staff")})." };
                lines.AddRange(Sync());
                return lines;
            default:
                return
                [
                    $"Festival: {FestivalSeasons.Label(FestivalSeasons.Current())}; the stall is {(IsUp ? "up" : "down")} at {Anchor} ({Placed().Count} pieces).",
                    "[FestivalMarket status|place|clear|season <halloween|winter|spring|none|auto>]"
                ];
        }
    }

    public static HashSet<Serial> Placed() => ClusterFSeasonalState.Placed(StateName);

    public static bool IsUp => Placed().Any(s => World.FindEntity(s) is { Deleted: false });

    public static FestivalPeddler? Peddler =>
        Placed().Select(s => World.FindMobile(s)).OfType<FestivalPeddler>().FirstOrDefault(p => !p.Deleted);

    /// <summary>
    /// The hourly and world-load check: up in season, down out of it, acting only when the season state changed since the
    /// last check (so a staff place or clear stands until then). A world first seen in season is brought up.
    /// </summary>
    public static List<string> Sync()
    {
        var season = FestivalSeasons.Current() != null;
        var last = ClusterFSeasonalState.GetLast(StateName);
        ClusterFSeasonalState.SetLast(StateName, season);

        if (last == season)
        {
            return [];
        }

        if (season)
        {
            return Up();
        }

        return last == null ? [] : Down();
    }

    /// <summary>Places what is missing of the stall. Place twice places once.</summary>
    public static List<string> Up()
    {
        var placed = Placed();
        placed.RemoveWhere(s => World.FindEntity(s) is not { Deleted: false });

        if (placed.Count > 0)
        {
            return [$"[FestivalMarket] The stall is already up ({placed.Count} pieces)."];
        }

        var at = Anchor;
        foreach (var p in Pieces)
        {
            var piece = new FestivalStallPiece(p.ItemID, p.Name);
            piece.MoveToWorld(new Point3D(at.X + p.Dx, at.Y + p.Dy, at.Z + p.Dz), Facet);
            placed.Add(piece.Serial);
        }

        var peddler = new FestivalPeddler { Direction = Direction.South, Home = at, RangeHome = 0 };
        peddler.MoveToWorld(at, Facet);
        placed.Add(peddler.Serial);

        return [$"[FestivalMarket] The Festival Peddler's stall is up at {at} ({placed.Count} pieces)."];
    }

    /// <summary>Takes away exactly what Up placed (by serial).</summary>
    public static List<string> Down()
    {
        var placed = Placed();
        var n = 0;
        foreach (var serial in placed.ToList())
        {
            if (World.FindEntity(serial) is { Deleted: false } e)
            {
                e.Delete();
                n++;
            }
        }

        placed.Clear();
        return [$"[FestivalMarket] The Festival Peddler's stall is down ({n} pieces taken away)."];
    }
}
