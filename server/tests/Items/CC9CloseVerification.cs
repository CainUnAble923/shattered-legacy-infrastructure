// CC9 close (2026-09-29): the batch that was sized as eleven files and ~25 types and shrank, under the reachability
// rule, to what a player can actually be handed. Every fact here is about one of three routes, and each route is the
// named, existing path by which the type reaches a player (shard-migration/notes/cc9-close.md section 1):
//
//   Part A. Niporailem, the gate for BOTH Epiphany suites. Pinned's own TerMur.json places him ("Spawner (602)" at
//           [143, 158, -20]); his unique list is the only thing in ServUO that names either suite. Before this batch
//           the twelve Villainous pieces (ours since CC9 batch 5) were in no player's reach.
//   Part B. The four King's Collection instruments, reached by carpentry (KingsCollectionCarpentryRecipes).
//   Part C. The orc mask, reached by tailoring (OrcMaskTailoringRecipe), and not the orcish kin mask.
//   Part D. The candidates this batch did NOT port stay unported, so a later port is a decision, not an accident.
//
// server/tests/<path> is mirrored into Projects/UOContent.Tests/Tests/<path> by docker/uo/apply-patches.sh and run
// as a gate by docker/uo/build.sh (see batch 1's file for the host facts). No fact here asserts what is worn, a layer
// read from tile data, or a tile weight, so none needs a TestTileRows scope (notes/test-host-truths.md).

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Engines.Craft;
using Server.Engines.Spawners;
using Server.Items;
using Server.Mobiles;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class CC9CloseVerification
{
    private readonly ITestOutputHelper _out;

    public CC9CloseVerification(ITestOutputHelper output) => _out = output;

    private static readonly Point3D Here = new(143, 158, -20);

    private static PlayerMobile NewPlayer(Point3D loc, Map map = null)
    {
        // Mobile.Player is a flag CharacterCreation sets, not something the PlayerMobile constructor does (batch 3).
        var pm = new PlayerMobile { Player = true };
        pm.AddItem(new Backpack());
        pm.MoveToWorld(loc, map ?? Map.TerMur);
        pm.Hits = pm.HitsMax;
        return pm;
    }

    // Batch 8's shape: through the generated Serialize and the generated (Serial) constructor.
    private static T RoundTrip<T>(T original) where T : ISerializable
    {
        var buffer = new byte[65536];
        var writer = new BufferWriter(buffer, true, new ConcurrentQueue<Type>());
        original.Serialize(writer);
        writer.Flush();

        var copy = (T)Activator.CreateInstance(typeof(T), original.Serial)!;
        copy.Deserialize(new BufferReader(buffer));
        return copy;
    }

    // ---- Part A: the Epiphany suites and their gate ----------------------------------------------------------------

    // ServUO VirtuousEpiphany.cs: class, stock base, label, whether the constructor sets MageArmor.
    private static readonly (Type Type, Type Base, int Label, bool MageArmor)[] Virtuous =
    {
        (typeof(HelmOfVirtuousEpiphany), typeof(PlateHelm), 1150233, true),
        (typeof(GorgetOfVirtuousEpiphany), typeof(PlateGorget), 1150234, true),
        (typeof(BreastplateOfVirtuousEpiphany), typeof(PlateChest), 1150235, true),
        (typeof(ArmsOfVirtuousEpiphany), typeof(PlateArms), 1150236, true),
        (typeof(GauntletsOfVirtuousEpiphany), typeof(PlateGloves), 1150237, true),
        (typeof(LegsOfVirtuousEpiphany), typeof(PlateLegs), 1150238, true),
        (typeof(KiltOfVirtuousEpiphany), typeof(GargishPlateKilt), 1150262, true),
        (typeof(EarringsOfVirtuousEpiphany), typeof(GargishEarrings), 1150262, true), // ServUO's number: the kilt's
        (typeof(GargishBreastplateOfVirtuousEpiphany), typeof(GargishPlateChest), 1150235, false),
        (typeof(GargishArmsOfVirtuousEpiphany), typeof(GargishPlateArms), 1150236, false),
        (typeof(NecklaceOfVirtuousEpiphany), typeof(GargishNecklace), 1150261, false),
        (typeof(GargishLegsOfVirtuousEpiphany), typeof(GargishPlateLegs), 1150238, false)
    };

    private static readonly Type[] Villainous =
    {
        typeof(HelmOfVillainousEpiphany), typeof(GorgetOfVillainousEpiphany), typeof(BreastplateOfVillainousEpiphany),
        typeof(ArmsOfVillainousEpiphany), typeof(GauntletsOfVillainousEpiphany), typeof(LegsOfVillainousEpiphany),
        typeof(KiltOfVillainousEpiphany), typeof(EarringsOfVillainousEpiphany), typeof(GargishBreastplateOfVillainousEpiphany),
        typeof(GargishArmsOfVillainousEpiphany), typeof(NecklaceOfVillainousEpiphany), typeof(GargishLegsOfVillainousEpiphany)
    };

    [Fact]
    public void TheTwelveVirtuousPiecesCarryServUOsValuesOnTheirStockBases()
    {
        foreach (var (type, baseType, label, mageArmor) in Virtuous)
        {
            var item = (BaseArmor)Activator.CreateInstance(type)!;
            var epiphany = Assert.IsAssignableFrom<IEpiphanyArmor>(item);

            _out.WriteLine(
                $"{type.Name}: base={type.BaseType!.Name} hue={item.Hue} label={item.LabelNumber} " +
                $"alignment={epiphany.Alignment} surge={epiphany.Type} mageArmor={item.ArmorAttributes.MageArmor}"
            );

            Assert.Same(baseType, type.BaseType);
            Assert.Equal(2076, item.Hue);
            Assert.Equal(label, item.LabelNumber);
            Assert.Equal(Alignment.Good, epiphany.Alignment);
            Assert.Equal(SurgeType.Mana, epiphany.Type);
            Assert.Equal(mageArmor ? 1 : 0, item.ArmorAttributes.MageArmor);

            item.Delete();
        }
    }

    [Fact]
    public void NiporailemConstructsWithServUOValuesAndNamesBothSuitesAsHisUniqueList()
    {
        var boss = new Niporailem();

        _out.WriteLine(
            $"Niporailem: name='{boss.Name}' title='{boss.Title}' body={(int)boss.Body} hits={boss.HitsMax} " +
            $"str={boss.RawStr} dex={boss.RawDex} int={boss.RawInt} fame={boss.Fame} karma={boss.Karma} ai={boss.AI} " +
            $"corpse='{boss.CorpseName}' unique={boss.UniqueSAList.Length} shared={boss.SharedSAList.Length}"
        );

        Assert.IsAssignableFrom<BaseSABoss>(boss);
        Assert.Equal("Niporailem", boss.Name);
        Assert.Equal("the Thief", boss.Title);
        Assert.Equal(722, (int)boss.Body);
        Assert.InRange(boss.HitsMax, 10000, 10500);
        Assert.Equal(1000, boss.RawStr);
        Assert.Equal(1200, boss.RawDex);
        Assert.Equal(1200, boss.RawInt);
        Assert.Equal(15000, boss.Fame);
        Assert.Equal(-15000, boss.Karma);
        Assert.Equal(AIType.AI_Mage, boss.AI);
        Assert.Equal(120.0, boss.Skills.Necromancy.Base);
        Assert.Equal(100, boss.PoisonResistSeed);
        Assert.True(boss.AlwaysMurderer);
        Assert.Equal("the corpse of niporailem", boss.CorpseName);

        // The unique list is exactly the 24 Epiphany pieces, both suites, and nothing else.
        var unique = boss.UniqueSAList;
        Assert.Equal(24, unique.Length);
        Assert.Equal(24, unique.Distinct().Count());
        Assert.Equal(
            Virtuous.Select(v => v.Type).Concat(Villainous).OrderBy(t => t.Name),
            unique.OrderBy(t => t.Name)
        );

        Assert.Equal(
            new[] { typeof(BladeOfBattle), typeof(DemonBridleRing), typeof(GiantSteps), typeof(SwordOfShatteredHopes) },
            boss.SharedSAList
        );

        boss.Delete();
    }

    [Fact]
    public void EveryEpiphanyPieceComesOutOfNiporailemsUniqueRoll()
    {
        // BaseSABoss.CreateArtifact picks uniformly from the list; 400 draws from 24 miss one with probability < 1e-6.
        var boss = new Niporailem();
        var seen = new HashSet<Type>();

        for (var i = 0; i < 400; i++)
        {
            var item = boss.CreateArtifact(boss.UniqueSAList);
            Assert.NotNull(item);
            Assert.IsAssignableFrom<IEpiphanyArmor>(item);
            seen.Add(item.GetType());
            item.Delete();
        }

        _out.WriteLine($"400 unique rolls gave {seen.Count} distinct types");

        Assert.Equal(24, seen.Count);
        Assert.All(Villainous, t => Assert.Contains(t, seen));
        Assert.All(Virtuous, v => Assert.Contains(v.Type, seen));

        boss.Delete();
    }

    [Fact]
    public void ASpawnerWhereTheDataPlacesNiporailemFillsWithHim()
    {
        // post-uoml/termur/TerMur.json, "Spawner (602)" at [143, 158, -20], entry name "Niporailem", maxCount 1.
        Assert.Same(typeof(Niporailem), AssemblyHandler.FindTypeByName("Niporailem"));
        Assert.Same(typeof(Niporailem), AssemblyHandler.FindTypeByName("niporailem"));

        var spawner = new Spawner();
        spawner.MoveToWorld(Here, Map.TerMur);
        spawner.SpawnBounds = default;
        var entry = spawner.AddEntry("Niporailem", 100, 1, dotimer: false);

        try
        {
            Assert.True(spawner.Spawn(entry, out var flags), $"Spawn returned false with flags {flags}");
            Assert.Equal(EntryFlags.None, flags);
            var spawned = Assert.Single(spawner.Spawned.Keys);
            var boss = Assert.IsType<Niporailem>(spawned);
            Assert.Same(Map.TerMur, boss.Map);
            _out.WriteLine($"spawner at {spawner.Location} filled with {boss.GetType().Name} '{boss.Name}' {boss.Title}");
        }
        finally
        {
            spawner.Delete();
        }
    }

    [Fact]
    public void NiporailemThrowsHisTreasureIntoTheCombatantsPackAndItTurnsToSandOutsideIt()
    {
        ShardTestClock.Arm(); // ThrowTreasure puts the gold in the pack from a 1 s DelayCall

        var pm = NewPlayer(new Point3D(Here.X + 1, Here.Y, Here.Z));
        var boss = new Niporailem();
        boss.MoveToWorld(Here, Map.TerMur);
        boss.Combatant = pm;

        boss.OnActionCombat();
        Assert.Null(pm.Backpack.FindItemByType<NiporailemsTreasure>()); // not yet: it arrives after a second

        ShardTestClock.Advance(TimeSpan.FromSeconds(1.1));

        var treasure = pm.Backpack.FindItemByType<NiporailemsTreasure>();
        Assert.NotNull(treasure);
        _out.WriteLine(
            $"thrown: item=0x{treasure.ItemID:X} weight={treasure.Weight} label={treasure.LabelNumber} link={treasure.Link?.Name} " +
            $"decays={treasure.Decays} decayTime={treasure.DecayTime}"
        );
        Assert.Equal(0xEEF, treasure.ItemID);
        Assert.Equal(100.0, treasure.Weight);
        Assert.Equal(1112113, treasure.LabelNumber);
        Assert.Same(boss, treasure.Link);
        Assert.Equal(TimeSpan.FromMinutes(15), treasure.DecayTime);

        // Moved within the pack itself it stays gold.
        Assert.True(treasure.DropToItem(pm, pm.Backpack, new Point3D(10, 10, 0)));
        Assert.Equal(0xEEF, treasure.ItemID);

        // Anywhere else it is sand.
        var bag = new Bag();
        pm.Backpack.DropItem(bag);
        Assert.True(treasure.DropToItem(pm, bag, new Point3D(10, 10, 0)));
        _out.WriteLine($"in a bag: item=0x{treasure.ItemID:X} weight={treasure.Weight} label={treasure.LabelNumber}");
        Assert.Same(bag, treasure.Parent);
        Assert.Equal(0x11EA, treasure.ItemID);
        Assert.Equal(25.0, treasure.Weight);
        Assert.Equal(1112115, treasure.LabelNumber);

        // The link survives a save.
        var copy = RoundTrip(treasure);
        Assert.Same(boss, copy.Link);

        boss.Delete();
        Assert.True(treasure.Decays); // its boss is gone
        pm.Delete();
    }

    [Fact]
    public void NiporailemsSpectralArmourIsHisSummonAndSurvivesASaveAndGoesWithHim()
    {
        var pm = NewPlayer(new Point3D(Here.X + 1, Here.Y, Here.Z));
        var boss = new Niporailem();
        boss.MoveToWorld(Here, Map.TerMur);

        boss.SpawnSpectralArmour(pm);

        var helper = Assert.IsType<SpectralArmour>(Assert.Single(boss.Helpers));
        Assert.Same(boss, helper.SummonMaster);
        Assert.Same(pm, helper.Combatant);
        Assert.Same(Map.TerMur, helper.Map);
        Assert.Equal(helper.ControlSlots, boss.Followers);

        var copy = RoundTrip(boss);
        Assert.Same(helper, Assert.Single(copy.Helpers));

        boss.Delete();
        Assert.True(helper.Deleted);
        Assert.Empty(boss.Helpers);
        pm.Delete();
    }

    // ---- Part B: the King's Collection instruments -----------------------------------------------------------------

    private static CraftSystem Carpentry()
    {
        if (DefCarpentry.CraftSystem == null)
        {
            DefCarpentry.Initialize();
        }

        KingsCollectionCarpentryRecipes.Register();
        KingsCollectionCarpentryRecipes.Register(); // idempotent
        return DefCarpentry.CraftSystem;
    }

    [Fact]
    public void TheFourInstrumentsAreCraftableUnderCarpentryInstrumentsWithServUOsCosts()
    {
        // ServUO DefCarpentry.cs:613-636: type, name, min, max, boards, second resource and amount.
        var expected = new (Type Type, int Name, double Min, double Max, int Boards, Type Res, int ResName, int ResAmount)[]
        {
            (typeof(CelloDeed), 1098390, 75.0, 105.0, 15, typeof(Cloth), 1044286, 5),
            (typeof(WallMountedBellSouthDeed), 1154162, 75.0, 105.0, 50, typeof(IronIngot), 1044036, 50),
            (typeof(WallMountedBellEastDeed), 1154163, 75.0, 105.0, 50, typeof(IronIngot), 1044036, 50),
            (typeof(TrumpetDeed), 1098388, 85.0, 105.0, 10, typeof(IronIngot), 1044036, 15),
            (typeof(CowBellDeed), 1098418, 85.0, 105.0, 10, typeof(IronIngot), 1044036, 15)
        };

        var carpentry = Carpentry();
        Assert.NotNull(carpentry);

        var instruments = carpentry.CraftItems.Where(c => (int)c.GroupNameNumber == 1044293).ToList();
        _out.WriteLine("Instruments group: " + string.Join(", ", instruments.Select(c => c.ItemType.Name)));

        // The five land at the end of the group, in ServUO's order.
        Assert.Equal(expected.Select(e => e.Type), instruments.TakeLast(5).Select(c => c.ItemType));

        foreach (var e in expected)
        {
            var entry = Assert.Single(carpentry.CraftItems, c => c.ItemType == e.Type);

            Assert.Equal(e.Name, (int)entry.NameNumber);
            Assert.Equal(Expansion.None, entry.RequiredExpansion);

            Assert.Equal(2, entry.Skills.Count);
            var carp = Assert.Single(entry.Skills, s => s.SkillToMake == SkillName.Carpentry);
            Assert.Equal(e.Min, carp.MinSkill);
            Assert.Equal(e.Max, carp.MaxSkill);
            var music = Assert.Single(entry.Skills, s => s.SkillToMake == SkillName.Musicianship);
            Assert.Equal(45.0, music.MinSkill);
            Assert.Equal(50.0, music.MaxSkill);

            Assert.Equal(2, entry.Resources.Count);
            Assert.Equal(typeof(Log), entry.Resources[0].ItemType);
            Assert.Equal(1044041, (int)entry.Resources[0].Name); // Boards or Logs
            Assert.Equal(e.Boards, entry.Resources[0].Amount);
            Assert.Equal(e.Res, entry.Resources[1].ItemType);
            Assert.Equal(e.ResName, (int)entry.Resources[1].Name);
            Assert.Equal(e.ResAmount, entry.Resources[1].Amount);
        }
    }

    [Fact]
    public void EachInstrumentDeedBuildsItsAddonWhoseComponentPlaysItsSound()
    {
        // deed, label, component item id, sound, z (ServUO's AddComponent offsets)
        var expected = new (BaseAddonDeed Deed, int Label, int ItemID, int Sound, int Z)[]
        {
            (new CelloDeed(), 1098390, 0x4C3E, 0x66D, 0),
            (new CowBellDeed(), 1098418, 0x4C5A, 0x66E, 0),
            (new TrumpetDeed(), 1098388, 0x4C3C, 0x66F, 0),
            (new WallMountedBellSouthDeed(), 1154162, 0x4C5C, 0x66C, 10),
            (new WallMountedBellEastDeed(), 1154163, 0x4C5D, 0x66C, 10)
        };

        foreach (var (deed, label, itemID, sound, z) in expected)
        {
            var addon = deed.Addon;
            var component = Assert.IsAssignableFrom<InstrumentedAddonComponent>(Assert.Single(addon.Components));

            _out.WriteLine(
                $"{deed.GetType().Name}: label={deed.LabelNumber} addon={addon.GetType().Name} retainHue={addon.RetainDeedHue} " +
                $"component={component.GetType().Name} 0x{component.ItemID:X} sound=0x{component.SuccessSound:X} offset={component.Offset}"
            );

            Assert.Equal(label, deed.LabelNumber);
            Assert.True(addon.RetainDeedHue);
            Assert.Same(deed.GetType(), addon.Deed.GetType());
            Assert.Equal(itemID, component.ItemID);
            Assert.Equal(sound, component.SuccessSound);
            Assert.Equal(new Point3D(0, 0, z), component.Offset);

            addon.Delete();
            deed.Delete();
        }
    }

    [Fact]
    public void AnInstrumentPlaysOnceASecondAndOnlyWithinTwoTiles()
    {
        ShardTestClock.Arm(); // the one-second lock is released by a DelayCall

        var addon = new CelloAddon();
        addon.MoveToWorld(Here, Map.TerMur);
        var component = (InstrumentedAddonComponent)addon.Components[0];

        var near = NewPlayer(new Point3D(Here.X + 2, Here.Y, Here.Z));
        var far = NewPlayer(new Point3D(Here.X + 3, Here.Y, Here.Z));

        component.OnDoubleClick(far);
        Assert.True(far.CanBeginAction<InstrumentedAddonComponent>()); // out of reach: no lock taken

        component.OnDoubleClick(near);
        Assert.False(near.CanBeginAction<InstrumentedAddonComponent>()); // played; locked for a second

        ShardTestClock.Advance(TimeSpan.FromSeconds(1.1));
        Assert.True(near.CanBeginAction<InstrumentedAddonComponent>());

        var copy = RoundTrip(component);
        Assert.Equal(0x66D, copy.SuccessSound);

        addon.Delete();
        near.Delete();
        far.Delete();
    }

    // ---- Part C: the orc mask --------------------------------------------------------------------------------------

    [Fact]
    public void TheOrcMaskIsTailoredUnderHatsAndIsNotTheOrcishKinMask()
    {
        if (DefTailoring.CraftSystem == null)
        {
            DefTailoring.Initialize();
        }

        OrcMaskTailoringRecipe.Register();
        OrcMaskTailoringRecipe.Register(); // idempotent

        var tailoring = DefTailoring.CraftSystem;
        var hats = tailoring.CraftItems.Where(c => (int)c.GroupNameNumber == 1011375).ToList();
        _out.WriteLine("Hats group: " + string.Join(", ", hats.Select(c => c.ItemType.Name)));

        var entry = Assert.Single(tailoring.CraftItems, c => c.ItemType == typeof(OrcMask));
        Assert.Same(entry, hats[^1]); // the end of the group, after the SE block, as ServUO has it
        Assert.Equal(1025147, (int)entry.NameNumber);
        Assert.Equal(Expansion.None, entry.RequiredExpansion);
        var skill = Assert.Single(entry.Skills);
        Assert.Equal(SkillName.Tailoring, skill.SkillToMake);
        Assert.Equal(75.0, skill.MinSkill);
        Assert.Equal(100.0, skill.MaxSkill);
        var res = Assert.Single(entry.Resources);
        Assert.Equal(typeof(Cloth), res.ItemType);
        Assert.Equal(12, res.Amount);

        // Pinned repairs clothing its craft system lists (Repair.cs:407); this is the lookup it makes.
        Assert.NotNull(tailoring.CraftItems.SearchForSubclass(typeof(OrcMask)));

        var mask = new OrcMask();
        var kin = new OrcishKinMask();
        _out.WriteLine(
            $"OrcMask 0x{mask.ItemID:X} hue={mask.Hue} label={mask.LabelNumber} layer={mask.Layer} " +
            $"resists={mask.PhysicalResistance}/{mask.FireResistance}/{mask.ColdResistance}/{mask.PoisonResistance}/{mask.EnergyResistance} " +
            $"hits={mask.MaxHitPoints}; OrcishKinMask 0x{kin.ItemID:X} hue=0x{kin.Hue:X} label={kin.LabelNumber} name='{kin.Name}'"
        );

        Assert.Equal(0x141B, mask.ItemID);
        Assert.Equal(kin.ItemID, mask.ItemID); // the shared graphic
        Assert.Equal(0, mask.Hue);
        Assert.Equal(0x8A4, kin.Hue);
        Assert.Equal(1025147, mask.LabelNumber);
        Assert.False(mask is OrcishKinMask);
        Assert.Equal(Layer.Helm, mask.Layer);
        Assert.Equal(1, mask.BasePhysicalResistance);
        Assert.Equal(1, mask.BaseFireResistance);
        Assert.Equal(7, mask.BaseColdResistance);
        Assert.Equal(7, mask.BasePoisonResistance);
        Assert.Equal(8, mask.BaseEnergyResistance);
        Assert.InRange(mask.MaxHitPoints, 20, 30);
        var dyer = NewPlayer(Here);
        var tub = new DyeTub();
        Assert.False(mask.Dye(dyer, tub));

        tub.Delete();
        dyer.Delete();
        mask.Delete();
        kin.Delete();
    }

    // ---- Part D: what this batch declined stays declined -----------------------------------------------------------

    [Fact]
    public void TheCandidatesThisBatchDeclinedResolveToNothing()
    {
        // notes/cc9-close.md section 1: a twin of a pinned type, collection rewards, no source in any tree, and a
        // source that is a subsystem. If one of these starts resolving, someone ported it: check it has a path.
        var declined = new[]
        {
            "MysticBook",                  // pinned's MysticSpellbook, value for value
            "NystulsWizardsHat", "GypsyHeaddress", "MalabellesDress", // Vesper museum
            "ShadowCloakOfRejuvenation", "GargishShadowCloakOfRejuvenation", // named by nothing in ServUO
            "GargishClothWingArmor",       // extracted only for the cloak
            "CorgulsHandbookOnMysticism",  // Corgul (High Seas)
            "ImprisonedDog", "AnkhPendant", // Travesty / Clean Up Britannia / virtue artifacts
            "MaleGargishClothLegs", "AncientShipModelOfTheHMSCape", "ChefsToque",
            "ZooMemberBonnet", "ZooMemberFloppyHat", "LibraryFriendFeatheredHat", "LibraryFriendSkirt", "LibraryFriendPants"
        };

        foreach (var name in declined)
        {
            Assert.True(AssemblyHandler.FindTypeByName(name) == null, $"{name} resolves to a type");
        }

        // MysticBook's twin is stock and craftable, which is why the batch did not need it.
        Assert.NotNull(AssemblyHandler.FindTypeByName("MysticSpellbook"));

        // The fourth Vesper museum hat is already stock under one lower-case letter: pinned's JesterHatofChuckles
        // (Items/Special/Community Collection/), same label, luck and resists. FindTypeByName ignores case, so
        // ServUO's spelling is already a live name here, and porting it would add a second type under it.
        Assert.Same(typeof(JesterHatofChuckles), AssemblyHandler.FindTypeByName("JesterHatOfChuckles"));
    }
}
