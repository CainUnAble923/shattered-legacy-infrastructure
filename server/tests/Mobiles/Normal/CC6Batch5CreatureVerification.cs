// CC6 batch 5: the Renowned row. Seventeen creatures chosen by batch 1's rule - pinned ModernUO's own spawn data
// (Distribution/Data/Spawns) places every one of them and no loaded assembly declared them: the thirteen Stygian Abyss
// "[Renowned]" creatures on BaseRenowned (post-uoml/termur/Abyss.json) and the four Labyrinth named (Flurry, Mistral,
// Tempest on BaseCreature, Grim on stock Drake; shared/malas/Labyrinth.json, lower-case). Five Stygian Abyss artifacts
// travel with them because the renowned hand them out and nothing else on the shard declared them: LegacyOfDespair,
// SwordOfShatteredHopes, TheImpalersPick (on their stock weapon parents), MantleOfTheFallen and SummonersKilt (on
// BaseSetClothing, the SpinedBloodwormBracers shape). The other 22 artifact types the thirteen name are CC9's.
//
// This file proves the batch does what it was chosen for: every one constructs with its ServUO values and resolves by
// its spawn-data spelling, every stock spawn entry naming one of them now resolves, BaseRenowned is the thirteen's final
// parent with each child a generated subclass, and - the fact that matters most - a renowned creature that dies hands
// one artifact to the player who did the most damage, at ServUO's rates, into that player's pack. The skeletal dragon
// breathes cold and Grim breathes fire through Drake, because pinned ModernUO has both breaths under other names.
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
public class CC6Batch5CreatureVerification
{
    private readonly ITestOutputHelper _out;

    public CC6Batch5CreatureVerification(ITestOutputHelper output) => _out = output;

    // (type, spawn-data spelling, ServUO Name, ServUO CorpseName, Body (-1: random 60/61), Hue, Fame, Karma), verbatim
    // from the ServUO files. The Labyrinth names the four in lower case; FindTypeByName ignores case and so must this.
    private static readonly (Type Type, string SpawnName, string Name, string Corpse, int Body, int Hue, int Fame, int Karma)[] Batch =
    {
        (typeof(FireDaemonRenowned), "FireDaemonRenowned", "Fire Daemon", "Fire Daemon [Renowned] corpse", 40, 243, 7000, -10000),
        (typeof(AcidElementalRenowned), "AcidElementalRenowned", "Acid Elemental", "Acid Elemental [Renowned] corpse", 0x9E, 0, 12500, -12500),
        (typeof(AncientLichRenowned), "AncientLichRenowned", "Ancient Lich", "Ancient Lich [Renowned] corpse", 78, 0, 23000, -23000),
        (typeof(DevourerRenowned), "DevourerRenowned", "Devourer of Souls", "Devourer of Souls [Renowned] corpse", 303, 0, 9500, -9500),
        (typeof(FireElementalRenowned), "FireElementalRenowned", "Fire Elemental", "Fire Elemental [Renowned] corpse", 15, 1161, 4500, -4500),
        (typeof(GrayGoblinMageRenowned), "GrayGoblinMageRenowned", "Gray Goblin Mage", "Gray Goblin Mage [Renowned] corpse", 723, 1900, 1500, -1500),
        (typeof(GreenGoblinAlchemistRenowned), "GreenGoblinAlchemistRenowned", "Green Goblin Alchemist", "Green Goblin Alchemist [Renowned] corpse", 723, 0, 1500, -1500),
        (typeof(PixieRenowned), "PixieRenowned", "Pixie", "Pixie [Renowned] corpse", 128, 0, 7000, 7000),
        (typeof(RakktaviRenowned), "RakktaviRenowned", "Rakktavi", "Rakktavi [Renowned] corpse", 0x8E, 0, 6500, -6500),
        (typeof(SkeletalDragonRenowned), "SkeletalDragonRenowned", "Skeletal Dragon", "Skeletal Dragon [Renowned] corpse", 104, 906, 22500, -22500),
        (typeof(TikitaviRenowned), "TikitaviRenowned", "Tikitavi", "Tikitavi [Renowned] corpse", 42, 0, 1500, -1500),
        (typeof(VitaviRenowned), "VitaviRenowned", "Vitavi", "Vitavi [Renowned] corpse", 0x8F, 0, 7500, -7500),
        (typeof(WyvernRenowned), "WyvernRenowned", "Wyvern", "Wyvern [Renowned] corpse", 62, 243, 24000, -24000),
        (typeof(Flurry), "flurry", "Flurry", "The remains of Flurry", 13, 3, 4500, -4500),
        (typeof(Mistral), "mistral", "Mistral", "a mistral corpse", 13, 924, 4500, -4500),
        (typeof(Tempest), "tempest", "Tempest", "the remains of Tempest", 13, 1175, 4500, -4500),
        (typeof(Grim), "grim", "Grim", "the remains of Grim", -1, 1744, 17500, -5500)
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

    // Everything in a player's pack that is not the pack itself, tallied by type and removed, so a pack never fills.
    private static Dictionary<string, int> TakeAll(PlayerMobile pm, Dictionary<string, int> tally)
    {
        foreach (var item in pm.Backpack!.Items.ToList())
        {
            tally[item.GetType().Name] = tally.GetValueOrDefault(item.GetType().Name) + 1;
            item.Delete();
        }

        return tally;
    }

    [Fact]
    public void AllSeventeenConstructWithTheirServUOValues()
    {
        Assert.Equal(17, Batch.Length);

        foreach (var (type, spawnName, name, corpse, body, hue, fame, karma) in Batch)
        {
            var bc = (BaseCreature)Activator.CreateInstance(type)!;

            _out.WriteLine($"{type.Name}: name='{bc.Name}' title='{bc.Title}' body={(int)bc.Body} hue={bc.Hue} fame={bc.Fame} " +
                           $"karma={bc.Karma} hits={bc.HitsMax} ai={bc.AI} range={bc.RangePerception} speed={bc.ActiveSpeed}/{bc.PassiveSpeed}");

            Assert.Equal(name, bc.Name);
            Assert.Equal(corpse, bc.CorpseName);

            if (body >= 0)
            {
                Assert.Equal(body, (int)bc.Body);
            }
            else
            {
                Assert.True((int)bc.Body is 60 or 61, $"{type.Name} body {(int)bc.Body} is not Drake's 60/61"); // Grim
            }

            Assert.Equal(hue, bc.Hue);
            Assert.Equal(fame, bc.Fame);
            Assert.Equal(karma, bc.Karma);
            Assert.True(bc.HitsMax > 0, $"{type.Name} has no hits");

            if (bc is BaseRenowned)
            {
                // ServUO: base(aiType, mode, 18, ...). 18 is not rewritten to 16 there (port-recipe.md), so it is carried.
                Assert.Equal("[Renowned]", bc.Title);
                Assert.Equal(18, bc.RangePerception);
            }
            else
            {
                Assert.Null(bc.Title);
                Assert.Equal(16, bc.RangePerception); // ServUO passes 10, which it rewrites to 16
            }

            // Q-008: every ported creature takes ModernUO's Medium fallback; none of these overrides it.
            Assert.Equal(0.25, bc.ActiveSpeed);
            Assert.Equal(0.5, bc.PassiveSpeed);

            Assert.Same(type, AssemblyHandler.FindTypeByName(spawnName));

            bc.Delete();
        }
    }

    [Fact]
    public void EveryStockSpawnEntryNamingOneOfTheSeventeenNowResolves()
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
            Assert.True(entries.TryGetValue(spawnName, out var count) && count > 0,
                $"{type.Name} is not named by any stock spawn entry; it was chosen because it is");
            Assert.DoesNotContain(spawnName, unresolved, StringComparer.OrdinalIgnoreCase);
            referenced += count;
            _out.WriteLine($"{type.Name}: {count} spawn entries as '{spawnName}'");
        }

        // Batch 4 left exactly 58 names unresolved (tools/spawn-orphans.txt at 8c87efe). The seventeen here were among
        // them, and nothing else in this batch adds or removes a spawnable type (the five artifacts are items no
        // spawner names), so the count must be exactly 41.
        Assert.Equal(41, unresolved.Count);
        // Counted 2026-09-20 from the same files with tools/spawn_orphans.py's reader: FireDaemonRenowned 2 (two Abyss
        // spawners), the other twelve renowned 1 each (Abyss), flurry/grim/mistral/tempest 1 each (one Labyrinth spawner).
        Assert.Equal(18, referenced);
    }

    [Fact]
    public void BaseRenownedIsTheThirteensFinalParentAndEveryArtifactTheyNameConstructs()
    {
        Assert.True(typeof(BaseRenowned).IsAbstract);
        Assert.True(typeof(BaseRenowned).IsSubclassOf(typeof(BaseCreature)));
        Assert.Single(typeof(BaseRenowned).GetCustomAttributes(typeof(SerializationGeneratorAttribute), false));

        var renowned = Batch.Where(b => b.Type.IsSubclassOf(typeof(BaseRenowned))).Select(b => b.Type).ToList();
        Assert.Equal(13, renowned.Count);

        var named = new HashSet<Type>();

        foreach (var type in renowned)
        {
            // Batch 4's subclass rule, checked on the type: the attribute is declared on the child (not inherited) and
            // the generator emitted this level's own (Serial) constructor.
            Assert.Single(type.GetCustomAttributes(typeof(SerializationGeneratorAttribute), false));
            var serialCtor = type.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(Serial) }, null);
            Assert.NotNull(serialCtor);
            Assert.Same(type, serialCtor!.DeclaringType);

            var bc = (BaseRenowned)Activator.CreateInstance(type)!;
            Assert.False(bc.NoGoodies);
            Assert.True(bc.UniqueSAList.Length + bc.SharedSAList.Length > 0, $"{type.Name} names no artifact at all");
            named.UnionWith(bc.UniqueSAList);
            named.UnionWith(bc.SharedSAList);
            bc.Delete();
        }

        // The thirteen name 27 distinct artifact types between their two lists; 22 are CC9's and 5 are this batch's.
        // Every one constructs through the same Loot.Construct the hand-out uses, which is also where a wrong spelling
        // of ResonantStaffofEnlightenment or TokenOfHolyFavor would have failed to compile.
        _out.WriteLine($"{named.Count} distinct artifact types named: {string.Join(", ", named.Select(t => t.Name).OrderBy(n => n))}");
        Assert.Equal(27, named.Count);
        Assert.Contains(typeof(ResonantStaffofEnlightenment), named);
        Assert.Contains(typeof(TokenOfHolyFavor), named);

        foreach (var t in named)
        {
            var item = Loot.Construct(t);
            Assert.NotNull(item);
            Assert.IsType(t, item);
            item!.Delete();
        }
    }

    [Fact]
    public void TheArtifactGoesToThePlayerWhoDidTheMostDamage()
    {
        ShardTestClock.Arm(); // RegisterDamage stamps Core.Now on the damage entries

        var heavy = NewPlayer(new Point3D(1000, 1000, 0));
        var light = NewPlayer(new Point3D(1001, 1000, 0));
        var pet = new Drake();
        pet.MoveToWorld(new Point3D(1003, 1000, 0), Map.TerMur);

        // Driven directly: the weighted draw. ServUO's table is keyed by player and the roll is proportional to damage.
        var renowned = new AcidElementalRenowned();
        renowned.MoveToWorld(new Point3D(1002, 1000, 0), Map.TerMur);
        renowned.RegisterDamage(heavy, 3000);
        renowned.RegisterDamage(light, 100);
        renowned.RegisterDamage(pet, 5000); // not a player: ignored

        var heavyGot = 0;
        var lightGot = 0;
        var nobody = 0;
        for (var i = 0; i < 100; i++)
        {
            var artifact = new AxeOfAbandon();
            renowned.AwardArtifact(artifact);
            Assert.False(artifact.Deleted);
            if (artifact.RootParent == heavy)
            {
                heavyGot++;
            }
            else if (artifact.RootParent == light)
            {
                lightGot++;
            }
            else
            {
                // ServUO's recipient test is strict (running total > roll), so a roll equal to the eligible total
                // (1 in 3100 here) hands the artifact to nobody and leaves it constructed. Copied; build C of this
                // batch went red on it once in 100 draws. Pinned deterministically below.
                nobody++;
            }

            artifact.Delete();
        }

        _out.WriteLine($"100 awards with damage 3000 : 100 : (pet 5000, ignored): heavy {heavyGot}, light {lightGot}, nobody {nobody} (ServUO 96.8% / 3.2% / 0.03%)");
        Assert.InRange(heavyGot, 85, 100);
        Assert.InRange(nobody, 0, 3);
        Assert.Equal(100, heavyGot + lightGot + nobody);

        // The strict roll, deterministically: a lone damager who did exactly 1 damage rolls RandomMinMax(1, 1) = 1,
        // and 1 > 1 is false, so ServUO never gives that artifact to anyone. It is not deleted either.
        var one = new AcidElementalRenowned();
        one.MoveToWorld(new Point3D(1002, 1000, 0), Map.TerMur);
        one.RegisterDamage(heavy, 1);
        for (var i = 0; i < 20; i++)
        {
            var artifact = new AxeOfAbandon();
            one.AwardArtifact(artifact);
            Assert.Null(artifact.RootParent);
            Assert.False(artifact.Deleted);
            artifact.Delete();
        }

        one.Delete();

        // Nobody a player: nothing is given and the artifact is left where it was constructed, not deleted (ServUO's).
        var orphan = new AcidElementalRenowned();
        orphan.RegisterDamage(pet, 5000);
        var unclaimed = new AxeOfAbandon();
        orphan.AwardArtifact(unclaimed);
        Assert.False(unclaimed.Deleted);
        Assert.Null(unclaimed.RootParent);
        unclaimed.Delete();
        orphan.Delete();

        // A pack that cannot take it: GiveArtifact deletes the artifact (ServUO's, no corpse fallback).
        var full = NewPlayer(new Point3D(1004, 1000, 0));
        for (var i = 0; i < full.Backpack!.MaxItems; i++)
        {
            full.Backpack.DropItem(new Candle());
        }

        var refused = new AxeOfAbandon();
        renowned.GiveArtifact(full, refused);
        Assert.True(refused.Deleted);
        Assert.Null(full.Backpack.FindItemByType<AxeOfAbandon>());

        // Real kills through OnBeforeDeath: the creature's own DamageEntries feed the table. One damager in range gets
        // the artifact in the PACK, never on the corpse, at 5% unique + 10% shared (AcidElementalRenowned: 2 unique
        // types, 1 shared).
        var tally = new Dictionary<string, int>();
        var bestowed = 0;
        for (var i = 0; i < 300; i++)
        {
            var bc = new AcidElementalRenowned();
            bc.MoveToWorld(new Point3D(1002, 1000, 0), Map.TerMur);
            bc.Damage(60, heavy);
            Assert.NotEmpty(bc.DamageEntries);
            bc.Kill();
            var corpse = bc.Corpse;
            Assert.NotNull(corpse);
            Assert.Null(corpse!.FindItemByType<BreastplateOfTheBerserker>());
            Assert.Null(corpse.FindItemByType<TerathanWarriorCostume>());
            Assert.Null(corpse.FindItemByType<MysticsGarb>());
            corpse.Delete();
            var before = tally.Values.Sum();
            TakeAll(heavy, tally);
            bestowed += tally.Values.Sum() - before;
        }

        var unique = tally.GetValueOrDefault("BreastplateOfTheBerserker") + tally.GetValueOrDefault("TerathanWarriorCostume");
        var shared = tally.GetValueOrDefault("MysticsGarb");
        _out.WriteLine($"300 real kills by one player in range: {bestowed} artifacts bestowed (ServUO 15%): {unique} unique, {shared} shared (ServUO 1:2): " +
                       string.Join(", ", tally.Select(kv => $"{kv.Key}={kv.Value}")));
        Assert.InRange(bestowed, 25, 70);
        Assert.Equal(bestowed, unique + shared);
        Assert.True(unique > 0 && shared > 0, "both lists should have been drawn from");

        // NoKillAwards (a summoned or GM-flagged creature) hands out nothing.
        for (var i = 0; i < 40; i++)
        {
            var bc = new AcidElementalRenowned { NoKillAwards = true };
            bc.MoveToWorld(new Point3D(1002, 1000, 0), Map.TerMur);
            bc.Damage(60, heavy);
            bc.Kill();
            bc.Corpse?.Delete();
        }

        Assert.Empty(heavy.Backpack!.Items);

        // ServUO's quirk, copied: eligibility (alive, within 32 tiles, room in the pack) only sizes the roll; the
        // recipient loop walks the whole table, so a lone damager 40 tiles away still receives the artifact.
        var far = NewPlayer(new Point3D(1040, 1000, 0));
        var farTally = new Dictionary<string, int>();
        for (var i = 0; i < 200; i++)
        {
            var bc = new AcidElementalRenowned();
            bc.MoveToWorld(new Point3D(1000, 1000, 0), Map.TerMur);
            Assert.False(bc.IsEligible(far, new AxeOfAbandon()));
            bc.Damage(60, far);
            bc.Kill();
            bc.Corpse?.Delete();
            TakeAll(far, farTally);
        }

        var farGot = farTally.Values.Sum();
        _out.WriteLine($"200 real kills by a player 40 tiles away (ineligible): {farGot} artifacts still bestowed (ServUO: the same 15%)");
        Assert.InRange(farGot, 12, 50);

        renowned.Delete();
        pet.Delete();
        heavy.Delete();
        light.Delete();
        full.Delete();
        far.Delete();
    }

    [Fact]
    public void TheSkeletalDragonBreathesColdAndGrimBreathesFireThroughDrake()
    {
        // ServUO: SetSpecialAbility(DragonBreath), which for SkeletalDragonRenowned resolves to the cold definition
        // (hue 0x480). Pinned ModernUO has it as MonsterAbilities.ColdBreath, the same thing stock SkeletalDragon uses.
        var dragon = new SkeletalDragonRenowned();
        var breath = Assert.Single(dragon.GetMonsterAbilities()!);
        var cold = Assert.IsType<ColdBreath>(breath);
        Assert.Equal(100, cold.ColdDamage);
        Assert.Equal(0, cold.FireDamage);
        Assert.Equal(0x480, cold.BreathEffectHue);
        Assert.Equal(TimeSpan.FromSeconds(30), cold.MinTriggerCooldown);
        Assert.Equal(TimeSpan.FromSeconds(45), cold.MaxTriggerCooldown);
        Assert.True(dragon.ReacquireOnMovement);
        Assert.True(dragon.AutoDispel);
        Assert.Same(Poison.Lethal, dragon.PoisonImmune);
        Assert.Equal(19, dragon.Meat);
        Assert.Equal(20, dragon.Hides);
        Assert.Equal(HideType.Barbed, dragon.HideType);
        Assert.Equal(3.0, dragon.BonusPetDamageScalar); // Core.SE
        dragon.Delete();

        // Grim is a generated subclass of STOCK Drake: the attribute on Grim itself, its own (Serial) constructor, and
        // Drake's fire breath, tameability values and reagents inherited; Grim then turns taming off.
        var grim = new Grim();
        Assert.IsAssignableFrom<Drake>(grim);
        Assert.Single(typeof(Grim).GetCustomAttributes(typeof(SerializationGeneratorAttribute), false));
        Assert.Single(typeof(Drake).GetCustomAttributes(typeof(SerializationGeneratorAttribute), false));
        var serialCtor = typeof(Grim).GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(Serial) }, null);
        Assert.NotNull(serialCtor);
        Assert.Same(typeof(Grim), serialCtor!.DeclaringType);

        var fire = Assert.Single(grim.GetMonsterAbilities()!);
        Assert.IsType<FireBreath>(fire);
        Assert.Equal(100, fire is FireBreath fb ? fb.FireDamage : -1);
        Assert.Equal(WeaponAbility.CrushingBlow, grim.GetWeaponAbility());
        Assert.False(grim.Tamable);
        Assert.Equal(84.3, grim.MinTameSkill); // Drake's, untouched by ServUO's Grim too
        Assert.Equal(2, grim.ControlSlots);
        Assert.InRange(grim.RawStr, 527, 580);
        Assert.InRange(grim.HitsMax, 1762, 2502);
        Assert.Equal(17, grim.DamageMin);
        Assert.Equal(25, grim.DamageMax);
        Assert.Equal(54, grim.VirtualArmor);
        Assert.True(grim.Skills.Anatomy.Value >= 105.0);
        Assert.True(grim.GivesMLMinorArtifact);
        Assert.Equal(2, grim.TreasureMapLevel);
        Assert.Equal(2, grim.Scales);
        Assert.True(grim.CanFly);
        Assert.NotNull(grim.Backpack!.FindItemByType<BaseReagent>()); // Drake's PackReg(3)
        Assert.Equal(362, grim.BaseSoundID);
        grim.Delete();

        // The three elementals: ML minor artifact givers with the dispel numbers of an elemental. Flurry and Mistral
        // carry an arcanist scroll about half the time (ServUO's re-rolled loop); Tempest has no such loop in ServUO
        // and never does. (Build A of this batch went red on a 180-roll expectation that counted Tempest.)
        var withScroll = 0;
        var tempestScrolls = 0;
        foreach (var t in new[] { typeof(Flurry), typeof(Mistral), typeof(Tempest) })
        {
            for (var i = 0; i < 60; i++)
            {
                var e = (BaseCreature)Activator.CreateInstance(t)!;
                Assert.True(e.GivesMLMinorArtifact);
                Assert.Equal(117.5, e.DispelDifficulty);
                Assert.Equal(45.0, e.DispelFocus);
                Assert.True(e.BleedImmune);
                Assert.Equal(2, e.TreasureMapLevel);
                Assert.Equal(AIType.AI_Mage, e.AI);
                if (e.Backpack?.Items.Any(it => it is SpellScroll) == true)
                {
                    if (t == typeof(Tempest))
                    {
                        tempestScrolls++;
                    }
                    else
                    {
                        withScroll++;
                    }
                }

                e.Delete();
            }
        }

        _out.WriteLine($"120 Flurry/Mistral: {withScroll} carry an arcanist scroll (ServUO ~50%); 60 Tempest: {tempestScrolls} (ServUO 0)");
        Assert.InRange(withScroll, 35, 85);
        Assert.Equal(0, tempestScrolls);

        var tempest = new Tempest();
        Assert.Equal(2, tempest.ControlSlots);
        Assert.False(tempest.Tamable);
        Assert.Equal(602, tempest.HitsMax);
        tempest.Delete();

        // The thirteen's remaining per-file facts that are not on the table above.
        var daemon = new FireDaemonRenowned();
        Assert.Equal(WeaponAbility.ConcussionBlow, daemon.GetWeaponAbility());
        Assert.Equal(AIType.AI_Mage, daemon.AI);
        daemon.Delete();

        var lich = new AncientLichRenowned();
        Assert.Equal(AIType.AI_Mage, lich.AI); // ServUO AI_NecroMage (Q-050)
        Assert.True(lich.Unprovokable);
        Assert.Same(OppositionGroup.FeyAndUndead, lich.OppositionGroup);
        Assert.Equal(0x19D, lich.GetIdleSound());
        Assert.True(lich.Skills.MagicResist.Value >= 175.2);
        lich.Delete();

        var vitavi = new VitaviRenowned();
        Assert.Equal(AIType.AI_Mage, vitavi.AI); // ServUO AI_Mystic (D-41)
        Assert.True(vitavi.Skills.Magery.Value >= 70.1);
        Assert.InRange(vitavi.HitsMax, 45000, 50000);
        Assert.Same(Server.Misc.InhumanSpeech.Ratman, vitavi.SpeechType);
        Assert.True(vitavi.CanRummageCorpses);
        vitavi.Delete();

        var rakktavi = new RakktaviRenowned();
        Assert.Equal(AIType.AI_Archer, rakktavi.AI);
        Assert.Equal(50000, rakktavi.HitsMax);
        Assert.Contains(rakktavi.Items, i => i is Bow);
        Assert.NotNull(rakktavi.Backpack!.FindItemByType<Arrow>());
        rakktavi.Delete();

        var pixie = new PixieRenowned();
        Assert.True(pixie.InitialInnocent);
        Assert.InRange(pixie.RawStr, 1, 380); // ServUO's SetStr(-350, 380), clamped at 1
        Assert.InRange(pixie.RawInt, 700, 8500);
        Assert.InRange(pixie.HitsMax, 9100, 9200);
        Assert.Equal(HideType.Spined, pixie.HideType);
        pixie.Delete();

        var wyvern = new WyvernRenowned();
        Assert.True(wyvern.BardImmune);
        Assert.Same(Poison.Deadly, wyvern.HitPoison);
        Assert.Equal(5, wyvern.TreasureMapLevel);
        Assert.Equal(725, wyvern.GetIdleSound());
        wyvern.Delete();

        var fireElemental = new FireElementalRenowned();
        Assert.Contains(fireElemental.Items, i => i is LightSource);
        Assert.NotNull(fireElemental.Backpack!.FindItemByType<SulfurousAsh>());
        Assert.Equal(117.5, fireElemental.DispelDifficulty);
        fireElemental.Delete();

        var goblin = new GrayGoblinMageRenowned();
        Assert.NotNull(goblin.Backpack!.FindItemByType<ThighBoots>());
        Assert.Equal(0x5FE, goblin.GetDeathSound());
        goblin.Delete();

        var acid = new AcidElementalRenowned();
        Assert.NotNull(acid.Backpack!.FindItemByType<Nightshade>());
        Assert.NotNull(acid.Backpack.FindItemByType<LesserPoisonPotion>());
        Assert.True(acid.BleedImmune);
        acid.Delete();

        var devourer = new DevourerRenowned();
        Assert.Equal(2000, devourer.HitsMax);
        Assert.Equal(3, devourer.Meat);
        Assert.True(devourer.Skills.Necromancy.Value >= 90.1);
        devourer.Delete();
    }

    [Fact]
    public void TheFiveNewArtifactsSitOnTheirFinalParents()
    {
        foreach (var t in new[] { typeof(LegacyOfDespair), typeof(SwordOfShatteredHopes), typeof(TheImpalersPick), typeof(MantleOfTheFallen), typeof(SummonersKilt) })
        {
            Assert.Single(t.GetCustomAttributes(typeof(SerializationGeneratorAttribute), false));
            var serialCtor = t.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(Serial) }, null);
            Assert.NotNull(serialCtor);
            Assert.Same(t, serialCtor!.DeclaringType);
        }

        var legacy = new LegacyOfDespair();
        Assert.IsAssignableFrom<DreadSword>(legacy);
        Assert.Equal(48, legacy.Hue);
        Assert.Equal(1113519, legacy.LabelNumber);
        Assert.Equal(30, legacy.Attributes.WeaponSpeed);
        Assert.Equal(60, legacy.Attributes.WeaponDamage);
        Assert.Equal(50, legacy.WeaponAttributes.HitLowerDefend);
        Assert.Equal(50, legacy.WeaponAttributes.HitLowerAttack);
        Assert.Equal(75, legacy.AosElementDamages.Cold);
        Assert.Equal(25, legacy.AosElementDamages.Poison);
        Assert.Equal(255, legacy.InitMinHits);
        Assert.Equal(Race.AllowGargoylesOnly, legacy.RequiredRaces); // DreadSword's
        legacy.Delete();

        var sword = new SwordOfShatteredHopes();
        Assert.IsAssignableFrom<GlassSword>(sword);
        Assert.Equal(91, sword.Hue);
        Assert.Equal(1112770, sword.LabelNumber);
        Assert.Equal(10, sword.ArtifactRarity);
        Assert.Equal(25, sword.WeaponAttributes.HitDispel);
        Assert.Equal(15, sword.WeaponAttributes.ResistFireBonus);
        Assert.Equal(30, sword.Attributes.WeaponSpeed);
        Assert.Equal(50, sword.Attributes.WeaponDamage);
        Assert.Equal(255, sword.InitMaxHits);
        sword.Delete();

        var pick = new TheImpalersPick();
        Assert.IsAssignableFrom<HammerPick>(pick);
        Assert.Equal(0x143D, pick.ItemID);
        Assert.Equal(2101, pick.Hue);
        Assert.Equal(1113822, pick.LabelNumber);
        Assert.Equal(SlayerName.Repond, pick.Slayer);
        Assert.Equal(40, pick.WeaponAttributes.HitLightning);
        Assert.Equal(40, pick.WeaponAttributes.HitLowerDefend);
        Assert.Equal(30, pick.Attributes.WeaponSpeed);
        Assert.Equal(45, pick.Attributes.WeaponDamage);
        Assert.Single(typeof(TheImpalersPick).GetCustomAttributes(typeof(FlippableAttribute), false));
        pick.Delete();

        // The two cloth pieces sit on BaseSetClothing (SetID None, so the carrier is inert) with the ServUO clothing's
        // item ID, layer, weight and race, and their absorption line intact.
        var mantle = new MantleOfTheFallen();
        Assert.IsAssignableFrom<BaseSetClothing>(mantle);
        Assert.Equal(SetItem.None, mantle.SetID);
        Assert.Equal(0x406, mantle.ItemID);
        Assert.Equal(Layer.InnerTorso, mantle.Layer);
        Assert.Equal(2.0, mantle.Weight);
        Assert.Equal(Race.AllowGargoylesOnly, mantle.RequiredRaces);
        Assert.Equal(1512, mantle.Hue);
        Assert.Equal(1113819, mantle.LabelNumber);
        Assert.Equal(3, mantle.AbsorptionAttributes.CastingFocus);
        Assert.Equal(25, mantle.Attributes.LowerRegCost);
        Assert.Equal(8, mantle.Attributes.BonusInt);
        Assert.Equal(8, mantle.Attributes.BonusMana);
        Assert.Equal(1, mantle.Attributes.RegenMana);
        Assert.Equal(5, mantle.Attributes.SpellDamage);
        Assert.Equal(5, mantle.BasePhysicalResistance);
        Assert.Equal(8, mantle.BaseFireResistance);
        Assert.Equal(11, mantle.BaseColdResistance);
        Assert.Equal(12, mantle.BasePoisonResistance);
        Assert.Equal(8, mantle.BaseEnergyResistance);
        Assert.Equal(255, mantle.InitMinHits);
        mantle.Delete();

        var kilt = new SummonersKilt();
        Assert.IsAssignableFrom<BaseSetClothing>(kilt);
        Assert.Equal(SetItem.None, kilt.SetID);
        Assert.Equal(0x408, kilt.ItemID);
        Assert.Equal(Layer.Gloves, kilt.Layer); // the gargoyle kilt slot, ServUO's
        Assert.Equal(2.0, kilt.Weight);
        Assert.Equal(Race.AllowGargoylesOnly, kilt.RequiredRaces);
        Assert.Equal(1266, kilt.Hue);
        Assert.Equal(1113540, kilt.LabelNumber);
        Assert.Equal(2, kilt.AbsorptionAttributes.CastingFocus);
        Assert.Equal(5, kilt.Attributes.BonusMana);
        Assert.Equal(2, kilt.Attributes.RegenMana);
        Assert.Equal(5, kilt.Attributes.SpellDamage);
        Assert.Equal(8, kilt.Attributes.LowerManaCost);
        Assert.Equal(10, kilt.Attributes.LowerRegCost);
        Assert.Equal(5, kilt.BasePhysicalResistance);
        Assert.Equal(7, kilt.BaseFireResistance);
        Assert.Equal(21, kilt.BaseColdResistance);
        Assert.Equal(6, kilt.BasePoisonResistance);
        Assert.Equal(21, kilt.BaseEnergyResistance);
        Assert.Equal(255, kilt.InitMaxHits);
        kilt.Delete();

        // The name is the stock armour's alias, not a second declaration: FindTypeByName("GargishClothChest") is the
        // pinned GargishClothChestType1, which is why the two ServUO clothing bases were not ported.
        Assert.Same(typeof(GargishClothChestType1), AssemblyHandler.FindTypeByName("GargishClothChest"));
        Assert.Same(typeof(GargishClothKiltType1), AssemblyHandler.FindTypeByName("GargishClothKilt"));
    }
}
