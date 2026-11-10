// cc-P760 (plan section 3): the Achievement store. Achievement points buy titles, cosmetics and decor.
//
// The currency is AP to spend: the account's AP total (ClusterFAccountData.AchievementPoints, every earned achievement's
// AP) less what this store has taken, kept as a spent counter beside it (the account's achievement counter "ap.spent").
// The AP total itself never drops: it still opens League shop rows and the AP titles, and rows here open by it too.
//
// Where: the "AP Store" button in the achievements window's footer, and "Achievement Store" in the League Registrar's
// menu. No new NPC.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Server.Accounting;
using Server.Engines.LeagueRewards;
using Server.Gumps;
using Server.Items;
using Server.LootBoxes;
using Server.Mobiles;

namespace Server.Engines.RewardStores;

/// <summary>AP to spend: the AP total less the spent counter. Spending never lowers the AP total.</summary>
public sealed class SpendableAp : IRewardCurrency
{
    public static readonly SpendableAp Instance = new();

    public const string SpentCounter = "ap.spent";

    public string Name => "AP";

    public static int Spent(string username) => ClusterFAchievementSystem.GetCounter(username, SpentCounter);

    public int Balance(PlayerMobile pm) =>
        pm.Account is IAccount acct && RewardStore.DataOf(pm) is { } data
            ? Math.Max(0, data.AchievementPoints - Spent(acct.Username))
            : 0;

    public bool TrySpend(PlayerMobile pm, int amount)
    {
        if (amount < 0 || pm.Account is not IAccount acct || Balance(pm) < amount)
        {
            return false;
        }

        ClusterFAchievementSystem.AddToCounter(acct.Username, SpentCounter, amount);
        return true;
    }
}

public sealed class AchievementStoreDef : RewardStoreDef
{
    public override string Key => "achievement";

    public override string Title => "The Achievement Store";

    public override string Blurb => "Your AP total opens a row and never drops.";

    public override IRewardCurrency Currency => SpendableAp.Instance;

    public override IReadOnlyList<RewardStoreRow> Rows() => AchievementStock.Rows;

    public override int Progress(PlayerMobile pm) => RewardStore.DataOf(pm)?.AchievementPoints ?? 0;

    public override string OpensText(int opensAt) => opensAt <= 0 ? "Open to all" : $"Requires {opensAt:N0} AP total";

    public override string ProgressText(PlayerMobile pm) => $"{Progress(pm):N0} AP total";

    public override string SpendNote => "Spending AP never lowers your AP total or League rank.";

    public static void Open(PlayerMobile pm) => pm.SendGump(new RewardStoreGump(pm, new AchievementStoreDef()));
}

public static class AchievementStock
{
    // The 12 Odin paintings (cc-P237, registry.csv 15518-15529), public-domain works; community art, credit Odin.
    public static readonly (int Id, string Name)[] Paintings =
    [
        (0x3C9E, "Beata Beatrix"), (0x3C9F, "Morgan le Fay"), (0x3CA0, "Ophelia"),
        (0x3CA1, "Sir Galahad at the Ruined Chapel"), (0x3CA2, "The Fairy Feller's Master-Stroke"),
        (0x3CA3, "The Garden of the Hesperides"), (0x3CA4, "The Lady of Shalott"), (0x3CA5, "The Love Song"),
        (0x3CA6, "The Tune of the Seven Towers"), (0x3CA7, "Wars of the Roses"), (0x3CA8, "The Nightmare"),
        (0x3CA9, "The Magic Circle")
    ];

    public static readonly (string Key, string Name, int Hue)[] SashHues =
    [
        ("gold", "gold", 0x8A5), ("crimson", "crimson", 0x26), ("azure", "azure", 0x59), ("emerald", "emerald", 0x3F),
        ("violet", "violet", 0x76)
    ];

    public const int RobeHue = 0x4F2;
    public const int PlaqueArt = 0x63D4; // stock "Broadsword Plaque" (south wall)

    private static RewardStoreRow[]? _rows;

    public static IReadOnlyList<RewardStoreRow> Rows => _rows ??= Build().ToArray();

    /// <summary>Whether the account holds every live achievement of a category (the Curious, Collector, Explorer titles).</summary>
    public static bool HasAll(PlayerMobile pm, AchievementCategory category)
    {
        if (pm.Account is not IAccount acct)
        {
            return false;
        }

        var live = ClusterFAchievementSystem.Definitions.Values.Where(d => !d.Retired && d.Category == category).ToList();
        var earned = ClusterFAchievementSystem.GetEarnedKeys(acct.Username);
        return live.Count > 0 && live.All(d => earned.Contains(d.Key));
    }

    private static RewardStoreRow Title(string key, string titleKey, int price, int opens, AchievementCategory? all = null,
        string? allText = null) =>
        new($"ap.{key}", $"Title: {LeagueTitles.Display(titleKey)}", price, opens, LeagueShop.TitleArt, LeagueShop.LeagueHue,
            titleKey: titleKey)
        {
            Condition = all is { } c ? pm => HasAll(pm, c) : null,
            ConditionText = allText
        };

    private static IEnumerable<RewardStoreRow> Build()
    {
        yield return Title("title.completionist", RewardStoreTitles.Completionist, 300, 1500);
        yield return Title("title.curious", RewardStoreTitles.Curious, 100, 0, AchievementCategory.Discovery,
            "Requires every Discovery achievement");
        yield return Title("title.collector", RewardStoreTitles.Collector, 200, 0, AchievementCategory.Collection,
            "Requires every Collection achievement");
        yield return Title("title.explorer", RewardStoreTitles.Explorer, 200, 0, AchievementCategory.Exploration,
            "Requires every Exploration achievement");

        foreach (var (key, name, hue) in SashHues)
        {
            yield return new RewardStoreRow($"ap.sash.{key}", $"Achiever's sash, {name}", 40, 0, 0x1541, hue,
                pm => [new BoundCosmeticSash(pm.Account?.Username ?? "", "Achiever's", hue)]);
        }

        yield return new RewardStoreRow("ap.robe", "Robe of the Noted (cosmetic)", 150, 500, 0x1F03, RobeHue,
            RewardStore.One(() => new Robe(RobeHue) { Name = "the robe of the Noted" }));

        yield return new RewardStoreRow("ap.plaque", "Achievement plaque (engraved)", 75, 250, PlaqueArt, 0,
            pm => [Plaque(pm)]);

        foreach (var (id, name) in Paintings)
        {
            var key = new string(name.ToLowerInvariant().Where(char.IsLetter).ToArray());
            yield return new RewardStoreRow($"ap.painting.{key}", $"Painting: {name}", 50, 100, id, 0,
                RewardStore.One(() => new RewardDecor(id, $"a painting: {name}")));
        }

        // cc-P858: the lamps that really light (Items/Lights/ShardLamps.cs), housing decor; the jack-o-lantern is the
        // Festival's (Halloween stock).
        foreach (var (key, label, art, make) in ShardLampStock.All)
        {
            if (key != ShardLampStock.HalloweenKey)
            {
                yield return new RewardStoreRow($"ap.lamp.{key}", $"Lamp: {label}", 75, 200, art, 0, RewardStore.One(() => make()));
            }
        }
    }

    /// <summary>A plaque engraved with the buyer's name and AP total when bought.</summary>
    public static Item Plaque(PlayerMobile pm) =>
        new RewardDecor(PlaqueArt, $"an achievement plaque: {pm.Name}, {RewardStore.DataOf(pm)?.AchievementPoints ?? 0:N0} AP");
}
