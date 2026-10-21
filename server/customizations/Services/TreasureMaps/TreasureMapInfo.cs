// cc-P127: ServUO pub57 Scripts/Services/TreasureMaps/TreasureMapInfo.cs -> ours: the Forgotten Treasures revamp (EJ).
// Five levels (Stash to Trove), five packages (Artisan to Warrior), seven facets, three chest qualities, and Fill, the
// loot of a dug chest. The map and chest themselves are server/patches/TreasureMap.cs and TreasureMapChest.cs (routed
// replacements of pinned's two files).
//
// Where this differs from ServUO, each argued in shard-migration notes/cc-P127-port-treasure-maps-monster-stealing.md:
//   - Types pinned and ours lack are left out of the tables (23 names; the list is in the notes), as P123 did for its
//     13 list rows. Pinned's gargish leather and stone armour comes as Type1 and Type2 pairs, so both stand in for
//     ServUO's single name.
//   - No RunicReforging and no RefinementComponent in pinned (D-38 precedent, CC4): equipment gets pinned's
//     BaseRunicTool.ApplyAttributesTo with the chest's own AOS stat rolls; the budget table GetMinMaxBudget went with it.
//   - Loot.MysticRegTypes does not exist in pinned: a Ter Mur mage's stash gets the standard reagents.
//   - ServUO's list.Concat(...) results are discarded (LINQ returns a new sequence), so its functional minor artifacts and
//     the Malas coffin piece never drop from those two paths; the no-ops are not reproduced and the lists are as ServUO
//     effectively runs them.
//   - MutateType turns a skull weapon gargish only for a Ter Mur chest; ServUO turned it gargish everywhere, which left a
//     human with a weapon no human can hold (fixed, D-4 rule).

using System;
using System.Collections.Generic;
using Server.Engines.Craft;
using Server.Engines.PartySystem;
using Server.Mobiles;

namespace Server.Items;

public enum TreasureLevel
{
    Stash,
    Supply,
    Cache,
    Hoard,
    Trove
}

public enum TreasurePackage
{
    Artisan,
    Assassin,
    Mage,
    Ranger,
    Warrior
}

public enum TreasureFacet
{
    Trammel,
    Felucca,
    Ilshenar,
    Malas,
    Tokuno,
    TerMur,
    Eodon
}

public enum ChestQuality
{
    None,
    Rusty,
    Standard,
    Gold
}

public static class TreasureMapInfo
{
    public static bool NewSystem => Core.EJ;

    /// <summary>
    /// ServUO calls this from BaseCreature so no creature file changes: the classic level (1 to 7) a creature declares
    /// becomes a revamp level. Our TreasureMap constructor applies it to every integer level it is given.
    /// </summary>
    public static int ConvertLevel(int level)
    {
        if (!NewSystem || level == -1)
        {
            return level;
        }

        return level switch
        {
            2 or 3 => (int)TreasureLevel.Supply,
            4 or 5 => (int)TreasureLevel.Cache,
            6      => (int)TreasureLevel.Hoard,
            7      => (int)TreasureLevel.Trove,
            _      => (int)TreasureLevel.Stash
        };
    }

    public static TreasureFacet GetFacet(IEntity e) => GetFacet(e.Location, e.Map);

    public static int PackageLocalization(TreasurePackage package) =>
        package switch
        {
            TreasurePackage.Artisan  => 1158989,
            TreasurePackage.Assassin => 1158987,
            TreasurePackage.Mage     => 1158986,
            TreasurePackage.Ranger   => 1158990,
            TreasurePackage.Warrior  => 1158988,
            _                        => 0
        };

    /// <summary>ServUO SpellHelper.IsEodon: the Eodon part of Ter Mur, and the Felucca copy of its arena.</summary>
    public static bool IsEodon(Map map, Point3D loc)
    {
        if (map == Map.Felucca && loc.X >= 6975 && loc.X <= 7042 && loc.Y >= 2048 && loc.Y <= 2115)
        {
            return true;
        }

        return map == Map.TerMur && loc.X >= 0 && loc.X <= 1087 && loc.Y >= 1344 && loc.Y <= 2495;
    }

    public static TreasureFacet GetFacet(IPoint2D p, Map map)
    {
        if (map == Map.TerMur)
        {
            return IsEodon(map, new Point3D(p.X, p.Y, 0)) ? TreasureFacet.Eodon : TreasureFacet.TerMur;
        }

        if (map == Map.Felucca)
        {
            return TreasureFacet.Felucca;
        }

        if (map == Map.Malas)
        {
            return TreasureFacet.Malas;
        }

        if (map == Map.Ilshenar)
        {
            return TreasureFacet.Ilshenar;
        }

        if (map == Map.Tokuno)
        {
            return TreasureFacet.Tokuno;
        }

        return TreasureFacet.Trammel;
    }

    public static IEnumerable<Type> GetRandomEquipment(TreasureLevel level, TreasurePackage package, TreasureFacet facet, int amount)
    {
        var weapons = GetWeaponList(level, package, facet);
        var armor = GetArmorList(level, package, facet);
        var jewels = GetJewelList(level, package, facet);

        for (var i = 0; i < amount; i++)
        {
            var list = Utility.Random(5) switch
            {
                1 or 2 => armor,
                3 or 4 => jewels,
                _      => weapons
            };

            yield return list[Utility.Random(list.Length)];
        }
    }

    private static Type[] FacetList(Type[][] table, TreasureFacet facet)
    {
        var list = facet switch
        {
            TreasureFacet.Ilshenar => table[1],
            TreasureFacet.Malas    => table[2],
            TreasureFacet.Tokuno   => table[3],
            TreasureFacet.TerMur   => table[4],
            TreasureFacet.Eodon    => table[5],
            _                      => table[0]
        };

        // tram/fel lists are always default
        if (list == null || list.Length == 0)
        {
            list = table[0];
        }

        return list;
    }

    public static Type[] GetWeaponList(TreasureLevel level, TreasurePackage package, TreasureFacet facet) =>
        FacetList(_weaponTable[(int)package], facet);

    public static Type[] GetArmorList(TreasureLevel level, TreasurePackage package, TreasureFacet facet) =>
        FacetList(_armorTable[(int)package], facet);

    public static Type[] GetJewelList(TreasureLevel level, TreasurePackage package, TreasureFacet facet) =>
        facet == TreasureFacet.TerMur ? _jewelTable[1] : _jewelTable[0];

    public static SkillName[] GetTranscendenceList(TreasureLevel level, TreasurePackage package)
    {
        if (level is TreasureLevel.Supply or TreasureLevel.Cache)
        {
            return null;
        }

        return _transcendenceTable[(int)package];
    }

    public static SkillName[] GetAlacrityList(TreasureLevel level, TreasurePackage package, TreasureFacet facet)
    {
        if (level == TreasureLevel.Stash || facet == TreasureFacet.Felucca && level == TreasureLevel.Cache)
        {
            return null;
        }

        return _alacrityTable[(int)package];
    }

    public static SkillName[] GetPowerScrollList(TreasureLevel level, TreasurePackage package, TreasureFacet facet)
    {
        if (facet != TreasureFacet.Felucca)
        {
            return null;
        }

        if (level >= TreasureLevel.Cache)
        {
            return _powerscrollTable[(int)package];
        }

        return null;
    }

    public static Type[] GetCraftingMaterials(TreasureLevel level, TreasurePackage package, ChestQuality quality)
    {
        if (package == TreasurePackage.Artisan && level <= TreasureLevel.Supply && quality != ChestQuality.None)
        {
            return _materialTable[(int)quality - 1];
        }

        return null;
    }

    public static Type[] GetSpecialMaterials(TreasureLevel level, TreasurePackage package, TreasureFacet facet)
    {
        if (package == TreasurePackage.Artisan && level == TreasureLevel.Supply)
        {
            return _specialMaterialTable[(int)facet];
        }

        return null;
    }

    public static Type[] GetDecorativeList(TreasureLevel level, TreasurePackage package, TreasureFacet facet)
    {
        if (level >= TreasureLevel.Cache)
        {
            return _decorativeTable[(int)package];
        }

        if (level == TreasureLevel.Supply)
        {
            return _decorativeMinorArtifacts;
        }

        return null;
    }

    public static Type[] GetReagentList(TreasureLevel level, TreasurePackage package, TreasureFacet facet)
    {
        if (level != TreasureLevel.Stash || package != TreasurePackage.Mage)
        {
            return null;
        }

        return facet switch
        {
            TreasureFacet.Felucca or TreasureFacet.Trammel => Loot.RegTypes,
            TreasureFacet.Malas                            => Loot.NecroRegTypes,
            TreasureFacet.TerMur                           => Loot.RegTypes, // ServUO: Loot.MysticRegTypes, absent in pinned
            _                                              => null
        };
    }

    public static Recipe[] GetRecipeList(TreasureLevel level, TreasurePackage package)
    {
        if (package == TreasurePackage.Artisan && level == TreasureLevel.Supply)
        {
            var list = new Recipe[Recipe.Recipes.Count];
            Recipe.Recipes.Values.CopyTo(list, 0);
            return list;
        }

        return null;
    }

    public static Type[] GetSpecialLootList(TreasureLevel level, TreasurePackage package)
    {
        if (level == TreasureLevel.Stash)
        {
            return null;
        }

        return level == TreasureLevel.Supply ? _specialSupplyLoot[(int)package] : _specialCacheHordeAndTrove;
    }

    public static int GetGemCount(ChestQuality quality, TreasureLevel level)
    {
        var baseAmount = quality switch
        {
            ChestQuality.Rusty    => 7,
            ChestQuality.Standard => Utility.RandomBool() ? 7 : 9,
            ChestQuality.Gold     => Utility.RandomList(7, 9, 11),
            _                     => 0
        };

        return baseAmount + (int)level * 5;
    }

    public static int GetGoldCount(TreasureLevel level) =>
        level switch
        {
            TreasureLevel.Supply => Utility.RandomMinMax(20000, 50000),
            TreasureLevel.Cache  => Utility.RandomMinMax(30000, 60000),
            TreasureLevel.Hoard  => Utility.RandomMinMax(40000, 70000),
            TreasureLevel.Trove  => Utility.RandomMinMax(50000, 70000),
            _                    => Utility.RandomMinMax(10000, 40000)
        };

    public static int GetGoldMin(TreasureLevel level) => 10000 + (int)level * 10000;
    public static int GetGoldMax(TreasureLevel level) => level == TreasureLevel.Trove ? 70000 : 40000 + (int)level * 10000;

    public static int GetRefinementRolls(ChestQuality quality) =>
        quality switch
        {
            ChestQuality.Standard => 4,
            ChestQuality.Gold     => 6,
            _                     => 2
        };

    public static int GetResourceAmount(TreasureLevel level) =>
        level switch
        {
            TreasureLevel.Stash  => 50,
            TreasureLevel.Supply => 100,
            _                    => 0
        };

    public static int GetRegAmount(ChestQuality quality) =>
        quality switch
        {
            ChestQuality.Standard => 40,
            ChestQuality.Gold     => 60,
            _                     => 20
        };

    public static int GetSpecialResourceAmount(ChestQuality quality) =>
        quality switch
        {
            ChestQuality.Standard => 2,
            ChestQuality.Gold     => 3,
            _                     => 1
        };

    public static int GetEquipmentAmount(Mobile from, TreasureLevel level, TreasurePackage package)
    {
        var amount = level switch
        {
            TreasureLevel.Supply => 8,
            TreasureLevel.Cache  => package == TreasurePackage.Assassin ? 24 : 12,
            TreasureLevel.Hoard  => 18,
            TreasureLevel.Trove  => 36,
            _                    => 6
        };

        var p = from != null ? Party.Get(from) : null;

        if (p?.Count > 1)
        {
            for (var i = 0; i < p.Count - 1; i++)
            {
                if (Utility.RandomBool())
                {
                    amount++;
                }
            }
        }

        return amount;
    }

    /// <summary>The chest's lock and trap by level: ServUO's table (required skill, trap power, trap level).</summary>
    public static (int RequiredSkill, int TrapPower, int TrapLevel) GetLockAndTrap(TreasureLevel level) =>
        level switch
        {
            TreasureLevel.Supply => (45, 75, 3),
            TreasureLevel.Cache  => (75, 125, 5),
            TreasureLevel.Hoard  => (80, 150, 6),
            TreasureLevel.Trove  => (80, 170, 7),
            _                    => (5, 25, 1)
        };

    private static readonly Type[][][] _weaponTable =
    {
        new[] // Artisan
        {
            new[] { typeof(HammerPick), typeof(WarAxe), typeof(WarHammer), typeof(Axe), typeof(BattleAxe), typeof(DoubleAxe), typeof(ExecutionersAxe), typeof(Hatchet), typeof(LargeBattleAxe), typeof(OrnateAxe), typeof(TwoHandedAxe), typeof(Pickaxe) }, // Trammel, Felucca
            null, // Ilshenar
            null, // Malas
            null, // Tokuno
            new[] { typeof(HammerPick), typeof(WarAxe), typeof(WarHammer), typeof(Axe), typeof(BattleAxe), typeof(DoubleAxe), typeof(ExecutionersAxe), typeof(Hatchet), typeof(LargeBattleAxe), typeof(OrnateAxe), typeof(TwoHandedAxe), typeof(Pickaxe), typeof(DualShortAxes) }, // TerMur
            Array.Empty<Type>() // Eodon
        },
        new[] // Assassin
        {
            new[] { typeof(Dagger), typeof(Kryss), typeof(Cleaver), typeof(Cutlass), typeof(ElvenMachete) },
            null,
            null,
            null,
            new[] { typeof(Dagger), typeof(Kryss), typeof(Cleaver), typeof(Cutlass) },
            new[] { typeof(Dagger), typeof(Kryss), typeof(Cleaver), typeof(Cutlass) } // ServUO adds the three Eodon whips
        },
        new[] // Mage
        {
            new[] { typeof(BlackStaff), typeof(ShepherdsCrook), typeof(GnarledStaff), typeof(QuarterStaff) },
            null,
            null,
            null,
            null,
            null
        },
        new[] // Ranger
        {
            new[] { typeof(Bow), typeof(Crossbow), typeof(HeavyCrossbow), typeof(CompositeBow), typeof(ButcherKnife), typeof(SkinningKnife) },
            new[] { typeof(Bow), typeof(Crossbow), typeof(HeavyCrossbow), typeof(CompositeBow), typeof(ButcherKnife), typeof(SkinningKnife), typeof(SoulGlaive) },
            new[] { typeof(Bow), typeof(Crossbow), typeof(HeavyCrossbow), typeof(CompositeBow), typeof(ButcherKnife), typeof(SkinningKnife), typeof(ElvenCompositeLongbow) },
            null,
            new[] { typeof(Bow), typeof(Crossbow), typeof(HeavyCrossbow), typeof(CompositeBow), typeof(ButcherKnife), typeof(SkinningKnife), typeof(GargishButcherKnife), typeof(Cyclone), typeof(SoulGlaive) },
            null
        },
        new[] // Warrior
        {
            new[] { typeof(Lance), typeof(Pike), typeof(Pitchfork), typeof(ShortSpear), typeof(WarFork), typeof(Club), typeof(Mace), typeof(Maul), typeof(WarAxe), typeof(Bardiche), typeof(Broadsword), typeof(CrescentBlade), typeof(Halberd), typeof(Longsword), typeof(Scimitar), typeof(VikingSword) },
            null,
            null,
            new[] { typeof(Lance), typeof(Pike), typeof(Pitchfork), typeof(ShortSpear), typeof(WarFork), typeof(Club), typeof(Mace), typeof(Maul), typeof(WarAxe), typeof(Bardiche), typeof(Broadsword), typeof(CrescentBlade), typeof(Halberd), typeof(Longsword), typeof(Scimitar), typeof(VikingSword), typeof(Bokuto), typeof(Daisho) },
            null,
            null
        }
    };

    private static readonly Type[][][] _armorTable =
    {
        new[] // Artisan
        {
            new[] { typeof(Bonnet), typeof(Cap), typeof(Circlet), typeof(ElvenGlasses), typeof(FeatheredHat), typeof(FlowerGarland), typeof(JesterHat), typeof(SkullCap), typeof(StrawHat), typeof(TallStrawHat), typeof(WideBrimHat) }, // Trammel/Fel
            null, // Ilshenar
            null, // Malas
            null, // Tokuno
            null, // TerMur
            new[] { typeof(Bonnet), typeof(Cap), typeof(Circlet), typeof(ElvenGlasses), typeof(FeatheredHat), typeof(FlowerGarland), typeof(JesterHat), typeof(SkullCap), typeof(StrawHat), typeof(TallStrawHat), typeof(WideBrimHat) } // Eodon (ServUO adds ChefsToque)
        },
        new[] // Assassin
        {
            new[] { typeof(ChainLegs), typeof(ChainCoif), typeof(ChainChest), typeof(RingmailLegs), typeof(RingmailGloves), typeof(RingmailChest), typeof(RingmailArms), typeof(Bandana) }, // Trammel/Fel
            null, // Ilshenar
            null, // Malas
            new[] { typeof(ChainLegs), typeof(ChainCoif), typeof(ChainChest), typeof(RingmailLegs), typeof(RingmailGloves), typeof(RingmailArms), typeof(RingmailArms), typeof(Bandana), typeof(LeatherSuneate), typeof(LeatherMempo), typeof(LeatherJingasa), typeof(LeatherHiroSode), typeof(LeatherHaidate), typeof(LeatherDo) }, // Tokuno
            null, // TerMur
            null // Eodon
        },
        new[] // Mage
        {
            new[] { typeof(LeafGloves), typeof(LeafLegs), typeof(LeafTonlet), typeof(LeafGorget), typeof(LeafArms), typeof(LeafChest), typeof(LeatherArms), typeof(LeatherChest), typeof(LeatherLegs), typeof(LeatherGloves), typeof(LeatherGorget), typeof(WizardsHat) }, // Trammel/Fel
            null, // Ilshenar
            new[] { typeof(LeafGloves), typeof(LeafLegs), typeof(LeafTonlet), typeof(LeafGorget), typeof(LeafArms), typeof(LeafChest), typeof(LeatherArms), typeof(LeatherChest), typeof(LeatherLegs), typeof(LeatherGloves), typeof(LeatherGorget), typeof(WizardsHat), typeof(BoneLegs), typeof(BoneHelm), typeof(BoneGloves), typeof(BoneChest), typeof(BoneArms) }, // Malas
            null, // Tokuno
            new[] { typeof(LeatherArms), typeof(LeatherChest), typeof(LeatherLegs), typeof(LeatherGloves), typeof(LeatherGorget), typeof(WizardsHat) }, // TerMur
            new[] { typeof(LeatherArms), typeof(LeatherChest), typeof(LeatherLegs), typeof(LeatherGloves), typeof(LeatherGorget), typeof(WizardsHat) } // Eodon
        },
        new[] // Ranger
        {
            new[] { typeof(HidePants), typeof(HidePauldrons), typeof(HideGorget), typeof(HideFemaleChest), typeof(HideChest), typeof(HideGloves), typeof(StuddedLegs), typeof(StuddedGorget), typeof(StuddedGloves), typeof(StuddedChest), typeof(StuddedBustierArms), typeof(StuddedArms), typeof(RavenHelm), typeof(VultureHelm), typeof(WingedHelm) }, // Trammel/Fel
            null, // Ilshenar
            null, // Malas
            new[] { typeof(StuddedLegs), typeof(StuddedGorget), typeof(StuddedGloves), typeof(StuddedChest), typeof(StuddedBustierArms), typeof(StuddedArms) }, // Tokuno
            new[] { typeof(HidePants), typeof(HidePauldrons), typeof(HideGorget), typeof(HideFemaleChest), typeof(HideChest), typeof(HideGloves), typeof(StuddedLegs), typeof(StuddedGorget), typeof(StuddedGloves), typeof(StuddedChest), typeof(StuddedBustierArms), typeof(StuddedArms), typeof(GargishLeatherKiltType1), typeof(GargishLeatherKiltType2), typeof(GargishLeatherLegsType1), typeof(GargishLeatherLegsType2), typeof(GargishLeatherArmsType1), typeof(GargishLeatherArmsType2), typeof(GargishLeatherChestType1), typeof(GargishLeatherChestType2) }, // TerMur
            new[] { typeof(StuddedLegs), typeof(StuddedGorget), typeof(StuddedGloves), typeof(StuddedChest), typeof(StuddedBustierArms), typeof(StuddedArms), typeof(TigerPeltSkirt), typeof(TigerPeltShorts), typeof(TigerPeltLegs), typeof(TigerPeltLongSkirt), typeof(TigerPeltHelm), typeof(TigerPeltChest), typeof(TigerPeltCollar), typeof(TigerPeltBustier), typeof(VultureHelm), typeof(TribalMask) } // Eodon
        },
        new[] // Warrior
        {
            new[] { typeof(PlateLegs), typeof(PlateHelm), typeof(PlateGorget), typeof(PlateGloves), typeof(PlateChest), typeof(PlateArms), typeof(Bascinet), typeof(CloseHelm), typeof(Helmet), typeof(LeatherCap), typeof(NorseHelm), typeof(TricorneHat), typeof(BronzeShield), typeof(Buckler), typeof(ChaosShield), typeof(HeaterShield), typeof(MetalKiteShield), typeof(MetalShield), typeof(OrderShield), typeof(WoodenKiteShield) }, // Trammel/Fel
            null, // Ilshenar
            new[] { typeof(PlateLegs), typeof(PlateHelm), typeof(PlateGorget), typeof(PlateGloves), typeof(PlateChest), typeof(PlateArms), typeof(Bascinet), typeof(CloseHelm), typeof(Helmet), typeof(LeatherCap), typeof(NorseHelm), typeof(TricorneHat), typeof(BronzeShield), typeof(Buckler), typeof(ChaosShield), typeof(HeaterShield), typeof(MetalKiteShield), typeof(MetalShield), typeof(OrderShield), typeof(WoodenKiteShield), typeof(DragonHelm), typeof(DragonGloves), typeof(DragonChest), typeof(DragonArms), typeof(DragonLegs) }, // Malas
            new[] { typeof(PlateLegs), typeof(PlateHelm), typeof(PlateGorget), typeof(PlateGloves), typeof(PlateChest), typeof(PlateArms), typeof(Bascinet), typeof(CloseHelm), typeof(Helmet), typeof(LeatherCap), typeof(NorseHelm), typeof(TricorneHat), typeof(BronzeShield), typeof(Buckler), typeof(ChaosShield), typeof(HeaterShield), typeof(MetalKiteShield), typeof(MetalShield), typeof(OrderShield), typeof(WoodenKiteShield), typeof(PlateSuneate), typeof(PlateMempo), typeof(PlateHiroSode), typeof(PlateHatsuburi), typeof(PlateHaidate), typeof(PlateDo), typeof(PlateBattleKabuto), typeof(DecorativePlateKabuto), typeof(LightPlateJingasa), typeof(SmallPlateJingasa) }, // Tokuno
            new[] { typeof(PlateLegs), typeof(PlateHelm), typeof(PlateGorget), typeof(PlateGloves), typeof(PlateChest), typeof(PlateArms), typeof(Bascinet), typeof(CloseHelm), typeof(Helmet), typeof(LeatherCap), typeof(NorseHelm), typeof(TricorneHat), typeof(BronzeShield), typeof(Buckler), typeof(ChaosShield), typeof(HeaterShield), typeof(MetalKiteShield), typeof(MetalShield), typeof(OrderShield), typeof(WoodenKiteShield), typeof(GargishPlateArms), typeof(GargishPlateChest), typeof(GargishPlateKilt), typeof(GargishPlateLegs), typeof(GargishStoneKiltType1), typeof(GargishStoneKiltType2), typeof(GargishStoneLegsType1), typeof(GargishStoneLegsType2), typeof(GargishStoneArmsType1), typeof(GargishStoneArmsType2), typeof(GargishStoneChestType1), typeof(GargishStoneChestType2) }, // TerMur
            new[] { typeof(PlateLegs), typeof(PlateHelm), typeof(PlateGorget), typeof(PlateGloves), typeof(PlateChest), typeof(PlateArms), typeof(Bascinet), typeof(CloseHelm), typeof(Helmet), typeof(LeatherCap), typeof(NorseHelm), typeof(TricorneHat), typeof(BronzeShield), typeof(Buckler), typeof(ChaosShield), typeof(HeaterShield), typeof(MetalKiteShield), typeof(MetalShield), typeof(OrderShield), typeof(WoodenKiteShield), typeof(DragonTurtleHideHelm), typeof(DragonTurtleHideLegs), typeof(DragonTurtleHideChest), typeof(DragonTurtleHideBustier), typeof(DragonTurtleHideArms) } // Eodon
        }
    };

    private static readonly Type[][] _materialTable =
    {
        new[] { typeof(SpinedLeather), typeof(OakBoard), typeof(AshBoard), typeof(DullCopperIngot), typeof(ShadowIronIngot), typeof(CopperIngot) },
        new[] { typeof(HornedLeather), typeof(YewBoard), typeof(HeartwoodBoard), typeof(BronzeIngot), typeof(GoldIngot), typeof(AgapiteIngot) },
        new[] { typeof(BarbedLeather), typeof(BloodwoodBoard), typeof(FrostwoodBoard), typeof(ValoriteIngot), typeof(VeriteIngot) }
    };

    private static readonly Type[][] _jewelTable =
    {
        new[] { typeof(GoldRing), typeof(GoldBracelet), typeof(SilverRing), typeof(SilverBracelet) }, // standard
        new[] { typeof(GoldRing), typeof(GoldBracelet), typeof(SilverRing), typeof(SilverBracelet), typeof(GargishBracelet) } // TerMur
    };

    // ServUO: SkullTiledFloorAddonDeed; AncientWeapon3; DecorativeHourglass; AncientWeapon1, CreepingVine; AncientWeapon2.
    // Only the vine exists here.
    private static readonly Type[][] _decorativeTable =
    {
        Array.Empty<Type>(),
        Array.Empty<Type>(),
        Array.Empty<Type>(),
        new[] { typeof(CreepingVine) },
        Array.Empty<Type>()
    };

    private static readonly Type[][] _specialMaterialTable =
    {
        null, // tram
        null, // fel
        null, // ilsh
        new[] { typeof(LuminescentFungi), typeof(BarkFragment), typeof(Blight), typeof(Corruption), typeof(Muculent), typeof(Putrefaction), typeof(Scourge), typeof(Taint) }, // malas
        null, // tokuno
        TreasureMapChest.ImbuingIngreds, // ter
        null // eodon
    };

    // ServUO's lists (TreasureMapInfo.cs:586-593) name LegendaryMapmakersGlasses, ManaPhasingOrb, RunedSashOfWarding,
    // ShieldEngravingTool (Artisan, plus a null meaning "a random recipe"), ForgedPardon, SkeletonKey, MasterSkeletonKey,
    // SurgeShield (Assassin) and TastyTreat (Ranger). Here: the glasses, and since cc-P199 the orb and the two keys. ServUO's
    // typeof(SkeletonKey) names Enrico's collection mobile (Mobiles/NPCs/Enrico.cs:6), a slip for the key, whose class is
    // Skeletonkey (Items/Tools/SkeletonKey.cs:6); the key is meant and is what is listed. RunedSashOfWarding,
    // ShieldEngravingTool, ForgedPardon and SurgeShield are in neither tree; TastyTreat (cc-P128) is not added, as it
    // was not before.
    private static readonly Type[][] _specialSupplyLoot =
    {
        new Type[] { typeof(LegendaryMapmakersGlasses), typeof(ManaPhasingOrb), null },
        new[] { typeof(LegendaryMapmakersGlasses), typeof(ManaPhasingOrb), typeof(Skeletonkey), typeof(MasterSkeletonKey) },
        new[] { typeof(LegendaryMapmakersGlasses), typeof(ManaPhasingOrb) },
        new[] { typeof(LegendaryMapmakersGlasses), typeof(ManaPhasingOrb) },
        new[] { typeof(LegendaryMapmakersGlasses), typeof(ManaPhasingOrb) }
    };

    // ServUO adds OctopusNecklace.
    private static readonly Type[] _specialCacheHordeAndTrove =
    {
        typeof(SkullGnarledStaff), typeof(SkullLongsword)
    };

    // ServUO adds AncientShipModelOfTheHMSCape.
    private static readonly Type[] _decorativeMinorArtifacts =
    {
        typeof(CandelabraOfSouls), typeof(GoldBricks), typeof(PhillipsWoodenSteed), typeof(AdmiralsHeartyRum)
    };

    public static Type[] DecorativeMinorArtifacts => _decorativeMinorArtifacts;

    private static readonly SkillName[][] _transcendenceTable =
    {
        new[] { SkillName.ArmsLore, SkillName.Blacksmith, SkillName.Carpentry, SkillName.Cartography, SkillName.Cooking, SkillName.Cooking, SkillName.Fletching, SkillName.Mining, SkillName.Tailoring },
        new[] { SkillName.Anatomy, SkillName.DetectHidden, SkillName.Fencing, SkillName.Poisoning, SkillName.RemoveTrap, SkillName.Snooping, SkillName.Stealth },
        new[] { SkillName.Magery, SkillName.Meditation, SkillName.MagicResist, SkillName.Spellweaving },
        new[] { SkillName.Alchemy, SkillName.AnimalLore, SkillName.AnimalTaming, SkillName.Archery },
        new[] { SkillName.Chivalry, SkillName.Focus, SkillName.Parry, SkillName.Swords, SkillName.Tactics, SkillName.Wrestling }
    };

    private static readonly SkillName[][] _alacrityTable =
    {
        new[] { SkillName.ArmsLore, SkillName.Blacksmith, SkillName.Carpentry, SkillName.Cartography, SkillName.Cooking, SkillName.Cooking, SkillName.Fletching, SkillName.Mining, SkillName.Tailoring, SkillName.Lumberjacking },
        new[] { SkillName.DetectHidden, SkillName.Fencing, SkillName.Hiding, SkillName.Lockpicking, SkillName.Poisoning, SkillName.RemoveTrap, SkillName.Snooping, SkillName.Stealing, SkillName.Stealth },
        new[] { SkillName.Alchemy, SkillName.EvalInt, SkillName.Inscribe, SkillName.Magery, SkillName.Meditation, SkillName.Spellweaving, SkillName.SpiritSpeak },
        new[] { SkillName.AnimalLore, SkillName.AnimalTaming, SkillName.Archery, SkillName.Musicianship, SkillName.Peacemaking, SkillName.Provocation, SkillName.Tinkering, SkillName.Tracking, SkillName.Veterinary },
        new[] { SkillName.Chivalry, SkillName.Focus, SkillName.Macing, SkillName.Parry, SkillName.Swords, SkillName.Wrestling }
    };

    private static readonly SkillName[][] _powerscrollTable =
    {
        null,
        new[] { SkillName.Ninjitsu },
        new[] { SkillName.Magery, SkillName.Meditation, SkillName.Mysticism, SkillName.Spellweaving, SkillName.SpiritSpeak },
        new[] { SkillName.AnimalTaming, SkillName.Discordance, SkillName.Provocation, SkillName.Veterinary },
        new[] { SkillName.Bushido, SkillName.Chivalry, SkillName.Focus, SkillName.Healing, SkillName.Parry, SkillName.Swords, SkillName.Tactics }
    };

    /// <summary>Drops a stack into the chest, joining an existing stack of the same type (ServUO's DropItemStacked).</summary>
    public static void DropItemStacked(Container cont, Item item)
    {
        if (item == null)
        {
            return;
        }

        if (item.Stackable)
        {
            foreach (var existing in cont.Items)
            {
                if (existing.Stackable && existing.GetType() == item.GetType() && existing.Hue == item.Hue &&
                    existing.Amount + item.Amount <= 60000)
                {
                    existing.Amount += item.Amount;
                    existing.InvalidateProperties();
                    item.Delete();
                    return;
                }
            }
        }

        cont.DropItem(item);
    }

    /// <summary>The loot of a dug chest in the revamp: ServUO's Fill, less reforging and refinement.</summary>
    public static void Fill(Mobile from, TreasureMapChest chest, TreasureMap tMap)
    {
        var level = tMap.TreasureLevel;
        var package = tMap.Package;
        var facet = tMap.TreasureFacet;
        var quality = chest.ChestQuality;

        chest.Movable = false;
        chest.Locked = true;

        chest.TrapType = TrapType.ExplosionTrap;

        var (requiredSkill, trapPower, trapLevel) = GetLockAndTrap(level);
        chest.RequiredSkill = requiredSkill;
        chest.TrapPower = trapPower;
        chest.TrapLevel = trapLevel;

        chest.LockLevel = chest.RequiredSkill - 10;
        chest.MaxLockLevel = chest.RequiredSkill + 40;

        // ServUO: RefinementComponent.Roll(chest, GetRefinementRolls(quality), 0.9) for a Stash. No refinements in pinned.

        var dropMap = false;

        if (level < TreasureLevel.Trove && 0.1 > Utility.RandomDouble())
        {
            chest.DropItem(new TreasureMap(level + 1, chest.Map, facet == TreasureFacet.Eodon));
            dropMap = true;
        }

        Type[] list;
        int amount;

        list = GetReagentList(level, package, facet);

        if (list != null)
        {
            amount = GetRegAmount(quality);

            for (var i = 0; i < amount; i++)
            {
                DropItemStacked(chest, Loot.Construct(list));
            }
        }

        amount = GetGemCount(quality, level);

        if (amount > 0)
        {
            var bag = new BagOfGems();

            foreach (var gemType in Loot.GemTypes)
            {
                var gem = Loot.Construct(gemType);

                if (gem == null)
                {
                    continue;
                }

                gem.Amount = amount;
                bag.DropItem(gem);
            }

            var goldAmount = GetGoldCount(level);

            while (goldAmount > 0)
            {
                if (goldAmount <= 20000)
                {
                    bag.DropItem(new Gold(goldAmount));
                    goldAmount = 0;
                }
                else
                {
                    bag.DropItem(new Gold(20000));
                    goldAmount -= 20000;
                }
            }

            chest.DropItem(bag);
        }

        list = GetCraftingMaterials(level, package, quality);

        if (list != null)
        {
            amount = GetResourceAmount(level);

            foreach (var type in list)
            {
                var craft = Loot.Construct(type);

                if (craft == null)
                {
                    continue;
                }

                craft.Amount = amount;
                chest.DropItem(craft);
            }
        }

        list = GetSpecialMaterials(level, package, facet);

        if (list != null)
        {
            amount = GetSpecialResourceAmount(quality);

            foreach (var type in list)
            {
                var specialCraft = Loot.Construct(type);

                if (specialCraft == null)
                {
                    continue;
                }

                specialCraft.Amount = amount;
                chest.DropItem(specialCraft);
            }
        }

        amount = (int)level + 1;

        if (dropMap)
        {
            amount--;
        }

        if (amount > 0)
        {
            var transList = GetTranscendenceList(level, package);
            var alacList = GetAlacrityList(level, package, facet);
            var pscrollList = GetPowerScrollList(level, package, facet);

            var scrollList = new List<(int Kind, SkillName Skill)>();

            if (transList != null)
            {
                foreach (var sk in transList)
                {
                    scrollList.Add((1, sk));
                }
            }

            if (alacList != null)
            {
                foreach (var sk in alacList)
                {
                    scrollList.Add((2, sk));
                }
            }

            if (pscrollList != null)
            {
                foreach (var sk in pscrollList)
                {
                    scrollList.Add((3, sk));
                }
            }

            if (scrollList.Count > 0)
            {
                for (var i = 0; i < amount; i++)
                {
                    var (kind, skill) = scrollList[Utility.Random(scrollList.Count)];

                    switch (kind)
                    {
                        case 1:
                            chest.DropItem(new ScrollofTranscendence(skill, Utility.RandomMinMax(1.0, chest.Map == Map.Felucca ? 7.0 : 5.0) / 10));
                            break;
                        case 2:
                            chest.DropItem(new ScrollofAlacrity(skill));
                            break;
                        case 3:
                            chest.DropItem(new PowerScroll(skill, 110.0));
                            break;
                    }
                }
            }
        }

        var dropChance = level switch
        {
            TreasureLevel.Supply => 0.2,
            TreasureLevel.Cache  => 0.4,
            TreasureLevel.Hoard  => 0.5,
            TreasureLevel.Trove  => 0.75,
            _                    => 0.0
        };

        if (Utility.RandomDouble() < dropChance)
        {
            list = GetDecorativeList(level, package, facet);

            if (list?.Length > 0)
            {
                var deco = Loot.Construct(list[Utility.Random(list.Length)]);

                if (deco != null)
                {
                    if (Array.IndexOf(_decorativeMinorArtifacts, deco.GetType()) >= 0)
                    {
                        Container pack = new Backpack { Hue = 1278 };
                        pack.DropItem(deco);
                        chest.DropItem(pack);
                    }
                    else
                    {
                        chest.DropItem(deco);
                    }
                }
            }
        }

        dropChance = level switch
        {
            TreasureLevel.Supply => 0.10,
            TreasureLevel.Cache  => 0.20,
            TreasureLevel.Hoard  => 0.50,
            TreasureLevel.Trove  => 0.75,
            _                    => 0.0
        };

        if (Utility.RandomDouble() < dropChance)
        {
            list = GetSpecialLootList(level, package);

            if (list?.Length > 0)
            {
                var type = MutateType(list[Utility.Random(list.Length)], facet);
                var deco = type == null ? TreasureMapChest.GetRandomRecipe() : Loot.Construct(type);

                if (deco != null)
                {
                    if (deco is SkullGnarledStaff or SkullLongsword or GargishSkullGnarledStaff or GargishSkullLongsword)
                    {
                        if (package == TreasurePackage.Artisan)
                        {
                            if (deco is BaseWeapon weapon)
                            {
                                weapon.Quality = WeaponQuality.Exceptional;
                            }
                        }
                        else
                        {
                            ApplyRandomProperties(deco);
                        }
                    }

                    chest.DropItem(deco);
                }
            }
        }

        amount = GetEquipmentAmount(from, level, package);

        foreach (var type in GetRandomEquipment(level, package, facet, amount))
        {
            var item = Loot.Construct(type);

            if (item == null)
            {
                continue;
            }

            ApplyRandomProperties(item);
            chest.DropItem(item);
        }
    }

    /// <summary>Pinned's magic properties (BaseRunicTool with the treasure chest's AOS rolls), in place of ServUO's reforging.</summary>
    public static void ApplyRandomProperties(Item item)
    {
        TreasureMapChest.GetRandomAOSStats(out var attributeCount, out var min, out var max);

        switch (item)
        {
            case BaseWeapon weapon:
                BaseRunicTool.ApplyAttributesTo(weapon, attributeCount, min, max);
                break;
            case BaseArmor armor:
                BaseRunicTool.ApplyAttributesTo(armor, attributeCount, min, max);
                break;
            case BaseHat hat:
                BaseRunicTool.ApplyAttributesTo(hat, attributeCount, min, max);
                break;
            case BaseJewel jewel:
                BaseRunicTool.ApplyAttributesTo(jewel, attributeCount, min, max);
                break;
        }
    }

    private static Type MutateType(Type type, TreasureFacet facet)
    {
        if (facet != TreasureFacet.TerMur)
        {
            return type;
        }

        if (type == typeof(SkullGnarledStaff))
        {
            return typeof(GargishSkullGnarledStaff);
        }

        if (type == typeof(SkullLongsword))
        {
            return typeof(GargishSkullLongsword);
        }

        return type;
    }
}
