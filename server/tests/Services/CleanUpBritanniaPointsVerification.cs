// CleanUpBritanniaPointsVerification.cs
//
// cc-P33 (F-3), stage 1: the points framework and Clean Up Britannia's valuation, ported from ServUO pub57
// Services/PointsSystems/{PointsSystem,CleanUpBritanniaData}.cs into server/customizations/Services/PointsSystems/.
// Notes in shard-migration notes/cc-P33-clean-up-britannia.md.
//
// Facts:
//   1. A sample of table entries pays exactly ServUO's value (line numbers in CleanUpBritanniaData.cs), stacks
//      multiply, and every key in the table is an Item: 453 ported rows plus our 16 extended resources.
//   2. The special cases pay ServUO's values: runic tools by uses, power scrolls, a scroll of transcendence, treasure
//      maps by level (old-system column), the slime statuette, Tokuno pigments by uses, an ancient SOS.
//   3. Crafted gear pays its first resource's amount times the material rate, truncated (ServUO :748-813).
//   4. Anti-farm, metals, leathers, scales and every extended resource: for every craftable piece of combat
//      equipment in every craft system, the piece pays no more than the most valuable form of the material it took.
//   5. Anti-farm, OSI's wood and cloth rates: measured and pinned. OSI pays crafted wood gear .17/.33/.67/2.17/3.17 a
//      board against boards of .10/.25/.50/2.0/3.0, and pays cloth gear .1 a cloth against cloth worth 0. So OSI's own
//      table lets a crafted wooden or cloth piece out-earn its materials. Recorded for Chase, not tuned here.
//   6. Anti-farm with the Smith Guild salvage bag (D-98) in the loop, every metal at Mining 300: what the bag gives
//      back is worth less than what the piece took, so craft, salvage, craft again and trash never beats trashing.
//   7. Points are per character: two characters on one account keep separate totals.
//   8. Awarding adds to lifetime; spending lowers points and never lifetime; an overdraft is refused.
//   9. The points save round-trips: two characters' points and lifetimes, and the reader stops on the last byte.
//      An unknown system name or version fails loudly.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Accounting.Security;
using Server.Engines.Craft;
using Server.Engines.Harvest;
using Server.Engines.Points;
using Server.Items;
using Server.Mobiles;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class CleanUpBritanniaPointsVerification
{
    private readonly ITestOutputHelper _out;

    public CleanUpBritanniaPointsVerification(ITestOutputHelper output)
    {
        _out = output;
        CleanUpCraftHost.EnsureCraftSystems();
    }

    private static double Points(Item item)
    {
        try
        {
            return CleanUpBritanniaData.GetPoints(item);
        }
        finally
        {
            item.Delete();
        }
    }

    // -- 1 ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void TableEntriesPayServUOsValues()
    {
        Assert.Equal(0.10, Points(new IronIngot()), 6);                 // :210
        Assert.Equal(1.0, Points(new IronIngot(10)), 6);                // stackable x amount (:49)
        Assert.Equal(0.50, Points(new DullCopperIngot()), 6);           // :211
        Assert.Equal(10.0, Points(new ValoriteIngot()), 6);             // :218
        Assert.Equal(100.0, Points(new ValoriteIngot(10)), 6);
        Assert.Equal(0.05, Points(new Board()), 6);                     // :234
        Assert.Equal(3.0, Points(new FrostwoodBoard()), 6);             // :240
        Assert.Equal(0.10, Points(new Leather()), 6);                   // :253
        Assert.Equal(2.0, Points(new BarbedLeather()), 6);              // :256
        Assert.Equal(0.30, Points(new Diamond()), 6);                   // :227
        Assert.Equal(25.0, Points(new BlueDiamond()), 6);               // :228
        Assert.Equal(0.05, Points(new Arrow()), 6);                     // :249
        Assert.Equal(0.01, Points(new Gold(1)), 6);                     // Miscellaneous, Gold 0.01
        Assert.Equal(10.0, Points(new Gold(1000)), 6);
        Assert.Equal(1600.0, Points(new Rope()), 6);                    // :206
        Assert.Equal(5.0, Points(new RockArtifact()), 6);               // rarity 1 stealable
        Assert.Equal(1400.0, Points(new SaddleArtifact()), 6);          // rarity 9 stealable
        Assert.Equal(0.10, Points(new Lockpick()), 6);                  // the table's last row
        Assert.Equal(100.0, Points(new MetallicClothDyetub()), 6);      // ServUO's MetallicClothDyeTub

        // Nothing outside the table, the special cases or equipment pays.
        Assert.Equal(0.0, Points(new IronOre()), 6);
        Assert.Equal(0.0, Points(new Log()), 6);
        Assert.Equal(0.0, Points(new Cloth()), 6);

        var entries = CleanUpBritanniaData.Entries;
        var notItems = entries.Keys.Where(t => !typeof(Item).IsAssignableFrom(t)).ToList();
        _out.WriteLine($"{entries.Count} table rows; not items: {string.Join(", ", notItems.Select(t => t.FullName))}");
        Assert.Empty(notItems);
        Assert.Equal(453 + 16, entries.Count);
    }

    // -- 2 ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void SpecialCasesPayServUOsValues()
    {
        Assert.Equal(40000.0, Points(new RunicHammer(CraftResource.Valorite, 5)), 6);   // 8000 a use
        Assert.Equal(50.0, Points(new RunicHammer(CraftResource.DullCopper, 10)), 6);   // 5 a use
        Assert.Equal(1200.0, Points(new RunicSewingKit(CraftResource.BarbedLeather, 3)), 6); // 400 a use
        Assert.Equal(30.0, Points(new RunicSewingKit(CraftResource.SpinedLeather, 3)), 6);   // 10 a use

        Assert.Equal(50.0, Points(new PowerScroll(SkillName.Swords, 105)), 6);
        Assert.Equal(100.0, Points(new PowerScroll(SkillName.Swords, 110)), 6);
        Assert.Equal(500.0, Points(new PowerScroll(SkillName.Swords, 115)), 6);
        Assert.Equal(2500.0, Points(new PowerScroll(SkillName.Swords, 120)), 6);

        Assert.Equal(20.0, Points(new ScrollofTranscendence(SkillName.Swords, 1.0)), 6); // value / 0.1 * 2

        int[] expected = [25, 50, 100, 250, 500, 750, 1000, 1000];
        for (var level = 0; level <= 7; level++)
        {
            Assert.Equal(expected[level], Points(new TreasureMap(level, Map.Trammel)), 6);
        }

        Assert.Equal(5000.0, Points(new MonsterStatuette(MonsterStatuetteType.Slime)), 6);
        Assert.Equal(0.0, Points(new MonsterStatuette(MonsterStatuetteType.Crocodile)), 6);
        Assert.Equal(1500.0, Points(new PigmentsOfTokuno(PigmentType.ParagonGold, 3)), 6); // 500 a use

        Assert.Equal(100.0, Points(new SOS { Level = 1 }), 6); // pinned's SOS() rolls a random level; 4 is ancient
        var ancient = new SOS { Level = 4 };
        Assert.True(ancient.IsAncient);
        Assert.Equal(2500.0, Points(ancient), 6);
    }

    // -- 3 ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void CraftedGearPaysItsMaterialsTruncated()
    {
        // pinned DefBlacksmithy: PlateChest 25 ingots, Longsword 12.
        Assert.Equal(25, CleanUpCraftHost.FirstResourceAmount(typeof(PlateChest)));
        Assert.Equal(12, CleanUpCraftHost.FirstResourceAmount(typeof(Longsword)));

        Assert.Equal(2.0, Points(new PlateChest()), 6);                                          // 25 x .1 = 2.5
        Assert.Equal(250.0, Points(new PlateChest { Resource = CraftResource.Valorite }), 6);   // 25 x 10
        Assert.Equal(11.0, Points(new PlateChest { Resource = CraftResource.DullCopper }), 6);  // 25 x .47 = 11.75
        Assert.Equal(1.0, Points(new Longsword()), 6);                                           // 12 x .1 = 1.2
        Assert.Equal(375.0, Points(new PlateChest { Resource = CraftResource.Platinum }), 6);   // ours, 25 x 15

        // Equipment no craft system makes pays nothing unless the table names it.
        Assert.Equal(0.0, Points(new MidnightBracers()), 6);
        Assert.Equal(5000.0, Points(new MidnightBracers { LootType = LootType.Cursed }), 6);
    }

    // -- 4, 5 ------------------------------------------------------------------------------------------------------

    [Fact]
    public void NoCraftedPieceOutEarnsItsMaterialsInMetalLeatherScaleOrOurResources()
    {
        var rows = CleanUpCraftHost.Measure(_out);
        var excess = rows.Where(r => r.Excess > 1e-9 && !CleanUpCraftHost.IsOsiWoodOrCloth(r.Resource)).ToList();

        foreach (var r in excess)
        {
            _out.WriteLine($"EXCESS {r}");
        }

        _out.WriteLine($"{rows.Count} piece-resource pairs measured, {excess.Count} out-earn their materials");
        Assert.True(rows.Count > 1000, $"only {rows.Count} pairs measured");
        Assert.Empty(excess);

        // Every extended resource was measured at least once.
        for (var res = CraftResource.Platinum; res <= CraftResource.Celestial; res++)
        {
            Assert.Contains(rows, r => r.Resource == res);
        }

        for (var res = CraftResource.Ironwood; res <= CraftResource.Starwood; res++)
        {
            Assert.Contains(rows, r => r.Resource == res);
        }
    }

    [Fact]
    public void OsisWoodAndClothRatesLetACraftedPieceOutEarnItsMaterials()
    {
        var rows = CleanUpCraftHost.Measure(null).Where(r => CleanUpCraftHost.IsOsiWoodOrCloth(r.Resource)).ToList();
        var excess = rows.Where(r => r.Excess > 1e-9).ToList();

        foreach (var group in excess.GroupBy(r => r.Resource))
        {
            var worst = group.OrderByDescending(r => r.Excess / Math.Max(r.Materials, 0.01)).First();
            _out.WriteLine($"{group.Key}: {group.Count()} pieces out-earn their materials; e.g. {worst}");
        }

        // The measurement this fact pins: OSI's own rates exceed OSI's own material values for these.
        Assert.Contains(excess, r => r.Resource == CraftResource.AshWood);
        Assert.Contains(excess, r => r.Resource == CraftResource.YewWood);
        Assert.Contains(excess, r => r.Resource == CraftResource.Frostwood);
        Assert.Contains(excess, r => r.Resource == CraftResource.None);

        // Heartwood's rate equals its board value, so it never out-earns.
        Assert.DoesNotContain(excess, r => r.Resource == CraftResource.Heartwood);
    }

    // -- 6 ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheGuildSalvageBagNeverReturnsMorePointsThanThePieceTook()
    {
        var cost = CleanUpCraftHost.FirstResourceAmount(typeof(PlateChest));

        for (var res = CraftResource.Iron; res <= CraftResource.Celestial; res++)
        {
            var piece = new PlateChest { Resource = res, PlayerConstructed = true };
            var pieceValue = Points(piece);

            var ingot = CraftResources.GetInfo(res).ResourceTypes[0];
            var perIngot = CleanUpCraftHost.UnitPoints(ingot);
            var returned = SmithGuildSalvageBag.IngotReturn(new PlateChest { PlayerConstructed = true }, 300.0, cost);

            var materials = cost * perIngot;
            var salvaged = returned * perIngot;

            _out.WriteLine($"{res}: piece {pieceValue}, {cost} ingots {materials}, salvage at 300 gives {returned} ({salvaged})");
            Assert.True(pieceValue <= materials + 1e-9, $"{res}: piece {pieceValue} > materials {materials}");
            Assert.True(salvaged < materials, $"{res}: salvage {salvaged} >= materials {materials}");
        }
    }

    // -- 7, 8 ------------------------------------------------------------------------------------------------------

    [Fact]
    public void PointsArePerCharacterNotPerAccount()
    {
        CleanUpCraftHost.EnsureAccounts();
        var account = new Account($"p33{Guid.NewGuid():N}"[..16], "p33-test-only");
        var a = new PlayerMobile();
        var b = new PlayerMobile();
        account[0] = a;
        account[1] = b;

        try
        {
            var cub = CleanUpBritanniaData.Instance;
            cub.AwardPoints(a, 12.5, message: false);
            cub.AwardPoints(b, 3.0, message: false);

            Assert.Same(a.Account, b.Account);
            Assert.Equal(12.5, cub.GetPoints(a), 6);
            Assert.Equal(3.0, cub.GetPoints(b), 6);
        }
        finally
        {
            a.Delete();
            b.Delete();
            account.Delete();
        }
    }

    [Fact]
    public void SpendingLowersPointsNeverLifetime()
    {
        var pm = new PlayerMobile();

        try
        {
            var cub = CleanUpBritanniaData.Instance;
            cub.AwardPoints(pm, 100, message: false);
            Assert.True(cub.DeductPoints(pm, 60));
            Assert.False(cub.DeductPoints(pm, 41)); // an overdraft is refused and changes nothing
            cub.AwardPoints(pm, 5, message: false);

            Assert.Equal(45.0, cub.GetPoints(pm), 6);
            Assert.Equal(105.0, cub.GetLifetimePoints(pm), 6);

            cub.AwardPoints(pm, -10, message: false); // ServUO :122: a non-positive award does nothing
            Assert.Equal(105.0, cub.GetLifetimePoints(pm), 6);
        }
        finally
        {
            pm.Delete();
        }
    }

    // -- 9 ---------------------------------------------------------------------------------------------------------

    [Fact]
    public void ThePointsSaveRoundTrips()
    {
        var a = new PlayerMobile();
        var b = new PlayerMobile();
        var cub = CleanUpBritanniaData.Instance;
        var saved = cub.PlayerTable.ToDictionary(kv => kv.Key, kv => ((CleanUpBritanniaEntry)kv.Value).Points);

        try
        {
            cub.Clear();
            cub.AwardPoints(a, 1234.56, message: false);
            cub.DeductPoints(a, 34.56);
            cub.AwardPoints(b, 0.1, message: false);

            var writer = new BufferWriter(true, new ConcurrentQueue<Type>());
            PointsSystemPersistence.WriteAll(writer);
            var bytes = writer.Buffer.AsSpan(0, (int)writer.Position).ToArray();
            _out.WriteLine($"{bytes.Length} bytes for two characters");

            cub.Clear();
            Assert.Equal(0.0, cub.GetPlayerEntry<CleanUpBritanniaEntry>(a, false)?.Lifetime ?? -1, 6);

            var reader = new BufferReader(bytes);
            PointsSystemPersistence.ReadAll(reader);
            Assert.Equal(bytes.Length, (int)reader.Position);

            Assert.Equal(2, cub.PlayerTable.Count);
            Assert.Equal(1200.0, cub.GetPoints(a), 6);
            Assert.Equal(1234.56, cub.GetLifetimePoints(a), 6);
            Assert.Equal(0.1, cub.GetPoints(b), 6);
            Assert.Equal(0.1, cub.GetLifetimePoints(b), 6);

            // An unknown system or version fails loudly.
            var unknown = new BufferWriter(true, new ConcurrentQueue<Type>());
            IGenericWriter u = unknown;
            u.WriteEncodedInt(0);
            u.WriteEncodedInt(1);
            u.Write("VirtueArtifacts");
            Assert.Throws<InvalidDataException>(() =>
                PointsSystemPersistence.ReadAll(new BufferReader(unknown.Buffer.AsSpan(0, (int)unknown.Position).ToArray())));

            var newer = new BufferWriter(true, new ConcurrentQueue<Type>());
            ((IGenericWriter)newer).WriteEncodedInt(1);
            Assert.Throws<InvalidDataException>(() =>
                PointsSystemPersistence.ReadAll(new BufferReader(newer.Buffer.AsSpan(0, (int)newer.Position).ToArray())));
        }
        finally
        {
            cub.Clear();
            a.Delete();
            b.Delete();
        }
    }
}

// Shared by the Clean Up facts: the craft tables the server builds at start, and the anti-farm measurement.
internal static class CleanUpCraftHost
{
    private static bool _accounts;

    public static void EnsureAccounts()
    {
        if (!_accounts)
        {
            Accounts.Configure();
            _accounts = true;
        }

        if (AccountSecurity.CurrentAlgorithm == PasswordProtectionAlgorithm.None)
        {
            AccountSecurity.CurrentAlgorithm = PasswordProtectionAlgorithm.PBKDF2;
        }
    }

    // Def*.Initialize() and the ServerStarted registrations do not run in the test host (CraftRegistrationsVerification).
    public static void EnsureCraftSystems()
    {
        if (DefBlacksmithy.CraftSystem == null) DefBlacksmithy.Initialize();
        if (DefTailoring.CraftSystem == null) DefTailoring.Initialize();
        if (DefCarpentry.CraftSystem == null) DefCarpentry.Initialize();
        if (DefBowFletching.CraftSystem == null) DefBowFletching.Initialize();
        if (DefTinkering.CraftSystem == null) DefTinkering.Initialize();
        if (DefAlchemy.CraftSystem == null) DefAlchemy.Initialize();
        if (DefCartography.CraftSystem == null) DefCartography.Initialize();
        if (DefCooking.CraftSystem == null) DefCooking.Initialize();
        if (DefGlassblowing.CraftSystem == null) DefGlassblowing.Initialize();
        if (DefInscription.CraftSystem == null) DefInscription.Initialize();
        if (DefMasonry.CraftSystem == null) DefMasonry.Initialize();

        BlacksmithyCraftRegistrations.Register();
        TailoringCraftRegistrations.Register();
        TinkeringCraftRegistrations.Register();
        CarpentryCraftRegistrations.Register();

        // Our extended ingots, boards and logs join the craft menus at ServerStarted (ClusterFMiningExtension.cs:52,
        // ClusterFLumberjackingExtension.cs:184), which the host never raises. Run those handlers once.
        if (!HasSubRes(DefBlacksmithy.CraftSystem, typeof(PlatinumIngot)))
        {
            RunServerStarted(typeof(ClusterFMiningExtension));
        }

        if (!HasSubRes(DefCarpentry.CraftSystem, typeof(IronwoodBoard)))
        {
            RunServerStarted(typeof(ClusterFLumberjackingExtension));
        }
    }

    private static bool HasSubRes(CraftSystem system, Type type)
    {
        for (var i = 0; i < system.CraftSubRes.Count; i++)
        {
            if (system.CraftSubRes.GetAt(i).ItemType == type)
            {
                return true;
            }
        }

        return false;
    }

    private static void RunServerStarted(Type extension) =>
        extension.GetMethod("OnServerStarted", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .Invoke(null, null);

    public static int FirstResourceAmount(Type type) =>
        CleanUpBritanniaData.CraftSystems.Select(s => s.CraftItems.SearchFor(type)).First(c => c != null).Resources[0].Amount;

    public static double UnitPoints(Type type)
    {
        var item = type.CreateInstance<Item>();

        try
        {
            item.Amount = 1;
            return CleanUpBritanniaData.GetPoints(item);
        }
        finally
        {
            item.Delete();
        }
    }

    // OSI's own wood and cloth rates, which the second anti-farm fact measures rather than asserts away.
    public static bool IsOsiWoodOrCloth(CraftResource res) =>
        res is CraftResource.None or >= CraftResource.RegularWood and <= CraftResource.Frostwood;

    public sealed record Row(string System, Type Piece, CraftResource Resource, int Amount, double PiecePoints, double Materials)
    {
        public double Excess => PiecePoints - Materials;

        public override string ToString() =>
            $"{System} {Piece.Name} [{Resource}] {Amount} x material = {Materials:0.##}, piece pays {PiecePoints:0.##}";
    }

    // Every craftable piece of combat equipment, made of every resource its craft system offers (including ours),
    // against the most valuable form of that resource (a log or its board, an ingot, a hide or its leather).
    public static List<Row> Measure(Xunit.Abstractions.ITestOutputHelper output)
    {
        var rows = new List<Row>();
        var skipped = 0;

        foreach (var system in CleanUpBritanniaData.CraftSystems)
        {
            // Each resource the system's menu offers, with the material type the menu names for it.
            var resources = new List<CraftResource>();
            var subResType = new Dictionary<CraftResource, Type>();

            if (system.CraftSubRes.Init)
            {
                for (var i = 0; i < system.CraftSubRes.Count; i++)
                {
                    var type = system.CraftSubRes.GetAt(i).ItemType;
                    var res = CraftResources.GetFromType(type);
                    resources.Add(res);
                    subResType[res] = type;
                }
            }

            foreach (var craftItem in system.CraftItems)
            {
                if (craftItem.Resources.Count == 0)
                {
                    continue;
                }

                Item probe;

                try
                {
                    probe = craftItem.ItemType.CreateInstance<Item>();
                }
                catch
                {
                    skipped++;
                    continue;
                }

                if (probe == null)
                {
                    skipped++;
                    continue;
                }

                var isEquipment = CleanUpBritanniaData.IsCombatEquipment(probe) &&
                                  !CleanUpBritanniaData.Entries.ContainsKey(probe.GetType());
                var baseResource = CleanUpBritanniaData.GetResource(probe);
                probe.Delete();

                if (!isEquipment)
                {
                    continue;
                }

                var amount = craftItem.Resources[0].Amount;
                var firstType = craftItem.Resources[0].ItemType;
                var usesSubRes = resources.Count > 0 && resources.Any(r => MaterialForms(subResType[r]).Contains(firstType));

                IEnumerable<CraftResource> tried = usesSubRes ? resources : [baseResource];

                foreach (var res in tried)
                {
                    var piece = craftItem.ItemType.CreateInstance<Item>();
                    SetResource(piece, res);
                    var piecePoints = CleanUpBritanniaData.GetPoints(piece);
                    piece.Delete();

                    var perUnit = usesSubRes ? MaterialForms(subResType[res]).Max(UnitPoints) : UnitPoints(firstType);

                    rows.Add(new Row(system.GetType().Name, craftItem.ItemType, usesSubRes ? res : baseResource,
                        amount, piecePoints, amount * perUnit));
                }
            }
        }

        output?.WriteLine($"{skipped} craft entries could not be built in the host and were skipped");
        return rows;
    }

    // A material and its other forms: what ResourceInfo lists for its resource, and its log or board twin by name.
    // The twin is needed because ResourceInfo's CraftResources.GetType stops the wood range at Frostwood, so GetInfo is
    // null for our eight extended woods (customizations/ResourceInfo.cs:1095; a defect of ours, noted in cc-P33).
    public static List<Type> MaterialForms(Type material)
    {
        var forms = new List<Type> { material };

        if (CraftResources.GetInfo(CraftResources.GetFromType(material)) is { } info)
        {
            forms.AddRange(info.ResourceTypes);
        }

        foreach (var (from, to) in new[] { ("Log", "Board"), ("Board", "Log") })
        {
            if (material.Name.EndsWith(from, StringComparison.Ordinal) &&
                material.Assembly.GetType($"{material.Namespace}.{material.Name[..^from.Length]}{to}") is { } twin)
            {
                forms.Add(twin);
            }
        }

        return forms.Distinct().ToList();
    }

    private static void SetResource(Item item, CraftResource res)
    {
        switch (item)
        {
            case BaseWeapon w:   w.Resource = res; break;
            case BaseArmor a:    a.Resource = res; break;
            case BaseClothing c: c.Resource = res; break;
            case BaseJewel j:    j.Resource = res; break;
        }
    }
}
