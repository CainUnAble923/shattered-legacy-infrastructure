// OldHavenBurrowsRegionVerification.cs
//
// cc-P36 Part C: the New Haven dungeon's region (server/customizations/Regions/OldHavenBurrowsRegion.cs), Chase's decisions
// 2026-10-01 items 2, 3, 5 and 6. Notes: shard-migration notes/cc-P36-dungeon-build-1.md, Part C.
//
// Facts:
//   C1. The region is a DungeonRegion on Trammel over x 5136-5247, y 1128-1263, exactly (corners in, one past out), with the
//       provisional entrance, and Initialize (run at every start) creates it with nothing saved.
//   C2. Every refused travel case says its own line, exactly, through SpellHelper.CheckTravel: recall (and Sacred Journey,
//       the same type) in, gate in, teleport in from outside, mark inside, gate cast inside.
//   C3. Every allowed case passes: recall out, recall and teleport from inside to outside, teleport inside the region,
//       travel that neither starts nor ends inside.
//   C4. Staff are exempt from every refusal.
//   C5. With three hostile creatures attacking a Young player inside, a fourth cannot harm them; one of the three still
//       can; a non-Young player is not limited; a tamed creature does not count and is not limited.

using System;
using System.Collections.Generic;
using Server;
using Server.Accounting;
using Server.Accounting.Security;
using Server.Mobiles;
using Server.Regions;
using Server.Spells;
using Server.Tutorial;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class OldHavenBurrowsRegionVerification : IDisposable
{
    private readonly ITestOutputHelper _out;
    private readonly List<Account> _accounts = new();

    public OldHavenBurrowsRegionVerification(ITestOutputHelper output)
    {
        _out = output;
        OldHavenBurrowsRegion.Initialize();
    }

    private static readonly Point3D Inside = new(5190, 1190, 0);
    private static readonly Point3D InsideToo = new(5195, 1195, 0);
    private static readonly Point3D Outside = new(1220, 1220, 0);   // as YoungPlayerVerification
    private static readonly Point3D JustOutside = new(5130, 1190, 0); // 6 tiles west of the edge, inside teleport range

    private PlayerMobile Player(Point3D at, AccessLevel level = AccessLevel.Player, bool graduated = false)
    {
        var pm = new PlayerMobile { Player = true, AccessLevel = level };
        if (graduated)
        {
            // P371: these region-policy probes need an ordinary graduate who is eligible to leave the island.
            if (AccountSecurity.CurrentAlgorithm == PasswordProtectionAlgorithm.None)
            {
                AccountSecurity.CurrentAlgorithm = PasswordProtectionAlgorithm.PBKDF2;
            }
            var account = new Account($"p371r{Guid.NewGuid():N}"[..16], "test-only");
            _accounts.Add(account);
            account[0] = pm;
            pm.AddItem(new Server.Items.Backpack());
            ClusterFLoops.Graduate(pm);
        }
        pm.MoveToWorld(at, Map.Trammel);
        return pm;
    }

    public void Dispose()
    {
        foreach (var account in _accounts)
        {
            Accounts.Remove(account);
        }
        Server.LootBoxes.ClusterFFanfare.FlushAll();
    }

    private static (bool Ok, string Message) Travel(Mobile m, Point3D to, TravelCheckType type)
    {
        var ok = SpellHelper.CheckTravel(m, Map.Trammel, to, type, out var message);
        return (ok, message?.String);
    }

    // ---------------------------------------------------------------- C1

    [Fact]
    public void TheRegionCoversCandidateAExactly()
    {
        var region = Region.Find(Inside, Map.Trammel);
        Assert.IsType<OldHavenBurrowsRegion>(region);
        Assert.Same(OldHavenBurrowsRegion.Instance, region);
        Assert.IsAssignableFrom<DungeonRegion>(region);
        Assert.Equal(new Point3D(3619, 2620, 0), ((DungeonRegion)region).Entrance);
        Assert.False(region.AllowHousing(null, Inside));

        foreach (var corner in new[] { new Point3D(5136, 1128, 0), new Point3D(5247, 1128, 0), new Point3D(5136, 1263, 0), new Point3D(5247, 1263, 0) })
        {
            Assert.IsType<OldHavenBurrowsRegion>(Region.Find(corner, Map.Trammel));
        }

        foreach (var past in new[] { new Point3D(5135, 1190, 0), new Point3D(5248, 1190, 0), new Point3D(5190, 1127, 0), new Point3D(5190, 1264, 0) })
        {
            Assert.IsNotType<OldHavenBurrowsRegion>(Region.Find(past, Map.Trammel));
        }

        Assert.IsNotType<OldHavenBurrowsRegion>(Region.Find(Inside, Map.Felucca));

        // Initialize runs at every start and is the only thing that makes it: a second call adds nothing, and the region is
        // not a saved entity (nothing in a save to lose or migrate).
        OldHavenBurrowsRegion.Initialize();
        Assert.Same(region, Region.Find(Inside, Map.Trammel));
        Assert.False(typeof(ISerializable).IsAssignableFrom(typeof(OldHavenBurrowsRegion)));
    }

    // ---------------------------------------------------------------- C2

    [Fact]
    public void EachRefusalSaysItsOwnLine()
    {
        var outside = Player(Outside);
        var near = Player(JustOutside);
        var inside = Player(Inside, graduated: true);
        try
        {
            var cases = new List<(string Name, Mobile Who, Point3D To, TravelCheckType Type, string Expected)>
            {
                ("recall in", outside, Inside, TravelCheckType.RecallTo, OldHavenBurrowsRegion.RecallInMessage),
                ("gate in", outside, Inside, TravelCheckType.GateTo, OldHavenBurrowsRegion.GateInMessage),
                ("teleport in", near, Inside, TravelCheckType.TeleportTo, OldHavenBurrowsRegion.TeleportInMessage),
                ("mark inside", inside, inside.Location, TravelCheckType.Mark, OldHavenBurrowsRegion.MarkMessage),
                ("gate cast inside", inside, inside.Location, TravelCheckType.GateFrom, OldHavenBurrowsRegion.GateOutMessage),
                ("recall to a rune inside, from inside", inside, InsideToo, TravelCheckType.RecallTo, OldHavenBurrowsRegion.RecallInMessage)
            };

            foreach (var (name, who, to, type, expected) in cases)
            {
                var (ok, message) = Travel(who, to, type);
                _out.WriteLine($"{name}: {ok} \"{message}\"");
                Assert.False(ok, name);
                Assert.Equal(expected, message);
            }
        }
        finally
        {
            outside.Delete();
            near.Delete();
            inside.Delete();
        }
    }

    // ---------------------------------------------------------------- C3

    [Fact]
    public void LeavingAndTravelElsewhereAreAllowed()
    {
        var outside = Player(Outside);
        var inside = Player(Inside, graduated: true);
        try
        {
            var cases = new List<(string Name, Mobile Who, Point3D To, TravelCheckType Type)>
            {
                ("recall cast inside", inside, inside.Location, TravelCheckType.RecallFrom),
                ("recall out to a rune outside", inside, Outside, TravelCheckType.RecallTo),
                ("teleport from inside", inside, inside.Location, TravelCheckType.TeleportFrom),
                ("teleport inside the region", inside, InsideToo, TravelCheckType.TeleportTo),
                ("teleport out", inside, new Point3D(5131, 1190, 0), TravelCheckType.TeleportTo),
                ("recall elsewhere", outside, new Point3D(1225, 1225, 0), TravelCheckType.RecallTo),
                ("mark elsewhere", outside, outside.Location, TravelCheckType.Mark)
            };

            foreach (var (name, who, to, type) in cases)
            {
                var (ok, message) = Travel(who, to, type);
                _out.WriteLine($"{name}: {ok} \"{message}\"");
                Assert.True(ok, $"{name}: {message}");
            }
        }
        finally
        {
            outside.Delete();
            inside.Delete();
        }
    }

    // ---------------------------------------------------------------- C4

    [Fact]
    public void StaffAreExempt()
    {
        var gmOutside = Player(Outside, AccessLevel.GameMaster);
        var gmInside = Player(Inside, AccessLevel.GameMaster);
        try
        {
            Assert.True(Travel(gmOutside, Inside, TravelCheckType.RecallTo).Ok);
            Assert.True(Travel(gmOutside, Inside, TravelCheckType.GateTo).Ok);
            Assert.True(Travel(gmInside, gmInside.Location, TravelCheckType.Mark).Ok);
            Assert.True(Travel(gmInside, gmInside.Location, TravelCheckType.GateFrom).Ok);
        }
        finally
        {
            gmOutside.Delete();
            gmInside.Delete();
        }
    }

    // ---------------------------------------------------------------- C5

    private static BaseCreature Creature(Point3D at)
    {
        var c = new Mongbat();
        c.MoveToWorld(at, Map.Trammel);
        return c;
    }

    // Pinned's own path: setting Combatant runs CanBeHarmful (and so this region's AllowHarmful), then DoHarmful, which
    // records the creature in the target's Aggressors (Mobile.Combatant, Mobile.DoHarmful, Mobile.AggressiveAction).
    private void Attacking(BaseCreature c, Mobile target)
    {
        c.Combatant = target;
        var mine = target.Aggressors.Find(a => a.Attacker == c);
        _out.WriteLine($"{c.GetType().Name} {c.Serial}: combatant {(c.Combatant == target)}, aggressor entry {mine != null}, expired {mine?.Expired}, hostile {OldHavenBurrowsRegion.IsHostileCreature(c)}");
    }

    [Fact]
    public void AFourthHostileCreatureCannotHarmAYoungPlayer()
    {
        ShardTestClock.Arm(); // Core.Now starts at DateTime.MinValue in the host; aggressor expiry reads it
        Assert.Equal(3, OldHavenBurrowsRegion.MaxHostilesOnYoung);

        var young = Player(Inside);
        young.Young = true;
        var veteran = Player(InsideToo);
        var creatures = new List<BaseCreature>();
        try
        {
            // 0-3 for the Young player, 4-7 for the veteran.
            for (var i = 0; i < 8; i++)
            {
                creatures.Add(Creature(new Point3D(Inside.X + 1 + i % 4, Inside.Y + i / 4, 0)));
            }

            Assert.All(creatures, c => Assert.IsType<OldHavenBurrowsRegion>(c.Region));
            Assert.True(creatures[3].CanBeHarmful(young, false)); // nothing attacking yet

            for (var i = 0; i < 3; i++)
            {
                Attacking(creatures[i], young);
            }

            Assert.Equal(3, OldHavenBurrowsRegion.HostilesAttacking(young));
            Assert.False(creatures[3].CanBeHarmful(young, false));
            Assert.True(creatures[0].CanBeHarmful(young, false)); // one already attacking keeps attacking

            // Through pinned's own path too: the fourth cannot take the Young player as its combatant.
            creatures[3].Combatant = young;
            Assert.Null(creatures[3].Combatant);

            // A non-Young player has no limit.
            for (var i = 4; i < 7; i++)
            {
                Attacking(creatures[i], veteran);
            }

            Assert.Equal(3, OldHavenBurrowsRegion.HostilesAttacking(veteran));
            Assert.True(creatures[7].CanBeHarmful(veteran, false));

            // A tamed creature does not count against the limit, and is not limited by it.
            creatures[3].Controlled = true;
            creatures[3].ControlMaster = veteran;
            Assert.False(OldHavenBurrowsRegion.IsHostileCreature(creatures[3]));
            Assert.Equal(3, OldHavenBurrowsRegion.HostilesAttacking(young));
            _out.WriteLine($"tamed fourth may harm the Young player: {creatures[3].CanBeHarmful(young, false)}");
        }
        finally
        {
            foreach (var c in creatures)
            {
                c.Delete();
            }

            young.Delete();
            veteran.Delete();
        }
    }

    [Fact]
    public void OutsideTheRegionThereIsNoLimit()
    {
        ShardTestClock.Arm();
        var young = Player(Outside);
        young.Young = true;
        var creatures = new List<BaseCreature>();
        try
        {
            for (var i = 0; i < 4; i++)
            {
                creatures.Add(Creature(new Point3D(Outside.X + 1 + i, Outside.Y, 0)));
            }

            for (var i = 0; i < 3; i++)
            {
                Attacking(creatures[i], young);
            }

            Assert.IsNotType<OldHavenBurrowsRegion>(creatures[3].Region);
            Assert.Equal(
                Mobile.AllowHarmfulHandler?.Invoke(creatures[3], young) ?? true,
                creatures[3].CanBeHarmful(young, false)
            );
        }
        finally
        {
            foreach (var c in creatures)
            {
                c.Delete();
            }

            young.Delete();
        }
    }
}
