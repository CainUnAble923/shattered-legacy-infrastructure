// cc-P26, F-8: the shattered runestone, the first item drawn with our own art through the player package.
//
// The art is Vrark's "Animated Runestone" (servuo.dev resource 2564; no license stated): one floating, shattered
// stone seen from four directions, 11 frames each. Players' TazUO draws it from PNGs the package ships
// (app\tazuo\ExternalImages\art\<id>.png) and animates it from our tiledata and animdata records, which the
// launcher writes into copies of the player's own EA files (app\Build-UoOverrides.ps1). Nothing of EA's ships.
// Where the IDs come from, and every ID we use: player-package\vendor\shattered-legacy-art\registry.csv and
// SOURCE.txt, and scripts\find-free-art.py. A client without the package update draws OSI's "UNUSED" placeholder.
//
// Staff choose the design per stone ([props Design 1 to 4, GameMaster and up). Players cannot turn it: a public
// stone turned by one player would turn for everyone.
//
// The server reads tiledata from the unpatched EA copy in CLIENT_DATA_PATH (TileData.Load, pinned
// Server/TileData.cs:303-305), which both shards share. There the four base IDs are "UNUSED" slots with no flags
// and height 0, so a stone would be walked through on the server (Movement.cs:112 skips an item without
// Impassable or Surface) while a patched client refused the step. ShatteredLegacyArt.Configure therefore gives
// the server's in-memory table (TileData.ItemTable, a static array, :288-292) the same records the players'
// files get. No file is touched, and every item with one of these IDs blocks like OSI's own animated runestone.

using System;
using ModernUO.Serialization;

namespace Server
{
    /// <summary>
    /// Our own art IDs that need tiledata on the server (cc-P26). The values are records.json's: change the two together
    /// (player-package\tests\Build-UoOverrides.Tests.ps1 compares them).
    /// </summary>
    public static class ShatteredLegacyArt
    {
        // OSI's animated runestone 0xADBA "Runestone01 Glow01": Animation | PartialHue | Impassable, weight 1, height 3.
        public const TileFlag RunestoneFlags = TileFlag.Animation | TileFlag.PartialHue | TileFlag.Impassable;
        public const int RunestoneWeight = 1;
        public const int RunestoneHeight = 3;
        public const string RunestoneName = "shattered runestone";

        public static void Configure() => Apply();

        public static void Apply()
        {
            foreach (var id in Items.ShatteredRunestone.DesignItemIds)
            {
                TileData.ItemTable[id] = new ItemData(
                    RunestoneName, RunestoneFlags, RunestoneWeight, 0, 0, 0, 0, RunestoneHeight
                );
            }
        }
    }
}

namespace Server.Items
{
    /// <summary>
    /// A floating, shattered runestone in one of four designs (cc-P26, F-8). Decoration for now; expected to become
    /// the F-12 loop stone. Not movable, does not decay.
    /// </summary>
    [SerializationGenerator(0, false)]
    public partial class ShatteredRunestone : Item
    {
        // The base ID of each design: the first of its 11 frames (registry.csv). Design n is DesignItemIds[n - 1].
        public static readonly int[] DesignItemIds = { 0x3C10, 0x3C1B, 0x3C26, 0x3C31 };

        public const int FirstDesign = 1;
        public const int LastDesign = 4;

        public static int ItemIdOf(int design) =>
            design is >= FirstDesign and <= LastDesign
                ? DesignItemIds[design - 1]
                : throw new ArgumentOutOfRangeException(nameof(design), design, "A shattered runestone's design is 1 to 4.");

        [Constructible]
        public ShatteredRunestone() : this(FirstDesign)
        {
        }

        [Constructible]
        public ShatteredRunestone(int design) : base(ItemIdOf(design))
        {
            _design = design;
            Movable = false;
            // Hue 0: TazUO hues a PNG override like any other art (it goes into the same atlas, and the hue shader
            // recolours from each texel's red channel; Art.cs ApplyHue mirrors it on the CPU). A hue would turn the
            // blue runes and grey stone into shades of one colour. The tiledata's PartialHue limits that to grey texels.
            Hue = 0;
        }

        public override string DefaultName => "a shattered runestone";

        public override bool Decays => false;

        [SerializableProperty(0)]
        [CommandProperty(AccessLevel.GameMaster)]
        public int Design
        {
            get => _design;
            set
            {
                _design = value;
                ItemID = value is >= 1 and <= 4 ? DesignItemIds[value - 1] : ItemID;
                this.MarkDirty();
            }
        }

        // A GM who sets ItemID by hand gets it back on the next load; Design is the one setting.
        [AfterDeserialization]
        private void AfterDeserialization()
        {
            if (_design is < FirstDesign or > LastDesign)
            {
                _design = FirstDesign;
            }

        }
    }
}
