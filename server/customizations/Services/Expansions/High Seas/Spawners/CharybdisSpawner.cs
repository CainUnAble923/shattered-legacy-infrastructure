// cc-P269: ServUO pub57 Scripts/Services/Expansions/High Seas/Spawners/CharybdisSpawner.cs -> ours, High Seas fishing,
// second half. The one Charybdis of the world and the Oracle of the Sea's side of it:
//   - An Oracle consulted when none is summoned and the last day is up names a spot at sea (SOS.FindLocation, Trammel or
//     Felucca, the consulting player's facet), a 10 by 10 square around it, and starts a 24-hour wait.
//   - A charybdis-baited cast into that square (HighSeasFishing.TrySummonCharybdis) snags Charybdis: eleven seconds of
//     effects, then it rises where the hook went in, takes the fisher as its target and raises up to eight tentacles
//     around the fisher's boat.
//   - When it dies the spawner resets once seven hours have passed since the Oracle was read (at once if they have). If
//     the 24 hours run out with Charybdis still alive, it sinks back and the spawner resets.
// Staff: [CharydbisSpawner opens its properties (Active off stops it; on resets it).
//
// Changes from ServUO (shard-migration notes/cc-P269-high-seas-fishing-second-half.md):
// - The spawner always exists (made on first use). ServUO makes it from [DecorateHS, the command that also places the
//   High Seas decoration (Corgul's altar, the bounty boards, the fish monger spawners), which is not ported whole.
// - Saved through pinned's GenericPersistence (HighSeasPersistence), where ServUO wrote its own Saves/Highseas.bin.
// - DateTime.UtcNow is Core.Now, and the timers are pinned's Timer.StartTimer.

using System;
using Server.Commands;
using Server.Gumps;
using Server.Items;
using Server.Multis;
using Server.Spells;

namespace Server.Mobiles;

[PropertyObject]
public class CharydbisSpawner
{
    public static readonly TimeSpan KillDelay = TimeSpan.FromHours(7);
    public static readonly TimeSpan NoSpawnDelay = TimeSpan.FromHours(24);

    private static CharydbisSpawner _spawnInstance;

    private bool _active;
    private TimerExecutionToken _timer;

    public CharydbisSpawner()
    {
        Charydbis = null;
        NextSpawn = Core.Now;
        IsSummoned = false;
        HasSpawned = false;

        Active = true;
    }

    public static CharydbisSpawner SpawnInstance
    {
        get => _spawnInstance ??= new CharydbisSpawner();
        set => _spawnInstance = value;
    }

    [CommandProperty(AccessLevel.GameMaster)]
    public Charydbis Charydbis { get; private set; }

    [CommandProperty(AccessLevel.GameMaster)]
    public DateTime NextSpawn { get; set; }

    [CommandProperty(AccessLevel.GameMaster)]
    public DateTime LastAttempt { get; set; }

    [CommandProperty(AccessLevel.GameMaster)]
    public bool IsSummoned { get; set; }

    [CommandProperty(AccessLevel.GameMaster)]
    public bool HasSpawned { get; set; }

    [CommandProperty(AccessLevel.GameMaster)]
    public Map Map { get; set; }

    [CommandProperty(AccessLevel.GameMaster)]
    public Rectangle2D CurrentLocation { get; set; }

    [CommandProperty(AccessLevel.GameMaster)]
    public Rectangle2D LastLocation { get; set; }

    [CommandProperty(AccessLevel.GameMaster)]
    public bool Active
    {
        get => _active;
        set
        {
            Reset();
            NextSpawn = Core.Now;
            _active = value;
        }
    }

    public static void Initialize()
    {
        CommandSystem.Register("CharydbisSpawner", AccessLevel.Administrator, Spawner_OnCommand);
    }

    [Usage("CharydbisSpawner")]
    [Description("Opens the properties of the charybdis spawner (the Oracle of the Sea's Charybdis): Active, the spot, the wait.")]
    [ShardCommand(CommandCategory.DevTool)]
    private static void Spawner_OnCommand(CommandEventArgs e)
    {
        e.Mobile.SendGump(new PropertiesGump(e.Mobile, SpawnInstance));
    }

    public bool TrySpawnCharybdis(Mobile from)
    {
        if (!_active)
        {
            from.SendLocalizedMessage(1150198); // The spyglass goes dark, it has failed to find what you seek.
            return false;
        }

        if (IsSummoned)
        {
            if (Charydbis?.Alive == true)
            {
                var pnt = new Point3D(CurrentLocation.X + 5, CurrentLocation.Y + 5, -5);
                from.SendMessage($"The location you seek is: {GetSextantLocation(pnt)} in {Map}");
            }
            else if (HasSpawned)
            {
                from.SendMessage("The creature you seek has already been slain.");
            }
            else
            {
                from.SendLocalizedMessage(1150198); // The spyglass goes dark, it has failed to find what you seek.
            }

            return false;
        }

        if (Core.Now < NextSpawn)
        {
            from.SendLocalizedMessage(1150198); // The spyglass goes dark, it has failed to find what you seek.
            return false;
        }

        var map = from.Map;

        if (map != Map.Felucca && map != Map.Trammel)
        {
            from.SendMessage("You can only summon Charydbis in Felucca or Trammel.");
            return false;
        }

        Map = map;

        from.SendLocalizedMessage(1150190); // You peer into the spyglass, images swirl in your mind as the magic device searches.
        NextSpawn = Core.Now + NoSpawnDelay;
        IsSummoned = true;
        var p = SOS.FindLocation(map);
        from.SendMessage($"The location you seek is: {GetSextantLocation(p)} in {Map}");
        CurrentLocation = new Rectangle2D(p.X - 5, p.Y - 5, 10, 10);
        LastLocation = CurrentLocation;
        StartResetTimer(NoSpawnDelay);
        LastAttempt = Core.Now;
        return true;
    }

    public string GetSextantLocation(Point3D pnt)
    {
        int xLong = 0, yLat = 0, xMins = 0, yMins = 0;
        bool xEast = false, ySouth = false;

        if (Sextant.Format(pnt, Map, ref xLong, ref yLat, ref xMins, ref yMins, ref xEast, ref ySouth))
        {
            return $"{yLat}° {yMins}'{(ySouth ? "S" : "N")}, {xLong}° {xMins}'{(xEast ? "E" : "W")}";
        }

        return pnt.ToString();
    }

    public void SpawnCharydbis(Mobile from, Point3D pnt, Map map, BaseBoat boat)
    {
        Effects.PlaySound(pnt, map, 0x668);

        var charybdis = new Charydbis(from);
        Charydbis = charybdis;
        from.SendMessage("It seems as though you've snagged your hook.");

        Timer.StartTimer(
            TimeSpan.FromSeconds(11),
            () => charybdis.DoTeleportEffects(new Point3D(pnt.X, pnt.Y, map.GetAverageZ(pnt.X, pnt.Y)), map)
        );
        Timer.StartTimer(TimeSpan.FromSeconds(12), () => DoDelayedSpawn(charybdis, from, pnt, map, boat));
    }

    private void DoDelayedSpawn(Charydbis charybdis, Mobile from, Point3D pnt, Map map, BaseBoat boat)
    {
        if (charybdis.Deleted)
        {
            return;
        }

        charybdis.MoveToWorld(new Point3D(pnt.X, pnt.Y, map.GetAverageZ(pnt.X, pnt.Y)), map);
        charybdis.Combatant = from;

        from.SendMessage("THATS NO FISH!");

        if (boat != null)
        {
            pnt = boat.Location;
        }

        for (var i = 0; i < 8; i++)
        {
            int x = pnt.X, y = pnt.Y;

            if (TrySpawnMobile(ref x, ref y, map))
            {
                var tent = new GiantTentacle(charybdis);
                charybdis.AddTentacle(tent);
                tent.MoveToWorld(new Point3D(x, y, -5), map);
            }
        }
    }

    public static bool TrySpawnMobile(ref int x, ref int y, Map map)
    {
        for (var i = 0; map != null && i < 25; ++i)
        {
            var tx = x - 10 + Utility.Random(21);
            var ty = y - 10 + Utility.Random(21);

            var t = map.Tiles.GetLandTile(tx, ty);

            if (t.Z == -5 && (t.ID >= 0xA8 && t.ID <= 0xAB || t.ID >= 0x136 && t.ID <= 0x137) &&
                !SpellHelper.CheckMulti(new Point3D(tx, ty, -5), map))
            {
                x = tx;
                y = ty;

                return true;
            }
        }

        return false;
    }

    public void OnCharybdisKilled()
    {
        var timeout = LastAttempt + KillDelay;

        if (timeout < Core.Now)
        {
            Charydbis = null;
            Reset();
        }
        else
        {
            var ts = timeout - Core.Now;
            NextSpawn = Core.Now + ts;
            StartResetTimer(ts);
        }
    }

    public void Reset()
    {
        if (Charydbis?.Alive == true)
        {
            foreach (var mob in Charydbis.GetMobilesInRange(12))
            {
                if (mob is PlayerMobile)
                {
                    mob.SendMessage(
                        "Charydbis sinks to the depths of the ocean from which it came from...You have taken too long!"
                    );
                }
            }

            for (var x = Charydbis.X - 1; x <= Charydbis.X + 1; x++)
            {
                for (var y = Charydbis.Y - 1; y <= Charydbis.Y + 1; y++)
                {
                    var splash = Utility.RandomList(0x352D, 0x5675);
                    var p = new Point3D(x, y, Charydbis.Z);

                    Effects.SendLocationEffect(p, Charydbis.Map, splash, 16, 4);
                    Effects.PlaySound(p, Charydbis.Map, 0x364);
                }
            }

            Charydbis.Delete();
        }

        Charydbis = null;
        IsSummoned = false;
        HasSpawned = false;
        CurrentLocation = new Rectangle2D(0, 0, 0, 0);
        NextSpawn = Core.Now;

        _timer.Cancel();
    }

    private void StartResetTimer(TimeSpan delay)
    {
        _timer.Cancel();
        Timer.StartTimer(delay, Reset, out _timer);
    }

    public void Serialize(IGenericWriter writer)
    {
        writer.WriteEncodedInt(0); // version

        writer.Write(HasSpawned);
        writer.Write(LastAttempt);
        writer.Write(_active);

        writer.Write(Charydbis);
        writer.Write(NextSpawn);
        writer.Write(IsSummoned);
        writer.Write(Map);
        writer.Write(CurrentLocation);
        writer.Write(LastLocation);
    }

    public void Deserialize(IGenericReader reader)
    {
        reader.ReadEncodedInt(); // version

        HasSpawned = reader.ReadBool();
        LastAttempt = reader.ReadDateTime();
        _active = reader.ReadBool();

        Charydbis = reader.ReadEntity<Charydbis>();
        NextSpawn = reader.ReadDateTime();
        IsSummoned = reader.ReadBool();
        Map = reader.ReadMap();
        CurrentLocation = reader.ReadRect2D();
        LastLocation = reader.ReadRect2D();

        if (NextSpawn > Core.Now)
        {
            StartResetTimer(NextSpawn - Core.Now);
        }
        else
        {
            Reset();
        }
    }
}
