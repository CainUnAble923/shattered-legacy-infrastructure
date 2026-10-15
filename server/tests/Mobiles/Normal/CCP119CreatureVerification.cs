// cc-P119: the port list's Creatures row, part 1 - the clean candidates that call nothing in Pet Training.
//
// Twenty-nine ServUO creatures (26 with no missing dependency, three that bring one leaf item each) and the three leaf
// items. ServUO's ToxicElemental was ported and then withdrawn in this task: pinned's AcidElemental carries
// [TypeAlias("Server.Mobiles.ToxicElemental")], so the name already resolves to the stock creature, and
// TypeIdentityVerification went red on the duplicate (the last fact below pins that).
// Full list, sorting and reasons in shard-migration notes/cc-P119-port-creatures-part1.md. Every row below was read off
// the ServUO file by a script (the notes carry it), not off our port, so a value copied wrong into a port goes red here.
//
// What this file proves, per creature: it resolves by name (the [add and spawner lookup), constructs with ServUO's
// values, runs at pinned's perception default and the Medium speed fallback (the port recipe's two fidelity traps,
// Q-008), carries its own generator attribute and (Serial) constructor, survives a save and reload through its
// generated serializer, and a spawner fills with it. Then the behaviour each one adds over its base: the weapon
// abilities, the Abyss Guardian's hatchet, the orc chopper's kin-mask rule, and the three leaf drops.
//
// server/tests/<path> is mirrored into Projects/UOContent.Tests/Tests/<path> by docker/uo/apply-patches.sh and run as a
// gate by docker/uo/build.sh. The test host has no tile data (D27) and regenerates a hit every 8 ms (D15): the hatchet
// fact reads hit points on the tick the damage lands, and spawners keep SpawnBounds at default.

using System;
using System.Linq;
using System.Reflection;
using ModernUO.Serialization;
using Server;
using Server.Engines.Spawners;
using Server.Items;
using Server.Misc;
using Server.Mobiles;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class CCP119CreatureVerification
{
    private readonly ITestOutputHelper _out;

    public CCP119CreatureVerification(ITestOutputHelper output) => _out = output;

    private const int Unchecked = int.MinValue;

    // (type name, its parent, ServUO Name (null: set at random, checked non-empty), ServUO CorpseName, Body, Hue, Fame,
    // Karma, AI as ported). Unchecked means ServUO's file does not set it and the stock parent's value stands. The AI is
    // ServUO's except AI_NecroMage and AI_Mystic, which are AI_Mage here (Q-050, D-41). The type names are ServUO's class
    // names: three differ from their file names (Icehound.cs is IceHound, EnragedCollosus.cs is EnragedColossus,
    // PalaminoHorse.cs is Palomino), and the files keep ServUO's names so the port list counts them.
    private static readonly (string Type, string Parent, string Name, string Corpse, int Body, int Hue, int Fame, int Karma, AIType AI)[] Rows =
    {
        ("NightTerror", "BaseCreature", "Night Terror", "a night terror corpse", 0x30C, 2963, 8000, -8000, AIType.AI_Mage),
        ("AbyssalAbomination", "BaseCreature", "an Abyssal Abomination", "an abyssal abomination corpse", 312, 769, 26000, -26000, AIType.AI_Mage),
        ("ArchDaemon", "BaseCreature", "an Arch Deamon", "a arch daemon corpse", 40, 1755, 24000, -24000, AIType.AI_Mage),
        ("BulbousPutrification", "BaseCreature", "a bulbous putrification", "a bulbous putrification corpse", 0x307, 0x55C, 0, 0, AIType.AI_Melee),
        ("DreadWarhorse", "BaseMount", "a dread warhorse", "a dread warhorse corpse", 0x74, 1175, 15000, -15000, AIType.AI_Mage),
        ("DreamWraith", "BaseCreature", "a Dream Wraith", "a dream wraith corpse", 740, 0, 4000, -4000, AIType.AI_Mage),
        ("EffetePutridGargoyle", "BaseCreature", "an Effete putrid gargoyle", "an effete putrid gargoyle corpse", 4, 0, 3500, -3500, AIType.AI_Mage),
        ("EffeteUndeadGargoyle", "BaseCreature", "an Effete Undead Gargoyle", "an  effete undead gargoyle corpse", 722, 0, 3500, -3500, AIType.AI_Melee),
        ("EnragedColossus", "BaseCreature", "Rising Colossus", "the remains of an enraged colossus", 829, 0, 0, 0, AIType.AI_Melee),
        ("EnragedEarthElemental", "BaseCreature", "Enraged Earth Elemental", "an earth elemental corpse", 14, 442, 3500, -3500, AIType.AI_Melee),
        ("GargoyleGuardian", "BaseCreature", "Abyss Guardian", "a gargoyle corpse", 0x2F3, 1153, 10000, -10000, AIType.AI_Mage),
        ("Grubber", "BaseCreature", "a grubber", "a grubber corpse", 270, 0, 1000, 0, AIType.AI_Animal),
        ("IceHound", "BaseCreature", "an ice hound", "an ice hound corpse", 98, 1153, 3400, -3400, AIType.AI_Melee),
        ("Lifestealer", "BaseCreature", "a lifestealer", "a lifestealer corpse", 303, 2606, 9500, -9500, AIType.AI_Mage),
        ("Mimic", "BaseCreature", "a mimic", "a mimic corpse", 729, 0, 0, 0, AIType.AI_Mage),
        ("MinotaurGeneral", "MinotaurCaptain", "a minotaur general", "a minotaur general corpse", 0x118, Unchecked, 18000, -18000, AIType.AI_Melee),
        ("OrcChopper", "BaseCreature", "an orc chopper", "an orcish corpse", 7, 0x96D, 4500, -4500, AIType.AI_Melee),
        ("Ortanord", "BaseCreature", "Ortanord", "an ortanord corpse", 58, 2071, 8000, -8000, AIType.AI_Melee),
        ("Palomino", "BaseMount", "a horse", "a horse corpse", 1408, 0, 300, 300, AIType.AI_Animal),
        ("PutridUndeadGargoyle", "BaseCreature", "a putrid undead gargoyle", "a putrid undead gargoyle corpse", 722, 1778, 3500, -3500, AIType.AI_Mage),
        ("RenegadeChangeling", "Changeling", "a renegade changeling", "a renegade changeling corpse", 264, Unchecked, 18900, -18900, AIType.AI_Mage),
        ("SAPixie", "BaseCreature", null, "a pixie corpse", 128, 0, 4000, -4000, AIType.AI_Mage),
        ("SkeletalDrake", "BaseCreature", "a skeletal drake", "a skeletal drake corpse", 104, 2101, 15000, -15000, AIType.AI_Mage),
        ("SpeckledScorpion", "Scorpion", "a speckled scorpion", "a speckled scorpion corpse", Unchecked, Unchecked, Unchecked, Unchecked, AIType.AI_Melee),
        ("Turkey", "BaseCreature", "a turkey", "an turkey corpse", 95, 0, 150, 0, AIType.AI_Animal),
        ("Wight", "BaseCreature", "a Wight", "a wight corpse", 252, 1153, 1500, -1500, AIType.AI_Mage),
        ("TigersClawMaster", "TigersClawThief", "Black Order Master", "a black order master corpse", Unchecked, Unchecked, 25000, -25000, AIType.AI_Melee),
        ("FireRabbit", "VorpalBunny", "a fire rabbit", "a hare corpse", Unchecked, 0x550, Unchecked, Unchecked, AIType.AI_Melee),
        ("TreefellowGuardian", "BaseCreature", "a Treefellow Guardian", "a treefellow guardian corpse", 301, 0, 500, 1500, AIType.AI_Mage)
    };

    private static readonly string[] LeafItems = { "TigerClawKey", "AnimalPheromone", "TreefellowWood" };

    private static Type Resolve(string name)
    {
        var t = AssemblyHandler.FindTypeByName(name);
        Assert.True(t != null, $"{name} resolves to no type: not ported");
        return t;
    }

    private static PlayerMobile NewPlayer(Point3D loc)
    {
        var pm = new PlayerMobile { Player = true };
        pm.AddItem(new Backpack());
        pm.MoveToWorld(loc, Map.Malas);
        pm.Hits = pm.HitsMax;
        return pm;
    }

    // Serialize through the generated Serialize and read back into a fresh instance built with the generated (Serial)
    // constructor: CC6 batch 8's shape.
    private static BaseCreature RoundTrip(BaseCreature original)
    {
        var buffer = new byte[65536];
        var writer = new BufferWriter(buffer, true);
        original.Serialize(writer);
        writer.Flush();

        var copy = (BaseCreature)Activator.CreateInstance(original.GetType(), original.Serial)!;
        copy.Deserialize(new BufferReader(buffer));
        return copy;
    }

    [Fact]
    public void EachCreatureResolvesAndConstructsWithItsServUOValues()
    {
        Assert.Equal(29, Rows.Length);
        Assert.Equal(29, Rows.Select(r => r.Type).Distinct().Count());

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
                Assert.False(string.IsNullOrEmpty(bc.Name), $"{row.Type} has no name"); // NameList "pixie"
            }

            Assert.Equal(row.Corpse, bc.CorpseName);
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
            var bc = (BaseCreature)Activator.CreateInstance(Resolve(row.Type))!;
            bc.MoveToWorld(new Point3D(1000, 1000, 0), Map.Malas);

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
    public void ASpawnerFillsWithEachCreature()
    {
        var where = new Point3D(1000, 1000, 0);

        foreach (var row in Rows)
        {
            var type = Resolve(row.Type);
            var spawner = new Spawner();
            spawner.MoveToWorld(where, Map.Malas);
            spawner.SpawnBounds = default; // no tile data in the host: spawn at the spawner's own location
            var entry = spawner.AddEntry(row.Type.ToLowerInvariant(), 100, 1, dotimer: false);

            try
            {
                Assert.True(spawner.Spawn(entry, out var flags), $"{row.Type}: Spawn returned false with flags {flags}");
                Assert.Equal(EntryFlags.None, flags);
                var spawned = Assert.IsAssignableFrom<BaseCreature>(Assert.Single(spawner.Spawned.Keys));
                Assert.Same(type, spawned.GetType());
                Assert.Equal(where, spawned.Location);
            }
            finally
            {
                spawner.Delete();
            }
        }
    }

    [Fact]
    public void TheWeaponAbilitiesAreServUOsSetWeaponAbilityCalls()
    {
        // ServUO's SetWeaponAbility, as pinned's GetWeaponAbility (port recipe, CC3). Sampled 200 times, so a pair shows
        // both members and a single shows only itself.
        (string Type, WeaponAbility[] Expected)[] cases =
        {
            ("AbyssalAbomination", new[] { WeaponAbility.MortalStrike, WeaponAbility.WhirlwindAttack }),
            ("IceHound", new[] { WeaponAbility.ParalyzingBlow }),
            ("OrcChopper", new[] { WeaponAbility.WhirlwindAttack, WeaponAbility.CrushingBlow }),
            // The deviation in Wight.cs: ServUO pairs Mortal Strike with Cold Wind, which pinned has no weapon ability for.
            ("Wight", new[] { WeaponAbility.MortalStrike }),
            ("TreefellowGuardian", new[] { WeaponAbility.Dismount })
        };

        foreach (var (typeName, expected) in cases)
        {
            var bc = (BaseCreature)Activator.CreateInstance(Resolve(typeName))!;
            var seen = Enumerable.Range(0, 200).Select(_ => bc.GetWeaponAbility()).Distinct().ToList();
            Assert.Equal(expected.OrderBy(a => Array.IndexOf(WeaponAbility.Abilities, a)),
                seen.OrderBy(a => Array.IndexOf(WeaponAbility.Abilities, a)));
            bc.Delete();
        }

        // And nothing else in the row gained one.
        foreach (var row in Rows.Where(r => cases.All(c => c.Type != r.Type)))
        {
            var bc = (BaseCreature)Activator.CreateInstance(Resolve(row.Type))!;
            var declared = bc.GetType().GetMethod("GetWeaponAbility", BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
            Assert.True(declared == null, $"{row.Type} declares a GetWeaponAbility ServUO does not give it");
            bc.Delete();
        }
    }

    [Fact]
    public void TheAbyssGuardianThrowsItsHatchetForFiftyPhysical()
    {
        // ServUO GargoyleGuardian.ThrowHatchet: AOS.Damage(to, this, 50, 100, 0, 0, 0, 0). A player with no armour has
        // 0 physical resistance, so 50 lands whole. Read on the tick it lands (D15).
        var guardian = (BaseCreature)Activator.CreateInstance(Resolve("GargoyleGuardian"))!;
        guardian.MoveToWorld(new Point3D(1000, 1000, 0), Map.Malas);
        Assert.True(guardian.Frozen);
        Assert.True(guardian.CantWalk);
        Assert.Equal(FightMode.None, guardian.FightMode);

        var pm = NewPlayer(new Point3D(1003, 1000, 0));
        pm.RawStr = 200; // enough hits that 50 cannot kill
        pm.Hits = pm.HitsMax;
        Assert.Equal(0, pm.PhysicalResistance);
        var before = pm.Hits;

        guardian.GetType().GetMethod("ThrowHatchet")!.Invoke(guardian, new object[] { pm });

        Assert.Equal(before - 50, pm.Hits);

        guardian.Delete();
        pm.Delete();
    }

    [Fact]
    public void TheOrcChopperSparesAKinMaskWearerAndBurnsTheMaskWhenAttacked()
    {
        var chopper = (BaseCreature)Activator.CreateInstance(Resolve("OrcChopper"))!;
        chopper.MoveToWorld(new Point3D(1000, 1000, 0), Map.Malas);
        Assert.Equal(OppositionGroup.SavagesAndOrcs, chopper.OppositionGroup);
        Assert.Same(InhumanSpeech.Orc, chopper.SpeechType);
        Assert.NotNull(chopper.Backpack?.FindItemByType<ExecutionersAxe>());

        var pm = NewPlayer(new Point3D(1001, 1000, 0));
        Assert.True(chopper.IsEnemy(pm));

        var mask = new OrcishKinMask();
        pm.AddItem(mask);
        Assert.Same(mask, pm.FindItemOnLayer(Layer.Helm));
        Assert.False(chopper.IsEnemy(pm));

        chopper.AggressiveAction(pm, false);
        Assert.True(mask.Deleted);
        Assert.True(chopper.IsEnemy(pm));

        chopper.Delete();
        pm.Delete();
    }

    [Fact]
    public void TheLeafItemsResolveAndTheMasterDropsItsKeyOnEveryDeath()
    {
        ShardTestClock.Arm(); // PeerlessKey schedules its slice on construction

        foreach (var name in LeafItems)
        {
            var t = Resolve(name);
            Assert.Equal("Server.Items." + name, t.FullName);
            Assert.Single(t.GetCustomAttributes(typeof(SerializationGeneratorAttribute), false));
            ((Item)Activator.CreateInstance(t)!).Delete();
        }

        // ServUO TigersClawMaster.OnDeath: the key every time, the sect badge half the time on top of the parent's.
        for (var i = 0; i < 5; i++)
        {
            var master = (BaseCreature)Activator.CreateInstance(Resolve("TigersClawMaster"))!;
            Assert.Equal("of the Serpent's Fang Sect", master.Title); // ServUO's, kept as written
            master.MoveToWorld(new Point3D(1000, 1000, 0), Map.Malas);
            master.Kill();

            var corpse = Assert.IsAssignableFrom<Corpse>(master.Corpse);
            var key = corpse.FindItemByType<PeerlessKey>();
            Assert.NotNull(key);
            Assert.Equal("TigerClawKey", key.GetType().Name);
            Assert.Equal(1074342, key.LabelNumber);
            Assert.Equal(105, key.Hue);
            Assert.Equal(LootType.Blessed, key.LootType);
            corpse.Delete();
        }
    }

    [Fact]
    public void ToxicElementalIsStockAcidElementalByAliasAndWasNotPorted()
    {
        // ServUO has both AcidElemental.cs and ToxicElemental.cs ("an acid elemental" each). Pinned aliases the second
        // name to the first (Mobiles/Monsters/Elemental/Magic/AcidElemental.cs:5), so a ServUO spawn entry or save naming
        // ToxicElemental already loads the stock creature. A port of our own would shadow that alias.
        Assert.Same(typeof(AcidElemental), AssemblyHandler.FindTypeByName("ToxicElemental"));
        Assert.Same(typeof(AcidElemental), AssemblyHandler.FindTypeByName("Server.Mobiles.ToxicElemental", fullName: true));
        Assert.Equal("an acid elemental", new AcidElemental().Name);
    }
}
