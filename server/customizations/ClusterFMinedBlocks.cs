// ClusterFMinedBlocks.cs
//
// cc-P238 (F-36, ours). The Prospector's Logbook's record per mined block: every 8 x 8 ore bank an account mines (facet,
// bank, first mined, ores found), counted as "blocks mined" in the logbook and on the Miners' Compact Liaison's climbing
// boots page, where it is one of the boots' gates. Account-backed in ClusterFAccountData.MinedBlocks (v22), like the rest
// of the logbook, so it survives loop resets.
//
// The bank is the TARGETED tile's, as pinned's harvest system takes it (HarvestSystem.FinishHarvesting: GetBank(map,
// loc.X, loc.Y)), not the miner's own tile, which can sit in the next bank over (Mining MaxRange 2). Mining.Give does not
// see the target, so MutateType (called with it just before the ore is made) notes it here, and the routing hook's Give
// (CompactOreSatchelRoutingHook.cs) records the block when the ore is handed over.

using System;
using System.Collections.Generic;
using Server.Accounting;
using Server.Items;
using Server.Mobiles;

namespace Server.Engines.Harvest
{
    public partial class Mining
    {
        public override Type MutateType(
            Type type, Mobile from, Item tool, HarvestDefinition def, Map map, Point3D loc, HarvestResource resource
        )
        {
            if (def == OreAndStone)
            {
                ClusterFMinedBlocks.Targeted(from, map, loc);
            }

            return base.MutateType(type, from, tool, def, map, loc, resource);
        }
    }
}

namespace Server
{
    public static class ClusterFMinedBlocks
    {
        // Pinned's ore banks are 8 x 8 (our Mining.cs OreAndStone BankWidth/BankHeight).
        public const int BankSize = 8;

        // The tile each miner's current swing targeted, until its ore is handed over.
        private static readonly Dictionary<Mobile, (Map Map, Point3D Loc)> _targets = new();

        public static void Targeted(Mobile m, Map map, Point3D loc)
        {
            if (m is PlayerMobile && map != null && map != Map.Internal)
            {
                _targets[m] = (map, loc);
            }
        }

        /// <summary>The ore key a block lists ("Iron", "DullCopper", ...): the ore type's name without "Ore".</summary>
        public static string OreKey(Item ore)
        {
            var name = ore.GetType().Name;
            return name.EndsWith("Ore", StringComparison.Ordinal) ? name[..^3] : name;
        }

        /// <summary>Called from Mining.Give with the ore about to be handed over. Records the targeted block.</summary>
        public static void OnGive(Mobile m, Item item)
        {
            if (item is not BaseOre || m is not PlayerMobile pm || !_targets.Remove(m, out var t))
            {
                return;
            }

            Record(pm, t.Map, t.Loc, OreKey(item));
        }

        /// <summary>Records ore found at a tile. Returns true when its block is new to the account.</summary>
        public static bool Record(PlayerMobile pm, Map map, Point3D loc, string ore)
        {
            if (pm.Account is not IAccount acct || map == null)
            {
                return false;
            }

            var data = ClusterFAccountPersistence.GetOrCreate(acct);
            var isNew = data.MinedBlocks.Record(map.Name, loc.X / BankSize, loc.Y / BankSize, ore, Core.Now);

            if (isNew && ClusterFClimbing.IsBlocksGate(data.MinedBlocks.Count))
            {
                pm.SendMessage(0x44, $"Your Prospector's Logbook now lists {data.MinedBlocks.Count:N0} blocks mined.");
            }

            return isNew;
        }

        public static int BlocksMined(IAccount acct) =>
            acct == null ? 0 : ClusterFAccountPersistence.GetOrCreate(acct).MinedBlocks.Count;
    }
}
