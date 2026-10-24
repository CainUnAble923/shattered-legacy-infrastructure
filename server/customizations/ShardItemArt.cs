// =============================================================================
// ShardItemArt.cs
// cc-P363: the art IDs of RedRooster's quest markers, Hestia's gem tokens and quest keys, and Carl Hamilton's four
// spinning moongates, 46592-46663 (0xB600-0xB647), and the server's copy of their tiledata records.
//
// The IDs are registered in player-package\vendor\shattered-legacy-art\registry.csv (SOURCE.txt has the credits and
// what was done to the pixels); the records are records.json's: change the two together (Build-UoOverrides.Tests.ps1
// compares them). ShatteredLegacyArt.Apply writes the table into the server's in-memory TileData.ItemTable, where the
// unpatched EA tiledata has empty slots, so the server weighs a key as the client draws it.
//
// Only each animation's base ID has a record; its other frames are animdata offsets (records.json "animdata").
// OSI's own moongates (0xF6C, 0xDDA, 0x1FD4) and keys keep their IDs and art: these are new IDs beside them.
// =============================================================================

using System;

namespace Server.Items
{
    public enum ShardGateColor
    {
        Blue,
        Red,
        Black,
        White
    }

    public static class ShardItemArt
    {
        public sealed record Record(int Id, string Name, TileFlag Flags, int Weight, int Height);

        public const int FirstId = 0xB600;
        public const int LastId = 0xB647;

        // RedRooster's markers: three frames each, animated 1,2,3,2 (records.json animdata).
        public const int QuestOfferedMarker = 0xB600;   // "!"
        public const int QuestReadyMarker = 0xB603;     // "?"

        // Carl Hamilton's moongates: five frames each, Blue, Red, Black, White.
        public const int FirstGate = 0xB606;
        public const int GateFrames = 5;

        // Hestia's twelve gem tokens, then her 34 keys (key 1 is her blank partial-hue key).
        public const int FirstToken = 0xB61A;
        public const int TokenCount = 12;
        public const int FirstKey = 0xB626;
        public const int KeyCount = 34;

        public static readonly string[] TokenGems =
        [
            "amber", "amethyst", "citrine", "diamond", "emerald", "opal", "pearl", "rose quartz", "ruby", "sapphire",
            "star sapphire", "tourmaline"
        ];

        public static int GateItemId(ShardGateColor color) =>
            color is >= ShardGateColor.Blue and <= ShardGateColor.White
                ? FirstGate + (int)color * GateFrames
                : throw new ArgumentOutOfRangeException(nameof(color), color, "A shard moongate is Blue, Red, Black or White.");

        public static int TokenItemId(int index) =>
            index is >= 0 and < TokenCount
                ? FirstToken + index
                : throw new ArgumentOutOfRangeException(nameof(index), index, "A gem token is 0 to 11.");

        // Key design n is Hestia's "Item Key n", 1 to 34.
        public static int KeyItemId(int design) =>
            design is >= 1 and <= KeyCount
                ? FirstKey + design - 1
                : throw new ArgumentOutOfRangeException(nameof(design), design, "A quest key's design is 1 to 34.");

        // The markers: Animation only (an effect, never placed). The gates: OSI's blue moongate 0xF6C, 0x05804000
        // (ArticleA | LightSource | Animation | bit 0x04000000), weight 255. Tokens and keys: OSI's copper key 0x100E,
        // ArticleA, weight 1, height 1; key 1 is partial hue, as its author made it.
        private const TileFlag Gate = TileFlag.ArticleA | TileFlag.LightSource | TileFlag.Animation | TileFlag.Unknown3;
        private const TileFlag Small = TileFlag.ArticleA;

        // One line a record, so Build-UoOverrides.Tests.ps1 can read them beside records.json.
        public static readonly Record[] Records =
        [
            new(0xB600, "quest marker", TileFlag.Animation, 255, 0),
            new(0xB603, "quest marker", TileFlag.Animation, 255, 0),
            new(0xB606, "blue moongate", Gate, 255, 0),
            new(0xB60B, "red moongate", Gate, 255, 0),
            new(0xB610, "black moongate", Gate, 255, 0),
            new(0xB615, "white moongate", Gate, 255, 0),
            new(0xB61A, "amber token", Small, 1, 1),
            new(0xB61B, "amethyst token", Small, 1, 1),
            new(0xB61C, "citrine token", Small, 1, 1),
            new(0xB61D, "diamond token", Small, 1, 1),
            new(0xB61E, "emerald token", Small, 1, 1),
            new(0xB61F, "opal token", Small, 1, 1),
            new(0xB620, "pearl token", Small, 1, 1),
            new(0xB621, "rose quartz token", Small, 1, 1),
            new(0xB622, "ruby token", Small, 1, 1),
            new(0xB623, "sapphire token", Small, 1, 1),
            new(0xB624, "star sapphire token", Small, 1, 1),
            new(0xB625, "tourmaline token", Small, 1, 1),
            new(0xB626, "quest key", Small | TileFlag.PartialHue, 1, 1),
            new(0xB627, "quest key", Small, 1, 1),
            new(0xB628, "quest key", Small, 1, 1),
            new(0xB629, "quest key", Small, 1, 1),
            new(0xB62A, "quest key", Small, 1, 1),
            new(0xB62B, "quest key", Small, 1, 1),
            new(0xB62C, "quest key", Small, 1, 1),
            new(0xB62D, "quest key", Small, 1, 1),
            new(0xB62E, "quest key", Small, 1, 1),
            new(0xB62F, "quest key", Small, 1, 1),
            new(0xB630, "quest key", Small, 1, 1),
            new(0xB631, "quest key", Small, 1, 1),
            new(0xB632, "quest key", Small, 1, 1),
            new(0xB633, "quest key", Small, 1, 1),
            new(0xB634, "quest key", Small, 1, 1),
            new(0xB635, "quest key", Small, 1, 1),
            new(0xB636, "quest key", Small, 1, 1),
            new(0xB637, "quest key", Small, 1, 1),
            new(0xB638, "quest key", Small, 1, 1),
            new(0xB639, "quest key", Small, 1, 1),
            new(0xB63A, "quest key", Small, 1, 1),
            new(0xB63B, "quest key", Small, 1, 1),
            new(0xB63C, "quest key", Small, 1, 1),
            new(0xB63D, "quest key", Small, 1, 1),
            new(0xB63E, "quest key", Small, 1, 1),
            new(0xB63F, "quest key", Small, 1, 1),
            new(0xB640, "quest key", Small, 1, 1),
            new(0xB641, "quest key", Small, 1, 1),
            new(0xB642, "quest key", Small, 1, 1),
            new(0xB643, "quest key", Small, 1, 1),
            new(0xB644, "quest key", Small, 1, 1),
            new(0xB645, "quest key", Small, 1, 1),
            new(0xB646, "quest key", Small, 1, 1),
            new(0xB647, "quest key", Small, 1, 1)
        ];

        public static void Apply()
        {
            foreach (var r in Records)
            {
                TileData.ItemTable[r.Id] = new ItemData(r.Name, r.Flags, r.Weight, 0, 0, 0, 0, r.Height);
            }
        }
    }
}
