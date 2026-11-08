// cc-P813: the Hall of Finishers banner, Chase's Claude Design art (canvas "Shattered Legacy gump art", 11-01; approved
// 11-05): a dark hanging banner with a sword snapped across the blade, 44 x 88, one tile wide. It ships in the player
// package as our art ID 46710 (0xB676; registry.csv, art\46710.png). Notes: shard-migration
// notes/cc-P813-gump-art-import.md.
//
// Record: OSI's hanging banner 0x1586's own tiledata, read from the approved client's tiledata.mul (7.0.117): "banner",
// 0x00044000 (ArticleA | PartialHue), weight 255, height 0. records.json carries the players' copy;
// player-package\tests\Build-UoOverrides.Tests.ps1 compares the two. ShatteredLegacyArt.Apply writes it into the
// server's TileData.
//
// Placed as decor by staff: [add FinishersBanner (not movable, does not decay). The art faces the viewer, so it reads
// best on the tile in front of a south or east wall.

using ModernUO.Serialization;

namespace Server.Items;

public static class FinishersBannerArt
{
    public const int ItemIdValue = 46710; // 0xB676

    public const string TileName = "banner";
    public const TileFlag TileFlags = TileFlag.ArticleA | TileFlag.PartialHue;
    public const int TileWeight = 255;
    public const int TileHeight = 0;

    public static void Apply()
    {
        TileData.ItemTable[ItemIdValue] = new ItemData(TileName, TileFlags, TileWeight, 0, 0, 0, 0, TileHeight);
    }
}

/// <summary>cc-P813: the Hall of Finishers banner as decor. Staff place it with [add FinishersBanner.</summary>
[SerializationGenerator(0, false)]
public partial class FinishersBanner : Item
{
    [Constructible]
    public FinishersBanner() : base(FinishersBannerArt.ItemIdValue)
    {
        Name = "the Hall of Finishers banner";
        Movable = false;
        Weight = 10.0;
    }

    public override bool Decays => false;
}
