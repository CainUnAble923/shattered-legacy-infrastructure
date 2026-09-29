// cc-P8 (2026-09-29): the loot-table patches Chase approved. Two of the three gates, each through the patch that
// carries it (shard-migration/notes/cc-P8-loot-tables-and-d35.md):
//
//   Loot-hat-types-orc-tribal-mask.patch     appends OrcMask (ours) and TribalMask (pinned) to Loot.HatTypes, as
//                                            ServUO's Misc/Loot.cs has them. Every composite pool that names
//                                            HatTypes holds the same array object, so it sees the two at once.
//   StealableArtifacts-servuo-entries.patch  appends ServUO pub57's 58 stealable entries for the 55 types of ours
//                                            they name. Appended, never interleaved: the persistence file saves one
//                                            slot per entry BY INDEX (StealableArtifacts.Deserialize), and the live
//                                            world holds 80 slots. An entry inserted mid-table would hand every
//                                            later saved item to the wrong entry.
//
// Gate 3 (treasure chests) is not here: pinned has none of the lists it was costed as. See the note, section 2.
//
// Test-host fact that limits one assertion: the host has no tile data, so a type whose weight comes from
// tiledata.mul rather than a DefaultWeight override reads 1 here. Of the 55 that is LightInTheVoid alone (its
// GargishTalwar base has no override; the client's tiledata row 0x908 says 4, read for the note). The weight fact
// is exact for the other 54 and only a lower bound for that one.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Server;
using Server.Engines.Stealables;
using Server.Items;
using Server.SkillHandlers;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class P8LootTablesVerification
{
    private readonly ITestOutputHelper _out;

    public P8LootTablesVerification(ITestOutputHelper output) => _out = output;

    // ---- OrcMask and TribalMask in Loot.HatTypes ------------------------------------------------

    private static readonly Type[] PinnedHatTypes =
    [
        typeof(SkullCap), typeof(Bandana), typeof(FloppyHat),
        typeof(Cap), typeof(WideBrimHat), typeof(StrawHat),
        typeof(TallStrawHat), typeof(WizardsHat), typeof(Bonnet),
        typeof(FeatheredHat), typeof(TricorneHat), typeof(JesterHat)
    ];

    [Fact]
    public void HatTypesIsPinnedsTwelveThenOrcMaskAndTribalMask()
    {
        var hats = Loot.HatTypes;
        _out.WriteLine($"Loot.HatTypes: {hats.Length} [{string.Join(", ", hats.Select(t => t.Name))}]");

        Assert.Equal(14, hats.Length);
        Assert.Equal(PinnedHatTypes, hats.Take(12));
        Assert.Equal(typeof(OrcMask), hats[12]);
        Assert.Equal(typeof(TribalMask), hats[13]);
    }

    [Theory]
    [InlineData(typeof(OrcMask))]
    [InlineData(typeof(TribalMask))]
    public void EachAddedHatConstructsThroughLoot(Type type)
    {
        var hat = Loot.Construct<BaseHat>(type);
        Assert.NotNull(hat);
        Assert.IsType(type, hat);
        hat.Delete();
    }

    // The composites are private static Type[][] fields built from the public arrays at type initialisation. They
    // hold references, so the patch reaches them only because it edits the array itself. This fact proves that, and
    // prints the size of every pool the change touches: each existing entry's odds are 1/N before and 1/(N+2) after.
    [Fact]
    public void EveryPoolThatNamesHatTypesSeesBothMasks()
    {
        var pools = typeof(Loot)
            .GetFields(BindingFlags.NonPublic | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(Type[][]))
            .Select(f => (f.Name, Arrays: (Type[][])f.GetValue(null)))
            .Where(p => p.Arrays.Any(a => ReferenceEquals(a, Loot.HatTypes)))
            .OrderBy(p => p.Name)
            .ToList();

        foreach (var (name, arrays) in pools)
        {
            var n = arrays.Sum(a => a.Length);
            _out.WriteLine($"{name}: N {n - 2} -> {n}; each existing entry 1/{n - 2} -> 1/{n}; each mask 1/{n}");
            var flat = arrays.SelectMany(a => a).ToList();
            Assert.Contains(typeof(OrcMask), flat);
            Assert.Contains(typeof(TribalMask), flat);
        }

        Assert.Equal(18, pools.Count);

        // The two a player meets most: RandomHat on this shard, and the treasure chest's item roll.
        Assert.Equal(17, Pool("_aosHatTypes").Sum(a => a.Length));
        Assert.Equal(120, Pool("_aosWeaponOrRangedOrArmorOrHatOrShieldOrJewelryTypes").Sum(a => a.Length));
    }

    private static Type[][] Pool(string name) =>
        (Type[][])typeof(Loot).GetField(name, BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null);

    // ---- Stealable artifacts ---------------------------------------------------------------------

    // Pinned's own 80, in pinned's order. Nothing before index 80 may move.
    private static readonly Type[] PinnedEntries =
    [
        typeof(RockArtifact), typeof(SkullCandleArtifact), typeof(BottleArtifact), typeof(DamagedBooksArtifact),
        typeof(StretchedHideArtifact), typeof(BrazierArtifact), typeof(LampPostArtifact),
        typeof(BooksNorthArtifact), typeof(BooksWestArtifact), typeof(BooksFaceDownArtifact),
        typeof(StuddedLeggingsArtifact), typeof(EggCaseArtifact), typeof(SkinnedGoatArtifact),
        typeof(GruesomeStandardArtifact), typeof(BloodyWaterArtifact), typeof(TarotCardsArtifact),
        typeof(BackpackArtifact), typeof(StuddedTunicArtifact), typeof(CocoonArtifact),
        typeof(SkinnedDeerArtifact), typeof(SaddleArtifact), typeof(LeatherTunicArtifact), typeof(ZyronicClaw),
        typeof(TitansHammer), typeof(BladeOfTheRighteous), typeof(InquisitorsResolution),
        typeof(RuinedPaintingArtifact), typeof(Basket1Artifact), typeof(Basket2Artifact), typeof(Basket4Artifact),
        typeof(Basket5NorthArtifact), typeof(Basket5WestArtifact), typeof(Urn1Artifact), typeof(Urn2Artifact),
        typeof(Sculpture1Artifact), typeof(Sculpture2Artifact), typeof(TeapotNorthArtifact),
        typeof(TeapotWestArtifact), typeof(TowerLanternArtifact), typeof(ManStatuetteSouthArtifact),
        typeof(Basket3NorthArtifact), typeof(Basket3WestArtifact), typeof(Basket6Artifact),
        typeof(ZenRock1Artifact), typeof(FanNorthArtifact), typeof(FanWestArtifact), typeof(BowlsVerticalArtifact),
        typeof(ZenRock2Artifact), typeof(ZenRock3Artifact), typeof(Painting1NorthArtifact),
        typeof(Painting1WestArtifact), typeof(Painting2NorthArtifact), typeof(Painting2WestArtifact),
        typeof(TripleFanNorthArtifact), typeof(TripleFanWestArtifact), typeof(BowlArtifact), typeof(CupsArtifact),
        typeof(BowlsHorizontalArtifact), typeof(SakeArtifact), typeof(SwordDisplay1NorthArtifact),
        typeof(SwordDisplay1WestArtifact), typeof(Painting3Artifact), typeof(Painting4NorthArtifact),
        typeof(Painting4WestArtifact), typeof(SwordDisplay2NorthArtifact), typeof(SwordDisplay2WestArtifact),
        typeof(FlowersArtifact), typeof(DolphinLeftArtifact), typeof(DolphinRightArtifact),
        typeof(SwordDisplay3SouthArtifact), typeof(SwordDisplay3EastArtifact), typeof(SwordDisplay4WestArtifact),
        typeof(Painting5NorthArtifact), typeof(Painting5WestArtifact), typeof(SwordDisplay4NorthArtifact),
        typeof(SwordDisplay5NorthArtifact), typeof(SwordDisplay5WestArtifact), typeof(Painting6NorthArtifact),
        typeof(Painting6WestArtifact), typeof(ManStatuetteEastArtifact)
    ];

    // The 55 types of ours, from 17 files, that ServUO pub57's spawner names and pinned's did not.
    private static readonly Type[] OursAdded =
    [
        typeof(BatteredPanArtifact), typeof(BlockAndTackleArtifact), typeof(BloodySpoonArtifact),
        typeof(BookOfTruthArtifact), typeof(BottlesOfSpoiledWine1Artifact), typeof(BottlesOfSpoiledWine2Artifact),
        typeof(BottlesOfSpoiledWine3Artifact), typeof(CarvedMyrmydexGlyph), typeof(CrownOfArcaneTemperament),
        typeof(DragonTurtleHatchlingNet), typeof(DriedUpInkWellArtifact), typeof(DyingPlantArtifact),
        typeof(FakeCopperIngotsArtifact), typeof(FigureheadOfBmvArarat), typeof(GargishBentasVaseArtifact),
        typeof(GargishKnowledgeTotemArtifact), typeof(GargishLuckTotemArtifact),
        typeof(GargishMemorialStatueArtifact), typeof(GargishPortraitArtifact),
        typeof(GargishProtectiveTotemArtifact), typeof(GargishTraditionalVaseArtifact),
        typeof(HalfEatenSupperArtifact), typeof(JugsOfGoblinRotgutArtifact), typeof(KingsGildedStatue),
        typeof(KingsPainting1), typeof(KingsPainting2), typeof(LargeDyingPlantArtifact),
        typeof(LargePewterBowlArtifact), typeof(LightInTheVoid), typeof(MysteriousSupperArtifact),
        typeof(MysticsGuard), typeof(NaverysWeb1Artifact), typeof(NaverysWeb2Artifact),
        typeof(NaverysWeb3Artifact), typeof(NaverysWeb4Artifact), typeof(NaverysWeb5Artifact),
        typeof(NaverysWeb6Artifact), typeof(PricelessTreasureArtifact), typeof(PushmePullyuArtifact),
        typeof(RemnantsOfMeatLoafArtifact), typeof(RottedOarsArtifact), typeof(RustedPanArtifact),
        typeof(SacredLavaRock), typeof(ShipsBellOfBmvArarat), typeof(StaffOfResonance),
        typeof(SternAnchorOfBmvArarat), typeof(StolenBottlesOfLiquor1Artifact),
        typeof(StolenBottlesOfLiquor2Artifact), typeof(StolenBottlesOfLiquor3Artifact),
        typeof(StolenBottlesOfLiquor4Artifact), typeof(StretchedDinosaurHide), typeof(TyballsFlaskStandArtifact),
        typeof(ValkyriesGlaive), typeof(WakuOnASpit), typeof(WhiteTigerFigurine)
    ];

    [Fact]
    public void PinnedsEightyAreUnchangedAndOursFollowThem()
    {
        var entries = StealableArtifacts.Entries;
        _out.WriteLine($"entries: {entries.Length} (pinned 80, appended {entries.Length - 80})");

        Assert.Equal(138, entries.Length);
        Assert.Equal(PinnedEntries, entries.Take(80).Select(e => e.Type));

        var appended = entries.Skip(80).Select(e => e.Type).ToList();
        Assert.Equal(55, OursAdded.Distinct().Count());
        Assert.Equal(OursAdded.OrderBy(t => t.Name), appended.Distinct().OrderBy(t => t.Name));
    }

    public static IEnumerable<object[]> AppendedEntries() =>
        StealableArtifacts.Entries.Skip(80).Select((e, i) => new object[] { 80 + i, e.Type.Name });

    [Theory]
    [MemberData(nameof(AppendedEntries))]
    public void EachAppendedEntrySpawnsItsItemWhereServUOPutsIt(int index, string typeName)
    {
        var entry = StealableArtifacts.Entries[index];
        var item = entry.CreateInstance();

        _out.WriteLine(
            $"[{index}] {typeName} on {entry.Map} at {entry.Location}, delay {entry.MinDelay}-{entry.MaxDelay} min, weight {item?.Weight}"
        );

        Assert.NotNull(item);
        Assert.IsType(entry.Type, item);
        Assert.Contains(entry.Type, OursAdded);
        Assert.False(item.Movable);
        Assert.Equal(entry.Map, item.Map);
        Assert.Equal(entry.Location, item.Location);
        Assert.True(entry.MinDelay > 0 && entry.MaxDelay >= entry.MinDelay);

        // Stealing refuses anything heavier (Stealing.cs:256). A stealable nobody can steal is not reached.
        Assert.True(
            item.Weight + item.TotalWeight <= Stealing.MaxWeightToSteal,
            $"{typeName} weighs {item.Weight}, over the {Stealing.MaxWeightToSteal} stone steal cap"
        );

        item.Delete();
    }
}
