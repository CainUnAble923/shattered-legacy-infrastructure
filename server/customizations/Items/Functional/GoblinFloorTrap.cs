// cc-P199: ServUO pub57 Scripts/Items/Functional/GoblinFloorTrap.cs. A player-set floor trap and the kit that sets it.
//
// GoblinFloorTrapKit (art 16704) is Tobin's reward for the "Done in the Name of Tinkering" quest (ServUO
// Mobiles/NPCs/Tobin.cs:43). From the pack, with 80 Tinkering, not mounted or flying and not in an active guard zone, it
// gives a target; a spot within two tiles gets an invisible GoblinFloorTrap owned by the setter and the kit is used up.
//
// GoblinFloorTrap (art 0x4004, "Armed Floor Trap", hidden, pinned BaseTrap): stepping on it, or passing within a tile
// (checked at most once a second), hurts a living player who is not staff and whom the owner may harm, other than the
// owner or the owner's guild: sound 0x22B, "You stepped onto a goblin trap!", 50-75 physical damage 0.3 seconds later,
// the victim flagged against the owner, the trap shown with "[Trapped]" over it and blood on the floor, hidden again 10
// seconds later. An unowned trap (built by [add) hurts any qualifying player.
//
// Differences from ServUO:
// - The kit's target re-checks skill, mount and guard zone and then places the trap anyway (GoblinFloorTrap.cs:170-184:
//   the three checks are followed by `if`, not `else if`). Here a failed check stops the placement, which is plainly
//   what the messages mean. Double-click already refuses all three, so this matters only if the setter mounts, loses
//   skill or walks into town while the target is up.
// - ServUO's IRevealableItem (Skills/DetectHidden.cs:16) does not exist in pinned, and pinned's Detect Hidden
//   (Skills/DetectHidden.cs) reveals no items, so the trap's CheckReveal (Detect Hidden 50-100 roll), OnRevealed (show it
//   for 10 seconds) and CheckPassiveDetect are kept as public methods with no caller. Until a Detect Hidden hook exists
//   the trap can only be found by walking on it. CheckPassiveDetect is kept as ServUO wrote it, which can never return
//   true: it needs the trap visible (:79) and its only caller asks only about hidden items (DetectHidden.cs:178).
// - CheckPassiveDetect's MessageLocalized packet (:83-87) is pinned's NetState.SendMessageLocalized, same arguments.

using System;
using ModernUO.Serialization;
using Server.Network;
using Server.Regions;
using Server.Spells;
using Server.Targeting;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class GoblinFloorTrap : BaseTrap
{
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private Mobile _owner;

    [Constructible]
    public GoblinFloorTrap(Mobile from = null) : base(0x4004)
    {
        _owner = from;
        Visible = false;
    }

    public override int LabelNumber => 1113296; // Armed Floor Trap

    public bool CheckWhenHidden => true;

    public override bool PassivelyTriggered => true;
    public override TimeSpan PassiveTriggerDelay => TimeSpan.FromSeconds(1.0);
    public override int PassiveTriggerRange => 1;
    public override TimeSpan ResetDelay => TimeSpan.FromSeconds(1.0);

    public override void OnTrigger(Mobile from)
    {
        if (from.AccessLevel > AccessLevel.Player || !from.Alive)
        {
            return;
        }

        if (_owner != null)
        {
            if (!_owner.CanBeHarmful(from) || _owner == from)
            {
                return;
            }

            if (_owner.Guild != null && _owner.Guild == from.Guild)
            {
                return;
            }
        }

        from.SendSound(0x22B);
        from.SendLocalizedMessage(1095157); // You stepped onto a goblin trap!

        SpellHelper.Damage(TimeSpan.FromSeconds(0.30), from, from, Utility.RandomMinMax(50, 75), 100, 0, 0, 0, 0);

        if (_owner != null)
        {
            from.DoHarmful(_owner);
        }

        Visible = true;
        Timer.StartTimer(TimeSpan.FromSeconds(10), Rehide_Callback);

        PublicOverheadMessage(MessageType.Regular, 0x65, 500813); // [Trapped]

        new Blood().MoveToWorld(from.Location, from.Map);
    }

    public virtual bool CheckReveal(Mobile m) => m.CheckTargetSkill(SkillName.DetectHidden, this, 50.0, 100.0);

    public virtual void OnRevealed(Mobile m)
    {
        Unhide();
    }

    public virtual bool CheckPassiveDetect(Mobile m)
    {
        if (Visible && Utility.RandomDouble() < 0.05 && m.NetState != null)
        {
            m.NetState.SendMessageLocalized(Serial, ItemID, MessageType.Regular, 0x65, 3, 500813, Name, ""); // [Trapped]
            return true;
        }

        return false;
    }

    public void Unhide()
    {
        Visible = true;

        Timer.StartTimer(TimeSpan.FromSeconds(10), Rehide_Callback);
    }

    public void Rehide_Callback()
    {
        Visible = false;
    }
}

[SerializationGenerator(0, false)]
public partial class GoblinFloorTrapKit : Item
{
    [Constructible]
    public GoblinFloorTrapKit() : base(16704)
    {
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (!IsChildOf(from.Backpack))
        {
            from.SendLocalizedMessage(1054107); // This item must be in your backpack.
        }
        else if (CheckCanSet(from, from.Region))
        {
            from.Target = new InternalTarget(this);
        }
    }

    // The three refusals ServUO repeats in OnDoubleClick (GoblinFloorTrap.cs:131-142) and in the target (:170-181).
    private static bool CheckCanSet(Mobile from, Region r)
    {
        if (from.Skills[SkillName.Tinkering].Value < 80)
        {
            from.SendLocalizedMessage(1113318); // You do not have enough skill to set the trap.
        }
        else if (from.Mounted || from.Flying)
        {
            from.SendLocalizedMessage(1113319); // You cannot set the trap while riding or flying.
        }
        else if (r is GuardedRegion guarded && !guarded.IsDisabled())
        {
            from.SendMessage("You cannot place a trap in a guard region.");
        }
        else
        {
            return true;
        }

        return false;
    }

    private class InternalTarget : Target
    {
        private readonly GoblinFloorTrapKit _kit;

        public InternalTarget(GoblinFloorTrapKit kit) : base(-1, false, TargetFlags.None) => _kit = kit;

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (targeted is not IPoint3D ip)
            {
                return;
            }

            var p = new Point3D(ip);

            if (!CheckCanSet(from, Region.Find(p, from.Map)))
            {
                return;
            }

            if (from.InRange(p, 2))
            {
                var trap = new GoblinFloorTrap(from);

                trap.MoveToWorld(p, from.Map);
                from.SendLocalizedMessage(1113294); // You carefully arm the goblin trap.
                from.SendLocalizedMessage(1113297); // You hide the trap to the best of your ability.

                _kit.Consume();
            }
            else
            {
                from.SendLocalizedMessage(500446); // That is too far away.
            }
        }
    }
}
