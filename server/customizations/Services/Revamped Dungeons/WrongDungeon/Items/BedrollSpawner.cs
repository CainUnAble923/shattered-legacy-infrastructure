// ServUO: Services/Revamped Dungeons/WrongDungeon/Items/BedrollSpawner.cs (cc-P194).
//
// One invisible item per facet (placed by [GenWrongRevamp at 5823,601,0). Ten seconds after it is placed, and every
// two hours after that, it clears and re-lays the 80 bedrolls in the three cell blocks (upper floor, left rooms,
// right rooms) and the five mysterious tunnels outside; three tunnels lead to a random bedroll and that bedroll
// back out to the tunnel, the other two lead to a room and the nearest unlinked bedroll there leads back. A bedroll
// with no destination is a plain search (a trap, soap, or nothing: WrongBedrolls.cs).
//
// Conversion notes: the restart timer is not saved; after a load it is rebuilt from the saved next-run time (as
// ServUO does, clamped to now). The bedroll and tunnel lists are generator List<T> fields. ServUO's version-0
// upgrade (spawning the hot-item chests) never runs for new content and is not carried.

using System;
using System.Collections.Generic;
using System.Linq;
using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class BedrollSpawner : Item
{
    public static readonly TimeSpan RestartDelay = TimeSpan.FromHours(2.0);

    public static readonly Point3D[] RoomDestinations =
    {
        new(5651, 555, 20),
        new(5658, 562, 20),
        new(5666, 558, 20),
        new(5793, 594, 10),
        new(5790, 587, 10),
        new(5789, 579, 10),
        new(5790, 571, 10),
        new(5793, 563, 10),
        new(5792, 555, 10),
        new(5863, 594, 15),
        new(5864, 585, 15),
        new(5864, 578, 15),
        new(5863, 570, 15),
        new(5865, 562, 15),
        new(5868, 554, 15)
    };

    public static readonly Point3D[] OutsideTunnels =
    {
        new(5670, 550, 22),
        new(5721, 550, 20),
        new(5670, 535, 0),
        new(5864, 532, 15),
        new(5782, 536, 10)
    };

    public readonly record struct BedrollEntry(Point3D Location, bool South);

    private static readonly BedrollEntry[] _entries =
    {
        // Upper Floor Room 1
        new(new Point3D(5653, 565, 20), false),
        new(new Point3D(5651, 564, 20), false),
        new(new Point3D(5653, 561, 20), false),
        new(new Point3D(5650, 560, 20), false),
        new(new Point3D(5651, 555, 20), false),
        new(new Point3D(5651, 557, 20), false),

        // Upper Floor Room 2
        new(new Point3D(5657, 554, 20), false),
        new(new Point3D(5659, 555, 20), false),
        new(new Point3D(5661, 558, 20), false),
        new(new Point3D(5657, 559, 20), false),
        new(new Point3D(5657, 561, 20), false),
        new(new Point3D(5660, 561, 20), false),
        new(new Point3D(5658, 563, 20), false),
        new(new Point3D(5657, 565, 20), false),
        new(new Point3D(5661, 563, 20), false),

        // Upper Floor Room 3
        new(new Point3D(5666, 554, 20), false),
        new(new Point3D(5665, 557, 20), false),
        new(new Point3D(5668, 558, 20), false),
        new(new Point3D(5665, 560, 20), true),
        new(new Point3D(5668, 563, 20), false),
        new(new Point3D(5665, 565, 20), false),

        // Left Room 1
        new(new Point3D(5787, 594, 10), false),
        new(new Point3D(5789, 596, 10), false),
        new(new Point3D(5792, 596, 10), true),
        new(new Point3D(5792, 593, 10), true),
        new(new Point3D(5795, 596, 10), false),
        new(new Point3D(5795, 593, 10), false),

        // Left Room 2
        new(new Point3D(5787, 588, 10), false),
        new(new Point3D(5790, 589, 10), false),
        new(new Point3D(5791, 585, 10), true),
        new(new Point3D(5793, 589, 10), true),
        new(new Point3D(5795, 585, 10), false),

        // Left Room 3
        new(new Point3D(5787, 581, 10), false),
        new(new Point3D(5791, 577, 10), false),
        new(new Point3D(5793, 581, 10), true),
        new(new Point3D(5795, 579, 10), true),

        // Left Room 4
        new(new Point3D(5787, 570, 10), false),
        new(new Point3D(5789, 573, 10), false),
        new(new Point3D(5792, 573, 10), true),
        new(new Point3D(5792, 570, 10), true),
        new(new Point3D(5795, 570, 10), true),

        // Left Room 5
        new(new Point3D(5787, 561, 10), true),
        new(new Point3D(5788, 564, 10), false),
        new(new Point3D(5790, 562, 10), false),
        new(new Point3D(5791, 565, 10), true),
        new(new Point3D(5795, 565, 10), false),
        new(new Point3D(5795, 561, 10), false),

        // Left Room 6
        new(new Point3D(5789, 553, 10), false),
        new(new Point3D(5790, 555, 10), false),
        new(new Point3D(5791, 557, 10), true),
        new(new Point3D(5794, 557, 10), true),
        new(new Point3D(5795, 554, 10), true),

        // Right Room 1
        new(new Point3D(5861, 596, 15), false),
        new(new Point3D(5862, 593, 15), false),
        new(new Point3D(5865, 596, 15), false),
        new(new Point3D(5866, 594, 15), false),
        new(new Point3D(5867, 592, 15), false),

        // Right Room 2
        new(new Point3D(5860, 585, 15), true),
        new(new Point3D(5861, 588, 15), false),
        new(new Point3D(5864, 588, 15), true),
        new(new Point3D(5866, 585, 15), true),
        new(new Point3D(5867, 588, 15), true),

        // Right Room 3
        new(new Point3D(5860, 577, 15), true),
        new(new Point3D(5861, 580, 15), true),
        new(new Point3D(5863, 580, 15), true),
        new(new Point3D(5864, 576, 15), true),
        new(new Point3D(5866, 579, 15), true),
        new(new Point3D(5867, 576, 15), false),

        // Right Room 4
        new(new Point3D(5860, 568, 15), false),
        new(new Point3D(5861, 572, 15), true),
        new(new Point3D(5863, 569, 15), false),
        new(new Point3D(5865, 571, 15), true),
        new(new Point3D(5867, 568, 15), true),

        // Right Room 5
        new(new Point3D(5860, 562, 15), false),
        new(new Point3D(5861, 560, 15), false),
        new(new Point3D(5862, 564, 15), true),
        new(new Point3D(5864, 563, 15), false),
        new(new Point3D(5864, 561, 15), false),
        new(new Point3D(5866, 564, 15), false),
        new(new Point3D(5867, 561, 15), false)
    };

    public static IReadOnlyList<BedrollEntry> Entries => _entries;

    public static List<BedrollSpawner> Instances { get; } = new();

    [SerializableField(0)]
    private DateTime _nextRespawn;

    // The generator emits the public Bedrolls / MysteriousTunnels lists from these two fields.
    [SerializableField(1)]
    private List<WrongBedrollBase> _bedrolls;

    [SerializableField(2)]
    private List<MysteriousTunnel> _mysteriousTunnels;

    private Timer _timer;

    [Constructible]
    public BedrollSpawner() : base(3796)
    {
        Movable = false;
        Visible = false;
        _bedrolls = new List<WrongBedrollBase>();
        _mysteriousTunnels = new List<MysteriousTunnel>();

        Timer.DelayCall(TimeSpan.FromSeconds(10), CheckRespawn);
        StartTimer(RestartDelay);

        Instances.Add(this);
    }

    public override string DefaultName => $"Wrong Bedrolls Spawner {Map}";

    public void AddTunnel(MysteriousTunnel tunnel)
    {
        if (tunnel != null && !_mysteriousTunnels.Contains(tunnel))
        {
            _mysteriousTunnels.Add(tunnel);
        }
    }

    private void StartTimer(TimeSpan delay)
    {
        _timer?.Stop();
        _nextRespawn = Core.Now + delay;
        _timer = Timer.DelayCall(delay, RestartDelay, CheckRespawn);
    }

    public void CheckRespawn()
    {
        if (Deleted || Map == null || Map == Map.Internal)
        {
            return;
        }

        _nextRespawn = Core.Now + RestartDelay;

        Cleanup();

        // Bedrolls Spawn
        foreach (var entry in _entries)
        {
            WrongBedrollBase item = entry.South ? new WrongBedrollSouth() : new WrongBedrollEast();

            item.Movable = false;
            item.MoveToWorld(entry.Location, Map);
            _bedrolls.Add(item);
        }

        // Mysterious Tunnels Spawn
        for (var i = 0; i < OutsideTunnels.Length; i++)
        {
            var mt = new MysteriousTunnel();

            if (i < 3)
            {
                var mtrandom = Utility.Random(_entries.Length);
                mt.PointDest = _bedrolls[mtrandom].Location;
                _bedrolls[mtrandom].PointDest = OutsideTunnels[i];
                _bedrolls[mtrandom].BedrollSpawner = this;
            }
            else
            {
                mt.PointDest = RoomDestinations[Utility.Random(RoomDestinations.Length)];
                var bedroll = _bedrolls.FirstOrDefault(x => x.InRange(mt.PointDest, 4) && x.PointDest == Point3D.Zero);

                if (bedroll != null)
                {
                    bedroll.PointDest = OutsideTunnels[i];
                    bedroll.BedrollSpawner = this;
                }
            }

            mt.MoveToWorld(OutsideTunnels[i], Map);
            _mysteriousTunnels.Add(mt);
        }
    }

    public void Cleanup()
    {
        if (_bedrolls != null)
        {
            foreach (var bedroll in _bedrolls)
            {
                bedroll?.Delete();
            }

            _bedrolls.Clear();
        }

        if (_mysteriousTunnels != null)
        {
            foreach (var tunnel in _mysteriousTunnels)
            {
                tunnel?.Delete();
            }

            _mysteriousTunnels.Clear();
        }
    }

    public override void OnDelete()
    {
        _timer?.Stop();
        _timer = null;

        Cleanup();
        Instances.Remove(this);

        base.OnDelete();
    }

    [AfterDeserialization]
    private void AfterDeserialization()
    {
        Instances.Add(this);

        _bedrolls ??= new List<WrongBedrollBase>();
        _mysteriousTunnels ??= new List<MysteriousTunnel>();
        _bedrolls.RemoveAll(b => b == null || b.Deleted);
        _mysteriousTunnels.RemoveAll(t => t == null || t.Deleted);

        var next = _nextRespawn;

        if (next < Core.Now)
        {
            next = Core.Now;
        }

        _timer = Timer.DelayCall(next - Core.Now, RestartDelay, CheckRespawn);
    }
}
