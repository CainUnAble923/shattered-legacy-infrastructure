// cc-P857: the carpet deed (customizations/Items/Addons/VariableCarpet.cs), after Manu, Draco van Peeble and Milva.
//
// Facts:
//   1. The six styles are stock carpet art; a carpet lays its corners, edges and middle where they belong, one tile
//      per part, in its colour; the deed is "a carpet deed" with no stock one-click addon.
//   2. The size cap: 3 tiles a side at least, variableCarpet.maxSide (default 18) at most.
//   3. The gump: the deed sends it only from the pack; a colour button redraws it in that colour; a style button
//      starts the corner targets.
//   4. Two corners in the owner's house lay the carpet: every tile, in the house's addons, the deed used up.
//   5. Every refusal keeps the deed and lays nothing: not the owner, outside a house, too small, too big, two floors,
//      someone on the area, the deed out of the pack.
//   6. Taking it up: the owner within 3 tiles gets the confirm, a visitor and a far owner do not; yes rolls it up into
//      a fresh deed, cancel leaves it, and yes re-checks the owner.
//   7. Chopping it with an axe (stock OnChop) also gives the deed back.
//   8. The carpet's style and size and the deed survive a save.
//
// Host notes: the house is the synthetic-floor SmallOldHouse of RaisedGardenVerification (the host has no multi data),
// at its own spot. The cap is set through VariableCarpetSettings.MaxSideOverride, never by writing modernuo.json.

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Multis;
using Server.Network;
using Server.Targeting;
using Server.Tests.Network;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class VariableCarpetVerification
{
    private static readonly Point3D Spot = new(1440, 1480, 0);

    public VariableCarpetVerification()
    {
        ShardTestHost.EnsureSkillChecks();
    }

    private sealed class TestHouse : SmallOldHouse
    {
        private static readonly MultiComponentList _floor = BuildFloor();

        public TestHouse(Mobile owner) : base(owner, 0x64)
        {
        }

        public override MultiComponentList Components => _floor;
        public override int GetAosMaxSecures() => 425;
        public override int GetAosMaxLockdowns() => 212;

        private static MultiComponentList BuildFloor()
        {
            var tiles = new List<MultiTileEntry>();
            for (short x = -3; x <= 3; x++)
            {
                for (short y = -3; y <= 3; y++)
                {
                    tiles.Add(new MultiTileEntry(0x4B8, x, y, 0, (TileFlag)1));
                }
            }

            return new MultiComponentList(tiles);
        }
    }

    private sealed class Home : IDisposable
    {
        public readonly PlayerMobile Owner;
        public readonly PlayerMobile Visitor;
        public readonly TestHouse House;
        public readonly NetState OwnerNs;
        public readonly List<Item> Loose = new();

        // The north corner of a 3 by 3 carpet that fits, and its far corner; the owner stands 2 tiles off its south.
        public Point3D A => new(Spot.X - 2, Spot.Y - 2, 0);
        public Point3D B => new(Spot.X, Spot.Y, 0);

        public Home()
        {
            ShardTestClock.Arm();
            Owner = NewPlayer(new Point3D(Spot.X + 2, Spot.Y + 2, 0));
            Visitor = NewPlayer(new Point3D(Spot.X + 3, Spot.Y - 3, 0));
            House = new TestHouse(Owner);
            House.MoveToWorld(Spot, Map.Trammel);
            OwnerNs = PacketTestUtilities.CreateTestNetState();
            OwnerNs.Mobile = Owner;
            Owner.NetState = OwnerNs;
        }

        public VariableCarpetDeed Deed(PlayerMobile who = null)
        {
            var deed = new VariableCarpetDeed();
            (who ?? Owner).Backpack.DropItem(deed);
            Loose.Add(deed);
            return deed;
        }

        public VariableCarpetAddon CarpetIn()
        {
            var carpet = House.Addons.OfType<VariableCarpetAddon>().FirstOrDefault();
            if (carpet != null)
            {
                Loose.Add(carpet);
            }

            return carpet;
        }

        public void Dispose()
        {
            VariableCarpetSettings.MaxSideOverride = null;
            Owner.NetState = null;

            foreach (var item in Loose)
            {
                item.Delete();
            }

            foreach (var addon in House.Addons.ToList())
            {
                addon.Delete();
            }

            House.Delete();
            foreach (var pm in new[] { Owner, Visitor })
            {
                foreach (var item in new List<Item>(pm.Backpack.Items))
                {
                    item.Delete();
                }

                pm.Delete();
            }
        }
    }

    private static PlayerMobile NewPlayer(Point3D at)
    {
        var pm = new PlayerMobile { Player = true, Race = Race.Human };
        pm.AddItem(new Backpack());
        pm.MoveToWorld(at, Map.Trammel);
        return pm;
    }

    private static RelayInfo Button(int id) => new(id, default, default, default, default);

    private static int CarpetsAround()
    {
        var count = 0;
        foreach (var _ in Map.Trammel.GetItemsInRange<VariableCarpetAddon>(Spot, 6))
        {
            count++;
        }

        return count;
    }

    // ---- 1 ------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheStylesAreStockCarpetArtLaidCornerEdgeAndMiddle()
    {
        var styles = VariableCarpetSettings.Styles;
        Assert.Equal(6, styles.Length);
        foreach (var s in styles)
        {
            var parts = new[]
            {
                s.NorthCorner, s.EastCorner, s.SouthCorner, s.WestCorner, s.NorthWestEdge, s.NorthEastEdge,
                s.SouthEastEdge, s.SouthWestEdge, s.Middle
            };
            Assert.All(parts, id => Assert.InRange(id, 0xABD, 0xAF9)); // the stock carpet art
            Assert.Equal(8, parts.Take(8).Distinct().Count()); // every border part is its own piece
        }

        var look = styles[3];
        var carpet = new VariableCarpetAddon(3, 5, 4) { Hue = 1157 };
        try
        {
            Assert.Equal(20, carpet.Components.Count);
            int At(int x, int y) => carpet.Components.Single(c => c.Offset == new Point3D(x, y, 0)).ItemID;

            Assert.Equal(look.NorthCorner, At(0, 0));
            Assert.Equal(look.EastCorner, At(4, 0));
            Assert.Equal(look.SouthCorner, At(4, 3));
            Assert.Equal(look.WestCorner, At(0, 3));
            Assert.Equal(look.NorthWestEdge, At(0, 1));
            Assert.Equal(look.NorthEastEdge, At(2, 0));
            Assert.Equal(look.SouthEastEdge, At(4, 2));
            Assert.Equal(look.SouthWestEdge, At(1, 3));
            Assert.Equal(look.Middle, At(2, 2));
            Assert.All(carpet.Components, c => Assert.Equal(1157, c.Hue));
            Assert.Equal(3, carpet.Style);
            Assert.Equal(5, carpet.Width);
            Assert.Equal(4, carpet.Height);
            Assert.IsType<VariableCarpetDeed>(carpet.Deed);
        }
        finally
        {
            carpet.Delete();
        }

        var deed = new VariableCarpetDeed();
        try
        {
            Assert.Equal("a carpet deed", deed.Name ?? deed.DefaultName);
            Assert.Null(deed.Addon);
        }
        finally
        {
            deed.Delete();
        }

        Assert.Equal(0, VariableCarpetSettings.HueAt(0));
        Assert.Equal(7, VariableCarpetSettings.Hues.Length);
    }

    // ---- 2 ------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheSizeCapIsThreeToMaxSide()
    {
        try
        {
            Assert.Equal(18, VariableCarpetSettings.DefaultMaxSide);
            VariableCarpetSettings.MaxSideOverride = 18;
            Assert.Equal(VariableCarpetResult.TooSmall, VariableCarpetSettings.CheckSize(2, 5));
            Assert.Equal(VariableCarpetResult.TooSmall, VariableCarpetSettings.CheckSize(5, 2));
            Assert.Equal(VariableCarpetResult.Placed, VariableCarpetSettings.CheckSize(3, 3));
            Assert.Equal(VariableCarpetResult.Placed, VariableCarpetSettings.CheckSize(18, 18));
            Assert.Equal(VariableCarpetResult.TooBig, VariableCarpetSettings.CheckSize(19, 4));
            Assert.Equal(VariableCarpetResult.TooBig, VariableCarpetSettings.CheckSize(4, 19));

            VariableCarpetSettings.MaxSideOverride = 6;
            Assert.Equal(VariableCarpetResult.TooBig, VariableCarpetSettings.CheckSize(7, 3));
            Assert.Equal(VariableCarpetResult.Placed, VariableCarpetSettings.CheckSize(6, 6));

            VariableCarpetSettings.MaxSideOverride = 1; // never below the minimum
            Assert.Equal(VariableCarpetSettings.MinSide, VariableCarpetSettings.MaxSide);
        }
        finally
        {
            VariableCarpetSettings.MaxSideOverride = null;
        }
    }

    // ---- 3 ------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheGumpPicksAColourThenAStyleStartsTheTargets()
    {
        using var h = new Home();
        var deed = new VariableCarpetDeed();
        h.Loose.Add(deed);
        deed.MoveToWorld(new Point3D(Spot.X + 2, Spot.Y + 1, 0), Map.Trammel);

        deed.OnDoubleClick(h.Owner); // on the floor: refused
        Assert.Null(h.Owner.FindGump<VariableCarpetGump>());

        h.Owner.Backpack.DropItem(deed);
        deed.OnDoubleClick(h.Owner);
        var gump = h.Owner.FindGump<VariableCarpetGump>();
        Assert.NotNull(gump);
        Assert.Equal(0, gump.HueIndex);

        gump.OnResponse(h.OwnerNs, Button(VariableCarpetGump.HueButtonBase + 3));
        var wine = h.Owner.FindGump<VariableCarpetGump>();
        Assert.NotNull(wine);
        Assert.Equal(3, wine.HueIndex);
        Assert.Null(h.Owner.Target);

        wine.OnResponse(h.OwnerNs, Button(VariableCarpetGump.StyleButtonBase + 2));
        Assert.NotNull(h.Owner.Target);
        Assert.False(deed.Deleted);
    }

    // ---- 4 ------------------------------------------------------------------------------------------------------

    [Fact]
    public void TwoCornersInTheOwnersHouseLayTheCarpet()
    {
        using var h = new Home();
        var deed = h.Deed();

        deed.BeginPlace(h.Owner, 1, 4); // blue plain, sapphire
        Assert.NotNull(h.Owner.Target);
        h.Owner.Target.Invoke(h.Owner, new LandTarget(h.B, Map.Trammel)); // corners in either order
        Assert.NotNull(h.Owner.Target);
        h.Owner.Target.Invoke(h.Owner, new LandTarget(h.A, Map.Trammel));

        var carpet = h.CarpetIn();
        Assert.NotNull(carpet);
        Assert.True(deed.Deleted);
        Assert.Equal(h.A, carpet.Location);
        Assert.Equal(Map.Trammel, carpet.Map);
        Assert.Equal(1, carpet.Style);
        Assert.Equal(3, carpet.Width);
        Assert.Equal(3, carpet.Height);
        Assert.Equal(9, carpet.Components.Count);
        Assert.All(carpet.Components, c =>
        {
            Assert.Equal(1266, c.Hue);
            Assert.Equal(h.A.Z, c.Z);
            Assert.InRange(c.X, h.A.X, h.B.X);
            Assert.InRange(c.Y, h.A.Y, h.B.Y);
            Assert.Same(h.House, BaseHouse.FindHouseAt(c));
        });
        Assert.Equal(VariableCarpetSettings.Styles[1].Middle, carpet.Components.Single(c => c.Offset == new Point3D(1, 1, 0)).ItemID);
    }

    // ---- 5 ------------------------------------------------------------------------------------------------------

    [Fact]
    public void EveryRefusalKeepsTheDeedAndLaysNothing()
    {
        using var h = new Home();
        var before = CarpetsAround();

        // Not the owner: the visitor's own deed, in the owner's house.
        var visitorsDeed = h.Deed(h.Visitor);
        Assert.Equal(VariableCarpetResult.NotInHouse, visitorsDeed.Place(h.Visitor, Map.Trammel, h.A, h.B, 0, 0));
        Assert.False(visitorsDeed.Deleted);

        var deed = h.Deed();

        // Outside any house (the floor runs 3 tiles round the spot).
        var outA = new Point3D(Spot.X + 5, Spot.Y + 5, 0);
        Assert.Equal(VariableCarpetResult.NotInHouse,
            deed.Place(h.Owner, Map.Trammel, outA, new Point3D(outA.X + 2, outA.Y + 2, 0), 0, 0));

        // Too small, too big.
        Assert.Equal(VariableCarpetResult.TooSmall,
            deed.Place(h.Owner, Map.Trammel, h.A, new Point3D(h.A.X + 1, h.A.Y + 2, 0), 0, 0));
        VariableCarpetSettings.MaxSideOverride = 4;
        Assert.Equal(VariableCarpetResult.TooBig,
            deed.Place(h.Owner, Map.Trammel, new Point3D(Spot.X - 3, Spot.Y - 3, 0), new Point3D(Spot.X + 1, Spot.Y - 1, 0), 0, 0));
        VariableCarpetSettings.MaxSideOverride = null;

        // Two floors.
        Assert.Equal(VariableCarpetResult.TwoFloors,
            deed.Place(h.Owner, Map.Trammel, h.A, new Point3D(h.B.X, h.B.Y, 20), 0, 0));

        // Someone on the area: the owner, then the visitor.
        h.Owner.MoveToWorld(new Point3D(Spot.X - 1, Spot.Y - 1, 0), Map.Trammel);
        Assert.Equal(VariableCarpetResult.Occupied, deed.Place(h.Owner, Map.Trammel, h.A, h.B, 0, 0));
        h.Owner.MoveToWorld(new Point3D(Spot.X + 2, Spot.Y + 2, 0), Map.Trammel);
        h.House.Friends.Add(h.Visitor); // a friend may walk in (a stranger is put out by the house), but is not the owner
        h.Visitor.MoveToWorld(h.A, Map.Trammel);
        Assert.Equal(h.A, h.Visitor.Location);
        Assert.Equal(VariableCarpetResult.Occupied, deed.Place(h.Owner, Map.Trammel, h.A, h.B, 0, 0));
        h.Visitor.MoveToWorld(new Point3D(Spot.X + 3, Spot.Y - 3, 0), Map.Trammel);

        // The deed left the pack between the gump and the corners.
        deed.MoveToWorld(new Point3D(Spot.X + 2, Spot.Y + 1, 0), Map.Trammel);
        Assert.Equal(VariableCarpetResult.NotInPack, deed.Place(h.Owner, Map.Trammel, h.A, h.B, 0, 0));

        Assert.False(deed.Deleted);
        Assert.Equal(before, CarpetsAround());
        Assert.Empty(h.House.Addons.OfType<VariableCarpetAddon>());

        // And with every check met, the same deed lays it.
        h.Owner.Backpack.DropItem(deed);
        Assert.Equal(VariableCarpetResult.Placed, deed.Place(h.Owner, Map.Trammel, h.A, h.B, 0, 0));
        Assert.NotNull(h.CarpetIn());
    }

    // ---- 6 ------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheOwnerRollsItUpThroughTheConfirm()
    {
        using var h = new Home();
        Assert.Equal(VariableCarpetResult.Placed, h.Deed().Place(h.Owner, Map.Trammel, h.A, h.B, 5, 6));
        var carpet = h.CarpetIn();
        var piece = carpet.Components[4];

        // A visitor: nothing.
        var visitorNs = PacketTestUtilities.CreateTestNetState();
        visitorNs.Mobile = h.Visitor;
        h.Visitor.NetState = visitorNs;
        try
        {
            piece.OnDoubleClick(h.Visitor);
            Assert.Null(h.Visitor.FindGump<VariableCarpetRemoveGump>());
            Assert.False(carpet.TryRemove(h.Visitor));
            Assert.False(carpet.Deleted);
        }
        finally
        {
            h.Visitor.NetState = null;
        }

        // The owner too far away: no confirm.
        h.Owner.MoveToWorld(new Point3D(Spot.X + 6, Spot.Y + 6, 0), Map.Trammel);
        piece.OnDoubleClick(h.Owner);
        Assert.Null(h.Owner.FindGump<VariableCarpetRemoveGump>());

        // Near: the confirm; cancel leaves it.
        h.Owner.MoveToWorld(new Point3D(Spot.X + 2, Spot.Y + 2, 0), Map.Trammel);
        piece.OnDoubleClick(h.Owner);
        var confirm = h.Owner.FindGump<VariableCarpetRemoveGump>();
        Assert.NotNull(confirm);
        confirm.OnResponse(h.OwnerNs, Button(0));
        Assert.False(carpet.Deleted);
        Assert.Empty(h.Owner.Backpack.Items.OfType<VariableCarpetDeed>());

        // Yes: rolled up into a fresh deed, out of the house's addons, every tile gone.
        var parts = carpet.Components.ToList();
        piece.OnDoubleClick(h.Owner);
        h.Owner.FindGump<VariableCarpetRemoveGump>().OnResponse(h.OwnerNs, Button(VariableCarpetRemoveGump.RemoveButton));
        Assert.True(carpet.Deleted);
        Assert.All(parts, c => Assert.True(c.Deleted));
        Assert.DoesNotContain(carpet, h.House.Addons);
        Assert.Single(h.Owner.Backpack.Items.OfType<VariableCarpetDeed>());
    }

    // ---- 7 ------------------------------------------------------------------------------------------------------

    [Fact]
    public void ChoppingItAlsoGivesTheDeedBack()
    {
        using var h = new Home();
        Assert.Equal(VariableCarpetResult.Placed, h.Deed().Place(h.Owner, Map.Trammel, h.A, h.B, 0, 0));
        var carpet = h.CarpetIn();

        carpet.OnChop(h.Visitor);
        Assert.False(carpet.Deleted);

        carpet.OnChop(h.Owner);
        Assert.True(carpet.Deleted);
        Assert.DoesNotContain(carpet, h.House.Addons);
        Assert.Single(h.Owner.Backpack.Items.OfType<VariableCarpetDeed>());
    }

    // ---- 8 ------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheCarpetAndTheDeedSurviveASave()
    {
        var carpet = new VariableCarpetAddon(4, 7, 9);
        var copy = ConsumablesVerification.RoundTrip(carpet, s => new VariableCarpetAddon(s));
        try
        {
            Assert.Equal(4, copy.Style);
            Assert.Equal(7, copy.Width);
            Assert.Equal(9, copy.Height);
        }
        finally
        {
            copy.Delete();
        }

        var deed = new VariableCarpetDeed();
        var deedCopy = ConsumablesVerification.RoundTrip(deed, s => new VariableCarpetDeed(s));
        try
        {
            Assert.Equal("a carpet deed", deedCopy.Name ?? deedCopy.DefaultName);
        }
        finally
        {
            deedCopy.Delete();
        }
    }
}
