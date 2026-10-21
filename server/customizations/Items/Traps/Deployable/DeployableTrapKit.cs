// cc-P275: the trap kits a tinker crafts (TinkeringCraftRegistrations, the Traps group) and a player sets with Remove
// Trap. One kit, one trap. An exceptional kit carries one more charge (the snare, gas, bear and shackle) and
// x1.2 damage; the crafter's mark stays on it.
//
// Payloads are dropped on the kit (D1): a poison potion on a gas trap is a Poisoning use with the potion's own window
// (pinned Skills/Poisoning.cs:124) and loads min(potion level, Poisoning / 25); an explosion potion on a blast trap
// loads its strength (lesser, normal, greater: Alchemy's work); fukiya darts on a dart trap load up to five darts, and
// a poisoned dart keeps its poison. The design's recipes listed the potion as a craft ingredient; loading it after
// the craft keeps one potion per set and lets a spent trap be reloaded in place.
//
// The practice net (D6) is a snare net kit flagged Practice: no skill to set, one charge, the practice hold, for the
// Burrows only (anywhere else it crumbles), never salvaged.

#nullable enable

using System;
using ModernUO.Serialization;
using Server.Engines.Craft;
using Server.Mobiles;
using Server.Regions;
using Server.SkillHandlers;
using Server.Targeting;

namespace Server.Items;

/// <summary>What a gas, blast or dart trap carries; a kit and a set trap both hold one.</summary>
public interface IDeployableTrapPayload
{
    DeployableTrapKind Kind { get; }
    Poison? GasPoison { get; set; }
    int BlastStrength { get; set; } // -1 empty, 0 lesser, 1 normal, 2 greater
    int Darts { get; set; }
    Poison? DartPoison { get; set; }
    int DartPoisonCharges { get; set; }
}

public static class DeployableTrapPayloads
{
    public static Poison? PoisonAt(int level) => level switch
    {
        <= 0 => Poison.Lesser,
        1    => Poison.Regular,
        2    => Poison.Greater,
        3    => Poison.Deadly,
        _    => Poison.Lethal
    };

    public static bool IsLoaded(IDeployableTrapPayload p) => p.Kind switch
    {
        DeployableTrapKind.Gas   => p.GasPoison != null,
        DeployableTrapKind.Blast => p.BlastStrength >= 0,
        DeployableTrapKind.Dart  => p.Darts > 0,
        _                        => true
    };

    public static bool TakesPayload(IDeployableTrapPayload p, Item dropped) => p.Kind switch
    {
        DeployableTrapKind.Gas   => dropped is BasePoisonPotion,
        DeployableTrapKind.Blast => dropped is BaseExplosionPotion,
        DeployableTrapKind.Dart  => dropped is FukiyaDarts,
        _                        => false
    };

    /// <summary>
    /// Loads <paramref name="dropped"/> into <paramref name="p"/>, using it up as it goes. A poison potion rolls
    /// Poisoning on its own window and is spent either way (as on a weapon). True when the trap now holds a payload.
    /// </summary>
    public static bool Load(Mobile from, IDeployableTrapPayload p, Item dropped)
    {
        switch (dropped)
        {
            case BasePoisonPotion potion when p.Kind == DeployableTrapKind.Gas:
                {
                    var success = from.CheckTargetSkill(SkillName.Poisoning, p, potion.MinPoisoningSkill, potion.MaxPoisoningSkill);
                    var level = Math.Min(potion.Poison?.Level ?? 0, (int)(from.Skills.Poisoning.Value / 25));
                    potion.Consume();
                    from.AddToBackpack(new Bottle());

                    if (!success)
                    {
                        from.SendMessage("You spill the poison loading the gas trap.");
                        return false;
                    }

                    p.GasPoison = PoisonAt(level);
                    from.SendMessage($"You load the gas trap with {p.GasPoison?.Name.ToLowerInvariant() ?? "poison"} poison.");
                    return true;
                }
            case BaseExplosionPotion potion when p.Kind == DeployableTrapKind.Blast:
                {
                    p.BlastStrength = potion switch
                    {
                        LesserExplosionPotion  => 0,
                        GreaterExplosionPotion => 2,
                        _                      => 1
                    };
                    potion.Consume();
                    from.AddToBackpack(new Bottle());
                    from.SendMessage("You pack the blast trap.");
                    return true;
                }
            case FukiyaDarts darts when p.Kind == DeployableTrapKind.Dart:
                {
                    var room = DeployableTraps.Setting(DeployableTrapKind.Dart, "charges") - p.Darts;
                    var take = Math.Min(room, darts.UsesRemaining);

                    if (take <= 0)
                    {
                        from.SendMessage("The dart trap is full.");
                        return p.Darts > 0;
                    }

                    if (darts.Poison != null && darts.PoisonCharges > 0)
                    {
                        var poisoned = Math.Min(take, darts.PoisonCharges);
                        p.DartPoison = darts.Poison;
                        p.DartPoisonCharges += poisoned;
                        darts.PoisonCharges -= poisoned;
                    }

                    p.Darts += take;
                    darts.UsesRemaining -= take;

                    if (darts.UsesRemaining <= 0)
                    {
                        darts.Delete();
                    }

                    from.SendMessage($"You load {take} dart{(take == 1 ? "" : "s")} into the dart trap.");
                    return true;
                }
        }

        return false;
    }

    public static void CopyPayload(IDeployableTrapPayload from, IDeployableTrapPayload to)
    {
        to.GasPoison = from.GasPoison;
        to.BlastStrength = from.BlastStrength;
        to.Darts = from.Darts;
        to.DartPoison = from.DartPoison;
        to.DartPoisonCharges = from.DartPoisonCharges;
    }
}

[SerializationGenerator(0, false)]
public abstract partial class DeployableTrapKit : Item, ICraftable, IDeployableTrapPayload
{
    [InvalidateProperties]
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private bool _exceptional;

    [InvalidateProperties]
    [SerializableField(1)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private Mobile? _crafter;

    [InvalidateProperties]
    [SerializableField(2)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private bool _practice;

    [InvalidateProperties]
    [SerializableField(3)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private Poison? _gasPoison;

    [InvalidateProperties]
    [SerializableField(4)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private int _blastStrength;

    [InvalidateProperties]
    [SerializableField(5)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private int _darts;

    [SerializableField(6)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private Poison? _dartPoison;

    [SerializableField(7)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private int _dartPoisonCharges;

    protected DeployableTrapKit(DeployableTrapKind kind) : base(DeployableTraps.SetArt(kind))
    {
        _blastStrength = -1;
    }

    public override double DefaultWeight => 2.0;

    public abstract DeployableTrapKind Kind { get; }

    public bool IsLoaded => DeployableTrapPayloads.IsLoaded(this);

    public override string DefaultName => Practice ? "a practice net" : $"a {DeployableTraps.NameOf(Kind)} kit";

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);

        if (Crafter != null)
        {
            list.Add(1050043, Crafter.Name); // crafted by ~1_NAME~
        }

        if (Exceptional)
        {
            list.Add(1060636); // exceptional
        }

        switch (Kind)
        {
            case DeployableTrapKind.Gas:
                list.Add(GasPoison == null ? "empty" : $"{GasPoison.Name} poison");
                break;
            case DeployableTrapKind.Blast:
                list.Add(BlastStrength switch { < 0 => "empty", 0 => "lesser charge", 1 => "charge", _ => "greater charge" });
                break;
            case DeployableTrapKind.Dart:
                list.Add(Darts == 0 ? "empty" : $"{Darts} dart{(Darts == 1 ? "" : "s")}");
                break;
        }
    }

    public int OnCraft(
        int quality, bool makersMark, Mobile from, CraftSystem craftSystem, Type typeRes, BaseTool tool,
        CraftItem craftItem, int resHue
    )
    {
        Exceptional = quality >= 2;

        if (makersMark)
        {
            Crafter = from;
        }

        return quality;
    }

    public override bool OnDragDrop(Mobile from, Item dropped)
    {
        if (!DeployableTrapPayloads.TakesPayload(this, dropped))
        {
            return base.OnDragDrop(from, dropped);
        }

        if (!IsChildOf(from.Backpack))
        {
            from.SendLocalizedMessage(1054107); // This item must be in your backpack.
            return false;
        }

        DeployableTrapPayloads.Load(from, this, dropped);
        return dropped.Deleted;
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (!IsChildOf(from.Backpack))
        {
            from.SendLocalizedMessage(1054107); // This item must be in your backpack.
            return;
        }

        if (Practice && from.Region?.IsPartOf<OldHavenBurrowsRegion>() != true)
        {
            from.SendMessage("The practice net crumbles. It was never meant to leave the Burrows.");
            Delete();
            return;
        }

        if (!DeployableTraps.CanSetAt(from, this, from.Map, from.Location, out var refusal) && !IsPlaceRefusal(refusal))
        {
            from.SendMessage(refusal);
            return;
        }

        from.SendMessage($"Where will you set the {DeployableTraps.NameOf(Kind)}?");
        from.Target = new SetTarget(this);
    }

    // The refusals that depend on the spot (asked again at the target) rather than the setter.
    private static bool IsPlaceRefusal(string? refusal) =>
        refusal is "You cannot set a trap there." or "That is too far away." or "There are enough traps here already."
            or "You cannot set a trap in a town or a house." or "The practice net is for the Burrows.";

    /// <summary>
    /// Sets the trap at <paramref name="p"/> now (the arming is done): checks the spot again, places a DeployedTrap
    /// owned by <paramref name="from"/>, uses up the kit and rolls the set's Remove Trap gain. Null when refused.
    /// </summary>
    public DeployedTrap? Set(Mobile from, Point3D p, bool hidden)
    {
        var map = from.Map;
        string? refusal = null;

        if (Deleted || !IsChildOf(from.Backpack) || !DeployableTraps.CanSetAt(from, this, map, p, out refusal))
        {
            from.SendMessage(refusal ?? "You cannot set that now.");
            return null;
        }

        var trap = new DeployedTrap(Kind, from, from.Skills.RemoveTrap.Value, hidden, Exceptional, Practice);
        DeployableTrapPayloads.CopyPayload(this, trap);
        trap.ResetCharges();
        trap.MoveToWorld(p, map);

        var (min, max) = DeployableTraps.SetGainWindow(Kind);
        from.CheckSkill(SkillName.RemoveTrap, min, max);

        var name = DeployableTraps.NameOf(Kind);
        from.SendMessage(
            !hidden ? $"You set the {name}." :
            from.Hidden ? $"You set the {name} without a sound." : $"You set the {name}, and something hears you."
        );

        Consume();
        return trap;
    }

    /// <summary>
    /// The hidden set's one stealth step (the design's section 2): pinned's step check (Skills/Stealth.cs), one of the
    /// allowed steps spent. A miss reveals the setter; the trap is set anyway.
    /// </summary>
    public static void StealthStep(Mobile from)
    {
        var ar = Stealth.GetArmorRating(from);

        if (!from.CheckSkill(SkillName.Stealth, -20.0 + ar * 2, 60.0 + ar * 2) || from.AllowedStealthSteps-- <= 0)
        {
            from.RevealingAction();
        }
    }

    private class SetTarget : Target
    {
        private readonly DeployableTrapKit _kit;

        public SetTarget(DeployableTrapKit kit) : base(DeployableTraps.SetRange, true, TargetFlags.None) => _kit = kit;

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (targeted is not IPoint3D ip)
            {
                return;
            }

            var p = new Point3D(ip);

            if (targeted is Item { Parent: null } item)
            {
                p = new Point3D(item.X, item.Y, item.Z + item.ItemData.CalcHeight);
            }

            if (!DeployableTraps.CanSetAt(from, _kit, from.Map, p, out var refusal))
            {
                from.SendMessage(refusal);
                return;
            }

            var hidden = from.Hidden;
            var seconds = DeployableTraps.ArmingSeconds(from.Skills.RemoveTrap.Value, hidden);
            var start = from.Location;

            from.SendMessage($"You begin to arm the {DeployableTraps.NameOf(_kit.Kind)}...");

            Timer.StartTimer(TimeSpan.FromSeconds(seconds), () =>
            {
                // Moving or dying spoils the arming, as a bandage is spoiled.
                if (from.Deleted || !from.Alive || from.Location != start)
                {
                    from.SendMessage("You stop arming the trap.");
                    return;
                }

                if (hidden && from.Hidden)
                {
                    StealthStep(from);
                }

                _kit.Set(from, p, hidden);
            });
        }
    }
}

[SerializationGenerator(0, false)]
public partial class SnareNetKit : DeployableTrapKit
{
    [Constructible]
    public SnareNetKit() : base(DeployableTrapKind.SnareNet)
    {
    }

    public override DeployableTrapKind Kind => DeployableTrapKind.SnareNet;

    /// <summary>The Echo's practice net (D6).</summary>
    public static SnareNetKit MakePractice() => new() { Practice = true, Hue = DeployableTraps.PracticeHue };
}

[SerializationGenerator(0, false)]
public partial class CaltropsKit : DeployableTrapKit
{
    [Constructible]
    public CaltropsKit() : base(DeployableTrapKind.Caltrops)
    {
    }

    public override DeployableTrapKind Kind => DeployableTrapKind.Caltrops;
}

[SerializationGenerator(0, false)]
public partial class GasTrapKit : DeployableTrapKit
{
    [Constructible]
    public GasTrapKit() : base(DeployableTrapKind.Gas) => Hue = 0x44; // the placeholder potion, a gas green

    public override DeployableTrapKind Kind => DeployableTrapKind.Gas;
}

[SerializationGenerator(0, false)]
public partial class BlastTrapKit : DeployableTrapKit
{
    [Constructible]
    public BlastTrapKit() : base(DeployableTrapKind.Blast) => Hue = 0x497; // a dark keg

    public override DeployableTrapKind Kind => DeployableTrapKind.Blast;
}

[SerializationGenerator(0, false)]
public partial class DartTrapKit : DeployableTrapKit
{
    [Constructible]
    public DartTrapKit() : base(DeployableTrapKind.Dart)
    {
    }

    public override DeployableTrapKind Kind => DeployableTrapKind.Dart;
}

[SerializationGenerator(0, false)]
public partial class BearTrapKit : DeployableTrapKit
{
    [Constructible]
    public BearTrapKit() : base(DeployableTrapKind.BearTrap)
    {
    }

    public override DeployableTrapKind Kind => DeployableTrapKind.BearTrap;
}

[SerializationGenerator(0, false)]
public partial class AlarmTrapKit : DeployableTrapKit
{
    [Constructible]
    public AlarmTrapKit() : base(DeployableTrapKind.Alarm)
    {
    }

    public override DeployableTrapKind Kind => DeployableTrapKind.Alarm;
}

[SerializationGenerator(0, false)]
public partial class ShackleTrapKit : DeployableTrapKit
{
    [Constructible]
    public ShackleTrapKit() : base(DeployableTrapKind.Shackle) => Hue = 0x48E; // a rune blue on the placeholder scroll

    public override DeployableTrapKind Kind => DeployableTrapKind.Shackle;
}
