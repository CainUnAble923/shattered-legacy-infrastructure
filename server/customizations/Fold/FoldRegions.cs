// FoldRegions.cs
//
// cc-P661 (P481 F1): the rules of the Fold (FoldMaps.cs), and a guard on Trammel's side of the same tiles.
// Notes: shard-migration notes/cc-P661-fold-map-32.md.
//
// FoldRegion: the DEFAULT region of each Fold map (32 to 48), set by FoldMaps.Configure as each map is made. Map-wide bans
// are hard-coded per stock map (HousePlacement.cs:66), so only a region can carry them here, and the default region is
// the one Region.Find returns wherever no registered region lies (Region.cs:291-311; Mobile.Region, Mobile.cs:1572). It
// is not registered: a registered region over a whole 7168 x 4096 map makes all 114,688 of its sectors, which on 17 maps
// cost minutes and a great deal of memory in the first build of this (notes). A zone region added on the Fold later
// (F2, F3) takes FoldRegion.On(map) as its Parent, so these rules still hold inside it: BaseRegion.CheckTravel defers to
// the parent (BaseRegion.cs:171-175) and HousePlacement's IsPartOf walks parents.
//   - No housing: it is a NoHousingRegion, the type HousePlacement.Check refuses by (HousePlacement.cs:76). Staff from
//     GameMaster place anywhere there, as everywhere (:61).
//   - No recall, mark or gate in or out: CheckTravel refuses every travel type for players, the GreenAcresRegion model
//     (GreenAcresRegion.cs:26-30). SpellHelper.CheckTravel asks the destination's region and the caster's (SpellHelper.cs
//     633-642), so recall and gate out of the Fold, and into it from anywhere, are both refused. Only our own moongates
//     and staff move people across.
//   - Always day: AlterLightLevel sets the global level to LightCycle.DayLevel, as DungeonRegion.cs:27-30 sets the
//     dungeon level. A client's own light setting can still override it (P605 risk 7). cc-P665: a player inside the
//     loop vision gets the scene's level instead (TutorialVisionScene.AlterLight), for them alone.
//   Trammel's protections (Young, no non-consensual PvP) come from the map's TrammelRules, not from here.
//
// FoldMirrorRegion: Trammel over void C (x 6160-6639, y 1832-2279), where the Wakening Isle's tiles are painted (design
// 3.1). The same tiles exist on Trammel, where no road, gate or teleporter reaches them; if a bug or a future teleporter
// ever puts a player there, they are sent to the New Haven start (HavenSpots.Start, cc-P663). Staff are left
// alone, so they can inspect the tiles on Trammel. Travel into it is refused for players too, so a rune never lands
// one there. P24 and P481 found void C in no pinned region and no ServUO Trammel region.
//
// FoldMirrorRegion is registered in Initialize, after RegionJsonSerializer.LoadRegions and World.Load (Main.cs:669-673),
// every start. Neither holds saved data.

using System;
using Server.Mobiles;
using Server.Spells;

namespace Server.Regions;

public class FoldRegion : NoHousingRegion
{
    public FoldRegion(Map map) : base(map.Name, map, DefaultPriority, Array.Empty<Rectangle3D>())
    {
    }

    /// <summary>The Fold's rules on <paramref name="map"/> (its default region), or null off the Fold.</summary>
    public static FoldRegion On(Map map) => map?.DefaultRegion as FoldRegion;

    public override bool CheckTravel(Mobile m, Point3D newLocation, TravelCheckType travelType, out TextDefinition message)
    {
        message = null; // the stock line for each type (SpellHelper.InvalidTravelMessage)
        return m == null || m.AccessLevel > AccessLevel.Player;
    }

    // cc-P718 item 8: on the Wakening Isle each skill gains at most once per TutorialSkillPace.Interval.
    public override bool AllowGain(Mobile m, Skill skill, object obj) =>
        Server.Tutorial.TutorialSkillPace.Allow(m, skill) && base.AllowGain(m, skill, obj);

    public override void AlterLightLevel(Mobile m, ref int global, ref int personal)
    {
        global = LightCycle.DayLevel;
        Server.Tutorial.TutorialWake.AlterLight(m, ref global, ref personal); // cc-P721: waking on the beach
        Server.Tutorial.TutorialVisionScene.AlterLight(m, ref global, ref personal); // cc-P665: the vision's light, per player
    }
}

public class FoldMirrorRegion : BaseRegion
{
    public const string RegionName = "the Fold's mirror";

    /// <summary>Void C, x 6160-6639, y 1832-2279 inclusive (Rectangle3D's end is exclusive).</summary>
    public static readonly Rectangle3D Area = new(new Point3D(6160, 1832, MinZ), new Point3D(6640, 2280, MaxZ));

    public const string SentHomeMessage = "The empty land lets go of you, and you wake in New Haven.";

    public static FoldMirrorRegion Instance { get; private set; }

    public FoldMirrorRegion() : base(RegionName, Map.Trammel, DefaultPriority, Area)
    {
    }

    public static void Initialize()
    {
        if (Instance != null || Map.Trammel == null)
        {
            return;
        }

        Instance = new FoldMirrorRegion();
        Instance.Register();
    }

    /// <summary>
    /// Where a player found here goes: the New Haven start. cc-P663: read from HavenSpots, since the start list's first
    /// entry is now "Tutorial Island", whose coordinates are only a button's place on the city screen.
    /// </summary>
    public static (Point3D Location, Map Map) Home => (Server.Tutorial.HavenSpots.Start, Server.Tutorial.HavenSpots.Map);

    public static bool SendsHome(Mobile m) => m is PlayerMobile && m.AccessLevel == AccessLevel.Player;

    public override bool CheckTravel(Mobile m, Point3D newLocation, TravelCheckType travelType, out TextDefinition message)
    {
        message = null;
        return m == null || m.AccessLevel > AccessLevel.Player;
    }

    public override void OnEnter(Mobile m)
    {
        base.OnEnter(m);

        if (SendsHome(m))
        {
            // Not inside the region change itself: the move would re-enter UpdateRegion while it runs.
            Timer.DelayCall(TimeSpan.Zero, SendHome, m);
        }
    }

    /// <summary>Moves <paramref name="m"/> to the New Haven start if it is still a player inside the mirror.</summary>
    public static void SendHome(Mobile m)
    {
        if (m == null || m.Deleted || !SendsHome(m) || m.Map != Map.Trammel || !Area.Contains(m.Location))
        {
            return;
        }

        var (location, map) = Home;
        BaseCreature.TeleportPets(m, location, map);
        m.MoveToWorld(location, map);
        m.SendMessage(SentHomeMessage);
    }
}
