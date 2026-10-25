// ServUO: Services/Revamped Dungeons/Covetous Void Spawn/Items/CauldronOfTransmutation.cs (cc-P194).
//
// A Void Pool reward: a deed for a cauldron of one resource (bronze, gold, agapite, verite; ash, yew, heartwood,
// bloodwood) that lasts 24 hours in the pack; placed in a house it lasts 4 hours and turns three of its resource
// into one of the next tier (ingots or boards: Verite -> Valorite, Bloodwood -> Frostwood on the shard's
// ResourceInfo order, which keeps OSI's up to there), 5000 charges, co-owners may use it, the owner's resources
// only. Expired deeds and cauldrons vanish in smoke ("The ~1_name~ expired...").
//
// Conversion notes: the decay timers are Timer.DelayCall and are rebuilt after a load; BaseAddon and
// BaseAddonDeed carry Resource here as there; the deed cannot be re-deeded (ServUO's RetainDeedHue + the
// cannot-re-deed line).
//
// cc-P610 (P489 #4): the remaining charges are saved (version 1). ServUO saved them too; the port had left them out,
// so a placed cauldron came back from a save with none. A version 0 cauldron has no count to read and loads with
// none, as before: the count it had is not in the save, and a refill to 5000 would hand out charges never paid for.

using System;
using ModernUO.Serialization;
using Server.Multis;
using Server.Targeting;

namespace Server.Items;

[SerializationGenerator(1, false)]
public partial class CauldronOfTransmutation : BaseAddon
{
    public const int DecayPeriod = 4;

    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private DateTime _expires;

    private Timer _decayTimer;

    public CauldronOfTransmutation()
    {
        Charges = 5000;

        AddComponent(new InternalComponent(2421), 0, 0, 0);

        _expires = Core.Now + TimeSpan.FromHours(DecayPeriod);
        BeginTimer();
    }

    [SerializableProperty(1)]
    [CommandProperty(AccessLevel.GameMaster)]
    public int Charges
    {
        get => _charges;
        set
        {
            _charges = value;
            this.MarkDirty();

            if (_charges <= 0 && RootParent is Mobile m)
            {
                m.SendLocalizedMessage(1152635); // The cauldron's magic is exhausted
            }

            InvalidateProperties();
        }
    }

    public override bool RetainDeedHue => true;

    public void BeginTimer()
    {
        EndTimer();

        _decayTimer = Timer.DelayCall(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10), CheckDecay);
    }

    public void EndTimer()
    {
        _decayTimer?.Stop();
        _decayTimer = null;
    }

    public void CheckDecay()
    {
        if (Deleted)
        {
            EndTimer();
            return;
        }

        if (_expires < Core.Now)
        {
            Decay();
        }
        else
        {
            InvalidateProperties();
        }
    }

    public void Decay()
    {
        VoidPoolDecay.Announce(this);
        Delete();
    }

    public override void OnDelete()
    {
        EndTimer();
        base.OnDelete();
    }

    public override void OnComponentUsed(AddonComponent c, Mobile from)
    {
        if (from.InRange(c.Location, 3))
        {
            var house = BaseHouse.FindHouseAt(from);

            if (house != null && house.IsCoOwner(from))
            {
                from.Target = new InternalTarget(this);
                from.SendLocalizedMessage(1152738); // Target ingots in your backpack to transmute them.
            }
        }
    }

    public bool TryTransmutate(Mobile from, Item dropped)
    {
        var house = BaseHouse.FindHouseAt(from);

        if (house == null || !house.IsOwner(from))
        {
            from.SendLocalizedMessage(1152632); // That is not yours!
            return false;
        }

        var res = CraftResources.GetFromType(dropped.GetType());

        if (res != Resource)
        {
            return false;
        }

        if (dropped.Amount < 3)
        {
            from.SendLocalizedMessage(1152634); // There is not enough to transmute
            return false;
        }

        if (Charges <= 0)
        {
            from.SendLocalizedMessage(1152635); // The cauldron's magic is exhausted
            return false;
        }

        var info = CraftResources.GetInfo(Resource + 1);

        if (info == null || info.ResourceTypes.Length == 0)
        {
            from.SendLocalizedMessage(1152633); // The cauldron cannot transmute that
            return false;
        }

        var toDrop = Math.Min(Charges * 3, dropped.Amount);

        while (toDrop % 3 != 0)
        {
            toDrop--;
        }

        var newAmount = toDrop / 3;

        if (toDrop < dropped.Amount)
        {
            dropped.Amount -= toDrop;
        }
        else
        {
            dropped.Delete();
        }

        var item = Loot.Construct(info.ResourceTypes[0]);

        if (item == null)
        {
            return false;
        }

        item.Amount = newAmount;
        from.AddToBackpack(item);

        from.SendLocalizedMessage(1152636); // The cauldron transmutes the material

        from.PlaySound(Utility.RandomList(0x22, 0x23));

        Charges -= newAmount;

        foreach (var component in Components)
        {
            component.InvalidateProperties();
        }

        return true;
    }

    private void MigrateFrom(V0Content content)
    {
        _expires = content.Expires;
    }

    [AfterDeserialization]
    private void AfterDeserialization()
    {
        if (_expires < Core.Now)
        {
            Timer.DelayCall(Decay);
        }
        else
        {
            BeginTimer();
        }
    }

    private class InternalTarget : Target
    {
        public InternalTarget(CauldronOfTransmutation addon) : base(-1, false, TargetFlags.None) => Addon = addon;

        public CauldronOfTransmutation Addon { get; }

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (Addon == null || Addon.Deleted || !from.InRange(Addon.Location, 3))
            {
                from.SendLocalizedMessage(500295); // You are too far away to do that.
                return;
            }

            if (targeted is Item item)
            {
                Addon.TryTransmutate(from, item);
            }
        }
    }

    [SerializationGenerator(0, false)]
    private partial class InternalComponent : AddonComponent
    {
        public InternalComponent(int id) : base(id)
        {
        }

        public override bool ForceShowProperties => true;

        public override void AddNameProperty(IPropertyList list)
        {
            if (Addon != null && Addon.Resource != CraftResource.None)
            {
                list.Add(1152600, $"#{CraftResources.GetLocalizationNumber(Addon.Resource)}"); // ~1_RES~ Cauldron of Transmutation
            }
            else
            {
                base.AddNameProperty(list);
            }
        }

        public override void GetProperties(IPropertyList list)
        {
            base.GetProperties(list);

            if (Addon is not CauldronOfTransmutation cauldron || cauldron.Resource == CraftResource.None)
            {
                return;
            }

            var left = 0;

            if (Core.Now < cauldron.Expires)
            {
                left = (int)(cauldron.Expires - Core.Now).TotalSeconds;
            }

            list.Add(1072517, left.ToString()); // Lifespan: ~1_val~ seconds

            var res = cauldron.Resource;
            var res2 = res + 1;

            // transmutes ~1_SOURCE~ to ~2_DEST~
            list.Add(1152630, $"#{CraftResources.GetLocalizationNumber(res)}\t#{CraftResources.GetLocalizationNumber(res2)}");
            list.Add(1152631, "3\t1"); // ratio ~1_INPUT~ to ~2_OUTPUT~
            list.Add(1060584, cauldron.Charges.ToString()); // uses remaining: ~1_val~
        }
    }
}

[SerializationGenerator(0, false)]
public partial class CauldronOfTransmutationDeed : BaseAddonDeed
{
    public const int DecayPeriod = 24;

    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private DateTime _expires;

    // Pinned BaseAddonDeed does not save its Resource (Items/Addons/BaseAddonDeed.cs:12); this deed does.
    [SerializableField(1)]
    private CraftResource _cauldronResource;

    private Timer _timer;

    [Constructible]
    public CauldronOfTransmutationDeed(CraftResource resource)
    {
        Resource = resource;
        _cauldronResource = resource;

        _expires = Core.Now + TimeSpan.FromHours(DecayPeriod);
        _timer = Timer.DelayCall(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10), CheckDecay);
    }

    public override BaseAddon Addon => new CauldronOfTransmutation();

    public void CheckDecay()
    {
        if (Deleted)
        {
            _timer?.Stop();
            _timer = null;
            return;
        }

        if (_expires < Core.Now)
        {
            Decay();
        }
        else
        {
            InvalidateProperties();
        }
    }

    public void Decay()
    {
        VoidPoolDecay.Announce(this);
        Delete();
    }

    public override void OnDelete()
    {
        _timer?.Stop();
        _timer = null;

        base.OnDelete();
    }

    public override void AddNameProperty(IPropertyList list)
    {
        list.Add(1152600, $"#{CraftResources.GetLocalizationNumber(Resource)}"); // ~1_RES~ Cauldron of Transmutation
    }

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);

        var left = 0;

        if (Core.Now < _expires)
        {
            left = (int)(_expires - Core.Now).TotalSeconds;
        }

        list.Add(1072517, left.ToString()); // Lifespan: ~1_val~ seconds
        list.Add(1152628); // cannot re-deed
        list.Add(1152629, CauldronOfTransmutation.DecayPeriod.ToString()); // lasts ~1_count~ hours
    }

    [AfterDeserialization]
    private void AfterDeserialization()
    {
        Resource = _cauldronResource;

        if (_expires < Core.Now)
        {
            Timer.DelayCall(Decay);
        }
        else
        {
            _timer = Timer.DelayCall(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10), CheckDecay);
        }
    }
}

// ServUO repeats this block in every decaying Void Pool reward: "The ~1_name~ expired..." to the holder, or smoke
// and a sound where the thing stood.
public static class VoidPoolDecay
{
    public static void Announce(Item item)
    {
        if (item.RootParent is Mobile parent)
        {
            if (item.Name == null)
            {
                parent.SendLocalizedMessage(1072515, $"#{item.LabelNumber}"); // The ~1_name~ expired...
            }
            else
            {
                parent.SendLocalizedMessage(1072515, item.Name); // The ~1_name~ expired...
            }

            Effects.SendLocationParticles(EffectItem.Create(parent.Location, parent.Map, EffectItem.DefaultDuration), 0x3728, 8, 20, 5042);
            Effects.PlaySound(parent.Location, parent.Map, 0x201);
        }
        else if (item.Map != null && item.Map != Map.Internal)
        {
            Effects.SendLocationParticles(EffectItem.Create(item.Location, item.Map, EffectItem.DefaultDuration), 0x3728, 8, 20, 5042);
            Effects.PlaySound(item.Location, item.Map, 0x201);
        }
    }
}
