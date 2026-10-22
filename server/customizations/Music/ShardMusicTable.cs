// cc-P368: the shard music table. One row maps a track (ShardMusicTracks) to a region, a sub-area (a box inside a
// region), a creature type, or a boss. ClusterFShardMusic applies it at start. Notes: shard-migration
// notes/cc-P368-added-music-areas-monsters-bosses.md, which carries the same table with Chase's reasons.
//
// Kinds:
//   Region   Target is a region's name as regions.json or our own region code gives it ("Covetous", "the Rootdeep
//            Burrows"). Map names one facet ("Trammel"); null means every facet that has a region of that name. The
//            region's Music becomes the track (its stock music is remembered for players who turned shard music off),
//            and so does every child region that was inheriting it.
//   Area     a box inside a region: Target is a name for it (shown nowhere, used in the staff list), Map the facet, Box
//            the tiles (x, y, width, height; the far edge exclusive, as Rectangle2D). A child region of whatever region
//            holds the box's centre, one priority above it, with the track as its music.
//   Creature a creature type by its class name (exact, case-insensitive: "Orc", "TheSiegebreaker"). Its track plays
//            for a player it fights, over the area's music.
//   Boss     the same, ranked over Creature.
//
// Empty on purpose (2026-10-21): no track in Chase's Music Room notes is marked Use yet, so there is nothing to assign.
// The shape, for the first rows:
//   new(ShardMusicKind.Region,   "the Rootdeep Burrows", 105, "Trammel"),
//   new(ShardMusicKind.Area,     "Old Haven square",     106, "Trammel", new Rectangle2D(3600, 2600, 40, 40)),
//   new(ShardMusicKind.Creature, "HedgeSnatcher",        107),
//   new(ShardMusicKind.Boss,     "TheSiegebreaker",      108),

#nullable enable

namespace Server.Music;

public enum ShardMusicKind
{
    Region,
    Area,
    Creature,
    Boss
}

/// <summary>One assignment: a track for a region, a box, a creature type or a boss. Note is Chase's reason, for the list.</summary>
public sealed record ShardMusicRow(
    ShardMusicKind Kind,
    string Target,
    int Track,
    string? Map = null,
    Rectangle2D? Box = null,
    string? Note = null
);

public static class ShardMusicTable
{
    public static readonly ShardMusicRow[] Rows = [];
}
