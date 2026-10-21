// cc-P142 (Chase's decisions of 2026-10-16, items 1 and 3a): the shard's rules for WHERE a monster can be stolen from
// and WHO may try, layered on cc-P127's faithful port of ServUO's StealingHandler. OSI's rule (StealingHandler.cs:
// CheckLocation) is a Felucca dungeon or a champion spawn, for anyone with Stealing 100. Ours, every one a registered
// deviation (shard-migration notes/cc-P142-monster-stealing-shard-rules.md):
//
//   - A thief must be a member of the Thieves' Den (guild key "thieves"; the ladder's names are the generic ones,
//     ClusterFGuildSystem.GenericRank: Initiate, Apprentice, Journeyman, Master, Grandmaster, Legendary).
//   - Rank opens places (ClusterFGuildSystem.GuildRankIndex, 0 to 5):
//       Initiate (member)   OSI's own places: Felucca dungeons and champion spawns on any facet; and the tutorial
//                           dungeon, the Rootdeep Burrows outside the Vault (TutorialDungeonRegionTypes; Chase,
//                           2026-10-18 05:51: "it is the tutorial dungeon", so it is sooner in the Den).
//       Journeyman (2)      dungeons on every facet (a DungeonRegion on Trammel, Ilshenar, Malas, Tokuno, Ter Mur ...).
//       Master (3)          the shard's own later dungeons (ShardDungeonRegionTypes, empty today; the Burrows is the
//                           tutorial set above) and bosses anywhere (ClusterFLootBoxes.IsBoss: champions, peerless
//                           and the shard's named bosses, paragons left out; plus ShardBossTypes, the Burrows'
//                           named bosses, which the loot boxes do not count).
//     A thief whose rank is too low, or who is not in the Den, hears an in-character line, but only in a place monster
//     stealing covers at some rank: outside every such place nothing is said, as OSI says nothing.
//   - Never: a tutorial creature, anything in the Vault, a companion (the pack mule), a pet or summon (OSI's own rule),
//     and a creature already stolen from (one SUCCESS per creature, decision 3a; a miss leaves it open).

using System;
using System.Collections.Generic;
using Server.Engines.CannedEvil;
using Server.LootBoxes;
using Server.Mobiles;
using Server.Regions;

namespace Server.Engines.CreatureStealing;

public enum StealPlace
{
    /// <summary>Nowhere monster stealing works at any rank.</summary>
    None,
    /// <summary>A dungeon on Felucca (OSI).</summary>
    FeluccaDungeon,
    /// <summary>A champion spawn region on any facet (OSI).</summary>
    ChampionSpawn,
    /// <summary>A dungeon on any other facet (ours, Journeyman).</summary>
    FacetDungeon,
    /// <summary>One of the shard's own dungeons (ours, Master).</summary>
    ShardDungeon,
    /// <summary>The tutorial dungeon, the Rootdeep Burrows outside the Vault (ours, Initiate).</summary>
    TutorialDungeon
}

public static class ThievesDenStealRules
{
    public const string GuildKey = "thieves";

    public const int RankForOsiPlaces = ClusterFGuildSystem.RankInitiate;
    public const int RankForTutorialDungeon = ClusterFGuildSystem.RankInitiate;
    public const int RankForEveryFacet = ClusterFGuildSystem.RankJourneyman;
    public const int RankForShardDungeons = ClusterFGuildSystem.RankMaster;
    public const int RankForBosses = ClusterFGuildSystem.RankMaster;

    /// <summary>
    /// The tutorial dungeon's regions: the Rootdeep Burrows (its Vault is on the never list by the creature's region).
    /// Any Den member may steal here (Chase, 2026-10-18 05:51). Checked before ShardDungeonRegionTypes.
    /// </summary>
    public static HashSet<Type> TutorialDungeonRegionTypes { get; } = new() { typeof(OldHavenBurrowsRegion) };

    /// <summary>
    /// The shard's own later dungeons (Master): a region is one if it or any parent is of a listed type. Empty today;
    /// the Burrows is the tutorial set above, not this one.
    /// </summary>
    public static HashSet<Type> ShardDungeonRegionTypes { get; } = new();

    /// <summary>
    /// Bosses the Den counts that the loot boxes do not (ClusterFLootBoxes.IsBoss knows champions, peerless and the
    /// stock named bosses): the Burrows' named bosses, so "bosses anywhere stay Master" holds in the tutorial dungeon.
    /// The Overdraft is not here: it lives in the Vault, which is never.
    /// </summary>
    public static HashSet<Type> ShardBossTypes { get; } = new() { typeof(TheBurrowMother), typeof(TheGloomQueen) };

    // The lines (Chase asked for a clear in-character line when the rank is too low).
    public const string NotAMemberLine =
        "Only a hand sworn to the Thieves' Den knows where a monster keeps its treasures.";

    public static string TooLowLine(string heldRank, string neededRank, string place) =>
        $"The Den does not trust {Article(heldRank)} {heldRank} to work {place}. Come back {Article(neededRank)} {neededRank}.";

    private static string Article(string word) =>
        word.Length > 0 && "AEIOUaeiou".IndexOf(word[0]) >= 0 ? "an" : "a";

    /// <summary>What the place is called in the refusal line.</summary>
    public static string PlaceName(StealPlace place) => place switch
    {
        StealPlace.FeluccaDungeon => "the dungeons of Felucca",
        StealPlace.ChampionSpawn => "a champion's ground",
        StealPlace.FacetDungeon => "the dungeons beyond Felucca",
        StealPlace.ShardDungeon => "these halls",
        StealPlace.TutorialDungeon => "the warrens",
        _ => "this ground"
    };

    /// <summary>
    /// Where the thief stands, for the ladder. The tutorial dungeon, then a shard dungeon, win over the stock dungeon
    /// either may also be.
    /// </summary>
    public static StealPlace PlaceOf(Mobile thief)
    {
        var region = thief?.Region;

        if (region == null)
        {
            return StealPlace.None;
        }

        if (IsTutorialDungeon(region))
        {
            return StealPlace.TutorialDungeon;
        }

        if (IsShardDungeon(region))
        {
            return StealPlace.ShardDungeon;
        }

        if (region.IsPartOf<ChampionSpawnRegion>())
        {
            return StealPlace.ChampionSpawn;
        }

        if (region.IsPartOf<DungeonRegion>())
        {
            return thief.Map == Map.Felucca ? StealPlace.FeluccaDungeon : StealPlace.FacetDungeon;
        }

        return StealPlace.None;
    }

    public static bool IsShardDungeon(Region region) => IsOfAny(region, ShardDungeonRegionTypes);

    public static bool IsTutorialDungeon(Region region) => IsOfAny(region, TutorialDungeonRegionTypes);

    private static bool IsOfAny(Region region, HashSet<Type> types)
    {
        for (var r = region; r != null; r = r.Parent)
        {
            if (types.Contains(r.GetType()))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The rank index a place needs, raised to the boss rank for a boss.</summary>
    public static int RequiredRank(StealPlace place, bool boss)
    {
        var rank = place switch
        {
            StealPlace.FeluccaDungeon or StealPlace.ChampionSpawn => RankForOsiPlaces,
            StealPlace.TutorialDungeon => RankForTutorialDungeon,
            StealPlace.FacetDungeon => RankForEveryFacet,
            StealPlace.ShardDungeon => RankForShardDungeons,
            _ => int.MaxValue
        };

        return boss ? Math.Max(rank, RankForBosses) : rank;
    }

    /// <summary>A boss to the Den: the loot boxes' bosses (paragons left out) and the shard's own named ones.</summary>
    public static bool IsBoss(BaseCreature bc) =>
        bc != null && (ClusterFLootBoxes.IsBoss(bc) || (!bc.Controlled && !bc.Summoned && ShardBossTypes.Contains(bc.GetType())));

    /// <summary>
    /// The creatures no rank reaches: tutorial creatures, anything standing in the Vault, the pack mule (a companion),
    /// and pets and summons (OSI's rule, kept). A creature already stolen from is refused separately, with OSI's line.
    /// </summary>
    public static bool IsNeverStolen(BaseCreature bc) =>
        bc == null || bc.Controlled || bc.Summoned || bc is TutorialCreature || bc is PackMule ||
        bc.Region?.IsPartOf<RootdeepVaultRegion>() == true;

    /// <summary>The thief's rank index in the Den, or -1 when not a member.</summary>
    public static int DenRank(Mobile thief)
    {
        if (thief is not PlayerMobile pm || !ClusterFGuildSystem.IsJoined(pm, GuildKey))
        {
            return -1;
        }

        var data = ClusterFAccountPersistence.GetGuild(pm);
        return data == null ? -1 : ClusterFGuildSystem.GuildRankIndex(GuildKey, data);
    }

    public static string RankName(int index) => ClusterFGuildPromotion.RankNameForIndex(GuildKey, Math.Max(0, index));

    /// <summary>
    /// Whether this thief may try here. False with a line when the thief is in a covered place but lacks the Den or the
    /// rank; false with no line outside every covered place (OSI's silence).
    /// </summary>
    public static bool CheckPlaceAndRank(PlayerMobile thief, BaseCreature from, out StealPlace place, out string refusal)
    {
        refusal = null;
        place = PlaceOf(thief);

        if (place == StealPlace.None && from is ExodusZealot)
        {
            // cc-P195: an Exodus zealot may be stolen from wherever it stands (ServUO StealingHandler.CheckLocation);
            // outside the Exodus Dungeon it counts as the other-facet dungeon it came from.
            place = StealPlace.FacetDungeon;
        }

        if (place == StealPlace.None)
        {
            return false;
        }

        var rank = DenRank(thief);

        if (rank < 0)
        {
            refusal = NotAMemberLine;
            return false;
        }

        var needed = RequiredRank(place, IsBoss(from));

        if (rank < needed)
        {
            refusal = TooLowLine(RankName(rank), RankName(needed), IsBoss(from) && needed == RankForBosses && RequiredRank(place, false) < needed ? "a creature of that renown" : PlaceName(place));
            return false;
        }

        return true;
    }
}
