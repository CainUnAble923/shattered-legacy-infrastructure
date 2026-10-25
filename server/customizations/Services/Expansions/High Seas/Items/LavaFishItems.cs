// cc-P269: ServUO pub57 Scripts/Services/Expansions/High Seas/Items/LavaFishItems.cs -> ours, High Seas fishing, second
// half. The things a lava-hooked pole pulls out of lava (HighSeasFishing's lava table) besides lava fish:
//   - Stone footwear (boots, sandals, shoes or thigh boots): wearable, and whoever wears it cannot move. Putting it on
//     frees a player from spider webbing.
//   - A cracked lava rock: split with an axe into a geode, a geode shard or a lava rock (the east-facing rock needs a
//     strong arm: it fails unless the player's raw Strength beats a roll of 0-149).
//   - A stone paver (decoration).
//
// Changes from ServUO (shard-migration notes/cc-P269-high-seas-fishing-second-half.md):
//   - ServUO splits the rock from a patch to the harvest target (Lumberjacking on a cracked rock calls OnCrack). Pinned's
//     harvest target already hands an axe's target to any IAxe item (Engines/Harvest/Core/HarvestTarget.cs), so the rocks
//     are IAxe and need no patch; pinned asks that an IAxe item be in the backpack, which ServUO did not.
//   - Weights are each item's DefaultWeight (ServUO set Weight in the constructor and patched old saves in Deserialize).

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class StoneFootwear : BaseShoes
{
    [Constructible]
    public StoneFootwear() : this(Utility.Random(5899, 8))
    {
    }

    [Constructible]
    public StoneFootwear(int itemID) : base(itemID) => StrRequirement = 10;

    public override double DefaultWeight =>
        GetNameInfo(ItemID) switch
        {
            "thigh boots" => 4.0,
            "boots"       => 3.0,
            "shoes"       => 2.0,
            "sandals"     => 1.0,
            _             => base.DefaultWeight
        };

    public static void Initialize()
    {
        EventSink.Movement += EventSink_Movement;
    }

    public static string GetNameInfo(int itemID) =>
        itemID switch
        {
            5899 or 5900 => "boots",
            5901 or 5902 => "sandals",
            5903 or 5904 => "shoes",
            5905 or 5906 => "thigh boots",
            _            => "foot wear"
        };

    public override void OnAdded(IEntity parent)
    {
        base.OnAdded(parent);

        if (parent is Mobile m)
        {
            if (SpiderWebbing.IsTrapped(m))
            {
                SpiderWebbing.RemoveEffects(m);
            }

            m.SendLocalizedMessage(1151094, GetNameInfo(ItemID)); // You manage to equip the stone ~1_token~ and find you can no longer move!
        }
    }

    public override void AddNameProperty(IPropertyList list)
    {
        list.Add(1151095, GetNameInfo(ItemID)); // stone ~1_token~
    }

    public static void EventSink_Movement(MovementEventArgs e)
    {
        var from = e.Mobile;

        if (from?.Alive == true && from.FindItemOnLayer(Layer.Shoes) is StoneFootwear)
        {
            e.Blocked = true;
        }
    }
}

[SerializationGenerator(0, false)]
public partial class CrackedLavaRockSouth : Item, IAxe
{
    [Constructible]
    public CrackedLavaRockSouth() : base(19279)
    {
    }

    public override int LabelNumber => 1098151;

    public bool Axe(Mobile from, BaseAxe axe)
    {
        OnCrack(from);
        return false;
    }

    public virtual void OnCrack(Mobile from)
    {
        from.AddToBackpack(CrackedLavaRock.RandomInside());
        from.SendMessage("You have split the lava rock!");
        Delete();
    }
}

[SerializationGenerator(0, false)]
public partial class CrackedLavaRockEast : Item, IAxe
{
    [Constructible]
    public CrackedLavaRockEast() : base(19275)
    {
    }

    public override int LabelNumber => 1098151;

    public bool Axe(Mobile from, BaseAxe axe)
    {
        OnCrack(from);
        return false;
    }

    public virtual void OnCrack(Mobile from)
    {
        from.SendSound(0x3B3);

        if (from.RawStr < Utility.Random(150))
        {
            from.SendMessage("You swing, but fail to crack the rock any further.");
            return;
        }

        from.AddToBackpack(CrackedLavaRock.RandomInside());
        from.SendMessage("You have split the lava rock!");
        Delete();
    }
}

/// <summary>What a cracked lava rock splits into: ServUO's five-way roll, shared by both rocks.</summary>
public static class CrackedLavaRock
{
    public static Item RandomInside() =>
        Utility.Random(5) switch
        {
            0 => new GeodeEast(),
            1 => new GeodeSouth(),
            2 => new GeodeShardEast(),
            3 => new GeodeShardSouth(),
            _ => new LavaRock()
        };

    /// <summary>The four hues a geode or geode shard comes in.</summary>
    public static int RandomGeodeHue() => Utility.Random(4) switch
    {
        0 => 2658,
        1 => 2659,
        2 => 2660,
        _ => 2654
    };
}

[SerializationGenerator(0, false)]
public partial class GeodeSouth : Item
{
    [Constructible]
    public GeodeSouth() : base(Utility.Random(19277, 2)) => Hue = CrackedLavaRock.RandomGeodeHue();

    public override int LabelNumber => 1098145;
}

[SerializationGenerator(0, false)]
public partial class GeodeEast : Item
{
    [Constructible]
    public GeodeEast() : base(Utility.Random(19273, 2)) => Hue = CrackedLavaRock.RandomGeodeHue();

    public override int LabelNumber => 1098145;
}

[SerializationGenerator(0, false)]
public partial class GeodeShardSouth : Item
{
    [Constructible]
    public GeodeShardSouth() : base(19276) => Hue = CrackedLavaRock.RandomGeodeHue();

    public override int LabelNumber => 1098148;
}

[SerializationGenerator(0, false)]
public partial class GeodeShardEast : Item
{
    [Constructible]
    public GeodeShardEast() : base(19272) => Hue = CrackedLavaRock.RandomGeodeHue();

    public override int LabelNumber => 1098148;
}

[SerializationGenerator(0, false)]
public partial class LavaRock : Item
{
    [Constructible]
    public LavaRock() : base(Utility.Random(4964, 6)) => Hue = 1175;

    public override int LabelNumber => 1151166;
}

[SerializationGenerator(0, false)]
public partial class StonePaver : Item
{
    [Constructible]
    public StonePaver() : base(Utility.RandomList(18396, 18397, 18398, 18399, 18400, 18405, 18652, 18653, 18654, 18655))
    {
    }

    public override int LabelNumber => 1097277;
    public override double DefaultWeight => 5.0;
}
