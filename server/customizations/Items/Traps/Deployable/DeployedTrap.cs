// cc-P275: a set trap on the floor (the rules are in DeployableTraps.cs). Hidden from everyone but its owner, the
// owner's party and staff (a CanSeeStaffOnly override, pinned Server/Items/Item.cs:471), unmovable, and a step-watcher
// as pinned's BaseTrap is (HandlesOnMovement, the same Z window, fires on the step that enters its reach).
//
// It fires when a creature enters its reach and every one of these holds: the trap has a charge; the owner is present
// (DeployableTraps.OwnerPresent); the creature is a fair target (DeployableTraps.IsTarget: monsters only unless the
// Felucca switch); it does not shrug this trap off (flying over a ground trap, incorporeal through a physical one, a
// boss below the Den's Master); and, if it has Detect Hidden, it did not spot the trap on its one roll.
//
// Detect Hidden finds it (IRevealableItem, the hook of patches/DetectHidden-revealable-items.patch) against its
// concealment and shows it for 10 seconds. Remove Trap disarms it (the IRemoveTrapTrainingKit hook of
// patches/RemoveTrap-trap-training-kits.patch, which hands the skill's target to the item): the owner always packs it
// back into a kit; anyone else rolls Remove Trap on the kit's window and salvages the kit on a success (a Den
// Grandmaster always succeeds). A practice net is never salvaged.

#nullable enable

using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Engines.CreatureStealing;
using Server.Mobiles;
using Server.Network;
using Server.Regions;
using Server.SkillHandlers;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class DeployedTrap : Item, IDeployableTrapPayload, IRevealableItem, IRemoveTrapTrainingKit
{
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private DeployableTrapKind _kind;

    [SerializableField(1)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private Mobile? _owner;

    [SerializableField(2)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private double _setterSkill;

    [SerializableField(3)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private bool _placedHidden;

    [SerializableField(4)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private bool _exceptional;

    [SerializableField(5)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private bool _practice;

    [InvalidateProperties]
    [SerializableField(6)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private int _charges;

    [SerializableField(7)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private bool _ambushUsed;

    [SerializableField(8)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private Poison? _gasPoison;

    [SerializableField(9)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private int _blastStrength;

    [SerializableField(10)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private int _darts;

    [SerializableField(11)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private Poison? _dartPoison;

    [SerializableField(12)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private int _dartPoisonCharges;

    // Creatures that have had their one Detect Hidden roll at this trap (not saved: a restart lets them roll again).
    private readonly HashSet<Serial> _rolled = [];

    public DeployedTrap(DeployableTrapKind kind, Mobile owner, double setterSkill, bool placedHidden, bool exceptional, bool practice)
        : base(DeployableTraps.SetArt(kind))
    {
        _kind = kind;
        _owner = owner;
        _setterSkill = setterSkill;
        _placedHidden = placedHidden;
        _exceptional = exceptional;
        _practice = practice;
        _blastStrength = -1;
        Movable = false;
        Visible = false;
        Hue = practice ? DeployableTraps.PracticeHue : 0;
        DeployableTraps.Placed.Add(this);
    }

    public bool Armed => Charges > 0;

    public int Concealment => DeployableTraps.Concealment(SetterSkill, PlacedHidden);

    public override bool HandlesOnMovement => Armed;

    public override string DefaultName =>
        $"a {(Practice ? "practice net" : DeployableTraps.NameOf(Kind))} [{(Armed ? $"armed, {Charges} charge{(Charges == 1 ? "" : "s")}" : "spent")}]";

    // Litter control (the design's section 5): the world's item decay, from the last set.
    public override bool Decays => true;

    public override TimeSpan DecayTime =>
        TimeSpan.FromMinutes(Armed ? DeployableTraps.DecayMinutes : DeployableTraps.SpentDecayMinutes);

    public override bool CanSeeStaffOnly(Mobile from) => base.CanSeeStaffOnly(from) || DeployableTraps.CanSee(this, from);

    /// <summary>A fresh set's charges: the trap's own, or (a dart trap) one for each loaded dart.</summary>
    public void ResetCharges()
    {
        Charges = Kind == DeployableTrapKind.Dart ? Darts :
            Practice ? 1 : DeployableTraps.Charges(Kind, Exceptional);
        AmbushUsed = false;
        ItemID = DeployableTraps.SetArt(Kind);
        Hue = Practice ? DeployableTraps.PracticeHue : 0;
        LastMoved = Core.Now;
        _rolled.Clear();
    }

    private void Spend()
    {
        Charges = 0;
        GasPoison = null;   // the cloud is gone
        BlastStrength = -1; // and the charge with it
        ItemID = DeployableTraps.SpentArt(Kind);

        if (ItemID == DeployableTraps.SetArt(Kind))
        {
            Hue = DeployableTraps.SpentHue;
        }

        LastMoved = Core.Now; // the spent clock starts now
    }

    // -- Triggering -----------------------------------------------------------------------------------------------

    private bool InReach(Point3D loc, int range) =>
        Z + 8 >= loc.Z && loc.Z + 16 > Z && Utility.InRange(GetWorldLocation(), loc, range);

    public override void OnMovement(Mobile m, Point3D oldLocation)
    {
        base.OnMovement(m, oldLocation);

        if (!Armed || m.Location == oldLocation)
        {
            return;
        }

        var range = DeployableTraps.TriggerRadius(Kind, SetterSkill);

        if (InReach(m.Location, range) && !InReach(oldLocation, range))
        {
            TryTrigger(m);
        }
    }

    /// <summary>The trap's whole decision for one creature entering its reach. True when it fired.</summary>
    public bool TryTrigger(Mobile m)
    {
        if (Deleted || !Armed || !DeployableTraps.OwnerPresent(this) || !DeployableTraps.IsTarget(this, m))
        {
            return false; // dormant, or not something a trap touches
        }

        var shrug = DeployableTraps.Shrugs(this, m);

        if (shrug != null)
        {
            if (shrug == "boss")
            {
                Owner!.SendMessage(DeployableTraps.BossNotTrustedLine(m, Kind));
            }

            return false;
        }

        if (Spots(m))
        {
            return false;
        }

        Fire(m);
        return true;
    }

    // A creature with Detect Hidden rolls once per trap, the player formula (DetectHidden.cs:197-211).
    private bool Spots(Mobile m)
    {
        var dh = m.Skills.DetectHidden.Value;

        if (m is not BaseCreature || dh <= 0 || !_rolled.Add(m.Serial))
        {
            return false;
        }

        if (dh + Utility.Random(21) - 10 < Concealment + Utility.Random(21) - 10)
        {
            return false;
        }

        Reveal();
        Owner?.SendMessage($"The {m.Name} sniffs out your {DeployableTraps.NameOf(Kind)}.");
        return true;
    }

    private void Fire(Mobile trigger)
    {
        var owner = Owner!;
        var name = DeployableTraps.NameOf(Kind);
        var ambush = PlacedHidden && !AmbushUsed && trigger.Combatant == null;
        AmbushUsed = true;

        var targets = new List<Mobile> { trigger };

        if (Kind is DeployableTrapKind.Gas or DeployableTrapKind.Blast or DeployableTrapKind.Shackle && Map is { } map)
        {
            foreach (var m in map.GetMobilesInRange(GetWorldLocation(), DeployableTraps.Setting(Kind, "radius")))
            {
                if (m != trigger && InReach(m.Location, DeployableTraps.Setting(Kind, "radius")) &&
                    DeployableTraps.IsTarget(this, m) && DeployableTraps.Shrugs(this, m) == null)
                {
                    targets.Add(m);
                }
            }
        }

        switch (Kind)
        {
            case DeployableTrapKind.SnareNet:
                {
                    Hold(trigger, Practice ? DeployableTraps.PracticeHoldSeconds : DeployableTraps.HoldSeconds(Kind, SetterSkill));
                    trigger.FixedEffect(0x376A, 10, 16);
                    trigger.PlaySound(0x204);
                    Emote(trigger, "*tangled*");
                    owner.SendMessage($"Your net has the {trigger.Name}.");
                    TrapLesson.OnNetted(owner, trigger);
                    break;
                }
            case DeployableTrapKind.Caltrops:
                {
                    Damage(trigger, Roll("damageMin", "damageMax", ambush, trigger), 100, 0);
                    BleedAttack.BeginBleed(trigger, owner);
                    Slow(trigger, DeployableTraps.Setting(Kind, "slowSeconds"));
                    trigger.PlaySound(0x22B);
                    new Blood().MoveToWorld(trigger.Location, trigger.Map);
                    Emote(trigger, "*limps*");
                    break;
                }
            case DeployableTrapKind.Gas:
                {
                    Effects.SendLocationEffect(GetWorldLocation(), Map, 0x113A, 20, 10);
                    Effects.PlaySound(GetWorldLocation(), Map, 0x231);

                    foreach (var t in targets)
                    {
                        Harm(t);
                        t.ApplyPoison(owner, GasPoison);
                        Emote(t, "*coughs*");
                    }

                    owner.SendMessage("Your gas trap fires.");
                    break;
                }
            case DeployableTrapKind.Blast:
                {
                    var (min, max) = BlastStrength switch
                    {
                        <= 0 => ("lesserMin", "lesserMax"),
                        1    => ("damageMin", "damageMax"),
                        _    => ("greaterMin", "greaterMax")
                    };

                    Effects.SendLocationEffect(GetWorldLocation(), Map, 0x36BD, 20, 10);
                    Effects.PlaySound(GetWorldLocation(), Map, 0x307);

                    foreach (var t in targets)
                    {
                        Damage(t, Roll(min, max, ambush, t), 0, 100);
                        Emote(t, "*burns*");
                    }

                    owner.SendMessage("Your blast trap fires.");
                    break;
                }
            case DeployableTrapKind.Dart:
                {
                    Effects.SendMovingEffect(this, trigger, 0x1BFE, 7, 1, false, false);
                    Effects.PlaySound(GetWorldLocation(), Map, 0x223);
                    Damage(trigger, Roll("damageMin", "damageMax", ambush, trigger), 100, 0);

                    if (DartPoison != null && DartPoisonCharges > 0)
                    {
                        trigger.ApplyPoison(owner, DartPoison);
                        DartPoisonCharges--;
                    }

                    Darts = Math.Max(0, Darts - 1);
                    Emote(trigger, "*struck*");
                    owner.SendMessage("Your dart trap fires.");
                    break;
                }
            case DeployableTrapKind.BearTrap:
                {
                    var hold = DeployableTraps.HoldSeconds(Kind, SetterSkill);
                    ItemID = DeployableTraps.SpentArt(Kind); // the jaws shut on it
                    Hold(trigger, hold, () =>
                    {
                        if (!Deleted && Armed)
                        {
                            ItemID = DeployableTraps.SetArt(Kind); // and spring open again for the next charge
                        }
                    });
                    Damage(trigger, Roll("damageMin", "damageMax", ambush, trigger), 100, 0);
                    BleedAttack.BeginBleed(trigger, owner);
                    trigger.PlaySound(0x22C);
                    Emote(trigger, "*snap*");
                    owner.SendMessage($"Your bear trap has the {trigger.Name}.");
                    break;
                }
            case DeployableTrapKind.Alarm:
                {
                    var where = Region.Find(GetWorldLocation(), Map)?.Name;
                    owner.SendMessage(0x22, $"Your alarm at {(string.IsNullOrEmpty(where) ? "the wilds" : where)}, {X},{Y}: {trigger.Name}.");
                    owner.SendSound(0x495);
                    break;
                }
            case DeployableTrapKind.Shackle:
                {
                    foreach (var t in targets)
                    {
                        Hold(t, DeployableTraps.HoldSeconds(Kind, SetterSkill));
                        t.FixedEffect(0x376A, 10, 16);
                        Emote(t, "*bound*");
                    }

                    Effects.PlaySound(GetWorldLocation(), Map, 0x204);
                    owner.SendMessage($"Your shackle holds {targets.Count}.");
                    break;
                }
        }

        // A hit gains Remove Trap on the window above the set's (the owner is present: dormant traps never fire).
        var (gainMin, gainMax) = DeployableTraps.HitGainWindow(Kind);
        owner.CheckSkill(SkillName.RemoveTrap, gainMin, gainMax);

        if (--Charges <= 0)
        {
            Spend();
        }
    }

    private static void Emote(Mobile m, string text) => m.PublicOverheadMessage(MessageType.Emote, 0x3B2, false, text);

    /// <summary>The kit's damage roll, times the setter's scale, exceptional, the ambush, and half on a boss.</summary>
    private double Roll(string min, string max, bool ambush, Mobile t) =>
        Utility.RandomMinMax(DeployableTraps.Setting(Kind, min), DeployableTraps.Setting(Kind, max)) *
        DeployableTraps.DamageScale(SetterSkill) * (Exceptional ? 1.2 : 1.0) * (ambush ? 1.5 : 1.0) * (DeployableTraps.IsBoss(t) ? 0.5 : 1.0);

    // With clusterf.traps.pvp on, a player or a pet hit by the trap is the owner's harmful act (a thrown potion's flag).
    private void Harm(Mobile t)
    {
        if (t is PlayerMobile || t is BaseCreature { Controlled: true })
        {
            Owner!.DoHarmful(t);
        }
    }

    private void Damage(Mobile t, double amount, int phys, int fire)
    {
        Harm(t);
        AOS.Damage(t, Owner, Math.Max(1, (int)amount), phys, fire, 0, 0, 0);
    }

    // Holds use Frozen (as the spider web), which damage does not clear (Mobile.cs Frozen vs Paralyzed): the party can
    // beat on a held creature. Half on a boss (at Master) and on a paragon.
    private void Hold(Mobile t, double seconds, Action? after = null)
    {
        if (DeployableTraps.IsBoss(t))
        {
            seconds /= 2;
        }

        if (t is BaseCreature { IsParagon: true })
        {
            seconds /= 2;
        }

        t.Frozen = true;
        Timer.StartTimer(TimeSpan.FromSeconds(seconds), () =>
        {
            t.Frozen = false;
            after?.Invoke();
        });
    }

    private static readonly Dictionary<BaseCreature, double> _slowed = new();

    // Caltrops: the creature's active speed doubled (half as much on a paragon) for the slow, then restored. Runtime
    // only; a save in the window would keep the slow speed on that one creature.
    private static void Slow(Mobile m, int seconds)
    {
        if (m is not BaseCreature bc || _slowed.ContainsKey(bc))
        {
            return;
        }

        var active = bc.ActiveSpeed;
        _slowed[bc] = active;
        var slow = active * (bc.IsParagon ? 1.5 : 2.0);
        var wasActive = Math.Abs(bc.CurrentSpeed - active) < 0.0001;
        bc.ActiveSpeed = slow;

        if (wasActive)
        {
            bc.CurrentSpeed = slow;
        }

        Timer.StartTimer(TimeSpan.FromSeconds(seconds), () =>
        {
            if (_slowed.Remove(bc, out var restore) && !bc.Deleted)
            {
                var stillSlow = Math.Abs(bc.CurrentSpeed - bc.ActiveSpeed) < 0.0001;
                bc.ActiveSpeed = restore;

                if (stillSlow)
                {
                    bc.CurrentSpeed = restore;
                }
            }
        });
    }

    public static bool IsSlowed(Mobile m) => m is BaseCreature bc && _slowed.ContainsKey(bc);

    // -- Seeing, re-setting, disarming ------------------------------------------------------------------------------

    public bool CheckWhenHidden => true;

    public bool CheckReveal(Mobile m) =>
        m != Owner && m.Skills.DetectHidden.Value + Utility.Random(21) - 10 >= Concealment + Utility.Random(21) - 10;

    public bool CheckPassiveDetect(Mobile m) => false;

    public void OnRevealed(Mobile m) => Reveal();

    /// <summary>Shows the trap to everyone for 10 seconds (pinned's reveal window, DetectHidden.cs:234-252).</summary>
    public void Reveal()
    {
        Visible = true;
        Timer.StartTimer(TimeSpan.FromSeconds(10), () =>
        {
            if (!Deleted)
            {
                Visible = false;
            }
        });
    }

    /// <summary>The owner's hand re-set of a spent trap (a gas, blast or dart trap takes a fresh payload dropped on it).</summary>
    public override void OnDoubleClick(Mobile from)
    {
        if (from != Owner)
        {
            return;
        }

        if (!from.InRange(GetWorldLocation(), DeployableTraps.SetRange))
        {
            from.SendLocalizedMessage(500446); // That is too far away.
            return;
        }

        if (Armed)
        {
            from.SendMessage($"Your {DeployableTraps.NameOf(Kind)} is armed. Remove Trap picks it up.");
            return;
        }

        ReSet(from);
    }

    /// <summary>Re-arms a spent trap: its charges back and a new set-gain roll. False when it still needs its payload.</summary>
    public bool ReSet(Mobile from)
    {
        if (Armed || from != Owner)
        {
            return false;
        }

        if (!DeployableTrapPayloads.IsLoaded(this))
        {
            from.SendMessage(Kind switch
            {
                DeployableTrapKind.Gas   => "It needs a fresh poison potion. Drop one on it.",
                DeployableTrapKind.Blast => "It needs a fresh explosion potion. Drop one on it.",
                _                        => "It needs darts. Drop fukiya darts on it."
            });
            return false;
        }

        ResetCharges();

        var (min, max) = DeployableTraps.SetGainWindow(Kind);
        from.CheckSkill(SkillName.RemoveTrap, min, max);
        from.SendMessage($"You re-set the {DeployableTraps.NameOf(Kind)}.");
        return true;
    }

    public override bool OnDragDrop(Mobile from, Item dropped)
    {
        if (from != Owner || Armed || !DeployableTrapPayloads.TakesPayload(this, dropped) ||
            !from.InRange(GetWorldLocation(), DeployableTraps.SetRange))
        {
            return false;
        }

        if (DeployableTrapPayloads.Load(from, this, dropped))
        {
            ReSet(from);
        }

        return dropped.Deleted;
    }

    public void OnRemoveTrap(Mobile from)
    {
        var name = DeployableTraps.NameOf(Kind);
        var (min, max) = DeployableTraps.SetGainWindow(Kind);

        if (from == Owner)
        {
            from.CheckSkill(SkillName.RemoveTrap, min, max);

            if (Practice)
            {
                from.SendMessage("You pull up the practice net. It falls apart in your hands.");
            }
            else
            {
                from.AddToBackpack(ToKit());
                from.SendMessage($"You disarm your {name} and pack it away.");
            }

            Delete();
            return;
        }

        var grandmaster = ThievesDenStealRules.DenRank(from) >= ClusterFGuildSystem.RankGrandmaster;

        if (!from.CheckTargetSkill(SkillName.RemoveTrap, this, min, max) && !grandmaster)
        {
            from.SendMessage($"You fail to disarm the {name}.");
            return;
        }

        if (Practice)
        {
            from.SendMessage("You pull up someone's practice net. It falls apart in your hands.");
        }
        else
        {
            from.AddToBackpack(ToKit());
            from.SendMessage($"You disarm the {name} and salvage the kit.");
        }

        Owner?.SendMessage($"Someone has disarmed your {name}.");
        Delete();
    }

    /// <summary>The kit this trap packs back into: its quality, and what is left of its payload.</summary>
    public DeployableTrapKit ToKit()
    {
        DeployableTrapKit kit = Kind switch
        {
            DeployableTrapKind.SnareNet => new SnareNetKit(),
            DeployableTrapKind.Caltrops => new CaltropsKit(),
            DeployableTrapKind.Gas      => new GasTrapKit(),
            DeployableTrapKind.Blast    => new BlastTrapKit(),
            DeployableTrapKind.Dart     => new DartTrapKit(),
            DeployableTrapKind.BearTrap => new BearTrapKit(),
            DeployableTrapKind.Alarm    => new AlarmTrapKit(),
            _                           => new ShackleTrapKit()
        };

        kit.Exceptional = Exceptional;

        if (Armed || Kind == DeployableTrapKind.Dart)
        {
            DeployableTrapPayloads.CopyPayload(this, kit);
        }

        return kit;
    }

    public override void OnAfterDelete()
    {
        base.OnAfterDelete();
        DeployableTraps.Placed.Remove(this);
    }

    [AfterDeserialization]
    private void AfterDeserialization()
    {
        if (!Deleted)
        {
            DeployableTraps.Placed.Add(this);
        }
    }
}
