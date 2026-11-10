// cc-P859: the Horn of Plenty (OSI Thanksgiving 2012), the Roasting Pig on a Spit and its two pork foods.
//
// Ported from Nuggzy's "Horn of Plenty (Nov 2012) w/ Roasting Pig addon, tamable turkeys and food" v1.2
// (https://www.servuo.dev/archive/horn-of-plenty-nov-2012-w-roasting-pig-addon-tamable-turkeys-and-food.489/), read as
// reference only; written fresh for ModernUO. Credit: Nuggzy. Checked against ServUO pub57's own HornOfPlenty
// (Services/HuntmasterChallenge/RewardItems.cs) and RoastingPigOnASpit (Items/Addons/RoastingPigOnASpit.cs).
//
// What the horn does (Nuggzy's odds): 10 charges; each use spends one charge and gives
//   9%  a Roasting Pig on a Spit deed,
//   16% a tamable wild turkey at your feet (1 in 10 of them is Mister Gobbles),
//   8%  a 10-second nap (frozen, "ZzzzZzzzZzzzZ"),
//   67% one of the Thanksgiving foods we already have (ThanksGivingFood.cs).
// In November it refills one charge a day, up to 10.
//
// Changed from Nuggzy, each in the notes (shard-migration notes/cc-P859-horn-of-plenty.md):
//   * Every outcome spends a charge (Nuggzy's turkey and nap were free, so a horn made endless turkeys). ServUO does the
//     same.
//   * The refill is a date check saved on the horn (NextRecharge), done when the horn is used, not one 1-day timer per
//     use that a restart forgets.
//   * The turkey appears where the user stands (Nuggzy used the horn's own X and Y, which are container coordinates when
//     the horn is in a pack).
//   * The horn must be in your pack to use.
//   * The pig gives pork once a day only to its house's owner or a co-owner (ServUO's house rule; Nuggzy had none), and
//     its next-pork time travels with the deed when it is redeeded.

#nullable enable

using System;
using ModernUO.Serialization;
using Server.Gumps;
using Server.Mobiles;
using Server.Multis;
using Server.Network;

namespace Server.Items;

[Flippable(0x4BD9, 0x4BDA)]
[SerializationGenerator(0, false)]
public partial class HornOfPlenty : Item
{
    public const int MaxCharges = 10;

    public const double PigChance = 0.09;
    public const double TurkeyChance = 0.25; // below this and at or above PigChance
    public const double NapChance = 0.33;    // below this and at or above TurkeyChance
    public const double GobblesChance = 0.10;

    public static readonly TimeSpan NapLength = TimeSpan.FromSeconds(10);

    /// <summary>The time the horn reads for its November refill. Tests replace it.</summary>
    public static Func<DateTime> Clock = () => Core.Now;

    /// <summary>The roll for each use, in [0, 1). Tests replace it.</summary>
    public static Func<double> Roll = Utility.RandomDouble;

    [InvalidateProperties]
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private int _charges;

    [SerializableField(1)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private DateTime _nextRecharge;

    [Constructible]
    public HornOfPlenty() : base(0x4BD9)
    {
        _charges = MaxCharges;
        Weight = 3.0;
    }

    public override int LabelNumber => 1153503; // Horn of Plenty

    public static bool IsHarvestMonth(DateTime when) => when.Month == 11;

    /// <summary>In November, one charge back a day, up to the maximum. Returns whether a charge came back.</summary>
    public bool CheckRecharge()
    {
        var now = Clock();

        if (!IsHarvestMonth(now) || _charges >= MaxCharges || now < _nextRecharge)
        {
            return false;
        }

        Charges = _charges + 1;
        NextRecharge = now + TimeSpan.FromDays(1);
        return true;
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (!IsChildOf(from.Backpack))
        {
            from.SendLocalizedMessage(1062334); // This item must be in your backpack to be used.
            return;
        }

        CheckRecharge();

        if (_charges <= 0)
        {
            from.SendLocalizedMessage(502412); // There are no charges left on that item.
            return;
        }

        Use(from, Roll());
    }

    /// <summary>One use with the given roll: spends a charge and hands out what the roll names.</summary>
    public void Use(Mobile from, double roll)
    {
        Charges = _charges - 1;

        // The first use after the horn runs below full starts the November clock from now.
        if (_nextRecharge < Clock())
        {
            NextRecharge = Clock() + TimeSpan.FromDays(1);
        }

        if (roll < PigChance)
        {
            Give(from, new RoastingPigOnASpitDeed());
        }
        else if (roll < TurkeyChance)
        {
            SpawnTurkey(from, Roll() < GobblesChance);
        }
        else if (roll < NapChance)
        {
            Nap(from);
        }
        else
        {
            Give(from, RandomFood(roll));
        }
    }

    public static Item RandomFood(double roll)
    {
        // The food band is [NapChance, 1): split it in four.
        var band = (int)((roll - NapChance) / (1.0 - NapChance) * 4);

        return Math.Clamp(band, 0, 3) switch
        {
            0 => new SweetPotatoPie(),
            1 => new MashedSweetPotatoes(),
            2 => new BasketOfRolls(),
            _ => new TurkeyPlatter()
        };
    }

    private static void Give(Mobile from, Item item)
    {
        if (from.Backpack?.TryDropItem(from, item, false) != true)
        {
            item.MoveToWorld(from.Location, from.Map);
        }
    }

    public static BaseCreature SpawnTurkey(Mobile from, bool gobbles)
    {
        BaseCreature turkey = gobbles ? new MisterGobbles() : new Turkey(true);
        turkey.MoveToWorld(from.Location, from.Map);

        from.SendLocalizedMessage(1153512); // That one's not cooked!
        return turkey;
    }

    public static void Nap(Mobile from)
    {
        from.Frozen = true;
        from.Say(1153513); // * ZzzzZzzzZzzzZ *

        var ticks = 0;
        var length = (int)NapLength.TotalSeconds;

        Timer.StartTimer(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), length, () =>
        {
            ticks++;

            if (ticks >= length)
            {
                from.Frozen = false;
            }
            else if (ticks % 3 == 0)
            {
                from.Say(1153513);
            }
        });
    }

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add(1060741, $"{_charges}"); // charges: ~1_val~
        list.Add("Refills in November");
    }
}

[SerializationGenerator(0, false)]
public partial class RoastingPigOnASpitAddon : BaseAddon
{
    public static readonly TimeSpan PorkDelay = TimeSpan.FromDays(1);

    /// <summary>The pork roll, in [0, 1): below 0.6 a platter, else a sandwich. Tests replace it.</summary>
    public static Func<double> Roll = Utility.RandomDouble;

    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private DateTime _nextEatTime;

    [SerializableField(1)]
    private bool _east;

    [Constructible]
    public RoastingPigOnASpitAddon(bool east = false, DateTime nextEatTime = default)
    {
        _east = east;
        _nextEatTime = nextEatTime;

        if (east)
        {
            AddComponent(new RoastingPigOnASpitComponent(0x9989), 0, 0, 0);
            AddComponent(new LocalizedAddonComponent(0x9988, 1123328), 1, -1, 0); // pile of logs
        }
        else
        {
            AddComponent(new RoastingPigOnASpitComponent(0x9995), 0, 0, 0);
            AddComponent(new LocalizedAddonComponent(0x9994, 1123328), -1, 1, 0); // pile of logs
        }
    }

    public override BaseAddonDeed Deed => new RoastingPigOnASpitDeed { NextEatTime = _nextEatTime };

    public override void OnComponentUsed(AddonComponent c, Mobile from)
    {
        if (c is not RoastingPigOnASpitComponent)
        {
            return;
        }

        if (!from.InRange(c.Location, 3) || from.Map != c.Map)
        {
            from.LocalOverheadMessage(MessageType.Regular, 0x3B2, 1019045); // I can't reach that.
            return;
        }

        var house = BaseHouse.FindHouseAt(from);

        if (house == null || house != BaseHouse.FindHouseAt(this) ||
            !(house.IsOwner(from) || house.IsCoOwner(from)))
        {
            from.SendLocalizedMessage(502092); // You must be in your house to do this.
            return;
        }

        if (Core.Now < _nextEatTime)
        {
            from.SendLocalizedMessage(1154555); // The pig is not quite ready for eating yet.
            return;
        }

        Item pork = Roll() < 0.6 ? new PulledPorkPlatter() : new PulledPorkSandwich();

        if (from.Backpack?.TryDropItem(from, pork, false) != true)
        {
            pork.MoveToWorld(from.Location, from.Map);
        }

        from.LocalOverheadMessage(MessageType.Regular, 0x35, 1154556); // *You cut some meat from the roasting pig...*
        NextEatTime = Core.Now + PorkDelay;
    }
}

[SerializationGenerator(0, false)]
public partial class RoastingPigOnASpitComponent : LocalizedAddonComponent
{
    public RoastingPigOnASpitComponent(int itemID) : base(itemID, 1123329) // Roasting Pig on a Spit
    {
    }

    public override bool ForceShowProperties => true;
}

[SerializationGenerator(0, false)]
public partial class RoastingPigOnASpitDeed : BaseAddonDeed, IRewardOption
{
    public const int South = 0;
    public const int East = 1;

    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private DateTime _nextEatTime;

    private bool _east;

    [Constructible]
    public RoastingPigOnASpitDeed() => LootType = LootType.Blessed;

    public override int LabelNumber => 1154557; // Deed for a Roasting Pig on a Spit

    public override BaseAddon Addon => new RoastingPigOnASpitAddon(_east, _nextEatTime);

    public void GetOptions(RewardOptionList list)
    {
        list.Add(South, 1075386); // South
        list.Add(East, 1075387);  // East
    }

    public void OnOptionSelected(Mobile from, int choice)
    {
        _east = choice == East;

        if (!Deleted)
        {
            base.OnDoubleClick(from);
        }
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (IsChildOf(from.Backpack))
        {
            RewardOptionGump.DisplayTo(from, this, 1154194); // Choose a Facing:
        }
        else
        {
            from.SendLocalizedMessage(1062334); // This item must be in your backpack to be used.
        }
    }
}

[SerializationGenerator(0, false)]
public partial class PulledPorkPlatter : Food
{
    [Constructible]
    public PulledPorkPlatter() : base(0x999F)
    {
        Stackable = false;
        Weight = 1.0;
        FillFactor = 5;
        Hue = 1157;
    }

    public override int LabelNumber => 1123351; // Pulled Pork Platter
}

[SerializationGenerator(0, false)]
public partial class PulledPorkSandwich : Food
{
    [Constructible]
    public PulledPorkSandwich() : base(0x99A0)
    {
        Stackable = false;
        Weight = 1.0;
        FillFactor = 3;
    }

    public override int LabelNumber => 1123352; // Pulled Pork Sandwich
}
