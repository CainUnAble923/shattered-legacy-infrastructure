// TamingAngerVerification.cs
//
// cc-P171 (Chase 2026-10-18: 30 tries at 200 Animal Taming on a Dread Warhorse, MinTameSkill 108, every one "You seem to
// anger the beast!"). Pinned AnimalTaming.cs:208 angered a CanAngerOnTame creature 95% of the time at any skill. Now
// (ClusterFTamingAnger, AnimalTaming-anger-scales.patch): max(floor, base - perPoint x (Animal Taming - MinTameSkill)),
// never above base; defaults 0.95, 0.015, 0.15, settings clusterf.taming.angerBase / angerPerPoint / angerFloor.
//
// Facts:
//   1. With the defaults, against a Dread Warhorse (108): 95% at 108, 77% at 120 (+12), 35% at 148 (+40), 17% at 160,
//      the 15% floor from 161.3 (162 and 200); below the minimum, still 95%.
//   2. The three settings are read (Configure) and change the chance.
//   3. A creature without CanAngerOnTame never angers: chance 0, and the real taming target always starts the taming,
//      even with the settings at 100%.
//   4. Through the real taming target (AnimalTaming's InternalTarget.OnTarget, so the patched line itself), a 200 tamer
//      on a Dread Warhorse angers it about 15% of 400 tries, not 95%.
//   5. The table: every tamable CanAngerOnTame creature type on the shard, its MinTameSkill and the chance at 108, 120,
//      150 and 200 (printed; the notes carry it).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Server;
using Server.Mobiles;
using Server.SkillHandlers;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class TamingAngerVerification
{
    private const string BaseKey = "clusterf.taming.angerBase";
    private const string PerPointKey = "clusterf.taming.angerPerPoint";
    private const string FloorKey = "clusterf.taming.angerFloor";

    private readonly ITestOutputHelper _out;

    public TamingAngerVerification(ITestOutputHelper output) => _out = output;

    private static void UseSettings(double b, double perPoint, double floor)
    {
        ServerConfiguration.SetSetting(BaseKey, b.ToString(System.Globalization.CultureInfo.InvariantCulture));
        ServerConfiguration.SetSetting(PerPointKey, perPoint.ToString(System.Globalization.CultureInfo.InvariantCulture));
        ServerConfiguration.SetSetting(FloorKey, floor.ToString(System.Globalization.CultureInfo.InvariantCulture));
        ClusterFTamingAnger.Configure();
    }

    private static void UseDefaults() =>
        UseSettings(ClusterFTamingAnger.DefaultBase, ClusterFTamingAnger.DefaultPerPoint, ClusterFTamingAnger.DefaultFloor);

    private static PlayerMobile Tamer(double skill)
    {
        var pm = new PlayerMobile { Player = true };
        pm.Skills.AnimalTaming.Cap = 200.0;
        pm.Skills.AnimalTaming.Base = skill;
        pm.MoveToWorld(new Point3D(1500, 1500, 0), Map.Trammel);
        return pm;
    }

    private static T Wild<T>() where T : BaseCreature, new()
    {
        var bc = new T();
        bc.MoveToWorld(new Point3D(1501, 1500, 0), Map.Trammel);
        return bc;
    }

    // ---------------------------------------------------------------- 1

    [Theory]
    [InlineData(108.0, 0.95)]
    [InlineData(120.0, 0.77)]
    [InlineData(148.0, 0.35)]
    [InlineData(160.0, 0.17)]
    [InlineData(162.0, 0.15)]
    [InlineData(200.0, 0.15)]
    [InlineData(100.0, 0.95)]
    public void TheChanceFallsWithSkillToTheFloor(double skill, double expected)
    {
        UseDefaults();
        var pm = Tamer(skill);
        var horse = Wild<DreadWarhorse>();
        try
        {
            Assert.True(horse.CanAngerOnTame);
            Assert.Equal(108.0, horse.MinTameSkill);
            Assert.Equal(expected, ClusterFTamingAnger.Chance(pm, horse), 6);
        }
        finally
        {
            horse.Delete();
            pm.Delete();
        }
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void TheThreeSettingsAreRead()
    {
        var pm = Tamer(118.0);
        var horse = Wild<DreadWarhorse>();
        try
        {
            UseSettings(0.5, 0.01, 0.3);
            Assert.Equal(0.5, ClusterFTamingAnger.Base, 6);
            Assert.Equal(0.01, ClusterFTamingAnger.PerPoint, 6);
            Assert.Equal(0.3, ClusterFTamingAnger.Floor, 6);
            Assert.Equal(0.4, ClusterFTamingAnger.Chance(pm, horse), 6); // 0.5 - 0.01 x 10

            UseSettings(0.5, 0.05, 0.3);
            Assert.Equal(0.3, ClusterFTamingAnger.Chance(pm, horse), 6); // the floor
        }
        finally
        {
            UseDefaults();
            horse.Delete();
            pm.Delete();
        }
    }

    // ---------------------------------------------------------------- the real taming target

    private static readonly Type TargetType =
        typeof(AnimalTaming).GetNestedType("InternalTarget", BindingFlags.NonPublic)!;

    private static readonly MethodInfo OnTarget =
        TargetType.GetMethod("OnTarget", BindingFlags.NonPublic | BindingFlags.Instance)!;

    private static HashSet<Mobile> BeingTamed =>
        (HashSet<Mobile>)typeof(AnimalTaming).GetField("m_BeingTamed", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null)!;

    /// <summary>Targets the creature with AnimalTaming's own target; true when it angered (the taming did not start).</summary>
    private static bool TryOnce(PlayerMobile pm, BaseCreature creature)
    {
        creature.Combatant = null;
        BeingTamed.Remove(creature);
        var target = Activator.CreateInstance(TargetType, true);
        OnTarget.Invoke(target, [pm, creature]);
        var started = BeingTamed.Remove(creature);
        return !started;
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void ACreatureThatCannotAngerNeverAngers()
    {
        var pm = Tamer(200.0);
        var horse = Wild<Horse>();
        try
        {
            Assert.False(horse.CanAngerOnTame);
            UseSettings(1.0, 0.0, 1.0);
            Assert.Equal(0.0, ClusterFTamingAnger.Chance(pm, horse));
            for (var i = 0; i < 200; i++)
            {
                Assert.False(ClusterFTamingAnger.Angers(pm, horse));
                Assert.False(TryOnce(pm, horse), "the horse was angered");
            }
        }
        finally
        {
            UseDefaults();
            horse.Delete();
            pm.Delete();
        }
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void ATwoHundredTamerAngersADreadWarhorseAboutFifteenPercent()
    {
        UseDefaults();
        var pm = Tamer(200.0);
        var horse = Wild<DreadWarhorse>();
        try
        {
            const int tries = 400;
            var angered = 0;
            for (var i = 0; i < tries; i++)
            {
                angered += TryOnce(pm, horse) ? 1 : 0;
            }

            _out.WriteLine($"200 vs Dread Warhorse: angered {angered} of {tries}");
            Assert.InRange(angered, 30, 95); // 15% is 60; pinned's 95% is 380
        }
        finally
        {
            horse.Delete();
            pm.Delete();
        }
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void TheTableForEveryCreatureThatCanAnger()
    {
        UseDefaults();
        var rows = new List<(string Name, double Min, string Chances)>();
        var types = AssemblyHandler.Assemblies.Append(Core.Assembly).Where(a => a != null).Distinct()
            .SelectMany(a => a.GetTypes())
            .Where(t => typeof(BaseCreature).IsAssignableFrom(t) && !t.IsAbstract && t.GetConstructor(Type.EmptyTypes) != null)
            .OrderBy(t => t.Name);

        foreach (var t in types)
        {
            BaseCreature bc;
            try
            {
                bc = (BaseCreature)Activator.CreateInstance(t)!;
            }
            catch
            {
                continue;
            }

            try
            {
                if (bc.Tamable && bc.CanAngerOnTame)
                {
                    var chances = string.Join(" | ", new[] { 108.0, 120.0, 150.0, 200.0 }
                        .Select(s => $"{ClusterFTamingAnger.Chance(s, bc.MinTameSkill) * 100:0}%"));
                    rows.Add((t.Name, bc.MinTameSkill, chances));
                }
            }
            finally
            {
                bc.Delete();
            }
        }

        _out.WriteLine("| Creature | MinTameSkill | 108 | 120 | 150 | 200 |");
        _out.WriteLine("|---|---|---|---|---|---|");
        foreach (var (name, min, chances) in rows)
        {
            _out.WriteLine($"| {name} | {min:0.0} | {chances} |");
        }

        _out.WriteLine($"{rows.Count} creature types");
        Assert.Contains(rows, r => r.Name == nameof(DreadWarhorse));
    }
}
