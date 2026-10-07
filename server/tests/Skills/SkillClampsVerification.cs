// SkillClampsVerification.cs
//
// cc-P66 Part A (batch X, cc-P54 B.4/B.5, Chase's decision 9). Seven pinned effects read a player's skill with no upper
// bound, so a skill above OSI's top made them certain or immune. Patched to read the skill through
// customizations ClusterFSkillClamps, which counts a player's skill at most at OSI's top (the power-scroll cap, else 100).
// Each fact drives pinned's own code where a test can reach it, at today's numbers (red without the patches) and at OSI's
// top (the same with and without). Notes: shard-migration notes/cc-P66-clamps-and-hook-g.md, Part A.
//
// Facts:
//   X1. Stealing's caught test (Stealing.cs:337, caught = skill < Random(150)). A 127.5 thief with +22.5 worn reads 150 and
//       was never caught; now caught as at 120. At 100 and 120 as pinned.
//   X2. Hiding's combat range (Hiding.cs:53). A 150 hider with a combatant 5 tiles off hid (range -17); now refused as at
//       100 (range 8). 9 tiles off still hides.
//   X3. Detect Hidden's radius (DetectHidden.cs:145). DH 200 swept 20 tiles; now 10. 9 tiles is found either way.
//   X4. Musicianship's reduction (Peacemaking.cs:164-169, Provocation.cs:133-138, Discordance.cs:155-160). Each bard
//       skill's window at Musicianship 200 was 40 below the window at 100; now 10 below, as at 120.
//   X5. The block chance (BaseWeapon.cs:1528, :1569). Parry 200, Bushido 120, Evasion, a two-hander: 0.95; now 0.600036,
//       which is also exactly what Parry 120 / Bushido 120 gets, as pinned. A shield at Parry 200 (0.55) is unchanged.
//   X6. Tracking's radius (Tracking.cs:166) and Nether Cyclone's resist (NetherCycloneSpell.cs:81): the patched lines
//       read the clamp (the effects are not reachable from a test without a client or a cast), and the clamp's values.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Server;
using Server.Items;
using Server.Misc;
using Server.Mobiles;
using Server.Random;
using Server.SkillHandlers;
using Server.Spells.Bushido;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class SkillClampsVerification
{
    private readonly ITestOutputHelper _out;

    public SkillClampsVerification(ITestOutputHelper output)
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

    private static void SetSkill(Mobile m, SkillName name, double value)
    {
        m.Skills[name].Cap = 200.0;
        m.Skills[name].Base = value;
    }

    // ---------------------------------------------------------------- X1

    // One steal of a 5-stone item off a pack on the ground with the caught roll held at `roll`: was the thief caught?
    private static bool CaughtAt(double stealingBase, double worn, int roll)
    {
        var at = new Point3D(1620, 1620, 0);
        var thief = Player(at);
        SetSkill(thief, SkillName.Stealing, stealingBase);
        DefaultSkillMod mod = null;

        if (worn > 0)
        {
            // Built as an item builds its own (ObeyCap); the cc-P53 hook lets it read above the cap (D-113).
            mod = new DefaultSkillMod(SkillName.Stealing, "P66Worn", true, worn) { ObeyCap = true };
            thief.AddSkillMod(mod);
        }

        var pack = new Backpack();
        pack.MoveToWorld(at, Map.Trammel);
        var item = new Item(0x1BF2) { Weight = 5.0 };
        pack.DropItem(item);

        var handler = Mobile.SkillCheckTargetHandler;
        Mobile.SkillCheckTargetHandler = (_, _, _, _, _) => true;
        BuiltInRng.Generator = new ConstantRandom { Value = (roll + 0.5) / 150.0 };

        try
        {
            Assert.Equal(stealingBase + worn, thief.Skills.Stealing.Value);
            var type = typeof(Stealing).GetNestedType("StealingTarget", BindingFlags.NonPublic)!;
            var target = Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, [thief], null);
            var method = type.GetMethod("TryStealItem", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var args = new object[] { item, false };
            var stolen = (Item)method.Invoke(target, args);
            Assert.NotNull(stolen);
            return (bool)args[1];
        }
        finally
        {
            Mobile.SkillCheckTargetHandler = handler;
            BuiltInRng.Reset();
            if (mod != null)
            {
                thief.RemoveSkillMod(mod);
            }

            item.Delete();
            pack.Delete();
            thief.Delete();
        }
    }

    [Fact]
    public void X1_GearCannotMakeAThiefUncatchable()
    {
        // Rolls 0..149; caught when the skill read is below the roll.
        int[] rolls = [101, 110, 121, 125, 149];

        foreach (var roll in rolls)
        {
            var geared = CaughtAt(127.5, 22.5, roll);
            var at120 = CaughtAt(120.0, 0.0, roll);
            var at100 = CaughtAt(100.0, 0.0, roll);
            _out.WriteLine($"roll {roll}: 127.5 + 22.5 worn caught {geared}; 120 caught {at120}; 100 caught {at100}");

            // OSI's range, as pinned.
            Assert.Equal(100 < roll, at100);
            Assert.Equal(120 < roll, at120);
            // Above it: caught exactly as at 120 (pinned: 150 < roll, never).
            Assert.Equal(at120, geared);
        }
    }

    // ---------------------------------------------------------------- X2

    private bool HidesBesideACombatant(double hiding, int distance)
    {
        var at = new Point3D(1700, 1700, 0);
        var pm = Player(at);
        SetSkill(pm, SkillName.Hiding, hiding);
        var foe = new Mongbat();
        foe.MoveToWorld(new Point3D(at.X + distance, at.Y, at.Z), Map.Trammel);

        try
        {
            pm.Combatant = foe;
            Assert.True(foe.InLOS(pm), "the test needs line of sight to mean anything");
            Hiding.OnUse(pm);
            _out.WriteLine($"Hiding {hiding}, combatant {distance} tiles: hidden {pm.Hidden}");
            return pm.Hidden;
        }
        finally
        {
            foe.Delete();
            pm.Delete();
        }
    }

    [Fact]
    public void X2_AHiderPastOneHundredCannotHideBesideACombatant()
    {
        Assert.False(HidesBesideACombatant(100.0, 5)); // pinned at OSI's top: range 8
        Assert.False(HidesBesideACombatant(150.0, 5)); // pinned: range -17, hid
        Assert.True(HidesBesideACombatant(150.0, 9));  // outside 8 tiles: hides, as at 100
        Assert.True(HidesBesideACombatant(100.0, 9));
    }

    // ---------------------------------------------------------------- X3

    private bool SweepFinds(double detect, int distance)
    {
        var at = new Point3D(1800, 1800, 0);
        var src = Player(at);
        SetSkill(src, SkillName.DetectHidden, detect);
        var hider = Player(new Point3D(at.X + distance, at.Y, at.Z));
        hider.Hidden = true;

        try
        {
            var type = typeof(DetectHidden).GetNestedType("InternalTarget", BindingFlags.NonPublic)!;
            var target = Activator.CreateInstance(type, true)!;
            type.GetMethod("OnTarget", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, [src, src]);
            _out.WriteLine($"Detect Hidden {detect}, hider {distance} tiles: revealed {!hider.Hidden}");
            return !hider.Hidden;
        }
        finally
        {
            hider.Delete();
            src.Delete();
        }
    }

    [Fact]
    public void X3_TheDetectHiddenSweepStopsAtOsisTenTiles()
    {
        Assert.True(SweepFinds(100.0, 9));
        Assert.True(SweepFinds(200.0, 9));
        Assert.False(SweepFinds(100.0, 15)); // pinned at OSI's top: 10 tiles
        Assert.False(SweepFinds(200.0, 15)); // pinned: 20 tiles, found
    }

    // ---------------------------------------------------------------- X4

    public enum Bard
    {
        Peacemaking,
        Provocation,
        Discordance
    }

    // The window a bard skill asks for at a given Musicianship, against the same two mongbats every time (a creature's
    // bard difficulty comes from its rolled stats, so each new one differs).
    private (double Min, double Max) BardWindow(Bard bard, PlayerMobile pm, Lute lute, Mongbat one, Mongbat two, double music)
    {
        SetSkill(pm, SkillName.Musicianship, music);

        var windows = new List<(double, double)>();
        var location = Mobile.SkillCheckLocationHandler;
        var targetHandler = Mobile.SkillCheckTargetHandler;
        Mobile.SkillCheckLocationHandler = (_, _, _, _) => true;
        Mobile.SkillCheckTargetHandler = (_, _, _, min, max) =>
        {
            windows.Add((min, max));
            return false;
        };
        BuiltInRng.Generator = new ConstantRandom { Value = 0.5 };

        try
        {
            object target;
            object targeted = one;

            switch (bard)
            {
                case Bard.Peacemaking:
                    target = Activator.CreateInstance(
                        typeof(Peacemaking).GetNestedType("InternalTarget", BindingFlags.NonPublic)!,
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [pm, lute], null);
                    break;
                case Bard.Provocation:
                    target = Activator.CreateInstance(
                        typeof(Provocation).GetNestedType("InternalSecondTarget", BindingFlags.NonPublic)!,
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [pm, lute, one], null);
                    targeted = two;
                    break;
                default:
                    target = new Discordance.DiscordanceTarget(pm, lute);
                    break;
            }

            target!.GetType().GetMethod("OnTarget", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(target, [pm, targeted]);

            Assert.Single(windows);
            _out.WriteLine($"{bard} at Musicianship {music}: window {windows[0].Item1} to {windows[0].Item2}");
            return windows[0];
        }
        finally
        {
            Mobile.SkillCheckLocationHandler = location;
            Mobile.SkillCheckTargetHandler = targetHandler;
            BuiltInRng.Reset();
        }
    }

    [Theory]
    [InlineData(Bard.Peacemaking)]
    [InlineData(Bard.Provocation)]
    [InlineData(Bard.Discordance)]
    public void X4_MusicianshipCutsBardDifficultyByTenAtMost(Bard bard)
    {
        var at = new Point3D(1900, 1900, 0);
        var pm = Player(at);
        SetSkill(pm, SkillName.Peacemaking, 100.0);
        SetSkill(pm, SkillName.Provocation, 100.0);
        SetSkill(pm, SkillName.Discordance, 100.0);
        var lute = new Lute();
        pm.Backpack.DropItem(lute);
        var one = new Mongbat();
        one.MoveToWorld(new Point3D(at.X + 2, at.Y, at.Z), Map.Trammel);
        var two = new Mongbat();
        two.MoveToWorld(new Point3D(at.X + 3, at.Y, at.Z), Map.Trammel);

        try
        {
            var at100 = BardWindow(bard, pm, lute, one, two, 100.0);
            var at120 = BardWindow(bard, pm, lute, one, two, 120.0);
            var at150 = BardWindow(bard, pm, lute, one, two, 150.0);
            var at200 = BardWindow(bard, pm, lute, one, two, 200.0);

            // OSI's range, as pinned: 120 takes 10 off.
            Assert.Equal(at100.Min - 10.0, at120.Min, 6);
            Assert.Equal(at100.Max - 10.0, at120.Max, 6);
            // Past it, nothing more (pinned: 150 took 25 off, 200 took 50).
            Assert.Equal(at120.Min, at150.Min, 6);
            Assert.Equal(at120.Min, at200.Min, 6);
            Assert.Equal(at120.Max, at200.Max, 6);
        }
        finally
        {
            one.Delete();
            two.Delete();
            lute.Delete();
            pm.Delete();
        }
    }

    // ---------------------------------------------------------------- X5

    private double BlockChance(double parry, double bushido, bool evasion, bool shield)
    {
        var pm = Player(new Point3D(2000, 2000, 0));
        SetSkill(pm, SkillName.Parry, parry);
        SetSkill(pm, SkillName.Bushido, bushido);
        SetSkill(pm, SkillName.Anatomy, 100.0);
        SetSkill(pm, SkillName.Tactics, 100.0);
        // Put on directly, on the layer tile data would give it (the test host loads no tile data, so a new item has no
        // layer and cannot be equipped); the equip rules are not what this fact is about.
        Item held = shield ? new HeaterShield() : new Bardiche();
        held.Layer = Layer.TwoHanded;
        pm.AddItem(held);
        Assert.Same(held, shield ? pm.FindItemOnLayer(Layer.TwoHanded) : pm.Weapon);

        var chances = new List<double>();
        var handler = Mobile.SkillCheckDirectLocationHandler;
        Mobile.SkillCheckDirectLocationHandler = (_, _, chance) =>
        {
            chances.Add(chance);
            return false;
        };

        try
        {
            if (evasion)
            {
                Evasion.BeginEvasion(pm);
                Assert.True(Evasion.IsEvading(pm));
            }

            BaseWeapon.CheckParry(pm);
            Assert.Single(chances);
            _out.WriteLine(
                $"Parry {parry}, Bushido {bushido}, evasion {evasion}, shield {shield}: block chance {chances[0]:F6}"
            );
            return chances[0];
        }
        finally
        {
            Mobile.SkillCheckDirectLocationHandler = handler;
            Evasion.EndEvasion(pm);
            held.Delete();
            pm.Delete();
        }
    }

    [Fact]
    public void X5_TheBlockChanceStopsAtOsisHighest()
    {
        var osiTop = (120.0 * 120.0 / 41140.0 + 0.05) * 1.5;
        Assert.Equal(osiTop, ClusterFSkillClamps.OsiMaxBlockChance, 9);

        // OSI's own highest, as pinned.
        Assert.Equal(osiTop, BlockChance(120.0, 120.0, true, false), 9);
        // Parry 200 with Bushido 120 and Evasion: pinned 0.95; OSI's highest now.
        Assert.Equal(osiTop, BlockChance(200.0, 120.0, true, false), 9);
        // Below the top: as pinned (Parry 100, Bushido 100, no Evasion: 10000 / 41140 + 0.05).
        Assert.Equal(10000.0 / 41140.0 + 0.05, BlockChance(100.0, 100.0, false, false), 9);
        // A shield at Parry 200 never reached the top: unchanged (200 / 400 + 0.05).
        Assert.Equal(0.55, BlockChance(200.0, 0.0, false, true), 9);
    }

    // ---------------------------------------------------------------- X6

    private static string BuildTreeFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Projects", "UOContent")))
        {
            dir = dir.Parent;
        }

        Assert.True(dir != null, $"no Projects/UOContent above {AppContext.BaseDirectory}");
        return File.ReadAllText(Path.Combine(dir!.FullName, "Projects", "UOContent", relative));
    }

    [Fact]
    public void X6_TrackingAndNetherCycloneReadTheClamp()
    {
        Assert.Contains(
            "var range = 10 + (int)ClusterFSkillClamps.TrackingRangeSkill(from) / 10 * 10;",
            BuildTreeFile("Skills/Tracking/Tracking.cs")
        );
        Assert.Contains(
            "var resistedReduction = reduction - ClusterFSkillClamps.NetherCycloneResistSkill(m) / 800.0;",
            BuildTreeFile("Spells/Mysticism/NetherCycloneSpell.cs")
        );

        var pm = Player(new Point3D(2100, 2100, 0));
        var mongbat = new Mongbat();

        try
        {
            SetSkill(pm, SkillName.Tracking, 200.0);
            SetSkill(pm, SkillName.MagicResist, 200.0);
            Assert.Equal(100.0, ClusterFSkillClamps.TrackingRangeSkill(pm)); // 110 tiles
            Assert.Equal(120.0, ClusterFSkillClamps.NetherCycloneResistSkill(pm));
            // A 120/120 mystic's drain against it: 240 / 1200 - 120 / 800 = 0.05, never negative.
            Assert.True(240.0 / 1200.0 - ClusterFSkillClamps.NetherCycloneResistSkill(pm) / 800.0 >= 0.0);

            SetSkill(pm, SkillName.Tracking, 90.0);
            Assert.Equal(90.0, ClusterFSkillClamps.TrackingRangeSkill(pm));

            // Creatures as pinned.
            mongbat.Skills.MagicResist.Cap = 200.0;
            mongbat.Skills.MagicResist.Base = 150.0;
            Assert.Equal(150.0, ClusterFSkillClamps.NetherCycloneResistSkill(mongbat));
        }
        finally
        {
            mongbat.Delete();
            pm.Delete();
        }
    }
}
