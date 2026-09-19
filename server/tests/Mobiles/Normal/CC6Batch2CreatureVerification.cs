// CC6 batch 2: fourteen more creatures chosen by batch 1's rule - pinned ModernUO's own spawn data
// (Distribution/Data/Spawns) already places them and no loaded assembly declared them, so their spawner entries
// were running with EntryFlags.InvalidType (BaseSpawner.cs:1126-1131). Nine are clean ports (the Citadel's three
// Black Order humans, the Abyss's lava elemental, skeletal lich, forgotten servant, plague rat and pit fiend, Ter
// Mur's gargoyle shade); five are the fur cluster (three boura, two kepetch) with four items ported for them.
//
// This file proves the batch does what it was chosen for: every one constructs with its ServUO values, every stock
// spawn entry naming one of them now resolves, and the fur cluster's ICarvable route (BladedItemTarget -> Carve)
// yields fur once per animal. server/tests/<path> is mirrored into Projects/UOContent.Tests/Tests/<path> by
// docker/uo/apply-patches.sh and run as a gate by docker/uo/build.sh (see batch 1's file for the host facts).

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
public class CC6Batch2CreatureVerification
{
    private readonly ITestOutputHelper _out;

    public CC6Batch2CreatureVerification(ITestOutputHelper output) => _out = output;

    // (type, ServUO Name, ServUO CorpseName, Body, Hue, Fame, Karma): the values a player sees, verbatim from the
    // ServUO files. null / -1 means "random per instance" (the humans' names, skin hues and bodies) and is checked
    // by shape below instead.
    private static readonly (Type Type, string Name, string Corpse, int Body, int Hue, int Fame, int Karma)[] Batch =
    {
        (typeof(SerpentsFangAssassin), "Black Order Assassin", "a black order assassin corpse", -1, -1, 13000, -13000),
        (typeof(DragonsFlameMage), "Black Order Mage", "a black order mage corpse", -1, -1, 13000, -13000),
        (typeof(TigersClawThief), "Black Order Thief", "a black order thief corpse", -1, -1, 13000, -13000),
        (typeof(LavaElemental), "a lava elemental", "a lava elemental corpse", 720, 0, 0, 0),
        (typeof(SkeletalLich), "a skeletal lich", "a skeletal corpse", 309, 1345, 6000, -6000),
        (typeof(ForgottenServant), null, null, -1, 768, 2500, -2500),
        (typeof(ClanRibbonPlagueRat), "Clan Ribbon Plague Rat", "a rat corpse", 238, 52, 150, -150),
        (typeof(GargoyleShade), "a gargoyle shade", "a ghostly corpse", 4, 16385, 4000, -4000),
        (typeof(PitFiend), "a Pit fiend", "a pit fiend corpse", 43, 1863, 18000, -18000),
        (typeof(RuddyBoura), "a ruddy boura", "a boura corpse", 715, 0, 5000, -2500),
        (typeof(HighPlainsBoura), "a high plains boura", "a boura corpse", 715, 0, 5000, -5000),
        (typeof(LowlandBoura), "a lowland boura", "a boura corpse", 715, 0, 5000, -3500),
        (typeof(Kepetch), "a kepetch", "a kepetch corpse", 726, 0, 6000, -6000),
        (typeof(KepetchAmbusher), "a kepetch ambusher", "a kepetch corpse", 726, 0, 2500, -2500)
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
    public void AllFourteenConstructWithTheirServUOValues()
    {
        Assert.Equal(14, Batch.Length);

        foreach (var (type, name, corpse, body, hue, fame, karma) in Batch)
        {
            var bc = (BaseCreature)Activator.CreateInstance(type)!;

            _out.WriteLine($"{type.Name}: name='{bc.Name}' title='{bc.Title}' body={(int)bc.Body} hue={bc.Hue} " +
                           $"fame={bc.Fame} karma={bc.Karma} hits={bc.HitsMax} ai={bc.AI} speed={bc.ActiveSpeed}/{bc.PassiveSpeed}");

            if (name != null)
            {
                Assert.Equal(name, bc.Name);
            }

            if (corpse != null)
            {
                Assert.Equal(corpse, bc.CorpseName);
            }

            if (hue >= 0)
            {
                Assert.Equal(hue, bc.Hue);
            }

            Assert.Equal(fame, bc.Fame);
            Assert.Equal(karma, bc.Karma);
            Assert.True(bc.HitsMax > 0, $"{type.Name} has no hits");

            if (body >= 0)
            {
                Assert.Equal(body, (int)bc.Body);
            }
            else
            {
                // The four humans: ServUO gives them Race.Human (the Citadel three) or body 0x190/0x191 (the servant).
                Assert.True(bc.Body.IsHuman, $"{type.Name} has body {(int)bc.Body}, not a human body");
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
    public void EveryStockSpawnEntryNamingOneOfTheFourteenNowResolves()
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

        foreach (var (type, _, _, _, _, _, _) in Batch)
        {
            Assert.True(entries.TryGetValue(type.Name, out var count) && count > 0,
                $"{type.Name} is not named by any stock spawn entry; it was chosen because it is");
            Assert.DoesNotContain(type.Name, unresolved, StringComparer.OrdinalIgnoreCase);
            referenced += count;
            _out.WriteLine($"{type.Name}: {count} spawn entries");
        }

        // Batch 1 left exactly 89 names unresolved (tools/spawn-orphans.txt, 391db6c). The fourteen here were among
        // them, and nothing else in this batch adds or removes a spawnable type, so the count must be exactly 75.
        Assert.Equal(75, unresolved.Count);
        // Counted 2026-09-19 from the same files: 33 entries name the clean nine (Citadel 11+8+5, Abyss 3+2+1+1+1,
        // Ter Mur 1) and 18 name the fur five (6+5+1 boura, 2+4 kepetch).
        Assert.Equal(51, referenced);
    }

    [Fact]
    public void TheFurClusterShearsOnceThroughTheBladedItemRoute()
    {
        // (animal, fur hue from Fur's FurType switch, fur amount from the creature's Fur member)
        var cases = new (BaseCreature Animal, int Hue, int Amount)[]
        {
            (new RuddyBoura(), 1541, 30),     // LightBrown
            (new HighPlainsBoura(), 153, 30), // Yellow
            (new LowlandBoura(), 58, 30),     // Green
            (new Kepetch(), 343, 15),         // Brown
            (new KepetchAmbusher(), 343, 15)  // Brown
        };

        var knife = new Dagger();

        foreach (var (animal, hue, amount) in cases)
        {
            var carvable = Assert.IsAssignableFrom<ICarvable>(animal);
            var player = NewPlayer(new Point3D(1000, 1000, 0));
            animal.MoveToWorld(new Point3D(1001, 1000, 0), Map.TerMur);

            // BladedItemTarget.OnTarget: `if (targeted is ICarvable carvable) carvable.Carve(from, m_Item);`
            carvable.Carve(player, knife);

            var fur = player.Backpack!.FindItemByType<Fur>();
            Assert.NotNull(fur);
            Assert.Equal(hue, fur.Hue);
            Assert.Equal(amount, fur.Amount);
            Assert.True(fur.Stackable);

            // A second shear yields nothing: GatheredFur is set and Fur reads 0.
            carvable.Carve(player, knife);
            Assert.Single(player.Backpack.Items.OfType<Fur>());
            Assert.Equal(amount, player.Backpack.Items.OfType<Fur>().Sum(f => f.Amount));

            var gathered = animal.GetType().GetProperty("GatheredFur")!;
            var furLeft = animal.GetType().GetProperty("Fur")!;
            Assert.True((bool)gathered.GetValue(animal)!);
            Assert.Equal(0, (int)furLeft.GetValue(animal)!);

            _out.WriteLine($"{animal.GetType().Name}: fur hue={fur.Hue} amount={fur.Amount}, gathered={gathered.GetValue(animal)}");

            player.Delete();
            animal.Delete();
        }

        knife.Delete();
    }

    [Fact]
    public void ClanRibbonPlagueRatIsNotTheP1PlagueRat()
    {
        // Trap 4 in the brief: P1 ported ServUO's Mobiles/Normal/PlagueRat.cs, which ServUO names
        // "a Clan Ribbon Plague Rat". The Abyss spawn data names ClanRibbonPlagueRat, a different ServUO type.
        var theirs = new PlagueRat();
        var ours = new ClanRibbonPlagueRat();

        Assert.NotEqual(theirs.GetType(), ours.GetType());
        Assert.Equal("a Clan Ribbon Plague Rat", theirs.Name);
        Assert.Equal("Clan Ribbon Plague Rat", ours.Name);
        Assert.Equal(0xD7, (int)theirs.Body);
        Assert.Equal(238, (int)ours.Body);
        Assert.Equal(1710, theirs.Hue);
        Assert.Equal(52, ours.Hue);
        Assert.Same(typeof(ClanRibbonPlagueRat), AssemblyHandler.FindTypeByName("ClanRibbonPlagueRat"));
        Assert.Same(typeof(PlagueRat), AssemblyHandler.FindTypeByName("PlagueRat"));
        Assert.False(ours.Tamable);
        Assert.Equal(AIType.AI_Animal, ours.AI);
        Assert.Equal(FightMode.Aggressor, ours.FightMode);

        theirs.Delete();
        ours.Delete();
    }

    [Fact]
    public void TheRestOfTheBatchKeepsItsServUOShape()
    {
        var lich = new SkeletalLich();
        Assert.Same(WeaponAbility.Dismount, lich.GetWeaponAbility());
        Assert.Equal(AIType.AI_Mage, lich.AI); // deviation D-47: ServUO AI_NecroMage
        Assert.True(lich.BleedImmune);
        Assert.Same(Poison.Lethal, lich.PoisonImmune);
        Assert.Equal(1, lich.TreasureMapLevel);
        Assert.True(lich.Skills.Necromancy.Value >= 100.0);

        var assassin = new SerpentsFangAssassin();
        Assert.True(assassin.AlwaysMurderer);
        Assert.False(assassin.ShowFameTitle);
        Assert.Equal("of the Serpent's Fang Sect", assassin.Title);
        Assert.Same(Race.Human, assassin.Race);
        Assert.Contains(assassin.Items, i => i is Sai);
        Assert.Contains(assassin.Items, i => i is JinBaori && i.Hue == 0x2A);

        var thief = new TigersClawThief();
        Assert.Equal("of the Tiger's Claw Sect", thief.Title);
        Assert.Contains(thief.Items, i => i is Wakizashi);
        Assert.Contains(thief.Items, i => i is JinBaori && i.Hue == 0x69);

        var mage = new DragonsFlameMage();
        Assert.Equal("of the Dragon's Flame Sect", mage.Title);
        Assert.Equal(AIType.AI_Mage, mage.AI);
        Assert.Contains(mage.Items, i => i is Kasa && i.Hue == 0x51D);

        // ServUO AlterSpellDamageFrom: half of the spell damage comes back on the caster.
        var caster = NewPlayer(new Point3D(1000, 1000, 0));
        var before = caster.Hits;
        var damage = 40;
        mage.AlterSpellDamageFrom(caster, ref damage);
        _out.WriteLine($"caster hits {before} -> {caster.Hits} after a 40-point spell on the mage");
        Assert.Equal(40, damage);
        Assert.Equal(before - 20, caster.Hits);

        var servant = new ForgottenServant();
        Assert.Equal("Forgotten Servant", servant.Title);
        Assert.False(servant.ClickTitle);
        Assert.True(servant.AlwaysMurderer);
        Assert.False(string.IsNullOrEmpty(servant.Name), "no name drawn from Data/names.json");
        Assert.Contains(servant.Items, i => i is Bandana);
        Assert.Contains(servant.Items, i => i is BaseWeapon);
        Assert.Contains(servant.Items, i => i is Skirt or ShortPants);

        var lava = new LavaElemental();
        Assert.Equal(4, lava.Backpack?.FindItemByType<Nightshade>()?.Amount);
        Assert.Equal(5, lava.Backpack?.FindItemByType<SulfurousAsh>()?.Amount);
        Assert.NotNull(lava.Backpack?.FindItemByType<LesserPoisonPotion>());
        Assert.Equal(0x60A, lava.GetAttackSound());

        var shade = new GargoyleShade();
        Assert.Same(OppositionGroup.FeyAndUndead, shade.OppositionGroup);
        Assert.True(shade.BleedImmune);
        Assert.NotNull(shade.Backpack?.FindItemByType<BaseReagent>());

        var fiend = new PitFiend();
        Assert.Equal(125.0, fiend.DispelDifficulty);
        Assert.Equal(45.0, fiend.DispelFocus);
        Assert.True(fiend.CanRummageCorpses);
        Assert.Same(Poison.Regular, fiend.PoisonImmune);
        Assert.Equal(4, fiend.TreasureMapLevel);

        var ruddy = new RuddyBoura();
        Assert.True(ruddy.Tamable);
        Assert.Equal(2, ruddy.ControlSlots);
        Assert.Equal(19.1, ruddy.MinTameSkill);
        Assert.Equal(20, ruddy.Hides);
        Assert.Equal(HideType.Spined, ruddy.HideType);
        Assert.Equal(10, ruddy.Meat);
        Assert.Equal(FoodType.FruitsAndVeggies, ruddy.FavoriteFood);

        var plains = new HighPlainsBoura();
        Assert.Equal(3, plains.ControlSlots);
        Assert.Equal(47.1, plains.MinTameSkill);
        Assert.Equal(22, plains.Hides);
        Assert.Equal(HideType.Horned, plains.HideType);

        var kepetch = new Kepetch();
        Assert.False(kepetch.Tamable);
        Assert.True(kepetch.Skills.Parry.Value >= 60.0);
        Assert.True(kepetch.FavoriteFood.HasFlag(FoodType.GrainsAndHay));
        Assert.Equal(1545, kepetch.GetIdleSound());

        foreach (var m in new Mobile[] { lich, assassin, thief, mage, caster, servant, lava, shade, fiend, ruddy, plains, kepetch })
        {
            m.Delete();
        }
    }

    [Fact]
    public void TheKepetchAmbusherStartsHiddenRevealsWhenHitAndKeepsStealthWhileItWalks()
    {
        // The TrapdoorSpider shape from batch 1: ServUO's CanStealth branch of BaseCreature.OnMove is inlined.
        var kepetch = new KepetchAmbusher();
        kepetch.MoveToWorld(new Point3D(1000, 1000, 0), Map.TerMur);

        Assert.True(kepetch.Hidden);
        Assert.Equal(125.0, kepetch.Skills.Stealth.Value);
        Assert.Equal(125.0, kepetch.Skills.Hiding.Value);
        Assert.NotNull(kepetch.Backpack?.FindItemByType<RawRibs>());

        var onMove = typeof(KepetchAmbusher).GetMethod(
            "OnMove", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
        );
        Assert.NotNull(onMove);
        Assert.Same(typeof(KepetchAmbusher), onMove.DeclaringType);

        kepetch.AllowedStealthSteps = 0;
        onMove.Invoke(kepetch, new object[] { Direction.North });

        _out.WriteLine($"after one step: hidden={kepetch.Hidden} steps={kepetch.AllowedStealthSteps}");
        Assert.True(kepetch.Hidden, "the ambusher revealed itself on its first step (stock Mobile.OnMove behaviour)");
        Assert.True(kepetch.AllowedStealthSteps > 0, "Stealth.OnUse did not grant steps");

        kepetch.AllowedStealthSteps = 1;
        onMove.Invoke(kepetch, new object[] { Direction.North | Direction.Running });
        Assert.False(kepetch.Hidden);

        kepetch.Hidden = true;
        kepetch.Damage(1, null);
        Assert.False(kepetch.Hidden);

        kepetch.Delete();
    }

    [Fact]
    public void TheFourItemsCarryTheirServUOValues()
    {
        var plain = new Fur();
        Assert.Equal(0x1875, plain.ItemID);
        Assert.Equal(0, plain.Hue);
        Assert.Equal(1, plain.Amount);
        Assert.True(plain.Stackable);

        var green = new Fur(FurType.Green, 30);
        Assert.Equal(58, green.Hue);
        Assert.Equal(30, green.Amount);
        Assert.Equal(1541, new Fur(FurType.LightBrown).Hue);
        Assert.Equal(153, new Fur(FurType.Yellow).Hue);
        Assert.Equal(343, new Fur(FurType.Brown).Hue);

        // ServUO's [TypeAlias("Server.Items.BouraFur", "Server.Items.KepetchFur")], kept.
        var alias = (TypeAliasAttribute)Attribute.GetCustomAttribute(typeof(Fur), typeof(TypeAliasAttribute))!;
        Assert.NotNull(alias);
        Assert.Contains("Server.Items.BouraFur", alias.Aliases);
        Assert.Contains("Server.Items.KepetchFur", alias.Aliases);

        var skin = new BouraSkin();
        Assert.Equal(0x11F4, skin.ItemID);
        Assert.Equal(LootType.Blessed, skin.LootType);
        Assert.Equal(0x292, skin.Hue);
        Assert.Equal(1112900, skin.LabelNumber);

        var wax = new KepetchWax();
        Assert.Equal(0x5745, wax.ItemID);
        Assert.Equal(1112412, wax.LabelNumber);

        var shield = new BouraTailShield();
        Assert.IsAssignableFrom<WoodenKiteShield>(shield);
        Assert.Equal(554, shield.Hue);
        Assert.Equal(10, shield.Attributes.ReflectPhysical);
        Assert.Equal(8, shield.BasePhysicalResistance);
        Assert.Equal(1, shield.BaseEnergyResistance);
        Assert.Equal(255, shield.InitMinHits);
        Assert.Equal(255, shield.InitMaxHits);
        Assert.Equal(1112361, shield.LabelNumber);
        // D-49: ServUO's ArmorAttributes.ReactiveParalyze = 1 has no member to land on here; nothing to assert.

        foreach (var i in new Item[] { plain, green, skin, wax, shield })
        {
            i.Delete();
        }
    }
}
