// LeaseholdVerification.cs
//
// cc-P634: Town Houses (leaseholds), stage 1 (customizations Leaseholds/, the patch BaseHouse-leasehold-region.patch;
// notes: shard-migration notes/cc-P634-build-town-houses.md; design notes/cc-P409-town-houses-design.md as corrected by
// notes/cc-P550-review-town-houses-design.md).
//
// Facts:
//   1. A leasehold is a house in the town's guards: BaseHouse.Region is the leasehold region, its parent is the town
//      region at the street spot, a point inside finds it and through it the GuardedRegion, the street stays the town;
//      updating the region again keeps the town as parent (never itself). It is the account's one house, ageless, with
//      no price and no vendors.
//   2. FindHouseAt finds it on every edge of a building that crosses sector lines, and not one tile outside, below the
//      lowest floor or above the highest; two leaseholds keep their own footprints and the shared empty list stays empty.
//   3. Town doors step aside while leased and come back: the house door stands where the town door stood and opens for
//      the house; ending the lease, or deleting the house any other way, puts the same town doors back, still linked.
//   4. Unsafe sites are refused: a street spot inside, an overlap, a creature inside, a building half outside the town;
//      a player whose account has a house cannot be given one, nor can a leased site be given again.
//   5. Save and load: the site keeps its rectangle, floors, town and doors; a reloaded leasehold re-registers its whole
//      footprint (FindHouseAt at the far corner) and its town-parented region.

using System;
using System.Collections.Generic;
using Server;
using Server.Accounting;
using Server.Items;
using Server.Leaseholds;
using Server.Mobiles;
using Server.Multis;
using Server.Regions;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class LeaseholdVerification : IDisposable
{
    // Far from any loaded town, and crossing the sector lines at x 6016 and y 128 (sectors are 16 tiles).
    private static readonly Rectangle2D Building = new(6008, 120, 16, 16);
    private static readonly Point3D Street = new(6015, 137, 0);

    private readonly List<IEntity> _things = [];
    private readonly List<Account> _accounts = [];
    private readonly GuardedRegion _town;

    public LeaseholdVerification()
    {
        ShardTestHost.EnsureAccounts();
        _town = new GuardedRegion("P634 Town", Map.Trammel, 50, new Rectangle2D(5950, 60, 200, 200));
        _town.Register();
    }

    public void Dispose()
    {
        foreach (var e in _things)
        {
            if (e is ClusterFLeaseSite { IsLeased: true } site)
            {
                site.EndLease();
            }
        }

        foreach (var e in _things)
        {
            e.Delete();
        }

        foreach (var a in _accounts)
        {
            a.Delete();
        }

        _town.Unregister();
    }

    private T Track<T>(T e) where T : IEntity
    {
        _things.Add(e);
        return e;
    }

    private PlayerMobile Player()
    {
        var pm = Track(new PlayerMobile { Player = true, Race = Race.Human, Name = "P634 tenant" });
        var account = new Account($"p634{Guid.NewGuid():N}"[..16], "p634-test-only");
        _accounts.Add(account);
        account[0] = pm;
        pm.Account = account;
        pm.AddItem(new Backpack());
        pm.MoveToWorld(new Point3D(Street.X, Street.Y + 2, 0), Map.Trammel);
        return pm;
    }

    private ClusterFLeaseSite Site(Rectangle2D bounds, Point3D street, int zLow = 0, int zHigh = 20)
    {
        var site = ClusterFLeaseSite.Create(Map.Trammel, bounds, zLow, zHigh, street, out var why);
        Assert.True(site != null, why);
        return Track(site!);
    }

    private ClusterFLeasehold Leased(ClusterFLeaseSite site, Mobile tenant)
    {
        Assert.Null(site.AssignTest(tenant));
        var house = site.Leasehold!;
        _things.Add(house);
        return house;
    }

    // ---------------------------------------------------------------- 1

    [Fact]
    public void ALeaseholdIsAHouseInsideTheTownsGuards()
    {
        var pm = Player();
        var site = Site(Building, Street);
        Assert.Equal("P634 Town", site.ParentName);
        var house = Leased(site, pm);

        Assert.IsType<ClusterFLeaseholdRegion>(house.Region);
        Assert.Same(_town, house.Region.Parent);
        Assert.False(house.ParentMissing);

        var inside = Region.Find(new Point3D(6020, 130, 0), Map.Trammel);
        Assert.Same(house.Region, inside);
        Assert.True(inside.IsPartOf<HouseRegion>());
        Assert.Same(_town, inside.GetRegion<GuardedRegion>());
        Assert.Same(_town, Region.Find(Street, Map.Trammel));

        // A second update builds a new region with the same parent, and the old one is gone.
        var first = house.Region;
        house.UpdateRegion();
        Assert.NotSame(first, house.Region);
        Assert.Same(_town, house.Region.Parent);
        Assert.Same(house.Region, Region.Find(new Point3D(6020, 130, 0), Map.Trammel));

        Assert.True(BaseHouse.HasAccountHouse(pm));
        Assert.Equal(DecayType.Ageless, house.DecayType);
        Assert.Equal(0, house.Price);
        Assert.Null(house.GetDeed());
        Assert.Equal(0, house.GetNewVendorSystemMaxVendors());
        Assert.True(house.GetAosMaxLockdowns() > 0);
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void FindHouseAtWorksAcrossSectorsAndOnlyBetweenTheFloors()
    {
        var house = Leased(Site(Building, Street, 0, 20), Player());

        Point3D[] edges = [new(6008, 120, 0), new(6023, 120, 0), new(6008, 135, 0), new(6023, 135, 20), new(6016, 128, 10)];
        foreach (var p in edges)
        {
            Assert.Same(house, BaseHouse.FindHouseAt(p, Map.Trammel, 16));
        }

        Point3D[] outside = [new(6007, 120, 0), new(6024, 135, 0), new(6015, 119, 0), new(6015, 136, 0), new(6015, 128, -1), new(6015, 128, 21)];
        foreach (var p in outside)
        {
            Assert.Null(BaseHouse.FindHouseAt(p, Map.Trammel, 16));
        }

        var other = Leased(Site(new Rectangle2D(6040, 120, 4, 6), new Point3D(6041, 127, 0)), Player());
        Assert.Same(other, BaseHouse.FindHouseAt(new Point3D(6043, 125, 0), Map.Trammel, 16));
        Assert.Null(BaseHouse.FindHouseAt(new Point3D(6044, 125, 0), Map.Trammel, 16));
        Assert.Same(house, BaseHouse.FindHouseAt(new Point3D(6023, 125, 0), Map.Trammel, 16));

        Assert.NotSame(house.Components, other.Components);
        Assert.Equal(16, house.Components.Width);
        Assert.Equal(4, other.Components.Width);
        Assert.Equal(0, MultiComponentList.Empty.Width);
        Assert.Empty(MultiComponentList.Empty.List);
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void TownDoorsStepAsideWhileLeasedAndComeBack()
    {
        var site = Site(Building, Street);
        var west = Track(new DarkWoodDoor(DoorFacing.WestCW));
        var east = Track(new DarkWoodDoor(DoorFacing.EastCCW));
        west.MoveToWorld(new Point3D(6014, 135, 0), Map.Trammel);
        east.MoveToWorld(new Point3D(6015, 135, 0), Map.Trammel);
        west.Link = east;
        east.Link = west;
        Assert.Null(site.AddDoor(west));
        Assert.Null(site.AddDoor(east));
        Assert.NotNull(site.AddDoor(west));

        var house = Leased(site, Player());
        Assert.Equal(Map.Internal, west.Map);
        Assert.Equal(new Point3D(6014, 135, 0), west.Location);
        Assert.Equal(2, house.Doors.Count);
        var leaseWest = Assert.IsType<ClusterFLeaseDoor>(house.Doors[0]);
        Assert.Equal(new Point3D(6014, 135, 0), leaseWest.Location);
        Assert.Equal(west.ClosedId, leaseWest.ItemID);
        Assert.Same(house, leaseWest.FindHouse());
        Assert.Same(house.Doors[1], leaseWest.Link);

        Assert.Null(site.EndLease());
        Assert.True(leaseWest.Deleted);
        Assert.False(site.IsLeased);
        Assert.Equal(Map.Trammel, west.Map);
        Assert.Equal(new Point3D(6014, 135, 0), west.Location);
        Assert.Same(east, west.Link);
        Assert.Equal(Map.Trammel, east.Map);

        // Deleted any other way (staff, demolish, account deletion): the doors still come back.
        var again = Leased(site, Player());
        Assert.Equal(Map.Internal, east.Map);
        again.Delete();
        Assert.False(site.IsLeased);
        Assert.Equal(Map.Trammel, east.Map);
        Assert.Equal(Map.Trammel, west.Map);
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void UnsafeSitesAndSecondHousesAreRefused()
    {
        var map = Map.Trammel;
        Assert.Contains("outside the building", ClusterFLeaseSite.WhyNotSite(map, Building, 0, 20, new Point3D(6010, 125, 0)));

        var site = Site(Building, Street);
        Assert.Contains("overlaps", ClusterFLeaseSite.WhyNotSite(map, new Rectangle2D(6020, 130, 8, 8), 0, 20, new Point3D(6025, 140, 0)));

        var guard = Track(new Mongbat());
        guard.MoveToWorld(new Point3D(6062, 122, 0), map);
        Assert.Contains("stands inside", ClusterFLeaseSite.WhyNotSite(map, new Rectangle2D(6060, 120, 5, 5), 0, 20, new Point3D(6061, 126, 0)));

        // Half in the town, half out (the town ends at x 6150).
        Assert.Contains("another region", ClusterFLeaseSite.WhyNotSite(map, new Rectangle2D(6145, 120, 10, 5), 0, 20, new Point3D(6146, 126, 0)));

        var pm = Player();
        Leased(site, pm);
        Assert.Contains("already leased", site.AssignTest(Player()));

        var second = Site(new Rectangle2D(6040, 150, 5, 5), new Point3D(6041, 156, 0));
        Assert.Contains("already has a house", second.AssignTest(pm));
        Assert.False(second.IsLeased);
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void SitesAndLeaseholdsSurviveSaveAndLoad()
    {
        var site = Site(Building, Street, 0, 20);
        var door = Track(new DarkWoodDoor(DoorFacing.WestCW));
        door.MoveToWorld(new Point3D(6014, 135, 0), Map.Trammel);
        Assert.Null(site.AddDoor(door));
        var house = Leased(site, Player());

        var buffer = new byte[1 << 16];
        var writer = new BufferWriter(buffer, true);
        site.Serialize(writer);
        writer.Flush();
        var siteCopy = new ClusterFLeaseSite(site.Serial);
        siteCopy.Deserialize(new BufferReader(buffer));
        ClusterFLeaseSite.All.Remove(siteCopy);
        Assert.Equal(Building, siteCopy.Bounds);
        Assert.Equal(20, siteCopy.ZHigh);
        Assert.Equal("P634 Town", siteCopy.ParentName);
        Assert.Same(house, siteCopy.Leasehold);
        Assert.True(siteCopy.TestLease);
        Assert.Same(door, Assert.Single(siteCopy.Doors));

        // The house: saved, taken off the map (as before a load), then read back into a fresh object.
        buffer = new byte[1 << 16];
        writer = new BufferWriter(buffer, true);
        house.Serialize(writer);
        writer.Flush();
        var serial = house.Serial;
        house.Internalize();
        Assert.Null(BaseHouse.FindHouseAt(new Point3D(6023, 135, 0), Map.Trammel, 16));

        var copy = new ClusterFLeasehold(serial);
        _things.Add(copy);
        copy.Deserialize(new BufferReader(buffer));
        Assert.Same(site, copy.Site);
        Assert.Same(copy, BaseHouse.FindHouseAt(new Point3D(6023, 135, 0), Map.Trammel, 16));
        Assert.Same(copy, BaseHouse.FindHouseAt(new Point3D(6008, 120, 0), Map.Trammel, 16));
        Assert.IsType<ClusterFLeaseholdRegion>(copy.Region);
        Assert.Same(_town, copy.Region.Parent);
    }
}
