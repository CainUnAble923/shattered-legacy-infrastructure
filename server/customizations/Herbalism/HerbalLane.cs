// HerbalLane.cs
//
// cc-P804 (F-51, phase 1): the reagent lane on pinned's plant engine. Design: D:\UO\cc-prompts\design\f-herbalism-reagents.md. Notes:
// shard-migration notes/cc-P804-herbalism-phase-1.md. The engine calls here from Plants-herbalism-lane.patch; stock decorative
// plants pass through every hook untouched.
//
// THE LANE. Five PlantType rows appended after the stock 43 (Garlic to BloodMoss). They cross only each other: a cross with a stock
// plant is refused, and a reagent cross averages along the lane (P260 For Chase 1, P392 U47).
//
// STRAIN TIER is its own value, not PlantHue, because the dye and basket tables key on PlantHue (P392 U48). Tier 0 to 16 runs up the
// League's metal ladder, Iron to Celestial, and the grown plant shows that metal's hue. It lives in HerbalStore beside the plant, keyed
// by the plant's serial, so no engine type changes its save (D40). ReagentSeed carries it on a seed.
//
// YIELD is perennial (U49): a mature tended reagent plant makes herbs every growth day and never runs out; stock plants keep OSI's
// eight. A bed mound is the same plant in a RaisedGardenPlantItem. All the mounds of a bed are one unit: watering, potions and the
// harvest of one reach them all.
//
// NEGLECT. Tending stamps the plant with the tender's grace (base days plus Herbalism). Inside it, a reagent plant takes no OSI damage.
// Past it, health falls a little each growth check, to 1 hit and never to death. Every further grace length past that drops one strain
// tier. Maladies do not hurt inside the lane; they cut the harvest, so the bowl's full OSI potion loop still pays.
//
// Phase 2 bolts on at GrowthDelay (kit and Herbalism speed), MayTendFrom (items that extend grace) and HerbalRecord's version.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Server.Accounting;
using Server.Items;
using Server.Mobiles;

namespace Server.Engines.Plants;

/// <summary>What the lane keeps beside one plant.</summary>
public sealed class HerbalRecord
{
    public int Tier;
    public int SeedTier = -1;       // the strain its seeds will be; -1 is the plant's own
    public DateTime LastTended;
    public double TenderSkill;      // the skill of whoever last tended it: its grace and yield
    public int TierDrops;           // windows already charged since the last tending

    public HerbalRecord()
    {
    }

    public HerbalRecord(IGenericReader r)
    {
        Tier = r.ReadEncodedInt();
        SeedTier = r.ReadEncodedInt() - 1;
        LastTended = r.ReadDateTime();
        TenderSkill = r.ReadDouble();
        TierDrops = r.ReadEncodedInt();
    }

    public void Serialize(IGenericWriter w)
    {
        w.WriteEncodedInt(Tier);
        w.WriteEncodedInt(SeedTier + 1);
        w.Write(LastTended);
        w.Write(TenderSkill);
        w.WriteEncodedInt(TierDrops);
    }
}

/// <summary>The lane's records, saved through pinned's GenericPersistence; versioned, an unknown version fails loudly (D40).</summary>
public class HerbalStore : GenericPersistence
{
    public const int CurrentVersion = 0;

    private static HerbalStore _instance;

    internal static readonly Dictionary<Serial, HerbalRecord> Rows = new();

    public HerbalStore() : base("Herbalism", 10)
    {
    }

    public static void Configure() => _instance ??= new HerbalStore();

    public override void Serialize(IGenericWriter writer)
    {
        var live = new List<KeyValuePair<Serial, HerbalRecord>>();
        var dead = new List<Serial>();

        foreach (var pair in Rows)
        {
            if (World.FindItem(pair.Key) is PlantItem { Deleted: false })
            {
                live.Add(pair);
            }
            else
            {
                dead.Add(pair.Key);
            }
        }

        foreach (var serial in dead)
        {
            Rows.Remove(serial);
        }

        writer.WriteEncodedInt(CurrentVersion);
        writer.WriteEncodedInt(live.Count);

        foreach (var (serial, record) in live)
        {
            writer.Write(serial);
            record.Serialize(writer);
        }
    }

    public override void Deserialize(IGenericReader reader)
    {
        var version = reader.ReadEncodedInt();

        if (version is < 0 or > CurrentVersion)
        {
            throw new InvalidDataException($"HerbalStore version {version} is not one this build can read.");
        }

        Rows.Clear();
        var count = reader.ReadEncodedInt();

        for (var i = 0; i < count; i++)
        {
            var serial = reader.ReadSerial();
            Rows[serial] = new HerbalRecord(reader);
        }
    }
}

public static class HerbalLane
{
    public const int TopStrain = 16; // Iron, rank 1, is tier 0; Celestial, rank 17, is tier 16
    public const string FirstHarvestAchievement = "herbalism.green_thumb";

    private static HerbalismSettings S => HerbalismSettings.Current;

    public static bool IsReagent(PlantType type) => type >= PlantType.Garlic;

    public static bool IsReagent(PlantItem plant) => IsReagent(plant.PlantType) && plant.PlantStatus is > PlantStatus.BowlOfDirt and < PlantStatus.DecorativePlant;

    public static HerbRow HerbOf(PlantType type) => S.Herb(type.ToString());

    public static double Skill(Mobile m) => m?.Skills[SkillName.Herbalism].Base ?? 0;

    public static HerbalRecord Record(PlantItem plant)
    {
        if (!HerbalStore.Rows.TryGetValue(plant.Serial, out var record))
        {
            HerbalStore.Rows[plant.Serial] = record = new HerbalRecord { LastTended = Core.Now };
        }

        return record;
    }

    // -- The strain ladder ----------------------------------------------------------------------------

    public static string StrainName(int tier) => ClusterFLeagueRanks.GetRankInfo(Math.Clamp(tier, 0, TopStrain) + 1)?.MetalName ?? "Iron";

    public static int StrainHue(int tier) => ClusterFLeagueRanks.GetRankInfo(Math.Clamp(tier, 0, TopStrain) + 1)?.Hue ?? 0;

    /// <summary>The highest strain this skill can breed up to.</summary>
    public static int StrainCap(double skill) => Math.Clamp((int)(skill / 200.0 * S.TopStrainAt200), 0, TopStrain);

    public static int PlantHueFor(PlantItem plant, int stockHue) =>
        IsReagent(plant.PlantType) && plant.PlantStatus < PlantStatus.DeadTwigs ? StrainHue(Record(plant).Tier) : stockHue;

    private static void SetTier(PlantItem plant, int tier)
    {
        Record(plant).Tier = Math.Clamp(tier, 0, TopStrain);

        if (plant.PlantStatus >= PlantStatus.FullGrownPlant && plant.PlantStatus < PlantStatus.DeadTwigs)
        {
            plant.Hue = StrainHue(tier);
        }

        plant.InvalidateProperties();
    }

    public static void AddProperties(PlantItem plant, IPropertyList list)
    {
        if (IsReagent(plant))
        {
            list.Add($"{StrainName(Record(plant).Tier)} strain");
        }
    }

    // -- Alembic gates --------------------------------------------------------------------------------

    public static int AlembicRank(Mobile m) =>
        m is PlayerMobile pm ? ClusterFGuildSystem.GuildRankIndex(ClusterFAlembicGuild.Key, ClusterFAccountPersistence.GetOrCreateGuild(pm)) : 0;

    public static int RankNeeded(PlantType type, int tier) => Math.Max(HerbOf(type)?.AlembicRank ?? 0, tier / S.StrainTiersPerAlembicRank);

    /// <summary>Recipe gating: the type and the strain each need an Alembic rank. Says why when it refuses.</summary>
    public static bool MayPlant(Mobile from, Seed seed)
    {
        if (!IsReagent(seed.PlantType))
        {
            return true;
        }

        var need = RankNeeded(seed.PlantType, (seed as ReagentSeed)?.StrainTier ?? 0);

        if (AlembicRank(from) >= need)
        {
            return true;
        }

        from.SendMessage(0x22, "The Order of the Alembic has not taught you this strain. You need a higher rank in the Order.");
        return false;
    }

    public static Seed NewSeed(PlantItem plant, PlantSystem system)
    {
        if (!IsReagent(system.SeedType))
        {
            return new Seed(system.SeedType, system.SeedHue, true);
        }

        var record = Record(plant);
        return new ReagentSeed(system.SeedType, record.SeedTier >= 0 ? record.SeedTier : record.Tier);
    }

    public static void OnPlanted(Mobile from, PlantItem plant, int tier)
    {
        if (!IsReagent(plant.PlantType))
        {
            return;
        }

        HerbalStore.Rows.Remove(plant.Serial);
        SetTier(plant, tier);

        if (plant is RaisedGardenPlantItem)
        {
            plant.PlantSystem.Water = 2; // a mound starts soft
        }

        Tended(from, plant);
        Gain(from, plant.PlantType, tier);
    }

    // -- Crossing -------------------------------------------------------------------------------------

    public static PlantType Cross(PlantType first, PlantType second)
    {
        if (!IsReagent(first) || !IsReagent(second))
        {
            return first;
        }

        var a = (int)first;
        var b = (int)second;

        return Math.Abs(a - b) == 1 ? Utility.RandomBool() ? first : second : (PlantType)((a + b) / 2);
    }

    public static bool RefusesPollen(PlantItem source, PlantItem target) => IsReagent(source.PlantType) != IsReagent(target.PlantType);

    public static double MutationChance(double skill) => (S.MutationPercentBase + S.MutationPercentAt200 * skill / 200.0) / 100.0;

    /// <summary>
    /// Sets the seeds' strain: the better parent's, and a same-strain cross of two plants can mutate up one tier, up to what the
    /// breeder's skill can reach.
    /// </summary>
    public static bool OnPollinated(Mobile from, PlantItem source, PlantItem target, double? roll = null)
    {
        if (!IsReagent(target.PlantType))
        {
            return false;
        }

        var a = Record(source).Tier;
        var b = Record(target).Tier;
        var tier = Math.Max(a, b);
        var mutated = false;

        if (source != target && a == b && tier + 1 <= StrainCap(Skill(from)) && (roll ?? Utility.RandomDouble()) < MutationChance(Skill(from)))
        {
            tier++;
            mutated = true;
        }

        Record(target).SeedTier = tier;
        Tended(from, target);
        Gain(from, target.PlantType, tier);

        if (mutated)
        {
            for (var i = 0; i < S.TierJumpGainRolls; i++)
            {
                Gain(from, target.PlantType, tier);
            }

            from.SendMessage(0x44, $"The cross takes! Its seeds will be the {StrainName(tier)} strain.");
        }

        return mutated;
    }

    // -- Gains ----------------------------------------------------------------------------------------

    public static (double Min, double Max) GainWindow(HerbRow herb, int strain)
    {
        var low = Math.Min(((herb?.Tier ?? 1) - 1) * 50.0 + strain * S.GainPerStrainTier, 200.0 - S.GainWindow);
        return (low, low + S.GainWindow);
    }

    public static void Gain(Mobile from, PlantType type, int strain)
    {
        var (min, max) = GainWindow(HerbOf(type), strain);
        from?.CheckSkill(SkillName.Herbalism, min, max);
    }

    // -- Tending and beds -----------------------------------------------------------------------------

    public static List<PlantItem> BedOf(PlantItem plant)
    {
        var bed = new List<PlantItem> { plant };

        if (plant is RaisedGardenPlantItem { Component.Addon: { } addon })
        {
            foreach (var part in addon.Components)
            {
                if (part is GardenAddonComponent { Plant: { } other } && other != plant && IsReagent(other))
                {
                    bed.Add(other);
                }
            }
        }

        return bed;
    }

    public static void Tended(Mobile from, PlantItem plant)
    {
        if (!IsReagent(plant))
        {
            return;
        }

        var system = plant.PlantSystem;

        foreach (var p in BedOf(plant))
        {
            var record = Record(p);
            record.LastTended = Core.Now;
            record.TierDrops = 0;
            record.TenderSkill = Skill(from);

            if (p != plant)
            {
                p.PlantSystem.Water = system.Water;
                p.PlantSystem.PoisonPotion = system.PoisonPotion;
                p.PlantSystem.CurePotion = system.CurePotion;
                p.PlantSystem.HealPotion = system.HealPotion;
                p.PlantSystem.StrengthPotion = system.StrengthPotion;
            }
        }
    }

    // -- Growth (PlantSystem) -------------------------------------------------------------------------

    /// <summary>Phase 2's speed goes here. Bowls stay at OSI's pace for good.</summary>
    public static TimeSpan GrowthDelay(PlantSystem system) => PlantSystem.CheckDelay;

    public static double GraceDays(double skill) => S.GraceBaseDays + S.GraceExtraDaysAt200 * skill / 200.0;

    public static bool SparesDamage(PlantSystem system) => IsReagent(system.Plant);

    public static bool SkipsMaladies(PlantSystem system, double? roll = null) =>
        IsReagent(system.Plant) && system.Plant is RaisedGardenPlantItem && (roll ?? Utility.RandomDouble()) * 100 >= S.BedMaladyPercent;

    public static void OnGrowthCheck(PlantSystem system)
    {
        var plant = system.Plant;

        if (!IsReagent(plant))
        {
            return;
        }

        var record = Record(plant);
        var grace = GraceDays(record.TenderSkill);
        var past = (Core.Now - record.LastTended).TotalDays - grace;

        if (past <= 0)
        {
            return;
        }

        system.Hits = Math.Max(1, system.Hits - S.NeglectHitsPerCheck);

        var windows = grace > 0 ? (int)(past / grace) : 0;

        if (windows > record.TierDrops)
        {
            SetTier(plant, record.Tier - (windows - record.TierDrops));
            record.TierDrops = windows;
        }
    }

    public static int MaladyCount(PlantSystem system) => system.Infestation + system.Fungus + system.Poison + system.Disease;

    public static int DailyYield(PlantItem plant)
    {
        var record = Record(plant);
        var system = plant.PlantSystem;
        double herbs = plant is RaisedGardenPlantItem ? S.BedHerbsPerDay : S.BowlHerbsPerDay;

        herbs += record.Tier * S.HerbsPerStrainTier;
        herbs *= 1 + S.YieldBonusPercentAt200 / 100.0 * record.TenderSkill / 200.0;
        herbs *= Math.Max(0, 1 - MaladyCount(system) * S.MaladyYieldLossPercent / 100.0);

        return (int)herbs;
    }

    /// <summary>A mature plant's day: perennial for the lane. True when the lane handled it.</summary>
    public static bool OnMature(PlantSystem system)
    {
        var plant = system.Plant;

        if (!IsReagent(plant))
        {
            return false;
        }

        var day = DailyYield(plant);
        system.AvailableResources = Math.Min(system.AvailableResources + day, day * S.MaxStoredDays);
        return true;
    }

    /// <summary>The Gather button for the lane: the whole bed at once. False for a stock plant.</summary>
    public static bool TryHarvest(Mobile from, PlantItem plant)
    {
        if (!IsReagent(plant))
        {
            return false;
        }

        var bed = BedOf(plant);
        var total = 0;

        foreach (var p in bed)
        {
            total += p.PlantSystem.AvailableResources;
        }

        if (total <= 0)
        {
            plant.LabelTo(from, 1053056); // This plant has no resources to gather!
            return true;
        }

        var herb = HerbOf(plant.PlantType);
        var raw = new RawHerb(herb.Key, total);

        if (!from.PlaceInBackpack(raw))
        {
            raw.Delete();
            plant.LabelTo(from, 1053058); // You attempt to gather as many resources as you can hold, but your backpack is full.
            return true;
        }

        foreach (var p in bed)
        {
            p.PlantSystem.AvailableResources = 0;
        }

        plant.LabelTo(from, 1053059); // You gather resources from the plant.
        Tended(from, plant);
        Gain(from, plant.PlantType, Record(plant).Tier);
        LogFind(from, herb.Key, total);

        if (from.Account is IAccount acct)
        {
            ClusterFAchievementSystem.TryGrant(acct, FirstHarvestAchievement);
        }

        return true;
    }

    // -- The ledger -----------------------------------------------------------------------------------

    private static string LedgerKey(Mobile m, string herb) => $"character:{(uint)m.Serial}:herbs:{herb}";

    public static int Found(Mobile m, string herb) =>
        m.Account is IAccount acct && ClusterFAccountPersistence.Get(acct) is { } data &&
        data.FlagValues.TryGetValue(LedgerKey(m, herb), out var text) &&
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : 0;

    public static void LogFind(Mobile m, string herb, int amount)
    {
        if (m.Account is IAccount acct)
        {
            var total = Found(m, herb) + amount;
            ClusterFAccountPersistence.GetOrCreate(acct).FlagValues[LedgerKey(m, herb)] = total.ToString(CultureInfo.InvariantCulture);
        }
    }

    // -- Processing -----------------------------------------------------------------------------------

    public static int ReagentsInBundle(double skill) =>
        (int)(S.ReagentsPerBundle * (1 + S.BundleBonusPercentAt200 / 100.0 * skill / 200.0));

    public static int SkillFloor(HerbRow herb) => (herb.Tier - 1) * 50;
}
