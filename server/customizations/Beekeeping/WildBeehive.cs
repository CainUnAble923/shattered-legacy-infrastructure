// cc-P787: wild beehives (Chase, 2026-11-03: "Build as planned, with wild hives"). About a dozen fixed hives (0x91A) in
// Trammel's woods, at the spots of beekeeping.json's wild table. Anyone may take a honeycomb from one (combsPerHive,
// with a sting of stingMin to stingMax hits that never kills); an emptied hive goes quiet and regrows after
// regrowHours. A hive tool may instead catch a swarm there (swarmChance per try, one swarm per regrowth): a free start
// for a home hive.
//
// Seeding (WildHiveSeeder) runs after every world load and on [Beekeeping seed: a spot with no wild hive within one
// tile gets one, a spot that has one is left alone, so it is idempotent and an existing world gains them by itself.
// Turning the wild table off stops seeding and leaves placed hives where they are (staff delete them by hand).

#nullable enable

using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Items;

namespace Server.Engines.Beekeeping;

[SerializationGenerator(0, false)]
public partial class WildBeehive : Item
{
    public const int EmptyHue = 0x3B2;

    private static readonly HashSet<WildBeehive> _all = [];

    public static IReadOnlyCollection<WildBeehive> All => _all;

    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private string? _spot;

    [SerializableField(1)]
    [InvalidateProperties]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private int _combs;

    [SerializableField(2)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private DateTime _regrowAt;

    [SerializableField(3)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private bool _swarmTaken;

    [Constructible]
    public WildBeehive(string? spot = null) : base(HouseBeehive.HiveArt)
    {
        Movable = false;
        _spot = spot;
        _combs = BeekeepingTable.Current.Wild.CombsPerHive;
        _all.Add(this);
    }

    public override string DefaultName => "a wild beehive";

    [AfterDeserialization]
    private void AfterDeserialization()
    {
        _all.Add(this);
    }

    public override void OnDelete()
    {
        _all.Remove(this);
        base.OnDelete();
    }

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add(Combs > 0 ? $"Honeycombs: {Combs}" : "Empty, regrowing");
    }

    /// <summary>Refills an emptied hive whose regrowth time has come. True when it regrew.</summary>
    public bool RegrowIfDue(DateTime now)
    {
        if (Combs > 0 || now < RegrowAt)
        {
            return false;
        }

        Combs = BeekeepingTable.Current.Wild.CombsPerHive;
        SwarmTaken = false;
        Hue = 0;
        return true;
    }

    private void Emptied(DateTime now)
    {
        RegrowAt = now + TimeSpan.FromHours(BeekeepingTable.Current.Wild.RegrowHours);
        Hue = EmptyHue;
        PublicOverheadMessage(MessageType.Regular, 0x3B2, false, "The emptied hive falls quiet.");
    }

    /// <summary>A sting of the table's size that never kills. Returns the damage dealt.</summary>
    public static int Sting(Mobile from)
    {
        var w = BeekeepingTable.Current.Wild;
        var dmg = Math.Min(Utility.RandomMinMax(w.StingMin, w.StingMax), Math.Max(0, from.Hits - 1));
        if (dmg > 0)
        {
            from.Damage(dmg);
            from.PlaySound(0x5A);
            from.SendMessage("The bees sting you!");
        }

        return dmg;
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (!from.InRange(GetWorldLocation(), 2))
        {
            from.SendLocalizedMessage(500446); // That is too far away.
            return;
        }

        TakeComb(from, Core.Now);
    }

    public bool TakeComb(Mobile from, DateTime now)
    {
        RegrowIfDue(now);
        if (Combs <= 0)
        {
            from.SendMessage("The hive is empty. The bees will rebuild it in time.");
            return false;
        }

        Combs--;
        from.AddToBackpack(new Honeycomb());
        from.SendMessage("You pull a honeycomb from the wild hive.");
        Sting(from);
        if (Combs <= 0)
        {
            Emptied(now);
        }

        return true;
    }

    public bool CatchSwarm(Mobile from, HiveTool tool) => CatchSwarm(from, tool, Core.Now, Utility.Random(100));

    public bool CatchSwarm(Mobile from, HiveTool tool, DateTime now, int roll)
    {
        RegrowIfDue(now);
        if (Combs <= 0)
        {
            from.SendMessage("The hive is empty. The bees will rebuild it in time.");
            return false;
        }

        if (SwarmTaken)
        {
            from.SendMessage("A swarm was already taken from this hive. Come back when it has regrown.");
            return false;
        }

        tool.UseOnce(from);
        Sting(from);
        if (roll >= BeekeepingTable.Current.Wild.SwarmChance)
        {
            from.SendMessage("The bees will not settle in your box. Try again.");
            return false;
        }

        SwarmTaken = true;
        from.AddToBackpack(new BeeSwarm());
        from.SendMessage("You coax a swarm of bees into a box.");
        return true;
    }
}

public static class WildHiveSeeder
{
    public const int SearchRange = 1;

    public static WildBeehive? FindAt(Map map, Point3D at)
    {
        foreach (var item in map.GetItemsInRange(at, SearchRange))
        {
            if (item is WildBeehive { Deleted: false } hive)
            {
                return hive;
            }
        }

        return null;
    }

    /// <summary>Places a wild hive at every spot that has none. Idempotent.</summary>
    public static List<string> Seed(bool dryRun = false)
    {
        var report = new List<string>();
        var w = BeekeepingTable.Current.Wild;
        var map = Map.Trammel;
        if (!BeekeepingTable.Current.Enabled || !w.Enabled)
        {
            report.Add("Wild hives are off; nothing seeded.");
            return report;
        }

        if (map == null || map == Map.Internal)
        {
            report.Add("Trammel is not loaded; nothing seeded.");
            return report;
        }

        int placed = 0, present = 0;
        foreach (var spot in w.Spots)
        {
            if (FindAt(map, spot.Location) != null)
            {
                present++;
                continue;
            }

            placed++;
            if (!dryRun)
            {
                new WildBeehive(spot.Name).MoveToWorld(spot.Location, map);
            }

            report.Add($"{(dryRun ? "would place" : "placed")} a wild hive at {spot.Name} {spot.Location}");
        }

        report.Add($"Wild hives: {placed} {(dryRun ? "to place" : "placed")}, {present} already there, of {w.Spots.Count} spots.");
        return report;
    }
}
