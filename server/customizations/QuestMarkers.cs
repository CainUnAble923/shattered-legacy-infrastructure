// =============================================================================
// QuestMarkers.cs
// cc-P363: RedRooster's "!" and "?" over an NPC that has something for this player. "!" offers something new; "?" means
// something is ready to hand in. Each player sees their own: the marker is an effect sent to that player's client only
// (another player near the same NPC sees their own state, or nothing).
//
// How it draws. TazUO draws a fixed effect on a mobile at the mobile's tile and moves it with the mobile
// (FixedEffect.Update, GameEffectView.Draw: one depth above it), animating it from our animdata (frames 1,2,3,2). The
// art is stood 92 pixels up its canvas so the mark clears the head (registry.csv, SOURCE.txt). An effect lasts
// duration x 50 ms (EffectManager.CreateEffect), so every PulseSeconds each player is sent a fresh one that lasts just
// past the next pulse: a marker that changes ("!" to "?", or gone) changes within one pulse.
//
// Where the state comes from (read-only checks; nothing here creates records or rolls offers):
//   League Registrar     "!" not registered with the League, or a mule errand can be taken now (MuleErrand.RefuseOffer)
//                        "?" a promotion is ready (ClusterFLeagueRanks.Evaluate().CanPromote), or the mule is delivered
//   Rook Hallowell       "!" a tutorial lesson is next (in the tutorial, core not done), or a skipped tutorial to resume
//                        "?" the core is done (graduate), or the graduation companion waits to be chosen
//   Wren Tallow (farm)   "!" the farm's first step (find the pet); "?" the pests are cleared, so report back
//   Guildmasters         for a member of that guild: "!" a work order can be accepted (ClusterFWorkOrderSystem), and for
//                        the Society of Smiths a bulk order or commission can be asked for; "?" a work order's
//                        requirements are met, a completed smith BOD is in the pack, or an item fills a commission
// Trials finish on the spot (TutorialCoreTrials.Observe, TutorialWingTrials.Finish), so no trial NPC has a "?" of its
// own; the next lesson is Rook's "!". Other systems add a source with QuestMarkers.Sources.Add.
//
// Setting: clusterf.questMarkers (default on). Off, no marker is sent; the ones on screen fade within a pulse.
// =============================================================================

using System;
using System.Collections.Generic;
using Server.Accounting;
using Server.Engines.BulkOrders;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Tutorial;

namespace Server.Engines.QuestMarkers
{
    public enum QuestMarkerState
    {
        None,
        Offered,
        Ready
    }

    public static class QuestMarkers
    {
        public const string SettingKey = "clusterf.questMarkers";
        public const int Range = 14;
        public const double PulseSeconds = 3.0;

        // TazUO: duration units of 50 ms; speed x 10 x the animdata interval (3) is 300 ms a frame.
        public const int EffectDuration = (int)(PulseSeconds * 20) + 4;
        public const int EffectSpeed = 10;

        public static bool Enabled => ServerConfiguration.GetOrUpdateSetting(SettingKey, true);

        /// <summary>Where a state comes from: one function per system. Ready beats Offered across sources.</summary>
        public static readonly List<Func<PlayerMobile, Mobile, QuestMarkerState>> Sources = [BuiltIn];

        /// <summary>Test seam: each marker as it is sent (player, NPC, item ID).</summary>
        public static event Action<PlayerMobile, Mobile, int> Sent;

        private static Timer _timer;

        public static void Initialize()
        {
            _ = Enabled; // writes the default into the settings file on first start
            _timer ??= Timer.DelayCall(TimeSpan.FromSeconds(PulseSeconds), TimeSpan.FromSeconds(PulseSeconds), Pulse);
        }

        public static int ItemIdFor(QuestMarkerState state) => state switch
        {
            QuestMarkerState.Offered => ShardItemArt.QuestOfferedMarker,
            QuestMarkerState.Ready   => ShardItemArt.QuestReadyMarker,
            _                        => 0
        };

        public static QuestMarkerState StateFor(PlayerMobile pm, Mobile npc)
        {
            if (pm == null || npc == null || npc.Deleted || npc is PlayerMobile)
            {
                return QuestMarkerState.None;
            }

            var best = QuestMarkerState.None;

            foreach (var source in Sources)
            {
                QuestMarkerState s;

                try
                {
                    s = source(pm, npc);
                }
                catch (Exception e)
                {
                    // One broken source must not take every marker down with it.
                    Console.WriteLine($"QuestMarkers: a source failed for {npc.GetType().Name}: {e.Message}");
                    continue;
                }

                if (s > best)
                {
                    best = s;
                }
            }

            return best;
        }

        private static void Pulse()
        {
            if (!Enabled)
            {
                return;
            }

            foreach (var ns in NetState.Instances)
            {
                if (ns.Mobile is PlayerMobile pm)
                {
                    ShowTo(pm);
                }
            }
        }

        /// <summary>Sends this player a marker over every NPC near them that has one for them; returns what was sent.</summary>
        public static List<(Mobile Npc, int ItemId)> ShowTo(PlayerMobile pm)
        {
            var shown = new List<(Mobile, int)>();
            var map = pm?.Map;

            if (map == null || map == Map.Internal || pm.Deleted || !Enabled)
            {
                return shown;
            }

            foreach (var m in map.GetMobilesInRange(pm.Location, Range))
            {
                if (m is not BaseCreature || !pm.CanSee(m))
                {
                    continue;
                }

                var id = ItemIdFor(StateFor(pm, m));

                if (id != 0)
                {
                    Send(pm, m, id);
                    shown.Add((m, id));
                }
            }

            return shown;
        }

        private static void Send(PlayerMobile pm, Mobile npc, int itemId)
        {
            var ns = pm.NetState;

            if (ns != null)
            {
                Span<byte> effect = stackalloc byte[OutgoingEffectPackets.HuedEffectLength].InitializePacket();
                OutgoingEffectPackets.CreateTargetHuedEffect(effect, npc, itemId, EffectSpeed, EffectDuration, 0, 0);
                ns.Send(effect);
            }

            Sent?.Invoke(pm, npc, itemId);
        }

        // -- The shard's own sources ------------------------------------------------------------------

        private static QuestMarkerState BuiltIn(PlayerMobile pm, Mobile npc) => npc switch
        {
            LeagueRegistrar    => League(pm),
            TutorialRecruiter  => Rook(pm),
            TutorialFarmhand   => Farmhand(pm),
            BaseCreature       => Guildmaster(pm, npc),
            _                  => QuestMarkerState.None
        };

        private static ClusterFAccountData Data(PlayerMobile pm) =>
            pm.Account is IAccount acct ? ClusterFAccountPersistence.Get(acct) : null;

        public static QuestMarkerState League(PlayerMobile pm)
        {
            var data = Data(pm);

            if (data == null || !ClusterFLeagueSystem.IsJoined(data))
            {
                return QuestMarkerState.Offered;
            }

            var stage = data.GetLeagueData(pm.Serial)?.MuleErrandStage ?? MuleErrand.NotTaken;

            if (stage == MuleErrand.Delivered || ClusterFLeagueRanks.Evaluate(pm).CanPromote)
            {
                return QuestMarkerState.Ready;
            }

            return stage == MuleErrand.NotTaken && MuleErrand.RefuseOffer(pm) == null
                ? QuestMarkerState.Offered
                : QuestMarkerState.None;
        }

        public static QuestMarkerState Rook(PlayerMobile pm)
        {
            var r = Data(pm)?.Tutorial;

            if (r == null)
            {
                return QuestMarkerState.None;
            }

            if (ClusterFTutorial.CompanionPending(r))
            {
                return QuestMarkerState.Ready;
            }

            if (r.State == TutorialState.Skipped)
            {
                return QuestMarkerState.Offered;
            }

            if (!r.IsActive || r.CharacterSerial != (uint)pm.Serial)
            {
                return QuestMarkerState.None;
            }

            return ClusterFTutorial.CoreDone(r) ? QuestMarkerState.Ready : QuestMarkerState.Offered;
        }

        public static QuestMarkerState Farmhand(PlayerMobile pm) => TutorialFarm.StepOf(pm) switch
        {
            1 => QuestMarkerState.Offered,
            3 => QuestMarkerState.Ready,
            _ => QuestMarkerState.None
        };

        public static QuestMarkerState Guildmaster(PlayerMobile pm, Mobile npc)
        {
            var def = ClusterFGuildSystem.GetDefForGuildmaster(npc.GetType());

            if (def == null || !ClusterFGuildSystem.IsJoined(pm, def.Key))
            {
                return QuestMarkerState.None;
            }

            var data = Data(pm);
            var guild = data?.GetGuildData(pm.Serial);

            if (data == null || guild == null)
            {
                return QuestMarkerState.None;
            }

            var offered = false;

            foreach (var e in guild.ActiveWorkOrders)
            {
                if (e.GuildKey.Equals(def.Key, StringComparison.OrdinalIgnoreCase) &&
                    ClusterFWorkOrderSystem.Get(e.DefKey)?.RequirementsMet(pm, e) == true)
                {
                    return QuestMarkerState.Ready;
                }
            }

            if (npc is BlacksmithGuildmaster)
            {
                if (HasCompleteSmithOrder(pm.Backpack) || FillsACommission(pm, guild))
                {
                    return QuestMarkerState.Ready;
                }

                var (small, large) = BlacksmithGuildmaster.CountBODs(pm);
                offered = small < BlacksmithGuildmaster.SmallBODCap ||
                          large < BlacksmithGuildmaster.LargeBODCap && BlacksmithGuildmaster.MeetsLargeSkill(pm) ||
                          guild.SmithCommissions.Count < SmithCommissionSystem.MaxActiveSmallCommissions &&
                          SmithCommissionSystem.RequestCooldownLeft(pm) <= TimeSpan.Zero;
            }

            if (!offered)
            {
                foreach (var d in ClusterFWorkOrderSystem.GetForGuild(def.Key))
                {
                    if (ClusterFWorkOrderSystem.CanAccept(guild, d.Key, def.Key) && d.IsEligible(pm, data))
                    {
                        offered = true;
                        break;
                    }
                }
            }

            return offered ? QuestMarkerState.Offered : QuestMarkerState.None;
        }

        private static bool HasCompleteSmithOrder(Container pack)
        {
            if (pack == null)
            {
                return false;
            }

            foreach (var item in pack.FindItems())
            {
                if (item is SmallSmithBOD { Complete: true } or LargeSmithBOD { Complete: true })
                {
                    return true;
                }
            }

            return false;
        }

        private static bool FillsACommission(PlayerMobile pm, CharacterGuildData guild)
        {
            if (pm.Backpack == null || guild.SmithCommissions.Count == 0)
            {
                return false;
            }

            foreach (var item in pm.Backpack.FindItems())
            {
                if (SmithCommissionSystem.FindMatch(guild, item) != null)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
