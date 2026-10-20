// cc-P129: the port list's Creatures row, part 2 - the creatures that call Pet Training (against cc-P121's ability
// core), cc-P119's cheap leftovers, four ML peerless bosses with their pet parrot, and the two Drelgors: ServUO's,
// ported, and the shard's own, renamed Vorlak the Impaler under a TypeAlias.
//
// Every row below was read off the ServUO file, not off our port, so a value copied wrong into a port goes red here.
// What this file proves, per creature: it resolves by name, constructs with ServUO's values, runs at pinned's
// perception default and the Medium speed fallback (the port recipe's two fidelity traps, Q-008), carries its own
// generator attribute and (Serial) constructor, survives a save and reload, and a spawner fills with it. Then the
// behaviour each one adds: the profile abilities (stored, or natural twins listed once), the weapon abilities, the
// cold auras, the elemental drakes' element through a save, the tiger pelts, the leather wolf's pack, the macaw foil,
// the carving kit, the gargish outcast's kit, the parrot on its perch and in its deed (the one upstream patch), the
// ooze, the Chief's pet-eating, Lady Melisande's life-taking, the Crimson Dragon's terror, and the two Drelgors.
//
// Test-host facts this file meets (notes/port-recipe.md): hit points regenerate every 8 ms, so damage is read on the
// tick it lands; everything with a timer is made after ShardTestClock.Arm(); there is no tile data (D27), so nothing
// here depends on a worn layer or a land tile (the iron beetle's mining is not exercised: it reads the map).

using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using ModernUO.Serialization;
using Server;
using Server.Engines.Spawners;
using Server.Items;
using Server.Misc;
using Server.Mobiles;
using Server.Targeting;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class CCP129CreatureVerification
{
    private readonly ITestOutputHelper _out;

    public CCP129CreatureVerification(ITestOutputHelper output) => _out = output;

    private const int Unchecked = int.MinValue;

    // (type name, its parent, ServUO Name (null: random or set from a list, checked non-empty), ServUO CorpseName
    // (null: the parent's), Body (Unchecked: random), Hue (Unchecked: random), Fame, Karma, AI as ported). The AI is
    // ServUO's except AI_NecroMage, AI_Necro, AI_Mystic and AI_Spellweaving, which are AI_Mage here (Q-050, D-41).
    private static readonly (string Type, string Parent, string Name, string Corpse, int Body, int Hue, int Fame, int Karma, AIType AI)[] Rows =
    {
        ("BloodFox", "BaseCreature", "Blood Fox", "a blood fox corpse", 0x58f, 0, Unchecked, Unchecked, AIType.AI_Melee),
        ("ColdDrake", "BaseCreature", "a cold drake", "a drake corpse", Unchecked, Unchecked, 12000, -12000, AIType.AI_Melee),
        ("FrostDrake", "ColdDrake", "a cold drake", "a drake corpse", Unchecked, Unchecked, 12000, -12000, AIType.AI_Melee),
        ("CrimsonDrake", "BaseCreature", "Crimson Drake", "a crimson drake corpse", Unchecked, Unchecked, 5500, -5500, AIType.AI_Melee),
        ("PlatinumDrake", "BaseCreature", "Platinum Drake", "a platinum drake corpse", Unchecked, Unchecked, 5500, -5500, AIType.AI_Melee),
        ("DragonWolf", "BaseCreature", "a dragon wolf", "a dragon wolf corpse", 719, 0, 8500, -8500, AIType.AI_Melee),
        ("FrostDragon", "BaseCreature", "a frost dragon", "a dragon corpse", Unchecked, Unchecked, 25000, -25000, AIType.AI_Mage),
        ("FrostMite", "BaseCreature", "Frost Mite", "a beetle corpse", 0x590, 0, Unchecked, Unchecked, AIType.AI_Melee),
        ("GiantTurkey", "BaseCreature", "a giant turkey", "a giant turkey corpse", 1026, 0, Unchecked, Unchecked, AIType.AI_Animal),
        ("IronBeetle", "BaseCreature", "an iron beetle", "an iron beetle corpse", 714, 0, 15000, -15000, AIType.AI_Melee),
        ("Lion", "BaseCreature", "Lion", "a lion corpse", 0x592, 0, 11000, -11000, AIType.AI_Melee),
        ("OsseinRam", "BaseCreature", "Ossein Ram", "an ossein ram corpse", 0x591, 0, Unchecked, Unchecked, AIType.AI_Melee),
        ("SabertoothedTiger", "BaseCreature", "saber-toothed tiger", "a saber-toothed tiger corpse", 0x588, 0, 11000, -11000, AIType.AI_Melee),
        ("ShadowDweller", "BaseCreature", "a shadow dweller", "a shadow dweller corpse", 740, 1, 8000, -8000, AIType.AI_Mage),
        ("StygianDrake", "BaseCreature", "Stygian Drake", "a stygian drake corpse", 0x58E, 32768, 5500, -5500, AIType.AI_Mage),
        ("Triceratops", "BaseCreature", "Triceratops", "a triceratops corpse", 0x587, 0, Unchecked, Unchecked, AIType.AI_Melee),
        ("Viscera", "BaseCreature", "Viscera", "a Visceral corpse", 775, 2718, 2000, -2000, AIType.AI_Melee),
        ("WildTiger", "BaseMount", "a wild tiger", "a tiger corpse", Unchecked, 0, 11000, -11000, AIType.AI_Melee),
        ("WildWhiteTiger", "WildTiger", "a wild white tiger", "a tiger corpse", Unchecked, 2500, 11000, -11000, AIType.AI_Melee),
        ("WildBlackTiger", "WildTiger", "a wild black tiger", "a tiger corpse", Unchecked, 1175, 11000, -11000, AIType.AI_Melee),
        ("Macaw", "BaseCreature", "vicious macaw", null, 5, Unchecked, 7000, -7000, AIType.AI_Melee),
        ("StoneMonster", "BaseCreature", "Stone Monster", "Stone Monster corpse", Unchecked, 0, 8000, -8000, AIType.AI_Mage),
        ("LeatherWolf", "BaseCreature", "a leather wolf", "a wolf corpse", 739, 0, 4500, -4500, AIType.AI_Melee),
        ("LeatherWolfFellow", "BaseCreature", "a leather wolf", null, 739, 0, 2500, -2500, AIType.AI_Melee),
        ("TheButcher", "BaseCreature", null, "an butcher corpse", 306, 2075, 24000, -24000, AIType.AI_Mage),
        ("GargishOutcast", "BaseCreature", null, null, Unchecked, Unchecked, 12000, -12000, AIType.AI_Mage),
        ("ChiefParoxysmus", "BasePeerless", "a chief paroxysmus", "a chief paroxysmus corpse", 0x100, 0, 25000, -25000, AIType.AI_Melee),
        ("CrimsonDragon", "BasePeerless", "a crimson dragon", "a crimson dragon corpse", 197, 0, 20000, -20000, AIType.AI_Mage),
        ("LadyMelisande", "BasePeerless", "a lady melisande", "a lady melisande corpse", 0x102, 0, 25000, -25000, AIType.AI_Mage),
        ("MonstrousInterredGrizzle", "BasePeerless", "a monstrous interred grizzle", "a monstrous interred grizzle corpse", 0x103, 0, 24000, -24000, AIType.AI_Mage),
        ("ParoxysmusSwampDragon", "SwampDragon", "Chief Paroxysmus' Swamp Dragon", "a swamp dragon corpse", Unchecked, 1155, 2000, -2000, AIType.AI_Melee),
        ("AlbinoSquirrel", "Squirrel", null, null, 0x116, 0x482, Unchecked, Unchecked, AIType.AI_Animal),
        ("PetParrot", "BaseCreature", "a pet parrot", null, 0x11A, 0, Unchecked, Unchecked, AIType.AI_Animal),
        ("Drelgor", "BaseCreature", "Drelgor the Impaler", "A Drelgor The Impaler Corpse", 147, 0, 3600, -3600, AIType.AI_Melee)
    };

    private static readonly string[] LeafItems =
    {
        "TigerPelt", "WhiteTigerPelt", "BlackTigerPelt", "UndamagedIronBeetleScale", "UndeadGargHorn", "LeatherWolfSkin",
        "PumpkinCarvingKit", "CarvablePumpkinTall", "CarvableGordPumpkinTall", "CarvablePlainPumpkin", "GargishRobe",
        "MacawSpawner", "MacawNest", "ParrotItem", "ParoxysmusSwampDragonStatuette", "AlbinoSquirrelImprisonedInCrystal"
    };

    private static Type Resolve(string name)
    {
        var t = AssemblyHandler.FindTypeByName(name);
        Assert.True(t != null, $"{name} resolves to no type: not ported");
        return t;
    }

    private static BaseCreature Make(string name) => (BaseCreature)Activator.CreateInstance(Resolve(name))!;

    // The [add command's shape: the shortest [Constructible] constructor, optional parameters at their defaults (a
    // stackable's (int amount = 1) is not a parameterless constructor to reflection).
    private static Item MakeItem(Type t)
    {
        var ctor = t.GetConstructors()
            .Where(c => c.GetCustomAttributes(typeof(ConstructibleAttribute), false).Length > 0)
            .OrderBy(c => c.GetParameters().Length)
            .First();
        var args = ctor.GetParameters().Select(p => p.HasDefaultValue ? p.DefaultValue : null).ToArray();
        return (Item)ctor.Invoke(args);
    }

    private static readonly Point3D Origin = new(1000, 1000, 0);

    private static Point3D At(int dx, int dy) => new(Origin.X + dx, Origin.Y + dy, Origin.Z);

    private static PlayerMobile NewPlayer(Point3D loc)
    {
        var pm = new PlayerMobile { Player = true, Young = false, RawStr = 200 };
        pm.AddItem(new Backpack());
        pm.MoveToWorld(loc, Map.Malas);
        pm.Hits = pm.HitsMax;
        return pm;
    }

    // Serialize through the generated Serialize and read back into a fresh instance built with the generated (Serial)
    // constructor: CC6 batch 8's shape.
    private static T RoundTrip<T>(T original) where T : class, ISerializable
    {
        var buffer = new byte[65536];
        var writer = new BufferWriter(buffer, true);
        original.Serialize(writer);
        writer.Flush();

        var copy = (T)Activator.CreateInstance(original.GetType(), original.Serial)!;
        copy.Deserialize(new BufferReader(buffer));
        return copy;
    }

    [Fact]
    public void EachCreatureResolvesAndConstructsWithItsServUOValues()
    {
        Assert.Equal(34, Rows.Length);
        Assert.Equal(34, Rows.Select(r => r.Type).Distinct().Count());

        foreach (var row in Rows)
        {
            var type = Resolve(row.Type);
            Assert.Equal("Server.Mobiles." + row.Type, type.FullName);
            Assert.Equal(row.Parent, type.BaseType!.Name);

            var bc = (BaseCreature)Activator.CreateInstance(type)!;

            _out.WriteLine($"{row.Type}: name='{bc.Name}' body={(int)bc.Body} hue={bc.Hue} fame={bc.Fame} karma={bc.Karma} " +
                           $"hits={bc.HitsMax} ai={bc.AI} perception={bc.RangePerception} speed={bc.ActiveSpeed}/{bc.PassiveSpeed}");

            if (row.Name != null)
            {
                Assert.Equal(row.Name, bc.Name);
            }
            else
            {
                Assert.False(string.IsNullOrEmpty(bc.Name), $"{row.Type} has no name");
            }

            if (row.Corpse != null)
            {
                Assert.Equal(row.Corpse, bc.CorpseName);
            }

            Assert.Equal(row.AI, bc.AI);
            Assert.True(bc.HitsMax > 0, $"{row.Type} has no hits");

            if (row.Body != Unchecked)
            {
                Assert.Equal(row.Body, (int)bc.Body);
            }

            if (row.Hue != Unchecked)
            {
                Assert.Equal(row.Hue, bc.Hue);
            }

            if (row.Fame != Unchecked)
            {
                Assert.Equal(row.Fame, bc.Fame);
                Assert.Equal(row.Karma, bc.Karma);
            }

            // The two traps (notes/port-recipe.md): ServUO's 10 runs at 16, which is pinned's default, and its speed
            // literals are dead, so every port lands on the Medium fallback (Q-008).
            Assert.Equal(BaseCreature.DefaultRangePerception, bc.RangePerception);
            Assert.Equal(0.25, bc.ActiveSpeed);
            Assert.Equal(0.5, bc.PassiveSpeed);

            // Its own generator attribute (inherit: false) and its own (Serial) constructor, at every level.
            Assert.Single(type.GetCustomAttributes(typeof(SerializationGeneratorAttribute), false));
            Assert.Same(type, type.GetConstructor(new[] { typeof(Serial) })!.DeclaringType);

            bc.Delete();
        }
    }

    [Fact]
    public void EachCreatureSurvivesASaveAndReload()
    {
        foreach (var row in Rows)
        {
            var bc = Make(row.Type);
            bc.MoveToWorld(Origin, Map.Malas);

            var copy = RoundTrip(bc);

            Assert.Same(bc.GetType(), copy.GetType());
            Assert.Equal(bc.Name, copy.Name);
            Assert.Equal(bc.Body, copy.Body);
            Assert.Equal(bc.Hue, copy.Hue);
            Assert.Equal(bc.RawStr, copy.RawStr);
            Assert.Equal(bc.HitsMax, copy.HitsMax);
            Assert.Equal(bc.Fame, copy.Fame);
            Assert.Equal(bc.Karma, copy.Karma);
            Assert.Equal(bc.Tamable, copy.Tamable);
            Assert.Equal(bc.Skills.MagicResist.Base, copy.Skills.MagicResist.Base);

            bc.Delete();
            copy.Delete();
        }
    }

    [Fact]
    public void TheElementalDrakesKeepTheirElementThroughASave()
    {
        // ServUO CrimsonDrake / PlatinumDrake: the element picks the body, the hue and the damage type, and is the one
        // persisted field (m_Type).
        var cold = new CrimsonDrake(ElementType.Cold);
        Assert.Equal(ElementType.Cold, cold.ElementType);
        Assert.Equal(0x58C, (int)cold.Body);
        Assert.Equal(34134, cold.Hue);
        Assert.Equal(100, cold.ColdDamage);
        Assert.Equal(0, cold.PhysicalDamage);

        var physical = new PlatinumDrake(ElementType.Physical);
        Assert.Equal(0x589, (int)physical.Body);
        Assert.Equal(100, physical.PhysicalDamage);

        cold.MoveToWorld(Origin, Map.Malas);
        var copy = RoundTrip(cold);
        Assert.Equal(ElementType.Cold, copy.ElementType);
        Assert.Equal(cold.Body, copy.Body);

        // The parameterless constructor picks one of the five.
        var any = new CrimsonDrake();
        Assert.True(any.ElementType <= ElementType.Energy, $"random element {any.ElementType}");

        cold.Delete();
        copy.Delete();
        physical.Delete();
        any.Delete();
    }

    [Fact]
    public void ASpawnerFillsWithEachCreature()
    {
        foreach (var row in Rows)
        {
            var type = Resolve(row.Type);
            var spawner = new Spawner();
            spawner.MoveToWorld(Origin, Map.Malas);
            spawner.SpawnBounds = default; // no tile data in the host: spawn at the spawner's own location
            var entry = spawner.AddEntry(row.Type.ToLowerInvariant(), 100, 1, dotimer: false);

            try
            {
                Assert.True(spawner.Spawn(entry, out var flags), $"{row.Type}: Spawn returned false with flags {flags}");
                Assert.Equal(EntryFlags.None, flags);
                var spawned = Assert.IsAssignableFrom<BaseCreature>(Assert.Single(spawner.Spawned.Keys));
                Assert.Same(type, spawned.GetType());
                Assert.Equal(Origin, spawned.Location);
            }
            finally
            {
                spawner.Delete();
            }
        }
    }

    [Fact]
    public void TheProfilesCarryTheServUOSetAbilityCalls()
    {
        // ServUO's SetSpecialAbility / SetAreaEffect / SetMagicalAbility, through cc-P121's ability core. A natural twin
        // (a cold drake's aura through pinned's HasAura, a lion's Heal through CanHeal) is listed once, not stored twice.
        (string Type, object[] Expected)[] cases =
        {
            ("BloodFox", new object[] { SpecialAbility.GraspingClaw }),
            ("ColdDrake", new object[] { SpecialAbility.DragonBreath, AreaEffect.AuraDamage }),
            ("FrostDrake", new object[] { SpecialAbility.DragonBreath, AreaEffect.AuraDamage }),
            ("CrimsonDrake", new object[] { SpecialAbility.DragonBreath }),
            ("DragonWolf", new object[] { SpecialAbility.DragonBreath }),
            ("FrostDragon", new object[] { SpecialAbility.DragonBreath, AreaEffect.AuraDamage }),
            ("FrostMite", new object[] { AreaEffect.AuraDamage }),
            ("GiantTurkey", new object[] { AreaEffect.EssenceOfDisease, SpecialAbility.HowlOfCacophony }),
            ("Lion", new object[] { MagicalAbility.Piercing, SpecialAbility.Heal }),
            ("OsseinRam", new object[] { MagicalAbility.BattleDefense, SpecialAbility.LifeLeech }),
            ("SabertoothedTiger", new object[] { MagicalAbility.Slashing, SpecialAbility.Heal }),
            ("ShadowDweller", new object[] { SpecialAbility.LifeLeech }),
            ("StygianDrake", new object[] { MagicalAbility.MageryMastery }),
            ("Triceratops", new object[] { MagicalAbility.Piercing, SpecialAbility.Heal }),
            ("Viscera", new object[] { AreaEffect.AuraOfNausea, SpecialAbility.StickySkin }),
            ("WildTiger", new object[] { SpecialAbility.GraspingClaw, SpecialAbility.Heal }),
            ("ChiefParoxysmus", new object[] { AreaEffect.PoisonBreath }),
            ("CrimsonDragon", new object[] { SpecialAbility.DragonBreath }),
            ("LadyMelisande", new object[] { AreaEffect.AuraOfNausea }),
            ("MonstrousInterredGrizzle", new object[] { SpecialAbility.HowlOfCacophony })
        };

        foreach (var (typeName, expected) in cases)
        {
            var bc = Make(typeName);
            var profile = PetTrainingHelper.GetAbilityProfile(bc, true);
            var names = profile.AbilityNames;
            _out.WriteLine($"{typeName}: {names}");

            foreach (var ability in expected)
            {
                Assert.True(bc.HasAbility(ability), $"{typeName} lacks {ability}");

                var name = ability switch
                {
                    SpecialAbility s => s.Name,
                    AreaEffect a => a.Name,
                    _ => ability.ToString()
                };
                Assert.Single(Regex.Matches(names, $@"\b{Regex.Escape(name!)}\b"));
            }

            bc.Delete();
        }

        // Pinned's own aura is the natural twin: the cold drake has HasAura, so the profile never stores AuraDamage.
        var drake = new ColdDrake();
        Assert.True(drake.HasAura);
        Assert.Contains(AreaEffect.AuraDamage, PetTrainingHelper.GetAbilityProfile(drake, true).EnumerateNaturalAreaEffects());
        drake.Delete();

        // And a port that calls nothing stores nothing.
        var platinum = new PlatinumDrake();
        Assert.True(PetTrainingHelper.GetAbilityProfile(platinum, true).IsEmpty);
        platinum.Delete();
    }

    [Fact]
    public void TheWeaponAbilitiesAreServUOsSetWeaponAbilityCalls()
    {
        (string Type, WeaponAbility[] Expected)[] cases =
        {
            ("BloodFox", new[] { WeaponAbility.BleedAttack }),
            ("FrostDragon", new[] { WeaponAbility.BleedAttack }),
            ("GiantTurkey", new[] { WeaponAbility.ParalyzingBlow, WeaponAbility.BleedAttack, WeaponAbility.Dismount }),
            ("OsseinRam", new[] { WeaponAbility.MortalStrike }),
            ("WildTiger", new[] { WeaponAbility.BleedAttack }),
            ("LeatherWolf", new[] { WeaponAbility.BleedAttack }),
            ("TheButcher", new[] { WeaponAbility.MortalStrike, WeaponAbility.BleedAttack })
        };

        foreach (var (typeName, expected) in cases)
        {
            var bc = Make(typeName);
            var seen = Enumerable.Range(0, 300).Select(_ => bc.GetWeaponAbility()).Distinct().ToList();
            Assert.Equal(expected.OrderBy(a => Array.IndexOf(WeaponAbility.Abilities, a)),
                seen.OrderBy(a => Array.IndexOf(WeaponAbility.Abilities, a)));
            bc.Delete();
        }

        // The Frost Mite's only ServUO weapon ability is Cold Wind, which pinned has not (the Wight's deviation,
        // cc-P119): it declares none. Neither does anything else in the row (Lion's three sit in a dead version branch).
        foreach (var row in Rows.Where(r => cases.All(c => c.Type != r.Type) && r.Type != "GargishOutcast"))
        {
            var bc = Make(row.Type);
            var declared = bc.GetType().GetMethod("GetWeaponAbility", BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
            Assert.True(declared == null, $"{row.Type} declares a GetWeaponAbility ServUO does not give it");
            bc.Delete();
        }

        // The outcast fights with its thrown weapon's own abilities.
        var outcast = new GargishOutcast();
        var weapon = Assert.IsAssignableFrom<BaseThrown>(outcast.Weapon);
        var abilities = Enumerable.Range(0, 200).Select(_ => outcast.GetWeaponAbility()).Distinct().ToList();
        Assert.All(abilities, a => Assert.True(a == weapon.PrimaryAbility || a == weapon.SecondaryAbility));
        outcast.Delete();
    }

    [Fact]
    public void TheColdAurasAreServUOsColdAuraDefinition()
    {
        // ServUO AreaEffects.cs:576-580: ColdDrake, FrostDrake, FrostDragon and FrostMite share coldAura: 15 damage,
        // 100% cold, every 5 s, range 10 (AuraDamage.EffectRange). Landed on a player with 0 cold resistance: 15.
        foreach (var name in new[] { "ColdDrake", "FrostDrake", "FrostDragon", "FrostMite" })
        {
            var bc = Make(name);
            Assert.True(bc.HasAura, $"{name} has no aura");
            Assert.Equal(TimeSpan.FromSeconds(5), bc.AuraInterval);
            Assert.Equal(10, bc.AuraRange);
            Assert.Equal(15, bc.AuraBaseDamage);
            Assert.Equal(100, bc.AuraColdDamage);
            Assert.Equal(0, bc.AuraFireDamage);

            bc.MoveToWorld(Origin, Map.Malas);
            var pm = NewPlayer(At(2, 0));
            Assert.Equal(0, pm.ColdResistance);
            var before = pm.Hits;

            bc.AuraDamage();

            Assert.Equal(before - 15, pm.Hits);

            bc.Delete();
            pm.Delete();
        }

        // Pinned's AuraDamage is fired from OnThink on the interval; the profile does not fire it a second time
        // (the twin is natural). A drake and a player 2 tiles apart, 6 seconds of thinks: one aura, not two.
        ShardTestClock.Arm();
        var drake = new ColdDrake();
        drake.MoveToWorld(Origin, Map.Malas);
        var player = NewPlayer(At(2, 0));
        var start = player.Hits;
        var lowest = start;

        for (var i = 0; i < 6 * 125; i++)
        {
            ShardTestClock.Advance(TimeSpan.FromMilliseconds(8));
            drake.OnThink();
            lowest = Math.Min(lowest, player.Hits);
        }

        _out.WriteLine($"six seconds of thinks: {start} -> lowest {lowest}");
        Assert.True(lowest <= start - 15, "no aura landed");
        Assert.True(lowest > start - 30, "two auras landed on one tick");

        drake.Delete();
        player.Delete();
    }

    [Fact]
    public void TheLeafItemsResolveAndTheTigersYieldTheirPelts()
    {
        foreach (var name in LeafItems)
        {
            var t = Resolve(name);
            Assert.Equal("Server.Items." + name, t.FullName);
            Assert.Single(t.GetCustomAttributes(typeof(SerializationGeneratorAttribute), false));
            MakeItem(t).Delete();
        }

        // ServUO WildTiger.GetPelt: four pelts of the tiger's colour, dropped on the corpse when carved.
        Assert.IsType<TigerPelt>(new WildTiger().GetPelt);
        Assert.IsType<WhiteTigerPelt>(new WildWhiteTiger().GetPelt);
        Assert.IsType<BlackTigerPelt>(new WildBlackTiger().GetPelt);
        Assert.Equal(4, new WildBlackTiger().GetPelt.Amount);
        Assert.Equal(1175, new WildBlackTiger().GetPelt.Hue);

        // The pelts and the beetle scale are commodities with ServUO's clilocs.
        Assert.Equal(1123908, new TigerPelt().LabelNumber);
        Assert.Equal(1112905, new UndamagedIronBeetleScale().LabelNumber);
        Assert.True(((ICommodity)new TigerPelt()).IsDeedable);

        // The gargish robe is gargoyle-only, pinned's RequiredRace gate.
        Assert.Same(Race.Gargoyle, new GargishRobe().RequiredRace);

        // The Crystal Hydra's parrot pack (D-72, retired) builds and rolls: a kill with the pack in its loot.
        var hydra = new CrystalHydra();
        hydra.MoveToWorld(Origin, Map.Malas);
        hydra.Kill();
        Assert.False(hydra.Alive);
    }

    [Fact]
    public void TheLeatherWolfHowlsForThreeFellowsWhenItFightsAndSendsThemHomeAfter()
    {
        ShardTestClock.Arm();

        var wolf = new LeatherWolf();
        wolf.MoveToWorld(Origin, Map.Malas);
        var pm = NewPlayer(At(1, 0));

        Assert.Empty(wolf.Fellows);

        wolf.Combatant = pm; // OnCombatantChange starts the 30 s timer, first tick at once
        ShardTestClock.Advance(TimeSpan.FromMilliseconds(8));

        Assert.Equal(3, wolf.Fellows.Count);
        Assert.All(wolf.Fellows, f =>
        {
            var fellow = Assert.IsType<LeatherWolfFellow>(f);
            Assert.Same(pm, fellow.Combatant);
            Assert.Equal(Map.Malas, fellow.Map);
        });

        // One falls; the next tick brings a replacement.
        wolf.Fellows[0].Delete();
        ShardTestClock.Advance(TimeSpan.FromSeconds(30));
        Assert.Equal(3, wolf.Fellows.Count);
        Assert.All(wolf.Fellows, f => Assert.False(f.Deleted));

        // The fight ends: the pack is deleted on the next tick.
        wolf.Combatant = null;
        ShardTestClock.Advance(TimeSpan.FromSeconds(30));
        Assert.Empty(wolf.Fellows);

        wolf.Delete();
        pm.Delete();
    }

    [Fact]
    public void TheGoldFoilLoosesMacawsOnWhoeverLiftsItAndAMacawThatStraysForgetsItsNest()
    {
        ShardTestClock.Arm();

        var foil = new MacawSpawner();
        foil.MoveToWorld(Origin, Map.Malas);
        Assert.NotNull(foil.Addon);
        Assert.Same(foil, foil.Addon.Foil);
        Assert.Equal(Map.Malas, foil.Addon.Map);
        Assert.Equal(1124032, foil.LabelNumber);
        Assert.False(foil.Movable);

        var pm = NewPlayer(At(1, 0));
        foil.SpawnBirdies(pm);

        Assert.InRange(foil.Spawn.Count, 3, 5);
        Assert.All(foil.Spawn, s =>
        {
            var macaw = Assert.IsType<Macaw>(s);
            Assert.Same(foil, macaw.MacawSpawner);
            Assert.Same(pm, macaw.Combatant);
        });
        Assert.False(foil.Visible);

        // The foil shows again 60 to 90 s later.
        ShardTestClock.Advance(TimeSpan.FromSeconds(91));
        Assert.True(foil.Visible);

        // A macaw 21 tiles away leaves the nest's list and forgets it.
        var stray = (Macaw)foil.Spawn[0];
        stray.MoveToWorld(At(25, 0), Map.Malas);
        Assert.Null(stray.MacawSpawner);
        Assert.DoesNotContain(stray, foil.Spawn);

        // The nest and the foil die together.
        var nest = foil.Addon;
        foil.Delete();
        Assert.True(nest.Deleted);

        foreach (var s in foil.Spawn.ToArray())
        {
            s.Delete();
        }

        stray.Delete();
        pm.Delete();
    }

    [Fact]
    public void TheCarvingKitCarvesAPumpkinOnceAndIsSpent()
    {
        var pm = NewPlayer(Origin);
        pm.Name = "Carver";
        var kit = new PumpkinCarvingKit();
        pm.Backpack!.DropItem(kit);

        var pumpkin = new CarvablePumpkinTall();
        pumpkin.MoveToWorld(At(1, 0), Map.Malas);
        Assert.Equal(1123239, pumpkin.LabelNumber); // Carvable Pumpkin
        Assert.Null(pumpkin.CarvedBy);
        Assert.Equal(0x992F, pumpkin.ItemID);

        kit.OnDoubleClick(pm);
        Assert.NotNull(pm.Target);
        pm.Target.Invoke(pm, pumpkin);

        Assert.Equal(pm.Name, pumpkin.CarvedBy);
        Assert.Contains(pumpkin.ItemID, pumpkin.PumpkinDefinition.Select(d => d.UnlitItemID));
        Assert.Equal(1096937, pumpkin.LabelNumber); // carved pumpkin
        Assert.True(pumpkin.LitItemID > 0); // the carved face has a lit form
        Assert.True(kit.Deleted);

        // A plain barrel is not suitable.
        var kit2 = new PumpkinCarvingKit();
        pm.Backpack.DropItem(kit2);
        var barrel = new Barrel();
        barrel.MoveToWorld(At(1, 1), Map.Malas);
        kit2.OnDoubleClick(pm);
        pm.Target.Invoke(pm, barrel);
        Assert.False(kit2.Deleted);

        pumpkin.Delete();
        barrel.Delete();
        kit2.Delete();
        pm.Delete();
    }

    [Fact]
    public void TheGargishOutcastIsAnArmedGargoyleOfEitherSex()
    {
        for (var i = 0; i < 20; i++)
        {
            var outcast = new GargishOutcast();
            Assert.Same(Race.Gargoyle, outcast.Race);
            Assert.Equal("the Gargish Outcast", outcast.Title);
            Assert.False(string.IsNullOrEmpty(outcast.Name));
            Assert.Equal(outcast.Female ? 667 : 666, (int)outcast.Body);

            var weapon = Assert.IsAssignableFrom<BaseThrown>(outcast.Weapon);
            Assert.Equal(1, weapon.Attributes.SpellChanneling);
            Assert.Equal(LootType.Blessed, weapon.LootType);
            Assert.Contains(outcast.Items, it => it is GargishClothChestType1);
            Assert.Contains(outcast.Items, it => it is GargishClothKiltType1);
            Assert.Equal(AIType.AI_Mage, outcast.AI);

            outcast.Delete();
        }
    }

    [Fact]
    public void TheParrotStandsOnItsPerchFollowsItAndTravelsInItsDeed()
    {
        // The one upstream patch of this port: pinned's ParrotPerchAddon and ParrotPerchDeed carry ServUO's Parrot.
        var perch = new ParrotPerchAddon();
        perch.MoveToWorld(Origin, Map.Malas);
        Assert.Null(perch.Parrot);

        var parrot = new PetParrot { Hue = 0x3F };
        parrot.MoveToWorld(new Point3D(Origin.X, Origin.Y, Origin.Z + 12), Map.Malas);
        perch.Parrot = parrot;
        Assert.True(parrot.Frozen);
        Assert.True(parrot.Blessed);
        Assert.Equal("the parrot", parrot.Title);

        // The perch moves: the parrot sits 12 above it.
        perch.MoveToWorld(At(5, 5), Map.Malas);
        Assert.Equal(new Point3D(Origin.X + 5, Origin.Y + 5, Origin.Z + 12), parrot.Location);

        // The perch and its deed survive a save with the parrot.
        var perchCopy = RoundTrip(perch);
        Assert.Same(parrot, perchCopy.Parrot);
        perchCopy.Delete();

        // Chopped: the deed carries the parrot, the parrot is put away.
        var deed = Assert.IsType<ParrotPerchDeed>(perch.Deed);
        Assert.Same(parrot, deed.Parrot);
        perch.Delete();
        Assert.Equal(Map.Internal, parrot.Map);
        Assert.False(parrot.Deleted);

        // Placed again: the new perch has the parrot back on it, and the spent deed does not take it along.
        var again = Assert.IsType<ParrotPerchAddon>(deed.Addon);
        Assert.Same(parrot, again.Parrot);
        again.MoveToWorld(Origin, Map.Malas);
        Assert.Equal(Map.Malas, parrot.Map);
        Assert.Equal(new Point3D(Origin.X, Origin.Y, Origin.Z + 12), parrot.Location);
        deed.Delete();
        Assert.False(parrot.Deleted);

        // A deed thrown away with its parrot still put away takes the parrot with it.
        var deed2 = Assert.IsType<ParrotPerchDeed>(again.Deed);
        again.Delete();
        Assert.Equal(Map.Internal, parrot.Map);
        deed2.Delete();
        Assert.True(parrot.Deleted);

        // The parrot item itself: a pet parrot of one of ServUO's eleven hues, a target on use.
        var pm = NewPlayer(Origin);
        var item = new ParrotItem();
        Assert.Equal(1074281, item.LabelNumber);
        pm.Backpack!.DropItem(item);
        item.OnDoubleClick(pm);
        Assert.IsType<ParrotItem.InternalTarget>(pm.Target);
        Target.Cancel(pm);
        pm.Delete();
    }

    [Fact]
    public void TheOozeBurnsWhoeverStandsInItForFortyPoison()
    {
        ShardTestClock.Arm();

        var grizzle = new MonstrousInterredGrizzle();
        grizzle.MoveToWorld(Origin, Map.Malas);
        var ooze = Assert.IsType<InfernalOoze>(grizzle.NewHarmfulItem());
        Assert.Equal(0x95, ooze.Hue);
        Assert.False(ooze.Movable);
        ooze.MoveToWorld(At(3, 0), Map.Malas);
        ooze.Corrosive = false;

        var pm = NewPlayer(At(3, 0));
        Assert.Equal(0, pm.PoisonResistance);
        var before = pm.Hits;

        ooze.OnMoveOver(pm); // ServUO: AOS.Damage(m, owner, 40, 0, 0, 0, 100, 0)

        Assert.Equal(before - 40, pm.Hits);

        // It lasts 35 s and is gone.
        ShardTestClock.Advance(TimeSpan.FromSeconds(36));
        Assert.True(ooze.Deleted);

        grizzle.Delete();
        pm.Delete();
    }

    [Fact]
    public void ChiefParoxysmusEatsAPetThatHitsHimAndPullsADistantPlayerIn()
    {
        ShardTestClock.Arm();

        var chief = new ChiefParoxysmus();
        chief.MoveToWorld(Origin, Map.Malas);
        var pm = NewPlayer(At(1, 0));
        var pet = new Dog { ControlMaster = pm, Controlled = true };
        pet.MoveToWorld(At(1, 1), Map.Malas);

        chief.Hits = chief.HitsMax - 500;
        var chiefBefore = chief.Hits;
        var petHits = pet.Hits;

        chief.Damage(10, pet);

        Assert.False(pet.Alive);
        Assert.True(chief.Hits >= chiefBefore - 10 + petHits - 1, $"the chief did not eat the pet: {chiefBefore} -> {chief.Hits}");

        // A player hitting from range is teleported to him.
        var far = NewPlayer(At(8, 0));
        chief.Damage(10, far);
        Assert.True(chief.InRange(far.Location, 1), $"the player stayed at {far.Location}");
        Assert.Same(far, chief.Combatant);

        chief.Delete();
        pm.Delete();
        far.Delete();
        pet.Delete();
    }

    [Fact]
    public void LadyMelisandeTakesLifeOnlyWhenNearlyDead()
    {
        ShardTestClock.Arm();

        var lady = new LadyMelisande();
        lady.MoveToWorld(Origin, Map.Malas);
        var pm = NewPlayer(At(1, 0));
        lady.Combatant = pm;

        lady.Hits = lady.HitsMax / 2;
        Assert.False(lady.CanTakeLife(pm));

        lady.Hits = lady.HitsMax / 20; // 5%: between 2.5% and 10%
        Assert.True(lady.CanTakeLife(pm));

        var before = lady.Hits;
        lady.TakeLife(pm);
        Assert.Equal(before + pm.Hits / 2, lady.Hits);
        Assert.False(lady.CanTakeLife(pm)); // 15 to 60 s of cooldown

        lady.Delete();
        pm.Delete();
    }

    [Fact]
    public void TheCrimsonDragonTerrorizesAPlayerWhoWalksUpForFiveSeconds()
    {
        ShardTestClock.Arm();

        var dragon = new CrimsonDragon();
        dragon.MoveToWorld(Origin, Map.Malas);
        var pm = NewPlayer(At(2, 0));
        Assert.False(pm.Frozen);

        dragon.OnMovement(pm, At(3, 0));
        Assert.True(pm.Frozen);

        ShardTestClock.Advance(TimeSpan.FromSeconds(5.1));
        Assert.False(pm.Frozen);

        // Five minutes of grace before the next terror.
        dragon.OnMovement(pm, At(3, 0));
        Assert.False(pm.Frozen);

        dragon.Delete();
        pm.Delete();
    }

    [Fact]
    public void DrelgorIsServUOsAndTheShardsImpalerAnswersToItsOldName()
    {
        // ServUO's Drelgor: a bone-armoured skeleton with a scimitar and shield, fey-and-undead, no bleeding.
        var drelgor = new Drelgor();
        Assert.Equal(OppositionGroup.FeyAndUndead, drelgor.OppositionGroup);
        Assert.True(drelgor.BleedImmune);
        Assert.NotNull(drelgor.Backpack?.FindItemByType<Scimitar>());
        Assert.NotNull(drelgor.Backpack?.FindItemByType<WoodenShield>());
        Assert.Single(drelgor.Backpack!.Items, i => i is BoneArms or BoneChest or BoneGloves or BoneLegs or BoneHelm);

        // His broadcast fires on his first think and then once a minute; outside the "Old Haven Training" region it
        // reaches nobody (here he stands on Malas; cc-P189 registers the region over Old Haven), but the timer runs.
        ShardTestClock.Arm();
        drelgor.MoveToWorld(Origin, Map.Malas);
        drelgor.OnThink();
        Assert.True(drelgor.Init);
        Assert.True(drelgor.NextMsgTime > Core.Now);
        drelgor.Delete();

        // The shard's own Impaler is Vorlak now, and every saved DrelgorTheImpaler still loads as him.
        var vorlak = new VorlakTheImpaler();
        Assert.Equal("Vorlak the Impaler", vorlak.Name);
        Assert.Equal("Vorlak's corpse", vorlak.CorpseName);
        Assert.Equal(8000, vorlak.Fame);
        Assert.Equal(0x190, (int)vorlak.Body);
        Assert.Same(typeof(VorlakTheImpaler), AssemblyHandler.FindTypeByName("DrelgorTheImpaler"));
        Assert.Same(typeof(VorlakTheImpaler), AssemblyHandler.FindTypeByName("Server.Mobiles.DrelgorTheImpaler", fullName: true));
        Assert.Same(typeof(Drelgor), AssemblyHandler.FindTypeByName("Drelgor"));

        // A spawner entry written under the old name still spawns him.
        var spawner = new Spawner();
        spawner.MoveToWorld(Origin, Map.Malas);
        spawner.SpawnBounds = default;
        var entry = spawner.AddEntry("DrelgorTheImpaler", 100, 1, dotimer: false);
        Assert.True(spawner.Spawn(entry, out var flags), $"flags {flags}");
        Assert.IsType<VorlakTheImpaler>(Assert.Single(spawner.Spawned.Keys));
        spawner.Delete();
        vorlak.Delete();
    }
}
