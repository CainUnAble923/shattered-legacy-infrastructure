// cc-P289: test-world lesson props, not a map build. No terrain, architecture or permanent decor is changed.
// cc-P663 (P481 F3): the core's props and NPCs stand on the Wakening Isle now (TutorialWorld, map 32), at P481 11.2's stops
// shifted to site A (P673 1.4); every lookup asks the lesson map. New Haven keeps Rook and its vet (HavenSpots). The isle's
// walls, floors, fences, dock and stones are P662's map patch; the doors, NPCs, lesson props and the moongate are server
// items placed here, so the seeder refuses until that patch is on the server (the void fails its fit check).
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using ModernUO.Serialization;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Server.Tutorial;

public static class TutorialCorePlacement
{
    public static Func<bool> IsTestShard { get; set; } = () => ClusterFStaffHub.IsTestShard;
    // Fake map seam for tests; the real seeder always checks the loaded server map (the lesson map, or New Haven's).
    public static Func<Point3D, Map, bool> FitsOn { get; set; } = (p, map) => map.CanFit(p, 16, false, false);
    public static bool Fits(Point3D p) => FitsOn(p, TutorialWorld.Isle);
    private static Point3D Ground(Point2D p) => TutorialWorld.Ground(p);

    public static Point3D FirstMark => Ground(TutorialWorld.BlueTile);
    public static Point3D SecondMark => Ground(TutorialWorld.GoldTile);
    public static Point3D DeathSpot => Ground(TutorialWorld.Echo);
    public static Point3D HealerSpot => Ground(TutorialWorld.Healer);
    public static Point3D GhostSpot => Ground(TutorialWorld.GhostSpot);
    public static Point3D GateSpot => Ground(TutorialWorld.Moongate);
    public static Point3D GateExit => HavenSpots.MoongateExit;
    // cc-P480 Part 8: a vet beside the Farmhand, and one in New Haven by the stables when the town has none.
    public static Point3D FarmVetSpot => Ground(TutorialFarm.FarmVetSpot);
    public static Point3D BankerSpot => Ground(TutorialWorld.Banker);
    public static Point3D ElaraSpot => Ground(TutorialWorld.Elara);

    /// <summary>Where a lesson sends the arrow: the thing to walk to, use or talk to.</summary>
    public static Point3D Spot(string key) => key switch
    {
        "core.movement" => FirstMark,
        "core.backpack" => Ground(TutorialWorld.Camp),
        "core.paperdoll" => Ground(ClothesTable), // cc-P718: the table the cloak lies on
        "core.chat" => Ground(ChatSignpost), // cc-P718: the post itself, not the stop in front of it
        "core.farm" => Ground(TutorialFarm.FarmhandSpot),
        "core.healing" => TutorialFarm.PipBench, // cc-P726: Pip on his bench
        "core.death" => DeathSpot,
        "core.banking" => BankerSpot,
        "core.league" => ElaraSpot,
        "core.loop" => Ground(TutorialWorld.Stones),
        "core.travel" => GateSpot,
        _ => Ground(TutorialWorld.Guide)
    };

    /// <summary>Where each lesson's marker stands: its stop on the road (P481 11.2), outside any door.</summary>
    public static Point3D MarkerSpot(string key) => key switch
    {
        "core.movement" => Ground(new Point2D(TutorialWorld.BlueTile.X, TutorialWorld.BlueTile.Y + 1)),
        "core.paperdoll" => Ground(TutorialWorld.Rack),
        "core.chat" => Ground(TutorialWorld.Signpost),
        "core.farm" => Ground(TutorialWorld.FarmGate),
        "core.healing" => Ground(new Point2D(6690, 3979)), // the farmhouse door's step
        "core.death" => Ground(TutorialWorld.Yard),
        "core.banking" => Ground(TutorialWorld.BankStop),
        "core.league" => Ground(TutorialWorld.LodgeStop),
        "core.travel" => Ground(TutorialWorld.DockEnd),
        _ => Spot(key)
    };

    /// <summary>
    /// cc-P720 item 3: where each lesson's signpost stands, beside its stop. The standing signpost is impassable, so it
    /// cannot stand on the stop (the road runs through it). Each is the nearest tile to the stop that is off the walked
    /// route and the road, outside every building, clear of every spot, door and gate, fits a person, and leaves the stop
    /// reachable (shard-migration notes/cc-P720-tools/sign_spots.py, on the built isle).
    /// </summary>
    public static Point2D SignPoint(string key) => key switch
    {
        "core.movement" => new Point2D(6646, 3994),
        "core.backpack" => new Point2D(6656, 3992),
        "core.paperdoll" => new Point2D(6665, 3993),
        "core.chat" => new Point2D(6692, 3991),
        "core.farm" => new Point2D(6714, 3981),
        "core.healing" => HealingSign,
        "core.death" => new Point2D(6736, 3996),
        "core.banking" => new Point2D(6762, 3982),
        "core.league" => new Point2D(6780, 3994),
        "core.loop" => new Point2D(6794, 3981),
        "core.travel" => new Point2D(6831, 3992),
        _ => new Point2D(MarkerSpot(key).X, MarkerSpot(key).Y)
    };

    public static Point3D SignSpot(string key) => Ground(SignPoint(key));

    /// <summary>
    /// cc-P726: the healing lesson's signpost, outside the farmhouse door (6690,3978) to the south-west, clear of its steps
    /// (6689-6691,3979) and its own sign (6692,3979). The seeder's fit check refuses the isle if it is blocked.
    /// </summary>
    public static readonly Point2D HealingSign = new(6688, 3980);

    /// <summary>cc-P720 item 3: the lesson sign's name, shown on a single click: "Lesson 5: The farm: ...".</summary>
    public static string SignName(string key)
    {
        var core = TutorialStations.Core.ToList();
        var i = core.FindIndex(s => s.Key == key);
        return i < 0 ? TutorialStations.Find(key)?.Title ?? key : $"Lesson {i + 1}: {core[i].Title}";
    }

    public const int OldMarkerId = 0x1F14; // the rune the markers were before cc-P720

    /// <summary>
    /// cc-P720 item 4: the practice bank's floor (inside its walls). The bank lesson's arrow points at the door until the
    /// trainee stands in here, then at Merrin.
    /// </summary>
    public static readonly Rectangle2D BankFloor = new(new Point2D(6758, 3973), new Point2D(6767, 3980));

    public static bool InBank(Point3D p) => BankFloor.Contains(new Point2D(p.X, p.Y));

    // cc-P720 item 5: the League's notice board, inside the lodge on its north wall, west of the door.
    public static readonly Point2D LeagueBoard = new(6780, 3996);

    // cc-P718 item 5: the clothes stop's table, beside the rack, where each trainee's cloak waits.
    public static readonly Point2D ClothesTable = new(TutorialWorld.Rack.X - 1, TutorialWorld.Rack.Y - 2);
    public const int TableId = 0x0B8F; // "table": a surface 6 high
    public const int TableHeight = 6;

    // cc-P718 item 6: a standing post with its sign board (0xA854, "sign_post"), where the chat lesson's post stood. The
    // old art, 0x0B97, is the arm that hangs a shop sign from a wall: on the ground it read as debris (Chase, 10-28).
    public static readonly Point2D ChatSignpost = new(TutorialWorld.Signpost.X, TutorialWorld.Signpost.Y - 2);
    public const int SignpostId = 0xA854;
    public const string ChatSignpostName = "Farm signpost: say 'I am ready to learn'";
    public const int OldSignpostId = 0x0B97;
    public const string OldChatSignpostName = "signpost: the farm, east";

    // The stops' props (all stock art, server items so P662's statics stay the map's own): id, spot, name.
    public static readonly (int Id, Point2D At, string Name)[] Props =
    [
        (0x0DE3, new Point2D(TutorialWorld.Camp.X - 1, TutorialWorld.Camp.Y - 2), "campfire"),
        (0x0A55, new Point2D(TutorialWorld.Camp.X + 1, TutorialWorld.Camp.Y - 2), "bedroll"),
        (0x0A55, new Point2D(TutorialWorld.Camp.X + 2, TutorialWorld.Camp.Y - 2), "bedroll"),
        (0x0EC6, new Point2D(TutorialWorld.Rack.X, TutorialWorld.Rack.Y - 2), "clothes rack"),
        (TableId, ClothesTable, "table"),
        (SignpostId, ChatSignpost, ChatSignpostName),
        (0x0E7C, TutorialWorld.BankBox, "practice bank box")
    ];

    public static TutorialStationMarker? FindMarker(string key, Map map)
    {
        foreach (var item in map.GetItemsInRange<TutorialStationMarker>(TutorialWorld.Ground(map, SignPoint(key)), 0))
        { if (!item.Deleted && item.StationKey == key) { return item; } }
        return null;
    }

    public static TutorialStationMarker? FindMarker(string key) => FindMarker(key, TutorialWorld.Isle);

    private static Mobile? FindMobile(Map map, Type type, Point3D spot)
    {
        foreach (var m in map.GetMobilesInRange(spot, 2))
        { if (!m.Deleted && type.IsInstanceOfType(m)) { return m; } }
        return null;
    }

    private static bool HasItem<T>(Map map, Point3D spot, Func<T, bool>? match = null) where T : Item
    {
        foreach (var item in map.GetItemsInRange<T>(spot, 0))
        { if (!item.Deleted && (match == null || match(item))) { return true; } }
        return false;
    }

    private static bool HasDoor(Map map, Point3D spot)
    {
        foreach (var door in map.GetItemsInRange<BaseDoor>(spot, 2))
        {
            var closed = door.Open ? new Point3D(door.X - door.Offset.X, door.Y - door.Offset.Y, door.Z - door.Offset.Z) : door.Location;
            if (!door.Deleted && closed == spot) { return true; }
        }
        return false;
    }

    /// <summary>
    /// The practice bank's and the lodge's doors, both in an east-west wall (south and north walls). cc-P720: hung
    /// WestCCW, so open the door folds back into the wall line (pinned offset -1, 0); WestCW swung it out past the wall
    /// (Chase 10-28).
    /// </summary>
    public static BaseDoor MakeDoor() => new DarkWoodDoor(DoorFacing.WestCCW);

    public const int DoorClosedId = 0x06A9; // DarkWoodDoor WestCCW, closed

    /// <summary>cc-P720: a door at <paramref name="spot"/> hung another way (before cc-P720), or null.</summary>
    private static BaseDoor? OldDoor(Map map, Point3D spot)
    {
        foreach (var door in map.GetItemsInRange<BaseDoor>(spot, 2))
        {
            var closed = door.Open ? new Point3D(door.X - door.Offset.X, door.Y - door.Offset.Y, door.Z - door.Offset.Z) : door.Location;
            if (!door.Deleted && closed == spot && door.ClosedId != DoorClosedId) { return door; }
        }
        return null;
    }

    /// <summary>cc-P720: moves every lesson marker still on its stop (a rune, before cc-P720) to its sign spot.</summary>
    public static int MoveOldMarkers(Map map)
    {
        var moved = new List<TutorialStationMarker>();
        foreach (var station in TutorialStations.Core)
        {
            foreach (var item in map.GetItemsInRange<TutorialStationMarker>(TutorialWorld.Ground(map, new Point2D(MarkerSpot(station.Key).X, MarkerSpot(station.Key).Y)), 0))
            {
                if (!item.Deleted && item.StationKey == station.Key && item.Location != TutorialWorld.Ground(map, SignPoint(station.Key))) { moved.Add(item); }
            }
        }
        foreach (var m in moved) { m.MoveToSign(); }
        return moved.Count;
    }

    public static string Seed(bool dryRun) => SeedOn(TutorialWorld.Isle, dryRun, withHaven: true);

    /// <summary>
    /// cc-P664: seeds the isle's lessons onto <paramref name="map"/>: the Fold, or one tutorial copy (TutorialCopies seeds
    /// each on first use and after a reset). <paramref name="withHaven"/> also places Rook and the town vet in New Haven.
    /// </summary>
    public static string SeedOn(Map? map, bool dryRun, bool withHaven)
    {
        if (!IsTestShard()) { return "Tutorial: refused. Test world only; live is never seeded."; }
        if (map == null) { return "Tutorial: refused. The Fold (map 32) is not registered."; }
        var haven = HavenSpots.Map;
        if (!dryRun) { MoveOldMarkers(map); } // cc-P720: a rune on its stop becomes the sign beside it
        var markers = TutorialStations.Core.Where(s => FindMarker(s.Key, map) == null).ToArray();
        var mobiles = new (Type Type, Point3D Spot, Func<Mobile> Make)[]
        {
            (typeof(TutorialGuide), Ground(TutorialWorld.Guide), () => new TutorialGuide { Direction = Direction.West }),
            (typeof(TutorialDeathEcho), DeathSpot, () => new TutorialDeathEcho()),
            (typeof(TutorialHealer), HealerSpot, () => new TutorialHealer()),
            (typeof(Banker), BankerSpot, () => new Banker { Name = "Merrin", CantWalk = true }),
            (typeof(LeagueRegistrar), ElaraSpot, () => new LeagueRegistrar { CantWalk = true }),
            (typeof(TutorialVeterinarian), FarmVetSpot, () => new TutorialVeterinarian { Name = "Tobin", Direction = Direction.East })
        }.Where(row => FindMobile(map, row.Type, row.Spot) == null).ToList();
        var rookMissing = withHaven && TutorialRecruiter.Find() == null;
        // New Haven's own vet, when the town has none (pinned's spawn data has none there).
        var townVetMissing = withHaven && FindTownVet() == null;
        var gateMissing = !HasItem<TutorialTravelGate>(map, GateSpot);
        var goldMissing = !HasItem<TutorialWalkMark>(map, SecondMark);
        var blueMissing = !HasItem<TutorialBlueWalkTile>(map, FirstMark);
        var doorSpots = new[] { Ground(TutorialWorld.BankDoor), Ground(TutorialWorld.LodgeDoor) };
        var rehung = doorSpots.Select(p => OldDoor(map, p)).Where(d => d != null).ToArray(); // cc-P720
        var doors = doorSpots.Where(p => !HasDoor(map, p) || rehung.Any(d => d!.Map == map && OldSpot(d) == p)).ToArray();
        var boardMissing = !HasItem<TutorialLeagueBoard>(map, Ground(LeagueBoard));
        var props = Props.Where(p => !HasItem<Item>(map, Ground(p.At), i => i.ItemID == p.Id && i.Name == p.Name)).ToArray();

        // Preflight the whole requested surface before mutation. cc-P720: the markers are signposts beside the stops now
        // (impassable), on tiles no NPC, door or prop uses.
        // CanFit reads the server's actual land/statics/items. Never guess a clear tile from an offline picture.
        foreach (var point in markers.Select(s => SignSpot(s.Key)).Concat(mobiles.Select(r => r.Spot))
                     .Concat(new[] { FirstMark, SecondMark, GateSpot, GhostSpot, Ground(TutorialWorld.Beach) })
                     .Concat(doors.Where(p => !rehung.Any(d => OldSpot(d!) == p))).Concat(props.Select(p => Ground(p.At)))
                     .Concat(boardMissing ? new[] { Ground(LeagueBoard) } : []))
        {
            if (!FitsOn(point, map))
            { return $"Tutorial: refused. Isle tile {point} is blocked; nothing placed. Is P662's isle patch on the server's map?"; }
        }
        foreach (var point in (withHaven ? new[] { GateExit, HavenSpots.Rook } : Array.Empty<Point3D>()).Concat(townVetMissing ? new[] { HavenSpots.VetSpot } : []))
        {
            if (!FitsOn(point, haven))
            { return $"Tutorial: refused. New Haven tile {point} is blocked; nothing placed."; }
        }
        var farm = TutorialFarm.Seed(map, dryRun);
        if (farm.Refused) { return farm.Message; }
        if (!dryRun)
        {
            TutorialCoreTrials.EnsureHooks();
            foreach (var station in markers)
            { new TutorialStationMarker(station.Key).MoveToWorld(TutorialWorld.Ground(map, SignPoint(station.Key)), map); }
            foreach (var row in mobiles)
            { Place(row.Make(), row.Spot, map); }
            if (rookMissing) { Place(new TutorialRecruiter(), HavenSpots.Rook, haven); }
            if (townVetMissing) { Place(new TutorialVeterinarian(), HavenSpots.VetSpot, haven); }
            if (gateMissing) { new TutorialTravelGate().MoveToWorld(GateSpot, map); }
            if (blueMissing) { new TutorialBlueWalkTile().MoveToWorld(FirstMark, map); }
            if (goldMissing) { new TutorialWalkMark().MoveToWorld(SecondMark, map); }
            foreach (var d in rehung) { d!.Delete(); } // cc-P720: rehung WestCCW
            foreach (var spot in doors) { MakeDoor().MoveToWorld(spot, map); }
            if (boardMissing) { new TutorialLeagueBoard().MoveToWorld(Ground(LeagueBoard), map); }
            RemoveOldSignpost(map); // cc-P718
            foreach (var (id, at, name) in props) { new Static(id) { Name = name }.MoveToWorld(Ground(at), map); }
        }
        var tiles = (blueMissing ? 1 : 0) + (goldMissing ? 1 : 0);
        var npcs = mobiles.Count + (rookMissing ? 1 : 0) + (townVetMissing ? 1 : 0);
        return $"Tutorial{(dryRun ? " dry run" : "")}: {(dryRun ? "would place" : "placed")} {markers.Length} station markers, {npcs} NPCs/echo/vets, {(gateMissing ? 1 : 0)} moongate, {tiles} walking tiles, {doors.Length} doors, {props.Length + (boardMissing ? 1 : 0)} props. {farm.Message}";
    }

    private static Point3D OldSpot(BaseDoor d) => d.Open ? new Point3D(d.X - d.Offset.X, d.Y - d.Offset.Y, d.Z - d.Offset.Z) : d.Location;

    /// <summary>cc-P718: removes the old wall-arm chat signpost a seeded world still has. Returns how many.</summary>
    public static int RemoveOldSignpost(Map map)
    {
        var old = new List<Item>();
        foreach (var i in map.GetItemsInRange<Static>(Ground(ChatSignpost), 0))
        { if (!i.Deleted && i.ItemID == OldSignpostId && i.Name == OldChatSignpostName) { old.Add(i); } }
        foreach (var i in old) { i.Delete(); }
        return old.Count;
    }

    private static void Place(Mobile m, Point3D spot, Map map)
    {
        if (m is BaseCreature bc) { bc.Home = spot; bc.RangeHome = 0; bc.CantWalk = true; }
        m.MoveToWorld(spot, map);
    }

    /// <summary>Any vet in New Haven (60 tiles of its centre).</summary>
    public static Mobile? FindTownVet()
    {
        foreach (var m in HavenSpots.Map.GetMobilesInRange(HavenSpots.Centre, 60))
        {
            if (m is Veterinarian { Deleted: false }) { return m; }
        }
        return null;
    }
}

[SerializationGenerator(0, false)]
public partial class TutorialStationMarker : Item
{
    [SerializableField(0)] private string _stationKey;
    // cc-P720 item 3: a standing signpost with its board, named for the lesson (it was a blue rune on the stop).
    public TutorialStationMarker(string key) : base(TutorialCorePlacement.SignpostId)
    {
        _stationKey = key; Movable = false;
        Name = TutorialCorePlacement.SignName(key);
    }

    // cc-P720: a marker placed before (a rune on its stop) becomes the sign beside the stop once the world has loaded.
    [AfterDeserialization(false)] private void AsSign() => Timer.DelayCall(TimeSpan.Zero, MoveToSign);

    /// <summary>cc-P720: the signpost's art and name, at its sign spot on its own map.</summary>
    public void MoveToSign()
    {
        if (Deleted) { return; }
        ItemID = TutorialCorePlacement.SignpostId;
        Hue = 0;
        Name = TutorialCorePlacement.SignName(StationKey);
        if (Map is { } map && map != Map.Internal)
        {
            var at = TutorialWorld.Ground(map, TutorialCorePlacement.SignPoint(StationKey));
            if (Location != at) { MoveToWorld(at, map); }
        }
    }
    public override void OnDoubleClick(Mobile from)
    {
        if (from is not PlayerMobile pm || !pm.Alive || !TutorialCoreTrials.Nearby(pm, Location) ||
            TutorialStations.Find(StationKey) is not { } station) { return; }
        if (StationKey == "core.loop" && TutorialCoreTrials.IsDoing(pm, StationKey)) { TutorialCoreTrials.WatchVision(pm); }
        else { ClusterFTutorial.RunStation(pm, station); }
    }
}

/// <summary>
/// cc-P720 item 5: the League's notice board in the lodge. Double-click it to read about the League without joining: it
/// opens the League's own window, as Elara does (LeagueRegistrarGump), and finishes no lesson.
/// </summary>
[SerializationGenerator(0, false)]
public partial class TutorialLeagueBoard : Item
{
    public const string BoardName = "League notice board: read about the League";

    [Constructible]
    public TutorialLeagueBoard() : base(0x1E5E) { Movable = false; Name = BoardName; }

    public override void OnDoubleClick(Mobile from)
    {
        if (from is not PlayerMobile pm) { return; }
        if (!pm.InRange(Location, 3)) { pm.SendLocalizedMessage(500446); return; } // That is too far away.
        pm.SendGump(new LeagueRegistrarGump(pm));
    }
}

// cc-P480 Part 3: the walking lesson's two marks are full, bright floor tiles (they were a pale rune and a dark-slate
// rune, Chase 10-23). The gold one keeps its old type, so a mark placed before becomes the tile when the world loads.
[SerializationGenerator(0, false)]
public partial class TutorialWalkMark : Item
{
    [Constructible]
    public TutorialWalkMark() : base(TutorialStyle.FloorTileId) { Name = "glowing gold floor tile"; Movable = false; Hue = TutorialStyle.GoldTileHue; }
    [AfterDeserialization(false)] private void AsTile() { ItemID = TutorialStyle.FloorTileId; Hue = TutorialStyle.GoldTileHue; Name = "glowing gold floor tile"; }
}

[SerializationGenerator(0, false)]
public partial class TutorialBlueWalkTile : Item
{
    [Constructible]
    public TutorialBlueWalkTile() : base(TutorialStyle.FloorTileId) { Name = "glowing blue floor tile"; Movable = false; Hue = TutorialStyle.BlueTileHue; }
}

/// <summary>
/// cc-P663: the dock's moongate out to New Haven, the last core lesson. Walk into it or double-click it (P605 risk 3). It
/// takes the trainee's pets with them (TutorialWorld.Cross) and sets them down beside Rook. It lets through a trainee on
/// the travel lesson, one whose core is done, and anyone not in the tutorial; a trainee with earlier lessons left is told
/// which one comes next.
/// </summary>
[SerializationGenerator(0, false)]
public partial class TutorialTravelGate : Item
{
    [Constructible]
    public TutorialTravelGate() : base(0xF6C) { Name = "moongate to New Haven"; Movable = false; }
    // cc-P480: the gate keeps the moongate's own blue (it was tinted pale gray, 0x482). cc-P663: and the new name.
    [AfterDeserialization(false)] private void Unhue() { Hue = 0; Name = "moongate to New Haven"; }

    public override bool OnMoveOver(Mobile m)
    {
        if (m is PlayerMobile pm) { Timer.DelayCall(TimeSpan.Zero, () => Use(pm, Location)); }
        return true;
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (from is PlayerMobile pm && TutorialCoreTrials.Nearby(pm, Location)) { Use(pm, Location); }
    }

    /// <summary>Sends <paramref name="pm"/> through, if they may go. True when they crossed.</summary>
    public static bool Use(PlayerMobile pm, Point3D gate)
    {
        if (!pm.Alive || pm.Deleted || pm.Map == null || !pm.InRange(gate, 3)) { return false; }
        var record = ClusterFTutorial.RecordOf(pm);
        var doing = TutorialCoreTrials.IsDoing(pm, "core.travel");
        if (!doing && ClusterFTutorial.IsParticipant(pm) && record != null && !ClusterFTutorial.CoreDone(record) &&
            ClusterFTutorial.CoreLeft(record).FirstOrDefault() is { } next && next.Key != "core.travel")
        {
            ClusterFTutorial.SystemLine(pm, $"The moongate is your last lesson. Finish {next.Title} first.");
            return false;
        }
        if (!TutorialCorePlacement.FitsOn(TutorialCorePlacement.GateExit, HavenSpots.Map))
        { ClusterFTutorial.SystemLine(pm, "The place where this gate sends you is blocked. Ask a staff member for help."); return false; }
        if (!TutorialWorld.Cross(pm, TutorialCorePlacement.GateExit, HavenSpots.Map)) { return false; }
        pm.PlaySound(0x1FE);
        if (doing || ClusterFTutorial.IsParticipant(pm) && record != null && !ClusterFTutorial.IsDone(record, "core.travel"))
        {
            if (!doing) { ClusterFTutorial.RunStation(pm, TutorialStations.Find("core.travel")!); }
            TutorialCoreTrials.Observe(pm, TutorialAction.UsedGate);
        }
        if (ClusterFTutorial.IsParticipant(pm)) { TutorialRecruiter.PointTo(pm); }
        return true;
    }
}

[SerializationGenerator(0, false)]
public partial class TutorialPracticeBag : Bag
{
    public TutorialPracticeBag() { }
    // The practice bag is temporary: never accept a player's belongings into it.
    public override bool OnDragDrop(Mobile from, Item dropped) => false;
    public override bool OnDragDropInto(Mobile from, Item dropped, Point3D p) => false;
    public override void OnDoubleClick(Mobile from)
    {
        base.OnDoubleClick(from);
        if (from is PlayerMobile pm) { TutorialCoreTrials.OpenBag(pm, this); }
    }
    [AfterDeserialization(false)] private void ClearAbandonedLesson() => Delete();
}

[SerializationGenerator(0, false)]
public partial class TutorialPracticeCloak : Cloak
{
    public TutorialPracticeCloak() { Name = "blue practice cloak"; Hue = TutorialStyle.SupplyHue; LootType = LootType.Blessed; }
    [AfterDeserialization(false)] private void ClearAbandonedLesson() => Delete();
}

/// <summary>
/// cc-P718 items 2 and 5: the clothes lesson's cloak. It waits at the clothes stop for its trainee alone (cc-P728: in a
/// chest that appears for them, TutorialCloakChest), and is theirs to keep: blessed (kept on death), and Nontransferable, so it cannot be sold, traded, banked, handed over or
/// dropped (pinned Item: OnDroppedToWorld, OnDroppedInto anything but the backpack, OnDroppedToMobile; GenericSellInfo
/// .IsSellable). Nothing takes it back at graduation, the skip or leaving the isle. No bonus (the notes propose one).
/// </summary>
[SerializationGenerator(0, false)]
public partial class WakeningIsleCloak : Cloak
{
    public const string CloakName = "cloak of the Wakening Isle";

    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private Mobile? _owner;

    [Constructible]
    public WakeningIsleCloak() : this(null) { }

    public WakeningIsleCloak(Mobile? owner)
    {
        _owner = owner;
        Name = CloakName;
        Hue = TutorialStyle.SupplyHue;
        LootType = LootType.Blessed;
    }

    public override bool Nontransferable => true;

    public override void HandleInvalidTransfer(Mobile from) =>
        from.SendMessage(0x3B2, "Your cloak of the Wakening Isle is yours alone. Keep it in your backpack or wear it.");

    // Lying on the table, only its trainee (and staff) can pick it up.
    public override bool CheckLift(Mobile from, Item item, ref LRReason reject)
    {
        if (Parent == null && Owner != null && from != Owner && from.AccessLevel == AccessLevel.Player)
        {
            from.SendMessage(0x3B2, "That cloak is waiting for another trainee.");
            reject = LRReason.Inspecific;
            return false;
        }

        return base.CheckLift(from, item, ref reject);
    }

    /// <summary>The isle cloak <paramref name="pm"/> has on them (worn or anywhere in the backpack), or null.</summary>
    public static WakeningIsleCloak? On(PlayerMobile pm) =>
        pm.FindItemOnLayer(Layer.Cloak) as WakeningIsleCloak ?? pm.Backpack?.FindItemByType<WakeningIsleCloak>();

    /// <summary>
    /// A cloak lying loose on the clothes table for <paramref name="pm"/> on <paramref name="map"/> (cc-P718 set it there;
    /// since cc-P728 it comes in TutorialCloakChest, which takes such a cloak off the table), or null.
    /// </summary>
    public static WakeningIsleCloak? OnTable(PlayerMobile pm, Map map)
    {
        foreach (var c in map.GetItemsInRange<WakeningIsleCloak>(TutorialWorld.Ground(map, TutorialCorePlacement.ClothesTable), 0))
        {
            if (!c.Deleted && c.Parent == null && c.Owner == pm) { return c; }
        }
        return null;
    }

}

/// <summary>
/// cc-P728 item 3 (Chase 10-28 15:23): the clothes lesson's chest. It appears for one trainee on the table by the clothes
/// rack (or the nearest free tile beside it, when a party member's chest is on the table), with a sparkle and a sound, and
/// holds their cloak of the Wakening Isle. Only that trainee (or staff) can open it, nothing can be put in it, and it
/// vanishes once the cloak is out of it and on them (worn or in the backpack). The lesson is still done when the cloak is
/// worn. One cloak per character, as before (cc-P718): a trainee with no cloak gets the chest again, a new cloak inside,
/// when the lesson starts, restarts or its Hint and arrow is clicked; a trainee who has theirs gets none. A copy's reset
/// sweeps a chest left behind.
/// </summary>
[SerializationGenerator(0, false)]
public partial class TutorialCloakChest : BaseContainer
{
    public const string ChestName = "chest with your cloak";
    public const int ChestId = 0x0E43;    // a wooden chest
    public const int SparkleId = 0x376A;  // the sparkle it appears in
    public const int PuffId = 0x3728;     // the puff it vanishes in
    public const int ChimeSound = 0x1F5;  // pinned's sparkle sound (BaseDoor, TrappableContainer)
    public const string OthersLine = "That chest is waiting for another trainee.";
    public const string NothingInLine = "This chest only holds your cloak. Keep your things in your backpack.";

    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private Mobile? _owner;

    private TimerExecutionToken _watch;
    private int _watched;

    [Constructible]
    public TutorialCloakChest() : this(null) { }

    // Movable, but never lifted (CheckLift): pinned lets nothing be lifted out of an immovable container (CannotLift, seen
    // in the P728 bot run and probed in the test host), so an immovable chest would hold the cloak for good.
    public TutorialCloakChest(Mobile? owner) : base(ChestId)
    {
        _owner = owner;
        Name = ChestName;
        Hue = TutorialStyle.SupplyHue;
    }

    public override bool Decays => false;

    public bool IsFor(Mobile m) => m == Owner || m.AccessLevel > AccessLevel.Player;

    public override void OnDoubleClick(Mobile from)
    {
        if (!IsFor(from)) { from.SendMessage(0x3B2, OthersLine); return; }
        base.OnDoubleClick(from);
    }

    public override bool CheckLift(Mobile from, Item item, ref LRReason reject)
    {
        if (item == this && from.AccessLevel == AccessLevel.Player) { reject = LRReason.CannotLift; return false; } // it stays put
        if (item != this && !IsFor(from)) { from.SendMessage(0x3B2, OthersLine); reject = LRReason.Inspecific; return false; }
        return base.CheckLift(from, item, ref reject);
    }

    public override bool OnDragDrop(Mobile from, Item dropped) { from.SendMessage(0x3B2, NothingInLine); return false; }

    public override bool OnDragDropInto(Mobile from, Item item, Point3D p) { from.SendMessage(0x3B2, NothingInLine); return false; }

    public override bool OnStackAttempt(Mobile from, Item stack, Item dropped) { from.SendMessage(0x3B2, NothingInLine); return false; }

    public override void OnItemRemoved(Item item)
    {
        base.OnItemRemoved(item);
        // The cloak is out (lifted, still in the hand): watch until it is on the trainee, or back in here.
        if (item is WakeningIsleCloak && !_watch.Running)
        {
            _watched = 0;
            Timer.StartTimer(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), () => { if (CheckTaken() || ++_watched >= 120) { _watch.Cancel(); } }, out _watch);
        }
    }

    public override void OnAfterDelete()
    {
        base.OnAfterDelete();
        _watch.Cancel();
    }

    /// <summary>Vanishes once its cloak is out of it and on its trainee. True when it did (or is gone).</summary>
    public bool CheckTaken()
    {
        if (Deleted) { return true; }
        if (FindItemByType<WakeningIsleCloak>() != null || Owner is not PlayerMobile pm || WakeningIsleCloak.On(pm) == null) { return false; }
        Vanish();
        return true;
    }

    public void Vanish()
    {
        if (Map is { } map && map != Map.Internal)
        {
            Effects.SendLocationEffect(Location, map, PuffId, 20, 10, 0, 0);
            Effects.PlaySound(Location, map, ChimeSound);
        }
        Delete();
    }

    /// <summary>The chest waiting for <paramref name="pm"/> at the clothes table on <paramref name="map"/>, or null.</summary>
    public static TutorialCloakChest? Find(PlayerMobile pm, Map map)
    {
        foreach (var c in map.GetItemsInRange<TutorialCloakChest>(TutorialWorld.Ground(map, TutorialCorePlacement.ClothesTable), 3))
        {
            if (!c.Deleted && c.Parent == null && c.Owner == pm) { return c; }
        }
        return null;
    }

    /// <summary>
    /// One cloak per character: when <paramref name="pm"/> has none on them, their chest (with a cloak in it) is at the
    /// clothes table. A cloak still lying on the table from before cc-P728 is taken off it. Returns the waiting chest (new or
    /// old), or null when they already have their cloak.
    /// </summary>
    public static TutorialCloakChest? EnsureFor(PlayerMobile pm)
    {
        if (WakeningIsleCloak.On(pm) != null) { return null; }
        var map = TutorialWorld.MapOf(pm);
        if (map == null) { return null; }
        WakeningIsleCloak.OnTable(pm, map)?.Delete();
        if (Find(pm, map) is { } waiting)
        {
            if (waiting.FindItemByType<WakeningIsleCloak>() == null) { waiting.DropItem(new WakeningIsleCloak(pm)); }
            return waiting;
        }
        var chest = new TutorialCloakChest(pm);
        chest.DropItem(new WakeningIsleCloak(pm));
        var at = Spot(map);
        chest.MoveToWorld(at, map);
        Effects.SendLocationEffect(at, map, SparkleId, 16, 10, TutorialStyle.SupplyHue, 0);
        Effects.PlaySound(at, map, ChimeSound);
        return chest;
    }

    /// <summary>On the table, or beside it when another trainee's chest is there (a party shares a copy).</summary>
    public static Point3D Spot(Map map)
    {
        var table = TutorialWorld.Ground(map, TutorialCorePlacement.ClothesTable);
        var top = new Point3D(table.X, table.Y, table.Z + TutorialCorePlacement.TableHeight);
        if (!Taken(map, top)) { return top; }
        foreach (var (dx, dy) in new[] { (-1, 0), (0, 1), (-1, 1), (0, -1), (-1, -1), (-2, 0) })
        {
            var p = TutorialWorld.Ground(map, new Point2D(table.X + dx, table.Y + dy));
            if (!Taken(map, p) && TutorialCorePlacement.FitsOn(p, map)) { return p; }
        }
        return top;
    }

    private static bool Taken(Map map, Point3D p)
    {
        foreach (var c in map.GetItemsInRange<TutorialCloakChest>(p, 0))
        {
            if (!c.Deleted && c.Parent == null && c.Z == p.Z) { return true; }
        }
        return false;
    }
}

[SerializationGenerator(0, false)]
public partial class TutorialBankToken : Item
{
    // cc-P480 Part 6: a small pile of coins in the bright supply blue (it was a dark rune, not recognisable as the thing to move).
    public TutorialBankToken() : base(0x0EF2) { Name = "blue practice token"; Hue = TutorialStyle.SupplyHue; LootType = LootType.Blessed; }
    [AfterDeserialization(false)] private void ClearAbandonedLesson() => Delete();
}

[SerializationGenerator(0, false)]
public partial class TutorialDeathEcho : TutorialCreature
{
    [Constructible]
    public TutorialDeathEcho() : base(AIType.AI_Animal)
    {
        Name = "the pale gray rat"; Body = 0xEE; Hue = 0x482; FightMode = FightMode.None;
        CantWalk = true; SetStr(20); SetDex(20); SetInt(5); SetHits(100);
    }
    public override bool IsInvulnerable => true;
    public override bool IsEnemy(Mobile m) => false;
    public override void OnDoubleClick(Mobile from)
    {
        if (from is PlayerMobile { Alive: true } pm && TutorialCoreTrials.IsDoing(pm, "core.death") &&
            TutorialCoreTrials.Nearby(pm, Location)) { pm.SendGump(new TutorialDeathGump(this)); }
    }
}

public sealed class TutorialDeathGump : Gump
{
    private readonly TutorialDeathEcho _echo;
    public TutorialDeathGump(TutorialDeathEcho echo) : base(120, 120)
    {
        _echo = echo;
        TutorialStyle.Panel(this, 450, 180);
        AddLabel(24, 18, TutorialStyle.HeadingHue, "Death and resurrection");
        AddHtml(24, 44, 402, 76, TutorialStyle.Html("Click Ready to die for this lesson, then double-click Sera beside the pale gray rat and click Resurrect. While you are Young, your belongings return to your backpack after death."), false, false);
        AddButton(24, 130, 4023, 4025, 1); AddLabel(60, 132, TutorialStyle.ActionHue, "Ready");
        AddButton(240, 130, 4017, 4019, 0); AddLabel(276, 132, TutorialStyle.TextHue, "Not yet");
    }
    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (info.ButtonID != 1 || sender.Mobile is not PlayerMobile { Alive: true } pm || _echo.Deleted ||
            !TutorialCoreTrials.IsDoing(pm, "core.death") || !TutorialCoreTrials.Nearby(pm, _echo.Location)) { return; }
        pm.Damage(pm.Hits + 100, _echo);
    }
}

[SerializationGenerator(0, false)]
public partial class TutorialHealer : Healer
{
    [Constructible]
    public TutorialHealer() { Name = "Sera"; Title = "the tutorial healer"; CantWalk = true; }
    // cc-P666: a dead bonded pet stepping near her gets the line pointing to Tobin (the Farmhand's cooldown, shared).
    public override void OnMovement(Mobile m, Point3D oldLocation)
    {
        base.OnMovement(m, oldLocation);
        if (m is BaseCreature { IsDeadBondedPet: true } pet && InRange(pet, 4)) { TutorialFarm.NoticeGhostPet(this, pet); }
    }
    // A ghost's double-click reaches OnDoubleClickDead (pinned Mobile.Use), never OnDoubleClick. The stock offer needs a
    // step into range (BaseHealer.OnMovement), and the death lesson teleports the ghost here, so this is the way to ask.
    public override void OnDoubleClickDead(Mobile from)
    {
        if (from is PlayerMobile pm && TutorialCoreTrials.Nearby(pm, Location, 4)) { OfferResurrection(pm); }
        else { base.OnDoubleClickDead(from); }
    }
    public override void OfferResurrection(Mobile m)
    {
        if (m is not PlayerMobile { Alive: false } pm || !TutorialCoreTrials.Nearby(pm, Location, 4)) { return; }
        if (TutorialCoreTrials.IsDoing(pm, "core.death")) { TutorialCoreTrials.Observe(pm, TutorialAction.HealerOffer); }
        pm.SendGump(new TutorialResurrectionGump(this));
    }
}

public sealed class TutorialResurrectionGump : Gump
{
    private readonly TutorialHealer _healer;
    public TutorialResurrectionGump(TutorialHealer healer) : base(120, 120)
    {
        _healer = healer;
        TutorialStyle.Panel(this, 360, 140);
        AddLabel(24, 24, TutorialStyle.HeadingHue, "Ready to return to life?");
        AddButton(24, 90, 4023, 4025, 1); AddLabel(60, 92, TutorialStyle.ActionHue, "Resurrect");
    }
    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (info.ButtonID != 1 || sender.Mobile is not PlayerMobile { Alive: false } pm || _healer.Deleted ||
            !TutorialCoreTrials.Nearby(pm, _healer.Location, 4) || !pm.Map.CanFit(pm.Location, 16, false, false)) { return; }
        pm.Resurrect();
        if (pm.Alive) { TutorialCoreTrials.Observe(pm, TutorialAction.Resurrected); }
    }
}
