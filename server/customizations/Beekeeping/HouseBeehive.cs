// cc-P787: the beehive, a house item like the OSI plant bowl. A beehive deed places it in its owner's house as one
// lockdown (at most maxHivesPerHouse a house); a bee swarm starts a colony in it; a daily check moves the colony along
// (Configuration/beekeeping.json, BeekeepingTable):
//
//   stages      Empty, then Colonizing, Brooding and Producing (colonizeDays, broodDays)
//   needs       flowers (blooming plant bowls, flower art) and water (troughs, tubs, barrels, filled water drinks, a
//               wet tile) within range tiles; each reads too low, fine or (water) too much
//   ailments    parasites (a Poison potion) and disease (a Cure potion), rolled each day
//   potions     Heal and Greater Heal mend health; Strength makes the colony hardy for a day (half the losses and
//               ailment chances); Agility widens the range for a day. The plant system's potions, poured the same way.
//   health      rises with no problem, falls per problem; the population grows when healthy and shrinks when weak. At
//               no bees the colony leaves and the hive is empty again (its honey and wax stay to harvest).
//   products    a producing colony makes honey and raw beeswax each day, harvested with a hive tool.
//
// A hive only lives while it is locked down in a house: a released hive keeps its state and waits. The next check is
// saved (NextCheck), and one shared sweep (BeekeepingSystem) runs every sweepMinutes, so a restart neither skips nor
// doubles a day; a check that is days late still counts one day, as plants do.
//
// The bees (0x91B over the hive) are a separate fixed item while a colony lives in it, deleted with the hive.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using ModernUO.Serialization;
using Server.Gumps;
using Server.Items;
using Server.Multis;
using Server.Targeting;

namespace Server.Engines.Beekeeping;

public enum HiveStage
{
    Empty,
    Colonizing,
    Brooding,
    Producing
}

public enum HiveNeed
{
    TooLow,
    Fine,
    TooMuch
}

[SerializationGenerator(0, false)]
public partial class HouseBeehive : Item
{
    public const int HiveArt = 0x91A;

    private static readonly HashSet<HouseBeehive> _all = [];

    /// <summary>Every live hive, for the shared sweep.</summary>
    public static IReadOnlyCollection<HouseBeehive> All => _all;

    /// <summary>The daily rolls (0 to n-1); facts replace it to force or forbid an ailment.</summary>
    public static Func<int, int> Roll { get; set; } = Utility.Random;

    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private HiveStage _stage;

    [SerializableField(1)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private int _age;

    [SerializableField(2)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private int _population;

    [SerializableField(3)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private int _health;

    [SerializableField(4)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private bool _parasites;

    [SerializableField(5)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private bool _disease;

    [SerializableField(6)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private int _honey;

    [SerializableField(7)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private int _wax;

    [SerializableField(8)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private DateTime _nextCheck;

    [SerializableField(9)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private DateTime _strengthUntil;

    [SerializableField(10)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private DateTime _agilityUntil;

    [SerializableField(11, setter: "private")]
    private Item? _bees;

    [Constructible]
    public HouseBeehive() : base(HiveArt)
    {
        _all.Add(this);
    }

    public override double DefaultWeight => 10.0;

    public override string DefaultName => "a beehive";

    private static BeekeepingTable T => BeekeepingTable.Current;

    public bool Lived => Stage != HiveStage.Empty;

    public bool Hardy(DateTime now) => now < StrengthUntil;

    public int RangeAt(DateTime now) => T.Range + (now < AgilityUntil ? T.AgilityRange : 0);

    [AfterDeserialization]
    private void AfterDeserialization()
    {
        _all.Add(this);
    }

    public override void OnDelete()
    {
        _all.Remove(this);
        _bees?.Delete();
        base.OnDelete();
    }

    public override void OnLocationChange(Point3D oldLocation)
    {
        base.OnLocationChange(oldLocation);
        UpdateBees();
    }

    public override void OnAdded(IEntity parent)
    {
        base.OnAdded(parent);
        UpdateBees();
    }

    public override void OnMapChange()
    {
        base.OnMapChange();
        UpdateBees();
    }

    /// <summary>The bees show over a lived-in hive in the world, and nowhere else.</summary>
    public void UpdateBees()
    {
        var show = Lived && Parent == null && Map != null && Map != Map.Internal;
        if (!show)
        {
            if (_bees != null)
            {
                _bees.Delete();
                Bees = null;
            }

            return;
        }

        if (_bees == null || _bees.Deleted)
        {
            Bees = new HiveBees();
        }

        _bees!.MoveToWorld(Location, Map);
    }

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add(Lived ? $"{Stage} colony, {Population} bees, health {Health}" : "No colony");
        if (Honey > 0 || Wax > 0)
        {
            list.Add($"Honey {Honey}, raw wax {Wax}");
        }
    }

    // -- Access ---------------------------------------------------------------------------------------

    public BaseHouse? House => BaseHouse.FindHouseAt(this);

    /// <summary>Locked down in a house: only then does the colony live its days.</summary>
    public bool IsLockedInHouse => Parent == null && House?.HasLockedDownItem(this) == true;

    /// <summary>The house's owner and co-owners tend it (and staff).</summary>
    public bool CanTend(Mobile from) => from.AccessLevel >= AccessLevel.GameMaster || House?.IsCoOwner(from) == true;

    /// <summary>Friends of the house may look.</summary>
    public bool CanView(Mobile from) => from.AccessLevel >= AccessLevel.GameMaster || House?.IsFriend(from) == true;

    public override void OnDoubleClick(Mobile from)
    {
        if (!from.InRange(GetWorldLocation(), 3))
        {
            from.SendLocalizedMessage(500446); // That is too far away.
            return;
        }

        if (!CanView(from))
        {
            from.SendMessage("Only the house's owners and friends may look into this hive.");
            return;
        }

        from.SendGump(new BeehiveGump(from, this));
    }

    // -- Reading the surroundings -----------------------------------------------------------------------

    public static bool IsFlower(Item item, BeekeepingTable t) =>
        item switch
        {
            Plants.PlantItem p => p.PlantStatus >= Plants.PlantStatus.Plant && p.PlantStatus <= Plants.PlantStatus.DecorativePlant,
            _                  => t.FlowerItemIds.Contains(item.ItemID)
        };

    public static bool IsWater(Item item, BeekeepingTable t) =>
        item switch
        {
            BaseBeverage b => !b.IsEmpty && b.Content == BeverageType.Water,
            _              => t.WaterItemIds.Contains(item.ItemID)
        };

    /// <summary>Flowers within range: blooming plants and flower art, on the ground or in the world nearby.</summary>
    public int CountFlowers(DateTime now)
    {
        var map = Map;
        if (map == null || map == Map.Internal)
        {
            return 0;
        }

        var t = T;
        var n = 0;
        foreach (var item in map.GetItemsInRange(GetWorldLocation(), RangeAt(now)))
        {
            if (item != this && IsFlower(item, t))
            {
                n++;
            }
        }

        return n;
    }

    /// <summary>Water within range: water items and filled water drinks, and a wet tile (counted once).</summary>
    public int CountWater(DateTime now)
    {
        var map = Map;
        if (map == null || map == Map.Internal)
        {
            return 0;
        }

        var t = T;
        var n = 0;
        var range = RangeAt(now);
        var at = GetWorldLocation();
        foreach (var item in map.GetItemsInRange(at, range))
        {
            if (IsWater(item, t))
            {
                n++;
            }
        }

        if (t.WetTileWater > 0 && HasWetTile(map, at, range))
        {
            n += t.WetTileWater;
        }

        return n;
    }

    private static bool HasWetTile(Map map, Point3D at, int range)
    {
        for (var x = at.X - range; x <= at.X + range; x++)
        {
            for (var y = at.Y - range; y <= at.Y + range; y++)
            {
                var land = map.Tiles.GetLandTile(x, y);
                if (land.ID < TileData.LandTable.Length && (TileData.LandTable[land.ID].Flags & TileFlag.Wet) != 0)
                {
                    return true;
                }

                foreach (var tile in map.Tiles.GetStaticAndMultiTiles(x, y))
                {
                    if (tile.ID < TileData.ItemTable.Length && (TileData.ItemTable[tile.ID].Flags & TileFlag.Wet) != 0)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    public static HiveNeed FlowerNeed(int flowers, int population, BeekeepingTable t) =>
        flowers < t.FlowersNeeded(population) ? HiveNeed.TooLow : HiveNeed.Fine;

    public static HiveNeed WaterNeed(int water, int population, BeekeepingTable t)
    {
        var need = t.WaterNeeded(population);
        if (water < need)
        {
            return HiveNeed.TooLow;
        }

        return need > 0 && water > need * t.TooMuchWaterFactor ? HiveNeed.TooMuch : HiveNeed.Fine;
    }

    // -- The daily check ----------------------------------------------------------------------------------

    /// <summary>The sweep's entry: one day if due and the hive is locked down in a house. True when a day passed.</summary>
    public bool CheckDue(DateTime now)
    {
        var t = T;
        if (!t.Enabled || Deleted || now < NextCheck)
        {
            return false;
        }

        NextCheck = now + TimeSpan.FromHours(t.CheckHours);
        if (!Lived || !IsLockedInHouse)
        {
            return false;
        }

        Day(now, CountFlowers(now), CountWater(now));
        return true;
    }

    /// <summary>One day of the colony, given what is around it.</summary>
    public void Day(DateTime now, int flowers, int water)
    {
        if (!Lived)
        {
            return;
        }

        var t = T;
        var hardy = Hardy(now);

        if (!Parasites && Roll(100) < (hardy ? t.ParasiteChance / 2 : t.ParasiteChance))
        {
            Parasites = true;
        }

        if (!Disease && Roll(100) < (hardy ? t.DiseaseChance / 2 : t.DiseaseChance))
        {
            Disease = true;
        }

        var problems = 0;
        if (FlowerNeed(flowers, Population, t) != HiveNeed.Fine)
        {
            problems++;
        }

        if (WaterNeed(water, Population, t) != HiveNeed.Fine)
        {
            problems++;
        }

        if (Parasites)
        {
            problems++;
        }

        if (Disease)
        {
            problems++;
        }

        if (problems == 0)
        {
            Health = Math.Min(100, Health + t.HealthGain);
        }
        else
        {
            var loss = t.HealthLossPerProblem * problems;
            Health = Math.Max(0, Health - (hardy ? loss / 2 : loss));
        }

        Age++;
        if (problems == 0 && Health >= t.GrowHealth)
        {
            Population = Math.Min(t.MaxPopulation, Population + 1);
        }
        else if (Health < t.ShrinkHealth)
        {
            Population = Math.Max(0, Population - 1);
        }

        if (Population <= 0)
        {
            Leave();
            return;
        }

        if (Stage == HiveStage.Colonizing && Age >= t.ColonizeDays)
        {
            Stage = HiveStage.Brooding;
        }

        if (Stage == HiveStage.Brooding && Age >= t.ColonizeDays + t.BroodDays)
        {
            Stage = HiveStage.Producing;
        }
        else if (Stage == HiveStage.Producing)
        {
            Honey = Math.Min(t.MaxHoney, Honey + Made(t.HoneyPerBee));
            Wax = Math.Min(t.MaxWax, Wax + Made(t.WaxPerBee));
        }

        InvalidateProperties();
    }

    private int Made(double perBee) => (int)Math.Round(Population * perBee * Health / 100.0, MidpointRounding.AwayFromZero);

    /// <summary>The bees leave: the hive is empty again, its honey and wax kept.</summary>
    public void Leave()
    {
        Stage = HiveStage.Empty;
        Population = 0;
        Health = 0;
        Age = 0;
        Parasites = false;
        Disease = false;
        StrengthUntil = DateTime.MinValue;
        AgilityUntil = DateTime.MinValue;
        UpdateBees();
        InvalidateProperties();
        PublicOverheadMessage(MessageType.Regular, 0x3B2, false, "The bees have left the hive.");
    }

    // -- Starting a colony ----------------------------------------------------------------------------------

    public string? WhyNotSwarm(Mobile from)
    {
        if (!CanTend(from))
        {
            return "Only the house's owners may tend this hive.";
        }

        if (Lived)
        {
            return "This hive already has a colony.";
        }

        if (!IsLockedInHouse)
        {
            return "The hive must be locked down in a house first.";
        }

        return null;
    }

    public bool AddSwarm(Mobile from, BeeSwarm swarm)
    {
        if (WhyNotSwarm(from) is { } why)
        {
            from.SendMessage(why);
            return false;
        }

        var t = T;
        Stage = HiveStage.Colonizing;
        Age = 0;
        Population = Math.Min(t.MaxPopulation, swarm.Population);
        Health = swarm.Health;
        Parasites = false;
        Disease = false;
        NextCheck = Core.Now + TimeSpan.FromHours(t.CheckHours);
        swarm.Consume();
        UpdateBees();
        InvalidateProperties();
        from.PlaySound(0x5A);
        from.SendMessage("The swarm settles into the hive.");
        return true;
    }

    // -- Potions --------------------------------------------------------------------------------------------

    /// <summary>What a potion does to this hive, if anything; null when it does nothing (the potion is kept).</summary>
    public string? ApplyPotion(PotionEffect effect, DateTime now)
    {
        var t = T;
        switch (effect)
        {
            case PotionEffect.PoisonLesser or PotionEffect.Poison or PotionEffect.PoisonGreater or PotionEffect.PoisonDeadly:
                if (!Parasites)
                {
                    return null;
                }

                Parasites = false;
                return "The poison kills the parasites in the hive.";
            case PotionEffect.CureLesser or PotionEffect.Cure or PotionEffect.CureGreater:
                if (!Disease)
                {
                    return null;
                }

                Disease = false;
                return "The cure clears the sickness from the hive.";
            case PotionEffect.HealLesser or PotionEffect.Heal or PotionEffect.HealGreater:
                if (Health >= 100)
                {
                    return null;
                }

                Health = Math.Min(100, Health + (effect == PotionEffect.HealGreater ? t.GreaterHealAmount : t.HealAmount));
                return "The colony looks healthier.";
            case PotionEffect.Strength or PotionEffect.StrengthGreater:
                StrengthUntil = now + TimeSpan.FromHours(t.StrengthHours);
                return "The colony grows hardier.";
            case PotionEffect.Agility or PotionEffect.AgilityGreater:
                AgilityUntil = now + TimeSpan.FromHours(t.AgilityHours);
                return "The bees range further afield.";
            default:
                return null;
        }
    }

    public void Pour(Mobile from, Item item)
    {
        if (!CanTend(from))
        {
            from.SendMessage("Only the house's owners may tend this hive.");
            return;
        }

        if (!Lived)
        {
            from.SendMessage("There is no colony in this hive to treat.");
            return;
        }

        if (item is not BasePotion potion || !potion.IsChildOf(from.Backpack))
        {
            from.SendMessage("You can only pour a potion from your backpack on a beehive.");
            return;
        }

        var said = ApplyPotion(potion.PotionEffect, Core.Now);
        if (said == null)
        {
            from.SendMessage("The hive does not need that.");
            return;
        }

        potion.Consume();
        from.PlaySound(0x240);
        from.AddToBackpack(new Bottle());
        InvalidateProperties();
        from.SendMessage(said);
    }

    // -- Harvest ---------------------------------------------------------------------------------------------

    public bool Harvest(Mobile from, HiveTool tool)
    {
        if (!CanTend(from))
        {
            from.SendMessage("Only the house's owners may harvest this hive.");
            return false;
        }

        if (Honey <= 0 && Wax <= 0)
        {
            from.SendMessage("There is nothing in the hive to harvest yet.");
            return false;
        }

        var t = T;
        if (Honey > 0)
        {
            from.AddToBackpack(new JarHoney { Amount = Honey });
        }

        if (Wax > 0)
        {
            from.AddToBackpack(new RawBeeswax(Wax));
        }

        from.SendMessage($"You harvest {Honey} jar{(Honey == 1 ? "" : "s")} of honey and {Wax} raw beeswax.");
        Honey = 0;
        Wax = 0;
        tool.UseOnce(from);
        from.PlaySound(0x57);
        InvalidateProperties();
        return true;
    }

    /// <summary>Packs an empty hive back into its deed.</summary>
    public bool PackUp(Mobile from)
    {
        if (!CanTend(from))
        {
            from.SendMessage("Only the house's owners may pack up this hive.");
            return false;
        }

        if (Lived)
        {
            from.SendMessage("Bees still live in this hive.");
            return false;
        }

        if (Honey > 0 || Wax > 0)
        {
            from.SendMessage("Harvest the hive before you pack it up.");
            return false;
        }

        from.AddToBackpack(new BeehiveDeed());
        Delete();
        from.SendMessage("You pack the empty hive away.");
        return true;
    }

    // -- Placement ---------------------------------------------------------------------------------------------

    public static int HivesIn(BaseHouse house) => house.LockDowns.Count(i => i is HouseBeehive { Deleted: false });

    /// <summary>Places a hive from its deed at a spot in the placer's house; null when placed, else why not.</summary>
    public static string? Place(Mobile from, BeehiveDeed deed, Point3D at, Map map)
    {
        if (!deed.IsChildOf(from.Backpack))
        {
            return "The deed must be in your backpack to use it.";
        }

        var house = BaseHouse.FindHouseAt(at, map, 16);
        if (house == null || house != BaseHouse.FindHouseAt(from))
        {
            return "A beehive can only be placed inside your house.";
        }

        if (!house.IsCoOwner(from))
        {
            return "Only the house's owners may place a beehive.";
        }

        if (HivesIn(house) >= T.MaxHivesPerHouse)
        {
            return $"This house already has {T.MaxHivesPerHouse} beehives.";
        }

        var hive = new HouseBeehive();
        hive.MoveToWorld(at, map);
        if (!house.LockDown(from, hive))
        {
            hive.Delete();
            return "The hive could not be locked down here.";
        }

        deed.Delete();
        from.SendMessage("You set up the beehive. Bring a bee swarm to start a colony.");
        return null;
    }
}

/// <summary>The bees over a lived-in hive: fixed, never saved apart from its hive's link, deleted with it.</summary>
[SerializationGenerator(0, false)]
public partial class HiveBees : Item
{
    [Constructible]
    public HiveBees() : base(0x91B) => Movable = false;

    public override string DefaultName => "bees";
}

public class HiveTarget : Target
{
    private readonly Action<Mobile, HouseBeehive> _onHive;

    public HiveTarget(Action<Mobile, HouseBeehive> onHive) : base(3, false, TargetFlags.None) => _onHive = onHive;

    protected override void OnTarget(Mobile from, object targeted)
    {
        if (targeted is HouseBeehive hive)
        {
            _onHive(from, hive);
        }
        else
        {
            from.SendMessage("That is not a beehive.");
        }
    }
}
