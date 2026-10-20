// ServUO: Services/Revamped Dungeons/Covetous Void Spawn/Level3Spawner.cs (cc-P194).
//
// The population of Covetous level 3 while the controller is active: nine spawn entries, each a set of rectangles
// and a count (8, or 15 for two of them), each rolled a void type and filled with that type's creatures at
// levels 5 to 15. When an entry's last creature dies it is re-rolled to a different type two to five minutes
// later. Every kill adds fame / 998 (split between the rightful looters) to each looter's overall Void Pool total.
//
// ServUO's spawner is a plain object implementing ISpawner, saved inline by the controller. Pinned ModernUO's
// ISpawner is an IEntity (Server/Interfaces.cs:56), so the spawner is an invisible item here, held by the
// controller and standing on its tile; the creature list is one generator list and each creature remembers its
// entry (CovetousCreature.Level3Entry), which is what ServUO's per-entry lists hold. Guid is fresh per load: the
// importer matches guids only for spawners it placed from a file.
//
// Conversion notes: Map.GetRandomSpawnPoint(rect) has no pinned helper; RandomPointIn does the same. The pinned
// ISpawner members ServUO's lacks (WalkingRange, SpawnBounds, Region, ReturnOnDeactivate, Running, Respawn) are
// answered as a spawner over level 3 would answer them.

using System;
using System.Collections.Generic;
using System.Linq;
using ModernUO.Serialization;
using Server.Collections;
using Server.Mobiles;

namespace Server.Engines.VoidPool;

[SerializationGenerator(0, false)]
public partial class Level3Spawner : Item, ISpawner
{
    public static readonly Rectangle2D Level3Bounds = new(5501, 1799, 127, 129);

    public readonly record struct EntryDefinition(Rectangle2D[] Bounds, int MaxCount);

    public static readonly EntryDefinition[] Definitions =
    {
        new(new[] { new Rectangle2D(5564, 1888, 26, 31) }, 8),
        new(new[] { new Rectangle2D(5554, 1888, 8, 28) }, 8),
        new(new[] { new Rectangle2D(5596, 1888, 8, 28) }, 8),
        new(new[] { new Rectangle2D(5536, 1876, 8, 8), new Rectangle2D(5552, 1840, 11, 42) }, 8),
        new(new[] { new Rectangle2D(5600, 1860, 8, 8), new Rectangle2D(5596, 1872, 15, 12) }, 8),
        new(new[] { new Rectangle2D(5608, 1832, 15, 15), new Rectangle2D(5616, 1848, 11, 34) }, 8),
        new(new[] { new Rectangle2D(5568, 1852, 22, 14) }, 15),
        new(new[] { new Rectangle2D(5568, 1824, 22, 11) }, 8),
        new(new[] { new Rectangle2D(5501, 1800, 42, 19) }, 15)
    };

    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private VoidPoolController _controller;

    [SerializableField(1, setter: "private")]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private bool _spawnActive;

    [SerializableField(2, getter: "private", setter: "private")]
    private List<BaseCreature> _spawned;

    private List<SpawnEntry> _spawns;

    public Level3Spawner(VoidPoolController controller) : base(3803)
    {
        Name = "Void Pool Level 3 Spawner";
        Visible = false;
        Movable = false;

        _controller = controller;
        _spawned = new List<BaseCreature>();

        Timer.DelayCall(TimeSpan.FromSeconds(1), () => Active = true);
    }

    public Guid Guid { get; private set; } = Guid.NewGuid();

    public List<SpawnEntry> Spawns => _spawns ??= new List<SpawnEntry>();

    [CommandProperty(AccessLevel.GameMaster)]
    public int EntryCount => _spawns?.Count ?? 0;

    [CommandProperty(AccessLevel.GameMaster)]
    public bool Active
    {
        get => _spawnActive;
        set
        {
            if (value == _spawnActive || Deleted)
            {
                return;
            }

            _spawnActive = value;

            if (!value)
            {
                Deactivate();
            }
            else
            {
                if (_controller != null && (Map == null || Map == Map.Internal))
                {
                    MoveToWorld(_controller.Location, _controller.Map);
                }

                if (_spawns != null)
                {
                    Deactivate(false);
                }

                LoadSpawns();
                StartSpawn();
            }
        }
    }

    // ISpawner
    public bool UnlinkOnTaming => true;
    public int WalkingRange => 20;
    public Rectangle3D SpawnBounds => Region.ConvertTo3D(Level3Bounds);
    public Region Region => Region.Find(Location, Map);
    public bool ReturnOnDeactivate => false;
    public bool Running => _spawnActive;
    public bool IsInSpawnBounds(Point3D location) => Level3Bounds.Contains(location);

    public void Remove(ISpawnable spawnable) => RemoveFromSpawner(spawnable);

    public Point3D GetSpawnPosition(ISpawnable spawned, Map map) =>
        spawned is BaseCreature bc && bc.Home != Point3D.Zero ? bc.Home : Location;

    public void Respawn() => StartSpawn();

    public override string ToString() => "...";

    public void LoadSpawns()
    {
        _spawns?.Clear();
        _spawns = new List<SpawnEntry>(Definitions.Length);

        for (var i = 0; i < Definitions.Length; i++)
        {
            _spawns.Add(new SpawnEntry(this, i, Definitions[i].Bounds, Definitions[i].MaxCount));
        }
    }

    public void RemoveFromSpawner(ISpawnable spawnable)
    {
        if (spawnable is not BaseCreature bc)
        {
            return;
        }

        var entry = FindEntryFor(bc);

        _spawned?.Remove(bc);
        bc.Spawner = null;

        if (entry == null)
        {
            return;
        }

        entry.Spawn.Remove(bc);

        if (entry.CurrentCount == 0)
        {
            Timer.DelayCall(TimeSpan.FromMinutes(Utility.RandomMinMax(2, 5)), () => Reset(entry));
        }
    }

    // ServUO awards in RemoveFromSpawner, which its Mobile.Delete reaches with the damage entries intact; pinned
    // Mobile.OnDelete reaches it after they are gone, so CovetousCreature.OnDeath calls this first.
    public void OnKilled(BaseCreature bc)
    {
        var list = BaseCreature.GetLootingRights(bc.DamageEntries, bc.HitsMax);

        foreach (var ds in list)
        {
            if (ds.m_HasRight)
            {
                var points = bc.Fame / 998 / list.Count;

                if (points > 0)
                {
                    AddToOverallTotal(ds.m_Mobile, points);
                }
            }
        }
    }

    public void AddToOverallTotal(Mobile m, int points)
    {
        if (_controller == null)
        {
            return;
        }

        var stats = VoidPoolStats.GetStats(_controller);

        stats.OverallTotal[m] = stats.OverallTotal.GetValueOrDefault(m) + points;
    }

    public void Reset(SpawnEntry entry)
    {
        if (!Active || Deleted || entry == null || _spawns == null || !_spawns.Contains(entry))
        {
            return;
        }

        var type = entry.SpawnType;

        do
        {
            entry.SpawnType = (VoidType)Utility.RandomMinMax(0, 4);
        }
        while (entry.SpawnType == type);

        entry.DoSpawn();
    }

    public void StartSpawn()
    {
        if (_spawns == null)
        {
            LoadSpawns();
        }

        foreach (var entry in _spawns)
        {
            entry.SpawnType = (VoidType)Utility.RandomMinMax(0, 4);
            entry.DoSpawn();
        }
    }

    public SpawnEntry FindEntryFor(BaseCreature bc)
    {
        if (_spawns == null)
        {
            return null;
        }

        if (bc is CovetousCreature cc && cc.Level3Entry >= 0 && cc.Level3Entry < _spawns.Count)
        {
            return _spawns[cc.Level3Entry];
        }

        return _spawns.FirstOrDefault(sp => sp.Spawn.Contains(bc));
    }

    public void Deactivate(bool deactivate = true)
    {
        if (deactivate)
        {
            _spawnActive = false;
        }

        if (_spawns != null)
        {
            foreach (var entry in _spawns)
            {
                foreach (var creature in entry.Spawn.ToList())
                {
                    creature.Delete();
                }

                entry.Spawn.Clear();
            }

            _spawns.Clear();
        }

        if (_spawned != null)
        {
            foreach (var creature in _spawned.ToList())
            {
                creature?.Delete();
            }

            _spawned.Clear();
        }
    }

    public override void OnDelete()
    {
        Deactivate();
        base.OnDelete();
    }

    internal void Track(BaseCreature bc)
    {
        _spawned ??= new List<BaseCreature>();

        if (!_spawned.Contains(bc))
        {
            _spawned.Add(bc);
        }
    }

    public static Point3D RandomPointIn(Rectangle2D rec, Map map)
    {
        for (var j = 0; j < 50; j++)
        {
            var x = rec.X + Utility.Random(Math.Max(1, rec.Width));
            var y = rec.Y + Utility.Random(Math.Max(1, rec.Height));
            var z = map.GetAverageZ(x, y);

            if (map.CanSpawnMobile(x, y, z))
            {
                return new Point3D(x, y, z);
            }
        }

        return Point3D.Zero;
    }

    [AfterDeserialization]
    private void AfterDeserialization()
    {
        _spawned ??= new List<BaseCreature>();
        _spawned.RemoveAll(bc => bc == null || bc.Deleted);

        LoadSpawns();

        foreach (var bc in _spawned)
        {
            bc.Spawner = this;

            if (bc is CovetousCreature cc && cc.Level3Entry >= 0 && cc.Level3Entry < _spawns.Count)
            {
                _spawns[cc.Level3Entry].Spawn.Add(bc);
            }
        }

        // ServUO SpawnEntry.Deserialize: an entry that came back empty spawns again.
        if (_spawnActive)
        {
            foreach (var entry in _spawns)
            {
                if (entry.CurrentCount == 0)
                {
                    var e = entry;
                    Timer.DelayCall(() => e.DoSpawn());
                }
            }
        }
    }

    public class SpawnEntry
    {
        public SpawnEntry(Level3Spawner spawner, int index, Rectangle2D[] bounds, int maxCount = 8)
        {
            Spawner = spawner;
            Index = index;
            Bounds = bounds;
            MaxCount = maxCount;
            Spawn = new List<BaseCreature>();
        }

        public Level3Spawner Spawner { get; }
        public int Index { get; }
        public Rectangle2D[] Bounds { get; }
        public int MaxCount { get; }
        public int CurrentCount => Spawn.Count;
        public VoidType SpawnType { get; set; }
        public List<BaseCreature> Spawn { get; }

        public void DoSpawn()
        {
            if (Spawner == null || Spawner.Deleted || Spawner.Controller == null || Spawner.Controller.Map == null ||
                Spawner.Controller.Map == Map.Internal || !Spawner.Active)
            {
                return;
            }

            var map = Spawner.Controller.Map;
            var types = VoidPoolController.SpawnTable[(int)SpawnType];

            for (var i = 0; i < MaxCount; i++)
            {
                if (Activator.CreateInstance(types[Utility.Random(types.Length)], Utility.RandomMinMax(5, 15), false) is not BaseCreature bc)
                {
                    continue;
                }

                var rec = Bounds[Utility.Random(Bounds.Length)];
                var p = RandomPointIn(rec, map);

                if (p != Point3D.Zero)
                {
                    bc.Spawner = Spawner;

                    bc.Home = p;
                    bc.RangeHome = 20;

                    if (bc is CovetousCreature cc)
                    {
                        cc.Level3Entry = Index;
                    }

                    bc.OnBeforeSpawn(p, map);
                    bc.MoveToWorld(p, map);
                    bc.OnAfterSpawn();

                    Spawn.Add(bc);
                    Spawner.Track(bc);
                }
                else
                {
                    bc.Delete();
                }
            }
        }
    }
}
