// cc-P808: the Tinkers' Union build quest for the Clockwork Scavenger (Chase, 2026-11-07). See ClockworkScavenger.cs.

using System;
using System.Linq;
using Server.Collections;
using Server.ContextMenus;
using Server.Items;

namespace Server.Mobiles
{
    public enum ScavengerQuestResult
    {
        NotMember,
        RankTooLow,
        HasScavenger,
        HasFrame,
        NoRoom,
        FrameGiven
    }

    public static class ClockworkScavengerQuest
    {
        public const string GuildKey = "tinkers";

        public static ClockworkScavenger ScavengerOf(Mobile m) => ClockworkScavenger.All.FirstOrDefault(s => s.Builder == m);

        public static ClockworkScavengerFrame FrameOf(Mobile m) => ClockworkScavengerFrame.All.FirstOrDefault(f => f.Owner == m);

        public static int RankOf(PlayerMobile pm) =>
            ClusterFAccountPersistence.GetGuild(pm) is { } data ? ClusterFGuildSystem.GuildRankIndex(GuildKey, data) : -1;

        public static string PartsText() =>
            string.Join(", ", ClockworkScavengerConfig.Current.Parts.Select(p => $"{p.Amount} {p.Label}"));

        /// <summary>The guildmaster's answer to "scavenger": the frame for a Journeyman who has neither frame nor Scavenger.</summary>
        public static ScavengerQuestResult Talk(PlayerMobile pm, Mobile guildmaster)
        {
            var minRank = ClockworkScavengerConfig.Current.MinRank;
            ScavengerQuestResult result;
            string line;

            if (!ClusterFGuildSystem.IsJoined(pm, GuildKey))
            {
                result = ScavengerQuestResult.NotMember;
                line = "The Clockwork Scavenger is a Union design. Join the Tinkers' Union first.";
            }
            else if (RankOf(pm) < minRank)
            {
                result = ScavengerQuestResult.RankTooLow;
                line = $"Come back when you are {ClusterFGuildSystem.RankIndexName(minRank)} in the Union, and I will show you the Scavenger.";
            }
            else if (ScavengerOf(pm) != null)
            {
                result = ScavengerQuestResult.HasScavenger;
                line = "You already have a Scavenger. One to a tinker.";
            }
            else if (FrameOf(pm) != null)
            {
                result = ScavengerQuestResult.HasFrame;
                line = $"You have the frame. Bring {PartsText()}, then use the frame with the parts in your pack.";
            }
            else
            {
                var frame = new ClockworkScavengerFrame(pm);

                if (pm.Backpack?.TryDropItem(pm, frame, false) != true)
                {
                    frame.Delete();
                    result = ScavengerQuestResult.NoRoom;
                    line = "Make room in your pack, and I will give you the frame.";
                }
                else
                {
                    result = ScavengerQuestResult.FrameGiven;
                    line = $"Here is a Scavenger frame. It wants {PartsText()}. Use the frame with the parts in your pack and it is yours: " +
                           "it picks gold and such from what you kill.";
                }
            }

            guildmaster?.SayTo(pm, line);

            if (guildmaster == null)
            {
                pm.SendMessage(line);
            }

            return result;
        }

        /// <summary>Uses the frame: every part from the pack, then the Scavenger, controlled by its builder. Nothing is used on a refusal.</summary>
        public static ClockworkScavenger Assemble(PlayerMobile pm, ClockworkScavengerFrame frame)
        {
            var c = ClockworkScavengerConfig.Current;
            var pack = pm.Backpack;

            if (frame.Deleted || frame.Owner != pm)
            {
                pm.SendMessage(0x22, "That frame is bound to another tinker.");
                return null;
            }

            if (pack == null || !frame.IsChildOf(pack))
            {
                pm.SendLocalizedMessage(1042001); // That must be in your pack for you to use it.
                return null;
            }

            if (ScavengerOf(pm) != null)
            {
                pm.SendMessage(0x22, "You already have a Scavenger.");
                return null;
            }

            if (pm.Followers + c.ControlSlots > pm.FollowersMax)
            {
                pm.SendLocalizedMessage(1049607); // You have too many followers to control that creature.
                return null;
            }

            var types = c.Parts.Select(ClockworkScavengerConfig.PartType).ToArray();
            var amounts = c.Parts.Select(p => p.Amount).ToArray();
            var missing = c.Parts.Where((p, i) => types[i] == null || pack.GetAmount(types[i]) < p.Amount).ToList();

            if (missing.Count > 0)
            {
                pm.SendMessage(0x22, $"The frame still needs: {string.Join(", ", missing.Select(p => $"{p.Amount} {p.Label}"))}.");
                return null;
            }

            if (pack.ConsumeTotal(types, amounts) != -1)
            {
                pm.SendMessage(0x22, "The parts would not fit together. Nothing was used.");
                return null;
            }

            var s = new ClockworkScavenger(pm);

            if (!s.SetControlMaster(pm))
            {
                s.Delete();
                return null;
            }

            s.ControlOrder = OrderType.Follow;
            s.ControlTarget = pm;
            s.MoveToWorld(pm.Location, pm.Map);
            frame.Delete();
            pm.PlaySound(0x241);
            pm.SendMessage(0x44, "The Clockwork Scavenger clicks to life. Set what it collects in its Options.");
            return s;
        }

        public static void AddEntry(BaseGuildmaster gm, Mobile from, ref PooledRefList<ContextMenuEntry> list)
        {
            if (gm is TinkerGuildmaster && from is PlayerMobile && ClusterFGuildSystem.IsJoined(from, GuildKey))
            {
                list.Add(new QuestEntry(gm));
            }
        }

        private sealed class QuestEntry : ContextMenuEntry
        {
            private readonly Mobile _gm;

            public QuestEntry(Mobile gm) : base(3006156, 4) => _gm = gm; // "Quest Conversation"

            public override void OnClick(Mobile from, IEntity target)
            {
                if (from is PlayerMobile pm && !_gm.Deleted)
                {
                    Talk(pm, _gm);
                }
            }
        }
    }
}
