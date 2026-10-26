// cc-P634: Town Houses (leaseholds), stage 1: the leasehold house, its region and its doors. Design: shard-migration
// notes/cc-P409-town-houses-design.md, corrected by notes/cc-P550-review-town-houses-design.md; build notes
// notes/cc-P634-build-town-houses.md. Chase (10-25): the review's defaults (weekly rent first with sublets, room tenants
// count toward the one house, belongings protected, deadlines count server uptime). Stage 1 is the core and the staff
// tools only: no rent, no player can lease (ClusterFLeaseSite.cs).
//
// A leasehold is a real BaseHouse over one rectangle of an existing town building, between two floor heights, so the
// engine treats it as a house everywhere (the house gump, friends, lockdowns, bans, and the account's one house). What
// differs from a stock house:
// - The footprint: an id the client has no multi for (nothing drawn), and a per-instance component list with no tiles
//   but the rectangle's real bounds, so the map registers the house in every sector the building covers and FindHouseAt
//   finds it anywhere inside (P550 correction 2: an empty list resized keeps zero bounds). Built through the list's own
//   reader constructor; IsInside is the rectangle and the floor range.
// - The region: the town region at the street spot is its parent (BaseHouse-leasehold-region.patch), so the town's
//   guards answer inside, and its area is the rectangle and the floor range, not the full height.
// - Storage (cc-P647): the AOS item budget is twice the lockdowns, the stock entries' ratio; both stay provisional.
// - Decay: ageless; a lease ends only through its site. Price 0 and no deed, so a player's demolish is refused and
//   nothing is ever refunded. No vendors.
// - Deleting it for any reason (staff, demolish by staff, account deletion) tells the site, which puts the town's own
//   doors back.

#nullable enable

using System;
using ModernUO.Serialization;
using Server.Items;
using Server.Multis;
using Server.Regions;

namespace Server.Leaseholds;

[SerializationGenerator(0, false)]
public partial class ClusterFLeasehold : BaseHouse
{
    // No client multi has this id (P409: multi.idx has no multi 7638), so the client draws nothing for the house.
    public const int MultiId = 0x1DD6;

    [SerializableField(0, setter: "private")]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private ClusterFLeaseSite? _site;

    // The footprint's size and floor range; the house stands at the rectangle's north-west corner at the lowest floor.
    [SerializableField(1, setter: "private")]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private int _width;

    [SerializableField(2, setter: "private")]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private int _depth;

    [SerializableField(3, setter: "private")]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private int _floors;

    private MultiComponentList? _components;

    // False while loading: the base Item enters the map before these fields are read (Item.Deserialize), so until
    // AfterDeserialization the house answers with the empty list, then leaves and enters again with its own.
    // The id never matters to the server's footprint (Components is ours), only to the client, which draws nothing.
    private bool _ready;

    public ClusterFLeasehold(Mobile owner, ClusterFLeaseSite site) :
        base(MultiId, owner, LockdownsFor(site.Bounds), SecuresFor(site.Bounds))
    {
        _site = site;
        _width = site.Bounds.Width;
        _depth = site.Bounds.Height;
        _floors = site.ZHigh - site.ZLow;
        _ready = true;
        RelativeBanLocation = new Point3D(site.X - site.Bounds.X, site.Y - site.Bounds.Y, site.Z - site.ZLow);
    }

    // Provisional storage until the lease terms stage: about a stock house's per floor tile.
    public static int LockdownsFor(Rectangle2D bounds) => Math.Max(1, bounds.Width * bounds.Height * 8);
    public static int SecuresFor(Rectangle2D bounds) => Math.Max(1, bounds.Width * bounds.Height / 16);

    public override Rectangle2D[] Area => _width > 0 ? [new Rectangle2D(0, 0, _width, _depth)] : [];

    public override Point3D BaseBanLocation => Point3D.Zero;

    public override MultiComponentList Components =>
        _ready && _width > 0 ? _components ??= BuildComponents(_width, _depth) : MultiComponentList.Empty;

    /// <summary>A list with no tiles whose bounds are 0,0 to width-1,depth-1 (the house stands at the corner).</summary>
    public static MultiComponentList BuildComponents(int width, int depth)
    {
        var buffer = new byte[64];
        var writer = new BufferWriter(buffer, true);
        writer.Write(1);                                  // the list's format version
        writer.Write(new Point2D(0, 0));                  // min
        writer.Write(new Point2D(width - 1, depth - 1));  // max
        writer.Write(new Point2D(0, 0));                  // center
        writer.Write(width);
        writer.Write(depth);
        writer.Write(0);                                  // no tiles
        return new MultiComponentList(new BufferReader(buffer));
    }

    public override bool IsInside(Point3D p, int height) =>
        !Deleted && _width > 0 && p.X >= X && p.X < X + _width && p.Y >= Y && p.Y < Y + _depth &&
        p.Z >= Z && p.Z <= Z + _floors;

    protected override HouseRegion? CreateRegion()
    {
        if (!_ready || _width <= 0)
        {
            return null;
        }

        var parent = FindTownRegion();
        ParentMissing = parent?.Name == null || parent.Name != Site?.ParentName;
        return new ClusterFLeaseholdRegion(this, parent, new Rectangle3D(X, Y, Z, _width, _depth, _floors + 1));
    }

    /// <summary>True when the region at the street spot is not the one the site was made in (staff must check).</summary>
    public bool ParentMissing { get; private set; }

    // The region at the street spot, never this or another house's region (UpdateRegion unregisters the old one first).
    private Region? FindTownRegion()
    {
        var r = Region.Find(BanLocation, Map);
        while (r is HouseRegion)
        {
            r = r.Parent;
        }

        return r == null || r == Map?.DefaultRegion ? null : r;
    }

    public override DecayType DecayType => DecayType.Ageless;

    public override int GetAosMaxLockdowns() => MaxLockDowns;

    // cc-P647: under AOS rules GetAosMaxSecures is not a count of secure containers but the house's whole item storage
    // (lockdowns, secures and everything inside them, BaseHouse.CheckAosStorage). Every stock HousePlacementEntry gives
    // twice its lockdowns (425 and 212 for a small house, 4076 and 2038 for a castle), so the leasehold does the same.
    public override int GetAosMaxSecures() => 2 * MaxLockDowns;
    public override int GetNewVendorSystemMaxVendors() => 0;

    public override void OnAfterDelete()
    {
        base.OnAfterDelete();
        Site?.OnLeaseholdDeleted(this);
    }

    [AfterDeserialization]
    private void AfterDeserialization()
    {
        // The map's sector lists are reached only through BaseMulti.ItemID, which leaves the map with the current list
        // and enters again with the new one: first a step with the empty list (the sector it entered while loading),
        // then back to the leasehold's id with its whole footprint.
        ItemID = MultiId + 1;
        _ready = true;
        ItemID = MultiId;

        UpdateRegion();
    }
}

/// <summary>A leasehold's region: a house region whose parent is the town region it stands in.</summary>
public class ClusterFLeaseholdRegion : HouseRegion
{
    public ClusterFLeaseholdRegion(BaseHouse house, Region? parent, Rectangle3D area)
        : base(house, parent, HousePriority, [area])
    {
    }
}

/// <summary>A leasehold's door: a house door made in the art and swing of the town door it stands in for.</summary>
[SerializationGenerator(0, false)]
public partial class ClusterFLeaseDoor : BaseHouseDoor
{
    public ClusterFLeaseDoor(BaseDoor original) : base(
        DoorFacing.WestCW,
        original.ClosedId,
        original.OpenedId,
        original.OpenedSound,
        original.ClosedSound,
        original.Offset
    ) => Hue = original.Hue;
}
