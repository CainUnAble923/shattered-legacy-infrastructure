// cc-P36 Part C (Chase's decisions 2026-10-01, items 2, 3, 5 and 6; shard-migration notes/dungeon-decisions-2026-10-01.md):
// the New Haven dungeon's region, registered in code over the still-empty interior so its harvest, travel and Young rules
// can be built and tested before the map is.
//
// Where: Trammel x 5136-5247, y 1128-1263 (cc-P24 candidate A). Void land with no statics in 114 or 117, inside no
// region of pinned Data/regions.json (checked again for cc-P36: 145 Trammel regions, none overlaps). Building keeps at
// least 20 tiles from x 5120.
//
// What it is: a stock DungeonRegion (pinned Regions/DungeonRegion.cs: Young players are not protected and get the
// warning gump on entry, BaseRegion.cs:76-81; no housing; dungeon light; Entrance for the Young death teleport,
// PlayerMobile.cs:4079-4081 at d4531cd9), plus two shard rules, both deviations in the register:
//   1. At most MaxHostilesOnYoung hostile creatures attack one Young player at once (Region.AllowHarmful,
//      Server/Regions/Region.cs:802, reached from Mobile.CanBeHarmful, Mobile.cs:8367, through the ATTACKER's region).
//   2. No magical entry (decisions file, "Item 3, settled"). SpellHelper.CheckTravel asks the destination's region and
//      the caster's region with the same type and location (SpellHelper.cs:637-645 at d4531cd9), so this decides by
//      where the travel ends, not by type alone. A region's own message is used whenever it sets one (:641,
//      message ??= InvalidTravelMessage(type)), so each refusal says its own line.
//
// Priority: Region.DefaultPriority (50). Despise needed 51 because it sits inside the static "Despise" region at the same
// priority and pinned constructs every region as Dynamic, so a tie falls to list order (DespiseRevamped/Region.cs:44-55).
// Nothing overlaps this rectangle, so there is no tie to break; if a regions.json entry ever overlaps it, raise this to
// DefaultPriority + 1 for the same reason Despise records.
//
// Registered in Initialize, after RegionJsonSerializer.LoadRegions and World.Load (Main.cs:671-675), every start. It has
// no saved data, so it exists after any restart.

using Server.Mobiles;
using Server.Spells;

namespace Server.Regions;

public class OldHavenBurrowsRegion : DungeonRegion
{
    /// <summary>The player name, the Rootdeep Burrows (Chase, 2026-10-15, F-45; cc-P141). The class keeps the working name.</summary>
    public const string RegionName = "the Rootdeep Burrows";

    /// <summary>x 5136-5247, y 1128-1263 inclusive (Rectangle3D's end is exclusive, Rectangle3D.cs:137-143).</summary>
    public static readonly Rectangle3D Area = new(new Point3D(5136, 1128, MinZ), new Point3D(5248, 1264, MaxZ));

    /// <summary>
    /// Old Haven's ruined Second Bank, the hole at 3619,2620 (decisions file item 6). Confirmed by cc-P188: the bank's
    /// wooden floor is Z 0, the map patch opens its south wall (3618-3620,2622) so the hole is reached on foot from
    /// New Haven, and the seeder's teleporter down stands on it (ClusterFRootdeepSeeder.Teleporters). Was named
    /// ProvisionalEntrance until P188. cc-P413: the hole is now an ant-hill burrow whose dark mouth is x 3617-3619,
    /// y 2617-2620 (ClusterFRootdeepSeeder.Mouth*); this tile is one of the twelve, so the Entrance stays here.
    /// </summary>
    public static readonly Point3D HoleEntrance = new(3619, 2620, 0);

    /// <summary>Hostile creatures that may attack one Young player at once (decisions file item 2: 2 to 3, pick 3).</summary>
    public const int MaxHostilesOnYoung = 3;

    // Chase's flavored refusals (decisions file, "Flavored failure messages"), as written.
    public const string RecallInMessage = "The rune finds only earth and root. Something below will not let you be pulled down.";
    public const string GateInMessage = "The gate tears open onto packed soil, and collapses.";
    public const string GateOutMessage = "The burrow swallows the light before the gate can take shape. Walk out, or recall.";
    public const string MarkMessage = "The rune will not take. The ground here does not want to be remembered.";
    public const string TeleportInMessage = "You cannot reach through the earth.";

    public static OldHavenBurrowsRegion Instance { get; private set; }

    public OldHavenBurrowsRegion() : base(RegionName, Map.Trammel, DefaultPriority, Area)
    {
        Entrance = HoleEntrance;
        GoLocation = new Point3D(5191, 1195, 0); // the middle; the map batch sets the real arrival spot
    }

    public static void Initialize()
    {
        if (Instance != null || Map.Trammel == null)
        {
            return;
        }

        Instance = new OldHavenBurrowsRegion();
        Instance.Register();
    }

    /// <summary>True when <paramref name="loc"/> on <paramref name="map"/> is inside the Burrows' rectangle.</summary>
    public static bool IsInside(Map map, Point3D loc) => map == Map.Trammel && Area.Contains(loc);

    // ---------------------------------------------------------------- travel

    public override bool CheckTravel(Mobile m, Point3D newLocation, TravelCheckType travelType, out TextDefinition message)
    {
        message = null;

        if (m == null || m.AccessLevel > AccessLevel.Player)
        {
            return true; // staff are exempt, as NoTravelSpellsAllowedRegion.cs:26 exempts them
        }

        // The caller gives no map with newLocation; this region is only asked about Trammel travel (as the destination's
        // region) or by a caster standing in it, so a Trammel test of the rectangle is what it means.
        var endsInside = Area.Contains(newLocation);
        var casterInside = IsInside(m.Map, m.Location);

        switch (travelType)
        {
            case TravelCheckType.RecallTo when endsInside: // Recall, scrolls, runebooks; Sacred Journey uses the same type
                message = RecallInMessage;
                return false;
            case TravelCheckType.GateTo when endsInside:
                message = GateInMessage;
                return false;
            case TravelCheckType.TeleportTo when endsInside && !casterInside: // Teleport and Shadow Jump from outside
                message = TeleportInMessage;
                return false;
            case TravelCheckType.Mark when endsInside:
                message = MarkMessage;
                return false;
            case TravelCheckType.GateFrom when endsInside:
                // A gate cast inside opens a linked pair whose outer half anyone may use for 30 seconds
                // (Spells/Seventh/GateTravel.cs), and using a moongate never asks CheckTravel. So no gate out.
                message = GateOutMessage;
                return false;
        }

        return true; // RecallFrom, TeleportFrom, and anything ending outside: leaving is allowed
    }

    // ---------------------------------------------------------------- Young limit

    public override bool AllowHarmful(Mobile from, Mobile target)
    {
        if (target is PlayerMobile { Young: true } young && IsHostileCreature(from)
            && !IsAttacking(from, young) && HostilesAttacking(young) >= MaxHostilesOnYoung)
        {
            return false;
        }

        return base.AllowHarmful(from, target);
    }

    /// <summary>A creature no player owns: not tamed, not a player's summon.</summary>
    public static bool IsHostileCreature(Mobile m) =>
        m is BaseCreature { Deleted: false, Alive: true, Controlled: false } bc && bc.SummonMaster is not PlayerMobile;

    /// <summary>
    /// "Attacking" as pinned records it: an unexpired entry in the player's Aggressors list (Mobile.Aggressors, refreshed by
    /// every harmful act, expiring 2 minutes after the last, AggressorInfo.cs:32-43) whose creature still has the player as
    /// its combatant. The combatant test drops a creature that hit once and moved on, which the 2-minute expiry alone
    /// would keep counting.
    /// </summary>
    public static bool IsAttacking(Mobile creature, Mobile player)
    {
        if (creature.Combatant != player)
        {
            return false;
        }

        foreach (var info in player.Aggressors)
        {
            if (info.Attacker == creature && !info.Expired)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The hostile creatures attacking <paramref name="player"/> now.</summary>
    public static int HostilesAttacking(Mobile player)
    {
        var count = 0;

        foreach (var info in player.Aggressors)
        {
            if (!info.Expired && IsHostileCreature(info.Attacker) && info.Attacker.Combatant == player)
            {
                count++;
            }
        }

        return count;
    }
}
