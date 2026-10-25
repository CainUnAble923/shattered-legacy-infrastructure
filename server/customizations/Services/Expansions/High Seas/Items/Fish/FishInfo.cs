// cc-P193: ServUO pub57 Scripts/Services/Expansions/High Seas/Items/Fish/FishInfo.cs -> ours, the High Seas fishing port.
//
// The table of the 16 rare and 18 legendary fish, crabs and lobsters (hue, type, label, where it is caught, deep water or
// not, base chance, minimum Fishing), and the roll that turns a cast or a lobster trap's bob into one of them or into one
// of the common High Seas fish by water (shore, deep, dungeon). ServUO's Fishing.GetResourceType calls GetSpecialItem;
// here Fishing-high-seas-fish.patch adds that override to pinned Engines/Harvest/Fishing.cs.
//
// Changes from ServUO, each in shard-migration notes/cc-P193-port-high-seas-fishing.md:
// - The table is built on first use rather than by a Configure method plus a ten-second timer that resolved the map
//   names (ServUO's InvalidateLocations): Map.Trammel and the others are registered before any fish is rolled, and the
//   test host, which runs no Configure, gets the same table.
// - MagicalFishFinder (the fish finder's "school" that multiplies a rare fish's chance by ten) is not ported, so Roll has
//   no school term.
// - cc-P269: the fishing pole carries bait too now (FishingPole-high-seas-bait-hooks.patch), read here as ServUO does.
// - The chance of a common High Seas fish grows with Fishing as skill / 121.5. The shard caps Fishing at 200, so a player
//   counts at most at OSI's top of 120 here (ClusterFSkillClamps.FishingFindSkill, cc-P67 B, the same clamp the deep
//   water finds use), or every cast past 121.5 would be a High Seas fish.
// - ServUO's SOSArtifacts array is not carried: pinned's own Fishing.Construct fills SOS chests and reads its own list.
// - IsFireIsland's second rectangle (p.Y > 4479 && p.Y < 3746) can never match, as in ServUO; copied as it is (a bug that
//   makes something missing is copied, docs/bug-list.md D-4 rule). No table row uses "fire island".

using System;
using System.Collections.Generic;
using Server.Mobiles;
using Server.Regions;
using Server.Spells;

namespace Server.Items;

public class FishInfo
{
    public static readonly double RareChance = 0.0075;
    public static readonly double LegendaryChance = 0.001;

    private static List<FishInfo> _fishInfos;

    public static List<FishInfo> FishInfos => _fishInfos ??= BuildTable();

    public static void Configure()
    {
        _ = FishInfos;
    }

    private static List<FishInfo> BuildTable()
    {
        var list = new List<FishInfo>
        {
            // Rare fish; 0 - 15
            new(2506, typeof(AutumnDragonfish), 1116090, "Ilshenar", false, RareChance, 105.4),
            new(2591, typeof(BlueLobster), 1116366, "Ice", false, RareChance, 103.1),
            new(1157, typeof(BullFish), 1116095, "Labyrinth", false, RareChance, 105.4),
            new(1167, typeof(CrystalFish), 1116092, "Prism of Light", false, RareChance, 105.4),
            new(2578, typeof(FairySalmon), 1116089, "TerMur", false, RareChance, 85.8),
            new(1461, typeof(FireFish), 1116093, "Shame", false, RareChance, 95.8),
            new(1257, typeof(GiantKoi), 1116088, "Tokuno", true, RareChance, 95.8),
            new(2579, typeof(GreatBarracuda), 1116100, "Felucca", true, RareChance, 89.0),
            new(2959, typeof(HolyMackerel), 1116087, "Gravewater Lake", false, RareChance, 102.9),
            new(2075, typeof(LavaFish), 1116096, "Abyss", false, RareChance, 110.0),
            new(2075, typeof(ReaperFish), 1116094, "Doom", false, RareChance, 98.1),
            new(2539, typeof(SpiderCrab), 1116367, "Terathan Keep", false, RareChance, 103.1),
            new(2558, typeof(StoneCrab), 1116365, "T2A", true, RareChance, 103.1),
            new(43, typeof(SummerDragonfish), 1116091, "Destard", false, RareChance, 105.2),
            new(1154, typeof(UnicornFish), 1116086, "Twisted Weald", false, RareChance, 110.0),
            new(2515, typeof(YellowtailBarracuda), 1116098, "Trammel", true, RareChance, 81.9),

            // Legendary fish; 16 - 34
            new(2406, typeof(AbyssalDragonfish), 1116118, "Destard", false, LegendaryChance, 120.0),
            new(2500, typeof(BlackMarlin), 1116099, "Felucca", true, LegendaryChance, 110.1),
            new(2117, typeof(BloodLobster), 1116370, "Shame", false, LegendaryChance, 115.1),
            new(0, typeof(BlueMarlin), 1116097, "Trammel", true, LegendaryChance, 105.0),
            new(1777, typeof(DreadLobster), 1116371, "Doom", false, LegendaryChance, 115.1),
            new(1158, typeof(DungeonPike), 1116107, "Terathan Keep", false, LegendaryChance, 105.1),
            new(2576, typeof(GiantSamuraiFish), 1116103, "Tokuno", true, LegendaryChance, 110.1),
            new(1281, typeof(GoldenTuna), 1116102, "Tokuno", true, LegendaryChance, 105.0),
            new(2604, typeof(Kingfish), 1116085, "TrammelAndFelucca", true, LegendaryChance, 92.2),
            new(1463, typeof(LanternFish), 1116106, "Prism of Light", false, LegendaryChance, 105.1),
            new(1283, typeof(RainbowFish), 1116108, "Twisted Weald", false, LegendaryChance, 105.1),
            new(2076, typeof(SeekerFish), 1116109, "Labyrinth", false, LegendaryChance, 105.1),
            new(0, typeof(SpringDragonfish), 1116104, "Ilshenar", false, LegendaryChance, 120.0),
            new(1837, typeof(StoneFish), 1116110, "T2A", true, LegendaryChance, 115.1),
            new(2600, typeof(TunnelCrab), 1116372, "Underworld", false, LegendaryChance, 115.1),
            new(2069, typeof(VoidCrab), 1116368, "TerMur", false, LegendaryChance, 120.0),
            new(2071, typeof(VoidLobster), 1116369, "Abyss", false, LegendaryChance, 120.0),
            new(2499, typeof(WinterDragonfish), 1116105, "Ice", false, LegendaryChance, 120.0),
            new(2551, typeof(ZombieFish), 1116101, "Gravewater Lake", false, LegendaryChance, 115.1),

            // Cannot be fished up; it is a bait (the charybdis bait, HighSeasFishing.TrySummonCharybdis)
            new(1170, typeof(Charydbis), 1150208, "cannotfishup", true, LegendaryChance, 120.0)
        };

        foreach (var info in list)
        {
            info.Location = info.Location switch
            {
                "Trammel" => Map.Trammel,
                "Felucca" => Map.Felucca,
                "Tokuno" => Map.Tokuno,
                "Ilshenar" => Map.Ilshenar,
                "Malas" => Map.Malas,
                "TerMur" => Map.TerMur,
                _ => info.Location
            };
        }

        return list;
    }

    public FishInfo(int hue, Type type, object label, object found, bool deepWater, double baseChance, double minSkill)
    {
        Hue = hue;
        Type = type;
        LabelNumber = label;
        Location = found;
        RequiresDeepWater = deepWater;
        BaseChance = baseChance;
        MinSkill = minSkill;
    }

    public int Hue { get; }
    public Type Type { get; }
    public object LabelNumber { get; }
    public object Location { get; set; }
    public bool RequiresDeepWater { get; }
    public double BaseChance { get; }
    public double MinSkill { get; }

    public static bool IsRareFish(Type type)
    {
        foreach (var info in FishInfos)
        {
            if (info.Type == type)
            {
                return true;
            }
        }

        return false;
    }

    public static FishInfo GetInfo(Type type)
    {
        foreach (var info in FishInfos)
        {
            if (info.Type == type)
            {
                return info;
            }
        }

        return null;
    }

    public static FishInfo GetInfo(int hue)
    {
        foreach (var info in FishInfos)
        {
            if (info.Hue == hue)
            {
                return info;
            }
        }

        return null;
    }

    public static int GetIndexFromType(Type type)
    {
        if (type == null)
        {
            return -1;
        }

        var infos = FishInfos;

        for (var i = 0; i < infos.Count; i++)
        {
            if (infos[i].Type == type)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>ServUO's shape: an index off the table gives the first row's type, not null.</summary>
    public static Type GetTypeFromIndex(int index)
    {
        var infos = FishInfos;

        if (index < 0 || index >= infos.Count)
        {
            return infos[0].Type;
        }

        return infos[index].Type;
    }

    public static int GetFishHue(int index)
    {
        var infos = FishInfos;

        if (index < 0 || index >= infos.Count)
        {
            return 0;
        }

        return infos[index].Hue;
    }

    public static int GetFishHue(Type type) => GetInfo(type)?.Hue ?? 0;

    public static object GetFishLabel(int index)
    {
        var infos = FishInfos;

        if (index < 0 || index >= infos.Count)
        {
            return 0;
        }

        return infos[index].LabelNumber;
    }

    public static object GetFishLabel(Type type) => GetInfo(type)?.LabelNumber;

    public static string GetFishLocation(Type type) => GetInfo(type)?.Location?.ToString();

    public static Type GetSpecialItem(Mobile from, Item harvestItem, Point3D pnt, bool rareOnly) =>
        GetSpecialItem(from, harvestItem, pnt, 0, rareOnly);

    /// <summary>
    /// The roll a cast (a FishingPole) or a trap's bob (a LobsterTrap) makes for a High Seas catch: first every rare and
    /// legendary row the water, the skill and the tool kind allow, baited rows first; then, unless rareOnly, a common
    /// High Seas fish of the water (dungeon from 106 Fishing, deep from 80, shore from 50) or, for a trap, a crab or
    /// lobster from 50. Null means the stock catch.
    /// </summary>
    public static Type GetSpecialItem(Mobile from, Item harvestItem, Point3D pnt, double bump, bool rareOnly)
    {
        var map = from.Map;
        var fromLoc = from.Location;
        var reg = from.Region;

        if (reg?.Parent != null)
        {
            reg = reg.Parent;
        }

        var skill = from.Skills[SkillName.Fishing].Value;
        var fishing = harvestItem is FishingPole;
        Type bait = null;
        Type item = null;
        var enhanced = false;

        if (harvestItem is FishingPole pole)
        {
            bait = pole.BaitType;
            enhanced = pole.EnhancedBait;
        }
        else if (harvestItem is LobsterTrap trap)
        {
            bait = trap.BaitType;
            enhanced = trap.EnhancedBait;
        }

        // The baited type goes first, so it is the row most likely to be rolled.
        var infos = new List<FishInfo>(FishInfos);

        if (bait != null)
        {
            for (var i = 0; i < infos.Count; i++)
            {
                var info = infos[i];

                if (info.Type == bait)
                {
                    infos.RemoveAt(i);
                    infos.Insert(0, info);
                    break;
                }
            }
        }

        for (var i = 0; i < infos.Count; i++)
        {
            var info = infos[i];
            var baitStr = info.GetBaitStrength(bait, from, enhanced);

            if (info.RequiresDeepWater && !IsDeepWater(pnt, map, reg) || skill < info.MinSkill)
            {
                continue;
            }

            if (fishing && info.Type.IsSubclassOf(typeof(BaseCrabAndLobster)))
            {
                continue;
            }

            if (!fishing && !info.Type.IsSubclassOf(typeof(BaseCrabAndLobster)))
            {
                continue;
            }

            if (info.Location is string loc)
            {
                var lower = loc.ToLowerInvariant();

                if (lower == "cannotfishup")
                {
                    continue;
                }

                if (lower == "t2a" && SpellHelper.IsAnyT2A(map, fromLoc) && info.Roll(from, baitStr, bump))
                {
                    item = info.Type;
                }

                if (lower == "trammelandfelucca" && (map == Map.Trammel || map == Map.Felucca) &&
                    !SpellHelper.IsAnyT2A(map, fromLoc) && info.Roll(from, baitStr, bump))
                {
                    item = info.Type;
                }

                if (lower == "fire island" && IsFireIsland(fromLoc, map) && info.Roll(from, baitStr, bump))
                {
                    item = info.Type;
                }

                if (lower == "gravewater lake" && IsGravewaterLake(fromLoc, map))
                {
                    item = info.Type;
                }

                if (from.Region?.IsPartOf(loc) == true && info.Roll(from, baitStr, bump))
                {
                    item = info.Type;
                }
            }
            else if (info.Location is Map locMap)
            {
                if (map == locMap && info.Roll(from, baitStr, bump))
                {
                    item = info.Type;
                }
            }
        }

        if (!rareOnly && item == null && from is PlayerMobile)
        {
            var chance = ClusterFSkillClamps.FishingFindSkill(from, skill, true) / 121.5;
            var dungeon = IsDungeon(pnt, map, reg);
            var shore = IsShore(pnt, map, reg);
            var deep = IsDeepWater(pnt, map, reg);

            if (fishing && chance >= Utility.RandomDouble())
            {
                if (dungeon && skill >= 106.0)
                {
                    item = BaseHighseasFish.DungeonFish.RandomElement();
                }
                else if (deep && skill >= 80.0)
                {
                    item = BaseHighseasFish.DeepWaterFish.RandomElement();
                }
                else if (shore && skill >= 50.0)
                {
                    item = BaseHighseasFish.ShoreFish.RandomElement();
                }
            }
            else if (skill >= 50.0 && !fishing && chance >= Utility.RandomDouble())
            {
                item = BaseHighseasFish.LobstersAndCrabs.RandomElement();
            }
        }

        return item;
    }

    /// <summary>
    /// 1.0 unbaited or baited for another fish; 2.0 (3.0 enhanced) for this fish, plus every worn piece of fishing
    /// attire's bait bonus, a full set paying its set bonus split across its pieces.
    /// </summary>
    public double GetBaitStrength(Type baitType, Mobile from, bool enhanced)
    {
        if (baitType != Type)
        {
            return 1.0;
        }

        var str = enhanced ? 3.0 : 2.0;

        for (var i = 0; i < from.Items.Count; i++)
        {
            var item = from.Items[i];

            if (item is IFishingAttire attire)
            {
                if (item is ISetItem set)
                {
                    if (set.SetEquipped)
                    {
                        str += attire.SetBonus / 100.0 / set.Pieces;
                    }
                    else
                    {
                        str += attire.BaitBonus / 100.0;
                    }
                }
                else
                {
                    str += attire.BaitBonus / 100.0;
                }
            }
        }

        return str;
    }

    public bool Roll(Mobile from, double baitStr, double bump) => (BaseChance + bump) * baitStr > Utility.RandomDouble();

    public static bool IsDeepWater(Point3D pnt, Map map, Region region) =>
        SpecialFishingNet.FullValidation(map, pnt.X, pnt.Y) && !IsDungeon(pnt, map, region);

    public static bool IsShore(Point3D pnt, Map map, Region region) =>
        !IsDeepWater(pnt, map, region) && !IsDungeon(pnt, map, region);

    public static bool IsDungeon(Point3D pnt, Map map, Region region) =>
        region != null && (region.IsPartOf<DungeonRegion>() || IsMondainDungeon(region)) ||
        SpellHelper.IsTrammelWind(map, pnt) || SpellHelper.IsFeluccaWind(map, pnt);

    public static bool IsMondainDungeon(Region region) =>
        region != null && (
            region.IsPartOf("Twisted Weald") || region.IsPartOf("Sanctuary") || region.IsPartOf("Prism of Light") ||
            region.IsPartOf("Citadel") || region.IsPartOf("Bedlam") || region.IsPartOf("Blighted Grove") ||
            region.IsPartOf("Painted Caves") || region.IsPartOf("Palace of Paroxysmus") || region.IsPartOf("Labyrinth"));

    public static bool IsFireIsland(Point3D p, Map map) =>
        (map == Map.Felucca || map == Map.Trammel) &&
        (p.X > 4559 && p.X < 4636 && p.Y > 3548 && p.Y < 3627 || p.X > 4465 && p.X < 4493 && p.Y > 4479 && p.Y < 3746);

    public static bool IsGravewaterLake(Point3D p, Map map) =>
        map == Map.Malas &&
        (p.X >= 1440 && p.X <= 1863 && p.Y >= 1527 && p.Y <= 1746 || p.X >= 1381 && p.X <= 1596 && p.Y >= 1565 && p.Y <= 1789);
}
