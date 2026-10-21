// cc-P364 (F-24): the League hunt bestiary, batch 2: the monster parts. One part per creature family, dropped on the
// corpse (PackItem, one a kill; the mini-bosses three), the currency of F-24's hunts and culls when that build lands.
// Notes: shard-migration notes\cc-P364-league-bestiary-batch-2.md.
//
// Parts are stackable items on stock graphics with a hue each, placeholders for Chase's eye, as cc-P140's Burrows parts
// are (BurrowsPartBase): severed elf ears 0x312D, the dread horn's horn 0x315C and 0x315D, a jaw bone 0x1B13, the slith
// tongue 0x5746, delicate scales 0x573A, hides 0x1078, a worldgem bit 0x1F13, a bottle 0xF0E, a void core 0x5728, crystal
// shards 0x5738, a daemon claw 0x5721 and a log 0x1BDD (names from the 7.0.117.0 tiledata). Nothing here is crafted or
// sold yet; F-24 gives them their sink.

using ModernUO.Serialization;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public abstract partial class BestiaryPartBase : Item
    {
        protected BestiaryPartBase(int itemId, int hue, string name, int amount) : base(itemId)
        {
            Stackable = true;
            Amount = amount;
            Hue = hue;
            Name = name;
        }

        public override double DefaultWeight => 0.1;
    }

    /// <summary>The hedge snatchers' and the outrider's part.</summary>
    [SerializationGenerator(0, false)]
    public partial class SnatcherEars : BestiaryPartBase
    {
        [Constructible]
        public SnatcherEars(int amount = 1) : base(0x312D, 0x8A8, "snatcher ears", amount)
        {
        }
    }

    /// <summary>The hornback raiders' part.</summary>
    [SerializationGenerator(0, false)]
    public partial class HornbackHorn : BestiaryPartBase
    {
        [Constructible]
        public HornbackHorn(int amount = 1) : base(0x315C, 0x7D1, "a hornback horn", amount)
        {
        }
    }

    /// <summary>The ditch crawlers' part.</summary>
    [SerializationGenerator(0, false)]
    public partial class CrawlerMandible : BestiaryPartBase
    {
        [Constructible]
        public CrawlerMandible(int amount = 1) : base(0x1B13, 0x8A2, "a crawler mandible", amount)
        {
        }
    }

    /// <summary>The mire croakers' part.</summary>
    [SerializationGenerator(0, false)]
    public partial class CroakerTongue : BestiaryPartBase
    {
        [Constructible]
        public CroakerTongue(int amount = 1) : base(0x5746, 0x8A0, "a croaker tongue", amount)
        {
        }
    }

    /// <summary>The brine lurkers' part.</summary>
    [SerializationGenerator(0, false)]
    public partial class LurkerGills : BestiaryPartBase
    {
        [Constructible]
        public LurkerGills(int amount = 1) : base(0x573A, 0x8AD, "lurker gills", amount)
        {
        }
    }

    /// <summary>The moonmaws' part.</summary>
    [SerializationGenerator(0, false)]
    public partial class MoonmawPelt : BestiaryPartBase
    {
        [Constructible]
        public MoonmawPelt(int amount = 1) : base(0x1078, 0x6A5, "a moonmaw pelt", amount)
        {
        }
    }

    /// <summary>The thornwisps' part.</summary>
    [SerializationGenerator(0, false)]
    public partial class ThornwispSpark : BestiaryPartBase
    {
        [Constructible]
        public ThornwispSpark(int amount = 1) : base(0x1F13, 0x8A5, "a thornwisp spark", amount)
        {
        }
    }

    /// <summary>The pale drifters' part.</summary>
    [SerializationGenerator(0, false)]
    public partial class DrifterEctoplasm : BestiaryPartBase
    {
        [Constructible]
        public DrifterEctoplasm(int amount = 1) : base(0xF0E, 0x4001, "drifter ectoplasm", amount)
        {
        }
    }

    /// <summary>The slaghearts' part.</summary>
    [SerializationGenerator(0, false)]
    public partial class SlagCore : BestiaryPartBase
    {
        [Constructible]
        public SlagCore(int amount = 1) : base(0x5728, 0x489, "a slag core", amount)
        {
        }
    }

    /// <summary>The glasswardens' part.</summary>
    [SerializationGenerator(0, false)]
    public partial class GlasswardenShard : BestiaryPartBase
    {
        [Constructible]
        public GlasswardenShard(int amount = 1) : base(0x5738, 0x480, "a glasswarden shard", amount)
        {
        }
    }

    /// <summary>The tunnelmaws' part.</summary>
    [SerializationGenerator(0, false)]
    public partial class TunnelmawClaw : BestiaryPartBase
    {
        [Constructible]
        public TunnelmawClaw(int amount = 1) : base(0x5721, 0x7D6, "a tunnelmaw claw", amount)
        {
        }
    }

    /// <summary>The Siegebreaker's boss part.</summary>
    [SerializationGenerator(0, false)]
    public partial class SiegebreakerTusk : BestiaryPartBase
    {
        [Constructible]
        public SiegebreakerTusk(int amount = 1) : base(0x315D, 0x455, "the Siegebreaker's tusk", amount)
        {
        }
    }

    /// <summary>The Hollow Oak's boss part.</summary>
    [SerializationGenerator(0, false)]
    public partial class HollowOakHeartwood : BestiaryPartBase
    {
        [Constructible]
        public HollowOakHeartwood(int amount = 1) : base(0x1BDD, 0x7D5, "the Hollow Oak's heartwood", amount)
        {
        }
    }
}
