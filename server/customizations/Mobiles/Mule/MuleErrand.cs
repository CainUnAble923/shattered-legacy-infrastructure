// cc-P69, F-33 Part E: the Compact's Mule, a League errand that grants the base mule.
//
// Host: the League Registrar's own gump (LeagueRegistrarGump, "The Compact's Mule" page). It is the League's NPC in New
// Haven, it already reads the character's rank, and its Promote button set the pattern (every check again on the
// press). An MLQuest would add a quest-item toggle, a 10-quest cap and a reward that must fit the pack, and still leave
// the rank gate, one-per-character and the replacement to custom code.
//
// Steps, state on the character's League record (CharacterLeagueData v1: MuleErrandStage, MuleGranted):
//   0 -> 1  Take it at the Registrar: registered, League rank Dull Copper (2) or higher.
//   1 -> 2  Prove the trade: bring 50 iron ore and 25 iron ingots (pack and bank, satchels included) to the Registrar.
//   2 -> 3  Collect the mule: a bonded tier 1 Compact Mule at the player's feet, if a follower slot is free. Then the
//           first Haul contract (F-38, not built: FirstHaulContract is the hook; a line points at it meanwhile).
// One per character. A mule lost for good (released, or deleted) is replaced at the Registrar for a fee, never re-earned:
// only when the character owns no PackMule at all (following, ridden, stabled or auto-stabled).

using System;
using Server.Accounting;
using Server.Items;
using Server.Mobiles;

namespace Server
{
    public static class MuleErrand
    {
        public const int RankGate = 2; // Dull Copper: reachable in well under an hour (cc-P69 notes, section 6)
        public const int OreCount = 50;
        public const int IngotCount = 25;
        public const int ReplaceGold = 5_000;
        public const int ReplaceVouchers = 10;

        public const int NotTaken = 0;
        public const int Taken = 1;
        public const int Delivered = 2;
        public const int Done = 3;

        /// <summary>F-38's first Haul contract, when it is built. Until then the player is told where it will be.</summary>
        public static Action<PlayerMobile> FirstHaulContract;

        public const string HaulStubLine =
            "The Compact will have hauling work for you and your mule soon: watch the League's board for Haul contracts.";

        private static ClusterFAccountData Data(PlayerMobile pm) =>
            pm.Account is IAccount acct ? ClusterFAccountPersistence.GetOrCreate(acct) : null;

        public static CharacterLeagueData Record(PlayerMobile pm) => Data(pm)?.GetOrCreateLeagueData(pm.Serial);

        public static int Stage(PlayerMobile pm) => Record(pm)?.MuleErrandStage ?? NotTaken;

        /// <summary>Why the errand cannot be taken now, or null.</summary>
        public static string RefuseOffer(PlayerMobile pm)
        {
            var data = Data(pm);

            if (data == null || !ClusterFLeagueSystem.IsJoined(data))
            {
                return "Only League citizens are given League errands. Join first.";
            }

            if (ClusterFLeagueRanks.GetRank(pm) < RankGate)
            {
                return $"The Compact trusts its mules to League members of {ClusterFLeagueRanks.RankName(RankGate)} rank or higher.";
            }

            var record = data.GetOrCreateLeagueData(pm.Serial);
            return record.MuleGranted || record.MuleErrandStage != NotTaken ? "You have already taken this errand." : null;
        }

        public static bool Accept(PlayerMobile pm, out string message)
        {
            message = RefuseOffer(pm);

            if (message != null)
            {
                return false;
            }

            Record(pm).MuleErrandStage = Taken;
            message = $"The Compact wants proof you can work a vein: bring {OreCount} iron ore and {IngotCount} iron ingots here.";
            return true;
        }

        public static bool Deliver(PlayerMobile pm, out string message)
        {
            var record = Record(pm);

            if (record?.MuleErrandStage != Taken)
            {
                message = "You have nothing to deliver for this errand.";
                return false;
            }

            var guild = Data(pm).GetOrCreateGuildData(pm.Serial);

            // TryPay says what is short itself and takes nothing unless all of it is there.
            if (!GuildResources.TryPay(pm, guild, "mining", 0, "Mining Vouchers", 0,
                    GuildCost.Of<IronOre>(OreCount), GuildCost.Of<IronIngot>(IngotCount)))
            {
                message = null;
                return false;
            }

            record.MuleErrandStage = Delivered;
            message = "The Registrar stamps your requisition. Your mule is ready: collect it when you have a follower slot free.";
            return true;
        }

        public static PackMule Collect(PlayerMobile pm, out string message)
        {
            var record = Record(pm);

            if (record?.MuleErrandStage != Delivered)
            {
                message = "There is no mule waiting for you.";
                return null;
            }

            var mule = PackMule.SummonFor(pm, MuleData.MinTier, MuleGenes.Fresh(),
                "A Compact handler leads your new mule over. It is bonded to you and will follow you faithfully.");

            if (mule == null)
            {
                message = null; // SummonFor said why
                return null;
            }

            record.MuleErrandStage = Done;
            record.MuleGranted = true;

            if (FirstHaulContract != null)
            {
                FirstHaulContract(pm);
            }
            else
            {
                pm.SendMessage(0x59, HaulStubLine);
            }

            message = null;
            return mule;
        }

        /// <summary>Any PackMule this character has: following, ridden, stabled or auto-stabled.</summary>
        public static bool OwnsAMule(PlayerMobile pm)
        {
            if (pm.Mount is PackMule)
            {
                return true;
            }

            foreach (var set in new[] { pm.AllFollowers, pm.Stabled, pm.AutoStabled })
            {
                if (set == null)
                {
                    continue;
                }

                foreach (var m in set)
                {
                    if (m is PackMule { Deleted: false })
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static bool CanReplace(PlayerMobile pm) => Record(pm)?.MuleGranted == true && !OwnsAMule(pm);

        /// <summary>A new tier 1 mule for one lost for good, for a fee. Its old bloodline is not recovered.</summary>
        public static PackMule Replace(PlayerMobile pm, out string message)
        {
            message = null;

            if (!CanReplace(pm))
            {
                message = "The Compact replaces a mule only for one who has lost theirs.";
                return null;
            }

            // Checked before the fee is taken, so the summon after it cannot fail.
            if (!pm.Alive)
            {
                message = "You cannot take a mule while dead.";
                return null;
            }

            if (pm.Followers + 1 > pm.FollowersMax)
            {
                message = "You have too many followers for a mule. Stable or dismiss one first.";
                return null;
            }

            var guild = Data(pm).GetOrCreateGuildData(pm.Serial);

            if (!GuildResources.TryPay(pm, guild, "mining", ReplaceVouchers, "Mining Vouchers", ReplaceGold))
            {
                return null;
            }

            return PackMule.SummonFor(pm, MuleData.MinTier, MuleGenes.Fresh(),
                "The Compact finds you another mule. It is bonded to you and will follow you faithfully.");
        }
    }
}
