// VariableCarpet.cs (cc-P857): a carpet deed for houses. Double-click the deed, pick a colour and a style in the gump,
// then target two opposite corners on your house floor: a carpet of that size goes down, from 3 to
// variableCarpet.maxSide tiles a side (modernuo.json, default 18). Only the house owner places one, every tile must
// fit (the stock addon checks: in the owner's house, not blocked, not next to a door), and nobody may stand on the
// area. The owner double-clicks the carpet within 3 tiles for a confirm, and it goes back into the pack as a deed;
// chopping it with an axe does the same (stock BaseAddon.OnChop).
//
// Credit: after the Variable Carpet Addon Deed by Manu (Splitterwelt.com, 1.0), updated by Draco van Peeble (2.0) and
// posted to servuo.dev by Milva (2013); the idea itself came from dracana's winecrafting grounds. Rewritten for
// ModernUO as our own code: the style table, the gumps and the checks are new; only the stock carpet art IDs are shared.
// Notes: shard-migration notes/cc-P857-variable-carpet.md.

#nullable enable

using System;
using ModernUO.Serialization;
using Server.Gumps;
using Server.Multis;
using Server.Network;
using Server.Spells;
using Server.Targeting;

namespace Server.Items;

/// <summary>One carpet look: the stock art for its four corners, four edges and middle.</summary>
public sealed record VariableCarpetStyle(
    string Name,
    int NorthCorner, int EastCorner, int SouthCorner, int WestCorner,
    int NorthWestEdge, int NorthEastEdge, int SouthEastEdge, int SouthWestEdge,
    int Middle
)
{
    /// <summary>
    /// The art for the tile at (x, y) of a carpet whose far tile is (lastX, lastY). x runs along the north-east edge,
    /// y along the north-west edge, so (0, 0) is the north corner, the top of the screen.
    /// </summary>
    public int ArtAt(int x, int y, int lastX, int lastY)
    {
        var west = x == 0;
        var east = x == lastX;
        var north = y == 0;
        var south = y == lastY;

        return (west, east, north, south) switch
        {
            (true, _, true, _)  => NorthCorner,
            (_, true, true, _)  => EastCorner,
            (_, true, _, true)  => SouthCorner,
            (true, _, _, true)  => WestCorner,
            (true, _, _, _)     => NorthWestEdge,
            (_, _, true, _)     => NorthEastEdge,
            (_, true, _, _)     => SouthEastEdge,
            (_, _, _, true)     => SouthWestEdge,
            _                   => Middle
        };
    }
}

public static class VariableCarpetSettings
{
    public const string MaxSideKey = "variableCarpet.maxSide";
    public const int MinSide = 3;
    public const int DefaultMaxSide = 18;

    /// <summary>For facts: a cap that wins over the setting while set.</summary>
    public static int? MaxSideOverride { get; set; }

    /// <summary>The longest side a carpet may have, in tiles; never below the 3-tile minimum.</summary>
    public static int MaxSide =>
        Math.Max(MinSide, MaxSideOverride ?? ServerConfiguration.GetOrUpdateSetting(MaxSideKey, DefaultMaxSide));

    // The stock carpet art (0xABD-0xAF9): six looks, the three carpet borders each with one or two middles.
    public static readonly VariableCarpetStyle[] Styles =
    [
        new("Blue, patterned", 0xAC3, 0xAC5, 0xAC2, 0xAC4, 0xAF6, 0xAF7, 0xAF8, 0xAF9, 0xABD),
        new("Blue, plain", 0xAC3, 0xAC5, 0xAC2, 0xAC4, 0xAF6, 0xAF7, 0xAF8, 0xAF9, 0xABE),
        new("Blue with gold", 0xAD3, 0xAD5, 0xAD2, 0xAD4, 0xAD6, 0xAD7, 0xAD8, 0xAD9, 0xAD1),
        new("Red, patterned", 0xACA, 0xACC, 0xAC9, 0xACB, 0xACD, 0xACE, 0xACF, 0xAD0, 0xAC7),
        new("Red, plain", 0xACA, 0xACC, 0xAC9, 0xACB, 0xACD, 0xACE, 0xACF, 0xAD0, 0xAC8),
        new("Gold, patterned", 0xADC, 0xADE, 0xADB, 0xADD, 0xADF, 0xAE0, 0xAE1, 0xAE2, 0xADA)
    ];

    // Hue 0 keeps the art's own colours; the rest tint the whole carpet (names checked in our client's hues.mul).
    public static readonly (string Name, int Hue)[] Hues =
    [
        ("As woven", 0),
        ("Charcoal", 1109),
        ("Ice white", 1150),
        ("Wine", 1157),
        ("Sapphire", 1266),
        ("Emerald", 1272),
        ("Gold", 2213)
    ];

    public static VariableCarpetStyle StyleAt(int index) => Styles[Math.Clamp(index, 0, Styles.Length - 1)];

    public static int HueAt(int index) => Hues[Math.Clamp(index, 0, Hues.Length - 1)].Hue;

    /// <summary>Whether a carpet this many tiles across may be laid: Placed when it may, else TooSmall or TooBig.</summary>
    public static VariableCarpetResult CheckSize(int width, int height)
    {
        if (width < MinSide || height < MinSide)
        {
            return VariableCarpetResult.TooSmall;
        }

        var max = MaxSide;
        return width > max || height > max ? VariableCarpetResult.TooBig : VariableCarpetResult.Placed;
    }
}

/// <summary>What became of a placement: Placed, or why not.</summary>
public enum VariableCarpetResult
{
    Placed,
    NotInPack,
    NoMap,
    TwoFloors,
    TooSmall,
    TooBig,
    Occupied,
    NotInHouse,
    DoorTooClose,
    Blocked
}

[SerializationGenerator(0, false)]
public partial class VariableCarpetAddon : BaseAddon
{
    public const int RemoveRange = 3;

    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster, readOnly: true)]
    private int _style;

    [SerializableField(1)]
    [SerializedCommandProperty(AccessLevel.GameMaster, readOnly: true)]
    private int _width;

    [SerializableField(2)]
    [SerializedCommandProperty(AccessLevel.GameMaster, readOnly: true)]
    private int _height;

    [Constructible]
    public VariableCarpetAddon(int style, int width, int height)
    {
        _style = Math.Clamp(style, 0, VariableCarpetSettings.Styles.Length - 1);
        _width = Math.Max(1, width);
        _height = Math.Max(1, height);

        var look = VariableCarpetSettings.StyleAt(_style);

        for (var x = 0; x < _width; x++)
        {
            for (var y = 0; y < _height; y++)
            {
                AddComponent(new AddonComponent(look.ArtAt(x, y, _width - 1, _height - 1)), x, y, 0);
            }
        }
    }

    public override BaseAddonDeed Deed => new VariableCarpetDeed();

    /// <summary>Whether this person may take the carpet up: the owner of the house it lies in.</summary>
    public bool CanRemove(Mobile from) => !Deleted && BaseHouse.FindHouseAt(this)?.IsOwner(from) == true;

    public bool IsNear(Mobile from)
    {
        if (from.Map != Map)
        {
            return false;
        }

        foreach (var c in Components)
        {
            if (from.InRange(c.GetWorldLocation(), RemoveRange))
            {
                return true;
            }
        }

        return false;
    }

    public override void OnComponentUsed(AddonComponent c, Mobile from)
    {
        if (!CanRemove(from))
        {
            return; // a visitor's double-click on a carpet does nothing
        }

        if (!IsNear(from))
        {
            from.SendLocalizedMessage(500295); // You are too far away to do that.
            return;
        }

        from.SendGump(new VariableCarpetRemoveGump(this));
    }

    /// <summary>The confirm's yes: every check again, then the carpet comes up and a fresh deed goes into the pack.</summary>
    public bool TryRemove(Mobile from)
    {
        if (!CanRemove(from))
        {
            return false;
        }

        if (!IsNear(from))
        {
            from.SendLocalizedMessage(500295); // You are too far away to do that.
            return false;
        }

        var house = BaseHouse.FindHouseAt(this);
        house?.Addons.Remove(this);
        Effects.PlaySound(GetWorldLocation(), Map, 0x3B3);
        Delete();

        from.AddToBackpack(new VariableCarpetDeed());
        from.SendMessage("You roll up the carpet. Its deed is in your pack.");
        return true;
    }
}

[SerializationGenerator(0, false)]
public partial class VariableCarpetDeed : BaseAddonDeed
{
    [Constructible]
    public VariableCarpetDeed()
    {
    }

    public override string DefaultName => "a carpet deed";

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add("Lays a carpet of any size on your house floor");
        list.Add("After Manu, Draco van Peeble and Milva");
    }

    // Placing goes through the gump and the corner targets, never the stock one-click target.
    public override BaseAddon? Addon => null;

    public override void OnDoubleClick(Mobile from)
    {
        if (!IsChildOf(from.Backpack))
        {
            from.SendLocalizedMessage(1042001); // That must be in your pack for you to use it.
            return;
        }

        from.SendGump(new VariableCarpetGump(this, 0));
    }

    public void BeginPlace(Mobile from, int style, int hueIndex)
    {
        from.SendMessage("Target one corner of the carpet on your house floor.");
        from.Target = new CornerTarget(this, style, hueIndex, null, null);
    }

    /// <summary>
    /// Lays the carpet between two opposite corners. Every check runs here, server side: the deed in the pack, one
    /// floor, the size, nobody standing on it, and the stock fit (the owner's house, not blocked, not by a door).
    /// The deed is used up only when the carpet is down.
    /// </summary>
    public VariableCarpetResult Place(Mobile from, Map? map, Point3D a, Point3D b, int style, int hueIndex)
    {
        if (Deleted || !IsChildOf(from.Backpack))
        {
            from.SendLocalizedMessage(1042001); // That must be in your pack for you to use it.
            return VariableCarpetResult.NotInPack;
        }

        if (map == null || map == Map.Internal)
        {
            return VariableCarpetResult.NoMap;
        }

        if (a.Z != b.Z)
        {
            from.SendMessage("Both corners must be on the same floor.");
            return VariableCarpetResult.TwoFloors;
        }

        Utility.FixPoints(ref a, ref b);
        var width = b.X - a.X + 1;
        var height = b.Y - a.Y + 1;

        var size = VariableCarpetSettings.CheckSize(width, height);
        if (size == VariableCarpetResult.TooSmall)
        {
            from.SendMessage($"A carpet must be at least {VariableCarpetSettings.MinSide} tiles on each side.");
            return size;
        }

        if (size == VariableCarpetResult.TooBig)
        {
            from.SendMessage($"A carpet can be at most {VariableCarpetSettings.MaxSide} tiles on a side.");
            return size;
        }

        foreach (var m in map.GetMobilesInBounds(new Rectangle2D(a.X, a.Y, width, height)))
        {
            if (m.Z >= a.Z - 2 && m.Z <= a.Z + 16)
            {
                from.SendMessage(m == from
                    ? "Step off the carpet's area first."
                    : "Someone is standing where the carpet would go.");
                return VariableCarpetResult.Occupied;
            }
        }

        var addon = new VariableCarpetAddon(style, width, height) { Hue = VariableCarpetSettings.HueAt(hueIndex) };
        BaseHouse? house = null;
        var res = addon.CouldFit(a, map, from, ref house);

        if (res != AddonFitResult.Valid || house == null)
        {
            addon.Delete();

            switch (res)
            {
                case AddonFitResult.NotInHouse:
                    from.SendLocalizedMessage(500274); // You can only place this in a house that you own!
                    return VariableCarpetResult.NotInHouse;
                case AddonFitResult.DoorTooClose:
                    from.SendLocalizedMessage(500271); // You cannot build near the door.
                    return VariableCarpetResult.DoorTooClose;
                default:
                    from.SendLocalizedMessage(500269); // You cannot build that there.
                    return VariableCarpetResult.Blocked;
            }
        }

        addon.MoveToWorld(a, map);
        house.Addons.Add(addon);
        Delete();

        from.SendMessage($"Your {width} by {height} carpet is down. Double-click it to roll it up again.");
        return VariableCarpetResult.Placed;
    }

    private sealed class CornerTarget : Target
    {
        private readonly VariableCarpetDeed _deed;
        private readonly int _style;
        private readonly int _hueIndex;
        private readonly Point3D? _first;
        private readonly Map? _map;

        public CornerTarget(VariableCarpetDeed deed, int style, int hueIndex, Point3D? first, Map? map)
            : base(-1, true, TargetFlags.None)
        {
            _deed = deed;
            _style = style;
            _hueIndex = hueIndex;
            _first = first;
            _map = map;
            CheckLOS = false;
        }

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (targeted is not IPoint3D ip || _deed.Deleted)
            {
                return;
            }

            if (!_deed.IsChildOf(from.Backpack))
            {
                from.SendLocalizedMessage(1042001); // That must be in your pack for you to use it.
                return;
            }

            SpellHelper.GetSurfaceTop(ref ip);
            var p = new Point3D(ip);

            if (_first is not { } first)
            {
                from.SendMessage("Now target the opposite corner.");
                from.Target = new CornerTarget(_deed, _style, _hueIndex, p, from.Map);
                return;
            }

            if (from.Map != _map)
            {
                from.SendMessage("Both corners must be on the same floor.");
                return;
            }

            _deed.Place(from, _map, first, p, _style, _hueIndex);
        }
    }
}

/// <summary>The deed's gump: a row of colours (each redraws the gump in that colour) and the six styles to place.</summary>
public sealed class VariableCarpetGump : DynamicGump
{
    public const int StyleButtonBase = 1;
    public const int HueButtonBase = 100;

    private const int CellWidth = 150;
    private const int CellHeight = 160;
    private const int LabelHue = 0x480;
    private const int TextHue = 0x7FF;
    private const int PickedHue = 0x35;

    private readonly VariableCarpetDeed _deed;
    private readonly int _hueIndex;

    public override bool Singleton => true;

    public VariableCarpetGump(VariableCarpetDeed deed, int hueIndex) : base(60, 40)
    {
        _deed = deed;
        _hueIndex = Math.Clamp(hueIndex, 0, VariableCarpetSettings.Hues.Length - 1);
    }

    public int HueIndex => _hueIndex;

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        var styles = VariableCarpetSettings.Styles;
        var hues = VariableCarpetSettings.Hues;
        var hue = hues[_hueIndex].Hue;
        var width = 3 * CellWidth + 30;
        var rows = (styles.Length + 2) / 3;
        var top = 82;
        var hueTop = top + rows * CellHeight + 5;
        var height = hueTop + 25 * ((hues.Length + 3) / 4) + 20;

        builder.AddPage();
        builder.AddBackground(0, 0, width, height, 9270);
        builder.AddAlphaRegion(12, 12, width - 24, height - 24);
        builder.AddLabel(20, 18, LabelHue, "Choose a carpet");
        builder.AddHtml(
            20, 38, width - 40, 40,
            $"Pick a colour below, then a style. Then target two opposite corners on your house floor: {VariableCarpetSettings.MinSide} to {VariableCarpetSettings.MaxSide} tiles a side.",
            "#FFFFFF"
        );

        for (var i = 0; i < styles.Length; i++)
        {
            var cellX = 15 + i % 3 * CellWidth;
            var cellY = top + i / 3 * CellHeight;
            var look = styles[i];

            // A 3 by 3 preview, drawn as the client lays floor tiles: each step east is 22 px right and 22 down.
            for (var y = 0; y < 3; y++)
            {
                for (var x = 0; x < 3; x++)
                {
                    builder.AddItem(cellX + 44 + (x - y) * 22, cellY + (x + y) * 22, look.ArtAt(x, y, 2, 2), hue);
                }
            }

            builder.AddButton(cellX, cellY + 135, 4005, 4007, StyleButtonBase + i);
            builder.AddLabel(cellX + 35, cellY + 137, TextHue, look.Name);
        }

        for (var i = 0; i < hues.Length; i++)
        {
            var x = 20 + i % 4 * 115;
            var y = hueTop + i / 4 * 25;
            var picked = i == _hueIndex;
            builder.AddButton(x, y, picked ? 4006 : 4005, 4007, HueButtonBase + i);
            builder.AddLabel(x + 35, y + 2, picked ? PickedHue : TextHue, hues[i].Name);
        }
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        var from = sender.Mobile;
        var id = info.ButtonID;

        if (from == null || id == 0 || _deed.Deleted)
        {
            return;
        }

        if (!_deed.IsChildOf(from.Backpack))
        {
            from.SendLocalizedMessage(1042001); // That must be in your pack for you to use it.
            return;
        }

        if (id >= HueButtonBase && id < HueButtonBase + VariableCarpetSettings.Hues.Length)
        {
            from.SendGump(new VariableCarpetGump(_deed, id - HueButtonBase));
        }
        else if (id >= StyleButtonBase && id < StyleButtonBase + VariableCarpetSettings.Styles.Length)
        {
            _deed.BeginPlace(from, id - StyleButtonBase, _hueIndex);
        }
    }
}

/// <summary>The removal confirm: yes rolls the carpet up into a deed, after every check again.</summary>
public sealed class VariableCarpetRemoveGump : DynamicGump
{
    public const int RemoveButton = 1;

    private readonly VariableCarpetAddon _carpet;

    public override bool Singleton => true;

    public VariableCarpetRemoveGump(VariableCarpetAddon carpet) : base(80, 80) => _carpet = carpet;

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        builder.AddPage();
        builder.AddBackground(0, 0, 320, 150, 9270);
        builder.AddAlphaRegion(12, 12, 296, 126);
        builder.AddLabel(25, 20, 0x22, "Roll up this carpet?");
        builder.AddHtml(25, 45, 270, 50, "It comes up off the floor, and a carpet deed goes into your pack.", "#FFFFFF");
        builder.AddButton(25, 105, 4005, 4007, RemoveButton);
        builder.AddLabel(60, 107, 0x7FF, "Roll it up");
        builder.AddButton(180, 105, 4017, 4019, 0);
        builder.AddLabel(215, 107, 0x7FF, "Cancel");
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (info.ButtonID == RemoveButton && sender.Mobile is { } from)
        {
            _carpet.TryRemove(from);
        }
    }
}
