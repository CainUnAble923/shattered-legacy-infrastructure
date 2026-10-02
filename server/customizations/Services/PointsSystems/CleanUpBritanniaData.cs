// cc-P33 (F-3): Clean Up Britannia's valuation, ported from ServUO pub57
// Services/PointsSystems/CleanUpBritanniaData.cs. OSI's numbers, not ours to tune (fidelity rule).
//
//   GetPoints(item)           ServUO :37-178. The per-type table, then the special cases in ServUO's order.
//   GetPointsForEquipment     ServUO :748-813. Crafted gear pays its first resource's amount times a per-material
//                             rate, so a crafted item is worth what its materials were worth.
//   Entries                   ServUO :187-745, generated from that file line by line (494 rows). 453 resolve to a
//                             type in pinned or ours and are below with ServUO's line number. 41 name a type
//                             neither tree has; they are kept as MISSING comments so a later port can uncomment
//                             them, and they are listed in shard-migration notes/cc-P33-clean-up-britannia.md.
//
// Changed from ServUO, each argued in the notes:
//   * Imbuing weight: ServUO adds Imbuing.GetTotalWeight(item) / 30 for magic properties (:800-805). Pinned has no
//     imbuing and no item-property weight table (ServUO LootGeneration/Imbuing + ItemPropertyInfo, ~3,300 lines), so
//     the term is 0 here: magic gear pays its materials only. A recorded gap, not a choice.
//   * IVvVItem (:39): pinned has no Vice vs Virtue, so nothing is a VvV item.
//   * Bait (:113): pinned has no Bait item.
//   * Treasure maps (:118-149): ServUO picks the new-system values when TreasureMapInfo.NewSystem (Core.EJ, true on
//     this shard). Pinned's maps are the old system (levels 0-7, pinned Items/Maps/TreasureMap.cs), so the old-system
//     column is the one that describes the maps this shard drops.
//   * Craft systems: ServUO walks CraftSystem.Systems, filled in first-use order. Pinned has no such list; ours walks
//     the eleven pinned systems in a fixed order (CraftSystems below).
//   * Ours, marked "Shattered Legacy" below: values for the extended ingots and boards (ResourceInfo.cs) and their
//     crafted-gear rates. Each rate equals its ingot or board value, so crafting never adds points.
// Left out: the Cleanup Point Exchange (:815-901). One character per account (F-4), so it has no use. Registered.

using System;
using System.Collections.Generic;
using Server.Engines.Craft;
using Server.Items;
using Server.Mobiles;
using Server.Targeting;

namespace Server.Engines.Points;

// Ours: the lifetime total the Custodian ranks key on (F-3). Spending never lowers it.
public class CleanUpBritanniaEntry : PointsEntry
{
    public CleanUpBritanniaEntry(PlayerMobile pm) : base(pm)
    {
    }

    [CommandProperty(AccessLevel.GameMaster)]
    public double Lifetime { get; set; }

    public override void Serialize(IGenericWriter writer)
    {
        writer.WriteEncodedInt(0); // version
        writer.Write(Points);
        writer.Write(Lifetime);
    }

    public override void Deserialize(IGenericReader reader)
    {
        var version = reader.ReadEncodedInt();

        if (version != 0)
        {
            throw new System.IO.InvalidDataException(
                $"CleanUpBritanniaEntry version {version} is not one this build reads (0).");
        }

        Points = reader.ReadDouble();
        Lifetime = reader.ReadDouble();
    }
}

public class CleanUpBritanniaData : PointsSystem
{
    private static CleanUpBritanniaData _instance;

    public static CleanUpBritanniaData Instance => _instance ??= new CleanUpBritanniaData();

    // ServUO :30: Enabled = Core.ML.
    public static bool Enabled => Core.ML;

    public override PointsType Loyalty => PointsType.CleanUpBritannia;
    public override bool AutoAdd => true;
    public override double MaxPoints => double.MaxValue;

    public static void Configure()
    {
        _ = Instance;
    }

    public override PointsEntry GetSystemEntry(PlayerMobile pm) => new CleanUpBritanniaEntry(pm);

    protected override void OnPointsAwarded(PointsEntry entry, double added)
    {
        if (entry is CleanUpBritanniaEntry e)
        {
            e.Lifetime += added;
        }

        // Ours: the Custodian rank follows the lifetime total (ClusterFCustodianSystem.cs).
        ClusterFCustodianSystem.SyncStanding(entry.Player);
    }

    public double GetLifetimePoints(Mobile from) => GetPlayerEntry<CleanUpBritanniaEntry>(from)?.Lifetime ?? 0.0;

    // ServUO :180.
    public override void SendMessage(PlayerMobile from, double old, double points, bool quest)
    {
        // Your Clean Up Britannia point total is now ~1_VALUE~!
        from.SendLocalizedMessage(1151281, GetPoints(from).ToString());
    }

    // -- Valuation -------------------------------------------------------------------------------------------------

    public static double GetPoints(Item item)
    {
        if (item == null || item.Deleted)
        {
            return 0;
        }

        double points = 0;

        if (Entries.TryGetValue(item.GetType(), out var value))
        {
            points = value;

            if (item is SOS { IsAncient: true })
            {
                points = 2500;
            }

            if (item.Stackable)
            {
                points *= item.Amount;
            }

            return points;
        }

        if (item is RunicHammer hammer)
        {
            points = hammer.Resource switch
            {
                CraftResource.DullCopper => 5 * hammer.UsesRemaining,
                CraftResource.ShadowIron => 10 * hammer.UsesRemaining,
                CraftResource.Copper     => 25 * hammer.UsesRemaining,
                CraftResource.Bronze     => 100 * hammer.UsesRemaining,
                CraftResource.Gold       => 250 * hammer.UsesRemaining,
                CraftResource.Agapite    => 1000 * hammer.UsesRemaining,
                CraftResource.Verite     => 4000 * hammer.UsesRemaining,
                CraftResource.Valorite   => 8000 * hammer.UsesRemaining,
                _                        => 0
            };
        }
        else if (item is RunicSewingKit sewing)
        {
            points = sewing.Resource switch
            {
                CraftResource.SpinedLeather => 10 * sewing.UsesRemaining,
                CraftResource.HornedLeather => 100 * sewing.UsesRemaining,
                CraftResource.BarbedLeather => 400 * sewing.UsesRemaining,
                _                           => 0
            };
        }
        else if (item is PowerScroll ps)
        {
            if (ps.Value == 105)
            {
                points = 50;
            }
            else if (ps.Value == 110)
            {
                points = 100;
            }
            else if (ps.Value == 115)
            {
                points = 500;
            }
            else if (ps.Value == 120)
            {
                points = 2500;
            }
        }
        else if (item is ScrollofTranscendence sot)
        {
            points = sot.Value / 0.1 * 2;
        }
        else if (item is TreasureMap tmap)
        {
            // ServUO :136-148, the old-system column (see the header).
            return tmap.Level switch
            {
                1      => 50,
                2      => 100,
                3      => 250,
                4      => 500,
                5      => 750,
                6 or 7 => 1000,
                _      => 25
            };
        }
        else if (item is MidnightBracers && item.LootType == LootType.Cursed)
        {
            points = 5000;
        }
        else if (item is MonsterStatuette ms)
        {
            if (ms.Type == MonsterStatuetteType.Slime)
            {
                points = 5000;
            }
        }
        else if (item is PigmentsOfTokuno or LesserPigmentsOfTokuno)
        {
            points = 500 * ((BasePigmentsOfTokuno)item).UsesRemaining;
        }
        else if (IsCombatEquipment(item))
        {
            points = GetPointsForEquipment(item);
        }

        if (item.LootType != LootType.Blessed && points < 100 && item is IShipwreckedItem { IsShipwreckedItem: true })
        {
            points = 100;
        }

        return points;
    }

    // ServUO's ICombatEquipment is implemented by exactly these four bases (ServUO BaseArmor.cs:15,
    // BaseClothing.cs:19, BaseJewel.cs:24, BaseWeapon.cs:33). Pinned has no such interface.
    public static bool IsCombatEquipment(Item item) => item is BaseWeapon or BaseArmor or BaseClothing or BaseJewel;

    public static IEnumerable<CraftSystem> CraftSystems
    {
        get
        {
            CraftSystem[] systems =
            [
                DefBlacksmithy.CraftSystem, DefTailoring.CraftSystem, DefCarpentry.CraftSystem,
                DefBowFletching.CraftSystem, DefTinkering.CraftSystem, DefAlchemy.CraftSystem,
                DefCartography.CraftSystem, DefCooking.CraftSystem, DefGlassblowing.CraftSystem,
                DefInscription.CraftSystem, DefMasonry.CraftSystem
            ];

            foreach (var system in systems)
            {
                if (system != null)
                {
                    yield return system;
                }
            }
        }
    }

    public static CraftResource GetResource(Item item) => item switch
    {
        BaseWeapon w   => w.Resource,
        BaseArmor a    => a.Resource,
        BaseClothing c => c.Resource,
        BaseJewel j    => j.Resource,
        _              => CraftResource.None
    };

    // ServUO :748.
    public static int GetPointsForEquipment(Item item)
    {
        if (item is IEpiphanyArmor)
        {
            return 1000;
        }

        var type = item.GetType();

        if (type == typeof(SilverRing))
        {
            type = typeof(GoldRing);
        }
        else if (type == typeof(SilverBracelet))
        {
            type = typeof(GoldBracelet);
        }

        foreach (var system in CraftSystems)
        {
            var crItem = system.CraftItems.SearchFor(type);

            if (crItem?.Resources == null)
            {
                continue;
            }

            double amount = crItem.Resources.Count > 0 ? crItem.Resources[0].Amount : 1;

            // Every equipment base carries a Resource in pinned, as ServUO's IResource does.
            var award = amount * GetEquipmentRate(GetResource(item));

            // ServUO :800-805 adds Imbuing.GetTotalWeight(item, ...) / 30 here. Pinned has no imbuing weight
            // table, so the term is 0 (see the header).

            return (int)award;
        }

        return 0;
    }

    // ServUO :777-797, then ours.
    public static double GetEquipmentRate(CraftResource resource) => resource switch
    {
        CraftResource.DullCopper    => .47,
        CraftResource.ShadowIron    => .73,
        CraftResource.Copper        => 1.0,
        CraftResource.Bronze        => 1.47,
        CraftResource.Gold          => 2.5,
        CraftResource.Agapite       => 5.0,
        CraftResource.Verite        => 8.5,
        CraftResource.Valorite      => 10,
        CraftResource.SpinedLeather => 0.5,
        CraftResource.HornedLeather => 1.0,
        CraftResource.BarbedLeather => 2.0,
        CraftResource.OakWood       => .17,
        CraftResource.AshWood       => .33,
        CraftResource.YewWood       => .67,
        CraftResource.Heartwood     => 1.0,
        CraftResource.Bloodwood     => 2.17,
        CraftResource.Frostwood     => 3.17,

        // Shattered Legacy: the extended tiers pay exactly their ingot or board value (ExtendedValue).
        >= CraftResource.Platinum and <= CraftResource.Celestial => ExtendedValue(resource),
        >= CraftResource.Ironwood and <= CraftResource.Starwood  => ExtendedValue(resource),

        _ => .1
    };

    // Shattered Legacy (ours, registered): the extended ingots and boards continue OSI's top tier by the
    // post-valorite multipliers our smith commissions already use (ClusterFSmithCommissions.PostValMultiplier,
    // 1.5 to 5.0): ingots from valorite's 10.0, boards from frostwood's 3.0.
    public static double ExtendedValue(CraftResource resource) => resource switch
    {
        CraftResource.Platinum   => 15.0,
        CraftResource.Toxic      => 20.0,
        CraftResource.Blaze      => 25.0,
        CraftResource.Frost      => 30.0,
        CraftResource.Obsidian   => 35.0,
        CraftResource.Mythril    => 40.0,
        CraftResource.Adamantium => 45.0,
        CraftResource.Celestial  => 50.0,
        CraftResource.Ironwood   => 4.5,
        CraftResource.Ghostwood  => 6.0,
        CraftResource.Emberbark  => 7.5,
        CraftResource.Frostbark  => 9.0,
        CraftResource.Shadowbark => 10.5,
        CraftResource.Runewood   => 12.0,
        CraftResource.Voidwood   => 13.5,
        CraftResource.Starwood   => 15.0,
        _                        => 0
    };

    // -- The table ---------------------------------------------------------------------------------------------------

    private static Dictionary<Type, double> _entries;

    public static IReadOnlyDictionary<Type, double> Entries => _entries ??= InitializeEntries();

    private static Dictionary<Type, double> _building;

    private static void Add(Type type, double points) => _building[type] = points;

    private static Dictionary<Type, double> InitializeEntries()
    {
        _building = new Dictionary<Type, double>();

        Add(typeof(global::Server.Items.DecorativeTopiary), 2.0); // :192

        // Fishing
        Add(typeof(global::Server.Items.LargeFishingNet), 500.0); // :195
        Add(typeof(global::Server.Items.AntiqueWeddingDress), 500.0); // :196
        Add(typeof(global::Server.Items.BronzedArmorValkyrie), 500.0); // :197
        // MISSING :198 BaseCrabAndLobster = 1.0
        Add(typeof(global::Server.Items.KelpWovenLeggings), 500.0); // :199
        Add(typeof(global::Server.Items.FabledFishingNet), 2500.0); // :200
        // MISSING :201 LavaRock = 500.0
        // MISSING :202 SmugglersLiquor = 30.0
        Add(typeof(global::Server.Items.MessageInABottle), 100.0); // :203
        Add(typeof(global::Server.Items.SOS), 100.0); // :204
        Add(typeof(global::Server.Items.RunedDriftwoodBow), 500.0); // :205
        Add(typeof(global::Server.Items.Rope), 1600.0); // :206
        Add(typeof(global::Server.Items.SpecialFishingNet), 250.0); // :207

        // Mining
        Add(typeof(global::Server.Items.IronIngot), 0.10); // :210
        Add(typeof(global::Server.Items.DullCopperIngot), 0.50); // :211
        Add(typeof(global::Server.Items.ShadowIronIngot), 0.75); // :212
        Add(typeof(global::Server.Items.CopperIngot), 1.0); // :213
        Add(typeof(global::Server.Items.BronzeIngot), 1.50); // :214
        Add(typeof(global::Server.Items.GoldIngot), 2.50); // :215
        Add(typeof(global::Server.Items.AgapiteIngot), 5.0); // :216
        Add(typeof(global::Server.Items.VeriteIngot), 8.50); // :217
        Add(typeof(global::Server.Items.ValoriteIngot), 10.0); // :218
        Add(typeof(global::Server.Items.Amber), 0.30); // :219
        Add(typeof(global::Server.Items.Citrine), 0.30); // :220
        Add(typeof(global::Server.Items.Ruby), 0.30); // :221
        Add(typeof(global::Server.Items.Tourmaline), 0.30); // :222
        Add(typeof(global::Server.Items.Amethyst), 0.30); // :223
        Add(typeof(global::Server.Items.Emerald), 0.30); // :224
        Add(typeof(global::Server.Items.Sapphire), 0.30); // :225
        Add(typeof(global::Server.Items.StarSapphire), 0.30); // :226
        Add(typeof(global::Server.Items.Diamond), 0.30); // :227
        Add(typeof(global::Server.Items.BlueDiamond), 25.0); // :228
        Add(typeof(global::Server.Items.FireRuby), 25.0); // :229
        Add(typeof(global::Server.Items.PerfectEmerald), 25.0); // :230
        Add(typeof(global::Server.Items.DarkSapphire), 25.0); // :231
        Add(typeof(global::Server.Items.Turquoise), 25.0); // :232
        Add(typeof(global::Server.Items.EcruCitrine), 25.0); // :233
        Add(typeof(global::Server.Items.WhitePearl), 25.0); // :234
        // MISSING :235 SmallPieceofBlackrock = 10.0

        // Lumberjacking
        Add(typeof(global::Server.Items.Board), 0.05); // :238
        Add(typeof(global::Server.Items.OakBoard), 0.10); // :239
        Add(typeof(global::Server.Items.AshBoard), 0.25); // :240
        Add(typeof(global::Server.Items.YewBoard), 0.50); // :241
        Add(typeof(global::Server.Items.HeartwoodBoard), 1.0); // :242
        Add(typeof(global::Server.Items.BloodwoodBoard), 2.0); // :243
        Add(typeof(global::Server.Items.FrostwoodBoard), 3.0); // :244
        Add(typeof(global::Server.Items.BarkFragment), 1.60); // :245
        Add(typeof(global::Server.Items.LuminescentFungi), 2.0); // :246
        Add(typeof(global::Server.Items.SwitchItem), 3.0); // :247
        Add(typeof(global::Server.Items.ParasiticPlant), 6.0); // :248
        Add(typeof(global::Server.Items.BrilliantAmber), 62.0); // :249

        // Fletching
        Add(typeof(global::Server.Items.Arrow), 0.05); // :252
        Add(typeof(global::Server.Items.Bolt), 0.05); // :253

        // Tailoring
        Add(typeof(global::Server.Items.Leather), 0.10); // :256
        Add(typeof(global::Server.Items.SpinedLeather), 0.50); // :257
        Add(typeof(global::Server.Items.HornedLeather), 1.0); // :258
        Add(typeof(global::Server.Items.BarbedLeather), 2.0); // :259
        Add(typeof(global::Server.Items.Fur), 0.10); // :260

        // BOD Rewards
        Add(typeof(global::Server.Items.Sandals), 2.0); // :264
        Add(typeof(global::Server.Items.LeatherGlovesOfMining), 50.0); // :265
        Add(typeof(global::Server.Items.StuddedGlovesOfMining), 100.0); // :266
        Add(typeof(global::Server.Items.RingmailGlovesOfMining), 500.0); // :267

        // ArtifactRarity 1 Stealable Artifacts
        Add(typeof(global::Server.Items.RockArtifact), 5.0); // :270
        Add(typeof(global::Server.Items.SkullCandleArtifact), 5.0); // :271
        Add(typeof(global::Server.Items.BottleArtifact), 5.0); // :272
        Add(typeof(global::Server.Items.DamagedBooksArtifact), 5.0); // :273
        Add(typeof(global::Server.Items.Basket1Artifact), 5.0); // :274
        Add(typeof(global::Server.Items.Basket2Artifact), 5.0); // :275
        Add(typeof(global::Server.Items.Basket3NorthArtifact), 5.0); // :276
        Add(typeof(global::Server.Items.Basket3WestArtifact), 5.0); // :277

        // ArtifactRarity 2 Stealable Artifacts
        Add(typeof(global::Server.Items.StretchedHideArtifact), 15.0); // :280
        Add(typeof(global::Server.Items.BrazierArtifact), 15.0); // :281
        Add(typeof(global::Server.Items.Basket4Artifact), 15.0); // :282
        Add(typeof(global::Server.Items.Basket5NorthArtifact), 15.0); // :283
        Add(typeof(global::Server.Items.Basket5WestArtifact), 15.0); // :284
        Add(typeof(global::Server.Items.Basket6Artifact), 15.0); // :285
        Add(typeof(global::Server.Items.ZenRock1Artifact), 15.0); // :286

        // ArtifactRarity 3 Stealable Artifacts
        Add(typeof(global::Server.Items.LampPostArtifact), 25.0); // :289
        Add(typeof(global::Server.Items.BooksNorthArtifact), 25.0); // :290
        Add(typeof(global::Server.Items.BooksWestArtifact), 25.0); // :291
        Add(typeof(global::Server.Items.BooksFaceDownArtifact), 25.0); // :292
        Add(typeof(global::Server.Items.BowlsVerticalArtifact), 25.0); // :293
        Add(typeof(global::Server.Items.FanWestArtifact), 25.0); // :294
        Add(typeof(global::Server.Items.FanNorthArtifact), 25.0); // :295
        Add(typeof(global::Server.Items.Sculpture1Artifact), 25.0); // :296
        Add(typeof(global::Server.Items.Sculpture2Artifact), 25.0); // :297
        Add(typeof(global::Server.Items.TeapotWestArtifact), 25.0); // :298
        Add(typeof(global::Server.Items.TeapotNorthArtifact), 25.0); // :299
        Add(typeof(global::Server.Items.TowerLanternArtifact), 25.0); // :300
        Add(typeof(global::Server.Items.Urn1Artifact), 25.0); // :301
        Add(typeof(global::Server.Items.Urn2Artifact), 25.0); // :302
        Add(typeof(global::Server.Items.ZenRock2Artifact), 25.0); // :303
        Add(typeof(global::Server.Items.ZenRock3Artifact), 25.0); // :304
        Add(typeof(global::Server.Items.JugsOfGoblinRotgutArtifact), 25.0); // :305
        Add(typeof(global::Server.Items.MysteriousSupperArtifact), 25.0); // :306

        // ArtifactRarity 4 Stealable Artifacts
        Add(typeof(global::Server.Items.BowlArtifact), 50.0); // :309
        Add(typeof(global::Server.Items.BowlsHorizontalArtifact), 50.0); // :310
        Add(typeof(global::Server.Items.CupsArtifact), 50.0); // :311
        Add(typeof(global::Server.Items.TripleFanWestArtifact), 50.0); // :312
        Add(typeof(global::Server.Items.TripleFanNorthArtifact), 50.0); // :313
        Add(typeof(global::Server.Items.Painting1WestArtifact), 50.0); // :314
        Add(typeof(global::Server.Items.Painting1NorthArtifact), 50.0); // :315
        Add(typeof(global::Server.Items.Painting2WestArtifact), 50.0); // :316
        Add(typeof(global::Server.Items.Painting2NorthArtifact), 50.0); // :317
        Add(typeof(global::Server.Items.SakeArtifact), 50.0); // :318
        Add(typeof(global::Server.Items.StolenBottlesOfLiquor1Artifact), 50.0); // :319
        Add(typeof(global::Server.Items.StolenBottlesOfLiquor2Artifact), 50.0); // :320
        Add(typeof(global::Server.Items.BottlesOfSpoiledWine1Artifact), 50.0); // :321
        Add(typeof(global::Server.Items.NaverysWeb1Artifact), 50.0); // :322
        Add(typeof(global::Server.Items.NaverysWeb2Artifact), 50.0); // :323

        // ArtifactRarity 5 Stealable Artifacts
        Add(typeof(global::Server.Items.Painting3Artifact), 100.0); // :326
        Add(typeof(global::Server.Items.SwordDisplay1WestArtifact), 100.0); // :327
        Add(typeof(global::Server.Items.SwordDisplay1NorthArtifact), 100.0); // :328
        Add(typeof(global::Server.Items.DyingPlantArtifact), 100.0); // :329
        Add(typeof(global::Server.Items.LargePewterBowlArtifact), 100.0); // :330
        Add(typeof(global::Server.Items.NaverysWeb3Artifact), 100.0); // :331
        Add(typeof(global::Server.Items.NaverysWeb4Artifact), 100.0); // :332
        Add(typeof(global::Server.Items.NaverysWeb5Artifact), 100.0); // :333
        Add(typeof(global::Server.Items.NaverysWeb6Artifact), 100.0); // :334
        Add(typeof(global::Server.Items.BloodySpoonArtifact), 100.0); // :335
        Add(typeof(global::Server.Items.RemnantsOfMeatLoafArtifact), 100.0); // :336
        Add(typeof(global::Server.Items.HalfEatenSupperArtifact), 100.0); // :337
        Add(typeof(global::Server.Items.BackpackArtifact), 100.0); // :338
        Add(typeof(global::Server.Items.BloodyWaterArtifact), 100.0); // :339
        Add(typeof(global::Server.Items.EggCaseArtifact), 100.0); // :340
        Add(typeof(global::Server.Items.GruesomeStandardArtifact), 100.0); // :341
        Add(typeof(global::Server.Items.SkinnedGoatArtifact), 100.0); // :342
        Add(typeof(global::Server.Items.StuddedLeggingsArtifact), 100.0); // :343
        Add(typeof(global::Server.Items.TarotCardsArtifact), 100.0); // :344

        // ArtifactRarity 6 Stealable Artifacts
        Add(typeof(global::Server.Items.Painting4WestArtifact), 200.0); // :347
        Add(typeof(global::Server.Items.Painting4NorthArtifact), 200.0); // :348
        Add(typeof(global::Server.Items.SwordDisplay2WestArtifact), 200.0); // :349
        Add(typeof(global::Server.Items.SwordDisplay2NorthArtifact), 200.0); // :350
        Add(typeof(global::Server.Items.LargeDyingPlantArtifact), 200.0); // :351
        Add(typeof(global::Server.Items.GargishLuckTotemArtifact), 200.0); // :352
        Add(typeof(global::Server.Items.BookOfTruthArtifact), 200.0); // :353
        Add(typeof(global::Server.Items.GargishTraditionalVaseArtifact), 200.0); // :354
        Add(typeof(global::Server.Items.GargishProtectiveTotemArtifact), 200.0); // :355
        Add(typeof(global::Server.Items.BottlesOfSpoiledWine2Artifact), 200.0); // :356
        Add(typeof(global::Server.Items.BatteredPanArtifact), 200.0); // :357
        Add(typeof(global::Server.Items.RustedPanArtifact), 200.0); // :358

        // ArtifactRarity 7 Stealable Artifacts
        Add(typeof(global::Server.Items.FlowersArtifact), 350.0); // :361
        Add(typeof(global::Server.Items.GargishBentasVaseArtifact), 350.0); // :362
        Add(typeof(global::Server.Items.GargishPortraitArtifact), 350.0); // :363
        Add(typeof(global::Server.Items.GargishKnowledgeTotemArtifact), 350.0); // :364
        Add(typeof(global::Server.Items.GargishMemorialStatueArtifact), 350.0); // :365
        Add(typeof(global::Server.Items.StolenBottlesOfLiquor3Artifact), 350.0); // :366
        Add(typeof(global::Server.Items.BottlesOfSpoiledWine3Artifact), 350.0); // :367
        Add(typeof(global::Server.Items.DriedUpInkWellArtifact), 350.0); // :368
        Add(typeof(global::Server.Items.FakeCopperIngotsArtifact), 350.0); // :369
        Add(typeof(global::Server.Items.CocoonArtifact), 350.0); // :370
        Add(typeof(global::Server.Items.StuddedTunicArtifact), 350.0); // :371

        // ArtifactRarity 8 Stealable Artifacts
        Add(typeof(global::Server.Items.Painting5WestArtifact), 750.0); // :374
        Add(typeof(global::Server.Items.Painting5NorthArtifact), 750.0); // :375
        Add(typeof(global::Server.Items.DolphinLeftArtifact), 750.0); // :376
        Add(typeof(global::Server.Items.DolphinRightArtifact), 750.0); // :377
        Add(typeof(global::Server.Items.SwordDisplay3SouthArtifact), 750.0); // :378
        Add(typeof(global::Server.Items.SwordDisplay3EastArtifact), 750.0); // :379
        Add(typeof(global::Server.Items.SwordDisplay4WestArtifact), 750.0); // :380
        Add(typeof(global::Server.Items.PushmePullyuArtifact), 750.0); // :381
        Add(typeof(global::Server.Items.StolenBottlesOfLiquor4Artifact), 750.0); // :382
        Add(typeof(global::Server.Items.RottedOarsArtifact), 750.0); // :383
        Add(typeof(global::Server.Items.PricelessTreasureArtifact), 750.0); // :384
        Add(typeof(global::Server.Items.SkinnedDeerArtifact), 750.0); // :385

        // ArtifactRarity 9 Stealable Artifacts
        Add(typeof(global::Server.Items.Painting6WestArtifact), 1400.0); // :388
        Add(typeof(global::Server.Items.Painting6NorthArtifact), 1400.0); // :389
        Add(typeof(global::Server.Items.ManStatuetteSouthArtifact), 1400.0); // :390
        Add(typeof(global::Server.Items.ManStatuetteEastArtifact), 1400.0); // :391
        Add(typeof(global::Server.Items.SwordDisplay4NorthArtifact), 1400.0); // :392
        Add(typeof(global::Server.Items.SwordDisplay5WestArtifact), 1400.0); // :393
        Add(typeof(global::Server.Items.SwordDisplay5NorthArtifact), 1400.0); // :394
        Add(typeof(global::Server.Items.TyballsFlaskStandArtifact), 1400.0); // :395
        Add(typeof(global::Server.Items.BlockAndTackleArtifact), 1400.0); // :396
        Add(typeof(global::Server.Items.LeatherTunicArtifact), 1400.0); // :397
        Add(typeof(global::Server.Items.SaddleArtifact), 1400.0); // :398

        // ArtifactRarity 10
        Add(typeof(global::Server.Items.TitansHammer), 2750.0); // :401
        Add(typeof(global::Server.Items.ZyronicClaw), 2750.0); // :402
        Add(typeof(global::Server.Items.InquisitorsResolution), 2750.0); // :403
        Add(typeof(global::Server.Items.BladeOfTheRighteous), 2750.0); // :404
        Add(typeof(global::Server.Items.LegacyOfTheDreadLord), 2750.0); // :405
        Add(typeof(global::Server.Items.TheTaskmaster), 2750.0); // :406

        // Virtue Artifacts
        Add(typeof(global::Server.Items.TenthAnniversarySculpture), 1500.0); // :409
        Add(typeof(global::Server.Items.MapOfTheKnownWorld), 1500.0); // :410
        // MISSING :411 AnkhPendant = 1500.0
        // MISSING :412 DragonsEnd = 1500.0
        // MISSING :413 JaanasStaff = 1500.0
        // MISSING :414 KatrinasCrook = 1500.0
        // MISSING :415 LordBlackthornsExemplar = 1500.0
        // MISSING :416 SentinelsGuard = 1500.0
        Add(typeof(global::Server.Items.CompassionArms), 1500.0); // :417
        Add(typeof(global::Server.Items.JusticeBreastplate), 1500.0); // :418
        Add(typeof(global::Server.Items.ValorGauntlets), 1500.0); // :419
        Add(typeof(global::Server.Items.HonestyGorget), 1500.0); // :420
        Add(typeof(global::Server.Items.SpiritualityHelm), 1500.0); // :421
        Add(typeof(global::Server.Items.HonorLegs), 1500.0); // :422
        Add(typeof(global::Server.Items.SacrificeSollerets), 1500.0); // :423

        // Minor Artifacts (ML/Peerless/Tokuno)
        Add(typeof(global::Server.Items.CandelabraOfSouls), 100.0); // :426
        Add(typeof(global::Server.Items.GhostShipAnchor), 100.0); // :427
        Add(typeof(global::Server.Items.GoldBricks), 100.0); // :428
        Add(typeof(global::Server.Items.PhillipsWoodenSteed), 100.0); // :429
        Add(typeof(global::Server.Items.SeahorseStatuette), 100.0); // :430
        Add(typeof(global::Server.Items.ShipModelOfTheHMSCape), 100.0); // :431
        Add(typeof(global::Server.Items.AdmiralsHeartyRum), 100.0); // :432
        Add(typeof(global::Server.Items.AlchemistsBauble), 100.0); // :433
        Add(typeof(global::Server.Items.ArcticDeathDealer), 100.0); // :434
        Add(typeof(global::Server.Items.BlazeOfDeath), 100.0); // :435
        Add(typeof(global::Server.Items.BurglarsBandana), 100.0); // :436
        Add(typeof(global::Server.Items.CaptainQuacklebushsCutlass), 100.0); // :437
        Add(typeof(global::Server.Items.CavortingClub), 100.0); // :438
        Add(typeof(global::Server.Items.DreadPirateHat), 100.0); // :439
        Add(typeof(global::Server.Items.EnchantedTitanLegBone), 100.0); // :440
        Add(typeof(global::Server.Items.GwennosHarp), 100.0); // :441
        Add(typeof(global::Server.Items.IolosLute), 100.0); // :442
        Add(typeof(global::Server.Items.LunaLance), 100.0); // :443
        Add(typeof(global::Server.Items.NightsKiss), 100.0); // :444
        Add(typeof(global::Server.Items.NoxRangersHeavyCrossbow), 100.0); // :445
        Add(typeof(global::Server.Items.PolarBearMask), 100.0); // :446
        Add(typeof(global::Server.Items.VioletCourage), 100.0); // :447
        Add(typeof(global::Server.Items.GlovesOfThePugilist), 100.0); // :448
        Add(typeof(global::Server.Items.PixieSwatter), 100.0); // :449
        Add(typeof(global::Server.Items.WrathOfTheDryad), 100.0); // :450
        Add(typeof(global::Server.Items.StaffOfPower), 100.0); // :451
        Add(typeof(global::Server.Items.OrcishVisage), 100.0); // :452
        Add(typeof(global::Server.Items.BowOfTheJukaKing), 100.0); // :453
        Add(typeof(global::Server.Items.ColdBlood), 100.0); // :454
        Add(typeof(global::Server.Items.CreepingVine), 100.0); // :455
        // MISSING :456 ForgedPardon = 100.0
        // MISSING :457 ManaPhasingOrb = 500.0
        // MISSING :458 RunedSashOfWarding = 100.0
        // MISSING :459 SurgeShield = 100.0
        Add(typeof(global::Server.Items.HeartOfTheLion), 100.0); // :460
        Add(typeof(global::Server.Items.ShieldOfInvulnerability), 100.0); // :461
        Add(typeof(global::Server.Items.AegisOfGrace), 100.0); // :462
        Add(typeof(global::Server.Items.BladeDance), 100.0); // :463
        Add(typeof(global::Server.Items.BloodwoodSpirit), 100.0); // :464
        Add(typeof(global::Server.Items.Bonesmasher), 100.0); // :465
        Add(typeof(global::Server.Items.Boomstick), 100.0); // :466
        Add(typeof(global::Server.Items.BrightsightLenses), 100.0); // :467
        Add(typeof(global::Server.Items.FeyLeggings), 100.0); // :468
        Add(typeof(global::Server.Items.FleshRipper), 100.0); // :469
        Add(typeof(global::Server.Items.HelmOfSwiftness), 100.0); // :470
        Add(typeof(global::Server.Items.PadsOfTheCuSidhe), 100.0); // :471
        Add(typeof(global::Server.Items.QuiverOfRage), 100.0); // :472
        Add(typeof(global::Server.Items.QuiverOfElements), 100.0); // :473
        Add(typeof(global::Server.Items.RaedsGlory), 100.0); // :474
        Add(typeof(global::Server.Items.RighteousAnger), 100.0); // :475
        Add(typeof(global::Server.Items.RobeOfTheEclipse), 100.0); // :476
        Add(typeof(global::Server.Items.RobeOfTheEquinox), 100.0); // :477
        Add(typeof(global::Server.Items.SoulSeeker), 100.0); // :478
        Add(typeof(global::Server.Items.TalonBite), 100.0); // :479
        Add(typeof(global::Server.Items.TotemOfVoid), 100.0); // :480
        Add(typeof(global::Server.Items.WildfireBow), 100.0); // :481
        Add(typeof(global::Server.Items.Windsong), 100.0); // :482
        Add(typeof(global::Server.Items.CrimsonCincture), 100.0); // :483
        Add(typeof(global::Server.Items.DreadFlute), 100.0); // :484
        Add(typeof(global::Server.Items.DreadsRevenge), 100.0); // :485
        Add(typeof(global::Server.Items.MelisandesCorrodedHatchet), 100.0); // :486
        // MISSING :487 AlbinoSquirrelImprisonedInCrystal = 100.0
        Add(typeof(global::Server.Items.GrizzledMareStatuette), 100.0); // :488
        Add(typeof(global::Server.Items.GrizzleGauntlets), 100.0); // :489
        Add(typeof(global::Server.Items.GrizzleGreaves), 100.0); // :490
        Add(typeof(global::Server.Items.GrizzleHelm), 100.0); // :491
        Add(typeof(global::Server.Items.GrizzleTunic), 100.0); // :492
        Add(typeof(global::Server.Items.GrizzleVambraces), 100.0); // :493
        // MISSING :494 ParoxysmusSwampDragonStatuette = 100.0
        Add(typeof(global::Server.Items.ScepterOfTheChief), 100.0); // :495
        Add(typeof(global::Server.Items.CrystallineRing), 100.0); // :496
        Add(typeof(global::Server.Items.MarkOfTravesty), 100.0); // :497
        // MISSING :498 ImprisonedDog = 100.0
        Add(typeof(global::Server.Items.AncientFarmersKasa), 100.0); // :499
        Add(typeof(global::Server.Items.AncientSamuraiDo), 100.0); // :500
        Add(typeof(global::Server.Items.AncientUrn), 100.0); // :501
        Add(typeof(global::Server.Items.ArmsOfTacticalExcellence), 100.0); // :502
        Add(typeof(global::Server.Items.BlackLotusHood), 100.0); // :503
        Add(typeof(global::Server.Items.ChestOfHeirlooms), 100.0); // :504
        Add(typeof(global::Server.Items.DaimyosHelm), 100.0); // :505
        Add(typeof(global::Server.Items.DemonForks), 100.0); // :506
        Add(typeof(global::Server.Items.TheDestroyer), 100.0); // :507
        Add(typeof(global::Server.Items.DragonNunchaku), 100.0); // :508
        Add(typeof(global::Server.Items.Exiler), 100.0); // :509
        Add(typeof(global::Server.Items.FluteOfRenewal), 100.0); // :510
        Add(typeof(global::Server.Items.GlovesOfTheSun), 100.0); // :511
        Add(typeof(global::Server.Items.HanzosBow), 100.0); // :512
        Add(typeof(global::Server.Items.HonorableSwords), 100.0); // :513
        Add(typeof(global::Server.Items.LegsOfStability), 100.0); // :514
        Add(typeof(global::Server.Items.LeurociansMempoOfFortune), 100.0); // :515
        Add(typeof(global::Server.Items.PeasantsBokuto), 100.0); // :516
        Add(typeof(global::Server.Items.PilferedDancerFans), 100.0); // :517
        Add(typeof(global::Server.Items.TomeOfEnlightenment), 100.0); // :518

        // Stygian Abyss Artifacts
        // MISSING :521 AbyssalBlade = 5000.0
        Add(typeof(global::Server.Items.AnimatedLegsoftheInsaneTinker), 5000.0); // :522
        Add(typeof(global::Server.Items.AxeOfAbandon), 5000.0); // :523
        Add(typeof(global::Server.Items.AxesOfFury), 5000.0); // :524
        Add(typeof(global::Server.Items.BansheesCall), 5000.0); // :525
        Add(typeof(global::Server.Items.BasiliskHideBreastplate), 5000.0); // :526
        Add(typeof(global::Server.Items.BladeOfBattle), 5000.0); // :527
        Add(typeof(global::Server.Items.BouraTailShield), 5000.0); // :528
        Add(typeof(global::Server.Items.BreastplateOfTheBerserker), 5000.0); // :529
        Add(typeof(global::Server.Items.BurningAmber), 5000.0); // :530
        Add(typeof(global::Server.Items.CastOffZombieSkin), 5000.0); // :531
        Add(typeof(global::Server.Items.CavalrysFolly), 5000.0); // :532
        Add(typeof(global::Server.Items.ChannelersDefender), 5000.0); // :533
        Add(typeof(global::Server.Items.ClawsOfTheBerserker), 5000.0); // :534
        // MISSING :535 DeathsHead = 5000.0
        Add(typeof(global::Server.Items.DefenderOfTheMagus), 5000.0); // :536
        Add(typeof(global::Server.Items.DemonBridleRing), 5000.0); // :537
        Add(typeof(global::Server.Items.DemonHuntersStandard), 5000.0); // :538
        Add(typeof(global::Server.Items.DragonHideShield), 5000.0); // :539
        Add(typeof(global::Server.Items.DragonJadeEarrings), 5000.0); // :540
        Add(typeof(global::Server.Items.DraconisWrath), 5000.0); // :541
        Add(typeof(global::Server.Items.EternalGuardianStaff), 5000.0); // :542
        Add(typeof(global::Server.Items.FallenMysticsSpellbook), 5000.0); // :543
        Add(typeof(global::Server.Items.GiantSteps), 5000.0); // :544
        Add(typeof(global::Server.Items.IronwoodCompositeBow), 5000.0); // :545
        Add(typeof(global::Server.Items.JadeWarAxe), 5000.0); // :546
        Add(typeof(global::Server.Items.LegacyOfDespair), 5000.0); // :547
        Add(typeof(global::Server.Items.Lavaliere), 5000.0); // :548
        Add(typeof(global::Server.Items.LifeSyphon), 5000.0); // :549
        Add(typeof(global::Server.Items.Mangler), 5000.0); // :550
        Add(typeof(global::Server.Items.MantleOfTheFallen), 5000.0); // :551
        Add(typeof(global::Server.Items.MysticsGarb), 5000.0); // :552
        Add(typeof(global::Server.Items.NightEyes), 5000.0); // :553
        Add(typeof(global::Server.Items.ObsidianEarrings), 5000.0); // :554
        Add(typeof(global::Server.Items.PetrifiedSnake), 5000.0); // :555
        Add(typeof(global::Server.Items.PillarOfStrength), 5000.0); // :556
        Add(typeof(global::Server.Items.ProtectoroftheBattleMage), 5000.0); // :557
        Add(typeof(global::Server.Items.RaptorClaw), 5000.0); // :558
        Add(typeof(global::Server.Items.ResonantStaffofEnlightenment), 5000.0); // :559
        Add(typeof(global::Server.Items.ShroudOfTheCondemned), 500.0); // :560
        Add(typeof(global::Server.Items.GargishSignOfOrder), 5000.0); // :561
        Add(typeof(global::Server.Items.HumanSignOfOrder), 5000.0); // :562
        Add(typeof(global::Server.Items.GargishSignOfChaos), 5000.0); // :563
        Add(typeof(global::Server.Items.HumanSignOfChaos), 5000.0); // :564
        Add(typeof(global::Server.Items.Slither), 5000.0); // :565
        Add(typeof(global::Server.Items.SpinedBloodwormBracers), 5000.0); // :566
        Add(typeof(global::Server.Items.StandardOfChaos), 5000.0); // :567
        Add(typeof(global::Server.Items.StandardOfChaosG), 5000.0); // :568
        // MISSING :569 StaffOfShatteredDreams = 5000.0
        Add(typeof(global::Server.Items.StoneDragonsTooth), 5000.0); // :570
        Add(typeof(global::Server.Items.StoneSlithClaw), 5000.0); // :571
        Add(typeof(global::Server.Items.StormCaller), 5000.0); // :572
        Add(typeof(global::Server.Items.SwordOfShatteredHopes), 5000.0); // :573
        Add(typeof(global::Server.Items.SummonersKilt), 5000.0); // :574
        Add(typeof(global::Server.Items.Tangle1), 5000.0); // :575
        Add(typeof(global::Server.Items.TheImpalersPick), 5000.0); // :576
        Add(typeof(global::Server.Items.TorcOfTheGuardians), 5000.0); // :577
        Add(typeof(global::Server.Items.TokenOfHolyFavor), 5000.0); // :578
        Add(typeof(global::Server.Items.VampiricEssence), 5000.0); // :579
        Add(typeof(global::Server.Items.Venom), 5000.0); // :580
        Add(typeof(global::Server.Items.VoidInfusedKilt), 5000.0); // :581
        Add(typeof(global::Server.Items.WallOfHungryMouths), 5000.0); // :582

        // Tokuno Major Artifacts
        Add(typeof(global::Server.Items.DarkenedSky), 2500.0); // :585
        Add(typeof(global::Server.Items.KasaOfTheRajin), 2500.0); // :586
        Add(typeof(global::Server.Items.RuneBeetleCarapace), 2500.0); // :587
        Add(typeof(global::Server.Items.Stormgrip), 2500.0); // :588
        Add(typeof(global::Server.Items.SwordOfTheStampede), 2500.0); // :589
        Add(typeof(global::Server.Items.SwordsOfProsperity), 2500.0); // :590
        Add(typeof(global::Server.Items.TheHorselord), 2500.0); // :591
        Add(typeof(global::Server.Items.TomeOfLostKnowledge), 2500.0); // :592
        Add(typeof(global::Server.Items.WindsEdge), 2500.0); // :593

        // Major Artifacts
        Add(typeof(global::Server.Items.TheDryadBow), 5500.0); // :596
        Add(typeof(global::Server.Items.RingOfTheElements), 5500.0); // :597
        Add(typeof(global::Server.Items.ArcaneShield), 5500.0); // :598
        Add(typeof(global::Server.Items.SerpentsFang), 5500.0); // :599
        Add(typeof(global::Server.Items.OrnamentOfTheMagician), 5500.0); // :600
        Add(typeof(global::Server.Items.BoneCrusher), 5500.0); // :601
        Add(typeof(global::Server.Items.OrnateCrownOfTheHarrower), 5500.0); // :602
        Add(typeof(global::Server.Items.HuntersHeaddress), 5500.0); // :603
        Add(typeof(global::Server.Items.DivineCountenance), 5500.0); // :604
        Add(typeof(global::Server.Items.BraceletOfHealth), 5500.0); // :605
        Add(typeof(global::Server.Items.Aegis), 5500.0); // :606
        Add(typeof(global::Server.Items.AxeOfTheHeavens), 5500.0); // :607
        Add(typeof(global::Server.Items.HelmOfInsight), 5500.0); // :608
        Add(typeof(global::Server.Items.Frostbringer), 5500.0); // :609
        Add(typeof(global::Server.Items.StaffOfTheMagi), 5500.0); // :610
        Add(typeof(global::Server.Items.TheDragonSlayer), 5500.0); // :611
        Add(typeof(global::Server.Items.BreathOfTheDead), 5500.0); // :612
        Add(typeof(global::Server.Items.HolyKnightsBreastplate), 5500.0); // :613
        Add(typeof(global::Server.Items.TunicOfFire), 5500.0); // :614
        Add(typeof(global::Server.Items.ShadowDancerLeggings), 5500.0); // :615
        Add(typeof(global::Server.Items.VoiceOfTheFallenKing), 5500.0); // :616
        Add(typeof(global::Server.Items.TheBeserkersMaul), 5500.0); // :617
        Add(typeof(global::Server.Items.HatOfTheMagi), 5500.0); // :618
        Add(typeof(global::Server.Items.BladeOfInsanity), 5500.0); // :619
        Add(typeof(global::Server.Items.JackalsCollar), 5500.0); // :620

        // Artifacts
        Add(typeof(global::Server.Items.PendantOfTheMagi), 35.0); // :623

        // Replicas
        Add(typeof(global::Server.Items.TatteredAncientMummyWrapping), 5000.0); // :626
        Add(typeof(global::Server.Items.WindSpirit), 5000.0); // :627
        // MISSING :628 GauntletsOfAnger = 5000.0
        Add(typeof(global::Server.Items.GladiatorsCollar), 5000.0); // :629
        Add(typeof(global::Server.Items.OrcChieftainHelm), 5000.0); // :630
        // MISSING :631 ShroudOfDeceit = 5000.0
        Add(typeof(global::Server.Items.AcidProofRobe), 5000.0); // :632
        Add(typeof(global::Server.Items.ANecromancerShroud), 5000.0); // :633
        Add(typeof(global::Server.Items.CaptainJohnsHat), 5000.0); // :634
        Add(typeof(global::Server.Items.CrownOfTalKeesh), 5000.0); // :635
        Add(typeof(global::Server.Items.DetectiveBoots), 5000.0); // :637
        Add(typeof(global::Server.Items.EmbroideredOakLeafCloak), 5000.0); // :638
        Add(typeof(global::Server.Items.JadeArmband), 5000.0); // :639
        Add(typeof(global::Server.Items.LieutenantOfTheBritannianRoyalGuard), 5000.0); // :640
        Add(typeof(global::Server.Items.MagicalDoor), 5000.0); // :641
        Add(typeof(global::Server.Items.RoyalGuardInvestigatorsCloak), 5000.0); // :642
        Add(typeof(global::Server.Items.SamaritanRobe), 5000.0); // :643
        Add(typeof(global::Server.Items.TheMostKnowledgePerson), 5000.0); // :644
        Add(typeof(global::Server.Items.TheRobeOfBritanniaAri), 5000.0); // :645
        Add(typeof(global::Server.Items.DjinnisRing), 5000.0); // :646
        Add(typeof(global::Server.Items.BraveKnightOfTheBritannia), 5000.0); // :648
        Add(typeof(global::Server.Items.Calm), 5000.0); // :649
        Add(typeof(global::Server.Items.FangOfRactus), 5000.0); // :650
        Add(typeof(global::Server.Items.OblivionsNeedle), 5000.0); // :651
        Add(typeof(global::Server.Items.Pacify), 5000.0); // :652
        Add(typeof(global::Server.Items.Quell), 5000.0); // :653
        Add(typeof(global::Server.Items.RoyalGuardSurvivalKnife), 5000.0); // :654
        Add(typeof(global::Server.Items.Subdue), 5000.0); // :655
        // MISSING :656 Asclepius = 5000.0
        // MISSING :657 BracersofAlchemicalDevastation = 5000.0
        // MISSING :659 GargishAsclepius = 5000.0
        // MISSING :660 GargishBracersofAlchemicalDevastation = 5000.0
        // MISSING :661 HygieiasAmulet = 5000.0
        // MISSING :662 ScrollofValiantCommendation = 5000.0

        // Easter
        Add(typeof(global::Server.Items.EasterEggs), 2.0); // :665
        Add(typeof(global::Server.Items.JellyBeans), 1.0); // :666

        // Miscellaneous
        // MISSING :669 ParrotItem = 25.0
        Add(typeof(global::Server.Items.Gold), 0.01); // :670
        Add(typeof(global::Server.Items.RedScales), 0.10); // :671
        Add(typeof(global::Server.Items.YellowScales), 0.10); // :672
        Add(typeof(global::Server.Items.BlackScales), 0.10); // :673
        Add(typeof(global::Server.Items.GreenScales), 0.10); // :674
        Add(typeof(global::Server.Items.WhiteScales), 0.10); // :675
        Add(typeof(global::Server.Items.BlueScales), 0.10); // :676
        Add(typeof(global::Server.Items.Bottle), 0.25); // :677
        Add(typeof(global::Server.Items.OrcishKinMask), 100.0); // :678
        Add(typeof(global::Server.Items.PottedPlantDeed), 15000.0); // :679
        Add(typeof(global::Server.Items.BagOfSending), 250.0); // :680
        Add(typeof(global::Server.Items.Cauldron), 200.0); // :681
        Add(typeof(global::Server.Items.ChampionSkull), 1000.0); // :682
        Add(typeof(global::Server.Items.ClockworkAssembly), 50.0); // :684
        // MISSING :685 ConjurersTrinket = 10000.0
        // MISSING :687 CorgulsHandbookOnMysticism = 250.0
        Add(typeof(global::Server.Items.CrownOfArcaneTemperament), 5000.0); // :688
        Add(typeof(global::Server.Items.DeadWood), 1.0); // :689
        // MISSING :690 DustyPillow = 250.0
        Add(typeof(global::Server.Items.EndlessDecanter), 10.0); // :691
        Add(typeof(global::Server.Items.EternallyCorruptTree), 1000.0); // :692
        Add(typeof(global::Server.Items.ExcellentIronMaiden), 50.0); // :693
        Add(typeof(global::Server.Items.ExecutionersCap), 1.0); // :694
        // MISSING :695 Flowstone = 250.0
        Add(typeof(global::Server.Items.GlacialStaff), 500.0); // :696
        Add(typeof(global::Server.Items.GrapeVine), 500.0); // :697
        Add(typeof(global::Server.Items.GrobusFur), 20.0); // :698
        Add(typeof(global::Server.Items.HorseShoes), 200.0); // :699
        Add(typeof(global::Server.Items.JocklesQuicksword), 2.0); // :701
        Add(typeof(global::Server.Items.MangledHeadOfDreadhorn), 1000.0); // :702
        Add(typeof(global::Server.Items.MedusaBlood), 1000.0); // :703
        Add(typeof(global::Server.Items.MedusaDarkScales), 200.0); // :704
        Add(typeof(global::Server.Items.MedusaLightScales), 200.0); // :705
        Add(typeof(global::Server.Items.ContestMiniHouseDeed), 6500.0); // :706
        Add(typeof(global::Server.Items.Moonstone), 5000.0); // :707
        Add(typeof(global::Server.Items.MysticsGuard), 2500.0); // :708
        Add(typeof(global::Server.Items.PowerCrystal), 100.0); // :709
        Add(typeof(global::Server.Items.PristineDreadHorn), 1000.0); // :710
        Add(typeof(global::Server.Items.ProspectorsTool), 3.0); // :711
        Add(typeof(global::Server.Items.RecipeScroll), 10.0); // :712
        Add(typeof(global::Server.Items.SwampTile), 5000.0); // :714
        // MISSING :715 TastyTreat = 100.0
        Add(typeof(global::Server.Items.TatteredAncientScroll), 200.0); // :716
        Add(typeof(global::Server.Items.ThorvaldsMedallion), 250.0); // :717
        Add(typeof(global::Server.Items.TribalBerry), 10.0); // :718
        Add(typeof(global::Server.Items.TunicOfGuarding), 2.0); // :719
        // MISSING :720 UndeadGargHorn = 1000.0
        Add(typeof(global::Server.Items.UntranslatedAncientTome), 200.0); // :721
        Add(typeof(global::Server.Items.WallBlood), 5000.0); // :722
        Add(typeof(global::Server.Items.Whip), 200.0); // :723
        // MISSING :724 BalmOfSwiftness = 100.0
        Add(typeof(global::Server.Items.TaintedMushroom), 1000.0); // :725
        Add(typeof(global::Server.Engines.Quests.Doom.GoldenSkull), 1000.0); // :726
        Add(typeof(global::Server.Items.RedSoulstone), 15000.0); // :727
        Add(typeof(global::Server.Items.BlueSoulstone), 15000.0); // :728
        Add(typeof(global::Server.Items.SoulStone), 15000.0); // :729
        // MISSING :730 HornOfPlenty = 2500.0
        Add(typeof(global::Server.Items.KepetchWax), 500.0); // :731
        Add(typeof(global::Server.Items.SlithEye), 500.0); // :732
        Add(typeof(global::Server.Items.SoulstoneFragment), 500.0); // :733
        Add(typeof(global::Server.Items.WhiteClothDyeTub), 300.0); // :734
        // MISSING :735 Lodestone = 75.0
        // MISSING :736 FeyWings = 75.0
        Add(typeof(global::Server.Items.StoutWhip), 3.0); // :737
        // MISSING :738 PlantClippings = 1.0
        // MISSING :739 BasketOfRolls = 5.0
        Add(typeof(global::Server.Items.Yeast), 10.0); // :740
        Add(typeof(global::Server.Items.ValentinesCard), 50.0); // :741
        Add(typeof(global::Server.Items.MetallicClothDyetub), 100.0); // :742 (ServUO MetallicClothDyeTub; pinned spells it Dyetub)

        // Treasure Hunting
        Add(typeof(global::Server.Items.Lockpick), 0.10); // :745

        // Shattered Legacy (ours, registered): the extended ingots and boards, valued by ExtendedValue.
        Add(typeof(PlatinumIngot), ExtendedValue(CraftResource.Platinum));
        Add(typeof(ToxicIngot), ExtendedValue(CraftResource.Toxic));
        Add(typeof(BlazeIngot), ExtendedValue(CraftResource.Blaze));
        Add(typeof(FrostIngot), ExtendedValue(CraftResource.Frost));
        Add(typeof(ObsidianIngot), ExtendedValue(CraftResource.Obsidian));
        Add(typeof(MythrilIngot), ExtendedValue(CraftResource.Mythril));
        Add(typeof(AdamantiumIngot), ExtendedValue(CraftResource.Adamantium));
        Add(typeof(CelestialIngot), ExtendedValue(CraftResource.Celestial));
        Add(typeof(IronwoodBoard), ExtendedValue(CraftResource.Ironwood));
        Add(typeof(GhostwoodBoard), ExtendedValue(CraftResource.Ghostwood));
        Add(typeof(EmberbarkBoard), ExtendedValue(CraftResource.Emberbark));
        Add(typeof(FrostbarkBoard), ExtendedValue(CraftResource.Frostbark));
        Add(typeof(ShadowbarkBoard), ExtendedValue(CraftResource.Shadowbark));
        Add(typeof(RunewoodBoard), ExtendedValue(CraftResource.Runewood));
        Add(typeof(VoidwoodBoard), ExtendedValue(CraftResource.Voidwood));
        Add(typeof(StarwoodBoard), ExtendedValue(CraftResource.Starwood));

        var built = _building;
        _building = null;
        return built;
    }
}

// ServUO :936-978. Targets items in the player's own pack, again and again until cancelled.
public class AppraiseforCleanupTarget : Target
{
    private readonly Mobile _mobile;

    public AppraiseforCleanupTarget(Mobile from) : base(-1, true, TargetFlags.None) => _mobile = from;

    // The message the appraisal sends for an item: (cliloc, argument). Shared with the facts.
    public static (int Number, string Args) Appraise(Item item)
    {
        var points = CleanUpBritanniaData.GetPoints(item);

        if (points == 0)
        {
            return (1151271, ""); // This item has no turn-in value for Clean Up Britannia.
        }

        if (points < 1)
        {
            return (1151272, ""); // This item is worth less than one point for Clean Up Britannia.
        }

        if (points == 1)
        {
            return (1151273, ""); // This item is worth approximately one point for Clean Up Britannia.
        }

        return (1151274, points.ToString()); // This item is worth approximately ~1_VALUE~ points for Clean Up Britannia.
    }

    protected override void OnTarget(Mobile m, object targeted)
    {
        if (targeted is Item item)
        {
            if (!item.IsChildOf(_mobile))
            {
                return;
            }

            var (number, args) = Appraise(item);
            _mobile.SendLocalizedMessage(number, args);
        }
        else
        {
            _mobile.SendLocalizedMessage(1151271); // This item has no turn-in value for Clean Up Britannia.
        }

        _mobile.Target = new AppraiseforCleanupTarget(_mobile);
    }
}
