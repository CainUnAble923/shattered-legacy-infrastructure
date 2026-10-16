// cc-P127: the Forgotten Treasures revamp (ServUO Services/TreasureMaps -> customizations/Services/TreasureMaps/
// TreasureMapInfo.cs, BagOfGems.cs; patches/TreasureMap.cs and TreasureMapChest.cs replacing pinned's two files; five
// patches into pinned Stealing, LockPick, Unlock, RemoveTrap and BaseCreature).
//
// Facts:
//   1. The revamp is on (Endless Journey) and a classic level becomes a revamp level: 1 Stash, 2-3 Supply, 4-5 Cache,
//      6 Hoard, 7 Trove, capped at Trove; a map carries a package.
//   2. A map knows its facet (Malas, Ter Mur, Eodon by rectangle) and the chest spot is inside the facet's area.
//   3. Decoding needs Cartography by level: a Stash at 100 always decodes (blessed after), a Trove at 50 never.
//   4. A dug chest is the revamp's: lock and trap by level, a quality, a bag of gems with the level's gold, the level's
//      count of skill scrolls and magic equipment, the map it came from, and four hued spawns of which the guardians
//      are listed.
//   5. The chest refuses to open while trapped or while an ancient chest guardian lives (the Remove Trap failure
//      raises one: named, paragon-buffed, hue 1960).
//   6. A failed lockpick marks the chest and the next pick brings a grubber that takes one item.
//   7. The map (package, reset time, level) and the chest (quality, failed pick, dig time, map, ancient guardians)
//      survive a serialization round trip.
//   8. The five pinned files carry their cc-P127 lines in the build tree (the patches applied).
//
// Host notes: no map files or tile data, so GetRandomLocation validates nothing and takes a point inside the facet's
// rectangle after its attempt cap; CanSpawnMobile is true everywhere at z 0 (the Shame facts' note). Spots are far from
// the other facts' and each fact deletes what it spawns.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Tests.Network;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class TreasureMapRevampVerification
{
    private readonly ITestOutputHelper _out;

    public TreasureMapRevampVerification(ITestOutputHelper output)
    {
        _out = output;
        ShardTestHost.EnsureAccounts();
        ShardTestHost.EnsureSkillChecks();
    }

    private static readonly Point3D Spot = new(2210, 3310, 0);

    private sealed class Digger : IDisposable
    {
        public readonly Account Account = new($"p127{Guid.NewGuid():N}"[..16], "p127-test-only");
        public readonly PlayerMobile Pm;
        public readonly NetState Ns;
        public readonly List<Mobile> Spawned = new();

        public Digger(double cartography, Point3D spot, Map map)
        {
            ShardTestClock.Arm();
            Pm = new PlayerMobile { Player = true, Race = Race.Human };
            Pm.AddItem(new Backpack());
            Pm.Skills[SkillName.Cartography].Cap = 200.0;
            Pm.Skills[SkillName.Cartography].Base = cartography;
            Account[0] = Pm;
            Pm.MoveToWorld(spot, map);

            Ns = PacketTestUtilities.CreateTestNetState();
            Ns.Account = Account;
            Pm.NetState = Ns;
            Ns.Mobile = Pm;
        }

        public void Dispose()
        {
            foreach (var item in Pm.Backpack.Items.ToList())
            {
                item.Delete();
            }

            Spawned.ForEach(m => m.Delete());
            Pm.NetState = null;
            Ns.Mobile = null;
            Pm.Delete();
            Ns.Dispose();
        }
    }

    private static TreasureMap MapAt(TreasureLevel level, Map map, Point3D spot)
    {
        var tm = new TreasureMap(level, map) { ChestLocation = new Point2D(spot.X, spot.Y) };
        tm.ChestMap = map;
        return tm;
    }

    /// <summary>A dig: the chest a shovel raises (temporary, unfilled), then the map's OnDug.</summary>
    private static TreasureMapChest Dig(Digger d, TreasureMap tm, Point3D spot)
    {
        var chest = new TreasureMapChest(d.Pm, tm.Level, true);
        chest.MoveToWorld(spot, tm.ChestMap);
        tm.OnDug(d.Pm, chest);

        foreach (var m in tm.ChestMap.GetMobilesInRange(spot, 8))
        {
            if (m is BaseCreature)
            {
                d.Spawned.Add(m);
            }
        }

        return chest;
    }

    [Fact]
    public void TheRevampIsOnAndAClassicLevelBecomesARevampLevel()
    {
        Assert.True(TreasureMapInfo.NewSystem, "Core.EJ: the revamp runs on this shard");

        Assert.Equal(-1, TreasureMapInfo.ConvertLevel(-1));
        Assert.Equal((int)TreasureLevel.Stash, TreasureMapInfo.ConvertLevel(0));
        Assert.Equal((int)TreasureLevel.Stash, TreasureMapInfo.ConvertLevel(1));
        Assert.Equal((int)TreasureLevel.Supply, TreasureMapInfo.ConvertLevel(2));
        Assert.Equal((int)TreasureLevel.Supply, TreasureMapInfo.ConvertLevel(3));
        Assert.Equal((int)TreasureLevel.Cache, TreasureMapInfo.ConvertLevel(4));
        Assert.Equal((int)TreasureLevel.Cache, TreasureMapInfo.ConvertLevel(5));
        Assert.Equal((int)TreasureLevel.Hoard, TreasureMapInfo.ConvertLevel(6));
        Assert.Equal((int)TreasureLevel.Trove, TreasureMapInfo.ConvertLevel(7));

        // Pinned's constructor, as every pinned caller uses it: a classic level in, a revamp level out.
        var five = new TreasureMap(5, Map.Trammel);
        var seven = new TreasureMap(7, Map.Felucca);
        var capped = new TreasureMap(TreasureLevel.Stash, Map.Trammel);

        try
        {
            Assert.Equal(2, five.Level);
            Assert.Equal(TreasureLevel.Cache, five.TreasureLevel);
            Assert.Equal(TreasureLevel.Trove, seven.TreasureLevel);
            Assert.InRange((int)five.Package, 0, 4);

            capped.Level = 9;
            Assert.Equal(4, capped.Level);

            Assert.True(five.NextReset > Core.Now + TimeSpan.FromDays(29), "a new map resets in 30 days");
            Assert.Same(Map.Trammel, five.ChestMap);
            Assert.Same(Map.Trammel, five.Facet);
            _out.WriteLine($"level 5 -> {five.TreasureLevel} {five.Package}, chest at {five.ChestLocation}, reset {five.NextReset:u}");
        }
        finally
        {
            five.Delete();
            seven.Delete();
            capped.Delete();
        }
    }

    [Fact]
    public void AMapKnowsItsFacetAndItsSpotIsInsideThatFacet()
    {
        var malas = new TreasureMap(TreasureLevel.Supply, Map.Malas);
        var terMur = new TreasureMap(TreasureLevel.Supply, Map.TerMur);
        var eodon = new TreasureMap(TreasureLevel.Supply, Map.TerMur, true);
        var tokuno = new TreasureMap(TreasureLevel.Supply, Map.Tokuno);

        try
        {
            Assert.Equal(TreasureFacet.Malas, malas.TreasureFacet);
            Assert.Equal(TreasureFacet.Tokuno, tokuno.TreasureFacet);
            Assert.Equal(TreasureFacet.TerMur, terMur.TreasureFacet);
            Assert.Equal(TreasureFacet.Eodon, eodon.TreasureFacet);

            // ServUO's Ter Mur rectangles all lie below y 2760; its Eodon rectangles inside IsEodon's box.
            Assert.True(terMur.ChestLocation.Y >= 2760, $"Ter Mur spot {terMur.ChestLocation}");
            Assert.True(TreasureMapInfo.IsEodon(Map.TerMur, new Point3D(eodon.ChestLocation, 0)), $"Eodon spot {eodon.ChestLocation}");
            Assert.InRange(malas.ChestLocation.X, 602, 2521);
            Assert.InRange(tokuno.ChestLocation.X, 95, 1427);

            Assert.Equal(1060851, TreasureMap.FacetLabel(malas.TreasureFacet));
            Assert.Equal(1158985, TreasureMap.FacetLabel(eodon.TreasureFacet));
            _out.WriteLine($"malas {malas.ChestLocation} termur {terMur.ChestLocation} eodon {eodon.ChestLocation} tokuno {tokuno.ChestLocation}");
        }
        finally
        {
            malas.Delete();
            terMur.Delete();
            eodon.Delete();
            tokuno.Delete();
        }
    }

    [Fact]
    public void DecodingNeedsCartographyByLevel()
    {
        using var low = new Digger(50.0, Spot, Map.Trammel);
        using var high = new Digger(100.0, new Point3D(Spot.X + 10, Spot.Y, 0), Map.Trammel);

        var stash = MapAt(TreasureLevel.Stash, Map.Trammel, Spot);
        var trove = MapAt(TreasureLevel.Trove, Map.Trammel, Spot);
        var troveForHigh = MapAt(TreasureLevel.Trove, Map.Trammel, Spot);

        try
        {
            Assert.Equal(0.0, stash.GetMinSkillLevel());
            Assert.Equal(91.0, trove.GetMinSkillLevel());

            // A Trove at 50: too difficult, and the roll (81, 121) cannot pass at 50.
            trove.Decode(low.Pm);
            Assert.Null(trove.Decoder);
            Assert.Equal(LootType.Regular, trove.LootType);

            // A Stash at 100: the roll (-10, 30) always passes; the map is the decoder's and blessed.
            stash.Decode(high.Pm);
            Assert.Same(high.Pm, stash.Decoder);
            Assert.Equal(LootType.Blessed, stash.LootType);

            // At 150 (Cap 200) a Trove decodes every time: (81, 121) is below the skill.
            high.Pm.Skills[SkillName.Cartography].Base = 150.0;
            troveForHigh.Decode(high.Pm);
            Assert.Same(high.Pm, troveForHigh.Decoder);
        }
        finally
        {
            stash.Delete();
            trove.Delete();
            troveForHigh.Delete();
        }
    }

    public static readonly TheoryData<TreasureLevel> Levels = new()
    {
        TreasureLevel.Stash, TreasureLevel.Supply, TreasureLevel.Cache, TreasureLevel.Hoard, TreasureLevel.Trove
    };

    [Theory]
    [MemberData(nameof(Levels))]
    public void ADugChestIsTheRevampsAndPaysByLevel(TreasureLevel level)
    {
        var spot = new Point3D(Spot.X + 40 + (int)level * 20, Spot.Y + 40, 0);
        using var d = new Digger(100.0, new Point3D(spot.X + 2, spot.Y, 0), Map.Trammel);
        var tm = MapAt(level, Map.Trammel, spot);
        TreasureMapChest chest = null;

        try
        {
            chest = Dig(d, tm, spot);

            Assert.False(chest.Temporary);
            Assert.Same(tm, chest.TreasureMap);
            Assert.True(tm.Completed);
            Assert.Same(d.Pm, tm.CompletedBy);
            Assert.True(chest.DigTime > DateTime.MinValue);
            Assert.NotEqual(ChestQuality.None, chest.ChestQuality);
            Assert.True(chest.Locked);
            Assert.Equal(TrapType.ExplosionTrap, chest.TrapType);

            var (req, power, trapLevel) = TreasureMapInfo.GetLockAndTrap(level);
            Assert.Equal(req, chest.RequiredSkill);
            Assert.Equal(power, chest.TrapPower);
            Assert.Equal(trapLevel, chest.TrapLevel);
            Assert.Equal(req - 10, chest.LockLevel);
            Assert.Equal(req + 40, chest.MaxLockLevel);

            var bag = chest.Items.OfType<BagOfGems>().Single();
            var gold = bag.Items.OfType<Gold>().Sum(g => g.Amount);
            Assert.InRange(gold, TreasureMapInfo.GetGoldMin(level), TreasureMapInfo.GetGoldMax(level));

            var gemStacks = bag.Items.Where(i => i is not Gold).ToList();
            Assert.Equal(Loot.GemTypes.Length, gemStacks.Count);
            var gemCount = gemStacks[0].Amount;
            Assert.All(gemStacks, g => Assert.Equal(gemCount, g.Amount));
            Assert.Contains(gemCount - (int)level * 5, new[] { 7, 9, 11 });

            var droppedMap = chest.Items.OfType<TreasureMap>().Count();
            Assert.InRange(droppedMap, 0, 1);
            var scrolls = chest.Items.Count(i => i is ScrollofTranscendence or ScrollofAlacrity or PowerScroll);
            Assert.Equal((int)level + 1 - droppedMap, scrolls);

            var expectedGear = TreasureMapInfo.GetEquipmentAmount(d.Pm, level, tm.Package);
            var gear = chest.Items.Count(i => i is BaseWeapon or BaseArmor or BaseJewel or BaseHat);
            Assert.InRange(gear, expectedGear, expectedGear + 1); // + one skull weapon from the special loot

            var spawned = d.Spawned.OfType<BaseCreature>().Where(m => m.Hue == 2725).ToList();
            Assert.Equal(4, spawned.Count);
            Assert.All(chest.Guardians ?? [], g => Assert.Equal("(Guardian)", ((BaseCreature)g).Title));
            Assert.InRange(chest.Guardians?.Count ?? 0, 0, 4);

            _out.WriteLine($"{level} {tm.Package} {chest.ChestQuality} (0x{chest.ItemID:X}): gold {gold}, gems {gemCount} x {gemStacks.Count}, " +
                           $"scrolls {scrolls}, gear {gear} of {expectedGear}, guardians {chest.Guardians?.Count ?? 0} of 4");
        }
        finally
        {
            chest?.Delete();
            tm.Delete();
        }
    }

    [Fact]
    public void TheChestRefusesToOpenWhileTrappedOrGuarded()
    {
        var spot = new Point3D(Spot.X + 200, Spot.Y, 0);
        using var d = new Digger(100.0, new Point3D(spot.X + 1, spot.Y, 0), Map.Trammel);
        var tm = MapAt(TreasureLevel.Supply, Map.Trammel, spot);
        TreasureMapChest chest = null;

        try
        {
            chest = Dig(d, tm, spot);
            chest.Locked = false;

            Assert.False(chest.CanOpen(d.Pm), "trapped: use Remove Trap first");
            Assert.True(chest.CheckLocked(d.Pm));

            chest.TrapType = TrapType.None;
            Assert.True(chest.CanOpen(d.Pm));
            Assert.False(chest.CheckLocked(d.Pm));

            chest.SpawnAncientGuardian(d.Pm);
            Assert.True(chest.HasLivingAncientGuardian());
            var guardian = chest.AncientGuardians.Single() as BaseCreature;
            Assert.NotNull(guardian);
            d.Spawned.Add(guardian);
            Assert.Equal("Ancient Chest Guardian", guardian.Name);
            Assert.Equal("(Guardian)", guardian.Title);
            Assert.Equal(1960, guardian.Hue);
            Assert.False(guardian.Tamable);
            Assert.False(chest.CanOpen(d.Pm), "an ancient guardian lives");

            // A second failure while one lives raises no second guardian.
            chest.SpawnAncientGuardian(d.Pm);
            Assert.Single(chest.AncientGuardians);

            guardian.Kill();
            Assert.False(chest.HasLivingAncientGuardian());
            Assert.True(chest.CanOpen(d.Pm));
            _out.WriteLine($"ancient guardian: {guardian.GetType().Name}, str {guardian.RawStr}, hits {guardian.HitsMax}");
        }
        finally
        {
            chest?.Delete();
            tm.Delete();
        }
    }

    [Fact]
    public void AFailedLockpickBringsAGrubberThatTakesOneItem()
    {
        var spot = new Point3D(Spot.X + 300, Spot.Y, 0);
        using var d = new Digger(100.0, new Point3D(spot.X + 1, spot.Y, 0), Map.Trammel);
        var tm = MapAt(TreasureLevel.Cache, Map.Trammel, spot);
        TreasureMapChest chest = null;

        try
        {
            chest = Dig(d, tm, spot);
            var before = chest.Items.Count;
            Assert.True(before > 0);

            chest.FailedLockpick = true;
            chest.LockPick(d.Pm);

            Assert.False(chest.Locked);
            Grubber grubber = null;

            foreach (var m in Map.Trammel.GetMobilesInRange(spot, 3))
            {
                if (m is Grubber g)
                {
                    grubber = g;
                    break;
                }
            }

            Assert.NotNull(grubber);
            d.Spawned.Add(grubber);
            Assert.Equal(before - 1, chest.Items.Count);
            Assert.NotNull(grubber.Backpack);
            Assert.NotEmpty(grubber.Backpack.Items);
            _out.WriteLine($"grubber took {grubber.Backpack.Items[0].GetType().Name}");
        }
        finally
        {
            chest?.Delete();
            tm.Delete();
        }
    }

    [Fact]
    public void AMapAndAChestSurviveASerializationRoundTrip()
    {
        var tm = new TreasureMap(TreasureLevel.Hoard, Map.Malas) { Package = TreasurePackage.Ranger };
        var guardian = new Skeleton();
        var chest = new TreasureMapChest(null, 3, true)
        {
            FailedLockpick = true,
            ChestQuality = ChestQuality.Gold,
            DigTime = new DateTime(2026, 10, 16, 6, 0, 0, DateTimeKind.Utc),
            FirstOpenedByOwner = true,
            TreasureMap = tm
        };
        chest.AddAncientGuardian(guardian);

        var mapCopy = new TreasureMap(tm.Serial);
        var chestCopy = new TreasureMapChest(chest.Serial);

        try
        {
            var buffer = new byte[65536];
            var writer = new BufferWriter(buffer, true);
            tm.Serialize(writer);
            writer.Flush();
            mapCopy.Deserialize(new BufferReader(buffer));

            Assert.Equal(3, mapCopy.Level);
            Assert.Equal(TreasurePackage.Ranger, mapCopy.Package);
            Assert.Equal(tm.ChestLocation, mapCopy.ChestLocation);
            Assert.Same(Map.Malas, mapCopy.ChestMap);
            Assert.Equal(tm.NextReset, mapCopy.NextReset);

            var buffer2 = new byte[65536];
            var writer2 = new BufferWriter(buffer2, true);
            chest.Serialize(writer2);
            writer2.Flush();
            chestCopy.Deserialize(new BufferReader(buffer2));

            Assert.True(chestCopy.FailedLockpick);
            Assert.Equal(ChestQuality.Gold, chestCopy.ChestQuality);
            Assert.Equal(0xA308, chestCopy.ItemID);
            Assert.Equal(chest.DigTime, chestCopy.DigTime);
            Assert.True(chestCopy.FirstOpenedByOwner);
            Assert.Same(tm, chestCopy.TreasureMap);
            Assert.Contains(guardian, chestCopy.AncientGuardians);
            Assert.Equal(3, chestCopy.Level);
        }
        finally
        {
            guardian.Delete();
            tm.Delete();
            mapCopy.Delete();
            chest.Delete();
            chestCopy.Delete();
        }
    }

    [Fact]
    public void TheFivePinnedFilesCarryTheirPatchedLinesInTheBuildTree()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Projects", "UOContent")))
        {
            dir = dir.Parent;
        }

        Assert.True(dir != null, $"no Projects/UOContent above {AppContext.BaseDirectory}");
        var root = Path.Combine(dir.FullName, "Projects", "UOContent");

        var expected = new (string File, string Line)[]
        {
            ("Skills/Stealing.cs", "StealingHandler.HandleSteal(bc, thief)"),
            ("Items/Skill Items/Thief/LockPick.cs", "chest.FailedLockpick = true"),
            ("Spells/Third/Unlock.cs", "reqSkill = tChest.Level switch"),
            ("Skills/RemoveTrap.cs", "StartChestDisarmTimer(from, tChest)"),
            ("Mobiles/BaseCreature.cs", "TreasureMapInfo.IsEodon(Map, Location)")
        };

        foreach (var (file, line) in expected)
        {
            var text = File.ReadAllText(Path.Combine(root, file));
            Assert.True(text.Contains(line, StringComparison.Ordinal), $"{file} lacks '{line}': its cc-P127 patch did not apply");
        }
    }
}
