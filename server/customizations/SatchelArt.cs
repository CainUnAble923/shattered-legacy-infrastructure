// =============================================================================
// SatchelArt.cs
// cc-P236: Chase's satchel and guild bag icons (made in Codex from his own source art, approved 2026-10-05,
// design\satchel-icons\) as our own art IDs, and what moves an item saved on the old stock graphic over to them.
//
// The IDs are registered in player-package\vendor\shattered-legacy-art\registry.csv, their PNGs are art\13184.png to
// art\13189.png, and their tiledata records are records.json's: change the three together (Build-UoOverrides.Tests.ps1
// compares this table with records.json). ShatteredLegacyArt.Apply writes the table into the server's in-memory
// TileData.ItemTable, so the server weighs and stacks the new IDs as the client draws them.
//
// Each record copies the stock graphic it replaces (read from the client's tiledata.mul) and adds PartialHue: the
// icons hold exact grays, so a tier hue tints only the leather, as it did on the stock art.
//   0xA272 "Ore_Satchel_east": Container, weight 3, height 1 (the satchels).
//   0xE76 "bag": Container | ArticleA, weight 2, height 2 (the two bags).
//
// A player without the package update sees the slot's own art: the "UNUSED" placeholder (find-free-art.py
// --placeholder picked these slots for that).
//
// Icons approved but not shipped, waiting on an item: the Arcane Society reagent pouch (hue 0x12B decided), the
// tailor's cloth satchel, the maritime fisher's creel, and the Healers' Covenant satchel.
// =============================================================================

using Server;

namespace Server.Items
{
    public static class SatchelArt
    {
        public sealed record Record(int Id, string Name, TileFlag Flags, int Weight, int Height);

        public const int OreSatchel = 0x3380;           // CompactOreSatchel and its tiers T2 to T5
        public const int LumberSatchel = 0x3381;        // ForestersLumberSatchel, Seasoned, Arborist
        public const int HuntersSatchel = 0x3382;       // HuntersSatchel, SeasonedHuntersSatchel, WardensFieldSatchel
        public const int ArtificersSatchel = 0x3383;    // ArtificersSatchel
        public const int GemologistsSatchel = 0x3384;   // GemologistsSatchel
        public const int SmithGuildSalvageBag = 0x3385; // SmithGuildSalvageBag

        // The stock graphics the classes wore before cc-P236.
        public const int StockSatchel = 0xA272;
        public const int StockBag = 0xE76;

        // Chase, 2026-10-05: the Artificer's Satchel in purple 0xDC; 0x497 rendered nearly black on the gray leather.
        public const int ArtificersHue = 0xDC;
        public const int LegacyArtificersHue = 0x497;

        public static readonly Record[] Records =
        {
            new(0x3380, "ore satchel", TileFlag.PartialHue | TileFlag.Container, 3, 1),
            new(0x3381, "lumber satchel", TileFlag.PartialHue | TileFlag.Container, 3, 1),
            new(0x3382, "hunter's satchel", TileFlag.PartialHue | TileFlag.Container, 3, 1),
            new(0x3383, "artificer's satchel", TileFlag.PartialHue | TileFlag.Container, 3, 1),
            new(0x3384, "gemologists satchel", TileFlag.ArticleA | TileFlag.PartialHue | TileFlag.Container, 2, 2),
            new(0x3385, "smith salvage bag", TileFlag.ArticleA | TileFlag.PartialHue | TileFlag.Container, 2, 2)
        };

        public static void Apply()
        {
            foreach (var r in Records)
            {
                TileData.ItemTable[r.Id] = new ItemData(r.Name, r.Flags, r.Weight, 0, 0, 0, 0, r.Height);
            }
        }

        // A version 0 save still on the stock graphic takes ours; any other graphic is a staff member's choice and stays.
        public static void MoveFromStock(Item item, int stock, int ours)
        {
            if (item.ItemID == stock)
            {
                item.ItemID = ours;
            }
        }
    }
}
