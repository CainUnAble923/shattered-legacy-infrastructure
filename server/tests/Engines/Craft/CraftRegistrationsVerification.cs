// The four craft registrations (2026-09-29; shard-migration/notes/craft-registrations.md): ServUO's blacksmithy,
// tailoring, tinkering and carpentry entries for types that are ours and not pinned's, appended to pinned's tables at
// ServerStarted by server/customizations/Engines/Craft/{Blacksmithy,Tailoring,Tinkering,Carpentry}CraftRegistrations.cs.
//
// One fact per system asserts every entry the hook adds, with ServUO's values, and that the entries it deliberately
// leaves out stay out (recipe-gated, or a resource no player can get). One fact builds a fresh stock table beside the
// live one to show pinned's own tables have none of them. One drives a real craft, CraftItem.CompleteCraft, end to end:
// a tinker with three ingots and a tool makes a gargish ring.
//
// server/tests/<path> is mirrored into Projects/UOContent.Tests/Tests/<path> by docker/uo/apply-patches.sh and run
// as a gate by docker/uo/build.sh. No fact here reads a layer or a tile weight, so none needs a TestTileRows scope
// (notes/test-host-truths.md). Def*.Initialize() and ServerStarted do not run in the host, so each fact calls them in
// the order the server runs them, as CC9CloseVerification does.

using System;
using System.Linq;
using Server;
using Server.Engines.Craft;
using Server.Items;
using Server.Mobiles;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class CraftRegistrationsVerification
{
    private readonly ITestOutputHelper _out;

    public CraftRegistrationsVerification(ITestOutputHelper output) => _out = output;

    private static CraftSystem Blacksmithy()
    {
        if (DefBlacksmithy.CraftSystem == null)
        {
            DefBlacksmithy.Initialize();
        }

        BlacksmithyCraftRegistrations.Register();
        BlacksmithyCraftRegistrations.Register(); // idempotent
        return DefBlacksmithy.CraftSystem;
    }

    private static CraftSystem Tailoring()
    {
        if (DefTailoring.CraftSystem == null)
        {
            DefTailoring.Initialize();
        }

        TailoringCraftRegistrations.Register();
        TailoringCraftRegistrations.Register();
        return DefTailoring.CraftSystem;
    }

    private static CraftSystem Tinkering()
    {
        if (DefTinkering.CraftSystem == null)
        {
            DefTinkering.Initialize();
        }

        TinkeringCraftRegistrations.Register();
        TinkeringCraftRegistrations.Register();
        return DefTinkering.CraftSystem;
    }

    private static CraftSystem Carpentry()
    {
        if (DefCarpentry.CraftSystem == null)
        {
            DefCarpentry.Initialize();
        }

        CarpentryCraftRegistrations.Register();
        CarpentryCraftRegistrations.Register();
        return DefCarpentry.CraftSystem;
    }

    // type, group, name, min, max, first resource, its amount
    private record Row(Type Type, int Group, int Name, double Min, double Max, Type Res, int Amount);

    // Every entry appears exactly once, with ServUO's values, and the main skill is the system's own.
    private void AssertRows(CraftSystem system, Row[] rows)
    {
        Assert.NotNull(system);

        foreach (var r in rows)
        {
            var entry = Assert.Single(system.CraftItems, c => c.ItemType == r.Type);

            Assert.Equal(r.Group, (int)entry.GroupNameNumber);
            Assert.Equal(r.Name, (int)entry.NameNumber);
            Assert.Equal(Expansion.None, entry.RequiredExpansion); // ServUO sets none on any of these
            Assert.Null(entry.Recipe); // none is recipe-gated

            var main = Assert.Single(entry.Skills, s => s.SkillToMake == system.MainSkill);
            Assert.Equal(r.Min, main.MinSkill);
            Assert.Equal(r.Max, main.MaxSkill);

            Assert.Equal(r.Res, entry.Resources[0].ItemType);
            Assert.Equal(r.Amount, entry.Resources[0].Amount);

            // What CraftItem.CompleteCraft does to make one (CraftItem.cs, `ItemType.CreateInstance<Item>()`).
            var made = entry.ItemType.CreateInstance<Item>();
            Assert.IsType(r.Type, made);
            made.Delete();
        }
    }

    private static void AssertAbsent(CraftSystem system, params Type[] types)
    {
        foreach (var t in types)
        {
            Assert.Null(system.CraftItems.SearchFor(t));
        }
    }

    [Fact]
    public void BlacksmithyCraftsServUOsThirtyFourGargishAndPlateEntries()
    {
        // ServUO DefBlacksmithy.cs :340-357, :425-435, :643-663, :702-704, :752-760, :824-828, :912-915.
        // Group 1011078 for ServUO's 1111704 is the one value decision (BlacksmithyCraftRegistrations.cs header).
        var ingot = typeof(IronIngot);
        var rows = new Row[]
        {
            new(typeof(FemaleGargishPlateArms), 1011078, 1095336, 66.3, 116.3, ingot, 18),
            new(typeof(FemaleGargishPlateChest), 1011078, 1095338, 75.0, 125.0, ingot, 25),
            new(typeof(FemaleGargishPlateLegs), 1011078, 1095342, 68.8, 118.8, ingot, 20),
            new(typeof(FemaleGargishPlateKilt), 1011078, 1095340, 58.9, 108.9, ingot, 12),
            new(typeof(GargishPlateArms), 1011078, 1095336, 66.3, 116.3, ingot, 18),
            new(typeof(GargishPlateChest), 1011078, 1095338, 75.0, 125.0, ingot, 25),
            new(typeof(GargishPlateLegs), 1011078, 1095342, 68.8, 118.8, ingot, 20),
            new(typeof(GargishPlateKilt), 1011078, 1095340, 58.9, 108.9, ingot, 12),
            new(typeof(GargishAmulet), 1011078, 1098595, 60.0, 110.0, ingot, 3),
            new(typeof(SmallPlateShield), 1011080, 1095770, -25.0, 25.0, ingot, 12),
            new(typeof(GargishKiteShield), 1011080, 1095774, 4.6, 54.6, ingot, 16),
            new(typeof(LargePlateShield), 1011080, 1095772, 24.3, 74.3, ingot, 18),
            new(typeof(MediumPlateShield), 1011080, 1095771, -10.2, 39.8, ingot, 14),
            new(typeof(GargishChaosShield), 1011080, 1095808, 85.0, 135.0, ingot, 25),
            new(typeof(GargishOrderShield), 1011080, 1095810, 85.0, 135.0, ingot, 25),
            new(typeof(GargishKatana), 1011081, 1097490, 44.1, 94.1, ingot, 8),
            new(typeof(GargishKryss), 1011081, 1097492, 36.7, 86.7, ingot, 8),
            new(typeof(GargishBoneHarvester), 1011081, 1097502, 33.0, 83.0, ingot, 10),
            new(typeof(GargishTekagi), 1011081, 1097510, 55.0, 105.0, ingot, 12),
            new(typeof(GargishDaisho), 1011081, 1097512, 60.0, 110.0, ingot, 15),
            new(typeof(GargishDagger), 1011081, 1095362, 0.0, 100.0, ingot, 3),
            new(typeof(Shortblade), 1011081, 1095374, 28.0, 100.0, ingot, 12),
            new(typeof(GargishBattleAxe), 1011082, 1097480, 30.5, 80.5, ingot, 14),
            new(typeof(GargishAxe), 1011082, 1097482, 34.2, 84.2, ingot, 14),
            new(typeof(GargishBardiche), 1011083, 1097484, 31.7, 81.7, ingot, 18),
            new(typeof(GargishWarFork), 1011083, 1097494, 42.9, 92.9, ingot, 12),
            new(typeof(GargishScythe), 1011083, 1097500, 39.0, 89.0, ingot, 14),
            new(typeof(GargishPike), 1011083, 1097504, 47.0, 97.0, ingot, 12),
            new(typeof(GargishLance), 1011083, 1097506, 48.0, 98.0, ingot, 20),
            new(typeof(GargishWarHammer), 1011084, 1097496, 34.2, 84.2, ingot, 16),
            new(typeof(GargishMaul), 1011084, 1097498, 19.4, 69.4, ingot, 10),
            new(typeof(GargishTessen), 1011084, 1097508, 85.0, 135.0, ingot, 16),
            new(typeof(CrushedGlass), 1011173, 1113351, 110.0, 135.0, typeof(BlueDiamond), 1),
            new(typeof(PowderedIron), 1011173, 1113353, 110.0, 135.0, typeof(WhitePearl), 1)
        };
        Assert.Equal(34, rows.Length);

        var smith = Blacksmithy();
        AssertRows(smith, rows);

        // The three entries with more than one skill or resource.
        var tessen = smith.CraftItems.SearchFor(typeof(GargishTessen));
        var tailoringSkill = Assert.Single(tessen.Skills, s => s.SkillToMake == SkillName.Tailoring);
        Assert.Equal(50.0, tailoringSkill.MinSkill);
        Assert.Equal(55.0, tailoringSkill.MaxSkill);
        Assert.Equal(typeof(Cloth), tessen.Resources[1].ItemType);
        Assert.Equal(10, tessen.Resources[1].Amount);

        var glass = smith.CraftItems.SearchFor(typeof(CrushedGlass));
        Assert.Equal(typeof(GlassSword), glass.Resources[1].ItemType);
        Assert.Equal(5, glass.Resources[1].Amount);

        var iron = smith.CraftItems.SearchFor(typeof(PowderedIron));
        Assert.Equal(typeof(IronIngot), iron.Resources[1].ItemType);
        Assert.Equal(20, iron.Resources[1].Amount);

        // Each lands at the end of its group, in ServUO's order.
        var shields = smith.CraftItems.Where(c => (int)c.GroupNameNumber == 1011080).Select(c => c.ItemType).ToList();
        _out.WriteLine("Shields: " + string.Join(", ", shields.Select(t => t.Name)));
        Assert.Equal(rows.Where(r => r.Group == 1011080).Select(r => r.Type), shields.TakeLast(6));

        // Recipe-gated and needing BloodOfTheDarkFather, which neither tree has.
        AssertAbsent(smith, typeof(BritchesOfWarding), typeof(GlovesOfFeudalGrip));
    }

    [Fact]
    public void TailoringCraftsLeatherTalonsAndNothingItsRecipesOrResourcesWouldStrand()
    {
        var tailoring = Tailoring();

        // ServUO DefTailoring.cs:543, verbatim, including its message 1044453.
        AssertRows(tailoring, new Row[] { new(typeof(LeatherTalons), 1015288, 1095728, 40.4, 65.4, typeof(Leather), 6) });
        Assert.Equal(1044453, (int)tailoring.CraftItems.SearchFor(typeof(LeatherTalons)).Resources[0].Message);

        // Recipe-gated with no recipe source we have, some also needing Lodestone or FeyWings (TailoringCraftRegistrations.cs).
        AssertAbsent(
            tailoring,
            typeof(AssassinsCowl), typeof(MagesHood), typeof(CowlOfTheMaceAndShield), typeof(MagesHoodOfScholarlyInsight),
            typeof(MaceBelt), typeof(SwordBelt), typeof(DaggerBelt), typeof(ElegantCollar),
            typeof(CrimsonMaceBelt), typeof(CrimsonSwordBelt), typeof(CrimsonDaggerBelt), typeof(ElegantCollarOfFortune),
            typeof(CuffsOfTheArchmage)
        );

        // Recipe-gated and needing TigerPelt or DragonTurtleScute (Q-066, gate 8).
        AssertAbsent(
            tailoring,
            typeof(TigerPeltChest), typeof(TigerPeltLegs), typeof(TigerPeltShorts), typeof(TigerPeltHelm),
            typeof(TigerPeltCollar), typeof(TigerPeltBustier), typeof(TigerPeltLongSkirt), typeof(TigerPeltSkirt),
            typeof(DragonTurtleHideChest), typeof(DragonTurtleHideLegs), typeof(DragonTurtleHideHelm),
            typeof(DragonTurtleHideArms), typeof(DragonTurtleHideBustier)
        );
    }

    [Fact]
    public void TinkeringCraftsGargishJewelleryUtensilsTheVoidOrbAndTheEightGemJewels()
    {
        // ServUO DefTinkering.cs :209-215, :399-401, :619, :706-736.
        var ingot = typeof(IronIngot);
        var rows = new Row[]
        {
            new(typeof(GargishNecklace), 1044049, 1095784, 60.0, 110.0, ingot, 3),
            new(typeof(GargishBracelet), 1044049, 1095785, 55.0, 105.0, ingot, 3),
            new(typeof(GargishRing), 1044049, 1095786, 65.0, 115.0, ingot, 3),
            new(typeof(GargishEarrings), 1044049, 1095787, 55.0, 105.0, ingot, 3),
            new(typeof(GargishCleaver), 1044048, 1097478, 20.0, 70.0, ingot, 3),
            new(typeof(GargishButcherKnife), 1044048, 1097486, 25.0, 75.0, ingot, 2),
            new(typeof(VoidOrb), 1044051, 1113354, 90.0, 104.3, typeof(DarkSapphire), 1),
            new(typeof(BrilliantAmberBracelet), 1073107, 1073453, 75.0, 125.0, ingot, 5),
            new(typeof(FireRubyBracelet), 1073107, 1073454, 75.0, 125.0, ingot, 5),
            new(typeof(DarkSapphireBracelet), 1073107, 1073455, 75.0, 125.0, ingot, 5),
            new(typeof(WhitePearlBracelet), 1073107, 1073456, 75.0, 125.0, ingot, 5),
            new(typeof(EcruCitrineRing), 1073107, 1073457, 75.0, 125.0, ingot, 5),
            new(typeof(BlueDiamondRing), 1073107, 1073458, 75.0, 125.0, ingot, 5),
            new(typeof(PerfectEmeraldRing), 1073107, 1073459, 75.0, 125.0, ingot, 5),
            new(typeof(TurqouiseRing), 1073107, 1073460, 75.0, 125.0, ingot, 5)
        };
        Assert.Equal(15, rows.Length);

        var tinkering = Tinkering();
        AssertRows(tinkering, rows);

        var orb = tinkering.CraftItems.SearchFor(typeof(VoidOrb));
        Assert.True(orb.ForceNonExceptional);
        var magery = Assert.Single(orb.Skills, s => s.SkillToMake == SkillName.Magery);
        Assert.Equal(80.0, magery.MinSkill);
        Assert.Equal(100.0, magery.MaxSkill);
        Assert.Equal(typeof(BlackPearl), orb.Resources[1].ItemType);
        Assert.Equal(50, orb.Resources[1].Amount);

        // Each gem jewel takes 20 of the plain gem and 10 of the ML one, as ServUO's table has it.
        var gems = new (Type Jewel, Type Plain, Type Rare)[]
        {
            (typeof(BrilliantAmberBracelet), typeof(Amber), typeof(BrilliantAmber)),
            (typeof(FireRubyBracelet), typeof(Ruby), typeof(FireRuby)),
            (typeof(DarkSapphireBracelet), typeof(Sapphire), typeof(DarkSapphire)),
            (typeof(WhitePearlBracelet), typeof(Tourmaline), typeof(WhitePearl)),
            (typeof(EcruCitrineRing), typeof(Citrine), typeof(EcruCitrine)),
            (typeof(BlueDiamondRing), typeof(Diamond), typeof(BlueDiamond)),
            (typeof(PerfectEmeraldRing), typeof(Emerald), typeof(PerfectEmerald)),
            (typeof(TurqouiseRing), typeof(Amethyst), typeof(Turquoise))
        };

        foreach (var (jewel, plain, rare) in gems)
        {
            var entry = tinkering.CraftItems.SearchFor(jewel);
            Assert.Equal(3, entry.Resources.Count);
            Assert.Equal(plain, entry.Resources[1].ItemType);
            Assert.Equal(20, entry.Resources[1].Amount);
            Assert.Equal(rare, entry.Resources[2].ItemType);
            Assert.Equal(10, entry.Resources[2].Amount);
        }

        // CrystalShards has no source a player can reach; the bracelet is recipe-gated on BloodOfTheDarkFather.
        AssertAbsent(tinkering, typeof(ArcanicRuneStone), typeof(BraceletOfPrimalConsumption));
    }

    [Fact]
    public void CarpentryCraftsTheGargishGnarledStaffAndTheAudCharButNoDarkwood()
    {
        var carpentry = Carpentry();

        // ServUO DefCarpentry.cs:460 and :604; typeof(Log) for its typeof(Board), as the other two carpentry hooks do.
        AssertRows(
            carpentry,
            new Row[]
            {
                new(typeof(GargishGnarledStaff), 1044566, 1097488, 78.9, 128.9, typeof(Log), 16),
                new(typeof(AudChar), 1044293, 1095315, 78.9, 103.9, typeof(Log), 35)
            }
        );

        var staff = carpentry.CraftItems.SearchFor(typeof(GargishGnarledStaff));
        Assert.Equal(typeof(EcruCitrine), staff.Resources[1].ItemType);
        Assert.Equal(1, staff.Resources[1].Amount);

        var aud = carpentry.CraftItems.SearchFor(typeof(AudChar));
        var music = Assert.Single(aud.Skills, s => s.SkillToMake == SkillName.Musicianship);
        Assert.Equal(45.0, music.MinSkill);
        Assert.Equal(50.0, music.MaxSkill);
        Assert.Equal(typeof(Granite), aud.Resources[1].ItemType);
        Assert.Equal(3, aud.Resources[1].Amount);

        // Five peerless ingredients nothing drops, and Putrefaction from a method nothing calls.
        AssertAbsent(
            carpentry,
            typeof(DarkwoodCrown), typeof(DarkwoodChest), typeof(DarkwoodGorget), typeof(DarkwoodLegs),
            typeof(DarkwoodPauldrons), typeof(DarkwoodGloves)
        );
    }

    [Fact]
    public void PinnedsOwnTablesCraftNoneOfTheseTypesAndTheLiveTablesCraftAllOfThem()
    {
        // A fresh stock table, built through the private constructor the way Initialize() builds it, without replacing
        // the live one. The hooks run only on the live tables, so every type they register must be missing here.
        var pairs = new (CraftSystem Live, Type SystemType, Type[] Types)[]
        {
            (Blacksmithy(), typeof(DefBlacksmithy), new[] { typeof(GargishPlateChest), typeof(GargishKatana), typeof(SmallPlateShield), typeof(PowderedIron) }),
            (Tailoring(), typeof(DefTailoring), new[] { typeof(LeatherTalons) }),
            (Tinkering(), typeof(DefTinkering), new[] { typeof(GargishRing), typeof(GargishEarrings), typeof(BlueDiamondRing), typeof(VoidOrb) }),
            (Carpentry(), typeof(DefCarpentry), new[] { typeof(GargishGnarledStaff), typeof(AudChar) })
        };

        // A stock table registers its recipes again, and Recipe's constructor throws on a duplicate ID into the one
        // process-wide Recipe.Recipes (Core/Recipes.cs:18-20). So the registry is emptied while the stock tables are
        // built and put back exactly as it was afterwards.
        var saved = Recipe.Recipes.ToList();

        try
        {
            foreach (var (live, systemType, types) in pairs)
            {
                Recipe.Recipes.Clear();
                var stock = (CraftSystem)Activator.CreateInstance(systemType, true)!;
                _out.WriteLine($"{systemType.Name}: stock {stock.CraftItems.Count} entries, live {live.CraftItems.Count}");

                foreach (var t in types)
                {
                    Assert.Null(stock.CraftItems.SearchFor(t));
                    Assert.NotNull(live.CraftItems.SearchFor(t));
                }
            }
        }
        finally
        {
            Recipe.Recipes.Clear();
            foreach (var (id, recipe) in saved)
            {
                Recipe.Recipes[id] = recipe;
            }
        }
    }

    [Fact]
    public void ATinkerWithThreeIngotsAndToolsMakesAGargishRing()
    {
        var tinkering = Tinkering();
        var entry = tinkering.CraftItems.SearchFor(typeof(GargishRing));
        Assert.NotNull(entry);

        var tinker = new PlayerMobile { Player = true };
        tinker.AddItem(new Backpack());
        tinker.MoveToWorld(new Point3D(1400, 1720, 0), Map.Trammel);
        tinker.Skills.Tinkering.Base = 120.0; // chance (120 - 65) / (115 - 65) > 1: the craft cannot fail

        var tools = new TinkerTools();
        tinker.Backpack.DropItem(tools);
        tinker.Backpack.DropItem(new IronIngot(3));

        entry.CompleteCraft(1, false, tinker, tinkering, typeof(IronIngot), tools, null);

        var ring = tinker.Backpack.FindItemByType<GargishRing>();
        _out.WriteLine($"made: {ring?.GetType().Name} ingots left={tinker.Backpack.GetAmount(typeof(IronIngot))}");

        Assert.NotNull(ring);
        Assert.Equal(0, tinker.Backpack.GetAmount(typeof(IronIngot)));

        ring.Delete();
        tools.Delete();
        tinker.Delete();
    }
}
