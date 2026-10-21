// cc-P264, companions B1: the companion's enums, its ladder, and the party and follower numbers, every number a
// server setting (Configuration/modernuo.json). The design is shard-migration docs/companions/
// companions-squire-evo-research.md ("Hybrid shape", "The party"); the corrections are docs/companions/
// p41-companions-review.md item 4 and notes/cc-P45-companion-probes.md section 2 (B1).
//
// Placeholders, all settings, for Chase (the numbers are in notes/cc-P264-companions-b1-base-and-record.md):
// - The ladder is F-12's (decided 2026-10-03): max level 50 at loop 0, 100 at loop 1, +5 a loop to 200 at loop 21.
// - Slot cost by rank 1/1/2/2/3/3 is the design's, written against the old six-loop ladder; how ranks fall on the
//   22-step ladder is not decided, so rank is its own field and not worked out from the loop.
// - Party size by the owner's loop 3/4/5/5/6/6 (player included) is the design's draft, same caveat.
// - FollowersMax by the owner's loop is P47's curve (5 at loop 0, 19 at loop 21, the evo mount at 3 as a placeholder).
//   Loop 0 stays at stock 5, so a new player keeps a pet or a mount beside two squires (review item 4).
// - No player loop record exists yet (F-12 is unbuilt), so OwnerLoopOf reads 0 until F-12 sets OwnerLoopProvider.

#nullable enable

using System;
using Server.Mobiles;

namespace Server.Companions;

/// <summary>The five starter paths (design, "Classes"). None: a bred heir before its path is picked.</summary>
public enum CompanionPath
{
    None,
    Blade,
    Bow,
    Arcane,
    Support,
    Rogue
}

/// <summary>Branches, chosen at loop 3 within the path; Penitent and Otherworlder are the rare personalities' own.</summary>
public enum CompanionBranch
{
    None,
    Knight,
    Blademaster,
    Ranger,
    Sharpshooter,
    Spellsword,
    Necromancer,
    Cleric,
    Troubadour,
    Assassin,
    Trickster,
    Penitent,
    Otherworlder
}

/// <summary>
/// The design's nine personalities: seven common, Gloomy rare, Isekai'd extra rare. Rolled at creation and fixed for life.
/// Not AccessoryFootLines' CompanionPersonality (cc-P262's six), which the accessories spec assumed; see the P264 notes.
/// </summary>
public enum Personality
{
    Noble,
    Commoner,
    Devout,
    Sellsword,
    Scholar,
    Braggart,
    Ditzy,
    Gloomy,
    Isekaid
}

public static class CompanionSettings
{
    public const string Prefix = "clusterf.companions.";

    public static bool Enabled => ServerConfiguration.GetOrUpdateSetting(Prefix + "enabled", true);

    // F-12's ladder (2026-10-03): 50 at loop 0, 100 at loop 1, then +5 a loop to the top.
    public static int FirstMaxLevel => ServerConfiguration.GetOrUpdateSetting(Prefix + "ladder.loop0MaxLevel", 50);
    public static int Loop1MaxLevel => ServerConfiguration.GetOrUpdateSetting(Prefix + "ladder.loop1MaxLevel", 100);
    public static int LevelsPerLoop => ServerConfiguration.GetOrUpdateSetting(Prefix + "ladder.levelsPerLoop", 5);
    public static int TopMaxLevel => ServerConfiguration.GetOrUpdateSetting(Prefix + "ladder.topMaxLevel", 200);

    // Design "Hybrid shape": follower slots per companion by rank (0 = Squire ... 5 = Legend).
    public static string SlotCostByRank => ServerConfiguration.GetOrUpdateSetting(Prefix + "slotCostByRank", "1,1,2,2,3,3");

    // Design "The party": party size including the player, by the owner's loop; the last entry holds from then on.
    public static string PartySizeByOwnerLoop =>
        ServerConfiguration.GetOrUpdateSetting(Prefix + "partySizeByOwnerLoop", "3,4,5,5,6,6");

    // P47 section 6's curve (E = 3); index is the owner's loop, the last entry holds from then on.
    public static string FollowersMaxByOwnerLoop =>
        ServerConfiguration.GetOrUpdateSetting(
            Prefix + "followersMaxByOwnerLoop",
            "5,6,7,9,10,11,12,14,15,16,17,18,18,18,18,18,18,18,18,18,18,19"
        );

    // Never below stock 5 (review item 4: a new player keeps a pet or mount).
    public static int FollowersMaxFloor => ServerConfiguration.GetOrUpdateSetting(Prefix + "followersMaxFloor", 5);

    public static int MaxRank => 5;

    /// <summary>The max level at a loop, on F-12's ladder.</summary>
    public static int MaxLevelAtLoop(int loop)
    {
        if (loop <= 0)
        {
            return FirstMaxLevel;
        }

        return Math.Min(TopMaxLevel, Loop1MaxLevel + (loop - 1) * LevelsPerLoop);
    }

    /// <summary>The last loop on the ladder (21 with the defaults).</summary>
    public static int TopLoop => 1 + Math.Max(0, (TopMaxLevel - Loop1MaxLevel) / Math.Max(1, LevelsPerLoop));

    public static int SlotCostForRank(int rank) => Pick(SlotCostByRank, rank, 1);

    public static int PartySizeAt(int ownerLoop) => Pick(PartySizeByOwnerLoop, ownerLoop, 3);

    public static int FollowersMaxAt(int ownerLoop) =>
        Math.Max(FollowersMaxFloor, Pick(FollowersMaxByOwnerLoop, ownerLoop, 5));

    /// <summary>F-12 sets this when the player loop record exists; until then every owner is at loop 0.</summary>
    public static Func<Mobile, int>? OwnerLoopProvider { get; set; }

    public static int OwnerLoopOf(Mobile? owner) => owner == null ? 0 : Math.Max(0, OwnerLoopProvider?.Invoke(owner) ?? 0);

    /// <summary>Entry <paramref name="index"/> of a comma list; past the end, the last entry. A bad list gives the fallback.</summary>
    public static int Pick(string list, int index, int fallback)
    {
        var parts = list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length == 0)
        {
            return fallback;
        }

        var i = Math.Clamp(index, 0, parts.Length - 1);
        return int.TryParse(parts[i], out var value) && value >= 0 ? value : fallback;
    }
}
