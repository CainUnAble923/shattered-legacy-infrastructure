// cc-P634: Town Houses (leaseholds), stage 1: the lease site and the staff tools. See ClusterFLeasehold.cs for the house.
//
// A site is a permanent item at the building's street spot (P550 correction 5: it outlives any house, sign or door).
// It keeps the rectangle, the floor range, the town region it was made in, the town doors that become the tenant's,
// and the leasehold while one is active. It is invisible to players: stage 1 has no rent and no player leasing.
//
// Vacant, the building is an ordinary public town building with its own doors. Assigning a tenant (staff only, a test
// lease with no money) makes the leasehold over the building, puts a house sign beside the site, and swaps each
// registered town door for a house door of the same art; the town door waits, unchanged, on the internal map at its
// own location. Ending the lease, or the leasehold being deleted any other way, deletes the house doors and puts the
// town doors back (P550: town doors stay in service while vacant; swap only while leased).
//
// [Leaseholds (GameMaster, test shard only): new, door, assign, end, list, go, remove.
// cc-P647: "test shard only" is enforced (IsTestShard): the command, its open targets, Create, AddDoor and assignment.

#nullable enable

using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Commands;
using Server.Engines.Spawners;
using Server.Items;
using Server.Mobiles;
using Server.Multis;
using Server.Regions;
using Server.Targeting;

namespace Server.Leaseholds;

[SerializationGenerator(0)]
public partial class ClusterFLeaseSite : Item
{
    public static readonly List<ClusterFLeaseSite> All = [];

    // cc-P647: stage 1 is the test shard's only. The command, every target it leaves open, and making, door
    // registration and assignment refuse anywhere else (ShardCommand's TestOnly is a label, not a gate). Tests flip it.
    public static Func<bool> IsTestShard { get; set; } = () => ClusterFStaffHub.IsTestShard;

    public const string TestShardOnly = "Town Houses are on the test shard only for now.";

    [SerializableField(0, setter: "private")]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private Rectangle2D _bounds;

    [SerializableField(1, setter: "private")]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private int _zLow;

    [SerializableField(2, setter: "private")]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private int _zHigh;

    // The name of the town region at the street spot when the site was made.
    [SerializableField(3, setter: "private")]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private string? _parentName;

    [SerializableField(4, setter: "private")]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private ClusterFLeasehold? _leasehold;

    // A staff test lease: no money was taken and none is ever owed or refunded.
    [SerializableField(5, setter: "private")]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private bool _testLease;

    // The town's own doors that become the tenant's while leased.
    [SerializableField(6)]
    private List<BaseDoor> _doors = [];

    public ClusterFLeaseSite(Rectangle2D bounds, int zLow, int zHigh, string? parentName) : base(0xBD2)
    {
        _bounds = bounds;
        _zLow = zLow;
        _zHigh = zHigh;
        _parentName = parentName;
        Movable = false;
        Visible = false;
        All.Add(this);
    }

    public override string DefaultName => "a town house site";

    public bool IsLeased => Leasehold is { Deleted: false };

    public bool Covers(Point3D p) => Bounds.Contains(p) && p.Z >= ZLow && p.Z <= ZHigh;

    // -- Making a site --------------------------------------------------------------------------------------------

    /// <summary>
    /// Why a site cannot go here, or null. The street spot is outside the building, in a guarded town, and in the same
    /// region as every tile at every height between the floors (an inn's or bank's own region, or a rectangle half in and
    /// half out of town, is refused), the street spot is walkable, and the building holds no house, other site, other
    /// multi (by its pieces), creature, spawner or town addon container.
    /// </summary>
    public static string? WhyNotSite(
        Map? map, Rectangle2D bounds, int zLow, int zHigh, Point3D street, ClusterFLeaseSite? self = null
    )
    {
        if (map == null || map == Map.Internal)
        {
            return "Sites go on a facet.";
        }

        if (bounds.Width < 1 || bounds.Height < 1 || bounds.Width > 32 || bounds.Height > 32)
        {
            return "The building must be 1 to 32 tiles each way.";
        }

        if (zHigh < zLow || zHigh - zLow > 60)
        {
            return "The highest floor must be 0 to 60 above the lowest.";
        }

        if (bounds.Contains(street))
        {
            return "The street spot must be outside the building.";
        }

        // cc-P647 (P646 finding 2): strangers are put on the street spot (the house's ban location, on login and on
        // walking in), with no fit check of the engine's own, so it must hold a person: a surface, room to stand, and
        // not in another house.
        if (!map.CanFit(street.X, street.Y, street.Z, 16, false, false, true))
        {
            return "The street spot must be walkable: a floor with room to stand, at the floor's height.";
        }

        if (BaseHouse.FindHouseAt(street, map, 16) != null)
        {
            return "The street spot must be walkable street, not inside a house.";
        }

        // cc-P647: the street region must be a town with its guards on (a TownRegion, itself or an ancestor).
        var town = Region.Find(street, map);
        if (town.GetRegion<TownRegion>() == null || town.GetRegion<GuardedRegion>() is not { } guards || guards.IsDisabled())
        {
            return "The street spot must be in a guarded town.";
        }

        for (var x = bounds.X; x < bounds.X + bounds.Width; x++)
        {
            for (var y = bounds.Y; y < bounds.Y + bounds.Height; y++)
            {
                var p = new Point3D(x, y, zLow);

                // cc-P647: every height the leasehold's region will cover, not the lowest floor only (a child region
                // upstairs, such as an inn's rooms, is refused too).
                for (var z = zLow; z <= zHigh; z++)
                {
                    if (Region.Find(new Point3D(x, y, z), map) != town)
                    {
                        return $"The tile at {x}, {y}, {z} is in another region than the street spot.";
                    }
                }

                if (BaseHouse.FindHouseAt(p, map, zHigh - zLow + 16) != null)
                {
                    return $"There is a house at {x}, {y}.";
                }
            }
        }

        foreach (var site in All)
        {
            if (site != self && !site.Deleted && site.Map == map && Overlaps(site.Bounds, site.ZLow, site.ZHigh, bounds, zLow, zHigh))
            {
                return "That overlaps another town house site.";
            }
        }

        foreach (var m in map.GetMobilesInBounds<Mobile>(bounds))
        {
            if (!m.Player && m.Z >= zLow && m.Z <= zHigh)
            {
                return $"{m.Name ?? "A creature"} stands inside: town services stay public.";
            }
        }

        foreach (var item in map.GetItemsInBounds<Item>(bounds))
        {
            if (item is BaseSpawner or Moongate or Teleporter && item.Z >= zLow && item.Z <= zHigh)
            {
                return $"{item.GetType().Name} is inside: spawners and gates stay out of town houses.";
            }

            // cc-P647 (P646 finding 1): a house owner may chop any unsecured addon container in the house into a deed
            // (BaseAddonContainer.OnChop asks only for ownership), so a town's own one, or any piece of it, keeps the
            // site out until fixtures are recorded and protected.
            if (item is BaseAddonContainer or AddonContainerComponent && item.Z >= zLow && item.Z <= zHigh)
            {
                return $"{item.GetType().Name} is a town fixture a tenant could chop into a deed: move it out first.";
            }
        }

        // cc-P647 (P646 finding 3): other multis by what they occupy. A multi is registered in every sector its
        // footprint covers, wherever it stands (an origin check would miss one standing outside and reaching in).
        var seen = new HashSet<BaseMulti>();
        for (var sx = bounds.X >> Map.SectorShift; sx <= (bounds.X + bounds.Width - 1) >> Map.SectorShift; sx++)
        {
            for (var sy = bounds.Y >> Map.SectorShift; sy <= (bounds.Y + bounds.Height - 1) >> Map.SectorShift; sy++)
            {
                foreach (var multi in map.GetMultisInSector(sx << Map.SectorShift, sy << Map.SectorShift))
                {
                    if (!multi.Deleted && seen.Add(multi) && Occupies(multi, bounds, zLow, zHigh))
                    {
                        return $"{multi.GetType().Name} reaches inside: multis stay out of town houses.";
                    }
                }
            }
        }

        return null;
    }

    // True when a piece of the multi stands on a tile of the rectangle within the floor range (a piece is at least a
    // step tall). A footprint with no pieces (a leasehold) counts by its bounds at its own height.
    private static bool Occupies(BaseMulti multi, Rectangle2D bounds, int zLow, int zHigh)
    {
        var mcl = multi.Components;
        if (mcl.List.Length == 0)
        {
            return mcl.Width > 0 && Overlaps(
                new Rectangle2D(multi.X + mcl.Min.X, multi.Y + mcl.Min.Y, mcl.Width, mcl.Height), multi.Z, multi.Z,
                bounds, zLow, zHigh
            );
        }

        foreach (var piece in mcl.List)
        {
            var z = multi.Z + piece.OffsetZ;
            var height = Math.Max(1, TileData.ItemTable[piece.ItemId & TileData.MaxItemValue].CalcHeight);
            if (bounds.Contains(new Point2D(multi.X + piece.OffsetX, multi.Y + piece.OffsetY)) && z <= zHigh &&
                z + height > zLow)
            {
                return true;
            }
        }

        return false;
    }

    private static bool Overlaps(Rectangle2D a, int aLow, int aHigh, Rectangle2D b, int bLow, int bHigh) =>
        a.X < b.X + b.Width && b.X < a.X + a.Width && a.Y < b.Y + b.Height && b.Y < a.Y + a.Height &&
        aLow <= bHigh && bLow <= aHigh;

    /// <summary>Makes a vacant site at the street spot. Returns the site, or null with why not.</summary>
    public static ClusterFLeaseSite? Create(Map map, Rectangle2D bounds, int zLow, int zHigh, Point3D street, out string? why)
    {
        why = IsTestShard() ? WhyNotSite(map, bounds, zLow, zHigh, street) : TestShardOnly;
        if (why != null)
        {
            return null;
        }

        var region = Region.Find(street, map);
        var site = new ClusterFLeaseSite(bounds, zLow, zHigh, region == map.DefaultRegion ? null : region.Name);
        site.MoveToWorld(street, map);
        return site;
    }

    /// <summary>Registers a town door of this building as one that becomes the tenant's. Vacant sites only.</summary>
    public string? AddDoor(BaseDoor door)
    {
        if (!IsTestShard())
        {
            return TestShardOnly;
        }

        if (IsLeased)
        {
            return "End the lease first.";
        }

        if (door is BaseHouseDoor || door.Deleted || door.Map != Map || door.Open || !Covers(door.Location))
        {
            return "That must be a closed town door inside the building.";
        }

        if (_doors.Contains(door))
        {
            return "That door is already the site's.";
        }

        foreach (var other in All)
        {
            if (other != this && other._doors.Contains(door))
            {
                return "That door is another site's.";
            }
        }

        _doors.Add(door);
        this.MarkDirty();
        return null;
    }

    // -- Leasing ----------------------------------------------------------------------------------------------------

    /// <summary>Why this player cannot hold this site now, or null (stage 1: staff test leases only).</summary>
    public string? WhyNotAssign(Mobile tenant)
    {
        if (!IsTestShard())
        {
            return TestShardOnly;
        }

        if (Deleted || Map == null || Map == Map.Internal)
        {
            return "That site is gone.";
        }

        if (IsLeased)
        {
            return "That site is already leased.";
        }

        if (tenant is not PlayerMobile { Deleted: false })
        {
            return "Only a player can hold a town house.";
        }

        // The account's one house (stock: staff are exempt).
        if (tenant.AccessLevel < AccessLevel.GameMaster && BaseHouse.HasAccountHouse(tenant))
        {
            return "That account already has a house.";
        }

        foreach (var door in _doors)
        {
            if (door.Deleted || door.Map != Map || door.Open)
            {
                return "One of the site's doors is gone or open; close it or re-register the doors.";
            }

            // cc-P647 (P646 finding 4): a linked pair (or chain) goes together. A partner left in town would still
            // open the hidden original (BaseDoor.Use opens Link wherever it is), which then could not close.
            if (door.Link != null && !_doors.Contains(door.Link))
            {
                return $"The door at {door.X}, {door.Y} is linked to one the site does not have; [Leaseholds door its partner too.";
            }
        }

        return WhyNotSite(Map, Bounds, ZLow, ZHigh, Location, this);
    }

    /// <summary>A staff test lease: the leasehold, its sign and its doors, with no money. Returns why not, or null.</summary>
    public string? AssignTest(Mobile tenant)
    {
        if (WhyNotAssign(tenant) is { } why)
        {
            return why;
        }

        var map = Map!;
        var house = new ClusterFLeasehold(tenant, this);
        Leasehold = house;
        TestLease = true;
        house.MoveToWorld(new Point3D(Bounds.X, Bounds.Y, ZLow), map);

        house.Sign = new HouseSign(house);
        house.Sign.MoveToWorld(Location, map);

        // Each town door steps aside for a house door of the same art; a linked pair stays linked.
        var made = new Dictionary<BaseDoor, BaseDoor>();
        foreach (var door in _doors)
        {
            var leaseDoor = new ClusterFLeaseDoor(door);
            leaseDoor.MoveToWorld(door.Location, map);
            house.Doors.Add(leaseDoor);
            made[door] = leaseDoor;
            door.Map = Map.Internal; // keeps its own location for the way back
        }

        foreach (var (door, leaseDoor) in made)
        {
            if (door.Link != null && made.TryGetValue(door.Link, out var partner))
            {
                leaseDoor.Link = partner;
            }
        }

        return null;
    }

    /// <summary>Ends the lease (stage 1: a staff test lease; belongings are not packed yet). Returns why not, or null.</summary>
    public string? EndLease()
    {
        if (!IsLeased)
        {
            return "That site is not leased.";
        }

        Leasehold!.Delete(); // its OnAfterDelete calls OnLeaseholdDeleted
        return null;
    }

    /// <summary>The leasehold is gone, whatever deleted it: the town doors come back and the site is vacant.</summary>
    public void OnLeaseholdDeleted(ClusterFLeasehold house)
    {
        if (house != Leasehold)
        {
            return;
        }

        Leasehold = null;
        TestLease = false;
        RestoreDoors();
    }

    private void RestoreDoors()
    {
        if (Map == null || Map == Map.Internal)
        {
            return;
        }

        foreach (var door in _doors)
        {
            if (!door.Deleted && door.Map == Map.Internal)
            {
                door.Map = Map;
            }
        }
    }

    public override void OnDelete()
    {
        if (IsLeased)
        {
            Leasehold!.Delete();
        }

        base.OnDelete();
    }

    public override void OnAfterDelete()
    {
        All.Remove(this);
        base.OnAfterDelete();
    }

    [AfterDeserialization]
    private void AfterDeserialization() => All.Add(this);

    // -- Staff (test shard) -----------------------------------------------------------------------------------------

    public static void Configure()
    {
        CommandSystem.Register("Leaseholds", AccessLevel.GameMaster, OnCommand);
    }

    [Usage("Leaseholds new <lowest z> <highest z> | door | assign | end | list | go <n> | remove")]
    [Description("Town Houses (stage 1, test shard): make a site over a town building (target two corners, then the street spot outside the door), register its doors, give a player a staff test lease (no money), end it, list, go to or remove sites.")]
    [ShardCommand(CommandCategory.DevTool, Shard = CommandShard.TestOnly)]
    private static void OnCommand(CommandEventArgs e)
    {
        var from = e.Mobile;
        var verb = e.Length > 0 ? e.GetString(0).ToLowerInvariant() : "list";

        if (Refused(from))
        {
            return;
        }

        switch (verb)
        {
            case "new" when e.Length >= 3:
                {
                    var zLow = e.GetInt32(1);
                    var zHigh = e.GetInt32(2);
                    from.SendMessage("Target one corner of the building's floor.");
                    from.BeginTarget(-1, true, TargetFlags.None, (_, a) =>
                    {
                        if (a is not IPoint3D pa || Refused(from))
                        {
                            return;
                        }

                        from.SendMessage("Target the opposite corner.");
                        from.BeginTarget(-1, true, TargetFlags.None, (_, b) =>
                        {
                            if (b is not IPoint3D pb || Refused(from))
                            {
                                return;
                            }

                            from.SendMessage("Target the street spot outside the door (the sign and the ban spot).");
                            from.BeginTarget(-1, true, TargetFlags.None, (_, s) =>
                            {
                                if (s is not IPoint3D ps || Refused(from))
                                {
                                    return;
                                }

                                var bounds = new Rectangle2D(
                                    new Point2D(Math.Min(pa.X, pb.X), Math.Min(pa.Y, pb.Y)),
                                    new Point2D(Math.Max(pa.X, pb.X) + 1, Math.Max(pa.Y, pb.Y) + 1)
                                );
                                var site = Create(from.Map, bounds, zLow, zHigh, new Point3D(ps), out var why);
                                from.SendMessage(site == null ? why! : $"Site {site.Serial} made over {bounds}, z {zLow} to {zHigh}. Now [Leaseholds door for each door.");
                            });
                        });
                    });
                    return;
                }
            case "door":
                TargetSite(from, "Target the site, then its door.", site =>
                {
                    from.BeginTarget(-1, false, TargetFlags.None, (_, d) =>
                        from.SendMessage(d is BaseDoor door ? site.AddDoor(door) ?? "Door registered." : "That is not a door."));
                });
                return;
            case "assign":
                TargetSite(from, "Target the site, then the player (yourself for your own test lease).", site =>
                {
                    from.BeginTarget(-1, false, TargetFlags.None, (_, m) =>
                        from.SendMessage(m is Mobile tenant ? site.AssignTest(tenant) ?? $"{tenant.Name} holds a test lease (no money)." : "That is not a player."));
                });
                return;
            case "end":
                TargetSite(from, "Target the site.", site => from.SendMessage(site.EndLease() ?? "Lease ended; the town doors are back."));
                return;
            case "remove":
                TargetSite(from, "Target the vacant site to remove.", site =>
                {
                    if (site.IsLeased)
                    {
                        from.SendMessage("End the lease first.");
                        return;
                    }

                    site.Delete();
                    from.SendMessage("Site removed.");
                });
                return;
            case "go" when e.Length >= 2:
                {
                    var n = e.GetInt32(1);
                    if (n < 1 || n > All.Count)
                    {
                        from.SendMessage($"There are {All.Count} sites.");
                        return;
                    }

                    from.MoveToWorld(All[n - 1].Location, All[n - 1].Map);
                    return;
                }
        }

        from.SendMessage($"{All.Count} town house site(s):");
        for (var i = 0; i < All.Count; i++)
        {
            var s = All[i];
            var state = s.IsLeased ? $"leased to {s.Leasehold!.Owner?.Name ?? "nobody"}{(s.TestLease ? " (test)" : "")}" : "vacant";
            var parent = s.Leasehold is { ParentMissing: true } ? "; CHECK: town region missing" : "";
            from.SendMessage($"{i + 1}. {s.Map} {s.Bounds} z {s.ZLow}-{s.ZHigh}, {s.ParentName ?? "no region"}, {s._doors.Count} door(s), {state}{parent}");
        }
    }

    // Off the test shard: says so and stops (also for a target opened before the switch and answered after).
    private static bool Refused(Mobile from)
    {
        if (IsTestShard())
        {
            return false;
        }

        from.SendMessage(TestShardOnly);
        return true;
    }

    private static void TargetSite(Mobile from, string prompt, Action<ClusterFLeaseSite> then)
    {
        from.SendMessage(prompt);
        from.BeginTarget(-1, false, TargetFlags.None, (_, t) =>
        {
            if (Refused(from))
            {
                return;
            }

            if (t is ClusterFLeaseSite site)
            {
                then(site);
            }
            else if (t is HouseSign { Owner: ClusterFLeasehold { Site: { } owned } })
            {
                then(owned);
            }
            else
            {
                from.SendMessage("That is not a town house site.");
            }
        });
    }
}
