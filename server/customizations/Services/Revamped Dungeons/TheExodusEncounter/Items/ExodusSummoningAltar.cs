// ServUO: Services/Revamped Dungeons/TheExodusEncounter/Items/ExodusSummoningAltar.cs (cc-P195): the deed that builds
// the Summoning Tome. Used in a party, targeted at a shrine tile (0x149F to 0x14D6) on Trammel or Felucca: the tome
// rises at the shrine's fixed spot when no altar stands within five tiles, Ver Lor Reg's controller is running and
// Clockwork Exodus is whole enough (CheckExodus: at or above 75 percent, or at 60 with his low-water mark at 60), else
// 1075213. Lives a week. ServUO's class name (ExodusSummoningAlter) kept. ServUO's ExodusTomeAltar.Altar static is
// never assigned by ServUO, so its null test always passes; kept as ServUO runs it (notes, Q-3).

using System.Linq;
using ModernUO.Serialization;
using Server.Engines.Exodus;
using Server.Engines.PartySystem;
using Server.Mobiles;
using Server.Targeting;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class ExodusSummoningAlter : BaseDecayingItem
{
    /// <summary>ServUO: the tome's spot per shrine region name.</summary>
    public static readonly (string Region, Point3D Spot)[] ShrineSpots =
    {
        ("Shrine of Compassion", new Point3D(1858, 875, 12)),
        ("Shrine of Honesty", new Point3D(4209, 564, 60)),
        ("Shrine of Honor", new Point3D(1727, 3528, 15)),
        ("Shrine of Humility", new Point3D(4274, 3697, 12)),
        ("Shrine of Justice", new Point3D(1301, 634, 28)),
        ("Shrine of Sacrifice", new Point3D(3355, 290, 16)),
        ("Shrine of Spirituality", new Point3D(1606, 2490, 20)),
        ("Shrine of Valor", new Point3D(2492, 3931, 17))
    };

    [Constructible]
    public ExodusSummoningAlter() : base(0x14F0)
    {
        LootType = LootType.Regular;
        Weight = 1;
    }

    public override int LabelNumber => 1153502; // exodus summoning altar

    public override int Lifespan => 604800;
    public override bool UseSeconds => false;

    public override void OnDoubleClick(Mobile from)
    {
        if (!IsChildOf(from.Backpack))
        {
            from.SendLocalizedMessage(1054107); // This item must be in your backpack.
        }
        else if (Party.Get(from) == null)
        {
            from.SendLocalizedMessage(1153596); // You must join a party with the players you wish to perform the ritual with.
        }
        else
        {
            from.SendLocalizedMessage(1153675); // The Summoning Altar must be built upon a shrine, within Trammel or Felucca it matters not...
            from.Target = new SummoningTarget(this);
        }
    }

    public static bool IsValidTile(int itemID) => itemID is >= 0x149F and <= 0x14D6;

    public static Point3D SpotFor(Region region)
    {
        foreach (var (name, spot) in ShrineSpots)
        {
            if (region.IsPartOf(name))
            {
                return spot;
            }
        }

        return Point3D.Zero;
    }

    /// <summary>ServUO CheckExodus: Clockwork Exodus stands in Ver Lor Reg at 75 percent, or at 60 and never lower this fight.</summary>
    public static bool CheckExodus() =>
        ClockworkExodus.Instances.Any(
            m => !m.Deleted && m.Alive && m.Region.IsPartOf(ExodusRegions.Name) &&
                 (m.Hits >= m.HitsMax * 0.60 && m.MinHits >= m.HitsMax * 0.60 || m.Hits >= m.HitsMax * 0.75)
        );

    /// <summary>ServUO SummoningTarget.OnTarget: a static tile is the way in. Returns the tome it built, or null.</summary>
    public ExodusTomeAltar Build(Mobile from, object targeted)
    {
        if (targeted is StaticTarget targ)
        {
            return Build(from, targ.Location, targ.ItemID);
        }

        from.SendLocalizedMessage(500269); // You cannot build that there.
        return null;
    }

    /// <summary>The target's checks on a tile given by hand (the facts: the test host has no tile data, D27).</summary>
    public ExodusTomeAltar Build(Mobile from, Point3D tile, int tileItemID)
    {
        if (!IsValidTile(tileItemID) || from.Map != Map.Felucca && from.Map != Map.Trammel)
        {
            from.SendLocalizedMessage(500269); // You cannot build that there.
            return null;
        }

        var altarNearby = false;

        foreach (var _ in from.Map.GetItemsInRange<ExodusTomeAltar>(tile, 5))
        {
            altarNearby = true;
            break;
        }

        if (altarNearby)
        {
            from.SendLocalizedMessage(1153590); // An altar has already been built here.
            return null;
        }

        if (ExodusTomeAltar.Altar != null || !VerLorRegController.Active || VerLorRegController.CurrentExodus == null || !CheckExodus())
        {
            // The master of this realm has already been summoned and is engaged in combat. Your opportunity will come
            // after he has squashed the current batch of intruders!
            from.SendLocalizedMessage(1075213);
            return null;
        }

        var p = SpotFor(from.Region);

        if (p == Point3D.Zero)
        {
            from.SendLocalizedMessage(500269); // You cannot build that there.
            return null;
        }

        var altar = new ExodusTomeAltar(from);
        altar.MoveToWorld(p, from.Map);
        altar.Owner = from;
        Delete();

        return altar;
    }

    private class SummoningTarget : Target
    {
        private readonly ExodusSummoningAlter _deed;

        public SummoningTarget(ExodusSummoningAlter deed) : base(2, true, TargetFlags.None) => _deed = deed;

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (!_deed.Deleted)
            {
                _deed.Build(from, targeted);
            }
        }
    }
}
