// cc-P632 (P571 design, stage 1 "BUTTE-A"): the scoring archery butte's hit pictures. Notes: shard-migration
// notes/cc-P632-build-scoring-buttes.md; design notes/cc-P571-design-butte-and-deed.md.
//
// Archaaz's "Archery Butte that Shows Where Arrows and Bolts Hit" (servuo.dev resource 1340; no license stated, credit
// Archaaz, ship only inside the player package): 20 pictures of the stock butte with one arrow or bolt in a ring, or
// stuck in the frame on a miss, for each facing. Each is, in 5-bit colour, the stock butte (0x100A east, 0x100B south)
// with only the projectile's box (at most 14 x 14 pixels) changed, so it stands exactly where the stock target stands.
// They ship as our PNGs at IDs 46690-46709 (player-package registry.csv, art\<id>.png); the empty target stays the stock
// art. Stage 1 is the art and its records only: the scoring butte that shows them is stage 2.
//
// Records: the stock butte's own tiledata, read from the approved client's tiledata.mul (7.0.117, the same bytes as
// UOAlive 7.0.116's, SHA-256 44f48a45...): "archery butte", 0x00008040 (ArticleAn | Impassable), weight 255, height 10,
// for both facings. records.json carries the players' copy; player-package\tests\Build-UoOverrides.Tests.ps1 compares
// the two. ShatteredLegacyArt.Apply writes these into the server's TileData so every picture blocks as the stock one.

namespace Server.Items;

/// <summary>What the scoring butte shows: the latest shot (stage 2 saves it). Numbered: saved values never change.</summary>
public enum ButteDisplay
{
    Empty = 0,
    ArrowMiss = 1,
    ArrowBullseye = 2,
    ArrowInner = 3,
    ArrowMiddle = 4,
    ArrowOuter = 5,
    BoltMiss = 6,
    BoltBullseye = 7,
    BoltInner = 8,
    BoltMiddle = 9,
    BoltOuter = 10
}

public static class ScoringArcheryButteArt
{
    public sealed record Record(int Id, string Name, TileFlag Flags, int Weight, int Height);

    // The stock targets: east 0x100A, south 0x100B (pinned ArcheryButte.FacingEast is ItemID == 0x100A).
    public const int StockEast = 0x100A;
    public const int StockSouth = 0x100B;

    // The 20 registered IDs, in four blocks of five: bullseye, inner, middle, outer, miss.
    public const int FirstId = 46690;
    public const int LastId = 46709;
    public const int ArrowEast = 46690;
    public const int ArrowSouth = 46695;
    public const int BoltEast = 46700;
    public const int BoltSouth = 46705;

    public const string TileName = "archery butte";
    public const TileFlag TileFlags = TileFlag.ArticleAn | TileFlag.Impassable;
    public const int TileWeight = 255;
    public const int TileHeight = 10;

    /// <summary>The picture for a display state in a facing. Empty (or an unknown value) is the stock target.</summary>
    public static int ItemId(ButteDisplay display, bool east)
    {
        var (block, offset) = display switch
        {
            ButteDisplay.ArrowBullseye => (east ? ArrowEast : ArrowSouth, 0),
            ButteDisplay.ArrowInner => (east ? ArrowEast : ArrowSouth, 1),
            ButteDisplay.ArrowMiddle => (east ? ArrowEast : ArrowSouth, 2),
            ButteDisplay.ArrowOuter => (east ? ArrowEast : ArrowSouth, 3),
            ButteDisplay.ArrowMiss => (east ? ArrowEast : ArrowSouth, 4),
            ButteDisplay.BoltBullseye => (east ? BoltEast : BoltSouth, 0),
            ButteDisplay.BoltInner => (east ? BoltEast : BoltSouth, 1),
            ButteDisplay.BoltMiddle => (east ? BoltEast : BoltSouth, 2),
            ButteDisplay.BoltOuter => (east ? BoltEast : BoltSouth, 3),
            ButteDisplay.BoltMiss => (east ? BoltEast : BoltSouth, 4),
            _ => (east ? StockEast : StockSouth, 0)
        };

        return block + offset;
    }

    public static readonly Record[] Records = MakeRecords();

    private static Record[] MakeRecords()
    {
        var records = new Record[LastId - FirstId + 1];
        for (var i = 0; i < records.Length; i++)
        {
            records[i] = new Record(FirstId + i, TileName, TileFlags, TileWeight, TileHeight);
        }

        return records;
    }

    public static void Apply()
    {
        foreach (var r in Records)
        {
            TileData.ItemTable[r.Id] = new ItemData(r.Name, r.Flags, r.Weight, 0, 0, 0, 0, r.Height);
        }
    }
}
