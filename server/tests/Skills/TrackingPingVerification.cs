// TrackingPingVerification.cs
//
// cc-P625, Tracking Ping stage 1: customizations ClusterFTrackingPing and server/patches/Tracking-ping-offer.patch.
// Design: shard-migration notes/cc-P563-tracking-ping-design.md; notes: notes/cc-P625-build-tracking-ping.md.
//
//   1. Radius and lifetime match the design's table (18/6 at 90 ... 44/10 at 200), and nothing below 90 Base.
//   2. Gear cannot buy the unlock: 89.9 Base with +20 from a skill mod cannot ping.
//   3. One ping finds exactly the eligible creatures, groups them, counts them in chat, and pays once: a second of focus,
//      then 15 stamina, a reveal, 30 s cooldown, at least 10 s skill delay, no skill gain.
//   4. A focus broken by moving or damage, too little stamina, a dungeon with no mapped floor, or a second ping costs nothing.
//   5. The cooldown holds 30 s from the pulse.
//   6. Players from 120 only, within 12 tiles and in sight, never hidden ones or a hidden owner's pet, each warned once
//      and at most once per 10 s across trackers.
//   7. The pulse re-reads once a second (arrivals in, the newly hidden out), ends with "Ping ended.", and stops when the
//      caster leaves the area or the floor.
//   8. The Tracking skill offers Ping from 90 Base, and its button shares every check with [TrackingPing.
//   9. Over the overload ceiling the ping is refused and costs nothing; under it every contact is kept (no 12 cap).

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Regions;
using Server.SkillHandlers;
using Server.Tests.Network;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class TrackingPingVerification : IDisposable
{
    private static readonly Point3D Spot = new(2300, 2300, 0);

    private readonly ITestOutputHelper _out;
    private readonly List<Mobile> _made = new();
    private readonly List<(Mobile Who, string Text)> _heard = new();
    private readonly RegenRateHandler _stamRate = Mobile.StamRegenRateHandler;

    public TrackingPingVerification(ITestOutputHelper output)
    {
        _out = output;
        // The host regenerates stamina every wheel tick (no RegenRates.Configure, as ShardTestClock says of hits), which
        // would hide every cost and every refund. An hour per point keeps what a ping charged readable.
        Mobile.StamRegenRateHandler = _ => TimeSpan.FromHours(1.0);
        ShardTestClock.Arm();
        ClusterFTrackingPing.ResetAll();
        ClusterFTrackingPing.Heard = (m, text) => _heard.Add((m, text));
    }

    public void Dispose()
    {
        Mobile.StamRegenRateHandler = _stamRate;
        ClusterFTrackingPing.Heard = null;
        ClusterFTrackingPing.ResetAll();
        foreach (var m in _made)
        {
            m.Delete();
        }
    }

    private PlayerMobile Caster(double tracking, Point3D? at = null)
    {
        var pm = new PlayerMobile { Player = true, Race = Race.Human, Name = "Scout" };
        pm.AddItem(new Backpack());
        pm.RawStr = pm.RawDex = pm.RawInt = 100;
        pm.Hits = pm.HitsMax;
        pm.Stam = 100;
        pm.Skills.Tracking.Cap = 200.0;
        pm.Skills.Tracking.Base = tracking;
        pm.MoveToWorld(at ?? Spot, Map.Trammel);
        _made.Add(pm);
        return pm;
    }

    private PlayerMobile Player(string name, int dx, int dy, bool hidden = false)
    {
        var pm = new PlayerMobile { Player = true, Race = Race.Human, Name = name };
        pm.AddItem(new Backpack());
        pm.MoveToWorld(new Point3D(Spot.X + dx, Spot.Y + dy, Spot.Z), Map.Trammel);
        pm.Hidden = hidden;
        _made.Add(pm);
        return pm;
    }

    private T Put<T>(T m, int dx, int dy, int dz = 0, Map map = null) where T : Mobile
    {
        m.MoveToWorld(new Point3D(Spot.X + dx, Spot.Y + dy, Spot.Z + dz), map ?? Map.Trammel);
        _made.Add(m);
        return m;
    }

    private static Horse PetOf(Mobile owner)
    {
        var horse = new Horse();
        horse.Controlled = true;
        horse.ControlMaster = owner;
        return horse;
    }

    private List<string> HeardBy(Mobile m) => _heard.Where(h => h.Who == m).Select(h => h.Text).ToList();

    private void Log(Mobile m)
    {
        foreach (var line in HeardBy(m))
        {
            _out.WriteLine($"{m.Name}: {line}");
        }
    }

    // Focus (1 s) and the first moment of the pulse.
    private static void FireNow() => ShardTestClock.AdvanceSeconds(1.1);

    // ---------------------------------------------------------------- 1

    [Theory]
    [InlineData(89.9, 0, 0)]
    [InlineData(90.0, 18, 6)]
    [InlineData(95.0, 21, 6)]
    [InlineData(100.0, 24, 6)]
    [InlineData(120.0, 28, 6)]
    [InlineData(150.0, 34, 8)]
    [InlineData(200.0, 44, 10)]
    [InlineData(250.0, 44, 10)]
    public void RadiusAndLifetimeMatchTheTable(double skill, int radius, int lifetime)
    {
        Assert.Equal(radius, ClusterFTrackingPing.Radius(skill));
        Assert.Equal(lifetime, ClusterFTrackingPing.Lifetime(skill));
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void GearCannotBuyTheUnlock()
    {
        var pm = Caster(89.9);
        pm.AddSkillMod(new DefaultSkillMod(SkillName.Tracking, "p625-test", true, 20.0));
        Assert.True(pm.Skills.Tracking.Value >= 109.0);

        Assert.False(ClusterFTrackingPing.Begin(pm, false));
        Assert.False(ClusterFTrackingPing.IsFocusing(pm));
        Assert.Contains(HeardBy(pm), t => t.Contains("90 Tracking"));

        pm.Skills.Tracking.Base = 90.0;
        Assert.True(ClusterFTrackingPing.Begin(pm, false));
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void OnePingFindsTheEligibleCountsThemAndPaysOnce()
    {
        var pm = Caster(100.0);
        pm.Skills.Tracking.Cap = 120.0;
        pm.Hidden = true;

        var orcNear = Put(new Orc(), 5, 0);
        var orcFar = Put(new Orc(), -10, 20);
        var wildHorse = Put(new Horse(), 3, 3);
        var myPet = Put(PetOf(pm), 2, 0);

        var hiddenOrc = Put(new Orc(), 4, 0);
        hiddenOrc.Hidden = true;
        var pastRadius = Put(new Orc(), 25, 0);
        var otherFloor = Put(new Orc(), 1, 1, 20);
        var otherFacet = Put(new Orc(), 1, 1, 0, Map.Felucca);
        var staff = Player("Staff", 6, 0);
        staff.AccessLevel = AccessLevel.GameMaster;
        var bystander = Player("Bystander", 5, 1);
        var deadOrc = Put(new Orc(), -3, 0);
        deadOrc.Kill();

        Assert.True(ClusterFTrackingPing.Begin(pm, false));
        Assert.True(ClusterFTrackingPing.IsFocusing(pm));
        Assert.Equal(100, pm.Stam); // nothing is paid during the focus

        var tickBefore = Core.TickCount;
        FireNow();
        Log(pm);

        Assert.True(ClusterFTrackingPing.IsActive(pm));
        Assert.Null(pm.Spell); // the focus gave the spell slot back
        var contacts = ClusterFTrackingPing.ContactsOf(pm);
        Assert.Equal(
            new[] { orcNear.Serial, orcFar.Serial, wildHorse.Serial, myPet.Serial }.OrderBy(s => s.Value),
            contacts.Select(c => c.Mobile.Serial).OrderBy(s => s.Value)
        );
        Assert.Equal(PingGroup.Hostile, contacts.Single(c => c.Mobile == orcNear).Group);
        Assert.Equal(PingGroup.NeutralAnimal, contacts.Single(c => c.Mobile == wildHorse).Group);
        Assert.Equal(PingGroup.Friendly, contacts.Single(c => c.Mobile == myPet).Group);
        Assert.Equal("your pet", contacts.Single(c => c.Mobile == myPet).Label);
        Assert.Equal("orc", contacts.Single(c => c.Mobile == orcNear).Label); // its kind, not its own name
        Assert.NotEqual("orc", orcNear.Name);
        Assert.NotEqual(PingThreat.None, contacts.Single(c => c.Mobile == orcNear).Threat);

        var summary = HeardBy(pm).Single(t => t.StartsWith("Ping: "));
        Assert.StartsWith("Ping: 2 hostiles, 1 neutral animal, 1 friendly. Hostiles: ", summary);
        Assert.EndsWith(" 6 seconds.", summary);

        Assert.Equal(100 - ClusterFTrackingPing.StaminaCost, pm.Stam);
        Assert.False(pm.Hidden);
        Assert.InRange(ClusterFTrackingPing.CooldownLeft(pm).TotalSeconds, 29.0, 30.0);
        Assert.True(pm.NextSkillTime >= tickBefore + 10_000);
        Assert.Equal(100.0, pm.Skills.Tracking.Base); // no skill gain from a ping
        Assert.True(hiddenOrc.Hidden);
        Assert.Empty(HeardBy(bystander));
        _ = pastRadius; _ = otherFloor; _ = otherFacet;
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void AFocusThatBreaksOrIsRefusedCostsNothing()
    {
        var pm = Caster(100.0);
        var orc = Put(new Orc(), 5, 0);

        // Moving during the focus.
        Assert.True(ClusterFTrackingPing.Begin(pm, false));
        Assert.False(ClusterFTrackingPing.Begin(pm, false)); // one at a time
        pm.Location = new Point3D(Spot.X + 1, Spot.Y, Spot.Z);
        FireNow();
        Assert.False(ClusterFTrackingPing.IsActive(pm));
        Assert.Equal(100, pm.Stam);
        Assert.Equal(TimeSpan.Zero, ClusterFTrackingPing.CooldownLeft(pm));
        Assert.Contains(HeardBy(pm), t => t.StartsWith("Your concentration breaks"));

        // Damage during the focus.
        Assert.True(ClusterFTrackingPing.Begin(pm, false));
        pm.Damage(5, orc);
        Assert.False(ClusterFTrackingPing.IsFocusing(pm)); // at once, even though the host heals the hit straight back
        Assert.Null(pm.Spell);
        FireNow();
        Assert.False(ClusterFTrackingPing.IsActive(pm));
        Assert.Equal(TimeSpan.Zero, ClusterFTrackingPing.CooldownLeft(pm));

        // Too tired.
        pm.Stam = ClusterFTrackingPing.StaminaCost - 1;
        Assert.False(ClusterFTrackingPing.Begin(pm, false));
        Assert.Contains(HeardBy(pm), t => t == "You are too tired to ping.");
        pm.Stam = 100;

        // The command route respects a running skill delay; the Tracking menu's own button does not see its own.
        pm.NextSkillTime = Core.TickCount + 5_000;
        Assert.False(ClusterFTrackingPing.Begin(pm, false));
        Assert.True(ClusterFTrackingPing.Begin(pm, true));
        ClusterFTrackingPing.Stop(pm);
        pm.NextSkillTime = Core.TickCount;

        // A dungeon with no mapped floor.
        var dungeon = new DungeonRegion("P625 test dungeon", Map.Trammel, 50,
            new Rectangle3D(new Point3D(Spot.X - 5, Spot.Y - 5, -128), new Point3D(Spot.X + 5, Spot.Y + 5, 127)));
        dungeon.Register();
        try
        {
            Assert.False(ClusterFTrackingPing.Begin(pm, false));
            Assert.Contains(HeardBy(pm), t => t == ClusterFTrackingPing.UnmappedFloor);
        }
        finally
        {
            dungeon.Unregister();
        }

        Assert.Equal(100, pm.Stam);
        Assert.Equal(TimeSpan.Zero, ClusterFTrackingPing.CooldownLeft(pm));
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void TheCooldownHoldsThirtySecondsFromThePulse()
    {
        var pm = Caster(100.0);
        Put(new Orc(), 5, 0);

        Assert.True(ClusterFTrackingPing.Begin(pm, false));
        FireNow();
        ShardTestClock.AdvanceSeconds(7.0);
        Assert.False(ClusterFTrackingPing.IsActive(pm));
        Assert.Contains(HeardBy(pm), t => t == ClusterFTrackingPing.Ended);

        pm.NextSkillTime = Core.TickCount;
        Assert.False(ClusterFTrackingPing.Begin(pm, false));
        Assert.Contains(HeardBy(pm), t => t.StartsWith("You must wait") && t.EndsWith("before you can ping again."));

        ShardTestClock.AdvanceSeconds(23.0);
        pm.NextSkillTime = Core.TickCount;
        Assert.True(ClusterFTrackingPing.Begin(pm, false));
    }

    // ---------------------------------------------------------------- 6

    [Fact]
    public void PlayersFrom120OnlyNearInSightNeverHiddenAndWarnedOnce()
    {
        var below = Caster(119.9);
        var scout = Caster(120.0);
        var second = Caster(120.0, new Point3D(Spot.X, Spot.Y + 1, Spot.Z));

        var near = Player("Near", 8, 0);
        var far = Player("Far", 15, 0);
        var hidden = Player("Hidden", 3, 0, hidden: true);
        var hiddenOwnersPet = Put(PetOf(hidden), 4, 0);
        var visibleOwner = Player("Owner", 20, 0);
        var petNear = Put(PetOf(visibleOwner), 0, 5);
        var petFar = Put(PetOf(visibleOwner), 0, 18);

        // Below 120: no player, and no other player's pet.
        Assert.True(ClusterFTrackingPing.Begin(below, false));
        FireNow();
        Assert.DoesNotContain(ClusterFTrackingPing.ContactsOf(below), c => c.Mobile.Player || c.Mobile == petNear);
        Assert.Empty(HeardBy(near));

        Assert.True(ClusterFTrackingPing.Begin(scout, false));
        FireNow();
        Log(scout);
        var found = ClusterFTrackingPing.ContactsOf(scout).Select(c => c.Mobile).ToList();
        Assert.Contains(near, found);
        Assert.Contains(petNear, found);
        Assert.Contains(below, found); // another tracker standing beside you is a visible player too
        Assert.DoesNotContain(far, found);
        Assert.DoesNotContain(hidden, found);
        Assert.DoesNotContain(hiddenOwnersPet, found);
        Assert.DoesNotContain(visibleOwner, found);
        Assert.DoesNotContain(petFar, found);
        Assert.Equal(PingGroup.Player, ClusterFTrackingPing.ContactsOf(scout).Single(c => c.Mobile == near).Group);
        Assert.Equal("a pet", ClusterFTrackingPing.ContactsOf(scout).Single(c => c.Mobile == petNear).Label);

        Assert.Equal(new[] { ClusterFTrackingPing.WatchedWarning }, HeardBy(near));
        Assert.Empty(HeardBy(hidden));
        Assert.Empty(HeardBy(far));
        Assert.True(hidden.Hidden);

        // Refreshes inside the same pulse and a second tracker within 10 s do not warn again.
        ShardTestClock.AdvanceSeconds(2.0);
        Assert.True(ClusterFTrackingPing.Begin(second, false));
        FireNow();
        Assert.Contains(near, ClusterFTrackingPing.ContactsOf(second).Select(c => c.Mobile));
        Assert.Single(HeardBy(near));

        // The player rule reads where the caster stands now: walk 13 tiles off and Near drops out at the next refresh.
        scout.Location = new Point3D(Spot.X - 5, Spot.Y, Spot.Z);
        ShardTestClock.AdvanceSeconds(1.0);
        Assert.True(ClusterFTrackingPing.IsActive(scout));
        Assert.DoesNotContain(near, ClusterFTrackingPing.ContactsOf(scout).Select(c => c.Mobile));
    }

    // ---------------------------------------------------------------- 7

    [Fact]
    public void ThePulseFollowsArrivalsAndDeparturesThenEnds()
    {
        var pm = Caster(100.0);
        var first = Put(new Orc(), 5, 0);

        Assert.True(ClusterFTrackingPing.Begin(pm, false));
        FireNow();
        Assert.Equal(new Mobile[] { first }, ClusterFTrackingPing.ContactsOf(pm).Select(c => c.Mobile));

        var arrival = Put(new Orc(), -6, 2);
        ShardTestClock.AdvanceSeconds(1.0);
        Assert.Equal(2, ClusterFTrackingPing.ContactsOf(pm).Count);

        first.Hidden = true;
        ShardTestClock.AdvanceSeconds(1.0);
        Assert.Equal(new Mobile[] { arrival }, ClusterFTrackingPing.ContactsOf(pm).Select(c => c.Mobile));

        ShardTestClock.AdvanceSeconds(4.0);
        Assert.False(ClusterFTrackingPing.IsActive(pm));
        Assert.Empty(ClusterFTrackingPing.ContactsOf(pm));
        Assert.Equal(1, HeardBy(pm).Count(t => t == ClusterFTrackingPing.Ended));

        // Leaving the area read ends it early.
        ClusterFTrackingPing.ResetAll();
        pm.NextSkillTime = Core.TickCount;
        Assert.True(ClusterFTrackingPing.Begin(pm, false));
        FireNow();
        pm.Location = new Point3D(Spot.X + 30, Spot.Y, Spot.Z);
        ShardTestClock.AdvanceSeconds(1.0);
        Assert.False(ClusterFTrackingPing.IsActive(pm));
        Assert.Contains(HeardBy(pm), t => t.StartsWith("You have left the area you read"));

        // So does climbing off the floor, and a skill that drops below what the ping started with.
        ClusterFTrackingPing.ResetAll();
        pm.Location = Spot;
        pm.NextSkillTime = Core.TickCount;
        Assert.True(ClusterFTrackingPing.Begin(pm, false));
        FireNow();
        pm.Z = Spot.Z + 20;
        ShardTestClock.AdvanceSeconds(1.0);
        Assert.False(ClusterFTrackingPing.IsActive(pm));

        ClusterFTrackingPing.ResetAll();
        pm.Z = Spot.Z;
        pm.NextSkillTime = Core.TickCount;
        Assert.True(ClusterFTrackingPing.Begin(pm, false));
        FireNow();
        pm.Skills.Tracking.Base = 95.0;
        ShardTestClock.AdvanceSeconds(1.0);
        Assert.False(ClusterFTrackingPing.IsActive(pm));
        Assert.Contains(HeardBy(pm), t => t.StartsWith("Your tracking falters"));
    }

    // ---------------------------------------------------------------- 8

    private static readonly System.Reflection.MethodInfo GumpRemove = typeof(GumpSystem).GetMethod("Remove",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static, null,
        [typeof(NetState), typeof(BaseGump)], null);

    private static void Press(BaseGump gump, NetState ns, int buttonId)
    {
        Assert.NotNull(GumpRemove);
        GumpRemove.Invoke(null, [ns, gump]);
        gump.OnResponse(ns, new RelayInfo(buttonId, ReadOnlySpan<int>.Empty, ReadOnlySpan<ushort>.Empty,
            ReadOnlySpan<Range>.Empty, ReadOnlySpan<byte>.Empty));
    }

    [Fact]
    public void TheTrackingSkillOffersPingFrom90AndItsButtonSharesTheChecks()
    {
        var pm = Caster(89.9);
        Put(new Horse(), 5, 0); // an animal that only answers attacks: the caster has a NetState here
        var ns = PacketTestUtilities.CreateTestNetState();
        pm.NetState = ns;
        ns.Mobile = pm;

        try
        {
            Tracking.OnUse(pm);
            Assert.NotNull(ns.GetGumps().Find<TrackWhatGump>());
            Assert.Null(ns.GetGumps().Find<ClusterFTrackingPing.PingOfferGump>());

            pm.Skills.Tracking.Base = 90.0;
            Tracking.OnUse(pm);
            Assert.NotNull(ns.GetGumps().Find<TrackWhatGump>());
            var offer = ns.GetGumps().Find<ClusterFTrackingPing.PingOfferGump>();
            Assert.NotNull(offer);

            // The skill use that sent the menu set its own delay; the button still works.
            pm.NextSkillTime = Core.TickCount + 10_000;
            Press(offer, ns, ClusterFTrackingPing.PingOfferGump.PingButton);
            Assert.True(ClusterFTrackingPing.IsFocusing(pm));
            FireNow();
            Assert.True(ClusterFTrackingPing.IsActive(pm));

            // The results button opens the list of what the ping found.
            Tracking.OnUse(pm);
            offer = ns.GetGumps().Find<ClusterFTrackingPing.PingOfferGump>();
            Press(offer, ns, ClusterFTrackingPing.PingOfferGump.ResultsButton);
            Assert.NotNull(ns.GetGumps().Find<ClusterFTrackingPing.PingResultsGump>());

            // After the pulse, the cooldown refuses both entry points alike.
            ShardTestClock.AdvanceSeconds(7.0);
            pm.NextSkillTime = Core.TickCount;
            Assert.False(ClusterFTrackingPing.Begin(pm, false));
            Tracking.OnUse(pm);
            offer = ns.GetGumps().Find<ClusterFTrackingPing.PingOfferGump>();
            Press(offer, ns, ClusterFTrackingPing.PingOfferGump.PingButton);
            Assert.False(ClusterFTrackingPing.IsFocusing(pm));
            Assert.Equal(2, HeardBy(pm).Count(t => t.StartsWith("You must wait") && t.EndsWith("before you can ping again.")));
        }
        finally
        {
            pm.NetState = null;
            ns.Mobile = null;
        }
    }

    // ---------------------------------------------------------------- 9

    [Fact]
    public void OverTheCeilingNothingIsPaidUnderItEveryContactIsKept()
    {
        var pm = Caster(100.0);
        for (var i = 0; i < 23; i++)
        {
            Put(new Orc(), -11 + i, 7);
        }

        var ceiling = ClusterFTrackingPing.OverloadCeiling;
        try
        {
            ClusterFTrackingPing.OverloadCeiling = 22;
            Assert.True(ClusterFTrackingPing.Begin(pm, false));
            FireNow();
            Assert.False(ClusterFTrackingPing.IsActive(pm));
            Assert.Contains(HeardBy(pm), t => t == ClusterFTrackingPing.Overloaded);
            Assert.Equal(100, pm.Stam);
            Assert.Equal(TimeSpan.Zero, ClusterFTrackingPing.CooldownLeft(pm));

            ClusterFTrackingPing.OverloadCeiling = 23;
            pm.NextSkillTime = Core.TickCount;
            Assert.True(ClusterFTrackingPing.Begin(pm, false));
            FireNow();
            Assert.Equal(23, ClusterFTrackingPing.ContactsOf(pm).Count);
            Assert.StartsWith("Ping: 23 hostiles.", HeardBy(pm).Single(t => t.StartsWith("Ping: ")));
            Assert.All(ClusterFTrackingPing.ContactsOf(pm), c => Assert.StartsWith("orc - hostile, ", ClusterFTrackingPing.RowText(pm, c)));
        }
        finally
        {
            ClusterFTrackingPing.OverloadCeiling = ceiling;
        }
    }
}
