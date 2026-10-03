// SmithTeachingToggleVerification.cs
//
// cc-P46 Part B (Chase, 2026-10-03). cc-P42 Part G1 made every Society of Smiths order ask for items that can still raise
// the smith's Blacksmithy. Each character can now turn that off (default on): off, the order picks among everything the
// smith can make, as stock does. The setting is CharacterGuildData.SmithTeachingOrders (v1), keyed by character
// serial with the rest of the guild data; the Bulk Order choice gump and the guild book's order page both show it.
// Regular NPC smiths never read it. Notes: shard-migration notes/cc-P46-smith-orders-2.md, Part B.
//
// Facts:
//   1. Default on: a new character with no guild data, and an existing character whose record was written before v1.
//   2. The setting survives the guild data's own serialize and deserialize, off and on.
//   3. Off (set by the choice gump's check box), a Grandmaster's Society small orders include at least one item that
//      teaches nothing over 60 draws, and so do large orders at 120 (a set other than plate) and commissions; on, every
//      small order teaches (cc-P42's fact, through the same Society path).
//   4. The regular-smith generator (SmallSmithBOD.CreateRandomFor(m)) still gives a 120 smith items that teach nothing,
//      with the setting on and off alike.
//   5. Both gumps show the one setting: the choice gump's check box turns it off and the book's order page then shows it
//      off; the book's check box turns it on and the choice gump then shows it on.

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Accounting;
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
public class SmithTeachingToggleVerification
{
    private const int Draws = 60;

    private readonly ITestOutputHelper _out;

    public SmithTeachingToggleVerification(ITestOutputHelper output)
    {
        _out = output;
        ShardTestHost.EnsureAccounts();
        ShardTestHost.EnsureCraftSystems();
        ClusterFGuildSystem.EnsureRegistered();
    }

    private sealed class Smith : IDisposable
    {
        public readonly Account Account = new($"p46b{Guid.NewGuid():N}"[..16], "p46-test-only");
        public readonly PlayerMobile Pm;
        public readonly NetState Ns;

        public Smith(double skill)
        {
            Pm = new PlayerMobile { Player = true, Race = Race.Human };
            Pm.AddItem(new Backpack());
            Pm.Skills.Blacksmith.Cap = 300.0;
            Pm.Skills.Blacksmith.Base = skill;
            Account[0] = Pm;
            Pm.MoveToWorld(new Point3D(1570, 1570, 0), Map.Trammel);

            Ns = PacketTestUtilities.CreateTestNetState();
            Ns.Account = Account;
            Pm.NetState = Ns;
            Ns.Mobile = Pm;

            Guild.JoinedGuilds.Add("smithing");
        }

        public CharacterGuildData Guild => ClusterFAccountPersistence.GetOrCreateGuild(Pm);

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

    // As the client's reply does (GumpSystem.IncomingPackets.cs:156-166): the gump leaves the open list, then OnResponse.
    private static void Press(BaseGump gump, NetState ns, int buttonId)
    {
        Assert.NotNull(GumpRemove);
        GumpRemove.Invoke(null, [ns, gump]);
        gump.OnResponse(ns, new RelayInfo(buttonId, ReadOnlySpan<int>.Empty, ReadOnlySpan<ushort>.Empty,
            ReadOnlySpan<Range>.Empty, ReadOnlySpan<byte>.Empty));
    }

    // The check box a gump draws for the setting: true when it shows "checked".
    private static bool ShowsOn(Gump gump, int buttonId)
    {
        var box = gump.Entries.OfType<GumpButton>().Single(b => b.ButtonID == buttonId);
        Assert.Contains(gump.Entries.OfType<GumpLabel>(), l => l.Text == ClusterFSmithTeaching.ToggleLabel);
        Assert.Contains(gump.Entries.OfType<GumpLabel>(), l => l.Text == ClusterFSmithTeaching.ToggleHint);
        return box.NormalID == SmithBulkOrderChoiceGump.CheckOn;
    }

    private static string Tally(IEnumerable<string> names) =>
        string.Join(", ", names.GroupBy(n => n).OrderByDescending(g => g.Count()).Select(g => $"{g.Key} x{g.Count()}"));

    // ---------------------------------------------------------------- 1

    [Fact]
    public void TheDefaultIsOnForNewAndExistingCharacters()
    {
        using var s = new Smith(100.0);
        ClusterFAccountPersistence.Get(s.Account)?.ClearGuildData();
        Assert.Null(ClusterFAccountPersistence.GetGuild(s.Pm));
        Assert.True(ClusterFSmithTeaching.WantsTeaching(s.Pm), "a character with no guild data");
        Assert.True(new CharacterGuildData().SmithTeachingOrders, "a new record");

        // A record written before v1: CharacterGuildData v0's layout (cc-P18), one guild joined.
        var buffer0 = new byte[4096];
        var w0 = new BufferWriter(buffer0, true);
        w0.Write(0);         // version
        w0.Write(1);         // joined guilds
        w0.Write("smithing");
        for (var i = 0; i < 7; i++)
        {
            w0.Write(0);     // apprentice, reputation, currency, active and completed work orders, small and large commissions
        }

        w0.Write("");        // no Artificer order
        w0.Write(0u);
        w0.Flush();

        var old = new CharacterGuildData(new BufferReader(buffer0));
        Assert.Contains("smithing", old.JoinedGuilds);
        Assert.True(old.SmithTeachingOrders, "a v0 record reads as on");
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void TheSettingSurvivesSerializeAndDeserialize()
    {
        foreach (var on in new[] { false, true })
        {
            var g = new CharacterGuildData { SmithTeachingOrders = on };
            g.JoinedGuilds.Add("smithing");
            g.AddCurrency("smithing", 42);

            var buffer = new byte[4096];
            var w = new BufferWriter(buffer, true);
            g.Serialize(w);
            w.Flush();

            var copy = new CharacterGuildData(new BufferReader(buffer));
            _out.WriteLine($"written {on}, read {copy.SmithTeachingOrders}");
            Assert.Equal(on, copy.SmithTeachingOrders);
            Assert.Equal(42, copy.GetCurrency("smithing"));
        }

        // A character whose only guild data is the setting turned off is not "empty" (the v15 migration's test).
        Assert.False(new CharacterGuildData { SmithTeachingOrders = false }.IsEmpty);
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void OffGivesAnyItemTheSmithCanMakeAndOnGivesOnlyTeachingItems()
    {
        using var s = new Smith(100.0);

        // On (the default): every Society small order teaches.
        var on = new List<string>();
        for (var i = 0; i < Draws; i++)
        {
            var bod = Assert.IsType<SmallSmithBOD>(BlacksmithGuildmaster.TryCreateSmallBOD(s.Pm));
            on.Add(bod.Type.Name);
            Assert.True(ClusterFSmithTeaching.Teaches(s.Pm, bod.Type), $"{bod.Type.Name} teaches nothing with the setting on");
            bod.Delete();
        }

        _out.WriteLine($"on, small at 100: {Tally(on)}");

        // Off, through the choice gump's own check box.
        var choice = new SmithBulkOrderChoiceGump(s.Pm);
        s.Pm.SendGump(choice);
        Press(choice, s.Ns, SmithBulkOrderChoiceGump.BtnTeaching);
        Assert.False(ClusterFSmithTeaching.WantsTeaching(s.Pm));
        Assert.False(s.Guild.SmithTeachingOrders);

        var off = new List<string>();
        for (var i = 0; i < Draws; i++)
        {
            var bod = Assert.IsType<SmallSmithBOD>(BlacksmithGuildmaster.TryCreateSmallBOD(s.Pm));
            off.Add(bod.Type.Name);
            Assert.True(ClusterFSmithTeaching.CanMake(s.Pm, bod.Type, bod.RequireExceptional));
            bod.Delete();
        }

        _out.WriteLine($"off, small at 100: {Tally(off)}");
        Assert.Contains(off, n => !ClusterFSmithTeaching.Teaches(s.Pm, SmallBulkEntry.BlacksmithArmor
            .Concat(SmallBulkEntry.BlacksmithWeapons).First(e => e.Type.Name == n).Type));

        // Large at 120: on gives plate every time (cc-P42); off gives other sets too.
        s.Pm.Skills.Blacksmith.Base = 120.0;
        var sets = new List<string>();
        for (var i = 0; i < Draws; i++)
        {
            var bod = Assert.IsType<LargeSmithBOD>(BlacksmithGuildmaster.TryCreateLargeBOD(s.Pm));
            sets.Add(bod.Entries[0].Details.Type.Name);
            bod.Delete();
        }

        _out.WriteLine($"off, large at 120 (first piece): {Tally(sets)}");
        Assert.Contains(sets, n => n != LargeSmithBOD.SetTypes(1)[0].Name);

        // Commissions at 100.
        s.Pm.Skills.Blacksmith.Base = 100.0;
        var keys = new List<string>();
        for (var i = 0; i < Draws; i++)
        {
            var c = SmithCommissionSystem.Generate(s.Pm);
            Assert.NotNull(c);
            keys.Add(c.ItemKey);
            s.Guild.SmithCommissions.Clear();
        }

        _out.WriteLine($"off, commissions at 100: {Tally(keys)}");
        Assert.Contains(keys, k => !ClusterFSmithTeaching.Teaches(s.Pm, SmithCommissionPool.GetItemType(k)));
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void RegularSmithsAreUnchangedEitherWay()
    {
        using var s = new Smith(120.0);
        foreach (var setting in new[] { true, false })
        {
            ClusterFSmithTeaching.SetWantsTeaching(s.Pm, setting);
            var nonTeaching = 0;
            for (var i = 0; i < 200; i++)
            {
                var bod = SmallSmithBOD.CreateRandomFor(s.Pm); // the one-argument form regular smiths call
                if (bod == null)
                {
                    continue;
                }

                nonTeaching += ClusterFSmithTeaching.Teaches(s.Pm, bod.Type) ? 0 : 1;
                bod.Delete();
            }

            _out.WriteLine($"setting {(setting ? "on" : "off")}: regular-smith orders teaching nothing at 120: {nonTeaching} of 200");
            Assert.True(nonTeaching > 0, $"the regular-smith generator changed with the setting {(setting ? "on" : "off")}");
        }
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void BothGumpsShowTheOneSetting()
    {
        using var s = new Smith(100.0);
        var book = new SmithGuildBook();
        s.Pm.Backpack.DropItem(book);

        Assert.True(ShowsOn(new SmithBulkOrderChoiceGump(s.Pm), SmithBulkOrderChoiceGump.BtnTeaching));
        Assert.True(ShowsOn(new SmithGuildBookGump(s.Pm, book), SmithGuildBookGump.BtnTeachingOrders));

        // Off in the choice gump; the book's order page shows it off.
        var choice = new SmithBulkOrderChoiceGump(s.Pm);
        s.Pm.SendGump(choice);
        Press(choice, s.Ns, SmithBulkOrderChoiceGump.BtnTeaching);
        var redrawn = s.Pm.FindGump<SmithBulkOrderChoiceGump>();
        Assert.NotNull(redrawn);
        Assert.False(ShowsOn(redrawn, SmithBulkOrderChoiceGump.BtnTeaching));
        Assert.False(ShowsOn(new SmithGuildBookGump(s.Pm, book), SmithGuildBookGump.BtnTeachingOrders));

        // On in the book; the choice gump shows it on.
        var page = new SmithGuildBookGump(s.Pm, book);
        s.Pm.SendGump(page);
        Press(page, s.Ns, SmithGuildBookGump.BtnTeachingOrders);
        Assert.True(ClusterFSmithTeaching.WantsTeaching(s.Pm));
        var bookAgain = s.Pm.FindGump<SmithGuildBookGump>();
        Assert.NotNull(bookAgain);
        Assert.True(ShowsOn(bookAgain, SmithGuildBookGump.BtnTeachingOrders));
        Assert.True(ShowsOn(new SmithBulkOrderChoiceGump(s.Pm), SmithBulkOrderChoiceGump.BtnTeaching));
    }
}
