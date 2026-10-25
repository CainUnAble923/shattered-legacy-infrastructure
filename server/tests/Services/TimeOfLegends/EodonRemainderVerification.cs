// cc-P198: the Eodon remainder of the port list (Time of Legends creatures and items, no Myrmidex, no Shadowguard)
// and [GenEodon, which places the Valley of Eodon moongate that pinned lists as a destination and nothing places.
//
// Every expected value below was read off the ServUO file, not off our port, so a value copied wrong goes red here.
// What this file proves: each creature resolves, constructs with ServUO's values at pinned's perception and the
// Medium speed fallback (Q-008), carries its own generator attribute, survives a save and reload, and a spawner fills
// with it; each tribesman for all six tribes (its body, outfit, one 100% resistance, the rally and the yell, the
// aggressor-only enemy test); the Infernus's trail of fire; the hatchling's scutes; the cards, the vines, the scute and
// the wandering shaman; and [GenEodon: the gate where pinned's gump sends players, the vines, the 46 spawners, the
// dry run, the replace on a re-run, the skip beside another gate, and [DeleteEodon. cc-P458 adds the spawn weights,
// a real tame of the hatchling, the vines refusing a paralyzed climber, the Infernus through its combat callback and
// its one-second tick, and a re-run deleting the first run's creatures.
//
// Test-host facts this file meets (notes/port-recipe.md): hit points regenerate every 8 ms, so damage is read on the
// tick it lands (D15); everything with a timer is made after ShardTestClock.Arm() (D16); there is no tile data (D27),
// so nothing here asserts a worn layer, and a spawner's own location is used where a land tile would matter.
// Notes: shard-migration/notes/cc-P198-port-eodon-remainder.md.

using System;
using System.Collections.Generic;
using System.Linq;
using ModernUO.Serialization;
using Server;
using Server.Engines.Eodon;
using Server.Engines.Spawners;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.SkillHandlers;
using Server.Tests.Network;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class EodonRemainderVerification
{
    private readonly ITestOutputHelper _out;

    public EodonRemainderVerification(ITestOutputHelper output) => _out = output;

    private const int Unchecked = int.MinValue;

    // Not 1000,1000: the CC6 creature tests stand there on Ter Mur, and a corpse or mobile left here must not reach them.
    private static readonly Point3D Origin = new(200, 3900, 0);

    // (type, parent, ServUO Name (null: from a name list), corpse, Body, Hue, Fame, Karma, AI, FightMode).
    private static readonly (string Type, string Parent, string Name, string Corpse, int Body, int Hue, int Fame, int Karma, AIType AI, FightMode Mode)[] Rows =
    {
        ("Anchisaur", "BaseCreature", "an anchisaur", "an anchisaur corpse", 1292, 0, 8000, -8000, AIType.AI_Melee, FightMode.Closest),
        ("Gallusaurus", "BaseCreature", "a gallusaurus", "a gallusaurus corpse", 1286, 0, 8100, -8100, AIType.AI_Melee, FightMode.Closest),
        ("DesertScorpion", "BaseCreature", "a desert scorpion", "a desert scorpion corpse", 0x2CD, 0, 8100, -8100, AIType.AI_Melee, FightMode.Closest),
        ("GreaterPhoenix", "BaseCreature", "a greater phoenix", "a greater phoenix corpse", 832, 0, 10000, -10000, AIType.AI_Mage, FightMode.Closest),
        ("Infernus", "BaseCreature", "an infernus", "an infernus corpse", 0x9F, 1955, 10000, -10000, AIType.AI_Melee, FightMode.Closest),
        ("DragonTurtleHatchling", "BaseCreature", "a dragon turtle hatchling", "a dragon turtle hatchling corpse", 1294, 0, 16000, -16000, AIType.AI_Mage, FightMode.Aggressor),
        ("BritannianInfantry", "BaseCreature", null, "a human corpse", 0x190, 33779, 7500, 4500, AIType.AI_Melee, FightMode.Closest)
    };

    private static readonly string[] Tribesmen = { "TribeWarrior", "TribeShaman", "TribeChieftan" };

    private static readonly EodonTribe[] AllTribes =
        { EodonTribe.Jukari, EodonTribe.Kurak, EodonTribe.Barrab, EodonTribe.Barako, EodonTribe.Urali, EodonTribe.Sakkhra };

    private static Type Resolve(string name)
    {
        var t = AssemblyHandler.FindTypeByName(name);
        Assert.True(t != null, $"{name} resolves to no type: not ported");
        return t;
    }

    private static BaseCreature Make(string name) => (BaseCreature)Activator.CreateInstance(Resolve(name))!;

    private static BaseEodonTribesman MakeTribesman(string name, EodonTribe tribe) =>
        (BaseEodonTribesman)Activator.CreateInstance(Resolve(name), tribe)!;

    private static PlayerMobile NewPlayer(Point3D loc)
    {
        var pm = new PlayerMobile { Player = true, Young = false, RawStr = 200 }; // 150 hits: one fire tile cannot kill
        pm.AddItem(new Backpack());
        pm.MoveToWorld(loc, Map.TerMur);
        pm.Hits = pm.HitsMax;
        return pm;
    }

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

    private static void ClearEodon()
    {
        EodonGeneration.Delete();

        foreach (var item in World.Items.Values.ToList())
        {
            if (item is PublicMoongate or ClimbingVine && item.Map == Map.TerMur ||
                item is Spawner spawner && EodonSpawns.IsEodonSpawner(spawner))
            {
                item.Delete();
            }
        }
    }

    // ---- the creatures ---------------------------------------------------------------------------------------------

    [Fact]
    public void EachCreatureResolvesAndConstructsWithItsServUOValues()
    {
        foreach (var row in Rows)
        {
            var type = Resolve(row.Type);
            Assert.Equal("Server.Mobiles." + row.Type, type.FullName);
            Assert.Equal(row.Parent, type.BaseType!.Name);

            var bc = (BaseCreature)Activator.CreateInstance(type)!;
            _out.WriteLine($"{row.Type}: '{bc.Name}' body={(int)bc.Body} hue={bc.Hue} hits={bc.HitsMax} ai={bc.AI}");

            if (row.Name != null)
            {
                Assert.Equal(row.Name, bc.Name);
            }
            else
            {
                Assert.False(string.IsNullOrEmpty(bc.Name), $"{row.Type} has no name");
            }

            Assert.Equal(row.Corpse, bc.CorpseName);
            Assert.Equal(row.Body, (int)bc.Body);
            Assert.Equal(row.Hue, bc.Hue);
            Assert.Equal(row.Fame, bc.Fame);
            Assert.Equal(row.Karma, bc.Karma);
            Assert.Equal(row.AI, bc.AI);
            Assert.Equal(row.Mode, bc.FightMode);

            Assert.Equal(BaseCreature.DefaultRangePerception, bc.RangePerception);
            Assert.Equal(0.25, bc.ActiveSpeed);
            Assert.Equal(0.5, bc.PassiveSpeed);

            Assert.Single(type.GetCustomAttributes(typeof(SerializationGeneratorAttribute), false));
            Assert.Same(type, type.GetConstructor(new[] { typeof(Serial) })!.DeclaringType);

            bc.Delete();
        }
    }

    [Fact]
    public void TheCreaturesCarryServUOsNumbers()
    {
        var anchisaur = new Anchisaur();
        Assert.InRange(anchisaur.HitsMax, 2663, 3718);
        Assert.InRange(anchisaur.RawStr, 441, 511);
        Assert.Equal(6, anchisaur.Meat);
        Assert.Equal(11, anchisaur.Hides);
        Assert.Equal(1, anchisaur.TreasureMapLevel);
        Assert.False(anchisaur.Tamable);

        var gallusaurus = new Gallusaurus();
        Assert.True(gallusaurus.Tamable);
        Assert.Equal(3, gallusaurus.ControlSlots);
        Assert.Equal(102.0, gallusaurus.MinTameSkill);
        Assert.True(gallusaurus.CanAngerOnTame);
        Assert.InRange(gallusaurus.Skills.Bushido.Base, 110.0, 120.0);

        // ServUO sets Physical 60, then Physical 40: the second call wins and nothing else is set.
        var scorpion = new DesertScorpion();
        Assert.Equal(40, scorpion.PhysicalDamage);
        Assert.Equal(0, scorpion.FireDamage + scorpion.ColdDamage + scorpion.PoisonDamage + scorpion.EnergyDamage);
        Assert.Same(Poison.Lethal, scorpion.HitPoison);
        Assert.Same(Poison.Lethal, scorpion.PoisonImmune);

        var phoenix = new GreaterPhoenix();
        Assert.Equal(40, phoenix.PhysicalDamage);
        Assert.Equal(60, phoenix.FireDamage);
        Assert.InRange(phoenix.Backpack?.GetAmount(typeof(Gold)) ?? 0, 500, 700);

        var infernus = new Infernus();
        Assert.Equal(100, infernus.FireDamage);
        Assert.Equal(5, infernus.TreasureMapLevel);

        var hatchling = new DragonTurtleHatchling();
        Assert.True(hatchling.Tamable);
        // ServUO's 5 meets Pet Training's definition (4-5): the constructor's assignment clamps to the minimum, 4, or 3
        // when its rolls came out low (cc-P130's ControlSlots setter, ServUO BaseCreature.cs:3843-3861).
        Assert.InRange(hatchling.ControlSlots, 3, 4);
        Assert.Equal(104.7, hatchling.MinTameSkill);
        Assert.Equal(FoodType.Fish, hatchling.FavoriteFood);

        var infantry = new BritannianInfantry();
        Assert.Equal("the Britannian", infantry.Title);
        Assert.Equal(2400, infantry.HitsMax);
        Assert.True(infantry.CanHeal);
        Assert.False(infantry.ClickTitle);
        // PackGold(250-300) plus whatever gold LootPack.Rich rolled into the same pack at construction.
        Assert.True((infantry.Backpack?.GetAmount(typeof(Gold)) ?? 0) >= 250, "the infantry packs at least 250 gold");
        Assert.Contains(infantry.Items, i => i is Halberd { LootType: LootType.Blessed, Movable: false });
        Assert.Contains(infantry.Items, i => i is Kilt { Hue: 1175 });
        Assert.Contains(infantry.Items, i => i is BodySash { Hue: 1157 });

        foreach (var bc in new BaseCreature[] { anchisaur, gallusaurus, scorpion, phoenix, infernus, hatchling, infantry })
        {
            bc.Delete();
        }
    }

    [Fact]
    public void TheAbilitiesAreServUOs()
    {
        Assert.True(new Gallusaurus().HasAbility(SpecialAbility.GraspingClaw));
        Assert.True(new GreaterPhoenix().HasAbility(SpecialAbility.GraspingClaw));
        Assert.True(new DragonTurtleHatchling().HasAbility(SpecialAbility.DragonBreath));

        Assert.Same(WeaponAbility.Block, new Gallusaurus().GetWeaponAbility());
        Assert.Same(WeaponAbility.BleedAttack, new DragonTurtleHatchling().GetWeaponAbility());

        var seen = new HashSet<WeaponAbility>();
        var anchisaur = new Anchisaur();

        for (var i = 0; i < 200; i++)
        {
            seen.Add(anchisaur.GetWeaponAbility());
        }

        Assert.Equal(new HashSet<WeaponAbility> { WeaponAbility.Disarm, WeaponAbility.ParalyzingBlow }, seen);
        anchisaur.Delete();
    }

    [Fact]
    public void EachCreatureSurvivesASaveAndReload()
    {
        var all = Rows.Select(r => Make(r.Type)).ToList();
        all.Add(new TribeWarrior(EodonTribe.Sakkhra));
        all.Add(new TribeShaman(EodonTribe.Urali));
        all.Add(new TribeChieftan(EodonTribe.Barrab));
        all.Add(new WanderingShaman());

        foreach (var bc in all)
        {
            bc.MoveToWorld(Origin, Map.TerMur);
            var copy = RoundTrip(bc);

            Assert.Same(bc.GetType(), copy.GetType());
            Assert.Equal(bc.Name, copy.Name);
            Assert.Equal(bc.Body, copy.Body);
            Assert.Equal(bc.Hue, copy.Hue);
            Assert.Equal(bc.HitsMax, copy.HitsMax);
            Assert.Equal(bc.Fame, copy.Fame);

            if (bc is BaseEodonTribesman tribesman)
            {
                // The one persisted field, written non-default (Jukari is 0).
                Assert.NotEqual(EodonTribe.Jukari, tribesman.TribeType);
                Assert.Equal(tribesman.TribeType, ((BaseEodonTribesman)copy).TribeType);
            }

            bc.Delete();
            copy.Delete();
        }
    }

    [Fact]
    public void ASpawnerFillsWithEachCreatureAndEachTribe()
    {
        var cases = Rows.Select(r => (r.Type, (string)null)).ToList();
        cases.Add(("WanderingShaman", null));

        foreach (var name in Tribesmen)
        {
            foreach (var tribe in AllTribes)
            {
                cases.Add((name, tribe.ToString()));
            }
        }

        foreach (var (typeName, parameter) in cases)
        {
            var spawner = new Spawner();
            spawner.MoveToWorld(Origin, Map.TerMur);
            spawner.SpawnBounds = default; // no tile data in the host: spawn at the spawner's own location
            var entry = spawner.AddEntry(typeName.ToLowerInvariant(), 100, 1, false, null, parameter);

            try
            {
                Assert.True(spawner.Spawn(entry, out var flags), $"{typeName} {parameter}: Spawn returned false, {flags}");
                Assert.Equal(EntryFlags.None, flags);
                var spawned = Assert.IsAssignableFrom<BaseCreature>(Assert.Single(spawner.Spawned.Keys));
                Assert.Same(Resolve(typeName), spawned.GetType());

                if (parameter != null)
                {
                    Assert.Equal(parameter, ((BaseEodonTribesman)spawned).TribeType.ToString());
                }
            }
            finally
            {
                spawner.Delete();
            }
        }
    }

    // ---- the tribesmen ---------------------------------------------------------------------------------------------

    // ServUO BaseEodonTribesman's constructor: the one 100% resistance per tribe (Barako has none). The base value, the
    // one SetResistance writes: the displayed one adds the tribe's worn armour.
    private static int? FullResistance(EodonTribe tribe, BaseCreature bc) =>
        tribe switch
        {
            EodonTribe.Jukari  => bc.BaseFireResistance,
            EodonTribe.Kurak   => bc.BaseColdResistance,
            EodonTribe.Barrab  => bc.BasePoisonResistance,
            EodonTribe.Urali   => bc.BaseEnergyResistance,
            EodonTribe.Sakkhra => bc.BasePhysicalResistance,
            _                  => null
        };

    [Fact]
    public void EachTribesmanIsBuiltForItsTribe()
    {
        foreach (var name in Tribesmen)
        {
            foreach (var tribe in AllTribes)
            {
                var t = MakeTribesman(name, tribe);
                _out.WriteLine($"{name} {tribe}: '{t.Name}' body={(int)t.Body} hue={t.Hue} hits={t.HitsMax}");

                Assert.Equal(tribe, t.TribeType);
                Assert.True(t.InitialInnocent);
                Assert.Equal(2, t.TreasureMapLevel);
                Assert.Equal(8000, t.Karma);

                var role = name switch
                {
                    "TribeWarrior" => "warrior",
                    "TribeShaman"  => "shaman",
                    _              => "chieftan" // ServUO reads Female before setting it: never "chieftess"
                };
                Assert.EndsWith($" the {tribe} {role}", t.Name);
                Assert.False(t.Name.StartsWith(" "), "no savage name: names.json not read");

                var hue = tribe switch
                {
                    EodonTribe.Jukari  => 34723,
                    EodonTribe.Kurak   => 33960,
                    EodonTribe.Barrab  => 34214,
                    EodonTribe.Barako  => 35187,
                    EodonTribe.Urali   => 35356,
                    _                  => 34894
                };
                Assert.Equal(hue, t.Hue);

                if (tribe == EodonTribe.Urali)
                {
                    Assert.Same(Race.Elf, t.Race);
                    Assert.Equal(name == "TribeShaman" ? 0x25D : 0x25E, (int)t.Body);
                }
                else
                {
                    Assert.Contains((int)t.Body, new[] { 0x190, 0x191 });
                }

                if (FullResistance(tribe, t) is { } full)
                {
                    Assert.Equal(100, full);
                }
                else
                {
                    Assert.True(t.BasePhysicalResistance < 100 && t.BaseFireResistance < 100 && t.BaseColdResistance < 100 &&
                                t.BasePoisonResistance < 100 && t.BaseEnergyResistance < 100, "Barako has no full resistance");
                }

                Assert.Same(tribe == EodonTribe.Barrab ? Poison.Deadly : null, t.PoisonImmune);

                var hits = name switch { "TribeChieftan" => 4500, _ => 2500 };
                Assert.Equal(hits, t.HitsMax);
                Assert.Equal(name == "TribeChieftan" ? 18000 : 12000, t.Fame);

                // Every carried item is unmovable; a weapon, when there is one, is blessed.
                Assert.All(t.Items.Where(i => i is not Backpack), i => Assert.False(i.Movable, $"{i.GetType().Name} movable"));
                Assert.All(t.Items.OfType<BaseWeapon>(), w => Assert.Equal(LootType.Blessed, w.LootType));

                t.Delete();
            }
        }
    }

    [Fact]
    public void EachTribeCarriesServUOsWeapon()
    {
        (string Name, EodonTribe Tribe, Type Weapon, int Hue)[] cases =
        {
            ("TribeWarrior", EodonTribe.Jukari, typeof(Pickaxe), 1175),
            ("TribeWarrior", EodonTribe.Kurak, typeof(Tekagi), 0),
            ("TribeWarrior", EodonTribe.Barrab, typeof(Spear), 0),
            ("TribeWarrior", EodonTribe.Sakkhra, typeof(Bow), 2125),
            ("TribeShaman", EodonTribe.Jukari, typeof(WildStaff), 1933),
            ("TribeShaman", EodonTribe.Urali, typeof(WildStaff), 2576),
            ("TribeChieftan", EodonTribe.Jukari, typeof(Bokuto), 1175),
            ("TribeChieftan", EodonTribe.Barako, typeof(Scepter), 2407),
            ("TribeChieftan", EodonTribe.Sakkhra, typeof(Yumi), 2118)
        };

        foreach (var (name, tribe, weaponType, hue) in cases)
        {
            var t = MakeTribesman(name, tribe);
            var weapon = Assert.Single(t.Items.OfType<BaseWeapon>());
            Assert.Same(weaponType, weapon.GetType());
            Assert.Equal(hue, weapon.Hue);
            t.Delete();
        }

        // The Urali warrior and chieftain fight unarmed behind a metal shield.
        foreach (var name in new[] { "TribeWarrior", "TribeChieftan" })
        {
            var urali = MakeTribesman(name, EodonTribe.Urali);
            Assert.Empty(urali.Items.OfType<BaseWeapon>());
            Assert.Contains(urali.Items, i => i is MetalShield { Hue: 2576 });
            urali.Delete();
        }

        var shaman = new TribeShaman(EodonTribe.Kurak);
        Assert.Equal(7, shaman.RangeFight);
        Assert.InRange(shaman.Backpack.GetAmount(typeof(Gold)), 60, 70);
        Assert.InRange(shaman.Backpack.GetAmount(typeof(Bandage)), 3, 5);
        shaman.Delete();
    }

    [Fact]
    public void AnAttackedTribesmanRalliesItsOwnTribeAndFightsOnlyAggressors()
    {
        ShardTestClock.Arm();

        var a = new TribeWarrior(EodonTribe.Kurak);
        var b = new TribeShaman(EodonTribe.Kurak);
        var other = new TribeWarrior(EodonTribe.Jukari);
        var pm = NewPlayer(Origin);
        var bystander = NewPlayer(new Point3D(Origin.X + 3, Origin.Y, Origin.Z));

        a.MoveToWorld(new Point3D(Origin.X + 1, Origin.Y, Origin.Z), Map.TerMur);
        b.MoveToWorld(new Point3D(Origin.X + 4, Origin.Y + 2, Origin.Z), Map.TerMur);
        other.MoveToWorld(new Point3D(Origin.X + 2, Origin.Y + 4, Origin.Z), Map.TerMur);

        try
        {
            Assert.False(a.IsEnemy(pm), "an innocent tribesman is nobody's enemy before it is attacked");
            Assert.False(b.HasYelled);

            pm.DoHarmful(a);

            Assert.True(a.IsEnemy(pm), "its aggressor is its enemy");
            Assert.False(a.IsEnemy(bystander), "a bystander is not");

            Assert.Same(pm, b.Combatant);
            Assert.True(b.Warmode);
            Assert.True(b.HasYelled, "the rallied tribesman yells");
            Assert.Null(other.Combatant);
            Assert.False(other.HasYelled);

            // The yell clears after 3-6 minutes (ServUO's HasYelled timer).
            ShardTestClock.AdvanceSeconds(361);
            Assert.False(b.HasYelled);
        }
        finally
        {
            a.Delete();
            b.Delete();
            other.Delete();
            pm.Delete();
            bystander.Delete();
        }
    }

    // ---- what the creatures do -------------------------------------------------------------------------------------

    [Fact]
    public void TheInfernusLaysATrailOfFireThatBurns()
    {
        ShardTestClock.Arm();

        var infernus = new Infernus();
        var pm = NewPlayer(Origin);
        infernus.MoveToWorld(new Point3D(Origin.X + 3, Origin.Y, Origin.Z), Map.TerMur);

        try
        {
            // ServUO's "centre always" test compares world coordinates (Infernus.cs's header), so each of the nine tiles
            // is a coin flip and one drop in 512 places nothing: drop until something burns.
            for (var i = 0; i < 10 && !World.Items.Values.OfType<Infernus.FireItem>().Any(f => !f.Deleted && f.Owner == infernus); i++)
            {
                infernus.DropFire(pm);
            }

            var fires = World.Items.Values.OfType<Infernus.FireItem>().Where(f => !f.Deleted && f.Owner == infernus).ToList();
            Assert.NotEmpty(fires);
            Assert.All(fires, f => Assert.True(Math.Abs(f.X - pm.X) <= 1 && Math.Abs(f.Y - pm.Y) <= 1, $"fire at {f.Location}"));
            Assert.All(fires, f => Assert.Equal(0x19AB, f.ItemID));

            var fire = fires[0];
            var before = pm.Hits;
            fire.OnMoveOver(pm);
            var taken = before - pm.Hits;
            _out.WriteLine($"fire: {fires.Count} tiles, took {taken} of {before}");
            Assert.InRange(taken, 50, 85);

            // A fire does not survive a reload (ServUO's wrote nothing at all): it deletes itself once the load is done.
            var copy = RoundTrip(fire);

            // It burns out after 30-40 seconds.
            ShardTestClock.AdvanceSeconds(41);
            Assert.True(copy.Deleted, "a reloaded fire deletes itself");
            Assert.All(fires, f => Assert.True(f.Deleted, "the fire outlived its 40 seconds"));
        }
        finally
        {
            foreach (var f in World.Items.Values.OfType<Infernus.FireItem>().ToList())
            {
                f.Delete();
            }

            infernus.Delete();
            pm.Delete();
        }
    }

    // cc-P458 (P407 #3): the trail through the AI's own callback, its cooldown, and a target standing still on a fire.
    [Fact]
    public void TheInfernusDropsFireFromCombatOnACooldownAndItBurnsWhoeverStandsInIt()
    {
        ShardTestClock.Arm();

        var infernus = new Infernus { CantWalk = true };
        var pm = NewPlayer(Origin);
        infernus.MoveToWorld(new Point3D(Origin.X + 3, Origin.Y, Origin.Z), Map.TerMur);

        List<Infernus.FireItem> Fires() =>
            World.Items.Values.OfType<Infernus.FireItem>().Where(f => !f.Deleted && f.Owner == infernus).ToList();

        try
        {
            infernus.Combatant = pm;

            // One drop in 512 places nothing (Infernus.cs's header): wait out the cooldown and ask again.
            infernus.OnActionCombat();
            for (var i = 0; i < 5 && Fires().Count == 0; i++)
            {
                ShardTestClock.AdvanceSeconds(16);
                infernus.Combatant = pm;
                infernus.OnActionCombat();
            }

            var first = Fires();
            Assert.NotEmpty(first);
            Assert.All(first, f => Assert.True(Math.Abs(f.X - pm.X) <= 1 && Math.Abs(f.Y - pm.Y) <= 1, $"fire at {f.Location}"));

            // The cooldown: 10-15 seconds before the next drop.
            infernus.OnActionCombat();
            Assert.Equal(first.Count, Fires().Count);

            // A target out of range (12 tiles) gets nothing, cooldown or not.
            ShardTestClock.AdvanceSeconds(16);
            var far = NewPlayer(new Point3D(Origin.X + 20, Origin.Y, Origin.Z));
            infernus.Combatant = far;
            infernus.OnActionCombat();
            Assert.DoesNotContain(Fires(), f => Math.Abs(f.X - far.X) <= 1 && Math.Abs(f.Y - far.Y) <= 1);
            far.Delete();

            // After the cooldown, a second drop.
            var before = Fires().Count;
            for (var i = 0; i < 5 && Fires().Count == before; i++)
            {
                infernus.Combatant = pm;
                infernus.OnActionCombat();
                if (Fires().Count == before)
                {
                    ShardTestClock.AdvanceSeconds(16);
                }
            }

            Assert.True(Fires().Count > before, "no second drop after the cooldown");

            // Standing still on a fire: the one-second tick burns (hits regenerate in the host, so the proof is the
            // harmful act and the damage entry the hit leaves).
            var stander = NewPlayer(Fires()[0].Location);
            Assert.Null(stander.FindDamageEntryFor(infernus));
            ShardTestClock.AdvanceSeconds(1.1);
            Assert.NotNull(stander.FindDamageEntryFor(infernus));
            stander.Delete();
        }
        finally
        {
            foreach (var f in World.Items.Values.OfType<Infernus.FireItem>().ToList())
            {
                f.Delete();
            }

            infernus.Delete();
            pm.Delete();
        }
    }

    [Fact]
    public void AWildHatchlingCarvesFourScutes()
    {
        if (Mobile.CreateCorpseHandler == null)
        {
            Corpse.Initialize();
        }

        var pm = NewPlayer(Origin);
        var wild = new DragonTurtleHatchling();
        wild.MoveToWorld(Origin, Map.TerMur);
        var corpse = new Corpse(wild, new List<Item>());
        corpse.MoveToWorld(Origin, Map.TerMur);

        try
        {
            wild.OnCarve(pm, corpse, new Dagger());
            var scute = Assert.Single(corpse.Items.OfType<DragonTurtleScute>());
            Assert.Equal(4, scute.Amount);

            var tame = new DragonTurtleHatchling { Controlled = true, ControlMaster = pm };
            var tameCorpse = new Corpse(tame, new List<Item>());
            tame.OnCarve(pm, tameCorpse, new Dagger());
            Assert.Empty(tameCorpse.Items.OfType<DragonTurtleScute>());
            tameCorpse.Delete();
            tame.Delete();
        }
        finally
        {
            corpse.Delete();
            wild.Delete();
            pm.Delete();
        }
    }

    private static Dictionary<SkillName, double> TrainedSkills(Mobile m) =>
        Enumerable.Range(0, m.Skills.Length).Select(i => m.Skills[i]).Where(s => s.Base > 0)
            .ToDictionary(s => s.SkillName, s => s.Base);

    // cc-P458 (P407 #2, P428 #1): a real tame through pinned's AnimalTaming and the patch's hook. ServUO's first-tame
    // body: every skill above zero keeps 45% of what taming left it (pinned's 90%) and caps at 100; the stats stay.
    [Fact]
    public void ATamedHatchlingLosesSkillNotStatsAndOnlyOnTheFirstTame()
    {
        ShardTestClock.Arm();

        var pm = NewPlayer(Origin);
        var ns = PacketTestUtilities.CreateTestNetState();
        pm.NetState = ns;
        ns.Mobile = pm;
        pm.Skills.AnimalTaming.Cap = 160.0; // past the top of the 104.7 roll (CheckTargetSkill 104.6-154.6): a sure tame
        pm.Skills.AnimalTaming.Base = 160.0;
        pm.Skills.AnimalLore.Cap = 120.0;
        pm.Skills.AnimalLore.Base = 120.0;

        // Rooted, as the Pet Training tame fact's bear: the twenty seconds must not walk it out of range.
        var hatchling = new DragonTurtleHatchling { CantWalk = true };
        hatchling.MoveToWorld(new Point3D(Origin.X + 1, Origin.Y, Origin.Z), Map.TerMur);

        try
        {
            var wild = TrainedSkills(hatchling);
            var (str, dex, intel, hits) = (hatchling.RawStr, hatchling.RawDex, hatchling.RawInt, hatchling.HitsMaxSeed);
            Assert.NotEmpty(wild);

            AnimalTaming.OnUse(pm);
            pm.Target.Invoke(pm, hatchling);
            ShardTestClock.AdvanceSeconds(20);

            Assert.True(hatchling.Controlled, "a 160 tamer tames a hatchling");
            Assert.Same(pm, hatchling.ControlMaster);

            foreach (var (name, was) in wild)
            {
                var sk = hatchling.Skills[name];
                _out.WriteLine($"{name}: wild {was:F1}, tamed {sk.Base:F1} cap {sk.Cap:F1}");
                Assert.InRange(sk.Base, was * 0.9 * 0.45 - 0.25, was * 0.9 * 0.45 + 0.05);
                Assert.Equal(100.0, sk.Cap);
            }

            Assert.Equal((str, dex, intel, hits), (hatchling.RawStr, hatchling.RawDex, hatchling.RawInt, hatchling.HitsMaxSeed));

            Assert.False(hatchling.StatLossAfterTame, "no substitute half-stat cut");

            // Released and tamed back by the same owner: "That wasn't even challenging", and no second cut.
            var tamed = TrainedSkills(hatchling);
            hatchling.SetControlMaster(null);
            Assert.False(hatchling.Controlled);

            AnimalTaming.OnUse(pm);
            pm.Target.Invoke(pm, hatchling);
            ShardTestClock.AdvanceSeconds(20);

            Assert.True(hatchling.Controlled, "the owner tames it back");
            Assert.All(tamed, kv => Assert.Equal(kv.Value, hatchling.Skills[kv.Key].Base));
            Assert.Equal((str, dex, intel, hits), (hatchling.RawStr, hatchling.RawDex, hatchling.RawInt, hatchling.HitsMaxSeed));
        }
        finally
        {
            hatchling.Delete();
            pm.Delete();
        }
    }

    [Fact]
    public void TheWanderingShamanIsAHealerInTribalDress()
    {
        var type = Resolve("WanderingShaman");
        Assert.Equal("WanderingHealer", type.BaseType!.Name);

        var shaman = new WanderingShaman();

        try
        {
            Assert.Equal("the wandering shaman", shaman.Title);
            Assert.False(string.IsNullOrEmpty(shaman.Name));
            Assert.Equal(0, shaman.Hue);
            Assert.Equal(0, shaman.HairItemID);
            Assert.Equal(0, shaman.FacialHairItemID);

            // Only the mask is clothing: the robe and the rest of the healer's outfit are gone.
            var clothing = shaman.Items.OfType<BaseClothing>().ToList();
            var mask = Assert.IsType<TribalMask>(Assert.Single(clothing));
            Assert.Equal(2500, mask.Hue);
            Assert.Contains(shaman.Items, i => i is BoneArms);
            Assert.Contains(shaman.Items, i => i is BoneLegs);
            Assert.Contains(shaman.Items, i => i is GnarledStaff);
        }
        finally
        {
            shaman.Delete();
        }
    }

    // ---- the items -------------------------------------------------------------------------------------------------

    [Fact]
    public void TheItemsConstructAndSurviveASave()
    {
        var scute = new DragonTurtleScute(5);
        Assert.Equal(0x9BCE, scute.ItemID);
        Assert.Equal(1123910, scute.LabelNumber);
        Assert.True(scute.Stackable);
        Assert.Equal(1.0, scute.Weight);
        Assert.Equal(1123910, ((ICommodity)scute).DescriptionNumber);
        Assert.True(((ICommodity)scute).IsDeedable);
        Assert.Equal(5, RoundTrip(scute).Amount);

        var card = new CardOfSemidar(CardOfSemidar.CardType.Krampus);
        Assert.Equal(0x9C14, card.ItemID);
        Assert.Equal(1156395, card.LabelNumber);
        Assert.Equal(CardOfSemidar.CardType.Krampus, RoundTrip(card).Type);

        var vine = new ClimbingVine(new Point3D(679, 1757, 100));
        Assert.Equal(0x1AA1, vine.ItemID);
        Assert.Equal(1023307, vine.LabelNumber);
        Assert.Equal(new Point3D(679, 1757, 100), RoundTrip(vine).ClimbLocation);

        foreach (var item in new Item[] { scute, card, vine })
        {
            Assert.Single(item.GetType().GetCustomAttributes(typeof(SerializationGeneratorAttribute), false));
            item.Delete();
        }
    }

    [Fact]
    public void EachCardShowsItsOwnPictureAndName()
    {
        // ServUO: gump 39904 + type (Krampus 39914); title 1156396 + type, Rafkin 1156562, Minax 1156981, Krampus 1158799.
        (CardOfSemidar.CardType Type, int Gump, int Title)[] cases =
        {
            (CardOfSemidar.CardType.Dupre, 39904, 1156396),
            (CardOfSemidar.CardType.Nystul, 39905, 1156397),
            (CardOfSemidar.CardType.Shamino, 39906, 1156398),
            (CardOfSemidar.CardType.Juonar, 39907, 1156399),
            (CardOfSemidar.CardType.ProfessorRafkin, 39908, 1156562),
            (CardOfSemidar.CardType.Minax, 39909, 1156981),
            (CardOfSemidar.CardType.Krampus, 39914, 1158799)
        };

        foreach (var (type, gump, title) in cases)
        {
            var card = new CardOfSemidar(type);
            Assert.Equal(gump, card.PictureGumpId);
            Assert.Equal(title, card.TitleNumber);
            card.Delete();
        }

        // The default constructor deals one of the first four.
        for (var i = 0; i < 100; i++)
        {
            var card = new CardOfSemidar();
            Assert.InRange((int)card.Type, 0, 3);
            card.Delete();
        }
    }

    [Fact]
    public void TheVineClimbsOnlyFromBesideAndBelowIt()
    {
        var top = new Point3D(679, 1757, 100);
        var vine = new ClimbingVine(top) { Movable = false };
        vine.MoveToWorld(new Point3D(678, 1759, 40), Map.TerMur);

        var near = NewPlayer(new Point3D(678, 1760, 40));
        var far = NewPlayer(new Point3D(678, 1765, 40));
        var above = NewPlayer(new Point3D(677, 1759, 60));

        try
        {
            vine.OnDoubleClick(far);
            Assert.Equal(new Point3D(678, 1765, 40), far.Location);

            vine.OnDoubleClick(above);
            Assert.Equal(new Point3D(677, 1759, 60), above.Location);

            vine.OnDoubleClick(near);
            Assert.Equal(top, near.Location);
            Assert.Same(Map.TerMur, near.Map);

            var bare = new ClimbingVine();
            bare.MoveToWorld(new Point3D(678, 1761, 40), Map.TerMur);
            var other = NewPlayer(new Point3D(678, 1762, 40));
            bare.OnDoubleClick(other);
            Assert.Equal(new Point3D(678, 1762, 40), other.Location);
            bare.Delete();
            other.Delete();
        }
        finally
        {
            vine.Delete();
            near.Delete();
            far.Delete();
            above.Delete();
        }
    }

    private sealed class CastingSpell : ISpell
    {
        public bool BlocksMovement => true;
        public bool IsCasting => true;
        public void OnCasterHurt() { }
        public void OnCasterKilled() { }
        public void OnConnectionChanged() { }
        public bool OnCasterMoving(Direction d) => false;
        public bool OnCasterEquipping(Item item) => false;
        public bool OnCasterUsingObject(IEntity entity) => false;
        public bool OnCastInTown(Region r) => true;
        public void FinishSequence() { }
    }

    // cc-P458 (P428 #2): a paralyzed, frozen or casting player in range stays at the foot of the cliff.
    [Fact]
    public void TheVineRefusesAParalyzedFrozenOrCastingClimber()
    {
        var top = new Point3D(679, 1757, 100);
        var vine = new ClimbingVine(top) { Movable = false };
        vine.MoveToWorld(new Point3D(678, 1759, 40), Map.TerMur);

        var foot = new Point3D(678, 1760, 40);
        var paralyzed = NewPlayer(foot);
        var frozen = NewPlayer(foot);
        var casting = NewPlayer(foot);
        var free = NewPlayer(foot);

        try
        {
            paralyzed.Paralyzed = true;
            frozen.Frozen = true;
            casting.Spell = new CastingSpell();

            foreach (var pm in new[] { paralyzed, frozen, casting })
            {
                vine.OnDoubleClick(pm);
                Assert.Equal(foot, pm.Location);
            }

            vine.OnDoubleClick(free);
            Assert.Equal(top, free.Location);
        }
        finally
        {
            casting.Spell = null;
            vine.Delete();
            paralyzed.Delete();
            frozen.Delete();
            casting.Delete();
            free.Delete();
        }
    }

    // ---- [GenEodon -------------------------------------------------------------------------------------------------

    [Fact]
    public void PinnedSendsTOLPlayersToTheSpotTheGateGoesOn()
    {
        var entry = Assert.Single(PMList.TerMurEodon.Entries, e => e.Number == 1156262); // Valley of Eodon
        Assert.Equal(new Point3D(719, 1863, 40), entry.Location);
        Assert.Equal(entry.Location, EodonGeneration.MoongateLocation);
        Assert.Same(Map.TerMur, PMList.TerMurEodon.Map);
    }

    [Fact]
    public void TheDryRunPlacesNothing()
    {
        ClearEodon();

        try
        {
            var before = World.Items.Count;
            var lines = EodonGeneration.Run(dryRun: true);
            foreach (var l in lines)
            {
                _out.WriteLine(l);
            }

            Assert.Equal(before, World.Items.Count);
            Assert.Contains(lines, l => l.Contains("Dry run"));
            Assert.Contains(lines, l => l.Contains("Valley of Eodon public moongate at (719, 1863, 40)"));
            Assert.Contains(lines, l => l.Contains("46 spawners"));
        }
        finally
        {
            ClearEodon();
        }
    }

    [Fact]
    public void GenEodonPlacesTheGateTheVinesAndTheSpawnersAndReplacesThemOnARerun()
    {
        ClearEodon();

        try
        {
            foreach (var l in EodonGeneration.Run(dryRun: false))
            {
                _out.WriteLine(l);
            }

            var gate = Assert.Single(World.Items.Values.OfType<PublicMoongate>(), g => g.Map == Map.TerMur && !g.Deleted);
            Assert.Equal(new Point3D(719, 1863, 40), gate.Location);
            Assert.False(gate.Movable);

            var vines = World.Items.Values.OfType<ClimbingVine>().Where(v => !v.Deleted).ToList();
            Assert.Equal(2, vines.Count);
            Assert.All(vines, v => Assert.Equal(new Point3D(679, 1757, 100), v.ClimbLocation));
            Assert.All(vines, v => Assert.False(v.Movable));
            Assert.Equal(new[] { 40, 60 }, vines.Select(v => v.Z).OrderBy(z => z).ToArray());
            Assert.All(vines, v => Assert.Equal(new Point2D(678, 1759), new Point2D(v.X, v.Y)));

            var spawners = World.Items.Values.OfType<Spawner>().Where(s => !s.Deleted && EodonSpawns.IsEodonSpawner(s)).ToList();
            Assert.Equal(46, spawners.Count);
            Assert.All(spawners, s => Assert.Same(Map.TerMur, s.Map));

            var placed = EodonGeneration.PlacedCount();
            Assert.Equal(1 + 2 + 46, placed);

            // A second run replaces, it does not double.
            EodonGeneration.Run(dryRun: false);
            Assert.Single(World.Items.Values.OfType<PublicMoongate>(), g => g.Map == Map.TerMur && !g.Deleted);
            Assert.Equal(2, World.Items.Values.OfType<ClimbingVine>().Count(v => !v.Deleted));
            Assert.Equal(46, World.Items.Values.OfType<Spawner>().Count(s => !s.Deleted && EodonSpawns.IsEodonSpawner(s)));
            Assert.True(gate.Deleted, "the first run's gate was replaced");

            Assert.Equal(placed, EodonGeneration.Delete());
            Assert.DoesNotContain(World.Items.Values.OfType<PublicMoongate>(), g => g.Map == Map.TerMur && !g.Deleted);
        }
        finally
        {
            ClearEodon();
        }
    }

    [Fact]
    public void TheStatusLineSaysWhetherTheGateStands()
    {
        // The start-up line and the console's "eodon status": what a log shows about a world.
        ClearEodon();

        try
        {
            var before = Assert.Single(EodonGeneration.Status());
            Assert.Contains("0 objects placed by GenEodon", before);
            Assert.Contains("MISSING", before);

            EodonGeneration.Run(dryRun: false);
            var after = Assert.Single(EodonGeneration.Status());
            Assert.Contains("49 objects placed by GenEodon", after);
            Assert.Contains("present (GenEodon's)", after);
        }
        finally
        {
            ClearEodon();
        }
    }

    // cc-P458 (P407 #1): ServUO's XmlSpawner draws among the entries not at their limit with weight MaxCount
    // (XmlSpawner2.cs:8377-8408); pinned draws with weight SpawnedProbability (BaseSpawner.cs:867-893). So each entry's
    // probability is its MaxCount.
    [Fact]
    public void EachGeneratedEntryIsWeightedByItsMaxCount()
    {
        ClearEodon();

        try
        {
            EodonGeneration.Run(dryRun: false);

            var spawners = World.Items.Values.OfType<Spawner>().Where(s => !s.Deleted && EodonSpawns.IsEodonSpawner(s)).ToList();
            Assert.Equal(46, spawners.Count);

            foreach (var d in EodonSpawns.Definitions)
            {
                var s = Assert.Single(spawners, x => x.Name == d.Name && x.X == d.CentreX && x.Y == d.CentreY);
                Assert.Equal(d.Entries.Length, s.Entries.Count);

                for (var i = 0; i < d.Entries.Length; i++)
                {
                    Assert.Equal(d.Entries[i].MaxCount, s.Entries[i].SpawnedMaxCount);
                    Assert.Equal(d.Entries[i].MaxCount, s.Entries[i].SpawnedProbability);
                }
            }

            // P407's example: a white tiger is 1 of Wildlife #4's 25, not 1 of 8 entries.
            var w4 = Assert.Single(spawners, s => s.Name == "Eodon_Wildlife #4");
            Assert.Equal(25, w4.Entries.Sum(e => e.SpawnedProbability));
            Assert.Equal(1, Assert.Single(w4.Entries, e => e.SpawnedName == "WildWhiteTiger").SpawnedProbability);
        }
        finally
        {
            ClearEodon();
        }
    }

    // cc-P458 (P407 #4): a re-run takes the first run's creatures with its spawners, and the new spawners fill again.
    [Fact]
    public void ARerunDeletesTheFirstRunsCreatures()
    {
        ClearEodon();

        try
        {
            EodonGeneration.Run(dryRun: false);

            var firstSpawners = World.Items.Values.OfType<Spawner>().Where(s => !s.Deleted && EodonSpawns.IsEodonSpawner(s)).ToList();
            var firstCreatures = firstSpawners.SelectMany(s => s.Spawned.Keys).OfType<BaseCreature>().ToList();
            _out.WriteLine($"first run: {firstCreatures.Count} creatures on {firstSpawners.Count} spawners");
            Assert.NotEmpty(firstCreatures);
            Assert.All(firstSpawners, s => Assert.True(s.Spawned.Count <= s.Count, $"{s.Name} over its cap"));

            EodonGeneration.Run(dryRun: false);

            Assert.All(firstSpawners, s => Assert.True(s.Deleted, $"{s.Name} survived the re-run"));
            Assert.All(firstCreatures, c => Assert.True(c.Deleted, $"{c.GetType().Name} from the first run survived"));

            var second = World.Items.Values.OfType<Spawner>().Where(s => !s.Deleted && EodonSpawns.IsEodonSpawner(s)).ToList();
            Assert.Equal(46, second.Count);
            Assert.NotEmpty(second.SelectMany(s => s.Spawned.Keys));

            // [DeleteEodon takes the second run's creatures the same way.
            var secondCreatures = second.SelectMany(s => s.Spawned.Keys).OfType<BaseCreature>().ToList();
            EodonGeneration.Delete();
            Assert.All(secondCreatures, c => Assert.True(c.Deleted, $"{c.GetType().Name} survived [DeleteEodon"));
        }
        finally
        {
            ClearEodon();
        }
    }

    [Fact]
    public void GenEodonLeavesAnotherGateAlone()
    {
        ClearEodon();
        var handPlaced = new PublicMoongate();
        handPlaced.MoveToWorld(new Point3D(720, 1863, 40), Map.TerMur);

        try
        {
            var dry = EodonGeneration.Run(dryRun: true);
            Assert.Contains(dry, l => l.Contains("already on Ter Mur at (720, 1863, 40)"));
            Assert.Contains(dry, l => l.Contains("no Eodon moongate"));

            var lines = EodonGeneration.Run(dryRun: false);
            Assert.Contains(lines, l => l.Contains("Eodon moongate skipped"));

            var gates = World.Items.Values.OfType<PublicMoongate>().Where(g => g.Map == Map.TerMur && !g.Deleted).ToList();
            Assert.Same(handPlaced, Assert.Single(gates));

            EodonGeneration.Delete();
            Assert.False(handPlaced.Deleted, "[DeleteEodon took a gate it did not place");
        }
        finally
        {
            handPlaced.Delete();
            ClearEodon();
        }
    }

    [Fact]
    public void TheSpawnTableIsEodonXmlWithoutMyrmidexOrTheQuestLine()
    {
        var defs = EodonSpawns.Definitions;
        Assert.Equal(46, defs.Length);
        Assert.Equal(46, defs.Select(d => (d.Name, d.CentreX, d.CentreY)).Distinct().Count());

        var types = defs.SelectMany(d => d.Entries).Select(e => e.Type).Distinct().ToList();
        Assert.DoesNotContain(types, t => t.StartsWith("Myrmidex", StringComparison.OrdinalIgnoreCase));

        foreach (var quest in new[] { "TRex", "GreatApe", "VolcanoElemental", "CubEnclosure", "NestWithEgg", "SirGeoffery", "Hawkwind", "XmlQuestNpc" })
        {
            Assert.DoesNotContain(types, t => t.Equals(quest, StringComparison.OrdinalIgnoreCase));
        }

        Assert.DoesNotContain(types, t => t.Contains("HighChief", StringComparison.OrdinalIgnoreCase));
        Assert.All(defs.SelectMany(d => d.Entries), e => Assert.True(e.MaxCount > 0, $"{e.Type} MX=0"));

        // ServUO's own numbers, spot-checked off Eodon.xml.
        var lava = Assert.Single(defs, d => d.Name == "Eodon_LavaSpawn #2");
        Assert.Equal((856, 2132, 40, 831, 2107, 50, 50, 12, 13, 60, 120), (lava.CentreX, lava.CentreY, lava.CentreZ, lava.X, lava.Y, lava.Width, lava.Height, lava.Range, lava.Count, lava.MinDelaySeconds, lava.MaxDelaySeconds));

        var jukari = Assert.Single(defs, d => d.Name == "Eodon_JukariTribe");
        Assert.Equal(16, jukari.Count);
        Assert.Equal(new[] { ("TribeWarrior", 8, "Jukari"), ("TribeShaman", 5, "Jukari"), ("TribeChieftan", 3, "Jukari") },
            jukari.Entries.Select(e => (e.Type, e.MaxCount, e.Parameter)).ToArray());

        // The desert rows keep their spiders and scorpions; the Myrmidex share of MaxCount 8 goes.
        var desert = Assert.Single(defs, d => d.Name == "Eodon_Desert #1");
        Assert.Equal(3, desert.Count);

        // The Barako village is on the clifftop: z 100.
        var barako = defs.Where(d => d.Name.StartsWith("Eodon_BarakoTribe", StringComparison.Ordinal)).ToList();
        Assert.Equal(5, barako.Count);
        Assert.All(barako, d => Assert.Equal(100, d.FixedZ));
        Assert.All(defs.Where(d => !d.Name.StartsWith("Eodon_BarakoTribe", StringComparison.Ordinal)), d => Assert.Null(d.FixedZ));

        Assert.Equal(3, defs.Count(d => d.Name.StartsWith("Vof1_Silverback", StringComparison.Ordinal)));
        Assert.All(defs, d => Assert.Equal(d.Count, d.Capacity));
    }

    [Fact]
    public void EveryEntryInTheTableSpawns()
    {
        var pairs = EodonSpawns.Definitions.SelectMany(d => d.Entries).Select(e => (e.Type, e.Parameter)).Distinct().ToList();
        _out.WriteLine($"{pairs.Count} distinct entries");

        foreach (var (typeName, parameter) in pairs)
        {
            var spawner = new Spawner();
            spawner.MoveToWorld(Origin, Map.TerMur);
            spawner.SpawnBounds = default;
            var entry = spawner.AddEntry(typeName, 100, 1, false, null, parameter);

            try
            {
                Assert.True(spawner.Spawn(entry, out var flags), $"{typeName} {parameter}: {flags}");
                Assert.Equal(EntryFlags.None, flags);
            }
            finally
            {
                spawner.Delete();
            }
        }
    }
}
