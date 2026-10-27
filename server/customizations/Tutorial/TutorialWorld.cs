// cc-P663 (P481 F3): the Wakening Isle, where the core tutorial is taught now, and the one rule for which map a trainee's
// lessons are on. Notes: shard-migration notes/cc-P663-tutorial-moves-to-isle.md. Design: P481 sections 4, 11.2 and
// 12.3; site and coordinates: P673 section 1.4 (site A, the south-east void; P481's void C plan shifted by +424,+1992).
//
// One lesson map: every core lookup (fit checks, NPC and prop searches, arrows, "nearby", the death lesson, the seeder)
// asks MapOf(trainee) or Isle, never Map.Trammel. Today both are the Fold, map 32 (FoldMaps); F4 (P664) hands each trainee
// a private copy (33 to 48) and MapOf returns that. The wing and trade trials stay in New Haven (HavenSpots) until S7.
//
// The isle is painted by P662 on Trammel's file, which the Fold reads (FoldMaps: file index 1). Until that patch is on
// the server the site is void; the seeder then refuses (its fit check fails), as it should.
//
// Crossing: every move onto or off the Fold (arrival, the dock moongate, Rook's remedial trip, the skip) goes through
// Cross, which takes the trainee's controlled pets first. On the Fold it takes every one of them on the Fold, whatever
// the range or order (P605 risk 4: pinned's TeleportPets only takes pets within 3 tiles that guard, follow or come).

#nullable enable

using System.Collections.Generic;
using Server.Mobiles;

namespace Server.Tutorial;

public static class TutorialWorld
{
    /// <summary>The isle's map: the Fold (32). F4 adds the copies.</summary>
    public static Map Isle => FoldMaps.Fold;

    /// <summary>
    /// The map a trainee's core lessons are on: the tutorial copy they are on or logged out on (cc-P664, TutorialCopies),
    /// else the Fold. No side effects: copies are handed out only by TutorialStart.SendToIsle.
    /// </summary>
    public static Map MapOf(PlayerMobile pm) => TutorialCopies.CopyOf(pm) ?? Isle;

    /// <summary>P673 1.4: site A minus P481's void C. Every stop below is P481 11.2's plus this.</summary>
    public static readonly Point2D SiteShift = new(424, 1992);

    /// <summary>The drawn area with its sea margin, x 6594-6865, y 3905-4049 inclusive.</summary>
    public static readonly Rectangle2D Area = new(new Point2D(6594, 3905), new Point2D(6866, 4050));

    // The stops, west to east (P481 11.2, P673 1.4). Z is read from the lesson map (Ground): flat Z 0 except the knoll.
    /// <summary>Stop 0, the west beach: where a trainee wakes ([Fold goto's default after S2).</summary>
    public static readonly Point2D Beach = new(6638, 3993);
    /// <summary>Tamsin, the guide, two tiles up the road from the beach.</summary>
    public static readonly Point2D Guide = new(6640, 3992);
    /// <summary>Stop 1, the two bright tiles in the road.</summary>
    public static readonly Point2D BlueTile = new(6646, 3992);
    public static readonly Point2D GoldTile = new(6649, 3992);
    /// <summary>Stop 2, the camp beside the road: the backpack lesson.</summary>
    public static readonly Point2D Camp = new(6656, 3990);
    /// <summary>Stop 3, the clothes rack: the paperdoll lesson.</summary>
    public static readonly Point2D Rack = new(6665, 3992);
    /// <summary>Stop 4, the signpost at the meadow's edge: the chat lesson.</summary>
    public static readonly Point2D Signpost = new(6692, 3990);
    /// <summary>Stop 5, the farm gate (the farm's own spots are in TutorialFarm, moved with the farm).</summary>
    public static readonly Point2D FarmGate = new(6714, 3979);
    /// <summary>Stop 6, the training yard's gate (x 6726-6748, y 3994-4010): the death lesson inside.</summary>
    public static readonly Point2D Yard = new(6736, 3995);
    public static readonly Point2D Echo = new(6736, 3998);
    public static readonly Point2D Healer = new(6740, 4000);
    public static readonly Point2D GhostSpot = new(6739, 4001);
    /// <summary>Stop 7, the practice bank's door in the trades square; Merrin inside.</summary>
    public static readonly Point2D BankStop = new(6762, 3981);
    public static readonly Point2D BankDoor = new(6762, 3980);
    public static readonly Point2D Banker = new(6762, 3976);
    public static readonly Point2D BankBox = new(6760, 3975);
    /// <summary>Stop 8, the League lodge's door (north wall); Elara inside.</summary>
    public static readonly Point2D LodgeStop = new(6782, 3994);
    public static readonly Point2D LodgeDoor = new(6782, 3995);
    public static readonly Point2D Elara = new(6782, 3999);
    /// <summary>Stop 9, the standing stones on the knoll (the ring's gap faces the road).</summary>
    public static readonly Point2D Stones = new(6794, 3982);
    /// <summary>Stop 10, the dock's end and its moongate out to New Haven.</summary>
    public static readonly Point2D DockEnd = new(6831, 3991);
    public static readonly Point2D Moongate = new(6832, 3991);

    public static Point3D Ground(Map map, Point2D p) => new(p.X, p.Y, map.GetAverageZ(p.X, p.Y));

    public static Point3D Ground(Point2D p) => Ground(Isle, p);

    /// <summary>Where a trainee wakes, or is sent back to (remedial training).</summary>
    public static Point3D Arrival => Ground(Beach);

    /// <summary>On the isle: the Fold (or a copy), inside the drawn area.</summary>
    public static bool IsOnIsle(Point3D p, Map? map) => FoldMaps.IsFold(map) && Area.Contains(p);

    public static bool IsOnIsle(Mobile m) => IsOnIsle(m.Location, m.Map);

    /// <summary>How far a pet may be from a trainee entering the Fold from elsewhere and still come along.</summary>
    public const int EnterRange = 12;

    /// <summary>
    /// The pets that cross with <paramref name="pm"/>: their own controlled, living or ghost, not stabled. On the Fold, every
    /// one on the Fold; elsewhere, those within EnterRange on the trainee's map. A mount is carried, so is not listed.
    /// </summary>
    public static List<BaseCreature> PetsCrossing(PlayerMobile pm)
    {
        var list = new List<BaseCreature>();
        if (pm.AllFollowers == null)
        {
            return list;
        }

        var onFold = FoldMaps.IsFold(pm.Map);
        foreach (var m in pm.AllFollowers)
        {
            if (m is BaseCreature { Deleted: false, Controlled: true, IsStabled: false } pet && pet.ControlMaster == pm &&
                pet.Map != null && pet.Map != Map.Internal &&
                (onFold ? FoldMaps.IsFold(pet.Map) : pet.Map == pm.Map && pm.InRange(pet, EnterRange)))
            {
                list.Add(pet);
            }
        }

        return list;
    }

    /// <summary>Moves a trainee and the pets that cross with them (PetsCrossing). False when the player's own move was refused.</summary>
    public static bool Cross(PlayerMobile pm, Point3D to, Map map)
    {
        var pets = PetsCrossing(pm);
        pm.MoveToWorld(to, map);
        if (pm.Map != map || pm.Location != to)
        {
            return false; // the island gate refused it, and said why
        }

        foreach (var pet in pets)
        {
            pet.MoveToWorld(to, map);
        }

        return true;
    }
}
