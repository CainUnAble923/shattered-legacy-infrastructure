// ClockworkScavengerVerification.cs
//
// cc-P808: the Clockwork Scavenger, earned by a Tinkers' Union build quest. Notes in shard-migration
// notes/cc-P808-clockwork-scavenger.md.
//
// Facts:
//   1. The guildmaster: not a member, then below Journeyman, is refused; a Journeyman gets one bound frame, never two.
//   2. Assembling: a missing part is refused with nothing used; all parts are used exactly and make a Scavenger that
//      follows its builder; then no new frame while it lives.
//   3. The frame is bound: another character cannot use it or lift it.
//   4. The loop: a corpse the owner may loot gives up what the filter keeps (gold, reagents), not gear; a corpse that
//      would be a crime, a player's corpse, an empty filter, an offline or distant owner give nothing; at most
//      maxPerTick a pass.
//   5. Clockwork Load Harness III sends the loot to the mule; the module says Built.
//   6. Released, it powers down, and the guildmaster gives a new frame.
//   7. Both survive a save: builder, filter, owner.
//   8. The file: defaults are valid and round-trip; a missing file is written; a bad file is refused whole.
//   9. The guildmaster offers Quest Conversation to members only; the Options window toggles the filter.

using System;
using System.IO;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Collections;
using Server.ContextMenus;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Tests.Network;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class ClockworkScavengerVerification
{
    private static readonly Point3D Spot = new(3460, 2604, 18);

    public ClockworkScavengerVerification()
    {
        ShardTestHost.EnsureAccounts();
        MuleModules.Configure();
        ClockworkScavengerConfig.Current = new ClockworkScavengerConfig();
    }

    private sealed class Tinker : IDisposable
    {
        public readonly Account Account = new($"p808{Guid.NewGuid():N}"[..16], "p808-test-only");
        public readonly PlayerMobile Pm;
        public readonly TinkerGuildmaster Gm = new();

        public Tinker(int standing = 5_000, bool join = true, bool online = true)
        {
            Pm = new PlayerMobile { Player = true, Race = Race.Human, Name = "Tester" };
            Pm.AddItem(new Backpack());
            Account[0] = Pm;
            Pm.MoveToWorld(Spot, Map.Trammel);
            Gm.MoveToWorld(new Point3D(Spot.X - 3, Spot.Y, Spot.Z), Map.Trammel);

            if (join)
            {
                Guild.JoinedGuilds.Add("tinkers");
                Guild.GuildReputation["tinkers"] = standing;
            }

            if (online)
            {
                var ns = PacketTestUtilities.CreateTestNetState();
                Pm.NetState = ns;
                ns.Mobile = Pm;
                ns.Account = Account;
            }
        }

        public CharacterGuildData Guild => ClusterFAccountPersistence.GetOrCreate(Account).GetOrCreateGuildData(Pm.Serial);

        public void GiveParts(int short1 = 0)
        {
            Pm.Backpack.DropItem(new Gears(10 - short1));
            Pm.Backpack.DropItem(new Springs(10));
            Pm.Backpack.DropItem(new ClockParts(5));
            Pm.Backpack.DropItem(new AxleGears(2));
            Pm.Backpack.DropItem(new IronIngot(50));
        }

        public ClockworkScavenger Build()
        {
            Assert.Equal(ScavengerQuestResult.FrameGiven, ClockworkScavengerQuest.Talk(Pm, Gm));
            GiveParts();
            var s = ClockworkScavengerQuest.Assemble(Pm, ClockworkScavengerQuest.FrameOf(Pm));
            Assert.NotNull(s);
            return s;
        }

        public void Dispose()
        {
            ClockworkScavengerQuest.ScavengerOf(Pm)?.Delete();
            ClockworkScavengerQuest.FrameOf(Pm)?.Delete();

            if (Pm.NetState is { } ns)
            {
                Pm.NetState = null;
                ns.Mobile = null;
                ns.Dispose();
            }

            Gm.Delete();
            Pm.Delete();
            Accounts.Remove(Account);
        }
    }

    private static Corpse MonsterCorpse(Mobile earnedBy, int dx = 1, params Item[] items)
    {
        var orc = new Orc();
        var corpse = new Corpse(orc, []);

        if (earnedBy != null)
        {
            corpse.Aggressors.Add(earnedBy);
        }

        foreach (var i in items)
        {
            corpse.DropItem(i);
        }

        corpse.MoveToWorld(new Point3D(Spot.X + dx, Spot.Y, Spot.Z), Map.Trammel);
        orc.Delete();
        return corpse;
    }

    // ---------------------------------------------------------------- 1

    [Fact]
    public void TheGuildmasterGivesAJourneymanOneBoundFrame()
    {
        using (var outsider = new Tinker(join: false))
        {
            Assert.Equal(ScavengerQuestResult.NotMember, ClockworkScavengerQuest.Talk(outsider.Pm, outsider.Gm));
            Assert.Null(ClockworkScavengerQuest.FrameOf(outsider.Pm));
        }

        using (var apprentice = new Tinker(standing: 4_999))
        {
            Assert.Equal(ScavengerQuestResult.RankTooLow, ClockworkScavengerQuest.Talk(apprentice.Pm, apprentice.Gm));
            Assert.Null(ClockworkScavengerQuest.FrameOf(apprentice.Pm));
        }

        using var t = new Tinker();
        Assert.Equal(ClusterFGuildSystem.RankJourneyman, ClockworkScavengerQuest.RankOf(t.Pm));
        Assert.Equal(ScavengerQuestResult.FrameGiven, ClockworkScavengerQuest.Talk(t.Pm, t.Gm));
        var frame = ClockworkScavengerQuest.FrameOf(t.Pm);
        Assert.NotNull(frame);
        Assert.True(frame.IsChildOf(t.Pm.Backpack));
        Assert.Equal(LootType.Blessed, frame.LootType);

        Assert.Equal(ScavengerQuestResult.HasFrame, ClockworkScavengerQuest.Talk(t.Pm, t.Gm));
        Assert.Same(frame, ClockworkScavengerFrame.All.Single(f => f.Owner == t.Pm));
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void AssemblingUsesEveryPartOnceAndMakesOneScavenger()
    {
        using var t = new Tinker();
        Assert.Equal(ScavengerQuestResult.FrameGiven, ClockworkScavengerQuest.Talk(t.Pm, t.Gm));
        var frame = ClockworkScavengerQuest.FrameOf(t.Pm);

        t.GiveParts(short1: 1); // one gear short
        Assert.Null(ClockworkScavengerQuest.Assemble(t.Pm, frame));
        Assert.False(frame.Deleted);
        Assert.Equal(9, t.Pm.Backpack.GetAmount(typeof(Gears)));
        Assert.Equal(50, t.Pm.Backpack.GetAmount(typeof(IronIngot)));

        t.Pm.Backpack.DropItem(new Gears(3));
        var followers = t.Pm.Followers;
        var s = ClockworkScavengerQuest.Assemble(t.Pm, frame);

        Assert.NotNull(s);
        Assert.True(frame.Deleted);
        Assert.Equal(2, t.Pm.Backpack.GetAmount(typeof(Gears)));
        Assert.Equal(0, t.Pm.Backpack.GetAmount(typeof(Springs)));
        Assert.Equal(0, t.Pm.Backpack.GetAmount(typeof(IronIngot)));
        Assert.Same(t.Pm, s.ControlMaster);
        Assert.Same(t.Pm, s.Builder);
        Assert.True(s.Controlled);
        Assert.True(s.Blessed);
        Assert.Equal(followers + 1, t.Pm.Followers);
        Assert.Equal(new[] { "gold", "gems", "reagents" }, s.Filter.ToArray());

        Assert.Equal(ScavengerQuestResult.HasScavenger, ClockworkScavengerQuest.Talk(t.Pm, t.Gm));
        Assert.Null(ClockworkScavengerQuest.FrameOf(t.Pm));
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void TheFrameIsBoundToItsTinker()
    {
        using var t = new Tinker();
        using var other = new Tinker();
        Assert.Equal(ScavengerQuestResult.FrameGiven, ClockworkScavengerQuest.Talk(t.Pm, t.Gm));
        var frame = ClockworkScavengerQuest.FrameOf(t.Pm);

        Assert.False(frame.OnDragLift(other.Pm));
        Assert.True(frame.OnDragLift(t.Pm));

        other.Pm.Backpack.DropItem(frame);
        other.GiveParts();
        Assert.Null(ClockworkScavengerQuest.Assemble(other.Pm, frame));
        Assert.False(frame.Deleted);
        Assert.Equal(10, other.Pm.Backpack.GetAmount(typeof(Gears)));
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void ItLootsOnlyWhatTheOwnerMayLootAndTheFilterKeeps()
    {
        using var t = new Tinker();
        var s = t.Build();
        var gold = new Gold(120);
        var pearl = new BlackPearl(4);
        var sword = new Longsword();
        var corpse = MonsterCorpse(t.Pm, 1, gold, pearl, sword);
        var crime = MonsterCorpse(null, -1, new Gold(77));

        var victim = new PlayerMobile { Player = true, Name = "Victim" };
        victim.MoveToWorld(Spot, Map.Trammel);
        var playerCorpse = new Corpse(victim, []);
        playerCorpse.DropItem(new Gold(55));
        playerCorpse.Aggressors.Add(t.Pm);
        playerCorpse.MoveToWorld(new Point3D(Spot.X, Spot.Y + 1, Spot.Z), Map.Trammel);

        try
        {
            Assert.False(ClockworkScavenger.MayLoot(t.Pm, crime));
            Assert.False(ClockworkScavenger.MayLoot(t.Pm, playerCorpse));
            Assert.True(ClockworkScavenger.MayLoot(t.Pm, corpse));

            Assert.Equal(2, s.Scavenge());
            Assert.True(gold.IsChildOf(t.Pm.Backpack));
            Assert.True(pearl.IsChildOf(t.Pm.Backpack));
            Assert.Same(corpse, sword.Parent);
            Assert.Equal(77, crime.GetAmount(typeof(Gold)));
            Assert.Equal(55, playerCorpse.GetAmount(typeof(Gold)));
            Assert.Equal(0, s.Scavenge()); // nothing left it may take

            // An empty filter takes nothing.
            corpse.DropItem(new Gold(10));
            foreach (var c in s.Filter.ToList())
            {
                s.ToggleFilter(c);
            }

            Assert.Equal(0, s.Scavenge());
            s.ToggleFilter("gold");
            Assert.Equal(1, s.Scavenge());

            // An owner who is away or offline gets nothing.
            corpse.DropItem(new Gold(10));
            t.Pm.MoveToWorld(new Point3D(Spot.X + 20, Spot.Y, Spot.Z), Map.Trammel);
            Assert.Equal(0, s.Scavenge());
            t.Pm.MoveToWorld(Spot, Map.Trammel);
            var ns = t.Pm.NetState;
            t.Pm.NetState = null;
            Assert.Equal(0, s.Scavenge());
            t.Pm.NetState = ns;
            Assert.Equal(1, s.Scavenge());

            // At most maxPerTick a pass.
            s.ToggleFilter("reagents");
            for (var i = 0; i < 8; i++)
            {
                corpse.DropItem(new BlackPearl(1) { Hue = i + 1 }); // hued, so they do not stack in the corpse
            }

            Assert.Equal(ClockworkScavengerConfig.Current.MaxPerTick, s.Scavenge());
        }
        finally
        {
            corpse.Delete();
            crime.Delete();
            playerCorpse.Delete();
            victim.Delete();
        }
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void LoadHarnessThreeSendsTheLootToTheMule()
    {
        Assert.Equal(MuleModuleState.Built, MuleModuleData.Info(MuleModuleId.ClockworkLoadHarness).State);

        using var t = new Tinker();
        var s = t.Build();
        var mule = PackMule.SummonFor(t.Pm, 3, MuleGenes.Fresh(), "here");
        var gold = new Gold(40);
        var corpse = MonsterCorpse(t.Pm, 1, gold);

        try
        {
            Assert.True(MuleModules.TryAttach(t.Pm, mule, new MuleModule(MuleModuleId.ClockworkLoadHarness, 2)));
            Assert.Equal(1, s.Scavenge());
            Assert.True(gold.IsChildOf(t.Pm.Backpack)); // tier II: still the backpack

            foreach (var m in MuleModules.Installed(mule).ToList())
            {
                MuleModules.Detach(t.Pm, mule, m);
            }

            Assert.True(MuleModules.TryAttach(t.Pm, mule, new MuleModule(MuleModuleId.ClockworkLoadHarness, 3)));
            var more = new Gold(41);
            corpse.DropItem(more);
            Assert.Equal(1, s.Scavenge());
            Assert.True(more.IsChildOf(mule.Backpack));
        }
        finally
        {
            corpse.Delete();
            mule.Delete();
        }
    }

    // ---------------------------------------------------------------- 6

    [Fact]
    public void AReleasedScavengerPowersDownAndCanBeRebuilt()
    {
        using var t = new Tinker();
        var s = t.Build();
        Assert.True(s.DeleteOnRelease);
        s.Delete(); // what release does to a construct
        Assert.Null(ClockworkScavengerQuest.ScavengerOf(t.Pm));
        Assert.Equal(ScavengerQuestResult.FrameGiven, ClockworkScavengerQuest.Talk(t.Pm, t.Gm));
    }

    // ---------------------------------------------------------------- 7

    [Fact]
    public void BothSurviveASave()
    {
        using var t = new Tinker();
        var s = t.Build();
        s.ToggleFilter("gems");
        s.ToggleFilter("bandages");

        var buffer = new byte[65536];
        var writer = new BufferWriter(buffer, true);
        s.Serialize(writer);
        writer.Flush();
        var copy = (ClockworkScavenger)Activator.CreateInstance(typeof(ClockworkScavenger), s.Serial)!;
        copy.Deserialize(new BufferReader(buffer));
        Assert.Same(t.Pm, copy.Builder);
        Assert.Equal(new[] { "gold", "reagents", "bandages" }, copy.Filter.ToArray());

        var frame = new ClockworkScavengerFrame(t.Pm);

        try
        {
            buffer = new byte[65536];
            writer = new BufferWriter(buffer, true);
            frame.Serialize(writer);
            writer.Flush();
            var frameCopy = (ClockworkScavengerFrame)Activator.CreateInstance(typeof(ClockworkScavengerFrame), frame.Serial)!;
            frameCopy.Deserialize(new BufferReader(buffer));
            Assert.Same(t.Pm, frameCopy.Owner);
        }
        finally
        {
            frame.Delete();
        }
    }

    // ---------------------------------------------------------------- 8

    [Fact]
    public void TheFileIsWrittenReadAndRefusedWhole()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"p808-{Guid.NewGuid():N}");
        var path = Path.Combine(dir, ClockworkScavengerConfig.FileName);

        try
        {
            Assert.Empty(new ClockworkScavengerConfig().Validate());

            var lines = ClockworkScavengerConfig.LoadOrWriteDefaults(path);
            Assert.True(File.Exists(path));
            Assert.Contains(lines, l => l.Contains("wrote the defaults"));

            File.WriteAllText(path, File.ReadAllText(path).Replace("\"maxPerTick\": 5", "\"maxPerTick\": 7"));
            Assert.Empty(ClockworkScavengerConfig.LoadOrWriteDefaults(path));
            Assert.Equal(7, ClockworkScavengerConfig.Current.MaxPerTick);
            Assert.Equal(5, ClockworkScavengerConfig.Current.Parts.Count);

            File.WriteAllText(path, "{ \"maxPerTick\": 3, \"parts\": [ { \"type\": \"NoSuchPart\", \"amount\": 1 } ], \"defaultFilter\": [\"swords\"] }");
            lines = ClockworkScavengerConfig.LoadOrWriteDefaults(path);
            Assert.Contains(lines, l => l.Contains("NoSuchPart"));
            Assert.Contains(lines, l => l.Contains("swords"));
            Assert.Equal(7, ClockworkScavengerConfig.Current.MaxPerTick); // refused whole: the table in force stays

            File.WriteAllText(path, "{ not json");
            Assert.Contains(ClockworkScavengerConfig.LoadOrWriteDefaults(path), l => l.Contains("not valid JSON"));
        }
        finally
        {
            ClockworkScavengerConfig.Current = new ClockworkScavengerConfig();

            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }
        }
    }

    // ---------------------------------------------------------------- 9

    [Fact]
    public void TheGuildmasterOffersTheQuestToMembersAndTheFilterWindowToggles()
    {
        using var t = new Tinker();
        using var outsider = new Tinker(join: false);

        var list = PooledRefList<ContextMenuEntry>.Create();
        ClockworkScavengerQuest.AddEntry(t.Gm, t.Pm, ref list);
        Assert.True(list.Count == 1);
        Assert.Equal(3006156, list[0].Number);
        list.Dispose();

        list = PooledRefList<ContextMenuEntry>.Create();
        ClockworkScavengerQuest.AddEntry(t.Gm, outsider.Pm, ref list);
        Assert.True(list.Count == 0);
        list.Dispose();

        var s = t.Build();
        var gump = new ScavengerFilterGump(s);
        var gems = Array.IndexOf(MuleModules.PickupCategories, "gems");
        gump.OnResponse(t.Pm.NetState, new RelayInfo(1 + gems, ReadOnlySpan<int>.Empty, ReadOnlySpan<ushort>.Empty,
            ReadOnlySpan<Range>.Empty, ReadOnlySpan<byte>.Empty));
        Assert.DoesNotContain("gems", s.Filter);
    }
}
