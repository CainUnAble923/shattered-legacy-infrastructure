// StaffHubEventsVerification.cs
//
// cc-P230 (F-30 second batch, ours). The Staff Hub Events tab: GM-run monster surges. Customizations
// StaffHub/ClusterFSurge.cs (the surge), ClusterFSurgeTemplates.cs (saved creature groups), StaffHubGump.cs (the tab).
// Notes in shard-migration notes/cc-P230-staff-hub-events-tab.md.
//
// Facts:
//   1 a surge spawns exactly the count per wave, on the interval (ShardTestClock), the boss with the last wave; Dry run
//     spawns nothing.
//   2 End leaves no flagged creature and no gate; a surge whose creatures are all dead after its last wave ends itself.
//   3 a restart mid-surge: the flagged serials survive the save and are deleted at world load, and nothing else is.
//   4 damage share (top N by damage over the whole surge, an expired damage entry not lost) and presence (inside the
//     radius at least half the run time) are counted and paid as specified.
//   5 the no-loot zone keeps a dead player's items in the backpack; outside the radius, or after End, they drop.
//   6 templates save and load (versioned), the three starters are pinned creatures and are seeded once.
//   7 the Events tab runs a surge through its own buttons, on live as on test, for a GameMaster.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Server;
using Server.Accounting;
using Server.Items;
using Server.Misc;
using Server.Mobiles;
using Server.Gumps;
using Server.Tests.Network;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class StaffHubEventsVerification : IDisposable
{
    private static readonly Point3D Spot = new(3460, 2604, 18);

    private readonly ITestOutputHelper _out;
    private readonly List<Account> _accounts = [];
    private readonly byte[] _templatesBefore;

    public StaffHubEventsVerification(ITestOutputHelper output)
    {
        _out = output;
        ShardTestHost.EnsureAccounts();
        ClusterFStaffHub.Configure();

        _templatesBefore = SaveTemplates();
        ClusterFSurgeTemplates.ResetForTests();
        ClusterFSurgeTemplates.SeedIfNeverSeeded();
        ClusterFSurge.ForgetForTests();
    }

    public void Dispose()
    {
        if (ClusterFSurge.Active is { } run)
        {
            foreach (var c in run.Creatures.ToList())
            {
                c.Delete();
            }

            foreach (var g in run.Gates)
            {
                g.Delete();
            }
        }

        ClusterFSurge.ForgetForTests();
        ClusterFSurgeTemplates.ReadFrom(new BufferReader(_templatesBefore));

        foreach (var account in _accounts)
        {
            for (var i = 0; i < account.Length; i++)
            {
                if (account[i] is { } m)
                {
                    if (m.NetState is { } ns)
                    {
                        m.NetState = null;
                        ns.Mobile = null;
                        ns.Dispose();
                    }

                    m.Delete();
                }
            }

            Accounts.Remove(account);
        }
    }

    // ---------------------------------------------------------------- helpers

    private static byte[] SaveTemplates()
    {
        var buffer = new byte[65536];
        var writer = new BufferWriter(buffer, true);
        ClusterFSurgeTemplates.WriteTo(writer);
        writer.Flush();
        return buffer;
    }

    private PlayerMobile NewCharacter(AccessLevel level = AccessLevel.Player, Point3D? at = null)
    {
        var account = new Account($"p230{Guid.NewGuid():N}"[..16], "p230-test-only");
        _accounts.Add(account);

        var pm = new PlayerMobile
        {
            Player = true, Race = Race.Human, AccessLevel = level, Name = $"P230 {Guid.NewGuid():N}"[..12]
        };
        pm.AddItem(new Backpack());
        account[0] = pm;
        pm.MoveToWorld(at ?? Spot, Map.Trammel);

        var ns = PacketTestUtilities.CreateTestNetState();
        pm.NetState = ns;
        ns.Mobile = pm;
        ns.Account = account;
        return pm;
    }

    private static SurgeSettings Settings(string template, int waves = 3, int perWave = 4, int interval = 30, int radius = 10) =>
        new()
        {
            Template = template, Map = Map.Trammel, Center = Spot, Radius = radius, Waves = waves, PerWave = perWave,
            IntervalSeconds = interval
        };

    private static void Template(string name, string creatures, string bossType = "", string bossName = "")
    {
        Assert.True(ClusterFSurgeTemplates.Save(name, creatures, bossType, bossName, out var message), message);
    }

    private static int Gold(Mobile m) => m.Backpack.GetAmount(typeof(Gold));

    /// <summary>
    /// Lands hits 8 ms before the surge's once-a-second read, then lets the read run. The test host regenerates a hit
    /// every 8 ms (ShardTestClock header, Q-060), and pinned drops a creature's damage entries when it is back to full
    /// (Mobile.cs:2043), so a hit left a whole second would be healed and forgotten before the surge read it.
    /// </summary>
    private static void OnTheTick(SurgeRun run, Action hits)
    {
        var intoSecond = (long)(Core.Now - run.StartedAt).TotalMilliseconds % 1000;
        var toTick = 1000 - intoSecond;
        if (toTick > 8)
        {
            ShardTestClock.Advance(TimeSpan.FromMilliseconds(toTick - 8));
        }

        hits();
        ShardTestClock.Advance(TimeSpan.FromMilliseconds(16));
    }

    private sealed class TestCenterSwitch : IDisposable
    {
        private static readonly MethodInfo Setter =
            typeof(TestCenter).GetProperty(nameof(TestCenter.Enabled))!.GetSetMethod(true)!;

        private readonly bool _was;

        public TestCenterSwitch(bool on)
        {
            _was = TestCenter.Enabled;
            Setter.Invoke(null, [on]);
        }

        public void Dispose() => Setter.Invoke(null, [_was]);
    }

    // ---------------------------------------------------------------- 1

    [Fact]
    public void ASurgeSpawnsTheRightCountPerWaveOnTheInterval()
    {
        ShardTestClock.Arm();
        var gm = NewCharacter(AccessLevel.GameMaster);
        Template("P230 counts", "Orc*3, Zombie", "OgreLord", "P230 Boss");
        var s = Settings("P230 counts", waves: 3, perWave: 4, interval: 30);

        // Dry run: the plan, and nothing made.
        var plan = ClusterFSurge.Plan(s);
        ClusterFSurge.DryRun(gm, s);
        Assert.Null(ClusterFSurge.Active);
        Assert.Contains(plan, l => l.Contains("3 wave(s) of 4, every 30 s: 13 creature(s) in all"));
        Assert.DoesNotContain(plan, l => l.StartsWith("Cannot"));

        Assert.True(ClusterFSurge.Start(gm, s, out var message), message);
        var run = ClusterFSurge.Active;
        Assert.NotNull(run);
        Assert.Equal((1, 4), (run.WavesSpawned, run.Creatures.Count));

        ShardTestClock.AdvanceSeconds(29);
        Assert.Equal((1, 4), (run.WavesSpawned, run.Spawned));

        ShardTestClock.AdvanceSeconds(1);
        Assert.Equal((2, 8), (run.WavesSpawned, run.Spawned));

        ShardTestClock.AdvanceSeconds(30);
        Assert.Equal((3, 13), (run.WavesSpawned, run.Spawned)); // the last wave brings the boss

        ShardTestClock.AdvanceSeconds(90);
        Assert.Equal((3, 13), (run.WavesSpawned, run.Spawned));

        var boss = Assert.Single(run.Creatures, c => c is OgreLord);
        Assert.Equal("P230 Boss", boss.Name);
        Assert.All(run.Creatures, c =>
        {
            Assert.True(c is Orc or Zombie or OgreLord, c.GetType().Name);
            Assert.True(Math.Abs(c.X - Spot.X) <= 10 && Math.Abs(c.Y - Spot.Y) <= 10, $"{c.GetType().Name} at {c.Location}");
            var bc = (BaseCreature)c;
            Assert.False(bc.Tamable);
            Assert.Equal(Spot, bc.Home);
        });

        _out.WriteLine($"made {run.Spawned}: {string.Join(", ", run.Creatures.GroupBy(c => c.GetType().Name).Select(g => $"{g.Key} {g.Count()}"))}");
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void EndLeavesNoFlaggedCreatureOrGate()
    {
        ShardTestClock.Arm();
        var gm = NewCharacter(AccessLevel.GameMaster);
        Template("P230 end", "Orc");
        var s = Settings("P230 end", waves: 2, perWave: 5, interval: 10);
        s.Gates = true;

        Assert.True(ClusterFSurge.Start(gm, s, out var message), message);
        var run = ClusterFSurge.Active;
        ShardTestClock.AdvanceSeconds(10);

        var made = run.Creatures.ToList();
        var gates = run.Gates.ToList();
        Assert.Equal(10, made.Count);
        Assert.Equal(2, gates.Count);
        Assert.Contains(gates, g => g.Location == ClusterFSurge.BritainGate && g.Map == Map.Trammel);
        Assert.Contains(gates, g => g.Location == ClusterFSurge.NewHavenGate && g.Map == Map.Trammel);
        Assert.All(gates.Cast<Moongate>(), g => Assert.Equal((Spot, Map.Trammel), (g.Target, g.TargetMap)));

        made[0].Kill();
        Assert.Equal(1, run.Killed);

        Assert.True(ClusterFSurge.End(gm, out message), message);
        Assert.Null(ClusterFSurge.Active);
        Assert.Same(run, ClusterFSurge.Last);
        Assert.All(made, c => Assert.True(c.Deleted, $"{c.GetType().Name} {c.Serial} was left"));
        Assert.All(gates, g => Assert.True(g.Deleted, $"gate {g.Serial} was left"));
        Assert.False(ClusterFSurge.End(gm, out _));

        // A surge whose last wave is out and whose creatures are all dead ends by itself.
        Assert.True(ClusterFSurge.Start(gm, Settings("P230 end", waves: 1, perWave: 2), out message), message);
        var second = ClusterFSurge.Active;
        foreach (var c in second.Creatures.ToList())
        {
            c.Kill();
        }

        ShardTestClock.AdvanceSeconds(1);
        Assert.Null(ClusterFSurge.Active);
        Assert.Same(second, ClusterFSurge.Last);
        Assert.Equal(2, second.Killed);
        _out.WriteLine($"ended: {run.EndReason} / {second.EndReason}");
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void ARestartMidSurgeCleansUpOnLoad()
    {
        ShardTestClock.Arm();
        var gm = NewCharacter(AccessLevel.GameMaster);
        Template("P230 restart", "Orc");
        var s = Settings("P230 restart", waves: 2, perWave: 3);
        s.Gates = true;

        Assert.True(ClusterFSurge.Start(gm, s, out var message), message);
        var made = ClusterFSurge.Active.Creatures.ToList();
        var gates = ClusterFSurge.Active.Gates.ToList();

        var buffer = new byte[4096];
        var writer = new BufferWriter(buffer, true);
        ClusterFSurge.WriteTo(writer);
        writer.Flush();

        // The shard goes down mid-surge: no End ran, so everything is still in the world.
        ClusterFSurge.ForgetForTests();
        Assert.All(made, c => Assert.False(c.Deleted));
        Assert.All(gates, g => Assert.False(g.Deleted));

        ClusterFSurge.ReadFrom(new BufferReader(buffer));
        Assert.Equal(made.Count + gates.Count, ClusterFSurge.CleanUpAfterRestart());
        Assert.All(made, c => Assert.True(c.Deleted));
        Assert.All(gates, g => Assert.True(g.Deleted));

        // A serial that has come to mean something else is left alone.
        var bystander = new Longsword();
        var other = new byte[64];
        var w = new BufferWriter(other, true);
        w.WriteEncodedInt(ClusterFSurge.CurrentVersion);
        w.WriteEncodedInt(1);
        w.Write(bystander.Serial);
        w.Flush();
        ClusterFSurge.ReadFrom(new BufferReader(other));
        Assert.Equal(0, ClusterFSurge.CleanUpAfterRestart());
        Assert.False(bystander.Deleted);
        bystander.Delete();

        // With no surge running, a save holds nothing to clean up.
        var empty = new byte[64];
        var e = new BufferWriter(empty, true);
        ClusterFSurge.WriteTo(e);
        e.Flush();
        ClusterFSurge.ReadFrom(new BufferReader(empty));
        Assert.Equal(0, ClusterFSurge.CleanUpAfterRestart());

        // An unknown version fails loudly.
        var bad = new byte[16];
        var b = new BufferWriter(bad, true);
        b.WriteEncodedInt(ClusterFSurge.CurrentVersion + 1);
        b.Flush();
        Assert.Throws<System.IO.InvalidDataException>(() => ClusterFSurge.ReadFrom(new BufferReader(bad)));
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void DamageShareAndPresenceAreCountedAsSpecified()
    {
        ShardTestClock.Arm();
        var gm = NewCharacter(AccessLevel.GameMaster);
        var a = NewCharacter();
        var b = NewCharacter();
        var c = NewCharacter();
        var d = NewCharacter();
        var far = NewCharacter(at: new Point3D(Spot.X + 40, Spot.Y, Spot.Z));
        foreach (var p in new[] { a, b, c, d, far })
        {
            p.Blessed = true; // the ogres cannot hurt them; the facts are about who hurt the ogres
        }

        Template("P230 ogres", "OgreLord");
        var s = Settings("P230 ogres", waves: 1, perWave: 2);
        s.PrizeDamage = true;
        s.PrizePresent = true;
        s.TopCount = 1;
        s.TopPrize = "Gold 2000";
        s.SharePrize = "Gold 500";

        Assert.True(ClusterFSurge.Start(gm, s, out var message), message);
        var run = ClusterFSurge.Active;
        var ogres = run.Creatures.ToList();

        // a: 50 on one ogre; d leaves after that first second (c stays the whole run; far never comes).
        OnTheTick(run, () => ogres[0].Damage(50, a));
        d.MoveToWorld(new Point3D(Spot.X + 30, Spot.Y, Spot.Z), Map.Trammel);

        // Minutes later, after that entry has expired and the ogre healed (a new DamageEntry): a 80 on the same ogre and
        // 20 on the other (150 in all); b 40. The GM's own damage never counts.
        ShardTestClock.Advance(DamageEntry.ExpireDelay + TimeSpan.FromSeconds(30));
        OnTheTick(run, () =>
        {
            ogres[0].Damage(80, a);
            ogres[1].Damage(20, a);
            ogres[1].Damage(40, b);
            ogres[1].Damage(30, gm);
        });
        ShardTestClock.AdvanceSeconds(10);

        Assert.Equal(150, run.Damage[a]);
        Assert.Equal(40, run.Damage[b]);
        Assert.False(run.Damage.ContainsKey(gm));
        Assert.True(run.WasPresent(c));
        Assert.False(run.WasPresent(d), $"d present {run.PresentTicks.GetValueOrDefault(d)} of {run.Ticks}");
        Assert.False(run.PresentTicks.ContainsKey(far));
        Assert.False(run.PresentTicks.ContainsKey(gm)); // staff are not players

        var (top, share) = ClusterFSurge.Winners(run);
        Assert.Equal([a], top);
        Assert.Equal(new HashSet<Mobile> { b, c }, share.ToHashSet());

        Assert.True(ClusterFSurge.End(gm, out message), message);
        Assert.Equal(2000, Gold(a)); // top: once, not also a share
        Assert.Equal(500, Gold(b));  // a damage dealer outside the top
        Assert.Equal(500, Gold(c));  // present
        Assert.Equal(0, Gold(d));
        Assert.Equal(0, Gold(far));
        Assert.Equal(3, run.Awards.Count);

        // Extras: only when the surge had them on, and only to a player.
        Assert.False(ClusterFSurge.GiveExtra(gm, d, out _));
        _out.WriteLine(string.Join("; ", run.Awards.Select(x => $"{x.Player.Name} {x.Prize} ({x.Why})")));
    }

    [Fact]
    public void TheGmHandsOutExtrasAndPrizesCanBeAnyMix()
    {
        ShardTestClock.Arm();
        var gm = NewCharacter(AccessLevel.GameMaster);
        var a = NewCharacter();
        a.Blessed = true;
        Template("P230 mix", "Orc");

        // Damage only, presence off: a player who only stood there gets nothing.
        var s = Settings("P230 mix", waves: 1, perWave: 1);
        s.PrizeDamage = true;
        s.PrizePresent = false;
        s.PrizeExtra = true;
        s.ExtraPrize = "Gold 777";
        Assert.True(ClusterFSurge.Start(gm, s, out var message), message);
        ShardTestClock.AdvanceSeconds(5);
        Assert.True(ClusterFSurge.GiveExtra(gm, a, out message), message);
        Assert.Equal(777, Gold(a));
        Assert.False(ClusterFSurge.GiveExtra(gm, gm, out _)); // not to staff
        Assert.True(ClusterFSurge.End(gm, out message), message);
        Assert.Equal(777, Gold(a));
        Assert.True(ClusterFSurge.GiveExtra(gm, a, out message), message); // from the summary, after End
        Assert.Equal(1554, Gold(a));

        // A prize the GM types must be an item type.
        s.TopPrize = "NotAnItem 5";
        Assert.Contains(ClusterFSurge.Problems(s), p => p.StartsWith("Top prize"));
        s.TopPrize = "Longsword 50";
        Assert.Contains(ClusterFSurge.Problems(s), p => p.StartsWith("Top prize")); // more than ten unstackable
        s.TopPrize = "Longsword 2";
        Assert.DoesNotContain(ClusterFSurge.Problems(s), p => p.StartsWith("Top prize"));
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void TheNoLootZoneKeepsADeadPlayersItems()
    {
        ShardTestClock.Arm();
        var gm = NewCharacter(AccessLevel.GameMaster);
        Template("P230 noloot", "Orc");
        var s = Settings("P230 noloot", waves: 1, perWave: 1);
        s.NoLoot = true;
        Assert.True(ClusterFSurge.Start(gm, s, out var message), message);

        (PlayerMobile Pm, Item Worn, Item Packed) Victim(Point3D at)
        {
            var pm = NewCharacter(at: at);
            var robe = new Robe();
            pm.AddItem(robe); // AddItem, not EquipItem: the test host has no tile data to check a layer against (D27)
            var sword = new Longsword();
            pm.Backpack.DropItem(sword);
            return (pm, robe, sword);
        }

        var inside = Victim(new Point3D(Spot.X + 3, Spot.Y - 2, Spot.Z));
        inside.Pm.Kill();
        Assert.False(inside.Pm.Alive);
        Assert.Same(inside.Pm.Backpack, inside.Worn.Parent);
        Assert.Same(inside.Pm.Backpack, inside.Packed.Parent);
        Assert.Empty(inside.Pm.Corpse.Items);

        var outside = Victim(new Point3D(Spot.X + 25, Spot.Y, Spot.Z));
        outside.Pm.Kill();
        Assert.Same(outside.Pm.Corpse, outside.Worn.Parent);
        Assert.Same(outside.Pm.Corpse, outside.Packed.Parent);

        // After End the zone is gone.
        Assert.True(ClusterFSurge.End(gm, out message), message);
        var after = Victim(new Point3D(Spot.X + 1, Spot.Y, Spot.Z));
        after.Pm.Kill();
        Assert.Same(after.Pm.Corpse, after.Packed.Parent);
    }

    // ---------------------------------------------------------------- 6

    [Fact]
    public void TemplatesSaveAndLoad()
    {
        // The three starters, seeded once, every creature a real type.
        Assert.True(ClusterFSurgeTemplates.Seeded);
        Assert.Equal(["Orc raid", "Undead rising", "Ogre warband"], ClusterFSurgeTemplates.All.Select(t => t.Name).ToArray());
        foreach (var t in ClusterFSurgeTemplates.Starters)
        {
            Assert.All(t.Creatures, c => Assert.NotNull(ClusterFSurgeTemplates.CreatureType(c.TypeName)));
            Assert.True(!t.HasBoss || ClusterFSurgeTemplates.CreatureType(t.BossType) != null);
            _out.WriteLine($"{t.Name}: {t.Describe()}");
        }

        // Bad input is refused by name.
        Assert.False(ClusterFSurgeTemplates.Save("Bad", "NotACreature*2", "", "", out var why));
        Assert.Contains("NotACreature", why);
        Assert.False(ClusterFSurgeTemplates.Save("Bad", "Orc*0", "", "", out _));
        Assert.False(ClusterFSurgeTemplates.Save("Bad", "", "", "", out _));
        Assert.False(ClusterFSurgeTemplates.Save("Bad", "Longsword", "", "", out _)); // an item, not a creature
        Assert.False(ClusterFSurgeTemplates.Save("Bad", "Orc", "Gold", "", out _));
        Assert.False(ClusterFSurgeTemplates.Save("  ", "Orc", "", "", out _));

        Template("P230 saved", "orc*2, zombie", "ogrelord", "Big One");
        var saved = ClusterFSurgeTemplates.Find("p230 SAVED");
        Assert.Equal([new SurgeEntry("Orc", 2), new SurgeEntry("Zombie", 1)], saved.Creatures.ToArray());
        Assert.Equal(("OgreLord", "Big One"), (saved.BossType, saved.BossName));

        var buffer = SaveTemplates();
        foreach (var t in ClusterFSurgeTemplates.All.ToList())
        {
            Assert.True(ClusterFSurgeTemplates.Delete(t.Name));
        }

        ClusterFSurgeTemplates.SeedIfNeverSeeded(); // deleted starters do not come back
        Assert.Empty(ClusterFSurgeTemplates.All);

        ClusterFSurgeTemplates.ReadFrom(new BufferReader(buffer));
        Assert.Equal(4, ClusterFSurgeTemplates.All.Count);
        Assert.True(ClusterFSurgeTemplates.Seeded);
        var loaded = ClusterFSurgeTemplates.Find("P230 saved");
        Assert.Equal(saved.Creatures.ToArray(), loaded.Creatures.ToArray());
        Assert.Equal(("OgreLord", "Big One"), (loaded.BossType, loaded.BossName));

        var bad = new byte[16];
        var w = new BufferWriter(bad, true);
        w.WriteEncodedInt(ClusterFSurgeTemplates.CurrentVersion + 1);
        w.Flush();
        Assert.Throws<System.IO.InvalidDataException>(() => ClusterFSurgeTemplates.ReadFrom(new BufferReader(bad)));

        // A new template from its own gump.
        var gm = NewCharacter(AccessLevel.GameMaster);
        var state = new StaffHubState { Tab = StaffHubTab.Events };
        new StaffHubTemplateGump(state, "", "", "", "").Respond(gm, true, "P230 gumped", "Skeleton*2", "", "");
        Assert.NotNull(ClusterFSurgeTemplates.Find("P230 gumped"));
        Assert.Equal("P230 gumped", state.Surge.Template);
    }

    // ---------------------------------------------------------------- 7

    [Fact]
    public void TheEventsTabRunsASurgeThroughItsButtons()
    {
        ShardTestClock.Arm();
        using var live = new TestCenterSwitch(false); // surges are not test-only
        var gm = NewCharacter(AccessLevel.GameMaster);
        var state = new StaffHubState { Tab = StaffHubTab.Events };

        var g = new StaffHubGump(gm, state);
        var buttons = g.Entries.OfType<GumpButton>().Select(x => x.ButtonID).ToHashSet();
        Assert.Contains(StaffHubGump.BtnTabBase + 3, buttons); // the Events tab is a live button now
        Assert.Contains(StaffHubGump.BtnSurgeStart, buttons);
        Assert.Contains(StaffHubGump.BtnSurgeDryRun, buttons);
        Assert.Equal(3, g.Templates.Count);
        Assert.All(g.Entries.OfType<GumpRadio>(), r => Assert.Equal((2117, 2118), (r.InactiveID, r.ActiveID)));
        Assert.All(g.Entries.OfType<GumpCheck>(), r => Assert.Equal((210, 211), (r.InactiveID, r.ActiveID)));
        Assert.Equal(5, g.Entries.OfType<GumpCheck>().Count());
        Assert.Equal(9270, g.Entries.OfType<GumpBackground>().First().GumpID);

        StaffHubGump.SurgeInput Input(int template, params int[] checks) =>
            new("5", "2", "3", "20", "1", "Gold 100", "Gold 10", "Gold 1", [StaffHubGump.SwitchTemplateBase + template, ..checks]);

        // Here, with the boxes filled in: the settings take them.
        g.Respond(gm, StaffHubGump.BtnSurgeHere, surge: Input(1, StaffHubGump.SwitchNoLoot, StaffHubGump.SwitchPresent));
        var s = state.Surge;
        Assert.Equal(("Undead rising", 5, 2, 3, 20), (s.Template, s.Radius, s.Waves, s.PerWave, s.IntervalSeconds));
        Assert.Equal((Spot, Map.Trammel), (s.Center, s.Map));
        Assert.Equal((true, false, true, false, false), (s.NoLoot, s.PrizeDamage, s.PrizePresent, s.PrizeExtra, s.Gates));

        // A bad number is named, and Start refuses.
        g = new StaffHubGump(gm, state);
        g.Respond(gm, StaffHubGump.BtnSurgeStart, surge: Input(1) with { Waves = "lots" });
        Assert.Null(ClusterFSurge.Active);
        Assert.Contains(ClusterFSurge.Problems(state.Surge), p => p.StartsWith("Waves"));

        // Dry run makes nothing; Start runs it.
        g = new StaffHubGump(gm, state);
        g.Respond(gm, StaffHubGump.BtnSurgeDryRun, surge: Input(1, StaffHubGump.SwitchNoLoot));
        Assert.Null(ClusterFSurge.Active);

        g = new StaffHubGump(gm, state);
        g.Respond(gm, StaffHubGump.BtnSurgeStart, surge: Input(1, StaffHubGump.SwitchNoLoot));
        var run = ClusterFSurge.Active;
        Assert.NotNull(run);
        Assert.Equal(("Undead rising", 3), (run.Template.Name, run.Creatures.Count));
        Assert.All(run.Creatures, c => Assert.True(c is Skeleton or Zombie or Ghoul or SkeletalMage or BoneKnight));

        // While it runs the tab shows it, with End and Go there.
        g = new StaffHubGump(gm, state);
        buttons = g.Entries.OfType<GumpButton>().Select(x => x.ButtonID).ToHashSet();
        Assert.Contains(StaffHubGump.BtnSurgeEnd, buttons);
        Assert.Contains(StaffHubGump.BtnSurgeGo, buttons);
        Assert.DoesNotContain(StaffHubGump.BtnSurgeStart, buttons);
        Assert.Empty(g.Entries.OfType<GumpTextEntry>());

        gm.MoveToWorld(new Point3D(Spot.X + 100, Spot.Y, Spot.Z), Map.Trammel);
        g.Respond(gm, StaffHubGump.BtnSurgeGo);
        Assert.Equal(Spot, gm.Location);

        var made = run.Creatures.ToList();
        g.Respond(gm, StaffHubGump.BtnSurgeEnd);
        Assert.Null(ClusterFSurge.Active);
        Assert.All(made, c => Assert.True(c.Deleted));

        // The summary, then New surge back to the setup.
        g = new StaffHubGump(gm, state);
        Assert.Contains(StaffHubGump.BtnSurgeNew, g.Entries.OfType<GumpButton>().Select(x => x.ButtonID));
        g.Respond(gm, StaffHubGump.BtnSurgeNew);
        Assert.Null(ClusterFSurge.Last);

        // Delete a template from its row.
        g = new StaffHubGump(gm, state);
        var index = g.Templates.ToList().IndexOf("Orc raid");
        g.Respond(gm, StaffHubGump.BtnTemplateDeleteBase + index, surge: Input(0));
        Assert.Null(ClusterFSurgeTemplates.Find("Orc raid"));

        // Below GameMaster the hub answers nothing.
        var counselor = NewCharacter(AccessLevel.Counselor);
        new StaffHubGump(gm, state).Respond(counselor, StaffHubGump.BtnSurgeStart, surge: Input(0));
        Assert.Null(ClusterFSurge.Active);
    }
}
