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
        "core.paperdoll" => Ground(TutorialWorld.Rack),
        "core.chat" => Ground(TutorialWorld.Signpost),
        "core.farm" => Ground(TutorialFarm.FarmhandSpot),
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
        "core.farm" => Ground(TutorialWorld.FarmGate),
        "core.death" => Ground(TutorialWorld.Yard),
        "core.banking" => Ground(TutorialWorld.BankStop),
        "core.league" => Ground(TutorialWorld.LodgeStop),
        "core.travel" => Ground(TutorialWorld.DockEnd),
        _ => Spot(key)
    };

    // The stops' props (all stock art, server items so P662's statics stay the map's own): id, spot, name.
    public static readonly (int Id, Point2D At, string Name)[] Props =
    [
        (0x0DE3, new Point2D(TutorialWorld.Camp.X - 1, TutorialWorld.Camp.Y - 2), "campfire"),
        (0x0A55, new Point2D(TutorialWorld.Camp.X + 1, TutorialWorld.Camp.Y - 2), "bedroll"),
        (0x0A55, new Point2D(TutorialWorld.Camp.X + 2, TutorialWorld.Camp.Y - 2), "bedroll"),
        (0x0EC6, new Point2D(TutorialWorld.Rack.X, TutorialWorld.Rack.Y - 2), "clothes rack"),
        (0x0B97, new Point2D(TutorialWorld.Signpost.X, TutorialWorld.Signpost.Y - 2), "signpost: the farm, east"),
        (0x0E7C, TutorialWorld.BankBox, "practice bank box")
    ];

    public static TutorialStationMarker? FindMarker(string key, Map map)
    {
        foreach (var item in map.GetItemsInRange<TutorialStationMarker>(MarkerSpot(key), 0))
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

    /// <summary>The practice bank's and the lodge's doors, both in an east-west wall (south and north walls).</summary>
    public static BaseDoor MakeDoor() => new DarkWoodDoor(DoorFacing.WestCW);

    public static string Seed(bool dryRun)
    {
        if (!IsTestShard()) { return "Tutorial: refused. Test world only; live is never seeded."; }
        var map = TutorialWorld.Isle;
        if (map == null) { return "Tutorial: refused. The Fold (map 32) is not registered."; }
        var haven = HavenSpots.Map;
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
        var rookMissing = TutorialRecruiter.Find() == null;
        // New Haven's own vet, when the town has none (pinned's spawn data has none there).
        var townVetMissing = FindTownVet() == null;
        var gateMissing = !HasItem<TutorialTravelGate>(map, GateSpot);
        var goldMissing = !HasItem<TutorialWalkMark>(map, SecondMark);
        var blueMissing = !HasItem<TutorialBlueWalkTile>(map, FirstMark);
        var doors = new[] { Ground(TutorialWorld.BankDoor), Ground(TutorialWorld.LodgeDoor) }.Where(p => !HasDoor(map, p)).ToArray();
        var props = Props.Where(p => !HasItem<Item>(map, Ground(p.At), i => i.ItemID == p.Id && i.Name == p.Name)).ToArray();

        // Preflight the whole requested surface before mutation. Markers are passable, so NPCs can share their tile.
        // CanFit reads the server's actual land/statics/items. Never guess a clear tile from an offline picture.
        foreach (var point in markers.Select(s => MarkerSpot(s.Key)).Concat(mobiles.Select(r => r.Spot))
                     .Concat(new[] { FirstMark, SecondMark, GateSpot, GhostSpot, Ground(TutorialWorld.Beach) })
                     .Concat(doors).Concat(props.Select(p => Ground(p.At))))
        {
            if (!FitsOn(point, map))
            { return $"Tutorial: refused. Isle tile {point} is blocked; nothing placed. Is P662's isle patch on the server's map?"; }
        }
        foreach (var point in new[] { GateExit, HavenSpots.Rook }.Concat(townVetMissing ? new[] { HavenSpots.VetSpot } : []))
        {
            if (!FitsOn(point, haven))
            { return $"Tutorial: refused. New Haven tile {point} is blocked; nothing placed."; }
        }
        if (TileData.ItemTable[0x1F14].Impassable)
        { return "Tutorial: refused. The station marker art is impassable in this tiledata."; }

        var farm = TutorialFarm.Seed(dryRun);
        if (farm.Refused) { return farm.Message; }
        if (!dryRun)
        {
            TutorialCoreTrials.EnsureHooks();
            foreach (var station in markers)
            { new TutorialStationMarker(station.Key).MoveToWorld(MarkerSpot(station.Key), map); }
            foreach (var row in mobiles)
            { Place(row.Make(), row.Spot, map); }
            if (rookMissing) { Place(new TutorialRecruiter(), HavenSpots.Rook, haven); }
            if (townVetMissing) { Place(new TutorialVeterinarian(), HavenSpots.VetSpot, haven); }
            if (gateMissing) { new TutorialTravelGate().MoveToWorld(GateSpot, map); }
            if (blueMissing) { new TutorialBlueWalkTile().MoveToWorld(FirstMark, map); }
            if (goldMissing) { new TutorialWalkMark().MoveToWorld(SecondMark, map); }
            foreach (var spot in doors) { MakeDoor().MoveToWorld(spot, map); }
            foreach (var (id, at, name) in props) { new Static(id) { Name = name }.MoveToWorld(Ground(at), map); }
        }
        var tiles = (blueMissing ? 1 : 0) + (goldMissing ? 1 : 0);
        var npcs = mobiles.Count + (rookMissing ? 1 : 0) + (townVetMissing ? 1 : 0);
        return $"Tutorial{(dryRun ? " dry run" : "")}: {(dryRun ? "would place" : "placed")} {markers.Length} station markers, {npcs} NPCs/echo/vets, {(gateMissing ? 1 : 0)} moongate, {tiles} walking tiles, {doors.Length} doors, {props.Length} props. {farm.Message}";
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
    public TutorialStationMarker(string key) : base(0x1F14)
    {
        _stationKey = key; Movable = false; Hue = TutorialStyle.SupplyHue;
        Name = TutorialStations.Find(key)?.Title ?? key;
    }
    // cc-P480: markers placed before were pale gray (0x482).
    [AfterDeserialization(false)] private void Brighten() => Hue = TutorialStyle.SupplyHue;
    public override void OnDoubleClick(Mobile from)
    {
        if (from is not PlayerMobile pm || !pm.Alive || !TutorialCoreTrials.Nearby(pm, Location) ||
            TutorialStations.Find(StationKey) is not { } station) { return; }
        if (StationKey == "core.loop" && TutorialCoreTrials.IsDoing(pm, StationKey)) { TutorialCoreTrials.WatchVision(pm); }
        else { ClusterFTutorial.RunStation(pm, station); }
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
