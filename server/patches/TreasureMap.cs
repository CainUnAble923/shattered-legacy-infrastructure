// cc-P127: ServUO pub57 Scripts/Services/TreasureMaps/TreasureMap.cs -> ours, replacing pinned's Items/Maps/TreasureMap.cs
// (a routed replacement in apply-patches.sh: the pinned file is 915 lines, ServUO's 1,786, and the diff is the whole
// file). The Forgotten Treasures revamp runs when the expansion is Endless Journey (TreasureMapInfo.NewSystem): five
// levels, packages, chest quality by Cartography, maps on every facet at a random valid spot, a 30-day reset, dig range
// by Cartography. The classic branches are kept as ServUO keeps them.
//
// Kept from pinned so every pinned caller still compiles: the (int level, Map map) constructor (the level is on the
// classic 0-7 scale pinned's creatures, camps, quests and our loot boxes pass; TreasureMapInfo.ConvertLevel turns it
// into a revamp level, as ServUO's BaseCreature does before the call), LootChance, IsInHavenIsland, HasDiggingTool, both
// Spawn overloads, and the serialized fields 0-5 (version 1 adds Package and NextReset; MigrateFrom(V0Content) converts
// a saved map's level and gives it a package, as ServUO's Deserialize does at its version 2).
//
// Changes from ServUO, each argued in shard-migration notes/cc-P127-port-treasure-maps-monster-stealing.md:
//   - GetRandomLocation gives up after MaxLocationAttempts and takes the last point unchecked: ServUO loops forever on a
//     facet whose map files are absent (and in the test host, which has none).
//   - GetWidthAndHeight: ServUO's missing "else" made every Trammel and Felucca map 300 wide (the 600 it sets is at
//     once overwritten); 600 here, as pinned and as the code reads.
//   - ResetLocation assigns the new location: ServUO computes one and discards it, so a reset only un-decoded the map.
//   - A guardian is hued only when it spawned (ServUO dereferences the spawn before its null check).
//   - OnChestOpened tells ClusterFAchievementSystem (the shard's "dug up a treasure map chest" trigger, built waiting for it).
//   - Creatures pinned and ours lack are out of the spawn tables (the list is in the notes).

using System;
using System.Collections.Generic;
using System.IO;
using ModernUO.Serialization;
using Server.Collections;
using Server.ContextMenus;
using Server.Engines.CannedEvil;
using Server.Engines.Harvest;
using Server.Mobiles;
using Server.Multis;
using Server.Network;
using Server.Regions;
using Server.Targeting;

namespace Server.Items;

[SerializationGenerator(1, false)]
public partial class TreasureMap : MapItem
{
    public const int MaxLocationAttempts = 10000;

    /// <summary>ServUO Config "TreasureMaps.LootChance" (0.01): a creature's chance to carry a map (pinned's constant).</summary>
    public static double LootChance { get; private set; } = 0.01;

    /// <summary>ServUO Config "TreasureMaps.Enabled" (true): random valid spots on every facet; off, the classic list.</summary>
    public static bool NewChestLocations { get; set; } = true;

    /// <summary>ServUO Config "TreasureMaps.ResetTime" (30 days): an undug map forgets its decoder and moves.</summary>
    public static TimeSpan ResetTime { get; private set; } = TimeSpan.FromDays(30.0);

    public static void Configure()
    {
        LootChance = ServerConfiguration.GetSetting("treasureMaps.lootChance", 0.01);
        NewChestLocations = ServerConfiguration.GetSetting("treasureMaps.newChestLocations", true);
        ResetTime = TimeSpan.FromDays(ServerConfiguration.GetSetting("treasureMaps.resetDays", 30.0));
    }

    private static Point2D[] _locations;
    private static Point2D[] _havenLocations;

    // Pinned HousePlacement.cs:29-40 (m_RoadIDs, private there): a chest never sits on a road.
    private static readonly int[] RoadIDs =
    {
        0x0071, 0x0078,
        0x00E8, 0x00EB,
        0x07AE, 0x07B1,
        0x3FF4, 0x3FF4,
        0x3FF8, 0x3FFB,
        0x0442, 0x0479, // Sand stones
        0x0501, 0x0510, // Sand stones
        0x0009, 0x0015, // Furrows
        0x0150, 0x015C  // Furrows
    };

    #region Spawn Types
    // ServUO's level-7 row names ColdDrake, FrostDragon and FrostDrake too; none is in pinned or ours.
    private static readonly Type[][] _spawnTypes =
    {
        new[] { typeof(HeadlessOne), typeof(Skeleton) },
        new[] { typeof(Mongbat), typeof(Ratman), typeof(HeadlessOne), typeof(Skeleton), typeof(Zombie) },
        new[] { typeof(OrcishMage), typeof(Gargoyle), typeof(Gazer), typeof(HellHound), typeof(EarthElemental) },
        new[] { typeof(Lich), typeof(OgreLord), typeof(DreadSpider), typeof(AirElemental), typeof(FireElemental) },
        new[] { typeof(DreadSpider), typeof(LichLord), typeof(Daemon), typeof(ElderGazer), typeof(OgreLord) },
        new[] { typeof(LichLord), typeof(Daemon), typeof(ElderGazer), typeof(PoisonElemental), typeof(BloodElemental) },
        new[] { typeof(AncientWyrm), typeof(Balron), typeof(BloodElemental), typeof(PoisonElemental), typeof(Titan) },
        new[] { typeof(BloodElemental), typeof(GreaterDragon), typeof(PoisonElemental) }
    };

    private static readonly Type[][] _tokunoSpawnTypes =
    {
        new[] { typeof(HeadlessOne), typeof(Skeleton) },
        new[] { typeof(HeadlessOne), typeof(Mongbat), typeof(Ratman), typeof(Skeleton), typeof(Zombie) },
        new[] { typeof(EarthElemental), typeof(Gazer), typeof(Gargoyle), typeof(HellHound), typeof(OrcishMage) },
        new[] { typeof(AirElemental), typeof(DreadSpider), typeof(FireElemental), typeof(Lich), typeof(OgreLord) },
        new[] { typeof(ElderGazer), typeof(Daemon), typeof(DreadSpider), typeof(LichLord), typeof(OgreLord) },
        new[] { typeof(FanDancer), typeof(RevenantLion), typeof(Ronin), typeof(RuneBeetle) },
        new[] { typeof(Hiryu), typeof(LadyOfTheSnow), typeof(Oni), typeof(RuneBeetle), typeof(YomotsuWarrior), typeof(YomotsuPriest) },
        new[] { typeof(Yamandon), typeof(LadyOfTheSnow), typeof(RuneBeetle), typeof(YomotsuPriest) }
    };

    private static readonly Type[][] _malasSpawnTypes =
    {
        new[] { typeof(HeadlessOne), typeof(Skeleton) },
        new[] { typeof(Mongbat), typeof(Ratman), typeof(HeadlessOne), typeof(Skeleton), typeof(Zombie) },
        new[] { typeof(OrcishMage), typeof(Gargoyle), typeof(Gazer), typeof(HellHound), typeof(EarthElemental) },
        new[] { typeof(Lich), typeof(OgreLord), typeof(DreadSpider), typeof(AirElemental), typeof(FireElemental) },
        new[] { typeof(DreadSpider), typeof(LichLord), typeof(Daemon), typeof(ElderGazer), typeof(OgreLord) },
        new[] { typeof(LichLord), typeof(Ravager), typeof(WandererOfTheVoid), typeof(Minotaur) },
        new[] { typeof(Devourer), typeof(MinotaurScout), typeof(MinotaurCaptain), typeof(RottingCorpse), typeof(WandererOfTheVoid) },
        new[] { typeof(Devourer), typeof(MinotaurGeneral), typeof(MinotaurCaptain), typeof(RottingCorpse), typeof(WandererOfTheVoid) }
    };

    private static readonly Type[][] _ilshenarSpawnTypes =
    {
        new[] { typeof(HeadlessOne), typeof(Skeleton) },
        new[] { typeof(Mongbat), typeof(Ratman), typeof(HeadlessOne), typeof(Skeleton), typeof(Zombie) },
        new[] { typeof(OrcishMage), typeof(Gargoyle), typeof(Gazer), typeof(HellHound), typeof(EarthElemental) },
        new[] { typeof(Lich), typeof(OgreLord), typeof(DreadSpider), typeof(AirElemental), typeof(FireElemental) },
        new[] { typeof(DreadSpider), typeof(LichLord), typeof(Daemon), typeof(ElderGazer), typeof(OgreLord) },
        new[] { typeof(DarkGuardian), typeof(ExodusOverseer), typeof(GargoyleDestroyer), typeof(GargoyleEnforcer), typeof(PoisonElemental) },
        new[] { typeof(Changeling), typeof(ExodusMinion), typeof(GargoyleEnforcer), typeof(GargoyleDestroyer), typeof(Titan) },
        new[] { typeof(RenegadeChangeling), typeof(ExodusMinion), typeof(GargoyleEnforcer), typeof(GargoyleDestroyer), typeof(Titan) }
    };

    // ServUO's rows name LeatherWolf and DragonWolf too; neither is in pinned or ours.
    private static readonly Type[][] _terMurSpawnTypes =
    {
        new[] { typeof(HeadlessOne), typeof(Skeleton) },
        new[] { typeof(ClockworkScorpion), typeof(CorrosiveSlime), typeof(GreaterMongbat) },
        new[] { typeof(AcidSlug), typeof(FireElemental), typeof(WaterElemental) },
        new[] { typeof(StoneSlith), typeof(ToxicSlith) },
        new[] { typeof(BloodWorm), typeof(Kepetch), typeof(StoneSlith), typeof(ToxicSlith) },
        new[] { typeof(FireAnt), typeof(LavaElemental), typeof(MaddeningHorror) },
        new[] { typeof(EnragedEarthElemental), typeof(FireDaemon), typeof(GreaterPoisonElemental), typeof(LavaElemental) },
        new[] { typeof(EnragedColossus), typeof(EnragedEarthElemental), typeof(FireDaemon), typeof(GreaterPoisonElemental), typeof(LavaElemental) }
    };

    // ServUO's rows name MyrmidexLarvae, WildTiger, Lion, SabertoothedTiger, Infernus, KotlAutomaton, MyrmidexDrone,
    // Triceratops and Anchisaur too; none is in pinned or ours.
    private static readonly Type[][] _eodonSpawnTypes =
    {
        new[] { typeof(SilverbackGorilla), typeof(Panther) },
        new[] { typeof(AcidElemental), typeof(SandVortex) },
        new[] { typeof(AcidElemental), typeof(SandVortex) },
        new[] { typeof(FireElemental), typeof(Dimetrosaur), typeof(Saurosaurus) },
        new[] { typeof(FireElemental), typeof(Dimetrosaur), typeof(Saurosaurus) },
        new[] { typeof(Allosaurus) },
        new[] { typeof(Allosaurus), typeof(SandVortex) }
    };
    #endregion

    #region Spawn Locations
    private static readonly Rectangle2D[] _felTramWrap = { new(0, 0, 5119, 4095) };

    private static readonly Rectangle2D[] _tokunoWrap =
    {
        new(155, 207, 30, 40), new(280, 230, 157, 45), new(445, 215, 30, 35), new(447, 53, 58, 40),
        new(612, 240, 20, 17), new(167, 275, 53, 60), new(734, 407, 14, 22), new(753, 489, 8, 30),
        new(624, 619, 20, 24), new(624, 725, 8, 8), new(574, 734, 20, 16), new(431, 752, 25, 27),
        new(348, 968, 52, 135), new(282, 1188, 90, 100), new(348, 1335, 50, 50),
        new(228, 284, 500, 316), new(95, 600, 345, 243), new(155, 842, 146, 1358), new(495, 812, 435, 350),
        new(501, 1156, 100, 150), new(876, 1156, 90, 150),
        new(970, 1159, 14, 25), new(990, 1151, 5, 15), new(1004, 1120, 16, 30), new(1008, 1032, 12, 15),
        new(1163, 383, 20, 20),
        new(839, 30, 168, 120), new(707, 150, 307, 250), new(845, 397, 179, 75), new(1068, 382, 60, 80),
        new(787, 687, 60, 72), new(848, 473, 557, 655)
    };

    private static readonly Rectangle2D[] _malasWrap =
    {
        new(611, 67, 1862, 705), new(1540, 852, 286, 182), new(602, 784, 546, 746), new(1160, 1035, 1299, 871)
    };

    private static readonly Rectangle2D[] _ilshenarWrap =
    {
        new(221, 314, 657, 286), new(530, 600, 212, 205), new(261, 805, 495, 655), new(908, 925, 90, 170),
        new(1031, 904, 730, 450), new(1028, 630, 318, 161), new(1205, 368, 265, 237), new(1551, 516, 200, 130)
    };

    private static readonly Rectangle2D[] _terMurWrap =
    {
        new(535, 2895, 85, 117), new(525, 3085, 115, 70), new(755, 2860, 400, 270), new(1025, 3280, 190, 100),
        new(305, 3445, 175, 255), new(480, 3540, 90, 110), new(605, 3880, 200, 170), new(750, 3830, 80, 80)
    };

    private static readonly Rectangle2D[] _eodonWrap =
    {
        new(259, 1400, 354, 510), new(259, 1400, 354, 510), new(259, 1400, 354, 510), new(688, 1440, 46, 88),
        new(613, 1466, 65, 139), new(678, 1568, 43, 40), new(613, 1720, 91, 72), new(618, 1792, 44, 273),
        new(662, 1969, 84, 166), new(754, 1963, 100, 65), new(174, 1540, 85, 420)
    };
    #endregion

    [InvalidateProperties]
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private Mobile _completedBy;

    [InvalidateProperties]
    [SerializableField(1, setter: "private")]
    private int _mapLevel;

    [InvalidateProperties]
    [SerializableField(2)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private bool _completed;

    [InvalidateProperties]
    [SerializableField(3)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private Mobile _decoder;

    [InvalidateProperties]
    [SerializableField(4)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private Map _chestMap;

    [SerializableField(5)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private Point2D _chestLocation;

    [InvalidateProperties]
    [SerializableField(6)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private TreasurePackage _package;

    [SerializableField(7)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private DateTime _nextReset;

    private void MigrateFrom(V0Content content)
    {
        _completedBy = content.CompletedBy;
        _mapLevel = TreasureMapInfo.ConvertLevel(content.Level);
        _completed = content.Completed;
        _decoder = content.Decoder;
        _chestMap = content.ChestMap;
        _chestLocation = content.ChestLocation;

        if (TreasureMapInfo.NewSystem)
        {
            AssignRandomPackage();
        }

        _nextReset = Core.Now + ResetTime;
    }

    /// <summary>
    /// The level, capped at Trove in the revamp (ServUO's Level setter). Set it through the constructor or
    /// TreasureLevel; the saved field is private so the cap always applies.
    /// </summary>
    [CommandProperty(AccessLevel.GameMaster)]
    public int Level
    {
        get => _mapLevel;
        set
        {
            MapLevel = Math.Min(value, TreasureMapInfo.NewSystem ? 4 : 7);
            InvalidateProperties();
        }
    }

    [CommandProperty(AccessLevel.GameMaster)]
    public TreasureLevel TreasureLevel
    {
        get => (TreasureLevel)_mapLevel;
        set
        {
            if ((int)value != _mapLevel)
            {
                Level = (int)value;
            }
        }
    }

    [CommandProperty(AccessLevel.GameMaster)]
    public TreasureFacet TreasureFacet => TreasureMapInfo.GetFacet(ChestLocation, _chestMap);

    /// <summary>
    /// Pinned's constructor, kept for every pinned caller: the level is on the classic scale (0 young, 1 to 7), and the
    /// revamp converts it (ServUO does the same in BaseCreature.OnBeforeDeath and ParagonChest before this call).
    /// </summary>
    [Constructible]
    public TreasureMap(int level, Map map) : this(level, map, false)
    {
    }

    public TreasureMap(int level, Map map, bool eodon) : this((TreasureLevel)TreasureMapInfo.ConvertLevel(level), map, eodon)
    {
    }

    /// <summary>A map at a revamp level (ServUO's constructor, which takes the new scale directly).</summary>
    public TreasureMap(TreasureLevel level, Map map, bool eodon = false)
    {
        Level = (int)level;
        var newSystem = TreasureMapInfo.NewSystem;

        if (newSystem)
        {
            AssignRandomPackage();
        }

        if (!newSystem && _mapLevel == 7 || map == null || map == Map.Internal)
        {
            map = GetRandomMap();
        }

        _chestMap = map;
        Facet = map;

        if (!newSystem && _mapLevel == 0)
        {
            _chestLocation = GetRandomHavenLocation();
        }
        else
        {
            _chestLocation = GetRandomLocation(map, eodon);
        }

        Width = 300;
        Height = 300;

        GetWidthAndHeight(map, out var width, out var height);

        var x1 = _chestLocation.X - Utility.RandomMinMax(width / 4, width / 4 * 3);
        var y1 = _chestLocation.Y - Utility.RandomMinMax(height / 4, height / 4 * 3);

        if (x1 < 0)
        {
            x1 = 0;
        }

        if (y1 < 0)
        {
            y1 = 0;
        }

        AdjustMap(map, out var x2, out var y2, x1, y1, width, height, eodon);

        x1 = x2 - width;
        y1 = y2 - height;

        Bounds = new Rectangle2D(x1, y1, width, height);
        Protected = true;

        AddWorldPin(_chestLocation.X, _chestLocation.Y);

        _nextReset = Core.Now + ResetTime;
    }

    protected void AssignRandomPackage()
    {
        Package = (TreasurePackage)Utility.Random(5);
    }

    /// <summary>ServUO: the chest's quality from the digger's Cartography against the level's difficulty.</summary>
    public void AssignChestQuality(Mobile digger, TreasureMapChest chest)
    {
        var skill = digger.Skills[SkillName.Cartography].Value;

        var dif = TreasureLevel switch
        {
            TreasureLevel.Supply => 200,
            TreasureLevel.Cache  => 300,
            TreasureLevel.Hoard  => 400,
            TreasureLevel.Trove  => 500,
            _                    => 100
        };

        if (Utility.Random(dif) <= skill)
        {
            chest.ChestQuality = ChestQuality.Gold;
        }
        else if (Utility.Random(dif) <= skill * 2)
        {
            chest.ChestQuality = ChestQuality.Standard;
        }
        else
        {
            chest.ChestQuality = ChestQuality.Rusty;
        }
    }

    public override int LabelNumber
    {
        get
        {
            if (_decoder != null)
            {
                return _mapLevel switch
                {
                    6 => 1063453,
                    7 => 1116773,
                    _ => 1041516 + _mapLevel
                };
            }

            return _mapLevel switch
            {
                6 => 1063452,
                7 => 1116790,
                _ => 1041510 + _mapLevel
            };
        }
    }

    public static Map GetRandomMap() =>
        Utility.Random(8) switch
        {
            1      => Map.Felucca,
            2 or 3 => Map.Ilshenar,
            4 or 5 => Map.Malas,
            6 or 7 => Map.Tokuno,
            _      => Map.Trammel
        };

    public static Point2D GetRandomLocation(Map map) => GetRandomLocation(map, false);

    public static Point2D GetRandomLocation(Map map, bool eodon)
    {
        if (!NewChestLocations || map == null)
        {
            return GetRandomClassicLocation();
        }

        Rectangle2D[] recs;

        if (map == Map.Trammel || map == Map.Felucca)
        {
            recs = _felTramWrap;
        }
        else if (map == Map.Tokuno)
        {
            recs = _tokunoWrap;
        }
        else if (map == Map.Malas)
        {
            recs = _malasWrap;
        }
        else if (map == Map.Ilshenar)
        {
            recs = _ilshenarWrap;
        }
        else if (eodon)
        {
            recs = _eodonWrap;
        }
        else
        {
            recs = _terMurWrap;
        }

        var x = 0;
        var y = 0;

        for (var i = 0; i < MaxLocationAttempts; i++)
        {
            var rec = recs[Utility.Random(recs.Length)];

            x = Utility.Random(rec.X, rec.Width);
            y = Utility.Random(rec.Y, rec.Height);

            if (ValidateLocation(x, y, map))
            {
                return new Point2D(x, y);
            }
        }

        // No valid spot in MaxLocationAttempts tries: the facet's map files are missing or the host has no tile data.
        // ServUO loops forever here. The last point stands, unchecked.
        return new Point2D(x, y);
    }

    public static bool ValidateLocation(int x, int y, Map map)
    {
        var lt = map.Tiles.GetLandTile(x, y);

        // A host with no tile data (the test host) has no land table: nothing validates, and the caller's cap holds.
        if (TileData.LandTable == null || lt.ID < 0 || lt.ID >= TileData.LandTable.Length)
        {
            return false;
        }

        var ld = TileData.LandTable[lt.ID];

        // Checks for impassable flag..cant walk, cant have a chest
        if (lt.Ignored || (ld.Flags & TileFlag.Impassable) != 0)
        {
            return false;
        }

        // Checks for roads (pinned's HousePlacement keeps its road table private; the same ranges)
        for (var i = 0; i < RoadIDs.Length; i += 2)
        {
            if (lt.ID >= RoadIDs[i] && lt.ID <= RoadIDs[i + 1])
            {
                return false;
            }
        }

        var reg = Region.Find(new Point3D(x, y, lt.Z), map);

        // no-go in towns, houses, dungeons and champspawns
        if (reg != null)
        {
            if (reg.IsPartOf<TownRegion>() || reg.IsPartOf<DungeonRegion>() ||
                reg.IsPartOf<ChampionSpawnRegion>() || reg.IsPartOf<HouseRegion>())
            {
                return false;
            }
        }

        if (!IsDiggableLand(ld.Name))
        {
            return false;
        }

        // Rare occrunces where a static tile needs to be checked
        foreach (var tile in map.Tiles.GetStaticTiles(x, y))
        {
            var index = tile.ID & TileData.MaxItemValue;

            if (TileData.ItemTable == null || index >= TileData.ItemTable.Length)
            {
                return false;
            }

            var td = TileData.ItemTable[index];

            if ((td.Flags & TileFlag.Impassable) != 0)
            {
                return false;
            }

            if (!IsDiggableLand(td.Name))
            {
                return false;
            }
        }

        // check for house within 5 tiles
        for (var xx = x - 5; xx <= x + 5; xx++)
        {
            for (var yy = y - 5; yy <= y + 5; yy++)
            {
                if (BaseHouse.FindHouseAt(new Point3D(xx, yy, lt.Z), map, Region.MaxZ - lt.Z) != null)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool IsDiggableLand(string name)
    {
        var n = (name ?? string.Empty).ToLowerInvariant();
        return n is "dirt" or "grass" or "jungle" or "forest" or "snow";
    }

    public static void GetWidthAndHeight(Map map, out int width, out int height)
    {
        if (map == Map.Trammel || map == Map.Felucca)
        {
            width = 600;
            height = 600;
        }
        else if (map == Map.TerMur)
        {
            width = 200;
            height = 200;
        }
        else
        {
            width = 300;
            height = 300;
        }
    }

    public static void AdjustMap(Map map, out int x2, out int y2, int x1, int y1, int width, int height, bool eodon)
    {
        x2 = x1 + width;
        y2 = y1 + height;

        if (map == Map.Trammel || map == Map.Felucca)
        {
            if (x2 >= 5120)
            {
                x2 = 5119;
            }

            if (y2 >= 4096)
            {
                y2 = 4095;
            }
        }
        else if (map == Map.Ilshenar)
        {
            if (x2 >= 1890)
            {
                x2 = 1889;
            }

            if (x2 <= 120)
            {
                x2 = 121;
            }

            if (y2 >= 1465)
            {
                y2 = 1464;
            }

            if (y2 <= 105)
            {
                y2 = 106;
            }
        }
        else if (map == Map.Malas)
        {
            if (x2 >= 2522)
            {
                x2 = 2521;
            }

            if (x2 <= 515)
            {
                x2 = 516;
            }

            if (y2 >= 1990)
            {
                y2 = 1989;
            }

            if (y2 <= 0)
            {
                y2 = 1;
            }
        }
        else if (map == Map.Tokuno)
        {
            if (x2 >= 1428)
            {
                x2 = 1427;
            }

            if (x2 <= 0)
            {
                x2 = 1;
            }

            if (y2 >= 1420)
            {
                y2 = 1419;
            }

            if (y2 <= 0)
            {
                y2 = 1;
            }
        }
        else if (map == Map.TerMur)
        {
            if (eodon)
            {
                if (x2 <= 62)
                {
                    x2 = 63;
                }

                if (x2 >= 960)
                {
                    x2 = 959;
                }

                if (y2 <= 1343)
                {
                    y2 = 1344;
                }

                if (y2 >= 2240)
                {
                    y2 = 2239;
                }
            }
            else
            {
                if (x2 >= 1271)
                {
                    x2 = 1270;
                }

                if (x2 <= 260)
                {
                    x2 = 261;
                }

                if (y2 >= 4094)
                {
                    y2 = 4083;
                }

                if (y2 <= 2760)
                {
                    y2 = 2761;
                }
            }
        }
    }

    public virtual void OnMapComplete(Mobile from, TreasureMapChest chest)
    {
    }

    /// <summary>The owner's first look into the chest. Also the shard's achievement trigger.</summary>
    public virtual void OnChestOpened(Mobile from, TreasureMapChest chest)
    {
        if (from is PlayerMobile pm)
        {
            ClusterFAchievementSystem.NotifyTreasureMap(pm);
        }
    }

    public static Point2D GetRandomClassicLocation()
    {
        if (_locations == null)
        {
            LoadLocations();
        }

        return _locations?.Length > 0 ? _locations.RandomElement() : Point2D.Zero;
    }

    public static Point2D GetRandomHavenLocation()
    {
        if (_havenLocations == null)
        {
            LoadLocations();
        }

        return _havenLocations?.Length > 0 ? _havenLocations.RandomElement() : Point2D.Zero;
    }

    private static void LoadLocations()
    {
        var filePath = Path.Combine(Core.BaseDirectory, "Data/treasure.cfg");

        var list = new List<Point2D>();
        var havenList = new List<Point2D>();

        if (File.Exists(filePath))
        {
            using var ip = new StreamReader(filePath);
            string line;

            while ((line = ip.ReadLine()) != null)
            {
                try
                {
                    var split = line.Split(' ');

                    int x = Convert.ToInt32(split[0]), y = Convert.ToInt32(split[1]);

                    var loc = new Point2D(x, y);
                    list.Add(loc);

                    if (IsInHavenIsland(loc))
                    {
                        havenList.Add(loc);
                    }
                }
                catch
                {
                    // ignored
                }
            }
        }

        _locations = list.ToArray();
        _havenLocations = havenList.ToArray();
    }

    public static bool IsInHavenIsland(IPoint2D loc) => loc.X >= 3314 && loc.X <= 3814 && loc.Y >= 2345 && loc.Y <= 3095;

    /// <summary>Pinned's overload: a Trammel or Felucca spawn (the table pinned used for every map).</summary>
    public static BaseCreature Spawn(int level, Point3D p, bool guardian) => Spawn(level, p, guardian, Map.Trammel);

    public static BaseCreature Spawn(int level, Point3D p, bool guardian, Map map)
    {
        Type[][] spawns;

        if (map == Map.Trammel || map == Map.Felucca)
        {
            spawns = _spawnTypes;
        }
        else if (map == Map.Tokuno)
        {
            spawns = _tokunoSpawnTypes;
        }
        else if (map == Map.Ilshenar)
        {
            spawns = _ilshenarSpawnTypes;
        }
        else if (map == Map.Malas)
        {
            spawns = _malasSpawnTypes;
        }
        else if (TreasureMapInfo.IsEodon(map, p))
        {
            spawns = _eodonSpawnTypes;
        }
        else
        {
            spawns = _terMurSpawnTypes;
        }

        if (level < 0 || level >= spawns.Length)
        {
            return null;
        }

        BaseCreature bc;
        var list = GetSpawnList(spawns, level);

        if (list == null || list.Length == 0)
        {
            return null;
        }

        try
        {
            bc = list.RandomElement().CreateInstance<BaseCreature>();
        }
        catch
        {
            return null;
        }

        bc.Home = p;
        bc.RangeHome = 5;

        if (guardian)
        {
            bc.Title = "(Guardian)";

            if (!TreasureMapInfo.NewSystem && level == 0)
            {
                bc.Name = "a chest guardian";
                bc.Hue = 0x835;
            }
        }

        return bc;
    }

    public static BaseCreature Spawn(int level, Point3D p, Map map, Mobile target, bool guardian)
    {
        if (map == null)
        {
            return null;
        }

        var c = Spawn(level, p, guardian, map);

        if (c == null)
        {
            return null;
        }

        var spawned = false;

        for (var i = 0; !spawned && i < 10; ++i)
        {
            var x = p.X - 3 + Utility.Random(7);
            var y = p.Y - 3 + Utility.Random(7);

            if (map.CanSpawnMobile(x, y, p.Z))
            {
                c.MoveToWorld(new Point3D(x, y, p.Z), map);
                spawned = true;
            }
            else
            {
                var z = map.GetAverageZ(x, y);

                if (map.CanSpawnMobile(x, y, z))
                {
                    c.MoveToWorld(new Point3D(x, y, z), map);
                    spawned = true;
                }
            }
        }

        if (!spawned)
        {
            c.Delete();
            return null;
        }

        if (target != null)
        {
            Timer.DelayCall(() => c.Combatant = target);
        }

        return c;
    }

    /// <summary>ServUO: the revamp folds the classic eight rows into five levels.</summary>
    public static Type[] GetSpawnList(Type[][] table, int level)
    {
        if (!TreasureMapInfo.NewSystem)
        {
            return table[level];
        }

        switch (level)
        {
            case 2:
                {
                    var list = new List<Type>();
                    list.AddRange(table[2]);
                    list.AddRange(table[3]);
                    return list.ToArray();
                }
            case 3:
                {
                    var list = new List<Type>();
                    list.AddRange(table[4]);
                    list.AddRange(table[5]);
                    return list.ToArray();
                }
            case 4:
                return table[6];
            case 5:
                return table[Math.Min(7, table.Length - 1)];
            default:
                return table[Math.Min(level + 1, table.Length - 1)];
        }
    }

    public static bool HasDiggingTool(Mobile m)
    {
        if (m.Backpack == null)
        {
            return false;
        }

        foreach (var tool in m.Backpack.FindItemsByType<BaseHarvestTool>())
        {
            if (tool.HarvestSystem == Mining.System)
            {
                return true;
            }
        }

        return false;
    }

    public virtual void OnBeginDig(Mobile from)
    {
        if (_completed)
        {
            from.SendLocalizedMessage(503028); // The treasure for this map has already been found.
        }
        else if (_mapLevel == 0 && !CheckYoung(from))
        {
            from.SendLocalizedMessage(1046447); // Only a young player may use this treasure map.
        }
        else if (_decoder != from && !HasRequiredSkill(from))
        {
            // You did not decode this map and have no clue where to look for the treasure.
            from.SendLocalizedMessage(503031);
        }
        else if (!from.CanBeginAction<TreasureMap>())
        {
            from.SendLocalizedMessage(503020); // You are already digging treasure.
        }
        else if (from.Map != _chestMap)
        {
            from.SendLocalizedMessage(1010479); // You seem to be in the right place, but may be on the wrong facet!
        }
        else
        {
            from.SendLocalizedMessage(503033); // Where do you wish to dig?
            from.Target = new DigTarget(this);
        }
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (!from.InRange(GetWorldLocation(), 2))
        {
            from.LocalOverheadMessage(MessageType.Regular, 0x3B2, 1019045); // I can't reach that.
            return;
        }

        if (!_completed && _decoder == null)
        {
            Decode(from);
        }
        else
        {
            DisplayTo(from);
        }
    }

    public virtual void Decode(Mobile from)
    {
        if (_completed || _decoder != null)
        {
            return;
        }

        if (_mapLevel == 0)
        {
            if (!CheckYoung(from))
            {
                from.SendLocalizedMessage(1046447); // Only a young player may use this treasure map.
                return;
            }
        }
        else
        {
            var minSkill = GetMinSkillLevel();

            if (from.Skills[SkillName.Cartography].Value < minSkill)
            {
                if (_mapLevel == 1)
                {
                    from.CheckSkill(SkillName.Cartography, 0, minSkill);
                }
                else
                {
                    from.SendLocalizedMessage(503013); // The map is too difficult to attempt to decode.
                }
            }

            if (!from.CheckSkill(SkillName.Cartography, minSkill - 10, minSkill + 30))
            {
                from.LocalOverheadMessage(MessageType.Regular, 0x3B2, 503018); // You fail to make anything of the map.
                return;
            }
        }

        from.LocalOverheadMessage(MessageType.Regular, 0x3B2, 503019); // You successfully decode a treasure map!
        Decoder = from;

        if (Core.AOS)
        {
            LootType = LootType.Blessed;
        }

        DisplayTo(from);
    }

    /// <summary>ServUO: 30 days undug, the map forgets its decoder and picks a new spot.</summary>
    public void ResetLocation()
    {
        if (_completed)
        {
            return;
        }

        ClearPins();
        LootType = LootType.Regular;
        Decoder = null;
        ChestLocation = GetRandomLocation(_chestMap, TreasureMapInfo.NewSystem && TreasureFacet == TreasureFacet.Eodon);
        AddWorldPin(_chestLocation.X, _chestLocation.Y);
        InvalidateProperties();
        NextReset = Core.Now + ResetTime;
    }

    public override void DisplayTo(Mobile from)
    {
        if (_completed)
        {
            SendLocalizedMessageTo(from, 503014); // This treasure hunt has already been completed.
        }
        else if (_mapLevel == 0 && !CheckYoung(from))
        {
            from.SendLocalizedMessage(1046447); // Only a young player may use this treasure map.
            return;
        }
        else if (_decoder != from && !HasRequiredSkill(from))
        {
            // You did not decode this map and have no clue where to look for the treasure.
            from.SendLocalizedMessage(503031);
            return;
        }
        else
        {
            // The treasure is marked by the red pin. Grab a shovel and go dig it up!
            SendLocalizedMessageTo(from, 503017);
        }

        if (Pins.Count == 0)
        {
            AddWorldPin(_chestLocation.X, _chestLocation.Y);
        }

        from.PlaySound(0x249);
        base.DisplayTo(from);
    }

    public override void GetContextMenuEntries(Mobile from, ref PooledRefList<ContextMenuEntry> list)
    {
        base.GetContextMenuEntries(from, ref list);

        if (!_completed)
        {
            if (_decoder == null)
            {
                list.Add(new DecodeMapEntry());
            }
            else
            {
                list.Add(new OpenMapEntry());
                list.Add(new DigEntry(HasDiggingTool(from)));
            }
        }
    }

    public override void AddNameProperty(IPropertyList list)
    {
        if (TreasureMapInfo.NewSystem)
        {
            // a ~2_package~ ~1_level~ treasure map (tattered, or decoded)
            list.Add(
                _decoder != null ? 1158975 + _mapLevel : 1158980 + _mapLevel,
                $"#{TreasureMapInfo.PackageLocalization(_package)}"
            );
        }
        else
        {
            base.AddNameProperty(list);
        }
    }

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);

        var facet = TreasureMapInfo.GetFacet(_chestLocation, _chestMap);

        list.Add(FacetLabel(facet));

        if (_completed)
        {
            list.Add(1041507, _completedBy?.RawName ?? "someone"); // completed by ~1_val~
        }
    }

    public static int FacetLabel(TreasureFacet facet) =>
        facet switch
        {
            TreasureFacet.Felucca  => 1041502, // for somewhere in Felucca
            TreasureFacet.Ilshenar => 1060850,
            TreasureFacet.Malas    => 1060851,
            TreasureFacet.Tokuno   => 1115645,
            TreasureFacet.TerMur   => 1115646,
            TreasureFacet.Eodon    => 1158985,
            _                      => 1041503 // for somewhere in Trammel
        };

    public override void OnSingleClick(Mobile from)
    {
        if (_completed)
        {
            from.NetState.SendMessageLocalizedAffix(
                Serial,
                ItemID,
                MessageType.Label,
                0x3B2,
                3,
                1048030,
                "",
                AffixType.Append,
                $" completed by {_completedBy?.RawName ?? "someone"}"
            );
        }
        else if (_decoder != null)
        {
            if (_mapLevel == 6)
            {
                LabelTo(from, 1063453);
            }
            else
            {
                LabelTo(from, 1041516 + _mapLevel);
            }
        }
        else
        {
            if (_mapLevel == 6)
            {
                LabelTo(from, 1041522, $"#{1063452}\t \t#{(_chestMap == Map.Felucca ? 1041502 : 1041503)}");
            }
            else
            {
                LabelTo(from, 1041522, $"#{1041510 + _mapLevel}\t \t#{(_chestMap == Map.Felucca ? 1041502 : 1041503)}");
            }
        }
    }

    [AfterDeserialization]
    private void AfterDeserialization()
    {
        if (Core.AOS && _decoder != null && LootType == LootType.Regular)
        {
            LootType = LootType.Blessed;
        }

        if (_nextReset == DateTime.MinValue)
        {
            _nextReset = Core.Now + ResetTime;
        }
        else if (!_completed && _nextReset < Core.Now)
        {
            Timer.DelayCall(TimeSpan.FromSeconds(30), ResetLocation);
        }
    }

    private bool CheckYoung(Mobile from)
    {
        if (from.AccessLevel >= AccessLevel.GameMaster || TreasureMapInfo.NewSystem)
        {
            return true;
        }

        if (from is PlayerMobile { Young: true })
        {
            return true;
        }

        if (from == Decoder)
        {
            Level = 1;
            from.SendLocalizedMessage(1046446); // This is now a level one treasure map.
            return true;
        }

        return false;
    }

    /// <summary>ServUO's table, read by the map's level: in the revamp Stash 0, Supply 27, Cache 71, Hoard 81, Trove 91.</summary>
    public double GetMinSkillLevel() =>
        _mapLevel switch
        {
            1      => Core.AOS ? 27.0 : -3.0,
            2      => Core.AOS ? 71.0 : 41.0,
            3      => Core.AOS ? 81.0 : 51.0,
            4      => Core.AOS ? 91.0 : 61.0,
            5 or 6 => Core.AOS ? 100.0 : 70.0,
            7      => 100.0,
            _      => 0.0
        };

    protected virtual bool HasRequiredSkill(Mobile from) => from.Skills[SkillName.Cartography].Value >= GetMinSkillLevel();

    /// <summary>
    /// The dig has reached the chest (DigTimer, and the pack mule's survey dig): the chest takes the map, the loot and
    /// its guardians. The chest must have been made temporary so nothing filled it yet.
    /// </summary>
    public void OnDug(Mobile from, TreasureMapChest chest)
    {
        chest.Temporary = false;
        chest.TreasureMap = this;
        chest.DigTime = Core.Now;
        Completed = true;
        CompletedBy = from;

        if (TreasureMapInfo.NewSystem)
        {
            AssignChestQuality(from, chest);
            TreasureMapInfo.Fill(from, chest, this);
        }
        else
        {
            TreasureMapChest.Fill(chest, chest.Level);
        }

        chest.MarkFilled();

        OnMapComplete(from, chest);

        var spawns = _mapLevel switch
        {
            0 => TreasureMapInfo.NewSystem ? 4 : 3,
            1 => TreasureMapInfo.NewSystem ? 4 : 0,
            _ => 4
        };

        for (var i = 0; i < spawns; ++i)
        {
            var guardian = !TreasureMapInfo.NewSystem || Utility.RandomDouble() >= 0.3;

            var bc = Spawn(_mapLevel, chest.Location, chest.Map, null, guardian);

            if (bc == null)
            {
                continue;
            }

            if (TreasureMapInfo.NewSystem)
            {
                bc.Hue = 2725;
            }

            if (guardian)
            {
                chest.AddGuardian(bc);
            }
        }
    }

    protected class DigTarget : Target
    {
        private readonly TreasureMap m_Map;

        public DigTarget(TreasureMap map) : base(6, true, TargetFlags.None) => m_Map = map;

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (m_Map.Deleted)
            {
                return;
            }

            var map = m_Map._chestMap;

            if (m_Map._completed)
            {
                from.SendLocalizedMessage(503028); // The treasure for this map has already been found.
            }
            else if (m_Map._decoder != from && !m_Map.HasRequiredSkill(from))
            {
                // You did not decode this map and have no clue where to look for the treasure.
                from.SendLocalizedMessage(503031);
            }
            else if (!from.CanBeginAction<TreasureMap>())
            {
                from.SendLocalizedMessage(503020); // You are already digging treasure.
            }
            else if (!HasDiggingTool(from))
            {
                from.SendLocalizedMessage(1114416); // You must have a digging tool to dig for treasure.
            }
            else if (from.Map != map)
            {
                from.SendLocalizedMessage(1010479); // You seem to be in the right place, but may be on the wrong facet!
            }
            else
            {
                var p = targeted as IPoint3D;

                var targ3D = (p as Item)?.GetWorldLocation() ?? new Point3D(p);

                // The revamp reads the digger's Cartography; the classic system Mining.
                var skillValue = TreasureMapInfo.NewSystem
                    ? from.Skills[SkillName.Cartography].Value
                    : from.Skills[SkillName.Mining].Value;

                var maxRange = skillValue switch
                {
                    >= 100.0 => 4,
                    >= 81.0  => 3,
                    >= 51.0  => 2,
                    _        => 1
                };

                var loc = m_Map.ChestLocation;
                int x = loc.X, y = loc.Y;

                var chest3D0 = new Point3D(loc, 0);

                if (Utility.InRange(targ3D, chest3D0, maxRange))
                {
                    if (from.Location.X == x && from.Location.Y == y)
                    {
                        // The chest can't be dug up because you are standing on top of it.
                        from.SendLocalizedMessage(503030);
                    }
                    else if (map != null)
                    {
                        var z = map.GetAverageZ(x, y);

                        if (!map.CanFit(x, y, z, 16, true))
                        {
                            // You have found the treasure chest but something is keeping it from being dug up.
                            from.SendLocalizedMessage(503021);
                        }
                        else if (from.BeginAction<TreasureMap>())
                        {
                            new DigTimer(from, m_Map, new Point3D(x, y, z), map).Start();
                        }
                        else
                        {
                            from.SendLocalizedMessage(503020); // You are already digging treasure.
                        }
                    }
                }
                else if (m_Map.Level > 0)
                {
                    if (Utility.InRange(targ3D, chest3D0, 8)) // We're close, but not quite
                    {
                        from.SendLocalizedMessage(503032); // You dig and dig but no treasure seems to be here.
                    }
                    else
                    {
                        from.SendLocalizedMessage(503035); // You dig and dig but fail to find any treasure.
                    }
                }
                else
                {
                    if (Utility.InRange(targ3D, chest3D0, 8)) // We're close, but not quite
                    {
                        from.SendAsciiMessage(0x44, "The treasure chest is very close!");
                    }
                    else
                    {
                        var dir = Utility.GetDirection(targ3D, chest3D0);

                        var sDir = dir switch
                        {
                            Direction.North => "north",
                            Direction.Right => "northeast",
                            Direction.East  => "east",
                            Direction.Down  => "southeast",
                            Direction.South => "south",
                            Direction.Left  => "southwest",
                            Direction.West  => "west",
                            _               => "northwest"
                        };

                        from.SendAsciiMessage(0x44, $"Try looking for the treasure chest more to the {sDir}.");
                    }
                }
            }
        }
    }

    private class DigTimer : Timer
    {
        private readonly Mobile m_From;
        private readonly long m_LastMoveTime;
        private readonly Map m_Map;
        private readonly long m_NextActionTime;
        private readonly long m_NextSkillTime;
        private readonly long m_NextSpellTime;
        private readonly TreasureMap m_TreasureMap;
        private TreasureMapChest m_Chest;

        private int m_Count;

        private TreasureChestDirt m_Dirt1;
        private TreasureChestDirt m_Dirt2;

        private readonly Point3D m_Location;

        public DigTimer(Mobile from, TreasureMap treasureMap, Point3D location, Map map) : base(
            TimeSpan.Zero,
            TimeSpan.FromSeconds(1.0)
        )
        {
            m_From = from;
            m_TreasureMap = treasureMap;

            m_Location = location;
            m_Map = map;

            m_NextSkillTime = from.NextSkillTime;
            m_NextSpellTime = from.NextSpellTime;
            m_NextActionTime = from.NextActionTime;
            m_LastMoveTime = from.LastMoveTime;
        }

        private void Terminate()
        {
            Stop();
            m_From.EndAction<TreasureMap>();

            m_Chest?.Delete();
            m_Dirt1?.Delete();
            m_Dirt2?.Delete();
        }

        protected override void OnTick()
        {
            // The revamp lets a digger cast while digging; the classic system does not.
            if (m_NextSkillTime != m_From.NextSkillTime ||
                !TreasureMapInfo.NewSystem && m_NextSpellTime != m_From.NextSpellTime ||
                m_NextActionTime != m_From.NextActionTime)
            {
                Terminate();
                return;
            }

            if (m_LastMoveTime != m_From.LastMoveTime)
            {
                // You cannot move around while digging up treasure. You will need to start digging anew.
                m_From.SendLocalizedMessage(503023);
                Terminate();
                return;
            }

            var z = m_Chest?.Z + m_Chest?.ItemData.Height ?? int.MinValue;
            var height = 16;

            if (z > m_Location.Z)
            {
                height -= z - m_Location.Z;
            }
            else
            {
                z = m_Location.Z;
            }

            if (!m_Map.CanFit(m_Location.X, m_Location.Y, z, height, true, true, false))
            {
                // You stop digging because something is directly on top of the treasure chest.
                m_From.SendLocalizedMessage(503024);
                Terminate();
                return;
            }

            m_Count++;

            m_From.RevealingAction();
            m_From.Direction = m_From.GetDirectionTo(m_Location);

            if (m_Count > 1 && m_Dirt1 == null)
            {
                m_Dirt1 = new TreasureChestDirt();
                m_Dirt1.MoveToWorld(m_Location, m_Map);

                m_Dirt2 = new TreasureChestDirt();
                m_Dirt2.MoveToWorld(new Point3D(m_Location.X, m_Location.Y - 1, m_Location.Z), m_Map);
            }

            if (m_Count == 5)
            {
                m_Dirt1.Turn1();
            }
            else if (m_Count == 10)
            {
                m_Dirt1.Turn2();
                m_Dirt2.Turn2();
            }
            else if (m_Count > 10)
            {
                if (m_Chest == null)
                {
                    m_Chest = new TreasureMapChest(m_From, m_TreasureMap.Level, true);
                    m_Chest.MoveToWorld(new Point3D(m_Location.X, m_Location.Y, m_Location.Z - 15), m_Map);
                }
                else
                {
                    m_Chest.Z++;
                }

                Effects.PlaySound(m_Chest.Location, m_Map, 0x33B);
            }

            if (m_Chest?.Location.Z >= m_Location.Z)
            {
                Stop();
                m_From.EndAction<TreasureMap>();

                m_TreasureMap.OnDug(m_From, m_Chest);
            }
            else
            {
                if (m_From.Body.IsHuman && !m_From.Mounted)
                {
                    m_From.Animate(11, 5, 1, true, false, 0);
                }

                new SoundTimer(m_From, 0x125 + m_Count % 2).Start();
            }
        }

        private class SoundTimer : Timer
        {
            private readonly Mobile m_From;
            private readonly int m_SoundID;

            public SoundTimer(Mobile from, int soundID) : base(TimeSpan.FromSeconds(0.9))
            {
                m_From = from;
                m_SoundID = soundID;
            }

            protected override void OnTick()
            {
                m_From.PlaySound(m_SoundID);
            }
        }
    }

    private class DecodeMapEntry : ContextMenuEntry
    {
        public DecodeMapEntry() : base(6147, 2)
        {
        }

        public override void OnClick(Mobile from, IEntity target)
        {
            if (target is TreasureMap { Deleted: false } map)
            {
                map.Decode(from);
            }
        }
    }

    private class OpenMapEntry : ContextMenuEntry
    {
        public OpenMapEntry() : base(6150, 2)
        {
        }

        public override void OnClick(Mobile from, IEntity target)
        {
            if (target is TreasureMap { Deleted: false } map)
            {
                map.DisplayTo(from);
            }
        }
    }

    private class DigEntry : ContextMenuEntry
    {
        public DigEntry(bool enabled) : base(6148, 2) => Enabled = enabled;

        public override void OnClick(Mobile from, IEntity target)
        {
            if (target is not TreasureMap { Deleted: false } map)
            {
                return;
            }

            if (!HasDiggingTool(from))
            {
                from.SendLocalizedMessage(1114416); // You must have a digging tool to dig for treasure.
                return;
            }

            map.OnBeginDig(from);
        }
    }
}

[SerializationGenerator(0)]
public partial class TreasureChestDirt : Item
{
    public TreasureChestDirt() : base(0x912)
    {
        Movable = false;

        Timer.StartTimer(TimeSpan.FromMinutes(2.0), Delete);
    }

    public override bool SkipSerialization => true;

    public void Turn1()
    {
        ItemID = 0x913;
    }

    public void Turn2()
    {
        ItemID = 0x914;
    }
}
