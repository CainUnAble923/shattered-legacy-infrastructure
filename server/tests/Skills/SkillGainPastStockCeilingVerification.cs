// SkillGainPastStockCeilingVerification.cs
//
// cc-P66 Part B: hook G and the worn-bonus rule in customizations ClusterFSkillGain (Chase's decisions 7 and 8), and
// SkillUsed once per use under ClusterF's attempts. The hook is installed over pinned's four handlers for each fact (the
// test host installs only pinned's, TestServerInitializer via UOContentFixture-npc-speeds.patch) and taken off after.
// Rolls are held at 0, so every roll pinned's CheckSkill makes succeeds and gains (SkillCheck.cs:115, :145). Notes:
// shard-migration notes/cc-P66-clamps-and-hook-g.md, Part B.
//
// Facts, per row (G1, a theory over all 18 stock calls the rows key on):
//   a. at the use's stock maximum - 0.1 hook G rolls nothing (the stock roll is pinned's own);
//   b. at the stock maximum (or the row's floor) it rolls, with a chance below 1, and a use there raises Base;
//   c. at Base = Cap it rolls nothing and Base stays at the cap;
//   d. a worn +15 at Base 185 does not stop it;
//   e. a creature gets nothing.
// Real uses (each red without hook G or the rule):
//   G2. Musicianship 150 playing an instrument gains; Animal Lore past 120 keys on the creature's MinTameSkill.
//   G3. Detect Hidden 150 gains when its sweep reveals a hider or a trapped chest, and not when it finds nothing.
//   G4. Stealing 150 lifting a 10-stone item gains.
//   G5. A worn +15 Tactics at Base 185: the cap-following roll (BaseWeapon.cs:2524) still gains; at Base 200 it does not.
//   G6. SkillEvents.SkillUsed fires once per use with ClusterF's three attempts on (pinned's handler ran three times).
//   G7. Casting schools are not rows: Magery, Necromancy, Chivalry, Spellweaving and Mysticism at their top windows.

using System;
using System.Collections.Generic;
using System.Reflection;
using Server;
using Server.Items;
using Server.Misc;
using Server.Mobiles;
using Server.Random;
using Server.SkillHandlers;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class SkillGainPastStockCeilingVerification
{
    private const double Cap = 200.0;

    private readonly ITestOutputHelper _out;

    public SkillGainPastStockCeilingVerification(ITestOutputHelper output)
    {
        _out = output;
        Stealing.Configure();
        ShardTestHost.EnsureSkillChecks();
    }

    private sealed class ConstantRandom : System.Random
    {
        public double Value;

        protected override double Sample() => Value;
        public override double NextDouble() => Value;
        public override int Next() => (int)(Value * int.MaxValue);
        public override int Next(int maxValue) => (int)(Value * maxValue);
        public override int Next(int minValue, int maxValue) => minValue + (int)(Value * (maxValue - minValue));
        public override long NextInt64(long maxValue) => (long)(Value * maxValue);
    }

    // The shard's hook over pinned's handlers, with the given rate, and every roll held at `roll`.
    private sealed class Hook : IDisposable
    {
        private readonly SkillCheckLocationHandler _location = Mobile.SkillCheckLocationHandler;
        private readonly SkillCheckDirectLocationHandler _directLocation = Mobile.SkillCheckDirectLocationHandler;
        private readonly SkillCheckTargetHandler _target = Mobile.SkillCheckTargetHandler;
        private readonly SkillCheckDirectTargetHandler _directTarget = Mobile.SkillCheckDirectTargetHandler;
        private readonly bool _enabled = ClusterFSkillGain.Enabled;
        private readonly int _attempts = ClusterFSkillGain.ChanceAttempts;
        private readonly int _amount = ClusterFSkillGain.AmountMultiplier;

        public Hook(bool rate = false, double roll = 0.0)
        {
            Mobile.SkillCheckLocationHandler = SkillCheck.Mobile_SkillCheckLocation;
            Mobile.SkillCheckDirectLocationHandler = SkillCheck.Mobile_SkillCheckDirectLocation;
            Mobile.SkillCheckTargetHandler = SkillCheck.Mobile_SkillCheckTarget;
            Mobile.SkillCheckDirectTargetHandler = SkillCheck.Mobile_SkillCheckDirectTarget;
            ClusterFSkillGain.Initialize();
            ClusterFSkillGain.Enabled = rate;
            ClusterFSkillGain.ChanceAttempts = 3;
            ClusterFSkillGain.AmountMultiplier = rate ? 5 : 1;
            BuiltInRng.Generator = new ConstantRandom { Value = roll };
        }

        public void Dispose()
        {
            BuiltInRng.Reset();
            Mobile.SkillCheckLocationHandler = _location;
            Mobile.SkillCheckDirectLocationHandler = _directLocation;
            Mobile.SkillCheckTargetHandler = _target;
            Mobile.SkillCheckDirectTargetHandler = _directTarget;
            ClusterFSkillGain.Enabled = _enabled;
            ClusterFSkillGain.ChanceAttempts = _attempts;
            ClusterFSkillGain.AmountMultiplier = _amount;
        }
    }

    private static PlayerMobile Player(Point3D at)
    {
        var pm = new PlayerMobile { Player = true, Race = Race.Human };
        pm.AddItem(new Backpack());
        pm.RawStr = 100;
        pm.RawDex = 100;
        pm.RawInt = 100;
        pm.Skills.Cap = 58 * 2000;
        pm.MoveToWorld(at, Map.Trammel);
        return pm;
    }

    private static Skill Set(Mobile m, SkillName name, double value)
    {
        var skill = m.Skills[name];
        skill.Cap = Cap;
        skill.Base = value;
        return skill;
    }

    // ---------------------------------------------------------------- G1

    // (skill, location?, stock min, stock max, the target the call passes, the lowest Value hook G rolls at)
    public static IEnumerable<object[]> Rows() =>
    [
        [SkillName.Hiding, true, 0.0, 100.0, "", 100.0],
        [SkillName.Stealth, true, -20.0, 60.0, "", 142.0],      // AR 0
        [SkillName.Stealth, true, 62.0, 142.0, "", 142.0],      // AR 41, the heaviest
        [SkillName.Snooping, false, 0.0, 100.0, "pack", 100.0],
        [SkillName.Poisoning, false, 60.0, 100.0, "item", 100.0],
        [SkillName.Poisoning, false, 95.0, 100.0, "item", 100.0],
        [SkillName.Healing, true, 0.0, 120.0, "", 120.0],
        [SkillName.Veterinary, true, 0.0, 120.0, "", 120.0],
        [SkillName.Begging, false, 0.0, 100.0, "creature", 100.0],
        [SkillName.Forensics, false, 40.0, 100.0, "creature", 100.0],
        [SkillName.Forensics, false, 0.0, 100.0, "item", 100.0],
        [SkillName.AnimalLore, false, 0.0, 120.0, "raptor", 120.0],
        [SkillName.Musicianship, true, 0.0, 120.0, "", 120.0],
        [SkillName.Tracking, true, 21.1, 100.0, "", 100.0],
        [SkillName.TasteID, false, 0.0, 100.0, "food", 100.0],
        [SkillName.Camping, true, 0.0, 100.0, "", 100.0],
        [SkillName.MagicResist, true, 0.0, 120.0, "", 120.0],
        [SkillName.Bushido, true, 70.0, 120.0, "", 120.0],
        [SkillName.Stealing, false, 77.5, 127.5, "item", 127.5]
    ];

    private static object MakeTarget(string kind) => kind switch
    {
        "pack"     => new Backpack(),
        "item"     => new Item(0x1BF2),
        "creature" => new Mongbat(),
        "raptor"   => new Raptor(), // MinTameSkill 107.1 (pinned Mobiles/Monsters/Reptile/Raptor.cs:38)
        "food"     => new Apple(),
        _          => null
    };

    private static void Delete(object o)
    {
        switch (o)
        {
            case Item i:
                i.Delete();
                break;
            case Mobile m:
                m.Delete();
                break;
        }
    }

    private static bool Use(Mobile from, SkillName name, bool location, object target, double min, double max) =>
        location ? from.CheckSkill(name, min, max) : from.CheckTargetSkill(name, target, min, max);

    private static double Chance(Mobile from, SkillName name, bool location, object target, double min, double max) =>
        ClusterFSkillGain.PastStockCeilingChance(
            from,
            from.Skills[name],
            location ? ClusterFSkillGain.CheckKind.Location : ClusterFSkillGain.CheckKind.Target,
            target,
            min,
            max
        );

    [Theory]
    [MemberData(nameof(Rows))]
    public void G1_EachRowRollsOnlyPastItsStockMaximumAndOnlyBelowTheCap(
        SkillName name, bool location, double min, double max, string targetKind, double floor
    )
    {
        var pm = Player(new Point3D(2300, 2300, 0));
        var target = MakeTarget(targetKind);
        var creature = new Mongbat();
        DefaultSkillMod worn = null;

        using var hook = new Hook();

        try
        {
            var skill = Set(pm, name, floor - 0.1);

            // a. Just below: nothing past the stock roll.
            Assert.True(double.IsNaN(Chance(pm, name, location, target, min, max)), $"{name} rolled at {floor - 0.1}");

            // b. At the floor and at 150: a roll below certainty, and a use raises Base.
            foreach (var value in new[] { floor, Math.Max(floor, 150.0) })
            {
                skill.Base = value;
                var chance = Chance(pm, name, location, target, min, max);
                _out.WriteLine($"{name} ({min}, {max}) at {value}: hook G chance {chance:F4}");
                Assert.InRange(chance, 0.0001, 0.9999);
                Use(pm, name, location, target, min, max);
                Assert.True(skill.Base > value, $"{name} did not gain at {value}");
            }

            // c. At the cap: nothing, and Base stays there.
            skill.Base = Cap;
            Assert.True(double.IsNaN(Chance(pm, name, location, target, min, max)));
            Use(pm, name, location, target, min, max);
            Assert.Equal(Cap, skill.Base);

            // d. A worn +15 at Base 185 reads 200 and still rolls. (Animal Lore's window is the creature's: a 160.1 tame,
            // window 160 to 210, plus the 15 worn.)
            if (target is BaseCreature bc)
            {
                bc.MinTameSkill = 160.1;
            }

            skill.Base = 185.0;
            worn = new DefaultSkillMod(name, "P66Worn", true, 15.0) { ObeyCap = true };
            pm.AddSkillMod(worn);
            Assert.Equal(200.0, skill.Value);
            Assert.False(double.IsNaN(Chance(pm, name, location, target, min, max)), $"{name} stopped at 185 + 15 worn");
            Use(pm, name, location, target, min, max);
            Assert.True(skill.Base > 185.0);

            // e. A creature: nothing.
            Set(creature, name, 150.0);
            Assert.True(double.IsNaN(Chance(creature, name, location, target, min, max)));
        }
        finally
        {
            if (worn != null)
            {
                pm.RemoveSkillMod(worn);
            }

            Delete(target);
            creature.Delete();
            pm.Delete();
        }
    }

    // ---------------------------------------------------------------- G2

    [Fact]
    public void G2_MusicianshipGainsByPlayingAndLoreKeysOnTheCreature()
    {
        var pm = Player(new Point3D(2400, 2400, 0));
        var raptor = new Raptor();
        var rabbit = new Rabbit();

        using var hook = new Hook();

        try
        {
            var music = Set(pm, SkillName.Musicianship, 150.0);
            BaseInstrument.CheckMusicianship(pm);
            _out.WriteLine($"Musicianship 150 after one play: {music.Base}");
            Assert.True(music.Base > 150.0);

            var lore = Set(pm, SkillName.AnimalLore, 150.0);
            Assert.Equal(107.1, raptor.MinTameSkill);
            // Raptor: window (107.0, 157.0); a rabbit's tops out far below 150.
            Assert.Equal((150.0 - 107.0) / 50.0, Chance(pm, SkillName.AnimalLore, false, raptor, 0.0, 120.0), 9);
            Assert.True(double.IsNaN(Chance(pm, SkillName.AnimalLore, false, rabbit, 0.0, 120.0)));
            pm.CheckTargetSkill(SkillName.AnimalLore, rabbit, 0.0, 120.0);
            Assert.Equal(150.0, lore.Base);
            pm.CheckTargetSkill(SkillName.AnimalLore, raptor, 0.0, 120.0);
            Assert.True(lore.Base > 150.0);
            // Past the raptor's 157: nothing (the climb past it needs a harder tame, batch T).
            lore.Base = 157.0;
            Assert.True(double.IsNaN(Chance(pm, SkillName.AnimalLore, false, raptor, 0.0, 120.0)));
        }
        finally
        {
            raptor.Delete();
            rabbit.Delete();
            pm.Delete();
        }
    }

    // ---------------------------------------------------------------- G3

    private double SweepAndRead(Action<Point3D> place, Func<PlayerMobile, object> targeted)
    {
        var at = new Point3D(2500, 2500, 0);
        var src = Player(at);
        var detect = Set(src, SkillName.DetectHidden, 150.0);
        place(at);

        var type = typeof(DetectHidden).GetNestedType("InternalTarget", BindingFlags.NonPublic)!;
        var target = Activator.CreateInstance(type, true)!;
        type.GetMethod("OnTarget", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, [src, targeted(src)]);
        var after = detect.Base;
        src.Delete();
        return after;
    }

    [Fact]
    public void G3_DetectHiddenGainsOnlyWhenItFindsSomething()
    {
        using var hook = new Hook();
        var made = new List<IEntity>();

        try
        {
            var nothing = SweepAndRead(_ => { }, src => src);
            _out.WriteLine($"nothing there: {nothing}");
            Assert.Equal(150.0, nothing);

            var hider = SweepAndRead(at =>
            {
                var h = Player(new Point3D(at.X + 3, at.Y, at.Z));
                h.Hidden = true;
                made.Add(h);
            }, src => src);
            _out.WriteLine($"a hider revealed: {hider}");
            Assert.True(hider > 150.0);

            MetalBox box = null;
            var trap = SweepAndRead(at =>
            {
                box = new MetalBox { TrapType = TrapType.MagicTrap, TrapPower = 10 };
                box.MoveToWorld(new Point3D(at.X + 2, at.Y, at.Z), Map.Trammel);
                made.Add(box);
            }, _ => box);
            _out.WriteLine($"a trapped box found: {trap}");
            Assert.True(trap > 150.0);
        }
        finally
        {
            foreach (var e in made)
            {
                e.Delete();
            }
        }
    }

    // ---------------------------------------------------------------- G4

    [Fact]
    public void G4_StealingGainsOnATenStoneSteal()
    {
        var at = new Point3D(2600, 2600, 0);
        var thief = Player(at);
        var stealing = Set(thief, SkillName.Stealing, 150.0);
        var pack = new Backpack();
        pack.MoveToWorld(at, Map.Trammel);
        var item = new Item(0x1BF2) { Weight = 10.0 };
        pack.DropItem(item);

        using var hook = new Hook(roll: 0.999); // the caught roll high; then every gain roll held at 0
        try
        {
            var type = typeof(Stealing).GetNestedType("StealingTarget", BindingFlags.NonPublic)!;
            var target = Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, [thief], null);
            BuiltInRng.Generator = new ConstantRandom { Value = 0.0 };
            var stolen = type.GetMethod("TryStealItem", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(target, [item, false]);
            _out.WriteLine($"Stealing 150, a 10-stone item: stolen {stolen != null}, after {stealing.Base}");
            Assert.NotNull(stolen);
            Assert.True(stealing.Base > 150.0);
        }
        finally
        {
            item.Delete();
            pack.Delete();
            thief.Delete();
        }
    }

    // ---------------------------------------------------------------- G5

    [Fact]
    public void G5_AWornBonusNoLongerStopsACapFollowingUse()
    {
        var pm = Player(new Point3D(2700, 2700, 0));
        var tactics = Set(pm, SkillName.Tactics, 185.0);
        var ring = new GoldRing();
        ring.SkillBonuses.SetValues(0, SkillName.Tactics, 15.0);

        using var hook = new Hook();

        try
        {
            Assert.True(pm.EquipItem(ring));
            Assert.Equal(200.0, tactics.Value);

            // BaseWeapon.cs:2524's call, as a weapon hit makes it.
            pm.CheckSkill(SkillName.Tactics, 0.0, pm.Skills.Tactics.Cap);
            _out.WriteLine($"Tactics 185 + 15 worn after one hit: {tactics.Base}");
            Assert.True(tactics.Base > 185.0);

            tactics.Base = Cap;
            pm.CheckSkill(SkillName.Tactics, 0.0, pm.Skills.Tactics.Cap);
            Assert.Equal(Cap, tactics.Base);

            // Any other maximum is left alone.
            Assert.Equal(120.0, ClusterFSkillGain.EffectiveMax(pm, tactics, 120.0));
        }
        finally
        {
            ring.Delete();
            pm.Delete();
        }
    }

    // ---------------------------------------------------------------- G6

    [Fact]
    public void G6_SkillUsedFiresOncePerUseUnderThreeAttempts()
    {
        var pm = Player(new Point3D(2800, 2800, 0));
        var camping = Set(pm, SkillName.Camping, 50.0);
        var fired = 0;
        Action<Mobile, Skill, bool> count = (m, s, _) =>
        {
            if (m == pm && s == camping)
            {
                fired++;
            }
        };

        // Rolls held at 0.999: the gain roll fails, so all three attempts run.
        using var hook = new Hook(rate: true, roll: 0.999);
        SkillEvents.SkillUsed += count;

        try
        {
            pm.CheckSkill(SkillName.Camping, 0.0, 100.0);
            _out.WriteLine($"one Camping use with 3 attempts: SkillUsed fired {fired} time(s)");
            Assert.Equal(1, fired);
        }
        finally
        {
            SkillEvents.SkillUsed -= count;
            pm.Delete();
        }
    }

    // ---------------------------------------------------------------- G7

    [Theory]
    [InlineData(SkillName.Magery, 80.0, 120.0)]
    [InlineData(SkillName.Necromancy, 80.0, 120.0)]
    [InlineData(SkillName.Chivalry, 65.0, 115.0)]
    [InlineData(SkillName.Spellweaving, 67.5, 117.5)]
    [InlineData(SkillName.Mysticism, 70.5, 120.5)]
    public void G7_TheCastingSchoolsAreNotRows(SkillName school, double min, double max)
    {
        var pm = Player(new Point3D(2900, 2900, 0));

        try
        {
            Set(pm, school, 150.0);
            Assert.True(double.IsNaN(Chance(pm, school, true, null, min, max)));
        }
        finally
        {
            pm.Delete();
        }
    }
}
