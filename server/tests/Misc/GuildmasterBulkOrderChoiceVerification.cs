// GuildmasterBulkOrderChoiceVerification.cs
//
// cc-P57 Part E (bug-list D81, Chase 2026-10-05). The Society guildmaster's single-click "Bulk Order Info" handed out a
// deed directly: the stock entry (pinned Mobiles/Vendors/BaseVendor.cs:1330-1332, BulkOrderInfoEntry) calls
// CreateBulkOrder, which the guildmaster overrides to TryCreateBOD. It now opens SmithBulkOrderChoiceGump, as cc-P55 Part H
// did for regular smiths. The same gump (Chase screenshot 08:07): the turn-in hint ran past the right edge, and it was
// translucent. Notes: shard-migration notes/cc-P57-batch-6.md, Part E.
//
// Facts:
//   E1. A member's guildmaster menu has "Bulk Order Info" (3006152) once; clicking it opens the Small/Large choice and
//       hands out no deed.
//   E2. Every label in the choice gump ends inside its frame (the frame's 9270 border is 10 pixels), measured in the
//       client's label font (GumpTextWidth), for each turn-in mode, teaching on and off, below and above 70.1.
//   E3. The choice gump is opaque: no alpha region, and the solid tile 2624 under the whole inside of the frame.

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Collections;
using Server.ContextMenus;
using Server.Engines.BulkOrders;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Tests.Network;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class GuildmasterBulkOrderChoiceVerification
{
    private readonly ITestOutputHelper _out;

    public GuildmasterBulkOrderChoiceVerification(ITestOutputHelper output)
    {
        _out = output;
        ShardTestHost.EnsureAccounts();
        ShardTestHost.EnsureSkillChecks();
        ShardTestHost.EnsureCraftSystems();
        ClusterFGuildSystem.EnsureRegistered();
    }

    private static readonly Point3D Spot = new(1540, 1540, 0);

    private sealed class Member : IDisposable
    {
        public readonly Account Account = new($"p57e{Guid.NewGuid():N}"[..16], "p57-test-only");
        public readonly PlayerMobile Pm;
        public readonly NetState Ns;

        public Member(double skill)
        {
            Pm = new PlayerMobile { Player = true, Race = Race.Human };
            Pm.AddItem(new Backpack());
            Pm.Skills.Blacksmith.Cap = 200.0;
            Pm.Skills.Blacksmith.Base = skill;
            Account[0] = Pm;
            Pm.MoveToWorld(Spot, Map.Trammel);
            Ns = PacketTestUtilities.CreateTestNetState();
            Ns.Account = Account;
            Pm.NetState = Ns;
            Ns.Mobile = Pm;
            ClusterFAccountPersistence.GetOrCreateGuild(Pm).JoinedGuilds.Add("smithing");
        }

        public void Dispose()
        {
            foreach (var item in Pm.Backpack.Items.ToList())
            {
                item.Delete();
            }

            Pm.NetState = null;
            Ns.Mobile = null;
            ClusterFAccountPersistence.Get(Account)?.ClearGuildData();
            Pm.Delete();
            Accounts.Remove(Account);
        }
    }

    private static ContextMenuEntry[] Entries(Mobile target, Mobile from)
    {
        var list = PooledRefList<ContextMenuEntry>.Create();
        try
        {
            target.GetContextMenuEntries(from, ref list);
            var result = new ContextMenuEntry[list.Count];
            for (var i = 0; i < list.Count; i++)
            {
                result[i] = list[i];
            }

            return result;
        }
        finally
        {
            list.Dispose();
        }
    }

    // ---------------------------------------------------------------- E1

    [Fact]
    public void BulkOrderInfoOpensTheChoiceAndHandsOutNoDeed()
    {
        using var m = new Member(100.0);
        var gm = new BlacksmithGuildmaster();
        try
        {
            gm.MoveToWorld(new Point3D(Spot.X + 1, Spot.Y, Spot.Z), Map.Trammel);
            var entries = Entries(gm, m.Pm);
            _out.WriteLine($"guildmaster: {string.Join(", ", entries.Select(e => $"{e.GetType().Name} {e.Number}"))}");

            var info = Assert.Single(entries, e => e.Number == 3006152);
            info.OnClick(m.Pm, gm);

            var (small, large) = BlacksmithGuildmaster.CountBODs(m.Pm);
            _out.WriteLine($"after the click: choice gump {m.Pm.FindGump<SmithBulkOrderChoiceGump>() != null}, " +
                           $"deeds {small} small {large} large, accept gump {m.Pm.FindGump<SmallBODAcceptGump>() != null}");
            Assert.NotNull(m.Pm.FindGump<SmithBulkOrderChoiceGump>());
            Assert.Equal((0, 0), (small, large));
            Assert.Null(m.Pm.FindGump<SmallBODAcceptGump>());
            Assert.Null(m.Pm.FindGump<LargeBODAcceptGump>());
        }
        finally
        {
            gm.Delete();
        }
    }

    // ---------------------------------------------------------------- E2, E3

    private IEnumerable<(string Name, SmithBulkOrderChoiceGump Gump)> EveryState(Member m)
    {
        foreach (var skill in new[] { 50.0, 100.0 })
        {
            m.Pm.Skills.Blacksmith.Base = skill;
            foreach (var teaching in new[] { false, true })
            {
                ClusterFSmithTeaching.SetWantsTeaching(m.Pm, teaching);
                foreach (var mode in new[] { SmithTurnInMode.Bank, SmithTurnInMode.CashOut, SmithTurnInMode.Ask })
                {
                    while (ClusterFSmithBODPayout.GetMode(m.Pm) != mode)
                    {
                        ClusterFSmithBODPayout.CycleMode(m.Pm);
                    }

                    yield return ($"skill {skill}, teaching {teaching}, {mode}", new SmithBulkOrderChoiceGump(m.Pm));
                    yield return ($"skill {skill}, teaching {teaching}, {mode}, with Back",
                        new SmithBulkOrderChoiceGump(m.Pm, _ => { }));
                }
            }
        }
    }

    [Fact]
    public void EveryLabelEndsInsideTheFrame()
    {
        using var m = new Member(100.0);
        var widest = (Text: "", Right: 0, Width: 0);
        foreach (var (name, g) in EveryState(m))
        {
            var frame = g.Entries.OfType<GumpBackground>().First();
            foreach (var label in g.Entries.OfType<GumpLabel>())
            {
                var right = label.X + GumpTextWidth.Label(label.Text);
                if (right > widest.Right)
                {
                    widest = (label.Text, right, frame.Width);
                }

                Assert.True(right <= frame.Width - 10,
                    $"{name}: \"{label.Text}\" ends at {right}, the frame's inside ends at {frame.Width - 10}");
            }
        }

        _out.WriteLine($"widest label: \"{widest.Text}\" ends at {widest.Right} of {widest.Width}");
        // The line Chase saw run off the edge is still the one drawn, whole.
        Assert.Equal("Bank: Seals. Cash out: gold, a chance at an item.", ClusterFSmithBODPayout.ToggleHint);
    }

    [Fact]
    public void TheChoiceGumpIsOpaque()
    {
        using var m = new Member(100.0);
        foreach (var (name, g) in EveryState(m))
        {
            var frame = g.Entries.OfType<GumpBackground>().First();
            Assert.Empty(g.Entries.OfType<GumpAlphaRegion>());
            var panel = g.Entries.OfType<GumpImageTiled>().FirstOrDefault(t => t.GumpID == 2624);
            Assert.NotNull(panel);
            Assert.Equal((8, 8, frame.Width - 16, frame.Height - 16), (panel.X, panel.Y, panel.Width, panel.Height));
        }

        _out.WriteLine("every state: tile 2624 under the inside of the frame, no alpha region");
    }
}
