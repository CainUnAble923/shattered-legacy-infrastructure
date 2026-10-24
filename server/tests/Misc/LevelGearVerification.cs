// LevelGearVerification.cs
//
// cc-P261: levelable crafted gear, phase 1 (customizations LevelGear/; notes: shard-migration
// notes/cc-P261-levelable-gear-build.md; research notes/cc-P72-level-items-research.md).
//
// Facts:
//   1. Crafted only: found gear, an artifact (crafted or not), clothing and staff-made gear are refused; a crafted weapon,
//      armour piece, shield and jewel are made levelable, once.
//   2. The curve: total XP to reach level n is ((n + 1) x 10)^2 - 100 (0, 300, ..., 4,040,000 at 200), and no item passes
//      the max level; 4 points a level and 8 on every 10th, every level of a multi-level jump paid.
//   3. The per-kill cap: one award gives an item at most the next level's total XP / 20, after the split.
//   4. The split: a creature's pool is shared by damage, a pet's damage counting as its master's; every equipped
//      levelable item of each credited player gets that player's share, the pet's own gear gets its master's share, and
//      a backpack item gets nothing. Through the real death event, the pool is the creature's Bard difficulty. A pet's
//      or summon's death pays nothing.
//   5. Spending: +1 costs the attribute's price; a property stops at its ceiling (the imbuing Full value, less the
//      item's own value), x1.5 on an exceptional item up to the Overcharged maximum; a property for another item kind
//      and a new property past the slot limit are refused; too few points is refused.
//   6. Applying: the bought bonus counts where the stock value is read (AosAttributes.GetValue, the wearer's
//      resistance) only while the item is worn, for a player and for a creature wearing it.
//   7. Save and load: every row round-trips through the store's own save; a deleted item's row is dropped.

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Items;
using Server.Mobiles;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class LevelGearVerification : IDisposable
{
    private static readonly Point3D Spot = new(1480, 1750, 0);

    private readonly ITestOutputHelper _out;
    private readonly List<IEntity> _things = [];

    public LevelGearVerification(ITestOutputHelper output)
    {
        _out = output;
        LevelGearStore.Rows.Clear();
    }

    public void Dispose()
    {
        foreach (var e in _things)
        {
            e.Delete();
        }

        LevelGearStore.Rows.Clear();
    }

    private T Track<T>(T e) where T : IEntity
    {
        _things.Add(e);
        return e;
    }

    private PlayerMobile Player()
    {
        var pm = Track(new PlayerMobile { Player = true, Race = Race.Human, Name = "P261 tester" });
        pm.AddItem(new Backpack());
        pm.MoveToWorld(Spot, Map.Trammel);
        return pm;
    }

    private T Crafted<T>(T item) where T : Item
    {
        item.PlayerConstructed = true;
        return Track(item);
    }

    private T Levelable<T>(T item, int level = 0, int points = 0) where T : Item
    {
        Crafted(item);
        Assert.Null(ClusterFLevelGear.MakeLevelable(item, null));
        var s = LevelGearStore.Of(item)!;
        s.Level = level;
        s.Xp = ClusterFLevelGear.XpForLevel(level);
        s.Points = points;
        return item;
    }

    // ---------------------------------------------------------------- 1

    [Fact]
    public void OnlyCraftedWeaponsArmourShieldsAndJewelsCanBeMadeLevelable()
    {
        var found = Track(new Longsword());
        Assert.Contains("crafted", ClusterFLevelGear.MakeLevelable(found, null));

        var artifact = Crafted(new TheDragonSlayer());
        Assert.Contains("Artifacts", ClusterFLevelGear.MakeLevelable(artifact, null));

        var cloth = Crafted(new Cloak());
        Assert.Contains("weapons, armor", ClusterFLevelGear.MakeLevelable(cloth, null));

        foreach (var item in new Item[] { Crafted(new Longsword()), Crafted(new PlateChest()), Crafted(new HeaterShield()), Crafted(new GoldRing()) })
        {
            Assert.Null(ClusterFLevelGear.MakeLevelable(item, null));
            Assert.True(ClusterFLevelGear.IsLevelable(item));
            Assert.Equal(200, LevelGearStore.Of(item)!.MaxLevel);
            Assert.Contains("already", ClusterFLevelGear.MakeLevelable(item, null));
        }

        Assert.False(ClusterFLevelGear.IsLevelable(found));
        Assert.False(ClusterFLevelGear.IsLevelable(artifact));
        Assert.False(ClusterFLevelGear.IsLevelable(cloth));

        var seal = Track(new SmithsSealDeed());
        Assert.Equal(LootType.Blessed, seal.LootType);
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void TheCurveIsXmlLevelItemsAndEveryLevelPaysItsPoints()
    {
        Assert.Equal(0, ClusterFLevelGear.XpForLevel(0));
        Assert.Equal(300, ClusterFLevelGear.XpForLevel(1));
        Assert.Equal(800, ClusterFLevelGear.XpForLevel(2));
        Assert.Equal(12_000, ClusterFLevelGear.XpForLevel(10));
        Assert.Equal(4_040_000, ClusterFLevelGear.XpForLevel(200));

        Assert.Equal(4, ClusterFLevelGear.PointsForReaching(9));
        Assert.Equal(8, ClusterFLevelGear.PointsForReaching(10));

        // From level 8 to 11 in one award (the per-kill cap off, so the jump is not cut short).
        var sword = Levelable(new Longsword(), level: 8);
        var s = LevelGearStore.Of(sword)!;
        var key = "clusterf.levelGear.capEnabled";
        var kept = ServerConfiguration.GetSetting(key, true);
        try
        {
            ServerConfiguration.SetSetting(key, false);
            var gained = ClusterFLevelGear.AddXp(sword, ClusterFLevelGear.XpForLevel(11) - s.Xp, null);
            Assert.Equal(ClusterFLevelGear.XpForLevel(11) - ClusterFLevelGear.XpForLevel(8), gained);
            Assert.Equal(11, s.Level);
            Assert.Equal(4 + 8 + 4, s.Points); // 9, 10 (the 10th), 11

            // Never past the max level: XP stops at level 200's total.
            ClusterFLevelGear.AddXp(sword, 100_000_000, null);
            Assert.Equal(200, s.Level);
            Assert.Equal(ClusterFLevelGear.XpForLevel(200), s.Xp);
            Assert.Equal(0, ClusterFLevelGear.AddXp(sword, 1000, null));
            Assert.Equal(19 * 8 + (200 - 11 - 19) * 4 + 16, s.Points);
        }
        finally
        {
            ServerConfiguration.SetSetting(key, kept);
        }
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void OneKillGivesAnItemAtMostTheNextLevelsXpOverTwenty()
    {
        var sword = Levelable(new Longsword());
        Assert.Equal(15, ClusterFLevelGear.CapFor(LevelGearStore.Of(sword)!)); // 300 / 20
        Assert.Equal(15, ClusterFLevelGear.AddXp(sword, 1000, null));
        Assert.Equal(15, LevelGearStore.Of(sword)!.Xp);

        var high = Levelable(new Longsword(), level: 50);
        var cap = (ClusterFLevelGear.XpForLevel(51)) / 20;
        Assert.Equal(13_515, cap);
        Assert.Equal(cap, ClusterFLevelGear.AddXp(high, 1_000_000, null));
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void ThePoolIsSplitByDamageWithAPetsDamageCountingAsItsMasters()
    {
        var a = Player();
        var b = Player();
        var pet = Track(new Horse { Controlled = true, ControlMaster = a });
        pet.MoveToWorld(Spot, Map.Trammel);

        var aSword = Levelable(new Longsword(), level: 50);
        var aRing = Levelable(new GoldRing(), level: 50);
        var aSpare = Levelable(new GoldBracelet(), level: 50);
        a.AddItem(aSword);
        a.AddItem(aRing);
        a.Backpack.DropItem(aSpare);
        var bSword = Levelable(new Longsword(), level: 50);
        b.AddItem(bSword);
        var petRing = Levelable(new GoldRing(), level: 50);
        pet.AddItem(petRing);
        Assert.Equal(pet, petRing.Parent);

        var orc = Track(new Orc());
        orc.MoveToWorld(Spot, Map.Trammel);
        orc.Damage(10, a);
        orc.Damage(20, pet);
        orc.Damage(20, b);
        var byA = orc.FindDamageEntryFor(a).DamageGiven;
        var byPet = orc.FindDamageEntryFor(pet).DamageGiven;
        var byB = orc.FindDamageEntryFor(b).DamageGiven;
        _out.WriteLine($"damage: a {byA}, a's pet {byPet}, b {byB}");
        Assert.True(byA > 0 && byPet > 0 && byB > 0);

        var start = ClusterFLevelGear.XpForLevel(50);
        var shares = ClusterFLevelGear.AwardDeath(orc, 1000);
        var total = byA + byPet + byB;
        var aShare = (long)Math.Round(1000.0 * (byA + byPet) / total);
        var bShare = (long)Math.Round(1000.0 * byB / total);
        Assert.Equal(aShare, shares[a]);
        Assert.Equal(bShare, shares[b]);
        Assert.False(shares.ContainsKey(pet));

        Assert.Equal(start + aShare, LevelGearStore.Of(aSword)!.Xp);
        Assert.Equal(start + aShare, LevelGearStore.Of(aRing)!.Xp);
        Assert.Equal(start + aShare, LevelGearStore.Of(petRing)!.Xp);
        Assert.Equal(start + bShare, LevelGearStore.Of(bSword)!.Xp);
        Assert.Equal(start, LevelGearStore.Of(aSpare)!.Xp); // in the backpack: not equipped

        // The real death event: the pool is the orc's Bard difficulty, split the same way.
        var orc2 = Track(new Orc());
        orc2.MoveToWorld(Spot, Map.Trammel);
        orc2.Damage(15, a);
        var pool = ClusterFLevelGear.PoolFor(orc2);
        _out.WriteLine($"orc pool {pool:F1}");
        Assert.True(pool > 0);
        var before = LevelGearStore.Of(aSword)!.Xp;
        orc2.Kill();
        Assert.Equal(before + (long)Math.Round(pool), LevelGearStore.Of(aSword)!.Xp);

        // A pet's death pays nothing, even to whoever damaged it. cc-P427 (cc-P379 finding 1): through a real death, as the
        // orc above. Pinned deletes a dead non-bonded pet, clearing its master, before CreatureDeathEvent; calling
        // AwardDeath on the live pet (as this fact did) never saw that.
        var tame = Track(new Horse { Controlled = true, ControlMaster = b });
        tame.MoveToWorld(Spot, Map.Trammel);
        tame.Damage(10, a);
        Assert.Empty(ClusterFLevelGear.AwardDeath(tame, 1000));
        Assert.True(ClusterFLevelGear.PoolFor(tame) > 0);
        before = LevelGearStore.Of(aSword)!.Xp;
        tame.Kill();
        Assert.True(tame.Deleted);
        Assert.Equal(before, LevelGearStore.Of(aSword)!.Xp);

        // A summoned creature's death pays nothing either.
        var summon = Track(new Orc { Summoned = true, SummonMaster = b });
        summon.MoveToWorld(Spot, Map.Trammel);
        summon.Damage(10, a);
        summon.Kill();
        Assert.Equal(before, LevelGearStore.Of(aSword)!.Xp);
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void PointsBuyUpToEachPropertysCeilingAndNoFurther()
    {
        var di = LevelGearAttribute.ByKey("damageIncrease")!;
        var luck = LevelGearAttribute.ByKey("luck")!;
        var resist = LevelGearAttribute.ByKey("physicalResist")!;

        // A regular sword: Damage Increase to the imbuing Full value, 50, at 5 points each.
        var sword = Levelable(new Longsword(), points: 1000);
        Assert.Equal(50, di.Ceiling(sword));
        for (var i = 0; i < 50; i++)
        {
            Assert.Null(ClusterFLevelGear.Buy(sword, di));
        }

        Assert.Contains("at its most", ClusterFLevelGear.Buy(sword, di));
        Assert.Equal(50, ClusterFLevelGear.EffectiveBonus(sword, di));
        Assert.Equal(1000 - 50 * 5, LevelGearStore.Of(sword)!.Points);

        // Its own (imbued) value counts against the ceiling, even when it was raised after the level bought.
        var imbued = Levelable(new Longsword(), points: 1000);
        imbued.Attributes.WeaponDamage = 30;
        Assert.Equal(20, di.MaxBuy(imbued));
        for (var i = 0; i < 20; i++)
        {
            Assert.Null(ClusterFLevelGear.Buy(imbued, di));
        }

        Assert.NotNull(ClusterFLevelGear.Buy(imbued, di));
        imbued.Attributes.WeaponDamage = 45;
        Assert.Equal(5, ClusterFLevelGear.EffectiveBonus(imbued, di));

        // Exceptional: x1.5 (75), but never past Damage Increase's Overcharged maximum (70).
        var fine = Levelable(new Longsword(), points: 1000);
        fine.Quality = WeaponQuality.Exceptional;
        var overcharged = di.Def!.TopValue;
        _out.WriteLine($"Damage Increase: Full {di.Def.FullMax}, Overcharged {overcharged}");
        Assert.Equal(70, overcharged);
        Assert.Equal(70, di.Ceiling(fine));
        Assert.Equal(70, di.MaxBuy(fine));

        // Another item kind's property, too few points, the slot limit.
        var ring = Levelable(new GoldRing(), points: 1);
        Assert.Contains("cannot go on", ClusterFLevelGear.Buy(ring, di));
        Assert.Contains("costs", ClusterFLevelGear.Buy(ring, LevelGearAttribute.ByKey("defenseChance")!));
        Assert.Contains("cannot go on", ClusterFLevelGear.Buy(ring, resist));

        var full = Levelable(new GoldRing(), points: 1000);
        full.Attributes.DefendChance = 5;
        full.Attributes.LowerManaCost = 5;
        full.Attributes.LowerRegCost = 5;
        full.Attributes.SpellDamage = 5;
        full.Attributes.EnhancePotions = 5;
        Assert.Equal(5, ClusterFLevelGear.SlotsUsed(full));
        Assert.Contains("properties", ClusterFLevelGear.Buy(full, luck));
        Assert.Null(ClusterFLevelGear.Buy(full, LevelGearAttribute.ByKey("defenseChance")!));

        // The setting's max per attribute, below the ceiling.
        var key = "clusterf.levelGear.attr.luck.max";
        var kept = ServerConfiguration.GetSetting(key, 0);
        try
        {
            ServerConfiguration.SetSetting(key, "3");
            var bracelet = Levelable(new GoldBracelet(), points: 100);
            Assert.Equal(3, luck.MaxBuy(bracelet));
        }
        finally
        {
            ServerConfiguration.SetSetting(key, kept.ToString());
        }
    }

    // ---------------------------------------------------------------- 6

    [Fact]
    public void TheBonusCountsWhereTheStockValueIsReadOnlyWhileWorn()
    {
        var pm = Player();
        var di = LevelGearAttribute.ByKey("damageIncrease")!;
        var resist = LevelGearAttribute.ByKey("physicalResist")!;

        var sword = Levelable(new Longsword(), points: 100);
        sword.Attributes.WeaponDamage = 10;
        for (var i = 0; i < 7; i++)
        {
            Assert.Null(ClusterFLevelGear.Buy(sword, di));
        }

        pm.Backpack.DropItem(sword);
        Assert.Equal(0, AosAttributes.GetValue(pm, AosAttribute.WeaponDamage));
        pm.AddItem(sword);
        Assert.Equal(17, AosAttributes.GetValue(pm, AosAttribute.WeaponDamage));
        Assert.Equal(10, sword.Attributes.WeaponDamage); // the item's own Attributes are never written

        var shield = Levelable(new HeaterShield(), points: 100);
        pm.AddItem(shield);
        pm.UpdateResistances();
        var before = pm.PhysicalResistance;
        for (var i = 0; i < 4; i++)
        {
            Assert.Null(ClusterFLevelGear.Buy(shield, resist));
        }

        Assert.Equal(before + 4, pm.PhysicalResistance);

        // A creature wearing levelable gear: the same bonuses (Chase's rule 3, companions).
        var horse = Track(new Horse());
        horse.MoveToWorld(Spot, Map.Trammel);
        var petShield = Levelable(new HeaterShield(), points: 100);
        var ring = Levelable(new GoldRing(), points: 100);
        var dci = LevelGearAttribute.ByKey("defenseChance")!;
        for (var i = 0; i < 3; i++)
        {
            Assert.Null(ClusterFLevelGear.Buy(petShield, resist));
            Assert.Null(ClusterFLevelGear.Buy(ring, dci));
        }

        horse.UpdateResistances();
        var bare = horse.PhysicalResistance;
        horse.AddItem(petShield);
        horse.AddItem(ring);
        horse.UpdateResistances();
        Assert.Equal(bare + petShield.PhysicalResistance + 3, horse.PhysicalResistance);
        Assert.Equal(3, AosAttributes.GetValue(horse, AosAttribute.DefendChance));
    }

    // ---------------------------------------------------------------- 7

    [Fact]
    public void RowsSurviveSaveAndLoadAndADeletedItemsRowIsDropped()
    {
        var sword = Levelable(new Longsword(), level: 37, points: 9);
        var s = LevelGearStore.Of(sword)!;
        s.Xp += 123;
        s.MaxLevel = 150;
        s.Bought["damageIncrease"] = 12;
        s.Bought["luck"] = 40;
        s.MadeBy = (Serial)0x1234u;
        var made = s.MadeAt;

        var gone = Levelable(new GoldRing(), level: 3);
        gone.Delete();

        var buffer = new byte[1 << 16];
        var writer = new BufferWriter(buffer, true);
        LevelGearStore.Instance.Serialize(writer);
        writer.Flush();

        LevelGearStore.Rows.Clear();
        LevelGearStore.Instance.Deserialize(new BufferReader(buffer));

        Assert.Single(LevelGearStore.Rows);
        var t = LevelGearStore.Of(sword)!;
        Assert.Equal(37, t.Level);
        Assert.Equal(ClusterFLevelGear.XpForLevel(37) + 123, t.Xp);
        Assert.Equal(9, t.Points);
        Assert.Equal(150, t.MaxLevel);
        Assert.Equal(made, t.MadeAt);
        Assert.Equal((Serial)0x1234u, t.MadeBy);
        Assert.Equal(12, t.Bought["damageIncrease"]);
        Assert.Equal(40, t.Bought["luck"]);
        Assert.Equal(2, t.Bought.Count);
        Assert.Null(LevelGearStore.Of(gone));
    }
}
