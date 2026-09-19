// ServUO: Items/Quest/WispOrb.cs (CC4 Despise). The two enums and the orb, as ServUO keeps them.
//
// The orb is handed out by a DespiseAnkh to a player whose karma matches its alignment. Double-click
// targets a DespiseCreature to possess it; the possessed creature follows an anchor (a mobile, an item
// or the player) on a short or long leash and fights defensively or aggressively, and it can be
// conscripted for the boss encounter once its power is 4 or more.
//
// Conversion notes:
//   Anchor is an IEntity; the generator serializes IEntity fields natively (EffectController.cs:39), so
//     ServUO's hand-written 0/1/2 type tag is not needed.
//   Context menu entries take (from, target) in OnClick and have an Enabled flag instead of CMEFlags.Disabled.
//   RootParentEntity is RootParent; Delete() becomes OnDelete(); ObjectPropertyList is IPropertyList.
//   CheckDrop is kept but has no caller: ServUO wires it into Container.DropToWorld (Container.cs:254)
//     so an orb inside a bag vanishes when the bag is dropped. That is an upstream hook not taken (D-31).

using System;
using System.Collections.Generic;
using System.Linq;
using ModernUO.Serialization;
using Server.Collections;
using Server.ContextMenus;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Targeting;

namespace Server.Engines.Despise;

public enum LeashLength
{
    // Tight,
    Short,
    Long
}

public enum Aggression
{
    // Following,
    Defensive,
    Aggressive
}

[SerializationGenerator(0, false)]
public partial class WispOrb : Item
{
    private static readonly int MinPowerToConscript = 4;

    private static readonly List<WispOrb> _orbs = new();

    public static List<WispOrb> Orbs => _orbs;

    [SerializableField(0, setter: "private")]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private Mobile _owner;

    // _pet, _leashLength, _aggression, _alignment, _anchor and _conscripted are declared by the
    // serialization generator from the [SerializableProperty] members below.

    public WispOrb(Mobile owner, Alignment alignment) : base(8448)
    {
        _owner = owner;
        LootType = LootType.Blessed;
        _alignment = alignment;

        _orbs.Add(this);
        InvalidateHue();
    }

    public override int LabelNumber => 1153273; // A Wisp Orb

    [SerializableProperty(1)]
    [CommandProperty(AccessLevel.GameMaster)]
    public DespiseCreature Pet
    {
        get => _pet;
        set
        {
            if (_pet != null && value == null)
            {
                _pet.Unlink();
            }
            else
            {
                _pet = value;

                _pet?.Link(this);
            }

            InvalidateHue();
            InvalidateProperties();
            this.MarkDirty();
        }
    }

    [SerializableProperty(2)]
    [CommandProperty(AccessLevel.GameMaster)]
    public LeashLength LeashLength
    {
        get => _leashLength;
        set
        {
            _leashLength = value;

            InvalidateHue();
            InvalidateProperties();
            this.MarkDirty();
        }
    }

    [SerializableProperty(3)]
    [CommandProperty(AccessLevel.GameMaster)]
    public Aggression Aggression
    {
        get => _aggression;
        set
        {
            _aggression = value;

            InvalidateHue();
            InvalidateProperties();
            this.MarkDirty();
        }
    }

    [SerializableProperty(4)]
    [CommandProperty(AccessLevel.GameMaster)]
    public Alignment Alignment
    {
        get => _alignment;
        set
        {
            _alignment = value;
            InvalidateProperties();
            this.MarkDirty();
        }
    }

    [SerializableProperty(5)]
    [CommandProperty(AccessLevel.GameMaster)]
    public IEntity Anchor
    {
        get => _anchor;
        set
        {
            _anchor = value;

            if (_pet != null && _anchor == null)
            {
                _anchor = _owner;
                _pet.Home = GetAnchorLocation();
            }

            InvalidateProperties();
            this.MarkDirty();
        }
    }

    [SerializableProperty(6)]
    [CommandProperty(AccessLevel.GameMaster)]
    public bool Conscripted
    {
        get => _conscripted;
        set
        {
            _conscripted = value;

            if (_conscripted && DespiseController.Instance?.Sequencing == true)
            {
                DespiseController.Instance.TryAddToArmy(this);
            }

            this.MarkDirty();
        }
    }

    [AfterDeserialization]
    private void AfterDeserialization()
    {
        if (_anchor == null && _pet != null)
        {
            Anchor = _owner;
        }

        _orbs.Add(this);
    }

    public void OnUnlinkPet()
    {
        _pet = null;
        _anchor = null;
        _aggression = Aggression.Aggressive;
        InvalidateProperties();
        this.MarkDirty();
    }

    public bool CheckOwnerAlignment()
    {
        if (_owner == null || _owner.Karma > 0 && _alignment != Alignment.Good ||
            _owner.Karma < 0 && _alignment != Alignment.Evil)
        {
            _owner?.SendLocalizedMessage(1153313); // You are no longer aligned with your Wisp Orb. It dissolves into aether!

            Delete();
            return false;
        }

        return true;
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (CheckOwnerAlignment() && IsChildOf(from.Backpack) && from == _owner)
        {
            var cliloc = _pet == null ? 1153274 : 1153277;
            from.SendLocalizedMessage(cliloc); // Target a creature to possess. / Target an object or creature to set the anchor. Target the Wisp Orb to change the leash setting. Target the possessed creature to change its aggression.
            from.Target = new InternalTarget(this);
        }
    }

    public override void GetContextMenuEntries(Mobile from, ref PooledRefList<ContextMenuEntry> list)
    {
        base.GetContextMenuEntries(from, ref list);

        list.Add(new ReleaseEntry(this));
        list.Add(new ConscriptEntry(this));
    }

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);

        list.Add(1153329, $"#{GetAlignment()}"); // Alignment: ~1_VAL~
        list.Add(1153306, $"{GetArmyPower()}"); // Army Power: ~1_VAL~
        list.Add(1153272, _pet != null ? _pet.Name : "None"); // Controlling: ~1_VAL~

        var name = GetAnchorName();

        if (name is int number) // Anchor: ~1_NAME~
        {
            list.Add(1153265, $"#{number}");
        }
        else if (name is string text)
        {
            list.Add(1153265, text);
        }

        var leash = 1153262 + (int)_leashLength;
        var aggr = 1153269 + (int)_aggression;

        list.Add(1153260, $"#{leash}"); // Leash: ~1_VAL~
        list.Add(1153267, $"#{aggr}"); // Aggression: ~1_VAL~
    }

    public override bool DropToWorld(Mobile from, Point3D p)
    {
        from.SendLocalizedMessage(1153233); // The Wisp Orb vanishes to whence it came...
        Delete();
        return false;
    }

    public static void CheckDrop(Container c, Mobile m)
    {
        var list = new List<WispOrb>(c.Items.OfType<WispOrb>());

        foreach (var orb in list)
        {
            m.SendLocalizedMessage(1153233); // The Wisp Orb vanishes to whence it came...
            orb.Delete();
        }
    }

    public override bool OnDroppedInto(Mobile from, Container target, Point3D p)
    {
        if (target.RootParent == from)
        {
            return base.OnDroppedInto(from, target, p);
        }

        from.SendLocalizedMessage(1153233); // The Wisp Orb vanishes to whence it came...
        Delete();
        return false;
    }

    public override bool OnDroppedOnto(Mobile from, Item target)
    {
        if (target is Container && target.RootParent != from)
        {
            from.SendLocalizedMessage(1153233); // The Wisp Orb vanishes to whence it came...
            Delete();
            return false;
        }

        return base.OnDroppedOnto(from, target);
    }

    public Point3D GetAnchorLocation()
    {
        if (_pet == null)
        {
            return Point3D.Zero;
        }

        _anchor ??= _pet.ControlMaster;

        if (_anchor is Item item)
        {
            if (item.HeldBy != null)
            {
                return item.HeldBy.Location;
            }

            return item.GetWorldLocation();
        }

        return _anchor.Location;
    }

    public IPoint3D GetAnchorActual()
    {
        if (_pet == null)
        {
            return null;
        }

        _anchor ??= _pet.ControlMaster;

        if (_anchor is Item { RootParent: not null } item)
        {
            return item.RootParent;
        }

        return _anchor;
    }

    private object GetAnchorName()
    {
        if (_anchor == null)
        {
            return "None";
        }

        if (_anchor is Mobile mobile)
        {
            return mobile.Name;
        }

        if (_anchor is Item item)
        {
            if (item.Name != null)
            {
                return item.Name;
            }

            return item.LabelNumber;
        }

        if (_anchor is StaticTarget st)
        {
            return $"{st.Name} {st.Location}";
        }

        if (_anchor is LandTarget lt)
        {
            return $"{lt.Name} {lt.Location}";
        }

        return new Point3D(_anchor).ToString();
    }

    public void TrySetAnchor(Mobile from, IPoint3D p)
    {
        if (!CheckOwnerAlignment() || from != _owner)
        {
            return;
        }

        if (p is Mobile m)
        {
            Anchor = m;
            from.SendLocalizedMessage(1153280, m == _owner ? "You!" : m.Name + "."); // Your possessed creature is now anchored to ~1_NAME~

            _pet.ControlTarget = m;
            _pet.ControlOrder = OrderType.Follow;
        }

        if (p is Item item)
        {
            Anchor = item;

            var name = GetAnchorName(); // Your possessed creature is now anchored to ~1_NAME~

            if (name is int number)
            {
                from.SendLocalizedMessage(1153280, $"#{number}");
            }
            else if (name is string text)
            {
                from.SendLocalizedMessage(1153280, text);
            }

            _pet.ControlTarget = _pet.ControlMaster;
            _pet.ControlOrder = OrderType.Follow;
        }
    }

    private int GetAlignment() =>
        _alignment switch
        {
            Alignment.Good => 1153330,
            Alignment.Evil => 1153331,
            _              => -1
        };

    public void InvalidateHue()
    {
        if (_pet == null)
        {
            Hue = 1910; // shadow wisp color
        }
        else if (_pet.Combatant != null)
        {
            Hue = 1931; // Orange
        }
        else if (IsFollowing())
        {
            Hue = 1912;
        }
        else
        {
            switch (_aggression)
            {
                // case Aggression.Following: Hue = 1912; break; // Yellow
                case Aggression.Defensive:
                    Hue = 1917; // blue
                    break;
                case Aggression.Aggressive:
                    Hue = 1914; // green
                    break;
            }
        }
    }

    public bool IsFollowing() =>
        (int)_pet.GetDistanceToSqrt(GetAnchorLocation()) > _pet.GetLeashLength() + 1 &&
        _pet.ControlOrder == OrderType.Follow;

    public override void OnDelete()
    {
        _orbs.Remove(this);

        if (_pet?.Alive == true)
        {
            _pet.Unlink(false);
        }

        base.OnDelete();
    }

    public int GetArmyPower()
    {
        if (_pet == null)
        {
            return 0;
        }

        var power = _pet.Power;
        return power * power;
    }

    public static void TeleportPet(Mobile owner)
    {
        if (owner?.Backpack == null)
        {
            return;
        }

        var pet = owner.Backpack.FindItemByType<WispOrb>()?.Pet;

        pet?.MoveToWorld(owner.Location, owner.Map);
    }

    private class ConscriptEntry : ContextMenuEntry
    {
        public ConscriptEntry(WispOrb orb) : base(1153285) // Conscript
        {
            Enabled = orb.Pet != null && !orb.Conscripted && orb.Pet.Alignment == orb.Alignment;
        }

        public override void OnClick(Mobile from, IEntity target)
        {
            if (target is not WispOrb orb || orb.Deleted)
            {
                return;
            }

            if (orb.Pet != null && orb.IsChildOf(from.Backpack) && !orb.Conscripted && orb.Pet.Alignment == orb.Alignment)
            {
                if (orb.Pet.Power < MinPowerToConscript)
                {
                    from.SendLocalizedMessage(1153311); // The creature under control of your Wisp Orb cannot be conscripted at this time.
                }
                else
                {
                    from.SendLocalizedMessage(1153310); // The creature you are controlling will now fight with you when the Call to Arms sounds. If you do not wish this, then release control of it.
                    orb.Conscripted = true;
                }
            }
        }
    }

    private class ReleaseEntry : ContextMenuEntry
    {
        public ReleaseEntry(WispOrb orb) : base(1153284) // Release
        {
            Enabled = orb.Pet != null;
        }

        public override void OnClick(Mobile from, IEntity target)
        {
            if (target is WispOrb { Deleted: false, Pet: not null } orb)
            {
                orb.Pet.Unlink();
            }
        }
    }

    private class InternalTarget : Target
    {
        private readonly WispOrb _orb;

        public InternalTarget(WispOrb orb) : base(8, true, TargetFlags.None) => _orb = orb;

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (targeted is BaseCreature bc)
            {
                var creature = targeted as DespiseCreature;

                if (creature == null)
                {
                    from.SendLocalizedMessage(1153286); // That cannot be possessed by a Wisp Orb.
                }
                else if (_orb.Pet == null)
                {
                    if (bc.Controlled)
                    {
                        from.SendLocalizedMessage(1153287); // That creature is already under the control of a Wisp Orb.
                    }
                    else if (creature.Power > 5)
                    {
                        from.SendLocalizedMessage(1153336); // That creature is too powerful for you to coerce.
                    }
                    else
                    {
                        _orb.Anchor = from;

                        _orb.Pet = creature;
                        creature.Link(_orb);

                        _orb.Pet.SetControlMaster(from);
                        _orb.Pet.ControlTarget = from;
                        _orb.Pet.ControlOrder = OrderType.Follow;

                        from.SendLocalizedMessage(1153276); // Your Wisp Orb takes control of the creature!
                        _orb.Pet.PublicOverheadMessage(MessageType.Regular, 0x3B2, 1153295, from.Name); // * This creature is now under the control of ~1_NAME~ *
                    }
                }
                else if (targeted == _orb.Pet)
                {
                    var aggr = (int)_orb.Aggression + 1;

                    if (aggr >= 2)
                    {
                        aggr = 0;
                    }

                    _orb.Aggression = (Aggression)aggr;

                    from.SendLocalizedMessage(1153279, _orb.Aggression.ToString()); // Your possessed creature's aggression level is now: ~1_VAL~
                }
                else
                {
                    _orb.TrySetAnchor(from, bc);
                }
            }
            else if (targeted == _orb)
            {
                var length = (int)_orb.LeashLength + 1;

                if (length >= 2)
                {
                    length = 0;
                }

                _orb.LeashLength = (LeashLength)length;

                from.SendLocalizedMessage(1153278, _orb.LeashLength.ToString()); // Your possessed creature's leash is now: ~1_VAL~
            }
            else if (targeted is IPoint3D p && _orb.Pet != null)
            {
                _orb.TrySetAnchor(from, p);
            }
        }
    }
}
