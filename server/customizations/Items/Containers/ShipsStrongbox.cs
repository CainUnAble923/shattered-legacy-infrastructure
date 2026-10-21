// cc-P199: ServUO pub57 Scripts/Items/Containers/ShipsStrongbox.cs. "A ship's strongbox" (1149959), a lockable strong
// box (art 0xE80/0x9A8) of 400 stones' capacity holding, for level 1-4: level x 10000 gold, level x 20 random gems,
// level x 5 plus 0-4 rare ML gems, all stacked, and level x 100 gold or copper ingots. Hue 0x836, or 0x481 at level 4
// and up. In ServUO it is the rare (1%) chest fished up with an SOS bottle, at the bottle's level, instead of a metal
// golden or wooden chest (Services/Harvest/Fishing.cs:470-474); that hook is not made here (see the report).
//
// No difference from ServUO in what it holds. ServUO's two [Constructable] constructors stay two (the no-argument one
// rolls the level 1-3, so it cannot fold into a default). DropItemStacked is ServUO's Container method and lives on
// BaseRewardBag (RewardBag.cs).

using System;
using ModernUO.Serialization;

namespace Server.Items;

[Flippable(0xE80, 0x9A8)]
[SerializationGenerator(0, false)]
public partial class ShipsStrongbox : LockableContainer
{
    [Constructible]
    public ShipsStrongbox() : this(Utility.RandomMinMax(1, 3))
    {
    }

    [Constructible]
    public ShipsStrongbox(int level) : base(0xE80)
    {
        // ServUO picks the hue before clamping the level (ShipsStrongbox.cs:20-21); kept.
        Hue = level >= 4 ? 0x481 : 0x836;
        level = Math.Min(4, Math.Max(1, level));

        DropItem(new Gold(10000 * level));

        for (var i = 0; i < level * 20; i++)
        {
            BaseRewardBag.DropItemStacked(this, Loot.RandomGem());
        }

        // ServUO re-rolls Utility.Random(5) in the loop test on every pass; kept.
        for (var i = 0; i < level * 5 + Utility.Random(5); i++)
        {
            Item gem = Utility.Random(8) switch
            {
                0 => new BlueDiamond(),
                1 => new FireRuby(),
                2 => new BrilliantAmber(),
                3 => new PerfectEmerald(),
                4 => new DarkSapphire(),
                5 => new Turquoise(),
                6 => new EcruCitrine(),
                _ => new WhitePearl()
            };

            BaseRewardBag.DropItemStacked(this, gem);
        }

        if (Utility.RandomBool())
        {
            DropItem(new GoldIngot(level * 100));
        }
        else
        {
            DropItem(new CopperIngot(level * 100));
        }
    }

    public override int LabelNumber => 1149959; // A ship's strongbox

    public override int DefaultMaxWeight => 400;
}
