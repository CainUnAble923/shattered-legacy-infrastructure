// CC6 batch 3: ten more creatures chosen by batch 1's rule - pinned ModernUO's own spawn data (Distribution/Data/Spawns)
// already places them and no loaded assembly declared them, so their spawner entries were running with
// EntryFlags.InvalidType (BaseSpawner.cs:1126-1131). Six were priced as Pet-Training-only (rotworm, wolf spider, fire
// daemon, coral snake, bloodworm, clockwork scorpion), three as one-leaf-item ports (fire ant, acid slug, maddening
// horror), and the orc scout wanted Yeast and an AI ModernUO lacks. Six items and two marker interfaces travel with
// them.
//
// This file proves the batch does what it was chosen for: every one constructs with its ServUO values, every stock
// spawn entry naming one of them now resolves, and the three mechanisms that were mapped onto ModernUO's own rather
// than dropped (the fire daemon's aura, the orc scout's self-bandaging, the bloodworm's corpse drain) actually fire.
// server/tests/<path> is mirrored into Projects/UOContent.Tests/Tests/<path> by docker/uo/apply-patches.sh and run
// as a gate by docker/uo/build.sh (see batch 1's file for the host facts).

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
public class CC6Batch3CreatureVerification
{
    private readonly ITestOutputHelper _out;

    public CC6Batch3CreatureVerification(ITestOutputHelper output) => _out = output;

    // (type, spawn-data spelling, ServUO Name, ServUO CorpseName, Body, Hue, Fame, Karma), verbatim from the ServUO
    // files. The spawn data names three of them in lower case; FindTypeByName ignores case and so must this. Hue -1 is
    // "random per instance" (the acid slug) and is checked by range below.
    private static readonly (Type Type, string SpawnName, string Name, string Corpse, int Body, int Hue, int Fame, int Karma)[] Batch =
    {
        (typeof(Rotworm), "rotworm", "a rotworm", "a rotworm corpse", 732, 0, 500, -500),
        (typeof(WolfSpider), "WolfSpider", "a Wolf spider", "a wolf spider spider corpse", 736, 0, 0, 0),
        (typeof(FireDaemon), "FireDaemon", "a fire daemon", "a fire daemon corpse", 9, 1636, 15000, -15000),
        (typeof(CoralSnake), "CoralSnake", "a coral snake", "a snake corpse", 52, 0x21, 300, -300),
        (typeof(BloodWorm), "bloodworm", "a bloodworm", "a bloodworm corpse", 287, 0, 0, 0),
        (typeof(ClockworkScorpion), "ClockworkScorpion", "a clockwork scorpion", "a clockwork scorpion corpse", 717, 0, 3500, -3500),
        (typeof(FireAnt), "FireAnt", "a fire ant", "a fire ant corpse", 738, 0, 0, 0),
        (typeof(AcidSlug), "AcidSlug", "an acid slug", "an acid slug corpse", 51, -1, 0, 0),
        (typeof(MaddeningHorror), "MaddeningHorror", "a maddening horror", "a maddening horror corpse", 721, 0, 23000, -23000),
        (typeof(OrcScout), "orcscout", "an orc scout", "an orcish corpse", 0xB5, 0, 1500, -1500)
    };

    private static PlayerMobile NewPlayer(Point3D loc, Map map = null)
    {
        // Mobile.Player is a flag CharacterCreation sets, not something the PlayerMobile constructor does, so a
        // test-host player is Player == false until told otherwise. Three of this batch's mechanisms branch on it
        // (the aura's target filter, the kin-mask check, the scorpion's master check); build A went red on all three.
        var pm = new PlayerMobile { Player = true };
        pm.AddItem(new Backpack());
        pm.MoveToWorld(loc, map ?? Map.TerMur);
        pm.Hits = pm.HitsMax; // a fresh PlayerMobile has 0 hits; the first point of damage would kill it
        return pm;
    }

    [Fact]
    public void AllTenConstructWithTheirServUOValues()
    {
        Assert.Equal(10, Batch.Length);

        foreach (var (type, spawnName, name, corpse, body, hue, fame, karma) in Batch)
        {
            var bc = (BaseCreature)Activator.CreateInstance(type)!;

            _out.WriteLine($"{type.Name}: name='{bc.Name}' body={(int)bc.Body} hue={bc.Hue} fame={bc.Fame} karma={bc.Karma} " +
                           $"hits={bc.HitsMax} ai={bc.AI} speed={bc.ActiveSpeed}/{bc.PassiveSpeed}");

            Assert.Equal(name, bc.Name);
            Assert.Equal(corpse, bc.CorpseName);
            Assert.Equal(body, (int)bc.Body);

            if (hue >= 0)
            {
                Assert.Equal(hue, bc.Hue);
            }
            else
            {
                Assert.InRange(bc.Hue, 242, 245);
            }

            Assert.Equal(fame, bc.Fame);
            Assert.Equal(karma, bc.Karma);
            Assert.True(bc.HitsMax > 0, $"{type.Name} has no hits");

            // Q-008: every ported creature takes ModernUO's Medium fallback; none of these overrides it.
            Assert.Equal(0.25, bc.ActiveSpeed);
            Assert.Equal(0.5, bc.PassiveSpeed);

            // The spawner path: BaseSpawner.Spawn resolves SpawnedName through AssemblyHandler.FindTypeByName, which
            // ignores case, so "rotworm", "bloodworm" and "orcscout" must reach their types as the spawn data spells them.
            Assert.Same(type, AssemblyHandler.FindTypeByName(spawnName));

            bc.Delete();
        }
    }

    [Fact]
    public void EveryStockSpawnEntryNamingOneOfTheTenNowResolves()
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

        // Batch 2 left exactly 72 names unresolved (tools/spawn-orphans.txt at 2d18ffa). The ten here were among
        // them, and nothing else in this batch adds or removes a spawnable type, so the count must be exactly 62.
        // CC6 batch 4 (2026-09-20) ported four spawn-named creatures of the 62, so it is now 58; that file's test
        // pins the batch-4 figure and tools/spawn-orphans.txt carries the list.
        // CC6 batch 5 (2026-09-20) ported the whole Renowned row, seventeen of the 58, so it is now 41.
        // CC6 batch 6 (2026-09-21) ported nothing; its one-name [TypeAlias] spike (Mobiles/Aliases/EliteNinjaSpawnAlias.cs,
        // Q-055) makes "eliteninjawarrior" resolve to stock EliteNinja, so it is now 40. Delete that file and it is 41.
        // CC6 batch 7 (2026-09-21), Part A: Q-055 answered yes, so the eight remaining name-mapping aliases were added
        // (Mobiles/Aliases/*SpawnAlias.cs: DryadA -> MLDryad, abbein..vicaie -> the seven Heartwood elders), so it is now 32.
        // CC6 batch 7 (2026-09-21), Parts B and C: the leaf-creature row (MageDragonsFlameMage and the seven Clan ratmen,
        // eight names) and TreasureLevel1h (one name) ported, so it is now 23.
        Assert.Equal(23, unresolved.Count);
        // Counted 2026-09-20 from the same files with tools/spawn_orphans.py's reader: rotworm 7, WolfSpider 4,
        // FireAnt 3, FireDaemon 3, CoralSnake 2, AcidSlug 2, bloodworm 2, orcscout 2, ClockworkScorpion 1,
        // MaddeningHorror 1 (Underworld, Abyss, TerMur, and the two Sanctuary files).
        Assert.Equal(27, referenced);
    }

    [Fact]
    public void TheFireDaemonsAuraBurnsThroughModernUOsOwnAuraPath()
    {
        // ServUO: SetAreaEffect(AreaEffect.AuraDamage) with the fireAura definition (7 damage, 100% fire, 5 s, and the
        // effect's own range 10) plus IAuraCreature.AuraEffect. Here: BaseCreature.HasAura and the Aura* virtuals,
        // fired from OnThink through AuraDamage(), which calls AuraEffect(m) on each victim.
        var daemon = new FireDaemon();
        Assert.True(daemon.HasAura);
        Assert.Equal(TimeSpan.FromSeconds(5), daemon.AuraInterval);
        Assert.Equal(10, daemon.AuraRange);
        Assert.Equal(7, daemon.AuraBaseDamage);
        Assert.Equal(100, daemon.AuraFireDamage);
        Assert.Equal(0, daemon.AuraPhysicalDamage + daemon.AuraColdDamage + daemon.AuraPoisonDamage + daemon.AuraEnergyDamage + daemon.AuraChaosDamage);
        Assert.Same(typeof(FireDaemon), typeof(FireDaemon).GetMethod("AuraEffect")!.DeclaringType);
        Assert.Equal(AIType.AI_Mage, daemon.AI);
        Assert.Equal(4, daemon.TreasureMapLevel);
        Assert.True(daemon.CanRummageCorpses);

        daemon.MoveToWorld(new Point3D(1000, 1000, 0), Map.TerMur);
        var near = NewPlayer(new Point3D(1001, 1000, 0));  // 1 tile away, inside the aura
        var far = NewPlayer(new Point3D(1011, 1000, 0));   // 11 tiles away, one past it
        var nearBefore = near.Hits;
        var farBefore = far.Hits;

        daemon.AuraDamage();

        _out.WriteLine($"near player hits {nearBefore} -> {near.Hits} (fire resist {near.FireResistance}); far player {farBefore} -> {far.Hits}");
        Assert.Equal(nearBefore - AOS.Scale(7, 100 - near.FireResistance), near.Hits);
        Assert.Equal(farBefore, far.Hits);

        daemon.Delete();
        near.Delete();
        far.Delete();
    }

    [Fact]
    public void TheOrcScoutBandagesItselfSparesAKinMaskAndBurnsItOffAnAggressor()
    {
        var scout = new OrcScout();
        scout.MoveToWorld(new Point3D(1000, 1000, 0), Map.TerMur);

        // ServUO HealChance => 1.0 (SpecialAbility.Heal, CheckHeal below 78%); ModernUO CanHeal with HealTrigger 0.78.
        Assert.True(scout.CanHeal);
        Assert.Equal(0.78, scout.HealTrigger);
        Assert.True(scout.Skills.Healing.Value >= 80.0);
        Assert.Equal(AIType.AI_Melee, scout.AI); // D-44: ServUO AI_OrcScout
        Assert.Equal(7, scout.RangeFight);
        Assert.Same(OppositionGroup.SavagesAndOrcs, scout.OppositionGroup);
        Assert.Same(Server.Misc.InhumanSpeech.Orc, scout.SpeechType);
        Assert.True(scout.CanRummageCorpses);
        Assert.Contains(scout.Items, i => i is Bow); // OrcishBow derives from Bow, so this covers both draws
        Assert.InRange(scout.Backpack!.FindItemByType<Apple>()!.Amount, 3, 5);
        Assert.InRange(scout.Backpack.FindItemByType<Arrow>()!.Amount, 60, 70);
        Assert.InRange(scout.Backpack.FindItemByType<Bandage>()!.Amount, 1, 15);

        // The bow and yeast draws are random; a hundred scouts see both bows and some yeast.
        var orcishBows = 0;
        var yeasts = 0;
        for (var i = 0; i < 100; i++)
        {
            var s = new OrcScout();
            if (s.Items.Any(it => it is OrcishBow))
            {
                orcishBows++;
            }

            if (s.Backpack?.FindItemByType<Yeast>() != null)
            {
                yeasts++;
            }

            s.Delete();
        }

        _out.WriteLine($"100 scouts: {orcishBows} orcish bows (ServUO 10%), {yeasts} with yeast (ServUO 50%)");
        Assert.InRange(orcishBows, 1, 30);
        Assert.InRange(yeasts, 25, 75);

        // A player in an orcish kin mask is not an enemy...
        var masked = NewPlayer(new Point3D(1002, 1000, 0));
        var mask = new OrcishKinMask();
        masked.AddItem(mask);
        Assert.Same(mask, masked.FindItemOnLayer(Layer.Helm));
        Assert.False(scout.IsEnemy(masked));

        // ...until they attack, when the mask burns off with 50 fire damage.
        var before = masked.Hits;
        scout.AggressiveAction(masked, false);
        _out.WriteLine($"masked aggressor hits {before} -> {masked.Hits}, mask deleted={mask.Deleted}");
        Assert.True(mask.Deleted);
        Assert.Null(masked.FindItemOnLayer(Layer.Helm));
        Assert.InRange(before - masked.Hits, 1, 50);

        scout.Delete();
        masked.Delete();
    }

    [Fact]
    public void TheBloodwormDrainsAHumanCorpseAndTheTwoMarkersTravel()
    {
        Assert.IsAssignableFrom<IBloodCreature>(new BloodWorm());
        Assert.IsAssignableFrom<IAcidCreature>(new AcidSlug());

        var worm = new BloodWorm();
        worm.MoveToWorld(new Point3D(1000, 1000, 0), Map.TerMur);
        worm.Hits = 1;

        var victim = NewPlayer(new Point3D(1005, 1005, 0));
        var corpse = new Corpse(victim, new List<Item>());
        corpse.MoveToWorld(new Point3D(1001, 1000, 0), Map.TerMur);
        Assert.Equal(0x2006, corpse.ItemID);

        // ServUO: a 25% roll per step while hurt. 300 steps make a miss astronomically unlikely (0.75^300).
        var steps = 0;
        while (worm.Hits < worm.HitsMax && steps < 300)
        {
            worm.OnAfterMove(worm.Location);
            steps++;
        }

        _out.WriteLine($"drained on step {steps}: corpse itemID=0x{corpse.ItemID:X}, worm hits {worm.Hits}/{worm.HitsMax}");
        Assert.Equal(worm.HitsMax, worm.Hits);
        Assert.InRange(corpse.ItemID, 0xECA, 0xED2); // Utility.Random(0xECA, 9), a bone pile graphic
        Assert.Equal(0, corpse.Hue);
        Assert.Equal(Direction.North, corpse.Direction);

        // A drained worm at full health leaves the next corpse alone.
        var second = new Corpse(victim, new List<Item>());
        second.MoveToWorld(new Point3D(1000, 1001, 0), Map.TerMur);
        for (var i = 0; i < 50; i++)
        {
            worm.OnAfterMove(worm.Location);
        }

        Assert.Equal(0x2006, second.ItemID);

        worm.Delete();
        corpse.Delete();
        second.Delete();
        victim.Delete();
    }

    [Fact]
    public void TheClockworkScorpionsMasterPaysForItsWoundsWithMana()
    {
        var scorpion = new ClockworkScorpion();
        Assert.True(scorpion.IsScaryToPets);
        Assert.False(scorpion.IsScaredOfScaryThings);
        Assert.False(scorpion.IsBondable);
        Assert.True(scorpion.DeleteOnRelease);
        Assert.True(scorpion.BleedImmune);
        Assert.Same(Poison.Lethal, scorpion.PoisonImmune);
        Assert.True(scorpion.AutoDispel); // !Controlled
        Assert.Equal(typeof(IronIngot), scorpion.RepairResource); // D-64: no IRepairableMobile to hang it on
        Assert.Equal(542, scorpion.GetIdleSound());
        Assert.Equal(545, scorpion.GetDeathSound());
        Assert.Equal(1, scorpion.ControlSlots);
        Assert.False(scorpion.Tamable);

        var master = NewPlayer(new Point3D(1000, 1000, 0));
        master.RawInt = 100;
        master.Mana = 100;
        Assert.Equal(100, master.Mana);

        scorpion.MoveToWorld(new Point3D(1001, 1000, 0), Map.TerMur);
        scorpion.Controlled = true;
        scorpion.ControlMaster = master;
        Assert.False(scorpion.AutoDispel);
        Assert.Equal(320, scorpion.GetHurtSound());

        var hitsBefore = scorpion.Hits;
        scorpion.Damage(30, null);
        _out.WriteLine($"scorpion hits {hitsBefore} -> {scorpion.Hits}; master mana 100 -> {master.Mana}");
        Assert.Equal(hitsBefore - 30, scorpion.Hits);
        Assert.Equal(70, master.Mana);

        // Beyond the master's mana, the remainder lands on the master.
        var masterHits = master.Hits;
        scorpion.Damage(80, null);
        _out.WriteLine($"after 80 more: master mana {master.Mana}, master hits {masterHits} -> {master.Hits}");
        Assert.Equal(0, master.Mana);
        Assert.Equal(masterHits - 10, master.Hits);

        scorpion.Delete();
        master.Delete();
    }

    [Fact]
    public void TheRestOfTheBatchKeepsItsServUOShape()
    {
        var rotworm = new Rotworm();
        var alias = (TypeAliasAttribute)Attribute.GetCustomAttribute(typeof(Rotworm), typeof(TypeAliasAttribute))!;
        Assert.NotNull(alias);
        Assert.Contains("Server.Mobiles.RotWorm", alias.Aliases);
        Assert.Equal(2, rotworm.Meat);
        Assert.Equal(MeatType.Ribs, rotworm.MeatType); // D-59: ServUO MeatType.Rotworm
        Assert.Equal(FoodType.Fish, rotworm.FavoriteFood);
        Assert.Equal(0x62A, rotworm.GetAttackSound());
        // ServUO PackBodyPartOrBones, inlined: exactly one of the eight in the pack.
        Assert.Single(rotworm.Backpack!.Items, i => i is LeftArm or RightArm or Torso or RightLeg or LeftLeg or Bone or RibCage or BonePile);

        var spider = new WolfSpider();
        Assert.True(spider.Tamable);
        Assert.Equal(2, spider.ControlSlots);
        Assert.Equal(59.1, spider.MinTameSkill);
        Assert.Equal(PackInstinct.Arachnid, spider.PackInstinct);
        Assert.Same(Poison.Regular, spider.HitPoison);
        Assert.Same(Poison.Regular, spider.PoisonImmune);
        Assert.Equal(FoodType.Meat, spider.FavoriteFood);
        Assert.True(spider.Skills.Poisoning.Value >= 62.3);
        Assert.Equal(1605, spider.GetIdleSound());

        var snake = new CoralSnake();
        Assert.Same(Poison.Deadly, snake.HitPoison);
        Assert.Same(Poison.Lesser, snake.PoisonImmune);
        Assert.False(snake.Tamable);
        Assert.Equal(1, snake.ControlSlots);
        Assert.Equal(59.1, snake.MinTameSkill);
        Assert.Equal(FoodType.Eggs, snake.FavoriteFood);
        // ServUO sets Physical twice and never Cold; the second Physical wins and Cold is 0. Copied (bug list §3).
        Assert.InRange(snake.PhysicalResistance, 5, 20);
        Assert.Equal(0, snake.ColdResistance);
        Assert.Equal(100, snake.PoisonResistance);

        var ant = new FireAnt();
        Assert.Equal(3, ant.TreasureMapLevel);
        Assert.Equal(96, ant.FireResistance);
        Assert.Equal(299, ant.HitsMax);
        Assert.Equal(846, ant.GetIdleSound());

        var slug = new AcidSlug();
        Assert.NotNull(slug.Backpack?.FindItemByType<CongealedSlugAcid>());
        Assert.Equal(0, slug.FireResistance);
        Assert.Equal(1499, slug.GetIdleSound());
        var sacs = 0;
        for (var i = 0; i < 100; i++)
        {
            var s = new AcidSlug();
            if (s.Backpack?.FindItemByType<AcidSac>() != null)
            {
                sacs++;
            }

            s.Delete();
        }

        _out.WriteLine($"100 slugs: {sacs} with an acid sac (ServUO 75%)");
        Assert.InRange(sacs, 50, 95);

        var horror = new MaddeningHorror();
        Assert.Equal(AIType.AI_Mage, horror.AI);
        Assert.Equal(120.0, horror.Skills.Necromancy.Value);
        Assert.Equal(120.0, horror.Skills.SpiritSpeak.Value);
        Assert.True(horror.Skills.Magery.Value >= 120.0);
        Assert.Equal(850, horror.RawInt);
        Assert.Equal(660, horror.HitsMax);
        // Q-050: stock MageAI casts Necromancy itself once Necromancy > 50, so AI_Mage is not a deviation here.
        var mageAI = Assert.IsAssignableFrom<MageAI>(horror.AIObject);
        Assert.True(mageAI.IsNecromancer);
        Assert.Equal(1553, horror.GetIdleSound());

        foreach (var m in new Mobile[] { rotworm, spider, snake, ant, slug, horror })
        {
            m.Delete();
        }
    }

    [Fact]
    public void TheSixItemsCarryTheirServUOValues()
    {
        var writ = new ArielHavenWritofMembership();
        Assert.Equal(0x2831, writ.ItemID);
        Assert.Equal(1094998, writ.LabelNumber);
        Assert.False(writ.Stackable);

        var sac = new AcidSac();
        Assert.Equal(0x0C67, sac.ItemID);
        Assert.Equal(648, sac.Hue);
        Assert.True(sac.Stackable);
        Assert.Equal(1, sac.Amount);
        Assert.Equal(1.0, sac.Weight);
        Assert.Equal(1111654, sac.LabelNumber);

        var acid = new CongealedSlugAcid(4);
        Assert.Equal(0x5742, acid.ItemID);
        Assert.Equal(4, acid.Amount);
        Assert.True(acid.Stackable);
        Assert.Equal(1112901, acid.LabelNumber);

        var goo = new SearedFireAntGoo(2);
        Assert.Equal(0x122E, goo.ItemID);
        Assert.Equal(1359, goo.Hue);
        Assert.Equal(2, goo.Amount);
        Assert.Equal(1112902, goo.LabelNumber);

        var tentacles = new VileTentacles(3);
        Assert.Equal(0x5727, tentacles.ItemID);
        Assert.Equal(3, tentacles.Amount);
        Assert.Equal(0.1, tentacles.Weight);
        Assert.Equal(1113333, tentacles.LabelNumber);
        var commodity = Assert.IsAssignableFrom<ICommodity>(tentacles);
        Assert.Equal(1113333, commodity.DescriptionNumber);
        Assert.True(commodity.IsDeedable);

        var yeast = new Yeast();
        Assert.Equal(3624, yeast.ItemID);
        Assert.Equal(2418, yeast.Hue);
        Assert.InRange(yeast.BacterialResistance, 1, 5);
        Assert.Equal(1150453, yeast.LabelNumber);
        // The clamp in the setter, and the (int) constructor's missing hue, both ServUO's.
        Assert.Equal(5, new Yeast(9).BacterialResistance);
        Assert.Equal(1, new Yeast(0).BacterialResistance);
        Assert.Equal(3, new Yeast(3).BacterialResistance);
        Assert.Equal(0, new Yeast(3).Hue);
        yeast.BacterialResistance = 7;
        Assert.Equal(5, yeast.BacterialResistance);
        var draws = new int[6];
        for (var i = 0; i < 500; i++)
        {
            draws[new Yeast().BacterialResistance]++;
        }

        _out.WriteLine($"500 random yeasts by resistance 1..5: {draws[1]} {draws[2]} {draws[3]} {draws[4]} {draws[5]} (ServUO ~59/20/10/5/6 %)");
        Assert.True(draws[1] > draws[2] && draws[2] > draws[3], "the resistance draw is not weighted toward 1 as ServUO's is");

        foreach (var i in new Item[] { writ, sac, acid, goo, tentacles, yeast })
        {
            i.Delete();
        }
    }
}
