// cc-P140 (F-45): the Rootdeep Burrows' shared pieces: the family parts (F-45's "each family drops its P24 part"), the
// two boss parts, Burrowhide carving, the bosses' jewelry, the Vault's fixed drops. Notes: shard-migration
// notes\cc-P140-rootdeep-burrows.md (Part C, the numbers and every deviation).
//
// Parts are stackable items on stock graphics with a hue each (placeholders for Chase's eye: a fang 0x10E8 for claws and
// fangs, a bottle 0xF0E for sap, the crystalline fragments 0x223B for shards, the plague gland 0x1CEF for glands). They
// are the currency of F-45's set upgrades (P24 section 5, built later); here they only drop.
//
// Burrowhide: P36 made the resource (BurrowhideHides, CraftResource.BurrowhideLeather) and left the carve to the
// creatures ("carved later from the dungeon's creatures through OnCarve, not a new HideType", ClusterFStarterResources.cs).
// Pinned's OnCarve knows four hide types and no hook for a fifth (BaseCreature.cs:2003-2110), so a Burrowhide bearer
// carves through BurrowsCarving.Carve: blood, the hides onto the corpse (AddCarvedItem, as pinned does for feathers),
// ribs for a creature with meat, the stock messages, Carved set. Stock creatures are untouched.

using System;
using ModernUO.Serialization;
using Server.Items;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public abstract partial class BurrowsPartBase : Item
    {
        protected BurrowsPartBase(int itemId, int hue, string name, int amount) : base(itemId)
        {
            Stackable = true;
            Amount = amount;
            Hue = hue;
            Name = name;
        }

        public override double DefaultWeight => 0.1;
    }

    /// <summary>The Warren's Burrowers part (burrow grubs, scamps, the Burrow Mother).</summary>
    [SerializationGenerator(0, false)]
    public partial class BurrowerClaws : BurrowsPartBase
    {
        [Constructible]
        public BurrowerClaws(int amount = 1) : base(0x10E8, 0x8AB, "burrower claws", amount)
        {
        }
    }

    /// <summary>The Tangle's Roots part (rootlings, sporecaps, the bramblehulk).</summary>
    [SerializationGenerator(0, false)]
    public partial class RootheartSap : BurrowsPartBase
    {
        [Constructible]
        public RootheartSap(int amount = 1) : base(0xF0E, 0x8A5, "rootheart sap", amount)
        {
        }
    }

    /// <summary>The Dawnseam's Stone part (dawnshards, the seamguard).</summary>
    [SerializationGenerator(0, false)]
    public partial class DawnstoneShards : BurrowsPartBase
    {
        [Constructible]
        public DawnstoneShards(int amount = 1) : base(0x223B, 0x8AC, "dawnstone shards", amount)
        {
        }
    }

    /// <summary>The Gloom's Spiders part (gloomlings, spinners, weavers, the Gloom Queen).</summary>
    [SerializationGenerator(0, false)]
    public partial class GloomGlands : BurrowsPartBase
    {
        [Constructible]
        public GloomGlands(int amount = 1) : base(0x1CEF, 0x497, "gloom glands", amount)
        {
        }
    }

    /// <summary>The Burrow Mother's boss part (P24's Why: each boss drops jewelry and a boss part).</summary>
    [SerializationGenerator(0, false)]
    public partial class BroodMothersMandible : BurrowsPartBase
    {
        [Constructible]
        public BroodMothersMandible(int amount = 1) : base(0x10E8, 0x845, "the Burrow Mother's mandible", amount)
        {
        }
    }

    /// <summary>The Gloom Queen's boss part.</summary>
    [SerializationGenerator(0, false)]
    public partial class GloomQueensFang : BurrowsPartBase
    {
        [Constructible]
        public GloomQueensFang(int amount = 1) : base(0x10E8, 0x455, "the Gloom Queen's fang", amount)
        {
        }
    }
}

namespace Server.Mobiles
{
    public static class BurrowsCarving
    {
        /// <summary>
        /// Carve a Burrowhide bearer: blood, <paramref name="hides"/> Burrowhide hides onto the corpse (AddCarvedItem, the
        /// way pinned puts feathers and wool there), raw ribs for <paramref name="meat"/>, pinned's messages, Carved set.
        /// A carved or animated corpse, or a summoned or bonded creature, gives pinned's own refusal.
        /// </summary>
        public static void Carve(BaseCreature bc, Mobile from, Corpse corpse, int hides, int meat)
        {
            if (corpse.Carved || corpse.Animated || bc.Summoned || bc.IsBonded)
            {
                from.SendLocalizedMessage(corpse.Animated ? 500464 : 500485); // Use this on corpses to carve.. / You see nothing useful to carve from the corpse.
                return;
            }

            if (Core.ML && from.Race == Race.Human)
            {
                hides = (int)Math.Ceiling(hides * 1.1); // pinned's 10% bonus on hides (BaseCreature.cs:2027)
            }

            if (corpse.Map == Map.Felucca)
            {
                hides *= 2;
                if (Core.ML)
                {
                    meat *= 2;
                }
            }

            new Blood(0x122D).MoveToWorld(corpse.Location, corpse.Map);

            if (meat > 0)
            {
                corpse.AddCarvedItem(new RawRibs(meat), from);
                from.SendLocalizedMessage(500467); // You carve some meat, which remains on the corpse.
            }

            if (hides > 0)
            {
                corpse.AddCarvedItem(new BurrowhideHides(hides), from);
                from.SendLocalizedMessage(500471); // You skin it, and the hides are now in the corpse.
            }

            corpse.Carved = true;
        }
    }

    public static class BurrowsLoot
    {
        /// <summary>The bosses' jewelry: one stock gold jewel (P24's Why: "jewelry and a boss part"; tinkering makes the same).</summary>
        public static Item RandomJewel() => Utility.Random(4) switch
        {
            0 => new GoldRing(),
            1 => new GoldBracelet(),
            2 => new GoldEarrings(),
            _ => new GoldNecklace()
        };

        /// <summary>The Overdraft's trinket list (one drops, tradeable): the first proposal for Chase.</summary>
        public static readonly Type[] OverdraftTrinkets =
        {
            typeof(GoldRing), typeof(GoldBracelet), typeof(GoldEarrings), typeof(SilverNecklace)
        };

        public static Item RandomOverdraftTrinket() =>
            (Item)Activator.CreateInstance(OverdraftTrinkets[Utility.Random(OverdraftTrinkets.Length)])!;

        /// <summary>A coin pouch holding exactly <paramref name="gold"/> gold.</summary>
        public static Pouch CoinPouch(int gold, string name)
        {
            var pouch = new Pouch { Name = name };
            pouch.DropItem(new Gold(gold));
            return pouch;
        }
    }
}
