// cc-P175 Part C 4: Clean Up Britannia's house addons and the beehive, ported from ServUO pub57
// Services/CleanUpBritannia/Items/: ChaosTile.cs, the eight <Virtue>VirtueTile.cs, FirePit.cs, HorseBarding.cs,
// DragonHead.cs and Beehive.cs. Art, offsets, clilocs and options as ServUO.
//
// Changes from ServUO, each small:
//   * The nine tiles share one shape in ServUO (four components from a first art id at (0,0), (0,1), (1,1), (1,0); a
//     virtue tile's North adds 4), so they share a base here (CleanUpTileAddon, CleanUpTileDeed) with ServUO's class
//     names on top. The choice is not saved, as pinned's own option deeds do not save theirs (AnkhOfSacrifice.cs).
//   * The deeds open pinned's RewardOptionGump (RewardOptionGump.DisplayTo, Engines/Veteran Rewards/RewardOptionGump.cs).
//   * AddonFacing is not in pinned: DragonHead's two facings are its own enum.
//   * DragonHead.cs's "old classes" (DragonHeadOld, DragonHeadDeedOld, a ServUO save conversion) are not ported: no
//     save here ever held them.
//   * Beehive: pinned has no Item.OnLockDownChange, so ServUO's bees overlay item (art 2331, shown while the hive is
//     locked down) is left out; the hive, its honeypots (one every 4 hours, up to 10) and its secure level are ServUO's.
//
// Notes: shard-migration notes/cc-P175-cont-clean-up-britannia-finish.md, Part C.

using System;
using ModernUO.Serialization;
using Server.Collections;
using Server.ContextMenus;
using Server.Gumps;
using Server.Multis;
using Server.Network;

namespace Server.Items
{
    // -- The tiles ---------------------------------------------------------------------------------------------------

    [SerializationGenerator(0, false)]
    public abstract partial class CleanUpTileAddon : BaseAddon
    {
        protected CleanUpTileAddon(int firstTile)
        {
            AddComponent(new AddonComponent(firstTile), 0, 0, 0);
            AddComponent(new AddonComponent(firstTile + 1), 0, 1, 0);
            AddComponent(new AddonComponent(firstTile + 2), 1, 1, 0);
            AddComponent(new AddonComponent(firstTile + 3), 1, 0, 0);
        }
    }

    // A virtue tile deed: North or West, chosen when it is used (ServUO's IRewardOption: North 0, West 1).
    [SerializationGenerator(0, false)]
    public abstract partial class CleanUpTileDeed : BaseAddonDeed, IRewardOption
    {
        protected CleanUpTileDeed() => LootType = LootType.Blessed;

        protected bool North { get; private set; }

        protected abstract int NorthOption { get; }
        protected abstract int WestOption { get; }

        public override void OnDoubleClick(Mobile from)
        {
            if (IsChildOf(from.Backpack))
            {
                RewardOptionGump.DisplayTo(from, this);
            }
            else
            {
                from.SendLocalizedMessage(1062334); // This item must be in your backpack to be used.
            }
        }

        public void GetOptions(RewardOptionList list)
        {
            list.Add(0, NorthOption);
            list.Add(1, WestOption);
        }

        public void OnOptionSelected(Mobile from, int choice)
        {
            North = choice == 0;

            if (!Deleted)
            {
                base.OnDoubleClick(from);
            }
        }
    }

    [SerializationGenerator(0, false)]
    public partial class ChaosTileAddon : CleanUpTileAddon
    {
        [Constructible]
        public ChaosTileAddon() : base(5347)
        {
        }

        public override BaseAddonDeed Deed => new ChaosTileDeed();
    }

    [SerializationGenerator(0, false)]
    public partial class ChaosTileDeed : BaseAddonDeed
    {
        [Constructible]
        public ChaosTileDeed() => LootType = LootType.Blessed;

        public override BaseAddon Addon => new ChaosTileAddon();
        public override int LabelNumber => 1080490; // Chaos Tile Deed
    }

    [SerializationGenerator(0, false)]
    public partial class HonestyVirtueTileAddon : CleanUpTileAddon
    {
        [Constructible]
        public HonestyVirtueTileAddon(bool north = false) : base(5279 + (north ? 4 : 0))
        {
        }

        public override BaseAddonDeed Deed => new HonestyVirtueTileDeed();
    }

    [SerializationGenerator(0, false)]
    public partial class HonestyVirtueTileDeed : CleanUpTileDeed
    {
        [Constructible]
        public HonestyVirtueTileDeed()
        {
        }

        public override BaseAddon Addon => new HonestyVirtueTileAddon(North);
        public override int LabelNumber => 1080488; // Honesty Virtue Tile Deed
        protected override int NorthOption => 1080215;
        protected override int WestOption => 1080214;
    }

    [SerializationGenerator(0, false)]
    public partial class CompassionVirtueTileAddon : CleanUpTileAddon
    {
        [Constructible]
        public CompassionVirtueTileAddon(bool north = false) : base(5287 + (north ? 4 : 0))
        {
        }

        public override BaseAddonDeed Deed => new CompassionVirtueTileDeed();
    }

    [SerializationGenerator(0, false)]
    public partial class CompassionVirtueTileDeed : CleanUpTileDeed
    {
        [Constructible]
        public CompassionVirtueTileDeed()
        {
        }

        public override BaseAddon Addon => new CompassionVirtueTileAddon(North);
        public override int LabelNumber => 1080481; // Compassion Virtue Tile Deed
        protected override int NorthOption => 1080218;
        protected override int WestOption => 1080217;
    }

    [SerializationGenerator(0, false)]
    public partial class ValorVirtueTileAddon : CleanUpTileAddon
    {
        [Constructible]
        public ValorVirtueTileAddon(bool north = false) : base(5303 + (north ? 4 : 0))
        {
        }

        public override BaseAddonDeed Deed => new ValorVirtueTileDeed();
    }

    [SerializationGenerator(0, false)]
    public partial class ValorVirtueTileDeed : CleanUpTileDeed
    {
        [Constructible]
        public ValorVirtueTileDeed()
        {
        }

        public override BaseAddon Addon => new ValorVirtueTileAddon(North);
        public override int LabelNumber => 1080486; // Valor Virtue Tile Deed
        protected override int NorthOption => 1080224;
        protected override int WestOption => 1080223;
    }

    [SerializationGenerator(0, false)]
    public partial class SpiritualityVirtueTileAddon : CleanUpTileAddon
    {
        [Constructible]
        public SpiritualityVirtueTileAddon(bool north = false) : base(5311 + (north ? 4 : 0))
        {
        }

        public override BaseAddonDeed Deed => new SpiritualityVirtueTileDeed();
    }

    [SerializationGenerator(0, false)]
    public partial class SpiritualityVirtueTileDeed : CleanUpTileDeed
    {
        [Constructible]
        public SpiritualityVirtueTileDeed()
        {
        }

        public override BaseAddon Addon => new SpiritualityVirtueTileAddon(North);
        public override int LabelNumber => 1080484; // Spirituality Virtue Tile Deed
        protected override int NorthOption => 1080227;
        protected override int WestOption => 1080226;
    }

    [SerializationGenerator(0, false)]
    public partial class HonorVirtueTileAddon : CleanUpTileAddon
    {
        [Constructible]
        public HonorVirtueTileAddon(bool north = false) : base(5319 + (north ? 4 : 0))
        {
        }

        public override BaseAddonDeed Deed => new HonorVirtueTileDeed();
    }

    [SerializationGenerator(0, false)]
    public partial class HonorVirtueTileDeed : CleanUpTileDeed
    {
        [Constructible]
        public HonorVirtueTileDeed()
        {
        }

        public override BaseAddon Addon => new HonorVirtueTileAddon(North);
        public override int LabelNumber => 1080485; // Honor Virtue Tile Deed
        protected override int NorthOption => 1080230;
        protected override int WestOption => 1080229;
    }

    [SerializationGenerator(0, false)]
    public partial class HumilityVirtueTileAddon : CleanUpTileAddon
    {
        [Constructible]
        public HumilityVirtueTileAddon(bool north = false) : base(5327 + (north ? 4 : 0))
        {
        }

        public override BaseAddonDeed Deed => new HumilityVirtueTileDeed();
    }

    [SerializationGenerator(0, false)]
    public partial class HumilityVirtueTileDeed : CleanUpTileDeed
    {
        [Constructible]
        public HumilityVirtueTileDeed()
        {
        }

        public override BaseAddon Addon => new HumilityVirtueTileAddon(North);
        public override int LabelNumber => 1080483; // Humility Virtue Tile Deed
        protected override int NorthOption => 1080233;
        protected override int WestOption => 1080232;
    }

    [SerializationGenerator(0, false)]
    public partial class SacrificeVirtueTileAddon : CleanUpTileAddon
    {
        [Constructible]
        public SacrificeVirtueTileAddon(bool north = false) : base(5386 + (north ? 4 : 0))
        {
        }

        public override BaseAddonDeed Deed => new SacrificeVirtueTileDeed();
    }

    [SerializationGenerator(0, false)]
    public partial class SacrificeVirtueTileDeed : CleanUpTileDeed
    {
        [Constructible]
        public SacrificeVirtueTileDeed()
        {
        }

        public override BaseAddon Addon => new SacrificeVirtueTileAddon(North);
        public override int LabelNumber => 1080482; // Sacrifice Virtue Tile Deed
        protected override int NorthOption => 1080236;
        protected override int WestOption => 1080235;
    }

    [SerializationGenerator(0, false)]
    public partial class JusticeVirtueTileAddon : CleanUpTileAddon
    {
        [Constructible]
        public JusticeVirtueTileAddon(bool north = false) : base(5295 + (north ? 4 : 0))
        {
        }

        public override BaseAddonDeed Deed => new JusticeVirtueTileDeed();
    }

    [SerializationGenerator(0, false)]
    public partial class JusticeVirtueTileDeed : CleanUpTileDeed
    {
        [Constructible]
        public JusticeVirtueTileDeed()
        {
        }

        public override BaseAddon Addon => new JusticeVirtueTileAddon(North);
        public override int LabelNumber => 1080487; // Justice Virtue Tile Deed
        protected override int NorthOption => 1080221;
        protected override int WestOption => 1080220;
    }

    // -- FirePit.cs ----------------------------------------------------------------------------------------------------

    [SerializationGenerator(0, false)]
    public partial class FirePitAddon : BaseAddon
    {
        [Constructible]
        public FirePitAddon() => AddComponent(new AddonComponent(10749), 0, 0, 0);

        public override BaseAddonDeed Deed => new FirePitDeed();
    }

    [SerializationGenerator(0, false)]
    public partial class FirePitDeed : BaseAddonDeed
    {
        [Constructible]
        public FirePitDeed() => LootType = LootType.Blessed;

        public override BaseAddon Addon => new FirePitAddon();
        public override int LabelNumber => 1080206; // Fire Pit
    }

    // -- HorseBarding.cs: South (0) or East (1) ------------------------------------------------------------------------

    [SerializationGenerator(0, false)]
    public partial class HorseBardingAddon : BaseAddon
    {
        [Constructible]
        public HorseBardingAddon(bool east = false)
        {
            if (east)
            {
                AddComponent(new AddonComponent(4983), 0, 0, 0);
                AddComponent(new AddonComponent(4982), -1, 0, 0);
            }
            else
            {
                AddComponent(new AddonComponent(4984), 0, 0, 0);
                AddComponent(new AddonComponent(4985), 0, -1, 0);
            }
        }

        public override BaseAddonDeed Deed => new HorseBardingDeed();
    }

    [SerializationGenerator(0, false)]
    public partial class HorseBardingDeed : BaseAddonDeed, IRewardOption
    {
        private bool _east;

        [Constructible]
        public HorseBardingDeed() => LootType = LootType.Blessed;

        public override BaseAddon Addon => new HorseBardingAddon(_east);
        public override int LabelNumber => 1080212; // Horse Barding

        public override void OnDoubleClick(Mobile from)
        {
            if (IsChildOf(from.Backpack))
            {
                RewardOptionGump.DisplayTo(from, this);
            }
            else
            {
                from.SendLocalizedMessage(1062334); // This item must be in your backpack to be used.
            }
        }

        public void GetOptions(RewardOptionList list)
        {
            list.Add(0, 1080211); // South
            list.Add(1, 1080210); // East
        }

        public void OnOptionSelected(Mobile from, int choice)
        {
            _east = choice == 1;

            if (!Deleted)
            {
                base.OnDoubleClick(from);
            }
        }
    }

    // -- DragonHead.cs: a wall piece, South (0) or East (1) -----------------------------------------------------------

    [SerializationGenerator(0, false)]
    public partial class DragonHeadAddon : BaseAddon
    {
        [Constructible]
        public DragonHeadAddon(bool east = false) =>
            AddComponent(new DragonHeadComponent(east ? 0x2235 : 0x2234), 0, 0, 10);

        public override BaseAddonDeed Deed => new DragonHeadAddonDeed();
    }

    [SerializationGenerator(0, false)]
    public partial class DragonHeadComponent : AddonComponent
    {
        public DragonHeadComponent(int itemID) : base(itemID)
        {
        }

        public override bool NeedsWall => true;
        public override Point3D WallPosition => ItemID == 0x2235 ? new Point3D(-1, 0, 0) : new Point3D(0, -1, 0);
    }

    [SerializationGenerator(0, false)]
    public partial class DragonHeadAddonDeed : BaseAddonDeed, IRewardOption
    {
        private bool _east;

        [Constructible]
        public DragonHeadAddonDeed() => LootType = LootType.Blessed;

        public override BaseAddon Addon => new DragonHeadAddon(_east);
        public override int LabelNumber => 1080209; // Dragon Head

        public override void OnDoubleClick(Mobile from)
        {
            if (IsChildOf(from.Backpack))
            {
                RewardOptionGump.DisplayTo(from, this);
            }
            else
            {
                from.SendLocalizedMessage(1062334); // This item must be in your backpack to be used.
            }
        }

        public void GetOptions(RewardOptionList list)
        {
            list.Add(0, 1080208); // South
            list.Add(1, 1080207); // East
        }

        public void OnOptionSelected(Mobile from, int choice)
        {
            _east = choice == 1;

            if (!Deleted)
            {
                base.OnDoubleClick(from);
            }
        }
    }

    // -- Beehive.cs ----------------------------------------------------------------------------------------------------

    [SerializationGenerator(0, false)]
    public partial class Beehive : Item, ISecurable
    {
        private static readonly TimeSpan SpawnTime = TimeSpan.FromHours(4.0);

        private TimerExecutionToken _spawnTimer;

        [SerializableField(0)]
        [SerializedCommandProperty(AccessLevel.GameMaster)]
        private SecureLevel _level;

        [SerializableField(1, setter: "private")]
        private DateTime _nextSpawnTime;

        [SerializableField(2, setter: "private")]
        private int _honeypots;

        [Constructible]
        public Beehive() : base(2330) => StartSpawnTimer(TimeSpan.FromMinutes(1.0));

        public override double DefaultWeight => 10.0;
        public override bool ForceShowProperties => true;
        public override int LabelNumber => 1080263; // Beehive

        [CommandProperty(AccessLevel.GameMaster)]
        public int HoneypotCount
        {
            get => _honeypots;
            set
            {
                if (value >= 10)
                {
                    Honeypots = 10;
                    _spawnTimer.Cancel();
                }
                else
                {
                    Honeypots = Math.Max(0, value);
                    StartSpawnTimer(SpawnTime);
                }

                InvalidateProperties();
            }
        }

        public override void GetProperties(IPropertyList list)
        {
            base.GetProperties(list);
            list.Add(1080202, _honeypots); // Honeypots: ~1_COUNT~
        }

        public override void GetContextMenuEntries(Mobile from, ref PooledRefList<ContextMenuEntry> list)
        {
            base.GetContextMenuEntries(from, ref list);
            SetSecureLevelEntry.AddTo(from, this, ref list);
        }

        private void StartSpawnTimer(TimeSpan delay)
        {
            _spawnTimer.Cancel();
            Timer.StartTimer(delay, () => HoneypotCount = _honeypots + 1, out _spawnTimer);
            NextSpawnTime = Core.Now + delay;
        }

        public override void OnDoubleClick(Mobile from)
        {
            if (!from.InRange(GetWorldLocation(), 2))
            {
                from.LocalOverheadMessage(MessageType.Regular, 0x3B2, 1019045); // I can't reach that.
            }
            else if (_honeypots > 0)
            {
                from.AddToBackpack(new JarHoney());
                HoneypotCount = _honeypots - 1;
            }
        }

        public override void OnDelete()
        {
            _spawnTimer.Cancel();
            base.OnDelete();
        }

        [AfterDeserialization]
        private void AfterDeserialization()
        {
            if (_honeypots < 10)
            {
                var delay = _nextSpawnTime - Core.Now;
                StartSpawnTimer(delay > TimeSpan.Zero ? delay : TimeSpan.Zero);
            }
        }
    }
}
