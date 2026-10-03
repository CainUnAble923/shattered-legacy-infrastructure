// SmithBulkOrderChoiceVerification.cs
//
// cc-P42 Part F (Chase, 2026-10-03; bug-list D51). The Society of Smiths' Bulk Order button lets the player pick a
// small or a large order, and a large one is gated by Blacksmithy only (70.1), not by Journeyman rank. The button
// works from the guild page with no guildmaster (it said "Please speak with the Guildmaster directly." before), and
// a choice ends at pinned's own accept gump, as asking the guildmaster does. Caps (3 small, 1 large) stay.
// Notes: shard-migration notes/cc-P42-defect-batch-3.md, Part F.
//
// Facts:
//   1. At 70.0 Blacksmithy the choice has no Large button and says why; pressing Large anyway (as a client could)
//      makes no deed and says the skill message. TryCreateLargeBOD refuses the same way.
//   2. At 70.1 with 0 standing, Large from the choice opens the large accept gump with no NPC anywhere, and
//      accepting puts the deed in the backpack.
//   3. The caps still refuse: a second large with one active, a fourth small with three.
//   4. Small from the choice is always small: 40 requests at 120 skill, where the guildmaster's roll gives a large
//      about one time in four, make 40 small deeds (each cancelled at the accept gump).
//   5. From the guild page opened with no guildmaster, Bulk Orders, Small, OK: a small deed in the backpack.

using System;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Accounting.Security;
using Server.Engines.BulkOrders;
using Server.Engines.Craft;
using Server.Gumps;
using Server.Items;
using Server.Misc;
using Server.Mobiles;
using Server.Network;
using Server.Tests.Network;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class SmithBulkOrderChoiceVerification
{
    private readonly ITestOutputHelper _out;

    public SmithBulkOrderChoiceVerification(ITestOutputHelper output)
    {
        _out = output;
        EnsureStartupHooks();
        ClusterFGuildSystem.EnsureRegistered();

        // The craft system is built at server start; a test run that reaches this class first has none
        // (as CraftRegistrationsVerification and CraftXVerification do it).
        if (DefBlacksmithy.CraftSystem == null)
        {
            DefBlacksmithy.Initialize();
        }

        BlacksmithyCraftRegistrations.Register(); // idempotent
    }

    private static bool _startupHooksRun;

    private static void EnsureStartupHooks()
    {
        if (!_startupHooksRun)
        {
            Accounts.Configure();
            WelcomeTimer.Initialize();
            _startupHooksRun = true;
        }

        if (AccountSecurity.CurrentAlgorithm == PasswordProtectionAlgorithm.None)
        {
            AccountSecurity.CurrentAlgorithm = PasswordProtectionAlgorithm.PBKDF2;
        }
    }

    private sealed class Smith : IDisposable
    {
        public readonly Account Account = new($"p42f{Guid.NewGuid():N}"[..16], "p42-test-only");
        public readonly PlayerMobile Pm;
        public readonly NetState Ns;

        public Smith(double skill, int standing = 0)
        {
            Pm = new PlayerMobile { Player = true, Race = Race.Human };
            Pm.AddItem(new Backpack());
            Pm.Skills.Blacksmith.Base = skill;
            Account[0] = Pm;
            Pm.MoveToWorld(new Point3D(1530, 1530, 0), Map.Trammel);

            Ns = PacketTestUtilities.CreateTestNetState();
            Ns.Account = Account;
            Pm.NetState = Ns;
            Ns.Mobile = Pm;

            var guild = ClusterFAccountPersistence.GetOrCreate(Account).GetOrCreateGuildData(Pm.Serial);
            guild.JoinedGuilds.Add("smithing");
            if (standing > 0)
            {
                guild.AddReputation("smithing", standing);
            }
        }

        public int Mark => Ns.SendBuffer.GetReadSpan().Length;

        public bool Said(int from, string text)
        {
            var span = Ns.SendBuffer.GetReadSpan()[from..];
            return span.IndexOf(System.Text.Encoding.BigEndianUnicode.GetBytes(text)) >= 0 ||
                   span.IndexOf(System.Text.Encoding.ASCII.GetBytes(text)) >= 0;
        }

        public T[] InPack<T>() where T : Item => Pm.Backpack.Items.OfType<T>().ToArray();

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

    private static readonly System.Reflection.MethodInfo GumpRemove = typeof(GumpSystem).GetMethod("Remove",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static, null,
        [typeof(NetState), typeof(BaseGump)], null);

    // As the client's reply does (GumpSystem.IncomingPackets.cs:156-166): the gump leaves the open list, then
    // OnResponse runs. Closing it from the server instead would run OnServerClose, which deletes an accept gump's deed.
    private static void Press(BaseGump gump, NetState ns, int buttonId)
    {
        Assert.NotNull(GumpRemove);
        GumpRemove.Invoke(null, [ns, gump]);
        gump.OnResponse(ns, new RelayInfo(buttonId, ReadOnlySpan<int>.Empty, ReadOnlySpan<ushort>.Empty,
            ReadOnlySpan<Range>.Empty, ReadOnlySpan<byte>.Empty));
    }

    // Opens the choice, presses a button, and returns the deed the accept gump holds (null: no accept gump).
    private static BaseBOD Choose(Smith s, int button)
    {
        var choice = new SmithBulkOrderChoiceGump(s.Pm);
        s.Pm.SendGump(choice);
        Press(choice, s.Ns, button);

        var small = s.Pm.FindGump<SmallBODAcceptGump>();
        var large = s.Pm.FindGump<LargeBODAcceptGump>();
        Assert.False(small != null && large != null);
        return (BaseBOD)Deed(small) ?? (BaseBOD)Deed(large);
    }

    private static Item Deed(BaseGump accept) =>
        accept?.GetType().GetField("_deed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.GetValue(accept) as Item;

    private static void Accept(Smith s)
    {
        BaseGump accept = s.Pm.FindGump<SmallBODAcceptGump>();
        accept ??= s.Pm.FindGump<LargeBODAcceptGump>();
        Assert.NotNull(accept);
        Press(accept, s.Ns, 1); // OK
    }

    private static string[] Labels(Gump g) => g.Entries.OfType<GumpLabel>().Select(l => l.Text).ToArray();

    // ---------------------------------------------------------------- 1

    [Fact]
    public void BelowSeventyPointOneALargeOrderIsRefusedWithTheSkillMessage()
    {
        using var s = new Smith(70.0, standing: 50_000);

        var choice = new SmithBulkOrderChoiceGump(s.Pm);
        var labels = Labels(choice);
        _out.WriteLine(string.Join(" | ", labels));
        Assert.DoesNotContain("Large bulk order", labels);
        Assert.Contains("Large bulk order: needs 70.1 Blacksmithy", labels);
        Assert.DoesNotContain(choice.Entries.OfType<GumpButton>(), b => b.ButtonID == SmithBulkOrderChoiceGump.BtnLarge);

        var mark = s.Mark;
        Assert.Null(Choose(s, SmithBulkOrderChoiceGump.BtnLarge));
        Assert.True(s.Said(mark, BlacksmithGuildmaster.LargeSkillMessage));

        Assert.Null(BlacksmithGuildmaster.TryCreateLargeBOD(s.Pm));
        Assert.Empty(s.InPack<LargeSmithBOD>());
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void AtSeventyPointOneWithNoStandingALargeOrderComesThroughTheAcceptGump()
    {
        using var s = new Smith(70.1);
        Assert.Contains("Large bulk order", Labels(new SmithBulkOrderChoiceGump(s.Pm)));

        var deed = Choose(s, SmithBulkOrderChoiceGump.BtnLarge);
        Assert.IsType<LargeSmithBOD>(deed);
        Assert.Empty(s.InPack<LargeSmithBOD>()); // not until it is accepted

        Accept(s);
        Assert.Single(s.InPack<LargeSmithBOD>());
        Assert.Same(deed, s.InPack<LargeSmithBOD>()[0]);
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void TheCapsStillRefuse()
    {
        using var s = new Smith(100.0);

        Choose(s, SmithBulkOrderChoiceGump.BtnLarge);
        Accept(s);
        var mark = s.Mark;
        Assert.Null(Choose(s, SmithBulkOrderChoiceGump.BtnLarge));
        Assert.True(s.Said(mark, "You already have a large order in progress."));
        Assert.Single(s.InPack<LargeSmithBOD>());

        for (var i = 0; i < 3; i++)
        {
            Assert.IsType<SmallSmithBOD>(Choose(s, SmithBulkOrderChoiceGump.BtnSmall));
            Accept(s);
        }

        mark = s.Mark;
        Assert.Null(Choose(s, SmithBulkOrderChoiceGump.BtnSmall));
        Assert.True(s.Said(mark, "You have 3 active orders."));
        Assert.Equal(3, s.InPack<SmallSmithBOD>().Length);
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void SmallFromTheChoiceIsAlwaysSmall()
    {
        using var s = new Smith(120.0);
        for (var i = 0; i < 40; i++)
        {
            var deed = Choose(s, SmithBulkOrderChoiceGump.BtnSmall);
            Assert.IsType<SmallSmithBOD>(deed);

            // Cancel: the accept gump deletes the deed (SmallBODAcceptGump.OnResponse), so the cap never binds.
            BaseGump accept = s.Pm.FindGump<SmallBODAcceptGump>();
            Press(accept, s.Ns, 0);
            Assert.True(deed.Deleted);
        }

        Assert.Empty(s.InPack<SmallSmithBOD>());
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void TheGuildPageWithNoGuildmasterGivesADeed()
    {
        using var s = new Smith(50.0);
        var def = ClusterFGuildSystem.GetDef("smithing");
        Assert.NotNull(def);

        var page = new SmithGuildmasterGump(s.Pm, def, s.Account); // as the guild directory opens it: no NPC
        s.Pm.SendGump(page);
        var mark = s.Mark;
        Press(page, s.Ns, 1); // Bulk Orders
        Assert.False(s.Said(mark, "Please speak with the Guildmaster directly."));

        var choice = s.Pm.FindGump<SmithBulkOrderChoiceGump>();
        Assert.NotNull(choice);
        Press(choice, s.Ns, SmithBulkOrderChoiceGump.BtnSmall);
        Accept(s);

        Assert.Single(s.InPack<SmallSmithBOD>());
    }
}
