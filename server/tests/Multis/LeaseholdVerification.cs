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
//
// cc-P647 (fixes of the P634 Codex review and P646), one fact each:
//   6. Off the test shard the command opens no target, a target opened on the test shard does nothing once answered
//      elsewhere, and Create, AddDoor and AssignTest refuse.
//   7. Guarded town through the whole volume: a building outside any town is refused, and so is one whose upper floor
//      is in a child region of the town (made before the site, or after it and caught at assignment).
//   8. Storage is the AOS budget: twice the lockdowns as in every stock placement entry; a lockdown and a filled secure
//      container both fit, and the container still takes more.
//   9. A town's addon container (which a house owner could chop into a deed) refuses the site, even when only one of
//      its components reaches inside, at making and again at assignment; one beside the building does not.
//  10. The street spot is where strangers are put, so it must be walkable (a surface, room to stand, no other house):
//      a spot in the air or in a wall is refused at making, and a wall built there later at assignment; on a good spot
//      a stranger walking in, or logging in inside, lands on it.
//  11. Another multi is found by what it occupies, not where it stands: one standing outside with a piece inside the
//      building is refused (at making and at assignment); one wholly beside it, or reaching over above the top floor,
//      is not.
//  12. A linked town door goes with its partner or not at all: one leaf of a pair registered alone stops the lease (the
//      other leaf would open the hidden one), and with both the lease goes ahead.

using System;
using System.Collections.Generic;
using System.Reflection;
using Server;
using Server.Commands;
using Server.Accounting;
using Server.Items;
using Server.Leaseholds;
using Server.Mobiles;
using Server.Multis;
using Server.Regions;
using Server.Targeting;
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
    private readonly Func<bool> _wasTestShard = ClusterFLeaseSite.IsTestShard;

    public LeaseholdVerification()
    {
        ShardTestHost.EnsureAccounts();
        ClusterFLeaseSite.IsTestShard = () => true;
        // A town as the shard's are: a TownRegion (a GuardedRegion), here from x 5950 to 6150 and y 60 to 260.
        _town = new TownRegion("P634 Town", Map.Trammel, 50, Region.ConvertTo3D(new Rectangle2D(5950, 60, 200, 200)));
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

        // Sites a staff command made in this town (the facts track only the ones they make themselves).
        foreach (var site in ClusterFLeaseSite.All.ToArray())
        {
            if (site.Map == Map.Trammel && site.X >= 5950 && site.X < 6400 && site.Y >= 60 && site.Y < 260)
            {
                site.Delete();
            }
        }

        foreach (var a in _accounts)
        {
            a.Delete();
        }

        _town.Unregister();
        ClusterFLeaseSite.IsTestShard = _wasTestShard;
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

    private PlayerMobile Staff()
    {
        var gm = Track(new PlayerMobile { Player = true, Race = Race.Human, Name = "P647 staff", AccessLevel = AccessLevel.GameMaster });
        gm.MoveToWorld(new Point3D(Street.X + 1, Street.Y + 2, 0), Map.Trammel);
        return gm;
    }

    // A multi that is not a house, with the pieces it is given (the host has no multi data).
    private sealed class PieceMulti : BaseMulti
    {
        private readonly MultiComponentList _pieces;

        public PieceMulti(params (short X, short Y, short Z)[] pieces) : base(0x1)
        {
            var list = new List<MultiTileEntry>();
            foreach (var (x, y, z) in pieces)
            {
                list.Add(new MultiTileEntry(0x80, x, y, z, (TileFlag)1));
            }

            _pieces = new MultiComponentList(list);
        }

        public override MultiComponentList Components => _pieces ?? MultiComponentList.Empty;
    }

    private static readonly MethodInfo Command =
        typeof(ClusterFLeaseSite).GetMethod("OnCommand", BindingFlags.NonPublic | BindingFlags.Static)!;

    private static void Run(Mobile from, params string[] args) =>
        Command.Invoke(null, [new CommandEventArgs(from, "Leaseholds", string.Join(" ", args), args)]);

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

    // ---------------------------------------------------------------- 6

    [Fact]
    public void OnlyTheTestShardMakesOrLeasesSites()
    {
        var gm = Staff();
        var before = ClusterFLeaseSite.All.Count;

        // Elsewhere: no target, and the methods refuse.
        ClusterFLeaseSite.IsTestShard = () => false;
        gm.Target = null;
        Run(gm, "new", "0", "20");
        Assert.Null(gm.Target);
        Assert.Null(ClusterFLeaseSite.Create(Map.Trammel, Building, 0, 20, Street, out var why));
        Assert.Contains("test shard", why);
        Assert.Equal(before, ClusterFLeaseSite.All.Count);

        // A "new" begun on the test shard and finished elsewhere makes nothing.
        ClusterFLeaseSite.IsTestShard = () => true;
        Run(gm, "new", "0", "20");
        gm.Target!.Invoke(gm, new LandTarget(new Point3D(6008, 120, 0), Map.Trammel));
        gm.Target!.Invoke(gm, new LandTarget(new Point3D(6023, 135, 0), Map.Trammel));
        ClusterFLeaseSite.IsTestShard = () => false;
        gm.Target!.Invoke(gm, new LandTarget(Street, Map.Trammel));
        Assert.Equal(before, ClusterFLeaseSite.All.Count);

        // An "assign" begun on the test shard and finished elsewhere leases nothing; nor do the methods.
        ClusterFLeaseSite.IsTestShard = () => true;
        var site = Site(Building, Street);
        var door = Track(new DarkWoodDoor(DoorFacing.WestCW));
        door.MoveToWorld(new Point3D(6014, 135, 0), Map.Trammel);
        var pm = Player();
        Run(gm, "assign");
        gm.Target!.Invoke(gm, site);
        ClusterFLeaseSite.IsTestShard = () => false;
        gm.Target!.Invoke(gm, pm);
        Assert.False(site.IsLeased);
        Assert.Contains("test shard", site.AssignTest(pm));
        Assert.Contains("test shard", site.AddDoor(door));
        Assert.False(site.IsLeased);
        Assert.Empty(site.Doors);
    }

    // ---------------------------------------------------------------- 7

    [Fact]
    public void TheWholeVolumeMustBeInTheGuardedTown()
    {
        var map = Map.Trammel;

        // Wilderness: one region (the facet's) everywhere, but no town and no guards.
        Assert.Contains("guarded town", ClusterFLeaseSite.WhyNotSite(map, new Rectangle2D(6300, 120, 5, 5), 0, 20, new Point3D(6301, 126, 0)));

        // An upper floor in a child of the town (an inn room upstairs): the ground floor alone would pass.
        var loft = new Region("P647 loft", map, _town, 60, new Rectangle3D(new Point3D(6010, 122, 10), new Point3D(6014, 126, 30)));
        loft.Register();
        try
        {
            Assert.Contains("another region", ClusterFLeaseSite.WhyNotSite(map, Building, 0, 20, Street));
            Assert.Null(ClusterFLeaseSite.WhyNotSite(map, Building, 0, 9, Street));
        }
        finally
        {
            loft.Unregister();
        }

        var site = Site(Building, Street);
        loft.Register();
        try
        {
            Assert.Contains("another region", site.AssignTest(Player()));
            Assert.False(site.IsLeased);
        }
        finally
        {
            loft.Unregister();
        }
    }

    // ---------------------------------------------------------------- 8

    [Fact]
    public void LockdownsAndAFilledSecureFitTheStorage()
    {
        var pm = Player();
        var house = Leased(Site(Building, Street), pm);
        Assert.True(house.IsAosRules);

        var chair = Track(new WoodenChair());
        chair.MoveToWorld(new Point3D(6012, 125, 0), Map.Trammel);
        Assert.True(house.LockDown(pm, chair));
        Assert.True(house.HasLockedDownItem(chair));

        var chest = Track(new WoodenChest());
        for (var i = 0; i < 40; i++)
        {
            chest.AddItem(new Bandage());
        }

        chest.MoveToWorld(new Point3D(6013, 125, 0), Map.Trammel);
        Assert.Equal(40, chest.TotalItems);
        house.AddSecure(pm, chest);
        Assert.True(house.HasSecureItem(chest));

        var more = new Bandage();
        Assert.True(BaseHouse.CheckHold(pm, chest, more, false, true, 0, 0));
        more.Delete();
        Assert.True(house.CheckAosStorage(100));
        Assert.Equal(2 * house.GetAosMaxLockdowns(), house.GetAosMaxSecures());
    }

    // ---------------------------------------------------------------- 9

    [Fact]
    public void TownAddonContainersKeepASiteOut()
    {
        var map = Map.Trammel;

        // The east bookshelf's second piece stands one tile west of it: here on the building's east edge (x 6023).
        var shelf = Track(new ArcaneBookshelfEastAddon());
        shelf.MoveToWorld(new Point3D(6024, 125, 0), map);
        Assert.Contains("fixture", ClusterFLeaseSite.WhyNotSite(map, Building, 0, 20, Street));

        shelf.MoveToWorld(new Point3D(6026, 125, 0), map);
        Assert.Null(ClusterFLeaseSite.WhyNotSite(map, Building, 0, 20, Street));

        var site = Site(Building, Street);
        shelf.MoveToWorld(new Point3D(6015, 125, 0), map);
        Assert.Contains("fixture", site.AssignTest(Player()));
        Assert.False(site.IsLeased);
    }

    // ---------------------------------------------------------------- 10

    [Fact]
    public void TheStreetSpotIsWalkable()
    {
        var map = Map.Trammel;
        using var tiles = TestTileRows.Seed(TileRows.StoneWall);

        // In the air (the host's land is flat at z 0), then in a wall.
        Assert.Contains("walkable", ClusterFLeaseSite.WhyNotSite(map, Building, 0, 20, new Point3D(Street.X, Street.Y, 40)));
        var wall = Track(new Static(0x0080));
        wall.MoveToWorld(Street, map);
        Assert.Contains("walkable", ClusterFLeaseSite.WhyNotSite(map, Building, 0, 20, Street));

        // A wall built on the spot after the site was made stops the lease.
        wall.Internalize();
        var site = Site(Building, Street);
        wall.MoveToWorld(Street, map);
        Assert.Contains("walkable", site.AssignTest(Player()));
        wall.Delete();

        // On the good spot: a stranger walking in, or logging in inside, is put there.
        var house = Leased(site, Player());
        Assert.Equal(Street, house.BanLocation);
        var stranger = Player();
        stranger.Location = new Point3D(6015, 128, 0);
        Assert.Equal(Street, stranger.Location);
        stranger.Internalize();
        stranger.MoveToWorld(new Point3D(6015, 128, 0), map);
        HouseRegion.OnLogin(stranger);
        Assert.Equal(Street, stranger.Location);
    }

    // ---------------------------------------------------------------- 11

    [Fact]
    public void OtherMultisAreFoundByWhatTheyOccupy()
    {
        var map = Map.Trammel;

        // Standing two tiles west of the building (x 6006), a piece three tiles east of that is inside (x 6009).
        var reaching = Track(new PieceMulti((0, 0, 0), (3, 0, 0)));
        reaching.MoveToWorld(new Point3D(6006, 125, 0), map);
        Assert.Contains("PieceMulti", ClusterFLeaseSite.WhyNotSite(map, Building, 0, 20, Street));
        reaching.Internalize();

        var beside = Track(new PieceMulti((0, 0, 0), (1, 0, 0)));
        beside.MoveToWorld(new Point3D(6006, 125, 0), map);
        var over = Track(new PieceMulti((0, 0, 0), (3, 0, 30)));
        over.MoveToWorld(new Point3D(6006, 127, 0), map);
        Assert.Null(ClusterFLeaseSite.WhyNotSite(map, Building, 0, 20, Street));

        var site = Site(Building, Street);
        reaching.MoveToWorld(new Point3D(6006, 125, 0), map);
        Assert.Contains("PieceMulti", site.AssignTest(Player()));
        Assert.False(site.IsLeased);
    }

    // ---------------------------------------------------------------- 12

    [Fact]
    public void ALinkedDoorNeedsItsPartner()
    {
        var site = Site(Building, Street);
        var west = Track(new DarkWoodDoor(DoorFacing.WestCW));
        var east = Track(new DarkWoodDoor(DoorFacing.EastCCW));
        west.MoveToWorld(new Point3D(6014, 135, 0), Map.Trammel);
        east.MoveToWorld(new Point3D(6015, 135, 0), Map.Trammel);
        west.Link = east;
        east.Link = west;

        Assert.Null(site.AddDoor(west));
        Assert.Contains("linked", site.AssignTest(Player()));
        Assert.False(site.IsLeased);
        Assert.Equal(Map.Trammel, west.Map);
        Assert.False(west.Open);

        Assert.Null(site.AddDoor(east));
        var house = Leased(site, Player());
        Assert.Equal(2, house.Doors.Count);
        Assert.Equal(Map.Internal, west.Map);
        Assert.Equal(Map.Internal, east.Map);
    }
}
