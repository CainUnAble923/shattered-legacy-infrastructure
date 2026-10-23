// cc-P289: test-world lesson props, not a map build. No terrain, architecture or permanent decor is changed.
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
    // Fake map seam for tests; the real seeder always checks the loaded server map.
    public static Func<Point3D, bool> Fits { get; set; } = p => Map.Trammel.CanFit(p, 16, false, false);
    public static readonly Point3D FirstMark = new(3499, 2571, 14);
    public static readonly Point3D SecondMark = new(3501, 2571, 14);
    public static Point3D DeathSpot => Ground(3402, 2617);
    public static Point3D HealerSpot => Ground(3402, 2613);
    public static Point3D GhostSpot => Ground(3403, 2614);
    public static Point3D GateSpot => Ground(3402, 2615);
    public static readonly Point3D GateExit = new(3501, 2572, 14);
    // cc-P480 Part 8: a vet beside the Farmhand, and one in New Haven by the stables when the town has none.
    public static Point3D FarmVetSpot => Ground(TutorialFarm.FarmVetSpot.X, TutorialFarm.FarmVetSpot.Y);
    public static readonly Point3D NewHavenVetSpot = new(3526, 2578, 7);
    public static readonly Point3D NewHavenCentre = new(3500, 2590, 0);
    private static Point3D Ground(int x, int y) => new(x, y, Map.Trammel.GetAverageZ(x, y));
    public static Point3D Spot(string key) => key switch
    {
        "core.movement" => FirstMark,
        "core.paperdoll" => new Point3D(3499, 2572, 14),
        "core.backpack" => new Point3D(3499, 2573, 14),
        "core.chat" => new Point3D(3500, 2571, 14),
        "core.death" => DeathSpot,
        "core.banking" => ClusterFNewHavenSeeder.BankAnchor,
        "core.travel" => GateSpot,
        "core.league" => ClusterFNewHavenSeeder.RegistrarAnchor,
        "core.loop" => Ground(3402, 2619),
        _ => TutorialRecruiter.Spot
    };

    public static TutorialStationMarker? FindMarker(string key)
    {
        foreach (var item in Map.Trammel.GetItemsInRange<TutorialStationMarker>(Spot(key), 0))
        { if (!item.Deleted && item.StationKey == key) { return item; } }
        return null;
    }

    private static Mobile? FindMobile(Type type, Point3D spot)
    {
        foreach (var m in Map.Trammel.GetMobilesInRange(spot, 2))
        { if (!m.Deleted && type.IsInstanceOfType(m)) { return m; } }
        return null;
    }

    private static bool HasItem<T>(Point3D spot) where T : Item
    {
        foreach (var item in Map.Trammel.GetItemsInRange<T>(spot, 0))
        { if (!item.Deleted) { return true; } }
        return false;
    }

    public static string Seed(bool dryRun)
    {
        if (!IsTestShard()) { return "Tutorial: refused. Test world only; live is never seeded."; }
        if (!TutorialFarm.MapPatched()) { return "Tutorial: refused. Apply P276's farm map patch first."; }
        var map = Map.Trammel;
        var markers = TutorialStations.Core.Where(s => FindMarker(s.Key) == null).ToArray();
        var mobiles = new (Type Type, Point3D Spot, Func<Mobile> Make)[]
        {
            (typeof(TutorialRecruiter), TutorialRecruiter.Spot, () => new TutorialRecruiter()),
            (typeof(TutorialDeathEcho), DeathSpot, () => new TutorialDeathEcho()),
            (typeof(TutorialHealer), HealerSpot, () => new TutorialHealer()),
            (typeof(Banker), Spot("core.banking"), () => new Banker { Name = "Merrin", CantWalk = true }),
            (typeof(LeagueRegistrar), Spot("core.league"), () => new LeagueRegistrar { CantWalk = true }),
            (typeof(TutorialVeterinarian), FarmVetSpot, () => new TutorialVeterinarian { Name = "Tobin", Direction = Direction.East })
        }.Where(row => FindMobile(row.Type, row.Spot) == null).ToList();
        // New Haven's own vet, when the town has none (pinned's spawn data has none there). The farm's is out of range.
        if (FindTownVet() == null) { mobiles.Add((typeof(TutorialVeterinarian), NewHavenVetSpot, () => new TutorialVeterinarian())); }
        var gateMissing = !HasItem<TutorialTravelGate>(GateSpot);
        var goldMissing = !HasItem<TutorialWalkMark>(SecondMark);
        var blueMissing = !HasItem<TutorialBlueWalkTile>(FirstMark);

        // Preflight the whole requested surface before mutation. Markers are passable, so NPCs can share their tile.
        // CanFit reads the server's actual land/statics/items. Never guess a clear tile from an offline picture.
        foreach (var point in markers.Select(s => Spot(s.Key)).Concat(mobiles.Select(r => r.Spot))
                     .Concat(new[] { FirstMark, SecondMark, GateSpot, GateExit, GhostSpot }))
        {
            if (!Fits(point))
            { return $"Tutorial: refused. Tile {point} is blocked; nothing placed. Check the test world's map and props."; }
        }
        if (TileData.ItemTable[0x1F14].Impassable)
        { return "Tutorial: refused. The station marker art is impassable in this tiledata."; }

        var farm = TutorialFarm.Seed(dryRun);
        if (farm.Refused) { return farm.Message; }
        if (!dryRun)
        {
            TutorialCoreTrials.EnsureHooks();
            foreach (var station in markers)
            { new TutorialStationMarker(station.Key).MoveToWorld(Spot(station.Key), map); }
            foreach (var row in mobiles)
            {
                var m = row.Make();
                if (m is BaseCreature bc) { bc.Home = row.Spot; bc.RangeHome = 0; bc.CantWalk = true; }
                m.MoveToWorld(row.Spot, map);
            }
            if (gateMissing) { new TutorialTravelGate().MoveToWorld(GateSpot, map); }
            if (blueMissing) { new TutorialBlueWalkTile().MoveToWorld(FirstMark, map); }
            if (goldMissing) { new TutorialWalkMark().MoveToWorld(SecondMark, map); }
        }
        var tiles = (blueMissing ? 1 : 0) + (goldMissing ? 1 : 0);
        return $"Tutorial{(dryRun ? " dry run" : "")}: {(dryRun ? "would place" : "placed")} {markers.Length} station markers, {mobiles.Count} NPCs/echo/vets, {(gateMissing ? 1 : 0)} gate, {tiles} walking tiles. {farm.Message}";
    }

    /// <summary>Any vet in New Haven (60 tiles of its centre; the farm's, 100 tiles west, is outside).</summary>
    public static Mobile? FindTownVet()
    {
        foreach (var m in Map.Trammel.GetMobilesInRange(NewHavenCentre, 60))
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

[SerializationGenerator(0, false)]
public partial class TutorialTravelGate : Item
{
    [Constructible]
    public TutorialTravelGate() : base(0xF6C) { Name = "tutorial moongate to New Haven"; Movable = false; }
    // cc-P480: the gate keeps the moongate's own blue (it was tinted pale gray, 0x482).
    [AfterDeserialization(false)] private void Unhue() => Hue = 0;
    public override void OnDoubleClick(Mobile from)
    {
        if (from is not PlayerMobile { Alive: true } pm || !TutorialCoreTrials.Nearby(pm, Location) ||
            !TutorialCoreTrials.IsDoing(pm, "core.travel")) { return; }
        if (!TutorialCorePlacement.Fits(TutorialCorePlacement.GateExit))
        { ClusterFTutorial.SystemLine(pm, "The gate's landing is blocked. Ask a member of staff."); return; }
        pm.MoveToWorld(TutorialCorePlacement.GateExit, Map.Trammel);
        TutorialCoreTrials.Observe(pm, TutorialAction.UsedGate);
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
        Name = "the pale death echo"; Body = 0xEE; Hue = 0x482; FightMode = FightMode.None;
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
        AddHtml(24, 44, 402, 76, TutorialStyle.Html("The echo will kill you for this lesson; while you are Young, your things come back to your backpack. Sera the healer, beside it, will bring you back. Ready?"), false, false);
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
