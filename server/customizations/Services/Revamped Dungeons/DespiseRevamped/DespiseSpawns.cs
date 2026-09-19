// ServUO: RevampedSpawns/DespiseRevamped.xml (CC4 Despise) - the 47 XmlSpawner definitions the Read Me has a
// GM load with [XmlLoad spawns/despiserevamped.xml - carried as data, plus the one spawner subclass the data needs.
//
// XmlSpawner -> ModernUO Spawner, field by field (the XML is 1,975 lines; parsed by script, 47 rows, every
// value below is the XML's):
//   <Name>                         Name. The controller finds "DespiseRevamped Good #n"/"Evil #n" by name.
//   <CentreX/Y/Z>                  Location.
//   <X> <Y> <Width> <Height>       SpawnBounds, the rectangle spawns land in, at full Z range like ModernUO's
//                                  own legacy-format import (BaseSpawner.cs:323-341).
//   <Range>5                       WalkingRange 5, which Spawn() writes to the creature's RangeHome.
//   <IsHomeRangeRelative>True      SpawnLocationIsHome: the creature's home is where it spawned.
//   <MaxCount>                     Count.  <MinDelay>/<MaxDelay> with <DelayInSec>False: minutes.
//   <Objects2>  Type,{RND,a,b}:MX=n One entry per type, probability 100, maxCount n. {RND,a,b} was a random
//                                  constructor argument (the power level). ModernUO entries take fixed
//                                  parameters only (BaseSpawner.cs:1151-1179, Add.ParseValues), so the roll is
//                                  PowerMin/PowerMax on DespiseSpawner and DespiseCreature.OnAfterSpawn applies it.
//   <SmartSpawning>True            No counterpart: XmlSpawner idles while no player is near. Paper-only.
//   Everything else                XmlSpawner defaults (no triggers, no sequential spawning, no despawn).
//
// THE TWO-ARGUMENT ENTRIES. The 30 lower-level spawners read "Phantom,{RND,4,8},{RND,1,5}". XmlSpawner
// matches a constructor by argument COUNT (XmlSpawner2.cs:11283-11340, via ParseObjectArgs at
// BaseXmlSpawner.cs:7473) and the Despise creatures have only () and (int), so on pub57 as shipped those 30
// spawners construct nothing and report "invalid type specification". Read, not run (Q-044): the shard cannot
// build ServUO. The first argument, 4-8, is the evident intent - stronger creatures on the battlefield, and
// WispOrb.MinPowerToConscript is 4 - so the lower level rolls 4-8 here and the upper levels 1-5 (D-35).
//
// Rosters: the good side's eight and the evil side's eight, in the XML's order. "BirlingBlades" and
// "Sagittarri" are the corrected spellings ServUO's CheckSpawnersVersion3 rewrote old saves to.

using System;
using System.Collections.Generic;
using System.Linq;
using ModernUO.Serialization;
using Server.Engines.Spawners;

namespace Server.Engines.Despise;

[SerializationGenerator(0, false)]
public partial class DespiseSpawner : Spawner
{
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private int _powerMin;

    [SerializableField(1)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private int _powerMax;

    [Constructible(AccessLevel.Developer)]
    public DespiseSpawner()
    {
        _powerMin = 1;
        _powerMax = 5;
    }

    public DespiseSpawner(
        int amount, TimeSpan minDelay, TimeSpan maxDelay, Rectangle3D spawnBounds, int powerMin, int powerMax
    ) : base(amount, minDelay, maxDelay, 0, spawnBounds)
    {
        _powerMin = powerMin;
        _powerMax = powerMax;
    }

    public override string DefaultName => "Despise Spawner";

    public int RollPower() => Utility.RandomMinMax(_powerMin, _powerMax);
}

public static class DespiseSpawns
{
    public static readonly string[] GoodRoster =
    {
        "Silenii", "ForestNymph", "DespiseUnicorn", "Sagittarri", "Ursadane", "DivineGuardian", "Dendrite", "Fairy"
    };

    public static readonly string[] EvilRoster =
    {
        "Phantom", "Naba", "Darkmane", "Skeletrex", "Hellion", "Echidnite", "BirlingBlades", "Prometheoid"
    };

    public readonly record struct Definition(
        string Name,
        int CentreX, int CentreY, int CentreZ,
        int X, int Y, int Width, int Height,
        int MaxCount, int MinDelayMinutes, int MaxDelayMinutes,
        int PowerMin, int PowerMax,
        int EntryMaxCount,
        string[] Roster
    )
    {
        public bool IsLowerLevel => PowerMin == 4;
    }

    // Generated from RevampedSpawns/DespiseRevamped.xml; columns as in Definition.
    public static readonly Definition[] Definitions =
    {
        new("DespiseRevamped #", 5476, 739, 5, 5464, 727, 24, 24, 3, 2, 3, 1, 5, 1, EvilRoster),
        new("DespiseRevamped #1", 5486, 566, 60, 5471, 551, 30, 30, 3, 2, 3, 1, 5, 1, GoodRoster),
        new("DespiseRevamped #2", 5501, 531, 60, 5486, 516, 30, 30, 3, 2, 3, 1, 5, 1, GoodRoster),
        new("DespiseRevamped #3", 5459, 547, 60, 5441, 529, 36, 36, 3, 2, 3, 1, 5, 1, GoodRoster),
        new("DespiseRevamped #4", 5421, 546, 60, 5384, 521, 54, 35, 9, 2, 3, 1, 5, 2, GoodRoster),
        new("DespiseRevamped #5", 5403, 586, 45, 5388, 571, 30, 30, 3, 2, 3, 1, 5, 1, GoodRoster),
        new("DespiseRevamped #6", 5395, 623, 30, 5385, 601, 26, 28, 5, 2, 3, 1, 5, 1, GoodRoster),
        new("DespiseRevamped #7", 5444, 612, 45, 5426, 594, 36, 36, 5, 2, 3, 1, 5, 1, GoodRoster),
        new("DespiseRevamped #8", 5490, 609, 45, 5475, 594, 30, 30, 3, 2, 3, 1, 5, 1, GoodRoster),
        new("DespiseRevamped #10", 5441, 677, 20, 5426, 662, 30, 30, 5, 2, 3, 1, 5, 1, EvilRoster),
        new("DespiseRevamped #11", 5405, 691, 20, 5382, 656, 38, 49, 7, 2, 3, 1, 5, 1, EvilRoster),
        new("DespiseRevamped #11", 5450, 715, 15, 5438, 703, 24, 24, 3, 2, 3, 1, 5, 1, EvilRoster),
        new("DespiseRevamped #12", 5420, 733, 5, 5408, 721, 24, 24, 3, 2, 3, 1, 5, 1, EvilRoster),
        new("DespiseRevamped #12", 5437, 748, 5, 5425, 736, 24, 24, 3, 2, 3, 1, 5, 1, EvilRoster),
        new("DespiseRevamped #13", 5392, 753, 5, 5382, 743, 30, 30, 3, 2, 3, 1, 5, 1, EvilRoster),
        new("DespiseRevamped #13", 5495, 748, 5, 5488, 704, 25, 55, 9, 2, 3, 1, 5, 1, EvilRoster),
        new("DespiseRevamped #14", 5494, 673, 20, 5472, 651, 44, 44, 9, 2, 3, 1, 5, 1, EvilRoster),
        new("DespiseRevamped Good #1", 5400, 787, 65, 5395, 782, 30, 30, 5, 5, 10, 4, 8, 1, GoodRoster),
        new("DespiseRevamped Good #2", 5455, 787, 60, 5450, 782, 30, 30, 5, 5, 10, 4, 8, 1, GoodRoster),
        new("DespiseRevamped Good #3", 5396, 821, 60, 5391, 816, 30, 30, 5, 5, 10, 4, 8, 1, GoodRoster),
        new("DespiseRevamped Good #4", 5459, 823, 60, 5454, 818, 30, 30, 5, 5, 10, 4, 8, 1, GoodRoster),
        new("DespiseRevamped Good #5", 5436, 855, 45, 5431, 850, 30, 30, 5, 5, 10, 4, 8, 1, GoodRoster),
        new("DespiseRevamped Good #6", 5428, 909, 30, 5423, 904, 30, 30, 5, 5, 10, 4, 8, 1, GoodRoster),
        new("DespiseRevamped Good #7", 5395, 973, 6, 5390, 968, 30, 30, 5, 5, 10, 4, 8, 1, GoodRoster),
        new("DespiseRevamped Good #8", 5471, 972, 15, 5466, 967, 30, 30, 5, 5, 10, 4, 8, 1, GoodRoster),
        new("DespiseRevamped Good #9", 5493, 923, 20, 5488, 918, 30, 30, 5, 5, 10, 4, 8, 1, GoodRoster),
        new("DespiseRevamped Good #10", 5548, 899, 30, 5543, 894, 30, 30, 5, 5, 10, 4, 8, 1, GoodRoster),
        new("DespiseRevamped Good #11", 5576, 872, 45, 5571, 867, 30, 30, 5, 5, 10, 4, 8, 1, GoodRoster),
        new("DespiseRevamped Good #12", 5605, 824, 60, 5600, 819, 30, 30, 5, 5, 10, 4, 8, 1, GoodRoster),
        new("DespiseRevamped Good #13", 5603, 789, 60, 5598, 784, 30, 30, 5, 5, 10, 4, 8, 1, GoodRoster),
        new("DespiseRevamped Good #14", 5558, 791, 63, 5553, 786, 30, 30, 5, 5, 10, 4, 8, 1, GoodRoster),
        new("DespiseRevamped Good #15", 5520, 817, 60, 5515, 812, 30, 30, 5, 5, 10, 4, 8, 1, GoodRoster),
        new("DespiseRevamped Evil #1", 5400, 787, 66, 5395, 782, 30, 30, 5, 5, 10, 4, 8, 1, EvilRoster),
        new("DespiseRevamped Evil #2", 5455, 787, 61, 5450, 782, 30, 30, 5, 5, 10, 4, 8, 1, EvilRoster),
        new("DespiseRevamped Evil #3", 5396, 821, 61, 5391, 816, 30, 30, 5, 5, 10, 4, 8, 1, EvilRoster),
        new("DespiseRevamped Evil #4", 5459, 823, 61, 5454, 818, 30, 30, 5, 5, 10, 4, 8, 1, EvilRoster),
        new("DespiseRevamped Evil #5", 5436, 855, 46, 5431, 850, 30, 30, 5, 5, 10, 4, 8, 1, EvilRoster),
        new("DespiseRevamped Evil #6", 5428, 909, 31, 5423, 904, 30, 30, 5, 5, 10, 4, 8, 1, EvilRoster),
        new("DespiseRevamped Evil #7", 5395, 973, 7, 5390, 968, 30, 30, 5, 5, 10, 4, 8, 1, EvilRoster),
        new("DespiseRevamped Evil #8", 5471, 972, 16, 5466, 967, 30, 30, 5, 5, 10, 4, 8, 1, EvilRoster),
        new("DespiseRevamped Evil #9", 5493, 923, 21, 5488, 918, 30, 30, 5, 5, 10, 4, 8, 1, EvilRoster),
        new("DespiseRevamped Evil #10", 5548, 899, 31, 5543, 894, 30, 30, 5, 5, 10, 4, 8, 1, EvilRoster),
        new("DespiseRevamped Evil #11", 5576, 872, 46, 5571, 867, 30, 30, 5, 5, 10, 4, 8, 1, EvilRoster),
        new("DespiseRevamped Evil #12", 5605, 824, 61, 5600, 819, 30, 30, 5, 5, 10, 4, 8, 1, EvilRoster),
        new("DespiseRevamped Evil #13", 5603, 789, 61, 5598, 784, 30, 30, 5, 5, 10, 4, 8, 1, EvilRoster),
        new("DespiseRevamped Evil #14", 5558, 791, 64, 5553, 786, 30, 30, 5, 5, 10, 4, 8, 1, EvilRoster),
        new("DespiseRevamped Evil #15", 5520, 817, 61, 5515, 812, 30, 30, 5, 5, 10, 4, 8, 1, EvilRoster)
    };

    public static bool AnyPresent() => World.Items.Values.OfType<DespiseSpawner>().Any(s => !s.Deleted);

    /// <summary>
    ///     Creates the 47 spawners on Trammel, tags them for [DeleteDespise, and fills them, as
    ///     [GenerateSpawners does for ModernUO's own spawn files (ImportSpawnersCommand.cs:168-177).
    /// </summary>
    public static List<DespiseSpawner> Generate()
    {
        var list = new List<DespiseSpawner>(Definitions.Length);

        foreach (var d in Definitions)
        {
            var spawner = new DespiseSpawner(
                d.MaxCount,
                TimeSpan.FromMinutes(d.MinDelayMinutes),
                TimeSpan.FromMinutes(d.MaxDelayMinutes),
                new Rectangle3D(d.X, d.Y, Region.MinZ, d.Width, d.Height, Region.MaxZ - Region.MinZ),
                d.PowerMin,
                d.PowerMax
            )
            {
                Name = d.Name,
                WalkingRange = 5,
                SpawnLocationIsHome = true
            };

            foreach (var type in d.Roster)
            {
                spawner.AddEntry(type, 100, d.EntryMaxCount, false);
            }

            WeakEntityCollection.Add(DespiseRevampedSetup.CollectionKey, spawner);
            spawner.MoveToWorld(new Point3D(d.CentreX, d.CentreY, d.CentreZ), Map.Trammel);
            spawner.Respawn();

            list.Add(spawner);
        }

        return list;
    }
}
