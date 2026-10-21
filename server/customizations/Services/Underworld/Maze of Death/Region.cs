// ServUO: Services/Underworld/Maze of Death/Region.cs (cc-P197). The Maze of Death, two rectangles in the
// Underworld's west (1057,1028 16x40 and 1057,990 16x38, Ter Mur): a corridor of hidden traps with one safe path.
// Entering: two seconds' paralysis and the two warnings (1113580, 1113581), and the compass gump for a player
// carrying a Golden Compass. Each step: the compass gump redrawn (its arrows point along the safe path from where
// the player stands); the two hints as the player crosses x 1063 westward (1113582 with a compass, 1113581 without)
// and y 991 to 990 (1113585 with the gump open); and a living player on a trap tile (inside 1057,990 7x71 and off
// the path) springs one of four traps for 75-150: fire (0x3709, 0x54, 1010524), a blade (0x223, 1010525),
// lightning (BoltEffect, 1010526) or deadly poison and a toxic cloud (0x113A, 0x231, 1010523). A player who dies on
// a trap tile is sent back to the entrance (1057,1062 7x5) three seconds later, corpse along. Mark, Gate and
// Teleport refuse to cast inside (501802), and ServUO's travel table (SpellHelper.cs:820, m_Rules' Maze of Death
// column) allows only recalling OUT: CheckTravel here says the same, RecallFrom and nothing else, staff excepted.
//
// The safe path is ServUO's 136 fixed tiles plus 33 random tiles inside the trap bounds, drawn once at
// registration (ServUO: at Initialize). Spliced from ServUO's source by gen_maze.py (notes), not retyped.
//
// Shape: ServUO's class is a plain Region at DefaultPriority, which ties with the Underworld's own DungeonRegion
// (pinned sorts a sector's regions by priority and a tie is unordered, Region.cs:231), so it is a DungeonRegion
// here, at DefaultPriority + 1, keeping the dungeon's light level and no young protection inside the maze (notes,
// Deviations). ServUO's OnTarget override (refusing TeleportSpell.InternalTarget) is not ported: pinned's
// Teleport targets through ITargetingSpell with no nested target type to name, and OnBeginSpellCast already
// refuses the cast inside the maze; CheckTravel refuses a teleport INTO it. DateTime is Core.Now.

using System;
using System.Collections.Generic;
using Server.Gumps;
using Server.Items;
using Server.Network;
using Server.Spells;
using Server.Spells.Seventh;
using Server.Spells.Sixth;
using Server.Spells.Third;

namespace Server.Regions;

public class MazeOfDeathRegion : DungeonRegion
{
    public const string RegionName = "Maze of Death";
    public const int RandomPathTiles = 33;

    public static readonly Rectangle2D[] Bounds =
    {
        new(1057, 1028, 16, 40),
        new(1057, 990, 16, 38)
    };

    public static readonly Rectangle2D TrapBounds = new(1057, 990, 7, 71);

    private static readonly Rectangle2D _entrance = new(1057, 1062, 7, 5);

    public static readonly Point3D[] FixedPath =
    {
        new(1062, 1060, -42),
        new(1062, 1059, -42),
        new(1062, 1058, -42),
        new(1061, 1058, -42),
        new(1060, 1058, -42),
        new(1060, 1057, -42),
        new(1059, 1057, -42),
        new(1059, 1056, -42),
        new(1059, 1055, -42),
        new(1060, 1055, -42),
        new(1060, 1054, -42),
        new(1060, 1053, -42),
        new(1059, 1053, -42),
        new(1059, 1052, -42),
        new(1059, 1051, -42),
        new(1059, 1050, -42),
        new(1058, 1050, -42),
        new(1058, 1049, -42),
        new(1057, 1049, -42),
        new(1057, 1048, -42),
        new(1057, 1047, -42),
        new(1057, 1046, -42),
        new(1058, 1047, -42),
        new(1059, 1047, -42),
        new(1059, 1046, -42),
        new(1059, 1045, -42),
        new(1059, 1044, -42),
        new(1060, 1044, -42),
        new(1061, 1044, -42),
        new(1061, 1043, -42),
        new(1060, 1042, -42),
        new(1059, 1042, -42),
        new(1058, 1042, -42),
        new(1057, 1042, -42),
        new(1061, 1042, -42),
        new(1061, 1041, -42),
        new(1061, 1042, -42),
        new(1062, 1042, -42),
        new(1062, 1041, -42),
        new(1062, 1040, -42),
        new(1063, 1040, -42),
        new(1063, 1041, -42),
        new(1063, 1040, -42),
        new(1063, 1039, -42),
        new(1062, 1039, -42),
        new(1062, 1038, -42),
        new(1061, 1038, -42),
        new(1061, 1037, -42),
        new(1060, 1037, -42),
        new(1059, 1037, -42),
        new(1058, 1037, -42),
        new(1057, 1037, -42),
        new(1057, 1036, -42),
        new(1057, 1035, -42),
        new(1058, 1035, -42),
        new(1059, 1035, -42),
        new(1057, 1034, -42),
        new(1057, 1033, -42),
        new(1057, 1032, -42),
        new(1058, 1032, -42),
        new(1059, 1032, -42),
        new(1060, 1032, -42),
        new(1060, 1031, -42),
        new(1060, 1030, -42),
        new(1060, 1029, -42),
        new(1061, 1029, -42),
        new(1061, 1028, -42),
        new(1061, 1027, -42),
        new(1062, 1027, -42),
        new(1063, 1027, -42),
        new(1064, 1027, -42),
        new(1061, 1026, -42),
        new(1062, 1026, -42),
        new(1063, 1026, -42),
        new(1064, 1026, -42),
        new(1061, 1026, -42),
        new(1061, 1025, -42),
        new(1061, 1024, -42),
        new(1061, 1023, -42),
        new(1061, 1022, -42),
        new(1060, 1026, -42),
        new(1059, 1026, -42),
        new(1058, 1026, -42),
        new(1058, 1025, -42),
        new(1058, 1024, -42),
        new(1058, 1023, -42),
        new(1058, 1022, -42),
        new(1058, 1021, -42),
        new(1057, 1021, -42),
        new(1057, 1020, -42),
        new(1057, 1019, -42),
        new(1057, 1018, -42),
        new(1058, 1018, -42),
        new(1059, 1018, -42),
        new(1060, 1018, -42),
        new(1061, 1018, -42),
        new(1061, 1017, -42),
        new(1061, 1016, -42),
        new(1061, 1015, -42),
        new(1061, 1014, -42),
        new(1061, 1013, -42),
        new(1061, 1012, -42),
        new(1061, 1011, -42),
        new(1061, 1010, -42),
        new(1060, 1010, -42),
        new(1059, 1010, -42),
        new(1059, 1009, -42),
        new(1059, 1008, -42),
        new(1059, 1007, -42),
        new(1059, 1006, -42),
        new(1059, 1005, -42),
        new(1059, 1004, -42),
        new(1058, 1004, -42),
        new(1057, 1004, -42),
        new(1057, 1003, -42),
        new(1057, 1002, -42),
        new(1057, 1001, -42),
        new(1057, 1000, -42),
        new(1057, 999, -42),
        new(1058, 999, -42),
        new(1059, 999, -42),
        new(1060, 999, -42),
        new(1061, 999, -42),
        new(1062, 999, -42),
        new(1063, 999, -42),
        new(1063, 998, -42),
        new(1063, 997, -42),
        new(1063, 996, -42),
        new(1063, 995, -42),
        new(1063, 994, -42),
        new(1062, 994, -42),
        new(1061, 994, -42),
        new(1061, 993, -42),
        new(1061, 992, -42),
        new(1061, 991, -42),
        new(1061, 990, -42)
    };

    private static List<Point3D> _path;

    public static List<Point3D> Path => _path;

    public static MazeOfDeathRegion Instance { get; private set; }

    public static void Initialize()
    {
        EnsureRegistered();
    }

    /// <summary>Registers the region (once) and lays the path (once); returns the registration.</summary>
    public static MazeOfDeathRegion EnsureRegistered()
    {
        if (_path == null)
        {
            BuildPath();
        }

        if (Instance?.Registered == true)
        {
            return Instance;
        }

        Instance = new MazeOfDeathRegion();
        Instance.Register();
        return Instance;
    }

    /// <summary>Test support.</summary>
    public static void RemoveRegistration()
    {
        if (Instance != null)
        {
            Instance.Unregister();
            Instance = null;
        }
    }

    /// <summary>ServUO's Initialize: the fixed tiles, then 33 random tiles inside the trap bounds.</summary>
    public static void BuildPath()
    {
        _path = new List<Point3D>(FixedPath);

        var toAdd = RandomPathTiles;

        while (toAdd > 0)
        {
            var x = Utility.RandomMinMax(TrapBounds.X, TrapBounds.X + TrapBounds.Width);
            var y = Utility.RandomMinMax(TrapBounds.Y, TrapBounds.Y + TrapBounds.Height);
            var z = -42;

            var p = new Point3D(x, y, z);

            if (!_path.Contains(p))
            {
                _path.Add(p);
                toAdd--;
            }
        }
    }

    public MazeOfDeathRegion() : base(RegionName, Map.TerMur, DefaultPriority + 1, ConvertTo3D(Bounds))
    {
    }

    public override bool OnBeginSpellCast(Mobile m, ISpell s)
    {
        if (m.AccessLevel > AccessLevel.Player)
        {
            return true;
        }

        if (s is MarkSpell or GateTravelSpell or TeleportSpell)
        {
            m.SendLocalizedMessage(501802); // that spell doesn't seem to work.
            return false;
        }

        return base.OnBeginSpellCast(m, s);
    }

    public override bool CheckTravel(Mobile m, Point3D newLocation, TravelCheckType travelType, out TextDefinition message)
    {
        message = null;

        if (m?.AccessLevel > AccessLevel.Player)
        {
            return true;
        }

        return travelType == TravelCheckType.RecallFrom;
    }

    public override void OnEnter(Mobile m)
    {
        if (m.Backpack == null)
        {
            return;
        }

        if (m.NetState != null)
        {
            m.Paralyze(TimeSpan.FromSeconds(2));
            m.PrivateOverheadMessage(MessageType.Regular, 33, 1113580, m.NetState);    // You are filled with a sense of dread and impending doom!
            m.PrivateOverheadMessage(MessageType.Regular, 0x3B2, 1113581, m.NetState); // I might need something to help me navigate through this.

            if (m.Backpack.FindItemByType<GoldenCompass>() != null)
            {
                m.CloseGump<CompassDirectionGump>();
                m.SendGump(new CompassDirectionGump(m));
            }
        }

        base.OnEnter(m);
    }

    public override void OnExit(Mobile m)
    {
        m.CloseGump<CompassDirectionGump>();
        base.OnExit(m);
    }

    public override void OnLocationChanged(Mobile m, Point3D oldLocation)
    {
        base.OnLocationChanged(m, oldLocation);

        if (oldLocation.X > 1063 && m.Location.X <= 1063)
        {
            if (m.Backpack?.FindItemByType<GoldenCompass>() != null)
            {
                m.SendLocalizedMessage(1113582); // I better proceed with caution.
            }
            else
            {
                m.SendLocalizedMessage(1113581); // I might need something to help me navigate through this.
            }
        }
        else if (oldLocation.Y == 991 && m.Location.Y == 990)
        {
            if (m.HasGump<CompassDirectionGump>())
            {
                m.SendLocalizedMessage(1113585); // The compass' arrows flicker. You must be near the right location.
            }
        }

        if (m?.Backpack != null)
        {
            if (m.Alive && !_path.Contains(m.Location) && TrapBounds.Contains(m.Location))
            {
                SpringTrap(m);
            }
            else if (m.Alive && m.HasGump<CompassDirectionGump>())
            {
                m.CloseGump<CompassDirectionGump>();
                m.SendGump(new CompassDirectionGump(m));
            }
        }
    }

    public override void OnDeath(Mobile m)
    {
        base.OnDeath(m);

        if (m.Player && TrapBounds.Contains(m.Location))
        {
            Timer.DelayCall(TimeSpan.FromSeconds(3), () => KickToEntrance(m));
        }
    }

    public void SpringTrap(Mobile from)
    {
        if (from?.Alive != true)
        {
            return;
        }

        int cliloc;
        var damage = Utility.RandomMinMax(75, 150);

        switch (Utility.Random(4))
        {
            default:
            case 0:
                Effects.SendLocationEffect(from, 0x3709, 30);
                from.PlaySound(0x54);
                cliloc = 1010524; // Searing heat scorches thy skin.
                AOS.Damage(from, damage, 0, 100, 0, 0, 0);
                break;
            case 1:
                from.PlaySound(0x223);
                cliloc = 1010525; // Pain lances through thee from a sharp metal blade.
                AOS.Damage(from, damage, 100, 0, 0, 0, 0);
                break;
            case 2:
                from.BoltEffect(0);
                cliloc = 1010526; // Lightning arcs through thy body.
                AOS.Damage(from, damage, 0, 0, 0, 0, 100);
                break;
            case 3:
                Effects.SendLocationEffect(from, 0x113A, 20, 10);
                from.PlaySound(0x231);
                from.ApplyPoison(from, Poison.Deadly);
                cliloc = 1010523; // A toxic vapor envelops thee.
                AOS.Damage(from, damage, 0, 0, 0, 100, 0);
                break;
        }

        from.LocalOverheadMessage(MessageType.Regular, 0xEE, cliloc);
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

        var p = new Point3D(x, y, z);

        from.MoveToWorld(p, Map.TerMur);

        if (from.Player && !from.Alive && from.Corpse != null)
        {
            from.Corpse.MoveToWorld(p, Map.TerMur);
        }

        from.SendMessage("You have been teleported to the beginning of the maze.");
    }
}
