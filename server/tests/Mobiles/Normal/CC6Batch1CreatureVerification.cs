// CC6 batch 1: twenty creatures chosen because pinned ModernUO's own spawn data (Distribution/Data/Spawns)
// already places them and no loaded assembly declared them, so their spawner entries were running with
// EntryFlags.InvalidType (BaseSpawner.cs:1126-1131). This file proves the batch does what it was chosen for:
// every one of the twenty constructs with its ServUO values, and every stock spawn entry that names one of
// them now resolves to a type.
//
// server/tests/<path> is mirrored into Projects/UOContent.Tests/Tests/<path> by docker/uo/apply-patches.sh and
// run as a gate by docker/uo/build.sh. The test host copies Distribution/Data into its output directory
// (UOContent.Tests.csproj, S8), so the spawn JSON files are read from Core.BaseDirectory here exactly as the
// server reads them for [GenerateSpawners.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class CC6Batch1CreatureVerification
{
    private readonly ITestOutputHelper _out;

    public CC6Batch1CreatureVerification(ITestOutputHelper output) => _out = output;

    // (type, ServUO Name, ServUO CorpseName, Body, Hue, Fame): the values a player sees, verbatim from the ServUO files.
    private static readonly (Type Type, string Name, string Corpse, int Body, int Hue, int Fame)[] Batch =
    {
        (typeof(GrayGoblin), "a gray goblin", "a goblin corpse", 723, 1900, 1500),
        (typeof(GrayGoblinMage), "a gray goblin mage", "a goblin mage corpse", 723, 1900, 1500),
        (typeof(GrayGoblinKeeper), "a gray goblin keeper", "a goblin keeper corpse", 723, 1900, 1500),
        (typeof(GreenGoblin), "a green goblin", "a goblin corpse", 723, 0, 1500),
        (typeof(GreenGoblinAlchemist), "a green goblin alchemist", "a goblin corpse", 723, 0, 1500),
        (typeof(GreenGoblinScout), "a green goblin scout", "a goblin corpse", 723, 0, 1500),
        (typeof(EnslavedGrayGoblin), "Enslaved Gray Goblin", "an goblin corpse", 334, 0, 1500),
        (typeof(EnslavedGreenGoblin), "Enslaved Green Goblin", "an goblin corpse", 334, 0, 1500),
        (typeof(EnslavedGoblinScout), "Enslaved Goblin Scout", "an goblin corpse", 334, 0, 1500),
        (typeof(EnslavedGoblinKeeper), "Enslaved Goblin Keeper", "an goblin corpse", 334, 0, 1500),
        (typeof(EnslavedGreenGoblinAlchemist), "Green Goblin Alchemist", "an goblin corpse", 723, 0, 1500),
        (typeof(EnslavedGoblinMage), "Enslaved Goblin Mage", "an goblin corpse", 334, 0, 1500),
        (typeof(TanglingRoots), "a tangling root", "a tangling root corpse", 8, 0, 3000),
        (typeof(SentinelSpider), "a Sentinel spider", "a sentinel spider corpse", 0x9d, 1141, 775),
        (typeof(Gremlin), "a gremlin", "a gremlin corpse", 724, 0, 0),
        (typeof(TrapdoorSpider), "a trapdoor spider", "a trapdoor spider corpse", 737, 0, 0),
        (typeof(Skree), "a skree", "a skree corpse", 733, 0, 0),
        (typeof(UndeadGuardian), "an undead guardian", "an undead guardian corpse", 722, 0, 0),
        (typeof(PutridUndeadGuardian), "an putrid undead guardian", "an putrid undead guardian corpse", 722, 0, 3000),
        (typeof(Spellbinder), "a spectral spellbinder", "a ghostly corpse", -1, 0, 2500)
    };

    private static PlayerMobile NewPlayer(Point3D loc, Map map = null)
    {
        var pm = new PlayerMobile();
        pm.AddItem(new Backpack());
        pm.MoveToWorld(loc, map ?? Map.TerMur);
        pm.Hits = pm.HitsMax; // a fresh PlayerMobile has 0 hits; the first point of damage would kill it
        return pm;
    }

    [Fact]
    public void AllTwentyConstructWithTheirServUOValues()
    {
        Assert.Equal(20, Batch.Length);

        foreach (var (type, name, corpse, body, hue, fame) in Batch)
        {
            var bc = (BaseCreature)Activator.CreateInstance(type)!;

            _out.WriteLine($"{type.Name}: name='{bc.Name}' body={(int)bc.Body} hue={bc.Hue} fame={bc.Fame} " +
                           $"hits={bc.HitsMax} ai={bc.AI} speed={bc.ActiveSpeed}/{bc.PassiveSpeed}");

            Assert.Equal(name, bc.Name);
            Assert.Equal(corpse, bc.CorpseName);
            Assert.Equal(hue, bc.Hue);
            Assert.Equal(fame, bc.Fame);
            Assert.Equal(-fame, bc.Karma);
            Assert.True(bc.HitsMax > 0, $"{type.Name} has no hits");

            if (body >= 0)
            {
                Assert.Equal(body, (int)bc.Body);
            }

            // Q-008: every ported creature takes ModernUO's Medium fallback; none of these overrides it.
            Assert.Equal(0.25, bc.ActiveSpeed);
            Assert.Equal(0.5, bc.PassiveSpeed);

            // The spawner path: BaseSpawner.Spawn resolves SpawnedName through AssemblyHandler.FindTypeByName.
            Assert.Same(type, AssemblyHandler.FindTypeByName(type.Name));

            bc.Delete();
        }
    }

    [Fact]
    public void EveryStockSpawnEntryNamingOneOfTheTwentyNowResolves()
    {
        var spawnsDir = Path.Combine(Core.BaseDirectory, "Data", "Spawns");
        Assert.True(Directory.Exists(spawnsDir), $"no spawn data at {spawnsDir}");

        var files = Directory.GetFiles(spawnsDir, "*.json", SearchOption.AllDirectories);
        Assert.True(files.Length > 100, $"expected pinned ModernUO's 109 spawn files, found {files.Length}");

        // entry name (lower case) -> number of spawner entries naming it, across every file the admin gump generates
        var entries = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(file));

            foreach (var spawner in doc.RootElement.EnumerateArray())
            {
                if (!spawner.TryGetProperty("entries", out var list))
                {
                    continue;
                }

                foreach (var entry in list.EnumerateArray())
                {
                    var name = entry.GetProperty("name").GetString()!;
                    entries[name] = entries.GetValueOrDefault(name) + 1;
                }
            }
        }

        var unresolved = entries.Keys.Where(n => AssemblyHandler.FindTypeByName(n) == null).OrderBy(n => n).ToList();
        _out.WriteLine($"{entries.Count} distinct entry names in {files.Length} files; {unresolved.Count} still resolve to no type: " +
                       string.Join(", ", unresolved));

        var referenced = 0;

        foreach (var (type, _, _, _, _, _) in Batch)
        {
            Assert.True(entries.TryGetValue(type.Name, out var count) && count > 0,
                $"{type.Name} is not named by any stock spawn entry; it was chosen because it is");
            Assert.DoesNotContain(type.Name, unresolved, StringComparer.OrdinalIgnoreCase);
            referenced += count;
            _out.WriteLine($"{type.Name}: {count} spawn entries");
        }

        // Measured 2026-09-19 against pinned 7c9215d97 before this batch: 109 names unresolved. The twenty here
        // were among them, and nothing else in this batch adds or removes a type, so the count was exactly 89.
        // CC6 batch 2 (same day) ported fourteen more of the 89, so it is now 75; that file's own test pins the
        // batch-2 figure and tools/spawn-orphans.txt carries the list. Every later batch moves this number.
        Assert.Equal(75, unresolved.Count);
        // Counted the same day from the same files: 46 entries name the twenty (GrayGoblin 6, GreenGoblin 5, ...).
        Assert.Equal(46, referenced);
    }

    [Fact]
    public void GrayAndGreenGoblinsAreEachOthersEnemiesAndTheEnslavedGroupIsInert()
    {
        var gray = new GrayGoblin();
        var grayMage = new GrayGoblinMage();
        var grayKeeper = new GrayGoblinKeeper();
        var green = new GreenGoblin();
        var greenAlch = new GreenGoblinAlchemist();
        var greenScout = new GreenGoblinScout();

        // ServUO IsTribeEnemy: gray <-> green, both directions, every member.
        foreach (var g in new BaseCreature[] { gray, grayMage, grayKeeper })
        {
            foreach (var h in new BaseCreature[] { green, greenAlch, greenScout })
            {
                Assert.True(g.IsEnemy(h), $"{g.GetType().Name} should be an enemy of {h.GetType().Name}");
                Assert.True(h.IsEnemy(g), $"{h.GetType().Name} should be an enemy of {g.GetType().Name}");
                Assert.False(g.IsFriend(h));
                Assert.False(h.IsFriend(g));
            }
        }

        // ...and not within a tribe.
        Assert.False(gray.IsEnemy(grayMage));
        Assert.False(grayMage.IsEnemy(grayKeeper));
        Assert.False(green.IsEnemy(greenAlch));
        Assert.False(greenAlch.IsEnemy(greenScout));
        Assert.True(gray.IsFriend(grayKeeper));
        Assert.True(green.IsFriend(greenScout));

        // The enslaved goblins carry ServUO's OppositionGroup.SavagesAndOrcs override, which names neither them nor
        // anything they meet, so it decides nothing: an orc is not their enemy through it, nor a savage.
        var enslaved = new EnslavedGrayGoblin();
        var orc = new Orc();
        var savage = new Savage();
        Assert.Same(OppositionGroup.SavagesAndOrcs, enslaved.OppositionGroup);
        Assert.False(OppositionGroup.SavagesAndOrcs.IsEnemy(enslaved, orc));
        Assert.False(OppositionGroup.SavagesAndOrcs.IsEnemy(enslaved, savage));
        Assert.False(OppositionGroup.SavagesAndOrcs.IsEnemy(orc, enslaved));
        Assert.Equal(-1, OppositionGroup.SavagesAndOrcs.IndexOf(enslaved));

        // A free goblin and an enslaved one are not tribal enemies either (the enslaved carry no tribe).
        Assert.False(gray.IsEnemy(enslaved));
        Assert.False(green.IsEnemy(enslaved));

        foreach (var m in new Mobile[] { gray, grayMage, grayKeeper, green, greenAlch, greenScout, enslaved, orc, savage })
        {
            m.Delete();
        }
    }

    [Fact]
    public void TheTrapdoorSpiderStartsHiddenRevealsWhenHitAndKeepsStealthWhileItWalks()
    {
        var spider = new TrapdoorSpider();
        spider.MoveToWorld(new Point3D(1000, 1000, 0), Map.TerMur);

        Assert.True(spider.Hidden);
        Assert.True(spider.Skills.Stealth.Value >= 110.5);
        Assert.True(spider.Skills.Hiding.Value >= 110.3);

        // ServUO's OnMove branch (inlined in TrapdoorSpider.OnMove): a hidden creature that CanStealth spends
        // AllowedStealthSteps and re-rolls Stealth.OnUse when they run out. Stock ModernUO would reveal it on
        // this first step. At Stealth >= 110 the roll cannot fail, so the outcome is deterministic.
        //
        // Mobile.Move runs CheckMovement before the OnMove hook (Mobile.cs CanMove), and the test host has no map
        // files, so a real Move fails before the hook. The hook is protected; it is invoked directly.
        var onMove = typeof(TrapdoorSpider).GetMethod(
            "OnMove", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
        );
        Assert.NotNull(onMove);
        Assert.Same(typeof(TrapdoorSpider), onMove.DeclaringType);

        spider.AllowedStealthSteps = 0;
        onMove.Invoke(spider, new object[] { Direction.North });

        _out.WriteLine($"after one step: hidden={spider.Hidden} steps={spider.AllowedStealthSteps}");
        Assert.True(spider.Hidden, "the spider revealed itself on its first step (stock Mobile.OnMove behaviour)");
        Assert.True(spider.AllowedStealthSteps > 0, "Stealth.OnUse did not grant steps");

        // Running spends two steps at a time and reveals when they run out.
        spider.AllowedStealthSteps = 1;
        onMove.Invoke(spider, new object[] { Direction.North | Direction.Running });
        Assert.False(spider.Hidden);

        // Damage reveals it (OnDamage calls RevealingAction before the base).
        spider.Hidden = true;
        spider.Damage(1, null);
        Assert.False(spider.Hidden);

        spider.Delete();
    }

    [Fact]
    public void TanglingRootsEntangleAPlayerWhoMovesNearbyAndTheAcidTicks()
    {
        ShardTestClock.Arm();

        var roots = new TanglingRoots();
        roots.MoveToWorld(new Point3D(1100, 1100, 0), Map.TerMur);

        var player = NewPlayer(new Point3D(1103, 1100, 0));
        Assert.True(roots.InRange(player, 6));

        // 20% per movement; 200 movements leave a failure chance of 0.8^200.
        var entangled = false;
        for (var i = 0; i < 200 && !entangled; i++)
        {
            roots.OnMovement(player, player.Location);
            entangled = player.Frozen;
        }

        Assert.True(entangled, "the player was never entangled");
        Assert.Equal(roots.Location, player.Location);
        Assert.True(player.InRange(roots, 1));

        // The acid timer started the moment the player stood adjacent (delay zero, one second interval). A fixed
        // advance cannot witness it: Mobile.Damage arms HitsTimer, the test host regenerates the four points within
        // the same advance, and the Hits setter clears DamageEntries once hits are full again (Mobile.cs:2022; the
        // Q-029 shape, see the Shame mana-taint fact). So walk the clock one wheel tick at a time and read the hit
        // on the tick it lands.
        Assert.Empty(player.DamageEntries);
        var landed = false;
        var ticks = 0;

        for (; ticks < 300 && !landed; ticks++) // up to 2.4 s, two acid ticks
        {
            ShardTestClock.Advance(TimeSpan.FromMilliseconds(8));
            landed = player.DamageEntries.Any(e => e.Damager == roots);
        }

        _out.WriteLine($"acid landed after {ticks} wheel ticks: hits={player.Hits}/{player.HitsMax} frozen={player.Frozen}");
        Assert.True(landed, "the acid never damaged the adjacent player within 2.4 s");

        // Untangled after at most six seconds from the entangle (at most 2.4 s have passed).
        ShardTestClock.Advance(TimeSpan.FromSeconds(6.1));
        Assert.False(player.Frozen);

        // Still on cooldown for 15 s from the entangle, so no second entangle even at 100% of the rolls.
        player.MoveToWorld(new Point3D(1103, 1100, 0), Map.TerMur);
        for (var i = 0; i < 50; i++)
        {
            roots.OnMovement(player, player.Location);
        }

        Assert.False(player.Frozen);

        ShardTestClock.Advance(TimeSpan.FromSeconds(20));

        player.Delete();
        roots.Delete();
    }

    [Fact]
    public void TheRestOfTheBatchKeepsItsServUOShape()
    {
        var spider = new SentinelSpider();
        Assert.Same(WeaponAbility.ArmorIgnore, spider.GetWeaponAbility());
        Assert.Equal(PackInstinct.Arachnid, spider.PackInstinct);

        var skree = new Skree();
        Assert.True(skree.Tamable);
        Assert.Equal(4, skree.ControlSlots);
        Assert.Equal(95.1, skree.MinTameSkill);
        Assert.Equal(AIType.AI_Mage, skree.AI); // deviation: ServUO AI_Mystic
        Assert.True(skree.Skills.Mysticism.Value >= 80);
        Assert.Equal(MeatType.Bird, skree.MeatType);

        var binder = new Spellbinder();
        Assert.Contains((int)binder.Body, new[] { 26, 50, 56 });
        Assert.Equal(FightMode.Aggressor, binder.FightMode);
        Assert.Equal(AIType.AI_Mage, binder.AI); // deviation: ServUO AI_Spellbinder
        Assert.Same(OppositionGroup.FeyAndUndead, binder.OppositionGroup);

        var scout = new GreenGoblinScout();
        Assert.Equal(7, scout.RangeFight);
        Assert.Equal(AIType.AI_Melee, scout.AI); // deviation: ServUO AI_OrcScout
        Assert.Equal(BaseCreature.DefaultRangePerception, scout.RangePerception);

        var gremlin = new Gremlin();
        Assert.Equal(AIType.AI_Archer, gremlin.AI);
        Assert.NotNull(gremlin.FindItemOnLayer(Layer.TwoHanded));
        Assert.NotNull(gremlin.Backpack?.FindItemByType<Arrow>());

        var guardian = new UndeadGuardian();
        Assert.True(guardian.Backpack?.FindItemByType<BaseReagent>() != null, "no necro reagents packed");

        var roots = new TanglingRoots();
        Assert.True(roots.DisallowAllMoves);
        Assert.NotNull(roots.Backpack?.FindItemByType<MandrakeRoot>());

        // The free goblins each carry boots and a random one of ribs, a shaft or a candle (ServUO PackItem).
        var goblin = new GreenGoblin();
        Assert.NotNull(goblin.Backpack?.FindItemByType<ThighBoots>());

        foreach (var m in new Mobile[] { spider, skree, binder, scout, gremlin, guardian, roots, goblin })
        {
            m.Delete();
        }
    }
}
