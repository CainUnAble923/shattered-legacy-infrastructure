// cc-P760 (plan section 1): the guild and society stores. Each of the 18 guilds sells its own goods at its guildmaster, for
// its own points.
//
// The currency is the points every guild already keeps per character (CharacterGuildData.GuildCurrency, shown in the
// guild menus by MuleUpgrades.CurrencyName). Spending takes from that balance only (CharacterGuildData.SpendCurrency):
// standing, and so rank, never moves; a spent counter beside it (the account's achievement counters,
// "guildstore.spent.<serial>.<guild>") records what the stores took. The Custodians keep no scrip since cc-P33 retired
// Civic Tokens (ClusterFCustodianSystem.RetireCivicTokens clears it at every load), so their store spends the character's
// Clean Up Britannia points, whose lifetime total (their standing) a spend never touches.
//
// Earning (GuildPointEarning below): the six deed-giving guilds other than the Smiths now pay 1 guild point per 50 bulk
// order points a deed is worth (BulkOrderVendors.TurnIn), and every requisition pays its guild's member 1 point per 10
// standing (RequisitionSystem.TurnIn). Everything that paid points before still does.
//
// Where: a Guild Store button in the guild's hall window (GuildTaskDetailGump), "Guild Store" on the guildmaster's context
// menu, and saying "guild store" to the guildmaster, for members at the guildmaster. No new NPC.
//
// Rows open by rank index (ClusterFGuildSystem.GuildRankIndex: 0 Initiate to 5 top rank); the price table sets each.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Server.Accounting;
using Server.Engines.LeagueRewards;
using Server.Engines.Points;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;

namespace Server.Engines.RewardStores;

/// <summary>A guild's points: CharacterGuildData.GuildCurrency, this character's.</summary>
public sealed class GuildPointsCurrency : IRewardCurrency
{
    public GuildPointsCurrency(string guildKey) => GuildKey = guildKey;

    public string GuildKey { get; }

    public string Name => MuleUpgrades.CurrencyName(GuildKey);

    public int Balance(PlayerMobile pm) => ClusterFAccountPersistence.GetGuild(pm)?.GetCurrency(GuildKey) ?? 0;

    public bool TrySpend(PlayerMobile pm, int amount)
    {
        if (amount < 0 || pm.Account is not IAccount acct)
        {
            return false;
        }

        if (!ClusterFAccountPersistence.GetOrCreateGuild(pm).SpendCurrency(GuildKey, amount))
        {
            return false;
        }

        ClusterFAchievementSystem.AddToCounter(acct.Username, SpentCounter(pm, GuildKey), amount);
        return true;
    }

    public static string SpentCounter(Mobile m, string guildKey) => $"guildstore.spent.{(uint)m.Serial}.{guildKey}";
}

/// <summary>The Custodians' store currency: Clean Up Britannia points (the balance; the lifetime total is their standing).</summary>
public sealed class CleanUpPointsCurrency : IRewardCurrency
{
    public string Name => "Clean Up points";

    public int Balance(PlayerMobile pm) => (int)Math.Floor(CleanUpBritanniaData.Instance.GetPoints(pm));

    public bool TrySpend(PlayerMobile pm, int amount)
    {
        if (amount < 0 || Balance(pm) < amount || pm.Account is not IAccount acct)
        {
            return false;
        }

        if (!CleanUpBritanniaData.Instance.DeductPoints(pm, amount))
        {
            return false;
        }

        ClusterFAchievementSystem.AddToCounter(acct.Username, GuildPointsCurrency.SpentCounter(pm, GuildStores.Custodians), amount);
        return true;
    }
}

public sealed class GuildStoreDef : RewardStoreDef
{
    public GuildStoreDef(string guildKey, Mobile? guildmaster)
    {
        GuildKey = guildKey;
        Guildmaster = guildmaster;
        Currency = guildKey == GuildStores.Custodians ? new CleanUpPointsCurrency() : new GuildPointsCurrency(guildKey);
    }

    public string GuildKey { get; }

    public Mobile? Guildmaster { get; }

    /// <summary>False only in tests: the store then sells away from the guildmaster.</summary>
    public bool RequireGuildmaster { get; init; } = true;

    public string GuildName => ClusterFGuildSystem.GetDef(GuildKey)?.Name ?? GuildKey;

    public override string Key => $"guild.{GuildKey}";

    public override string Title => GuildName;

    public override string Blurb => "Guild Store. Your rank opens a row.";

    public override IRewardCurrency Currency { get; }

    public override IReadOnlyList<RewardStoreRow> Rows() => GuildStores.RowsFor(GuildKey);

    public override int Progress(PlayerMobile pm) =>
        ClusterFAccountPersistence.GetGuild(pm) is { } data ? ClusterFGuildSystem.GuildRankIndex(GuildKey, data) : -1;

    public override string OpensText(int opensAt) =>
        opensAt <= 0 ? "Members" : $"Requires {ClusterFGuildSystem.RankIndexName(opensAt)} rank";

    public override string ProgressText(PlayerMobile pm) =>
        ClusterFAccountPersistence.GetGuild(pm) is { } data ? ClusterFGuildSystem.GetRankName(GuildKey, data) : "not a member";

    public override string SpendNote => "Spending points never lowers your standing or rank.";

    public override bool CanShop(PlayerMobile pm, out string why)
    {
        if (!ClusterFGuildSystem.IsJoined(pm, GuildKey))
        {
            why = $"The {GuildName}'s store is for members.";
            return false;
        }

        if (RequireGuildmaster && !ClusterFGuildSystem.IsNearGuildmaster(pm, Guildmaster!))
        {
            why = "You must be at the guildmaster to buy.";
            return false;
        }

        why = "";
        return true;
    }

    public override RewardStoreDef Refresh() => new GuildStoreDef(GuildKey, Guildmaster) { RequireGuildmaster = RequireGuildmaster };
}

public static class GuildStores
{
    public const string Custodians = "custodians";

    // The craft guilds and their craft's tool, next-tier material and runic tool (the plan: "where stock has one").
    // Rank indexes: 0 Initiate, 1 Apprentice, 2 Journeyman, 3 Master, 4 Grandmaster, 5 top rank.

    /// <summary>Every guild's store, without a guildmaster: the price table's list and the tests'.</summary>
    public static IEnumerable<GuildStoreDef> All()
    {
        ClusterFGuildSystem.EnsureRegistered();
        return ClusterFGuildSystem.AllGuilds.Keys.OrderBy(k => k, StringComparer.Ordinal).Select(k => new GuildStoreDef(k, null));
    }

    private static readonly Dictionary<string, RewardStoreRow[]> _rows = new(StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<RewardStoreRow> RowsFor(string guildKey)
    {
        if (!_rows.TryGetValue(guildKey, out var rows))
        {
            _rows[guildKey] = rows = Build(guildKey).ToArray();
        }

        return rows;
    }

    private static RewardStoreRow R(string guild, string key, string label, int price, int opens, int itemID, int hue,
        Func<PlayerMobile, List<Item>> create) =>
        new($"guild.{guild}.{key}", label, price, opens, itemID, hue, create);

    private static IEnumerable<RewardStoreRow> Build(string g)
    {
        var name = ClusterFGuildSystem.GetDef(g)?.Name ?? g;
        var hue = GuildMenuThemes.Get(g).AccentHue;

        // Every guild: its title (top rank) and a cloak in the guild's colour.
        if (RewardStoreTitles.GuildTitleNames.TryGetValue(g, out var title))
        {
            yield return new RewardStoreRow($"guild.{g}.title", $"Title: {title}", 2000, ClusterFGuildSystem.RankTop,
                LeagueShop.TitleArt, hue, titleKey: RewardStoreTitles.GuildTitleKey(g));
        }

        yield return R(g, "cloak", $"{name} cloak", 300, ClusterFGuildSystem.RankJourneyman, 0x1515, hue,
            RewardStore.One(() => new Cloak(hue) { Name = $"a {name} cloak" }));

        foreach (var row in Stock(g, name, hue))
        {
            yield return row;
        }
    }

    private static IEnumerable<RewardStoreRow> Stock(string g, string name, int hue)
    {
        switch (g)
        {
            case "smithing":
                yield return R(g, "tools", "Smith's hammers (3, 50 uses each)", 50, 0, 0x13E3, 0, Bundle<SmithHammer>("a bag of smith's hammers"));
                yield return R(g, "material", "Dull copper ingots (50)", 150, 1, 0x1BF2, CraftResources.GetHue(CraftResource.DullCopper), RewardStore.Stack<DullCopperIngot>(50));
                yield return R(g, "runic", "Runic hammer, dull copper (50 uses)", 1500, 3, 0x13E3, CraftResources.GetHue(CraftResource.DullCopper),
                    RewardStore.One(() => new RunicHammer(CraftResource.DullCopper, 50)));
                yield return R(g, "banner", "Society of Smiths banner (decor)", 600, 3, 0x15AE, hue,
                    RewardStore.One(() => new RewardDecor(0x15AE, "a Society of Smiths banner", hue)));
                break;
            case "tailoring":
                yield return R(g, "tools", "Sewing kits (3, 50 uses each)", 50, 0, 0xF9D, 0, Bundle<SewingKit>("a bag of sewing kits"));
                yield return R(g, "cloth", "Cloth (50)", 50, 0, 0x1766, 0, RewardStore.Stack<Cloth>(50));
                yield return R(g, "material", "Spined leather (50)", 150, 1, 0x1081, CraftResources.GetHue(CraftResource.SpinedLeather), RewardStore.Stack<SpinedLeather>(50));
                yield return R(g, "dressform", "Dress form (decor)", 400, 2, 0xEC6, 0, RewardStore.One(() => new Dressform()));
                yield return R(g, "runic", "Runic sewing kit, spined (50 uses)", 1500, 3, 0xF9D, CraftResources.GetHue(CraftResource.SpinedLeather),
                    RewardStore.One(() => new RunicSewingKit(CraftResource.SpinedLeather, 50)));
                break;
            case "tinkers":
                yield return R(g, "tools", "Tinker's tools (3, 50 uses each)", 50, 0, 0x1EB8, 0, Bundle<TinkerTools>("a bag of tinker's tools"));
                yield return R(g, "gears", "Gears (20)", 50, 0, 0x1053, 0, RewardStore.Stack<Gears>(20));
                yield return R(g, "springs", "Springs (20)", 50, 0, 0x105D, 0, RewardStore.Stack<Springs>(20));
                yield return R(g, "material", "Dull copper ingots (50)", 150, 1, 0x1BF2, CraftResources.GetHue(CraftResource.DullCopper), RewardStore.Stack<DullCopperIngot>(50));
                yield return R(g, "clock", "Clock (decor)", 400, 2, 0x104B, 0, RewardStore.One(() => new Clock()));
                break;
            case "foresters":
                yield return R(g, "tools", "Hatchets (3)", 50, 0, 0xF43, 0, Bundle<Hatchet>("a bag of hatchets"));
                yield return R(g, "saws", "Dovetail saws (3, 50 uses each)", 50, 0, 0x1028, 0, Bundle<DovetailSaw>("a bag of dovetail saws"));
                yield return R(g, "material", "Oak boards (50)", 150, 1, 0x1BD7, CraftResources.GetHue(CraftResource.OakWood), RewardStore.Stack<OakBoard>(50));
                yield return R(g, "pottedtree", "Potted tree (decor)", 400, 2, 0x11C8, 0, RewardStore.One(() => new PottedTree()));
                yield return R(g, "runic.saw", "Runic dovetail saw, oak (50 uses)", 1500, 3, 0x1028, CraftResources.GetHue(CraftResource.OakWood),
                    RewardStore.One(() => new RunicDovetailSaw(CraftResource.OakWood, 50)));
                yield return R(g, "runic.fletcher", "Runic fletcher's tool, oak (50 uses)", 1500, 3, 0x1022, CraftResources.GetHue(CraftResource.OakWood),
                    RewardStore.One(() => new RunicFletcherTool(CraftResource.OakWood, 50)));
                break;
            case "alembic":
                yield return R(g, "tools", "Mortars and pestles (3, 50 uses each)", 50, 0, 0xE9B, 0, Bundle<MortarPestle>("a bag of mortars and pestles"));
                yield return R(g, "bottles", "Empty bottles (50)", 50, 0, 0xF0E, 0, RewardStore.Stack<Bottle>(50));
                yield return R(g, "reagents", "Bag of reagents (50 each)", 150, 1, 0xE76, 0, RewardStore.One(() => new BagOfReagents()));
                for (var i = 0; i < 8; i++)
                {
                    var id = 0x6DBB + i; // cc-P265's partial-hue flasks (registry.csv 28091-28098, Ems)
                    yield return R(g, $"flask.{i + 1}", $"Partial-hue flask {i + 1} (decor)", 400, 2, id, 0,
                        RewardStore.One(() => new RewardDecor(id, "a flask")));
                }

                break;
            case "arcane":
                yield return R(g, "tools", "Scribe's pens (3, 50 uses each)", 50, 0, 0xFBF, 0, Bundle<ScribesPen>("a bag of scribe's pens"));
                yield return R(g, "scrolls", "Blank scrolls (50)", 50, 0, 0xEF3, 0, RewardStore.Stack<BlankScroll>(50));
                yield return R(g, "reagents", "Bag of reagents (50 each)", 150, 1, 0xE76, 0, RewardStore.One(() => new BagOfReagents()));
                yield return R(g, "crystalball", "Crystal ball (decor)", 600, 3, 0x6DC4, 0, // cc-P265, RedRooster
                    RewardStore.One(() => new RewardDecor(0x6DC4, "a crystal ball")));
                break;
            case "mining":
                yield return R(g, "prospector", "Prospector's tool", 50, 0, 0xFB4, 0, RewardStore.One(() => new ProspectorsTool()));
                yield return R(g, "shovels", "Shovels (3, 50 uses each)", 50, 0, 0xF39, 0, Bundle<Shovel>("a bag of shovels"));
                yield return R(g, "gems", "Pouch of gems (10)", 150, 1, 0xE79, 0, _ => [GemPouch()]);
                break;
            case "rangers":
                yield return R(g, "arrows", "Arrows (200)", 50, 0, 0xF3F, 0, RewardStore.Stack<Arrow>(200));
                yield return R(g, "bolts", "Crossbow bolts (200)", 50, 0, 0x1BFB, 0, RewardStore.Stack<Bolt>(200));
                yield return R(g, "fletcher", "Fletcher's tools (3, 50 uses each)", 50, 0, 0x1022, 0, Bundle<FletcherTools>("a bag of fletcher's tools"));
                yield return R(g, "feathers", "Feathers (100)", 100, 1, 0x1BD1, 0, RewardStore.Stack<Feather>(100));
                break;
            case "artificers":
                yield return R(g, "residue", "Magical residue (20)", 100, 0, 0x2DB1, 0, RewardStore.Stack<MagicalResidue>(20));
                yield return R(g, "essence", "Enchanted essence (10)", 150, 1, 0x2DB2, 0, RewardStore.Stack<EnchantedEssence>(10));
                yield return R(g, "relic", "Relic fragments (5)", 150, 2, 0x2DB3, 0, RewardStore.Stack<RelicFragment>(5));
                yield return R(g, "crystal", "Floating crystal, green (decor)", 800, 3, 0x6DD8, 0, // cc-P265, MrRiots
                    RewardStore.One(() => new RewardDecor(0x6DD8, "a floating crystal")));
                break;
            case "healers":
                yield return R(g, "bandages", "Bandages (100)", 50, 0, 0xE21, 0, RewardStore.Stack<Bandage>(100));
                yield return R(g, "heal", "Greater heal potions (5)", 100, 1, 0xF0C, 0, Bundle<GreaterHealPotion>("a bag of heal potions", 5));
                yield return R(g, "cure", "Greater cure potions (5)", 100, 1, 0xF07, 0, Bundle<GreaterCurePotion>("a bag of cure potions", 5));
                yield return R(g, "kit", "Healer's kit (bag: 50 bandages, 2 heal, 2 cure)", 150, 2, 0xE76, 0, _ => [HealersKit()]);
                break;
            case "bards":
                yield return R(g, "lute", "Lute", 100, 0, 0xEB3, 0, RewardStore.One(() => new Lute()));
                yield return R(g, "harp", "Lap harp", 100, 0, 0xEB2, 0, RewardStore.One(() => new LapHarp()));
                yield return R(g, "drum", "Drum", 100, 0, 0xE9C, 0, RewardStore.One(() => new Drums()));
                yield return R(g, "stand", "Music stand (decor)", 400, 2, 0x0EB5, 0, RewardStore.One(() => new RewardDecor(0x0EB5, "a music stand")));
                break;
            case "warriors":
                yield return R(g, "dummy", "Training dummy (deed)", 600, 2, 0x14F0, 0, RewardStore.One(() => new TrainingDummyEastDeed()));
                yield return R(g, "repair", "Weapon repair deed (smithing 90)", 100, 1, 0x14F0, 0x1BC,
                    RewardStore.One(() => new RepairDeed(RepairDeed.RepairSkillType.Smithing, 90.0)));
                yield return R(g, "bandages", "Bandages (100)", 50, 0, 0xE21, 0, RewardStore.Stack<Bandage>(100));
                break;
            case "maritime":
                yield return R(g, "bait", "Fish bait, a random fish (20 uses)", 50, 0, 2454, 0,
                    RewardStore.One(() => new Bait(Utility.Random(FishInfo.FishInfos.Count)) { UsesRemaining = 20 }));
                yield return R(g, "pole", "Fishing pole", 50, 0, 0xDC0, 0, RewardStore.One(() => new FishingPole()));
                yield return R(g, "sextant", "Sextant", 100, 1, 0x1057, 0, RewardStore.One(() => new Sextant()));
                yield return R(g, "spyglass", "Spyglass", 100, 1, 0x14F5, 0, RewardStore.One(() => new Spyglass()));
                yield return R(g, "rope", "Coil of rope (decor)", 400, 2, 0x14F8, 0, RewardStore.One(() => new Rope()));
                break;
            case "merchants":
                yield return R(g, "deeds", "Commodity deeds (5)", 100, 0, 0x14F0, 0, Bundle<CommodityDeed>("a bag of commodity deeds", 5));
                yield return R(g, "scales", "Scales (decor)", 400, 2, 0x1852, 0, RewardStore.One(() => new Scales()));
                yield return R(g, "sign", "Shop sign (decor)", 400, 2, 0xBD2, 0, RewardStore.One(() => new RewardDecor(0xBD2, "a merchant's sign")));
                break;
            case "thieves":
                yield return R(g, "lockpicks", "Lockpicks (20)", 50, 0, 0x14FC, 0, RewardStore.Stack<Lockpick>(20));
                yield return R(g, "lockpicks.50", "Lockpicks (50)", 100, 1, 0x14FC, 0, RewardStore.Stack<Lockpick>(50));
                yield return R(g, "disguise", "Disguise kit", 150, 2, 0xE05, 0, RewardStore.One(() => new DisguiseKit()));
                break;
            case "dojo":
                yield return R(g, "tatami", "Tatami mat (decor)", 400, 2, 0x29CA, 0, RewardStore.One(() => new RewardDecor(0x29CA, "a tatami mat")));
                yield return R(g, "bonsai", "Bonsai tree (decor)", 600, 3, 0x28DC, 0, RewardStore.One(() => new RewardDecor(0x28DC, "a bonsai tree")));
                yield return R(g, "bandages", "Bandages (100)", 50, 0, 0xE21, 0, RewardStore.Stack<Bandage>(100));
                break;
            case "keepers":
                yield return R(g, "gravedust", "Grave dust (50)", 50, 0, 0xF8F, 0, RewardStore.Stack<GraveDust>(50));
                yield return R(g, "necroreagents", "Bag of necromancer reagents (50 each)", 150, 1, 0xE76, 0, RewardStore.One(() => new BagOfNecroReagents()));
                yield return R(g, "candles", "Candles (5)", 50, 0, 0xA28, 0, Bundle<Candle>("a bag of candles", 5));
                break;
            case Custodians:
                yield return R(g, "barrel", "Trash barrel", 400, 2, 0xE77, 0, RewardStore.One(() => new TrashBarrel()));
                yield return R(g, "broom", "Custodian's broom (decor)", 400, 2, 0xB027, 0, // stock art "Broom Yellow South"
                    RewardStore.One(() => new RewardDecor(0xB027, "a custodian's broom")));
                yield return R(g, "cloth", "Cloth (50)", 50, 0, 0x1766, 0, RewardStore.Stack<Cloth>(50));
                break;
        }
    }

    private static Func<PlayerMobile, List<Item>> Bundle<T>(string bagName, int count = 3) where T : Item =>
        RewardStore.Bagged<T>(bagName, count);

    /// <summary>The Miners' gem pouch: a pouch of ten stock gems, two of five kinds.</summary>
    public static Item GemPouch()
    {
        var pouch = new Pouch { Name = "a pouch of gems" };
        pouch.DropItem(new Amber(2));
        pouch.DropItem(new Citrine(2));
        pouch.DropItem(new Ruby(2));
        pouch.DropItem(new Sapphire(2));
        pouch.DropItem(new Emerald(2));
        return pouch;
    }

    /// <summary>The Healers' kit: a bag of 50 bandages, two greater heal and two greater cure potions.</summary>
    public static Item HealersKit()
    {
        var bag = new Bag { Name = "a healer's kit" };
        bag.DropItem(new Bandage(50));
        bag.DropItem(new GreaterHealPotion());
        bag.DropItem(new GreaterHealPotion());
        bag.DropItem(new GreaterCurePotion());
        bag.DropItem(new GreaterCurePotion());
        return bag;
    }

    /// <summary>Opens a guild's store for a member at the guildmaster; says why not otherwise.</summary>
    public static void Open(PlayerMobile pm, string guildKey, Mobile? guildmaster)
    {
        var store = new GuildStoreDef(guildKey, guildmaster);
        if (!store.CanShop(pm, out var why))
        {
            pm.SendMessage(0x22, why);
            return;
        }

        pm.SendGump(new RewardStoreGump(pm, store));
    }
}

/// <summary>
/// cc-P760: guild points from work that paid none (plan section 1). The six deed-giving guilds other than the Smiths pay
/// 1 point per 50 bulk order points; a requisition pays its guild's member 1 point per 10 standing.
/// </summary>
public static class GuildPointEarning
{
    public const int BodPointsPerGuildPoint = 50;
    public const int StandingPerGuildPoint = 10;

    /// <summary>What a deed worth this many bulk order points pays a member: at least 1 for a deed worth any.</summary>
    public static int ForBodPoints(double bodPoints) =>
        bodPoints <= 0 ? 0 : Math.Max(1, (int)(bodPoints / BodPointsPerGuildPoint));

    public static int ForStanding(int standing) => standing <= 0 ? 0 : Math.Max(1, standing / StandingPerGuildPoint);

    /// <summary>
    /// A deed turned in at a stock-rules turn-in (BulkOrderVendors.TurnIn). Pays the deed's guild's member (not the Smiths:
    /// the Society pays Seals its own way). Returns what it paid.
    /// </summary>
    public static int OnBodTurnIn(PlayerMobile pm, string? guildKey, double bodPoints)
    {
        if (guildKey == null || guildKey == "smithing" || !ClusterFGuildSystem.IsJoined(pm, guildKey))
        {
            return 0;
        }

        var points = ForBodPoints(bodPoints);
        if (points > 0)
        {
            ClusterFAccountPersistence.GetOrCreateGuild(pm).AddCurrency(guildKey, points);
            pm.SendMessage(0x44, $"+{points} {MuleUpgrades.CurrencyName(guildKey)}.");
        }

        return points;
    }

    /// <summary>A requisition filled (RequisitionSystem.TurnIn): its guild's member gets points. Returns what it paid.</summary>
    public static int OnRequisition(PlayerMobile pm, string guildKey, int standing)
    {
        // The Custodians keep no scrip (cc-P33); their store spends Clean Up points.
        if (guildKey == GuildStores.Custodians || !ClusterFGuildSystem.IsJoined(pm, guildKey))
        {
            return 0;
        }

        var points = ForStanding(standing);
        if (points > 0)
        {
            ClusterFAccountPersistence.GetOrCreateGuild(pm).AddCurrency(guildKey, points);
        }

        return points;
    }
}
