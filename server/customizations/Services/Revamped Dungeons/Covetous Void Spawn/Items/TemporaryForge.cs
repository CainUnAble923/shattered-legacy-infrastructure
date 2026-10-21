// ServUO: Services/Revamped Dungeons/Covetous Void Spawn/Items/TemporaryForge.cs (cc-P194).
//
// A Void Pool reward (250 points): a deed that lasts 24 hours and builds a forge that lasts 4 hours. In a house it
// places as any addon deed; outside one, target a spot 2 to 5 tiles away in line of sight with no impassable
// tile beside it, and the forge is yours to chop down. Expired deeds and forges vanish in smoke.
//
// Conversion notes: ServUO's ValidateLocation asks TreasureMap.ValidateLocation (a buildable, dry land tile);
// pinned's TreasureMap has no such helper, so IsOpenGround checks the land tile and the items on it the same
// way (not impassable, not wet, nothing standing above ground level). The decay timers are rebuilt after a load.

using System;
using ModernUO.Serialization;
using Server.Multis;
using Server.Spells;
using Server.Targeting;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class TemporaryForge : BaseAddon
{
    public const int DecayPeriod = 4;

    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private DateTime _expires;

    [SerializableField(1)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private Mobile _owner;

    private Timer _timer;

    public TemporaryForge()
    {
        AddComponent(new InternalComponent(), 0, 0, 0);

        _expires = Core.Now + TimeSpan.FromHours(DecayPeriod);
        _timer = Timer.DelayCall(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10), CheckDecay);
    }

    public override void OnChop(Mobile from)
    {
        if (BaseHouse.FindHouseAt(this) == null && _owner == from)
        {
            Effects.PlaySound(GetWorldLocation(), Map, 0x3B3);
            from.SendLocalizedMessage(500461); // You destroy the item.
            Delete();
            return;
        }

        base.OnChop(from);
    }

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

    [AfterDeserialization]
    private void AfterDeserialization()
    {
        if (_expires < Core.Now)
        {
            Timer.DelayCall(Decay);
        }
        else
        {
            _timer = Timer.DelayCall(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10), CheckDecay);
        }
    }

    [SerializationGenerator(0, false)]
    private partial class InternalComponent : ForgeComponent
    {
        public InternalComponent() : base(0xFB1)
        {
        }

        public override bool ForceShowProperties => true;
        public override int LabelNumber => 1152601; // Temporary Forge

        public override void GetProperties(IPropertyList list)
        {
            base.GetProperties(list);

            if (Addon is not TemporaryForge forge)
            {
                return;
            }

            var left = 0;

            if (Core.Now < forge.Expires)
            {
                left = (int)(forge.Expires - Core.Now).TotalSeconds;
            }

            list.Add(1072517, left.ToString()); // Lifespan: ~1_val~ seconds
        }
    }
}

[SerializationGenerator(0, false)]
public partial class TemporaryForgeDeed : BaseAddonDeed
{
    public const int DecayPeriod = 24;

    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private DateTime _expires;

    private Timer _timer;

    [Constructible]
    public TemporaryForgeDeed()
    {
        _expires = Core.Now + TimeSpan.FromHours(DecayPeriod);
        _timer = Timer.DelayCall(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10), CheckDecay);
    }

    public override BaseAddon Addon => new TemporaryForge();

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
        list.Add(1152601); // Temporary Forge
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
        list.Add(1152627); // no house required
        list.Add(1152628); // cannot re-deed
        list.Add(1152629, TemporaryForge.DecayPeriod.ToString()); // lasts ~1_count~ hours
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (!IsChildOf(from.Backpack))
        {
            from.SendLocalizedMessage(1042001); // That must be in your pack for you to use it.
            return;
        }

        if (BaseHouse.FindHouseAt(from) != null)
        {
            base.OnDoubleClick(from);
            return;
        }

        from.Target = new BuildTarget(this);
    }

    public void TryBuild(Mobile from, object targeted)
    {
        if (Deleted || !IsChildOf(from.Backpack) || targeted is not IPoint3D target)
        {
            return;
        }

        var p = new Point3D(target);
        var dist = (int)from.GetDistanceToSqrt(p);

        if (dist < 2 || dist > 5)
        {
            from.SendLocalizedMessage(1152736); // You must stand between 2 and 5 tiles away from the targeted location to attempt to build this.
        }
        else if (!from.InLOS(p))
        {
            from.SendLocalizedMessage(500237); // Target cannot be seen.
        }
        else if (!ValidateLocation(p, from.Map))
        {
            from.SendLocalizedMessage(1152735); // The targeted location has at least one impassable tile adjacent to the structure.
        }
        else if (BaseHouse.FindHouseAt(from) != null)
        {
            from.SendLocalizedMessage(500269); // You cannot build that there.
        }
        else
        {
            IPoint3D point = target;
            SpellHelper.GetSurfaceTop(ref point);

            var addon = Addon;
            addon.MoveToWorld(new Point3D(point), from.Map);

            if (addon is TemporaryForge forge)
            {
                forge.Owner = from;
            }

            Delete();
        }
    }

    public static bool ValidateLocation(Point3D p, Map map)
    {
        if (map == null || map == Map.Internal || !IsOpenGround(p.X, p.Y, map))
        {
            return false;
        }

        for (var x = p.X - 1; x <= p.X + 1; x++)
        {
            for (var y = p.Y - 1; y <= p.Y + 1; y++)
            {
                if (!IsOpenGround(x, y, map))
                {
                    continue;
                }

                var z = map.GetAverageZ(x, y);

                foreach (var item in map.GetItemsInRange(new Point3D(x, y, z), 0))
                {
                    var id = item.ItemData;

                    if (item.Z + id.CalcHeight >= z)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        return false;
    }

    // ServUO TreasureMap.ValidateLocation: a dry land tile nothing impassable stands on.
    public static bool IsOpenGround(int x, int y, Map map)
    {
        var landTile = map.Tiles.GetLandTile(x, y);
        var landFlags = TileData.LandTable[landTile.ID & TileData.MaxLandValue].Flags;

        if ((landFlags & (TileFlag.Impassable | TileFlag.Wet)) != 0)
        {
            return false;
        }

        foreach (var tile in map.Tiles.GetStaticTiles(x, y))
        {
            var flags = TileData.ItemTable[tile.ID & TileData.MaxItemValue].Flags;

            if ((flags & (TileFlag.Impassable | TileFlag.Wet)) != 0)
            {
                return false;
            }
        }

        return true;
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
            _timer = Timer.DelayCall(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10), CheckDecay);
        }
    }

    private class BuildTarget : Target
    {
        private readonly TemporaryForgeDeed _deed;

        public BuildTarget(TemporaryForgeDeed deed) : base(10, true, TargetFlags.None) => _deed = deed;

        protected override void OnTarget(Mobile from, object targeted) => _deed.TryBuild(from, targeted);
    }
}
