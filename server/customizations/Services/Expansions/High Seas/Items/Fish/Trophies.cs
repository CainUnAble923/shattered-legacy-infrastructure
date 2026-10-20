// cc-P193: ServUO pub57 Scripts/Services/Expansions/High Seas/Items/Fish/Trophies.cs -> ours, the High Seas fishing port.
//
// A rare or legendary fish, crab or lobster becomes a wall trophy: the taxidermy kit turns the fish into a FishTrophyDeed
// (TaxidermyKit-fish-trophies.patch calls FishTrophies.TryMake), the deed hangs a FishTrophy addon on a house wall facing
// north or west in the style of the fish (marlin, dragonfish, two mounting boards, lobster, crab), hued as the fish, and
// the addon's component shows the weight, the fisher and the date. Chopping the addon or using it gives the deed back.
//
// ServUO keeps the 35 fish rows inside TaxidermyKit's own table (its NorthID column is this file's style index). Pinned's
// table is private and stock, so the rows live here (FishTrophies.Table) and the kit patch asks this file first.
//
// Changes from ServUO (shard-migration notes/cc-P193-port-high-seas-fishing.md): pinned BaseHouse.Addons is a list, not
// a dictionary of addon to placer, so the "placed by you" clause of taking a trophy down is the house co-owner test alone.

using System;
using ModernUO.Serialization;
using Server.Multis;

namespace Server.Items;

public enum TrophyStyle
{
    Marlin,
    Dragonfish,
    BoardGrouper,
    BoardMahi,
    Lobster,
    Crab
}

/// <summary>One fish the taxidermy kit mounts: ServUO TaxidermyKit.m_Table's High Seas rows.</summary>
public class FishTrophyInfo
{
    public FishTrophyInfo(Type type, TrophyStyle style, int deedNumber, int addonNumber)
    {
        FishType = type;
        Style = style;
        DeedNumber = deedNumber;
        AddonNumber = addonNumber;
    }

    public Type FishType { get; }
    public TrophyStyle Style { get; }
    public int DeedNumber { get; }
    public int AddonNumber { get; }
}

public static class FishTrophies
{
    // ServUO TaxidermyKit.cs:72-106. Its NorthID column (0-34) selected the style through FishTrophyDeed.ConstructTrophy's
    // switch; the style is written out here instead, in the same order.
    public static readonly FishTrophyInfo[] Table =
    {
        new(typeof(AutumnDragonfish), TrophyStyle.Dragonfish, 1116124, 1116185),
        new(typeof(BullFish), TrophyStyle.BoardMahi, 1116129, 1116190),
        new(typeof(FireFish), TrophyStyle.BoardGrouper, 1116127, 1116188),
        new(typeof(GiantKoi), TrophyStyle.BoardMahi, 1116122, 1116183),
        new(typeof(LavaFish), TrophyStyle.Marlin, 1116130, 1116191),
        new(typeof(SummerDragonfish), TrophyStyle.Dragonfish, 1116124, 1116186),
        new(typeof(UnicornFish), TrophyStyle.Marlin, 1116120, 1116181),
        new(typeof(AbyssalDragonfish), TrophyStyle.Dragonfish, 1116140, 1116201),
        new(typeof(BlackMarlin), TrophyStyle.Marlin, 1116133, 1116194),
        new(typeof(BlueMarlin), TrophyStyle.Marlin, 1116131, 1116192),
        new(typeof(GiantSamuraiFish), TrophyStyle.BoardGrouper, 1116138, 1116199),
        new(typeof(Kingfish), TrophyStyle.BoardGrouper, 1116119, 1116180),
        new(typeof(LanternFish), TrophyStyle.BoardMahi, 1116142, 1116203),
        new(typeof(SeekerFish), TrophyStyle.BoardGrouper, 1116145, 1116206),
        new(typeof(SpringDragonfish), TrophyStyle.Dragonfish, 1116139, 1116200),
        new(typeof(StoneFish), TrophyStyle.BoardMahi, 1116135, 1116196),
        new(typeof(WinterDragonfish), TrophyStyle.Dragonfish, 1116141, 1116202),
        new(typeof(BlueLobster), TrophyStyle.Lobster, 1149812, 1149804),
        new(typeof(BloodLobster), TrophyStyle.Lobster, 1149816, 1149808),
        new(typeof(DreadLobster), TrophyStyle.Lobster, 1149817, 1149809),
        new(typeof(VoidLobster), TrophyStyle.Lobster, 1149815, 1149807),
        new(typeof(StoneCrab), TrophyStyle.Crab, 1149811, 1149803),
        new(typeof(SpiderCrab), TrophyStyle.Crab, 1149813, 1149805),
        new(typeof(TunnelCrab), TrophyStyle.Crab, 1149818, 1149810),
        new(typeof(VoidCrab), TrophyStyle.Crab, 1149814, 1149806),
        new(typeof(CrystalFish), TrophyStyle.BoardGrouper, 1116126, 1116187),
        new(typeof(FairySalmon), TrophyStyle.BoardGrouper, 1116123, 1116184),
        new(typeof(GreatBarracuda), TrophyStyle.BoardGrouper, 1116134, 1116195),
        new(typeof(HolyMackerel), TrophyStyle.BoardGrouper, 1116121, 1116182),
        new(typeof(ReaperFish), TrophyStyle.BoardGrouper, 1116128, 1116189),
        new(typeof(YellowtailBarracuda), TrophyStyle.BoardGrouper, 1116132, 1116193),
        new(typeof(DungeonPike), TrophyStyle.BoardGrouper, 1116143, 1116204),
        new(typeof(GoldenTuna), TrophyStyle.BoardGrouper, 1116137, 1116198),
        new(typeof(RainbowFish), TrophyStyle.BoardGrouper, 1116144, 1116205),
        new(typeof(ZombieFish), TrophyStyle.BoardGrouper, 1116136, 1116197)
    };

    public static FishTrophyInfo InfoFor(Type type)
    {
        if (type == null)
        {
            return null;
        }

        foreach (var info in Table)
        {
            if (info.FishType == type)
            {
                return info;
            }
        }

        return null;
    }

    public static int IndexOf(Type type) => Array.FindIndex(Table, i => i.FishType == type);

    /// <summary>
    /// The taxidermy kit's High Seas branch (ServUO TaxidermyKit.cs:236-268): a rare fish, crab or lobster becomes a
    /// FishTrophyDeed for ten boards and the kit. Returns false when the target is not one, so the kit goes on with its
    /// own corpse and big-fish rules; true when it answered the player either way.
    /// </summary>
    public static bool TryMake(Mobile from, Item kit, object targeted)
    {
        Mobile fisher;
        int weight;
        DateTime caught;

        switch (targeted)
        {
            case RareFish fish:
                fisher = fish.Fisher;
                weight = (int)fish.Weight;
                caught = fish.DateCaught;
                break;
            case RareCrabAndLobster crab:
                fisher = crab.Fisher;
                weight = (int)crab.Weight;
                caught = crab.DateCaught;
                break;
            default:
                return false;
        }

        var item = (Item)targeted;
        var info = InfoFor(item.GetType());

        if (info == null)
        {
            from.SendLocalizedMessage(1042599); // That does not look like something you want hanging on a wall.
            return true;
        }

        if (from.Backpack?.ConsumeTotal(typeof(Board), 10) != true)
        {
            from.SendLocalizedMessage(1042598); // You do not have enough boards.
            return true;
        }

        from.SendLocalizedMessage(1042278); // You review the corpse and find it worthy of a trophy.
        from.SendLocalizedMessage(1042602); // You use your kit up making the trophy.

        from.AddToBackpack(new FishTrophyDeed(weight, fisher, caught, info.DeedNumber, info.AddonNumber, IndexOf(info.FishType)));

        item.Delete();
        kit.Delete();
        return true;
    }
}

[SerializationGenerator(0, false)]
public partial class FishTrophyDeed : Item
{
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    [SerializableField(0, setter: "private")]
    private int _trophyWeight;

    [SerializedCommandProperty(AccessLevel.GameMaster)]
    [SerializableField(1, setter: "private")]
    private Mobile _fisher;

    [SerializedCommandProperty(AccessLevel.GameMaster)]
    [SerializableField(2, setter: "private")]
    private DateTime _dateCaught;

    [SerializedCommandProperty(AccessLevel.GameMaster)]
    [SerializableField(3, setter: "private")]
    private int _trophyName;

    [SerializedCommandProperty(AccessLevel.GameMaster)]
    [SerializableField(4, setter: "private")]
    private int _deedName;

    [SerializedCommandProperty(AccessLevel.GameMaster)]
    [SerializableField(5, setter: "private")]
    private int _trophyID;

    public FishTrophyDeed(int weight, Mobile fisher, DateTime caught, int deedName, int trophyName, int id) : base(0x14F0)
    {
        _trophyWeight = weight;
        _fisher = fisher;
        _dateCaught = caught;
        _deedName = deedName;
        _trophyName = trophyName;
        _trophyID = id;
    }

    public override int LabelNumber => _deedName;

    public override void OnDoubleClick(Mobile from)
    {
        if (!IsChildOf(from.Backpack))
        {
            from.SendLocalizedMessage(1042001); // That must be in your pack for you to use it.
            return;
        }

        var house = BaseHouse.FindHouseAt(from);

        if (house == null || !house.IsCoOwner(from))
        {
            from.SendLocalizedMessage(502092); // You must be in your house to do this.
            return;
        }

        var northWall = BaseAddon.IsWall(from.X, from.Y - 1, from.Z, from.Map);
        var westWall = BaseAddon.IsWall(from.X - 1, from.Y, from.Z, from.Map);

        if (northWall && westWall)
        {
            switch (from.Direction & Direction.Mask)
            {
                case Direction.North:
                case Direction.South:
                    northWall = true;
                    westWall = false;
                    break;
                case Direction.East:
                case Direction.West:
                    northWall = false;
                    westWall = true;
                    break;
                default:
                    from.SendMessage("Turn to face the wall on which to hang this trophy.");
                    return;
            }
        }

        BaseAddon addon = null;

        if (northWall)
        {
            addon = ConstructTrophy(true);
        }
        else if (westWall)
        {
            addon = ConstructTrophy(false);
        }
        else
        {
            from.SendLocalizedMessage(1042626); // The trophy must be placed next to a wall.
        }

        if (addon != null)
        {
            house.Addons.Add(addon);
            addon.MoveToWorld(from.Location, from.Map);
            Delete();
        }
    }

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);

        if (_trophyWeight >= 20)
        {
            if (_fisher != null)
            {
                list.Add(1070857, _fisher.Name); // Caught by ~1_fisherman~
            }

            list.Add(1070858, _trophyWeight.ToString()); // ~1_weight~ stones
        }
    }

    public BaseAddon ConstructTrophy(bool north)
    {
        if (_trophyID < 0 || _trophyID >= FishTrophies.Table.Length)
        {
            return null;
        }

        var info = FishTrophies.Table[_trophyID];

        return new FishTrophy(info.FishType, info.Style, north, _trophyName, _trophyWeight, _fisher, _dateCaught);
    }
}

[SerializationGenerator(0, false)]
public partial class FishTrophy : BaseAddon
{
    [SerializableField(0, setter: "private")]
    private string _typeName;

    [SerializedCommandProperty(AccessLevel.GameMaster)]
    [SerializableField(1, setter: "private")]
    private int _fishWeight;

    [SerializedCommandProperty(AccessLevel.GameMaster)]
    [SerializableField(2, setter: "private")]
    private Mobile _fisher;

    [SerializedCommandProperty(AccessLevel.GameMaster)]
    [SerializableField(3, setter: "private")]
    private DateTime _dateCaught;

    public FishTrophy(Type type, TrophyStyle style, bool north, int label, int weight, Mobile fisher, DateTime dateCaught)
    {
        var hue = FishInfo.GetFishHue(type);
        _typeName = type.Name;
        _fishWeight = weight;
        _fisher = fisher;
        _dateCaught = dateCaught;

        switch (style)
        {
            case TrophyStyle.Marlin:
                if (north)
                {
                    AddComponent(new TrophyComponent(17641, label, hue), 0, 0, 0);
                    AddComponent(new TrophyComponent(17642, label, hue), -1, 0, 0);
                    AddComponent(new TrophyComponent(17643, label, hue), 1, 0, 0);
                }
                else
                {
                    AddComponent(new TrophyComponent(17644, label, hue), 0, 0, 0);
                    AddComponent(new TrophyComponent(17645, label, hue), 0, 1, 0);
                    AddComponent(new TrophyComponent(17646, label, hue), 0, -1, 0);
                }

                break;
            case TrophyStyle.Dragonfish:
                AddComponent(new TrophyComponent(north ? 17639 : 17640, label, hue), 0, 0, 0);
                break;
            case TrophyStyle.BoardMahi:
                if (north)
                {
                    AddComponent(new TrophyComponent(19283, label, 0), 0, 0, 0);
                    AddComponent(new TrophyComponent(19287, label, hue), 0, 0, 0);
                }
                else
                {
                    AddComponent(new TrophyComponent(19282, label, 0), 0, 0, 0);
                    AddComponent(new TrophyComponent(19286, label, hue), 0, 0, 0);
                }

                break;
            case TrophyStyle.BoardGrouper:
                if (north)
                {
                    AddComponent(new TrophyComponent(19281, label, 0), 0, 0, 0);
                    AddComponent(new TrophyComponent(19285, label, hue), 0, 0, 0);
                }
                else
                {
                    AddComponent(new TrophyComponent(19280, label, 0), 0, 0, 0);
                    AddComponent(new TrophyComponent(19284, label, hue), 0, 0, 0);
                }

                break;
            case TrophyStyle.Crab:
                AddComponent(new TrophyComponent(north ? 18106 : 18107, label, hue), 0, 0, 0);
                break;
            case TrophyStyle.Lobster:
                AddComponent(new TrophyComponent(north ? 18108 : 18109, label, hue), 0, 0, 0);
                break;
        }
    }

    public Type FishType => _typeName == null ? null : AssemblyHandler.FindTypeByName(_typeName);

    public override BaseAddonDeed Deed => null;

    /// <summary>The deed this trophy folds back into, or null when its fish is not in the table.</summary>
    public Item TrophyDeed
    {
        get
        {
            var info = FishTrophies.InfoFor(FishType);

            return info == null
                ? null
                : new FishTrophyDeed(_fishWeight, _fisher, _dateCaught, info.DeedNumber, info.AddonNumber, FishTrophies.IndexOf(info.FishType));
        }
    }

    public override void OnChop(Mobile from)
    {
        if (Components.Count > 0)
        {
            OnComponentUsed(Components[0], from);
        }
    }

    public override void OnComponentUsed(AddonComponent c, Mobile from)
    {
        var deed = TrophyDeed;

        if (deed == null)
        {
            return;
        }

        var house = BaseHouse.FindHouseAt(c);

        if (house != null && house.IsCoOwner(from))
        {
            from.AddToBackpack(deed);
            house.Addons.Remove(this);
            Delete();
        }
        else
        {
            deed.Delete();
            from.SendLocalizedMessage(502092); // You must be in your house to do this.
        }
    }
}

[SerializationGenerator(0, false)]
public partial class TrophyComponent : LocalizedAddonComponent
{
    public TrophyComponent(int itemID, int label, int hue) : base(itemID, label) => Hue = hue;

    public override bool ForceShowProperties => true;

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);

        if (Addon is FishTrophy trophy)
        {
            list.Add(1070858, trophy.FishWeight.ToString()); // ~1_weight~ stones
            list.Add(1070857, trophy.Fisher != null ? trophy.Fisher.Name : "Unknown"); // Caught by ~1_fisherman~
            list.Add($"[{trophy.DateCaught.ToShortDateString()}]");
        }
    }
}
