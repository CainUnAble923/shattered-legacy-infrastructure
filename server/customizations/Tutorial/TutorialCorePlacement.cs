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
            (typeof(LeagueRegistrar), Spot("core.league"), () => new LeagueRegistrar { CantWalk = true })
        }.Where(row => FindMobile(row.Type, row.Spot) == null).ToArray();
        var gateMissing = !HasItem<TutorialTravelGate>(GateSpot);
        var markMissing = !HasItem<TutorialWalkMark>(SecondMark);

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
            if (markMissing) { new TutorialWalkMark().MoveToWorld(SecondMark, map); }
        }
        return $"Tutorial{(dryRun ? " dry run" : "")}: {(dryRun ? "would place" : "placed")} {markers.Length} station markers, {mobiles.Length} NPCs/echo, {(gateMissing ? 1 : 0)} gate, {(markMissing ? 1 : 0)} gold mark. {farm.Message}";
    }
}

[SerializationGenerator(0, false)]
public partial class TutorialStationMarker : Item
{
    [SerializableField(0)] private string _stationKey;
    public TutorialStationMarker(string key) : base(0x1F14)
    {
        _stationKey = key; Movable = false; Hue = 0x482;
        Name = TutorialStations.Find(key)?.Title ?? key;
    }
    public override void OnDoubleClick(Mobile from)
    {
        if (from is not PlayerMobile pm || !pm.Alive || !TutorialCoreTrials.Nearby(pm, Location) ||
            TutorialStations.Find(StationKey) is not { } station) { return; }
        if (StationKey == "core.loop" && TutorialCoreTrials.IsDoing(pm, StationKey)) { TutorialCoreTrials.WatchVision(pm); }
        else { ClusterFTutorial.RunStation(pm, station); }
    }
}

[SerializationGenerator(0, false)]
public partial class TutorialWalkMark : Item
{
    [Constructible]
    public TutorialWalkMark() : base(0x1F14) { Name = "gold walking mark"; Movable = false; Hue = 0x8AF; }
}

[SerializationGenerator(0, false)]
public partial class TutorialTravelGate : Item
{
    [Constructible]
    public TutorialTravelGate() : base(0xF6C) { Name = "tutorial moongate to New Haven"; Movable = false; Hue = 0x482; }
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
    public TutorialPracticeCloak() { Name = "practice cloak"; Hue = 0x482; LootType = LootType.Blessed; }
    [AfterDeserialization(false)] private void ClearAbandonedLesson() => Delete();
}

[SerializationGenerator(0, false)]
public partial class TutorialBankToken : Item
{
    public TutorialBankToken() : base(0x1F14) { Name = "practice bank token"; Hue = 0x8AF; LootType = LootType.Blessed; }
    [AfterDeserialization(false)] private void ClearAbandonedLesson() => Delete();
}

[SerializationGenerator(0, false)]
public partial class TutorialDeathEcho : TutorialCreature
{
    [Constructible]
    public TutorialDeathEcho() : base(AIType.AI_Animal)
    {
        Name = "the death lesson echo"; Body = 0xEE; Hue = 0x482; FightMode = FightMode.None;
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
        AddBackground(0, 0, 450, 180, 9270);
        AddHtml(24, 24, 402, 80, "System: The echo will kill you for this lesson. Your corpse stays here. The healer nearby will bring you back. Ready?", false, false);
        AddButton(24, 130, 4023, 4025, 1); AddLabel(60, 132, 68, "Ready");
        AddButton(240, 130, 4017, 4019, 0); AddLabel(276, 132, 999, "Not yet");
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
    public override void OnDoubleClick(Mobile from)
    {
        if (from is PlayerMobile { Alive: false } pm && TutorialCoreTrials.Nearby(pm, Location, 4)) { OfferResurrection(pm); }
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
        AddBackground(0, 0, 360, 140, 9270);
        AddLabel(24, 24, 1154, "System: Ready to return to life?");
        AddButton(24, 90, 4023, 4025, 1); AddLabel(60, 92, 68, "Resurrect");
    }
    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (info.ButtonID != 1 || sender.Mobile is not PlayerMobile { Alive: false } pm || _healer.Deleted ||
            !TutorialCoreTrials.Nearby(pm, _healer.Location, 4) || !pm.Map.CanFit(pm.Location, 16, false, false)) { return; }
        pm.Resurrect();
        if (pm.Alive) { TutorialCoreTrials.Observe(pm, TutorialAction.Resurrected); }
    }
}
