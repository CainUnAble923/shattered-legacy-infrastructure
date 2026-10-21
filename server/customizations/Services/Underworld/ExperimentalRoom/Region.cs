// ServUO: Services/Underworld/ExperimentalRoom/Region.cs (cc-P197). The Experimental Room's own region over its
// three colour rooms, the final room and the entrance hall: a controlled or summoned pet that steps into a colour
// room is thrown back to the entrance and its master told (1113472, "This is not a stable!"); a player in a colour
// room without an Experimental Gem in the pack (any gem, active or not; a pack-less player anywhere in the region)
// goes the same way, pets along (BaseCreature.TeleportPets), with smoke at both ends.
//
// ServUO declares the class and never registers it: Data/Regions.xml has no entry for it (checked: the only
// Underworld entry is the DungeonRegion over the whole dungeon), so on ServUO neither rule ever runs. The room
// itself relies on the blockers and doors for the gem rule, which cannot throw out a pet or a player whose gem was
// deleted inside; the book says both. Registered here at Initialize over 977,1062 23x59 (the three rooms at
// 977,1074-1113, the final room above them to y 1062 and the entrance hall to y 1120), the D-4 rule's broken kind
// (notes, Deviations). Priority is DefaultPriority + 1 so it wins over the Underworld's DungeonRegion at 50: pinned
// sorts a sector's regions by priority and a tie is unordered (Region.CompareTo, Region.cs:231). A DungeonRegion,
// as ServUO's.

using Server.Items;
using Server.Mobiles;

namespace Server.Regions;

public class ExperimentalRoomRegion : DungeonRegion
{
    public const string RegionName = "Experimental Room";

    public static readonly Rectangle2D Bounds = new(977, 1062, 23, 59);

    private static readonly Rectangle2D _entrance = new(994, 1114, 4, 4);

    public static readonly Rectangle2D[] RoomRecs =
    {
        new(977, 1104, 15, 10), // RoomOne
        new(977, 1092, 15, 9),  // RoomTwo
        new(977, 1074, 15, 10)  // RoomThree
    };

    public static ExperimentalRoomRegion Instance { get; private set; }

    public static void Initialize()
    {
        Register();
    }

    /// <summary>Registers the region (once); returns the registration.</summary>
    public static ExperimentalRoomRegion Register()
    {
        if (Instance?.Registered == true)
        {
            return Instance;
        }

        Instance = new ExperimentalRoomRegion();
        Instance.Register();
        return Instance;
    }

    /// <summary>Test support.</summary>
    public static void Unregister()
    {
        if (Instance != null)
        {
            Instance.Unregister();
            Instance = null;
        }
    }

    public ExperimentalRoomRegion()
        : base(RegionName, Map.TerMur, DefaultPriority + 1, ConvertTo3D(Bounds))
    {
    }

    public override void OnLocationChanged(Mobile m, Point3D oldLocation)
    {
        base.OnLocationChanged(m, oldLocation);

        if (m is BaseCreature bc && (bc.Controlled || bc.Summoned))
        {
            foreach (var rec in RoomRecs)
            {
                if (rec.Contains(m.Location))
                {
                    KickToEntrance(m);
                    var master = bc.GetMaster();

                    if (master?.NetState != null)
                    {
                        master.SendLocalizedMessage(1113472); // Your pet has been kicked out of the room. This is not a stable!
                    }
                }
            }
        }
        else if (m is PlayerMobile && m.AccessLevel == AccessLevel.Player)
        {
            if (m.Backpack == null)
            {
                KickToEntrance(m);
            }
            else
            {
                var item = m.Backpack.FindItemByType(typeof(ExperimentalGem));

                if (item == null)
                {
                    foreach (var rec in RoomRecs)
                    {
                        if (rec.Contains(m.Location))
                        {
                            KickToEntrance(m);
                        }
                    }
                }
            }
        }
    }

    public void KickToEntrance(Mobile from)
    {
        if (from?.Map == null)
        {
            return;
        }

        var x = Utility.RandomMinMax(_entrance.X, _entrance.X + _entrance.Width);
        var y = Utility.RandomMinMax(_entrance.Y, _entrance.Y + _entrance.Height);
        var z = from.Map.GetAverageZ(x, y);

        var loc = from.Location;
        var p = new Point3D(x, y, z);

        if (from is PlayerMobile)
        {
            BaseCreature.TeleportPets(from, p, Map.TerMur);
        }

        from.MoveToWorld(p, Map.TerMur);

        Effects.SendLocationParticles(EffectItem.Create(loc, from.Map, EffectItem.DefaultDuration), 0x3728, 10, 10, 2023);
        Effects.SendLocationParticles(EffectItem.Create(p, from.Map, EffectItem.DefaultDuration), 0x3728, 10, 10, 5023);
    }
}
