// cc-P787: the beekeeping items besides the hive. All stock art (shard-migration notes/cc-P787-tools/art-check.txt):
//
//   BeehiveDeed   a deed (0x14F0) that places a beehive in its owner's house (HouseBeehive.Place)
//   BeeSwarm      a boxed swarm (wooden box 0x9AA) that starts a colony in an empty hive (swarmPopulation, swarmHealth)
//   HiveTool      a fork (0x9F5) with hiveToolUses uses: harvests a hive, or catches a swarm at a wild hive
//   RawBeeswax    raw wax from a hive (beeswax art 0x1422, hued rawWaxHue), rendered in a wax pot
//   Honeycomb     a wild comb (folded cloth art 0x1762, hued honeycombHue, as the Cooking Expansion Set has it); opened
//                 by hand into a jar of honey and a raw beeswax
//   WaxPot        a pot of wax (0x142A): at a heat source renders up to waxPotBatch raw beeswax into stock Beeswax, less
//                 waxPotWastePercent lost as slumgum. Sold by beekeepers and made by Tinkering.
//
// Honey comes out as stock jars of honey and wax as stock Beeswax, so Cooking and the candle recipes take them as they
// are.

#nullable enable

using System;
using ModernUO.Serialization;
using Server.Engines.Craft;
using Server.Items;
using Server.Targeting;

namespace Server.Engines.Beekeeping;

[SerializationGenerator(0, false)]
public partial class BeehiveDeed : Item
{
    [Constructible]
    public BeehiveDeed() : base(0x14F0)
    {
    }

    public override string DefaultName => "a beehive deed";

    public override double DefaultWeight => 1.0;

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add("Places a beehive in your house (one lockdown)");
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (!IsChildOf(from.Backpack))
        {
            from.SendLocalizedMessage(1042001); // That must be in your pack for you to use it.
            return;
        }

        from.SendMessage("Where in your house will you set up the beehive?");
        from.Target = new PlaceTarget(this);
    }

    private class PlaceTarget : Target
    {
        private readonly BeehiveDeed _deed;

        public PlaceTarget(BeehiveDeed deed) : base(3, true, TargetFlags.None) => _deed = deed;

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (_deed.Deleted || targeted is not IPoint3D p)
            {
                return;
            }

            var at = p is Item item ? item.GetWorldTop() : new Point3D(p);
            if (HouseBeehive.Place(from, _deed, at, from.Map) is { } why)
            {
                from.SendMessage(why);
            }
        }
    }
}

[SerializationGenerator(0, false)]
public partial class BeeSwarm : Item
{
    [Constructible]
    public BeeSwarm() : base(0x9AA)
    {
    }

    public override string DefaultName => "a bee swarm";

    public override double DefaultWeight => 2.0;

    public int Population => BeekeepingTable.Current.SwarmPopulation;

    public int Health => BeekeepingTable.Current.SwarmHealth;

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add("Use on an empty beehive to start a colony");
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (!IsChildOf(from.Backpack))
        {
            from.SendLocalizedMessage(1042001); // That must be in your pack for you to use it.
            return;
        }

        from.SendMessage("Which hive will the swarm settle in?");
        from.Target = new HiveTarget((m, hive) =>
        {
            if (!Deleted && IsChildOf(m.Backpack))
            {
                hive.AddSwarm(m, this);
            }
        });
    }
}

[SerializationGenerator(0, false)]
public partial class HiveTool : Item
{
    [SerializableField(0)]
    [InvalidateProperties]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private int _usesRemaining;

    [Constructible]
    public HiveTool() : base(0x9F5) => _usesRemaining = BeekeepingTable.Current.HiveToolUses;

    public override string DefaultName => "a hive tool";

    public override double DefaultWeight => 1.0;

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add(1060584, UsesRemaining.ToString()); // uses remaining: ~1_val~
    }

    public void UseOnce(Mobile from)
    {
        UsesRemaining--;
        if (UsesRemaining <= 0)
        {
            from.SendMessage("Your hive tool is worn out.");
            Delete();
        }
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (!IsChildOf(from.Backpack))
        {
            from.SendLocalizedMessage(1042001); // That must be in your pack for you to use it.
            return;
        }

        from.SendMessage("Use the hive tool on which hive?");
        from.Target = new ToolTarget(this);
    }

    private class ToolTarget : Target
    {
        private readonly HiveTool _tool;

        public ToolTarget(HiveTool tool) : base(3, false, TargetFlags.None) => _tool = tool;

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (_tool.Deleted || !_tool.IsChildOf(from.Backpack))
            {
                return;
            }

            switch (targeted)
            {
                case HouseBeehive hive:
                    hive.Harvest(from, _tool);
                    break;
                case WildBeehive wild:
                    wild.CatchSwarm(from, _tool);
                    break;
                default:
                    from.SendMessage("That is not a beehive.");
                    break;
            }
        }
    }
}

[SerializationGenerator(0, false)]
public partial class RawBeeswax : Item
{
    [Constructible]
    public RawBeeswax(int amount = 1) : base(0x1422)
    {
        Stackable = true;
        Amount = Math.Max(1, amount);
        Hue = BeekeepingTable.Current.RawWaxHue;
    }

    public override string DefaultName => "raw beeswax";

    public override double DefaultWeight => 1.0;
}

[SerializationGenerator(0, false)]
public partial class Honeycomb : Item
{
    [Constructible]
    public Honeycomb(int amount = 1) : base(0x1762)
    {
        Stackable = true;
        Amount = Math.Max(1, amount);
        Hue = BeekeepingTable.Current.HoneycombHue;
    }

    public override string DefaultName => "a honeycomb";

    public override double DefaultWeight => 1.0;

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add("Open it for honey and raw beeswax");
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (!IsChildOf(from.Backpack))
        {
            from.SendLocalizedMessage(1042001); // That must be in your pack for you to use it.
            return;
        }

        Open(from);
    }

    /// <summary>One comb into a jar of honey and a raw beeswax.</summary>
    public void Open(Mobile from)
    {
        Consume();
        from.AddToBackpack(new JarHoney());
        from.AddToBackpack(new RawBeeswax());
        from.SendMessage("You break open the comb: a jar of honey and a lump of raw beeswax.");
    }
}

[SerializationGenerator(0, false)]
public partial class WaxPot : Item
{
    // Pinned CraftItem's heat sources (CraftItem.cs:35-46, private there): ovens, fireplaces, campfires, firepits,
    // heating stands, fire fields, elven stoves and braziers.
    public static readonly int[] HeatSources =
    [
        0x461, 0x48E, 0x92B, 0x96C, 0xDE3, 0xDE9, 0xFAC, 0xFAC, 0x184A, 0x184C, 0x184E, 0x1850, 0x398C, 0x399F,
        0x2DDB, 0x2DDC, 0x19AA, 0x19BB
    ];

    [Constructible]
    public WaxPot() : base(0x142A)
    {
    }

    public override string DefaultName => "a wax pot";

    public override double DefaultWeight => 5.0;

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add("Renders raw beeswax at a heat source");
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (!IsChildOf(from.Backpack) && !from.InRange(GetWorldLocation(), 2))
        {
            from.SendLocalizedMessage(500446); // That is too far away.
            return;
        }

        if (Render(from) is { } why)
        {
            from.SendMessage(why);
        }
    }

    /// <summary>Renders one batch of the user's raw beeswax; null when done, else why not.</summary>
    public string? Render(Mobile from, bool checkHeat = true)
    {
        var t = BeekeepingTable.Current;
        var pack = from.Backpack;
        var raw = pack?.GetAmount(typeof(RawBeeswax)) ?? 0;
        if (raw <= 0)
        {
            return "You have no raw beeswax to render.";
        }

        if (checkHeat && !CraftItem.Find(from, HeatSources))
        {
            return "You must be near a heat source to render wax.";
        }

        var n = Math.Min(raw, t.WaxPotBatch);
        pack!.ConsumeTotal(typeof(RawBeeswax), n);
        var waste = n * t.WaxPotWastePercent / 100;
        var made = n - waste;
        if (made > 0)
        {
            from.AddToBackpack(new Beeswax(made));
        }

        from.PlaySound(0x21);
        from.SendMessage(waste > 0
            ? $"You render {n} raw beeswax into {made} beeswax; {waste} is lost as slumgum."
            : $"You render {n} raw beeswax into {made} beeswax.");
        return null;
    }
}
