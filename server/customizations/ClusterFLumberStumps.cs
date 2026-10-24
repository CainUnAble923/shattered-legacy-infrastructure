// P359: temporary markers for exhausted lumber banks. The tree static stays in the client map.
// P412: the stump sits at the tree static's own base Z, and our TazUO (client/patches/0006-exhausted-trees.patch) is
// told which tree statics to hide until the bank refills: packet 0xF4 sub 0x05, see ClusterFExhaustedTrees below.
using System;
using System.Buffers;
using System.Collections.Generic;
using ModernUO.CodeGeneratedEvents;
using ModernUO.Serialization;
using Server.Engines.Harvest;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
using Server.Targeting;

namespace Server.Engines.Harvest
{
    public partial class Lumberjacking
    {
        public override void OnHarvestFinished(
            Mobile from, Item tool, HarvestDefinition def, HarvestVein vein,
            HarvestBank bank, HarvestResource resource, object harvested
        )
        {
            base.OnHarvestFinished(from, tool, def, vein, bank, resource, harvested);

            if (def == Definitions[0] &&
                GetHarvestDetails(from, tool, harvested, out var tile, out var map, out var loc, out var isLand) &&
                !isLand && def.Validate(tile, false) && ReferenceEquals(bank, def.GetBank(map, loc.X, loc.Y)))
            {
                if (harvested is StaticTarget)
                {
                    ClusterFLumberStumps.MarkMapTree(bank, map, loc, tile);
                }
                else
                {
                    ClusterFLumberStumps.Mark(bank, map, loc);
                }
            }
        }
    }
}

namespace Server
{
    public static class ClusterFLumberStumps
    {
        private static readonly Dictionary<HarvestBank, ChoppedTreeStump> _stumps = new();
        private static int _stumpItemId;

        // P412: only statics this far above the tree's base count as its foliage, so a roof or a ledge stacked on the
        // same tile is never hidden. The tallest stock trunk is 20 high (tiledata 0x0CCA to 0x0CCD).
        public const int FoliageReach = 30;

        public static bool Enabled { get; set; } = true;

        // Zero picks a random stock stump per tree; a fixed ID is restricted to decimal 3670–3673.
        public static int StumpItemId
        {
            get => _stumpItemId;
            set => _stumpItemId = value == 0 ? 0 : Math.Clamp(value, 0xE56, 0xE59);
        }

        // P412: the map statics at one tile, as (item ID, Z). A seam for the test host, which loads no map files.
        public static Func<Map, int, int, IEnumerable<(int ItemID, int Z)>> StaticsAt { get; set; } = ReadStatics;

        public static void Configure()
        {
            Enabled = ServerConfiguration.GetOrUpdateSetting("clusterf.lumber.stumps", true);
            StumpItemId = ServerConfiguration.GetOrUpdateSetting("clusterf.lumber.stumpItemId", 0);
        }

        // The live markers, for a client that logs in (ClusterFExhaustedTrees.BuildLoginPackets).
        public static IEnumerable<ChoppedTreeStump> Stumps => _stumps.Values;

        public static void Mark(HarvestBank bank, Map map, Point3D target) => Place(bank, map, target, null);

        // P412: a tree from the map. Pinned StaticTarget raised the target's Z by the tile's CalcHeight
        // (Projects/Server/Targeting/StaticTarget.cs:11 and :18), so the stump goes to the static's own Z.
        public static void MarkMapTree(HarvestBank bank, Map map, Point3D target, int itemId)
        {
            if (bank == null || map == null || map == Map.Internal)
            {
                return;
            }

            var baseZ = ResolveTree(bank.Definition, map, target, itemId, out var ids);
            Place(bank, map, new Point3D(target.X, target.Y, baseZ), ids);
        }

        /// <summary>
        /// The base Z of the tree at the target, and the item IDs at that tile that make it up (its trunk and the
        /// foliage over it). The static is the one with the target's item ID whose Z plus its height is the target's
        /// Z; leaves resolve to the trunk under them. With no such static (map data missing) the height comes off.
        /// </summary>
        public static int ResolveTree(HarvestDefinition def, Map map, Point3D target, int itemId, out int[] ids)
        {
            var expected = target.Z - TileData.ItemTable[itemId & TileData.MaxItemValue].CalcHeight;
            var statics = new List<(int ItemID, int Z)>();
            foreach (var s in StaticsAt(map, target.X, target.Y))
            {
                statics.Add((s.ItemID & TileData.MaxItemValue, s.Z));
            }

            int? matched = null;
            foreach (var s in statics)
            {
                if (s.ItemID == itemId &&
                    (matched == null || Math.Abs(s.Z - expected) < Math.Abs(matched.Value - expected)))
                {
                    matched = s.Z;
                }
            }

            var baseZ = matched ?? expected;

            if (IsFoliage(itemId))
            {
                int? trunk = null;
                foreach (var s in statics)
                {
                    if (!IsFoliage(s.ItemID) && def.Validate(s.ItemID, false) && s.Z <= baseZ &&
                        (trunk == null || s.Z > trunk))
                    {
                        trunk = s.Z;
                    }
                }

                baseZ = trunk ?? baseZ;
            }

            var found = new List<int>();
            foreach (var s in statics)
            {
                if (s.Z >= baseZ && s.Z <= baseZ + FoliageReach &&
                    (def.Validate(s.ItemID, false) || IsFoliage(s.ItemID)) && !found.Contains(s.ItemID))
                {
                    found.Add(s.ItemID);
                }
            }

            ids = found.ToArray();
            return baseZ;
        }

        private static bool IsFoliage(int itemId) =>
            (TileData.ItemTable[itemId & TileData.MaxItemValue].Flags & TileFlag.Foliage) != 0;

        private static IEnumerable<(int ItemID, int Z)> ReadStatics(Map map, int x, int y)
        {
            var list = new List<(int ItemID, int Z)>();
            foreach (var tile in map.Tiles.GetStaticTiles(x, y))
            {
                list.Add((tile.ID, tile.Z));
            }

            return list;
        }

        private static void Place(HarvestBank bank, Map map, Point3D target, int[] treeIds)
        {
            if (!Enabled || bank == null || bank.Definition.Skill != SkillName.Lumberjacking ||
                map == null || map == Map.Internal || bank.Current >= bank.Definition.ConsumedPerHarvest ||
                bank.NextRespawn <= Core.Now || _stumps.ContainsKey(bank))
            {
                return;
            }

            // Only the final targeted tree: never enumerate all statics in the 4 by 3 bank.
            var stump = new ChoppedTreeStump(bank, treeIds, map, target);
            _stumps.Add(bank, stump);
            stump.MoveToWorld(target, map);
            ClusterFExhaustedTrees.Broadcast(stump, true);
        }

        // Called by HarvestBank only after a real refill, including lazy refill on Current/Vein/Consume.
        public static void OnBankRespawn(HarvestBank bank)
        {
            if (_stumps.Remove(bank, out var stump))
            {
                stump.Delete();
            }
        }

        internal static void Forget(HarvestBank bank, ChoppedTreeStump stump)
        {
            if (bank != null && _stumps.TryGetValue(bank, out var current) && ReferenceEquals(current, stump))
            {
                _stumps.Remove(bank);
            }
        }
    }

    /// <summary>
    /// P412: tells clients which map tree statics are exhausted, on the shard's own packet 0xF4 (the fog of war's,
    /// ClusterFExplorationPackets), as a new sub 0x05. Our TazUO already routes 0xF4 (client patch 0004); patch 0006
    /// keeps the list and hides those statics. Every client already gets 0xF4 at login, so a stock client is unharmed.
    ///   [0xF4][u16 length][u8 0x05][u8 facet][u32 BE payload length] then per tree:
    ///   [u16 x][u16 y][u32 seconds until the bank refills, 0 = it regrew][u8 n][n x u16 item ID]
    /// A change goes to every client online, whatever its distance or facet: the client keeps the list for every
    /// facet, so moving out of range and back needs nothing new. A login gets every live tree, and a client drops an
    /// entry at its deadline by itself, so a missed regrowth never leaves a tree hidden past it.
    /// </summary>
    public static class ClusterFExhaustedTrees
    {
        private static readonly ILogger logger = LogFactory.GetLogger(typeof(ClusterFExhaustedTrees));

        public const byte PacketId = 0xF4;
        public const byte Sub = 0x05;
        private const int HeaderLength = 1 + 2 + 1 + 1 + 4;
        private const int MaxPacket = 0xFF00;

        /// <summary>Each sub 0x05 packet built for a change, as it goes out (the facts read it).</summary>
        public static event Action<byte[]> Broadcasting;

        public static int SecondsLeft(ChoppedTreeStump stump) =>
            (int)Math.Clamp(Math.Ceiling((stump.Deadline - Core.Now).TotalSeconds), 1, int.MaxValue);

        public static byte[] BuildPacket(int facet, IReadOnlyList<(int X, int Y, int Seconds, int[] Ids)> trees)
        {
            var length = HeaderLength;
            foreach (var t in trees)
            {
                length += 9 + 2 * t.Ids.Length;
            }

            var buffer = new byte[length];
            var writer = new SpanWriter(buffer);
            writer.Write(PacketId);
            writer.Write((ushort)length);
            writer.Write(Sub);
            writer.Write((byte)facet);
            writer.Write((uint)(length - HeaderLength));
            foreach (var t in trees)
            {
                writer.Write((ushort)t.X);
                writer.Write((ushort)t.Y);
                writer.Write((uint)t.Seconds);
                writer.Write((byte)t.Ids.Length);
                foreach (var id in t.Ids)
                {
                    writer.Write((ushort)id);
                }
            }

            return buffer;
        }

        public static void Broadcast(ChoppedTreeStump stump, bool exhausted)
        {
            if (stump.TreeIds.Length == 0 || stump.TreeMap == null || stump.TreeMap == Map.Internal)
            {
                return; // a dynamic Static tree, or a tile with no map tree: nothing for a client to hide
            }

            var seconds = exhausted ? SecondsLeft(stump) : 0;
            var packet = BuildPacket(stump.TreeMap.MapID, [(stump.TreeX, stump.TreeY, seconds, stump.TreeIds)]);
            Broadcasting?.Invoke(packet);

            var sent = 0;
            foreach (var ns in NetState.Instances)
            {
                if (ns.Mobile is PlayerMobile && !ns.CannotSendPackets())
                {
                    ns.Send(packet);
                    sent++;
                }
            }

            logger.Information(
                "Exhausted tree {State}: facet {Facet} at {X},{Y}, item IDs {Ids}, refill deadline {Deadline} ({Seconds} s), sent to {Clients} client(s)",
                exhausted ? "hidden" : "regrown", stump.TreeMap.MapID, stump.TreeX, stump.TreeY,
                string.Join(",", Array.ConvertAll(stump.TreeIds, id => $"0x{id:X4}")),
                stump.Deadline.ToString("u"), seconds, sent
            );
        }

        /// <summary>Every live exhausted map tree, one packet per facet (more if one would pass 65,280 bytes).</summary>
        public static List<byte[]> BuildLoginPackets()
        {
            var byFacet = new SortedDictionary<int, List<(int X, int Y, int Seconds, int[] Ids)>>();
            foreach (var stump in ClusterFLumberStumps.Stumps)
            {
                if (stump.Deleted || stump.TreeIds.Length == 0 || stump.TreeMap == null || stump.TreeMap == Map.Internal)
                {
                    continue;
                }

                if (!byFacet.TryGetValue(stump.TreeMap.MapID, out var list))
                {
                    byFacet[stump.TreeMap.MapID] = list = new List<(int X, int Y, int Seconds, int[] Ids)>();
                }

                list.Add((stump.TreeX, stump.TreeY, SecondsLeft(stump), stump.TreeIds));
            }

            var packets = new List<byte[]>();
            foreach (var (facet, trees) in byFacet)
            {
                var chunk = new List<(int X, int Y, int Seconds, int[] Ids)>();
                var length = HeaderLength;
                foreach (var t in trees)
                {
                    var size = 9 + 2 * t.Ids.Length;
                    if (chunk.Count > 0 && length + size > MaxPacket)
                    {
                        packets.Add(BuildPacket(facet, chunk));
                        chunk = new List<(int X, int Y, int Seconds, int[] Ids)>();
                        length = HeaderLength;
                    }

                    chunk.Add(t);
                    length += size;
                }

                packets.Add(BuildPacket(facet, chunk));
            }

            return packets;
        }

        // DoLogin raises this last, after EnterWorld (where the client empties its list) and once packets flow (cc-P165).
        [OnEvent(nameof(PlayerMobile.PlayerLoginEvent))]
        public static void OnLogin(PlayerMobile pm)
        {
            if (pm?.NetState is not { } ns || ns.CannotSendPackets())
            {
                return;
            }

            var packets = BuildLoginPackets();
            foreach (var packet in packets)
            {
                ns.Send(packet);
            }

            if (packets.Count > 0)
            {
                logger.Information("Exhausted trees: {Count} packet(s) sent to {Name} at login", packets.Count, pm.Name);
            }
        }
    }
}

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class ChoppedTreeStump : Item, IChoppable
    {
        private static readonly int[] NoTree = [];

        private readonly Engines.Harvest.HarvestBank _bank;
        private readonly Timer _expiry;

        internal ChoppedTreeStump(
            Engines.Harvest.HarvestBank bank, int[] treeIds = null, Map treeMap = null, Point3D tree = default
        ) : base(ClusterFLumberStumps.StumpItemId == 0 ? Utility.RandomMinMax(0xE56, 0xE59) : ClusterFLumberStumps.StumpItemId)
        {
            _bank = bank;
            TreeIds = treeIds ?? NoTree;
            TreeMap = treeMap;
            TreeX = tree.X;
            TreeY = tree.Y;
            Deadline = bank.NextRespawn;
            Movable = false;
            // Deadline belongs to the FIRST chop, not the swing that used the last wood.
            _expiry = Timer.DelayCall(bank.NextRespawn - Core.Now, Delete);
        }

        // P412: the map tree clients were told to hide, kept apart from Map and Location, which are gone by
        // OnAfterDelete. No IDs for a dynamic Static tree.
        public int[] TreeIds { get; } = NoTree;
        public Map TreeMap { get; }
        public int TreeX { get; }
        public int TreeY { get; }
        public DateTime Deadline { get; }

        public override string DefaultName => "This tree has been chopped clean.";
        public override bool Decays => false;
        public override bool SkipSerialization => true;

        public override void OnSingleClick(Mobile from) => LabelTo(from, DefaultName);
        public override void OnDoubleClick(Mobile from) => OnChop(from);

        // HarvestTarget routes an equipped lumber tool aimed at an IChoppable here as well.
        public void OnChop(Mobile from)
        {
            if (!Deleted)
            {
                from.SendLocalizedMessage(RefusalFor(from));
            }
        }

        // P412: any equipped tool whose harvest system is Lumberjacking (an axe, or a polearm: pinned
        // BasePoleArm.cs:32) gets the no-wood message, not the equip-an-axe one.
        public int RefusalFor(Mobile from)
        {
            if (from.Map != Map || !from.InRange(Location, 2))
            {
                return 500446; // That is too far away.
            }

            var lumberTool = from.Weapon is BaseAxe { HarvestSystem: Lumberjacking } or
                BasePoleArm { HarvestSystem: Lumberjacking };

            return lumberTool
                ? 500493  // There's not enough wood here to harvest.
                : 500487; // The axe must be equipped for any serious wood chopping.
        }

        public override void OnAfterDelete()
        {
            _expiry?.Stop();
            ClusterFLumberStumps.Forget(_bank, this);
            ClusterFExhaustedTrees.Broadcast(this, false);
            base.OnAfterDelete();
        }

        // SkipSerialization excludes these from normal world saves. Defensive for a forced/legacy load.
        [AfterDeserialization]
        private void AfterDeserialization() => Delete();
    }
}
