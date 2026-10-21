// P359: temporary markers for exhausted lumber banks. The tree static stays in the client map.
using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Engines.Harvest;
using Server.Items;

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
                ClusterFLumberStumps.Mark(bank, map, loc);
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

        public static bool Enabled { get; set; } = true;

        // Zero picks a random stock stump per tree; a fixed ID is restricted to decimal 3670–3673.
        public static int StumpItemId
        {
            get => _stumpItemId;
            set => _stumpItemId = value == 0 ? 0 : Math.Clamp(value, 0xE56, 0xE59);
        }

        public static void Configure()
        {
            Enabled = ServerConfiguration.GetOrUpdateSetting("clusterf.lumber.stumps", true);
            StumpItemId = ServerConfiguration.GetOrUpdateSetting("clusterf.lumber.stumpItemId", 0);
        }

        public static void Mark(HarvestBank bank, Map map, Point3D target)
        {
            if (!Enabled || bank == null || bank.Definition.Skill != SkillName.Lumberjacking ||
                map == null || map == Map.Internal || bank.Current >= bank.Definition.ConsumedPerHarvest ||
                bank.NextRespawn <= Core.Now || _stumps.ContainsKey(bank))
            {
                return;
            }

            // Only the final targeted tree: never enumerate all statics in the 4 by 3 bank.
            var stump = new ChoppedTreeStump(bank);
            _stumps.Add(bank, stump);
            stump.MoveToWorld(target, map);
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
}

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class ChoppedTreeStump : Item, IChoppable
    {
        private readonly Engines.Harvest.HarvestBank _bank;
        private readonly Timer _expiry;

        internal ChoppedTreeStump(Engines.Harvest.HarvestBank bank) : base(
            ClusterFLumberStumps.StumpItemId == 0 ? Utility.RandomMinMax(0xE56, 0xE59) : ClusterFLumberStumps.StumpItemId)
        {
            _bank = bank;
            Movable = false;
            // Deadline belongs to the FIRST chop, not the swing that used the last wood.
            _expiry = Timer.DelayCall(bank.NextRespawn - Core.Now, Delete);
        }

        public override string DefaultName => "This tree has been chopped clean.";
        public override bool Decays => false;
        public override bool SkipSerialization => true;

        public override void OnSingleClick(Mobile from) => LabelTo(from, DefaultName);
        public override void OnDoubleClick(Mobile from) => OnChop(from);

        // HarvestTarget routes an equipped axe aimed at an IChoppable here as well.
        public void OnChop(Mobile from)
        {
            if (Deleted)
            {
                return;
            }

            if (from.Map != Map || !from.InRange(Location, 2))
            {
                from.SendLocalizedMessage(500446); // That is too far away.
            }
            else if (from.Weapon is not BaseAxe)
            {
                from.SendLocalizedMessage(500487); // The axe must be equipped for any serious wood chopping.
            }
            else
            {
                from.SendLocalizedMessage(500493); // There's not enough wood here to harvest.
            }
        }

        public override void OnAfterDelete()
        {
            _expiry?.Stop();
            ClusterFLumberStumps.Forget(_bank, this);
            base.OnAfterDelete();
        }

        // SkipSerialization excludes these from normal world saves. Defensive for a forced/legacy load.
        [AfterDeserialization]
        private void AfterDeserialization() => Delete();
    }
}
