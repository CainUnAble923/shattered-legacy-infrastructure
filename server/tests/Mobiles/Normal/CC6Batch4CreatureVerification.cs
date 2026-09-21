// CC6 batch 4: the Peerless key trio and the chicken lizard cluster. Five creatures chosen by batch 1's rule - pinned
// ModernUO's own spawn data (Distribution/Data/Spawns) places four of them (CrystalHydra, FairyDragon,
// SerpentsFangHighExecutioner, ChickenLizard) and no loaded assembly declared them - plus BattleChickenLizard, which no
// spawner names and only a mature ChickenLizardEgg produces. Six items travel with them: PeerlessKey (the inert key
// base, P9 / Q-051), the three keys that derive from it, FairyDragonWing and ChickenLizardEgg. CrystallineFragments,
// which the brief listed as a seventh, is stock ModernUO and is not re-ported.
//
// This file proves the batch does what it was chosen for: every one constructs with its ServUO values, every stock
// spawn entry naming one of them now resolves, the three keys sit on PeerlessKey and count down, the high executioner
// is a correctly generated subclass of our own SerpentsFangAssassin (the first time this project has subclassed one of
// its own ports), the chicken lizard lays for its bonded owner with the cooldown persisted, and the egg incubates,
// waters, mutates and hatches a battle chicken lizard. Creature drops are checked through a real Kill(), which needs
// the corpse handler this batch added to the fixture patch.
// server/tests/<path> is mirrored into Projects/UOContent.Tests/Tests/<path> by docker/uo/apply-patches.sh and run
// as a gate by docker/uo/build.sh (see batch 1's file for the host facts).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using ModernUO.Serialization;
using Server;
using Server.Items;
using Server.Mobiles;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class CC6Batch4CreatureVerification
{
    private readonly ITestOutputHelper _out;

    public CC6Batch4CreatureVerification(ITestOutputHelper output) => _out = output;

    // (type, spawn-data spelling or null, ServUO Name, ServUO CorpseName, Body, Hue, Fame, Karma), verbatim from the
    // ServUO files. The Citadel names the executioner in lower case; FindTypeByName ignores case and so must this.
    private static readonly (Type Type, string SpawnName, string Name, string Corpse, int Body, int Hue, int Fame, int Karma)[] Batch =
    {
        (typeof(ChickenLizard), "ChickenLizard", "a chicken lizard", "a chicken lizard corpse", 716, 0, 0, 0),
        (typeof(CrystalHydra), "CrystalHydra", "a crystal hydra", "a crystal hydra corpse", 0x109, 0x47E, 17000, -17000),
        (typeof(FairyDragon), "FairyDragon", "Fairy Dragon", "a Fairy dragon corpse", 718, 0, 15000, -15000),
        (typeof(SerpentsFangHighExecutioner), "serpentsfanghighexecutioner", "Black Order High Executioner", "a black order high executioner corpse", -1, -1, 25000, -25000),
        (typeof(BattleChickenLizard), null, "a battle chicken lizard", "a chicken lizard corpse", 716, 0, 0, 0)
    };

    private static PlayerMobile NewPlayer(Point3D loc, Map map = null)
    {
        // Mobile.Player is a flag CharacterCreation sets, not something the PlayerMobile constructor does (batch 3).
        var pm = new PlayerMobile { Player = true };
        pm.AddItem(new Backpack());
        pm.MoveToWorld(loc, map ?? Map.TerMur);
        pm.Hits = pm.HitsMax;
        return pm;
    }

    private static Container KillAndGetCorpse(BaseCreature bc, Point3D loc)
    {
        bc.MoveToWorld(loc, Map.TerMur);
        bc.Kill();
        var corpse = bc.Corpse;
        Assert.NotNull(corpse); // the fixture's Corpse.Initialize() line, added by this batch
        return corpse;
    }

    [Fact]
    public void AllFiveConstructWithTheirServUOValues()
    {
        Assert.Equal(5, Batch.Length);

        foreach (var (type, spawnName, name, corpse, body, hue, fame, karma) in Batch)
        {
            var bc = (BaseCreature)Activator.CreateInstance(type)!;

            _out.WriteLine($"{type.Name}: name='{bc.Name}' body={(int)bc.Body} hue={bc.Hue} fame={bc.Fame} karma={bc.Karma} " +
                           $"hits={bc.HitsMax} ai={bc.AI} speed={bc.ActiveSpeed}/{bc.PassiveSpeed}");

            Assert.Equal(name, bc.Name);
            Assert.Equal(corpse, bc.CorpseName);

            if (body >= 0)
            {
                Assert.Equal(body, (int)bc.Body);
                Assert.Equal(hue, bc.Hue);
            }
            else
            {
                Assert.True(bc.Body.IsHuman, $"{type.Name} should have a human body");
            }

            Assert.Equal(fame, bc.Fame);
            Assert.Equal(karma, bc.Karma);
            Assert.True(bc.HitsMax > 0, $"{type.Name} has no hits");

            // Q-008: every ported creature takes ModernUO's Medium fallback; none of these overrides it.
            Assert.Equal(0.25, bc.ActiveSpeed);
            Assert.Equal(0.5, bc.PassiveSpeed);

            if (spawnName != null)
            {
                Assert.Same(type, AssemblyHandler.FindTypeByName(spawnName));
            }

            bc.Delete();
        }
    }

    [Fact]
    public void EveryStockSpawnEntryNamingOneOfTheFourNowResolves()
    {
        var spawnsDir = Path.Combine(Core.BaseDirectory, "Data", "Spawns");
        Assert.True(Directory.Exists(spawnsDir), $"no spawn data at {spawnsDir}");

        var files = Directory.GetFiles(spawnsDir, "*.json", SearchOption.AllDirectories);
        Assert.True(files.Length > 100, $"expected pinned ModernUO's 109 spawn files, found {files.Length}");

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

        foreach (var (type, spawnName, _, _, _, _, _, _) in Batch)
        {
            if (spawnName == null)
            {
                // The battle chicken lizard is not spawn-placed anywhere; it exists only through the egg.
                Assert.False(entries.ContainsKey(type.Name), $"{type.Name} is named by stock spawn data after all");
                continue;
            }

            Assert.True(entries.TryGetValue(spawnName, out var count) && count > 0,
                $"{type.Name} is not named by any stock spawn entry; it was chosen because it is");
            Assert.DoesNotContain(spawnName, unresolved, StringComparer.OrdinalIgnoreCase);
            referenced += count;
            _out.WriteLine($"{type.Name}: {count} spawn entries as '{spawnName}'");
        }

        // Batch 3 left exactly 62 names unresolved (tools/spawn-orphans.txt at 8161862). The FOUR spawn-named
        // creatures here were among them; the battle chicken lizard never was, so the brief's "62 -> 57" was one too
        // many. Nothing else in this batch adds or removes a spawnable type, so the count must be exactly 58.
        // CC6 batch 5 (2026-09-20) ported the whole Renowned row, seventeen of the 58, so it is now 41; that file's
        // test pins the batch-5 figure and tools/spawn-orphans.txt carries the list.
        // CC6 batch 6 (2026-09-21) ported nothing; its one-name [TypeAlias] spike (Mobiles/Aliases/EliteNinjaSpawnAlias.cs,
        // Q-055) makes "eliteninjawarrior" resolve to stock EliteNinja, so it is now 40. Delete that file and it is 41.
        // CC6 batch 7 (2026-09-21), Part A: Q-055 answered yes, so the eight remaining name-mapping aliases were added
        // (Mobiles/Aliases/*SpawnAlias.cs: DryadA -> MLDryad, abbein..vicaie -> the seven Heartwood elders), so it is now 32.
        // CC6 batch 7 (2026-09-21), Parts B and C: the leaf-creature row (MageDragonsFlameMage and the seven Clan ratmen,
        // eight names) and TreasureLevel1h (one name) ported, so it is now 23.
        Assert.Equal(23, unresolved.Count);
        // Counted 2026-09-20 from the same files with tools/spawn_orphans.py's reader: ChickenLizard 8 (TerMur),
        // CrystalHydra 2 (PrismOfLight, Felucca and Trammel), FairyDragon 1 (Abyss), serpentsfanghighexecutioner 1
        // (Citadel).
        Assert.Equal(12, referenced);
    }

    [Fact]
    public void PeerlessKeyIsTheThreeKeysFinalParentAndCountsDown()
    {
        // The keys schedule their 10 s slice on construction; arm the route's clock first so the wheel is theirs.
        ShardTestClock.Arm();

        var crystals = new ShatteredCrystals();
        var orb = new DraconicOrb();
        var key = new SerpentFangKey();

        foreach (var k in new PeerlessKey[] { crystals, orb, key })
        {
            Assert.Equal(LootType.Blessed, k.LootType);
            Assert.Equal(k.Lifespan, k.TimeLeft);
            Assert.Null(k.KeyMap);
            // The subclass rule: each level carries its own generator attribute (not inherited) and its own
            // (Serial) constructor, so each has its own version slot after PeerlessKey's.
            Assert.Single(k.GetType().GetCustomAttributes(typeof(SerializationGeneratorAttribute), false));
            Assert.Same(k.GetType(), k.GetType().GetConstructor(new[] { typeof(Serial) })!.DeclaringType);
        }

        Assert.Single(typeof(PeerlessKey).GetCustomAttributes(typeof(SerializationGeneratorAttribute), false));

        Assert.Equal(0x223F, crystals.ItemID);
        Assert.Equal(0x47E, crystals.Hue);
        Assert.Equal(1.0, crystals.Weight);
        Assert.Equal(1074266, crystals.LabelNumber);
        Assert.Equal(604800, crystals.Lifespan);

        Assert.Equal(0x573E, orb.ItemID);
        Assert.Equal(0x80F, orb.Hue);
        Assert.Equal(1.0, orb.Weight);
        Assert.Equal(1113515, orb.LabelNumber);
        Assert.Equal(43200, orb.Lifespan); // 12 hours, the one override of the base's week

        Assert.Equal(0x2002, key.ItemID);
        Assert.Equal(53, key.Hue);
        Assert.Equal(2.0, key.Weight);
        Assert.Equal(1074341, key.LabelNumber);
        Assert.Equal(604800, key.Lifespan);
        Assert.False(key.UseSeconds);

        // The 10-second timer really ticks: 25 s of clock is two slices for all three keys.
        ShardTestClock.Advance(TimeSpan.FromSeconds(25));
        Assert.Equal(604780, crystals.TimeLeft);
        Assert.Equal(43180, orb.TimeLeft);
        Assert.Equal(604780, key.TimeLeft);

        // Each slice takes 10 off; at or below zero the key decays: message, smoke, Delete().
        crystals.Slice();
        Assert.Equal(604770, crystals.TimeLeft);
        Assert.False(crystals.Deleted);

        crystals.KeyMap = Map.Felucca;
        Assert.Same(Map.Felucca, crystals.KeyMap);

        crystals.TimeLeft = 10;
        crystals.Slice();
        Assert.True(crystals.Deleted);

        // Decay inside a player's pack takes the RootParent branch.
        var pm = NewPlayer(new Point3D(1000, 1000, 0));
        pm.AddToBackpack(orb);
        Assert.NotNull(pm.Backpack!.FindItemByType<DraconicOrb>());
        orb.TimeLeft = 5;
        orb.Slice();
        Assert.True(orb.Deleted);
        Assert.Null(pm.Backpack.FindItemByType<DraconicOrb>());

        // A deleted key stops ticking (the one housekeeping line PeerlessKey adds over ServUO's).
        key.Delete();
        var stopped = key.TimeLeft;
        ShardTestClock.Advance(TimeSpan.FromSeconds(30));
        Assert.Equal(stopped, key.TimeLeft);

        pm.Delete();
    }

    [Fact]
    public void TheHighExecutionerIsAGeneratedSubclassOfOurOwnAssassin()
    {
        var exec = new SerpentsFangHighExecutioner();

        // The subclass rule, checked on the type itself: the attribute is declared here (not inherited from the
        // assassin), and the generator emitted this level's own (Serial) constructor.
        Assert.IsAssignableFrom<SerpentsFangAssassin>(exec);
        Assert.Single(typeof(SerpentsFangHighExecutioner).GetCustomAttributes(typeof(SerializationGeneratorAttribute), false));
        Assert.Single(typeof(SerpentsFangAssassin).GetCustomAttributes(typeof(SerializationGeneratorAttribute), false));
        var serialCtor = typeof(SerpentsFangHighExecutioner).GetConstructor(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(Serial) }, null);
        Assert.NotNull(serialCtor);
        Assert.Same(typeof(SerpentsFangHighExecutioner), serialCtor!.DeclaringType);

        // ServUO's values over the assassin's.
        Assert.Equal("of the Serpent's Fang Sect", exec.Title);
        Assert.InRange(exec.RawStr, 545, 560);
        Assert.InRange(exec.RawDex, 160, 175);
        Assert.Equal(800, exec.HitsMax);
        Assert.InRange(exec.StamMax, 190, 205);
        Assert.Equal(15, exec.DamageMin);
        Assert.Equal(20, exec.DamageMax);
        Assert.Equal(60, exec.VirtualArmor);
        Assert.True(exec.AlwaysMurderer);
        Assert.False(exec.ShowFameTitle);
        Assert.Equal(AIType.AI_Melee, exec.AI);
        Assert.Same(Race.Human, exec.Race);
        // The assassin's constructor ran first: its gear is on the executioner.
        Assert.Contains(exec.Items, i => i is Sai);
        Assert.Contains(exec.Items, i => i is JinBaori);
        Assert.Contains(exec.Items, i => i is StuddedMempo);

        // ServUO: AlterMeleeDamageFrom returns half of every melee hit onto the attacker.
        var attacker = NewPlayer(new Point3D(1000, 1000, 0));
        var before = attacker.Hits;
        var damage = 40;
        exec.AlterMeleeDamageFrom(attacker, ref damage);
        _out.WriteLine($"attacker hits {before} -> {attacker.Hits} after a 40-point hit on the executioner");
        Assert.Equal(40, damage);
        Assert.Equal(before - 20, attacker.Hits);
        exec.Delete();

        // Every death drops a SerpentFangKey; a badge drops at the parent's 30% and then this file's 50%.
        var keys = 0;
        var badges = 0;
        for (var i = 0; i < 20; i++)
        {
            var e = new SerpentsFangHighExecutioner();
            var corpse = KillAndGetCorpse(e, new Point3D(1000 + i, 1000, 0));
            var key = corpse.FindItemByType<SerpentFangKey>();
            if (key != null)
            {
                keys++;
                Assert.Equal(604800, key.TimeLeft);
                Assert.Equal(LootType.Blessed, key.LootType);
            }

            if (corpse.FindItemByType<SerpentFangSectBadge>() != null)
            {
                badges++;
            }

            corpse.Delete();
        }

        _out.WriteLine($"20 executioners: {keys} keys, {badges} with a badge (ServUO 100% / 65%)");
        Assert.Equal(20, keys);
        Assert.InRange(badges, 5, 20);

        attacker.Delete();
    }

    [Fact]
    public void TheCrystalHydraDropsStockFragmentsAndItsKey()
    {
        var hydra = new CrystalHydra();
        Assert.Equal(AIType.AI_Melee, hydra.AI);
        Assert.Equal(0x16A, hydra.BaseSoundID);
        Assert.Equal(40, hydra.Hides);
        Assert.Equal(19, hydra.Meat);
        Assert.Equal(5, hydra.TreasureMapLevel);
        Assert.InRange(hydra.HitsMax, 1450, 1500);
        Assert.InRange(hydra.ColdResistance, 80, 100);
        Assert.True(hydra.Skills.Wrestling.Value >= 100.0);
        hydra.Delete();

        // ServUO's re-rolled loop: 0 arcanist scrolls about half the time, otherwise one or more.
        var withScroll = 0;
        for (var i = 0; i < 100; i++)
        {
            var h = new CrystalHydra();
            if (h.Backpack?.Items.Any(it => it is SpellScroll) == true)
            {
                withScroll++;
            }

            h.Delete();
        }

        _out.WriteLine($"100 hydras: {withScroll} carry an arcanist scroll (ServUO ~50%)");
        Assert.InRange(withScroll, 25, 75);

        // CrystallineFragments is stock ModernUO (Items/Misc/Prism of Light/CrystallineFragments.cs); the hydra drops
        // that type, not a re-port. Every corpse has one; a quarter have the key.
        Assert.NotNull(typeof(CrystallineFragments).Assembly.GetType("Server.Items.CrystallineFragments"));
        var fragments = 0;
        var crystals = 0;
        for (var i = 0; i < 60; i++)
        {
            var corpse = KillAndGetCorpse(new CrystalHydra(), new Point3D(1000 + i, 1002, 0));
            var f = corpse.FindItemByType<CrystallineFragments>();
            if (f != null)
            {
                fragments++;
                Assert.Equal(0x223B, f.ItemID);
                Assert.Equal(0x47E, f.Hue);
                Assert.Equal(LootType.Blessed, f.LootType);
            }

            var c = corpse.FindItemByType<ShatteredCrystals>();
            if (c != null)
            {
                crystals++;
                Assert.Equal(604800, c.TimeLeft);
            }

            corpse.Delete();
        }

        _out.WriteLine($"60 hydras killed: {fragments} fragments, {crystals} shattered crystals (ServUO 100% / 25%)");
        Assert.Equal(60, fragments);
        Assert.InRange(crystals, 4, 30);
    }

    [Fact]
    public void TheFairyDragonRunsMageAIAndDropsItsWingAndOrb()
    {
        var dragon = new FairyDragon();
        Assert.Equal(AIType.AI_Mage, dragon.AI); // D-41: ServUO AI_Mystic
        Assert.True(dragon.Skills.Mysticism.Value >= 101.8);
        Assert.Equal(0.0, dragon.Skills.Magery.Value);
        Assert.Same(Poison.Greater, dragon.HitPoison);
        Assert.Equal(0.75, dragon.HitPoisonChance);
        Assert.True(dragon.AutoDispel);
        Assert.Equal(3, dragon.TreasureMapLevel);
        Assert.Equal(9, dragon.Meat);
        Assert.Equal(FoodType.Meat, dragon.FavoriteFood);
        Assert.Equal(39, dragon.VirtualArmor);
        Assert.Equal(362, dragon.BaseSoundID);
        Assert.Equal(1513, dragon.GetAttackSound());
        Assert.Equal(1558, dragon.GetAngerSound());
        Assert.Equal(1516, dragon.GetIdleSound());
        Assert.InRange(dragon.HitsMax, 398, 403);
        dragon.Controlled = true;
        Assert.False(dragon.AutoDispel);
        dragon.Delete();

        var wings = 0;
        var orbs = 0;
        for (var i = 0; i < 100; i++)
        {
            var corpse = KillAndGetCorpse(new FairyDragon(), new Point3D(1000 + i, 1004, 0));
            var wing = corpse.FindItemByType<FairyDragonWing>();
            if (wing != null)
            {
                wings++;
                Assert.Equal(0x1084, wing.ItemID);
                Assert.Equal(1111, wing.Hue);
                Assert.True(wing.Stackable);
                Assert.Equal(1, wing.Amount);
                Assert.Equal(1112899, wing.LabelNumber);
            }

            var orb = corpse.FindItemByType<DraconicOrb>();
            if (orb != null)
            {
                orbs++;
                Assert.Equal(43200, orb.TimeLeft);
            }

            corpse.Delete();
        }

        _out.WriteLine($"100 fairy dragons killed: {wings} wings, {orbs} draconic orbs (ServUO 25% / 10%)");
        Assert.InRange(wings, 10, 45);
        Assert.InRange(orbs, 2, 25);

        Assert.Equal(3, new FairyDragonWing(3).Amount);
    }

    [Fact]
    public void TheChickenLizardLaysForItsBondedOwnerAndPersistsTheCooldown()
    {
        // Core.Now sits at DateTime.MinValue in the test host until the route's clock is armed (Q-029); the cooldown
        // arithmetic below subtracts days from it. This fact failed in isolation and passed in the suite before this
        // line, which is the order dependence Arm() exists to remove.
        ShardTestClock.Arm();

        // The persisted cooldown: NextEgg is a [SerializableField], as ServUO's version-1 DateTime is.
        var field = typeof(ChickenLizard).GetField("_nextEgg", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(field);
        Assert.Single(field!.GetCustomAttributes(typeof(SerializableFieldAttribute), false));

        var lizard = new ChickenLizard();
        Assert.True(lizard.Tamable);
        Assert.Equal(1, lizard.ControlSlots);
        Assert.Equal(0.0, lizard.MinTameSkill);
        Assert.Equal(MeatType.Bird, lizard.MeatType);
        Assert.Equal(3, lizard.Meat);
        Assert.Equal(FoodType.Meat, lizard.FavoriteFood);
        Assert.Equal(AIType.AI_Animal, lizard.AI);
        Assert.Equal(1511, lizard.GetIdleSound());
        Assert.Equal(DateTime.MinValue, lizard.NextEgg);

        // 5% of wild lizards carry an egg.
        var withEgg = 0;
        for (var i = 0; i < 400; i++)
        {
            var l = new ChickenLizard();
            if (l.Backpack?.FindItemByType<ChickenLizardEgg>() != null)
            {
                withEgg++;
            }

            l.Delete();
        }

        _out.WriteLine($"400 wild chicken lizards: {withEgg} carry an egg (ServUO 5%)");
        Assert.InRange(withEgg, 5, 45);

        var master = NewPlayer(new Point3D(1000, 1006, 0));
        lizard.MoveToWorld(new Point3D(1001, 1006, 0), Map.TerMur);
        lizard.Controlled = true;
        lizard.ControlMaster = master;
        Assert.True(lizard.IsBondable);

        // The bonding feed: the base bonds it and this override arms NextEgg one day out, laying nothing yet.
        lizard.BondingBegin = Core.Now - lizard.BondingDelay - TimeSpan.FromMinutes(1);
        Assert.False(lizard.IsBonded);
        var fed = lizard.CheckFeed(master, new Ribs());
        Assert.True(fed);
        Assert.True(lizard.IsBonded);
        Assert.InRange(lizard.NextEgg, Core.Now + TimeSpan.FromDays(1) - TimeSpan.FromMinutes(1), Core.Now + TimeSpan.FromDays(1) + TimeSpan.FromMinutes(1));
        Assert.DoesNotContain(World.Items.Values.OfType<ChickenLizardEgg>(), e => e.Map == Map.TerMur && e.Location == master.Location);

        // Once the day is up, each feed rolls 50% for an egg and re-arms the cooldown a week out either way, so the
        // field is reset between feeds here to observe the roll. ServUO drops the egg at the owner's FEET when the
        // pack accepts it (the inverted TryDropItem test, copied faithfully), never into the pack.
        var laid = 0;
        for (var i = 0; i < 40; i++)
        {
            field.SetValue(lizard, DateTime.MinValue);
            Assert.True(lizard.CheckFeed(master, new Ribs()));
            Assert.InRange(lizard.NextEgg, Core.Now + TimeSpan.FromDays(7) - TimeSpan.FromMinutes(1), Core.Now + TimeSpan.FromDays(7) + TimeSpan.FromMinutes(1));
            var onGround = World.Items.Values.OfType<ChickenLizardEgg>().Count(e => e.Map == Map.TerMur && e.Location == master.Location && e.Parent == null);
            if (onGround > laid)
            {
                laid = onGround;
            }
        }

        _out.WriteLine($"40 feeds after the cooldown: {laid} eggs laid at the owner's feet (ServUO 50%), none in the pack");
        Assert.InRange(laid, 8, 32);
        Assert.Null(master.Backpack!.FindItemByType<ChickenLizardEgg>());

        // A feed inside the cooldown lays nothing.
        Assert.True(lizard.CheckFeed(master, new Ribs()));
        Assert.Equal(laid, World.Items.Values.OfType<ChickenLizardEgg>().Count(e => e.Map == Map.TerMur && e.Location == master.Location && e.Parent == null));

        foreach (var e in World.Items.Values.OfType<ChickenLizardEgg>().Where(e => e.Location == master.Location).ToList())
        {
            e.Delete();
        }

        lizard.Delete();
        master.Delete();
    }

    [Fact]
    public void TheEggIncubatesWatersMutatesAndHatches()
    {
        ShardTestClock.Arm(); // Core.Now arithmetic below; see the lizard fact

        var pm = NewPlayer(new Point3D(1000, 1008, 0));
        var pitcher = new Pitcher(BeverageType.Water);
        pm.AddToBackpack(pitcher);
        Assert.Equal(5, pitcher.Quantity);

        var egg = new ChickenLizardEgg();
        pm.AddToBackpack(egg);
        Assert.Equal(0x41BD, egg.ItemID);
        Assert.Equal(EggStage.New, egg.Stage);
        Assert.Equal(Dryness.Moist, egg.Dryness);
        Assert.Equal(1112462, egg.LabelNumber);
        Assert.False(egg.Incubating);
        Assert.Equal(TimeSpan.Zero, egg.TotalIncubationTime);

        // A New egg needs no water.
        egg.Pour(pm, pitcher);
        Assert.Equal(5, pitcher.Quantity);
        Assert.Equal(0, egg.WaterLevel);

        // Incubating accumulates time when it stops; lifting the egg stops it.
        egg.IncubationStart = Core.Now - TimeSpan.FromHours(2);
        egg.Incubating = true;
        egg.OnItemLifted(pm, egg);
        Assert.False(egg.Incubating);
        Assert.InRange(egg.TotalIncubationTime, TimeSpan.FromHours(2) - TimeSpan.FromMinutes(1), TimeSpan.FromHours(2) + TimeSpan.FromMinutes(1));

        // 24 h: New -> Stage1 (0x41BE); unwatered it is Dry, one pour makes it Moist.
        egg.TotalIncubationTime = TimeSpan.FromHours(25);
        egg.CheckStatus();
        Assert.Equal(EggStage.Stage1, egg.Stage);
        Assert.Equal(0x41BE, egg.ItemID);
        Assert.Equal(Dryness.Dry, egg.Dryness);
        Assert.Equal(1112463, egg.LabelNumber);
        egg.Pour(pm, pitcher);
        Assert.Equal(4, pitcher.Quantity);
        Assert.Equal(1, egg.WaterLevel);
        Assert.Equal(Dryness.Moist, egg.Dryness);
        egg.Pour(pm, pitcher); // "doesn't need it"
        Assert.Equal(4, pitcher.Quantity);

        // 48 h: Stage1 -> Stage2 (0x41BF), watered so no burn roll; pour again.
        egg.TotalIncubationTime = TimeSpan.FromHours(49);
        egg.CheckStatus();
        Assert.Equal(EggStage.Stage2, egg.Stage);
        Assert.Equal(0x41BF, egg.ItemID);
        egg.Pour(pm, pitcher);
        Assert.Equal(2, egg.WaterLevel);
        Assert.Equal(3, pitcher.Quantity);

        // 72 h: Stage2 -> Mature. The stage increments before the dryness is read, so a fully watered egg is Dry
        // (2 water against stage 3) at the roll and the 10% "Moist" branch is unreachable: the best a player gets
        // is 5%. ServUO's; copied. Mature is hue 555, or a hiryu hue with IsBattleChicken set.
        egg.TotalIncubationTime = TimeSpan.FromHours(73);
        egg.CheckStatus();
        Assert.Equal(EggStage.Mature, egg.Stage);
        Assert.Equal(0x41BF, egg.ItemID);
        Assert.Equal(Dryness.Dry, egg.Dryness);
        Assert.Equal(egg.IsBattleChicken ? 1112468 : 1112467, egg.LabelNumber);
        Assert.True(egg.IsBattleChicken ? egg.Hue != 555 : egg.Hue == 555, $"mature egg hue {egg.Hue}, battle={egg.IsBattleChicken}");

        // 120 h at Mature: burnt (hue 2026), and it will not take water.
        var burnt = new ChickenLizardEgg { TotalIncubationTime = TimeSpan.FromHours(121), Stage = EggStage.Mature };
        burnt.CheckStatus();
        Assert.Equal(EggStage.Burnt, burnt.Stage);
        Assert.Equal(2026, burnt.Hue);
        Assert.Equal(1112466, burnt.LabelNumber);
        burnt.Pour(pm, pitcher);
        Assert.Equal(3, pitcher.Quantity);
        burnt.Delete();

        // The mutation roll over many watered eggs lands near 5% (Dry); an unwatered egg that survives its two
        // burn rolls is Dehydrated at the roll and never a battle chicken.
        var battle = 0;
        var maturedDry = 0;
        for (var i = 0; i < 400; i++)
        {
            var e = new ChickenLizardEgg();
            e.TotalIncubationTime = TimeSpan.FromHours(25);
            e.CheckStatus();
            e.Pour(pm, pitcher);
            pitcher.Quantity = 5;
            e.TotalIncubationTime = TimeSpan.FromHours(49);
            e.CheckStatus();
            e.Pour(pm, pitcher);
            pitcher.Quantity = 5;
            e.TotalIncubationTime = TimeSpan.FromHours(73);
            e.CheckStatus();
            Assert.Equal(EggStage.Mature, e.Stage);
            maturedDry++;
            if (e.IsBattleChicken)
            {
                battle++;
                Assert.NotEqual(555, e.Hue);
            }

            e.Delete();
        }

        var maturedDehydrated = 0;
        var burntUnwatered = 0;
        for (var i = 0; i < 200; i++)
        {
            var e = new ChickenLizardEgg();
            e.TotalIncubationTime = TimeSpan.FromHours(25);
            e.CheckStatus();
            e.TotalIncubationTime = TimeSpan.FromHours(49);
            e.CheckStatus();
            if (e.Stage == EggStage.Stage2)
            {
                e.TotalIncubationTime = TimeSpan.FromHours(73);
                e.CheckStatus();
            }

            if (e.Stage == EggStage.Mature)
            {
                maturedDehydrated++;
                Assert.False(e.IsBattleChicken);
                Assert.Equal(555, e.Hue);
            }
            else
            {
                Assert.Equal(EggStage.Burnt, e.Stage);
                burntUnwatered++;
            }

            e.Delete();
        }

        // The unwatered survival rate is 25%, not 12.5%: at the 48 h check an unwatered egg is stage 1 with water 0,
        // Dryness 1 = Dry, below Parched, so the 50% burn roll never fires; only the 72 h roll (Dehydrated, 75% burn)
        // does. This fact was written as 12.5% with a range wide enough to pass most runs, and went red at 57 of 200
        // in the CC6 follow-up (build A6); corrected there to the implementation's 25%, n=200, +-3.6 sigma.
        _out.WriteLine($"400 watered eggs matured: {battle} battle chickens (ServUO 5% at Dry); 200 unwatered: {maturedDehydrated} matured, none battle, {burntUnwatered} burnt (ServUO 25% / 75%)");
        Assert.Equal(400, maturedDry);
        Assert.InRange(battle, 5, 50);
        Assert.InRange(maturedDehydrated, 28, 72);

        // Hatching: an immature egg crumbles; a mature battle egg hatches a BattleChickenLizard in the egg's hue at
        // the hatcher's feet; a mature plain egg hatches a ChickenLizard.
        var immature = new ChickenLizardEgg();
        pm.AddToBackpack(immature);
        immature.TryHatchEgg(pm);
        Assert.True(immature.Deleted);

        egg.IsBattleChicken = true;
        egg.Hue = 1173;
        egg.TryHatchEgg(pm);
        Assert.True(egg.Deleted);
        var hatched = World.Mobiles.Values.OfType<BattleChickenLizard>().Where(b => b.Location == pm.Location && b.Map == Map.TerMur).ToList();
        Assert.Single(hatched);
        Assert.Equal(1173, hatched[0].Hue);
        Assert.Equal("a battle chicken lizard", hatched[0].Name);
        Assert.True(hatched[0].Tamable);
        Assert.Equal(FoodType.GrainsAndHay, hatched[0].FavoriteFood);
        Assert.Equal(MeatType.Bird, hatched[0].MeatType);

        var plain = new ChickenLizardEgg { Stage = EggStage.Mature };
        pm.AddToBackpack(plain);
        plain.TryHatchEgg(pm);
        Assert.True(plain.Deleted);
        Assert.Single(World.Mobiles.Values.OfType<ChickenLizard>(), b => b.Location == pm.Location && b.Map == Map.TerMur);

        // A wild battle chicken lizard is a coward: acquiring a combatant makes it flee 30 s (95%), and its own
        // CheckFlee reads the timer. Tamed, it uses the base and stands.
        var wild = hatched[0];
        var fled = 0;
        var stoodItsGround = 0;
        for (var i = 0; i < 60; i++)
        {
            wild.StopFlee();
            wild.Combatant = pm;
            if (wild.CheckFlee())
            {
                fled++;
                Assert.InRange(wild.EndFleeTime, Core.Now + TimeSpan.FromSeconds(29), Core.Now + TimeSpan.FromSeconds(31));
            }
            else
            {
                stoodItsGround++;
            }
        }

        _out.WriteLine($"60 combatant acquisitions by a wild battle chicken lizard: fled {fled}, stood {stoodItsGround} (ServUO 95% / 5%)");
        Assert.InRange(fled, 45, 60);

        wild.StopFlee();
        wild.Controlled = true;
        wild.ControlMaster = pm;
        wild.Combatant = pm;
        Assert.False(wild.CheckFlee());
        Assert.Equal(DateTime.MinValue, wild.EndFleeTime);

        foreach (var m in World.Mobiles.Values.Where(m => m is ChickenLizard or BattleChickenLizard).ToList())
        {
            m.Delete();
        }

        pm.Delete();
    }
}
