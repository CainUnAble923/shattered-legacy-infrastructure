// ServUO: Services/Underworld/Navrey's Lair/NavreysController.cs (cc-P197; the folder is named without ServUO's
// apostrophe). Navrey Night-Eyes' lair: an invisible controller at 1054,861,-31 (Ter Mur) that builds the three
// pillars (Three at 1071,847, Six at 1039,879, Nine at 1039,850, all z 0), spawns her within ten tiles of itself
// with RangeHome 20 and lights the pillars; when she dies the pillars go Off and she returns ten minutes later.
// When all three pillars are Hot at once (NavreysPillar) the rockfall runs: for thirty seconds, four times a
// second, two boulders (0x1362-0x136D, half of them hued 0x456-0x45F) fall from above and north-west of her onto
// her, with rock and crash sounds, for 100-150 to her and 100-150 physical to everyone else within one tile (and
// their hiding is broken); she cannot walk meanwhile; UsedPillars is set on her. The pillars then go Off and come
// back On five minutes later while she lives. Once a day the three types rotate (Three to Six to Nine to Three).
//
// [GenNavrey (ServUO: Developer) builds the lair unless a controller already stands ("Navrey spawner is already
// present."); [DeleteNavrey (ours) removes the controller, which takes its pillars and its Navrey with it.
// GenerateUnderworldRooms calls GenNavery too, as ServUO's [DecorateSA does.
//
// Conversion notes. The link from her back to the lair is NavreyNightEyes.Controller, a property the controller
// sets when it spawns her and again after a load (ServUO saves the spawner reference on the creature, version 1
// there; here the creature's save is unchanged and the controller, which saves her, re-links: no save-shape
// change to a type that may be in a save, the Serialization rule). ServUO's TypeRestart getter rotates the pillar
// types when read and is read at every world save (its Serialize writes it); pinned's generated Serialize cannot
// be hooked, so the same check runs at every pillar click and every respawn (RotateIfDue), and the day's deadline
// is saved as a DateTime. ServUO's HuedEffect packet is Effects.SendMovingEffect with the same arguments;
// SendDamageToAll is inside pinned's Mobile.Damage. Map.GetSpawnPosition is ServUO's Map.cs:1159-1175, the
// SerpentNest's helper. The first MoveToWorld is in the constructor, as ServUO's.

using System;
using ModernUO.Serialization;
using Server.Commands;
using Server.Mobiles;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class NavreysController : Item
{
    public static readonly Point3D ControllerLocation = new(1054, 861, -31);

    public static readonly (PillarType Type, Point3D At)[] PillarSpots =
    {
        (PillarType.Three, new Point3D(1071, 847, 0)),
        (PillarType.Six, new Point3D(1039, 879, 0)),
        (PillarType.Nine, new Point3D(1039, 850, 0))
    };

    public static readonly TimeSpan RotationInterval = TimeSpan.FromHours(24.0);
    public static readonly TimeSpan RespawnDelay = TimeSpan.FromMinutes(10.0);
    public static readonly TimeSpan PillarResetDelay = TimeSpan.FromMinutes(5.0);
    public const int RockRainTicks = 120;
    public const int SpawnRange = 10;

    [SerializableField(0)]
    private NavreyNightEyes _navrey;

    [SerializableField(1)]
    private NavreysPillar _pillarThree;

    [SerializableField(2)]
    private NavreysPillar _pillarSix;

    [SerializableField(3)]
    private NavreysPillar _pillarNine;

    [SerializableField(4)]
    private DateTime _typeRestartAt;

    private RockRainTimer _rockRain;

    public static void Initialize()
    {
        CommandSystem.Register("GenNavrey", AccessLevel.Developer, GenNavrey_Command);
        CommandSystem.Register("DeleteNavrey", AccessLevel.Administrator, DeleteNavrey_Command);
    }

    [Usage("GenNavrey")]
    [Description("Builds Navrey Night-Eyes' lair in the Underworld: the controller, the three pillars and Navrey.")]
    [ShardCommand(CommandCategory.WorldGeneration, Rerun = CommandRerun.Skips, Shard = CommandShard.Any, NoDryRun = true, Summary = "Navrey Night-Eyes' lair in the Ter Mur Underworld: the controller, the three pillars and Navrey herself. Skips when a controller already stands.")]
    private static void GenNavrey_Command(CommandEventArgs e)
    {
        GenNavery(e.Mobile);
    }

    [Usage("DeleteNavrey")]
    [Description("Removes Navrey Night-Eyes' lair: the controller, its pillars and its Navrey.")]
    [ShardCommand(CommandCategory.WorldRemoval, Rerun = CommandRerun.DeletesAgain, Shard = CommandShard.Unverified, NoDryRun = true, Summary = "DELETES Navrey Night-Eyes' lair (the controller, the three pillars and the Navrey it spawned).")]
    private static void DeleteNavrey_Command(CommandEventArgs e)
    {
        var deleted = DeleteAll();
        e.Mobile.SendMessage($"Navrey Night-Eyes' lair removed ({deleted} controller(s)).");
    }

    public static void GenNavery(Mobile m)
    {
        if (Find() != null)
        {
            m?.SendMessage("Navrey spawner is already present.");
        }
        else
        {
            m?.SendMessage("Creating Navrey Night-Eyes Lair...");

            _ = new NavreysController();

            m?.SendMessage("Generation completed!");
        }
    }

    public static NavreysController Find()
    {
        foreach (var item in World.Items.Values)
        {
            if (item is NavreysController { Deleted: false } controller)
            {
                return controller;
            }
        }

        return null;
    }

    public static int DeleteAll()
    {
        var deleted = 0;

        while (Find() is { } controller)
        {
            controller.Delete();
            deleted++;
        }

        return deleted;
    }

    public NavreysController() : base(0x1F13)
    {
        Name = "Navrey Spawner - Do not remove !!";
        Visible = false;
        Movable = false;

        MoveToWorld(ControllerLocation, Map.TerMur);

        _pillarThree = new NavreysPillar(this, PillarType.Three);
        _pillarThree.MoveToWorld(PillarSpots[0].At, Map.TerMur);
        _pillarSix = new NavreysPillar(this, PillarType.Six);
        _pillarSix.MoveToWorld(PillarSpots[1].At, Map.TerMur);
        _pillarNine = new NavreysPillar(this, PillarType.Nine);
        _pillarNine.MoveToWorld(PillarSpots[2].At, Map.TerMur);

        // ServUO leaves the deadline unset, so its first world save rotates the types minutes after creation and
        // daily from there; the first rotation here is a day after creation (notes).
        TypeRestart = RotationInterval;

        Respawn();
    }

    public NavreysPillar[] Pillars => new[] { _pillarThree, _pillarSix, _pillarNine };

    public bool IsRockRaining => _rockRain?.Running == true;

    /// <summary>
    ///     ServUO's TypeRestart: the time until the pillar types rotate. Reading it when the day is up rotates
    ///     them (Three to Six to Nine to Three) and starts the next day, as ServUO's getter does.
    /// </summary>
    [CommandProperty(AccessLevel.GameMaster)]
    public TimeSpan TypeRestart
    {
        get
        {
            var ts = _typeRestartAt - Core.Now;

            if (ts < TimeSpan.Zero)
            {
                ts = TimeSpan.Zero;

                foreach (var pillar in Pillars)
                {
                    if (pillar == null)
                    {
                        continue;
                    }

                    if (pillar.Type != PillarType.Nine)
                    {
                        pillar.Type++;
                    }
                    else
                    {
                        pillar.Type = PillarType.Three;
                    }
                }

                TypeRestart = RotationInterval; // The timers rotate among the three stone ruins randomly once a day
            }

            return ts;
        }
        set => TypeRestartAt = Core.Now + value;
    }

    public void RotateIfDue()
    {
        _ = TypeRestart;
    }

    public bool AllPillarsHot
    {
        get
        {
            foreach (var pillar in Pillars)
            {
                if (pillar == null || pillar.Deleted || pillar.State != NavreysPillarState.Hot)
                {
                    return false;
                }
            }

            return true;
        }
    }

    public void OnNavreyKilled()
    {
        SetAllPillars(NavreysPillarState.Off);

        Timer.DelayCall(RespawnDelay, Respawn);
    }

    public void Respawn()
    {
        if (Deleted)
        {
            return;
        }

        RotateIfDue();

        var navrey = new NavreyNightEyes
        {
            Controller = this,
            RangeHome = 20
        };

        navrey.MoveToWorld(GetSpawnPosition(Map, Location, SpawnRange), Map);

        SetAllPillars(NavreysPillarState.On);

        Navrey = navrey;
    }

    public void ResetPillars()
    {
        if (_navrey?.Deleted == false && _navrey.Alive)
        {
            SetAllPillars(NavreysPillarState.On);
        }
    }

    public override void OnDelete()
    {
        base.OnDelete();

        foreach (var pillar in Pillars)
        {
            pillar?.Delete();
        }

        _navrey?.Delete();
        _rockRain?.Stop();
    }

    public void CheckPillars()
    {
        RotateIfDue();

        if (AllPillarsHot && _navrey != null)
        {
            _navrey.UsedPillars = true;

            _rockRain = new RockRainTimer(_navrey);
            _rockRain.Start();

            SetAllPillars(NavreysPillarState.Off);
            Timer.DelayCall(PillarResetDelay, ResetPillars);
        }
    }

    private void SetAllPillars(NavreysPillarState state)
    {
        foreach (var pillar in Pillars)
        {
            if (pillar?.Deleted == false)
            {
                pillar.State = state;
            }
        }
    }

    [AfterDeserialization]
    private void AfterDeserialization()
    {
        if (_navrey == null)
        {
            Timer.DelayCall(Respawn);
        }
        else
        {
            _navrey.Controller = this;
            SetAllPillars(NavreysPillarState.On);
        }
    }

    // ServUO Server/Map.cs:1159-1175, Map.GetSpawnPosition(Point3D center, int range).
    public static Point3D GetSpawnPosition(Map map, Point3D center, int range)
    {
        if (map == null)
        {
            return center;
        }

        for (var i = 0; i < 10; i++)
        {
            var x = center.X + (Utility.Random(range * 2 + 1) - range);
            var y = center.Y + (Utility.Random(range * 2 + 1) - range);
            var z = map.GetAverageZ(x, y);

            if (map.CanSpawnMobile(new Point2D(x, y), center.Z))
            {
                return new Point3D(x, y, center.Z);
            }

            if (map.CanSpawnMobile(new Point2D(x, y), z))
            {
                return new Point3D(x, y, z);
            }
        }

        return center;
    }

    private class RockRainTimer : Timer
    {
        private readonly NavreyNightEyes _navrey;
        private int _ticks;

        public RockRainTimer(NavreyNightEyes navrey) : base(TimeSpan.Zero, TimeSpan.FromSeconds(0.25))
        {
            _navrey = navrey;
            _ticks = RockRainTicks;

            _navrey.CantWalk = true;
        }

        protected override void OnTick()
        {
            _ticks--;

            var dest = _navrey.Location;
            var orig = new Point3D(dest.X - Utility.RandomMinMax(3, 4), dest.Y - Utility.RandomMinMax(8, 9), dest.Z + Utility.RandomMinMax(41, 43));
            var itemId = Utility.RandomMinMax(0x1362, 0x136D);
            var speed = Utility.RandomMinMax(5, 10);
            var hue = Utility.RandomBool() ? 0 : Utility.RandomMinMax(0x456, 0x45F);

            Effects.SendMovingEffect(orig, _navrey.Map, itemId, orig, dest, speed, 0, false, false, hue, 4);
            Effects.SendMovingEffect(orig, _navrey.Map, itemId, orig, dest, speed, 0, false, false, hue, 4);

            Effects.PlaySound(_navrey.Location, _navrey.Map, 0x15E + Utility.Random(3));
            Effects.PlaySound(_navrey.Location, _navrey.Map, Utility.RandomList(0x305, 0x306, 0x307, 0x309));

            var amount = Utility.RandomMinMax(100, 150);

            foreach (var m in _navrey.GetMobilesInRange(1))
            {
                if (m == null || !m.Alive)
                {
                    continue;
                }

                if (m is NavreyNightEyes)
                {
                    m.Damage(amount);
                }
                else
                {
                    m.RevealingAction();
                    AOS.Damage(m, amount, 100, 0, 0, 0, 0);
                }
            }

            if (_ticks == 0 || !_navrey.Alive)
            {
                _navrey.CantWalk = false;
                Stop();
            }
        }
    }
}
