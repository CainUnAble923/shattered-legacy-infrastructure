// cc-P199: ServUO pub57 Scripts/Items/Functional/HouseCraftables.cs. The Time of Legends craftable house pieces:
//   - CraftableHouseItem: one item for 30 kinds (CraftableItemType): rough stone walls, windows, arches and pillars,
//     rough stone stairs, light/medium/dark pavers, wooden and metal sign hangers, and the decorative stable set. Its
//     ItemID comes from the IDs table; OnFlip cycles the kind's alternative art. "House Only": dropped on the ground
//     outside a house the dropper co-owns it goes back to the pack (or is deleted if it cannot), and it cannot go into
//     a movable container or a pack animal.
//   - CraftableMetalHouseDoor and CraftableStoneHouseDoor (DoorType): movable house doors made by tinkering and
//     masonry, with the same house-only rule (and not into a custom foundation); used only once locked down.
//   - CraftableHouseAddon, CraftableHouseAddonComponent, CraftableHouseAddonDeed and CraftableHouseDoorDeed: ServUO's
//     older shapes for the same things, which ServUO converts on load (30 seconds after) into a CraftableHouseItem or a
//     craftable door through CraftableHouseItem.Replace / CraftableHouseDoorDeed.Replace. Nothing makes them any more;
//     they are kept so the file is ServUO's whole, and they convert the same way here.
// Which crafts register these, and where, is in the report (ServUO DefMasonry.cs:228-325, DefCarpentry.cs:240-249,
// DefTinkering.cs:474-532); no registration is made here.
//
// Differences from ServUO:
//   - CraftItem.Data does not exist in pinned (Engines/Craft/Core/CraftItem.cs has no Data and no AddCreateItem), so
//     ServUO's SetData(index, CraftableItemType.X) / SetData(index, DoorType.X) cannot reach OnCraft or Create. Here the
//     kind is found from the craft entry's own name: CraftableHouseItem.OnCraft matches craftItem.NameNumber against
//     the IDs table's first column (the cliloc ServUO registers each kind under; the three pavers are registered by
//     string, "Light Paver", "Medium Paver", "Dark Paver", and are matched by NameString), and the doors' Create matches
//     NameNumber against their LabelNumber values (ServUO registers each door under its own label). For every craft
//     ServUO registers this gives the kind SetData gives. The doors still need a create hook in pinned CraftItem to be
//     craftable at all (they have no parameterless constructor, as in ServUO); see the report.
//   - CraftableHouseItem saves its Resource as well as its kind; ServUO saves only the kind (HouseCraftables.cs:
//     283-297), so a crafted piece's "~1_NUMBER~ ~2_ITEMNAME~" name lost its metal or wood after a restart while the hue
//     stayed. The doors and the door deed save both, as in ServUO.
//   - IFlipable (ServUO's house-turn interface, Interior Decorator) and IResource do not exist in pinned; OnFlip and
//     Resource are kept as public members. Nothing calls OnFlip on this shard (the hook is in the report).
//   - The doors' version-0 load fix (moving a door from house.Addons to LockDowns, :781-797 and :1062-1077) is dropped:
//     it is for ServUO saves older than version 1, and this shard has none.
//   - ServUO's BaseHouse.LockDowns is a Dictionary<Item, Mobile> and IsSecure(item) a method; pinned's LockDowns is a
//     List<Item> (BaseHouse.cs:514) and the secure test is HasSecureItem (:3672). Replace uses those.
//   - IsArtifact => true is dropped (ServUO-only, no reader in pinned; port recipe "Items: two more differences").
//   - ICraftable.OnCraft takes pinned's BaseTool where ServUO has ITool, and Resources[0] for Resources.GetAt(0).
//   - The three load-time conversions skip an item deleted in the 30 seconds before they run; ServUO's would build a
//     replacement for an item that no longer exists.
//   - Door OnFlip, as in ServUO, changes Type, Facing, ClosedId, OpenedId and Offset but not the ItemID; the new art
//     shows on the next open or close. ServUO's LeftMetalDoor_S_Out flips to RightMetalDoor_E_Out (:631-633), kept.

using System;
using ModernUO.Serialization;
using Server.Engines.Craft;
using Server.Mobiles;
using Server.Multis;

namespace Server.Items;

public enum CraftableItemType
{
    // Walls
    RoughWindowless,
    RoughWindow,
    RoughArch,
    RoughPillar,
    RoughRoundedArch,
    RoughSmallArch,
    RoughAngledPillar,
    ShortRough,

    // Stairs
    RoughBlock,
    RoughSteps,
    RoughCornerSteps,
    RoughRoundedCornerSteps,
    RoughInsetSteps,
    RoughRoundedInsetSteps,

    // Floors
    LightPaver,
    MediumPaver,
    DarkPaver,

    // Hangers
    LightWoodenSignHanger,
    DarkWoodenSignHanger,
    CurledMetalSignHanger,
    FlourishedMetalSignHanger,
    InwardCurledMetalSignHanger,
    EndCurledMetalSignHanger,

    // DecorativeStableSet
    Arch,
    NWCornerPiece,
    SouthFacingPieces,
    EastFacingPieces,
    GateSouth,
    GateEast,
    CornerPiece
}

[SerializationGenerator(0, false)]
public partial class CraftableHouseItem : Item, ICraftable
{
    public static int[][] IDs =
    {
        new[] { 1155794, 464, 465, 466, 463 },      // RoughWindowless
        new[] { 1155797, 467, 468 },                // RoughWindow
        new[] { 1155799, 469, 470, 471, 472, 473 }, // RoughArch
        new[] { 1155804, 474 },                     // RoughPillar
        new[] { 1155805, 475, 476, 477, 478, 479 }, // RoughRoundedArch
        new[] { 1155810, 480, 481, 482, 484 },      // RoughSmallArch
        new[] { 1155814, 486, 487 },                // RoughAngledPillar
        new[] { 1155816, 488, 489, 490, 491 },      // ShortRough
        new[] { 1155821, 1928 },                    // RoughBlock
        new[] { 1155822, 1929, 1930, 1931, 1932 },  // RoughSteps
        new[] { 1155826, 1934, 1933, 1935, 1936 },  // RoughCornerSteps
        new[] { 1155830, 1938, 1940, 1937, 1939 },  // RoughRoundedCornerSeps
        new[] { 1155834, 1941, 1942, 1943, 1944 },  // RoughInsetSteps
        new[] { 1155838, 1945, 1946, 1947, 1948 },  // RoughRoundedIsetSteps
        new[] { 1155878, 1305 },                    // LightPaver
        new[] { 1155879, 1309 },                    // Medium Paver
        new[] { 1155880, 1313 },                    // DarkPaver
        new[] { 1155850, 2969, 2970 },              // LightWoodenSIgnHanger
        new[] { 1155849, 2967, 2968 },              // DarkWoodenSIgnHanger
        new[] { 1155851, 2971, 2972 },              // CurledMetalSIgnHanger
        new[] { 1155852, 2973, 2974 },              // FlourishedCurledSignHanger
        new[] { 1155853, 2975, 2976 },              // InwardCurledCurledMetalSIgnHanger
        new[] { 1155854, 2977, 2978 },              // EndCurledMetalSignHanger
        new[] { 1126213, 42189, 42190 },            // Arch
        new[] { 1126197, 42171 },                   // NWCornerPiece
        new[] { 1126197, 42172, 42173 },            // SouthFacingPieces
        new[] { 1126197, 42173, 42172 },            // EastFacingPieces
        new[] { 1126211, 42176 },                   // GateSouth
        new[] { 1126211, 42187 },                   // GateEast
        new[] { 1126197, 42174 }                    // CornerPiece
    };

    // ServUO DefMasonry.cs:314-323 registers the three pavers by string name, not cliloc.
    private static readonly string[] _paverNames = { "Light Paver", "Medium Paver", "Dark Paver" };

    public CraftableHouseItem() : base(1)
    {
    }

    [Constructible]
    public CraftableHouseItem(CraftableItemType type) : base(IDs[(int)type][1]) => _itemType = type;

    public int NorthID => 0;
    public int WestID => 0;

    [CommandProperty(AccessLevel.GameMaster)]
    public bool CanFlip
    {
        get
        {
            var value = (int)_itemType;

            return value is >= 0 and <= 13 || value >= 17;
        }
    }

    // Both members load straight into their fields (useField), as ServUO's Deserialize does: the setters would reset a
    // flipped ItemID (InvalidateID) or a re-hued piece on every load.
    private CraftResource _resource;

    [SerializableProperty(1, useField: nameof(_resource))]
    [CommandProperty(AccessLevel.GameMaster)]
    public CraftResource Resource
    {
        get => _resource;
        set
        {
            if (_resource != value)
            {
                _resource = value;
                Hue = CraftResources.GetHue(_resource);

                InvalidateProperties();
                this.MarkDirty();
            }
        }
    }

    private CraftableItemType _itemType;

    [SerializableProperty(0, useField: nameof(_itemType))]
    [CommandProperty(AccessLevel.GameMaster)]
    public CraftableItemType ItemType
    {
        get => _itemType;
        set
        {
            _itemType = value;
            InvalidateID();
            this.MarkDirty();
        }
    }

    public void InvalidateID()
    {
        ItemID = IDs[(int)_itemType][1];
    }

    public void OnFlip(Mobile from)
    {
        var list = IDs[(int)_itemType];

        if (CanFlip && list.Length > 2)
        {
            for (var i = 1; i < list.Length; i++)
            {
                var id = list[i];

                if (ItemID == id)
                {
                    ItemID = i >= list.Length - 1 ? list[1] : list[i + 1];
                    break;
                }
            }
        }
    }

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);

        list.Add(1153494); // House Only
    }

    public virtual int OnCraft(
        int quality, bool makersMark, Mobile from, CraftSystem craftSystem, Type typeRes, BaseTool tool,
        CraftItem craftItem, int resHue
    )
    {
        // ServUO: `craftItem.Data is CraftableItemType`. Pinned has no Data; the craft entry's name stands for it.
        if (craftItem != null && TryGetCraftedType(craftItem, out var type))
        {
            ItemType = type;

            var resourceType = typeRes ?? craftItem.Resources[0].ItemType;

            Resource = CraftResources.GetFromType(resourceType);
        }

        return quality;
    }

    /// <summary>
    /// The kind a craft entry makes, from its name: the IDs table's first column for a cliloc name (first match, which
    /// is the only match for every kind ServUO's Def files register), the paver names for a string name.
    /// </summary>
    public static bool TryGetCraftedType(CraftItem craftItem, out CraftableItemType type)
    {
        if (craftItem.NameNumber > 0)
        {
            for (var i = 0; i < IDs.Length; i++)
            {
                if (IDs[i][0] == craftItem.NameNumber)
                {
                    type = (CraftableItemType)i;
                    return true;
                }
            }
        }

        if (craftItem.NameString != null)
        {
            for (var i = 0; i < _paverNames.Length; i++)
            {
                if (craftItem.NameString == _paverNames[i])
                {
                    type = CraftableItemType.LightPaver + i;
                    return true;
                }
            }
        }

        type = default;
        return false;
    }

    public override void AddNameProperty(IPropertyList list)
    {
        if (!CraftResources.IsStandard(_resource))
        {
            list.Add(1050039, $"#{CraftResources.GetLocalizationNumber(_resource)}\t{GetNameString()}"); // ~1_NUMBER~ ~2_ITEMNAME~
        }
        else
        {
            base.AddNameProperty(list);
        }
    }

    private string GetNameString() => Name ?? $"#{LabelNumber}";

    public override bool DropToWorld(Mobile from, Point3D p)
    {
        var h = BaseHouse.FindHouseAt(p, from.Map, ItemData.Height);

        if (h?.IsCoOwner(from) == true)
        {
            return base.DropToWorld(from, p);
        }

        if (from.Backpack?.TryDropItem(from, this, false) != true)
        {
            Delete();
        }

        return false;
    }

    public override bool DropToMobile(Mobile from, Mobile target, Point3D p)
    {
        if (target.Backpack is StrongBackpack)
        {
            return false;
        }

        return base.DropToMobile(from, target, p);
    }

    public override bool OnDroppedInto(Mobile from, Container target, Point3D p)
    {
        if (target.Movable || target is StrongBackpack)
        {
            return false;
        }

        return base.OnDroppedInto(from, target, p);
    }

    public override bool OnDroppedOnto(Mobile from, Item target)
    {
        if (target is Container && target.Movable || target is StrongBackpack)
        {
            return false;
        }

        return base.OnDroppedOnto(from, target);
    }

    public static void Replace(Item oldItem, CraftableItemType type)
    {
        var addon = (oldItem as AddonComponent)?.Addon;

        var item = new CraftableHouseItem(type);

        if (oldItem.Parent is Container container)
        {
            container.DropItem(item);
        }
        else
        {
            var house = BaseHouse.FindHouseAt(oldItem);

            item.MoveToWorld(oldItem.Location, oldItem.Map);

            item.IsLockedDown = oldItem.IsLockedDown;
            item.IsSecure = oldItem.IsSecure;
            item.Movable = oldItem.Movable;

            if (house != null)
            {
                if (house.LockDowns.Contains(oldItem))
                {
                    house.LockDowns.Remove(oldItem);
                    house.LockDowns.Add(item);
                }
                else if (house.HasSecureItem(oldItem))
                {
                    house.ReleaseSecure(house.Owner, oldItem);
                    house.AddSecure(house.Owner, item);
                }
                else if (addon != null)
                {
                    house.Addons?.Remove(addon);

                    house.LockDowns.Add(item);
                }
            }

            item.InvalidateProperties();
        }

        if (addon != null)
        {
            addon.Delete();
        }
        else
        {
            oldItem.Delete();
        }
    }
}

[SerializationGenerator(0, false)]
public partial class CraftableHouseAddonComponent : AddonComponent
{
    public CraftableHouseAddonComponent(int id) : base(id)
    {
    }

    [AfterDeserialization(false)]
    private void AfterDeserialization()
    {
        Timer.StartTimer(
            TimeSpan.FromSeconds(30),
            () =>
            {
                if (!Deleted && Addon is CraftableHouseAddon houseAddon)
                {
                    CraftableHouseItem.Replace(this, houseAddon.ItemType);
                }
            }
        );
    }
}

[SerializationGenerator(0, false)]
public partial class CraftableHouseAddon : BaseAddon
{
    [SerializableField(0)]
    private CraftableItemType _itemType;

    public CraftableHouseAddon()
    {
    }

    public override BaseAddonDeed Deed => null;
}

[SerializationGenerator(0, false)]
public partial class CraftableHouseAddonDeed : BaseAddonDeed
{
    [SerializableField(0)]
    private CraftableItemType _itemType;

    public CraftableHouseAddonDeed()
    {
    }

    // ServUO's constructor takes the kind and does not keep it (HouseCraftables.cs:410-412); kept as written.
    public CraftableHouseAddonDeed(CraftableItemType type)
    {
    }

    public override BaseAddon Addon => null;

    public override void OnDoubleClick(Mobile m)
    {
    }

    [AfterDeserialization(false)]
    private void AfterDeserialization()
    {
        Timer.StartTimer(
            TimeSpan.FromSeconds(30),
            () =>
            {
                if (!Deleted)
                {
                    CraftableHouseItem.Replace(this, _itemType);
                }
            }
        );
    }
}

[SerializationGenerator(0, false)]
public partial class CraftableHouseDoorDeed : Item
{
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private DoorType _type;

    public CraftableHouseDoorDeed() : base(0x14F0)
    {
    }

    public CraftableHouseDoorDeed(DoorType type) : base(0x14F0) => _type = type;

    // Loaded straight into the field (useField), as ServUO's Deserialize reads _Resource; the setter would re-hue.
    private CraftResource _resource;

    [SerializableProperty(1, useField: nameof(_resource))]
    [CommandProperty(AccessLevel.GameMaster)]
    public CraftResource Resource
    {
        get => _resource;
        set
        {
            if (_resource != value)
            {
                _resource = value;
                Hue = CraftResources.GetHue(_resource);

                InvalidateProperties();
                this.MarkDirty();
            }
        }
    }

    public override void OnDoubleClick(Mobile m)
    {
    }

    [AfterDeserialization(false)]
    private void AfterDeserialization()
    {
        Timer.StartTimer(TimeSpan.FromSeconds(30), Replace);
    }

    public void Replace()
    {
        if (Deleted)
        {
            return;
        }

        BaseDoor door;

        if (_type < DoorType.LeftMetalDoor_S_In)
        {
            var stone = new CraftableStoneHouseDoor(_type, CraftableMetalHouseDoor.GetDoorFacing(_type));
            stone.Resource = _resource; // ServUO: `if (door is IResource)`; both door kinds are
            door = stone;
        }
        else
        {
            var metal = new CraftableMetalHouseDoor(_type, CraftableMetalHouseDoor.GetDoorFacing(_type));
            metal.Resource = _resource;
            door = metal;
        }

        if (Parent is Container container)
        {
            container.DropItem(door);
        }
        else
        {
            var house = BaseHouse.FindHouseAt(this);

            door.MoveToWorld(Location, Map);

            door.IsLockedDown = IsLockedDown;
            door.IsSecure = IsSecure;
            door.Movable = Movable;

            if (house?.LockDowns.Contains(this) == true)
            {
                house.LockDowns.Remove(this);
                house.LockDowns.Add(door);
            }
            else if (house?.HasSecureItem(this) == true)
            {
                house.ReleaseSecure(house.Owner, this);
                house.AddSecure(house.Owner, door);
            }
        }

        Delete();
    }
}

public enum DoorType
{
    StoneDoor_S_In,
    StoneDoor_S_Out,
    StoneDoor_E_In,
    StoneDoor_E_Out,
    LeftMetalDoor_S_In,
    LeftMetalDoor_S_Out,
    RightMetalDoor_S_In,
    RightMetalDoor_S_Out,
    LeftMetalDoor_E_Out,
    LeftMetalDoor_E_In,
    RightMetalDoor_E_Out,
    RightMetalDoor_E_In
}

[SerializationGenerator(0, false)]
public partial class CraftableMetalHouseDoor : MetalHouseDoor, ICraftable
{
    [SerializableField(0)]
    private DoorType _type;

    public CraftableMetalHouseDoor(DoorType type, DoorFacing facing) : base(facing)
    {
        _type = type;
        Movable = true;
    }

    public override int LabelNumber => GetLabel(_type);

    // Loaded straight into the field (useField), as ServUO's Deserialize reads _Resource; the setter would re-hue.
    private CraftResource _resource;

    [SerializableProperty(1, useField: nameof(_resource))]
    [CommandProperty(AccessLevel.GameMaster)]
    public CraftResource Resource
    {
        get => _resource;
        set
        {
            if (_resource != value)
            {
                _resource = value;
                Hue = CraftResources.GetHue(_resource);

                InvalidateProperties();
                this.MarkDirty();
            }
        }
    }

    public static int GetLabel(DoorType type) =>
        type switch
        {
            DoorType.RightMetalDoor_S_In  => 1156081,
            DoorType.LeftMetalDoor_E_In   => 1156352,
            DoorType.RightMetalDoor_E_In  => 1156353,
            DoorType.LeftMetalDoor_S_Out  => 1156350,
            DoorType.RightMetalDoor_S_Out => 1156351,
            DoorType.LeftMetalDoor_E_Out  => 1156082,
            DoorType.RightMetalDoor_E_Out => 1156083,
            _                             => 1156080 // LeftMetalDoor_S_In, and ServUO's default
        };

    /// <summary>
    /// The door a craft entry makes, from its name (ServUO: craftItem.Data, set by SetData). Every ServUO entry for a
    /// craftable door is registered under that door's own label.
    /// </summary>
    public static bool TryGetDoorType(int nameNumber, out DoorType type)
    {
        foreach (var t in Enum.GetValues<DoorType>())
        {
            var label = t < DoorType.LeftMetalDoor_S_In ? CraftableStoneHouseDoor.GetLabel(t) : GetLabel(t);

            if (label == nameNumber)
            {
                type = t;
                return true;
            }
        }

        type = default;
        return false;
    }

    public static Item Create(Mobile m, CraftItem craftItem, BaseTool tool)
    {
        var type = DoorType.LeftMetalDoor_S_In;

        if (craftItem != null && TryGetDoorType(craftItem.NameNumber, out var crafted))
        {
            type = crafted;
        }

        return new CraftableMetalHouseDoor(type, GetDoorFacing(type));
    }

    public virtual int OnCraft(
        int quality, bool makersMark, Mobile from, CraftSystem craftSystem, Type typeRes, BaseTool tool,
        CraftItem craftItem, int resHue
    )
    {
        if (craftItem != null)
        {
            var resourceType = typeRes ?? craftItem.Resources[0].ItemType;

            Resource = CraftResources.GetFromType(resourceType);
        }

        return quality;
    }

    public virtual void OnFlip(Mobile from)
    {
        if (Open)
        {
            from.SendMessage("The door must be closed before you can do that.");
            return;
        }

        Type = Flip(_type);

        Facing = GetDoorFacing(_type);

        ClosedId = 0x675 + 2 * (int)Facing;
        OpenedId = 0x676 + 2 * (int)Facing;

        Offset = GetOffset(Facing);
        InvalidateProperties();
    }

    // ServUO's switch in both doors' OnFlip (HouseCraftables.cs:604-643, 967-1006), kept whole.
    public static DoorType Flip(DoorType type) =>
        type switch
        {
            DoorType.StoneDoor_S_Out      => DoorType.StoneDoor_S_In,
            DoorType.StoneDoor_E_In       => DoorType.StoneDoor_E_Out,
            DoorType.StoneDoor_E_Out      => DoorType.StoneDoor_E_In,
            DoorType.LeftMetalDoor_S_In   => DoorType.LeftMetalDoor_S_Out,
            DoorType.RightMetalDoor_S_In  => DoorType.RightMetalDoor_S_Out,
            DoorType.LeftMetalDoor_E_In   => DoorType.LeftMetalDoor_E_Out,
            DoorType.RightMetalDoor_E_In  => DoorType.RightMetalDoor_E_Out,
            DoorType.LeftMetalDoor_S_Out  => DoorType.RightMetalDoor_E_Out,
            DoorType.RightMetalDoor_S_Out => DoorType.RightMetalDoor_S_In,
            DoorType.LeftMetalDoor_E_Out  => DoorType.LeftMetalDoor_E_In,
            DoorType.RightMetalDoor_E_Out => DoorType.RightMetalDoor_E_In,
            _                             => DoorType.StoneDoor_S_Out // StoneDoor_S_In, and ServUO's default
        };

    public static DoorFacing GetDoorFacing(DoorType type) =>
        type switch
        {
            DoorType.StoneDoor_S_Out      => DoorFacing.WestCW,
            DoorType.StoneDoor_E_In       => DoorFacing.NorthCW,
            DoorType.StoneDoor_E_Out      => DoorFacing.SouthCW,
            DoorType.LeftMetalDoor_S_In   => DoorFacing.WestCCW,
            DoorType.RightMetalDoor_S_In  => DoorFacing.EastCW,
            DoorType.LeftMetalDoor_E_In   => DoorFacing.SouthCCW,
            DoorType.RightMetalDoor_E_In  => DoorFacing.NorthCW,
            DoorType.LeftMetalDoor_S_Out  => DoorFacing.WestCW,
            DoorType.RightMetalDoor_S_Out => DoorFacing.EastCCW,
            DoorType.LeftMetalDoor_E_Out  => DoorFacing.SouthCW,
            DoorType.RightMetalDoor_E_Out => DoorFacing.NorthCCW,
            _                             => DoorFacing.EastCW // StoneDoor_S_In, and ServUO's default
        };

    public override void Use(Mobile from)
    {
        if (!Movable)
        {
            base.Use(from);
        }
    }

    public override void AddNameProperty(IPropertyList list)
    {
        if (!CraftResources.IsStandard(_resource))
        {
            list.Add(1050039, $"#{CraftResources.GetLocalizationNumber(_resource)}\t{GetNameString()}"); // ~1_NUMBER~ ~2_ITEMNAME~
        }
        else
        {
            base.AddNameProperty(list);
        }
    }

    private string GetNameString() => Name ?? $"#{LabelNumber}";

    public override bool DropToWorld(Mobile from, Point3D p)
    {
        var h = BaseHouse.FindHouseAt(p, from.Map, ItemData.Height);

        if (h != null && h is not HouseFoundation && h.IsCoOwner(from))
        {
            return base.DropToWorld(from, p);
        }

        if (from.Backpack?.TryDropItem(from, this, false) != true)
        {
            Delete();
        }

        return false;
    }

    public override bool DropToMobile(Mobile from, Mobile target, Point3D p)
    {
        if (target.Backpack is StrongBackpack)
        {
            return false;
        }

        return base.DropToMobile(from, target, p);
    }

    public override bool OnDroppedInto(Mobile from, Container target, Point3D p)
    {
        if (target.Movable || target is StrongBackpack)
        {
            return false;
        }

        return base.OnDroppedInto(from, target, p);
    }

    public override bool OnDroppedOnto(Mobile from, Item target)
    {
        if (target is Container && target.Movable || target is StrongBackpack)
        {
            return false;
        }

        return base.OnDroppedOnto(from, target);
    }
}

[SerializationGenerator(0, false)]
public partial class CraftableStoneHouseDoor : BaseHouseDoor, ICraftable
{
    [SerializableField(0)]
    private DoorType _type;

    public CraftableStoneHouseDoor(DoorType type, DoorFacing facing)
        : base(facing, 0x324 + 2 * (int)facing, 0x325 + 2 * (int)facing, 0xED, 0xF4, GetOffset(facing))
    {
        _type = type;
        Movable = true;
    }

    public override int LabelNumber => GetLabel(_type);

    // Loaded straight into the field (useField), as ServUO's Deserialize reads _Resource; the setter would re-hue.
    private CraftResource _resource;

    [SerializableProperty(1, useField: nameof(_resource))]
    [CommandProperty(AccessLevel.GameMaster)]
    public CraftResource Resource
    {
        get => _resource;
        set
        {
            if (_resource != value)
            {
                _resource = value;
                Hue = CraftResources.GetHue(_resource);

                InvalidateProperties();
                this.MarkDirty();
            }
        }
    }

    public static int GetLabel(DoorType type) =>
        type switch
        {
            DoorType.StoneDoor_S_Out => 1156348,
            DoorType.StoneDoor_E_In  => 1156349,
            DoorType.StoneDoor_E_Out => 1156079,
            _                        => 1156078 // StoneDoor_S_In, and ServUO's default
        };

    public static Item Create(Mobile m, CraftItem craftItem, BaseTool tool)
    {
        var type = DoorType.StoneDoor_S_In;

        if (craftItem != null && CraftableMetalHouseDoor.TryGetDoorType(craftItem.NameNumber, out var crafted))
        {
            type = crafted;
        }

        return new CraftableStoneHouseDoor(type, CraftableMetalHouseDoor.GetDoorFacing(type));
    }

    public virtual int OnCraft(
        int quality, bool makersMark, Mobile from, CraftSystem craftSystem, Type typeRes, BaseTool tool,
        CraftItem craftItem, int resHue
    )
    {
        if (craftItem != null)
        {
            var resourceType = typeRes ?? craftItem.Resources[0].ItemType;

            Resource = CraftResources.GetFromType(resourceType);
        }

        return quality;
    }

    public override void Use(Mobile from)
    {
        if (!Movable)
        {
            base.Use(from);
        }
    }

    public override void AddNameProperty(IPropertyList list)
    {
        if (!CraftResources.IsStandard(_resource))
        {
            list.Add(1050039, $"#{CraftResources.GetLocalizationNumber(_resource)}\t{GetNameString()}"); // ~1_NUMBER~ ~2_ITEMNAME~
        }
        else
        {
            base.AddNameProperty(list);
        }
    }

    private string GetNameString() => Name ?? $"#{LabelNumber}";

    public override bool DropToWorld(Mobile from, Point3D p)
    {
        var h = BaseHouse.FindHouseAt(p, from.Map, ItemData.Height);

        if (h != null && h is not HouseFoundation && h.IsCoOwner(from))
        {
            return base.DropToWorld(from, p);
        }

        if (from.Backpack?.TryDropItem(from, this, false) != true)
        {
            Delete();
        }

        return false;
    }

    public override bool DropToMobile(Mobile from, Mobile target, Point3D p)
    {
        if (target.Backpack is StrongBackpack)
        {
            return false;
        }

        return base.DropToMobile(from, target, p);
    }

    public override bool OnDroppedInto(Mobile from, Container target, Point3D p)
    {
        if (target.Movable || target is StrongBackpack)
        {
            return false;
        }

        return base.OnDroppedInto(from, target, p);
    }

    public override bool OnDroppedOnto(Mobile from, Item target)
    {
        if (target is Container && target.Movable || target is StrongBackpack)
        {
            return false;
        }

        return base.OnDroppedOnto(from, target);
    }

    public virtual void OnFlip(Mobile from)
    {
        if (Open)
        {
            from.SendMessage("The door must be closed before you can do that.");
            return;
        }

        Type = CraftableMetalHouseDoor.Flip(_type);

        Facing = CraftableMetalHouseDoor.GetDoorFacing(_type);

        ClosedId = 0x324 + 2 * (int)Facing;
        OpenedId = 0x325 + 2 * (int)Facing;

        Offset = GetOffset(Facing);
        InvalidateProperties();
    }
}
